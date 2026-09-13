using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Parking;

/// <summary>
/// <b>What the ways at a bay are driven over, and what is driven over them</b> (TER-5c) — worked out once
/// from the lines themselves and folded into the town's own table, so that a car working into a bay and the
/// traffic on the lane it crosses are held apart by the mechanism that holds a junction apart.
/// </summary>
/// <remarks>
/// <para>
/// <b>A way at a bay crosses lanes, where a junction's join crosses joins.</b> That is the whole of the
/// difference between the two, and it is why the table is indexed by way rather than by movement
/// (<see cref="WayCrossings"/>): the lanes hand over clear of a box, so a junction never needed to name
/// one, and a line that leaves a lane part-way along it can name nothing else.
/// </para>
/// <para>
/// <b>The lanes it can touch are the one it is nearest and the one running back the other way</b>, and no
/// wider a search than that — the same bound the town's furniture is projected under
/// (<c>StandingGround</c>), and for the same reason: a carriageway is two lanes, a lot keeps its distance
/// from a junction (GEN-4d), and a bay whose way reached a third would be a car park laid across a street.
/// </para>
/// <para>
/// <b>And the ways at its neighbours.</b> Two bays of one lot are a car's width apart, so the line into one
/// and the line into the next share ground: read only against the lanes, two cars would work into
/// neighbouring bays at the same moment and meet between them. Which pairs are measured is the town's
/// geometry grid's to say (<see cref="ChainIndex.Crossing"/>) and what each pair comes to is still the two
/// boxes and then the two lines — <b>a few hundred ways were cheaper to weigh pairwise than to index, and a
/// city lays eight thousand of them</b>, which is thirty-seven million pairs to prove that two lines a
/// district apart do not touch.
/// </para>
/// <para>
/// <b>Only the stretch of a lane the way can reach is measured.</b> A lane is two hundred metres and a way
/// at a bay is a dozen; sampled whole against each other under one budget, a section would be opened out by
/// the eight metres between two samples of the lane — a grant cut half a street early
/// (<see cref="LineOverlap"/>).
/// </para>
/// </remarks>
internal static class BayCrossings
{
    /// <summary>
    /// <b>The town's whole table of what is driven over what</b>: the road's own, grown by what the bays
    /// lay off it. The one place the two are put together, so that nothing reads half of it.
    /// </summary>
    public static WayCrossings Over(BayWays ways, RoadGraph roads, SimConfig config) =>
        roads.Crossings.Grown(ways.TotalWayCount, Lay(ways, roads, config));

