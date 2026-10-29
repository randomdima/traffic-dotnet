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
    /// <b>How many points of the ribbon atlas stand across one cell of the grid</b> (TER-4c.4, SIM-8) — the
    /// level a body's collider is read against to find the ways it stands on. A power of two.
    /// </summary>
    /// <remarks>
    /// <b>A point is sampled and never a cell</b>, so a coarse lattice claims nothing that is not really
    /// overlapped; what it costs is the overlaps it can miss, which are those thinner than its diagonal. A
    /// quarter of a car keeps that under half a car's width — a car straddling the line between two lanes is
    /// on both — and holds the atlas to a few bytes per square metre of road. <b>It is the one level of the
    /// grid finer than the boundary's, and that is why</b>: nothing coarser holds a car to the lanes under it.
    /// </remarks>
    public int RibbonPointsAcrossGridCell { get; init; } = 16;

    /// <summary>
    /// <b>How far inside both of two ribbons their shared ground has to lie before the two are marked</b>, in
    /// car widths (TER-5c). Ribbons laid edge to edge — the two lanes of a carriageway, a lane and the
    /// connector it hands over to — share an edge and no ground, and this keeps rounding in the lines from
    /// marking them.
    /// </summary>
    public float RibbonTouchInCarWidths { get; init; } = 0.02f;

    /// <summary>
    /// <b>How far along a way a car's body is held either side of each of its wheels on it</b> (TER-4c.2): the stretch
    /// is the least and most of where its wheels stand on that way, widened by this at each end.
    /// </summary>
    /// <remarks><b>The owner's figure</b>, one for every build.</remarks>
    public float HeldAroundAWheelM { get; init; } = 1f;

    public float IntersectionCornerRadiusInCarWidths { get; init; } = 2.5f;

    /// <summary>
    /// One walking lane, in prop diameters — the pavement's own lane, and the pavement is two of them, one each way
    /// (<see cref="SimConfig.PavementWidthM"/>). At least two bodies wide, so somebody stepping round somebody coming
    /// the other way does it inside the walk rather than over the kerb.
    /// </summary>
    /// <remarks>
    /// <b>Off the town's scale and not the body's</b>, so a body resized for how closely walkers pass
    /// (<see cref="PersonFigures.DiameterInPropDiameters"/>) moves no pavement, crossing or building line.
    /// </remarks>
    public float WalkingLaneInPropDiameters { get; init; } = 1f;

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
    /// <b>How near a road's two pedestrian stations have to stand for the road to be crossed once</b>
    /// (WLK-10a): a street short enough that its two ends' walks are cut within this of each other is
    /// crossed once instead, midway between the two places, and met by the traffic from both hands. <b>Only
    /// the paint moves</b>: the stations stay where they are, so the street is still cut at both ends and
    /// still held at both (TER-6).
    /// </summary>
    /// <remarks>
    /// <b>The owner's figure, and measured between the stations rather than between the kerb ends.</b> What
    /// it weighs is how far a walker would go out of their way to use the other crossing, which is the gap
    /// between the places they can stand — the kerb ends are half a <see cref="CrossingDepthM"/> further apart than
    /// that at either hand, and a figure read off them would say a different thing every time that one moved.
    /// </remarks>
    public float CrossedOnceBelowM { get; init; } = 25f;

    /// <summary>
    /// <b>How far apart two crossings' ways across may turn and still be painted over each other as one band</b>
    /// (WLK-10b): within it their stripes run side by side, as a dual carriageway's two halves crossed at one
    /// station are; beyond it they cross each other, and the one of the two the walk needs less is not painted.
    /// </summary>
    public float ZebrasOneBandWithinDeg { get; init; } = 10f;

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
    /// <b>Its own figure and not <see cref="FootNodeBackM"/></b>, because they answer different questions: that one says where the corner of a street stands, and
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
    /// <b>How wide the shoulder beside a road with no walk is</b> (GEN-75): the gravel off its kerb, where a walked road
    /// has its walk, and on whose outer edge its barrier stands.
    /// </summary>
    public float ShoulderWidthM { get; init; } = 2.5f;

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
    /// closed over and no ribbon of tarmac is ever swallowed. <b>1.8 m is too far</b>: twice that is a lane
    /// exactly, and what it costs Odesa is the tarmac — the kerb's own ring closes over every carriageway in
    /// the town, and a quarter of the kerb ends the walk is cut at go with it (510 crossings to 376). <b>And it is a radius and not a share of any distance</b>, so the
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
    /// paint one sum of its four figures rather than a sum with the bar's thickness folded into it twice.
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
    /// <b>It is the side and never the end.</b> A parallel bay is entered by
    /// reversing into it, so what sizes its length is a manoeuvre; nothing manoeuvres sideways, so read at
    /// the same figure a space comes out wider than the traffic lane its way is driven out of — and a way
    /// lays the ground its space is wide the whole distance the two run together, so every bay in the town
    /// stands a lip of tarmac past the kerb it hangs off. Odesa draws four hundred and sixty-one steps of
    /// perimeter onto that lip and a hundred and forty of them are shorter than a hand.
    /// </remarks>
    public float ParkingSpaceSideMarginInCarWidths { get; init; } = 0.25f;

    /// <summary>
    /// <b>How far along its street a way out of a bay runs past its turn</b> (GEN-4f). <b>The longest vehicle's
    /// length</b>, because a line is handed over with the car at rest anywhere in its last car length, and a way
    /// that ended on the turn would hand a car over crosswise in the street; <b>and no more</b>, because a car
    /// reversing out drives all of it backwards up the street before it pulls away.
    /// </summary>
    public float ParkingRunOutInCarLengths { get; init; } = 1.1f;

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
    /// over the centreline. What squares a car is the wheel driving the shape as it bends, and a straightening where it
    /// still came to rest crooked (GEN-4i).
    /// </remarks>
    public float ParkingStraightensUpInCarLengths { get; init; } = 0.05f;

    /// <summary>The street a car park's frontage leaves beyond its own run-in, at either end of its road.</summary>
    public float ParkingFrontageClearInCarLengths { get; init; } = 2f;

    /// <summary>
    /// <b>How far from a junction or a crossing nobody parks at the kerb</b> (GEN-4n): the ten metres the rules of the
    /// road keep clear, so a car turning in or a walker stepping off the kerb sees down the street.
    /// </summary>
    public float KerbParkingClearM { get; init; } = 10f;

    /// <summary>
    /// <b>The gap a car parking at the kerb leaves to the bodies either side of it</b> (GEN-4n): room to get its nose
    /// out past the one in front, and the one behind its own.
    /// </summary>
    public float KerbParkingGapM { get; init; } = 1.25f;

    /// <summary>
    /// <b>How far past its place's middle a car parallel parking backs in</b> (GEN-4n) before it pulls forward to it: into
    /// the gap behind, so its nose swings in clear of the body in front. Less than <see cref="KerbParkingGapM"/>, which is
    /// all the room there is behind.
    /// </summary>
    public float KerbParkingBacksPastM { get; init; } = 0.8f;

    /// <summary>
    /// <b>How far the lane beside a kerb runs on either side of where a car parks along it</b> (GEN-4n): before it, room
    /// for a car come off a junction to settle into the lane and stop for its turn in; past it, room for one pulling out
    /// to get into the lane and across to the next before the lane ends — four car lengths.
    /// </summary>
    public float KerbParkingLaneRunM { get; init; } = 18f;
}

/// <summary>A building as a trip uses it.</summary>
internal sealed class BuildingFigures
{
    /// <summary>The one strip of ground somebody is meant to stand in.</summary>
    public float FrontGapM { get; init; } = 1f;

    /// <summary>
    /// <b>How long somebody stays inside a building</b> (PER-11), drawn between the two at every arrival
    /// (<see cref="DwellSkew"/>). The scale is how much of a town is indoors at once, and so how much of its traffic
    /// is parked (PER-29): at a dwell of seconds nearly everybody is out at once, and with most trips driven, so is
    /// nearly every car.
    /// </summary>
    public float DwellMinS { get; init; } = 10f;

    public float DwellMaxS { get; init; } = 180f;

    /// <summary>
    /// <b>How far a dwell leans to its short end</b>: the share of the way from the shortest to the longest is a draw
    /// raised to this power. Two: most visits are short and a few run long, a quarter of the way along at the median.
    /// </summary>
    public float DwellSkew { get; init; } = 2f;

    /// <summary>`PER-11`'s patience at a full door, in longest dwells: well above one, so an ordinary turnover is always waited out.</summary>
    public float PlacePatienceInDwells { get; init; } = 3f;
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
    /// <remarks>
    /// Long enough for a car that could no longer stop at its start to have its rear axle over the bar by its end
    /// while slowing for the turn beyond it. Shorter, such a car is counted over on a red (CAR-13.3); longer, every
    /// green loses what it gains, since the amber is held as a red is.
    /// </remarks>
    public float AmberTailS { get; init; } = 2f;

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

/// <summary>The railway: its track, the ground it is laid as, and the level crossing (RAIL-1…7).</summary>
internal sealed class RailFigures
{
    /// <summary>Between the rails' inner faces: the broad gauge of the place the traced city stands in.</summary>
    public float GaugeM { get; init; } = 1.52f;

