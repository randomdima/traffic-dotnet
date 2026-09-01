namespace TrafficSimulation.Core.Config;

/// <summary>
/// The relations between the authored figures. Nothing here may be overridden — moving one authored
/// ratio has to move everything that hangs off it, which is what makes a single constant rescale the town.
/// </summary>
/// <remarks>
/// <b>Every <c>Car…</c> figure here is the nominal car's</b> (CAR-11a): it is what the town's own geometry
/// is laid against and what a variant is resolved against, and it is <em>not</em> what any car is driven by.
/// The figures a driver spends are on <see cref="Agents.Car.Body.CarBuild"/>, one build per look, and a
/// decision taken against these instead is a decision taken for a car nobody is in.
/// </remarks>
internal sealed partial class SimConfig
{
    public float TickSeconds => 1f / Sim.TickRateHz;

    /// <summary>+1 where traffic keeps right, which with <c>+y</c> down is the way curvature counts positive.</summary>
    public float RoadSideSign => Road.TrafficKeepsRight ? 1f : -1f;

    /// <summary>
    /// The nominal car's wheels stand at the corners of its own footprint, so its track is its width; the
    /// wheelbase is shorter than the body it sits under.
    /// </summary>
    public float CarTrackM => Car.WidthM;

    /// <summary>≈ 3.9 m, and what sizes a dead end's turning head.</summary>
    public float CarTurningRadiusM => Car.WheelbaseM / MathF.Tan(Car.MaxSteeringDeg * MathF.PI / 180f);

    /// <summary>How far the middle of the body stands ahead of the rear axle the line is driven for.</summary>
    public float CarCentreAheadOfAxleM => Car.WheelbaseM * 0.5f;

    /// <summary>
    /// <b>What the nominal car's tyres hold, as an acceleration</b>: the coefficient times a weight.
    /// Derived, and derived here once — nothing authors a grip in m/s², because a grip in m/s² is a
    /// coefficient and a gravity that somebody has already multiplied together.
    /// </summary>
    /// <remarks>
    /// <b>The same figure along the roll and across it, at any load.</b> A stop and a corner are worth the
    /// same here, which is Coulomb and is what a town watched from above can tell apart: the refinements
    /// that would separate them are each worth about a per cent, and a per cent of difference is a place to
    /// hide a fudge rather than a thing anybody sees. What the loads still decide is which <em>wheel</em>
    /// runs out first, not what the four hold between them.
    /// </remarks>
    public float TyreGripMps2 => Tyre.Friction * Tyre.StandardGravityMps2;

    /// <summary>What each ground costs a wheel simply going round, off its own coefficient and a weight.</summary>
    public float GrassDragMps2 => Terrain.GrassResistance * Tyre.StandardGravityMps2;

    public float PavedDragMps2 => Terrain.PavedResistance * Tyre.StandardGravityMps2;

    public float WaterDragMps2 => Terrain.WaterResistance * Tyre.StandardGravityMps2;

    /// <summary>
    /// The radius the parking templates are built at: the car's own turning circle with a margin, so the
    /// steering is not sitting on its stop for the whole arc.
    /// </summary>
    public float ParkingTemplateRadiusM => CarTurningRadiusM * Car.ParkingTemplateArcMargin;

    /// <summary>
    /// <b>The widest a line has to be drawn for a car to hold this speed round it</b> — the corner formula
    /// the speed profile reads, turned round. A template laid tighter is not refused; it is driven slower,
    /// because the profile's corner term reads the arcs of a template exactly as it reads the arcs of a road.
    /// </summary>
    public float CarCorneringRadiusM(float atMps, float groundCoefficient) =>
        atMps * atMps / (TyreGripMps2 * groundCoefficient * Driving.GripMargin);

    /// <summary>A run rather than a walk, because the town is watched at <see cref="PersonFigures.PaceScale"/> of life.</summary>
    public float PersonWalkSpeedMps => Person.RealWalkSpeedMps * Person.PaceScale;

