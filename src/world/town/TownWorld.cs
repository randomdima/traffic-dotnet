using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Car.Actions;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Evacuator;
using TrafficSimulation.Agents.Person.Actions;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Agents.TrafficLight.Body;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.World.Physics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Parking;
using TrafficSimulation.Agents.Train.Control;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.World.Town;

/// <summary>
/// The town as a running simulation: the plan stood up as bodies, the roster the five phases walk,
/// and the one place the phases of <see cref="ISimWorld"/> mean something other than nothing.
/// </summary>
/// <remarks>
/// The split between <see cref="TickAgent"/> and <see cref="DecideAgent"/> is where the phase order
/// shows: holding the line is asked every tick, choosing where to walk on the decision clock. A
/// follower run on the clock steps in tenth-of-a-second lurches; a destination drawn every tick is a
/// new destination sixty times a second.
/// <para>
/// No impulse is applied before phase 4 — phase 3 writes them down and phase 4 applies them all and
/// steps, so every decision is taken against the same instant of the world.
/// </para>
/// <para>
/// This file holds the parts, the roster and the phases themselves; each <c>TownWorld.*.cs</c> beside
/// it holds one thing a phase does.
/// </para>
/// </remarks>
internal sealed partial class TownWorld : ISimWorld, IDamageRoster, IDisposable
{
    readonly SimConfig _config;
    readonly CityPlan _plan;

    /// <summary>
    /// <b>The town's furniture</b>, laid off the plan: handed to the world with the laying, and read here only while the
    /// map is built (<see cref="LayTheExits"/>) — let go once the world is the device's (<see cref="HandTheWorldTo"/>).
    /// </summary>
    StaticBodies? _statics;

    int _staticBodyCount;

    /// <summary>The furniture, while the town still holds it (<see cref="_statics"/>).</summary>
    StaticBodies Statics => _statics ?? throw new InvalidOperationException("The town's furniture is the device's once the world is.");

    /// <summary>The world layer: every body, stepped (<see cref="WorldLayer"/>).</summary>
    WorldLayer _world = null!;

    readonly RoadGraph _roads;

    /// <summary>
    /// <b>Every way in the town, in one numbering</b> — the carriageway, the bays and the pavement
    /// (<see cref="TownWays"/>). One table, so a claim on any of them is comparable with a claim on any
    /// other.
    /// </summary>
    readonly TownWays _ways;

    /// <summary>
    /// <b>Who is on each way of the town</b>, each agent's held by itself and edited on its own turn —
    /// <see cref="TakeTheTurns"/>. One set of claims over one set of ways: a car that has mounted a
    /// kerb and the walker beside it are two stretches of one footway and not two records in two tables.
    /// </summary>
    readonly LaneOccupancy _occupancy;
    readonly SignalService _signals;
    readonly SignalHeads _heads;

    /// <summary>
    /// <b>The ground every light holds against it</b> (TLT-1): laid into the reservations each rebuild, and the
    /// whole of what a light does to anybody.
    /// </summary>
    readonly SignalHolds _signalHolds;

    /// <summary>
    /// <b>The bars this town paints, laid once and read by everything that holds at one</b> (TER-6): the lanes
    /// that carry them as furniture, the lights that hold from them and the heads that stand past them.
    /// </summary>
    readonly StopBars _bars;

    /// <summary>The paint each lane meets — its stop bar and the crossings across it — projected once at load.</summary>
    readonly LaneFurniture _furniture;

    /// <summary>Whether each car's nose was behind its approach's painted bar last tick — the other half of a crossing event.</summary>
    readonly bool[] _behindTheBar;

    /// <summary>Each car's nearest light's hold on its line (<see cref="LightAheadM"/>).</summary>
    readonly LightsAhead _lightsAhead;

    /// <summary>A search over the road network: one entry, since a car is on the lane it is on.</summary>
    readonly RouteSearch _driveSearch;

    /// <summary>The walking side's own, which enters a stretch from either end.</summary>
    readonly RouteSearch _walkSearch;

    /// <summary>What the driving network's links cost beyond their length: the ways a driver was blocked entering.</summary>
    readonly LinkSurcharges _surcharges;

    /// <summary>
    /// <b>And the walking network's own</b>, which nothing marks. A link is an index into its own network, so
    /// a table shared with the drivers would price whichever walking link had a blocked driving link's number.
    /// </summary>
    readonly LinkSurcharges _walkSurcharges;

    /// <summary>Where the interface's own plans are kept, and the searches they are made over — <see cref="RouteBeyond"/>.</summary>
    readonly SelectionPaths _paths;

    /// <summary>
    /// Which crossing each stretch of the foot graph is — which ways of the pavement are paint. What the
    /// paint covers of the carriageway is the marks' (TER-5c.3).
    /// </summary>
    readonly CrossingEdges _crossingEdges;

    /// <summary>
    /// The town's clock as ticks counted, which <see cref="ElapsedS"/> is derived from rather than summed, so a phase read
    /// hours in is as true as one read in the first minute.
    /// </summary>
    long _clockTicks;

    float _elapsedS;

    /// <summary>How close each walker has come to where it is going, and how long since it last did better.</summary>
    readonly LegProgress _progress;

    readonly CarBuilds _builds;

    readonly ulong _agentSeed;

    readonly FootGraph _foot;
    readonly PavementLanes _pavementLanes;
    readonly CrossingWays _crossingWays;
    readonly WalkingNetwork _walking;
    readonly DrivingNetwork _driving;

    /// <summary>Who is inside what, and every bay in the town with who is standing in it.</summary>
    readonly Containers _containers;

    readonly ParkingRegistry _parking;

    /// <summary>The street every bay is worked off (GEN-4f); getting in and out of one is the car's own manoeuvre.</summary>
    readonly BayStreets _bayStreets;

    /// <summary>
    /// The ground of every way, and which ways share it (<see cref="RibbonAtlas"/>) — read by the managed world, and by
    /// the town only while the map is built; let go once the world is the device's (<see cref="HandTheWorldTo"/>).
    /// </summary>
    RibbonAtlas? _atlas;

    /// <summary>The line every way is travelled on, and the zebra it paints (<see cref="WayLines"/>).</summary>
    readonly WayLines _lines;

    /// <summary>Whether any road of the town is driven on a level of its own — a bridge over other roads (PHY-1a).</summary>
    readonly bool _levelled;

    /// <inheritdoc cref="_levelled"/>
    public bool Levelled => _levelled;

