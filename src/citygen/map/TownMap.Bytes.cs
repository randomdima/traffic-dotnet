using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;

namespace TrafficSimulation.CityGen.Map;

internal sealed partial record TownMap
{
    public static TownMap Read(string path) => Read(File.ReadAllBytes(path), path);

    public static TownMap Read(ReadOnlySpan<byte> file, string what)
    {
        TownMap map;
        try
        {
            var bytes = new Bytes(file);
            if (bytes.UInt32() != Magic) throw new InvalidDataException($"{what}: not a map.");
            if (bytes.UInt16() is var version && version != Version)
            {
                throw new InvalidDataException($"{what}: a map of version {version}, and this build reads {Version} — `qq osm --upgrade` rewrites a traced one.");
            }

            map = ReadBody(ref bytes);
            if (bytes.Left != 0) throw new InvalidDataException($"{what}: {bytes.Left} bytes past the end of the map.");
        }
        catch (Exception cut) when (cut is ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            throw new InvalidDataException($"{what}: a map cut short.");
        }

        map.Check(what);
        return map;
    }

    /// <summary>
    /// The name and the description off the head of a file, which is all the menu reads of it and all a page holds of
    /// one until it is opened (<see cref="MapHead"/>).
    /// </summary>
    public static (string Name, string Description) Head(ReadOnlySpan<byte> head, string what)
    {
        try
        {
            var bytes = new Bytes(head);
            if (bytes.UInt32() != Magic || bytes.UInt16() != Version) throw new InvalidDataException($"{what}: not a map of version {Version}.");

            return (bytes.String(), bytes.String());
        }
        catch (Exception cut) when (cut is ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            throw new InvalidDataException($"{what}: no description in its first {head.Length} bytes, where a map's head holds it.");
        }
    }

    /// <summary>The map as it reads back once written: every place and setting held to the step it is written in.</summary>
    public TownMap Held()
    {
        using var bytes = new MemoryStream();
        Write(bytes);
        return Read(bytes.ToArray(), Name);
    }

    static TownMap ReadBody(ref Bytes bytes)
    {
        var (name, description, licence, osmBase) = (bytes.String(), bytes.String(), bytes.String(), bytes.String());
        var seed = bytes.Varint();
        var frame = new OsmFrame
        {
            Lat0Deg = bytes.Double(), Lon0Deg = bytes.Double(), WestM = bytes.Double(), SouthM = bytes.Double(),
            WidthM = bytes.Double(), HeightM = bytes.Double(), MarginM = bytes.Double(),
        };

        var pointM = new Vector2D[bytes.Count()];
        var (x, y) = (0, 0);
        for (var point = 0; point < pointM.Length; point++)
        {
            x += bytes.Signed();
            y += bytes.Signed();
            pointM[point] = new Vector2D(x / StepsPerM, y / StepsPerM);
        }

        var classes = new string[bytes.Count()];
        for (var at = 0; at < classes.Length; at++) classes[at] = bytes.String();

        var roads = new TracedRoad[bytes.Count()];
        var (id, last) = (0L, 0);
        for (var road = 0; road < roads.Length; road++)
        {
            id += bytes.Signed64();
            roads[road] = ReadRoad(ref bytes, id, classes, ref last);
        }

        var coast = new int[bytes.Count()][];
        for (var line = 0; line < coast.Length; line++) coast[line] = ReadIndices(ref bytes, ref last);

        return new TownMap
        {
            Name = name, Description = description, Licence = licence, OsmBase = osmBase, Seed = seed, Frame = frame,
            PointM = pointM, Roads = roads, Coast = coast, Waters = ReadWaters(ref bytes), Courses = ReadCourses(ref bytes), Buildings = ReadStood(ref bytes),
            Props = ReadProps(ref bytes), Lots = ReadLots(ref bytes), Zones = ReadZones(ref bytes),
        };
    }

