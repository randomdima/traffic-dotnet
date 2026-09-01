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

    /// <summary>
    /// One marked traffic lane, in car widths — 3.6 m at the shipped car, which is the width every road in
    /// this town is laid at. <b>A lane is the standard, and the carriageway is two of them</b>
    /// (<see cref="SimConfig.LanesPerCarriageway"/>): a road wide enough for a car to pass another is a road
    /// nothing on it has to negotiate, and a town of roads that each chose their own width is a town where
    /// no figure quoted against a lane means anything.
    /// </summary>
    public float LaneWidthInCarWidths { get; init; } = 1.8f;

    public float IntersectionCornerRadiusInCarWidths { get; init; } = 2.5f;

    /// <summary>
    /// How far back along an arm a kerb fillet may run from the carriageway's own edge, in car widths.
    /// <b>It is what bounds a corner at a skew junction</b>: two arms meeting at an angle have kerbs that
    /// cross far outside the mouth, so a fillet turned on the full radius there lets go of the kerb tens of
    /// metres down the road — and the crossing, the bar and the straight they stand on all follow it out.
    /// A square junction is nowhere near this and keeps the full radius.
    /// </summary>
    public float JunctionFilletReachInCarWidths { get; init; } = 3f;

    /// <summary>
    /// One walking lane, in bodies — the pavement's own lane, and the pavement is two of them, one each way
    /// (<see cref="SimConfig.PavementWidthM"/>). Two bodies wide, so somebody stepping round somebody coming
    /// the other way does it inside the walk rather than over the kerb.
    /// </summary>
    public float WalkingLaneInPersonDiameters { get; init; } = 2f;

    public float EdgeLineWidthM { get; init; } = 0.3f;

    /// <summary>One painted line: a lane dash, a bay stroke. A zebra's bar is twice it and a stop bar is the plan's own.</summary>
    public float PaintLineWidthM { get; init; } = 0.25f;

    /// <summary>The dashed lane centreline: how long a dash is and how long the gap after it.</summary>
    public float LaneDashLengthM { get; init; } = 2f;

    public float LaneDashGapM { get; init; } = 2f;

    /// <summary>A zebra's bars: how wide one is and how far apart they are laid across the carriageway.</summary>
    public float ZebraStripeWidthM { get; init; } = 0.5f;

    public float ZebraStripePitchM { get; init; } = 1f;

    /// <summary>
    /// A crossing as it is laid at a junction: how deep the band is along the road it crosses, and how far
    /// past the ground the junction itself reaches it stands. <b>Every map that lays paint lays it here</b>,
    /// so a crossing on the exam and a crossing in a generated town are the same distance off the box.
    /// </summary>
    /// <remarks>
    /// <b>The setback is measured from where that arm's own kerb fillet lets go of the kerb</b>
    /// (<see cref="SimConfig.JunctionArmReachM"/>) and never from the node, so a skew junction's paint stands
    /// off the ground the junction actually takes rather than off an average of every junction in the town.
    /// It is therefore a stride of carriageway and not a slack allowance for the skew: enough that the
    /// zebra's end bars stand on straight kerb rather than on the corner's own arc, and no more.
    /// </remarks>
    public float CrossingDepthM { get; init; } = 4f;

    public float CrossingSetbackM { get; init; } = 1f;

    /// <summary>And the bar behind it: how thickly it is painted, and how far behind the crossing it stands.</summary>
    public float StopBarThicknessM { get; init; } = 0.4f;

    public float StopBarSetbackM { get; init; } = 1f;

    public float ParkingSpaceMarginInCarWidths { get; init; } = 0.5f;

    /// <summary>
    /// How far before a bay a way in leaves its lane: where a car drops to manoeuvring pace, and the
    /// run-in the entry template needs to hold its own radius. It is also how far beyond a car park's
    /// frontage the road is cut for it, so the run-in stands inside the section's own stretch.
    /// </summary>
    public float ParkingStagedInCarLengths { get; init; } = 3f;

    /// <summary>
    /// <b>The least straight a parking template may end on</b>, so it does not end with the rack still
    /// wound on. It is a floor and not a target: the arcs take the lateral they need and whatever is left
    /// over between the bay and the lane is the straight, which for a bay standing well off its lane is
    /// metres.
    /// </summary>
    /// <remarks>
    /// <b>It is bought with the oncoming lane, which is what makes it small</b> (GEN-4j, P-14). A floor
    /// above what the geometry affords is not free straight — it is met by swinging the template away from
    /// the bay first, and every metre of that swing is ground taken off the far side of the street. On the
    /// shipped lot a quarter of a car length here cost a 27° swing, five metres of extra path and a body
    /// over the centreline, and bought <em>no measurable squareness at all</em>: the follower hands a car on
    /// about twenty degrees out either way and settles the rest at rest
    /// (<c>ManeuverTests.ACarThatHasParkedStandsSquareInItsBay</c>).
    /// </remarks>
    public float ParkingStraightensUpInCarLengths { get; init; } = 0.05f;

    /// <summary>The shortest stretch a cut laid for a car park may leave standing on either side of itself.</summary>
    public float ParkingSectionShortestStretchInCarLengths { get; init; } = 2f;
}

