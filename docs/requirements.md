# Requirements — the cross-cutting rules

**Only what belongs to no single slice is here.** Everything else lives with the code it governs, and
[index.md](index.md) maps every requirement ID to the document that owns it.

**A rule states a relation. A number is not a rule** — every figure is on `SimConfig`
([core](../src/core/docs/requirements.md#where-a-figure-lives)). **No ID is ever renumbered** and a retired
number is never reused, because the code cites these codes.

`†` formalises a consequence of the original brief; `‡` is a design decision that could have gone another
way.

## Purpose and scope

**What this project is for, what it refuses to be, and the bar every judgement call is settled against
are [goals.md](goals.md)** — stated there and nowhere else, because a purpose written twice is two
purposes within a month.

**TEC-1** The spec constrains no engine, language or renderer; this project settles it as C# on .NET 10
with nothing under it ([goals.md](goals.md)).

**TEC-2** The physics layer provides collision, contact resolution **and motion integration**. Every
dynamic body is a rigid body driven only by traction-limited friction and drive impulses; pushes,
stopping and terrain effects are **solver output, never scripted displacement**. Motion is continuous and
vector-based: no grid quantisation of position, velocity or heading anywhere.

## The two rule classes

**SIM-1** Two classes exist and **must stay distinct in the implementation**:

| | Hard rules | Soft rules |
|---|---|---|
| What | Physical constraints | Traffic rules |
| Enforced by | The physics layer | The agent's own intent |
| Violable | No | Yes |
| On violation | Cannot happen | A defined **recovery behaviour**, never an engine-level correction |

Hard rules live in the physics layer and the damage resolver only. **Soft rules never touch the physics
layer**: nothing may teleport, snap, clamp or nudge a body because it is somewhere illegal.

**SIM-2** Every dynamic body carries at least: position, heading, velocity, the terrain it currently
occupies, and its liveness state.

**SIM-6** A soft rule binds in exactly one of two ways, and **which one is part of the rule**:

- **As a ban**, where the agent *chooses* — in route planning, target selection, plan derivation. A
  banned option is not costed, it is **absent**.
- **As a price**, where the agent merely *prefers*. A price is outbid by distance and is meant to be.

**A ban lifts only where keeping it would leave the agent with no route at all** — being stranded is
worse than being illegal — and it lifts for that agent, on that plan, never in general. Where a ban is
lifted the *manner* of the act is unchanged: it is still performed by the manoeuvre that owns it, with
that manoeuvre's own guards. **Hard rules never lift**, in planning or in recovery.

**SIM-7** Where a decision has already been taken by one mechanism, **no second mechanism may guard it**.
A duplicate gate does not make the town safer; it makes the first mechanism useless. **Before adding a
check that refuses a movement, name what has already refused it.** The measured symptom is in
[decision-log.md](decision-log.md).

Units, the two seeds, the tick and the decision clock are [core](../src/core/docs/requirements.md) —
`SIM-3`, `SIM-4` and `AGT-6` live there.

## The object catalogue

**OBJ-2** Five kinds, two shapes:

**One shape**, an oriented box with its corners rounded, and the five kinds are what they set it to:

| Object | Shape | Kind | Contains |
|---|---|---|---|
| Prop | small disc — a radius with no box | static | — |
| Traffic light | small disc | static | — |
| Building | one or more square-cornered rectangles | static | 0..capacity persons |
| Person | small disc | dynamic | — |
| Car | one rounded rectangle | dynamic | 0..1 driver |

**A disc is not a second shape**: it is the same rounded box with nothing in the middle of it, and the
solver holds one shape and one narrow phase for all five (`SOL-1`).

**A car collides as a shape fitted inside its picture** and not as the footprint that picture was drawn
in (`CAR-12b`). A per-variant hull is still not what the town is laid against.

**OBJ-4** A building exposes at least one point on walkable terrain through which persons enter and exit
([world/containment](../src/world/containment/docs/requirements.md)).

**OBJ-5** Building capacity scales with footprint.

**OBJ-5a** **A building is collided as the rectangles its roof is drawn of and not as the box that roof
was drawn in.** An L, a courtyard, a cut corner and a porch are all one defect otherwise — metres of
empty box that stop a car in the open — and the parts are the picture's own, so what is drawn and what
is stood are one answer and cannot drift apart.

**Props and buildings are solid** — a car hits a tree — and anything static and numerous is priced
against **the queries it joins**, not against what it draws.

## Agents

**AGT-5** An agent in a terminal state — a broken car, and nothing else — performs no further actions. A
person knocked down takes no actions either, and is **not** terminal: an ambulance is coming for them
(PER-18), and a state something else can end is not one an agent is in for good.

**AGT-7** Everything an agent does is a **named manoeuvre from a closed catalogue** per agent type, every
failure exit names its successor, and every entry is bounded by time, distance or attempts. **A situation
the catalogue does not cover is a gap in the catalogue, never a licence to improvise.** A stuck agent
walks **one** ordered escalation ladder that ends, always, at "get as close as your actions allow and take
the goal you actually reached".‡

**An entry is a file, and its contract is a page.** The driver's catalogue is
[agents/car/maneuvers/](../src/agents/car/maneuvers/docs/index.md): one file per entry holding its `Sa`, its
procedure and its exits, one page per entry saying when it is the right thing to do and what state it
leaves the car in, and one dispatch that the two are wired through. **The walker has none yet.**
