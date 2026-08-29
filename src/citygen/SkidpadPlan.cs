using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>One run of the skidpad</b>: what the pedal is doing while the wheel is on its stop, as a share of
/// what that car's own throttle or brake is worth. Negative is astern.
/// </summary>
/// <remarks>
/// <b>The wheel is not in the table</b> because it is what every row holds still: the lock is what makes
/// the movement a circle at all, and a row that also varied it would be measuring two things at once.
/// </remarks>
/// <param name="Name">What the row is called, on the panel and in the report.</param>
/// <param name="Pedal">The share of the car's own pedal, ahead if positive and astern if negative.</param>
internal readonly record struct SkidpadRun(string Name, float Pedal);

/// <summary>
/// <b>The skidpad</b>: nothing but road, with a car every hundred metres — a column for every look the
/// fleet ships and a row for every way of driving a circle — each of them with its wheel on the left stop
/// and its pedal held where its row says.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it is for is one comparison</b>: the circle the geometry says a car must turn against the
/// circle it actually turns. The first is drawn by the turn-circle layer
/// (<c>App.Debug.TurnCircle</c>, OBS-2j) from the axles and the wheel angles alone; the second is written
/// on the ground by the tyres themselves, because every wheel on this map marks whatever it is doing
/// (<see cref="MarkFigures.PadFloor"/>). Standing a car still and looking at the two is the whole
/// instrument.
/// </para>
/// <para>
/// <b>A hundred metres a car, which is a figure and not a margin.</b> The widest circle anything here
/// turns is a few car lengths across, so the pitch is what keeps every square a private one: nothing
/// reaches its neighbour, nothing queues, and a car that has left its own square has failed rather than
/// merely wandered.
/// </para>
/// <para>
/// <b>A road a row, laid a whole pitch wide</b>, so the map is road edge to edge and the rows of the grid
/// are rows of the town. Nothing drives a lane here — every car on this map has its wheel held over
/// (<c>TownWorld.HoldTheWheels</c>) — but a pad with no road on it would be a pad whose ground, whose
/// grip and whose marks were the grass's.
/// </para>
/// <para>
/// <b>It is laid by this build like the proving grounds are</b> (<see cref="TrackPlan"/>): every figure it
/// stands on is the car's own, so a car that changes size is a pad that has to be laid again rather than a
/// file quietly measuring a fleet this build no longer ships.
/// </para>
/// </remarks>
internal static class SkidpadPlan
{
    /// <summary>The map's catalogue name, which is also its file's.</summary>
    public const string Name = "Skidpad";

    /// <summary>
    /// Whether the cars on this map are driven by the map rather than by drivers of their own. It is
    /// stated here because the map's name is all a town has to go on when it stands its cars up.
    /// </summary>
    public static bool HoldsItsCarsWheels(string name) => string.Equals(name, Name, StringComparison.Ordinal);

    /// <summary>
    /// <b>Every row the pad stands</b>, in the order they are laid down the map. Astern first, because the
    /// two ahead of it are the ones a reader already has an expectation about.
    /// </summary>
    /// <remarks>
    /// <b>Two pedals each way and the same lock throughout.</b> The pair is what the comparison is: half
    /// the pedal against all of it, in each gear, on one lock — so what differs between two rows is the
    /// throttle and nothing else, and what differs between the two pairs is the gear.
    /// <para>
    /// <b>Nothing lighter than half.</b> A tenth of the pedal is up against four patches scrubbing on full
    /// lock and most of the fleet never gets round at all under it — a row of cars sitting still is a row
    /// that measures nothing and takes as long to watch as one that moves.
    /// </para>
    /// </remarks>
    public static ReadOnlySpan<SkidpadRun> Runs => Table;

    static readonly SkidpadRun[] Table =
    [
        new("astern, full pedal", -1f),
        new("astern, half pedal", -0.5f),
        new("ahead, full pedal", 1f),
        new("ahead, half pedal", 0.5f),
    ];

    /// <summary>
    /// The wheel every car on the pad holds: hard over to the driver's left, as the share of that car's
    /// own lock a hand at the wheel asks for.
    /// </summary>
    public const float LockedLeft = -1f;

