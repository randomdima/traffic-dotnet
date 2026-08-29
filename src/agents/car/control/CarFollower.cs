using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Agents.Car.Control;

/// <summary>
/// What the driver found in front of it on the road it is driving: how far off it is <em>from the nose</em>,
/// and how fast it is going along this car's own heading — the second of which is the whole difference
/// between a queue that will move and an obstruction that will not.
/// </summary>
/// <remarks>
/// Both come out of the town's own book, in the way's own metres. There is no ray behind it: everything
/// that can be on a lane is a stretch of that lane, so the gap is a subtraction rather than a cast.
/// </remarks>
internal readonly record struct HeadwayReading(float DistanceM, float AlongMps)
{
    public static HeadwayReading Nothing => new(float.PositiveInfinity, 0f);

    public bool Found => float.IsFinite(DistanceM);
}

/// <summary>
/// <b>What the thing in front actually is</b>, which a ray cannot say and the lane index can: a shape at
/// a distance is the same reading whether it is a driver waiting his turn or a wreck.
/// </summary>
/// <remarks>
/// It is a reading and never a decision. What it decides is only whether the way past something is
/// <em>round</em> it: everything but <see cref="Obstruction"/> is waited behind, and the blocked-road
/// clock is what eventually gets a car out from behind a queue that never moves.
/// </remarks>
internal enum HeadwayKind : byte
{
    /// <summary>Nothing in front, or nothing near enough to read.</summary>
    Nothing,

    /// <summary>
    /// A live driver on this car's own path, pointed the way this car is going. <b>A queue however long
    /// it has stood</b> — the car at its head is held by something, and that something is not this car's
    /// to drive round.
    /// </summary>
    Queue,

    /// <summary>
    /// Something on the road that is not going anywhere: a wreck, a car with nobody in it, a body shoved
    /// off its own line. This is the one reading `E-4` may act on.
    /// </summary>
    Obstruction,

    /// <summary>Ground somebody else has claimed and is crossing into — a bay being backed out of, a swerve about to swing.</summary>
    Claimed,

    /// <summary>
    /// <b>A person standing in the lane</b> — on the paint or on bare carriageway, it is the same fact to a
    /// driver. Waited behind while it is moving, and <b>gone round once it has stopped</b>: a walker is an
    /// agent like any other, and what keeps a swerve off one is the body's own stretch of the book rather
    /// than a rule that refuses to look at it (`E-4`).
    /// </summary>
    Walker,

    /// <summary>
    /// Something the lane index does not account for: a walker, the town's furniture, a body off the
    /// network altogether. <b>Never driven round</b>, because what the reading does not name it cannot
    /// justify crossing the centreline to pass.
    /// </summary>
    Unknown,
}

