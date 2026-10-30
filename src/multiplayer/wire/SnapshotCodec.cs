using TrafficSimulation.CityGen;

namespace TrafficSimulation.Multiplayer.Wire;

/// <summary>
/// <b>The movers of a player's area, as a difference from a baseline the player already holds</b>: the numbers of those
/// that left it, then a record for each that entered it or changed — only the fields that changed, a move rather than a
/// place where it is short. A mover unchanged since the baseline costs nothing, and one entering is a change from
/// nothing, so there is one record and one way to read it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both lists are in mover order</b>, which is what lets every number be sent as the gap from the last and both
/// sides walk the baseline once.
/// </para>
/// <para>
/// <b>The baseline is the one the player acknowledged</b>, never merely the last one sent, so a snapshot lost on the
/// way costs a larger difference and never a wrong one; an empty baseline is a whole area.
/// </para>
/// <para>
/// <b>A change is sent as the step from the baseline where the step is short</b>: a move in the narrowest of three
/// widths that holds it, a heading as the turn, a road wheel or a lean as the nudge — each behind a prefix saying which,
/// and the whole figure where none holds it. A mover entering has no baseline, and is sent whole without the prefixes.
/// </para>
/// </remarks>
internal static class SnapshotCodec
{
    [Flags]
    enum Field : uint
    {
        Position = 1,
        Heading = 2,
        Steer = 4,
        Tilt = 8,
        Lamps = 16,
        Marks = 32,
    }

    const int FieldBits = 6;

    const int LookBits = 16;

    /// <summary>A walker's move between two snapshots, and a car's crawl.</summary>
    const int TinyMoveBits = 6;

    /// <summary>A car's move across the few snapshots an acknowledgement lags by.</summary>
    const int ShortMoveBits = 9;

    const int TurnBits = 7;

    const int NudgeBits = 5;

    /// <summary>Enough for every <see cref="MoverFlags"/> at once.</summary>
    static readonly int FlagBits = WireShape.BitsFor((uint)(MoverFlags.Wrecked | MoverFlags.Walking | MoverFlags.Down));

    /// <summary>A bit a level of the town (<see cref="CityPlan.RoadArrays.ChannelOf"/>), which is all a mover's channels are.</summary>
    const int ChannelBits = CityPlan.RoadArrays.Over + 1;

    public static void Write(ref BitWriter w, in WireShape shape, ReadOnlySpan<MoverState> now, ReadOnlySpan<MoverState> baseline)
    {
        w.Write((uint)CountLeft(now, baseline), shape.IdBits);
        var last = -1;
        var n = 0;
        foreach (ref readonly var was in baseline)
        {
            while (n < now.Length && now[n].Id < was.Id) n++;
            if (n < now.Length && now[n].Id == was.Id) continue;

            WriteGap(ref w, shape, was.Id - last - 1);
            last = was.Id;
        }

        w.Write((uint)CountEntries(now, baseline), shape.IdBits);
        last = -1;
        var b = 0;
        foreach (ref readonly var mover in now)
        {
            while (b < baseline.Length && baseline[b].Id < mover.Id) b++;
            var held = b < baseline.Length && baseline[b].Id == mover.Id;
            if (held && mover.SameAs(baseline[b])) continue;

            WriteGap(ref w, shape, mover.Id - last - 1);
            last = mover.Id;
            WriteRecord(ref w, shape, mover, held ? baseline[b] : default, entered: !held);
        }
    }

