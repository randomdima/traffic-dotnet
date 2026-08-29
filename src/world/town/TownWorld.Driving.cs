using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>The standing rules (§1.7)</b>: how a car is driven at all, underneath whichever entry of the
/// catalogue is in charge. They are not manoeuvres and are never selected — holding the driven line at
/// the rear axle, taking speed as the minimum of every constraint with a reaction lead, watching ahead
/// along the line actually being driven, claiming the junction ahead and releasing the one behind, and
/// holding a stop the car has already made.
/// </summary>
/// <remarks>
/// The order is the argument. Progress is read before anything else because every other question is
/// asked <em>from</em> it; the line is re-laid the moment the car reaches the lane after the junction,
/// which is a fact about the line and not something to wait a decision for; and the tyres are last,
/// because what they are given is a command and never a position.
/// <para>
/// <b>Which procedure runs is the line's question and never the entry's name.</b> A reactive entry that
/// lays no line of its own leaves the car driving whatever it was already on, and a dispatch keyed on
/// the name would hand that car to the route procedure with no lanes under its line.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>One tick of one car's body: where it is, what it can see, and what the pedals and the wheel are asked for.</summary>
    void TickCar(int car)
    {
        // EVA-5: <b>a car on somebody's arm is not driving</b>. One end of it is off the ground and the other
        // is rolling where the truck takes it, which is exactly a wreck's state said of a car nobody broke —
        // so what it gets is the trailer's two wheels and no manoeuvre at all.
        if (_recovery.OnTheHookOf[car] >= 0)
        {
            TrailerWheels(car);
            return;
        }

        var pose = PoseOf(car);
        if (HandAtTheWheel(car))
        {
            // Under a hand no manoeuvre is selected and no soft rule is consulted (S-7).
            LeaveTheCatalogue(car);
            HandDrive(car, pose);
            return;
        }

        if (!Cars.Driven[car])
        {
            Hold(car, pose, DrivingHold.None);
            return;
        }

        Cars.InManeuverS[car] += _config.TickSeconds;
        Cars.SinceDecisionS[car] += _config.TickSeconds;
        _trace.Ticked(Cars.Doing[car], IsClocked(car));

        // <b>A car with a driver in it and no line is still in an entry of the catalogue</b>, and its
        // clocks still run: `E-9` and `E-10` end a leg from exactly this state, and a recovery that could
        // not find a lane is left in it until the ladder gets there. It has nothing to drive, so the body
        // holds — but it is decided about like any other car.
        if (Cars.Line[car].ArcCount == 0) Hold(car, pose, DrivingHold.None);
        else if (Cars.Line[car].LaneCount > 0) DriveTheRoute(car, pose);
        else if (Cars.LineWayOf(car) != CarFleet.NoWay) DriveTheWay(car, pose);
        else DriveTheTemplate(car, pose);
    }

    /// <summary>
    /// A car on the route's own line: the lanes under it, the junction ahead of it, the paint across it,
    /// and what the book says is down it.
    /// </summary>
    void DriveTheRoute(int car, in CarPose pose)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var forward = pose.Forward;
        var alongMps = Vector2.Dot(pose.VelocityMps, forward);
        var rearAxleM = CarFollower.RearAxleM(build, pose.PositionM, forward);
        var progressM = CarFollower.ProgressM(build, Cars.LineOf(car), rearAxleM, Cars.ProgressM[car]);
        var coveredM = MathF.Abs(progressM - Cars.ProgressM[car]);
        if (progressM >= Cars.LaneStartsOf(car)[1] && Cars.Line[car].LaneCount > 1)
        {
            progressM = AdvanceLane(car, rearAxleM, progressM);
        }

        Cars.ProgressM[car] = progressM;
        Cars.AlongMps[car] = alongMps;
        Cars.GroundCoefficient[car] = _terrain.At(pose.PositionM).Coefficient;
        Cars.OffLineM[car] = CarFollower.OffLineM(Cars.LineOf(car), rearAxleM, progressM);

        // <b>Being off the line is ordinary; being off it by this much is not</b> (CAR-10a). A line is a
        // recommendation and every car holds it with its own steering, so a long car cuts a corner a short
        // one takes cleanly and neither is corrected — what is watched for is the car that is no longer
        // driving the line at all.
        //
        // A car that has lost its own line stops. Taking the lane it is actually standing on is the cheap
        // half of the recovery and needs no manoeuvre; everything past that is `E-8`, which the watchdog
        // reaches by the ladder because a car standing off its line spends the blocked clock.
        if (Cars.OffLineM[car] > OffTheLineAllowanceM(car))
        {
            DropTheMovement(car);
            Cars.InsideTheBox[car] = false;
            Cars.LightAheadM[car] = float.PositiveInfinity;
            Cars.ToTheBoxM[car] = float.PositiveInfinity;
            Cars.BoxIsOurs[car] = false;
            Hold(car, pose, DrivingHold.LostLine);
            if (MathF.Abs(alongMps) <= _config.Driving.StopSpeedMps) Reacquire(car, rearAxleM);
            return;
        }

        // A line laid a sight distance ahead runs out mid-lane on a town whose runs are kilometres long,
        // and a car that brakes for the end of its own knowledge reads as timidity. The chain is grown
        // from its far end, so nothing already laid moves and the car's progress is untouched.
        if (Cars.Line[car].LengthM - progressM < SightM(car)
            && Cars.Line[car].LaneCount < PathAssembler.MostLanes
            && !IsOnTheFinalApproach(car)
            && _roads.TurnsFrom(Cars.ChainOf(car)[Cars.Line[car].LaneCount - 1]).Length > 0)
        {
            LayLine(car, Cars.Line[car].LaneCount, progressM);
        }

        var line = Cars.LineOf(car);
        var centreProgressM = progressM + build.CentreAheadOfAxleM;

        // <b>A driver looks as far as it needs to stop, which is a reaction interval and the stop itself</b>
        // — and it is the rate the profile actually brakes at, against what the tyres can put down, not what
        // the pedal is allowed to ask for. Sized to the pedal it was a third short of the stop it was for,
        // so a body standing on an open road at the gear's own cap came into view too late to be stopped
        // for. It is the same figure the line is grown to (<see cref="SimConfig.CarSightM"/>), for the same reason.
        var reachM = MathF.Min(
            (alongMps * _config.CarReactionS)
            + (alongMps * alongMps / (2f * CarFollower.BrakingMps2(_config, build, Cars.GroundCoefficient[car])))
            + (build.LengthM * 2f),
            MathF.Max(0f, Cars.Line[car].LengthM - centreProgressM));

        // S-3: what is in front, what it is and how far off — one walk of the book the grant was taken
        // against, so the reading and the road this car was given can never disagree.
        var seen = LookAhead(car, progressM + build.NoseAheadOfAxleM, reachM, out var kind, out var claimM);

        // S-4: the junction ahead is claimed and the one behind released, on every tick and never on the
        // decision clock — a red is what actually refuses a car a box, and it can change under one.
        var junctionStopM = JunctionStopM(car, progressM, alongMps, seen.DistanceM, out var toTheBoxM, out var claimed);
        Cars.ToTheBoxM[car] = toTheBoxM;
        Cars.BoxIsOurs[car] = claimed;

        // Ground somebody else has claimed is a place to be stopped short of and not a body to keep a gap
        // behind: it is empty now, which is exactly why a reading taken off the bodies lets two cars take
        // it at once. It joins the stop point rather than the headway for that reason.
        if (claimM < junctionStopM)
        {
            junctionStopM = claimM;
            if (kind == HeadwayKind.Nothing || claimM < seen.DistanceM) kind = HeadwayKind.Claimed;
        }

        // The paint, asked after the junction because a crossing is the stop line for the junction
        // behind it — a car held by the box stops short of the paint rather than a dozen metres past it,
        // which is a stop taken *on* the crossing.
        CrossingAhead(
            car, LaneAheadSlot(car, progressM), progressM, MathF.Min(junctionStopM, seen.DistanceM),
            out var crossingStopM, out var crossingAtM, out var crossingPaceMps);

        // The grant was taken against the book while it was being laid, so it is a distance from where the
        // nose stood then: walking it in by the ground covered since is what stops it receding at exactly
        // the car's own speed, which is the same correction a manoeuvre's stop point gets.
        var context = new DriveContext(
            seen.DistanceM, seen.AlongMps, junctionStopM, Cars.GroundCoefficient[car],
            crossingStopM, crossingAtM, crossingPaceMps, kind, Cars.AuthorityM[car] - coveredM,
            Cars.GrantCutBy[car]);

        Cars.Context[car] = context;
        Drive(
            car, build, pose, line, progressM, Cars.Line[car].LengthM, context, forward, alongMps, coveredM,
            reverse: false);
    }

    /// <summary>
    /// <b>A car whose line is one of the town's own ways</b> — a bay's way out, driven backwards. The same
    /// wheel and the same profile as a route, and the same book underneath: what is in front comes off the
    /// index, the road ahead is the grant, and the movement is taken and given back exactly as a junction's
    /// is.
    /// </summary>
    /// <remarks>
    /// <b>It is not a template and the difference is the whole point.</b> A template is laid over no way, so
    /// its driver holds the sweep it is committed to and reads the ground under it with a walk of its own;
    /// a way is in the book, so the reservation runs along it, the traffic on the lane it crosses is cut by
    /// the town's own table, and there is nothing here that a car on a lane does not also do.
    /// </remarks>
    void DriveTheWay(int car, in CarPose pose)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var reverse = Cars.LineIsReverse[car];
        var forward = pose.Forward;
        var travel = reverse ? -forward : forward;
        var rearAxleM = CarFollower.RearAxleM(build, pose.PositionM, forward);
        var line = Cars.LineOf(car);
        var lengthM = Cars.Line[car].LengthM;
        var progressM = CarFollower.ProgressM(build, line, rearAxleM, Cars.ProgressM[car]);
        var alongMps = Vector2.Dot(pose.VelocityMps, travel);
        var coveredM = MathF.Abs(progressM - Cars.ProgressM[car]);

        Cars.ProgressM[car] = progressM;
        Cars.AlongMps[car] = alongMps;
        Cars.OffLineM[car] = CarFollower.OffLineM(line, rearAxleM, progressM);
        Cars.GroundCoefficient[car] = _terrain.At(pose.PositionM).Coefficient;

        var leadM = progressM + LeadingEdgeAheadOfTheAxleM(car);
        var reachM = MathF.Max(0f, lengthM - leadM);
        var seen = LookAhead(car, leadM, reachM, out var kind, out var claimM);

        // The movement is taken and given back on the same argument a junction's is (S-4): the crossings on
        // the car's own way are held from the moment it commits to them, and the car stops short of the
        // first of them while anything else has the ground.
        var stopAtM = MovementStopM(car, progressM, alongMps);
        if (claimM < stopAtM)
        {
            stopAtM = claimM;
            if (kind == HeadwayKind.Nothing || claimM < seen.DistanceM) kind = HeadwayKind.Claimed;
        }

        // A car backing out crosses the same paint anything else does.
        CrossingOnTheTemplate(car, line, leadM, reachM, out var crossingStopM, out var crossingAtM);

        var context = new DriveContext(
            seen.DistanceM, seen.AlongMps, stopAtM, Cars.GroundCoefficient[car], crossingStopM, crossingAtM,
            float.IsPositiveInfinity(crossingAtM) ? float.PositiveInfinity : build.CrossingPaceMps, kind,
            Cars.AuthorityM[car] - coveredM, Cars.GrantCutBy[car]);

        Cars.Context[car] = context;
        Drive(car, build, pose, line, progressM, lengthM, context, travel, alongMps, coveredM, reverse);
    }

    /// <summary>
    /// <b>Where a car on a way of its own is stopped short</b>, and where it commits: the first place another
    /// way is driven over this one is its box, and the whole of the way past that point is what it takes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is `JunctionStopM`'s argument on a way that is not a join</b>, and it is here rather than there
    /// because a bay's way out enters no junction and has no lane ahead of it to be measured against. What
    /// the two share is the protocol: read the table, take the ground before moving onto it, hold nothing
    /// before the car is near enough to want it, and be refused at the place the ground is somebody's rather
    /// than at the first crossing on the way — as far up as the body can be brought to rest without standing
    /// on one (<see cref="WaitsClearOfTheCrossings"/>).
    /// </para>
    /// <para>
    /// <b>Past the point it could stop at, the car is going in whatever anything says</b> — the same
    /// exception a junction makes, and for the same reason: ground given back there is handed straight back
    /// on the next tick, and between the two the sections read free to whoever crosses them.
    /// </para>
    /// </remarks>
    float MovementStopM(int car, float progressM, float alongMps)
    {
        var way = Cars.LineWayOf(car);
        if (Cars.MovementWay[car] != way) DropTheMovement(car);

        var crossedAtM = FirstCrossedOnTheWayM(way);
        if (float.IsPositiveInfinity(crossedAtM)) return float.PositiveInfinity;

        var toTheCrossingM = crossedAtM - progressM - LeadingEdgeAheadOfTheAxleM(car);
        if (Cars.MovementWay[car] == way) return float.PositiveInfinity;

        ref readonly var build = ref Cars.BuildOf(car);
        var brakingMps2 = CarFollower.BrakingMps2(_config, build, Cars.GroundCoefficient[car]);
        if (toTheCrossingM <= StoppingM(alongMps, brakingMps2)) return float.PositiveInfinity;

        var reserveAtM = MathF.Min(
            StoppingM(alongMps, brakingMps2) + build.LengthM, _config.CarJunctionReserveM);

        if (toTheCrossingM > reserveAtM) return float.PositiveInfinity;

        var heldFromM = FirstHeldOnTheMovementM(car, way);
        if (float.IsFinite(heldFromM))
        {
            var restM = heldFromM - build.BodyMarginM;
            return WaitsClearOfTheCrossings(car, way, restM)
                ? restM - progressM - LeadingEdgeAheadOfTheAxleM(car)
                : toTheCrossingM - build.HalfLengthM;
        }

        TakeTheMovement(car, way);
        return float.PositiveInfinity;
    }

    /// <summary>The first metre of a way that any other way of the town is driven over it at, or infinity.</summary>
    float FirstCrossedOnTheWayM(int way)
    {
        var leastM = float.PositiveInfinity;
        foreach (ref readonly var run in _crossings.OwnRuns(way)) leastM = MathF.Min(leastM, run.FromM);

        return leastM;
    }

    /// <summary>
    /// A car driving a template rather than a route: the same wheel and the same profile, on a line of a
    /// few arcs, in whichever gear the template is driven in.
    /// </summary>
    /// <remarks>
    /// Reversing is the same pure pursuit against the direction the rear axle is travelling, with the
    /// steering negated: a car going backwards is a car whose steered wheels are behind it. Tracking the
    /// rear axle backwards is only stable over a short line — a dozen metres at manoeuvring pace — which
    /// is why nothing else here is driven this way.
    /// </remarks>
    void DriveTheTemplate(int car, in CarPose pose)
    {
        ref readonly var build = ref Cars.BuildOf(car);
        var reverse = Cars.LineIsReverse[car];
        var forward = pose.Forward;
        var travel = reverse ? -forward : forward;
        var rearAxleM = CarFollower.RearAxleM(build, pose.PositionM, forward);
        var line = Cars.LineOf(car);
        var progressM = CarFollower.ProgressM(build, line, rearAxleM, Cars.ProgressM[car]);
        var lengthM = Cars.Line[car].LengthM;
        var alongMps = Vector2.Dot(pose.VelocityMps, travel);
        var coveredM = MathF.Abs(progressM - Cars.ProgressM[car]);

        Cars.ProgressM[car] = progressM;
        Cars.AlongMps[car] = alongMps;
        Cars.OffLineM[car] = CarFollower.OffLineM(line, rearAxleM, progressM);
        Cars.GroundCoefficient[car] = _terrain.At(pose.PositionM).Coefficient;

        // A template enters no junction of its own: what it has to see is what lies along the line it is
        // driving, in the gear it is driving it — from the end of its own body that leads in that gear.
        //
        // <b>And all of it, to the end of the line.</b> What a body on a template holds is the whole sweep it
        // is committed to (<see cref="WhereTheTemplateSweepEndsM"/>), so anything shorter is a driver
        // checking less ground than it is taking — and the two body lengths this was bounded to are less
        // than the sweep on every template the catalogue lays.
        var tailM = progressM + (reverse ? build.TailBehindAxleM : build.CentreAheadOfAxleM);
        var reachM = MathF.Max(0f, lengthM - tailM);

        // <b>The ground under the shape and not a ray down it</b> (<see cref="GroundAhead"/>). A template is
        // laid over no way, so what the book is asked is who has the ground each place along it would put a
        // body — which is the same question the desk asked before it committed to this line at all.
        var clearM = GroundAhead.ClearM(_roads, _occupancy, line, tailM, reachM, build.FlankM, car);

        // The paint is owed by a car under its own geometry as much as by one on its route (CAR-7b): a
        // swerve, a bay entry and a bay exit all cross the same crossings.
        CrossingOnTheTemplate(car, line, tailM, reachM, out var crossingStopM, out var crossingAtM);

        // <b>Unknown, still.</b> The ways under a template are not the ways it is driving, so what the book
        // named there is a fact about somebody else's lane rather than about this car's own path — and a
        // reading that cannot be trusted to name what is in the way must never license driving round it.
        var context = new DriveContext(
            clearM < reachM ? clearM : float.PositiveInfinity, 0f, float.PositiveInfinity,
            Cars.GroundCoefficient[car], crossingStopM, crossingAtM,
            float.IsPositiveInfinity(crossingAtM) ? float.PositiveInfinity : build.CrossingPaceMps,
            clearM < reachM ? HeadwayKind.Unknown : HeadwayKind.Nothing);

        Cars.Context[car] = context;
        Drive(car, build, pose, line, progressM, lengthM, context, travel, alongMps, coveredM, reverse);
    }

    /// <summary>
    /// S-1, S-2 and S-5, and the one place the entry in charge gets a say: <b>the wheel is pure pursuit,
    /// the pedals are the speed profile, and the manoeuvre's limits are folded into the minimum the
    /// profile already takes</b>. Nothing here decides what the car is doing — only how what it is doing
    /// is delivered by the tyres.
    /// </summary>
    void Drive(
        int car, in CarBuild build, in CarPose pose, ReadOnlySpan<ArcSeg> line, float progressM, float lengthM,
        in DriveContext context, Vector2 travel, float alongMps, float coveredM, bool reverse)
    {
        var rearAxleM = CarFollower.RearAxleM(build, pose.PositionM, pose.Forward);
        var lookaheadM = CarFollower.LookaheadM(build, MathF.Abs(alongMps), _config.Driving.LookaheadS);

        // Pure pursuit asks; the rack answers (CAR-3a). The angle carried is the one this side of the
        // gear, since a reverse command is the same wheel with its sign turned round on the way out.
        var wasRad = Cars.Command[car].Reverse ? -Cars.Command[car].SteerRad : Cars.Command[car].SteerRad;
        var steerRad = build.WheelWoundTo(
            wasRad, CarFollower.Steer(build, line, progressM, rearAxleM, travel, lookaheadM), _config.TickSeconds);
        var targetMps = CarFollower.TargetSpeedMps(
            _config, build, line, progressM, lengthM, steerRad, alongMps, lookaheadM, context, out var hold,
            out var plannedMps);

        // The ceiling on the next reservation. It is the profile's own answer with the grant left out, so a
        // car held at a standstill by the queue in front is not held to a standstill's worth of road.
        Cars.PlannedMps[car] = plannedMps;

        // A template is driven at manoeuvring pace whichever way round it is taken, and the reverse cap is
        // that pace — deliberately off the forward cap's scale, because this is its only use.
        if (reverse || (Cars.Line[car].LaneCount == 0 && ManeuverCatalogue.AtManeuveringPace(Cars.Doing[car])))
        {
            targetMps = MathF.Min(targetMps, build.ReverseMaxMps);
        }

        // AMB-4: <b>a blue light buys the road and never the tyres.</b> A rescue keeps every constraint the
        // profile already takes and loses three that hold every other car's speed down — the reds, the
        // bars and the wait at a kerb — so without a pace of its own it reaches the gear's cap on the
        // first straight it meets and arrives as a second casualty.
        if (Cars.BlueLight[car]) targetMps = MathF.Min(targetMps, _config.Ambulance.CallPaceMps);

        var limits = CarryTheStopPoint(car, coveredM);
        targetMps = UnderTheLimits(build, limits, targetMps, alongMps, context, ref hold);

        // Where the foot already was, so the pedal travels rather than snapping. It is along the direction
        // being driven on both sides of the gear, because the reverse command negates the wheel and nothing
        // else.
        var lastMps2 = CarFollower.PedalMps2(Cars.Command[car]);
        var pedals = CarFollower.Pedals(
            _config, build, steerRad, targetMps, alongMps, _config.TickSeconds, lastMps2);
        var command = reverse ? Reversed(pedals) : pedals;

        // `E-2` outranks everything (§1.5 row 1), so it is asked of the profile's own answer rather than
        // instead of it: what it overrides is the pedal, and nothing else. It is asked on every tick
        // whatever the decision clock says, because a hazard inside braking distance is not something to
        // discover at the end of a scheduling interval.
        if (E02EmergencyStop.IsAHazard(_config, build, alongMps, context))
        {

            command = command with { ThrottleMps2 = 0f, BrakeMps2 = build.BrakingMps2, Handbrake = false };
            if (Cars.Doing[car] != Maneuver.EmergencyStop) Interrupt(car, Maneuver.EmergencyStop);
        }
        else if (limits.SpendTheTyre)
        {
            command = command with { ThrottleMps2 = 0f, BrakeMps2 = build.BrakingMps2, Handbrake = false };
        }

        Cars.Command[car] = command;
        Cars.Hold[car] = hold;
        Tyres(car, pose);
    }

    /// <summary>The same command, driven backwards: the gear, and the wheel turned against the direction the axle travels.</summary>
    DriveCommand Reversed(DriveCommand command) =>
        command with { SteerRad = -command.SteerRad, Reverse = true };

    /// <summary>
    /// What the entry in charge is still asking for, with its stop point walked in by the ground covered
    /// since it was set. <b>A stop point is a distance from the axle</b>, so a car that keeps it unchanged
    /// while driving toward it is holding a point that recedes at exactly its own speed — a stop line it
    /// can never reach.
    /// </summary>
    DriveLimits CarryTheStopPoint(int car, float coveredM)
    {
        var limits = Cars.Limits[car].Carried(coveredM);
        Cars.Limits[car] = limits;
        return limits;
    }

    /// <summary>
    /// The manoeuvre's own terms, folded into the minimum. Speed is the least of everything that limits a
    /// car and a procedure is one of those things — the only one that is a <em>decision</em> rather than a
    /// reading, which is why it is named apart in the read-outs.
    /// </summary>
    float UnderTheLimits(
        in CarBuild build, in DriveLimits limits, float targetMps, float alongMps, in DriveContext context,
        ref DrivingHold hold)
    {
        if (limits.HoldStill)
        {
            hold = DrivingHold.Procedure;
            return 0f;
        }

        if (limits.CapMps < targetMps)
        {
            targetMps = limits.CapMps;
            hold = DrivingHold.Procedure;
        }

        if (!limits.HasStopPoint) return targetMps;

        var brakingMps2 = CarFollower.BrakingMps2(_config, build, context.GroundCoefficient);
        var leadM = MathF.Abs(alongMps) * CarFollower.LeadS(_config, build, brakingMps2);
        var stoppingMps = CarFollower.ApproachMps(0f, limits.StopWithinM - leadM, brakingMps2);
        if (stoppingMps >= targetMps) return targetMps;

        hold = DrivingHold.Procedure;
        return stoppingMps;
    }

    /// <summary>
    /// <b>What is down the line ahead, what it is, and how far off it is</b> — all three out of the town's
    /// own book, which is the whole of what a driver on a route looks at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>There is no ray here and that is the point.</b> A cast found a shape at a distance and could not
    /// say whose it was, so the distance was the geometry's and the naming was the book's, and the two
    /// regularly disagreed — a body the network never had came back as <c>Unknown</c>, and a reservation
    /// with nothing standing on it yet came back as an empty road. Everything that can be on a lane is in
    /// the book now: the traffic, the people (<see cref="LaneUse.OnFoot"/>) and the town's own furniture
    /// (<see cref="StandingGround"/>), so one question answers all of it.
    /// </para>
    /// <para>
    /// <b>And the reading cannot disagree with the grant any more.</b> Both are walks of the same ways over
    /// the same metres of the same tick's book — where a cast was a second opinion about a road the car had
    /// already been granted or refused.
    /// </para>
    /// </remarks>
    HeadwayReading LookAhead(int car, float noseM, float reachM, out HeadwayKind kind, out float claimM)
    {
        AheadOnThePath(car, noseM, reachM, out var onThePath, out var bodyM, out claimM);
        if (!onThePath.Found)
        {
            kind = HeadwayKind.Nothing;
            return HeadwayReading.Nothing;
        }

        kind = KindOf(onThePath.Use);
        return new HeadwayReading(bodyM, onThePath.AlongMps);
    }

    /// <summary>
    /// A stopped car that has lost its line takes the lane it is actually standing on and starts again —
    /// the part of the recovery that needs no manoeuvre, since a car on a lane's own ground within a lane's
    /// width of its line is a car whose line was simply the wrong one. Anywhere else it stays put and the
    /// ladder reaches it.
    /// </summary>
    /// <remarks>
    /// <b>The lane that runs the way the car is pointing, and never simply the nearest one.</b> A car set
    /// off down the oncoming lane is a head-on rather than a recovery — but refusing the nearest lane on
    /// that ground and stopping there leaves the one case this is most needed for with no answer at all: a
    /// body shoved across the centreline stands nearest the oncoming line, pointing the way it always was,
    /// and the lane it wants is that one's reverse. Refusing it left the car standing in the other stream
    /// on ground a car may drive on, so `E-8` had nothing to say either, and the ladder took it all the way
    /// down to giving the journey up.
    /// </remarks>
    void Reacquire(int car, Vector2 rearAxleM)
    {
        if (!_terrain.At(rearAxleM).Drivable) return;

        var lane = _roads.NearestLane(rearAxleM, out var alongM);
        if (lane < 0) return;

        var forward = ForwardOf(car);
        if (Vector2.Dot(Spline.SampleAt(_roads.ArcsOf(lane), alongM).Direction, forward) <= 0f)
        {
            lane = _roads.LaneReverse[lane];
            if (lane < 0) return;

            var arcs = _roads.ArcsOf(lane);
            alongM = Spline.ProjectM(arcs, rearAxleM, _roads.LaneLengthM[lane] * 0.5f, _roads.LaneLengthM[lane]);
        }

        var onto = Spline.SampleAt(_roads.ArcsOf(lane), alongM);
        if ((onto.PositionM - rearAxleM).Length() > _config.CarOffPathM * OffLineTolerance) return;
        if (Vector2.Dot(onto.Direction, forward) <= 0f) return;

        // <b>The same lane taken again is not a line to lay again.</b> A body that has come to rest off
        // its line is asked this every tick until something moves it, and laying the line means searching
        // the network for the route behind it — the same answer, from the same lane, for as long as the
        // car stands there. What has to keep up is where the body is on it, which is arithmetic.
        Cars.ProgressM[car] = alongM;
        if (Cars.ChainOf(car)[0] == lane && Cars.Line[car].LaneCount > 0) return;

        Cars.ChainOf(car)[0] = lane;
        LayLine(car, 1);
        Cars.ProgressM[car] = alongM;
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
    /// The car has reached the second lane of its chain: the way through the junction behind it is let
    /// go, the chain shifts down by one and a new lane is drawn onto the end of it, and the line is
    /// re-laid.
    /// </summary>
    float AdvanceLane(int car, Vector2 rearAxleM, float progressM)
    {
        var chain = Cars.ChainOf(car);
        DropTheMovement(car);

        // Where the new line's origin sits on the old one, taken before the old one is overwritten: the
        // projection is a search in a window around where the car last was, and seeding it with a progress
        // measured from the wrong origin hands the car a place a hundred metres up the road.
        var shiftM = Cars.LaneStartsOf(car)[1];
        var lanes = Cars.Line[car].LaneCount;
        for (var index = 1; index < lanes; index++) chain[index - 1] = chain[index];

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
        if (next >= 0 && _roads.TurnSlot(fromLane, next) == RoadGraph.NoTurn)
        {
            Cars.ClearRoute(car);
            next = CarFleet.NoLane;
        }

        if (next >= 0) return Cars.TakeNextRouteLane(car);

        // The route has run out at a car park's frontage this leg turns at (GEN-4l): what is past the end
        // of this lane is a bay and not a lane, so the queue stops here — whether or not there is a bay
        // free to turn in yet, which is asked again every time the line is laid.
        if (TurnsBackHere(car, fromLane)) return CarFleet.NoLane;

        // The lane the leg's own bay is reached from is where the road runs out: the line finishes on the
        // way into that bay, so it is not grown past it and the route is not asked for again.
        if (TheWayIntoTheBay(car, fromLane) != CarFleet.NoWay) return CarFleet.NoLane;

        if (searched) return LaneTour.NextLane(_roads, _config, fromLane, ref Cars.Draw[car]);

        searched = true;
        PlanRoute(car, fromLane);
        next = Cars.TakeNextRouteLane(car);
        if (next >= 0) return next;
        if (TurnsBackHere(car, fromLane)) return CarFleet.NoLane;

        return TheWayIntoTheBay(car, fromLane) != CarFleet.NoWay
            ? CarFleet.NoLane
            : LaneTour.NextLane(_roads, _config, fromLane, ref Cars.Draw[car]);
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
    /// not offer: the queue ends there whether or not a bay was found, and the car turns itself round on
    /// the spot (`P-19`) at the one place a town promises the room for it (TER-5a).
    /// </para>
    /// </remarks>
    bool TurnsBackHere(int car, int fromLane) =>
        Cars.TurnsBackOn[car] >= 0
        && _roads.LaneReverse[fromLane] == Cars.TurnsBackOn[car]
        && (TakeABayToTurnIn(car, fromLane, Cars.TurnsBackOn[car]) || _roads.TurnsFrom(fromLane).Length == 0);

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
    /// Where the leg ends is the place the bay's own template is staged from, and never the nearest lane
    /// to the bay: the bay is entered from the lane the arithmetic allows, which is regularly the one on
    /// the other side of the road. <b>An errand's leg ends on a lane instead</b> (AMB-5, EVA-3) — beside a
    /// body or a wreck rather than inside a bay — and both directions of the stretch it stands on are
    /// offered, because only the search can say which of them reaches it first.
    /// </remarks>
    int RouteGoalsFor(int car, Span<RouteGoal> into, out Vector2 goalPointM)
    {
        if (!IsAimedAtAPlaceInTheRoad(car)) return BayGoals(BayAimedAt(car), into, out goalPointM);

        goalPointM = Cars.DestinationM[car];
        return _driving.GoalsAt(goalPointM, into);
    }

    RouteFound TryPlan(int car, int fromLane)
    {
        var driving = Driving;

        var goalCount = RouteGoalsFor(car, _driveSearch.Goals, out var goalPointM);
        if (goalCount == 0) return RouteFound.Nowhere;

        _driveSearch.Entries[0] = driving.EntryOnLane(fromLane, _roads.LaneLengthM[fromLane]);
        if (_driveSearch.Entries[0].Link == TravelGraph.NoLink) return RouteFound.Nowhere;

        // A place on a lane is arrived at and not got near, so a goal the car has driven past is searched
        // for rather than counted as reached: the route round the block is what a driver who has overshot
        // the turn-in actually does.
        var linkCount = SearchTheDrivingNetwork(goalCount, goalPointM, out var goalSlot);
        if (linkCount == 0 || goalSlot < 0) return RouteFound.Nowhere;

        ExpandRoute(car, fromLane, _driveSearch.Links(linkCount), _driveSearch.Goals[goalSlot]);

        // A route with nothing left in it is an arrival; one that stops at a frontage to turn (GEN-4l) is
        // a leg with a manoeuvre still in front of it, whether or not it has a lane left to drive first.
        return Cars.RouteCount[car] > 0 || Cars.TurnsBackOn[car] >= 0 ? RouteFound.Route : RouteFound.Arrived;
    }

    /// <summary>
    /// <b>The one place the driving network is searched</b>, so that what a leg spends on finding its way
    /// is counted where it is spent rather than estimated from the outside. Every entry is the car's own
    /// (<see cref="RouteSearch.Entries"/>), because a body under way joins the network by the link it is
    /// already committed to.
    /// </summary>
    int SearchTheDrivingNetwork(int goalCount, Vector2 goalPointM, out int goalSlot)
    {
        RouteSearches++;
        return _driveSearch.Plan(1, goalCount, goalPointM, _surcharges, out goalSlot);
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
    /// a manoeuvre, and never a lane a line could be laid over.
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
        var runs = driving.Runs;
        var written = 0;
        var last = fromLane;
        var joined = true;
        turnsBackOn = CarFleet.NoLane;
        ranOut = false;

        for (var index = 0; index < links.Length && joined && !ranOut; index++)
        {
            var link = links[index];
            var lanes = runs.PiecesOf(link);

            // The first link is the one the car is already on, and the lanes behind it are spent.
            var from = index == 0 && driving.LinkOfLane(fromLane) == link ? driving.SlotOfLane(fromLane) + 1 : 0;

            // The last link is only travelled as far as the destination stands along it.
            var to = lanes.Length;
            if (index == links.Length - 1 && link == goal.Link) to = Math.Min(to, SlotAtM(runs, link, goal.AlongM) + 1);

            for (var slot = from; slot < to; slot++)
            {
                if (written == into.Length)
                {
                    ranOut = true;
                    break;
                }

                if (_roads.TurnSlot(last, lanes[slot]) == RoadGraph.NoTurn)
                {
                    turnsBackOn = _roads.LaneReverse[last] == lanes[slot] ? lanes[slot] : CarFleet.NoLane;
                    joined = false;
                    break;
                }

                last = lanes[slot];
                into[written++] = last;
            }
        }

        return written;
    }

    /// <summary>Which piece of a run a place along it stands on.</summary>
    static int SlotAtM(RunNetwork runs, int link, float alongM) => runs.PieceAt(link, alongM, out _);

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

        while (lanes < PathAssembler.MostLanes && seenM < reachM)
        {
            var next = NextLaneOnRoute(car, chain[lanes - 1], ref searched);
            if (next < 0) break;

            chain[lanes++] = next;
            seenM += _roads.LaneLengthM[next];
        }

        // A route is driven forwards and is a chain rather than a way, whatever the last line this car was
        // given was: both belong to the line and not to the car.
        Cars.LineIsReverse[car] = false;
        Cars.LineWay[car] = CarFleet.NoWay;

        // A line whose last lane is the one the car's own bay is reached from leaves that lane where the
        // bay's own way does and finishes on it — so the whole of a leg, down to the pose the car is left
        // in, is one chain over the town's own ways.
        //
        // <b>Unless that way is reversed into</b> (GEN-4j): a route is driven forwards, so a way whose
        // metres run against the car is not one to thread onto the end of it. The line stops where the way
        // begins, the car comes to rest there, and `P-14` lays the same shape from the pose it stopped in
        // and drives it in the gear it is drawn for.
        var tail = TheWayIntoTheBay(car, chain[lanes - 1]);
        var threaded = tail != CarFleet.NoWay && !_bayWays.IsDrivenInReverse(tail);
        Cars.TailWay[car] = tail;
        Cars.Line[car] = PathAssembler.Assemble(
            _roads, chain[..lanes], Cars.LineArcsOf(car), Cars.LaneStartsOf(car), Cars.LaneEndsOf(car),
            tail == CarFleet.NoWay ? float.PositiveInfinity : _bayWays.AtLaneM(tail),
            threaded ? _bayWays.ArcsOf(tail) : default);
    }
}