    /// <summary>
    /// <b>The zebras this town paints, laid once and read by both networks</b> (TER-6, WLK-10): the walk is
    /// cut and joined at them, the lanes they are painted across carry them as furniture, and the bands
    /// under each stretch of paint are projected off them. <b>One source</b> — laid twice, the paint a body
    /// walks and the paint a driver knows about would be two different sets of zebras.
    /// </summary>
    readonly Crossings _zebras;

    /// <summary>
    /// What each of the town's buildings is for — its hospitals, its police stations and its depots
    /// (AMB-1, SRV-1) — and what every ambulance and every patrol is doing about it (AMB-5, SRV-5).
    /// </summary>
    readonly BuildingUses _uses;
    readonly RescueDuty _duty;
    readonly PatrolDuty _beat;
    readonly RecoveryDuty _recovery;

    /// <summary>Every tow: what each arm holds, and which tractor holds each end of each body (EVA-5).</summary>
    readonly TowHitches _hitches;
    readonly ServiceBeat _serviceBeat;

    readonly SelectionSet _selected;

    /// <summary>The orders given since the last tick took them, one per walker and never more than the selection can hold.</summary>
    readonly int[] _orderedPeople;
    readonly Vector2[] _orderedToM;

    HandInput _hands;

    /// <summary>
    /// CTL-5d — the car a second driver has the wheel of, or −1, and what that driver is holding. It is
    /// kept beside the player's own hand rather than inside the selection, because the whole of what it is
    /// for is to be held while somebody else picks units out.
    /// </summary>
    int _otherCar = -1;
    HandInput _otherHand;

    int _ordered;

    /// <param name="agentSeed">
    /// The second of the two seeds: what the agents draw from, kept apart from the world seed so the
    /// same town can be watched with different traffic on it. It defaults to the plan's own, which is
    /// what a map opened from the command line runs at.
    /// </param>
    /// <param name="worldMaker">
    /// The world the town stands on where it is not the managed one, made as the town is laid (<see cref="IWorldMaker"/>).
    /// </param>
    public TownWorld(
        CityPlan plan, SimConfig config, bool standStatics = true, ulong? agentSeed = null, IWorldMaker? worldMaker = null)
        : this(Groundwork.Through(plan, config, worldMaker), standStatics, agentSeed)
    {
    }

