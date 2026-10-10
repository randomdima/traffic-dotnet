// The cars' fields no row of town_rows.glsl holds, by id (128–255) and by their C# names: cars_Action(car) is
// CarFleet.Action[car], town_AsksTheRoute(car) is TownWorld._asksTheRoute[car]. Laid once off the town
// (app/devicetick/TownState.Cars). A car's field a row holds is reached there, never here — and of those, BoxIsOurs,
// TurningAtTheBox, InsideTheBox and CommittedToTheBox are cars_Box(c).flags' BoxClaimed, BoxTurning, BoxInside and
// BoxCommitted, DrivenSlipping and WheelsOnAWay cars_TyreRow(c).flags' bit 2 and bits 8–11, and TownWorld._standsParked
// cars_BodyFlags(c)'s CarRestsParked.

#ifndef TOWN_CARS_GLSL
#define TOWN_CARS_GLSL

#include "world/town/kernels/town_tables.glsl"

// CarAction.
const uint CarActionStand = 0u;
const uint CarActionFollow = 1u;
const uint CarActionOvertake = 2u;
const uint CarActionSwitch = 3u;
const uint CarActionBackUp = 4u;
const uint CarActionPark = 5u;
const uint CarActionUnpark = 6u;
const uint CarActionRejoin = 7u;
const uint CarActionHand = 8u;
const uint CarActionTowed = 9u;

// HeadwayKind: what cut a grant (CarFleet.GrantCutBy).
const uint HeadwayKindNothing = 0u;
const uint HeadwayKindQueue = 1u;
const uint HeadwayKindObstruction = 2u;
const uint HeadwayKindClaimed = 3u;
const uint HeadwayKindLight = 4u;
const uint HeadwayKindWalker = 5u;
const uint HeadwayKindPassing = 6u;

// DrivingHold: what limited a car (CarFleet.Hold).
const uint DrivingHoldNone = 0u;
const uint DrivingHoldCorner = 1u;
const uint DrivingHoldWheel = 2u;
const uint DrivingHoldLineEnd = 3u;
const uint DrivingHoldClaimed = 4u;
const uint DrivingHoldWaiting = 5u;
const uint DrivingHoldReach = 6u;
const uint DrivingHoldPlace = 7u;
const uint DrivingHoldLostLine = 8u;
const uint DrivingHoldBackingUp = 9u;

// CarFleet.NoLane, NoOwner, NoBay, NoWay; and how much of a route a car carries at once.
const int CarFleetNoLane = -1;
const int CarFleetNoOwner = -1;
const int CarFleetNoBay = -1;
const int CarFleetNoWay = -1;
const uint CarFleetRouteLanesPerCar = 64u;

const uint CarsDrawId = 128u;
const uint CarsOwnerId = 129u;
const uint CarsDrivenId = 130u;
const uint CarsActionId = 131u;
const uint CarsBrokenId = 132u;
const uint CarsChannelsId = 133u;
const uint CarsAmbulanceId = 134u;
const uint CarsBlueLightId = 135u;
const uint CarsAtWorkId = 136u;
const uint CarsLineLaidId = 137u;
const uint CarsRouteLaidId = 138u;
const uint CarsStopsForBayId = 139u;
const uint CarsStopsAtItsPlaceId = 140u;
const uint CarsTurnsBackOnId = 141u;
const uint CarsClaimToMId = 142u;
const uint CarsRouteLanesId = 143u;
const uint CarsRouteCountId = 144u;
const uint CarsRouteTakenId = 145u;
const uint CarsRouteRunsOutId = 146u;
const uint CarsRouteEndsOnId = 147u;
const uint CarsRouteEndsAtMId = 148u;
const uint CarsDestinationMId = 149u;
const uint CarsHasDestinationId = 150u;
const uint CarsReroutesId = 151u;
const uint CarsFuseJitterId = 152u;
const uint CarsBacksIntoBaysId = 153u;
const uint CarsLineIsReverseId = 154u;
const uint CarsBackRoomMId = 155u;
const uint TownBehindTheBarId = 160u;
const uint TownAsksTheRouteId = 161u;
const uint TownWreckedWordsId = 162u;
const uint StandingKeptId = 163u;
const uint StandingFrozeAtStepId = 164u;
const uint StandingEntriesId = 165u;
const uint StandingPositionMId = 166u;
const uint StandingHeadingRadId = 167u;
const uint StandingAskedId = 168u;
const uint StandingAskedCountId = 169u;
const uint StandingReadOnId = 170u;

