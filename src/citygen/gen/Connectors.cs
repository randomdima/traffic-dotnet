using System.Numerics;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>The second stage: how the town is reached from outside</b> (GEN-61, GEN-63) — its core, the railways that cross the
/// map and the intercity roads that enter it, each laid once on the terrain and before anything of the town.
/// </summary>
/// <remarks>
/// <para>
/// <b>The core is where the town grew up</b>: dry ground in from the map's edge, near its middle, and a block or two off
/// its river or its harbour — which is where towns stand.
/// </para>
/// <para>
/// <b>An intercity road is routed, then drawn along its route as one curve</b> (<see cref="Routes"/>,
/// <see cref="RouteSpline"/>), round the sea and the railway and over them only at a crossing found for it, square and
/// short (<see cref="Crossings"/>, GEN-70). <b>In a town graded out to the country it passes the town</b>: in past the map's
/// edge, round the town's solid middle and out past the edge again, one curve nothing cuts. In a town built alike to its
/// edge it runs to the core, or to an intercity road already laid where it meets one first.
/// </para>
/// </remarks>
internal static class Connectors
{
    /// <summary>
    /// The town's ways in: the core, the railway's tracks, the intercity roads, and every place a road may cross the
    /// water or the railway, which the town's own links take as well (GEN-69).
    /// </summary>
    /// <param name="BypassM">
    /// The radius about the core the intercity roads pass the town at, or nought where they run into it instead (GEN-63).
    /// </param>
    internal sealed record Laid(
        Vector2 CoreM, TownMap.RailArrays Tracks, CityPlan.RailArrays Rails, List<Intercity> Roads, List<Crossing> Bridges,
        List<Crossing> LevelCrossings, float BypassM);

    /// <summary>
    /// One intercity road: its line from the map's edge, where its far end meets, and what it crosses — every one of them
    /// <see cref="LayoutEdge.MostLanes"/> each way (GEN-67).
    /// </summary>
    /// <param name="EndsOn">The intercity road its far end meets, or −1 where it ends at the core or past the map's edge.</param>
    /// <param name="Crossed">The bridges and level crossings its route took, in order (<see cref="Routes.Taken"/>).</param>
    internal sealed record Intercity(Polyline Line, int EndsOn, List<Crossing> Crossed);

    /// <param name="bypassed">
    /// Whether the intercity roads pass round the town's solid middle rather than into it — a town graded out to the
    /// country (GEN-76); one built alike to its edge has its roads run to its core.
    /// </param>
    public static Laid Lay(
        TownBrief brief, TerrainStage.Water water, SimConfig config, ulong seed, Func<Vector2, bool> sea, bool bypassed)
    {
        var extentM = new Vector2(brief.WidthM, brief.HeightM);
        var draw = new Rng(seed, Streams.Connectors);
        var wet = new GroundShapes(GroundPieces.None(seed, extentM, config.PavementWidthM).With(water.Rings), config);

        var coreM = Core(extentM, wet, water.Courses.Count > 0 || brief.Sea is not null, brief.Sea is null ? null : sea, config, ref draw);
        var tracks = Railways(brief, extentM, coreM, sea, config, ref draw);
        var rails = RailLines.Lay(tracks, extentM, config, wet);

        var ground = new GroundShapes(GroundPieces.None(seed, extentM, config.PavementWidthM).With(water.Rings).With(rails), config);
        var rules = TownGen.RulesOf(ground, rails, sea, config);
        var clearM = config.CityGen.WaterfrontClearM + TownGen.CorridorM(config, LayoutEdge.MostLanes);
        var bridges = Crossings.Bridges(water.Courses, ground, rules, sea, extentM, clearM, config);
        var levelCrossings = Crossings.LevelCrossings(rails, ground, rules, extentM, TownGen.CorridorM(config, LayoutEdge.MostLanes) + config.Rail.JunctionClearanceM, config);

        var bypassM = bypassed ? config.CityGen.TownSolidShare * FurthestDryM(coreM, extentM, wet, config) : 0f;
        var roads = Intercities(brief, extentM, coreM, bypassM, ground, rules, bridges, levelCrossings, sea, config, ref draw);
        return new Laid(coreM, tracks, rails, roads, bridges, levelCrossings, bypassM);
    }

