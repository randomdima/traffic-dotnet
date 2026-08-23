using System.Numerics;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Car.Body;

/// <summary>
/// Every car in the town, as one array per field — including the line each one is driving, which is a
/// flat run of arcs with a count beside it and not a path object per car.
/// </summary>
/// <remarks>
/// <para>
/// A car is an index, as a walker is (<see cref="Person.Body.PersonFleet"/>): the same reasons hold,
/// and one more. <b>A car's line is laid into the roster's own arcs</b> at a fixed budget per car, so
/// re-laying a line when a car reaches the next junction writes into memory the roster already owns and
/// the steady state allocates nothing however often the town turns a corner.
/// </para>
/// <para>
/// <b>Pose is mirrored out of the solver once a tick</b>, after the step. A car reads its own arrays
/// for the rest of the tick, and the acceleration kept here is the one the tyres weigh their loads
/// with — measured, from the two poses either side of a step, rather than the one the pedals asked for.
/// </para>
/// </remarks>
internal sealed class CarFleet
{
    readonly int _arcsPerCar;

    public CarFleet(int capacity, int arcsPerCar)
    {
        _arcsPerCar = arcsPerCar;
        Body = new BodyId[capacity];
        PositionM = new Vector2[capacity];
        HeadingRad = new float[capacity];
        VelocityMps = new Vector2[capacity];
        YawRateRadPerS = new float[capacity];
        AccelerationMps2 = new Vector2[capacity];
        MassKg = new float[capacity];
        Variant = new byte[capacity];
        Draw = new Rng[capacity];
        Driven = new bool[capacity];
        Broken = new bool[capacity];
        LaneChain = new int[capacity * PathAssembler.MostLanes];
        LaneStartM = new float[capacity * PathAssembler.MostLanes];
        LaneEndM = new float[capacity * PathAssembler.MostLanes];
        ProgressM = new float[capacity];
        AlongMps = new float[capacity];
        OffLineM = new float[capacity];
        ToTheBoxM = new float[capacity];
        Array.Fill(ToTheBoxM, float.PositiveInfinity);
        BoxIsOurs = new bool[capacity];
        SinceDecisionS = new float[capacity];
        Line = new DrivenLine[capacity];
        LineArcs = new ArcSeg[capacity * arcsPerCar];
        Crossing = new int[capacity];
        Array.Fill(Crossing, NoMovement);
        ClaimWay = new int[capacity];
        Array.Fill(ClaimWay, NoWay);
        ClaimFromM = new float[capacity];
        ClaimToM = new float[capacity];
        ReserveFromM = new float[capacity];
        ReserveToM = new float[capacity];
        AuthorityM = new float[capacity];
        Array.Fill(AuthorityM, float.PositiveInfinity);
        PlannedMps = new float[capacity];
        GroundCoefficient = new float[capacity];
        Command = new DriveCommand[capacity];
        Hold = new Control.DrivingHold[capacity];
        Context = new Control.DriveContext[capacity];
        Array.Fill(Context, Control.DriveContext.Clear);
        RouteLanes = new int[capacity * RouteLanesPerCar];
        RouteCount = new int[capacity];
        RouteTaken = new int[capacity];
        DestinationM = new Vector2[capacity];
        HasDestination = new bool[capacity];
        Doing = new Maneuver[capacity];
        Suspended = new Maneuver[capacity];
        Was = new Maneuver[capacity];
        About = new int[capacity];
        Limits = new DriveLimits[capacity];
        Array.Fill(Limits, DriveLimits.None);
        InManeuverS = new float[capacity];
        BlockedS = new float[capacity];
        HeldBackS = new float[capacity];
        Rung = new int[capacity];
        BackOffs = new byte[capacity];
        Reroutes = new byte[capacity];
        Recoveries = new byte[capacity];
        FuseJitter = new float[capacity];
        ClimbedFromM = new Vector2[capacity];
        ChangedAtM = new Vector2[capacity];
        LineIsReverse = new bool[capacity];
        InsideTheBox = new bool[capacity];
        LightAheadM = new float[capacity];
        Array.Fill(LightAheadM, float.PositiveInfinity);
        WaitedS = new float[capacity];
        WheelSpinMps = new float[capacity * TyreModel.Wheels];
        TreadPhaseM = new float[capacity * TyreModel.Wheels];
        ScrubTravelM = new float[capacity * TyreModel.Wheels];
        MarkFromM = new Vector2[capacity * TyreModel.Wheels];
        MarkIntensity = new float[capacity * TyreModel.Wheels];
        Marking = new bool[capacity * TyreModel.Wheels];
        SlipThrottle = new float[capacity];
        Array.Fill(SlipThrottle, 1f);
        DrivenSlipping = new bool[capacity];
        DrivenFrontShare = new float[capacity];
    }

