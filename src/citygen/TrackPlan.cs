using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Which proving ground is laid.</b> The road and its shapes are the same on every one of them and
/// exactly one other thing differs, so a figure that moves between two of these tables is a fact about
/// that one thing and about nothing else.
/// </summary>
internal enum TrackLap
{
    /// <summary>
    /// The lap as it is measured. Fifteen people <b>beside the road, pacing across it</b>: out into the
    /// lane at a car that can still stop, a stand until that car has, and back. Each of them is a stop for
    /// the shape behind it to be measured by, and the cars are six nominal ones (CAR-11a).
    /// </summary>
    Pacing,

    /// <summary>
    /// The same six cars, with the same fifteen people <b>in the road rather than beside it, reeling down
    /// it.</b> A body put down on the carriageway with nowhere to be lurches along the way the traffic
    /// runs, thrown anywhere across the width of it, and stands where it is every few lurches — so what a
    /// driver meets is a slow thing to be followed that becomes, when it stops, a thing to be got past.
    /// </summary>
    Drunk,

    /// <summary>
    /// <b>The whole fleet driving instead of six of one car</b> — one of every look, at its own weight, its
    /// own footprint, its own axles and its own handling. What the measured lap deliberately holds still
    /// (CAR-11a) is the whole of what this one varies, so this table answers whether a car anybody may be
    /// handed can drive the road, and never which drive layout is worth what.
    /// </summary>
    /// <remarks>
    /// <b>And nobody is on foot here.</b> The other two laps carry fifteen people because what they measure
    /// is a driver stopping for what is in front of it; what this one measures is the car, and a body in
    /// the road would only be a second thing setting its speed. The lap is the cars and the road.
    /// </remarks>
    Fleet,
}

/// <summary>
/// <b>The test track</b>: one closed lap cut into ten roads — five shapes with a link between each pair,
/// so that what a car does on a shape is a fact about that shape and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the one map this build lays itself</b>, and it is not a city: no building, nobody living on
/// it, and its only junctions are the ten the lap is cut at. Every other map arrives as a file exported by
/// whatever generated it (GEN-1), and this one would too if a proving ground were a town — but the whole
/// value of it is that its geometry is <em>chosen</em>, so it is written where the figures it is chosen
/// against live and exported through the same format.
/// </para>
/// <para>
/// <b>One lap, and every car on it.</b> Four separate circuits measured four shapes with one car apiece
/// and could say nothing about a fifth car or a second kind of drivetrain; one lap carries as many cars as
/// it has room for, each of them meeting every shape in turn. What that costs is traffic — a car held by
/// the one in front is a car the road is no longer the reason for — and what pays for it is that the
/// holding is <em>named</em>, so a pass somebody else was in the way of is thrown away rather than averaged
/// in.
/// </para>
/// <para>
/// <b>It lays three maps and not one</b> (<see cref="TrackLap"/>), and each of the other two differs from
/// <c>Track</c> in exactly one thing so that its table can be read against <c>Track</c>'s. <c>Drunk</c>
/// moves the fifteen people from beside the road into it. <c>Fleet</c> takes them off the lap altogether
/// and puts <em>the whole fleet</em> on it instead of six of the nominal car — so what that table answers
/// is whether every car anybody may be handed can drive this road, with nothing but the road and the other
/// cars deciding its speed.
/// </para>
/// <para>
/// <b>People pace the road all the way round it, one of them at the end of every shape.</b> There is no
/// light anywhere on this lap and no paint under one: a body steps out of the lane's own edge into the
/// middle of it at a car that can still stop, stands until that car has, and walks back — so what brings a
/// car to rest is a driver looking at what is in front of it, and nothing owes it any warning. The one at
/// the end of each shape is what makes a leg of the lap a whole measurement: pull away, reach a top speed,
/// be held to whatever the shape allows, and stop for whoever is in the way. A pass nobody stepped out in
/// front of is as useful as one they did, and is what the held speed is really read off — a car that never
/// stopped is a car the shape alone slowed.
/// </para>
/// <para>
/// <b>Every bend on the lap is one of the five shapes.</b> There is no neutral corner anywhere: the sweep
/// of the arc is what pays for the half turn of the hairpin and the quarter turn back, so the lap closes on
/// the shapes themselves. A link is always straight, and the last of them is <em>derived</em> rather than
/// chosen — it is whatever brings the lap home.
/// </para>
/// </remarks>
internal static class TrackPlan
{
    /// <summary>The map's catalogue name, which is also its file's.</summary>
    public const string Name = "Track";

