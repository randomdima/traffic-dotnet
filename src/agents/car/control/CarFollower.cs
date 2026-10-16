using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Car.Control;

/// <summary>
/// <b>What ended a car's grant</b> — the reservation its plan was refused at, named for whoever reads it out.
/// </summary>
/// <remarks>
/// <b>It is a reading and never a decision</b>: the grant is a distance whatever cut it, and the car is driven
/// to stop within it (S-2a). The leg's own clock is what eventually gets a car out from behind a queue that
/// never moves (CAR-15), and it asks this whether the wait is a light's.
/// </remarks>
internal enum HeadwayKind : byte
{
    /// <summary>Nothing cut it.</summary>
    Nothing,

    /// <summary>
    /// <b>A body standing on the road</b> (TER-4c.2) — a car, a walker, a wreck: the claims say where and not whose
    /// (TER-4c.5).
    /// </summary>
    Body,

    /// <summary>
    /// <b>Ground another holder plans to use</b> (TER-4c.1) — a movement this car gives way to, a bay being
    /// backed out of, a closed road.
    /// </summary>
    Claimed,

    /// <summary>
    /// <b>A light's hold</b> (TLT-1): the stretch past a bar a light is not showing green at — a wait that
    /// spends no clock, since the light changes on its own (TLT-2a).
    /// </summary>
    Light,
}

/// <summary>
/// What the driver has been told about the world this tick, over and above the line it is driving: the road it was
/// granted. <b>Never the ground under it</b> — what a wheel stands on is its tyre's alone (TER-7b), and the driver plans on
/// its own car's rubber.
/// </summary>
/// <param name="AuthorityM">
/// How far ahead of the nose the reservations cut this car's own plan short, or
/// <see cref="float.PositiveInfinity"/> where nothing cut it. <b>It is the whole of following</b>: no two
/// grants overlap, so the car behind simply has less road to stop in, and drives at what that road affords.
/// </param>
/// <param name="GrantCutBy">What cut it — a reading for the words and the clock, and never a term of the speed.</param>
/// <param name="PlaceStopM">
/// How far ahead along the line the place this car was sent to stands — a casualty, a wreck, a scene, a
/// place a hand named. Infinite for every car that is not on its way to one.
/// </param>
/// <param name="MarginM">
/// The gap the grant was taken short of what cut it, so that <see cref="AuthorityM"/> and this together are
/// the section the car holds.
/// </param>
/// <param name="WaitsToPass">Whether what cut it is something this car means to get past (CAR-46).</param>
/// <param name="HorizonM">
/// How far ahead of the nose the car's own plan ends where it was held short of what the car wanted by how far a
/// plan may reach (TER-4c.1), or <see cref="float.PositiveInfinity"/> where it planned all it wanted. Nothing cut
/// it, so it is no part of the grant.
/// </param>
/// <param name="PassAsideM">
/// <b>The lane beside this car has decided to get past what cut it on</b> (CAR-46), as how far across it stands
/// along the driver's right, negative on its left — or zero where it has decided on no pass. <b>Decided and not
/// granted</b>: it is set from where the car would begin slowing for what it passes, while it waits for the lane
/// beside as when it has it, and it is what the indicator says until the pass is begun (CAR-14.7).
/// </param>
/// <param name="BackUpM">
/// <b>How far behind its tail this car means to back up</b> for the room to step out round what it has decided to
/// get past (CAR-50), or zero. Asked for at the car's next turn, which says how much of it was had
/// (<see cref="Body.CarFleet.BackRoomM"/>).
/// </param>
/// <param name="Blocked">
/// <b>It can neither get past what stands in front of it nor back up for the room to</b> (CAR-50): it stands where it
/// is, asking again on the pass's clock.
/// </param>
/// <param name="WaitAtM">
/// <b>How far ahead of the rear axle the car stands until it has the ground for what it does next</b> — where the step
/// out of the pass it waits for begins (CAR-46), or the first piece of its manoeuvre into a bay (GEN-4f) — or infinity
/// where it waits for nothing.
/// </param>
internal readonly record struct DriveContext(
    float AuthorityM, HeadwayKind GrantCutBy = HeadwayKind.Nothing,
    float PlaceStopM = float.PositiveInfinity, float MarginM = 0f, bool WaitsToPass = false,
    float HorizonM = float.PositiveInfinity, float PassAsideM = 0f, float BackUpM = 0f, bool Blocked = false,
    float WaitAtM = float.PositiveInfinity)
{
    public static DriveContext Clear => new(float.PositiveInfinity);

    /// <summary>How far ahead of the nose the section this car holds ends — where what cut it begins.</summary>
    public float HeldM => AuthorityM + MarginM;
}

/// <summary>
/// Which of the things that limit a car is the one limiting it. Speed is the minimum of everything, and
/// <b>which term won is the only question worth asking of a car that is going slowly</b> — an instrument
/// rather than a rule, and the whole of what a driver can be said to be doing (CAR-15).
/// </summary>
internal enum DrivingHold : byte
{
    /// <summary>Nothing but the gear's own cap.</summary>
    None,

    /// <summary>The corner being driven, or one within braking range ahead.</summary>
    Corner,

