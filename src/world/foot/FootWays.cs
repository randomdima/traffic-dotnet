using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// <b>The lines a walk is walked on between the points the nodes hand over at</b> (WLK-11): one way per pair
/// of points still handed over (<see cref="FootConnectors"/>), and a chain of arcs per lane of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A way down a road or round a junction is a stretch of its lane's own course</b>
/// (<see cref="WalkLines"/>, <see cref="KerbLines.Between"/>) — the driven ground's boundary already moved
/// off itself by that lane's offset, so the pavement bends where the kerb bends and keeps the same distance
/// off the tarmac along its whole length. <b>Nothing is offset here</b>: the move is the whole town's and is
/// taken once, which is what makes a corner tighter than the offset come out as the shape's own answer
/// instead of a fold. <b>A crossing is the straight between its two points</b>: what it runs over is the
/// carriageway, which the boundary is the edge of rather than a line across.
/// </para>
/// <para>
/// <b>A way is laid once, from the lower-numbered of the two nodes it joins.</b> Which node a way reaches is
/// its own inverse (<see cref="FootConnectors.Reaches"/>), so both ends would otherwise lay the same line
/// twice — and the pair a merge dropped is laid from neither end, a corner having nothing to walk to itself
/// along.
/// </para>
/// <para>
/// <b>Lane for lane and not end for end</b>: the two points of a pair are the two lanes' own lines (WLK-8),
/// and the <c>n</c>th of one pair faces the <c>n</c>th of the other — both are struck the same way round
/// from the boundary, so a way's two lanes run beside each other rather than crossing in the middle.
/// </para>
/// <para>
/// <b>And a lane is walked one way, as a car's lane is driven one way</b> (WLK-8): the pair's two lanes run
/// opposite ways, and which is which is the side the town keeps (TER-4a) rather than the order the way was
/// laid in. Each lane's chain runs the way it is walked, so its first piece starts where a walker joins it.
/// </para>
/// <para>
/// <b>It exists only while a town is being laid</b>, as the points and the nodes before it do. Nothing on a
/// tick holds one: what a body is held on is the walking network's own (<see cref="WalkingNetwork"/>).
/// </para>
/// </remarks>
internal sealed class FootWays
{
    /// <summary>A town with no pavement, whose nodes hand nothing over and which has nothing to lay between.</summary>
    public static readonly FootWays None = new(FootConnectors.None, [], [], [], [0], [], [], []);

    readonly FootConnectors _connectors;
    readonly int[] _fromNode;
    readonly int[] _ontoNode;
    readonly FootConnectorKind[] _kind;

    /// <summary>Where each lane's chain begins in <see cref="_arcs"/>, one more entry than there are lanes.</summary>
    readonly int[] _laneOffsets;

    readonly ArcSeg[] _arcs;
    readonly bool[] _alongTheKerb;
    readonly int[] _setsOffAt;

    FootWays(
        FootConnectors connectors, int[] fromNode, int[] ontoNode, FootConnectorKind[] kind, int[] laneOffsets,
        ArcSeg[] arcs, bool[] alongTheKerb, int[] setsOffAt)
    {
        _connectors = connectors;
        _fromNode = fromNode;
        _ontoNode = ontoNode;
        _kind = kind;
        _laneOffsets = laneOffsets;
        _arcs = arcs;
        _alongTheKerb = alongTheKerb;
        _setsOffAt = setsOffAt;
    }

    /// <summary>How many ways the town's walk is laid as, each of them a pair of lanes.</summary>
    public int Count => _kind.Length;

    /// <summary>The points these ways run between, which is what placed every one of them.</summary>
    public FootConnectors Connectors => _connectors;

    /// <summary>Which of a node's three ways this one is, which is the whole of what decides how it is laid.</summary>
    public FootConnectorKind KindOf(int way) => _kind[way];

    /// <summary>The node the way was laid from — the lower-numbered of the two it joins.</summary>
    /// <remarks>
    /// <b>It says which two nodes the way joins and not which way anything is walked</b>: a lane runs one
    /// way and the other lane the other (<see cref="SetsOffAt"/>), so a way has no direction of its own any
    /// more than a two-way street has.
    /// </remarks>
    public int FromNode(int way) => _fromNode[way];

    /// <summary>And the node at its other end.</summary>
    public int OntoNode(int way) => _ontoNode[way];

