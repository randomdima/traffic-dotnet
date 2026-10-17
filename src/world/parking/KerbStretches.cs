using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Parking;

/// <summary>
/// <b>The stretches of roadside a car may park along</b> (GEN-4n): each roadside's own run outside the boxes, less the
/// ground about every crossing and junction a car may not stand in front of, each worked off the lane beside it — the kerb
/// lane of the traffic keeping to that kerb.
/// </summary>
/// <remarks>
/// <para>
/// <b>A stretch is continuous and holds no slot</b>: where along it a car stands is whatever gap it finds when it comes
/// to park, and the ground it then holds is its body's ordinary claim on the roadside (TER-4c.2). Nothing here says
/// which stretches are taken; the bodies on the roadside do.
/// </para>
/// <para>
/// <b>A roadside nobody's traffic keeps to</b> — the far kerb of a one-way street — carries none: a car parks the way
/// the traffic beside it runs. <b>Nor does one beside a lane that only ends</b> (<see cref="RoadGraph.DrivesOn"/>): a dead
/// end, a lane off the map's edge.
/// </para>
/// </remarks>
internal sealed class KerbStretches
{
    readonly int[] _roadside;
    readonly int[] _beside;
    readonly float[] _fromM;
    readonly float[] _toM;
    readonly int[] _firstOfLane;
    readonly int[] _ofLane;

    KerbStretches(int[] roadside, int[] beside, float[] fromM, float[] toM, int[] firstOfLane, int[] ofLane, Dictionary<int, int> laneBeside)
    {
        _roadside = roadside;
        _beside = beside;
        _fromM = fromM;
        _toM = toM;
        _firstOfLane = firstOfLane;
        _ofLane = ofLane;
        _laneBeside = laneBeside;
    }

    /// <summary>A town with no kerb to park along, which is every town before its kerbs are laid.</summary>
    public static KerbStretches None => new([], [], [], [], [0], [], []);

    public int Count => _roadside.Length;

    /// <summary>The roadside a stretch runs along.</summary>
    public int RoadsideOf(int stretch) => _roadside[stretch];

    /// <summary>The street lane a stretch is worked off: the one a car drives to park along it, and leaves onto.</summary>
    public int BesideOf(int stretch) => _beside[stretch];

    /// <summary>Where along its roadside a stretch runs from, in the roadside's own metres.</summary>
    public float FromM(int stretch) => _fromM[stretch];

    /// <inheritdoc cref="FromM"/>
    public float ToM(int stretch) => _toM[stretch];

    /// <summary>The stretches worked off a street lane, each once.</summary>
    public ReadOnlySpan<int> BesideLane(int lane) => _ofLane.AsSpan(_firstOfLane[lane], _firstOfLane[lane + 1] - _firstOfLane[lane]);

    /// <summary>
    /// <b>The kerb lane a roadside lies beside</b>, whether or not anything of it may be parked along — or −1: what a car
    /// standing on the roadside takes as the road under it (CAR-9), the roadside being no lane it is driven down.
    /// </summary>
    public int LaneBeside(int roadside) => _laneBeside.TryGetValue(roadside, out var lane) ? lane : -1;

    readonly Dictionary<int, int> _laneBeside;

    /// <summary>
    /// <b>Every roadside's parkable stretches</b>: its own run (<see cref="RoadGraph.RoadsideFromM"/>), less
    /// <paramref name="clearM"/> at each end — the junction it runs into — and either side of every stretch of it another
    /// way's ribbon lies over (<see cref="WayCrossings"/>), a zebra's above all; and of what is left, every piece of
    /// <paramref name="leastM"/> or more.
    /// </summary>
    /// <param name="laneRunM">
    /// How far the lane beside a stretch runs on before it and past it, which the stretch is cut back to: a car
    /// stopping for it, and one pulling out of it, each have that much of the lane.
    /// </param>
    /// <param name="curvatureMax">The most a roadside may bend under a car parked along it, past which it carries none.</param>
    public static KerbStretches Build(
        RoadGraph roads, TownWays ways, WayCrossings marks, float clearM, float leastM, float laneRunM, float curvatureMax)
    {
        var lanesOfRoad = new Dictionary<int, List<int>>();
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (!roads.IsAStreetLane(lane) || roads.LaneFromKerb[lane] != 0) continue;

            var road = roads.LaneRoad[lane];
            if (!lanesOfRoad.TryGetValue(road, out var kerbLanes)) lanesOfRoad[road] = kerbLanes = [];
            kerbLanes.Add(lane);
        }

        var entered = EnteredFromTheTown(roads);
        var roadside = new List<int>();
        var beside = new List<int>();
        var fromM = new List<float>();
        var toM = new List<float>();
        var perLane = new List<int>?[roads.LaneCount];
        var laneBeside = new Dictionary<int, int>();
        var taken = new List<(float FromM, float ToM)>();
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (!roads.IsARoadside(lane) || !lanesOfRoad.TryGetValue(roads.LaneRoad[lane], out var kerbLanes)) continue;

            var runFromM = roads.RoadsideFromM(lane) + clearM;
            var runToM = roads.RoadsideToM(lane) - clearM;
            var lane0 = BesideTheRoadside(roads, lane, (roads.RoadsideFromM(lane) + roads.RoadsideToM(lane)) * 0.5f, kerbLanes);
            if (lane0 < 0) continue;

