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
/// <b>A bay is an arm of a car park's junction</b> (GEN-4h), so every way here is laid over ground the road
/// already has — the arm's own lanes and the movements onto and off them (<see cref="Build"/>). The road's
/// lanes and these ways cover the same ground, and the atlas marks every pair of them that does, which is
/// what holds a car on one off a body on the other.
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
    /// <b>The other half of this way's pair</b> — one shape driven the other way (GEN-4f) — or
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
    /// <b>The ways, read off the car parks the plan cut</b> (GEN-53, GEN-4h): a bay is its arm, the arm's two
    /// lanes are the space driven in over and out over, and the car park's own movements are what join the
    /// arm to the street. <b>Nothing here draws a curve</b> — every way is the town's lines, joined end to end
    /// and, for the half driven in reverse, walked the other way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nose in, the shape is the last of the lane the movement leaves, the movement onto the arm and the
    /// arm</b>: driven forwards, and backed out over the same ground onto the same place. <b>Backed in, it is
    /// the arm, the movement off it and the first of the lane it lands on</b>: driven out forwards, and
    /// reversed in from there — so a car backing in has passed the car park and stands clear of its box before
    /// it changes gear. <b>Either shape runs a staging length along its lane</b>
    /// (<see cref="SimConfig.ParkingStagedInM"/>): a leg hands one line over to the next with the car at rest in
    /// the last car length of it (<c>TownWorld.TheLineIsSpent</c>), and a way that ended on the turn would hand
    /// a car over crosswise in the street.
    /// </para>
    /// <para>
    /// <b>Only a way that crosses no oncoming stream is reversed over</b> (GEN-4j): the near side's, and
    /// either side of a street that runs one way. Across the carriageway a car noses in and drives out, and
    /// backs over neither.
    /// </para>
    /// </remarks>
    public static BayWays Build(CityPlan plan, RoadGraph roads, SimConfig config)
    {
        var parks = plan.CarParks;
        var (armIn, armOut) = TheArms(parks, roads);
        var armOfLane = ArmsByLane(roads.LaneCount, armIn, armOut);
        var (turnsIn, turnsOut) = TheMovements(roads, armIn, armOut, armOfLane);
        var laying = new Laying(roads, config, MostArcsOfAShape(roads), parks.Road.Length);

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
    sealed class Laying(RoadGraph roads, SimConfig config, int mostArcsOfAShape, int bays)
    {
        readonly int[] _firstWayOfBay = new int[bays + 1];
        readonly List<int> _bay = [];
        readonly List<int> _lane = [];
        readonly List<float> _atLaneM = [];
        readonly List<float> _lengthM = [];
        readonly List<float> _drivenM = [];
        readonly List<bool> _isEntry = [];
        readonly List<bool> _isNoseIn = [];
        readonly List<int> _arcOffsets = [0];
        readonly List<ArcSeg> _arcs = [];
        readonly List<Vector2> _atTheBayM = [];
        readonly ArcSeg[] _shape = new ArcSeg[mostArcsOfAShape];
        readonly ArcSeg[] _reversed = new ArcSeg[mostArcsOfAShape];
        int _mostArcs;
        int _space;
        int _in;
        int _out;
        float _armM;
        float _noseInM;
        float _backedInM;
        Vector2 _noseInAxleM;
        Vector2 _backedInAxleM;

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
            _in = into;
            _out = outOf;
            _armM = roads.LaneLengthM[into];
            _noseInM = BayTemplate.RearAxleIntoTheBayM(config.CarCentreAheadOfAxleM, _armM, noseIn: true);
            _backedInM = MathF.Max(
                0f,
                roads.LaneLengthM[outOf] - BayTemplate.RearAxleIntoTheBayM(config.CarCentreAheadOfAxleM, _armM, noseIn: false));
            _noseInAxleM = Spline.SampleAt(roads.ArcsOf(into), _noseInM).PositionM;
            _backedInAxleM = Spline.SampleAt(roads.ArcsOf(outOf), _backedInM).PositionM;
        }

        /// <summary>
        /// The last of the lane, the movement onto the arm and the arm: in forwards, and out in reverse where that
        /// crosses nothing.
        /// </summary>
        public void NoseIn(int connector)
        {
            var lane = roads.ConnectorFrom(connector);
            var laneM = roads.LaneLengthM[lane];
            var stagedM = MathF.Min(config.ParkingStagedInM, laneM);
            var turnM = roads.ConnectorLengthM(connector);

            var count = Spline.SubChainInto(roads.ArcsOf(lane), laneM - stagedM, laneM, _shape);
            count = Append(_shape, count, roads.ConnectorArcs(connector));
            count = Append(_shape, count, roads.ArcsOf(_in));
            var inM = stagedM + turnM;
            Add(lane, laneM - stagedM, _noseInAxleM, _shape.AsSpan(0, count), inM + _armM, inM + _noseInM, entry: true, noseIn: true);

            if (!BacksOverNothingOncoming(roads, connector, lane)) return;

            count = Spline.SubChainInto(roads.ArcsOf(lane), laneM - stagedM, laneM, _shape);
            count = Append(_shape, count, roads.ConnectorArcs(connector));
            count += Spline.SubChainInto(roads.ArcsOf(_in), 0f, _noseInM, _shape.AsSpan(count));
            Spline.ReverseInto(_shape.AsSpan(0, count), _reversed);
            Add(lane, laneM - stagedM, _noseInAxleM, _reversed.AsSpan(0, count), inM + _noseInM, inM + _noseInM, entry: false, noseIn: true);
        }

        /// <summary>
        /// The arm, the movement off it and the first of the lane it lands on, driven out forwards. <b>Never
        /// reversed in</b>: a car backing in has driven past the car park first, and whoever was following it
        /// stops at its tail — on the ground it has to reverse over, and waiting on it to move.
        /// </summary>
        public void BackedIn(int connector)
        {
            var lane = roads.ConnectorTo(connector);
            var turnM = roads.ConnectorLengthM(connector);
            var onM = MathF.Min(config.ParkingStagedInM, roads.LaneLengthM[lane]);
            var armOutM = roads.LaneLengthM[_out];

            var count = Spline.SubChainInto(roads.ArcsOf(_out), _backedInM, armOutM, _shape);
            count = Append(_shape, count, roads.ConnectorArcs(connector));
            count += Spline.SubChainInto(roads.ArcsOf(lane), 0f, onM, _shape.AsSpan(count));
            var outM = armOutM - _backedInM + turnM + onM;
            Add(lane, onM, _backedInAxleM, _shape.AsSpan(0, count), outM, outM, entry: false, noseIn: false);
        }

        public BayWays Into(int firstWay, int[] armOfLane)
        {
            _firstWayOfBay[bays] = _bay.Count;
            var (firstBayOfLane, baysOffLane) = BaysByLane(roads.LaneCount, _bay, _lane);

            return new BayWays(
                firstWay, _firstWayOfBay, firstBayOfLane, baysOffLane, [.. _bay], [.. _lane], [.. _atLaneM],
                [.. _lengthM], [.. _drivenM], [.. _isEntry], [.. _isNoseIn], [.. _arcOffsets], [.. _arcs],
                [.. _atTheBayM])
            {
                MostArcs = _mostArcs, OffTheRoad = roads, SpaceWidthM = config.ParkingSpaceWidthM, ArmOfLane = armOfLane,
            };
        }

        void Add(
            int lane, float onLaneM, Vector2 axleM, ReadOnlySpan<ArcSeg> line, float lengthM, float drivenM,
            bool entry, bool noseIn)
        {
            _bay.Add(_space);
            _lane.Add(lane);
            _atLaneM.Add(onLaneM);
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
    /// The most pieces one shape can take: the longest arm, the longest movement and the longest stretch of
    /// lane a backed-in way is carried on down — each bounded by the most any one line of the town took.
    /// </summary>
    static int MostArcsOfAShape(RoadGraph roads)
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

    static int Append(ArcSeg[] into, int count, ReadOnlySpan<ArcSeg> arcs)
    {
        arcs.CopyTo(into.AsSpan(count));
        return count + arcs.Length;
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
