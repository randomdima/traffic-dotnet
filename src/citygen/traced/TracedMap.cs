using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace TrafficSimulation.CityGen.Traced;

/// <summary>Where a road's measured width was read.</summary>
internal enum MeasuredFrom : byte
{
    /// <summary>Its <c>width</c> tag, the mapper's own word.</summary>
    Tag,

    /// <summary>The carriageway surface OSM outlines it with (<c>area:highway</c>).</summary>
    Surface,

    /// <summary>The paved surface a model read off aerial imagery, kerb to kerb, parking and gutters in it.</summary>
    Imagery,
}

/// <summary>What controls a junction, as the enrichment read it off what is mapped and seen at and before it.</summary>
internal enum SurveyControl : byte
{
    /// <summary>Nothing mapped or seen: the traffic rules decide who goes.</summary>
    Unsigned,
    Signals,

    /// <summary>Signals mapped as flashing amber, which control nothing.</summary>
    Blinking,
    Roundabout,

    /// <summary>A stop or give-way sign on one of its arms.</summary>
    Signs,

    /// <summary>A priority road through it, signed as one.</summary>
    PriorityRoad,
}

/// <summary>A pedestrian crossing's kind, as its tags say.</summary>
internal enum SurveyCrossingKind : byte
{
    /// <summary>Marked on the road, which in Ukraine is a zebra (marking 1.14.1).</summary>
    Zebra,

    /// <summary>Controlled by lights for the walkers.</summary>
    Signals,
    Unmarked,

    /// <summary>A crossing whose kind is not tagged.</summary>
    Unknown,
}

/// <summary>
/// <b>What a building is for</b>, as OSM's tags on it say — its <c>building</c> value, else its <c>amenity</c> or
/// <c>shop</c> — and, where they say nothing, as the land use it stands in does.
/// </summary>
/// <remarks>
/// Facts and no rule: what a town makes of them — which building is a house and which a block of flats — is
/// <see cref="TracedBuildings"/>'s.
/// </remarks>
internal enum FootprintUse : byte
{
    /// <summary>Nothing says: <c>building=yes</c> or a machine-traced outline, standing in no land use that tells.</summary>
    Unknown,

    /// <summary>A home whose tags or land use say no more than that — a house or a block of flats.</summary>
    Residential,

    House,
    Apartments,

    /// <summary>A lock-up or a row of them, a carport, or anything standing in a garage cooperative's ground.</summary>
    Garages,

    /// <summary>An outbuilding: a shed, a hut, a guardhouse, a service building.</summary>
    Shed,

    /// <summary>A kiosk, or anything standing in a market.</summary>
    Kiosk,

    Retail,

    /// <summary>Offices, civic and public buildings, hotels and stations.</summary>
    Office,

    /// <summary>Industry, warehouses, hangars and silos, or anything standing in industrial or port ground.</summary>
    Industrial,

    /// <summary>Schools, kindergartens, colleges and universities.</summary>
    School,
    Hospital,
    Religious,

    /// <summary>A roof on posts with no walls (<c>building=roof</c>).</summary>
    Canopy,
    Greenhouse,
}

/// <summary>
/// <b>A traced map's own file, and the whole of what the engine reads of a real place</b> (GEN-57,
/// <c>towns/traced/&lt;Map&gt;.map</c>): every road way's line and its carriageway as OSM means it, the coastline,
/// the turns OSM forbids, and what else is known of the place — each junction's control, every pedestrian
/// crossing, every building's footprint and every tree — in the map's own metres.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the map, and an edit is made to it</b>: imported once off the scanner's extract and the enrichment's
/// layers (<see cref="TracedMapImport"/>, <c>qq osm --import</c>), which stay as OSM and the other sources gave them,
/// and changed in place after that (<see cref="Cropped"/>, <c>qq osm --crop</c>; <see cref="Without"/>,
/// <c>qq osm --drop-stumps</c>). A second import replaces it and
/// every edit made since.
/// </para>
/// <para>
/// <b>Facts and no rule of the engine's</b>: a lane's direction, width and arrows, a width as measured and where, a
/// control as the enrichment read it. Which of them a town takes, and how, is <see cref="Survey.Of"/>'s and
/// <see cref="TracedPlan"/>'s, so retuning a rule relays the town off the same file. Nothing it holds is kept for
/// a tool alone: no tag, no node id, no surface OSM outlines a road with — the extract keeps those.
/// </para>
/// <para>
/// <b>Read in one pass over bytes already in memory</b> (<see cref="Read(ReadOnlySpan{byte}, string)"/>). Places are
/// whole millimetres, x east and y south off the frame's north-west corner, each written as its step from the place
/// before; a footprint's are centimetres, which is what keeps a city's buildings a few megabytes. Counts, indices and
/// ids are 7-bit varints, a signed one zigzagged; anything else little-endian, as <see cref="BinaryWriter"/> writes.
/// The name and the description come first, so the head the menu reads holds them (<see cref="SurveyHead"/>).
/// </para>
/// </remarks>
internal sealed partial record TracedMap
{
    /// <summary>"TMAP", first in the file.</summary>
    const uint Magic = 0x50414D54;