    /// <summary>How wide one rail's head is: half of it and half the gauge is how far each rail's middle stands off the line.</summary>
    public float RailHeadWidthM { get; init; } = 0.075f;

    /// <summary>
    /// How wide a band of the track's picture about each rail is laid over the roads and walks crossing it (RAIL-3): the
    /// rail and the flangeway either side of it.
    /// </summary>
    public float RailBandM { get; init; } = 0.24f;

    /// <summary>The ballast's band across one track, shoulder to shoulder (RAIL-4).</summary>
    public float BallastWidthM { get; init; } = 4.4f;

    /// <summary>Between two tracks' lines where a line is laid double.</summary>
    public float TrackSpacingM { get; init; } = 4.1f;

    /// <summary>
    /// The tightest a generated track is bent (RAIL-2): as tight as two coupled coaches stand clear of each other round
    /// (<c>RollingStockTests</c>), so a loop of it (RAIL-11) is short enough for a train to pass over itself.
    /// </summary>
    public float LeastRadiusM { get; init; } = 55f;

    /// <summary>How high a rail bridge's rails stand over the rails it crosses (RAIL-11): a train's height and the deck's depth.</summary>
    public float BridgeClearanceM { get; init; } = 6.5f;

    /// <summary>
    /// The steepest a train is laid to climb (RAIL-11), rise over run: three times a main line's ruling grade, so a bridge
    /// reads as one at the town's scale rather than as a viaduct round its loop.
    /// </summary>
    public float GradeMostShare { get; init; } = 0.06f;

    /// <summary>How far a rail bridge's deck stands out past its ballast either side (RAIL-8): its walkway and its parapet.</summary>
    public float BridgeDeckOverhangM { get; init; } = 1.1f;

    /// <summary>How far off its curve any chord of a generated track may stand: what says how finely it is laid.</summary>
    public float ChordToleranceM { get; init; } = 0.01f;

    /// <summary>
    /// How far past the map's edge a generated track runs on (TRN-7): the run-in a train is put down on, a warning's run
    /// short of the map with its whole length behind that (RAIL-1, <c>RollingStockTests</c>).
    /// </summary>
    public float RunOffM { get; init; } = 950f;

    /// <summary>How far a junction's own disc stands clear of the ballast's edge at the least (RAIL-5).</summary>
    public float JunctionClearanceM { get; init; } = 12f;

    /// <summary>How far off square to a track a road may cross it (RAIL-5).</summary>
    public float CrossingSkewMostDeg { get; init; } = 35f;

    /// <summary>
    /// <b>How long before a train's front reaches a crossing it closes</b> (RAIL-1): long enough for a car at the
    /// town's pace to stop short of it, and short enough that a queue a train is coming for is seen to form.
    /// </summary>
    public float WarningS { get; init; } = 14f;

    /// <summary>How far short of a switch on its leg a train's driver sets it (TRN-9).</summary>
    public float SwitchSetAheadM { get; init; } = 100f;

    /// <summary>How long a barrier takes to come down across its lanes, and to go back up (RAIL-7).</summary>
    public float BarrierS { get; init; } = 5f;

    /// <summary>
    /// How long a crossing's lamps flash before its barriers start down (RAIL-7): a car too near to stop when it closed is
    /// over the barrier's line by then, rather than met by its arm (RAIL-6).
    /// </summary>
    public float BarrierDelayS { get; init; } = 4f;

    /// <summary>How long after its last wagon has cleared a crossing it opens.</summary>
    public float ClearS { get; init; } = 1.5f;

    /// <summary>
    /// <b>How near, along its road, any of a barrier's line comes to a rail</b> (RAIL-7), the walk's included — and how near
    /// along a walk its walkers wait: this much square across, more at the road's middle where it crosses at a slant.
    /// </summary>
    public float BarrierRailClearanceM { get; init; } = 2f;

    /// <summary>How far short of its barrier's line, along a lane, a level crossing's stop bar stands.</summary>
    public float StopLineSetbackM { get; init; } = 3f;

    /// <summary>The X light's flash: lit for half of it, dark for the other half (RAIL-7).</summary>
    public float FlashS { get; init; } = 1f;

    /// <summary>
    /// How wide a barrier's arm is — its body's, which its picture is held to (<c>RailLooks</c>) — and how far past the
    /// kerb onto the walk the middle of its drive's housing stands.
    /// </summary>
    public float BarrierWidthM { get; init; } = 0.3f;

    public float GearPastKerbM { get; init; } = 0.8f;

    /// <summary>How far up from lying across the road a raised arm stands.</summary>
    public float BarrierRaisedDeg { get; init; } = 90f;
}

/// <summary>What each surface does to a tyre standing on it, and how readily it takes a mark.</summary>
internal sealed class TerrainFigures
{
    public float GrassCoefficient { get; init; } = 0.8f;
    public float PavedCoefficient { get; init; } = 1.0f;

    /// <summary>
    /// Resistance to travel over a surface, <b>as a coefficient</b> — the raw term, dimensionless, against
    /// which the deceleration a wheel actually feels is derived (<see cref="SimConfig.GrassDragMps2"/> and
    /// its pair). It is spent outside the traction budget so it costs nothing a tyre would have used for
    /// cornering: terrain slows by friction, never by a speed multiplier. Tarmac's is a road tyre's own
    /// rolling resistance; what keeps a car let off from coasting the length of the town is its engine
    /// (<see cref="CarFigures.EngineBrakingResistance"/>), because a drag on the ground is paid under power
    /// too. Grass is deep turf, enough that a lawn is somewhere a car struggles and not so much that it
    /// strands one there.
    /// </summary>
    public float GrassResistance { get; init; } = 0.2915f;

    public float PavedResistance { get; init; } = 0.012f;

    /// <summary>
    /// How easily tarmac takes a permanent mark, as a factor on <see cref="MarkFigures.PowerM2S3"/>. Softer
    /// than the bar's own figure, so a slide that only just clears it still shows as a scuff. Grass has no
    /// factor: it records the wheel <em>ploughing</em> it rather than a slide at all, and takes the bar.
    /// </summary>
    public float PavedMarkFactor { get; init; } = 0.8f;

    /// <summary>
    /// <b>How many cells of the lattice the town's boundary is answered off stand across one cell of the
    /// grid</b> (<see cref="Geometry.RingSides"/>, SIM-8). A power of two.
    /// </summary>
    /// <remarks>
    /// A cell the boundary does not pass through is answered by a lookup, so the narrower the cells the more
    /// of a lane that is — at five bytes a cell for each layer answered. <b>Finer than the grid's own cell
    /// because the generator asks it at every point of every prop it stands</b> (<c>GroundShapes.IsAll</c>): at
    /// the grid's cell a two-lane street is kerb to kerb in one cell, and every answer on it walks the kerbs. No
    /// tick asks it — a wheel reads the ribbons (TER-7b).
    /// </remarks>
    public int ShellCellsAcrossGridCell { get; init; } = 2;

    /// <summary>
    /// How finely the ground is walked by a caller that has to <em>sample</em> it — a walk checking that a
    /// stretch stays on ground a person may stand on, a camera looking for the nearest road. It is a step
    /// and never a resolution: what the ground is at a point is exact wherever it is asked.
    /// </summary>
    public float GroundStepM { get; init; } = 1f;
}

/// <summary>
/// The air over the town (<c>Wind</c>): a light breeze that carries what is light enough to be carried — a car's smoke
/// (CAR-54) — and nothing that stands on the ground. It blows from a way the town's seed picks and swings slowly either
/// side of it, and gusts — which way and how hard each a term of its own, neither moved by the other's figures.
/// </summary>
internal sealed class WindFigures
{
    /// <summary>How fast the air moves between gusts.</summary>
    public float SpeedMps { get; init; } = 1.5f;

    /// <summary>How much harder a gust blows, and softer the lull after it, as a share of <see cref="SpeedMps"/>; and how long one gust is from the next.</summary>
    public float GustShare { get; init; } = 0.4f;

    public float GustPeriodS { get; init; } = 9f;

    /// <summary>How far the wind swings either side of the way the town's seed has it blow, and how long one swing there and back takes.</summary>
    public float TurnDeg { get; init; } = 60f;

    public float TurnPeriodS { get; init; } = 300f;
}

/// <summary>
/// <b>The clouds over the town</b> (OBJ-8, <c>Clouds</c>): never drawn themselves, only the shadows they cast, carried
/// across the town by the air at their height and changing shape as they go.
/// </summary>
internal sealed class CloudFigures
{
    /// <summary>How much of the sky is cloud, as a share: the share of the ground in shade at any one moment. Nought is a clear sky, and draws nothing.</summary>
    public float CoverShare { get; init; } = 0.25f;