    /// <summary>
    /// How much of a route a car carries at once. <b>A bound on the work and not a figure behaviour
    /// reads</b>: a route longer than this is planned again from where the car has got to, which is what
    /// the planner does anyway when a way it was given is priced up under it.
    /// </summary>
    public const int RouteLanesPerCar = 64;

    public int Count { get; private set; }

    public int Capacity => Body.Length;

    public BodyId[] Body { get; }

    /// <summary>The middle of the body. Every <em>line</em> is the rear axle's, which is that less half a wheelbase along the heading.</summary>
    public Vector2[] PositionM { get; }

    /// <summary>Solver output, unlike a walker's: a car is a box that turns because its tyres turned it.</summary>
    public float[] HeadingRad { get; }

    public Vector2[] VelocityMps { get; }

    public float[] YawRateRadPerS { get; }

    /// <summary>What the body actually did last tick, in its own frame — the loads the tyres are weighed by.</summary>
    public Vector2[] AccelerationMps2 { get; }

    public float[] MassKg { get; }

    public byte[] Variant { get; }

    public Rng[] Draw;

    /// <summary>CAR-1: a car acts only while it contains a driver. Without one it is an inert dynamic object holding its handbrake.</summary>
    public bool[] Driven { get; }

    /// <summary>
    /// PHY-3's terminal state for a car, which is the whole of what damage does to one: broken, never
    /// driven again, and never removed (PHY-5). A wreck keeps its body and its shape, stays dynamic and
    /// is pushed like anything else — with all four of its wheels locked, so a shunted one skids as a
    /// block rather than rolling away.
    /// </summary>
    public bool[] Broken { get; }

    /// <summary>The run of lanes the line is laid over, nearest first. A car's current lane is the first of them.</summary>
    public int[] LaneChain { get; }

    /// <summary>Where each lane of the chain begins along the line, and where it ends — between the two is the junction.</summary>
    public float[] LaneStartM { get; }

    public float[] LaneEndM { get; }

    /// <summary>How far along its own line the rear axle is.</summary>
    public float[] ProgressM { get; }

    /// <summary>
    /// How fast the body is going <b>along the direction its line is driven in</b>, which for a template
    /// taken in reverse is the way the car is not pointing. The sensing half of the tick works it out
    /// and the catalogue reads it, so an entry never has to know which gear its line is in to ask
    /// whether the car is moving.
    /// </summary>
    public float[] AlongMps { get; }

    /// <summary>How far off its own line the rear axle is, which is what says whether the car is still on it (CAR-9).</summary>
    public float[] OffLineM { get; }

    /// <summary>
    /// How far ahead the junction box this car's own line enters stands, and whether it is this car's to
    /// enter. <b>A fact about the geometry and not about the lane under the car</b>: mid-turn the nearest
    /// lane is already the one leading out.
    /// </summary>
    public float[] ToTheBoxM { get; }

    public bool[] BoxIsOurs { get; }

    /// <summary>
    /// How long since this driver last ran its procedure. <b>The decision's own elapsed time and not the
    /// loop's nominal interval</b>: an entry that declares itself unschedulable is asked on every tick,
    /// and handing it a whole interval each time would run every clock inside the catalogue at six times
    /// real time.
    /// </summary>
    public float[] SinceDecisionS { get; }

    public DrivenLine[] Line { get; }

    /// <summary>
    /// The way through a junction this car is crossing on, as the turn it is, or <see cref="NoMovement"/>.
    /// <b>At most one</b>: the one behind is dropped as soon as the car is queueing for the next.
    /// </summary>
    /// <remarks>
    /// <b>It is the name of a reservation and not a permission.</b> What the car actually holds is ground —
    /// the section of every other join at that junction its own line is driven over
    /// (<see cref="World.Road.JunctionCrossings"/>) — laid into the road's book from this field every tick,
    /// exactly as <see cref="ClaimWay"/> is. So two cars crossing one junction without being driven over
    /// each other's ground take one each, and nothing about a junction is refused by anything other than
    /// what is standing on the metres wanted.
    /// </remarks>
    public int[] Crossing { get; }

