using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Parking;

/// <summary>
/// <b>The ways at a bay, laid once with the town</b> (GEN-4f): a line per standing the bay affords and per
/// lane it can be worked off, each carried as the pair of ways it is driven as — into the bay, and out of
/// it. They are ways of the road in every sense — arcs, a length, metres of their own, and a row in
/// the town's table of what is driven over what — and they are the whole of what makes a car park a place
/// the ordinary mechanisms reach.
/// </summary>
/// <remarks>
/// <para>
/// <b>A bay is a mini-junction and not a special case.</b> A way leaves a lane at a place on it, sweeps
/// whatever stands between and ends at the bay's own pose. Which ground it takes off the lanes it crosses is
/// the same question a junction's joins ask of each other (TER-5c), measured with the same code
/// (<see cref="LineOverlap"/>) and read out of the same table (<see cref="WayCrossings"/>) — so a car
/// entering a bay is held off the traffic, and the traffic off it, by the mechanism that already holds a
/// junction apart, and by no second one (SIM-7).
/// </para>
/// <para>
/// <b>The way in and the way out are one line</b> (GEN-4f): one shape is solved and the way out is that
/// shape reversed, so it lands on the lane by construction rather than by a second solve aimed back at it, a
/// bay that can be driven into can be driven out of, and a car leaving retraces the ground it arrived over.
/// <b>The pair is two ways all the same</b>, because a way's metres run in the direction it is
/// driven and everything that reads one — a claim, a grant, a crossing — counts from its start.
/// The two directions of a street are two lanes here for exactly the same reason.
/// </para>
/// <para>
/// <b>A car stands in a bay one of two ways round, and they are different shapes</b> (GEN-4j). Nose in, it
/// drove in and must reverse out; backed in, it reversed in and drives out. Both are the one template,
/// asked with the lane's direction reversed for the second, and the four ways that come of them differ only
/// in which end is the bay's and which gear the car is in — <see cref="IsDrivenInReverse"/>.
/// </para>
/// <para>
/// <b>Reversing is between the bay and the lane beside it and nowhere else</b> (GEN-4j). Both standings are
/// laid off the near lane, where the car may reverse either into the bay or out of it. The far lane is
/// asked the same question and kept only in the direction that is driven <em>forwards</em>: a car may nose
/// into a bay across the carriageway, and one that backed in may drive out across it, but nobody reverses
/// over a lane of moving traffic to do either. <b>So the near lane is what a bay is usable off at all</b> —
/// a standing needs both its ways, and only the near lane lays both — and what the far lane adds is an
/// approach and a departure, never a standing of its own.
/// </para>
/// <para>
/// <b>How near a shape passes the oncoming lane is not asked here</b> (SIM-7). The turn into a bay swings
/// away from it first, out over the carriageway, and how much of the street that takes is the table's
/// question and is answered for every way in the town by one piece of code
/// (<see cref="BayCrossings"/>). A second bar held up here — the body off the oncoming lane's own paint —
/// refuses shapes the table has already said nobody meets on, and refusing a shape costs a bay the
/// standing it served: on a four-metre lane that is every nose-in in the town.
/// </para>
/// <para>
/// <b>The way is the bay's and not the car's</b> (GEN-4e, and the same argument the walker's way in is
/// settled by). Laid from the pose the car happens to be standing in, the line into a bay is a different
/// line every time it is asked for, so nothing can be said about the ground it takes until the car is on top
/// of it — which is a manoeuvre nobody can be held off. Laid once from the lane, it is a way, and the car
/// converges onto it the way it converges onto every other line in the town.
/// </para>
/// <para>
/// <b>A way is the manoeuvre and not the approach to it.</b> It begins at the metre of the lane where the
/// car stops driving straight, so the ground before that is the lane's own — driven under the lane's own
/// claim on the way in, and not reversed back up on the way out. A car part-way onto a way still has
/// its tail on the lane behind, and the traffic there is cut by that tail like any other. The lane is
/// treated as straight over the template's own length, which is <see cref="BayTemplate"/>'s stated
/// approximation and not a new one.
/// </para>
/// <para>
/// <b>A bay whose template refuses the near lane both ways round is a bay with no way</b>, and that is the
/// whole of what <see cref="CanBeReached"/> means. It is a refusal of the geometry and never a shortage the
/// caller can work around.
/// </para>
/// </remarks>
internal sealed class BayWays
{
    /// <summary>A bay the template refuses on every lane, which is a bay no trip ever claims.</summary>
    public const int NoWay = -1;

