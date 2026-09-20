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
        WalkedLineM = new Vector2[capacity * WalkedPointsPerPerson];
        WalkedCrossing = new int[capacity * WalkedPointsPerPerson];
        WalkedWay = new int[capacity * WalkedPointsPerPerson];
        WalkedAlongM = new float[capacity * WalkedPointsPerPerson];
        WalkedCount = new int[capacity];
        WalkedTaken = new int[capacity];
        WalkedRunsOut = new bool[capacity];
        OnWay = new int[capacity];
        Array.Fill(OnWay, NoWay);
        OnWayM = new float[capacity];
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

    /// <summary>
    /// How much of a walked line a body carries at once. A bound on the work rather than a figure
    /// behaviour reads: a longer walk is laid again from where the body has got to.
    /// </summary>
    public const int WalkedPointsPerPerson = 64;

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

    /// <summary>The points still to be walked, in order, on the lane each stretch's own side asks for.</summary>
    public Vector2[] WalkedLineM { get; }

    /// <summary>Which crossing each of those points stands on, or −1 where it is pavement.</summary>
    public int[] WalkedCrossing { get; }

    /// <summary>
    /// And which way of the pavement each of them stands on, as <see cref="World.Foot.WalkedLine"/> writes
    /// it: the stretch's own directed edge, or the complement of a mitre's turn slot on a corner.
    /// </summary>
    public int[] WalkedWay { get; }

    /// <summary>How far along that way's own line the point stands.</summary>
    public float[] WalkedAlongM { get; }

    /// <summary>
    /// The way this body stands on now, or <see cref="NoWay"/> — read off the point it is walking at
    /// rather than searched for, since the line already knows where it goes.
    /// </summary>
    /// <remarks>
    /// <b>It is also the question the walk itself turns on</b> (PER-25): a body on a way of the network
    /// walks the line the network laid it, and a body on none of it walks straight at the network.
    /// </remarks>
    public int[] OnWay { get; }

    /// <summary>And how far along that way it stands, which is where its own ground begins.</summary>
    public float[] OnWayM { get; }

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

    public int[] WalkedCount { get; }

    /// <summary>How many of them are behind the body.</summary>
    public int[] WalkedTaken { get; }

    /// <summary>
    /// Whether the line stops short of where the walker is going: <see cref="WalkedPointsPerPerson"/>
    /// points were not enough for the route the search found, so the rest of it will be laid again from
    /// where the body has got to. <b>A line that reaches its goal answers no.</b>
    /// </summary>
    /// <remarks>
    /// The car's <c>RouteRunsOut</c> for walkers, and asked for by the same reader: the interface draws
    /// past the end of a line only where there is a walk past it (CTL-1a).
    /// </remarks>
    public bool[] WalkedRunsOut { get; }

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
        WalkedCount[person] = 0;
        WalkedTaken[person] = 0;
        WalkedRunsOut[person] = false;
        OnWay[person] = NoWay;
        OnWayM[person] = 0f;
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

    public Span<Vector2> WalkedLineOf(int person) =>
        WalkedLineM.AsSpan(person * WalkedPointsPerPerson, WalkedPointsPerPerson);

    public Span<int> WalkedCrossingOf(int person) =>
        WalkedCrossing.AsSpan(person * WalkedPointsPerPerson, WalkedPointsPerPerson);

    public Span<int> WalkedWayOf(int person) =>
        WalkedWay.AsSpan(person * WalkedPointsPerPerson, WalkedPointsPerPerson);

    public Span<float> WalkedAlongOf(int person) =>
        WalkedAlongM.AsSpan(person * WalkedPointsPerPerson, WalkedPointsPerPerson);

    /// <summary>
    /// Which point of the line the body is walking at, or −1 where it is walking at none of them. <b>The
    /// point already taken</b>: what the body is aiming at is the last one handed out and not the next one.
    /// </summary>
    public int WalkedAt(int person) => WalkedTaken[person] - 1;

    /// <summary>Which crossing the next point of the line stands on, or −1.</summary>
    public int CrossingAhead(int person) =>
        WalkedTaken[person] < WalkedCount[person]
            ? WalkedCrossing[(person * WalkedPointsPerPerson) + WalkedTaken[person]]
            : -1;

    /// <summary>The next point of the line, or false where there is none left.</summary>
    public bool TakeNextWalkedPoint(int person, out Vector2 pointM)
    {
        if (WalkedTaken[person] >= WalkedCount[person])
        {
            pointM = PositionM[person];
            return false;
        }

        pointM = WalkedLineM[(person * WalkedPointsPerPerson) + WalkedTaken[person]++];
        return true;
    }

    public void ClearWalkedLine(int person)
    {
        WalkedCount[person] = 0;
        WalkedTaken[person] = 0;
        WalkedRunsOut[person] = false;
    }
}