    /// <summary>
    /// <b>The node one lane sets off from</b> (WLK-8): the pair's two lanes are walked opposite ways, so this
    /// is one of the way's two ends for one of them and the other end for the other.
    /// </summary>
    public int SetsOffAt(int way, int lane) => _setsOffAt[At(way, lane)];

    /// <summary>And the node it arrives at, which is the way's other end.</summary>
    public int ArrivesAt(int way, int lane) =>
        _setsOffAt[At(way, lane)] == _fromNode[way] ? _ontoNode[way] : _fromNode[way];

    /// <summary>
    /// One lane's own line, <b>walked in the direction it is walked</b> — from <see cref="SetsOffAt"/>'s
    /// point to <see cref="ArrivesAt"/>'s, as a car's lane runs the way the car drives it.
    /// </summary>
    public ReadOnlySpan<ArcSeg> LaneOf(int way, int lane) =>
        _arcs.AsSpan(_laneOffsets[At(way, lane)], _laneOffsets[At(way, lane) + 1] - _laneOffsets[At(way, lane)]);

    /// <summary>
    /// <b>Whether that lane really is a stretch of its own course</b> (<see cref="WalkLines"/>), which every
    /// lane of a way down a road or round a junction should be. A crossing is never one, and a walk that is
    /// not one where it should be was laid as the straight between its ends instead — <b>which is the
    /// instruments' to report</b> rather than something to pass over.
    /// </summary>
    public bool AlongTheKerb(int way, int lane) => _alongTheKerb[At(way, lane)];

    /// <summary>Lays every way the town's pedestrian nodes hand over to. Build-time only — it allocates freely.</summary>
    public static FootWays Lay(CityPlan plan, SimConfig config) =>
        // One merge for the boundary the points are struck off and the courses the ways are taken from: a
        // second one would be a second answer about the same ground.
        Lay(plan, config, WalkLines.Of(plan.Paving(config).Perimeter(config), config));

    /// <summary>The same, off courses somebody already holds — which everything laid over the ways needs too.</summary>
    public static FootWays Lay(CityPlan plan, SimConfig config, WalkLines walks) =>
        plan.PavementWidthM <= 0f
            ? None
            : Lay(FootConnectors.Lay(FootJunctions.Lay(plan, config), walks, config), walks, config);

    /// <summary>The same, off points and courses somebody already holds.</summary>
    public static FootWays Lay(FootConnectors connectors, WalkLines walks, SimConfig config)
    {
        var fromNode = new List<int>();
        var ontoNode = new List<int>();
        var kinds = new List<FootConnectorKind>();
        var laneOffsets = new List<int> { 0 };
        var arcs = new List<ArcSeg>();
        var alongTheKerb = new List<bool>();
        var setsOffAt = new List<int>();

        for (var node = 0; node < connectors.NodeCount; node++)
        {
            if (!connectors.StandsAt(node)) continue;

            for (var which = 0; which < FootConnectors.Kinds; which++)
            {
                var kind = (FootConnectorKind)which;
                var onto = connectors.Reaches(node, kind);
                if (!connectors.HandsOver(node, kind) || onto <= node || !connectors.StandsAt(onto)) continue;

                fromNode.Add(node);
                ontoNode.Add(onto);
                kinds.Add(kind);

                // A crossing runs over the carriageway rather than along it, so the boundary has nothing to
                // say about either the side it takes or the way it is walked.
                var laid = kind == FootConnectorKind.Crossing
                    ? (NearM: (Vector2?)null, DownFrom: NoEnd)
                    : AlongTheBoundary(walks.Boundary, connectors, node, onto);

                for (var lane = 0; lane < FootConnectors.LanesPerWay; lane++)
                {
                    var line = Lane(connectors, walks, node, onto, kind, lane, laid.NearM, out var followed);
                    setsOffAt.Add(
                        SetsOff(
                            connectors, node, onto, kind, lane, line, laid.DownFrom, config.RoadSideSign,
                            out var forward));

                    alongTheKerb.Add(followed);
                    if (forward)
                    {
                        arcs.AddRange(line);
                    }
                    else
                    {
                        var back = new ArcSeg[line.Length];
                        Spline.ReverseInto(line, back);
                        arcs.AddRange(back);
                    }

                    laneOffsets.Add(arcs.Count);
                }
            }
        }

        return new FootWays(
            connectors, [.. fromNode], [.. ontoNode], [.. kinds], [.. laneOffsets], [.. arcs],
            [.. alongTheKerb], [.. setsOffAt]);
    }

