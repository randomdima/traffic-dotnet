using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Town;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>What a town's built form came to</b>: how much of the map is town, how thickly it is built from its middle out,
/// how much of each kind of zone its buildings cover, how big its blocks are, how much of its streets nothing fronts,
/// and how many buildings each service serves — the figures a generated town is set beside a traced one by.
/// </summary>
/// <remarks>
/// <b>Every figure here is measured and none is authored</b> (CLAUDE.md, <i>figures flow one way</i>): a coverage or a
/// density is what the generator's lengths and counts came to, and is read here so a change to them can be weighed
/// against a real place (<c>OdesaOsm</c>) rather than by eye. It reads the plan and stands no world up, and gates nothing.
/// </remarks>
internal static class TownForm
{
    /// <summary>The square the map is read in for what is built and what is only street: a city block's order.</summary>
    const float CellM = 100f;

    /// <summary>How wide each ring about the town's middle is, for its density.</summary>
    const float RingM = 500f;

    /// <summary>How far off a street a building may stand and still front it: a front garden and a deep plot.</summary>
    const float FrontReachM = 35f;

    /// <summary>How often a street is asked whether anything fronts it.</summary>
    const float StreetStepM = 10f;

    /// <summary>How finely a block's ring is walked for its area.</summary>
    const float BlockStepM = 5f;

    public static void Run(string map, SimConfig config)
    {
        var plan = Maps.Plan(map, config, ShippedArt.Sizes);
        var buildings = plan.Buildings;
        var hubM = Hub(plan);

        Console.WriteLine($"form — {plan.Name}, seed {plan.Seed}, {plan.WorldSizeM.X / 1000f:F1} x {plan.WorldSizeM.Y / 1000f:F1} km");
        Console.WriteLine($"  buildings        {buildings.Count}, middle at ({hubM.X:F0}, {hubM.Y:F0})");
        Console.WriteLine();

        Extent(plan);
        Console.WriteLine();
        Rings(plan, hubM);
        Console.WriteLine();
        Kinds(plan, config);
        Console.WriteLine();
        Blocks(plan, config);
        Console.WriteLine();
        Fronted(plan);
        Console.WriteLine();
        Services(plan);
    }

    /// <summary>The middle the town thins away from where it says one (GEN-54), else the middle of its buildings.</summary>
    static Vector2 Hub(CityPlan plan)
    {
        var root = TownMap.ZoneArrays.Root;
        if (plan.Zones.Count > 0 && plan.Zones.Own(root, ZoneParam.HubXM) is { } x && plan.Zones.Own(root, ZoneParam.HubYM) is { } y)
        {
            return new Vector2(x, y);
        }

        var sum = Vector2.Zero;
        foreach (var centreM in plan.Buildings.CentreM) sum += centreM;
        return plan.Buildings.Count > 0 ? sum / plan.Buildings.Count : plan.WorldSizeM * 0.5f;
    }

    /// <summary>
    /// <b>How much of the map is town</b>: the squares holding a building, the squares holding a street and no building,
    /// and the squares holding neither — the land a town is not, which a real one is surrounded by.
    /// </summary>
    static void Extent(CityPlan plan)
    {
        var columns = (int)MathF.Ceiling(plan.WorldSizeM.X / CellM);
        var rows = (int)MathF.Ceiling(plan.WorldSizeM.Y / CellM);
        var built = new bool[columns * rows];
        var streeted = new bool[columns * rows];
        foreach (var centreM in plan.Buildings.CentreM)
        {
            if (CellOf(centreM, columns, rows) is var cell and >= 0) built[cell] = true;
        }

        for (var road = 0; road < plan.Roads.Count; road++)
        {
            foreach (var pointM in Along(plan.Roads.SegmentsOf(road), CellM * 0.5f))
            {
                if (CellOf(pointM, columns, rows) is var cell and >= 0) streeted[cell] = true;
            }
        }

        var (builtCells, bareCells) = (0, 0);
        for (var cell = 0; cell < built.Length; cell++)
        {
            if (built[cell]) builtCells++;
            else if (streeted[cell]) bareCells++;
        }

        var cellKm2 = CellM * CellM / 1e6f;
        var totalKm2 = built.Length * cellKm2;
        Console.WriteLine($"  extent           {totalKm2:F1} km² in {CellM:F0} m squares");
        Console.WriteLine($"    built          {builtCells * cellKm2,7:F1} km²  {builtCells * 100f / built.Length,5:F1}%");
        Console.WriteLine($"    street, bare   {bareCells * cellKm2,7:F1} km²  {bareCells * 100f / built.Length,5:F1}%");
        Console.WriteLine($"    neither        {(built.Length - builtCells - bareCells) * cellKm2,7:F1} km²  " +
                          $"{(built.Length - builtCells - bareCells) * 100f / built.Length,5:F1}%");
    }