/// <summary>
/// What the driver has been told about the world this tick, over and above the line it is driving:
/// what is in front of it, how fast that is going, and where it must be stopped by.
/// </summary>
/// <param name="HeadwayM">From the nose to whatever the book has in front, or <see cref="float.PositiveInfinity"/> for an empty road.</param>
/// <param name="HeadwaySpeedMps">How fast that thing is going <em>along this car's heading</em> — the whole difference between a queue that will move and an obstruction that will not.</param>
/// <param name="StopAtM">How far ahead along the line the car must be stopped: an unclaimed junction, a stop bar, a red light. Infinite where nothing stops it.</param>
/// <param name="GroundCoefficient">What the surface under it is worth, which scales every grip figure the profile plans against.</param>
/// <param name="CrossingStopM">Where a crossing says to stop short of its paint — somebody on it, or a queue that would leave this car standing on it. Infinite where none does.</param>
/// <param name="CrossingAtM">How far ahead the paint the pace below is for begins. Infinite where there is none within reach.</param>
/// <param name="CrossingPaceMps">What a crossing is approached at whether or not anybody is visible — <b>a pace to arrive at and not a cap to hold from here</b>. Infinite where none applies, which includes a crossing lit and showing its kerbs a red.</param>
/// <param name="Ahead">What the thing <see cref="HeadwayM"/> is about actually is, which decides whether the way past it is round it.</param>
/// <param name="AuthorityM">
/// How far ahead of the nose the lane index cut this car's own ask short, or
/// <see cref="float.PositiveInfinity"/> where nothing cut it — an empty road, a car on a template of its
/// own, or one the road has nothing to say about. <b>It is what makes a queue a queue</b>: no two grants
/// overlap, so the car behind simply has less road to stop in.
/// </param>
/// <param name="GrantCutBy">
/// What cut it, which is what says whether the car is <em>following</em> something or merely stopping short
/// of it. <see cref="HeadwayKind.Nothing"/> where nothing did.
/// </param>
internal readonly record struct DriveContext(
    float HeadwayM, float HeadwaySpeedMps, float StopAtM, float GroundCoefficient,
    float CrossingStopM, float CrossingAtM, float CrossingPaceMps, HeadwayKind Ahead = HeadwayKind.Nothing,
    float AuthorityM = float.PositiveInfinity, HeadwayKind GrantCutBy = HeadwayKind.Nothing)
{
    public DriveContext(float headwayM, float headwaySpeedMps, float stopAtM, float groundCoefficient)
        : this(
            headwayM, headwaySpeedMps, stopAtM, groundCoefficient, float.PositiveInfinity, float.PositiveInfinity,
            float.PositiveInfinity)
    {
    }

    public static DriveContext Clear => new(
        float.PositiveInfinity, 0f, float.PositiveInfinity, 1f, float.PositiveInfinity, float.PositiveInfinity,
        float.PositiveInfinity);
}

/// <summary>
/// Which of the things that limit a car is the one limiting it. Speed is the minimum of everything, and
/// <b>which term won is the only question worth asking of a car that is going slowly</b> — an instrument
/// rather than a rule, and the first piece of the manoeuvre trace the catalogue will want.
/// </summary>
internal enum DrivingHold : byte
{
    /// <summary>Nothing but the gear's own cap.</summary>
    None,

    /// <summary>The corner being driven, or one within braking range ahead.</summary>
    Corner,

    /// <summary>The end of the line it has been given.</summary>
    LineEnd,

    /// <summary>The shape the rays found in front of it — a walker, a wreck, the car it is following.</summary>
    Headway,

    /// <summary>
    /// The ground it was granted to stop in has run out: somebody in front reserved the rest of it.
    /// <b>This is what queueing is</b> — the whole of following, and a speed behaviour rather than a
    /// decision. It binds where the book knows something the rays cannot see: road spoken for round a
    /// bend, across a join, or by a car that is not in the corridor yet.
    /// </summary>
    Reserved,

    /// <summary>A junction it has not been given, a bar or a red.</summary>
    Waiting,

    /// <summary>A crossing ahead: the pace it is approached at (CAR-7b), or somebody on the paint.</summary>
    Crossing,

    /// <summary>
    /// The manoeuvre in charge asked for it — a hold in the mouth of a bay, a stop point of its own, an
    /// emergency. <b>The one term that is a decision rather than a reading</b>, which is why it is named
    /// apart: everything else here is something the road did to the car.
    /// </summary>
    Procedure,

    /// <summary>It is not on its line at all (CAR-9), and what it does about that is the catalogue's.</summary>
    LostLine,
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

    /// <summary>How far off its own line the car is, which is what says whether it is still on it at all.</summary>
    public static float OffLineM(ReadOnlySpan<ArcSeg> line, Vector2 rearAxleM, float progressM) =>
        (Spline.SampleAt(line, progressM).PositionM - rearAxleM).Length();