    /// <summary>Asking of whichever lane has one, rather than of a named lane.</summary>
    const int NoLane = -1;

    readonly int _firstWay;
    readonly int[] _firstWayOfBay;
    readonly int[] _firstBayOfLane;
    readonly int[] _baysOffLane;
    readonly int[] _bay;
    readonly int[] _lane;
    readonly float[] _atLaneM;
    readonly float[] _lengthM;
    readonly float[] _drivenM;
    readonly bool[] _isEntry;
    readonly bool[] _isNoseIn;
    readonly int[] _arcOffsets;
    readonly ArcSeg[] _arcs;
    readonly Vector2[] _atTheBayM;
    readonly int[] _waysInOrder;

    BayWays(
        int firstWay, int[] firstWayOfBay, int[] firstBayOfLane, int[] baysOffLane, int[] bay, int[] lane,
        float[] atLaneM, float[] lengthM, float[] drivenM,
        bool[] isEntry, bool[] isNoseIn, int[] arcOffsets, ArcSeg[] arcs, Vector2[] atTheBayM)
    {
        _firstWay = firstWay;
        _firstWayOfBay = firstWayOfBay;
        _firstBayOfLane = firstBayOfLane;
        _baysOffLane = baysOffLane;
        _bay = bay;
        _lane = lane;
        _atLaneM = atLaneM;
        _lengthM = lengthM;
        _drivenM = drivenM;
        _isEntry = isEntry;
        _isNoseIn = isNoseIn;
        _arcOffsets = arcOffsets;
        _arcs = arcs;
        _atTheBayM = atTheBayM;

        _waysInOrder = new int[bay.Length];
        for (var at = 0; at < _waysInOrder.Length; at++) _waysInOrder[at] = firstWay + at;

        for (var space = 0; space < BayCount; space++)
        {
            MostWaysAtABay = Math.Max(MostWaysAtABay, WayCountOf(space));
        }
    }

    /// <summary>The ways the busiest bay has, which is what a walk over this network has to have room for.</summary>
    public int MostWaysAtABay { get; }

    /// <summary>
    /// <b>These read as one of the town's networks</b> (<see cref="BayNetwork"/>) — a view and not a second
    /// structure, so a body is laid onto a bay by the walk that lays it onto a lane and a footway.
    /// </summary>
    public BayNetwork Ways => new(this, OffTheRoad, SpaceWidthM);

    /// <summary>The carriageway these hang off, which is where a walk over them starts from.</summary>
    RoadGraph OffTheRoad { get; init; } = null!;

    /// <summary>How wide a bay's way is measured (<see cref="SimConfig.ParkingSpaceWidthM"/>): the space it serves.</summary>
    float SpaceWidthM { get; init; }

    /// <summary>The way number the band begins at — the road's own ways are numbered before it.</summary>
    public int FirstWay => _firstWay;

    /// <summary>How many ways the bays add to the numbering.</summary>
    public int WayCount => _bay.Length;

    /// <summary>And how many ways the town has once they are in it, which is what the claims are sized to.</summary>
    public int TotalWayCount => _firstWay + _bay.Length;

    public int BayCount => _firstWayOfBay.Length - 1;

