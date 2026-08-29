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
        Assert.True(supercar.AccelerationMps2 > evacuator.AccelerationMps2 * 3f);
        Assert.True(supercar.GripMps2 > evacuator.GripMps2);

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
            Assert.Equal(Catalogue.Variants[variant].DrivenFrontShare, build.DrivenFrontShare);
            if (build.DrivenFrontShare != Figures.Car.DrivenFrontShare) layouts++;
        }

        Assert.True(layouts > 0, "the fleet varies its drive layouts and the lab keeps that much");
    }
}
