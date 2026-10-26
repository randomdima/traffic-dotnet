using System.Numerics;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>A generated town laid off its brief</b> (GEN-60, GEN-61), in its three stages and each once: the terrain
/// (<see cref="TerrainStage"/>), the connectors (<see cref="Connectors"/>), and the town — its land cut into districts
/// (<see cref="Partition"/>), the roads between them, the streets inside them, and what the rest of the plan lays off those.
/// </summary>
/// <remarks>
/// <para>
/// <b>Authored, then laid</b>. What a brief says of the ground — the water, the railway, the districts and what each is
/// built as — is drawn first into a map in memory (<see cref="Author"/>), so the town is laid off it as every town is
/// (<see cref="TownPlan"/>, GEN-58) and nothing downstream asks which kind of map it was; and the streets are laid off what
/// was authored (<see cref="Lay"/>).
/// </para>
/// <para>
/// <b>Every node the town will have is placed before its first road is laid</b> (GEN-10, GEN-16): the intercity roads,
/// the district roads along the edges districts share and the links across the barriers put theirs down, then each
/// district's lattice its own and the stubs it hangs by — and only once those are settled is a road drawn, the intercity
/// roads first, so a district road or a street offered against ground one holds is the one refused (GEN-49).
/// </para>
/// </remarks>
internal static class TownGen
{
    /// <summary>
    /// <b>The ground a road of this many lanes each way keeps to itself</b>: half its carriageway, its walk, and the corner
    /// a street turning off it needs — which is the ground a junction with it stands on.
    /// </summary>
    public static float CorridorM(SimConfig config, int lanes) =>
        (config.LaneWidthM * lanes * SimConfig.LanesPerCarriageway * 0.5f) + config.PavementWidthM + config.IntersectionCornerRadiusM;

    /// <summary>What the water and the railway let a road do (<see cref="WaterRules"/>), over this ground.</summary>
    public static WaterRules RulesOf(GroundShapes ground, CityPlan.RailArrays rails, Func<Vector2, bool> sea, SimConfig config) =>
        new(ground, config.Terrain.GroundStepM, sea, config.CityGen.BridgeDeckLongestM, CorridorM(config, 1),
            (config.RoadWidthM * 0.5f) + config.PavementWidthM, rails, CorridorM(config, 1) + config.Rail.JunctionClearanceM,
            Trig.Cos(config.Rail.CrossingSkewMostDeg * MathF.PI / 180f));

    /// <summary>
    /// <b>What a brief's town is, before its streets</b>: its brief and the seeds each stage drew on, the water, the
    /// connectors, the land cut into districts and what each is like, and the map in memory the rest is laid off.
    /// </summary>
    internal sealed record Authored(
        TownBrief Brief, StageSeeds Seeds, TerrainStage.Water Water, Connectors.Laid Connectors, GroundShapes Ground,
        Partition Partition, Districts Districts, TownMap Map);

    /// <summary>The streets a brief's town came out with, its car parks among them, and its districts.</summary>
    internal sealed record Laid(RoadStage.Laid Roads, CarParks.Laid CarParks, DistrictMap Districts);

    public static Authored Author(TownBrief brief, SimConfig config)
    {
        brief.Check();
        var seeds = brief.Seeds.Resolved;
        var extentM = new Vector2(brief.WidthM, brief.HeightM);

        var terrain = new Rng(seeds.Terrain, Streams.Terrain);
        var water = TerrainStage.Lay(brief, config, ref terrain);
        var connectors = Connectors.Lay(brief, water, config, seeds.Connectors, water.InTheSea);
        var ground = new GroundShapes(GroundPieces.None(seeds.Terrain, extentM, config.PavementWidthM).With(water.Rings).With(connectors.Rails), config);

        var grid = CellGrid.Over(extentM, config.CityGen.DistrictCellM);
        var partition = Partition.Cut(grid, Barriers(grid, ground, water, connectors, config), config, seeds.Districts);
        var industrial = partition.EdgeShares(static barrier => barrier is Barrier.Rail or Barrier.Road);
        var districts = Districts.Plan(partition, industrial, connectors.CoreM, brief.Districts.GridShare, config, seeds.Districts);

        var map = TownMap.Bare(brief.Name, brief.Description, seeds.Streets, extentM, ZoneKind.Town, []) with
        {
            Courses = water.Courses,
            Rails = connectors.Tracks,
            Zones = Zones(brief, partition, districts, connectors.CoreM, extentM),
        };
        return new Authored(brief, seeds, water, connectors, ground, partition, districts, map);
    }

