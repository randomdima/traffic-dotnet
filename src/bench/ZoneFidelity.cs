using System.Globalization;
using System.Numerics;
using System.Text;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Statics;

namespace TrafficSimulation.Bench;

/// <summary>
/// <b>How close a town its zones built stands to the place it was traced off</b> (GEN-58, <c>--bench zones</c>): the
/// buildings laid weighed against the place's own (<see cref="SurveyFootprints"/>, <c>footprints.bin</c> beside its
/// layers), kind of zone by kind — how many stand, how much of the ground they cover, how much of the street frontage
/// is built and how far back, how turned off square, and what they are drawn as — and square by square across the map.
/// It gates nothing: how well a map's zones say what is built is a fact about the map.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both sides are measured the one way</b>, which is the scanner's (<c>ZoneHints</c>): a building is its zone's by
/// where its middle stands; the frontage is probed every <see cref="ProbeStepM"/> along every street either side, from
/// its carriageway's edge out to the first building within <see cref="ProbeReachM"/>, the probe the zone's
/// <see cref="AskedAtM"/> out; and a laid building is the box it stands as. The squares are <see cref="CellM"/> a side,
/// each holding the ground of the buildings whose middles stand in it.
/// </para>
/// <para>
/// <b>The fit is one figure for the whole map</b>: by each kind of zone, weighed by the ground the place's own buildings
/// cover in it — so a city's sea and fields, which hold few, weigh little — how near the two sides' counts, cover and
/// built frontage are as the smaller of each over the larger, and how alike their looks are as one less half their
/// shares' difference — the four averaged — and the squares' correlation beside it.
/// </para>
/// </remarks>
internal static class ZoneFidelity
{
    /// <summary>How far apart along a street its frontage is probed — the scanner's own.</summary>
    const float ProbeStepM = 5f;

    /// <summary>How finely a probe walks out from the carriageway's edge.</summary>
    const float ProbeWalkM = 1f;

    /// <summary>How far past the carriageway's edge a probe looks for a front.</summary>
    const float ProbeReachM = 40f;

    /// <summary>Where along a probe its zone is read, past the carriageway's edge.</summary>
    const float AskedAtM = 9f;

    /// <summary>The side of a square the map's built ground is weighed square by square in.</summary>
    const float CellM = 100f;

    /// <summary>The side of a square the buildings are filed in to be asked whether one stands at a place.</summary>
    const float FiledM = 64f;

    public static void Run(string map, SimConfig config, string? outPath)
    {
        var reference = Path.Combine(ProjectPaths.SurveyFolder(map), SurveyFootprints.File);
        if (!File.Exists(reference))
        {
            Console.WriteLine($"{map} has no footprints to weigh its town against: no {Path.GetRelativePath(ProjectPaths.Root, reference)}, which `qq osm --zones` writes.");
            return;
        }

        var sizes = BuildingCatalog.Roofs;
        var plan = Maps.Plan(map, config, sizes);
        var survey = Survey.Of(Maps.Read(map), config);
        var zones = new ZoneTree(plan.Zones, config.Zones);

        var real = Side.Of(SurveyFootprints.Read(reference), zones, survey, plan.WorldSizeM);
        var laid = Side.Of(Footprints(plan.Buildings, sizes), zones, survey, plan.WorldSizeM);
        var report = Report(map, zones, real, laid);
        Console.Write(report);
        if (outPath is null) return;

        File.WriteAllText(outPath, report);
        Console.WriteLine($"written to {outPath}");
    }

    /// <summary>The buildings laid, each as the box it stands as and the look its prefab or its use is drawn as.</summary>
    static SurveyFootprints Footprints(CityPlan.BuildingArrays buildings, BuildingSizes sizes)
    {
        var pointM = new Vector2[buildings.Count * 4];
        var look = new BuildingLook[buildings.Count];
        for (var building = 0; building < buildings.Count; building++)
        {
            var along = Heading.Unit(buildings.HeadingRad[building]);
            var across = Heading.RightOf(along);
            var halfM = buildings.SizeM[building] * 0.5f;
            var centreM = buildings.CentreM[building];
            pointM[(4 * building) + 0] = centreM - (along * halfM.X) - (across * halfM.Y);
            pointM[(4 * building) + 1] = centreM + (along * halfM.X) - (across * halfM.Y);
            pointM[(4 * building) + 2] = centreM + (along * halfM.X) + (across * halfM.Y);
            pointM[(4 * building) + 3] = centreM - (along * halfM.X) + (across * halfM.Y);
            look[building] = buildings.Prefab.Length > 0 && buildings.Prefab[building] >= 0
                ? sizes.PrefabLook[buildings.Prefab[building]]
                : buildings.Use[building] switch
                {
                    BuildingUse.Hospital => BuildingLook.Hospital,
                    BuildingUse.Depot => BuildingLook.Industrial,
                    BuildingUse.PoliceStation => BuildingLook.Office,
                    _ => BuildingLook.Apartments,
                };
        }

        return new SurveyFootprints
        {
            RingOffsets = [.. Enumerable.Range(0, buildings.Count + 1)], PointOffsets = [.. Enumerable.Range(0, buildings.Count + 1).Select(at => at * 4)],
            PointM = pointM, HeightM = new float[buildings.Count], Use = new FootprintUse[buildings.Count], Look = look,
        };
    }

