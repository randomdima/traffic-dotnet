using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Parking;

/// <summary>
/// <b>The ways at a bay</b> (GEN-4f): a pair per standing the bay affords and per lane it is worked off —
/// into the bay, and out of it. They are ways of the road in every sense — arcs, a length, metres of their
/// own, and a ribbon in the town's atlas — and they are the whole of what would make a car park a place the
/// ordinary mechanisms reach.
/// </summary>
/// <remarks>
/// <para>
/// <b>A bay is a mini-junction and not a special case.</b> Which ground a way shares with the lanes it
/// crosses is the same question a junction's joins ask of each other (TER-5c), answered by the same marks
/// (<see cref="RibbonAtlas.Marks"/>) — so a car entering a bay is held off the traffic, and the traffic off
/// it, by the mechanism that already holds a junction apart, and by no second one (SIM-7).
/// </para>
/// <para>
/// <b>The pair is two ways</b>, because a way's metres run in the direction it is driven and everything that
/// reads one — a claim, a grant, a crossing — counts from its start. The two directions of a street are two
/// lanes here for exactly the same reason.
/// </para>
/// <para>
/// <b>A car stands in a bay one of two ways round</b> (GEN-4j). Nose in, it drove in and must reverse out;
/// backed in, it was stood there and drives out — nothing reverses into a bay. So the ways differ only in
/// which end is the bay's and which gear the car is in (<see cref="IsDrivenInReverse"/>).
/// </para>
/// <para>
/// <b>The way is the bay's and not the car's</b> (GEN-4e, and the same argument the walker's way in is
/// settled by). Laid from the pose the car happens to be standing in, the line into a bay is a different
/// line every time it is asked for, so nothing can be said about the ground it takes until the car is on top
/// of it. A way of the town's is one line, and the car converges onto it the way it converges onto every
/// other line in the town.
/// </para>
/// <para>
/// <b>A bay is an arm of a car park's junction</b> (GEN-4h), so every way here runs down the arm's own line
/// and meets the street's own line, <b>turning between the two on the car's own circle</b> (<see cref="Build"/>).
/// The road's lanes and these ways cover the same ground, and the atlas marks every pair of them that does,
/// which is what holds a car on one off a body on the other.
/// </para>
/// </remarks>
internal sealed class BayWays
{
    /// <summary>No way: what a bay no way reaches answers, and a bay no leg ever claims.</summary>
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
    readonly int[] _street;
    readonly float[] _onTheStreetM;
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
        float[] atLaneM, int[] street, float[] onTheStreetM, float[] lengthM, float[] drivenM,
        bool[] isEntry, bool[] isNoseIn, int[] arcOffsets, ArcSeg[] arcs, Vector2[] atTheBayM)
    {
        _firstWay = firstWay;
        _firstWayOfBay = firstWayOfBay;
        _firstBayOfLane = firstBayOfLane;
        _baysOffLane = baysOffLane;
        _bay = bay;
        _lane = lane;
        _atLaneM = atLaneM;
        _street = street;
        _onTheStreetM = onTheStreetM;
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
    /// <b>How many ways this bay is worked off</b>. They are numbered together, so a bay's ways are a run of
    /// the band and not a set to gather.
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
    /// <b>The other half of this way's pair</b> — in and out of one standing off one lane (GEN-4f) — or
    /// <see cref="NoWay"/> where the lane laid only the one.
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
    /// is whatever comes out the other way. The nose-in shape is asked for first, being the one a leg can
    /// drive without stopping to change gear.
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

            // <b>And a stretch with no way out of it</b>, which is a dead end: a search may turn a leg there
            // with no bay, and nothing turns the car round when it arrives — it stands at the end until its
            // leg's clock gives the leg up (CAR-15a).
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
    /// <b>The standing a car put in this bay is stood in</b> — a spawn, a service vehicle, a wreck set down:
    /// the one asked for where the bay lays a way out of it, and the other where it does not. <b>A way out is
    /// all it needs</b>, since nothing drove it in; a car backed into a bay no car is ever reversed into still
    /// drives out of it (<see cref="Laying.BackedIn"/>).
    /// </summary>
    public bool TheStandingOnOffer(int bay, bool wantsNoseIn) =>
        TheWay(bay, NoLane, entry: false, wantsNoseIn) != NoWay ? wantsNoseIn : !wantsNoseIn;

    public bool CanBeReached(int bay) => WayCountOf(bay) > 0;

    /// <summary>
    /// <b>The bay a lane of the road is the arm of</b> (GEN-4h), or <see cref="NoBay"/> for a lane of the
    /// carriageway. A body standing in a bay stands on its arm, which is the road's nearest lane to it and
    /// not one its ways are worked off.
    /// </summary>
    public int BayOnArm(int lane) => lane >= 0 && lane < ArmOfLane.Length ? ArmOfLane[lane] : NoBay;

    /// <summary>No bay: what a lane of the carriageway is the arm of.</summary>
    public const int NoBay = -1;

    int[] ArmOfLane { get; init; } = [];

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
    /// And how far along that lane it does so — where a route down the lane runs out into a way in, and where
    /// a way out lands, which is the lane's own end or start where it lands in the car park's box
    /// (<see cref="OnTheStreetM"/>).
    /// </summary>
    public float AtLaneM(int way) => _atLaneM[way - _firstWay];

    /// <summary>
    /// <b>The street this way meets, as the lane a line down it is laid from</b>: the carriageway lane that
    /// arrives at the car park on that side, whose line runs on straight through the box
    /// (<see cref="ThroughTheBox"/>). A way in leaves it, and a way out lands on it — often inside the box,
    /// where no lane runs.
    /// </summary>
    public int StreetOf(int way) => _street[way - _firstWay];

    /// <summary>
    /// And where along that line — the lane's own metres, run on past its end through the box. It is what a
    /// car leaving a bay is seated on its street by (<c>TownWorld.TakeTheStreetOutOfTheBay</c>).
    /// </summary>
    public float OnTheStreetM(int way) => _onTheStreetM[way - _firstWay];

    /// <summary>
    /// <b>The lane a car on this one carries on to through a car park's box</b>, or <see cref="NoLane"/>:
    /// the one movement off it that is not onto a bay's arm, since no junction turns a car back the way it
    /// came (TER-5f).
    /// </summary>
    public static int ThroughTheBox(RoadGraph roads, int lane)
    {
        foreach (var onto in roads.LanesFrom(lane))
        {
            if (!roads.IsABayArm(onto)) return onto;
        }

        return NoLane;
    }

    /// <summary>And the lane a car arrives on this one from, the same way round.</summary>
    static int IntoTheBox(RoadGraph roads, int lane)
    {
        foreach (var from in roads.LanesIntoJunction(roads.LaneFromJunction[lane]))
        {
            if (!roads.IsABayArm(from) && roads.ConnectorBetween(from, lane) != RoadGraph.NoConnector) return from;
        }

        return NoLane;
    }

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
    /// of a way out, which begins at that pose. What is past it is ground and nothing else — no route is
    /// threaded through it, and a car standing at the pose has come to the end of its line.
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
    /// <b>The ways, laid off the car parks the plan cut</b> (GEN-53, GEN-4h): a bay is its arm, and <b>every way
    /// is the arm's line and the street's joined by one turn on the car's own circle</b>
    /// (<see cref="SimConfig.CarParkingTemplateRadiusM"/>), at the corner the two lines make.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Not the car park's own movements.</b> Those are laid tighter than a car turns
    /// (<see cref="SimConfig.CarParkTurnRadiusM"/>) and run from the edge of the box, so a way read off them
    /// put a car through every metre of the box both ways: backing out of the furthest bay was the whole box
    /// and a staging length beyond it, up the street against its own traffic.
    /// </para>
    /// <para>
    /// <b>A way in leaves its lane where its turn begins, or at the lane's end where the turn is further on</b>
    /// — a route stops on a lane and never inside a box (<see cref="LineAssembler.Assemble"/>), so a car bound
    /// for a bay deeper in carries on straight across the box to its turn. <b>A way out runs a run-out along
    /// the street past its turn</b> (<see cref="SimConfig.ParkingRunOutM"/>): a leg hands one line on with the
    /// car at rest in the last car length of it (<c>TownWorld.TheLineIsSpent</c>), and a way that ended on the
    /// turn would hand a car over crosswise in the street.
    /// </para>
    /// <para>
    /// <b>Nose in, a car turns in forwards and backs out with its tail swung up the street</b>, so it stands
    /// facing the way the street runs and pulls away. Backed in, it drives out forwards; nothing reverses in
    /// (GEN-4j). <b>Only a way that crosses no oncoming stream is reversed over</b>: the near side's, and either
    /// side of a street that runs one way.
    /// </para>
    /// <para>
    /// <b>The turn is no wider than the room</b>, and where the room is tighter than the car park's own turn the
    /// standing is not laid off that lane at all.
    /// </para>
    /// </remarks>
    public static BayWays Build(CityPlan plan, RoadGraph roads, SimConfig config)
    {
        var parks = plan.CarParks;
        var (armIn, armOut) = TheArms(parks, roads);
        var armOfLane = ArmsByLane(roads.LaneCount, armIn, armOut);
        var (turnsIn, turnsOut) = TheMovements(roads, armIn, armOut, armOfLane);
        var laying = new Laying(roads, config, MostArcsThroughABox(roads), parks.Road.Length);

        for (var bay = 0; bay < parks.Road.Length; bay++)
        {
            laying.Begin(bay);
            if (armIn[bay] < 0 || armOut[bay] < 0) continue;

            laying.AtTheArm(armIn[bay], armOut[bay]);
            foreach (var connector in turnsIn[bay]) laying.NoseIn(connector);
            foreach (var connector in turnsOut[bay]) laying.BackedIn(connector);
        }

        return laying.Into(TownWays.FirstBayWay(roads), armOfLane);
    }

    /// <summary>
    /// <b>The ways as they are laid, one bay at a time</b> — a bay's run of ways being contiguous is what
    /// <see cref="WayOf"/> reads them by.
    /// </summary>
    sealed class Laying(RoadGraph roads, SimConfig config, int mostArcsThroughABox, int bays)
    {
        /// <summary>The most one way takes: the straight before its turn, the turn, and the straight after it.</summary>
        const int ArcsOfAWay = 3;

        readonly int[] _firstWayOfBay = new int[bays + 1];
        readonly List<int> _bay = [];
        readonly List<int> _lane = [];
        readonly List<float> _atLaneM = [];
        readonly List<int> _street = [];
        readonly List<float> _onTheStreetM = [];
        readonly List<float> _lengthM = [];
        readonly List<float> _drivenM = [];
        readonly List<bool> _isEntry = [];
        readonly List<bool> _isNoseIn = [];
        readonly List<int> _arcOffsets = [0];
        readonly List<ArcSeg> _arcs = [];
        readonly List<Vector2> _atTheBayM = [];
        readonly ArcSeg[] _shape = new ArcSeg[ArcsOfAWay];
        readonly ArcSeg[] _streetLine = new ArcSeg[mostArcsThroughABox];
        readonly int[] _streetLanes = new int[2];
        readonly float[] _streetLaneStartM = new float[2];
        readonly float[] _streetLaneEndM = new float[2];
        readonly float _turnM = config.CarParkingTemplateRadiusM;
        int _mostArcs;
        int _space;
        Vector2 _noseInAxleM;
        Vector2 _backedInAxleM;
        Vector2 _farEndM;
        float _intoRad;
        float _streetM;

        public void Begin(int bay)
        {
            _space = bay;
            _firstWayOfBay[bay] = _bay.Count;
        }

        /// <summary>
        /// <b>Square in the middle of the space</b> (GEN-4i), which the arm is. The arm's two lanes run over one
        /// line (GEN-53), so the way out reads the same metres from its own end.
        /// </summary>
        public void AtTheArm(int into, int outOf)
        {
            var armM = roads.LaneLengthM[into];
            var noseInM = BayTemplate.RearAxleIntoTheBayM(config.CarCentreAheadOfAxleM, armM, noseIn: true);
            var backedInM = MathF.Max(
                0f,
                roads.LaneLengthM[outOf] - BayTemplate.RearAxleIntoTheBayM(config.CarCentreAheadOfAxleM, armM, noseIn: false));
            _noseInAxleM = Spline.SampleAt(roads.ArcsOf(into), noseInM).PositionM;
            _backedInAxleM = Spline.SampleAt(roads.ArcsOf(outOf), backedInM).PositionM;

            var farEnd = Spline.SampleAt(roads.ArcsOf(into), armM);
            _farEndM = farEnd.PositionM;
            _intoRad = farEnd.HeadingRad;
        }

        /// <summary>In forwards off the lane, and out backwards onto it where that crosses nothing.</summary>
        public void NoseIn(int connector)
        {
            var lane = roads.ConnectorFrom(connector);
            var street = TheStreet(lane);
            if (!TheCorner(street, _noseInAxleM, out var cornerM, out var depthM)) return;

            // <b>Up to the far end of the space and driven as far as the pose</b> (GEN-4f), square in it for the
            // last of that, and begun wherever the turn begins — or at the lane's end, where the car carries on
            // across the box to a turn further on.
            var squaresUpM = config.ParkingStraightensUpM;
            var perM = PerRadiusM(Spline.WrapRad(_intoRad - Spline.SampleAt(street, cornerM).HeadingRad));
            var startM = Math.Clamp(cornerM - (MathF.Min(_turnM, (depthM - squaresUpM) / perM) * perM), 0f, roads.LaneLengthM[lane]);
            var start = Spline.SampleAt(street, startM);
            var radiusM = TheTurnM(start.PositionM, start.HeadingRad, _noseInAxleM, _intoRad, squaresUpM);
            var count = radiusM > 0f
                ? Spline.StraightArcStraightInto(start.PositionM, start.HeadingRad, _farEndM, _intoRad, radiusM, _shape)
                : 0;
            if (count > 0)
            {
                var way = _shape.AsSpan(0, count);
                var lengthM = LengthOf(way);
                var drivenM = lengthM - Vector2.Distance(_farEndM, _noseInAxleM);
                Add(lane, startM, lane, startM, _noseInAxleM, way, lengthM, drivenM, entry: true, noseIn: true);
            }

            if (!BacksOverNothingOncoming(roads, connector, lane)) return;

            if (Out(street, _noseInAxleM, cornerM, depthM, upTheStreet: true, out var landingM, out count))
            {
                var way = _shape.AsSpan(0, count);
                var lengthM = LengthOf(way);
                var atLaneM = MathF.Min(landingM, roads.LaneLengthM[lane]);
                Add(lane, atLaneM, lane, landingM, _noseInAxleM, way, lengthM, lengthM, entry: false, noseIn: true);
            }
        }

        /// <summary>
        /// Out forwards onto the lane. <b>Never reversed in</b>: a car backing in has driven past the car park
        /// first, and whoever was following it stops at its tail — on the ground it has to reverse over, and
        /// waiting on it to move.
        /// </summary>
        public void BackedIn(int connector)
        {
            var lane = roads.ConnectorTo(connector);
            var from = IntoTheBox(roads, lane);
            if (from == NoLane || ThroughTheBox(roads, from) != lane) return;

            var street = TheStreet(from);
            if (!TheCorner(street, _backedInAxleM, out var cornerM, out var depthM)) return;
            if (!Out(street, _backedInAxleM, cornerM, depthM, upTheStreet: false, out var landingM, out var count)) return;

            var way = _shape.AsSpan(0, count);
            var lengthM = LengthOf(way);
            var atLaneM = Math.Clamp(landingM - _streetLaneStartM[1], 0f, roads.LaneLengthM[lane]);
            Add(lane, atLaneM, from, landingM, _backedInAxleM, way, lengthM, lengthM, entry: false, noseIn: false);
        }

        /// <summary>
        /// <b>A way out of the space from a pose in it</b>: out along the arm, round onto the street at the corner
        /// the two lines make, and a run-out along it (<see cref="SimConfig.ParkingRunOutM"/>) — backwards up the
        /// street where the car reverses out, and forwards down it where it drives out.
        /// </summary>
        bool Out(
            ReadOnlySpan<ArcSeg> street, Vector2 poseM, float cornerM, float depthM, bool upTheStreet,
            out float landingM, out int count)
        {
            count = 0;
            landingM = cornerM;
            var outRad = _intoRad + MathF.PI;
            var alongRad = Spline.SampleAt(street, cornerM).HeadingRad + (upTheStreet ? MathF.PI : 0f);
            var perM = PerRadiusM(Spline.WrapRad(alongRad - outRad));
            var runM = (MathF.Min(_turnM, depthM / perM) * perM) + config.ParkingRunOutM;
            var landing = default(SplineSample);
            var landRad = 0f;
            var radiusM = 0f;

            // <b>The run-out is exact and the corner is not</b>: read abeam of the pose, it is where the street's
            // line was there, and a street may bend a little between that and where the car lands. Each pass
            // lands the car, measures the straight the turn actually leaves it, and moves the landing by the
            // difference.
            for (var pass = 0; pass < LandingPasses; pass++)
            {
                landingM = upTheStreet ? cornerM - runM : cornerM + runM;
                if (landingM < 0f || landingM > _streetM) return false;

                landing = Spline.SampleAt(street, landingM);
                landRad = landing.HeadingRad + (upTheStreet ? MathF.PI : 0f);
                if (!Spline.ToTheCorner(poseM, outRad, landing.PositionM, landRad, out var turnRad, out var beforeM, out var toLandingM))
                {
                    return false;
                }

                perM = PerRadiusM(turnRad);
                radiusM = MathF.Min(_turnM, beforeM / perM);
                runM += config.ParkingRunOutM - (toLandingM - (radiusM * perM));
            }

            if (radiusM < config.CarParkTurnRadiusM) return false;

            count = Spline.StraightArcStraightInto(poseM, outRad, landing.PositionM, landRad, radiusM, _shape);
            return count > 0;
        }

        /// <summary>
        /// How many times a way out's landing is moved onto its run-out — each pass takes the error in the one
        /// before down by the street's bend over the few metres the two landings stand apart.
        /// </summary>
        const int LandingPasses = 3;

        /// <summary>
        /// <b>The street's own line on one side of the car park</b>: the lane that arrives at it, run on through
        /// the box onto the lane it carries straight on to (<see cref="ThroughTheBox"/>), measured from that
        /// first lane's own nought as <see cref="OnTheStreetM"/> is.
        /// </summary>
        ReadOnlySpan<ArcSeg> TheStreet(int lane)
        {
            _streetLanes[0] = lane;
            var lanes = 1;
            if (ThroughTheBox(roads, lane) is var onward and not NoLane) _streetLanes[lanes++] = onward;

            var laid = LineAssembler.Assemble(
                roads, _streetLanes.AsSpan(0, lanes), _streetLine, _streetLaneStartM, _streetLaneEndM);
            _streetM = laid.LengthM;
            return _streetLine.AsSpan(0, laid.ArcCount);
        }

        /// <summary>
        /// <b>Where the arm's line crosses the street's</b>, as a metre of the street, and how deep into the space
        /// the pose stands past it — read off the street abeam of the pose, which is where a street straight
        /// enough to carry a car park (GEN-53) is still the line the turn is made onto.
        /// </summary>
        bool TheCorner(ReadOnlySpan<ArcSeg> street, Vector2 poseM, out float cornerM, out float depthM)
        {
            var abeamM = Spline.ProjectM(street, poseM, 0f, _streetM);
            var abeam = Spline.SampleAt(street, abeamM);
            var crosses = Spline.ToTheCorner(abeam.PositionM, abeam.HeadingRad, poseM, _intoRad, out _, out var aheadM, out depthM);
            cornerM = abeamM + aheadM;
            return crosses;
        }

        /// <summary>
        /// <b>The widest turn that fits between two poses</b>, up to the car's own circle and leaving
        /// <paramref name="afterM"/> of straight past it — or nought where only a turn tighter than the car park's
        /// own would (<see cref="SimConfig.CarParkTurnRadiusM"/>).
        /// </summary>
        float TheTurnM(Vector2 fromM, float fromRad, Vector2 toM, float toRad, float afterM)
        {
            if (!Spline.ToTheCorner(fromM, fromRad, toM, toRad, out var turnRad, out var beforeM, out var toCornerM)) return 0f;

            var perM = PerRadiusM(turnRad);
            var radiusM = MathF.Min(_turnM, MathF.Min(beforeM, toCornerM - afterM) / perM);
            return radiusM >= config.CarParkTurnRadiusM ? radiusM : 0f;
        }

        /// <summary>How far a turn through this angle stands off the corner, per metre of its radius.</summary>
        static float PerRadiusM(float turnRad) => MathF.Abs(MathF.Tan(turnRad * 0.5f));

        static float LengthOf(ReadOnlySpan<ArcSeg> way)
        {
            var lengthM = 0f;
            foreach (var arc in way) lengthM += arc.LengthM;

            return lengthM;
        }

        public BayWays Into(int firstWay, int[] armOfLane)
        {
            _firstWayOfBay[bays] = _bay.Count;
            var (firstBayOfLane, baysOffLane) = BaysByLane(roads.LaneCount, _bay, _lane);

            return new BayWays(
                firstWay, _firstWayOfBay, firstBayOfLane, baysOffLane, [.. _bay], [.. _lane], [.. _atLaneM],
                [.. _street], [.. _onTheStreetM], [.. _lengthM], [.. _drivenM], [.. _isEntry], [.. _isNoseIn],
                [.. _arcOffsets], [.. _arcs], [.. _atTheBayM])
            {
                MostArcs = _mostArcs, OffTheRoad = roads, SpaceWidthM = config.ParkingSpaceWidthM, ArmOfLane = armOfLane,
            };
        }

        void Add(
            int lane, float onLaneM, int street, float onTheStreetM, Vector2 axleM, ReadOnlySpan<ArcSeg> line,
            float lengthM, float drivenM, bool entry, bool noseIn)
        {
            _bay.Add(_space);
            _lane.Add(lane);
            _atLaneM.Add(onLaneM);
            _street.Add(street);
            _onTheStreetM.Add(onTheStreetM);
            _atTheBayM.Add(axleM);
            _lengthM.Add(lengthM);
            _drivenM.Add(drivenM);
            _isEntry.Add(entry);
            _isNoseIn.Add(noseIn);
            foreach (var arc in line) _arcs.Add(arc);
            _arcOffsets.Add(_arcs.Count);
            _mostArcs = Math.Max(_mostArcs, line.Length);
        }
    }

    /// <summary>
    /// <b>Every movement onto each bay's arm and off it</b>, found in one pass over the town's movements
    /// rather than one pass a bay — a city has thousands of each.
    /// </summary>
    static (List<int>[] In, List<int>[] Out) TheMovements(RoadGraph roads, int[] armIn, int[] armOut, int[] armOfLane)
    {
        var turnsIn = new List<int>[armIn.Length];
        var turnsOut = new List<int>[armIn.Length];
        for (var bay = 0; bay < armIn.Length; bay++)
        {
            turnsIn[bay] = [];
            turnsOut[bay] = [];
        }

        for (var connector = 0; connector < roads.ConnectorCount; connector++)
        {
            var onto = roads.ConnectorTo(connector);
            if (armOfLane[onto] is var bayOn and >= 0 && armIn[bayOn] == onto) turnsIn[bayOn].Add(connector);

            var off = roads.ConnectorFrom(connector);
            if (armOfLane[off] is var bayOff and >= 0 && armOut[bayOff] == off) turnsOut[bayOff].Add(connector);
        }

        return (turnsIn, turnsOut);
    }

    /// <summary>
    /// Whether a car may reverse over the movement between this lane and a bay (GEN-4j): <b>one that crosses
    /// no oncoming stream</b> — the kerb side's, or any on a street with no stream the other way.
    /// </summary>
    static bool BacksOverNothingOncoming(RoadGraph roads, int connector, int lane) =>
        roads.KindOf(connector) == LaneTurn.NearSide || roads.LaneReverse[lane] < 0;

    /// <summary>Each bay's arm as its two lanes: the one driven in along, and the one driven out along.</summary>
    static (int[] In, int[] Out) TheArms(CityPlan.CarParkArrays parks, RoadGraph roads)
    {
        var bayOfRoad = new Dictionary<int, int>(parks.Road.Length);
        for (var bay = 0; bay < parks.Road.Length; bay++) bayOfRoad[parks.Road[bay]] = bay;

        var into = new int[parks.Road.Length];
        var outOf = new int[parks.Road.Length];
        Array.Fill(into, NoLane);
        Array.Fill(outOf, NoLane);
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (!bayOfRoad.TryGetValue(roads.LaneRoad[lane], out var bay)) continue;

            // A bay's road runs from the car park's junction out to the bay's own node (GEN-53), so the lane
            // with the road is the one driven in.
            if (roads.LaneForward[lane]) into[bay] = lane;
            else outOf[bay] = lane;
        }

        return (into, outOf);
    }

    /// <summary>
    /// The most pieces a street's line through a car park can take (<see cref="Laying.TheStreet"/>): two lanes
    /// and the movement between them, each bounded by the most any one line of the town took.
    /// </summary>
    static int MostArcsThroughABox(RoadGraph roads)
    {
        var mostLane = 0;
        for (var lane = 0; lane < roads.LaneCount; lane++) mostLane = Math.Max(mostLane, roads.ArcsOf(lane).Length);

        var mostTurn = 0;
        for (var connector = 0; connector < roads.ConnectorCount; connector++)
        {
            mostTurn = Math.Max(mostTurn, roads.ConnectorArcs(connector).Length);
        }

        return (2 * mostLane) + mostTurn;
    }

    /// <summary>
    /// The ways read the other way round: which bays each lane works. A bay lays up to four ways off one
    /// lane — the pair per standing — and appears in its lane's run once.
    /// </summary>
    static (int[] Offsets, int[] Bays) BaysByLane(int laneCount, List<int> bayOfWay, List<int> laneOfWay)
    {
        var perLane = new List<int>?[laneCount];
        for (var way = 0; way < bayOfWay.Count; way++)
        {
            var bays = perLane[laneOfWay[way]] ??= [];
            if (!bays.Contains(bayOfWay[way])) bays.Add(bayOfWay[way]);
        }

        var offsets = new int[laneCount + 1];
        var flat = new List<int>();
        for (var lane = 0; lane < laneCount; lane++)
        {
            if (perLane[lane] is { } bays) flat.AddRange(bays);
            offsets[lane + 1] = flat.Count;
        }

        return (offsets, [.. flat]);
    }

    /// <summary>Which bay each lane is an arm of, or <see cref="NoBay"/> for every lane of the carriageway.</summary>
    static int[] ArmsByLane(int laneCount, int[] armIn, int[] armOut)
    {
        var arms = new int[laneCount];
        Array.Fill(arms, NoBay);
        for (var bay = 0; bay < armIn.Length; bay++)
        {
            if (armIn[bay] >= 0) arms[armIn[bay]] = bay;
            if (armOut[bay] >= 0) arms[armOut[bay]] = bay;
        }

        return arms;
    }
}
