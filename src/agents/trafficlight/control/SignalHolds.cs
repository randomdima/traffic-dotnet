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
/// <b>It ends at the first body travelling the way it holds</b> (TLT-2a): a hold runs from the bar or the kerb
/// to the first body going down that way in front of it. So a car with any of itself past the bar has started
/// and finishes, and a walker still on the paint when it turns red walks off it — what holds the one behind is
/// the light, and what holds the light off the one already there is that it is there.
/// </para>
/// <para>
/// <b>A body only standing on the way cuts nothing of it</b> — a car across the paint, a walker across the
/// lane. The hold lies over it whole, as any secondary claim lies over what it does not meet, and whoever asks
/// that way is stopped at the body itself first (TER-4c.1).
/// </para>
/// <para>
/// <b>The stretches are found when the town is laid</b>, so a tick asks each one its colour and places at
/// most one piece for it — laid before any plan, since a secondary claim cuts nothing where it is placed
/// (<see cref="LaneOccupancy.Place"/>).
/// </para>
/// </remarks>
internal sealed class SignalHolds
{
    readonly int[] _way;
    readonly float[] _fromM;
    readonly float[] _toM;
    readonly int[] _subject;
    readonly bool[] _forCars;

    SignalHolds(int[] way, float[] fromM, float[] toM, int[] subject, bool[] forCars)
    {
        _way = way;
        _fromM = fromM;
        _toM = toM;
        _subject = subject;
        _forCars = forCars;
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
        var subject = new List<int>();
        var forCars = new List<bool>();

        for (var bar = 0; bar < bars.Count; bar++)
        {
            var lane = bars.Lane[bar];
            if (signals.AxisOfLane(lane) == SignalService.NoAxis) continue;

            way.Add(ways.OfRoadLane(lane));
            fromM.Add(bars.AlongM[bar] - (bars.ThicknessM[bar] * 0.5f));
            toM.Add(roads.LaneLengthM[lane]);
            subject.Add(lane);
            forCars.Add(true);
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
            subject.Add(crossing);
            forCars.Add(false);
        }

        return new SignalHolds([.. way], [.. fromM], [.. toM], [.. subject], [.. forCars]);
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
        for (var stretch = 0; stretch < _way.Length; stretch++)
        {
            var colour = _forCars[stretch]
                ? signals.ForApproach(_subject[stretch], timeS)
                : signals.ForCrossing(_subject[stretch], timeS);
            if (colour == SignalColour.Green) continue;

            HoldTheStretch(occupancy, stretch, _way[stretch], _fromM[stretch], _toM[stretch]);
        }
    }

    /// <summary>
    /// <b>One stretch held</b>, from <paramref name="fromM"/> to the first body travelling the way in front of it
    /// (<see cref="LaneOccupancy.AheadTraveller"/>), or to <paramref name="toM"/>.
    /// </summary>
    /// <remarks>
    /// <b>Nothing planned is read</b>: the only plans already down are other secondary claims, which a secondary
    /// claim never meets (TER-5c.1).
    /// </remarks>
    internal static void HoldTheStretch(LaneOccupancy occupancy, int stretch, int way, float fromM, float toM)
    {
        var hold = occupancy.BeginHold(0f);
        var ask = new PlannedAsk(
            hold, stretch, LaneRoster.Signal, ClaimPriority.Signal, fromM, 0f, 0f, float.NegativeInfinity, 0f);
        var reachM = occupancy.AheadTraveller(way, fromM, toM, out var cutBy) ? MathF.Max(fromM, cutBy.FromM) : toM;
        occupancy.Place(ask, way, reachM);
        occupancy.EndHold(hold, reachM < toM ? reachM - fromM : float.PositiveInfinity, 0f, cutBy);
    }
}
