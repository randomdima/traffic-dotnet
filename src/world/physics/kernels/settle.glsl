// What the step settled (PhysicsWorld.Rest, SOL-37, SOL-37b), shared by settle.comp and settle_spread.comp: the table
// each pass reads, and what is passed along the contacts.
//
// Every moving body is on the roster and stepped, frozen or not; a frozen one is laid at exactly no motion and left
// exactly where it stood. The owners are the host step's (owned.glsl): every body in the world awake as the step began,
// and every frozen one a manifold with an owner woke, and so on — so every contact has an owner, and a frozen body in
// one is owned. Rest and its unsettling are worked over the owners alone, as the host's are.

#include "step.glsl"
#include "owned.glsl"

// The passes, in the order a step records them (DeviceSolver.RecordTheSettling).
const uint Resting = 2u;
const uint Unsettling = 3u;
const uint Freezing = 4u;
const uint Listing = 5u;

// Where the step's totals keep whether the parallel pass passed anything along, and how many woke and froze
// (DeviceSolver.Totals).
const uint UnsettledAt = 17u;
const uint WokeCountAt = 18u;
const uint FrozeCountAt = 19u;

layout(std430, binding = 0) readonly buffer Owners { int owners[]; };
layout(std430, binding = 1) buffer Filter { uint filter_[]; };
layout(std430, binding = 2) buffer Motion { vec4 motion[]; };
layout(std430, binding = 3) readonly buffer ShapeB { vec4 shape[]; };
layout(std430, binding = 4) readonly buffer Overlap { uint overlap[]; };
layout(std430, binding = 5) buffer Rest { uint restTicks[]; };

// Nought for every body off the roster — the furniture's above all — which no pass writes.
layout(std430, binding = 6) coherent buffer State { uint state[]; };

layout(std430, binding = 7) readonly buffer Contact { uvec4 contact[]; };
layout(std430, binding = 8) buffer Totals { uint totals[]; };

// How many woke and how many froze, then the woken from 2 on and the frozen from 2 + the roster on, in its order.
layout(std430, binding = 9) writeonly buffer Settled { uint settled[]; };

// Each roster slot's flag for the scans, and after them where it is listed.
layout(std430, binding = 10) buffer WokeAt { uint wokeAt[]; };
layout(std430, binding = 11) buffer FrozeAt { uint frozeAt[]; };

uint contactCount() { return min(totals[ContactTotalAt], contactCapacity); }

// A pair touching settles together or not at all (SOL-37b): the furniture's pairs aside. Whether the contact passed it
// along.
bool unsettle(uint at)
{
    uvec2 pair = contact[at].xy;
    uint first = state[pair.x];
    uint second = state[pair.y];
    if ((filter_[pair.y] & staticBit) != 0u) return false;
    if (((first ^ second) & Settling) == 0u) return false;

    atomicAnd(state[pair.x], ~Settling);
    atomicAnd(state[pair.y], ~Settling);
    return true;
}
