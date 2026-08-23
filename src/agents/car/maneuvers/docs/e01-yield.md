# `E-1` — yield

Code: [E01Yield.cs](../reactive/E01Yield.cs) · [catalogue](index.md)

**Scenario.** Another agent is entitled to be where this car wants to go, and waiting for it is the
correct answer. Reached as **rung 0 of the ladder** — that is, once the watchdog has noticed that this
car has been standing still, and has asked what for.

**`Sa` — the state it starts in.** Something with priority is in the way: a light showing this approach
anything but green, a box somebody else holds, or a body ahead that is itself moving.

**`Sb` — the state it delivers.** The obligation discharged and the car moving again, handed back to
whatever it was doing.

**Line.** None of its own. The car keeps whatever line it had.

**Do.** Nothing. The wait is already what the speed profile is doing; what this entry adds is the
**name** and the **bound**.

**Guards.** None of its own.

**Bounds.** The blocked clock. Waiting for a junction somebody else is in is correct right up until it has
been correct for half a minute, and after that it is a jam rather than traffic.

**Exits.**

| | Successor |
|---|---|
| the obstruction has gone **and the car is moving again** | resume the suspended entry, through its own `Sa` |
| the blocked clock runs out | the ladder, from the rung it stands on |

**Why it keeps the name until the car moves.** Not until the profile stops naming the term: a yield
discharged the moment another constraint won the minimum would be a yield nobody could see, and a car at
rest at a junction is still yielding whichever of its constraints happens to bind this tick.

**Why an idle here is lawful.** Yielding to another agent that blocks the path is legitimate idling and is
the normal way cars resolve conflicts (CAR-7). It is one of the three exceptions to the no-idling rule,
and the only one that is bounded by a clock rather than by a place.

**Refs.** CAR-6.4, CAR-7, CAR-7a.
