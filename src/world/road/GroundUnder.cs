using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>What one way has of one body</b> (<see cref="RoadGraph.WithinTheBand"/>): the run of its line the body
/// covers, how far aside of that line it stands, how far past the way's own end, and which way the line runs
/// there. <b>Read once and handed on</b> — every figure here comes out of the same reduction of the same
/// angle, and a caller working any of them out again is working it out a second time.
/// </summary>
internal readonly record struct BandReach(
    Vector2 AlongUnit, float PastTheEndM, float AcrossFromM, float AcrossToM, float BackM, float AheadM);

/// <summary>
/// One of the town's ways a place stands on: which way, how far along that way's own metres the place
/// projects, and how wide the band it was found inside is.
/// </summary>
/// <param name="BandM">
/// The width the way was measured at, which for a junction's join is the arriving lane's — a join is drawn
/// between two lanes and has no width of its own.
/// </param>
/// <param name="AlongUnit">
/// Which way the way's own line runs where the place falls on it, handed back by the band test that had to
/// work it out anyway (<see cref="RoadGraph.WithinTheBand"/>).
/// </param>
/// <param name="PastTheEndM">
/// <b>How far beyond this way's own two ends the body stands</b>, and nought where it is square to the line.
/// A projection is clamped to the way it is taken on (<see cref="Spline.ProjectM"/>), so a body in a junction
/// answers on the arms' lanes at their own ends — ground that is the box's and not theirs (TER-5d), and the
/// one thing that tells a body <em>on</em> a lane from a body past it.
/// </param>
/// <param name="AcrossFromM">
/// <b>Where across this way's own line the body begins</b>, signed to the way's right
/// (<see cref="RoadGraph.WithinTheBand"/>) — what a stretch of this way carries about where across it its
/// holder is, and the whole of what lets a reader decide whether it can get past.
/// </param>
/// <param name="AcrossToM">And where it ends, on the same terms.</param>
/// <param name="BackM">
/// <b>Where the run this body covers begins</b>, from <paramref name="AlongM"/> and in this way's own
/// direction — negative behind it. It is the box <em>clipped to the band</em>
/// (<see cref="BodyFootprint.CoversOn"/>) and not the box's shadow, so a body across a way's corner covers
/// the corner and not its own length of the way.
/// </param>
/// <param name="AheadM">And where that run ends, on the same terms.</param>
internal readonly record struct WayUnder(
    int Way, float AlongM, float BandM, Vector2 AlongUnit, float PastTheEndM, float AcrossFromM,
    float AcrossToM, float BackM, float AheadM);

/// <summary>
/// <b>Which of the town's ways one place stands on</b> — the lane it is in, the lane running back the other
/// way where the body reaches into it, and every way of a node it is lying over: the joins of a junction and
/// the lanes that end there.
/// </summary>
/// <remarks>
/// <para>
/// <b>One walk, read by whoever lays a claim for a body and by whoever asks the claims about ground</b>
/// (SIM-7). A body lying askew is laid onto these ways; a manoeuvre's template asks these same ways whose
/// the ground under it is. Asked of a narrower set than it is written to, a driver on geometry of its own
/// could not see what a body standing in the same place had written — which is exactly a junction, where
/// every car crossing writes its road on a <em>join</em> and no lane at all.
/// </para>
/// <para>
/// <b>And one walk over each of the town's networks</b> (<see cref="IWayNetwork"/>), because a body holds the
/// ground it stands on whether that ground is carriageway, pavement or parking bay (TER-4c.2): a car shoved
/// onto a footway is read here exactly as a car shoved into the oncoming lane is, and a car standing in a bay
/// exactly as either. The network is a generic argument and a struct, so the walk over each is its own
/// compiled code and none of them pays for the others.
/// </para>
/// <para>
/// <b>The nearest lane is where the walk starts and never where it stops.</b> The nearest lane to a point is
/// an answer for every point in the town, so the band is what says whether the place is really on it; and a
/// carriageway is two lanes, so a body over the centreline is in both of their bands and is a fact to the
/// traffic in each.
/// </para>
/// <para>
/// <b>A node is asked at either end of that lane the body reaches</b> (TER-5d): past a lane's own end the
/// ground stops being the lane's, and a lane shorter than the bodies on it answers to both its nodes.
/// <b>How far the body reaches and not where its middle projects</b> — a car short of a lane's last metre
/// with its nose over it is standing in the box. It is the other networks that hold a line running on past
/// where they are travelled, and the two figures are theirs to answer.
/// </para>
/// <para>
/// <b>And a node is its lanes as much as its joins</b> (TER-4c.2). A junction's ground is carried by the
/// movements over it, but a node cut into a road carries a movement of no length
/// (<see cref="RoadGraph.IsAPlace"/>) and nothing else, so a body lying over one is on the ends of two lanes
/// and on no join at all: read as the nearest lane's alone, the ground under the half of it past the node
/// belonged to nobody.
/// </para>
/// </remarks>
internal static class GroundUnder
{
    /// <summary>How much room a caller has to give the walk: the two lanes of a stretch, and every way the busiest node has — its movements and the lanes at its ends — at each end of one.</summary>
    public static int MostWaysUnderAPlace(int mostTurnsAtANode, int mostLanesAtANode) =>
        2 + ((mostTurnsAtANode + mostLanesAtANode) * 2);