    /// <summary>
    /// <b>How far the town's furthest dry ground stands from its core</b>: the intercity roads pass round
    /// <see cref="CityGenFigures.TownSolidShare"/> of it (GEN-63, GEN-76).
    /// </summary>
    static float FurthestDryM(Vector2 coreM, Vector2 extentM, GroundShapes wet, SimConfig config)
    {
        var grid = CellGrid.Over(extentM, config.CityGen.CoreCellM);
        var furthestM = 0f;
        for (var cell = 0; cell < grid.Count; cell++)
        {
            if (!wet.Waterside(grid.CentreOf(cell))) furthestM = MathF.Max(furthestM, Vector2.Distance(grid.CentreOf(cell), coreM));
        }

        return furthestM;
    }

    /// <summary>
    /// <b>The town's core</b>: of the dry places in from the map's edge by its inset, the one nearest the middle — and,
    /// where the map holds a river or a sea, near it: <b>near the sea where there is one</b>, a port's middle being its
    /// harbour and not the river upstream of it — moved by a draw so two towns of one shape differ.
    /// </summary>
    static Vector2 Core(Vector2 extentM, GroundShapes wet, bool water, Func<Vector2, bool>? sea, SimConfig config, ref Rng draw)
    {
        var figures = config.CityGen;
        var grid = CellGrid.Over(extentM, figures.CoreCellM);
        var offWaterM = OffWaterM(grid, wet.Waterside);
        var offHarbourM = sea is null ? offWaterM : OffWaterM(grid, atM => sea(atM) && wet.Waterside(atM));
        var landShare = LandShares(grid, offWaterM, figures.CoreLandReachM);
        var insetM = MathF.Min(extentM.X, extentM.Y) * config.CityGen.CoreInsetShare;
        var shortSideM = MathF.Min(extentM.X, extentM.Y);
        var (best, bestScore) = (-1, float.PositiveInfinity);
        for (var cell = 0; cell < grid.Count; cell++)
        {
            var atM = grid.CentreOf(cell);
            if (atM.X < insetM || atM.Y < insetM || atM.X > extentM.X - insetM || atM.Y > extentM.Y - insetM) continue;
            if (offWaterM[cell] < figures.CoreOffWaterLeastM || landShare[cell] < figures.CoreLandLeastShare) continue;

            var score = (Vector2.Distance(atM, extentM * 0.5f) / shortSideM) + (draw.NextFloat() * figures.CoreJitterShare);
            if (water) score += figures.CoreWaterWeight * MathF.Abs(offHarbourM[cell] - figures.CoreOffWaterBestM) / shortSideM;
            if (score < bestScore) (best, bestScore) = (cell, score);
        }

        // Where no place in from the edge stands far enough off the water, the driest place on the map: the core is dry
        // ground whatever the brief, or no road reaches it.
        if (best < 0)
        {
            for (var cell = 0; cell < grid.Count; cell++)
            {
                if (best < 0 || offWaterM[cell] > offWaterM[best]) best = cell;
            }
        }

        return grid.CentreOf(best);
    }

    /// <summary>How much of the ground within a reach of each cell is dry: a cell standing on the water's shore is wet.</summary>
    static float[] LandShares(CellGrid grid, float[] offWaterM, float reachM)
    {
        var reach = (int)MathF.Ceiling(reachM / grid.CellM);
        var share = new float[grid.Count];
        for (var cell = 0; cell < grid.Count; cell++)
        {
            var (column, row) = (cell % grid.Columns, cell / grid.Columns);
            var (dry, all) = (0, 0);
            for (var dy = -reach; dy <= reach; dy++)
            {
                for (var dx = -reach; dx <= reach; dx++)
                {
                    if ((dx * dx) + (dy * dy) > reach * reach || !grid.Holds(column + dx, row + dy)) continue;

                    all++;
                    dry += offWaterM[grid.Index(column + dx, row + dy)] > 0f ? 1 : 0;
                }
            }

            share[cell] = all == 0 ? 0f : dry / (float)all;
        }

        return share;
    }

