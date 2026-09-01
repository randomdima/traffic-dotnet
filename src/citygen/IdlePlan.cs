using System.Numerics;
using System.Runtime.InteropServices;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Which part a car on the idle ring plays</b>, off its own place in the spawns. It is what the town
/// dresses it in, and it is a part rather than a look because a plan knows core and nothing else — the
/// fleet and the service list are files the agents' slice owns.
/// </summary>
internal enum IdlePart : byte
{
    /// <summary>The escort, in the police paint: one ahead of what it is escorting and one behind.</summary>
    Police,

    /// <summary>And what they are escorting, in the heaviest look the fleet ships.</summary>
    Armoured,

    /// <summary>The one car the ring carries besides them, in the quickest look, running the other way round.</summary>
    Sports,
}

/// <summary>
/// <b>The idle ring</b>: one loop of road with nothing else on it — a square with rounded corners — an
/// escorted convoy running it one way, an armoured car between two police with beacons up, and a single
/// sports car running it the other.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is laid to be looked at rather than to measure anything.</b> Every other scenario map answers a
/// question (<see cref="TrackPlan"/>, <see cref="ExamPlan"/>, <see cref="SkidpadPlan"/>); this one is the
/// picture the game idles on, so what it is chosen for is that it never stops being worth watching and
/// never needs anybody's attention — a closed loop, traffic in both directions, and nothing on it that can
/// end.
/// </para>
/// <para>
/// <b>Nothing drives it that is not already in the town.</b> There is no building and no bay, so the rule
/// that a map with nowhere to be on it drives its own cars (<c>TownWorld.DriveTheEmptyMap</c>) puts every
/// car on the lane under it and the ordinary catalogue does the rest: the ring is a tour with no end, and
/// what a car does on it — the gap it keeps, the speed the radius affords it, getting past what it catches
/// — is the same driver a city gets.
/// </para>
/// <para>
/// <b>The whole circuit is on screen</b> (<see cref="RadiusM"/>), which is what makes it a picture rather
/// than a stretch of road with something on it now and then: every car is in the frame the whole time,
/// and the two files meet head to head twice a lap.
/// </para>
/// <para>
/// <b>What is escorted is the heaviest look the fleet ships</b> and the police either side of it are among
/// the quickest, so the escort is <em>held</em> to the pace of what it is escorting
/// (<see cref="EscortPaceShare"/>) — which is what an escort is, and what makes three cars read as one
/// thing rather than three that happen to be in a row.
/// </para>
/// <para>
/// <b>The two files are the two lanes of one road</b>, and never two roads. What is worth looking at is
/// traffic passing traffic; what makes it worth trusting is that both are driving the ordinary lanes of an
/// ordinary carriageway, so a car on the wrong side of the centreline is a fault and not a staging.
/// </para>
/// <para>
/// <b>One car comes the other way and not a second convoy.</b> Two convoys read as a staging — the same
/// thing twice, laid out to be symmetric — where a single quick car passing a slow escort is the plainest
/// picture of traffic there is, and the one thing on this ring whose closing speed changes lap to lap.
/// </para>
/// <para>
/// <b>The field in the middle of it is rectangular because what stands in it is</b>: the start menu opens
/// over this map (<c>GEN-1b</c>) and a panel is a rectangle, so a circle spends most of the ground it
/// encloses on corners a panel cannot reach into. Rounding a square instead leaves the middle of the field
/// as wide as the field is, and the corners are what keeps it a road somebody drives round rather than a
/// track with four right angles on it.
/// </para>
/// <para>
/// <b>It is cut into four roads because a road runs between two named junctions</b> (TER-4) and a loop has
/// no end. One road a side is the fewest that leaves no pair of nodes joined twice, and <b>the cuts are at
/// the middle of each straight</b> — half a side, a corner, half the next — so every node stands on straight
/// road. Each is no wider than a lane's own half-width: nothing meets there, nothing is lit, and there is
/// nothing to give way to.
/// </para>
/// </remarks>
internal static class IdlePlan
{
    /// <summary>The map's catalogue name, which is also its file's.</summary>
    public const string Name = "Idle";

