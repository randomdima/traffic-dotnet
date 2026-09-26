using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>What makes a car stop short of a junction: the lamps governing its approach and the painted bar it is measured against.</summary>
/// <remarks>
/// <b>A junction's ground is not decided here.</b> Whether a car may have the box is the planned layer's
/// answer (TER-4c.1): its plan runs through the box and is settled against every other plan that shares
/// ground with it, and what is left is the road it drives to. A light is the one thing that stops a car short
/// of a box and is not a reservation — the town's signals are infrastructure, driven by the clock, and a red
/// bounds the plan rather than holding ground of its own (TER-4c.5).
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// <b>How far ahead the light stops this car</b>, or infinity where it does not — and the junction's
    /// facts for the indicator, the read-out and the instruments: how far off the box is, whether the
    /// movement into it turns, whether the car is in it, could still stop short of it, and was granted into
    /// it.
    /// </summary>
    /// <param name="toTheBoxM">
    /// How far ahead the box the car's own line enters stands, or infinity where its line enters none. It is
    /// what a car's indicator is read off (CAR-14.1).
    /// </param>
    /// <param name="claimed">Whether the road this car was granted reaches into the box, or it is already inside.</param>
    float JunctionStopM(int car, float progressM, out float toTheBoxM, out bool claimed)
    {
        var ends = Cars.LaneEndsOf(car);
        var chain = Cars.ChainOf(car);
        toTheBoxM = float.PositiveInfinity;
        claimed = false;
        Cars.InsideTheBox[car] = false;
        Cars.CommittedToTheBox[car] = false;
        Cars.LightAheadM[car] = float.PositiveInfinity;

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
        if (movement == RoadGraph.NoConnector || _roads.ConnectorLengthM(movement) <= 0f) return float.PositiveInfinity;

        NoteBarCrossing(car, ahead, chain[ahead], progressM);

        if (progressM >= ends[ahead])
        {
            Cars.InsideTheBox[car] = true;
            Cars.CommittedToTheBox[car] = true;
            toTheBoxM = 0f;
            claimed = true;
            return float.PositiveInfinity;
        }

        var lightStopM = SignalStopM(car, ahead, chain[ahead], progressM);
        Cars.LightAheadM[car] = lightStopM;

        var noseM = progressM + Cars.BuildOf(car).NoseAheadOfAxleM;
        toTheBoxM = ends[ahead] - noseM;
        Cars.CommittedToTheBox[car] = Cars.CommittedToM[car] > ends[ahead];
        claimed = noseM + Cars.AuthorityM[car] > ends[ahead];
        return lightStopM;
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
    /// How far ahead the light says to stop, or infinity where it does not: the approach's own painted
    /// bar, while that approach is showing anything but green and the car's nose has not yet reached it.
    /// </summary>
    float SignalStopM(int car, int ahead, int lane, float progressM)
    {
        // AMB-4: a red is a rule about whose turn it is, and a car on a call (EVA-4, SRV-6) is not taking a turn.
        // Nothing else is lifted with it — the box is still refused by a body in it, and the profile still
        // stops for whatever is standing on the far side.
        if (Cars.BlueLight[car]) return float.PositiveInfinity;

        // CAR-13: and the same lifted for a worse reason. What the two have in common is only this line —
        // a rescue is exempt from the rule and a reckless driver is in breach of it, which is the whole of
        // why <see cref="NoteBarCrossing"/> counts one of them and not the other.
        if (RecklessAtTheWheel(car)) return float.PositiveInfinity;

        if (_signals.AxisOfLane(lane) == SignalService.NoAxis) return float.PositiveInfinity;
        if (_signals.ForApproach(lane, _elapsedS) == SignalColour.Green) return float.PositiveInfinity;

        var barOnLineM = BarOnLineM(car, ahead, lane);
        if (float.IsPositiveInfinity(barOnLineM)) return float.PositiveInfinity;

        // The nose is what stops at the paint's near edge, and the rear axle is what says the car has
        // started. The car's own nose-to-axle length is the whole of the difference, and it is what
        // stops a car that has crept a centimetre over the paint from taking that as permission: the
        // exemption is for a car with its *body* over the bar, not its bumper.
        var nearEdgeM = barOnLineM - (_furniture.StopBarThicknessM(lane) * 0.5f);
        if (progressM >= nearEdgeM) return float.PositiveInfinity;

        var noseM = progressM + Cars.BuildOf(car).NoseAheadOfAxleM;
        return noseM < nearEdgeM ? nearEdgeM - noseM : 0f;
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

        // Measured at exactly the point the stop rule stops governing the car — its rear axle reaching
        // the paint's near edge. Judging it half a metre later instead would count every car the light
        // turned red behind, which is a car that had already gone.
        // A car on a call is exempt from the rule (AMB-4), so it cannot be in breach of it. Counted
        // anyway, the soak's own invariant would report a town where nobody had run a red as one where the
        // rescue had run several.
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
