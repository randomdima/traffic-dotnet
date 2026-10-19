using System.Numerics;
using TrafficSimulation.Agents.Train.Body;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;
using TrafficSimulation.World.Rail;

namespace TrafficSimulation.Agents.Train.Control;

/// <summary>
/// <b>The town's trains</b> (TRN-1…8): each railway line's timetable, the one train it has out at a time, and where every
/// wagon of that train stands on its track.
/// </summary>
/// <remarks>
/// <para>
/// <b>A train is a timetable and a distance run</b>, and nothing decides: it is sent when its line's wait runs out, runs
/// its track at its kind's speed from past one edge of the map to past the other (TRN-1, TRN-2), and its line waits again
/// once its last wagon is off the map. <b>One train a line</b> (TRN-5), so nothing here ever asks where another train is.
/// </para>
/// <para>
/// <b>Every wagon is a slot laid once</b> (TRN-7): a line has a slot for every vehicle any of its trains can be made of,
/// a locomotive of each kind and as many of each wagon as the longest train of that kind hauls, and a train sent is the
/// slots its kind takes. A slot off the map is out of the world; nothing is allocated as trains come and go.
/// </para>
/// <para>
/// <b>A wagon rides two bogies on its track</b> (TRN-8): each a pitch apart about the vehicle's middle along the train,
/// placed on the line, and the body laid along the chord between them.
/// </para>
/// </remarks>
internal sealed class Trains
{
    /// <summary>The agent stream's own sub-stream a line's timetable draws on (AGT-6), each line keyed on its number as well.</summary>
    const ulong Stream = 0x5452_4E53;

    readonly SimConfig _config;
    readonly TrainCatalog _catalog;
    readonly TrackLine[] _tracks;
    readonly Vector2 _worldSizeM;

    /// <summary>Each line's tracks with the ways they are run (TRN-5): a track run both ways is two.</summary>
    readonly (int Track, bool Forward)[][] _ways;

    /// <summary>Where along its run each track and way first comes onto the map and last goes off it, in metres run.</summary>
    readonly float[] _entersM, _leavesM;

    /// <summary>How many wagon slots each line has, and where in a line's slots each kind of vehicle's begin.</summary>
    readonly int _slotsPerLine;

    readonly int[] _firstSlotOfKind;

    /// <summary>Each slot's kind, and as it stands now: in the world or not, where, facing which way, and in which look.</summary>
    readonly VehicleKind[] _kindOf;

    readonly bool[] _inWorld;
    readonly Vector2[] _positionM;
    readonly float[] _headingRad;
    readonly int[] _look;

    /// <summary>The collision channels each slot stands on: the ground's, or a bridge's (PHY-1a).</summary>
    readonly byte[] _channels;

    /// <summary>Each line's draws, wait, and the train it has out: its track and way, how far it has run, its speed, length and vehicles.</summary>
    readonly Rng[] _draw;

    readonly float[] _waitS;
    readonly bool[] _running;
    readonly int[] _way;
    readonly float[] _runM;
    readonly float[] _speedMps;
    readonly float[] _lengthM;
    readonly int[] _vehicles;

    /// <summary>Each line's train, vehicle by vehicle from the front: its slot, and how far behind the train's front its own front is.</summary>
    readonly int[][] _slotOf;

    readonly float[][] _behindM;

    readonly TrackRun[] _runs;
    int _runCount;

    public Trains(CityPlan.RailArrays rails, TrackLine[] tracks, Vector2 worldSizeM, TrainCatalog catalog, SimConfig config, ulong agentSeed)
    {
        (_config, _catalog, _tracks, _worldSizeM) = (config, catalog, tracks, worldSizeM);
        var lines = rails.Count == 0 ? 0 : rails.Line.Max() + 1;
        var ways = new List<(int, bool)>[lines];
        for (var line = 0; line < lines; line++) ways[line] = [];
        for (var track = 0; track < rails.Count; track++)
        {
            if ((rails.Runs[track] & TownMap.TrackRuns.Forward) != 0) ways[rails.Line[track]].Add((track, true));
            if ((rails.Runs[track] & TownMap.TrackRuns.Backward) != 0) ways[rails.Line[track]].Add((track, false));
        }

        _ways = [.. ways.Select(list => list.ToArray())];
        (_entersM, _leavesM) = (new float[tracks.Length * 2], new float[tracks.Length * 2]);
        for (var track = 0; track < tracks.Length; track++)
        {
            foreach (var forward in (ReadOnlySpan<bool>)[true, false]) (_entersM[WayIndex(track, forward)], _leavesM[WayIndex(track, forward)]) = OnTheMap(track, forward);
        }

        var figures = config.Train;
        var mostOf = new[] { 1, 1, figures.IntercityCoaches, figures.OilWagons, figures.CoalWagons };
        _firstSlotOfKind = new int[mostOf.Length];
        for (var kind = 1; kind < mostOf.Length; kind++) _firstSlotOfKind[kind] = _firstSlotOfKind[kind - 1] + mostOf[kind - 1];

        _slotsPerLine = _firstSlotOfKind[^1] + mostOf[^1];
        var wagons = lines * _slotsPerLine;
        (_kindOf, _inWorld, _positionM, _headingRad, _look) = (new VehicleKind[wagons], new bool[wagons], new Vector2[wagons], new float[wagons], new int[wagons]);
        _channels = new byte[wagons];
        for (var wagon = 0; wagon < wagons; wagon++)
        {
            var slot = wagon % _slotsPerLine;
            var kind = mostOf.Length - 1;
            while (_firstSlotOfKind[kind] > slot) kind--;
            _kindOf[wagon] = (VehicleKind)kind;
        }

        var longest = 1 + Math.Max(figures.IntercityCoaches, Math.Max(figures.OilWagons, figures.CoalWagons));
        (_draw, _waitS, _running, _way, _runM) = (new Rng[lines], new float[lines], new bool[lines], new int[lines], new float[lines]);
        (_speedMps, _lengthM, _vehicles) = (new float[lines], new float[lines], new int[lines]);
        (_slotOf, _behindM) = (new int[lines][], new float[lines][]);
        for (var line = 0; line < lines; line++)
        {
            _draw[line] = new Rng(agentSeed, Stream + (ulong)line);
            _waitS[line] = _draw[line].NextFloat(figures.FirstLeastS, figures.FirstMostS);
            (_slotOf[line], _behindM[line]) = (new int[longest], new float[longest]);
        }

        _runs = new TrackRun[lines];
    }

