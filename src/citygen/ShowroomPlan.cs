using System.Numerics;
using TrafficSimulation.CityGen.Map;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>The showroom</b>: one big car park with every car look parked in it once — the fleet's and the service vehicles'
/// — every building look stood once in rows all round it, and a yard of sidings along its foot with every rolling-stock
/// look standing on it once. The map the art and its animations are looked at in: parked, the car picked out putting its
/// lamps through their states (CAR-14.9), driven by hand, and the town it would stand in.
/// </summary>
/// <remarks>
/// <para>
/// <b>The car park is a loop with ranks of bays down both kerbs of its long sides</b> — the idle ring's shape
/// (<see cref="IdlePlan.Loop"/>) drawn out wide — every bay the bay every town lays (GEN-53): a lane wide, square to
/// the kerb, a node a lead off each end. A car spawn stands on every bay, so the town parks each look there
/// (<c>TownWorld.StandCar</c>), and the bays past the last look stand empty. <b>Nobody else is on it</b>: no pavement and
/// nobody on foot, and its cars run no round (CAR-8) — each stands where it was parked until a hand or an order drives it.
/// </para>
/// <para>
/// <b>What it shows is whatever the looks are</b>, which is the run's to say (<c>--fleet</c>, or the shipped list): the
/// plan knows core and nothing else, so it lays <see cref="Slots"/> bays and the town parks the looks in them in order,
/// one each (<see cref="ShowsEachLookOnce"/>). The buildings are the catalogue's, handed down as their footprints
/// (<see cref="BuildingSizes"/>): every ordinary roof, every civic one and every prefab, once.
/// </para>
/// <para>
/// <b>The rows go round the car park ring by ring, the shallowest nearest</b>, each building's door wall on the row's
/// inner edge and facing the car park: a row is as deep as its deepest building, and sorting by depth keeps a row
/// from being one tower and a strip of sheds.
/// </para>
/// <para>
/// <b>The yard is the car park's twin for the rolling stock</b> (<see cref="Yard"/>): it lays <see cref="Sidings"/>
/// straight tracks, and the town stands a train on each, every look once (<c>Trains</c>), coupled and braked in the
/// middle of the map and never sent — each stands until a hand drives it, as the cars do.
/// </para>
/// <para>
/// <b>Laid but not shipped</b>: it measures nothing, so no probe or sweep reads it. It is offered
/// (<see cref="Maps.Offered"/>), on the menu's scenario page and by name.
/// </para>
/// </remarks>
internal static class ShowroomPlan
{
    /// <summary>The map's catalogue name.</summary>
    public const string Name = "Showroom";

    /// <summary>
    /// How many bays a rank holds, end to end from the node at the middle of its side to the corner: the hundred-car
    /// fleet plan with the service looks beside it fits.
    /// </summary>
    const int BaysARank = 16;

    /// <summary>
    /// How many ranks there are: both kerbs of each half of the two long sides, which the loop's nodes cut at their
    /// middles.
    /// </summary>
    const int Ranks = 8;

    /// <summary>How many bays the car park has, which is the most looks it shows.</summary>
    public const int Slots = BaysARank * Ranks;

    /// <summary>The grass between two buildings side by side in a row.</summary>
    const float BuildingGapM = 3f;

    /// <summary>The grass between one row and the next, and between the car park and the first.</summary>
    const float RowGapM = 6f;

    /// <summary>The grass between the outermost row and the edge of the map.</summary>
    const float EdgeM = 20f;

    /// <summary>
    /// How many sidings the yard has, which is the most trains it shows: a train a locomotive look (<c>Trains</c>), and
    /// the rolling stock's four with room for more.
    /// </summary>
    public const int Sidings = 6;

    /// <summary>
    /// Whether the cars on this map are every look once each rather than the fleet's wrap, its service buildings
    /// stand no fleet of their own, its cars run no round and the one picked out shows its lamps in turn (CAR-14.9).
    /// Stated here because the map's name is all a town has to go on.
    /// </summary>
    public static bool ShowsEachLookOnce(string name) => string.Equals(name, Name, StringComparison.Ordinal);