    /// <summary>One tick of one driver: the line, what is on it, and what the body is doing, into one command.</summary>
    public static DriveDecision Step(
        SimConfig config, in CarBuild car, in CarPose pose, ReadOnlySpan<ArcSeg> line, float progressM,
        float lineLengthM, in DriveContext context, float dtS)
    {
        var forward = pose.Forward;
        var alongMps = Vector2.Dot(pose.VelocityMps, forward);
        var rearAxleM = RearAxleM(car, pose.PositionM, forward);

        var lookaheadM = LookaheadM(car, MathF.Abs(alongMps), config.Driving.LookaheadS);
        var steerRad = Steer(car, line, progressM, rearAxleM, forward, lookaheadM);
        var targetMps = TargetSpeedMps(
            config, car, line, progressM, lineLengthM, steerRad, alongMps, lookaheadM, context, out var hold, out _);

        return new DriveDecision(Pedals(config, car, steerRad, targetMps, alongMps, dtS), hold, targetMps);
    }

    /// <summary>
    /// Pure pursuit: the wheel is turned for the circle through the car's rear axle and a point a
    /// <b>time</b> ahead on the line, floored and ceilinged — too short and the car saws, too long and
    /// it cuts the corner.
    /// </summary>
    public static float Steer(
        in CarBuild car, ReadOnlySpan<ArcSeg> line, float progressM, Vector2 rearAxleM, Vector2 forward,
        float lookaheadM)
    {
        var toLead = Spline.SampleAt(line, progressM + lookaheadM).PositionM - rearAxleM;
        var reachM = toLead.Length();
        if (reachM < 1e-3f) return 0f;

        // The circle through the axle, tangent to the heading, that passes through the lead point: its
        // curvature is 2·sin α ⁄ reach, and the steering angle that holds it is what <em>this car's</em>
        // wheelbase says — the same line asks a long car for more lock than a short one, and past the lock
        // it is asking for a circle the car cannot hold at all (CAR-11).
        var curvature = 2f * Spline.Cross(forward, toLead) / (reachM * reachM);
        return Math.Clamp(MathF.Atan(curvature * car.WheelbaseM), -car.MaxSteerRad, car.MaxSteerRad);
    }

    /// <summary>
    /// <b>How far in front of itself the profile plans</b>: the staleness of the driver's own decision, and
    /// the time the pedal takes to arrive at the rate that decision asked for.
    /// </summary>
    /// <remarks>
    /// <b>Both halves are delays and neither is a margin.</b> A pedal that travels rather than snapping
    /// (<see cref="SimConfig.CarPedalRateMps3"/>) reaches the planned rate after a ramp, and a car that
    /// planned as though it arrived at once brakes over less ground than it asked for and makes up the
    /// difference by braking harder than it planned. The ramp costs half of itself, which is the area a
    /// triangle of it has against the rectangle it is standing in.
    /// </remarks>
    public static float LeadS(SimConfig config, in CarBuild car, float brakingMps2) =>
        config.CarReactionS + (brakingMps2 / (2f * car.PedalRateMps3));

    /// <summary>How far ahead the wheel is aimed: a time, floored at the car's own length and ceilinged.</summary>
    public static float LookaheadM(in CarBuild car, float speedMps, float lookaheadS) =>
        Math.Clamp(speedMps * lookaheadS, car.LookaheadFloorM, car.LookaheadCeilingM);

