using TrafficSimulation.Core.Simulation;
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

    /// <summary>The lit junctions any stretch is at, each once.</summary>
    readonly int[] _junctions;

    /// <summary>The lane each approach's stretch holds, or −1 for a crossing's.</summary>
    readonly int[] _laneOf;

    /// <summary>The hold each stretch is held in, placed at its last turn, or <see cref="LaneOccupancy.NoHold"/>.</summary>
    readonly int[] _holds;

    /// <summary>How far each stretch's hold was placed, to the first body as it stood then (<see cref="ReachOf"/>).</summary>
    readonly float[] _reachM;

    /// <summary>What each approach's stretch held as the last laying answered it (<see cref="NoteWhatHolds"/>): whether anything, and where.</summary>
    readonly bool[] _held;
    readonly float[] _heldFromM, _heldToM;

    /// <summary>The laying at which what any light holds on each lane last changed.</summary>
    readonly long[] _changedAt;

    /// <summary>The tick each approach lane's light last took its turn.</summary>
    readonly long[] _turnedAt;

    SignalHolds(
        int[] way, float[] fromM, float[] toM, bool[] forCars, int[] axis, int[] junctionAt, int[] junctions, int[] laneOf,
        int lanes)
    {
        _way = way;
        _fromM = fromM;
        _toM = toM;
        _forCars = forCars;
        _axis = axis;
        _junctionAt = junctionAt;
        _junctions = junctions;
        _laneOf = laneOf;
        _holds = LaneOccupancy.NoHolds(way.Length);
        _reachM = new float[way.Length];
        _held = new bool[way.Length];
        _heldFromM = new float[way.Length];
        _heldToM = new float[way.Length];
        _changedAt = new long[lanes];
        _turnedAt = new long[lanes];
        Array.Fill(_turnedAt, -1L);
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
        var laneOf = new List<int>();
        var slotOf = new Dictionary<int, int>();

        for (var bar = 0; bar < bars.Count; bar++)
        {
            var lane = bars.Lane[bar];
            if (signals.AxisOfLane(lane) == SignalService.NoAxis) continue;

            way.Add(ways.OfRoadLane(lane));
            laneOf.Add(lane);
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
            laneOf.Add(-1);
            fromM.Add(0f);
            toM.Add(ways.LengthM(footway));
            forCars.Add(false);
            axis.Add(signals.AxisOfCrossing(crossing));
            junctionAt.Add(SlotOf(signals.JunctionOfCrossing(crossing)));
        }

        return new SignalHolds(
            [.. way], [.. fromM], [.. toM], [.. forCars], [.. axis], [.. junctionAt], [.. junctions], [.. laneOf],
            roads.LaneCount);

        int SlotOf(int junction)
        {
            if (slotOf.TryGetValue(junction, out var slot)) return slot;

            slotOf[junction] = junctions.Count;
            junctions.Add(junction);
            return junctions.Count - 1;
        }
    }

    /// <summary>
    /// <b>Every stretch whose turn it is, held where its light is against it</b> — an approach on anything but green
    /// (TLT-2a: amber is not green) and a crossing on red — <b>now or at any time before its next turn</b> (root decision
    /// log, 2026-10-14): nothing of it is looked at again before then, so a light about to turn holds its bar already,
    /// and one turned green lets it go at its next turn. <b>A hold that would be placed as it stands is kept</b> — uncut,
    /// and ending at the same first body — so nothing waiting at the bar is woken by a light that did not change.
    /// </summary>
    /// <remarks>
    /// <b>Laid after the bodies and before any plan</b> of a turn, and every plan asked with it is answered against it.
    /// </remarks>
    /// <param name="turns">The stretches' turns, by their index, and <paramref name="tick"/> which of them it is.</param>
    public void Lay(LaneOccupancy occupancy, SignalService signals, float timeS, TickGroups turns, long tick)
    {
        foreach (var stretch in turns.InTurn(tick, _way.Length))
        {
            if (_laneOf[stretch] >= 0) _turnedAt[_laneOf[stretch]] = tick;

            var junction = _junctions[_junctionAt[stretch]];
            var against = ColourOf(stretch, signals.PhaseOf(junction, timeS)) != SignalColour.Green
                          || ColourOf(stretch, signals.PhaseOf(junction, timeS + turns.IntervalS)) != SignalColour.Green;
            var hold = _holds[stretch];
            if (!against)
            {
                occupancy.Release(hold);
                _holds[stretch] = LaneOccupancy.NoHold;
                continue;
            }

            var reachM = ReachOf(occupancy, _way[stretch], _fromM[stretch], _toM[stretch]);
            if (hold != LaneOccupancy.NoHold && reachM == _reachM[stretch] && !occupancy.CutOf(hold).Refused) continue;

            occupancy.Release(hold);
            _holds[stretch] = HoldTheStretch(occupancy, stretch, _way[stretch], _fromM[stretch], reachM);
            _reachM[stretch] = reachM;
        }
    }

    /// <summary>
    /// <b>What every approach's light whose turn it is holds, as its turn answered it</b>, against what its last one did:
    /// each lane whose hold came out anywhere else is stamped with <paramref name="laying"/> (<see cref="ChangedAt"/>).
    /// Read as a driver reads it (<see cref="LaneOccupancy.FirstSecondary"/>), so a hold the answer cut — ground a car
    /// could no longer stop short of, on an amber — is a change too.
    /// </summary>
    /// <param name="turns">The stretches' turns, as <see cref="Lay"/> took them.</param>
    public void NoteWhatHolds(LaneOccupancy occupancy, long laying, TickGroups turns, long tick)
    {
        foreach (var stretch in turns.InTurn(tick, _way.Length))
        {
            var lane = _laneOf[stretch];
            if (lane < 0) continue;

            var held = occupancy.FirstSecondary(
                _way[stretch], _fromM[stretch], _toM[stretch], ClaimPriority.Signal, out var heldFromM, out var heldToM);
            if (held == _held[stretch] && heldFromM == _heldFromM[stretch] && heldToM == _heldToM[stretch]) continue;

            (_held[stretch], _heldFromM[stretch], _heldToM[stretch]) = (held, heldFromM, heldToM);
            _changedAt[lane] = laying;
        }
    }

    /// <summary>The laying at which what any light holds on this lane last came out otherwise (<see cref="NoteWhatHolds"/>).</summary>
    public long ChangedAt(int lane) => _changedAt[lane];

    /// <summary>The tick the light over an approach lane last took its turn (<see cref="Lay"/>), for an instrument.</summary>
    public long TurnedAt(int lane) => _turnedAt[lane];

    /// <summary>
    /// What a stretch's light shows it at a phase of its junction's cycle: its approach's colour, or its crossing's
    /// (<see cref="SignalService.ForApproach"/>, <see cref="SignalService.ForCrossing"/>). <b>Held on anything but
    /// green</b> (<see cref="Lay"/>).
    /// </summary>
    SignalColour ColourOf(int stretch, JunctionPhase phase)
    {
        return _forCars[stretch]
            ? SignalCycle.Shown(_axis[stretch], phase)
            : SignalCycle.ShownToTheCrossing(_axis[stretch], phase);
    }

    /// <summary>
    /// <b>How far a stretch is held</b>: from <paramref name="fromM"/> to the first body on the way in front of it
    /// (<see cref="LaneOccupancy.FirstBody"/>), or to <paramref name="toM"/>. A body past the bar is one the light let
    /// go, and is held by nothing behind it.
    /// </summary>
    internal static float ReachOf(LaneOccupancy occupancy, int way, float fromM, float toM) =>
        occupancy.FirstBody(way, fromM, toM, LaneOccupancy.Nobody, LaneRoster.Signal, out var atM) ? atM : toM;

    /// <summary><b>One stretch held</b> over [<paramref name="fromM"/>, <paramref name="reachM"/>) of its way.</summary>
    /// <returns>The hold it is held in.</returns>
    internal static int HoldTheStretch(LaneOccupancy occupancy, int stretch, int way, float fromM, float reachM)
    {
        var hold = occupancy.BeginHold(stretch, LaneRoster.Signal);
        occupancy.Place(hold, way, fromM, reachM, ClaimPriority.Signal);
        return hold;
    }
}