    /// <summary>
    /// The ways this place stands on, written into <paramref name="into"/> and returned as the count of
    /// them. <b>A place over no lane is on no way</b>: ground the network never had is ground nobody can
    /// have claimed.
    /// </summary>
    /// <param name="body">
    /// The box the asking body stands in (<see cref="BodyFootprint"/>), which is what decides both how far
    /// past a band it reaches and how much of each way it covers.
    /// </param>
    /// <param name="crossesByM">
    /// How far past a way's own edge the body has to reach before it is on that way
    /// (<see cref="SimConfig.CrossesOntoAWayM"/>) — nought for a caller asking the bare question of whether
    /// it overlaps at all, which is the wider and so the safer way round for a walk over ground.
    /// </param>
    public static int At<TWays>(
        in TWays ways,Vector2 atM, in BodyFootprint body, float crossesByM,
        Span<WayUnder> into)
        where TWays : struct, IWayNetwork
    {
        var lane = ways.NearestLane(atM, out var alongM);
        if (lane < 0) return 0;

        var written = 0;
        WriteTheLane(ways,lane, alongM, atM, body, crossesByM, into, ref written);

        var back = ways.Reverse(lane);
        if (back >= 0) WriteTheLaneAt(ways,back, atM, body, crossesByM, into, ref written);

        // Both ends of the nearest lane — and never the reverse lane's, whose ends are the same two
        // junctions read the other way round. <b>Asked of where the body reaches and never of where its
        // middle projects</b>: a carriageway lane is left for the box at its own last metre (TER-5d), so a
        // body short of that metre with its nose over it was a body the node was never asked about at all —
        // the ground under that nose held by nobody, and the block on the lane stopping dead at the mouth
        // of the junction.
        var lengthM = ways.LaneLengthM(lane);
        var arcs = ways.ArcsOf(lane);
        body.ReachOn(Spline.SampleAt(arcs, 0f).Direction, out var reachesBackM, out _);
        if (alongM - reachesBackM <= ways.JoinedAtM(lane))
        {
            WriteTheNode(ways,ways.FromNode(lane), atM, body, crossesByM, into, ref written);
        }

        body.ReachOn(Spline.SampleAt(arcs, lengthM).Direction, out var reachesOnM, out _);
        if (alongM + reachesOnM >= lengthM - ways.LeftAtM(lane))
        {
            WriteTheNode(ways,ways.ToNode(lane), atM, body, crossesByM, into, ref written);
        }

        return written;
    }

