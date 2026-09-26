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

    /// <summary>
    /// <b>How far apart the points of the ribbon atlas stand</b>, in car widths (TER-4c.4) — the lattice a
    /// body's collider is read against to find the ways it stands on.
    /// </summary>
    /// <remarks>
    /// <b>A point is sampled and never a cell</b>, so a coarse lattice claims nothing that is not really
    /// overlapped; what it costs is the overlaps it can miss, which are those thinner than its diagonal. A
    /// quarter of a car keeps that under half a car's width — a car straddling the line between two lanes is
    /// on both — and holds the atlas to a few bytes per square metre of road.
    /// </remarks>
    public float RibbonLatticeInCarWidths { get; init; } = 0.25f;

    /// <summary>
    /// <b>How far inside both of two ribbons their shared ground has to lie before the two are marked</b>, in
    /// car widths (TER-5c). Ribbons laid edge to edge — the two lanes of a carriageway, a lane and the
    /// connector it hands over to — share an edge and no ground, and this keeps rounding in the lines from
    /// marking them.
    /// </summary>
    public float RibbonTouchInCarWidths { get; init; } = 0.02f;

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

    /// <summary>
    /// <b>Where a pedestrian node stands off the end of a road</b> (WLK-2): how far back along the road from
    /// the point its lanes hand the car over to the junction (TER-5d), and how far off the carriageway's own
    /// edge.
    /// </summary>
    /// <remarks>
    /// <b>The owner's figures, and a place rather than a derivation.</b> A node is the corner a walk turns at
    /// the mouth of a street, and where that corner stands is a choice about the town — far enough back that
    /// the walk is clear of the ground the junction's movements are driven across, far enough out that it
    /// stands on the walk rather than in the channel.
    /// </remarks>
    public float FootNodeBackM { get; init; } = 4f;

    /// <inheritdoc cref="FootNodeBackM"/>
    public float FootNodeAsideM { get; init; } = 2.5f;

    /// <summary>
    /// <b>How far clear of the end of a road's kerb a pedestrian node stands</b>, away from the junction
    /// along the road (WLK-1). The kerb end is where the town's outline stops following the road
    /// (<c>CityGen.KerbEnds</c>), so this is the gap between that place and where a walk beside the road is
    /// cut.
    /// </summary>
    /// <remarks>
    /// <b>The owner's figure, and a place rather than a derivation.</b> It is measured off the kerb's own
    /// end rather than off the lane's, which is what <see cref="FootNodeBackM"/> measures and why the two
    /// are different numbers: a road's line stops at the mouth of the box, and its kerb gives up the
    /// boundary some way short of that wherever the mouth widens first.
    /// </remarks>
    public float FootNodeClearM { get; init; } = 4f;

    /// <summary>
    /// <b>How near a road's two pedestrian stations have to stand for the road to be crossed once</b>
    /// (WLK-10a): a street short enough that its two ends' walks are cut within this of each other is
    /// crossed once instead, midway between the two places, and met by the traffic from both hands. <b>Only
    /// the paint moves</b>: the stations stay where they are, so the street is still cut at both ends and
    /// still held at both (TER-6).
    /// </summary>
    /// <remarks>
    /// <b>The owner's figure, and measured between the stations rather than between the kerb ends.</b> What
    /// it weighs is how far a walker would go out of their way to use the other crossing, which is the gap
    /// between the places they can stand — the kerb ends are <see cref="FootNodeClearM"/> further apart than
    /// that at either hand, and a figure read off them would say a different thing every time that one moved.
    /// </remarks>
    public float CrossedOnceBelowM { get; init; } = 25f;

    /// <summary>
    /// <b>How far off the end of a zebra a walk may stand and still be the pavement that zebra is reached
    /// on</b> (WLK-15): the crossing is cut into every course passing within this of where its paint ends,
    /// and into nothing at all where the nearest of them stands further off than that.
    /// </summary>
    /// <remarks>
    /// <b>A reach and not a tolerance.</b> The paint stops at the edge of the carriageway and every lane of
    /// the walk stands its own distance beyond the driven ground's own boundary, which at a mouth reaches
    /// past that edge — so the two are a stride apart down a straight street and further wherever the ground
    /// a junction is driven over widens, or wherever a course was rounded back off a tight corner
    /// (<see cref="WalkRoundedM"/>). What it refuses is the crossing whose nearest walk is across the road or
    /// round the block, which is a way nobody takes and a line laid over whatever stands between.
    /// <b>It is asked where the line passes the junction</b> and not at the hand-over point a setback along
    /// it lands on: the setback says where to hand over, and reading the reach off it would refuse a course
    /// the junction stands beside for the shape of the line further on. <b>How many are refused and how far
    /// off the nearest of them stood is the census's to report.</b>
    /// </remarks>
    public float CrossingMeetsTheWalkWithinM { get; init; } = 6f;

    /// <summary>
    /// <b>How far along the kerb from a node the walk down its road and the walk round its junction are
    /// handed over</b> (WLK-9) — measured along the driven ground's own boundary, so it follows a corner
    /// round rather than running off down the arm's centreline.
    /// </summary>
    /// <remarks>
    /// <b>Its own figure and not <see cref="FootNodeBackM"/>.</b> The two were one while they happened to
    /// agree, and they answer different questions: that one says where the corner of a street stands, and
    /// this says how far out of it a way is reached. Moving the node does not move the reach.
    /// </remarks>
    public float FootConnectorAlongM { get; init; } = 2.5f;

    /// <summary>
    /// <b>How near two pedestrian nodes have to stand to be one pedestrian junction</b> (WLK-3, WLK-15). Two
    /// crossings at one corner put their mouths on the same stretch of pavement, and a corner a walk can
    /// cross in two strides is one place to arrive at rather than two to choose between.
    /// </summary>
    /// <remarks>
    /// <b>Twice <see cref="FootConnectorAlongM"/>, which is the figure it answers to.</b> A junction hands
    /// over that far either side of itself along each lane of the walk, so two of them nearer than this have
    /// their own ground overlapping — and what stands between them is not a stretch anybody walks but a
    /// stride of pavement with a hand-over at each end of it. <b>It is authored rather than derived</b>: how
    /// near two places have to stand to be one place is a choice about the town, and the two figures agreeing
    /// today is what the choice came to and not an arithmetic one of them is bound by.
    /// </remarks>
    public float FootNodeMergeM { get; init; } = 5f;

    /// <summary>
    /// <b>How near the place a walking lane sets off from and the place it arrives at have to stand to be
    /// one place</b> (WLK-12). A stride of walk between two places a stride apart is not a walk anybody
    /// takes, so the two ends are welded into the point between them and the ways either side of it meet
    /// there.
    /// </summary>
    /// <remarks>
    /// <b>Its own figure and not <see cref="FootNodeMergeM"/>, which is a question about corners.</b> That
    /// one asks how near two <em>nodes</em> stand, and answers it at the width of a corner a walk crosses in
    /// two strides; this asks how near the two ends of one <em>lane</em> stand, and a lane is welded only
    /// where there is nothing left of it at all — so the figure is a stride and not a corner.
    /// </remarks>
    public float FootConnectorMergeM { get; init; } = 1f;

    /// <summary>
    /// <b>The kerb: how far the concrete stands proud of the ground it bounds</b> (TER-3c.3). Two hundred
    /// millimetres, which is a kerbstone's face, and it is struck twice — once where the carriageway hands
    /// over to the walk and once where the walk hands over to the grass.
    /// </summary>
    /// <remarks>
    /// <b>A width and not a line's width.</b> <see cref="EdgeLineWidthM"/> and
    /// <see cref="PaintLineWidthM"/> are strokes a layer leaves of the one under it and are measured in what
    /// reads at a framing; a kerb is a thing the town is built of and is measured in what it is.
    /// </remarks>
    public float KerbWidthM { get; init; } = 0.2f;

    /// <summary>
    /// <b>How tightly any line struck off the driven ground's boundary is allowed to turn</b> (TER-3c.10):
    /// the radius of the ball every one of them is rolled with (<c>Core.Geometry.ArcOutset.Of</c>) — the
    /// kerb, the pavement's outer face and the courses a walking lane is a stretch of, all at the one figure
    /// so that no two of them disagree about the same corner.
    /// </summary>
    /// <remarks>
    /// <b>Under half a lane, which is what keeps it a rounding rather than a rubbing out.</b> A feature
    /// narrower than twice the radius does not survive the roll: at this figure that is 2.8 m against a
    /// lane's <see cref="LaneWidthInCarWidths"/> of car (3.6 m), so no mouth a car is driven through is ever
    /// closed over and no ribbon of tarmac is ever swallowed. <b>Raising it to 1.8 m was tried and is not
    /// here</b>: twice that is a lane exactly, and what it cost Odesa was the tarmac — the kerb's own ring
    /// closed over every carriageway in the town, and a quarter of the kerb ends the walk is cut at went
    /// with it (510 crossings to 376). <b>And it is a radius and not a share of any distance</b>, so the
    /// boundary itself is rounded by it as much as the pavement's outer face is.
    /// </remarks>
    public float LineRoundedM { get; init; } = 1.4f;

    /// <summary>
    /// <b>And how tightly the line a walking lane is a stretch of may turn</b> (TER-3c.10, WLK-1): the same
    /// roll at the courses' own figure, which is <em>not</em> the one the ground's layers are struck at
    /// (<see cref="LineRoundedM"/>) — a course is a line a body is held on and not a thing the town is built
    /// of, so what it owes is a walk nobody has to pick their way round rather than agreement with a
    /// kerbstone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It fills and never cuts</b> (<c>Core.Geometry.ArcOutset.Corners.Filled</c>), which is what makes it
    /// a figure with no bound: a corner the course turns <em>into</em> — the notch an offset's own fold
    /// leaves — is filled at this radius, and a corner it turns away at comes back as the arc of the distance
    /// moved however wide this is asked for. <b>So no radius pulls a walk towards the tarmac</b> and none
    /// takes a course's closure, which a roll that cut the away-corners did at a tenth past the offset and
    /// the ground's radius together.
    /// </para>
    /// <para>
    /// <b>What it costs instead is the pockets</b>: a dip in a course narrower than twice this is closed over
    /// rather than walked into, so at a sharp fork the walk stands off the apex by about this much and a
    /// junction there hands over further from its own course. <b>How far is the census's to report</b>, and
    /// what may not follow from it is a crossing left unreached (WLK-15).
    /// </para>
    /// </remarks>
    public float WalkRoundedM { get; init; } = 2.4f;

    public float EdgeLineWidthM { get; init; } = 0.3f;

    /// <summary>One painted line: a lane dash, a bay stroke. A zebra's bar is twice it and a stop bar is the plan's own.</summary>
    public float PaintLineWidthM { get; init; } = 0.25f;

    /// <summary>The dashed line between a road's two lanes: how long a dash is and how long the gap after it.</summary>
    public float LaneDashLengthM { get; init; } = 2f;

    public float LaneDashGapM { get; init; } = 2f;

    /// <summary>
    /// <b>How deep a crossing's band is along the road it crosses, and how far short of its arm's own end
    /// it stands</b> (TER-6). The end is the line that arm's lanes hand the car over to the junction on
    /// (TER-5d), so a skew junction's paint stands off where its own arm stops being driven rather than at a
    /// distance every arm of the town shares.
    /// </summary>
    /// <remarks>
    /// <b>Four metres of band and two of margin are the owner's figures</b>: two strides of carriageway to
    /// walk over, and a car's length of road between the walk and the ground the junction's movements are
    /// driven across — so a walker on the paint stands clear of the traffic turning through the corner.
    /// </remarks>
    public float CrossingDepthM { get; init; } = 4f;

    /// <inheritdoc cref="CrossingDepthM"/>
    public float CrossingSetbackM { get; init; } = 2f;

    /// <summary>A zebra's bars: how wide one is and how far apart they are laid across the carriageway.</summary>
    public float ZebraStripeWidthM { get; init; } = 0.5f;

    public float ZebraStripePitchM { get; init; } = 1f;

    /// <summary>And the bar behind it: how thickly it is painted.</summary>
    public float StopBarThicknessM { get; init; } = 0.4f;

    /// <summary>
    /// <b>The clear road between a crossing's near edge and the bar behind it</b> (TER-6): a metre, so a car
    /// held at the paint is stopped short of the walk rather than on it, and a walker stepping off the zebra
    /// is not stepping into a bumper.
    /// </summary>
    /// <remarks>
    /// <b>A gap and not a distance to the bar's middle</b>, which is what makes the arm's whole bundle of
    /// paint one sum (<see cref="SimConfig.ArmPaintM"/>) rather than a figure with the bar's thickness
    /// folded into it twice.
    /// </remarks>
    public float StopBarSetbackM { get; init; } = 1f;

    /// <summary>
    /// <b>The arrow painted on a lane behind its bar</b> (TER-6a): how long one is down its lane, tail to the
    /// far end of whichever branch reaches furthest, and <b>the least of that which is shaft</b> — what a
    /// straight arrow leaves in front of its fork.
    /// </summary>
    /// <remarks>
    /// <b>Every arrow is the length, and the shaft is what the branches did not spend</b> (TER-6a): a turn
    /// reaches less far down the lane for the room it takes, so its shaft is the longer and the two arrows end
    /// together. <b>The run after the fork is the rest of the length</b> — a bend or a straight, and the head
    /// — so the three figures are not independent and a shaft as long as the arrow leaves no arrow.
    /// </remarks>
    public float LaneArrowLengthM { get; init; } = 3.3f;

    /// <inheritdoc cref="LaneArrowLengthM"/>
    public float LaneArrowShaftM { get; init; } = 1.6f;

    /// <summary>How wide the shaft and every branch off it are painted — a mark a driver reads at a glance, so several times a line.</summary>
    public float LaneArrowShaftWidthM { get; init; } = 0.33f;

    /// <summary>And the head each branch ends in: how far it reaches and how far across the barbs stand.</summary>
    public float LaneArrowHeadLengthM { get; init; } = 0.7f;

    /// <inheritdoc cref="LaneArrowHeadLengthM"/>
    public float LaneArrowHeadWidthM { get; init; } = 0.88f;

    /// <summary>
    /// <b>How far across its lane a turning arrow reaches</b>, as a share of that lane's width — the figure
    /// the bend's own radius is solved out of (TER-6a), so an arrow stays on the ground its lane is driven on
    /// whatever the movement behind it turns through.
    /// </summary>
    /// <remarks>
    /// <b>The far corner of the head and not the middle of it</b>: the whole glyph is inside this, so the
    /// clear lane either side of the widest arrow is the rest of the half-lane — 0.4 m at the shipped
    /// figures, which is a lane's own line and more.
    /// </remarks>
    public float LaneArrowReachAcrossInLaneWidths { get; init; } = 0.38f;

    /// <summary>
    /// The clear road between the bar and the arrow behind it, as the bar's own setback is the clear road in
    /// front of it. <b>Every arrow stands at it</b>, being the one length whatever it says.
    /// </summary>
    public float LaneArrowSetbackM { get; init; } = 1.5f;

    /// <summary>
    /// The most an arrow's bend is drawn through, however sharply the movement itself turns: a head laid past
    /// a quarter turn points back down the road at the driver reading it.
    /// </summary>
    public float LaneArrowBendMostDeg { get; init; } = 90f;

    /// <summary>
    /// <b>How much of a car's width a bay leaves clear at each end of it</b>: the room a car needs to get
    /// itself square into the space and out again, which at a parallel bay is what it reverses into.
    /// </summary>
    public float ParkingSpaceMarginInCarWidths { get; init; } = 0.5f;

    /// <summary>
    /// <b>And how much it leaves clear down each side</b> — a door's swing, which is the whole of what a bay
    /// is wider than the car in it.
    /// </summary>
    /// <remarks>
    /// <b>It is the side and never the end</b>, and the two were one figure. A parallel bay is entered by
    /// reversing into it, so what sizes its length is a manoeuvre; nothing manoeuvres sideways, so read at
    /// the same figure a space came out wider than the traffic lane its way is driven out of — and a way
    /// lays the ground its space is wide the whole distance the two run together, so every bay in the town
    /// stood a lip of tarmac past the kerb it hangs off. Odesa drew four hundred and sixty-one steps of
    /// perimeter onto that lip and a hundred and forty of them were shorter than a hand.
    /// </remarks>
    public float ParkingSpaceSideMarginInCarWidths { get; init; } = 0.25f;

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
    /// <b>It is bought with the oncoming lane, which is what makes it small</b> (GEN-4j). A floor
    /// above what the geometry affords is not free straight — it is met by swinging the template away from
    /// the bay first, and every metre of that swing is ground taken off the far side of the street. On the
    /// shipped lot a quarter of a car length here cost a 27° swing, five metres of extra path and a body
    /// over the centreline, and bought <em>no measurable squareness at all</em>: the follower hands a car on
    /// about twenty degrees out either way and settles the rest at rest
    /// (<c>ManeuverTests.ACarThatHasParkedStandsSquareInItsBay</c>).
    /// </remarks>
    public float ParkingStraightensUpInCarLengths { get; init; } = 0.05f;

    /// <summary>The street a car park's frontage leaves beyond its own run-in, at either end of its road.</summary>
    public float ParkingFrontageClearInCarLengths { get; init; } = 2f;
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

    /// <summary>
    /// How wide a bucket the ground's own broad phases are laid over — the roads, the junction discs, the
    /// kerb fillets and the car parks. Near a road's own width, so a query on a street looks at the street
    /// and its neighbours and not at the block.
    /// </summary>
    public float GroundBucketM { get; init; } = 8f;

    /// <summary>
    /// How finely the ground is walked by a caller that has to <em>sample</em> it — a walk checking that a
    /// stretch stays on ground a person may stand on, a camera looking for the nearest road. It is a step
    /// and never a resolution: what the ground is at a point is exact wherever it is asked.
    /// </summary>
    public float GroundStepM { get; init; } = 1f;
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
    /// How many marks the town remembers before the oldest is overwritten. Scenery only: nothing samples a
    /// mark and no agent sees one.
    /// </summary>
    /// <remarks>
    /// <b>Sized for a crowd marking at once, not for a town.</b> A town's traffic marks the road rarely and
    /// any figure would do there; this is a couple of turns of ninety-odd cars writing with every wheel, so
    /// a circle is not overwritten halfway round before it can be measured.
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
    /// How far one one-way street stands off the next (GEN-18), which is what scatters them evenly over a
    /// town rather than gathering them into a district.
    /// </summary>
    /// <remarks>
    /// <b>Wider than the coarsest block a district is laid at</b> (<see cref="BlockSpacingAlongMaxM"/>), so
    /// no block has two of them round it and a driver meets one every few blocks wherever they are in the
    /// town. Authored: it is a spacing between two things on the ground, like <see cref="LocalityM"/>, and
    /// not a share of anything the town came out with.
    /// </remarks>
    public float OneWayApartMinM { get; init; } = 250f;

    /// <summary>
    /// The speed a roundabout's circulating carriageway is laid for (GEN-19), which is the tightest circle
    /// one may be: the radius is <see cref="SimConfig.CarCorneringRadiusM"/> of it on tarmac and is never
    /// authored. <b>Slower than a street</b>, because the whole of a roundabout is one corner.
    /// </summary>
    public float RoundaboutDesignSpeedMps { get; init; } = 8f;

    /// <summary>
    /// How far one roundabout stands off the next (GEN-19), so a district's exits do not all become one.
    /// </summary>
    /// <remarks>
    /// <b>Wider again than the one-way scatter</b> (<see cref="OneWayApartMinM"/>): a roundabout is the
    /// largest thing a junction can be and a town that turned every exit into one reads as a ring road
    /// rather than as a town. Authored, like every other spacing between two things on the ground.
    /// </remarks>
    public float RoundaboutApartMinM { get; init; } = 400f;

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
    /// The speed a movement through a junction is laid for, which is what the tightest turn across a box is
    /// allowed to be: the radius is <see cref="SimConfig.CarCorneringRadiusM"/> of it on tarmac and is never
    /// authored. <b>A junction's own and not the roundabout's</b> — a box is entered off a street and left
    /// onto one, and retuning what a car may hold across it has no business moving every ring in the town.
    /// </summary>
    public float JunctionDesignSpeedMps { get; init; } = 4f;

    /// <summary>
    /// <b>How far off a node's own centre a lane ends</b> (TER-5d). Every arm of every junction stands its
    /// connection points this far out, and the disc the junction is drawn on follows from it
    /// (<see cref="SimConfig.JunctionRadiusM"/>) rather than the other way round — a disc sized off the arms
    /// and a standoff read back off the disc is one relation stated twice.
    /// </summary>
    /// <remarks>Unproven. It is the first figure a town laid too tight or too baggy is retuned by.</remarks>
    public float ConnectionStandoffM { get; init; } = 6f;

    /// <summary>
    /// How far off the chord to its neighbour an arm's bearing may be drawn. <b>It is what makes a junction
    /// a shape rather than a crossroads</b>: the two ends of a link are drawn independently, so the road
    /// between them has two bearings to satisfy and they do not agree.
    /// </summary>
    /// <remarks>
    /// Unproven, and bounded from above by GEN-13: two arms drawn towards each other close the angle
    /// between them by twice this, so a bound past half the least spread would let a node lay two arms
    /// lying against each other.
    /// </remarks>
    public float ConnectionJitterDeg { get; init; } = 20f;

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

    /// <summary>
    /// <b>How often the pavement's outer face is offered a building</b> (GEN-54). <b>Shorter than the
    /// narrowest thing the catalogue draws</b>: what spaces two neighbours is then their own padding
    /// against each other rather than the step, so a stretch of kerb carries what fits along it — which is
    /// the opposite of what a verge wants of its own step (<see cref="PropVergePitchM"/>, GEN-6b).
    /// </summary>
    public float BuildingPitchM { get; init; } = 6f;

    /// <summary>
    /// <b>How much of its own footprint a building keeps clear around it</b> (GEN-3), as a share: the
    /// walkable padding every neighbour is held off by, so no pocket between two of them is too narrow to
    /// walk through.
    /// </summary>
    /// <remarks>
    /// <b>A share and not a length</b>, because what has to be walked round is the building: a quarter of a
    /// ten-metre terrace is two and a half metres of gap and a quarter of a twenty-metre block is five, and
    /// a fixed figure would be a passage beside the one and a seam beside the other.
    /// </remarks>
    public float BuildingPaddingShare { get; init; } = 0.25f;

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
    /// <b>How many buildings one car park stands the cars of</b> (GEN-53,
    /// <see cref="SimConfig.CarParksFor"/>): a town plans buildings and cuts a car park for every this many
    /// of them, so how many a map has is the map's and the share is the engine's (GEN-6).
    /// </summary>
    /// <remarks>
    /// <b>Four buildings to a handful of bays</b> is a kerb where a driver parks within a block of the door
    /// they are going to without the street being car parks end to end — the ratio a dense old town keeps,
    /// where a building's own cars mostly stand somewhere other than its own frontage.
    /// </remarks>
    public int BuildingsPerCarPark { get; init; } = 4;

    /// <summary>
    /// <b>How tight a bay is turned into, as a share of the circle the car itself turns on</b> (GEN-53,
    /// <see cref="SimConfig.CarParkTurnRadiusM"/>). One is the car's own parking circle; less than one is
    /// tighter than the nominal car can hold.
    /// </summary>
    /// <remarks>
    /// <b>Seven tenths is inside the steering lock, and deliberately</b>: what a car park's movements are
    /// laid for is the hook a car actually makes off a street at a walking pace, which is shorter than the
    /// circle its own front wheels describe at full lock from a standstill. A car tracking one of these
    /// lines has the wheel on its stop and runs a little wide of it; what that costs is the driver's to
    /// answer for and not the plan's.
    /// </remarks>
    public float BayTurnInParkingCircles { get; init; } = 0.7f;

    /// <summary>
    /// <b>How long a bay is</b> (GEN-53, <see cref="SimConfig.CarParkBayLengthM"/>): a length of ground,
    /// authored like every other length of ground in this file, and <b>longer than the longest vehicle the
    /// town draws</b> (<see cref="CarFigures.LongestLengthM"/>, which <c>SimConfigTests</c> holds it to).
    /// </summary>
    /// <remarks>
    /// <b>Five metres, which is the clearance and nothing else</b> — a bay square to the street is driven
    /// straight into off its own turn and straight out of again, where what sizes the parallel bay on a kerb
    /// is the reverse into it (<see cref="ParkingSpaceMarginInCarWidths"/>). A bay any longer is tarmac laid
    /// for nothing and an arm that much further out into the ground behind the street.
    /// </remarks>
    public float BayLengthM { get; init; } = 5f;

    /// <summary>
    /// <b>How far off the lane it is following a car on its way to a bay may be carried</b> (GEN-53). A
    /// movement holds the street straight until it turns in (<see cref="Spline.StraightArcStraightInto"/>)
    /// and a street is free to bend, so the two part company across a car park's box by the sagitta between
    /// them. This is how much of that is tolerable, and <b>everything about how straight a road has to be to
    /// carry a car park is read off it</b> (<see cref="SimConfig.CarParkCurvatureMax"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A fifth of a metre is a ninth of what the nominal car has spare in its lane</b> — nothing at the
    /// lane end and that much by the time the car turns in, which is a drift nobody watching the town would
    /// pick out and no width any other car is denied.
    /// </para>
    /// <para>
    /// <b>The longer the run the straighter the road</b>: the sagitta grows with the square of the run and a
    /// car park's longest run is its whole box and the reach of its rank again, so a wide car park asks for a
    /// much straighter road than a narrow one — which is the bound doing its job rather than an awkwardness
    /// in it.
    /// </para>
    /// </remarks>
    public float CarParkOffLaneMaxM { get; init; } = 0.2f;

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
    /// The rings are the bank the ground is answered off as well as the one drawn (TER-7), so this is how
    /// far both stand off the wave the water is laid from.
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
    /// face (GEN-6b) — <b>up against the walk rather than back off it</b>, because what a verge is for is
    /// to be seen from the street. <b>It is the prop's near rim that stands in the band and not its
    /// centre</b>, so a narrow look reaches the near edge and a wide one is pushed out by its own width: a
    /// prop owes its whole girth to grass (GEN-6a), and the concrete is a figure the boundary was struck at
    /// rather than something the ground answers with.
    /// </summary>
    /// <remarks>
    /// <b>The near edge all but touches the stone</b>: the walk's own kerb reaches half its width past the
    /// face (<see cref="SimConfig.WalkKerbOuterM"/>), so this leaves a hand's breadth of grass between the
    /// kerbstone and the nearest a prop's rim may come. <b>And the band is narrow</b>, because a band as wide
    /// as the verge scatters the row back off the street, where what a bin, a planter or a street tree is is
    /// a thing standing at the kerb.
    /// </remarks>
    public float PropVergeNearM { get; init; } = 0.2f;

    public float PropVergeFarM { get; init; } = 0.5f;

    /// <summary>
    /// How far apart along a kerb the verge pass takes its candidates, each jittered inside its own step.
    /// <b>It is longer than the props are wide</b>, so what spaces a verge is the step and not the props'
    /// own girth against each other (GEN-6c): a kerb carries a scatter with the town visible through it,
    /// rather than the unbroken run a pitch inside a girth fills every metre of.
    /// </summary>
    public float PropVergePitchM { get; init; } = 5f;

    /// <summary>
    /// The grass two props leave between them, girth to girth (GEN-6c). <b>Not touching is not enough</b>:
    /// a prop is a picture as well as a disc, and a row of them laid rim to rim along a kerb reads as one
    /// thing rather than as several — a verge wants to be seen through.
    /// </summary>
    public float PropApartM { get; init; } = 0.5f;

    /// <summary>
    /// How far a wild prop keeps off the town's paving (GEN-6b) — <b>past the verge and not up against
    /// it</b>, so the strip between the two passes reads as the edge of the town rather than as one scatter
    /// that happens to change what it is made of.
    /// </summary>
    public float PropWildStandOffM { get; init; } = 7f;

    /// <summary>
    /// How much of a verge is furniture rather than planting, and how much of the planting on any verge is
    /// drawn from the wild set instead. <b>A verge is not a flower bed end to end</b>: a town whose every
    /// kerb carried only the things it plants reads as a catalogue laid out along the street.
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
    /// <summary>
    /// <b>How near two ends of the walking network have to land to be one place</b> (WLK-1a): a quarter of a
    /// metre, which is the room a way drawn to a pedestrian node is allowed to have missed it by.
    /// </summary>
    public float FootGraphNodeWeldM { get; init; } = 0.25f;
    public float SplineToleranceWalkedM { get; init; } = 0.1f;
}
