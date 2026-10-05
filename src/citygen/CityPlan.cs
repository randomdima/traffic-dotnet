using System.Numerics;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// One complete city as pure data: no node references and no behaviour. A generator emits it, a builder
/// stands the world up from it, and a validator judges it.
/// </summary>
/// <remarks>
/// <para>
/// Structure of arrays, laid once: one array per field, and a variable-length run — a road's segments, a
/// lot's spaces, a building's ways in, a water outline's points — is a flat array with an offsets array
/// beside it, so a plan of a whole city is a few dozen allocations and no per-record object.
/// </para>
/// <para>
/// <b>Every field here is a shape, and there is no raster among them.</b> What the ground is at a point is
/// solved against these shapes (<see cref="GroundShapes"/>) rather than looked up in a
/// classification laid beside them, so there is one geometry and the question of whether the two agree
/// cannot be asked (TER-7).
/// </para>
/// </remarks>
internal sealed class CityPlan
{
    /// <summary>
    /// What a reference to another record holds when there is no such record — a crossing struck
    /// mid-block belongs to no junction, and on a scenario map every crossing is one of those.
    /// </summary>
    public const int NoRecord = -1;

    public required ulong Seed { get; init; }

    /// <summary>The map's catalogue name. A generated town has none and is exported by hand with its seed.</summary>
    public required string Name { get; init; }

    public required Vector2 WorldSizeM { get; init; }

    /// <summary>0 for a map laid without a pavement.</summary>
    public required float PavementWidthM { get; init; }

    public required JunctionArrays Junctions { get; init; }

    /// <summary>
    /// Kerb fillets, carried because they cannot be read back off any other shape. <b>Nothing lays one
    /// yet</b>: a corner is turned on the town's own boundary now, at the radius the junction was laid at
    /// and once for every distance struck off it (<see cref="LaneShell"/>, TER-5), and the fillets
    /// come back with the kerb ([the known gaps](../../docs/index.md#known-gaps)).
    /// </summary>
    public required JunctionCornerArrays JunctionCorners { get; init; }

    public required RoadArrays Roads { get; init; }

    public required BridgeArrays Bridges { get; init; }

    /// <summary>
    /// <b>The town's roundabouts, as which of its roads each one circulates on</b> (GEN-19). A roundabout is
    /// a ring of ordinary junctions joined by one-way arcs, so this says which those are and nothing else:
    /// there is no roundabout geometry, no roundabout junction and no rule downstream about one.
    /// </summary>
    public RoundaboutArrays Roundabouts { get; init; } = RoundaboutArrays.None;

    /// <summary>
    /// <b>The town's car parks, as the street each stands off and its bays</b> (GEN-53). A bay is a short road
    /// of its own joined to nothing, so this says which roads those are and which street they front — there is
    /// no car park geometry beyond the bays' own lines.
    /// </summary>
    public CarParkArrays CarParks { get; init; } = CarParkArrays.None;

    public required PavedAreaArrays PavedAreas { get; init; }

    public required CrosswalkArrays Crosswalks { get; init; }

    /// <summary>
    /// <b>Whether a zebra is painted at every station the town's kerb ends cut</b> (TER-6, WLK-10), or the town
    /// says where its zebras are (<see cref="Crosswalks"/>) and a station is a place the walk is cut and the
    /// traffic held at, painted with nothing. A traced map's are where its survey maps them (GEN-57).
    /// </summary>
    public bool ZebraAtEveryStation { get; init; } = true;

    public required ParkingLotArrays ParkingLots { get; init; }

    public required BuildingArrays Buildings { get; init; }


    public required PropArrays Props { get; init; }

    public required SpawnArrays Spawns { get; init; }

    public required WaterArrays Water { get; init; }

    /// <summary>The districts the town was laid in (GEN-56), which a service's beat is kept to (SRV-5).</summary>
    public DistrictWheel Districts { get; init; } = DistrictWheel.Whole;

    /// <summary>
    /// The shapes the ground is cut from, as the one bundle every reading of the town's surface takes
    /// (<see cref="GroundShapes"/>).
    /// </summary>
    public GroundPieces Ground => new(
        Seed, WorldSizeM, PavementWidthM, Roads, Bridges, Junctions, JunctionCorners, Roundabouts,
        ParkingLots, PavedAreas, Crosswalks, Water);