    /// <summary>How far across a cloud is, about: the widest lumps of its shadow, its finest a thirty-second of it.</summary>
    public float AcrossM { get; init; } = 500f;

    /// <summary>
    /// How fast the air at the clouds' height carries them, the way the town's wind blows about (<c>Wind.PrevailingRad</c>):
    /// steadier and faster than the air at the ground, so they follow none of its swings and gusts.
    /// </summary>
    public float SpeedMps { get; init; } = 6f;

    /// <summary>How long a cloud keeps its shape before it has become another.</summary>
    public float LifeS { get; init; } = 900f;

    /// <summary>How high the clouds' base is, and how wide the sun is across the sky: what blurs a shadow's edge.</summary>
    public float HeightM { get; init; } = 1200f;

    public float SunAcrossDeg { get; init; } = 0.53f;

    /// <summary>How much of the light on the ground a cloud's shadow takes away where the cloud is thickest, as a share.</summary>
    public float ShadeShare { get; init; } = 0.17f;

    /// <summary>How far in from its edge a cloud thickens to its thickest: its shadow deepens over as far, so a thin fringe barely shades.</summary>
    public float ThickensOverM { get; init; } = 30f;
}

/// <summary>
/// <b>A roof's chimneys' smoke</b> (OBJ-7): a slow plume off the pots of whichever of the town's chimneys are burning,
/// which the wind (<see cref="WindFigures"/>) lays out downwind.
/// </summary>
internal sealed class ChimneyFigures
{
    /// <summary>
    /// How many of the town's chimneys are burning at any one time, as a share, and how long a fire holds before it is lit
    /// or let out afresh: every stack takes its turns from its own start, so the town's fires change a few at a time.
    /// </summary>
    public float SmokingShare { get; init; } = 0.4f;

    public float FirePeriodS { get; init; } = 120f;

    /// <summary>How long after one puff leaves a pot the next does, and how long a house's is drawn for.</summary>
    public float PuffEveryS { get; init; } = 0.2f;

    public float PuffLifeS { get; init; } = 6f;

    /// <summary>
    /// <b>How wide a house's pot is</b>, as its picture draws it — one pot, of the two most of the art's stacks carry: the
    /// width a roof's file that says none of its own (<c>potWidthM</c>) is taken to have. A puff leaves a pot as wide as
    /// it, and a wider pot's plume is thicker and lives longer than a house's (<c>ChimneySprites</c>).
    /// </summary>
    public float PotWidthM { get; init; } = 0.35f;

    /// <summary>How fast a puff grows across as it mixes into the air.</summary>
    public float SpreadMps { get; init; } = 0.6f;

    /// <summary>
    /// How fast the eddies over the roof blow a puff off the wind's line at the most, any way, and the time it gives that
    /// up to the wind over: what makes the plume billow and not a string of beads.
    /// </summary>
    public float ScatterMps { get; init; } = 0.5f;

    public float DragS { get; init; } = 1.5f;

    /// <summary>
    /// How opaque a puff is as it leaves a house's pot, thinning to nothing over its life; and the time it rises out of the
    /// pot before the wind takes it — straight up, which from above is standing on the pot — thickening to that as it
    /// rises, so the plume's root is the pot whatever the wind and not a disc stood on it.
    /// </summary>
    public float Opacity { get; init; } = 0.45f;

    public float RiseS { get; init; } = 0.4f;

    /// <summary>The grey the smoke is drawn, as a share of white.</summary>
    public float Shade { get; init; } = 0.85f;

    /// <summary>
    /// How far a chimney's smoke thins between its thickest and its thinnest, as a share, and how long one swing there
    /// and back takes: a fire is stoked and burns down, each chimney from its own start.
    /// </summary>
    public float BreathShare { get; init; } = 0.5f;

    public float BreathPeriodS { get; init; } = 40f;

    /// <summary>
    /// The most puffs a frame draws off the chimneys: what the emit's room holds for them past every car's, and what a
    /// frame's further chimneys are cut at. A street's framing draws a few hundred.
    /// </summary>
    public int MostPuffsDrawn { get; init; } = 16384;
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

/// <summary>
/// <b>How one kind of district lays its blocks</b> (GEN-72, GEN-74): its streets' spacing along its bearing, drawn between the
/// two, and how much longer a block runs across the bearing than along it. <b>A block is a rectangle</b> — the traced
/// cities' are, and a town of square blocks is a town with half as much road again as it needs.
/// </summary>
internal sealed class BlockFigures
{
    public float AlongMinM { get; init; }

    public float AlongMaxM { get; init; }

    public float AspectMin { get; init; }

    public float AspectMax { get; init; }
}

/// <summary>How coarsely a town is spaced.</summary>
/// <remarks>
/// <b>The block is this project's unit of town distance</b>: how far somebody will walk rather than drive,
/// what a blocked way is priced at, and how near its own building a service vehicle counts as home are all
/// quoted in it, so moving this figure moves all of them together.
/// </remarks>
internal sealed class CityGenFigures
{
    /// <summary>
    /// <b>A town's block, as distance is reckoned in</b>: the unit a walk, a trip and a service's home are quoted in. What a
    /// generated district is laid at is its kind's own (<see cref="OldTownBlocks"/> and the rest).
    /// </summary>
    public float BlockSpacingAlongMinM { get; init; } = 95f;

    /// <summary>
    /// <b>How an old town's blocks are laid</b> (GEN-74): small and near square, every side a street's frontage — 60 to 100 m
    /// in the old quarters of real towns.
    /// </summary>
    public BlockFigures OldTownBlocks { get; init; } = new() { AlongMinM = 70f, AlongMaxM = 95f, AspectMin = 1.1f, AspectMax = 1.6f };

    /// <summary>How the housing round it is laid: a little larger, and longer one way.</summary>
    public BlockFigures ResidentialBlocks { get; init; } = new() { AlongMinM = 85f, AlongMaxM = 120f, AspectMin = 1.2f, AspectMax = 2f };

    /// <summary>How an estate of towers is laid: superblocks, the towers standing in open ground inside them.</summary>
    public BlockFigures HighRiseBlocks { get; init; } = new() { AlongMinM = 140f, AlongMaxM = 190f, AspectMin = 1f, AspectMax = 1.5f };

    /// <summary>
    /// How a suburb is laid: <b>long and thin</b>, the short side two plots deep back to back and the long side the street
    /// the houses front.
    /// </summary>
    public BlockFigures SuburbBlocks { get; init; } = new() { AlongMinM = 65f, AlongMaxM = 90f, AspectMin = 2.2f, AspectMax = 3.4f };

    /// <summary>How works are laid: big plots and few streets.</summary>
    public BlockFigures IndustrialBlocks { get; init; } = new() { AlongMinM = 120f, AlongMaxM = 180f, AspectMin = 1f, AspectMax = 1.6f };

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
    /// The longest road a traced map lays between two places (GEN-57): a way surveyed further than this
    /// without meeting another is cut into roads no longer, at places of two arms.
    /// </summary>
    /// <remarks>
    /// <b>A lane is a whole road</b>, and what files one is bounded: the ribbon atlas keeps a way's metres in
    /// tenths of a ushort, 6 553.5 m, and a car's look-ahead is budgeted off the most arcs any one lane has.
    /// A generated town never lays a road a tenth of that; a survey's ring road through open country does.
    /// </remarks>
    public float TracedRoadLongestM { get; init; } = 1000f;

    /// <summary>
    /// <b>The longest road a generated town joins a run into</b> (GEN-51, <c>ThroughRoads</c>): a run longer is joined as
    /// pieces no longer, at the places of two arms it passes. The bound is the ribbon atlas's, which files a way's metres in
    /// tenths of a ushort, 6 553.5 m, and a road in run on round a bypass comes to more.
    /// </summary>
    public float JoinedRoadLongestM { get; init; } = 3000f;

    /// <summary>
    /// The shortest road a traced map lays between two junctions (GEN-57): a junction's disc stops this short
    /// of the middle of the way to its nearest neighbour, so two places the survey drew close together keep the
    /// road between them.
    /// </summary>
    public float TracedShortestRoadM { get; init; } = 0.5f;

    /// <summary>
    /// <b>How much further off its junction's centre an arm's lanes may end than the shortest road there lets that
    /// road's own end</b> (GEN-57): a road too short for both its junctions' discs squeezes its own two ends, and every
    /// other arm there ends where its junction asks, but no more than this further back.
    /// </summary>
    /// <remarks>
    /// Squeezed to the short road's end, every arm's lanes run into the middle of the crossing, past where the lanes they
    /// turn onto cross their line, and a movement between them has to wind round to come back to that corner. Stood
    /// back without bound, the short road's lanes would reach far into a box whose other arms end well short of them.
    /// </remarks>
    public float TracedArmEndsApartM { get; init; } = 5f;

