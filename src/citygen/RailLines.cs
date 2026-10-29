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

    /// <summary>The least and the most a brief's loop turns (RAIL-11): within these its way out crosses its way in on the straights either side.</summary>
    public const float LoopLeastDeg = 240f, LoopMostDeg = 300f;

    /// <summary>
    /// How long, in the loop's radii, the line runs straight either side of it before it wanders: past where its way out
    /// crosses its way in, for any loop from <see cref="LoopLeastDeg"/> to <see cref="LoopMostDeg"/> (1.73 radii at the
    /// most), so the two cross square to what the brief turns.
    /// </summary>
    const float StraightRadii = 2.5f;

    /// <summary>
    /// <b>One line of a town's railway, drawn once</b> (GEN-63): through a place on a bearing, wandering as far as its least
    /// radius allows, and run on past the map at both ends far enough for a whole train to stand off it (TRN-7). A double
    /// line is two tracks either side of that, each run one way, on the right as it is faced.
    /// </summary>
    /// <remarks>
    /// <b>A loop is laid at the least radius round the place it passes</b> (RAIL-11), straight either side of it, and its
    /// way out crosses its way in on a bridge (RAIL-8): the chords within the bridge's half-span of the crossing along the
    /// line — the ballast under it, and the climb to its clearance at the ruling grade either side — stand
    /// <see cref="TownMap.RailArrays.Above"/>. <b>The line is walked by its heading</b>, so a loop and the
    /// wander compose: the wander turns the heading either way about the leg's own and never on average, and the line
    /// keeps its bearing however far it runs.
    /// </remarks>
    /// <param name="middleM">Where the line passes, and where its loop is laid if it has one.</param>
    /// <param name="meander">How far it wanders off its bearing, as a share of the map's short side.</param>
    /// <param name="line">Which line of the map's railway this is, so a map of several numbers each its own.</param>
    public static TownMap.RailArrays Author(
        Vector2 extentM, Vector2 middleM, float bearingRad, int tracks, float loopDeg, float meander, int line, SimConfig config, ref Rng draw)
    {
        if (tracks == 0) return TownMap.RailArrays.None;

        var rail = config.Rail;
        var shortSideM = MathF.Min(extentM.X, extentM.Y);
        var halfSpacingM = tracks == 2 ? rail.TrackSpacingM * 0.5f : 0f;

        // The line bends no tighter than its inner track may (RAIL-2).
        var radiusM = rail.LeastRadiusM + halfSpacingM;
        var loopRad = loopDeg * MathF.PI / 180f;
        var turn = MathF.Sign(loopRad);

        // A sum of sines turns hardest at the sum of what each turns, so the wander is held to what the radius allows.
        var periodM = extentM.Length();
        var wave = new Wave(MathF.Tau / periodM, MathF.Tau / (periodM * SecondWaveShare));
        var wanderRad = MathF.Min(meander * shortSideM * wave.FirstPerM, 1f / (radiusM * wave.TurnPerRad));
        var (inSide, outSide) = (SideOf(ref draw), SideOf(ref draw));

        // A chord of a curve stands off it by its square over eight radii, which is the sagitta.
        var stepM = MathF.Sqrt(8f * rail.LeastRadiusM * rail.ChordToleranceM);
        var straightM = turn == 0f ? 0f : StraightRadii * radiusM;
        var centreOfLoopM = middleM;
        var startM = middleM - (Heading.RightOf(Heading.Unit(bearingRad)) * (turn * radiusM));
        var turns = turn == 0f ? 0 : (int)MathF.Ceiling(MathF.Abs(loopRad) * radiusM / stepM);

        var wayIn = Leg(startM, bearingRad + MathF.PI, straightM, wanderRad * inSide, wave, stepM, extentM, rail.RunOffM);
        var centre = new List<Vector2>(wayIn.Count * 2);
        for (var point = wayIn.Count - 1; point >= 0; point--) centre.Add(wayIn[point]);
        for (var step = 1; step <= turns; step++)
        {
            var headingRad = bearingRad + (loopRad * step / turns);
            centre.Add(centreOfLoopM - (Heading.RightOf(Heading.Unit(headingRad)) * (turn * radiusM)));
        }

        var outFrom = centre.Count - 1;
        var outRad = bearingRad + loopRad;
        var wayOut = Leg(centre[outFrom], outRad, straightM, wanderRad * outSide, wave, stepM, extentM, rail.RunOffM);
        for (var point = 1; point < wayOut.Count; point++) centre.Add(wayOut[point]);

        var points = centre.Count;
        var level = new byte[points];
        if (turn != 0f)
        {
            // Where the way out's straight crosses the way in's, in metres along the line: over it, and under it.
            var (inward, outward) = (Heading.Unit(bearingRad), Heading.Unit(outRad));
            var alongM = new float[points];
            for (var point = 1; point < points; point++) alongM[point] = alongM[point - 1] + Vector2.Distance(centre[point - 1], centre[point]);
            var overM = alongM[outFrom] + (Cross(startM - centre[outFrom], inward) / Cross(outward, inward));
            var crossingM = centre[outFrom] + (outward * (overM - alongM[outFrom]));
            var underM = alongM[wayIn.Count - 1] - Vector2.Dot(startM - crossingM, inward);

            // The bridge spans the ballast under it and climbs to it at the ruling grade either side — on its inner track,
            // which runs the least radius round where the line runs its own, and so climbs in less than the line runs.
            var underHalfM = (halfSpacingM + (rail.BallastWidthM * 0.5f)) / MathF.Abs(Trig.Sin(loopRad));
            var halfSpanM = underHalfM + (rail.BridgeClearanceM / rail.GradeMostShare * radiusM / rail.LeastRadiusM);
            if (overM - halfSpanM <= underM + underHalfM)
            {
                throw new InvalidDataException(
                    $"a bridge climbing {halfSpanM:F0} m to its crossing reaches the track it passes over, {overM - underM:F0} m back along the loop.");
            }

            for (var chord = 0; chord + 1 < points; chord++)
            {
                if (alongM[chord + 1] >= overM - halfSpanM && alongM[chord] <= overM + halfSpanM) level[chord] = TownMap.RailArrays.Above;
            }
        }

        var centreM = centre.ToArray();
        if (tracks == 1)
        {
            return new TownMap.RailArrays
            {
                Line = [line], Runs = [TownMap.TrackRuns.Both], PointOffsets = [0, points], PointM = centreM, Level = level,
            };
        }

        var pointM = new Vector2[points * 2];
        for (var point = 0; point < points; point++)
        {
            var side = SideAt(centreM, point);
            pointM[point] = centreM[point] + (side * halfSpacingM);
            pointM[points + point] = centreM[point] - (side * halfSpacingM);
        }

        return new TownMap.RailArrays
        {
            Line = [line, line], Runs = [TownMap.TrackRuns.Forward, TownMap.TrackRuns.Backward], PointOffsets = [0, points, points * 2],
            PointM = pointM, Level = [.. level, .. level],
        };
    }

    /// <summary>The two waves a line wanders on, each by how far its phase turns a metre.</summary>
    readonly record struct Wave(float FirstPerM, float SecondPerM)
    {
        /// <summary>How fast the heading turns, a radian of wander at most, where both waves turn together.</summary>
        public float TurnPerRad => (FirstPerM + (SecondWaveWeight * SecondPerM)) / (1f + SecondWaveWeight);

        /// <summary>The wander's share at a distance along it, from nought where it starts.</summary>
        public float At(float alongM) =>
            (Trig.Sin(alongM * FirstPerM) + (SecondWaveWeight * Trig.Sin(alongM * SecondPerM))) / (1f + SecondWaveWeight);
    }

    /// <summary>
    /// <b>One leg of a line, from a point until it stands the run-off past the map</b>: straight for its first stretch,
    /// then wandering about its heading by up to <paramref name="wanderRad"/>, a chord a step.
    /// </summary>
    static List<Vector2> Leg(
        Vector2 fromM, float headingRad, float straightM, float wanderRad, Wave wave, float stepM, Vector2 extentM, float runOffM)
    {
        var lineM = new List<Vector2> { fromM };
        var mostM = 2f * (extentM.Length() + runOffM + straightM);
        var atM = fromM;
        for (var alongM = 0f; OffTheMapM(atM, extentM) < runOffM; alongM += stepM)
        {
            if (alongM > mostM) throw new InvalidOperationException($"a leg of the railway from {fromM} never left the map");

            var wanderedM = alongM + (stepM * 0.5f) - straightM;
            var offRad = wanderedM <= 0f ? 0f : wanderRad * wave.At(wanderedM);
            atM += Heading.Unit(headingRad + offRad) * stepM;
            lineM.Add(atM);
        }

        return lineM;
    }

    static float OffTheMapM(Vector2 pointM, Vector2 extentM)
    {
        var outM = Vector2.Max(Vector2.Max(-pointM, pointM - extentM), Vector2.Zero);
        return outM.Length();
    }

    static float SideOf(ref Rng draw) => draw.NextFloat(0f, 1f) < 0.5f ? -1f : 1f;

    static float Cross(Vector2 one, Vector2 other) => (one.X * other.Y) - (one.Y * other.X);

    /// <summary>
    /// The map's tracks, each with the ballast's band struck round it and cut to the map (RAIL-4, GEN-2b), <b>and every
    /// chord over the water or its shore raised onto a bridge</b> (RAIL-8, GEN-14a): the water is laid before the railway,
    /// and a track at grade across a river is a track under it.
    /// </summary>
    /// <param name="water">The ground the water was laid as, asked only where the water stands; none where the plan has none.</param>
    public static CityPlan.RailArrays Lay(TownMap.RailArrays tracks, Vector2 worldSizeM, SimConfig config, GroundShapes? water = null)
    {
        if (tracks.Count == 0) return CityPlan.RailArrays.None;

        var (pointM, pointOffsets, levels) = OneDeck(
            tracks, water is null ? tracks.Level : OverTheWater(tracks, water, config.Terrain.GroundStepM), config.RailDeckHalfM);
        tracks = new TownMap.RailArrays { Line = tracks.Line, Runs = tracks.Runs, PointOffsets = pointOffsets, PointM = pointM, Level = levels };

        // <b>Each band cut on its own</b>: cut together, the runs of two tracks side by side are closed into each other
        // along the map's edge, and a run closed round a corner encloses everything between the line and that corner.
        // <b>And each run of one level banded on its own</b>: a track passing over itself (RAIL-11) banded whole is one
        // ring crossing itself, which covers neither pass where they cross.
        var cut = new List<Vector2[]>();
        for (var track = 0; track < tracks.Count; track++)
        {
            var lineM = tracks.TrackOf(track);
            var level = levels.AsSpan(tracks.PointOffsets[track], lineM.Length);
            for (var from = 0; from + 1 < lineM.Length;)
            {
                var to = from + 1;
                while (to + 1 < lineM.Length && level[to] == level[from]) to++;

                cut.AddRange(MapCut.Rings([Band(lineM[from..(to + 1)], config.Rail.BallastWidthM * 0.5f)], worldSizeM));
                from = to;
            }
        }

        var offsets = new int[cut.Count + 1];
        for (var ring = 0; ring < cut.Count; ring++) offsets[ring + 1] = offsets[ring] + cut[ring].Length;

        return new CityPlan.RailArrays
        {
            Line = [.. tracks.Line], Runs = [.. tracks.Runs], PointOffsets = [.. tracks.PointOffsets], PointM = [.. tracks.PointM],
            Level = [.. levels], Ballast = new CityPlan.RingArrays { Offsets = offsets, PointM = [.. cut.SelectMany(ring => ring)] },
        };
    }

    /// <summary>
    /// Each chord's level, raised to <see cref="TownMap.RailArrays.Above"/> where any of it stands over the water or its
    /// shore, walked a step at a time: a bridge spans the shore as well, so its deck lands on the grass and not on the
    /// pebbles at the water's edge.
    /// </summary>
    static byte[] OverTheWater(TownMap.RailArrays tracks, GroundShapes water, float stepM)
    {
        var levels = tracks.Level.ToArray();
        for (var track = 0; track < tracks.Count; track++)
        {
            var from = tracks.PointOffsets[track];
            var lineM = tracks.TrackOf(track);
            for (var point = 0; point + 1 < lineM.Length; point++)
            {
                if (levels[from + point] != TownMap.RailArrays.AtGrade) continue;

                var runM = Vector2.Distance(lineM[point], lineM[point + 1]);
                for (var alongM = 0f; alongM <= runM; alongM += stepM)
                {
                    if (!water.Waterside(Blend.Lerp(lineM[point], lineM[point + 1], MathF.Min(1f, alongM / runM)))) continue;

                    levels[from + point] = TownMap.RailArrays.Above;
                    break;
                }
            }
        }

        return levels;
    }

    /// <summary>
    /// <b>Tracks side by side cross on one bridge</b> (RAIL-8), as a road's lanes do: the runs of tracks whose decks touch
    /// are each carried on to the furthest any of them reaches at either end, so the bridge ends square across all of them
    /// rather than each track's where its own chords happened to leave the shore. A chord such an end falls inside is cut
    /// there.
    /// </summary>
    static (Vector2[] PointM, int[] PointOffsets, byte[] Level) OneDeck(TownMap.RailArrays tracks, byte[] levels, float deckHalfM)
    {

        var alongM = new float[tracks.Count][];
        var runs = new List<(int Track, float FromM, float ToM)>();
        for (var track = 0; track < tracks.Count; track++)
        {
            var lineM = tracks.TrackOf(track);
            var level = levels.AsSpan(tracks.PointOffsets[track], lineM.Length);
            alongM[track] = new float[lineM.Length];
            for (var point = 1; point < lineM.Length; point++) alongM[track][point] = alongM[track][point - 1] + Vector2.Distance(lineM[point - 1], lineM[point]);

            for (var from = 0; from + 1 < lineM.Length; from++)
            {
                if (level[from] == TownMap.RailArrays.AtGrade) continue;

                var to = from + 1;
                while (to + 1 < lineM.Length && level[to] != TownMap.RailArrays.AtGrade) to++;

                runs.Add((track, alongM[track][from], alongM[track][to]));
                from = to;
            }
        }

        // Two runs are one bridge where each passes within a deck's width of the other a quarter and three quarters along it,
        // which two tracks crossing each other do not.
        var root = Enumerable.Range(0, runs.Count).ToArray();
        for (var one = 0; one < runs.Count; one++)
        {
            for (var other = one + 1; other < runs.Count; other++)
            {
                if (runs[one].Track != runs[other].Track && Beside(one, other) && Beside(other, one)) root[Find(one)] = Find(other);
            }
        }

        var cutsM = new List<float>[tracks.Count];
        var raised = new List<(float FromM, float ToM)>[tracks.Count];
        for (var track = 0; track < tracks.Count; track++) (cutsM[track], raised[track]) = ([], []);

        foreach (var group in Enumerable.Range(0, runs.Count).GroupBy(Find))
        {
            var first = runs[group.First()];
            var axis = Vector2.Normalize(At(first.Track, first.ToM) - At(first.Track, first.FromM));
            var (leastM, mostM) = (float.PositiveInfinity, float.NegativeInfinity);
            foreach (var run in group)
            {
                foreach (var endM in (ReadOnlySpan<float>)[runs[run].FromM, runs[run].ToM])
                {
                    var onM = Vector2.Dot(At(runs[run].Track, endM), axis);
                    (leastM, mostM) = (MathF.Min(leastM, onM), MathF.Max(mostM, onM));
                }
            }

            foreach (var run in group)
            {
                var (track, fromM, toM) = runs[run];
                var forward = Vector2.Dot(At(track, toM) - At(track, fromM), axis) >= 0f;
                var (startM, endM) = (Reach(track, fromM, -1, axis, forward ? leastM : mostM), Reach(track, toM, 1, axis, forward ? mostM : leastM));
                cutsM[track].Add(startM);
                cutsM[track].Add(endM);
                raised[track].Add((startM, endM));
            }
        }

        var points = new List<Vector2>(tracks.PointM.Length);
        var laid = new List<byte>(tracks.PointM.Length);
        var offsets = new int[tracks.Count + 1];
        var pointAlongM = new List<float>();
        for (var track = 0; track < tracks.Count; track++)
        {
            var lineM = tracks.TrackOf(track);
            var level = levels.AsSpan(tracks.PointOffsets[track], lineM.Length);
            pointAlongM.Clear();
            for (var point = 0; point < lineM.Length; point++)
            {
                Add(lineM[point], alongM[track][point], level[point]);
                if (point + 1 == lineM.Length) break;

                var (fromM, toM) = (alongM[track][point], alongM[track][point + 1]);
                foreach (var cutM in cutsM[track].Where(cutM => cutM > fromM + LineTolerance.OnePlaceM && cutM < toM - LineTolerance.OnePlaceM).Order())
                {
                    Add(Vector2.Lerp(lineM[point], lineM[point + 1], (cutM - fromM) / (toM - fromM)), cutM, level[point]);
                }
            }

            offsets[track + 1] = points.Count;

            // A chord's level is the point's it starts at, so each is read again off where its middle stands.
            for (var chord = 0; chord + 1 < pointAlongM.Count; chord++)
            {
                var middleM = (pointAlongM[chord] + pointAlongM[chord + 1]) * 0.5f;
                if (raised[track].Exists(span => middleM > span.FromM && middleM < span.ToM)) laid[offsets[track] + chord] = TownMap.RailArrays.Above;
            }

            void Add(Vector2 atM, float onM, byte chordLevel)
            {
                points.Add(atM);
                laid.Add(chordLevel);
                pointAlongM.Add(onM);
            }
        }

        return ([.. points], offsets, [.. laid]);

        int Find(int run) => root[run] == run ? run : root[run] = Find(root[run]);

        Vector2 At(int track, float atM)
        {
            var lineM = tracks.TrackOf(track);
            var along = alongM[track];
            var found = Array.BinarySearch(along, atM);
            var point = Math.Clamp(found >= 0 ? found : ~found - 1, 0, lineM.Length - 2);
            var chordM = along[point + 1] - along[point];
            return chordM <= 0f ? lineM[point] : Vector2.Lerp(lineM[point], lineM[point + 1], Math.Clamp((atM - along[point]) / chordM, 0f, 1f));
        }

        // Whether one run passes within two deck half-widths of the other — their decks touching — a quarter and three
        // quarters along it.
        bool Beside(int one, int other)
        {
            var (track, fromM, toM) = runs[one];
            var (otherTrack, otherFromM, otherToM) = runs[other];
            var along = alongM[otherTrack];
            foreach (var share in (ReadOnlySpan<float>)[0.25f, 0.75f])
            {
                var placeM = At(track, fromM + ((toM - fromM) * share));
                var (nearestM, lastM) = (float.PositiveInfinity, At(otherTrack, otherFromM));
                foreach (var stepM in along.Where(stepM => stepM > otherFromM && stepM < otherToM).Append(otherToM))
                {
                    var hereM = At(otherTrack, stepM);
                    var runM = hereM - lastM;
                    var onShare = Math.Clamp(Vector2.Dot(placeM - lastM, runM) / MathF.Max(runM.LengthSquared(), float.Epsilon), 0f, 1f);
                    nearestM = MathF.Min(nearestM, Vector2.Distance(placeM, lastM + (runM * onShare)));
                    lastM = hereM;
                }

                if (nearestM > 2f * deckHalfM) return false;
            }

            return true;
        }

        // How far along its track a run's end is carried on, the way given, to stand as far along the axis as its group
        // reaches: where the track first gets there, the end itself where it is there already, and where the track turns
        // away or ends short of it.
        float Reach(int track, float endM, int way, Vector2 axis, float targetM)
        {
            var along = alongM[track];
            var (hereM, hereOnM) = (endM, Vector2.Dot(At(track, endM), axis));
            var found = Array.BinarySearch(along, endM);
            for (var point = found >= 0 ? found + way : way > 0 ? ~found : ~found - 1; point >= 0 && point < along.Length; point += way)
            {
                var thereM = along[point];
                var thereOnM = Vector2.Dot(At(track, thereM), axis);
                if ((thereOnM - targetM) * (hereOnM - targetM) <= 0f)
                {
                    return thereOnM == hereOnM ? hereM : hereM + ((thereM - hereM) * (targetM - hereOnM) / (thereOnM - hereOnM));
                }

                if (MathF.Abs(thereOnM - targetM) >= MathF.Abs(hereOnM - targetM)) return hereM;

                (hereM, hereOnM) = (thereM, thereOnM);
            }

            return hereM;
        }
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
