using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Physics;

namespace TrafficSimulation.Agents.Person.Body;

/// <summary>
/// Every walker in the town, as one array per field. Laid once at load at the capacity the town needs
/// and never grown, so a tick over five hundred walkers touches no allocator.
/// </summary>
/// <remarks>
/// <para>
/// There is no <c>Person</c> object and there is deliberately nowhere to put one: a walker is an index
/// into these arrays, which is what a proximity index, a debug layer and an instanced draw all want it
/// to be anyway. The index is also the agent's identity for the decision clock's stagger, so it may
/// not be reordered while the town is running.
/// </para>
/// <para>
/// <b>Position and velocity are mirrored out of the solver once a tick</b>, after the step, and read
/// from here for the rest of the tick. Asking the body twice in one tick is how two parts of a
/// decision end up describing two different instants.
/// </para>
/// <para>
/// <b>Nothing here is a grant</b> (PER-26). A walker holds the ground it stands on and states the ground
/// it is walking at, and both of those live on the ways rather than in this roster — so there is no
/// distance in front of a body for two readers to disagree about, and no field that has to be cleared
/// when a walk ends.
/// </para>
/// </remarks>
internal sealed class PersonFleet
{
    public PersonFleet(int capacity)
    {
        Body = new BodyId[capacity];
        PositionM = new Vector2[capacity];
        VelocityMps = new Vector2[capacity];
        HeadingRad = new float[capacity];
        DestinationM = new Vector2[capacity];
        Walking = new bool[capacity];
        Manual = new bool[capacity];
        Wounded = new bool[capacity];
        Reckless = new bool[capacity];
        MassKg = new float[capacity];
        RadiusM = new float[capacity];
        Variant = new byte[capacity];
        Draw = new Rng[capacity];
        DistanceWalkedM = new float[capacity];
        GroundCoefficient = new float[capacity];
        RouteWays = new int[capacity * RouteWaysPerPerson];
        RouteCount = new int[capacity];
        RouteTaken = new int[capacity];
        RouteRunsOut = new bool[capacity];
        RouteToM = new float[capacity];
        OnWay = new int[capacity];
        Array.Fill(OnWay, NoWay);
        OnWayM = new float[capacity];
        OffWayM = new float[capacity];
        OnCrossing = new int[capacity];
        Array.Fill(OnCrossing, NoCrossing);
        OnCrossingWay = new int[capacity];
        Array.Fill(OnCrossingWay, NoWay);
        GrantM = new float[capacity];
        Array.Fill(GrantM, float.PositiveInfinity);
        GoalM = new Vector2[capacity];
        Stage = new TripStage[capacity];
        DestinationBuilding = new int[capacity];
        Array.Fill(DestinationBuilding, NoBuilding);
        TimerS = new float[capacity];
        Inside = new Contained[capacity];
    }

    /// <summary>What a person holds when this trip has no building of its own to be at.</summary>
    public const int NoBuilding = -1;

    /// <summary>What a person holds when the pavement has no way to put it on.</summary>
    public const int NoWay = -1;

    /// <summary>And when the way it is on is pavement rather than paint.</summary>
    public const int NoCrossing = -1;

    /// <summary>
    /// How much of a route a body carries at once, in ways. A bound on the work rather than a figure
    /// behaviour reads: a longer route is laid again from where the body has got to.
    /// </summary>
    /// <remarks>
    /// <b>The same bound a car carries</b> (<see cref="Agents.Car.Body.CarFleet.RouteLanesPerCar"/>), for
    /// the same reason and at the same tier — which is the whole point of both holding a route rather than
    /// one of them holding a route and the other a line.
    /// </remarks>
    public const int RouteWaysPerPerson = 64;

    public int Count { get; private set; }

    public int Capacity => Body.Length;

    public BodyId[] Body { get; }

    public Vector2[] PositionM { get; }

    public Vector2[] VelocityMps { get; }

    /// <summary>Intent, not solver output: rotation is locked, so this is set by code and read by what draws.</summary>
    public float[] HeadingRad { get; }

    /// <summary>
    /// Where the follower is aiming <em>this</em> stretch — the next point of the walked line, and not the
    /// end of the walk. <b>It is also what the body states on the ways</b> (PER-26): the ground a walker
    /// says it is walking at is the ground between itself and this.
    /// </summary>
    public Vector2[] DestinationM { get; }

    /// <summary>Where the walk ends. The line is what gets there; this is what it is a line to.</summary>
    public Vector2[] GoalM { get; }