    Paving? _paving;

    /// <summary>
    /// <b>The pavement the generator already laid on its way to this plan</b>, where there was one. A town
    /// read from a file has none and lays it on the first ask; a generated one hands over the laying its own
    /// stages stood on, so the lanes, the movements and the ground are drawn once for the town's whole life.
    /// </summary>
    /// <remarks>
    /// <b>It is the same shapes or it is a defect</b>: what is handed over was laid off the pieces this plan
    /// carries, which is why nothing after the roads may add driven ground (<c>TownGenerator</c>).
    /// </remarks>
    public Paving? PavingLaidWithIt { get; init; }

    /// <summary>
    /// <b>What laying this plan cost, stage by stage and in the order they ran</b> (<c>--bench load</c>). A
    /// generated town times its own stages, so the reading is the lay's and not a probe's second run of it; a
    /// map laid in code has none.
    /// </summary>
    public (string Stage, double Ms)[] LaidMs { get; init; } = [];

    /// <summary>
    /// <b>The pavement of this plan, laid once</b> (<see cref="Paving"/>). The ground answers off it, the
    /// mesh draws off it, the road graph reads its lines and the walking graph is cut from it, and a finished
    /// plan does not change — so all of them share one construction rather than each wrapping the tarmac
    /// again.
    /// </summary>
    public Paving Paving(SimConfig config) => _paving ??= PavingLaidWithIt ?? CityGen.Paving.Lay(Ground, config);

    /// <summary>
    /// How far a zebra reaches across the road, kerb to kerb: <b>the width of the road it is painted on,
    /// measured along the paint's own axis</b> (TER-6). A crossing laid off square is longer by exactly what
    /// the skew costs it, and one laid square across its road is the carriageway's width.
    /// </summary>
    /// <remarks>
    /// <b>It is solved and never carried.</b> A span beside the road's own width is a second answer to a
    /// question the road has already answered, and the two disagree the moment either is laid again. The
    /// projection makes this a build-time question: whoever asks it every tick keeps the answer.
    /// </remarks>
    public float CrossingSpanM(int crossing)
    {
        var road = Crosswalks.Road[crossing];
        var arcs = Roads.SegmentsOf(road);
        var lengthM = Spline.TotalLengthM(arcs);
        var centreM = Crosswalks.CentreM[crossing];
        var at = Spline.SampleAt(arcs, Spline.ProjectM(arcs, centreM, lengthM * 0.5f, lengthM));

        var axis = Crosswalks.Axis[crossing];
        var alongItsRoad = axis.LengthSquared() > 0f
            ? MathF.Abs(Vector2.Dot(at.Direction, Vector2.Normalize(axis)))
            : 1f;

        return Roads.WidthM[road] / MathF.Max(alongItsRoad, LeastAlongItsRoad);
    }

    /// <summary>
    /// How square to its road a crossing is held while its span is solved — an eighth of a turn off, by
    /// which the skew has already made the paint half again as long as the road is wide. Past that the axis
    /// is not that road's, and the span would run away rather than reach the far kerb.
    /// </summary>
    const float LeastAlongItsRoad = 0.7071068f;

    internal sealed class JunctionArrays
    {
        public required Vector2[] CentreM { get; init; }
        public required float[] RadiusM { get; init; }
        public required bool[] Lit { get; init; }
        public required float[] PhaseOffsetS { get; init; }

        /// <summary>
        /// <b>Whether its one road runs on off the map here</b> (GEN-2b): a road's end on the map's edge, with no box
        /// round it — its lanes run up to it, and the ground is laid on past the edge and cut there
        /// (<see cref="LaneShell"/>). Empty in a town none of whose roads leaves the map.
        /// </summary>
        public bool[] RunsOffTheMap { get; init; } = [];

        public int Count => CentreM.Length;

        public bool RunsOff(int junction) => (uint)junction < (uint)RunsOffTheMap.Length && RunsOffTheMap[junction];
    }

    internal sealed class JunctionCornerArrays
    {
        public required Vector2[] CornerM { get; init; }
        public required Vector2[] ArcCentreM { get; init; }
        public required float[] RadiusM { get; init; }
        public required Vector2[] TangentAM { get; init; }
        public required Vector2[] TangentBM { get; init; }
        public int Count => CornerM.Length;
    }