    /// <summary>The town stood on what was laid for it (<see cref="Laying"/>).</summary>
    TownWorld(Groundwork laid, bool standStatics, ulong? agentSeed)
    {
        var (plan, config, worldMaker) = (laid.Plan, laid.Config, laid.WorldMaker);
        _config = config;
        _plan = plan;
        _agentSeed = agentSeed ?? plan.Seed;
        Wind = new Wind(config.Wind, plan.Seed);
        Clouds = new Clouds(config.Clouds, Wind, plan.Seed);
        WheelGrounds = WheelGrounds.Of(config);
        _statics = new StaticBodies(config.Grid.Main);

        // The selection and the orders it can give are laid with the town: what the interface holds is
        // bounded (CTL-1b), so a drag over a district writes into an array that already exists.
        _selected = new SelectionSet(config.View.SelectionMaxUnits);
        _orderedPeople = new int[_selected.Capacity];
        _orderedToM = new Vector2[_selected.Capacity];
        _roads = laid.Roads;
        _levelled = laid.Levelled;
        RoadsMs = laid.RoadsMs;
        _bayStreets = laid.BayStreets;
        _driving = laid.Driving;
        _driveSearch = new RouteSearch(
            _driving.Cells, mostEntries: Math.Max(BayStreets.MostLanes, _roads.MostLanesOfAWay), mostGoals: 2, MostRunsInARoute);
        _surcharges = new LinkSurcharges(MostWaysGivenUpOn);
        _walkSurcharges = new LinkSurcharges(MostWaysGivenUpOn);

        _pavementLanes = laid.PavementLanes;
        _zebras = laid.Zebras;
        _bars = laid.Bars;
        _signals = laid.Signals;
        _heads = laid.Heads;
        _crossingWays = laid.CrossingWays;
        _foot = laid.Foot;
        FootMs = laid.FootMs;
        _walking = laid.Walking;
        WalkingMs = laid.WalkingMs;
        _walkSearch = new RouteSearch(_walking.Cells, mostEntries: 2, mostGoals: 2, MostRunsInARoute);
        _ways = laid.Ways;

        // The interface's own room to plan a whole route into (CTL-1a), laid with the selection it is
        // bounded by and never on the frame that wants it.
        _paths = new SelectionPaths(_selected.Capacity, _driving.Cells, _walking.Cells, MostRunsInARoute);
        _furniture = laid.Furniture;
        _crossingEdges = laid.CrossingEdges;
        _signalHolds = laid.SignalHolds;
        _uses = laid.Uses;
        var (walkers, drivers) = (laid.Walkers, laid.Drivers);
        _lines = laid.Lines;
        _atlas = laid.Atlas;
        var marks = laid.Marks;
        AtlasMs = laid.AtlasMs;

        People = new PersonFleet(walkers);
        _progress = new LegProgress(walkers);

        // <b>A town stands the fleet</b> (CAR-11a).
        _builds = CarBuilds.OfTheFleet(config, CarCatalog.Shared);

        Cars = new CarFleet(drivers, Math.Max(LineAssembler.ArcsFor(_roads), BayManoeuvre.MostArcsPerPiece), _builds,
            config.Patience.FuseJitterShare);
        _mostArcsOfALane = LineAssembler.MostArcsOfALane(_roads);
        _manoeuvres = new Manoeuvres(drivers);

        _crossingHolds = CrossingHolds.Of(plan, _lines, _ways, _roads, config);
        _crossingGear = CrossingGear.Of(plan, LevelCrossings.Of(plan, config), _crossingHolds.Lights, config);
        _barrierBlocks = new bool[_crossingGear.Count];
        _rails = laid.Rails;
        _trains = TrainsOf(plan, _rails, config, _agentSeed);
        _railSeen = new RailVehicleSeen[_trains.WagonCount];
        _trainOrders = new TrainOrders(_trains.MostOrdersATick);
        _trainManual = new bool[_trains.Count];
        _trainHold = LaneOccupancy.NoHolds(_trains.Count);
        OpenWatch.Passed("town: crossings and trains");
        _occupancy = new LaneOccupancy(_ways, marks, drivers + walkers + _signalHolds.Count + _crossingHolds.Count + _trains.Count);
        _ground = new DrivingGround(Cars, _occupancy, _ways, _roads, _lines, config);
        _carHold = _ground.PlanHold;
        _decisions = new TickGroups(config.Sim.AgentDecisionIntervalS, config.Sim.TickRateHz);
        _walkersInTurn = new int[People.Capacity];
        _carsInTurn = new int[Cars.Capacity];
        _carActions = new CarActions(_ground, _manoeuvres);
        _overtaking = new Overtaking(_ground, _carActions);
        _switching = new Switching(_ground, _carActions);
        _backingUp = new BackingUp(_ground, _carActions, _overtaking);
        _walkingGround = new WalkingGround(People, _occupancy, _ways, _lines, _walking, config);
        _personActions = new PersonActions(People);
        _sidestepping = new Sidestepping(_walkingGround, _personActions);
        _walkingItsRoute = new WalkingItsRoute(_walkingGround, _personActions, _sidestepping, _progress);
        _walkingBack = new WalkingBack(_walkingGround, _personActions);
        _walkingToThePost = new WalkingToThePost(_walkingGround, _personActions);
        _standsParked = new bool[drivers];
        _stood = new bool[drivers];
        _stoodTick = new StoodTick[drivers];
        _refusedALane = new bool[drivers];
        _tours = new bool[drivers];
        _drivesOnFrom = new int[drivers];
        Array.Fill(_drivesOnFrom, CarFleet.NoLane);
        _exitRefusedAtS = new float[drivers];
        Array.Fill(_exitRefusedAtS, float.NegativeInfinity);
        _asksTheRoute = new bool[drivers];
        _wounded = new AgentSet(walkers);
        _wrecked = new AgentSet(drivers);
        _standing = new StandingCars(drivers);
        _behindTheBar = new bool[drivers];
        _lightsAhead = new LightsAhead(drivers);
        _sentCommand = new DriveCommand[drivers];
        Array.Fill(_sentCommand, DriveCommand.Parked);
        _sentHand = new bool[drivers];
        _sentAimM = new Vector2[walkers];
        _sentWalk = new ushort[walkers];
        Array.Fill(_sentWalk, NeverSent);
        _sentShow = new ushort[drivers];
        _followedLaid = new uint[drivers];
        Array.Fill(_followedLaid, NeverFollowed);
        _followSerial = new ushort[drivers];
        _progressRead = new float[drivers];
        _carSetBy = new long[drivers];
        _walkerSetBy = new long[walkers];
        _walkLaid = new WalkLaid[walkers];
        Array.Fill(_walkLaid, WalkLaid.Never);
        _crewOf = new int[walkers];
        Array.Fill(_crewOf, NoCar);

        _containers = new Containers(_plan.Buildings.Capacity, drivers, People.Inside);
        _parking = ParkingRegistry.Build(plan, _bayStreets, config, drivers);
        OpenWatch.Passed("town: fleets and parking");
        LayThePlaces(walkers, drivers);
        OpenWatch.Passed("town: places");
        LayTheBaysAndDoorsOntoTheLanes();
        OpenWatch.Passed("town: bays and doors onto the lanes");
        _bays = new BayManoeuvring(_ground, _carActions, _manoeuvres, _bayStreets);
        _pullingOut = new PullingOut(_ground, _bays, _parking, _bayStreets);
        _parkingIn = new ParkingIn(_ground, _carActions, _bays, _parking, _bayStreets);
        _following = new Following(_ground, _carActions, _overtaking, _switching, _parkingIn);
        _rejoining = new Rejoining(_ground, _carActions, _following);
        _round = new ParkedRound(drivers);
        LayTheKerbs();
        _showroom = ShowroomPlan.ShowsEachLookOnce(plan.Name);
        _townParks = _kerbs.Count > 0;
        for (var bay = 0; bay < _parking.PaintedCount && !_townParks; bay++) _townParks = _parking.CanBeReached(bay);

        // How close each driver has come to the end of the way it is on, which is the clock a leg is
        // given up by — the walkers' own, over the cars (<see cref="LegProgress"/>).
        _driveProgress = new LegProgress(drivers);
        _carOrders = new PlayerOrders(drivers);
        _duty = new RescueDuty(drivers);
        _beat = new PatrolDuty(drivers, config.Service.ClosureMostLanes);
        _closedLanes = new bool[_roads.LaneCount];
        _closedLinks = new bool[_driving.Graph.LinkCount];
        _recovery = new RecoveryDuty(drivers);
        _hitches = new TowHitches(drivers, _trains.WagonCount);
        _serviceBeat = new ServiceBeat(drivers);
        LayTheDistrictStreets();
        OpenWatch.Passed("town: kerbs and district streets");

        if (standStatics) StandStatics();
        LayTheExits();
        OpenWatch.Passed("town: furniture and exits");

        // The aprons are claimed before the plan's cars are stood and filled after (GEN-4k): a bay a
        // spawned car is already standing in is not a bay a hospital can have, and a bay a hospital has is
        // not one the plan may spawn into.
        HoldTheAprons();
        Spawn();
        StandTheServiceVehicles();
        ListTheServiceVehicles();

        // <b>The roster is the agents there actually are</b>, and not the room that was laid for them: a
        // hospital with no bay near it stands no ambulance, and a slot nothing was put in is a body the
        // decision loop would hand a tick to and the solver has never heard of.
        Roster = new AgentRoster(People.Count, Cars.Count);
        OpenWatch.Passed("town: spawned");
        if (worldMaker is null)
        {
            _world = new ManagedWorld(LayingForTheWorld);
        }
        else
        {
            _world = worldMaker.Lay(LayingForTheWorld);
            LetGoOfTheGround();
        }

        // After the world, which reads every body onto its ways as it is stood: a car takes the lane its wheels are on.
        DriveTheEmptyMap();
        SeatTheOwners();
        OpenWatch.Passed("town: its world");

        StoodMs = Stopwatch.GetElapsedTime(laid.StartedAt).TotalMilliseconds;
    }

    /// <summary>
    /// <b>What laying the town has left behind, collected and given back</b>, either side of the atlas: the
    /// open's largest working set, laid on top of what the networks left, with the fleets, the claims and the
    /// bodies laid straight after it. Only for a town of <see cref="SimFigures.CollectTheLayAboveKm2"/> or more.
    /// </summary>
    /// <remarks>
    /// Aggressive, because a collection that keeps what it freed committed lays the next stage into reused room,
    /// which the runtime clears page by page — and a table that is mostly room, laid on pages nothing has written,
    /// holds none of that room in memory (root <c>decision-log.md</c>).
    /// </remarks>
    static void CollectTheLay(CityPlan plan, SimConfig config)
    {
        if (plan.WorldSizeM.X * plan.WorldSizeM.Y < config.Sim.CollectTheLayAboveKm2 * SquareMetresPerKm2) return;

        ClearTheStack();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }

