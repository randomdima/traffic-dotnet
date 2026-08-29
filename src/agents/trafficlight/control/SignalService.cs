using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.TrafficLight.Control;

/// <summary>
/// <b>The one town-wide lookup</b>: what colour an approach is showing, and what colour a crossing is.
/// Cars and walkers query this and <em>neither ever holds a bundle</em>.
/// </summary>
/// <remarks>
/// <para>
/// Keeping it one lookup is what makes the driver's crossing exemption safe: a driver reads the
/// <em>pedestrian</em> side of the same table to know the walkers are being held, so what a driver may
/// do and what the people on the kerb have been told can never disagree.
/// </para>
/// <para>
/// <b>An arm's axis is decided once, when the town is laid, off the bearing it meets the junction at.</b>
/// The first arm the graph lists is the reference; every other arm is on that axis if it lies within
/// 45° of it <em>modulo a half turn</em>, and on the other axis if it does not. Taking it modulo a half
/// turn is what puts the two ends of one road on one axis — which is TLT-4's "both ends of a road always
/// show the same colour", reached by the shape of the table rather than by a check.
/// </para>
/// <para>
/// <b>There is no state here and nothing to step.</b> A light is a timer, the timer is the world clock
/// plus the junction's own offset, and the offset is the plan's — drawn from the world seed when the
/// town was laid (TLT-3), so two runs of one town light it the same way.
/// </para>
/// </remarks>
internal sealed class SignalService
{
    public const int NoAxis = -1;

    readonly SimConfig _config;
    readonly bool[] _lit;
    readonly float[] _offsetS;
    readonly int[] _laneAxis;

    /// <summary>Which junction each lane arrives at, so a colour is a load rather than a call back into the graph.</summary>
    readonly int[] _laneJunction;

    readonly int[] _crossingAxis;
    readonly int[] _crossingJunction;

    SignalService(
        SimConfig config, bool[] lit, float[] offsetS, int[] laneAxis, int[] laneJunction, int[] crossingAxis,
        int[] crossingJunction)
    {
        _config = config;
        _lit = lit;
        _offsetS = offsetS;
        _laneAxis = laneAxis;
        _laneJunction = laneJunction;
        _crossingAxis = crossingAxis;
        _crossingJunction = crossingJunction;
    }

    public int JunctionCount => _lit.Length;

    public int CrossingCount => _crossingAxis.Length;

    /// <summary>Whether this junction carries a bundle at all (TLT-3). An unlit one publishes nothing.</summary>
    public bool Lit(int junction) => junction >= 0 && junction < _lit.Length && _lit[junction];

    /// <summary>Which axis of its junction a lane arrives on, or <see cref="NoAxis"/> if that junction is unlit.</summary>
    public int AxisOfLane(int lane) => lane >= 0 && lane < _laneAxis.Length ? _laneAxis[lane] : NoAxis;

    /// <summary>Whether a crossing is governed by a light, rather than by the give-way rule at the kerb.</summary>
    public bool CrossingIsLit(int crossing) =>
        crossing >= 0 && crossing < _crossingAxis.Length && _crossingAxis[crossing] != NoAxis;

    /// <summary>
    /// What the traffic arriving on this lane is being shown. <b>An approach at an unlit junction is
    /// green</b> — there is no light to obey, and a driver that read a red there would stop for nothing.
    /// </summary>
    public SignalColour ForApproach(int lane, float timeS)
    {
        var axis = AxisOfLane(lane);
        if (axis == NoAxis) return SignalColour.Green;

        return SignalCycle.ForAxis(_config, axis, _offsetS[_laneJunction[lane]], timeS);
    }

    /// <summary>What a crossing is showing. An unlit crossing shows red: there is nothing telling a walker it may go.</summary>
    public SignalColour ForCrossing(int crossing, float timeS)
    {
        if (!CrossingIsLit(crossing)) return SignalColour.Red;

        return SignalCycle.ForCrossing(_config, _crossingAxis[crossing], _offsetS[_crossingJunction[crossing]], timeS);
    }

