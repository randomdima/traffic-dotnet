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
/// <b>There is no geometry here at all</b>, not even a point: a link is a weight and a list of links it
/// may be left for. Where each one stands on the ground is the local tier's (<see cref="RunNetwork.AnchorM"/>),
/// and a search that could read it would be a search whose answers depended on the town being laid in
/// metres rather than on what the graph says things cost.
/// </para>
/// <para>
/// <b>There is no node table either.</b> A link says which links it may be left for, the way a lane says
/// which lanes it may be left for (<see cref="Road.ILaneEnds"/>); where several of them meet is not a
/// record this graph keeps. A junction is what a set of crossed ways happens to make (TER-5d), and a
/// search that could name one would be entitled to settle it — which is the bug this shape refuses (see
/// <see cref="RoutePlanner"/>).
/// </para>
/// </remarks>
internal sealed class TravelGraph
{
    public const int NoLink = -1;

    readonly float[] _linkWeightM;
    readonly int[] _turnOffsets;
    readonly int[] _turnToLink;
    readonly float[] _turnPriceM;

    TravelGraph(float[] linkWeightM, int[] turnOffsets, int[] turnToLink, float[] turnPriceM)
    {
        _linkWeightM = linkWeightM;
        _turnOffsets = turnOffsets;
        _turnToLink = turnToLink;
        _turnPriceM = turnPriceM;
    }

    public int LinkCount => _linkWeightM.Length;

    public float WeightM(int link) => _linkWeightM[link];

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
        readonly List<float> _weightM = [];
        readonly List<int> _joinFrom = [];
        readonly List<int> _joinTo = [];

        public int LinkCount => _weightM.Count;

        /// <summary>A directed way on, at what travelling the whole of it costs.</summary>
        public int AddLink(float weightM)
        {
            _weightM.Add(weightM);
            return _weightM.Count - 1;
        }

        /// <summary>
        /// A way on from the end of one link onto the start of another. <b>Whether they meet is the
        /// caller's</b>: this graph has no coordinates to check it against, and the local tier that lays
        /// the links knows which places they run between (<see cref="RunNetwork"/>).
        /// </summary>
        public void Join(int fromLink, int toLink)
        {
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

            return new TravelGraph([.. _weightM], turnOffsets, turnToLink, turnPriceM);
        }
    }
}