    /// <summary>
    /// <b>The stack below the caller written over before a collection</b>: a page's runtime reads its stack conservatively,
    /// so a table a returned call last pointed at is kept for as long as nothing writes over the slot — and the
    /// collector's own frames leave most of them as they were. On a traced city that is the gigabyte of atlas a device
    /// world has just been laid off (root decision-log.md, 2026-10-15).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    static int ClearTheStack()
    {
        Span<byte> below = stackalloc byte[StackClearedBytes];
        below.Clear();
        return below[^1];
    }

    /// <summary>How much of the stack below a collection's caller is written over: deeper than an open's calls reach.</summary>
    const int StackClearedBytes = 256 * 1024;

    const float SquareMetresPerKm2 = 1e6f;

    /// <summary>
    /// <b>What the lay and the handover let go of, collected</b> — by a caller whose own frames never held it: a frame still
    /// standing that held the managed world keeps it, the atlas with it, on a runtime that reads its stack conservatively
    /// (<see cref="ClearTheStack"/>).
    /// </summary>
    public void CollectWhatTheLayLeft() => CollectTheLay(_plan, _config);

    /// <summary>
    /// <b>What standing this town up cost</b>, and how much of that went on each of the three graphs that
    /// dominate it (<c>--bench load</c>). The town is stood once, so the readings are the object's own and
    /// not a probe's second run of the same work.
    /// </summary>
    /// <remarks>
    /// <b>Three and not every stage</b>: the graphs are the seconds, and everything else the constructor does
    /// — the fleets, the tables they are numbered in, the roster and the spawn — is the remainder the probe
    /// prints against <see cref="StoodMs"/>, and the stages it says as they pass (<see cref="OpenWatch"/>) are what
    /// breaks that down. A row a line of the constructor would be an instrument nobody could read.
    /// </remarks>
    public double StoodMs { get; }

    public double RoadsMs { get; }

    public double FootMs { get; }

    public double WalkingMs { get; }

    public double AtlasMs { get; }

    /// <summary>
    /// How many runs one search may return. A bound on the work rather than a figure behaviour reads: a
    /// section is a few cells (<see cref="RouteSearch"/>) and fits many times over, and only a search asked
    /// over the whole graph can come back longer — which is refused, and the leg given up by its own clock.
    /// </summary>
    const int MostRunsInARoute = 256;

    /// <summary>
    /// How many ways each network's table may price up at once. The walking network's is never marked and stands
    /// empty; it is there because a search with no table would be a second code path.
    /// </summary>
    const int MostWaysGivenUpOn = 64;

    /// <summary>The spawn kinds the format carries.</summary>
    const byte SpawnKindPerson = 0;

    const byte SpawnKindCar = 1;

    /// <summary>
    /// <b>The cars a plan's own people and spawns can stand</b>: a car of the plan's for each car spawn, and one of
    /// their own for each person, where a bay near their door is free (PER-29) — the room the fleet is laid with
    /// before the service vehicles go on top of it, and what anything drawing the fleet lays its room for.
    /// </summary>
    /// <remarks>
    /// Nobody owns more cars than the plan has bays to stand them in, so a town that cut no car park lays no room for
    /// its people's: every claim slot a car's room reserves is its worst case on every way it could be over.
    /// </remarks>
    public static int CarsOfThePlan(CityPlan plan) =>
        Math.Min(PeopleIn(plan), plan.CarParks.Road.Length) + CountOf(plan, SpawnKindCar);

    static int PeopleIn(CityPlan plan) => CountOf(plan, SpawnKindPerson);

    static int CountOf(CityPlan plan, byte kind)
    {
        var count = 0;
        foreach (var spawned in plan.Spawns.Kind)
        {
            if (spawned == kind) count++;
        }

        return count;
    }

    public PersonFleet People { get; }

    public CarFleet Cars { get; }

    /// <summary>What the traffic has written on the ground: the world's (<see cref="WorldLayer.Marks"/>), for whoever draws it.</summary>
    internal DriftMarks Marks => _world.Marks;

    /// <summary>The solver itself, where the world steps it in managed code, for an instrument that measures it or draws it.</summary>
    internal PhysicsWorld? PhysicsForInstruments => _world.Solver;

    /// <summary>
    /// <b>The cars whose bodies meet a box, in index order and each once</b> — what a picture of the box draws, read
    /// off the world's own grids where it keeps them (<see cref="WorldLayer.InBox"/>), so a frame of a street pays for the
    /// street and not for the town. A box spanning more cells than the town has cars, or a world keeping no grid the host
    /// reads or stepping its bodies as it is asked, is answered off the fleet's own places as the view left them: one
    /// pass over the roster, each body kept whose reach meets the box.
    /// </summary>
    /// <param name="into">At least the fleet's <see cref="CarFleet.Capacity"/> long.</param>
    /// <returns>The part of <paramref name="into"/> written.</returns>
    public ReadOnlySpan<int> CarsIn(Vector2 leastM, Vector2 mostM, Span<int> into) =>
        InBox(BodyKind.Car, Cars.Count, leastM, mostM, into);

    /// <summary>And the people, on the same terms. Nobody inside a building or a car is among them: their bodies are put away (PHY-7).</summary>
    /// <param name="into">At least the roster's <see cref="PersonFleet.Capacity"/> long.</param>
    public ReadOnlySpan<int> PeopleIn(Vector2 leastM, Vector2 mostM, Span<int> into) =>
        InBox(BodyKind.Person, People.Count, leastM, mostM, into);

    ReadOnlySpan<int> InBox(BodyKind kind, int roster, Vector2 leastM, Vector2 mostM, Span<int> into)
    {
        var found = _world.InBox(leastM, mostM, kind, roster, into);
        if (found < 0) return kind == BodyKind.Car ? CarsPlacedIn(leastM, mostM, into) : PeoplePlacedIn(leastM, mostM, into);

        var units = into[..found];
        units.Sort();
        var kept = 0;
        for (var at = 0; at < units.Length; at++)
        {
            if (kept == 0 || units[at] != units[kept - 1]) units[kept++] = units[at];
        }

        return units[..kept];
    }

