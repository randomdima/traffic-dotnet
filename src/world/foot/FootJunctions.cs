using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// <b>The places a walk chooses between, drawn off the ends of the roads</b> (WLK-1a, WLK-2, WLK-3): a pair
/// of pedestrian nodes at each end of each road, one either side of it, and the junctions the pairs standing
/// near one another are merged into.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists only while a town is being laid.</b> Nothing on a tick holds one: what survives is the
/// points it hands its ways over at (<see cref="FootConnectors"/>), exactly as a connection point survives
/// only as the lane end it drew (<see cref="ConnectionPoints"/>).
/// </para>
/// <para>
/// <b>Everything here is in the road end's own outward frame</b> — the way the road leaves its junction —
/// rather than in the road's own direction of drawing. A node's <em>hand</em> is which side of that
/// outward bearing it stands on, so the two ends of one road name their sides independently and the same
/// physical corner is named the same way whichever way round either road happened to be drawn
/// (<see cref="Node"/>).
/// </para>
/// <para>
/// <b>A bay carries none, and neither does the junction it hangs off</b> (GEN-53, WLK-2). A car park's bay
/// is ground a car is put down on rather than a street with a frontage, and the node its rank was cut into
/// the street at is not a place the street stops (<see cref="JunctionArms.Special"/>) — so no node stands
/// there, nothing is handed over there, and <b>the walk down the street runs straight through it</b>
/// (<see cref="DownTheRoad"/>) as the one stretch of course that goes round the mouth of the rank.
/// </para>
/// </remarks>
internal sealed class FootJunctions
{
    /// <summary>What a road end that stands at no junction answers, and what a node off it belongs to.</summary>
    public const int NoJunction = -1;

    /// <summary>What a node with nothing round the corner from it answers — a node at no junction at all.</summary>
    public const int NoNode = -1;

    readonly bool[] _standsAtEnd;
    readonly Vector2[] _outwardUnitAtEnd;
    readonly Vector2[] _onTheLineAtEnd;
    readonly float[] _alongM;
    readonly float[] _asideM;
    readonly Vector2[] _atM;
    readonly int[] _junctionOfNode;
    readonly Vector2[] _hubM;
    readonly int[] _wedgeNeighbour;
    readonly int[] _downTheRoad;

    FootJunctions(
        bool[] standsAtEnd, Vector2[] outwardUnitAtEnd, Vector2[] onTheLineAtEnd, float[] alongM,
        float[] asideM, Vector2[] atM, int[] junctionOfNode, Vector2[] hubM, int[] wedgeNeighbour,
        int[] downTheRoad)
    {
        _standsAtEnd = standsAtEnd;
        _outwardUnitAtEnd = outwardUnitAtEnd;
        _onTheLineAtEnd = onTheLineAtEnd;
        _alongM = alongM;
        _asideM = asideM;
        _atM = atM;
        _junctionOfNode = junctionOfNode;
        _hubM = hubM;
        _wedgeNeighbour = wedgeNeighbour;
        _downTheRoad = downTheRoad;
    }

    /// <summary>
    /// The node standing on one hand of one road end (<see cref="JunctionArms.End"/>): <c>+1</c> to the right
    /// of the bearing the road leaves its junction on, <c>-1</c> to the left.
    /// </summary>
    public static int Node(int end, int hand) => (end * 2) + (hand > 0 ? 1 : 0);

    public int NodeCount => _atM.Length;

    /// <summary>How many road ends the town has, which is two per road and a pair of nodes at each.</summary>
    public int EndCount => _standsAtEnd.Length;

    /// <summary>How many pedestrian junctions the merge left, which is what a walk has places to choose at.</summary>
    public int JunctionCount => _hubM.Length;

    /// <summary>
    /// <b>Whether a road end carries a pair of nodes at all</b> (WLK-2): every end of a road that is neither
    /// a bay nor a roundabout's ring, standing at a place the street really stops rather than at a junction
    /// cut into one that carries on past it (<see cref="JunctionArms.Special"/>).
    /// </summary>
    public bool StandsAt(int end) => _standsAtEnd[end];

    /// <summary>The bearing the road leaves its junction on, read where the nodes stand rather than at the node's centre.</summary>
    public Vector2 OutwardUnitAt(int end) => _outwardUnitAtEnd[end];

    /// <summary>The point on the road's own line the end's two nodes straddle.</summary>
    public Vector2 OnTheLineAt(int end) => _onTheLineAtEnd[end];

    /// <summary>How far along its road that point stands, which is where the walk beside it begins.</summary>
    public float AlongM(int end) => _alongM[end];

    /// <summary>And how far off the road's own line the nodes stand: half its carriageway and the figure beyond it.</summary>
    public float AsideM(int end) => _asideM[end];