// CarFleet.
#define cars_Draw(c) TOWN(Rngs, CarsDrawId).r[c]
#define cars_Owner(c) TOWN(Ints, CarsOwnerId).v[c]
#define cars_Driven(c) TOWN(Uints, CarsDrivenId).v[c]
#define cars_Action(c) TOWN(Uints, CarsActionId).v[c]
#define cars_Broken(c) TOWN(Uints, CarsBrokenId).v[c]
#define cars_Channels(c) TOWN(Uints, CarsChannelsId).v[c]
#define cars_Ambulance(c) TOWN(Uints, CarsAmbulanceId).v[c]
#define cars_BlueLight(c) TOWN(Uints, CarsBlueLightId).v[c]
#define cars_AtWork(c) TOWN(Uints, CarsAtWorkId).v[c]
#define cars_LineLaid(c) TOWN(Uints, CarsLineLaidId).v[c]
#define cars_RouteLaid(c) TOWN(Uints, CarsRouteLaidId).v[c]
#define cars_StopsForBay(c) TOWN(Ints, CarsStopsForBayId).v[c]
#define cars_StopsAtItsPlace(c) TOWN(Uints, CarsStopsAtItsPlaceId).v[c]
#define cars_TurnsBackOn(c) TOWN(Ints, CarsTurnsBackOnId).v[c]
#define cars_ClaimToM(c) TOWN(Floats, CarsClaimToMId).v[c]
#define cars_RouteLane(c, i) TOWN(Ints, CarsRouteLanesId).v[(c) * CarFleetRouteLanesPerCar + (i)]
#define cars_RouteCount(c) TOWN(Ints, CarsRouteCountId).v[c]
#define cars_RouteTaken(c) TOWN(Ints, CarsRouteTakenId).v[c]
#define cars_RouteRunsOut(c) TOWN(Uints, CarsRouteRunsOutId).v[c]
#define cars_RouteEndsOn(c) TOWN(Ints, CarsRouteEndsOnId).v[c]
#define cars_RouteEndsAtM(c) TOWN(Floats, CarsRouteEndsAtMId).v[c]
#define cars_DestinationM(c) TOWN(Vec2s, CarsDestinationMId).v[c]
#define cars_HasDestination(c) TOWN(Uints, CarsHasDestinationId).v[c]
#define cars_Reroutes(c) TOWN(Uints, CarsReroutesId).v[c]
#define cars_FuseJitter(c) TOWN(Floats, CarsFuseJitterId).v[c]
#define cars_BacksIntoBays(c) TOWN(Uints, CarsBacksIntoBaysId).v[c]
#define cars_LineIsReverse(c) TOWN(Uints, CarsLineIsReverseId).v[c]
#define cars_BackRoomM(c) TOWN(Floats, CarsBackRoomMId).v[c]

// CarFleet.DrivenFrontShare is the build's (written once off it and read nowhere): reached there.
#define cars_DrivenFrontShare(c) cars_Build(c).drivenFrontShare

// TownWorld's own: whether the nose was behind its approach's bar last tick, whether the next decision lays the rest of
// the line, and the wrecks as a bitset (AgentSet, a word 32 cars: bit c & 31 of word c >> 5).
#define town_BehindTheBar(c) TOWN(Uints, TownBehindTheBarId).v[c]
#define town_AsksTheRoute(c) TOWN(Uints, TownAsksTheRouteId).v[c]
#define town_WreckedWord(w) TOWN(Uints, TownWreckedWordsId).v[w]

// TownWorld.StandingCars: what a laying kept of a car that stood, and the cars it asked (Asked, AskedCount long).
#define standing_Kept(c) TOWN(Uints, StandingKeptId).v[c]
#define standing_FrozeAtStep(c) TOWN(Ints, StandingFrozeAtStepId).v[c]
#define standing_Entries(c) TOWN(Uints, StandingEntriesId).v[c]
#define standing_PositionM(c) TOWN(Vec2s, StandingPositionMId).v[c]
#define standing_HeadingRad(c) TOWN(Floats, StandingHeadingRadId).v[c]
#define standing_Asked(i) TOWN(Ints, StandingAskedId).v[i]
#define standing_AskedCount TOWN(Ints, StandingAskedCountId).v[0]
#define standing_ReadOn(c) TOWN(Uints, StandingReadOnId).v[c]

#endif
