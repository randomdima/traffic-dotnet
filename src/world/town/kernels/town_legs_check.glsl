// The drive leg, the line and parking asked of themselves on staged towns (tests/devicetick/TownLegsTests): one thread
// runs a probe over a list of cars in the list's order, as the C# takes them one after another, and writes what each left
// of the car into a row of the table outId names, and what the leg counted in the row after. What the leg calls of
// another port — a car's action, an errand — is answered here from what the host staged for it, or, where a probe should
// never reach it, marked as reached.
//
// A probe a kernel (town_legs_check_*.comp): the leg inlined whole into one kernel for every probe is past the optimiser's
// room for ids.

#ifndef TOWN_LEGS_CHECK_GLSL
#define TOWN_LEGS_CHECK_GLSL

// One invocation whose every read is uniform: each read made a lane's (town_root.glsl's TOWN_AT).
#extension GL_KHR_shader_subgroup_basic : require
#define TOWN_AT(id) ((id) + (gl_SubgroupInvocationID >> 31u))

#include "world/town/kernels/town_state.glsl"
#include "world/town/kernels/town_legs.glsl"

layout(local_size_x = 1) in;

layout(push_constant) uniform Push
{
    uint probe;
    uint count;
    uint outId;
    uint stride;
    float sinceLastDecisionS;
    uint fromTheQueue;
} push;

// The staged tables, at the top of the root: each listed car and its argument, each car's answers to what the leg asks of
// another port, each person's (whether at the wheel), what ParkingIn.StopForTheBayM answers a variant at each bay's lane
// slots, and the town's.
const uint ListTable = 2043u;
const uint AnswersTable = 2046u;
const uint PeopleAnswersTable = 2045u;
const uint StopsTable = 2044u;
const uint GlobalsTable = 2042u;

struct LegsAnswers
{
    uint aimedAtAPlace;
    int entranceLane;
    float entranceAlongM;
    uint underOrders;
    uint evacuator;
    uint patrol;
    uint wheelHeldOver;
    float offTheLineAllowanceM;
    uint closedLinksFor;
    uint getsPast;
    uint wantsToSwitch;
    uint pad0;
};

struct LegsCheckGlobals
{
    uint closedLinksNow;
    uint holdAgents;
    int bayCount;
    uint pad;
};