    /// <summary>The cars within the fleet's widest half-diagonal of the box from where the view left them, in index order.</summary>
    ReadOnlySpan<int> CarsPlacedIn(Vector2 leastM, Vector2 mostM, Span<int> into)
    {
        if (_carReachM < 0f)
        {
            for (var variant = 0; variant < CarCatalog.Shared.SheetCount; variant++)
            {
                ref readonly var build = ref _builds.Of(variant);
                _carReachM = MathF.Max(_carReachM, new Vector2(build.LengthM, build.WidthM).Length() * 0.5f);
            }
        }

        var reachM = _carReachM;
        if (_carsCulled.Holds(_clockTicks, _placements, Cars.Count, reachM, leastM, mostM)) return _carsCulled.Into(into);

        // The roster's fields read once: a store into the caller's span is a store the JIT cannot tell from one into
        // them, and read in the loop they were read again a car.
        var kept = 0;
        var positions = Cars.PositionM.AsSpan(0, Cars.Count);
        for (var car = 0; car < positions.Length; car++)
        {
            if (Meets(positions[car], reachM, leastM, mostM)) into[kept++] = car;
        }

        return _carsCulled.Keep(_clockTicks, _placements, Cars.Count, reachM, leastM, mostM, into[..kept], Cars.Capacity);
    }

    readonly KeptCull _carsCulled = new(), _peopleCulled = new();

    /// <summary>Counted on wherever a body is put down out of a tick, which is the one place one moves between two.</summary>
    int _placements;

    /// <summary>
    /// <b>A roster's cull kept for the frames between two ticks</b>: nothing moves a body or puts one away between them
    /// but a placement, so the same box asked again before the next tick or placement is answered as it was — at a
    /// window's uncapped rate, eighty frames a tick that would each walk the whole roster.
    /// </summary>
    sealed class KeptCull
    {
        int[] _bodies = [];
        int _count;
        long _clockTicks = -1;
        int _placements, _roster;
        float _reachM;
        Vector2 _leastM, _mostM;

        public bool Holds(long clockTicks, int placements, int roster, float reachM, Vector2 leastM, Vector2 mostM) =>
            clockTicks == _clockTicks && placements == _placements && roster == _roster && reachM.Equals(_reachM)
            && leastM.Equals(_leastM) && mostM.Equals(_mostM);

        public ReadOnlySpan<int> Into(Span<int> into)
        {
            _bodies.AsSpan(0, _count).CopyTo(into);
            return into[.._count];
        }

        /// <param name="capacity">The roster's room, which the kept cull is laid at once so it never grows.</param>
        public ReadOnlySpan<int> Keep(
            long clockTicks, int placements, int roster, float reachM, Vector2 leastM, Vector2 mostM, ReadOnlySpan<int> culled,
            int capacity)
        {
            if (_bodies.Length < capacity) _bodies = new int[capacity];

            culled.CopyTo(_bodies);
            (_count, _clockTicks, _placements, _roster, _reachM, _leastM, _mostM) =
                (culled.Length, clockTicks, placements, roster, reachM, leastM, mostM);
            return culled;
        }
    }

    /// <summary>The widest half-diagonal of any build of the fleet, found the first time a box is asked of the fleet itself.</summary>
    float _carReachM = -1f;

    /// <summary>And the people, nobody put away inside a building or a car among them (PHY-7).</summary>
    ReadOnlySpan<int> PeoplePlacedIn(Vector2 leastM, Vector2 mostM, Span<int> into)
    {
        var reachM = _config.PersonDiameterM * 0.5f;
        if (_peopleCulled.Holds(_clockTicks, _placements, People.Count, reachM, leastM, mostM)) return _peopleCulled.Into(into);

        var kept = 0;
        var positions = People.PositionM.AsSpan(0, People.Count);
        var inside = People.Inside.AsSpan(0, positions.Length);
        for (var person = 0; person < positions.Length; person++)
        {
            if (!inside[person].Any && Meets(positions[person], reachM, leastM, mostM)) into[kept++] = person;
        }

        return _peopleCulled.Keep(
            _clockTicks, _placements, People.Count, reachM, leastM, mostM, into[..kept], People.Capacity);
    }

    static bool Meets(Vector2 atM, float reachM, Vector2 leastM, Vector2 mostM) =>
        atM.X + reachM >= leastM.X && atM.X - reachM <= mostM.X && atM.Y + reachM >= leastM.Y && atM.Y - reachM <= mostM.Y;

    public RoadGraph Roads => _roads;

    /// <summary>The street every bay is worked off, for whoever draws it or measures it.</summary>
    public BayStreets BayStreets => _bayStreets;

    /// <summary>
    /// <b>Who is on each way of the town this tick</b>, for whoever draws it or measures it. <b>The claims
    /// are nobody's roster and no one surface's</b>: a body on the carriageway is a stretch of the lane it
    /// stands in, a car over a zebra a band of the walk it crosses, and a car on a kerb a stretch of the
    /// footway under it.
    /// </summary>
    public LaneOccupancy Occupancy => _occupancy;

    /// <summary>Every way in the town and what kind of ground each is, for whoever holds a way number.</summary>
    public TownWays Ways => _ways;

    /// <summary>The line every way is travelled on, its rails' among them — what the atlas was laid from.</summary>
    public WayLines Lines => _lines;

    /// <summary>
    /// The ground of every way and which ways share it, for whoever measures or draws it — <b>only while the world is
    /// the managed one</b>; the device's keeps its own.
    /// </summary>
    public RibbonAtlas Atlas => _atlas ?? throw new InvalidOperationException("The town's atlas is the device's once the world is.");

    /// <summary>
    /// The only thing that knows what colour anything is. No agent reads it: a light holds ground
    /// (<see cref="SignalHolds"/>), and this is for the picture and the instruments.
    /// </summary>
    public SignalService Signals => _signals;

    /// <summary>The tick the light over an approach lane last took its turn (<see cref="SignalHolds.TurnedAt"/>), for an instrument.</summary>
    public long LightTurnedAt(int lane) => _signalHolds.TurnedAt(lane);

    /// <summary>The heads, for whoever draws them. No agent reads one.</summary>
    public SignalHeads Heads => _heads;

    /// <summary>The bars this town paints, which the lights hold from (<see cref="StopBars"/>).</summary>
    public StopBars Bars => _bars;

    /// <summary>The town's own clock, which is what a phase is derived from.</summary>
    public float ElapsedS => _elapsedS;

    /// <summary>
    /// How many times a car has crossed a painted stop line its own approach was showing red at, counted
    /// as it happens rather than sampled.
    /// </summary>
    /// <remarks>
    /// <b>A light holds nobody who can no longer stop short of its bar</b> (TER-5e), so a car crosses one on a
    /// red the way anything crosses ground it did not mean to — committed when the amber ran out, or shunted
    /// over it (CAR-13.3). A car on a call is not counted: the light's hold is below its rung (AMB-4), so it
    /// breaches nothing.
    /// </remarks>
    public long RedBarCrossings { get; private set; }

