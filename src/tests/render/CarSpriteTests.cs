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
            fleet.Add(new Vector2(car * 10f, 0f), 0f, (byte)car, false, new Rng(1, (ulong)car));
        }

        return fleet;
    }

    /// <summary>Every car of the fleet, in index order, as a frame that shows the whole town hands them over.</summary>
    internal static int[] Every(CarFleet fleet) => Enumerable.Range(0, fleet.Count).ToArray();

    static SpriteInstance[] Drawn(CarFleet fleet, CarCatalog catalogue, int firstSheet)
    {
        var into = new SpriteInstance[fleet.Count];
        var written = CarSprites.Fill(
            fleet, Every(fleet), catalogue, firstSheet, new Vector2(fleet.Count * 5f, 0f),
            new Vector2((fleet.Count * 10f) + 100f, 1_000f), into);

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
            fleet, Every(fleet), catalogue, 0, viewCentreM, new Vector2(1_000f, 1_000f), into,
            TrafficSimulation.CityGen.CityPlan.RoadArrays.Ground);
        Assert.Equal([fleet.PositionM[0]], into[..ground].Select(sprite => sprite.CentreM));

        var above = CarSprites.Fill(
            fleet, Every(fleet), catalogue, 0, viewCentreM, new Vector2(1_000f, 1_000f), into,
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
        ref readonly var build = ref fleet.BuildOf(0);
        var intactHalfM = new Vector2(build.LengthM, build.WidthM) * 0.5f;
        Assert.Equal(SpriteInstance.AsDrawn(intactHalfM), intact.HalfSizeM);
        Assert.Equal(SpriteInstance.AsDrawn(intactHalfM * catalogue.Variants[0].WreckScale), wrecked.HalfSizeM);
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
    /// CAR-12a: <b>a car is drawn at the box it is simulated at</b> and not at the nominal car's. The last assertion
    /// keeps the first from being vacuous: over a fleet built at one size, a fill drawing every car at that size would
    /// pass it.
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
            Assert.Equal(SpriteInstance.AsDrawn(new Vector2(build.LengthM, build.WidthM) * 0.5f), drawn[car].HalfSizeM);
            sizes.Add(new Vector2(build.LengthM, build.WidthM));
        }

        Assert.True(sizes.Count > 1, "every car in the fleet was built at the same size");
    }

    /// <summary>
    /// CAR-12: <b>every variant's tyres show their share past its own bodywork, and no more</b>, measured
    /// against the picture the track was authored from rather than against another number in the same file.
    /// A tyre tucked under the panels is invisible from above and is four impulses acting on a base narrower
    /// than the car looks; one standing well clear is a car on outriggers.
    /// </summary>
    /// <remarks>
    /// The bodywork is the <em>median</em> of the silhouette across a band at the axle and not its widest
    /// point: a wing mirror is a spike a tenth of a metre long, and a car does not corner on its mirrors. The
    /// share is held at the wider of the two axles, the one track serving both, and to a texel either way.
    /// </remarks>
    [Fact]
    public void EveryVariantsTyresShowTheirShareOfTyrePastItsOwnBodywork()
    {
        var owedM = Config.Tyre.WheelWidthM * Config.Tyre.ShowsPastTheBodyworkShare;

        foreach (var variant in CarCatalog.Load().Variants)
        {
            using var art = SixLabors.ImageSharp.Image.Load<Rgba32>(variant.SpritePath);
            var pixelsPerMetre = art.Width / variant.FootprintM.X;
            var middleM = variant.FootprintM.X * 0.5f;
            var bodyM = MathF.Max(
                FlankAtM(art, pixelsPerMetre, middleM + variant.FrontAxleM),
                FlankAtM(art, pixelsPerMetre, middleM + variant.RearAxleM));
            var showsM = variant.HalfTrackM + (Config.Tyre.WheelWidthM * 0.5f) - bodyM;

            Assert.True(
                MathF.Abs(showsM - owedM) <= 1f / pixelsPerMetre,
                $"{variant.Id} shows {showsM * 1000f:F0} mm of tyre past {bodyM * 2f:F2} m of bodywork, and owes " +
                $"{owedM * 1000f:F0} mm on a {variant.HalfTrackM * 2f:F2} m track");
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

    /// <summary>
    /// Half the width of the panels at one place along the car, in metres, off the art's own alpha: the median
    /// span of the five columns about it, a texel half covered being inside, as <c>qq sprites</c> measures it.
    /// </summary>
    static float FlankAtM(SixLabors.ImageSharp.Image<Rgba32> art, float pixelsPerMetre, float alongM)
    {
        const int BandPx = 2;
        var at = (int)MathF.Round(alongM * pixelsPerMetre);

        var spans = new List<int>();
        for (var column = Math.Max(0, at - BandPx); column <= Math.Min(art.Width - 1, at + BandPx); column++)
        {
            int first = -1, last = -1;
            for (var row = 0; row < art.Height; row++)
            {
                if (art[column, row].A < 128) continue;

                if (first < 0) first = row;
                last = row;
            }

            if (first >= 0) spans.Add(last + 1 - first);
        }

        spans.Sort();
        return spans.Count > 0 ? spans[spans.Count / 2] * 0.5f / pixelsPerMetre : 0f;
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

        var drawn = Beams(fleet, catalogue, new TowHitches(fleet.Count, wagons: 0));

        var back = -Heading.Unit(0.4f);
        Assert.Single(drawn);
        Assert.Equal(back, Heading.Unit(drawn[0].HeadingRad), Near);
        Assert.Equal(SpriteInstance.AsDrawn(beam.Collapsed.SizeM * 0.5f), drawn[0].HalfSizeM);
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
        fleet.Add(new Vector2(-6f, 3f), 0.9f, 0, false, new Rng(1, 2));

        var hitches = new TowHitches(fleet.Count, wagons: 0);
        hitches.Hitch(0, 1, byTheTail: true, TowBar.ForkAlongM(fleet.BuildOf(1).TowGripFromTheMiddleM, byTheTail: true));
        var drawn = Beams(fleet, catalogue, hitches);

        // By the tail, so the fork the arm points at is under that car's back end and not its front: the
        // picture follows which end the tow actually has hold of.
        var forkM = TowBar.ForkM(fleet.BuildOf(1), fleet.PositionM[1], Heading.Unit(0.9f), byTheTail: true);
        var hingeM = HingeOf(fleet, beam);
        var pointing = Vector2.Normalize(forkM - hingeM);

        Assert.Single(drawn);
        Assert.Equal(MathF.Atan2(pointing.Y, pointing.X), drawn[0].HeadingRad, 1e-5f);
        Assert.Equal(SpriteInstance.AsDrawn(beam.Extended.SizeM * 0.5f), drawn[0].HalfSizeM);
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

        Assert.Empty(Beams(fleet, catalogue, new TowHitches(fleet.Count, wagons: 0)));
    }

    static CarFleet OneEvacuator(CarCatalog catalogue, float headingRad, out CarTowBeam beam)
    {
        beam = catalogue.BeamOf(catalogue.Evacuator)!.Value;
        var fleet = new CarFleet(2, arcsPerCar: 1, CarBuilds.OfTheFleet(Config, catalogue));
        fleet.Add(new Vector2(2f, 5f), headingRad, (byte)catalogue.Evacuator, false, new Rng(1, 1));
        return fleet;
    }

    static Vector2 HingeOf(CarFleet fleet, in CarTowBeam beam)
    {
        var forward = Heading.Unit(fleet.HeadingRad[0]);
        return fleet.PositionM[0] + (forward * beam.PivotM.X) + (Heading.RightOf(forward) * beam.PivotM.Y);
    }

    static SpriteInstance[] Beams(CarFleet fleet, CarCatalog catalogue, TowHitches hitches)
    {
        var into = new SpriteInstance[fleet.Count];
        var written = CarSprites.FillBeams(
            fleet, Every(fleet), catalogue, hitches, [], firstBeamSheet: 7, Config.View.CarTiltEyeHeightM, new Vector2(0f, 0f),
            new Vector2(1_000f, 1_000f), into);

        return into[..written];
    }

    /// <summary>Half a millimetre of town, which is under a texel at any framing a street is watched from.</summary>
    static readonly EqualityComparer<Vector2> Near =
        EqualityComparer<Vector2>.Create((a, b) => (a - b).Length() < 5e-4f);

    static int FillTyres(CarFleet fleet, float leastWidthM, Span<SpriteInstance> into) =>
        CarSprites.FillTyres(
            fleet, Every(fleet), rubberSheet: 9, leastWidthM, new Vector2(fleet.Count * 5f, 0f), new Vector2(1_000f, 1_000f), into);

    static SpriteInstance[] Tyres(CarFleet fleet)
    {
        var into = new SpriteInstance[fleet.Count * fleet.BuildOf(0).WheelCount];
        Assert.Equal(into.Length, FillTyres(fleet, leastWidthM: 0f, into));
        return into;
    }

    /// <summary>
    /// <b>A tyre is drawn at the very offset its impulse acts on</b>, at its build's size, whatever the body above it is
    /// doing on its springs (CAR-56). Two constructions that agree are the more misleading of the two: they agree until
    /// one of them is changed, so the drawing asks the model where the wheel is rather than knowing.
    /// </summary>
    [Fact]
    public void EveryTyreIsDrawnWhereItsImpulseActsHoweverTheBodyTilts()
    {
        var fleet = FleetOf(1);
        fleet.TiltRad[0] = new Vector2(0.05f, -0.07f);
        ref readonly var build = ref fleet.BuildOf(0);
        var drawn = Tyres(fleet);

        for (var wheel = 0; wheel < build.WheelCount; wheel++)
        {
            Assert.Equal(fleet.PositionM[0] + TyreModel.WheelAtM(build, wheel), drawn[wheel].CentreM);
            Assert.Equal(SpriteInstance.AsDrawn(new Vector2(build.WheelLengthM, build.WheelWidthM) * 0.5f), drawn[wheel].HalfSizeM);
        }
    }

    /// <summary>The front tyres are drawn at the rack's angle off the car's heading, and the rear pair along it.</summary>
    [Fact]
    public void TheFrontTyresAreDrawnAtTheRacksAngleAndTheRearAlongTheBody()
    {
        var fleet = FleetOf(1);
        fleet.HeadingRad[0] = 1f;
        fleet.Command[0] = new DriveCommand(0.4f, 0f, 0f, false, false);
        var drawn = Tyres(fleet);

        Assert.Equal(1.4f, drawn[0].HeadingRad, 1e-5f);
        Assert.Equal(1.4f, drawn[1].HeadingRad, 1e-5f);
        Assert.Equal(1f, drawn[2].HeadingRad);
        Assert.Equal(1f, drawn[3].HeadingRad);
    }

    /// <summary>A car whose tyres are narrower than the frame's least draws none, and one exactly at it draws all four.</summary>
    [Fact]
    public void ATyreNarrowerThanTheFramesLeastIsLeftOut()
    {
        var fleet = FleetOf(1);
        var widthM = fleet.BuildOf(0).WheelWidthM;
        var into = new SpriteInstance[fleet.BuildOf(0).WheelCount];

        Assert.Equal(0, FillTyres(fleet, MathF.BitIncrement(widthM), into));
        Assert.Equal(into.Length, FillTyres(fleet, widthM, into));
    }

    /// <summary>
    /// <b>A tilted body is drawn where the car stands, its tilt carried as its ID</b> (CAR-56, <see cref="BodyTilt"/>): the
    /// vertex stage tilts the plane about the middle, so the middle does not move, and the tilt it reads back is the one the
    /// springs gave to within half a step.
    /// </summary>
    [Fact]
    public void ATiltedBodyStandsWhereTheCarStandsAndCarriesItsTilt()
    {
        var catalogue = CarCatalog.Load();
        var fleet = FleetOf(1);
        var tiltRad = new Vector2(0.031f, -0.052f);
        fleet.TiltRad[0] = tiltRad;
        var drawn = Drawn(fleet, catalogue, 0)[0];

        var steps = new Vector2((sbyte)(drawn.Id & 0xFF), (sbyte)(drawn.Id >> 8));
        Assert.Equal(fleet.PositionM[0], drawn.CentreM);
        Assert.Equal(tiltRad.X, steps.X / BodyTilt.StepsPerRad, 0.5f / BodyTilt.StepsPerRad);
        Assert.Equal(tiltRad.Y, steps.Y / BodyTilt.StepsPerRad, 0.5f / BodyTilt.StepsPerRad);
    }

    /// <summary>
    /// <b>A point of the body its spring let down is drawn in towards the middle, and one let up stands out</b>, along the
    /// line from the middle (<see cref="BodyTilt.OnBodyM"/>) — and the middle, which the plane turns about, stays. Nose
    /// down and right side down: the front right corner sinks and the rear left rises.
    /// </summary>
    [Fact]
    public void ACornerLetDownIsDrawnInAndOneLetUpStandsOut()
    {
        var tiltRad = new Vector2(0.04f, 0.05f);
        var frontRightM = new Vector2(2f, 0.9f);
        var rearLeftM = -frontRightM;

        var sunkM = BodyTilt.OnBodyM(frontRightM, tiltRad, eyeHeightM: 1f);
        var raisedM = BodyTilt.OnBodyM(rearLeftM, tiltRad, eyeHeightM: 1f);

        Assert.InRange(sunkM.Length(), 0f, frontRightM.Length() * 0.95f);
        Assert.InRange(raisedM.Length(), rearLeftM.Length() * 1.05f, float.MaxValue);
        Assert.Equal(Vector2.Normalize(frontRightM), Vector2.Normalize(sunkM), Near);
        Assert.Equal(Vector2.Normalize(rearLeftM), Vector2.Normalize(raisedM), Near);
        Assert.Equal(Vector2.Zero, BodyTilt.OnBodyM(Vector2.Zero, tiltRad, eyeHeightM: 1f));
    }
}
