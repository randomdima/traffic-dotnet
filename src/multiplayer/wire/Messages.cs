using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace TrafficSimulation.Multiplayer.Wire;

/// <summary>What a message is, in its first byte.</summary>
internal enum MessageKind : byte
{
    /// <summary>A player asking in, with the protocol it speaks and who it says it is.</summary>
    Hello = 1,

    /// <summary>The server letting it in: the town to lay, the shape of the wire, and the car it is seated in.</summary>
    Welcome = 2,

    /// <summary>Or turning it away, and why.</summary>
    Refused = 3,

    /// <summary>The player has laid the town and is ready to be sent it.</summary>
    Ready = 4,

    /// <summary>
    /// What moves in the player's area at one tick (<see cref="SnapshotCodec"/>), after the cards of any seat in it the
    /// player has not been told of (<see cref="SeatCard.ReadSection"/>).
    /// </summary>
    Snapshot = 5,

    /// <summary>The keys a player holds, and the newest snapshot it has (MP-3).</summary>
    Input = 6,
}

internal enum Refusal : byte
{
    /// <summary>The player speaks another protocol, which is another build.</summary>
    Version = 1,

    /// <summary>Every seat is taken.</summary>
    Full = 2,

    /// <summary>The player said something that was not a message (MP-10).</summary>
    Malformed = 4,
}

/// <summary>Where a player's seat stands, as each snapshot says it.</summary>
internal enum SeatState : byte
{
    /// <summary>Somebody, as <see cref="SnapshotHeader.OwnMover"/> is drawn: on foot or in what it drives.</summary>
    Playing = 0,

    /// <summary>Its body lying where it fell, waiting to be stood up as somebody new (MP-4).</summary>
    Down = 1,
}

/// <summary>The wire's own version: a player and a server that differ in it differ in everything after it.</summary>
internal static class Protocol
{
    public const ushort Version = 6;

    /// <summary>A key's travel either way in steps: a pedal or the wheel as a signed byte.</summary>
    public const int KeySteps = 127;

    public static MessageKind KindOf(ReadOnlySpan<byte> message) => message.IsEmpty ? 0 : (MessageKind)message[0];

    /// <summary>A hello: the protocol, and who the player says it is (MP-15).</summary>
    public static int WriteHello(Span<byte> into, in SeatCard card)
    {
        var w = new BitWriter(into);
        w.Write((uint)MessageKind.Hello, 8);
        w.Write(Version, 16);
        card.Write(ref w);
        return Done(ref w);
    }

    /// <summary>
    /// A hello, or false for anything that is not one. <b>Another build's is read only as far as its version</b>, which is
    /// what it is refused for, since what follows the version is that build's to say.
    /// </summary>
    public static bool ReadHello(ReadOnlySpan<byte> message, out ushort version, out SeatCard card)
    {
        var r = new BitReader(message);
        var kind = r.Read(8);
        version = (ushort)r.Read(16);
        card = new SeatCard(0, string.Empty);
        if (version != Version) return kind == (uint)MessageKind.Hello && !r.Failed;

        card = SeatCard.Read(ref r);
        return kind == (uint)MessageKind.Hello && r.Finished;
    }

    public static int WriteRefused(Span<byte> into, Refusal why)
    {
        var w = new BitWriter(into);
        w.Write((uint)MessageKind.Refused, 8);
        w.Write((uint)why, 8);
        return Done(ref w);
    }

    public static bool ReadRefused(ReadOnlySpan<byte> message, out Refusal why)
    {
        var r = new BitReader(message);
        var kind = r.Read(8);
        why = (Refusal)r.Read(8);
        return kind == (uint)MessageKind.Refused && r.Finished;
    }

    public static int WriteReady(Span<byte> into)
    {
        var w = new BitWriter(into);
        w.Write((uint)MessageKind.Ready, 8);
        return Done(ref w);
    }

    public static bool ReadReady(ReadOnlySpan<byte> message)
    {
        var r = new BitReader(message);
        return r.Read(8) == (uint)MessageKind.Ready && r.Finished;
    }

    /// <summary>A message's length, or −1 for one its buffer could not hold.</summary>
    internal static int Done(ref BitWriter w)
    {
        var length = w.Finish();
        return w.Overflowed ? -1 : length;
    }

    internal static void WriteFloat(ref BitWriter w, float value) => w.Write(BitConverter.SingleToUInt32Bits(value), 32);

    internal static float ReadFloat(ref BitReader r)
    {
        var value = BitConverter.UInt32BitsToSingle(r.Read(32));
        if (!float.IsFinite(value)) r.Fail();
        return value;
    }
}

