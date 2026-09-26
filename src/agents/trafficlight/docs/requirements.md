# The traffic light agent — requirements

The third agent kind, and the simplest: a timer that publishes colours. Everything difficult about
junctions lives in the drivers who read it. What a movement takes off another is
[world/road](../../../world/road/docs/claims.md); a bundle is timed on **axes** and never reads that
table. How the cycle is kept and asked is `SignalCycle`'s and `SignalService`'s own.

## The rules

**TLT-1** `P5` A traffic light is a **timer-driven agent** that cycles signal states for the directions of one
intersection. **It takes no input from traffic** — no detection loops, no demand, no adaptive timing.

**TLT-2** `P5` Signals are **published per direction** and read by cars. **A walker reads no signal**
(PER-25): the pedestrian side is asked by the heads' picture and by nothing that moves. A car
signal has three states: green, **amber** — the last stretch of its own green, during which the box may
no longer be taken — and red. A pedestrian signal has two, because "do not *begin* crossing" already
carries the whole of the warning, and a crossing shows green only against a road that is **fully red**,
so a walker is never shown an amber to interpret.

**TLT-3** `P6` An intersection carries **exactly one light bundle if and only if it admits conflicting
movements** (TER-5c), which is read off the shape of the junction rather than taken on trust from the map.
**A crossing does not qualify one on its own**: an intersection of fewer than three arms admits no crossing
car movements, so a dead end and an inline junction (TER-5b) carry no bundle, and the crossing an inline
junction exists for is an **uncontrolled** one — governed by the walker's right of way over the traffic
(TER-5e) rather than by a phase. Lit instead, a mid-block zebra holds a street on a timer nothing on it is
waiting for. **Placement is not randomised**; each bundle's initial phase offset is drawn from the world
seed.

**TLT-4** `P3` A bundle shares a single cycle whose phases green an **axis** rather than a list of directions,
so **conflicting greens are impossible by the shape of the table** rather than by a runtime check, and
both ends of a road always show the same colour. **There is no all-red phase**: the box is emptied by the
amber tail and by yielding, not by a clearance interval.

## What obedience means

**TLT-2a** `P5` A light governs the traffic **outside** the box. Three things follow, and they are the whole
of it:

1. **No car begins on anything but green**, and amber is not green.
2. **A car that has already started finishes**, whatever the light does under it. **The test is
   positional and never predictive** — a car with its rear axle past the bar's near edge or inside the
   box. Stopping there is worse for everyone, including whoever has the green. **How it got there is not
   asked**: a car put over the line by a shunt has started like any other, since nothing but a bay's own
   way is ever driven backwards (CAR-6.5).
3. **A red, and the queue standing at one, are not obstructions.** They are traffic doing what this agent
   is about to do, so nobody overtakes them or spends a patience clock on them. In particular **lights
   never enter pathfinding**: a signal wait may not mark a road blocked.

## The heads

A bundle is both the agent and the visual. A car head stands a fixed distance past its arm's stop bar on
the bar's own centre line — the middle of the approaching lane, and so on the tarmac; a pedestrian head
stands at the near-left corner of every governed crossing, one for each direction it is walked. Every head is upright and square to the arm it
governs. **Each car head shows exactly one lit lamp** — never two, never none. Heads facing opposite arms
of the same axis show the same colour, and where a car head is green the pedestrian head for the crossing
over that arm is red.
