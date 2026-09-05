using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// What the roads say about one point: the level of the answer that follows a curve, and the only level
/// that does.
/// </summary>
/// <remarks>
/// The four are not ranked against one another here because they are not ranked against one another at
/// all — each takes its place in <see cref="GroundShapes"/>'s one order among the shapes that belong to
/// no road, and a walk loses to water where a carriageway beats it.
/// </remarks>
internal readonly record struct RoadGround(bool Walk, bool Deck, bool Carriageway, bool Crossing);

internal sealed partial class GroundShapes
{
    /// <summary>
    /// How many roads may pass within reach of one point before the index's answer stops being the whole
    /// one. Four arms of a junction and their neighbours is six; this is that with room to spare, and
    /// <see cref="Roads"/> falls back to every road in the town rather than truncate.
    /// </summary>
    const int MostRoadsNear = 24;

    ChainIndex _roadIndex = null!;
    float[] _roadHalfM = [];
    float[] _roadLengthM = [];
    float[] _roadReachM = [];
    float _walkM;
    float _farthestReachM;

    /// <summary>Count + 1 entries: road <c>i</c>'s decks are the runs at <c>[_deckAt[i].._deckAt[i + 1]]</c>.</summary>
    int[] _deckAt = [0];

    float[] _deckFromM = [];
    float[] _deckToM = [];
    float[] _deckHalfM = [];

    /// <summary>The same, for the paint a walker is permitted to cross on.</summary>
    int[] _paintAt = [0];

    float[] _paintFromM = [];
    float[] _paintToM = [];

    /// <summary>
    /// Everything the roads have to say about a point, in one walk of the ones that reach it.
    /// <b>The curve is left behind here</b>: a road is projected onto once, and what the point is to that
    /// road is a distance along it and a signed offset across it — after which a carriageway, a walk, a
    /// deck and a zebra are four intervals in a frame with no bend left in it.
    /// </summary>
    /// <remarks>
    /// A point past either end of a road is not on it, however near the end it stands. The projection
    /// clamps to the chain's own ends, so what is left of the offset along the road's direction is what
    /// says the point is beyond it — which squares the end of every road off, exactly as the ribbon that
    /// draws it is squared off, and leaves the ground a road runs into to the junction that owns it.
    /// </remarks>
    RoadGround Roads(Vector2 pointM)
    {
        Span<int> near = stackalloc int[MostRoadsNear];
        Span<float> alongM = stackalloc float[MostRoadsNear];
        var found = _roadIndex.Near(pointM, _farthestReachM, near, alongM);

        var walk = false;
        var deck = false;
        var carriageway = false;
        var crossing = false;
        var count = Math.Min(found, near.Length);
        for (var index = 0; index < count; index++)
        {
            Weigh(near[index], alongM[index], pointM, ref walk, ref deck, ref carriageway, ref crossing);
        }

        // The index answered with more roads than there was room for, so what it gave back is part of the
        // answer rather than the answer. Every road in the town is the only thing that is still the whole of
        // it (BucketGrid.Query keeps the same bargain).
        if (found > near.Length)
        {
            for (var road = 0; road < _roadHalfM.Length; road++)
            {
                var arcs = _pieces.Roads.SegmentsOf(road);
                var atM = Spline.ProjectM(arcs, pointM, _roadLengthM[road] * 0.5f, _roadLengthM[road]);
                Weigh(road, atM, pointM, ref walk, ref deck, ref carriageway, ref crossing);
            }
        }

        return new RoadGround(walk, deck, carriageway, crossing);
    }