    /// <summary>
    /// The tightest the middle of a traced road's roadside is laid round a corner (GEN-57), where the survey drew one
    /// sharper than its carriageway can round: a strip nobody drives, so a hairpin drawn to a point turns it this tight
    /// rather than folding it into a cusp. A lane is never laid tighter than a car steers
    /// (<see cref="SimConfig.CarTurningRadiusM"/>).
    /// </summary>
    public float TracedTightestLaneRadiusM { get; init; } = 0.5f;

    /// <summary>
    /// <b>How far a traced road's corners may stand off a point its way was surveyed through</b> for the line to be
    /// laid in the fewest corners and the roundest arcs (GEN-57, <c>TracedAlignment</c>): a point this near the
    /// straight past it is no corner, two corners one arc passes this near are one, and a corner is rounded as wide as
    /// keeps its stretch of the survey this near. A corner sharper than that can round at half its carriageway sags
    /// further.
    /// </summary>
    /// <remarks>
    /// <b>The line its corners make is the one its junctions' movements are held to</b> (<c>TracedStreets</c>), so it
    /// stands nearer the survey than the line laid in the end may: a bend a mapper drew as a polygon is laid as the
    /// bend it was.
    /// </remarks>
    public float TracedCornerToleranceM { get; init; } = 0.5f;

    /// <summary>
    /// <b>How far a traced road's line may stand off a point its way was surveyed through</b> for runs of its pieces to
    /// be merged into fewer (GEN-57, <c>TracedPieces</c>), and <see cref="TracedLineToleranceShare"/> of its
    /// carriageway's width further: a run one arc, two, or two and a straight keep that near is laid as those.
    /// </summary>
    /// <remarks>
    /// Inside a lane, and inside what OSM's own nodes are placed to off imagery — and twice
    /// <see cref="TracedCornerToleranceM"/>, which leaves a merge as much room as the corners took.
    /// </remarks>
    public float TracedLineToleranceM { get; init; } = 1f;

    /// <summary>
    /// <b>How much further a traced road's line may stand off its survey for each metre of its carriageway's
    /// width</b>, past <see cref="TracedLineToleranceM"/> (GEN-57): a road standing a share of its own width off the
    /// way surveyed down its middle covers as much of it whatever its width, so a wide one is merged the more loosely.
    /// </summary>
    /// <remarks>
    /// An eighth: a street of one lane, 3.5 m, stands within 1.4 m of its survey, and one of a lane each way between two
    /// roadsides, 11.4 m, within 2.4 m.
    /// </remarks>
    public float TracedLineToleranceShare { get; init; } = 0.125f;

    /// <summary>
    /// <b>How wide every lane of a traced road is laid</b> (GEN-57), whatever OSM tags or assumes: a street's lanes
    /// are painted alike, and what a mapper's widths and a survey's measurements differ by lane to lane is how far the
    /// sources disagree.
    /// </summary>
    /// <remarks>
    /// OSM's own assumed lane (<c>OsmCarriageway.AssumedLaneWidthM</c>, JOSM's lane renderer), which Odesa's measured
    /// street widths do not tell from anything between 3.2 and 3.5 m; <see cref="TracedRoadsideWidthM"/> is the
    /// roadside that fits them best beside it (the decision log has the reading).
    /// </remarks>
    public float TracedLaneWidthM { get; init; } = 3.5f;

    /// <summary>
    /// <b>How wide a traced road's roadside is laid</b> (GEN-57): a strip of carriageway between the lanes and a kerb
    /// that nobody drives — the cars parked along a street and its gutter, which imagery reads as road. A measured
    /// width holds as many of these past its lanes as the rest of it holds to the nearest strip, one a kerb at most.
    /// </summary>
    /// <remarks>
    /// Beside lanes of <see cref="TracedLaneWidthM"/>, Odesa's measured street widths fit a strip of 2.2 m and one of
    /// 2.8 m equally — the one a street of a lane each way read 10.4 m wide with a strip at each kerb, the other with one
    /// strip and its lanes set off its way. This is the first: a parked car's width, and a street laid as it was drawn.
    /// </remarks>
    public float TracedRoadsideWidthM { get; init; } = 2.2f;

    /// <summary>
    /// <b>The longest stretch of a traced street laid in the lanes either side of it</b> (GEN-57): where its lanes change
    /// and change back, alike either side, within this along the street, the stretch is laid in theirs.
    /// </summary>
    /// <remarks>
    /// A block: a width read off imagery holds a lane each way and a roadside at each kerb as well as it holds two lanes
    /// each way, and nothing but a yard's way meets a street that narrows for less than a block and widens again.
    /// </remarks>
    public float TracedLanesHeldM { get; init; } = 150f;

    /// <summary>
    /// <b>How much of the shorter of two roads must run alongside the other, their carriageways over each other, for the
    /// two to be parted</b> (GEN-57, <c>TracedStreets.Parted</c>): two carriageways drawn a little too close along most of
    /// their length, and not a slip road running in beside a street for the last of its own.
    /// </summary>
    public float TracedPartedAlongShare { get; init; } = 0.8f;

    /// <summary>
    /// <b>How far short of a junction a traced street that loses lanes into it keeps them</b> (GEN-57): a street losing
    /// lanes no further than this from it runs into it in the lanes it had.
    /// </summary>
    /// <remarks>
    /// A junction's approach: a lane the street has no room for at the junction ends unjoined there, rather than a few
    /// car lengths short of it.
    /// </remarks>
    public float TracedLanesHeldShortOfJunctionM { get; init; } = 30f;

    /// <summary>
    /// <b>The shortest roadside a traced street is laid with</b> (GEN-57): one shorter along its street is not laid,
    /// and one as long runs on along the whole of it wherever its kerb and walk stay off another road's carriageway.
    /// </summary>
    /// <remarks>
    /// A few parked cars: a roadside is read off a width measured way by way, and a street holding one along one way
    /// and none along the next was measured so, not built so.
    /// </remarks>
    public float TracedRoadsideShortestM { get; init; } = 30f;

    /// <summary>
    /// <b>How near two traced junctions a road joins stand for them to be laid as one</b> (GEN-57): two places of three
    /// arms or more this near each other, centre to centre, are one junction, and the road between them is its ground.
    /// </summary>
    /// <remarks>
    /// A dual carriageway's crossing, a side street meeting its far carriageway through the gap in the median, two side
    /// streets a few metres apart across a street: a car crosses each as one movement. Further apart they are two side
    /// streets a building apart and two forks, each a junction of its own with a few cars' queue and a zebra between,
    /// and as one they make a box twice as long crossed by connectors fanned the whole of it (the decision log has the
    /// reading at 30 m).
    /// </remarks>
    public float TracedJunctionsMergedM { get; init; } = 20f;

    /// <summary>
    /// The narrowest a traced road's measured width may leave each of its lanes (GEN-57): a width that would leave its
    /// lanes narrower is a misreading of that road — a stretch of imagery under one carriageway of a pair — and is not
    /// taken. About the least a lane of a residential street is laid at.
    /// </summary>
    public float TracedNarrowestLaneM { get; init; } = 2.75f;

    /// <summary>
    /// The widest a traced road's measured width may leave each of its lanes (GEN-57): about a lane with a car parked
    /// either side of it, which is what a street's kerb-to-kerb width holds over its lanes. A width that would leave a
    /// lane wider read something else as the road — a square, a yard, a car park the way runs through — and is not
    /// taken.
    /// </summary>
    public float TracedWidestLaneM { get; init; } = 8f;

    /// <summary>
    /// The cell a town's buildings and their padding are claimed at while it is laid (GEN-3, GEN-6b): a city across is
    /// tens of millions of these, a bit each. A prop is held up to a cell further off a building than a metre would
    /// hold it.
    /// </summary>
    public float ClaimCellM { get; init; } = 2f;

    /// <summary>
    /// <b>How many of a traced town's bridges over its roads have cars stood at them</b> (GEN-57, PHY-1a): a car each way
    /// on the bridge over the road it crosses, and one each way on that road short of it — the widest crossings first.
    /// It is content, as a map's own cars are: a handful to look at a level by, and no traffic.
    /// </summary>
    public int TracedBridgesWithCars { get; init; } = 4;

    /// <summary>
    /// <b>How far behind the building line a town's zones are asked what is built there</b> (GEN-58): a front room's
    /// depth, so a zone's edge drawn along the kerb or a few metres short of it still speaks for the frontage.
    /// </summary>
    public float ZoneDepthM { get; init; } = 5f;

    /// <summary>
    /// <b>The least class of road a traced junction's arms are main roads at</b> (GEN-57), on
    /// <c>TracedRoad.Rank</c>'s scale: a junction of three main arms or more carries lights. Tertiary — a crossing of
    /// two through streets carries lights, and a residential street's off one does not.
    /// </summary>
    public int TracedLitRankLeast { get; init; } = 3;