    /// <summary>Where one node stands, before anything is merged.</summary>
    public Vector2 AtM(int node) => _atM[node];

    /// <summary>The pedestrian junction a node was merged into.</summary>
    public int JunctionOf(int node) => _junctionOfNode[node];

    /// <summary>And where that junction is: the middle of the nodes merged into it, which is one node's own place where nothing merged.</summary>
    public Vector2 HubM(int node) => _hubM[_junctionOfNode[node]];

    /// <summary>
    /// <b>The node round the corner from this one</b> — the other node facing into the wedge this one bounds
    /// (WLK-3): the arms at a junction are taken in the order their bearings turn, each consecutive pair
    /// bounds one wedge, and a node on the right of one arm faces the node on the left of the next.
    /// </summary>
    /// <remarks>
    /// <b>One rule and no cases</b>: a five-armed node has five wedges, a two-armed node two — which is a
    /// street carried straight through a bend — and a one-armed node one, which is the head of a dead end,
    /// where a node's neighbour is the other node of its own end. A node at no junction has none.
    /// </remarks>
    public int WedgeNeighbour(int node) => _wedgeNeighbour[node];

    /// <summary>
    /// <b>The node at the far end of the walk down this node's own side of the street</b> (WLK-1a, WLK-2):
    /// the node on the same physical side at the other end of the road, and <b>at the far end of the road
    /// after it wherever the road runs into a junction cut into the street</b> — a car park's, which stands
    /// no node, so the street's own walk carries through it.
    /// </summary>
    /// <remarks>
    /// <b>The side is physical and the hand is not.</b> A node's hand is read off the bearing its road
    /// leaves the junction on, so the same side of the street names the other hand at the road's far end and
    /// the first hand again on the road across the cut — the two arms of a cut leaving on opposite bearings
    /// (GEN-52). <b>A chain that comes back to the end it set off from is a street closed by cuts alone</b>
    /// and reaches no node, every end having exactly one successor.
    /// </remarks>
    public int DownTheRoad(int node) => _downTheRoad[node];

    /// <summary>
    /// Lays every pedestrian node in the town and merges the ones standing within
    /// <see cref="RoadFigures.FootNodeMergeM"/> of one another (WLK-2, WLK-3).
    /// </summary>
    public static FootJunctions Lay(CityPlan plan, SimConfig config)
    {
        var roads = plan.Roads;
        var ends = roads.Count * 2;
        var standsAtEnd = new bool[ends];
        var carriesAWalk = new bool[roads.Count];
        var outwardUnitAtEnd = new Vector2[ends];
        var onTheLineAtEnd = new Vector2[ends];
        var alongM = new float[ends];
        var asideM = new float[ends];
        var atM = new Vector2[ends * 2];
        var junctionOfEnd = new int[ends];
        Array.Fill(junctionOfEnd, NoJunction);

        var arms = JunctionArms.Of(plan);
        var backM = config.Road.FootNodeBackM;
        for (var road = 0; road < roads.Count; road++)
        {
            var line = roads.SegmentsOf(road);
            if (line.Length == 0) continue;

            // <b>A bay carries no walk and neither does a roundabout's ring</b> (WLK-2, GEN-53, GEN-19):
            // one is ground a car is put down on and the other a carriageway with no frontage on either
            // hand. <b>The ring's arms are ordinary</b> — an arm really does meet the ring, so the corner
            // it makes is a corner — which is why this is asked of the road and not of its junction.
            // <b>Both are still arms of the junctions they meet</b>, and are taken as far as their bearing
            // so the wedges either side of them are known not to be corners (<see cref="RoundTheCorner"/>).
            carriesAWalk[road] = !roads.IsABay(road) && !arms.Circulates(road);

            var lengthM = Spline.TotalLengthM(line);
            var halfWidthM = roads.WidthM[road] * 0.5f;
            var offTheLineM = halfWidthM + config.Road.FootNodeAsideM;

            // <b>Never past the middle of the road</b>: a street shorter than two setbacks would otherwise
            // stand its two ends' nodes the wrong way round, and the walk between them would run backwards.
            var middleM = lengthM * 0.5f;
            for (var atTo = 0; atTo < 2; atTo++)
            {
                var end = JunctionArms.End(road, atTo == 1);
                var junction = atTo == 1 ? roads.ToJunction[road] : roads.FromJunction[road];

                var standsAtM = atTo == 1 ? MathF.Max(lengthM - backM, middleM) : MathF.Min(backM, middleM);
                var at = Spline.SampleAt(line, standsAtM);
                var outward = atTo == 1 ? -at.Direction : at.Direction;
                var right = Heading.RightOf(outward);

                outwardUnitAtEnd[end] = outward;

                // <b>A bay is not an arm of the junction it hangs off</b> (<see cref="JunctionArms"/>), so
                // it bounds no wedge: it is a stub ending inside one, and the boundary runs round its mouth
                // and back. <b>A ring is an arm</b> — it carries no walk and still divides the wedge, the
                // way between its two sides being the way round the town.
                if (!roads.IsABay(road)) junctionOfEnd[end] = junction;

                // <b>A junction cut into a street stands no node</b> (WLK-2): a rank of bays hung off a
                // street that carries on past it is a hole in the pavement the walk goes round rather than
                // a corner it arrives at, so there is nothing to cross at, nothing to turn through and no
                // place — the street's own walk runs through the mouth in one stretch
                // (<see cref="DownTheStreet"/>).
                if (!carriesAWalk[road] || (junction >= 0 && arms.Special(junction))) continue;

                standsAtEnd[end] = true;
                onTheLineAtEnd[end] = at.PositionM;
                alongM[end] = standsAtM;
                asideM[end] = offTheLineM;
                atM[Node(end, -1)] = at.PositionM - (right * offTheLineM);
                atM[Node(end, +1)] = at.PositionM + (right * offTheLineM);
            }
        }

        var (junctionOfNode, hubM) = Merged(standsAtEnd, atM, config.Road.FootNodeMergeM);
        return new FootJunctions(
            standsAtEnd, outwardUnitAtEnd, onTheLineAtEnd, alongM, asideM, atM, junctionOfNode, hubM,
            RoundTheCorner(plan.Junctions.Count, standsAtEnd, junctionOfEnd, outwardUnitAtEnd),
            DownTheStreet(standsAtEnd, carriesAWalk, junctionOfEnd, arms));
    }

