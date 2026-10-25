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
internal readonly record struct Crossing(Vector2 NearM, Vector2 FarM, bool Bridge, int Barrier, float AlongM)
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
    /// <summary>How far apart along a river or a railway the places a crossing is tried at stand.</summary>
    const float StepM = 20f;

    /// <summary>
    /// <b>Every bridge site</b> over every river the map holds, in order along each — <paramref name="clearM"/> being how
    /// far past the shore a bridgehead stands: the strip roads keep off it, and the ground the junction there takes.
    /// </summary>
    public static List<Crossing> Bridges(TownMap.CourseArrays courses, GroundShapes ground, WaterRules rules, Func<Vector2, bool> sea,
        Vector2 extentM, float clearM, SimConfig config)
    {
        var sites = new List<Crossing>();
        var stepM = config.Terrain.GroundStepM;
        for (var river = 0; river < courses.Count; river++)
        {
            if (courses.Kind[river] != TownMap.WaterBody.River) continue;

            var line = new Polyline([.. courses.CourseOf(river)]);
            for (var alongM = 0f; alongM <= line.LengthM; alongM += StepM)
            {
                var atM = line.At(alongM);
                if (!Inside(atM, extentM) || !ground.Waterside(atM) || sea(atM)) continue;

                var across = Heading.RightOf(line.Heading(alongM));
                if (BankM(ground, atM, across, stepM, extentM) is not { } nearM || BankM(ground, atM, -across, stepM, extentM) is not { } farM) continue;

                var site = new Crossing(atM - (across * (farM + clearM)), atM + (across * (nearM + clearM)), true, river, alongM);
                if (site.SpanM > config.CityGen.BridgeDeckLongestM) continue;
                if (!rules.Holds(site.NearM) || !rules.Holds(site.FarM) || !Inside(site.NearM, extentM) || !Inside(site.FarM, extentM)) continue;
                if (ground.Waterside(site.NearM) || ground.Waterside(site.FarM)) continue;
                if (!rules.Spans(site.NearM, site.FarM)) continue;

                sites.Add(site);
            }
        }

        return sites;
    }

    /// <summary>
    /// How far across from a point in the water its bank's shore ends, walked a step at a time — or none where the walk
    /// runs off the map first.
    /// </summary>
    static float? BankM(GroundShapes ground, Vector2 fromM, Vector2 across, float stepM, Vector2 extentM)
    {
        for (var offM = 0f; offM < 1000f; offM += stepM)
        {
            var atM = fromM + (across * offM);
            if (!Inside(atM, extentM)) return null;
            if (!ground.Waterside(atM)) return offM;
        }

        return null;
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
            for (var alongM = StepM; alongM < line.LengthM; alongM += StepM)
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