    /// <summary>
    /// A road is carried as its <em>curve</em>. Anything that draws uses the arcs; a consumer that
    /// wants a polyline samples them itself, at a quarter-metre tolerance.
    /// </summary>
    internal sealed class RoadArrays
    {
        public required int[] FromJunction { get; init; }
        public required int[] ToJunction { get; init; }
        public required float[] WidthM { get; init; }

        /// <summary>Which way each road is driven (TER-4d).</summary>
        public required RoadFlow[] Flow { get; init; }

        /// <summary>
        /// <b>How many lanes each road is driven in each way</b> (TER-4d): a surveyed carriageway's own count.
        /// <b>Empty where every road is one lane each way it is driven</b>, which is every town the generator
        /// lays (GEN-15); where it is filled, a way <see cref="Flow"/> does not drive has none.
        /// </summary>
        public RoadLanes[] Lanes { get; init; } = [];

        /// <summary>The lanes a road is driven in from its <c>From</c> junction towards its <c>To</c>.</summary>
        public int LanesWithTheRoad(int road) =>
            Lanes.Length > 0 ? Lanes[road].With : Flow[road] != RoadFlow.AgainstTheRoad ? 1 : 0;

        /// <summary>And the other way.</summary>
        public int LanesAgainstTheRoad(int road) =>
            Lanes.Length > 0 ? Lanes[road].Against : Flow[road] != RoadFlow.WithTheRoad ? 1 : 0;

        /// <summary>How many lanes a road's own width carries, both ways together.</summary>
        public int LanesOn(int road) => LanesWithTheRoad(road) + LanesAgainstTheRoad(road);

        /// <summary>
        /// <b>Which roads are a car park's bays</b> (GEN-53), each the space itself and joined to nothing.
        /// <b>Empty where the town lays none.</b>
        /// </summary>
        public bool[] Bay { get; init; } = [];

        /// <inheritdoc cref="Bay"/>
        public bool IsABay(int road) => Bay.Length > 0 && Bay[road];

        /// <summary>
        /// <b>Whether a road is driven both ways over one line</b> — a car's width of ground driven in over
        /// and driven back out over (GEN-4f), so its two lanes are its own line rather than two halves of a
        /// carriageway: a bay, and nothing else.
        /// </summary>
        public bool DrivenOverOneLine(int road) => IsABay(road);

        /// <summary>
        /// <b>The roadside each road carries inside its kerbs</b> (GEN-57): a strip of carriageway between the kerb
        /// the traffic with the road keeps to and its lanes, and between the other kerb and the lanes against it —
        /// laid as a lane joined to nothing (<see cref="LaneLines.IsRoadside"/>), painted off from the lanes, and
        /// driven by nobody. <b>Empty where no road has one</b>, which is every town the generator lays.
        /// </summary>
        public float[] RoadsideWithM { get; init; } = [];

        /// <inheritdoc cref="RoadsideWithM"/>
        public float[] RoadsideAgainstM { get; init; } = [];

        /// <summary>
        /// How wide the roadside is between a road's lanes and the kerb the traffic one way keeps to — with the
        /// road where <paramref name="withTheRoad"/>, else against it.
        /// </summary>
        public float RoadsideM(int road, bool withTheRoad) =>
            RoadsideWithM.Length == 0 ? 0f : withTheRoad ? RoadsideWithM[road] : RoadsideAgainstM[road];

        /// <summary>How wide a road's lanes are side by side: its carriageway less the roadside either side.</summary>
        public float LanesWidthM(int road) => WidthM[road] - RoadsideM(road, true) - RoadsideM(road, false);

        /// <summary>
        /// <b>How wide the ground one lane of a road is driven on is</b>: its share of the lanes' width, or the
        /// whole of it where the road's two ways share one line (<see cref="DrivenOverOneLine(int)"/>).
        /// </summary>
        public float LaneWidthM(int road) =>
            DrivenOverOneLine(road) ? LanesWidthM(road) : LanesWidthM(road) / LanesOn(road);

