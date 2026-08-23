namespace TrafficSimulation.Core.Config;

/// <summary>The carriageway and everything painted on it, plus the bays and crossings cut into it.</summary>
internal sealed class RoadFigures
{
    /// <summary>
    /// The one global constant: which side of the centreline traffic keeps. The lane offset, the turn
    /// classification, keep-right on foot and which flank a door is on all derive their sign from it.
    /// </summary>
    public bool TrafficKeepsRight { get; init; } = true;

    /// <summary>
    /// How far off straight ahead a movement through a junction still counts as going straight on. Half a
    /// right angle, so a four-armed junction splits evenly and a street that bends through one is still a
    /// street — a bend joining two roads crosses nothing.
    /// </summary>
    public float TurnStraightToleranceDeg { get; init; } = 45f;

    public float WidthInCarWidths { get; init; } = 3f;
    public float IntersectionCornerRadiusInCarWidths { get; init; } = 2.5f;
    public float PavementWidthM { get; init; } = 4f;

    /// <summary>Half the walk, which is what stands a corner 4.83 m deep against the straight's 4 m.</summary>
    public float PavementCornerRadiusM { get; init; } = 2f;

    public float BridgeDeckMarginM { get; init; } = 1.5f;
    public float EdgeLineWidthM { get; init; } = 0.3f;

    /// <summary>One painted line: a lane dash, a bay stroke. A zebra's bar is twice it and a stop bar is the plan's own.</summary>
    public float PaintLineWidthM { get; init; } = 0.25f;

    /// <summary>The dashed lane centreline: how long a dash is and how long the gap after it.</summary>
    public float LaneDashLengthM { get; init; } = 2f;

    public float LaneDashGapM { get; init; } = 2f;

    /// <summary>A zebra's bars: how wide one is and how far apart they are laid across the carriageway.</summary>
    public float ZebraStripeWidthM { get; init; } = 0.5f;

    public float ZebraStripePitchM { get; init; } = 1f;
    public float ParkingSpaceMarginInCarWidths { get; init; } = 0.5f;
    public float LotClearanceFromJunctionInCarLengths { get; init; } = 2f;
    public float LotClearanceFromLotInCarLengths { get; init; } = 3f;
    public float MidBlockCrossingSpacingM { get; init; } = 70f;
    public float MidBlockCrossingChance { get; init; } = 0.3f;
}

/// <summary>A building as the plan sizes it and as a trip uses it.</summary>
internal sealed class BuildingFigures
{
    public float FootprintMinInCarFootprints { get; init; } = 4f;
    public float FootprintMaxInCarFootprints { get; init; } = 12f;
    public int CapacityMin { get; init; } = 1;
    public int CapacityMax { get; init; } = 3;
    public float WalkablePaddingInPersonDiameters { get; init; } = 2f;

    /// <summary>The one strip of ground somebody is meant to stand in, and not the padding above.</summary>
    public float FrontGapM { get; init; } = 1f;

    public float DwellMinS { get; init; } = 0f;
    public float DwellMaxS { get; init; } = 10f;
}

/// <summary>Street furniture: the unit every other static size is quoted against.</summary>
internal sealed class PropFigures
{
    public float DiameterInCarWidths { get; init; } = 1f;
    public float SizeJitterMin { get; init; } = 0.7f;
    public float SizeJitterMax { get; init; } = 1.6f;

    /// <summary>Load-bearing rather than tidy: a continuous range gives every prop its own collision shape.</summary>
    public float RadiusRoundingM { get; init; } = 0.05f;

    /// <summary>The four sizes a prop is laid at, as multiples of the prop diameter.</summary>
    public static ReadOnlySpan<float> SizeBands => [0.35f, 0.6f, 1f, 1.35f];
}

/// <summary>The lights, and the heads that show them.</summary>
internal sealed class SignalFigures
{
    /// <summary>Both phases share the cycle, so each axis gets half of it.</summary>
    public float CycleS { get; init; } = 15f;

    /// <summary>The last stretch of a green rather than time added to it.</summary>
    public float AmberTailS { get; init; } = 1.5f;

    /// <summary>How far past its own stop bar, along the road, a car head stands.</summary>
    public float HeadSetbackM { get; init; } = 7f;

    /// <summary>A car head, along its lamps and across them.</summary>
    public float CarHeadLengthM { get; init; } = 2.4f;

    public float CarHeadWidthM { get; init; } = 0.9f;

    /// <summary>A pedestrian head, along its lamps and across them.</summary>
    public float WalkHeadLengthM { get; init; } = 0.95f;

    public float WalkHeadWidthM { get; init; } = 0.6f;

    /// <summary>What a pedestrian head keeps between itself and the paint it stands beside.</summary>
    public float HeadClearanceM { get; init; } = 0.1f;
}

/// <summary>What each surface does to a tyre standing on it, and how readily it takes a mark.</summary>
internal sealed class TerrainFigures
{
    public float GrassCoefficient { get; init; } = 0.8f;
    public float PavedCoefficient { get; init; } = 1.0f;
    public float WaterCoefficient { get; init; } = 0.15f;

