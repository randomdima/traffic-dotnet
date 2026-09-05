using System.Numerics;

namespace TrafficSimulation.World.Routing;

/// <summary>
/// The graph a network is contracted <em>from</em>: every piece of the town a body can travel, as
/// <b>directed lanes joined by connectors</b>. Carriageway lanes cut at their junctions on the driving
/// side, stretches of pavement and crossings on the walking side.
/// </summary>
/// <remarks>
/// <b>There are no nodes in it.</b> Where two lanes meet is not a record a network carries and hands over —
/// it is what the connectors say, and <see cref="RunNetwork.Places"/> works it out. A network that carried
/// its own node table would be stating the same fact twice, and the town's junctions are the plan's
/// business rather than the traveller's: what a body needs to know at the end of a lane is which lanes it
/// may leave for, and that is the connector.
/// </remarks>
internal interface IFineGraph
{
    int LaneCount { get; }

    float LengthM(int lane);

    /// <summary>The same piece travelled the other way, or <see cref="RunNetwork.NoEdge"/>.</summary>
    int Reverse(int lane);

    /// <summary>The lanes a body on this one may leave for at the end of it, which is what its connectors say.</summary>
    ReadOnlySpan<int> Onward(int lane);

    /// <summary>Where the lane's line begins, which is one of the two places its runs are joined at.</summary>
    Vector2 StartsAtM(int lane);

    /// <summary>And where it ends.</summary>
    Vector2 EndsAtM(int lane);

    /// <summary>
    /// Whether the place this lane ends at survives the contraction however few ways on it offers — <b>a
    /// place a body may be sent to</b>, which is a place whether or not a decision is taken there.
    /// </summary>
    bool EndsARun(int lane);
}

/// <summary>What one turn between two fine lanes costs, whether it falls inside a run or between two.</summary>
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
/// second clause is <see cref="IFineGraph.EndsARun"/>, and it is the ends of a parking section: nothing
/// is decided at one, but a leg has to be able to name it.
/// </para>
/// <para>
/// <b>A closed run with no split anywhere on it would contract to nothing</b> — the band a car park is
/// wrapped in, the arc round a dead end's head. One place of it is promoted, and the ring becomes two
/// links leaving and returning to that place. The lowest place is promoted rather than whichever the walk
/// reached first, so the answer does not depend on how the town was built.
/// </para>
/// </remarks>
internal sealed class RunNetwork
{
    public const int NoEdge = -1;

    readonly int[] _pieceOffsets;
    readonly int[] _pieces;
    readonly float[] _stationM;
    readonly float[] _lengthM;
    readonly int[] _placeOf;
    readonly Where _places;

