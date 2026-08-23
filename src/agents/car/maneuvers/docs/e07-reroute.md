# `E-7` — reroute

Code: [E07Reroute.cs](../reactive/E07Reroute.cs) · [catalogue](index.md)

**Scenario.** The **road**, not the destination, is the problem. Reached as **rung 5 of the ladder**.

**`Sa` — the state it starts in.** On a route, with a reroute left on this leg, and a stretch ahead the
network actually has a link for.

**`Sb` — the state it delivers.** The blocked stretch priced up in the town's own table, the route in
hand dropped, and the car back on `P-4` — which will have the search draw a new one over the new prices.

**Line.** None of its own.

**Do.** Mark the stretch the car is blocked **entering** — the lane after the one under it, or the one
under it where the line goes no further — then clear the route.

**Guards.** None.

**Bounds.** Three per leg. Past that the road has stopped being the thing that is wrong with this leg.

**Exits.**

| | Successor |
|---|---|
| the way is marked | `P-4` |
| no route, or no reroutes left | the next rung of the ladder |

**Expensive, never impassable.** In a town this small the only road to a place may be the marked one, so a
blocked stretch is **priced** rather than banned (SIM-6).

**The mark expires and is never swept.** Nothing unmarks a road by inspection, so a stretch that is still
blocked is marked again by whoever finds it so — and one that has cleared is tried again by whoever comes
along after the mark has died.

**Everybody benefits.** The price goes on the town's table and not in this car's pocket, which is the
whole difference between a car that avoids a jam and a town that routes around one.

**Why a refusal must not go back to `P-4`.** `E-7` refused for having already rerouted twice, handed back
to `P-4`, replans the same blocked road, jams on it and asks for `E-7` again — for the rest of the run,
without ever climbing high enough to reach a rung that ends the leg. A refused recovery takes the **next
rung** and never the plan.

**Refs.** SIM-6, MAN-3.
