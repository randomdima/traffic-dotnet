using System.Numerics;
using TrafficSimulation.Multiplayer.Wire;
using Xunit;

namespace TrafficSimulation.Tests.Multiplayer.Wire;

/// <summary>
/// <b>What a player and a server say to each other</b> reads back as said, and anything else is refused rather than
/// read as near enough (MP-3, MP-10).
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P2)]
public class MessagesTests
{
    [Fact]
    public void AnInputReadsBackToTheKeysAStepCanHold()
    {
        var sent = new InputMessage(Sequence: 41, Ack: 9, Throttle: -1f, Steer: 0.5f, Handbrake: true, Acts: 255, Boost: true);
        var buffer = new byte[32];

        Assert.True(InputMessage.Read(buffer.AsSpan(0, sent.Write(buffer)), out var read));
        Assert.Equal(sent with { Steer = 64 / (float)Protocol.KeySteps }, read);
    }

    [Fact]
    public void AKeyPastItsTravelIsRefused()
    {
        var buffer = new byte[32];
        var w = new BitWriter(buffer);
        w.Write((uint)MessageKind.Input, 8);
        w.Write(1, 32);
        w.Write(0, 32);
        w.WriteSigned(-(Protocol.KeySteps + 1), 8);
        w.WriteSigned(0, 8);
        w.Write(false);
        w.Write(0, 8);
        w.Write(false);

        Assert.False(InputMessage.Read(buffer.AsSpan(0, w.Finish()), out _));
    }

    [Fact]
    public void AnInputWithBytesAfterItIsRefused()
    {
        var buffer = new byte[32];
        var length = new InputMessage(1, 0, 0f, 0f, false, 0).Write(buffer);

        Assert.False(InputMessage.Read(buffer.AsSpan(0, length + 1), out _));
    }

    [Fact]
    public void AWelcomeReadsBackAsWritten()
    {
        var sent = new Welcome
        {
            Map = "Saltbay",
            PlanDigest = 0x0123_4567_89AB_CDEFUL,
            TickRateHz = 60,
            TicksPerSnapshot = 3,
            ServerTick = 12_345,
            WorldSizeM = new Vector2(1_200f, 900f),
            Movers = new MoverIds(520, 600, 14),
            PositionQuantumM = 0.01f,
            AngleQuantumDeg = 0.1f,
            SmallAngleQuantumDeg = 0.5f,
            SmallMoveM = 8f,
            WidestViewM = 160f,
            InterpolationDelayS = 0.15f,
            LongestExtrapolationS = 0.25f,
        };
        var buffer = new byte[256];

        var read = Welcome.Read(buffer.AsSpan(0, sent.Write(buffer)));

        Assert.NotNull(read);
        Assert.Equivalent(sent, read);
    }

    /// <summary>MP-4: <b>a player may be any mover</b> — the last walker here, past every car — and reads back as written.</summary>
    [Fact]
    public void ASnapshotHeaderReadsBackAsWritten()
    {
        var shape = new WireShape(0.01f, 0.1f, 0.5f, new Vector2(100f, 100f), new MoverIds(50, 10, 0), 5f);
        var sent = new SnapshotHeader(Sequence: 8, Baseline: 6, ServerTick: 999, InputApplied: 30, OwnMover: 59, SeatState.Down);
        var buffer = new byte[64];
        var w = new BitWriter(buffer);
        sent.Write(ref w, shape);
        var r = new BitReader(buffer.AsSpan(0, w.Finish()));

        Assert.Equal(sent, SnapshotHeader.Read(ref r, shape));
        Assert.True(r.Finished);
    }

    [Fact]
    public void ASnapshotHeaderNamingABaselineAtOrPastItselfIsRefused()
    {
        var shape = new WireShape(0.01f, 0.1f, 0.5f, new Vector2(100f, 100f), new MoverIds(50, 10, 0), 5f);
        var buffer = new byte[64];
        var w = new BitWriter(buffer);
        w.Write((uint)MessageKind.Snapshot, 8);
        w.Write(3, 32);
        w.Write(true);
        w.Write(3, 8);
        w.Write(0, 32);
        w.Write(0, 16);
        w.Write(0, shape.IdBits);
        w.Write(0, 1);
        var r = new BitReader(buffer.AsSpan(0, w.Finish()));

        SnapshotHeader.Read(ref r, shape);

        Assert.True(r.Failed);
    }

    /// <summary>MP-15: <b>a hello reads back to the card it said</b>, a name of the longest length included.</summary>
    [Fact]
    public void AHelloReadsBackToItsCard()
    {
        var sent = new SeatCard(0x12_34_56, new string('w', SeatCard.LongestName));
        var buffer = new byte[64];

        Assert.True(Protocol.ReadHello(buffer.AsSpan(0, Protocol.WriteHello(buffer, sent)), out var version, out var read));
        Assert.Equal((Protocol.Version, sent), (version, read));
    }

    /// <summary>MP-10, MP-15: <b>a name holding a byte that is not printable is refused</b>.</summary>
    [Fact]
    public void AHelloWhoseNameIsNotPrintableIsRefused()
    {
        var buffer = new byte[64];
        var length = Protocol.WriteHello(buffer, new SeatCard(0, "a\tb"));

        Assert.False(Protocol.ReadHello(buffer.AsSpan(0, length), out _, out _));
    }

    /// <summary>MP-15: <b>a name is cleaned to what may be sent</b>: printable ASCII, trimmed, no longer than the longest.</summary>
    [Fact]
    public void ANameIsCleanedToPrintableAsciiAndTheLongestLength()
    {
        Assert.Equal("Jane Doe", SeatCard.Clean("  Jane Doe \n"));
        Assert.Equal("Jne Doe", SeatCard.Clean("Jäne Doe"));
        Assert.Equal(new string('x', SeatCard.LongestName), SeatCard.Clean(new string('x', SeatCard.LongestName + 5)));
    }

    /// <summary>MP-1, MP-15: <b>the cards of a snapshot read back by seat</b>, after its header.</summary>
    [Fact]
    public void ASnapshotsCardsReadBackBySeat()
    {
        var buffer = new byte[128];
        var w = new BitWriter(buffer);
        w.Write(true);
        w.Write(3, SeatCard.SeatBits);
        new SeatCard(0xAB_CD_EF, "John Smith").Write(ref w);
        w.Write(false);
        var r = new BitReader(buffer.AsSpan(0, w.Finish()));
        var cards = new SeatCard[1 << SeatCard.SeatBits];

        SeatCard.ReadSection(ref r, cards);

        Assert.True(r.Finished);
        Assert.Equal(new SeatCard(0xAB_CD_EF, "John Smith"), cards[3]);
    }
}