    /// <summary>
    /// The sections the bays add to it, each filed under the way whose row it belongs in. <b>Both ways
    /// round for every crossing</b>: what one line takes of another is what the other finds when it looks
    /// the same crossing up.
    /// </summary>
    static List<(int Way, CrossedSection Section)> Lay(BayWays ways, RoadGraph roads, SimConfig config)
    {
        var clearanceM = config.JunctionCrossingClearanceM;
        var found = new List<(int Way, CrossedSection Section)>();
        if (ways.WayCount == 0) return found;

        var pointM = new Vector2[ways.WayCount][];
        var stepM = new float[ways.WayCount];
        var boxM = new (Vector2 LeastM, Vector2 MostM)[ways.WayCount];
        var buffer = new Vector2[LineOverlap.MostSamples];
        var laneLine = new Vector2[LineOverlap.MostSamples];

        for (var at = 0; at < ways.WayCount; at++)
        {
            var way = ways.FirstWay + at;

            // <b>The driven part and never the whole way</b> (TER-5c). What a way in carries past the pose is
            // the space itself (<see cref="BayWays.LengthM"/>), and nothing is driven over ground nothing
            // drives: measured whole, every bay would read as crossed by its own neighbours' spaces and a car
            // would give way to the empty tarmac in front of a parked one.
            var lengthM = ways.DrivenLengthM(way);

            // <b>Sampled as finely as the budget allows, and not at the clearance.</b> A section's ends are
            // read to the step it was found at (<see cref="LineOverlap.Covered"/>); at the clearance that is
            // two metres of slop on a way whose last four metres are the bay a car is parked in, and where
            // the street's ground ends is what tells a parked body from one cutting the lane beside it
            // (GEN-4i). A way at a bay is a dozen metres, so the finest step the sample count allows is a
            // hand's breadth and costs the same walk.
            var count = LineOverlap.Sample(
                ways.ArcsOf(way), 0f, lengthM, lengthM, lengthM / (LineOverlap.MostSamples - 1), buffer,
                out stepM[at]);

            pointM[at] = buffer[..count].ToArray();
            boxM[at] = BoxOf(pointM[at], clearanceM);
        }

        // <b>The ways over the same grid the lanes are indexed on</b> (<see cref="ChainIndex"/>), which is
        // what makes "the ways that could touch this one" a question about the cells round it rather than
        // about every way in the town. The index is of the whole way and the answer is a superset of what
        // the boxes below admit, since a box is the driven part of one grown by the clearance.
        var index = new ChainIndex.Builder();
        for (var at = 0; at < ways.WayCount; at++)
        {
            var way = ways.FirstWay + at;
            index.Add(at, ways.ArcsOf(way), ways.LengthM(way));
        }

        var near = index.Seal(config.NearestChainCellM);
        var candidate = new int[ways.WayCount];

        for (var at = 0; at < ways.WayCount; at++)
        {
            var way = ways.FirstWay + at;
            var mine = new SampledWay(pointM[at], 0f, stepM[at], ways.DrivenLengthM(way));

            AgainstTheLanes(way, at, mine);

            // <b>In the ways' own order and not the lattice's.</b> The grid hands its cells back row by
            // row; a crossing is filed under both ways it is a crossing of, so the order the rows come out
            // in is the order the pairs were weighed in.
            var sharing = near.Crossing(ways.ArcsOf(way), clearanceM * 2f, candidate);
            var offered = candidate.AsSpan(0, Math.Min(sharing, candidate.Length));
            offered.Sort();
            foreach (var other in offered)
            {
                if (other <= at) continue;
                if (!Overlap(boxM[at], boxM[other])) continue;

                var theirWay = ways.FirstWay + other;
                var theirs = new SampledWay(pointM[other], 0f, stepM[other], ways.DrivenLengthM(theirWay));
                Keep(way, mine, theirWay, theirs);
            }
        }

        return found;

        // The carriageway under one way at a bay: whichever lanes its own samples stand nearest, and the
        // lanes running back the other way from those.
        void AgainstTheLanes(int way, int at, in SampledWay mine)
        {
            var first = -1;
            var second = -1;
            foreach (var sample in pointM[at])
            {
                var lane = roads.NearestLane(sample, out _);
                if (lane < 0) continue;

                Note(lane);
                Note(roads.LaneReverse[lane]);
            }

            if (first >= 0) AgainstOneLane(way, at, mine, first);
            if (second >= 0) AgainstOneLane(way, at, mine, second);

            void Note(int lane)
            {
                if (lane < 0 || lane == first || lane == second) return;

                if (first < 0) first = lane;
                else if (second < 0) second = lane;
            }
        }

        void AgainstOneLane(int way, int at, in SampledWay mine, int lane)
        {
            var arcs = roads.ArcsOf(lane);
            var laneLengthM = roads.LaneLengthM[lane];

            var leastM = float.PositiveInfinity;
            var mostM = float.NegativeInfinity;
            foreach (var sample in pointM[at])
            {
                var alongM = Spline.ProjectM(arcs, sample, laneLengthM * 0.5f, laneLengthM);
                leastM = MathF.Min(leastM, alongM);
                mostM = MathF.Max(mostM, alongM);
            }

            // Opened out by a clearance either way, so that the window holds the whole of any crossing whose
            // middle its own samples found — a projection is the nearest point and not the first one.
            var fromM = leastM - (clearanceM * 2f);
            var toM = mostM + (clearanceM * 2f);
            var count = LineOverlap.Sample(arcs, fromM, toM, laneLengthM, clearanceM, laneLine, out var step);
            if (count < 2) return;

            Keep(way, mine, lane, new SampledWay(laneLine.AsSpan(0, count), MathF.Max(0f, fromM), step, laneLengthM));
        }

        void Keep(int mineWay, in SampledWay mine, int theirWay, in SampledWay theirs)
        {
            if (!LineOverlap.Measure(mine, theirs, clearanceM, out var onMine, out var onTheirs)) return;

            found.Add((mineWay, new CrossedSection(theirWay, onTheirs.FromM, onTheirs.ToM, onMine.FromM, onMine.ToM)));
            found.Add((theirWay, new CrossedSection(mineWay, onMine.FromM, onMine.ToM, onTheirs.FromM, onTheirs.ToM)));
        }
    }

    /// <summary>The box a sampled line stands in, opened out by the clearance it will be compared at.</summary>
    static (Vector2 LeastM, Vector2 MostM) BoxOf(ReadOnlySpan<Vector2> pointM, float clearanceM)
    {
        var leastM = new Vector2(float.PositiveInfinity);
        var mostM = new Vector2(float.NegativeInfinity);
        foreach (var point in pointM)
        {
            leastM = Vector2.Min(leastM, point);
            mostM = Vector2.Max(mostM, point);
        }

        var outM = new Vector2(clearanceM);
        return (leastM - outM, mostM + outM);
    }

    static bool Overlap(in (Vector2 LeastM, Vector2 MostM) first, in (Vector2 LeastM, Vector2 MostM) second) =>
        first.LeastM.X <= second.MostM.X && second.LeastM.X <= first.MostM.X
        && first.LeastM.Y <= second.MostM.Y && second.LeastM.Y <= first.MostM.Y;
}