    /// <summary>
    /// <b>The corner the wheel is asking for</b>, tighter than the line's where the car is: steering back onto its
    /// line, or turning harder than the tyres let it and asking for more.
    /// </summary>
    Wheel,

    /// <summary>The end of the line it has been given.</summary>
    LineEnd,

    /// <summary>
    /// The ground it was granted to stop in has run out: somebody in front stands on or claimed the rest of
    /// it. <b>This is what queueing is</b> — the whole of following, and a speed behaviour rather than a
    /// decision.
    /// </summary>
    Claimed,

    /// <summary>A light holding the road in front of it (<see cref="HeadwayKind.Light"/>): the grant, cut at a bar.</summary>
    Waiting,

    /// <summary>
    /// <b>The end of its own plan</b> (<see cref="DriveContext.HorizonM"/>): nothing in front, and the road it may
    /// plan runs out before the road it wanted. On an open road this is the pace the town is driven at.
    /// </summary>
    Reach,

    /// <summary>
    /// <b>The place this car was sent to</b> — a casualty, a wreck, a scene a police car is closing the
    /// road at, or a place a hand named (AMB-5, EVA-3, SRV-6, CTL-8a). It is named apart from the rest
    /// because it is the one term somebody asked for rather than something the road did to the car.
    /// </summary>
    Place,

    /// <summary>It is not on its line at all (CAR-9), and what it does about that is the leg's.</summary>
    LostLine,

    /// <summary>
    /// <b>Backing down its own lane</b> for the room to step out round what it means to get past (CAR-50), to where
    /// that room begins or the ground behind it was had to, whichever is nearer.
    /// </summary>
    BackingUp,
}

/// <summary>
/// <b>A tick of a car driving a line</b> (<see cref="CarFollower.DriveTheLine"/>): the command, the term that set its
/// speed, the speed the road alone would have allowed — the ceiling on its next claim — and whether the margin was spent.
/// </summary>
/// <param name="PlannedMps"><see cref="float.NaN"/> for a car backing up, which plans nothing down the road.</param>
internal readonly record struct LineDrive(DriveCommand Command, DrivingHold Hold, float PlannedMps, bool Hazard);

/// <summary>
/// The feedback controller that drives a line: <b>the wheel is pure pursuit and the pedals are a speed
/// profile</b>, and its whole output is the one command a hand could have given (the control
/// loop).
/// </summary>
/// <remarks>
/// <para>
/// <b>Autonomy runs on top of the physics and never instead of it.</b> Nothing here moves a car: it
/// returns a steering angle and a pedal, the tyres decide what that is worth on the ground under them,
/// and the solver decides where the car ends up. A follower that placed a car on its line would be a
/// car that could not be pushed off it.
/// </para>
/// <para>
/// <b>The line is a recommendation and this is the car's own answer to it</b> (CAR-10). What the town
/// precomputed is where a car is <em>asked</em> to go; how far the wheel turns and how fast the car takes
/// it are worked out here, every tick, from <see cref="CarBuild"/> — so the same line is driven by a
/// hatchback and by a truck at different speeds, at different lock, and along slightly different ground
/// (CAR-10a).
/// </para>
/// <para>
/// <b>Progress is the rear axle projected onto the line</b> (CAR-4a), searched in a window around where
/// the car last was, so a car that has been shoved sideways knows how far along it actually is rather
/// than how far it has driven — and a line that doubles back past itself is not read backwards.
/// </para>
/// <para>
/// <b>Speed is the minimum of everything</b>, and every distance in it is measured a reaction lead
/// ahead of where the car is, or the car arrives at each constraint as late as what it read is stale.
/// </para>
/// </remarks>
internal static class CarFollower
{
    /// <summary>The one point on a car that travels the way the car is pointing, and the point every line is drawn for.</summary>
    public static Vector2 RearAxleM(in CarBuild car, Vector2 positionM, float headingRad) =>
        RearAxleM(car, positionM, Heading.Unit(headingRad));

    /// <summary>The same point, for a caller that already holds the direction — which every tick of the driver does.</summary>
    /// <remarks>
    /// <b>Where the axle is under the body is this car's own</b> (CAR-11): a van carries its rear axle a
    /// metre and a bit behind the middle of itself and a hatchback barely a metre, and a line driven for
    /// the wrong point is a car that parks with its nose where its doors should be.
    /// </remarks>
    public static Vector2 RearAxleM(in CarBuild car, Vector2 positionM, Vector2 forward) =>
        positionM - forward * car.CentreAheadOfAxleM;

    /// <summary>Where the car is along its line, searched in a window around where it last was.</summary>
    public static float ProgressM(in CarBuild car, ReadOnlySpan<ArcSeg> line, Vector2 rearAxleM, float lastProgressM) =>
        Spline.ProjectM(line, rearAxleM, lastProgressM, car.ProjectionWindowM);

    /// <summary>The same, the line walked from where <paramref name="cursor"/> stands and the cursor moved on with the car.</summary>
    public static float ProgressM(
        in CarBuild car, ReadOnlySpan<ArcSeg> line, Vector2 rearAxleM, float lastProgressM, ref SplineCursor cursor) =>
        Spline.ProjectM(line, rearAxleM, lastProgressM, car.ProjectionWindowM, ref cursor);

