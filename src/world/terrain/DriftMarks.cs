using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Terrain;

/// <summary>
/// <b>One knot of a mark's stroke</b> (<c>drift_mark_rows.glsl</c>'s <c>DriftKnot</c>): where the wheel was, the way it was
/// travelling there, the width of the tyre — negative where it was laid on the level above the ground — how hard it
/// worked the ground — negative where it ploughed rather than laid rubber — and the knot before it on its stroke, by its
/// place among every knot ever laid (<see cref="DriftMarks.Total"/>), or <see cref="DriftMarks.NoKnot"/> where it starts
/// one. Twenty-four bytes.
/// </summary>
/// <remarks>
/// A knot is drawn as the curve from the knot before it: a cubic through both, leaving one and meeting the other along
/// each's way of travel (<c>mark.vert</c>), its width and strength running straight between theirs. A stroke's first knot
/// draws nothing of its own. <b>A knot is drawn with its level</b> (TER-7b): one on a deck over the deck, and one on the
/// ground under it.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct DriftKnot(Vector2 AtM, float HeadingRad, float WidthM, float Strength, uint Previous)
{
    /// <summary>Whether the tyre ploughed soft ground here rather than laid rubber on hard.</summary>
    public bool Ploughed => Strength < 0f;

    /// <summary>
    /// Whether the tyre's car was drawn on the level above the ground (PHY-1a) — on a deck over other roads or its
    /// bridgehead, a channel above the ground's among its own.
    /// </summary>
    public bool Raised => WidthM < 0f;
}

/// <summary>
/// <b>The words a ring of <see cref="DriftKnot"/> is drawn by</b> (<c>DriftMarkRing</c>): the draw first — a curve's strip
/// of <see cref="Corners"/> corners as many times as there are knots — so the block is the indirect buffer of that draw,
/// then every knot laid since the ring was cleared (the low 32 bits of <see cref="DriftMarks.Total"/>), how many places
/// are written, how many the ring holds, and how many a tick on the device has claimed. The oldest written is at
/// <c>Total % Capacity</c> once the ring is full and at nought before.
/// </summary>
/// <remarks>
/// <b>The total is 32 bits on the device</b>, so a ring whose capacity is not a power of two loses its place once
/// 2³² knots have been laid — years of a town skidding.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct DriftMarkRing(
    uint Corners, uint Drawn, uint FirstCorner, uint FirstDrawn, uint Total, uint Laid, uint Capacity, uint Claimed)
{
    /// <summary>
    /// How many pieces a knot's curve is drawn in (<c>DriftKnotPieces</c>): enough that the most a stroke turns between two
    /// knots (<c>MarkFigures.KnotTurnDeg</c>) reads as a curve.
    /// </summary>
    public const int Pieces = 8;

    /// <summary>A curve's strip: two corners a piece and two to close it (<c>DriftKnotCorners</c>).</summary>
    public const uint StripCorners = 2 * (Pieces + 1);

    /// <summary>The ring after <paramref name="total"/> knots laid into <paramref name="capacity"/> places.</summary>
    public static DriftMarkRing After(long total, int capacity)
    {
        var laid = (uint)Math.Min(total, capacity);
        return new DriftMarkRing(StripCorners, laid, 0, 0, (uint)total, laid, (uint)capacity, 0);
    }
}

/// <summary>
/// <b>How far a stroke's last knot may stand from the knot before it before it is left where it is</b>
/// (<c>SimConfig.MarkKnotTurnCos</c>, <c>MarkFigures.KnotMostM</c>, <c>KnotStrengthStep</c>): turned through, as the
/// cosine of the most, run along, or grown darker or fainter by.
/// </summary>
internal readonly record struct StrokeFit(float TurnCos, float MostM, float StrengthStep)
{
    public static StrokeFit Of(SimConfig config) => new(config.MarkKnotTurnCos, config.Marks.KnotMostM, config.Marks.KnotStrengthStep);
}

