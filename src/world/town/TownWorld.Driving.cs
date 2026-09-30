using System.Numerics;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The standing rules (S-1…S-7)</b>: how a car is driven at all, underneath whichever line the leg
/// has handed it — holding that line at the rear axle, taking speed as the minimum of every constraint
/// with a reaction lead, watching ahead along the line actually being driven, claiming the junction
/// ahead and releasing the one behind, and holding a stop the car has already made.
/// </summary>
/// <remarks>
/// The order is the argument. Progress is read before anything else because every other question is
/// asked <em>from</em> it; the line is re-laid the moment the car reaches the lane after the junction,
/// which is a fact about the line and not something to wait a decision for; and the tyres are last,
/// because what they are given is a command and never a position.
/// <para>
/// <b>Which procedure runs is the car's action</b> (CAR-15b, <see cref="CarAction"/>): the route's own line for a
/// car following it, getting past something on it, backing down it or coming up it to its bay, and a piece of the
/// manoeuvre it shaped for itself, in the gear that piece was shaped for, for a car getting into a bay or out of one.
/// </para>
/// <para>
/// <b>Everything here runs every tick.</b> What runs on the driver's own clock is the leg
/// (<see cref="DecideDriver"/>), which is about distances of tens of metres; the claims, the looking and
/// the profile are about a gap that had already closed.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>One tick of one car's body: where it is, what it can see, and what the pedals and the wheel are asked for.</summary>
    void TickCar(int car)
    {
        // EVA-5: <b>a car on somebody's arm is not driving</b>. One end of it is off the ground and the other
        // is rolling where the truck takes it, which is exactly a wreck's state said of a car nobody broke —
        // so what it gets is the trailer's two wheels and no driving at all.
        if (_recovery.OnTheHookOf[car] >= 0)
        {
            TrailerWheels(car);
            return;
        }

        var pose = PoseOf(car);
        if (Cars.Action[car] == CarAction.Hand)
        {
            // Under a hand nothing of the leg is decided and no soft rule is consulted (S-7).
            HandDrive(car, pose);
            return;
        }

        if (!Cars.Driven[car])
        {
            Hold(car, pose, DrivingHold.None);
            return;
        }

        Cars.SinceDecisionS[car] += _config.TickSeconds;

        // Off a route there is no movement into a box: a manoeuvre at a bay is laid over ground the graph
        // has no turn for. Left as it was, the last junction this car approached would still be what its
        // indicator is announcing.
        if (Cars.Line[car].LaneCount == 0) Cars.TurningAtTheBox[car] = false;

        var town = new CarTown(this);
        switch (Cars.Action[car])
        {
            case CarAction.Follow or CarAction.Overtake or CarAction.BackUp:
                if (ReadTheLine(car, pose, out var progressM, out var alongMps, out var coveredM))
                {
                    DriveTheRoute(car, pose, progressM, alongMps, coveredM);
                }

                break;

            case CarAction.Park:
                _parkingIn.Tick(ref town, car, pose);
                break;

            case CarAction.Unpark:
                _pullingOut.Tick(ref town, car, pose);
                break;

            case CarAction.Rejoin:
                _rejoining.Tick(ref town, car, pose);
                break;

            default:
                Hold(car, pose, DrivingHold.None);
                break;
        }
    }

    /// <summary>
    /// <b>The tick of a car driving the route's own line</b>, once the line has been read — handed to whichever action
    /// the car is in after reading it, since a pass the car is no longer on the lane of is over.
    /// </summary>
    void DriveTheRoute(int car, in CarPose pose, float progressM, float alongMps, float coveredM)
    {
        var town = new CarTown(this);
        switch (Cars.Action[car])
        {
            case CarAction.Overtake:
                _overtaking.Tick(ref town, car, pose, progressM, alongMps, coveredM);
                return;

            case CarAction.BackUp:
                _backingUp.Tick(ref town, car, pose, progressM, alongMps, coveredM);
                return;

            default:
                _following.Tick(ref town, car, pose, progressM, alongMps, coveredM);
                return;
        }
    }

    /// <summary>
    /// <b>The route's line under the car, read</b>: where along it the body is, the lanes it has passed shifted off the
    /// chain and the chain grown to its sight. False where the car is off it past what it may be (CAR-10a), which
    /// hands it to <see cref="Rejoining"/>, stopping.
    /// </summary>
    bool ReadTheLine(int car, in CarPose pose, out float progressM, out float alongMps, out float coveredM)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var forward = pose.Forward;
        alongMps = Vector2.Dot(pose.VelocityMps, forward);
        var rearAxleM = CarFollower.RearAxleM(build, pose.PositionM, forward);
        var town = new CarTown(this);
        if (Cars.Line[car].LaneCount == 0)
        {
            progressM = coveredM = 0f;
            _rejoining.LoseTheLine(ref town, car, pose, alongMps, rearAxleM);
            return false;
        }

        progressM = CarFollower.ProgressM(build, Cars.LineOf(car), rearAxleM, Cars.ProgressM[car]);
        coveredM = MathF.Abs(progressM - Cars.ProgressM[car]);
        if (progressM >= Cars.LaneStartsOf(car)[1] && Cars.Line[car].LaneCount > 1)
        {
            progressM = AdvanceLane(car, rearAxleM, progressM);
        }

        Cars.ProgressM[car] = progressM;
        Cars.AlongMps[car] = alongMps;
        Cars.GroundCoefficient[car] = _terrain.At(pose.PositionM).Coefficient;

        // A pass is laid along the lane the car is on and nowhere else (CAR-46).
        if (_overtaking.IsOffTheLaneOfItsPass(car)) Enter(car, CarAction.Follow);

        Cars.OffLineM[car] = CarFollower.OffLineM(Cars.LineOf(car), rearAxleM, progressM, _overtaking.AsideAtM(car, progressM));

        // <b>Being off the line is ordinary; being off it by this much is not</b> (CAR-10a). A line is a
        // recommendation and every car holds it with its own steering, so a long car cuts a corner a short
        // one takes cleanly and neither is corrected — what is watched for is the car that is no longer
        // driving the line at all.
        if (Cars.OffLineM[car] > OffTheLineAllowanceM(car))
        {
            _rejoining.LoseTheLine(ref town, car, pose, alongMps, rearAxleM);
            return false;
        }

        // A line laid a sight distance ahead runs out mid-lane on a town whose runs are kilometres long,
        // and a car that brakes for the end of its own knowledge reads as timidity. The chain is grown
        // from its far end, so nothing already laid moves and the car's progress is untouched.
        if (Cars.Line[car].LengthM - progressM < SightM(car)
            && Cars.Line[car].LaneCount < LineAssembler.MostLanes
            && !IsOnTheFinalApproach(car)
            && _roads.LanesFrom(Cars.ChainOf(car)[Cars.Line[car].LaneCount - 1]).Length > 0)
        {
            LayLine(car, Cars.Line[car].LaneCount, progressM);
        }

        return true;
    }

    /// <summary>
    /// <b>A car driven forwards down the route's own line</b>: the junction ahead of it, the paint across it and what
    /// the claims say is down it, with whatever its action says about a pass it is waiting for or the room it means
    /// to back up for.
    /// </summary>
    void DriveOnTheLine(
        int car, in CarPose pose, float progressM, float alongMps, float coveredM, bool waitsToPass = false,
        float passAsideM = 0f, float backUpM = 0f, bool blocked = false, float waitAtM = float.PositiveInfinity)
    {
        var context = SetTheContext(car, progressM, coveredM, waitsToPass, passAsideM, backUpM, blocked, waitAtM);
        Drive(
            car, Cars.BuildOf(car), pose, Cars.LineOf(car), progressM, Cars.Line[car].LengthM, context, pose.Forward,
            alongMps, reverse: false);
    }

    /// <summary>What a car on the route's line is told about the world this tick, and the junction ahead of it read.</summary>
    DriveContext SetTheContext(
        int car, float progressM, float coveredM, bool waitsToPass, float passAsideM, float backUpM, bool blocked,
        float waitAtM = float.PositiveInfinity)
    {
        // S-4: the junction ahead. Whether the box is this car's is its grant's to say, and a light is in the
        // grant too — its hold is ground like any other (TLT-1).
        ReadTheBoxAhead(car, progressM, out var toTheBoxM, out var claimed);
        Cars.ToTheBoxM[car] = toTheBoxM;
        Cars.BoxIsOurs[car] = claimed;

        // The grant was taken against the claims while they were being laid, so it is a distance from where the
        // nose stood then: walking it in by the ground covered since is what stops it receding at exactly
        // the car's own speed, which is the same correction a manoeuvre's piece gets (<see cref="DriveTheWay"/>).
        // <b>And the place this car was sent to is a stop point like any other</b> (AMB-5, EVA-3, SRV-6,
        // CTL-8a): a casualty, a wreck, a scene a police car is closing the road at, or a place a hand
        // named. It is a term of the same minimum the grant is in, so a driver stopping for one is running its
        // line on the road that place left it.
        var context = new DriveContext(
            Cars.GroundCoefficient[car], Cars.AuthorityM[car] - coveredM, Cars.GrantCutBy[car], ToTheSceneM(car),
            Cars.GrantMarginM[car], waitsToPass, Cars.HorizonM[car] - coveredM, passAsideM, backUpM, blocked, waitAtM);

        Cars.Context[car] = context;
        return context;
    }

    /// <summary>
    /// S-1, S-2 and S-5: <b>the wheel is pure pursuit and the pedals are the speed profile</b>. Nothing
    /// here decides what the car is doing — only how what it is doing is delivered by the tyres.
    /// </summary>
    void Drive(
        int car, in CarBuild build, in CarPose pose, ReadOnlySpan<ArcSeg> line, float progressM, float lengthM,
        in DriveContext context, Vector2 travel, float alongMps, bool reverse)
    {
        var rearAxleM = CarFollower.RearAxleM(build, pose.PositionM, pose.Forward);
        var lookaheadM = CarFollower.LookaheadM(build, MathF.Abs(alongMps), _config.Driving.LookaheadS);

        // Pure pursuit asks, or the pass the car is on (CAR-46); the rack answers (CAR-3a). The wheel as it stands is
        // carried into the frame of the gear this tick drives in, since a reverse command is the same wheel with its
        // sign turned round on the way out — whichever gear it was left in, so a change of gear is no jump of the rack.
        var wasRad = reverse ? -Cars.Command[car].SteerRad : Cars.Command[car].SteerRad;
        var wantedRad = Cars.Pass[car].Begun
            ? CarFollower.SteerThePass(build, line, Cars.Pass[car], progressM, rearAxleM, travel, lookaheadM)
            : CarFollower.Steer(build, line, progressM, rearAxleM, travel, lookaheadM);
        var steerRad = build.WheelWoundTo(wasRad, wantedRad, _config.TickSeconds);

        // Where the foot already was, so the pedal travels rather than snapping — and the profile plans from
        // there. It is along the direction being driven on both sides of the gear, because the reverse command
        // negates the wheel and nothing else.
        var lastMps2 = CarFollower.PedalMps2(Cars.Command[car]);
        var targetMps = CarFollower.TargetSpeedMps(
            _config, build, line, Cars.EntriesOf(car), progressM, lengthM, steerRad, alongMps, lookaheadM, context,
            out var hold, out var plannedMps, Cars.Pass[car], lastMps2);

        // <b>A manoeuvre at a bay is driven at manoeuvring pace</b> whichever gear its piece is in, and the
        // reverse cap is that pace — deliberately off the forward cap's scale, because this is its only
        // use. A line with no lanes is the whole of the test: a manoeuvre's pieces are the only such lines
        // (GEN-4f).
        var capMps = reverse || Cars.Line[car].LaneCount == 0 ? build.ReverseMaxMps : float.PositiveInfinity;

        // AMB-4: <b>a blue light buys the road and never the tyres.</b> A car on a call keeps every
        // constraint the profile already takes and plans above what holds every other car at a light — the
        // light's hold on the road (TLT-1) — so without a pace of its own it reaches the gear's cap on the
        // first straight it meets and arrives as a second casualty.
        if (Cars.BlueLight[car]) capMps = MathF.Min(capMps, _config.Ambulance.CallPaceMps);

        // And a pace somebody put on this car, which is neither the road's nor the build's: an escort held
        // under the pace of what it is escorting keeps station by being caught rather than by being told to
        // (<c>IdlePlan.EscortPaceShare</c>). It is folded in with the profile's own terms and not instead of
        // them, so a corner, a queue and a stop line all still outrank it.
        capMps = MathF.Min(capMps, Cars.PaceMps[car]);
        targetMps = MathF.Min(targetMps, capMps);

        // The ceiling on the next claim. It is the profile's own answer with the grant left out, so a
        // car held at a standstill by the queue in front is not held to a standstill's worth of road — and
        // under the same caps, so the ground a car holds is the ground at the pace it will drive.
        Cars.PlannedMps[car] = MathF.Min(plannedMps, capMps);

        var pedals = CarFollower.Pedals(
            _config, build, steerRad, targetMps, alongMps, context.GroundCoefficient, _config.TickSeconds, lastMps2);
        var command = reverse ? Reversed(pedals) : pedals;

        // <b>The margin the profile kept back, spent</b>: the profile plans every stop against usable grip
        // (S-2), and this is the tick where that margin is no longer enough. It is asked of
        // the profile's own answer rather than instead of it — what it overrides is the pedal and nothing
        // else — and it is asked on every tick, because a hazard inside braking distance is not something
        // to discover at the end of a decision interval. Braking and never swerving: verifying a lane is
        // clear takes time this does not have.
        if (CarFollower.IsAHazard(_config, build, alongMps, context))
        {
            command = command with { ThrottleMps2 = 0f, BrakeMps2 = build.BrakingMps2, Handbrake = false };
            HardBrakings++;
        }

        Cars.Command[car] = command;
        Cars.Hold[car] = hold;
        Tyres(car, pose);
    }

    /// <summary>The same command, driven backwards: the gear, and the wheel turned against the direction the axle travels.</summary>
    DriveCommand Reversed(DriveCommand command) =>
        command with { SteerRad = -command.SteerRad, Reverse = true };

    /// <summary>
    /// <b>A stopped car that has lost its line takes the lane it is actually standing on</b> and starts
    /// again — the driver's half of PER-25's second walk: a body off the network heads straight back onto
    /// it, which for a car is the lane under it and the follower's own steering onto that lane's line.
    /// Anywhere else it stays put and the leg's clock ends it.
    /// </summary>
    /// <remarks>
    /// <b>The lane that runs the way the car is pointing, and never simply the nearest one.</b> A car set
    /// off down the oncoming lane is a head-on rather than a recovery — but refusing the nearest lane on
    /// that ground and stopping there leaves the one case this is most needed for with no answer at all: a
    /// body shoved across the centreline stands nearest the oncoming line, pointing the way it always was,
    /// and the lane it wants is that one's reverse. Refusing it left the car standing in the other stream
    /// on ground a car may drive on, and nothing but the leg's own clock had anything to say about it.
    /// </remarks>
    /// <returns>Whether a line was laid over the lane under it.</returns>
    bool Reacquire(int car, Vector2 rearAxleM)
    {
        if (!_terrain.At(rearAxleM).Drivable) return false;

        var forward = ForwardOf(car);
        var under = TheCarriagewayUnder(rearAxleM, forward);
        if (under.Lane < 0) return false;
        if ((under.At.PositionM - rearAxleM).Length() > _config.CarOffPathM * OffLineTolerance) return false;
        if (Vector2.Dot(under.At.Direction, forward) <= 0f) return false;

        // <b>The same lanes taken again are not a line to lay again.</b> A body that has come to rest off
        // its line is asked this every tick until something moves it, and laying the line means searching
        // the network for the route behind it — the same answer, from the same lanes, for as long as the
        // car stands there. What has to keep up is where the body is on it, which is arithmetic.
        Cars.ProgressM[car] = under.AlongM;
        var chain = Cars.ChainOf(car);
        var lanes = Cars.Line[car].LaneCount;
        if (lanes > 0 && chain[0] == under.Lane
            && (under.Onward == CarFleet.NoLane || (lanes > 1 && chain[1] == under.Onward)))
        {
            return false;
        }

        LayLine(car, TheChainFrom(car, under));
        Cars.ProgressM[car] = under.AlongM;
        LinesReacquired++;
        return true;
    }

    /// <summary>A car that is doing nothing this tick, and the one reason it is not.</summary>
    void Hold(int car, in CarPose pose, DrivingHold why)
    {
        Cars.Command[car] = why == DrivingHold.None
            ? DriveCommand.Parked
            : DriveCommand.Stopping(Cars.BuildOf(car).BrakingMps2);
        Cars.Hold[car] = why;
        Cars.Context[car] = DriveContext.Clear;
        Tyres(car, pose);
    }

    /// <summary>
    /// The car has reached the second lane of its chain: the chain shifts down by one, a new lane is drawn
    /// onto the end of it, and the line is re-laid.
    /// </summary>
    float AdvanceLane(int car, Vector2 rearAxleM, float progressM)
    {
        var chain = Cars.ChainOf(car);

        // <b>Taking the next lane is progress</b>, and the clock that decides a leg is getting nowhere is
        // measured against what is left of the lane the car is on (<see cref="RemainingOnTheDriveM"/>).
        // Left standing, that clock would run up over a whole lane and call a car that had just been
        // handed a fresh one stuck — which is the walker's own lesson said of a driver (PER-25).
        _driveProgress.Restart(car);

        // Where the new line's origin sits on the old one, taken before the old one is overwritten: the
        // projection is a search in a window around where the car last was, and seeding it with a progress
        // measured from the wrong origin hands the car a place a hundred metres up the road.
        //
        // <b>Both lines start at a lane's own nought</b> (TER-5i), so the new line's origin is where the old
        // one had the second lane of its chain begin.
        var shiftM = Cars.LaneStartsOf(car)[1];
        var lanes = Cars.Line[car].LaneCount;
        for (var index = 1; index < lanes; index++) chain[index - 1] = chain[index];

        // A pass carries on into the next lane, measured from where that lane begins (CAR-46).
        _overtaking.ShiftTheLine(car, chain[0], shiftM);

        LayLine(car, lanes - 1);
        return CarFollower.ProgressM(
            Cars.BuildOf(car), Cars.LineOf(car), rearAxleM, MathF.Max(0f, progressM - shiftM));
    }

    /// <summary>
    /// The next lane the car's own route says to take, which the road is known to join to
    /// <paramref name="fromLane"/>. A car is given a route and never draws a turn; what draws is where it
    /// is going next once it gets where it was going.
    /// </summary>
    /// <remarks>
    /// The route runs out rather than ends: a car carries a bounded run of it
    /// (<see cref="CarFleet.RouteLanesPerCar"/>), so a long trip is planned again from where the car has
    /// got to — the same call an arrival makes, with the destination changed rather than kept, which is
    /// why a truncated route and a completed one need no flag to tell them apart. The tour is the
    /// fallback: a car whose route cannot be found draws its next turn rather than standing still,
    /// because a car stopped in a lane is an obstruction the whole street queues behind.
    /// </remarks>
    /// <param name="searched">
    /// Whether the network has already been searched for the line being laid. <b>One search per line,
    /// however many lanes it takes</b>: a search that came back with nothing comes back with nothing
    /// again from the lane the tour draws next, and a line is a dozen of those.
    /// </param>
    int NextLaneOnRoute(int car, int fromLane, ref bool searched)
    {
        var next = Cars.PeekNextRouteLane(car);

        // A queued lane the road does not join to the one under the car is a route from before a recovery
        // moved this car off it. The whole of it is stale, so the whole of it goes: taken one lane at a
        // time it ends the line at every one of them, a lane a tick, until the queue drains.
        if (next >= 0 && _roads.ConnectorBetween(fromLane, next) == RoadGraph.NoConnector)
        {
            Cars.ClearRoute(car);
            next = CarFleet.NoLane;
        }

        if (next >= 0) return Cars.TakeNextRouteLane(car);

        // The route has run out at a car park's frontage this leg turns at (GEN-4l): what is past the end
        // of this lane is a bay and not a lane, so the queue stops here — whether or not there is a bay
        // free to turn in yet, which is asked again every time the line is laid.
        if (TurnsBackHere(car, fromLane)) return CarFleet.NoLane;

        // The lane the leg's own bay is worked off is where the road runs out: the line stops there for the
        // manoeuvre into that bay, so it is not grown past it and the route is not asked for again.
        if (TheBayTheLineStopsFor(car, fromLane) != CarFleet.NoBay) return CarFleet.NoLane;

        if (searched) return LaneTour.NextLane(_roads, _config, fromLane, _closedLanes, ref Cars.Draw[car]);

        searched = true;
        PlanRoute(car, fromLane);
        next = Cars.TakeNextRouteLane(car);
        if (next >= 0) return next;
        if (TurnsBackHere(car, fromLane)) return CarFleet.NoLane;

        return TheBayTheLineStopsFor(car, fromLane) != CarFleet.NoBay
            ? CarFleet.NoLane
            : LaneTour.NextLane(_roads, _config, fromLane, _closedLanes, ref Cars.Draw[car]);
    }

    /// <summary>
    /// Whether the leg comes back the other way from the end of <em>this</em> lane and has a bay to do it
    /// in (GEN-4l) — the bay claimed here, where one is free.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the stretch that is asked and not a flag</b>: the lane the leg comes back down is the
    /// reverse of the one it turns off, so a car already round the turn answers no to the same question
    /// with the same field still set, and nothing has to remember to clear it.
    /// </para>
    /// <para>
    /// <b>A frontage with no bay free answers no, and the car drives on.</b> The alternative — stopping at
    /// the car park to wait for one — is a body standing in a lane, which is an obstruction the whole
    /// street queues behind and, on a street whose bays are freed by the cars in that queue, a jam that
    /// cannot clear. Driving on is what a driver does at a full car park: the route is asked for again from
    /// wherever this one gets to.
    /// </para>
    /// <para>
    /// <b>A stretch with no way out of it at all is the exception</b>, because driving on is what it does
    /// not offer: the queue ends there whether or not a bay was found. Nothing turns the car round — it
    /// stands at the end of the stretch until the leg's own clock gives the leg up (CAR-15a).
    /// </para>
    /// </remarks>
    bool TurnsBackHere(int car, int fromLane) =>
        Cars.TurnsBackOn[car] >= 0
        && _roads.LaneReverse[fromLane] == Cars.TurnsBackOn[car]
        && (TakeABayToTurnIn(car, fromLane, Cars.TurnsBackOn[car]) || _roads.LanesFrom(fromLane).Length == 0);

    /// <summary>
    /// A route from the far end of <paramref name="fromLane"/> to where the car is going, expanded into
    /// the lanes the line will be laid over. A car standing at its own destination draws another first.
    /// </summary>
    /// <remarks>
    /// Arriving is a route with nothing left in it, and not a radius: a car park stands off the kerb, so
    /// the distance from a lane's end to a destination is metres even when the car could not be nearer. A
    /// route the search could not find at all is a different answer and is not counted as an arrival.
    /// </remarks>
    void PlanRoute(int car, int fromLane)
    {
        Cars.ClearRoute(car);

        // <b>The turn goes with the route that asked for it</b> (GEN-4l), bay and all: a search run again is
        // a search that may come back another way round, and a bay held for a turn nothing is making is a
        // bay taken out of the town. The route about to be laid claims it again where it still turns there,
        // in this same call, so nobody else can be handed it in between.
        GiveUpTheTurn(car);
        if (fromLane < 0 || !Cars.HasDestination[car]) return;

        // A car has no goals of its own, so a leg with no bay left to it does not draw a destination — it
        // claims another bay near where the car has got to, and one that can claim none drives on rather
        // than standing in a lane. <b>An errand's own leg is the one that is not aimed at a bay at all</b>
        // (AMB-5, EVA-3): it is aimed at a body or a wreck in the road, and a bay claimed for it would be a
        // leg that parked instead of arriving.
        if (!IsAimedAtAPlaceInTheRoad(car)
            && BayAimedAt(car) < 0
            && !RetargetTheBay(car, Cars.PositionM[car], ParkingRegistry.NoBay))
        {
            return;
        }

        // A route with no lanes left is a car already on the lane its bay is entered from, so the line
        // stops at the staging place and the plan's next step takes over there.
        if (TryPlan(car, fromLane) == RouteFound.Arrived) RouteArrivals++;
    }

    enum RouteFound
    {
        /// <summary>Lanes were laid into the queue.</summary>
        Route,

        /// <summary>A route exists and has nothing left of it: the car is where it was going.</summary>
        Arrived,

        /// <summary>The search found nothing — a lane it cannot leave, a hole in the network. The tour carries the car.</summary>
        Nowhere,
    }

    /// <summary>
    /// Where this leg is aimed, as the places on the network a search may finish at, and the point they
    /// stand for. <b>One question and one answer</b>: the drive and the interface both plan to where the
    /// car is going (CTL-1a), and two readings of that would be two routes.
    /// </summary>
    /// <remarks>
    /// Where the leg ends is the metre of each lane of its street the bay's mouth stands abeam of
    /// (<see cref="BayGoals"/>), and never the nearest lane to the bay: the car manoeuvres in off whichever
    /// lane it gets there on, which may be the one on the other side of the road. <b>An errand's leg ends on a lane instead</b> (AMB-5, EVA-3) — beside a
    /// body or a wreck rather than inside a bay — and both directions of the stretch it stands on are
    /// offered, because only the search can say which of them reaches it first.
    /// </remarks>
    int RouteGoalsFor(int car, Span<RouteGoal> into)
    {
        // A police car sent to close a lane has to arrive on that lane (SRV-9): the other side of its street is
        // the mouth of a lane it is not closing.
        if (TheEntranceItIsSentTo(car, out var entrance, out var alongM)) return _driving.GoalOnLane(entrance, alongM, into);

        if (!IsAimedAtAPlaceInTheRoad(car)) return BayGoals(BayAimedAt(car), into);

        return _driving.GoalsAt(Cars.DestinationM[car], into);
    }

    RouteFound TryPlan(int car, int fromLane)
    {
        var driving = Driving;

        var goalCount = RouteGoalsFor(car, _driveSearch.Goals);
        if (goalCount == 0) return RouteFound.Nowhere;

        _driveSearch.Entries[0] = driving.EntryOnLane(fromLane, AlongTheEntryM(car, fromLane));
        if (_driveSearch.Entries[0].Link == TravelGraph.NoLink) return RouteFound.Nowhere;

        // A place on a lane is arrived at and not got near, so a goal the car has driven past is searched
        // for rather than counted as reached: the route round the block is what a driver who has overshot
        // the turn-in actually does.
        var linkCount = SearchTheDrivingNetwork(goalCount, ClosedLinksFor(car), out var goalSlot);
        if (linkCount == 0 || goalSlot < 0) return RouteFound.Nowhere;

        ExpandRoute(car, fromLane, _driveSearch.Links(linkCount), _driveSearch.Goals[goalSlot]);

        // A route with nothing left in it is an arrival; one that stops at a frontage to turn (GEN-4l) is
        // a leg with a turn in a bay still in front of it, whether or not it has a lane left to drive first.
        return Cars.RouteCount[car] > 0 || Cars.TurnsBackOn[car] >= 0 ? RouteFound.Route : RouteFound.Arrived;
    }

    /// <summary>
    /// <b>How far into the lane the search sets off from the body has got</b>: where the car actually
    /// stands when that lane is the one under it, and the far end of it when it is a lane further down the
    /// line being extended, which the car is going to drive the whole of.
    /// </summary>
    /// <remarks>
    /// <b>A lane is the whole stretch between two junctions</b>, so the two are hundreds of metres apart on
    /// an open street. Entered at the far end regardless, every destination between the car and that end —
    /// a bay's turn-in, a wreck, a body in the road — reads as a place already driven past
    /// (<see cref="RoutePlanner"/>), and the leg is sent round the block to reach ground it is already
    /// rolling towards. It is the one figure that says whether a goal is ahead.
    /// </remarks>
    float AlongTheEntryM(int car, int fromLane)
    {
        if (Cars.Line[car].LaneCount == 0 || Cars.ChainOf(car)[0] != fromLane) return _roads.LaneLengthM[fromLane];

        return Math.Clamp(Cars.ProgressM[car], 0f, _roads.LaneLengthM[fromLane]);
    }

    /// <summary>
    /// <b>The one place the driving network is searched</b>, so that what a leg spends on finding its way
    /// is counted where it is spent rather than estimated from the outside. Every entry is the car's own
    /// (<see cref="RouteSearch.Entries"/>), because a body under way joins the network by the link it is
    /// already committed to. <b>The closed runs are what it may not enter</b> (SRV-10), handed in by the asker:
    /// every one of them, or none for a car carrying a call (<see cref="ClosedLinksFor"/>).
    /// </summary>
    int SearchTheDrivingNetwork(int goalCount, ReadOnlySpan<bool> closed, out int goalSlot)
    {
        RouteSearches++;
        return _driveSearch.Plan(1, goalCount, _surcharges, closed, out goalSlot);
    }

    /// <summary>The lanes a search's links are driven as, laid into this car's own queue.</summary>
    void ExpandRoute(int car, int fromLane, ReadOnlySpan<int> links, RouteGoal goal)
    {
        Cars.RouteCount[car] = LayRouteLanes(
            fromLane, links, goal, Cars.RouteOf(car), out var turnsBackOn, out var ranOut);
        Cars.RouteTaken[car] = 0;
        Cars.TurnsBackOn[car] = turnsBackOn;
        Cars.RouteRunsOut[car] = ranOut;
    }

    /// <summary>
    /// The run-links a search returned, as the lanes a line is laid over: the lanes of the first link past
    /// the one the car is on, then whole links, then the lanes of the last one up to the place the
    /// destination stands. <b>Written wherever the caller keeps it</b> — the car's own bounded queue while
    /// it is driving, and a longer buffer where the interface is drawing the whole of a route (CTL-1a).
    /// </summary>
    /// <remarks>
    /// <b>The queue holds only lanes the road joins</b>, and there is one pair it can be asked for that the
    /// road does not: the two sides of a car park's frontage, where the search has come back the way the
    /// leg went (GEN-4l). The queue stops at the lane the car turns off, and what is past it is the bay —
    /// its own ways, and never a lane a line could be laid over.
    /// </remarks>
    /// <param name="ranOut">
    /// Whether <paramref name="into"/> filled with route still to come, which is the difference between a
    /// route that ends and one that stops.
    /// </param>
    int LayRouteLanes(
        int fromLane, ReadOnlySpan<int> links, RouteGoal goal, Span<int> into, out int turnsBackOn,
        out bool ranOut)
    {
        var driving = Driving;

        // The first link is the one the car is already on, and the lanes behind it are spent. Where it is
        // not, the network says where the movement onto it lands.
        var firstSlot = links.Length > 0 && driving.LinkOfLane(fromLane) == links[0]
            ? driving.SlotOfLane(fromLane) + 1
            : -1;

        var joins = new RoadJoins(_roads);
        var written = RouteChain.LayInto(ref joins, driving.Runs, links, fromLane, firstSlot, goal, into, out ranOut);
        turnsBackOn = joins.TurnsBackOn;
        return written;
    }

    /// <summary>
    /// How the carriageway answers <see cref="IRouteJoins"/>: <b>two lanes are travelled one after the
    /// other where a connector runs between them</b>, and there is nothing between them of the route's own
    /// — a junction's ground is assembled into the line (<see cref="LineAssembler"/>) rather than held on
    /// the network the way a pavement's corner is.
    /// </summary>
    /// <remarks>
    /// <b>The one pair the search can return that the road does not join</b> is the two sides of a car
    /// park's frontage, where the leg comes back the way it went (GEN-4l). The chain stops at the lane the
    /// car turns off and <see cref="TurnsBackOn"/> carries which lane that was, the rest being a bay's own
    /// ways and never a lane a line could be laid over.
    /// </remarks>
    struct RoadJoins(RoadGraph roads) : IRouteJoins
    {
        public int TurnsBackOn { get; private set; } = CarFleet.NoLane;

        /// <summary>Its first lane at all but a merge — there, the lanes before the one the merge joins are not driven.</summary>
        public int JoinedAt(ReadOnlySpan<int> pieces, int from)
        {
            for (var slot = 0; slot < pieces.Length; slot++)
            {
                if (roads.ConnectorBetween(from, pieces[slot]) != RoadGraph.NoConnector) return slot;
            }

            return 0;
        }

        public bool Reaches(int from, int onto)
        {
            if (roads.ConnectorBetween(from, onto) != RoadGraph.NoConnector) return true;

            TurnsBackOn = roads.LaneReverse[from] == onto ? onto : CarFleet.NoLane;
            return false;
        }

        public readonly int Between(int from, int onto) => RouteChain.NoWay;
    }

    /// <summary>
    /// How far this car has to be able to see: <b>its own stopping distance from its own top speed</b>
    /// (CAR-11), so a car that will do more than the nominal one looks further before it commits to a
    /// line and a slow one is not made to plan road it will never reach.
    /// </summary>
    float SightM(int car) => Cars.BuildOf(car).SightM;

    /// <summary>
    /// The line drawn over as many lanes as it takes to see a car's own stopping distance ahead of it,
    /// drawing new ones onto the end of the chain from <paramref name="from"/>.
    /// </summary>
    /// <remarks>
    /// A line shorter than that is a car braking for the end of its own knowledge, which reads as timidity
    /// and is a missing lane. The bound is the assembler's, not a figure behaviour holds.
    /// </remarks>
    /// <param name="spentM">
    /// How much of the chain already in hand is behind the car, so that what is drawn is a sight distance
    /// <em>ahead of the body</em> rather than ahead of the line's own origin.
    /// </param>
    void LayLine(int car, int from, float spentM = 0f)
    {
        var chain = Cars.ChainOf(car);
        var lanes = from;
        var reachM = SightM(car);
        var seenM = -spentM;
        var searched = false;
        for (var index = 0; index < from; index++) seenM += _roads.LaneLengthM[chain[index]];

        // <b>The route is asked at the end of the line already in hand, whether or not another lane is
        // wanted.</b> A lane is the whole stretch between two junctions, so the one under a car regularly
        // covers that car's own sight distance on its own — and everything the asking settles would then be
        // settled for no car on an open street: no queue to drive, no bay claimed to turn in (GEN-4l), and
        // no way of knowing the street it set off down runs out ahead, which is a car driving to the head of
        // a dead end and standing there for the rest of the run. <b>A lane it hands back is taken</b>: the
        // queue has already given that lane up, and a line one lane past the sight distance is line to
        // spare rather than line to waste.
        while (lanes < LineAssembler.MostLanes)
        {
            var next = NextLaneOnRoute(car, chain[lanes - 1], ref searched);
            if (next < 0) break;

            chain[lanes++] = next;
            seenM += _roads.LaneLengthM[next];
            if (seenM >= reachM) break;
        }

        // A route is driven forwards, whatever the last line this car was given was: the gear belongs to the
        // line and not to the car.
        Cars.LineIsReverse[car] = false;

        // <b>A line whose last lane is the one the car's own bay is worked off stops where the car waits for
        // its manoeuvre into it</b> (GEN-4f, <see cref="StopForTheBayM"/>), and the manoeuvre is what the car
        // drives next once it has the ground for it (<see cref="Park"/>). It is not threaded onto the
        // end of this line: a manoeuvre is shaped from where the car is when it gets there, in whichever gear
        // each of its pieces is driven.
        var bay = TheBayTheLineStopsFor(car, chain[lanes - 1]);
        Cars.StopsForBay[car] = bay;
        Cars.Line[car] = LineAssembler.Assemble(
            _roads, chain[..lanes], Cars.LineArcsOf(car), Cars.LaneStartsOf(car), Cars.LaneEndsOf(car),
            bay == CarFleet.NoBay ? float.PositiveInfinity : _parkingIn.StopForTheBayM(car, bay, chain[lanes - 1]));

        // What a plan reads of the line it runs down (TER-4c.1, S-2): the corners folded into each arc, and which
        // joins break it — once a line, so the plan and the profile read segments rather than walk geometry.
        CornerLimits.Lay(Cars.LineOf(car), Cars.LineEntriesOf(car), _config);
        var breaks = Cars.JoinBreaksOf(car);
        for (var slot = 0; slot + 1 < Cars.Line[car].LaneCount; slot++)
        {
            var join = _roads.ConnectorBetween(chain[slot], chain[slot + 1]);
            breaks[slot] = join != RoadGraph.NoConnector && _roads.BreaksTheLine(join);
        }
    }
}
