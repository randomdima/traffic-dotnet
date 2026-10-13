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

    /// <summary>
    /// <b>The most arcs a line is laid with</b> — a bound on each car's room for its line, not a figure behaviour reads: a
    /// line stops at the last lane sure to fit, as it stops at <see cref="MostLanes"/>, and the car asks for more as it
    /// runs out. OdesaOsm's longest held 57 (`--bench room`), where room for twelve of the town's longest lanes was 343.
    /// </summary>
    public const int MostArcs = 96;

    /// <summary>The most one lane and the join into it add to a line.</summary>
    public static int MostArcsOfALane(RoadGraph graph)
    {
        var most = 0;
        for (var lane = 0; lane < graph.LaneCount; lane++) most = Math.Max(most, graph.ArcsOf(lane).Length);

        return most + Spline.MostMovementArcs;
    }

    /// <summary>What one lane and the join into it from <paramref name="fromLane"/> add to a line.</summary>
    public static int ArcsOfALane(RoadGraph graph, int fromLane, int lane)
    {
        var join = fromLane < 0 ? RoadGraph.NoConnector : graph.ConnectorBetween(fromLane, lane);
        return graph.ArcsOf(lane).Length + (join == RoadGraph.NoConnector ? 0 : graph.ConnectorArcs(join).Length);
    }

    /// <summary>The room each car has for its line: <see cref="MostArcs"/>, or two of the town's longest lanes where that is more.</summary>
    public static int ArcsFor(RoadGraph graph) => Math.Max(MostArcs, 2 * MostArcsOfALane(graph));

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
    /// How far along the final lane the line stops, where the leg ends short of the lane's own end — where the
    /// car waits for its manoeuvre into the bay it is aimed at, or the place in the road it was sent to. The car
    /// is brought to rest there, and either takes the manoeuvre as its next line or stands where it was going
    /// (<c>TownWorld.TakeTheNextStepOfTheLeg</c>).
    /// </param>
    /// <param name="laneFirstArc">
    /// Where given, the arc each lane's own arcs begin at — what a line shifted on past its first lane drops the arcs
    /// in front of (<c>CarFleet.ShiftTheLine</c>).
    /// </param>
    /// <param name="keptWhole">
    /// How many of the first lanes already stand in <paramref name="into"/> as an assembly over these very lanes laid
    /// them, each with a lane after it — a line grown at its far end. Their arcs are kept as they stand and only their
    /// metres summed again, so the line comes out to the bit as one assembled whole; <paramref name="laneFirstArc"/>
    /// has to hold where the earlier assembly began each of them and the one after.
    /// </param>
    public static DrivenLine Assemble(
        RoadGraph graph, ReadOnlySpan<int> lanes, Span<ArcSeg> into, Span<float> laneStartM,
        Span<float> laneEndM, float lastLaneToM = float.PositiveInfinity, Span<int> laneFirstArc = default,
        int keptWhole = 0)
    {
        var written = 0;
        var lengthM = 0f;
        var arrivedOn = RoadGraph.NoConnector;

        for (var index = 0; index < lanes.Length; index++)
        {
            var lane = lanes[index];
            var leavingOn = index < lanes.Length - 1 ? graph.ConnectorBetween(lane, lanes[index + 1]) : RoadGraph.NoConnector;
            var kept = index < keptWhole;

            if (arrivedOn != RoadGraph.NoConnector)
            {
                var join = graph.ConnectorArcs(arrivedOn);
                if (!kept) join.CopyTo(into[written..]);
                written += join.Length;
                lengthM += graph.ConnectorLengthM(arrivedOn);
            }

            // <b>A lane is threaded whole</b> (TER-5d): every join lands on its first point, and the join
            // after it begins where its line ends.
            var toM = LaidToM(graph, lane, index == lanes.Length - 1, lastLaneToM);

            laneStartM[index] = lengthM;
            if (!laneFirstArc.IsEmpty) laneFirstArc[index] = written;
            var laid = kept
                ? laneFirstArc[index + 1] - written - graph.ConnectorArcs(leavingOn).Length
                : Spline.HeadInto(graph.ArcsOf(lane), toM, into[written..]);
            for (var arc = 0; arc < laid; arc++) lengthM += into[written + arc].LengthM;
            written += laid;
            laneEndM[index] = lengthM;

            if (leavingOn == RoadGraph.NoConnector && index < lanes.Length - 1)
            {
                return new DrivenLine(written, index + 1, lengthM);
            }

            arrivedOn = leavingOn;
        }

        return new DrivenLine(written, lanes.Length, lengthM);
    }

    /// <summary>How far along its own lane a lane is laid into a line: whole, or to where the line stops on the last one asked for.</summary>
    public static float LaidToM(RoadGraph graph, int lane, bool lastAskedFor, float lastLaneToM) =>
        lastAskedFor ? MathF.Min(graph.LaneLengthM[lane], MathF.Max(0f, lastLaneToM)) : graph.LaneLengthM[lane];

    /// <summary>
    /// <b>Where a place on one of a line's lanes falls along the line itself</b> — a painted bar, a
    /// crossing, anything the town measured against a lane and a driver has to meet on its own line.
    /// </summary>
    /// <remarks>
    /// <b>The two measures run at the same rate and start together at the lane's own start.</b> The line over
    /// a lane <em>is</em> that lane's own arcs and nothing else (<see cref="Spline.HeadInto"/>), so a
    /// metre of one is a metre of the other. It is still clamped to the stretch being driven, because a line
    /// may stop short of a lane's end where a car waits for its manoeuvre into a bay.
    /// </remarks>
    public static float OnTheLineM(
        ReadOnlySpan<float> laneStartM, ReadOnlySpan<float> laneEndM, int slot, float alongLaneM) =>
        Math.Clamp(laneStartM[slot] + alongLaneM, laneStartM[slot], laneEndM[slot]);
}