    /// <summary>
    /// And the pivot at the same scale, because a body moving five times a real walk turns five times a
    /// real turn. It is what lets a walker turn nearly on the spot, and so what decides how much ground the
    /// pavement has to give up at every corner to be a line the feet can hold
    /// (<see cref="WalkerTightestTurnM"/>).
    /// </summary>
    public float PersonTurnRateDegPerS => Person.RealPivotDegPerS * Person.PaceScale;

    /// <summary>
    /// <b>What the feet hold</b>: whatever stops a body inside
    /// <see cref="PersonFigures.StopsWithinDiameters"/> of its own diameter at the pace it is going.
    /// <b>The relation is the figure</b> — move the pace or the body and this follows, which is the whole
    /// reason it is not a number somebody chose.
    /// </summary>
    public float PersonFootGripMps2 =>
        PersonWalkSpeedMps * PersonWalkSpeedMps / (2f * PersonDiameterM * Person.StopsWithinDiameters);

    public float PropDiameterM => Car.WidthM * Prop.DiameterInCarWidths;

    public float PersonDiameterM => PropDiameterM * Person.DiameterInPropDiameters;

    public float PersonExitSearchRadiusM => PropDiameterM * Person.ExitSearchRadiusInPropDiameters;

    /// <summary>What a casualty slides to a stop on, at the same scale as everything else the pace decides.</summary>
    public float PersonSlidingGripMps2 => PersonFootGripMps2 * Person.SlidingGripInFootGrips;

    /// <summary>
    /// <b>What a contact has to carry to leave somebody down in the road</b> (PER-23): the work of sliding
    /// a body <see cref="DamageFigures.SlideToCasualtyM"/> along the ground, which is its mass times the
    /// grip it slides on times that distance — 3.92 kJ, or a car meeting a standing body at 10 m/s.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the distance in the limit of a heavy vehicle and a little under it otherwise.</b> The
    /// energy a contact is judged by is the pair's reduced mass, so a person struck by a car of seventeen
    /// times their mass keeps about 95% of the closing speed and slides about 95% of the half metre. The
    /// figure the town is authored with is the distance, and the arithmetic is honest about the mass it
    /// actually has to move.
    /// </para>
    /// <para>
    /// <b>The band has to sit above <see cref="PersonFigures.WalkSpeedMps"/>, and it is the grip that puts
    /// it there</b> — half again over walking pace at the shipped figures. Nothing about the closing speed
    /// says who was carrying it (PER-23), so a band below the town's own pace is one a walker meets by
    /// arriving at a parked car.
    /// </para>
    /// </remarks>
    public float PersonCasualtyKj => Person.MassKg * PersonSlidingGripMps2 * Damage.SlideToCasualtyM / 1000f;

    /// <summary>The longest walk anybody chooses, and the ceiling on the one a trip hands them.</summary>
    public float PersonWalkWorthM => CityGen.BlockSpacingAlongMinM * Person.WalkWorthInBlockSpacings;

    /// <summary>
    /// The one short straight hop everything off the walking network gets — a doorway, the ground beside a
    /// bay. The shortness is the whole safeguard: roughly one frontage depth, which is the pavement the
    /// building line stands behind plus the strip in front of it.
    /// </summary>
    public float PersonOffNetworkHopM => PavementWidthM + Building.FrontGapM;

    /// <summary>
    /// The tightest circle the feet can hold at walking pace — the speed over the turn rate, 0.28 m at the
    /// shipped figures. <b>A line laid tighter than this is a line nothing can walk</b>: a body aiming at
    /// the far side of it turns as hard as it can and goes round rather than across.
    /// </summary>
    public float WalkerTightestTurnM => PersonWalkSpeedMps / (PersonTurnRateDegPerS * MathF.PI / 180f);


    /// <summary>
    /// One walking lane, two bodies wide (<see cref="RoadFigures.WalkingLaneInPersonDiameters"/>) — the
    /// width every stretch of pavement in the town is walked at.
    /// </summary>
    public float WalkingLaneWidthM => PersonDiameterM * Road.WalkingLaneInPersonDiameters;

    /// <summary>
    /// The walk beside a carriageway: one lane each way, so two walkers passing each stay on their own
    /// (TER-3c). It is the width a bridge deck carries and the depth the building line stands behind.
    /// </summary>
    public float PavementWidthM => WalkingLaneWidthM * LanesPerPavement;