    /// <summary>
    /// <b>The pedal a car is held at</b>, which is its row's. Read by the town that holds the wheels and by
    /// the watch that reads what they came to, so the two cannot disagree about what was asked for.
    /// </summary>
    public static float PedalOf(int run) => Table[run].Pedal;

    public static string RunName(int run) => Table[run].Name;

    /// <summary>
    /// How many looks stand in a row, which is <b>one of every look the fleet ships</b>: a car takes its
    /// variant off its own place in the order the spawns are laid, so a column is one look driven six ways.
    /// </summary>
    /// <remarks>
    /// <b>It is a count and not the catalogue</b>, which a plan may not read: a plan knows core and nothing
    /// else. <c>SkidpadPlanTests</c> is what holds the two to each other, so a look added to
    /// <c>Fleet.json</c> without a square to drive in fails the suite rather than going unmeasured.
    /// </remarks>
    public const int Looks = 16;

    /// <summary>Which row of the pad a car stands in, off its own place in the fleet, and which column.</summary>
    public static int RunOf(int car) => car / Looks;

    public static int LookOf(int car) => car % Looks;

    /// <summary>How many cars the pad stands: one of every look, in every row.</summary>
    public static int Cars => Looks * Table.Length;

    /// <summary>
    /// The square each car has to itself. Wide enough that the widest circle on the map is a fraction of
    /// it, so nothing here is ever about two cars.
    /// </summary>
    public const float PitchM = 100f;

    /// <summary>
    /// <b>Four metres a cell, where every other map this build lays uses one.</b> A cell is a
    /// classification of ground and the whole of this map is one kind of it: there is no kerb to be a
    /// metre out of place, no verge to be stepped onto and nothing on foot to step. What the finer grid
    /// would buy is a file of several megabytes, because a grid that is road edge to edge carries a bearing
    /// under every cell of it and none of them is the nothing the format packs away.
    /// </summary>
    const float CellSizeM = 4f;

    /// <summary>Where a car stands: the middle of its own square, facing along the row.</summary>
    public static Vector2 StandsAtM(int car) =>
        new((LookOf(car) + 0.5f) * PitchM, (RunOf(car) + 0.5f) * PitchM);

    /// <summary>
    /// How big the map is: a square a car, and no margin at all. The pitch is the margin — a car in the
    /// middle of its own square is half a pitch from every edge it could reach.
    /// </summary>
    public static Vector2 WorldSizeM => new(Looks * PitchM, Table.Length * PitchM);

