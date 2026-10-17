using System.Numerics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>Where a town's roads stand on each other with nothing joining them</b>: two roads on one level whose lines cross
/// away from every junction either reaches, and two whose carriageways run over each other along a stretch — a way drawn
/// twice, a crossing mapped with no node, or a dual carriageway's two halves drawn into each other.
/// </summary>
/// <remarks>
/// <b>Quoted and gating nothing</b>: what is listed is where to look. A road's line is read at <see cref="StepM"/>, and a
/// place inside the disc of a junction the road ends at is the junction's and never a fault of the road.
/// </remarks>
internal static class RoadFaults
{
    /// <summary>How finely a road's line is read.</summary>
    const float StepM = 2f;

    /// <summary>How long two carriageways must run over each other before the stretch is listed: two cars' length.</summary>
    const float OverlapLongestM = 10f;

    const int Listed = 30;

    public static void Run(string map, SimConfig config)
    {
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        var roads = plan.Roads;
        var junctions = plan.Junctions;

        var widestM = 0f;
        for (var road = 0; road < roads.Count; road++) widestM = MathF.Max(widestM, roads.WidthM[road]);

        // Every road's line as places a step apart, each filed in a cell as wide as the widest road.
        var cellM = MathF.Max(widestM, StepM * 2f);
        var points = new List<(int Road, Vector2 AtM, Vector2 Along)>();
        var cells = new Dictionary<(int X, int Y), List<int>>();
        for (var road = 0; road < roads.Count; road++)
        {
            if (roads.IsABay(road)) continue;

            var line = roads.SegmentsOf(road);
            var lengthM = Spline.TotalLengthM(line);
            for (var atM = 0f; atM <= lengthM; atM += StepM)
            {
                var at = Spline.SampleAt(line, atM);
                if (InItsBox(road, at.PositionM)) continue;

                var cell = Cell(at.PositionM);
                if (!cells.TryGetValue(cell, out var filed)) cells[cell] = filed = [];
                filed.Add(points.Count);
                points.Add((road, at.PositionM, at.Direction));
            }
        }

        // Pairs of roads whose places stand within their half-widths of each other, outside both their boxes: each place
        // of the first counted once for each road it stands over.
        var near = new Dictionary<(int One, int Other), (int Across, int Along, Vector2 AtM)>();
        var counted = new HashSet<int>();
        for (var at = 0; at < points.Count; at++)
        {
            var (road, pointM, along) = points[at];
            var (x, y) = Cell(pointM);
            counted.Clear();
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (!cells.TryGetValue((x + dx, y + dy), out var filed)) continue;

                    foreach (var other in filed)
                    {
                        var (otherRoad, otherM, otherAlong) = points[other];
                        if (otherRoad <= road || roads.LevelOf(road) != roads.LevelOf(otherRoad)) continue;
                        if (SharesAJunction(road, otherRoad, pointM)) continue;

                        var apartM = Vector2.Distance(pointM, otherM);
                        if (apartM > (roads.WidthM[road] + roads.WidthM[otherRoad]) * 0.5f) continue;
                        if (InItsBox(otherRoad, pointM) || InItsBox(road, otherM) || !counted.Add(otherRoad)) continue;

                        var key = (road, otherRoad);
                        var (across, alongCount, firstM) = near.GetValueOrDefault(key, (0, 0, pointM));
                        if (MathF.Abs(Vector2.Dot(along, otherAlong)) > MathF.Cos(MathF.PI / 6f)) alongCount++;
                        else across++;
                        near[key] = (across, alongCount, firstM);
                    }
                }
            }
        }

        var crossings = new List<(float Score, string Row)>();
        var overlaps = new List<(float Score, string Row)>();
        foreach (var ((one, other), (across, along, atM)) in near)
        {
            var row = $"  {atM.X,7:F0}{atM.Y,8:F0}  {one,6} {Describe(one)}   {other,6} {Describe(other)}";
            if (along * StepM >= OverlapLongestM) overlaps.Add((along * StepM, $"{row}   {along * StepM:F0} m"));
            else if (across > 0) crossings.Add((across, row));
        }

        Console.WriteLine($"{plan.Name}  {crossings.Count} pairs of roads on one level cross away from any junction, " +
                          $"{overlaps.Count} run over each other for {OverlapLongestM:F0} m or more");
        Print("crossing with no junction", crossings);
        Print("carriageways over each other", overlaps);

        void Print(string what, List<(float Score, string Row)> rows)
        {
            if (rows.Count == 0) return;

            Console.WriteLine();
            Console.WriteLine($"  {what}:");
            rows.Sort((a, b) => b.Score.CompareTo(a.Score));
            var every = Math.Max(1, rows.Count / Listed);
            for (var row = 0; row < rows.Count; row += every) Console.WriteLine(rows[row].Row);
        }

        string Describe(int road) =>
            $"{roads.LanesWithTheRoad(road)}+{roads.LanesAgainstTheRoad(road)} {roads.WidthM[road],4:F1}m " +
            $"{Spline.TotalLengthM(roads.SegmentsOf(road)),5:F0}m long";

        bool InItsBox(int road, Vector2 pointM) =>
            Vector2.Distance(pointM, junctions.CentreM[roads.FromJunction[road]]) < junctions.RadiusM[roads.FromJunction[road]] ||
            Vector2.Distance(pointM, junctions.CentreM[roads.ToJunction[road]]) < junctions.RadiusM[roads.ToJunction[road]];

        // Two roads meeting at one junction stand beside each other leaving it, as far as their two widths take to part.
        bool SharesAJunction(int one, int other, Vector2 pointM)
        {
            foreach (var junction in (ReadOnlySpan<int>)[roads.FromJunction[one], roads.ToJunction[one]])
            {
                if (junction != roads.FromJunction[other] && junction != roads.ToJunction[other]) continue;

                var partM = junctions.RadiusM[junction] + ((roads.WidthM[one] + roads.WidthM[other]) * 2f);
                if (Vector2.Distance(pointM, junctions.CentreM[junction]) < partM) return true;
            }

            return false;
        }

        (int X, int Y) Cell(Vector2 pointM) => ((int)MathF.Floor(pointM.X / cellM), (int)MathF.Floor(pointM.Y / cellM));
    }
}
