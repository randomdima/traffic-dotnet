using System.Numerics;
using TrafficSimulation.CityGen.Gen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.Core.Simulation;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The railway as the plan holds it</b> (RAIL-2, RAIL-4): a brief's tracks drawn once when its map is authored
/// (<see cref="Author"/>), and a map's tracks laid with the ballast's band struck round each (<see cref="Lay"/>) — the
/// rings the ground answers the railway off and draws it from (TER-7).
/// </summary>
/// <remarks>
/// <b>A line is a chain of points close enough that no chord stands off the curve by more than a tolerance</b>, so a
/// train placed along it by its points is placed along the curve. The band is that chain moved out either side, which
/// on a track bent no tighter than its least radius is a band one width the whole way.
/// </remarks>
internal static class RailLines
{
    /// <summary>The second wave's period, as a share of the first's: a line wanders on two, so it is not a sine.</summary>
    const float SecondWaveShare = 0.45f;

    /// <summary>How much of the wander is the second wave's.</summary>
    const float SecondWaveWeight = 0.4f;

    /// <summary>
    /// <b>A brief's railway, drawn once</b>: a line through the town on the brief's bearing, off its middle by its
    /// offset, wandering as far as its least radius allows, and run on past the map at both ends far enough for a whole
    /// train to stand off it (TRN-1). A double line is two tracks either side of that, each run one way, on the right
    /// as it is faced.
    /// </summary>
    public static TownMap.RailArrays Author(TownBrief brief, SimConfig config, ref Rng draw)
    {
        if (brief.RailTracks == 0) return TownMap.RailArrays.None;

        var rail = config.Rail;
        var extentM = new Vector2(brief.WidthM, brief.HeightM);
        var shortSideM = MathF.Min(extentM.X, extentM.Y);
        var bearing = Heading.Unit(brief.RailBearingDeg * MathF.PI / 180f);
        var across = Heading.RightOf(bearing);
        var halfRunM = extentM.Length() + rail.RunOffM;
        var middleM = (extentM * 0.5f) + (across * (brief.RailOffsetShare * shortSideM));

        // A sum of sines bends hardest at the sum of what each bends, so the wander is held to what the least radius allows.
        var periodM = extentM.Length();
        var (firstPerM, secondPerM) = (MathF.Tau / periodM, MathF.Tau / (periodM * SecondWaveShare));
        var bendPerWanderM = ((firstPerM * firstPerM) + (SecondWaveWeight * secondPerM * secondPerM)) / (1f + SecondWaveWeight);
        var wanderM = MathF.Min(brief.RailMeander * shortSideM, 1f / (rail.LeastRadiusM * bendPerWanderM));
        var (firstPhase, secondPhase) = (draw.NextFloat(0f, MathF.Tau), draw.NextFloat(0f, MathF.Tau));

        // A chord of a curve stands off it by its square over eight radii, which is the sagitta.
        var stepM = MathF.Sqrt(8f * rail.LeastRadiusM * rail.ChordToleranceM);
        var points = (int)MathF.Ceiling(2f * halfRunM / stepM) + 1;
        var centreM = new Vector2[points];
        for (var point = 0; point < points; point++)
        {
            var alongM = -halfRunM + (2f * halfRunM * point / (points - 1));
            var wave = (MathF.Sin((alongM * firstPerM) + firstPhase) + (SecondWaveWeight * MathF.Sin((alongM * secondPerM) + secondPhase)))
                       / (1f + SecondWaveWeight);
            centreM[point] = middleM + (bearing * alongM) + (across * (wave * wanderM));
        }

        if (brief.RailTracks == 1)
        {
            return new TownMap.RailArrays
            {
                Line = [0], Runs = [TownMap.TrackRuns.Both], PointOffsets = [0, points], PointM = centreM, Level = new byte[points],
            };
        }

        var halfSpacingM = rail.TrackSpacingM * 0.5f;
        var pointM = new Vector2[points * 2];
        for (var point = 0; point < points; point++)
        {
            var side = SideAt(centreM, point);
            pointM[point] = centreM[point] + (side * halfSpacingM);
            pointM[points + point] = centreM[point] - (side * halfSpacingM);
        }

        return new TownMap.RailArrays
        {
            Line = [0, 0], Runs = [TownMap.TrackRuns.Forward, TownMap.TrackRuns.Backward], PointOffsets = [0, points, points * 2],
            PointM = pointM, Level = new byte[points * 2],
        };
    }