/// <summary>A building as a trip uses it.</summary>
internal sealed class BuildingFigures
{
    /// <summary>The one strip of ground somebody is meant to stand in.</summary>
    public float FrontGapM { get; init; } = 1f;

    public float DwellMinS { get; init; } = 0f;
    public float DwellMaxS { get; init; } = 10f;
}

/// <summary>Street furniture: the unit every other static size is quoted against.</summary>
internal sealed class PropFigures
{
    public float DiameterInCarWidths { get; init; } = 1f;
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
    /// Resistance to travel over a surface, <b>as a coefficient</b> — the raw term, dimensionless, against
    /// which the deceleration a wheel actually feels is derived (<see cref="SimConfig.GrassDragMps2"/> and
    /// its pair). It is spent outside the traction budget so it costs nothing a tyre would have used for
    /// cornering: terrain slows by friction, never by a speed multiplier. Tarmac's is a feel figure rather
    /// than a physical one — a real coastdown is ≈ 0.023 and a car that coasts the length of the town reads
    /// as floating. Grass is deep turf, enough that a lawn is somewhere a car struggles and not so much
    /// that it strands one there.
    /// </summary>
    public float GrassResistance { get; init; } = 0.2915f;

    public float PavedResistance { get; init; } = 0.1223f;
    public float WaterResistance { get; init; } = 0.3466f;

    /// <summary>
    /// How easily tarmac takes a permanent mark, as a factor on <see cref="MarkFigures.PowerM2S3"/>. Softer
    /// than the bar's own figure, so a slide that only just clears it still shows as a scuff. Grass has no
    /// factor: it records the wheel <em>ploughing</em> it rather than a slide at all, and takes the bar.
    /// </summary>
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

    /// <summary>
    /// What every wheel writes on a map laid to be driven in circles and read off the ground afterwards —
    /// the skidpad, and nothing else this build ships. It is the same kind of figure as
    /// <see cref="PloughFloor"/>: a floor under the intensity rather than a second way of marking, so a
    /// wheel that is genuinely sliding still darkens above it and the slide is still visible in the track.
    /// </summary>
    /// <remarks>
    /// Fainter than a slide on purpose. The track is there to be measured against a circle drawn over it,
    /// and a track as black as a skid would be a picture of four wheels all sliding.
    /// </remarks>
    public float PadFloor { get; init; } = 0.5f;

    /// <summary>
    /// How many marks the town remembers before the oldest is overwritten. Scenery only: nothing samples a
    /// mark and no agent sees one.
    /// </summary>
    /// <remarks>
    /// <b>The skidpad is what sets it.</b> A town's traffic marks the road rarely and any figure would do
    /// there; the pad has ninety-odd cars writing with every wheel at once, and a circle the ring wrapped
    /// halfway round is a circle nobody can measure. This is a couple of turns of the whole pad.
    /// </remarks>
    public int Capacity { get; init; } = 80000;
}

/// <summary>How coarsely a town is spaced.</summary>
/// <remarks>
/// <b>The block is this project's unit of town distance</b>: how far somebody will walk rather than drive,
/// what a blocked way is priced at, and how near its own building a service vehicle counts as home are all
/// quoted in it, so moving this figure moves all of them together.
/// </remarks>
internal sealed class CityGenFigures
{
    public float BlockSpacingAlongMinM { get; init; } = 95f;

    /// <summary>The coarsest a district may be spaced. A district draws its blocks between the two.</summary>
    public float BlockSpacingAlongMaxM { get; init; } = 170f;