    /// <summary>What one side's buildings measure, by kind of zone and by square.</summary>
    sealed class Side
    {
        public readonly int[] Count = new int[Kinds];
        public readonly double[] GroundM2 = new double[Kinds];
        public readonly int[] Asked = new int[Kinds];
        public readonly int[] Met = new int[Kinds];
        public readonly List<float>[] FrontM = Lists();
        public readonly List<float>[] SkewDeg = Lists();
        public readonly int[,] Looks = new int[Kinds, ZoneParams.Looks];
        public readonly Dictionary<(int, int), double> CellM2 = [];

        static List<float>[] Lists() => [.. Enumerable.Range(0, Kinds).Select(_ => new List<float>())];

        public static Side Of(SurveyFootprints buildings, ZoneTree zones, Survey survey, Vector2 sizeM)
        {
            var side = new Side();
            var centres = new Vector2[buildings.Count];
            var area = new float[buildings.Count];
            var filed = new Dictionary<(int, int), List<int>>();
            for (var building = 0; building < buildings.Count; building++)
            {
                var outline = buildings.RingOf(buildings.RingOffsets[building]);
                (centres[building], area[building]) = Centre(outline);
                for (var ring = buildings.RingOffsets[building] + 1; ring < buildings.RingOffsets[building + 1]; ring++) area[building] -= MathF.Abs(Centre(buildings.RingOf(ring)).AreaM2);

                var (leastM, mostM) = (new Vector2(float.MaxValue), new Vector2(float.MinValue));
                foreach (var pointM in outline) (leastM, mostM) = (Vector2.Min(leastM, pointM), Vector2.Max(mostM, pointM));
                for (var x = Filed(leastM.X); x <= Filed(mostM.X); x++)
                {
                    for (var y = Filed(leastM.Y); y <= Filed(mostM.Y); y++)
                    {
                        if (!filed.TryGetValue((x, y), out var here)) filed[(x, y)] = here = [];
                        here.Add(building);
                    }
                }

                if (centres[building] is not { X: >= 0f, Y: >= 0f } centre || centre.X > sizeM.X || centre.Y > sizeM.Y) continue;

                var kind = (int)zones[zones.At(centre)].Kind;
                side.Count[kind]++;
                side.GroundM2[kind] += area[building];
                var cell = ((int)MathF.Floor(centre.X / CellM), (int)MathF.Floor(centre.Y / CellM));
                side.CellM2[cell] = side.CellM2.GetValueOrDefault(cell) + area[building];
            }

            var frontRow = new HashSet<int>();
            foreach (var way in survey.Ways)
            {
                if (TracedRoad.Rank(way.Highway) == 0) continue;

                foreach (var hand in (ReadOnlySpan<float>)[-1f, 1f])
                {
                    var edgeM = (way.CarriagewayM * 0.5f) + (hand * way.CentreOffsetM);
                    for (var at = 1; at < way.Points.Length; at++)
                    {
                        var (a, b) = (survey.PointM(way.Points[at - 1]), survey.PointM(way.Points[at]));
                        var lengthM = Vector2.Distance(a, b);
                        if (lengthM <= 0f) continue;

                        var along = (b - a) / lengthM;
                        var outward = Heading.RightOf(along) * hand;
                        for (var stepM = ProbeStepM * 0.5f; stepM < lengthM; stepM += ProbeStepM)
                        {
                            var edge = a + (along * stepM) + (outward * edgeM);
                            var askedAt = edge + (outward * AskedAtM);
                            if (askedAt is not { X: >= 0f, Y: >= 0f } || askedAt.X > sizeM.X || askedAt.Y > sizeM.Y) continue;

                            var kind = (int)zones[zones.At(askedAt)].Kind;
                            side.Asked[kind]++;
                            if (Met(edge, outward) is not (>= 0 and var building, var offM)) continue;

                            side.Met[kind]++;
                            side.FrontM[kind].Add(offM);
                            if (!frontRow.Add(building)) continue;

                            side.SkewDeg[kind].Add(Skew(buildings.RingOf(buildings.RingOffsets[building]), along));
                            side.Looks[kind, (int)buildings.Look[building]]++;
                        }
                    }
                }
            }

            return side;

            (int Building, float OffM) Met(Vector2 edge, Vector2 outward)
            {
                for (var offM = 0f; offM <= ProbeReachM; offM += ProbeWalkM)
                {
                    var atM = edge + (outward * offM);
                    if (!filed.TryGetValue((Filed(atM.X), Filed(atM.Y)), out var near)) continue;

                    foreach (var building in near)
                    {
                        var inside = false;
                        for (var ring = buildings.RingOffsets[building]; ring < buildings.RingOffsets[building + 1]; ring++) inside ^= Encloses(buildings.RingOf(ring), atM);

                        if (inside) return (building, offM);
                    }
                }

                return (-1, 0f);
            }
        }
    }