    /// <summary>
    /// <b>How far the walk may stand from where a building's front is asked to be seated</b> (GEN-58, GEN-54): further,
    /// and the place is no walk's.
    /// </summary>
    public float ZoneFrontageReachM { get; init; } = 25f;

    /// <summary>
    /// <b>How far any part of a building's front may stand back off the building line</b> (GEN-58), where the rest of it
    /// stands on it: the walk under a front may curve that much and no more, so nothing is stood across a bend or a jog
    /// in the kerb.
    /// </summary>
    public float ZoneFrontageStraightM { get; init; } = 1f;

    /// <summary>
    /// How far the walk under a building's front may turn off the front's own bearing (GEN-58): further, and the front
    /// runs round a block's corner.
    /// </summary>
    public float ZoneFrontageTurnDeg { get; init; } = 30f;

    /// <summary>
    /// <b>How many of a look's prefabs a place on a town's walk is tried with</b> (GEN-58), from the one drawn on, before
    /// it stands nothing: the first that seats on the walk and stands clear of the ground and of the buildings stood
    /// before it is the one.
    /// </summary>
    public int ZonePrefabsTried { get; init; } = 6;

    /// <summary>
    /// <b>How sharply a town that plans fewer buildings than its zones stand thins away from its hub</b> (GEN-54): all of
    /// the frontage is kept out to a radius of the hub, and beyond it the share kept falls as the distance to this power —
    /// so the middle of a town is built solid and its edge thins out, rather than every street being thinned alike.
    /// </summary>
    public float ZoneThinFalloff { get; init; } = 1f;

    /// <summary>
    /// How far two buildings a zone stands may stand into each other and still both stand (GEN-58): a party wall's half,
    /// which two neighbours of a terrace share.
    /// </summary>
    public float ZonePartyWallM { get; init; } = 0.3f;

    /// <summary>
    /// <b>How widely a zone's buildings' footprints spread about its typical one</b> (GEN-58), as a factor: a prefab
    /// that many times bigger or smaller than the zone's footprint is drawn about three-fifths as often as one its size.
    /// </summary>
    public float ZoneFootprintSpread { get; init; } = 2f;

    /// <summary>
    /// How far behind the walk's outer face a building behind the frontage keeps (GEN-58): past a front row's depth, so
    /// the ground behind the frontage is built over where the frontage builder has not reached.
    /// </summary>
    public float ZoneBehindClearM { get; init; } = 15f;

    /// <summary>
    /// How far a building behind the frontage may stand from the walk its way in opens onto (GEN-58): further, and it is
    /// not built — a door nobody walks to is no building of the town's.
    /// </summary>
    public float ZoneBehindReachM { get; init; } = 150f;

    /// <summary>
    /// The least share of its ground a zone builds behind its frontage before it is built at all (GEN-58): less is a shed
    /// or two in a city's district, not worth the places asked.
    /// </summary>
    public float ZoneBehindLeast { get; init; } = 0.005f;

    /// <summary>
    /// How many places a zone's ground behind its frontage is offered for each building its share wants (GEN-58): enough
    /// that the walk's band, the paving and the frontage refusing most of them still leave the share built.
    /// </summary>
    public float ZoneBehindOffered { get; init; } = 25f;

    /// <summary>
    /// How far one one-way street stands off the next (GEN-18), which is what scatters them evenly over a
    /// town rather than gathering them into a district.
    /// </summary>
    /// <remarks>
    /// <b>Wider than the coarsest block a district is laid at</b> (<see cref="IndustrialBlocks"/>), so
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
    /// turns.
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

    /// <summary>The same for an arterial, which is very nearly a chord.</summary>
    public float ArterialWanderInBlocks { get; init; } = 0.012f;

    /// <summary>
    /// <b>How many of a grid district's streets are laid straight</b> (GEN-47), as the chance each one is:
    /// most, and short of all, so a grid carries a street or two that wanders. Each district draws its own
    /// within <see cref="StraightShareSpread"/> of it.
    /// </summary>
    public float GridStraightShare { get; init; } = 0.9f;

    /// <summary>The same for a loose district, whose streets mostly wander and some of which run straight.</summary>
    public float LooseStraightShare { get; init; } = 0.3f;

    /// <summary>
    /// How far either side of its kind's share a district's own is drawn, so no two districts of a kind lay
    /// the same mix. Inside the gap either share leaves to none and to all.
    /// </summary>
    public float StraightShareSpread { get; init; } = 0.08f;

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
    /// one here is six bays, 24 m of kerb, which is about the frontage of one building. A service's yard is the one
    /// exception, as wide as its building's frontage when that is wider (GEN-55, <c>CarParks.YardBays</c>).
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
    /// <b>How far a car park's spaces stand back from the carriageway's edge</b> (GEN-53): an apron of the
    /// bay's own tarmac between the street and the space, which a car turning in or backing out swings across
    /// rather than across the street.
    /// </summary>
    /// <remarks>
    /// <b>It is also what keeps a rank's end a corner the walk can be struck round</b> (TER-3c.8): a rank's end
    /// runs this and a bay back from the kerb, and one only a bay deep is a side the walk's outset
    /// (<see cref="SimConfig.WalkOuterM"/> and <see cref="RoadFigures.LineRoundedM"/>) all but folds shut on — a
    /// crossing three centimetres off the corner, which on three rank ends of Odesa it did not find.
    /// </remarks>
    public float BaySetbackM { get; init; } = 1.5f;

    /// <summary>
    /// <b>How long a bay is</b> (GEN-53, <see cref="SimConfig.CarParkBayLengthM"/>): a length of ground,
    /// authored like every other length of ground in this file, and <b>longer than the longest vehicle the
    /// town draws</b> (<see cref="CarFigures.LongestLengthM"/>, which <c>SimConfigTests</c> holds it to).
    /// </summary>
    /// <remarks>
    /// <b>Six metres, the longest van's clearance and nothing else</b> — a bay square to the street is driven
    /// straight into off its own turn and straight out of again, where what sizes the parallel bay on a kerb
    /// is the reverse into it (<see cref="RoadFigures.ParkingSpaceMarginInCarWidths"/>). A bay any longer is tarmac laid
    /// for nothing and an arm that much further out into the ground behind the street.
    /// </remarks>
    public float BayLengthM { get; init; } = 6f;

    /// <summary>
    /// <b>The walk between two rows of a car park's bays standing back to back</b> (GEN-53a): a strip of ground
    /// that is not the tarmac, so the walk wraps it (TER-3c.3) and the zebras across the aisles at either end of it
    /// land on it — the way a driver walks up the car park to the door.
    /// </summary>
    /// <remarks>
    /// <b>Three metres holds the walk's own lane</b> (<see cref="SimConfig.WalkingLaneWidthM"/>) with its kerb either side;
    /// any narrower and the walk wrapping two rows meets itself down the middle.
    /// </remarks>
    public float CarParkIslandM { get; init; } = 3f;

    /// <summary>
    /// <b>How far a car park's aisles reach to join a street</b> (GEN-53a): an entrance drawn from the outermost aisle
    /// straight out to the first street it meets, and no car park laid where none is this near.
    /// </summary>
    /// <remarks>
    /// A block's length: a car park in a courtyard is reached down a drive OSM drew as the place's own ground, and
    /// dropped (<c>qq osm --drop-zone-ways</c>); 60 m left 64 of OdesaOsm's 262 car parks with no street, 100 m 45.
    /// </remarks>
    public float CarParkEntranceReachM { get; init; } = 100f;

    /// <summary>
    /// <b>How many bays one entrance of a car park serves</b> (GEN-53a): a car park is joined to the streets by one
    /// entrance for every this many bays its aisles rank, as far as its streets offer places to join.
    /// </summary>
    /// <remarks>
    /// A mall's thousand bays are let in and out at three or four places; through one, the queue to leave is the length
    /// of the car park.
    /// </remarks>
    public int CarParkBaysPerEntrance { get; init; } = 250;

    /// <summary>
    /// <b>How far off the lane it is following a car on its way to a bay may be carried</b> (GEN-53). A
    /// movement holds the street straight until it turns in (<see cref="Geometry.Spline.StraightArcStraightInto"/>)
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
    /// <b>How far off its river's own normal a bridge may run</b> (GEN-70). A bridge site is laid on the normal, so this is
    /// the bound the suite holds every bridge to rather than a figure the generator aims at.
    /// </summary>
    public float BridgeSkewMostDeg { get; init; } = 15f;

    /// <summary>How near each other two bridges over one river may stand at the least (GEN-70), along the river.</summary>
    public float BridgesApartLeastM { get; init; } = 300f;

    /// <summary>
    /// How far apart, along a river parting built-up districts, the bridges over it stand at the most (GEN-70): a town split
    /// by water is crossed often enough to be one town.
    /// </summary>
    public float BridgesApartMostM { get; init; } = 1500f;

