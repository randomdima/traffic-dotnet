using System.Diagnostics;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Terrain;

namespace TrafficSimulation.Bench;

/// <summary>
/// What is actually in the town that a figure was taken on. The performance doctrine requires it
/// beside every measurement, because a tick figure alone says how fast a build runs and not whether
/// the town it ran was a town — a town where every car is stuck against a kerb is very fast indeed.
/// </summary>
/// <remarks>
/// It also reads back what is modelled: a line that says <em>none yet</em> is where an unbuilt part of
/// the town stays visible.
/// </remarks>
internal static class TownCensus
{
    public static void Run(string map, SimConfig config)
    {
        var path = ProjectPaths.TownFile(map);
        var bytes = new FileInfo(path).Length;

        var started = Stopwatch.GetTimestamp();
        var plan = TownReader.ReadFile(path);
        var elapsed = Stopwatch.GetElapsedTime(started);

        var grid = new TerrainGrid(plan, config);

        Console.WriteLine($"census — {plan.Name}, seed {plan.Seed}");
        Console.WriteLine($"{plan.WorldSizeM.X:F0} x {plan.WorldSizeM.Y:F0} m, {plan.GridWidth} x {plan.GridHeight} cells of " +
                          $"{plan.CellSizeM:F2} m, {plan.PavementWidthM:F1} m pavement");
        Console.WriteLine($"{bytes / 1024d / 1024d:F2} MB read and laid out in {elapsed.TotalMilliseconds:F1} ms");
        Console.WriteLine();

        Console.WriteLine("ground");
        Span<int> cellsPerGround = stackalloc int[GroundCatalog.Kinds];
        foreach (var ground in plan.Cells) cellsPerGround[(int)ground]++;
        for (var ground = 0; ground < cellsPerGround.Length; ground++)
        {
            if (cellsPerGround[ground] == 0) continue;

            var rules = GroundCatalog.RulesOf((Ground)ground);
            Console.WriteLine($"  {(Ground)ground,-13}{cellsPerGround[ground],10} cells  " +
                              $"{100d * cellsPerGround[ground] / plan.CellCount,5:F1} %  {rules}");
        }

        var directionalCells = 0;
        for (var cell = 0; cell < plan.CellCount; cell++)
        {
            if (plan.LaneDirs[cell * 2] != 0 || plan.LaneDirs[cell * 2 + 1] != 0) directionalCells++;
        }

        Console.WriteLine($"  lane directions on {directionalCells} cells, {grid.CellSizeM:F2} m each");
        Console.WriteLine();

        var lit = 0;
        foreach (var isLit in plan.Junctions.Lit)
        {
            if (isLit) lit++;
        }

        var roadLengthM = 0f;
        foreach (var segment in plan.Roads.Segments) roadLengthM += segment.LengthM;

        var capacity = 0;
        foreach (var seats in plan.Buildings.Capacity) capacity += seats;

        var people = 0;
        var cars = 0;
        foreach (var kind in plan.Spawns.Kind)
        {
            if (kind == 0) people++;
            else cars++;
        }

        // The widths the paint is laid against, printed beside the counts: whether a zebra spans its
        // carriageway is a claim about two of these numbers and cannot be argued from a picture.
        var propsByKind = new int[8];
        foreach (var kind in plan.Props.Kind) propsByKind[kind % propsByKind.Length]++;

        Console.WriteLine("what is laid");
        Console.WriteLine($"  roads          {plan.Roads.Count,7}  {plan.Roads.Segments.Length} arcs, {roadLengthM / 1000f:F2} km, " +
                          $"{Mean(plan.Roads.WidthM):F2} m wide");
        Console.WriteLine($"  junctions      {plan.Junctions.Count,7}  {lit} lit, {plan.JunctionCorners.Count} kerb corners, " +
                          $"reach {Mean(plan.Junctions.RadiusM):F2} m");
        Console.WriteLine($"  pavement       {plan.PavementCorners.Count,7}  inner corners");
        Console.WriteLine($"  bridges        {plan.Bridges.Count,7}  paved areas {plan.PavedAreas.Count}");
        Console.WriteLine($"  crossings      {plan.Crosswalks.Count,7}  {Mean(plan.Crosswalks.SpanM):F2} m across the road, " +
                          $"{Mean(plan.Crosswalks.DepthM):F2} m deep");
        Console.WriteLine($"  stop bars      {plan.StopLines.Count,7}  {Mean(plan.StopLines.SpanM):F2} m across the lane, " +
                          $"{Mean(plan.StopLines.ThicknessM):F2} m thick");
        Console.WriteLine($"  parking lots   {plan.ParkingLots.Count,7}  {plan.ParkingLots.SpaceCount} spaces");
        Console.WriteLine($"  buildings      {plan.Buildings.Count,7}  capacity {capacity}, {plan.Buildings.EntryPointM.Length} ways in");
        Console.WriteLine($"  props          {plan.Props.Count,7}  {propsByKind[0]} tree, {propsByKind[1]} scatter, {propsByKind[2]} furniture");
        Console.WriteLine($"  water          {plan.Water.Count,7}  outlines, {plan.Water.PointM.Length} points");
        Console.WriteLine();

        Console.WriteLine("the roster the plan asks for");
        Console.WriteLine($"  {people} people, {cars} cars");
        Console.WriteLine();

        Networks(plan, config);
    }

