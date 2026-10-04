using System.Globalization;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TrafficSimulation.CityGen.Traced;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>The enriched layers drawn back off the files written</b>, over a square of the map: zones tinted by kind,
/// buildings grey (machine-traced ones red), roads by role at their carriageway's width, pavements green, crossings
/// by kind, junctions by control, stops blue — so a layer is checked by looking at what was written, not at what
/// the scanner meant to write.
/// </summary>
/// <remarks>
/// Junctions: red signals, orange signs, blue roundabout, purple priority road, dark grey right-hand rule, light grey
/// a yard's way out, a magenta ring where an outside source saw a signal OSM does not map. Crossings: white zebra,
/// yellow painted, red signals, grey unmarked, black none. Roads: dark grey street, orange a zone's way, tan driveway
/// or alley; a bridge cased black, a tunnel or passage dashed.
/// </remarks>
internal static class Picture
{
    const int SizePx = 1600;

    public static int Run(string root, string map, double latDeg, double lonDeg, double spanM, string into)
    {
        var folder = System.IO.Path.Combine(root, "towns", "traced", map);
        var extract = JsonSerializer.Deserialize(File.ReadAllBytes(System.IO.Path.Combine(root, "towns", "traced", $"{map}.json")), OsmExtractJson.Default.OsmExtract)
                      ?? throw new InvalidDataException($"{map}: no extract");
        var plane = new Plane(extract.Frame);
        var centre = plane.At((int)Math.Round(latDeg * 1e7), (int)Math.Round(lonDeg * 1e7));
        var pxPerM = SizePx / spanM;

        PointF Px(Pt at) => new((float)(((at.X - centre.X) * pxPerM) + (SizePx / 2.0)), (float)(((at.Y - centre.Y) * pxPerM) + (SizePx / 2.0)));
        PointF[] Line(JsonElement places)
        {
            var line = new PointF[places.GetArrayLength() / 2];
            for (var at = 0; at < line.Length; at++) line[at] = Px(plane.At(places[2 * at].GetInt32(), places[(2 * at) + 1].GetInt32()));
            return line;
        }

        bool Seen(PointF[] line) => line.Any(p => p.X >= -50 && p.Y >= -50 && p.X <= SizePx + 50 && p.Y <= SizePx + 50);

        using var image = new Image<Rgba32>(SizePx, SizePx, Color.FromRgb(246, 244, 238));
        image.Mutate(canvas =>
        {
            foreach (var zone in Items(folder, "zones"))
            {
                if (!zone.TryGetProperty("outer", out var outer)) continue;

                var tint = Tint(zone.GetProperty("kind").GetString()!);
                if (tint is null) continue;

                foreach (var ring in outer.EnumerateArray())
                {
                    var line = Line(ring);
                    if (line.Length >= 3 && Seen(line)) canvas.Fill(tint.Value, new Polygon(line));
                }
            }

            foreach (var building in Items(folder, "buildings"))
            {
                var ml = building.GetProperty("source").GetString() == "ml";
                foreach (var ring in building.GetProperty("outer").EnumerateArray())
                {
                    var line = Line(ring);
                    if (line.Length < 3 || !Seen(line)) continue;

                    canvas.Fill(ml ? Color.FromRgba(220, 80, 80, 140) : Color.FromRgb(196, 190, 182), new Polygon(line));
                    canvas.DrawPolygon(Color.FromRgb(150, 144, 136), 1f, line);
                }
            }

            var roads = Items(folder, "roads").ToDictionary(road => road.GetProperty("way").GetInt64());
            foreach (var way in extract.Ways)
            {
                if (way.Carriageway is not { } carriageway || !roads.TryGetValue(way.Id, out var road)) continue;

                var line = way.Nodes.Select(node => Px(plane.At(extract.Nodes.Lat[node], extract.Nodes.Lon[node]))).ToArray();
                if (!Seen(line)) continue;

                var widthPx = (float)Math.Max(1.5, carriageway.WidthM * pxPerM);
                var structure = road.TryGetProperty("structure", out var built) ? built.GetString() : null;
                if (structure is "bridge" or "viaduct") canvas.DrawLine(Color.Black, widthPx + 4, line);

                var colour = road.GetProperty("role").GetString() switch
                {
                    "zone_way" => Color.FromRgb(240, 150, 40),
                    "driveway" or "alley" => Color.FromRgb(200, 170, 130),
                    "track" => Color.FromRgb(160, 130, 90),
                    _ => Color.FromRgb(90, 90, 96),
                };
                if (structure is "tunnel" or "building_passage" or "covered") canvas.Draw(Pens.Dash(colour, widthPx), new SixLabors.ImageSharp.Drawing.Path(new LinearLineSegment(line)));
                else canvas.DrawLine(colour, widthPx, line);
            }

            foreach (var walk in Items(folder, "walk"))
            {
                var line = Line(walk.GetProperty("line"));
                if (!Seen(line)) continue;

                var (colour, widthPx) = walk.GetProperty("kind").GetString()! switch
                {
                    "sidewalk" => (Color.FromRgb(40, 160, 70), 2.5f),
                    "crossing" => (Color.FromRgb(210, 40, 200), 3f),
                    "steps" => (Color.FromRgb(120, 60, 20), 3f),
                    var kind when kind.Contains("area", StringComparison.Ordinal) => (Color.FromRgba(40, 160, 70, 90), 1f),
                    _ => (Color.FromRgb(130, 200, 120), 1.5f),
                };
                canvas.DrawLine(colour, widthPx, line);
            }

            foreach (var crossing in Items(folder, "crossings"))
            {
                var at = Px(plane.At(crossing.GetProperty("at")[0].GetInt32(), crossing.GetProperty("at")[1].GetInt32()));
                var colour = crossing.GetProperty("kind").GetString() switch
                {
                    "zebra" => Color.White,
                    "marked" => Color.FromRgb(250, 220, 40),
                    "signals" => Color.FromRgb(230, 30, 30),
                    "unmarked" or "unknown" or "informal" => Color.FromRgb(150, 150, 150),
                    "rail" or "tram" => Color.FromRgb(30, 90, 220),
                    _ => Color.Black,
                };
                var dot = new EllipsePolygon(at, 5f);
                canvas.Fill(colour, dot);
                canvas.Draw(Color.Black, 1f, dot);
            }

            foreach (var junction in Items(folder, "junctions"))
            {
                var at = Px(plane.At(junction.GetProperty("at")[0].GetInt32(), junction.GetProperty("at")[1].GetInt32()));
                if (at.X < 0 || at.Y < 0 || at.X > SizePx || at.Y > SizePx) continue;

                var colour = junction.GetProperty("control").GetString() switch
                {
                    "signals" => Color.FromRgb(230, 30, 30),
                    "blinking" => Color.FromRgb(250, 200, 0),
                    "signs" => Color.FromRgb(250, 140, 0),
                    "roundabout" => Color.FromRgb(30, 90, 220),
                    "priority_road" => Color.FromRgb(140, 50, 200),
                    _ => junction.TryGetProperty("rule", out var rule) && rule.GetString() == "right_hand" ? Color.FromRgb(60, 60, 60) : Color.FromRgb(190, 190, 190),
                };
                canvas.Fill(colour, new EllipsePolygon(at, 4f));
                if (junction.TryGetProperty("hints", out _)) canvas.Draw(Color.FromRgb(230, 0, 230), 2f, new EllipsePolygon(at, 9f));
            }

            foreach (var stop in Items(folder, "stops"))
            {
                var at = Px(plane.At(stop.GetProperty("at")[0].GetInt32(), stop.GetProperty("at")[1].GetInt32()));
                canvas.Fill(Color.FromRgb(30, 90, 220), new RectangularPolygon(at.X - 3, at.Y - 3, 6, 6));
            }
        });

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(into))!);
        image.SaveAsPng(into);
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{into}  {latDeg:F6},{lonDeg:F6}  {spanM:F0} m across  {pxPerM:F2} px/m"));
        return 0;
    }

    static Color? Tint(string kind) => kind switch
    {
        "parking" => Color.FromRgb(250, 225, 170),
        "parking_space" => Color.FromRgb(245, 205, 140),
        "garages" => Color.FromRgb(215, 205, 230),
        "fuel" or "car_wash" or "charging_station" or "services" or "bus_station" => Color.FromRgb(240, 190, 190),
        "industrial" or "port" or "railway" => Color.FromRgb(235, 215, 225),
        "education" or "health" or "civic" or "religious" => Color.FromRgb(250, 245, 190),
        "leisure" or "green" or "nature" or "cemetery" or "agriculture" or "allotments" => Color.FromRgb(210, 235, 200),
        "water" => Color.FromRgb(170, 210, 240),
        "beach" => Color.FromRgb(245, 235, 200),
        "square" => Color.FromRgb(235, 230, 225),
        "bridge" => Color.FromRgb(200, 200, 200),
        _ => null,
    };

    static IEnumerable<JsonElement> Items(string folder, string layer)
    {
        var path = System.IO.Path.Combine(folder, layer + ".json");
        if (!File.Exists(path)) yield break;

        using var file = JsonDocument.Parse(File.ReadAllBytes(path));
        foreach (var item in file.RootElement.GetProperty("items").EnumerateArray()) yield return item.Clone();
    }
}