    /// <summary>
    /// <b>How far every road keeps off the shore's landward edge</b> (GEN-71), carriageway and walk: a strip of open
    /// ground along every bank that only a bridge and its approach cross.
    /// </summary>
    public float WaterfrontClearM { get; init; } = 6f;

    /// <summary>
    /// <b>The most ground a district takes</b> (GEN-65). A town grows by its districts and never by growing them, so a
    /// region of land is cut into as many as keep each under this. Authored: how big a neighbourhood is, is a fact about
    /// towns.
    /// </summary>
    public float DistrictLargestM2 { get; init; } = 1_000_000f;

    /// <summary>
    /// The least ground a district takes (GEN-65): a piece of land the barriers leave smaller than this is open land and
    /// stands no street of its own.
    /// </summary>
    public float DistrictLeastM2 { get; init; } = 150_000f;

    /// <summary>
    /// <b>How far out a town is built solid</b> (GEN-76), as a share of the way from its core to its furthest land: every
    /// district within it is built as full as its kind builds, and its bypass runs round it (GEN-63).
    /// </summary>
    public float TownSolidShare { get; init; } = 0.3f;

    /// <summary>
    /// <b>How much of what its kind builds a district at the town's furthest land builds</b> (GEN-76), as a share: from the
    /// solid middle out, a district builds less the further out it stands, down to this at the edge.
    /// </summary>
    public float TownEdgeFill { get; init; } = 0.3f;

    /// <summary>
    /// <b>How far below what its place says a district's fill is drawn</b> (GEN-76), as a share of it: no two neighbourhoods
    /// are built alike, and none is quite full.
    /// </summary>
    public float TownFillSpread { get; init; } = 0.35f;

    /// <summary>
    /// <b>How much of a district's thinning is its streets standing further apart</b> (GEN-76), as the power of its fill its
    /// blocks are widened by — the rest is gaps along its streets: the edge of a town is sparser in its lanes as well as
    /// its houses, and nought widens no block.
    /// </summary>
    public float TownSparseBlockShare { get; init; } = 0.5f;

    /// <summary>
    /// <b>How much nearer the core a district beside a road into the town counts</b> (GEN-76), as a share of its distance:
    /// a town is built thicker along its roads than in the ground between them.
    /// </summary>
    public float TownRoadwardShare { get; init; } = 0.35f;

    /// <summary>How far off a road into the town a district's middle still counts as beside it, falling to nothing (GEN-76).</summary>
    public float TownRoadwardReachM { get; init; } = 700f;

    /// <summary>
    /// <b>How far a district's distance from the core is moved by a draw</b>, either way, as a share of it (GEN-76): no town
    /// thins out evenly.
    /// </summary>
    public float TownEdgeJitterShare { get; init; } = 0.2f;

    /// <summary>
    /// <b>How many of the districts on the map's rim are left to the country</b> (GEN-76), as a share of them at the
    /// furthest land, falling to none at the solid middle's edge: farmland and the wild between the villages.
    /// </summary>
    public float CountryRimShare { get; init; } = 0.4f;

    /// <summary>
    /// <b>How much of the land a town leaves to the country is left wild</b> rather than farmed (GEN-76), as a share of its
    /// districts, drawn one by one.
    /// </summary>
    public float CountryWildShare { get; init; } = 0.3f;

    /// <summary>
    /// <b>How many of a town's built districts one hospital serves</b> (GEN-56): a catchment, so a city of a hundred
    /// districts has a handful and not a hundred.
    /// </summary>
    public int DistrictsPerHospital { get; init; } = 12;

    /// <summary>How many one police station serves (GEN-56); each patrol's beat is still its own district (SRV-5).</summary>
    public int DistrictsPerPoliceStation { get; init; } = 4;

    /// <summary>How many one depot serves (GEN-56).</summary>
    public int DistrictsPerDepot { get; init; } = 6;

    /// <summary>
    /// <b>How many of a generated town's blocks are parks</b> (GEN-77), as a share of its built districts' blocks, drawn
    /// one by one — and the block nearest the core, the town's own garden, always.
    /// </summary>
    public float BlockParkShare { get; init; } = 0.07f;

    /// <summary>
    /// <b>How thickly a generated park is planted</b> (GEN-77), as a share of the most the ground holds: a park is laid as
    /// a garden, thicker than the grass round a town's buildings.
    /// </summary>
    public float BlockParkGrowth { get; init; } = 1f;

    /// <summary><b>How many are a school, a church or a public building and its grounds</b> (GEN-77), drawn as the parks are.</summary>
    public float BlockCivicShare { get; init; } = 0.04f;

    /// <summary>
    /// <b>How many of the old town's and the housing's blocks along a road between districts are shops and offices</b>
    /// (GEN-77) — the high street — drawn one by one.
    /// </summary>
    public float BlockCommercialShare { get; init; } = 0.6f;

    /// <summary>
    /// <b>How much of a block's face must run along a road between districts</b> for the block to front it (GEN-77), as a
    /// share of the face.
    /// </summary>
    public float BlockFrontsAShare { get; init; } = 0.2f;

    /// <summary>
    /// <b>How fine the land is cut into districts</b> (GEN-64): the side of the square the partition is laid on. A strip of
    /// land narrower than a few of these is not seen as land at all.
    /// </summary>
    public float DistrictCellM { get; init; } = 10f;

    /// <summary>
    /// How far a district's edge is bent off the straight line between two districts' middles, as a share of the distance
    /// between them (GEN-66): none lays a field of polygons, and enough lays a town whose districts read as shapes.
    /// </summary>
    public float DistrictBendShare { get; init; } = 0.12f;

    /// <summary>
    /// How much of a district's edge a railway or an intercity road must run along for the district to lean industrial
    /// (GEN-74).
    /// </summary>
    public float DistrictIndustrialEdgeShare { get; init; } = 0.4f;

    /// <summary>
    /// <b>The share of district roads laid four lanes</b> (GEN-67): the most loaded, by the demand routed over them. A
    /// ranking and not a load: what a demand is worth in cars is nothing the generator knows.
    /// </summary>
    public float FourLaneDistrictShare { get; init; } = 0.3f;

    /// <summary>How near each other two junctions on an intercity road may stand at the least (GEN-68).</summary>
    public float IntercityJunctionsApartLeastM { get; init; } = 300f;

    /// <summary>
    /// <b>How near each other two interchanges on a bypassed town's intercity road stand at the least</b> (GEN-68): each
    /// takes its slip roads' run either side of where it meets the road, and a road carried over it its span.
    /// </summary>
    public float InterchangesApartLeastM { get; init; } = 500f;

    /// <summary>
    /// <b>How far off an intercity road's line an interchange's terminal stands</b> (GEN-68): where its slip roads meet the
    /// town's road, and where a road carried over the intercity road comes down — clear of the intercity road's carriageway
    /// and of the terminal's own box.
    /// </summary>
    public float InterchangeOffsetM { get; init; } = 20f;

    /// <summary>
    /// <b>The speed a slip road is laid for</b> (GEN-47, GEN-68): what the turn off its shallow leg into its terminal is
    /// rounded at, a car having slowed off the intercity road for the town's.
    /// </summary>
    public float SlipDesignSpeedMps { get; init; } = 14f;

    /// <summary>
    /// <b>The angle a slip road leaves an intercity road at, and joins it at</b> (GEN-68), off the intercity road's line:
    /// shallow, so a car leaves and joins the kerb lane near the speed it is driven at. How far along the road it runs
    /// from its terminal is read off this and <see cref="InterchangeOffsetM"/>.
    /// </summary>
    public float SlipLeavesDeg { get; init; } = 25f;

    /// <summary>
    /// <b>How far either side of a road with no walk nothing is built or planted</b> (GEN-75), off its carriageway's
    /// edge: open ground an intercity road runs through, and the shoulder and its barrier stand in.
    /// </summary>
    public float UnwalkedVergeM { get; init; } = 15f;

    /// <summary>How near each other two junctions on a district road may stand at the least (GEN-68).</summary>
    public float DistrictJunctionsApartLeastM { get; init; } = 40f;

    /// <summary>
    /// <b>How much further off the arms already there a road between districts is planned than a junction's arms must
    /// stand</b> (GEN-13, GEN-69): what a node moved as the nodes settle, and a line rounded where it is laid, take off the
    /// angle it was planned at — so a road planned at the bound is not refused as it is laid.
    /// </summary>
    public float LinkArmsMarginDeg { get; init; } = 8f;

    /// <summary>
    /// <b>How far in from the map's edge the town's core stands at the least</b> (GEN-63), as a share of its short side:
    /// the place the intercity roads converge on is a town's middle and not its edge.
    /// </summary>
    public float CoreInsetShare { get; init; } = 0.25f;

    /// <summary>
    /// How far off the town's core a railway passes, across its own line (GEN-63) — and how much further out each line after
    /// the first passes.
    /// </summary>
    public float RailPassesCoreM { get; init; } = 450f;

