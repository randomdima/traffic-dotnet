// The town's errands, by id (512–639) and by the C# name of the field the town keeps them in: duty_Stage(car) is
// TownWorld._duty.Stage[car] (RescueDuty), beat_ the police's (PatrolDuty), recovery_ the evacuators' (RecoveryDuty),
// serviceBeat_, round_ (ParkedRound), carOrders_ (PlayerOrders), and town_ the town's own — the lanes closed, the aprons,
// what the interface handed it and the counts its errands skip their searches on. Laid once off the town
// (app/devicetick/TownState.Errands). The runs a route may not enter (TownWorld._closedLinks) are the routing module's.

#ifndef TOWN_ERRANDS_GLSL
#define TOWN_ERRANDS_GLSL

#include "world/town/kernels/town_tables.glsl"

// RescueStage, PatrolStage, RecoveryStage.
const uint RescueStageWaiting = 0u;
const uint RescueStagePatrolling = 1u;
const uint RescueStageRunning = 2u;
const uint RescueStageLoading = 3u;
const uint RescueStageCarrying = 4u;
const uint RescueStageHandingOver = 5u;
const uint RescueStageGoingHome = 6u;
const uint PatrolStageStanding = 0u;
const uint PatrolStagePatrolling = 1u;
const uint PatrolStageAttending = 2u;
const uint PatrolStageClosing = 3u;
const uint PatrolStageReopening = 4u;
const uint PatrolStageReturningToStation = 5u;
const uint RecoveryStageWaiting = 0u;
const uint RecoveryStagePatrolling = 1u;
const uint RecoveryStageRunning = 2u;
const uint RecoveryStageHitching = 3u;
const uint RecoveryStageHauling = 4u;
const uint RecoveryStageUnhitching = 5u;
const uint RecoveryStageGoingHome = 6u;

// PlayerOrder and SelectionKind.
const uint PlayerOrderNone = 0u;
const uint PlayerOrderDriveThere = 1u;
const uint PlayerOrderParkThere = 2u;
const uint PlayerOrderParkAndWalkThere = 3u;
const uint PlayerOrderFollowThatCar = 4u;
const uint SelectionKindNone = 0u;
const uint SelectionKindPerson = 1u;
const uint SelectionKindCar = 2u;

// The duties' nobody, no building, no bay and no yard; ServiceBeat.NoDistrict; PlayerOrders.NoCar.
const int RescueDutyNobody = -1;
const int PatrolDutyNobody = -1;
const int RecoveryDutyNothing = -1;
const int ServiceBeatNoDistrict = -1;
const int PlayerOrdersNoCar = -1;

// What a hand holds (HandInput).
struct HandInput
{
    uint held;
    float throttle;
    float steer;
    uint handbrake;
    vec2 walkDirection;
};

// One unit of the selection (Selection).
struct Selection
{
    uint kind;
    int index;
};

// The town's errand scalars: both hands and the walkers' orders the interface handed it, the selection's count, the
// counts its errands skip their searches on, whether the town parks, whether a closure changed, an apron entry's run
// and how many lanes one patrol closes (PatrolDuty.MostLanesClosed).
struct ErrandsState
{
    HandInput hands;
    HandInput otherHand;
    int otherCar;
    int ordered;
    int selectedCount;
    int woundedCount;
    int wreckCount;
    int mendingCount;
    int onTheBar;
    uint townParks;
    uint closuresChanged;
    int apronStride;
    int beatMostLanesClosed;
    int pad;
};

layout(buffer_reference, std430, buffer_reference_align = 4) buffer SelectionRows { Selection r[]; };
layout(buffer_reference, std430, buffer_reference_align = 8) buffer ErrandsStateRows { ErrandsState r[]; };