    /// <summary>
    /// And the same lap with <see cref="TrackLap.Drunk"/> on it, which is a map of its own because the
    /// two are read against each other: one figure differs, so a difference between the two tables is a
    /// difference about what is in the road.
    /// </summary>
    public const string DrunkName = "Drunk";

    /// <summary>And the same lap again with the whole fleet on it (<see cref="TrackLap.Fleet"/>).</summary>
    public const string FleetName = "Fleet";

    /// <summary>Which map each lap lays, so the name and what it stands for are decided in one place.</summary>
    public static string NameOf(TrackLap lap) => lap switch
    {
        TrackLap.Drunk => DrunkName,
        TrackLap.Fleet => FleetName,
        _ => Name,
    };

    /// <summary>
    /// Whether the cars on this map are the nominal one (CAR-11a) rather than the fleet as it ships. It is
    /// the whole of what <see cref="TrackLap.Fleet"/> changes, and it is stated here because the map's name
    /// is all a town has to go on when it stands its cars up.
    /// </summary>
    public static bool StandsTheNominalCar(string name) =>
        string.Equals(name, Name, StringComparison.Ordinal)
        || string.Equals(name, DrunkName, StringComparison.Ordinal);

    /// <summary>
    /// The road each shape is, which is also its place in <see cref="Sections"/>. <b>A shape is even and
    /// the link after it is odd</b>, so the node a shape ends at is the odd one — which is where somebody
    /// is standing.
    /// </summary>
    public const int Straight = 0;

    public const int Turn180 = 2;

    public const int Snake = 4;

    public const int Arc = 6;

    public const int Turn90 = 8;

    /// <summary>How many roads the lap is cut into: a shape and the link that follows it, five times over.</summary>
    public const int Roads = 10;

    /// <summary>Every stretch of the lap in the order a car meets them.</summary>
    public static ReadOnlySpan<TrackSection> Sections => Table;

    static readonly TrackSection[] Table =
    [
        new(Straight, "straight", TrackShape.Straight, 0f),
        new(Straight + 1, "to the 180", TrackShape.Link, 0f),
        new(Turn180, "180 turn", TrackShape.Turn180, Turn180RadiusM),
        new(Turn180 + 1, "to the snake", TrackShape.Link, 0f),
        new(Snake, "snake", TrackShape.Snake, SnakeRadiusM),
        new(Snake + 1, "to the arc", TrackShape.Link, 0f),
        new(Arc, "arc", TrackShape.Arc, ArcRadiusM),
        new(Arc + 1, "to the 90", TrackShape.Link, 0f),
        new(Turn90, "90 turn", TrackShape.Turn90, Turn90RadiusM),
        new(Turn90 + 1, "to the straight", TrackShape.Link, 0f),
    ];

    /// <summary>A metre a cell, which is what the fixture map uses and what the classifier's tolerances are written against.</summary>
    const float CellSizeM = 1f;

    /// <summary>How much ground is left round the lap. Enough that the camera can frame the whole of it.</summary>
    const float MarginM = 40f;

    /// <summary>
    /// <b>Long enough to reach the gear's own cap and still stop for whoever steps out at the end of it.</b>
    /// That is the whole of what this shape is for: a car that runs out of road before it runs out of
    /// gearbox measures the road, and a car that cannot stop from what it reached measures nothing at all.
    /// </summary>
    const float StraightM = 500f;

    /// <summary>The run away from the end of the straight and up to the hairpin, which is braked for from whatever it gives.</summary>
    const float ToTheTurn180M = 260f;

    /// <summary>Tighter than any corner the shipped towns hold.</summary>
    public const float Turn180RadiusM = 15f;

    /// <summary>
    /// A link is long enough for a car to pull away from a standstill, reach a speed the shape after it has
    /// to be braked for, and shed it again. <b>Shortening the straight lengthens these two</b> unless they
    /// give the same ground back, because what closes the lap is what is left over once every chosen length
    /// has been laid.
    /// </summary>
    const float ToTheSnakeM = 185f;

