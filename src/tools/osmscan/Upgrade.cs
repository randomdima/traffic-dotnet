using System.Text;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;
using TrafficSimulation.Tools.OsmScan.Meta;

namespace TrafficSimulation.Tools.OsmScan;

/// <summary>
/// <b>A traced map of version 6 rewritten as this build writes one</b>, every edit made to it kept: its frame, points,
/// roads and coast as they stand, its seed its relation, who its town stands said by its whole map's zone, and its zones
/// laid again off the layers (<see cref="ZoneHints"/>) — moved from <c>towns/traced/</c> to <c>towns/</c>, where every
/// map is. Run by <c>qq osm --upgrade</c>.
/// </summary>
internal static class Upgrade
{
    const uint Magic = 0x50414D54;

    const ushort Version = 6;

    const double StepsPerM = 1000.0;

    /// <summary>Version 6's road flags.</summary>
    const int Bridge = 1, Roundabout = 2, Offset = 4, Width = 8, LanesTagged = 16, Marked = 32, SharedShift = 6;

    public static int Run(string root, string map)
    {
        var path = Path.Combine(root, "towns", "traced", $"{map}.map");
        using var reader = new BinaryReader(File.OpenRead(path), Encoding.UTF8);
        if (reader.ReadUInt32() != Magic || reader.ReadUInt16() is var version && version != Version)
        {
            throw new InvalidDataException($"{path}: not a traced map of version {Version}.");
        }

        var (name, description, licence, osmBase) = (reader.ReadString(), reader.ReadString(), reader.ReadString(), reader.ReadString());
        var relation = Signed64(reader);
        var frame = new OsmFrame
        {
            Lat0Deg = reader.ReadDouble(), Lon0Deg = reader.ReadDouble(), WestM = reader.ReadDouble(), SouthM = reader.ReadDouble(),
            WidthM = reader.ReadDouble(), HeightM = reader.ReadDouble(), MarginM = reader.ReadDouble(),
        };

        var pointM = new Vector2D[reader.Read7BitEncodedInt()];
        var (x, y) = (0L, 0L);
        for (var point = 0; point < pointM.Length; point++)
        {
            (x, y) = (x + Signed(reader), y + Signed(reader));
            pointM[point] = new Vector2D(x / StepsPerM, y / StepsPerM);
        }

        var classes = new string[reader.Read7BitEncodedInt()];
        for (var at = 0; at < classes.Length; at++) classes[at] = reader.ReadString();

        var roads = new TracedRoad[reader.Read7BitEncodedInt()];
        var (id, last) = (0L, 0);
        for (var road = 0; road < roads.Length; road++)
        {
            id += Signed64(reader);
            roads[road] = Road(reader, id, classes, ref last);
        }

        var coast = new int[reader.Read7BitEncodedInt()][];
        for (var line = 0; line < coast.Length; line++) coast[line] = Indices(reader, ref last);

        SkipZones(reader);
        var (people, cars) = (reader.Read7BitEncodedInt(), reader.Read7BitEncodedInt());
        if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException($"{path}: bytes past the end of the map.");

        reader.Dispose();
        var held = new TownMap
        {
            Name = name, Description = description, Licence = licence, OsmBase = osmBase, Seed = (ulong)relation, Frame = frame,
            PointM = pointM, Roads = roads, Coast = coast,
            Zones = TownMap.ZoneArrays.Whole(new((float)frame.WidthM, (float)frame.HeightM), ZoneKind.Town, [(ZoneParam.People, people), (ZoneParam.Cars, cars)]),
        };

        var into = Scan.MapFile(root, map);
        Console.WriteLine($"  {map}: version {Version}, {new FileInfo(path).Length / 1024} KB, into {into}");
        var upgraded = Zoning.Zoned(root, map, held);
        upgraded.Write(into);
        File.Delete(path);
        Crop.Describe(upgraded, into);
        Zoning.Tell(upgraded.Zones);
        return 0;
    }

    static TracedRoad Road(BinaryReader reader, long id, string[] classes, ref int last)
    {
        var highway = classes[reader.Read7BitEncodedInt()];
        var flags = reader.ReadByte();
        var lanes = reader.ReadByte();
        var offsetShare = (flags & Offset) != 0 ? Signed(reader) / 1000f : 0f;
        float? widthM = (flags & Width) != 0 ? reader.Read7BitEncodedInt() / (float)StepsPerM : null;
        return new TracedRoad
        {
            OsmId = id, Highway = highway, Bridge = (flags & Bridge) != 0, Roundabout = (flags & Roundabout) != 0,
            LanesForward = lanes & 15, LanesBackward = lanes >> 4, LanesShared = flags >> SharedShift,
            LanesTagged = (flags & LanesTagged) != 0, Marked = (flags & Marked) != 0, CentreOffsetShare = offsetShare, WidthM = widthM,
            Points = Indices(reader, ref last),
        };
    }

    static void SkipZones(BinaryReader reader)
    {
        var (count, _, _) = (reader.Read7BitEncodedInt(), reader.Read7BitEncodedInt(), reader.Read7BitEncodedInt());
        for (var zone = 0; zone < count; zone++)
        {
            (_, _) = (reader.ReadByte(), reader.ReadByte());
            for (var ring = reader.Read7BitEncodedInt(); ring > 0; ring--)
            {
                for (var point = reader.Read7BitEncodedInt(); point > 0; point--) (_, _) = (Signed(reader), Signed(reader));
            }
        }
    }

    static int[] Indices(BinaryReader reader, ref int last)
    {
        var indices = new int[reader.Read7BitEncodedInt()];
        for (var at = 0; at < indices.Length; at++) indices[at] = last += Signed(reader);
        return indices;
    }

    static int Signed(BinaryReader reader)
    {
        var value = (uint)reader.Read7BitEncodedInt();
        return (int)(value >> 1) ^ -(int)(value & 1);
    }

    static long Signed64(BinaryReader reader)
    {
        var value = (ulong)reader.Read7BitEncodedInt64();
        return (long)(value >> 1) ^ -(long)(value & 1);
    }
}