    /// <summary>
    /// <b>The stretch of road this car has claimed and is not on yet</b> — the way it is on, and the two
    /// metres along it. At most one, and <see cref="NoWay"/> where the car is claiming nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is a claim on <em>ground the body is about to cross into</em> and never on the road ahead of a
    /// car already driving down it: what holds a car off the queue in front of it is the headway term, and
    /// a claim that restated it would be a second gate on the same movement (SIM-7). The two entries that
    /// need one are the two that put a body somewhere the road did not send it — backing out of a bay, and
    /// swinging round something in the lane.
    /// </para>
    /// <para>
    /// <b>It is a field and not a register</b>, re-laid into the index from here every tick. A claim
    /// therefore cannot outlive the car that made it, cannot leak when one is wrecked, and needs nothing
    /// released: an entry that stops wanting it writes <see cref="NoWay"/> and it is gone the next tick.
    /// </para>
    /// </remarks>
    public int[] ClaimWay { get; }

    public float[] ClaimFromM { get; }

    public float[] ClaimToM { get; }

    /// <summary>
    /// <b>The stretch of its own line this car is committed to</b>, from its own tail to where its nose
    /// comes to rest if it holds this pedal until its next decision and then stops. In the line's metres,
    /// and meaningful only while the index is being laid — the grant taken off it is <see cref="AuthorityM"/>.
    /// </summary>
    public float[] ReserveFromM { get; }

    public float[] ReserveToM { get; }

    /// <summary>
    /// <b>How far ahead of its nose the car was granted room to stop</b> — its own asked-for stretch cut at
    /// the near edge of the nearest one already spoken for. Infinite where nothing cut it: an empty road,
    /// a car on a template of its own, or one that is not under way at all.
    /// </summary>
    /// <remarks>
    /// It is a distance from the nose and is walked in by the ground covered since it was granted, exactly
    /// as a manoeuvre's stop point is (<see cref="DriveLimits.Carried"/>): a car that held it unchanged
    /// while driving at it would be holding a point receding at its own speed. Negative where the car is
    /// already inside ground somebody else has, which is a fact about a contact and not about a gap.
    /// </remarks>
    public float[] AuthorityM { get; }

    /// <summary>
    /// What the speed profile would have asked for with the road to itself — every term but the grant. It
    /// is the <em>ceiling</em> on what the next reservation is sized by: a car may be committed to no more
    /// road than it is going to drive over, and never to the road its top speed would need whether or not
    /// it is anywhere near it.
    /// </summary>
    public float[] PlannedMps { get; }

    public float[] GroundCoefficient { get; }

    /// <summary>What the driver asked for this tick, kept for the tyres, the debug layer and whoever asks what the car is doing.</summary>
    public DriveCommand[] Command { get; }

    /// <summary>Which of the things that limit a car limited this one — an instrument, and the only useful question about a slow car.</summary>
    public Control.DrivingHold[] Hold { get; }

    /// <summary>
    /// What the driver was told about the world this tick — what the book has ahead of it and where
    /// it must be stopped by.
    /// </summary>
    /// <remarks>
    /// It is kept for the same reason <see cref="Hold"/> is: <b>a debug layer that worked either of them
    /// out for itself would be drawing its own arithmetic beside the car rather than the car's</b>, and
    /// the two agree right up until one of them is changed. Cleared to
    /// <see cref="Control.DriveContext.Clear"/> for a car that took no decision this tick.
    /// </remarks>
    public Control.DriveContext[] Context { get; }

    /// <summary>
    /// The lanes of the route still to be driven, nearest first — the run the planner returned, expanded
    /// into the lanes the line is laid over. <b>The chain is the front of this and the queue is the
    /// rest</b>, so a car that has been given a route never draws a turn.
    /// </summary>
    public int[] RouteLanes { get; }

    public int[] RouteCount { get; }

    /// <summary>How many of them have already been laid into the chain.</summary>
    public int[] RouteTaken { get; }

