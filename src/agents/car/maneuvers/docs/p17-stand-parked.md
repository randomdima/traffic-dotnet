# `P-17` — stand parked

Code: [P17StandParked.cs](../planned/P17StandParked.cs) · [catalogue](index.md)

**Scenario.** The car is in the bay. The leg is over.

**`Sa` — the state it starts in.** At rest in the bay this leg was aiming at.

**`Sb` — the state it delivers.** The bay marked **occupied**, the handbrake holding, every claim
released, no line and no route in hand, and whoever was driving handed back to their trip on foot.

**Line.** None.

**Do.** Take the occupancy, hold still, end the leg.

**Guards.** None.

**Bounds.** None — and it is the one entry that needs none, because standing still *is* the procedure.

**Exits.**

| | Successor |
|---|---|
| always, on its first tick | the leg is finished: **parked** |

**Why it is a manoeuvre and not the absence of one.** Standing still is what a car does most of the time
in a town, and **a state nothing names is a state no instrument can count and no watchdog can exempt** —
which is how a parked car ends up being escalated for not making progress. It is also where the leg's
result is read: the squareness this leg achieved is taken at the moment this entry is taken up and never
at the end of a run, because a parked car later shoved by traffic is not a parking result.

**Refs.** CAR-1, GEN-4, VER-3.