        /// <summary>
        /// <b>How far one lane's own line stands off its road's</b>, toward the side its own traffic keeps
        /// (TER-4a), counting its lanes from the kerb on that side: the carriageway is the road's line half its
        /// width either way, and each way's lanes run in from its own kerb, past the roadside there, to the line
        /// its traffic meets the other's on. A road driven both ways over one line
        /// (<see cref="DrivenOverOneLine(int)"/>) lays both on the middle of its lanes' width.
        /// </summary>
        /// <remarks>
        /// <b>It depends on the lanes counted from the kerb and the roadside at that kerb and on nothing else</b>,
        /// so a carriageway of three lanes one way and two the other has the line its two ways meet on half a lane
        /// off the road's own — and the lane and the paint beside it (<see cref="LineBetweenLanesM"/>) are one
        /// arithmetic, which cannot disagree about where a lane is.
        /// </remarks>
        /// <param name="withTheRoad">Whether the lane runs with the road, which is which kerb it counts from.</param>
        public float LaneOffsetM(int road, int fromKerb, bool withTheRoad) =>
            RoadsideLineM(road, withTheRoad) - ((fromKerb + 0.5f) * LaneWidthM(road));

        /// <summary>
        /// <b>Where the paint between two lanes of a road goes</b> (TER-6): the line <paramref name="fromKerb"/>
        /// lanes in from a kerb, off the road's own line toward the side the traffic keeping to that kerb keeps
        /// — between the lanes <see cref="LaneOffsetM"/> stands either side of it.
        /// </summary>
        /// <param name="withTheRoad">Whether the kerb is the one the traffic with the road keeps to.</param>
        public float LineBetweenLanesM(int road, int fromKerb, bool withTheRoad) =>
            RoadsideLineM(road, withTheRoad) - (fromKerb * LaneWidthM(road));

        /// <summary>
        /// <b>Where a road's lanes end and the roadside at one kerb begins</b>: off the road's own line toward the
        /// side the traffic keeping to that kerb keeps, which is the kerb itself where there is no roadside.
        /// </summary>
        /// <param name="withTheRoad">Whether the kerb is the one the traffic with the road keeps to.</param>
        public float RoadsideLineM(int road, bool withTheRoad) => (WidthM[road] * 0.5f) - RoadsideM(road, withTheRoad);

        /// <summary>
        /// How many lines are painted down a road: one between each pair of its lanes that touch, the line its
        /// two ways meet on among them. <b>None on a road of one lane or one driven over one line</b>, which has
        /// no two ribbons to part.
        /// </summary>
        public int LinesBetweenLanes(int road) => LanesOn(road) < 2 || DrivenOverOneLine(road) ? 0 : LanesOn(road) - 1;

        /// <summary>
        /// <b>Whether the line a road's two ways meet on is crossed to get past what stands in a lane</b> (CAR-6.2b):
        /// on a carriageway of one lane each way only. One carrying more is painted unbroken there (TER-6), and a car
        /// on it gets past over a lane of its own way instead.
        /// </summary>
        public bool LineCrossedToPass(int road) =>
            LanesWithTheRoad(road) == 1 && LanesAgainstTheRoad(road) == 1 && !DrivenOverOneLine(road);

        /// <summary>
        /// <b>The turns each lane is marked for where it runs into its junction</b> (TER-5j) — a traced map's arrows
        /// (GEN-57): a road's lanes with it from the kerb, then those against it, or nothing for a road with none
        /// marked (<see cref="MarkedTurnsOf"/>). <b>Empty where the town marks none</b>, which is every town the
        /// generator lays.
        /// </summary>
        public MarkedTurns[] MarkedTurns { get; init; } = [];

        /// <summary>Count + 1 entries where <see cref="MarkedTurns"/> is filled: road <c>i</c>'s run starts at entry <c>i</c>.</summary>
        public int[] MarkedTurnOffsets { get; init; } = [];

        /// <summary>The turns a road's lanes one way are marked for, from the kerb, or none where none is marked.</summary>
        public ReadOnlySpan<MarkedTurns> MarkedTurnsOf(int road, bool withTheRoad)
        {
            if (MarkedTurnOffsets.Length == 0 || MarkedTurnOffsets[road] == MarkedTurnOffsets[road + 1]) return default;

            var with = LanesWithTheRoad(road);
            return withTheRoad
                ? MarkedTurns.AsSpan(MarkedTurnOffsets[road], with)
                : MarkedTurns.AsSpan(MarkedTurnOffsets[road] + with, LanesAgainstTheRoad(road));
        }

