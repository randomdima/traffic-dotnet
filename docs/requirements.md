# Requirements — the cross-cutting rules

**Only what belongs to no single slice is here.** Everything else lives with the code it governs, and
[index.md](index.md) maps every requirement ID to the document that owns it.

**A rule states a relation. A number is not a rule** — every figure is on `SimConfig`
([core](../src/core/docs/requirements.md#where-a-figure-lives)). **No ID is ever renumbered** and a retired
number is never reused, because the code cites these codes. **Every rule carries a rung** — `P0` and `P1`
are the owner's and bend for nothing and for very little; `P2`–`P9` are the assistant's, ranked by what
bending one costs ([priority.md](priority.md)).

## Purpose and scope

**What this project is for, what it refuses to be, and the bar every judgement call is settled against
are [goals.md](goals.md)** — stated there and nowhere else, because a purpose written twice is two
purposes within a month.

**TEC-1** `P2` The spec constrains no engine, language or renderer; this project settles it as C# on .NET 11
with nothing under it ([goals.md](goals.md)).

**TEC-2** `P3` The physics layer provides collision, contact resolution **and motion integration**. Every
dynamic body is a rigid body driven only by traction-limited friction and drive impulses; pushes,
stopping and terrain effects are **solver output, never scripted displacement**. Motion is continuous and
vector-based: no grid quantisation of position, velocity or heading anywhere.

## The two rule classes

**SIM-1** `P3` Two classes exist and **must stay distinct in the implementation**:

| | Hard rules | Soft rules |
|---|---|---|
| What | Physical constraints | Traffic rules |
| Enforced by | The physics layer | The agent's own intent |
| Violable | No | Yes |
| On violation | Cannot happen | A defined **recovery behaviour**, never an engine-level correction |

Hard rules live in the physics layer and the damage resolver only. **Soft rules never touch the physics
layer**: nothing may teleport, snap, clamp or nudge a body because it is somewhere illegal.

**SIM-2** `P4` Every dynamic body carries at least: position, heading, velocity, the terrain it currently
occupies, and its liveness state.

**SIM-6** `P5` A soft rule binds in exactly one of two ways, and **which one is part of the rule**:

- **As a ban**, where the agent *chooses* — in route planning, target selection, plan derivation. A
  banned option is not costed, it is **absent**.
- **As a price**, where the agent merely *prefers*. A price is outbid by distance and is meant to be.

**A ban lifts only where keeping it would leave the agent with no route at all** — being stranded is
worse than being illegal — and it lifts for that agent, on that plan, never in general. Where a ban is
lifted the *manner* of the act is unchanged: it is still performed by whatever performs it everywhere
else, with that mechanism's own guards. **Hard rules never lift**, in planning or in recovery.

**SIM-7** `P4` Where a decision has already been taken by one mechanism, **no second mechanism may guard it**.
A duplicate gate does not make the town safer; it makes the first mechanism useless. **Before adding a
check that refuses a movement, name what has already refused it.** The measured symptom is in
[decision-log.md](decision-log.md).

Units, the two seeds, the tick and the decision clock are [core](../src/core/docs/requirements.md) —
`SIM-3`, `SIM-4` and `AGT-6` live there.

## The object catalogue

**OBJ-2** `P4` Four kinds and **one shape** — an oriented box with its corners rounded — and the four kinds
are what they set it to:

| Object | Shape | Kind | Contains |
|---|---|---|---|
| Prop | small disc — a radius with no box | static | — |
| Building | one rounded rectangle, or one or more square-cornered ones | static | 0..capacity persons |
| Person | small disc | dynamic | — |
| Car | one rounded rectangle | dynamic | 0..1 driver |

**A disc is not a second shape**: it is the same rounded box with nothing in the middle of it, and the
solver holds one shape and one narrow phase for all four (`SOL-1`). **A traffic light has no body**: its
heads are drawn and never collided (`SignalHeads`).

**A car collides as its authored shape**, a rounded box inside the footprint its picture is drawn in, and
not as the footprint (`CAR-12b`). A per-variant shape is still not what the town is laid against.

**OBJ-4** `P5` A building exposes at least one point on walkable terrain through which persons enter and exit
([world/containment](../src/world/containment/docs/requirements.md)).

**OBJ-5** `P6` Building capacity scales with footprint.

**OBJ-5a** `P3` **A building is collided as the rectangles its roof is drawn of and not as the box that roof
was drawn in.** An L, a courtyard, a cut corner and a porch are all one defect otherwise — metres of
empty box that stop a car in the open — and the parts are the picture's own, so what is drawn and what
is stood are one answer and cannot drift apart.

**OBJ-6** `P9` **A roof's fans turn.** Each round fan its picture draws is kept as its geometry alone — its hub and
its rim — and drawn over as the roof is drawn in one of the fans' looks (`assets/world/building/fans/`), its middle on
the hub and its edge on the rim, turned round, as a vehicle's is (TRN-13): no instance of its own. Every fan turns at
the view's one rate (`ViewFigures`), each building's from its own start and in its own look, both read off its ID and
the tick and nothing kept for it.

**OBJ-7** `P9` **A roof's chimneys smoke.** Where each chimney's pots are is read off its picture (`chimneys`, by eye with
`qq chimneys`). **Every chimney can smoke, and a share of them is burning at any one time**: each stack's fire is lit
or let out afresh turn by turn, off its own key and the clock and nothing kept, so the roofs that smoke change a few at a
time. A burning one puts out a slow plume that the town's wind (`Wind`) lays out downwind, thicker and thinner as its
fire is stoked and burns down; one lit grows its plume out of the pot, and one let out sees its last smoke carried
off. **A chimney is the pots its stack
carries**: whether it smokes is the stack's, and every pot of one that does puts out a plume of its own, so a twin stack
smokes as two thin plumes and not one wide one. **A wider pot puts out a thicker, wider and longer plume** than a
house's: how wide a roof's pots are is read off its picture with them (`potWidthM`), and a roof whose file says none has
a house's. Like a car's (CAR-54), the smoke
stands nothing, is drawn where the world is stepped on the device and nowhere else, and nothing is kept for it: a puff is
where the wind has carried it since it left the pot, read off the clock. What it looks like is `SimConfig.Chimneys`.

**OBJ-8** `P9` **Clouds pass over the town, and only their shadows are seen.** A cover of the sky's share is shade,
over everything that stands and under the interface, and darkens what is under it without changing its colour. The
clouds are carried the way the town's wind (`Wind`) blows about, at a speed of their own and following none of its
swings and gusts, and change shape as they go; a shadow's edge is blurred by as much as a cloud's height across the
sun's width blurs it. Like the smoke, they stand nothing and nothing is kept for them: where they are is the town's
seed and its clock, so a paused town's sky stands. A clear sky draws nothing. What they are is `SimConfig.Clouds`.

**Props and buildings are solid** — a car hits a tree — and anything static and numerous is priced
against **the queries it joins**, not against what it draws.

## Agents

**AGT-5** `P5` An agent in a terminal state — a broken car, and nothing else — performs no further actions. A
person knocked down takes no actions either, and is **not** terminal: an ambulance is coming for them
(PER-18), and a state something else can end is not one an agent is in for good.

**AGT-7** `P4` **An agent travels the ways the network laid it and nothing else**, and **every leg is
bounded**: what one does is one search, a chain of the town's own ways taken in turn, and the leg laid
again from wherever the body got to — the same code for both agent kinds at the tier where they are the
same thing (`World.Routing`). A leg that covers no ground for its own patience is given up, and there is
no other exit.

**A situation nothing covers is a leg given up, never a licence to improvise.** An agent lays no geometry
of its own: where it is not on the network it heads straight back onto it (PER-25, CAR-9), and where it
cannot, the clock ends the leg. What each agent kind adds to that is stated in its own slice —
[agents/car](../src/agents/car/docs/requirements.md) `CAR-15`, [agents/person](../src/agents/person/docs/requirements.md) `PER-25`
— and the errands above it (`AMB-*`, `SRV-*`, `EVA-*`) are legs with a place named rather than actions of
their own.
