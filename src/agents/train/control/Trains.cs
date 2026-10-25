using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Rail;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.Agents.Train.Control;

/// <summary>What a train asks of the world for one of its vehicles (<see cref="TrainOrder"/>).</summary>
internal enum TrainOrderKind : byte
{
    /// <summary>Put down at a place, facing a heading and moving along it, in a look (<see cref="TrainOrder.Other"/>).</summary>
    Place,

    /// <summary>Taken out of the world (TRN-7).</summary>
    Contain,

    /// <summary>Coupled behind the vehicle <see cref="TrainOrder.Other"/>, or uncoupled where that is −1.</summary>
    Couple,

    /// <summary>Its pedals (<see cref="TrainOrder.Command"/>).</summary>
    Drive,

    /// <summary>The switch <see cref="TrainOrder.Wagon"/> set to <see cref="TrainOrder.Other"/> (TRN-9).</summary>
    Switch,

    /// <summary>Put on the collision channels <see cref="TrainOrder.Other"/> (RAIL-8).</summary>
    Channels,
}

/// <summary>
/// <b>One instruction from the trains to the world</b>, by vehicle slot: whoever composes the town turns it into the
/// world's own signal, so the trains know nothing of the world.
/// </summary>
internal readonly record struct TrainOrder(
    TrainOrderKind Kind, int Wagon, int Other, Vector2 AtM = default, float HeadingRad = 0f, float SpeedMps = 0f, DriveCommand Command = default);

/// <summary>What the world last left a rail vehicle as: where it is, which way it points and how it moves, and whether it is in the world.</summary>
internal readonly record struct RailVehicleSeen(Vector2 PositionM, Vector2 Forward, Vector2 VelocityMps, bool InWorld = true);

/// <summary>
/// <b>What a train is driven by on a tick</b> (TRN-10), whoever drives it — its own driver or a hand on it (CTL-5): a share
/// of its locomotive's traction, forward or back, its brake on every vehicle, and a step through the branches of the next
/// switch ahead.
/// </summary>
/// <param name="Pull">The share of the locomotive's tractive effort asked, nought to one.</param>
/// <param name="Reverse">Whether it pulls back along its leg rather than on.</param>
/// <param name="Brake">Every vehicle's wheels braked, sliding at the wheel-rail friction (TRN-8); no pull is asked under it.</param>
/// <param name="Branch">
/// −1 for the branch to the left of the one the next switch ahead is set for, +1 for the one to its right, nought for
/// neither: a step taken once as it is first asked, and not again until it has been let go (TRN-11).
/// </param>
internal readonly record struct TrainControls(float Pull, bool Reverse, bool Brake, int Branch = 0)
{
    public static TrainControls Coasting => default;
}

/// <summary>
/// <b>The town's trains</b> (TRN-3…12): each one's pool of vehicles, its wait off the map, the leg it runs across it, and
/// its driver.
/// </summary>
/// <remarks>
/// <para>
/// <b>A train is vehicles on their own wheels, coupled by the solver's link, and a dumb driver</b>: put on the run-in
/// past the map's edge moving at its pace (TRN-7), it is the world that runs it. The driver reads where its locomotive
/// is along its leg, either way, and drives it with the same controls a hand on it does (TRN-10): its own asks the pull
/// that holds its pace for the whole train's weight and never yields (TRN-12). Whoever drives it, each switch on its leg
/// is set ahead of its front (TRN-9), so a train backing retraces the leg it came by.
/// </para>
/// <para>
/// <b>A train a way onto the map</b>: each line has a train for every lane its tracks lead onto the map by, each with its
/// own pool of vehicles laid once (TRN-7) and its own clock (TRN-6). A train is sent only on a leg that shares no ground
/// with a leg still out (TRN-5), and waits a tick and draws again otherwise.
/// </para>
/// <para>
/// <b>Off the map it is a wait</b> (TRN-7): once its last vehicle has left, it is taken out of the world whole, and comes
/// back when its clock runs out by the way it left where a lane leads in there.
/// </para>
/// </remarks>
internal sealed class Trains
{
    /// <summary>The agent stream's own sub-stream a train's timetable draws on (AGT-6), each train keyed on its number as well.</summary>
    const ulong Stream = 0x5452_4E53;

    /// <summary>How many lane ends a vehicle's own reading of its leg walks on in a tick: more than a tick's travel ever crosses.</summary>
    const int MostLanesATick = 4;

    readonly SimConfig _config;
    readonly TrainCatalog _catalog;
    readonly RailGraph _graph;
    readonly Vector2 _worldSizeM;

    /// <summary>Each vehicle slot's kind, and how many slots a train's pool has and where each kind's begin in it.</summary>
    readonly VehicleKind[] _kindOf;

    readonly int _slotsPerTrain;
    readonly int[] _firstSlotOfKind;

    /// <summary>Each slot's look as it was last put down, and the channels it was last put on.</summary>
    readonly int[] _look;

    readonly byte[] _channels;

    /// <summary>Each train's line, and the lanes each line's tracks lead onto the map by.</summary>
    readonly int[] _lineOf;

    readonly int[][] _entriesOf;

    /// <summary>Each line's loose ends off the map: where a leg may end.</summary>
    readonly int[][] _portalsOf;

    readonly Rng[] _draw;
    readonly float[] _waitS;
    readonly bool[] _running;

    /// <summary>The node each train last left the map by, which it comes back by where it can; −1 before its first.</summary>
    readonly int[] _leftBy;

    /// <summary>Each train's vehicles from the front: its slot, and how far behind the train's front its own front is.</summary>
    readonly int[] _vehicles;

    readonly int[][] _slotOf;
    readonly float[][] _behindM;
    readonly float[] _lengthM, _massKg, _speedMps;

    /// <summary>Each train's pace as it is wanted this tick: its kind's, or slower for a curve under it or ahead (TRN-12).</summary>
    readonly float[] _wantedMps;

    /// <summary>Each segment's chords a curve slows a train on, by index, and the speed each is taken at (<see cref="CurveSpeeds"/>).</summary>
    readonly int[][] _curvedChords;

    readonly float[][] _curveMps;

    /// <summary>Each train's leg: its lanes, where along the leg each begins, and where the leg is on the map.</summary>
    readonly int[][] _leg;

    readonly float[][] _legStartM;
    readonly int[] _legCount;
    readonly float[] _entersM, _leavesM;

    /// <summary>Where each train's locomotive's front bogie was last read: its leg's lane and how far along that lane's segment.</summary>
    readonly int[] _atLane;

    readonly float[] _atSegmentM;

