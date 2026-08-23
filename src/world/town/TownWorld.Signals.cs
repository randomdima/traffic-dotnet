using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Town;

/// <summary>What makes a car stop short of a junction: the box itself, the lamps governing its approach, and the painted bar it is measured against.</summary>
internal sealed partial class TownWorld
{
    /// <summary>
    /// How far ahead the car must be stopped by: the ground through the junction it has not been given, or
    /// the bar its approach is showing a red at. A car <em>inside</em> a junction holds its crossing
    /// outright, and drops the one behind it as soon as it is queueing for the next: at most one is held,
    /// or ground nobody is on stops the traffic crossing it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What is taken is ground and not a permission</b> (TER-5c) — and it is ground on the car's <em>own</em>
    /// way through the box, which goes into the road's book like any other stretch of road. Where that way
    /// is driven over another is <see cref="JunctionCrossings"/>'s to say, and it is looked up rather than
    /// written to. What is decided here is only <em>when</em> a car commits and when it lets go.
    /// </para>
    /// <para>
    /// A green is the whole permission and a red the whole refusal, asked separately of the same
    /// approach: a red stops the car at the paint whether or not the ground beyond is free, and a green
    /// leaves the movement to be claimed against the cars the phase does not govern — the one in front out
    /// of this lane, and whatever is merging into the lane this car is turning into. The test is
    /// positional and never predictive: what decides whether a car has started is where its nose is, not
    /// whether it could stop. Amber is not green, so a car short of the paint stops for one.
    /// </para>
    /// <para>
    /// <b>The crossing is one state, held by the car and never recomputed from whether it would still be
    /// granted.</b> It is taken only by a driver nothing but the box is holding up and given back the
    /// moment something else is, so that a car waiting at a bar holds no way through the junction beyond
    /// it — which would be the arm with the green refused by the arm with the red, the duplicate SIM-7 was
    /// written about. Recomputed instead, it moves under a car that is merely slowing down.
    /// </para>
    /// </remarks>
    /// <param name="toTheBoxM">
    /// How far ahead the box the car's own line enters stands, or infinity where its line enters none.
    /// It is what the catalogue names `P-8` off, and it is a fact about the geometry rather than about
    /// the lane under the car.
    /// </param>
    /// <param name="claimed">Whether the way through is this car's to take: the claim held, or the car already inside the box.</param>
    float JunctionStopM(int car, float progressM, float alongMps, float headwayM, out float toTheBoxM, out bool claimed)
    {
        var ends = Cars.LaneEndsOf(car);
        var chain = Cars.ChainOf(car);
        toTheBoxM = float.PositiveInfinity;
        claimed = false;
        Cars.InsideTheBox[car] = false;
        Cars.LightAheadM[car] = float.PositiveInfinity;

        // Which way through it is comes from the *geometry the car's own line enters*, and never from
        // the lane under the car: mid-turn the nearest lane is already the one leading out, and keying
        // on it waves cars into crossings unclaimed.
        var ahead = LaneAheadSlot(car, progressM);
        var movement = ahead + 1 < Cars.Line[car].LaneCount
            ? _roads.TurnSlot(chain[ahead], chain[ahead + 1])
            : RoadGraph.NoTurn;

        if (Cars.Crossing[car] != movement) DropTheMovement(car);

        if (movement == RoadGraph.NoTurn) return float.PositiveInfinity;

        NoteBarCrossing(car, ahead, chain[ahead], progressM);

        if (progressM >= ends[ahead])
        {
            // Ground taken from in there is a statement of fact and is asked of nobody: a book that said
            // otherwise would be describing a town other than the one that exists. Being in there is also
            // what puts this car on the short fuse — it is standing on everything its line crosses.
            if (Cars.Crossing[car] != movement) TakeTheCrossing(car, movement);
            Cars.InsideTheBox[car] = true;
            toTheBoxM = 0f;
            claimed = true;
            return float.PositiveInfinity;
        }

        var lightStopM = SignalStopM(car, ahead, chain[ahead], progressM);
        Cars.LightAheadM[car] = lightStopM;

        var toBoxM = ends[ahead] - progressM - _config.CarNoseAheadOfAxleM;
        toTheBoxM = toBoxM;

        // The rate the profile actually brakes at on the ground under this car, which is what every other
        // stretch of road it holds is sized by. Sized to the pedal's own cap instead, both readings below
        // err the one way that costs: a car past stopping range judged able to stop still, and a claim
        // taken a tick or two after the car committed.
        var brakingMps2 = CarFollower.BrakingMps2(_config, Cars.GroundCoefficient[car]);

        // <b>What stops this car short of the boundary that is not the boundary</b>: the bar its approach
        // is showing anything but green at, and traffic it would still be behind with the pair of them at
        // rest.
        var stoppedShort = float.IsFinite(lightStopM) || headwayM + _config.CarBodyMarginM < toBoxM;

        // <b>Past the point it could stop at, the car is going in whatever anything says.</b> Ground given
        // back there is handed straight back as a fact on the next tick, and in between the two ticks the
        // sections read free to whoever crosses them.
        var committed = toBoxM <= StoppingM(alongMps, brakingMps2);

        if (Cars.Crossing[car] == movement)
        {
            if (!stoppedShort || committed)
            {
                claimed = true;
                return lightStopM;
            }

            DropTheMovement(car);
        }

        if (stoppedShort) return lightStopM;

        // Reserved once within stopping distance, capped by the reserve distance: earlier than that is
        // a car holding ground it is nowhere near, and later is two cars committing in the same tick.
        var reserveAtM = MathF.Min(
            StoppingM(alongMps, brakingMps2) + _config.Car.LengthM, _config.CarJunctionReserveM);
        if (toBoxM > reserveAtM) return lightStopM;

        if (!TheCrossingIsFree(car, movement))
        {
            return MathF.Min(lightStopM, toBoxM - _config.Car.LengthM * 0.5f);
        }

        TakeTheCrossing(car, movement);
        claimed = true;
        return lightStopM;
    }