    /// <summary>How many wagon slots the town's lines have between them, each a body of the world (TRN-7).</summary>
    public int WagonCount => _kindOf.Length;

    /// <summary>The same, of a railway and the figures before any town stands on it — what a frame's room is laid for.</summary>
    public static int WagonsFor(CityPlan.RailArrays rails, TrainFigures figures) =>
        (rails.Count == 0 ? 0 : rails.Line.Max() + 1) * (2 + figures.IntercityCoaches + figures.OilWagons + figures.CoalWagons);

    /// <summary>What a slot's vehicle measures, half its length and width, and how round its corners are: the body it is.</summary>
    public (Vector2 HalfSizeM, float CornerRadiusM) ShapeOf(int wagon)
    {
        ref readonly var shape = ref _catalog.ShapeOf(_kindOf[wagon]);
        return (shape.FootprintM * 0.5f, shape.CornerRadiusM);
    }

    public bool InWorld(int wagon) => _inWorld[wagon];

    public Vector2 PositionM(int wagon) => _positionM[wagon];

    public float HeadingRad(int wagon) => _headingRad[wagon];

    /// <summary>How fast a wagon is running: its line's train's speed.</summary>
    public float SpeedMps(int wagon) => _speedMps[wagon / _slotsPerLine];

    /// <summary>Which of the catalogue's vehicles a wagon is drawn as.</summary>
    public int LookOf(int wagon) => _look[wagon];

    /// <summary>
    /// The collision channels a wagon stands on (PHY-1a, RAIL-8): the ground's at grade, and on a bridge one no road is on,
    /// so it meets nothing it passes over.
    /// </summary>
    /// <remarks>
    /// <b>A wagon any of which is over a bridge is on the bridge's channel</b>: it is one body, what stands under a bridge's
    /// end stands under the wagon's, and the track at grade beside a bridge is its approach, where nobody stands.
    /// </remarks>
    public byte ChannelsOf(int wagon) => _channels[wagon];

    /// <summary>Every train out, where it is on its track, for the crossings (RAIL-1).</summary>
    public ReadOnlySpan<TrackRun> Runs => _runs.AsSpan(0, _runCount);

    /// <summary>How many trains the lines have sent, all told: an instrument's reading.</summary>
    public int Sent { get; private set; }

    /// <summary>How many lines the town's railway has.</summary>
    public int Lines => _running.Length;

    /// <summary>
    /// <b>One tick of every line</b>: a waiting line's clock run down and its train sent when it runs out, every train run on
    /// at its speed and its wagons placed — out of the world off the map — and a train whose last wagon has left the map
    /// retired and its line set waiting.
    /// </summary>
    public void Advance(float dtS)
    {
        _runCount = 0;
        for (var line = 0; line < _running.Length; line++)
        {
            if (!_running[line])
            {
                _waitS[line] -= dtS;
                if (_waitS[line] > 0f || _ways[line].Length == 0) continue;

                Send(line);
            }

            _runM[line] += _speedMps[line] * dtS;
            var way = _way[line];
            if (_runM[line] - _lengthM[line] > _leavesM[way])
            {
                Retire(line);
                continue;
            }

            Place(line);
        }
    }

