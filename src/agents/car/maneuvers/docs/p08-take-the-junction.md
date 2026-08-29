# `P-8` — take the junction

Code: [P08TakeTheJunction.cs](../planned/P08TakeTheJunction.cs) · [catalogue](index.md)

**Scenario.** The route crosses a junction. **One entry for all three movements** — ahead, the near-side
turn, and the turn across the oncoming stream — because which one is being made is a fact about the
route and the geometry for it was drawn with the rest of the leg.

**`Sa` — the state it starts in.** The box the car's own line enters is within the reserve distance and the
**ground through it** is this car's: the crossing taken, or the body already inside it.

**`Sb` — the state it delivers.** The body out the far side, on the lane the route wanted, with the ground
given back and the next box further off than a car reserves at.

**Line.** The route's. The turn is lanes and the join between them, laid by the assembler.

**Do.** Drive the line. The claim is made and given back by the standing rules on every tick (S-4),
because a red is what actually refuses a car a junction and it can change under one.

**Guards.** The crossing held. A movement begun is finished: a car stopped mid-box is standing on
everything its own way through is driven over.

**What is taken is ground and not a permission** (TER-5c;
[`WayCrossings`](../../../../world/road/WayCrossings.cs) is the table and the rule is stated
there). **This entry adds nothing on top of it** — what the car takes is a stretch of the way it is itself
driving, in the same book as every other stretch of road, and it is refused by whatever is standing on the
metres it wants. So two cars going straight on opposite arms take the junction together, and so does a
queue making the same movement.

**And it takes nothing on the ways it is driven over** (TER-5c.1). Where its own way crosses another, the
table says which metres of which other way to ask about, and the driver reads them where they lie. A car
approaching a box therefore reserves the road it is going to be on and nothing else.

**Bounds.** The short fuse, because a car inside a box **is** the obstruction and patience is the wrong
answer there.

**Exits.**

| | Successor |
|---|---|
| the body is out and the next box is beyond the reserve distance | `P-4` |
| the ground was given back before the car committed | `P-6` (failure) — stop at the boundary |

**When it is given back** is the standing rules' answer and not this entry's (S-4): only a car nothing but
the box is holding up takes a crossing, it gives one back the moment something else holds it up, and past
the point it could have stopped at it keeps it whatever anything says. What the entry has to survive is
that the exit to `P-6` can therefore fire under it on any tick.

**What is deliberately *not* here.** Nothing is added on top of a green. Where a signal has already
decided whose turn it is, a second gate — an exclusive claim on the whole box, a crawl over the paint —
does not make the junction safer, it makes the phase useless: a box that takes one car at a time is a
queue crossing on green in single file. A phase greens arms that are not driven over each other, so a car
on a green is refused only by ground the phase never spoke about. What stays standing besides is
everything that is not a duplicate of the signal — the headway reading, the stranded-in-the-box refusal,
the stop line, and the yield to anybody on the paint.
**Before adding a rule that slows a car at a junction, ask what has already refused the movement it is
guarding against** (SIM-7).

**Why it is not scheduled.** Negotiating with traffic doing eleven metres a second. A tenth of a second
of staleness is a gap that had already closed, and priority is not something to be approximately right
about.

**Refs.** CAR-6.3, CAR-7, CAR-7a, TER-5, SIM-7, S-4.
