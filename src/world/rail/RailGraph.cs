using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Rail;

/// <summary>Where one wheel stands on the railway (TRN-9): the segment, and how far along it from its start; −1 for none yet.</summary>
internal record struct RailCursor(int Segment, float AlongM)
{
    public static RailCursor Nowhere => new(-1, 0f);
}

/// <summary>
/// <b>The railway as a network</b> (RAIL-2, RAIL-9, TRN-9): the map's tracks cut into segments wherever another track
/// ends on them, the nodes the segments meet at, the switches standing at every node where a track forks, and the lanes
/// a train runs — one a segment each way its track is run. Laid once with the town, as the road graph is.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing about a switch is stored</b>: a node where three or more segment ends meet is one, and which of them is
/// its common end is read off the geometry — the one end leading away from all the others. Its positions are its
/// other ends, its branches, in the order they turn from the common end's line.
/// </para>
/// <para>
/// <b>A segment end</b> is <c>segment * 2</c> for its start and <c>segment * 2 + 1</c> for its end, and is what a wheel
/// runs out of a segment through (<see cref="NextEnd"/>): at a plain joint, onto the one other end; through a switch
/// from a branch, onto its common end; and from the common end, onto the branch the switch is set to.
/// </para>
/// <para>
/// <b>Tracks meet only exactly</b>: an end shares a node with another end standing within <see cref="MeetM"/> of it, or
/// cuts a track whose line passes that near. A traced map whose tracks stop centimetres short of each other lays no
/// switch there (<c>docs/tech-debt.md</c>).
/// </para>
/// </remarks>
internal sealed class RailGraph
{
    /// <summary>How near two track ends, or an end and a line, stand to be one node: a float's slack, not a tolerance.</summary>
    public const float MeetM = 0.01f;

    /// <summary>A segment end through which nothing leads on: a loose end.</summary>
    public const int Nowhere = -1;

    /// <summary>The longest a segment is laid: half what the atlas files of a way (<see cref="RibbonAtlas.LongestWayM"/>).</summary>
    public const float LongestSegmentM = RibbonAtlas.LongestWayM * 0.5f;

    /// <summary>Each segment's line, its track and the node at each of its two ends.</summary>
    public TrackLine[] Segments { get; }

    public int[] TrackOfSegment { get; }

    /// <summary>How far along its track each segment begins, so a place on it is a place on the track (RAIL-1).</summary>
    public float[] SegmentFromM { get; private init; } = [];

    public int[] NodeOfEnd { get; }

    /// <summary>Each node's place, and its segment ends (<see cref="EndOffsets"/>), the common end first at a switch.</summary>
    public Vector2[] NodeM { get; }

    public int[] EndOffsets { get; }

    public int[] Ends { get; }

    /// <summary>Each node's switch, or −1 where it is none.</summary>
    public int[] SwitchOfNode { get; }

    /// <summary>Each switch's node; its branches are the node's ends after the first, position by position.</summary>
    public int[] NodeOfSwitch { get; }

    /// <summary>Whether a node is a loose end off the map: where a train comes on and goes off (TRN-7).</summary>
    public bool[] IsPortal { get; }

    /// <summary>Each lane's segment, and whether it runs the segment from its start to its end.</summary>
    public int[] SegmentOfLane { get; }

    public bool[] LaneForward { get; }

    /// <summary>Each segment's lane run each way, or −1 where its track is not run that way.</summary>
    public int[] ForwardLaneOf { get; }

    public int[] BackwardLaneOf { get; }

    public int SegmentCount => Segments.Length;

    public int LaneCount => SegmentOfLane.Length;

    public int SwitchCount => NodeOfSwitch.Length;

    public int NodeCount => NodeM.Length;

    RailGraph(
        TrackLine[] segments, int[] trackOfSegment, int[] nodeOfEnd, Vector2[] nodeM, int[] endOffsets, int[] ends, int[] switchOfNode,
        int[] nodeOfSwitch, bool[] isPortal, int[] segmentOfLane, bool[] laneForward, int[] forwardLaneOf, int[] backwardLaneOf)
    {
        (Segments, TrackOfSegment, NodeOfEnd, NodeM, EndOffsets, Ends) = (segments, trackOfSegment, nodeOfEnd, nodeM, endOffsets, ends);
        (SwitchOfNode, NodeOfSwitch, IsPortal) = (switchOfNode, nodeOfSwitch, isPortal);
        (SegmentOfLane, LaneForward, ForwardLaneOf, BackwardLaneOf) = (segmentOfLane, laneForward, forwardLaneOf, backwardLaneOf);
    }

