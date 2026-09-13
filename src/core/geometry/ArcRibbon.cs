using System.Numerics;

namespace TrafficSimulation.Core.Geometry;

/// <summary>
/// <b>The ground one line lays, as the closed line round it</b>: a chain's two edges half a width either
/// side of it and the square end at either end (TER-3c.6), strung into one ring.
/// </summary>
/// <remarks>
/// <para>
/// <b>An offset of an arc is an arc</b>, so a ribbon is exact: each piece keeps the angle its parent
/// subtends and moves its radius (<see cref="Spline.OffsetInto"/>), and the ends are the one straight each
/// really is. Nothing here is sampled, so nothing downstream inherits a chord where the ground has a bend.
/// </para>
/// <para>
/// <b>The ground is on the walker's right the whole way round</b> (TER-3c.9). That is what fixes the order:
/// the left edge is walked the way the line runs, the far end is crossed to the right edge, the right edge
/// is walked back, and the near end closes the ring — four turns, each of which leaves the ribbon's inside
/// on the same hand. A ribbon walked the other way round hands every normal back inverted while still
/// looking like a perfectly good closed line.
/// </para>
/// <para>
/// <b>A ribbon may fold through itself and that is not this type's to fix.</b> A line that bends tighter
/// than half its own width offsets to an edge that runs backwards, and the piece comes back with the
/// negative length the arithmetic gives it. Whatever merges ribbons decides what a fold is; cutting one out
/// here would be a second opinion about the same shape.
/// </para>
/// </remarks>
internal static class ArcRibbon
{
    /// <summary>How many pieces the ribbon of a chain of <paramref name="arcs"/> pieces holds: two edges and two ends.</summary>
    public static int Count(int arcs) => (arcs * 2) + 2;

    /// <summary>
    /// <b>The ribbon of one line at one half-width</b>, as the closed chain it is — empty for a line with
    /// no pieces or no width.
    /// </summary>
    /// <remarks>
    /// <b>The line is read as the pieces it really turns at</b> (<see cref="Spline.JoinedInto"/>,
    /// <paramref name="joinM"/>): a lane laid in three straights along one bearing is one straight, and an
    /// edge of it is one piece of ribbon rather than three that a reader downstream has to notice are the
    /// same. It is the same shape either way — an offset is taken piece by piece and a joined piece offsets
    /// to the join of the offsets — and everything that weighs this ribbon against another does so piece
    /// against piece, so the pieces the line does not need are paid for by every one of them.
    /// </remarks>
    public static ArcSeg[] Of(ReadOnlySpan<ArcSeg> line, float halfM, float joinM)
    {
        if (line.Length == 0 || halfM <= 0f) return [];

        var joined = new ArcSeg[line.Length];
        var edges = Spline.JoinedInto(line, joinM, joined);
        var ring = new ArcSeg[Count(edges)];

        // The left edge the way the line runs, then the right edge walked back — which is the order that
        // leaves the ribbon's inside on the walker's right throughout.
        Spline.OffsetInto(joined.AsSpan(0, edges), -halfM, ring.AsSpan(0, edges));

        var right = new ArcSeg[edges];
        Spline.OffsetInto(joined.AsSpan(0, edges), halfM, right);
        Spline.ReverseInto(right, ring.AsSpan(edges + 1, edges));

        ring[edges] = Across(ring[edges - 1].EndM, ring[edges + 1].StartM);
        ring[^1] = Across(ring[^2].EndM, ring[0].StartM);
        return ring;
    }

    /// <summary>The square end: the straight from one edge to the other.</summary>
    static ArcSeg Across(Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        return new ArcSeg(fromM, MathF.Atan2(runM.Y, runM.X), runM.Length(), 0f);
    }
}
