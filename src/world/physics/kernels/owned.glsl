// A body's state through a step (DeviceSolver's State buffer): which bodies own their pairs, as the host's step's owners
// do (PhysicsWorld.FindContacts, SOL-37, SOL-37a), and what the settling works out of them (settle.glsl).
//
// grid_count.comp writes it for every roster body as the step begins: out of the world, or in it — frozen as the step
// began or not, and owned where it is awake: not frozen, or frozen and given motion since, which is an impulse having
// reached it. narrow.comp and woken.comp then own every frozen body a manifold with an owner meets, as it is met. The
// furniture is on no roster, and its state is always nought.

#ifndef OWNED_GLSL
#define OWNED_GLSL

// Owns its pairs this step: awake as it began, or woken by what met it.
const uint Owned = 1u;

// Settled enough to freeze (settle.glsl).
const uint Settling = 2u;

// Frozen as the step began.
const uint WasFrozen = 4u;

// Frozen as the step began and woken in it, set with Owned in one write.
const uint Woken = 8u;

// Out of the world, where it touches nothing — which asks what it touches as surely as owning does (PhysicsWorld.OwnedThisStep).
const uint Left = 16u;

// woken.comp's: among the woken bodies owning their pairs in this round, and having owned them in an earlier one.
const uint InRound = 32u;
const uint Asked = 64u;

bool awakeAtStart(uint s) { return (s & (Owned | Woken)) == Owned; }

bool frozenAtStart(uint s) { return (s & WasFrozen) != 0u && !awakeAtStart(s); }

// Frozen as the step began and not woken yet: what a manifold with an owner wakes.
bool stillFrozen(uint s) { return (s & (WasFrozen | Owned)) == WasFrozen; }

#endif