    /// <summary>Where this car is going. A place in the town, and not a node: a destination always is.</summary>
    public Vector2[] DestinationM { get; }

    public bool[] HasDestination { get; }

    /// <summary>
    /// <b>Which entry of the closed catalogue this car is in</b> (AGT-7). A car has no goals of its own
    /// (CAR-8), so every one of these is a bounded step of somebody's trip — and a car with nobody in it
    /// is in none of them.
    /// </summary>
    public Maneuver[] Doing { get; }

    /// <summary>
    /// The planned manoeuvre a reactive one interrupted, to be <b>re-entered through its own
    /// <c>Sa</c></b> and never resumed mid-procedure.
    /// </summary>
    public Maneuver[] Suspended { get; }

    /// <summary>What it was doing before that, which is what says a pair is passing it back and forth.</summary>
    public Maneuver[] Was { get; }

    /// <summary>
    /// What the entry in charge is <em>about</em>: the bay being left, the bay being parked in. The
    /// plan's own parameter for the step being driven, carried here so an entry that hands over to
    /// another about the same thing — `P-14` after `P-16` — hands the subject over with it.
    /// </summary>
    public int[] About { get; }

    /// <summary>
    /// What the entry in charge last asked of the car, which stands until the driver thinks again. <b>A
    /// bounded staleness rather than an unrenewed command</b>: a limit here is at most one decision
    /// interval old, and that interval is a stated figure.
    /// </summary>
    public DriveLimits[] Limits { get; }

    /// <summary>How long this car has been in the manoeuvre it is in — MAN-4's bound, for every entry that carries a time.</summary>
    public float[] InManeuverS { get; }

    /// <summary>
    /// The blocked-road clock: how long this car has stood still <b>with no lawful cause</b>. Waiting at
    /// a red, yielding and waiting in a bay for a gap all spend nothing, because standing still for a
    /// reason the car can see is waiting rather than being stuck.
    /// </summary>
    public float[] BlockedS { get; }

    /// <summary>
    /// The other patience: how long this car has been held <b>below the pace the road affords</b> by
    /// something slow in front of it. <see cref="BlockedS"/> cannot answer that — a car crawling behind a
    /// body reeling down its lane is never standing still, so no clock it keeps ever runs out.
    /// </summary>
    public float[] HeldBackS { get; }

    /// <summary>Where on the escalation ladder this car has climbed to. <b>It rewinds on road covered, never on manoeuvres completed.</b></summary>
    public int[] Rung { get; }

    /// <summary>Attempts spent on the back-off in this jam, and on the reroute and the recoveries in this leg.</summary>
    public byte[] BackOffs { get; }

    public byte[] Reroutes { get; }

    public byte[] Recoveries { get; }

    /// <summary>
    /// This car's own share of every timeout, drawn once when it joins the roster. <b>Two cars jammed
    /// against each other move in lockstep and stay jammed</b> without it.
    /// </summary>
    public float[] FuseJitter { get; }

    /// <summary>Where the car stood when it started climbing the ladder, which is what road covered is measured from.</summary>
    public Vector2[] ClimbedFromM { get; }

    /// <summary>And where it stood when its manoeuvre last changed, which is what "in one spot" means to the trace.</summary>
    public Vector2[] ChangedAtM { get; }

    /// <summary>
    /// Whether the line in hand is driven backwards. <b>A property of the line and not of the car</b>:
    /// the reverse-out template, the back-off's straight and the reverse-in template are laid in the
    /// direction the rear axle travels, and the follower steers against it.
    /// </summary>
    public bool[] LineIsReverse { get; }

    /// <summary>
    /// Whether the body is <em>in</em> the junction box rather than approaching one, which is what
    /// decides the fuse it is watched on. <b>Waiting at a boundary is not standing across a lane.</b>
    /// </summary>
    public bool[] InsideTheBox { get; }

    /// <summary>
    /// How far ahead stands the light showing this car anything but green, or infinity where there is
    /// none. <b>A car queueing for a light spends neither clock</b>, and this is the whole of how the
    /// watchdog knows one.
    /// </summary>
    public float[] LightAheadM { get; }

    /// <summary>
    /// How long this car has been waiting for the gap it needs, which is what the give-way patience is
    /// spent against. <b>It starts below zero</b>: the short random beat `P-2` takes before looking at
    /// all is what stops two neighbouring bays taking the same gap.
    /// </summary>
    public float[] WaitedS { get; }

