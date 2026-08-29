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
    /// is driven over another is <see cref="WayCrossings"/>'s to say, and it is looked up rather than
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
    /// <para>
    /// <b>The one thing that takes it back is a greater right of way</b> (TER-5e,
    /// <see cref="TheMovementIsTakenBack"/>), and that is not the crossing recomputed: a movement that gives
    /// way never had a claim on the ground the stronger one is asking for, so what it holds was never its to
    /// keep. It is asked only of a car that could still stop short of the box, because past that point the
    /// car is going in whatever anything says.
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
        Cars.CommittedToTheBox[car] = false;
        Cars.LightAheadM[car] = float.PositiveInfinity;

        // Which way through it is comes from the *geometry the car's own line enters*, and never from
        // the lane under the car: mid-turn the nearest lane is already the one leading out, and keying
        // on it waves cars into crossings unclaimed.
        var ahead = LaneAheadSlot(car, progressM);
        var movement = ahead + 1 < Cars.Line[car].LaneCount
            ? _roads.TurnSlot(chain[ahead], chain[ahead + 1])
            : RoadGraph.NoTurn;

        // <b>A movement with no ground under it is not a movement</b>: the two lanes at a place cut into a
        // road (GEN-4h) meet at a point, so the join between them is a join of no length and there is no box
        // to be given, to be refused, or to stop short of. Read as one anyway, every car park in the town
        // would put a junction across the street in front of it and every car would negotiate it.
        var movementWay = movement == RoadGraph.NoTurn || _roads.JoinLengthM(movement) <= 0f
            ? CarFleet.NoWay
            : _occupancy.WayOfTurn(movement);

        if (Cars.MovementWay[car] != movementWay) DropTheMovement(car);

        if (movementWay == CarFleet.NoWay) return float.PositiveInfinity;

        NoteBarCrossing(car, ahead, chain[ahead], progressM);

        if (progressM >= ends[ahead])
        {
            // Ground taken from in there is a statement of fact and is asked of nobody: a book that said
            // otherwise would be describing a town other than the one that exists. Being in there is also
            // what puts this car on the short fuse — it is standing on everything its line crosses.
            Cars.InsideTheBox[car] = true;
            Cars.CommittedToTheBox[car] = true;
            if (Cars.MovementWay[car] != movementWay) TakeTheMovement(car, movementWay);
            toTheBoxM = 0f;
            claimed = true;
            return float.PositiveInfinity;
        }

        var lightStopM = SignalStopM(car, ahead, chain[ahead], progressM);
        Cars.LightAheadM[car] = lightStopM;

        ref readonly var build = ref Cars.BuildOf(car);
        var toBoxM = ends[ahead] - progressM - build.NoseAheadOfAxleM;
        toTheBoxM = toBoxM;

        // The rate the profile actually brakes at on the ground under this car, which is what every other
        // stretch of road it holds is sized by. Sized to the pedal's own cap instead, both readings below
        // err the one way that costs: a car past stopping range judged able to stop still, and a claim
        // taken a tick or two after the car committed.
        var brakingMps2 = CarFollower.BrakingMps2(_config, build, Cars.GroundCoefficient[car]);

        // <b>What stops this car short of the boundary that is not the boundary</b>: the bar its approach
        // is showing anything but green at, and traffic it would still be behind with the pair of them at
        // rest.
        var stoppedShort = float.IsFinite(lightStopM) || headwayM + build.BodyMarginM < toBoxM;

        // <b>Past the point it could stop at, the car is going in whatever anything says.</b> Ground given
        // back there is handed straight back as a fact on the next tick, and in between the two ticks the
        // sections read free to whoever crosses them. It is written to the car because the ground of the
        // movement is laid with it (TER-5e): what a committed body holds, nothing's right of way takes.
        //
        // <b>Read a decision ahead, because everybody else reads it a decision late</b>: the book that
        // carries this to the rest of the town is laid at the top of a tick from what the last decision
        // wrote, so a car that will be past stopping by the time the ranks are next compared has to count
        // as committed now. It costs the weaker movement a fraction of a second of ground it would have
        // given up anyway, and without it a stronger movement is waved across a car that can no longer
        // stop — which is what a fleet of cars that brake at their own rates makes reachable (CAR-11).
        var committed = toBoxM - (MathF.Max(0f, alongMps) * _config.CarReactionS)
                        <= StoppingM(alongMps, brakingMps2);
        Cars.CommittedToTheBox[car] = committed;

        if (Cars.MovementWay[car] == movementWay)
        {
            // <b>Committed first, and both of the ways below are read only where it is not.</b> Past the
            // point it could stop, a car keeps what it holds whatever asks for it: a right of way orders who
            // waits and never who is driven into (TER-5e).
            var takenBack = !committed && !stoppedShort && TheMovementIsTakenBack(car, movementWay);
            if (committed || (!stoppedShort && !takenBack))
            {
                claimed = true;
                return lightStopM;
            }

            if (takenBack) CrossingsGivenBack++;
            DropTheMovement(car);
        }

        if (stoppedShort) return lightStopM;

        // Reserved once within stopping distance, capped by the reserve distance: earlier than that is
        // a car holding ground it is nowhere near, and later is two cars committing in the same tick.
        var reserveAtM = MathF.Min(
            StoppingM(alongMps, brakingMps2) + build.LengthM, _config.CarJunctionReserveM);
        if (toBoxM > reserveAtM) return lightStopM;

        // Refused at a place and not outright: the movement's own metres begin at the boundary, so ground
        // held on the far side of the box is stopped short of on the far side of the box — but only as far
        // as the car can wait without standing on a crossing (<see cref="WaitsClearOfTheCrossings"/>).
        var heldFromM = FirstHeldOnTheMovementM(car, movementWay);
        if (float.IsFinite(heldFromM))
        {
            var restM = heldFromM - build.BodyMarginM;
            return MathF.Min(
                lightStopM,
                WaitsClearOfTheCrossings(car, movementWay, restM)
                    ? toBoxM + restM
                    : toBoxM - build.HalfLengthM);
        }

        TakeTheMovement(car, movementWay);
        claimed = true;
        return lightStopM;
    }

    /// <summary>
    /// <b>Whether a car brought to rest with its nose here would be standing clear of every crossing on its
    /// own way</b> — the one thing that decides whether it may wait inside the box at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing about it is about the car's own movement.</b> It is the traffic on the ways this one is
    /// driven over: a body at rest across a crossing is that crossing shut for as long as the wait lasts,
    /// whether or not the metres it holds are ones this car was ever refused. Left out, a car that had been
    /// let up to the far side of a box stood on everything it had crossed to get there, the movements behind
    /// it were refused by a body that was itself waiting, and Odesa's minute abandoned half again as many
    /// cars as it does with the box entered only by whoever can clear it.
    /// </para>
    /// <para>
    /// <b>The runs and not the sections</b> (<see cref="WayCrossings.OwnRuns"/>), because the question is
    /// where the body may come to rest rather than which movement holds what: the runs are the same
    /// crossings merged, so the gaps between them are exactly the places on this way there is nothing to
    /// stand on.
    /// </para>
    /// </remarks>
    bool WaitsClearOfTheCrossings(int car, int movementWay, float noseM)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var tailM = noseM - build.LengthM - build.TailMarginM;
        foreach (ref readonly var run in _crossings.OwnRuns(movementWay))
        {
            if (run.FromM < noseM && run.ToM > tailM) return false;
        }

        return true;
    }

    /// <summary>
    /// <b>Where the junction this movement is driven over is first somebody else's</b>, in the movement's
    /// own metres, or infinity where every section of it is free — <b>asked of all of them before any is
    /// taken</b>. Taken one at a time, two cars whose lines cross twice each end up holding the half the
    /// other needs and neither can go.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It answers where and not whether</b>, which is what lets the car have the part of the box in front
    /// of the section that is held. Answered as a verdict instead, a movement refused anywhere was refused
    /// everywhere and the car stopped on its own approach — so a body sitting on the far corner of a box
    /// held the near half of it against a car that would never have reached the corner while it was there,
    /// and the picture was a junction empty but for the queue outside it.
    /// </para>
    /// <para>
    /// <b>The cut is the one the road grant makes</b> (<see cref="WhereTheGroundIsCrossed"/>): the near edge
    /// of the section, less the asker's own margin, since a section is a place and carries none of its own.
    /// That figure is what keeps this from deadlocking where a verdict did not — a car stopped a margin
    /// short of a section reserves no metre of it, so the movement crossing there still reads it free and
    /// goes, and the pair resolve rather than sitting one on each side of the ground they share.
    /// </para>
    /// <para>
    /// <b>Every section is read where it lies and none of it is written anywhere</b> (TER-5c). The table
    /// says which metres of which other join this movement is driven over; what is asked of those metres is
    /// what is standing on them, in that join's own book — the same question a grant asks, at the moment a
    /// car decides whether to commit rather than as a distance.
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
    float FirstHeldOnTheMovementM(int car, int movementWay)
    {
        var mine = RightOfWayOf(car, movementWay);

        // Read on the movement's own metres, so that what it is refused by and what it would take are one
        // set: ground it is already past is ground it is not asking for.
        var passedM = PastOnTheMovementM(car, movementWay);
        var leastM = float.PositiveInfinity;
        foreach (ref readonly var section in _crossings.Of(movementWay))
        {
            if (section.MineToM <= passedM || section.MineFromM >= leastM) continue;

            // Where inside the section the holder stands says nothing about where this car may stand: the
            // section is a piece of this way, and its near edge is the whole of what this movement has.
            if (NobodyElseIsOn(car, movementWay, section.OnWay, section.FromM, section.ToM, mine)) continue;

            leastM = MathF.Max(section.MineFromM, passedM);
        }

        // And this movement's own share of the same crossings, which is the ground a car coming the other
        // way is refused by. The same runs the car would lay, on the same test — read on this way's own
        // metres, so what is held is the near edge of the holder rather than the near edge of the run.
        foreach (ref readonly var run in _crossings.OwnRuns(movementWay))
        {
            var fromM = MathF.Max(run.FromM, passedM);
            if (run.ToM <= passedM || fromM >= leastM) continue;

            var heldM = FirstHeldOn(car, movementWay, movementWay, fromM, MathF.Min(run.ToM, leastM), mine);
            if (float.IsFinite(heldM)) leastM = MathF.Max(heldM, fromM);
        }

        return leastM;
    }

    /// <summary>One section of one join, and whether anything not crossing on this same movement is on it.</summary>
    /// <remarks>
    /// <para>
    /// <b>Everything lying over the section and not only what begins on it</b>
    /// (<see cref="LaneOccupancy.NextSpokenForOver"/>). A section is a named piece of ground rather than the
    /// road under the asker, so a stretch that began at a body further back and runs through it is the
    /// answer: read the other way round, a car whose reservation entered a join before the metres two lines
    /// meet on was invisible to the movement crossing there, and both went.
    /// </para>
    /// <para>
    /// <b>And a claim this movement has the right of way over is no answer either</b> (TER-5e). It is ground
    /// nobody has reached, held by a movement that gives way to this one, so it is given up rather than
    /// waited behind — which is what makes two crossing movements resolve the same way round whichever of
    /// them looked first.
    /// </para>
    /// <para>
    /// <b>And what is skipped is a car, never whoever else holds the same number.</b> A section is a stretch
    /// of any way and not only of a join — the way into a bay sweeps the lane running back the other way
    /// (<see cref="WayCrossings"/>) — and a lane carries the walking roster's bodies and the town's own
    /// furniture beside the traffic. Which roster an occupant is named in is carried by the stretch
    /// (<see cref="LaneSlot.Of"/>), so a walker in the road and a bollard standing in it refuse this
    /// movement like anything else on the ground it crosses; read as a car's number instead, the one is
    /// whichever car happens to be indexed there and the other is nobody at all.
    /// </para>
    /// </remarks>
    bool NobodyElseIsOn(int car, int movementWay, int way, float fromM, float toM, RightOfWay mine) =>
        float.IsPositiveInfinity(FirstHeldOn(car, movementWay, way, fromM, toM, mine));

    /// <summary>
    /// The same walk, answered as the near edge of the first stretch that refuses rather than as the fact
    /// that one does — in <paramref name="way"/>'s own metres, and never behind the piece asked about.
    /// </summary>
    /// <remarks>
    /// <b>The first is the least</b>, since the book holds a way's stretches in the order their near edges
    /// fall on it (<see cref="LaneOccupancy.NextSpokenForOver"/>), so the walk stops at the first one that
    /// binds rather than carrying a minimum through the rest of them.
    /// </remarks>
    float FirstHeldOn(int car, int movementWay, int way, float fromM, float toM, RightOfWay mine)
    {
        var at = LaneOccupancy.FromTheStart;
        while (_occupancy.NextSpokenForOver(way, fromM, toM, car, ref at, out var taken))
        {
            var crossingWithUs = taken is { Of: LaneRoster.Driving, Occupant: >= 0 }
                                 && Cars.MovementWay[taken.Occupant] == movementWay;
            if (!crossingWithUs && LaneOccupancy.Binds(taken, mine)) return MathF.Max(taken.FromM, fromM);
        }

        return float.PositiveInfinity;
    }

    /// <summary>
    /// <b>Whether something with the right of way over this movement has come for the ground it is
    /// holding</b> (TER-5e) — the one thing that takes a crossing back off a car that has already been given
    /// one, and the whole of what a revocation is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Greater and never equal</b>, which is what keeps this from being the crossing recomputed. Two
    /// movements of the same right of way settle by whichever asked first and go on holding what they were
    /// given; a movement that gives way to the one now asking never had a claim on that ground to begin
    /// with, and hands it over the moment the ask appears.
    /// </para>
    /// <para>
    /// <b>The sections and not this movement's own runs.</b> What is coming lays its road on its <em>own</em>
    /// way, which is where this movement reads it (TER-5c.1); nothing with another movement's right of way
    /// is ever on this one's join.
    /// </para>
    /// <para>
    /// <b>It is asked of a car that could still stop short and of no other</b>
    /// (<see cref="JunctionStopM"/>). Past that point the car is going in whatever anything says, and a
    /// right of way that took ground from a body already committed to it would be a rule about who is driven
    /// into rather than about who waits.
    /// </para>
    /// </remarks>
    bool TheMovementIsTakenBack(int car, int movementWay)
    {
        var mine = RightOfWayOf(car, movementWay);
        var passedM = PastOnTheMovementM(car, movementWay);
        foreach (ref readonly var section in _crossings.Of(movementWay))
        {
            if (section.MineToM <= passedM) continue;

            var at = LaneOccupancy.FromTheStart;
            while (_occupancy.NextSpokenForOver(
                       section.OnWay, section.FromM, section.ToM, car, ref at, out var taken))
            {
                if (taken.Right > mine) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The movement taken up: the car's own field, and the runs of its own join written into the book at
    /// the moment they are taken, so that a car later in this same walk is refused ground this one has just
    /// been given. <see cref="DropTheMovement"/> is the pair of it, and neither half is ever done alone.
    /// </summary>
    void TakeTheMovement(int car, int movementWay)
    {
        Cars.MovementWay[car] = movementWay;
        LayTheMovement(car, movementWay);
    }

    /// <summary>
    /// The ground this car was crossing on, given back — <b>from the car and from the book together</b>.
    /// The book is laid from the cars once a tick, so a field written away on its own leaves stretches
    /// standing against everything that crosses them for the rest of the walk that wrote it.
    /// </summary>
    /// <remarks>
    /// <b>And from what the driver was told, which is the third reader of the same fact.</b> The two are one
    /// hold seen from two sides — the registry, which refuses the traffic crossing it, and
    /// <see cref="CarFleet.BoxIsOurs"/>, which the catalogue's entry turns on — so a drop that left the
    /// second standing would be a driver crossing a junction on ground the town had already given away. It
    /// is cleared here rather than at each of the eight places a movement is dropped, because a pairing kept
    /// by hand is a pairing that comes apart: the one that came apart was a car re-aimed mid-junction by an
    /// errand (AMB-9, SRV-5), which is the one drop that happens outside the driving step.
    /// </remarks>
    void DropTheMovement(int car)
    {
        var held = Cars.MovementWay[car];
        if (held == CarFleet.NoWay) return;

        Cars.MovementWay[car] = CarFleet.NoWay;
        Cars.BoxIsOurs[car] = false;
        _occupancy.Withdraw(held, car, LaneUse.Claimed);
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
        // AMB-4: a red is a rule about whose turn it is, and an ambulance on a call is not taking a turn.
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
        // An ambulance on a call is exempt from the rule (AMB-4), so it cannot be in breach of it. Counted
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