    public static CityPlan Lay(SimConfig config)
    {
        var worldSizeM = WorldSizeM;
        var gridWidth = (int)MathF.Round(worldSizeM.X / CellSizeM);
        var gridHeight = (int)MathF.Round(worldSizeM.Y / CellSizeM);
        var cells = new Ground[gridWidth * gridHeight];
        var laneDirs = new sbyte[cells.Length * 2];

        var rows = Table.Length;
        var segments = new ArcSeg[rows];
        var offsets = new int[rows + 1];
        var nodeM = new Vector2[rows * 2];
        for (var row = 0; row < rows; row++)
        {
            var alongM = (row + 0.5f) * PitchM;
            segments[row] = new ArcSeg(new Vector2(0f, alongM), 0f, worldSizeM.X, 0f);
            offsets[row + 1] = row + 1;
            nodeM[row * 2] = segments[row].StartM;
            nodeM[(row * 2) + 1] = segments[row].EndM;
        }

        var painter = new GroundPainter(cells, laneDirs, gridWidth, gridHeight, CellSizeM, config.RoadSideSign);
        for (var row = 0; row < rows; row++) painter.Road(segments.AsSpan(row, 1), PitchM);

        return new CityPlan
        {
            Seed = 0x736B6964_70616431UL,
            Name = Name,
            WorldSizeM = worldSizeM,
            CellSizeM = CellSizeM,

            // No pavement and so no walking network: there is nobody on foot here, and a kerb laid for
            // nobody is ground the comparison would have to explain.
            PavementWidthM = 0f,
            GridWidth = gridWidth,
            GridHeight = gridHeight,
            Cells = cells,
            LaneDirs = laneDirs,
            Junctions = Nodes(nodeM, config),
            StopLines = new CityPlan.StopLineArrays
            {
                CentreM = [], Approach = [], SpanM = [], ThicknessM = [], Junction = [], Road = [],
            },
            JunctionCorners = new CityPlan.JunctionCornerArrays
            {
                CornerM = [], ArcCentreM = [], RadiusM = [], TangentAM = [], TangentBM = [],
            },
            PavementCorners = new CityPlan.PavementCornerArrays
            {
                CornerM = [], NormalA = [], NormalB = [], RadiusM = [],
            },
            Roads = new CityPlan.RoadArrays
            {
                FromJunction = Ends(rows, first: true),
                ToJunction = Ends(rows, first: false),
                WidthM = Filled(rows, PitchM),
                SegmentOffsets = offsets,
                Segments = segments,
            },
            Bridges = new CityPlan.BridgeArrays
            {
                Road = [], FromM = [], ToM = [], DeckWidthM = [], PavementWidthM = [],
            },
            PavedAreas = new CityPlan.PavedAreaArrays { MinM = [], SizeM = [] },
            Crosswalks = new CityPlan.CrosswalkArrays
            {
                CentreM = [], Axis = [], DepthM = [], SpanM = [], Junction = [],
            },
            ParkingLots = new CityPlan.ParkingLotArrays
            {
                CentreM = [], Axis = [], HalfExtentM = [], SpaceOffsets = [0], SpacePositionM = [], SpaceHeadingRad = [],
            },
            Buildings = new CityPlan.BuildingArrays
            {
                CentreM = [], SizeM = [], HeadingRad = [], Capacity = [], Use = [], EntryOffsets = [0], EntryPointM = [],
            },
            Props = new CityPlan.PropArrays { CentreM = [], RadiusM = [], Kind = [] },
            Spawns = Spawns(),
            Water = new CityPlan.WaterArrays { OutlineOffsets = [0], PointM = [] },
        };
    }

    /// <summary>
    /// The two nodes each row is cut at, at the ends of it and no wider than a lane's own half-width.
    /// <b>None of them is lit</b>: nothing meets here, nothing gives way, and a light over ground nobody
    /// drives to would be a second thing refusing a movement nothing is making (SIM-7).
    /// </summary>
    static CityPlan.JunctionArrays Nodes(Vector2[] nodeM, SimConfig config) => new()
    {
        CentreM = nodeM,
        RadiusM = Filled(nodeM.Length, config.LaneOffsetM),
        Lit = new bool[nodeM.Length],
        PhaseOffsetS = new float[nodeM.Length],
    };

    /// <summary>A road end names a junction on every map there is, and each row of the pad has two of its own.</summary>
    static int[] Ends(int rows, bool first)
    {
        var ends = new int[rows];
        for (var row = 0; row < rows; row++) ends[row] = (row * 2) + (first ? 0 : 1);

        return ends;
    }

    /// <summary>
    /// The cars, one to a square, <b>row by row and look by look</b> — a car takes its variant off its own
    /// place in the spawns, so laying a whole row of them at a time is what makes a column one look.
    /// </summary>
    /// <remarks>
    /// They are all put down facing along the row. What each of them then does is its row's, and none of it
    /// is here: a spawned car is parked (GEN-7), and what holds this map's wheels over is the town.
    /// </remarks>
    static CityPlan.SpawnArrays Spawns()
    {
        var kind = new byte[Cars];
        var positionM = new Vector2[Cars];
        var headingRad = new float[Cars];
        for (var car = 0; car < Cars; car++)
        {
            kind[car] = SpawnKindCar;
            positionM[car] = StandsAtM(car);
            headingRad[car] = 0f;
        }

        return new CityPlan.SpawnArrays { Kind = kind, PositionM = positionM, HeadingRad = headingRad };
    }

    /// <summary>The spawn kind the format carries for a car.</summary>
    const byte SpawnKindCar = 1;

    static T[] Filled<T>(int count, T value)
    {
        var filled = new T[count];
        Array.Fill(filled, value);
        return filled;
    }
}
