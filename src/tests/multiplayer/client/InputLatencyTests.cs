using TrafficSimulation.Multiplayer.Client;
using Xunit;

namespace TrafficSimulation.Tests.Multiplayer.Client;

/// <summary>
/// <b>A player's keys are timed to the snapshot answering them and to the frame drawing that snapshot's tick</b>, one
/// input at a time.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P7)]
public class InputLatencyTests
{
    [Fact]
    public void AnInputIsTimedToTheFirstSnapshotThatAppliedItAndTheFirstFrameAtThatTick()
    {
        var latency = new InputLatency();
        latency.Sent(5, localS: 1.00);
        latency.Answered(inputApplied: 4, serverTick: 297, localS: 1.05);
        latency.Answered(inputApplied: 5, serverTick: 300, localS: 1.08);
        latency.Drawn(tick: 299.5, localS: 1.20);
        latency.Drawn(tick: 300.2, localS: 1.25);

        var (roundTrip, toPicture) = latency.Take();
        Assert.Equal((1, 0.08), (roundTrip.Count, Math.Round(roundTrip.MeanS, 6)));
        Assert.Equal((1, 0.25), (toPicture.Count, Math.Round(toPicture.MeanS, 6)));
    }

    [Fact]
    public void AnInputSentWhileAnotherIsTimedIsNotTimed()
    {
        var latency = new InputLatency();
        latency.Sent(5, localS: 1.00);
        latency.Sent(6, localS: 1.02);
        latency.Answered(inputApplied: 6, serverTick: 300, localS: 1.08);

        Assert.Equal(0.08, Math.Round(latency.Take().RoundTrip.MeanS, 6));
    }

    [Fact]
    public void ASnapshotSaysTheInputAppliedInItsLowSixteenBits()
    {
        var latency = new InputLatency();
        latency.Sent(65_541, localS: 1.00);
        latency.Answered(inputApplied: 65_541 & 0xFFFF, serverTick: 300, localS: 1.08);

        Assert.Equal(1, latency.Take().RoundTrip.Count);
    }
}