    /// <summary>
    /// The least of everything that limits a car: the gear's cap, the corner it is in, the corner the
    /// wheel is asking for, every corner within braking range, the end of the line, what is in front of
    /// it, where it must be stopped by, and the ground it was granted to stop in.
    /// </summary>
    /// <param name="plannedMps">
    /// What it would have asked for with the road to itself — every term but the grant. <b>It is the
    /// ceiling on the next reservation</b>, and it is taken here because this is where the terms are: the
    /// road a car holds is bounded by the speed it is driving towards as well as by the one it can reach
    /// before the next decision.
    /// </param>
    public static float TargetSpeedMps(
        SimConfig config, in CarBuild car, ReadOnlySpan<ArcSeg> line, float progressM, float lineLengthM,
        float steerRad, float alongMps, float lookaheadM, in DriveContext context, out DrivingHold hold,
        out float plannedMps)
    {
        hold = DrivingHold.None;
        var lateralMps2 = car.GripMps2 * context.GroundCoefficient * config.Driving.GripMargin;
        var brakingMps2 = BrakingMps2(config, car, context.GroundCoefficient);
        var leadM = MathF.Abs(alongMps) * LeadS(config, car, brakingMps2);

        var targetMps = car.MaxSpeedMps;
        Bind(ref targetMps, CornerMps(MathF.Tan(steerRad) / car.WheelbaseM, lateralMps2), DrivingHold.Corner, ref hold);

        // Every corner within braking range, each read as the speed this car may be doing *here* and
        // still be down to that corner's own speed by the time it arrives at it. The line is walked a
        // piece at a time and not a sample at a time: a piece is one constant curvature by
        // construction, so one corner speed per arc is the whole answer and costs the arc rather than
        // the metre.
        var rangeM = alongMps * alongMps / (2f * brakingMps2) + leadM;
        var startM = 0f;
        foreach (var arc in line)
        {
            var endM = startM + arc.LengthM;
            if (endM >= progressM)
            {
                var aheadM = MathF.Max(0f, startM - progressM);
                if (aheadM > rangeM) break;

                // A corner is reached by the *lead point* before it is reached by the car, and the
                // wheel is already turning into it by then. Counting the lookahead as well as the
                // reaction lead is what stops a car arriving at the corner speed a lookahead too late,
                // which is a car on the pavement at the exit of every tight bend.
                Bind(
                    ref targetMps,
                    ApproachMps(CornerMps(arc.Curvature, lateralMps2), aheadM - leadM - lookaheadM, brakingMps2),
                    DrivingHold.Corner, ref hold);
            }

            startM = endM;
        }

        Bind(ref targetMps, ApproachMps(0f, lineLengthM - progressM - leadM, brakingMps2), DrivingHold.LineEnd, ref hold);
        Bind(ref targetMps, ApproachMps(0f, context.StopAtM - leadM, brakingMps2), DrivingHold.Waiting, ref hold);

        // CAR-7b: the stop short of the paint, and the pace to <em>arrive at</em> it at. Both are the
        // crossing's own term rather than the junction's, because what a driver owes somebody on the
        // paint is a stop point on this car's own line and not a claim on a box — and the pace is read
        // like a corner, so a car three streets from a zebra is not driving at zebra pace.
        Bind(ref targetMps, ApproachMps(0f, context.CrossingStopM - leadM, brakingMps2), DrivingHold.Crossing, ref hold);
        Bind(
            ref targetMps, ApproachMps(context.CrossingPaceMps, context.CrossingAtM - leadM, brakingMps2),
            DrivingHold.Crossing, ref hold);

        // <b>The gap to a shape, which is a different measurement from the grant below and not a second
        // gate on it</b> (SIM-7). The book holds every body as an interval of the way's own arclength,
        // which follows the road round every bend but carries no width and no angle — so what it cannot
        // say is how near the *shape* of a car mid-turn, one straddling a join or one cutting a corner
        // actually is, and a walker is in no such book at all.
        // Suppressing this wherever the index had a name for what was ahead cost 290 emergency stops in a
        // minute of Odesa.
        var gapM = context.HeadwayM - car.HalfLengthM - leadM;
        Bind(
            ref targetMps, ApproachMps(MathF.Max(0f, context.HeadwaySpeedMps), gapM, brakingMps2),
            DrivingHold.Headway, ref hold);

        // The road to itself, which is the figure the next reservation is asked for at — before the grant
        // is folded in, and never after it.
        plannedMps = MathF.Max(0f, targetMps);

        // <b>And the grant, which is the whole of following.</b> The ground the index gave this car to
        // stop in inverts straight into a speed: what may be held here to be at rest by the far end of it.
        // A car in front is credited with the ground it will have vacated, because its own reservation
        // begins where it will have stopped and not where it is.
        //
        // <b>Read at a following time and not at the reaction lead</b>, which is the one term here that is
        // — every other distance is measured from where the car will be when it next decides, and this one
        // is measured from the gap it means to keep. The braking figure cancels out of the equilibrium
        // (the car in front was credited out of the same arithmetic), so what a queue settles at is the
        // standstill gap and a second of travel, and nothing else.
        //
        // <b>And a following time is kept from what is being followed, and from nothing else.</b> A grant cut
        // at a wreck, at somebody on foot, at ground somebody has claimed or at a crossing point already ends
        // the asker's own margin short of it (<c>LaneCredit.AtAPlaceM</c>) — none of them is a body to keep
        // station behind, and a second of travel on top of that margin is a car holding a street shut at
        // speed for something it needed only to stop short of.
        var followingM = context.GrantCutBy == HeadwayKind.Queue
            ? MathF.Abs(alongMps) * config.Driving.FollowingHeadwayS
            : 0f;
        Bind(
            ref targetMps, ApproachMps(0f, context.AuthorityM - followingM, brakingMps2),
            DrivingHold.Reserved, ref hold);

        return MathF.Max(0f, targetMps);
    }

