using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>What a road is for, which is what decides its width, how far it may bend and what it fronts.</summary>
internal enum RoadClass : byte
{
    /// <summary>A block's own street: short, laid on its district's bearing, and where the buildings are.</summary>
    Street,

    /// <summary>A road between districts: long, faster, and the only roads that cross a district edge.</summary>
    Arterial,

    /// <summary>
    /// A road between the town and outside (GEN-63): the widest, met least often, and never by a district's own
    /// street (GEN-68). It may run off the map (GEN-2b).
    /// </summary>
    Intercity,

    /// <summary>
    /// The one span between two bridgeheads. <b>The only class of road that may stand over water</b>
    /// (GEN-14a), and the only one whose shape is settled before it is laid: straight, whatever the arterial
    /// it is a piece of was going to do.
    /// </summary>
    Bridge,

    /// <summary>
    /// One piece of a roundabout's circulating carriageway (GEN-19). <b>Every one of them is driven one way
    /// and bends</b>, so a ring is the one place this layout carries a road that is neither a street nor a
    /// way between two districts, and nothing fronts one.
    /// </summary>
    Roundabout,

    /// <summary>
    /// A car park's bay (GEN-53), a road of its own joined to nothing. <b>It is laid rather than drawn</b>: a
    /// straight square to the street it stands off, from over that street's kerb to the far end of the space,
    /// so nothing here wanders, jitters or bends.
    /// </summary>
    CarPark,

    /// <summary>
    /// <b>A slip road</b> (GEN-68): one lane, one way, between an intercity road and an interchange's terminal, leaving or
    /// joining the intercity road at a shallow angle. <b>Its movement onto or off the intercity road is the kerb lane's
    /// alone</b>, whatever its angle (TER-5j), so the lanes beside it pass by.
    /// </summary>
    Slip,
}

/// <summary>One road of the layout before it has a shape: what it joins, what it is for, and how it runs.</summary>
/// <param name="Curvature">
/// The bend the layout itself asks for, as 1/radius — a roundabout's own circle, and
/// zero for everything the road stage is free to wander (<see cref="RoadStage"/>). It is signed the way
/// <see cref="ArcSeg.Curvature"/> is: positive turns to the driver's right.
/// </param>
/// <param name="Flow">
/// Which way it is driven (TER-4d). <b>Every road is laid running both ways</b>; which of them run one way
/// is chosen and settled over the whole layout once it stands (<see cref="OneWayStreets"/>).
/// </param>
/// <param name="ThroughM">
/// The places the road passes on its way between its two ends, in order, and <b>empty on all but a road
/// <see cref="Gen.ThroughRoads"/> joined out of several</b> (GEN-51). They are where junctions stood that
/// turned out to be nowhere two roads met: the corner is the town's and the road keeps it, so the road
/// stage lays the line through them rather than drawing a middle of its own.
/// </param>
/// <param name="Straight">
/// <b>Whether the road is laid straight</b> (GEN-47): each arm on the chord to the place the road runs for
/// rather than jittered off it (GEN-46), and no wander of its own — so between two places it is one straight
/// piece, and it bends only at a corner it was joined through. Some of every district's streets are, drawn
/// street by street at the district's own share (<see cref="Lattice"/>).
/// </param>
/// <param name="Lanes">
/// How many lanes it carries each way it is driven (GEN-67), and so how wide it is laid and how much ground it keeps
/// (<see cref="RoadLines"/>). Fixed when the road is offered, because the ground it keeps is.
/// </param>
/// <param name="Unwalked">
/// Whether it has no walk beside it (GEN-75): an intercity road, and a bridge that is a span of one — which its own
/// class does not say, a span being <see cref="RoadClass.Bridge"/> whatever it carries.
/// </param>
/// <param name="Over">
/// <b>Whether it is carried over what it crosses</b> (<see cref="CityPlan.RoadArrays.Over"/>): an interchange's road over
/// an intercity road, and an intercity road's span over a railway (GEN-68). Its ground and an intercity road's are never
/// one another's, and it meets nothing between its ends.
/// </param>
internal readonly record struct LayoutEdge(
    int From, int To, RoadClass Class, float Curvature, RoadFlow Flow, Vector2[] ThroughM, bool Straight = false, int Lanes = 1,
    bool Unwalked = false, bool Over = false)
{
    /// <summary>
    /// How wide its carriageway is laid as it is offered: its lanes each way, both ways — every road is offered running
    /// both ways, and which run one way is settled after (<see cref="OneWayStreets"/>).
    /// </summary>
    public float OfferedWidthM(float laneWidthM) => laneWidthM * Lanes * SimConfig.LanesPerCarriageway;

    /// <summary>The most lanes a road carries each way: four in all (GEN-67).</summary>
    public const int MostLanes = 2;
}