    /// <summary>
    /// How far off its own line the car is, which is what says whether it is still on it at all — measured to
    /// where it is aimed across the line, <paramref name="asideM"/> to the right of it on a pass (CAR-46).
    /// </summary>
    public static float OffLineM(
        ReadOnlySpan<ArcSeg> line, Vector2 rearAxleM, float progressM, float asideM = 0f, SplineCursor cursor = default)
    {
        var on = Spline.SampleFrom(line, progressM, ref cursor);
        var aimedM = asideM == 0f ? on.PositionM : on.PositionM + (on.Right * asideM);
        return (aimedM - rearAxleM).Length();
    }

    /// <summary>
    /// S-1, S-2 and S-5: <b>the wheel is pure pursuit and the pedals are the speed profile</b> — one tick of a car on its
    /// line, from the wheel and the pedal it was left at to the command it is given. Nothing here decides what the car
    /// is doing, only how what it is doing is delivered by the tyres.
    /// </summary>
    /// <param name="travel">The way the car is driving down the line: its forward, or its back for a piece in reverse.</param>
    /// <param name="alongMps">How fast it goes that way.</param>
    /// <param name="last">The command it was left at, whose wheel the rack winds on from and whose pedal travels.</param>
    /// <param name="laneless">A line with no lanes, which is a manoeuvre's piece and driven at manoeuvring pace (GEN-4f).</param>
    /// <param name="blueLight">On a call (AMB-4): a pace of its own, under every constraint the profile takes.</param>
    /// <param name="paceMps">A pace somebody put on this car, folded in with the profile's own terms.</param>
    /// <param name="cursor">Where the car's walks of <paramref name="line"/> begin (<see cref="CarFleet.LineCursor"/>), or its head.</param>
    /// <param name="backsUpByM">
    /// Where given, the car backs down the line against its metres (CAR-50) to rest this far behind where it is, at its
    /// own backing pace (<see cref="BackingMps"/>) — the wheel pursuing the line behind it, and nothing of the road ahead
    /// asked: not its corners, not its grant and not the hazard, which reads the grant ahead.
    /// </param>
    public static LineDrive DriveTheLine(
        SimConfig config, in CarBuild build, in CarPose pose, ReadOnlySpan<ArcSeg> line, ReadOnlySpan<float> entryM,
        float progressM, float lengthM, in DriveContext context, Vector2 travel, float alongMps, bool reverse,
        in DriveCommand last, in Overtake pass, bool laneless, bool blueLight, float paceMps,
        SplineCursor cursor = default, float backsUpByM = float.NaN, float leadAtM = float.NaN, float aimRad = 0f)
    {
        var rearAxleM = RearAxleM(build, pose.PositionM, pose.Forward);
        var lookaheadM = LookaheadM(build, MathF.Abs(alongMps), config.Driving.LookaheadS);
        var backing = !float.IsNaN(backsUpByM);

        // Pure pursuit asks, or the pass the car is on (CAR-46); the rack answers (CAR-3a). The wheel as it stands is
        // carried into the frame of the gear this tick drives in, since a reverse command is the same wheel with its
        // sign turned round on the way out — whichever gear it was left in, so a change of gear is no jump of the rack.
        var wasRad = reverse ? -last.SteerRad : last.SteerRad;
        var wantedRad = backing ? Steer(build, line, progressM, rearAxleM, travel, -lookaheadM)
            : pass.Begun ? SteerThePass(build, line, pass, progressM, rearAxleM, travel, lookaheadM)
            : Steer(build, line, progressM, rearAxleM, travel, lookaheadM, cursor, leadAtM, aimRad);
        var steerRad = build.WheelWoundTo(wasRad, wantedRad, config.TickSeconds);

        // Where the foot already was, so the pedal travels rather than snapping — and the profile plans from
        // there. It is along the direction being driven on both sides of the gear, because the reverse command
        // negates the wheel and nothing else.
        var lastMps2 = PedalMps2(last);
        if (backing)
        {
            var backingPedals = Pedals(
                config, build, steerRad, BackingMps(build, backsUpByM, alongMps, lastMps2), alongMps, config.TickSeconds,
                lastMps2);
            return new LineDrive(InTheGear(backingPedals, reverse), DrivingHold.BackingUp, float.NaN, Hazard: false);
        }

        var steerTan = MathF.Tan(steerRad);
        var targetMps = TargetSpeedMps(
            config, build, line, entryM, progressM, lengthM, steerRad, alongMps, lookaheadM, context, out var hold,
            out var plannedMps, pass, lastMps2, cursor, steerTan);

        // <b>A manoeuvre at a bay is driven at manoeuvring pace</b> whichever gear its piece is in, and the reverse cap
        // is that pace — deliberately off the forward cap's scale, because this is its only use.
        var capMps = reverse || laneless ? build.ReverseMaxMps : float.PositiveInfinity;

        // AMB-4: <b>a blue light buys the road and never the tyres.</b> A car on a call keeps every constraint the
        // profile already takes and plans above what holds every other car at a light — the light's hold on the road
        // (TLT-1) — so without a pace of its own it reaches the gear's cap on the first straight it meets and arrives
        // as a second casualty.
        if (blueLight) capMps = MathF.Min(capMps, config.Ambulance.CallPaceMps);

        // And a pace somebody put on this car, which is neither the road's nor the build's: an escort held under the
        // pace of what it is escorting keeps station by being caught rather than by being told to
        // (<c>IdlePlan.EscortPaceShare</c>). Folded in with the profile's own terms, so a corner, a queue and a stop
        // line all still outrank it.
        capMps = MathF.Min(capMps, paceMps);
        targetMps = MathF.Min(targetMps, capMps);

        var command = InTheGear(
            Pedals(config, build, steerRad, targetMps, alongMps, config.TickSeconds, lastMps2, steerTan), reverse);

        // <b>The margin the profile kept back, spent</b>: the profile plans every stop against usable grip (S-2), and
        // this is the tick where that margin is no longer enough. Asked of the profile's own answer rather than instead
        // of it — what it overrides is the pedal — and every tick, because a hazard inside braking distance is not
        // something to discover at the end of a decision interval. Braking and never swerving: verifying a lane is
        // clear takes time this does not have.
        var hazard = IsAHazard(config, build, alongMps, context);
        if (hazard) command = command with { ThrottleMps2 = 0f, BrakeMps2 = build.BrakingMps2, Handbrake = false };

        // The ceiling on the next claim: the profile's own answer with the grant left out, so a car held at a standstill
        // by the queue in front is not held to a standstill's worth of road — and under the same caps, so the ground a
        // car holds is the ground at the pace it will drive.
        return new LineDrive(command, hold, MathF.Min(plannedMps, capMps), hazard);
    }

