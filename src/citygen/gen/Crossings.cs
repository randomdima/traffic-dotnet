using System.Numerics;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen.Gen;

/// <summary>
/// <b>One place a road may cross a barrier</b>: a straight from one dry end to the other, square to what it crosses —
/// a river's own line (GEN-70) or a railway's (RAIL-5) — and the stretch a road holds that line for either side of it.
/// </summary>
/// <param name="NearM">Where the crossing begins: a bridgehead, or the near end of the ground a level crossing takes.</param>
/// <param name="FarM">And where it ends.</param>
/// <param name="Bridge">Whether it is a bridge over a river (GEN-14a) rather than a road over a railway at grade.</param>
/// <param name="Barrier">Which river (its course) or which railway line it crosses.</param>
/// <param name="AlongM">How far along that river or railway it stands, which is what spaces two of them apart.</param>
/// <param name="Over">
/// Whether the road is carried over what it crosses on a span of its own (<see cref="LayoutEdge.Over"/>): a road between
/// districts over an intercity road (GEN-68), whose <paramref name="Barrier"/> is that road.
/// </param>
internal readonly record struct Crossing(Vector2 NearM, Vector2 FarM, bool Bridge, int Barrier, float AlongM, bool Over = false)
{
    public Vector2 Unit => Vector2.Normalize(FarM - NearM);

    public float SpanM => Vector2.Distance(NearM, FarM);

    /// <summary>Where a road arriving at the near end holds the crossing's line from (<see cref="CityGenFigures.BridgeApproachM"/>).</summary>
    public Vector2 NearApproachM(float approachM) => NearM - (Unit * approachM);

    public Vector2 FarApproachM(float approachM) => FarM + (Unit * approachM);

    public Crossing Reversed => this with { NearM = FarM, FarM = NearM };
}

/// <summary>
/// <b>Every place a road may cross the water or the railway</b> (GEN-70, RAIL-5), found once off the ground and handed
/// to whatever lays a road across one: an intercity road's route (<see cref="Routes"/>) and a link between two districts
/// a barrier parts (<see cref="TownGen"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>A bridge site is laid on its river's own normal</b>, read off the river's line where it stands, so every bridge
/// crosses square by construction and short — the width of the water there, its shore, the strip every road keeps off
/// the shore (GEN-71) and a bridgehead's own ground either side. One longer than the deck a town builds is no site
/// (GEN-14a), nor one with either end wet, in the sea, or on the railway.
/// </para>
/// <para>
/// <b>A level crossing is laid on the railway's normal</b>, as long as both tracks, the ballast and the clearance a junction
/// keeps off it take, and never where the line is on a bridge.
/// </para>
/// </remarks>
internal static class Crossings
{
    /// <summary>
    /// <b>Every bridge site</b> over every river the map holds, in order along each — <paramref name="clearM"/> being how
    /// far past the shore a bridgehead stands: the strip roads keep off it, and the ground the junction there takes. Tried
    /// a route's cell apart (<see cref="CityGenFigures.RouteCellM"/>), which is as finely as a route can tell two apart.
    /// </summary>
    /// <remarks>
    /// <b>Square to every river it spans and not to its own alone</b> (GEN-70): where a tributary runs into its parent, its
    /// last stretch lies in the parent's water, and a site laid on the tributary's normal there spans the parent at the
    /// angle the two meet at.
    /// </remarks>
    public static List<Crossing> Bridges(TownMap.CourseArrays courses, GroundShapes ground, WaterRules rules, Func<Vector2, bool> sea,
        Vector2 extentM, float clearM, SimConfig config)
    {
        var sites = new List<Crossing>();
        var stepM = config.Terrain.GroundStepM;
        var skewMost = MathF.Sin(config.CityGen.BridgeSkewMostDeg * MathF.PI / 180f);
        var rivers = new List<(int Course, Polyline Line)>();
        var riverOf = new List<int>();
        var lines = new SegmentCells(config.CityGen.BridgeDeckLongestM);
        var crossed = new List<(float Along, int Segment, Vector2 AtM)>();
        for (var river = 0; river < courses.Count; river++)
        {
            if (courses.Kind[river] != TownMap.WaterBody.River) continue;

            var pointsM = courses.CourseOf(river);
            rivers.Add((river, new Polyline([.. pointsM])));
            for (var point = 0; point + 1 < pointsM.Length; point++)
            {
                lines.Add(pointsM[point], pointsM[point + 1]);
                riverOf.Add(river);
            }
        }

        foreach (var (river, line) in rivers)
        {
            for (var alongM = 0f; alongM <= line.LengthM; alongM += config.CityGen.RouteCellM)
            {
                var atM = line.At(alongM);
                if (!Inside(atM, extentM) || !ground.Waterside(atM) || sea(atM)) continue;

                var across = Heading.RightOf(line.Heading(alongM));
                var reachM = config.CityGen.BridgeDeckLongestM;
                if (BankM(ground, atM, across, stepM, reachM, extentM) is not { } nearM || BankM(ground, atM, -across, stepM, reachM, extentM) is not { } farM) continue;

                var site = new Crossing(atM - (across * (farM + clearM)), atM + (across * (nearM + clearM)), true, river, alongM);
                if (site.SpanM > config.CityGen.BridgeDeckLongestM) continue;
                if (!rules.Holds(site.NearM) || !rules.Holds(site.FarM) || !Inside(site.NearM, extentM) || !Inside(site.FarM, extentM)) continue;
                if (ground.Waterside(site.NearM) || ground.Waterside(site.FarM)) continue;
                if (!rules.Spans(site.NearM, site.FarM) || !SquareToEvery(site, river)) continue;

                sites.Add(site);
            }
        }

        return sites;

        // Whether a site crosses every other river whose line it spans no further off that river's normal than a bridge may.
        bool SquareToEvery(Crossing site, int own)
        {
            lines.Crossing(site.NearM, site.FarM, crossed);
            foreach (var (_, segment, _) in crossed)
            {
                if (riverOf[segment] == own) continue;

                var (fromM, toM) = lines[segment];
                if (MathF.Abs(Vector2.Dot(site.Unit, Vector2.Normalize(toM - fromM))) > skewMost) return false;
            }

            return true;
        }
    }

