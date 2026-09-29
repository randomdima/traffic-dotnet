# The traffic light agent — requirements

The third agent kind, and the simplest: a timer that holds ground. Everything difficult about a junction
lives in the reservations its light is one more holder of — what a movement takes off another, and who
keeps a piece of ground, is [world/road](../../../world/road/docs/claims.md); a bundle is timed on **axes**
and never reads that table. How the cycle is kept and asked is `SignalCycle`'s and `SignalService`'s own,
and how a hold is laid is `SignalHolds`'.

## The rules

**TLT-1** `P5` A traffic light is a **timer-driven agent** that cycles signal states for the directions of one
intersection, and **what it does to the town is hold ground**: while an approach is not showing green, a
secondary claim over that approach from the near edge of its bar to the mouth of the box; while a crossing
is showing red, one over the paint of that crossing, from either kerb. **It takes no input from traffic** —
no detection loops, no demand, no adaptive timing.

- **It holds at a rung of its own** (TER-5g): above a walker and every movement a box admits, and below a
  closed road and a call. So a police car closing a road and anything answering a call go through, and
  ground its holder can no longer stop short of beats it as it beats every rung.
- **It is a secondary claim** (TER-5c.1): placed on the way it governs and following no mark, it meets the
  main claims laid on that way and no secondary claim placed there. A light holding an approach costs
  nobody the zebra across it, and one holding the zebra costs no car the lane under it.
- **It ends at the first body travelling the way it holds** (TLT-2a): it runs from the bar or the kerb to
  the first body going down that way in front of it — a car on its own lane, a walker on the paint it walks.
  A body only standing on the way, a car across the paint or a walker across the lane, cuts nothing of it:
  the hold lies over it whole, and whoever asks that way is stopped at the body itself (TER-4c.1).

**TLT-2** `P5` Signals are published per direction and **read by no agent**: a car is held by the light's
claim on its lane and a walker by its claim on the paint, each answered against it like any other plan
(TER-4c.1). A car signal has three states: green, **amber** — the last stretch of its own green, during
which the approach is held as it is on red — and red. A pedestrian signal has two, because "do not *begin*
crossing" already carries the whole of the warning, and a crossing shows green only against a road that is
**fully red**, so a walker is never held by an amber.

**TLT-3** `P6` An intersection carries **at most one light bundle, and only if it admits conflicting
movements** (TER-5c), which is read off the shape of the junction rather than taken on trust from the map:
three arms or more, and neither a roundabout's ring (GEN-19) nor a car park (GEN-53). **Of those, a town
lights the share its brief asks for, to the junction, drawn once from the world seed and weighted by the
movements each admits** — so a crossroads is lit more often than a tee, and no tee is out of the draw. What
is left is ranked (TER-5e). **A crossing does not qualify one on its own**: an intersection of fewer than
three arms admits no crossing car movements, so a dead end and an inline junction (TER-5b) carry no bundle,
and the crossing an inline junction exists for is an **uncontrolled** one — governed by the walker's right
of way over the traffic (TER-5e) rather than by a phase. So is a zebra painted midway along a road
(WLK-10a), which stands at no junction's arm. Each bundle's initial phase offset is drawn from the world
seed.

**TLT-4** `P3` A bundle shares a single cycle whose phases green an **axis** rather than a list of directions,
so **conflicting greens are impossible by the shape of the table** rather than by a runtime check, and
both ends of a road always show the same colour. **There is no all-red phase**: the box is emptied by the
amber tail and by yielding, not by a clearance interval.

## What obedience means

**TLT-2a** `P5` A light governs the traffic **outside** the box. Three things follow, and they are the whole
of it:

1. **No car begins on anything but green**, and amber is not green: a car that can still stop short of the
   bar is refused it, and stops there.
2. **A car that has already started finishes**, whatever the light does under it. **The test is
   positional and never predictive** — a car with any of its body past the bar's near edge, or inside the
   box, is a body the light's claim ends at, and a car that can no longer stop short of the bar holds ground
   the light does not take. **How it got there is not asked**: a car put over the line by a shunt has
   started like any other. A walker already on the paint when its crossing turns red walks off it the same
   way.
3. **A red, and the queue standing at one, are not obstructions.** They are traffic doing what this agent
   is about to do, so nobody overtakes them or spends a patience clock on them. In particular **lights
   never enter pathfinding**: a wait at a light's hold may not mark a road blocked.

## The heads

A bundle is both the agent and the visual. A car head stands a fixed distance past its arm's stop bar on
the bar's own centre line — the middle of the approaching lane, and so on the tarmac; a pedestrian head
stands at the near-left corner of every governed crossing, one for each direction it is walked. Every head is upright and square to the arm it
governs. **Each car head shows exactly one lit lamp** — never two, never none. Heads facing opposite arms
of the same axis show the same colour, and where a car head is green the pedestrian head for the crossing
over that arm is red.