    /// <summary>Pedals worked out along the way the car is going, given out in its gear: in reverse, the wheel's sign turned round.</summary>
    static DriveCommand InTheGear(in DriveCommand pedals, bool reverse) =>
        reverse ? pedals with { SteerRad = -pedals.SteerRad, Reverse = true } : pedals;

    /// <summary>
    /// Pure pursuit: the wheel is turned for the circle through the car's rear axle and a point a
    /// <b>time</b> ahead on the line, floored and ceilinged — too short and the car saws, too long and
    /// it cuts the corner.
    /// </summary>
    /// <param name="leadAtM">
    /// Where along the line the world aimed the wheel at (<c>CarView.LeadAtM</c>): its <paramref name="aimRad"/> where that
    /// is the lookahead past this very progress, and the line sampled here anywhere else.
    /// </param>
    public static float Steer(
        in CarBuild car, ReadOnlySpan<ArcSeg> line, float progressM, Vector2 rearAxleM, Vector2 forward,
        float lookaheadM, SplineCursor cursor = default, float leadAtM = float.NaN, float aimRad = 0f)
    {
        var aheadM = progressM + lookaheadM;
        return leadAtM == aheadM ? aimRad : AimAt(car, rearAxleM, forward, Spline.SampleFrom(line, aheadM, ref cursor).PositionM);
    }

    /// <summary>The wheel pure pursuit asks for to pass through a point of the line (S-1), the world's and the driver's alike.</summary>
    public static float AimAt(in CarBuild car, Vector2 rearAxleM, Vector2 forward, Vector2 leadM) =>
        SteerFor(car, PursuitBend(rearAxleM, forward, leadM));

    /// <summary>
    /// <b>The wheel on a pass</b> (CAR-46): turned for how the pass bends where the car is, and corrected by what
    /// pure pursuit asks beyond that — its answer for where the car is less its answer for where the pass puts
    /// it, which is nothing for a car on the pass.
    /// </summary>
    /// <remarks>
    /// <b>Pursuit alone would cut every step</b>: it aims a lookahead ahead, so it turns into a step before the
    /// step begins, and one drawn as short as the car can drive is one it would then need more than it has to get
    /// back onto.
    /// </remarks>
    public static float SteerThePass(
        in CarBuild car, ReadOnlySpan<ArcSeg> line, in Overtake pass, float progressM, Vector2 rearAxleM,
        Vector2 forward, float lookaheadM)
    {
        pass.PoseAtM(line, progressM, out var drawnM, out var drawnForward);
        pass.PoseAtM(line, progressM + lookaheadM, out var leadM, out _);

        var curvature = pass.BendAtM(line, progressM)
                        + PursuitBend(rearAxleM, forward, leadM) - PursuitBend(drawnM, drawnForward, leadM);
        return SteerFor(car, curvature);
    }