const uint DutyStageId = 512u;
const uint DutyCasualtyId = 513u;
const uint DutyHospitalId = 514u;
const uint DutyHomeBayId = 515u;
const uint DutySinceSId = 516u;
const uint DutyLoadedForSId = 517u;
const uint BeatStageId = 518u;
const uint BeatStationId = 519u;
const uint BeatHomeBayId = 520u;
const uint BeatSinceSId = 521u;
const uint BeatCasualtyId = 522u;
const uint BeatWreckId = 523u;
const uint BeatClosedForSId = 524u;
const uint BeatClosedLanesId = 525u;
const uint BeatClosedCountId = 526u;
const uint BeatSceneLaneId = 527u;
const uint BeatPostMId = 528u;
const uint BeatStandMId = 529u;
const uint BeatOfficerId = 530u;
const uint RecoveryStageId = 531u;
const uint RecoveryWreckId = 532u;
const uint RecoveryTowingId = 533u;
const uint RecoveryOnTheHookOfId = 534u;
const uint RecoveryHeldByTheTailId = 535u;
const uint RecoveryDepotId = 536u;
const uint RecoveryYardId = 537u;
const uint RecoveryHomeBayId = 538u;
const uint RecoveryHaulsLeftId = 539u;
const uint RecoveryInTheYardId = 540u;
const uint RecoverySinceSId = 541u;
const uint RecoveryHitchedForSId = 542u;
const uint RecoveryRepairedForSId = 543u;
const uint ServiceBeatDistrictId = 544u;
const uint ServiceBeatPatrolsId = 545u;
const uint ServiceBeatSetsOutAtSId = 546u;
const uint RoundStandSId = 547u;
const uint RoundStoodSId = 548u;
const uint CarOrdersManualId = 549u;
const uint CarOrdersKindId = 550u;
const uint CarOrdersPointMId = 551u;
const uint CarOrdersLeadId = 552u;
const uint CarOrdersAimedAtMId = 553u;
const uint TownClosedLanesId = 554u;
const uint TownApronBaysId = 555u;
const uint TownSelectedId = 556u;
const uint TownOrderedPeopleId = 557u;
const uint TownOrderedToMId = 558u;
const uint ErrandsStateId = 559u;

// RescueDuty: an ambulance's errand.
#define duty_Stage(c) TOWN(Uints, DutyStageId).v[c]
#define duty_Casualty(c) TOWN(Ints, DutyCasualtyId).v[c]
#define duty_Hospital(c) TOWN(Ints, DutyHospitalId).v[c]
#define duty_HomeBay(c) TOWN(Ints, DutyHomeBayId).v[c]
#define duty_SinceS(c) TOWN(Floats, DutySinceSId).v[c]
#define duty_LoadedForS(c) TOWN(Floats, DutyLoadedForSId).v[c]

// PatrolDuty: a police car's errand, the lanes it closes (beat_MostLanesClosed a car, the entrance first) and its officer.
#define beat_Stage(c) TOWN(Uints, BeatStageId).v[c]
#define beat_Station(c) TOWN(Ints, BeatStationId).v[c]
#define beat_HomeBay(c) TOWN(Ints, BeatHomeBayId).v[c]
#define beat_SinceS(c) TOWN(Floats, BeatSinceSId).v[c]
#define beat_Casualty(c) TOWN(Ints, BeatCasualtyId).v[c]
#define beat_Wreck(c) TOWN(Ints, BeatWreckId).v[c]
#define beat_ClosedForS(c) TOWN(Floats, BeatClosedForSId).v[c]
#define beat_ClosedLane(c, i) TOWN(Ints, BeatClosedLanesId).v[(c) * uint(beat_MostLanesClosed) + (i)]
#define beat_ClosedCount(c) TOWN(Ints, BeatClosedCountId).v[c]
#define beat_SceneLane(c) TOWN(Ints, BeatSceneLaneId).v[c]
#define beat_PostM(c) TOWN(Floats, BeatPostMId).v[c]
#define beat_StandM(c) TOWN(Floats, BeatStandMId).v[c]
#define beat_Officer(c) TOWN(Ints, BeatOfficerId).v[c]