    /// <summary>
    /// Whether the cars on this map are the ring's own rather than the fleet's wrap. It is stated here
    /// because the map's name is all a town has to go on when it stands its cars up.
    /// </summary>
    public static bool StandsConvoys(string name) => string.Equals(name, Name, StringComparison.Ordinal);

    /// <summary>
    /// Which part a car plays, off its own place in the spawns: <b>the middle car of the convoy is the one
    /// being escorted</b>, the two either side of it are the escort, and the car past them is on its own.
    /// </summary>
    public static IdlePart PartOf(int car) => car switch
    {
        Escorted => IdlePart.Armoured,
        LooseCar => IdlePart.Sports,
        _ => IdlePart.Police,
    };

    /// <summary>A metre a cell, as the other laid maps use.</summary>
    const float CellSizeM = 1f;

    /// <summary>
    /// How much ground is laid round the ring, in views a run opens on. <b>Enough that a wide window is
    /// still looking at map</b>: the opening view is a figure across the <em>short</em> side (OBS-1b), so a
    /// window twice as wide as it is tall shows a screen's worth of ground either side of the circuit.
    /// </summary>
    const float MarginInViews = 2f;

    /// <summary>
    /// How much grass stands between the road and the edge of the view a run opens on. <b>A road's own
    /// width of it</b>: a carriageway is drawn with a shoulder either side that the plan lays no cell for,
    /// and a ring laid up against the edge of the frame has that shoulder clipped by it.
    /// </summary>
    static float InsideTheViewM(SimConfig config) => config.RoadWidthM;

    /// <summary>
    /// Half the ring's own extent across either axis, centreline to centre: <b>as wide as the view a run
    /// opens on will hold</b> (<see cref="ViewFigures.CameraDefaultViewM"/>, across the short side, less the
    /// road's own width and a little grass).
    /// </summary>
    /// <remarks>
    /// <b>The whole circuit is on screen or the map is not doing its job.</b> What this map is for is being
    /// looked at, and a ring wider than the window is a picture of an empty stretch of road for the twenty
    /// seconds between one car passing and the next. The loop is square-on rather than wider than it is
    /// tall because the view is a figure across the <em>short</em> side only (OBS-1b): a loop laid to a wide
    /// window is a loop the same build clips on a square one.
    /// </remarks>
    public static float HalfSideM(SimConfig config) =>
        (config.View.CameraDefaultViewM * 0.5f) - (config.RoadWidthM * 0.5f) - InsideTheViewM(config);

    /// <summary>
    /// How much of <see cref="HalfSideM"/> a corner is. <b>What it trades is the field against the corner
    /// speed</b>: rounded further, the panel standing in the middle runs out of straight side to sit
    /// against; rounded less, the corner is a hairpin the convoy crawls through, because on a loop this
    /// size it is the radius and not the driver that sets the speed.
    /// </summary>
    public const float CornerShare = 0.4f;

    /// <summary>
    /// The radius of each of the four corners, which is <b>the tightest thing on the ring</b> and so what
    /// the escort's pace is held against (<see cref="EscortPaceShare"/>).
    /// </summary>
    public static float CornerRadiusM(SimConfig config) => HalfSideM(config) * CornerShare;

    /// <summary>How long each straight half-side is, from the node at its middle to where the corner starts.</summary>
    static float HalfStraightM(SimConfig config) => HalfSideM(config) - CornerRadiusM(config);

    /// <summary>How many roads the ring is cut into: one a side, so no two nodes are joined twice.</summary>
    public const int Roads = 4;

    /// <summary>An escorted car and the police ahead of and behind it.</summary>
    public const int ConvoyCars = 3;

    /// <summary>Which car of the convoy is the escorted one, which is the middle of it.</summary>
    public const int Escorted = 1;

    /// <summary>And the one car that is not in it, which runs the ring the other way round on its own.</summary>
    const int LooseCar = ConvoyCars;

    public const int Cars = ConvoyCars + 1;