    /// <summary>
    /// The movers a snapshot holds, read against the baseline it was written against into <paramref name="into"/> in mover
    /// order; or −1 for a message that is not one (MP-10). <paramref name="entries"/> and <paramref name="left"/> are
    /// working room, each as long as <paramref name="into"/>.
    /// </summary>
    public static int Read(
        ref BitReader r, in WireShape shape, ReadOnlySpan<MoverState> baseline, Span<MoverState> into,
        Span<MoverState> entries, Span<int> left)
    {
        var leftCount = (int)r.Read(shape.IdBits, (uint)Math.Min(baseline.Length, left.Length));
        var last = -1;
        for (var i = 0; i < leftCount && !r.Failed; i++)
        {
            last = ReadId(ref r, shape, last);
            left[i] = last;
        }

        var entryCount = (int)r.Read(shape.IdBits, (uint)entries.Length);
        last = -1;
        var b = 0;
        for (var i = 0; i < entryCount && !r.Failed; i++)
        {
            last = ReadId(ref r, shape, last);
            while (b < baseline.Length && baseline[b].Id < last) b++;
            var held = b < baseline.Length && baseline[b].Id == last;
            entries[i] = ReadRecord(ref r, shape, held ? baseline[b] : default, last, entered: !held);
        }

        return r.Failed ? -1 : Merge(baseline, entries[..entryCount], left[..leftCount], into);
    }

    /// <summary>The baseline with what left taken out and what entered or changed laid over it, still in mover order.</summary>
    static int Merge(ReadOnlySpan<MoverState> baseline, ReadOnlySpan<MoverState> entries, ReadOnlySpan<int> left, Span<MoverState> into)
    {
        int b = 0, e = 0, l = 0, count = 0;
        while (b < baseline.Length || e < entries.Length)
        {
            var nextB = b < baseline.Length ? baseline[b].Id : int.MaxValue;
            var nextE = e < entries.Length ? entries[e].Id : int.MaxValue;
            if (count == into.Length) return -1;

            if (nextE <= nextB)
            {
                if (nextE == nextB) b++;
                into[count++] = entries[e++];
            }
            else if (l < left.Length && left[l] == nextB)
            {
                l++;
                b++;
            }
            else
            {
                into[count++] = baseline[b++];
            }
        }

        // A number said to have left that the baseline never held, or one that left and changed at once, is not a
        // message this codec writes.
        return l == left.Length ? count : -1;
    }

    static void WriteRecord(ref BitWriter w, in WireShape shape, in MoverState now, in MoverState was, bool entered)
    {
        var fields = FieldsChanged(now, was);
        w.Write((uint)fields, FieldBits);
        if ((fields & Field.Position) != 0) WritePosition(ref w, shape, now, was, entered);
        if ((fields & Field.Heading) != 0) WriteHeading(ref w, shape, now.Heading, was.Heading, entered);
        if ((fields & Field.Steer) != 0) WriteSmallAngle(ref w, shape, now.Steer, was.Steer, entered);
        if ((fields & Field.Tilt) != 0)
        {
            WriteSmallAngle(ref w, shape, now.TiltAlong, was.TiltAlong, entered);
            WriteSmallAngle(ref w, shape, now.TiltAcross, was.TiltAcross, entered);
        }

        if ((fields & Field.Lamps) != 0) w.Write(now.Lamps, 8);
        if ((fields & Field.Marks) != 0)
        {
            w.Write((uint)now.Flags, FlagBits);
            w.Write(now.Channels, ChannelBits);
            w.Write(now.Driver, shape.DriverBits);
        }

        if (entered) w.Write(now.Look, LookBits);
    }

    static MoverState ReadRecord(ref BitReader r, in WireShape shape, in MoverState was, int id, bool entered)
    {
        var mover = was;
        mover.Id = id;
        var fields = (Field)r.Read(FieldBits);
        if ((fields & Field.Position) != 0) ReadPosition(ref r, shape, ref mover, was, entered);
        if ((fields & Field.Heading) != 0) mover.Heading = ReadHeading(ref r, shape, was.Heading, entered);
        if ((fields & Field.Steer) != 0) mover.Steer = ReadSmallAngle(ref r, shape, was.Steer, entered);
        if ((fields & Field.Tilt) != 0)
        {
            mover.TiltAlong = ReadSmallAngle(ref r, shape, was.TiltAlong, entered);
            mover.TiltAcross = ReadSmallAngle(ref r, shape, was.TiltAcross, entered);
        }

        if ((fields & Field.Lamps) != 0) mover.Lamps = (byte)r.Read(8);
        if ((fields & Field.Marks) != 0)
        {
            mover.Flags = (MoverFlags)r.Read(FlagBits);
            mover.Channels = (byte)r.Read(ChannelBits);
            mover.Driver = (byte)r.Read(shape.DriverBits);
        }

        if (entered) mover.Look = (ushort)r.Read(LookBits);
        return mover;
    }

