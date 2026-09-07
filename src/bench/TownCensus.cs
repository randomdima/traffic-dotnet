using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Persistence;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Road;
using TrafficSimulation.World.Routing;
using TrafficSimulation.World.Statics;
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
        var started = Stopwatch.GetTimestamp();
        var plan = Maps.Plan(map, config, BuildingCatalog.Shared.OrdinaryFootprintsM());
        var elapsed = Stopwatch.GetElapsedTime(started);

        var locator = new GroundLocator(plan, config);

        Console.WriteLine($"census — {plan.Name}, seed {plan.Seed}");
        Console.WriteLine($"{plan.WorldSizeM.X:F0} x {plan.WorldSizeM.Y:F0} m, {plan.PavementWidthM:F1} m pavement");
        Console.WriteLine($"laid in {elapsed.TotalMilliseconds:F0} ms");
        Console.WriteLine();

        // <b>A share and never a count</b>: the ground is a set of shapes with no cells in it, so how much
        // of the town each kind covers is measured by asking, on a lattice this report owns and at the step
        // the figures name. The area is the sample's, which is what the row says.
        var stepM = config.Terrain.GroundStepM;
        Span<int> samplesPerGround = stackalloc int[GroundCatalog.Kinds];
        var samples = 0;
        var asked = Stopwatch.GetTimestamp();
        for (var y = stepM * 0.5f; y < plan.WorldSizeM.Y; y += stepM)
        {
            for (var x = stepM * 0.5f; x < plan.WorldSizeM.X; x += stepM)
            {
                samplesPerGround[(int)locator.GroundAt(new Vector2(x, y))]++;
                samples++;
            }
        }

        // <b>What one question costs, beside the answers.</b> Four wheels a car, sixty times a second, is
        // what this figure is really about, and a sweep of the whole town is the cheapest honest way to take
        // it — every kind of ground in the proportion the town actually holds them.
        var perAsk = Stopwatch.GetElapsedTime(asked).TotalMilliseconds * 1e6 / samples;

        Console.WriteLine($"ground, sampled every {stepM:F2} m — {samples / 1000} k asks at {perAsk:F0} ns each");
        for (var ground = 0; ground < samplesPerGround.Length; ground++)
        {
            if (samplesPerGround[ground] == 0) continue;

            var rules = GroundCatalog.RulesOf((Ground)ground);
            Console.WriteLine($"  {(Ground)ground,-13}{samplesPerGround[ground] * stepM * stepM / 10000f,10:F2} ha  " +
                              $"{100d * samplesPerGround[ground] / samples,5:F1} %  {rules}");
        }

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

        // How many props wear a look with a front, which is the only sense in which the bearing the verge
        // pass laid them on is visible (GEN-6b): a look that does not turn is drawn upright either way.
        var looks = PropCatalog.Load();
        var turned = 0;
        for (var prop = 0; prop < plan.Props.Count; prop++)
        {
            var look = looks.Look(plan.Props.Kind[prop], plan.Props.RadiusM[prop] * 2f, prop);
            if (looks.Variants[look].Turns) turned++;
        }

        Console.WriteLine("what is laid");
        Console.WriteLine($"  roads          {plan.Roads.Count,7}  {plan.Roads.Segments.Length} arcs, {roadLengthM / 1000f:F2} km, " +
                          $"{Mean(plan.Roads.WidthM):F2} m wide, {OneWay(plan)} of them one way");
        Console.WriteLine($"  junctions      {plan.Junctions.Count,7}  {lit} lit, {JunctionsWith(plan, 2)} with no fork, " +
                          $"{JunctionsWith(plan, 1)} dead ends, {plan.JunctionCorners.Count} kerb corners, " +
                          $"reach {Mean(plan.Junctions.RadiusM):F2} m");
        Console.WriteLine($"  bridges        {plan.Bridges.Count,7}  paved areas {plan.PavedAreas.Count}");
        // A zebra has no span of its own to print: what it reaches is solved off the road it is painted on
        // (TER-6), and the widest is the one laid furthest off square.
        Console.WriteLine($"  crossings      {plan.Crosswalks.Count,7}  {Mean(plan.Crosswalks.DepthM):F2} m deep, " +
                          $"reaching {Widest(plan):F2} m at the widest");
        Console.WriteLine($"  stop bars      {plan.StopLines.Count,7}  {Mean(plan.StopLines.SpanM):F2} m across the lane, " +
                          $"{Mean(plan.StopLines.ThicknessM):F2} m thick");
        Console.WriteLine($"  parking lots   {plan.ParkingLots.Count,7}  {plan.ParkingLots.SpaceCount} spaces, " +
                          $"{Fronting(plan, config)} of them front a kerb the line is broken over");
        Console.WriteLine($"  buildings      {plan.Buildings.Count,7}  capacity {capacity}, {plan.Buildings.EntryPointM.Length} ways in");
        Console.WriteLine($"  props          {plan.Props.Count,7}  {propsByKind[0]} wild, {propsByKind[1]} planted, " +
                          $"{propsByKind[2]} furniture; {turned} turned onto the kerb they stand along");
        Console.WriteLine($"  water          {plan.Water.Outline.Count,7}  outlines, {plan.Water.Outline.PointM.Length} points; " +
                          $"{plan.Water.Shore.Count} shores of {plan.Water.Shore.PointM.Length}");
        Console.WriteLine();

        Console.WriteLine("the roster the plan asks for");
        Console.WriteLine($"  {people} people, {cars} cars");

        // What the map's service buildings lay on top of its own spawns: a crewed car for every bay of
        // every apron and one at each depot (AMB-2, SRV-2). They are what the town has room for — a
        // building with fewer bays near it than the apron asks for stands fewer, and one with none stands
        // none. Where the shares this build would place them at differ from what the file declares, the
        // map is due another `--place-services`.
        var uses = BuildingUses.Of(plan);
        var apron = (uses.Hospitals.Count + uses.PoliceStations.Count) * config.Service.ApronBays;
        Console.WriteLine(
            $"  plus an apron of {config.Service.ApronBays} cars — a driver and a hand apiece (SRV-3) — at " +
            $"each of the map's own hospitals ({uses.Hospitals.Count} of {HospitalRoster.CountIn(plan, config)} " +
            $"this build would place) and police stations ({uses.PoliceStations.Count} of " +
            $"{PoliceStationRoster.CountIn(plan, config)}), and one at each of its depots " +
            $"({uses.Depots.Count} of {DepotRoster.CountIn(plan, config)}) — {apron} bays held off the town");
        Console.WriteLine();

        Networks(plan, config);
    }

    /// <summary>
    /// How many of the town's junctions have this many arms. <b>A generated town has none of one</b>
    /// (GEN-5a) — a dead end wants the disc a car turns round in (TER-5a) and a city lays every junction as
    /// the crossing its arms make — so that count says whether a map laid in code promised that ground on
    /// purpose, and the count of two says how much of the town is crossed once rather than once an arm
    /// (TER-6).
    /// </summary>
    /// <summary>How many of the town's roads carry traffic one way only (TER-4d), which is how much of a grid town its middle is.</summary>
    static int OneWay(CityPlan plan)
    {
        var found = 0;
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.Flow[road] != RoadFlow.BothWays) found++;
        }

        return found;
    }

    static int JunctionsWith(CityPlan plan, int arms)
    {
        var atEach = new int[plan.Junctions.Count];
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            atEach[plan.Roads.FromJunction[road]]++;
            atEach[plan.Roads.ToJunction[road]]++;
        }

        var found = 0;
        foreach (var at in atEach)
        {
            if (at == arms) found++;
        }

        return found;
    }

    /// <summary>
    /// How many car parks stand against the carriageway itself rather than behind a walk. A town where it
    /// is far below the lot count is a town whose lots were laid off the kerb they were meant to hang off
    /// (GEN-4b).
    /// </summary>
    static int Fronting(CityPlan plan, SimConfig config)
    {
        var fronting = 0;
        foreach (var front in RoadFrontages.Lay(plan.Ground, config).All)
        {
            if (front.FrontsTheKerb) fronting++;
        }

        return fronting;
    }

    /// <summary>
    /// What the town contracts to. <b>The interesting figure is the second column</b>: how many of the
    /// plan's junctions are places a driver actually chooses at, because everything else is a bend the
    /// search must never be asked a question at — plus the ends of the parking sections (GEN-4h), which
    /// are on the network for the opposite reason, being nodes nothing is decided at.
    /// </summary>
    static void Networks(CityPlan plan, SimConfig config)
    {
        var started = Stopwatch.GetTimestamp();
        var roads = RoadGraph.Build(plan, config);
        var driving = DrivingNetwork.Build(roads, BayWays.WhereALegMayTurn(roads, BayWays.Build(plan, roads, config)), plan, config);
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

        // What the town actually lights, which is not what the map asks for: a bundle wants movements to
        // conflict (TLT-3), so a place where a road is merely cut carries an uncontrolled crossing and the
        // walker's right of way is the whole of what governs it (TER-5e).
        var signals = SignalService.Build(plan, roads, config);
        var bundles = 0;
        for (var junction = 0; junction < signals.JunctionCount; junction++)
        {
            if (signals.Lit(junction)) bundles++;
        }

        var uncontrolled = 0;
        for (var crossing = 0; crossing < signals.CrossingCount; crossing++)
        {
            if (!signals.CrossingIsLit(crossing)) uncontrolled++;
        }

        Console.WriteLine("the networks");
        Console.WriteLine($"  signals        {bundles,7}  bundles of the {plan.Junctions.Count} junctions, " +
                          $"{uncontrolled} of {signals.CrossingCount} crossings uncontrolled");
        var cut = 0;
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            if (roads.LaneEndsAtAPlace[lane]) cut++;
        }

        Console.WriteLine($"  driving        {roads.LaneCount,7}  lanes meeting at {roads.Places.Count} places, " +
                          $"{plan.Junctions.Count} of them the plan's junctions and {cut} lane ends cut for car " +
                          $"parks, laid in {elapsed.TotalMilliseconds:F0} ms");
        Console.WriteLine($"  contracted to  {runs.LinkCount,7}  runs joined {WaysOn(runs.Graph)} ways on; " +
                          $"mean {(runs.LinkCount == 0 ? 0f : totalM / runs.LinkCount):F0} m, longest {longestM:F0} m, most lanes in one {mostPieces}");
        Joins(roads, config);

        var footStarted = Stopwatch.GetTimestamp();
        var foot = FootGraph.Build(plan, config);
        var footElapsed = Stopwatch.GetElapsedTime(footStarted);

        var walkStarted = Stopwatch.GetTimestamp();
        var walking = WalkingNetwork.Build(foot, new GroundLocator(plan, config), config);
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
        Console.WriteLine($"  contracted to  {walkRuns.LinkCount,7}  runs joined {WaysOn(walkRuns.Graph)} ways on; " +
                          $"mean {(walkRuns.LinkCount == 0 ? 0f : totalWalkM / walkRuns.LinkCount):F0} m, longest {longestWalkM:F0} m, " +
                          $"mean lane offset {(stretches == 0 ? 0f : keptM / stretches):F2} m of " +
                          $"{config.WalkingLaneOffsetM:F2}, in {walkElapsed.TotalMilliseconds:F0} ms");
    }

    /// <summary>
    /// How branchy the contracted graph came out: every way on from a link onto another, which is the whole
    /// of what a search has to choose between once the runs are laid.
    /// </summary>
    static int WaysOn(TravelGraph graph)
    {
        var ways = 0;
        for (var link = 0; link < graph.LinkCount; link++) ways += graph.TurnsFrom(link).Length;

        return ways;
    }

    /// <summary>
    /// What the joins between the town's connection points came out at, and what the junctions cost the
    /// lanes they stand on. <b>The figure to read is the tightest arc</b>: a join is the corner the junction
    /// was paved for, since the arms are cut back to where that paving reaches, so a town whose tightest
    /// join is inside the corner radius is a town with a junction paved smaller than the wedge it stands in.
    /// </summary>
    static void Joins(RoadGraph roads, SimConfig config)
    {
        var butted = 0;
        var joinM = 0f;
        var longestM = 0f;
        var cutBackM = 0f;
        var deepestM = 0f;
        var tightestM = float.PositiveInfinity;

        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            cutBackM += roads.LaneCutBackM[lane];
            deepestM = MathF.Max(deepestM, roads.LaneCutBackM[lane]);
        }

        for (var slot = 0; slot < roads.ConnectorCount; slot++)
        {
            if (roads.ConnectorArcs(slot).Length == 0) butted++;

            joinM += roads.ConnectorLengthM(slot);
            longestM = MathF.Max(longestM, roads.ConnectorLengthM(slot));
            foreach (var arc in roads.ConnectorArcs(slot))
            {
                if (MathF.Abs(arc.Curvature) > 1e-6f) tightestM = MathF.Min(tightestM, 1f / MathF.Abs(arc.Curvature));
            }
        }

        Console.WriteLine($"  joins          {roads.ConnectorCount,7}  movements over {roads.LaneCount} lanes; " +
                          $"{butted} join two lanes that butt; " +
                          $"mean {(roads.ConnectorCount == 0 ? 0f : joinM / roads.ConnectorCount):F2} m, longest {longestM:F2} m, " +
                          $"tightest arc {(float.IsFinite(tightestM) ? tightestM : 0f):F2} m of " +
                          $"{config.IntersectionCornerRadiusM:F2}; " +
                          $"corners took a further {(roads.LaneCount == 0 ? 0f : cutBackM / roads.LaneCount):F2} m " +
                          $"a lane, deepest {deepestM:F2} m");
    }

    /// <summary>How far the furthest-reaching zebra runs, which on a town of square crossings is a road's width.</summary>
    static float Widest(CityPlan plan)
    {
        var spanM = 0f;
        for (var crossing = 0; crossing < plan.Crosswalks.Count; crossing++)
        {
            spanM = MathF.Max(spanM, plan.CrossingSpanM(crossing));
        }

        return spanM;
    }

    static float Mean(ReadOnlySpan<float> figures)
    {
        if (figures.Length == 0) return 0f;

        var total = 0f;
        foreach (var figure in figures) total += figure;
        return total / figures.Length;
    }
}
