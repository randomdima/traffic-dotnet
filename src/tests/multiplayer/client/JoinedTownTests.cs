using System.Buffers.Binary;
using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Multiplayer.Client;
using TrafficSimulation.Multiplayer.Wire;
using TrafficSimulation.Runtime;
using Xunit;

namespace TrafficSimulation.Tests.Multiplayer.Client;

/// <summary>
/// MP-3: <b>a player's keys go when they say something new</b> — never more often than the server ticks, and at least
/// once a keep-alive however still they are held.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P6)]
public class JoinedTownTests
{
    const double FrameS = 1d / 144d;

    static readonly Welcome Town = new()
    {
        Map = "Test",
        PlanDigest = 7,
        TickRateHz = 60,
        TicksPerSnapshot = 3,
        ServerTick = 0,
        WorldSizeM = new Vector2(500f, 500f),
        Movers = new MoverIds(Cars: 4, People: 2, Wagons: 0),
        PositionQuantumM = 0.02f,
        AngleQuantumDeg = 0.1f,
        SmallAngleQuantumDeg = 0.5f,
        SmallMoveM = 8f,
        WidestViewM = 160f,
        InterpolationDelayS = 0.15f,
        LongestExtrapolationS = 0.25f,
        Seats = 16,
    };

    /// <summary>The instants, over a second of 144 Hz frames, at which an input left — the keys at each frame chosen by <paramref name="keysAt"/>.</summary>
    static List<double> InputsSent(Func<int, float> keysAt)
    {
        var link = new ServerLink();
        var joined = new JoinedTown(link, SimConfig.Shipped(), new SeatCard(0xFF0000, "John Smith"));
        var welcome = new byte[256];
        Arrived(link, welcome.AsSpan(0, Town.Write(welcome)));
        joined.Read(0d);
        Assert.True(joined.Laid(Town.PlanDigest));

        var sent = new List<double>();
        for (var frame = 0; frame < 144; frame++)
        {
            var localS = 1d + frame * FrameS;
            Arrived(link, []);
            joined.Hold(keysAt(frame), 0f, false);
            joined.Read(localS);
            foreach (var message in Sent(link))
            {
                if (Protocol.KindOf(message) == MessageKind.Input) sent.Add(localS);
            }
        }

        return sent;
    }

    /// <summary>An open link whose last pump brought <paramref name="message"/>, or nothing for an empty one.</summary>
    static void Arrived(ServerLink link, ReadOnlySpan<byte> message)
    {
        var inbox = link.Inbox;
        var used = message.IsEmpty ? 0 : 4 + message.Length;
        BinaryPrimitives.WriteInt32LittleEndian(inbox, used);
        inbox[4] = (byte)LinkState.Open;
        if (!message.IsEmpty)
        {
            BinaryPrimitives.WriteInt32LittleEndian(inbox[8..], message.Length);
            message.CopyTo(inbox[12..]);
        }

        link.Rewind();
    }

    /// <summary>What the frame wrote to go, emptied as a pump empties it.</summary>
    static List<byte[]> Sent(ServerLink link)
    {
        var outbox = link.Outbox;
        var used = BinaryPrimitives.ReadInt32LittleEndian(outbox);
        var messages = new List<byte[]>();
        for (var at = 4; at < 4 + used;)
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(outbox[at..]);
            messages.Add(outbox.Slice(at + 4, length).ToArray());
            at += 4 + length;
        }

        BinaryPrimitives.WriteInt32LittleEndian(outbox, 0);
        return messages;
    }

    [Fact]
    public void KeysHeldStillAreSentOnceAKeepAlive()
    {
        var keepAliveS = SimConfig.Shipped().Multiplayer.InputKeepAliveS;
        var sent = InputsSent(_ => 0.5f);

        for (var i = 1; i < sent.Count; i++) Assert.InRange(sent[i] - sent[i - 1], keepAliveS, keepAliveS + FrameS);
    }

    [Fact]
    public void KeysChangedEveryFrameAreSentNoOftenerThanTheServerTicks()
    {
        var sent = InputsSent(frame => frame % 2 == 0 ? 1f : -1f);

        for (var i = 1; i < sent.Count; i++) Assert.InRange(sent[i] - sent[i - 1], 1d / Town.TickRateHz, 1d / Town.TickRateHz + FrameS);
    }
}