    /// <summary>
    /// <b>The ways still to be walked, in order</b> — the route, as the walking network numbers its ways: a
    /// stretch's own directed lane, or the complement of the corner leading onto one
    /// (<see cref="World.Foot.WalkingNetwork.IsACorner"/>).
    /// </summary>
    /// <remarks>
    /// <b>It is the car's <see cref="Agents.Car.Body.CarFleet.RouteLanes"/> in the pavement's own ways.</b>
    /// Both agent kinds search one graph, are handed the same run-links, and keep the chain those expand
    /// into (<see cref="World.Routing.RouteChain"/>) rather than a line laid over the whole of it — a body
    /// walks the chain one way at a time and needs the shape of only the way it is on, which the network
    /// already holds and never hands out a copy of.
    /// </remarks>
    public int[] RouteWays { get; }

    /// <summary>How far along the <em>last</em> way of the chain the walk stops, the destination standing part-way along it.</summary>
    public float[] RouteToM { get; }

    /// <summary>
    /// <b>The way this body is walking now</b>, or <see cref="NoWay"/> where it is on none of the network —
    /// the way of the chain it has been handed, and the car's <c>LaneOf</c> for a walker.
    /// </summary>
    /// <remarks>
    /// <b>It is also the question the walk itself turns on</b> (PER-25): a body on a way of the network
    /// walks that way's own line, and a body on none of it walks straight at the network.
    /// </remarks>
    public int[] OnWay { get; }

    /// <summary>
    /// And how far along that way's own line it stands, which is where its own ground begins and where it
    /// aims from. <b>The car's <c>ProgressM</c> for a walker</b>, and written rather than recovered.
    /// </summary>
    public float[] OnWayM { get; }

    /// <summary>
    /// <b>And how far off that line it stands</b>, which is the walking side's own off-line: past the
    /// ground the way has either side of itself, the body has lost it and the route is laid again (PER-25).
    /// </summary>
    /// <remarks>
    /// <b>Worked out once a tick, where the body's place on its way is</b> (SIM-7): the projection that
    /// answers *where along* answers *how far off* in the same breath, and asking it twice was two
    /// answers about one body a tick apart.
    /// </remarks>
    public float[] OffWayM { get; }

    /// <summary>
    /// <b>The crossing this walk is on or is arriving at</b>, or −1 where it is neither: the crossing the
    /// way being walked is part of, and otherwise the one the next way of the chain is, from a stop short
    /// of it. <b>Written where the way is</b>, so that what a walker is doing is one reading and not a walk
    /// of the route by everybody who wants to know.
    /// </summary>
    /// <remarks>
    /// <b>It is what a walker wants of a zebra and not where its feet are</b> (PER-27): a crossing of this
    /// town runs kerb to kerb, so a body that is on one is already in the road and anything it asked for
    /// there it would have to ask standing on the carriageway.
    /// </remarks>
    public int[] OnCrossing { get; }

    /// <summary>
    /// <b>The one stretch of paint that crossing is walked on</b>, as the walking network numbers its lanes,
    /// or <see cref="NoWay"/> where there is no crossing — the way of <see cref="OnCrossing"/> this body is
    /// on, and where it is still at the kerb the way it is about to step onto.
    /// </summary>
    /// <remarks>
    /// <b>A zebra is walked one way at a time</b> (PER-27, WLK-15): its two lanes are the two directions
    /// over the same carriageway, and what a walker reserves is the one it is taking. Written where
    /// <see cref="OnCrossing"/> is, off the same reading of the route (SIM-7), so nothing walks the chain
    /// again to recover which direction a crossing was being taken in.
    /// </remarks>
    public int[] OnCrossingWay { get; }

    /// <summary>
    /// <b>How far down its walk this walker was granted room to stop</b> (PER-26): its plan as it survived
    /// every body and every other plan, from the front of its body, less the gap it keeps — infinite where
    /// nothing cut it. <b>A walker's counterpart of a driver's grant</b>, and what it walks to.
    /// </summary>
    /// <remarks>
    /// <b>A walker refused a crossing stands at the kerb</b> (PER-27): its plan runs to the far kerb or none,
    /// so a crossing the traffic has leaves it nothing past the near one.
    /// </remarks>
    public float[] GrantM { get; }

    /// <summary>
    /// PER-9's own state: what this person is doing about the trip they are on. <b>Observable</b> — it
    /// is what the interface reads out beside a selected walker and what the trip probe counts.
    /// </summary>
    public TripStage[] Stage { get; }

    /// <summary>The building this trip is for (PER-9), or <see cref="NoBuilding"/>. Its claim is held while the walk lasts.</summary>
    public int[] DestinationBuilding { get; }