/// <summary>
/// <b>Everything a player sends once it is in</b> (MP-3): the keys as a key could hold them, its count of them, the
/// newest snapshot it holds — which is the baseline the next one it is sent is a difference from — and how many times it
/// has pressed to get in or out (MP-13).
/// </summary>
/// <param name="Acts">
/// <b>A count and not a key</b>, wrapping at a byte: a press lasts a frame, and an input overtaken by a later one is
/// never read, so a press sent as held would be lost whenever it was. The server acts on the count changing.
/// </param>
/// <param name="Boost">Shift held: the hand's boost on whatever the player drives or walks (CTL-5e).</param>
internal readonly record struct InputMessage(
    uint Sequence, uint Ack, float Throttle, float Steer, bool Handbrake, byte Acts, bool Boost = false)
{
    public int Write(Span<byte> into)
    {
        var w = new BitWriter(into);
        w.Write((uint)MessageKind.Input, 8);
        w.Write(Sequence, 32);
        w.Write(Ack, 32);
        w.WriteSigned(ToSteps(Throttle), 8);
        w.WriteSigned(ToSteps(Steer), 8);
        w.Write(Handbrake);
        w.Write(Acts, 8);
        w.Write(Boost);
        return Protocol.Done(ref w);
    }

    /// <summary>An input, or false for anything that is not exactly one — a key past its travel included (MP-10).</summary>
    public static bool Read(ReadOnlySpan<byte> message, out InputMessage input)
    {
        var r = new BitReader(message);
        var kind = r.Read(8);
        var sequence = r.Read(32);
        var ack = r.Read(32);
        var throttle = r.ReadSigned(8);
        var steer = r.ReadSigned(8);
        var handbrake = r.ReadBool();
        var acts = (byte)r.Read(8);
        var boost = r.ReadBool();
        input = new InputMessage(sequence, ack, FromSteps(throttle), FromSteps(steer), handbrake, acts, boost);
        return kind == (uint)MessageKind.Input && r.Finished &&
               Math.Abs(throttle) <= Protocol.KeySteps && Math.Abs(steer) <= Protocol.KeySteps;
    }

    /// <summary>Whether two inputs hold the same keys and presses as the wire carries them, whatever their counts.</summary>
    public bool HoldsTheSameAs(in InputMessage other) =>
        ToSteps(Throttle) == ToSteps(other.Throttle) && ToSteps(Steer) == ToSteps(other.Steer) &&
        Handbrake == other.Handbrake && Acts == other.Acts && Boost == other.Boost;

    static int ToSteps(float key) => (int)MathF.Round(Math.Clamp(key, -1f, 1f) * Protocol.KeySteps);

    static float FromSteps(int steps) => steps / (float)Protocol.KeySteps;
}

/// <summary>What a snapshot says before its movers.</summary>
/// <param name="Sequence">Counted from one by the server for this player; nought names no snapshot.</param>
/// <param name="Baseline">
/// The snapshot this one is a difference from, or nought for none (a whole area); sent as how far back it is, which is
/// never more than <see cref="MostBaselineGap"/>.
/// </param>
/// <param name="ServerTick">The tick it is a picture of.</param>
/// <param name="InputApplied">The newest of this player's inputs the server had applied by then, its low sixteen bits.</param>
/// <param name="OwnMover">
/// What the player is drawn as, by its mover number (<see cref="MoverIds"/>) — its body on foot, the car it is in, the
/// front of the train it drives — or −1 before it is anybody (MP-4).
/// </param>
internal readonly record struct SnapshotHeader(uint Sequence, uint Baseline, uint ServerTick, uint InputApplied, int OwnMover, SeatState Seat)
{
    const int BaselineGapBits = 8;

    const int InputBits = 16;

    /// <summary>The furthest back a baseline may be named.</summary>
    public const uint MostBaselineGap = (1u << BaselineGapBits) - 1;

    /// <summary>The most bits a header can take.</summary>
    public static int MostBits(in WireShape shape) => 8 + 32 + 1 + BaselineGapBits + 32 + InputBits + shape.IdBits + 1;

    public void Write(ref BitWriter w, in WireShape shape)
    {
        Debug.Assert(Baseline == 0 || (Baseline < Sequence && Sequence - Baseline <= MostBaselineGap));
        w.Write((uint)MessageKind.Snapshot, 8);
        w.Write(Sequence, 32);
        w.Write(Baseline != 0);
        if (Baseline != 0) w.Write(Sequence - Baseline, BaselineGapBits);
        w.Write(ServerTick, 32);
        w.Write(InputApplied, InputBits);
        w.Write((uint)(OwnMover + 1), shape.IdBits);
        w.Write((uint)Seat, 1);
    }

    public static SnapshotHeader Read(ref BitReader r, in WireShape shape)
    {
        var kind = r.Read(8);
        if (kind != (uint)MessageKind.Snapshot) r.Fail();
        var sequence = r.Read(32);
        var baseline = 0u;
        if (r.ReadBool())
        {
            var gap = r.Read(BaselineGapBits);
            if (gap == 0 || gap >= sequence) r.Fail();
            else baseline = sequence - gap;
        }

        return new SnapshotHeader(
            sequence, baseline, r.Read(32), r.Read(InputBits), (int)r.Read(shape.IdBits, (uint)shape.Movers.Count) - 1,
            (SeatState)r.Read(1));
    }
}