    void Send(int line)
    {
        ref var draw = ref _draw[line];
        var figures = _config.Train;
        var share = draw.NextFloat();
        var kind = share < figures.IntercityShare ? TrainKind.Intercity : share < figures.IntercityShare + figures.OilShare ? TrainKind.Oil : TrainKind.Coal;
        var (track, forward) = _ways[line][draw.NextInt(_ways[line].Length)];
        var (lead, wagon, wagons) = kind switch
        {
            TrainKind.Intercity => (VehicleKind.Electric, VehicleKind.Coach, figures.IntercityCoaches),
            TrainKind.Oil => (VehicleKind.Diesel, VehicleKind.Tank, figures.OilWagons),
            _ => (VehicleKind.Diesel, VehicleKind.Coal, figures.CoalWagons),
        };

        var behindM = 0f;
        _vehicles[line] = 1 + wagons;
        for (var vehicle = 0; vehicle < _vehicles[line]; vehicle++)
        {
            var vehicleKind = vehicle == 0 ? lead : wagon;
            var slot = (line * _slotsPerLine) + _firstSlotOfKind[(int)vehicleKind] + (vehicle == 0 ? 0 : vehicle - 1);
            var looks = _catalog.OfKind(vehicleKind);
            _look[slot] = looks[draw.NextInt(looks.Length)];
            (_slotOf[line][vehicle], _behindM[line][vehicle]) = (slot, behindM);
            behindM += _catalog.ShapeOf(vehicleKind).FootprintM.X + figures.CouplingGapM;
        }

        var way = WayIndex(track, forward);
        _lengthM[line] = behindM - figures.CouplingGapM;
        _speedMps[line] = kind == TrainKind.Intercity ? figures.IntercitySpeedMps : figures.FreightSpeedMps;

        // Sent far enough short of the map that every crossing near the edge it comes on at closes a warning ahead of it (RAIL-1).
        _runM[line] = MathF.Max(0f, _entersM[way] - (_speedMps[line] * _config.Rail.WarningS * 2f));
        (_running[line], _way[line]) = (true, way);
        Sent++;
    }

    void Retire(int line)
    {
        _running[line] = false;
        _waitS[line] = _draw[line].NextFloat(_config.Train.GapLeastS, _config.Train.GapMostS);
        for (var vehicle = 0; vehicle < _vehicles[line]; vehicle++) _inWorld[_slotOf[line][vehicle]] = false;
    }

    /// <summary>Every vehicle of a line's train put on its two bogies (TRN-8), and in the world where any of it is on the map (TRN-7).</summary>
    void Place(int line)
    {
        var way = _way[line];
        var (track, forward) = (way / 2, way % 2 == 0);
        var trackLine = _tracks[track];
        _runs[_runCount++] = new TrackRun(track, AlongTrackM(trackLine, forward, _runM[line]), _lengthM[line], forward, _speedMps[line]);
        for (var vehicle = 0; vehicle < _vehicles[line]; vehicle++)
        {
            var slot = _slotOf[line][vehicle];
            ref readonly var shape = ref _catalog.ShapeOf(_kindOf[slot]);
            var middleRunM = _runM[line] - _behindM[line][vehicle] - (shape.FootprintM.X * 0.5f);
            var frontBogieM = trackLine.PlaceAt(AlongTrackM(trackLine, forward, middleRunM + (shape.BogiePitchM * 0.5f)));
            var rearBogieM = trackLine.PlaceAt(AlongTrackM(trackLine, forward, middleRunM - (shape.BogiePitchM * 0.5f)));
            var centreM = (frontBogieM + rearBogieM) * 0.5f;
            var reachM = shape.FootprintM.X * 0.5f;
            _inWorld[slot] = centreM.X > -reachM && centreM.Y > -reachM && centreM.X < _worldSizeM.X + reachM && centreM.Y < _worldSizeM.Y + reachM;
            _positionM[slot] = centreM;
            var along = frontBogieM - rearBogieM;
            _headingRad[slot] = MathF.Atan2(along.Y, along.X);
            _channels[slot] = trackLine.BridgedBetween(AlongTrackM(trackLine, forward, middleRunM - reachM), AlongTrackM(trackLine, forward, middleRunM + reachM))
                ? CityPlan.RailArrays.BridgeChannel
                : CityPlan.RoadArrays.GroundChannel;
        }
    }

    static float AlongTrackM(TrackLine track, bool forward, float runM) => forward ? runM : track.LengthM - runM;

    static int WayIndex(int track, bool forward) => (track * 2) + (forward ? 0 : 1);

    /// <summary>Where a track run one way first comes onto the map and last goes off it, in metres run that way.</summary>
    (float EntersM, float LeavesM) OnTheMap(int track, bool forward)
    {
        var line = _tracks[track];
        var (firstM, lastM) = (float.PositiveInfinity, float.NegativeInfinity);
        for (var chord = 0; chord + 1 < line.PointM.Length; chord++)
        {
            if (!MapCut.Shares(line.PointM[chord], line.PointM[chord + 1], _worldSizeM, out var enters, out var leaves)) continue;

            var (fromM, lengthM) = (line.AtM[chord], line.AtM[chord + 1] - line.AtM[chord]);
            foreach (var share in (ReadOnlySpan<float>)[enters, leaves])
            {
                var atM = fromM + (share * lengthM);
                var runM = forward ? atM : line.LengthM - atM;
                (firstM, lastM) = (MathF.Min(firstM, runM), MathF.Max(lastM, runM));
            }
        }

        return float.IsPositiveInfinity(firstM) ? (0f, 0f) : (firstM, lastM);
    }
}