    /// <summary>
    /// <b>The node at the far end of each node's own walk down the street</b> (<see cref="DownTheRoad"/>):
    /// the far end of its road, carried on across every junction cut into the street (GEN-52) until an end
    /// that stands a node.
    /// </summary>
    /// <remarks>
    /// <b>Across a cut junction and across nothing else.</b> A bend and a dead end fork nothing either
    /// (<see cref="JunctionArms.Forks"/>) and are ordinary corners of a street: they stand their nodes, so
    /// the chain stops at them on the first reading and no rule about them is needed here.
    /// <para>
    /// <b>It terminates on a closed street.</b> Each end has exactly one successor and exactly one
    /// predecessor, so a chain that does not run out reaches the end it set off from rather than circling
    /// some other cycle.
    /// </para>
    /// </remarks>
    static int[] DownTheStreet(
        bool[] standsAtEnd, bool[] carriesAWalk, int[] junctionOfEnd, JunctionArms arms)
    {
        var reached = new int[standsAtEnd.Length * 2];
        Array.Fill(reached, NoNode);

        for (var node = 0; node < reached.Length; node++)
        {
            var from = node / 2;
            if (!standsAtEnd[from]) continue;

            var at = from;
            var side = node % 2 == 1 ? +1 : -1;
            while (true)
            {
                var far = JunctionArms.End(JunctionArms.Road(at), !JunctionArms.AtTo(at));
                side = -side;
                if (standsAtEnd[far])
                {
                    reached[node] = Node(far, side);
                    break;
                }

                var across = arms.Across(junctionOfEnd[far], far);
                if (across == JunctionArms.NoEnd || !carriesAWalk[JunctionArms.Road(across)]) break;

                at = across;
                side = -side;
                if (at == from) break;
            }
        }

        return reached;
    }

