# The traffic light agent — requirements

The third agent kind, and the simplest: a timer that publishes colours. Everything difficult about
junctions lives in the drivers and walkers who read it. What a movement takes off another is
[world/road](../../../world/road/docs/requirements.md); a bundle is timed on **axes** and never reads
that table.

## The rules

**TLT-1** A traffic light is a **timer-driven agent** that cycles signal states for the directions of one
intersection. **It takes no input from traffic** — no detection loops, no demand, no adaptive timing.

**TLT-2** Signals are **published per direction** and read by both car agents and person agents. A car
signal has three states: green, **amber** — the last stretch of its own green, during which the box may
no longer be taken — and red. A pedestrian signal has two, because "do not *begin* crossing" already
carries the whole of the warning, and a crossing shows green only against a road that is **fully red**,
so a walker is never shown an amber to interpret.

**TLT-3** An intersection carries **exactly one light bundle if and only if it admits conflicting
movements** (TER-5c); a crossing on an intersection always qualifies. **Placement is not randomised**;
each bundle's initial phase offset is drawn from the world seed.

**TLT-4** A bundle shares a single cycle whose phases green an **axis** rather than a list of directions,
so **conflicting greens are impossible by the shape of the table** rather than by a runtime check, and
both ends of a road always show the same colour. **There is no all-red phase**: the box is emptied by the
amber tail and by yielding, not by a clearance interval.

## The cycle

One cycle length shared by both phases, so each axis gets half of it. The **amber is the last stretch of
a green**, not time added to it. The phase is derived from a global clock plus the junction's own offset,
so there is no per-bundle state to drift. **A crossing's signal is the negation of its own road's**:
green exactly while that road is fully red, amber included.

The table's *shape* is the safety argument, so the test that matters asserts, across a whole cycle at a
crossroads, that no two conflicting directions are ever green together and that both ends of one road
always agree.

## What obedience means

**TLT-2a** A light governs the traffic **outside** the box. Three things follow, and they are the whole
of it:

1. **Nobody begins on anything but green.** For a car, amber is not green; for a walker, anything but
   green is not green.
2. **An agent that has already started finishes**, whatever the light does under it. **The test is
   positional and never predictive** — a car with its body over the bar or inside the box, a walker
   already on the paint. Stopping there is worse for everyone, including whoever has the green. **A car
   *put* over the line by a shunt has not started** and returns behind the paint.
3. **A red, and the queue standing at one, are not obstructions.** They are traffic doing what this agent
   is about to do, so nobody overtakes them, steps round them, or spends a patience clock on them. In
   particular **lights never enter pathfinding**: a signal wait may not mark a road blocked.

## The one town-wide lookup

Cars and walkers query **one signal service**, and **neither ever holds a bundle**. The bundle is the
timer-and-visual; the service is what "what colour is my approach, my crossing" is asked of.

Keeping it one lookup is what makes the driver's crossing exemption safe: a driver reads the *pedestrian*
side of the same table to know that the walkers are being held, so what a driver may do and what the
people on the kerb have been told can never disagree.

## The heads

A bundle is both the agent and the visual. A car head hangs **beside** the carriageway on the approach it
governs, placed a fixed distance past that arm's stop bar and never out on the tarmac; a pedestrian head
stands at the near-left corner of every governed crossing. Every head is upright and square to the arm it
governs. **Each car head shows exactly one lit lamp** — never two, never none. Heads facing opposite arms
of the same axis show the same colour, and where a car head is green the pedestrian head for the crossing
over that arm is red.
