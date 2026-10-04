using System.IO.Compression;
using System.Text.Json;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>Buildings a machine traced off imagery</b> (Microsoft's Global ML Building Footprints): every footprint over
/// the map, with the height and confidence the model gave it where it gave one.
/// </summary>
/// <remarks>
/// The dataset is filed by Bing quadkey at zoom 9 and by country; a quadkey's file is a gzip of one GeoJSON feature
/// a line, whatever its <c>.csv.gz</c> name says. A height or confidence of −1 is the model giving none, which in
/// Ukraine is every height. The release is dated in its path and the list of files moves with it (the dataset's
/// README names the current one).
/// </remarks>
internal static class MlBuildings
{
    public const string Licence = "Microsoft Global ML Building Footprints, ODbL 1.0 — https://github.com/microsoft/GlobalMLBuildingFootprints";

    const string Links = "https://bfppub.blob.core.windows.net/%24web/2026-08-13/dataset-links.csv";

    /// <summary>When the footprints stand: the release's date, which is not the imagery's — that is years older and not given.</summary>
    const string Release = "release 2026-08-13, traced off imagery of years before";

    const int Zoom = 9;

    /// <summary>Every quadkey file over the sources' rectangle, fetched where it is not kept.</summary>
    public static string[] Fetch(Sources sources)
    {
        var links = sources.File("ml-buildings-links.csv", Links, Licence);
        var keys = QuadKeys(sources.SouthDeg, sources.WestDeg, sources.NorthDeg, sources.EastDeg);
        var kept = new List<string>();
        foreach (var line in File.ReadLines(links).Skip(1))
        {
            var cells = line.Split(',');
            if (cells.Length < 3 || !keys.Contains(cells[1])) continue;

            kept.Add(sources.File($"ml-buildings-{cells[0]}-{cells[1]}-{kept.Count}.csv.gz", cells[2], Licence, _ => Release));
        }

        if (kept.Count == 0) throw new InvalidDataException($"no ML building file for quadkeys {string.Join(", ", keys)}");
        return [.. kept];
    }

    /// <summary>Every footprint in the rectangle: its outer ring as lat, lon pairs in 1e-7°, its height and confidence.</summary>
    public static List<(int[] Ring, double HeightM, double Confidence)> Read(string[] kept, Sources sources)
    {
        var read = new List<(int[] Ring, double HeightM, double Confidence)>();
        foreach (var path in kept)
        {
            using var file = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
            using var lines = new StreamReader(file);
            for (var line = lines.ReadLine(); line is not null; line = lines.ReadLine())
            {
                if (line.Length == 0) continue;

                using var feature = JsonDocument.Parse(line);
                var geometry = feature.RootElement.GetProperty("geometry");
                if (geometry.GetProperty("type").GetString() != "Polygon") continue;

                var outer = geometry.GetProperty("coordinates")[0];
                var ring = new int[outer.GetArrayLength() * 2];
                var inside = false;
                var at = 0;
                foreach (var place in outer.EnumerateArray())
                {
                    var (lon, lat) = (place[0].GetDouble(), place[1].GetDouble());
                    inside |= lat >= sources.SouthDeg && lat <= sources.NorthDeg && lon >= sources.WestDeg && lon <= sources.EastDeg;
                    ring[at++] = (int)Math.Round(lat * 1e7);
                    ring[at++] = (int)Math.Round(lon * 1e7);
                }

                if (!inside) continue;

                var properties = feature.RootElement.GetProperty("properties");
                read.Add((ring, Number(properties, "height"), Number(properties, "confidence")));
            }
        }

        return read;

        static double Number(JsonElement properties, string name) =>
            properties.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : -1;
    }

    /// <summary>The Bing quadkeys at <see cref="Zoom"/> a rectangle of degrees meets.</summary>
    static HashSet<string> QuadKeys(double south, double west, double north, double east)
    {
        var (x0, y0) = Tile(north, west);
        var (x1, y1) = Tile(south, east);
        var keys = new HashSet<string>();
        for (var x = x0; x <= x1; x++)
        {
            for (var y = y0; y <= y1; y++)
            {
                var key = new char[Zoom];
                for (var level = Zoom; level > 0; level--)
                {
                    var mask = 1 << (level - 1);
                    key[Zoom - level] = (char)('0' + ((x & mask) != 0 ? 1 : 0) + ((y & mask) != 0 ? 2 : 0));
                }

                keys.Add(new string(key));
            }
        }

        return keys;

        static (int X, int Y) Tile(double latDeg, double lonDeg)
        {
            var n = 1 << Zoom;
            var lat = double.DegreesToRadians(latDeg);
            return ((int)Math.Floor((lonDeg + 180) / 360 * n),
                    (int)Math.Floor((1 - (Math.Log(Math.Tan(lat) + (1 / Math.Cos(lat))) / Math.PI)) / 2 * n));
        }
    }
}