    /// <summary>
    /// A place, as the narrowest move that holds it — <c>0</c> tiny, <c>10</c> short, <c>110</c> as far as the area's
    /// margin — or <c>111</c> and the place itself; an entering mover's place bare.
    /// </summary>
    static void WritePosition(ref BitWriter w, in WireShape shape, in MoverState now, in MoverState was, bool entered)
    {
        var (dx, dy) = (now.X - was.X, now.Y - was.Y);
        if (!entered)
        {
            if (Fits(dx, TinyMoveBits) && Fits(dy, TinyMoveBits))
            {
                w.Write(0, 1);
                WriteMove(ref w, dx, dy, TinyMoveBits);
                return;
            }

            if (Fits(dx, ShortMoveBits) && Fits(dy, ShortMoveBits))
            {
                w.Write(0b01, 2);
                WriteMove(ref w, dx, dy, ShortMoveBits);
                return;
            }

            if (Math.Abs(dx) <= shape.MostSmallMove && Math.Abs(dy) <= shape.MostSmallMove)
            {
                w.Write(0b011, 3);
                WriteMove(ref w, dx, dy, shape.SmallMoveBits);
                return;
            }

            w.Write(0b111, 3);
        }

        w.Write((uint)now.X, shape.PositionBits);
        w.Write((uint)now.Y, shape.PositionBits);
    }

    static void WriteMove(ref BitWriter w, int dx, int dy, int bits)
    {
        w.WriteSigned(dx, bits);
        w.WriteSigned(dy, bits);
    }

    static void ReadPosition(ref BitReader r, in WireShape shape, ref MoverState mover, in MoverState was, bool entered)
    {
        var moveBits = entered ? 0
            : !r.ReadBool() ? TinyMoveBits
            : !r.ReadBool() ? ShortMoveBits
            : !r.ReadBool() ? shape.SmallMoveBits
            : 0;
        if (moveBits == 0)
        {
            mover.X = (int)r.Read(shape.PositionBits, (uint)shape.MostPosition);
            mover.Y = (int)r.Read(shape.PositionBits, (uint)shape.MostPosition);
            return;
        }

        mover.X = was.X + r.ReadSigned(moveBits);
        mover.Y = was.Y + r.ReadSigned(moveBits);
        if (!InPlace(shape, mover)) Fail(ref r);
    }

    /// <summary>A heading as the turn from the baseline's, the short way round, where it is short; the heading itself otherwise.</summary>
    static void WriteHeading(ref BitWriter w, in WireShape shape, int now, int was, bool entered)
    {
        var turn = (int)MathF.IEEERemainder(now - was, shape.HeadingSteps);
        var turned = !entered && Fits(turn, TurnBits);
        if (!entered) w.Write(turned);
        if (turned) w.WriteSigned(turn, TurnBits);
        else w.Write((uint)now, shape.HeadingBits);
    }

    static ushort ReadHeading(ref BitReader r, in WireShape shape, int was, bool entered)
    {
        if (entered || !r.ReadBool()) return (ushort)r.Read(shape.HeadingBits, (uint)shape.HeadingSteps - 1);

        var heading = (was + r.ReadSigned(TurnBits)) % shape.HeadingSteps;
        return (ushort)(heading < 0 ? heading + shape.HeadingSteps : heading);
    }

    /// <summary>A road wheel's angle or a lean, as the nudge from the baseline's where it is short; the angle itself otherwise.</summary>
    static void WriteSmallAngle(ref BitWriter w, in WireShape shape, int now, int was, bool entered)
    {
        var nudged = !entered && Fits(now - was, NudgeBits);
        if (!entered) w.Write(nudged);
        if (nudged) w.WriteSigned(now - was, NudgeBits);
        else w.WriteSigned(now, shape.SmallAngleBits);
    }