    public static RailGraph None { get; } = new([], [], [], [], [0], [], [], [], [], [], [], [], []);

    /// <summary>The lane a train runs a lane's segment the other way on, or −1.</summary>
    public int Opposite(int lane) => LaneForward[lane] ? BackwardLaneOf[SegmentOfLane[lane]] : ForwardLaneOf[SegmentOfLane[lane]];

    /// <summary>The segment end a lane comes into its node by, and the one it leaves its first node by.</summary>
    public int ArrivesBy(int lane) => (SegmentOfLane[lane] * 2) + (LaneForward[lane] ? 1 : 0);

    public int LeavesBy(int lane) => (SegmentOfLane[lane] * 2) + (LaneForward[lane] ? 0 : 1);

    public int NodeAhead(int lane) => NodeOfEnd[ArrivesBy(lane)];

    public int NodeBehind(int lane) => NodeOfEnd[LeavesBy(lane)];

    public float LengthM(int lane) => Segments[SegmentOfLane[lane]].LengthM;

    /// <summary>The level a segment stands at: at grade, or on a bridge (RAIL-8) — one the whole way, since a segment is cut where it changes.</summary>
    public byte LevelOf(int segment) => Segments[segment].LevelAt(0f);

    /// <summary>The collision channels a train on a segment is on (PHY-1a): the ground's, or the bridge's own (RAIL-8).</summary>
    public byte ChannelsOf(int segment) =>
        LevelOf(segment) == TownMap.RailArrays.AtGrade ? CityPlan.RoadArrays.GroundChannel : CityPlan.RailArrays.BridgeChannel;

    /// <summary>Every lane as a way of the town (RAIL-9): its line, its channels, and a ribbon <paramref name="widthM"/> wide.</summary>
    public RailWayLines WayLines(float widthM)
    {
        var arcs = new ArcSeg[LaneCount][];
        var channels = new byte[LaneCount];
        for (var lane = 0; lane < LaneCount; lane++)
        {
            arcs[lane] = ArcsOf(lane).ToArray();
            channels[lane] = ChannelsOf(SegmentOfLane[lane]);
        }

        return new RailWayLines(arcs, channels, widthM);
    }

    ArcSeg[][]? _arcs;

    /// <summary>
    /// <b>A lane's line as the town's ways are drawn</b> (<see cref="TownWays"/>): its segment's chords, straight, in
    /// the order the lane runs them.
    /// </summary>
    public ReadOnlySpan<ArcSeg> ArcsOf(int lane)
    {
        _arcs ??= new ArcSeg[LaneCount][];
        if (_arcs[lane] is { } laid) return laid;

        var points = Segments[SegmentOfLane[lane]].PointM;
        var arcs = new List<ArcSeg>(points.Length);
        for (var chord = 0; chord + 1 < points.Length; chord++)
        {
            var (fromM, toM) = LaneForward[lane] ? (points[chord], points[chord + 1]) : (points[^(chord + 1)], points[^(chord + 2)]);
            var runM = toM - fromM;
            var lengthM = runM.Length();
            if (lengthM > 0f) arcs.Add(new ArcSeg(fromM, MathF.Atan2(runM.Y, runM.X), lengthM, 0f));
        }

        return _arcs[lane] = [.. arcs];
    }

    /// <summary>
    /// <b>The segment end a wheel runs out of <paramref name="end"/> onto</b>, with the switches set as
    /// <paramref name="positions"/> holds them (TRN-9): the other end at a joint, the common end from a branch, the branch
    /// set from the common end — or <see cref="Nowhere"/> at a loose end.
    /// </summary>
    public int NextEnd(int end, ReadOnlySpan<byte> positions)
    {
        var node = NodeOfEnd[end];
        var (first, count) = (EndOffsets[node], EndOffsets[node + 1] - EndOffsets[node]);
        if (count < 2) return Nowhere;
        if (count == 2) return Ends[first] == end ? Ends[first + 1] : Ends[first];

        if (Ends[first] != end) return Ends[first];

        return Ends[first + 1 + Math.Min(positions[SwitchOfNode[node]], count - 2)];
    }