    /// <summary>
    /// <b>How thickly the town is built from its middle out</b>: buildings per km² and the share of the ground their
    /// footprints cover, ring by ring — a real town's falls steeply from its centre.
    /// </summary>
    static void Rings(CityPlan plan, Vector2 hubM)
    {
        var farthestM = MathF.Max(
            MathF.Max(Vector2.Distance(hubM, Vector2.Zero), Vector2.Distance(hubM, plan.WorldSizeM)),
            MathF.Max(Vector2.Distance(hubM, new Vector2(plan.WorldSizeM.X, 0f)), Vector2.Distance(hubM, new Vector2(0f, plan.WorldSizeM.Y))));
        var count = (int)MathF.Ceiling(farthestM / RingM);
        var areaM2 = new double[count];
        var stepM = CellM * 0.5f;
        for (var y = stepM * 0.5f; y < plan.WorldSizeM.Y; y += stepM)
        {
            for (var x = stepM * 0.5f; x < plan.WorldSizeM.X; x += stepM)
            {
                areaM2[Math.Min(count - 1, (int)(Vector2.Distance(hubM, new Vector2(x, y)) / RingM))] += stepM * stepM;
            }
        }

        var (inRing, coverM2) = (new int[count], new double[count]);
        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var ring = Math.Min(count - 1, (int)(Vector2.Distance(hubM, plan.Buildings.CentreM[building]) / RingM));
            inRing[ring]++;
            coverM2[ring] += plan.Buildings.SizeM[building].X * plan.Buildings.SizeM[building].Y;
        }

