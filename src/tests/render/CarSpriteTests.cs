using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// Breaking a car changes which picture is stretched over its quad and nothing else — which is a claim
/// about a number in an instance, and is therefore checked as one rather than by looking at a town.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class CarSpriteTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static CarFleet FleetOf(int cars)
    {
        var fleet = new CarFleet(cars, arcsPerCar: 1);
        for (var car = 0; car < cars; car++)
        {
            fleet.Add(
                default, new Vector2(car * 10f, 0f), 0f, Config.Car.MassKg, (byte)car, Config.Car.DrivenFrontShare,
                new Rng(1, (ulong)car));
        }

        return fleet;
    }

    static SpriteInstance[] Drawn(CarFleet fleet, CarCatalog catalogue, int firstSheet)
    {
        var into = new SpriteInstance[fleet.Count];
        var written = CarSprites.Fill(
            fleet, catalogue, firstSheet, Config, new Vector2(fleet.Count * 5f, 0f), new Vector2(1_000f, 1_000f), selected: -1, into);

        Assert.Equal(fleet.Count, written);
        return into;
    }

    /// <summary>
    /// The wreck sheets sit one whole fleet past the intact ones, so a variant's two looks are the same
    /// index apart for every car — which is what makes breaking one an addition rather than a lookup.
    /// </summary>
    [Fact]
    public void ABrokenCarIsDrawnWithItsOwnVariantsWreckSheet()
    {
        var catalogue = CarCatalog.Load();
        var fleet = FleetOf(2);
        fleet.Broken[1] = true;

        const int FirstSheet = 4;
        var drawn = Drawn(fleet, catalogue, FirstSheet);

        Assert.Equal((uint)(FirstSheet + 0), drawn[0].Sheet);
        Assert.Equal((uint)(FirstSheet + catalogue.Count + 1), drawn[1].Sheet);
    }

    /// <summary>PHY-5 keeps the body, so the quad does not move: only the art's own box differs, by whatever the variant carries.</summary>
    [Fact]
    public void AWreckStandsWhereTheCarStoodAtTheWreckArtsOwnSize()
    {
        var catalogue = CarCatalog.Load();
        var fleet = FleetOf(1);
        var intact = Drawn(fleet, catalogue, 0)[0];

        fleet.Broken[0] = true;
        var wrecked = Drawn(fleet, catalogue, 0)[0];

        Assert.Equal(intact.CentreM, wrecked.CentreM);
        Assert.Equal(intact.HeadingRad, wrecked.HeadingRad);
        Assert.Equal(intact.HalfSizeM * catalogue.Variants[0].WreckScale, wrecked.HalfSizeM);
    }

    /// <summary>Every variant names a wreck sheet, and it is a different file from the car's — the fallback is for art that is missing.</summary>
    [Fact]
    public void EveryShippedVariantCarriesItsOwnWreckArt()
    {
        foreach (var variant in CarCatalog.Load().Variants)
        {
            Assert.NotEqual(variant.SpritePath, variant.WreckSpritePath);
            Assert.True(File.Exists(variant.WreckSpritePath), $"{variant.Id} names wreck art that is not on disk.");
        }
    }

    static SpriteInstance[] Wheels(CarFleet fleet)
    {
        var into = new SpriteInstance[fleet.Count * TyreModel.Wheels];
        var written = CarSprites.FillWheels(
            fleet, Config, treadSheet: 9, Config.Tyre.TreadPitchM, new Vector2(fleet.Count * 5f, 0f),
            new Vector2(1_000f, 1_000f), into);

        Assert.Equal(into.Length, written);
        return into;
    }

    /// <summary>
    /// <b>A tyre is drawn at the very offset its impulse acts on.</b> Two constructions that agree are
    /// the more misleading of the two: they agree until one of them is changed, so the drawing asks the
    /// model where the wheel is rather than knowing.
    /// </summary>
    [Fact]
    public void EveryTyreIsDrawnWhereItsImpulseActs()
    {
        var fleet = FleetOf(1);
        var drawn = Wheels(fleet);

        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            var atBody = TyreModel.WheelAtM(Config, wheel);
            Assert.Equal(fleet.PositionM[0] + atBody, drawn[wheel].CentreM);
            Assert.Equal(new Vector2(Config.Tyre.WheelLengthM, Config.Tyre.WheelWidthM) * 0.5f, drawn[wheel].HalfSizeM);
        }
    }

    /// <summary>The front pair is drawn at its own Ackermann angles and the rear pair along the body, which is what the tyres are working at.</summary>
    [Fact]
    public void TheFrontTyresAreDrawnAtTheAngleTheyAreWorkingAt()
    {
        var fleet = FleetOf(1);
        fleet.Command[0] = new DriveCommand(0.4f, 0f, 0f, false, false);
        var drawn = Wheels(fleet);

        Span<float> steerRad = stackalloc float[TyreModel.Wheels];
        TyreModel.Ackermann(Config, 0.4f, steerRad);

        Assert.Equal(steerRad[0], drawn[0].HeadingRad, 1e-5f);
        Assert.Equal(steerRad[1], drawn[1].HeadingRad, 1e-5f);
        Assert.True(drawn[0].HeadingRad > drawn[1].HeadingRad, "the inner wheel is on the tighter lock");
        Assert.Equal(0f, drawn[2].HeadingRad);
        Assert.Equal(0f, drawn[3].HeadingRad);
    }

    /// <summary>
    /// The tread tiles along the roll and rolling it is where the slice is taken from: a wheel's length
    /// is several pitches of the picture, and its phase is an offset into it.
    /// </summary>
    [Fact]
    public void TheTreadIsTakenFromFurtherAlongThePictureAsTheWheelTurns()
    {
        var fleet = FleetOf(1);
        fleet.TreadPhaseM[1] = Config.Tyre.TreadPitchM * 0.5f;
        var drawn = Wheels(fleet);

        Assert.Equal(Config.Tyre.WheelLengthM / Config.Tyre.TreadPitchM, drawn[0].UvSize.X, 1e-4f);
        Assert.Equal(1f, drawn[0].UvSize.Y);
        Assert.Equal(0f, drawn[0].UvMin.X);
        Assert.Equal(-0.5f, drawn[1].UvMin.X, 1e-4f);
    }

    /// <summary>
    /// <b>The pitch the phase is wrapped into is the shipped picture's own.</b> The sheet is one pitch
    /// of tread laid across the full width of a tyre, so its aspect carries the figure — and wrapped
    /// into anything else the pattern snaps back part of a block several times a revolution.
    /// </summary>
    [Fact]
    public void TheTreadPitchIsThePicturesOwnPeriod()
    {
        using var tread = SixLabors.ImageSharp.Image.Load(ProjectPaths.TreadFile());
        var pitchM = Config.Tyre.WheelWidthM * tread.Width / tread.Height;

        Assert.Equal(pitchM, Config.Tyre.TreadPitchM, 1e-3f);
    }
}