    public static CityPlan Lay(SimConfig config, BuildingSizes sizes)
    {
        var lot = new Lot(config);
        var rows = Rows(Footings(sizes), lot.HalfM + new Vector2(RowGapM), config);

        // The yard along the map's foot, under the rows, its sidings a double line's spacing apart.
        var yardM = config.Rail.BallastWidthM + ((Sidings - 1) * config.Rail.TrackSpacingM) + RowGapM;
        var worldSizeM = new Vector2(
            MathF.Ceiling(2f * (rows.ExtentM.X + EdgeM)), MathF.Ceiling((2f * (rows.ExtentM.Y + EdgeM)) + yardM));
        var centreM = new Vector2(worldSizeM.X * 0.5f, worldSizeM.Y - rows.ExtentM.Y - EdgeM);

        var loop = IdlePlan.Loop(centreM, lot.LoopHalfM, lot.CornerM);
        var bays = Bays(loop, lot, config);
        return new CityPlan
        {
            Seed = 0x73686F77_726F6F6DUL,
            Name = Name,
            WorldSizeM = worldSizeM,

            // No pavement and nobody on foot: the buildings are pictures of themselves, and a kerb laid for nobody is
            // ground the picture would have to explain.
            PavementWidthM = 0f,
            Junctions = Junctions(loop, bays, config),
            JunctionCorners = new CityPlan.JunctionCornerArrays
            {
                CornerM = [], ArcCentreM = [], RadiusM = [], TangentAM = [], TangentBM = [],
            },
            Roads = Roads(loop, bays, config),
            Bridges = new CityPlan.BridgeArrays { Road = [], FromM = [], ToM = [], DeckWidthM = [], PavementWidthM = [] },
            CarParks = bays.CarParks,
            PavedAreas = new CityPlan.PavedAreaArrays { MinM = [centreM - lot.HalfM], SizeM = [2f * lot.HalfM] },
            Crosswalks = new CityPlan.CrosswalkArrays { CentreM = [], Axis = [], DepthM = [], Road = [], Junction = [] },
            ParkingLots = new CityPlan.ParkingLotArrays
            {
                CentreM = [], Axis = [], HalfExtentM = [], SpaceOffsets = [0], SpacePositionM = [], SpaceHeadingRad = [],
            },
            Buildings = rows.Buildings(centreM),
            Props = CityPlan.PropArrays.None,
            Spawns = bays.Spawns,
            Water = CityPlan.WaterArrays.None,
            Rails = RailLines.Lay(Yard(worldSizeM, config), worldSizeM, config),
        };
    }

    /// <summary>
    /// <b>The yard</b>: <see cref="Sidings"/> straight tracks side by side along the map's foot, each a line of its own run
    /// one way and on past the map at both ends as every track is (TRN-7) — so each fields one train, which the town
    /// stands on it rather than sends.
    /// </summary>
    static TownMap.RailArrays Yard(Vector2 worldSizeM, SimConfig config)
    {
        var rail = config.Rail;
        var pointM = new Vector2[2 * Sidings];
        var offsets = new int[Sidings + 1];
        for (var siding = 0; siding < Sidings; siding++)
        {
            var y = EdgeM + (rail.BallastWidthM * 0.5f) + (siding * rail.TrackSpacingM);
            (pointM[2 * siding], pointM[(2 * siding) + 1]) = (new Vector2(-rail.RunOffM, y), new Vector2(worldSizeM.X + rail.RunOffM, y));
            offsets[siding + 1] = 2 * (siding + 1);
        }

        return new TownMap.RailArrays
        {
            Line = [.. Enumerable.Range(0, Sidings)], Runs = [.. Enumerable.Repeat(TownMap.TrackRuns.Forward, Sidings)],
            PointOffsets = offsets, PointM = pointM, Level = new byte[pointM.Length],
        };
    }

