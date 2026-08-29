using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.CityGen;

/// <summary>
/// One complete city as pure data: no node references and no behaviour. A builder stands the world up
/// from it, a validator judges it, the <c>.town</c> format carries it, and the generator will emit it.
/// </summary>
/// <remarks>
/// Structure of arrays, laid once at load: one array per field, and a variable-length run — a road's
/// segments, a lot's spaces, a building's ways in, a water outline's points — is a flat array with an
/// offsets array beside it, so a plan of a whole city is a few dozen allocations and no per-record
/// object. The world is built from this structure and never from the file.
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

    public required float CellSizeM { get; init; }

    /// <summary>0 for a map laid without a pavement.</summary>
    public required float PavementWidthM { get; init; }

    public required int GridWidth { get; init; }

    public required int GridHeight { get; init; }

    /// <summary>The terrain classification, row-major with y outer. One byte a cell; the town is fully covered.</summary>
    public required Ground[] Cells { get; init; }

    /// <summary>
    /// Two components a cell, quantised to 1/127, dense over the whole grid and zero off the
    /// carriageway. The file carries it sparse because a city's grid is 24.6 MB of mostly nothing;
    /// it is expanded here because the tick asks for it <em>by position</em>, and a sparse lookup in
    /// the follower's inner loop is a hash where a load would do.
    /// </summary>
    public required sbyte[] LaneDirs { get; init; }

    public required JunctionArrays Junctions { get; init; }

    /// <summary>Kerb fillets, carried because they cannot be read back off any other shape.</summary>
    public required JunctionCornerArrays JunctionCorners { get; init; }

    /// <summary>
    /// The pavement's inner corners as the map that arrived recorded them. <b>Nothing draws from this</b>
    /// — a corner is a fact about the pair of shapes the walk is laid from, so the build solves them
    /// itself (TER-3c.4). It is here because a shipped `.town` carries the field and the round trip over
    /// every one of them is what holds the reader and the writer to each other.
    /// </summary>
    public required PavementCornerArrays PavementCorners { get; init; }

    public required RoadArrays Roads { get; init; }

    public required BridgeArrays Bridges { get; init; }

    public required PavedAreaArrays PavedAreas { get; init; }

    public required CrosswalkArrays Crosswalks { get; init; }

    /// <summary>The bars that were <em>painted</em>, not the ones the plan called for: a bar nobody painted is a bar nobody stops at.</summary>
    public required StopLineArrays StopLines { get; init; }

    public required ParkingLotArrays ParkingLots { get; init; }

    public required BuildingArrays Buildings { get; init; }

    public required PropArrays Props { get; init; }

    public required SpawnArrays Spawns { get; init; }

    public required WaterArrays Water { get; init; }

    public int CellCount => GridWidth * GridHeight;

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

    internal sealed class PavementCornerArrays
    {
        public required Vector2[] CornerM { get; init; }
        public required Vector2[] NormalA { get; init; }
        public required Vector2[] NormalB { get; init; }
        public required float[] RadiusM { get; init; }
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

        /// <summary>Count + 1 entries: road <c>i</c>'s pieces are <c>Segments[SegmentOffsets[i]..SegmentOffsets[i + 1]]</c>.</summary>
        public required int[] SegmentOffsets { get; init; }

        public required ArcSeg[] Segments { get; init; }
        public int Count => WidthM.Length;

        public ReadOnlySpan<ArcSeg> SegmentsOf(int road) =>
            Segments.AsSpan(SegmentOffsets[road], SegmentOffsets[road + 1] - SegmentOffsets[road]);
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

    internal sealed class PavedAreaArrays
    {
        public required Vector2[] MinM { get; init; }
        public required Vector2[] SizeM { get; init; }
        public int Count => MinM.Length;
    }

    internal sealed class CrosswalkArrays
    {
        public required Vector2[] CentreM { get; init; }
        public required Vector2[] Axis { get; init; }
        public required float[] DepthM { get; init; }
        public required float[] SpanM { get; init; }

        /// <summary>The junction the crossing belongs to, or <see cref="NoRecord"/> where it was struck mid-block.</summary>
        public required int[] Junction { get; init; }

        public int Count => CentreM.Length;
    }

    internal sealed class StopLineArrays
    {
        public required Vector2[] CentreM { get; init; }
        public required Vector2[] Approach { get; init; }
        public required float[] SpanM { get; init; }
        public required float[] ThicknessM { get; init; }
        public required int[] Junction { get; init; }
        public required int[] Road { get; init; }
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
        public required byte[] Kind { get; init; }
        public int Count => CentreM.Length;
    }

    /// <summary>
    /// Where the roster stands at the first tick. A spawn's patrol point is a scenario's device and the
    /// <c>.town</c> format deliberately does not carry it, so a scenario map's walkers wander instead.
    /// </summary>
    internal sealed class SpawnArrays
    {
        /// <summary>0 person, 1 car.</summary>
        public required byte[] Kind { get; init; }

        public required Vector2[] PositionM { get; init; }
        public required float[] HeadingRad { get; init; }
        public int Count => Kind.Length;
    }

    internal sealed class WaterArrays
    {
        /// <summary>Count + 1 entries, over <see cref="PointM"/>.</summary>
        public required int[] OutlineOffsets { get; init; }

        public required Vector2[] PointM { get; init; }
        public int Count => OutlineOffsets.Length - 1;

        public ReadOnlySpan<Vector2> OutlineOf(int outline) =>
            PointM.AsSpan(OutlineOffsets[outline], OutlineOffsets[outline + 1] - OutlineOffsets[outline]);
    }
}