    /// <summary>
    /// The snake's own corner, taken alternately one way and the other and never let go of.
    /// </summary>
    /// <remarks>
    /// A wave is <c>−θ, +2θ, −θ</c>, which is a palindrome and therefore <b>leaves the car exactly where
    /// the line it started on would have taken it</b> — so any number of waves is a snake that neither
    /// turns nor drifts, and the lap closes without the snake being in the arithmetic.
    /// </remarks>
    public const float SnakeRadiusM = 40f;

    const float SnakeSwingDeg = 60f;

    const int SnakeWaves = 5;

    const float ToTheArcM = 185f;

    /// <summary>
    /// The widest of the corners and the longest held: three quarters of a turn at one radius, which is
    /// where a speed a car simply <em>keeps</em> is read off.
    /// </summary>
    /// <remarks>
    /// <b>Its sweep is what closes the lap.</b> The hairpin turns the car through half a circle and the
    /// quarter turn takes back a quarter of one; three quarters here is what is left to make a whole, and
    /// that is why the track carries no bend that is not one of the five shapes.
    /// </remarks>
    public const float ArcRadiusM = 120f;

    const float ArcSweepDeg = 270f;

    /// <summary>The corner an ordinary street junction is, and the only right-hander on the lap.</summary>
    public const float Turn90RadiusM = 30f;

    /// <summary>No bend is laid in one piece longer than this. A quarter turn is the most any consumer here has ever been handed.</summary>
    const float MostOfATurnRad = MathF.PI * 0.5f;

    /// <summary>How fine the cells are painted along and across a road. Under half a cell, so no cell of the band is stepped over.</summary>
    const float PaintStepM = CellSizeM * 0.4f;

    /// <summary>
    /// How near a node a body may stand before it is on ground the lane was cut back off. It is what keeps
    /// a car off the disc it was parked beside, and what puts the pacer on the shape's own last stretch
    /// rather than in the gap between two roads.
    /// </summary>
    const float ClearOfANodeM = 12f;

    /// <summary>
    /// The paving each pacer stands on: a square of it, so the body has somewhere to be that is plainly
    /// not the road. It is scenery — nothing walks a network here — and it is the one thing on the map
    /// that says where to look.
    /// </summary>
    const float PadM = 6f;

    /// <summary>
    /// How many cars the measured lap carries. <b>Two of each drivetrain</b>, in the order the fleet ships
    /// them, so the rear, front and all-wheel answers are each a pair rather than one car's day.
    /// </summary>
    public const int Cars = 6;

    /// <summary>
    /// And how many the fleet lap carries: <b>one of every look the fleet ships</b>, since a car takes its
    /// variant off its own place in the order the spawns are laid in.
    /// </summary>
    /// <remarks>
    /// <b>It is a count and not the catalogue</b>, which a plan may not read: a plan knows core and nothing
    /// else, and the fleet is a file the agents' slice owns. <b>The lap's own table is what reports it</b>:
    /// a look added to <c>Fleet.json</c> without a car to drive it turns up as a row nothing drove rather
    /// than as a look quietly going unmeasured.
    /// </remarks>
    public const int FleetCars = 16;

    /// <summary>How many cars stand on each lap.</summary>
    public static int CarsOn(TrackLap lap) => lap == TrackLap.Fleet ? FleetCars : Cars;

    /// <summary>
    /// And how many people. They are laid after the cars, so a car's spawn index is its own place in the
    /// fleet.
    /// </summary>
    /// <remarks>
    /// <b>One at the end of every shape, and the rest spread evenly round the lap.</b> The first five are
    /// placed rather than spread because a shape is measured by the stop at the end of it, and an even
    /// spacing over a lap whose shapes run from 47 m to 838 m would leave the two tight corners with nobody
    /// on them. The others are what makes the whole lap a road somebody might step into rather than five
    /// places where somebody does.
    /// </remarks>
    public const int Pacers = 15;

    /// <summary>
    /// How far apart two of them stand at the least. Two bodies nearer than this are one obstacle: a car
    /// that stopped for the first would still be pulling away when it reached the second.
    /// </summary>
    const float PacersApartM = 60f;

    /// <summary>The corner a section is read against, or zero where it does not bend.</summary>
    public static float RadiusM(int road) => Table[road].RadiusM;

