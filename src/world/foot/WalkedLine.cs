using System.Numerics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// A walking route as a <b>line</b>: the run-links a search returned, turned into the points a body
/// actually walks through, on the lane of each stretch the walker's own side of it asks for.
/// </summary>
/// <remarks>
/// <para>
/// <b>The line is the route's own geometry and never a straight line between nodes.</b> A stretch of
/// pavement round a junction corner or round a car park is an arc, and a walk that took its two ends
/// would cut the corner across the carriageway — which is the one thing the walking network was laid
/// this way to prevent.
/// </para>
/// <para>
/// <b>A straight stretch contributes one point and a bent one contributes several.</b> What decides is
/// the stretch's own curvature rather than a spacing applied everywhere: a city's route is a hundred
/// stretches, and a point every few metres down every one of them would be a walker following a line
/// nobody can hold in the budget it has.
/// </para>
/// <para>
/// <b>Both ends are partial.</b> The walker stands part-way along the stretch it is on and the
/// destination stands part-way along the last one, so the first and last pieces are laid from and to
/// where those actually are — laying the whole of either sends a walker back the way it came to start.
/// </para>
/// </remarks>
internal static class WalkedLine
{
    /// <summary>The furthest apart two points of a walk are ever laid, however straight the ground is.</summary>
    public const float StepM = 4f;

    /// <summary>
    /// What a point that stands on no way of the network carries: the one straight hop off it onto a
    /// doorstep, which is walked over ground the pavement graph has never heard of.
    /// </summary>
    /// <remarks>
    /// Apart from every way a point can carry, which are the stretches themselves and the complements of
    /// the mitres — and <c>~0</c> is a mitre, so nothing nearer zero would do.
    /// </remarks>
    public const int NoWay = int.MinValue;

    /// <summary>And the nearest, so a hairpin does not fill the whole budget.</summary>
    const float ClosestStepM = 0.5f;

    /// <summary>
    /// Lays the walked points of a route into <paramref name="into"/> and answers how many.
    /// </summary>
    /// <param name="complete">
    /// False where the points ran out before the route did — the walk is then laid again from where the
    /// body has got to, exactly as a driver's route is.
    /// </param>
    /// <param name="toleranceM">How far the straight between two of the walk's points may bow off the ground's own arc.</param>
    /// <param name="crossingOfEdge">Which crossing each stretch is, or −1 where it is pavement.</param>
    /// <param name="intoCrossing">
    /// Filled beside <paramref name="into"/>: which crossing each point stands on, or −1. <b>It is what a
    /// walker asks the kerb about</b> — the first point of a run of them is the far side of a step off a
    /// pavement, and the point before it is the kerb the question is asked from.
    /// </param>
    /// <param name="intoWay">
    /// And which way of the pavement each point stands on: the stretch's own directed edge, or the
    /// <em>complement</em> of a mitre's turn slot where the point is on a corner. <b>It is what the walking
    /// book is laid from</b> — a walker's place on the network is read off the point it is walking at, and
    /// re-deriving it from the body would be a second opinion about where the walk goes.
    /// </param>
    /// <param name="intoAlongM">How far along that way's own line the point stands, in the lane's metres.</param>
    public static int Lay(
        WalkingNetwork walking, ReadOnlySpan<int> links, RouteEntry entry, RouteGoal goal, float toleranceM,
        ReadOnlySpan<int> crossingOfEdge, Span<Vector2> into, Span<int> intoCrossing,
        Span<int> intoWay, Span<float> intoAlongM, out bool complete)
    {
        complete = true;
        if (links.Length == 0 || into.Length == 0) return 0;

        var runs = walking.Runs;
        var foot = walking.Foot;
        var written = 0;
        var arrivedOn = WalkingNetwork.NoTurn;
        var arrivedCarried = false;

        for (var index = 0; index < links.Length; index++)
        {
            var link = links[index];
            var pieces = runs.PiecesOf(link);

            var fromSlot = 0;
            var fromEdgeM = 0f;
            if (index == 0 && link == entry.Link) fromSlot = runs.PieceAt(link, entry.AlongM, out fromEdgeM);

            var toSlot = LastSlot(runs, links, goal, index);
            var toEdgeM = float.PositiveInfinity;
            if (index == links.Length - 1 && link == goal.Link) runs.PieceAt(link, goal.AlongM, out toEdgeM);

            for (var slot = fromSlot; slot <= toSlot; slot++)
            {
                var edge = pieces[slot];
                var walked = walking.LaneOf(edge);
                if (walked.Length == 0) continue;

                // The offset line is shorter inside a bend and longer outside it, so a place measured
                // along the stretch is carried over as a fraction of it rather than as a distance — and
                // as a fraction of the lane's own ground, since a corner the lane carries stands past the
                // end of the stretch that ground belongs to.
                var edgeLengthM = MathF.Max(1e-4f, foot.LengthM(edge));
                var walkedLengthM = walking.LaneLengthM(edge);
                var alongTheStretchM = walkedLengthM - walking.TailLengthM(edge);
                var fromM = slot == fromSlot ? fromEdgeM / edgeLengthM * alongTheStretchM : 0f;
                var toM = slot == toSlot && float.IsFinite(toEdgeM)
                    ? toEdgeM / edgeLengthM * alongTheStretchM
                    : walkedLengthM;

                // The corner out of this stretch, and the one into it: the lane gives up the ground the
                // corner stands on at either end, so the two are one line and not two that cross. A corner
                // the lane carries takes nothing from it — the lane is already the corner, and it runs to
                // where the next one is walked from.
                var onward = NextEdge(runs, links, goal, index, slot);
                var leavingOn = onward < 0 ? WalkingNetwork.NoTurn : walking.TurnSlot(edge, onward);
                var carried = leavingOn != WalkingNetwork.NoTurn && leavingOn == walking.TailOf(edge);
                if (leavingOn != WalkingNetwork.NoTurn && !carried)
                {
                    toM = MathF.Min(toM, walkedLengthM - walking.JoinFromM(leavingOn));
                }

                // Wherever the walk arrived from, it starts where that corner put it down — the carried
                // ones included, which is the same point by a line the walk has already covered.
                if (arrivedOn != WalkingNetwork.NoTurn) fromM = MathF.Max(fromM, walking.JoinToM(arrivedOn));

                // <b>A mitre belongs to the stretch it leads onto</b>, so the question a walker asks the
                // road is asked from the near side of the corner rather than from inside it: a corner
                // into a crossing gives up ground on the paint as well as on the pavement, and a walker
                // that only noticed the crossing once it was round the corner would already be on it.
                if (arrivedOn != WalkingNetwork.NoTurn && !arrivedCarried
                    && !Station(
                        walking.JoinArcs(arrivedOn), 0f, walking.JoinLengthM(arrivedOn), toleranceM,
                        crossingOfEdge[edge], ~arrivedOn, into, intoCrossing, intoWay, intoAlongM, ref written))
                {
                    complete = false;
                    return written;
                }

                if (!Station(
                        walked, fromM, toM, toleranceM, crossingOfEdge[edge], edge, into, intoCrossing, intoWay,
                        intoAlongM, ref written))
                {
                    complete = false;
                    return written;
                }

                arrivedOn = leavingOn;
                arrivedCarried = carried;
            }
        }

        return written;
    }

