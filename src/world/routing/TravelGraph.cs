using System.Numerics;

namespace TrafficSimulation.World.Routing;

/// <summary>What one turn out of one link into another costs. Asked once per pair when the town is laid.</summary>
internal interface ITurnPricer
{
    float PriceM(int fromLink, int toLink);
}

/// <summary>
/// The global tier: <b>a standalone abstract weighted directed graph and nothing more</b> — nodes,
/// directed links, a weight on each link and a price on each turn out of one.
/// </summary>
/// <remarks>
/// <para>
/// It could not tell a four-lane boulevard from a zebra crossing, and that is the point: both agent
/// kinds' networks are this type, and <em>which way to go</em> does not get a better answer for being
/// asked in metres. What a link is made of, what shape it is and what is standing on it are the local
/// tier's — see <see cref="RunNetwork"/>, which is this graph plus the pieces each link is walked as.
/// </para>
/// <para>
/// <b>The one geometric fact a node holds is its anchor</b>, used for exactly two things: finding the
/// node nearest a place, and bounding the search. The second is why
/// <see cref="Builder.AddLink"/> holds a link's weight up to the span between its two anchors — the
/// straight line is only an admissible heuristic while no link is priced below it, and a search whose
/// bound is not admissible returns routes that are not the cheapest without ever looking wrong.
/// </para>
/// </remarks>
internal sealed class TravelGraph
{
    public const int NoLink = -1;

    readonly Vector2[] _nodeAnchorM;
    readonly int[] _linkFromNode;
    readonly int[] _linkToNode;
    readonly float[] _linkWeightM;
    readonly int[] _turnOffsets;
    readonly int[] _turnToLink;
    readonly float[] _turnPriceM;

    TravelGraph(
        Vector2[] nodeAnchorM, int[] linkFromNode, int[] linkToNode, float[] linkWeightM,
        int[] turnOffsets, int[] turnToLink, float[] turnPriceM)
    {
        _nodeAnchorM = nodeAnchorM;
        _linkFromNode = linkFromNode;
        _linkToNode = linkToNode;
        _linkWeightM = linkWeightM;
        _turnOffsets = turnOffsets;
        _turnToLink = turnToLink;
        _turnPriceM = turnPriceM;
    }

    public int NodeCount => _nodeAnchorM.Length;

    public int LinkCount => _linkWeightM.Length;

    public Vector2 AnchorOf(int node) => _nodeAnchorM[node];

    public int FromNode(int link) => _linkFromNode[link];

    public int ToNode(int link) => _linkToNode[link];

    public float WeightM(int link) => _linkWeightM[link];

    /// <summary>Where a link leaves from and where it arrives, which is the whole of its geometry here.</summary>
    public Vector2 StartAnchorM(int link) => _nodeAnchorM[_linkFromNode[link]];

    public Vector2 EndAnchorM(int link) => _nodeAnchorM[_linkToNode[link]];

    /// <summary>The links a body on this one may leave for, at the node this one arrives at.</summary>
    public ReadOnlySpan<int> TurnsFrom(int link) =>
        _turnToLink.AsSpan(_turnOffsets[link], _turnOffsets[link + 1] - _turnOffsets[link]);

    /// <summary>What each of those turns costs, in the same order.</summary>
    public ReadOnlySpan<float> TurnPricesFrom(int link) =>
        _turnPriceM.AsSpan(_turnOffsets[link], _turnOffsets[link + 1] - _turnOffsets[link]);

    /// <summary>
    /// Lays the graph a link at a time. Build-time only: it allocates freely, and nothing it produces is
    /// written to again.
    /// </summary>
    internal sealed class Builder
    {
        readonly List<Vector2> _anchorM = [];
        readonly List<int> _fromNode = [];
        readonly List<int> _toNode = [];
        readonly List<float> _weightM = [];

        public int NodeCount => _anchorM.Count;

        public int LinkCount => _weightM.Count;

        public int AddNode(Vector2 anchorM)
        {
            _anchorM.Add(anchorM);
            return _anchorM.Count - 1;
        }

        /// <summary>
        /// A directed way on, priced at least at the span between the two anchors it joins — which is
        /// the relation the search's bound rests on, enforced here rather than asserted later.
        /// </summary>
        public int AddLink(int fromNode, int toNode, float weightM)
        {
            _fromNode.Add(fromNode);
            _toNode.Add(toNode);
            _weightM.Add(MathF.Max(weightM, (_anchorM[toNode] - _anchorM[fromNode]).Length()));
            return _weightM.Count - 1;
        }

        public Vector2 AnchorOf(int node) => _anchorM[node];

        public TravelGraph Build<TPricer>(TPricer pricer) where TPricer : ITurnPricer
        {
            var linkCount = _weightM.Count;
            var outOf = new List<int>[_anchorM.Count];
            for (var node = 0; node < outOf.Length; node++) outOf[node] = [];
            for (var link = 0; link < linkCount; link++) outOf[_fromNode[link]].Add(link);

            var turnOffsets = new int[linkCount + 1];
            for (var link = 0; link < linkCount; link++)
            {
                turnOffsets[link + 1] = turnOffsets[link] + outOf[_toNode[link]].Count;
            }

            var turnToLink = new int[turnOffsets[linkCount]];
            var turnPriceM = new float[turnOffsets[linkCount]];
            for (var link = 0; link < linkCount; link++)
            {
                var slot = turnOffsets[link];
                foreach (var leaving in outOf[_toNode[link]])
                {
                    turnToLink[slot] = leaving;
                    turnPriceM[slot] = pricer.PriceM(link, leaving);
                    slot++;
                }
            }

            return new TravelGraph(
                [.. _anchorM], [.. _fromNode], [.. _toNode], [.. _weightM], turnOffsets, turnToLink, turnPriceM);
        }
    }
}