    public Span<int> RouteOf(int car) => RouteLanes.AsSpan(car * RouteLanesPerCar, RouteLanesPerCar);

    /// <summary>
    /// The next lane the route says to take, <b>without taking it</b> — which is what says whether the
    /// leg is about to reverse direction, and so whether `P-11` is the movement being made.
    /// </summary>
    public int PeekNextRouteLane(int car) =>
        RouteTaken[car] >= RouteCount[car] ? NoLane : RouteLanes[(car * RouteLanesPerCar) + RouteTaken[car]];

    /// <summary>The next lane the route says to take, or <see cref="NoLane"/> where the route has run out.</summary>
    public int TakeNextRouteLane(int car)
    {
        if (RouteTaken[car] >= RouteCount[car]) return NoLane;

        return RouteLanes[(car * RouteLanesPerCar) + RouteTaken[car]++];
    }

    public void ClearRoute(int car)
    {
        RouteCount[car] = 0;
        RouteTaken[car] = 0;
    }

    /// <summary>
    /// Each wheel's own rotation, as the speed its tread runs over the ground — four to a car, in the
    /// order the tyre model works them. It is the state the whole rolling model turns on: the daylight
    /// between it and the road under the patch is what says whether a wheel is spinning, locked, or
    /// simply rolling.
    /// </summary>
    public float[] WheelSpinMps { get; }

    /// <summary>How far each tyre's pattern has scrolled, wrapped into one pitch. Drawing only — nothing in the model reads it.</summary>
    public float[] TreadPhaseM { get; }

    /// <summary>How far each tyre has dragged in the slide it is in, capped at the onset distance.</summary>
    public float[] ScrubTravelM { get; }

    /// <summary>Where the stretch of mark being laid started, how dark it is, and whether that wheel is marking at all.</summary>
    public Vector2[] MarkFromM { get; }

    public float[] MarkIntensity { get; }

    public bool[] Marking { get; }

    /// <summary>
    /// What is left of the throttle after backing off for driven wheels that are already past what
    /// they can put down — traction control, worked off the slide the tyres themselves report rather
    /// than off anything the driver has to guess at. A hand at the wheel gets none of it.
    /// </summary>
    public float[] SlipThrottle { get; }

    /// <summary>Whether a wheel the engine is turning was past its budget last tick.</summary>
    public bool[] DrivenSlipping { get; }

    /// <summary>Where this car's drive is placed: 1 front, 0 rear, ½ all four — the variant's own layout.</summary>
    public float[] DrivenFrontShare { get; }

    public Span<float> WheelSpinOf(int car) => WheelSpinMps.AsSpan(car * TyreModel.Wheels, TyreModel.Wheels);

    /// <summary>
    /// Whether all four of this car's wheels are at a standstill and carrying nothing over: not
    /// turning, not part-way through a mark, and with no scrub banked toward one.
    /// </summary>
    /// <remarks>
    /// It is the second half of the question a caller asks before skipping the tyres altogether — the
    /// first half being that the body itself is not moving — and it exists because the four wheels of a
    /// standing car are <em>state</em>, and a wheel still holding some of it is a wheel with something
    /// left to do.
    /// </remarks>
    public bool WheelsAtRest(int car)
    {
        for (var wheel = car * TyreModel.Wheels; wheel < (car + 1) * TyreModel.Wheels; wheel++)
        {
            if (WheelSpinMps[wheel] != 0f || Marking[wheel] || ScrubTravelM[wheel] > 0f) return false;
        }

        return true;
    }

    public Span<ArcSeg> LineArcsOf(int car) => LineArcs.AsSpan(car * _arcsPerCar, _arcsPerCar);

    public ReadOnlySpan<ArcSeg> LineOf(int car) => LineArcs.AsSpan(car * _arcsPerCar, Line[car].ArcCount);

    public Span<int> ChainOf(int car) => LaneChain.AsSpan(car * PathAssembler.MostLanes, PathAssembler.MostLanes);

    public Span<float> LaneStartsOf(int car) => LaneStartM.AsSpan(car * PathAssembler.MostLanes, PathAssembler.MostLanes);

    public Span<float> LaneEndsOf(int car) => LaneEndM.AsSpan(car * PathAssembler.MostLanes, PathAssembler.MostLanes);

