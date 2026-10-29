using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>A drive leg</b> (CAR-15): the ways of the route the car is on, the manoeuvre at a bay at either end
/// of it, and the clock that says a leg is getting nowhere. Nothing here steers or brakes — what a car is
/// doing is the standing rules' (<see cref="TickCar"/>), and what is decided here is only which line it
/// is on next.
/// </summary>
/// <remarks>
/// <b>It is the walker's own tick in the driver's words</b> (<see cref="WalkingItsRoute.WalkTheWay"/>, PER-25). One
/// search lays a chain of the network's ways, the body is held on each in turn, and the leg is laid again
/// from wherever the body has got to when a chain runs out. What a driver holds that a walker does not is
/// the assembled line over the next few lanes — a car at road speed has to see the corners a walker takes
/// one stride at a time — and the gear: a manoeuvre's pieces are driven in whichever one they were shaped
/// for (GEN-4f), which is the whole of parking and unparking.
/// <para>
/// <b>The one state here is the manoeuvre's</b> (<see cref="Agents.Car.Control.Manoeuvres"/>): which shape a car was given and
/// which piece of it it is on. Everything else about which step of a leg the car is on is read off where it
/// is standing — in a bay or on the road — so a car cannot be doing one thing and be recorded as doing
/// another.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>How close each driver has come to the end of the way it is on, and how long since it last did better.</summary>
    readonly LegProgress _driveProgress;

    /// <summary>
    /// How many legs were given up where they stood — the road covered nothing for a whole patience and
    /// there was nothing left to try. <b>A town with many of these is a town to go and look at</b>, which
    /// is why it is a figure the instruments print rather than a quiet correction.
    /// </summary>
    public long LegsGivenUp { get; private set; }

    /// <summary>How many times a leg priced up the stretch it could not get into and laid its route again.</summary>
    public long ReroutesTaken { get; private set; }

    /// <summary>And how many gave the place they were going to up for another one nearer where they got to.</summary>
    public long PlacesGivenUp { get; private set; }

    /// <summary>How many times a driver took the lane it was standing on after losing the line it was given.</summary>
    public long LinesReacquired { get; internal set; }

    /// <summary>
    /// <b>How many car-ticks spent the margin the speed profile keeps back</b> (S-2's usable grip). Frequent use of
    /// it is a planning failure and not a safety feature: constant flat-out braking means the profile or
    /// the looking is wrong upstream, which is why it is counted rather than merely done.
    /// </summary>
    public long HardBrakings { get; private set; }

    /// <summary>
    /// <b>One turn of the driver's own head</b>: the next step of the leg where the line in hand is spent,
    /// and the clock that decides this leg is not getting anywhere.
    /// </summary>
    /// <remarks>
    /// <b>The sensing is not on this clock.</b> A body moves every physics tick, the claims
    /// are laid and answered every tick, and the speed profile is taken every tick; what runs here is the
    /// leg's own arithmetic, which is about distances of tens of metres.
    /// </remarks>
    void DecideDriver(int car, float sinceLastDecisionS)
    {
        // A car with nobody in it takes no action, and a hand at the wheel suspends the leg — neither is a
        // car for this to have opinions about, and the clock is the leg's rather than the body's.
        if (!Cars.Driven[car] || Cars.Broken[car] || Cars.Action[car] == CarAction.Hand)
        {
            _asksTheRoute[car] = false;
            _driveProgress.Restart(car);
            return;
        }

        LayTheRestOfTheLine(car);
        DecideTheAction(car, sinceLastDecisionS);

        if (TheLineIsSpent(car) && TakeTheNextStepOfTheLeg(car))
        {
            _driveProgress.Restart(car);
            return;
        }

        WatchTheProgress(car, sinceLastDecisionS);
    }

    /// <summary>
    /// <b>What the car's action decides, on the car's own clock</b> rather than its tick (CAR-15b): into a bay's
    /// manoeuvre or onto a pass from the route's line, a manoeuvre shaped afresh while it waits for the last one's
    /// ground, a pass looked round for and given up, and the lane under a car off its line taken.
    /// </summary>
    void DecideTheAction(int car, float sinceLastDecisionS)
    {
        var town = new CarTown(this);
        switch (Cars.Action[car])
        {
            case CarAction.Follow:
                _following.Decide(car);

                // A pass drawn now is looked round for at once — and so is the room to move across.
                if (Cars.Action[car] == CarAction.Overtake) _overtaking.Decide(car, 0f);
                if (Cars.Action[car] == CarAction.Switch) _switching.Decide(car, 0f);
                return;

            case CarAction.Overtake or CarAction.BackUp:
                _overtaking.Decide(car, sinceLastDecisionS);
                return;

            case CarAction.Switch:
                _switching.Decide(car, sinceLastDecisionS);
                if (_switching.HasNoRoomToMoveAcross(car)) DriveOnPastTheMoveAcross(car);
                return;

            case CarAction.Park:
                _parkingIn.Decide(car, sinceLastDecisionS);
                return;

            case CarAction.Unpark:
                _pullingOut.Decide(car, sinceLastDecisionS);
                return;

            case CarAction.Rejoin:
                _rejoining.Decide(ref town, car, sinceLastDecisionS);
                return;
        }
    }

    /// <summary>
    /// <b>The line has been driven to its end and the car has stopped there.</b> A leg's steps hand over at
    /// rest and nowhere else: a manoeuvre's pieces are driven in their own gears, and a car changing gear while
    /// it is still rolling is a car driven into whatever it was reversing away from. <b>A piece is spent at its
    /// end and not a car length short of it</b> (<see cref="SpentWithinM"/>): its end is where the car stands square in the
    /// bay, or straight on its lane, and short of that it is still turning.
    /// </summary>
    bool TheLineIsSpent(int car) =>
        Cars.Line[car].ArcCount > 0
        && Cars.Line[car].LengthM - Cars.ProgressM[car] <= SpentWithinM(car)
        && MathF.Abs(Cars.AlongMps[car]) <= _config.Driving.StopSpeedMps;

    /// <summary>
    /// <b>How near its end a line is spent</b>: a car's length of a route's, and a manoeuvre's piece at its end (GEN-4i) —
    /// a parallel park's pull forward and a straightening's turn are shorter than a flank, and spent a flank short they were
    /// spent standing still at their start.
    /// </summary>
    float SpentWithinM(int car) => IsManoeuvring(car) ? _config.Driving.ParkedMiddleWithinM : Cars.BuildOf(car).LengthM;

    /// <summary>
    /// <b>The next line this leg is driven on</b>, read off where the car is standing rather than off a
    /// stage it was recorded in: on a piece of a manoeuvre it is the next piece, the bay or the road, and on the
    /// road it is the route laid again from here.
    /// </summary>
    /// <returns>Whether there was a next line at all — false is a leg with nothing left to drive.</returns>
    bool TakeTheNextStepOfTheLeg(int car)
    {
        if (IsManoeuvring(car))
        {
            // A manoeuvre out of a bay that has not been had yet is not driven, whatever its line reads.
            return _manoeuvres.IsBegun(car) && TheManoeuvreIsDriven(car);
        }

        // A line that ends at the place the leg is aimed at in the road has been driven: the car stands where it
        // decided to stop, which is the step — and a car standing where it was going is not a leg getting nowhere,
        // so no clock is run against it. What it does there is its errand's to say (<see cref="StandsAtItsPlace"/>).
        if (Cars.StopsAtItsPlaceOf(car)) return true;

        // A line that stops for a bay waits there for the manoeuvre into it (<see cref="CarAction.Park"/>),
        // which is not a line run out.
        if (IsOnTheFinalApproach(car)) return false;

        // Anywhere else the road has run out under the car, so it is taken again from where the body has
        // actually got to — the walker's own answer to a chain that runs out (PER-25).
        return TakeTheRoadAgain(car);
    }

    /// <summary>
    /// <b>The line laid again from the lane the body is standing on</b>, route and all. It is a step of the
    /// leg only where it got the car more road than it had: a lane with nothing off the end of it answers
    /// the same line however often it is asked, and a step that changed nothing would hand the clock back
    /// for ever to a car standing at a dead end.
    /// </summary>
    /// <remarks>
    /// <b>A dead end is refused before it is searched.</b> This is asked every decision a car spends at
    /// the end of its line, and a search of the network for an answer the road has already given is a
    /// leg's whole routing budget spent standing still.
    /// </remarks>
    bool TakeTheRoadAgain(int car)
    {
        var lane = Cars.LaneOf(car);
        if (lane < 0 || _roads.LanesFrom(lane).Length == 0) return false;

        var leftM = Cars.Line[car].LengthM - Cars.ProgressM[car];
        Cars.ClearRoute(car);
        return TakeTheLaneUnderIt(car) && Cars.Line[car].LengthM - Cars.ProgressM[car] > leftM;
    }

    /// <summary>
    /// <b>The car is in the bay and the leg is over</b>: the place is this car's until something else
    /// drives it away, and the car is stood down in it.
    /// </summary>
    void ParkIt(int car, int bay)
    {
        if (bay >= 0)
        {
            _parking.Occupy(bay, car);
            ParkedSquareness.Note(
                _parking.IsKerb(bay), Cars.PositionM[car], Cars.HeadingRad[car], _parking.CentreM(bay), _parking.HeadingRad(bay),
                _config.Driving.ParkedSquareWithinDeg);
        }

        BaysParkedIn++;
        StandTheCarDown(car);
        BeginTheStand(car);
    }

    /// <summary>
    /// <b>Whether this leg is getting anywhere, and what is done about it when it is not</b> — the clock
    /// both agent kinds are given up by (<see cref="LegProgress"/>). The road is priced up and the route
    /// laid again first, because a leg that cannot get down this street may well get down another; past
    /// the last reroute the answer is that the leg is over.
    /// </summary>
    internal void WatchTheProgress(int car, float elapsedS)
    {
        var remainingM = RemainingOnTheDriveM(car);

        // The one wait that spends no clock is a light, which will change on its own — and it buys the
        // wait rather than the standing, so the clock is held where it is rather than given back. Only a car getting
        // nowhere is asked whether one is why: one getting somewhere is getting somewhere wherever it is.
        var widthM = Cars.BuildOf(car).WidthM;
        if (!_driveProgress.Gains(car, remainingM, widthM) && WaitingForAReasonItCanSee(car)) _driveProgress.Hold(car, remainingM);
        else _driveProgress.Note(car, remainingM, widthM, elapsedS);

        if (!_driveProgress.IsStuck(car, _config.CarPatienceS * Cars.FuseJitter[car])) return;

        // A car waiting at its bay for the street its way in needs has no road to route round: laid again, the route
        // ends where it stands. What holds it is at the bay, so the bay is what is given up.
        // A car waiting to move across for its route has a route laid again from where it waits ask for the same move
        // across, so it drives on past it instead (CAR-53): through the junction its own lane leads to, routed again from there.
        if (Cars.Action[car] == CarAction.Switch && !Cars.Pass[car].Begun && Cars.Reroutes[car] < _config.Patience.ReroutesPerLeg)
        {
            Cars.Reroutes[car]++;
            DriveOnPastTheMoveAcross(car);
            ReroutesTaken++;
            _driveProgress.Restart(car);
            return;
        }

        var waitsAtItsBay = Cars.Action[car] == CarAction.Park && !Manoeuvres.IsBegun(car);
        if (!waitsAtItsBay && Cars.Reroutes[car] < _config.Patience.ReroutesPerLeg && MarkTheWayBlocked(car))
        {
            ReroutesTaken++;
            _driveProgress.Restart(car);
            return;
        }

        // The road was not the problem, so the place this leg is going to might be: another bay near where
        // the car has actually got to, and the route to that one. It is the destination's own last chance — <b>and
        // only one</b>, spent as the last reroute is: a town with a free bay near every jam otherwise gave the place up
        // for the next one a patience at a time, and the leg never ended (CAR-15a). <b>Never a leg aimed at a place in the
        // road</b>, which a bay is no other answer for: it is the errand's own to lay again once the leg is given up.
        if (!IsAimedAtAPlaceInTheRoad(car) && Cars.Reroutes[car] <= _config.Patience.ReroutesPerLeg
            && RetargetTheBay(car, Cars.PositionM[car], BayAimedAt(car)))
        {
            Cars.Reroutes[car] = (byte)(_config.Patience.ReroutesPerLeg + 1);
            PlacesGivenUp++;
            _driveProgress.Restart(car);
            return;
        }

        GiveUpTheLeg(car);
    }

    /// <summary>
    /// How long this car has been getting nowhere, which is a reading the instruments print and never a
    /// decision anything here takes.
    /// </summary>
    public float GettingNowhereForS(int car) => _driveProgress.StuckForS(car);

    /// <summary>
    /// <b>How much of this leg is left to drive</b>: what remains of the lane the car is on, with the
    /// ground back onto its line counted as ground still to be covered.
    /// </summary>
    /// <remarks>
    /// <b>The lane and not the line, and never the point the wheel is aimed at.</b> The line is re-laid a
    /// lane at a time as the car leaves each, so what is left of it stands at a sight distance whether the
    /// leg is going well or not at all — which is the carrot a clock cannot measure against. What is left
    /// of one lane shrinks as the car drives it and the handover restarts the clock
    /// (<see cref="AdvanceLane"/>), exactly as a walker's way does.
    /// </remarks>
    float RemainingOnTheDriveM(int car)
    {
        var backOntoItM = MathF.Max(0f, Cars.OffLineM[car] - OffTheLineAllowanceM(car));
        if (Cars.Line[car].ArcCount == 0) return backOntoItM;

        var endsAtM = Cars.Line[car].LaneCount > 0
            ? Cars.LaneEndsOf(car)[0]
            : Cars.Line[car].LengthM;

        return backOntoItM + MathF.Max(0f, endsAtM - Cars.ProgressM[car]);
    }

    /// <summary>
    /// <b>The leg given up where the car stands</b>: the place it was going to is the town's again, the
    /// line is dropped and the car is stood down (CAR-9a). It is the one exit every stuck car reaches, and
    /// it is finite because the clock that reaches it is.
    /// </summary>
    void GiveUpTheLeg(int car)
    {
        _parking.Release(car);
        LegsGivenUp++;
        StandTheCarDown(car);
    }

    /// <summary>
    /// <b>The stretch this car is blocked entering, priced up</b> so other drivers route around it, and the
    /// route in hand dropped so the next line is drawn over the new answer.
    /// </summary>
    /// <remarks>
    /// <b>The mark expires and is never swept</b> — nothing unmarks a road by inspection, so a stretch
    /// that is still blocked is marked again by whoever finds it so.
    /// </remarks>
    bool MarkTheWayBlocked(int car)
    {
        var lane = LaneAheadOnTheLine(car);
        if (lane < 0) return false;

        var link = _driving.LinkOfLane(lane);
        if (link == TravelGraph.NoLink) return false;

        _surcharges.Mark(link, _config.CarBlockedWayPriceM, _config.CarBlockedWayLifeS);
        Cars.Reroutes[car]++;
        Cars.ClearRoute(car);
        return TakeTheLaneUnderIt(car);
    }

    /// <summary>
    /// The stretch the car is blocked <em>entering</em>, which is the one worth marking: the lane after
    /// the one under it, or the one under it where the line goes no further.
    /// </summary>
    int LaneAheadOnTheLine(int car)
    {
        var lanes = Cars.Line[car].LaneCount;
        if (lanes == 0) return CarFleet.NoLane;

        var chain = Cars.ChainOf(car);
        var starts = Cars.LaneStartsOf(car);
        var ahead = 0;
        while (ahead < lanes - 1 && Cars.ProgressM[car] >= starts[ahead + 1]) ahead++;

        return ahead + 1 < lanes ? chain[ahead + 1] : chain[ahead];
    }

    /// <summary>
    /// The one wait that spends no clock: <b>a light</b>, which will change on its own (TLT-2a).
    /// </summary>
    /// <remarks>
    /// Everything else that stands still spends the clock, including a lawful yield: waiting for a
    /// junction somebody else is in is correct right up until it has been correct for half a minute, and
    /// after that it is a jam rather than traffic. <b>A car waiting to leave a bay is one of those</b> —
    /// its way out is a movement across the street like a junction's, and a bay it cannot get out of for
    /// half a minute is a place to give up rather than a wait to be excused. <b>And so is a car with a light
    /// somewhere ahead and open road between</b>: the light is a reason only for a queue that reaches it
    /// (<see cref="QueuedUpTo"/>), and a car stood behind a wreck or in a jammed box within sight of a red
    /// spent its clock on the greens alone.
    /// </remarks>
    bool WaitingForAReasonItCanSee(int car)
    {
        var lightM = LightAheadM(car);
        return lightM <= QueueReachM(car) && QueuedUpTo(car, lightM);
    }

    /// <summary>
    /// <b>Whether the road from this car's nose to <paramref name="aheadM"/> in front of it is a queue</b>: bodies on its
    /// own ways all the way there, none with a gap in front of it another car of this one's length could stand in.
    /// </summary>
    bool QueuedUpTo(int car, float aheadM)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var roomM = build.LengthM + (2f * _config.Driving.StandOffM);
        var noseM = Cars.ProgressM[car] + build.NoseAheadOfAxleM;
        var clearFromM = noseM;
        Span<LineWay> ways = stackalloc LineWay[MostWaysAlongALine];
        var count = _ground.WaysAlong(car, noseM, noseM + aheadM, ways);
        for (var index = 0; index < count; index++)
        {
            ref readonly var way = ref ways[index];
            var fromM = way.FromM;
            while (_occupancy.FirstBodySeen(way.Way, fromM, way.ToM, car, LaneRoster.Driving, out var body))
            {
                if (LineWays.OnTheLineM(way, body.FromM) - clearFromM >= roomM) return false;

                clearFromM = MathF.Max(clearFromM, LineWays.OnTheLineM(way, body.ToM));
                if (body.ToM <= fromM) break;

                fromM = body.ToM;
            }
        }

        return noseM + aheadM - clearFromM < roomM;
    }

    /// <summary>
    /// How far back a queue at a light reaches, for a car of this length (<see cref="CarBuild.LightQueueReachM"/>).
    /// <b>The test is "a light's hold ahead, within a queue's length of it"</b>, so it has to cover the whole queue and
    /// not only its front — and a body bogged on the verge beside that junction meets it too.
    /// </summary>
    float QueueReachM(int car) => Cars.BuildOf(car).LightQueueReachM;
}
