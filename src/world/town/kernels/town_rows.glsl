// The tables the device's tick already kept before the town was resident, by id (app/devicetick/ResidentTown), and every
// fleet field one of their rows holds, by its C# name: cars_ProgressM(car) is CarFleet.ProgressM[car]. A field held here
// is held nowhere else. What no row held is the fleets' own headers' (town_cars.glsl, town_people.glsl).

#ifndef TOWN_ROWS_GLSL
#define TOWN_ROWS_GLSL

#include "world/town/kernels/town_root.glsl"
#include "agents/car/body/kernels/tyre_rows.glsl"
#include "agents/car/body/kernels/car_build.glsl"
#include "agents/car/control/kernels/car_line.glsl"
#include "agents/car/control/kernels/drive_context_row.glsl"
#include "world/town/kernels/car_tick_rows.glsl"
#include "world/town/kernels/box_ahead.glsl"
#include "world/town/kernels/walker_table.glsl"
#include "agents/person/actions/kernels/walker_rows.glsl"
#include "world/road/kernels/claims_rows.glsl"
#include "world/road/kernels/swept_rows.glsl"

// How many of each the tables have room for, and each row's stride where an agent has several (TownCounts).
struct TownCounts
{
    uint carCapacity;
    uint peopleCapacity;
    uint cars;
    uint people;
    uint arcsPerCar;
    uint mostLanes;
    uint wheels;
    uint routeLanesPerCar;
    uint routeWaysPerPerson;
    uint mostRunsOfAStraight;
    uint mostRunsOfAPass;
    uint firstCarAgent;
};