        /// <summary>
        /// <b>The turns a junction does not make</b>, from one road into another (TER-5j): a traced map's turn
        /// restrictions (GEN-57). <b>Empty where the town forbids none</b>, which is every town the generator lays.
        /// </summary>
        public RoadTurn[] BannedTurns { get; init; } = [];

        /// <summary>
        /// <b>The lanes a turn joins, where the survey says which</b> (TER-5j): a traced map's lane connectivity
        /// (GEN-57). A turn with any is made between those lanes and no others. <b>Empty where the town says none.</b>
        /// </summary>
        public LaneLink[] LaneLinks { get; init; } = [];

        /// <summary>A town whose every road runs both ways, which is every map that lays no one-way street.</summary>
        public static RoadFlow[] AllBothWays(int roads) => new RoadFlow[roads];

        /// <summary>Count + 1 entries: road <c>i</c>'s pieces are <c>Segments[SegmentOffsets[i]..SegmentOffsets[i + 1]]</c>.</summary>
        public required int[] SegmentOffsets { get; init; }

        public required ArcSeg[] Segments { get; init; }

        /// <summary>
        /// <b>The places each road passes between its two junctions</b> (GEN-51), in the order it passes
        /// them — count + 1 entries, or <b>empty where no road of the town passes anywhere</b>, which is
        /// every town whose junctions are all places roads meet.
        /// </summary>
        /// <remarks>
        /// <b>This is what a road is, not a record of what it was.</b> A road joined out of a run keeps the
        /// corners the run had, and the bearing it leaves each junction on is drawn toward the first place
        /// it passes rather than toward the far end it never points at
        /// (<see cref="ConnectionPoints.ArmOf"/>) — so a plan that did not carry them would draw a different
        /// town when it was read back than when it was laid.
        /// </remarks>
        public int[] ThroughOffsets { get; init; } = [];

        /// <inheritdoc cref="ThroughOffsets"/>
        public Vector2[] ThroughM { get; init; } = [];

        /// <summary>
        /// <b>Which roads were cut</b> (GEN-52): ones whose line stood before the junction at one of their
        /// ends did, so <b>both</b> their arms are read off that line rather than drawn for it
        /// (<see cref="ConnectionPoints.ArmOf"/>). A car park's bays are read the same way, their lines being
        /// laid where the kerb is (GEN-53). <b>Empty where the town cut none</b>, which is every map that lays
        /// no car park.
        /// </summary>
        /// <remarks>
        /// <b>Both ends and not the cut one.</b> An arm is drawn toward the far end of its own road
        /// (GEN-46), and a cut moves that far end onto the new node — so a road whose other end went on
        /// drawing its bearing would draw a different one than the line it already carries was laid to.
        /// </remarks>
        public bool[] Cut { get; init; } = [];

        /// <inheritdoc cref="Cut"/>
        public bool WasCut(int road) => Cut.Length > 0 && Cut[road];

        /// <summary>
        /// <b>Which roads were laid straight</b> (GEN-47): their arms leave on the chord rather than jittered
        /// off it (<see cref="ConnectionPoints.ArmOf"/>), so the plan has to carry it or it would draw a
        /// different arm when it was read back than the one the road was laid to. <b>Empty where the town laid
        /// none.</b>
        /// </summary>
        public bool[] LaidStraight { get; init; } = [];

        /// <inheritdoc cref="LaidStraight"/>
        public bool IsLaidStraight(int road) => LaidStraight.Length > 0 && LaidStraight[road];

        /// <summary>
        /// <b>The level each road is driven on</b> (GEN-57, PHY-1a): <see cref="Ground"/>, or
        /// <see cref="Over"/> for a bridge that carries it over other roads — whose cars, ground and claims never
        /// meet those of the roads it passes over. <b>Empty where every road is on the ground</b>, which is every
        /// town the generator lays: its bridges span water and nothing passes under them.
        /// </summary>
        public byte[] Level { get; init; } = [];