    /// <summary>
    /// The car park's measurements, off the bay every town lays: <b>its long sides as long as a rank needs</b>, and its
    /// short ones far enough apart that the far nodes of the two inner ranks meet in the middle and no further.
    /// </summary>
    readonly struct Lot
    {
        public Lot(SimConfig config)
        {
            KerbM = config.RoadWidthM * 0.5f;
            BayLineM = config.CarParkKerbOverlapM + config.CarParkBayDepthM;
            LeadM = config.CityGen.ConnectionStandoffM;
            var farNodeM = KerbM - config.CarParkKerbOverlapM + BayLineM + LeadM;

            // The ring's nodes are a lane's half-width across (IdlePlan), and a rank stops short of one by a zebra.
            NodeRadiusM = config.LaneOffsetM;
            RankFromM = NodeRadiusM + config.CarParkRankClearM;

            var halfY = farNodeM + config.JunctionRadiusM;
            CornerM = halfY * IdlePlan.CornerShare;
            LoopHalfM = new Vector2(CornerM + RankFromM + (BaysARank * config.LaneWidthM), halfY);
            // At the ends, a lane past the kerb: it takes in the shoulder the ground draws round a carriageway.
            HalfM = new Vector2(LoopHalfM.X + KerbM + config.LaneWidthM, halfY + farNodeM + config.JunctionRadiusM);
        }

        /// <summary>From the loop's centre to its centreline, either way.</summary>
        public Vector2 LoopHalfM { get; }

        /// <summary>And to the edge of everything the car park takes, its outer ranks' far nodes included.</summary>
        public Vector2 HalfM { get; }

        public float CornerM { get; }

        public float KerbM { get; }

        /// <summary>A bay's line: a hair back over the street's kerb and a bay deep past it.</summary>
        public float BayLineM { get; }

        public float LeadM { get; }

        public float NodeRadiusM { get; }

        /// <summary>How far from the node a rank's first bay begins.</summary>
        public float RankFromM { get; }
    }

    /// <summary>The bays as their lines, the car parks they make, and a car spawn on each.</summary>
    sealed record Laid(List<ArcSeg> Lines, CityPlan.CarParkArrays CarParks, CityPlan.SpawnArrays Spawns);

    /// <summary>
    /// <b>A rank down each kerb of each half of the two long sides</b>, from a zebra past the node to the corner, and
    /// a car spawn on each bay's middle. The spawns take the ranks in turn, so a showing shorter than the car park
    /// holds is spread round all of it.
    /// </summary>
    static Laid Bays(List<ArcSeg>[] loop, in Lot lot, SimConfig config)
    {
        // The straights the long sides are cut into: the last piece of roads 0 and 2, which end on their node, and the
        // first of roads 1 and 3, which start on it.
        (int Road, ArcSeg Piece, bool NodeAtStart)[] halves =
            [(0, loop[0][^1], false), (1, loop[1][0], true), (2, loop[2][^1], false), (3, loop[3][0], true)];

        var lines = new List<ArcSeg>();
        var bayCentreM = new List<Vector2>();
        var bayHeadingRad = new List<float>();
        var street = new int[halves.Length];
        var atM = new Vector2[halves.Length];
        var bayOffsets = new int[halves.Length + 1];
        var right = new List<bool>();
        for (var half = 0; half < halves.Length; half++)
        {
            var (road, piece, nodeAtStart) = halves[half];
            street[half] = road;
            atM[half] = Spline.SampleAt([piece], piece.LengthM * 0.5f).PositionM;
            for (var bay = 0; bay < BaysARank; bay++)
            {
                var fromNodeM = lot.RankFromM + ((bay + 0.5f) * config.LaneWidthM);
                var at = Spline.SampleAt([piece], nodeAtStart ? fromNodeM : piece.LengthM - fromNodeM);
                foreach (var onTheRight in (ReadOnlySpan<bool>)[true, false])
                {
                    var outward = onTheRight ? at.Right : -at.Right;
                    var line = new ArcSeg(
                        at.PositionM + (outward * (lot.KerbM - config.CarParkKerbOverlapM)), Trig.Atan2(outward.Y, outward.X),
                        lot.BayLineM, 0f);
                    lines.Add(line);
                    right.Add(onTheRight);
                    bayCentreM.Add(line.StartM + (outward * (line.LengthM - (config.CarParkBayLengthM * 0.5f))));
                    bayHeadingRad.Add(line.HeadingRad);
                }
            }

            bayOffsets[half + 1] = lines.Count;
        }

        // Rank by rank, the first bay of each before the second of any.
        var kind = new byte[lines.Count];
        var positionM = new Vector2[lines.Count];
        var headingRad = new float[lines.Count];
        for (var spawn = 0; spawn < lines.Count; spawn++)
        {
            // A rank is one kerb of one half: the lines go half by half, bay by bay, both kerbs of each bay.
            var (rank, inRank) = (spawn % Ranks, spawn / Ranks);
            var line = bayOffsets[rank / 2] + (inRank * 2) + (rank % 2);
            kind[spawn] = IdlePlan.SpawnKindCar;
            positionM[spawn] = bayCentreM[line];
            headingRad[spawn] = bayHeadingRad[line];
        }

        var carParks = new CityPlan.CarParkArrays
        {
            Street = street, AtM = atM, BayOffsets = bayOffsets,
            Road = [.. Enumerable.Range(IdlePlan.Roads, lines.Count)], Right = [.. right],
        };
        return new Laid(lines, carParks, new CityPlan.SpawnArrays { Kind = kind, PositionM = positionM, HeadingRad = headingRad });
    }

