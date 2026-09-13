using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The fold: every join that forks nothing rubbed out, and the two lanes either side of it made one</b>
/// (TER-5h). A place a car arrives at with one way on and no choice to make is a place no driver decides
/// anything at, so it is not a junction whatever the plan called it — and a line broken there is one line
/// the town reports as two.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asked of the movements and of nothing else.</b> A lane runs on into the next when the connectors say
/// it does: one way out of the lane arriving, one way into the lane leaving, and the two are the same
/// movement. That is the whole test, and it is the same one the census counts a town's seams with. Arm
/// counts do not answer it — a node of two arms where a one-way street meets the carriageway it feeds forks
/// nothing either, and the plan's own bend-joining pass (GEN-12b) has already taken away the two-armed nodes
/// it could prove were one road.
/// </para>
/// <para>
/// <b>Both ways of a stretch fold together or neither does.</b> <see cref="LaneLines.LaneReverse"/> pairs a
/// lane with the one running back down its own ground, and a fold that took one way and left the other
/// would leave the pair pointing at lanes of different lengths over different metres — so the mirror of
/// every fold is asked for before the fold is taken. It costs nothing on a real town, where the two ways of
/// a street meet the same node and answer the same question.
/// </para>
/// <para>
/// <b>What the lane swallows is the join's own line</b>, drawn between the two ends after the cut back
/// (<see cref="LaneLines.LayConnectorLines"/>) and therefore tangent to both of them. The folded lane is the
/// arcs of the first stretch, the arcs of that join, and the arcs of the next, in order: the same ground in
/// the same shape, reported as the one line a driver actually drives. Nothing is redrawn and no corner is
/// re-solved, so a fold cannot move the tarmac a millimetre.
/// </para>
/// </remarks>
internal sealed partial class LaneLines
{
    /// <summary>
    /// How far apart two lane widths may stand and still be one lane: a millimetre, which is float noise on
    /// a figure both ends worked out from a road's own declared width.
    /// </summary>
    /// <remarks>
    /// <b>A step in width is a carriageway that really does change there</b> (TER-5d.1), and a band has one
    /// width — so the two stretches keep their join, which is then a movement like any other and paves the
    /// wedge between two bands that do not line up. No shipped town lays one: the census reports nought.
    /// </remarks>
    const float SameWidthM = 1e-3f;

    /// <summary>
    /// <b>The joins folded into a lane</b>, each as the band of tarmac it still is: the junction it crosses,
    /// how wide the ground under it is, and the line it is driven on.
    /// </summary>
    /// <remarks>
    /// It is not a movement and nothing may route over one — the lane it belongs to already runs along it.
    /// What wants it is the tarmac's own outline (<see cref="Kerbs"/>), which is laid off the roads at their
    /// declared widths: where the kerbs of the two arms do not meet, the wedge between their bands is ground
    /// no road covers and only the join does.
    /// </remarks>
    internal readonly record struct WeldTable(int[] Junction, float[] WidthM, int[] ArcOffsets, ArcSeg[] Arcs)
    {
        /// <summary>The table a town that folded nothing carries.</summary>
        public static WeldTable None => new([], [], [0], []);

        public int Count => Junction.Length;

        public ReadOnlySpan<ArcSeg> ArcsOf(int weld) =>
            Arcs.AsSpan(ArcOffsets[weld], ArcOffsets[weld + 1] - ArcOffsets[weld]);
    }

    /// <summary>
    /// The town with every forkless join folded away: the runs of lanes that hand over to one another with
    /// nothing decided in between, each written down as the single lane it is.
    /// </summary>
    static LaneLines Welded(LaneLines cut)
    {
        var (next, previous) = RunsOn(cut);

        // Where a lane is its own run there is nothing to fold, and the town is already the answer.
        var folding = false;
        for (var lane = 0; lane < cut.LaneCount && !folding; lane++) folding = next[lane] != NoLane;

        return folding ? Join(cut, next, previous) : cut;
    }

