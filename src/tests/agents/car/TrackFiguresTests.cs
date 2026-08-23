using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using Xunit;
using Xunit.Abstractions;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// <b>What each shape of road costs a car</b>, taken off the proving ground (<see cref="TrackPlan"/>) by
/// the instrument that measures it and asserted as the relations the shapes were laid to show: a corner is
/// taken at the speed its radius affords, a tighter corner slower, a straight is worth accelerating down
/// and braking for, and the car is on the road through all of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Relations and not figures.</b> Every number here comes out of the tyres, the profile and the solver
/// together, and pinning one would be pinning the sum of three models — the same test would then fail for
/// a change to any of them without saying which. What each assertion states is a fact that has to hold
/// whatever those three do, and the figures themselves are what <c>--bench track</c> prints.
/// </para>
/// <para>
/// <b>Nothing here stages anything at all.</b> The cars drive laps of the one lap under the standing rules,
/// and what stops them is somebody stepping into the road at the end of each shape — so every figure is
/// measured from whatever speed the road before it gave the car, over and over, for as long as the probe
/// watches.
/// </para>
/// </remarks>
[Collection(TrafficSimulation.Tests.Simulation.SolverCollection.Name)]
[Trait(Tier.Key, Tier.Town)]
public class TrackFiguresTests(ITestOutputHelper output)
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// How near the corner formula a corner speed has to come. The shortfall is the reaction lead and the
    /// lookahead the corner is taken with, and it is largest where the corner is tightest.
    /// </summary>
    const float OfWhatTheRadiusAffords = 0.7f;

    /// <summary>
    /// And how far over it the fastest moment on a corner may be. Two things put it there: a corner is
    /// entered while the car is still on its way down to the speed it will be held at, and the lane the car
    /// drives is offset from the centreline the radius is stated at — so the outside of a 30 m corner is a
    /// 31.5 m one.
    /// </summary>
    const float OverWhatTheRadiusAffords = 1.1f;

    /// <summary>How near the planned rate a slowing has to come. What is left over is the rolling resistance, which the tyres spend outside their own budget.</summary>
    const float OfWhatItPlanned = 0.25f;

    /// <summary>What a straight is worth: the car reaches this much of the fastest corner's speed down one.</summary>
    const float OverTheFastestCorner = 2f;

    /// <summary>How few passes a section may be quoted on. Under this the mean is one car's day rather than the road's.</summary>
    const int WorthQuoting = 4;

    /// <summary>
    /// One run of the lap, shared by every case here. <b>Four cases about the same laps and not four runs
    /// of them</b>: they assert different relations over one set of figures, and running the town four times
    /// over would be four minutes spent proving the probe is deterministic.
    /// </summary>
    static readonly TrackMetrics Ran = TrackProbe.Measure(Config);

    [Fact]
    public void EveryShapeIsDrivenAtWhatItAffordsAndStaysOnTheRoad()
    {
        var lateralMps2 = Config.Tyre.GripMps2 * Config.Driving.GripMargin;
        var plannedMps2 = CarFollower.BrakingMps2(Config, groundCoefficient: 1f);
        var metrics = Ran;

        for (var shape = 0; shape < metrics.Shapes; shape++)
        {
            var section = metrics.SectionOf(shape);
            var figures = metrics.Figures(shape);
            output.WriteLine(
                $"{section.Name}: {figures.Passes} passes, top {figures.TopMps:F2}, hold {figures.HoldMps:F2}, "
                + $"{figures.Stops} stops, slowed {figures.SlowFromMps:F1}→{figures.SlowToMps:F1} in "
                + $"{figures.SlowM:F1} m at {figures.SlowedAtMps2:F2} m/s², back up in {figures.AccelS:F1} s "
                + $"and {figures.AccelM:F0} m, off-line {figures.OffLineM:F3} m");

            // <b>It drove the shape</b>, often enough for the mean under it to be the road's answer.
            Assert.True(
                figures.Passes >= WorthQuoting,
                $"{section.Name}: only {figures.Passes} passes over it were anybody's own");

            // <b>And it stayed on the road while it did.</b> A speed taken by a body that has left its lane
            // is a speed on ground the road never offered, and the bar is the town's own: past twice a
            // lane's half-width the town stops calling a car crabbing across its line a car on it.
            Assert.True(
                figures.OffLineM < Config.CarOffPathM * 2f,
                $"{section.Name}: the car ran {figures.OffLineM:F2} m off its line, past the "
                + $"{Config.CarOffPathM * 2f:F2} m this town calls losing it");

            // <b>A corner is taken at the speed its radius affords</b>, which is the tyre's own arithmetic
            // and not the road's: sqrt(lateral grip × radius), arrived at from under.
            if (section.RadiusM > 0f)
            {
                var affordsMps = MathF.Sqrt(lateralMps2 * section.RadiusM);
                Assert.InRange(
                    figures.TopMps, affordsMps * OfWhatTheRadiusAffords, affordsMps * OverWhatTheRadiusAffords);
            }

            // <b>The slowing into it is the planned braking and nothing else.</b> Every reservation on the
            // road is sized by that figure, so a car that slowed far harder held less street than it used
            // and one that slowed far softer held a street shut for ground it never needed.
            Assert.True(figures.Slowings >= WorthQuoting, $"{section.Name}: nothing braked for it");
            Assert.InRange(
                figures.SlowedAtMps2, plannedMps2 * (1f - OfWhatItPlanned), plannedMps2 * (1f + OfWhatItPlanned));

            // <b>And it got back up to speed afterwards.</b> A shape nobody accelerated out of is a shape
            // the car was carried through rather than driven through.
            Assert.True(figures.Pulls >= WorthQuoting, $"{section.Name}: nothing pulled away out of it");
            Assert.True(
                figures.AccelM > 0f && figures.AccelS > 0f,
                $"{section.Name}: the run back up to speed came to {figures.AccelM:F0} m in {figures.AccelS:F1} s");
        }

        // <b>Nobody was run over while any of it was measured.</b> A body pacing the road is the whole of
        // what brings a car to rest here, and one that was knocked down is a car that could not stop for
        // what was in front of it — and, lying in a lane, the end of every figure taken past it.
        Assert.Equal(0L, metrics.Knocks);
    }

    /// <summary>
    /// <b>The tighter the corner the slower it is taken</b> — the whole of what the shapes are laid to show,
    /// stated over the sections rather than one section at a time.
    /// </summary>
    [Fact]
    public void ATighterCornerIsHeldSlower()
    {
        var metrics = Ran;
        var top = new Dictionary<string, float>();
        for (var shape = 0; shape < metrics.Shapes; shape++)
        {
            top[metrics.SectionOf(shape).Name] = metrics.Figures(shape).TopMps;
        }

        Assert.True(top["180 turn"] < top["90 turn"], "a 15 m corner cannot be taken at what a 30 m one allows");
        Assert.True(top["90 turn"] < top["snake"], "a 30 m corner cannot be taken at what a 40 m one allows");
        Assert.True(top["snake"] < top["arc"], "a 40 m corner cannot be taken at what a 120 m one allows");
        Assert.True(top["arc"] < top["straight"], "no corner on this lap is worth what the straight is");
    }

    /// <summary>
    /// <b>A straight is worth accelerating down and braking for.</b> It is the difference between a car
    /// driving a road and a car merely being carried round one, and it is why the lap carries one at all.
    /// </summary>
    [Fact]
    public void TheStraightIsAcceleratedDownAndBrakedFor()
    {
        var metrics = Ran;
        var straight = metrics.Figures(ShapeCalled(metrics, "straight"));
        var arc = metrics.Figures(ShapeCalled(metrics, "arc"));

        Assert.True(
            straight.TopMps > arc.TopMps * OverTheFastestCorner,
            $"the car only reached {straight.TopMps:F1} m/s down the straight, off a {arc.TopMps:F1} m/s corner");

        // <b>And it reached the gear's own cap on it</b>, which is what the straight is sized for: a car
        // that runs out of road first is measuring the road rather than itself.
        Assert.InRange(straight.TopMps, Config.Car.MaxSpeedMps * 0.9f, Config.Car.MaxSpeedMps);

        // <b>And it was stopped at the end of it</b>, which is where the stop from a top speed comes from:
        // the whole of the reason somebody paces the road there.
        Assert.True(straight.Stops > 0, "nobody was ever brought to rest at the end of the straight");
    }

    /// <summary>
    /// <b>Every drivetrain drove every shape.</b> The lap carries the same number of each and they differ in
    /// nothing else, so a row with no passes behind it is a comparison with a hole in it rather than a
    /// finding about drive layout.
    /// </summary>
    [Fact]
    public void EveryDrivetrainAnsweredForEveryShape()
    {
        var metrics = Ran;
        for (var shape = 0; shape < metrics.Shapes; shape++)
        {
            for (var drivetrain = 0; drivetrain < TrackMetrics.Drivetrains; drivetrain++)
            {
                Assert.True(
                    metrics.Figures(shape, drivetrain).Any,
                    $"{TrackMetrics.DrivetrainName(drivetrain)}-drive never got a clean pass over "
                    + $"{metrics.SectionOf(shape).Name}");
            }
        }
    }

    static int ShapeCalled(TrackMetrics metrics, string name)
    {
        for (var shape = 0; shape < metrics.Shapes; shape++)
        {
            if (string.Equals(metrics.SectionOf(shape).Name, name, StringComparison.Ordinal)) return shape;
        }

        throw new ArgumentException($"The lap carries no shape called {name}.");
    }
}
