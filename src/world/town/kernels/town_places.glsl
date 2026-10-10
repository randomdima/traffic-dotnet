// The town's places, by id (448–511) and by the C# name of what keeps them: parking_CarInBay(bay) is
// ParkingRegistry._carInBay[bay], containers_ the containment registry, buildings_ the plan's buildings, uses_ the rosters
// of what they are for, bayStreets_ the street each bay is worked off, town_ the town's index of the ways in. Laid once
// off the town (app/devicetick/TownState.Places). Who a walker is inside is town_people.glsl's people_Inside, the one
// array Containers keeps it in too; an exit spot is searched for each time and never kept.

#ifndef TOWN_PLACES_GLSL
#define TOWN_PLACES_GLSL

#include "world/town/kernels/town_tables.glsl"

// ParkingRegistry.NoBay, Nobody, and what a long hold may be besides a car: Claimed (an apron held, its vehicle not yet
// stood) and TheYard (a depot's yard slot).
const int ParkingRegistryNoBay = -1;
const int ParkingRegistryNobody = -1;
const int ParkingRegistryClaimed = -2;
const int ParkingRegistryTheYard = -3;

// Containers.NoDriver and the crew seats a car has; BayStreets.NoBay and the street lanes a bay is worked off at most.
const int ContainersNoDriver = -1;
const uint ContainersCrewSeats = 2u;
const int BayStreetsNoBay = -1;
const uint BayStreetsMostLanes = 2u;

// BuildingUse.
const uint BuildingUseOrdinary = 0u;
const uint BuildingUseHospital = 1u;
const uint BuildingUsePoliceStation = 2u;
const uint BuildingUseDepot = 3u;

// A bucket grid as it stands (BucketGrid): its window's cells (GridWindow), the level they are at (GridLevel: cells a
// metre, a cell's metres), the most an item sticks out of its own bucket, and how many items it files. A bucket's start
// and count are tables of their own, a word a cell of the window, a count nought where the bucket is not live.
struct BucketGridRow
{
    int fromX;
    int fromY;
    int width;
    int height;
    float perCellM;
    float cellM;
    float maxRadiusM;
    int count;
};

// How many of each place the town has, one bay's size, and the two grids: the bays' centres and the ways in.
struct PlacesState
{
    int bayCount;
    int buildingCount;
    int doorCount;
    int hospitals;
    int policeStations;
    int depots;
    float spaceLengthM;
    float spaceWidthM;
    BucketGridRow parkingNear;
    BucketGridRow doors;
};

layout(buffer_reference, std430, buffer_reference_align = 4) buffer PlacesStateRows { PlacesState r[]; };

const uint ParkingCentreMId = 448u;
const uint ParkingHeadingRadId = 449u;
const uint ParkingDoorMId = 450u;
const uint ParkingBayOfCarId = 451u;
const uint ParkingCarInBayId = 452u;
const uint ParkingClaimedById = 453u;
const uint ParkingClaimedBayOfCarId = 454u;
const uint ParkingTurningInId = 455u;
const uint ParkingTurnOfCarId = 456u;
const uint ParkingHeldForId = 457u;
const uint ParkingNearStartsId = 458u;
const uint ParkingNearCountsId = 459u;
const uint ParkingNearItemsId = 460u;
const uint ContainersBuildingCapacityId = 461u;
const uint ContainersBuildingOccupantsId = 462u;
const uint ContainersBuildingClaimsId = 463u;
const uint ContainersCarDriverId = 464u;
const uint ContainersCarPassengerId = 465u;
const uint ContainersCarCrewId = 466u;
const uint BuildingsCentreMId = 467u;
const uint BuildingsSizeMId = 468u;
const uint BuildingsHeadingRadId = 469u;
const uint BuildingsUseId = 470u;
const uint BuildingsEntryOffsetsId = 471u;
const uint BuildingsEntryPointMId = 472u;
const uint TownBuildingOfDoorId = 473u;
const uint TownDoorsStartsId = 474u;
const uint TownDoorsCountsId = 475u;
const uint TownDoorsItemsId = 476u;
const uint UsesHospitalsId = 477u;
const uint UsesPoliceStationsId = 478u;
const uint UsesDepotsId = 479u;
const uint BayStreetsLaneId = 480u;
const uint BayStreetsAtLaneMId = 481u;
const uint BayStreetsFirstBayOfLaneId = 482u;
const uint BayStreetsBaysOffLaneId = 483u;
const uint BayStreetsBayOnLaneId = 484u;
const uint PlacesStateId = 485u;