    RunNetwork(
        TravelGraph graph, int[] pieceOffsets, int[] pieces, float[] stationM, float[] lengthM, int[] placeOf,
        Where places)
    {
        Graph = graph;
        _places = places;
        _pieceOffsets = pieceOffsets;
        _pieces = pieces;
        _stationM = stationM;
        _lengthM = lengthM;
        _placeOf = placeOf;
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

    /// <summary>The place a travel node stands at, for a caller that has to get back to the geometry.</summary>
    public int PlaceOf(int node) => _placeOf[node];

    /// <summary>
    /// <b>Where the network's lanes meet</b>, as this contraction worked it out from the connectors
    /// (<see cref="Places"/>). It is what a link's two travel nodes stand at, and the only sense in which
    /// this network has junctions at all.
    /// </summary>
    public int PlaceCount => _places.Count;

    /// <summary>The place one of the fine network's lanes sets off from.</summary>
    public int PlaceLeaving(int lane) => _places.Starting(lane);

    /// <summary>And the place it arrives at.</summary>
    public int PlaceArriving(int lane) => _places.Arriving(lane);

    /// <summary>The lanes that set off from a place.</summary>
    public ReadOnlySpan<int> LanesLeaving(int place) => _places.LanesLeaving(place);

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
        var places = Places(fine);
        var decision = Decisions(fine, places);

        var travelNodeOf = new int[places.Count];
        Array.Fill(travelNodeOf, -1);
        var placeOf = new List<int>();
        var builder = new TravelGraph.Builder();

        var covered = new bool[fine.LaneCount];
        var pieceOffsets = new List<int> { 0 };
        var pieces = new List<int>();
        var stationM = new List<float>();
        var lengthM = new List<float>();
        var firstEdge = new List<int>();
        var lastEdge = new List<int>();

        for (var place = 0; place < places.Count; place++)
        {
            if (decision[place]) WalkFrom(place);
        }

        // What is left over is a ring nothing splits. Promoting the lowest place of it makes the ring two
        // links leaving and returning to one place, which is a shape the search already understands.
        while (true)
        {
            var promote = -1;
            for (var lane = 0; lane < fine.LaneCount; lane++)
            {
                if (covered[lane]) continue;

                var place = places.Starting(lane);
                if (promote < 0 || place < promote) promote = place;
            }

            if (promote < 0) break;

            decision[promote] = true;
            WalkFrom(promote);
        }

        return new RunNetwork(
            builder.Build(new BoundaryPricer<TPricer>(pricer, [.. firstEdge], [.. lastEdge])),
            [.. pieceOffsets], [.. pieces], [.. stationM], [.. lengthM], [.. placeOf], places);

        void WalkFrom(int startPlace)
        {
            foreach (var start in places.LanesLeaving(startPlace))
            {
                if (covered[start]) continue;

                var runLengthM = 0f;
                var weightM = 0f;
                var lane = start;
                while (true)
                {
                    covered[lane] = true;
                    pieces.Add(lane);
                    stationM.Add(runLengthM);
                    runLengthM += fine.LengthM(lane);

                    var arrivedAt = places.Arriving(lane);
                    if (decision[arrivedAt]) break;

                    var onward = Onward(arrivedAt, lane);
                    if (onward < 0) break;

                    // The turns inside a run are in its weight — a run through a sharp bend is genuinely
                    // dearer than a straight one of the same length, and after contraction there is
                    // nowhere else for that to be said.
                    weightM += pricer.PriceM(lane, onward);
                    lane = onward;
                }

                builder.AddLink(TravelNode(startPlace), TravelNode(places.Arriving(lane)), runLengthM + weightM);
                pieceOffsets.Add(pieces.Count);
                lengthM.Add(runLengthM);
                firstEdge.Add(start);
                lastEdge.Add(lane);
            }
        }

        int Onward(int place, int arrivedOn)
        {
            var reverse = fine.Reverse(arrivedOn);
            var fallback = -1;
            foreach (var leaving in places.LanesLeaving(place))
            {
                if (leaving == reverse) continue;
                if (covered[leaving]) continue;

                if (fallback < 0) fallback = leaving;
            }

            return fallback;
        }

        int TravelNode(int place)
        {
            if (travelNodeOf[place] >= 0) return travelNodeOf[place];

            travelNodeOf[place] = builder.AddNode(places.AnchorM(place));
            placeOf.Add(place);
            return travelNodeOf[place];
        }
    }

    /// <summary>
    /// <b>Where the lanes of a network meet, worked out from the connectors and never read off a node
    /// table</b>. Two lane ends are the same place when a connector runs between them, or when they are the
    /// two ends of one stretch driven either way — and a place is what a chain of those comes to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The second clause is what a dead end and a mid-block crossing rest on.</b> No box admits the
    /// movement that turns a car round (TER-5f), so the lane into a dead end has no connector to the lane
    /// coming back out of it and the two ends would stand in different places — a run arriving with nowhere
    /// to go and a run leaving that nothing arrives at. They are the same ground, and joining them is what
    /// lets a leg be priced round a car park's bay (GEN-4l) rather than not offered at all.
    /// </para>
    /// <para>
    /// <b>It is a fact about the network and never about the town's junctions.</b> A junction is a thing the
    /// plan authored and the ground was paved for; what a traveller meets is lanes that end where other
    /// lanes begin, and this is that and nothing more (TER-5d).
    /// </para>
    /// </remarks>
    readonly struct Where(int[] placeOfEnd, int count, Vector2[] anchorM, int[] leavingAt, int[] leaving)
    {
        /// <summary>The place a lane sets off from.</summary>
        public int Starting(int lane) => placeOfEnd[lane * 2];

        /// <summary>And the place it arrives at.</summary>
        public int Arriving(int lane) => placeOfEnd[(lane * 2) + 1];

        public int Count => count;

        /// <summary>Where the place stands: the middle of the lane ends that meet there.</summary>
        public Vector2 AnchorM(int place) => anchorM[place];

        public ReadOnlySpan<int> LanesLeaving(int place) =>
            leaving.AsSpan(leavingAt[place], leavingAt[place + 1] - leavingAt[place]);
    }