        Console.WriteLine($"  from the middle  {"ring",-12}{"bldg/km²",10}{"covered",9}");
        for (var ring = 0; ring < count; ring++)
        {
            if (areaM2[ring] <= 0d) continue;

            Console.WriteLine($"                   {$"{ring * RingM / 1000f:F1}–{(ring + 1) * RingM / 1000f:F1} km",-12}" +
                              $"{inRing[ring] / (areaM2[ring] / 1e6),10:F0}{coverM2[ring] / areaM2[ring] * 100d,8:F1}%");
        }
    }

    /// <summary>
    /// <b>How much of each kind of zone its buildings cover</b>, by the zone each building's middle stands in: the kind's
    /// whole area, how many buildings, and the share of the area under them.
    /// </summary>
    static void Kinds(CityPlan plan, SimConfig config)
    {
        var tree = new ZoneTree(plan.Zones, config);
        var kinds = Enum.GetValues<ZoneKind>();
        var (areaM2, inKind, coverM2) = (new double[kinds.Length], new int[kinds.Length], new double[kinds.Length]);

        // Sampled by the same question a building is asked, so a zone nested in another counts as its own and once.
        var stepM = CellM * 0.25f;
        for (var y = stepM * 0.5f; y < plan.WorldSizeM.Y; y += stepM)
        {
            for (var x = stepM * 0.5f; x < plan.WorldSizeM.X; x += stepM) areaM2[(int)plan.Zones.Kind[tree.At(new Vector2(x, y))]] += stepM * stepM;
        }

        for (var building = 0; building < plan.Buildings.Count; building++)
        {
            var kind = (int)plan.Zones.Kind[tree.At(plan.Buildings.CentreM[building])];
            inKind[kind]++;
            coverM2[kind] += plan.Buildings.SizeM[building].X * plan.Buildings.SizeM[building].Y;
        }

        Console.WriteLine($"  by zone kind     {"kind",-12}{"km²",8}{"bldg",8}{"bldg/km²",10}{"covered",9}");
        for (var kind = 0; kind < kinds.Length; kind++)
        {
            if (areaM2[kind] <= 1d && inKind[kind] == 0) continue;

            var km2 = areaM2[kind] / 1e6;
            Console.WriteLine($"                   {kinds[kind],-12}{km2,8:F2}{inKind[kind],8}" +
                              $"{(km2 > 0 ? inKind[kind] / km2 : 0),10:F0}{(areaM2[kind] > 0 ? coverM2[kind] / areaM2[kind] * 100d : 0),8:F1}%");
        }
    }

    /// <summary>
    /// <b>How big the blocks are</b>: every closed ring of the walk's outer face that turns the way a block's does, its
    /// area and the side of a square that big — and how many hold no building.
    /// </summary>
    static void Blocks(CityPlan plan, SimConfig config)
    {
        var areasM2 = new List<float>();
        var empty = 0;
        foreach (var face in plan.Paving(config).Rings(config).WalkEdge)
        {
            var points = Along(face, BlockStepM);
            if (points.Count < 3) continue;

            var signedM2 = 0f;
            for (int at = 0, before = points.Count - 1; at < points.Count; before = at++)
            {
                signedM2 += (points[before].X * points[at].Y) - (points[at].X * points[before].Y);
            }

            // A block's face runs round it the way the outside of the town's runs the other.
            if (signedM2 >= 0f) continue;

            areasM2.Add(-signedM2 * 0.5f);
            if (!HoldsABuilding(plan, points)) empty++;
        }

        areasM2.Sort();
        if (areasM2.Count == 0)
        {
            Console.WriteLine("  blocks           none");
            return;
        }

        Console.WriteLine($"  blocks           {areasM2.Count}, {empty} holding no building");
        Console.WriteLine($"    area           p10 {Percentile(areasM2, 10) / 1e4f:F2} ha, median {Percentile(areasM2, 50) / 1e4f:F2} ha, " +
                          $"p90 {Percentile(areasM2, 90) / 1e4f:F2} ha");
        Console.WriteLine($"    as a square    p10 {MathF.Sqrt(Percentile(areasM2, 10)):F0} m, median {MathF.Sqrt(Percentile(areasM2, 50)):F0} m, " +
                          $"p90 {MathF.Sqrt(Percentile(areasM2, 90)):F0} m");
    }

    static bool HoldsABuilding(CityPlan plan, List<Vector2> ring)
    {
        var (leastM, mostM) = (ring[0], ring[0]);
        foreach (var pointM in ring) (leastM, mostM) = (Vector2.Min(leastM, pointM), Vector2.Max(mostM, pointM));

        foreach (var centreM in plan.Buildings.CentreM)
        {
            if (centreM.X < leastM.X || centreM.Y < leastM.Y || centreM.X > mostM.X || centreM.Y > mostM.Y) continue;

            var inside = false;
            for (int at = 0, before = ring.Count - 1; at < ring.Count; before = at++)
            {
                if ((ring[at].Y > centreM.Y) != (ring[before].Y > centreM.Y) &&
                    centreM.X < ring[at].X + ((ring[before].X - ring[at].X) * (centreM.Y - ring[at].Y) / (ring[before].Y - ring[at].Y)))
                {
                    inside = !inside;
                }
            }

            if (inside) return true;
        }

        return false;
    }

    /// <summary>
    /// <b>How much of each class of road something fronts</b>: the share of its length with a building's middle within
    /// <see cref="FrontReachM"/> of the road's — a street nothing stands along is a street laid for nobody.
    /// </summary>
    static void Fronted(CityPlan plan)
    {
        var cells = new Dictionary<(int, int), List<Vector2>>();
        foreach (var centreM in plan.Buildings.CentreM)
        {
            var key = ((int)MathF.Floor(centreM.X / FrontReachM), (int)MathF.Floor(centreM.Y / FrontReachM));
            if (!cells.TryGetValue(key, out var list)) cells[key] = list = [];
            list.Add(centreM);
        }

        var classes = Enum.GetValues<RoadClass>();
        var (lengthM, frontedM) = (new double[classes.Length], new double[classes.Length]);
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.IsABay(road)) continue;

            var roadClass = plan.Roads.Class.Length > 0 ? (int)plan.Roads.Class[road] : 0;
            foreach (var pointM in Along(plan.Roads.SegmentsOf(road), StreetStepM))
            {
                lengthM[roadClass] += StreetStepM;
                if (Near(cells, pointM)) frontedM[roadClass] += StreetStepM;
            }
        }

        Console.WriteLine($"  fronted within {FrontReachM:F0} m  {"class",-12}{"km",8}{"fronted",9}");
        for (var roadClass = 0; roadClass < classes.Length; roadClass++)
        {
            if (lengthM[roadClass] <= 0d) continue;

            Console.WriteLine($"                   {classes[roadClass],-12}{lengthM[roadClass] / 1000d,8:F1}" +
                              $"{frontedM[roadClass] / lengthM[roadClass] * 100d,8:F1}%");
        }
    }

    static bool Near(Dictionary<(int, int), List<Vector2>> cells, Vector2 pointM)
    {
        var (column, row) = ((int)MathF.Floor(pointM.X / FrontReachM), (int)MathF.Floor(pointM.Y / FrontReachM));
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (!cells.TryGetValue((column + dx, row + dy), out var list)) continue;

                foreach (var centreM in list)
                {
                    if (Vector2.DistanceSquared(centreM, pointM) <= FrontReachM * FrontReachM) return true;
                }
            }
        }

        return false;
    }

    /// <summary><b>How many buildings each service serves</b>: a real town has one hospital to tens of thousands.</summary>
    static void Services(CityPlan plan)
    {
        var uses = new int[Enum.GetValues<BuildingUse>().Length];
        foreach (var use in plan.Buildings.Use) uses[(int)use]++;

        Console.WriteLine($"  services         {"use",-14}{"count",7}{"bldg each",11}");
        foreach (var use in ServiceBuildings.Uses)
        {
            var count = uses[(int)use];
            Console.WriteLine($"                   {use,-14}{count,7}{(count > 0 ? plan.Buildings.Count / count : 0),11}");
        }
    }

    static List<Vector2> Along(ReadOnlySpan<ArcSeg> arcs, float stepM)
    {
        var points = new List<Vector2>();
        var totalM = Spline.TotalLengthM(arcs);
        for (var atM = stepM * 0.5f; atM < totalM; atM += stepM) points.Add(Spline.SampleAt(arcs, atM).PositionM);
        return points;
    }

    static int CellOf(Vector2 pointM, int columns, int rows)
    {
        var (column, row) = ((int)(pointM.X / CellM), (int)(pointM.Y / CellM));
        return column < 0 || row < 0 || column >= columns || row >= rows ? -1 : (row * columns) + column;
    }

    static float Percentile(List<float> sorted, float which) => sorted[Math.Clamp((int)(which / 100f * (sorted.Count - 1)), 0, sorted.Count - 1)];
}