// RecoveryDuty: an evacuator's errand, indexed by the evacuator — but OnTheHookOf, HeldByTheTail, InTheYard and
// RepairedForS, which are the wreck's.
#define recovery_Stage(c) TOWN(Uints, RecoveryStageId).v[c]
#define recovery_Wreck(c) TOWN(Ints, RecoveryWreckId).v[c]
#define recovery_Towing(c) TOWN(Ints, RecoveryTowingId).v[c]
#define recovery_OnTheHookOf(c) TOWN(Ints, RecoveryOnTheHookOfId).v[c]
#define recovery_HeldByTheTail(c) TOWN(Uints, RecoveryHeldByTheTailId).v[c]
#define recovery_Depot(c) TOWN(Ints, RecoveryDepotId).v[c]
#define recovery_Yard(c) TOWN(Ints, RecoveryYardId).v[c]
#define recovery_HomeBay(c) TOWN(Ints, RecoveryHomeBayId).v[c]
#define recovery_HaulsLeft(c) TOWN(Ints, RecoveryHaulsLeftId).v[c]
#define recovery_InTheYard(c) TOWN(Uints, RecoveryInTheYardId).v[c]
#define recovery_SinceS(c) TOWN(Floats, RecoverySinceSId).v[c]
#define recovery_HitchedForS(c) TOWN(Floats, RecoveryHitchedForSId).v[c]
#define recovery_RepairedForS(c) TOWN(Floats, RecoveryRepairedForSId).v[c]

#define serviceBeat_District(c) TOWN(Ints, ServiceBeatDistrictId).v[c]
#define serviceBeat_Patrols(c) TOWN(Uints, ServiceBeatPatrolsId).v[c]
#define serviceBeat_SetsOutAtS(c) TOWN(Floats, ServiceBeatSetsOutAtSId).v[c]

#define round_StandS(c) TOWN(Floats, RoundStandSId).v[c]
#define round_StoodS(c) TOWN(Floats, RoundStoodSId).v[c]

#define carOrders_Manual(c) TOWN(Uints, CarOrdersManualId).v[c]
#define carOrders_Kind(c) TOWN(Uints, CarOrdersKindId).v[c]
#define carOrders_PointM(c) TOWN(Vec2s, CarOrdersPointMId).v[c]
#define carOrders_Lead(c) TOWN(Ints, CarOrdersLeadId).v[c]
#define carOrders_AimedAtM(c) TOWN(Vec2s, CarOrdersAimedAtMId).v[c]

// TownWorld._closedLanes, one flag a lane; _apronBays, town_ApronStride an entry (hospitals, stations, then depots).
#define town_ClosedLane(l) TOWN(Uints, TownClosedLanesId).v[l]
#define town_ApronBay(i) TOWN(Ints, TownApronBaysId).v[i]

// What the interface handed the town: the selection (town_SelectedCount of it), and the walkers' orders.
#define town_Selected(i) TOWN(SelectionRows, TownSelectedId).r[i]
#define town_OrderedPerson(i) TOWN(Ints, TownOrderedPeopleId).v[i]
#define town_OrderedToM(i) TOWN(Vec2s, TownOrderedToMId).v[i]

#define errandsState TOWN(ErrandsStateRows, ErrandsStateId).r[0]
#define town_Hands errandsState.hands
#define town_OtherHand errandsState.otherHand
#define town_OtherCar errandsState.otherCar
#define town_Ordered errandsState.ordered
#define town_SelectedCount errandsState.selectedCount
#define town_WoundedCount errandsState.woundedCount
#define town_WreckCount errandsState.wreckCount
#define town_MendingCount errandsState.mendingCount
#define town_OnTheBar errandsState.onTheBar
#define town_TownParks errandsState.townParks
#define town_ClosuresChanged errandsState.closuresChanged
#define town_ApronStride errandsState.apronStride
#define beat_MostLanesClosed errandsState.beatMostLanesClosed

#endif