    /// <summary>Bumped whenever what is written changes, so a file of the old shape is refused rather than misread.</summary>
    public const ushort Version = 3;

    /// <summary>
    /// The one older shape still read: no footprint carries its use, which reads as <see cref="FootprintUse.Unknown"/>
    /// until <c>qq osm --footprints</c> lays them again off the layers.
    /// </summary>
    const ushort Useless = 2;

    const double StepsPerM = 1000.0;

    const double FootprintStepsPerM = 100.0;

    /// <summary>A crossing's junction where it is struck mid-block.</summary>
    public const int NoJunction = -1;

    public required string Name { get; init; }

    /// <summary>The line the start menu prints under the name.</summary>
    public required string Description { get; init; }

    public required string Licence { get; init; }

    /// <summary>The moment the OSM database it was imported from stood at.</summary>
    public required string OsmBase { get; init; }

    /// <summary>The OSM boundary relation whose roads drew the map, which is the map's own number.</summary>
    public required long Relation { get; init; }

    public required OsmFrame Frame { get; init; }

    /// <summary>
    /// Every point a road or the coast passes. <b>Two ways sharing a point share an index</b>, which is how OSM says
    /// they meet; two that merely cross are not joined.
    /// </summary>
    public required Vector2D[] PointM { get; init; }

    /// <summary>Every road way, in OSM id order.</summary>
    public required TracedRoad[] Roads { get; init; }

    /// <summary>Every coastline way as indices into <see cref="PointM"/>, land on its left as OSM draws it.</summary>
    public required int[][] Coast { get; init; }

    /// <summary>What OSM forbids a car and which lanes it joins (<see cref="OsmTurns"/>), a node by its point's index.</summary>
    public required OsmTurns Turns { get; init; }

    /// <summary>Each controlled junction, by its point; a junction the rules decide is not here.</summary>
    public required ControlArrays Controls { get; init; }

    public required CrossingArrays Crossings { get; init; }

    public required FootprintArrays Footprints { get; init; }

    /// <summary>Every tree OSM maps a point for (<c>natural=tree</c>).</summary>
    public required Vector2[] TreeM { get; init; }

    internal sealed class ControlArrays
    {
        public required int[] Point { get; init; }

        public required SurveyControl[] Control { get; init; }

        /// <summary>
        /// The near junctions it is controlled with as one — a dual carriageway's crossing is four nodes and one set of
        /// lights — named by one of their nodes' OSM ids, the same for each; nought where it stands alone.
        /// </summary>
        public required long[] Cluster { get; init; }

        public int Count => Point.Length;

        public static ControlArrays None => new() { Point = [], Control = [], Cluster = [] };
    }

    internal sealed class CrossingArrays
    {
        /// <summary>The road way it crosses, by its OSM id.</summary>
        public required long[] Way { get; init; }

        public required Vector2[] AtM { get; init; }

        public required SurveyCrossingKind[] Kind { get; init; }

        /// <summary>Whether its tags say it is painted on the road, or null where they say nothing of it.</summary>
        public required bool?[] Painted { get; init; }

        /// <summary>The point of the junction whose arm it is on, or <see cref="NoJunction"/> where it is struck mid-block.</summary>
        public required int[] Junction { get; init; }

        public int Count => Way.Length;

        public static CrossingArrays None => new() { Way = [], AtM = [], Kind = [], Painted = [], Junction = [] };
    }

    /// <summary>
    /// Every building's footprint as its rings, flat with offsets beside them: the outline first, then any courtyard
    /// cut out of it, none closed on its first point — a ring is closed by being one.
    /// </summary>
    internal sealed class FootprintArrays
    {
        /// <summary>Count + 1 entries, over the rings: footprint i's are <c>RingOffsets[i]..RingOffsets[i + 1]</c>.</summary>
        public required int[] RingOffsets { get; init; }

        /// <summary>One entry a ring + 1, over <see cref="PointM"/>.</summary>
        public required int[] PointOffsets { get; init; }

