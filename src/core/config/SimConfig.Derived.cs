namespace TrafficSimulation.Core.Config;

/// <summary>
/// The relations between the authored figures. Nothing here may be overridden — moving one authored
/// ratio has to move everything that hangs off it, which is what makes a single constant rescale the town.
/// </summary>
internal sealed partial class SimConfig
{
    public float TickSeconds => 1f / Sim.TickRateHz;

    /// <summary>+1 where traffic keeps right, which with <c>+y</c> down is the way curvature counts positive.</summary>
    public float RoadSideSign => Road.TrafficKeepsRight ? 1f : -1f;

    public float CarFootprintAreaM2 => Car.LengthM * Car.WidthM;

    /// <summary>
    /// The nominal car's wheels stand at the corners of its own footprint, so its track is its width; the
    /// wheelbase is shorter than the body it sits under.
    /// </summary>
    public float CarTrackM => Car.WidthM;

    /// <summary>≈ 3.9 m, and what sizes a dead end's turning head.</summary>
    public float CarTurningRadiusM => Car.WheelbaseM / MathF.Tan(Car.MaxSteeringDeg * MathF.PI / 180f);

    /// <summary>How far the middle of the body stands ahead of the rear axle the line is driven for.</summary>
    public float CarCentreAheadOfAxleM => Car.WheelbaseM * 0.5f;

    /// <summary>How far the nose stands ahead of the rear axle, which every line the axle drives has to be read back through.</summary>
    public float CarNoseAheadOfAxleM => CarCentreAheadOfAxleM + Car.LengthM * 0.5f;

    /// <summary>
    /// The radius the parking templates are built at: the car's own turning circle with a margin, so the
    /// steering is not sitting on its stop for the whole arc.
    /// </summary>
    public float ParkingTemplateRadiusM => CarTurningRadiusM * Car.ReverseTemplateArcMargin;

    /// <summary>
    /// <b>The widest a line has to be drawn for a car to hold this speed round it</b> — the corner formula
    /// the speed profile reads, turned round. A template laid tighter is not refused; it is driven slower,
    /// because the profile's corner term reads the arcs of a template exactly as it reads the arcs of a road.
    /// </summary>
    public float CarCorneringRadiusM(float atMps, float groundCoefficient) =>
        atMps * atMps / (Tyre.GripMps2 * groundCoefficient * Driving.GripMargin);

    public float PropDiameterM => Car.WidthM * Prop.DiameterInCarWidths;

    public float PersonDiameterM => PropDiameterM * Person.DiameterInPropDiameters;

    public float PersonRunSpeedMps => Person.WalkSpeedMps * Person.RunFactor;

    public float PersonExitSearchRadiusM => PropDiameterM * Person.ExitSearchRadiusInPropDiameters;

    /// <summary>The longest walk anybody chooses, and the ceiling on the one a trip hands them.</summary>
    public float PersonWalkWorthM => CityGen.BlockSpacingAlongMinM * Person.WalkWorthInBlockSpacings;

    /// <summary>
    /// The one short straight hop everything off the walking network gets — a doorway, the ground beside a
    /// bay. The shortness is the whole safeguard: roughly one frontage depth, which is the pavement the
    /// building line stands behind plus the strip in front of it.
    /// </summary>
    public float PersonOffNetworkHopM => Road.PavementWidthM + Building.FrontGapM;

    /// <summary>
    /// The tightest circle the feet can hold at walking pace — the speed over the turn rate, 0.28 m at the
    /// shipped figures. <b>A line laid tighter than this is a line nothing can walk</b>: a body aiming at
    /// the far side of it turns as hard as it can and goes round rather than across.
    /// </summary>
    public float WalkerTightestTurnM => Person.WalkSpeedMps / (Person.TurnRateDegPerS * MathF.PI / 180f);


    public float WalkingLaneOffsetM => Road.PavementWidthM * Person.LaneOffsetFraction;

    /// <summary>
    /// The clear ground between one walker's reserved stretch and the next one's, which is what a queue on
    /// a pavement stands at — half a metre at the shipped figures.
    /// </summary>
    public float PersonStandstillGapM => PersonDiameterM * Person.StandstillGapInDiameters;

    /// <summary>
    /// How far off a pavement lane's own line a body is still standing on that lane: a quarter of the band,
    /// which is the half of the lane's ground it has either side of the line it is held on.
    /// </summary>
    public float WalkerOffLaneM => WalkingLaneOffsetM;

    public float RoadWidthM => Car.WidthM * Road.WidthInCarWidths;

    /// <summary>Half the carriageway is one direction's, and a lane's own line is the middle of that.</summary>
    public float LaneOffsetM => RoadWidthM * 0.25f;

