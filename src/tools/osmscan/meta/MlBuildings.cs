using System.Globalization;
using System.IO.Compression;
using System.Text;
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
    /// <remarks>
    /// Read once a process (<see cref="Held"/>): the layers and their audit both read it. A line is read by a reader of
    /// its own rather than parsed into a document: the files hold near a million lines, and a document a line costs more in
    /// renting and returning its buffers than in reading them.
    /// </remarks>
    public static List<(int[] Ring, double HeightM, double Confidence)> Read(string[] kept, Sources sources) =>
        Held.Once(string.Create(CultureInfo.InvariantCulture, $"{Held.Files(kept)}\n{sources.SouthDeg:R},{sources.WestDeg:R},{sources.NorthDeg:R},{sources.EastDeg:R}"),
            () => ReadFiles(kept, sources));

    static List<(int[] Ring, double HeightM, double Confidence)> ReadFiles(string[] kept, Sources sources)
    {
        var read = new List<(int[] Ring, double HeightM, double Confidence)>();
        byte[] utf8 = [];
        var outer = new List<double>();
        foreach (var path in kept)
        {
            using var file = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
            using var lines = new StreamReader(file);
            for (var line = lines.ReadLine(); line is not null; line = lines.ReadLine())
            {
                if (line.Length == 0) continue;

                var most = Encoding.UTF8.GetMaxByteCount(line.Length);
                if (most > utf8.Length) utf8 = new byte[most];

                var feature = new Utf8JsonReader(utf8.AsSpan(0, Encoding.UTF8.GetBytes(line, utf8)));
                if (!Polygon(ref feature, outer, out var heightM, out var confidence)) continue;

                var ring = new int[outer.Count];
                var inside = false;
                for (var at = 0; at < outer.Count; at += 2)
                {
                    var (lon, lat) = (outer[at], outer[at + 1]);
                    inside |= lat >= sources.SouthDeg && lat <= sources.NorthDeg && lon >= sources.WestDeg && lon <= sources.EastDeg;
                    ring[at] = (int)Math.Round(lat * 1e7);
                    ring[at + 1] = (int)Math.Round(lon * 1e7);
                }

                if (inside) read.Add((ring, heightM, confidence));
            }
        }

        return read;
    }

    /// <summary>
    /// One feature's outer ring as lon, lat pairs and its height and confidence — each −1 where it is not a number —
    /// where its geometry is a polygon; false where it is any other. The last of a name given twice is the one read.
    /// </summary>
    static bool Polygon(ref Utf8JsonReader feature, List<double> outer, out double heightM, out double confidence)
    {
        (heightM, confidence) = (-1, -1);
        var (polygon, placed) = (false, false);
        Open(ref feature, JsonTokenType.StartObject);
        while (Next(ref feature) == JsonTokenType.PropertyName)
        {
            if (feature.ValueTextEquals("geometry"u8))
            {
                (polygon, placed) = (false, false);
                outer.Clear();
                Open(ref feature, JsonTokenType.StartObject);
                while (Next(ref feature) == JsonTokenType.PropertyName)
                {
                    var (type, coordinates) = (feature.ValueTextEquals("type"u8), feature.ValueTextEquals("coordinates"u8));
                    Next(ref feature);
                    if (type) polygon = feature.GetString() == "Polygon";
                    else if (coordinates) placed = FirstRing(ref feature, outer);
                    else feature.Skip();
                }
            }
            else if (feature.ValueTextEquals("properties"u8))
            {
                (heightM, confidence) = (-1, -1);
                Open(ref feature, JsonTokenType.StartObject);
                while (Next(ref feature) == JsonTokenType.PropertyName)
                {
                    var (height, confident) = (feature.ValueTextEquals("height"u8), feature.ValueTextEquals("confidence"u8));
                    var number = Next(ref feature) == JsonTokenType.Number ? feature.GetDouble() : -1;
                    feature.Skip();
                    if (height) heightM = number;
                    else if (confident) confidence = number;
                }
            }
            else
            {
                Next(ref feature);
                feature.Skip();
            }
        }

        if (!polygon) return false;

        return placed ? true : throw new InvalidDataException("an ML footprint's polygon whose first ring is not of places");
    }

    /// <summary>
    /// A geometry's first ring, the reader standing on its <c>coordinates</c>: whether it is a ring of places — arrays of
    /// two numbers or more — and those places' first two numbers. The reader is left on the coordinates' end.
    /// </summary>
    static bool FirstRing(ref Utf8JsonReader feature, List<double> outer)
    {
        if (feature.TokenType != JsonTokenType.StartArray)
        {
            feature.Skip();
            return false;
        }

        var depth = feature.CurrentDepth;
        var placed = Next(ref feature) == JsonTokenType.StartArray;
        while (placed && Next(ref feature) == JsonTokenType.StartArray)
        {
            if (Next(ref feature) != JsonTokenType.Number) break;

            var lon = feature.GetDouble();
            if (Next(ref feature) != JsonTokenType.Number) break;

            outer.Add(lon);
            outer.Add(feature.GetDouble());
            while (Next(ref feature) != JsonTokenType.EndArray) feature.Skip();
        }

        placed &= feature.TokenType == JsonTokenType.EndArray && feature.CurrentDepth == depth + 1;
        while (feature.TokenType != JsonTokenType.EndArray || feature.CurrentDepth != depth) Next(ref feature);
        return placed;
    }

    static void Open(ref Utf8JsonReader feature, JsonTokenType opening)
    {
        if (Next(ref feature) != opening) throw new InvalidDataException($"an ML footprint with {feature.TokenType} where {opening} is read");
    }

    static JsonTokenType Next(ref Utf8JsonReader feature) =>
        feature.Read() ? feature.TokenType : throw new InvalidDataException("an ML footprint cut off");

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
