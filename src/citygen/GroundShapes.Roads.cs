using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// What the roads say about one point: the level of the answer that follows a curve, and the only level
/// that does.
/// </summary>
/// <remarks>
/// <b>The two are not ranked against one another here</b>, because they are not ranked against one another
/// at all — each takes its place in <see cref="GroundShapes"/>'s one order among the shapes that belong to
/// no road, and a deck loses to water where a zebra beats it.
///
/// <b>Which road's carriageway a point is on is not among them.</b> It is the lanes that say that
/// (<see cref="GroundShapes.At"/>): a road's own band runs on to the junctions at its ends while its lanes
/// stop short of them, so the road answered carriageway over a sliver at every mouth in the town.
///
/// <b>So a road with neither a deck nor a stretch of paint on it answers nothing</b>, and
/// <see cref="GroundShapes.Answers"/> is what keeps it out of the index altogether: both figures here are
/// read off a road's own runs, a road with no runs leaves both untouched, and the shipped city carries paint
/// on a quarter of its roads and a deck on none of them.
/// </remarks>
internal readonly record struct RoadGround(bool Deck, bool Crossing);

internal sealed partial class GroundShapes
{
    /// <summary>
    /// How many roads may pass within reach of one point before the index's answer stops being the whole
    /// one. Four arms of a junction and their neighbours is six, and only the roads that can answer at all
    /// are in the index (<see cref="Answers"/>) — so this is generous by a wide margin, which it can afford
    /// to be: the frame is not zeroed (<c>SkipLocalsInit</c>), so room nothing fills costs the stack pointer
    /// and nothing else. <see cref="Roads"/> falls back to every road in the town rather than truncate.
    /// </summary>
    const int MostRoadsNear = 64;

    ChainIndex _roadIndex = null!;
    float[] _roadHalfM = [];
    float[] _roadLengthM = [];
    float[] _roadReachM = [];
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
    /// A point past either end of a road is not on its carriageway, however near the end it stands. The
    /// projection clamps to the chain's own ends, so what is left of the offset along the road's direction
    /// is what says the point is beyond it — which squares the end of every road off, exactly as the ribbon
    /// that draws it is squared off. <b>The walk turns that end</b> (TER-3c.6): it is the ground within a
    /// walk of the tarmac and the tarmac stops square, so past the end the band is the square end grown by
    /// a walk and the corner of it is an arc.
    /// </remarks>
    RoadGround Roads(Vector2 pointM) => Roads(_ownScan.Roads, pointM);

    /// <inheritdoc cref="Roads(Vector2)"/>
    /// <remarks>
    /// <b>The working set is not zeroed.</b> <see cref="ChainIndex.Near"/> fills every slot below the count
    /// it returns before anything reads one, and nothing here reads past that count — so initialising the
    /// frame is a kilobyte of stores a query pays and never reads, on the path a tick asks most.
    /// </remarks>
    [SkipLocalsInit]
    RoadGround Roads(ChainIndex.Scan scan, Vector2 pointM)
    {
        Span<int> near = stackalloc int[MostRoadsNear];
        Span<float> alongM = stackalloc float[MostRoadsNear];
        var found = _roadIndex.Near(scan, pointM, _farthestReachM, near, alongM);

        var deck = false;
        var crossing = false;
        var count = Math.Min(found, near.Length);
        for (var index = 0; index < count; index++)
        {
            Weigh(near[index], alongM[index], pointM, ref deck, ref crossing);
        }

        // The index answered with more roads than there was room for, so what it gave back is part of the
        // answer rather than the answer. Every road in the town is the only thing that is still the whole of
        // it (BucketGrid.Query keeps the same bargain) — every road that can answer, which is the same set
        // the index holds.
        if (found > near.Length)
        {
            for (var road = 0; road < _roadHalfM.Length; road++)
            {
                if (!Answers(road)) continue;

                var arcs = _pieces.Roads.SegmentsOf(road);
                var atM = Spline.ProjectM(arcs, pointM, _roadLengthM[road] * 0.5f, _roadLengthM[road]);
                Weigh(road, atM, pointM, ref deck, ref crossing);
            }
        }

        return new RoadGround(deck, crossing);
    }