        /// <inheritdoc cref="Level"/>
        public byte LevelOf(int road) => Level.Length > 0 ? Level[road] : Ground;

        /// <summary>The level a road on the ground is driven on, which is every road but a bridge over others.</summary>
        public const byte Ground = 0;

        /// <summary>The level a bridge over other roads is driven on.</summary>
        public const byte Over = 1;

        /// <summary>
        /// <b>The collision channel a level is</b> (PHY-1a): a bit of its own, so what stands on several levels at once —
        /// a bridgehead, a car on it — carries each of theirs.
        /// </summary>
        public static byte ChannelOf(byte level) => (byte)(1 << level);

        /// <summary>The ground's channel (<see cref="ChannelOf"/>), which everything but a bridge over other roads is on.</summary>
        public const byte GroundChannel = 1 << Ground;

        public int Count => WidthM.Length;

        public ReadOnlySpan<ArcSeg> SegmentsOf(int road) =>
            Segments.AsSpan(SegmentOffsets[road], SegmentOffsets[road + 1] - SegmentOffsets[road]);

        /// <inheritdoc cref="ThroughOffsets"/>
        public ReadOnlySpan<Vector2> ThroughOf(int road) =>
            ThroughOffsets.Length == 0
                ? default
                : ThroughM.AsSpan(ThroughOffsets[road], ThroughOffsets[road + 1] - ThroughOffsets[road]);
    }

    /// <summary>The stretch of its road each deck spans, and the pavement the deck carries over at the width it has on land.</summary>
    internal sealed class BridgeArrays
    {
        public required int[] Road { get; init; }
        public required float[] FromM { get; init; }
        public required float[] ToM { get; init; }
        public required float[] DeckWidthM { get; init; }
        public required float[] PavementWidthM { get; init; }
        public int Count => Road.Length;
    }

    /// <summary>
    /// Which roads make up each roundabout's ring, flat with an offsets array beside it as every run in this
    /// structure is. <b>Membership and no geometry</b>: where a ring stands is its own arcs' to say.
    /// </summary>
    internal sealed class RoundaboutArrays
    {
        /// <summary>A map with no roundabout on it, which is every map that is not a generated city.</summary>
        public static RoundaboutArrays None => new() { RingOffsets = [0], Road = [] };

        /// <summary>Count + 1 entries, over <see cref="Road"/>.</summary>
        public required int[] RingOffsets { get; init; }

        public required int[] Road { get; init; }

        public int Count => RingOffsets.Length - 1;

        public ReadOnlySpan<int> RoadsOf(int roundabout) =>
            Road.AsSpan(RingOffsets[roundabout], RingOffsets[roundabout + 1] - RingOffsets[roundabout]);
    }

    /// <summary>
    /// <b>The town's car parks</b> (GEN-53), as the street each stands off, the place on it, and the bays laid
    /// off its kerb. <b>Membership and counts, and no geometry</b>: where a bay reaches is its own road's to
    /// say, the same way a roundabout carries no circle.
    /// </summary>
    internal sealed class CarParkArrays
    {
        /// <summary>A map with no car park on it, which is every map that lays none.</summary>
        public static CarParkArrays None => new() { Street = [], AtM = [], BayOffsets = [0], Road = [], Right = [] };

        /// <summary>The road each car park's bays stand off, one per car park. It is not parted (GEN-53).</summary>
        public required int[] Street { get; init; }

        /// <summary>The middle of each car park's rank, on its street's line.</summary>
        public required Vector2[] AtM { get; init; }

        /// <summary>Count + 1 entries, over <see cref="Road"/> and <see cref="Right"/>.</summary>
        public required int[] BayOffsets { get; init; }

        /// <summary>
        /// <b>One road a bay</b> (GEN-53): the space itself, from the kerb out, on nodes of its own and joined to
        /// nothing. It is one lane wide and its lane is driven both ways over one line
        /// (<see cref="RoadArrays.DrivenOverOneLine"/>), so it runs into the bay from the street.
        /// </summary>
        public required int[] Road { get; init; }

        /// <summary>
        /// Which side of its street each bay stands on — the driver's right of that road's own direction, or
        /// its left. <b>The two counts are what a car park's size is</b>, each of them nought or a handful
        /// (GEN-4b), and not both nought.
        /// </summary>
        public required bool[] Right { get; init; }