    /// <summary>Each train's front, in metres along its leg, as last read.</summary>
    readonly float[] _frontM;

    /// <summary>How fast each train's locomotive moves along its leg, back as negative, as last read.</summary>
    readonly float[] _alongMps;

    /// <summary>
    /// Each train's switches on its leg in order — the lane of the leg each leads onto, the switch, its position, and whether
    /// the leg faces it — and the next to set.
    /// </summary>
    readonly int[][] _switchLane, _switchOf, _switchPosition;

    readonly bool[][] _switchFacing;
    readonly int[] _switches, _nextSwitch;

    /// <summary>A second leg each train's is drawn again into where a switch ahead is thrown (TRN-11), and swapped for it where it is taken.</summary>
    readonly int[][] _spareLeg;

    /// <summary>Whether each train's vehicles were last told to brake; and whether a hand is on it, what it holds, and the branch step it last asked.</summary>
    readonly bool[] _braked, _handed;

    readonly TrainControls[] _hand;
    readonly int[] _branchWas;

    /// <summary>What each train was last driven by, whoever drove it.</summary>
    readonly TrainControls[] _given;

    readonly TrackRun[] _runs;
    int _runCount;

    /// <summary>The looks of the train being put down, front first: what <see cref="Compose"/> makes it of.</summary>
    readonly int[] _composing;

    /// <summary>Each train's looks, front first, where the town stands each look once (<see cref="StandEachLookOnce"/>); null where it sends them.</summary>
    readonly int[][]? _stood;

    /// <param name="standEachLookOnce">
    /// Whether the trains are stood rather than sent: the showroom's, every look of the rolling stock once
    /// (<see cref="StandEachLookOnce"/>).
    /// </param>
    public Trains(
        RailGraph graph, CityPlan.RailArrays rails, Vector2 worldSizeM, TrainCatalog catalog, SimConfig config, ulong agentSeed,
        bool standEachLookOnce = false)
    {
        (_graph, _config, _catalog, _worldSizeM) = (graph, config, catalog, worldSizeM);
        var lines = rails.Count == 0 ? 0 : rails.Line.Max() + 1;
        var entries = new List<int>[lines];
        var portals = new List<int>[lines];
        for (var line = 0; line < lines; line++) (entries[line], portals[line]) = ([], []);
        for (var lane = 0; lane < graph.LaneCount; lane++)
        {
            var line = rails.Line[graph.TrackOfSegment[graph.SegmentOfLane[lane]]];
            if (graph.IsPortal[graph.NodeBehind(lane)]) entries[line].Add(lane);
            var ahead = graph.NodeAhead(lane);
            if (graph.IsPortal[ahead] && !portals[line].Contains(ahead)) portals[line].Add(ahead);
        }

        _entriesOf = [.. entries.Select(list => list.ToArray())];
        _portalsOf = [.. portals.Select(list => list.ToArray())];
        var lineOf = new List<int>();
        for (var line = 0; line < lines; line++)
        {
            for (var entry = 0; entry < _entriesOf[line].Length; entry++) lineOf.Add(line);
        }

        _lineOf = [.. lineOf];
        var trains = _lineOf.Length;

        var figures = config.Train;
        var mostOf = new[] { 1, 1, figures.IntercityCoaches, figures.OilWagons, figures.CoalWagons };
        _firstSlotOfKind = new int[mostOf.Length];
        for (var kind = 1; kind < mostOf.Length; kind++) _firstSlotOfKind[kind] = _firstSlotOfKind[kind - 1] + mostOf[kind - 1];

        _slotsPerTrain = _firstSlotOfKind[^1] + mostOf[^1];
        var wagons = trains * _slotsPerTrain;
        (_kindOf, _look, _channels) = (new VehicleKind[wagons], new int[wagons], new byte[wagons]);
        for (var wagon = 0; wagon < wagons; wagon++)
        {
            var slot = wagon % _slotsPerTrain;
            var kind = mostOf.Length - 1;
            while (_firstSlotOfKind[kind] > slot) kind--;
            _kindOf[wagon] = (VehicleKind)kind;
            _channels[wagon] = CityPlan.RoadArrays.GroundChannel;
        }

        var longest = 1 + Math.Max(figures.IntercityCoaches, Math.Max(figures.OilWagons, figures.CoalWagons));
        (_draw, _waitS, _running, _leftBy, _vehicles) = (new Rng[trains], new float[trains], new bool[trains], new int[trains], new int[trains]);
        (_slotOf, _behindM) = (new int[trains][], new float[trains][]);
        (_lengthM, _massKg, _speedMps) = (new float[trains], new float[trains], new float[trains]);
        (_leg, _legStartM, _legCount, _entersM, _leavesM) = (new int[trains][], new float[trains][], new int[trains], new float[trains], new float[trains]);
        (_atLane, _atSegmentM, _frontM, _alongMps) = (new int[trains], new float[trains], new float[trains], new float[trains]);
        (_switchLane, _switchOf, _switchPosition, _switchFacing, _switches, _nextSwitch) =
            (new int[trains][], new int[trains][], new int[trains][], new bool[trains][], new int[trains], new int[trains]);
        (_spareLeg, _braked, _handed, _hand, _branchWas, _given) =
            (new int[trains][], new bool[trains], new bool[trains], new TrainControls[trains], new int[trains], new TrainControls[trains]);
        var lanes = Math.Max(1, graph.LaneCount);
        for (var train = 0; train < trains; train++)
        {
            _draw[train] = new Rng(agentSeed, Stream + (ulong)train);
            _waitS[train] = _draw[train].NextFloat(figures.FirstLeastS, figures.FirstMostS);
            _leftBy[train] = -1;
            (_slotOf[train], _behindM[train]) = (new int[longest], new float[longest]);
            (_leg[train], _spareLeg[train], _legStartM[train]) = (new int[lanes], new int[lanes], new float[lanes + 1]);
            (_switchLane[train], _switchOf[train], _switchPosition[train], _switchFacing[train]) =
                (new int[lanes], new int[lanes], new int[lanes], new bool[lanes]);
        }

        _composing = new int[longest];
        if (standEachLookOnce)
        {
            _stood = StandEachLookOnce(catalog, trains);
            Array.Clear(_waitS);
        }

        _runs = new TrackRun[Math.Max(1, trains * Math.Max(1, graph.LaneCount))];
        _wantedMps = new float[trains];
        (_curvedChords, _curveMps) = CurveSpeeds(
            graph, figures.CurveSideAccelerationMps2, MathF.Max(figures.IntercitySpeedMps, figures.FreightSpeedMps));
    }

