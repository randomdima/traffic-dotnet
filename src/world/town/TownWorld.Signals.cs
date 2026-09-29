using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The lights in the reservations</b> (TLT-1): the ground each lays against the traffic, and what a driver
/// reads back of the junction in front of it.
/// </summary>
/// <remarks>
/// <b>Nothing here stops a car.</b> A light's hold is laid with the planned layer and every plan is answered
/// against it like any other (TER-4c.1), so a red is where a grant ends and a car drives to that the way it
/// drives to anything. What is left is reading: the junction's facts for the indicator and the instruments, how
/// far off a light holds the road — the one wait that spends no clock (TLT-2a) — and the bar a red was crossed
/// at.
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>Every light's hold, laid</b> — after the bodies, which end it, and before any plan, which it is laid
    /// to refuse.
    /// </summary>
    void LightTheWays() => _signalHolds.Lay(_occupancy, _signals, _elapsedS);

    /// <summary>
    /// <b>The junction ahead of this car</b>, for the indicator, the read-out and the instruments: how far off
    /// the box is, whether the movement into it turns, whether the car is in it, could still stop short of it
    /// and was granted into it — and how far off a light holds its road.
    /// </summary>
    /// <param name="toTheBoxM">
    /// How far ahead the box the car's own line enters stands, or infinity where its line enters none. It is
    /// what a car's indicator is read off (CAR-14.1).
    /// </param>
    /// <param name="claimed">Whether the road this car was granted reaches into the box, or it is already inside.</param>
    void ReadTheBoxAhead(int car, float progressM, out float toTheBoxM, out bool claimed)
    {
        var ends = Cars.LaneEndsOf(car);
        var chain = Cars.ChainOf(car);
        var noseM = progressM + Cars.BuildOf(car).NoseAheadOfAxleM;
        toTheBoxM = float.PositiveInfinity;
        claimed = false;
        Cars.InsideTheBox[car] = false;
        Cars.CommittedToTheBox[car] = false;
        Cars.LightAheadM[car] = LightAheadM(car, noseM);

        // Which way through it is comes from the *geometry the car's own line enters*, and never from
        // the lane under the car: mid-turn the nearest lane is already the one leading out.
        var ahead = LaneAheadSlot(car, progressM);
        var movement = ahead + 1 < Cars.Line[car].LaneCount
            ? _roads.ConnectorBetween(chain[ahead], chain[ahead + 1])
            : RoadGraph.NoConnector;

        // CAR-14.1 reads its indicator off this same classification.
        Cars.TurningAtTheBox[car] = movement != RoadGraph.NoConnector
            && _roads.FirmOnConnector(movement) != ClaimPriority.FirmStraight;

        // A movement with no ground under it is not a movement: two lanes meeting at a point have no box.
        if (movement == RoadGraph.NoConnector || _roads.ConnectorLengthM(movement) <= 0f) return;

        NoteBarCrossing(car, ahead, chain[ahead], progressM);

        if (progressM >= ends[ahead])
        {
            Cars.InsideTheBox[car] = true;
            Cars.CommittedToTheBox[car] = true;
            toTheBoxM = 0f;
            claimed = true;
            return;
        }

        toTheBoxM = ends[ahead] - noseM;
        Cars.CommittedToTheBox[car] = Cars.CommittedToM[car] > ends[ahead];
        claimed = noseM + Cars.AuthorityM[car] > ends[ahead];
    }

    /// <summary>
    /// Which lane of the chain the car is on — the one its own line is running down, which past a
    /// junction's boundary is already the lane it turned into.
    /// </summary>
    int LaneAheadSlot(int car, float progressM)
    {
        var starts = Cars.LaneStartsOf(car);
        var ahead = 0;
        while (ahead < Cars.Line[car].LaneCount - 1 && progressM >= starts[ahead + 1]) ahead++;

        return ahead;
    }

    /// <summary>
    /// <b>How far in front of this car's nose a light holds its road</b>, out to a queue's length of it — or
    /// infinity. Read off the planned layer like everything else in front of it (TER-4c.5): a light's hold is
    /// ground on the ways of the car's line, laid under the lights' own roster.
    /// </summary>
    float LightAheadM(int car, float noseM)
    {
        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        var count = WaysAlong(car, noseM, noseM + QueueReachM(car), ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            if (!_occupancy.AheadPlanned(way.Way, way.FromM, way.ToM, LaneRoster.Signal, out var held)) continue;

            return MathF.Max(0f, OnTheLineM(way, held.FromM) - noseM);
        }

        return float.PositiveInfinity;
    }

    /// <summary>
    /// Where a lane's own painted bar falls on this car's line, or infinity where the lane has none.
    /// </summary>
    float BarOnLineM(int car, int ahead, int lane)
    {
        var barAlongM = _furniture.StopBarAlongM(lane);
        return float.IsPositiveInfinity(barAlongM) ? float.PositiveInfinity : OnTheLineM(car, ahead, barAlongM);
    }

    /// <summary>
    /// The soak's own invariant, counted where it happens: a car whose nose was behind a painted
    /// bar last tick and is past it now, on an approach showing red. A figure taken anywhere else would
    /// be a sample rather than the event.
    /// </summary>
    void NoteBarCrossing(int car, int ahead, int lane, float progressM)
    {
        var barOnLineM = BarOnLineM(car, ahead, lane);
        if (float.IsPositiveInfinity(barOnLineM))
        {
            _behindTheBar[car] = false;
            return;
        }

        // Measured where a light's hold begins — the paint's near edge — and read at the rear axle, so a nose
        // brought to rest over the edge is a car that stopped and not one that crossed. A car on a call is not
        // held by the light at all (AMB-4), so it cannot be in breach of it.
        var behind = progressM < barOnLineM - (_furniture.StopBarThicknessM(lane) * 0.5f);
        if (_behindTheBar[car] && !behind && !Cars.BlueLight[car] &&
            _signals.ForApproach(lane, _elapsedS) == SignalColour.Red)
        {
            RedBarCrossings++;
            LastRedBarCrossing = new RedBarCrossing(car, Cars.PositionM[car], Cars.VelocityMps[car].Length());
        }

        _behindTheBar[car] = behind;
    }
}