// ParkingRegistry: each bay's place (its centre, nose-in heading, and the way in's offset from the centre), who stands
// in it, whose leg drives to it, who turns in it and who holds it, with the per-car inverses; and the bays' grid.
#define parking_CentreM(b) TOWN(Vec2s, ParkingCentreMId).v[b]
#define parking_HeadingRad(b) TOWN(Floats, ParkingHeadingRadId).v[b]
#define parking_DoorM(b) TOWN(Vec2s, ParkingDoorMId).v[b]
#define parking_BayOfCar(c) TOWN(Ints, ParkingBayOfCarId).v[c]
#define parking_CarInBay(b) TOWN(Ints, ParkingCarInBayId).v[b]
#define parking_ClaimedBy(b) TOWN(Ints, ParkingClaimedById).v[b]
#define parking_ClaimedBayOfCar(c) TOWN(Ints, ParkingClaimedBayOfCarId).v[c]
#define parking_TurningIn(b) TOWN(Ints, ParkingTurningInId).v[b]
#define parking_TurnOfCar(c) TOWN(Ints, ParkingTurnOfCarId).v[c]
#define parking_HeldFor(b) TOWN(Ints, ParkingHeldForId).v[b]
#define parking_Near placesState.parkingNear
#define parking_NearStart(cell) TOWN(Ints, ParkingNearStartsId).v[cell]
#define parking_NearCount(cell) TOWN(Ints, ParkingNearCountsId).v[cell]
#define parking_NearItem(i) TOWN(Ints, ParkingNearItemsId).v[i]
#define parking_BayCount placesState.bayCount
#define parking_SpaceLengthM placesState.spaceLengthM
#define parking_SpaceWidthM placesState.spaceWidthM

// Containers: each building's room, who is in it and who is on the way; each car's driver, stretcher and crew seats
// (ContainersCrewSeats a car).
#define containers_BuildingCapacity(b) TOWN(Ints, ContainersBuildingCapacityId).v[b]
#define containers_BuildingOccupants(b) TOWN(Ints, ContainersBuildingOccupantsId).v[b]
#define containers_BuildingClaims(b) TOWN(Ints, ContainersBuildingClaimsId).v[b]
#define containers_CarDriver(c) TOWN(Ints, ContainersCarDriverId).v[c]
#define containers_CarPassenger(c) TOWN(Ints, ContainersCarPassengerId).v[c]
#define containers_CarCrew(c, seat) TOWN(Ints, ContainersCarCrewId).v[(c) * ContainersCrewSeats + (seat)]

// The plan's buildings (CityPlan.BuildingArrays) and their ways in, building b's from buildings_EntryOffsets(b) up to
// buildings_EntryOffsets(b + 1); the town's grid of the ways in and whose each is.
#define buildings_CentreM(b) TOWN(Vec2s, BuildingsCentreMId).v[b]
#define buildings_SizeM(b) TOWN(Vec2s, BuildingsSizeMId).v[b]
#define buildings_HeadingRad(b) TOWN(Floats, BuildingsHeadingRadId).v[b]
#define buildings_Use(b) TOWN(Uints, BuildingsUseId).v[b]
#define buildings_EntryOffsets(b) TOWN(Ints, BuildingsEntryOffsetsId).v[b]
#define buildings_EntryPointM(d) TOWN(Vec2s, BuildingsEntryPointMId).v[d]
#define buildings_Count placesState.buildingCount
#define town_BuildingOfDoor(d) TOWN(Ints, TownBuildingOfDoorId).v[d]
#define town_Doors placesState.doors
#define town_DoorsStart(cell) TOWN(Ints, TownDoorsStartsId).v[cell]
#define town_DoorsCount(cell) TOWN(Ints, TownDoorsCountsId).v[cell]
#define town_DoorsItem(i) TOWN(Ints, TownDoorsItemsId).v[i]
#define town_DoorCount placesState.doorCount

// BuildingUses: each roster's buildings in ascending order, an entry's index what the aprons are numbered by.
#define uses_Hospital(entry) TOWN(Ints, UsesHospitalsId).v[entry]
#define uses_PoliceStation(entry) TOWN(Ints, UsesPoliceStationsId).v[entry]
#define uses_Depot(entry) TOWN(Ints, UsesDepotsId).v[entry]
#define uses_Hospitals placesState.hospitals
#define uses_PoliceStations placesState.policeStations
#define uses_Depots placesState.depots

// BayStreets: the street lanes each bay is worked off (_lane, BayStreetsMostLanes slots a bay, the first NoLane ending
// them) and how far along each its mouth stands (_atLaneM — the query by lane is town_map_queries.glsl's
// bayStreets_AtLaneM); the bays off each lane (from bayStreets_FirstBayOfLane(l) up to that of l + 1); the bay a bay's
// own arm is.
#define bayStreets_SlotLane(b, slot) TOWN(Ints, BayStreetsLaneId).v[(b) * BayStreetsMostLanes + (slot)]
#define bayStreets_SlotAtLaneM(b, slot) TOWN(Floats, BayStreetsAtLaneMId).v[(b) * BayStreetsMostLanes + (slot)]
#define bayStreets_FirstBayOfLane(l) TOWN(Ints, BayStreetsFirstBayOfLaneId).v[l]
#define bayStreets_BayOffLane(i) TOWN(Ints, BayStreetsBaysOffLaneId).v[i]
#define bayStreets_BayOnLane(l) TOWN(Ints, BayStreetsBayOnLaneId).v[l]

#define placesState TOWN(PlacesStateRows, PlacesStateId).r[0]

#endif
