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
    /// <b>The town's car parks, as the junctions they were cut into roads at</b> (GEN-53). A car park is an
    /// ordinary junction with an arm out to its bays, so this says which those are and nothing else — there
    /// is no car park geometry and no rule downstream about one.
    /// </summary>
    public CarParkArrays CarParks { get; init; } = CarParkArrays.None;

    public required PavedAreaArrays PavedAreas { get; init; }

    public required CrosswalkArrays Crosswalks { get; init; }

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
        public int Count => CentreM.Length;
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

        /// <summary>Which way each road is driven (TER-4d), which is also how many lanes its width is.</summary>
        public required RoadFlow[] Flow { get; init; }

        /// <summary>How many lanes a road's own width carries: one where it runs one way and two where it runs both.</summary>
        public int LanesOn(int road) => Flow[road] == RoadFlow.BothWays ? 2 : 1;

        /// <summary>
        /// <b>Which roads are a car park's bays</b> (GEN-53), the arm being the way its bay is reached over.
        /// <b>Empty where the town lays none.</b>
        /// </summary>
        public bool[] Bay { get; init; } = [];

        /// <inheritdoc cref="Bay"/>
        public bool IsABay(int road) => Bay.Length > 0 && Bay[road];

        /// <summary>
        /// <b>Whether a road is driven both ways over one line</b> — a car's width of ground driven in over
        /// and driven back out over (GEN-4f), so its two lanes are its own line rather than two halves of a
        /// carriageway. <b>A bay's way is the only ground in this town that is</b>, which is why it is that
        /// and not a flag of its own: two arrays that must agree are two answers.
        /// </summary>
        public bool DrivenOverOneLine(int road) => IsABay(road);

        /// <summary>
        /// <b>How wide the ground one lane of a road is driven on is</b>: its share of the carriageway, or
        /// the whole of it where the road's two ways share one line
        /// (<see cref="DrivenOverOneLine(int)"/>).
        /// </summary>
        public float LaneWidthM(int road) =>
            DrivenOverOneLine(road) ? WidthM[road] : WidthM[road] / LanesOn(road);

        /// <summary>
        /// <b>Whether a road's two ways are laid either side of its own line</b>, so the ground each of them
        /// is driven over meets the other's along it. A one-way street carries one lane down the middle
        /// (TER-4d) and a bay's way carries two over one line
        /// (<see cref="DrivenOverOneLine(int)"/>): neither has two ribbons to part.
        /// </summary>
        /// <remarks>
        /// <b>One question asked twice over</b>: it is the offset a lane is laid at
        /// (<c>LaneLines.Of</c>) and the line the paint between the two goes down (TER-6), and the two would
        /// otherwise be two readings of the same geometry that could disagree about which roads are which.
        /// </remarks>
        public bool LanesMeetOnItsLine(int road) => LanesOn(road) == 2 && !DrivenOverOneLine(road);

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
        /// (<see cref="ConnectionPoints.ArmOf"/>). <b>Empty where the town cut none</b>, which is every map
        /// that lays no car park.
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
    /// <b>The town's car parks</b> (GEN-53), as the junction each was cut into a road at and the arms that
    /// junction carries out to its bays. <b>Membership and counts, and no geometry</b>: where an arm reaches
    /// is its own road's to say, the same way a roundabout carries no circle.
    /// </summary>
    internal sealed class CarParkArrays
    {
        /// <summary>A map with no car park on it, which is every map that lays none.</summary>
        public static CarParkArrays None => new() { Junction = [], BayOffsets = [0], Road = [], Right = [] };

        /// <summary>The junction the car park was cut into a road at, one per car park.</summary>
        public required int[] Junction { get; init; }

        /// <summary>Count + 1 entries, over <see cref="Road"/> and <see cref="Right"/>.</summary>
        public required int[] BayOffsets { get; init; }

        /// <summary>
        /// <b>One road a bay</b> (GEN-53): the way that bay is reached over, running from the car park's
        /// junction out to the node the bay itself stands at. It is one lane wide and its lane is driven both
        /// ways over one line (<see cref="RoadArrays.DrivenOverOneLine"/>).
        /// </summary>
        public required int[] Road { get; init; }

        /// <summary>
        /// Which side of the road the car park was cut into each bay stands on — the driver's right of that
        /// road's own direction, or its left. <b>The two counts are what a car park's size is</b>, each of
        /// them nought or a handful (GEN-4b), and not both nought.
        /// </summary>
        public required bool[] Right { get; init; }

        public int Count => Junction.Length;

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
        /// The longer of a car park's two ranks, which is how far along the street it reaches and so how far
        /// back its street stands off (GEN-53, <see cref="SimConfig.CarParkStandoffM"/>).
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
        public int Count => CentreM.Length;
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