    /// <summary>
    /// <b>The ways this bay is worked off</b>, the near lane's first and each shape's pair together — the
    /// pair for every standing the near lane lays, and past them the single forward-driven way each
    /// standing gets off the far one. They are laid with the bay, so a bay's ways are a run of the band and
    /// not a set to gather.
    /// </summary>
    public int WayCountOf(int bay) => _firstWayOfBay[bay + 1] - _firstWayOfBay[bay];

    public int WayOf(int bay, int slot) => _firstWay + _firstWayOfBay[bay] + slot;

    /// <summary>
    /// <b>All of them at once, as the run of ways they are</b> — the lanes at a bay, for the walk that
    /// reads which of them a body standing there is on (<see cref="BayNetwork"/>).
    /// </summary>
    public ReadOnlySpan<int> WaysOf(int bay) => _waysInOrder.AsSpan(_firstWayOfBay[bay], WayCountOf(bay));

    /// <summary>
    /// <b>Where the bay's own end of this way is</b> — the axle pose it was drawn to, which is where a car
    /// standing in the bay stands. Kept because it is the cheapest thing in the town to compare a place
    /// against, and a walk over this network starts by asking which bay it could possibly be at.
    /// </summary>
    public Vector2 AtTheBayM(int way) => _atTheBayM[way - _firstWay];

    /// <summary>
    /// <b>The other half of this way's pair</b> — one shape driven the other way (GEN-4f) — or
    /// <see cref="NoWay"/> where the lane laid only the one, which is every way off the far lane.
    /// </summary>
    public int PairOf(int way) => TheWay(BayOfWay(way), LaneOf(way), !IsEntry(way), IsNoseIn(way));

    /// <summary>
    /// <b>The bays worked off one lane</b>, each named once — the inverse of <see cref="LaneOf"/>, laid
    /// with the town because what a leg asks at a car park's frontage is a question about that stretch
    /// (GEN-4l) and walking every bay in the town to answer it is a scan per leg.
    /// </summary>
    public ReadOnlySpan<int> BaysOffLane(int lane) =>
        _baysOffLane.AsSpan(_firstBayOfLane[lane], _firstBayOfLane[lane + 1] - _firstBayOfLane[lane]);

    /// <summary>
    /// <b>The way this bay is driven into off the given lane</b> — what a leg routed down it finishes on.
    /// The standing asked for where that lane lays it, the other where it does not, and <see cref="NoWay"/>
    /// where the lane reaches the bay at all in neither.
    /// </summary>
    public int WayInOffLane(int bay, int lane, bool noseIn) =>
        TheWay(bay, lane, entry: true, noseIn) is var wanted and not NoWay
            ? wanted
            : TheWay(bay, lane, entry: true, !noseIn);

    /// <summary>
    /// <b>The way into this bay a car turning here drives</b> (GEN-4l), or <see cref="NoWay"/>: the
    /// <em>nose-in</em> entry off <paramref name="offLane"/> where the same standing also lays an exit onto
    /// <paramref name="ontoLane"/>, which is the other lane of the same stretch.
    /// </summary>
    /// <remarks>
    /// <b>The standing is the turn's and not the driver's</b> (GEN-4j, GEN-4l): what a car parks like here
    /// is whatever comes out the other way. Off the lane a bay's kerb is on that is backing in and driving
    /// out across the carriageway; off the lane across the street it is nosing in and reversing out. The
    /// nose-in shape is asked for first, being the one a leg can drive without stopping to change gear.
    /// </remarks>
    public int TheWayToTurnIn(int bay, int offLane, int ontoLane) =>
        TurningWayIn(bay, offLane, ontoLane, noseIn: true) is var noseIn and not NoWay
            ? noseIn
            : TurningWayIn(bay, offLane, ontoLane, noseIn: false);

    /// <summary>The pair one standing lays, as the way in — or <see cref="NoWay"/> where either half is missing.</summary>
    int TurningWayIn(int bay, int offLane, int ontoLane, bool noseIn) =>
        TheWay(bay, ontoLane, entry: false, noseIn) != NoWay ? TheWay(bay, offLane, entry: true, noseIn) : NoWay;