    /// <summary>A walking lane's own line is the middle of its half of the band.</summary>
    public float WalkingLaneOffsetM => WalkingLaneWidthM * 0.5f;

    /// <summary>Half the walk, which is what stands a corner 4.83 m deep against the straight's 4 m.</summary>
    public float PavementCornerRadiusM => PavementWidthM * 0.5f;

    /// <summary>
    /// The clear ground between one walker's reserved stretch and the next one's, which is what a queue on
    /// a pavement stands at — half a metre at the shipped figures.
    /// </summary>
    public float PersonStandstillGapM => PersonDiameterM * Person.StandstillGapInDiameters;

    /// <summary>
    /// The room a walker leaves between itself and a body it is stepping round (PER-24) — a quarter of a
    /// metre at the shipped figures, on top of the two bodies' own radii.
    /// </summary>
    /// <remarks>
    /// <b>It is the least that gets past and is sized as nothing else.</b> A pavement lane's line is
    /// <see cref="WalkingLaneOffsetM"/> from the edge of the band, so a step round a body standing on that
    /// line reaches the ground beyond it whatever this is set to — which is why the side is answered by the
    /// terrain and not by a figure that could be tuned until it fits.
    /// </remarks>
    public float PersonShoulderRoomM => PersonDiameterM * Person.ShoulderRoomInDiameters;

    /// <summary>
    /// <b>How far past a kerb line the middle of a body may be while it steps round somebody</b> (PER-24) —
    /// half a metre at the shipped figures, which is a body at the channel and over the kerb rather than one
    /// standing in a lane.
    /// </summary>
    /// <remarks>
    /// <b>It is measured off the lane's band and never off the ground grid.</b> A walker's line runs
    /// <see cref="WalkingLaneOffsetM"/> from the edge of its band and a step reaches
    /// <see cref="PersonShoulderRoomM"/> past the two bodies, so a step round somebody on that line is a
    /// quarter of a body over the kerb — under this, and taken, which is what stops nearly every step in the
    /// town being turned back the other way.
    /// </remarks>
    public float PersonRoadGrazeM => PersonDiameterM * Person.RoadGrazeInDiameters;

    /// <summary>
    /// How far off a pavement lane's own line a body is still standing on that lane: a quarter of the band,
    /// which is the half of the lane's ground it has either side of the line it is held on.
    /// </summary>
    public float WalkerOffLaneM => WalkingLaneOffsetM;

    /// <summary>
    /// How many lanes a carriageway carries: one each way (TER-4a). It is what makes a road's width a lane
    /// question rather than a width somebody chose.
    /// </summary>
    public const int LanesPerCarriageway = 2;

    /// <summary>The same for the walk beside it, which is walked keeping right exactly as the road is.</summary>
    public const int LanesPerPavement = 2;

    /// <summary>
    /// One traffic lane, 3.6 m at the shipped car (<see cref="RoadFigures.LaneWidthInCarWidths"/>).
    /// <b>Every carriageway this build lays is laid at this</b>, so a figure quoted against a lane — a line's
    /// offset, a kerb, a bar's span — means the same thing on every map (GEN-15).
    /// </summary>
    public float LaneWidthM => Car.WidthM * Road.LaneWidthInCarWidths;

    public float RoadWidthM => LaneWidthM * LanesPerCarriageway;

    /// <summary>
    /// The whole width of ground a road takes: its carriageway and the walk either side of it. <b>It is
    /// how far apart two roads' own lines have to stand to be two roads</b> (GEN-17), and it is what a
    /// bridge's deck carries over the water.
    /// </summary>
    public float RoadFootprintM => RoadWidthM + (PavementWidthM * 2f);

    /// <summary>Half the carriageway is one direction's, and a lane's own line is the middle of that.</summary>
    public float LaneOffsetM => LaneWidthM * 0.5f;

    /// <summary>The ground the roads share: one road width.</summary>
    public float IntersectionReachM => RoadWidthM;

    public float IntersectionCornerRadiusM => Car.WidthM * Road.IntersectionCornerRadiusInCarWidths;