        public int Count => Street.Length;

        public ReadOnlySpan<int> RoadsOf(int carPark) =>
            Road.AsSpan(BayOffsets[carPark], BayOffsets[carPark + 1] - BayOffsets[carPark]);

        /// <summary>How many bays one car park has on one side of the road it was cut into.</summary>
        public int BaysOn(int carPark, bool right)
        {
            var bays = 0;
            for (var bay = BayOffsets[carPark]; bay < BayOffsets[carPark + 1]; bay++)
            {
                if (Right[bay] == right) bays++;
            }

            return bays;
        }

        /// <summary>
        /// The longer of a car park's two ranks, which is how far along the street it reaches
        /// (GEN-53, <see cref="SimConfig.CarParkFrontageM"/>).
        /// </summary>
        public int MostBaysOnASide(int carPark) =>
            Math.Max(BaysOn(carPark, right: true), BaysOn(carPark, right: false));
    }

    internal sealed class PavedAreaArrays
    {
        /// <summary>A map with no paving of its own on it, which is most of them.</summary>
        public static PavedAreaArrays None => new() { MinM = [], SizeM = [] };

        public required Vector2[] MinM { get; init; }
        public required Vector2[] SizeM { get; init; }
        public int Count => MinM.Length;
    }

    /// <summary>
    /// The zebras. <b>A crossing carries no width of its own</b> (TER-6): it is a band of the carriageway
    /// it is painted on, so how far it reaches is <see cref="CrossingSpanM"/> off the road it names.
    /// </summary>
    internal sealed class CrosswalkArrays
    {
        public required Vector2[] CentreM { get; init; }

        /// <summary>Along the road the crossing crosses, so the way over it is square to this.</summary>
        public required Vector2[] Axis { get; init; }

        /// <summary>How much of the road's length the paint covers, which is the crossing's own figure.</summary>
        public required float[] DepthM { get; init; }

        /// <summary>The road the paint is laid across, whose width the crossing spans kerb to kerb.</summary>
        public required int[] Road { get; init; }

        /// <summary>The junction the crossing belongs to, or <see cref="NoRecord"/> where it was struck mid-block.</summary>
        public required int[] Junction { get; init; }

        public int Count => CentreM.Length;
    }

    internal sealed class ParkingLotArrays
    {
        public required Vector2[] CentreM { get; init; }
        public required Vector2[] Axis { get; init; }
        public required Vector2[] HalfExtentM { get; init; }

        /// <summary>Count + 1 entries, over <see cref="SpacePositionM"/> and <see cref="SpaceHeadingRad"/>.</summary>
        public required int[] SpaceOffsets { get; init; }

        public required Vector2[] SpacePositionM { get; init; }
        public required float[] SpaceHeadingRad { get; init; }
        public int Count => CentreM.Length;
        public int SpaceCount => SpacePositionM.Length;
    }

    internal sealed class BuildingArrays
    {
        /// <summary>A town with nothing standing on it: a brief that asks for no building, or a face with no place to stand one.</summary>
        public static BuildingArrays None => new()
        {
            CentreM = [], SizeM = [], HeadingRad = [], Capacity = [], Use = [], EntryOffsets = [0],
            EntryPointM = [],
        };

        public required Vector2[] CentreM { get; init; }
        public required Vector2[] SizeM { get; init; }
        public required float[] HeadingRad { get; init; }
        public required int[] Capacity { get; init; }

        /// <summary>What each one is for (AMB-1, SRV-1). Authored with the building, so a map's services are the map's.</summary>
        public required BuildingUse[] Use { get; init; }

        /// <summary>Count + 1 entries, over <see cref="EntryPointM"/>. Every building has at least one.</summary>
        public required int[] EntryOffsets { get; init; }

        public required Vector2[] EntryPointM { get; init; }

        /// <summary>
        /// <b>The prefab each building wears where the plan chose it</b> (GEN-57): its place among the prefabs the plan
        /// was handed (<see cref="BuildingSizes.PrefabM"/>), its <see cref="SizeM"/> that prefab laid along or across
        /// its bearing. Empty where none was chosen, and the catalogue matches a roof to each building's size.
        /// </summary>
        public int[] Prefab { get; init; } = [];

