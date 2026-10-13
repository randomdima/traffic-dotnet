using TrafficSimulation.Core.Simulation;
using Xunit;

namespace TrafficSimulation.Tests.Simulation;

/// <summary>One job on a thread of its own (<see cref="AsideThread"/>): run whole, waited for, and let go of with a run in flight.</summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class AsideThreadTests
{
    /// <summary>A world disposed one tick after it stepped: the step begun runs before the thread ends.</summary>
    [Fact]
    public void ARunBegunBeforeDisposeIsRunWhole()
    {
        for (var round = 0; round < 200; round++)
        {
            var runs = 0;
            var aside = new AsideThread(() => runs++, "test");
            aside.Run();
            aside.Dispose();

            Assert.Equal(1, runs);
        }
    }

    [Fact]
    public void WhatAJobThrowsIsThrownByTheWaitThatEndsIt()
    {
        using var aside = new AsideThread(() => throw new InvalidOperationException("thrown aside"), "test");
        aside.Run();

        Assert.Equal("thrown aside", Assert.Throws<InvalidOperationException>(aside.Wait).Message);
    }
}