    static TracedRoad ReadRoad(ref Bytes bytes, long id, string[] classes, ref int last)
    {
        var highway = classes[bytes.Count()];
        var flags = bytes.Byte();
        var lanes = bytes.Byte();
        var offsetShare = (flags & RoadFlags.Offset) != 0 ? bytes.Signed() / (float)OffsetSteps : 0f;
        float? widthM = (flags & RoadFlags.Width) != 0 ? bytes.Count() / (float)StepsPerM : null;
        return new TracedRoad
        {
            OsmId = id,
            Highway = highway,
            Bridge = (flags & RoadFlags.Bridge) != 0,
            Roundabout = (flags & RoadFlags.Roundabout) != 0,
            LanesForward = lanes & LanesMost,
            LanesBackward = lanes >> 4,
            LanesShared = flags >> RoadFlags.SharedShift,
            LanesTagged = (flags & RoadFlags.LanesTagged) != 0,
            Marked = (flags & RoadFlags.Marked) != 0,
            CentreOffsetShare = offsetShare,
            WidthM = widthM,
            Points = ReadIndices(ref bytes, ref last),
        };
    }

    static int[] ReadIndices(ref Bytes bytes, ref int last)
    {
        var indices = new int[bytes.Count()];
        for (var at = 0; at < indices.Length; at++) indices[at] = last += bytes.Signed();
        return indices;
    }

    static WaterArrays ReadWaters(ref Bytes bytes)
    {
        var (count, points) = (bytes.Count(), bytes.Count());
        var (kind, pointOffsets, pointM) = (new WaterBody[count], new int[count + 1], new Vector2[points]);
        var (point, x, y) = (0, 0, 0);
        for (var water = 0; water < count; water++)
        {
            kind[water] = (WaterBody)bytes.Byte();
            var itsPoints = bytes.Count();
            for (var at = 0; at < itsPoints; at++) pointM[point++] = Place(ref bytes, ref x, ref y, StepsPerM);

            pointOffsets[water + 1] = point;
        }

        Tally("waters", count, points, count, point);
        return new WaterArrays { Kind = kind, PointOffsets = pointOffsets, PointM = pointM };
    }

    static CourseArrays ReadCourses(ref Bytes bytes)
    {
        var (count, points) = (bytes.Count(), bytes.Count());
        var (kind, across, nearM, farM) = (new WaterBody[count], new Vector2[count], new float[count], new float[count]);
        var (pointOffsets, pointM) = (new int[count + 1], new Vector2[points]);
        var point = 0;
        for (var water = 0; water < count; water++)
        {
            kind[water] = (WaterBody)bytes.Byte();
            (across[water], nearM[water], farM[water]) = (new Vector2(bytes.Single(), bytes.Single()), bytes.Single(), bytes.Single());
            for (var at = bytes.Count(); at > 0; at--) pointM[point++] = new Vector2(bytes.Single(), bytes.Single());

            pointOffsets[water + 1] = point;
        }

        Tally("courses", count, points, count, point);
        return new CourseArrays { Kind = kind, Across = across, NearM = nearM, FarM = farM, PointOffsets = pointOffsets, PointM = pointM };
    }

    static StoodArrays ReadStood(ref Bytes bytes)
    {
        var count = bytes.Count();
        var (look, centreM, sizeM, headingRad) = (new BuildingLook[count], new Vector2[count], new Vector2[count], new float[count]);
        var (x, y) = (0, 0);
        for (var building = 0; building < count; building++)
        {
            look[building] = (BuildingLook)bytes.Byte();
            centreM[building] = Place(ref bytes, ref x, ref y, StandStepsPerM);
            sizeM[building] = new Vector2(bytes.Count(), bytes.Count()) / (float)StandStepsPerM;
            headingRad[building] = Turned(bytes.UInt16());
        }

        return new StoodArrays { Look = look, CentreM = centreM, SizeM = sizeM, HeadingRad = headingRad };
    }