    /// <summary>The sharpest corner a junction turns, as the half-angle every kerb fillet is solved on.</summary>
    public float ArmsApartMinRad => CityGen.ArmsApartMinDeg * MathF.PI / 180f;

    /// <summary>
    /// The fillet a corner whose arms stand that far apart is turned on: the junction's own radius, unless
    /// the corner is skew enough that a full-sized one would run back along the arm further than a kerb
    /// transition may reach (<see cref="RoadFigures.JunctionFilletReachInCarWidths"/>).
    /// </summary>
    public float JunctionFilletRadiusM(float armsApartRad) =>
        MathF.Min(IntersectionCornerRadiusM, JunctionFilletReachM * MathF.Tan(armsApartRad * 0.5f));

    public float JunctionFilletReachM => Car.WidthM * Road.JunctionFilletReachInCarWidths;

    /// <summary>
    /// <b>How far along an arm a junction reaches, corner by corner</b>: where the fillet between two arms
    /// that far apart lets go of the kerb, which is where the ground the roads share ends and the arm's own
    /// paint begins. An arm is reached by each of its two corners and stands off the further of them.
    /// </summary>
    /// <remarks>
    /// It grows as the corner sharpens — two kerbs meeting at an angle cross well outside the mouth — so the
    /// crossing and the bar on a skew arm stand further out than on a square one and both are the same stride
    /// off their own junction. <b>Never the distance from the node</b>, which is the same everywhere and
    /// right nowhere.
    /// </remarks>
    public float JunctionArmReachM(float armsApartRad) =>
        ((RoadWidthM * 0.5f) + JunctionFilletRadiusM(armsApartRad)) / MathF.Tan(armsApartRad * 0.5f);

    /// <summary>The furthest that ever is: the reach at the sharpest corner a junction may turn (GEN-13).</summary>
    public float JunctionArmReachMaxM => JunctionArmReachM(ArmsApartMinRad);

    /// <summary>
    /// How much of a road either end is straight. <b>It is what is laid on it and never a length somebody
    /// chose</b> (GEN-12): the junction's own ground and its fillet at their worst, then the crossing at its
    /// setback, then the bar behind that — so a road laid to this carries every one of them across a straight
    /// arm however skew its junctions came out, and a wider carriageway lengthens the stub rather than pushing
    /// its own paint onto the bend.
    /// </summary>
    public float StraightStubM =>
        JunctionArmReachMaxM + Road.CrossingSetbackM + Road.CrossingDepthM + Road.StopBarSetbackM
        + Road.StopBarThicknessM;

    /// <summary>
    /// How near two lines through a junction pass before one is driven over the other (TER-5c): a car's
    /// own width, because a line is where a body is driven and two lines a body's width apart are two
    /// bodies touching. Half the carriageway at the shipped figures, so two opposing straights clear each
    /// other by a metre.
    /// </summary>
    public float JunctionCrossingClearanceM => Car.WidthM;

    /// <summary>A street's own bend puts its inner kerb on exactly the flare radius a junction's corners use.</summary>
    public float RoadCornerRadiusM => IntersectionCornerRadiusM + RoadWidthM * 0.5f;

    public float ParkingSpaceLengthM => Car.LengthM + Car.WidthM * Road.ParkingSpaceMarginInCarWidths * 2f;

    public float ParkingSpaceWidthM => Car.WidthM * (1f + Road.ParkingSpaceMarginInCarWidths * 2f);

    /// <summary>
    /// <b>How much of a bay's own way the body standing nose-first in it reaches back over</b>: from the
    /// mouth of the bay to the axle the way ends at, which for a car square in the middle of its space
    /// (GEN-4i) is half the space behind the axle. It is the ceiling on what a body standing there holds
    /// (<see cref="World.Parking.BayStandings"/>).
    /// </summary>
    public float ParkingStandingGroundM => (ParkingSpaceLengthM * 0.5f) - CarCentreAheadOfAxleM;