    /// <summary>
    /// Which lane each lane runs on into, and which one runs on into it — <see cref="NoLane"/> at either end
    /// of a run, which is every end a driver has a choice at.
    /// </summary>
    /// <remarks>
    /// <b>A run that closes on itself is opened at one lane</b>, and which one is as arbitrary as the ring
    /// is: a loop of street with no junction anywhere on it has no first lane, and a lane every reader can
    /// ask the length and the ends of has to start somewhere. The ring running the other way is opened at
    /// the mirror of the same join, so the two stay each other's reverse.
    /// </remarks>
    static (int[] Next, int[] Previous) RunsOn(LaneLines cut)
    {
        var arriving = new int[cut.LaneCount];
        foreach (var landing in cut.ConnectorToLane) arriving[landing]++;

        var next = new int[cut.LaneCount];
        var previous = new int[cut.LaneCount];
        Array.Fill(next, NoLane);
        Array.Fill(previous, NoLane);

        for (var lane = 0; lane < cut.LaneCount; lane++)
        {
            var onto = OneWayOut(cut, arriving, lane);
            if (onto == NoLane) continue;

            // The mirror: the two ways of one stretch are folded together or neither of them is.
            var back = cut.LaneReverse[onto];
            var backOnto = cut.LaneReverse[lane];
            if ((back == NoLane) != (backOnto == NoLane)) continue;
            if (back != NoLane && OneWayOut(cut, arriving, back) != backOnto) continue;

            next[lane] = onto;
            previous[onto] = lane;
        }

        var onARun = new bool[cut.LaneCount];
        for (var lane = 0; lane < cut.LaneCount; lane++)
        {
            if (previous[lane] == NoLane) Walk(lane);
        }

        // Whatever the first pass could not reach is on a ring — a run with no first lane, because every
        // join on it forks nothing.
        for (var lane = 0; lane < cut.LaneCount; lane++)
        {
            if (onARun[lane]) continue;

            Open(lane);
            Walk(lane);
        }

        return (next, previous);

        void Walk(int from)
        {
            for (var at = from; at != NoLane && !onARun[at]; at = next[at]) onARun[at] = true;
        }

        void Open(int lane)
        {
            var before = previous[lane];
            if (before == NoLane) return;

            previous[lane] = NoLane;
            next[before] = NoLane;

            // <b>And the same join the other way round</b>, so the ring driven the other way is opened
            // between the same two stretches and the two runs stay each other's reverse. It is walked here
            // rather than left to the loop, which would otherwise reach it as another ring and cut a second
            // join in it.
            var back = cut.LaneReverse[lane];
            if (back == NoLane || next[back] == NoLane) return;

            var mirror = next[back];
            next[back] = NoLane;
            previous[mirror] = NoLane;
            Walk(mirror);
        }
    }

    /// <summary>
    /// The one lane this one runs on into, or <see cref="NoLane"/> where a driver reaching its end has
    /// something to decide: more than one way out, a way out others also arrive by, or a carriageway that
    /// steps in width there (<see cref="SameWidthM"/>).
    /// </summary>
    static int OneWayOut(LaneLines cut, int[] arriving, int lane)
    {
        if (cut.ConnectorAt[lane + 1] - cut.ConnectorAt[lane] != 1) return NoLane;

        var onto = cut.ConnectorToLane[cut.ConnectorAt[lane]];
        if (arriving[onto] != 1) return NoLane;

        return MathF.Abs(cut.LaneWidthM[lane] - cut.LaneWidthM[onto]) <= SameWidthM ? onto : NoLane;
    }