    /// <summary>
    /// <b>The node facing each node across the wedge it bounds</b> (<see cref="WedgeNeighbour"/>), from the
    /// road ends at each junction <b>sorted by the bearing their roads leave on</b> — which is the whole of
    /// what makes two consecutive ends the two sides of one wedge.
    /// </summary>
    /// <remarks>
    /// <b>Every arm is taken and not only the ones that stand a node</b>, and a wedge with an arm on either
    /// side of it that carries no walk is no corner. An arm's mouth is a hole in the ground the walk goes
    /// round, so a wedge reaching over one does not bound a corner at all: at a roundabout's arm, the two
    /// ends of the ring stand between the arm's own two sides, and a walk paired across them would set off
    /// round the outside of the whole town to get from one side of the arm to the other.
    /// <para>
    /// <b>A cut junction pairs nothing</b>, neither of its two arms standing a node (WLK-2): what runs round
    /// the mouth of the rank is the street's own walk (<see cref="DownTheStreet"/>) rather than a way from
    /// one side of a wedge to the other.
    /// </para>
    /// </remarks>
    static int[] RoundTheCorner(
        int junctions, bool[] standsAtEnd, int[] junctionOfEnd, Vector2[] outwardUnitAtEnd)
    {
        var neighbour = new int[standsAtEnd.Length * 2];
        Array.Fill(neighbour, NoNode);

        var offsets = new int[junctions + 1];
        for (var end = 0; end < standsAtEnd.Length; end++)
        {
            if (junctionOfEnd[end] >= 0) offsets[junctionOfEnd[end] + 1]++;
        }

        for (var junction = 0; junction < junctions; junction++) offsets[junction + 1] += offsets[junction];

        var ends = new int[offsets[^1]];
        var cursor = (int[])offsets.Clone();
        for (var end = 0; end < standsAtEnd.Length; end++)
        {
            if (junctionOfEnd[end] >= 0) ends[cursor[junctionOfEnd[end]]++] = end;
        }

        var byBearing = Comparer<int>.Create(
            (one, other) => Bearing(outwardUnitAtEnd[one]).CompareTo(Bearing(outwardUnitAtEnd[other])));

        for (var junction = 0; junction < junctions; junction++)
        {
            var from = offsets[junction];
            var count = offsets[junction + 1] - from;
            if (count == 0) continue;

            Array.Sort(ends, from, count, byBearing);
            for (var at = 0; at < count; at++)
            {
                // The wedge opens to the right of the first arm and to the left of the second, so the two
                // nodes facing into it are the pair that is joined — where both of those arms stand one.
                var one = ends[from + at];
                var next = ends[from + ((at + 1) % count)];
                if (!standsAtEnd[one] || !standsAtEnd[next]) continue;

                neighbour[Node(one, +1)] = Node(next, -1);
                neighbour[Node(next, -1)] = Node(one, +1);
            }
        }

        return neighbour;

        static float Bearing(Vector2 unit) => MathF.Atan2(unit.Y, unit.X);
    }

    /// <summary>
    /// <b>The nodes standing within the merge distance of one another, made one pedestrian junction</b>
    /// (WLK-3), and the middle of each set.
    /// </summary>
    /// <remarks>
    /// <b>Transitive, and asked of the nodes rather than of the junction they came off.</b> What makes two
    /// corners one place is that a walk crosses between them in a stride, which is a fact about where they
    /// stand — so two arms of one junction merge exactly as two junctions laid a few metres apart do, and
    /// nothing has to decide which of those a run of near neighbours is.
    /// </remarks>
    static (int[] JunctionOfNode, Vector2[] HubM) Merged(bool[] standsAtEnd, Vector2[] atM, float mergeM)
    {
        var owner = new int[atM.Length];
        for (var node = 0; node < owner.Length; node++) owner[node] = node;

        // Bucketed at the merge distance, so the search for a node's neighbours is the nine cells round it
        // rather than the town: a city lays thousands of these and the pairing is otherwise its square.
        var cells = new Dictionary<(int X, int Y), List<int>>();
        for (var node = 0; node < atM.Length; node++)
        {
            if (!standsAtEnd[node / 2]) continue;

            var cell = Cell(atM[node], mergeM);
            if (!cells.TryGetValue(cell, out var here)) cells[cell] = here = [];

            here.Add(node);
        }

        foreach (var (cell, here) in cells)
        {
            for (var y = -1; y <= 1; y++)
            {
                for (var x = -1; x <= 1; x++)
                {
                    if (!cells.TryGetValue((cell.X + x, cell.Y + y), out var there)) continue;

                    foreach (var node in here)
                    {
                        foreach (var other in there)
                        {
                            if (other <= node) continue;
                            if (Vector2.DistanceSquared(atM[node], atM[other]) <= mergeM * mergeM)
                            {
                                Join(owner, node, other);
                            }
                        }
                    }
                }
            }
        }

        var junctionOfNode = new int[atM.Length];
        Array.Fill(junctionOfNode, NoJunction);
        var hubM = new List<Vector2>();
        var standing = new List<int>();
        for (var node = 0; node < atM.Length; node++)
        {
            if (!standsAtEnd[node / 2]) continue;

            var root = Root(owner, node);
            if (junctionOfNode[root] < 0)
            {
                junctionOfNode[root] = hubM.Count;
                hubM.Add(Vector2.Zero);
                standing.Add(0);
            }

            junctionOfNode[node] = junctionOfNode[root];
            hubM[junctionOfNode[node]] += atM[node];
            standing[junctionOfNode[node]]++;
        }

        for (var junction = 0; junction < hubM.Count; junction++) hubM[junction] /= standing[junction];

        return (junctionOfNode, [.. hubM]);
    }

    static (int X, int Y) Cell(Vector2 atM, float sizeM) =>
        ((int)MathF.Floor(atM.X / sizeM), (int)MathF.Floor(atM.Y / sizeM));

    static int Root(int[] owner, int node)
    {
        while (owner[node] != node) node = owner[node] = owner[owner[node]];

        return node;
    }

    static void Join(int[] owner, int one, int other)
    {
        var first = Root(owner, one);
        var second = Root(owner, other);
        if (first != second) owner[second] = first;
    }

}
