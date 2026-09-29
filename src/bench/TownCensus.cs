using System.Diagnostics;
using System.Numerics;
using TrafficSimulation.Agents.Ambulance;
using TrafficSimulation.Agents.Service;
using TrafficSimulation.Agents.TrafficLight.Control;
using TrafficSimulation.App.Render;
using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
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
        var plan = Maps.Plan(map, config, BuildingCatalog.Roofs);
        var elapsed = Stopwatch.GetElapsedTime(started);

        Console.WriteLine($"census — {plan.Name}, seed {plan.Seed}");
        Console.WriteLine($"{plan.WorldSizeM.X:F0} x {plan.WorldSizeM.Y:F0} m, {plan.PavementWidthM:F1} m pavement");
        Console.WriteLine($"laid in {elapsed.TotalMilliseconds:F0} ms");
        Console.WriteLine();

        Ground(plan, config);

        var shapes = new GroundShapes(plan.Paving(config), config);
        var locator = new GroundLocator(shapes, config);

        // <b>A share and never a count</b>: the ground is a set of shapes with no cells in it, so how much
        // of the town each kind covers is measured by asking at the middle of every cell of the grid's level
        // at the step the figures name (SIM-8). The area is the sample's, which is what the row says.
        var sampled = Sampled(config.Grid.Within(config.Terrain.GroundStepM), plan.WorldSizeM);
        var stepM = sampled.Level.CellM;
        var kindAt = new byte[sampled.Count];
        Span<int> samplesPerGround = stackalloc int[GroundCatalog.Kinds];
        var samples = 0;
        for (var y = sampled.FromY; y <= sampled.ToY; y++)
        {
            for (var x = sampled.FromX; x <= sampled.ToX; x++)
            {
                var ground = locator.GroundAt(sampled.Level.MiddleM(x, y));
                kindAt[samples++] = (byte)ground;
                samplesPerGround[(int)ground]++;
            }
        }

        var nsPerAsk = new double[GroundCatalog.Kinds];
        var askedNs = 0d;
        for (var ground = 0; ground < nsPerAsk.Length; ground++)
        {
            if (samplesPerGround[ground] == 0) continue;

            nsPerAsk[ground] =
                NsPerAsk(locator, kindAt, (Ground)ground, sampled, samplesPerGround[ground]);
            askedNs += nsPerAsk[ground] * samplesPerGround[ground];
        }

        // <b>What one question costs, beside the answers.</b> Four wheels a car, sixty times a second, is
        // what this figure is really about, and the whole town is the cheapest honest way to take it — every
        // kind of ground in the proportion the town actually holds them. <b>Which is the mean of the rows and
        // not a sweep of its own</b>, because the two hot paths have almost nothing in common: a wheel's ask
        // is the roads and the boundary, and a walker's carries on into the water's rings and the walk — so
        // one figure over the mix says what a town costs and nothing about what either of them costs.
        var perAsk = askedNs / samples;

        Console.WriteLine($"ground, sampled every {stepM:F2} m — {samples / 1000} k asks at {perAsk:F0} ns each");
        for (var ground = 0; ground < samplesPerGround.Length; ground++)
        {
            if (samplesPerGround[ground] == 0) continue;

            var rules = GroundCatalog.RulesOf((Ground)ground);
            Console.WriteLine($"  {(Ground)ground,-13}{samplesPerGround[ground] * stepM * stepM / 10000f,10:F2} ha  " +
                              $"{100d * samplesPerGround[ground] / samples,5:F1} %  {nsPerAsk[ground],5:F0} ns  {rules}");
        }

        // <b>What the boundary is answered off</b>: a cell no ring crosses is a lookup, so the crossed share is
        // the share of the ground that costs more than one.
        Console.WriteLine($"  boundary answered off the grid's {config.ShellLevel.CellM:F2} m level");
        Lattice("carriageway", shapes.CarriagewaySides);
        Lattice("walk", shapes.WalkSides);

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
        var ring = RingWidest(plan);
        Console.WriteLine($"  roundabouts    {plan.Roundabouts.Count,7}  {plan.Roundabouts.Road.Length} roads circulating, " +
                          $"{ring.WidestM:F1} m across at the widest, at {ring.AtM.X:F0},{ring.AtM.Y:F0}");
        // A zebra has no span of its own to print: what it reaches is the two kerbs its walk crosses between
        // (TER-6, WLK-10), and the widest is the widest reach any of them has. Read off the registry the
        // paint is laid from (<see cref="Crossings"/>) and never the plan's own array, which is a town's
        // arms and not its paint. <b>Asked for by the places the walk crosses</b> (<see cref="KerbEnds"/>),
        // which is the one answer the picture is painted from too — and the bars off the places it is cut,
        // which is the answer they are laid from there.
        var ends = plan.Paving(config).RoadEnds(config);
        var crossings = Crossings.Lay(plan, config, ends.CrossedM);
        var bars = StopBars.Lay(plan.Paving(config).Lanes, Crossings.Lay(plan, config, ends.HeldM), config);
        var overrunning = Overrunning(plan, crossings);
        var reaching = Reaching(ends);
        Console.WriteLine($"  kerb ends      {ends.Further.Length + ends.Nearer.Length,7}  standing " +
                          $"{reaching.MiddleM:F2} m out of the box at the middle of them, {reaching.WorstM:F2} m at " +
                          $"the furthest, on road {reaching.Road} at {reaching.AtM.X:F0},{reaching.AtM.Y:F0}");
        Console.WriteLine($"  crossings      {crossings.Count,7}  {Mean(crossings.DepthM):F2} m deep, " +
                          $"reaching {Widest(crossings):F2} m at the widest; {Midway(crossings)} midway down " +
                          $"a short road; {overrunning.Over} reach past " +
                          $"their carriageway, by up to {overrunning.WorstM:F2} m");
        Console.WriteLine($"  stop bars      {bars.Count,7}  {Mean(bars.SpanM):F2} m across the lane, " +
                          $"{Mean(bars.ThicknessM):F2} m thick");
        // What an arrow says is what its lane offers (TER-6a), so the spread over the three is the town's
        // own reading of how much choice a driver holding at a bar has.
        var arrows = LaneArrows.Lay(plan.Paving(config).Lanes, bars, config);
        Console.WriteLine($"  lane arrows    {arrows.Count,7}  {Saying(arrows, 1)} say one way, " +
                          $"{Saying(arrows, 2)} two, {Saying(arrows, 3)} three; " +
                          $"{Bending(arrows):F0}° of turn at the sharpest branch");
        Console.WriteLine($"  parking lots   {plan.ParkingLots.Count,7}  {plan.ParkingLots.SpaceCount} spaces");
        Console.WriteLine($"  buildings      {plan.Buildings.Count,7}  of {BuildingsAskedFor(plan)} planned, " +
                          $"capacity {capacity}, {plan.Buildings.EntryPointM.Length} ways in");
        Console.WriteLine($"  props          {plan.Props.Count,7}  {propsByKind[0]} wild, {propsByKind[1]} planted, " +
                          $"{propsByKind[2]} furniture; {turned} turned onto the kerb they stand along");
        Console.WriteLine($"  water          {plan.Water.Outline.Count,7}  outlines, {plan.Water.Outline.PointM.Length} points; " +
                          $"{plan.Water.Shore.Count} shores of {plan.Water.Shore.PointM.Length}");
        Console.WriteLine();

        Console.WriteLine("the roster the plan asks for");
        Console.WriteLine($"  {people} people, {cars} cars");

        // What the map's service buildings lay on top of its own spawns: a car for every bay of
        // every apron and one at each depot (AMB-2, SRV-2). They are what the town has room for — a
        // building with fewer bays near it than the apron asks for stands fewer, and one with none stands
        // none. Where the shares this build would place them at differ from what the file declares, the
        // map is due another `--place-services`.
        var uses = BuildingUses.Of(plan);
        var apron = (uses.Hospitals.Count + uses.PoliceStations.Count) * config.Service.ApronBays;
        Console.WriteLine(
            $"  plus an apron of {config.Service.ApronBays} cars, nobody aboard any of them (SRV-3), at " +
            $"each of the map's own hospitals ({uses.Hospitals.Count} of {HospitalRoster.CountIn(plan, config)} " +
            $"this build would place) and police stations ({uses.PoliceStations.Count} of " +
            $"{PoliceStationRoster.CountIn(plan, config)}), and one at each of its depots " +
            $"({uses.Depots.Count} of {DepotRoster.CountIn(plan, config)}) — {apron} bays held off the town");
        Console.WriteLine();

        Networks(plan, config);
        RibbonCensus.Run(plan, config);
    }

    /// <summary>The cells of a level whose middles stand inside the town, from its corner at the world's origin.</summary>
    static GridWindow Sampled(GridLevel level, Vector2 worldSizeM)
    {
        var columns = 0;
        while (level.MiddleM(columns) < worldSizeM.X) columns++;

        var rows = 0;
        while (level.MiddleM(rows) < worldSizeM.Y) rows++;

        return GridWindow.Of(level, 0, 0, columns, rows);
    }

    static void Lattice(string layer, RingSides sides) =>
        Console.WriteLine($"    {layer,-12}{sides.CellCount / 1000,7} k cells, " +
                          $"{100d * sides.CrossedCellCount / Math.Max(1, sides.CellCount),4:F1} % crossed, " +
                          $"{sides.PieceCount / 1000} k pieces, {sides.Bytes / 1048576d:F1} MB");

    /// <summary>
    /// How many asks of one kind are timed. A quarter of a million is far past what the figure needs to
    /// settle, and it holds the whole reading to a fraction of a second on a city.
    /// </summary>
    const int MostTimedAsks = 1 << 18;

    /// <summary>
    /// <b>What one ask of one kind of ground costs</b>, over the points the sweep already found to be it.
    /// </summary>
    /// <remarks>
    /// <b>Asked again rather than timed in place.</b> A timestamp costs a fair share of the ask it would be
    /// measuring, so a per-kind figure taken inside the sweep would be mostly the instrument — the sweep
    /// classifies and this measures, and the points are collected outside the clock.
    ///
    /// <b>Strided and not capped at the front</b>, because a kind lies in bands across a town: the first
    /// quarter of a million points answering water are one end of the river, where every ask is inside the
    /// same ring's box. And nothing is done with the answer because nothing needs to be — the ask stamps the
    /// locator's own scan, so it cannot be elided.
    /// </remarks>
    static double NsPerAsk(GroundLocator locator, byte[] kindAt, Ground ground, GridWindow sampled, int count)
    {
        var stride = (count + MostTimedAsks - 1) / MostTimedAsks;
        var pointsM = new Vector2[(count + stride - 1) / stride];
        var taken = 0;
        var seen = 0;
        var at = 0;
        for (var y = sampled.FromY; y <= sampled.ToY; y++)
        {
            for (var x = sampled.FromX; x <= sampled.ToX; x++)
            {
                if (kindAt[at++] != (byte)ground) continue;
                if (seen++ % stride == 0 && taken < pointsM.Length) pointsM[taken++] = sampled.Level.MiddleM(x, y);
            }
        }

        var asked = Stopwatch.GetTimestamp();
        foreach (var pointM in pointsM.AsSpan(0, taken)) locator.GroundAt(pointM);

        return Stopwatch.GetElapsedTime(asked).TotalMilliseconds * 1e6 / taken;
    }

    /// <summary>
    /// <b>The same ground as the picture holds it</b> (<see cref="GroundMesh"/>): the triangles the
    /// renderer is handed, cut into the layers they were laid in, and what each layer cost to cut
    /// (OBS-2v). The menu's ground section reads the same tallies off the same mesh, so the figure a session sees
    /// and the figure a report prints are one reading.
    /// </summary>
    /// <remarks>
    /// <b>Taken before anything else asks the plan a question</b>, because the boundary every layer is
    /// struck off is laid once and kept: a mesh built after the locator has already asked for it would
    /// charge the merge to whoever came first and read nothing here.
    /// </remarks>
    static void Ground(CityPlan plan, SimConfig config)
    {
        var mesh = GroundMesh.Build(plan, config);
        var triangles = mesh.Indices.Length / 3;
        Console.WriteLine($"the ground as it is drawn — {triangles} triangles over {mesh.Vertices.Length} corners, " +
                          $"laid in {mesh.LaidMs:F0} ms, of which the boundary is {mesh.BoundaryMs:F0} ms " +
                          $"and the merge behind it {mesh.MergeMs:F0} ms");

        for (var part = 0; part < GroundParts.Count; part++)
        {
            var tally = mesh.Parts[part];
            Console.WriteLine($"  {GroundParts.Names[part],-16}{tally.Triangles,8} tri  " +
                              $"{100d * tally.Triangles / triangles,5:F1} %  {tally.Corners,8} corners  " +
                              $"{tally.LaidMs,7:F1} ms");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// How wide across the widest of the town's roundabouts is (GEN-19) and where it stands, measured on the
    /// ground its ring nodes stand on — the point being to name a framing <c>--shot</c> can be pointed at.
    /// <b>Read off the ring and never carried beside it</b>: a roundabout is the roads that circulate on it
    /// and nothing else (<c>CityPlan.Roundabouts</c>).
    /// </summary>
    static (float WidestM, Vector2 AtM) RingWidest(CityPlan plan)
    {
        var widestM = 0f;
        var atM = Vector2.Zero;
        for (var ring = 0; ring < plan.Roundabouts.Count; ring++)
        {
            foreach (var road in plan.Roundabouts.RoadsOf(ring))
            {
                foreach (var other in plan.Roundabouts.RoadsOf(ring))
                {
                    var oneM = plan.Junctions.CentreM[plan.Roads.FromJunction[road]];
                    var elseM = plan.Junctions.CentreM[plan.Roads.FromJunction[other]];
                    if (Vector2.Distance(oneM, elseM) <= widestM) continue;

                    widestM = Vector2.Distance(oneM, elseM);
                    atM = (oneM + elseM) * 0.5f;
                }
            }
        }

        return (widestM, atM);
    }

    /// <summary>How many of the town's roads carry traffic one way only (TER-4d), scattered over the whole of it (GEN-18).</summary>
    static int OneWay(CityPlan plan)
    {
        var found = 0;
        for (var road = 0; road < plan.Roads.Count; road++)
        {
            if (plan.Roads.Flow[road] != RoadFlow.BothWays) found++;
        }

        return found;
    }

    /// <summary>
    /// <b>How many buildings the map planned</b> (GEN-54), so what is printed beside it is what the frontage
    /// could carry of them (GEN-8). <b>A map laid in code has no brief and plans none.</b>
    /// </summary>
    static int BuildingsAskedFor(CityPlan plan) =>
        Maps.IsGenerated(plan.Name) ? Maps.Brief(plan.Name).Buildings : 0;

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
    /// What the town contracts to. <b>The interesting figure is the second column</b>: how many of the
    /// plan's junctions are places a driver actually chooses at, because everything else is a bend the
    /// search must never be asked a question at.
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
        var zebras = Crossings.Lay(plan, config, plan.Paving(config).RoadEnds(config).CrossedM);
        var signals = SignalService.Build(plan, roads, zebras, config);
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
        // <b>How many of the plan's junctions are a junction at all</b>: a node of two arms carries one road
        // through and decides nothing, so the gap between the three figures is where the town's lines meet
        // without anybody choosing anything.
        var arms = plan.Ground.ArmsPerJunction();
        var carriedThrough = 0;
        foreach (var at in arms)
        {
            if (at == 2) carriedThrough++;
        }

        Console.WriteLine($"  driving        {roads.LaneCount,7}  lanes meeting at {roads.Places.Count} places over " +
                          $"the plan's {plan.Junctions.Count} junctions, {carriedThrough} of them two-armed, " +
                          $"laid in {elapsed.TotalMilliseconds:F0} ms");
        Console.WriteLine($"  contracted to  {runs.LinkCount,7}  runs joined {WaysOn(runs.Graph)} ways on; " +
                          $"mean {(runs.LinkCount == 0 ? 0f : totalM / runs.LinkCount):F0} m, longest {longestM:F0} m, most lanes in one {mostPieces}");
        Console.WriteLine($"  searched at    {Searching(runs)}");
        Joins(roads, plan, config);

        var footStarted = Stopwatch.GetTimestamp();
        var pavement = PavementLanes.Of(plan, config);
        var crossed = CrossingWays.Of(plan, pavement, config);
        var foot = FootGraph.Build(pavement, crossed, config);
        var footElapsed = Stopwatch.GetElapsedTime(footStarted);

        var walkStarted = Stopwatch.GetTimestamp();
        var walking = WalkingNetwork.Build(foot, config);
        var walkElapsed = Stopwatch.GetElapsedTime(walkStarted);

        // Every edge, because every edge is a lane walked its own way (WLK-8) rather than one of a pair.
        var footM = 0f;
        var crossings = 0;
        var walkedM = 0f;
        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            footM += foot.LengthM(edge);
            walkedM += walking.LaneWidthM(edge);
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

        Courses(plan, config);

        var lanes = foot.EdgeCount;
        Console.WriteLine($"  walking        {lanes,7}  lanes over {foot.NodeCount} fine nodes, {footM / 1000f:F2} km, " +
                          $"{crossings} of them crossings, laid in {footElapsed.TotalMilliseconds:F0} ms");
        var unreached = crossed.Unreached == 0
            ? "0 of their lanes with no walk in reach"
            : $"{crossed.Unreached} of their lanes with no walk in reach, one at " +
              $"{crossed.UnreachedAtM.X:F0},{crossed.UnreachedAtM.Y:F0} whose course stands " +
              $"{crossed.UnreachedOffM:F1} m off";
        Console.WriteLine($"  crossed at     {crossed.Junctions,7}  junctions, {crossed.Merged} of them a " +
                          $"crossing standing where another already did; {crossed.Refused} zebras refused " +
                          $"for want of a kerb to stop at, {unreached}; " +
                          $"{crossed.Unjoined} connections no curve would join");
        Console.WriteLine($"  contracted to  {walkRuns.LinkCount,7}  runs joined {WaysOn(walkRuns.Graph)} ways on; " +
                          $"mean {(walkRuns.LinkCount == 0 ? 0f : totalWalkM / walkRuns.LinkCount):F0} m, longest {longestWalkM:F0} m, " +
                          $"mean lane {(lanes == 0 ? 0f : walkedM / lanes):F2} m wide of " +
                          $"{config.WalkingLaneWidthM:F2}");
        Console.WriteLine($"  searched at    {Searching(walkRuns)}");

        Console.WriteLine($"  laid in        {walkElapsed.TotalMilliseconds,7:F0}  ms");
        Smoothness(foot, walking);
        Boundary(plan, config);
    }

    /// <summary>
    /// <b>The lanes the town's pavement is walked down</b> (WLK-1, <see cref="PavementLanes"/>): the driven
    /// ground's boundary moved off itself once per lane, which is the shape the fine graph is laid from.
    /// </summary>
    /// <remarks>
    /// <b>One reading per lane, and what it weighs is whether the move closed</b>: a course that came back
    /// as a run rather than a ring is a pavement with two ends in the middle of the town, and it is walked
    /// by nobody. How far round each ring goes is the second question and is what says whether a lane is a
    /// street's frontage or the whole outside of a block; <b>how many pieces it came back in is how many
    /// lanes the graph holds</b>, one per piece.
    /// </remarks>
    static void Courses(CityPlan plan, SimConfig config)
    {
        var courses = PavementLanes.Of(plan.Paving(config).Perimeter(config), config);
        for (var lane = 0; lane < courses.Count; lane++)
        {
            var rings = courses.RingsOf(lane);
            var loose = courses.LooseOf(lane);
            var lengthM = 0f;
            var longestM = 0f;
            var longestAtM = Vector2.Zero;
            foreach (var ring in rings)
            {
                var ringM = Spline.TotalLengthM(ring);
                lengthM += ringM;
                if (ringM <= longestM) continue;

                longestM = ringM;
                longestAtM = ring[0].StartM;
            }

            var openM = 0f;
            var endsApartM = 0f;
            foreach (var run in loose)
            {
                openM += Spline.TotalLengthM(run);
                endsApartM = MathF.Max(endsApartM, Vector2.Distance(run[0].StartM, run[^1].EndM));
            }

            var pieces = 0;
            foreach (var ring in rings) pieces += ring.Length;

            Console.WriteLine($"  lane at        {courses.OffsetM(lane),7:F2}  m off the tarmac, walked " +
                              $"{(courses.RunsWithTheRing(lane) ? "with" : "against")} its rings: " +
                              $"{rings.Length} closed, {lengthM / 1000f:F2} km over {pieces} pieces, " +
                              $"longest {longestM:F0} m at {longestAtM.X:F0},{longestAtM.Y:F0}; " +
                              $"{loose.Length} runs left open over {openM:F1} m, ends up to " +
                              $"{endsApartM:F3} m apart");
        }
    }

    /// <summary>
    /// <b>How smoothly the walking network's own lines run</b>: at every joint of every chain in it — the
    /// stretch the graph holds and the lane a body is actually held on — the gap between the piece arriving
    /// and the piece leaving, and how far the line turns across that joint.
    /// </summary>
    /// <remarks>
    /// <b>The two readings say different things, and a lane worse than the stretch under it is the one to
    /// act on.</b> A stretch is cut out of the boundary moved by one figure (<see cref="FootGraph"/>), so
    /// whatever it turns at a joint is the town's own corner and no instrument can argue with it; a lane is
    /// laid beside that stretch (<see cref="ArcOutset.Beside"/>) and owes the reader every joint of it shut,
    /// whatever the stretch turns.
    /// </remarks>
    static void Smoothness(FootGraph foot, WalkingNetwork walking)
    {
        var stretches = new Joints();
        var lanes = new Joints();
        for (var edge = 0; edge < foot.EdgeCount; edge++)
        {
            if ((edge & 1) == 0) stretches.Walk(foot.ArcsOf(edge));

            lanes.Walk(walking.LaneOf(edge));
        }

        Console.WriteLine($"  stretch joints {stretches.Count,7}  {stretches.Say()}");
        Console.WriteLine($"  lane joints    {lanes.Count,7}  {lanes.Say()}");
    }

    /// <summary>What a run of chains came to at the joints between their pieces, gathered as it is walked.</summary>
    /// <remarks>
    /// <b>A hair of a corner and a corner are read apart</b>, because a chain of arcs fitted to a curve turns
    /// a little at every joint by construction and turning a little is not what anybody means by rugged. The
    /// figure that separates them is the one a bend is fitted to (<c>SplineToleranceWalkedM</c>) read as an
    /// angle, which is <see cref="CornerRad"/>.
    /// </remarks>
    sealed class Joints
    {
        /// <summary>How wide a gap has to be before a frame shows it: a centimetre.</summary>
        const float SeenM = 0.01f;

        /// <summary>
        /// And how far a joint has to turn before it is a corner rather than the grain of a fitted bend: five
        /// degrees, under which nothing drawn at a town's scale reads as anything but a curve.
        /// </summary>
        const float CornerRad = 5f * MathF.PI / 180f;

        public int Count { get; private set; }

        int Open { get; set; }

        int Cornered { get; set; }

        int Folded { get; set; }

        float WorstM { get; set; }

        float WorstRad { get; set; }

        Vector2 WorstAtM { get; set; }

        Vector2 WorstCornerAtM { get; set; }

        public void Walk(ReadOnlySpan<ArcSeg> chain)
        {
            foreach (ref readonly var piece in chain)
            {
                if (piece.LengthM <= 0f) Folded++;
            }

            for (var piece = 1; piece < chain.Length; piece++)
            {
                ref readonly var arriving = ref chain[piece - 1];
                ref readonly var leaving = ref chain[piece];
                var gapM = Vector2.Distance(arriving.EndM, leaving.StartM);
                var turnRad = MathF.Abs(
                    Spline.WrapRad(leaving.HeadingRad - arriving.HeadingAtRad(arriving.LengthM)));

                Count++;
                if (gapM > SeenM) Open++;
                if (turnRad > CornerRad) Cornered++;
                if (gapM > WorstM) (WorstM, WorstAtM) = (gapM, arriving.EndM);
                if (turnRad > WorstRad) (WorstRad, WorstCornerAtM) = (turnRad, arriving.EndM);
            }
        }

        public string Say() =>
            $"{Open} open past a centimetre, worst {WorstM * 1000f:F0} mm at {WorstAtM.X:F0}, {WorstAtM.Y:F0}; " +
            $"{Cornered} turning past five degrees, worst {WorstRad * 180f / MathF.PI:F0}° at " +
            $"{WorstCornerAtM.X:F0}, {WorstCornerAtM.Y:F0}; {Folded} pieces the move turned inside out";
    }

    /// <summary>
    /// <b>What the merge made of the ribbons the driven lines lay</b> (<see cref="LaneShell"/>): the rings
    /// it closed, and the runs it handed back with two ends instead. <b>How far apart those two ends stand
    /// is the figure to read</b> — a run open by a fraction of the weld is a crossing the merge measured and
    /// then declined to call one, and a run open by metres is a hole in the boundary.
    /// </summary>
    /// <remarks>
    /// Asked of a shipped city from the command line, because it is a question about one town's geometry and
    /// not about the engine: what the suite may ask is asked of the towns it lays for itself
    /// (<c>LaneShellTests</c>).
    /// </remarks>
    static void Boundary(CityPlan plan, SimConfig config)
    {
        var started = Stopwatch.GetTimestamp();
        var shell = plan.Paving(config).Perimeter(config);
        var elapsed = Stopwatch.GetElapsedTime(started);

        var openM = 0f;
        var apartM = 0f;
        foreach (var run in shell.Loose)
        {
            var lengthM = Spline.TotalLengthM(run);
            openM += lengthM;
            apartM = MathF.Max(
                apartM, Vector2.Distance(run[0].StartM, Spline.SampleAt(run, lengthM).PositionM));
        }

        Console.WriteLine($"  boundary       {shell.Chains.Length,7}  rings closed; {shell.Loose.Length} runs left " +
                          $"open over {openM:F1} m, ends up to {apartM:F3} m apart, merged in " +
                          $"{elapsed.TotalMilliseconds:F0} ms");

        // <b>And every open run said on its own, because the summary above cannot be acted on.</b> A merge
        // that lost a crossing lost it at one place, and what a reader needs is where to go and look: the
        // two ends the walk could not join, and the gap between them read against the weld that would have.
        for (var run = 0; run < shell.Loose.Length; run++)
        {
            var open = shell.Loose[run];
            var lengthM = Spline.TotalLengthM(open);
            var endM = open[^1].EndM;
            Console.WriteLine(
                $"    open run {run,-3}  {open.Length,6} stretches, {lengthM:F1} m, from {open[0].StartM.X:F3},"
                + $"{open[0].StartM.Y:F3} to {endM.X:F3},{endM.Y:F3} — {Vector2.Distance(open[0].StartM, endM):F3} m "
                + "apart");
        }
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

    /// <summary>How many routes are sampled to say what a search over a network costs.</summary>
    const int RoutesSampled = 64;

    /// <summary>Longer than any route either network holds, so a route is never refused for want of room.</summary>
    const int MostLinksSampled = 1024;

    /// <summary>
    /// <b>What a route over this network costs to find</b>: how much of the graph a search settles before it
    /// answers, over routes sampled end to end across the town.
    /// </summary>
    /// <remarks>
    /// <b>The figure to read is the settled count against the link count beside it.</b> It is what a change
    /// to the search's shape moves and a tick figure does not — a route is planned when a leg is drawn
    /// rather than every tick, so what a search costs is invisible in a frame time and plain here.
    /// </remarks>
    static string Searching(RunNetwork runs)
    {
        if (runs.LinkCount < 2) return "no links to search over";

        var planner = new RoutePlanner(runs.Graph);
        var route = new int[MostLinksSampled];
        var entries = new RouteEntry[1];
        var goals = new RouteGoal[1];

        var settled = 0L;
        var found = 0;
        for (var sample = 0; sample < RoutesSampled; sample++)
        {
            // Spread over the link numbering rather than drawn, so the figure is the same every run and
            // two builds are comparable without a seed to carry.
            var from = (int)((long)sample * runs.LinkCount / RoutesSampled);
            var to = (int)((((long)sample * HalfTurn) + (RoutesSampled / 2)) % RoutesSampled * runs.LinkCount / RoutesSampled);
            if (from == to) continue;

            entries[0] = new RouteEntry(from, 0f, runs.LengthM(from));
            goals[0] = new RouteGoal(to, runs.LengthM(to));

            if (planner.Plan(entries, goals, null, route, out _, out var goalSlot) > 0 && goalSlot >= 0)
            {
                found++;
            }

            settled += planner.SettledLinks;
        }

        return $"{settled / RoutesSampled,7}  links settled per route on average, over " +
               $"{RoutesSampled} routes of which {found} were found";
    }

    /// <summary>Half the sample count, coprime with it, so the far end of each sampled route is nowhere near the near one.</summary>
    const int HalfTurn = 31;

    /// <summary>
    /// What the joins between the town's connection points came out at. <b>The figure to read is the tightest
    /// arc</b>: a join runs between two lane ends a standoff out from the node (GEN-46), so a town whose
    /// tightest join is well inside the junction's own cornering radius is a town whose arms were drawn at
    /// angles the standoff cannot turn through.
    /// </summary>
    static void Joins(RoadGraph roads, CityPlan plan, SimConfig config)
    {
        var butted = 0;
        var joinM = 0f;
        var longestM = 0f;
        var tightestM = float.PositiveInfinity;

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
                          $"{config.JunctionCorneringRadiusM:F2}");
        HandOvers(roads, plan);
    }

    /// <summary>
    /// <b>How many lanes the town leaves with one way out of them, and where those nodes are</b>. A lane with
    /// one movement is a car driven through a place it decides nothing at, and the town is laid not to have
    /// them: a run through nodes nothing meets at is one road (GEN-51) and a one-way street arrives only where
    /// there is still a choice (GEN-18).
    /// </summary>
    /// <remarks>
    /// <b>Split three ways, because what is left is three different things.</b> A junction of two arms is a
    /// road that bends and a run the join could not take (GEN-51); a roundabout's entry is one movement
    /// because a ring is one way round (GEN-19) and is the shape working; and anywhere else it is a corner
    /// every other movement was refused for (GEN-48), which is a real decision at a tight junction.
    /// </remarks>
    static void HandOvers(RoadGraph roads, CityPlan plan)
    {
        var arms = plan.Ground.ArmsPerJunction();
        var onARing = new bool[plan.Junctions.Count];
        for (var ring = 0; ring < plan.Roundabouts.Count; ring++)
        {
            foreach (var road in plan.Roundabouts.RoadsOf(ring))
            {
                onARing[plan.Roads.FromJunction[road]] = true;
                onARing[plan.Roads.ToJunction[road]] = true;
            }
        }

        var atABend = 0;
        var atARing = 0;
        var atAFork = 0;
        var longestM = 0f;
        for (var lane = 0; lane < roads.LaneCount; lane++)
        {
            longestM = MathF.Max(longestM, roads.LaneLengthM[lane]);

            if (roads.LanesFrom(lane).Length != 1) continue;

            var node = roads.LaneToJunction[lane];
            if (arms[node] <= 2) atABend++;
            else if (onARing[node]) atARing++;
            else atAFork++;
        }

        Console.WriteLine($"  one way out    {atABend + atARing + atAFork,7}  lanes the node offers one movement; " +
                          $"{atABend} at a junction of two arms, {atARing} entering a roundabout, " +
                          $"{atAFork} at a junction whose other movements were refused as too tight; " +
                          $"longest lane {longestM:F0} m");
    }

    /// <summary>How many arrows say as many ways as this — how much choice a driver holding at a bar has.</summary>
    static int Saying(LaneArrows arrows, int ways)
    {
        var saying = 0;
        for (var arrow = 0; arrow < arrows.Count; arrow++)
        {
            if (arrows.BranchAt[arrow + 1] - arrows.BranchAt[arrow] == ways) saying++;
        }

        return saying;
    }

    /// <summary>
    /// And how far the sharpest branch anywhere in the town bends through, in degrees — the cap
    /// (<see cref="RoadFigures.LaneArrowBendMostDeg"/>) where any turn at all reached it.
    /// </summary>
    static float Bending(LaneArrows arrows)
    {
        var sweptRad = 0f;
        foreach (var branch in arrows.Branch) sweptRad = MathF.Max(sweptRad, MathF.Abs(branch.Curvature * branch.LengthM));

        return sweptRad * 180f / MathF.PI;
    }

    /// <summary>How far the furthest-reaching zebra runs, which on a town of square crossings is a road's width.</summary>
    static float Widest(Crossings crossings)
    {
        var spanM = 0f;
        foreach (var span in crossings.SpanM) spanM = MathF.Max(spanM, span);

        return spanM;
    }

    /// <summary>
    /// <b>How many of the town's zebras are the one a short road is crossed by</b> (WLK-10): a road whose two
    /// ends would be cut within a stride of each other is cut once between them instead, so this is the count
    /// of roads the figure caught rather than a second fact about the paint.
    /// </summary>
    static int Midway(Crossings crossings)
    {
        var midway = 0;
        foreach (var one in crossings.Midway)
        {
            if (one) midway++;
        }

        return midway;
    }

    /// <summary>
    /// <b>How far into its box the kerb of a road runs before the boundary leaves it</b>
    /// (<see cref="KerbEnds.Mark.OutM"/>): the middle of them, the furthest of them, and where that one
    /// stands — which is a walk's station and a driver's bar a metre further out again, both of them laid
    /// off the further of a road's two ends (TER-6, WLK-10).
    /// </summary>
    /// <remarks>
    /// <b>A reading and not a gate.</b> How deep a mouth is belongs to the box: a wide road meeting a narrow
    /// one at a shallow angle really does keep its kerb a long way in, so there is no figure here that a town
    /// is wrong for passing. What the reading is for is the other thing it catches — an end taken from a
    /// place the boundary was misread at stands tens of metres out, and being the furthest it is the one
    /// that carries the paint (<see cref="KerbEnds.Further"/>).
    /// </remarks>
    static (float MiddleM, float WorstM, int Road, Vector2 AtM) Reaching(KerbEnds ends)
    {
        var totalM = 0f;
        var worst = new KerbEnds.Mark(Vector2.Zero, -1, -1, -1, 0f);
        var standing = 0;
        Weigh(ends.Further);
        Weigh(ends.Nearer);
        return (standing == 0 ? 0f : totalM / standing, worst.OutM, worst.Road, worst.AtM);

        void Weigh(ReadOnlySpan<KerbEnds.Mark> set)
        {
            foreach (var end in set)
            {
                totalM += end.OutM;
                standing++;
                if (end.OutM > worst.OutM) worst = end;
            }
        }
    }

    /// <summary>
    /// <b>How far the widest zebra overruns the carriageway it crosses</b>, and how many overrun one at all.
    /// A zebra reaches the two kerbs its walk crosses between (WLK-10, TER-6), and at a mouth the ground a
    /// junction's movements are driven over reaches past the arm's own edge — so a band there is longer than
    /// its road is wide, and what it reaches over past the tarmac is the corner.
    /// </summary>
    /// <remarks>
    /// <b>A reading and not a gate.</b> Nothing refuses a band for overrunning, so how far one does is the
    /// instrument's to report — and it is the figure behind two arms of a tight junction laying paint over
    /// one another at the corner they share.
    /// </remarks>
    static (int Over, float WorstM) Overrunning(CityPlan plan, Crossings crossings)
    {
        var over = 0;
        var worstM = 0f;
        for (var crossing = 0; crossing < crossings.Count; crossing++)
        {
            var pastM = crossings.SpanM[crossing] - plan.Roads.WidthM[crossings.Road[crossing]];
            if (pastM <= LineTolerance.RoundingM) continue;

            over++;
            worstM = MathF.Max(worstM, pastM);
        }

        return (over, worstM);
    }

    static float Mean(ReadOnlySpan<float> figures)
    {
        if (figures.Length == 0) return 0f;

        var total = 0f;
        foreach (var figure in figures) total += figure;
        return total / figures.Length;
    }
}
