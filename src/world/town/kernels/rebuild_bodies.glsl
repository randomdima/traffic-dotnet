// Every body of a rebuild numbered and filed on the device (TownWorld.LayTheWalkersBody, LayTheCarsBody,
// LaneOccupancy.LayBody): the pieces each walker's and each car's body laid, a unit each in the host's order — walkers,
// then cars — taken as candidates; every candidate whose holder already stands on its way under the same name grown into
// the one laid first (LayBody), and every other given the next slot. The one binding layout every kernel of it runs over
// (rebuild_bodies_*.comp), laid by app/devicetick/DeviceRebuild. The including kernel declares `figures`
// (walkerCount, carCount, wayCount, coverRoom, carRoom) first.
//
// A candidate is named by where it lies: in a walker's room of the walkers' covers, or — its top bit set — in a car's room
// of the cars' rows. Which candidates share a way is found by filing them under their ways, each way's run sorted into
// the host's order, so a body is grown in exactly the order the host grows it.

#include "world/road/kernels/claims_rows.glsl"
#include "world/road/kernels/claims_tally.glsl"
#include "world/town/kernels/rebuild_rows.glsl"

layout(std430, binding = 0) readonly buffer WalkerCounts { uvec4 walkerCounts[]; };
layout(std430, binding = 1) readonly buffer WalkerCovers { ClaimRow walkerCovers[]; };
layout(std430, binding = 2) readonly buffer CarCounts { uvec4 carCounts[]; };
layout(std430, binding = 3) readonly buffer CarRows { ClaimRow carRows[]; };
layout(std430, binding = 4) buffer UnitCandidates { uint unitCandidates[]; };
layout(std430, binding = 5) buffer UnitBodies { uint unitBodies[]; };
layout(std430, binding = 6) buffer CandidateRefs { uint candidateRefs[]; };
layout(std430, binding = 7) buffer WayCounts { uint wayCounts[]; };
layout(std430, binding = 8) readonly buffer WayStarts { uint wayStarts[]; };
layout(std430, binding = 9) buffer WayCursor { uint wayCursor[]; };
layout(std430, binding = 10) buffer CandidateOrder { uint candidateOrder[]; };
layout(std430, binding = 11) buffer Firsts { uint firsts[]; };
layout(std430, binding = 12) writeonly buffer Rows { ClaimRow rows[]; };
layout(std430, binding = 13) readonly buffer Tally { uint tally[]; };

const uint CandidateOfACar = 0x80000000u;

uint bodiesUnitCount() { return figures.walkerCount + figures.carCount; }

// How many pieces a unit's body laid: a walker's body rows (walkers_body.comp), or a car's (car_bodies.comp).
uint bodiesPiecesOf(uint unit)
{
    return unit < figures.walkerCount ? walkerCounts[unit].x : carCounts[unit - figures.walkerCount].x;
}

// The name of a unit's piece, by where it lies.
uint bodiesRefOf(uint unit, uint piece)
{
    return unit < figures.walkerCount
        ? unit * figures.coverRoom + piece
        : CandidateOfACar | ((unit - figures.walkerCount) * figures.carRoom + piece);
}

ClaimRow bodiesCandidate(uint ref)
{
    return (ref & CandidateOfACar) != 0u ? carRows[ref & ~CandidateOfACar] : walkerCovers[ref];
}

// A piece of no length the host never lays (LayBody's own refusal).
bool bodiesRefused(ClaimRow piece) { return piece.toM < piece.fromM; }

bool bodiesSameHolder(ClaimRow one, ClaimRow other)
{
    return one.occupant == other.occupant && claimRoster(one) == claimRoster(other);
}

// A body grown to cover a piece laid after it under the same name on the same way (LaneOccupancy.LayBody): both
// stretches, travelling the way where either was, and onward where the last travelling it says.
ClaimRow bodiesGrown(ClaimRow body, ClaimRow laid)
{
    body.fromM = min(body.fromM, laid.fromM);
    body.toM = max(body.toM, laid.toM);
    if ((laid.flags & ClaimOnItsLine) != 0u)
    {
        body.flags |= ClaimOnItsLine;
        body.onward = laid.onward;
    }

    return body;
}