    /// <summary>
    /// How much longer a block is across its district's bearing than along it. <b>A block is a rectangle</b>
    /// — the traced cities' are, and a town of square blocks is a town with half as much road again as it
    /// needs, all of which is then laid, walked, driven and drawn.
    /// </summary>
    public float BlockAspectMin { get; init; } = 1.2f;

    public float BlockAspectMax { get; init; } = 2.2f;

    /// <summary>
    /// How near two of a kind have to stand before they are one thing rather than two (GEN-16): two nodes
    /// this far apart are one junction, and two car parks sharing a kerb this far apart are one car park.
    /// </summary>
    /// <remarks>
    /// <b>Authored rather than derived, and a town's figure rather than a car's.</b> It is wider than the
    /// ground two junctions' own discs and corners take, which is what a road between them is already
    /// refused for being shorter than — the point of this one is the gap that clears that floor and still
    /// reads as one place: a stride of pavement between two car parks, or a pair of boxes a car crosses one
    /// straight after the other.
    /// </remarks>
    public float LocalityM { get; init; } = 30f;

    /// <summary>
    /// How far off each other a junction's arms must stand (GEN-13). <b>Sixty degrees</b>: below it two
    /// carriageways meeting at a node lie against each other rather than crossing, and the fillet, the
    /// crossing and the bar on one arm are laid over the other. It is also the sharpest corner any junction
    /// turns, so it is what a road's straight stub has to be long enough for
    /// (<see cref="SimConfig.JunctionArmReachM"/>).
    /// </summary>
    public float ArmsApartMinDeg { get; init; } = 60f;

    /// <summary>
    /// The speed a street and an arterial are laid for, which is what their tightest bend is allowed to be:
    /// the radius is <see cref="SimConfig.CarCorneringRadiusM"/> of it on tarmac and is never authored.
    /// </summary>
    public float StreetDesignSpeedMps { get; init; } = 14f;

    public float ArterialDesignSpeedMps { get; init; } = 22f;

    /// <summary>
    /// How far off its own chord a street may wander, as a share of the district's block spacing. <b>It is
    /// bounded by the block and not by the road</b> — two streets a block apart that each wandered half a
    /// block would meet, and a town whose streets cross where no junction is is not a town.
    /// </summary>
    public float StreetWanderInBlocks { get; init; } = 0.06f;

    /// <summary>The same for a district laid as a strict grid, where a street is very nearly a chord.</summary>
    public float GridWanderInBlocks { get; init; } = 0.012f;

    /// <summary>How many virtual nodes a road's middle span may carry. Odesa's most-bent road holds nine arcs.</summary>
    public int WanderNodesMost { get; init; } = 3;

    /// <summary>A building's footprint, drawn between the two. Odesa's run 10 m to 20 m a side.</summary>
    public float BuildingSideMinM { get; init; } = 10f;

    public float BuildingSideMaxM { get; init; } = 20f;

    /// <summary>How many people a building holds, which is what the town's roster is spread over.</summary>
    public int BuildingCapacity { get; init; } = 3;

    /// <summary>
    /// How many bays one car park holds, drawn between the two — which is how much frontage a lot takes
    /// (GEN-4b). <b>A car park is a handful of spaces beside a street and never an apron</b>: the widest
    /// one here is six bays, 24 m of kerb, which is about the frontage of one building.
    /// </summary>
    public int BaysPerLotFewest { get; init; } = 3;

    public int BaysPerLotMost { get; init; } = 6;

    /// <summary>
    /// The longest deck a town builds. <b>A crossing wider than this is one the town does not make</b>
    /// (GEN-14a): a road that would need a longer span stops at the bank instead, and what that leaves
    /// unreachable is deleted with its own piece. It is authored rather than derived — how much bridge a
    /// small town can afford is a fact about the town and not about any car that drives over it.
    /// </summary>
    public float BridgeDeckLongestM { get; init; } = 150f;

    /// <summary>
    /// How far the shore runs back from the water it belongs to. <b>The strip between the water and whatever
    /// the town does with the ground</b>: nothing is scattered on it and nothing is built on it, because it
    /// is not the grass those take.
    /// </summary>
    public float ShoreWidthM { get; init; } = 8f;