    /// <summary>
    /// <b>Each segment's chords a curve slows a train on</b> (TRN-12): the speed at which the tighter of the radii at its two
    /// ends pulls a train sideways by its driver's most — and only those slower than the fastest pace, so a straight costs
    /// a driver nothing to read. A radius is a point's turn between its chords over their mean length.
    /// </summary>
    static (int[][] Chords, float[][] Mps) CurveSpeeds(RailGraph graph, float sideMps2, float fastestMps)
    {
        var (chords, speeds) = (new int[graph.SegmentCount][], new float[graph.SegmentCount][]);
        var (curved, mps) = (new List<int>(), new List<float>());
        for (var segment = 0; segment < graph.SegmentCount; segment++)
        {
            var line = graph.Segments[segment];
            var pointM = line.PointM;
            (curved, mps) = ([], []);
            for (var chord = 0; chord + 1 < pointM.Length; chord++)
            {
                var radiusM = MathF.Min(RadiusAtM(pointM, chord), RadiusAtM(pointM, chord + 1));
                var speedMps = MathF.Sqrt(sideMps2 * radiusM);
                if (speedMps >= fastestMps) continue;

                curved.Add(chord);
                mps.Add(speedMps);
            }

            (chords[segment], speeds[segment]) = ([.. curved], [.. mps]);
        }

        return (chords, speeds);
    }

    static float RadiusAtM(ReadOnlySpan<Vector2> pointM, int point)
    {
        if (point == 0 || point + 1 >= pointM.Length) return float.PositiveInfinity;

        var (inM, outM) = (pointM[point] - pointM[point - 1], pointM[point + 1] - pointM[point]);
        var turnRad = MathF.Abs(Trig.Atan2((inM.X * outM.Y) - (inM.Y * outM.X), Vector2.Dot(inM, outM)));
        return turnRad == 0f ? float.PositiveInfinity : (inM.Length() + outM.Length()) * 0.5f / turnRad;
    }

    /// <summary>
    /// <b>The fastest a train may be going now and still take every curve at its curve's speed</b> (TRN-12): every curve
    /// under it at that speed, and every one ahead within its whole brake's reach at what that brake leaves room to shed
    /// before it. Its brake is every wheel sliding (<see cref="RailWheels"/>), steel on steel.
    /// </summary>
    float CurveLimitMps(int train)
    {
        var frontM = _frontM[train];
        var tailM = frontM - _lengthM[train];
        var brakingMps2 = _config.Train.WheelRailFriction * _config.Tyre.StandardGravityMps2;
        var limitMps = _speedMps[train];
        var reachM = frontM + (limitMps * limitMps / (2f * brakingMps2));
        for (var lane = LaneAt(train, tailM); lane < _legCount[train] && _legStartM[train][lane] <= reachM; lane++)
        {
            var legLane = _leg[train][lane];
            var segment = _graph.SegmentOfLane[legLane];
            var line = _graph.Segments[segment];
            var forward = _graph.LaneForward[legLane];
            var startM = _legStartM[train][lane];
            var (chords, speeds) = (_curvedChords[segment], _curveMps[segment]);
            for (var at = 0; at < chords.Length; at++)
            {
                var (oneM, otherM) = (line.AtM[chords[at]], line.AtM[chords[at] + 1]);
                var (fromM, toM) = forward ? (startM + oneM, startM + otherM) : (startM + line.LengthM - otherM, startM + line.LengthM - oneM);
                if (toM < tailM || fromM > reachM) continue;

                var aheadM = MathF.Max(0f, fromM - frontM);
                limitMps = MathF.Min(limitMps, MathF.Sqrt((speeds[at] * speeds[at]) + (2f * brakingMps2 * aheadM)));
            }
        }

        return limitMps;
    }

    /// <summary>How many vehicle slots the town's trains have between them, each a body of the world (TRN-7).</summary>
    public int WagonCount => _kindOf.Length;

    /// <summary>
    /// <b>The most orders the trains give in a tick</b>, which <see cref="TrainOrders"/> is laid for: every vehicle put
    /// down, coupled and set rolling at once — more than a tick's brakes and channels, or a train taken out — and every
    /// switch of the railway set.
    /// </summary>
    public int MostOrdersATick => (WagonCount * OrdersAVehicleAtMost) + _graph.NodeOfSwitch.Length;

    /// <summary>A vehicle put down is placed, coupled and told to roll (<see cref="Send"/>).</summary>
    const int OrdersAVehicleAtMost = 3;

    /// <summary>The same, of a railway and the figures before any town stands on it — what a frame's room is laid for.</summary>
    public static int WagonsFor(RailGraph graph, TrainFigures figures)
    {
        var entries = 0;
        for (var lane = 0; lane < graph.LaneCount; lane++) entries += graph.IsPortal[graph.NodeBehind(lane)] ? 1 : 0;
        return entries * (2 + figures.IntercityCoaches + figures.OilWagons + figures.CoalWagons);
    }

    /// <summary>What a slot's vehicle is: the first look of its kind, which every look of the kind is the shape and weight of.</summary>
    public ref readonly TrainVehicle VehicleOf(int wagon) => ref _catalog.ShapeOf(_kindOf[wagon]);

    /// <summary>Which of the catalogue's vehicles a slot was last put down as.</summary>
    public int LookOf(int wagon) => _look[wagon];

    /// <summary>Whether its trains are stood, every look once, rather than sent (<see cref="StandEachLookOnce"/>): none has a driver of its own.</summary>
    public bool StandsEachLookOnce => _stood is not null;

    /// <summary>How many trains the town has, a lane onto the map each.</summary>
    public int Count => _running.Length;

    public bool Running(int train) => _running[train];

    /// <summary>A running train's vehicles, front first, as slots.</summary>
    public ReadOnlySpan<int> VehiclesOf(int train) => _slotOf[train].AsSpan(0, _running[train] ? _vehicles[train] : 0);

    /// <summary>A running train's leg, and where along it its front and its tail are.</summary>
    public ReadOnlySpan<int> LegOf(int train) => _leg[train].AsSpan(0, _legCount[train]);

    public float FrontM(int train) => _frontM[train];

    public float LengthM(int train) => _lengthM[train];

    /// <summary>The pace a train's own driver holds it at on a straight (TRN-12): a train of its kind's.</summary>
    public float PaceMps(int train) => _speedMps[train];

    /// <summary>The pace as it is wanted this tick (TRN-12): <see cref="PaceMps"/>, or slower for a curve under the train or ahead of it.</summary>
    public float WantedMps(int train) => _wantedMps[train];