/// <summary>
/// <b>What a player is told as it is let in</b>: the town to lay and the digest it must lay it to (MP-5), how the town
/// keeps time, the wire's shape, and how it may look and how far behind it draws. The car it is seated in is named by
/// every snapshot, since it is chosen only once the player has laid the town and may change (MP-4). Everything
/// a player needs to read the server is the server's to say, so a player whose own figures differ still reads it right.
/// </summary>
internal sealed class Welcome
{
    const int LongestMapName = 64;

    public required string Map { get; init; }

    public required ulong PlanDigest { get; init; }

    public required int TickRateHz { get; init; }

    public required int TicksPerSnapshot { get; init; }

    public required uint ServerTick { get; init; }

    public required Vector2 WorldSizeM { get; init; }

    public required MoverIds Movers { get; init; }

    public required float PositionQuantumM { get; init; }

    public required float AngleQuantumDeg { get; init; }

    public required float SmallAngleQuantumDeg { get; init; }

    public required float SmallMoveM { get; init; }

    public required float WidestViewM { get; init; }

    public required float InterpolationDelayS { get; init; }

    public required float LongestExtrapolationS { get; init; }

    public WireShape Shape => new(PositionQuantumM, AngleQuantumDeg, SmallAngleQuantumDeg, WorldSizeM, Movers, SmallMoveM);

    public int Write(Span<byte> into)
    {
        var w = new BitWriter(into);
        w.Write((uint)MessageKind.Welcome, 8);
        w.Write(Protocol.Version, 16);
        var name = Encoding.UTF8.GetBytes(Map);
        if (name.Length > LongestMapName) return -1;

        w.Write((uint)name.Length, 8);
        foreach (var b in name) w.Write(b, 8);
        w.Write((uint)PlanDigest, 32);
        w.Write((uint)(PlanDigest >> 32), 32);
        w.Write((uint)TickRateHz, 16);
        w.Write((uint)TicksPerSnapshot, 16);
        w.Write(ServerTick, 32);
        Protocol.WriteFloat(ref w, WorldSizeM.X);
        Protocol.WriteFloat(ref w, WorldSizeM.Y);
        w.Write((uint)Movers.Cars, 32);
        w.Write((uint)Movers.People, 32);
        w.Write((uint)Movers.Wagons, 32);
        Protocol.WriteFloat(ref w, PositionQuantumM);
        Protocol.WriteFloat(ref w, AngleQuantumDeg);
        Protocol.WriteFloat(ref w, SmallAngleQuantumDeg);
        Protocol.WriteFloat(ref w, SmallMoveM);
        Protocol.WriteFloat(ref w, WidestViewM);
        Protocol.WriteFloat(ref w, InterpolationDelayS);
        Protocol.WriteFloat(ref w, LongestExtrapolationS);
        return Protocol.Done(ref w);
    }

    /// <summary>A welcome, or null for anything that is not one a player of this version can lay a town from.</summary>
    public static Welcome? Read(ReadOnlySpan<byte> message)
    {
        var r = new BitReader(message);
        if (r.Read(8) != (uint)MessageKind.Welcome || r.Read(16) != Protocol.Version) return null;

        var nameLength = (int)r.Read(8, LongestMapName);
        Span<byte> name = stackalloc byte[LongestMapName];
        for (var i = 0; i < nameLength; i++) name[i] = (byte)r.Read(8);
        var welcome = new Welcome
        {
            Map = Encoding.UTF8.GetString(name[..nameLength]),
            PlanDigest = r.Read(32) | ((ulong)r.Read(32) << 32),
            TickRateHz = (int)r.Read(16),
            TicksPerSnapshot = (int)r.Read(16),
            ServerTick = r.Read(32),
            WorldSizeM = new Vector2(Protocol.ReadFloat(ref r), Protocol.ReadFloat(ref r)),
            Movers = new MoverIds((int)r.Read(32), (int)r.Read(32), (int)r.Read(32)),
            PositionQuantumM = Protocol.ReadFloat(ref r),
            AngleQuantumDeg = Protocol.ReadFloat(ref r),
            SmallAngleQuantumDeg = Protocol.ReadFloat(ref r),
            SmallMoveM = Protocol.ReadFloat(ref r),
            WidestViewM = Protocol.ReadFloat(ref r),
            InterpolationDelayS = Protocol.ReadFloat(ref r),
            LongestExtrapolationS = Protocol.ReadFloat(ref r),
        };

        var sane = welcome.TickRateHz > 0 && welcome.TicksPerSnapshot > 0 && welcome.PositionQuantumM > 0f &&
                   welcome.AngleQuantumDeg > 0f && welcome.SmallAngleQuantumDeg > 0f && welcome.SmallMoveM > 0f && welcome.WorldSizeM.X > 0f &&
                   welcome.WorldSizeM.Y > 0f && welcome.Movers.Cars > 0 && welcome.Movers.People >= 0 &&
                   welcome.Movers.Wagons >= 0;
        return r.Finished && sane ? welcome : null;
    }
}
