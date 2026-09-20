using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// A walked route drawn as a <b>line</b>: the ways of a route chain, stationed into the points something
/// has to draw a polyline through.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nobody walks this.</b> A walker holds the chain and is held on the network's own arcs, one way at a
/// time (<see cref="World.Routing.RouteChain"/>, <see cref="WalkingNetwork.WayArcs"/>); the straights
/// between stations exist only because a screen draws straights. Laying them under a body was what made a
/// walk a chain of chords across ground that is arcs, and made the tolerance below a thing the town had to
/// be safe at rather than a thing a picture had to look right at.
/// </para>
/// <para>
/// <b>A straight way contributes one point and a bent one contributes several</b>, by its own curvature
/// rather than by a spacing applied everywhere: a city's route is a hundred ways, and a point every few
/// metres down each of them is more than any drawing needs.
/// </para>
/// </remarks>
internal static class WalkedLine
{
    /// <summary>The furthest apart two points are ever stationed, however straight the ground is.</summary>
    public const float StepM = 4f;

    /// <summary>And the nearest, so a hairpin does not fill the whole buffer.</summary>
    const float ClosestStepM = 0.5f;

    /// <summary>
    /// Stations the ways of a chain into <paramref name="into"/> and answers how many points were written.
    /// </summary>
    /// <param name="fromM">How far along the first way of the chain to begin, the body standing part-way along it.</param>
    /// <param name="toM">And how far along the last one to stop, the destination standing part-way along it.</param>
    /// <param name="toleranceM">How far the straight between two points may bow off the ground's own arc.</param>
    /// <param name="complete">False where the points ran out before the chain did.</param>
    public static int Station(
        WalkingNetwork walking, ReadOnlySpan<int> ways, float fromM, float toM, float toleranceM,
        Span<Vector2> into, out bool complete)
    {
        complete = true;
        var written = 0;

        for (var slot = 0; slot < ways.Length; slot++)
        {
            var way = ways[slot];
            var before = slot > 0 ? ways[slot - 1] : WalkingNetwork.NoLane;
            var after = slot + 1 < ways.Length ? ways[slot + 1] : WalkingNetwork.NoLane;

            walking.SpanOfWay(before, way, after, out var startM, out var endM);
            if (slot == 0) startM = MathF.Max(startM, fromM);
            if (slot == ways.Length - 1) endM = MathF.Min(endM, toM);

            if (!Stationed(walking.WayArcs(way), startM, endM, toleranceM, into, ref written))
            {
                complete = false;
                return written;
            }
        }

        return written;
    }

    /// <summary>
    /// Stations one chain between two distances along it, stopping short and answering false where the
    /// points run out. A straight needs its far end and nothing else; a bent one is stationed so the chord
    /// between two points bows off the arc by no more than the tolerance.
    /// </summary>
    static bool Stationed(
        ReadOnlySpan<ArcSeg> chain, float fromM, float toM, float toleranceM, Span<Vector2> into, ref int written)
    {
        if (chain.Length == 0 || toM <= fromM) return true;

        var stepM = StepFor(chain, toleranceM);
        for (var atM = fromM; atM < toM;)
        {
            atM = atM + stepM < toM - (stepM * 0.5f) ? atM + stepM : toM;
            if (written >= into.Length) return false;

            into[written++] = Spline.SampleAt(chain, atM).PositionM;
        }

        return true;
    }

    /// <summary>
    /// How far apart to station a chain so the straight between two of its points bows off the arc by no
    /// more than <paramref name="toleranceM"/>. The tightest bend sets the step for the whole of it.
    /// </summary>
    static float StepFor(ReadOnlySpan<ArcSeg> arcs, float toleranceM)
    {
        var bend = 0f;
        foreach (var arc in arcs) bend = MathF.Max(bend, MathF.Abs(arc.Curvature));

        // A straight's chord is unbounded and clamps to the furthest two points are ever stationed apart.
        return Math.Clamp(Spline.ChordForSagM(bend, toleranceM), ClosestStepM, StepM);
    }
}
