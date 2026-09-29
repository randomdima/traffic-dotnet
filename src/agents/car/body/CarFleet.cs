using System.Numerics;
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

    readonly CarBuilds _builds;

    public CarFleet(int capacity, int arcsPerCar, CarBuilds builds)
    {
        _arcsPerCar = arcsPerCar;
        _builds = builds;
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
        Ambulance = new bool[capacity];
        BlueLight = new bool[capacity];
        AtWork = new bool[capacity];
        LaneChain = new int[capacity * LineAssembler.MostLanes];
        LaneStartM = new float[capacity * LineAssembler.MostLanes];
        LaneEndM = new float[capacity * LineAssembler.MostLanes];
        ProgressM = new float[capacity];
        AlongMps = new float[capacity];
        OffLineM = new float[capacity];
        ToTheBoxM = new float[capacity];
        Array.Fill(ToTheBoxM, float.PositiveInfinity);
        TurningAtTheBox = new bool[capacity];
        BoxIsOurs = new bool[capacity];
        CommittedToTheBox = new bool[capacity];
        SinceDecisionS = new float[capacity];
        Line = new DrivenLine[capacity];
        LineArcs = new ArcSeg[capacity * arcsPerCar];
        LineWay = new int[capacity];
        Array.Fill(LineWay, NoWay);
        TailWay = new int[capacity];
        Array.Fill(TailWay, NoWay);
        TurnsBackOn = new int[capacity];
        Array.Fill(TurnsBackOn, NoLane);
        ClaimFromM = new float[capacity];
        ClaimToM = new float[capacity];
        CommittedToM = new float[capacity];
        AuthorityM = new float[capacity];
        Array.Fill(AuthorityM, float.PositiveInfinity);
        GrantMarginM = new float[capacity];
        GrantCutBy = new Control.HeadwayKind[capacity];
        PlannedMps = new float[capacity];
        PaceMps = new float[capacity];
        Array.Fill(PaceMps, float.PositiveInfinity);
        GroundCoefficient = new float[capacity];
        Command = new DriveCommand[capacity];
        Hold = new Control.DrivingHold[capacity];
        Context = new Control.DriveContext[capacity];
        Array.Fill(Context, Control.DriveContext.Clear);
        RouteLanes = new int[capacity * RouteLanesPerCar];
        RouteCount = new int[capacity];
        RouteTaken = new int[capacity];
        RouteRunsOut = new bool[capacity];
        DestinationM = new Vector2[capacity];
        HasDestination = new bool[capacity];
        Reroutes = new byte[capacity];
        FuseJitter = new float[capacity];
        BacksIntoBays = new bool[capacity];
        LineIsReverse = new bool[capacity];
        InsideTheBox = new bool[capacity];
        LightAheadM = new float[capacity];
        Array.Fill(LightAheadM, float.PositiveInfinity);
        Pass = new Control.Overtake[capacity];
        Array.Fill(Pass, Control.Overtake.None);
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

    /// <summary>
    /// CAR-1: a car acts only while something is driving it — an errand, an order or the tour of a map with
    /// no bay to reach. Set by whatever sends it and cleared where it is stood down; nobody sitting in it
    /// sets it. Without it the car is an inert dynamic object holding its handbrake.
    /// </summary>
    public bool[] Driven { get; }

    /// <summary>
    /// PHY-3's terminal state for a car, which is the whole of what damage does to one: broken, never
    /// driven again, and never removed (PHY-5). A wreck keeps its body and its shape, stays dynamic and
    /// is pushed like anything else — with all four of its wheels locked, so a shunted one skids as a
    /// block rather than rolling away.
    /// </summary>
    public bool[] Broken { get; }

    /// <summary>
    /// <b>AMB-3: whether this car is an ambulance</b> — a fact about the car and never about what it is
    /// doing. It is drawn from no catalogue and changes for one reason only, a wreck mended in a yard
    /// (EVA-7): an ambulance is one from the tick the town is stood up, whether or not anybody has been run
    /// over yet.
    /// </summary>
    public bool[] Ambulance { get; }

    /// <summary>
    /// <b>And whether it is answering a call</b> (AMB-4). This is the whole of the difference a call makes
    /// to the road: what claims its ground at <c>ClaimPriority.Special</c> — above a light's hold, so no red
    /// holds it — and what holds the car to the call's own pace.
    /// </summary>
    /// <remarks>
    /// <b>It is the errand's and not the vehicle's</b>: an ambulance on a call, an evacuator on its way to a
    /// wreck (EVA-4), a police car on its way to a scene (SRV-6) — and the idle map's escort, which is stood
    /// with it lit. It goes out with the leg that carries it: an ambulance driving home is ordinary traffic,
    /// and one that kept its priority between calls would be a town where a whole lane belongs to a parked van.
    /// </remarks>
    public bool[] BlueLight { get; }

    /// <summary>
    /// <b>Whether this vehicle is out on the job it exists for</b> (CAR-14.6) — an evacuator from the tick
    /// it takes a wreck until it is back in its own bay, both ways round.
    /// </summary>
    /// <remarks>
    /// <b>It is the work and not the priority</b>, which is the whole reason it is a second fact: a truck
    /// hauling a wreck home is ordinary traffic (EVA-4) and is still a truck working in the street. Nothing
    /// on the road reads it — it buys no ground and orders nobody — and the only thing that does is the
    /// amber bar.
    /// </remarks>
    public bool[] AtWork { get; }

    /// <summary>The run of lanes the line is laid over, nearest first. A car's current lane is the first of them.</summary>
    public int[] LaneChain { get; }

    /// <summary>Where each lane of the chain begins along the line, and where it ends — between the two is the junction.</summary>
    public float[] LaneStartM { get; }

    public float[] LaneEndM { get; }

    /// <summary>How far along its own line the rear axle is.</summary>
    public float[] ProgressM { get; }

    /// <summary>
    /// How fast the body is going <b>along the direction its line is driven in</b>, which for a bay's way
    /// taken in reverse is the way the car is not pointing. The tick works it out and the leg reads it, so
    /// nothing has to know which gear the line is in to ask whether the car is moving.
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

    /// <summary>
    /// <b>Whether the movement into that box is a turn rather than straight on</b> — which is the whole of
    /// what an indicator has to say (CAR-14.1). It is the road's own classification of the pair of lanes the
    /// line joins, handed over as the fact rather than as the type, because the fleet knows nothing of the
    /// graph the turn is read off.
    /// </summary>
    public bool[] TurningAtTheBox { get; }

    /// <summary>
    /// <b>Whether the road this car was granted reaches into that box</b> — for the read-out and the
    /// instruments. Nothing decides on it: the grant is what the car drives to.
    /// </summary>
    public bool[] BoxIsOurs { get; }

    /// <summary>
    /// <b>Whether this car is past the point it could stop short of that box</b> — an instrument's reading of
    /// the same relation the plan is laid on (<see cref="CommittedToM"/>).
    /// </summary>
    public bool[] CommittedToTheBox { get; }

    /// <summary>
    /// How long this car has been driven since its leg last decided — run up by the tick, spent by the
    /// decision. <b>The decision's own elapsed time and not the loop's nominal interval</b>, which is what the
    /// leg's clock and an errand's clocks integrate over.
    /// </summary>
    public float[] SinceDecisionS { get; }

    public DrivenLine[] Line { get; }

    /// <summary>
    /// <b>The numbered way this car's line <em>is</em></b>, or <see cref="NoWay"/> where the line is the
    /// route's chain of lanes.
    /// </summary>
    /// <remarks>
    /// A bay's own ways are the lines of this kind: they are not lanes, so they carry no chain, and the
    /// town laid them, so they are not geometry a driver invented (CAR-15). What it buys is that a car driving it is a car on a way — its
    /// claim is laid along it, its grant is cut by the table, and nothing about it needs a second
    /// mechanism. <b>Read through <see cref="LineWayOf"/></b>, which is what makes it impossible for it to
    /// be stale.
    /// </remarks>
    public int[] LineWay { get; }

    /// <summary>
    /// <b>The way this car's line finishes on past its last lane</b> — the way into the bay the leg is
    /// aimed at — or <see cref="NoWay"/> where the line ends on the road. <b>Read through
    /// <see cref="TailWayOf"/></b>, which is what makes it impossible for it to be stale.
    /// </summary>
    /// <remarks>
    /// It is what claims the last dozen metres of a leg like every other metre of it: the
    /// claim runs along it, the traffic on the lane it crosses is held off it by the town's own table
    /// of crossings, and a driver working into a bay is a driver on a way. <b>Written where the line is
    /// assembled and nowhere else</b>, so it cannot describe a line the car is not holding.
    /// </remarks>
    public int[] TailWay { get; }

    /// <summary>
    /// <b>The lane this leg comes back down after turning at a car park</b> (GEN-4l), or
    /// <see cref="NoLane"/>. Written where the route is expanded, because it is the route that says the leg
    /// has to come back the other way; read where the queue runs out, where it says the line ends at this
    /// frontage rather than the road running on.
    /// </summary>
    /// <remarks>
    /// It is not the bay — that is a claim in the registry (GEN-4g). A leg can want to turn here and
    /// have no bay to do it in yet, which is a car driving up to the frontage and asking again, and the two
    /// facts have to be able to say so separately.
    /// </remarks>
    public int[] TurnsBackOn { get; }

    /// <summary>
    /// <b>The stretch of its own line this car plans to use</b> (TER-4c.1): from its nose to where it means
    /// to be able to stop, in the line's metres, as the last rebuild asked for it — the grant taken off it is
    /// <see cref="AuthorityM"/>. Both are the nose for a car that plans nothing.
    /// </summary>
    public float[] ClaimFromM { get; }

    public float[] ClaimToM { get; }

    /// <summary>
    /// <b>Where the ground this car can no longer stop short of ends</b>, in the line's metres — the part of
    /// its plan held at <see cref="World.Road.ClaimPriority.Committed"/>, which nothing takes.
    /// </summary>
    public float[] CommittedToM { get; }

    /// <summary>
    /// <b>How far ahead of its nose the car was granted room to stop</b> — its plan as it survived every
    /// body and every other plan, less the ground it keeps off whatever cut it. Infinite where nothing cut
    /// it: an empty road, or a car that is not under way at all.
    /// </summary>
    /// <remarks>
    /// It is a distance from the nose and is walked in by the ground covered since it was granted: a car
    /// that held it unchanged while driving at it would be holding a point receding at its own speed. Negative where the car is
    /// already inside ground somebody else has, which is a fact about a contact and not about a gap.
    /// </remarks>
    public float[] AuthorityM { get; }

    /// <summary>
    /// <b>The ground the grant was taken short of what cut it</b> — the gap the car keeps, so that the grant and
    /// this together are the whole of the section it holds.
    /// </summary>
    public float[] GrantMarginM { get; }

    /// <summary>
    /// <b>What cut that grant</b> — the queue in front, a body going nowhere, somebody on foot in the lane,
    /// ground somebody has claimed — or <see cref="Control.HeadwayKind.Nothing"/> where nothing did.
    /// </summary>
    /// <remarks>
    /// <b>Why a car is held, and never how fast it may go</b>: the speed is the grant's distance whatever cut
    /// it (S-2a). The laying knew it when it made the cut, so anything that has to say <em>why</em> a car is
    /// held reads it rather than searching for the answer again — the read-out, the leg's clock and the stuck
    /// probe.
    /// </remarks>
    public Control.HeadwayKind[] GrantCutBy { get; }

    /// <summary>
    /// What the speed profile would have asked for with the road to itself — every term but the grant. It
    /// is the <em>ceiling</em> on what the next claim is sized by: a car may be committed to no more
    /// road than it is going to drive over, and never to the road its top speed would need whether or not
    /// it is anywhere near it.
    /// </summary>
    public float[] PlannedMps { get; }

    /// <summary>
    /// <b>A pace this car is held under whatever else it could do</b>, or <c>+∞</c> for a car held to
    /// nothing but its own build. It is a ceiling somebody put on the car rather than one the road, the
    /// corner or the traffic put on it — an escort held to the pace of what it is escorting is the only
    /// thing that sets one.
    /// </summary>
    public float[] PaceMps { get; }

    public float[] GroundCoefficient { get; }

    /// <summary>What the driver asked for this tick, kept for the tyres, the debug layer and whoever asks what the car is doing.</summary>
    public DriveCommand[] Command { get; }

    /// <summary>Which of the things that limit a car limited this one — an instrument, and the only useful question about a slow car.</summary>
    public Control.DrivingHold[] Hold { get; }

    /// <summary>
    /// What the driver was told about the world this tick — what is claimed ahead of it and where
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

    /// <summary>
    /// Whether the queue stops short of where the car is going: <see cref="RouteLanesPerCar"/> lanes were
    /// not enough for the route the search found, so the rest of it will be planned again from the last
    /// lane in hand. <b>A route that ends at its destination answers no</b>, and so does one that ends at a
    /// frontage it turns back on (<see cref="TurnsBackOn"/>), which is a leg with a turn in a bay in front
    /// of it rather than a road.
    /// </summary>
    /// <remarks>
    /// It is asked from outside the drive: what the interface draws past the end of a held route (CTL-1a)
    /// is only drawn where there is a route past it, and the alternative — planning to find out — comes
    /// back with the way round the block for every car already standing at its own destination.
    /// </remarks>
    public bool[] RouteRunsOut { get; }

    /// <summary>Where this car is going. A place in the town, and not a node: a destination always is.</summary>
    public Vector2[] DestinationM { get; }

    public bool[] HasDestination { get; }

    /// <summary>Reroutes spent on this leg, which is what bounds the road being the thing that is wrong with it.</summary>
    public byte[] Reroutes { get; }

    /// <summary>
    /// This car's own share of the patience a leg is given up after, drawn once when it joins the roster.
    /// <b>Two cars jammed against each other move in lockstep and stay jammed</b> without it.
    /// </summary>
    public float[] FuseJitter { get; }

    /// <summary>
    /// <b>Whether this driver backs into parking spaces</b> (GEN-4j) — drawn once when it joins the roster,
    /// like the fuse jitter, because it is a habit and not a decision. A bay that lays only the other
    /// standing overrules it (<see cref="World.Parking.BayWays.TheStandingOnOffer"/>).
    /// </summary>
    public bool[] BacksIntoBays { get; }

    /// <summary>
    /// Whether the line in hand is driven backwards. <b>A property of the line and not of the car</b>: a
    /// bay's way laid for reverse (GEN-4j) is laid in the direction the rear axle travels, and the follower
    /// steers against it.
    /// </summary>
    public bool[] LineIsReverse { get; }

    /// <summary>
    /// Whether the body is <em>in</em> the junction box rather than approaching one. <b>Waiting at a
    /// boundary is not standing across a lane.</b> An instrument's: the stuck probe prints it and nothing
    /// decides by it.
    /// </summary>
    public bool[] InsideTheBox { get; }

    /// <summary>
    /// How far ahead of its nose a light holds this car's road, out to a queue's length of it, or infinity
    /// where none does (TLT-1). <b>A car queueing for a light spends none of its leg's clock</b>, and this is
    /// the whole of how the clock knows one.
    /// </summary>
    public float[] LightAheadM { get; }

    /// <summary>
    /// The pass this car is getting past something on (CAR-46), or <see cref="Control.Overtake.None"/>. <b>The
    /// driver's own and read by nobody else</b>: what the town sees of it is the ground it covers (TER-4c.6).
    /// </summary>
    public Control.Overtake[] Pass { get; }

    public Span<int> RouteOf(int car) => RouteLanes.AsSpan(car * RouteLanesPerCar, RouteLanesPerCar);

    /// <summary>
    /// The next lane the route says to take, <b>without taking it</b> — which is what says whether the
    /// road joins to it at all, and so whether the queue in hand is one this car may still drive.
    /// </summary>
    public int PeekNextRouteLane(int car) =>
        RouteTaken[car] >= RouteCount[car] ? NoLane : RouteLanes[(car * RouteLanesPerCar) + RouteTaken[car]];

    /// <summary>The next lane the route says to take, or <see cref="NoLane"/> where the route has run out.</summary>
    public int TakeNextRouteLane(int car)
    {
        if (RouteTaken[car] >= RouteCount[car]) return NoLane;

        return RouteLanes[(car * RouteLanesPerCar) + RouteTaken[car]++];
    }

    /// <summary>
    /// The queue dropped, <b>and with it the turn at the end of it</b>: a leg comes back the other way
    /// because the route it is holding says to (GEN-4l), so a route given up takes that with it. The bay
    /// claimed for the turn is the registry's and is given back by whoever gave up the route.
    /// </summary>
    public void ClearRoute(int car)
    {
        RouteCount[car] = 0;
        RouteTaken[car] = 0;
        RouteRunsOut[car] = false;
        TurnsBackOn[car] = NoLane;
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

    public Span<int> ChainOf(int car) => LaneChain.AsSpan(car * LineAssembler.MostLanes, LineAssembler.MostLanes);

    public Span<float> LaneStartsOf(int car) => LaneStartM.AsSpan(car * LineAssembler.MostLanes, LineAssembler.MostLanes);

    public Span<float> LaneEndsOf(int car) => LaneEndM.AsSpan(car * LineAssembler.MostLanes, LineAssembler.MostLanes);

    /// <summary>
    /// The way the line in hand <em>is</em>, or <see cref="NoWay"/>. <b>A line with no arcs is no way</b>,
    /// whatever was last written — so a car whose line was taken away holds none, and nothing has to
    /// remember to say so.
    /// </summary>
    public int LineWayOf(int car) => Line[car].ArcCount > 0 ? LineWay[car] : NoWay;

    /// <summary>
    /// The way the line in hand finishes on past its last lane, or <see cref="NoWay"/>. <b>A line with no
    /// lanes has no tail</b>, whatever was last written — which is what makes a bay's way taken as the line
    /// drop the route's tail with it, and nothing has to remember to.
    /// </summary>
    public int TailWayOf(int car) => Line[car].LaneCount > 0 ? TailWay[car] : NoWay;

    /// <summary>The lane the car is on, which is the first of its chain — or <see cref="NoLane"/> when it is on none.</summary>
    public int LaneOf(int car) => Line[car].LaneCount > 0 ? LaneChain[car * LineAssembler.MostLanes] : NoLane;

    /// <summary>
    /// <b>The car this one is</b> — its own body, axles and what its tyres are worth (CAR-11). Every
    /// decision taken for a car is taken against this and not against the nominal car the town is sized
    /// for: read <c>in</c>, and the same instance for every car wearing the same look.
    /// </summary>
    public ref readonly CarBuild BuildOf(int car) => ref _builds.Of(Variant[car]);

    /// <param name="variant">
    /// Which look this car wears, and with it <b>which car it is</b>: the build it is driven by is this
    /// variant's (<see cref="BuildOf"/>), so its weight, its axles and what its tyres are worth all come
    /// from the same place its picture does.
    /// </param>
    /// <param name="backsIntoBays">
    /// Whether this driver backs into parking spaces (GEN-4j). The spawner's, because it is what the pose
    /// a car starts standing in was chosen against.
    /// </param>
    public int Add(
        BodyId body, Vector2 positionM, float headingRad, byte variant, bool backsIntoBays, Rng draw)
    {
        if (Count == Capacity) throw new InvalidOperationException($"The roster was laid for {Capacity} cars and is full.");

        var car = Count++;
        ref readonly var build = ref _builds.Of(variant);
        Body[car] = body;
        PositionM[car] = positionM;
        HeadingRad[car] = headingRad;
        VelocityMps[car] = Vector2.Zero;
        YawRateRadPerS[car] = 0f;
        AccelerationMps2[car] = Vector2.Zero;
        MassKg[car] = build.MassKg;
        Variant[car] = variant;
        DrivenFrontShare[car] = build.DrivenFrontShare;
        Draw[car] = draw;
        Driven[car] = false;
        Broken[car] = false;
        Ambulance[car] = false;
        BlueLight[car] = false;
        AtWork[car] = false;
        ProgressM[car] = 0f;
        AlongMps[car] = 0f;
        OffLineM[car] = 0f;
        ToTheBoxM[car] = float.PositiveInfinity;
        TurningAtTheBox[car] = false;
        BoxIsOurs[car] = false;
        CommittedToTheBox[car] = false;
        SinceDecisionS[car] = 0f;
        Line[car] = default;
        LineWay[car] = NoWay;
        TailWay[car] = NoWay;
        TurnsBackOn[car] = NoLane;
        AuthorityM[car] = float.PositiveInfinity;
        GrantMarginM[car] = 0f;
        GrantCutBy[car] = Control.HeadwayKind.Nothing;
        PlannedMps[car] = 0f;
        PaceMps[car] = float.PositiveInfinity;
        GroundCoefficient[car] = 1f;
        Command[car] = DriveCommand.Parked;
        Hold[car] = Control.DrivingHold.None;
        RouteCount[car] = 0;
        RouteTaken[car] = 0;
        RouteRunsOut[car] = false;
        HasDestination[car] = false;
        Reroutes[car] = 0;

        // Drawn from the car's own stream, so the jitter is the town's seed and not the clock's.
        FuseJitter[car] = Draw[car].NextFloat(1f - FuseJitterShare, 1f + FuseJitterShare);
        BacksIntoBays[car] = backsIntoBays;
        LineIsReverse[car] = false;
        InsideTheBox[car] = false;
        LightAheadM[car] = float.PositiveInfinity;
        Pass[car] = Control.Overtake.None;
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

    /// <summary>
    /// No way at all: a car committed to no movement, claiming no stretch, or whose line is a chain of
    /// lanes.
    /// </summary>
    public const int NoWay = -1;

    ArcSeg[] LineArcs { get; }
}
