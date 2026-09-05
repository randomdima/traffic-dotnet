using System.Numerics;

namespace TrafficSimulation.World.Road;

/// <summary>
/// What a network has to say for <see cref="LanePlaces"/> to work out where its lanes meet: how many lanes
/// it has, which of them each may leave for, which is the same stretch travelled back, and where each line
/// begins and ends.
/// </summary>
internal interface ILaneEnds
{
    int LaneCount { get; }

    /// <summary>The same piece travelled the other way, or negative where there is none.</summary>
    int Reverse(int lane);

    /// <summary>The lanes a body on this one may leave for at the end of it, which is what its connectors say.</summary>
    ReadOnlySpan<int> Onward(int lane);

    Vector2 StartsAtM(int lane);

    Vector2 EndsAtM(int lane);
}

/// <summary>
/// <b>Where a network's lanes meet, worked out from the connectors and never read off a node table.</b> Two
/// lane ends are one place when a connector runs between them, or when they are the two ends of one stretch
/// travelled either way — and a place is what a chain of those comes to.
/// </summary>
/// <remarks>
/// <para>
/// <b>A place is not a junction.</b> A junction is something a plan authored, sized a disc for and paved a
/// corner on; a place is the arithmetic answer to "which lane ends are the same ground", and it carries no
/// centre, no radius, no signal and no identity of the plan's. Nothing that reads one can learn what an
/// intersection is, which is the point: what the town runs on is lanes and the connectors between them, and
/// a junction is the shape a set of crossed lanes happens to make (TER-5d).
/// </para>
/// <para>
/// <b>The second clause is what a dead end and a car park's frontage rest on.</b> No box admits the movement
/// that turns a car round (TER-5f), so the lane into a dead end has no connector to the lane coming back out
/// of it; joined only by their connectors the two would be different places, and then a body lying across
/// the head of the close would hold one of them and not the other, and a leg could not be priced round a
/// bay (GEN-4l) because nothing would offer the pair.
/// </para>
/// <para>
/// <b>One derivation, read by the two things that need it</b> (SIM-7): the contraction that decides where a
/// run of road ends (<see cref="Routing.RunNetwork"/>) and the walk that lays a body onto the ground it is
/// standing on (<see cref="GroundUnder"/>). Worked out twice, the router and the claims would be entitled to
/// disagree about which lane ends are one piece of the world.
/// </para>
/// </remarks>
internal sealed class LanePlaces
{
    /// <summary>Where a lane runs out onto no place of this network's — a bay's way onto the carriageway.</summary>
    public const int NoPlace = -1;

    readonly int[] _placeOfEnd;
    readonly Vector2[] _anchorM;
    readonly int[] _leavingAt;
    readonly int[] _leaving;
    readonly int[] _arrivingAt;
    readonly int[] _arriving;

    LanePlaces(
        int[] placeOfEnd, int count, Vector2[] anchorM, int[] leavingAt, int[] leaving, int[] arrivingAt,
        int[] arriving)
    {
        _placeOfEnd = placeOfEnd;
        _anchorM = anchorM;
        _leavingAt = leavingAt;
        _leaving = leaving;
        _arrivingAt = arrivingAt;
        _arriving = arriving;
        Count = count;
    }

    public int Count { get; }

    /// <summary>The place a lane sets off from.</summary>
    public int Starting(int lane) => _placeOfEnd[lane * 2];

    /// <summary>And the place it arrives at.</summary>
    public int Arriving(int lane) => _placeOfEnd[(lane * 2) + 1];

    /// <summary>Where the place stands: the middle of the lane ends that meet there.</summary>
    public Vector2 AnchorM(int place) => _anchorM[place];

    public ReadOnlySpan<int> LanesLeaving(int place) =>
        _leaving.AsSpan(_leavingAt[place], _leavingAt[place + 1] - _leavingAt[place]);

    public ReadOnlySpan<int> LanesArriving(int place) =>
        _arriving.AsSpan(_arrivingAt[place], _arrivingAt[place + 1] - _arrivingAt[place]);

    /// <summary>The most lanes any one place has an end at, arriving and leaving counted apart.</summary>
    public int MostLanesAtOne { get; private set; }

    public static LanePlaces Of<TEnds>(TEnds lanes) where TEnds : ILaneEnds
    {
        var ends = lanes.LaneCount * 2;
        var parent = new int[ends];
        for (var end = 0; end < ends; end++) parent[end] = end;

        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            foreach (var onto in lanes.Onward(lane)) Join(End(lane), Start(onto));

            var reverse = lanes.Reverse(lane);
            if (reverse >= 0) Join(End(lane), Start(reverse));
        }

        var placeOf = new int[ends];
        var count = 0;
        for (var end = 0; end < ends; end++)
        {
            var root = Root(end);
            if (root == end) placeOf[end] = count++;
        }

        for (var end = 0; end < ends; end++) placeOf[end] = placeOf[Root(end)];

        var anchorM = new Vector2[count];
        var met = new int[count];
        var leavingAt = new int[count + 1];
        var arrivingAt = new int[count + 1];
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            Meet(placeOf[Start(lane)], lanes.StartsAtM(lane));
            Meet(placeOf[End(lane)], lanes.EndsAtM(lane));
            leavingAt[placeOf[Start(lane)] + 1]++;
            arrivingAt[placeOf[End(lane)] + 1]++;
        }

        for (var place = 0; place < count; place++) anchorM[place] /= Math.Max(1, met[place]);
        for (var place = 1; place <= count; place++)
        {
            leavingAt[place] += leavingAt[place - 1];
            arrivingAt[place] += arrivingAt[place - 1];
        }

        var leavingCursor = (int[])leavingAt.Clone();
        var arrivingCursor = (int[])arrivingAt.Clone();
        var leaving = new int[lanes.LaneCount];
        var arriving = new int[lanes.LaneCount];
        for (var lane = 0; lane < lanes.LaneCount; lane++)
        {
            leaving[leavingCursor[placeOf[Start(lane)]]++] = lane;
            arriving[arrivingCursor[placeOf[End(lane)]]++] = lane;
        }

        var places = new LanePlaces(placeOf, count, anchorM, leavingAt, leaving, arrivingAt, arriving);
        for (var place = 0; place < count; place++)
        {
            places.MostLanesAtOne = Math.Max(
                places.MostLanesAtOne,
                places.LanesArriving(place).Length + places.LanesLeaving(place).Length);
        }

        return places;

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
}
