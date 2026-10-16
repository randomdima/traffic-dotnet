using System.Diagnostics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Simulation;

[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P0)]
public class SimClockTests
{
    static readonly SimConfig Config = SimConfig.Shipped();

    static (SimClock Clock, long FromStamp) Clock()
    {
        var clock = new SimClock(Config);
        var from = Stopwatch.GetTimestamp();
        clock.Resynchronise(from);
        return (clock, from);
    }

    static long After(long stamp, double seconds) => stamp + (long)(seconds * Stopwatch.Frequency);

    /// <summary>SIM-9: a display at half the tick rate runs a second of town in a second of frames.</summary>
    [Fact]
    public void ADisplayAtHalfTheTickRateKeepsRealTime()
    {
        var (clock, at) = Clock();
        var frameS = 2d / Config.Sim.TickRateHz;
        var frames = Config.Sim.TickRateHz / 2;

        var ran = 0;
        for (var frame = 0; frame < frames; frame++) ran += clock.TicksDue(at = After(at, frameS));

        Assert.InRange(ran, Config.Sim.TickRateHz - 1, Config.Sim.TickRateHz);
    }

    /// <summary>SIM-9: a tick slower than the time it stands for is not followed by another in the same frame.</summary>
    [Fact]
    public void ATickSlowerThanItsTimeIsNotCaughtUpOn()
    {
        var (clock, _) = Clock();

        Assert.True(clock.MayRunAnother(1, 0.5 * Config.TickSeconds));
        Assert.False(clock.MayRunAnother(1, 1.5 * Config.TickSeconds));
    }

    /// <summary>SIM-9: a town watched faster runs as many ticks to a frame as its speed asks, however slow they are.</summary>
    [Fact]
    public void AFrameWatchedFasterRunsItsSpeedsTicksAtAnyCost()
    {
        var (clock, _) = Clock();
        clock.TimeScale = 3f;

        Assert.True(clock.MayRunAnother(2, 10d));
        Assert.False(clock.MayRunAnother(3, 10d));
    }

    /// <summary>
    /// SIM-9: one tick the frame did not run is run by the next, so a display beating against the tick's period — none
    /// owed on a frame a hair early, two on the next — does not slow the town; the rest are dropped.
    /// </summary>
    [Fact]
    public void OneTickTheFrameDidNotRunIsKeptForTheNext()
    {
        var (clock, at) = Clock();
        var due = clock.TicksDue(at = After(at, 4.5 * Config.TickSeconds));

        clock.Unrun(due - 1);

        Assert.Equal(1, clock.TicksDue(at));
    }

    /// <summary>
    /// SIM-9: ticks a world could not take yet — a page's device answers between frames — are all run once it can, so a
    /// page at a third of the tick rate keeps real time.
    /// </summary>
    [Fact]
    public void TicksAWorldCouldNotTakeYetAreAllKept()
    {
        var (clock, at) = Clock();
        var due = clock.TicksDue(at = After(at, 3.5 * Config.TickSeconds));

        clock.Postpone(due - 1);

        Assert.Equal(due - 1, clock.TicksDue(at));
    }

    /// <summary>A hitch is not repaid as one frame many ticks long: a frame catches up over its longest span at most.</summary>
    [Fact]
    public void AFrameCatchesUpOverItsLongestSpanAtMost()
    {
        var (clock, at) = Clock();

        var due = clock.TicksDue(at = After(at, 10 * Config.Sim.LongestCaughtUpFrameS));

        Assert.InRange(due * Config.TickSeconds, 0d, Config.Sim.LongestCaughtUpFrameS + (0.5 * Config.TickSeconds));
        Assert.InRange(clock.TicksDue(at), 0, 1);
    }

    /// <summary>
    /// A time scale that stretches the physics delta integrates the whole simulation coarsely and
    /// manufactures collisions the model never had, so it is capped at the soak's own bound.
    /// </summary>
    [Fact]
    public void TheTimeScaleIsCappedAtTheSoaksOwnBound()
    {
        var (clock, _) = Clock();

        clock.TimeScale = 100f;
        Assert.Equal(Config.Sim.SoakMaxTimeScale, clock.TimeScale);

        clock.TimeScale = -1f;
        Assert.Equal(0f, clock.TimeScale);
    }

    [Fact]
    public void NoTimeIsOwedTheInstantTheClockIsMade()
    {
        var (clock, at) = Clock();

        Assert.Equal(0, clock.TicksDue(at));
    }

    [Fact]
    public void AStallIsForgottenRatherThanCaughtUpOn()
    {
        var (clock, at) = Clock();
        clock.Resynchronise(at = After(at, 1d));

        Assert.Equal(0, clock.TicksDue(at));
    }
}
