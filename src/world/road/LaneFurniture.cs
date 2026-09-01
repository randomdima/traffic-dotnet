using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// The paint each lane meets, projected onto that lane once at load: its own stop bar, and the
/// crossings laid across it in the order it reaches them.
/// </summary>
/// <remarks>
/// Load time and never per tick — neither the paint nor the lane moves, and asking per tick would walk
/// the whole crossing register for every driving car. It is the only place the road graph and the two
/// paint registers are put side by side.
/// </remarks>
internal sealed class LaneFurniture
{
    /// <summary>
    /// How square a lane has to run to a crossing's own axis before the paint is taken to be on it. The
    /// axis runs the way the traffic does, so a lane over the paint is very nearly parallel to it and a
    /// lane on the crossing road is very nearly at a right angle.
    /// </summary>
    const float RunsAcrossThePaint = 0.7f;

    readonly float[] _stopBarM;
    readonly float[] _stopBarThicknessM;
    readonly float[] _crossingSpanM;
    readonly int[] _crossingFirst;
    readonly int[] _crossing;
    readonly float[] _crossingAlongM;
    readonly int[] _laneFirst;
    readonly int[] _crossedLane;
    readonly float[] _crossedAlongM;

    LaneFurniture(
        float[] stopBarM, float[] stopBarThicknessM, float[] crossingSpanM, int[] crossingFirst, int[] crossing,
        float[] crossingAlongM, int[] laneFirst, int[] crossedLane, float[] crossedAlongM)
    {
        _stopBarM = stopBarM;
        _stopBarThicknessM = stopBarThicknessM;
        _crossingSpanM = crossingSpanM;
        _crossingFirst = crossingFirst;
        _crossing = crossing;
        _crossingAlongM = crossingAlongM;
        _laneFirst = laneFirst;
        _crossedLane = crossedLane;
        _crossedAlongM = crossedAlongM;
    }

    public static LaneFurniture Project(CityPlan plan, RoadGraph roads)
    {
        var (barM, thicknessM) = StopBars(plan, roads);
        var spanM = Spans(plan);
        var (first, crossing, alongM) = Crossings(plan, roads, spanM);
        var (laneFirst, crossedLane, crossedAlongM) = LanesUnderCrossings(plan, first, crossing, alongM);

        var most = 0;
        for (var on = 0; on + 1 < laneFirst.Length; on++) most = Math.Max(most, laneFirst[on + 1] - laneFirst[on]);

        return new LaneFurniture(
            barM, thicknessM, spanM, first, crossing, alongM, laneFirst, crossedLane, crossedAlongM)
        {
            MostLanesUnderACrossing = most,
        };
    }

    /// <summary>How far along the lane its painted bar stands, or infinity where nothing was painted on it.</summary>
    public float StopBarAlongM(int lane) => _stopBarM[lane];

    /// <summary>The paint a car stops at the near edge of and has crossed at the far one.</summary>
    public float StopBarThicknessM(int lane) => _stopBarThicknessM[lane];

    /// <summary>
    /// How far each crossing reaches across its road, solved once here because a driver asks it of every
    /// crossing ahead of it, every tick (<see cref="CityPlan.CrossingSpanM"/>).
    /// </summary>
    public float CrossingSpanM(int crossing) => _crossingSpanM[crossing];

    /// <summary>How many lane–crossing pairs there are in all, which is what sizes a per-pair roster.</summary>
    public int CrossingsOnLanes => _crossing.Length;

    /// <summary>
    /// The most lanes any one crossing is laid across. <b>What one walker on the paint costs the road's
    /// book</b>, and therefore the figure the book has to be sized with room for — a dropped stretch here
    /// would be a body on a crossing no driver could see.
    /// </summary>
    public int MostLanesUnderACrossing { get; private init; }

    /// <summary>The crossings this lane runs across, nearest first, with how far along the lane each falls.</summary>
    public CrossingsOnLane CrossingsOn(int lane) => new(this, _crossingFirst[lane], _crossingFirst[lane + 1]);

    internal readonly struct CrossingsOnLane(LaneFurniture furniture, int from, int to)
    {
        public int From => from;

        public int To => to;

        public int CrossingAt(int slot) => furniture._crossing[slot];

        public float AlongM(int slot) => furniture._crossingAlongM[slot];
    }

