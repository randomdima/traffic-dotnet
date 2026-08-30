using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// CAR-11: <b>a car is driven by its own body</b>. What is asked here is that the build a variant resolves
/// to is that variant's — its dimensions, its axles, its weight and what its file says its tyres and
/// gearing are worth — and that the figures every decision is taken against follow from it.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class CarBuildTests
{
    static readonly SimConfig Figures = SimConfig.Shipped();

    static readonly CarCatalog Catalogue = CarCatalog.Load();

    static CarBuild Of(string id)
    {
        for (var variant = 0; variant < Catalogue.SheetCount; variant++)
        {
            if (Catalogue.Variants[variant].Id == id) return CarBuild.Of(Figures, Catalogue.Variants[variant]);
        }

        throw new InvalidDataException($"no variant is called '{id}'");
    }

    /// <summary>
    /// The dimensions are the file's and not the nominal car's, and <b>the axle is where the picture puts
    /// it</b>: the middle of the body stands ahead of the rear axle by the variant's own figure, which is
    /// what every line drawn for that axle is read back through (CAR-4a).
    /// </summary>
    [Fact]
    public void AVariantIsBuiltFromItsOwnBody()
    {
        var truck = Of("truck_brown");
        var compact = Of("compact_green");

        Assert.Equal(3.56f, truck.LengthM, 1e-3f);
        Assert.Equal(2000f, truck.MassKg);
        Assert.Equal(1050f, compact.MassKg);
        Assert.True(truck.MassKg > compact.MassKg, "a truck outweighs a compact");

        // compact_green's axles are at ±0.98/−1.02 m of its own middle, so the middle stands 1.02 m ahead
        // of the axle and the nose 1.7 m ahead of the middle.
        Assert.Equal(1.02f, compact.CentreAheadOfAxleM, 1e-3f);
        Assert.Equal(compact.CentreAheadOfAxleM + compact.HalfLengthM, compact.NoseAheadOfAxleM, 1e-3f);
        Assert.Equal(compact.HalfLengthM - compact.CentreAheadOfAxleM, compact.TailBehindAxleM, 1e-3f);
        Assert.Equal(2f, compact.WheelbaseM, 1e-3f);
    }

    /// <summary>
    /// <b>The handling multipliers are read.</b> A file that states what it is worth against the nominal
    /// car gets a top speed, a pedal, a brake and a grip of its own — which is the whole difference between
    /// a supercar and a tow truck on the same road.
    /// </summary>
    [Fact]
    public void WhatAVariantIsWorthReachesTheFiguresItIsDrivenBy()
    {
        var supercar = Of("super_cyan");
        var evacuator = Of("evacuator_yellow");

        Assert.True(supercar.MaxSpeedMps > evacuator.MaxSpeedMps * 2f);
        Assert.True(supercar.GripMps2 > evacuator.GripMps2);

        // The pedals are a multiple of what each one's own driven axle holds (CAR-45), so the spread
        // between them is the rubber's and the layout's rather than a figure either file states: both
        // drive all four wheels, and the supercar's lead is its compound and the engine it hangs on it.
        Assert.True(supercar.AccelerationMps2 > evacuator.AccelerationMps2 * 2.5f);

        // And the derived figures follow: what a car can see is its own stopping distance from its own top
        // speed, so the fast one plans further ahead than the slow one.
        Assert.True(supercar.SightM > evacuator.SightM);
        Assert.True(supercar.LookaheadFloorM < evacuator.LookaheadFloorM, "and the long body aims further ahead");
    }

    /// <summary>
    /// <b>A wider circle needs more street.</b> The turning radius is the car's own wheelbase against the
    /// steering lock, so the shape a long car draws to get into a bay is drawn wider than a short one's —
    /// which is why the template is asked for per car rather than taken from the town.
    /// </summary>
    [Fact]
    public void ALongerWheelbaseTurnsInAWiderCircle()
    {
        var van = Of("van_purple");
        var sports = Of("sports_red");

        Assert.True(van.WheelbaseM > sports.WheelbaseM);
        Assert.True(van.TurningRadiusM > sports.TurningRadiusM);
        Assert.True(van.ParkingTemplateRadiusM > van.TurningRadiusM, "and the template keeps the steering off its stop");
    }

    /// <summary>
    /// <b>No lock a file can state turns a circle inside out.</b> A circle is <c>wheelbase/tan(lock)</c>,
    /// which is not linear in the lock: at a right angle it is nothing, and past one the tangent changes
    /// sign and hands the whole parking geometry a <em>negative</em> radius.
    /// </summary>
    /// <remarks>
    /// Held well past anything the fleet authors — every shipped lock is under 32° — because what a bound
    /// is for is the figure nobody has written yet.
    /// </remarks>
    [Fact]
    public void NoLockAFileCanStateTurnsACircleInsideOut()
    {
        foreach (var lockDeg in new[] { 3.5f, 17.7f, 35.42f, 70.8f, 89.9f, 90f, 106f, 354.2f })
        {
            var figures = new SimConfig { Car = new CarFigures { MaxSteeringDeg = lockDeg } };
            var car = CarBuild.Nominal(figures, figures.Car.DrivenFrontShare);

            Assert.True(
                car.TurningRadiusM > 0f && float.IsFinite(car.TurningRadiusM),
                $"a lock of {lockDeg:0.##} deg turns a circle of {car.TurningRadiusM:0.00} m");
            Assert.True(car.ParkingTemplateRadiusM > car.TurningRadiusM);
        }
    }

    /// <summary>
    /// <b>The same line asks a long car for more lock than a short one</b> (CAR-10a). Pure pursuit turns the
    /// wheel for the circle through the axle and the lead point, and what holds that circle is the
    /// wheelbase — so the two cars drive the one recommendation along slightly different ground.
    /// </summary>
    [Fact]
    public void TheSameLineIsDrivenDifferentlyByDifferentCars()
    {
        ReadOnlySpan<ArcSeg> line = [new ArcSeg(Vector2.Zero, 0f, 60f, 0.05f)];
        var van = Of("van_purple");
        var compact = Of("compact_green");

        var vanRad = CarFollower.Steer(van, line, 0f, Vector2.Zero, Vector2.UnitX, 6f);
        var compactRad = CarFollower.Steer(compact, line, 0f, Vector2.Zero, Vector2.UnitX, 6f);

        Assert.True(vanRad > compactRad, $"the van asked for {vanRad:F3} rad and the compact for {compactRad:F3}");
    }

    /// <summary>
    /// <b>The nominal car is the lab's</b> (CAR-11a): every build in it is the town's own figures with one
    /// variant's drive layout on top, so a lap of the proving ground compares layouts and nothing else.
    /// </summary>
    [Fact]
    public void TheNominalBuildTakesNothingFromAVariantButItsDriveLayout()
    {
        var builds = CarBuilds.OfTheNominalCar(Figures, Catalogue);
        var layouts = 0;

        for (var variant = 0; variant < Catalogue.SheetCount; variant++)
        {
            ref readonly var build = ref builds.Of(variant);
            Assert.Equal(Figures.Car.LengthM, build.LengthM);
            Assert.Equal(Figures.Car.WidthM, build.WidthM);
            Assert.Equal(Figures.Car.MassKg, build.MassKg);
            Assert.Equal(Figures.Car.WheelbaseM, build.WheelbaseM);
            Assert.Equal(Figures.Car.MaxSpeedMps, build.MaxSpeedMps);
            Assert.Equal(
                Catalogue.Variants[variant].DrivenFrontShare(Figures.Car.StaticFrontShare),
                build.DrivenFrontShare);
            if (build.DrivenFrontShare != Figures.Car.DrivenFrontShare) layouts++;
        }

        Assert.True(layouts > 0, "the fleet varies its drive layouts and the lab keeps that much");
    }

    static CarBuild Of(string id, SimConfig figures)
    {
        for (var variant = 0; variant < Catalogue.SheetCount; variant++)
        {
            if (Catalogue.Variants[variant].Id == id) return CarBuild.Of(figures, Catalogue.Variants[variant]);
        }

        throw new InvalidDataException($"no variant is called '{id}'");
    }

    /// <summary>
    /// <b>The road's trim scales what the variant resolved to, and a car's own figures have no trim at
    /// all</b> (<see cref="TrimFigures"/>). A truck states its own compound, so scaling the nominal
    /// coefficient would leave it exactly where it was — and it states its own lock, height and mass,
    /// which are its and which nothing on a panel may speak for.
    /// </summary>
    [Fact]
    public void TheRoadsTrimScalesThisCarAndItsOwnFiguresAreUntouched()
    {
        var shipped = Of("truck_brown");
        var figures = SimConfig.Shipped();
        figures.Trim.Friction = 2f;

        var trimmed = Of("truck_brown", figures);
        Assert.Equal(shipped.GripMps2 * 2f, trimmed.GripMps2, 3);

        // This truck's own body is what its file says it is, whatever the panel is doing to the road.
        Assert.Equal(shipped.CgHeightM, trimmed.CgHeightM, 3);
        Assert.Equal(shipped.MassKg, trimmed.MassKg, 3);
        Assert.Equal(shipped.TurningRadiusM, trimmed.TurningRadiusM, 3);
        Assert.Equal(shipped.MaxSpeedMps, trimmed.MaxSpeedMps, 3);
    }

    /// <summary>
    /// <b>Figures flow one way</b>: what is authored is the lock at the road wheel, and the circle a maker
    /// quotes is worked out from it. The two must agree, or the figure a reader checks against a spec sheet
    /// is not the figure the car is driven by.
    /// </summary>
    [Fact]
    public void TheQuotedCircleIsTheAuthoredLockReadBack()
    {
        for (var variant = 0; variant < Catalogue.SheetCount; variant++)
        {
            var build = CarBuild.Of(Figures, Catalogue.Variants[variant]);
            var lockRad = Catalogue.Variants[variant].MaxSteeringDeg ?? Figures.Car.MaxSteeringDeg;

            Assert.Equal(lockRad * MathF.PI / 180f, build.MaxSteerRad, 4);
            Assert.Equal(build.WheelbaseM / MathF.Tan(build.MaxSteerRad), build.TurningRadiusM, 3);

            // 9 to 14 m kerb to kerb is where a road vehicle of these sizes actually sits.
            Assert.InRange(build.TurningCircleM, 9f, 14f);
        }
    }

    /// <summary>
    /// And every trim is one on a run nobody has touched, so a shipped build is the build this suite has
    /// always measured — <b>a multiply by one is exact</b>, so the figures compare bit for bit.
    /// </summary>
    [Fact]
    public void AnUntouchedRunResolvesExactlyTheShippedCar()
    {
        Assert.True(SimConfig.Shipped().Trim.Untouched);

        for (var variant = 0; variant < Catalogue.SheetCount; variant++)
        {
            var build = CarBuild.Of(Figures, Catalogue.Variants[variant]);
            var again = CarBuild.Of(SimConfig.Shipped(), Catalogue.Variants[variant]);
            Assert.Equal(build, again);
        }
    }
}
