using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using SixLabors.ImageSharp.PixelFormats;
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
        var fleet = new CarFleet(cars, arcsPerCar: 1, CarBuilds.OfTheFleet(Config, CarCatalog.Shared));
        for (var car = 0; car < cars; car++)
        {
            fleet.Add(default, new Vector2(car * 10f, 0f), 0f, (byte)car, false, new Rng(1, (ulong)car));
        }

        return fleet;
    }

    static SpriteInstance[] Drawn(CarFleet fleet, CarCatalog catalogue, int firstSheet)
    {
        var into = new SpriteInstance[fleet.Count];
        var written = CarSprites.Fill(
            fleet, catalogue, firstSheet, new Vector2(fleet.Count * 5f, 0f), new Vector2(1_000f, 1_000f), into);

        Assert.Equal(fleet.Count, written);
        return into;
    }

    /// <summary>
    /// The wreck sheets sit one whole sheet list past the intact ones, so a variant's two looks are the
    /// same index apart for every car — which is what makes breaking one an addition rather than a lookup.
    /// <b>Every look and not only the fleet's</b>: the service vehicles share the list
    /// (<see cref="CarCatalog.SheetCount"/>), so an ambulance's wreck is that same stride from its van.
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
        Assert.Equal((uint)(FirstSheet + catalogue.SheetCount + 1), drawn[1].Sheet);
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

    /// <summary>
    /// Every variant that can break names a wreck sheet of its own, and it is a different file from the
    /// car's — the fallback is for art that is missing. A variant PHY-4b says never breaks names none,
    /// because there is no state for that art to be drawn in.
    /// </summary>
    [Fact]
    public void EveryShippedVariantThatCanBreakCarriesItsOwnWreckArt()
    {
        foreach (var variant in CarCatalog.Load().Variants)
        {
            if (variant.Unbreakable)
            {
                Assert.Equal(variant.SpritePath, variant.WreckSpritePath);
                continue;
            }

            Assert.NotEqual(variant.SpritePath, variant.WreckSpritePath);
            Assert.True(File.Exists(variant.WreckSpritePath), $"{variant.Id} names wreck art that is not on disk.");
        }
    }

    /// <summary>
    /// CAR-12a: <b>a car is drawn at the box it is simulated at</b> and not at the nominal car's. The
    /// second assertion is the one that bites: drawn at one size for all of them the first would pass for
    /// whichever variant happens to be the nominal size.
    /// </summary>
    [Fact]
    public void EveryCarIsDrawnAtItsOwnFootprint()
    {
        var catalogue = CarCatalog.Load();
        var fleet = FleetOf(catalogue.SheetCount);
        var drawn = Drawn(fleet, catalogue, 0);

        var sizes = new HashSet<Vector2>();
        for (var car = 0; car < fleet.Count; car++)
        {
            ref readonly var build = ref fleet.BuildOf(car);
            Assert.Equal(new Vector2(build.LengthM, build.WidthM) * 0.5f, drawn[car].HalfSizeM);
            sizes.Add(drawn[car].HalfSizeM);
        }

        Assert.True(sizes.Count > 1, "every car in the fleet was drawn at the same size");
    }

    /// <summary>
    /// CAR-12: <b>every variant's tyres stand outside its own bodywork</b>, measured against the picture the
    /// track was authored from rather than against another number in the same file. A tyre tucked under the
    /// panels is invisible from above and is four impulses acting on a base narrower than the car looks.
    /// </summary>
    /// <remarks>
    /// The bodywork is the <em>median</em> of the silhouette across a band at the axle and not its widest
    /// point: a wing mirror is a spike a tenth of a metre long, and a car does not corner on its mirrors.
    /// </remarks>
    [Fact]
    public void EveryVariantsTyresShowPastItsOwnBodywork()
    {
        var mustShowM = Config.Tyre.WheelWidthM * Config.Tyre.ShowsPastTheBodyworkShare;

        foreach (var variant in CarCatalog.Load().Variants)
        {
            using var art = SixLabors.ImageSharp.Image.Load<Rgba32>(variant.SpritePath);
            var pixelsPerMetre = art.Width / variant.FootprintM.X;

            foreach (var axleM in (float[])[variant.FrontAxleM, variant.RearAxleM])
            {
                var bodyM = FlankAtM(art, pixelsPerMetre, (variant.FootprintM.X * 0.5f) + axleM);
                var showsM = variant.HalfTrackM + (Config.Tyre.WheelWidthM * 0.5f) - bodyM;

                Assert.True(
                    showsM >= mustShowM,
                    $"{variant.Id} shows {showsM * 100f:F0} mm of tyre past {bodyM * 2f:F2} m of bodywork at " +
                    $"{axleM:+0.00;-0.00} m, and owes {mustShowM * 100f:F0} mm on a {variant.HalfTrackM * 2f:F2} m track");
            }
        }
    }

    /// <summary>
    /// CAR-12: <b>no sheet carries pixel dust</b> — a speck of opaque colour standing on its own, off the
    /// body it was cut from. A wreck's shed panel is a picture of something; four pixels in the middle of
    /// nowhere is a crop that was not cleaned up, and at the framings a street is watched from it reads as
    /// a bright fleck beside the car.
    /// </summary>
    [Fact]
    public void NoCarSheetCarriesPixelDust()
    {
        foreach (var variant in CarCatalog.Load().Variants)
        {
            foreach (var sheet in (string[])[variant.SpritePath, variant.WreckSpritePath])
            {
                using var art = SixLabors.ImageSharp.Image.Load<Rgba32>(sheet);
                foreach (var speck in Islands(art))
                {
                    Assert.True(
                        speck.Size >= DustPx,
                        $"{Path.GetFileName(sheet)} carries {speck.Size} loose pixels at ({speck.X}, {speck.Y})");
                }
            }
        }
    }

    /// <summary>Below this an island is not a part of anything, it is a leftover.</summary>
    const int DustPx = 8;

    /// <summary>
    /// Every opaque island on a sheet but the body itself, as its size and where it starts. The body is the
    /// biggest of them and is dropped, so what comes back is whatever else the picture is carrying.
    /// </summary>
    static List<(int Size, int X, int Y)> Islands(SixLabors.ImageSharp.Image<Rgba32> art)
    {
        var seen = new bool[art.Width * art.Height];
        var stack = new Stack<int>();
        var found = new List<(int Size, int X, int Y)>();

        for (var start = 0; start < seen.Length; start++)
        {
            if (seen[start] || art[start % art.Width, start / art.Width].A <= 128) continue;

            stack.Push(start);
            seen[start] = true;
            var size = 0;
            while (stack.Count > 0)
            {
                var at = stack.Pop();
                size++;
                var x = at % art.Width;
                var y = at / art.Width;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= art.Width || ny >= art.Height) continue;

                        var next = (ny * art.Width) + nx;
                        if (seen[next] || art[nx, ny].A <= 128) continue;

                        seen[next] = true;
                        stack.Push(next);
                    }
                }
            }

            found.Add((size, start % art.Width, start / art.Width));
        }

        found.Sort((a, b) => b.Size.CompareTo(a.Size));
        if (found.Count > 0) found.RemoveAt(0);
        return found;
    }

    /// <summary>Half the width of the panels at one place along the car, in metres, off the art's own alpha.</summary>
    static float FlankAtM(SixLabors.ImageSharp.Image<Rgba32> art, float pixelsPerMetre, float alongM)
    {
        const float BandM = 0.12f;
        var from = Math.Max(0, (int)MathF.Round((alongM - BandM) * pixelsPerMetre));
        var to = Math.Min(art.Width - 1, (int)MathF.Round((alongM + BandM) * pixelsPerMetre));
        var middle = (art.Height - 1) * 0.5f;

        var flanks = new List<float>();
        for (var column = from; column <= to; column++)
        {
            var widest = -1f;
            for (var row = 0; row < art.Height; row++)
            {
                if (art[column, row].A <= 24) continue;

                widest = MathF.Max(widest, MathF.Abs(row - middle) + 0.5f);
            }

            if (widest > 0f) flanks.Add(widest / pixelsPerMetre);
        }

        flanks.Sort();
        return flanks.Count > 0 ? flanks[flanks.Count / 2] : 0f;
    }

    /// <summary>
    /// EVA-5: <b>a vehicle with nothing on the arm stows it along its own deck</b>, drawn in on its own
    /// picture, hinged where its file says and pointing back down the body. It is the state an evacuator
    /// spends most of a run in.
    /// </summary>
    [Fact]
    public void AStowedTowArmIsDrawnInAndLiesBackAlongTheDeck()
    {
        var catalogue = CarCatalog.Load();
        var fleet = OneEvacuator(catalogue, headingRad: 0.4f, out var beam);

        var drawn = Beams(fleet, catalogue, new RecoveryDuty(fleet.Count));

        var back = -Heading.Unit(0.4f);
        Assert.Single(drawn);
        Assert.Equal(back, Heading.Unit(drawn[0].HeadingRad), Near);
        Assert.Equal(beam.Collapsed.SizeM * 0.5f, drawn[0].HalfSizeM);
        Assert.Equal((uint)(7 + catalogue.BeamSlotOf(catalogue.Evacuator, towing: false)), drawn[0].Sheet);

        // The hinge sits at the pivot its file names, whichever way the quad's own middle fell.
        Assert.Equal(HingeOf(fleet, beam), drawn[0].CentreM + (back * beam.Collapsed.HingeAtM), Near);

        // And the arm it stows is the one that stays on the truck: an arm drawn in that hung a metre past
        // the tail would be parked on the pavement behind every depot bay.
        var behindTheMiddleM = -beam.PivotM.X - beam.Collapsed.HingeAtM + (beam.Collapsed.SizeM.X * 0.5f);
        Assert.InRange(behindTheMiddleM - (catalogue.Variants[catalogue.Evacuator].FootprintM.X * 0.5f), 0f, 0.3f);
    }

    /// <summary>
    /// <b>And one with a wreck on it reaches out, pointing just inside that wreck's nose</b> — the place the
    /// coupling has hold of (<see cref="TowBar.ForkM"/>), so the arm on screen cannot point somewhere the
    /// tow is not spent, nor be drawn in while it is holding something at full reach.
    /// </summary>
    [Fact]
    public void ATowingArmReachesOutAtTheForkItIsHolding()
    {
        var catalogue = CarCatalog.Load();
        var fleet = OneEvacuator(catalogue, headingRad: 0f, out var beam);
        fleet.Add(default, new Vector2(-6f, 3f), 0.9f, 0, false, new Rng(1, 2));

        var recovery = new RecoveryDuty(fleet.Count);
        recovery.Towing[0] = 1;
        recovery.HeldByTheTail[1] = true;
        var drawn = Beams(fleet, catalogue, recovery);

        // By the tail, so the fork the arm points at is under that car's back end and not its front: the
        // picture follows which end the tow actually has hold of.
        var forkM = TowBar.ForkM(fleet.BuildOf(1), fleet.PositionM[1], Heading.Unit(0.9f), byTheTail: true);
        var hingeM = HingeOf(fleet, beam);
        var pointing = Vector2.Normalize(forkM - hingeM);

        Assert.Single(drawn);
        Assert.Equal(MathF.Atan2(pointing.Y, pointing.X), drawn[0].HeadingRad, 1e-5f);
        Assert.Equal(beam.Extended.SizeM * 0.5f, drawn[0].HalfSizeM);
        Assert.Equal((uint)(7 + catalogue.BeamSlotOf(catalogue.Evacuator, towing: true)), drawn[0].Sheet);
        Assert.Equal(hingeM - (pointing * beam.Extended.HingeAtM), drawn[0].CentreM, Near);
    }

    /// <summary>A wrecked recovery vehicle wears its own crumpled picture, arm and all, so no arm is drawn over it.</summary>
    [Fact]
    public void AWreckedRecoveryVehicleDrawsNoArmOverItsOwnWreckArt()
    {
        var catalogue = CarCatalog.Load();
        var fleet = OneEvacuator(catalogue, headingRad: 0f, out _);
        fleet.Broken[0] = true;

        Assert.Empty(Beams(fleet, catalogue, new RecoveryDuty(fleet.Count)));
    }

    static CarFleet OneEvacuator(CarCatalog catalogue, float headingRad, out CarTowBeam beam)
    {
        beam = catalogue.BeamOf(catalogue.Evacuator)!.Value;
        var fleet = new CarFleet(2, arcsPerCar: 1, CarBuilds.OfTheFleet(Config, catalogue));
        fleet.Add(default, new Vector2(2f, 5f), headingRad, (byte)catalogue.Evacuator, false, new Rng(1, 1));
        return fleet;
    }

    static Vector2 HingeOf(CarFleet fleet, in CarTowBeam beam)
    {
        var forward = Heading.Unit(fleet.HeadingRad[0]);
        return fleet.PositionM[0] + (forward * beam.PivotM.X) + (Heading.RightOf(forward) * beam.PivotM.Y);
    }

    static SpriteInstance[] Beams(CarFleet fleet, CarCatalog catalogue, RecoveryDuty recovery)
    {
        var into = new SpriteInstance[fleet.Count];
        var written = CarSprites.FillBeams(
            fleet, catalogue, recovery, firstBeamSheet: 7, new Vector2(0f, 0f), new Vector2(1_000f, 1_000f), into);

        return into[..written];
    }

    /// <summary>Half a millimetre of town, which is under a texel at any framing a street is watched from.</summary>
    static readonly EqualityComparer<Vector2> Near =
        EqualityComparer<Vector2>.Create((a, b) => (a - b).Length() < 5e-4f);

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
            var atBody = TyreModel.WheelAtM(fleet.BuildOf(0), wheel);
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
        TyreModel.Ackermann(fleet.BuildOf(0), 0.4f, steerRad);

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