    /// <summary>
    /// How many times the driving network has been searched at all — the bay screened before it is
    /// claimed, and the route laid from where a car stands.
    /// </summary>
    /// <remarks>
    /// <b>A leg is routed once and driven, and this is what says so.</b> Against the legs begun over the
    /// same window it is the only reading that tells a router asked once from one asked again every time
    /// a car reaches a junction — a fault nothing else here can show, because both towns drive the same.
    /// </remarks>
    public long RouteSearches { get; private set; }

    /// <summary>How many walks have ended where they were going. The same figure for the other agent kind.</summary>
    public long WalkArrivals { get; private set; }


    /// <summary>
    /// And how many ended because the body stopped making progress for long enough to give up. The two
    /// are the whole of how a leg ends; reporting only the first would call a jammed town a busy one.
    /// </summary>
    public long WalksGivenUp { get; private set; }

    /// <summary>What a wheel on a way and off one stands on, for the figures as they stand (TER-7b).</summary>
    public WheelGrounds WheelGrounds { get; private set; }

    public CityPlan Plan => _plan;

    /// <summary>The air over the town, off its plan's seed (<see cref="Town.Wind"/>).</summary>
    public Wind Wind { get; }

    /// <summary>The clouds over it, carried the way that air blows (<see cref="Town.Clouds"/>).</summary>
    public Clouds Clouds { get; }

    /// <summary>
    /// The pavement's own fine graph, and the two contracted networks over it and the roads.
    /// </summary>
    /// <remarks>
    /// All three are laid with the town because the tick reads all three, and a structure the tick needs
    /// belongs to the standing cost <c>--bench town</c> reports.
    /// </remarks>
    public FootGraph Foot => _foot;

    /// <summary>
    /// The town's pavement as the lanes it is walked down (WLK-1): the driven ground's boundary moved off
    /// itself once per lane. <b>It is what <see cref="Foot"/> is laid from</b> and not a second answer
    /// beside it — the graph holds these very lines, cut at the joints of the pieces the move came back
    /// with, so a reader wanting the shape reads this and one wanting the network reads that.
    /// </summary>
    public PavementLanes PavementLanes => _pavementLanes;

    /// <summary>
    /// The ways the town's zebras are walked and the places they part that pavement at (WLK-15). <b>Kept
    /// because the graph does not hold it</b>: a crossing there is a kind of lane and the place it meets the
    /// walk is a node like any other, so what is asked of this is which places those were.
    /// </summary>
    public CrossingWays CrossingWays => _crossingWays;

    public WalkingNetwork Walking => _walking;

    /// <summary><b>Which crossing each stretch of the foot graph is</b> — which crossing a walk is on.</summary>
    public CrossingEdges CrossingEdges => _crossingEdges;

    public DrivingNetwork Driving => _driving;

    /// <summary>The figures the town was stood with.</summary>
    internal SimConfig Config => _config;

    public int StaticBodyCount => _statics?.Count ?? _staticBodyCount;

    public int IntegratedBodyCount => _world.IntegratedBodyCount;

    public int FrozenBodyCount => _world.FrozenBodyCount;

    /// <inheritdoc cref="WorldLayer.AllocatedAsideBytes"/>
    public long AllocatedAsideBytes => _world.AllocatedAsideBytes;

    /// <summary>The walkers, then the cars — the flat index space the decision clock staggers.</summary>
    /// <remarks>
    /// Held rather than made on each read: neither fleet is ever resized, so the decode is the same two
    /// numbers for the life of the town, and every phase asks for it several times per agent per tick.
    /// </remarks>
    public AgentRoster Roster { get; }

    public int AgentCount => Roster.Count;

    /// <summary>
    /// A broken car takes no further actions, so it is never asked to think. It is still stepped, struck
    /// and pushed — a terminal body leaves the roster's decisions, never the world.
    /// </summary>
    /// <remarks>
    /// <b>No walker is ever terminal</b> (PHY-3): the worst a contact does to a person is put them in the
    /// road, and a casualty is coming back (PER-18). What holds them out of the decisions while they are
    /// down is <see cref="PersonFleet.Acts"/> and not this.
    /// </remarks>
    public bool IsTerminal(int agent) => Roster.IsCar(agent) && Cars.Broken[Roster.CarIndex(agent)];

    /// <summary>
    /// <b>Nobody decides on every tick.</b> What has to be asked at that rate is the sensing and the
    /// claims, which are not decisions and are taken in the tick itself (<see cref="TickCar"/>); what a
    /// decision is here is a leg's next line, which is about distances of tens of metres.
    /// </summary>
    public bool DecidesEveryTick(int agent) => false;


    /// <summary>
    /// Whether the town keeps <see cref="Sub"/> — the drill-down inside phases 3 and 4 — which is off
    /// unless something is looking, on the same footing as the loop's own phase timing.
    /// </summary>
    public bool Timed { get; set; }

    /// <summary>Which kind of agent phase 3 spent its time on, and how much of phase 4 was the solver's step.</summary>
    public TickParts Sub;

    /// <summary>Whether the agent pass under way has reached the cars, so that each crossing is stamped once rather than per agent.</summary>
    bool _decidingCars;

    /// <summary>And whether it is the decisions' pass rather than the ticks'.</summary>
    bool _inTheDecisions;

    /// <summary>
    /// <b>The agents take their turns on a view whose step read the covers</b> (<see cref="WorldView.CoversRead"/>), which
    /// every step does — between two of its turns each agent holds to the grant it was given, walked in by the ground it
    /// has covered since (<see cref="CarFleet.GrantLeftM"/>). This is the batch of signals
    /// (<see cref="WorldView.SignalsTaken"/>) of the view the turns were last taken off, so a view shown for two ticks is
    /// taken off once.
    /// </summary>
    long _turnsTakenOff = -1;

    /// <summary>
    /// Whether the turns were taken this tick — so whoever reads what the layer holds against where the bodies are
    /// reads it on a tick the two are of one instant.
    /// </summary>
    public bool TurnsTakenThisTick { get; private set; }

    /// <summary>
    /// <b>Every agent's turn taken now, off the clock</b> — for whoever has put something down and asks what the layer
    /// makes of it. The tick takes the turns of the agents whose turn it is (<see cref="TakeTheTurns"/>).
    /// </summary>
    public void LayTheClaims()
    {
        _world.ReadTheCovers();
        ReadTheWorld();
        TakeTheTurns(everyone: true, tick: 0);
        _turnsTakenOff = _world.View.SignalsTaken;
        TurnsTakenThisTick = true;
    }