            laneBeside[lane] = lane0;

            // Never beside a lane that only ends (RoadGraph.DrivesOn): a car parked there leaves onto a lane it cannot
            // turn round on, and one coming to park drives to the end of it. Nor beside one nothing enters but the far
            // side of a dead end, which a car gets to only by driving down that dead end first.
            if (runToM - runFromM < leastM || !roads.DrivesOn(lane0) || !entered[lane0]) continue;

            var roadsideArcs = roads.ArcsOf(lane);
            var besideArcs = roads.ArcsOf(lane0);
            var besideM = roads.LaneLengthM[lane0];
            var roadsideM = roads.LaneLengthM[lane];
            runFromM = MathF.Max(runFromM, Spline.ProjectM(roadsideArcs, Spline.SampleAt(besideArcs, laneRunM).PositionM, roadsideM * 0.5f, roadsideM));
            runToM = MathF.Min(runToM, Spline.ProjectM(roadsideArcs, Spline.SampleAt(besideArcs, besideM - laneRunM).PositionM, roadsideM * 0.5f, roadsideM));
            if (besideM < 2f * laneRunM || runToM - runFromM < leastM) continue;

            taken.Clear();
            foreach (ref readonly var mark in marks.Of(ways.OfRoadLane(lane))) taken.Add((mark.MineFromM - clearM, mark.MineToM + clearM));

            // A bend tighter than a car parks along is no kerb to park at, a half car either side of it.
            var arcFromM = 0f;
            foreach (ref readonly var arc in roadsideArcs)
            {
                if (MathF.Abs(arc.Curvature) > curvatureMax) taken.Add((arcFromM - (leastM * 0.5f), arcFromM + arc.LengthM + (leastM * 0.5f)));
                arcFromM += arc.LengthM;
            }

            taken.Sort();

            var atM = runFromM;
            foreach (var (markFromM, markToM) in taken)
            {
                if (markFromM - atM >= leastM) Add(lane, lane0, atM, MathF.Min(markFromM, runToM));
                atM = MathF.Max(atM, markToM);
                if (atM >= runToM) break;
            }

            if (runToM - atM >= leastM) Add(lane, lane0, atM, runToM);
        }

        var firstOfLane = new int[roads.LaneCount + 1];
        var ofLane = new List<int>();
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (perLane[lane] is { } stretches) ofLane.AddRange(stretches);
            firstOfLane[lane + 1] = ofLane.Count;
        }

        return new KerbStretches([.. roadside], [.. beside], [.. fromM], [.. toM], firstOfLane, [.. ofLane], laneBeside);

        void Add(int onRoadside, int besideLane, float fromAtM, float toAtM)
        {
            if (toAtM - fromAtM < leastM) return;

            (perLane[besideLane] ??= []).Add(roadside.Count);
            roadside.Add(onRoadside);
            beside.Add(besideLane);
            fromM.Add(fromAtM);
            toM.Add(toAtM);
        }
    }

    /// <summary>
    /// <b>The street lanes a car gets onto from the rest of the town</b>, one flag a lane: every street lane, less — again
    /// and again, until none is left to take — every lane no movement enters from a lane still kept. What is taken is
    /// the far side of a dead end and what only it leads onto.
    /// </summary>
    static bool[] EnteredFromTheTown(RoadGraph roads)
    {
        var entering = new int[roads.LaneCount];
        var entered = new bool[roads.LaneCount];
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (!roads.IsAStreetLane(lane)) continue;

            entered[lane] = true;
            foreach (var connector in roads.ConnectorsFrom(lane)) entering[roads.ConnectorTo(connector)]++;
        }

        var taken = new Queue<int>();
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (entered[lane] && entering[lane] == 0) taken.Enqueue(lane);
        }

        while (taken.TryDequeue(out var lane))
        {
            entered[lane] = false;
            foreach (var connector in roads.ConnectorsFrom(lane))
            {
                var onto = roads.ConnectorTo(connector);
                if (entered[onto] && --entering[onto] == 0) taken.Enqueue(onto);
            }
        }

        return entered;
    }

    /// <summary>
    /// <b>The kerb lane a roadside lies beside</b>: of its road's kerb lanes, the nearest one running the way the roadside
    /// does, read at a metre of it — or −1 where none does.
    /// </summary>
    static int BesideTheRoadside(RoadGraph roads, int roadside, float atM, List<int> kerbLanes)
    {
        var at = Spline.SampleAt(roads.ArcsOf(roadside), atM);
        var best = -1;
        var bestSq = float.PositiveInfinity;
        foreach (var lane in kerbLanes)
        {
            var arcs = roads.ArcsOf(lane);
            var alongM = Spline.ProjectM(arcs, at.PositionM, roads.LaneLengthM[lane] * 0.5f, roads.LaneLengthM[lane], out var offSq);
            if (offSq >= bestSq || System.Numerics.Vector2.Dot(Spline.SampleAt(arcs, alongM).Direction, at.Direction) <= 0f) continue;

            best = lane;
            bestSq = offSq;
        }

        return best;
    }
}