    /// <summary>
    /// Every way one node has — the movements through it and <b>the lanes that end at it</b> — for a body
    /// standing near enough to be on any of them.
    /// </summary>
    /// <remarks>
    /// <b>The lanes, because a join is not always there to carry the ground across</b>. A node cut into a
    /// road rather than planned as a junction has a movement of no length over it
    /// (<see cref="RoadGraph.IsAPlace"/>), so a body lying over that node is on the ends of two lanes and on
    /// no join at all — and read as the nearest lane's alone, half of it stood on ground the claims said was
    /// empty and the block drawn for it stopped at the node.
    /// <para>
    /// <b>The lanes are what a place can meet twice and the joins are not</b> (TER-5c.2), which is why the
    /// dedupe is theirs alone (<see cref="WriteTheLane"/>). A lane is a lane of both the nodes at its ends,
    /// so a body over one node meets the nearest lane and its reverse again there; a movement belongs to the
    /// one node it crosses, and the two nodes this walk asks are the two ends of one lane.
    /// </para>
    /// </remarks>
    static void WriteTheNode<TWays>(
        in TWays ways,int node, Vector2 atM, in BodyFootprint body, float crossesByM,
        Span<WayUnder> into, ref int written)
        where TWays : struct, IWayNetwork
    {
        // <b>A lane may end at nothing</b>, and then there is nothing to write: a parking bay's way runs out
        // onto the carriageway, which is a node of a different network. What the body standing there holds of
        // the street is that network's own walk to say, made over that network's own ways.
        if (node < 0) return;

        WriteTheConnectors(ways,node, atM, body, crossesByM, into, ref written);
        WriteTheLanesAt(ways,ways.LanesIn(node), atM, body, crossesByM, into, ref written);
        WriteTheLanesAt(ways,ways.LanesOut(node), atM, body, crossesByM, into, ref written);
    }

    static void WriteTheLanesAt<TWays>(
        in TWays ways,ReadOnlySpan<int> lanes, Vector2 atM, in BodyFootprint body,
        float crossesByM, Span<WayUnder> into, ref int written)
        where TWays : struct, IWayNetwork
    {
        foreach (var lane in lanes)
        {
            WriteTheLaneAt(ways,lane, atM, body, crossesByM, into, ref written);
        }
    }

    /// <summary>One lane the place was not found on, at wherever on it the place falls.</summary>
    static void WriteTheLaneAt<TWays>(
        in TWays ways,int lane, Vector2 atM, in BodyFootprint body, float crossesByM,
        Span<WayUnder> into, ref int written)
        where TWays : struct, IWayNetwork
    {
        var lengthM = ways.LaneLengthM(lane);
        WriteTheLane(
            ways, lane, Spline.ProjectM(ways.ArcsOf(lane), atM, lengthM * 0.5f, lengthM), atM, body,
            crossesByM, into, ref written);
    }

    static void WriteTheLane<TWays>(
        in TWays ways,int lane, float alongM, Vector2 atM, in BodyFootprint body,
        float crossesByM, Span<WayUnder> into, ref int written)
        where TWays : struct, IWayNetwork
    {
        if (written >= into.Length) return;

        // The nearest lane and the lane running back against it are both lanes of their own two nodes, so a
        // body over a node meets them again there — and one way twice is one body laid twice (TER-5c.2).
        var way = ways.WayOfLane(lane);
        for (var already = 0; already < written; already++)
        {
            if (into[already].Way == way) return;
        }

        var bandM = ways.LaneWidthM(lane);
        if (!RoadGraph.WithinTheBand(
                ways.ArcsOf(lane), alongM, atM, bandM, body, crossesByM, out var reach))
        {
            return;
        }

        into[written++] = new WayUnder(
            way, alongM, bandM, reach.AlongUnit, reach.PastTheEndM, reach.AcrossFromM, reach.AcrossToM,
            reach.BackM, reach.AheadM);
    }

    static void WriteTheConnectors<TWays>(
        in TWays ways,int node, Vector2 atM, in BodyFootprint body, float crossesByM,
        Span<WayUnder> into, ref int written)
        where TWays : struct, IWayNetwork
    {
        foreach (var arriving in ways.LanesIn(node))
        {
            foreach (var connector in ways.ConnectorsFrom(arriving))
            {
                if (written >= into.Length) return;

                var arcs = ways.ConnectorArcs(connector);
                if (arcs.Length == 0) continue;

                var lengthM = ways.ConnectorLengthM(connector);
                var alongM = Spline.ProjectM(arcs, atM, lengthM * 0.5f, lengthM);
                var bandM = ways.LaneWidthM(arriving);
                if (!RoadGraph.WithinTheBand(arcs, alongM, atM, bandM, body, crossesByM, out var reach)) continue;

                into[written++] = new WayUnder(
                    ways.WayOfConnector(connector), alongM, bandM, reach.AlongUnit, reach.PastTheEndM,
                    reach.AcrossFromM, reach.AcrossToM, reach.BackM, reach.AheadM);
            }
        }
    }
}
