using System.Numerics;
using TrafficSimulation.Core.Config;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>What a road is for, which is what decides its width, how far it may bend and what it fronts.</summary>
internal enum RoadClass : byte
{
    /// <summary>A block's own street: short, laid on its district's bearing, and where the buildings are.</summary>
    Street,

    /// <summary>The orbital and the spokes: long, faster, and the only roads that cross a district edge.</summary>
    Arterial,

    /// <summary>
    /// The one span between two bridgeheads. <b>The only class of road that may stand over water</b>
    /// (GEN-14a), and the only one whose shape is settled before it is laid: straight, whatever the arterial
    /// it is a piece of was going to do.
    /// </summary>
    Bridge,
}

/// <summary>One road of the layout before it has a shape: what it joins, what it is for, and how it runs.</summary>
/// <param name="Curvature">
/// The bend the layout itself asks for, as 1/radius — an orbital's own arc, and zero for everything the road
/// stage is free to wander (<see cref="RoadStage"/>). It is signed: left of travel is positive.
/// </param>
internal readonly record struct LayoutEdge(int From, int To, RoadClass Class, float Curvature);

/// <summary>
/// <b>The town as nodes and what joins them</b>, before any of it is a curve or a cell — the product of the
/// district and node stage and the whole of what the road stage is given.
/// </summary>
/// <remarks>
/// <para>
/// <b>One connected component with nothing dangling off it, reached by deletion rather than by retry</b>
/// (GEN-5, GEN-5a). Streets are laid inside a district's own convex region and arterials are laid through
/// the town, which is most of what keeps them apart; what the arrangement does not settle,
/// <see cref="UnpickTheCrossings"/> deletes (GEN-17). Water or a district edge can leave a piece of the town
/// joined to nothing, and <see cref="KeepTheLargestComponent"/> deletes that, or leave a street ending in a
/// field, and <see cref="PruneTheDeadEnds"/> deletes that. A town is what stayed connected and led
/// somewhere, and the alternative — laying it again with another seed until it is one piece — is the search
/// this generator does not do.
/// </para>
/// <para>
/// <b>Everything here deletes and nothing retries</b>, so the four passes run in the order that leaves the
/// town the most road: the local nodes are merged first, because a merge moves what the next pass has to
/// measure; the crossings are unpicked next, in the order the town cares about its roads; and what either
/// left stranded or dangling goes last.
/// </para>
/// </remarks>
internal sealed class TownLayout(float shortestRoadM, float armsApartMinRad, float localityM, WaterRules water)
{
    readonly List<Vector2> _nodeM = [];

    /// <summary>The bearing each road leaves each node on, so a new arm can be asked how square it stands to them.</summary>
    readonly List<List<float>> _armsAt = [];
    readonly List<LayoutEdge> _edges = [];

    /// <summary>Which pairs of nodes are already joined, so no two of them are joined twice.</summary>
    readonly HashSet<(int From, int To)> _joined = [];

    public IReadOnlyList<Vector2> NodeM => _nodeM;

    public IReadOnlyList<LayoutEdge> Edges => _edges;

    /// <summary>
    /// One node, or <c>−1</c> where the ground will not carry one. <b>Nothing stands on the water</b>
    /// (GEN-14): a node is a junction, and a junction in the river is the box, the fillets, the crossings and
    /// the bar that come with it laid over open water. A caller that is refused lays nothing there.
    /// </summary>
    public int AddNode(Vector2 atM)
    {
        if (water.Wet(atM)) return -1;

        _nodeM.Add(atM);
        _armsAt.Add([]);
        return _nodeM.Count - 1;
    }

    /// <summary>
    /// One road between two nodes, if it is a road at all. Three things are refused here rather than found
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
    /// </list>
    /// <b>A road refused here is a road the town does not have</b>, and whatever that leaves unreachable is
    /// deleted with its own piece (<see cref="KeepTheLargestComponent"/>) rather than joined some other way.
    /// <b>What is not refused here is a road crossing another road</b>: which of the two the town would
    /// rather keep is not knowable while they are still being offered, so that is
    /// <see cref="UnpickTheCrossings"/>'s to settle once every road has been laid.
    /// </summary>
    public void Join(int from, int to, RoadClass roadClass, float curvature = 0f)
    {
        if (from == to) return;

        var runM = _nodeM[to] - _nodeM[from];
        if (runM.Length() < shortestRoadM) return;
        if (!water.Carries(_nodeM[from], _nodeM[to], roadClass)) return;
        if (!_joined.Add(from < to ? (from, to) : (to, from))) return;

        var outward = MathF.Atan2(runM.Y, runM.X);
        if (!StandsSquareEnough(from, outward) || !StandsSquareEnough(to, outward + MathF.PI))
        {
            _joined.Remove(from < to ? (from, to) : (to, from));
            return;
        }

        _armsAt[from].Add(outward);
        _armsAt[to].Add(outward + MathF.PI);
        _edges.Add(new LayoutEdge(from, to, roadClass, curvature));
    }

