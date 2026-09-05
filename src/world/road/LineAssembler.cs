using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>How much of a line there is: the arcs it took, the lanes it covers, and how far it runs.</summary>
internal readonly record struct DrivenLine(int ArcCount, int LaneCount, float LengthM);

/// <summary>
/// Route into geometry: the whole of what a car is asked to drive, as one chain of arcs.
/// <b>Pure and engine-free</b> — it takes a graph and a run of lanes and writes arcs into spans the
/// caller owns, so the line a car drives can be laid out and judged with no solver, no body and no town.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every line is drawn for the rear axle</b> (CAR-4a), the one point on a car that travels the way
/// the car is pointing. The lane lines the graph carries are that already, so nothing here re-derives
/// an offset; what this adds is the joins between them.
/// </para>
/// <para>
/// <b>The join through a junction is geometry and not a lane</b>, and it is not <em>this</em> code's
/// either. A junction's ground belongs to no lane, because an intersection is a transition and never a
/// destination (CAR-6.2a); the line across it is laid once with the town, per turn, by
/// <see cref="RoadGraph"/>, and what this does is thread the joins a route uses between the lanes they
/// join, trimmed by the same setbacks those joins were taken at. That is what makes the whole thing one
/// chain: the follower projects onto it, samples ahead on it and reads its curvature without ever
/// knowing which piece it is on — and it is what makes a line a car drives and a movement an overlay
/// draws the same arcs rather than two constructions that agree until one of them is changed.
/// </para>
/// <para>
/// <b>A line reaches at least as far as the car can stop from its own top speed.</b> Anything shorter
/// is a car braking for the end of its own knowledge: the speed profile has to see every corner within
/// braking range, and at the speed cap that is a couple of hundred metres and several junctions. The
/// line is re-laid one lane at a time as the car leaves each, so the cost is one assembly per junction
/// crossed and never one per tick.
/// </para>
/// </remarks>
internal static class LineAssembler
{
    /// <summary>The most lanes a line is laid over, whatever the distance says — a bound on the work, not a figure behaviour reads.</summary>
    public const int MostLanes = 12;

    /// <summary>What the arc budget per car has to be for a line of <see cref="MostLanes"/> lanes and the joins between them.</summary>
    public static int ArcsFor(RoadGraph graph)
    {
        var most = 0;
        for (var lane = 0; lane < graph.LaneCount; lane++) most = Math.Max(most, graph.ArcsOf(lane).Length);

        return (most + 2) * MostLanes + (MostLanes - 1) * 2;
    }