    /// <summary>
    /// How far across from a point in the water its bank's shore ends, walked a step at a time — or none where the walk
    /// runs off the map, or further than <paramref name="reachM"/>, first.
    /// </summary>
    static float? BankM(GroundShapes ground, Vector2 fromM, Vector2 across, float stepM, float reachM, Vector2 extentM)
    {
        for (var offM = 0f; offM < reachM; offM += stepM)
        {
            var atM = fromM + (across * offM);
            if (!Inside(atM, extentM)) return null;
            if (!ground.Waterside(atM)) return offM;
        }

        return null;
    }

    /// <summary>
    /// <b>Every place a road may cross an intercity road already laid</b> (GEN-63, GEN-68): square to its line, carried over
    /// it, and <paramref name="reachM"/> either side of it — every step along it but its ends.
    /// </summary>
    public static List<Crossing> OverARoad(Polyline line, int road, WaterRules rules, Vector2 extentM, float reachM, float stepM)
    {
        var sites = new List<Crossing>();
        for (var alongM = stepM; alongM < line.LengthM - stepM; alongM += stepM)
        {
            var atM = line.At(alongM);
            var across = Heading.RightOf(line.Heading(alongM));
            var site = new Crossing(atM - (across * reachM), atM + (across * reachM), false, road, alongM, Over: true);
            if (!Inside(site.NearM, extentM) || !Inside(site.FarM, extentM)) continue;
            if (!rules.Holds(site.NearM) || !rules.Holds(site.FarM) || rules.Wets(site.NearM, site.FarM)) continue;

            sites.Add(site);
        }

        return sites;
    }

    /// <summary>
    /// <b>Every place a road may cross a railway at grade</b>, square to its line: as long as the tracks, the ballast and
    /// the clearance a junction keeps off it, <paramref name="clearM"/>, take either side.
    /// </summary>
    public static List<Crossing> LevelCrossings(CityPlan.RailArrays rails, GroundShapes ground, WaterRules rules, Vector2 extentM, float clearM,
        SimConfig config)
    {
        var sites = new List<Crossing>();
        var reachM = config.Rail.TrackSpacingM + (config.Rail.BallastWidthM * 0.5f) + clearM;
        var seen = new HashSet<int>();
        for (var track = 0; track < rails.Count; track++)
        {
            // One line's tracks are crossed together, off its first track.
            if (!seen.Add(rails.Line[track])) continue;

            var lineM = rails.TrackOf(track);
            var levels = rails.Level.AsSpan(rails.PointOffsets[track], lineM.Length);
            var line = new Polyline([.. lineM]);
            for (var alongM = config.CityGen.RouteCellM; alongM < line.LengthM; alongM += config.CityGen.RouteCellM)
            {
                var atM = line.At(alongM);
                if (!Inside(atM, extentM) || OnABridge(line, levels, alongM)) continue;

                var across = Heading.RightOf(line.Heading(alongM));
                var site = new Crossing(atM - (across * reachM), atM + (across * reachM), false, rails.Line[track], alongM);
                if (!Inside(site.NearM, extentM) || !Inside(site.FarM, extentM)) continue;
                if (!rules.Holds(site.NearM) || !rules.Holds(site.FarM) || rules.Wets(site.NearM, site.FarM)) continue;

                sites.Add(site);
            }
        }

        return sites;
    }

    static bool OnABridge(Polyline line, ReadOnlySpan<byte> levels, float alongM)
    {
        var at = Array.BinarySearch(line.AlongM, alongM);
        var chord = Math.Clamp(at >= 0 ? at : ~at - 1, 0, levels.Length - 1);
        return levels[chord] != TownMap.RailArrays.AtGrade
               || (chord > 0 && levels[chord - 1] != TownMap.RailArrays.AtGrade)
               || (chord + 1 < levels.Length && levels[chord + 1] != TownMap.RailArrays.AtGrade);
    }

    static bool Inside(Vector2 atM, Vector2 extentM) => atM.X > 0f && atM.Y > 0f && atM.X < extentM.X && atM.Y < extentM.Y;
}
