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
/// <b>The core is the place the intercity roads converge on</b>: dry ground in from the map's edge, near its middle, and
/// near the water where the map has a river — which is where towns stand.
/// </para>
/// <para>
/// <b>An intercity road is routed, not drawn</b> (<see cref="Routes"/>): from where it enters past the map's edge to the
/// core, or to an intercity road already laid where it meets one first — so two roads entering on one side are one
/// before they reach the town — round the sea and the railway and over them only at a crossing found for it, square
/// and short (<see cref="Crossings"/>, GEN-70).
/// </para>
/// </remarks>
internal static class Connectors
{
    /// <summary>How far a railway wanders off its bearing, as a share of the map's short side.</summary>
    const float RailMeanderShare = 0.08f;

    /// <summary>How many places a railway or an intercity road is drawn at before it is left out.</summary>
    const int Tries = 12;

    /// <summary>How finely the core is looked for: the side of the square each place tried stands in.</summary>
    const float CoreCellM = 50f;

    /// <summary>How far off the water the core stands at the least, and how far off it the core is best placed.</summary>
    const float CoreOffWaterLeastM = 200f, CoreOffWaterBestM = 400f;

    /// <summary>How many times its own length a crossing costs a route, which keeps a road off a barrier it can go round.</summary>
    const float CrossingWeight = 3f;

    /// <summary>
    /// The town's ways in: the core, the railway's tracks, the intercity roads, and every place a road may cross the
    /// water or the railway, which the town's own links take as well (GEN-69).
    /// </summary>
    internal sealed record Laid(
        Vector2 CoreM, TownMap.RailArrays Tracks, CityPlan.RailArrays Rails, List<Intercity> Roads, List<Crossing> Bridges,
        List<Crossing> LevelCrossings);

    /// <summary>One intercity road: its line from the map's edge, its lanes each way, and where its far end meets.</summary>
    /// <param name="EndsOn">The intercity road its far end meets, or −1 where it ends at the core.</param>
    internal sealed record Intercity(Polyline Line, int Lanes, int EndsOn);

    public static Laid Lay(
        TownBrief brief, TerrainStage.Water water, SimConfig config, ulong seed, Func<Vector2, bool> sea)
    {
        var extentM = new Vector2(brief.WidthM, brief.HeightM);
        var draw = new Rng(seed, Streams.Connectors);
        var wet = new GroundShapes(GroundPieces.None(seed, extentM, config.PavementWidthM).With(water.Rings), config);

        var coreM = Core(extentM, wet, water.Courses.Count > 0 && Array.IndexOf(water.Courses.Kind, TownMap.WaterBody.River) >= 0, config, ref draw);
        var tracks = Railways(brief, extentM, coreM, sea, config, ref draw);
        var rails = RailLines.Lay(tracks, extentM, config, wet);

        var ground = new GroundShapes(GroundPieces.None(seed, extentM, config.PavementWidthM).With(water.Rings).With(rails), config);
        var rules = TownGen.RulesOf(ground, rails, sea, config);
        var clearM = config.CityGen.WaterfrontClearM + TownGen.CorridorM(config, LayoutEdge.MostLanes);
        var bridges = Crossings.Bridges(water.Courses, ground, rules, sea, extentM, clearM, config);
        var levelCrossings = Crossings.LevelCrossings(rails, ground, rules, extentM, TownGen.CorridorM(config, LayoutEdge.MostLanes) + config.Rail.JunctionClearanceM, config);

        var roads = Intercities(brief, extentM, coreM, ground, rules, bridges, levelCrossings, config, ref draw);
        return new Laid(coreM, tracks, rails, roads, bridges, levelCrossings);
    }

    /// <summary>
    /// <b>The town's core</b>: of the dry places in from the map's edge by its inset, the one nearest the middle — and,
    /// where a river runs through the map, near it — moved by a draw so two towns of one shape differ.
    /// </summary>
    static Vector2 Core(Vector2 extentM, GroundShapes wet, bool river, SimConfig config, ref Rng draw)
    {
        var grid = CellGrid.Over(extentM, CoreCellM);
        var offWaterM = OffWaterM(grid, wet);
        var insetM = MathF.Min(extentM.X, extentM.Y) * config.CityGen.CoreInsetShare;
        var shortSideM = MathF.Min(extentM.X, extentM.Y);
        var (best, bestScore) = (-1, float.PositiveInfinity);
        for (var cell = 0; cell < grid.Count; cell++)
        {
            var atM = grid.CentreOf(cell);
            if (atM.X < insetM || atM.Y < insetM || atM.X > extentM.X - insetM || atM.Y > extentM.Y - insetM) continue;
            if (offWaterM[cell] < CoreOffWaterLeastM) continue;

            var score = (Vector2.Distance(atM, extentM * 0.5f) / shortSideM) + (draw.NextFloat() * 0.15f);
            if (river) score += 0.5f * MathF.Abs(offWaterM[cell] - CoreOffWaterBestM) / shortSideM;
            if (score < bestScore) (best, bestScore) = (cell, score);
        }

        return best < 0 ? extentM * 0.5f : grid.CentreOf(best);
    }

