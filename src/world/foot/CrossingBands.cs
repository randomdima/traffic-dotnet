using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// <b>The carriageway under a crossing, as stretches of the crossing's own ways</b>: for each way a zebra
/// is made of, the band of it each lane running underneath covers, projected once when the town is laid.
/// </summary>
/// <remarks>
/// <para>
/// <b>A crossing is ground and not a unit.</b> A car drives over the width of its own lane and a body on
/// foot stands in one lane at a time, so what either of them has of a zebra is a band of it — and the two
/// networks can only be held against each other in one measure, which is this one.
/// </para>
/// <para>
/// <b>It is read from both sides and written to by neither</b> (TER-5c.1). A body writes into the book of
/// the network it is on; where the two lie over one another, this says which stretch of the other book
/// stands for the same ground — which lane a body on the paint is standing in, and where on a walk the lane
/// a body was refused begins.
/// </para>
/// <para>
/// <b>It is the turning-over of a projection already made</b> — <see cref="LaneFurniture.LanesUnder"/>
/// says which lanes a crossing is painted across and how far along each of them the paint falls, and this
/// says where those same lanes fall on the crossing. Neither is a search of the geometry at tick time.
/// </para>
/// <para>
/// <b>Per way and never per crossing.</b> The two directions of a zebra are two lines a band apart and a
/// crossing split at a refuge is several stretches, so a lane's band lands at different metres on each of
/// them; a figure held per crossing would be one of those answers used for all of them.
/// </para>
/// </remarks>
internal sealed class CrossingBands
{
    /// <summary>
    /// How finely a crossing's way is walked when the lanes under it are looked for. A quarter of a metre
    /// is an eighth of the narrowest lane a town lays, so no band is missed and none is found that is not
    /// there; the step is load-time work and buys nothing by being coarser.
    /// </summary>
    const float StationM = 0.25f;

    readonly int[] _first;
    readonly Band[] _bands;
    readonly int[] _wayFirst;
    readonly int[] _wayOfCrossing;
    readonly int[] _crossingOfEdge;

    CrossingBands(int[] first, Band[] bands, int[] wayFirst, int[] wayOfCrossing, int[] crossingOfEdge)
    {
        _first = first;
        _bands = bands;
        _wayFirst = wayFirst;
        _wayOfCrossing = wayOfCrossing;
        _crossingOfEdge = crossingOfEdge;
    }

    /// <summary>
    /// One lane's stretch of one crossing way, in that way's own metres, with where the same paint falls
    /// on the lane.
    /// </summary>
    /// <param name="Lane">The lane running under the crossing here.</param>
    /// <param name="FromM">The near edge of its band, measured the way the crossing is walked.</param>
    /// <param name="ToM">And the far edge.</param>
    /// <param name="AlongLaneM">How far along that lane the paint stands, which is the same pair read from the road's side.</param>
    public readonly record struct Band(int Lane, float FromM, float ToM, float AlongLaneM);

    /// <summary>
    /// The lanes running under one way of a crossing, <b>in the order that way meets them</b> — so the
    /// first is the lane a body stepping onto it enters first.
    /// </summary>
    /// <remarks>Empty for every way that is not a crossing, which is nearly all of the pavement.</remarks>
    public ReadOnlySpan<Band> On(int edge) => _bands.AsSpan(_first[edge], _first[edge + 1] - _first[edge]);

    /// <summary>The band one named lane covers of one crossing way, or false where that lane does not run under it.</summary>
    public bool Of(int edge, int lane, out Band band)
    {
        foreach (var at in On(edge))
        {
            if (at.Lane != lane) continue;

            band = at;
            return true;
        }

        band = default;
        return false;
    }

    /// <summary>
    /// The pavement's own ways one crossing is made of — <b>what anybody asking about a whole zebra rather
    /// than about the way one body is walking has to look at</b>, since the pavement holds everything as a
    /// stretch of one of its ways.
    /// </summary>
    /// <remarks>
    /// Every stretch of foot graph is a walking lane of its own, so a crossing is at least the two
    /// directions of it and is a run rather than a pair.
    /// </remarks>
    public ReadOnlySpan<int> WaysOf(int crossing) =>
        _wayOfCrossing.AsSpan(_wayFirst[crossing], _wayFirst[crossing + 1] - _wayFirst[crossing]);

    /// <summary>
    /// Which crossing each stretch of the foot graph <em>is</em>, or <see cref="CityPlan.NoRecord"/> where
    /// it is pavement — what a walker asks a kerb about, and what a walked line carries.
    /// </summary>
    public ReadOnlySpan<int> CrossingOfEdge => _crossingOfEdge;

    /// <summary>The same for one stretch.</summary>
    public int CrossingOf(int edge) => _crossingOfEdge[edge];