    /// <summary>The lane the car is on, which is the first of its chain — or <see cref="NoLane"/> when it is on none.</summary>
    public int LaneOf(int car) => Line[car].LaneCount > 0 ? LaneChain[car * PathAssembler.MostLanes] : NoLane;

    /// <param name="drivenFrontShare">
    /// Which end this car drives through, as the share of the drive placed on the front axle. It is
    /// the <em>variant's</em> — the fleet's own layouts — and the one per-variant figure this engine
    /// reads, because unlike a footprint or a wheelbase it changes how a car behaves without changing
    /// what it is drawn as.
    /// </param>
    public int Add(BodyId body, Vector2 positionM, float headingRad, float massKg, byte variant, float drivenFrontShare, Rng draw)
    {
        if (Count == Capacity) throw new InvalidOperationException($"The roster was laid for {Capacity} cars and is full.");

        var car = Count++;
        Body[car] = body;
        PositionM[car] = positionM;
        HeadingRad[car] = headingRad;
        VelocityMps[car] = Vector2.Zero;
        YawRateRadPerS[car] = 0f;
        AccelerationMps2[car] = Vector2.Zero;
        MassKg[car] = massKg;
        Variant[car] = variant;
        DrivenFrontShare[car] = drivenFrontShare;
        Draw[car] = draw;
        Driven[car] = false;
        Broken[car] = false;
        ProgressM[car] = 0f;
        AlongMps[car] = 0f;
        OffLineM[car] = 0f;
        ToTheBoxM[car] = float.PositiveInfinity;
        BoxIsOurs[car] = false;
        SinceDecisionS[car] = 0f;
        Line[car] = default;
        Crossing[car] = NoMovement;
        ClaimWay[car] = NoWay;
        AuthorityM[car] = float.PositiveInfinity;
        PlannedMps[car] = 0f;
        GroundCoefficient[car] = 1f;
        Command[car] = DriveCommand.Parked;
        Hold[car] = Control.DrivingHold.None;
        RouteCount[car] = 0;
        RouteTaken[car] = 0;
        HasDestination[car] = false;
        Doing[car] = Maneuver.None;
        Suspended[car] = Maneuver.None;
        Was[car] = Maneuver.None;
        About[car] = -1;
        Limits[car] = DriveLimits.None;
        InManeuverS[car] = 0f;
        BlockedS[car] = 0f;
        HeldBackS[car] = 0f;
        Rung[car] = 0;
        BackOffs[car] = 0;
        Reroutes[car] = 0;
        Recoveries[car] = 0;

        // Drawn from the car's own stream, so the jitter is the town's seed and not the clock's.
        FuseJitter[car] = Draw[car].NextFloat(1f - FuseJitterShare, 1f + FuseJitterShare);
        ClimbedFromM[car] = positionM;
        ChangedAtM[car] = positionM;
        LineIsReverse[car] = false;
        InsideTheBox[car] = false;
        LightAheadM[car] = float.PositiveInfinity;
        WaitedS[car] = 0f;
        SlipThrottle[car] = 1f;
        DrivenSlipping[car] = false;
        for (var wheel = car * TyreModel.Wheels; wheel < (car + 1) * TyreModel.Wheels; wheel++)
        {
            WheelSpinMps[wheel] = 0f;
            TreadPhaseM[wheel] = 0f;
            ScrubTravelM[wheel] = 0f;
            MarkFromM[wheel] = positionM;
            MarkIntensity[wheel] = 0f;
            Marking[wheel] = false;
        }

        return car;
    }

    /// <summary>
    /// How far either side of a bound a car's own fuse falls. A fifth: enough that two cars jammed
    /// against one another give up on different ticks, and not so much that a bound stops meaning what
    /// it says.
    /// </summary>
    const float FuseJitterShare = 0.2f;

    /// <summary>A car that is on no lane at all — parked, or shoved off the network and recovering.</summary>
    public const int NoLane = -1;

    /// <summary>A car that has been given no way through a junction — which is every car not approaching or inside one.</summary>
    public const int NoMovement = -1;

    /// <summary>A car claiming no stretch of road — which is every car that is not crossing into a lane it was not sent down.</summary>
    public const int NoWay = -1;

    ArcSeg[] LineArcs { get; }
}
