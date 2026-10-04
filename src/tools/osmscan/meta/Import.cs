using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using TrafficSimulation.CityGen.Traced;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>A traced map imported off its survey and the layers the engine lays it with</b> (<see cref="TracedMap"/>,
/// <see cref="TracedMapImport"/>): the extract's roads, coast and turns, and each road's measured width, each
/// junction's control, every pedestrian crossing, every building's footprint and height and every tree, in the
/// survey's own frame — written to <c>towns/traced/&lt;Map&gt;.map</c>, the only file of the place the engine reads. Run by <c>qq osm --import</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The map is the master once imported</b>: it is edited in place (<see cref="Crop"/>), so an import over one that
/// is there is refused unless forced, which replaces it and every edit made since. The extract, the layers and the
/// sources they were read off are left as they are.
/// </para>
/// <para>
/// <b>What is imported is what the layers say</b>, each record's one answer: a road's <c>widthM</c> where it was
/// measured rather than made of its lanes, a junction's control where it is not left to the rules, a crossing's
/// kind and whether its tags say it is painted, a footprint OSM's or the machine-traced one OSM lacks. No rule of the
/// engine's is applied here. Layers laid off another survey than the extract's are refused.
/// </para>
/// <para>
/// <b>A footprint is one outline and the courtyards inside it</b>: a building of several outer rings is a footprint
/// each, every inner ring given to the outer one it stands in. A <c>building:part</c> is not imported — it stands on
/// its building's footprint — and a ring closed on its first point is written without the repeat.
/// </para>
/// </remarks>
internal static class Import
{
    public static int Run(string root, string map, bool force)
    {
        var clock = Stopwatch.StartNew();
        var into = Path.Combine(root, "towns", "traced", $"{map}.map");
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

        var plane = new Plane(survey.Frame);
        var facts = new PlaceFacts
        {
            Widths = Widths(Layer<RoadRecord>(folder, "roads")),
            Controls = Controls(Layer<JunctionRecord>(folder, "junctions")),
            Crossings = Crossings(Layer<CrossingRecord>(folder, "crossings"), plane),
            Footprints = Footprints(folder, plane, _ => true),
            TreeM = Trees(Layer<PointRecord>(folder, "furniture"), plane),
        };

        var traced = TracedMapImport.Of(survey, facts);
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

    static PlaceFacts.ControlArrays Controls(List<JunctionRecord> junctions)
    {
        var (node, control, cluster) = (new List<long>(), new List<SurveyControl>(), new List<long>());
        foreach (var junction in junctions)
        {
            SurveyControl? read = junction.Control switch
            {
                "signals" => SurveyControl.Signals,
                "blinking" => SurveyControl.Blinking,
                "roundabout" => SurveyControl.Roundabout,
                "signs" => SurveyControl.Signs,
                "priority_road" => SurveyControl.PriorityRoad,
                _ => null,
            };
            if (read is not { } controlled) continue;

            node.Add(junction.Node);
            control.Add(controlled);
            cluster.Add(junction.Cluster ?? 0);
        }

        return new PlaceFacts.ControlArrays { Node = [.. node], Control = [.. control], Cluster = [.. cluster] };
    }

    static PlaceFacts.CrossingArrays Crossings(List<CrossingRecord> crossings, Plane plane)
    {
        var (way, atM, kind, painted) = (new List<long>(), new List<Vector2>(), new List<SurveyCrossingKind>(), new List<bool?>());
        var junction = new List<long>();
        foreach (var crossing in crossings)
        {
            if (crossing.Type != "pedestrian") continue;

            SurveyCrossingKind? read = crossing.Kind switch
            {
                "zebra" or "marked" => SurveyCrossingKind.Zebra,
                "signals" => SurveyCrossingKind.Signals,
                "unmarked" or "informal" => SurveyCrossingKind.Unmarked,
                "unknown" => SurveyCrossingKind.Unknown,
                _ => null,
            };
            if (read is not { } crossed) continue;

            way.Add(crossing.Road);
            atM.Add(Placed(plane, crossing.At[0], crossing.At[1]));
            kind.Add(crossed);
            painted.Add(crossing.Painted);
            junction.Add(crossing.AtJunction ? crossing.Junction ?? 0 : 0);
        }

        return new PlaceFacts.CrossingArrays
        {
            Way = [.. way], AtM = [.. atM], Kind = [.. kind], Painted = [.. painted], Junction = [.. junction],
        };
    }

    static Vector2[] Trees(List<PointRecord> furniture, Plane plane) =>
        [.. furniture.Where(point => point.Kind == "natural=tree" && point.At is { Length: 2 }).Select(point => Placed(plane, point.At![0], point.At[1]))];

    /// <summary>Every building's footprints and what each is for (<see cref="FootprintUses"/>), off the layers in <paramref name="folder"/>.</summary>
    /// <param name="keeps">Whether an outline is kept, read off its own places on the map.</param>
    public static TracedMap.FootprintArrays Footprints(string folder, Plane plane, Func<Vector2[], bool> keeps) =>
        Footprints(Layer<BuildingRecord>(folder, "buildings"), new FootprintUses(Layer<ZoneRecord>(folder, "zones"), plane), plane, keeps);

    static TracedMap.FootprintArrays Footprints(List<BuildingRecord> buildings, FootprintUses uses, Plane plane, Func<Vector2[], bool> keeps)
    {
        var ringOffsets = new List<int> { 0 };
        var pointOffsets = new List<int> { 0 };
        var pointM = new List<Vector2>();
        var traced = new List<bool>();
        var heightM = new List<float>();
        var use = new List<FootprintUse>();
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
                traced.Add(building.Source == "ml");
                heightM.Add((float)(building.HeightM ?? 0));
                use.Add(uses.Of(building, Shape.Centroid([.. outer.Select(atM => new Pt(atM.X, atM.Y))])));
            }
        }

        return new TracedMap.FootprintArrays
        {
            RingOffsets = [.. ringOffsets], PointOffsets = [.. pointOffsets], PointM = [.. pointM], Traced = [.. traced],
            HeightM = [.. heightM], Use = [.. use],
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