    public static SignalService Build(CityPlan plan, RoadGraph roads, SimConfig config)
    {
        var junctions = plan.Junctions;
        var lit = new bool[roads.NodeCount];
        var offsetS = new float[roads.NodeCount];
        for (var junction = 0; junction < roads.NodeCount && junction < junctions.Count; junction++)
        {
            lit[junction] = junctions.Lit[junction] && AdmitsConflictingMovements(roads, junction);
            offsetS[junction] = junctions.PhaseOffsetS[junction];
        }

        // The reference bearing of each junction, taken off the first arm the graph lists there. Which
        // arm that is does not matter — what matters is that it is the same one every time the town is
        // read, which it is, because the graph is laid from the plan in the plan's own order.
        var reference = new Vector2[roads.NodeCount];
        for (var node = 0; node < roads.NodeCount; node++)
        {
            var arms = roads.LanesIn(node);
            reference[node] = arms.Length > 0 ? roads.EndOf(arms[0]).Direction : Vector2.UnitX;
        }

        var laneAxis = new int[roads.LaneCount];
        Array.Fill(laneAxis, NoAxis);
        for (var node = 0; node < roads.NodeCount; node++)
        {
            if (!lit[node]) continue;

            foreach (var lane in roads.LanesIn(node)) laneAxis[lane] = AxisOf(reference[node], roads.EndOf(lane).Direction);
        }

        var crossings = plan.Crosswalks;
        var crossingAxis = new int[crossings.Count];
        var crossingJunction = new int[crossings.Count];
        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            var junction = crossings.Junction[crossing];
            crossingJunction[crossing] = junction;

            // A crossing's axis is *the axis of the arm it is painted across*, found by which arm its
            // own axis vector lies along — and never re-derived off the junction's reference bearing.
            // At a skewed junction the two answers can differ, and a crossing that greened against a
            // different axis from the traffic it is painted across is the one failure the whole table
            // exists to make impossible.
            crossingAxis[crossing] = junction >= 0 && junction < lit.Length && lit[junction]
                ? AxisOfArmAlong(roads, laneAxis, junction, crossings.Axis[crossing])
                : NoAxis;
        }

        return new SignalService(config, lit, offsetS, laneAxis, roads.LaneToNode, crossingAxis, crossingJunction);
    }

    /// <summary>
    /// <b>Whether an intersection admits movements that are driven over each other</b> — TLT-3's whole
    /// condition, read off the shape of the junction rather than taken on trust from the map.
    /// </summary>
    /// <remarks>
    /// <b>Fewer than three arms admits none</b> (TER-5c): a dead end has one carriageway and an inline
    /// junction is a place <em>on</em> a road (TER-5b), so the two arms are one street's two halves passing
    /// a lane apart. What such a junction carries is a crossing, and a crossing with no conflicting traffic
    /// to phase against is an uncontrolled one, where the walker has the right of way (TER-5e) and the
    /// traffic gives way to whoever is standing at the kerb. Lit instead, a mid-block zebra holds a street
    /// on a timer that nothing on it is waiting for.
    /// </remarks>
    static bool AdmitsConflictingMovements(RoadGraph roads, int junction) => roads.LanesIn(junction).Length >= 3;

    /// <summary>
    /// Which of the two axes a bearing is on: the reference's, or the other one. <b>Modulo a half turn</b>,
    /// so a road's two ends are one axis.
    /// </summary>
    public static int AxisOf(Vector2 reference, Vector2 bearing)
    {
        if (reference.LengthSquared() <= 0f || bearing.LengthSquared() <= 0f) return 0;

        var along = Vector2.Normalize(reference);
        var to = Vector2.Normalize(bearing);
        var parallel = MathF.Abs(Vector2.Dot(along, to));
        var across = MathF.Abs((along.X * to.Y) - (along.Y * to.X));
        return parallel >= across ? 0 : 1;
    }

    /// <summary>The axis of whichever arm of this junction runs most nearly along a bearing.</summary>
    static int AxisOfArmAlong(RoadGraph roads, int[] laneAxis, int junction, Vector2 bearing)
    {
        if (bearing.LengthSquared() <= 0f) return NoAxis;

        var along = Vector2.Normalize(bearing);
        var best = NoAxis;
        var bestAgreement = -1f;
        foreach (var arm in roads.LanesIn(junction))
        {
            var agreement = MathF.Abs(Vector2.Dot(roads.EndOf(arm).Direction, along));
            if (agreement <= bestAgreement) continue;

            (best, bestAgreement) = (laneAxis[arm], agreement);
        }

        return best;
    }
}