/// <summary>
/// The record the traffic leaves on the ground: wherever a tyre works the surface harder than it can
/// shrug off — a slide on tarmac, simply rolling over grass — that stretch of ground is marked, and
/// stays marked.
/// </summary>
/// <remarks>
/// <para>
/// <b>Marks are pure scenery.</b> Nothing samples them, no agent sees one, and no rule is written
/// against one. They are the only thing in the town that is written by the physics and read by
/// nothing but the renderer.
/// </para>
/// <para>
/// <b>A wheel's marks are a stroke of knots</b>, and not a stamp every stretch it lays (<see cref="Lay"/>): its last knot
/// moves with the wheel while the stroke still bends and darkens within <see cref="StrokeFit"/> of the knot before it,
/// and is left where it stands only once it does not — so a straight skid is two knots however long, and a ring of
/// knots holds a town's marks at a small share of the room a stamp a stretch took, which is what a copy of them, to a
/// frame or to a player, carries. <c>drift_marks.glsl</c> lays them on the device line for line.
/// </para>
/// <para>
/// The buffer is a ring holding its whole capacity: the oldest knot is overwritten once it is full,
/// which is the only sense in which a permanent mark has a limit. It is laid once and never grows, so
/// a town driven for an hour costs exactly what one driven for a minute costs.
/// </para>
/// <para>
/// <b>Every place written is logged</b> (<see cref="Written"/>, <see cref="WrittenSlot"/>), a knot laid or a last knot
/// moved alike, so a reader keeping a copy takes only what changed since it last looked.
/// </para>
/// </remarks>
internal sealed class DriftMarks
{
    /// <summary>A stroke's first knot's <see cref="DriftKnot.Previous"/>: nothing before it.</summary>
    public const uint NoKnot = uint.MaxValue;

    /// <summary>Shorter than this and there is no mark to speak of.</summary>
    const float MinLengthM = 1e-3f;

    readonly DriftKnot[] _knots;
    readonly StrokeFit _fit;

    /// <summary>Each wheel's stroke's last knot, by its place among every knot laid, or <see cref="NoKnot"/>.</summary>
    readonly uint[] _lastOf;

    readonly int[] _log;

    /// <param name="wheels">How many wheels may lay a stroke: a car's <c>TyreModel.MostWheels</c> a car of the roster.</param>
    public DriftMarks(int capacity, int wheels, StrokeFit fit)
    {
        _knots = new DriftKnot[Math.Max(1, capacity)];
        _log = new int[_knots.Length];
        _lastOf = new uint[Math.Max(1, wheels)];
        _fit = fit;
        Clear();
    }

    /// <summary>How many of the ring's places have been written — the whole of it once it has wrapped.</summary>
    public int Count { get; private set; }

    /// <summary>How many places the ring holds.</summary>
    public int Capacity => _knots.Length;

    /// <summary>
    /// <b>Every knot laid since the ring was last cleared</b>, the overwritten among them: knot <c>t</c> is at place
    /// <c>t % Capacity</c> while it is one of the last <see cref="Capacity"/>.
    /// </summary>
    public long Total { get; private set; }

    /// <summary>Every place written since the ring was last cleared, a knot laid or a last knot moved.</summary>
    public long Written { get; private set; }

    /// <summary>How far back <see cref="WrittenSlot"/> reaches: a reader further behind takes the whole ring.</summary>
    public int LogCapacity => _log.Length;

    /// <summary>The place the <paramref name="written"/>-th write went to, while it is one of the last <see cref="LogCapacity"/>.</summary>
    public int WrittenSlot(long written) => _log[(int)(written % _log.Length)];

    public ReadOnlySpan<DriftKnot> Laid => _knots.AsSpan(0, Count);

