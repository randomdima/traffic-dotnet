using System.Numerics;

namespace TrafficSimulation.CityGen;

/// <summary>
/// <b>Every shape the ground is cut from</b>, and nothing else a map carries. It is what
/// <see cref="GroundShapes"/> answers a point against and what <see cref="PavementCorners"/> solves the
/// walk's own corners from — the two readings of the town's surface, off one set of records.
/// </summary>
/// <remarks>
/// <para>
/// It exists so the ground can be asked about <em>while a town is still being laid</em>. A finished map
/// hands over <see cref="CityPlan.Ground"/>; a generator part-way through hands over what it has and
/// <see cref="None"/> for the rest, and gets the same answer about the shapes that do exist. Without it
/// the only way to ask was a raster painted alongside — a second description of the same ground, agreeing
/// with it to within a cell (TER-7).
/// </para>
/// <para>
/// The arrays are the plan's own and are never copied: a plan is immutable once laid, and a generator's
/// are finished before they are handed over.
/// </para>
/// </remarks>
internal readonly record struct GroundPieces(
    Vector2 WorldSizeM,
    float PavementWidthM,
    CityPlan.RoadArrays Roads,
    CityPlan.BridgeArrays Bridges,
    CityPlan.JunctionArrays Junctions,
    CityPlan.JunctionCornerArrays JunctionCorners,
    CityPlan.ParkingLotArrays ParkingLots,
    CityPlan.PavedAreaArrays PavedAreas,
    CityPlan.CrosswalkArrays Crosswalks,
    CityPlan.WaterArrays Water)
{
    /// <summary>Bare ground of the size given: grass everywhere, which is what a town starts as.</summary>
    public static GroundPieces None(Vector2 worldSizeM, float pavementWidthM) =>
        new(worldSizeM, pavementWidthM,
            new CityPlan.RoadArrays
            {
                FromJunction = [], ToJunction = [], WidthM = [], SegmentOffsets = [0], Segments = [],
            },
            new CityPlan.BridgeArrays { Road = [], FromM = [], ToM = [], DeckWidthM = [], PavementWidthM = [] },
            new CityPlan.JunctionArrays { CentreM = [], RadiusM = [], Lit = [], PhaseOffsetS = [] },
            new CityPlan.JunctionCornerArrays
            {
                CornerM = [], ArcCentreM = [], RadiusM = [], TangentAM = [], TangentBM = [],
            },
            new CityPlan.ParkingLotArrays
            {
                CentreM = [], Axis = [], HalfExtentM = [], SpaceOffsets = [0], SpacePositionM = [],
                SpaceHeadingRad = [],
            },
            CityPlan.PavedAreaArrays.None,
            new CityPlan.CrosswalkArrays { CentreM = [], Axis = [], DepthM = [], Road = [], Junction = [] },
            CityPlan.WaterArrays.None);

    /// <summary>The same ground with its water laid, which is the first thing a town gets.</summary>
    public GroundPieces With(CityPlan.WaterArrays water) => this with { Water = water };

    /// <summary>And with its roads on it — the carriageways, what each junction shares, and the kerb corners.</summary>
    public GroundPieces With(
        CityPlan.RoadArrays roads, CityPlan.BridgeArrays bridges, CityPlan.JunctionArrays junctions,
        CityPlan.JunctionCornerArrays corners, CityPlan.CrosswalkArrays crosswalks) =>
        this with
        {
            Roads = roads, Bridges = bridges, Junctions = junctions, JunctionCorners = corners,
            Crosswalks = crosswalks,
        };

    /// <summary>And with the car parks the slot stage laid.</summary>
    public GroundPieces With(CityPlan.ParkingLotArrays lots) => this with { ParkingLots = lots };
}
