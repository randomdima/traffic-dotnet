using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;

namespace TrafficSimulation.Tools.OsmScan.Meta;

/// <summary>
/// <b>A height model at one arc-second (30 m)</b>, read at any place by bilinear interpolation between its four
/// nearest posts: the ground's (GEDTM30, a terrain model) or the surface's (the Copernicus DEM).
/// </summary>
/// <remarks>
/// <para>
/// <b>The surface model stands on whatever the radar saw</b> — a roof, a canopy, a bridge deck — so in a dense block
/// it reads high. <b>The terrain model is the ground</b>, buildings and trees taken off by a model fitted to lidar
/// heights; under a bridge it reads the valley or the water, never the deck. Either is a figure to weigh against the
/// levels OSM tags, never to overrule them.
/// </para>
/// <para>
/// The files are cloud-optimised GeoTIFFs; only the first image, full resolution, is read, of float samples
/// compressed by DEFLATE or LZW under any of TIFF's three predictors. Nothing else a TIFF may be is read.
/// </para>
/// </remarks>
internal sealed class Dem
{
    public const string Licence =
        "Copernicus DEM (30 m) © DLR e.V. 2010-2014 and © Airbus Defence and Space GmbH 2014-2018, provided under COPERNICUS by the European Union and ESA; all rights reserved";

    public const string TerrainLicence = "GEDTM30 v1.2, OpenGeoHub Foundation, CC BY 4.0 — https://doi.org/10.5281/zenodo.14900180";

    /// <summary>
    /// GEDTM30: one global BigTIFF of some 430 GB, in blocks of 2048 posts square, of which the map needs one or two.
    /// </summary>
    /// <remarks>
    /// Its GDAL metadata gives a scale of 0.1, which its float samples do not carry: they read in metres — Peresyp's
    /// lowland some 3 m, the plateau some 45 m — as the surface model reads open ground.
    /// </remarks>
    const string TerrainUrl = "https://s3.opengeohub.org/global/dtm/v1.2/gedtm_rf_m_30m_s_20060101_20151231_go_epsg.4326.3855_v1.2.tif";

    public const string TerrainName = "dtm-gedtm30.tif";

    const string Bucket = "https://copernicus-dem-30m.s3.amazonaws.com";

    /// <summary>The terrain's blocks over the sources' rectangle, read out of the global file and kept as a GeoTIFF of their own.</summary>
    public static string FetchTerrain(Sources sources) =>
        sources.Made(TerrainName, TerrainUrl, TerrainLicence, _ => "terrain of 2006–2015 inputs", into => Clip(TerrainUrl, sources, into));

