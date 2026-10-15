using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Simulation;

[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P0)]
public class SimClockTests
{
    static SimClock Clock() => new(SimConfig.Shipped().TickSeconds, SimConfig.Shipped().Sim.SoakMaxTimeScale);

    /// <summary>SIM-9: at the game's own speed a frame late by several ticks runs one, so every tick is drawn.</summary>
    [Fact]
    public void ALateFrameAtRealTimeRunsOneTick()
    {
        var clock = Clock();
        Thread.Sleep(60);

        Assert.Equal(1, clock.TicksDue());
    }

    /// <summary>
    /// SIM-9: a tick owed past the cap is run by the next frame and not dropped, so a display beating against the tick's
    /// period — none owed on a frame a hair early, two on the next — does not slow the town.
    /// </summary>
    [Fact]
    public void ATickOwedPastTheCapIsRunByTheNextFrame()
    {
        var clock = Clock();
        Thread.Sleep(TimeSpan.FromSeconds(2.5 * SimConfig.Shipped().TickSeconds));

        Assert.Equal(1, clock.TicksDue());
        Assert.Equal(1, clock.TicksDue());
    }

    /// <summary>SIM-9: a town watched faster runs as many ticks to a late frame as its speed asks, and no more.</summary>
    [Fact]
    public void ALateFrameWatchedFasterRunsItsSpeedsTicks()
    {
        var clock = Clock();
        clock.TimeScale = 3f;
        Thread.Sleep(60);

        Assert.Equal(3, clock.TicksDue());
    }

    /// <summary>
    /// A time scale that stretches the physics delta integrates the whole simulation coarsely and
    /// manufactures collisions the model never had, so it is capped at the soak's own bound.
    /// </summary>
    [Fact]
    public void TheTimeScaleIsCappedAtTheSoaksOwnBound()
    {
        var clock = Clock();

        clock.TimeScale = 100f;
        Assert.Equal(SimConfig.Shipped().Sim.SoakMaxTimeScale, clock.TimeScale);

        clock.TimeScale = -1f;
        Assert.Equal(0f, clock.TimeScale);
    }

    [Fact]
    public void NoTimeIsOwedTheInstantTheClockIsMade()
    {
        Assert.Equal(0, Clock().TicksDue());
    }

    [Fact]
    public void AStallIsForgottenRatherThanCaughtUpOn()
    {
        var clock = Clock();
        Thread.Sleep(60);
        clock.Resynchronise();

        Assert.Equal(0, clock.TicksDue());
    }
}