    /// <summary>
    /// <b>How a pass is drawn for a car doing this speed</b> (CAR-46): the speed its steps are drawn for, and the
    /// shortest step that speed allows (<see cref="Overtake.ShortestStepM"/>) — false where the line under it
    /// bends as tight as the lock on its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Drawn for the speed the car is doing, and never slower than a step bent to the lock needs</b>: below
    /// that the rack keeps up with a step at the lock whatever the pace, and a slower step is no shorter — so a
    /// car getting past from a standstill is let up to that pace.
    /// </para>
    /// <para>
    /// <b>The line's own bend is taken off what the step may bend</b>, so the two together never ask for more than
    /// the lock or the tyres.
    /// </para>
    /// </remarks>
    /// <param name="lineBend">The most the line under the pass bends, at the lane or the lane beside.</param>
    public static bool ShapeAPass(
        in CarBuild car, float speedMps, float asideM, float corneringMps2, float lineBend, out float stepM,
        out float driveMps)
    {
        stepM = 0f;
        driveMps = 0f;
        var lockBend = (1f / car.TurningRadiusM) - lineBend;
        if (lockBend <= 0f) return false;

        var acrossM = MathF.Abs(asideM);
        var atTheLockM = Overtake.ShortestStepM(acrossM, lockBend, float.PositiveInfinity);
        var rackMps = atTheLockM * atTheLockM * atTheLockM * car.SteerRateRadPerS
                      / (4f * MathF.PI * MathF.PI * acrossM * car.WheelbaseM);
        var gripMps = MathF.Sqrt(corneringMps2 * car.TurningRadiusM);
        driveMps = MathF.Max(speedMps, MathF.Min(rackMps, gripMps));

        var mostBend = MathF.Min(lockBend, (corneringMps2 / (driveMps * driveMps)) - lineBend);
        if (mostBend <= 0f) return false;

        stepM = Overtake.ShortestStepM(acrossM, mostBend, car.SteerRateRadPerS / (driveMps * car.WheelbaseM));
        return true;
    }

    /// <summary>
    /// <b>The most a step this long may be driven at</b> (CAR-46) — <see cref="ShapeAPass"/> the other way round: its
    /// bend and the line's under it inside what the tyres hold at the speed, and its bend changing no faster than the
    /// rack turns while the car rolls it. Nothing where the two together bend tighter than the lock.
    /// </summary>
    /// <param name="lineBend">The most the line under the step bends, at the lane or the lane beside.</param>
    public static float StepMps(in CarBuild car, float stepM, float asideM, float corneringMps2, float lineBend)
    {
        var bend = Overtake.MostBendOfAStep(asideM, stepM) + lineBend;
        if (bend > 1f / car.TurningRadiusM) return 0f;

        var gripMps = MathF.Sqrt(corneringMps2 / bend);
        var rackMps = car.SteerRateRadPerS / (Overtake.MostBendChangeOfAStep(asideM, stepM) * car.WheelbaseM);
        return MathF.Min(gripMps, rackMps);
    }

    /// <summary>
    /// The circle through the axle, tangent to the heading, that passes through the lead point: its curvature is
    /// 2·sin α ⁄ reach.
    /// </summary>
    static float PursuitBend(Vector2 rearAxleM, Vector2 forward, Vector2 leadM)
    {
        var toLead = leadM - rearAxleM;
        var reachM = toLead.Length();
        return reachM < 1e-3f ? 0f : 2f * Spline.Cross(forward, toLead) / (reachM * reachM);
    }

    /// <summary>
    /// The steering angle that holds a curvature on <em>this car's</em> wheelbase — the same line asks a long car
    /// for more lock than a short one, and past the lock it is asking for a circle the car cannot hold at all
    /// (CAR-11).
    /// </summary>
    static float SteerFor(in CarBuild car, float curvature) =>
        Math.Clamp(Trig.Atan(curvature * car.WheelbaseM), -car.MaxSteerRad, car.MaxSteerRad);

    /// <summary>
    /// <b>How far in front of itself the profile plans</b>: the staleness of what the driver reads (<see cref="SimConfig.CarReactionS"/>), and
    /// the time the pedal takes to travel from where it is to the rate that decision asked for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both halves are delays and neither is a margin.</b> A pedal that travels rather than snapping
    /// (<see cref="CarBuild.PedalRateMps3"/>) reaches the planned rate after a ramp, and a car that
    /// planned as though it arrived at once brakes over less ground than it asked for and makes up the
    /// difference by braking harder than it planned. The ramp costs half of itself, which is the area a
    /// triangle of it has against the rectangle it is standing in.
    /// </para>
    /// <para>
    /// <b>A pedal still on the throttle has that to come off first</b>, and the car goes on gaining speed while
    /// it does: a release taking <c>t = p / r</c> costs <c>t (1 + p / 2b)</c> of lead. Left out, a car pulling
    /// away hard towards a red began braking a metre late and was committed past the bar.
    /// </para>
    /// </remarks>
    /// <param name="throttleMps2">What the pedal is asking for now, throttle positive (<see cref="PedalMps2"/>).</param>
    /// <remarks>With the pedal off the throttle it is the build's own (<see cref="CarBuild.RestingLeadS"/>).</remarks>
    public static float LeadS(in CarBuild car, float throttleMps2 = 0f)
    {
        var onMps2 = MathF.Max(0f, throttleMps2);
        var releaseS = onMps2 / car.PedalRateMps3;
        return car.RestingLeadS + (releaseS * (1f + (onMps2 / (2f * car.PlannedBrakingMps2))));
    }

    /// <summary>
    /// <b>The ground a car doing <paramref name="alongMps"/> covers before it is doing <paramref name="toMps"/></b>, slowed
    /// as the profile slows it: a lead (<see cref="LeadS"/>) and the braking from there. Where <paramref name="toMps"/> is
    /// nought it is the ground the car can no longer stop short of.
    /// </summary>
    /// <remarks>
    /// The lead is the one a pedal off the throttle takes, so the ground is the speed's alone: a plan's committed ground
    /// never recedes as a throttle comes off.
    /// </remarks>
    public static float SlowsOverM(in CarBuild car, float alongMps, float toMps = 0f) =>
        alongMps <= toMps
            ? 0f
            : (alongMps * car.RestingLeadS)
              + (((alongMps * alongMps) - (toMps * toMps)) / (2f * car.PlannedBrakingMps2));