    /// <summary>
    /// The loop's four nodes, a lane's half-width across and unlit as the ring's are (<see cref="IdlePlan"/>), and two
    /// a bay at the town's one disc — a lead back over the street and a lead past its far end (GEN-46).
    /// </summary>
    static CityPlan.JunctionArrays Junctions(List<ArcSeg>[] loop, Laid bays, SimConfig config)
    {
        var count = IdlePlan.Roads + (2 * bays.Lines.Count);
        var centreM = new Vector2[count];
        var radiusM = new float[count];
        for (var node = 0; node < IdlePlan.Roads; node++) (centreM[node], radiusM[node]) = (loop[node][0].StartM, config.LaneOffsetM);

        var leadM = config.CityGen.ConnectionStandoffM;
        for (var bay = 0; bay < bays.Lines.Count; bay++)
        {
            var line = bays.Lines[bay];
            var near = IdlePlan.Roads + (2 * bay);
            (centreM[near], radiusM[near]) = (line.StartM - (line.StartUnit * leadM), config.JunctionRadiusM);
            (centreM[near + 1], radiusM[near + 1]) = (line.EndM + (line.StartUnit * leadM), config.JunctionRadiusM);
        }

        return new CityPlan.JunctionArrays { CentreM = centreM, RadiusM = radiusM, Lit = new bool[count], PhaseOffsetS = new float[count] };
    }

    /// <summary>The loop's four roads, road <c>r</c> from node <c>r</c> to node <c>r + 1</c>, then a road a bay between its own two.</summary>
    static CityPlan.RoadArrays Roads(List<ArcSeg>[] loop, Laid bays, SimConfig config)
    {
        var count = IdlePlan.Roads + bays.Lines.Count;
        var from = new int[count];
        var to = new int[count];
        var widthM = new float[count];
        var bay = new bool[count];
        var segments = new List<ArcSeg>();
        var offsets = new List<int> { 0 };
        for (var road = 0; road < count; road++)
        {
            if (road < IdlePlan.Roads)
            {
                (from[road], to[road], widthM[road]) = (road, (road + 1) % IdlePlan.Roads, config.RoadWidthM);
                segments.AddRange(loop[road]);
            }
            else
            {
                var near = IdlePlan.Roads + (2 * (road - IdlePlan.Roads));
                (from[road], to[road], widthM[road], bay[road]) = (near, near + 1, config.LaneWidthM, true);
                segments.Add(bays.Lines[road - IdlePlan.Roads]);
            }

            offsets.Add(segments.Count);
        }

        return new CityPlan.RoadArrays
        {
            FromJunction = from, ToJunction = to, WidthM = widthM, Flow = CityPlan.RoadArrays.AllBothWays(count), Bay = bay,
            SegmentOffsets = [.. offsets], Segments = [.. segments],
        };
    }

    /// <summary>
    /// One building of the catalogue's: its footprint with the door's wall first, what it serves, and which prefab it
    /// is (−1 for a roof matched by its size or its use).
    /// </summary>
    readonly record struct Footing(Vector2 SizeM, BuildingUse Use, int Prefab);

    /// <summary>
    /// <b>Every building look once</b>: the ordinary roofs, matched by footprint; the civic ones, by the use they serve;
    /// and every prefab by its place in the catalogue — the shallowest first.
    /// </summary>
    static List<Footing> Footings(BuildingSizes sizes)
    {
        var footings = new List<Footing>();
        foreach (var sizeM in sizes.OrdinaryM) footings.Add(new Footing(sizeM, BuildingUse.Ordinary, -1));
        foreach (var use in (ReadOnlySpan<BuildingUse>)[BuildingUse.Hospital, BuildingUse.PoliceStation, BuildingUse.Depot])
        {
            if (sizes.Of(use) != Vector2.Zero) footings.Add(new Footing(sizes.Of(use), use, -1));
        }

        for (var prefab = 0; prefab < sizes.PrefabM.Length; prefab++) footings.Add(new Footing(sizes.PrefabM[prefab], BuildingUse.Ordinary, prefab));

        // Stable, so buildings as deep as each other keep the catalogue's order and a look's prefabs stand together.
        return [.. footings.OrderBy(footing => footing.SizeM.Y)];
    }