    /// <summary>
    /// And backed in, where the same body stands over the same ground but its axle is at the deep end of
    /// the space instead (GEN-4j) — so the way runs a wheelbase's half further in and the body reaches
    /// that much further back along it.
    /// </summary>
    public float ParkingBackedInStandingGroundM => (ParkingSpaceLengthM * 0.5f) + CarCentreAheadOfAxleM;

    /// <summary>How far before a bay a way in leaves its lane, which is also the run-in the template needs.</summary>
    public float ParkingStagedInM => Car.LengthM * Road.ParkingStagedInCarLengths;

    /// <summary>And how much straight it ends on, which is what puts the car in the bay square.</summary>
    public float ParkingStraightensUpM => Car.LengthM * Road.ParkingStraightensUpInCarLengths;

    /// <summary>
    /// How far beyond a car park's own frontage the road is cut for it, so that the run-in every bay's
    /// way in wants stands inside the section's own stretch rather than on the street before it.
    /// </summary>
    public float ParkingSectionSetbackM => ParkingStagedInM;

    public float ParkingSectionShortestStretchM => Car.LengthM * Road.ParkingSectionShortestStretchInCarLengths;

    /// <summary>Half a pavement band plus the front gap plus a person: how close a door counts as reached.</summary>
    public float WayInTouchingReachM => PavementWidthM * 0.5f + Building.FrontGapM + PersonDiameterM;

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
    /// <b>What the brake pedal may ask for</b>, which stands well clear of what the tyres will hold
    /// (<see cref="CarFigures.BrakePedalInTyreGrips"/>). It is a ceiling and never a stopping figure: every
    /// stop in this town is taken off <see cref="TyreGripMps2"/>, and this only has to be high enough
    /// never to be the thing in the way.
    /// </summary>
    public float CarBrakingMps2 => TyreGripMps2 * Car.BrakePedalInTyreGrips;

    /// <summary>
    /// <b>What the throttle may ask the nominal car for</b>, which is what its driven axle puts down
    /// (<see cref="CarFigures.DrivePedalInDrivenGrips"/>, CAR-45). The nominal car drives one axle and stands
    /// evenly on two (<see cref="CarFigures.StaticFrontShare"/>), so half its grip is the whole of its pedal;
    /// a variant's own is <see cref="Agents.Car.Body.CarBuild.AccelerationMps2"/>, off its own layout.
    /// </summary>
    public float CarAccelerationMps2 =>
        TyreGripMps2 * (1f - Car.StaticFrontShare) * Car.DrivePedalInDrivenGrips;

    /// <summary>
    /// How fast the commanded acceleration may change: the whole travel of the pedal, from full brake to
    /// full throttle, over the time that travel takes.
    /// </summary>
    public float CarPedalRateMps3 => (CarAccelerationMps2 + CarBrakingMps2) / Driving.PedalTravelS;

    /// <summary>
    /// <b>How far ahead a car has to be able to see</b>: its stopping distance from its top speed, against
    /// what the tyres can put down and not what the pedal asks for, because that is the figure the profile
    /// brakes with. A line laid to the pedal's stopping distance is two and a half times too short. It is
    /// also the ceiling on any manoeuvre's own geometry — ground further off than this is ground nothing
    /// has looked at.
    /// </summary>
    public float CarSightM =>
        Car.MaxSpeedMps * Car.MaxSpeedMps
        / (2f * MathF.Min(CarBrakingMps2, TyreGripMps2) * Driving.GripMargin);

    public float CarCrossingStandOffM => Car.WidthM * Driving.CrossingStandOffInCarWidths;

    /// <summary>The blocked-road clock, 30 s at the shipped figures — four full red phases.</summary>
    public float CarBlockedRoadS => Signals.CycleS * Ladder.BlockedRoadInLightCycles;

    /// <summary>The fuse a car standing across a lane is measured on instead, 6 s at the shipped figures.</summary>
    public float CarShortFuseS => Ladder.ObstructionWaitS * Ladder.ShortFuseInObstructionWaits;

    public float CarLadderRewindM => Car.LengthM * Ladder.RewindInCarLengths;

    /// <summary>How long a turn on the spot has to come round in (`P-19`), 18 s at the shipped figures.</summary>
    public float CarShuntRoundS => CarShortFuseS * Ladder.ShuntRoundInShortFuses;

