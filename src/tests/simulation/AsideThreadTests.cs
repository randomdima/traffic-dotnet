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

    /// <summary>
    /// Rule 2, on the one path a steady state can reach late: the first wait the caller cannot spin through, which a
    /// world that always found its step finished first meets on whatever tick the machine is slow.
    /// </summary>
    [Fact]
    public void TheFirstWaitTheCallerHasToBlockOnAllocatesNothing()
    {
        var slow = false;
        using var aside = new AsideThread(() => { if (Volatile.Read(ref slow)) Thread.Sleep(20); }, "test");

        // Each run is over before it is waited for, so the caller never blocks.
        for (var run = 0; run < 8; run++)
        {
            aside.Run();
            Thread.Sleep(20);
            aside.Wait();
        }

        var before = GC.GetAllocatedBytesForCurrentThread() + aside.AllocatedBytes;
        Volatile.Write(ref slow, true);
        aside.Run();
        aside.Wait();

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread() + aside.AllocatedBytes);
    }

    [Fact]
    public void WhatAJobThrowsIsThrownByTheWaitThatEndsIt()
    {
        using var aside = new AsideThread(() => throw new InvalidOperationException("thrown aside"), "test");
        aside.Run();

        Assert.Equal("thrown aside", Assert.Throws<InvalidOperationException>(aside.Wait).Message);
    }
}