    /// <summary>How fast a train's locomotive moves along its leg, back as negative, as last read.</summary>
    public float AlongMps(int train) => _alongMps[train];

    /// <summary>What a train was last driven by, whoever drove it (TRN-10).</summary>
    public TrainControls Given(int train) => _given[train];

    /// <summary>A running train's whole weight, every vehicle's.</summary>
    public float MassKg(int train) => _massKg[train];

    /// <summary>How long a train's leg is, end to end — where its front is when it is all the way along.</summary>
    public float LegLengthM(int train) => _legStartM[train][_legCount[train]];

    /// <summary>The catalogue's name for a slot's look as it was last put down.</summary>
    public string LookIdOf(int wagon) => _catalog.Vehicles[_look[wagon]].Id;

    /// <summary>The train a vehicle slot belongs to, running or not.</summary>
    public int TrainOfWagon(int wagon) => wagon / _slotsPerTrain;

    /// <summary>Whether a hand is on a train this tick (CTL-5).</summary>
    public bool Handed(int train) => _handed[train];

    /// <summary>
    /// <b>A hand on a train for the ticks to come</b> (CTL-5, TRN-10): it is driven by <paramref name="controls"/> in place
    /// of its own driver's, held or not, until it is let go of.
    /// </summary>
    public void Hand(int train, in TrainControls controls) => (_handed[train], _hand[train]) = (true, controls);

    /// <summary>The hand taken off a train: its own driver drives it again from wherever the hand left it.</summary>
    public void LetGo(int train) => (_handed[train], _hand[train]) = (false, default);

    /// <summary>
    /// <b>The next switch a train faces ahead of its front</b> within the set-ahead distance — the one a branch step throws
    /// (TRN-11) — as its index among the train's switches, or −1; and how far ahead of its front it stands.
    /// </summary>
    public int NextSwitchAhead(int train, out float aheadM)
    {
        aheadM = float.PositiveInfinity;
        if (!_running[train]) return -1;

        for (var at = 0; at < _switches[train]; at++)
        {
            var atM = _legStartM[train][_switchLane[train][at]];
            if (!_switchFacing[train][at] || atM <= _frontM[train]) continue;
            if (atM - _frontM[train] > _config.Rail.SwitchSetAheadM) return -1;

            aheadM = atM - _frontM[train];
            return at;
        }

        return -1;
    }

    /// <summary>The switch a train's <paramref name="at"/>-th is, the position its leg runs it at, and how many it has.</summary>
    public (int Switch, int Position, int Positions) SwitchOnTheLeg(int train, int at)
    {
        var node = _graph.NodeOfSwitch[_switchOf[train][at]];
        return (_switchOf[train][at], _switchPosition[train][at], _graph.EndOffsets[node + 1] - _graph.EndOffsets[node] - 1);
    }

    /// <summary>Where along a train's leg each of its lanes begins.</summary>
    public float LegStartM(int train, int legLane) => _legStartM[train][legLane];

    /// <summary>Every train out, where it is on each track it is on or about to be, for the crossings (RAIL-1).</summary>
    public ReadOnlySpan<TrackRun> Runs => _runs.AsSpan(0, _runCount);

    /// <summary>How many trains have been sent, all told: an instrument's reading.</summary>
    public int Sent { get; private set; }

    /// <summary>How many trains have waited a tick for a leg clear of the others (TRN-5): an instrument's reading.</summary>
    public int WaitedForGround { get; private set; }

    /// <summary>
    /// <b>One tick of every train</b>: a waiting train's clock run down and, where its leg is clear, its vehicles put on its
    /// run-in; a running one driven off where the world left its vehicles; and one whose last vehicle has left the map
    /// taken out of the world. What each asks of the world is written into <paramref name="orders"/>.
    /// </summary>
    /// <param name="seen">Each slot as the world last left it.</param>
    /// <param name="held">Whether the town holds its agents: nothing is sent and nothing driven but a train under a hand.</param>
    public void Advance(ReadOnlySpan<RailVehicleSeen> seen, float dtS, bool held, TrainOrders orders)
    {
        _runCount = 0;
        for (var train = 0; train < _running.Length; train++)
        {
            if (!_running[train])
            {
                // A stood train is put down however the town is held, and put back once a hand has driven it off the map.
                if (_stood is null ? held : _stood[train].Length == 0) continue;

                _waitS[train] -= dtS;
                if (_waitS[train] > 0f) continue;

                if (_stood is null) Send(train, orders);
                else Stand(train, orders);

                if (!_running[train]) continue;
            }

            if (!held || _handed[train]) Drive(train, seen, dtS, orders);
            if (_running[train]) Run(train);
        }
    }

    /// <summary>
    /// <b>Every look of the rolling stock once, as trains</b> (the showroom's, <c>ShowroomPlan</c>), front first, one a
    /// locomotive look in the catalogue's order: each heads a train of one kind (TRN-4) — an electric coaches, a diesel tanks
    /// or coal, the diesels taking the two in turn — and each wagon look stands once behind the locomotives of its kind,
    /// dealt round them. A locomotive left with none of its kind's looks to haul takes one of them again; a train past
    /// <paramref name="trains"/> stands nowhere, and one past the locomotives stands empty.
    /// </summary>
    internal static int[][] StandEachLookOnce(TrainCatalog catalog, int trains)
    {
        (VehicleKind Lead, VehicleKind Wagon)[] kinds =
            [(VehicleKind.Electric, VehicleKind.Coach), (VehicleKind.Diesel, VehicleKind.Tank), (VehicleKind.Diesel, VehicleKind.Coal)];
        var heads = new List<(int Look, VehicleKind Wagon)>();
        foreach (var lead in (ReadOnlySpan<VehicleKind>)[VehicleKind.Electric, VehicleKind.Diesel])
        {
            var hauls = kinds.Where(kind => kind.Lead == lead).Select(kind => kind.Wagon).ToArray();
            var locomotives = catalog.OfKind(lead);
            for (var each = 0; each < locomotives.Length; each++) heads.Add((locomotives[each], hauls[each % hauls.Length]));
        }

        var stood = new int[trains][];
        for (var train = 0; train < trains; train++)
        {
            if (train >= heads.Count)
            {
                stood[train] = [];
                continue;
            }

            var (look, wagon) = heads[train];
            var sameKind = heads.Where(head => head.Wagon == wagon).ToList();
            var at = sameKind.IndexOf(heads[train]);
            var wagons = catalog.OfKind(wagon).ToArray();
            var dealt = Enumerable.Range(0, wagons.Length).Where(each => each % sameKind.Count == at).Select(each => wagons[each]).ToList();
            if (dealt.Count == 0) dealt.Add(wagons[at % wagons.Length]);
            stood[train] = [look, .. dealt];
        }

        return stood;
    }

