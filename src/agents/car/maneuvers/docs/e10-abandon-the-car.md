# `E-10` — abandon the car

Code: [E10AbandonTheCar.cs](../reactive/E10AbandonTheCar.cs) · [catalogue](index.md)

**Scenario.** Nothing else is available. **Rung 8, the last one, and the entry the whole ladder is finite
by.**

**`Sa` — the state it starts in.** None, and there may never be one. Every other entry in the catalogue
may refuse; this one may not, because a car that every rung has refused would otherwise be a stuck agent
for the rest of the run.

**`Sb` — the state it delivers.** The car stopped and held wherever it is, every claim released, the
reserved bay back on the market, the leg over, and the driver out on foot.

**Line.** None.

**Do.** Give up the reservation, hold, end the leg.

**Guards.** None.

**Bounds.** None.

**Exits.**

| | Successor |
|---|---|
| always | the leg is finished: **abandoned** |

**An abandoned car is no longer an agent** (CAR-1) and is town furniture until somebody else drives it
away. It is exempt from the stuck-agent check for exactly that reason: it is not stuck, it has stopped
being a driver.

**How this differs from `E-9`.** `E-9` requires somewhere lawful to stop and leaves the car parked badly;
this one leaves it wherever it is, including across a lane. That is a worse outcome for the town, which
is why it is last — and it is still a defined outcome, which is the point.

**A high count is a finding.** Cars abandoning legs means the ladder above this rung is not working, and
`--bench maneuvers` reports the figure beside the rest.

**Refs.** CAR-1, CAR-9a, MAN-4, MAN-5, VER-3.