    /// <summary>How long each stretch is, which is what a lap count and a section's own figures are read off.</summary>
    public static float LengthM(int road) => Spline.TotalLengthM(CollectionsMarshal.AsSpan(Lap()[road]));

    /// <summary>How long one lap is.</summary>
    public static float LapM()
    {
        var lap = Lap();
        var lengthM = 0f;
        for (var road = 0; road < Roads; road++) lengthM += Spline.TotalLengthM(CollectionsMarshal.AsSpan(lap[road]));

        return lengthM;
    }

    /// <summary>
    /// The lap, road by road, in the order it is driven — <b>laid where it stands on the map</b>, so what
    /// a test walks is what a car drives.
    /// </summary>
    public static List<ArcSeg>[] Lap()
    {
        var lap = Open();
        var offsetM = MarginM * Vector2.One - Corner(lap, lowest: true);
        foreach (var road in lap)
        {
            for (var arc = 0; arc < road.Count; arc++) road[arc] = road[arc] with { StartM = road[arc].StartM + offsetM };
        }

        return lap;
    }

    /// <summary>
    /// How big the map has to be to hold the lap with its margin: the ground the roads cover, rounded out
    /// to whole cells. <b>Taken off the geometry rather than declared</b>, so a shape that grows cannot
    /// quietly run off the edge of the world.
    /// </summary>
    public static Vector2 WorldSizeM(SimConfig config)
    {
        var lap = Open();
        var spanM = Corner(lap, lowest: false) - Corner(lap, lowest: true) + (2f * MarginM * Vector2.One);
        var edgeM = (config.RoadWidthM * 0.5f) + CellSizeM;
        return new Vector2(
            MathF.Ceiling((spanM.X + (2f * edgeM)) / CellSizeM) * CellSizeM,
            MathF.Ceiling((spanM.Y + (2f * edgeM)) / CellSizeM) * CellSizeM);
    }

