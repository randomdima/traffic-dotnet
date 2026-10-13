using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.TrafficLight.Control;

/// <summary>
/// <b>What a light does to the town, which is hold ground</b> (TLT-1): a secondary claim at its own rung
/// (<see cref="ClaimPriority.Signal"/>) over an approach from its bar on while that approach is not showing
/// green, and over the paint of a crossing while the crossing is showing red.
/// </summary>
/// <remarks>
/// <para>
/// <b>A secondary claim, so it meets the main claims laid on the way it holds and nothing else</b>
/// (TER-5c.1). A car planning over its bar is answered at the bar and waits there; a walker planning the paint
/// is answered at the kerb and waits there (PER-27). The walkers' own secondary claims on the lanes under a
/// zebra, and the drivers' on the paint they drive over, lie beside it and never meet it — so a light holding
/// an approach costs nobody the zebra across it, and one holding the zebra costs no car the lane under it.
/// </para>
/// <para>
/// <b>Where it stands in the ladder is the whole of who it holds</b> (TER-5e, TER-5g): above a walker and
/// every movement, below a call. Ground a holder can no longer stop short of beats it as it beats
/// every rung, which is what the amber is for.
/// </para>
/// <para>
/// <b>It ends at the first body on the way it holds</b> (TLT-2a): a hold runs from the bar or the kerb to the first
/// body in front of it. So a car with any of itself past the bar has started and finishes, and a walker still on the
/// paint when it turns red walks off it — what holds the one behind is the light, and what holds the light off the one
/// already there is that it is there. <b>A body only standing on the way ends it all the same</b>, since the claims say
/// where a body is and never what it is doing (TER-4c.5); whoever asks that way is stopped at the body itself first.
/// </para>
/// <para>
/// <b>The stretches are found when the town is laid</b>, so a tick asks each one its colour and places at most one
/// piece for it (<see cref="LaneOccupancy.Place"/>).
/// </para>
/// </remarks>
internal sealed class SignalHolds
{
    readonly int[] _way;
    readonly float[] _fromM;
    readonly float[] _toM;
    readonly bool[] _forCars;

    /// <summary>The axis each stretch is shown off: its approach's, or the road's its crossing is across.</summary>
    readonly int[] _axis;

    /// <summary>Which of <see cref="_junctions"/> each stretch's light is at.</summary>
    readonly int[] _junctionAt;

    /// <summary>The lit junctions any stretch is at, each once, and where each one's cycle stood at the last laying.</summary>
    readonly int[] _junctions;
    readonly JunctionPhase[] _phases;

    SignalHolds(int[] way, float[] fromM, float[] toM, bool[] forCars, int[] axis, int[] junctionAt, int[] junctions)
    {
        _way = way;
        _fromM = fromM;
        _toM = toM;
        _forCars = forCars;
        _axis = axis;
        _junctionAt = junctionAt;
        _junctions = junctions;
        _phases = new JunctionPhase[junctions.Length];
    }

    /// <summary>
    /// How many stretches the lights can hold at once — every one of them, which is what the reservations are
    /// sized with room for: one hold and one piece a stretch.
    /// </summary>
    public int Count => _way.Length;

    /// <param name="bars">The town's own bars (<see cref="StopBars"/>): an approach is held from the near edge of its own.</param>
    /// <param name="paint">Which ways of the pavement are the paint of which crossing.</param>
    public static SignalHolds Of(SignalService signals, StopBars bars, CrossingEdges paint, RoadGraph roads, TownWays ways)
    {
        var way = new List<int>();
        var fromM = new List<float>();
        var toM = new List<float>();
        var forCars = new List<bool>();
        var axis = new List<int>();
        var junctionAt = new List<int>();
        var junctions = new List<int>();
        var slotOf = new Dictionary<int, int>();

        for (var bar = 0; bar < bars.Count; bar++)
        {
            var lane = bars.Lane[bar];
            if (signals.AxisOfLane(lane) == SignalService.NoAxis) continue;

            way.Add(ways.OfRoadLane(lane));
            fromM.Add(bars.AlongM[bar] - (bars.ThicknessM[bar] * 0.5f));
            toM.Add(roads.LaneLengthM[lane]);
            forCars.Add(true);
            axis.Add(signals.AxisOfLane(lane));
            junctionAt.Add(SlotOf(signals.JunctionOfLane(lane)));
        }

        // A zebra is walked from either kerb on a way of its own each way (WLK-15), and both are held.
        var crossingOf = paint.CrossingOfEdge;
        for (var edge = 0; edge < crossingOf.Length; edge++)
        {
            var crossing = crossingOf[edge];
            if (crossing < 0 || !signals.CrossingIsLit(crossing)) continue;

            var footway = ways.OfFootway(edge);
            way.Add(footway);
            fromM.Add(0f);
            toM.Add(ways.LengthM(footway));
            forCars.Add(false);
            axis.Add(signals.AxisOfCrossing(crossing));
            junctionAt.Add(SlotOf(signals.JunctionOfCrossing(crossing)));
        }

        return new SignalHolds([.. way], [.. fromM], [.. toM], [.. forCars], [.. axis], [.. junctionAt], [.. junctions]);

        int SlotOf(int junction)
        {
            if (slotOf.TryGetValue(junction, out var slot)) return slot;

            slotOf[junction] = junctions.Count;
            junctions.Add(junction);
            return junctions.Count - 1;
        }
    }

    /// <summary>
    /// <b>Every stretch whose light is against it, held</b> — an approach on anything but green (TLT-2a: amber
    /// is not green) and a crossing on red.
    /// </summary>
    /// <remarks>
    /// <b>Laid after the bodies and before any plan</b>, and every plan laid after it is answered against it.
    /// </remarks>
    public void Lay(LaneOccupancy occupancy, SignalService signals, float timeS)
    {
        for (var at = 0; at < _junctions.Length; at++) _phases[at] = signals.PhaseOf(_junctions[at], timeS);

        for (var stretch = 0; stretch < _way.Length; stretch++)
        {
            if (ColourOf(stretch) == SignalColour.Green) continue;

            HoldTheStretch(occupancy, stretch, _way[stretch], _fromM[stretch], _toM[stretch]);
        }
    }

    /// <summary>
    /// What a stretch's light is showing it at the last laying: its approach's colour, or its crossing's
    /// (<see cref="SignalService.ForApproach"/>, <see cref="SignalService.ForCrossing"/>). <b>Held on anything but
    /// green</b> (<see cref="Lay"/>).
    /// </summary>
    SignalColour ColourOf(int stretch)
    {
        var phase = _phases[_junctionAt[stretch]];
        return _forCars[stretch]
            ? SignalCycle.Shown(_axis[stretch], phase)
            : SignalCycle.ShownToTheCrossing(_axis[stretch], phase);
    }

    /// <summary>
    /// <b>One stretch held</b>, from <paramref name="fromM"/> to the first body on the way in front of it
    /// (<see cref="LaneOccupancy.FirstBody"/>), or to <paramref name="toM"/>: a body past the bar is one the light let
    /// go, and is held by nothing behind it.
    /// </summary>
    internal static void HoldTheStretch(LaneOccupancy occupancy, int stretch, int way, float fromM, float toM)
    {
        var reachM = occupancy.FirstBody(way, fromM, toM, LaneOccupancy.Nobody, LaneRoster.Signal, out var atM) ? atM : toM;
        occupancy.Place(occupancy.BeginHold(stretch, LaneRoster.Signal), way, fromM, reachM, ClaimPriority.Signal);
    }
}