        public required Vector2[] PointM { get; init; }

        /// <summary>Whether it was traced off imagery by a machine rather than mapped.</summary>
        public required bool[] Traced { get; init; }

        /// <summary>How tall it stands, as OSM tags it or its levels make it, or nought where nothing says.</summary>
        public required float[] HeightM { get; init; }

        public required FootprintUse[] Use { get; init; }

        public int Count => Traced.Length;

        public ReadOnlySpan<Vector2> RingOf(int ring) =>
            PointM.AsSpan(PointOffsets[ring], PointOffsets[ring + 1] - PointOffsets[ring]);

        public static FootprintArrays None => new() { RingOffsets = [0], PointOffsets = [0], PointM = [], Traced = [], HeightM = [], Use = [] };
    }

    /// <summary>Refuses a map that cannot describe a place, at the point it is read or before it is written.</summary>
    public void Check(string what)
    {
        if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException($"{what}: a map with no name.");
        if (!(Frame.MarginM >= 0) || !(Frame.WidthM > 2 * Frame.MarginM) || !(Frame.HeightM > 2 * Frame.MarginM))
        {
            throw new InvalidDataException($"{what}: a frame of {Frame.WidthM} by {Frame.HeightM} m with a margin of {Frame.MarginM} m.");
        }

        var points = PointM.Length;
        foreach (var road in Roads)
        {
            if (road.Points.Length < 2) throw new InvalidDataException($"{what}: road way {road.OsmId} of {road.Points.Length} points.");
            if (road.Carriageway.Lanes.Length == 0) throw new InvalidDataException($"{what}: road way {road.OsmId} has no lanes.");
            if (road.Arrowed && road.Carriageway.Lanes.Length > ushort.MaxValue) throw new InvalidDataException($"{what}: road way {road.OsmId} of {road.Carriageway.Lanes.Length} lanes.");
            Within(road.Points, $"road way {road.OsmId}");
        }

        foreach (var line in Coast)
        {
            if (line.Length < 2) throw new InvalidDataException($"{what}: a coastline of {line.Length} points.");
            Within(line, "a coastline");
        }

        foreach (var turn in Turns.Restrictions) Within([turn.Via], $"restriction {turn.Relation}");
        foreach (var link in Turns.LaneLinks) Within([link.Via], $"lane link {link.Relation}");
        Within(Controls.Point, "a control");
        foreach (var junction in Crossings.Junction)
        {
            if (junction != NoJunction) Within([junction], "a crossing");
        }

        void Within(ReadOnlySpan<int> indices, string which)
        {
            foreach (var point in indices)
            {
                if ((uint)point >= (uint)points) throw new InvalidDataException($"{what}: {which} passes point {point} of {points}.");
            }
        }
    }

    public static TracedMap Read(string path) => Read(File.ReadAllBytes(path), path);

