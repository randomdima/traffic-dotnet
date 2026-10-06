using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>How far a traced town's lanes stand off its map's own</b> (GEN-57), read both ways: every metre of every lane
/// the map counts on a laid way — each <see cref="CityGenFigures.TracedLaneWidthM"/> wide about the way's line, as
/// far off it as OSM places the carriageway — against the nearest lane the engine drives the same way, and every metre
/// the engine lays against the nearest of the map's. The engine's lines are its lanes and the connectors across its
/// junctions, which is where the map's lanes run on to the node the ways meet at.
/// </summary>
/// <remarks>
/// <b>Quoted and gating nothing</b>: it is the reading a change to how a survey is laid is weighed by, and the
/// worst places are named by OSM way and by road so they can be looked up. A lane is only ever held against one
/// running within <see cref="SameWayCos"/> of its own heading, so a carriageway's two sides are never mistaken
/// for each other.
/// </remarks>
internal static class TracedFidelity
{
    /// <summary>Readings are taken this often along a line, so a long straight weighs what it is long.</summary>
    const float StepM = 1f;

    /// <summary>Nothing nearer than this is looked for, and a reading at it is a line with nothing laid along it.</summary>
    const float SearchM = 40f;

    const float CellM = 20f;

    /// <summary>The cosine of the widest angle a lane is still read as running the same way as another.</summary>
    const float SameWayCos = 0.5f;

    static readonly float[] Thresholds = [0.05f, 0.25f, 0.5f, 1f, 2f, 5f, 10f];

    /// <summary>How near a line's metre must be to the other set's for the line to count as laid there.</summary>
    const float NearM = 1f;

    /// <summary>A line shorter than this has no heading worth the name — a rounding's sliver — and is not read.</summary>
    const float ShortestM = 1e-3f;

    /// <param name="atM">
    /// A place to read closely instead: every surveyed way and every laid road and junction within
    /// <see cref="SearchM"/> of it.
    /// </param>
    public static void Run(string map, SimConfig config, Vector2? atM = null)
    {
        var traced = Maps.IsCity(map) ? Maps.Read(map) : null;
        if (traced is null || traced.Roads.Length == 0)
        {
            Console.WriteLine($"{map} holds no road of its own: nothing to hold it against.");
            return;
        }

        var survey = Survey.Of(traced, config);
        var plan = Maps.Plan(map, config, BuildingSizes.None);
        var ways = traced.Roads.ToDictionary(road => road.OsmId);
        if (atM is { } placeM)
        {
            Near(survey, ways, plan, placeM);
            return;
        }

        // Each way as far as the map lays it, its lanes left to right along its points: those against them on the side
        // traffic does not keep to, then any driven both ways, then those with them — between its roadsides, the one
        // along it at the kerb its traffic keeps to.
        var osm = new Lines();
        var widthM = config.CityGen.TracedLaneWidthM;
        var keepsRight = config.Road.TrafficKeepsRight;
        foreach (var way in survey.Ways)
        {
            var lineM = way.Points.Select(survey.PointM).ToArray();
            var laneM = new Vector2[lineM.Length];
            var (against, both, with) = (way.LanesBackward, way.LanesShared, way.LanesForward);
            var counted = against + both + with;
            var middleM = way.CentreOffsetM + ((way.RoadsideAgainstM - way.RoadsideAlongM) * (keepsRight ? 0.5f : -0.5f));
            for (var lane = 0; lane < counted; lane++)
            {
                var fromLeft = keepsRight ? lane : counted - 1 - lane;
                OsmCarriageway.OffsetInto(lineM, middleM + ((fromLeft + 0.5f - (counted * 0.5f)) * widthM), laneM);
                if (lane >= against && lane < against + both)
                {
                    osm.AddLine(laneM, way.OsmId, Shared);
                    continue;
                }

                if (lane < against) Array.Reverse(laneM);
                osm.AddLine(laneM, way.OsmId, Lane);
            }
        }

        var discs = new Discs(plan.Junctions);

        // A roadside is ground of the carriageway and no lane OSM draws (LaneLines.IsRoadside), so the lanes weighed are
        // the ones before them.
        var lanes = plan.Paving(config).Lanes;
        var laidLanes = new Lines();
        var laid = new Lines();
        for (var lane = 0; lane < lanes.FirstRoadside; lane++)
        {
            laidLanes.AddArcs(lanes.ArcsOf(lane), lanes.LaneRoad[lane]);
            laid.AddArcs(lanes.ArcsOf(lane), lanes.LaneRoad[lane]);
        }

        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            laid.AddArcs(lanes.ArcsOfConnector(connector), -1 - lanes.JunctionOfConnector(connector));
        }