    /// <summary>What is left of a bounded interval — the dwell inside a building (PER-11), or the idle between goals.</summary>
    public float[] TimerS { get; }

    /// <summary>
    /// What this person is inside, or nothing (PHY-7). <b>Not drawn, not stepped, not picked and not in
    /// anybody's way</b> while it is anything — and the pose left behind is the container's, never the
    /// body's.
    /// </summary>
    public Contained[] Inside { get; }

    /// <summary>How many ways of the chain are laid.</summary>
    public int[] RouteCount { get; }

    /// <summary>And how many have been taken off it — the one being walked is the last one handed out.</summary>
    public int[] RouteTaken { get; }

    /// <summary>
    /// Whether the chain stops short of where the walker is going: <see cref="RouteWaysPerPerson"/> ways
    /// were not enough for the route the search found, so the rest of it is laid again from where the body
    /// has got to. <b>A chain that reaches its goal answers no.</b>
    /// </summary>
    /// <remarks>
    /// The car's <see cref="Agents.Car.Body.CarFleet.RouteRunsOut"/>, at the same tier and asked by the
    /// same reader: the interface draws past the end of a route only where there is a walk past it
    /// (CTL-1a).
    /// </remarks>
    public bool[] RouteRunsOut { get; }

    public bool[] Walking { get; }

    /// <summary>CTL-4: an ordered walker idles awaiting the next order instead of drawing a new destination.</summary>
    public bool[] Manual { get; }

    /// <summary>
    /// <b>PER-18: knocked down and lying where it fell</b> — taking no actions of its own, and waiting for
    /// an ambulance. <b>Nobody in this town dies</b> (PHY-3), so this is the whole of what a contact can
    /// make of a person and it is not a terminal state (AGT-5): a casualty is collected, treated and put
    /// back on the pavement, which is the whole of what the rescue is for.
    /// </summary>
    /// <remarks>
    /// <b>It is one fact and not two.</b> Going down and losing the ground under your feet are the same
    /// moment and last the same time, so the impulse of the impact carries the body down the road and the
    /// body stays there — which is what an impact is supposed to look like.
    /// </remarks>
    public bool[] Wounded { get; }

    /// <summary>
    /// <b>CAR-13: this one does not keep the courtesies</b> — drawn once when the person is made and true
    /// for the rest of the run. What it changes is what they do about a red and about somebody waiting at
    /// a kerb, and it changes nothing at all until they take a wheel.
    /// </summary>
    /// <remarks>
    /// <b>It is a fact about the person and not about the car</b>, because it is the driver who does or
    /// does not stop: the same hatchback is driven past a red by one owner and held at it by the next, and
    /// a flag on the car would make it the paintwork's habit. The road reads it through whoever has the
    /// wheel (<c>TownWorld.Crossings.cs</c>), so there is one copy of it and nothing to keep in step.
    /// </remarks>
    /// <seealso cref="DrawsReckless"/>
    public bool[] Reckless { get; }

    /// <summary>
    /// <b>CAR-13's draw, on a stream that belongs to nothing else.</b> Off the person's own stream instead
    /// it would spend a value, and every draw that person made afterwards — every destination, every dwell
    /// — would come out different: adding a habit would have moved every walk in every town, and the
    /// figures that moved with it would have been read as this habit's doing.
    /// </summary>
    public static bool DrawsReckless(ulong seed, ulong person, float share) =>
        new Rng(seed, RecklessStream + person).NextFloat() < share;

    /// <summary>The stream CAR-13 is drawn on, which belongs to nothing else.</summary>
    const ulong RecklessStream = 0x5245434B;

    public float[] MassKg { get; }

    public float[] RadiusM { get; }

    public byte[] Variant { get; }

    /// <summary>The agent's own stream, so a walker's destinations are its own and are reproducible.</summary>
    public Rng[] Draw;

    /// <summary>Metres covered on foot. The walk cycle is stepped by <em>distance</em>, not by time, so ground that slows a walker slows its stride.</summary>
    public float[] DistanceWalkedM { get; }

    /// <summary>The ground's own factor under this walker, sampled once a tick and read by everything that needs it.</summary>
    public float[] GroundCoefficient { get; }