    /// <summary>
    /// How near two of a town's railways come at the least (GEN-63): a line drawn nearer one already laid, or across it, is
    /// drawn again — a block's room between them, so the ground they part is a district's and not a sliver.
    /// </summary>
    public float RailLinesApartLeastM { get; init; } = 100f;

    /// <summary>
    /// <b>How fine an intercity road is routed</b> (GEN-63): the side of the square its search walks. It is smoothed after,
    /// so this is how near a bank or a track it can find its way past and not how it bends.
    /// </summary>
    public float RouteCellM { get; init; } = 20f;

    /// <summary>
    /// <b>How far round a place a curve could not be drawn a road passing the town is routed on its next try</b> (GEN-63),
    /// in the tightest radius it may bend at: the two crossings it could not turn between, and room either side to turn
    /// before them.
    /// </summary>
    public float RouteStrayBarredRadii { get; init; } = 2f;

    /// <summary>
    /// <b>How far a curve drawn along a route may stand off the cells its search walked</b> (GEN-63, GEN-71): the clearance
    /// the search keeps off the water and the railway is widened by as much, so a curve through its corners has room to
    /// keep what the search kept.
    /// </summary>
    public float RouteToleranceM { get; init; } = 10f;

    /// <summary>
    /// <b>How far an intercity road's straights stand off the curve it was drawn as, at the most</b> (GEN-63): its line is
    /// as few points as keep it this near, so a gentle curve is few points and the road laid round them few arcs.
    /// </summary>
    public float IntercityChordToleranceM { get; init; } = 0.5f;

    /// <summary>How far a road holds a bridge's own line before and after its span (GEN-70), so it meets the river square.</summary>
    public float BridgeApproachM { get; init; } = 30f;

    /// <summary>
    /// <b>How far a bridgehead's lanes stand off it</b> (GEN-14a): the span and the road carrying it on meet on one line,
    /// lane onto its own lane, so the handover between them is a joint in the carriageway and no junction's ground.
    /// </summary>
    public float BridgeheadStandoffM { get; init; } = 0.5f;

    /// <summary>
    /// How far along a barrier two districts it parts are linked across it at the most (GEN-69): one link to every this much
    /// of their shared edge, and at least one.
    /// </summary>
    public float DistrictLinksApartM { get; init; } = 1200f;

    /// <summary>
    /// How wide the parapet along each edge of a bridge's deck is (TER-3b.1): the margin the deck carries past the street's
    /// own pavement either side, which nothing walks on.
    /// </summary>
    public float BridgeParapetWidthM { get; init; } = 0.5f;

    /// <summary>
    /// How far the shore runs back from the water it belongs to. <b>The strip between the water and whatever
    /// the town does with the ground</b>: nothing is scattered on it and nothing is built on it, because it
    /// is not the grass those take.
    /// </summary>
    public float ShoreWidthM { get; init; } = 8f;

    /// <summary>
    /// How finely a shoreline is sampled: the most a chord may stand off the curve it is drawn through.
    /// The rings are the bank the ground is answered off as well as the one drawn (TER-7), so this is how
    /// far both stand off the wave the water is laid from.
    /// </summary>
    public float ShoreChordToleranceM { get; init; } = 0.5f;

    /// <summary>
    /// The radius the water's bank is rounded to where a river flows into another (GEN-2c): either side of the mouth the
    /// two banks meet at a point of land, and rounded they meet as a bank does. <b>Wider than the shore</b>, whose own
    /// edge there is rounded about the same centre a shore's width tighter, so the strip is one width round the bend.
    /// </summary>
    public float ConfluenceRadiusM { get; init; } = 24f;

    /// <summary>
    /// How wide a river is where it flows into another, as a share of the other's width there: the narrowest a brief's
    /// tributaries are drawn, then the widest. <b>Kept under the whole</b> so a mouth reads as one water joining a
    /// bigger one, and so a deck that spans the river spans what flows into it.
    /// </summary>
    public float TributaryWidthLeastShare { get; init; } = 0.3f;

    /// <inheritdoc cref="TributaryWidthLeastShare"/>
    public float TributaryWidthMostShare { get; init; } = 0.6f;

    /// <summary>
    /// How narrow a tributary has grown by where it enters the map, as a share of its width at its mouth: a river
    /// gathers its water on the way down.
    /// </summary>
    public float TributaryHeadNarrowShare { get; init; } = 0.35f;

    /// <summary>
    /// The angle a tributary runs off the water it flows into, measured from that water's way upstream: the least, then
    /// the most. <b>Never square and never alongside</b> — a river joins another pointing downstream, and one joining
    /// at a glance runs beside its parent for a kilometre with a strip of shore between them.
    /// </summary>
    public float TributaryJoinLeastDeg { get; init; } = 35f;

    /// <inheritdoc cref="TributaryJoinLeastDeg"/>
    public float TributaryJoinMostDeg { get; init; } = 75f;

    /// <summary>
    /// How far in from the map's edge along its parent a tributary's mouth stands at the least, as a share of the
    /// parent's run across the map: a mouth at the edge is a river the town never sees join.
    /// </summary>
    public float TributaryMouthInsetShare { get; init; } = 0.05f;

    /// <summary>
    /// How deep the tributaries branch: one flowing into the main water is the first, one flowing into that the second.
    /// </summary>
    public int TributaryDepthMost { get; init; } = 2;

    /// <summary>
    /// The lattice the props are scattered on — one candidate a cell, jittered inside it. <b>It is the
    /// town's prop density, and density goes as its square</b>: the scatter thins by a fifth for every
    /// twelve centimetres in a metre this grows by.
    /// </summary>
    public float PropSpacingM { get; init; } = 6.5f;

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
    /// How far apart along the walk's face the trees of a tree-lined street stand, centre to centre (GEN-6f). <b>A row is
    /// a pitch and not a scatter</b>, so it is not jittered: the eye reads the regularity before it reads the trees.
    /// </summary>
    public float PropStreetTreePitchM { get; init; } = 9f;

    /// <summary>How many of the walk's rings are lined with street trees, as a share (GEN-6f); the rest carry only the verge's scatter.</summary>
    public float PropTreeLinedShare { get; init; } = 0.6f;

    /// <summary>
    /// How many more of the wild set may grow round a clump's heart (GEN-6g), the count drawn evenly from none to this. A
    /// patch of weeds, a thicket round a tree, a grove's understorey.
    /// </summary>
    public int PropClumpMost { get; init; } = 9;

    /// <summary>
    /// How far past the clearance from its heart's rim a clump's member may stand, rim to rim (GEN-6g). <b>Short against
    /// the lattice</b> (<see cref="PropSpacingM"/>), so the ground between two clumps stays open and the clumps read as clumps.
    /// </summary>
    public float PropClumpReachM { get; init; } = 2f;

    /// <summary>
    /// How wide the open ground's thickets and clearings are, the width of the coarser of their field's two lattices
    /// (GEN-6g, <c>CityGen.Gen.Thickets</c>). <b>Several lattice cells across</b> (<see cref="PropSpacingM"/>), so a
    /// thicket is many clumps and a clearing is ground the eye sees as bare.
    /// </summary>
    public float PropThicketM { get; init; } = 40f;

    /// <summary>
    /// How hard the thickets' field is stretched about its middle before it is clamped (GEN-6g): one leaves the ground
    /// thinning and thickening by a quarter either way, which reads as even; more bares the clearings and fills the
    /// thickets.
    /// </summary>
    public float PropThicketContrast { get; init; } = 3f;

    /// <summary>How many of a verge's planted and furnished stations lay a run of one look rather than one prop, as a share (GEN-6h).</summary>
    public float PropRunShare { get; init; } = 0.3f;

    /// <summary>The most props a run lays, the first included, the count drawn evenly from two to this (GEN-6h).</summary>
    public int PropRunMost { get; init; } = 4;

    /// <summary>
    /// The grass between two props of a run, rim to rim along the face (GEN-6h). <b>Wider than <see cref="PropApartM"/></b>,
    /// the least two props may leave (GEN-6c): a run laid at exactly that is refused member by member wherever the face
    /// bends inward or the arithmetic rounds down.
    /// </summary>
    public float PropRunGapM { get; init; } = 0.7f;

    /// <summary>How many places a railway or an intercity road is drawn at before it is left out (GEN-8): drawn again, never searched for.</summary>
    public int ConnectorTries { get; init; } = 12;

    /// <summary>How many places a river or a tributary is drawn at before it is left out (GEN-8): drawn again, never searched for.</summary>
    public int WaterTries { get; init; } = 8;

    /// <summary>How far past the map's own edge a water's far side is run on, so no shore closes inside the town (GEN-62).</summary>
    public float WaterOverrunM { get; init; } = 200f;

    /// <summary>How much of a river's run its wander rises over from either end, so it leaves the edge and meets the sea where drawn (GEN-62).</summary>
    public float RiverRiseShare { get; init; } = 0.15f;