    static short ReadSmallAngle(ref BitReader r, in WireShape shape, int was, bool entered)
    {
        var quanta = !entered && r.ReadBool() ? was + r.ReadSigned(NudgeBits) : r.ReadSigned(shape.SmallAngleBits);
        if (Math.Abs(quanta) > shape.MostSmallAngle) Fail(ref r);
        return (short)quanta;
    }

    static bool Fits(int value, int bits) => Zigzag.Encode(value) >> bits == 0;

    /// <summary>
    /// The most bits one record can take — a long gap, every field, a place rather than a move, its look — which, with
    /// the two counts, bounds a snapshot of so many movers.
    /// </summary>
    public static int MostBits(in WireShape shape, int movers)
    {
        var recordBits = 2 + shape.IdBits + FieldBits + 3 + 2 * shape.PositionBits + 1 + shape.HeadingBits +
                         3 * (1 + shape.SmallAngleBits) + 8 + FlagBits + ChannelBits + shape.DriverBits + LookBits;
        return 2 * shape.IdBits + movers * (recordBits + 2 + shape.IdBits);
    }

    static bool InPlace(in WireShape shape, in MoverState mover) =>
        mover.X >= 0 && mover.X <= shape.MostPosition && mover.Y >= 0 && mover.Y <= shape.MostPosition;

    static void Fail(ref BitReader r) => r.Fail();

    static Field FieldsChanged(in MoverState now, in MoverState was)
    {
        var fields = default(Field);
        if (now.X != was.X || now.Y != was.Y) fields |= Field.Position;
        if (now.Heading != was.Heading) fields |= Field.Heading;
        if (now.Steer != was.Steer) fields |= Field.Steer;
        if (now.TiltAlong != was.TiltAlong || now.TiltAcross != was.TiltAcross) fields |= Field.Tilt;
        if (now.Lamps != was.Lamps) fields |= Field.Lamps;
        if (now.Flags != was.Flags || now.Channels != was.Channels || now.Driver != was.Driver) fields |= Field.Marks;
        return fields;
    }

    /// <summary>
    /// A gap between two numbers in the order: under 16 in five bits, under 256 in ten, anything else in the numbers'
    /// own width and two — most areas are thick with movers, so most gaps are short.
    /// </summary>
    static void WriteGap(ref BitWriter w, in WireShape shape, int gap)
    {
        if (gap < 16)
        {
            w.Write(0, 1);
            w.Write((uint)gap, 4);
        }
        else if (gap < 256)
        {
            w.Write(1, 2);
            w.Write((uint)gap, 8);
        }
        else
        {
            w.Write(3, 2);
            w.Write((uint)gap, shape.IdBits);
        }
    }

    static int ReadId(ref BitReader r, in WireShape shape, int last)
    {
        var gap = r.ReadBool()
            ? r.ReadBool() ? (int)r.Read(shape.IdBits) : (int)r.Read(8)
            : (int)r.Read(4);
        var id = last + 1 + gap;
        if (id >= shape.Movers.Count) Fail(ref r);
        return id;
    }

    static int CountLeft(ReadOnlySpan<MoverState> now, ReadOnlySpan<MoverState> baseline)
    {
        var count = 0;
        var n = 0;
        foreach (ref readonly var was in baseline)
        {
            while (n < now.Length && now[n].Id < was.Id) n++;
            if (n >= now.Length || now[n].Id != was.Id) count++;
        }

        return count;
    }

    static int CountEntries(ReadOnlySpan<MoverState> now, ReadOnlySpan<MoverState> baseline)
    {
        var count = 0;
        var b = 0;
        foreach (ref readonly var mover in now)
        {
            while (b < baseline.Length && baseline[b].Id < mover.Id) b++;
            if (b >= baseline.Length || baseline[b].Id != mover.Id || !mover.SameAs(baseline[b])) count++;
        }

        return count;
    }
}
