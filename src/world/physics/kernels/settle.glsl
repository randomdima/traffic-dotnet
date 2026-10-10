// What the step settled (PhysicsWorld.Rest, SOL-37, SOL-37a, SOL-37b), shared by settle.comp and settle_spread.comp:
// the table each pass reads, a body's state through them, and the two things passed along the contacts.
//
// Every moving body is on the roster and stepped, frozen or not; a frozen one is laid at exactly no motion and left
// exactly where it stood. The owners are the host's step's: every body in the world not frozen as the step began,
// every frozen one the step moved or left moving — an impulse reached it — and every frozen one touching an owner,
// and so on. Rest and its unsettling are worked over the owners alone, as the host's are.

#include "step.glsl"

// A body's state through the passes: owned by the step, settled enough to freeze, frozen as the step began.
const uint Owned = 1u;
const uint Settling = 2u;
const uint WasFrozen = 4u;

// The passes, in the order a step records them (DeviceSolver.RecordTheSettling).
const uint Owning = 0u;
const uint Waking = 1u;
const uint Resting = 2u;
const uint Unsettling = 3u;
const uint Freezing = 4u;
const uint Listing = 5u;

// Where the step's totals keep whether a parallel pass passed anything along, and how many woke and froze
// (DeviceSolver.Totals).
const uint WokenAt = 16u;
const uint UnsettledAt = 17u;
const uint WokeCountAt = 18u;
const uint FrozeCountAt = 19u;

layout(std430, binding = 0) readonly buffer Owners { int owners[]; };
layout(std430, binding = 1) buffer Filter { uvec4 filter_[]; };
layout(std430, binding = 2) buffer Motion { vec4 motion[]; };
layout(std430, binding = 3) readonly buffer ShapeB { vec4 shape[]; };
layout(std430, binding = 4) readonly buffer Overlap { uint overlap[]; };
layout(std430, binding = 5) readonly buffer Moved { uint moved[]; };
layout(std430, binding = 6) buffer Rest { uint restTicks[]; };

// Nought for every body off the roster — the furniture's above all — which no pass writes.
layout(std430, binding = 7) coherent buffer State { uint state[]; };

layout(std430, binding = 8) readonly buffer Contact { uvec4 contact[]; };
layout(std430, binding = 9) buffer Totals { uint totals[]; };
layout(std430, binding = 10) readonly buffer Afresh { int afresh[]; };

// How many woke and how many froze, then the woken from 2 on and the frozen from 2 + the roster on, in its order.
layout(std430, binding = 11) writeonly buffer Settled { uint settled[]; };

// Each roster slot's flag for the scans, and after them where it is listed.
layout(std430, binding = 12) buffer WokeAt { uint wokeAt[]; };
layout(std430, binding = 13) buffer FrozeAt { uint frozeAt[]; };

uint contactCount() { return min(totals[ContactTotalAt], capacity); }

// A frozen body touching an owner, owned (PhysicsWorld.WakeWhatTheStepTouched); whether the contact passed it along.
bool wake(uint at)
{
    uvec2 pair = contact[at].xy;
    uint first = state[pair.x];
    uint second = state[pair.y];
    bool firstOwned = (first & Owned) != 0u;
    if (firstOwned == ((second & Owned) != 0u)) return false;
    if (((firstOwned ? second : first) & WasFrozen) == 0u) return false;

    atomicOr(state[firstOwned ? pair.y : pair.x], Owned);
    return true;
}

// A pair touching settles together or not at all (SOL-37b): the furniture's pairs aside, and those neither of whose
// bodies the step owned, which the host drops. Whether the contact passed it along.
bool unsettle(uint at)
{
    uvec2 pair = contact[at].xy;
    uint first = state[pair.x];
    uint second = state[pair.y];
    if (((first | second) & Owned) == 0u || (filter_[pair.y].x & staticBit) != 0u) return false;
    if (((first ^ second) & Settling) == 0u) return false;

    atomicAnd(state[pair.x], ~Settling);
    atomicAnd(state[pair.y], ~Settling);
    return true;
}