    /// <summary>
    /// What the escort is held to, as a share of the pace the escorted car keeps on the ring's <em>tightest
    /// corner</em> (<see cref="CornerRadiusM"/>). <b>An escort that can outrun what it is escorting is three
    /// cars in a row rather than a convoy</b>: held a little under it, the leading car is caught by its
    /// charge and the trailing one by the same gap it already keeps, so nothing has to be told to stay
    /// together and nothing has to be staged.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the corner and not the straight that the share is read against</b>, so it is close to one:
    /// the escorted car can do this speed everywhere on the loop and the corner is where it has the least
    /// margin over the escort, which is where a convoy comes apart if it is going to. Read against a
    /// straight instead, the same margin would be a leading car the charge could not catch on the bends.
    /// </para>
    /// <para>
    /// <b>It is also half of what closes the convoy up</b>, with <see cref="ConvoyFollowingShare"/> the
    /// other half: the road a follower is granted is the road it needs to stop in, so a slower convoy is a
    /// tighter one. The two together run the three at about half the spacing ordinary traffic keeps.
    /// </para>
    /// </remarks>
    public const float EscortPaceShare = 0.9f;

    /// <summary>
    /// And how much of the ordinary following interval the convoy keeps (<c>CarFleet.FollowingShare</c>) —
    /// <b>half of it, because a convoy is one thing and not three</b>. Left at what traffic keeps, the three
    /// run at a spacing anybody would read as three cars that happen to be on the same road.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the interval and never the stopping distance.</b> What it gives up is the second of travel
    /// a driver leaves on top of the road it needs; every corner, every stop point and the ground the car
    /// in front has yet to vacate are untouched, so a convoy running close is still a convoy that can stop.
    /// </para>
    /// <para>
    /// <b>A quarter was too little and the convoy stopped for itself.</b> The interval is also the slack a
    /// follower has when the car in front reaches its own corner speed a tick before it does; cut to a
    /// quarter, the escorted car ran out of granted road on the bends and came to a standstill behind an
    /// escort that was still moving. Half of it keeps the three reading as one and leaves that slack.
    /// </para>
    /// </remarks>
    public const float ConvoyFollowingShare = 0.5f;

    /// <summary>
    /// How far apart the cars of the convoy are put down. <b>Not nearer than the gap they will drive at</b>:
    /// a car standing closer to the one in front than the road would grant it is a car held at a standstill
    /// until that one has pulled away — which is the first thing anybody sees, because the ring is what the
    /// start menu opens over. Standing further apart than they settle at only costs the first few seconds.
    /// </summary>
    const float ConvoyGapM = 12f;

    /// <summary>
    /// How near a node a car may stand before it is on ground the lane was cut back off. A car standing on
    /// a node is off its line before it has moved. <b>The node is a disc a lane's half-width across</b> and
    /// nothing meets at it, so what this has to clear is that disc and half a car — not the room a junction
    /// somewhere else would want.
    /// </summary>
    const float ClearOfANodeM = 6f;

    /// <summary>How far round one lap of the centreline is: four sides and the four corners between them.</summary>
    public static float RingM(SimConfig config) =>
        (8f * HalfStraightM(config)) + (2f * MathF.PI * CornerRadiusM(config));

    /// <summary>
    /// How big the map is: the ring, the road either side of it and the margin, rounded out to whole cells.
    /// <b>Square and centred</b>, so the middle of the map is the middle of the ring.
    /// </summary>
    public static Vector2 WorldSizeM(SimConfig config)
    {
        var edgeM = (config.RoadWidthM * 0.5f) + CellSizeM;
        var marginM = config.View.CameraDefaultViewM * MarginInViews;
        var sideM = MathF.Ceiling(2f * (HalfSideM(config) + edgeM + marginM) / CellSizeM) * CellSizeM;
        return new Vector2(sideM, sideM);
    }

    /// <summary>Where the ring is centred, which the watch reads to say which side of the centreline a car is on.</summary>
    public static Vector2 CentreM(SimConfig config) => WorldSizeM(config) * 0.5f;