        var laidWays = survey.Ways.Select(way => way.OsmId).Distinct().ToArray();
        Console.WriteLine($"{map}: the map's {laidWays.Sum(id => ways[id].Lanes)} lanes on {laidWays.Length} ways, " +
                          $"{osm.LengthM / 1000f:F1} km driven inside the map; " +
                          $"laid {lanes.FirstRoadside} lanes on {plan.Roads.Count} roads, {laidLanes.LengthM / 1000f:F1} km, " +
                          $"{lanes.LaneCount - lanes.FirstRoadside} roadsides beside them, " +
                          $"and {lanes.ConnectorCount} connectors, {(laid.LengthM - laidLanes.LengthM) / 1000f:F1} km");
        Console.WriteLine($"measured: {laidWays.Count(id => ways[id].WidthM is not null)} of {laidWays.Length} ways as wide as measured");

        // The map's lanes run on to the node the ways meet at, which is across a junction the engine drives on a
        // connector; a connector turning there has no lane of the map's to be held against.
        Report("the map's lanes off laid lanes and connectors", osm, laid, discs, id => $"way {id}");
        Report("laid lanes off the map's", laidLanes, osm, discs, id => $"road {id}");

        var streets = TracedStreets.Lay(survey, config);
        Console.WriteLine($"shared: {streets.Shared.Directed} roads of one lane both ways share run one way; taken out " +
                          $"{streets.Shared.Spurs} spurs and {streets.Shared.Stranded} roads of pieces of nothing else");
        Console.WriteLine($"gathered: {streets.Gathered.Junctions} junctions into {streets.Gathered.Into}, {streets.Gathered.Places} of them " +
                          $"places of two arms inside another's disc, {streets.Gathered.Roads} roads " +
                          $"between them gone, {streets.Gathered.Unmade} movements across them none of those roads made forbidden");
        Turns(plan, lanes);
    }

    /// <summary>
    /// <b>Whether any connector makes a turn the plan forbids</b> — the movements across a gathered junction none of its
    /// roads made — and which lanes into a junction another road leaves are left with no turn there.
    /// </summary>
    static void Turns(CityPlan plan, LaneLines lanes)
    {
        var banned = plan.Roads.BannedTurns.ToHashSet();
        var forbidden = 0;
        for (var connector = 0; connector < lanes.ConnectorCount; connector++)
        {
            var (from, to) = (lanes.ConnectorFromLane[connector], lanes.ConnectorToLane[connector]);
            if (banned.Contains(new RoadTurn(lanes.JunctionOfConnector(connector), lanes.LaneRoad[from], lanes.LaneRoad[to]))) forbidden++;
        }

        // A lane into a junction some other road leaves, left with no turn: what a gathered junction forbids may do that.
        var leaves = lanes.LaneRoad.Take(lanes.FirstRoadside).Select((road, lane) => (Junction: lanes.LaneFromJunction[lane], Road: road)).ToHashSet();
        var leaving = leaves.GroupBy(leaves => leaves.Junction).ToDictionary(group => group.Key, group => group.Count());
        var (stranded, strandedByBans, ended) = (0, 0, 0);
        var strandedAt = new List<string>();
        var turnedAt = banned.Select(turn => (turn.Junction, turn.FromRoad)).ToHashSet();
        for (var lane = 0; lane < lanes.FirstRoadside; lane++)
        {
            var (junction, road) = (lanes.LaneToJunction[lane], lanes.LaneRoad[lane]);
            var others = leaving.GetValueOrDefault(junction) - (leaves.Contains((junction, road)) ? 1 : 0);
            if (lanes.ConnectorAt[lane + 1] > lanes.ConnectorAt[lane] || others == 0) continue;

            // A lane lost at the node is left by moving across (TER-5j), and is no lane stranded.
            if (BesideOneThat(lanes, lane, beside => lanes.ConnectorAt[beside + 1] > lanes.ConnectorAt[beside]))
            {
                ended++;
                continue;
            }

            stranded++;
            if (turnedAt.Contains((junction, road))) strandedByBans++;
            if (strandedAt.Count < 8)
            {
                strandedAt.Add($"road {road} at ({plan.Junctions.CentreM[junction].X:F0}, {plan.Junctions.CentreM[junction].Y:F0}) " +
                               $"forbidden {banned.Count(turn => turn.Junction == junction && turn.FromRoad == road)} of {others}");
            }
        }

        var reached = new bool[lanes.LaneCount];
        foreach (var onto in lanes.ConnectorToLane) reached[onto] = true;
        var movedOnto = 0;
        for (var lane = 0; lane < lanes.FirstRoadside; lane++)
        {
            if (!reached[lane] && !lanes.LaneIsBay[lane] && BesideOneThat(lanes, lane, beside => reached[beside])) movedOnto++;
        }

        Console.WriteLine($"lanes: {lanes.ConnectorCount} connectors; {ended} lanes end at a node beside one that carries on, " +
                          $"{movedOnto} are reached only by moving across (TER-5j)");
        Console.WriteLine($"turns: {banned.Count} forbidden across gathered junctions; connectors making a forbidden turn {forbidden}; " +
                          $"lanes into a junction another road leaves with no turn there {stranded}, {strandedByBans} of them off a road a turn is forbidden from" +
                          (strandedAt.Count > 0 ? ": " + string.Join(", ", strandedAt) : ""));
    }

    /// <summary>Whether any lane beside this one running its way (CAR-53) is one that <paramref name="holds"/>.</summary>
    static bool BesideOneThat(LaneLines lanes, int lane, Func<int, bool> holds)
    {
        foreach (var toward in (ReadOnlySpan<int[]>)[lanes.LaneInward, lanes.LaneOutward])
        {
            for (var beside = toward[lane]; beside != LaneLines.NoLane; beside = toward[beside])
            {
                if (holds(beside)) return true;
            }
        }

        return false;
    }

    const byte Lane = 0;

    /// <summary>
    /// A lane OSM has driven both ways, which the engine lays as a lane of one way or not at all: read as drawn, and
    /// held against a line running either way.
    /// </summary>
    const byte Shared = 1;

    static readonly string[] Parts = ["lanes", "shared single lanes", "inside junctions"];

    /// <summary>
    /// Every metre of one set of lines read against the nearest of the other running the same way: what share of
    /// the length stands within each threshold, overall and for each part of the network read apart
    /// (<see cref="Parts"/>), and the worst place on each of the dozen lines furthest off.
    /// </summary>
    static void Report(string what, Lines from, Lines onto, Discs discs, Func<long, string> named)
    {
        var withinM = new double[Parts.Length + 1, Thresholds.Length];
        var totalM = new double[Parts.Length + 1];
        var offs = new List<float>();
        var worst = new Dictionary<long, (float OffM, Vector2 AtM)>();
        var ownM = new Dictionary<long, (double TotalM, double NearM)>();
        for (var line = 0; line < from.Count; line++)
        {
            var (a, b) = (from.A[line], from.B[line]);
            var lengthM = Vector2.Distance(a, b);
            var steps = Math.Max(1, (int)MathF.Ceiling(lengthM / StepM));
            for (var step = 0; step <= steps; step++)
            {
                var atM = Vector2.Lerp(a, b, (float)step / steps);
                var offM = onto.NearestM(atM, from.Heading[line], eitherWay: from.Kind[line] == Shared);
                var weightM = (step == 0 || step == steps ? 0.5 : 1.0) * lengthM / steps;
                var part = 1 + (discs.Inside(atM) ? 2 : from.Kind[line]);
                foreach (var counted in (ReadOnlySpan<int>)[0, part])
                {
                    totalM[counted] += weightM;
                    for (var at = 0; at < Thresholds.Length; at++)
                    {
                        if (offM <= Thresholds[at]) withinM[counted, at] += weightM;
                    }
                }

                offs.Add(offM);
                var owned = ownM.GetValueOrDefault(from.Owner[line]);
                ownM[from.Owner[line]] = (owned.TotalM + weightM, owned.NearM + (offM <= NearM ? weightM : 0.0));
                if (!worst.TryGetValue(from.Owner[line], out var was) || offM > was.OffM) worst[from.Owner[line]] = (offM, atM);
            }
        }

        offs.Sort();
        Console.WriteLine($"{what}: median {Pick(0.5f):F2} m  p90 {Pick(0.9f):F2}  p99 {Pick(0.99f):F2}  max {offs[^1]:F1}" +
                          $"{(offs[^1] >= SearchM ? "+" : "")}");
        for (var part = 0; part <= Parts.Length; part++)
        {
            if (totalM[part] <= 0) continue;

            var shares = string.Join("  ", Thresholds.Select((t, at) => $"≤{t:0.##} m {withinM[part, at] / totalM[part] * 100.0:F1}%"));
            Console.WriteLine($"  {(part == 0 ? "all" : Parts[part - 1]),-19} {totalM[part] / 1000:F0} km  {shares}");
        }

        // A line laid nowhere near is a line lost, which a share of the whole would hide among the rest.
        var lost = ownM.Where(entry => entry.Value.NearM < entry.Value.TotalM * 0.5).OrderByDescending(entry => entry.Value.TotalM).ToArray();
        Console.WriteLine($"  {lost.Length} of {ownM.Count} {named(0).Split(' ')[0]}s have less than half their length within {NearM:0} m, " +
                          $"{lost.Sum(entry => entry.Value.TotalM):F0} m in all" +
                          (lost.Length > 0 ? ": " + string.Join(", ", lost.Take(8).Select(entry =>
                              $"{named(entry.Key)} {entry.Value.TotalM:F0} m at ({worst[entry.Key].AtM.X:F0}, {worst[entry.Key].AtM.Y:F0})")) : ""));

        foreach (var (id, (offM, atM)) in worst.OrderByDescending(entry => entry.Value.OffM).Take(12))
        {
            Console.WriteLine($"  {named(id),-18} {offM,6:F2} m at ({atM.X:F1}, {atM.Y:F1})");
        }

        float Pick(float share) => offs[Math.Min(offs.Count - 1, (int)(offs.Count * share))];
    }

    static void Near(Survey survey, Dictionary<long, TracedRoad> ways, CityPlan plan, Vector2 placeM)
    {
        foreach (var way in survey.Ways)
        {
            var points = way.Points;
            if (!Array.Exists(points, point => Vector2.Distance(survey.PointM(point), placeM) <= SearchM)) continue;

            var road = ways[way.OsmId];
            Console.WriteLine($"way {way.OsmId} {way.Highway} lanes {way.LanesForward}+{way.LanesBackward}+{way.LanesShared} " +
                              $"{(road.WidthM is { } widthM ? $"{widthM:F2} m" : "unmeasured")} off {way.CentreOffsetM:F2}: " +
                              string.Join(" ", points.Select(point => $"{point}({survey.PointM(point).X:F1},{survey.PointM(point).Y:F1})")));
        }

        for (var junction = 0; junction < plan.Junctions.Count; junction++)
        {
            var centreM = plan.Junctions.CentreM[junction];
            if (Vector2.Distance(centreM, placeM) > SearchM) continue;

            Console.WriteLine($"junction {junction} at ({centreM.X:F1},{centreM.Y:F1}) radius {plan.Junctions.RadiusM[junction]:F2}");
        }

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            var arcs = plan.Roads.SegmentsOf(road);
            if (!arcs.ToArray().Any(arc => Vector2.Distance(arc.StartM, placeM) <= SearchM || Vector2.Distance(arc.EndM, placeM) <= SearchM)) continue;

            Console.WriteLine($"road {road} {plan.Roads.FromJunction[road]}->{plan.Roads.ToJunction[road]} lanes {plan.Roads.LanesWithTheRoad(road)}+" +
                              $"{plan.Roads.LanesAgainstTheRoad(road)} width {plan.Roads.WidthM[road]:F2}: " +
                              string.Join(" ", arcs.ToArray().Select(arc => $"({arc.StartM.X:F1},{arc.StartM.Y:F1}) k {arc.Curvature:F3} l {arc.LengthM:F1}")));
        }
    }

    /// <summary>Every junction's disc — its centre and the standoff its roads end at — filed by the cell its centre is in.</summary>
    sealed class Discs
    {
        readonly CityPlan.JunctionArrays _junctions;
        readonly Dictionary<(int X, int Y), List<int>> _cells = [];
        readonly float _widestM;

        public Discs(CityPlan.JunctionArrays junctions)
        {
            _junctions = junctions;
            for (var junction = 0; junction < junctions.Count; junction++)
            {
                var cell = Cell(junctions.CentreM[junction]);
                if (!_cells.TryGetValue(cell, out var filed)) _cells[cell] = filed = [];
                filed.Add(junction);
                _widestM = MathF.Max(_widestM, junctions.RadiusM[junction]);
            }
        }

        public bool Inside(Vector2 atM)
        {
            var (least, most) = (Cell(atM - new Vector2(_widestM)), Cell(atM + new Vector2(_widestM)));
            for (var x = least.X; x <= most.X; x++)
            {
                for (var y = least.Y; y <= most.Y; y++)
                {
                    if (!_cells.TryGetValue((x, y), out var filed)) continue;

                    foreach (var junction in filed)
                    {
                        if (Vector2.Distance(atM, _junctions.CentreM[junction]) <= _junctions.RadiusM[junction]) return true;
                    }
                }
            }

            return false;
        }

        static (int X, int Y) Cell(Vector2 atM) => ((int)MathF.Floor(atM.X / CellM), (int)MathF.Floor(atM.Y / CellM));
    }

    /// <summary>Straight lines, each with its heading, filed in a grid of cells under every cell its box touches.</summary>
    sealed class Lines
    {
        public readonly List<Vector2> A = [];
        public readonly List<Vector2> B = [];
        public readonly List<Vector2> Heading = [];
        public readonly List<long> Owner = [];
        public readonly List<byte> Kind = [];
        public double LengthM;
        readonly Dictionary<(int X, int Y), List<int>> _cells = [];

        public int Count => A.Count;

        public void AddLine(ReadOnlySpan<Vector2> pointsM, long owner, byte kind)
        {
            for (var at = 1; at < pointsM.Length; at++) Add(pointsM[at - 1], pointsM[at], owner, kind);
        }

        public void AddArcs(ReadOnlySpan<ArcSeg> arcs, long owner)
        {
            foreach (var arc in arcs)
            {
                var steps = Math.Max(1, (int)MathF.Ceiling(arc.LengthM / StepM));
                for (var step = 0; step < steps; step++)
                {
                    Add(arc.PointAtM(arc.LengthM * step / steps), arc.PointAtM(arc.LengthM * (step + 1) / steps), owner, Lane);
                }
            }
        }

        void Add(Vector2 a, Vector2 b, long owner, byte kind)
        {
            if (Vector2.Distance(a, b) < ShortestM) return;

            var line = A.Count;
            A.Add(a);
            B.Add(b);
            Heading.Add(Vector2.Normalize(b - a));
            Owner.Add(owner);
            Kind.Add(kind);
            LengthM += Vector2.Distance(a, b);
            var (least, most) = (Cell(Vector2.Min(a, b)), Cell(Vector2.Max(a, b)));
            for (var x = least.X; x <= most.X; x++)
            {
                for (var y = least.Y; y <= most.Y; y++)
                {
                    if (!_cells.TryGetValue((x, y), out var filed)) _cells[(x, y)] = filed = [];
                    filed.Add(line);
                }
            }
        }

        /// <summary>
        /// The distance to the nearest line running the same way — or either way, asked so or where the line is
        /// <see cref="Shared"/> — or <see cref="SearchM"/> where none is that near. The cells round a place's own
        /// reach a cell off it every way, so a line found nearer than that is the nearest.
        /// </summary>
        public float NearestM(Vector2 atM, Vector2 heading, bool eitherWay)
        {
            var nearestM = Within(atM, heading, eitherWay, CellM, SearchM);
            return nearestM <= CellM ? nearestM : Within(atM, heading, eitherWay, SearchM, nearestM);
        }

        float Within(Vector2 atM, Vector2 heading, bool eitherWay, float reachM, float nearestM)
        {
            var (least, most) = (Cell(atM - new Vector2(reachM)), Cell(atM + new Vector2(reachM)));
            for (var x = least.X; x <= most.X; x++)
            {
                for (var y = least.Y; y <= most.Y; y++)
                {
                    if (!_cells.TryGetValue((x, y), out var filed)) continue;

                    foreach (var line in filed)
                    {
                        var cos = Vector2.Dot(Heading[line], heading);
                        if (eitherWay || Kind[line] == Shared) cos = MathF.Abs(cos);
                        if (cos >= SameWayCos) nearestM = MathF.Min(nearestM, OffM(atM, A[line], B[line]));
                    }
                }
            }

            return nearestM;
        }

        static (int X, int Y) Cell(Vector2 atM) => ((int)MathF.Floor(atM.X / CellM), (int)MathF.Floor(atM.Y / CellM));

        static float OffM(Vector2 atM, Vector2 a, Vector2 b)
        {
            var runM = b - a;
            var along = Math.Clamp(Vector2.Dot(atM - a, runM) / runM.LengthSquared(), 0f, 1f);
            return Vector2.Distance(atM, a + (runM * along));
        }
    }
}