    /// <summary>
    /// <b>The same pairs the other way up</b>: the lanes one crossing's paint is laid across, with how far
    /// along each of them it falls. What a body <em>on</em> a crossing has to be written into the road's
    /// book against, since the road holds everything as a stretch of a lane.
    /// </summary>
    public LanesUnderACrossing LanesUnder(int crossing) => new(this, _laneFirst[crossing], _laneFirst[crossing + 1]);

    internal readonly struct LanesUnderACrossing(LaneFurniture furniture, int from, int to)
    {
        public int From => from;

        public int To => to;

        public int LaneAt(int slot) => furniture._crossedLane[slot];

        public float AlongM(int slot) => furniture._crossedAlongM[slot];
    }

    /// <summary>
    /// The lane-and-crossing pairs sorted by crossing rather than by lane. It is the projection that has
    /// already been done, turned over — never a second search of the geometry, which would be a second
    /// answer to the same question.
    /// </summary>
    static (int[] First, int[] Lane, float[] AlongM) LanesUnderCrossings(
        CityPlan plan, int[] crossingFirst, int[] crossing, float[] alongM)
    {
        var count = plan.Crosswalks.Count;
        var first = new int[count + 1];
        foreach (var on in crossing) first[on + 1]++;
        for (var at = 1; at < first.Length; at++) first[at] += first[at - 1];

        var cursor = (int[])first.Clone();
        var laneOfSlot = new int[crossing.Length];
        var alongOfSlot = new float[crossing.Length];
        for (var lane = 0; lane + 1 < crossingFirst.Length; lane++)
        {
            for (var slot = crossingFirst[lane]; slot < crossingFirst[lane + 1]; slot++)
            {
                var at = cursor[crossing[slot]]++;
                laneOfSlot[at] = lane;
                alongOfSlot[at] = alongM[slot];
            }
        }

        return (first, laneOfSlot, alongOfSlot);
    }

    /// <summary>
    /// The bars that were painted, not the ones the plan called for: a lane with no bar is a lane
    /// nothing was painted on, and a driver there has nothing to stop at short of the box itself.
    /// </summary>
    /// <remarks>
    /// <b>A lane's own bar is the one it ends at</b>, and only where nothing was painted there is it the one
    /// it meets on its way out of a node — which is the far bar of the crossing a junction with no fork
    /// carries (TER-6), painted for the traffic that has just come through. Taken the other way round, a
    /// lane running from one of those to a lit junction would keep the bar behind it and lose the red one in
    /// front.
    /// </remarks>
    static (float[] AlongM, float[] ThicknessM) StopBars(CityPlan plan, RoadGraph roads)
    {
        var alongM = new float[roads.LaneCount];
        var thicknessM = new float[roads.LaneCount];
        Array.Fill(alongM, float.PositiveInfinity);

        var bars = plan.StopLines;
        for (var pass = 0; pass < 2; pass++)
        {
            for (var bar = 0; bar < bars.Count; bar++)
            {
                var approach = bars.Approach[bar];
                if (approach.LengthSquared() <= 0f) continue;

                approach = Vector2.Normalize(approach);
                var junction = bars.Junction[bar];
                if (junction < 0 || junction >= roads.NodeCount) continue;

                var ends = pass == 0;
                foreach (var lane in ends ? roads.LanesIn(junction) : roads.LanesOut(junction))
                {
                    if (roads.LaneRoad[lane] != bars.Road[bar]) continue;
                    if (!ends && !float.IsPositiveInfinity(alongM[lane])) continue;

                    var at = ends ? roads.EndOf(lane) : roads.StartOf(lane);
                    if (Vector2.Dot(at.Direction, approach) <= 0f) continue;

                    var arcs = roads.ArcsOf(lane);
                    var lengthM = roads.LaneLengthM[lane];
                    alongM[lane] = Spline.ProjectM(arcs, bars.CentreM[bar], ends ? lengthM : 0f, lengthM);
                    thicknessM[lane] = bars.ThicknessM[bar];
                }
            }
        }

        return (alongM, thicknessM);
    }

    /// <summary>The plan's own answer for every crossing at once (TER-6), taken here because it is a projection.</summary>
    static float[] Spans(CityPlan plan)
    {
        var spanM = new float[plan.Crosswalks.Count];
        for (var crossing = 0; crossing < spanM.Length; crossing++) spanM[crossing] = plan.CrossingSpanM(crossing);
        return spanM;
    }

