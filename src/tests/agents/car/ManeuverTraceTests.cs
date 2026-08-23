using TrafficSimulation.Agents.Car.Maneuvers;
using Xunit;

namespace TrafficSimulation.Tests.Agents.Car;

/// <summary>
/// The trace as an instrument: it counts what happened, and the one thing it is really for is the pair
/// that hands a car back and forth without it going anywhere.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
public class ManeuverTraceTests
{
    [Fact]
    public void ItCountsTicksEntriesAndHandOvers()
    {
        var trace = new ManeuverTrace();
        trace.Ticked(Maneuver.RunTheLine, clocked: true);
        trace.Ticked(Maneuver.RunTheLine, clocked: true);
        trace.Changed(Maneuver.RunTheLine, Maneuver.HoldAtALine, inOneSpot: false);
        trace.Ticked(Maneuver.HoldAtALine, clocked: true);

        Assert.Equal(3, trace.CarTicks);
        Assert.Equal(2, trace.TicksIn(Maneuver.RunTheLine));
        Assert.Equal(1, trace.Entries(Maneuver.HoldAtALine));
        Assert.Equal(1, trace.Transitions(Maneuver.RunTheLine, Maneuver.HoldAtALine));
        Assert.Equal(0, trace.Transitions(Maneuver.HoldAtALine, Maneuver.RunTheLine));
    }

    /// <summary>
    /// <b>The back-and-forth is counted in both directions and against the pair</b>, because a manoeuvre
    /// that names a successor which names it straight back is one loop and not two halves.
    /// </summary>
    [Fact]
    public void ItCountsTheBackAndForthAgainstThePairEitherWayRound()
    {
        var trace = new ManeuverTrace();
        trace.Changed(Maneuver.RunTheLine, Maneuver.HoldAtALine, inOneSpot: true);
        trace.Changed(Maneuver.HoldAtALine, Maneuver.RunTheLine, inOneSpot: true);

        Assert.Equal(2, trace.ShuttlesBetween(Maneuver.RunTheLine, Maneuver.HoldAtALine));
        Assert.Equal(2, trace.ShuttlesBetween(Maneuver.HoldAtALine, Maneuver.RunTheLine));

        var worst = trace.WorstShuttle();
        Assert.Equal(2, worst.Count);
        Assert.Contains(Maneuver.RunTheLine, new[] { worst.A, worst.B });
        Assert.Contains(Maneuver.HoldAtALine, new[] { worst.A, worst.B });
    }

    /// <summary>A hand-over the car drove away from is traffic and is not counted as a loop.</summary>
    [Fact]
    public void AHandOverWithGroundCoveredIsNotABackAndForth()
    {
        var trace = new ManeuverTrace();
        trace.Changed(Maneuver.RunTheLine, Maneuver.HoldAtALine, inOneSpot: false);
        trace.Changed(Maneuver.HoldAtALine, Maneuver.RunTheLine, inOneSpot: false);

        Assert.Equal(0, trace.ShuttlesBetween(Maneuver.RunTheLine, Maneuver.HoldAtALine));
        Assert.Equal(0, trace.WorstShuttle().Count);
    }

    /// <summary>A car standing still that no clock is running for is a fault, and this is where it is visible.</summary>
    [Fact]
    public void ItCountsTheCarTicksNothingWasRunningFor()
    {
        var trace = new ManeuverTrace();
        trace.Ticked(Maneuver.HoldAtALine, clocked: true);
        trace.Ticked(Maneuver.HoldAtALine, clocked: false);

        Assert.Equal(1, trace.StoodUnclocked);
    }

    /// <summary>
    /// An entry nothing ever reaches is either unbuilt or unenterable, and the trace has to be able to
    /// say which entries those were — a run of it is what the probe prints as <c>never entered</c>.
    /// </summary>
    [Fact]
    public void ItReportsWhichEntriesWereNeverReached()
    {
        var trace = new ManeuverTrace();
        trace.Changed(Maneuver.None, Maneuver.RunTheLine, inOneSpot: false);

        Assert.True(trace.EverEntered(Maneuver.RunTheLine));
        Assert.False(trace.EverEntered(Maneuver.TurnAround));

        trace.Reset();
        Assert.False(trace.EverEntered(Maneuver.RunTheLine));
        Assert.Equal(0, trace.CarTicks);
    }
}