    /// <summary>
    /// Resistance to travel over a surface, spent outside the traction budget so it costs nothing a tyre
    /// would have used for cornering — terrain slows by friction, never by a speed multiplier. Tarmac's is
    /// a feel figure rather than a physical one: a real coastdown is ≈ 0.23 m/s² and a car that coasts the
    /// length of the town reads as floating. Grass is deep turf at ≈ 0.29 g — enough that a lawn is
    /// somewhere a car struggles, not so much that it strands one there.
    /// </summary>
    public float GrassDragMps2 { get; init; } = 2.86f;

    public float PavedDragMps2 { get; init; } = 1.2f;
    public float WaterDragMps2 { get; init; } = 3.4f;

    /// <summary>
    /// How easily a surface takes a permanent mark, as a factor on <see cref="MarkFigures.PowerM2S3"/>.
    /// Tarmac is slightly softer than the bar's own figure, so a slide that just clears it shows as a
    /// scuff. Grass records the wheel <em>ploughing</em> it rather than a slide at all, and its factor
    /// tracks the road figure and the grass drag both — move either and this moves to match.
    /// </summary>
    public float GrassMarkFactor { get; init; } = 1.01f;

    public float PavedMarkFactor { get; init; } = 0.8f;
}

/// <summary>What a tyre has to be doing before it writes on the ground, and how much of that the town keeps.</summary>
internal sealed class MarkFigures
{
    /// <summary>
    /// What reaches the ground at all is friction the tyre is <em>losing</em>: the patch dragging across
    /// the surface rather than rolling over it. Rolling resistance never marks a road — it is hysteresis
    /// inside the rubber and not the road being worked — which is the one thing a mark model must not get
    /// wrong, or every car paints a line behind it simply by moving.
    /// <para>
    /// Three figures decide it and they are three different questions: is the patch sliding at all
    /// (<see cref="SlipMps"/>), has it slid far enough to leave rubber (<see cref="OnsetM"/>), and how
    /// hard is it working the ground (this, as friction power per kg of the load it carries). The first
    /// two are what keep ordinary driving off the road.
    /// </para>
    /// </summary>
    public float PowerM2S3 { get; init; } = 10f;

    /// <summary>
    /// The minor bar, and the rate a scrub that has stopped drains away at. Small on purpose: along its
    /// roll a wheel either turns with the ground or it does not, so a locked wheel, a braked one that has
    /// stopped turning and a spinning one are all genuinely dragging and all should write.
    /// </summary>
    public float SlipMps { get; init; } = 0.5f;

    /// <summary>
    /// Sideways is the exception, because a rolling tyre makes its cornering force <em>by</em> creeping
    /// across the ground: a firm corner runs metres a second of it and is cornering rather than sliding.
    /// Only what exceeds the creep is a slide, and a wheel that is not rolling gets no such allowance.
    /// </summary>
    public float CorneringSlipMps { get; init; } = 5f;

    /// <summary>
    /// How far a tyre has to drag over the ground before it starts writing on it — the whole difference
    /// between a hard turn-in, which scrubs for a few centimetres while the body yaws into line, and a
    /// slide that goes on for metres.
    /// </summary>
    public float OnsetM { get; init; } = 0.5f;

    /// <summary>How far a wheel travels per mark quad: short enough that a corner reads as a curve, long enough that a car lays a handful a second.</summary>
    public float SpacingM { get; init; } = 0.4f;

    /// <summary>
    /// What a wheel merely crossing soft ground writes, before any question of how hard it is working it.
    /// Ploughing is displacement, not friction: a tyre pushes grass aside by rolling over it, so a car
    /// that idles across a lawn leaves the same two tracks a fast one does, only fainter. Priced as power
    /// alone the effect dies with the speed and a car creeping onto a verge leaves it pristine, which is
    /// the one thing soft ground must not do.
    /// </summary>
    public float PloughFloor { get; init; } = 0.3f;

    /// <summary>Below this the wheel is standing on the ground rather than crossing it, and standing on grass ploughs nothing.</summary>
    public float PloughCrawlMps { get; init; } = 0.2f;

    /// <summary>How many marks the town remembers before the oldest is overwritten. Scenery only: nothing samples a mark and no agent sees one.</summary>
    public int Capacity { get; init; } = 24000;
}

/// <summary>How much town the generator lays down, and how coarsely it is spaced.</summary>
internal sealed class CityGenFigures
{
    public int AttemptBound { get; init; } = 16;
    public int Buildings { get; init; } = 20;
    public int Pedestrians { get; init; } = 10;
    public int LotSpacesMin { get; init; } = 3;
    public int LotSpacesMax { get; init; } = 8;
    public int LotsMin { get; init; } = 3;
    public int LotsMax { get; init; } = 24;
    public float BlockSpacingAlongMinM { get; init; } = 95f;
    public float BlockSpacingAlongMaxM { get; init; } = 120f;
    public float BlockSpacingAcrossMinM { get; init; } = 120f;
    public float BlockSpacingAcrossMaxM { get; init; } = 135f;

    /// <summary>A density, not a count.</summary>
    public float WildScatterFill { get; init; } = 0.425f;

    public float WildScatterTreeShare { get; init; } = 0.45f;
}

/// <summary>Tolerances the walkable and drivable graphs are built to.</summary>
internal sealed class NetworkFigures
{
    public float FootGraphStubPruneM { get; init; } = 2f;
    public float FootGraphNodeWeldM { get; init; } = 0.25f;
    public float SplineToleranceWalkedM { get; init; } = 0.1f;
    public float SplineToleranceRoadM { get; init; } = 0.25f;
}