    public static CrossingBands Project(
        CityPlan plan, RoadGraph roads, LaneFurniture furniture, WalkingNetwork walking)
    {
        var crossingOfFootEdge = CrossingsOnFootEdges(plan, walking.Foot);
        var edges = walking.Foot.EdgeCount;
        var first = new int[edges + 1];
        var bands = new List<Band>();

        for (var edge = 0; edge < edges; edge++)
        {
            first[edge] = bands.Count;

            var crossing = crossingOfFootEdge[edge];
            if (crossing < 0) continue;

            var line = walking.LaneOf(edge);
            if (line.Length == 0) continue;

            var lengthM = walking.LaneLengthM(edge);
            var depthM = plan.Crosswalks.DepthM[crossing];
            var under = furniture.LanesUnder(crossing);
            for (var slot = under.From; slot < under.To; slot++)
            {
                var lane = under.LaneAt(slot);
                var alongLaneM = under.AlongM(slot);
                if (!BandOf(line, lengthM, roads, lane, alongLaneM, depthM, out var fromM, out var toM)) continue;

                bands.Add(new Band(lane, fromM, toM, alongLaneM));
            }

            bands.Sort(first[edge], bands.Count - first[edge], NearestFirst.Instance);
        }

        first[edges] = bands.Count;

        var (wayFirst, wayOfCrossing) = WaysUnderCrossings(plan.Crosswalks.Count, crossingOfFootEdge);
        return new CrossingBands(first, [.. bands], wayFirst, wayOfCrossing, crossingOfFootEdge);
    }

    /// <summary>
    /// Which crossing each stretch of the foot graph is, filled by the one thing the two structures share:
    /// where the stretch stands.
    /// </summary>
    /// <remarks>
    /// The foot graph does not carry the plan's crossing index: a crossing there is a kind of edge and
    /// nothing else, which is what makes crossing at a crossing structural rather than looked-up.
    /// </remarks>
    static int[] CrossingsOnFootEdges(CityPlan plan, FootGraph foot)
    {
        var of = new int[foot.EdgeCount];
        Array.Fill(of, CityPlan.NoRecord);

        var crossings = plan.Crosswalks;
        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            if (foot.KindOf(edge) != FootEdgeKind.Crossing) continue;

            var middleM = Spline.SampleAt(foot.ArcsOf(edge), foot.LengthM(edge) * 0.5f).PositionM;
            var best = CityPlan.NoRecord;
            var bestDistanceSq = float.MaxValue;
            for (var crossing = 0; crossing < crossings.Count; crossing++)
            {
                var distanceSq = Vector2.DistanceSquared(middleM, crossings.CentreM[crossing]);
                if (distanceSq >= bestDistanceSq) continue;

                (best, bestDistanceSq) = (crossing, distanceSq);
            }

            of[edge] = best;
        }

        return of;
    }

    /// <summary>
    /// The stretch of one crossing way that lies inside one lane's band, <b>walked rather than solved
    /// for</b>: a way is a chain of arcs and a lane bends under it, so where the band begins and ends is a
    /// question about the line and not about an angle between two directions.
    /// </summary>
    /// <remarks>
    /// <b>The lane's own band and its own paint.</b> A station is inside the band when it stands within
    /// half a lane's width of the lane's line and within the crossing's depth of the paint — the second
    /// half being what keeps a way that runs beside a lane for a while from being taken as a way across
    /// it.
    /// </remarks>
    static bool BandOf(
        ReadOnlySpan<ArcSeg> line, float lengthM, RoadGraph roads, int lane, float alongLaneM, float depthM,
        out float fromM, out float toM)
    {
        var on = Spline.SampleAt(roads.ArcsOf(lane), alongLaneM);
        var halfWidthM = roads.LaneWidthM[lane] * 0.5f;

        fromM = float.PositiveInfinity;
        toM = float.NegativeInfinity;

        var stations = Math.Max(1, (int)MathF.Ceiling(lengthM / StationM));
        for (var station = 0; station <= stations; station++)
        {
            var atM = lengthM * station / stations;
            var offM = Spline.SampleAt(line, atM).PositionM - on.PositionM;
            if (MathF.Abs(Vector2.Dot(offM, on.Right)) > halfWidthM) continue;
            if (MathF.Abs(Vector2.Dot(offM, on.Direction)) > depthM) continue;

            fromM = MathF.Min(fromM, atM);
            toM = MathF.Max(toM, atM);
        }

        if (!float.IsFinite(fromM)) return false;

        // A station either way, and outward rather than to the nearer edge: the band's own edge stands
        // between the last station outside it and the first one inside, and a band read short is a car
        // granted paint it drives over — where one read long is a hand's width of lane spoken for twice.
        var stepM = lengthM / stations;
        fromM = MathF.Max(0f, fromM - stepM);
        toM = MathF.Min(lengthM, toM + stepM);
        return true;
    }

    /// <summary>The stretches of foot graph each crossing is made of, as a run per crossing.</summary>
    static (int[] First, int[] Way) WaysUnderCrossings(int crossings, ReadOnlySpan<int> crossingOfFootEdge)
    {
        var first = new int[crossings + 1];
        foreach (var crossing in crossingOfFootEdge)
        {
            if (crossing >= 0) first[crossing + 1]++;
        }

        for (var at = 1; at < first.Length; at++) first[at] += first[at - 1];

        var cursor = (int[])first.Clone();
        var wayOfSlot = new int[first[^1]];
        for (var edge = 0; edge < crossingOfFootEdge.Length; edge++)
        {
            var crossing = crossingOfFootEdge[edge];
            if (crossing >= 0) wayOfSlot[cursor[crossing]++] = edge;
        }

        return (first, wayOfSlot);
    }

    /// <summary>The bands of one way in the order it meets them, which is what makes the first of them the one a body enters first.</summary>
    sealed class NearestFirst : IComparer<Band>
    {
        public static readonly NearestFirst Instance = new();

        public int Compare(Band one, Band other) => one.FromM.CompareTo(other.FromM);
    }
}
