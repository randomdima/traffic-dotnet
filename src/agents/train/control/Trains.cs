using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Rail;

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
/// <b>The town's trains</b> (TRN-1…9): each one's pool of vehicles, its wait off the map, the leg it runs across it, and
/// its driver.
/// </summary>
/// <remarks>
/// <para>
/// <b>A train is vehicles on their own wheels, coupled by the solver's link, and a dumb driver</b>: put on the run-in
/// past the map's edge moving at its line's speed (TRN-7), it is the world that runs it. The driver reads where its
/// locomotive is along its leg, asks the throttle that holds its line's speed for the whole train's weight (TRN-2), sets
/// each switch on its leg ahead of its front (TRN-9), and never brakes, steers or yields.
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

    /// <summary>Each train's facing switches on its leg in order — the lane of the leg each leads onto, the switch and its position — and the next to set.</summary>
    readonly int[][] _switchLane, _switchOf, _switchPosition;

    readonly int[] _switches, _nextSwitch;

    readonly TrackRun[] _runs;
    int _runCount;

    public Trains(RailGraph graph, CityPlan.RailArrays rails, Vector2 worldSizeM, TrainCatalog catalog, SimConfig config, ulong agentSeed)
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
        (_atLane, _atSegmentM, _frontM) = (new int[trains], new float[trains], new float[trains]);
        (_switchLane, _switchOf, _switchPosition, _switches, _nextSwitch) =
            (new int[trains][], new int[trains][], new int[trains][], new int[trains], new int[trains]);
        for (var train = 0; train < trains; train++)
        {
            _draw[train] = new Rng(agentSeed, Stream + (ulong)train);
            _waitS[train] = _draw[train].NextFloat(figures.FirstLeastS, figures.FirstMostS);
            _leftBy[train] = -1;
            (_slotOf[train], _behindM[train]) = (new int[longest], new float[longest]);
            (_leg[train], _legStartM[train]) = (new int[Math.Max(1, graph.LaneCount)], new float[Math.Max(1, graph.LaneCount) + 1]);
            (_switchLane[train], _switchOf[train], _switchPosition[train]) =
                (new int[Math.Max(1, graph.LaneCount)], new int[Math.Max(1, graph.LaneCount)], new int[Math.Max(1, graph.LaneCount)]);
        }

        _runs = new TrackRun[Math.Max(1, trains * Math.Max(1, graph.LaneCount))];
    }

    /// <summary>How many vehicle slots the town's trains have between them, each a body of the world (TRN-7).</summary>
    public int WagonCount => _kindOf.Length;

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

    /// <summary>How many trains the town has, a lane onto the map each.</summary>
    public int Count => _running.Length;

    public bool Running(int train) => _running[train];

    /// <summary>A running train's vehicles, front first, as slots.</summary>
    public ReadOnlySpan<int> VehiclesOf(int train) => _slotOf[train].AsSpan(0, _running[train] ? _vehicles[train] : 0);

    /// <summary>A running train's leg, and where along it its front and its tail are.</summary>
    public ReadOnlySpan<int> LegOf(int train) => _leg[train].AsSpan(0, _legCount[train]);

    public float FrontM(int train) => _frontM[train];

    public float LengthM(int train) => _lengthM[train];

    /// <summary>The speed a train's line runs it at (TRN-2).</summary>
    public float SpeedMps(int train) => _speedMps[train];

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
    /// <param name="held">Whether the town holds its agents: nothing is sent and nothing driven.</param>
    public void Advance(ReadOnlySpan<RailVehicleSeen> seen, float dtS, bool held, TrainOrders orders)
    {
        _runCount = 0;
        for (var train = 0; train < _running.Length; train++)
        {
            if (!_running[train])
            {
                if (held) continue;

                _waitS[train] -= dtS;
                if (_waitS[train] > 0f) continue;

                Send(train, orders);
                if (!_running[train]) continue;
            }

            if (!held) Drive(train, seen, dtS, orders);
            if (_running[train]) Run(train);
        }
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

        var behindM = 0f;
        var massKg = 0f;
        _vehicles[train] = 1 + wagons;
        for (var vehicle = 0; vehicle < _vehicles[train]; vehicle++)
        {
            var vehicleKind = vehicle == 0 ? lead : wagon;
            var slot = (train * _slotsPerTrain) + _firstSlotOfKind[(int)vehicleKind] + (vehicle == 0 ? 0 : vehicle - 1);
            var looks = _catalog.OfKind(vehicleKind);
            _look[slot] = looks[draw.NextInt(looks.Length)];
            (_slotOf[train][vehicle], _behindM[train][vehicle]) = (slot, behindM);
            behindM += _catalog.ShapeOf(vehicleKind).FootprintM.X + figures.CouplingGapM;
            massKg += _catalog.ShapeOf(vehicleKind).MassKg;
        }

        _lengthM[train] = behindM - figures.CouplingGapM;
        _massKg[train] = massKg;
        _speedMps[train] = kind == TrainKind.Intercity ? figures.IntercitySpeedMps : figures.FreightSpeedMps;
        var frontM = MathF.Max(_lengthM[train], _entersM[train] - (_speedMps[train] * _config.Rail.WarningS));

        for (var vehicle = 0; vehicle < _vehicles[train]; vehicle++)
        {
            var slot = _slotOf[train][vehicle];
            ref readonly var shape = ref _catalog.ShapeOf(_kindOf[slot]);
            var middleM = frontM - _behindM[train][vehicle] - (shape.FootprintM.X * 0.5f);
            var frontBogieM = PlaceOnTheLeg(train, middleM + shape.BogiesM[0]);
            var rearBogieM = PlaceOnTheLeg(train, middleM + shape.BogiesM[^1]);
            var along = frontBogieM - rearBogieM;
            orders.Add(new TrainOrder(
                TrainOrderKind.Place, slot, _look[slot], (frontBogieM + rearBogieM) * 0.5f, MathF.Atan2(along.Y, along.X), _speedMps[train]));
            orders.Add(new TrainOrder(TrainOrderKind.Couple, slot, vehicle == 0 ? -1 : _slotOf[train][vehicle - 1]));
            orders.Add(new TrainOrder(TrainOrderKind.Drive, slot, 0, Command: Rolling));
            _channels[slot] = CityPlan.RoadArrays.GroundChannel;
        }

        ref readonly var locomotive = ref _catalog.ShapeOf(lead);
        var frontBogieLegM = frontM - (locomotive.FootprintM.X * 0.5f) + locomotive.BogiesM[0];
        (_atLane[train], _atSegmentM[train], _frontM[train]) = (LaneAt(train, frontBogieLegM), float.NaN, frontM);
        _nextSwitch[train] = 0;
        _running[train] = true;
        Sent++;
    }

    /// <summary>A vehicle rolling free, neither driven nor braked: what every vehicle of a train is told as it is put down (TRN-2).</summary>
    static DriveCommand Rolling => new(0f, 0f, 0f, false, false);

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

        if (count == 0 || SharesGroundWithALegOut(train, count)) return false;

        _legCount[train] = count;
        var startM = _legStartM[train];
        startM[0] = 0f;
        for (var lane = 0; lane < count; lane++) startM[lane + 1] = startM[lane] + _graph.LengthM(_leg[train][lane]);
        (_entersM[train], _leavesM[train]) = OnTheMap(train);

        var switches = 0;
        Span<(int Lane, int Switch, int Position)> next = stackalloc (int, int, int)[8];
        for (var lane = 1; lane < count; lane++)
        {
            var options = _graph.NextLanes(_leg[train][lane - 1], next);
            for (var option = 0; option < options; option++)
            {
                if (next[option].Lane != _leg[train][lane] || next[option].Switch < 0) continue;

                (_switchLane[train][switches], _switchOf[train][switches], _switchPosition[train][switches]) =
                    (lane, next[option].Switch, next[option].Position);
                switches++;
            }
        }

        _switches[train] = switches;
        return true;
    }

    bool SharesGroundWithALegOut(int train, int count)
    {
        var leg = _leg[train].AsSpan(0, count);
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
    /// <b>A train driven</b> (TRN-2, TRN-9): its front read off where the world left its locomotive, the throttle that holds
    /// its line's speed for the whole train's weight asked, the switches ahead set, each vehicle put on a bridge's channel
    /// where any of it is over one — and the train taken out of the world once its tail has left the map.
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
        _frontM[train] = frontM;

        if (frontM - _lengthM[train] > _leavesM[train])
        {
            Leave(train, orders);
            return;
        }

        var speedMps = Vector2.Dot(at.VelocityMps, at.Forward);
        var wantedMps2 = MathF.Max(0f, (_speedMps[train] - speedMps) / dtS);
        orders.Add(new TrainOrder(
            TrainOrderKind.Drive, loco, 0, Command: new DriveCommand(0f, wantedMps2 * _massKg[train] / locomotive.MassKg, 0f, false, false)));

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

    /// <summary>A train taken out of the world whole, uncoupled, and set waiting for its round trip off the map (TRN-7).</summary>
    void Leave(int train, TrainOrders orders)
    {
        foreach (var slot in VehiclesOf(train))
        {
            orders.Add(new TrainOrder(TrainOrderKind.Couple, slot, -1));
            orders.Add(new TrainOrder(TrainOrderKind.Contain, slot, 0));
        }

        _leftBy[train] = _graph.NodeAhead(_leg[train][_legCount[train] - 1]);
        _running[train] = false;
        _waitS[train] = _draw[train].NextFloat(_config.Train.GapLeastS, _config.Train.GapMostS);
    }

    /// <summary>
    /// <b>How far along its leg a point stands</b>, read from where the train's front bogie was last read on, walking on
    /// into the next lane of the leg past the end of one — a car's progress along its line, said of a train.
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
            if (pastM <= 0f || _atLane[train] + 1 >= _legCount[train]) return _legStartM[train][_atLane[train]] + laneM + MathF.Max(pastM, 0f);

            _atLane[train]++;
            _atSegmentM[train] = _graph.LaneForward[leg[_atLane[train]]] ? 0f : _graph.LengthM(leg[_atLane[train]]);
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
    /// <b>Where a train is on each track its leg runs</b>, from its tail's lane to the lane a warning's run ahead of its
    /// front, for the crossings (RAIL-1): its front as a distance along that track, the way the leg runs it.
    /// </summary>
    void Run(int train)
    {
        var frontM = _frontM[train];
        var reachM = _speedMps[train] * (_config.Rail.WarningS + _config.Rail.ClearS);
        var (firstRun, from, to) = (_runCount, LaneAt(train, frontM - _lengthM[train]), LaneAt(train, frontM + reachM));
        for (var lane = from; lane <= to; lane++)
        {
            var legLane = _leg[train][lane];
            var segment = _graph.SegmentOfLane[legLane];
            var forward = _graph.LaneForward[legLane];
            var startM = _graph.SegmentFromM[segment] + (forward ? 0f : _graph.Segments[segment].LengthM);
            var trackFrontM = startM + ((forward ? 1f : -1f) * (frontM - _legStartM[train][lane]));
            var run = new TrackRun(_graph.TrackOfSegment[segment], trackFrontM, _lengthM[train], forward, _speedMps[train]);
            var known = false;
            for (var other = firstRun; other < _runCount; other++) known |= _runs[other].Track == run.Track && _runs[other].Forward == forward;
            if (!known) _runs[_runCount++] = run;
        }
    }

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
internal sealed class TrainOrders
{
    TrainOrder[] _orders = new TrainOrder[256];

    public int Count { get; private set; }

    public ReadOnlySpan<TrainOrder> Written => _orders.AsSpan(0, Count);

    public void Add(in TrainOrder order)
    {
        if (Count == _orders.Length) Array.Resize(ref _orders, _orders.Length * 2);

        _orders[Count++] = order;
    }

    public void Clear() => Count = 0;
}