    public static TracedMap Read(ReadOnlySpan<byte> file, string what)
    {
        TracedMap map;
        try
        {
            var bytes = new Bytes(file);
            if (bytes.UInt32() != Magic) throw new InvalidDataException($"{what}: not a traced map.");
            if (bytes.UInt16() is var version && version != Version && version != Useless)
            {
                throw new InvalidDataException($"{what}: a map of version {version}, and this build reads {Version}.");
            }

            map = ReadBody(ref bytes, version);
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
    /// one until it is opened (<see cref="SurveyHead"/>).
    /// </summary>
    public static (string Name, string Description) Head(ReadOnlySpan<byte> head, string what)
    {
        try
        {
            var bytes = new Bytes(head);
            if (bytes.UInt32() != Magic || bytes.UInt16() is not (Version or Useless)) throw new InvalidDataException($"{what}: not a traced map of version {Version}.");

            return (bytes.String(), bytes.String());
        }
        catch (Exception cut) when (cut is ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            throw new InvalidDataException($"{what}: no description in its first {head.Length} bytes, where a map's head holds it.");
        }
    }

    static TracedMap ReadBody(ref Bytes bytes, ushort version)
    {
        var (name, description, licence, osmBase) = (bytes.String(), bytes.String(), bytes.String(), bytes.String());
        var relation = bytes.Signed64();
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

        return new TracedMap
        {
            Name = name, Description = description, Licence = licence, OsmBase = osmBase, Relation = relation, Frame = frame,
            PointM = pointM, Roads = roads, Coast = coast,
            Turns = ReadTurns(ref bytes),
            Controls = ReadControls(ref bytes),
            Crossings = ReadCrossings(ref bytes),
            Footprints = ReadFootprints(ref bytes, version != Useless),
            TreeM = ReadPlaces(ref bytes),
        };
    }

    static TracedRoad ReadRoad(ref Bytes bytes, long id, string[] classes, ref int last)
    {
        var highway = classes[bytes.Count()];
        var flags = (RoadFlags)bytes.Byte();
        var widthFrom = (OsmWidthFrom)bytes.Byte();
        var centreOffsetM = bytes.Signed() / (float)StepsPerM;
        var lanes = new OsmLane[bytes.Count()];
        for (var lane = 0; lane < lanes.Length; lane++)
        {
            var way = (OsmLaneWay)bytes.Byte();
            var widthM = bytes.Count() / (float)StepsPerM;
            var arrows = flags.HasFlag(RoadFlags.Arrowed) ? (OsmArrows)bytes.Count() : OsmArrows.None;
            lanes[lane] = new OsmLane { Way = way, WidthM = widthM, Arrows = arrows };
        }

        (float WidthM, MeasuredFrom From)? measured = flags.HasFlag(RoadFlags.Measured)
            ? (bytes.Count() / (float)StepsPerM, (MeasuredFrom)bytes.Byte())
            : null;

        return new TracedRoad
        {
            OsmId = id,
            Highway = highway,
            Bridge = flags.HasFlag(RoadFlags.Bridge),
            Tunnel = flags.HasFlag(RoadFlags.Tunnel),
            Roundabout = flags.HasFlag(RoadFlags.Roundabout),
            Marked = flags.HasFlag(RoadFlags.Marked),
            Carriageway = new OsmCarriageway
            {
                Lanes = lanes, CentreOffsetM = centreOffsetM, WidthFrom = widthFrom,
                LanesFrom = flags.HasFlag(RoadFlags.LanesTagged) ? OsmLanesFrom.Tagged : OsmLanesFrom.Assumed,
            },
            Measured = measured,
            Points = ReadIndices(ref bytes, ref last),
        };
    }

    static int[] ReadIndices(ref Bytes bytes, ref int last)
    {
        var indices = new int[bytes.Count()];
        for (var at = 0; at < indices.Length; at++) indices[at] = last += bytes.Signed();
        return indices;
    }

    static OsmTurns ReadTurns(ref Bytes bytes)
    {
        var restrictions = new OsmTurnRestriction[bytes.Count()];
        for (var at = 0; at < restrictions.Length; at++)
        {
            restrictions[at] = new OsmTurnRestriction
            {
                Relation = bytes.Signed64(), From = bytes.Signed64(), Via = bytes.Count(), To = bytes.Signed64(), Only = bytes.Byte() != 0,
            };
        }

        var links = new OsmLaneLink[bytes.Count()];
        for (var at = 0; at < links.Length; at++)
        {
            links[at] = new OsmLaneLink
            {
                Relation = bytes.Signed64(), From = bytes.Signed64(), Via = bytes.Count(), To = bytes.Signed64(),
                FromLane = bytes.Count(), ToLane = bytes.Count(),
            };
        }

        return new OsmTurns { Restrictions = restrictions, LaneLinks = links };
    }

    static ControlArrays ReadControls(ref Bytes bytes)
    {
        var count = bytes.Count();
        var (point, control, cluster) = (new int[count], new SurveyControl[count], new long[count]);
        for (var at = 0; at < count; at++) (point[at], control[at], cluster[at]) = (bytes.Count(), (SurveyControl)bytes.Byte(), bytes.Signed64());
        return new ControlArrays { Point = point, Control = control, Cluster = cluster };
    }

    static CrossingArrays ReadCrossings(ref Bytes bytes)
    {
        var count = bytes.Count();
        var (way, atM, kind, painted, junction) = (new long[count], new Vector2[count], new SurveyCrossingKind[count], new bool?[count], new int[count]);
        var (x, y) = (0, 0);
        for (var at = 0; at < count; at++)
        {
            way[at] = bytes.Signed64();
            atM[at] = Place(ref bytes, ref x, ref y, StepsPerM);
            kind[at] = (SurveyCrossingKind)bytes.Byte();
            painted[at] = bytes.Byte() switch { 0 => false, 1 => true, _ => null };
            junction[at] = bytes.Count() - 1;
        }

        return new CrossingArrays { Way = way, AtM = atM, Kind = kind, Painted = painted, Junction = junction };
    }

    static FootprintArrays ReadFootprints(ref Bytes bytes, bool withUses)
    {
        var (count, rings, points) = (bytes.Count(), bytes.Count(), bytes.Count());
        var ringOffsets = new int[count + 1];
        var pointOffsets = new int[rings + 1];
        var pointM = new Vector2[points];
        var traced = new bool[count];
        var heightM = new float[count];
        var use = new FootprintUse[count];
        var (ring, point, x, y) = (0, 0, 0, 0);
        for (var footprint = 0; footprint < count; footprint++)
        {
            traced[footprint] = bytes.Byte() != 0;
            heightM[footprint] = bytes.Count() / (float)FootprintStepsPerM;
            if (withUses) use[footprint] = (FootprintUse)bytes.Byte();
            var itsRings = bytes.Count();
            for (var one = 0; one < itsRings; one++)
            {
                var itsPoints = bytes.Count();
                for (var at = 0; at < itsPoints; at++) pointM[point++] = Place(ref bytes, ref x, ref y, FootprintStepsPerM);

                pointOffsets[++ring] = point;
            }

            ringOffsets[footprint + 1] = ring;
        }

        if (ring != rings || point != points) throw new InvalidDataException($"footprints of {rings} rings and {points} points read as {ring} and {point}.");

        return new FootprintArrays { RingOffsets = ringOffsets, PointOffsets = pointOffsets, PointM = pointM, Traced = traced, HeightM = heightM, Use = use };
    }

    static Vector2[] ReadPlaces(ref Bytes bytes)
    {
        var placeM = new Vector2[bytes.Count()];
        var (x, y) = (0, 0);
        for (var at = 0; at < placeM.Length; at++) placeM[at] = Place(ref bytes, ref x, ref y, StepsPerM);
        return placeM;
    }

    static Vector2 Place(ref Bytes bytes, ref int x, ref int y, double stepsPerM)
    {
        x += bytes.Signed();
        y += bytes.Signed();
        return new Vector2((float)(x / stepsPerM), (float)(y / stepsPerM));
    }

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
        WriteSigned(writer, Relation);
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

        WriteTurns(writer, Turns);
        WriteControls(writer, Controls);
        WriteCrossings(writer, Crossings);
        WriteFootprints(writer, Footprints);
        WritePlaces(writer, TreeM);
    }

    static void WriteRoad(BinaryWriter writer, TracedRoad road, int highway, ref int last)
    {
        var carriageway = road.Carriageway;
        var flags = (road.Bridge ? RoadFlags.Bridge : 0) | (road.Tunnel ? RoadFlags.Tunnel : 0) | (road.Roundabout ? RoadFlags.Roundabout : 0)
                    | (road.Marked ? RoadFlags.Marked : 0) | (carriageway.LanesFrom == OsmLanesFrom.Tagged ? RoadFlags.LanesTagged : 0)
                    | (road.Measured is not null ? RoadFlags.Measured : 0) | (road.Arrowed ? RoadFlags.Arrowed : 0);
        writer.Write7BitEncodedInt(highway);
        writer.Write((byte)flags);
        writer.Write((byte)carriageway.WidthFrom);
        WriteSigned(writer, Steps(carriageway.CentreOffsetM, StepsPerM));
        writer.Write7BitEncodedInt(carriageway.Lanes.Length);
        foreach (var lane in carriageway.Lanes)
        {
            writer.Write((byte)lane.Way);
            writer.Write7BitEncodedInt(Steps(lane.WidthM, StepsPerM));
            if (flags.HasFlag(RoadFlags.Arrowed)) writer.Write7BitEncodedInt((int)lane.Arrows);
        }

        if (road.Measured is { } measured)
        {
            writer.Write7BitEncodedInt(Steps(measured.WidthM, StepsPerM));
            writer.Write((byte)measured.From);
        }

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

    static void WriteTurns(BinaryWriter writer, OsmTurns turns)
    {
        writer.Write7BitEncodedInt(turns.Restrictions.Length);
        foreach (var turn in turns.Restrictions)
        {
            WriteSigned(writer, turn.Relation);
            WriteSigned(writer, turn.From);
            writer.Write7BitEncodedInt(turn.Via);
            WriteSigned(writer, turn.To);
            writer.Write(turn.Only);
        }

        writer.Write7BitEncodedInt(turns.LaneLinks.Length);
        foreach (var link in turns.LaneLinks)
        {
            WriteSigned(writer, link.Relation);
            WriteSigned(writer, link.From);
            writer.Write7BitEncodedInt(link.Via);
            WriteSigned(writer, link.To);
            writer.Write7BitEncodedInt(link.FromLane);
            writer.Write7BitEncodedInt(link.ToLane);
        }
    }

    static void WriteControls(BinaryWriter writer, ControlArrays controls)
    {
        writer.Write7BitEncodedInt(controls.Count);
        for (var at = 0; at < controls.Count; at++)
        {
            writer.Write7BitEncodedInt(controls.Point[at]);
            writer.Write((byte)controls.Control[at]);
            WriteSigned(writer, controls.Cluster[at]);
        }
    }

    static void WriteCrossings(BinaryWriter writer, CrossingArrays crossings)
    {
        writer.Write7BitEncodedInt(crossings.Count);
        var (x, y) = (0, 0);
        for (var at = 0; at < crossings.Count; at++)
        {
            WriteSigned(writer, crossings.Way[at]);
            WritePlace(writer, crossings.AtM[at].X, crossings.AtM[at].Y, ref x, ref y, StepsPerM);
            writer.Write((byte)crossings.Kind[at]);
            writer.Write((byte)(crossings.Painted[at] switch { false => 0, true => 1, null => 2 }));
            writer.Write7BitEncodedInt(crossings.Junction[at] + 1);
        }
    }

    static void WriteFootprints(BinaryWriter writer, FootprintArrays footprints)
    {
        writer.Write7BitEncodedInt(footprints.Count);
        writer.Write7BitEncodedInt(footprints.PointOffsets.Length - 1);
        writer.Write7BitEncodedInt(footprints.PointM.Length);
        var (x, y) = (0, 0);
        for (var footprint = 0; footprint < footprints.Count; footprint++)
        {
            writer.Write(footprints.Traced[footprint]);
            writer.Write7BitEncodedInt(Steps(footprints.HeightM[footprint], FootprintStepsPerM));
            writer.Write((byte)footprints.Use[footprint]);
            var (first, past) = (footprints.RingOffsets[footprint], footprints.RingOffsets[footprint + 1]);
            writer.Write7BitEncodedInt(past - first);
            for (var ring = first; ring < past; ring++)
            {
                var points = footprints.RingOf(ring);
                writer.Write7BitEncodedInt(points.Length);
                foreach (var pointM in points) WritePlace(writer, pointM.X, pointM.Y, ref x, ref y, FootprintStepsPerM);
            }
        }
    }

    static void WritePlaces(BinaryWriter writer, Vector2[] placeM)
    {
        writer.Write7BitEncodedInt(placeM.Length);
        var (x, y) = (0, 0);
        foreach (var atM in placeM) WritePlace(writer, atM.X, atM.Y, ref x, ref y, StepsPerM);
    }

    static void WritePlace(BinaryWriter writer, double xM, double yM, ref int x, ref int y, double stepsPerM)
    {
        var (stepX, stepY) = (Steps(xM, stepsPerM), Steps(yM, stepsPerM));
        WriteSigned(writer, stepX - x);
        WriteSigned(writer, stepY - y);
        (x, y) = (stepX, stepY);
    }

    static int Steps(double valueM, double stepsPerM) => checked((int)Math.Round(valueM * stepsPerM));

    static void WriteSigned(BinaryWriter writer, int value) => writer.Write7BitEncodedInt((value << 1) ^ (value >> 31));

    static void WriteSigned(BinaryWriter writer, long value) => writer.Write7BitEncodedInt64((value << 1) ^ (value >> 63));

    /// <summary>
    /// <b>This map cut down to a smaller frame</b>, its north-west corner at (<paramref name="leftM"/>,
    /// <paramref name="topM"/>) of this one's and every place moved by as much: what stands outside the new frame is
    /// left out, and a road or the coast running on past it keeps its line up to the first point past the
    /// frame, which is all <see cref="Survey.Of"/> reads of it — the engine cuts a road where it leaves the map and
    /// closes the sea along the map's edge. The frame keeps its projection and its margin.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Whole metres</b>, so every place moves by whole steps and stays the one it was. A coast way that never
    /// reaches the frame is left out even where it joined two that do: the sea is closed along the frame's edge
    /// between where they leave it and enter it again.
    /// </para>
    /// <para>
    /// A footprint is kept where it stands wholly inside the frame; a tree or a crossing where it stands inside it; a
    /// control or a turn where its point is still a road's.
    /// </para>
    /// </remarks>
    public TracedMap Cropped(int leftM, int topM, int widthM, int heightM)
    {
        var frame = new OsmFrame
        {
            Lat0Deg = Frame.Lat0Deg, Lon0Deg = Frame.Lon0Deg, WestM = Frame.WestM + leftM,
            SouthM = Frame.SouthM + (Frame.HeightM - topM - heightM), WidthM = widthM, HeightM = heightM, MarginM = Frame.MarginM,
        };
        if (!(widthM > 2 * frame.MarginM) || !(heightM > 2 * frame.MarginM)) throw new ArgumentException($"a frame of {widthM} by {heightM} m holds nothing inside its {frame.MarginM} m margin.");

        var offsetM = new Vector2D(leftM, topM);
        var placedM = new Vector2D[PointM.Length];
        for (var point = 0; point < placedM.Length; point++) placedM[point] = new Vector2D(PointM[point].X - leftM, PointM[point].Y - topM);

        var whole = new Box(0, 0, widthM, heightM);
        var renumbered = new int[PointM.Length];
        Array.Fill(renumbered, -1);
        var kept = new List<Vector2D>();

        var roads = new List<TracedRoad>();
        foreach (var road in Roads)
        {
            if (Trimmed(road.Points, whole) is { } points) roads.Add(road with { Points = points });
        }

        var coast = new List<int[]>();
        foreach (var line in Coast)
        {
            if (Trimmed(line, whole) is { } points) coast.Add(points);
        }

        var restrictions = Turns.Restrictions.Where(turn => renumbered[turn.Via] >= 0)
            .Select(turn => new OsmTurnRestriction { Relation = turn.Relation, From = turn.From, Via = renumbered[turn.Via], To = turn.To, Only = turn.Only });
        var links = Turns.LaneLinks.Where(link => renumbered[link.Via] >= 0)
            .Select(link => new OsmLaneLink { Relation = link.Relation, From = link.From, Via = renumbered[link.Via], To = link.To, FromLane = link.FromLane, ToLane = link.ToLane });

        var controls = Enumerable.Range(0, Controls.Count).Where(at => renumbered[Controls.Point[at]] >= 0).ToArray();
        var crossings = Enumerable.Range(0, Crossings.Count).Where(at => whole.Holds(Moved(Crossings.AtM[at]))).ToArray();
        var trees = TreeM.Select(Moved).Where(whole.Holds).ToArray();

        return new TracedMap
        {
            Name = Name, Description = Description, Licence = Licence, OsmBase = OsmBase, Relation = Relation, Frame = frame,
            PointM = [.. kept],
            Roads = [.. roads],
            Coast = [.. coast],
            Turns = new OsmTurns { Restrictions = [.. restrictions], LaneLinks = [.. links] },
            Controls = new ControlArrays
            {
                Point = [.. controls.Select(at => renumbered[Controls.Point[at]])],
                Control = [.. controls.Select(at => Controls.Control[at])],
                Cluster = [.. controls.Select(at => Controls.Cluster[at])],
            },
            Crossings = new CrossingArrays
            {
                Way = [.. crossings.Select(at => Crossings.Way[at])],
                AtM = [.. crossings.Select(at => Flat(Moved(Crossings.AtM[at])))],
                Kind = [.. crossings.Select(at => Crossings.Kind[at])],
                Painted = [.. crossings.Select(at => Crossings.Painted[at])],
                Junction = [.. crossings.Select(at => Crossings.Junction[at] == NoJunction ? NoJunction : renumbered[Crossings.Junction[at]])],
            },
            Footprints = CroppedFootprints(),
            TreeM = [.. trees.Select(Flat)],
        };

        Vector2D Moved(Vector2 atM) => new(atM.X - offsetM.X, atM.Y - offsetM.Y);

        static Vector2 Flat(Vector2D atM) => new((float)atM.X, (float)atM.Y);

        FootprintArrays CroppedFootprints()
        {
            var (ringOffsets, pointOffsets, pointM) = (new List<int> { 0 }, new List<int> { 0 }, new List<Vector2>());
            var (traced, heightM, use) = (new List<bool>(), new List<float>(), new List<FootprintUse>());
            for (var footprint = 0; footprint < Footprints.Count; footprint++)
            {
                var (first, past) = (Footprints.RingOffsets[footprint], Footprints.RingOffsets[footprint + 1]);
                var outline = Footprints.RingOf(first);
                var inside = true;
                foreach (var atM in outline) inside &= whole.Holds(Moved(atM));
                if (!inside) continue;

                for (var ring = first; ring < past; ring++)
                {
                    foreach (var atM in Footprints.RingOf(ring)) pointM.Add(Flat(Moved(atM)));
                    pointOffsets.Add(pointM.Count);
                }

                ringOffsets.Add(pointOffsets.Count - 1);
                traced.Add(Footprints.Traced[footprint]);
                heightM.Add(Footprints.HeightM[footprint]);
                use.Add(Footprints.Use[footprint]);
            }

            return new FootprintArrays
            {
                RingOffsets = [.. ringOffsets], PointOffsets = [.. pointOffsets], PointM = [.. pointM], Traced = [.. traced], HeightM = [.. heightM],
                Use = [.. use],
            };
        }

        // From the first straight touching the box to the last, so the points past it at either end are one each.
        int[]? Trimmed(int[] points, Box box)
        {
            var (first, past) = (-1, -1);
            for (var at = 1; at < points.Length; at++)
            {
                if (!box.Touches(placedM[points[at - 1]], placedM[points[at]])) continue;

                if (first < 0) first = at - 1;
                past = at + 1;
            }

            if (first < 0) return null;

            var trimmed = points[first..past];
            for (var at = 0; at < trimmed.Length; at++)
            {
                ref var point = ref renumbered[trimmed[at]];
                if (point < 0)
                {
                    point = kept.Count;
                    kept.Add(placedM[trimmed[at]]);
                }

                trimmed[at] = point;
            }

            return trimmed;
        }
    }

    [Flags]
    enum RoadFlags : byte
    {
        Bridge = 1,
        Tunnel = 2,
        Roundabout = 4,
        Marked = 8,
        LanesTagged = 16,
        Measured = 32,
        Arrowed = 64,
    }

    /// <summary>A rectangle in a map's own metres, and whether a straight touches it (Liang–Barsky).</summary>
    readonly record struct Box(double Left, double Top, double Right, double Bottom)
    {

        public bool Holds(Vector2D atM) => atM.X >= Left && atM.X <= Right && atM.Y >= Top && atM.Y <= Bottom;

        public bool Touches(Vector2D fromM, Vector2D toM)
        {
            var (enters, leaves) = (0.0, 1.0);
            var (acrossM, downM) = (toM.X - fromM.X, toM.Y - fromM.Y);
            ReadOnlySpan<(double Toward, double RoomM)> sides =
            [
                (-acrossM, fromM.X - Left), (acrossM, Right - fromM.X), (-downM, fromM.Y - Top), (downM, Bottom - fromM.Y),
            ];
            foreach (var (toward, roomM) in sides)
            {
                if (toward == 0.0)
                {
                    if (roomM < 0.0) return false;

                    continue;
                }

                var share = roomM / toward;
                if (toward < 0.0) enters = Math.Max(enters, share);
                else leaves = Math.Min(leaves, share);
            }

            return enters <= leaves;
        }
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

        ulong Varint()
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

/// <summary>
/// <b>One road way as a traced map holds it</b> (<see cref="TracedMap.Roads"/>): its OSM id, its class, whether it is a
/// bridge, a tunnel or part of a roundabout, its carriageway as OSM means it, its width as measured, and its line.
/// </summary>
internal sealed record TracedRoad
{
    /// <summary>The OSM way it was imported off, to look it up or fetch it again by.</summary>
    public required long OsmId { get; init; }

    /// <summary>OSM's own <c>highway</c> value: <c>primary</c>, <c>residential</c>, <c>trunk_link</c>.</summary>
    public required string Highway { get; init; }

    public bool Bridge { get; init; }

    public bool Tunnel { get; init; }

    /// <summary>Whether it is a roundabout's circulating carriageway, or a piece of one.</summary>
    public bool Roundabout { get; init; }

    /// <summary>
    /// Whether any lane has a <c>turn</c>, <c>change</c> or <c>psv</c>/<c>bus</c> <c>:lanes</c> entry of its own, which
    /// counts its lanes lane by lane (<see cref="Survey.Of"/>).
    /// </summary>
    public bool Marked { get; init; }

    /// <summary>
    /// Every lane left to right looking along <see cref="Points"/> as OSM drew them, with its direction, width and
    /// arrows; no <c>:lanes</c> entry is kept but its arrows (<see cref="Marked"/> says whether there were any).
    /// </summary>
    public required OsmCarriageway Carriageway { get; init; }

    /// <summary>The carriageway's width as the enrichment measured it and where, or null where nothing measured it.</summary>
    public (float WidthM, MeasuredFrom From)? Measured { get; init; }

    /// <summary>Indices into <see cref="TracedMap.PointM"/>, in the way's own order.</summary>
    public required int[] Points { get; init; }

    /// <summary>Whether any lane carries an arrow.</summary>
    public bool Arrowed
    {
        get
        {
            foreach (var lane in Carriageway.Lanes)
            {
                if (lane.Arrows != OsmArrows.None) return true;
            }

            return false;
        }
    }
}

/// <summary>A place in metres held in doubles, for the arithmetic done before a place is a float.</summary>
internal readonly record struct Vector2D(double X, double Y);