layout(buffer_reference, std430, buffer_reference_align = 4) buffer LegsAnswersRows { LegsAnswers r[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer LegsCheckGlobalsRows { LegsCheckGlobals r[]; };

#define answers(c) TOWN(LegsAnswersRows, AnswersTable).r[c]
#define globals TOWN(LegsCheckGlobalsRows, GlobalsTable).r[0]

// What a probe reached that it should not have, a bit each.
const uint ReachedShift = 1u;
const uint ReachedManoeuvre = 2u;
const uint ReachedErrand = 4u;
const uint ReachedUnstagedStop = 8u;

uint reached;

bool manoeuvres_IsBegun(uint car) { return manoeuvres_Stage(car) == ManoeuvreStageBegun; }

void manoeuvres_Clear(uint car)
{
    manoeuvres_Shape(car) = ManoeuvreShape(0, 0, 0u, 0u);
    manoeuvres_Kind(car) = ManoeuvreKindNone;
    manoeuvres_Stage(car) = ManoeuvreStageNone;
    manoeuvres_Bay(car) = ManoeuvresNoBay;
    manoeuvres_Lane(car) = ManoeuvresNoLane;
    manoeuvres_Piece(car) = 0;
}

void overtaking_ShiftTheLine(uint car, int lane, float shiftM) { reached |= ReachedShift; }

void switching_ShiftTheLine(uint car, float shiftM) { reached |= ReachedShift; }

// Staged as the C#'s MayGetPastWhatCutIt and ComesUpTo together, which is all the leg asks of them.
bool overtaking_MayGetPastWhatCutIt(uint car, out ClaimRow cutBy, out int cutOn)
{
    cutBy = ClaimRow(0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, -1, -1, -1, 0u, 0u);
    cutOn = -1;
    return answers(car).getsPast != 0u;
}

bool overtaking_ComesUpTo(uint car, float alongMps, float toTheStopM) { return true; }

bool switching_Wants(uint car, out bool inward)
{
    inward = false;
    return answers(car).wantsToSwitch != 0u;
}

bool pullingOut_ShapeTheWayOut(uint car, int bay, int lane)
{
    reached |= ReachedManoeuvre;
    return false;
}

bool pullingOut_Arrive(uint car)
{
    reached |= ReachedManoeuvre;
    return false;
}

bool parkingIn_Arrive(uint car)
{
    reached |= ReachedManoeuvre;
    return false;
}

bool bayManoeuvring_TakeTheNextPiece(uint car)
{
    reached |= ReachedManoeuvre;
    return false;
}

float parkingIn_StopForTheBayM(uint car, int bay, int lane)
{
    for (uint slot = 0u; slot < BayStreetsMostLanes; slot++)
    {
        if (bayStreets_SlotLane(bay, slot) != lane) continue;

        return TOWN(Floats, StopsTable).v[(cars_Variant(car) * uint(globals.bayCount) + uint(bay)) * BayStreetsMostLanes + slot];
    }

    reached |= ReachedUnstagedStop;
    return townMapNaN();
}

bool town_IsAimedAtAPlaceInTheRoad(uint car) { return answers(car).aimedAtAPlace != 0u; }

bool town_TheEntranceItIsSentTo(uint car, out int lane, out float alongM)
{
    lane = answers(car).entranceLane;
    alongM = answers(car).entranceAlongM;
    return lane >= 0;
}

bool town_ClosedLinksFor(uint car) { return answers(car).closedLinksFor != 0u; }

bool town_ClosedLinksNow() { return globals.closedLinksNow != 0u; }

bool town_IsUnderOrders(uint car) { return answers(car).underOrders != 0u; }

void carOrders_Release(uint car) { reached |= ReachedErrand; }

bool town_IsAnEvacuator(uint car) { return answers(car).evacuator != 0u; }

float town_OffTheLineAllowanceM(uint car) { return answers(car).offTheLineAllowanceM; }

bool town_IsAPatrolCar(uint car) { return answers(car).patrol != 0u; }

bool town_WheelIsHeldOver(uint car) { return answers(car).wheelHeldOver != 0u; }

bool town_IsAtTheWheel(int person) { return TOWN(Uints, PeopleAnswersTable).v[person] != 0u; }

void town_BeginTheStand(uint car) { reached |= ReachedErrand; }

bool town_HoldAgents() { return globals.holdAgents != 0u; }

// Words of a car's row: its figures, then its line's arcs, entries, lanes and route queue.
const uint RowHead = 32u;

void put(uint row, uint word, uint value) { TOWN(Uints, push.outId).v[push.stride * row + word] = value; }
void put(uint row, uint word, int value) { put(row, word, uint(value)); }
void put(uint row, uint word, float value) { put(row, word, floatBitsToUint(value)); }
void put(uint row, uint word, bool value) { put(row, word, value ? 1u : 0u); }

void putTheCar(uint row, uint car)
{
    // A row a probe run twice wrote first holds the first run's words past where the second's end.
    for (uint word = 0u; word < push.stride; word++) put(row, word, 0u);

    CarLineRow line = cars_Line(car);
    put(row, 0u, line.arcCount);
    put(row, 1u, line.laneCount);
    put(row, 2u, line.lengthM);
    put(row, 3u, line.finalApproach);
    put(row, 4u, cars_StopsForBay(car));
    put(row, 5u, cars_StopsAtItsPlace(car));
    put(row, 6u, town_AsksTheRoute(car));
    put(row, 7u, cars_LineIsReverse(car));
    put(row, 8u, cars_AuthorityM(car));
    put(row, 9u, cars_RouteTaken(car));
    put(row, 10u, cars_RouteCount(car));
    put(row, 11u, cars_RouteRunsOut(car));
    put(row, 12u, cars_RouteEndsOn(car));
    put(row, 13u, cars_RouteEndsAtM(car));
    put(row, 14u, cars_TurnsBackOn(car));
    put(row, 15u, cars_ProgressM(car));
    put(row, 16u, reached);
    put(row, 17u, cars_LineLaid(car));
    put(row, 18u, cars_RouteLaid(car));
    put(row, 19u, parking_TurnOfCar(car));
    put(row, 20u, parking_ClaimedBayOfCar(car));
    put(row, 21u, cars_ClockSinceS(car));
    put(row, 22u, cars_ClockClosestM(car));
    put(row, 23u, cars_Reroutes(car));
    put(row, 24u, cars_Action(car));
    put(row, 25u, cars_Driven(car));
    put(row, 26u, cars_HasDestination(car));
    put(row, 27u, cars_CoveredSinceClaimM(car));
    put(row, 28u, cars_HorizonM(car));

    uint arcs = townCounts.arcsPerCar;
    uint lanes = townCounts.mostLanes;
    for (uint arc = 0u; arc < line.arcCount; arc++)
    {
        vec4 a = cars_LineArcA(car, arc);
        vec4 b = cars_LineArcB(car, arc);
        for (uint word = 0u; word < 4u; word++)
        {
            put(row, RowHead + 8u * arc + word, a[word]);
            put(row, RowHead + 8u * arc + 4u + word, b[word]);
        }

        put(row, RowHead + 8u * arcs + arc, cars_LineEntry(car, arc));
    }

    uint lanesAt = RowHead + 9u * arcs;
    for (uint slot = 0u; slot < line.laneCount; slot++)
    {
        put(row, lanesAt + 5u * slot, cars_Chain(car, slot));
        put(row, lanesAt + 5u * slot + 1u, cars_LaneStartM(car, slot));
        put(row, lanesAt + 5u * slot + 2u, cars_LaneEndM(car, slot));
        put(row, lanesAt + 5u * slot + 3u, cars_LaneFirstArc(car, slot));
        put(row, lanesAt + 5u * slot + 4u, cars_JoinBreaks(car, slot));
    }

    uint routeAt = lanesAt + 5u * lanes;
    for (int at = 0; at < cars_RouteCount(car); at++) put(row, routeAt + uint(at), cars_RouteLane(car, uint(at)));
}

int listedCar(uint row) { return TOWN(Ints, ListTable).v[2u * row]; }
int listedArgument(uint row) { return TOWN(Ints, ListTable).v[2u * row + 1u]; }

// What the leg counted, in the row after the listed ones.
void putTheCounts()
{
    put(push.count, 0u, legsCounts.legsGivenUp);
    put(push.count, 1u, legsCounts.reroutesTaken);
    put(push.count, 2u, legsCounts.placesGivenUp);
    put(push.count, 3u, legsCounts.turnsAtALotBegun);
    put(push.count, 4u, legsCounts.linesReacquired);
    put(push.count, 5u, legsCounts.baysParkedIn);
    put(push.count, 6u, legsCounts.routeArrivals);
    put(push.count, 7u, legsCounts.routeSearches);
}

#endif