    static PropArrays ReadProps(ref Bytes bytes)
    {
        var count = bytes.Count();
        var (kind, centreM, bearingRad) = (new PropKind[count], new Vector2[count], new float[count]);
        var (x, y) = (0, 0);
        for (var prop = 0; prop < count; prop++)
        {
            kind[prop] = (PropKind)bytes.Byte();
            centreM[prop] = Place(ref bytes, ref x, ref y, StandStepsPerM);
            bearingRad[prop] = Turned(bytes.UInt16());
        }

        return new PropArrays { Kind = kind, CentreM = centreM, BearingRad = bearingRad };
    }

    static LotArrays ReadLots(ref Bytes bytes)
    {
        var count = bytes.Count();
        var (centreM, sizeM, headingRad) = (new Vector2[count], new Vector2[count], new float[count]);
        var (x, y) = (0, 0);
        for (var lot = 0; lot < count; lot++)
        {
            centreM[lot] = Place(ref bytes, ref x, ref y, StandStepsPerM);
            sizeM[lot] = new Vector2(bytes.Count(), bytes.Count()) / (float)StandStepsPerM;
            headingRad[lot] = Turned(bytes.UInt16());
        }

        return new LotArrays { CentreM = centreM, SizeM = sizeM, HeadingRad = headingRad };
    }

    static ZoneArrays ReadZones(ref Bytes bytes)
    {
        var (count, rings, points, settings) = (bytes.Count(), bytes.Count(), bytes.Count(), bytes.Count());
        var (parent, kind) = (new int[count], new ZoneKind[count]);
        var (paramOffsets, paramKey, paramValue) = (new int[count + 1], new ZoneParam[settings], new float[settings]);
        var (ringOffsets, pointOffsets, pointM) = (new int[count + 1], new int[rings + 1], new Vector2[points]);
        var (ring, point, setting, x, y) = (0, 0, 0, 0, 0);
        for (var zone = 0; zone < count; zone++)
        {
            parent[zone] = bytes.Count() - 1;
            kind[zone] = (ZoneKind)bytes.Byte();
            var its = bytes.Count();
            for (var one = 0; one < its; one++)
            {
                var param = (ZoneParam)bytes.Byte();
                paramKey[setting] = param;
                paramValue[setting++] = ZoneParams.Exact(param) ? bytes.Single() : (float)(bytes.Signed64() * ZoneParams.StepOf(param));
            }

            paramOffsets[zone + 1] = setting;
            ReadRings(ref bytes, pointOffsets, pointM, ref ring, ref point, ref x, ref y, ZoneStepsPerM);
            ringOffsets[zone + 1] = ring;
        }

        Tally("zones", rings, points, ring, point);
        if (setting != settings) throw new InvalidDataException($"zones of {settings} settings read as {setting}.");

        return new ZoneArrays
        {
            Parent = parent, Kind = kind, ParamOffsets = paramOffsets, ParamKey = paramKey, ParamValue = paramValue,
            RingOffsets = ringOffsets, PointOffsets = pointOffsets, PointM = pointM,
        };
    }

    static void ReadRings(ref Bytes bytes, int[] pointOffsets, Vector2[] pointM, ref int ring, ref int point, ref int x, ref int y, double stepsPerM)
    {
        var itsRings = bytes.Count();
        for (var one = 0; one < itsRings; one++)
        {
            var itsPoints = bytes.Count();
            for (var at = 0; at < itsPoints; at++) pointM[point++] = Place(ref bytes, ref x, ref y, stepsPerM);

            pointOffsets[++ring] = point;
        }
    }

    static void Tally(string what, int rings, int points, int ring, int point)
    {
        if (ring != rings || point != points) throw new InvalidDataException($"{what} of {rings} rings and {points} points read as {ring} and {point}.");
    }

    static Vector2 Place(ref Bytes bytes, ref int x, ref int y, double stepsPerM)
    {
        x += bytes.Signed();
        y += bytes.Signed();
        return new Vector2((float)(x / stepsPerM), (float)(y / stepsPerM));
    }

