using System.Numerics;
using TrafficSimulation.CityGen.Traced;
using TrafficSimulation.CityGen.Zones;

namespace TrafficSimulation.CityGen.Map;

/// <summary>
/// <b>A map's own file, and the whole of what the engine reads of a town</b> (GEN-58, <c>towns/&lt;Map&gt;.map</c>):
/// what is fixed of it — its water, its roads, and any building, prop or lot set down by hand — and what it is zoned
/// for, each zone an outline, a kind and the settings its builders lay it by (<see cref="Zones"/>). <b>Everything else is
/// laid when it is opened</b> (<see cref="TownPlan"/>), off the map's own seed: a wheel's streets, each lane off its
/// road's count, every connection across a junction, the walk, the lights, the zebras, the buildings, the props and
/// who stands in the town.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the map, and an edit is made to it.</b> A map is written by what authors it — the scanner off a real
/// place's survey (<see cref="TracedMapImport"/>, <c>qq osm --import</c>), or a brief's water and wheel
/// (<see cref="Gen.TownAuthor"/>, <c>--author</c>) — and changed in place after that (<see cref="Cropped"/>,
/// <see cref="Without"/>, <c>qq osm --zones</c>). Authoring it again replaces it and every edit made since.
/// </para>
/// <para>
/// <b>What is fixed is respected and what is zoned is laid</b>: a fixed road is laid as it stands, and a building or a
/// prop the map sets down stands where it was set and before anything the zones lay, which then stands clear of it.
/// <b>Facts and no rule</b>: a road's lanes are its counts and its carriageway one width, and what a lane is, how many
/// a width holds and what a zone's kind lays by default are the engine's (<see cref="Survey.Of"/>,
/// <see cref="Core.Config.ZoneFigures"/>), so retuning a rule relays the town off the same file.
/// </para>
/// <para>
/// <b>Read in one pass over bytes already in memory</b> (<see cref="Read(ReadOnlySpan{byte}, string)"/>). A road's,
/// the coast's and the water's places are whole millimetres — <b>not centimetres</b>: rounded to them, OdesaOsm's walk
/// no longer closes round two of its junctions — what is set down whole centimetres, and a zone's outline whole metres,
/// x east and y south off the frame's north-west corner, each written as its step from the place before. Counts,
/// indices and ids are 7-bit varints, a signed one zigzagged; a zone's setting is a whole number of its own step
/// (<see cref="ZoneParams.StepOf"/>); anything else little-endian, as <see cref="BinaryWriter"/> writes. The name and
/// the description come first, so the head the menu reads holds them (<see cref="MapHead"/>).
/// </para>
/// </remarks>
internal sealed partial record TownMap
{
    /// <summary>"TMAP", first in the file.</summary>
    const uint Magic = 0x50414D54;

    /// <summary>Bumped whenever what is written changes, so a file of the old shape is refused rather than misread.</summary>
    public const ushort Version = 7;

    /// <summary>A line's places, in steps a metre: millimetres.</summary>
    const double StepsPerM = 1000.0;

    /// <summary>What is set down, its place and its size: centimetres.</summary>
    const double StandStepsPerM = 100.0;

    /// <summary>A zone's outline: whole metres.</summary>
    const double ZoneStepsPerM = 1.0;

    /// <summary>A carriageway's offset is held to a thousandth of its width.</summary>
    const double OffsetSteps = 1000.0;

    /// <summary>A heading, in steps a turn.</summary>
    const double TurnSteps = 65536.0;

    /// <summary>The most lanes one way a road's lanes byte holds, a nibble each.</summary>
    const int LanesMost = 15;

    /// <summary>The most lanes both ways share its flags hold, in two bits.</summary>
    const int SharedMost = 3;

    public required string Name { get; init; }

    /// <summary>The line the start menu prints under the name.</summary>
    public required string Description { get; init; }

    /// <summary>Whose the map is to copy: a survey's licence, or empty for a map nobody else's data drew.</summary>
    public required string Licence { get; init; }

    /// <summary>The moment the OSM database it was traced off stood at, or empty for a map no survey drew.</summary>
    public required string OsmBase { get; init; }

    /// <summary>
    /// <b>What every draw made laying the town is keyed on</b> (GEN-1, GEN-11): the same map at the same seed is the same
    /// town every time it is opened. A traced map's is the OSM boundary relation its roads were drawn inside.
    /// </summary>
    public required ulong Seed { get; init; }