    /// <summary>The buildings as stood about the origin, and how far from it the outermost reaches either way.</summary>
    sealed class Stood
    {
        public readonly List<(Footing Footing, Vector2 CentreM, float HeadingRad, Vector2 DoorM)> All = [];

        public Vector2 ExtentM { get; set; }

        public CityPlan.BuildingArrays Buildings(Vector2 centreM) => new()
        {
            CentreM = [.. All.Select(stood => centreM + stood.CentreM)],
            SizeM = [.. All.Select(stood => stood.Footing.SizeM)],
            HeadingRad = [.. All.Select(stood => stood.HeadingRad)],
            Capacity = [.. All.Select(_ => 1)],
            Use = [.. All.Select(stood => stood.Footing.Use)],
            EntryOffsets = [.. Enumerable.Range(0, All.Count + 1)],
            EntryPointM = [.. All.Select(stood => centreM + stood.DoorM)],
            Prefab = [.. All.Select(stood => stood.Footing.Prefab)],
        };
    }

    /// <summary>
    /// <b>Row after row round the car park</b>, from <paramref name="innerHalfM"/> out: a ring is a row along each of its
    /// four sides, the north and south rows as long as the ring is wide and the east and west ones as tall, so no two
    /// of them share a corner; the next ring stands a row's depth and a gap further out on each side.
    /// </summary>
    static Stood Rows(List<Footing> footings, Vector2 innerHalfM, SimConfig config)
    {
        var stood = new Stood();
        var (min, max) = (-innerHalfM, innerHalfM);
        var next = 0;
        while (next < footings.Count)
        {
            var placedBefore = next;
            var north = Row(footings, ref next, new Vector2(min.X, max.Y), Vector2.UnitX, Vector2.UnitY, max.X - min.X, stood, config);
            var south = Row(footings, ref next, new Vector2(min.X, min.Y), Vector2.UnitX, -Vector2.UnitY, max.X - min.X, stood, config);
            var east = Row(footings, ref next, new Vector2(max.X, min.Y), Vector2.UnitY, Vector2.UnitX, max.Y - min.Y, stood, config);
            var west = Row(footings, ref next, new Vector2(min.X, min.Y), Vector2.UnitY, -Vector2.UnitX, max.Y - min.Y, stood, config);

            // A ring that stood nothing is a building longer than every side of it; the next ring is longer.
            var grownM = next == placedBefore ? footings[next].SizeM.X : 0f;
            max += new Vector2(MathF.Max(east, grownM), MathF.Max(north, grownM)) + new Vector2(RowGapM);
            min -= new Vector2(MathF.Max(west, grownM), MathF.Max(south, grownM)) + new Vector2(RowGapM);
        }

        stood.ExtentM = Vector2.Max(max, -min);
        return stood;
    }

    /// <summary>
    /// One row: buildings stood along <paramref name="along"/> from <paramref name="startM"/>, each with its door's wall
    /// on the row's inner edge and its depth running <paramref name="outward"/>, for as long as the next one fits. How
    /// deep the row came out.
    /// </summary>
    static float Row(
        List<Footing> footings, ref int next, Vector2 startM, Vector2 along, Vector2 outward, float lengthM, Stood stood, SimConfig config)
    {
        var (cursorM, depthM) = (0f, 0f);
        var headingRad = Trig.Atan2(along.Y, along.X);
        while (next < footings.Count && cursorM + footings[next].SizeM.X <= lengthM)
        {
            var footing = footings[next++];
            var wallM = startM + (along * (cursorM + (footing.SizeM.X * 0.5f)));
            stood.All.Add((footing, wallM + (outward * (footing.SizeM.Y * 0.5f)), headingRad, wallM - (outward * config.Building.FrontGapM)));
            cursorM += footing.SizeM.X + BuildingGapM;
            depthM = MathF.Max(depthM, footing.SizeM.Y);
        }

        return depthM;
    }
}