    /// <summary>How far each cell stands off a water, stepped over the cells (an eight-way distance).</summary>
    static float[] OffWaterM(CellGrid grid, Func<Vector2, bool> water)
    {
        var offM = new float[grid.Count];
        var open = new PriorityQueue<int, float>();
        for (var cell = 0; cell < grid.Count; cell++)
        {
            offM[cell] = float.PositiveInfinity;
            if (!water(grid.CentreOf(cell))) continue;

            offM[cell] = 0f;
            open.Enqueue(cell, 0f);
        }

        while (open.TryDequeue(out var cell, out var atM))
        {
            if (atM > offM[cell]) continue;

            var (column, row) = (cell % grid.Columns, cell / grid.Columns);
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if ((dx | dy) == 0 || !grid.Holds(column + dx, row + dy)) continue;

                    var next = grid.Index(column + dx, row + dy);
                    var nextM = atM + (grid.CellM * ((dx != 0 && dy != 0) ? MathF.Sqrt(2f) : 1f));
                    if (nextM >= offM[next]) continue;

                    offM[next] = nextM;
                    open.Enqueue(next, nextM);
                }
            }
        }

        return offM;
    }

    /// <summary>
    /// <b>The railways</b>: each a line edge to edge passing the core a little off it, the lines of a map parallel and on
    /// alternate sides of it — along the coast where there is a sea, every line inland of the core and each further in than
    /// the last, so no track runs in it. A line that would run in the sea is drawn again further out, or left out; one
    /// nearer another than <see cref="CityGenFigures.RailLinesApartLeastM"/> is drawn again too, and where no draw stands
    /// apart the first dry one is laid — a looped line's way out leaves on its bearing turned by the loop, so every line
    /// parallel to its way in on that side crosses it, and the brief's count is kept over the spacing. Only the first
    /// carries the brief's loop.
    /// </summary>
    static TownMap.RailArrays Railways(TownBrief brief, Vector2 extentM, Vector2 coreM, Func<Vector2, bool> sea, SimConfig config, ref Rng draw)
    {
        var connectors = brief.Connectors;
        if (connectors.Railways == 0) return TownMap.RailArrays.None;

        var seaward = brief.Sea is { } seaBrief ? Heading.Unit(seaBrief.BearingDeg * MathF.PI / 180f) : Vector2.Zero;
        var baseRad = brief.Sea is not null ? Trig.Atan2(-Heading.RightOf(seaward).Y, -Heading.RightOf(seaward).X) : draw.NextFloat(0f, MathF.PI);

        // <b>One bearing for every line of the map</b>, so they run parallel: drawn a line each, two lines a turn apart
        // closed on each other across the map and crossed.
        var figures = config.CityGen;
        var bearingRad = baseRad + draw.NextFloat(-figures.RailTurnRad, figures.RailTurnRad);
        var side = Heading.RightOf(Heading.Unit(bearingRad));
        var lines = new List<TownMap.RailArrays>();
        var laidM = new PointCells(config.Grid.Main);
        for (var line = 0; line < connectors.Railways; line++)
        {
            TownMap.RailArrays? dry = null;
            for (var tries = 0; tries < figures.ConnectorTries; tries++)
            {
                // Alternate sides of the core, further out each pair — and where the sea lies to one side, every line inland
                // of it and each further in than the last. A line drawn again is drawn a little further out.
                var outward = (brief.Sea is not null ? line : line / 2) + 1f + (tries * figures.RailRetryOutShare);
                var sign = brief.Sea is not null ? -MathF.Sign(Vector2.Dot(side, seaward)) : (line % 2 == 0 ? 1f : -1f);
                var passesM = coreM + (side * (sign * outward * figures.RailPassesCoreM));
                var shiftM = draw.NextFloat(-figures.RailLoopShiftShare, figures.RailLoopShiftShare) * MathF.Max(extentM.X, extentM.Y);
                var loopM = passesM + (Heading.Unit(bearingRad) * shiftM);
                var laid = RailLines.Author(
                    extentM, line == 0 && connectors.RailLoopDeg != 0f ? loopM : passesM, bearingRad, connectors.RailTracks,
                    line == 0 ? connectors.RailLoopDeg : 0f, figures.RailMeanderShare, line, config, ref draw);
                if (InTheSea(laid, extentM, sea)) continue;

                dry ??= laid;
                if (NearAny(laid, laidM, figures.RailLinesApartLeastM)) continue;

                dry = laid;
                break;
            }

            if (dry is not { } kept) continue;

            lines.Add(kept);
            for (var track = 0; track < kept.Count; track++)
            {
                foreach (var pointM in kept.TrackOf(track)) laidM.Add(pointM);
            }
        }

        return Joined(lines);
    }

    /// <summary>
    /// Whether a line comes nearer than <paramref name="apartM"/> to any line already laid — two railways of one town run
    /// apart, and a town is cut by them only where they are. <b>A crossing is a nearing too</b>: a track is drawn through
    /// points its chord tolerance apart (<see cref="RailFigures.ChordToleranceM"/>), a few metres, so a line crossing
    /// another passes one of its points nearer than any spacing worth keeping.
    /// </summary>
    static bool NearAny(TownMap.RailArrays tracks, PointCells laid, float apartM)
    {
        for (var track = 0; track < tracks.Count; track++)
        {
            foreach (var pointM in tracks.TrackOf(track))
            {
                if (laid.AnyWithin(pointM, apartM)) return true;
            }
        }

        return false;
    }

    static bool InTheSea(TownMap.RailArrays tracks, Vector2 extentM, Func<Vector2, bool> sea)
    {
        foreach (var pointM in tracks.PointM)
        {
            if (pointM.X >= 0f && pointM.Y >= 0f && pointM.X <= extentM.X && pointM.Y <= extentM.Y && sea(pointM)) return true;
        }

        return false;
    }

    /// <summary>Several lines' tracks as one railway's arrays, each line keeping its own number.</summary>
    static TownMap.RailArrays Joined(List<TownMap.RailArrays> lines)
    {
        if (lines.Count == 0) return TownMap.RailArrays.None;

        var (line, runs, offsets, pointM, level) = (new List<int>(), new List<TownMap.TrackRuns>(), new List<int> { 0 }, new List<Vector2>(), new List<byte>());
        foreach (var tracks in lines)
        {
            for (var track = 0; track < tracks.Count; track++)
            {
                line.Add(tracks.Line[track]);
                runs.Add(tracks.Runs[track]);
                pointM.AddRange(tracks.TrackOf(track));
                level.AddRange(tracks.Level.AsSpan(tracks.PointOffsets[track], tracks.PointOffsets[track + 1] - tracks.PointOffsets[track]));
                offsets.Add(pointM.Count);
            }
        }

        return new TownMap.RailArrays { Line = [.. line], Runs = [.. runs], PointOffsets = [.. offsets], PointM = [.. pointM], Level = [.. level] };
    }

    /// <summary>
    /// <b>The intercity roads</b>, spread round the core: each enters on dry ground where a line from the core on its
    /// bearing leaves the map — turned a little where that is wet or on the railway — routed across the land, and drawn
    /// along its route as one curve (<see cref="RouteSpline"/>). <b>Where the town is bypassed, the brief's ways in are paired
    /// into roads passing it</b> (GEN-63), spread round the land: each in past the map's edge and out past it again, touching
    /// the circle the town's solid middle stands in and never entering it — one curve edge to edge that nothing cuts — and
    /// carried over a road it crosses. <b>Where it is not, the two entering half the roads apart are one road</b>, edge to
    /// core to edge, and every other ends on the core or on the first road already laid. One no curve is drawn for is left
    /// out.
    /// </summary>
    static List<Intercity> Intercities(
        TownBrief brief, Vector2 extentM, Vector2 coreM, float bypassM, GroundShapes ground, WaterRules rules, List<Crossing> bridges,
        List<Crossing> levelCrossings, Func<Vector2, bool> sea, SimConfig config, ref Rng draw)
    {
        var count = brief.Connectors.IntercityRoads;
        var roads = new List<Intercity>();
        if (count == 0) return roads;

        var figures = config.CityGen;
        var clearM = figures.WaterfrontClearM + TownGen.CorridorM(config, LayoutEdge.MostLanes);
        var railClearM = TownGen.CorridorM(config, LayoutEdge.MostLanes) + (config.Rail.BallastWidthM * 0.5f) + config.Rail.TrackSpacingM;
        var tightestM = RoadLines.FloorRadiusM(config, RoadClass.Intercity) * figures.IntercityRoundingShare;

        // The search keeps off the water and the track by as much again as a curve drawn along its corners may stray
        // (RouteToleranceM); the curve itself is held to the clearance. It holds a crossing's line for the approach and a
        // radius past it, so the curve turning off the straight swings no nearer what the crossing crossed, and leaves a
        // radius again before another crossing, so the curve has the room to turn between two.
        var toleranceM = figures.RouteToleranceM;
        var routeGrid = CellGrid.Over(extentM, figures.RouteCellM);
        bool Barred(Vector2 atM) => Near(ground, atM, clearM + toleranceM) || ground.RailWithin(atM, railClearM + toleranceM);
        var crossings = bridges.Concat(levelCrossings).Where(HeldClear).ToList();
        var routes = new Routes(routeGrid, Barred, crossings, figures.BridgeApproachM, tightestM);

        var grid = routes.Grid;
        var onARoad = new int[grid.Count];
        Array.Fill(onARoad, -1);
        var core = grid.IndexOf(coreM);
        var firstRad = draw.NextFloat(0f, MathF.Tau);

        // <b>Where there is a sea, the roads enter spread round the land's edge and not round the whole</b>: a bearing that
        // leaves the map through the sea is no way in, and a port's roads all come from inland.
        var landward = new List<float>();
        if (brief.Sea is not null)
        {
            for (var step = 0; step < LandwardSteps; step++)
            {
                var rad = firstRad + (MathF.Tau * step / LandwardSteps);
                var heading = Heading.Unit(rad);
                if (!routes.Blocked(OnTheEdge(coreM + (heading * ToTheEdgeM(coreM, heading, extentM)), extentM))) landward.Add(rad);
            }
        }

        // The brief's roads are its ways in, two to each road passing the town.
        if (bypassM > 0f)
        {
            var (bearings, passing) = (Bearings(), (count + 1) / 2);
            for (var road = 0; road < passing; road++) Passing(road, passing, bearings, ref draw);
            return roads;
        }

        // Half the roads apart: opposite where there are an even number, and as near it as an odd number allows.
        var (first, second) = (0, count / 2);
        var paired = second > 0 && ThroughTheCore(ref draw);
        for (var road = 0; road < count; road++)
        {
            if (paired && (road == first || road == second)) continue;

            for (var tries = 0; tries < figures.ConnectorTries; tries++)
            {
                if (Route(road, tries, cell => cell == core || onARoad[cell] >= 0, ref draw) is not { } corners) continue;

                var endsOn = onARoad[grid.IndexOf(corners[^1].AtM)];
                var endM = endsOn < 0 ? coreM : roads[endsOn].Line.At(roads[endsOn].Line.Nearest(corners[^1].AtM).AlongM);
                EndAt(corners, endM);

                // Off every road already laid but the one it arrives on, where it ends.
                if (Lay(corners, -1, endsOn, atM => Clear(atM) && (onARoad[grid.IndexOf(atM)] < 0 || Vector2.Distance(atM, endM) < tightestM))) break;
            }
        }

        return roads;

        // <b>Every bearing a road may enter or leave on, in order round the land's edge</b>: the whole of the turn, or where
        // there is a sea only those leaving the map over land, begun past the widest stretch of sea so the land is one run.
        List<float> Bearings()
        {
            var rads = new List<float>();
            for (var step = 0; step < LandwardSteps; step++)
            {
                var rad = firstRad + (MathF.Tau * step / LandwardSteps);
                if (brief.Sea is null || landward.Contains(rad)) rads.Add(rad);
            }

            if (rads.Count < 2) return rads;

            var (widest, widestRad) = (0, 0f);
            for (var at = 0; at < rads.Count; at++)
            {
                var gapRad = MathF.IEEERemainder(rads[(at + 1) % rads.Count] - rads[at], MathF.Tau);
                if (gapRad < 0f) gapRad += MathF.Tau;
                if (gapRad > widestRad) (widest, widestRad) = (at, gapRad);
            }

            return [.. rads.Skip(widest + 1), .. rads.Take(widest + 1)];
        }

        // <b>One road passing the town</b>: in past the map's edge, routed to touch the circle round the town's solid middle
        // on its own bearing, and out past the edge again IntercitySpanDeg round from where it came in, or at the land's end
        // where there is a sea — routed off the ground well inside the circle, and off the roads already laid but over them at
        // a span carried over each, and drawn as one curve held off that ground too. A try is turned a little either way and
        // further each pair.
        void Passing(int road, int passing, List<float> bearings, ref Rng draw)
        {
            var insideM = bypassM * figures.BypassInsideShare;
            var over = new List<Crossing>(crossings);
            var overM = MathF.Max(TownGen.OverHalfSpanM(config), grid.CellM * (figures.RouteMarkCells + 1));
            for (var laid = 0; laid < roads.Count; laid++)
            {
                over.AddRange(Crossings.OverARoad(roads[laid].Line, laid, rules, extentM, overM, figures.RouteCellM).Where(HeldClear));
            }

            var around = new Routes(
                routeGrid, atM => Barred(atM) || Vector2.Distance(atM, coreM) < insideM || onARoad[grid.IndexOf(atM)] >= 0, over,
                figures.BridgeApproachM, tightestM);
            var steps = bearings.Count;
            var middle = (((2 * road) + 1) * steps) / (2 * passing);
            var halfSteps = (int)(figures.IntercitySpanDeg * 0.5f * LandwardSteps / 360f);
            var (enters, leaves) = brief.Sea is null
                ? ((middle - halfSteps + steps) % steps, (middle + halfSteps) % steps)
                : (Math.Max(0, middle - halfSteps), Math.Min(steps - 1, middle + halfSteps));
            for (var tries = 0; tries < figures.ConnectorTries; tries++)
            {
                var turned = ((tries % 2 == 0 ? -1f : 1f) * ((tries + 1) / 2) * figures.IntercityEntryTurnRad)
                             + draw.NextFloat(-figures.IntercityEntryJitterRad, figures.IntercityEntryJitterRad);
                var (entersM, leavesM) = (EdgeOn(bearings[enters] + turned), EdgeOn(bearings[leaves] + turned));
                if (routes.Blocked(entersM) || routes.Blocked(leavesM)) continue;
                if (OnTheCircle(around, bypassM, Heading.Unit(bearings[middle] + turned)) is not { } touchM) continue;

                var (touch, goal) = (grid.IndexOf(touchM), grid.IndexOf(leavesM));
                if (around.Find(entersM, cell => cell == touch, touchM, figures.RouteCrossingWeight) is not { Count: >= 2 } inbound) continue;
                if (around.Find(touchM, cell => cell == goal, leavesM, figures.RouteCrossingWeight) is not { Count: >= 2 } outbound) continue;

                // The two routes meet at the place it touches, and a corner of either within a cell of it is that place.
                inbound[0] = new RouteCorner(entersM, inbound[0].Crossing);
                EndAt(inbound, touchM);
                inbound.RemoveAll(corner => corner.Crossing < 0 && corner.AtM != touchM && Vector2.Distance(corner.AtM, touchM) < figures.RouteCellM);
                EndAt(outbound, leavesM);
                inbound.AddRange(outbound.Where(corner => corner.Crossing >= 0 || Vector2.Distance(corner.AtM, touchM) >= figures.RouteCellM));
                // A curve that cannot turn between two crossings there is routed round that place on the next try.
                if (Lay(inbound, -1, -1, atM => Clear(atM) && Vector2.Distance(atM, coreM) >= insideM, around, strayM => around.Bar(strayM, tightestM * figures.RouteStrayBarredRadii))) return;
            }
        }

        Vector2 EdgeOn(float rad)
        {
            var heading = Heading.Unit(rad);
            return OnTheEdge(coreM + (heading * ToTheEdgeM(coreM, heading, extentM)), extentM);
        }

        // A place on the circle the roads pass at, along one way from the core, moved in and out along it to the nearest dry ground
        // clear of the track and in from the map's edge — or none where the circle stands in the sea or off the map.
        Vector2? OnTheCircle(Routes around, float radiusM, Vector2 outward)
        {
            var onM = coreM + (outward * radiusM);
            var marginM = figures.RouteCellM * 2f;
            if (onM.X < marginM || onM.Y < marginM || onM.X > extentM.X - marginM || onM.Y > extentM.Y - marginM || sea(onM)) return null;

            for (var moved = 0; moved * figures.RouteCellM <= radiusM * (1f - figures.BypassInsideShare); moved++)
            {
                foreach (var sign in (ReadOnlySpan<float>)[1f, -1f])
                {
                    var atM = onM + (outward * (sign * moved * figures.RouteCellM));
                    if (atM.X < marginM || atM.Y < marginM || atM.X > extentM.X - marginM || atM.Y > extentM.Y - marginM) continue;
                    if (!around.Blocked(atM) && Clear(atM)) return atM;
                }
            }

            return null;
        }

        // <b>The pair as one road through the core</b>: each one's routes to the core tried against each of the other's, the
        // second's walked back out — the first pair one curve is drawn along.
        bool ThroughTheCore(ref Rng draw)
        {
            var leaving = new List<List<RouteCorner>?>();
            for (var arrivingTry = 0; arrivingTry < figures.ConnectorTries; arrivingTry++)
            {
                if (Route(first, arrivingTry, cell => cell == core, ref draw) is not { } arriving) continue;

                for (var leavingTry = 0; leavingTry < figures.ConnectorTries; leavingTry++)
                {
                    if (leavingTry == leaving.Count) leaving.Add(Route(second, leavingTry, cell => cell == core, ref draw));
                    if (leaving[leavingTry] is not { } back) continue;

                    var corners = new List<RouteCorner>(arriving);
                    EndAt(corners, coreM);
                    var atTheCore = corners.Count - 1;
                    for (var at = back[^1].Crossing < 0 ? back.Count - 2 : back.Count - 1; at >= 0; at--) corners.Add(back[at]);

                    if (Lay(corners, atTheCore, -1, Clear)) return true;
                }
            }

            return false;
        }

        // A route in from the map's edge on a road's bearing, turned off it either way in turn and further each pair of
        // tries: 0, +1, −1, +2, −2 … A route found where it starts is one entering on a road already laid: a stub, and none.
        List<RouteCorner>? Route(int road, int tries, Func<int, bool> goal, ref Rng draw)
        {
            var turned = (tries % 2 == 0 ? -1f : 1f) * ((tries + 1) / 2) * figures.IntercityEntryTurnRad;
            var jitterRad = draw.NextFloat(-figures.IntercityEntryJitterRad, figures.IntercityEntryJitterRad);
            var spreadRad = landward.Count > 0
                ? landward[road * landward.Count / count]
                : firstRad + (MathF.Tau * road / count);
            var heading = Heading.Unit(spreadRad + turned + jitterRad);
            var entersM = OnTheEdge(coreM + (heading * ToTheEdgeM(coreM, heading, extentM)), extentM);
            if (routes.Blocked(entersM)) return null;

            return routes.Find(entersM, goal, coreM, figures.RouteCrossingWeight) is { Count: >= 2 } corners ? corners : null;
        }

        // Its crossings read off the routes it was found over.
        bool Lay(
            List<RouteCorner> corners, int through, int endsOn, Func<Vector2, bool> clear, Routes? foundOver = null, Action<Vector2>? strayed = null)
        {
            var (leaves, arrives) = (Inward(corners[0].AtM, extentM), -Inward(corners[^1].AtM, extentM));
            if (RouteSpline.Of(corners, through, leaves, arrives, clear, tightestM, figures.IntercityChordToleranceM, figures.RouteCellM, strayed) is not { } line) return false;

            roads.Add(new Intercity(line, endsOn, (foundOver ?? routes).Taken(corners)));
            Mark(grid, line, roads.Count - 1, grid.CellM * figures.RouteMarkCells, onARoad);
            return true;
        }

        // A crossing whose line runs clear from the end of its approach to the end of its room, either side: a road held
        // on it there strays onto nothing.
        bool HeldClear(Crossing crossing)
        {
            var (fromM, toM) = (figures.BridgeApproachM, figures.BridgeApproachM + tightestM);
            for (var alongM = fromM; alongM <= toM; alongM += figures.RouteCellM / ReadsPerCell)
            {
                if (!Clear(crossing.NearApproachM(alongM)) || !Clear(crossing.FarApproachM(alongM))) return false;
            }

            return true;
        }

        // As far again as the line drawn off the curve may stand off it.
        bool Clear(Vector2 atM) =>
            atM.X >= 0f && atM.Y >= 0f && atM.X <= extentM.X && atM.Y <= extentM.Y
            && !Near(ground, atM, clearM + figures.IntercityChordToleranceM)
            && !ground.RailWithin(atM, railClearM + figures.IntercityChordToleranceM);
    }

    /// <summary>How many times a route's cell a crossing's held line is read at for whether it runs clear.</summary>
    const int ReadsPerCell = 4;

    /// <summary>How many bearings round the core are asked whether they leave the map over land: a degree apart.</summary>
    const int LandwardSteps = 360;

    /// <summary>The way into the map square to the edge a place stands on, or zero off every edge.</summary>
    static Vector2 Inward(Vector2 atM, Vector2 extentM) =>
        atM.X <= 0f ? Vector2.UnitX : atM.X >= extentM.X ? -Vector2.UnitX
        : atM.Y <= 0f ? Vector2.UnitY : atM.Y >= extentM.Y ? -Vector2.UnitY
        : Vector2.Zero;

    /// <summary>A route ended at a place: its last cell moved there, or the place added past the crossing it ended on.</summary>
    static void EndAt(List<RouteCorner> corners, Vector2 atM)
    {
        if (corners[^1].Crossing < 0) corners[^1] = new RouteCorner(atM, -1);
        else corners.Add(new RouteCorner(atM, -1));
    }

    /// <summary>Every cell within a reach of a road's line, marked as that road's.</summary>
    static void Mark(CellGrid grid, Polyline line, int road, float reachM, int[] onARoad)
    {
        for (var point = 0; point + 1 < line.PointsM.Length; point++)
        {
            grid.Band(line.PointsM[point], line.PointsM[point + 1], reachM, cell => onARoad[cell] = road);
        }
    }

    /// <summary>Whether the water or its shore stands within a reach of a place: the place and eight round it, asked.</summary>
    public static bool Near(GroundShapes ground, Vector2 atM, float reachM)
    {
        if (ground.Waterside(atM)) return true;

        for (var at = 0; at < 8; at++)
        {
            if (ground.Waterside(atM + (Heading.Unit(at * MathF.Tau / 8f) * reachM))) return true;
        }

        return false;
    }

    /// <summary>
    /// A place a line reached the map's edge at, put on the edge exactly: a road runs off the map at a node on its edge
    /// (GEN-2b), and a float a hair past it stands off the map.
    /// </summary>
    static Vector2 OnTheEdge(Vector2 atM, Vector2 extentM)
    {
        var x = MathF.Abs(atM.X) < 0.5f ? 0f : MathF.Abs(atM.X - extentM.X) < 0.5f ? extentM.X : Math.Clamp(atM.X, 0f, extentM.X);
        var y = MathF.Abs(atM.Y) < 0.5f ? 0f : MathF.Abs(atM.Y - extentM.Y) < 0.5f ? extentM.Y : Math.Clamp(atM.Y, 0f, extentM.Y);
        return new Vector2(x, y);
    }

    /// <summary>How far a line from a place on the map runs on a heading before it leaves it.</summary>
    static float ToTheEdgeM(Vector2 fromM, Vector2 heading, Vector2 extentM)
    {
        var runM = float.PositiveInfinity;
        if (heading.X > 1e-6f) runM = MathF.Min(runM, (extentM.X - fromM.X) / heading.X);
        if (heading.X < -1e-6f) runM = MathF.Min(runM, -fromM.X / heading.X);
        if (heading.Y > 1e-6f) runM = MathF.Min(runM, (extentM.Y - fromM.Y) / heading.Y);
        if (heading.Y < -1e-6f) runM = MathF.Min(runM, -fromM.Y / heading.Y);
        return runM;
    }
}
