using TrafficSimulation.Bench;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Tests.Bench;
using Xunit;
using Xunit.Abstractions;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// <b>What each shape of road costs a car</b>, taken off the proving ground (<see cref="TrackPlan"/>) by
/// the instrument that measures it and asserted as the claims the shapes were laid to show: a corner is
/// taken at the speed its radius affords, a tighter corner slower, a straight is worth accelerating down
/// and braking for, and the car is on the road through all of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The claims are <see cref="TrackWatch"/>'s and this only asserts them.</b> The same watch answers
/// <c>--bench track</c> and the panel on a run of <c>--map Track</c>, so a lap cannot pass here and read as
/// broken there. What each claim is and what it is bounded by is the watch's own business.
/// </para>
/// <para>
/// <b>Relations and not figures.</b> Every number under those claims comes out of the tyres, the profile
/// and the solver together, and pinning one would be pinning the sum of three models — the same test would
/// then fail for a change to any of them without saying which. The figures themselves are what
/// <c>--bench track</c> prints, and what this run saw is written into the test's own output below.
/// </para>
/// <para>
/// <b>Nothing here stages anything at all.</b> The cars drive laps of the one lap under the standing rules,
/// and what stops them is somebody stepping into the road at the end of each shape — so every figure is
/// measured from whatever speed the road before it gave the car, over and over, for as long as the probe
/// watches.
/// </para>
/// </remarks>
[Trait(Tier.Key, Tier.Town)]
public class TrackFiguresTests(ITestOutputHelper output)
{
    static readonly SimConfig Config = SimConfig.Shipped();

    /// <summary>
    /// One run of the lap, shared by every case here. <b>Two cases about the same laps and not two runs of
    /// them</b>: they ask different things of one set of figures, and running the town twice over would be
    /// two minutes spent proving the probe is deterministic.
    /// </summary>
    static readonly LapWatch Ran = TrackProbe.Measure(Config);

    /// <summary>
    /// <b>Every claim the lap makes about itself is kept.</b> A claim still waiting fails as loudly as a
    /// broken one: the probe chooses how long it watches, so a claim it never got round to answering is a
    /// run too short to be quoting anything.
    /// </summary>
    [Fact]
    public void EveryClaimTheProvingGroundMakesIsKept()
    {
        Claims.AssertKept(Ran, output);
    }

    /// <summary>
    /// <b>Every drivetrain drove every shape.</b> The lap carries the same number of each and they differ in
    /// nothing else, so a row with no passes behind it is a comparison with a hole in it rather than a
    /// finding about drive layout — which is a question about the table's rows and not about the lap, and
    /// therefore is asked here rather than claimed on the panel.
    /// </summary>
    [Fact]
    public void EveryDrivetrainAnsweredForEveryShape()
    {
        var metrics = Ran.Metrics;
        for (var shape = 0; shape < metrics.Shapes; shape++)
        {
            var figures = metrics.Figures(shape);
            output.WriteLine(
                $"{metrics.SectionOf(shape).Name}: {figures.Passes} passes, top {figures.TopMps:F2}, "
                + $"hold {figures.HoldMps:F2}, {figures.Stops} stops, slowed at {figures.SlowedAtMps2:F2} m/s², "
                + $"back up in {figures.AccelS:F1} s, off-line {figures.OffLineM:F3} m");

            for (var drivetrain = 0; drivetrain < TrackMetrics.Drivetrains; drivetrain++)
            {
                Assert.True(
                    metrics.Figures(shape, drivetrain).Any,
                    $"{TrackMetrics.DrivetrainName(drivetrain)}-drive never got a clean pass over "
                    + $"{metrics.SectionOf(shape).Name}");
            }
        }
    }
}