    /// <summary>
    /// <b>One lane of one way: the stretch of that lane's own course between its two ends</b>
    /// (<see cref="WalkLines"/>, WLK-11) — the whole town's boundary moved by this lane's offset, cut
    /// between the two places the nodes hand it over at, and nothing else at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing is fitted to the ends, because the ends are on the course</b> (WLK-9): a point is struck
    /// off the boundary and then dropped onto the course it belongs to, so the stretch already begins and
    /// ends where the place hands the lane over. A corner laid between a point and a line passing near it is
    /// a line the shape never drew, and it is what put a metre and a half of walk across open pavement at
    /// every corner the move swallowed.
    /// </para>
    /// <para>
    /// <b>A crossing is the straight between its two points</b>: what it runs over is the carriageway, which
    /// the boundary is the edge of rather than a line across.
    /// </para>
    /// <para>
    /// <b>And where the course cannot answer there is no lane</b> — two points on two different lines of it
    /// have no stretch between them. A straight drawn between them instead is a walk over whatever stands in
    /// the way; <b>how many a town has is the census's to report</b>.
    /// </para>
    /// <para>
    /// <b>Laid without a direction and turned round after</b> (<see cref="SetsOff"/>): which way a lane is
    /// walked is a fact about the side the town keeps (TER-4a) and not about which of the two nodes the way
    /// happened to be laid from.
    /// </para>
    /// </remarks>
    /// <param name="nearM">A place the way is known to run near, or none where the boundary could not say.</param>
    /// <param name="followed">Whether the course answered, a crossing being the one lane that never asks it.</param>
    static ArcSeg[] Lane(
        FootConnectors connectors, WalkLines walks, int node, int onto, FootConnectorKind kind, int lane,
        Vector2? nearM, out bool followed)
    {
        followed = false;

        // A lane whose two ends are one point has no length to lay down (WLK-12), and a stretch of course
        // between one place and itself would be the whole ring.
        if (connectors.Shares(node, kind, lane)) return [];

        var fromM = connectors.PointM(node, kind, lane);
        var ontoM = connectors.PointM(onto, kind, lane);
        if (kind == FootConnectorKind.Crossing)
        {
            var run = ontoM - fromM;
            var lengthM = run.Length();
            return lengthM > LineTolerance.RoundingM
                ? [new ArcSeg(fromM, MathF.Atan2(run.Y, run.X), lengthM, 0f)]
                : [];
        }

        var course = walks.Course(lane);
        if (!course.NearestTo(fromM, out _, out var from) || !course.NearestTo(ontoM, out _, out var at))
        {
            return [];
        }

        var alongTheCourse = nearM is { } beside ? course.Between(from, at, beside) : course.Between(from, at);
        followed = alongTheCourse.Length > 0;
        return alongTheCourse;
    }

    /// <summary>What a way whose boundary could not be read stands for an end of, and no node ever is.</summary>
    const int NoEnd = FootJunctions.NoNode;

    /// <summary>
    /// <b>Where the boundary between a way's two ends runs</b>, read once for the way off the one line every
    /// course was struck from: a place it is known to pass, and the end of the way it runs forward from.
    /// </summary>
    /// <remarks>
    /// <b>Both answers have to be the way's and not a lane's</b>, because a lane's course is cut against the
    /// whole town at its own distance and two of them can disagree about either. The place is what keeps the
    /// two lanes on one side of a block (<see cref="KerbLines.Between(KerbLines.Station, KerbLines.Station,
    /// Vector2)"/>); the end is what keeps them walked against one another, a course being walked forward
    /// the way the boundary under it is.
    /// </remarks>
    static (Vector2? NearM, int DownFrom) AlongTheBoundary(
        KerbLines kerbs, FootConnectors connectors, int node, int onto)
    {
        var fromM = connectors.KerbAtM(node);
        var ontoM = connectors.KerbAtM(onto);
        if (!kerbs.NearestTo(fromM, out _, out var from) || !kerbs.NearestTo(ontoM, out _, out var at))
        {
            return (null, NoEnd);
        }

        var stretch = kerbs.Between(from, at);
        if (stretch.Length == 0) return (null, NoEnd);

        var lengthM = Spline.TotalLengthM(stretch);
        var cutFromNode =
            Vector2.DistanceSquared(stretch[0].StartM, fromM)
            <= Vector2.DistanceSquared(stretch[0].StartM, ontoM);

        return (Spline.SampleAt(stretch, lengthM * 0.5f).PositionM, cutFromNode ? node : onto);
    }

