using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
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

    readonly DrivenLine[] _line;

    public CarFleet(int capacity, int arcsPerCar, CarBuilds builds)
    {
        _arcsPerCar = arcsPerCar;
        _builds = builds;
        PositionM = new Vector2[capacity];
        HeadingRad = new float[capacity];
        Forward = new Vector2[capacity];
        VelocityMps = new Vector2[capacity];
        YawRateRadPerS = new float[capacity];
        Variant = new byte[capacity];
        Draw = new Rng[capacity];
        Owner = new int[capacity];
        Array.Fill(Owner, NoOwner);
        Driven = new bool[capacity];
        Action = new CarAction[capacity];
        Broken = new bool[capacity];
        Channels = new byte[capacity];
        Ambulance = new bool[capacity];
        BlueLight = new bool[capacity];
        AtWork = new bool[capacity];
        LaneChain = new int[capacity * LineAssembler.MostLanes];
        LaneStartM = new float[capacity * LineAssembler.MostLanes];
        LaneEndM = new float[capacity * LineAssembler.MostLanes];
        LaneFirstArc = new int[capacity * LineAssembler.MostLanes];
        JoinBreaks = new bool[capacity * LineAssembler.MostLanes];
        Joins = new int[capacity * LineAssembler.MostLanes];
        Array.Fill(Joins, RoadGraph.NoConnector);
        ProgressM = new float[capacity];
        AlongMps = new float[capacity];
        OffLineM = new float[capacity];
        ToTheBoxM = new float[capacity];
        Array.Fill(ToTheBoxM, float.PositiveInfinity);
        TurningAtTheBox = new bool[capacity];
        _line = new DrivenLine[capacity];
        LinesLaid = new uint[capacity];
        LineCursor = new SplineCursor[capacity];
        LineArcs = new ArcSeg[capacity * arcsPerCar];
        LineEntryM = new float[capacity * arcsPerCar];
        StopsForBay = new int[capacity];
        Array.Fill(StopsForBay, NoBay);
        StopsAtItsPlace = new bool[capacity];
        LineStopsAtM = new float[capacity];
        TurnsBackOn = new int[capacity];
        Array.Fill(TurnsBackOn, NoLane);
        ClaimFromM = new float[capacity];
        ClaimToM = new float[capacity];
        CommittedToM = new float[capacity];
        AuthorityM = new float[capacity];
        CoveredSinceClaimM = new float[capacity];
        HorizonM = new float[capacity];
        Array.Fill(HorizonM, float.PositiveInfinity);
        GrantMarginM = new float[capacity];
        GrantCutBy = new Control.HeadwayKind[capacity];
        PlannedMps = new float[capacity];
        PaceMps = new float[capacity];
        Array.Fill(PaceMps, float.PositiveInfinity);
        Command = new DriveCommand[capacity];
        Lamps = new CarLampSet[capacity];
        Hold = new Control.DrivingHold[capacity];
        Context = new Control.DriveContext[capacity];
        Array.Fill(Context, Control.DriveContext.Clear);
        RouteLanes = new int[capacity * RouteLanesPerCar];
        RouteCount = new int[capacity];
        RouteTaken = new int[capacity];
        RouteRunsOut = new bool[capacity];
        RouteEndsOn = new int[capacity];
        Array.Fill(RouteEndsOn, NoLane);
        RouteEndsAtM = new float[capacity];
        DestinationM = new Vector2[capacity];
        HasDestination = new bool[capacity];
        Reroutes = new byte[capacity];
        FuseJitter = new float[capacity];
        BacksIntoBays = new bool[capacity];
        LineIsReverse = new bool[capacity];
        Pass = new Control.Overtake[capacity];
        Array.Fill(Pass, Control.Overtake.None);
        BackRoomM = new float[capacity];
        Array.Fill(BackRoomM, float.NaN);
    }

    /// <summary>
    /// How much of a route a car carries at once. <b>A bound on the work and not a figure behaviour
    /// reads</b>: a route longer than this is planned again from where the car has got to, which is what
    /// the planner does anyway when a way it was given is priced up under it.
    /// </summary>
    public const int RouteLanesPerCar = 64;

    public int Count { get; private set; }

    public int Capacity => PositionM.Length;

    /// <summary>The middle of the body. Every <em>line</em> is the rear axle's, which is that less half a wheelbase along the heading.</summary>
    public Vector2[] PositionM { get; }

    /// <summary>Solver output, unlike a walker's: a car is a box that turns because its tyres turned it.</summary>
    public float[] HeadingRad { get; }

    /// <summary>
    /// <see cref="HeadingRad"/> as the unit it is — the body's own rotation as the world turned it, which is
    /// <c>Heading.Unit</c> of the heading to the bit — so a tick reading a car's pose reduces no angle.
    /// </summary>
    /// <remarks>Written wherever <see cref="HeadingRad"/> is, and nowhere else.</remarks>
    public Vector2[] Forward { get; }

    public Vector2[] VelocityMps { get; }

    public float[] YawRateRadPerS { get; }

    /// <summary>What the car weighs — its build's (<see cref="CarBuild.MassKg"/>), which nothing changes while it runs.</summary>
    public float MassKgOf(int car) => BuildOf(car).MassKg;

    public byte[] Variant { get; }

    public Rng[] Draw;

    /// <summary>
    /// <b>The person whose car this is</b> (PER-29), or <see cref="NoOwner"/> — the other half of
    /// <see cref="Agents.Person.Body.PersonFleet.Car"/>, set once where the town stands both.
    /// </summary>
    public int[] Owner { get; }

    /// <summary>
    /// CAR-1: a car acts only while something is driving it — its owner's trip, an errand, an order or the tour
    /// of a map with no bay to reach. Set by whatever sends it and cleared where it is stood down; nobody
    /// sitting in it sets it. Without it the car is an inert dynamic object holding its handbrake.
    /// </summary>
    public bool[] Driven { get; }

    /// <summary>
    /// <b>What each car is doing</b> (CAR-15b) — one action, changed in one place in the town and read everywhere
    /// else. Every claim a car lays and every command it gives is its action's.
    /// </summary>
    public CarAction[] Action { get; }

    /// <summary>
    /// PHY-3's terminal state for a car, which is the whole of what damage does to one: broken, never
    /// driven again, and never removed (PHY-5). A wreck keeps its body and its shape, stays dynamic and
    /// is pushed like anything else — with all four of its wheels locked, so a shunted one skids as a
    /// block rather than rolling away.
    /// </summary>
    public bool[] Broken { get; }

    /// <summary>
    /// <b>The collision channels the car is on</b> (PHY-1a): every channel of the ways its collider stood over when its
    /// body was last laid — on a bridge over other roads the bridge's alone, so it meets nobody and claims nothing of the
    /// road below, and at a bridgehead the bridge's and the ground's. Kept where it stood over none.
    /// </summary>
    public byte[] Channels { get; }

    /// <summary>
    /// <b>The level the car is drawn at and stands on</b>: the highest of its channels' (<see cref="Channels"/>), so a
    /// car at a bridgehead is on the deck where there is deck and on the ground past its end.
    /// </summary>
    public byte LevelOf(int car) => (byte)BitOperations.Log2(Channels[car]);

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
    /// How fast the body is going <b>along the direction its line is driven in</b>, which for a piece of a
    /// manoeuvre taken in reverse is the way the car is not pointing. The tick works it out and the leg reads it, so
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

    /// <summary>How much of each car's line there is, its arrays aside; laid by <see cref="SetLine"/>.</summary>
    public ReadOnlySpan<DrivenLine> Line => _line;

    /// <summary>This car's line, its arrays already written, taken as laid.</summary>
    public void SetLine(int car, DrivenLine line)
    {
        _line[car] = line;
        LinesLaid[car]++;
        LineCursor[car] = default;
    }

    /// <summary>
    /// <b>Where on its line each car's walks of it begin</b>: the first arc that reaches the window its progress was last
    /// found in (<see cref="Spline.ProjectM(ReadOnlySpan{ArcSeg}, Vector2, float, float, ref SplineCursor)"/>), so a tick
    /// reads the line from about where the car is rather than from its head. Back at the head with every line.
    /// </summary>
    public SplineCursor[] LineCursor { get; }

    /// <summary>
    /// <b>How many lines this car has been handed</b> — laid, shifted or dropped (<see cref="SetLine"/>): what a figure
    /// read off the line and kept beside it is checked against, so it cannot outlive the line it was read off.
    /// </summary>
    public uint[] LinesLaid { get; }

    /// <summary>
    /// <b>The bay this car's line stops short of</b> — the one the leg parks in off the last lane of the line,
    /// where the car waits for its manoeuvre (GEN-4f) — or <see cref="NoBay"/> where the line ends on the road.
    /// <b>Read through <see cref="StopsForBayOf"/></b>, which is what makes it impossible for it to be stale.
    /// </summary>
    /// <remarks>
    /// <b>Written where the line is assembled and nowhere else</b>, so it cannot describe a line the car is not
    /// holding: the line's end was placed for this bay, and the manoeuvre into it is asked from there.
    /// </remarks>
    public int[] StopsForBay { get; }

    /// <summary>
    /// <b>Whether this car's line ends at the place its leg is aimed at in the road</b> — where the route ends, and
    /// where the car stands once it has driven the line out (SRV-5). <b>Read through <see cref="StopsAtItsPlaceOf"/></b>,
    /// and written where the line is assembled and nowhere else, as <see cref="StopsForBay"/> is.
    /// </summary>
    public bool[] StopsAtItsPlace { get; }

    /// <summary>
    /// <b>How far along its own lane the line's last lane is laid to</b> (<see cref="LineAssembler.Assemble"/>): the lane's
    /// length, or short of it where the line stops for a bay or a place — what a world reading the car on its lanes cuts
    /// that lane at (<see cref="LineFollowed"/>). Written where the line is assembled, and kept by a shift.
    /// </summary>
    public float[] LineStopsAtM { get; }

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
    /// to be able to stop, in the line's metres, as its last turn asked for it — the grant taken off it is
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
    /// <b>How far ahead of its nose the car was granted room to stop</b> — its action's claim as it survived every
    /// body and every other plan, less the ground it keeps off whatever cut it. Infinite where nothing cut it —
    /// an empty road, or a manoeuvre with nobody on its ground — and <b>nothing at all where its action claimed
    /// nothing</b> (CAR-15b): a car moves over no ground it has not claimed.
    /// </summary>
    /// <remarks>
    /// It is a distance from the nose where the claim was laid from (<see cref="ClaimFromM"/>) and is walked in by
    /// the ground covered since (<see cref="CoveredSinceClaimM"/>, <see cref="GrantLeftM"/>): a car that held it
    /// unchanged while driving at it would be holding a point receding at its own speed. Negative where the car is
    /// already inside ground somebody else has, which is a fact about a contact and not about a gap.
    /// </remarks>
    public float[] AuthorityM { get; }

    /// <summary>
    /// <b>The ground a car has covered along its line since its claim was laid</b> — every tick's, until the claims are
    /// laid again, which is a decision interval and not a tick.
    /// </summary>
    public float[] CoveredSinceClaimM { get; }

    /// <summary>How much of its grant a car has still in front of its nose: <see cref="AuthorityM"/> walked in.</summary>
    public float GrantLeftM(int car) => AuthorityM[car] - CoveredSinceClaimM[car];

    /// <summary>
    /// <b>How far ahead of its nose the car's own plan ends where it was held short of what the car wanted</b> by
    /// how far a plan may reach (TER-4c.1) — or infinity where the car planned all it wanted. Walked in by the
    /// ground covered since, as <see cref="AuthorityM"/> is.
    /// </summary>
    /// <remarks>
    /// <b>Beside the grant and never inside it</b>: nothing cut this plan, so it is not what holds a car in a queue,
    /// a car's clock or its words — only a stop point the profile drives to like the end of its line (S-2).
    /// </remarks>
    public float[] HorizonM { get; }

    /// <summary>
    /// <b>The ground the grant was taken short of what cut it</b> — the gap the car keeps, so that the grant and
    /// this together are the whole of the section it holds.
    /// </summary>
    public float[] GrantMarginM { get; }

    /// <summary>
    /// <b>What cut that grant</b> — a body, a light's hold, ground somebody has claimed — or
    /// <see cref="Control.HeadwayKind.Nothing"/> where nothing did.
    /// </summary>
    /// <remarks>
    /// <b>Why a car is held, and never how fast it may go</b>: the speed is the grant's distance whatever cut
    /// it (S-2a). The turn knew it when it made the cut, so anything that has to say <em>why</em> a car is
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

    /// <summary>What the driver asked for this tick, kept for the tyres, the debug layer and whoever asks what the car is doing.</summary>
    public DriveCommand[] Command { get; }

    /// <summary>
    /// <b>What each car's lamps say</b> (CAR-14), as its last tick worked them out (<see cref="CarLamps.Showing"/>) — what
    /// the world is handed and what a frame draws, so the two never disagree about a lamp.
    /// </summary>
    public CarLampSet[] Lamps { get; }

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
    /// not enough for the route the search found, or the search handed over only the section of it in front
    /// of the car — so the rest of it will be planned again from the last lane in hand. <b>A route that ends at its destination answers no</b>, and so does one that ends at a
    /// frontage it turns back on (<see cref="TurnsBackOn"/>), which is a leg with a turn in a bay in front
    /// of it rather than a road.
    /// </summary>
    /// <remarks>
    /// It is asked from outside the drive: what the interface draws past the end of a held route (CTL-1a)
    /// is only drawn where there is a route past it, and the alternative — planning to find out — comes
    /// back with the way round the block for every car already standing at its own destination.
    /// </remarks>
    public bool[] RouteRunsOut { get; }

    /// <summary>
    /// <b>The lane the queue ends on where it ends at the place it was searched for</b>, or <see cref="NoLane"/> where it
    /// stops short of it — out of room (<see cref="RouteRunsOut"/>) or at a frontage it turns back on
    /// (<see cref="TurnsBackOn"/>). A route that is already there when it is laid ends on the lane it was laid from.
    /// </summary>
    public int[] RouteEndsOn { get; }

    /// <summary>How far along <see cref="RouteEndsOn"/> that place stands, in the lane's own metres.</summary>
    public float[] RouteEndsAtM { get; }

    /// <summary>Where this car is going. A place in the town, and not a node: a destination always is.</summary>
    public Vector2[] DestinationM { get; }

    public bool[] HasDestination { get; }

    /// <summary>
    /// Reroutes spent on this leg, which is what bounds the road being the thing that is wrong with it — and the place
    /// given up for another, counted past the last of them (<c>TownWorld.WatchTheProgress</c>).
    /// </summary>
    public byte[] Reroutes { get; }

    /// <summary>
    /// This car's own share of the patience a leg is given up after, drawn once when it joins the roster.
    /// <b>Two cars jammed against each other move in lockstep and stay jammed</b> without it.
    /// </summary>
    public float[] FuseJitter { get; }

    /// <summary>
    /// <b>Whether this driver backs into parking spaces</b> (GEN-4j) — drawn once when it joins the roster,
    /// like the fuse jitter, because it is a habit and not a decision: the way round it is stood in a bay, and
    /// the shape it takes into one where two take the same ground.
    /// </summary>
    public bool[] BacksIntoBays { get; }

    /// <summary>
    /// Whether the line in hand is driven backwards. <b>A property of the line and not of the car</b>: a piece
    /// of a manoeuvre driven in reverse (GEN-4f) is laid in the direction the rear axle travels, and the
    /// follower steers against it.
    /// </summary>
    public bool[] LineIsReverse { get; }

    /// <summary>
    /// Whether the body is <em>in</em> the junction box rather than approaching one — its rear axle past the mouth, where
    /// <see cref="ToTheBoxM"/> reads nought. <b>Waiting at a boundary is not standing across a lane.</b> An instrument's:
    /// the stuck probe prints it and nothing decides by it.
    /// </summary>
    /// <remarks>A nose brought to a stop on the mouth to the bit reads as in it too.</remarks>
    public bool IsInsideTheBox(int car) => ToTheBoxM[car] == 0f;

    /// <summary>
    /// The pass this car is getting past something on (CAR-46), or <see cref="Control.Overtake.None"/>. <b>The
    /// driver's own and read by nobody else</b> but its own indicator (CAR-14.7): what the town sees of it is the
    /// ground it covers (TER-4c.6).
    /// </summary>
    public Control.Overtake[] Pass { get; }

    /// <summary>
    /// <b>How far behind its tail the ground this car asked to back up over was had</b> (CAR-50, TER-4c.7) — the
    /// grant of a car backing up, as <see cref="AuthorityM"/> is of one going forwards: infinite where all it asked
    /// for was had, and <see cref="float.NaN"/> where it asked for none.
    /// </summary>
    public float[] BackRoomM { get; }

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

    /// <summary>And the one after that, without taking either — what a route moving across onto the next goes on to (CAR-53).</summary>
    public int PeekRouteLaneAfterNext(int car) =>
        RouteTaken[car] + 1 >= RouteCount[car] ? NoLane : RouteLanes[(car * RouteLanesPerCar) + RouteTaken[car] + 1];

    /// <summary>
    /// <b>Lanes laid into the chain handed back to the front of the queue</b>, in the order they were — where the car
    /// has moved across off them (CAR-53) and the line is laid again beside them. <b>What does not fit is dropped off
    /// the far end</b>, and the queue then runs out there and is planned again from wherever the car has got to.
    /// </summary>
    public void PutBackOnRoute(int car, ReadOnlySpan<int> lanes)
    {
        var route = RouteOf(car);
        var taken = RouteTaken[car];
        var left = RouteCount[car] - taken;
        var kept = Math.Min(left, route.Length - lanes.Length);
        if (kept < left)
        {
            RouteRunsOut[car] = true;
            RouteEndsOn[car] = NoLane;
        }

        route.Slice(taken, kept).CopyTo(route[lanes.Length..]);
        lanes.CopyTo(route);
        RouteTaken[car] = 0;
        RouteCount[car] = lanes.Length + kept;
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
        RouteEndsOn[car] = NoLane;
        TurnsBackOn[car] = NoLane;
    }

    /// <summary>
    /// <b>A car's line asked of memory ahead of its reading</b> — where its walks begin (<see cref="LineCursor"/>), its
    /// lanes' metres, its chain and its joins: each car's line is a block of its own, which a walk of the fleet in order
    /// meets cold. A hint and nothing else.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void PrefetchTheLine(int car)
    {
        if (!System.Runtime.Intrinsics.X86.Sse.IsSupported || (uint)car >= (uint)Count) return;

        var arc = (car * _arcsPerCar) + Math.Min(LineCursor[car].Piece, _arcsPerCar - 1);
        System.Runtime.Intrinsics.X86.Sse.Prefetch0(Unsafe.AsPointer(ref LineArcs[arc]));
        System.Runtime.Intrinsics.X86.Sse.Prefetch0(Unsafe.AsPointer(ref LineEntryM[arc]));
        System.Runtime.Intrinsics.X86.Sse.Prefetch0(Unsafe.AsPointer(ref LaneStartM[car * LineAssembler.MostLanes]));
        System.Runtime.Intrinsics.X86.Sse.Prefetch0(Unsafe.AsPointer(ref LaneEndM[car * LineAssembler.MostLanes]));
        System.Runtime.Intrinsics.X86.Sse.Prefetch0(Unsafe.AsPointer(ref Joins[car * LineAssembler.MostLanes]));
        System.Runtime.Intrinsics.X86.Sse.Prefetch0(Unsafe.AsPointer(ref LaneChain[car * LineAssembler.MostLanes]));
    }

    public Span<ArcSeg> LineArcsOf(int car) => LineArcs.AsSpan(car * _arcsPerCar, _arcsPerCar);

    public ReadOnlySpan<ArcSeg> LineOf(int car) => LineArcs.AsSpan(car * _arcsPerCar, Line[car].ArcCount);

    /// <summary>
    /// <b>The speed each arc of the line in hand may be entered at</b> (<see cref="CornerLimits"/>), as its square
    /// over the grip — laid with the line, one to an arc.
    /// </summary>
    public Span<float> LineEntriesOf(int car) => LineEntryM.AsSpan(car * _arcsPerCar, _arcsPerCar);

    public ReadOnlySpan<float> EntriesOf(int car) => LineEntryM.AsSpan(car * _arcsPerCar, Line[car].ArcCount);

    public Span<int> ChainOf(int car) => LaneChain.AsSpan(car * LineAssembler.MostLanes, LineAssembler.MostLanes);

    public Span<float> LaneStartsOf(int car) => LaneStartM.AsSpan(car * LineAssembler.MostLanes, LineAssembler.MostLanes);

    public Span<float> LaneEndsOf(int car) => LaneEndM.AsSpan(car * LineAssembler.MostLanes, LineAssembler.MostLanes);

    /// <summary>The arc each lane of the chain's own arcs begin at, laid with the line (<see cref="LineAssembler.Assemble"/>).</summary>
    public Span<int> LaneFirstArcsOf(int car) => LaneFirstArc.AsSpan(car * LineAssembler.MostLanes, LineAssembler.MostLanes);

    /// <summary>
    /// <b>The line shifted on past its first lane</b>: that lane and the join out of it dropped, every other lane's arcs,
    /// entry figures, join and the stop it ends at kept exactly as laid, and its metres measured from the second lane's
    /// own start (TER-5i).
    /// </summary>
    /// <remarks>
    /// <b>Nothing is laid</b>: no lane is added and nothing is asked of the road or the route, so a tick shifts a line
    /// without deciding anything and one car's shift reads nothing of another's. The entry figures carry over because
    /// each holds the corners past its own arc and none before it (<see cref="CornerLimits.Lay"/>). Growing the line
    /// again is the decision's.
    /// </remarks>
    /// <returns>Where the second lane began on the line as it was: how far every metre of it moved back.</returns>
    public float ShiftTheLine(int car)
    {
        var line = Line[car];
        var lanes = line.LaneCount;
        var chain = ChainOf(car);
        var startM = LaneStartsOf(car);
        var endM = LaneEndsOf(car);
        var firstArc = LaneFirstArcsOf(car);
        var breaks = JoinBreaksOf(car);
        var joins = JoinsOf(car);
        var shiftM = startM[1];
        var shiftArcs = firstArc[1];

        LineArcsOf(car)[shiftArcs..line.ArcCount].CopyTo(LineArcsOf(car));
        LineEntriesOf(car)[shiftArcs..line.ArcCount].CopyTo(LineEntriesOf(car));
        for (var slot = 0; slot + 1 < lanes; slot++)
        {
            chain[slot] = chain[slot + 1];
            startM[slot] = startM[slot + 1] - shiftM;
            endM[slot] = endM[slot + 1] - shiftM;
            firstArc[slot] = firstArc[slot + 1] - shiftArcs;
            breaks[slot] = breaks[slot + 1];
            joins[slot] = joins[slot + 1];
        }

        SetLine(car, new DrivenLine(line.ArcCount - shiftArcs, lanes - 1, line.LengthM - shiftM));
        return shiftM;
    }

    /// <summary>
    /// <b>Whether the join after each lane of the chain breaks the line</b> (<see cref="RoadGraph.BreaksTheLine"/>),
    /// laid with the line — what says how many lanes ahead a plan may reach (TER-4c.1).
    /// </summary>
    public Span<bool> JoinBreaksOf(int car) => JoinBreaks.AsSpan(car * LineAssembler.MostLanes, LineAssembler.MostLanes);

    /// <summary>
    /// <b>The movement each lane of the chain is left by</b> — the connector onto the next — and
    /// <see cref="RoadGraph.NoConnector"/> past the last, laid with the line (<see cref="LayTheJoins"/>).
    /// </summary>
    public Span<int> JoinsOf(int car) => Joins.AsSpan(car * LineAssembler.MostLanes, LineAssembler.MostLanes);

    /// <summary>
    /// <b>The joins of a line just assembled over the chain</b>: each lane's movement onto the next and whether it breaks
    /// the line (<see cref="JoinsOf"/>, <see cref="JoinBreaksOf"/>), so nothing that reads the line searches the road for
    /// them again.
    /// </summary>
    public void LayTheJoins(int car, RoadGraph roads)
    {
        var chain = ChainOf(car);
        var joins = JoinsOf(car);
        var breaks = JoinBreaksOf(car);
        var lanes = Line[car].LaneCount;
        for (var slot = 0; slot < lanes; slot++)
        {
            var join = slot + 1 < lanes ? roads.ConnectorBetween(chain[slot], chain[slot + 1]) : RoadGraph.NoConnector;
            joins[slot] = join;
            breaks[slot] = join != RoadGraph.NoConnector && roads.BreaksTheLine(join);
        }
    }

    /// <summary>
    /// The bay the line in hand stops short of, or <see cref="NoBay"/>. <b>A line with no lanes stops for no
    /// bay</b>, whatever was last written — which is what makes a manoeuvre taken as the line drop it, and
    /// nothing has to remember to.
    /// </summary>
    public int StopsForBayOf(int car) => Line[car].LaneCount > 0 ? StopsForBay[car] : NoBay;

    /// <summary>Whether the line in hand ends at the place its leg is aimed at in the road — never for a line with no lanes.</summary>
    public bool StopsAtItsPlaceOf(int car) => Line[car].LaneCount > 0 && StopsAtItsPlace[car];

    /// <summary>The lane the car is on, which is the first of its chain — or <see cref="NoLane"/> when it is on none.</summary>
    public int LaneOf(int car) => Line[car].LaneCount > 0 ? LaneChain[car * LineAssembler.MostLanes] : NoLane;

    /// <summary>
    /// <b>The car this one is</b> — its own body, axles and what its tyres are worth (CAR-11). Every
    /// decision taken for a car is taken against this and not against the nominal car the town is sized
    /// for: read <c>in</c>, and the same instance for every car wearing the same look.
    /// </summary>
    public ref readonly CarBuild BuildOf(int car) => ref _builds.Of(Variant[car]);

    /// <summary>And the car a look is, whether or not any of this fleet wears it — for a table laid once a look.</summary>
    public ref readonly CarBuild BuildOfVariant(int variant) => ref _builds.Of(variant);

    /// <param name="variant">
    /// Which look this car wears, and with it <b>which car it is</b>: the build it is driven by is this
    /// variant's (<see cref="BuildOf"/>), so its weight, its axles and what its tyres are worth all come
    /// from the same place its picture does.
    /// </param>
    /// <param name="backsIntoBays">
    /// Whether this driver backs into parking spaces (GEN-4j). The spawner's, because it is what the pose
    /// a car starts standing in was chosen against.
    /// </param>
    public int Add(Vector2 positionM, float headingRad, byte variant, bool backsIntoBays, Rng draw)
    {
        if (Count == Capacity) throw new InvalidOperationException($"The roster was laid for {Capacity} cars and is full.");

        var car = Count++;
        PositionM[car] = positionM;
        HeadingRad[car] = headingRad;
        Forward[car] = Heading.Unit(headingRad);
        VelocityMps[car] = Vector2.Zero;
        YawRateRadPerS[car] = 0f;
        Variant[car] = variant;
        Draw[car] = draw;
        Owner[car] = NoOwner;
        Driven[car] = false;
        Action[car] = CarAction.Stand;
        Broken[car] = false;
        Channels[car] = CityPlan.RoadArrays.GroundChannel;
        Ambulance[car] = false;
        BlueLight[car] = false;
        AtWork[car] = false;
        ProgressM[car] = 0f;
        AlongMps[car] = 0f;
        OffLineM[car] = 0f;
        ToTheBoxM[car] = float.PositiveInfinity;
        TurningAtTheBox[car] = false;
        SetLine(car, default);
        StopsForBay[car] = NoBay;
        StopsAtItsPlace[car] = false;
        TurnsBackOn[car] = NoLane;
        AuthorityM[car] = 0f;
        CoveredSinceClaimM[car] = 0f;
        HorizonM[car] = float.PositiveInfinity;
        GrantMarginM[car] = 0f;
        GrantCutBy[car] = Control.HeadwayKind.Nothing;
        PlannedMps[car] = 0f;
        PaceMps[car] = float.PositiveInfinity;
        Command[car] = DriveCommand.Parked;
        Lamps[car] = CarLampSet.None;
        Hold[car] = Control.DrivingHold.None;
        Context[car] = Control.DriveContext.Clear;
        RouteCount[car] = 0;
        RouteTaken[car] = 0;
        RouteRunsOut[car] = false;
        RouteEndsOn[car] = NoLane;
        HasDestination[car] = false;
        Reroutes[car] = 0;

        // Drawn from the car's own stream, so the jitter is the town's seed and not the clock's.
        FuseJitter[car] = Draw[car].NextFloat(1f - FuseJitterShare, 1f + FuseJitterShare);
        BacksIntoBays[car] = backsIntoBays;
        LineIsReverse[car] = false;
        Pass[car] = Control.Overtake.None;
        BackRoomM[car] = float.NaN;
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

    /// <summary>A car nobody owns: a service vehicle, or one of a town nobody lives in.</summary>
    public const int NoOwner = -1;

    /// <summary>A line that stops for no bay.</summary>
    public const int NoBay = -1;

    /// <summary>
    /// No way at all: a car committed to no movement, claiming no stretch, or whose line is a chain of
    /// lanes.
    /// </summary>
    public const int NoWay = -1;

    ArcSeg[] LineArcs { get; }

    float[] LineEntryM { get; }

    int[] LaneFirstArc { get; }

    bool[] JoinBreaks { get; }

    int[] Joins { get; }
}