    /// <summary>
    /// <b>A train sent</b>, where its leg is clear (TRN-5, TRN-6): its kind and its leg drawn — in by the way it last left
    /// where a lane leads in there — and its vehicles put on the run-in moving at its line's speed and coupled, its front a
    /// warning's run short of where the leg comes onto the map, so every crossing there closes ahead of it (RAIL-1).
    /// </summary>
    void Send(int train, TrainOrders orders)
    {
        ref var draw = ref _draw[train];
        if (!DrawALeg(train, ref draw))
        {
            WaitedForGround++;
            return;
        }

        var figures = _config.Train;
        var share = draw.NextFloat();
        var kind = share < figures.IntercityShare ? TrainKind.Intercity : share < figures.IntercityShare + figures.OilShare ? TrainKind.Oil : TrainKind.Coal;
        var (lead, wagon, wagons) = kind switch
        {
            TrainKind.Intercity => (VehicleKind.Electric, VehicleKind.Coach, figures.IntercityCoaches),
            TrainKind.Oil => (VehicleKind.Diesel, VehicleKind.Tank, figures.OilWagons),
            _ => (VehicleKind.Diesel, VehicleKind.Coal, figures.CoalWagons),
        };

        _vehicles[train] = 1 + wagons;
        for (var vehicle = 0; vehicle < _vehicles[train]; vehicle++)
        {
            var looks = _catalog.OfKind(vehicle == 0 ? lead : wagon);
            _composing[vehicle] = looks[draw.NextInt(looks.Length)];
        }

        Compose(train, kind);
        var frontM = MathF.Max(_lengthM[train], _entersM[train] - (_speedMps[train] * _config.Rail.WarningS));
        PutDown(train, frontM, _speedMps[train], orders);
        Sent++;
    }

    /// <summary>
    /// <b>A showroom's train stood</b> (<see cref="StandEachLookOnce"/>): its looks put down front first on its siding,
    /// coupled and braked, its middle on the middle of the map's stretch of its leg, to stand until a hand drives it.
    /// </summary>
    void Stand(int train, TrainOrders orders)
    {
        ref var draw = ref _draw[train];
        if (!DrawALeg(train, ref draw))
        {
            WaitedForGround++;
            return;
        }

        var looks = _stood![train];
        _vehicles[train] = looks.Length;
        looks.CopyTo(_composing, 0);
        Compose(train, _catalog.Vehicles[looks[^1]].Kind switch
        {
            VehicleKind.Coach or VehicleKind.Electric => TrainKind.Intercity,
            VehicleKind.Tank => TrainKind.Oil,
            _ => TrainKind.Coal,
        });
        PutDown(train, ((_entersM[train] + _leavesM[train]) * 0.5f) + (_lengthM[train] * 0.5f), 0f, orders);
    }

    /// <summary>
    /// A train made of the looks in <see cref="_composing"/>, front first — its locomotive's and then its wagons', each
    /// in a slot of its kind — and its length, its weight and its kind's pace.
    /// </summary>
    void Compose(int train, TrainKind kind)
    {
        var behindM = 0f;
        var massKg = 0f;
        for (var vehicle = 0; vehicle < _vehicles[train]; vehicle++)
        {
            var vehicleKind = _catalog.Vehicles[_composing[vehicle]].Kind;
            var slot = (train * _slotsPerTrain) + _firstSlotOfKind[(int)vehicleKind] + (vehicle == 0 ? 0 : vehicle - 1);
            _look[slot] = _composing[vehicle];
            (_slotOf[train][vehicle], _behindM[train][vehicle]) = (slot, behindM);
            behindM += _catalog.ShapeOf(vehicleKind).FootprintM.X;
            massKg += _catalog.ShapeOf(vehicleKind).MassKg;
        }

        var figures = _config.Train;
        _lengthM[train] = behindM;
        _massKg[train] = massKg;
        _speedMps[train] = kind == TrainKind.Intercity ? figures.IntercitySpeedMps : figures.FreightSpeedMps;
        _wantedMps[train] = _speedMps[train];
    }

    /// <summary>
    /// A composed train's vehicles put on its leg with its front <paramref name="frontM"/> along it, moving at
    /// <paramref name="alongMps"/> and coupled — rolling free, or braked where it is put down standing.
    /// </summary>
    void PutDown(int train, float frontM, float alongMps, TrainOrders orders)
    {
        var braked = alongMps == 0f;
        for (var vehicle = 0; vehicle < _vehicles[train]; vehicle++)
        {
            var slot = _slotOf[train][vehicle];
            ref readonly var shape = ref _catalog.ShapeOf(_kindOf[slot]);
            var middleM = frontM - _behindM[train][vehicle] - (shape.FootprintM.X * 0.5f);
            var frontBogieM = PlaceOnTheLeg(train, middleM + shape.BogiesM[0]);
            var rearBogieM = PlaceOnTheLeg(train, middleM + shape.BogiesM[^1]);
            var along = frontBogieM - rearBogieM;
            orders.Add(new TrainOrder(
                TrainOrderKind.Place, slot, _look[slot], (frontBogieM + rearBogieM) * 0.5f, Trig.Atan2(along.Y, along.X), alongMps));
            orders.Add(new TrainOrder(TrainOrderKind.Couple, slot, vehicle == 0 ? -1 : _slotOf[train][vehicle - 1]));
            orders.Add(new TrainOrder(TrainOrderKind.Drive, slot, 0, Command: braked ? Braked : Rolling));
            _channels[slot] = CityPlan.RoadArrays.GroundChannel;
        }

        ref readonly var locomotive = ref _catalog.ShapeOf(_kindOf[_slotOf[train][0]]);
        var frontBogieLegM = frontM - (locomotive.FootprintM.X * 0.5f) + locomotive.BogiesM[0];
        (_atLane[train], _atSegmentM[train], _frontM[train]) = (LaneAt(train, frontBogieLegM), float.NaN, frontM);
        (_alongMps[train], _braked[train], _branchWas[train]) = (alongMps, braked, 0);
        _nextSwitch[train] = 0;
        _running[train] = true;
    }

    /// <summary>A vehicle rolling free, neither driven nor braked: what every vehicle of a train is told as it is put down.</summary>
    static DriveCommand Rolling => new(0f, 0f, 0f, false, false);