    /// <summary>
    /// <b>The node a lane sets off from</b> (WLK-8): a walker keeps the hand the traffic keeps (TER-4a), so
    /// <b>a lane is walked the way that puts it on its own walker's side of the pair it belongs to</b> —
    /// which is one statement over all three kinds, the pavement whose lanes stand across the kerb and the
    /// crossing whose lanes stand along it alike.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A pavement's answer is the way's and not the lane's</b>, and it is the boundary's own direction
    /// under the way (<paramref name="downFrom"/>). Every ring of a merged shape is walked with the covered
    /// ground on its right and is moved off itself to the walker's left
    /// (<see cref="BandShell.Outset"/>), so every course is walked with the tarmac on the same hand as the
    /// boundary under it — and the lane against the kerb is the one whose walker has that tarmac on the
    /// keeping hand. <b>Read off each lane's own line instead, the two lanes disagree</b>: a lane that fell
    /// back to a stride of straight has no direction worth reading, and two courses cut apart from one
    /// another can start their stretch at two different ends of the way.
    /// </para>
    /// <para>
    /// <b>Which end the line was laid from is a separate question and is also not the end the way is
    /// numbered from</b>: a stretch of a course is cut forward from whichever of the way's two ends the arc
    /// begins at (<see cref="KerbLines.Between"/>). It decides only whether the chain stands as laid or is
    /// turned round.
    /// </para>
    /// <para>
    /// <b>A crossing is read off its pair</b>, which is the same statement made where the boundary cannot
    /// make it: its two lanes stand along the boundary rather than across it, so the hand is the pair's own
    /// axis weighed against the line. Its line is the straight from one of its own points, so the pair and
    /// the tangent are read at one place by construction.
    /// </para>
    /// </remarks>
    /// <param name="downFrom">
    /// The end the boundary under the way runs forward from (<see cref="AlongTheBoundary"/>), or
    /// <see cref="NoEnd"/> where it says nothing — a crossing, whose pair stands along the boundary rather
    /// than across it, and a way whose two ends the boundary could not be read between.
    /// </param>
    /// <param name="forward">Whether that is the end the line was laid from, so the line stands as laid.</param>
    static int SetsOff(
        FootConnectors connectors, int node, int onto, FootConnectorKind kind, int lane,
        ReadOnlySpan<ArcSeg> line, int downFrom, float sideSign, out bool forward)
    {
        forward = true;

        // A lane welded to one point (WLK-12) has no line to read an end off, and still has two ends: it
        // sets off where the boundary says it would have, so a way's two lanes still name each other's.
        if (line.Length == 0)
        {
            if (downFrom == NoEnd) return node;

            return (lane == 0) == (sideSign > 0f) ? downFrom : downFrom == node ? onto : node;
        }

        var laidFrom =
            Vector2.DistanceSquared(line[0].StartM, connectors.PointM(node, kind, lane))
            <= Vector2.DistanceSquared(line[0].StartM, connectors.PointM(onto, kind, lane))
                ? node
                : onto;

        var setsOff = downFrom == NoEnd
            ? Keeps(connectors, laidFrom, kind, lane, line[0].StartUnit, sideSign) ? laidFrom : Other(laidFrom)
            : (lane == 0) == (sideSign > 0f) ? downFrom : Other(downFrom);

        forward = setsOff == laidFrom;
        return setsOff;

        int Other(int end) => end == node ? onto : node;
    }

    /// <summary>
    /// Whether a lane, walked the way its line was laid, leaves its walker on the hand the town keeps: the
    /// pair's own axis weighed against that hand of the line.
    /// </summary>
    static bool Keeps(
        FootConnectors connectors, int laidFrom, FootConnectorKind kind, int lane, Vector2 startUnit,
        float sideSign)
    {
        var across = connectors.PointM(laidFrom, kind, 1) - connectors.PointM(laidFrom, kind, 0);
        var ownSide = Vector2.Dot(Heading.RightOf(startUnit) * sideSign, across);

        return lane == 1 ? ownSide > 0f : ownSide < 0f;
    }

    static int At(int way, int lane) => (way * FootConnectors.LanesPerWay) + lane;
}