    /// <summary>The ground the roads share: one road width.</summary>
    public float IntersectionReachM => RoadWidthM;

    public float IntersectionCornerRadiusM => Car.WidthM * Road.IntersectionCornerRadiusInCarWidths;

    /// <summary>
    /// How near two lines through a junction pass before one is driven over the other (TER-5c): a car's
    /// own width, because a line is where a body is driven and two lines a body's width apart are two
    /// bodies touching. Half the carriageway at the shipped figures, so two opposing straights clear each
    /// other by a metre.
    /// </summary>
    public float JunctionCrossingClearanceM => Car.WidthM;

    /// <summary>A street's own bend puts its inner kerb on exactly the flare radius a junction's corners use.</summary>
    public float RoadCornerRadiusM => IntersectionCornerRadiusM + RoadWidthM * 0.5f;

    public float BridgeDeckHalfWidthM => RoadWidthM * 0.5f + Road.PavementWidthM + Road.BridgeDeckMarginM;

    public float ParkingSpaceLengthM => Car.LengthM + Car.WidthM * Road.ParkingSpaceMarginInCarWidths * 2f;

    public float ParkingSpaceWidthM => Car.WidthM * (1f + Road.ParkingSpaceMarginInCarWidths * 2f);

    public float BuildingWalkablePaddingM => PersonDiameterM * Building.WalkablePaddingInPersonDiameters;

    /// <summary>Half a pavement band plus the front gap plus a person: how close a door counts as reached.</summary>
    public float WayInTouchingReachM => Road.PavementWidthM * 0.5f + Building.FrontGapM + PersonDiameterM;

    public float CarLookaheadFloorM => Car.LengthM * Driving.LookaheadFloorInCarLengths;

    public float CarLookaheadCeilingM => Car.LengthM * Driving.LookaheadCeilingInCarLengths;

    public float CarProjectionWindowM => Car.LengthM * Driving.ProjectionWindowInCarLengths;

    public float CarJunctionReserveM => Driving.NominalCarLengthM * Driving.JunctionReserveInCarLengths;

    /// <summary>
    /// <b>The ground a car keeps around itself</b> — asked for in front of its nose as part of its own
    /// stretch, and laid into the book behind its tail at <see cref="CarTailMarginM"/> (TER-4c.1), so that
    /// <b>what a queue at rest stands at and what a body in a junction is still swinging through are one
    /// figure and one stretch</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a margin on a lossy reading before it is a comfort.</b> The book puts a body on a way as one
    /// interval of that way's arclength, which is the whole width of the road thrown away: a crossing point
    /// is where two <em>lines</em> pass, and what has to be clear of it is a body that is off its own line by
    /// up to the road's tolerance and swings wider still at the back. A tail exactly on the far edge of a
    /// section is a body that may well still be standing on it, and this is what covers the difference.
    /// </para>
    /// <para>
    /// <b>It is measured, and it is at its floor.</b> Against the 0 wrecked, 56 touches and 97.9 mm peak
    /// interpenetration Odesa's soak gives at a body's width:
    /// <list type="bullet">
    /// <item>at nothing at all — a body's ground released at its bare tail — <b>2 wrecked and 923.6 mm</b>;</item>
    /// <item>at half a body's width, <b>2 wrecked and 263 touches</b>, near five times as many.</item>
    /// </list>
    /// Both are a car granted a crossing point that the body ahead of it is still swinging off. So the floor
    /// is a body's width, and a fleet tuned to queue closer than that gets the floor rather than the wreck:
    /// <see cref="DrivingFigures.StandstillGapInCarLengths"/> sets the gap and never lowers the margin.
    /// </para>
    /// <para>
    /// <b>What it costs is the stretch on the overlay behind a body</b> — a block that begins
    /// <see cref="CarTailMarginM"/> behind the tail, on every way that body is on. It was a claim of its own
    /// on a junction's join once, which made one body two occupants of one piece of ground and left a bar
    /// across the road behind a car that looked to have left it.
    /// </para>
    /// </remarks>
    public float CarBodyMarginM =>
        MathF.Max(Car.WidthM, Car.LengthM * Driving.StandstillGapInCarLengths);

    /// <summary>
    /// <b>The part of that ground a reservation keeps behind the tail</b>
    /// (<see cref="DrivingFigures.TailMarginShare"/>) — where a body's stretch begins, on every way it is on,
    /// and therefore where whoever comes up behind it is cut.
    /// </summary>
    /// <remarks>
    /// The end that swings widest is also the end that queues the road behind it, and the two ends are read
    /// by different traffic: in front the margin is this car's own cover against a bar or a body it is
    /// closing on, behind it is what the book owes the width it threw away. Only the tail is short of
    /// <see cref="CarBodyMarginM"/>, and how short is a question `--bench soak` answers.
    /// </remarks>
    public float CarTailMarginM => CarBodyMarginM * Driving.TailMarginShare;

