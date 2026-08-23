# `P-6` — hold at a line

Code: [P06HoldAtALine.cs](../planned/P06HoldAtALine.cs) · [catalogue](index.md)

**Scenario.** There is a **place** ahead the car may not pass: a red, a painted bar, a junction box it was
refused, or the end of the line it was given. Come to rest short of it, wait, pull away when it has gone.

**`Sa` — the state it starts in.** A stop point somewhere ahead on the line, a light showing this
approach anything but green, or the end of the line within a car length.

**`Sb` — the state it delivers.** The car moving again on the same line, with the thing it stopped for
behind it — or, at the end of the leg's last lane, a car at rest exactly where the bay's own template is
staged from.

**Line.** The route's, unchanged.

**Do.** Nothing of its own. The reservation pass and the speed profile between them already stop the car
at the bar; what this adds is that a car doing it has a **name**, so the watchdog can tell a car waiting
out a light from a car that has stopped for no reason at all.

**Guards.** None of its own.

**Bounds.** A car queueing at a light spends neither the blocked clock nor the stuck one — it is doing
exactly what the light asked. Everything else it might be holding for does spend the blocked clock, and
that is what reaches the ladder.

**Exits.**

| | Successor |
|---|---|
| at rest at the end of the leg's last lane | the plan's next step — `P-14` |
| nothing left to hold at **and the car is moving again** | `P-4` |

**Why the exit follows the body and not the line.** A car at rest at a junction is bound by the box one
tick and by the queue in front the next, and neither means the thing it stopped for has gone. This entry
imposes nothing, so the profile pulls the car away the moment it may — and taking the exit from the body
moving is what stops `P-4` being handed a stationary car a hundred times in one spot.

**Why it is not scheduled.** Braking to a line is a closed loop on an error. It looks perfectly safe to
schedule, because the car is stopping anyway and the reservation pass holds it at the bar every tick
regardless — and scheduling it was still measured to push the front of the queue nearly twice as far back
from the paint.

**Refs.** CAR-6.3, CAR-6.4, TLT-2, S-2, S-5.