    /// <summary>
    /// <b>Which barrier each cell is</b> (GEN-64): the water and the sea, and as far off their shore as a street keeps
    /// (GEN-71); the railway, as far off its ballast as a junction keeps; each intercity road, as far off it as a street
    /// keeps (GEN-68); and the margin round the map's edge — so what is left is the ground a district's streets stand on.
    /// </summary>
    static Barrier[] Barriers(CellGrid grid, GroundShapes ground, TerrainStage.Water water, Connectors.Laid connectors, SimConfig config)
    {
        var barrier = new Barrier[grid.Count];
        var streetM = CorridorM(config, 1);
        var offWaterM = new float[grid.Count];
        var open = new PriorityQueue<int, float>();
        for (var cell = 0; cell < grid.Count; cell++)
        {
            offWaterM[cell] = float.PositiveInfinity;
            var atM = grid.CentreOf(cell);
            if (!ground.Waterside(atM)) continue;

            barrier[cell] = water.InTheSea(atM) ? Barrier.Sea : Barrier.Water;
            offWaterM[cell] = 0f;
            open.Enqueue(cell, 0f);
        }

        var clearM = config.CityGen.WaterfrontClearM + streetM;
        while (open.TryDequeue(out var cell, out var atM))
        {
            if (atM > offWaterM[cell]) continue;

            var (column, row) = (cell % grid.Columns, cell / grid.Columns);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if ((dx | dy) == 0 || !grid.Holds(column + dx, row + dy)) continue;

                    var next = grid.Index(column + dx, row + dy);
                    var nextM = atM + (grid.CellM * ((dx != 0 && dy != 0) ? MathF.Sqrt(2f) : 1f));
                    if (nextM > clearM || nextM >= offWaterM[next]) continue;

                    offWaterM[next] = nextM;
                    if (barrier[next] == Barrier.None) barrier[next] = Barrier.Water;
                    open.Enqueue(next, nextM);
                }
            }
        }

        var railM = streetM + config.Rail.JunctionClearanceM;
        if (connectors.Rails.Count > 0)
        {
            for (var cell = 0; cell < grid.Count; cell++)
            {
                if (barrier[cell] == Barrier.None && ground.RailWithin(grid.CentreOf(cell), railM)) barrier[cell] = Barrier.Rail;
            }
        }

        foreach (var road in connectors.Roads)
        {
            var halfM = CorridorM(config, road.Lanes) + streetM;
            for (var point = 0; point + 1 < road.Line.PointsM.Length; point++)
            {
                grid.Band(road.Line.PointsM[point], road.Line.PointsM[point + 1], halfM, cell =>
                {
                    if (barrier[cell] is Barrier.None or Barrier.Rail) barrier[cell] = Barrier.Road;
                });
            }
        }

        var (columnsIn, rowsIn) = ((int)MathF.Ceiling(streetM / grid.CellM), (int)MathF.Ceiling(streetM / grid.CellM));
        for (var row = 0; row < grid.Rows; row++)
        {
            for (var column = 0; column < grid.Columns; column++)
            {
                if (column >= columnsIn && row >= rowsIn && column < grid.Columns - columnsIn && row < grid.Rows - rowsIn) continue;

                var cell = grid.Index(column, row);
                if (barrier[cell] == Barrier.None) barrier[cell] = Barrier.Edge;
            }
        }

        return barrier;
    }

    /// <summary>
    /// <b>What the town is zoned for</b> (GEN-58): the whole map, saying the town's counts and its core as the hub its
    /// buildings thin away from (GEN-54), and a zone for each district — its outline, its kind (GEN-74) and the bearing its
    /// buildings behind the frontage stand square to.
    /// </summary>
    static TownMap.ZoneArrays Zones(TownBrief brief, Partition partition, Districts districts, Vector2 coreM, Vector2 extentM)
    {
        var zones = new TownMap.ZoneArrays.Builder();
        zones.Add(
            -1, ZoneKind.Town,
            [
                (ZoneParam.People, brief.Roster.People), (ZoneParam.Cars, brief.Roster.Cars), (ZoneParam.Buildings, brief.Roster.Buildings),
                (ZoneParam.UnregulatedShare, brief.Streets.UnregulatedShare), (ZoneParam.HubXM, coreM.X), (ZoneParam.HubYM, coreM.Y),
            ],
            [TownMap.WholeOutline(extentM)]);

        for (var district = 0; district < partition.Count; district++)
        {
            var outline = Polyline.Thinned(partition.Outline(district), partition.Grid.CellM * 0.5f);
            if (outline.Count > 1 && outline[0] == outline[^1]) outline.RemoveAt(outline.Count - 1);
            if (outline.Count < 3) continue;

            zones.Add(
                TownMap.ZoneArrays.Root, districts[district].Kind,
                [(ZoneParam.BearingDeg, districts[district].BearingRad * 180f / MathF.PI)],
                [[.. outline]]);
        }

        return zones.Arrays();
    }

    /// <summary>
    /// <b>The town's streets, laid off what was authored</b>: the roads between districts and to outside, the streets
    /// inside each district, settled, joined, pruned, opened out and directed — and the car parks and the services' yards
    /// off what that leaves — each stage once and in the one order they can run in (GEN-10).
    /// </summary>
    /// <param name="took">Told each stage's name as it finishes.</param>
    /// <param name="joined">Handed the layout as every road was offered and before any was deleted, for a sketch of it.</param>
    public static Laid Lay(Authored town, SimConfig config, BuildingSizes sizes, Action<string> took, Action<TownLayout, Paths>? joined = null)
    {
        var extentM = town.Map.SizeM;
        var figures = config.CityGen;
        var seed = town.Seeds.Streets;
        var rules = RulesOf(town.Ground, town.Connectors.Rails, town.Water.InTheSea, config);
        var nodeSizes = new NodeSizes(extentM, config);
        var lines = new RoadLines(seed, config, town.Districts.BlockTightestM, extentM, rules, nodeSizes);
        var shortestRoadM = CorridorM(config, 1) * 2f;
        var layout = new TownLayout(shortestRoadM, config.ArmsApartMinRad, figures.LocalityM, config.Grid.Main, rules, lines, nodeSizes);
        var paths = new Paths(layout, config);

        var intercity = Intercities(town, paths);
        var links = new TownLinks(town, paths, intercity, config);
        links.Lay();
        took("paths");

        var corridors = Lattice.Corridors(town.Partition, paths);
        var streets = Lattice.Place(layout, town.Partition, town.Districts, paths, corridors, town.Ground, config, seed);
        took("nodes");

        var moved = layout.SettleTheNodes();
        paths.TheNodesMoved(moved);
        took("settle");
        paths.Close();
        links.JoinWhatWasLeft(layout);
        Lattice.Lay(layout, streets, moved);
        took("streets");

        joined?.Invoke(layout, paths);
        layout.KeepTheLargestComponent();
        layout.PruneTheDeadEnds();
        took("prune");
        ThroughRoads.Lay(layout);
        took("through");
        Roundabouts.Lay(layout, town.Districts.BlockTightestM, rules, config, extentM, CorridorM(config, 1));
        took("roundabouts");
        OneWayStreets.Lay(layout, config);
        took("one-way");

        var districtMap = new DistrictMap(
            town.Partition.Label, town.Partition.Grid.CellM, town.Partition.Grid.Columns, town.Partition.Grid.Rows, town.Partition.CentreM,
            WithStreets(layout, town.Partition));
        var carPark = new Rng(seed, Streams.CarPark);
        var carParks = CarParks.Lay(layout, town.Brief.Roster.Buildings, config, districtMap, sizes, ref carPark);
        took("car parks");

        var signals = new Rng(seed, Streams.Signal);
        var roads = RoadStage.Lay(layout, town.Brief.Streets.UnregulatedShare, config, carParks, ref signals);
        took("roads");

        return new Laid(roads, carParks, districtMap);
    }

    /// <summary>Which districts a street stands in, by the middle of each street the layout kept (<see cref="DistrictMap.Built"/>).</summary>
    static bool[] WithStreets(TownLayout layout, Partition partition)
    {
        var built = new bool[partition.Count];
        for (var road = 0; road < layout.Edges.Count; road++)
        {
            if (layout.Edges[road].Class != RoadClass.Street) continue;

            var line = layout.LineOf(road);
            var district = partition.At(Spline.SampleAt(line, Spline.TotalLengthM(line) * 0.5f).PositionM);
            if (district >= 0) built[district] = true;
        }

        return built;
    }

    /// <summary>
    /// <b>The intercity roads, laid first</b> (GEN-63): each its route, the crossings it takes, and its far end put on the
    /// road it meets where it ends on another.
    /// </summary>
    static int[] Intercities(Authored town, Paths paths)
    {
        var roads = town.Connectors.Roads;
        var path = new int[roads.Count];
        for (var road = 0; road < roads.Count; road++)
        {
            var line = roads[road].Line;
            path[road] = paths.Add(line, RoadClass.Intercity, roads[road].Lanes, CrossingsOn(line, town.Connectors));
            if (roads[road].EndsOn < 0) continue;

            var onto = path[roads[road].EndsOn];
            paths.Share(onto, paths.All[onto].Line.Nearest(line.LastM).AlongM, paths.EndNode(path[road], last: true));
        }

        return path;
    }

    /// <summary>The crossings a line takes: every one both of whose ends it passes through.</summary>
    public static List<Crossing> CrossingsOn(Polyline line, Connectors.Laid connectors)
    {
        var on = new List<Crossing>();
        foreach (var crossing in (IEnumerable<Crossing>)[.. connectors.Bridges, .. connectors.LevelCrossings])
        {
            if (line.Nearest(crossing.NearM).AwayM < 0.5f && line.Nearest(crossing.FarM).AwayM < 0.5f) on.Add(crossing);
        }

        return on;
    }
}