    static float Turned(ushort steps) => (float)(steps * Math.Tau / TurnSteps);

    /// <summary>The map into a file, whole or not at all: written beside it and moved over it once complete.</summary>
    public void Write(string path)
    {
        Check(path);
        var partial = path + ".part";
        using (var file = File.Create(partial))
        using (var buffered = new BufferedStream(file, 1 << 16))
        {
            Write(buffered);
        }

        File.Move(partial, path, overwrite: true);
    }

    public void Write(Stream into)
    {
        using var writer = new BinaryWriter(into, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(Name);
        writer.Write(Description);
        writer.Write(Licence);
        writer.Write(OsmBase);
        writer.Write7BitEncodedInt64(unchecked((long)Seed));
        foreach (var figure in (ReadOnlySpan<double>)[Frame.Lat0Deg, Frame.Lon0Deg, Frame.WestM, Frame.SouthM, Frame.WidthM, Frame.HeightM, Frame.MarginM])
        {
            writer.Write(figure);
        }

        writer.Write7BitEncodedInt(PointM.Length);
        var (x, y) = (0, 0);
        foreach (var pointM in PointM) WritePlace(writer, pointM.X, pointM.Y, ref x, ref y, StepsPerM);

        var classes = new List<string>();
        var classOf = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var road in Roads)
        {
            if (classOf.TryAdd(road.Highway, classes.Count)) classes.Add(road.Highway);
        }

        writer.Write7BitEncodedInt(classes.Count);
        foreach (var highway in classes) writer.Write(highway);

        writer.Write7BitEncodedInt(Roads.Length);
        var (id, last) = (0L, 0);
        foreach (var road in Roads)
        {
            WriteSigned(writer, road.OsmId - id);
            id = road.OsmId;
            WriteRoad(writer, road, classOf[road.Highway], ref last);
        }

        writer.Write7BitEncodedInt(Coast.Length);
        foreach (var line in Coast) WriteIndices(writer, line, ref last);

        WriteWaters(writer, Waters);
        WriteCourses(writer, Courses);
        WriteStood(writer, Buildings);
        WriteProps(writer, Props);
        WriteLots(writer, Lots);
        WriteZones(writer, Zones);
    }

    static void WriteRoad(BinaryWriter writer, TracedRoad road, int highway, ref int last)
    {
        var offsetSteps = Steps(road.CentreOffsetShare, OffsetSteps);
        var flags = (road.Bridge ? RoadFlags.Bridge : 0) | (road.Roundabout ? RoadFlags.Roundabout : 0) | (offsetSteps != 0 ? RoadFlags.Offset : 0)
                    | (road.WidthM is not null ? RoadFlags.Width : 0) | (road.LanesTagged ? RoadFlags.LanesTagged : 0) | (road.Marked ? RoadFlags.Marked : 0)
                    | (road.LanesShared << RoadFlags.SharedShift);
        writer.Write7BitEncodedInt(highway);
        writer.Write((byte)flags);
        writer.Write((byte)(road.LanesForward | (road.LanesBackward << 4)));
        if (offsetSteps != 0) WriteSigned(writer, offsetSteps);
        if (road.WidthM is { } widthM) writer.Write7BitEncodedInt(Steps(widthM, StepsPerM));
        WriteIndices(writer, road.Points, ref last);
    }

    static void WriteIndices(BinaryWriter writer, int[] indices, ref int last)
    {
        writer.Write7BitEncodedInt(indices.Length);
        foreach (var index in indices)
        {
            WriteSigned(writer, index - last);
            last = index;
        }
    }