    /// <summary>How far each cell stands off the water and its shore, stepped over the cells (an eight-way distance).</summary>
    static float[] OffWaterM(CellGrid grid, GroundShapes wet)
    {
        var offM = new float[grid.Count];
        var open = new PriorityQueue<int, float>();
        for (var cell = 0; cell < grid.Count; cell++)
        {
            offM[cell] = float.PositiveInfinity;
            if (!wet.Waterside(grid.CentreOf(cell))) continue;

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
    /// alternate sides of it — along the coast and inland of the core where there is a sea, so no track runs in it. A line
    /// that would run in the sea is drawn again, or left out. Only the first carries the brief's loop.
    /// </summary>
    static TownMap.RailArrays Railways(TownBrief brief, Vector2 extentM, Vector2 coreM, Func<Vector2, bool> sea, SimConfig config, ref Rng draw)
    {
        var connectors = brief.Connectors;
        if (connectors.Railways == 0) return TownMap.RailArrays.None;

        var seaward = brief.Sea is { } seaBrief ? Heading.Unit(seaBrief.BearingDeg * MathF.PI / 180f) : Vector2.Zero;
        var baseRad = brief.Sea is not null ? Trig.Atan2(-Heading.RightOf(seaward).Y, -Heading.RightOf(seaward).X) : draw.NextFloat(0f, MathF.PI);
        var lines = new List<TownMap.RailArrays>();
        for (var line = 0; line < connectors.Railways; line++)
        {
            for (var tries = 0; tries < Tries; tries++)
            {
                var bearingRad = baseRad + draw.NextFloat(-0.2f, 0.2f);
                var side = Heading.RightOf(Heading.Unit(bearingRad));

                // Alternate sides of the core, further out each pair; inland of it where the sea lies to one side.
                var outward = (line / 2) + 1f + (tries * 0.25f);
                var sign = brief.Sea is not null ? -MathF.Sign(Vector2.Dot(side, seaward)) : (line % 2 == 0 ? 1f : -1f);
                var passesM = coreM + (side * (sign * outward * config.CityGen.RailPassesCoreM));
                var loopM = passesM + (Heading.Unit(bearingRad) * (draw.NextFloat(-0.3f, 0.3f) * MathF.Max(extentM.X, extentM.Y)));
                var laid = RailLines.Author(
                    extentM, line == 0 && connectors.RailLoopDeg != 0f ? loopM : passesM, bearingRad, connectors.RailTracks,
                    line == 0 ? connectors.RailLoopDeg : 0f, RailMeanderShare, line, config, ref draw);
                if (InTheSea(laid, extentM, sea)) continue;

                lines.Add(laid);
                break;
            }
        }

        return Joined(lines);
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
    /// bearing leaves the map — turned a little where that is wet or on the railway — and is routed to the core or to the
    /// first intercity road already laid it reaches. One no route reaches is left out.
    /// </summary>
    static List<Intercity> Intercities(
        TownBrief brief, Vector2 extentM, Vector2 coreM, GroundShapes ground, WaterRules rules, List<Crossing> bridges,
        List<Crossing> levelCrossings, SimConfig config, ref Rng draw)
    {
        var count = brief.Connectors.IntercityRoads;
        var roads = new List<Intercity>();
        if (count == 0) return roads;

        var clearM = config.CityGen.WaterfrontClearM + TownGen.CorridorM(config, LayoutEdge.MostLanes);
        var railClearM = TownGen.CorridorM(config, LayoutEdge.MostLanes) + (config.Rail.BallastWidthM * 0.5f) + config.Rail.TrackSpacingM;
        var radiusM = RoadLines.FloorRadiusM(config, RoadClass.Intercity) * 1.25f;

        // The approach a route holds off a crossing is the straight, and the corner after it is rounded out of the leg
        // beyond — so the straight still stands whole once the line is rounded, for the road off the bridge to leave on.
        var routes = new Routes(
            CellGrid.Over(extentM, config.CityGen.RouteCellM),
            atM => Near(ground, atM, clearM) || ground.RailWithin(atM, railClearM),
            [.. bridges, .. levelCrossings], config.CityGen.BridgeApproachM + radiusM);

        var grid = routes.Grid;
        var onARoad = new int[grid.Count];
        Array.Fill(onARoad, -1);
        var core = grid.IndexOf(coreM);
        var minor = (int)MathF.Round(count * brief.Connectors.MinorShare);
        var firstRad = draw.NextFloat(0f, MathF.Tau);
        for (var road = 0; road < count; road++)
        {
            for (var tries = 0; tries < Tries; tries++)
            {
                var bearingRad = firstRad + (MathF.Tau * road / count) + ((tries % 2 == 0 ? 1f : -1f) * (tries / 2) * 0.12f) + draw.NextFloat(-0.15f, 0.15f);
                var heading = Heading.Unit(bearingRad);
                var entersM = OnTheEdge(coreM + (heading * ToTheEdgeM(coreM, heading, extentM)), extentM);
                if (routes.Blocked(entersM)) continue;

                var corners = routes.Find(entersM, cell => cell == core || onARoad[cell] >= 0, coreM, CrossingWeight);
                if (corners is null) continue;

                var endsOn = onARoad[grid.IndexOf(corners[^1].AtM)];
                corners.Add(new RouteCorner(endsOn < 0 ? coreM : roads[endsOn].Line.At(roads[endsOn].Line.Nearest(corners[^1].AtM).AlongM), -1));
                var line = Routes.Line(corners, radiusM, config.CityGen.RouteCellM, grid.CellM);
                roads.Add(new Intercity(line, road < minor ? 1 : LayoutEdge.MostLanes, endsOn));
                Mark(grid, line, roads.Count - 1, onARoad);
                break;
            }
        }

        return roads;
    }

    /// <summary>Every cell a road's line passes through, marked as that road's.</summary>
    static void Mark(CellGrid grid, Polyline line, int road, int[] onARoad)
    {
        for (var point = 0; point + 1 < line.PointsM.Length; point++)
        {
            grid.Band(line.PointsM[point], line.PointsM[point + 1], grid.CellM * 0.75f, cell => onARoad[cell] = road);
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