    /// <summary>
    /// The stretch that follows this one on the route, or −1 where the route ends here. <b>It is looked
    /// ahead for rather than remembered</b>, because the corner a stretch gives ground up to is a fact
    /// about the pair and the ground has to be given up before the stretch is laid.
    /// </summary>
    static int NextEdge(RunNetwork runs, ReadOnlySpan<int> links, RouteGoal goal, int index, int slot)
    {
        if (slot < LastSlot(runs, links, goal, index)) return runs.PiecesOf(links[index])[slot + 1];
        if (index + 1 >= links.Length) return -1;

        var onward = runs.PiecesOf(links[index + 1]);
        return onward.Length == 0 ? -1 : onward[0];
    }

    /// <summary>The last piece of a link the route actually walks — the whole run, or as far as the destination stands.</summary>
    static int LastSlot(RunNetwork runs, ReadOnlySpan<int> links, RouteGoal goal, int index) =>
        index == links.Length - 1 && links[index] == goal.Link
            ? runs.PieceAt(links[index], goal.AlongM, out _)
            : runs.PiecesOf(links[index]).Length - 1;

    /// <summary>
    /// Stations one chain between two distances along it, stopping short and answering false where the
    /// walk's own points run out. A straight needs its far end and nothing else; a bent one is stationed
    /// so the chord between two points bows off the arc by no more than the walked tolerance.
    /// </summary>
    static bool Station(
        ReadOnlySpan<ArcSeg> chain, float fromM, float toM, float toleranceM, int crossing, int way,
        Span<Vector2> into, Span<int> intoCrossing, Span<int> intoWay, Span<float> intoAlongM, ref int written)
    {
        if (chain.Length == 0 || toM <= fromM) return true;

        var stepM = StepFor(chain, toleranceM);
        for (var atM = fromM; atM < toM;)
        {
            atM = atM + stepM < toM - (stepM * 0.5f) ? atM + stepM : toM;
            if (written >= into.Length) return false;

            intoCrossing[written] = crossing;
            intoWay[written] = way;
            intoAlongM[written] = atM;
            into[written++] = Spline.SampleAt(chain, atM).PositionM;
        }

        return true;
    }

    /// <summary>
    /// How far apart to station a stretch so the straight between two of its points bows off the arc by
    /// no more than <paramref name="toleranceM"/>. A straight needs only its far end; the tightest bend
    /// on the walk is what sets the step for the whole of it.
    /// </summary>
    /// <remarks>
    /// <b>This is what keeps a walk out of the road</b>, and it is not a smoothness question: the tightest
    /// thing a pavement bends round is a junction corner filleted at half a walk, and a chord laid a
    /// fixed few metres across one of those clips the carriageway inside it. The chord itself is
    /// <see cref="Spline.ChordForSagM"/>, which is the same rule the layers that draw a bend step it at.
    /// </remarks>
    static float StepFor(ReadOnlySpan<ArcSeg> arcs, float toleranceM)
    {
        var bend = 0f;
        foreach (var arc in arcs) bend = MathF.Max(bend, MathF.Abs(arc.Curvature));

        // A straight's chord is unbounded and clamps to the furthest two points are ever stationed apart.
        return Math.Clamp(Spline.ChordForSagM(bend, toleranceM), ClosestStepM, StepM);
    }

}