    static Where Places<TFine>(TFine fine) where TFine : IFineGraph
    {
        var ends = fine.LaneCount * 2;
        var parent = new int[ends];
        for (var end = 0; end < ends; end++) parent[end] = end;

        for (var lane = 0; lane < fine.LaneCount; lane++)
        {
            foreach (var onto in fine.Onward(lane)) Join(End(lane), Start(onto));

            var reverse = fine.Reverse(lane);
            if (reverse >= 0) Join(End(lane), Start(reverse));
        }

        var placeOf = new int[ends];
        Array.Fill(placeOf, -1);
        var count = 0;
        for (var end = 0; end < ends; end++)
        {
            var root = Root(end);
            if (placeOf[root] < 0) placeOf[root] = count++;

            placeOf[end] = placeOf[root];
        }

        var anchorM = new Vector2[count];
        var met = new int[count];
        var leavingAt = new int[count + 1];
        for (var lane = 0; lane < fine.LaneCount; lane++)
        {
            Meet(placeOf[Start(lane)], fine.StartsAtM(lane));
            Meet(placeOf[End(lane)], fine.EndsAtM(lane));
            leavingAt[placeOf[Start(lane)] + 1]++;
        }

        for (var place = 0; place < count; place++) anchorM[place] /= MathF.Max(1, met[place]);
        for (var place = 1; place <= count; place++) leavingAt[place] += leavingAt[place - 1];

        var cursor = (int[])leavingAt.Clone();
        var leaving = new int[fine.LaneCount];
        for (var lane = 0; lane < fine.LaneCount; lane++) leaving[cursor[placeOf[Start(lane)]]++] = lane;

        return new Where(placeOf, count, anchorM, leavingAt, leaving);

        static int Start(int lane) => lane * 2;

        static int End(int lane) => (lane * 2) + 1;

        void Meet(int place, Vector2 atM)
        {
            anchorM[place] += atM;
            met[place]++;
        }

        int Root(int end)
        {
            while (parent[end] != end) end = parent[end] = parent[parent[end]];

            return end;
        }

        // The lower end keeps the class, so which place a chain of joins comes to is the network's and not
        // the order the joins happened to be made in.
        void Join(int left, int right)
        {
            var a = Root(left);
            var b = Root(right);
            if (a == b) return;

            if (a < b) parent[b] = a;
            else parent[a] = b;
        }
    }

    /// <summary>
    /// <b>Which nodes survive the contraction</b>: the ones something is decided at, the ones two runs meet
    /// at, and the ones a body can be sent to. Everything else is a place one run passes through — one way
    /// on for every way in, and each of them taken by exactly one of them — and a run is what a chain of
    /// those is.
    /// </summary>
    /// <remarks>
    /// <b>Counted as movements and never as edges</b> (TER-4d). Two lanes leaving is what a bend looks like
    /// where every stretch is driven both ways, and a town with one-way streets in it has bends of one lane
    /// in and one lane out, junctions of three lanes where nothing is decided, and — the case an edge count
    /// cannot see at all — <b>merges</b>: two one-way streets running into one, where each arrival has a
    /// single way on and the two of them are the same way. A merge is a node because two links end there.
    /// </remarks>
    static bool[] Decisions<TFine>(TFine fine, Where places) where TFine : IFineGraph
    {
        var arriving = new int[places.Count];
        var comingBack = new int[places.Count];
        var sentTo = new bool[places.Count];
        for (var lane = 0; lane < fine.LaneCount; lane++)
        {
            var place = places.Arriving(lane);
            arriving[place]++;
            if (fine.Reverse(lane) >= 0) comingBack[place]++;
            if (fine.EndsARun(lane)) sentTo[place] = true;
        }

        var decision = new bool[places.Count];
        for (var place = 0; place < places.Count; place++)
        {
            // An arrival that has the way back down its own piece among the ways out has one fewer way on
            // than the place offers, because turning round is no movement (TER-5f). So a place of two-way
            // pieces passes through at two ways out and one of one-way pieces at one, and a place of both
            // has an arrival with a choice whichever it is.
            var ways = places.LanesLeaving(place).Length;
            var oneEach = comingBack[place] == arriving[place]
                ? ways == 2
                : comingBack[place] == 0 && ways == 1;

            decision[place] = sentTo[place] || !oneEach || arriving[place] != ways;
        }

        return decision;
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
