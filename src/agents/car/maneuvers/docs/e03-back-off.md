# `E-3` — back off

Code: [E03BackOff.cs](../reactive/E03BackOff.cs) · [catalogue](index.md)

**Scenario.** The car is jammed and the cheapest change of state available is to make room along its own
axis and re-decide from the new distance. Reached as **rungs 1 and 3 of the ladder**.

**`Sa` — the state it starts in.** All four of:
- an attempt left on this jam;
- **something to back away from** — one of four states: something in the way **that had no business being
  there**, a boundary the car may not cross **while it is itself standing across a lane**, a template it can
  no longer follow, or a line it has lost;
- drivable ground behind the car, **walked** step by step with the whole body tested at each one, never
  assumed;
- and the swept path over ground nobody else has taken, walked **in the gear it will be driven in** — and
  then **held for as long as the straight is driven** (`TER-4c.1`), on every way it runs over.

**`Sb` — the state it delivers.** The car at rest, a car length or more from where it jammed, on drivable
ground, with the plan re-derived from there.

**Line.** One straight along the car's own axis, in whichever direction the jammed manoeuvre was **not**
going — so a reversing template that jams is got out of forwards. Drawn for the rear axle and driven by
the same follower, at manoeuvring pace, never past the reverse bound.

**Do.** Drive it to the end and hand back.

**Guards.** The line stays a template.

**Bounds.** Two attempts per jam, and the reverse bound on the distance.

**Exits.**

| | Successor |
|---|---|
| the straight is driven | `P-4`, which takes the lane under the car and re-derives |
| the line is no longer a template | `P-4` (failure) |

**Room made is room kept.** What the car does at the end of the straight is run the decision that failed
again **from a different place**, which is the plan re-derived and not the same choice made twice.

**Why the ladder offers it twice.** Rungs 1 and 3 are not a repeat: what the second one buys is the
**fuse between them**. A jam that has had another watchdog's worth of time to change is a different jam,
and deleting the rung was measured and moved the wrong way.

**Why the `Sa` is so fussy.** A clock cannot tell "cannot go forward" from "waiting to". The fault the
first guard fixes was cars reversing away from empty intersections while yielding perfectly correctly;
the fault the second fixes was cars reversing into whatever was behind them.

**Traffic is not something to back away from.** A driver queueing behind another driver, or held by ground a
crossing movement has claimed, is waiting for something that is going to move: reversing neither clears the
queue nor changes the decision about to be re-taken from further back, and the road it reverses into is road
the traffic behind is entitled to be standing in. What is left of the boundary door is the case where the car
is **itself** the obstruction — standing in a box or across a lane on a template — because the ground it is
backing out of is then the ground it is blocking.

**Why the swept path is held and not only walked.** Ground checked at the moment the straight was drawn is
ground everybody else still reads as free, and a straight takes a second or two to drive: a car came up
behind, was granted road that ran through the sweep, stopped in it, and was reversed into at manoeuvring
pace. The line is written into the book from the body every tick for as long as it is being driven, so what
refuses that car is the same book that refuses everything else, and there is no second mechanism (`SIM-7`).

**Why the reading is taken in the new gear.** What is in front is only as good as the gear and the line it
was read on, and it needs a moment to settle after either changes.

**Refs.** CAR-6.5, CAR-9, MAN-4, S-3.