        public int Count => CentreM.Length;
    }

    /// <summary>
    /// Footprints as their rings, flat with offsets beside them as every run in this structure is: each its outline
    /// first and then any courtyard cut out of it.
    /// </summary>
    internal sealed class FootprintArrays
    {
        public static FootprintArrays None => new() { RingOffsets = [0], Rings = RingArrays.None, Traced = [], HeightM = [], Use = [] };

        /// <summary>Count + 1 entries, over <see cref="Rings"/>: footprint i's are <c>RingOffsets[i]..RingOffsets[i + 1]</c>.</summary>
        public required int[] RingOffsets { get; init; }

        /// <summary>Every ring, none closed on its first point, in either hand.</summary>
        public required RingArrays Rings { get; init; }

        /// <summary>Whether it was traced off imagery by a machine rather than mapped, which it is drawn a shade apart for.</summary>
        public required bool[] Traced { get; init; }

        /// <summary>How tall it stands, or nought where nothing says — which its roof is drawn the lighter for.</summary>
        public required float[] HeightM { get; init; }

        public required Traced.FootprintUse[] Use { get; init; }

        public int Count => Traced.Length;
    }

    internal sealed class PropArrays
    {
        public required Vector2[] CentreM { get; init; }
        public required float[] RadiusM { get; init; }

        /// <summary>
        /// The road's own bearing where a prop was laid along a kerb (GEN-6b), and zero for one the wild
        /// pass dropped on open ground. <b>It is drawn on only by a look that turns</b>
        /// (<c>PropVariant.Turns</c>): a tree has no bearing to be wrong about, so the field says what the
        /// ground was doing there and the catalogue says whether the picture cares.
        /// </summary>
        public required float[] BearingRad { get; init; }

        public required byte[] Kind { get; init; }
        public int Count => CentreM.Length;
    }

    /// <summary>Where the roster stands at the first tick.</summary>
    internal sealed class SpawnArrays
    {
        /// <summary>0 person, 1 car.</summary>
        public required byte[] Kind { get; init; }

        public required Vector2[] PositionM { get; init; }
        public required float[] HeadingRad { get; init; }
        public int Count => Kind.Length;
    }

    /// <summary>Closed rings, carried flat with an offsets array beside them as every run in this structure is.</summary>
    internal sealed class RingArrays
    {
        public static RingArrays None => new() { Offsets = [0], PointM = [] };

        /// <summary>Count + 1 entries, over <see cref="PointM"/>.</summary>
        public required int[] Offsets { get; init; }

        public required Vector2[] PointM { get; init; }

        public int Count => Offsets.Length - 1;

        public ReadOnlySpan<Vector2> RingOf(int ring) =>
            PointM.AsSpan(Offsets[ring], Offsets[ring + 1] - Offsets[ring]);
    }

    /// <summary>
    /// A town's water, as the four rings each piece of it is drawn from (GEN-2c). <b>They are the same wave
    /// at four offsets</b>, drawn largest first so that each fill leaves a line of the one under it: the
    /// shore, the shore less a line, the water plus a line, the water.
    /// </summary>
    internal sealed class WaterArrays
    {
        /// <summary>A map with no water on it, which is most of them.</summary>
        public static WaterArrays None => new()
        {
            Outline = RingArrays.None, Shore = RingArrays.None,
            ShoreEdge = RingArrays.None, WaterEdge = RingArrays.None,
        };

        /// <summary>The water's own edge, which is what is drawn as water and what was classified as it.</summary>
        public required RingArrays Outline { get; init; }

        /// <summary>
        /// The outer edge of the shore the water is set in. <b>The ring and not the strip</b>: the strip is
        /// what is left of it once the water is laid over it, which is how it is drawn and how the ground
        /// under it was classified.
        /// </summary>
        public required RingArrays Shore { get; init; }

        /// <summary>That edge less a line's width, so what is left between the two is the line along the grass.</summary>
        public required RingArrays ShoreEdge { get; init; }

        /// <summary>And the water's edge plus one, so what is left between the two is the line along the water.</summary>
        public required RingArrays WaterEdge { get; init; }
    }
}
