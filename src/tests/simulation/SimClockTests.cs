using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Simulation;

[Trait(Tier.Key, Tier.Unit)]
public class SimClockTests
{
    static SimClock Clock() => new(SimConfig.Shipped().TickSeconds, SimConfig.Shipped().Sim.SoakMaxTimeScale);

    /// <summary>A stall owes several ticks, never one long one, and never more than it can catch up on.</summary>
    [Fact]
    public void ACatchUpIsBoundedRatherThanASpiral()
    {
        var clock = Clock();
        Thread.Sleep(60);

        Assert.InRange(clock.TicksDue(), 1, clock.MaxTicksPerCall);
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