layout(buffer_reference, std430, buffer_reference_align = 4) buffer TownCountsRows { TownCounts r[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer CarTickRows { CarTickRow r[]; };
layout(buffer_reference, std430, buffer_reference_align = 8) buffer CarToldRows { CarToldRow r[]; };
layout(buffer_reference, std430, buffer_reference_align = 8) buffer CarGrantRows { CarGrantRow r[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer ClaimRows { ClaimRow r[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer BoxAheadRows { BoxAheadRow r[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer DriveContextRows { DriveContextRow r[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer CarBriefRows { CarBriefRow r[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer CarLineRows { CarLineRow r[]; };
layout(buffer_reference, std430, buffer_reference_align = 16) buffer ArcRows { vec4 r[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer OvertakeRows { int r[]; };
layout(buffer_reference, std430, buffer_reference_align = 8) buffer TyrePoseRows { TyrePose r[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer TyreCommandRows { TyreCommand r[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer TyreCarRows { TyreCarRow r[]; };
layout(buffer_reference, std430, buffer_reference_align = 8) buffer TyreWheelRows { TyreWheelRow r[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer CarBuildRows { CarBuildRow r[]; };
layout(buffer_reference, std430, buffer_reference_align = 8) buffer WalkerTableRows { WalkerTableRow r[]; };
layout(buffer_reference, std430, buffer_reference_align = 8) buffer WalkerRows { WalkerRow r[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer SweptRunRows { SweptRun r[]; };

// Root and counts.
const uint TownCountsId = 1u;

// The cars' row tables (64–127).
const uint CarsTicks = 64u;
const uint CarsTold = 65u;
const uint CarsGrants = 66u;
const uint CarsCutters = 67u;
const uint CarsHoldBits = 68u;
const uint CarsBoxes = 69u;
const uint CarsContexts = 70u;
const uint CarsBriefs = 71u;
const uint CarsLines = 72u;
const uint CarsArcs = 73u;
const uint CarsEntries = 74u;
const uint CarsChain = 75u;
const uint CarsStarts = 76u;
const uint CarsEnds = 77u;
const uint CarsFirstArcs = 78u;
const uint CarsBreaks = 79u;
const uint CarsPasses = 80u;
const uint CarsClocks = 81u;
const uint CarsPoses = 82u;
const uint CarsCommands = 83u;
const uint CarsTyreRows = 84u;
const uint CarsWheels = 85u;
const uint CarsWheelImpulses = 86u;
const uint CarsBuildOf = 87u;
const uint CarsBuilds = 88u;
const uint CarsBodyRows = 89u;

// The people's row tables (256–287).
const uint PeopleTable = 256u;
const uint PeopleImpulses = 257u;
const uint PeopleBodies = 258u;
const uint PeopleWalks = 259u;
const uint PeopleRoutes = 260u;
const uint PeopleStraights = 261u;
const uint PeoplePasses = 262u;
const uint PeopleClocks = 263u;

#define townCounts TOWN(TownCountsRows, TownCountsId).r[0]

// CarFleet's fields a car's tick row holds (CarTickRow).
#define cars_Reading(c) TOWN(CarTickRows, CarsTicks).r[c]
#define cars_ProgressM(c) TOWN(CarTickRows, CarsTicks).r[c].progressM
#define cars_CoveredSinceClaimM(c) TOWN(CarTickRows, CarsTicks).r[c].coveredSinceClaimM
#define cars_AlongMps(c) TOWN(CarTickRows, CarsTicks).r[c].alongMps
#define cars_OffLineM(c) TOWN(CarTickRows, CarsTicks).r[c].offLineM
#define cars_PlannedMps(c) TOWN(CarTickRows, CarsTicks).r[c].plannedMps
#define cars_Hold(c) TOWN(CarTickRows, CarsTicks).r[c].hold

// What a car's decisions tell its tick (CarToldRow): CarFleet.PaceMps is held here.
#define cars_Told(c) TOWN(CarToldRows, CarsTold).r[c]
#define cars_PaceMps(c) TOWN(CarToldRows, CarsTold).r[c].paceMps

// A car's grant (CarGrantRow), what cut it and its hold bits.
#define cars_Grant(c) TOWN(CarGrantRows, CarsGrants).r[c]
#define cars_AuthorityM(c) TOWN(CarGrantRows, CarsGrants).r[c].authorityM
#define cars_HorizonM(c) TOWN(CarGrantRows, CarsGrants).r[c].horizonM
#define cars_ClaimFromM(c) TOWN(CarGrantRows, CarsGrants).r[c].claimFromM
#define cars_CommittedToM(c) TOWN(CarGrantRows, CarsGrants).r[c].committedToM
#define cars_GrantMarginM(c) TOWN(CarGrantRows, CarsGrants).r[c].grantMarginM
#define cars_GrantCutBy(c) TOWN(CarGrantRows, CarsGrants).r[c].grantCutBy
#define cars_Cutter(c) TOWN(ClaimRows, CarsCutters).r[c]
#define cars_HoldBits(c) TOWN(Uints, CarsHoldBits).v[c]

// The junction ahead (BoxAheadRow): ToTheBoxM, LightAheadM, and BoxIsOurs, TurningAtTheBox, InsideTheBox and
// CommittedToTheBox as its flags.
#define cars_Box(c) TOWN(BoxAheadRows, CarsBoxes).r[c]
#define cars_ToTheBoxM(c) TOWN(BoxAheadRows, CarsBoxes).r[c].toTheBoxM
#define cars_LightAheadM(c) TOWN(BoxAheadRows, CarsBoxes).r[c].lightAheadM

// CarFleet.Context (DriveContextRow), and what the town read of it (CarBriefRow).
#define cars_Context(c) TOWN(DriveContextRows, CarsContexts).r[c]
#define cars_Brief(c) TOWN(CarBriefRows, CarsBriefs).r[c]

// A car's line: CarFleet.Line, LineArcs (two vec4s an arc: Arc.a, Arc.b), LineEntryM, LaneChain, LaneStartM, LaneEndM,
// LaneFirstArc and JoinBreaks, each row a car's townCounts.arcsPerCar or townCounts.mostLanes long.
#define cars_Line(c) TOWN(CarLineRows, CarsLines).r[c]
#define cars_LineArcA(c, i) TOWN(ArcRows, CarsArcs).r[2u * ((c) * townCounts.arcsPerCar + (i))]
#define cars_LineArcB(c, i) TOWN(ArcRows, CarsArcs).r[2u * ((c) * townCounts.arcsPerCar + (i)) + 1u]
#define cars_LineEntry(c, i) TOWN(Floats, CarsEntries).v[(c) * townCounts.arcsPerCar + (i)]
#define cars_Chain(c, i) TOWN(Ints, CarsChain).v[(c) * townCounts.mostLanes + (i)]
#define cars_LaneStartM(c, i) TOWN(Floats, CarsStarts).v[(c) * townCounts.mostLanes + (i)]
#define cars_LaneEndM(c, i) TOWN(Floats, CarsEnds).v[(c) * townCounts.mostLanes + (i)]
#define cars_LaneFirstArc(c, i) TOWN(Uints, CarsFirstArcs).v[(c) * townCounts.mostLanes + (i)]
#define cars_JoinBreaks(c, i) TOWN(Uints, CarsBreaks).v[(c) * townCounts.mostLanes + (i)]

// CarFleet.Pass (OvertakeRow, twelve words a car: lane, then its figures — read whole with carsPass).
#define cars_PassWord(c, w) TOWN(OvertakeRows, CarsPasses).r[12u * (c) + (w)]

// The leg's clock (LegProgress): how long it has got nowhere, and the closest it came.
#define cars_ClockSinceS(c) TOWN(Vec2s, CarsClocks).v[c].x
#define cars_ClockClosestM(c) TOWN(Vec2s, CarsClocks).v[c].y

// The pose the tyres read (TyrePose): PositionM, VelocityMps, its forward, AccelerationMps2, HeadingRad, YawRateRadPerS,
// MassKg.
#define cars_Pose(c) TOWN(TyrePoseRows, CarsPoses).r[c]
#define cars_PositionM(c) TOWN(TyrePoseRows, CarsPoses).r[c].positionM
#define cars_VelocityMps(c) TOWN(TyrePoseRows, CarsPoses).r[c].velocityMps
#define cars_AccelerationMps2(c) TOWN(TyrePoseRows, CarsPoses).r[c].accelerationMps2
#define cars_HeadingRad(c) TOWN(TyrePoseRows, CarsPoses).r[c].headingRad
#define cars_YawRateRadPerS(c) TOWN(TyrePoseRows, CarsPoses).r[c].yawRateRadPerS
#define cars_MassKg(c) TOWN(TyrePoseRows, CarsPoses).r[c].massKg

// CarFleet.Command (TyreCommand); SlipThrottle, DrivenSlipping, WheelsOnAWay and whether it is handed (TyreCarRow); each
// wheel's carried state (TyreWheelRow, townCounts.wheels a car) and the impulse it last put down.
#define cars_Command(c) TOWN(TyreCommandRows, CarsCommands).r[c]
#define cars_TyreRow(c) TOWN(TyreCarRows, CarsTyreRows).r[c]
#define cars_SlipThrottle(c) TOWN(TyreCarRows, CarsTyreRows).r[c].slipThrottle
#define cars_Wheel(c, w) TOWN(TyreWheelRows, CarsWheels).r[(c) * townCounts.wheels + (w)]
#define cars_WheelImpulse(c, w) TOWN(Vec4s, CarsWheelImpulses).v[(c) * townCounts.wheels + (w)]

// CarFleet.Variant, and the build it names (CarBuildRow, one a variant): CarFleet.BuildOf(car).
#define cars_Variant(c) TOWN(Uints, CarsBuildOf).v[c]
#define cars_Build(c) TOWN(CarBuildRows, CarsBuilds).r[TOWN(Uints, CarsBuildOf).v[c]]
#define buildOfVariant(v) TOWN(CarBuildRows, CarsBuilds).r[v]

// CarFleet.Body's row in the solver's tables, and whether it rested parked (bodies_phase.glsl's CarBodyRow: two words).
#define cars_Body(c) TOWN(Ints, CarsBodyRows).v[2u * (c)]
#define cars_BodyFlags(c) TOWN(Uints, CarsBodyRows).v[2u * (c) + 1u]

// PersonFleet's fields the walkers' tick row holds (WalkerTableRow): PositionM, VelocityMps, DeclaredMps, the aim it
// walks at, HeadingRad, MassKg, DistanceWalkedM.
#define people_Body(p) TOWN(Ints, PeopleBodies).v[p]
#define people_TickRow(p) TOWN(WalkerTableRows, PeopleTable).r[p]
#define people_PositionM(p) TOWN(WalkerTableRows, PeopleTable).r[p].positionM
#define people_VelocityMps(p) TOWN(WalkerTableRows, PeopleTable).r[p].velocityMps
#define people_DeclaredMps(p) TOWN(WalkerTableRows, PeopleTable).r[p].declaredMps
#define people_HeadingRad(p) TOWN(WalkerTableRows, PeopleTable).r[p].headingRad
#define people_MassKg(p) TOWN(WalkerTableRows, PeopleTable).r[p].massKg
#define people_DistanceWalkedM(p) TOWN(WalkerTableRows, PeopleTable).r[p].distanceWalkedM
#define people_ImpulseNs(p) TOWN(Vec2s, PeopleImpulses).v[p]

// PersonFleet's fields a walk row holds (WalkerRow, the claims' kept walkers): DestinationM, GoalM, RadiusM, OnWayM,
// OffWayM, RouteToM, GrantM, Pass, RouteCount, RouteTaken, OnWay, OnCrossing, Car, Action, Stage, and Walking, Wounded,
// Inside.Any and RouteRunsOut as its flags. The tick row's aim is DestinationM as a rebuild or a decision last set it.
#define people_Walk(p) TOWN(WalkerRows, PeopleWalks).r[p]
#define people_DestinationM(p) TOWN(WalkerRows, PeopleWalks).r[p].destinationM
#define people_GoalM(p) TOWN(WalkerRows, PeopleWalks).r[p].goalM
#define people_RadiusM(p) TOWN(WalkerRows, PeopleWalks).r[p].radiusM
#define people_OnWayM(p) TOWN(WalkerRows, PeopleWalks).r[p].onWayM
#define people_OffWayM(p) TOWN(WalkerRows, PeopleWalks).r[p].offWayM
#define people_RouteToM(p) TOWN(WalkerRows, PeopleWalks).r[p].routeToM
#define people_GrantM(p) TOWN(WalkerRows, PeopleWalks).r[p].grantM
#define people_RouteCount(p) TOWN(WalkerRows, PeopleWalks).r[p].routeCount
#define people_RouteTaken(p) TOWN(WalkerRows, PeopleWalks).r[p].routeTaken
#define people_OnWay(p) TOWN(WalkerRows, PeopleWalks).r[p].onWay
#define people_OnCrossing(p) TOWN(WalkerRows, PeopleWalks).r[p].onCrossing
#define people_Car(p) TOWN(WalkerRows, PeopleWalks).r[p].car
#define people_Action(p) TOWN(WalkerRows, PeopleWalks).r[p].action
#define people_Stage(p) TOWN(WalkerRows, PeopleWalks).r[p].stage
#define people_WalkFlags(p) TOWN(WalkerRows, PeopleWalks).r[p].flags

// PersonFleet.RouteWays, townCounts.routeWaysPerPerson a walker; the straight it committed to and the pass it is on,
// as swept runs.
#define people_RouteWay(p, i) TOWN(Ints, PeopleRoutes).v[(p) * townCounts.routeWaysPerPerson + (i)]
#define people_StraightRun(p, i) TOWN(SweptRunRows, PeopleStraights).r[(p) * townCounts.mostRunsOfAStraight + (i)]
#define people_PassRun(p, i) TOWN(SweptRunRows, PeoplePasses).r[(p) * townCounts.mostRunsOfAPass + (i)]

// A walker's leg's clock (walker_decide.glsl's WalkerClock: since, closest, the route slot it was run at, four words).
#define people_ClockSinceS(p) TOWN(Floats, PeopleClocks).v[4u * (p)]
#define people_ClockClosestM(p) TOWN(Floats, PeopleClocks).v[4u * (p) + 1u]
#define people_ClockRouteTakenAt(p) TOWN(Ints, PeopleClocks).v[4u * (p) + 2u]

#endif
