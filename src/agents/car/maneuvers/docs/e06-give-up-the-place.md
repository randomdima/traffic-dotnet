# `E-6` — give up the target place

Code: [E06GiveUpThePlace.cs](../reactive/E06GiveUpThePlace.cs) · [catalogue](index.md)

**Scenario.** The **destination**, not the manoeuvre, is the problem: the bay this leg holds cannot be
reached, or cannot be driven into from anywhere the car can get to. Reached as **rung 4 of the ladder**,
and as the successor of a park attempt whose geometry was refused.

**`Sa` — the state it starts in.** There is a place to give up, **and** the town can find another to take:
a free bay within walking distance of where the car has actually got to, whose own approach lane a route
exists to.

**`Sb` — the state it delivers.** The old place back on the market, a new one booked, the route in hand
dropped, and the leg's chain re-derived to aim at it.

**Line.** None of its own.

**Do.** **Release before taking** — a place held by a car that has gone elsewhere is a place removed from
the town — then book, then replan.

**Guards.** None.

**Bounds.** The booking itself: a car that can book none keeps driving rather than standing in a lane, and
that is the refusal this entry returns.

**Exits.**

| | Successor |
|---|---|
| the new place is booked | `P-4` |
| there is nowhere to book | the next rung of the ladder |

**Why the route is checked before the booking.** A bay booked without a route to its approach lane is the
same jam again with a different postcode.

**Why the whole entry is one tick.** The manoeuvre *is* the booking. It is still an entry rather than a
branch in a controller, because a leg that changes its destination is a thing worth being able to count.

**Refs.** CAR-8, CAR-9a, GEN-4, MAN-3.
