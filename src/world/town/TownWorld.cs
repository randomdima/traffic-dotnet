using System.Numerics;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Car.Control;
using TrafficSimulation.Agents.Car.Maneuvers;
using TrafficSimulation.Agents.Evacuator;
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
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;
using TrafficSimulation.World.Statics;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.World.Town;

/// <summary>A car that drove through a painted stop line its own approach was showing red at.</summary>
internal readonly record struct RedBarCrossing(int Car, Vector2 AtM, float SpeedMps);

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
    readonly TerrainGrid _terrain;
    readonly PhysicsWorld _physics;
    readonly BucketGrid _nearby;

    readonly RoadGraph _roads;

    /// <summary>Who is on each way of the road, rebuilt from the bodies in phase 2 — <see cref="RebuildLaneOccupancy"/>.</summary>
    readonly LaneOccupancy _occupancy;

    /// <summary>And who is on each way of the pavement — <see cref="RebuildFootOccupancy"/>. Two networks, two books.</summary>
    readonly LaneOccupancy _footfall;
    readonly SignalService _signals;
    readonly SignalHeads _heads;

    /// <summary>The paint each lane meets — its stop bar and the crossings across it — projected once at load.</summary>
    readonly LaneFurniture _furniture;

    /// <summary>And the town's furniture, as the stretches of lane it stands on — laid into the book every tick.</summary>
    readonly StandingGround _standing;

    /// <summary>Whether each car's nose was behind its approach's painted bar last tick — the other half of a crossing event.</summary>
    readonly bool[] _behindTheBar;

    /// <summary>A search over the road network: one entry, since a car is on the lane it is on.</summary>
    readonly RouteSearch _driveSearch;

    /// <summary>The walking side's own, which enters a stretch from either end.</summary>
    readonly RouteSearch _walkSearch;

    readonly LinkSurcharges _surcharges;

    /// <summary>Where the interface's own plans are kept, and the searches they are made over — <see cref="RouteBeyond"/>.</summary>
    readonly SelectionPaths _paths;

    /// <summary>
    /// Which crossing each stretch of the foot graph is, the ways each crossing is made of, and the band
    /// of each of those every lane running underneath covers — the whole join between the paint, the
    /// pavement and the carriageway.
    /// </summary>
    readonly CrossingBands _bands;

    float _elapsedS;

    readonly Vector2[] _impulseNs;

    /// <summary>How close each walker has come to where it is going, and how long since it last did better.</summary>
    readonly WalkProgress _progress;

    /// <summary>
    /// What every dynamic body carried into this tick, in roster order — walkers, then cars. Taken
    /// before the step, because the arithmetic in phase 5 needs the motion that <em>caused</em> a
    /// contact and the bodies by then hold the solver's answer to it.
    /// </summary>
    readonly Vector2[] _velocityIntoTickMps;

    /// <summary>One tick's working set for the fleet's wheels; it survives no tick.</summary>
    readonly WheelScratch _wheels;

    /// <summary>What each body that is not driving last laid, and the state it was laid from — <see cref="PlaceWhatIsNotDriving"/>.</summary>
    readonly LyingBook _lying;

    readonly CarBuilds _builds;

    readonly ulong _agentSeed;

    readonly FootGraph _foot;
    readonly WalkingNetwork _walking;
    readonly DrivingNetwork _driving;

    /// <summary>Who is inside what, and every bay in the town with who is standing in it.</summary>
    readonly Containers _containers;

    readonly ParkingRegistry _parking;

    /// <summary>The ways at every bay, laid with the town like the joins through a junction.</summary>
    readonly BayWays _bayWays;

    /// <summary>
    /// <b>The town's whole table of what is driven over what</b>: the road's own, grown by the ways the bays
    /// lay off it. Every decision reads this and never <see cref="RoadGraph.Crossings"/>, which knows only
    /// the junctions it was built from.
    /// </summary>
    readonly WayCrossings _crossings;

    /// <summary>And how much of a bay's ways the body standing in it may hold, off that table.</summary>
    readonly BayStandings _standings;

    /// <summary>
    /// What each of the town's buildings is for — its hospitals, its police stations and its depots
    /// (AMB-1, SRV-1) — and what every ambulance and every patrol is doing about it (AMB-5, SRV-5).
    /// </summary>
    readonly BuildingUses _uses;
    readonly RescueDuty _duty;
    readonly PatrolDuty _beat;
    readonly RecoveryDuty _recovery;

    readonly SelectionSet _selected;

    /// <summary>The orders given since the last tick took them, one per walker and never more than the selection can hold.</summary>
    readonly int[] _orderedPeople;
    readonly Vector2[] _orderedToM;

    HandInput _hands;

    /// <summary>
    /// The wheel each car is holding over for the whole run, or null on the towns — <b>every map but the
    /// skidpad</b> (<see cref="HoldTheWheels"/>). Laid once with the fleet, since what a held wheel is
    /// asked for never changes.
    /// </summary>
    HandInput[]? _wheelHeld;

    /// <summary>Whether every wheel on this map writes on the ground it stands on, whatever it is doing (<see cref="LayMark"/>).</summary>
    readonly bool _everyWheelWrites;

    int _ordered;

    /// <param name="agentSeed">
    /// The second of the two seeds: what the agents draw from, kept apart from the world seed so the
    /// same town can be watched with different traffic on it. It defaults to the plan's own, which is
    /// what a map opened from the command line runs at.
    /// </param>
    public TownWorld(CityPlan plan, SimConfig config, bool standStatics = true, ulong? agentSeed = null)
    {
        _config = config;
        _plan = plan;
        _agentSeed = agentSeed ?? plan.Seed;
        _everyWheelWrites = SkidpadPlan.HoldsItsCarsWheels(plan.Name);
        _terrain = new TerrainGrid(plan, config);
        _physics = new PhysicsWorld(config);

        // The selection and the orders it can give are laid with the town: what the interface holds is
        // bounded (CTL-1b), so a drag over a district writes into an array that already exists.
        _selected = new SelectionSet(config.View.SelectionMaxUnits);
        _orderedPeople = new int[_selected.Capacity];
        _orderedToM = new Vector2[_selected.Capacity];
        _roads = RoadGraph.Build(plan, config);
        _signals = SignalService.Build(plan, _roads, config);
        _heads = SignalHeads.Place(plan, _roads, _signals, config);

        // The bays' own ways come before the network that prices them: the one movement a route may make
        // that no junction admits is a turn at a car park (GEN-4l), and whether a frontage lays one is a
        // question about its bays' ways.
        _bayWays = BayWays.Build(plan, _roads, config);

        // Laid with the town rather than on demand: a structure the tick reads belongs to the town's
        // own standing cost.
        _driving = DrivingNetwork.Build(_roads, BayWays.WhereALegMayTurn(_roads, _bayWays), plan, config);
        _driveSearch = new RouteSearch(_driving.Graph, mostEntries: 1, mostGoals: 2, MostRunsInARoute);
        _surcharges = new LinkSurcharges(MostWaysGivenUpOn);

        // Both networks are read by the tick — cars over one, walkers over the other — so both are laid
        // with the town rather than the first time something asks.
        _foot = FootGraph.Build(plan, config);
        _walking = WalkingNetwork.Build(_foot, _terrain, config);
        _walkSearch = new RouteSearch(_walking.Graph, mostEntries: 2, mostGoals: 2, MostRunsInARoute);

        // The interface's own room to plan a whole path into (CTL-1a), laid with the selection it is
        // bounded by and never on the frame that wants it.
        _paths = new SelectionPaths(_selected.Capacity, _driving.Graph, _walking.Graph, MostRunsInARoute);
        _furniture = LaneFurniture.Project(plan, _roads);
        _bands = CrossingBands.Project(plan, _roads, _furniture, _walking);
        _standing = StaticsOnTheRoad();

        var walkers = 0;
        var drivers = 0;
        foreach (var kind in plan.Spawns.Kind)
        {
            if (kind == SpawnKindPerson) walkers++;
            else if (kind == SpawnKindCar) drivers++;
        }

        // The service vehicles are laid on top of the plan's own spawns: a car and its crew for every bay of
        // every hospital's and every station's apron, and one for every depot (AMB-2, SRV-2), which is why
        // the counts have to be answerable from the plan alone. A building that turns out to have no bay
        // near it leaves its slots unused, which is what makes the rosters the fleets' own counts rather
        // than these capacities. Which buildings they are is the map's (GEN-9).
        _uses = BuildingUses.Of(plan);

        var served = ((_uses.Hospitals.Count + _uses.PoliceStations.Count) * config.Service.ApronBays)
                     + _uses.Depots.Count;

        // <b>A driver and a hand apiece</b> (SRV-3): the one who keeps the wheel, and the one who gets out
        // and does the work in the street (AMB-10, EVA-5, SRV-6).
        walkers += served * CrewPerServiceVehicle;
        drivers += served;

        People = new PersonFleet(walkers);
        _impulseNs = new Vector2[walkers];
        _progress = new WalkProgress(walkers);
        _footfall = BookOfPavement(_walking, walkers * MostSlotsPerWalker);

        // The ways at the bays are laid with the road graph above, before the book: the book is sized to
        // every way in the town, and a bay's is a way like the rest of them.
        _crossings = BayCrossings.Over(_bayWays, _roads, config);
        _standings = BayStandings.Of(_bayWays, _crossings, config);

        // <b>A map laid to compare one thing stands the nominal car and everything else stands the fleet</b>
        // (CAR-11a): the measured lap differs in the drive layout and in nothing else, and the exam's cards
        // differ in the crossing and in nothing else. The fleet lap is the one proving ground that does not,
        // which is the whole of what it is for.
        _builds = TrackPlan.StandsTheNominalCar(plan.Name) || ExamPlan.StandsOneLook(plan.Name)
            ? CarBuilds.OfTheNominalCar(config, CarCatalog.Shared)
            : CarBuilds.OfTheFleet(config, CarCatalog.Shared);

        Cars = new CarFleet(drivers, PathAssembler.ArcsFor(_roads) + _bayWays.MostArcs, _builds);
        _occupancy = new LaneOccupancy(
            _roads,
            _bayWays.LengthsM,
            (drivers * MostSlotsPerCar(_roads, _crossings)) + (walkers * MostRoadSlotsPerWalker(_furniture))
            + _standing.Count);
        _lying = new LyingBook(drivers, MostLyingRowsPerCar(_roads, _bayWays));
        _wheels = new WheelScratch(drivers);
        _behindTheBar = new bool[drivers];
        Marks = new DriftMarks(config.Marks.Capacity);

        _velocityIntoTickMps = new Vector2[walkers + drivers];

        _containers = new Containers(_plan.Buildings.Capacity, drivers, People.Inside);
        _parking = ParkingRegistry.Build(plan, _bayWays, config, drivers);

        // The catalogue's two halves: what a driver has to hand, and the chain the planner fills in for
        // each leg. Both are laid with the town because the tick reads both.
        _desk = new ManeuverDesk(config, Cars, _terrain, _roads, _occupancy, _parking, _bayWays);
        _drivePlans = new DrivePlan(drivers);
        _carOrders = new PlayerOrders(drivers);
        _duty = new RescueDuty(drivers);
        _beat = new PatrolDuty(drivers);
        _recovery = new RecoveryDuty(drivers);
        _handOut = new int[drivers];
        Array.Fill(_handOut, NoHand);
        _recallS = new float[drivers];

        if (standStatics) StandStatics();

        // The aprons are claimed before the plan's cars are stood and filled after (GEN-4k): a bay a
        // spawned car is already standing in is not a bay a hospital can have, and a bay a hospital has is
        // not one the plan may spawn into.
        HoldTheAprons();
        Spawn();
        StandTheServiceVehicles();

        // <b>The roster is the agents there actually are</b>, and not the room that was laid for them: a
        // hospital with no bay near it stands no ambulance, and a slot nothing was put in is a body the
        // decision loop would hand a tick to and the solver has never heard of.
        Roster = new AgentRoster(People.Count, Cars.Count);
        HoldTheWheels();
        DriveTheEmptyMap();

        // One bucket the width of the widest question asked of it; the index is rebuilt into it every
        // tick and survives nothing.
        _nearby = new BucketGrid(plan.WorldSizeM, config.ProximityBucketM);
    }

    /// <summary>
    /// How many runs one search may return. A bound on the work rather than a figure behaviour reads:
    /// the longest route any shipped town needs is a fraction of it, and a route that would not fit is
    /// planned again from further along.
    /// </summary>
    const int MostRunsInARoute = 256;

    /// <summary>
    /// How many ways may be priced up at once. Nothing marks one yet, so the table stands empty; it is
    /// here because a search with no table would be a second code path.
    /// </summary>
    const int MostWaysGivenUpOn = 64;

    /// <summary>The spawn kinds the format carries.</summary>
    const byte SpawnKindPerson = 0;

    const byte SpawnKindCar = 1;

    public PersonFleet People { get; }

    public CarFleet Cars { get; }

    /// <summary>What the traffic has written on the ground. Scenery: the town lays marks and nothing in it reads one.</summary>
    internal DriftMarks Marks { get; }

    internal PhysicsWorld PhysicsForTrace => _physics;

    public RoadGraph Roads => _roads;

    /// <summary>
    /// The ways at every bay — the rest of the driving network, for whoever draws it or measures it.
    /// A layer that drew the lanes and the joins and left these out would say a car reaches a bay by
    /// teleporting off the end of a lane.
    /// </summary>
    public BayWays BayWays => _bayWays;

    /// <summary>Who is on each way of the road this tick, for whoever draws it or measures it.</summary>
    public LaneOccupancy Occupancy => _occupancy;

    /// <summary>
    /// The same for the pavement. <b>Neither book is one roster's</b>: a body on the carriageway is a
    /// stretch of the lane it stands in, and a car driving over a zebra a band of the walk it crosses.
    /// </summary>
    public LaneOccupancy Footfall => _footfall;

    /// <summary>The one town-wide lookup both agent kinds read, and the only thing that knows what colour anything is.</summary>
    public SignalService Signals => _signals;

    /// <summary>The heads, for whoever draws them. No agent reads one.</summary>
    public SignalHeads Heads => _heads;

    /// <summary>The town's own clock, which is what a phase is derived from.</summary>
    public float ElapsedS => _elapsedS;

    /// <summary>
    /// How many times a car has crossed a painted stop line its own approach was showing red at, counted
    /// as it happens rather than sampled.
    /// </summary>
    /// <remarks>
    /// <b>It has two sources and neither of them is zero</b>, so it is read against
    /// <see cref="RecklessDrivers"/> rather than against nothing. A share of the town does not keep the
    /// rule at all (CAR-13); the rest cross a bar the way anything crosses ground it did not mean to — a
    /// shunt, or a phase that turned while the car was already committed — and a lit shipped map reports a
    /// handful of those in a minute with nobody reckless on it. What a rise in this figure means therefore
    /// depends on which of the two moved, and the figure alone does not say.
    /// </remarks>
    public long RedBarCrossings { get; private set; }

    /// <summary>
    /// <b>How many of this town's people do not keep the driver's courtesies</b> (CAR-13) — the
    /// denominator <see cref="RedBarCrossings"/> is read against, and the reason a lit town no longer
    /// reports zero of them.
    /// </summary>
    public int RecklessDrivers { get; private set; }

    /// <summary>
    /// The last one of them, so a count above zero has somewhere to be looked at. Two crossings at one
    /// place and one instant are a pair of cars in contact — one shunting the other over the paint —
    /// and a single one at walking pace is a car that crept.
    /// </summary>
    public RedBarCrossing LastRedBarCrossing { get; private set; }

    /// <summary>
    /// How many times a car's route has run out on the lane its bay is entered from — the last thing the
    /// search has to say about a leg, after which the leg is a template.
    /// </summary>
    /// <remarks>
    /// Not how many drive legs finished — a car reaches its bay by coming to rest in it, which is
    /// <see cref="BaysParkedIn"/>. This is an instrument about the router.
    /// </remarks>
    public long RouteArrivals { get; private set; }

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

    /// <summary>How many times a walker has stood at a kerb and asked the road, which is PER-15 running.</summary>
    public long KerbWaitsBegun { get; private set; }

    /// <summary>
    /// And how many times one has begun crossing on a red. The lit-town soak wants zero, and it is
    /// counted where it happens rather than sampled.
    /// </summary>
    public long CrossingsBegunOnRed { get; private set; }

    /// <summary>
    /// <b>How many car-ticks a driver has been stopped short of an uncontrolled crossing for somebody
    /// standing at its kerb</b> — TER-5e's pedestrian right of way, counted where the ground is given up
    /// rather than inferred from a walker having got across.
    /// </summary>
    public long GaveWayAtAKerb { get; private set; }

    /// <summary>
    /// <b>And how many crossings already taken have been given back to a movement with the right of way over
    /// them</b> (TER-5e) — the revocation, counted where it happens. A town where it never happens is one
    /// where the ranks are never compared, whatever the code says.
    /// </summary>
    public long CrossingsGivenBack { get; private set; }


    /// <summary>
    /// And how many ended because the body stopped making progress for long enough to give up. The two
    /// are the whole of how a leg ends; reporting only the first would call a jammed town a busy one.
    /// </summary>
    public long WalksGivenUp { get; private set; }

    /// <summary>
    /// How many walker-ticks have been spent stepping round a body in the way (PER-24), and how many of
    /// those went the other way because the ground refused the right.
    /// </summary>
    /// <remarks>
    /// <b>The second is the one worth watching.</b> The right is the rule and the left is what the terrain
    /// leaves of it, so a town where most steps go left is a town where the rule is the exception — which is
    /// a fact about the figures and the pavements rather than about the code, and is invisible without a
    /// count.
    /// </remarks>
    public long StepsRound { get; private set; }

    public long StepsRoundToTheLeft { get; private set; }

    public TerrainGrid Terrain => _terrain;

    public CityPlan Plan => _plan;

    /// <summary>
    /// The pavement's own fine graph, and the two contracted networks over it and the roads — laid
    /// the first time something asks for them and kept.
    /// </summary>
    /// <remarks>
    /// All three are laid with the town because the tick reads all three, and a structure the tick needs
    /// belongs to the standing cost <c>--bench town</c> reports.
    /// </remarks>
    public FootGraph Foot => _foot;

    public WalkingNetwork Walking => _walking;

    /// <summary>
    /// Where the two networks lie over one another: the band of each crossing way each lane under it covers
    /// (<see cref="CrossingBands"/>). It is what either side reads to ask the other's book about ground it
    /// is about to be on, and neither side writes to it.
    /// </summary>
    public CrossingBands Bands => _bands;

    public DrivingNetwork Driving => _driving;


    public int StaticBodyCount => _physics.StaticBodyCount;

    /// <summary>
    /// How many stretches of lane the town's own furniture stands on. <b>The instrument that says whether
    /// the road's book knows about the immovable things at all</b> — a town reading zero here is a town
    /// where nothing was built in a carriageway, which is the answer a well-formed map file gives.
    /// </summary>
    public int StandingSlots => _standing.Count;

    public int IntegratedBodyCount => _physics.IntegratedBodyCount;


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
    /// <b>The entry a car is in declares for itself whether it may be scheduled</b> — the ones
    /// negotiating with something that is itself moving, and the ones that are a control loop wearing a
    /// decision's clothes. Nothing on the walking side asks for it yet.
    /// </summary>
    /// <remarks>
    /// It is declared by the catalogue rather than worked out here on purpose: the catalogue is what
    /// knows which of its entries those are, and a geometric test in the loop would be a second opinion
    /// about it that had to be kept in step with every entry added afterwards.
    /// </remarks>
    public bool DecidesEveryTick(int agent) =>
        Roster.IsCar(agent) && ManeuverCatalogue.ThinksEveryTick(Cars.Doing[Roster.CarIndex(agent)]);


    /// <summary>
    /// Whether the town keeps <see cref="Sub"/> — the drill-down inside phases 3 and 4 — which is off
    /// unless something is looking, on the same footing as the loop's own phase timing.
    /// </summary>
    public bool Timed { get; set; }

    /// <summary>Which kind of agent phase 3 spent its time on, and how much of phase 4 was the solver's step.</summary>
    public TickParts Sub;

    /// <summary>Whether the agent loop has reached the cars, so that the crossing is stamped once rather than per agent.</summary>
    bool _decidingCars;

    public void RebuildProximityIndex()
    {
        DriveTheEmptyMap();
        MendTheYards(_config.TickSeconds);
        _nearby.Rebuild(People.PositionM, People.RadiusM, People.Count);
        RebuildLaneOccupancy();
        RebuildFootOccupancy();

        // Phase 3 begins on the walkers, which is the end of the roster the loop walks first.
        if (!Timed) return;

        _decidingCars = false;
        Sub.Begin();
    }

    /// <summary>
    /// One timestamp a tick, not one an agent: the roster is walkers then cars, so the only instant
    /// worth marking is the one the loop crosses between them. Timing each of five hundred agents would
    /// cost several percent of the tick this is measuring.
    /// </summary>
    /// <remarks>
    /// Read off the agent that actually ran rather than off the roster's boundary, because a terminal
    /// agent is never handed here: a wrecked car at the head of the fleet would otherwise leave the
    /// crossing unmarked and put every car's time on the walkers.
    /// </remarks>
    void AccountFor(int agent)
    {
        if (!Timed || _decidingCars || !Roster.IsCar(agent)) return;

        Sub.Mark(ref Sub.WalkerTicks);
        _decidingCars = true;
    }

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
    /// Whether this agent is one of the units under the player's hand. <b>A terminal unit is not</b>: a
    /// selection may hold a wreck and a working car at once, so the wheel is refused per unit rather
    /// than per selection.
    /// </summary>
    bool Handed(int agent) =>
        _hands.Held && _selected.Any && !IsTerminal(agent) &&
        (Roster.IsCar(agent)
            ? _selected.Holds(SelectionKind.Car, Roster.CarIndex(agent))
            : _selected.Holds(SelectionKind.Person, agent));

    public void TickAgent(int agent)
    {
        AccountFor(agent);

        if (HoldAgents && !Handed(agent))
        {
            Paused(agent);
            return;
        }

        if (Roster.IsCar(agent))
        {
            TickCar(Roster.CarIndex(agent));
            return;
        }

        // PHY-7: inside a container there is no body in the world to move, and the only actions are the
        // container's own — which are the trip's and are taken on the decision clock.
        if (People.Inside[agent].Any)
        {
            _impulseNs[agent] = Vector2.Zero;
            return;
        }

        // PER-18: a casualty declares nothing. What still acts on it is the ground it is lying on, and
        // that is <see cref="Settle"/>'s — asked of the body rather than of the walker it was.
        if (People.Wounded[agent]) return;

        var positionM = People.PositionM[agent];
        var ground = _terrain.At(positionM);
        People.GroundCoefficient[agent] = ground.Coefficient;

        // CTL-6's seam, read every tick: the goal is substituted and nothing under it is, so what
        // follows cannot tell this walker from any other.
        if (_hands.Held && _selected.Holds(SelectionKind.Person, agent)) HandWalk(agent);

        // Arriving is the manoeuvre's own end condition, so it is asked every tick and not on the
        // decision clock: at a walker's pace a tenth of a second is two thirds of a metre, and a
        // walker that is only allowed to notice its destination six times a second walks past it. The
        // same applies to reaching a point of its line and taking the next one, which is the same
        // question asked of a shorter leg.
        var atTheKerb = AtTheKerb(agent, positionM);
        while (People.Walking[agent] && !atTheKerb &&
               (People.DestinationM[agent] - positionM).Length() <= People.RadiusM[agent])
        {
            var steppingOnto = _terrain.At(positionM).Drivable ? -1 : People.CrossingAhead(agent);
            if (People.TakeNextWalkedPoint(agent, out var nextM))
            {
                // Counted where it happens: the tick a body leaves a kerb for the paint. A sample taken
                // afterwards finds a walker on a crossing and cannot say what it was shown when it set
                // off.
                if (steppingOnto >= 0 && _signals.CrossingIsLit(steppingOnto) &&
                    _signals.ForCrossing(steppingOnto, _elapsedS) != SignalColour.Green)
                {
                    CrossingsBegunOnRed++;
                }

                // Reaching a point of the line *is* progress, and the clock that decides a walker has
                // given up is measured against the point it is walking at. Left standing, it would run
                // up on the leg after a long one and call a walker that had just arrived stuck.
                People.DestinationM[agent] = nextM;
                _progress.Restart(agent);
                atTheKerb = AtTheKerb(agent, positionM);
                continue;
            }

            People.Walking[agent] = false;
        }

        // What the pavement's book granted this walker, read as the permission it is (PER-13): there is
        // ground in front of it to walk into, or it stands where it is until whoever has that ground moves.
        // A body going nowhere is the other answer and is no part of that grant (PER-24): the aim goes
        // round it, which is a walker's whole reply to something in the way — and where there is nowhere to
        // step to, standing is, exactly as it was before there was a step.
        var walledIn = false;
        var aimM = atTheKerb
            ? WaitAimM(agent)
            : StepRoundAim(agent, positionM, People.DestinationM[agent], out walledIn);

        var moving = People.Walking[agent] && !People.IsHeldByTheBook(agent, StopsInM(agent)) && !walledIn &&
                     (!atTheKerb || (aimM - positionM).Length() > People.RadiusM[agent]);

        var step = WalkerFollower.Step(
            _config, People.HeadingRad[agent], positionM, People.VelocityMps[agent], aimM, moving, ground.Coefficient,
            People.IsOnItsFeet(agent), People.MassKg[agent], _config.TickSeconds);

        People.HeadingRad[agent] = step.HeadingRad;
        _impulseNs[agent] = step.ImpulseNs;
    }


    public void DecideAgent(int agent, float sinceLastDecisionS)
    {
        AccountFor(agent);

        // Held: no decision, and — the load-bearing half — no clock running up either, so nothing is
        // stuck by having stood still while the town was paused.
        if (HoldAgents && !Handed(agent)) return;

        // Which way to go at the junction ahead is decided when the line is re-laid, not on the clock:
        // the interval is a floor on staleness, never a ceiling on thinking. What is taken here is the
        // catalogue's decision — whether this car is stuck rather than waiting, and which rung answers.
        if (Roster.IsCar(agent))
        {
            // The errand before the driving (AMB-5): what comes out of it is a destination and a chain,
            // and the catalogue's own tick below is what drives them.
            var car = Roster.CarIndex(agent);

            // EVA-5: what is on a bar takes no decisions, the way a casualty on a stretcher takes none.
            // Its errand is still its own and it picks it up again where the arm puts it down.
            if (_recovery.OnTheHookOf[car] >= 0) return;

            // CTL-2: the player's order pins the goal the behaviour would otherwise have picked, and for a
            // vehicle with an errand the errand is that behaviour — so an ordered one runs the order in
            // place of it, and picks its errand back up where the reset leaves it (CTL-4).
            if (IsUnderOrders(car)) RunTheOrder(car);
            else if (Cars.Ambulance[car]) RunTheRescue(car, sinceLastDecisionS);
            else if (IsAnEvacuator(car)) RunTheRecovery(car, sinceLastDecisionS);
            else if (IsAPatrolCar(car)) RunThePatrol(car, sinceLastDecisionS);

            DecideDriver(car, sinceLastDecisionS);
            return;
        }

        // PER-18: a casualty takes no actions. It is not terminal — an ambulance is on its way — so the
        // body is still stepped, struck and pushed like any other; what it has stopped doing is deciding.
        if (People.Wounded[agent]) return;

        // Inside a building or a car there is no line to hold and no ground to be on: what runs is the
        // trip, and the whole action set is the container's.
        if (People.Inside[agent].Any)
        {
            DecideContained(agent, sinceLastDecisionS);
            return;
        }

        // Held at a kerb is not stuck, and the clock that gives a leg up is frozen while it is: a walker
        // waiting out a long red would otherwise be handed somewhere else to be, halfway through a
        // crossing it had not begun.
        if (People.HeldAtTheKerb[agent]) return;

        // Nor is queueing, which is now the whole of what the book holds a walker for: the walker in front
        // is under way and the ground it holds is ground it is about to give back. A body going nowhere
        // stopped being one of these when it stopped cutting the grant (PER-24) — nothing waits behind one
        // any more, so nothing has to be given a leg up out from behind one either.
        if (People.IsHeldByTheBook(agent, StopsInM(agent))) return;

        // Nor is a beat stood on purpose. It is the walking side's own idle — between two goals, and in the
        // road while a body paces one — and a clock that gave a leg up while it ran would end the stand
        // rather than the stand ending itself.
        if (!People.Walking[agent] && People.Stage[agent] == TripStage.StandingBy)
        {
            StandingStill(agent, sinceLastDecisionS);
            return;
        }

        _progress.Note(agent, (People.DestinationM[agent] - People.PositionM[agent]).Length(), sinceLastDecisionS);

        // Standing here means the follower has already answered: either it arrived, or it never had
        // anywhere to go. Being stuck is the other way a leg ends, and it is the one that needs a
        // clock — a walker held up by something is not a walker that has finished.
        var stuck = _progress.IsStuck(agent, _config.Ladder.ObstructionWaitS);
        if (People.Walking[agent] && !stuck) return;

        if (stuck)
        {
            // A crew out working has no trip to give up (SRV-3). What it is walking at is its vehicle's
            // errand, and that errand's own bound is what ends this — the recall clock, or the call's.
            // Handed a trip here, a paramedic ten metres from a casualty would walk off to a shop.
            if (People.Stage[agent] == TripStage.Attending) return;

            // Held up long enough to give up on where it was going. Not an arrival and not counted as
            // one: conflating the two would report a jammed town as a busy one.
            WalksGivenUp++;

            // A failed order runs the normal recovery and ends in idle-awaiting-orders rather than in a
            // new goal of the walker's own.
            if (People.Manual[agent])
            {
                People.Walking[agent] = false;
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









    /// <summary>
    /// Nothing to release: the solver is this project's own arrays and the collector owns them. The town
    /// is still handed out as a disposable because that is what every caller holds it in.
    /// </summary>
    public void Dispose()
    {
    }

}
