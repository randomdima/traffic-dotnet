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

    /// <summary>
    /// <b>How far either side of a road a span carried over it reaches</b> (GEN-68): past the corridor of the road under it,
    /// and as far again as a junction's box — which its end is where anything but the road carrying it on meets it there
    /// (GEN-14a) — so the node where it comes down stands clear of the road it crosses and is read as no junction with it.
    /// Asked of the widest roads both, either being met over any intercity road.
    /// </summary>
    public static float OverHalfSpanM(SimConfig config) =>
        CorridorM(config, LayoutEdge.MostLanes)
        + (2f * config.JunctionRadiusAcrossM(config.LaneWidthM * LayoutEdge.MostLanes * SimConfig.LanesPerCarriageway));

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
        // A town that plans no building has no edge to thin out to: it is every district, its roads through its core.
        var graded = brief.Districts.Country && brief.Roster.Buildings > 0;

        var terrain = new Rng(seeds.Terrain, Streams.Terrain);
        var water = TerrainStage.Lay(brief, config, ref terrain);
        var connectors = Connectors.Lay(brief, water, config, seeds.Connectors, water.InTheSea, graded);
        var ground = new GroundShapes(GroundPieces.None(seeds.Terrain, extentM, config.PavementWidthM).With(water.Rings).With(connectors.Rails), config);

        var grid = CellGrid.Over(extentM, config.CityGen.DistrictCellM);
        var land = Partition.Cut(grid, Barriers(grid, ground, water, connectors, config), config, seeds.Districts);
        var partition = land;
        var country = new List<(ZoneKind Kind, List<Vector2> Outline)>();
        float[]? fill = null;

        // <b>The town grades out to the country</b> (GEN-76): some of the rim left to it, the rest cut on its own so the old
        // town and the edge are the town's and not the map's, and each district filled less the further out it is.
        if (graded)
        {
            var roadsIn = connectors.Roads.Select(road => road.Line).ToList();
            var furthestM = TownExtent.FurthestM(land, connectors.CoreM);
            var built = TownExtent.Built(land, connectors.CoreM, furthestM, roadsIn, config, seeds.Districts);
            for (var district = 0; district < land.Count; district++)
            {
                if (!built[district]) country.Add((TownExtent.Country(land, district, seeds.Districts, config), land.Outline(district)));
            }

            if (country.Count > 0) partition = land.Only(built);
            fill = TownExtent.Fill(partition, connectors.CoreM, furthestM, roadsIn, config, seeds.Districts);
        }

        var districts = Planned(partition, connectors.CoreM, brief, config, seeds.Districts, fill);

        var map = TownMap.Bare(brief.Name, brief.Description, seeds.Streets, extentM, ZoneKind.Town, []) with
        {
            Courses = water.Courses,
            Rails = connectors.Tracks,
            Zones = Zones(brief, partition, districts, fill, country, connectors.CoreM, extentM, config),
        };
        return new Authored(brief, seeds, water, connectors, ground, partition, districts, map);
    }

    /// <summary>What each district of a cut is like (GEN-74), off what borders it, how far it stands from the core and how full it is built.</summary>
    static Districts Planned(Partition partition, Vector2 coreM, TownBrief brief, SimConfig config, ulong seed, float[]? fill)
    {
        var industrial = partition.EdgeShares(static barrier => barrier is Barrier.Rail or Barrier.Road);
        return Districts.Plan(partition, industrial, coreM, brief.Districts.GridShare, config, seed, fill);
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

            barrier[cell] = Barrier.Water;
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

        // A bypassed town's intercity roads keep their interchanges' slip roads in their verge (GEN-68), so the district roads
        // running along their edge stand clear of the slips.
        var verges = connectors.BypassM > 0f ? config.CityGen.InterchangeOffsetM + CorridorM(config, 1) : CorridorM(config, LayoutEdge.MostLanes);
        foreach (var road in connectors.Roads)
        {
            var halfM = verges + streetM;
            for (var point = 0; point + 1 < road.Line.PointsM.Length; point++)
            {
                grid.Band(road.Line.PointsM[point], road.Line.PointsM[point + 1], halfM, cell =>
                {
                    if (barrier[cell] is Barrier.None or Barrier.Rail) barrier[cell] = Barrier.Road;
                });
            }
        }

        var marginCells = (int)MathF.Ceiling(streetM / grid.CellM);
        for (var row = 0; row < grid.Rows; row++)
        {
            for (var column = 0; column < grid.Columns; column++)
            {
                if (column >= marginCells && row >= marginCells && column < grid.Columns - marginCells && row < grid.Rows - marginCells) continue;

                var cell = grid.Index(column, row);
                if (barrier[cell] == Barrier.None) barrier[cell] = Barrier.Edge;
            }
        }

        return barrier;
    }

    /// <summary>
    /// <b>What the town is zoned for</b> (GEN-58): the whole map, saying the town's counts and its core as the hub its
    /// buildings thin away from (GEN-54), and a zone for each district — its outline, its kind (GEN-74), the bearing its
    /// buildings behind the frontage stand square to and, in a town graded out to the country, how much of its kind's
    /// frontage and ground behind it builds — and a zone for each piece of the country round it (GEN-76).
    /// </summary>
    /// <param name="fill">How much of what its kind builds each district builds (<see cref="TownExtent.Fill"/>); none builds all of it.</param>
    static TownMap.ZoneArrays Zones(
        TownBrief brief, Partition partition, Districts districts, float[]? fill, List<(ZoneKind Kind, List<Vector2> Outline)> country,
        Vector2 coreM, Vector2 extentM, SimConfig config)
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

            var kind = districts[district].Kind;
            var bearing = (ZoneParam.BearingDeg, districts[district].BearingRad * 180f / MathF.PI);
            // What its widened blocks (Districts.Plan) have not thinned of its frontage, its gaps do; its ground behind is
            // as large as its blocks are, and thins by the whole of its fill.
            var defaults = ZoneTree.DefaultsOf(kind, config.Zones);
            var fronted = fill is null ? 1f : MathF.Pow(fill[district], 1f - config.CityGen.TownSparseBlockShare);
            zones.Add(
                TownMap.ZoneArrays.Root, kind,
                fill is null
                    ? [bearing]
                    : [bearing, (ZoneParam.Frontage, defaults.Frontage * fronted), (ZoneParam.Interior, defaults.Interior * fill[district])],
                [[.. outline]]);
        }

        foreach (var (kind, raw) in country)
        {
            var outline = Polyline.Thinned(raw, partition.Grid.CellM * 0.5f);
            if (outline.Count > 1 && outline[0] == outline[^1]) outline.RemoveAt(outline.Count - 1);
            if (outline.Count >= 3) zones.Add(TownMap.ZoneArrays.Root, kind, [], [[.. outline]]);
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
        var layout = new TownLayout(
            shortestRoadM, config.ArmsApartMinRad, figures.LocalityM, config.Grid.Main, rules, lines, nodeSizes, config.SlipArmsApartMinRad);
        var paths = new Paths(layout, config);

        var intercity = Intercities(town, paths, config);
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
        var settled = Lattice.Settled(streets, moved);
        links.JoinWhatWasLeft(layout, settled);
        Lattice.Lay(layout, settled);
        took("streets");

        joined?.Invoke(layout, paths);
        layout.KeepTheLargestComponent();
        layout.PruneTheDeadEnds();
        took("prune");
        ThroughRoads.Lay(layout, config);
        took("through");
        Roundabouts.Lay(layout, town.Districts.BlockTightestM, rules, config, extentM, CorridorM(config, 1));
        took("roundabouts");
        OneWayStreets.Lay(layout, config);
        took("one-way");

        var districtMap = new DistrictMap(
            town.Partition.Label, town.Partition.Grid.CellM, town.Partition.Grid.Columns, town.Partition.Grid.Rows, town.Partition.CentreM,
            WithStreets(layout, town.Partition));
        var carPark = new Rng(seed, Streams.CarPark);
        var carParks = CarParks.Lay(
            layout, town.Brief.Roster.Buildings, config, districtMap, sizes, extentM,
            atM => town.Districts.At(atM) is var district && (district < 0 || town.Districts[district].Kind != ZoneKind.Suburb), ref carPark);
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
    /// road it meets where it ends on another. <b>In a bypassed town nothing on them stops</b> (GEN-68): each railway is
    /// crossed on a span carried over it.
    /// </summary>
    static int[] Intercities(Authored town, Paths paths, SimConfig config)
    {
        var roads = town.Connectors.Roads;
        var path = new int[roads.Count];
        for (var road = 0; road < roads.Count; road++)
        {
            var line = roads[road].Line;
            path[road] = paths.Add(line, RoadClass.Intercity, LayoutEdge.MostLanes, roads[road].Crossed, overRails: town.Connectors.BypassM > 0f);

            // The road it is carried over stands no node under it, nor where one settled would draw its end down (GEN-16).
            foreach (var crossing in roads[road].Crossed)
            {
                if (!crossing.Over) continue;

                var keepM = OverHalfSpanM(config) + config.CityGen.LocalityM;
                paths.Keep(path[crossing.Barrier], crossing.AlongM - keepM, crossing.AlongM + keepM);
            }
            if (roads[road].EndsOn < 0) continue;

            var onto = path[roads[road].EndsOn];
            paths.Share(onto, paths.All[onto].Line.Nearest(line.LastM).AlongM, paths.EndNode(path[road], last: true));
        }

        return path;
    }
}