    /// <summary>Every run of lanes written down as the one lane it is, with the joins inside it folded in.</summary>
    static LaneLines Join(LaneLines cut, int[] next, int[] previous)
    {
        // Which folded lane each of the cut town's lanes ended up part of, so a connector's far end can be
        // named before the lane it lands on has been written.
        var folded = new int[cut.LaneCount];
        var runCount = 0;
        for (var lane = 0; lane < cut.LaneCount; lane++)
        {
            if (previous[lane] != NoLane) continue;

            for (var at = lane; at != NoLane; at = next[at]) folded[at] = runCount;

            runCount++;
        }

        var laneFromRoad = new int[runCount];
        var laneToRoad = new int[runCount];
        var laneWidthM = new float[runCount];
        var laneFromJunction = new int[runCount];
        var laneToJunction = new int[runCount];
        var laneForward = new bool[runCount];
        var laneReverse = new int[runCount];
        var laneLengthM = new float[runCount];
        var cutBackAtStartM = new float[runCount];
        var cutBackAtEndM = new float[runCount];
        var laneArcOffsets = new int[runCount + 1];
        var laneArcs = new List<ArcSeg>(cut.LaneArcs.Length + cut.ConnectorArcs.Length);

        var connectorAt = new int[runCount + 1];
        var connectorToLane = new List<int>(cut.ConnectorCount);
        var connectorKind = new List<LaneTurn>(cut.ConnectorCount);
        var connectorArcOffsets = new List<int> { 0 };
        var connectorArcs = new List<ArcSeg>(cut.ConnectorArcs.Length);
        var connectorLengthM = new List<float>(cut.ConnectorCount);

        var weldJunction = new List<int>();
        var weldWidthM = new List<float>();
        var weldArcOffsets = new List<int> { 0 };
        var weldArcs = new List<ArcSeg>();

        var run = 0;
        for (var lane = 0; lane < cut.LaneCount; lane++)
        {
            if (previous[lane] != NoLane) continue;

            laneFromRoad[run] = cut.LaneFromRoad[lane];
            laneWidthM[run] = cut.LaneWidthM[lane];
            laneFromJunction[run] = cut.LaneFromJunction[lane];
            laneForward[run] = cut.LaneForward[lane];
            cutBackAtStartM[run] = cut.LaneCutBackAtStartM[lane];

            // The run's reverse is the run its own first lane's reverse belongs to, which that lane is the
            // last of: the mirror was asked for before any of these joins was taken.
            laneReverse[run] = cut.LaneReverse[lane] == NoLane ? NoLane : folded[cut.LaneReverse[lane]];

            var last = lane;
            while (true)
            {
                laneArcs.AddRange(cut.ArcsOf(last));
                laneLengthM[run] += cut.LaneLengthM[last];
                if (next[last] == NoLane) break;

                var join = cut.ConnectorAt[last];
                laneArcs.AddRange(cut.ArcsOfConnector(join));
                laneLengthM[run] += cut.ConnectorLengthM[join];

                weldJunction.Add(cut.JunctionOfConnector(join));
                weldWidthM.Add(cut.ConnectorWidthM(join));
                weldArcs.AddRange(cut.ArcsOfConnector(join));
                weldArcOffsets.Add(weldArcs.Count);

                last = next[last];
            }

            laneToRoad[run] = cut.LaneToRoad[last];
            laneToJunction[run] = cut.LaneToJunction[last];
            cutBackAtEndM[run] = cut.LaneCutBackAtEndM[last];
            laneArcOffsets[run + 1] = laneArcs.Count;

            // What is left of the run's own choices is the last lane's: every join before it is inside the
            // line now.
            for (var join = cut.ConnectorAt[last]; join < cut.ConnectorAt[last + 1]; join++)
            {
                connectorToLane.Add(folded[cut.ConnectorToLane[join]]);
                connectorKind.Add(cut.ConnectorKind[join]);
                connectorArcs.AddRange(cut.ArcsOfConnector(join));
                connectorArcOffsets.Add(connectorArcs.Count);
                connectorLengthM.Add(cut.ConnectorLengthM[join]);
            }

            connectorAt[run + 1] = connectorToLane.Count;
            run++;
        }

        return new LaneLines(
            cut.JunctionCount, laneFromRoad, laneToRoad, laneWidthM, laneFromJunction, laneToJunction,
            laneForward, laneReverse, laneLengthM, cutBackAtStartM, cutBackAtEndM, laneArcOffsets,
            [.. laneArcs], connectorAt, [.. connectorToLane], [.. connectorKind], [.. connectorArcOffsets],
            [.. connectorArcs], [.. connectorLengthM],
            new WeldTable([.. weldJunction], [.. weldWidthM], [.. weldArcOffsets], [.. weldArcs]));
    }
}