    /// <summary>And how far round one leg of it sweeps, as an angle.</summary>
    public float CarShuntSweepRad => Driving.ShuntSweepDeg * MathF.PI / 180f;

    public float CarBlockedWayPriceM => CityGen.BlockSpacingAlongMinM * Ladder.BlockedWayPriceInBlockSpacings;

    public float CarBlockedWayLifeS => CarBlockedRoadS * Ladder.BlockedWayLifeInBlockedClocks;

    /// <summary>How near its standoff mark an ambulance has to have stopped before the crew get out (AMB-10).</summary>
    public float AmbulanceSceneReachM => Car.LengthM * Ambulance.SceneReachInCarLengths;

    /// <summary>And how far short of the casualty that mark stands, which is what the crew then walk (AMB-10).</summary>
    public float AmbulanceStandoffM => Car.LengthM * Ambulance.StandoffInCarLengths;

    /// <summary>How far from its hospital an ambulance waits, which is the walk-worth distance said of a bay.</summary>
    public float AmbulanceHomeM => CityGen.BlockSpacingAlongMinM * Ambulance.HomeWithinBlockSpacings;

    /// <summary>How long a call runs before the casualty is written off as unreachable, 120 s at the shipped figures.</summary>
    public float AmbulanceGiveUpS => CarBlockedRoadS * Ambulance.GiveUpInBlockedClocks;

    /// <summary>And how far from its own building a police car or an evacuator stands waiting (SRV-2).</summary>
    public float ServiceHomeM => CityGen.BlockSpacingAlongMinM * Service.HomeWithinBlockSpacings;

    /// <summary>How long one leg of a beat may run before the patrol is sent somewhere else (SRV-5).</summary>
    public float PatrolGiveUpS => CarBlockedRoadS * Service.GiveUpInBlockedClocks;

    /// <summary>How long a hand who is out has to walk back to their seat before they are put in it (SRV-3).</summary>
    public float ServiceRecallS => CarBlockedRoadS * Service.RecallInBlockedClocks;

    /// <summary>How much road an officer holds either side of the scene he is closing (SRV-6).</summary>
    public float PoliceClosureM => Car.LengthM * Service.ClosureInCarLengths;

    /// <summary>And how far short of that scene his own car is parked (SRV-6).</summary>
    public float PoliceStandoffM => Car.LengthM * Service.SceneStandoffInCarLengths;

    /// <summary>How long a closure may stand before the lane is given back to the town (SRV-6).</summary>
    public float PoliceClosureLifeS => CarBlockedRoadS * Service.ClosureInBlockedClocks;

    /// <summary>How near the wreck an evacuator has to stop before the crew can get a hook on it (EVA-5).</summary>
    public float EvacuatorSceneReachM => Car.LengthM * Evacuator.SceneReachInCarLengths;

    /// <summary>And how near a yard slot it has to have got before the crew can set the wreck down in it (EVA-6).</summary>
    public float EvacuatorYardReachM => Car.LengthM * Evacuator.YardReachInCarLengths;

    /// <summary>How long one leg of a recovery may run before it is written off (EVA-8).</summary>
    public float EvacuatorGiveUpS => CarBlockedRoadS * Evacuator.GiveUpInBlockedClocks;

    /// <summary>The ceiling on what the tow bar may spend, as an acceleration on the pair's reduced mass (EVA-5).</summary>
    public float EvacuatorHitchMostMps2 => Evacuator.HitchMostInGrips * Tyre.StandardGravityMps2;

    /// <summary>How near an ordered place a car has to have stopped before that order is finished (CTL-8a).</summary>
    public float OrderedPlaceReachM => Car.LengthM * Control.PlaceReachInCarLengths;

    /// <summary>How far back along the road an ordered car is aimed at the one it is following (CTL-8c).</summary>
    public float OrderedFollowGapM => Car.LengthM * Control.FollowGapInCarLengths;

    /// <summary>And how far that one moves before the route after it is drawn again (CTL-8c).</summary>
    public float OrderedFollowRedrawM => Car.LengthM * Control.FollowRedrawInCarLengths;

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
