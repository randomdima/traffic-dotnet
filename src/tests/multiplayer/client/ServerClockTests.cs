using TrafficSimulation.Multiplayer.Client;
using Xunit;

namespace TrafficSimulation.Tests.Multiplayer.Client;

/// <summary>
/// MP-7: <b>a player draws a fixed delay behind the server's clock as its arrivals tell it</b> — the least-delayed of
/// them, never one late — and follows a server that slows without the picture jumping.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class ServerClockTests
{
    const float DelayS = 0.15f;
    const float SlewShare = 0.05f;

    [Fact]
    public void ALateArrivalSaysNothingOfTheClock()
    {
        var clock = new ServerClock(samples: 8, DelayS, SlewShare);
        clock.Arrived(serverS: 10.0, localS: 100.02);
        clock.Arrived(serverS: 10.05, localS: 100.12);

        Assert.Equal(10.0 - 100.02 + 101.0, clock.ServerS(101.0), 9);
    }

    [Fact]
    public void TheInstantDrawnIsTheDelayBehindTheEstimate()
    {
        var clock = new ServerClock(samples: 8, DelayS, SlewShare);
        clock.Arrived(serverS: 10.0, localS: 100.0);

        Assert.Equal(10.0 - DelayS, clock.Drawn(100.0), 9);
    }

    [Fact]
    public void TheInstantDrawnClosesOnASlowerServerAtNoMoreThanTheSlew()
    {
        var clock = new ServerClock(samples: 1, DelayS, SlewShare);
        clock.Arrived(serverS: 10.0, localS: 100.0);
        var before = clock.Drawn(100.0);

        // The server has fallen 0.05 s behind real time over the next second.
        clock.Arrived(serverS: 10.95, localS: 101.0);
        var after = clock.Drawn(101.0);

        Assert.Equal(1.0 - SlewShare, after - before, 6);
    }
}