    /// <summary>
    /// What the car may actually brake at on the ground it is on: the pedal's own bound, or what the tyres
    /// can put down along the roll, whichever gives out first — and nearly all of that
    /// (<see cref="DrivingFigures.BrakingMargin"/>), because a stop is the one manoeuvre a driver aims the
    /// whole car at.
    /// </summary>
    /// <remarks>
    /// <b>It is a figure along the roll and takes no notice of what the wheel is doing.</b> A driver
    /// slowing into a corner spends the whole of it on top of the lateral demand the corner is already
    /// making, and the tyres answer to one ellipse — so where a fast corner follows a fast approach the
    /// combined ask is over the budget and the car drifts. The proving ground's long arc is where it shows:
    /// <c>--bench track</c> reads a metre and a half off the line there against a tenth on every other shape.
    /// </remarks>
    public static float BrakingMps2(SimConfig config, in CarBuild car, float groundCoefficient) =>
        car.UtmostBrakingMps2(groundCoefficient) * config.Driving.BrakingMargin;

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
    /// <param name="lastMps2">
    /// What the pedal was asking for last tick, throttle positive and brake negative — <c>0</c> for a
    /// caller with no previous command, which is a foot starting from neither pedal.
    /// </param>
    public static DriveCommand Pedals(
        SimConfig config, in CarBuild car, float steerRad, float targetMps, float alongMps, float dtS,
        float lastMps2 = 0f)
    {
        if (targetMps <= config.Driving.StopSpeedMps && MathF.Abs(alongMps) <= config.Driving.StopSpeedMps)
        {
            return new DriveCommand(steerRad, 0f, 0f, Handbrake: true, Reverse: false);
        }

        var travelMps2 = car.PedalRateMps3 * dtS;
        var wantedMps2 = Math.Clamp((targetMps - alongMps) / dtS, lastMps2 - travelMps2, lastMps2 + travelMps2);

        return wantedMps2 >= 0f
            ? new DriveCommand(steerRad, MathF.Min(wantedMps2, car.AccelerationMps2), 0f, false, false)
            : new DriveCommand(steerRad, 0f, MathF.Min(-wantedMps2, car.BrakingMps2), false, false);
    }

    /// <summary>Which way a command is leaning, throttle positive and brake negative — what the next tick's pedal travels from.</summary>
    public static float PedalMps2(in DriveCommand command) => command.ThrottleMps2 - command.BrakeMps2;
}
