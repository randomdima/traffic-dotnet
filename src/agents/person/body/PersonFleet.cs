using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Physics;
using TrafficSimulation.World.Road;

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
        StoodAtM = new Vector2[capacity];
        Walking = new bool[capacity];
        Manual = new bool[capacity];
        Dead = new bool[capacity];
        OffFeetForS = new float[capacity];
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
        OnWay = new int[capacity];
        Array.Fill(OnWay, NoWay);
        OnWayM = new float[capacity];
        ReserveAheadM = new float[capacity];
        AuthorityM = new float[capacity];
        Array.Fill(AuthorityM, float.PositiveInfinity);
        HeldBy = new LaneUse[capacity];
        GoalM = new Vector2[capacity];
        WaitingToCrossS = new float[capacity];
        WaitingForLane = new int[capacity];
        Array.Fill(WaitingForLane, NoLane);
        RefusedWay = new int[capacity];
        Array.Fill(RefusedWay, NoWay);
        RefusedAtM = new float[capacity];
        KerbM = new Vector2[capacity];
        HeldAtTheKerb = new bool[capacity];
        Stage = new TripStage[capacity];
        DestinationBuilding = new int[capacity];
        Array.Fill(DestinationBuilding, NoBuilding);
        TripCar = new int[capacity];
        Array.Fill(TripCar, NoCar);
        TimerS = new float[capacity];
        Inside = new Contained[capacity];
    }

    /// <summary>What a person holds when this trip has no building of its own to be at, and no car in it.</summary>
    public const int NoBuilding = -1;

    public const int NoCar = -1;

    /// <summary>What a person holds when the pavement's book has nowhere to put it.</summary>
    public const int NoWay = -1;

    /// <summary>And when it is waiting for no lane of a crossing, which is nearly always.</summary>
    public const int NoLane = -1;

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

    /// <summary>Where the follower is aiming <em>this</em> stretch — the next point of the walked line, and not the end of the walk.</summary>
    public Vector2[] DestinationM { get; }

    /// <summary>Where the walk ends. The line is what gets there; this is what it is a line to.</summary>
    public Vector2[] GoalM { get; }

    /// <summary>
    /// Where this walker was put down. <b>A walker with nowhere to be comes back to it</b>, which is what
    /// makes pacing a road a pair of places rather than a wander that happens to return.
    /// </summary>
    public Vector2[] StoodAtM { get; }

    /// <summary>The points still to be walked, in order, on the lane each stretch's own side asks for.</summary>
    public Vector2[] WalkedLineM { get; }

    /// <summary>Which crossing each of those points stands on, or −1 where it is pavement.</summary>
    public int[] WalkedCrossing { get; }

    /// <summary>
    /// And which way of the pavement each of them stands on, as <see cref="WalkedLine"/> writes it: the
    /// stretch's own directed edge, or the complement of a mitre's turn slot on a corner.
    /// </summary>
    public int[] WalkedWay { get; }

    /// <summary>How far along that way's own line the point stands.</summary>
    public float[] WalkedAlongM { get; }

    /// <summary>
    /// The way this body stands on now, or <see cref="NoWay"/> — read off the point it is walking at
    /// rather than searched for, since the line already knows where it goes.
    /// </summary>
    public int[] OnWay { get; }

    /// <summary>And how far along that way it stands, which is where its own stretch of the book begins.</summary>
    public float[] OnWayM { get; }

    /// <summary>How much pavement past its own front this body asked the book for, before anything was cut off it.</summary>
    public float[] ReserveAheadM { get; }

    /// <summary>
    /// <b>What it was granted</b>: clear ground from its own front to where it may come to rest, and
    /// nothing where the book cut it at somebody else's. Infinite where nothing binds it at all.
    /// </summary>
    /// <remarks>
    /// A walker's pace is a cap and never a profile (PER-3), so this is read as a permission and not as a
    /// speed: it walks while there is ground granted for it to walk into and stands while there is not.
    /// </remarks>
    public float[] AuthorityM { get; }

    /// <summary>
    /// What the grant was cut at, which is only ever <see cref="LaneUse.Reserved"/> — the road another
    /// walker has taken — or <see cref="LaneUse.Obstruction"/>, a body that is going nowhere. <b>The
    /// difference is what tells waiting from being stuck</b>: a queue moves on its own and a body in the
    /// way does not.
    /// </summary>
    public LaneUse[] HeldBy { get; }

    /// <summary>
    /// How long this body has been waiting to get across a crossing — standing at its kerb, or stopped in
    /// the road at the edge of a lane somebody else has. Only the gap spends it, never a red.
    /// </summary>
    public float[] WaitingToCrossS { get; }

    /// <summary>
    /// Which lane of a crossing that wait is for, or <see cref="NoLane"/>. <b>What says when the wait is
    /// over</b>: it ends when the body is standing in that lane and not when the traffic gives way, or the
    /// patience that bought the ground would be handed back a tick before the body used it.
    /// </summary>
    public int[] WaitingForLane { get; }

    /// <summary>
    /// <b>The way a lane this body asked for was refused on, or <see cref="NoWay"/> where it was refused
    /// none</b>, with <see cref="RefusedAtM"/> the metre of that way the lane's band begins at. Written when
    /// the ask is answered against the road's book and read when the walk is granted, so <b>the refusal is
    /// arrived at once and spent in the other network's metres</b> rather than asked twice of two books.
    /// </summary>
    /// <remarks>
    /// It is not <see cref="WaitingForLane"/>, though the two are set together and regularly agree: that
    /// one is patience bookkeeping and stands until the body is <em>in</em> the lane, and this one is the
    /// answer to this tick's ask and goes the moment the ask is granted. Read the wrong one and a body
    /// granted the band in front of it is still held at its edge, which is a body that never finishes
    /// crossing.
    /// </remarks>
    public int[] RefusedWay { get; }

    /// <summary>Where on <see cref="RefusedWay"/> that lane's band begins, in the way's own metres.</summary>
    public float[] RefusedAtM { get; }

    /// <summary>Where that kerb is — taken when the wait begins, because the stand-off is measured from the paint and not from wherever the body has backed off to.</summary>
    public Vector2[] KerbM { get; }

    /// <summary>
    /// Whether it is waiting there now. <b>A leg is not over because a walker is standing still</b>, so
    /// this is what tells the two apart at the decision clock.
    /// </summary>
    public bool[] HeldAtTheKerb { get; }

    /// <summary>
    /// PER-9's own state: what this person is doing about the trip they are on. <b>Observable</b> — it
    /// is what the interface reads out beside a selected walker and what the trip probe counts.
    /// </summary>
    public TripStage[] Stage { get; }

    /// <summary>The building this trip is for (PER-9), or <see cref="NoBuilding"/>. Its claim is held while the walk lasts.</summary>
    public int[] DestinationBuilding { get; }

    /// <summary>The car this trip is using (PER-10), or <see cref="NoCar"/>. <b>This trip's car and no other</b>.</summary>
    public int[] TripCar { get; }

    /// <summary>What is left of a bounded interval — the dwell inside a building, or `P-12`'s own idle.</summary>
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

    public bool[] Walking { get; }

    /// <summary>CTL-4: an ordered walker idles awaiting the next order instead of drawing a new destination.</summary>
    public bool[] Manual { get; }

    /// <summary>
    /// PER-12, and the whole of what a person's terminal state is (PHY-3): dead, and therefore taking
    /// no further actions (AGT-5). <b>The body is never removed</b> (PHY-5) — it keeps its shape, stays
    /// dynamic and can still be shoved down the road, which is what a corpse in a carriageway is for.
    /// </summary>
    public bool[] Dead { get; }

    /// <summary>
    /// What is left of the 0.25 s stumble window after a vehicle struck this walker and it
    /// survived. Above zero the walker is off its feet and the sliding grip applies — which is what
    /// leaves the impulse of an impact visible after the impact is over. <b>Intent is never suspended
    /// by it</b>: the body still declares whatever its manoeuvre asks, and only the friction that could
    /// act on the declaration is reduced.
    /// </summary>
    public float[] OffFeetForS { get; }

    public float[] MassKg { get; }

    public float[] RadiusM { get; }

    public byte[] Variant { get; }

    /// <summary>The agent's own stream, so a walker's destinations are its own and are reproducible.</summary>
    public Rng[] Draw;

    /// <summary>Metres covered on foot. The walk cycle is stepped by <em>distance</em>, not by time, so ground that slows a walker slows its stride.</summary>
    public float[] DistanceWalkedM { get; }

    /// <summary>The ground's own factor under this walker, sampled once a tick and read by everything that needs it.</summary>
    public float[] GroundCoefficient { get; }

    public int Add(BodyId body, Vector2 positionM, float headingRad, float massKg, float radiusM, byte variant, Rng draw)
    {
        if (Count == Capacity) throw new InvalidOperationException($"The roster was laid for {Capacity} walkers and is full.");

        var person = Count++;
        Body[person] = body;
        PositionM[person] = positionM;
        VelocityMps[person] = Vector2.Zero;
        HeadingRad[person] = headingRad;
        DestinationM[person] = positionM;
        StoodAtM[person] = positionM;
        Walking[person] = false;
        Manual[person] = false;
        Dead[person] = false;
        OffFeetForS[person] = 0f;
        MassKg[person] = massKg;
        RadiusM[person] = radiusM;
        Variant[person] = variant;
        Draw[person] = draw;
        DistanceWalkedM[person] = 0f;
        GroundCoefficient[person] = 1f;
        GoalM[person] = positionM;
        WalkedCount[person] = 0;
        WalkedTaken[person] = 0;
        OnWay[person] = NoWay;
        OnWayM[person] = 0f;
        ReserveAheadM[person] = 0f;
        AuthorityM[person] = float.PositiveInfinity;
        HeldBy[person] = LaneUse.Reserved;
        WaitingToCrossS[person] = 0f;
        WaitingForLane[person] = NoLane;
        RefusedWay[person] = NoWay;
        HeldAtTheKerb[person] = false;
        Stage[person] = TripStage.StandingBy;
        DestinationBuilding[person] = NoBuilding;
        TripCar[person] = NoCar;
        TimerS[person] = 0f;
        Inside[person] = Contained.Nowhere;
        return person;
    }

    /// <summary>Dead, or inside the stumble window: either way what acts on the body is the sliding grip and not a sole.</summary>
    public bool IsOnItsFeet(int person) => !Dead[person] && OffFeetForS[person] <= 0f;

    /// <summary>
    /// Whether the pavement's book is holding this walker where it stands: no ground granted past its own
    /// front, so there is nowhere for the next stride to go. <b>The grant is a permission and not a
    /// speed</b> (PER-3, PER-13) — there is ground to walk into or there is not.
    /// </summary>
    /// <remarks>
    /// The kerb is asked first and answers for itself (`P-3`): a walker waiting out a red stands where the
    /// kerb put it rather than where the pavement in front of it ran out, and it may still walk back to the
    /// stand-off while it waits.
    /// </remarks>
    public bool IsHeldByTheBook(int person) =>
        Walking[person] && !HeldAtTheKerb[person] && AuthorityM[person] <= 0f;

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

    /// <summary>Which crossing the next point of the line stands on, or −1 — the whole of what the kerb is asked about.</summary>
    public int CrossingAhead(int person) =>
        WalkedTaken[person] < WalkedCount[person]
            ? WalkedCrossing[(person * WalkedPointsPerPerson) + WalkedTaken[person]]
            : -1;

    /// <summary>How much of the walked line is still ahead of the body.</summary>
    public int WalkedLeft(int person) => WalkedCount[person] - WalkedTaken[person];

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
    }
}
