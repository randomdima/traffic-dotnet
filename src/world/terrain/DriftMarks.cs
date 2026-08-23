using System.Numerics;

namespace TrafficSimulation.World.Terrain;

/// <summary>
/// One stretch of mark: a quad spanning the travel of one wheel since the last one, the width of the
/// tyre that made it, and how hard that tyre was working the ground over it.
/// </summary>
/// <remarks>
/// <b>Ploughed is what it is and not how it looks</b> — displaced ground rather than rubber laid on a
/// surface that is still there. What each kind is drawn as belongs to whatever is drawing it.
/// </remarks>
internal readonly record struct DriftMark(Vector2 CentreM, float LengthM, float WidthM, float HeadingRad, float Intensity, bool Ploughed);

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
/// The buffer is a ring holding its whole capacity: the oldest mark is overwritten once it is full,
/// which is the only sense in which a permanent mark has a limit. It is laid once and never grows, so
/// a town driven for an hour costs exactly what one driven for a minute costs.
/// </para>
/// </remarks>
internal sealed class DriftMarks
{
    /// <summary>Shorter than this and there is no mark to speak of.</summary>
    const float MinLengthM = 1e-3f;

    readonly DriftMark[] _marks;
    int _next;

    public DriftMarks(int capacity) => _marks = new DriftMark[Math.Max(1, capacity)];

    /// <summary>How many of the ring's places have been written — the whole of it once it has wrapped.</summary>
    public int Count { get; private set; }

    public ReadOnlySpan<DriftMark> Laid => _marks.AsSpan(0, Count);

    /// <summary>Lay one stretch, from where the wheel was when the stretch began to where it is now.</summary>
    public void Mark(Vector2 fromM, Vector2 toM, float widthM, float intensity, bool ploughed)
    {
        var spanM = toM - fromM;
        var lengthM = spanM.Length();
        if (lengthM < MinLengthM || intensity <= 0f || widthM <= 0f) return;

        _marks[_next] = new DriftMark(
            Vector2.Lerp(fromM, toM, 0.5f), lengthM, widthM, MathF.Atan2(spanM.Y, spanM.X),
            Math.Clamp(intensity, 0f, 1f), ploughed);

        _next = (_next + 1) % _marks.Length;
        Count = Math.Min(Count + 1, _marks.Length);
    }

    /// <summary>Forget the lot, which is what opening another town does.</summary>
    public void Clear()
    {
        _next = 0;
        Count = 0;
    }
}