    public void RebuildProximityIndex()
    {
        TakeUpTheHands();
        LayTheClosures();
        DriveTheEmptyMap();
        MendTheYards(_config.TickSeconds);

        var view = _world.View;
        TurnsTakenThisTick = view.CoversRead && view.SignalsTaken != _turnsTakenOff;
        if (TurnsTakenThisTick) _turnsTakenOff = view.SignalsTaken;

        // Phase 3 begins on the walkers' ticks, which is the end of the roster the loop walks first.
        if (!Timed) return;

        _decidingCars = false;
        _inTheDecisions = false;
        Sub.Begin();
    }

    /// <summary>
    /// Five timestamps a tick, not one an agent: the roster is walkers then cars and phase 3 walks it twice — the
    /// ticks, then the decisions — so the only instants worth marking are where a pass crosses from one kind to the
    /// other, or from the ticks to the decisions. Timing each of five hundred agents would cost several percent of the
    /// tick this is measuring.
    /// </summary>
    /// <remarks>
    /// Read off the agent that actually ran rather than off the roster's boundary, because a terminal
    /// agent is never handed here: a wrecked car at the head of the fleet would otherwise leave the
    /// crossing unmarked and put every car's time on the walkers.
    /// </remarks>
    void AccountFor(int agent, bool deciding)
    {
        var car = Roster.IsCar(agent);
        if (!Timed || (_decidingCars == car && _inTheDecisions == deciding)) return;

        Sub.Mark(ref SpentSoFar());
        _decidingCars = car;
        _inTheDecisions = deciding;
    }

    /// <summary>Where the stretch of phase 3 since the last mark goes: whose, and whether ticks or decisions.</summary>
    ref long SpentSoFar() =>
        ref _inTheDecisions
            ? ref _decidingCars ? ref Sub.CarDecisionTicks : ref Sub.WalkerDecisionTicks
            : ref _decidingCars ? ref Sub.CarTicks : ref Sub.WalkerTicks;

    /// <summary>
    /// <c>Pause</c>: <b>the decide loop is skipped and every car is told its controller is paused,
    /// while the bodies keep stepping</b> — so physics, contacts and damage run on.
    /// </summary>
    /// <remarks>
    /// <b>Nothing is unwound.</b> Routes, lines, junction claims and states all survive, and no stuck
    /// timeout runs up while the town stands still, because the clock those run on is the decision
    /// loop that is being skipped. The hand-driven agent keeps deciding, so a held town can still be
    /// driven around.
    /// </remarks>
    public bool HoldAgents { get; set; }

    /// <summary>
    /// Whether somebody has this agent's wheel — the player's own hand over the selection, or the second
    /// driver's over the car it named (CTL-5d). <b>A terminal unit is not handed</b>: a selection may hold a
    /// wreck and a working car at once, so the wheel is refused per unit rather than per selection.
    /// </summary>
    bool Handed(int agent) =>
        !IsTerminal(agent) &&
        (Roster.IsCar(agent)
            ? WheelIsHeldOver(Roster.CarIndex(agent))
            : _hands.Held && _selected.Holds(SelectionKind.Person, agent));

    /// <summary>The decisions' clock, the loop's own (<c>SimLoop.Decisions</c>).</summary>
    readonly TickGroups _decisions;

    public void TickTheAgents()
    {
        RunTheTrains();
        var world = this;
        EveryAgent.Tick(ref world);
    }

    /// <summary>
    /// <b>The agents whose turn it is decide, then claim</b> (<see cref="TakeTheTurns"/>): what each claims is on the line
    /// it has just decided on, so a line laid again is planned down the tick it is laid.
    /// </summary>
    public void DecideTheAgents(long tick, TickGroups decisions)
    {
        var world = this;
        EveryAgent.DecideTheTurn(ref world, tick, decisions);
        if (TurnsTakenThisTick) TakeTheTurns(everyone: false, tick);
    }

    public void TickAgent(int agent)
    {
        AccountFor(agent, deciding: false);
        if (HoldAgents && !Handed(agent))
        {
            Paused(agent);
            return;
        }

        if (Roster.IsCar(agent))
        {
            TickTheCar(Roster.CarIndex(agent));
            return;
        }

        // CTL-6's seam, read every tick: the goal is substituted and nothing under it is, so what follows cannot
        // tell this walker from any other. Only a body in the world and on its feet takes a hand (PHY-7, PER-18).
        if (!People.Inside[agent].Any && People.Acts(agent) && _hands.Held && _selected.Holds(SelectionKind.Person, agent))
        {
            HandWalk(agent);
        }

        TickTheWalker(agent, paused: false);
    }

    /// <summary><b>A car's own tick by the town</b>, nothing for one that stands parked.</summary>
    public void TickTheCar(int car)
    {
        // The roster ticks its cars in order, and each car's line is a block of its own that order meets cold.
        var ahead = car + PrefetchAhead;
        if (ahead < Cars.Count && !_standsParked[ahead]) Cars.PrefetchTheLine(ahead);

        if (StandsParked(car))
        {
            // Parked is the showroom's whole state, so its lamps are signalled here: in turn while picked out (CAR-14.9),
            // and dark again the tick after.
            if (_showroom && (ShowsItsLampsInTurn(car) || Cars.Lamps[car] != CarLampSet.None)) SignalTheShow(car);
            return;
        }

        _standsParked[car] = false;
        if (TicksAsBefore(car)) return;

        var commandWas = Cars.Command[car];
        var progressWasM = Cars.ProgressM[car];
        TickCar(car);
        SignalTheShow(car);
        SignalTheFollow(car);
        KeepTheTick(car, commandWas, progressWasM);
    }

    /// <summary>
    /// <b>A walker's own tick</b>: at the aim settled when the grant was read (<see cref="AimTheWalker"/>), handed to the
    /// world, whose footing walks it there (<see cref="SignalTheWalk"/>).
    /// </summary>
    void TickTheWalker(int person, bool paused) => SignalTheWalk(person, paused);

    public void DecideAgent(int agent, float sinceLastDecisionS) => DecideNow(agent, sinceLastDecisionS);

