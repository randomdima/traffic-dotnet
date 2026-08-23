# `E-8` — return to legal ground

Code: [E08ReturnToLegalGround.cs](../reactive/E08ReturnToLegalGround.cs) · [catalogue](index.md)

**Scenario.** The car is somewhere it should not be — shoved onto a verge, pushed across a kerb, left off
the network by a collision. CAR-9's recovery, and **rung 6 of the ladder**.

**`Sa` — the state it starts in.** The whole body is *not* on drivable ground, and a pose along the car's
**own axis** exists — searched outward both ways, half a car at a time, with centre, nose and tail tested
at every step — where all of it would be.

**`Sb` — the state it delivers.** The whole body on ground a car may drive on, at rest, with the plan
re-derived from there.

**Line.** One straight along the car's own axis, drawn for the rear axle, in whichever direction the
search found ground first.

**Do.** **Stop first**, then drive the straight at manoeuvring pace.

**Guards.** **Lane legality and the no-idling rule are suspended for this path. No-collision and red
lights still bind** (S-6) — which is exactly what keeps it a recovery rather than a licence.

**Bounds.** The search distance: four car lengths along the axis. Past that the car is not off the road,
it is somewhere else, and `E-9` or `E-10` is the honest answer.

**Exits.**

| | Successor |
|---|---|
| the straight is driven | `P-4`, which takes the lane under the car |
| the line is no longer a template | `P-4` (failure) |
| no pose along the axis reaches drivable ground | the next rung of the ladder |

**Why it stops first.** A car correcting a violation while still moving acquires a second one.

**Why the straight is along the axis and not toward the nearest legal point.** The path this manoeuvre can
issue is a **single straight**, so "the nearest lane point" — which is generally off to one side — would
have the car drive the right distance in the wrong direction.

**Refs.** CAR-6.2, CAR-9, S-6.