    /// <summary>One more person on the roster, with everything about them at the value a new one holds.</summary>
    /// <param name="reckless">
    /// Whether this one keeps the driver's courtesies (CAR-13). <b>Drawn by the caller and on a stream of
    /// its own</b>, never off <paramref name="draw"/>: a draw spent here would shift every later draw of
    /// this person's, so adding the habit would silently move every walk in every town.
    /// </param>
    public int Add(
        BodyId body, Vector2 positionM, float headingRad, float massKg, float radiusM, byte variant, Rng draw,
        bool reckless)
    {
        if (Count == Capacity) throw new InvalidOperationException($"The roster was laid for {Capacity} walkers and is full.");

        var person = Count++;
        Body[person] = body;
        PositionM[person] = positionM;
        VelocityMps[person] = Vector2.Zero;
        HeadingRad[person] = headingRad;
        DestinationM[person] = positionM;
        Walking[person] = false;
        Manual[person] = false;
        Wounded[person] = false;
        MassKg[person] = massKg;
        RadiusM[person] = radiusM;
        Variant[person] = variant;
        Draw[person] = draw;
        Reckless[person] = reckless;
        DistanceWalkedM[person] = 0f;
        GroundCoefficient[person] = 1f;
        GoalM[person] = positionM;
        RouteCount[person] = 0;
        RouteTaken[person] = 0;
        RouteRunsOut[person] = false;
        RouteToM[person] = 0f;
        OnWay[person] = NoWay;
        OnWayM[person] = 0f;
        OffWayM[person] = 0f;
        OnCrossing[person] = NoCrossing;
        OnCrossingWay[person] = NoWay;
        GrantM[person] = float.PositiveInfinity;
        Stage[person] = TripStage.StandingBy;
        DestinationBuilding[person] = NoBuilding;
        TimerS[person] = 0f;
        Inside[person] = Contained.Nowhere;
        return person;
    }

    /// <summary>Which of the two grips acts on this body: a sole pressed into the ground, or a body along it.</summary>
    public bool IsOnItsFeet(int person) => !Wounded[person];

    /// <summary>
    /// Whether this person takes actions at all. <b>A casualty is out of the roster's decisions</b> for as
    /// long as it takes an ambulance to reach them, and back in it once they have been treated — which is
    /// not the same as being terminal (PER-18), because nothing about it is permanent.
    /// </summary>
    public bool Acts(int person) => !Wounded[person];

    /// <summary>This person's own stretch of the chain, which is where a route is written.</summary>
    public Span<int> RouteOf(int person) =>
        RouteWays.AsSpan(person * RouteWaysPerPerson, RouteWaysPerPerson);

    /// <summary>
    /// <b>The way of the chain this body is walking</b>, or <see cref="NoWay"/> where it holds no route.
    /// It is the cursor; <see cref="OnWay"/> is that same way once the town has agreed the body is
    /// actually standing on it.
    /// </summary>
    public int CurrentRouteWay(int person) =>
        RouteTaken[person] < 1 ? NoWay : RouteWays[(person * RouteWaysPerPerson) + RouteTaken[person] - 1];

    /// <summary>
    /// Which slot of the chain the body is walking, or −1 where it is walking none of it. <b>The way
    /// already taken</b>: what the body is on is the last one handed out and not the next one.
    /// </summary>
    public int RouteAt(int person) => RouteTaken[person] - 1;

    /// <summary>The way after the one being walked, or <see cref="NoWay"/> where the chain ends here.</summary>
    public int PeekNextRouteWay(int person) =>
        RouteTaken[person] >= RouteCount[person] ? NoWay : RouteWays[(person * RouteWaysPerPerson) + RouteTaken[person]];

    /// <summary>The way before it, or <see cref="NoWay"/> where the body is on the first of the chain.</summary>
    public int RouteWayBefore(int person) =>
        RouteTaken[person] < 2 ? NoWay : RouteWays[(person * RouteWaysPerPerson) + RouteTaken[person] - 2];

    /// <summary>The next way of the chain, or false where there is none left.</summary>
    public bool TakeNextRouteWay(int person, out int way)
    {
        if (RouteTaken[person] >= RouteCount[person])
        {
            way = NoWay;
            return false;
        }

        way = RouteWays[(person * RouteWaysPerPerson) + RouteTaken[person]++];
        return true;
    }

    /// <summary>Whether the way being walked is the last of the chain, which is the one the destination stands on.</summary>
    public bool OnTheLastWay(int person) => RouteTaken[person] >= RouteCount[person];

    public void ClearRoute(int person)
    {
        RouteCount[person] = 0;
        RouteTaken[person] = 0;
        RouteRunsOut[person] = false;
        RouteToM[person] = 0f;
        OnWay[person] = NoWay;
        OnWayM[person] = 0f;
        OffWayM[person] = 0f;
        OnCrossing[person] = NoCrossing;
        OnCrossingWay[person] = NoWay;
        GrantM[person] = float.PositiveInfinity;
    }
}