    /// <summary>The map's frame: how big it is, and where on the earth it stands where a survey placed it.</summary>
    public required OsmFrame Frame { get; init; }

    /// <summary>
    /// Every point a road or the coast passes. <b>Two ways sharing a point share an index</b>, which is how OSM says
    /// they meet; two that merely cross are not joined.
    /// </summary>
    public required Vector2D[] PointM { get; init; }

    /// <summary>The map's own roads, each laid as it stands; none where a zone lays the streets (<see cref="ZoneKind.Wheel"/>).</summary>
    public required TracedRoad[] Roads { get; init; }

    /// <summary>Every coastline as indices into <see cref="PointM"/>, land on its left as OSM draws it, closed against the map's edge.</summary>
    public required int[][] Coast { get; init; }

    /// <summary>Every water standing whole on the map: a river, a lake, a sea already closed.</summary>
    public WaterArrays Waters { get; init; } = WaterArrays.None;

    /// <summary>Every water drawn along a course: a river through the map, a sea along one edge of it.</summary>
    public CourseArrays Courses { get; init; } = CourseArrays.None;

    /// <summary>The buildings set down by hand, each stood as a prefab of its look.</summary>
    public StoodArrays Buildings { get; init; } = StoodArrays.None;

    /// <summary>The props set down by hand.</summary>
    public PropArrays Props { get; init; } = PropArrays.None;

    /// <summary>The car parks set down by hand, each a paved rectangle.</summary>
    public LotArrays Lots { get; init; } = LotArrays.None;

    /// <summary>What the map is zoned for, the whole map the first zone (<see cref="ZoneArrays"/>).</summary>
    public required ZoneArrays Zones { get; init; }

    /// <summary>How many people the town stands at its doors (<see cref="ZoneParam.People"/>).</summary>
    public int People => (int)(Zones.Own(ZoneArrays.Root, ZoneParam.People) ?? 0f);

    /// <summary>How many cars the town stands where nobody lives in it (<see cref="ZoneParam.Cars"/>).</summary>
    public int Cars => (int)(Zones.Own(ZoneArrays.Root, ZoneParam.Cars) ?? 0f);

    public Vector2 SizeM => new((float)Frame.WidthM, (float)Frame.HeightM);

    /// <summary>
    /// <b>The bare frame of a map</b>: no line, nothing set down, and one zone, the whole of it, of a kind and with
    /// settings given.
    /// </summary>
    public static TownMap Bare(string name, string description, ulong seed, Vector2 sizeM, ZoneKind root, ReadOnlySpan<(ZoneParam, float)> settings) =>
        new()
        {
            Name = name, Description = description, Licence = "", OsmBase = "", Seed = seed,
            Frame = new OsmFrame { Lat0Deg = 0, Lon0Deg = 0, WestM = 0, SouthM = 0, WidthM = sizeM.X, HeightM = sizeM.Y, MarginM = 0 },
            PointM = [], Roads = [], Coast = [], Zones = ZoneArrays.Whole(sizeM, root, settings),
        };

    /// <summary>The outline of a whole frame, clockwise from its north-west corner as y runs south.</summary>
    public static Vector2[] WholeOutline(Vector2 sizeM) => [Vector2.Zero, new(sizeM.X, 0f), sizeM, new(0f, sizeM.Y)];

    /// <summary>Refuses a map that cannot describe a town, at the point it is read or before it is written.</summary>
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
            if (road.Lanes == 0) throw new InvalidDataException($"{what}: road way {road.OsmId} has no lanes.");
            if (road.LanesForward is < 0 or > LanesMost || road.LanesBackward is < 0 or > LanesMost || road.LanesShared is < 0 or > SharedMost)
            {
                throw new InvalidDataException($"{what}: road way {road.OsmId} of {road.LanesForward}+{road.LanesBackward}+{road.LanesShared} lanes.");
            }

            if (road.WidthM is <= 0f) throw new InvalidDataException($"{what}: road way {road.OsmId} measured {road.WidthM} m wide.");

