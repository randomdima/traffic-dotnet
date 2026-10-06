using System.Numerics;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TrafficSimulation.CityGen.Traced;

namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>OSM's own lanes drawn over a shot of the traced map</b>, at the shot's own framing (its <c>.json</c>
/// beside it): every lane's middle as OSM's tags place it, red driven along the way as drawn, blue against
/// it and magenta both ways; each carriageway's edges in yellow; each surface a mapper outlined
/// (<c>area:highway</c>) in cyan. Written beside the shot as <c>&lt;shot&gt;-osm.png</c>.
/// </summary>
internal static class Draw
{
    static readonly Color Forward = Color.FromRgb(235, 40, 40);
    static readonly Color Backward = Color.FromRgb(40, 110, 245);
    static readonly Color Both = Color.FromRgb(225, 40, 225);
    static readonly Color Edge = Color.FromRgb(250, 220, 40);
    static readonly Color Surface = Color.FromRgb(40, 225, 235);

    public static int Run(string root, string shot, string map)
    {
        using var figures = JsonDocument.Parse(File.ReadAllBytes(shot + ".json"));
        var cell = figures.RootElement.TryGetProperty("Cells", out var cells) ? cells[0] : figures.RootElement;
        var centreM = new Vector2(cell.GetProperty("CentreM")[0].GetSingle(), cell.GetProperty("CentreM")[1].GetSingle());
        var pxPerM = cell.GetProperty("PxPerM").GetSingle();
        var widthPx = cell.GetProperty("WidthPx").GetSingle();
        var heightPx = cell.GetProperty("HeightPx").GetSingle();
        var leftPx = cell.TryGetProperty("LeftPx", out var left) ? left.GetSingle() : 0f;
        var topPx = cell.TryGetProperty("TopPx", out var top) ? top.GetSingle() : 0f;

        var extract = JsonSerializer.Deserialize(File.ReadAllBytes(Path.Combine(root, "towns", "traced", $"{map}.json")), OsmExtractJson.Default.OsmExtract)
                      ?? throw new InvalidDataException($"{map}: no extract");

        // The shot is in the map's frame, which a crop moves off the extract's.
        var frame = CityGen.Map.TownMap.Read(Scan.MapFile(root, map)).Frame;
        var projection = frame.Projection();
        var placedM = new Vector2[extract.Nodes.Id.Length];
        for (var node = 0; node < placedM.Length; node++)
        {
            var (x, y) = frame.Place(projection, extract.Nodes, node);
            placedM[node] = new Vector2((float)x, (float)y);
        }

        var halfM = new Vector2(widthPx, heightPx) * (0.5f / pxPerM);
        var (leastM, mostM) = (centreM - halfM, centreM + halfM);

        using var image = Image.Load<Rgba32>(shot);
        image.Mutate(canvas =>
        {
            foreach (var surface in extract.Areas)
            {
                var lineM = surface.Nodes.Select(node => placedM[node]).ToArray();
                if (Seen(lineM)) canvas.DrawLine(Surface, 1.5f, Px(lineM));
            }

            foreach (var way in extract.Ways)
            {
                if (way.Carriageway is not { } carriageway) continue;

                var lineM = way.Nodes.Select(node => placedM[node]).ToArray();
                if (!Seen(lineM)) continue;

                var offM = new Vector2[lineM.Length];
                var halfWidthM = carriageway.WidthM * 0.5f;
                foreach (var sideM in (ReadOnlySpan<float>)[carriageway.CentreOffsetM - halfWidthM, carriageway.CentreOffsetM + halfWidthM])
                {
                    OsmCarriageway.OffsetInto(lineM, sideM, offM);
                    canvas.DrawLine(Edge, 1f, Px(offM));
                }

                for (var lane = 0; lane < carriageway.Lanes.Length; lane++)
                {
                    OsmCarriageway.OffsetInto(lineM, carriageway.LaneOffsetM(lane), offM);
                    var colour = carriageway.Lanes[lane].Way switch
                    {
                        OsmLaneWay.Forward => Forward,
                        OsmLaneWay.Backward => Backward,
                        _ => Both,
                    };
                    canvas.DrawLine(colour, 2f, Px(offM));
                }
            }
        });

        var into = Path.Combine(Path.GetDirectoryName(shot) ?? ".", Path.GetFileNameWithoutExtension(shot) + "-osm.png");
        image.SaveAsPng(into);
        Console.WriteLine(into);
        return 0;

        bool Seen(Vector2[] lineM)
        {
            var (least, most) = (lineM[0], lineM[0]);
            foreach (var atM in lineM) (least, most) = (Vector2.Min(least, atM), Vector2.Max(most, atM));
            return most.X >= leastM.X && least.X <= mostM.X && most.Y >= leastM.Y && least.Y <= mostM.Y;
        }

        PointF[] Px(Vector2[] lineM) =>
            [.. lineM.Select(atM => new PointF(leftPx + (widthPx * 0.5f) + ((atM.X - centreM.X) * pxPerM), topPx + (heightPx * 0.5f) + ((atM.Y - centreM.Y) * pxPerM)))];
    }
}