    /// <summary>
    /// How far off its line a car is no longer on it: half a lane, which is the width of ground the lane
    /// it is meant to be in actually has to spare.
    /// </summary>
    public float CarOffPathM => LaneOffsetM;

    /// <summary>
    /// The lead every distance the speed profile measures is taken from — the staleness of the driver's
    /// own decision, about a metre at town speed. A car that planned from where it is arrives at each
    /// constraint one decision late.
    /// </summary>
    public float CarReactionS => Sim.AgentDecisionIntervalS;

    /// <summary>
    /// How fast the commanded acceleration may change: the whole travel of the pedal, from full brake to
    /// full throttle, over the time that travel takes — 129 m/s³ at the shipped figures.
    /// </summary>
    public float CarPedalRateMps3 => (Car.AccelerationMps2 + Car.BrakingMps2) / Driving.PedalTravelS;

    /// <summary>
    /// <b>What a driver may be doing at all before the tyres are what stops it</b>, on the ground it is on
    /// and with no margin kept back: the pedal's own cap, or what the patch puts down along the roll. The
    /// speed profile plans inside it (<see cref="DrivingFigures.BrakingMargin"/>) and `E-2` is the tick that
    /// margin ran out, so the two are the same figure read with and without it.
    /// </summary>
    public float CarUtmostBrakingMps2(float groundCoefficient) =>
        MathF.Min(Car.BrakingMps2, Tyre.GripMps2 * Tyre.LongAxisFactor * groundCoefficient);

    /// <summary>
    /// <b>How far ahead a car has to be able to see</b>: its stopping distance from its top speed, against
    /// what the tyres can put down and not what the pedal asks for, because that is the figure the profile
    /// brakes with. A line laid to the pedal's stopping distance is two and a half times too short. It is
    /// also the ceiling on any manoeuvre's own geometry — ground further off than this is ground nothing
    /// has looked at.
    /// </summary>
    public float CarSightM =>
        Car.MaxSpeedMps * Car.MaxSpeedMps
        / (2f * MathF.Min(Car.BrakingMps2, Tyre.GripMps2 * Tyre.LongAxisFactor) * Driving.GripMargin);

    public float CarCrossingPaceMps => Car.LengthM * Driving.CrossingPaceInCarLengthsPerS;

    public float CarCrossingStandOffM => Car.WidthM * Driving.CrossingStandOffInCarWidths;

    /// <summary>The blocked-road clock, 30 s at the shipped figures — four full red phases.</summary>
    public float CarBlockedRoadS => Signals.CycleS * Ladder.BlockedRoadInLightCycles;

    /// <summary>The fuse a car standing across a lane is measured on instead, 6 s at the shipped figures.</summary>
    public float CarShortFuseS => Ladder.ObstructionWaitS * Ladder.ShortFuseInObstructionWaits;

    public float CarLadderRewindM => Car.LengthM * Ladder.RewindInCarLengths;

    public float CarBlockedWayPriceM => CityGen.BlockSpacingAlongMinM * Ladder.BlockedWayPriceInBlockSpacings;

    public float CarBlockedWayLifeS => CarBlockedRoadS * Ladder.BlockedWayLifeInBlockedClocks;

    /// <summary>How early a pair is given a manifold. See <see cref="SolverFigures.AllowedPenetrationM"/>.</summary>
    public float SolverSpeculativeM => Solver.AllowedPenetrationM * 4f;

    /// <summary>
    /// The broad phase's cell. Sized at the query rather than at the population: a car's own box is what
    /// most cells are asked about, and a cell of two car lengths puts a car in one or two of them while
    /// keeping a hundred-metre ray's walk to a dozen.
    /// </summary>
    public float SolverCellSizeM => Car.LengthM * 2f;

    /// <summary>
    /// The bucket the proximity index is laid at: the widest question anything asks of it, which is the
    /// reach a walker keeps clear of a car it is running from.
    /// </summary>
    /// <remarks>
    /// Sized at the query and never at the body or the terrain cell. Too small and a query walks a field
    /// of empty buckets to find its handful of neighbours; too large and every query hands back most of
    /// the district. Laying it at the terrain cell — a metre — put 6.9 million buckets over Odesa.
    /// </remarks>
    public float ProximityBucketM => Person.FleeDistanceM;

    /// <summary>
    /// The cell a network's own lines are binned into, for the scans that ask which line a point is
    /// nearest. Sized at the road rather than at the query: what shares a cell is then the handful of
    /// pieces that actually run alongside each other, so the first ring holds the answer and the search
    /// stops at it.
    /// </summary>
    public float NearestChainCellM => RoadWidthM * 2f;
}