    const int Kinds = (int)ZoneKind.Open + 1;

    static int Filed(float atM) => (int)MathF.Floor(atM / FiledM);

    static string Report(string map, ZoneTree zones, Side real, Side laid)
    {
        var areaM2 = new double[Kinds];
        var counted = new int[Kinds];
        for (var zone = 0; zone < zones.Count; zone++)
        {
            var kind = (int)zones[zone].Kind;
            counted[kind]++;
            areaM2[kind] += zones.AreaM2Of(zone);
        }

        var text = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;
        text.AppendLine(culture, $"# Zone fidelity — {map}");
        text.AppendLine();
        text.AppendLine(culture, $"Laid {laid.Count.Sum()} buildings against the place's {real.Count.Sum()}; real | laid in each pair of columns.");
        text.AppendLine();
        text.AppendLine("| kind | zones | km² | buildings | cover % | frontage % | front m | skew ° | looks alike | fit |");
        text.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

        var (fitSum, fitWeight) = (0.0, 0.0);
        for (var kind = 0; kind < Kinds; kind++)
        {
            if (real.Count[kind] + laid.Count[kind] + real.Asked[kind] == 0) continue;

            var ground = areaM2[kind] > 0 ? areaM2[kind] : double.NaN;
            var looks = LooksAlike(real.Looks, laid.Looks, kind);
            var fit = (Near(real.Count[kind], laid.Count[kind]) + Near(real.GroundM2[kind], laid.GroundM2[kind])
                       + Near(Share(real.Met[kind], real.Asked[kind]), Share(laid.Met[kind], laid.Asked[kind])) + looks) / 4;
            (fitSum, fitWeight) = (fitSum + (fit * real.GroundM2[kind]), fitWeight + real.GroundM2[kind]);

            text.AppendLine(culture,
                $"| {(ZoneKind)kind} | {counted[kind]} | {areaM2[kind] / 1e6:F2} | {real.Count[kind]} \\| {laid.Count[kind]} " +
                $"| {100 * real.GroundM2[kind] / ground:F1} \\| {100 * laid.GroundM2[kind] / ground:F1} " +
                $"| {100 * Share(real.Met[kind], real.Asked[kind]):F0} \\| {100 * Share(laid.Met[kind], laid.Asked[kind]):F0} " +
                $"| {Median(real.FrontM[kind]):F1} \\| {Median(laid.FrontM[kind]):F1} | {Median(real.SkewDeg[kind]):F1} \\| {Median(laid.SkewDeg[kind]):F1} " +
                $"| {looks:P0} | {fit:P0} |");
        }

        var (cells, maeM2, r) = Squares(real.CellM2, laid.CellM2);
        text.AppendLine();
        text.AppendLine(culture, $"- **Fit**: {fitSum / fitWeight:P1} — each kind's count, cover, frontage and looks as near as the smaller over the larger, weighed by the ground the place's buildings cover in it.");
        text.AppendLine(culture, $"- **Squares** of {CellM:F0} m: {cells} holding a building on either side; built ground off by {maeM2 / (CellM * CellM):P1} of a square on average; correlation {r:F3}.");
        text.AppendLine(culture, $"- **Frontage** read every {ProbeStepM:F0} m along every street either side, out to {ProbeReachM:F0} m past its carriageway.");
        return text.ToString();
    }