    /// <summary>
    /// What the town contracts to. <b>The interesting figure is the second column</b>: how many of the
    /// plan's junctions are places a driver actually chooses at, because everything else is a bend the
    /// search must never be asked a question at.
    /// </summary>
    static void Networks(CityPlan plan, SimConfig config)
    {
        var started = Stopwatch.GetTimestamp();
        var roads = RoadGraph.Build(plan, config);
        var driving = DrivingNetwork.Build(roads, plan, config);
        var elapsed = Stopwatch.GetElapsedTime(started);

        var runs = driving.Runs;
        var longestM = 0f;
        var totalM = 0f;
        var mostPieces = 0;
        for (var link = 0; link < runs.LinkCount; link++)
        {
            longestM = MathF.Max(longestM, runs.LengthM(link));
            totalM += runs.LengthM(link);
            mostPieces = Math.Max(mostPieces, runs.PiecesOf(link).Length);
        }

        Console.WriteLine("the networks");
        Console.WriteLine($"  driving        {roads.LaneCount,7}  lanes over {plan.Junctions.Count} junctions, laid in {elapsed.TotalMilliseconds:F0} ms");
        Console.WriteLine($"  contracted to  {runs.LinkCount,7}  runs over {runs.Graph.NodeCount} nodes; " +
                          $"mean {(runs.LinkCount == 0 ? 0f : totalM / runs.LinkCount):F0} m, longest {longestM:F0} m, most lanes in one {mostPieces}");
        Joins(roads, config);

        var footStarted = Stopwatch.GetTimestamp();
        var foot = FootGraph.Build(plan, config);
        var footElapsed = Stopwatch.GetElapsedTime(footStarted);

        var walkStarted = Stopwatch.GetTimestamp();
        var walking = WalkingNetwork.Build(foot, new TerrainGrid(plan, config), config);
        var walkElapsed = Stopwatch.GetElapsedTime(walkStarted);

        var footM = 0f;
        var crossings = 0;
        var keptM = 0f;
        for (var edge = 0; edge < foot.EdgeCount; edge += 2)
        {
            footM += foot.LengthM(edge);
            keptM += walking.LaneOffsetM(edge);
            if (foot.KindOf(edge) == FootEdgeKind.Crossing) crossings++;
        }

        var walkRuns = walking.Runs;
        var longestWalkM = 0f;
        var totalWalkM = 0f;
        for (var link = 0; link < walkRuns.LinkCount; link++)
        {
            longestWalkM = MathF.Max(longestWalkM, walkRuns.LengthM(link));
            totalWalkM += walkRuns.LengthM(link);
        }

        var stretches = foot.EdgeCount / 2;
        Console.WriteLine($"  walking        {stretches,7}  stretches over {foot.NodeCount} fine nodes, {footM / 1000f:F2} km, " +
                          $"{crossings} of them crossings, laid in {footElapsed.TotalMilliseconds:F0} ms");
        Console.WriteLine($"  contracted to  {walkRuns.LinkCount,7}  runs over {walkRuns.Graph.NodeCount} nodes; " +
                          $"mean {(walkRuns.LinkCount == 0 ? 0f : totalWalkM / walkRuns.LinkCount):F0} m, longest {longestWalkM:F0} m, " +
                          $"mean lane offset {(stretches == 0 ? 0f : keptM / stretches):F2} m of " +
                          $"{config.WalkingLaneOffsetM:F2}, in {walkElapsed.TotalMilliseconds:F0} ms");
    }

    /// <summary>
    /// What the joins through the town's junctions came out at. <b>The figure to read is how many take
    /// no setback at all</b>: a join is widened into the two lanes only as far as it takes for the arc
    /// to reach the junction's own corner radius, so a straight and an open turn take none and only a
    /// right-angle turn pays for one — and a town where most of them pay is a town whose junctions are
    /// tight.
    /// </summary>
    static void Joins(RoadGraph roads, SimConfig config)
    {
        var turns = 0;
        var free = 0;
        var atTheCap = 0;
        var setbackM = 0f;
        var widestM = 0f;
        var tightestM = float.PositiveInfinity;

        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            var kinds = roads.TurnKindsFrom(lane);
            for (var turn = 0; turn < kinds.Length; turn++)
            {
                // A turn-around is laid like any other and no car may be routed through one, so counting
                // it here would price the town's junctions by a manoeuvre nothing drives (P-11, M8).
                if (kinds[turn] == LaneTurn.TurnAround) continue;

                var slot = roads.TurnSlotAt(lane, turn);
                var atM = roads.JoinFromM(slot);
                turns++;
                if (atM <= 0f) free++;
                var capM = MathF.Min(
                    config.IntersectionCornerRadiusM,
                    MathF.Min(roads.LaneLengthM[lane], roads.LaneLengthM[roads.TurnsFrom(lane)[turn]]) * 0.5f);
                if (atM >= capM - 1e-3f) atTheCap++;

                setbackM += atM;
                widestM = MathF.Max(widestM, atM);
                foreach (var arc in roads.JoinArcs(slot))
                {
                    if (MathF.Abs(arc.Curvature) > 1e-6f) tightestM = MathF.Min(tightestM, 1f / MathF.Abs(arc.Curvature));
                }
            }
        }

        Console.WriteLine($"  joins          {turns,7}  movements; {free} take no setback, {atTheCap} take all the town allows; " +
                          $"mean {(turns == 0 ? 0f : setbackM / turns):F2} m, widest {widestM:F2} m of " +
                          $"{config.IntersectionCornerRadiusM:F2}, tightest arc {(float.IsFinite(tightestM) ? tightestM : 0f):F2} m");
    }

    static float Mean(ReadOnlySpan<float> figures)
    {
        if (figures.Length == 0) return 0f;

        var total = 0f;
        foreach (var figure in figures) total += figure;
        return total / figures.Length;
    }
}