    /// <summary>
    /// <b>Every place written since a copy last took them</b>, into <paramref name="into"/> at the same places, and
    /// <paramref name="copied"/> moved up to <see cref="Written"/>: the logged places only, or the whole ring where the log
    /// has turned over past the copy, or the ring was cleared under it. Answers the run of places that covers every one
    /// taken, from and past, empty where none was.
    /// </summary>
    public (int From, int To) CopyChangedInto(Span<DriftKnot> into, ref long copied)
    {
        var written = Written;
        if (copied > written) copied = 0;

        var (from, to) = (Capacity, 0);
        if (written - copied > _log.Length)
        {
            _knots.AsSpan(0, Count).CopyTo(into);
            (from, to) = (0, Count);
        }
        else
        {
            for (var write = copied; write < written; write++)
            {
                var slot = WrittenSlot(write);
                into[slot] = _knots[slot];
                (from, to) = (Math.Min(from, slot), Math.Max(to, slot + 1));
            }
        }

        copied = written;
        return from < to ? (from, to) : (0, 0);
    }

    public ref readonly DriftKnot At(int slot) => ref _knots[slot];

    /// <summary>
    /// <b>Lay one stretch of wheel <paramref name="wheel"/>'s travel</b>, from where it was when the stretch began to where
    /// it is now: the wheel's stroke drawn on to it where the stretch carries on from its last knot in the same kind, width
    /// and level, and a stroke begun where it does not. The last knot is moved to the stretch's end while the stroke stays
    /// within <see cref="StrokeFit"/> of the knot before; past it, it is left where it was and a knot laid on.
    /// </summary>
    /// <param name="raised">Whether the wheel's car is drawn on the level above the ground (<see cref="DriftKnot.Raised"/>).</param>
    public void Lay(int wheel, Vector2 fromM, Vector2 toM, float widthM, float intensity, bool ploughed, bool raised = false)
    {
        var spanM = toM - fromM;
        if (spanM.Length() < MinLengthM || intensity <= 0f || widthM <= 0f) return;

        var strength = Math.Clamp(intensity, 0f, 1f) * (ploughed ? -1f : 1f);
        var leveledWidthM = raised ? -widthM : widthM;
        var headingRad = Trig.Atan2(spanM.Y, spanM.X);
        var last = _lastOf[wheel];
        if (Holds(last) && _knots[SlotOf(last)] is var lastKnot && lastKnot.AtM == fromM && lastKnot.WidthM == leveledWidthM
            && lastKnot.Ploughed == ploughed)
        {
            var pinned = lastKnot.Previous;
            if (Holds(pinned) && !Bends(_knots[SlotOf(pinned)], toM, headingRad, strength))
            {
                Write(SlotOf(last), new DriftKnot(toM, headingRad, leveledWidthM, strength, pinned));
                return;
            }

            _lastOf[wheel] = Append(new DriftKnot(toM, headingRad, leveledWidthM, strength, last));
            return;
        }

        var first = Append(new DriftKnot(fromM, headingRad, leveledWidthM, strength, NoKnot));
        _lastOf[wheel] = Append(new DriftKnot(toM, headingRad, leveledWidthM, strength, first));
    }

    /// <summary>Whether the curve from <paramref name="pinned"/> to a last knot here would stray past <see cref="StrokeFit"/>.</summary>
    bool Bends(in DriftKnot pinned, Vector2 toM, float headingRad, float strength) =>
        MathF.Cos(headingRad - pinned.HeadingRad) < _fit.TurnCos
        || Vector2.DistanceSquared(pinned.AtM, toM) > _fit.MostM * _fit.MostM
        || MathF.Abs(strength - pinned.Strength) > _fit.StrengthStep;

    /// <summary>Whether knot <paramref name="knot"/> of every knot laid is still in the ring.</summary>
    bool Holds(uint knot) => knot != NoKnot && (uint)Total - knot <= (uint)Count;

    int SlotOf(uint knot) => (int)(knot % (uint)Capacity);

    uint Append(in DriftKnot knot)
    {
        var laid = (uint)Total;
        Write((int)(Total % Capacity), knot);
        Count = Math.Min(Count + 1, Capacity);
        Total++;
        return laid;
    }

    void Write(int slot, in DriftKnot knot)
    {
        _knots[slot] = knot;
        _log[(int)(Written % _log.Length)] = slot;
        Written++;
    }

    /// <summary>Forget the lot, which is what opening another town does.</summary>
    public void Clear()
    {
        Count = 0;
        Total = 0;
        Written = 0;
        Array.Fill(_lastOf, NoKnot);
    }
}
