using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using TrafficSimulation.CityGen;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Traced;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>A traced map imported off its survey and the layers the engine lays it with</b> (<see cref="TownMap"/>,
/// <see cref="TracedMapImport"/>): the extract's roads and coast, each road's measured width, and what the place is
/// zoned for (<see cref="ZoneHints"/>), in the survey's own frame — written to <c>towns/&lt;Map&gt;.map</c>, the only
/// file of the place the engine reads. Run by <c>qq osm --import</c>. No turn, control, crossing, tree or footprint the
/// layers hold is imported: a town lays its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>The map is the master once imported</b>: it is edited in place (<see cref="Crop"/>), so an import over one that
/// is there is refused unless forced, which replaces it and every edit made since. The extract, the layers and the
/// sources they were read off are left as they are.
/// </para>
/// <para>
/// <b>What is imported is what the layers say</b>, each record's one answer: a road's <c>widthM</c> where it was
/// measured rather than made of its lanes, and a zone's land use with the look its buildings stand in. No rule of the
/// engine's is applied here. Layers laid off another survey than the extract's are refused.
/// </para>
/// <para>
/// <b>A footprint is one outline and the courtyards inside it</b> (<see cref="Footprints"/>, read for the zones and the
/// stumps and never imported): a building of several outer rings is a footprint each, every inner ring given to the
/// outer one it stands in. A <c>building:part</c> is not read — it stands on its building's footprint — and a ring
/// closed on its first point is read without the repeat.
/// </para>
/// </remarks>
internal static class Import
{
    public static int Run(string root, string map, bool force)
    {
        var clock = Stopwatch.StartNew();
        var into = Scan.MapFile(root, map);
        if (File.Exists(into) && !force)
        {
            throw new InvalidOperationException(
                $"{Path.GetRelativePath(root, into)} is there, and it is the map: importing over it loses every edit made to it since — `qq osm --import --force` does.");
        }

        var folder = Path.Combine(root, "towns", "traced", map);
        var surveyAt = Path.Combine(root, "towns", "traced", $"{map}.json");
        var survey = JsonSerializer.Deserialize(File.ReadAllBytes(surveyAt), OsmExtractJson.Default.OsmExtract)
                     ?? throw new InvalidDataException($"{map}: no extract");
        survey.Check(surveyAt);
        using (var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(folder, "manifest.json"))))
        {
            var laidOff = manifest.RootElement.GetProperty("surveyOsmBase").GetString();
            if (laidOff != survey.Source.OsmBase)
            {
                throw new InvalidDataException($"{map}: the layers were laid off the survey of {laidOff}, and the survey is of {survey.Source.OsmBase} — `qq osm --meta` lays them again.");
            }
        }

        var traced = TracedMapImport.Of(survey, new PlaceFacts { Widths = Widths(Layer<RoadRecord>(folder, "roads")) });
        traced = Zoning.Zoned(root, map, traced);
        traced.Write(into);
        Console.WriteLine($"{Path.GetRelativePath(root, into)} imported off {Path.GetRelativePath(root, surveyAt)} and {Path.GetRelativePath(root, folder)}/ in {clock.Elapsed.TotalSeconds:F1} s");
        Crop.Describe(traced, into);
        return 0;
    }

    static List<T> Layer<T>(string folder, string layer) =>
        Layers.Read<T>(folder, layer)?.Items ?? throw new InvalidOperationException($"no {layer} layer in {folder}; `qq osm --meta` writes it");

    static PlaceFacts.WidthArrays Widths(List<RoadRecord> roads)
    {
        var (way, widthM, from) = (new List<long>(), new List<float>(), new List<MeasuredFrom>());
        foreach (var road in roads)
        {
            MeasuredFrom? measured = road.WidthFrom switch
            {
                "tag" => MeasuredFrom.Tag,
                "surface" => MeasuredFrom.Surface,
                "imagery" => MeasuredFrom.Imagery,
                _ => null,
            };
            if (measured is not { } read) continue;

            way.Add(road.Way);
            widthM.Add((float)road.WidthM);
            from.Add(read);
        }

        return new PlaceFacts.WidthArrays { Way = [.. way], WidthM = [.. widthM], From = [.. from] };
    }

    /// <summary>Every building's footprints and what each is for (<see cref="FootprintUses"/>), off the layers in <paramref name="folder"/>.</summary>
    /// <param name="keeps">Whether an outline is kept, read off its own places on the map.</param>
    public static SurveyFootprints Footprints(string folder, Plane plane, Func<Vector2[], bool> keeps) =>
        Footprints(Layer<BuildingRecord>(folder, "buildings"), new FootprintUses(Layer<ZoneRecord>(folder, "zones"), plane), plane, keeps);

    static SurveyFootprints Footprints(List<BuildingRecord> buildings, FootprintUses uses, Plane plane, Func<Vector2[], bool> keeps)
    {
        var ringOffsets = new List<int> { 0 };
        var pointOffsets = new List<int> { 0 };
        var pointM = new List<Vector2>();
        var heightM = new List<float>();
        var use = new List<FootprintUse>();
        var look = new List<BuildingLook>();
        foreach (var building in buildings)
        {
            if (building.Part == true) continue;

            var outers = building.Outer.Select(ring => Ring(ring, plane)).Where(ring => ring.Length >= 3).ToArray();
            var inners = (building.Inner ?? []).Select(ring => Ring(ring, plane)).Where(ring => ring.Length >= 3).ToArray();
            foreach (var outer in outers)
            {
                if (!keeps(outer)) continue;

                Add(outer);
                foreach (var inner in inners)
                {
                    if (Holds(outer, inner[0])) Add(inner);
                }

                ringOffsets.Add(pointOffsets.Count - 1);
                var outline = outer.Select(atM => new Pt(atM.X, atM.Y)).ToArray();
                heightM.Add((float)(building.HeightM ?? 0));
                use.Add(uses.Of(building, Shape.Centroid(outline)));
                look.Add(FootprintUses.LookOf(use[^1], heightM[^1], (float)Math.Abs(Shape.SignedArea(outline))));
            }
        }

        return new SurveyFootprints
        {
            RingOffsets = [.. ringOffsets], PointOffsets = [.. pointOffsets], PointM = [.. pointM], HeightM = [.. heightM], Use = [.. use],
            Look = [.. look],
        };

        void Add(Vector2[] ring)
        {
            pointM.AddRange(ring);
            pointOffsets.Add(pointM.Count);
        }
    }

    /// <summary>A ring of lat, lon pairs on the map, without the repeat of its first point it is closed on.</summary>
    static Vector2[] Ring(int[] latLon, Plane plane)
    {
        var points = new List<Vector2>(latLon.Length / 2);
        for (var at = 0; at + 1 < latLon.Length; at += 2) points.Add(Placed(plane, latLon[at], latLon[at + 1]));
        if (points.Count > 1 && points[0] == points[^1]) points.RemoveAt(points.Count - 1);
        return [.. points];
    }

    static Vector2 Placed(Plane plane, int lat, int lon)
    {
        var at = plane.At(lat, lon);
        return new Vector2((float)at.X, (float)at.Y);
    }

    /// <summary>Whether a point stands inside a ring, by the crossings of a ray east from it.</summary>
    static bool Holds(Vector2[] ring, Vector2 pointM)
    {
        var inside = false;
        for (int at = 0, before = ring.Length - 1; at < ring.Length; before = at++)
        {
            var (a, b) = (ring[at], ring[before]);
            if ((a.Y > pointM.Y) != (b.Y > pointM.Y) && pointM.X < a.X + ((pointM.Y - a.Y) / (b.Y - a.Y) * (b.X - a.X))) inside = !inside;
        }

        return inside;
    }
}