    /// <summary>
    /// Whether an arm leaving on this bearing stands far enough off every arm already at the node — or
    /// straight through one of them, which is a road passing a junction rather than meeting one.
    /// </summary>
    bool StandsSquareEnough(int node, float outwardRad)
    {
        foreach (var arm in _armsAt[node])
        {
            // Half a turn apart is a road running through the node rather than two arms lying together, and
            // that is the one wide angle a junction is made of.
            if (MathF.Abs(MathF.IEEERemainder(outwardRad - arm, MathF.Tau)) < armsApartMinRad) return false;
        }

        return true;
    }

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

    /// <summary>
    /// <b>Every cluster of nodes standing within a locality of each other is one node</b> (GEN-16): what was
    /// two junctions a stride apart — a pair of boxes with their corners, crossings and bars laid over each
    /// other, and a road between them no car is ever on — becomes the one junction their roads all meet at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It runs once the whole layout is joined and not as each node is placed.</b> A node welded at
    /// placement is a node the stage that placed it has lost track of — an arterial that recorded a node on
    /// its own line and got back one off it lays its next piece to somewhere else — so the arithmetic that
    /// puts nodes on a spoke, on the orbital and on a lattice is left to finish first, and this is what the
    /// arrangement it produced is then held to.
    /// </para>
    /// <para>
    /// <b>The node the town cares more about is the one that stays</b>, and the others move onto it: a
    /// bridgehead cannot move at all, an arterial's line is the town's and a street is what bends to meet
    /// either. What is left is re-offered road by road through <see cref="Join"/> in that same order, so a
    /// merge that leaves two arms lying together drops the street rather than the arterial (GEN-13) — and
    /// whatever that leaves hanging is deleted with its own piece, as everything else here is.
    /// </para>
    /// </remarks>
    public void MergeTheLocalNodes()
    {
        var root = new int[_nodeM.Count];
        for (var node = 0; node < root.Length; node++) root[node] = node;
        for (var node = 0; node < root.Length; node++)
        {
            for (var other = node + 1; other < root.Length; other++)
            {
                if ((_nodeM[node] - _nodeM[other]).LengthSquared() < localityM * localityM)
                {
                    Union(root, node, other);
                }
            }
        }

        var precedence = new int[_nodeM.Count];
        foreach (var edge in _edges)
        {
            var rank = Precedence(edge.Class);
            precedence[edge.From] = Math.Max(precedence[edge.From], rank);
            precedence[edge.To] = Math.Max(precedence[edge.To], rank);
        }

        var stays = new int[_nodeM.Count];
        Array.Fill(stays, -1);
        for (var node = 0; node < _nodeM.Count; node++)
        {
            var cluster = Find(root, node);
            if (stays[cluster] < 0 || precedence[node] > precedence[stays[cluster]]) stays[cluster] = node;
        }

        var moved = new int[_nodeM.Count];
        var kept = new List<Vector2>(_nodeM.Count);
        for (var node = 0; node < _nodeM.Count; node++)
        {
            if (stays[Find(root, node)] != node)
            {
                moved[node] = -1;
                continue;
            }

            moved[node] = kept.Count;
            kept.Add(_nodeM[node]);
        }

        for (var node = 0; node < _nodeM.Count; node++)
        {
            if (moved[node] < 0) moved[node] = moved[stays[Find(root, node)]];
        }

        // <b>Offered back in the order the town cares about them</b>, so that where a merge leaves two arms
        // lying together it is the street that is dropped and never the arterial — offered in the order they
        // were laid, a street can sever the arterial it was hung off and take half the town with it when
        // the largest piece is kept.
        var edges = new List<LayoutEdge>(_edges.Count);
        for (var rank = 2; rank >= 0; rank--)
        {
            foreach (var edge in _edges)
            {
                if (Precedence(edge.Class) != rank) continue;

                edges.Add(edge with { From = moved[edge.From], To = moved[edge.To] });
            }
        }

        Reoffered(kept, edges);
    }

