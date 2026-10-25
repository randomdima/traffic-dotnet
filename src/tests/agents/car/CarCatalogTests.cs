using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// The two lists in one array (SRV-3): the fleet a town's traffic wraps over, and the service variants
/// past the end of it that can only be reached by name.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P8)]
public class CarCatalogTests
{
    static readonly CarCatalog Catalogue = CarCatalog.Load();

    /// <summary>
    /// <b>The wrap cannot reach a service variant.</b> Handing a picture out to the seventeenth ordinary
    /// car is what would put an ambulance on the school run, so the boundary is asserted from both sides.
    /// </summary>
    [Fact]
    public void TheWrapStaysInsideTheFleet()
    {
        Assert.True(Catalogue.Count > 0);
        Assert.True(Catalogue.SheetCount > Catalogue.Count, "there are no service variants past the fleet");

        for (var entry = 0; entry < Catalogue.Count; entry++) Assert.False(Catalogue.IsService(entry));
        for (var entry = Catalogue.Count; entry < Catalogue.SheetCount; entry++) Assert.True(Catalogue.IsService(entry));
    }

    /// <summary>
    /// Each service vehicle is named by id rather than by position, so <c>Service.json</c> can be reordered
    /// without a car changing what it is.
    /// </summary>
    [Fact]
    public void EveryServiceVehicleResolvesToItsOwnVariant()
    {
        Assert.Equal("ambulance_white", Catalogue.Variants[Catalogue.Ambulance].Id);
        Assert.Equal("police_white", Catalogue.Variants[Catalogue.Police].Id);
        Assert.Equal("evacuator_yellow", Catalogue.Variants[Catalogue.Evacuator].Id);

        Assert.True(Catalogue.IsService(Catalogue.Ambulance));
        Assert.True(Catalogue.IsService(Catalogue.Police));
        Assert.True(Catalogue.IsService(Catalogue.Evacuator));
    }

    /// <summary>
    /// <b>CAR-45 — every variant's pedal is a multiple of what its own driven tyres put down.</b> The
    /// multiple is the whole of what a variant authors; what is checked here is that the figure the build
    /// hands out is that multiple of <em>this</em> arithmetic, worked out where the rule states it rather
    /// than where the code does — drive placed by layout, over the load of the axle it is placed on
    /// (<see cref="TyreModel"/>), at the static load a car pulling away stands on.
    /// </summary>
    [Fact]
    public void EveryVariantsPedalIsAMultipleOfWhatItsDrivenTyresHold()
    {
        for (var entry = 0; entry < Catalogue.SheetCount; entry++)
        {
            var variant = Catalogue.Variants[entry];
            var build = CarBuild.Of(Figures, variant);
            Assert.Equal(TractionLimitMps2(build), build.DrivenTractionMps2, 1e-3f);
            Assert.Equal(
                TractionLimitMps2(build) * Figures.Car.DrivePedalInDrivenGrips * variant.Handling.Acceleration,
                build.AccelerationMps2,
                1e-3f);
        }
    }

    /// <summary>
    /// <b>CAR-45 — and no variant's engine reaches so far past its rubber that the figure stops describing
    /// the car.</b> A pedal the tyres answer at every opening is a car with no wheelspin in it at all; a
    /// pedal several times past them is an engine figure nothing that happens to the car is downstream of,
    /// since the whole of the excess buys no acceleration (CAR-3b) and every metre per second is the
    /// compound's. The band is wide because where a look sits in it is that look's character.
    /// </summary>
    [Fact]
    public void NoVariantsEngineOutrunsItsOwnTyresByMoreThanHalf()
    {
        for (var entry = 0; entry < Catalogue.SheetCount; entry++)
        {
            var build = CarBuild.Of(Figures, Catalogue.Variants[entry]);
            var reach = build.AccelerationMps2 / build.DrivenTractionMps2;
            Assert.True(reach is >= 0.5f and <= 1.5f, $"{Catalogue.Variants[entry].Id}'s engine reaches {reach:F2} of its tyres");
        }
    }