    /// <summary>How alike two sides' looks are in a kind of zone: one less half the sum of the differences of their shares.</summary>
    static double LooksAlike(int[,] real, int[,] laid, int kind)
    {
        var (realTotal, laidTotal) = (0, 0);
        for (var look = 0; look < ZoneParams.Looks; look++) (realTotal, laidTotal) = (realTotal + real[kind, look], laidTotal + laid[kind, look]);
        if (realTotal == 0 || laidTotal == 0) return realTotal == laidTotal ? 1 : 0;

        var apart = 0.0;
        for (var look = 0; look < ZoneParams.Looks; look++) apart += Math.Abs((real[kind, look] / (double)realTotal) - (laid[kind, look] / (double)laidTotal));
        return 1 - (apart / 2);
    }

    /// <summary>How many squares hold a building on either side, how far apart their ground is on average, and how the two correlate.</summary>
    static (int Cells, double MaeM2, double R) Squares(Dictionary<(int, int), double> real, Dictionary<(int, int), double> laid)
    {
        var cells = real.Keys.Union(laid.Keys).ToArray();
        if (cells.Length == 0) return (0, 0, 0);

        var (sumR, sumL, sumRR, sumLL, sumRL, absolute) = (0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
        foreach (var cell in cells)
        {
            var (r, l) = (real.GetValueOrDefault(cell), laid.GetValueOrDefault(cell));
            (sumR, sumL, sumRR, sumLL, sumRL, absolute) = (sumR + r, sumL + l, sumRR + (r * r), sumLL + (l * l), sumRL + (r * l), absolute + Math.Abs(r - l));
        }

        var n = cells.Length;
        var spread = Math.Sqrt(((n * sumRR) - (sumR * sumR)) * ((n * sumLL) - (sumL * sumL)));
        return (n, absolute / n, spread > 0 ? ((n * sumRL) - (sumR * sumL)) / spread : 0);
    }

    static double Near(double real, double laid) => real <= 0 && laid <= 0 ? 1 : Math.Min(real, laid) / Math.Max(real, laid);

    static double Share(int part, int whole) => whole > 0 ? part / (double)whole : 0;

    static float Median(List<float> values)
    {
        if (values.Count == 0) return float.NaN;

        values.Sort();
        return values[values.Count / 2];
    }

    /// <summary>How far a building turns off square to a street, by its longest wall, in degrees from nought to forty-five.</summary>
    static float Skew(ReadOnlySpan<Vector2> outline, Vector2 along)
    {
        var (longest, wall) = (0f, along);
        for (var at = 0; at < outline.Length; at++)
        {
            var run = outline[(at + 1) % outline.Length] - outline[at];
            if (run.Length() > longest) (longest, wall) = (run.Length(), run);
        }

        var turnDeg = MathF.Abs(MathF.Atan2((along.X * wall.Y) - (along.Y * wall.X), Vector2.Dot(along, wall))) * (180f / MathF.PI) % 90f;
        return MathF.Min(turnDeg, 90f - turnDeg);
    }

    /// <summary>A ring's centre of area and its area.</summary>
    static (Vector2 CentreM, float AreaM2) Centre(ReadOnlySpan<Vector2> ring)
    {
        var (twice, x, y) = (0.0, 0.0, 0.0);
        for (var at = 0; at < ring.Length; at++)
        {
            var (a, b) = (ring[at], ring[(at + 1) % ring.Length]);
            var cross = ((double)a.X * b.Y) - ((double)b.X * a.Y);
            (twice, x, y) = (twice + cross, x + ((a.X + b.X) * cross), y + ((a.Y + b.Y) * cross));
        }

        return Math.Abs(twice) < 1e-9 ? (ring[0], 0f) : (new Vector2((float)(x / (3 * twice)), (float)(y / (3 * twice))), (float)Math.Abs(twice * 0.5));
    }

    static bool Encloses(ReadOnlySpan<Vector2> ring, Vector2 atM)
    {
        var inside = false;
        for (int at = 0, before = ring.Length - 1; at < ring.Length; before = at++)
        {
            var (a, b) = (ring[at], ring[before]);
            if ((a.Y > atM.Y) != (b.Y > atM.Y) && atM.X < a.X + ((atM.Y - a.Y) / (b.Y - a.Y) * (b.X - a.X))) inside = !inside;
        }

        return inside;
    }
}