    /// <summary>
    /// Which of two roads the other gives way to where a merge has to choose between them: a deck cannot
    /// move, an arterial's line is the town's, and a street is what bends to meet either.
    /// </summary>
    static int Precedence(RoadClass roadClass) => roadClass switch
    {
        RoadClass.Bridge => 2,
        RoadClass.Arterial => 1,
        _ => 0,
    };

    /// <summary>
    /// <b>Drops every road that would share ground with a road it does not meet at a junction</b> (GEN-17).
    /// A junction is the only place two carriageways may touch: two that cross anywhere else have no box, no
    /// fillets, no crossings and no bar where they meet, and nothing downstream — the follower, the claim,
    /// the walk — has anything to say about the ground they share.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a pass over the finished layout and not a test inside <see cref="Join"/></b>, because the
    /// stages lay in the order the ground is walked and not in the order the town cares about: the lattice
    /// hangs its streets before <see cref="Arterials.Close"/> joins the arterial they cross, so a test made
    /// as each road is offered would keep whichever was laid first and delete the orbital. Taken in
    /// precedence order afterwards, the arterial stands and the street gives way (GEN-13, GEN-16).
    /// </para>
    /// <para>
    /// <b>Two roads are measured as the ground they will take and not as the lines they are joined on.</b>
    /// A road is laid at <paramref name="apartM"/> wide, and its shape does not stay on its chord: a street
    /// wanders off it and an arterial's arc bulges off it, which is what <paramref name="straysM"/> carries
    /// road for road (<see cref="RoadStage.StraysM"/>). Measured against the chords alone, two roads that
    /// pass the test still meet once they are drawn.
    /// </para>
    /// <para>
    /// <b>Roads that share a node are left alone.</b> They touch there because that is what a junction is,
    /// and how square they have to stand to each other is <see cref="StandsSquareEnough"/>'s (GEN-13).
    /// </para>
    /// </remarks>
    /// <param name="apartM">
    /// How far apart two roads' own lines stand when their ground merely touches — one road's whole width,
    /// carriageway and walk (<see cref="SimConfig.RoadFootprintM"/>).
    /// </param>
    /// <param name="straysM">
    /// How far off its own chord each road is drawn, in the order <see cref="Edges"/> carries them.
    /// </param>
    public void UnpickTheCrossings(float apartM, ReadOnlySpan<float> straysM)
    {
        var kept = new List<LayoutEdge>(_edges.Count);
        var keptStraysM = new List<float>(_edges.Count);
        for (var rank = 2; rank >= 0; rank--)
        {
            for (var road = 0; road < _edges.Count; road++)
            {
                if (Precedence(_edges[road].Class) != rank) continue;
                if (SharesGround(kept, keptStraysM, _edges[road], straysM[road], apartM)) continue;

                kept.Add(_edges[road]);
                keptStraysM.Add(straysM[road]);
            }
        }

        if (kept.Count == _edges.Count) return;

        Rebuilt([.. _nodeM], kept);
    }

    bool SharesGround(
        List<LayoutEdge> kept, List<float> straysM, LayoutEdge edge, float strayM, float apartM)
    {
        var fromM = _nodeM[edge.From];
        var toM = _nodeM[edge.To];
        for (var at = 0; at < kept.Count; at++)
        {
            var other = kept[at];
            if (other.From == edge.From || other.From == edge.To
                || other.To == edge.From || other.To == edge.To)
            {
                continue;
            }

            var clearM = apartM + strayM + straysM[at];
            if (ApartM(fromM, toM, _nodeM[other.From], _nodeM[other.To]) < clearM) return true;
        }

        return false;
    }

    /// <summary>How near two chords pass, which is nil where they cross.</summary>
    static float ApartM(Vector2 aFromM, Vector2 aToM, Vector2 bFromM, Vector2 bToM)
    {
        var a = aToM - aFromM;
        var b = bToM - bFromM;
        var between = aFromM - bFromM;
        var denominator = (a.X * b.Y) - (a.Y * b.X);
        if (MathF.Abs(denominator) > 1e-6f)
        {
            var alongA = ((b.X * between.Y) - (b.Y * between.X)) / denominator;
            var alongB = ((a.X * between.Y) - (a.Y * between.X)) / denominator;
            if (alongA is >= 0f and <= 1f && alongB is >= 0f and <= 1f) return 0f;
        }

        return MathF.Min(
            MathF.Min(OffM(aFromM, bFromM, bToM), OffM(aToM, bFromM, bToM)),
            MathF.Min(OffM(bFromM, aFromM, aToM), OffM(bToM, aFromM, aToM)));
    }