    /// <summary>
    /// The ring, road by road, in the order it is driven — laid where it stands on the map, so what a test
    /// walks is what a car drives. It runs anticlockwise, which puts the file driving with the road on the
    /// outside of the loop and the one driving against it on the inside.
    /// </summary>
    /// <remarks>
    /// It starts at the middle of the side to the map's right and each road is half a straight, a corner
    /// and half the next straight — so the four are the same length and a quarter of the lap is a road.
    /// </remarks>
    public static List<ArcSeg>[] Ring(SimConfig config)
    {
        var halfStraightM = HalfStraightM(config);
        var cornerM = CornerRadiusM(config);
        var at = new Cursor
        {
            AtM = CentreM(config) + new Vector2(HalfSideM(config), 0f),
            HeadingRad = MathF.PI * 0.5f,
        };

        var ring = new List<ArcSeg>[Roads];
        for (var road = 0; road < Roads; road++)
        {
            ring[road] =
            [
                Piece(ref at, halfStraightM, 0f),
                Piece(ref at, cornerM * MathF.PI * 0.5f, 1f / cornerM),
                Piece(ref at, halfStraightM, 0f),
            ];
        }

        return ring;
    }

    public static CityPlan Lay(SimConfig config)
    {
        var worldSizeM = WorldSizeM(config);
        var gridWidth = (int)MathF.Round(worldSizeM.X / CellSizeM);
        var gridHeight = (int)MathF.Round(worldSizeM.Y / CellSizeM);
        var cells = new Ground[gridWidth * gridHeight];
        var laneDirs = new sbyte[cells.Length * 2];
        var widthM = config.RoadWidthM;

        var ring = Ring(config);
        var segments = new List<ArcSeg>();
        var offsets = new List<int> { 0 };
        var nodeM = new Vector2[Roads];
        for (var road = 0; road < Roads; road++)
        {
            nodeM[road] = ring[road][0].StartM;
            segments.AddRange(ring[road]);
            offsets.Add(segments.Count);
        }

        var painter = new GroundPainter(cells, laneDirs, gridWidth, gridHeight, CellSizeM, config.RoadSideSign);
        for (var road = 0; road < Roads; road++)
        {
            painter.Road(CollectionsMarshal.AsSpan(segments)[offsets[road]..offsets[road + 1]], widthM);
        }

        return new CityPlan
        {
            Seed = 0x69646C65_63686173UL,
            Name = Name,
            WorldSizeM = worldSizeM,
            CellSizeM = CellSizeM,

            // No pavement, and so no walking network: there is nobody on this map, and a kerb laid for
            // nobody is ground the picture would have to explain.
            PavementWidthM = 0f,
            GridWidth = gridWidth,
            GridHeight = gridHeight,
            Cells = cells,
            LaneDirs = laneDirs,
            Junctions = Nodes(nodeM, config),

            // No paint and no light anywhere on the ring: nothing meets at a node here, so a bar or a
            // signal would be a second thing refusing a movement nothing refuses (SIM-7).
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
            Spawns = Spawns(ring, config),
            Water = CityPlan.WaterArrays.None,
        };
    }

    /// <summary>Where a car stands along the ring: the escorted car on its own road, and the police either side of it.</summary>
    /// <remarks>
    /// The convoy stands in the middle of the first road — what it escorts on that middle, a police car a
    /// gap ahead of it and another a gap behind — so the whole of it is clear of the nodes at either end,
    /// and the car coming the other way starts half a ring from it. The middle of a road is the middle of a
    /// corner, which is the furthest a car can stand from both of the nodes it runs between.
    /// </remarks>
    public static float StandsAtM(SimConfig config, int car)
    {
        var ringM = RingM(config);
        var quarterM = ringM / Roads;
        var middleM = ((WithTheRoad(car) ? 0 : Roads / 2) + 0.5f) * quarterM;
        var backM = WithTheRoad(car) ? (Escorted - car) * ConvoyGapM : 0f;
        var alongM = middleM + backM;
        return alongM - (MathF.Floor(alongM / ringM) * ringM);
    }

    /// <summary>Whether this car drives the ring the way it was laid, which is what decides its lane and its heading.</summary>
    public static bool WithTheRoad(int car) => car < ConvoyCars;

    /// <summary>Where the next piece of the ring starts, carried along as the loop is laid.</summary>
    struct Cursor
    {
        public Vector2 AtM;

        public float HeadingRad;
    }

