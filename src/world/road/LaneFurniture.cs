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
    readonly int[] _crossingFirst;
    readonly int[] _crossing;
    readonly float[] _crossingAlongM;

    LaneFurniture(
        float[] stopBarM, float[] stopBarThicknessM, int[] crossingFirst, int[] crossing, float[] crossingAlongM)
    {
        _stopBarM = stopBarM;
        _stopBarThicknessM = stopBarThicknessM;
        _crossingFirst = crossingFirst;
        _crossing = crossing;
        _crossingAlongM = crossingAlongM;
    }

    /// <param name="bars">
    /// <b>The town's own bars</b> (<see cref="Road.StopBars"/>, TER-6), the laying the picture paints and the
    /// lights hold at — never a second one.
    /// </param>
    /// <param name="zebras">
    /// <b>The town's own crossings</b> (<see cref="Crossings"/>, TER-6) and never a second laying of them:
    /// what the walk is cut at is what the lanes carry, or a body is on paint the traffic has never heard
    /// of.
    /// </param>
    public static LaneFurniture Project(StopBars bars, Crossings zebras, RoadGraph roads)
    {
        var (barM, thicknessM) = BarsOn(bars, roads.LaneCount);
        var (first, crossing, alongM) = Crossings(zebras, roads, zebras.SpanM.ToArray());
        return new LaneFurniture(barM, thicknessM, first, crossing, alongM);
    }

    /// <summary>How far along the lane its painted bar stands, or infinity where nothing was painted on it.</summary>
    public float StopBarAlongM(int lane) => _stopBarM[lane];

    /// <summary>The paint a car stops at the near edge of and has crossed at the far one.</summary>
    public float StopBarThicknessM(int lane) => _stopBarThicknessM[lane];

    /// <summary>How many lane–crossing pairs there are in all, which is what sizes a per-pair roster.</summary>
    public int CrossingsOnLanes => _crossing.Length;

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
    /// <b>Each lane's own bar</b>, the one it ends at, carried over from the laying in the lane's own metres —
    /// the figure the bar was placed by, and not a projection of its centre back onto the lane. Infinity where
    /// nothing was painted, and a driver there has nothing to stop at short of the box itself.
    /// </summary>
    static (float[] AlongM, float[] ThicknessM) BarsOn(StopBars bars, int laneCount)
    {
        var alongM = new float[laneCount];
        var thicknessM = new float[laneCount];
        Array.Fill(alongM, float.PositiveInfinity);

        for (var bar = 0; bar < bars.Count; bar++)
        {
            alongM[bars.Lane[bar]] = bars.AlongM[bar];
            thicknessM[bars.Lane[bar]] = bars.ThicknessM[bar];
        }

        return (alongM, thicknessM);
    }

    /// <summary>
    /// A crossing adds no node to the road graph — a zebra is a band of the same carriageway and nothing
    /// turns at one — so a lane's own list is kept rather than a junction's: only the crossing on the arm
    /// being approached counts, and a junction paints its far arm too.
    /// </summary>
    static (int[] First, int[] Crossing, float[] AlongM) Crossings(
        Crossings crossings, RoadGraph roads, float[] spanM)
    {
        var first = new int[roads.LaneCount + 1];
        var found = new List<(int Lane, int Crossing, float AlongM)>();
        var near = new int[Math.Max(1, roads.LaneCount)];

        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            var axis = crossings.Axis[crossing];
            if (axis.LengthSquared() <= 0f) continue;

            axis = Vector2.Normalize(axis);
            var centreM = crossings.CentreM[crossing];
            var halfSpanM = spanM[crossing] * 0.5f;

            // <b>Only the lanes that could pass under the paint</b>, in lane order: asked of every lane, the
            // town's crossings times its lanes was the one part of standing a town up that grew with its
            // square.
            var offered = Math.Min(roads.LanesAround(centreM, halfSpanM, near), near.Length);
            Array.Sort(near, 0, offered);

            var already = found.Count;
            for (var candidate = 0; candidate < offered; candidate++)
            {
                var lane = near[candidate];
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
        RoadGraph roads, Crossings crossings, int crossing, Vector2 axis,
        List<(int Lane, int Crossing, float AlongM)> found)
    {
        var junction = crossings.Junction[crossing];
        if (junction < 0 || junction >= roads.JunctionCount || roads.LanesIntoJunction(junction).Length >= 3) return;

        foreach (var lane in roads.LanesIntoJunction(junction))
        {
            if (RunsAcross(roads.EndOf(lane).Direction, axis)) found.Add((lane, crossing, roads.LaneLengthM[lane]));
        }

        foreach (var lane in roads.LanesOutOfJunction(junction))
        {
            if (RunsAcross(roads.StartOf(lane).Direction, axis)) found.Add((lane, crossing, 0f));
        }
    }

    /// <summary>Whether a lane running this way is one the paint is laid across, to a right angle.</summary>
    static bool RunsAcross(Vector2 direction, Vector2 axis) =>
        MathF.Abs(Vector2.Dot(direction, axis)) >= RunsAcrossThePaint;
}