    /// <summary>
    /// The blocks of a remote tiled BigTIFF over the sources' rectangle, asked for by range and written byte for byte
    /// under a classic TIFF's head of its own that places them — so the clip is read like any other tile here.
    /// </summary>
    static void Clip(string url, Sources sources, Stream into)
    {
        var head = Http.Bytes(url, 0, 16);
        if (head[0] != 'I' || head[1] != 'I' || BinaryPrimitives.ReadUInt16LittleEndian(head.AsSpan(2)) != 43) throw new InvalidDataException("not a little-endian BigTIFF");

        var ifd = (long)BinaryPrimitives.ReadUInt64LittleEndian(head.AsSpan(8));
        var count = (long)BinaryPrimitives.ReadUInt64LittleEndian(Http.Bytes(url, ifd, 8));
        var entries = Http.Bytes(url, ifd + 8, count * 20);
        var tags = new Dictionary<int, (int Type, long Count, long At)>();
        for (var entry = 0; entry < count; entry++)
        {
            var at = entry * 20;
            tags[BinaryPrimitives.ReadUInt16LittleEndian(entries.AsSpan(at))] =
                (BinaryPrimitives.ReadUInt16LittleEndian(entries.AsSpan(at + 2)), (long)BinaryPrimitives.ReadUInt64LittleEndian(entries.AsSpan(at + 4)), at + 12);
        }

        // A tag's values from the n-th on: the entry's own eight bytes where they hold them, else asked for by range.
        byte[] Raw(int tag, long from, long n, int size)
        {
            var (_, total, at) = tags[tag];
            if (total * size <= 8) return entries.AsSpan((int)(at + (from * size)), (int)(n * size)).ToArray();

            return Http.Bytes(url, (long)BinaryPrimitives.ReadUInt64LittleEndian(entries.AsSpan((int)at)) + (from * size), n * size);
        }

        long Value(int tag, long index = 0) => tags[tag].Type switch
        {
            3 => BinaryPrimitives.ReadUInt16LittleEndian(Raw(tag, index, 1, 2)),
            4 => BinaryPrimitives.ReadUInt32LittleEndian(Raw(tag, index, 1, 4)),
            16 => (long)BinaryPrimitives.ReadUInt64LittleEndian(Raw(tag, index, 1, 8)),
            var type => throw new InvalidDataException($"tag {tag} of type {type}"),
        };

        double[] Doubles(int tag)
        {
            var raw = Raw(tag, 0, tags[tag].Count, 8);
            return [.. Enumerable.Range(0, (int)tags[tag].Count).Select(k => BinaryPrimitives.ReadDoubleLittleEndian(raw.AsSpan(8 * k)))];
        }

        var (width, tileWidth, tileHeight) = (Value(256), (int)Value(322), (int)Value(323));
        var (scale, tie) = (Doubles(33550), Doubles(33922));
        var keys = Raw(34735, 0, tags[34735].Count, 2);
        var rasterType = 1;
        for (var k = 4; (k + 3) * 2 < keys.Length; k += 4)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(keys.AsSpan(k * 2)) == 1025) rasterType = BinaryPrimitives.ReadUInt16LittleEndian(keys.AsSpan((k + 3) * 2));
        }

        // Two posts past the rectangle each way, so every place on it has its four posts in the clip.
        const int PadPosts = 2;
        int Column(double lonDeg, int pad) => (int)Math.Floor((((lonDeg - tie[3]) / scale[0]) + pad) / tileWidth);
        int Row(double latDeg, int pad) => (int)Math.Floor((((tie[4] - latDeg) / scale[1]) + pad) / tileHeight);
        var (west, east) = (Column(sources.WestDeg, -PadPosts), Column(sources.EastDeg, PadPosts));
        var (north, south) = (Row(sources.NorthDeg, -PadPosts), Row(sources.SouthDeg, PadPosts));
        var across = (width + tileWidth - 1) / tileWidth;

        var blocks = new List<byte[]>();
        for (var row = north; row <= south; row++)
        {
            for (var column = west; column <= east; column++)
            {
                var block = (row * across) + column;
                blocks.Add(Http.Bytes(url, Value(324, block), Value(325, block)));
            }
        }

        var nodata = tags.ContainsKey(42113) ? Raw(42113, 0, tags[42113].Count, 1) : null;
        TiffWriter.Write(into, blocks, (east - west + 1) * tileWidth, (south - north + 1) * tileHeight, tileWidth, tileHeight,
            (int)Value(259), tags.ContainsKey(317) ? (int)Value(317) : 1, scale,
            [0, 0, 0, tie[3] + (west * tileWidth * scale[0]), tie[4] - (north * tileHeight * scale[1]), 0], rasterType, nodata);
    }

    readonly List<Tile> _tiles = [];

    /// <summary>Every tile over the sources' rectangle, fetched where it is not kept. The kept paths joined by ';'.</summary>
    public static string Fetch(Sources sources)
    {
        var kept = new List<string>();
        for (var lat = (int)Math.Floor(sources.SouthDeg); lat <= (int)Math.Floor(sources.NorthDeg); lat++)
        {
            for (var lon = (int)Math.Floor(sources.WestDeg); lon <= (int)Math.Floor(sources.EastDeg); lon++)
            {
                var name = string.Create(CultureInfo.InvariantCulture,
                    $"Copernicus_DSM_COG_10_{(lat >= 0 ? 'N' : 'S')}{Math.Abs(lat):00}_00_{(lon >= 0 ? 'E' : 'W')}{Math.Abs(lon):000}_00_DEM");
                kept.Add(sources.File($"dem-{name}.tif", $"{Bucket}/{name}/{name}.tif", Licence, _ => "radar acquired 2011–2015"));
            }
        }

        return string.Join(';', kept);
    }

    public Dem(string keptAt)
    {
        foreach (var path in keptAt.Split(';')) _tiles.Add(new Tile(File.ReadAllBytes(path)));
    }

    /// <summary>The ground's height in metres above the geoid at a place, NaN off every tile or on a void.</summary>
    public double HeightM(int lat, int lon)
    {
        var (latDeg, lonDeg) = (lat / 1e7, lon / 1e7);
        foreach (var tile in _tiles)
        {
            if (tile.Holds(latDeg, lonDeg)) return tile.HeightM(latDeg, lonDeg);
        }

        return double.NaN;
    }

    sealed class Tile
    {
        readonly byte[] _file;
        readonly int _width, _height, _tileWidth, _tileHeight, _compression, _predictor;
        readonly long[] _offsets, _counts;
        readonly double _westDeg, _northDeg, _stepXDeg, _stepYDeg, _centre;
        readonly float _void;
        readonly Dictionary<int, float[]> _decoded = [];

        public Tile(byte[] file)
        {
            _file = file;
            if (file[0] != 'I' || file[1] != 'I' || BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(2)) != 42)
            {
                throw new InvalidDataException("not a little-endian classic TIFF");
            }

            var ifd = (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(4));
            var count = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(ifd));
            var tags = new Dictionary<int, (int Type, long Count, int At)>();
            for (var entry = 0; entry < count; entry++)
            {
                var at = ifd + 2 + (12 * entry);
                tags[BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(at))] =
                    (BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(at + 2)), BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(at + 4)), at + 8);
            }

            long[] Values(int tag)
            {
                var (type, n, at) = tags[tag];
                var size = type switch { 3 => 2, 4 => 4, 12 => 8, 16 => 8, _ => 1 };
                var from = n * size <= 4 ? at : (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(at));
                var values = new long[n];
                for (var k = 0; k < n; k++)
                {
                    values[k] = type switch
                    {
                        3 => BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(from + (2 * k))),
                        4 => BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(from + (4 * k))),
                        16 => BinaryPrimitives.ReadInt64LittleEndian(file.AsSpan(from + (8 * k))),
                        _ => file[from + k],
                    };
                }

                return values;
            }

            double[] Doubles(int tag)
            {
                var (_, n, at) = tags[tag];
                var from = (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(at));
                return [.. Enumerable.Range(0, (int)n).Select(k => BinaryPrimitives.ReadDoubleLittleEndian(file.AsSpan(from + (8 * k))))];
            }

            (_width, _height) = ((int)Values(256)[0], (int)Values(257)[0]);
            if (Values(258)[0] != 32 || !tags.ContainsKey(339) || Values(339)[0] != 3) throw new InvalidDataException("a DEM tile not of 32-bit floats");

            _compression = (int)Values(259)[0];
            _predictor = tags.ContainsKey(317) ? (int)Values(317)[0] : 1;
            if (tags.ContainsKey(322))
            {
                (_tileWidth, _tileHeight) = ((int)Values(322)[0], (int)Values(323)[0]);
                (_offsets, _counts) = (Values(324), Values(325));
            }
            else
            {
                (_tileWidth, _tileHeight) = (_width, (int)Values(278)[0]);
                (_offsets, _counts) = (Values(273), Values(279));
            }

            var scale = Doubles(33550);
            var tie = Doubles(33922);
            (_stepXDeg, _stepYDeg) = (scale[0], scale[1]);
            (_westDeg, _northDeg) = (tie[3] - (tie[0] * _stepXDeg), tie[4] + (tie[1] * _stepYDeg));

            // GTRasterTypeGeoKey: a post standing at its pixel's corner (PixelIsPoint, 2) or its middle (PixelIsArea, 1).
            _centre = 0.5;
            if (tags.ContainsKey(34735))
            {
                var keys = Values(34735);
                for (var k = 4; k + 3 < keys.Length; k += 4)
                {
                    if (keys[k] == 1025 && keys[k + 3] == 2) _centre = 0;
                }
            }

            _void = tags.TryGetValue(42113, out var nodata)
                ? float.Parse(System.Text.Encoding.ASCII.GetString(file, nodata.Count <= 4 ? nodata.At : (int)BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(nodata.At)), (int)nodata.Count).TrimEnd('\0'), CultureInfo.InvariantCulture)
                : float.NaN;
        }

        public bool Holds(double latDeg, double lonDeg) =>
            lonDeg >= _westDeg && lonDeg < _westDeg + (_width * _stepXDeg) && latDeg <= _northDeg && latDeg > _northDeg - (_height * _stepYDeg);

        public double HeightM(double latDeg, double lonDeg)
        {
            var x = ((lonDeg - _westDeg) / _stepXDeg) - _centre;
            var y = ((_northDeg - latDeg) / _stepYDeg) - _centre;
            var (x0, y0) = ((int)Math.Floor(x), (int)Math.Floor(y));
            var (fx, fy) = (x - x0, y - y0);
            var (a, b, c, d) = (Post(x0, y0), Post(x0 + 1, y0), Post(x0, y0 + 1), Post(x0 + 1, y0 + 1));
            return (((a * (1 - fx)) + (b * fx)) * (1 - fy)) + (((c * (1 - fx)) + (d * fx)) * fy);
        }

        double Post(int x, int y)
        {
            (x, y) = (Math.Clamp(x, 0, _width - 1), Math.Clamp(y, 0, _height - 1));
            var across = (_width + _tileWidth - 1) / _tileWidth;
            var tile = ((y / _tileHeight) * across) + (x / _tileWidth);
            if (!_decoded.TryGetValue(tile, out var samples)) _decoded[tile] = samples = Decode(tile);

            var value = samples[((y % _tileHeight) * _tileWidth) + (x % _tileWidth)];
            return value == _void || float.IsNaN(value) || value < -1000 ? double.NaN : value;
        }

        float[] Decode(int tile)
        {
            var packed = _file.AsSpan((int)_offsets[tile], (int)_counts[tile]).ToArray();
            var bytes = _compression switch
            {
                1 => packed,
                5 => Lzw(packed, _tileWidth * _tileHeight * 4),
                8 or 32946 => Inflate(packed),
                _ => throw new InvalidDataException($"a DEM tile compressed by scheme {_compression}"),
            };

            var rowBytes = _tileWidth * 4;
            var samples = new float[_tileWidth * _tileHeight];
            var row = new byte[rowBytes];
            for (var y = 0; y < _tileHeight && (y + 1) * rowBytes <= bytes.Length; y++)
            {
                var source = bytes.AsSpan(y * rowBytes, rowBytes);
                switch (_predictor)
                {
                    case 3:
                        // Floating-point predictor: byte-wise differences over the row, its bytes in planes most
                        // significant first (Adobe Photoshop TIFF Technical Note 3).
                        for (var k = 1; k < rowBytes; k++) source[k] += source[k - 1];
                        for (var k = 0; k < _tileWidth; k++)
                        {
                            row[(4 * k) + 3] = source[k];
                            row[(4 * k) + 2] = source[_tileWidth + k];
                            row[(4 * k) + 1] = source[(2 * _tileWidth) + k];
                            row[4 * k] = source[(3 * _tileWidth) + k];
                        }

                        break;
                    case 2:
                        // Horizontal differencing over the samples' 32-bit words, which libtiff applies to floats too.
                        source.CopyTo(row);
                        for (var k = 1; k < _tileWidth; k++)
                        {
                            BinaryPrimitives.WriteUInt32LittleEndian(row.AsSpan(4 * k),
                                BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(4 * k)) + BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(4 * (k - 1))));
                        }

                        break;
                    case 1:
                        source.CopyTo(row);
                        break;
                    default:
                        throw new InvalidDataException($"a float DEM tile under predictor {_predictor}");
                }

                for (var k = 0; k < _tileWidth; k++) samples[(y * _tileWidth) + k] = BinaryPrimitives.ReadSingleLittleEndian(row.AsSpan(4 * k));
            }

            return samples;
        }

        static byte[] Inflate(byte[] packed)
        {
            using var into = new MemoryStream();
            using (var zlib = new ZLibStream(new MemoryStream(packed), CompressionMode.Decompress)) zlib.CopyTo(into);
            return into.ToArray();
        }

        /// <summary>TIFF's LZW: codes most significant bit first, widened one code early.</summary>
        static byte[] Lzw(byte[] packed, int expected)
        {
            var output = new List<byte>(expected);
            var table = new List<byte[]>(4096);
            void Reset()
            {
                table.Clear();
                for (var k = 0; k < 256; k++) table.Add([(byte)k]);
                table.Add([]);
                table.Add([]);
            }

            Reset();
            var (width, buffer, held) = (9, 0, 0);
            byte[]? previous = null;
            for (var at = 0; ;)
            {
                while (held < width && at < packed.Length)
                {
                    buffer = (int)(((buffer << 8) | packed[at++]) & 0xFFFFFF);
                    held += 8;
                }

                if (held < width) break;

                var code = (buffer >> (held - width)) & ((1 << width) - 1);
                held -= width;
                if (code == 257) break;
                if (code == 256)
                {
                    Reset();
                    width = 9;
                    previous = null;
                    continue;
                }

                byte[] entry;
                if (code < table.Count) entry = table[code];
                else if (previous is not null) entry = [.. previous, previous[0]];
                else throw new InvalidDataException("a broken LZW stream");

                output.AddRange(entry);
                if (previous is not null) table.Add([.. previous, entry[0]]);
                previous = entry;
                width = table.Count + 1 >= 2048 ? 12 : table.Count + 1 >= 1024 ? 11 : table.Count + 1 >= 512 ? 10 : 9;
            }

            return [.. output];
        }
    }
}

