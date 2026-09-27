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
    public float TickSeconds => 1f / Sim.TickRateHz;

    /// <summary>+1 where traffic keeps right, which with <c>+y</c> down is the way curvature counts positive.</summary>
    public float RoadSideSign => Road.TrafficKeepsRight ? 1f : -1f;

    /// <summary>
    /// The nominal car's wheels stand at the corners of its own footprint, so its track is its width; the
    /// wheelbase is shorter than the body it sits under.
    /// </summary>
    public float CarTrackM => Car.WidthM;

    /// <summary>≈ 3.9 m: the nominal car's own circle, which <see cref="CarParkingTemplateRadiusM"/> is opened from.</summary>
    public float CarTurningRadiusM => Car.WheelbaseM / MathF.Tan(Car.MaxSteeringDeg * MathF.PI / 180f);

    /// <summary>How far the middle of the body stands ahead of the rear axle the line is driven for.</summary>
    public float CarCentreAheadOfAxleM => Car.WheelbaseM * 0.5f;

    /// <summary>
    /// The nominal car's circle opened by <see cref="CarFigures.ParkingTemplateArcMargin"/>, so a turn laid on
    /// it is one a car holds rather than one it is exactly at the limit of. <see cref="CarParkTurnRadiusM"/>
    /// is taken in from it, and nothing else reads it.
    /// </summary>
    public float CarParkingTemplateRadiusM => CarTurningRadiusM * Car.ParkingTemplateArcMargin;

    /// <summary>
    /// <b>The circle a bay is turned into on off the street</b> (GEN-53): the car's own parking circle, taken
    /// in by <see cref="CityGenFigures.BayTurnInParkingCircles"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A car turning into a bay is at a walking pace or stopped</b>, so what decides the line is the hook
    /// it makes rather than a design speed — a junction's cornering radius
    /// (<see cref="JunctionCorneringRadiusM"/>) is answering a question nobody asked here, and would lay the
    /// turn three times as wide.
    /// </para>
    /// <para>
    /// <b>It is tighter than the circle the car's own steering describes</b>, which is the whole of what the
    /// share is for and is stated where the share is authored. Everything about how much ground a car park's
    /// junction takes is read off this one length — <see cref="CarParkStandoffM"/> along the street and
    /// <see cref="CarParkBayLeadM"/> off it — so a turn laid tighter is a junction that much smaller and
    /// bays that stay exactly where the arm's own reach put them.
    /// </para>
    /// </remarks>
    public float CarParkTurnRadiusM => CarParkingTemplateRadiusM * CityGen.BayTurnInParkingCircles;

    /// <summary>How much straight a bay's way ends on, so the car it is laid for parks square in the space.</summary>
    public float CarParkingStraightensUpM => Car.LengthM * Road.ParkingStraightensUpInCarLengths;

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
    /// <b>The widest a line has to be drawn for a car to hold this speed round it</b> — the corner formula
    /// the speed profile reads, turned round. A line laid tighter is not refused; it is driven slower,
    /// because the profile's corner term reads the arcs of every line exactly as it reads the arcs of a road.
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
    /// <b>The band has to sit above <see cref="PersonWalkSpeedMps"/>, and it is the grip that puts
    /// it there</b> — half again over walking pace at the shipped figures. Nothing about the closing speed
    /// says who was carrying it (PER-23), so a band below the town's own pace is one a walker meets by
    /// arriving at a parked car.
    /// </para>
    /// </remarks>
    public float PersonCasualtyKj => Person.MassKg * PersonSlidingGripMps2 * Damage.SlideToCasualtyM / 1000f;

    /// <summary>How far from a place the bays a car may be given there are looked for: a spawn's, and a leg's retarget.</summary>
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
    /// (TER-3c.3). It is what something laid near a kerb is asked to stand clear of (GEN-6a).
    /// </summary>
    public float PavementCornerReachM => PavementWidthM * 0.5f;

    /// <summary>
    /// The clear ground between one walker's claimed stretch and the next one's, which is what a queue on
    /// a pavement stands at — half a metre at the shipped figures.
    /// </summary>
    public float PersonStandstillGapM => PersonDiameterM * Person.StandstillGapInDiameters;

    /// <summary>
    /// How far along the way in front of it a walker looks — a metre at the shipped figures. It is what the
    /// follower is aimed at (PER-25), the ground ahead being the way's own line rather than a straight
    /// anybody laid, and the window its place on that way is searched in.
    /// </summary>
    public float PersonWalkAheadM => PersonDiameterM * Person.WalkAheadInDiameters;


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
    /// <b>How far off the kerb the roadside perimeter stands</b> — half a lane, 1.8 m at the shipped car.
    /// <b>Nothing reads it</b>: the ground's layers (<c>CityGen.GroundLayer</c>) are the carriageway and the
    /// walk, and no roadside line is struck off the boundary.
    /// </summary>
    /// <remarks>
    /// <b>Half a lane and not half a walk</b>, which is the point of naming it: it is quoted against the
    /// carriageway the boundary is the edge of (GEN-15) rather than against the pavement, so it means the
    /// same thing on a street whose pavement is the map's own figure as on one whose pavement is the town's.
    /// </remarks>
    public float RoadsidePerimeterOutM => LaneWidthM * 0.5f;

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
    /// <b>How far out along an arm the corner between it and its neighbour stands</b>: where the two kerbs
    /// bounding the wedge cross, as a distance along the arm's own line, so a narrow one-way street meeting
    /// a full carriageway makes a corner that stands further out along the narrow arm than along the wide
    /// one.
    /// </summary>
    /// <remarks>
    /// <b>Each arm is given the distance to its own kerb and not its own half</b> (TER-4d): a road standing
    /// off the node — the half of a carriageway a one-way street is driven on — has one kerb that much
    /// nearer its neighbour and the other that much further, and the crossing of two lines is the same
    /// arithmetic either way. Measured along the arm from the node, which the road's own line stands square
    /// off, so a distance along it is a distance along the road.
    /// </remarks>
    public static float JunctionCornerAlongM(float armsApartRad, float kerbM, float neighbourKerbM) =>
        ((kerbM * MathF.Cos(armsApartRad)) + neighbourKerbM) / MathF.Sin(armsApartRad);

    /// <summary>
    /// <b>Whether two arms turn a corner at all, and how far out their kerbs cross.</b> Two arms a straight
    /// line or more apart never turn one — their kerbs run away from each other. Two that stand all but
    /// straight through do not either: their kerbs cross so far off the node that the junction never reaches
    /// it (<see cref="JunctionArmReachMaxM"/>), which is a carriageway running through and, where the two
    /// are different widths (TER-4d), a step in the kerb rather than a corner. And a crossing no further out
    /// than a line is wide leaves a spike nothing can see and no cell can hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The spike is measured off the nearer of the two kerbs</b>, which is the mouth the wedge is a spike
    /// out of: measured off the further one, a street half a road wide meeting a full carriageway obliquely
    /// reads as no corner at all, and the pavement that ought to stop at its kerb runs on over the
    /// carriageway instead.
    /// </para>
    /// <para>
    /// <b>And the crossing has to stand out along <em>both</em> arms.</b> Behind an arm the two kerbs have
    /// not met yet — they are still running apart — so what crosses there is the two <em>lines</em> and not
    /// the two kerbs, and the arm has no tarmac to be tangent to. It happens where a kerb passes through the
    /// node: a one-way street stands its own half off (TER-4d), so the kerb on the half it gave up is the
    /// node's own line, and against a neighbour more than a right angle away the crossing of that line falls
    /// on the far side of the junction. What a fillet laid there is is a lens of carriageway hanging half a
    /// metre off the kerb in the middle of a street, with the pavement wrapping it.
    /// </para>
    /// </remarks>
    public bool JunctionTurnsACorner(float armsApartRad, float kerbM, float neighbourKerbM)
    {
        if (armsApartRad >= MathF.PI) return false;

        var alongM = JunctionCornerAlongM(armsApartRad, kerbM, neighbourKerbM);
        if (alongM < 0f || JunctionCornerAlongM(armsApartRad, neighbourKerbM, kerbM) < 0f) return false;

        var offM = MathF.Sqrt((alongM * alongM) + (kerbM * kerbM));
        return offM <= JunctionArmReachMaxM
               && offM - MathF.Min(kerbM, neighbourKerbM) >= Road.PaintLineWidthM;
    }

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
    public float JunctionArmReachM(float armsApartRad, float kerbM, float neighbourKerbM) =>
        JunctionCornerAlongM(armsApartRad, kerbM, neighbourKerbM)
        + (JunctionFilletRadiusM(armsApartRad) / MathF.Tan(armsApartRad * 0.5f));

    /// <summary>The same where both arms are a whole carriageway, which is every arm of a road driven both ways.</summary>
    public float JunctionArmReachM(float armsApartRad) =>
        JunctionArmReachM(armsApartRad, RoadWidthM * 0.5f, RoadWidthM * 0.5f);

    /// <summary>The furthest that ever is: the reach at the sharpest corner a junction may turn (GEN-13).</summary>
    public float JunctionArmReachMaxM => JunctionArmReachM(ArmsApartMinRad);

    /// <summary>
    /// <b>How much road the paint on one arm would take</b>, measured back from the line that arm's lanes
    /// hand over to the junction on (TER-5d): the margin in front of the crossing, its band, the clear road
    /// behind it and the bar itself. <b>Nothing reads it</b>: the paint an arm carries is measured off the
    /// band actually laid there (<c>CentrelineRuns.PaintedM</c>).
    /// </summary>
    public float ArmPaintM =>
        Road.CrossingSetbackM + Road.CrossingDepthM + Road.StopBarSetbackM + Road.StopBarThicknessM;

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
        CarCorneringRadiusM(CityGen.RoundaboutDesignSpeedMps, Terrain.PavedCoefficient);

    /// <summary>
    /// <b>The tightest line a movement through a junction may be laid on</b>: what the junction's own design
    /// speed affords on tarmac. Derived and never authored, for the reason above — a turning circle quoted
    /// in metres is a figure nobody could check against the car that has to hold it.
    /// </summary>
    public float JunctionCorneringRadiusM =>
        CarCorneringRadiusM(CityGen.JunctionDesignSpeedMps, Terrain.PavedCoefficient);

    /// <summary>
    /// <b>How much ground a junction takes</b>: the standoff its arms' lanes end at
    /// (<see cref="CityGenFigures.ConnectionStandoffM"/>). The disc follows the standoff and the arms follow
    /// the disc — stated the other way round, a standoff read off a disc sized by the arms that end at the
    /// standoff is a circle.
    /// </summary>
    public float JunctionRadiusM => CityGen.ConnectionStandoffM;

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

    /// <summary>How many of a town's buildings are hospitals (AMB-1).</summary>
    public int HospitalsFor(int buildings) =>
        ServicesFor(buildings, Ambulance.HospitalsPerBuilding, Ambulance.MostHospitals);

    /// <summary>How many are police stations (SRV-1).</summary>
    public int PoliceStationsFor(int buildings) =>
        ServicesFor(buildings, Service.StationsPerBuilding, Service.MostStations);

    /// <summary>How many are depots (SRV-1).</summary>
    public int DepotsFor(int buildings) => ServicesFor(buildings, Service.DepotsPerBuilding, Service.MostDepots);

    /// <summary>
    /// <b>How many buildings of one service use a town of this many buildings has</b> (AMB-1, SRV-1): a
    /// share of the count the map plans, capped, and never none where there is a building to be one.
    /// </summary>
    /// <remarks>
    /// <b>Struck here because two readers want it and neither may hold a second copy</b>: the generator cuts
    /// a yard for every one of them before it stands a building (GEN-55), and the fleets are laid off the
    /// finished plan (<c>World.Statics.BuildingRoster.CountIn</c>) — a vehicle a bay, nobody aboard. The two
    /// disagreeing is an ambulance with no hospital to go home to.
    /// </remarks>
    public static int ServicesFor(int buildings, float perBuilding, int most)
    {
        if (buildings <= 0) return 0;

        var wanted = (int)MathF.Round(buildings * perBuilding);
        return Math.Clamp(wanted, 1, Math.Min(most, buildings));
    }

    /// <summary>
    /// <b>The tightest a road may bend where a car park is cut into it</b> (GEN-53), as a curvature: the one
    /// that leaves the longest movement in the car park no further off its lane than
    /// <see cref="CityGenFigures.CarParkOffLaneMaxM"/> at the middle of its run.
    /// </summary>
    /// <remarks>
    /// <b>The offset of a tangent, turned round</b>: a straight held on the bearing a bend of curvature
    /// <c>k</c> had at the lane end parts from that bend by <c>kL²/2</c> over a run of <c>L</c> — so the
    /// bound falls with the square of the run, and <b>the wider the car park the straighter the road it asks
    /// for</b>. The run is <see cref="CarParkRunM"/>, the whole of the box and the reach of the rank again.
    /// </remarks>
    public float CarParkCurvatureMax(int mostBays) =>
        2f * CityGen.CarParkOffLaneMaxM / (CarParkRunM(mostBays) * CarParkRunM(mostBays));

    /// <summary>
    /// <b>How far along the street the outermost bay of a rank of <paramref name="bays"/> stands from the
    /// node</b> (GEN-53): they stand a lane apart and centred on it, so a rank reaches half its own span
    /// either way.
    /// </summary>
    public float CarParkRankReachM(int bays) => bays <= 1 ? 0f : (bays - 1) * 0.5f * LaneWidthM;

    /// <summary>
    /// <b>The box a car park's junction takes along the street</b>, either side of the node: the rank's own
    /// reach and the turn at the end of it (<see cref="CarParkTurnRadiusM"/>, a quarter turn standing off its
    /// corner by its own radius). <b>What the standoff is on a street that does not bend</b>, and what
    /// everything the bend costs is measured against.
    /// </summary>
    public float CarParkBoxM(int mostBays) => CarParkRankReachM(mostBays) + CarParkTurnRadiusM;

    /// <summary>
    /// <b>The longest a movement in a car park runs straight</b>: from the lane end at the far edge of the
    /// box to the bay at the far end of the rank, which is the box and the reach again.
    /// </summary>
    public float CarParkRunM(int mostBays) => CarParkBoxM(mostBays) + CarParkRankReachM(mostBays);

    /// <summary>
    /// <b>How far back of the node a car park's own street stands off</b> (GEN-53), which is <b>the nearest
    /// the road can be parted</b>: the place the turn into the furthest bay of the rank leaves the street.
    /// The box (<see cref="CarParkBoxM"/>) and what the bend adds to the turn's own tangent — but never less
    /// junction than any other junction is (<see cref="JunctionRadiusM"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Parting it any nearer costs a bay its way, and any further is junction nobody drives.</b> A bay
    /// whose way begins <em>behind</em> the lane end a car arrives on is one that car can only loop back
    /// into — a turn tighter than <see cref="JunctionCorneringRadiusM"/> and so no movement at all; a bay
    /// only a little ahead of it is one the car has to start turning for before it has arrived, drifting
    /// across the mouths of the bays before its own (<see cref="Spline.StraightArcStraightInto"/>). At
    /// exactly this much the furthest bay's turn begins where the street ends and every nearer bay has
    /// street in hand.
    /// </para>
    /// <para>
    /// <b>What the bend costs the street is the swing, twice</b> (<see cref="CarParkBearingSwingRad"/>). The
    /// box is exact on a road that does not bend. On one that does, a street that has turned by the time it
    /// reaches the lane end meets the bay at more than a square corner — and a corner turned through more
    /// than a right angle stands its tangent off by more than its radius, by the radius times that swing;
    /// and the lane end itself, being half a carriageway off the line the box was measured down, slides
    /// along the street by its own offset times the same. Everything else the bend does is across the street
    /// rather than along it, and is the bay's end of the turn to pay for
    /// (<see cref="CarParkBayLeadM"/>).
    /// </para>
    /// <para>
    /// <b>The floor is the standoff every other junction has</b> (<see cref="JunctionRadiusM"/>). A car park
    /// of one bay a side turns on a circle smaller than that, and the arithmetic would part the road nearer
    /// the node than any junction's arms end — which is not a shorter car park but a junction whose lane ends
    /// stand inside the ground its own roads share.
    /// </para>
    /// </remarks>
    public float CarParkStandoffM(int mostBays) =>
        MathF.Max(
            CarParkBoxM(mostBays)
            + ((CarParkTurnRadiusM + LaneOffsetM) * CarParkBearingSwingRad(mostBays)),
            JunctionRadiusM);

    /// <summary>
    /// <b>How far off the line its node stands on a bay's own way begins</b> (GEN-53) — <b>the standoff at
    /// the bay's end of the turn</b>, as <see cref="CarParkStandoffM"/> is the standoff at the street's end
    /// of it: <paramref name="laneTowardM"/>, the radius that turn is made at
    /// (<see cref="CarParkTurnRadiusM"/>), and what the bend costs the pair of them. One turn, stood off at
    /// both ends by the tangent it spends.
    /// </summary>
    /// <param name="laneTowardM">
    /// <b>How far toward this bay's own side the lane it is turned off runs</b>, from the line the junction's
    /// node stands on. It is half a lane on a street of two ways, which carries one each side; on a street
    /// driven one way it is that half lane toward the side the traffic was moved onto (TER-4d) and <b>the
    /// same half lane the other way on the side it was moved off</b> — so both ranks stand the same clearance
    /// from the one carriageway there is, rather than one of them a lane and a half further out.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>Nearer than this there is no turn into the bay at all</b>, only the biarc that joins any two poses
    /// — the car would have to begin coming round before the street had let it, which is the drift across
    /// the mouths of the other bays this rule exists to refuse.
    /// </para>
    /// <para>
    /// <b>The bend is paid twice here, and the whole of it is paid here</b>: the tangent lengthens by the
    /// radius times the swing (<see cref="CarParkBearingSwingRad"/>) as it does at the street's end of the
    /// turn, and the straight run parts from the lane it is following, which moves the bay across rather
    /// than along. <b>That second one is never more than the run is allowed to leave its lane by</b>
    /// (<see cref="CityGenFigures.CarParkOffLaneMaxM"/>) — the worst run is the longest one, and the longest
    /// one is what the bound was set from.
    /// </para>
    /// <para>
    /// <b>And no further off than this, which is why it is not the standoff every other road end keeps</b>
    /// (<see cref="CityGenFigures.ConnectionStandoffM"/>, GEN-46). Past the point the turn straightens out,
    /// what a movement lays is the bay's own line — the same ground, the same bearing, driven twice over: a
    /// metre of it is a metre of bay drawn as a junction. A bay's way begins where its turn ends.
    /// </para>
    /// </remarks>
    public float CarParkBayLeadM(int mostBays, float laneTowardM) =>
        laneTowardM + CityGen.CarParkOffLaneMaxM
        + (CarParkTurnRadiusM * (1f + CarParkBearingSwingRad(mostBays)));

    /// <summary>
    /// <b>How far a car park's street may swing from one end of what it stands on to the other</b>: the
    /// tightest bend it may carry (<see cref="CarParkCurvatureMax"/>) held for the whole run
    /// (<see cref="CarParkRunM"/>), which is the far lane end to the far bay of the far rank.
    /// </summary>
    /// <remarks>
    /// <b>The one angle everything the bend costs is priced in</b>, and the whole car park's rather than any
    /// one part of it: the rank is laid off the tangent at the node, cars arrive at both edges of the box,
    /// and the bays reach past it either way — so what each of those is out by is some part of this, and
    /// none of them is out by more.
    /// </remarks>
    public float CarParkBearingSwingRad(int mostBays) =>
        CarParkCurvatureMax(mostBays) * CarParkRunM(mostBays);

    /// <summary><b>How long a bay is</b> (GEN-53, <see cref="CityGenFigures.BayLengthM"/>).</summary>
    /// <remarks>
    /// <b>What it has to clear is the longest vehicle the town draws and not the nominal car</b>
    /// (<see cref="CarFigures.LongestLengthM"/>): every bay is one anything in the town can stand in, so
    /// the one that sizes them is the one nothing else is longer than. <b>And it is a length driven and not
    /// a length manoeuvred</b> — a bay square to the street is entered off its own turn and left the same
    /// way, where the parallel bay on a kerb has to be reversed into (<see cref="ParkingSpaceLengthM"/>).
    /// </remarks>
    public float CarParkBayLengthM => CityGen.BayLengthM;

    /// <summary>
    /// <b>How far the stand line at the far end of a car park's arm is from the line the node stands on</b>
    /// (GEN-53): where the bay's way begins (<see cref="CarParkBayLeadM"/>) and one bay from there.
    /// </summary>
    /// <inheritdoc cref="CarParkBayLeadM" path="/param[@name='laneTowardM']"/>
    /// <remarks>
    /// <b>So the arm is a bay and a turn into it and nothing else.</b> What stands the rank off the kerb is
    /// what the turn spends getting there rather than a setback chosen for it — the ground between the
    /// carriageway's edge and the first bay is whatever the lead leaves, which is a little over a metre
    /// wherever the lane it turns off runs.
    /// </remarks>
    public float CarParkArmStandM(int mostBays, float laneTowardM) =>
        CarParkBayLeadM(mostBays, laneTowardM) + CarParkBayLengthM;

    public float ParkingSpaceLengthM => Car.LengthM + Car.WidthM * Road.ParkingSpaceMarginInCarWidths * 2f;

    /// <summary>
    /// <b>A bay is narrower than the lane its way is driven out of</b>
    /// (<see cref="RoadFigures.ParkingSpaceSideMarginInCarWidths"/>, <see cref="LaneWidthM"/>): the ground a
    /// way lays is the space it serves, and a way leaves along its lane, so a space wider than that lane
    /// lips past the kerb over the metres the two run together.
    /// </summary>
    public float ParkingSpaceWidthM => Car.WidthM * (1f + Road.ParkingSpaceSideMarginInCarWidths * 2f);

    /// <summary>How far before a bay a way in leaves its lane. Read by <see cref="ParkingFrontageClearOfTheEndsM"/> alone.</summary>
    public float ParkingStagedInM => Car.LengthM * Road.ParkingStagedInCarLengths;

    /// <summary>And how much straight it ends on, which is what puts the car in the bay square.</summary>
    public float ParkingStraightensUpM => Car.LengthM * Road.ParkingStraightensUpInCarLengths;

    /// <summary>
    /// <b>How far clear of its road's own ends a car park's frontage has to stand</b>: the run-in every
    /// bay's way in is staged over (<see cref="ParkingStagedInM"/>), and a stretch of street beyond that for
    /// the car to have been driving down before it turns in. <b>Nothing reads it</b>: no bay's way is laid
    /// (GEN-4f).
    /// </summary>
    public float ParkingFrontageClearOfTheEndsM =>
        ParkingStagedInM + (Car.LengthM * Road.ParkingFrontageClearInCarLengths);

    /// <summary>Half a pavement band plus the front gap plus a person: how close a door counts as reached.</summary>
    public float WayInTouchingReachM => PavementWidthM * 0.5f + Building.FrontGapM + PersonDiameterM;

    public float CarJunctionClaimM => Driving.NominalCarLengthM * Driving.JunctionClaimInCarLengths;

    /// <summary>
    /// <b>The ground a car keeps around itself</b> — asked for in front of its nose as part of its own
    /// stretch, and claimed behind its tail at <see cref="CarTailMarginM"/> (TER-4c.1), so that
    /// <b>what a queue at rest stands at and what a body in a junction is still swinging through are one
    /// figure and one stretch</b>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It is a margin on a lossy reading before it is a comfort.</b> A claim puts a body on a way as one
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
    /// <b>The part of that ground a claim keeps behind the tail</b>
    /// (<see cref="DrivingFigures.TailMarginShare"/>) — where a body's stretch begins, on every way it is on,
    /// and therefore where whoever comes up behind it is cut.
    /// </summary>
    /// <remarks>
    /// The end that swings widest is also the end that queues the road behind it, and the two ends are read
    /// by different traffic: in front the margin is this car's own cover against a bar or a body it is
    /// closing on, behind it is what the claim owes the width it threw away. Only the tail is short of
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
    /// brakes with. A line laid to the pedal's stopping distance is two and a half times too short.
    /// <b>The nominal car's, and nothing reads it</b>: what a car's line is laid out to is its own
    /// (<see cref="Agents.Car.Body.CarBuild.SightM"/>).
    /// </summary>
    public float CarSightM =>
        Car.MaxSpeedMps * Car.MaxSpeedMps
        / (2f * MathF.Min(CarBrakingMps2, TyreGripMps2) * Driving.GripMargin);

    public float CarCrossingStandOffM => Car.WidthM * Driving.CrossingStandOffInCarWidths;

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

    /// <summary>How long a call runs before the casualty is written off as unreachable, 120 s at the shipped figures.</summary>
    public float AmbulanceGiveUpS => CarPatienceS * Ambulance.GiveUpInBlockedClocks;

    /// <summary>And how far from its own building a police car or an evacuator stands waiting (SRV-2).</summary>
    public float ServiceHomeM => CityGen.BlockSpacingAlongMinM * Service.HomeWithinBlockSpacings;

    /// <summary>How long one leg of a beat may run before the patrol is sent somewhere else (SRV-5).</summary>
    public float PatrolGiveUpS => CarPatienceS * Service.GiveUpInBlockedClocks;

    /// <summary>
    /// How long a hand who is out would have to walk back to their seat before being put in it. <b>Nothing
    /// reads it</b>: no service vehicle carries a crew (SRV-3).
    /// </summary>
    public float ServiceRecallS => CarPatienceS * Service.RecallInBlockedClocks;

    /// <summary>How much road a police car holds either side of the scene it is closing (SRV-6).</summary>
    public float PoliceClosureM => Car.LengthM * Service.ClosureInCarLengths;

    /// <summary>And how far short of that scene the car itself is parked (SRV-6).</summary>
    public float PoliceStandoffM => Car.LengthM * Service.SceneStandoffInCarLengths;

    /// <summary>How long a closure may stand before the lane is given back to the town (SRV-6).</summary>
    public float PoliceClosureLifeS => CarPatienceS * Service.ClosureInBlockedClocks;

    /// <summary>How near its hitching place an evacuator has to stop before the hitch is worked (EVA-5).</summary>
    public float EvacuatorSceneReachM => Car.LengthM * Evacuator.SceneReachInCarLengths;

    /// <summary>And how near a yard slot it has to have got before the wreck can be set down in it (EVA-6).</summary>
    public float EvacuatorYardReachM => Car.LengthM * Evacuator.YardReachInCarLengths;

    /// <summary>How long one leg of a recovery may run before it is written off (EVA-8).</summary>
    public float EvacuatorGiveUpS => CarPatienceS * Evacuator.GiveUpInBlockedClocks;

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
}
