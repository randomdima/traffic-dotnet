using TrafficSimulation.Core.Geometry;

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
    /// <summary>
    /// <b>The figures a tick spends per car or per wheel, worked out on their first read and kept</b>, so a hot path
    /// loads them rather than dividing for them every time.
    /// </summary>
    /// <remarks>
    /// <b>Only figures off groups that are set once</b>: every authored group is init-only and the override file is
    /// applied before anything reads a figure (<see cref="Load"/>), so the first read sees the figures the run keeps.
    /// Nothing off <see cref="Trim"/>, which a running session moves, may be laid here.
    /// </remarks>
    sealed class PerTick(SimConfig config)
    {
        public readonly float TickSeconds = 1f / config.Sim.TickRateHz;

        public readonly float SlipBackOff = (1f / config.Sim.TickRateHz) / config.Tyre.SlipBackOffS;

        public readonly float SlipRecover = (1f / config.Sim.TickRateHz) / config.Tyre.SlipRecoverS;

        // A walker's own figures, read several times a walker a tick. Struck as the getters below strike them, and
        // never through them: a getter reads Laid, which is still being laid here.
        public readonly float WalkSpeedMps = config.Person.RealWalkSpeedMps * config.Person.PaceScale;

        public readonly float DiameterM =
            config.Car.WidthM * config.Prop.DiameterInCarWidths * config.Person.DiameterInPropDiameters;
    }

    PerTick? _perTick;

    PerTick Laid => _perTick ??= new PerTick(this);

    public float TickSeconds => Laid.TickSeconds;

    /// <summary>How much of the throttle a self-driver lifts in one tick while a driven wheel slides (<see cref="TyreFigures.SlipBackOffS"/>).</summary>
    public float TyreSlipBackOffPerTick => Laid.SlipBackOff;

    /// <summary>And how much it gives back in one tick once none does (<see cref="TyreFigures.SlipRecoverS"/>).</summary>
    public float TyreSlipRecoverPerTick => Laid.SlipRecover;

    /// <summary>
    /// <see cref="LampFigures.BeaconPeriodS"/> as a rate, divided once here so the device is handed the host's own
    /// float and a lamp kernel needs no quotient.
    /// </summary>
    public float BeaconHz => 1f / Lamps.BeaconPeriodS;

    /// <summary>+1 where traffic keeps right, which with <c>+y</c> down is the way curvature counts positive.</summary>
    public float RoadSideSign => Road.TrafficKeepsRight ? 1f : -1f;

    /// <summary>
    /// The nominal car's wheels stand at the corners of its own footprint, so its track is its width; the
    /// wheelbase is shorter than the body it sits under.
    /// </summary>
    public float CarTrackM => Car.WidthM;

    /// <summary>≈ 3.9 m: the nominal car's own circle, the rear axle's at full lock.</summary>
    public float CarTurningRadiusM => Car.WheelbaseM / Trig.Tan(Car.MaxSteeringDeg * MathF.PI / 180f);

    /// <summary>How far the middle of the body stands ahead of the rear axle the line is driven for.</summary>
    public float CarCentreAheadOfAxleM => Car.WheelbaseM * 0.5f;

    /// <summary><see cref="DrivingFigures.ParkingSwingMostDeg"/>, as the angle it is turned through.</summary>
    public float ParkingSwingMostRad => Driving.ParkingSwingMostDeg * MathF.PI / 180f;

    /// <summary><see cref="DrivingFigures.ParkedSquareWithinDeg"/>, as the angle it is turned through.</summary>
    public float ParkedSquareWithinRad => Driving.ParkedSquareWithinDeg * MathF.PI / 180f;

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

    /// <summary>
    /// What the drivetrain holds a car back by with the throttle shut, off its coefficient and a weight
    /// (<see cref="CarFigures.EngineBrakingResistance"/>) — about 1 m/s², where tarmac's own drag is a tenth of it.
    /// </summary>
    public float CarEngineBrakingMps2 => Car.EngineBrakingResistance * Tyre.StandardGravityMps2;

    /// <summary>
    /// <b>The widest a line has to be drawn for a car to hold this speed round it</b> — the corner formula
    /// the speed profile reads, turned round. A line laid tighter is not refused; it is driven slower,
    /// because the profile's corner term reads the arcs of every line exactly as it reads the arcs of a road.
    /// On tarmac, the one ground a line is laid over.
    /// </summary>
    public float CarCorneringRadiusM(float atMps) =>
        atMps * atMps / (TyreGripMps2 * Terrain.PavedCoefficient * Driving.GripMargin);

    /// <summary>A walk, at <see cref="PersonFigures.PaceScale"/> of life's.</summary>
    public float PersonWalkSpeedMps => Laid.WalkSpeedMps;

    /// <summary>
    /// And the pivot at the same scale, because a body moving at a multiple of a real walk turns at that
    /// multiple of a real turn. It is what lets a walker turn nearly on the spot, and so what decides how much ground the
    /// pavement has to give up at every corner to be a line the feet can hold
    /// (<see cref="WalkerTightestTurnM"/>).
    /// </summary>
    public float PersonTurnRateDegPerS => Person.RealPivotDegPerS * Person.PaceScale;

    /// <summary>
    /// <b>What the feet hold</b>: whatever stops a body shoved at walking pace inside
    /// <see cref="PersonFigures.StopsWithinDiameters"/> of its own diameter. <b>The relation is the figure</b>
    /// — move the pace or the body and this follows, which is the whole reason it is not a number somebody
    /// chose.
    /// </summary>
    /// <remarks>
    /// <b>It bounds what was done to a walker and never what it does</b>: its own change of pace is delivered
    /// whole (PER-3), and only the difference a contact made is taken back at this.
    /// </remarks>
    public float PersonFootGripMps2 =>
        PersonWalkSpeedMps * PersonWalkSpeedMps / (2f * PersonDiameterM * Person.StopsWithinDiameters);

    public float PropDiameterM => Car.WidthM * Prop.DiameterInCarWidths;

    public float PersonDiameterM => Laid.DiameterM;

    /// <summary>A walker's body as the solver stands it, and as the town and the device read its ground under it.</summary>
    public float PersonRadiusM => PersonDiameterM * 0.5f;

    public float PersonExitSearchRadiusM => PropDiameterM * Person.ExitSearchRadiusInPropDiameters;

    /// <summary>What a casualty slides to a stop on, at the same scale as everything else the pace decides.</summary>
    public float PersonSlidingGripMps2 => PersonFootGripMps2 * Person.SlidingGripInFootGrips;

    /// <summary>
    /// <b>What a contact has to carry to leave somebody down in the road</b> (PER-23): the work of sliding
    /// a body <see cref="DamageFigures.SlideToCasualtyM"/> along the ground, which is its mass times the
    /// grip it slides on times that distance — 3.76 kJ, or a car meeting a standing body at 10 m/s.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is the distance in the limit of a heavy vehicle and a little under it otherwise.</b> The
    /// energy a contact is judged by is the pair's reduced mass, so a person struck by a car of seventeen
    /// times their mass keeps about 95% of the closing speed and slides about 95% of the distance. The
    /// figure the town is authored with is the distance, and the arithmetic is honest about the mass it
    /// actually has to move.
    /// </para>
    /// <para>
    /// <b>The band has to sit above <see cref="PersonWalkSpeedMps"/></b> — several times over it at the
    /// shipped figures. Nothing about the closing speed says who was carrying it (PER-23), so a band below
    /// the town's own pace is one a walker meets by arriving at a parked car.
    /// </para>
    /// </remarks>
    public float PersonCasualtyKj => Person.MassKg * PersonSlidingGripMps2 * Damage.SlideToCasualtyM / 1000f;

    /// <summary>How far from a place the bays a car may be given there are looked for: a spawn's, and a leg's retarget.</summary>
    public float PersonWalkWorthM => CityGen.BlockSpacingAlongMinM * Person.WalkWorthInBlockSpacings;

    /// <summary>The furthest a trip is walked (PER-29), 285 m at the shipped figures.</summary>
    public float PersonWalkReachM => CityGen.BlockSpacingAlongMinM * Person.WalksNoFurtherInBlockSpacings;

    /// <summary>How far from a person their next destination is drawn (PER-9), 570 m at the shipped figures.</summary>
    public float PersonTripReachM => CityGen.BlockSpacingAlongMinM * Person.TripReachInBlockSpacings;

    /// <summary>
    /// The one short straight hop everything off the walking network gets — a doorway, the ground beside a
    /// bay. The shortness is the whole safeguard: roughly one frontage depth, which is the pavement the
    /// building line stands behind plus the strip in front of it.
    /// </summary>
    public float PersonOffNetworkHopM => PavementWidthM + Building.FrontGapM;

    /// <summary>How long a walker waits at a full door before giving the place up (PER-11), 9 min at the shipped figures.</summary>
    public float PersonPlacePatienceS => Building.DwellMaxS * Building.PlacePatienceInDwells;

    /// <summary>
    /// The tightest circle the feet can hold at walking pace — the speed over the turn rate, 0.28 m at the
    /// shipped figures. <b>A line laid tighter than this is a line nothing can walk</b>: a body aiming at
    /// the far side of it turns as hard as it can and goes round rather than across.
    /// </summary>
    public float WalkerTightestTurnM => PersonWalkSpeedMps / (PersonTurnRateDegPerS * MathF.PI / 180f);

    /// <summary>
    /// One walking lane (<see cref="RoadFigures.WalkingLaneInPropDiameters"/>) — the width every stretch of
    /// pavement in the town is walked at.
    /// </summary>
    public float WalkingLaneWidthM => PropDiameterM * Road.WalkingLaneInPropDiameters;

    /// <summary>
    /// The walk beside a carriageway: one lane each way, so two walkers passing each stay on their own
    /// (TER-3c). It is the width a bridge deck carries and the depth the building line stands behind.
    /// </summary>
    public float PavementWidthM => WalkingLaneWidthM * LanesPerPavement;

    /// <summary>A walking lane's own line is the middle of its half of the band.</summary>
    public float WalkingLaneOffsetM => WalkingLaneWidthM * 0.5f;

    /// <summary>
    /// <b>How far off the driven ground's own boundary one walking lane's line runs</b>, the pavement's band
    /// lying against that boundary (WLK-9): half a lane for the one against the kerb and a whole lane further
    /// for each one behind it.
    /// </summary>
    /// <remarks>
    /// <b>The one place the figure is struck</b>, because two constructions read it: the points a node hands
    /// a way over at, and the boundary moved off itself that the way between them is taken from. Struck twice,
    /// the line would not run through the points.
    /// </remarks>
    public float WalkingLaneAtM(int lane) => WalkingLaneOffsetM + (lane * WalkingLaneWidthM);

    /// <summary>
    /// <b>How far off the driven ground's own boundary the walk's own ground begins</b> (TER-3c.3): a kerb.
    /// It is a <em>line</em>'s figure and not a layer's, so nothing offsets the boundary by it.
    /// </summary>
    /// <remarks>
    /// <b>The kerbstone's own outer face stands at half of this</b>, the boundary running down the middle of
    /// the stone (TER-3d); what the other half buys is that the concrete a walker uses begins clear of the
    /// stone rather than on it.
    /// </remarks>
    public float WalkInnerM => Road.KerbWidthM;

    /// <summary>
    /// <b>How far off that boundary the pavement's outer face stands</b> (TER-3c.3): a kerb and a walk. The
    /// ground within it is the layer the concrete is drawn as. The walking lanes are measured off the
    /// boundary itself and not off <see cref="WalkInnerM"/> (<see cref="WalkingLaneAtM"/>), so they stand a
    /// kerb's width nearer the carriageway than the middle of their halves of this band.
    /// </summary>
    public float WalkOuterM => WalkInnerM + PavementWidthM;

    /// <summary>
    /// <b>How far off that boundary the walk's own kerb reaches</b> (TER-3c.3): half a kerb beyond the
    /// pavement's outer face, that kerb straddling it (TER-3d), and the last of the figures one boundary is
    /// read at.
    /// </summary>
    public float WalkKerbOuterM => WalkOuterM + (Road.KerbWidthM * 0.5f);

    /// <summary>
    /// <b>How far the ground's own carriageway runs on under a bridge past its bridgehead</b> (TER-7b): the corner
    /// its boundary is rounded at and a kerb, so the rounded end and the kerb struck round it both stand under the
    /// deck drawn over them and the road on the ground meets the bridge straight.
    /// </summary>
    public float UnderTheDeckM => Road.LineRoundedM + Road.KerbWidthM;

    /// <summary>How far a rail bridge's deck reaches either side of its track (RAIL-8): half its ballast and the overhang past it.</summary>
    public float RailDeckHalfM => (Rail.BallastWidthM * 0.5f) + Rail.BridgeDeckOverhangM;

    /// <summary>
    /// <b>How far past the map's edge the ground of a road running off it is laid</b> (GEN-2b, TER-7b): the last
    /// figure one boundary is read at and the corner it is rounded at, so the boundary's turn round the road's end and
    /// every layer and kerb struck off it stand off the map, and what the map keeps of them runs straight across its
    /// edge.
    /// </summary>
    public float PastTheMapEdgeM => WalkKerbOuterM + Road.LineRoundedM;

    /// <summary>
    /// <b>The building line</b> (GEN-54, TER-3c.2): how far off the driven ground's boundary a front wall
    /// stands, which is the outer face of the walk's own kerbstone — so a building touches the kerb it
    /// fronts and nothing it is made of stands on the concrete.
    /// </summary>
    public float BuildingLineM => WalkKerbOuterM;

    /// <summary>
    /// <b>How far off that boundary a building's way in stands</b> (GEN-54, GEN-2a): the line of the
    /// walking lane furthest from the carriageway, which is the one running past the front wall. It is on
    /// the pavement, clear of both kerbs, and on a line the walk is actually held on (GEN-5).
    /// </summary>
    public float BuildingWayInM => WalkingLaneAtM(LanesPerPavement - 1);

    /// <summary>
    /// <b>How much deeper into the verge the walk reaches at a corner than down a straight</b>: half a
    /// walk, which covers the 0.41 of one a right angle grown on the full width actually stands proud by
    /// (TER-3c.3). A prop clears the drawn ground itself and no collar over it (GEN-6a), so nothing laid reads this.
    /// </summary>
    public float PavementCornerReachM => PavementWidthM * 0.5f;

    /// <summary>
    /// The clear ground between one walker's claimed stretch and the next one's, which is what a queue on
    /// a pavement stands at — a metre at the shipped figures.
    /// </summary>
    public float PersonStandstillGapM => PersonDiameterM * Person.StandstillGapInDiameters;

    /// <summary>
    /// How far along the way in front of it a walker looks — 2 m at the shipped figures. It is what the
    /// follower is aimed at (PER-25), the ground ahead being the way's own line rather than a straight
    /// anybody laid, and the window its place on that way is searched in.
    /// </summary>
    public float PersonWalkAheadM => PersonDiameterM * Person.WalkAheadInDiameters;

    /// <summary>How far past what it needs a walker asks for its walk (<see cref="PersonFigures.ClaimSlackInDiameters"/>) — 2 m at the shipped figures.</summary>
    public float PersonClaimSlackM => PersonDiameterM * Person.ClaimSlackInDiameters;

    /// <summary>How far a walker walks before letting go of what it holds behind it (<see cref="PersonFigures.TrimSlackInDiameters"/>).</summary>
    public float PersonTrimSlackM => PersonDiameterM * Person.TrimSlackInDiameters;

    /// <summary>
    /// <b>How far a walker covers in one tick at its pace</b> — 0.044 m at the shipped figures. A walker has no
    /// acceleration of its own (PER-3), so it is also the whole of what one takes to come to rest.
    /// </summary>
    public float PersonStepM => PersonWalkSpeedMps * TickSeconds;

    /// <summary>
    /// <b>The least a body's heading may share with a driver's line and still be travelling it</b>, as the cosine of
    /// <see cref="DrivingFigures.TravelsTheLineWithinDeg"/> — 0.71 at the shipped figures.
    /// </summary>
    public float TravelsTheLineCos => MathF.Cos(Driving.TravelsTheLineWithinDeg * MathF.PI / 180f);

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
    /// how far apart two roads' own lines have to stand to be two roads</b> (GEN-49), and it is what a
    /// bridge's deck carries over the water.
    /// </summary>
    public float RoadFootprintM => RoadWidthM + (PavementWidthM * 2f);

    /// <summary>
    /// <b>The one grid every index over the map is laid on</b> (SIM-8), its main cell
    /// <see cref="SimFigures.GridCellInCarWidths"/> car widths across.
    /// </summary>
    public WorldGrid Grid => new(Car.WidthM * Sim.GridCellInCarWidths);

    /// <summary>The level the ribbon atlas's points stand on (<see cref="RoadFigures.RibbonPointsAcrossGridCell"/>).</summary>
    public GridLevel RibbonLevel => Grid.Split(Road.RibbonPointsAcrossGridCell);

    /// <summary>How far apart the ribbon atlas's points stand.</summary>
    public float RibbonLatticeStepM => RibbonLevel.CellM;

    /// <summary>The level the town's boundary is answered off (<see cref="TerrainFigures.ShellCellsAcrossGridCell"/>).</summary>
    public GridLevel ShellLevel => Grid.Split(Terrain.ShellCellsAcrossGridCell);

    /// <summary>How deep two ribbons' shared ground has to be before they are marked (<see cref="RoadFigures.RibbonTouchInCarWidths"/>).</summary>
    public float RibbonTouchM => Car.WidthM * Road.RibbonTouchInCarWidths;

    /// <summary>Half the carriageway is one direction's, and a lane's own line is the middle of that.</summary>
    public float LaneOffsetM => LaneWidthM * 0.5f;

    public float IntersectionCornerRadiusM => Car.WidthM * Road.IntersectionCornerRadiusInCarWidths;

    /// <summary>The sharpest corner a junction turns (GEN-13).</summary>
    public float ArmsApartMinRad => CityGen.ArmsApartMinDeg * MathF.PI / 180f;

    /// <summary>How far off the arms already there a road between districts is planned (GEN-13, GEN-69).</summary>
    public float LinkArmsApartMinRad => (CityGen.ArmsApartMinDeg + CityGen.LinkArmsMarginDeg) * MathF.PI / 180f;

    /// <summary>The angle a slip road leaves and joins an intercity road at (GEN-68).</summary>
    public float SlipLeavesRad => CityGen.SlipLeavesDeg * MathF.PI / 180f;

    /// <summary>
    /// <b>How near a slip road's arm lies to the intercity road's, or to another slip's, at the node they share</b> (GEN-13):
    /// its own angle, less what laying it takes off (<see cref="CityGenFigures.LinkArmsMarginDeg"/>).
    /// </summary>
    public float SlipArmsApartMinRad => (CityGen.SlipLeavesDeg - CityGen.LinkArmsMarginDeg) * MathF.PI / 180f;

    /// <summary>
    /// <b>How far along the intercity road a slip road runs to reach its terminal's offset</b> (GEN-68), at the slip's own
    /// angle.
    /// </summary>
    public float SlipRunM => CityGen.InterchangeOffsetM / MathF.Tan(SlipLeavesRad);

    /// <summary>
    /// <b>The standoff of a node a slip road leaves an intercity road at</b> (GEN-46, GEN-68): far enough out along the slip's
    /// shallow arm, at the angle it leaves on, that its lane ends clear of the intercity road's carriageway, so no lane of
    /// either starts on the other.
    /// </summary>
    public float SlipStandoffM(float carriagewayM, float leavesRad) => (carriagewayM + LaneWidthM) * 0.5f / MathF.Sin(leavesRad);

    public float TributaryJoinLeastRad => CityGen.TributaryJoinLeastDeg * MathF.PI / 180f;

    public float TributaryJoinMostRad => CityGen.TributaryJoinMostDeg * MathF.PI / 180f;

    /// <summary>The radius the shore's outer edge is rounded to where a river flows into another: about the same centre as the bank's.</summary>
    public float ConfluenceShoreRadiusM => MathF.Max(0f, CityGen.ConfluenceRadiusM - CityGen.ShoreWidthM);

    /// <summary>
    /// The tightest circle a roundabout may be driven round (GEN-19): what its own design speed affords on
    /// tarmac. <b>Derived and never authored</b> — a roundabout quoted in metres is a figure nobody could
    /// check against the car that has to go round it.
    /// </summary>
    /// <remarks>
    /// It is a floor and rarely the answer: what usually sizes a ring is the ground its own nodes need
    /// between them (<c>CityGenFigures.LocalityM</c>), which at three or four arms is the wider of the two.
    /// </remarks>
    public float RoundaboutRadiusFloorM =>
        CarCorneringRadiusM(CityGen.RoundaboutDesignSpeedMps);

    /// <summary>
    /// <b>The tightest line a movement through a junction may be laid on</b>: what the junction's own design
    /// speed affords on tarmac. Derived and never authored, for the reason above — a turning circle quoted
    /// in metres is a figure nobody could check against the car that has to hold it.
    /// </summary>
    public float JunctionCorneringRadiusM =>
        CarCorneringRadiusM(CityGen.JunctionDesignSpeedMps);

    /// <summary>
    /// <b>The circle a turn through a junction is given room for</b>: the wider of the junction's cornering radius
    /// and the car's own tightest turn (<see cref="CarTurningRadiusM"/>). A line tighter than the car can hold is
    /// ridden wide of it, over the lane beside.
    /// </summary>
    public float JunctionTurnRoomM => MathF.Max(JunctionCorneringRadiusM, CarTurningRadiusM);

    /// <summary>
    /// <b>How much ground a junction takes</b>: the standoff its arms' lanes end at
    /// (<see cref="CityGenFigures.ConnectionStandoffM"/>). The disc follows the standoff and the arms follow
    /// the disc — stated the other way round, a standoff read off a disc sized by the arms that end at the
    /// standoff is a circle.
    /// </summary>
    public float JunctionRadiusM => CityGen.ConnectionStandoffM;

    /// <summary>
    /// <b>The same for a junction whose widest arm is a carriageway this wide</b>: stood out by however much
    /// further than a street of one lane each way that carriageway's kerb stands off its line, so a lane ends
    /// as far beyond the widest road crossing it as it does in a town of streets.
    /// </summary>
    public float JunctionRadiusAcrossM(float widestCarriagewayM) =>
        JunctionRadiusM + (MathF.Max(0f, widestCarriagewayM - RoadWidthM) * 0.5f);

    /// <summary>
    /// <b>How far a traced road whose carriageway is this wide may stand off its survey</b> for its pieces to be merged
    /// into fewer (GEN-57): <see cref="CityGenFigures.TracedLineToleranceM"/>, and
    /// <see cref="CityGenFigures.TracedLineToleranceShare"/> of the width further.
    /// </summary>
    public float TracedLineToleranceAcrossM(float carriagewayM) =>
        CityGen.TracedLineToleranceM + (CityGen.TracedLineToleranceShare * carriagewayM);

    /// <summary>
    /// <b>How many car parks a town of <paramref name="buildings"/> buildings cuts</b> (GEN-53): one for every
    /// <see cref="CityGenFigures.BuildingsPerCarPark"/> of them, the count being the map's and the share of it
    /// the engine's (GEN-6).
    /// </summary>
    /// <remarks>
    /// <b>What the ground cannot carry is what fitted</b> (GEN-8): this is what the town asks for, and
    /// <c>CarParks</c> lays as many of them as there are sites straight enough to take one.
    /// </remarks>
    public int CarParksFor(int buildings) => buildings / CityGen.BuildingsPerCarPark;

    /// <summary>
    /// <b>The tightest a road may bend where a car park stands on it</b> (GEN-53), as a curvature: the one
    /// that leaves the kerb under the end of the rank no further off the rank's own line than
    /// <see cref="CityGenFigures.CarParkOffLaneMaxM"/>.
    /// </summary>
    /// <remarks>
    /// <b>The offset of a tangent, turned round</b>: a straight held on the bearing a bend of curvature
    /// <c>k</c> had at the middle parts from that bend by <c>kL²/2</c> over a run of <c>L</c> — so the bound
    /// falls with the square of the run, and <b>the wider the car park the straighter the road it asks
    /// for</b>. The run is <see cref="CarParkRunM"/>, the rank's own reach to the far edge of its end bay.
    /// </remarks>
    public float CarParkCurvatureMax(int mostBays) =>
        2f * CityGen.CarParkOffLaneMaxM / (CarParkRunM(mostBays) * CarParkRunM(mostBays));

    /// <summary>
    /// <b>How far along the street the outermost bay of a rank of <paramref name="bays"/> stands from its
    /// middle</b> (GEN-53): they stand a lane apart and centred on it, so a rank reaches half its own span
    /// either way.
    /// </summary>
    public float CarParkRankReachM(int bays) => bays <= 1 ? 0f : (bays - 1) * 0.5f * LaneWidthM;

    /// <summary>And to the far edge of the outermost bay, which is how far along its street a rank's ground runs.</summary>
    public float CarParkRunM(int mostBays) => CarParkRankReachM(mostBays) + (LaneWidthM * 0.5f);

    /// <summary>
    /// <b>How much street a car park takes either side of its middle</b> (GEN-53, GEN-4d): its rank, and past
    /// either end of it the street a car manoeuvres over (<see cref="ParkingFrontageClearOfTheEndsM"/>). It is
    /// what a car park's site stands clear of the ends of the arc it is on by.
    /// </summary>
    public float CarParkFrontageM(int mostBays) => CarParkRunM(mostBays) + ParkingFrontageClearOfTheEndsM;

    /// <summary>
    /// <b>How near the end of its lane a car park has to stand to be where that lane turns round</b> (GEN-4l): a
    /// locality — the nearest a car park stands to any junction (GEN-16) — and the widest car park's own frontage.
    /// The router turns a leg at the end of a lane, so a bay turned in further back than this lands the car on the
    /// lane running back further along it than the route was planned for.
    /// </summary>
    public float TurnAtALotWithinM => CityGen.LocalityM + CarParkFrontageM(CityGen.BaysPerLotMost);

    /// <summary>
    /// <b>How far a bay's own ground runs back over the street's</b> (GEN-53): enough that the two are one
    /// piece of tarmac (TER-3c.8), and short of a touch (<see cref="RibbonTouchM"/>), so they are not ground
    /// the two share (TER-5c).
    /// </summary>
    public float CarParkKerbOverlapM => RibbonTouchM * 0.5f;

    /// <summary><b>How long a bay is</b> (GEN-53, <see cref="CityGenFigures.BayLengthM"/>).</summary>
    public float CarParkBayLengthM => CityGen.BayLengthM;

    /// <summary>
    /// <b>How far a bay's own ground reaches off the kerb</b>: the apron it stands back behind
    /// (<see cref="CityGenFigures.BaySetbackM"/>) and the space itself, which is the deepest
    /// <see cref="CarParkBayLengthM"/> of it.
    /// </summary>
    public float CarParkBayDepthM => CityGen.BaySetbackM + CarParkBayLengthM;

    /// <summary><b>How wide a traced car park's aisle is</b> (GEN-53a): one lane each way, at a traced lane's width.</summary>
    public float CarParkAisleWidthM => 2f * CityGen.TracedLaneWidthM;

    /// <summary>
    /// <b>How far apart a traced car park's aisles run</b> (GEN-53a): an aisle, a rank of bays either side of it, and the
    /// walk between the rank and the next aisle's (<see cref="CityGenFigures.CarParkIslandM"/>).
    /// </summary>
    public float CarParkAislePitchM => CarParkAisleWidthM + (2f * CarParkBayDepthM) + CityGen.CarParkIslandM;

    /// <summary>
    /// <b>How far back from where an aisle's lanes end its ranks begin</b> (GEN-53a): the depth of the zebra struck from the
    /// kerb end, so the zebra lands on the walk and not on a bay's apron.
    /// </summary>
    public float CarParkRankClearM => Road.CrossingDepthM;

    /// <summary>
    /// <b>How far a car park's entrance stands from any junction of the street it joins</b> (GEN-53a): further than two
    /// traced junctions are gathered into one (<see cref="CityGenFigures.TracedJunctionsMergedM"/>), by a junction's
    /// own disc.
    /// </summary>
    public float CarParkEntranceClearM => CityGen.TracedJunctionsMergedM + JunctionRadiusM;

    /// <summary>
    /// <b>The most a kerb may bend and still be parked along</b> (GEN-4n): the curvature at which a car's length laid as a
    /// chord stands off the kerb by <see cref="CityGenFigures.CarParkOffLaneMaxM"/> at its middle — the sagitta
    /// <c>kL²/8</c>, turned round. A car slid in alongside a kerb bending more stands off it, or over it.
    /// </summary>
    public float KerbCurvatureMax => 8f * CityGen.CarParkOffLaneMaxM / (Car.LengthM * Car.LengthM);

    public float ParkingSpaceLengthM => Car.LengthM + Car.WidthM * Road.ParkingSpaceMarginInCarWidths * 2f;

    /// <summary>
    /// <b>A bay is narrower than the lane its way is driven out of</b>
    /// (<see cref="RoadFigures.ParkingSpaceSideMarginInCarWidths"/>, <see cref="LaneWidthM"/>): the ground a
    /// way lays is the space it serves, and a way leaves along its lane, so a space wider than that lane
    /// lips past the kerb over the metres the two run together.
    /// </summary>
    public float ParkingSpaceWidthM => Car.WidthM * (1f + Road.ParkingSpaceSideMarginInCarWidths * 2f);

    /// <summary>How far along its street a way out of a bay runs past its turn (<see cref="RoadFigures.ParkingRunOutInCarLengths"/>).</summary>
    public float ParkingRunOutM => Car.LengthM * Road.ParkingRunOutInCarLengths;

    /// <summary>
    /// <b>How far clear of its road's own ends a car park's frontage has to stand</b>: the run-out a car
    /// leaving a bay takes along the street (<see cref="ParkingRunOutM"/>), and a stretch of street beyond that
    /// for the car to have been driving down before it turns in (<see cref="CarParkFrontageM"/>).
    /// </summary>
    public float ParkingFrontageClearOfTheEndsM =>
        ParkingRunOutM + (Car.LengthM * Road.ParkingFrontageClearInCarLengths);

    /// <summary>Half a pavement band plus the front gap plus a person: how close a door counts as reached.</summary>
    public float WayInTouchingReachM => PavementWidthM * 0.5f + Building.FrontGapM + PersonDiameterM;

    /// <summary>
    /// How far off its line a car is no longer on it: half a lane, which is the width of ground the lane
    /// it is meant to be in actually has to spare.
    /// </summary>
    public float CarOffPathM => LaneOffsetM;

    /// <summary>
    /// The lead every distance the speed profile measures is taken from — how stale what a driver reads can be by the
    /// time its pedal acts: a grant as old as a turn (<see cref="SimFigures.AgentDecisionIntervalS"/>), read off a world
    /// that has stepped on since (<see cref="SimFigures.WorldStepsAhead"/>). A car that planned from where it is arrives
    /// at each constraint that much late.
    /// </summary>
    public float CarReactionS => Sim.AgentDecisionIntervalS + (SimFigures.WorldStepsAhead * TickSeconds);

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

    /// <summary>The patience a drive leg is given up after, 30 s at the shipped figures — four full red phases.</summary>
    public float CarPatienceS => Signals.CycleS * Patience.BlockedRoadInLightCycles;

    public float CarBlockedWayPriceM => CityGen.BlockSpacingAlongMinM * Patience.BlockedWayPriceInBlockSpacings;

    public float CarBlockedWayLifeS => CarPatienceS * Patience.BlockedWayLifeInBlockedClocks;

    /// <summary>How near its standoff mark an ambulance has to have stopped before the casualty is got aboard (AMB-10).</summary>
    public float AmbulanceSceneReachM => Car.LengthM * Ambulance.SceneReachInCarLengths;

    /// <summary>And how far short of the casualty that mark stands, which the loading covers by a placement (AMB-10).</summary>
    public float AmbulanceStandoffM => Car.LengthM * Ambulance.StandoffInCarLengths;

    /// <summary>How far from its hospital an ambulance waits, which is the walk-worth distance said of a bay.</summary>
    public float AmbulanceHomeM => CityGen.BlockSpacingAlongMinM * Ambulance.HomeWithinBlockSpacings;

    /// <summary>How long a call runs before the casualty is written off as unreachable, 300 s at the shipped figures.</summary>
    public float AmbulanceGiveUpS => CarPatienceS * Ambulance.GiveUpInBlockedClocks;

    /// <summary>And how far from its own building a police car or an evacuator stands waiting (SRV-2).</summary>
    public float ServiceHomeM => CityGen.BlockSpacingAlongMinM * Service.HomeWithinBlockSpacings;

    /// <summary>How long one leg of a beat may run before the patrol is sent somewhere else (SRV-5).</summary>
    public float PatrolGiveUpS => CarPatienceS * Service.GiveUpInBlockedClocks;

    /// <summary>How long an officer who is out has to walk back to their seat before being put in it (SRV-11).</summary>
    public float ServiceRecallS => CarPatienceS * Service.RecallInBlockedClocks;

    /// <summary>How long a closure may stand before the lane is given back to the town (SRV-6).</summary>
    public float PoliceClosureLifeS => CarPatienceS * Service.ClosureInBlockedClocks;

    /// <summary>How near its hitching place an evacuator has to stop before the hitch is worked (EVA-5).</summary>
    public float EvacuatorSceneReachM => Car.LengthM * Evacuator.SceneReachInCarLengths;

    /// <summary>And how near a yard slot it has to have got before the wreck can be set down in it (EVA-6).</summary>
    public float EvacuatorYardReachM => Car.LengthM * Evacuator.YardReachInCarLengths;

    /// <summary>How long one leg of a recovery may run before it is written off (EVA-8).</summary>
    public float EvacuatorGiveUpS => CarPatienceS * Evacuator.GiveUpInBlockedClocks;

    /// <summary>How near an ordered place a car has to have stopped before that order is finished (CTL-8a).</summary>
    public float OrderedPlaceReachM => Car.LengthM * Control.PlaceReachInCarLengths;

    /// <summary>How far back along the road an ordered car is aimed at the one it is following (CTL-8c).</summary>
    public float OrderedFollowGapM => Car.LengthM * Control.FollowGapInCarLengths;

    /// <summary>And how far that one moves before the route after it is drawn again (CTL-8c).</summary>
    public float OrderedFollowRedrawM => Car.LengthM * Control.FollowRedrawInCarLengths;

    /// <summary>How early a pair is given a manifold. See <see cref="SolverFigures.AllowedPenetrationM"/>.</summary>
    public float SolverSpeculativeM => Solver.AllowedPenetrationM * 4f;

    /// <summary>
    /// How wide the blur at a cloud's shadow's edge is (OBJ-8): the ground there sees part of the sun past the cloud's
    /// edge, which from <see cref="CloudFigures.HeightM"/> up is that height across the sun's width.
    /// </summary>
    public float CloudPenumbraM => Clouds.HeightM * Trig.Tan(Clouds.SunAcrossDeg * MathF.PI / 180f);

    /// <summary>How far the water's two shorter trains of waves run off the prevailing wind (<see cref="ViewFigures.WaterWaveLongM"/>).</summary>
    public float WaterWaveMidOffWindRad => View.WaterWaveMidOffWindDeg * MathF.PI / 180f;

    public float WaterWaveShortOffWindRad => View.WaterWaveShortOffWindDeg * MathF.PI / 180f;

    /// <summary>How far the waterline's swash moves the water's fade onto the shore, as a share of the fade's cover.</summary>
    public float WaterWaveSwashShare => View.WaterWaveSwashM / View.ShoreFadeM;

    /// <summary>How many steps in a row a body has to end at rest before it freezes (SOL-37) — never fewer than one.</summary>
    public int SolverRestTicks => Math.Max(1, (int)MathF.Ceiling(Solver.RestS * Sim.TickRateHz));
}