    /// <summary>
    /// The most a car can accelerate before a driven wheel breaks away: what the patch holds, against the
    /// share of the drive its axle carries divided by the share of the car's weight standing on it.
    /// </summary>
    /// <remarks>The shipped fleet stands on two axles: the front one carries the balance, the rear the rest.</remarks>
    static float TractionLimitMps2(in CarBuild build)
    {
        var limitMps2 = float.PositiveInfinity;
        if (build.DriveOf(0) > 0f) limitMps2 = build.GripMps2 * build.FrontWeightShare / build.DriveOf(0);
        if (build.DriveOf(1) > 0f)
        {
            limitMps2 = MathF.Min(limitMps2, build.GripMps2 * (1f - build.FrontWeightShare) / build.DriveOf(1));
        }

        return limitMps2;
    }

    static readonly SimConfig Figures = SimConfig.Shipped();

    /// <summary>
    /// <b>Every variant's wheel centres stand under its own bodywork.</b> The axles and the track are
    /// authored against the picture, and the one way to be sure they were read off the right one is that
    /// they fit inside it: a wheel <em>centre</em> outside the footprint is a car on outriggers, and a
    /// wheelbase as long as the body is a car with no overhang at either end. That the rubber round those
    /// centres shows past the panels is CAR-12, and is checked against the art itself.
    /// </summary>
    [Fact]
    public void EveryVariantKeepsItsWheelsInsideItsOwnFootprint()
    {
        for (var entry = 0; entry < Catalogue.SheetCount; entry++)
        {
            var variant = Catalogue.Variants[entry];
            var halfLengthM = variant.FootprintM.X * 0.5f;

            Assert.True(
                variant.HalfTrackM * 2f < variant.FootprintM.Y,
                $"{variant.Id} is {variant.FootprintM.Y:F2} m wide and its track is {variant.HalfTrackM * 2f:F2} m");
            Assert.True(
                variant.FrontAxleM < halfLengthM && variant.RearAxleM > -halfLengthM,
                $"{variant.Id} stands an axle outside its own {variant.FootprintM.X:F2} m body");
            Assert.True(
                variant.WheelbaseM > 0f && variant.WheelbaseM < variant.FootprintM.X,
                $"{variant.Id} has a {variant.WheelbaseM:F2} m wheelbase under a {variant.FootprintM.X:F2} m body");
        }
    }

    /// <summary>
    /// CAR-12b: <b>every variant authors the shape it is collided as, inside the footprint its picture is
    /// drawn in</b> — a rounded box no larger than that rectangle, rounded no more than its own half-sizes
    /// allow. The shape comes first and the art is drawn to it, so nothing here is asked of the picture.
    /// </summary>
    [Fact]
    public void EveryVariantAuthorsAShapeInsideItsFootprint()
    {
        foreach (var path in ShippedVariantFiles())
        {
            var file = AssetJson.Read(path, CarVariantJson.Default.CarVariantFile);
            Assert.NotNull(file.CollisionM);

            var sizeM = file.CollisionM.SizeM;
            var half = sizeM * 0.5f;

            Assert.InRange(file.CollisionM.CornerRadiusM, 0f, MathF.Min(half.X, half.Y));
            Assert.True(
                sizeM.X <= file.FootprintM.X && sizeM.Y <= file.FootprintM.Y,
                $"{file.Id} is collided at {sizeM} outside its {file.FootprintM} footprint");
        }
    }

    /// <summary>
    /// The two lists the catalogue itself reads, named here because what is being asked about is the file
    /// rather than the model: the footprint the shape must fit in is not on <see cref="CarVariant"/>.
    /// </summary>
    static IEnumerable<string> ShippedVariantFiles()
    {
        foreach (var list in (string[])["Fleet.json", "Service.json"])
        {
            var path = Path.Combine(ProjectPaths.Assets, "agents", "car", "variants", "common", list);
            foreach (var variant in AssetJson.Catalog(path)) yield return variant;
        }
    }