    /// <summary>
    /// The line over a run of lanes, with the town's own join through each junction threaded between
    /// them. <paramref name="laneStartM"/> and <paramref name="laneEndM"/> come back holding where each
    /// lane begins and ends along it — so the stretch between one lane's end and the next lane's start
    /// is the junction, and the car is inside it exactly while its progress is.
    /// </summary>
    /// <remarks>
    /// <b>A lane the next one does not follow ends the line.</b> A route is a chain of turns and every
    /// pair of lanes in it meets at a node, so this is not a case that arises from a route — but a line
    /// laid over a pair the town has no join for would have to invent one, and inventing geometry is the
    /// one thing this may not do. The car is handed the shorter line and asks for another when it runs
    /// out, which is what it already does with every route that runs out.
    /// </remarks>
    /// <param name="lastLaneToM">
    /// How far along the final lane the line stops, where something past the road is going to take the
    /// car off it — the place a way at a parking bay leaves its lane is the one case. The line ends there
    /// rather than at the lane's own end, so the profile brakes for the manoeuvre and not for the kerb
    /// beyond it.
    /// </param>
    /// <param name="tail">
    /// A way to finish on that is not one of the graph's lanes — <b>the line into a parking bay</b>, which
    /// leaves its lane at <paramref name="lastLaneToM"/> and carries the car to the bay's own pose.
    /// <para>
    /// <b>Arcs and not a way number</b>: what the assembler needs is geometry that begins where the last
    /// lane's stops, and which of the town's features drew it is that feature's business. Threaded here
    /// rather than driven as a line of its own, the whole of a leg is one chain the follower reads without
    /// knowing which piece it is on — which is the same reason the joins through a junction are threaded.
    /// </para>
    /// </param>
    public static DrivenLine Assemble(
        RoadGraph graph, ReadOnlySpan<int> lanes, Span<ArcSeg> into, Span<float> laneStartM,
        Span<float> laneEndM, float lastLaneToM = float.PositiveInfinity, ReadOnlySpan<ArcSeg> tail = default,
        float tailToM = float.PositiveInfinity)
    {
        var written = 0;
        var lengthM = 0f;
        var arrivedOn = RoadGraph.NoConnector;

        for (var index = 0; index < lanes.Length; index++)
        {
            var lane = lanes[index];
            var leavingOn = index < lanes.Length - 1 ? graph.ConnectorBetween(lane, lanes[index + 1]) : RoadGraph.NoConnector;

            if (arrivedOn != RoadGraph.NoConnector)
            {
                var join = graph.ConnectorArcs(arrivedOn);
                join.CopyTo(into[written..]);
                written += join.Length;
                lengthM += graph.ConnectorLengthM(arrivedOn);
            }

            // <b>A lane is threaded whole</b> (TER-5d): it was cut back to the points its movements hand
            // over at, so the join before it ends where its line begins and the join after it begins where
            // its line ends.
            var toM = graph.LaneLengthM[lane];
            if (index == lanes.Length - 1) toM = MathF.Min(toM, MathF.Max(0f, lastLaneToM));

            laneStartM[index] = lengthM;
            var laid = Spline.SubChainInto(graph.ArcsOf(lane), 0f, toM, into[written..]);
            for (var arc = 0; arc < laid; arc++) lengthM += into[written + arc].LengthM;
            written += laid;
            laneEndM[index] = lengthM;

            if (leavingOn == RoadGraph.NoConnector && index < lanes.Length - 1)
            {
                return new DrivenLine(written, index + 1, lengthM);
            }

            arrivedOn = leavingOn;
        }

        // Only ever off the end of a whole chain: a line cut short above never reached the lane the tail
        // leaves, so there is nothing for it to be threaded onto.
        //
        // <b>And only as far as the tail is driven</b>: a way at a bay runs on past the pose a car comes to
        // rest in, to the end of the space itself, and that run is ground rather than line
        // (<c>BayWays.DrivenLengthM</c>). Threaded whole, every leg would end a car's length deeper in its
        // bay than the paint puts it.
        var tailArcs = Spline.SubChainInto(tail, 0f, tailToM, into[written..]);
        for (var arc = 0; arc < tailArcs; arc++) lengthM += into[written + arc].LengthM;

        return new DrivenLine(written + tailArcs, lanes.Length, lengthM);
    }

    /// <summary>
    /// <b>Where a place on one of a line's lanes falls along the line itself</b> — a painted bar, a
    /// crossing, anything the town measured against a lane and a driver has to meet on its own line.
    /// </summary>
    /// <remarks>
    /// <b>The two measures run at the same rate and start together.</b> The line over a lane <em>is</em>
    /// that lane's own arcs and nothing else (<see cref="Spline.SubChainInto"/>), and a lane begins where
    /// the join before it ended (TER-5d) — so a metre of one is a metre of the other, measured from the
    /// same place. There is no origin here to get wrong and no geometry to walk. It is still clamped to the
    /// lane's own stretch, because a line may stop short of a lane's end for the way into a bay.
    /// </remarks>
    public static float OnTheLineM(
        ReadOnlySpan<float> laneStartM, ReadOnlySpan<float> laneEndM, int slot, float alongLaneM) =>
        Math.Clamp(laneStartM[slot] + alongLaneM, laneStartM[slot], laneEndM[slot]);
}