    static void WriteWaters(BinaryWriter writer, WaterArrays waters)
    {
        writer.Write7BitEncodedInt(waters.Count);
        writer.Write7BitEncodedInt(waters.PointM.Length);
        var (x, y) = (0, 0);
        for (var water = 0; water < waters.Count; water++)
        {
            writer.Write((byte)waters.Kind[water]);
            var outline = waters.OutlineOf(water);
            writer.Write7BitEncodedInt(outline.Length);
            foreach (var pointM in outline) WritePlace(writer, pointM.X, pointM.Y, ref x, ref y, StepsPerM);
        }
    }

    static void WriteCourses(BinaryWriter writer, CourseArrays courses)
    {
        writer.Write7BitEncodedInt(courses.Count);
        writer.Write7BitEncodedInt(courses.PointM.Length);
        for (var water = 0; water < courses.Count; water++)
        {
            writer.Write((byte)courses.Kind[water]);
            foreach (var figure in (ReadOnlySpan<float>)[courses.Across[water].X, courses.Across[water].Y, courses.NearM[water], courses.FarM[water]]) writer.Write(figure);

            var course = courses.CourseOf(water);
            writer.Write7BitEncodedInt(course.Length);
            foreach (var pointM in course)
            {
                writer.Write(pointM.X);
                writer.Write(pointM.Y);
            }
        }
    }

    static void WriteStood(BinaryWriter writer, StoodArrays stood)
    {
        writer.Write7BitEncodedInt(stood.Count);
        var (x, y) = (0, 0);
        for (var building = 0; building < stood.Count; building++)
        {
            writer.Write((byte)stood.Look[building]);
            WritePlace(writer, stood.CentreM[building].X, stood.CentreM[building].Y, ref x, ref y, StandStepsPerM);
            writer.Write7BitEncodedInt(Steps(stood.SizeM[building].X, StandStepsPerM));
            writer.Write7BitEncodedInt(Steps(stood.SizeM[building].Y, StandStepsPerM));
            writer.Write(Turn(stood.HeadingRad[building]));
        }
    }

    static void WriteProps(BinaryWriter writer, PropArrays props)
    {
        writer.Write7BitEncodedInt(props.Count);
        var (x, y) = (0, 0);
        for (var prop = 0; prop < props.Count; prop++)
        {
            writer.Write((byte)props.Kind[prop]);
            WritePlace(writer, props.CentreM[prop].X, props.CentreM[prop].Y, ref x, ref y, StandStepsPerM);
            writer.Write(Turn(props.BearingRad[prop]));
        }
    }

    static void WriteLots(BinaryWriter writer, LotArrays lots)
    {
        writer.Write7BitEncodedInt(lots.Count);
        var (x, y) = (0, 0);
        for (var lot = 0; lot < lots.Count; lot++)
        {
            WritePlace(writer, lots.CentreM[lot].X, lots.CentreM[lot].Y, ref x, ref y, StandStepsPerM);
            writer.Write7BitEncodedInt(Steps(lots.SizeM[lot].X, StandStepsPerM));
            writer.Write7BitEncodedInt(Steps(lots.SizeM[lot].Y, StandStepsPerM));
            writer.Write(Turn(lots.HeadingRad[lot]));
        }
    }

    static void WriteZones(BinaryWriter writer, ZoneArrays zones)
    {
        writer.Write7BitEncodedInt(zones.Count);
        writer.Write7BitEncodedInt(zones.PointOffsets.Length - 1);
        writer.Write7BitEncodedInt(zones.PointM.Length);
        writer.Write7BitEncodedInt(zones.ParamKey.Length);
        var (x, y) = (0, 0);
        for (var zone = 0; zone < zones.Count; zone++)
        {
            writer.Write7BitEncodedInt(zones.Parent[zone] + 1);
            writer.Write((byte)zones.Kind[zone]);
            var (first, past) = (zones.ParamOffsets[zone], zones.ParamOffsets[zone + 1]);
            writer.Write7BitEncodedInt(past - first);
            for (var at = first; at < past; at++)
            {
                writer.Write((byte)zones.ParamKey[at]);
                if (ZoneParams.Exact(zones.ParamKey[at])) writer.Write(zones.ParamValue[at]);
                else WriteSigned(writer, ZoneParams.Steps(zones.ParamKey[at], zones.ParamValue[at]));
            }

            WriteRings(writer, zones.RingOffsets[zone], zones.RingOffsets[zone + 1], zones.RingOf, ref x, ref y, ZoneStepsPerM);
        }
    }