/// <summary>
/// <b>The town as nodes and what joins them</b>, before any of it is a curve or a cell — the product of the
/// district and node stage and the whole of what the road stage is given.
/// </summary>
/// <remarks>
/// <para>
/// <b>A road is a road when it is offered or it is not one at all</b> (GEN-10). Every link is drawn as the
/// line it would be laid as before it is taken (<see cref="RoadLines"/>), so the town never holds a road that
/// cannot be drawn and no later pass has to delete one and repair behind itself: the arms have to stand square
/// enough (GEN-13), the line has to hold its class's floor (GEN-47) and it has to keep off the ground every
/// road already laid holds (GEN-49). <b>Which of two roads gives way is the order they were offered in</b>,
/// which is why the arterials are laid before the lattice.
/// </para>
/// <para>
/// <b>One connected component with nothing dangling off it, reached by deletion rather than by retry</b>
/// (GEN-5, GEN-5a). Water or a district edge can leave a piece of the town joined to nothing, and
/// <see cref="KeepTheLargestComponent"/> deletes that, or leave a street ending in a field, and
/// <see cref="PruneTheDeadEnds"/> deletes that. A town is what stayed connected and led somewhere, and the
/// alternative — laying it again with another seed until it is one piece — is the search this generator does
/// not do.
/// </para>
/// <para>
/// <b>The nodes are settled before the first road is laid and nothing moves one afterwards</b>
/// (<see cref="SettleTheNodes"/>, GEN-16). A node is what every line, arm and lane end is drawn from, so a
/// node moved once a road stands is every road at it drawn again — and after that, everything here deletes
/// and nothing retries.
/// </para>
/// </remarks>
/// <param name="slipArmsApartMinRad">How near a slip road's arm may lie to an intercity road's or another slip's (GEN-13).</param>
internal sealed class TownLayout(
    float shortestRoadM, float armsApartMinRad, float localityM, GridLevel level, WaterRules water, RoadLines lines,
    NodeSizes sizes, float slipArmsApartMinRad = 0f)
{
    /// <summary>How much ground each node takes and which of them a road runs off the map at.</summary>
    public NodeSizes Sizes => sizes;

    /// <summary>The shortest road the layout takes: the ground two junctions take (<see cref="Join"/>).</summary>
    public float ShortestRoadM => shortestRoadM;

    readonly List<Vector2> _nodeM = [];

    /// <summary>
    /// The nodes filed by cell (SIM-8, the main level), for <see cref="StandsClear"/>. Laid on the first ask and
    /// added to as nodes are; a settle or a rebuild renumbers the nodes and drops it.
    /// </summary>
    PointCells? _nodesIn;

    /// <summary>The line each road was laid as, drawn when it was offered and carried with it thereafter.</summary>
    readonly List<ArcSeg[]> _lineOf = [];

    /// <summary>
    /// The bearing each road leaves each node on, its class and its lanes each way, so a new arm can be asked how square it
    /// stands to them and whether it carries a joint's lanes on (<see cref="JointTakes"/>).
    /// </summary>
    readonly List<List<(float Rad, RoadClass Class, int Lanes)>> _armsAt = [];
    readonly List<LayoutEdge> _edges = [];

    /// <summary>Which pairs of nodes are already joined, so no two of them are joined twice.</summary>
    readonly HashSet<(int From, int To)> _joined = [];

    public IReadOnlyList<Vector2> NodeM => _nodeM;

    public IReadOnlyList<LayoutEdge> Edges => _edges;

    /// <summary>Whether a node could stand here at all, which is the one thing the ground refuses (GEN-14, RAIL-5).</summary>
    public bool Dry(Vector2 atM) => water.Holds(atM);

    /// <summary>
    /// <b>The line one road is laid as</b>, drawn when it was offered (<see cref="RoadLines"/>). It is the
    /// carriageway's own centreline: what a one-way road is moved onto half of, and what every lane of it is
    /// offset from (TER-4d).
    /// </summary>
    public ReadOnlySpan<ArcSeg> LineOf(int road) => _lineOf[road];

    /// <summary>
    /// One node, or <c>−1</c> where the ground will not carry one. <b>Nothing stands on the water</b>
    /// (GEN-14): a node is a junction, and a junction in the river is the box, the fillets, the crossings and
    /// the bar that come with it laid over open water. A caller that is refused lays nothing there.
    /// </summary>
    public int AddNode(Vector2 atM)
    {
        if (!water.Holds(atM)) return -1;

        _nodeM.Add(atM);
        _nodesIn?.Add(atM);
        _armsAt.Add([]);
        return _nodeM.Count - 1;
    }

    /// <summary>
    /// <b>Whether a node here would stand a locality clear of every node the layout has</b> (GEN-16) — asked
    /// of all of them and not of the two a road runs between, a road being free to bow past a third.
    /// </summary>
    /// <remarks>
    /// <b>Safe to ask from several threads at once</b> while nothing is added, the index being laid whole
    /// before any of them reads it.
    /// </remarks>
    public bool StandsClear(Vector2 atM)
    {
        var nodesIn = LazyInitializer.EnsureInitialized(ref _nodesIn, () =>
        {
            var index = new PointCells(level);
            foreach (var nodeM in _nodeM) index.Add(nodeM);
            return index;
        });

        return !nodesIn.AnyWithin(atM, localityM);
    }

    /// <summary>
    /// <b>Every cluster of nodes standing within a locality of each other is one node</b> (GEN-16), settled
    /// <b>before the first road is laid</b>: what would otherwise be two junctions a stride apart — a pair of
    /// boxes with their corners, crossings and bars laid over each other, joined by a road no car is ever on —
    /// is one junction every road at either of them meets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A cluster and not a pair.</b> Two nodes a stride apart and a third a stride beyond the second are
    /// one place and not two, and closing the chain is most of what this is worth: asked pair by pair, the
    /// third stays where it is and whatever ran through it is left hanging off a town it no longer reaches.
    /// </para>
    /// <para>
    /// <b>The node placed first is the node that stays</b>, which is the precedence without weighing one: the
    /// hub, the bridgeheads and the arterials are placed before any lattice point, so a street standing too
    /// near an arterial's junction is the one that moves onto it and never the other way round. A deck cannot
    /// move at all, and nothing has been laid yet that could have to be laid again.
    /// </para>
    /// <para>
    /// <b>It is why the ground bound holds from the first road</b> (GEN-49): a pair of junctions about to
    /// become one is a pair that already is one, so a road may be held to the ground every road already laid
    /// holds without any of it being offered twice.
    /// </para>
    /// </remarks>
    /// <returns>Where each node went, for the stages holding node numbers of their own.</returns>
    public int[] SettleTheNodes()
    {
        var root = Clusters.Apart(_nodeM.Count);
        var filed = new PointCells(level);
        foreach (var nodeM in _nodeM) filed.Add(nodeM);

        var near = new List<int>();
        for (var node = 0; node < root.Length; node++)
        {
            filed.Within(_nodeM[node], localityM, near);
            foreach (var other in near)
            {
                if (other > node && (_nodeM[node] - _nodeM[other]).LengthSquared() < localityM * localityM)
                {
                    Clusters.Union(root, node, other);
                }
            }
        }

        var stays = new int[_nodeM.Count];
        Array.Fill(stays, -1);
        for (var node = 0; node < _nodeM.Count; node++)
        {
            var cluster = Clusters.Find(root, node);
            if (stays[cluster] < 0) stays[cluster] = node;
        }

        var moved = new int[_nodeM.Count];
        var kept = new List<Vector2>(_nodeM.Count);
        for (var node = 0; node < _nodeM.Count; node++)
        {
            if (stays[Clusters.Find(root, node)] != node)
            {
                moved[node] = -1;
                continue;
            }

            moved[node] = kept.Count;
            kept.Add(_nodeM[node]);
        }

        for (var node = 0; node < _nodeM.Count; node++)
        {
            if (moved[node] >= 0) continue;

            var stayed = stays[Clusters.Find(root, node)];
            moved[node] = moved[stayed];
            sizes.Took(_nodeM[stayed], _nodeM[node]);
        }

        _nodeM.Clear();
        _nodeM.AddRange(kept);
        _nodesIn = null;
        _armsAt.Clear();
        for (var node = 0; node < _nodeM.Count; node++) _armsAt.Add([]);
        return moved;
    }

    /// <summary>
    /// One road between two nodes, if it is a road at all. Five things are refused here rather than found
    /// later, and each of them is something no junction in this engine's geometry can be made of:
    /// <list type="bullet">
    /// <item><b>Two nodes joined twice</b> — ground two carriageways share with no junction between them.</item>
    /// <item><b>A road shorter than the ground two junctions take</b> — a pair of boxes painted over each
    /// other.</item>
    /// <item><b>A road standing on the water that is not a bridge</b> (GEN-14a), and a bridge that is not a
    /// short straight span over a river.</item>
    /// <item><b>An arm standing too far off square to the arms already there</b> (GEN-13). A junction's kerb fillets,
    /// the crossing on each arm and the bar behind it are all laid across an arm on the assumption that the
    /// next arm round is not lying against it; two carriageways meeting at a shallow angle overlap for tens
    /// of metres, and everything laid on either of them lands on the other.</item>
    /// <item><b>A link whose line cannot be laid</b> (<see cref="RoadLines"/>) — one that cannot meet both of
    /// its drawn bearings inside its own floor, one that would run off the world or over water, or one that
    /// would share ground with a road already laid (GEN-47, GEN-49). <b>The line is drawn here rather than
    /// after the layout stands</b>, because a road refused later is a road every repair behind it has to be
    /// made for.</item>
    /// </list>
    /// <b>A road refused here is a road the town does not have</b>, and whatever that leaves unreachable is
    /// deleted with its own piece (<see cref="KeepTheLargestComponent"/>) rather than joined some other way.
    /// </summary>
    /// <returns>The road, or <c>−1</c> where it was refused.</returns>
    /// <param name="flow">
    /// Which way it is driven, where that is the layout's own to say and not the one-way scatter's: a slip road
    /// (<see cref="RoadClass.Slip"/>).
    /// </param>
    /// <param name="over">Whether it is carried over what it crosses (<see cref="LayoutEdge.Over"/>).</param>
    public int Join(
        int from, int to, RoadClass roadClass, float curvature = 0f, Vector2[]? throughM = null,
        bool straight = false, int lanes = 1, bool unwalked = false, RoadFlow flow = RoadFlow.BothWays, bool over = false)
    {
        if (from == to) return -1;

        var runM = _nodeM[to] - _nodeM[from];
        if (runM.Length() < shortestRoadM) return -1;
        if (!water.Carries(_nodeM[from], _nodeM[to], roadClass, throughM)) return -1;
        if (!_joined.Add(from < to ? (from, to) : (to, from))) return -1;

        var road = new LayoutEdge(
            from, to, roadClass, curvature, flow, throughM ?? [], straight, lanes,
            unwalked || roadClass is RoadClass.Intercity or RoadClass.Slip, over);
        var (outOfFrom, outOfTo) = BearingsOf(road);
        if (!StandsSquareEnough(from, outOfFrom, roadClass) || !StandsSquareEnough(to, outOfTo, roadClass)
            || !JointTakes(from, outOfFrom, lanes) || !JointTakes(to, outOfTo, lanes)
            || !lines.CanLay(road, _nodeM, out var line))
        {
            _joined.Remove(from < to ? (from, to) : (to, from));
            return -1;
        }

        _armsAt[from].Add((outOfFrom, roadClass, lanes));
        _armsAt[to].Add((outOfTo, roadClass, lanes));
        _edges.Add(road);
        _lineOf.Add(line);
        lines.Keep(road, line);
        return _edges.Count - 1;
    }

    /// <summary>
    /// <b>One straight road from a node to another through each place given</b>, a piece between each two: a node the
    /// layout has, or a node of its own stood there — <b>every piece asked before any is laid</b>, and none laid unless all
    /// of them can be: each a road's length at least, on ground that carries it, joining no two nodes joined already,
    /// leaving every node it meets that has arms square enough to them (GEN-13), and its lines laid together
    /// (<see cref="CouldLay"/>). What <see cref="TownLinks.JoinWhatWasLeft"/> hangs the streets it crosses on.
    /// </summary>
    /// <param name="through">Each place in order: a node of the layout's, or −1 and where to stand one.</param>
    /// <returns>The node at each place, in order, or none where it was refused.</returns>
    public int[]? JoinThrough(int from, int to, IReadOnlyList<(int Node, Vector2 AtM)> through, RoadClass roadClass)
    {
        var nodeM = new List<Vector2>(_nodeM);
        var at = new int[through.Count + 2];
        (at[0], at[^1]) = (from, to);
        for (var place = 0; place < through.Count; place++)
        {
            var (node, atM) = through[place];
            if (node < 0)
            {
                if (!water.Holds(atM)) return null;

                node = nodeM.Count;
                nodeM.Add(atM);
            }

            at[place + 1] = node;
        }

        var edges = new LayoutEdge[through.Count + 1];
        var pairs = new HashSet<(int, int)>();
        for (var piece = 0; piece < edges.Length; piece++)
        {
            var (a, b) = (at[piece], at[piece + 1]);
            if (a == b || (nodeM[b] - nodeM[a]).Length() < shortestRoadM || !water.Carries(nodeM[a], nodeM[b], roadClass, null)) return null;

            var pair = a < b ? (a, b) : (b, a);
            if (_joined.Contains(pair) || !pairs.Add(pair)) return null;

            edges[piece] = new LayoutEdge(
                a, b, roadClass, 0f, RoadFlow.BothWays, [], Straight: true, Unwalked: roadClass == RoadClass.Intercity);
            var (outOfA, outOfB) = Bearings(nodeM[a], nodeM[b], 0f);
            var lanes = edges[piece].Lanes;
            if ((a < _nodeM.Count && (!StandsSquareEnough(a, outOfA, roadClass) || !JointTakes(a, outOfA, lanes)))
                || (b < _nodeM.Count && (!StandsSquareEnough(b, outOfB, roadClass) || !JointTakes(b, outOfB, lanes))))
            {
                return null;
            }

            // And its own two arms at each place it passes, which no arm of the layout's stands between yet.
            if (piece > 0)
            {
                var (_, cameIn) = Bearings(nodeM[at[piece - 1]], nodeM[a], 0f);
                if (MathF.Abs(MathF.IEEERemainder(outOfA - cameIn, MathF.Tau)) < armsApartMinRad) return null;
            }
        }

        var laid = new List<ArcSeg[]>(edges.Length);
        if (!CouldLay(nodeM, edges, [], laid)) return null;

        for (var node = _nodeM.Count; node < nodeM.Count; node++) Appended(nodeM[node]);
        for (var piece = 0; piece < edges.Length; piece++) Added(edges[piece], laid[piece]);

        return at[1..^1];
    }

    /// <summary>
    /// <b>Whether these roads could be laid together, without laying any of them</b> — what a stage that is
    /// about to change the shape of several at once has to know before it changes any (<see cref="Roundabouts"/>,
    /// <see cref="ThroughRoads"/>, GEN-19, GEN-51). <b>Asked of the nodes the change would leave</b>, because
    /// moving a node is what changes the lines.
    /// </summary>
    /// <param name="instead">The roads the new ones would stand in the place of, whose ground is theirs to take.</param>
    /// <param name="laid">Their lines, in the same order — <b>drawn once</b>, so that a caller which goes ahead has them.</param>
    public bool CouldLay(
        IReadOnlyList<Vector2> nodeM, ReadOnlySpan<LayoutEdge> edges, ReadOnlySpan<int> instead,
        List<ArcSeg[]> laid)
    {
        laid.Clear();
        for (var at = 0; at < edges.Length; at++)
        {
            if (!lines.CanLay(edges[at], nodeM, instead, out var line)) return false;

            // <b>And against the others in the batch</b> (GEN-49): none of them is standing yet, so the index
            // cannot answer for them — and a ring's arms all move at once and stop meeting at the node they
            // used to share, which is exactly the pair that would go unasked.
            for (var earlier = 0; earlier < at; earlier++)
            {
                if (!lines.Apart(line, edges[at], laid[earlier], edges[earlier])) return false;
            }

            laid.Add(line);
        }

        return true;
    }

    /// <summary>
    /// <b>Whether a line the caller drew itself is one the town can have</b> (GEN-49,
    /// <see cref="RoadLines.Clear"/>) — what a car park's bay has to ask, its line being laid where the kerb
    /// is rather than drawn here (GEN-53).
    /// </summary>
    public bool Clear(ArcSeg[] line, in LayoutEdge edge, ReadOnlySpan<int> instead) =>
        lines.Clear(line, edge, instead);

    /// <summary>
    /// <b>Which way a road leaves each of its two ends</b> — the tangent the carriageway is actually drawn
    /// on there and not the chord it is joined along (<see cref="RoadStage"/>). A straight leaves both ends
    /// on its own chord; an arc leaves each end turned half its own sweep off it, which is the whole of the
    /// difference between a circle and the polygon its nodes make.
    /// </summary>
    /// <remarks>
    /// <b>It is what GEN-13 is measured on.</b> Two pieces of one circle meeting at a node lie half a turn
    /// apart — a road running through it — where their chords stand at the polygon's own interior angle and
    /// would be refused for lying against each other.
    /// </remarks>
    static (float OutOfFrom, float OutOfTo) Bearings(Vector2 fromM, Vector2 toM, float curvature)
    {
        var runM = toM - fromM;
        var chordRad = Trig.Atan2(runM.Y, runM.X);
        if (MathF.Abs(curvature) <= 0f) return (chordRad, chordRad + MathF.PI);

        var radiusM = 1f / MathF.Abs(curvature);
        var halfRad = Trig.Asin(MathF.Min(1f, runM.Length() * 0.5f / radiusM)) * MathF.Sign(curvature);
        return (chordRad - halfRad, chordRad + halfRad + MathF.PI);
    }

    /// <summary>
    /// And the same for one of the layout's own roads. <b>A road that passes somewhere leaves each of its
    /// ends for the first place it passes</b> (<see cref="LayoutEdge.ThroughM"/>, GEN-51), which is the
    /// bearing GEN-13 is owed there — the far end it never points at would read as an arm that is not
    /// where the carriageway goes.
    /// </summary>
    (float OutOfFrom, float OutOfTo) BearingsOf(in LayoutEdge edge)
    {
        var fromM = _nodeM[edge.From];
        var toM = _nodeM[edge.To];
        if (edge.ThroughM.Length == 0) return Bearings(fromM, toM, edge.Curvature);

        var leaves = edge.ThroughM[0] - fromM;
        var arrives = edge.ThroughM[^1] - toM;
        return (Trig.Atan2(leaves.Y, leaves.X), Trig.Atan2(arrives.Y, arrives.X));
    }

    /// <summary>
    /// Whether an arm leaving on this bearing stands far enough off every arm already at the node — or
    /// straight through one of them, which is a road passing a junction rather than meeting one.
    /// </summary>
    /// <remarks>
    /// <b>A slip road may lie nearer an intercity road's arm, or another slip's</b> (GEN-13, GEN-68): nothing is laid across
    /// either — no walk, no crossing, no bar on the intercity road — and its node stands its arms out until the slip's lane
    /// clears the intercity road's carriageway (<see cref="SimConfig.SlipStandoffM"/>).
    /// </remarks>
    bool StandsSquareEnough(int node, float outwardRad, RoadClass roadClass)
    {
        foreach (var (armRad, armClass, _) in _armsAt[node])
        {
            // Half a turn apart is a road running through the node rather than two arms lying together, and
            // that is the one wide angle a junction is made of.
            var leastRad = Glancing(roadClass, armClass) ? slipArmsApartMinRad : armsApartMinRad;
            if (MathF.Abs(MathF.IEEERemainder(outwardRad - armRad, MathF.Tau)) < leastRad) return false;
        }

        return true;
    }

    /// <summary>
    /// Whether an arm leaving on this bearing with these lanes each way may meet the node: any may, but at a bridgehead that
    /// is no junction (<see cref="NodeSizes.Joint"/>), which takes its span and the one road carrying it on, lane onto its
    /// own lane, and nothing else (GEN-14a) — the handover between them has no room for a lane to end in.
    /// </summary>
    bool JointTakes(int node, float outwardRad, int lanes)
    {
        if (!sizes.IsJoint(_nodeM[node])) return true;

        var arms = _armsAt[node];
        return arms.Count == 0
               || (arms.Count == 1 && arms[0].Lanes == lanes
                   && MathF.Abs(MathF.IEEERemainder(outwardRad - arms[0].Rad - MathF.PI, MathF.Tau)) <= ConnectionPoints.CarriesOnWithinRad);
    }

    /// <summary>Whether two arms of these classes may lie as near each other as a slip road leaves its intercity road.</summary>
    static bool Glancing(RoadClass one, RoadClass other) =>
        (one == RoadClass.Slip && other is RoadClass.Slip or RoadClass.Intercity)
        || (other == RoadClass.Slip && one == RoadClass.Intercity);

    /// <summary>
    /// Which way one road is driven, for the pass that settles that and changes nothing else
    /// (<see cref="OneWayStreets"/>). <b>It is not a re-offer</b>: a road's ends, its class and its shape
    /// are what they were, and only the traffic on it has changed.
    /// </summary>
    public void RunsOneWay(int edge, RoadFlow flow) => _edges[edge] = _edges[edge] with { Flow = flow };

    /// <summary>
    /// Which way one of a node's roads leaves it, as the bearing the carriageway is drawn on there
    /// (<see cref="Bearings"/>).
    /// </summary>
    public float OutwardRad(int edge, int node)
    {
        var (outOfFrom, outOfTo) = BearingsOf(_edges[edge]);
        return _edges[edge].From == node ? outOfFrom : outOfTo;
    }

    /// <summary>
    /// <b>Which of two classes of road the other gives way to</b>: a deck cannot move, an arterial's line is
    /// the town's, and a street is what bends to meet either. It is the order the nodes are placed in
    /// (<see cref="AddNode"/>) and the order the roads are laid in, and it is what a road joined out of two
    /// classes comes out as (<see cref="Gen.ThroughRoads"/>, GEN-16).
    /// </summary>
    public static int Precedence(RoadClass roadClass) => roadClass switch
    {
        RoadClass.Bridge => 3,
        RoadClass.Intercity => 2,
        RoadClass.Arterial or RoadClass.Slip => 1,
        _ => 0,
    };

    /// <summary>The roads at one node, in the order they leave it.</summary>
    public List<int> ArmsAt(int node)
    {
        var arms = new List<int>();
        for (var edge = 0; edge < _edges.Count; edge++)
        {
            if (_edges[edge].From == node || _edges[edge].To == node) arms.Add(edge);
        }

        arms.Sort((a, b) => Wrapped(OutwardRad(a, node)).CompareTo(Wrapped(OutwardRad(b, node))));
        return arms;
    }

    /// <summary>
    /// <b>One node opened out into a ring driven one way round it</b> (GEN-19). Each of its roads is cut
    /// back to its own point on a circle of <paramref name="radiusM"/> — <b>the point on that road's own
    /// bearing</b>, so nothing bends to meet the ring and every arm arrives square to it — and the points
    /// are joined into a closed circle of one-way arcs, in the direction <paramref name="curvature"/> says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing is laid and taken back</b> (GEN-19, GEN-10): every arm the ring moves and every piece of the
    /// ring itself is drawn first, and where one of them cannot be laid the node stays the junction it was.
    /// A half-laid ring is a worse town than the junction it replaced, so the answer is the whole circle or
    /// none of it.
    /// </para>
    /// <para>
    /// <b>The node itself is left standing with nothing at it</b> rather than deleted, so that every road
    /// and every other node keeps the number it had while the rest of the town is still being rung out;
    /// <see cref="PruneTheDeadEnds"/> is what drops the husks afterwards.
    /// </para>
    /// </remarks>
    /// <returns>Whether the ring was laid.</returns>
    public bool RingOut(int node, float radiusM, float curvature)
    {
        var arms = ArmsAt(node);
        if (arms.Count < 3) return false;

        // Round the circle the way the traffic goes: a right-hand turn is the way the angles increase.
        if (curvature < 0f) arms.Reverse();

        var centreM = _nodeM[node];
        var nodeM = new List<Vector2>(_nodeM);
        var onTheRing = new int[arms.Count];
        for (var arm = 0; arm < arms.Count; arm++)
        {
            onTheRing[arm] = nodeM.Count;
            nodeM.Add(centreM + (Heading.Unit(OutwardRad(arms[arm], node)) * radiusM));
        }

        var edges = new List<LayoutEdge>(_edges);
        var lineOf = new List<ArcSeg[]>(_lineOf);
        var changed = new List<LayoutEdge>(arms.Count * 2);
        for (var arm = 0; arm < arms.Count; arm++)
        {
            var edge = edges[arms[arm]];
            edges[arms[arm]] = edge.From == node
                ? edge with { From = onTheRing[arm] }
                : edge with { To = onTheRing[arm] };
            changed.Add(edges[arms[arm]]);
        }

        for (var arm = 0; arm < arms.Count; arm++)
        {
            var piece = new LayoutEdge(
                onTheRing[arm], onTheRing[(arm + 1) % arms.Count], RoadClass.Roundabout, curvature,
                RoadFlow.WithTheRoad, []);
            edges.Add(piece);
            changed.Add(piece);
        }

        // The arms are laid where they already reach and the ring inside the ground they gave up, so what the
        // circle has to clear is every road but the arms it is made of.
        var drawn = new List<ArcSeg[]>(changed.Count);
        if (!CouldLay(nodeM, CollectionsMarshal.AsSpan(changed), CollectionsMarshal.AsSpan(arms), drawn))
        {
            return false;
        }

        for (var arm = 0; arm < arms.Count; arm++) lineOf[arms[arm]] = drawn[arm];
        for (var piece = arms.Count; piece < drawn.Count; piece++) lineOf.Add(drawn[piece]);

        Rebuilt(nodeM, edges, lineOf);
        return true;
    }

    static float Wrapped(float radians) => radians - (MathF.Tau * MathF.Floor(radians / MathF.Tau));

    /// <summary>How many roads meet at each node, which is what decides a junction's radius and whether it may be lit.</summary>
    public int[] Arms()
    {
        var arms = new int[_nodeM.Count];
        foreach (var edge in _edges)
        {
            arms[edge.From]++;
            arms[edge.To]++;
        }

        return arms;
    }

    /// <summary>The pieces the roads laid so far make: each node's cluster (<see cref="Clusters"/>).</summary>
    public int[] Pieces()
    {
        var root = Clusters.Apart(_nodeM.Count);
        foreach (var edge in _edges) Clusters.Union(root, edge.From, edge.To);

        return root;
    }

    /// <summary>
    /// <b>Which piece is the town</b> (GEN-5, GEN-63): the one with the most roads of those holding an intercity road, and
    /// the one with the most roads where none holds one — or −1 where there are no roads. The one answer the repair
    /// (<see cref="TownLinks.JoinWhatWasLeft"/>) and the deletion (<see cref="KeepTheLargestComponent"/>) both read, so a
    /// piece the repair left out never outgrows the town and takes its intercity roads with it.
    /// </summary>
    public int MainPiece(int[] root)
    {
        var roads = new int[_nodeM.Count];
        var outside = new bool[_nodeM.Count];
        foreach (var edge in _edges)
        {
            var piece = Clusters.Find(root, edge.From);
            roads[piece]++;
            outside[piece] |= edge.Class == RoadClass.Intercity;
        }

        var main = -1;
        for (var piece = 0; piece < roads.Length; piece++)
        {
            if (roads[piece] == 0) continue;
            if (main < 0 || (outside[piece], roads[piece]).CompareTo((outside[main], roads[main])) > 0) main = piece;
        }

        return main;
    }

    /// <summary>
    /// Drops everything not joined to the town's piece (<see cref="MainPiece"/>), nodes and roads together, and renumbers
    /// what is left. <b>A node nothing reaches is deleted and not connected</b>: a link drawn to reach it
    /// would cross whatever stands in the way, and a road crossing another road where no junction is would be
    /// worse than the island it fixed.
    /// </summary>
    public void KeepTheLargestComponent()
    {
        if (_edges.Count == 0) return;

        var lineOf = new List<ArcSeg[]>(_edges.Count);

        var root = Pieces();
        var largest = MainPiece(root);

        var moved = new int[_nodeM.Count];
        var kept = new List<Vector2>(_nodeM.Count);
        for (var node = 0; node < _nodeM.Count; node++)
        {
            if (Clusters.Find(root, node) != largest)
            {
                moved[node] = -1;
                continue;
            }

            moved[node] = kept.Count;
            kept.Add(_nodeM[node]);
        }

        var edges = new List<LayoutEdge>(_edges.Count);
        for (var road = 0; road < _edges.Count; road++)
        {
            var edge = _edges[road];
            if (moved[edge.From] < 0 || moved[edge.To] < 0) continue;

            edges.Add(edge with { From = moved[edge.From], To = moved[edge.To] });
            lineOf.Add(_lineOf[road]);
        }

        Rebuilt(kept, edges, lineOf);
    }

    /// <summary>
    /// Drops every road that leads nowhere, and keeps dropping until none does — with the nodes no road is
    /// left at, which is a lattice point whose every arm was cut at the water's edge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A junction of one arm is a dead end</b> (GEN-5a, TER-5a), and it is the one junction a town has to size
    /// around a turning circle rather than around a crossing. The road stage lays every junction it is given
    /// as the disc its arms need, so a dead end reaching this town would be a place a car can drive into and
    /// never leave — and the arms that fall out of a lattice are the ends of streets nobody planned, not
    /// cul-de-sacs anybody laid.
    /// </para>
    /// <para>
    /// <b>Deleted rather than reached round</b> (GEN-8), the same as a piece joined to nothing: growing an
    /// arm on to close the loop would cross whatever cut the street short in the first place. Deleting one
    /// road can leave the junction behind it standing on one arm, so this runs to a fixed point and what is
    /// left is a town where every road runs between two places worth being at.
    /// </para>
    /// </remarks>
    public void PruneTheDeadEnds()
    {
        var edgesAt = new List<int>[_nodeM.Count];
        for (var node = 0; node < edgesAt.Length; node++) edgesAt[node] = [];
        for (var edge = 0; edge < _edges.Count; edge++)
        {
            edgesAt[_edges[edge].From].Add(edge);
            edgesAt[_edges[edge].To].Add(edge);
        }

        var arms = Arms();
        var dropped = new bool[_edges.Count];
        var leaves = new Stack<int>();
        for (var node = 0; node < arms.Length; node++)
        {
            if (IsALeaf(node, arms, edgesAt, dropped)) leaves.Push(node);
        }

        while (leaves.Count > 0)
        {
            var node = leaves.Pop();
            if (!IsALeaf(node, arms, edgesAt, dropped)) continue;

            foreach (var edge in edgesAt[node])
            {
                if (dropped[edge]) continue;

                dropped[edge] = true;
                arms[node]--;
                var beyond = _edges[edge].From == node ? _edges[edge].To : _edges[edge].From;
                arms[beyond]--;
                if (IsALeaf(beyond, arms, edgesAt, dropped)) leaves.Push(beyond);
            }
        }

        var moved = new int[_nodeM.Count];
        var kept = new List<Vector2>(_nodeM.Count);
        for (var node = 0; node < _nodeM.Count; node++)
        {
            if (arms[node] == 0)
            {
                moved[node] = -1;
                continue;
            }

            moved[node] = kept.Count;
            kept.Add(_nodeM[node]);
        }

        var edges = new List<LayoutEdge>(_edges.Count);
        var lineOf = new List<ArcSeg[]>(_edges.Count);
        for (var edge = 0; edge < _edges.Count; edge++)
        {
            if (dropped[edge]) continue;

            edges.Add(_edges[edge] with { From = moved[_edges[edge].From], To = moved[_edges[edge].To] });
            lineOf.Add(_lineOf[edge]);
        }

        Rebuilt(kept, edges, lineOf);
    }

    /// <summary>
    /// <b>Whether a node is somewhere a road merely stops</b> (GEN-5a): one that carries a single arm, or
    /// one a car can arrive at and not leave.
    /// </summary>
    /// <remarks>
    /// <b>The second is a one-way street's doing and is not a count of arms</b> (GEN-18, TER-5f): a car may
    /// not turn round in the road, so a node whose only way out is the road the car came in on is a node it
    /// is stuck at — and a lane with no movement off it is a hole in the drivable region rather than a
    /// corner nobody takes. It is asked here rather than of the lanes because the layout is where a road is
    /// deleted and a lane is the shape of one.
    /// </remarks>
    bool IsALeaf(int node, int[] arms, List<int>[] edgesAt, bool[] dropped)
    {
        if (arms[node] <= 0) return false;

        // <b>Nor is a node a road runs off the map at</b> (GEN-2b, GEN-63): the road carries on past the edge, and the
        // node is only where the map stops showing it.
        if (sizes.RunsOff(_nodeM[node])) return false;

        // <b>A node on a ring is never one a road merely stops at</b> (GEN-19): a roundabout is one junction
        // laid out as a circle, so pruning one of its nodes leaves the rest of them standing a radius apart
        // with nothing to say they were ever one place (GEN-16).
        foreach (var edge in edgesAt[node])
        {
            if (!dropped[edge] && _edges[edge].Class == RoadClass.Roundabout) return false;
        }

        if (arms[node] == 1) return true;

        var leaving = 0;
        var arriving = 0;
        var onlyWayOut = -1;
        var onlyWayIn = -1;
        foreach (var edge in edgesAt[node])
        {
            if (dropped[edge]) continue;

            var outward = Leaves(node, edge);
            var inward = Arrives(node, edge);

            if (outward)
            {
                leaving++;
                onlyWayOut = edge;
            }

            if (inward)
            {
                arriving++;
                onlyWayIn = edge;
            }
        }

        if (leaving == 0 || arriving == 0) return true;

        // <b>One way out is one every car that arrives has to take</b>, so it may not also be a road they
        // arrive on — a car that did would be turning round in the road (TER-5f). And the same read the
        // other way: one way in that is also a way out is a lane nothing can be driven onto.
        return (leaving == 1 && Arrives(node, onlyWayOut)) || (arriving == 1 && Leaves(node, onlyWayIn));
    }

    /// <summary>Whether a car can arrive at a node on this road, which is the flow read from that node's end.</summary>
    public bool Arrives(int node, int edge) =>
        _edges[edge].From == node
            ? _edges[edge].Flow != RoadFlow.WithTheRoad
            : _edges[edge].Flow != RoadFlow.AgainstTheRoad;

    /// <summary>And whether one can leave on it.</summary>
    public bool Leaves(int node, int edge) =>
        _edges[edge].From == node
            ? _edges[edge].Flow != RoadFlow.AgainstTheRoad
            : _edges[edge].Flow != RoadFlow.WithTheRoad;

    /// <summary>
    /// The layout on a new set of nodes with every road carried over, which is what a deletion or a join
    /// needs: nothing has moved, so nothing has to pass <see cref="Join"/> again.
    /// </summary>
    /// <remarks>
    /// <b>The bearings it fills are each road's own</b> (<see cref="BearingsOf"/>), so a road
    /// <see cref="Gen.ThroughRoads"/> joined out of several leaves its two arms for the places it passes
    /// rather than for the far end it never points at.
    /// </remarks>
    /// <param name="lineOf">
    /// The line each of those roads is laid as, in the same order. <b>Every road brings its own</b>: what is
    /// carried over was laid when it was offered and nothing here has moved, so a line redrawn would be the
    /// same line — and a road whose shape really does change is offered through <see cref="Join"/> instead.
    /// </param>
    public void Rebuilt(List<Vector2> nodeM, List<LayoutEdge> edges, List<ArcSeg[]> lineOf)
    {
        _nodeM.Clear();
        _nodeM.AddRange(nodeM);
        _nodesIn = null;
        _edges.Clear();
        _edges.AddRange(edges);
        _lineOf.Clear();
        _lineOf.AddRange(lineOf);
        _joined.Clear();
        _armsAt.Clear();
        lines.Reset(_edges, _lineOf);
        for (var node = 0; node < _nodeM.Count; node++) _armsAt.Add([]);
        foreach (var edge in _edges) Joined(edge);
    }

    /// <summary>
    /// <b>Roads on nodes of their own, joined to nothing already laid</b> — a car park's bays (GEN-53), done
    /// where the layout stands. The nodes are appended first and the roads after them, in the order given.
    /// </summary>
    public void Stand(ReadOnlySpan<Vector2> nodesM, ReadOnlySpan<LayoutEdge> roads, ReadOnlySpan<ArcSeg[]> roadLines)
    {
        foreach (var atM in nodesM) Appended(atM);
        for (var road = 0; road < roads.Length; road++) Added(roads[road], roadLines[road]);
    }

    /// <summary>
    /// <b>A road the town had, put back as it stood</b>, its line and all, between nodes it still has — what
    /// <see cref="Gen.ThroughRoads"/> falls back on when a piece of a run cannot be offered again on the town its joined
    /// runs left. It passed <see cref="Join"/> once on these very nodes, and deleting it would part the town (GEN-5).
    /// </summary>
    public void Restore(in LayoutEdge edge, ArcSeg[] line) => Added(edge, line);

    void Appended(Vector2 atM)
    {
        _nodeM.Add(atM);
        _nodesIn?.Add(atM);
        _armsAt.Add([]);
    }

    void Added(in LayoutEdge edge, ArcSeg[] line)
    {
        _edges.Add(edge);
        _lineOf.Add(line);
        lines.Keep(edge, line);
        Joined(edge);
    }

    /// <summary>A road's two ends marked joined, and the bearing it leaves each of them on kept there.</summary>
    void Joined(in LayoutEdge edge)
    {
        _joined.Add(edge.From < edge.To ? (edge.From, edge.To) : (edge.To, edge.From));
        var (outOfFrom, outOfTo) = BearingsOf(edge);
        _armsAt[edge.From].Add((outOfFrom, edge.Class, edge.Lanes));
        _armsAt[edge.To].Add((outOfTo, edge.Class, edge.Lanes));
    }
}