    /// <summary>
    /// <b>The pace a car backing up for the room to step out drives at</b> (CAR-50): its reverse cap, and down to
    /// rest by <paramref name="toGoM"/> behind it, taken a lead ahead as every stop is.
    /// </summary>
    /// <param name="alongMps">How fast it is going the way it is backing.</param>
    /// <param name="lastMps2">Where the pedal was left last tick (<see cref="PedalMps2"/>), which the lead travels from.</param>
    public static float BackingMps(in CarBuild car, float toGoM, float alongMps, float lastMps2)
    {
        var leadM = MathF.Abs(alongMps) * LeadS(car, lastMps2);
        return MathF.Min(car.ReverseMaxMps, ApproachMps(0f, toGoM - leadM, car.PlannedBrakingMps2));
    }

    /// <summary>How far ahead the wheel is aimed: a time, floored at the car's own length and ceilinged.</summary>
    public static float LookaheadM(in CarBuild car, float speedMps, float lookaheadS) =>
        Math.Clamp(speedMps * lookaheadS, car.LookaheadFloorM, car.LookaheadCeilingM);

    /// <summary>
    /// The least of everything that limits a car: the gear's cap, the corner it is in, the corner the
    /// wheel is asking for, every corner within braking range, the end of the line, what is in front of
    /// it, where it must be stopped by, and the ground it was granted to stop in.
    /// </summary>
    /// <param name="plannedMps">
    /// What it would have asked for with the road to itself — every term the road sets, and not the grant, the end
    /// of its own plan or the corner its wheel is asking for. <b>It is the ceiling on the next claim</b>, and it is
    /// taken here because this is where the terms are: the road a car holds is bounded by the speed it is driving
    /// towards as well as by the one it can reach before the next decision.
    /// </param>
    /// <param name="pass">
    /// The pass the car is on (CAR-46), each of whose steps is driven no faster than its own figure
    /// (<see cref="Overtake.MostMpsAtM"/>): quicker, the rack could not keep up with its bend nor the tyres hold it.
    /// </param>
    /// <param name="lastMps2">Where the pedal was left last tick (<see cref="PedalMps2"/>), which the lead travels from.</param>
    /// <param name="entryM">
    /// The line's own entry figures (<see cref="World.Road.CornerLimits"/>), one to an arc: every corner past an
    /// arc, folded into what that arc may be entered at.
    /// </param>
    /// <param name="cursor">Where on the line the car's walks of it begin (<see cref="CarFleet.LineCursor"/>), or its head.</param>
    /// <param name="steerTan">The tangent of <paramref name="steerRad"/> where the caller has it, or NaN.</param>
    public static float TargetSpeedMps(
        SimConfig config, in CarBuild car, ReadOnlySpan<ArcSeg> line, ReadOnlySpan<float> entryM, float progressM,
        float lineLengthM, float steerRad, float alongMps, float lookaheadM, in DriveContext context,
        out DrivingHold hold, out float plannedMps, in Overtake pass = default, float lastMps2 = 0f,
        SplineCursor cursor = default, float steerTan = float.NaN)
    {
        hold = DrivingHold.None;
        var lateralMps2 = car.PlannedCorneringMps2;
        var brakingMps2 = car.PlannedBrakingMps2;
        var leadM = MathF.Abs(alongMps) * LeadS(car, lastMps2);

        var targetMps = car.MaxSpeedMps;

        // Every corner ahead, off the segments. A corner is reached by the *lead point* before it is reached by the
        // car, and the wheel is already turning into it by then — counting the lookahead as well as the reaction
        // lead is what stops a car arriving at the corner speed a lookahead too late, which is a car on the pavement
        // at the exit of every tight bend. So each arc the lead point has reached binds at its own corner, and the
        // first it has not binds the braking into it: its entry figure already holds every corner past it. The arcs
        // reached bind as their tightest does — a root and a quotient are monotone and rounded, so to the bit.
        var gripMps2 = car.UtmostBrakingMps2;
        var reachedM = progressM + leadM + lookaheadM;
        var from = cursor.Piece < line.Length ? cursor.StartingBefore(progressM) : default;
        var startM = from.PieceStartM;
        var tightest = 0f;
        for (var arc = from.Piece; arc < line.Length; arc++)
        {
            var endM = startM + line[arc].LengthM;
            if (endM >= progressM)
            {
                if (startM > reachedM)
                {
                    Bind(
                        ref targetMps, ApproachMps(MathF.Sqrt(gripMps2 * entryM[arc]), startM - reachedM, brakingMps2),
                        DrivingHold.Corner, ref hold);
                    break;
                }

                tightest = MathF.Max(tightest, MathF.Abs(line[arc].Curvature));
            }

            startM = endM;
        }

        Bind(ref targetMps, CornerMps(tightest, lateralMps2), DrivingHold.Corner, ref hold);

        // Each step of a pass no faster than it was drawn for — come up to at that pace where the car has its pass short
        // of where the step out begins — and the straight between them the car's own, which it pulls away along, as far
        // as it can come down again to the pace of the step back by where that begins.
        if (pass.Begun)
        {
            var stepMps = progressM < pass.OutM
                ? ApproachMps(pass.OutMps, pass.OutM - progressM - leadM, brakingMps2)
                : pass.MostMpsAtM(progressM);
            Bind(ref targetMps, stepMps, DrivingHold.Corner, ref hold);
            Bind(
                ref targetMps, ApproachMps(pass.BackMps, pass.BackM - progressM - leadM, brakingMps2), DrivingHold.Corner,
                ref hold);
        }

        Bind(ref targetMps, ApproachMps(0f, lineLengthM - progressM - leadM, brakingMps2), DrivingHold.LineEnd, ref hold);

        // The place this car was sent to, which is a term of the same minimum rather than an errand's own
        // hand on the wheel: an ambulance stopping at a casualty is running its line on the road that
        // casualty left it.
        Bind(ref targetMps, ApproachMps(0f, context.PlaceStopM - leadM, brakingMps2), DrivingHold.Place, ref hold);

        // The road to itself, which is the figure the next claim is asked for at — before the grant
        // is folded in, and never after it.
        plannedMps = MathF.Max(0f, targetMps);

        // <b>And the corner the wheel is asking for</b>, which is the car's and not the road's: left out of the planned
        // speed, or a car steering back onto its line would draw its plan back for a bend the road does not have.
        Bind(
            ref targetMps, CornerMps(TanOf(steerRad, steerTan) / car.WheelbaseM, lateralMps2), DrivingHold.Wheel, ref hold);

        // <b>And the end of its own plan</b> (TER-4c.1), where it was held short of what the car wanted: past it is
        // road nobody holds for this car, so ground it could no longer stop short of could run on beyond anything
        // the town can see it coming. Left out of the planned speed, as the grant is, or the plan would shrink to
        // fit it and let it go. <b>Stopped a stand-off short of it and two leads ahead</b>: what is found standing just
        // past the end is met by the plan only once it has grown into it, cuts the grant a stand-off short of it, and
        // is read a lead after the end itself was — bound at the bare end, a car cruising at its plan's reach was
        // already committed past a light's hold by the turn that found it.
        Bind(
            ref targetMps, ApproachMps(0f, context.HorizonM - config.Driving.StandOffM - (2f * leadM), brakingMps2),
            DrivingHold.Reach, ref hold);

        // <b>And the grant, which is the whole of following</b> (S-2a). The ground the reservations gave this
        // car to stop in inverts straight into a speed, taken a lead ahead as every stop point above is: what may
        // be held here to be at rest by the far end of it whatever cut it — a queue, a light, somebody on foot.
        // Nothing in front is followed or credited with a speed of its own; a car keeps to the section it was
        // given, and what is in front moving on is that section growing.
        Bind(
            ref targetMps, ApproachMps(0f, context.AuthorityM - leadM, brakingMps2),
            context.GrantCutBy == HeadwayKind.Light ? DrivingHold.Waiting : DrivingHold.Claimed, ref hold);

        // Something the car means to get past is slowed for gently (CAR-46), so it is come up to slower and the
        // lane beside has longer to clear before the car has to stand.
        if (context.WaitsToPass)
        {
            Bind(
                ref targetMps,
                ApproachMps(0f, context.AuthorityM - leadM, brakingMps2 * config.Driving.WaitingToPassBrakingShare),
                DrivingHold.Claimed, ref hold);
        }

        // And stood for where it waits for the ground for what it does next: its step out, or its manoeuvre's first piece.
        Bind(ref targetMps, ApproachMps(0f, context.WaitAtM - leadM, brakingMps2), DrivingHold.Claimed, ref hold);

        return MathF.Max(0f, targetMps);
    }