    /// <summary>How far a river crossing a map with no sea is turned off the brief's bearing, either way (GEN-62).</summary>
    public float RiverTurnRad { get; init; } = 0.35f;

    /// <summary>
    /// <b>How near two rivers of a map run at the least</b> (GEN-62), each the other's: a river drawn nearer one already
    /// laid is drawn again. Two rivers a few blocks apart would have met long before the town was built.
    /// </summary>
    public float RiversApartLeastM { get; init; } = 2000f;

    /// <summary>How far off the map's middle a river passes, or meets the coast, either way, as a share of the map's breadth there (GEN-62).</summary>
    public float RiverPassShare { get; init; } = 0.35f;

    /// <summary>How far a river flowing into the sea is turned off straight inland, either way (GEN-62).</summary>
    public float RiverInlandTurnRad { get; init; } = 0.8f;

    /// <summary>How finely the town's core is looked for (GEN-63): the side of the square each place tried stands in.</summary>
    public float CoreCellM { get; init; } = 50f;

    /// <summary>
    /// How far off the water the core stands at the least (GEN-63): a street's room off the bank and a block of the old town
    /// before it.
    /// </summary>
    public float CoreOffWaterLeastM { get; init; } = 80f;

    /// <summary>
    /// <b>How far off the water the core is best placed</b> (GEN-63): a town grew up at its river's crossing or its harbour, so
    /// its middle stands a block or two off the bank.
    /// </summary>
    public float CoreOffWaterBestM { get; init; } = 150f;

    /// <summary>How far round the core its ground is asked whether it is dry (GEN-63): the old town's few blocks.</summary>
    public float CoreLandReachM { get; init; } = 400f;

    /// <summary>
    /// <b>How much of the ground round the core is dry at the least</b> (GEN-63): a town grew up on land, beside the water
    /// and not in a wedge between two waters, where its old town has no room.
    /// </summary>
    public float CoreLandLeastShare { get; init; } = 0.7f;

    /// <summary>How much a draw moves the core's ranking, as a share of the map's short side (GEN-63), so two towns of one shape differ.</summary>
    public float CoreJitterShare { get; init; } = 0.15f;

    /// <summary>How much being near its best distance off the water weighs in the core's ranking against being near the middle (GEN-63).</summary>
    public float CoreWaterWeight { get; init; } = 1f;

    /// <summary>
    /// <b>How far inside the circle it touches an intercity road passing the town may be routed</b>, as a share of its radius
    /// (GEN-63): the search is barred from the ground nearer the core than this, so a road round a river is routed round the
    /// town and not through it.
    /// </summary>
    public float BypassInsideShare { get; init; } = 0.85f;

    /// <summary>
    /// <b>How much of the turn round the town an intercity road passing it spans</b> (GEN-63), from the bearing it enters past
    /// the map's edge on to the one it leaves on, the place it touches the circle round the solid middle halfway: near half
    /// a turn, so it runs on near the tangent there rather than doubling back.
    /// </summary>
    public float IntercitySpanDeg { get; init; } = 160f;

    /// <summary>How far a town's railways are turned off the bearing they run along, either way (GEN-63).</summary>
    public float RailTurnRad { get; init; } = 0.2f;

    /// <summary>How far a railway wanders off its bearing, as a share of the map's short side (GEN-63, RAIL-2).</summary>
    public float RailMeanderShare { get; init; } = 0.08f;

    /// <summary>How far along its bearing a railway's loop is moved off where the line passes the core, either way, as a share of the map's long side (RAIL-11).</summary>
    public float RailLoopShiftShare { get; init; } = 0.3f;

    /// <summary>How much further out, in <see cref="RailPassesCoreM"/>, each retry draws a railway that would not stand (GEN-63).</summary>
    public float RailRetryOutShare { get; init; } = 0.25f;

    /// <summary>How far an intercity road's entry is turned off its spread bearing a retry, alternately either way (GEN-63).</summary>
    public float IntercityEntryTurnRad { get; init; } = 0.12f;

    /// <summary>How far a draw turns an intercity road's entry off its spread bearing, either way (GEN-63).</summary>
    public float IntercityEntryJitterRad { get; init; } = 0.15f;

    /// <summary>
    /// How much wider than its class's floor an intercity route's corners are rounded (GEN-47, GEN-63): the rounding of a
    /// route's cells and the settling of its nodes take a little off the radius it was rounded at.
    /// </summary>
    public float IntercityRoundingShare { get; init; } = 1.25f;

    /// <summary>How many times its own length a crossing costs a route (GEN-63), which keeps a road off a barrier it can go round.</summary>
    public float RouteCrossingWeight { get; init; } = 3f;

    /// <summary>How wide, in route cells, a laid intercity road marks the cells a later road's route may end on it at (GEN-63).</summary>
    public float RouteMarkCells { get; init; } = 0.75f;

    /// <summary>How many times a region's middles are moved to the middle of what they took (GEN-65).</summary>
    public int DistrictSettles { get; init; } = 8;

    /// <summary>How many more districts a region is cut into, at the most, when its first cut leaves one too big (GEN-65).</summary>
    public int DistrictRecuts { get; init; } = 6;

    /// <summary>How far a district's bearing is turned off its own ground's longer axis, either way (GEN-74).</summary>
    public float DistrictBearingSpreadRad { get; init; } = 0.17f;

    /// <summary>The share of a town's districts nearest its core that are its old town, and at least one (GEN-74).</summary>
    public float OldTownShare { get; init; } = 0.1f;

    /// <summary>How far out, as a share of the furthest district's distance from the core, the housing gives way to the edge's kinds (GEN-74).</summary>
    public float InnerDistrictShare { get; init; } = 0.55f;

    /// <summary>How much of a block a street's own stub may be, before the junction it would make is too near the last one (GEN-72).</summary>
    public float ShortestStubInBlocks { get; init; } = 0.15f;

    /// <summary>How near the place its lattice line crosses a road a stub meets a node already there rather than one of its own (GEN-72).</summary>
    public float StubWeldM { get; init; } = 5f;

    /// <summary>How far apart along an intercity road the places a district fronts it are asked (GEN-69).</summary>
    public float FrontStationStepM { get; init; } = 50f;

    /// <summary>How much less a metre along an intercity road costs the demand than one along a district road: it is the faster (GEN-67).</summary>
    public float IntercityDemandWeight { get; init; } = 0.6f;

    /// <summary>How many times an edge between districts has its corners cut, so its steps off the cells are gone and it bends smoothly (GEN-66).</summary>
    public int SeamCornerCuts { get; init; } = 4;

    /// <summary>How far an edge between districts is thinned off the cells it was traced along, in cells (GEN-66).</summary>
    public float SeamThinCells { get; init; } = 0.75f;

    /// <summary>The shortest edge between districts laid as a road, in localities (GEN-16, GEN-69).</summary>
    public float SeamShortestLocalities { get; init; } = 3f;

    /// <summary>How far past an intercity road's corridor, in district cells, a district is read as fronting it (GEN-69).</summary>
    public float FrontCells { get; init; } = 2f;

    /// <summary>How far past an intercity road's corridor, in district cells, an edge between districts ending near it is run on to it (GEN-69).</summary>
    public float RunOnCells { get; init; } = 3f;

    /// <summary>How far past the ends of a crossing's approach, in district cells, the districts it joins are read (GEN-69).</summary>
    public float FacedCells { get; init; } = 3f;

    /// <summary>How near, in an intercity road's corridors, a junction planned on it is welded onto one already planned (GEN-68).</summary>
    public float StationWeldCorridors { get; init; } = 2f;

    /// <summary>How far, in a district's widest spacing, a link may run on to reach its district's edge road (GEN-69).</summary>
    public float ToTheEdgeRoadInBlocks { get; init; } = 2f;

    /// <summary>How far, in a district's widest spacing, a road in from an intercity road may run to the roads it joins (GEN-69).</summary>
    public float RoadInReachInBlocks { get; init; } = 4f;

    /// <summary>How wide, as a share of each crossing's own stretch of the barrier, the window its shortest site is looked for in is (GEN-70).</summary>
    public float CrossingWindowShare { get; init; } = 0.25f;
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

    /// <summary>
    /// <b>How far a route cell reaches from the link at its middle</b>, along the network: what a sectioned search
    /// cuts a network into, and so how much of it one plan floods. A few blocks of a town either way.
    /// </summary>
    public float RouteCellReachM { get; init; } = 200f;

    /// <summary>How many cells a search details and hands over, the one the body sets off in included.</summary>
    public int RouteSectionCells { get; init; } = 3;

    /// <summary>
    /// <b>The overlap</b>: how near the middle of the cell past a section the search reaches before it hands the
    /// section over, so the section's last turn is chosen with the ground beyond it in view.
    /// </summary>
    public float RouteAimWithinM { get; init; } = 50f;
}