    /// <summary>A vehicle's wheels braked (TRN-8): a rail wheel's brake is on or off, so it is said as the lock.</summary>
    static DriveCommand Braked => new(0f, 0f, 0f, true, false);

    /// <summary>A train held where it stands: no pull and the brake on every vehicle.</summary>
    static TrainControls Standing => new(0f, Reverse: false, Brake: true);

    /// <summary>
    /// <b>A leg drawn for a train</b>: in by the way it last left where a lane leads in there and otherwise by any of its
    /// line's, and on to any other loose end of its line it can be routed to — refused where it shares ground with a
    /// leg still out (TRN-5).
    /// </summary>
    bool DrawALeg(int train, ref Rng draw)
    {
        var line = _lineOf[train];
        var entries = _entriesOf[line];
        var entry = entries[draw.NextInt(entries.Length)];
        foreach (var lane in entries)
        {
            if (_graph.NodeBehind(lane) == _leftBy[train]) entry = lane;
        }

        var portals = _portalsOf[line];
        var first = draw.NextInt(portals.Length);
        var count = 0;
        for (var tried = 0; tried < portals.Length && count == 0; tried++)
        {
            var portal = portals[(first + tried) % portals.Length];
            if (portal != _graph.NodeBehind(entry)) count = _graph.Route(entry, portal, _leg[train]);
        }

        if (count == 0 || SharesGroundWithALegOut(train, _leg[train].AsSpan(0, count))) return false;

        TakeTheLeg(train, count);
        return true;
    }

    /// <summary>
    /// A train's leg, its first <paramref name="count"/> lanes, taken as drawn: where along it each lane begins, where it is
    /// on the map, and every switch on it with the position it is run at — facing or trailing, so a train backing over one
    /// runs back the way it came (TRN-9).
    /// </summary>
    void TakeTheLeg(int train, int count)
    {
        _legCount[train] = count;
        var leg = _leg[train];
        var startM = _legStartM[train];
        startM[0] = 0f;
        for (var lane = 0; lane < count; lane++) startM[lane + 1] = startM[lane] + _graph.LengthM(leg[lane]);
        (_entersM[train], _leavesM[train]) = OnTheMap(train);

        var switches = 0;
        for (var lane = 1; lane < count; lane++)
        {
            var position = _graph.PositionThrough(leg[lane - 1], leg[lane], out var facing);
            if (position < 0) continue;

            var node = _graph.NodeAhead(leg[lane - 1]);
            (_switchLane[train][switches], _switchOf[train][switches], _switchPosition[train][switches], _switchFacing[train][switches]) =
                (lane, _graph.SwitchOfNode[node], position, facing);
            switches++;
        }

        _switches[train] = switches;
    }