    public static CityPlan Lay(SimConfig config, TrackLap which = TrackLap.Pacing)
    {
        var worldSizeM = WorldSizeM(config);
        var gridWidth = (int)MathF.Round(worldSizeM.X / CellSizeM);
        var gridHeight = (int)MathF.Round(worldSizeM.Y / CellSizeM);
        var cells = new Ground[gridWidth * gridHeight];
        var laneDirs = new sbyte[cells.Length * 2];
        var widthM = config.RoadWidthM;

        var lap = Lap();
        var segments = new List<ArcSeg>();
        var offsets = new List<int> { 0 };
        var nodeM = new Vector2[Roads];
        for (var road = 0; road < Roads; road++)
        {
            nodeM[road] = lap[road][0].StartM;
            segments.AddRange(lap[road]);
            offsets.Add(segments.Count);
        }

        var painter = new GroundPainter(cells, laneDirs, gridWidth, gridHeight, CellSizeM, config.RoadSideSign);
        for (var road = 0; road < Roads; road++)
        {
            painter.Road(CollectionsMarshal.AsSpan(segments)[offsets[road]..offsets[road + 1]], widthM);
        }

        // After the roads, and only over ground no road took: a pad is where somebody stands, and a road
        // that gave way to one would be a hole in the lap. There is none under a drunk — it is put down in
        // the carriageway, and paving the place it started would be paving a piece of the lap — and none at
        // all on the fleet lap, which carries nobody on foot.
        var standing = which switch
        {
            TrackLap.Drunk => ReelingFrom(lap, config),
            TrackLap.Fleet => [],
            _ => PacedFrom(lap, config),
        };

        if (which == TrackLap.Pacing)
        {
            foreach (var pacer in standing) painter.Pad(pacer.StandM, PadM);
        }

        return new CityPlan
        {
            Seed = which switch
            {
                TrackLap.Drunk => 0x7261636B_64726E6BUL,
                TrackLap.Fleet => 0x7261636B_666C7431UL,
                _ => 0x7261636B_74726B32UL,
            },
            Name = NameOf(which),
            WorldSizeM = worldSizeM,
            CellSizeM = CellSizeM,

            // No pavement, and so no walking network at all: there is nobody on a proving ground, and a
            // kerb laid for nobody is ground the measurement would have to explain.
            PavementWidthM = 0f,
            GridWidth = gridWidth,
            GridHeight = gridHeight,
            Cells = cells,
            LaneDirs = laneDirs,
            Junctions = Nodes(nodeM, config),

            // No paint anywhere on the lap. A bar is a place to stop that a driver is told about before it
            // needs to, and the whole of what this track asks of a driver is that it stops for what it can
            // see (SIM-7: naming a second thing that refuses the same movement makes the first useless).
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
                FromJunction = Ends(first: true),
                ToJunction = Ends(first: false),
                WidthM = Filled(Roads, widthM),
                SegmentOffsets = [.. offsets],
                Segments = [.. segments],
            },
            Bridges = new CityPlan.BridgeArrays
            {
                Road = [], FromM = [], ToM = [], DeckWidthM = [], PavementWidthM = [],
            },
            PavedAreas = new CityPlan.PavedAreaArrays { MinM = [], SizeM = [] },
            Crosswalks = new CityPlan.CrosswalkArrays
            {
                CentreM = [], Axis = [], DepthM = [], Road = [], Junction = [],
            },
            ParkingLots = new CityPlan.ParkingLotArrays
            {
                CentreM = [], Axis = [], HalfExtentM = [], SpaceOffsets = [0], SpacePositionM = [], SpaceHeadingRad = [],
            },
            Buildings = new CityPlan.BuildingArrays
            {
                CentreM = [], SizeM = [], HeadingRad = [], Capacity = [], Use = [], EntryOffsets = [0], EntryPointM = [],
            },
            Props = new CityPlan.PropArrays { CentreM = [], RadiusM = [], BearingRad = [], Kind = [] },
            Spawns = Spawns(lap, standing, config, CarsOn(which)),
            Water = CityPlan.WaterArrays.None,
        };
    }

    /// <summary>
    /// The lap as it comes out of the arithmetic, before it is moved onto the map: it starts at the origin
    /// heading east and comes back to it.
    /// </summary>
    /// <remarks>
    /// <b>The last two straights are answers rather than choices.</b> Everything up to the end of the arc
    /// is chosen, and what remains is one rise and one run — so the lap closes exactly instead of nearly,
    /// and a shape that changes size moves those two rather than leaving a step in the road.
    /// </remarks>
    static List<ArcSeg>[] Open()
    {
        var lap = new List<ArcSeg>[Roads];
        var at = new Cursor { AtM = Vector2.Zero, HeadingRad = 0f };

        lap[Straight] = [Line(ref at, StraightM)];
        lap[Straight + 1] = [Line(ref at, ToTheTurn180M)];
        lap[Turn180] = Bend(ref at, Turn180RadiusM, MathF.PI);
        lap[Turn180 + 1] = [Line(ref at, ToTheSnakeM)];
        lap[Snake] = SnakeRoad(ref at);
        lap[Snake + 1] = [Line(ref at, ToTheArcM)];
        lap[Arc] = Bend(ref at, ArcRadiusM, Radians(ArcSweepDeg));

        // The arc leaves the car heading north, a quarter turn and two straights from home: the rise is
        // what the right-hander needs to arrive on the lap's own line, and the run is what is left of it.
        var riseM = -at.AtM.Y - Turn90RadiusM;
        Closes(riseM, "the rise into the 90");
        lap[Arc + 1] = [Line(ref at, riseM)];
        lap[Turn90] = Bend(ref at, Turn90RadiusM, -MostOfATurnRad);

        var runM = -at.AtM.X;
        Closes(runM, "the run back to the straight");
        lap[Turn90 + 1] = [Line(ref at, runM)];

        return lap;
    }

    /// <summary>A lap that does not close is a track laid against figures it cannot hold, and it fails here rather than on the road.</summary>
    static void Closes(float lengthM, string what)
    {
        if (lengthM > ClearOfANodeM) return;

        throw new InvalidOperationException(
            $"The {Name} lap cannot close: {what} came to {lengthM:F1} m. A shape has outgrown the room the others leave it.");
    }

    /// <summary>Where the next piece of the lap starts, carried along as the chain is laid.</summary>
    struct Cursor
    {
        public Vector2 AtM;

        public float HeadingRad;
    }

    static ArcSeg Line(ref Cursor at, float lengthM)
    {
        var arc = new ArcSeg(at.AtM, at.HeadingRad, lengthM, 0f);
        at.AtM = arc.EndM;
        return arc;
    }

    /// <summary>
    /// One bend, in pieces no longer than a quarter turn each. <b>The pieces are the same circle</b> — a
    /// bend is not made gentler by being cut up — and cutting it keeps every arc on this map inside the
    /// span every other map's arcs are already drawn and sampled at.
    /// </summary>
    static List<ArcSeg> Bend(ref Cursor at, float radiusM, float sweepRad)
    {
        // Shaved before rounding up, so a sweep that is exactly three quarter turns is three pieces and
        // not four: the ratio comes back a hair over three in float, and ceiling takes it at its word.
        var pieces = Math.Max(1, (int)MathF.Ceiling((MathF.Abs(sweepRad) / MostOfATurnRad) - 1e-3f));
        var eachRad = sweepRad / pieces;

        var arcs = new List<ArcSeg>(pieces);
        for (var piece = 0; piece < pieces; piece++)
        {
            var arc = new ArcSeg(
                at.AtM, at.HeadingRad, radiusM * MathF.Abs(eachRad), MathF.Sign(eachRad) / radiusM);
            arcs.Add(arc);
            at.AtM = arc.EndM;
            at.HeadingRad = arc.HeadingAtRad(arc.LengthM);
        }

        return arcs;
    }

    /// <summary>
    /// The snake: waves of <c>−θ, +2θ, −θ</c>, which swing to the outside of the lap and come back to the
    /// line they were laid on. Bulging inward would put it in the straight's lap.
    /// </summary>
    static List<ArcSeg> SnakeRoad(ref Cursor at)
    {
        var swingRad = Radians(SnakeSwingDeg);
        var arcs = new List<ArcSeg>();
        for (var wave = 0; wave < SnakeWaves; wave++)
        {
            arcs.AddRange(Bend(ref at, SnakeRadiusM, -swingRad));
            arcs.AddRange(Bend(ref at, SnakeRadiusM, 2f * swingRad));
            arcs.AddRange(Bend(ref at, SnakeRadiusM, -swingRad));
        }

        return arcs;
    }

    static float Radians(float degrees) => degrees * MathF.PI / 180f;

    /// <summary>The lowest or highest corner of the ground the lap's centreline covers, sampled finely enough to catch the outside of a bend.</summary>
    static Vector2 Corner(List<ArcSeg>[] lap, bool lowest)
    {
        var cornerM = new Vector2(lowest ? float.MaxValue : float.MinValue);
        foreach (var road in lap)
        {
            var chain = CollectionsMarshal.AsSpan(road);
            var lengthM = Spline.TotalLengthM(chain);
            for (var atM = 0f; atM <= lengthM; atM += PaintStepM)
            {
                var pointM = Spline.SampleAt(chain, MathF.Min(atM, lengthM)).PositionM;
                cornerM = lowest ? Vector2.Min(cornerM, pointM) : Vector2.Max(cornerM, pointM);
            }
        }

        return cornerM;
    }

    /// <summary>
    /// <b>A road end names a junction on every map there is.</b> The lap's nodes are laid one to a road,
    /// in road order, so road <c>r</c> runs from node <c>r</c> to node <c>r + 1</c> and the last one
    /// arrives back at the first.
    /// </summary>
    static int[] Ends(bool first)
    {
        var ends = new int[Roads];
        for (var road = 0; road < Roads; road++) ends[road] = (road + (first ? 0 : 1)) % Roads;

        return ends;
    }

    /// <summary>
    /// The nodes the lap is cut at, <b>no wider than a lane's own half-width</b> — a disc is ground no lane
    /// is laid over, and every metre of it is a metre of lap the measurement loses.
    /// </summary>
    /// <remarks>
    /// <b>None of them is lit.</b> Nothing meets at a node here and there is nothing to give way to, so
    /// there would be nothing for a light to arbitrate; what brings a car to rest at the end of a shape is
    /// somebody standing in the lane, and a light over the same ground would be a second thing refusing the
    /// same movement (SIM-7).
    /// </remarks>
    static CityPlan.JunctionArrays Nodes(Vector2[] nodeM, SimConfig config) => new()
    {
        CentreM = nodeM,
        RadiusM = Filled(Roads, config.LaneOffsetM),
        Lit = new bool[Roads],
        PhaseOffsetS = new float[Roads],
    };

    /// <summary>
    /// Where one of the lap's people is put down, and the way it faces — across the lane a pacer is about
    /// to step into, along the one a drunk is about to reel down.
    /// </summary>
    readonly record struct Paced(Vector2 StandM, float FacingRad);

    /// <summary>
    /// Where the people stand: <b>one at the end of every shape and the rest spread evenly round the
    /// lap</b>, each clear of the carriageway on the side the lap is driven.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Back from the node rather than at it.</b> A disc is ground no lane is laid over, so a body beside
    /// one is beside two lanes that both end there — and the lane the shape's own pacer steps into has to be
    /// the one that shape is measured on, or a car would be stopped on the wrong side of the boundary the
    /// figures are cut at.
    /// </para>
    /// <para>
    /// <b>Clear of the carriageway by a body's own width.</b> Nearer and a driver's own chains reach the
    /// kerb, and a car would hold off somebody who has not stepped out yet; further and there is no road
    /// beside the pad to be paced at all.
    /// </para>
    /// <para>
    /// <b>The spread ones are pushed along the lap until they stand clear of everybody already placed.</b>
    /// An even spacing lands where it lands, and two bodies a car's length apart are one obstacle rather
    /// than two.
    /// </para>
    /// </remarks>
    static Paced[] PacedFrom(List<ArcSeg>[] lap, SimConfig config)
    {
        var paced = new Paced[Pacers];
        var placedM = new float[Pacers];
        var at = 0;

        var fromM = 0f;
        foreach (var section in Table)
        {
            var lengthM = Spline.TotalLengthM(CollectionsMarshal.AsSpan(lap[section.Road]));
            if (section.IsShape)
            {
                placedM[at] = fromM + MathF.Max(0f, lengthM - ClearOfANodeM);
                paced[at] = Standing(lap, placedM[at], config);
                at++;
            }

            fromM += lengthM;
        }

        // Nobody stands in the middle of the straight. Its figure is the one speed the whole lap builds up
        // to, and a body halfway down it is a car that never reaches it — 67 m/s against the gear's own 75,
        // measured. Every other shape is held to a speed its radius sets, which a stop in the middle of it
        // does not change.
        var lapM = fromM;
        var clearOfM = Spline.TotalLengthM(CollectionsMarshal.AsSpan(lap[Straight]));
        var spread = Pacers - at;
        for (var slot = 0; slot < spread; slot++)
        {
            var alongM = (slot + 0.5f) * lapM / spread;
            while (alongM % lapM < clearOfM || Crowded(placedM, at, alongM, lapM)) alongM += PacersApartM;

            placedM[at] = alongM % lapM;
            paced[at] = Standing(lap, placedM[at], config);
            at++;
        }

        return paced;
    }

    /// <summary>
    /// And where the drunks are put down: <b>in the carriageway rather than beside it</b>, evenly round the
    /// lap and on the side the traffic runs, each facing the way it will reel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The pose is the whole of what makes one a drunk</b> — a body with nowhere to be that was put down
    /// on a lane reels down it, and one put down beside a lane paces across it. Nothing anywhere names
    /// either of them, which is what keeps a scenario map a map rather than a special case in the agents.
    /// </para>
    /// <para>
    /// <b>Clear of where the cars are parked, and of each other.</b> Six cars stand round the same ring, and
    /// a body put down inside one of them is a contact the run begins with; two drunks a car's length apart
    /// are one obstacle rather than two.
    /// </para>
    /// </remarks>
    static Paced[] ReelingFrom(List<ArcSeg>[] lap, SimConfig config)
    {
        var lapM = 0f;
        for (var road = 0; road < Roads; road++) lapM += Spline.TotalLengthM(CollectionsMarshal.AsSpan(lap[road]));

        var placedM = new float[Pacers + Cars];
        for (var car = 0; car < Cars; car++) placedM[car] = ((car + 0.5f) * lapM / Cars) % lapM;

        var reeling = new Paced[Pacers];
        for (var drunk = 0; drunk < Pacers; drunk++)
        {
            var alongM = (drunk + 0.5f) * lapM / Pacers;
            while (Crowded(placedM, Cars + drunk, alongM, lapM)) alongM += PacersApartM;

            placedM[Cars + drunk] = alongM % lapM;

            var on = Standing(lap, placedM[Cars + drunk]);
            var right = Heading.RightOf(on.Direction);
            reeling[drunk] = new Paced(
                on.PositionM + (right * config.LaneOffsetM * config.RoadSideSign), on.HeadingRad);
        }

        return reeling;
    }

    /// <summary>Whether somebody is already standing within <see cref="PacersApartM"/> of here, the lap being a ring.</summary>
    static bool Crowded(float[] placedM, int placed, float alongM, float lapM)
    {
        for (var other = 0; other < placed; other++)
        {
            var apartM = MathF.Abs((alongM % lapM) - placedM[other]);
            if (MathF.Min(apartM, lapM - apartM) < PacersApartM) return true;
        }

        return false;
    }

    /// <summary>One of them, beside the lap at a place along it and facing the road it is about to step into.</summary>
    static Paced Standing(List<ArcSeg>[] lap, float alongM, SimConfig config)
    {
        var on = Standing(lap, alongM);
        var right = Heading.RightOf(on.Direction) * config.RoadSideSign;
        var kerbM = (config.RoadWidthM * 0.5f) + config.PersonDiameterM;
        return new Paced(on.PositionM + (right * kerbM), MathF.Atan2(-right.Y, -right.X));
    }

    /// <summary>
    /// The cars, spread evenly round the lap on their own side of the centreline and facing the way the
    /// road runs, and then the people. Nothing puts a car under way — a spawned car is parked (GEN-7), and
    /// what drives it is the rule that a map with nowhere to be on it drives its own.
    /// </summary>
    /// <remarks>
    /// <b>The cars come first, and their order is the fleet's order</b> — a car takes its look off its own
    /// place in the spawns, so the measured lap's six are two of each drivetrain out of the first three
    /// entries the fleet ships, and the fleet lap's <see cref="FleetCars"/> are one of every look there is.
    /// What differs between the six is drive layout alone; what differs between the sixteen is everything a
    /// variant states.
    /// </remarks>
    static CityPlan.SpawnArrays Spawns(List<ArcSeg>[] lap, Paced[] standing, SimConfig config, int cars)
    {
        var lapM = 0f;
        for (var road = 0; road < Roads; road++) lapM += Spline.TotalLengthM(CollectionsMarshal.AsSpan(lap[road]));

        var kind = new byte[cars + standing.Length];
        var positionM = new Vector2[kind.Length];
        var headingRad = new float[kind.Length];
        for (var car = 0; car < cars; car++)
        {
            var on = Standing(lap, ((car + 0.5f) * lapM / cars) % lapM);
            var right = Heading.RightOf(on.Direction);

            kind[car] = SpawnKindCar;
            positionM[car] = on.PositionM + (right * config.LaneOffsetM * config.RoadSideSign);
            headingRad[car] = on.HeadingRad;
        }

        for (var person = 0; person < standing.Length; person++)
        {
            kind[cars + person] = SpawnKindPerson;
            positionM[cars + person] = standing[person].StandM;
            headingRad[cars + person] = standing[person].FacingRad;
        }

        return new CityPlan.SpawnArrays { Kind = kind, PositionM = positionM, HeadingRad = headingRad };
    }

    /// <summary>
    /// Where along the lap a car stands, <b>pushed clear of the node it would otherwise be parked in</b>:
    /// a disc is ground no lane is laid over, and a car standing on one is off its line before it has moved.
    /// </summary>
    static SplineSample Standing(List<ArcSeg>[] lap, float alongM)
    {
        var fromM = 0f;
        for (var road = 0; road < Roads; road++)
        {
            var chain = CollectionsMarshal.AsSpan(lap[road]);
            var lengthM = Spline.TotalLengthM(chain);
            if (alongM > fromM + lengthM)
            {
                fromM += lengthM;
                continue;
            }

            var intoM = Math.Clamp(alongM - fromM, ClearOfANodeM, MathF.Max(ClearOfANodeM, lengthM - ClearOfANodeM));
            return Spline.SampleAt(chain, intoM);
        }

        return Spline.SampleAt(CollectionsMarshal.AsSpan(lap[Roads - 1]), ClearOfANodeM);
    }

    /// <summary>The spawn kinds the format carries.</summary>
    const byte SpawnKindPerson = 0;

    const byte SpawnKindCar = 1;

    static T[] Filled<T>(int count, T value)
    {
        var filled = new T[count];
        Array.Fill(filled, value);
        return filled;
    }

}
