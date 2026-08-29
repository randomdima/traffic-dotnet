using System.Numerics;

namespace TrafficSimulation.World.Routing;

/// <summary>
/// The graph a network is contracted <em>from</em>: every piece of the town a body can travel, as
/// directed edges between fine nodes. Roads cut at their junctions on the driving side, stretches of
/// pavement and crossings on the walking side.
/// </summary>
internal interface IFineGraph
{
    int NodeCount { get; }

    int EdgeCount { get; }

    Vector2 AnchorM(int node);

    int FromNode(int edge);

    int ToNode(int edge);

    float LengthM(int edge);

    /// <summary>The same piece travelled the other way, or <see cref="RunNetwork.NoEdge"/>.</summary>
    int Reverse(int edge);

    ReadOnlySpan<int> EdgesOut(int node);

    /// <summary>
    /// Whether a node survives the contraction however few ways on it offers — <b>a place a body may be
    /// sent to</b>, which is a node whether or not a decision is taken there.
    /// </summary>
    bool AlwaysANode(int node);
}

/// <summary>What one turn between two fine edges costs, whether it falls inside a run or between two.</summary>
internal interface IEdgeTurnPricer
{
    float PriceM(int fromEdge, int toEdge);
}

/// <summary>
/// A contracted network: the abstract <see cref="TravelGraph"/> the search runs over, plus, for each
/// link, <b>the pieces it is made of in the order they are travelled and how far into itself each of
/// them starts</b>. The two networks are deliberately this same shape.
/// </summary>
/// <remarks>
/// <para>
/// <b>A node is a place a body can go more than one way, or a place it can be sent to, and nothing else is
/// a node.</b> A line, however it bends, produces no nodes: a plan cuts a street wherever it wants a
/// junction disc, and a body arriving at one of those has exactly one way on, so no decision can be made
/// there. Everything between two decisions is therefore one link — which is what stops the search asking a
/// question at every bend in the town, and what makes a turn price mean something when it is asked. The
/// second clause is <see cref="IFineGraph.AlwaysANode"/>, and it is the ends of a parking section: nothing
/// is decided at one, but a leg has to be able to name it.
/// </para>
/// <para>
/// <b>A closed run with no split anywhere on it would contract to nothing</b> — the band a car park is
/// wrapped in, the arc round a dead end's head. One node of it is promoted, and the ring becomes two
/// links leaving and returning to that node. The lowest fine-node id is promoted rather than whichever
/// the walk reached first, so the answer does not depend on how the town was built.
/// </para>
/// </remarks>
internal sealed class RunNetwork
{
    public const int NoEdge = -1;

    readonly int[] _pieceOffsets;
    readonly int[] _pieces;
    readonly float[] _stationM;
    readonly float[] _lengthM;
    readonly int[] _fineNodeOf;

    RunNetwork(
        TravelGraph graph, int[] pieceOffsets, int[] pieces, float[] stationM, float[] lengthM, int[] fineNodeOf)
    {
        Graph = graph;
        _pieceOffsets = pieceOffsets;
        _pieces = pieces;
        _stationM = stationM;
        _lengthM = lengthM;
        _fineNodeOf = fineNodeOf;
    }

    public TravelGraph Graph { get; }

    public int LinkCount => _lengthM.Length;

    /// <summary>The pieces this link is travelled as, in the order they are travelled.</summary>
    public ReadOnlySpan<int> PiecesOf(int link) =>
        _pieces.AsSpan(_pieceOffsets[link], _pieceOffsets[link + 1] - _pieceOffsets[link]);

    /// <summary>How far into the link each of those pieces begins, in the same order. Monotone, starting at zero.</summary>
    public ReadOnlySpan<float> StationsOf(int link) =>
        _stationM.AsSpan(_pieceOffsets[link], _pieceOffsets[link + 1] - _pieceOffsets[link]);

    /// <summary>The run's own length on the ground, which is not its weight: a weight also carries what its turns cost.</summary>
    public float LengthM(int link) => _lengthM[link];

    /// <summary>The fine node a travel node stands at, for a caller that has to get back to the geometry.</summary>
    public int FineNodeOf(int node) => _fineNodeOf[node];

    /// <summary>How far into the link a place on one of its pieces stands.</summary>
    public float PlaceOfM(int link, int slot, float alongPieceM) => StationsOf(link)[slot] + alongPieceM;

    /// <summary>
    /// Which piece of the link a distance into it falls on, and how far into that piece. A bisection,
    /// because a route is driven in order and every question asked of it is answered from where the body
    /// already is rather than by walking the run from the start.
    /// </summary>
    public int PieceAt(int link, float alongLinkM, out float alongPieceM)
    {
        var stations = StationsOf(link);
        var low = 0;
        var high = stations.Length - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (stations[middle] <= alongLinkM) low = middle;
            else high = middle - 1;
        }