    /// <summary>
    /// <b>The tick where the section this car holds cannot be stopped in</b> (S-2) even at what the tyres can
    /// put down, so what is left of them is spent at once. Braking that ramps up wastes the most valuable
    /// distance there is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The section and nothing beside it</b> (<see cref="DriveContext.HeldM"/>): a car keeps to the section
    /// it was given, so the only way its stop runs past the end of it is the section being cut short under it
    /// — somebody stepping out, a car pulling in. Braking into the gap it keeps off the end is ordinary braking.
    /// </para>
    /// <para>
    /// <b>It is read against the utmost and never a figure of its own</b> (SIM-7). The profile plans
    /// every stop at <see cref="DrivingFigures.BrakingMargin"/> of that, so a threshold below it fires on
    /// the profile's own ordinary braking and takes the pedal off it.
    /// </para>
    /// </remarks>
    public static bool IsAHazard(SimConfig config, in CarBuild car, float alongMps, in DriveContext context)
    {
        if (alongMps <= config.Driving.StopSpeedMps || float.IsPositiveInfinity(context.AuthorityM)) return false;

        var heldM = context.HeldM;
        if (heldM <= 0f) return true;

        return alongMps * alongMps / (2f * heldM) > car.UtmostBrakingMps2;
    }

    static void Bind(ref float targetMps, float limitMps, DrivingHold limit, ref DrivingHold hold)
    {
        if (limitMps >= targetMps) return;

        targetMps = limitMps;
        hold = limit;
    }

