using System.Numerics;
using TrafficSimulation.Agents.Person.Control;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Containment;
using TrafficSimulation.World.Foot;
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
        ReserveAheadM = new float[capacity];
        AuthorityM = new float[capacity];
        Array.Fill(AuthorityM, float.PositiveInfinity);
        HeldBy = new int[capacity];
        Array.Fill(HeldBy, NoBody);
        StepsRound = new int[capacity];
        Array.Fill(StepsRound, NoBody);
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
        ClosesTheRoadM = new float[capacity];
        Inside = new Contained[capacity];
    }

    /// <summary>What a person holds when this trip has no building of its own to be at, and no car in it.</summary>
    public const int NoBuilding = -1;

    public const int NoCar = -1;

    /// <summary>What a person holds when the pavement's book has nowhere to put it.</summary>
    public const int NoWay = -1;

    /// <summary>And when it is waiting for no lane of a crossing, which is nearly always.</summary>
    public const int NoLane = -1;

    /// <summary>And when there is nothing in its way to be stepped round (PER-24).</summary>
    public const int NoBody = -1;

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
    /// <b>Whose stretch the grant was cut at</b>, or <see cref="NoBody"/> where nothing cut it — the
    /// driving side's <c>GrantCutBy</c> for walkers, and read for the same reason: the distance says a body
    /// is being held and only this says by whom, which is the whole of what tells a queue from a jam.
    /// </summary>
    public int[] HeldBy { get; }

    /// <summary>
    /// <b>The body in front that is going nowhere</b>, or <see cref="NoBody"/> — a walker standing about,
    /// somebody knocked down, one shoved off its own line. It cuts no grant (PER-24): what the walker does
    /// with it is aim past it, so this is written where the grant is taken and read by the follower.
    /// </summary>
    /// <remarks>
    /// <b>It is the nearest one on the ground this body asked for</b> and never a scan of the fleet, so a
    /// walker steps round what its own reservation ran into and takes no notice of a body on the other side
    /// of the street.
    /// </remarks>
    public int[] StepsRound { get; }

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

    /// <summary>What is left of a bounded interval — the dwell inside a building (PER-11), or the idle between goals.</summary>
    public float[] TimerS { get; }

    /// <summary>
    /// <b>How much road this body closes</b> (SRV-6), either side of itself along the lane it is standing
    /// beside — zero for everybody in the town but an officer working a scene.
    /// </summary>
    /// <remarks>
    /// <b>A body closing the road is a body that holds more road than it stands on</b>, and stating it that
    /// way is what keeps the road's book from ever learning what a policeman is: the book reads one float
    /// and lays one claim, on the terms every other stretch is laid on (TER-5e).
    /// </remarks>
    public float[] ClosesTheRoadM { get; }

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
        StoodAtM[person] = positionM;
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
        ReserveAheadM[person] = 0f;
        AuthorityM[person] = float.PositiveInfinity;
        HeldBy[person] = NoBody;
        StepsRound[person] = NoBody;
        WaitingToCrossS[person] = 0f;
        WaitingForLane[person] = NoLane;
        RefusedWay[person] = NoWay;
        HeldAtTheKerb[person] = false;
        Stage[person] = TripStage.StandingBy;
        DestinationBuilding[person] = NoBuilding;
        TripCar[person] = NoCar;
        TimerS[person] = 0f;
        ClosesTheRoadM[person] = 0f;
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

    /// <summary>
    /// Whether the pavement's book is holding this walker where it stands: less ground granted in front of
    /// it than it needs to come to rest in. <b>The grant is a permission and not a speed</b> (PER-3,
    /// PER-13) — there is ground to walk into or there is not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bar is what the body is carrying and not what its pace is worth</b>, which is what makes the
    /// permission honest at both ends. Read against zero, a walker with a centimetre of grant walks on at
    /// full pace and comes to rest a whole stopping distance <em>inside</em> the gap it keeps: the queue
    /// closes up to one stop short of the standing gap and moves off in lock step, which is a heap and not
    /// a queue. Read against the pace instead, a body at rest is refused a stride it could take, and two
    /// bodies each a little inside the other's gap stand for ever — the creep is the only thing that gets
    /// a pair out of that, and it costs nothing to leave it there.
    /// </para>
    /// <para>
    /// The kerb is asked first and answers for itself (PER-15): a walker waiting out a red stands where the
    /// kerb put it rather than where the pavement in front of it ran out, and it may still walk back to the
    /// stand-off while it waits.
    /// </para>
    /// </remarks>
    /// <param name="stopsInM">What this body needs to come to rest in from the speed it is doing — nothing at rest.</param>
    public bool IsHeldByTheBook(int person, float stopsInM) =>
        Walking[person] && !HeldAtTheKerb[person] && AuthorityM[person] <= stopsInM;

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