        alongPieceM = alongLinkM - stations[low];
        return low;
    }

    /// <summary>
    /// Contracts a fine graph into runs. Build-time only, and it allocates freely: nothing it produces is
    /// written to again.
    /// </summary>
    public static RunNetwork Contract<TFine, TPricer>(TFine fine, TPricer pricer)
        where TFine : IFineGraph
        where TPricer : IEdgeTurnPricer
    {
        var decision = new bool[fine.NodeCount];
        for (var node = 0; node < fine.NodeCount; node++)
        {
            decision[node] = fine.EdgesOut(node).Length != 2 || fine.AlwaysANode(node);
        }

        var travelNodeOf = new int[fine.NodeCount];
        Array.Fill(travelNodeOf, -1);
        var fineNodeOf = new List<int>();
        var builder = new TravelGraph.Builder();

        var covered = new bool[fine.EdgeCount];
        var pieceOffsets = new List<int> { 0 };
        var pieces = new List<int>();
        var stationM = new List<float>();
        var lengthM = new List<float>();
        var firstEdge = new List<int>();
        var lastEdge = new List<int>();

        for (var node = 0; node < fine.NodeCount; node++)
        {
            if (decision[node]) WalkFrom(node);
        }

        // What is left over is a ring nothing splits. Promoting the lowest fine node of it makes the ring
        // two links leaving and returning to one place, which is a shape the search already understands.
        while (true)
        {
            var promote = -1;
            for (var edge = 0; edge < fine.EdgeCount; edge++)
            {
                if (covered[edge]) continue;

                var node = fine.FromNode(edge);
                if (promote < 0 || node < promote) promote = node;
            }

            if (promote < 0) break;

            decision[promote] = true;
            WalkFrom(promote);
        }

        return new RunNetwork(
            builder.Build(new BoundaryPricer<TPricer>(pricer, [.. firstEdge], [.. lastEdge])),
            [.. pieceOffsets], [.. pieces], [.. stationM], [.. lengthM], [.. fineNodeOf]);

        void WalkFrom(int startNode)
        {
            foreach (var start in fine.EdgesOut(startNode))
            {
                if (covered[start]) continue;

                var runLengthM = 0f;
                var weightM = 0f;
                var edge = start;
                while (true)
                {
                    covered[edge] = true;
                    pieces.Add(edge);
                    stationM.Add(runLengthM);
                    runLengthM += fine.LengthM(edge);

                    var arrivedAt = fine.ToNode(edge);
                    if (decision[arrivedAt]) break;

                    var onward = Onward(arrivedAt, edge);
                    if (onward < 0) break;

                    // The turns inside a run are in its weight — a run through a sharp bend is genuinely
                    // dearer than a straight one of the same length, and after contraction there is
                    // nowhere else for that to be said.
                    weightM += pricer.PriceM(edge, onward);
                    edge = onward;
                }

                builder.AddLink(TravelNode(startNode), TravelNode(fine.ToNode(edge)), runLengthM + weightM);
                pieceOffsets.Add(pieces.Count);
                lengthM.Add(runLengthM);
                firstEdge.Add(start);
                lastEdge.Add(edge);
            }
        }

        int Onward(int node, int arrivedOn)
        {
            var reverse = fine.Reverse(arrivedOn);
            var fallback = -1;
            foreach (var leaving in fine.EdgesOut(node))
            {
                if (leaving == reverse) continue;
                if (covered[leaving]) continue;

                if (fallback < 0) fallback = leaving;
            }

            return fallback;
        }

        int TravelNode(int fineNode)
        {
            if (travelNodeOf[fineNode] >= 0) return travelNodeOf[fineNode];

            travelNodeOf[fineNode] = builder.AddNode(fine.AnchorM(fineNode));
            fineNodeOf.Add(fineNode);
            return travelNodeOf[fineNode];
        }
    }

    /// <summary>
    /// The turn between two links is the turn between the last piece of one and the first piece of the
    /// next — the only thing about a link's shape the abstract graph is allowed to know.
    /// </summary>
    readonly struct BoundaryPricer<TPricer>(TPricer pricer, int[] firstEdge, int[] lastEdge) : ITurnPricer
        where TPricer : IEdgeTurnPricer
    {
        public float PriceM(int fromLink, int toLink) => pricer.PriceM(lastEdge[fromLink], firstEdge[toLink]);
    }
}