    /// <summary>What may be held here to be down to <paramref name="atMps"/> in <paramref name="distanceM"/>.</summary>
    public static float ApproachMps(float atMps, float distanceM, float brakingMps2) =>
        distanceM <= 0f ? atMps : MathF.Sqrt(atMps * atMps + 2f * brakingMps2 * distanceM);

    /// <summary>What a corner of this curvature may be taken at before the tyres let go.</summary>
    public static float CornerMps(float curvature, float lateralMps2)
    {
        var bend = MathF.Abs(curvature);
        return bend < 1e-4f ? float.PositiveInfinity : MathF.Sqrt(lateralMps2 / bend);
    }

    /// <summary>
    /// One pedal or the other, never both, and never more than the pedal itself can ask for. The
    /// handbrake is what holds a car that has arrived at a stop rather than the brake pedal being held
    /// against a body the solver has already settled.
    /// </summary>
    /// <remarks>
    /// <b>The pedal moves at a bounded rate</b> (<see cref="CarBuild.PedalRateMps3"/>). What closes the
    /// speed error in one tick is a demand of sixty times that error, so anything past a fifth of a metre a
    /// second saturates it and a car merely holding a speed snaps between the two stops several times a
    /// second — which the tyre model then answers with a load transfer apiece. Bounding the rate keeps the
    /// demand exactly where it was and only limits how fast the foot gets there.
    /// </remarks>
    /// <param name="lastMps2">
    /// What the pedal was asking for last tick, throttle positive and brake negative — <c>0</c> for a
    /// caller with no previous command, which is a foot starting from neither pedal.
    /// </param>
    /// <param name="steerTan">The tangent of <paramref name="steerRad"/> where the caller has it, or NaN.</param>
    public static DriveCommand Pedals(
        SimConfig config, in CarBuild car, float steerRad, float targetMps, float alongMps, float dtS, float lastMps2 = 0f,
        float steerTan = float.NaN)
    {
        // Both terms, and not just the target: the handbrake is the car's own (CTL-5a), pulled on a dead
        // stop it has already made and never on the way down to one, so a car waiting holds its spot
        // instead of creeping.
        if (targetMps <= config.Driving.StopSpeedMps && MathF.Abs(alongMps) <= config.Driving.StopSpeedMps)
        {
            return new DriveCommand(steerRad, 0f, 0f, Handbrake: true, Reverse: false);
        }

        // The pedal left last tick acts on steps the car has not seen yet (SimFigures.WorldStepsAhead): the demand closes
        // on the speed the car will be doing once it has, or each tick asks again for a gap the last one already closed
        // and the speed hunts round its target.
        var willBeMps = alongMps + (lastMps2 * dtS * SimFigures.WorldStepsAhead);
        var travelMps2 = car.PedalRateMps3 * dtS;
        var wantedMps2 = Math.Clamp((targetMps - willBeMps) / dtS, lastMps2 - travelMps2, lastMps2 + travelMps2);

        return wantedMps2 >= 0f
            ? new DriveCommand(steerRad, MathF.Min(wantedMps2, car.AccelerationMps2), 0f, false, false)
            : new DriveCommand(
                steerRad, 0f, MathF.Min(-wantedMps2, BrakeCeilingMps2(config, car, steerRad, alongMps, steerTan)), false,
                false);
    }

    /// <summary>The tangent of a wheel's angle, unless the caller already has it.</summary>
    static float TanOf(float steerRad, float steerTan) => float.IsNaN(steerTan) ? MathF.Tan(steerRad) : steerTan;

    /// <summary>
    /// <b>The most a driver brakes at in the ordinary way</b> (CAR-47): what the tyres have left along the roll once
    /// the corner the wheel is asking for has been paid for, at the share a stop is planned at — so on a straight it
    /// is the braking every stop is planned against (<see cref="CarBuild.PlannedBrakingMps2"/>), and in a bend it is less.
    /// </summary>
    /// <remarks>
    /// <b>Past it the brake buys a slide and costs the corner</b>: the pedal stands well clear of the tyres, and a
    /// car cornering at its share of grip that was let ask for the whole of it braked straight on out of the bend,
    /// ran wide, wound the wheel on for the corner it had lost, and was braked harder for that. <b>The wheel's corner
    /// and not the one the tyres are carrying</b>, because a sliding car carries less than it asks for and would read
    /// its own slide as room to brake. A hazard is not held to this (<see cref="IsAHazard"/>).
    /// </remarks>
    public static float BrakeCeilingMps2(
        SimConfig config, in CarBuild car, float steerRad, float alongMps, float steerTan = float.NaN)
    {
        var acrossMps2 = alongMps * alongMps * MathF.Abs(TanOf(steerRad, steerTan)) / car.WheelbaseM;
        var leftMps2 = TyreModel.DriveLeftMps2(car.GripMps2, acrossMps2);
        return MathF.Min(car.BrakingMps2, leftMps2) * config.Driving.BrakingMargin;
    }

    /// <summary>Which way a command is leaning, throttle positive and brake negative — what the next tick's pedal travels from.</summary>
    public static float PedalMps2(in DriveCommand command) => command.ThrottleMps2 - command.BrakeMps2;
}
