# `P-4` — run the line

Code: [P04RunTheLine.cs](../planned/P04RunTheLine.cs) · [catalogue](index.md)

**Scenario.** There is road ahead and nothing else applies. This is the default, and **the state every
other manoeuvre comes back to** — which is why almost every other entry names it as its successor rather
than naming the plan.

**`Sa` — the state it starts in.** The whole body on drivable ground, with the route's line under it. A
car holding a template of its own instead asks the town for the lane it is standing on first: the pose a
manoeuvre ends in is not the pose the plan expected (MAN-3), and a car that could not be put on a lane
does not enter — which is what sends a car stranded on a verge to `E-8` rather than driving it with no
line.

**`Sb` — the state it delivers.** Whatever the road produced: a stop line, a junction, or the place the
line leaves the road for the way into the bay this leg holds. **A queue is not one of them** — a car held
by the road it was granted is running its line on a shorter road, which is this entry (S-2a) — and
**neither is a crossing**: the pace over the paint (CAR-7b) and the stop short of somebody on it
(TER-4c.1) are terms of the same profile, so a car at a zebra is on a shorter road too.

**Line.** None of its own. It holds the route's line, drawn over the lanes the plan says to take and
grown from its far end as the car eats it, so nothing already laid moves.

**Do.** Read this tick's own facts and name what the car is actually doing. Nothing here is a state
machine running beside the driving; it *is* the driving, named.

**Guards.** The standing rules, and nothing else. This entry imposes no limits of its own.

**Bounds.** The watchdog's blocked clock, which is what makes a car that stops making progress on an open
road reach the ladder — and the obstruction wait, which is the bound on standing behind one thing.

**Exits**, in the order they are asked.

| | Successor |
|---|---|
| the line was lost | keeps running — the standing rules re-acquire, and the blocked clock reaches the ladder |
| the line has left the road for the way into the leg's own bay, or has stopped at the mouth of one the car reverses into | the plan's next step — `P-14` |
| a stop point or the end of the line bound the speed | `P-6` |
| at rest, past the obstruction wait, in front of a **named obstruction** at rest with nobody exercising priority | `E-4` |
| the box ahead is within reserve distance and is this car's | `P-8` |
| at rest where the line runs out, on a stretch the leg comes back the other way from | `P-19` |
| the grant or the headway bound the speed | keeps running — **this is what queueing is** |
| the crossing term bound the speed | keeps running — **this is what slowing at a zebra is** (CAR-7b) |

**Why the exits are named off the binding term.** A car's speed is the minimum of everything that limits
it, and the term that won is the least ambiguous reading there is of what the car is doing. Each entry
this hands to then exits on the **fact** it is about and not on that term — see
[decision-log.md](decision-log.md).

**Why the obstruction wait is spent on the watchdog's clock.** This is the entry a car spends its whole
journey in, so time-in-entry says how long it has been driving and not how long it has stood. The blocked
clock is the one that stops when the car does, and a red ahead spends none of it.

**What is in front is asked what it is, not how long it has stood.** A live driver on this car's own road
is a queue however long it stands there, because whatever holds the car at its head is not this car's to
drive round; what eventually gets a car out from behind one that never moves is the blocked-road clock,
which is thirty seconds and not three. Only the lane index's **obstruction** — a wreck, a car with nobody
in it, a body off its own line — is `E-4`'s to act on, and a body the index cannot name is never gone
round either.

**Refs.** CAR-6.1, CAR-6.2, CAR-6.2b, CAR-7, MAN-2, MAN-3, S-2, S-2a, S-3.