    void Weigh(
        int road, float atM, Vector2 pointM, ref bool walk, ref bool deck, ref bool carriageway,
        ref bool crossing)
    {
        var arcs = _pieces.Roads.SegmentsOf(road);
        var on = Spline.SampleAt(arcs, atM);
        var offsetM = pointM - on.PositionM;

        // Past an end the projection had to clamp to, and so past the road.
        var beyondM = Vector2.Dot(offsetM, on.Direction);
        if ((atM <= 0f && beyondM < 0f) || (atM >= _roadLengthM[road] && beyondM > 0f)) return;

        var acrossM = MathF.Abs(Vector2.Dot(offsetM, on.Right));
        if (acrossM > _roadReachM[road]) return;

        var halfM = _roadHalfM[road];
        if (acrossM <= halfM)
        {
            carriageway = true;
            crossing |= Covers(_paintAt, _paintFromM, _paintToM, road, atM);
        }
        else if (acrossM <= halfM + _walkM)
        {
            walk = true;
        }

        for (var run = _deckAt[road]; run < _deckAt[road + 1]; run++)
        {
            if (atM < _deckFromM[run] || atM > _deckToM[run] || acrossM > _deckHalfM[run]) continue;

            deck = true;
        }
    }

    /// <summary>
    /// Whether any road's paving — its carriageway or the walk either side of it — stands within reach of a
    /// point. <b>Asked of the roads and never by sampling the ground round the point</b>: how far the
    /// nearest paving is, is a fact about the shapes, and a lattice fine enough not to step over a band is
    /// a hundred and fifty questions where this is one.
    /// </summary>
    bool RoadPavingWithin(Vector2 pointM, float reachM)
    {
        Span<int> near = stackalloc int[MostRoadsNear];
        Span<float> alongM = stackalloc float[MostRoadsNear];
        var found = _roadIndex.Near(pointM, _farthestReachM + reachM, near, alongM);
        var count = Math.Min(found, near.Length);
        for (var index = 0; index < count; index++)
        {
            if (Within(near[index], alongM[index], pointM, reachM)) return true;
        }

        if (found <= near.Length) return false;

        for (var road = 0; road < _roadHalfM.Length; road++)
        {
            var arcs = _pieces.Roads.SegmentsOf(road);
            var atM = Spline.ProjectM(arcs, pointM, _roadLengthM[road] * 0.5f, _roadLengthM[road]);
            if (Within(road, atM, pointM, reachM)) return true;
        }

        return false;
    }

    /// <summary>
    /// One road's paving, grown by the reach in both directions — <b>along its own ends as well as across
    /// it</b>, since a point off the end of a street is that far from the paving there.
    /// </summary>
    bool Within(int road, float atM, Vector2 pointM, float reachM)
    {
        var arcs = _pieces.Roads.SegmentsOf(road);
        var on = Spline.SampleAt(arcs, atM);
        var offsetM = pointM - on.PositionM;
        var beyondM = MathF.Abs(Vector2.Dot(offsetM, on.Direction));
        var acrossM = MathF.Abs(Vector2.Dot(offsetM, on.Right));
        return beyondM <= reachM && acrossM <= _roadReachM[road] + reachM;
    }

    static bool Covers(int[] at, float[] fromM, float[] toM, int road, float atM)
    {
        for (var run = at[road]; run < at[road + 1]; run++)
        {
            if (atM >= fromM[run] && atM <= toM[run]) return true;
        }

        return false;
    }