    /// <summary>
    /// <b>The place on the track nearest a wheel's pivot, and which way the track runs there</b> (TRN-8, TRN-9): the
    /// wheel's cursor walked on from where it last stood, out of a segment's end onto whatever the switches lead it, so a
    /// wheel knows only the track under it and never its train's route. A cursor on no segment yet is found by the
    /// nearest of all of them, once. A loose end holds the wheel at it.
    /// </summary>
    public (Vector2 OnM, Vector2 Along) Follow(ref RailCursor cursor, Vector2 pivotM, ReadOnlySpan<byte> positions)
    {
        if (cursor.Segment < 0) cursor = Nearest(pivotM);

        for (var step = 0; step <= MostSegmentsAWheelCrosses; step++)
        {
            var line = Segments[cursor.Segment];
            cursor.AlongM = line.NearestAlongFromM(pivotM, cursor.AlongM, out var beyondM);
            if (beyondM == 0f) break;

            var next = NextEnd((cursor.Segment * 2) + (beyondM > 0f ? 1 : 0), positions);
            if (next == Nowhere) break;

            cursor.Segment = next / 2;
            cursor.AlongM = next % 2 == 0 ? 0f : Segments[cursor.Segment].LengthM;
        }

        var on = Segments[cursor.Segment];
        return (on.PlaceAt(cursor.AlongM), on.EasedAlongAt(cursor.AlongM));
    }

    /// <summary>How many segment ends a wheel is walked across in one reading: more than a tick's travel ever crosses.</summary>
    const int MostSegmentsAWheelCrosses = 4;

    /// <summary>The place on the whole railway nearest a point: a walk of every segment, for a wheel put down and never in a step's steady run.</summary>
    public RailCursor Nearest(Vector2 pointM)
    {
        var (best, bestAwayM2) = (new RailCursor(0, 0f), float.PositiveInfinity);
        for (var segment = 0; segment < Segments.Length; segment++)
        {
            var alongM = Segments[segment].NearestAlongM(pointM);
            var awayM2 = Vector2.DistanceSquared(Segments[segment].PlaceAt(alongM), pointM);
            if (awayM2 < bestAwayM2) (best, bestAwayM2) = (new RailCursor(segment, alongM), awayM2);
        }

        return best;
    }

    /// <summary>The lane a train leaves a node on through a segment end, or −1 where its track is not run that way.</summary>
    public int LaneLeaving(int end) => end % 2 == 0 ? ForwardLaneOf[end / 2] : BackwardLaneOf[end / 2];

    /// <summary>
    /// <b>The lanes a train may run on from a lane</b> (TRN-9), each with the switch and position it needs, or −1 where no
    /// switch is faced: on through a joint, out of a branch onto the common end, or into whichever branch.
    /// </summary>
    public int NextLanes(int lane, Span<(int Lane, int Switch, int Position)> into)
    {
        var arrives = ArrivesBy(lane);
        var node = NodeOfEnd[arrives];
        var (first, count) = (EndOffsets[node], EndOffsets[node + 1] - EndOffsets[node]);
        var written = 0;
        if (count == 2 || (count > 2 && Ends[first] != arrives))
        {
            var next = count == 2 ? (Ends[first] == arrives ? Ends[first + 1] : Ends[first]) : Ends[first];
            var onto = LaneLeaving(next);
            if (onto >= 0) into[written++] = (onto, -1, -1);
            return written;
        }

        if (count < 2) return 0;

        for (var branch = 0; branch < count - 1; branch++)
        {
            var onto = LaneLeaving(Ends[first + 1 + branch]);
            if (onto >= 0) into[written++] = (onto, SwitchOfNode[node], branch);
        }

        return written;
    }