            Within(road.Points, $"road way {road.OsmId}");
        }

        foreach (var line in Coast)
        {
            if (line.Length < 2) throw new InvalidDataException($"{what}: a coastline of {line.Length} points.");
            Within(line, "a coastline");
        }

        for (var water = 0; water < Waters.Count; water++)
        {
            if (Waters.OutlineOf(water).Length < 3) throw new InvalidDataException($"{what}: water {water} has an outline of fewer than three points.");
        }

        for (var water = 0; water < Courses.Count; water++)
        {
            if (Courses.CourseOf(water).Length < 2) throw new InvalidDataException($"{what}: water {water} runs along a course of fewer than two points.");
        }

        for (var building = 0; building < Buildings.Count; building++)
        {
            if (!(Buildings.SizeM[building].X > 0f) || !(Buildings.SizeM[building].Y > 0f))
            {
                throw new InvalidDataException($"{what}: building {building} set down {Buildings.SizeM[building]} m.");
            }
        }

        Zones.Check(what);

        void Within(ReadOnlySpan<int> indices, string which)
        {
            foreach (var point in indices)
            {
                if ((uint)point >= (uint)points) throw new InvalidDataException($"{what}: {which} passes point {point} of {points}.");
            }
        }
    }

    /// <summary>What a water is, which is whether a road may ever stand over it (GEN-14b).</summary>
    internal enum WaterBody : byte
    {
        /// <summary>A sea, or a sea's bay: one shore inside the map, and never bridged.</summary>
        Sea,

        /// <summary>A river across the map, bridged where a road meets it.</summary>
        River,

        /// <summary>A lake or a pond: water standing whole on the land, and never bridged.</summary>
        Lake,
    }

    /// <summary>
    /// <b>The waters standing whole on the map</b>, each its kind and its outline, flat with offsets beside them, none
    /// closed on its first point. An island is the land between two waters' outlines, a water having no hole.
    /// </summary>
    internal sealed class WaterArrays
    {
        public required WaterBody[] Kind { get; init; }

        /// <summary>Count + 1 entries, over <see cref="PointM"/>.</summary>
        public required int[] PointOffsets { get; init; }

        public required Vector2[] PointM { get; init; }

        public int Count => Kind.Length;

        public ReadOnlySpan<Vector2> OutlineOf(int water) => PointM.AsSpan(PointOffsets[water], PointOffsets[water + 1] - PointOffsets[water]);

        public static WaterArrays None => new() { Kind = [], PointOffsets = [0], PointM = [] };
    }

    /// <summary>
    /// <b>The waters drawn along a course</b> (<see cref="Gen.TerrainStage.Rings"/>): each its kind, the line it runs
    /// along, the way across that line and how far each of its banks stands off it that way — a sea's far bank past the
    /// map. <b>Written as they are</b>, a float's own four bytes each: what a wheel was laid round when it was authored
    /// is the water its streets are laid round when it is opened.
    /// </summary>
    internal sealed class CourseArrays
    {
        public required WaterBody[] Kind { get; init; }
        public required Vector2[] Across { get; init; }
        public required float[] NearM { get; init; }
        public required float[] FarM { get; init; }

        /// <summary>Count + 1 entries, over <see cref="PointM"/>.</summary>
        public required int[] PointOffsets { get; init; }

        public required Vector2[] PointM { get; init; }

        public int Count => Kind.Length;

        public ReadOnlySpan<Vector2> CourseOf(int water) => PointM.AsSpan(PointOffsets[water], PointOffsets[water + 1] - PointOffsets[water]);

        public static CourseArrays None => new() { Kind = [], Across = [], NearM = [], FarM = [], PointOffsets = [0], PointM = [] };
    }

    /// <summary>The buildings set down by hand: each a look, its middle, its sides — the front first — and its bearing.</summary>
    internal sealed class StoodArrays
    {
        public required BuildingLook[] Look { get; init; }
        public required Vector2[] CentreM { get; init; }
        public required Vector2[] SizeM { get; init; }
        public required float[] HeadingRad { get; init; }
        public int Count => Look.Length;

        public static StoodArrays None => new() { Look = [], CentreM = [], SizeM = [], HeadingRad = [] };
    }

    /// <summary>The props set down by hand: each its kind, where it stands and the bearing it turns to.</summary>
    internal sealed class PropArrays
    {
        public required PropKind[] Kind { get; init; }
        public required Vector2[] CentreM { get; init; }
        public required float[] BearingRad { get; init; }
        public int Count => Kind.Length;

        public static PropArrays None => new() { Kind = [], CentreM = [], BearingRad = [] };
    }

    /// <summary>The car parks set down by hand: each its middle, its sides and the bearing of its first.</summary>
    internal sealed class LotArrays
    {
        public required Vector2[] CentreM { get; init; }
        public required Vector2[] SizeM { get; init; }
        public required float[] HeadingRad { get; init; }
        public int Count => CentreM.Length;

        public static LotArrays None => new() { CentreM = [], SizeM = [], HeadingRad = [] };
    }
}