    void DecideNow(int agent, float sinceLastDecisionS)
    {
        AccountFor(agent, deciding: true);

        // Held: no decision, and — the load-bearing half — no clock running up either, so nothing is
        // stuck by having stood still while the town was paused.
        if (HoldAgents && !Handed(agent)) return;

        // Which way to go at the junction ahead is decided when the line is re-laid, not on the clock:
        // the interval is a floor on staleness, never a ceiling on thinking. What is taken here is the
        // leg's decision — the next line where the one in hand is spent, and whether the leg is getting
        // anywhere (<see cref="DecideDriver"/>).
        if (Roster.IsCar(agent))
        {
            // The errand before the driving (AMB-5): what comes out of it is a destination and a chain,
            // and the leg's own decision below is what drives them.
            var car = Roster.CarIndex(agent);

            // EVA-5: what is on a bar takes no decisions, the way a casualty on a stretcher takes none.
            // Its errand is still its own and it picks it up again where the arm puts it down.
            if (_hitches.IsHeld(car)) return;

            // CTL-2: the player's order pins the goal the behaviour would otherwise have picked, and for a
            // vehicle with an errand the errand is that behaviour — so an ordered one runs the order in
            // place of it, and picks its errand back up where the reset leaves it (CTL-4).
            if (IsUnderOrders(car)) RunTheOrder(car);
            else if (Cars.Ambulance[car]) RunTheRescue(car, sinceLastDecisionS);
            else if (IsAnEvacuator(car)) RunTheRecovery(car, sinceLastDecisionS);
            else if (IsAPatrolCar(car)) RunThePatrol(car, sinceLastDecisionS);
            else if (Cars.Owner[car] != CarFleet.NoOwner) RunTheOwnersCar(car, sinceLastDecisionS);
            else if (_townParks && !_showroom && !_tours[car] && !IsAServiceVehicle(car)) RunTheRound(car, sinceLastDecisionS);

            DecideDriver(car, sinceLastDecisionS);
            SignalTheFollow(car);
            return;
        }

        // PER-18: a casualty takes no actions. It is not terminal — an ambulance is on its way — so the
        // body is still stepped, struck and pushed like any other; what it has stopped doing is deciding.
        if (People.Wounded[agent]) return;

        // PER-30: a look on show stands still and takes no trip.
        if (IsOnShow(agent) && !People.Inside[agent].Any)
        {
            StandOnShow(agent);
            return;
        }

        // Inside a building or a car there is no line to hold and no ground to be on: what runs is the
        // trip, and the whole action set is the container's.
        if (People.Inside[agent].Any)
        {
            DecideContained(agent, sinceLastDecisionS);
            return;
        }

        // An officer on duty is the police car's errand on foot (SRV-11): where they stand is the car's to say,
        // and none of a trip's clocks or walks runs for them.
        if (People.Stage[agent] == TripStage.OnDuty) return;

        // A walk to a car is over once its owner can touch the car's way in, wherever the walk has got to (PER-29): the
        // last of it is across a car park whose bays hold their neighbours' bodies, and an owner held off one of them a
        // stride from their own door is not a walk going nowhere.
        if (People.Stage[agent] == TripStage.WalkingToTheCar && HasReached(agent, _config.WayInTouchingReachM))
        {
            TakeTheWheel(agent);
            return;
        }

        // PER-28: getting past somebody on its way is decided, waited for and given up on here, on the walk's own clock.
        _sidestepping.Decide(agent, sinceLastDecisionS);

        // A beat stood on purpose is not a leg going wrong. It is the walking side's own idle between two
        // goals, and a clock that gave a leg up while it ran would end the stand rather than the stand
        // ending itself.
        if (!People.Walking[agent] && People.Stage[agent] == TripStage.StandingBy)
        {
            StandingStill(agent, sinceLastDecisionS);
            return;
        }

        // <b>PER-25's second half, asked once a decision.</b> A body further off its line than the pavement
        // is wide has lost it — shoved, knocked aside, put down beside a vehicle — and what it is owed is
        // the line laid again from where it now stands, whose first leg is the straight back onto the
        // network. Left alone it would walk at a point on ground it is no longer near.
        //
        // <b>And a chain that ran out of room is laid on from where the body has got to before its plan runs off
        // the end of it</b>, which is the same laying from the same place (<see cref="ComesToTheEndOfItsChain"/>).
        //
        // <b>Never for one already walking back</b> (PER-25): its walk is laid from where it last stood on the pavement
        // (<see cref="_placeOf"/>), and the straight back there is what it is doing — laid again, it would be handed back
        // to its route each decision and walked back to it the laying after.
        if ((HasLostItsLine(agent) && People.Action[agent] != PersonAction.Rejoin) || ComesToTheEndOfItsChain(agent))
        {
            LayWalkAgain(
                agent,
                reachTheGoal: People.Stage[agent] is TripStage.WalkingToTheDoor or TripStage.WalkingToTheCar
                    or TripStage.WalkingFromTheCar or TripStage.UnderOrders);

            // A route that could not be laid again leaves nothing to walk (PER-25). Left walking, the body
            // would set off at its goal in a straight line over whatever lay between.
            SetWalking(agent, People.RouteCount[agent] > 0);
        }

        // <b>And the clock runs through that.</b> A body that has lost its line is a body that has got nowhere, and a
        // line laid again from the same place is the same answer: restarted here, a walker wedged against a wall with
        // the pavement four metres through it would be re-laid and restarted for the rest of the run, never walking
        // and never giving up.
        //
        // <b>A light holding the crossing in front is the one wait that spends no clock</b>, as it is a driver's
        // (TLT-2a): it will change on its own, so the wait is bought and the standing is not given back.
        RunTheWalksClock(agent, sinceLastDecisionS);

        // Standing here means the follower has already answered: either it arrived, or it never had
        // anywhere to go. Being stuck is the other way a leg ends, and it is the one that needs a
        // clock — a walker held up by something is not a walker that has finished.
        var stuck = _progress.IsStuck(agent, _config.Person.GivesUpAfterS);
        if (People.Walking[agent] && !stuck) return;

        if (stuck)
        {
            // Held up long enough to give up on where it was going. Not an arrival and not counted as
            // one: conflating the two would report a jammed town as a busy one.
            WalksGivenUp++;

            // PER-8: and where it is off the network as well as getting nowhere, the leg it draws next
            // will not move it either — so it is set down on the pavement rather than left to draw
            // destinations it cannot walk to.
            PutItBackOnThePavement(agent);

            // A failed order runs the normal recovery and ends in idle-awaiting-orders rather than in a
            // new goal of the walker's own.
            if (People.Manual[agent])
            {
                SetWalking(agent, false);
                People.Stage[agent] = TripStage.UnderOrders;
                return;
            }

            GiveUpTheTrip(agent);
            _progress.Restart(agent);
            return;
        }

        // Standing still with nowhere left to walk: the stage is what says whether that is an arrival,
        // a wait, or a leg that ran out short of where it was going.
        StandingStill(agent, sinceLastDecisionS);
    }

    /// <summary>The world let go of: the thread a managed world steps on, or the device's buffers.</summary>
    public void Dispose() => _world.Dispose();
}