    /// <summary>
    /// <b>The position a switch stands at for a train to run from one lane onto the next</b> (TRN-9), or −1 where they meet
    /// at no switch: the branch it leaves by where it faces the switch from its common end, and the branch it comes in by
    /// where it trails through it — the position that train, backing, runs back onto the lane it came off.
    /// </summary>
    public int PositionThrough(int fromLane, int ontoLane, out bool facing)
    {
        var arrives = ArrivesBy(fromLane);
        var node = NodeOfEnd[arrives];
        var first = EndOffsets[node];
        facing = Ends[first] == arrives;
        if (SwitchOfNode[node] < 0) return -1;

        var branchEnd = facing ? LeavesBy(ontoLane) : arrives;
        for (var branch = 0; first + 1 + branch < EndOffsets[node + 1]; branch++)
        {
            if (Ends[first + 1 + branch] == branchEnd) return branch;
        }

        return -1;
    }

    // The route search's own working rows, laid once so a train sent allocates nothing.
    float[]? _shortestM;
    int[]? _cameFrom;
    bool[]? _settled;

    /// <summary>
    /// <b>The shortest run of lanes from one lane to any lane arriving at a node</b>, written into <paramref name="into"/>
    /// first lane first: a train's leg, found as a car's is, over the lanes and through the switches (TRN-9). A route that
    /// would ask a train to reverse is none, since no lane leads onto its own opposite. How many lanes it is, or 0 for none.
    /// </summary>
    public int Route(int fromLane, int toNode, Span<int> into)
    {
        var lanes = LaneCount;
        _shortestM ??= new float[lanes];
        _cameFrom ??= new int[lanes];
        _settled ??= new bool[lanes];
        Array.Fill(_shortestM, float.PositiveInfinity);
        Array.Fill(_cameFrom, -1);
        Array.Clear(_settled);
        _shortestM[fromLane] = LengthM(fromLane);
        Span<(int Lane, int Switch, int Position)> next = stackalloc (int, int, int)[8];
        while (true)
        {
            var lane = -1;
            for (var each = 0; each < lanes; each++)
            {
                if (!_settled[each] && !float.IsPositiveInfinity(_shortestM[each]) && (lane < 0 || _shortestM[each] < _shortestM[lane])) lane = each;
            }

            if (lane < 0) return 0;
            if (NodeAhead(lane) == toNode) return Unwound(lane, into);

            _settled[lane] = true;
            var count = NextLanes(lane, next);
            for (var each = 0; each < count; each++)
            {
                var onto = next[each].Lane;
                var throughM = _shortestM[lane] + LengthM(onto);
                if (_settled[onto] || throughM >= _shortestM[onto]) continue;

                _shortestM[onto] = throughM;
                _cameFrom[onto] = lane;
            }
        }
    }

    int Unwound(int last, Span<int> into)
    {
        var count = 0;
        for (var lane = last; lane >= 0; lane = _cameFrom![lane]) count++;
        if (count > into.Length) return 0;

        var at = count;
        for (var lane = last; lane >= 0; lane = _cameFrom![lane]) into[--at] = lane;
        return count;
    }

    bool[]? _crossed;

    /// <summary>
    /// <b>Whether two segments share ground</b> (TRN-5): one and the same — a track run both ways is two lanes over one —
    /// or two tracks crossing on the level, a track crossing itself among them. Worked out once, the first time asked.
    /// </summary>
    public bool ShareGround(int segment, int other)
    {
        if (segment == other) return true;

        var count = Segments.Length;
        if (_crossed is null)
        {
            var crossed = new bool[count * count];
            for (var one = 0; one < count; one++)
            {
                for (var two = one + 1; two < count; two++)
                {
                    if (!Cross(Segments[one], Segments[two])) continue;

                    crossed[(one * count) + two] = crossed[(two * count) + one] = true;
                }
            }

            _crossed = crossed;
        }

        return _crossed[(segment * count) + other];
    }

    /// <summary>Whether two lines cross anywhere but where they meet end to end: a chord of each through the other.</summary>
    static bool Cross(TrackLine one, TrackLine two)
    {
        var (oneLeast, oneMost) = Box(one.PointM);
        var (twoLeast, twoMost) = Box(two.PointM);
        if (oneMost.X < twoLeast.X || twoMost.X < oneLeast.X || oneMost.Y < twoLeast.Y || twoMost.Y < oneLeast.Y) return false;

        for (var a = 0; a + 1 < one.PointM.Length; a++)
        {
            for (var b = 0; b + 1 < two.PointM.Length; b++)
            {
                if (ChordsCross(one.PointM[a], one.PointM[a + 1], two.PointM[b], two.PointM[b + 1])) return true;
            }
        }

        return false;
    }