    bool SharesGroundWithALegOut(int train, ReadOnlySpan<int> leg)
    {
        for (var other = 0; other < _running.Length; other++)
        {
            if (other == train || !_running[other]) continue;

            foreach (var lane in leg)
            {
                foreach (var theirs in LegOf(other))
                {
                    if (_graph.ShareGround(_graph.SegmentOfLane[lane], _graph.SegmentOfLane[theirs])) return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// <b>A train driven</b> (TRN-9, TRN-10): its front read off where the world left its locomotive, either way along its
    /// leg; its controls taken from the hand on it or from its own driver and given to its vehicles; the switches ahead set,
    /// each vehicle put on a bridge's channel where any of it is over one — and the train taken out of the world once it
    /// has left the map by either end of its leg (TRN-7).
    /// </summary>
    void Drive(int train, ReadOnlySpan<RailVehicleSeen> seen, float dtS, TrainOrders orders)
    {
        var loco = _slotOf[train][0];
        ref readonly var locomotive = ref _catalog.ShapeOf(_kindOf[loco]);
        var at = seen[loco];

        // Put down this tick, it is not in the world the view shows yet: the world reads its signals a step behind.
        if (!at.InWorld) return;

        var bogieM = ReadAlongTheLeg(train, at.PositionM + (at.Forward * locomotive.BogiesM[0]));
        var frontM = bogieM + (locomotive.FootprintM.X * 0.5f) - locomotive.BogiesM[0];
        var alongMps = Vector2.Dot(at.VelocityMps, at.Forward);
        (_frontM[train], _alongMps[train]) = (frontM, alongMps);

        var backedOff = alongMps < 0f && (frontM < _entersM[train] || frontM - _lengthM[train] <= 0f);
        if (frontM - _lengthM[train] > _leavesM[train] || backedOff)
        {
            Leave(train, orders, backedOff);
            return;
        }

        _wantedMps[train] = MathF.Min(_speedMps[train], CurveLimitMps(train));

        // A stood train has no driver of its own: let go, it brakes to a stand where it was left, as the showroom's cars do.
        var controls = _handed[train] ? _hand[train] : _stood is not null ? Standing : OwnDriver(train, locomotive, alongMps, dtS);
        Give(train, locomotive, controls, orders);
        if (controls.Branch != 0 && _branchWas[train] == 0) ThrowTheSwitchAhead(train, controls.Branch);
        _branchWas[train] = controls.Branch;

        var aheadM = _config.Rail.SwitchSetAheadM;
        while (_nextSwitch[train] < _switches[train]
               && frontM >= _legStartM[train][_switchLane[train][_nextSwitch[train]]] - aheadM)
        {
            var next = _nextSwitch[train]++;
            orders.Add(new TrainOrder(TrainOrderKind.Switch, _switchOf[train][next], _switchPosition[train][next]));
        }

        for (var vehicle = 0; vehicle < _vehicles[train]; vehicle++)
        {
            var slot = _slotOf[train][vehicle];
            var halfM = _catalog.ShapeOf(_kindOf[slot]).FootprintM.X * 0.5f;
            var middleM = frontM - _behindM[train][vehicle] - halfM;
            var channels = Bridged(train, middleM - halfM, middleM + halfM)
                ? CityPlan.RailArrays.BridgeChannel
                : CityPlan.RoadArrays.GroundChannel;
            if (channels == _channels[slot]) continue;

            _channels[slot] = channels;
            orders.Add(new TrainOrder(TrainOrderKind.Channels, slot, channels));
        }
    }

    /// <summary>
    /// <b>A train's own driver</b> (TRN-12): the pull that brings it to the pace it wants on the next tick for the whole
    /// train's weight; the brake where it is over that pace by more than a tick of braking sheds, coming to a curve; and the
    /// brake where it is rolling back along its leg — a hand let go of it backing, say. It never yields.
    /// </summary>
    TrainControls OwnDriver(int train, in TrainVehicle locomotive, float alongMps, float dtS)
    {
        if (alongMps < -_config.Driving.StopSpeedMps) return new TrainControls(0f, Reverse: false, Brake: true);

        var aTickOfBrakingMps = _config.Train.WheelRailFriction * _config.Tyre.StandardGravityMps2 * dtS;
        if (alongMps > _wantedMps[train] + aTickOfBrakingMps) return new TrainControls(0f, Reverse: false, Brake: true);

        var wantedMps2 = MathF.Max(0f, (_wantedMps[train] - alongMps) / dtS);
        return new TrainControls(MathF.Min(1f, wantedMps2 * _massKg[train] / locomotive.TractiveEffortN), Reverse: false, Brake: false);
    }

    /// <summary>
    /// <b>A train's controls given to its vehicles</b> (TRN-10): the pull to its locomotive as an acceleration of its own
    /// mass, which its wheels hold to its tractive effort (<see cref="RailWheels"/>); and the brake to every vehicle, told
    /// only as it changes, since the world keeps a command until the next.
    /// </summary>
    void Give(int train, in TrainVehicle locomotive, in TrainControls controls, TrainOrders orders)
    {
        _given[train] = controls;
        var pullN = controls.Brake ? 0f : Math.Clamp(controls.Pull, 0f, 1f) * locomotive.TractiveEffortN;
        var loco = _slotOf[train][0];
        orders.Add(new TrainOrder(
            TrainOrderKind.Drive, loco, 0,
            Command: controls.Brake ? Braked : new DriveCommand(0f, pullN / locomotive.MassKg, 0f, false, controls.Reverse)));
        if (controls.Brake == _braked[train]) return;

        _braked[train] = controls.Brake;
        for (var vehicle = 1; vehicle < _vehicles[train]; vehicle++)
        {
            orders.Add(new TrainOrder(TrainOrderKind.Drive, _slotOf[train][vehicle], 0, Command: controls.Brake ? Braked : Rolling));
        }
    }

    /// <summary>
    /// <b>The next switch ahead thrown to the branch beside the one it is set for</b> (TRN-11), on the side asked: the leg
    /// drawn again from that branch to any loose end of the railway it can be routed to, and taken only where it shares no
    /// ground with a leg still out (TRN-5) — otherwise the switch stays as it is. The switch is set by the driver as any
    /// on its leg is, this tick, since it is within the set-ahead distance.
    /// </summary>
    void ThrowTheSwitchAhead(int train, int side)
    {
        var at = NextSwitchAhead(train, out _);
        if (at < 0) return;

        var lane = _switchLane[train][at];
        var leg = _leg[train];
        Span<(int Lane, int Switch, int Position)> next = stackalloc (int, int, int)[8];
        var options = _graph.NextLanes(leg[lane - 1], next);
        var (onto, position) = (-1, side > 0 ? int.MaxValue : int.MinValue);
        for (var option = 0; option < options; option++)
        {
            var p = next[option].Position;
            var beside = side > 0 ? p > _switchPosition[train][at] && p < position : p < _switchPosition[train][at] && p > position;
            if (beside) (onto, position) = (next[option].Lane, p);
        }

        if (onto < 0) return;

        var spare = _spareLeg[train];
        leg.AsSpan(0, lane).CopyTo(spare);
        var routed = 0;
        foreach (var portal in _portalsOf[_lineOf[train]])
        {
            routed = _graph.Route(onto, portal, spare.AsSpan(lane));
            if (routed > 0) break;
        }

        if (routed == 0 || SharesGroundWithALegOut(train, spare.AsSpan(0, lane + routed))) return;

        (_leg[train], _spareLeg[train]) = (spare, leg);
        TakeTheLeg(train, lane + routed);
        _nextSwitch[train] = at;
    }

    /// <summary>A train taken out of the world whole, uncoupled, and set waiting for its round trip off the map (TRN-7).</summary>
    /// <param name="backedOff">Whether it left by the end of its leg it came in by, backing.</param>
    void Leave(int train, TrainOrders orders, bool backedOff)
    {
        foreach (var slot in VehiclesOf(train))
        {
            orders.Add(new TrainOrder(TrainOrderKind.Couple, slot, -1));
            orders.Add(new TrainOrder(TrainOrderKind.Contain, slot, 0));
        }

        _leftBy[train] = backedOff ? _graph.NodeBehind(_leg[train][0]) : _graph.NodeAhead(_leg[train][_legCount[train] - 1]);
        _running[train] = false;
        _waitS[train] = _draw[train].NextFloat(_config.Train.GapLeastS, _config.Train.GapMostS);
    }

    /// <summary>
    /// <b>How far along its leg a point stands</b>, read from where the train's front bogie was last read on, walking on
    /// into the next lane of the leg past the end of one and back into the one before short of its start — a car's
    /// progress along its line, said of a train that may back. Off either end of the leg it is read on along the end's line.
    /// </summary>
    float ReadAlongTheLeg(int train, Vector2 pointM)
    {
        var leg = _leg[train];
        for (var step = 0; step <= MostLanesATick; step++)
        {
            var lane = leg[_atLane[train]];
            var forward = _graph.LaneForward[lane];
            var line = _graph.Segments[_graph.SegmentOfLane[lane]];
            var nearM = float.IsNaN(_atSegmentM[train]) ? line.NearestAlongM(pointM) : _atSegmentM[train];
            var segmentM = line.NearestAlongFromM(pointM, nearM, out var beyondM);
            _atSegmentM[train] = segmentM;
            var laneM = forward ? segmentM : line.LengthM - segmentM;
            var pastM = forward ? beyondM : -beyondM;
            var on = pastM > 0f ? _atLane[train] + 1 : pastM < 0f ? _atLane[train] - 1 : -1;
            if (on < 0 || on >= _legCount[train]) return _legStartM[train][_atLane[train]] + laneM + pastM;

            _atLane[train] = on;
            var onto = leg[on];
            _atSegmentM[train] = _graph.LaneForward[onto] == (pastM > 0f) ? 0f : _graph.LengthM(onto);
        }

        return _legStartM[train][_atLane[train]];
    }

    /// <summary>The place a distance along a train's leg stands at, held to the leg's two ends.</summary>
    Vector2 PlaceOnTheLeg(int train, float legM)
    {
        var lane = LaneAt(train, legM);
        var laneLane = _leg[train][lane];
        var line = _graph.Segments[_graph.SegmentOfLane[laneLane]];
        var laneM = Math.Clamp(legM - _legStartM[train][lane], 0f, line.LengthM);
        return line.PlaceAt(_graph.LaneForward[laneLane] ? laneM : line.LengthM - laneM);
    }

    /// <summary>
    /// <b>Where every running train means to go next</b> (OBS-2h): the rest of the rail lane its front is on and the lane
    /// after it on its leg, each a way of the town's one numbering (<see cref="TownWays.OfRail"/>).
    /// </summary>
    public void StateIntents(WayIntents into, TownWays ways)
    {
        for (var train = 0; train < Count; train++)
        {
            if (!_running[train]) continue;

            var leg = LegOf(train);
            var front = LaneAt(train, _frontM[train]);
            for (var lane = front; lane < leg.Length && lane < front + PiecesStated; lane++)
            {
                var lengthM = _graph.Segments[_graph.SegmentOfLane[leg[lane]]].LengthM;
                var fromM = lane == front ? Math.Clamp(_frontM[train] - _legStartM[train][lane], 0f, lengthM) : 0f;
                into.Add(train, LaneRoster.Rail, ways.OfRail(leg[lane]), fromM, lengthM);
            }
        }
    }

    /// <summary>The pieces a train states: the lane its front is on and the one after it (OBS-2h).</summary>
    const int PiecesStated = 2;

    /// <summary>Which lane of its leg a distance along it falls in.</summary>
    int LaneAt(int train, float legM)
    {
        var lane = 0;
        while (lane + 1 < _legCount[train] && legM >= _legStartM[train][lane + 1]) lane++;
        return lane;
    }

    /// <summary>Whether any of a train's leg between two distances along it stands on a bridge (RAIL-8).</summary>
    bool Bridged(int train, float fromM, float toM)
    {
        for (var lane = LaneAt(train, fromM); lane <= LaneAt(train, toM); lane++)
        {
            if (_graph.LevelOf(_graph.SegmentOfLane[_leg[train][lane]]) != CityGen.Map.TownMap.RailArrays.AtGrade) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>Where a train is on each track its leg runs</b>, from a warning's run behind its tail to a warning's run ahead of its
    /// front — whichever way it is going — for the crossings (RAIL-1): its front as a distance along that track, the way the
    /// leg runs it, and how it moves along it.
    /// </summary>
    void Run(int train)
    {
        var frontM = _frontM[train];
        var rail = _config.Rail;
        var onM = Reach(train, rail.WarningS + rail.ClearS, out var backM);
        var (speedMps, mostMps2) = ClosesAt(train);
        var (firstRun, from, to) = (_runCount, LaneAt(train, frontM - _lengthM[train] - backM), LaneAt(train, frontM + onM));
        for (var lane = from; lane <= to; lane++)
        {
            var legLane = _leg[train][lane];
            var segment = _graph.SegmentOfLane[legLane];
            var forward = _graph.LaneForward[legLane];
            var startM = _graph.SegmentFromM[segment] + (forward ? 0f : _graph.Segments[segment].LengthM);
            var trackFrontM = startM + ((forward ? 1f : -1f) * (frontM - _legStartM[train][lane]));
            var run = new TrackRun(_graph.TrackOfSegment[segment], trackFrontM, _lengthM[train], forward, speedMps, mostMps2);
            var known = false;
            for (var other = firstRun; other < _runCount; other++) known |= _runs[other].Track == run.Track && _runs[other].Forward == forward;
            if (!known) _runs[_runCount++] = run;
        }
    }

    /// <summary>
    /// <b>How far a train can be on its way along its leg</b> in <paramref name="seconds"/>, ahead of its front, and
    /// <paramref name="backM"/> behind its tail — what its claim and the crossings are laid ahead of it by (RAIL-1, RAIL-9).
    /// </summary>
    public float Reach(int train, float seconds, out float backM)
    {
        var (speedMps, mostMps2) = ClosesAt(train);
        backM = TrackRun.ReachM(-speedMps, mostMps2, seconds);
        return TrackRun.ReachM(speedMps, mostMps2, seconds);
    }

    /// <summary>
    /// <b>The speed and pull a train is reached ahead by</b> (RAIL-1, TRN-12): under a hand, as it moves and pulled either
    /// way by the whole of its traction, since a hand may do that at any moment; under its own driver, at its pace, which it
    /// is never past and is always bringing it back to — and not at its pull, which flickers to the whole of its traction
    /// for a tick at a time holding it there.
    /// </summary>
    (float SpeedMps, float MostMps2) ClosesAt(int train) => _handed[train]
        ? (_alongMps[train], _catalog.ShapeOf(_kindOf[_slotOf[train][0]]).TractiveEffortN / _massKg[train])
        : (_alongMps[train] < 0f ? _alongMps[train] : MathF.Max(_alongMps[train], _speedMps[train]), 0f);

    /// <summary>Where a train's leg first comes onto the map and last goes off it, in metres along it.</summary>
    (float EntersM, float LeavesM) OnTheMap(int train)
    {
        var (firstM, lastM) = (float.PositiveInfinity, float.NegativeInfinity);
        for (var lane = 0; lane < _legCount[train]; lane++)
        {
            var legLane = _leg[train][lane];
            var line = _graph.Segments[_graph.SegmentOfLane[legLane]];
            var forward = _graph.LaneForward[legLane];
            for (var chord = 0; chord + 1 < line.PointM.Length; chord++)
            {
                if (!MapCut.Shares(line.PointM[chord], line.PointM[chord + 1], _worldSizeM, out var enters, out var leaves)) continue;

                var (fromM, lengthM) = (line.AtM[chord], line.AtM[chord + 1] - line.AtM[chord]);
                foreach (var share in (ReadOnlySpan<float>)[enters, leaves])
                {
                    var segmentM = fromM + (share * lengthM);
                    var legM = _legStartM[train][lane] + (forward ? segmentM : line.LengthM - segmentM);
                    (firstM, lastM) = (MathF.Min(firstM, legM), MathF.Max(lastM, legM));
                }
            }
        }

        return float.IsPositiveInfinity(firstM) ? (0f, 0f) : (firstM, lastM);
    }
}

/// <summary>A tick's train orders, written in the order they were given; laid with room to spare so a tick never grows it.</summary>
internal sealed class TrainOrders(int capacity)
{
    TrainOrder[] _orders = new TrainOrder[Math.Max(1, capacity)];

    public int Count { get; private set; }

    public ReadOnlySpan<TrainOrder> Written => _orders.AsSpan(0, Count);

    public void Add(in TrainOrder order)
    {
        if (Count == _orders.Length) Array.Resize(ref _orders, _orders.Length * 2);

        _orders[Count++] = order;
    }

    public void Clear() => Count = 0;
}
