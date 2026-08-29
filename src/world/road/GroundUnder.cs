using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

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
internal readonly record struct WayUnder(int Way, float AlongM, float BandM, Vector2 AlongUnit);

/// <summary>
/// <b>Which of the town's ways one place stands on</b> — the lane it is in, the lane running back the other
/// way where the body reaches into it, and every join of a junction it is lying under.
/// </summary>
/// <remarks>
/// <para>
/// <b>One walk, read by whoever writes a body into the book and by whoever asks the book about ground</b>
/// (SIM-7). A body lying askew is laid onto these ways; a manoeuvre's template asks these same ways whose
/// the ground under it is. Asked of a narrower set than it is written to, a driver on geometry of its own
/// could not see what a body standing in the same place had written — which is exactly a junction, where
/// every car crossing writes its road on a <em>join</em> and no lane at all.
/// </para>
/// <para>
/// <b>The nearest lane is where the walk starts and never where it stops.</b> The nearest lane to a point is
/// an answer for every point in the town, so the band is what says whether the place is really on it; and a
/// carriageway is two lanes, so a body over the centreline is in both of their bands and is a fact to the
/// traffic in each.
/// </para>
/// <para>
/// <b>The joins are asked at both ends of that lane, and the setbacks say which end can be it</b> (TER-5d):
/// past one the ground stops being the lane's, and a lane shorter than the junctions either side of it
/// answers to both.
/// </para>
/// </remarks>
internal static class GroundUnder
{
    /// <summary>How much room a caller has to give the walk: the two lanes of a carriageway, and every movement the busiest junction admits at each end of one.</summary>
    public static int MostWaysUnderAPlace(RoadGraph roads) => 2 + (roads.MostTurnsAtANode * 2);

    /// <summary>
    /// The ways this place stands on, written into <paramref name="into"/> and returned as the count of
    /// them. <b>A place over no lane is on no way</b>: ground the network never had is ground nobody can
    /// have reserved.
    /// </summary>
    /// <param name="flankM">Half the asking body's width, which is how far either side of the way's own line it reaches.</param>
    /// <param name="halfLengthM">And half its length, which is how far past a way's own ends it may stand and still be on it.</param>
    /// <param name="book">
    /// Which way each lane and each join is, asked of the book so that the numbering is stated in one place
    /// and read everywhere (<see cref="LaneOccupancy.WayOfLane"/>).
    /// </param>
    public static int At(
        RoadGraph roads, LaneOccupancy book, Vector2 atM, float flankM, float halfLengthM, Span<WayUnder> into)
    {
        var lane = roads.NearestLane(atM, out var alongM);
        if (lane < 0) return 0;

        var written = 0;
        WriteTheLane(roads, book, lane, alongM, atM, flankM, halfLengthM, into, ref written);

        var back = roads.LaneReverse[lane];
        if (back >= 0)
        {
            var lengthM = roads.LaneLengthM[back];
            WriteTheLane(
                roads, book, back, Spline.ProjectM(roads.ArcsOf(back), atM, lengthM * 0.5f, lengthM), atM,
                flankM, halfLengthM, into, ref written);
        }

        // Both ends of the nearest lane, because the setbacks are what say which of them the place can be
        // past — and never the reverse lane's, whose ends are the same two junctions read the other way round.
        if (alongM <= roads.JoinedAtM(lane))
        {
            WriteTheJoins(roads, book, roads.LaneFromNode[lane], atM, flankM, halfLengthM, into, ref written);
        }

        if (alongM >= roads.LaneLengthM[lane] - roads.LeftAtM(lane))
        {
            WriteTheJoins(roads, book, roads.LaneToNode[lane], atM, flankM, halfLengthM, into, ref written);
        }

        return written;
    }

    static void WriteTheLane(
        RoadGraph roads, LaneOccupancy book, int lane, float alongM, Vector2 atM, float flankM,
        float halfLengthM, Span<WayUnder> into, ref int written)
    {
        if (written >= into.Length) return;
        if (!RoadGraph.WithinTheBand(
                roads.ArcsOf(lane), alongM, atM, roads.LaneWidthM[lane], flankM, halfLengthM, out var alongUnit))
        {
            return;
        }

        into[written++] = new WayUnder(book.WayOfLane(lane), alongM, roads.LaneWidthM[lane], alongUnit);
    }

    static void WriteTheJoins(
        RoadGraph roads, LaneOccupancy book, int node, Vector2 atM, float flankM, float halfLengthM,
        Span<WayUnder> into, ref int written)
    {
        foreach (var arriving in roads.LanesIn(node))
        {
            for (var turn = 0; turn < roads.TurnsFrom(arriving).Length && written < into.Length; turn++)
            {
                var slot = roads.TurnSlotAt(arriving, turn);
                var arcs = roads.JoinArcs(slot);
                if (arcs.Length == 0) continue;

                var lengthM = roads.JoinLengthM(slot);
                var onJoinM = Spline.ProjectM(arcs, atM, lengthM * 0.5f, lengthM);
                var bandM = roads.LaneWidthM[arriving];
                if (!RoadGraph.WithinTheBand(arcs, onJoinM, atM, bandM, flankM, halfLengthM, out var alongUnit))
                {
                    continue;
                }

                into[written++] = new WayUnder(book.WayOfTurn(slot), onJoinM, bandM, alongUnit);
            }
        }
    }
}