    /// <summary>
    /// How wide the line along each of the shore's own edges is drawn — the one where it meets the grass and
    /// the one where it meets the water. <b>The width this town draws an edge at</b>
    /// (<see cref="RoadFigures.EdgeLineWidthM"/>) is a road's figure and this is the shore's, because the two
    /// are read at different distances.
    /// </summary>
    public float ShoreEdgeWidthM { get; init; } = 1f;

    /// <summary>
    /// How finely a shoreline is sampled: the most a chord may stand off the curve it is drawn through.
    /// <b>Half a cell</b>, which is the finest the ground under it is classified, so the drawn bank and the
    /// classified one agree everywhere and the outline is as smooth as the map can tell.
    /// </summary>
    public float ShoreChordToleranceM { get; init; } = 0.5f;

    /// <summary>
    /// The lattice the props are scattered on — one candidate a cell, jittered inside it. <b>It is the
    /// town's prop density, and density goes as its square</b>: the scatter thins by a fifth for every
    /// twelve centimetres in a metre this grows by.
    /// </summary>
    public float PropSpacingM { get; init; } = 7.4f;

    /// <summary>
    /// The band of grass a prop laid along a kerb stands in, measured out from the pavement's own outer
    /// edge (GEN-6b) — <b>up against the walk rather than back off it</b>, because what a verge is for is
    /// to be seen from the street. <b>The near edge is what the ground affords rather than what a figure
    /// promises</b>: a prop owes its whole girth to grass (GEN-6a), so a narrow look reaches the near edge
    /// of the band and a wide one is pushed out by its own width.
    /// </summary>
    public float PropVergeNearM { get; init; } = 0.5f;

    public float PropVergeFarM { get; init; } = 2f;

    /// <summary>
    /// How far apart along a kerb the verge pass takes its candidates, each jittered inside its own step.
    /// <b>It is shorter than the props are wide</b>, so what spaces a verge is the props' own girth against
    /// each other (GEN-6c) and not the pitch: a stretch of kerb carries what fits along it.
    /// </summary>
    public float PropVergePitchM { get; init; } = 1.5f;

    /// <summary>
    /// The grass two props leave between them, girth to girth (GEN-6c). <b>Not touching is not enough</b>:
    /// a prop is a picture as well as a disc, and a row of them laid rim to rim along a kerb reads as one
    /// thing rather than as several — a verge wants to be seen through.
    /// </summary>
    public float PropApartM { get; init; } = 0.5f;

    /// <summary>
    /// How far a wild prop keeps off the walk and the car parks (GEN-6b) — <b>past the verge and not up
    /// against it</b>, so the strip between the two passes reads as the edge of the town rather than as one
    /// scatter that happens to change what it is made of.
    /// </summary>
    public float PropWildStandOffM { get; init; } = 7f;

    /// <summary>
    /// How much of a car park's verge is furniture rather than planting, and how much of the planting on
    /// any verge is drawn from the wild set instead. <b>A verge is not a flower bed end to end</b>: a town
    /// whose every kerb carried only the things it plants reads as a catalogue laid out along the street.
    /// </summary>
    public float PropFurnitureShare { get; init; } = 0.5f;

    public float PropWildOnAVergeShare { get; init; } = 0.5f;

    /// <summary>
    /// The sizes a prop is drawn at. <b>The catalogue matches a prop by its kind and then by its size</b>,
    /// so a kind nothing was drawn for, or a size no variant is near, is a prop the town has no picture of.
    /// </summary>
    public float PropDiameterMinM { get; init; } = 0.6f;

    public float PropDiameterMaxM { get; init; } = 2.2f;

    /// <summary>
    /// The widest a <em>wild</em> prop is drawn, wherever it stands — the great trees are the only art
    /// authored past the band above, and a wild look on a verge is a street tree. <b>A band is the set's
    /// own</b>: asking for a size nothing in a set was drawn near gets the nearest look at the size that
    /// was asked for, which is a planter stretched to the size of an oak.
    /// </summary>
    public float PropWildDiameterMaxM { get; init; } = 3f;
}

/// <summary>Tolerances the walkable and drivable graphs are built to.</summary>
internal sealed class NetworkFigures
{
    public float FootGraphStubPruneM { get; init; } = 2f;
    public float FootGraphNodeWeldM { get; init; } = 0.25f;
    public float SplineToleranceWalkedM { get; init; } = 0.1f;
}
