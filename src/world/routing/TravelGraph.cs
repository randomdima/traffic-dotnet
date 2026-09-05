using System.Numerics;

namespace TrafficSimulation.World.Routing;

/// <summary>What one turn out of one link into another costs. Asked once per pair when the town is laid.</summary>
internal interface ITurnPricer
{
    float PriceM(int fromLink, int toLink);
}

/// <summary>
/// The global tier: <b>a standalone abstract weighted directed graph of links and nothing more</b> —
/// directed links, a weight on each, the links each may be left for, and a price on each of those turns.
/// </summary>
/// <remarks>
/// <para>
/// It could not tell a four-lane boulevard from a zebra crossing, and that is the point: both agent
/// kinds' networks are this type, and <em>which way to go</em> does not get a better answer for being
/// asked in metres. What a link is made of, what shape it is and what is standing on it are the local
/// tier's — see <see cref="RunNetwork"/>, which is this graph plus the pieces each link is walked as.
/// </para>
/// <para>
/// <b>There is no node table.</b> A link says which links it may be left for, the way a lane says which
/// lanes it may be left for (<see cref="Road.ILaneEnds"/>); where several of them meet is not a record
/// this graph keeps. A junction is what a set of crossed ways happens to make (TER-5d), and a search that
/// could name one would be entitled to settle it — which is the bug this shape refuses (see
/// <see cref="RoutePlanner"/>).
/// </para>
/// <para>
/// <b>The one geometric fact a link holds is its own two ends</b>, used for exactly two things: aiming a
/// search and bounding it. The second is why <see cref="Builder.AddLink"/> holds a link's weight up to the
/// span between them and <see cref="Builder.Join"/> refuses a join between ends that are not the same
/// point — the straight line is only an admissible heuristic while no link is priced below it and the
/// links of a route meet end to end, and a search whose bound is not admissible returns routes that are
/// not the cheapest without ever looking wrong.
/// </para>
/// </remarks>
internal sealed class TravelGraph
{
    public const int NoLink = -1;

    readonly Vector2[] _linkStartM;
    readonly Vector2[] _linkEndM;
    readonly float[] _linkWeightM;
    readonly int[] _turnOffsets;
    readonly int[] _turnToLink;
    readonly float[] _turnPriceM;

    TravelGraph(
        Vector2[] linkStartM, Vector2[] linkEndM, float[] linkWeightM, int[] turnOffsets, int[] turnToLink,
        float[] turnPriceM)
    {
        _linkStartM = linkStartM;
        _linkEndM = linkEndM;
        _linkWeightM = linkWeightM;
        _turnOffsets = turnOffsets;
        _turnToLink = turnToLink;
        _turnPriceM = turnPriceM;
    }

    public int LinkCount => _linkWeightM.Length;

    public float WeightM(int link) => _linkWeightM[link];

    /// <summary>Where a link leaves from and where it arrives, which is the whole of its geometry here.</summary>
    public Vector2 StartAnchorM(int link) => _linkStartM[link];

    public Vector2 EndAnchorM(int link) => _linkEndM[link];

    /// <summary>The links a body on this one may leave for, where this one ends.</summary>
    public ReadOnlySpan<int> TurnsFrom(int link) =>
        _turnToLink.AsSpan(_turnOffsets[link], _turnOffsets[link + 1] - _turnOffsets[link]);

    /// <summary>What each of those turns costs, in the same order.</summary>
    public ReadOnlySpan<float> TurnPricesFrom(int link) =>
        _turnPriceM.AsSpan(_turnOffsets[link], _turnOffsets[link + 1] - _turnOffsets[link]);

    /// <summary>
    /// Lays the graph a link and a join at a time. Build-time only: it allocates freely, and nothing it
    /// produces is written to again.
    /// </summary>
    internal sealed class Builder
    {
        readonly List<Vector2> _startM = [];
        readonly List<Vector2> _endM = [];
        readonly List<float> _weightM = [];
        readonly List<int> _joinFrom = [];
        readonly List<int> _joinTo = [];

        public int LinkCount => _weightM.Count;

        /// <summary>
        /// A directed way on between two points, priced at least at the span between them — which is
        /// the relation the search's bound rests on, enforced here rather than asserted later.
        /// </summary>
        public int AddLink(Vector2 startM, Vector2 endM, float weightM)
        {
            _startM.Add(startM);
            _endM.Add(endM);
            _weightM.Add(MathF.Max(weightM, (endM - startM).Length()));
            return _weightM.Count - 1;
        }

        /// <summary>
        /// A way on from the end of one link onto the start of another. <b>They have to be the same
        /// point</b>: the heuristic is the straight line from where a link ends to where the trip is going,
        /// and a route whose links do not meet is a route whose remaining spans no longer add up to at least
        /// that line, which is an inadmissible bound and a silently dearer route.
        /// </summary>
        public void Join(int fromLink, int toLink)
        {
            if (_endM[fromLink] != _startM[toLink])
            {
                throw new ArgumentException(
                    $"link {fromLink} ends at {_endM[fromLink]} and link {toLink} starts at {_startM[toLink]}");
            }

            _joinFrom.Add(fromLink);
            _joinTo.Add(toLink);
        }

        public TravelGraph Build<TPricer>(TPricer pricer) where TPricer : ITurnPricer
        {
            var linkCount = _weightM.Count;
            var turnOffsets = new int[linkCount + 1];
            foreach (var from in _joinFrom) turnOffsets[from + 1]++;
            for (var link = 1; link <= linkCount; link++) turnOffsets[link] += turnOffsets[link - 1];

            var slot = (int[])turnOffsets.Clone();
            var turnToLink = new int[_joinTo.Count];
            var turnPriceM = new float[_joinTo.Count];
            for (var join = 0; join < _joinTo.Count; join++)
            {
                var at = slot[_joinFrom[join]]++;
                turnToLink[at] = _joinTo[join];
                turnPriceM[at] = pricer.PriceM(_joinFrom[join], _joinTo[join]);
            }

            return new TravelGraph(
                [.. _startM], [.. _endM], [.. _weightM], turnOffsets, turnToLink, turnPriceM);
        }
    }
}