    /// <summary>
    /// SRV-4: <b>every shipped vehicle breaks, the evacuator included</b>. PHY-4b is a state the file format
    /// offers and nothing in this town wears: a vehicle that cannot be wrecked is the one in the town nothing
    /// could ever happen to.
    /// </summary>
    [Fact]
    public void NoShippedVariantIsUnbreakable()
    {
        for (var entry = 0; entry < Catalogue.SheetCount; entry++)
        {
            Assert.False(Catalogue.UnbreakableOf(entry), $"{Catalogue.Variants[entry].Id} cannot be wrecked");
        }
    }

    /// <summary>
    /// <b>No variant outgrows the car the town lays its bays to</b>
    /// (<see cref="CarFigures.LongestLengthM"/>, GEN-53). The plan is laid before a car exists and sizes a
    /// bay against that figure, so a variant drawn longer than it is a car that parks across the bay it was
    /// sent to — which is a thing to fail a build for and not to discover in a car park.
    /// </summary>
    [Fact]
    public void NoVariantIsLongerThanTheCarTheTownLaysItsBaysTo()
    {
        for (var entry = 0; entry < Catalogue.SheetCount; entry++)
        {
            var variant = Catalogue.Variants[entry];
            Assert.True(
                variant.FootprintM.X <= Figures.Car.LongestLengthM,
                $"{variant.Id} is {variant.FootprintM.X:F2} m long and a bay is laid for "
                + $"{Figures.Car.LongestLengthM:F2} m");
        }
    }

    /// <summary>
    /// EVA-5: <b>the evacuators are the only variants that tow on an arm</b>, and the arm is their picture's — the
    /// reach the coupling is held at is a distance somebody drew, so it is read off the same file as the
    /// sprite and not chosen in the config.
    /// </summary>
    [Fact]
    public void OnlyTheEvacuatorsTowOnAnArm()
    {
        for (var entry = 0; entry < Catalogue.SheetCount; entry++)
        {
            var evacuator = Catalogue.IsEvacuator(entry);
            Assert.Equal(evacuator, Catalogue.BeamOf(entry) is not null);
            Assert.Equal(evacuator, Catalogue.BeamSlotOf(entry, towing: false) != CarCatalog.NoBeam);
            if (!evacuator) continue;

            var beam = Catalogue.BeamOf(entry)!.Value;
            CarTowArm[] pictures = [beam.Collapsed, beam.Extended];
            foreach (var arm in pictures)
            {
                Assert.True(File.Exists(arm.SpritePath), $"the arm names art that is not there: {arm.SpritePath}");
            }

            // Two pictures of one machine, and two slots: an arm drawn in reaches less far than one that is out.
            Assert.NotEqual(Catalogue.BeamSlotOf(entry, towing: false), Catalogue.BeamSlotOf(entry, towing: true));
            Assert.True(beam.Collapsed.SizeM.X < beam.Extended.SizeM.X, "the arm drawn in is no shorter than the arm out");
            Assert.True(beam.ReachM > 0f, "an arm with no reach holds nothing at any distance");

            // The hinge is bolted behind the middle of the truck and the fork reaches past its tail, or the arm
            // is drawn somewhere inside the bodywork it is meant to stick out of.
            var half = Catalogue.Variants[entry].FootprintM.X * 0.5f;
            var id = Catalogue.Variants[entry].Id;
            Assert.True(beam.PivotM.X < 0f && beam.PivotM.X > -half, $"{id}'s hinge sits at {beam.PivotM.X:F2} m");
            Assert.True(-beam.PivotM.X + beam.ReachM > half, $"{id}'s fork does not reach past the truck's own tail");
        }
    }
}