    /// <summary>
    /// <b>The lanes a leg may come back the other way from</b>, one flag per lane of the town — a bay of a
    /// car park it can turn in (GEN-4l). <b>The data
    /// the driving network is priced off</b>, handed over as flags rather than as this type because the
    /// road is below the car parks that hang off it and a slice may not reach up.
    /// </summary>
    public static bool[] WhereALegMayTurn(RoadGraph roads, BayWays bays)
    {
        var turns = new bool[roads.LaneCount];
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var back = roads.LaneReverse[lane];

            // <b>And a stretch with no way out of it</b>, which is a dead end: what turns a car round there
            // is the car itself, on the room TER-5a promises, and no bay is wanted for it.
            turns[lane] = back >= 0 && (bays.ATurnIsLaidBetween(lane, back) || roads.LanesFrom(lane).Length == 0);
        }

        return turns;
    }

    /// <summary>
    /// <b>Whether any bay off this lane can be turned in</b> (GEN-4l) — the frontage's own answer to
    /// whether a leg may come back down <paramref name="ontoLane"/> from it. It says nothing about a bay
    /// being free: that is the leg's question at the frontage.
    /// </summary>
    public bool ATurnIsLaidBetween(int offLane, int ontoLane)
    {
        foreach (var bay in BaysOffLane(offLane))
        {
            if (TheWayToTurnIn(bay, offLane, ontoLane) != NoWay) return true;
        }

        return false;
    }

    /// <summary>Whether a car may come to rest in this bay that way round: it needs both the way in and the way out.</summary>
    public bool CanStand(int bay, bool noseIn) =>
        TheWay(bay, NoLane, entry: true, noseIn) != NoWay && TheWay(bay, NoLane, entry: false, noseIn) != NoWay;

    /// <summary>
    /// <b>The standing a driver actually gets here</b>: the one it would take where the bay lays it, and the
    /// other where it does not. A bay that lays neither is one <see cref="CanBeReached"/> refuses.
    /// </summary>
    public bool TheStandingOnOffer(int bay, bool wantsNoseIn) => CanStand(bay, wantsNoseIn) ? wantsNoseIn : !wantsNoseIn;

    public bool CanBeReached(int bay) => WayCountOf(bay) > 0;

    /// <summary>One of this bay's ways, or <see cref="NoWay"/>; <see cref="NoLane"/> asks of any lane.</summary>
    int TheWay(int bay, int lane, bool entry, bool noseIn)
    {
        for (var slot = 0; slot < WayCountOf(bay); slot++)
        {
            var way = WayOf(bay, slot);
            if (IsEntry(way) == entry && IsNoseIn(way) == noseIn && (lane == NoLane || LaneOf(way) == lane))
            {
                return way;
            }
        }

        return NoWay;
    }

    /// <summary>Whether a numbered way is one of these rather than a lane or a junction's join.</summary>
    public bool IsBayWay(int way) => way >= _firstWay && way < TotalWayCount;

    public int BayOfWay(int way) => _bay[way - _firstWay];

    /// <summary>
    /// Which way round this one is driven: in from the lane, or out to it. The pair covers the same ground,
    /// and which end of it the bay is at is the whole of what this answers.
    /// </summary>
    public bool IsEntry(int way) => _isEntry[way - _firstWay];

    /// <summary>Which way round the car stands at the bay end of this way: nose into the space, or backed into it.</summary>
    public bool IsNoseIn(int way) => _isNoseIn[way - _firstWay];

    /// <summary>
    /// <b>And therefore the gear it is driven in</b> (GEN-4j): a car noses in and reverses out, or reverses
    /// in and drives out. The two facts above are what a way is; this is the one thing every driver of one
    /// needs from them.
    /// </summary>
    public bool IsDrivenInReverse(int way) => IsEntry(way) != IsNoseIn(way);

    /// <summary>The carriageway lane this way leaves, for a way in, or arrives on, for a way out.</summary>
    public int LaneOf(int way) => _lane[way - _firstWay];

    /// <summary>
    /// And how far along that lane it does so — where a route down the lane runs out, and where a car
    /// backing out lands. One metre for both of a pair, because it is one line.
    /// </summary>
    public float AtLaneM(int way) => _atLaneM[way - _firstWay];

    /// <summary>
    /// <b>The way's own metres, end to end</b> — which for a way in runs past the pose a car comes to rest
    /// in, on to the far end of the space itself (GEN-4f).
    /// </summary>
    /// <remarks>
    /// <b>It is the ground and not the drive</b>, which is the same split a lane carries (TER-5d): a lane's
    /// line runs the whole stretch and a movement joins and leaves it inside its own ends. A bay's way ended
    /// at the pose instead, and then the deepest metres of the space — the ground in front of a car that
    /// nosed in, which is most of the space — belonged to no way at all, so a body standing there claimed
    /// nothing and the driver aiming at that space read it as empty (TER-4c.2).
    /// </remarks>
    public float LengthM(int way) => _lengthM[way - _firstWay];

    /// <summary>
    /// <b>And how much of it is driven</b>: from the lane to the pose in the bay for a way in, and the whole
    /// of a way out, which begins at that pose. What is past it is ground and nothing else — nothing is
    /// driven over it (<see cref="BayCrossings"/>), no route is threaded through it, and a car standing at
    /// the pose has come to the end of its line.
    /// </summary>
    public float DrivenLengthM(int way) => _drivenM[way - _firstWay];

    /// <summary>Every one of them measured, in way order — what the claims are laid over them by.</summary>
    public ReadOnlySpan<float> LengthsM => _lengthM;

    /// <summary>
    /// The line itself, in the direction the rear axle travels along it — <b>including the run past the pose
    /// that only a way in has</b> (<see cref="LengthM"/>), so whoever wants the drive and not the ground
    /// takes the chain as far as <see cref="DrivenLengthM"/>.
    /// </summary>
    public ReadOnlySpan<ArcSeg> ArcsOf(int way)
    {
        var at = way - _firstWay;
        return _arcs.AsSpan(_arcOffsets[at], _arcOffsets[at + 1] - _arcOffsets[at]);
    }

    /// <summary>The most arcs any one of them took, which is what a line assembled through one has to have room for.</summary>
    public int MostArcs { get; private init; }

    /// <summary>
    /// <b>Read off the lines the plan carries</b> rather than laid here. A bay's way is ground the town is
    /// made of — it is what a car park <em>is</em>, the way a junction is its own movements (TER-5) — so it
    /// is laid where the ground is laid and this holds what the driving makes of it: which lane each way
    /// works, which way round the car ends up, and the numbering the rest of the network reads them by.
    /// </summary>
    /// <remarks>
    /// <b>Nothing lays a bay's way</b>, so every bay the plan carries is a bay no way reaches — which is
    /// what <see cref="CanBeReached"/> already answers for, and is the state a map with no frontage has
    /// always stood up in. The slice stands rather than being unpicked: parking is in the road back, and
    /// the lines it reads come with it.
    /// </remarks>
    public static BayWays Build(CityPlan plan, RoadGraph roads, SimConfig config) =>
        new(
            TownWays.FirstBayWay(roads), firstWayOfBay: new int[plan.ParkingLots.SpaceCount + 1],
            firstBayOfLane: new int[roads.LaneCount + 1],
            baysOffLane: [], bay: [], lane: [], atLaneM: [], lengthM: [], drivenM: [], isEntry: [],
            isNoseIn: [], arcOffsets: [0], arcs: [], atTheBayM: [])
        {
            MostArcs = 0, OffTheRoad = roads, SpaceWidthM = config.ParkingSpaceWidthM,
        };
}
