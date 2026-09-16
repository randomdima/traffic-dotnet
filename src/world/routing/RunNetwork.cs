using System.Numerics;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Routing;

/// <summary>
/// The graph a network is contracted <em>from</em>: every piece of the town a body can travel, as
/// <b>directed lanes</b>. Carriageway lanes on the driving side, stretches of pavement and crossings on the
/// walking side.
/// </summary>
/// <remarks>
/// <b>There are no nodes in it, and it is not asked for any.</b> Where two lanes meet is
/// <see cref="Road.LanePlaces"/>'s answer, worked out once per network by whoever owns it and handed to
/// <see cref="RunNetwork.Contract"/> — so a contraction cannot work it out a second way and disagree about
/// which lane ends are one piece of the world (SIM-7). What is left for the fine graph to say is how long a
/// piece is and which one is the same piece travelled back.
/// </remarks>
internal interface IFineGraph
{
    int LaneCount { get; }

    float LengthM(int lane);

    /// <summary>The same piece travelled the other way, or negative where there is none.</summary>
    int Reverse(int lane);
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
/// <b>A link ends where a body can go more than one way, and nowhere else.</b>
/// A line, however it bends, ends no link: a plan cuts a street wherever it wants a junction disc, and a
/// body arriving at one of those has exactly one way on, so no decision can be made there. Everything
/// between two decisions is therefore one link — which is what stops the search asking a question at every
/// bend in the town, and what makes a turn price mean something when it is asked. <b>Nowhere is promoted
/// for being somewhere a body is sent</b>: a destination is a place on a link and carries how far into it
/// stands (<see cref="RouteGoal"/>), so a car park's frontage needs no node of its own to be aimed at.
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
    readonly LanePlaces _places;

    RunNetwork(
        TravelGraph graph, int[] pieceOffsets, int[] pieces, float[] stationM, float[] lengthM,
        LanePlaces places)
    {
        Graph = graph;
        _places = places;
        _pieceOffsets = pieceOffsets;
        _pieces = pieces;
        _stationM = stationM;
        _lengthM = lengthM;
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

    /// <summary>
    /// <b>Where the network's lanes meet</b> (<see cref="LanePlaces"/>). It is where a link's two ends
    /// stand, and the only sense in which this network has junctions at all — the coarse graph keeps no
    /// record of them, only of which link may be left for which.
    /// </summary>
    public int PlaceCount => _places.Count;

    /// <summary>The place one of the fine network's lanes sets off from.</summary>
    public int PlaceLeaving(int lane) => _places.Starting(lane);

    /// <summary>And the place it arrives at.</summary>
    public int PlaceArriving(int lane) => _places.Arriving(lane);

    /// <summary>The lanes that set off from a place.</summary>
    public ReadOnlySpan<int> LanesLeaving(int place) => _places.LanesLeaving(place);

    /// <summary>Where a place stands, which is where every link leaving or arriving at it has its own end.</summary>
    public Vector2 AnchorM(int place) => _places.AnchorM(place);

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
    /// <param name="places">
    /// <b>Where the fine graph's lanes meet, worked out by whoever owns them</b> and never here (SIM-7): the
    /// claims and the router read one answer, so neither can be entitled to a different one.
    /// </param>
    public static RunNetwork Contract<TFine, TPricer>(TFine fine, TPricer pricer, LanePlaces places)
        where TFine : IFineGraph
        where TPricer : IEdgeTurnPricer
    {
        var decision = Decisions(fine, places);

        var builder = new TravelGraph.Builder();
        var linkFromPlace = new List<int>();
        var linkToPlace = new List<int>();

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

        JoinTheLinks();

        return new RunNetwork(
            builder.Build(new BoundaryPricer<TPricer>(pricer, [.. firstEdge], [.. lastEdge])),
            [.. pieceOffsets], [.. pieces], [.. stationM], [.. lengthM], places);

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

                var arrivedAtPlace = places.Arriving(lane);
                builder.AddLink(
                    places.AnchorM(startPlace), places.AnchorM(arrivedAtPlace), runLengthM + weightM);
                linkFromPlace.Add(startPlace);
                linkToPlace.Add(arrivedAtPlace);
                pieceOffsets.Add(pieces.Count);
                lengthM.Add(runLengthM);
                firstEdge.Add(start);
                lastEdge.Add(lane);
            }
        }

        // <b>What the coarse graph is told is which link may be left for which</b>, never that some set of
        // them meets somewhere: a place is how the joins are worked out here and is then done with, the way
        // the plan's junctions are done with once the lanes are laid.
        void JoinTheLinks()
        {
            var leavingAt = new int[places.Count + 1];
            foreach (var place in linkFromPlace) leavingAt[place + 1]++;
            for (var place = 1; place <= places.Count; place++) leavingAt[place] += leavingAt[place - 1];

            var cursor = (int[])leavingAt.Clone();
            var leaving = new int[linkFromPlace.Count];
            for (var link = 0; link < linkFromPlace.Count; link++) leaving[cursor[linkFromPlace[link]]++] = link;

            // <b>Every way on leaves the place a link arrives at</b>, so the joins are that list and nothing
            // else: a link ends where its last piece does, and what a body may do there is take one of the
            // links setting off from the same ground (<c>TravelGraph.Builder.Join</c>).
            for (var link = 0; link < linkToPlace.Count; link++)
            {
                var place = linkToPlace[link];
                for (var at = leavingAt[place]; at < leavingAt[place + 1]; at++) builder.Join(link, leaving[at]);
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
    }

    static bool[] Decisions<TFine>(TFine fine, LanePlaces places) where TFine : IFineGraph
    {
        var arriving = new int[places.Count];
        var comingBack = new int[places.Count];
        for (var lane = 0; lane < fine.LaneCount; lane++)
        {
            var place = places.Arriving(lane);
            arriving[place]++;
            if (fine.Reverse(lane) >= 0) comingBack[place]++;
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

            decision[place] = !oneEach || arriving[place] != ways;
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