    void Weigh(int road, float atM, Vector2 pointM, ref bool deck, ref bool crossing)
    {
        var arcs = _pieces.Roads.SegmentsOf(road);
        var on = Spline.SampleAt(arcs, atM);
        var offsetM = pointM - on.PositionM;

        // How far past an end the projection had to clamp to the point stands, which is nought anywhere
        // along the road itself.
        var alongM = Vector2.Dot(offsetM, on.Direction);
        var pastM = atM <= 0f
            ? MathF.Max(0f, -alongM)
            : atM >= _roadLengthM[road] ? MathF.Max(0f, alongM) : 0f;

        var acrossM = MathF.Abs(Vector2.Dot(offsetM, on.Right));
        if (acrossM > _roadReachM[road]) return;

        if (pastM > 0f) return;

        if (acrossM <= _roadHalfM[road])
        {
            crossing |= Covers(_paintAt, _paintFromM, _paintToM, road, atM);
        }

        for (var run = _deckAt[road]; run < _deckAt[road + 1]; run++)
        {
            if (atM < _deckFromM[run] || atM > _deckToM[run] || acrossM > _deckHalfM[run]) continue;

            deck = true;
        }
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
    /// <remarks>
    /// <b>The runs are struck before the index is, because they decide what goes in it</b>
    /// (<see cref="Answers"/>). A road carrying neither is a chain a query walks, projects onto and learns
    /// nothing from, and there is no early-out to save it: the loop has to weigh every candidate to
    /// establish that none of them is painted.
    /// </remarks>
    void LayTheRoads(GroundPieces plan, SimConfig config)
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

        for (var road = 0; road < roads; road++)
        {
            _roadHalfM[road] = plan.Roads.WidthM[road] * 0.5f;
            _roadLengthM[road] = Spline.TotalLengthM(plan.Roads.SegmentsOf(road));
        }

        Runs(plan.Bridges.Count, plan.Bridges.Road, _deckAt);
        var deck = new int[roads];
        Array.Copy(_deckAt, deck, roads);
        for (var bridge = 0; bridge < plan.Bridges.Count; bridge++)
        {
            var road = plan.Bridges.Road[bridge];
            if (road < 0) continue;

            var run = deck[road]++;
            _deckFromM[run] = plan.Bridges.FromM[bridge];
            _deckToM[run] = plan.Bridges.ToM[bridge];

            // <b>The deck is the deck and carries no pavement of its own</b>: grown to the road's own half
            // plus a walk, a bridge laid the town's pavement across its margin by arithmetic of its own —
            // the one thing every line beside a road is no longer allowed to be (TER-3c.3). What the margin
            // outside the carriageway is, is the ground a parapet stands on, and the walk that ought to
            // cross it is the boundary's to strike once the boundary can cross water
            // (<c>docs/index.md#known-gaps</c>).
            _deckHalfM[run] = plan.Bridges.DeckWidthM[bridge] * 0.5f;
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

        var index = new ChainIndex.Builder();
        var farthestM = 0f;
        for (var road = 0; road < roads; road++)
        {
            // <b>The road's own half and nothing beside it.</b> It was widened by a walk here, which is the
            // last place the pavement was a figure in the ground answer — and the walk comes back as the
            // boundary moved by it (TER-7b) rather than as a road grown by it.
            var reachM = _roadHalfM[road];
            for (var run = _deckAt[road]; run < _deckAt[road + 1]; run++)
            {
                reachM = MathF.Max(reachM, _deckHalfM[run]);
            }

            _roadReachM[road] = reachM;
            if (!Answers(road)) continue;

            index.Add(road, plan.Roads.SegmentsOf(road), _roadLengthM[road]);

            // <b>The farthest of the roads in the index and not of the roads in the town</b>, since it is
            // the radius every query reads its cells by: a road nothing asks about may not widen the ring.
            farthestM = MathF.Max(farthestM, reachM);
        }

        _roadIndex = index.Seal(config.Terrain.GroundBucketM);
        _farthestReachM = farthestM;
    }

    /// <summary>
    /// <b>Whether this road has anything to say about a point at all</b>: a stretch of paint on it, or a deck
    /// carried over something. Those are the whole of <see cref="RoadGround"/>, so a road with neither is a
    /// candidate that costs a projection and answers no.
    /// </summary>
    bool Answers(int road) =>
        _deckAt[road + 1] > _deckAt[road] || _paintAt[road + 1] > _paintAt[road];

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