    /// <summary>
    /// Whether every section of the junction this movement is driven over is ground nobody else has —
    /// <b>asked of all of them before any is taken</b>. Taken one at a time, two cars whose lines cross
    /// twice each end up holding the half the other needs and neither can go.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every section is read where it lies and none of it is written anywhere</b> (TER-5c). The table
    /// says which metres of which other join this movement is driven over; what is asked of those metres is
    /// what is standing on them, in that join's own book — the same question a grant asks
    /// (<c>WhereTheGroundIsCrossed</c>), at the moment a car decides whether to commit rather than as a
    /// distance.
    /// </para>
    /// <para>
    /// <b>Spoken for and not merely claimed</b>: a car already crossing lays the join it is driving as its
    /// own reservation rather than as a claim, and that is exactly the car this one must not be driven into.
    /// It is what makes the pair of tests symmetric — each of two crossing movements is refused by the
    /// other's road, whichever of them asked first.
    /// </para>
    /// <para>
    /// <b>A car making this same movement is not an answer</b>, and it is the one occupant skipped. It has
    /// taken the same sections this car wants, off the same lines, so read literally every queue at a
    /// junction refuses its own second car — and what holds one of them off the next is the road each was
    /// granted (S-2a), which is a headway and not a crossing.
    /// </para>
    /// </remarks>
    bool TheCrossingIsFree(int car, int movement)
    {
        // Read on the movement's own metres, so that what it is refused by and what it would take are one
        // set: ground it is already past is ground it is not asking for.
        var passedM = PastOnTheCrossing(car, movement);
        foreach (ref readonly var section in _roads.Crossings.Of(movement))
        {
            if (section.MineToM <= passedM) continue;

            if (!NobodyElseIsOn(car, movement, section.OnTurn, section.FromM, section.ToM)) return false;
        }

        // And this movement's own share of the same crossings, which is the ground a car coming the other
        // way is refused by. The same runs the car would lay, on the same test.
        foreach (ref readonly var run in _roads.Crossings.OwnRuns(movement))
        {
            if (run.ToM <= passedM) continue;

            if (!NobodyElseIsOn(car, movement, movement, MathF.Max(run.FromM, passedM), run.ToM)) return false;
        }

        return true;
    }

    /// <summary>One section of one join, and whether anything not crossing on this same movement is on it.</summary>
    /// <remarks>
    /// <b>Everything lying over the section and not only what begins on it</b>
    /// (<see cref="LaneOccupancy.NextSpokenForOver"/>). A section is a named piece of ground rather than the
    /// road under the asker, so a stretch that began at a body further back and runs through it is the
    /// answer: read the other way round, a car whose reservation entered a join before the metres two lines
    /// meet on was invisible to the movement crossing there, and both went.
    /// </remarks>
    bool NobodyElseIsOn(int car, int movement, int turn, float fromM, float toM)
    {
        var at = LaneOccupancy.FromTheStart;
        while (_occupancy.NextSpokenForOver(_occupancy.WayOfTurn(turn), fromM, toM, car, ref at, out var taken))
        {
            if (Cars.Crossing[taken.Occupant] != movement) return false;
        }

        return true;
    }

    /// <summary>
    /// The movement taken up: the car's own field, and the runs of its own join written into the book at
    /// the moment they are taken, so that a car later in this same walk is refused ground this one has just
    /// been given. <see cref="DropTheMovement"/> is the pair of it, and neither half is ever done alone.
    /// </summary>
    void TakeTheCrossing(int car, int movement)
    {
        Cars.Crossing[car] = movement;
        LayTheCrossing(car, movement);
    }

    /// <summary>
    /// The ground this car was crossing on, given back — <b>from the car and from the book together</b>.
    /// The book is laid from the cars once a tick, so a field written away on its own leaves stretches
    /// standing against everything that crosses them for the rest of the walk that wrote it.
    /// </summary>
    void DropTheMovement(int car)
    {
        var held = Cars.Crossing[car];
        if (held < 0) return;

        Cars.Crossing[car] = CarFleet.NoMovement;
        _occupancy.Withdraw(_occupancy.WayOfTurn(held), car, LaneUse.Claimed);
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

        var noseM = progressM + _config.CarNoseAheadOfAxleM;
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
        var behind = progressM < barOnLineM - (_furniture.StopBarThicknessM(lane) * 0.5f);
        if (_behindTheBar[car] && !behind && _signals.ForApproach(lane, _elapsedS) == SignalColour.Red)
        {
            RedBarCrossings++;
            LastRedBarCrossing = new RedBarCrossing(car, Cars.PositionM[car], Cars.VelocityMps[car].Length());
        }

        _behindTheBar[car] = behind;
    }
}