    delegate ReadOnlySpan<Vector2> RingAt(int ring);

    static void WriteRings(BinaryWriter writer, int first, int past, RingAt ringOf, ref int x, ref int y, double stepsPerM)
    {
        writer.Write7BitEncodedInt(past - first);
        for (var ring = first; ring < past; ring++)
        {
            var points = ringOf(ring);
            writer.Write7BitEncodedInt(points.Length);
            foreach (var pointM in points) WritePlace(writer, pointM.X, pointM.Y, ref x, ref y, stepsPerM);
        }
    }

    static void WritePlace(BinaryWriter writer, double xM, double yM, ref int x, ref int y, double stepsPerM)
    {
        var (stepX, stepY) = (Steps(xM, stepsPerM), Steps(yM, stepsPerM));
        WriteSigned(writer, stepX - x);
        WriteSigned(writer, stepY - y);
        (x, y) = (stepX, stepY);
    }

    static ushort Turn(float headingRad) => (ushort)((long)Math.Round(headingRad * TurnSteps / Math.Tau) & 0xFFFF);

    static int Steps(double valueM, double stepsPerM) => checked((int)Math.Round(valueM * stepsPerM));

    static void WriteSigned(BinaryWriter writer, int value) => writer.Write7BitEncodedInt((value << 1) ^ (value >> 31));

    static void WriteSigned(BinaryWriter writer, long value) => writer.Write7BitEncodedInt64((value << 1) ^ (value >> 63));

    /// <summary>A road's first byte: its level and shape, what follows the lanes byte, and how many lanes both ways share.</summary>
    static class RoadFlags
    {
        public const int Bridge = 1;
        public const int Roundabout = 2;

        /// <summary>Its carriageway's middle stands off its line, by the signed share that follows.</summary>
        public const int Offset = 4;

        /// <summary>Its carriageway was measured, as wide as the count that follows.</summary>
        public const int Width = 8;

        public const int LanesTagged = 16;
        public const int Marked = 32;
        public const int SharedShift = 6;
    }

    /// <summary>
    /// <b>Reads a map's bytes front to back</b>, each read taking what it needs off the front; one past the end throws,
    /// which <see cref="Read(ReadOnlySpan{byte}, string)"/> reports as a map cut short.
    /// </summary>
    ref struct Bytes(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> _left = bytes;

        public readonly int Left => _left.Length;

        public byte Byte()
        {
            var value = _left[0];
            _left = _left[1..];
            return value;
        }

        public ushort UInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));

        public uint UInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));

        public double Double() => BinaryPrimitives.ReadDoubleLittleEndian(Take(8));

        public float Single() => BinaryPrimitives.ReadSingleLittleEndian(Take(4));

        public string String() => Encoding.UTF8.GetString(Take(Count()));

        public int Count() => checked((int)Varint());

        public int Signed()
        {
            var value = (uint)Varint();
            return (int)(value >> 1) ^ -(int)(value & 1);
        }

        public long Signed64()
        {
            var value = Varint();
            return (long)(value >> 1) ^ -(long)(value & 1);
        }

        public ulong Varint()
        {
            var (value, shift) = (0UL, 0);
            while (true)
            {
                var part = Byte();
                value |= (ulong)(part & 0x7F) << shift;
                if (part < 0x80) return value;

                shift += 7;
                if (shift > 63) throw new InvalidDataException("a varint longer than ten bytes.");
            }
        }

        ReadOnlySpan<byte> Take(int count)
        {
            var taken = _left[..count];
            _left = _left[count..];
            return taken;
        }
    }
}