    /// <summary>The map's tracks, each with the ballast's band struck round it and cut to the map (RAIL-4, GEN-2b).</summary>
    public static CityPlan.RailArrays Lay(TownMap.RailArrays tracks, Vector2 worldSizeM, SimConfig config)
    {
        if (tracks.Count == 0) return CityPlan.RailArrays.None;

        // <b>Each band cut on its own</b>: cut together, the runs of two tracks side by side are closed into each other
        // along the map's edge, and a run closed round a corner encloses everything between the line and that corner.
        var cut = new List<Vector2[]>();
        for (var track = 0; track < tracks.Count; track++)
        {
            cut.AddRange(MapCut.Rings([Band(tracks.TrackOf(track), config.Rail.BallastWidthM * 0.5f)], worldSizeM));
        }

        var offsets = new int[cut.Count + 1];
        for (var ring = 0; ring < cut.Count; ring++) offsets[ring + 1] = offsets[ring] + cut[ring].Length;

        return new CityPlan.RailArrays
        {
            Line = [.. tracks.Line], Runs = [.. tracks.Runs], PointOffsets = [.. tracks.PointOffsets], PointM = [.. tracks.PointM],
            Level = [.. tracks.Level], Ballast = new CityPlan.RingArrays { Offsets = offsets, PointM = [.. cut.SelectMany(ring => ring)] },
        };
    }

    /// <summary>
    /// <b>The closed band round a line</b>, its own half-width either side, walked with the band on the walker's right as
    /// <see cref="MapCut"/> reads a ring.
    /// </summary>
    public static Vector2[] Band(ReadOnlySpan<Vector2> lineM, float halfM)
    {
        var ring = new Vector2[lineM.Length * 2];
        for (var point = 0; point < lineM.Length; point++)
        {
            var side = SideAt(lineM, point) * halfM;
            ring[point] = lineM[point] + side;
            ring[^(point + 1)] = lineM[point] - side;
        }

        var twiceAreaM2 = 0f;
        for (int here = 0, there = ring.Length - 1; here < ring.Length; there = here++)
        {
            twiceAreaM2 += (ring[there].X * ring[here].Y) - (ring[here].X * ring[there].Y);
        }

        if (twiceAreaM2 < 0f) Array.Reverse(ring);
        return ring;
    }

    /// <summary>
    /// <b>The way a line runs where it passes nearest a point</b>, of whichever track passes nearest — what a road
    /// crossing the railway is asked to stand square to (RAIL-5). A walk of every chord, for the generator's asks.
    /// </summary>
    public static Vector2 AlongNearest(CityPlan.RailArrays rails, Vector2 pointM)
    {
        var (along, nearestM2) = (Vector2.UnitX, float.PositiveInfinity);
        for (var track = 0; track < rails.Count; track++)
        {
            var lineM = rails.TrackOf(track);
            for (var point = 0; point + 1 < lineM.Length; point++)
            {
                var runM = lineM[point + 1] - lineM[point];
                var lengthM2 = runM.LengthSquared();
                if (lengthM2 <= 0f) continue;

                var share = Math.Clamp(Vector2.Dot(pointM - lineM[point], runM) / lengthM2, 0f, 1f);
                var awayM2 = (lineM[point] + (runM * share) - pointM).LengthSquared();
                if (awayM2 >= nearestM2) continue;

                (along, nearestM2) = (runM / MathF.Sqrt(lengthM2), awayM2);
            }
        }

        return along;
    }

    /// <summary>The way across a line at one of its points: square to the chord between the points either side.</summary>
    static Vector2 SideAt(ReadOnlySpan<Vector2> lineM, int point)
    {
        var runM = lineM[Math.Min(point + 1, lineM.Length - 1)] - lineM[Math.Max(point - 1, 0)];
        return Heading.RightOf(runM / MathF.Max(runM.Length(), float.Epsilon));
    }
}