    /// <summary>One piece laid from where the last one ended — a straight at zero curvature, and a corner
    /// turned to the left so the ring runs anticlockwise.</summary>
    static ArcSeg Piece(ref Cursor at, float lengthM, float curvature)
    {
        var piece = new ArcSeg(at.AtM, at.HeadingRad, lengthM, curvature);
        at.AtM = piece.EndM;
        at.HeadingRad = piece.HeadingAtRad(piece.LengthM);
        return piece;
    }

    /// <summary>
    /// A road end names a junction on every map there is. The ring's nodes are laid one to a road, in road
    /// order, so road <c>r</c> runs from node <c>r</c> to node <c>r + 1</c> and the last arrives back at
    /// the first.
    /// </summary>
    static int[] Ends(bool first)
    {
        var ends = new int[Roads];
        for (var road = 0; road < Roads; road++) ends[road] = (road + (first ? 0 : 1)) % Roads;

        return ends;
    }

    /// <summary>
    /// The nodes the ring is cut at, no wider than a lane's own half-width: a disc is ground no lane is
    /// laid over, and every metre of it is a metre of ring nothing drives.
    /// </summary>
    static CityPlan.JunctionArrays Nodes(Vector2[] nodeM, SimConfig config) => new()
    {
        CentreM = nodeM,
        RadiusM = Filled(Roads, config.LaneOffsetM),
        Lit = new bool[Roads],
        PhaseOffsetS = new float[Roads],
    };

    /// <summary>
    /// The cars, each on its own side of the centreline and facing the way that side is driven.
    /// Nothing puts one under way — a spawned car is parked (GEN-7), and what drives it is the rule that a
    /// map with nowhere to be on it drives its own.
    /// </summary>
    static CityPlan.SpawnArrays Spawns(List<ArcSeg>[] ring, SimConfig config)
    {
        var kind = new byte[Cars];
        var positionM = new Vector2[Cars];
        var headingRad = new float[Cars];

        for (var car = 0; car < Cars; car++)
        {
            var alongM = StandsAtM(config, car);
            ClearOfTheNodes(config, alongM, car);

            var on = Standing(ring, alongM);
            var forward = WithTheRoad(car) ? on.Direction : -on.Direction;
            var right = Heading.RightOf(forward);

            kind[car] = SpawnKindCar;
            positionM[car] = on.PositionM + (right * config.LaneOffsetM * config.RoadSideSign);
            headingRad[car] = MathF.Atan2(forward.Y, forward.X);
        }

        return new CityPlan.SpawnArrays { Kind = kind, PositionM = positionM, HeadingRad = headingRad };
    }

    /// <summary>
    /// A convoy that has grown until one of its cars stands on a node is a map laid against figures the
    /// ring cannot hold, and it fails here rather than on the road.
    /// </summary>
    static void ClearOfTheNodes(SimConfig config, float alongM, int car)
    {
        var quarterM = RingM(config) / Roads;
        var fromNodeM = alongM - (MathF.Floor(alongM / quarterM) * quarterM);
        if (fromNodeM >= ClearOfANodeM && quarterM - fromNodeM >= ClearOfANodeM) return;

        throw new InvalidOperationException(
            $"The {Name} ring cannot stand car {car}: it comes to {fromNodeM:F1} m into its own quarter, "
            + $"which is inside the {ClearOfANodeM:F0} m a car has to stand clear of a node by.");
    }

    /// <summary>Where along the ring a point is, road by road.</summary>
    static SplineSample Standing(List<ArcSeg>[] ring, float alongM)
    {
        var fromM = 0f;
        for (var road = 0; road < Roads; road++)
        {
            var chain = CollectionsMarshal.AsSpan(ring[road]);
            var lengthM = Spline.TotalLengthM(chain);
            if (alongM > fromM + lengthM)
            {
                fromM += lengthM;
                continue;
            }

            return Spline.SampleAt(chain, alongM - fromM);
        }

        var last = CollectionsMarshal.AsSpan(ring[Roads - 1]);
        return Spline.SampleAt(last, Spline.TotalLengthM(last));
    }

    /// <summary>The spawn kinds the format carries.</summary>
    const byte SpawnKindCar = 1;

    static T[] Filled<T>(int count, T value)
    {
        var filled = new T[count];
        Array.Fill(filled, value);
        return filled;
    }
}