    /// <summary>
    /// A crossing adds no node to the road graph — a zebra is a band of the same carriageway and nothing
    /// turns at one — so a lane's own list is kept rather than a junction's: only the crossing on the arm
    /// being approached counts, and a junction paints its far arm too.
    /// </summary>
    static (int[] First, int[] Crossing, float[] AlongM) Crossings(CityPlan plan, RoadGraph roads, float[] spanM)
    {
        var crossings = plan.Crosswalks;
        var first = new int[roads.LaneCount + 1];
        var found = new List<(int Lane, int Crossing, float AlongM)>();

        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            var axis = crossings.Axis[crossing];
            if (axis.LengthSquared() <= 0f) continue;

            axis = Vector2.Normalize(axis);
            var centreM = crossings.CentreM[crossing];
            var halfSpanM = spanM[crossing] * 0.5f;

            var already = found.Count;
            for (var lane = 0; lane < roads.LaneCount; lane++)
            {
                // The lane's own line has to pass through the paint, which is a question about where the
                // crossing's centre falls on it: the span crosses the road and the lane runs down it, so a
                // lane the paint covers projects onto it within a quarter of the road's width.
                var arcs = roads.ArcsOf(lane);
                var lengthM = roads.LaneLengthM[lane];
                var alongM = Spline.ProjectM(arcs, centreM, lengthM * 0.5f, lengthM);
                var at = Spline.SampleAt(arcs, alongM);
                if ((at.PositionM - centreM).Length() > halfSpanM) continue;

                // And it has to be the same piece of road rather than one passing beside it: the lane runs
                // across the paint, so the two directions agree to a right angle.
                if (!RunsAcross(at.Direction, axis)) continue;
                if (alongM <= 0f || alongM >= lengthM) continue;

                found.Add((lane, crossing, alongM));
            }

            if (found.Count == already) OnTheNodeItself(roads, crossings, crossing, axis, found);
        }

        found.Sort((a, b) => a.Lane != b.Lane ? a.Lane.CompareTo(b.Lane) : a.AlongM.CompareTo(b.AlongM));

        var crossingOfSlot = new int[found.Count];
        var alongOfSlot = new float[found.Count];
        var slot = 0;
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            first[lane] = slot;
            while (slot < found.Count && found[slot].Lane == lane)
            {
                crossingOfSlot[slot] = found[slot].Crossing;
                alongOfSlot[slot] = found[slot].AlongM;
                slot++;
            }
        }

        first[roads.LaneCount] = slot;
        return (first, crossingOfSlot, alongOfSlot);
    }

    /// <summary>
    /// <b>The crossing an inline junction exists to carry</b> (TER-5b), laid across the lanes that meet at
    /// the node rather than projected down one of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is on the node itself and therefore past the end of every lane there</b> — the disc's own
    /// reach further than the paint is wide — so the projection above finds nothing, and a crossing found
    /// by nothing is paint no driver slows for and a walker no driver can see (TER-4c). What the node has
    /// is one carriageway arriving and the same one leaving, and the paint is across both of them: the
    /// arriving lane meets it at its own end and the leaving one at its own start.
    /// </para>
    /// <para>
    /// <b>Only where the junction admits no turns.</b> Anywhere else a crossing is set back onto the arm it
    /// approaches (TER-6) and is found where it lies; laid on the node regardless, one crossing at a
    /// crossroads would be painted across every arm of it.
    /// </para>
    /// </remarks>
    static void OnTheNodeItself(
        RoadGraph roads, CityPlan.CrosswalkArrays crossings, int crossing, Vector2 axis,
        List<(int Lane, int Crossing, float AlongM)> found)
    {
        var junction = crossings.Junction[crossing];
        if (junction < 0 || junction >= roads.NodeCount || roads.LanesIn(junction).Length >= 3) return;

        foreach (var lane in roads.LanesIn(junction))
        {
            if (RunsAcross(roads.EndOf(lane).Direction, axis)) found.Add((lane, crossing, roads.LaneLengthM[lane]));
        }

        foreach (var lane in roads.LanesOut(junction))
        {
            if (RunsAcross(roads.StartOf(lane).Direction, axis)) found.Add((lane, crossing, 0f));
        }
    }

    /// <summary>Whether a lane running this way is one the paint is laid across, to a right angle.</summary>
    static bool RunsAcross(Vector2 direction, Vector2 axis) =>
        MathF.Abs(Vector2.Dot(direction, axis)) >= RunsAcrossThePaint;
}
