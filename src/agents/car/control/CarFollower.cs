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
    /// A live driver on this car's own line, pointed the way this car is going. <b>A queue however long
    /// it has stood</b> — the car at its head is held by something, and that something is not this car's
    /// to drive round.
    /// </summary>
    Queue,

    /// <summary>
    /// Something on the road that is not going anywhere: a wreck, a car with nobody in it, a body shoved
    /// off its own line.
    /// </summary>
    Obstruction,

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

    /// <summary>
    /// <b>A person standing in the lane</b> — on the paint or on bare carriageway, it is the same fact to a
    /// driver, and the body's own reservation is what holds the traffic off it (PER-26, TER-4c.2).
    /// </summary>
    Walker,

    /// <summary>
    /// <b>Somebody getting past something</b> (TER-4c.6): the ground an overtake will cover, which is held
    /// like a body and is never itself something to get past.
    /// </summary>
    Passing,
}

/// <summary>
/// What the driver has been told about the world this tick, over and above the line it is driving: the
/// ground under it, and the road it was granted.
/// </summary>
/// <param name="GroundCoefficient">What the surface under it is worth, which scales every grip figure the profile plans against.</param>
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
/// get past (CAR-50), or zero. Asked for in the rebuild after, which says how much of it was had
/// (<see cref="Body.CarFleet.BackRoomM"/>).
/// </param>
/// <param name="Blocked">
/// <b>It can neither get past what stands in front of it nor back up for the room to</b> (CAR-50): the rebuild after
/// lays its body as one going nowhere, which the traffic behind it may get past in turn.
/// </param>
internal readonly record struct DriveContext(
    float GroundCoefficient, float AuthorityM = float.PositiveInfinity, HeadwayKind GrantCutBy = HeadwayKind.Nothing,
    float PlaceStopM = float.PositiveInfinity, float MarginM = 0f, bool WaitsToPass = false,
    float HorizonM = float.PositiveInfinity, float PassAsideM = 0f, float BackUpM = 0f, bool Blocked = false)
{
    public static DriveContext Clear => new(1f);

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

/// <summary>What a tick of the driver came to: the command, and what decided it.</summary>
internal readonly record struct DriveDecision(DriveCommand Command, DrivingHold Hold, float TargetMps);

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
/// ahead of where the car is, or the car arrives at each constraint one decision late.
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

    /// <summary>
    /// How far off its own line the car is, which is what says whether it is still on it at all — measured to
    /// where it is aimed across the line, <paramref name="asideM"/> to the right of it on a pass (CAR-46).
    /// </summary>
    public static float OffLineM(ReadOnlySpan<ArcSeg> line, Vector2 rearAxleM, float progressM, float asideM = 0f)
    {
        var on = Spline.SampleAt(line, progressM);
        return (on.PositionM + (on.Right * asideM) - rearAxleM).Length();
    }

    /// <summary>One tick of one driver: the line, what is on it, and what the body is doing, into one command.</summary>
    /// <param name="entryM">The line's own entry figures (<see cref="World.Road.CornerLimits"/>), one to an arc.</param>
    public static DriveDecision Step(
        SimConfig config, in CarBuild car, in CarPose pose, ReadOnlySpan<ArcSeg> line, ReadOnlySpan<float> entryM,
        float progressM, float lineLengthM, in DriveContext context, float dtS)
    {
        var forward = pose.Forward;
        var alongMps = Vector2.Dot(pose.VelocityMps, forward);
        var rearAxleM = RearAxleM(car, pose.PositionM, forward);

        var lookaheadM = LookaheadM(car, MathF.Abs(alongMps), config.Driving.LookaheadS);
        var steerRad = Steer(car, line, progressM, rearAxleM, forward, lookaheadM);
        var targetMps = TargetSpeedMps(
            config, car, line, entryM, progressM, lineLengthM, steerRad, alongMps, lookaheadM, context, out var hold,
            out _);

        return new DriveDecision(
            Pedals(config, car, steerRad, targetMps, alongMps, context.GroundCoefficient, dtS), hold, targetMps);
    }

    /// <summary>
    /// Pure pursuit: the wheel is turned for the circle through the car's rear axle and a point a
    /// <b>time</b> ahead on the line, floored and ceilinged — too short and the car saws, too long and
    /// it cuts the corner.
    /// </summary>
    public static float Steer(
        in CarBuild car, ReadOnlySpan<ArcSeg> line, float progressM, Vector2 rearAxleM, Vector2 forward,
        float lookaheadM) =>
        SteerFor(car, PursuitBend(rearAxleM, forward, Spline.SampleAt(line, progressM + lookaheadM).PositionM));

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

    /// <summary>What the car plans a corner to hold on the ground it is on: the tyres' grip, less the margin kept back.</summary>
    public static float CorneringMps2(SimConfig config, in CarBuild car, float groundCoefficient) =>
        car.GripMps2 * groundCoefficient * config.Driving.GripMargin;

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
        Math.Clamp(MathF.Atan(curvature * car.WheelbaseM), -car.MaxSteerRad, car.MaxSteerRad);

    /// <summary>
    /// <b>How far in front of itself the profile plans</b>: the staleness of the driver's own decision, and
    /// the time the pedal takes to travel from where it is to the rate that decision asked for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Both halves are delays and neither is a margin.</b> A pedal that travels rather than snapping
    /// (<see cref="SimConfig.CarPedalRateMps3"/>) reaches the planned rate after a ramp, and a car that
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
    public static float LeadS(SimConfig config, in CarBuild car, float brakingMps2, float throttleMps2 = 0f)
    {
        var onMps2 = MathF.Max(0f, throttleMps2);
        var releaseS = onMps2 / car.PedalRateMps3;
        return config.CarReactionS + (brakingMps2 / (2f * car.PedalRateMps3))
               + (releaseS * (1f + (onMps2 / (2f * brakingMps2))));
    }

    /// <summary>
    /// <b>The pace a car backing up for the room to step out drives at</b> (CAR-50): its reverse cap, and down to
    /// rest by <paramref name="toGoM"/> behind it, taken a lead ahead as every stop is.
    /// </summary>
    /// <param name="alongMps">How fast it is going the way it is backing.</param>
    /// <param name="lastMps2">Where the pedal was left last tick (<see cref="PedalMps2"/>), which the lead travels from.</param>
    public static float BackingMps(
        SimConfig config, in CarBuild car, float toGoM, float alongMps, float groundCoefficient, float lastMps2)
    {
        var brakingMps2 = BrakingMps2(config, car, groundCoefficient);
        var leadM = MathF.Abs(alongMps) * LeadS(config, car, brakingMps2, lastMps2);
        return MathF.Min(car.ReverseMaxMps, ApproachMps(0f, toGoM - leadM, brakingMps2));
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
    public static float TargetSpeedMps(
        SimConfig config, in CarBuild car, ReadOnlySpan<ArcSeg> line, ReadOnlySpan<float> entryM, float progressM,
        float lineLengthM, float steerRad, float alongMps, float lookaheadM, in DriveContext context,
        out DrivingHold hold, out float plannedMps, in Overtake pass = default, float lastMps2 = 0f)
    {
        hold = DrivingHold.None;
        var lateralMps2 = CorneringMps2(config, car, context.GroundCoefficient);
        var brakingMps2 = BrakingMps2(config, car, context.GroundCoefficient);
        var leadM = MathF.Abs(alongMps) * LeadS(config, car, brakingMps2, lastMps2);

        var targetMps = car.MaxSpeedMps;

        // Every corner ahead, off the segments. A corner is reached by the *lead point* before it is reached by the
        // car, and the wheel is already turning into it by then — counting the lookahead as well as the reaction
        // lead is what stops a car arriving at the corner speed a lookahead too late, which is a car on the pavement
        // at the exit of every tight bend. So each arc the lead point has reached binds at its own corner, and the
        // first it has not binds the braking into it: its entry figure already holds every corner past it.
        var gripMps2 = car.UtmostBrakingMps2(context.GroundCoefficient);
        var reachedM = progressM + leadM + lookaheadM;
        var startM = 0f;
        for (var arc = 0; arc < line.Length; arc++)
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

                Bind(ref targetMps, CornerMps(line[arc].Curvature, lateralMps2), DrivingHold.Corner, ref hold);
            }

            startM = endM;
        }

        // Each step of a pass no faster than it was drawn for, and the straight between them the car's own — which it
        // pulls away along, as far as it can come down again to the pace of the step back by where that begins.
        if (pass.Begun)
        {
            Bind(ref targetMps, pass.MostMpsAtM(progressM), DrivingHold.Corner, ref hold);
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
        Bind(ref targetMps, CornerMps(MathF.Tan(steerRad) / car.WheelbaseM, lateralMps2), DrivingHold.Wheel, ref hold);

        // <b>And the end of its own plan</b> (TER-4c.1), where it was held short of what the car wanted: past it is
        // road nobody holds for this car, so ground it could no longer stop short of could run on beyond anything
        // the town can see it coming. Left out of the planned speed, as the grant is, or the plan would shrink to
        // fit it and let it go.
        Bind(ref targetMps, ApproachMps(0f, context.HorizonM - leadM, brakingMps2), DrivingHold.Reach, ref hold);

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

        return MathF.Max(0f, targetMps);
    }

    /// <summary>
    /// What the car may actually brake at on the ground it is on: the pedal's own bound, or what the tyres
    /// can put down along the roll, whichever gives out first — and nearly all of that
    /// (<see cref="DrivingFigures.BrakingMargin"/>), because a stop is the one manoeuvre a driver aims the
    /// whole car at.
    /// </summary>
    /// <remarks>
    /// <b>It is a figure along the roll and takes no notice of what the wheel is doing</b>, and every stop is
    /// planned at it. The brake a driver actually applies in a bend is less (<see cref="BrakeCeilingMps2"/>), since
    /// the tyres answer to one ellipse: a stop planned through a corner is made over more ground than it was planned
    /// in, and what catches the difference is the hazard (<see cref="IsAHazard"/>).
    /// </remarks>
    public static float BrakingMps2(SimConfig config, in CarBuild car, float groundCoefficient) =>
        car.UtmostBrakingMps2(groundCoefficient) * config.Driving.BrakingMargin;

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

        return alongMps * alongMps / (2f * heldM) > car.UtmostBrakingMps2(context.GroundCoefficient);
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
    /// <b>The pedal moves at a bounded rate</b> (<see cref="SimConfig.CarPedalRateMps3"/>). What closes the
    /// speed error in one tick is a demand of sixty times that error, so anything past a fifth of a metre a
    /// second saturates it and a car merely holding a speed snaps between the two stops several times a
    /// second — which the tyre model then answers with a load transfer apiece. Bounding the rate keeps the
    /// demand exactly where it was and only limits how fast the foot gets there.
    /// </remarks>
    /// <param name="groundCoefficient">The ground under the car, which the brake's ceiling is taken on (<see cref="BrakeCeilingMps2"/>).</param>
    /// <param name="lastMps2">
    /// What the pedal was asking for last tick, throttle positive and brake negative — <c>0</c> for a
    /// caller with no previous command, which is a foot starting from neither pedal.
    /// </param>
    public static DriveCommand Pedals(
        SimConfig config, in CarBuild car, float steerRad, float targetMps, float alongMps, float groundCoefficient,
        float dtS, float lastMps2 = 0f)
    {
        // Both terms, and not just the target: the handbrake is the car's own (CTL-5a), pulled on a dead
        // stop it has already made and never on the way down to one, so a car waiting holds its spot
        // instead of creeping.
        if (targetMps <= config.Driving.StopSpeedMps && MathF.Abs(alongMps) <= config.Driving.StopSpeedMps)
        {
            return new DriveCommand(steerRad, 0f, 0f, Handbrake: true, Reverse: false);
        }

        var travelMps2 = car.PedalRateMps3 * dtS;
        var wantedMps2 = Math.Clamp((targetMps - alongMps) / dtS, lastMps2 - travelMps2, lastMps2 + travelMps2);

        return wantedMps2 >= 0f
            ? new DriveCommand(steerRad, MathF.Min(wantedMps2, car.AccelerationMps2), 0f, false, false)
            : new DriveCommand(
                steerRad, 0f, MathF.Min(-wantedMps2, BrakeCeilingMps2(config, car, steerRad, alongMps, groundCoefficient)),
                false, false);
    }

    /// <summary>
    /// <b>The most a driver brakes at in the ordinary way</b> (CAR-47): what the tyres have left along the roll once
    /// the corner the wheel is asking for has been paid for, at the share a stop is planned at — so on a straight it
    /// is the braking every stop is planned against (<see cref="BrakingMps2"/>), and in a bend it is less.
    /// </summary>
    /// <remarks>
    /// <b>Past it the brake buys a slide and costs the corner</b>: the pedal stands well clear of the tyres, and a
    /// car cornering at its share of grip that was let ask for the whole of it braked straight on out of the bend,
    /// ran wide, wound the wheel on for the corner it had lost, and was braked harder for that. <b>The wheel's corner
    /// and not the one the tyres are carrying</b>, because a sliding car carries less than it asks for and would read
    /// its own slide as room to brake. A hazard is not held to this (<see cref="IsAHazard"/>).
    /// </remarks>
    public static float BrakeCeilingMps2(
        SimConfig config, in CarBuild car, float steerRad, float alongMps, float groundCoefficient)
    {
        var acrossMps2 = alongMps * alongMps * MathF.Abs(MathF.Tan(steerRad)) / car.WheelbaseM;
        var leftMps2 = TyreModel.DriveLeftMps2(car.GripMps2 * groundCoefficient, acrossMps2);
        return MathF.Min(car.BrakingMps2, leftMps2) * config.Driving.BrakingMargin;
    }

    /// <summary>Which way a command is leaning, throttle positive and brake negative — what the next tick's pedal travels from.</summary>
    public static float PedalMps2(in DriveCommand command) => command.ThrottleMps2 - command.BrakeMps2;
}