    static float OffM(Vector2 pointM, Vector2 fromM, Vector2 toM)
    {
        var runM = toM - fromM;
        var lengthSquared = runM.LengthSquared();
        var along = lengthSquared > 0f
            ? Math.Clamp(Vector2.Dot(pointM - fromM, runM) / lengthSquared, 0f, 1f)
            : 0f;
        return (pointM - (fromM + (runM * along))).Length();
    }

    /// <summary>
    /// Drops everything not joined to the largest piece of the town, nodes and roads together, and renumbers
    /// what is left. <b>A node nothing reaches is deleted and not connected</b>: a link drawn to reach it
    /// would cross whatever stands in the way, and a road crossing another road where no junction is would be
    /// worse than the island it fixed.
    /// </summary>
    public void KeepTheLargestComponent()
    {
        if (_edges.Count == 0) return;

        var root = new int[_nodeM.Count];
        for (var node = 0; node < root.Length; node++) root[node] = node;
        foreach (var edge in _edges) Union(root, edge.From, edge.To);

        var size = new int[_nodeM.Count];
        for (var node = 0; node < root.Length; node++) size[Find(root, node)]++;

        var largest = 0;
        for (var node = 1; node < size.Length; node++)
        {
            if (size[node] > size[largest]) largest = node;
        }

        var moved = new int[_nodeM.Count];
        var kept = new List<Vector2>(_nodeM.Count);
        for (var node = 0; node < _nodeM.Count; node++)
        {
            if (Find(root, node) != largest)
            {
                moved[node] = -1;
                continue;
            }

            moved[node] = kept.Count;
            kept.Add(_nodeM[node]);
        }

        var edges = new List<LayoutEdge>(_edges.Count);
        foreach (var edge in _edges)
        {
            if (moved[edge.From] < 0 || moved[edge.To] < 0) continue;

            edges.Add(edge with { From = moved[edge.From], To = moved[edge.To] });
        }

        Rebuilt(kept, edges);
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
            if (arms[node] == 1) leaves.Push(node);
        }

        while (leaves.Count > 0)
        {
            var node = leaves.Pop();
            if (arms[node] != 1) continue;

            foreach (var edge in edgesAt[node])
            {
                if (dropped[edge]) continue;

                dropped[edge] = true;
                arms[node]--;
                var beyond = _edges[edge].From == node ? _edges[edge].To : _edges[edge].From;
                if (--arms[beyond] == 1) leaves.Push(beyond);
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
        for (var edge = 0; edge < _edges.Count; edge++)
        {
            if (dropped[edge]) continue;

            edges.Add(_edges[edge] with { From = moved[_edges[edge].From], To = moved[_edges[edge].To] });
        }

        Rebuilt(kept, edges);
    }

    /// <summary>
    /// The layout on a new set of nodes, with every road <em>offered again</em> rather than carried over —
    /// which is what a merge needs and a deletion does not: moving a node changes what its arms are worth,
    /// so each of them has to pass what it passed the first time (<see cref="Join"/>).
    /// </summary>
    void Reoffered(List<Vector2> nodeM, List<LayoutEdge> edges)
    {
        _nodeM.Clear();
        _nodeM.AddRange(nodeM);
        _edges.Clear();
        _joined.Clear();
        _armsAt.Clear();
        for (var node = 0; node < _nodeM.Count; node++) _armsAt.Add([]);
        foreach (var edge in edges) Join(edge.From, edge.To, edge.Class, edge.Curvature);
    }

    void Rebuilt(List<Vector2> nodeM, List<LayoutEdge> edges)
    {
        _nodeM.Clear();
        _nodeM.AddRange(nodeM);
        _edges.Clear();
        _edges.AddRange(edges);
        _joined.Clear();
        _armsAt.Clear();
        for (var node = 0; node < _nodeM.Count; node++) _armsAt.Add([]);
        foreach (var edge in _edges)
        {
            _joined.Add(edge.From < edge.To ? (edge.From, edge.To) : (edge.To, edge.From));
            var runM = _nodeM[edge.To] - _nodeM[edge.From];
            var outward = MathF.Atan2(runM.Y, runM.X);
            _armsAt[edge.From].Add(outward);
            _armsAt[edge.To].Add(outward + MathF.PI);
        }
    }

    static int Find(int[] root, int node)
    {
        while (root[node] != node)
        {
            root[node] = root[root[node]];
            node = root[node];
        }

        return node;
    }

    static void Union(int[] root, int a, int b)
    {
        a = Find(root, a);
        b = Find(root, b);
        if (a != b) root[b] = a;
    }
}