    /// <summary>
    /// The roads laid over the index that answers which of them reach a point, and the stretches of each
    /// one that are something other than plain carriageway <b>reduced to that road's own frame</b> while
    /// the town is being laid — so a deck is two distances along a road and a zebra is two more, and
    /// nothing on a tick asks where a crossing stands in the world.
    /// </summary>
    void LayTheRoads(GroundPieces plan, SimConfig config, float walkM)
    {
        var roads = plan.Roads.Count;
        _roadHalfM = new float[roads];
        _roadLengthM = new float[roads];
        _roadReachM = new float[roads];
        _deckAt = new int[roads + 1];
        _paintAt = new int[roads + 1];
        _deckFromM = new float[Named(plan.Bridges.Count, plan.Bridges.Road)];
        _deckToM = new float[_deckFromM.Length];
        _deckHalfM = new float[_deckFromM.Length];
        _paintFromM = new float[Named(plan.Crosswalks.Count, plan.Crosswalks.Road)];
        _paintToM = new float[_paintFromM.Length];

        var index = new ChainIndex.Builder();
        for (var road = 0; road < roads; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            _roadHalfM[road] = plan.Roads.WidthM[road] * 0.5f;
            _roadLengthM[road] = Spline.TotalLengthM(arcs);
            index.Add(road, arcs, _roadLengthM[road]);
        }

        _roadIndex = index.Seal(config.Terrain.GroundBucketM);

        Runs(plan.Bridges.Count, plan.Bridges.Road, _deckAt);
        var deck = new int[roads];
        Array.Copy(_deckAt, deck, roads);
        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            var road = plan.Bridges.Road[bridge];
            if (road < 0) continue;

            var deckWalkM = plan.Bridges.PavementWidthM[bridge] > 0f ? plan.Bridges.PavementWidthM[bridge] : walkM;
            var run = deck[road]++;
            _deckFromM[run] = plan.Bridges.FromM[bridge];
            _deckToM[run] = plan.Bridges.ToM[bridge];
            _deckHalfM[run] = MathF.Max(
                plan.Bridges.DeckWidthM[bridge] * 0.5f, _roadHalfM[road] + deckWalkM);
        }

        Runs(plan.Crosswalks.Count, plan.Crosswalks.Road, _paintAt);
        var paint = new int[roads];
        Array.Copy(_paintAt, paint, roads);
        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            var road = plan.Crosswalks.Road[crossing];
            if (road < 0) continue;

            var arcs = plan.Roads.SegmentsOf(road);
            var lengthM = _roadLengthM[road];
            var centreM = plan.Crosswalks.CentreM[crossing];
            var atM = Spline.ProjectM(arcs, centreM, lengthM * 0.5f, lengthM);

            // The paint is a depth along its own axis, and the axis is only as square to the road as the
            // generator managed. What that depth costs the road it is laid across is the depth divided by
            // how much of the axis runs along the road — the same skew CityPlan.CrossingSpanM charges the
            // span for, seen from the other side.
            var axis = plan.Crosswalks.Axis[crossing];
            var alongItsRoad = axis.LengthSquared() > 0f
                ? MathF.Abs(Vector2.Dot(Spline.SampleAt(arcs, atM).Direction, Vector2.Normalize(axis)))
                : 1f;
            var halfDepthM = plan.Crosswalks.DepthM[crossing] * 0.5f / MathF.Max(alongItsRoad, LeastAlongItsRoad);

            var run = paint[road]++;
            _paintFromM[run] = atM - halfDepthM;
            _paintToM[run] = atM + halfDepthM;
        }

        var farthestM = 0f;
        for (var road = 0; road < roads; road++)
        {
            var reachM = _roadHalfM[road] + walkM;
            for (var run = _deckAt[road]; run < _deckAt[road + 1]; run++)
            {
                reachM = MathF.Max(reachM, _deckHalfM[run]);
            }

            _roadReachM[road] = reachM;
            farthestM = MathF.Max(farthestM, reachM);
        }

        _farthestReachM = farthestM;
    }

    /// <summary>
    /// Count + 1 offsets over records that each name a road, by counting them into place. A record naming
    /// no road at all is dropped, which is what the runs are shorter than the arrays they came from by.
    /// </summary>
    static void Runs(int count, int[] road, int[] at)
    {
        for (var record = 0; record < count; record++)
        {
            if (road[record] >= 0) at[road[record] + 1]++;
        }

        for (var next = 1; next < at.Length; next++) at[next] += at[next - 1];
    }

    static int Named(int count, int[] road)
    {
        var named = 0;
        for (var record = 0; record < count; record++)
        {
            if (road[record] >= 0) named++;
        }

        return named;
    }

    /// <summary>
    /// How square to its road a crossing is held while what it costs that road is solved — the same eighth
    /// of a turn <see cref="CityPlan.CrossingSpanM"/> holds the span to, because it is the same skew.
    /// </summary>
    const float LeastAlongItsRoad = 0.7071068f;
}