    static (Vector2 Least, Vector2 Most) Box(ReadOnlySpan<Vector2> points)
    {
        var (least, most) = (points[0], points[0]);
        foreach (var point in points) (least, most) = (Vector2.Min(least, point), Vector2.Max(most, point));
        return (least, most);
    }

    /// <summary>Whether two chords cross strictly inside both, so two lines meeting at a node do not.</summary>
    static bool ChordsCross(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
    {
        var (runA, runB) = (a1 - a0, b1 - b0);
        var denominator = Cross(runA, runB);
        if (denominator == 0f) return false;

        var alongA = Cross(b0 - a0, runB) / denominator;
        var alongB = Cross(b0 - a0, runA) / denominator;
        var slackA = MeetM / MathF.Max(runA.Length(), MeetM);
        var slackB = MeetM / MathF.Max(runB.Length(), MeetM);
        return alongA > slackA && alongA < 1f - slackA && alongB > slackB && alongB < 1f - slackB;
    }

    /// <summary>The railway of a plan as a network: its tracks cut where another ends on them, and its nodes and switches inferred.</summary>
    public static RailGraph Of(CityPlan.RailArrays rails, Vector2 worldSizeM)
    {
        if (rails.Count == 0) return None;

        // Where each track is cut: its two ends, and wherever another track's end stands on its line.
        var cuts = new List<float>[rails.Count];
        var lines = new TrackLine[rails.Count];
        for (var track = 0; track < rails.Count; track++)
        {
            lines[track] = new TrackLine(rails.TrackOf(track), rails.Level.AsSpan(rails.PointOffsets[track], rails.TrackOf(track).Length));
            cuts[track] = [0f, lines[track].LengthM];
        }

        for (var track = 0; track < rails.Count; track++)
        {
            foreach (var endM in (ReadOnlySpan<Vector2>)[rails.TrackOf(track)[0], rails.TrackOf(track)[^1]])
            {
                for (var other = 0; other < rails.Count; other++)
                {
                    if (other == track) continue;

                    var atM = lines[other].NearestAlongM(endM);
                    if (Vector2.Distance(lines[other].PlaceAt(atM), endM) > MeetM) continue;
                    if (atM <= MeetM || atM >= lines[other].LengthM - MeetM) continue;

                    cuts[other].Add(atM);
                }
            }
        }

        // And wherever a track goes onto a bridge or comes off one, so a segment — and the way over it — stands on one level,
        // and wherever it would be longer than the atlas files a way.
        for (var track = 0; track < rails.Count; track++)
        {
            var levels = rails.Level.AsSpan(rails.PointOffsets[track], rails.TrackOf(track).Length);
            for (var point = 1; point + 1 < levels.Length; point++)
            {
                if (levels[point] != levels[point - 1]) cuts[track].Add(lines[track].AtM[point]);
            }

            cuts[track].Sort();
            for (var cut = 0; cut + 1 < cuts[track].Count; cut++)
            {
                var (startM, endM) = (cuts[track][cut], cuts[track][cut + 1]);
                var pieces = (int)MathF.Ceiling((endM - startM) / LongestSegmentM);
                for (var piece = 1; piece < pieces; piece++) cuts[track].Add(startM + ((endM - startM) * piece / pieces));
            }
        }

        var segments = new List<TrackLine>();
        var trackOf = new List<int>();
        var fromM = new List<float>();
        var endPlaces = new List<Vector2>();
        for (var track = 0; track < rails.Count; track++)
        {
            var at = cuts[track];
            at.Sort();
            for (var cut = 0; cut + 1 < at.Count; cut++)
            {
                if (at[cut + 1] - at[cut] <= MeetM) continue;

                var piece = lines[track].Piece(at[cut], at[cut + 1]);
                segments.Add(piece);
                fromM.Add(at[cut]);
                trackOf.Add(track);
                endPlaces.Add(piece.PointM[0]);
                endPlaces.Add(piece.PointM[^1]);
            }
        }

        // The nodes: every segment end within a float's slack of another one is the same node.
        var nodeOfEnd = new int[endPlaces.Count];
        var nodeM = new List<Vector2>();
        for (var end = 0; end < endPlaces.Count; end++)
        {
            var node = nodeM.FindIndex(placeM => Vector2.Distance(placeM, endPlaces[end]) <= MeetM);
            if (node < 0)
            {
                node = nodeM.Count;
                nodeM.Add(endPlaces[end]);
            }

            nodeOfEnd[end] = node;
        }

        var endsOf = new List<int>[nodeM.Count];
        for (var node = 0; node < nodeM.Count; node++) endsOf[node] = [];
        for (var end = 0; end < nodeOfEnd.Length; end++) endsOf[nodeOfEnd[end]].Add(end);

        var switchOfNode = new int[nodeM.Count];
        var nodeOfSwitch = new List<int>();
        var isPortal = new bool[nodeM.Count];
        var endOffsets = new int[nodeM.Count + 1];
        var ends = new List<int>();
        for (var node = 0; node < nodeM.Count; node++)
        {
            var at = endsOf[node];
            switchOfNode[node] = -1;
            isPortal[node] = at.Count == 1 && !OnTheMap(nodeM[node], worldSizeM);
            if (at.Count > 2)
            {
                switchOfNode[node] = nodeOfSwitch.Count;
                nodeOfSwitch.Add(node);
                at = SwitchOrder(at, segments);
            }

            ends.AddRange(at);
            endOffsets[node + 1] = ends.Count;
        }

        var segmentOfLane = new List<int>();
        var laneForward = new List<bool>();
        var forwardLaneOf = new int[segments.Count];
        var backwardLaneOf = new int[segments.Count];
        for (var segment = 0; segment < segments.Count; segment++)
        {
            var runs = rails.Runs[trackOf[segment]];
            forwardLaneOf[segment] = backwardLaneOf[segment] = -1;
            if ((runs & TownMap.TrackRuns.Forward) != 0)
            {
                forwardLaneOf[segment] = segmentOfLane.Count;
                segmentOfLane.Add(segment);
                laneForward.Add(true);
            }

            if ((runs & TownMap.TrackRuns.Backward) != 0)
            {
                backwardLaneOf[segment] = segmentOfLane.Count;
                segmentOfLane.Add(segment);
                laneForward.Add(false);
            }
        }

        return new RailGraph(
            [.. segments], [.. trackOf], nodeOfEnd, [.. nodeM], endOffsets, [.. ends], switchOfNode, [.. nodeOfSwitch], isPortal,
            [.. segmentOfLane], [.. laneForward], forwardLaneOf, backwardLaneOf) { SegmentFromM = [.. fromM] };
    }

    static bool OnTheMap(Vector2 atM, Vector2 worldSizeM) => atM.X >= 0f && atM.Y >= 0f && atM.X <= worldSizeM.X && atM.Y <= worldSizeM.Y;

    /// <summary>The way a segment leaves its node by one of its ends.</summary>
    static Vector2 Leaving(List<TrackLine> segments, int end)
    {
        var line = segments[end / 2];
        return end % 2 == 0 ? line.AlongAt(0f) : -line.AlongAt(line.LengthM);
    }

    /// <summary>
    /// <b>A switch's ends, its common end first</b>: the one end leading away from every other — and then its branches, in
    /// the order they turn from the common end's line, so a position names the same branch every time the town is laid.
    /// </summary>
    /// <exception cref="InvalidDataException">No one end leads away from all the others: no switch a train can be set through.</exception>
    static List<int> SwitchOrder(List<int> ends, List<TrackLine> segments)
    {
        var common = -1;
        foreach (var end in ends)
        {
            var away = true;
            foreach (var other in ends)
            {
                if (other != end && Vector2.Dot(Leaving(segments, end), Leaving(segments, other)) >= 0f) away = false;
            }

            if (!away) continue;
            if (common >= 0) throw new InvalidDataException("Two ends of one node lead away from the rest: it is no switch.");

            common = end;
        }

        if (common < 0) throw new InvalidDataException("No end of a node where tracks fork leads away from the rest: it is no switch.");

        var through = -Leaving(segments, common);
        var branches = ends.Where(end => end != common)
            .OrderBy(end => MathF.Atan2(Cross(through, Leaving(segments, end)), Vector2.Dot(through, Leaving(segments, end))))
            .ThenBy(end => end);
        return [common, .. branches];
    }

    static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);
}
