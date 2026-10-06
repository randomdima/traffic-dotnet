using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.App.Render;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.Runtime;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace TrafficSimulation.Tests.Render;

/// <summary>
/// Breaking a car changes which picture is stretched over its quad and nothing else — which is a claim
/// about a number in an instance, and is therefore checked as one rather than by looking at a town.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P5)]
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

    /// <summary>Every car of the fleet, in index order, as a frame that shows the whole town hands them over.</summary>
    internal static int[] Every(CarFleet fleet) => Enumerable.Range(0, fleet.Count).ToArray();

    /// <summary>Every car sheet drawn at its art's own box, as though no tyre stood past it.</summary>
    static Vector2[] Unscaled(CarCatalog catalogue) => Enumerable.Repeat(Vector2.One, catalogue.SheetCount * 2).ToArray();

    static SpriteInstance[] Drawn(CarFleet fleet, CarCatalog catalogue, int firstSheet, Vector2[]? scales = null)
    {
        var into = new SpriteInstance[fleet.Count];
        var written = CarSprites.Fill(
            fleet, Every(fleet), catalogue, firstSheet, scales ?? Unscaled(catalogue), new Vector2(fleet.Count * 5f, 0f),
            new Vector2(1_000f, 1_000f), into);

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

    /// <summary>
    /// <b>A car is drawn in the run of its own level and in no other</b> (TER-7b, PHY-1a): the ground's run is drawn
    /// under the bridges over its roads and the level above's over them, so a car in both would be a car seen
    /// through a deck.
    /// </summary>
    [Fact]
    public void ACarIsDrawnInTheRunOfItsOwnLevelAlone()
    {
        var catalogue = CarCatalog.Load();
        var fleet = FleetOf(2);
        fleet.Channels[1] = TrafficSimulation.CityGen.CityPlan.RoadArrays.ChannelOf(TrafficSimulation.CityGen.CityPlan.RoadArrays.Over);
        var viewCentreM = new Vector2(fleet.Count * 5f, 0f);
        var into = new SpriteInstance[fleet.Count];

        var ground = CarSprites.Fill(
            fleet, Every(fleet), catalogue, 0, Unscaled(catalogue), viewCentreM, new Vector2(1_000f, 1_000f), into,
            TrafficSimulation.CityGen.CityPlan.RoadArrays.Ground);
        Assert.Equal([fleet.PositionM[0]], into[..ground].Select(sprite => sprite.CentreM));

        var above = CarSprites.Fill(
            fleet, Every(fleet), catalogue, 0, Unscaled(catalogue), viewCentreM, new Vector2(1_000f, 1_000f), into,
            TrafficSimulation.CityGen.CityPlan.RoadArrays.Over);
        Assert.Equal([fleet.PositionM[1]], into[..above].Select(sprite => sprite.CentreM));
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
    /// CAR-12a: <b>a car is drawn at the box it is simulated at</b> and not at the nominal car's, grown by
    /// its own sheet's margin for the tyres. A different margin per sheet, so a fill that read another
    /// sheet's — or none — is caught. The last assertion keeps the first from being vacuous: over a fleet
    /// built at one size, a fill drawing every car at that size would pass it.
    /// </summary>
    [Fact]
    public void EveryCarIsDrawnAtItsOwnFootprint()
    {
        var catalogue = CarCatalog.Load();
        var fleet = FleetOf(catalogue.SheetCount);
        var scales = new Vector2[catalogue.SheetCount * 2];
        for (var sheet = 0; sheet < scales.Length; sheet++) scales[sheet] = new Vector2(1f + (sheet * 0.01f), 1f + (sheet * 0.02f));
        var drawn = Drawn(fleet, catalogue, 0, scales);

        var sizes = new HashSet<Vector2>();
        for (var car = 0; car < fleet.Count; car++)
        {
            ref readonly var build = ref fleet.BuildOf(car);
            Assert.Equal(new Vector2(build.LengthM, build.WidthM) * 0.5f * scales[car], drawn[car].HalfSizeM);
            sizes.Add(new Vector2(build.LengthM, build.WidthM));
        }

        Assert.True(sizes.Count > 1, "every car in the fleet was built at the same size");
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
            fleet, Every(fleet), catalogue, recovery, firstBeamSheet: 7, new Vector2(0f, 0f), new Vector2(1_000f, 1_000f), into);

        return into[..written];
    }

    /// <summary>Half a millimetre of town, which is under a texel at any framing a street is watched from.</summary>
    static readonly EqualityComparer<Vector2> Near =
        EqualityComparer<Vector2>.Create((a, b) => (a - b).Length() < 5e-4f);

    static int FillFrontTyres(CarFleet fleet, float leastWidthM, Span<SpriteInstance> into) =>
        CarSprites.FillFrontTyres(
            fleet, Every(fleet), rubberSheet: 9, leastWidthM, new Vector2(fleet.Count * 5f, 0f), new Vector2(1_000f, 1_000f), into);

    static SpriteInstance[] FrontTyres(CarFleet fleet)
    {
        var into = new SpriteInstance[fleet.Count * TyreModel.SteeredWheels];
        Assert.Equal(into.Length, FillFrontTyres(fleet, leastWidthM: 0f, into));
        return into;
    }

    /// <summary>
    /// <b>A steered tyre is drawn at the very offset its impulse acts on</b>, at its build's size. Two
    /// constructions that agree are the more misleading of the two: they agree until one of them is changed,
    /// so the drawing asks the model where the wheel is rather than knowing.
    /// </summary>
    [Fact]
    public void EveryFrontTyreIsDrawnWhereItsImpulseActs()
    {
        var fleet = FleetOf(1);
        ref readonly var build = ref fleet.BuildOf(0);
        var drawn = FrontTyres(fleet);

        for (var wheel = 0; wheel < TyreModel.SteeredWheels; wheel++)
        {
            Assert.Equal(fleet.PositionM[0] + TyreModel.WheelAtM(build, wheel), drawn[wheel].CentreM);
            Assert.Equal(new Vector2(build.WheelLengthM, build.WheelWidthM) * 0.5f, drawn[wheel].HalfSizeM);
        }
    }

    /// <summary>Both front tyres are drawn at the rack's angle off the car's heading.</summary>
    [Fact]
    public void BothFrontTyresAreDrawnAtTheRacksAngle()
    {
        var fleet = FleetOf(1);
        fleet.HeadingRad[0] = 1f;
        fleet.Command[0] = new DriveCommand(0.4f, 0f, 0f, false, false);
        var drawn = FrontTyres(fleet);

        Assert.Equal(1.4f, drawn[0].HeadingRad, 1e-5f);
        Assert.Equal(1.4f, drawn[1].HeadingRad, 1e-5f);
    }

    /// <summary>A car whose tyres are narrower than the frame's least draws none, and one exactly at it draws both.</summary>
    [Fact]
    public void ATyreNarrowerThanTheFramesLeastIsLeftOut()
    {
        var fleet = FleetOf(1);
        var widthM = fleet.BuildOf(0).WheelWidthM;
        var into = new SpriteInstance[TyreModel.SteeredWheels];

        Assert.Equal(0, FillFrontTyres(fleet, MathF.BitIncrement(widthM), into));
        Assert.Equal(TyreModel.SteeredWheels, FillFrontTyres(fleet, widthM, into));
    }

    /// <summary>
    /// <b>The rear pair is painted at the very offset its impulse acts on, at its build's size, and the
    /// front pair is not painted at all</b> — under its own steered quad it would stand as a straight ghost.
    /// Probed on the strip of rubber standing past the bodywork (CAR-12): rubber at the middle of its roll and
    /// clear a few centimetres past either end, through the same metres-to-texels the sprite shader lays the
    /// quad by.
    /// </summary>
    [Fact]
    public void OnlyTheRearTyresArePaintedAndWhereTheirImpulseActs()
    {
        const float InsideM = 0.03f;
        var variant = CarCatalog.Shared.Variants[CarCatalog.Shared.Plain];
        var build = CarBuild.Of(Config, variant);
        var source = CarSheets.WithTyres(variant.SpritePath, variant.FootprintM, build, out var scale);
        var sheet = SheetAtlas.Decode(source);
        var (widthPx, heightPx) = SheetAtlas.Measure(source);
        var drawnM = variant.FootprintM * scale;

        Texel At(Vector2 atM)
        {
            var uv = (atM / drawnM) + new Vector2(0.5f);
            return sheet[((int)(uv.Y * heightPx) * widthPx) + (int)(uv.X * widthPx)];
        }

        var halfTyreM = new Vector2(build.WheelLengthM, build.WheelWidthM) * 0.5f;
        for (var wheel = 0; wheel < TyreModel.Wheels; wheel++)
        {
            var atM = TyreModel.WheelAtM(build, wheel);
            var outerM = atM + new Vector2(0f, MathF.Sign(atM.Y) * (halfTyreM.Y - InsideM));
            var pastM = new Vector2(halfTyreM.X + InsideM, 0f);

            if (wheel < TyreModel.SteeredWheels)
            {
                Assert.Equal(0, At(outerM).A);
                continue;
            }

            Assert.Equal(new Texel(0, 0, 0, 255), At(outerM));
            Assert.Equal(0, At(outerM + pastM).A);
            Assert.Equal(0, At(outerM - pastM).A);
        }
    }

    /// <summary>
    /// CAR-12a: <b>the padding moves no bodywork</b> — the quad grows by exactly what the sheet grew by, so
    /// the art is drawn at its own texels per metre and its own box, for the car and the wreck alike.
    /// </summary>
    [Fact]
    public void ATyreMarginLeavesTheArtAtItsOwnBox()
    {
        var variant = CarCatalog.Shared.Variants[CarCatalog.Shared.Plain];
        var build = CarBuild.Of(Config, variant);

        var looks = new[]
        {
            (variant.SpritePath, variant.FootprintM),
            (variant.WreckSpritePath, variant.FootprintM * variant.WreckScale),
        };
        foreach (var (path, spanM) in looks)
        {
            var source = CarSheets.WithTyres(path, spanM, build, out var scale);
            var (artWidth, artHeight) = ImageHeader.Measure(path);
            var (widthPx, heightPx) = SheetAtlas.Measure(source);
            var artPerM = new Vector2(artWidth, artHeight) / spanM;
            var drawnPerM = new Vector2(widthPx, heightPx) / (spanM * scale);

            Assert.True(widthPx > artWidth || heightPx > artHeight, $"{path}: no tyre stood past the art to pad for");
            Assert.Equal(artPerM.X, drawnPerM.X, 1e-3f);
            Assert.Equal(artPerM.Y, drawnPerM.Y, 1e-3f);
        }
    }
}