/// <summary>
/// <b>A classic little-endian tiled GeoTIFF of 32-bit float samples</b>, its blocks handed over already compressed
/// and written as they are, placed on the WGS 84 grid by a pixel scale and a tie point.
/// </summary>
internal static class TiffWriter
{
    public static void Write(
        Stream into, List<byte[]> blocks, long width, long height, int tileWidth, int tileHeight, int compression, int predictor,
        double[] scale, double[] tie, int rasterType, byte[]? nodata)
    {
        using var file = new BinaryWriter(into, System.Text.Encoding.ASCII, leaveOpen: true);
        file.Write("II"u8);
        file.Write((ushort)42);
        file.Write(0u);
        var offsets = new List<uint>(blocks.Count);
        foreach (var block in blocks)
        {
            offsets.Add((uint)into.Position);
            file.Write(block);
            if (into.Position % 2 == 1) file.Write((byte)0);
        }

        var entries = new List<(ushort Tag, ushort Type, uint Count, byte[] Data)>
        {
            (256, 4, 1, Words([(uint)width])),
            (257, 4, 1, Words([(uint)height])),
            (258, 3, 1, Shorts([32])),
            (259, 3, 1, Shorts([(ushort)compression])),
            (262, 3, 1, Shorts([1])),
            (277, 3, 1, Shorts([1])),
            (284, 3, 1, Shorts([1])),
            (317, 3, 1, Shorts([(ushort)predictor])),
            (322, 4, 1, Words([(uint)tileWidth])),
            (323, 4, 1, Words([(uint)tileHeight])),
            (324, 4, (uint)blocks.Count, Words([.. offsets])),
            (325, 4, (uint)blocks.Count, Words([.. blocks.Select(block => (uint)block.Length)])),
            (339, 3, 1, Shorts([3])),
            (33550, 12, 3, Doubles(scale)),
            (33922, 12, 6, Doubles(tie)),
            // GeoKey directory: model type geographic (1024), raster type (1025), geographic CRS WGS 84 (2048).
            (34735, 3, 16, Shorts([1, 1, 0, 3, 1024, 0, 1, 2, 1025, 0, 1, (ushort)rasterType, 2048, 0, 1, 4326])),
        };
        if (nodata is not null) entries.Add((42113, 2, (uint)nodata.Length, nodata));

        var ifdAt = into.Position;
        var extraAt = ifdAt + 2 + (12 * entries.Count) + 4;
        file.Write((ushort)entries.Count);
        var extras = new List<byte[]>();
        foreach (var (tag, type, count, data) in entries)
        {
            file.Write(tag);
            file.Write(type);
            file.Write(count);
            if (data.Length <= 4)
            {
                file.Write(data);
                file.Write(new byte[4 - data.Length]);
                continue;
            }

            file.Write((uint)extraAt);
            extras.Add(data);
            extraAt += data.Length + (data.Length % 2);
        }

        file.Write(0u);
        foreach (var data in extras)
        {
            file.Write(data);
            if (data.Length % 2 == 1) file.Write((byte)0);
        }

        into.Position = 4;
        file.Write((uint)ifdAt);

        static byte[] Shorts(ushort[] values) => [.. values.SelectMany(value => BitConverter.GetBytes(value))];

        static byte[] Words(uint[] values) => [.. values.SelectMany(value => BitConverter.GetBytes(value))];

        static byte[] Doubles(double[] values) => [.. values.SelectMany(value => BitConverter.GetBytes(value))];
    }
}
