# `E-4` — go round what is in the way

Code: [E04GoRound.cs](../reactive/E04GoRound.cs) · [catalogue](index.md)

**The only overtaking that exists in this town.**

**Scenario.** `P-4` is held by something the lane index names as being **in the way** — a wreck, a car with
nobody in it, a body shoved off its own line, **somebody in the carriageway** — it has put up with it for
longer than a driver waits, and nobody with priority is asking this car to stay put. Reached from `P-4` and
never from the ladder: it is **discretionary** (row 5).

**`Sa` — the state it starts in.** On a route, with something within sight ahead that is **worth going
round** ([`DriveScene.WorthGoingRound`](../framework/DriveScene.cs)), and a swerve the **ground** and the
**book** both admit. On taking up, the stretch of its own lane the swerve leaves and returns to is
**claimed**, so the traffic behind reads that ground as taken rather than as empty road.

**`Sb` — the state it delivers.** The car back on the line it left, pointing the way it was pointing, with
the obstruction behind it.

## Worth going round

One reading, and `P-4` names this entry off the same one — an entry that asked the question again in its
own words is an entry `P-4` hands a car that then refuses it, which is a pair passing a car to and fro in
one spot for as long as the obstruction lasts.

**Two doors, because the two things that can be in the way are not the same situation. Both spend the
obstruction wait**, since something in the road may be about to be gone and a body crossing a carriageway is
out of the lane in a second. What differs is the clock that can answer:

| what is in front | the wait is spent on | and also |
|---|---|---|
| **stopped** | the blocked clock, standing still behind it | — |
| **moving** | `HeldBackS`, held below what the road affords | it is under `PassWorthShare` of this car's own pace, and this car is gaining on it |

A car behind something slow never stands still, so no clock it keeps by standing still ever runs out. That
is the whole reason the second one exists.

## Line

An **S out**, a run past what is in the way, and the **mirror S back**: four arcs of equal angle and the run
between them, which carries the line sideways and leaves it parallel to where it started.

- **The side is tried, not assumed** — the centreline first, because that is the side CAR-6.2b licenses,
  and the verge only if the ground on the other side refuses.
- **A lane over, not a body's width.** The book carries what is in the way as a stretch of arclength and
  never as a place across the road, so where in its lane it stands is a fact nothing here can read; moving
  over by the lane's own width clears anything the lane can hold. The narrow shift is the last thing tried,
  for a road with no room for the wide one.
- **Drawn for the speed the car is doing.** The profile reads a template's arcs exactly as it reads a
  road's, so a swerve laid at the steering lock is a 6 m/s manoeuvre whatever the road affords — and a car
  that has slowed to 6 m/s cannot get past a body walking at 6.6. The radius is what the speed asks for
  (`SimConfig.CarCorneringRadiusM`), floored at the lock for a car starting from rest.
- **Drawn on top of the road's own bend.** Every piece carries the curvature under the car, so what the
  shape *does* is measured against the arc the car was on rather than against the plane. Flat, it is a
  chord: fifty metres of it on a forty-metre radius is ten metres off the carriageway.
- **The run past is the ground the closing speed needs.** `clear · v ⁄ (v − u)` — the static gap again
  wherever `u` is zero, and a road nobody has as `u` approaches `v`.

**Do.** Lay it, walk it, ask the book whose the ground under it is, drive it.

**Guards.** The line stays a template. The route, the progress measure and the junction claims are
untouched throughout.

**Bounds.** **There is no attempt budget.** Every swerve costs the obstruction wait, and every swerve is
laid only over ground the walk found clear — which is what bounds the wrong side of the road, and bounds it
by the road rather than by a count. A count could not: a car that has spent it stands at the next
obstruction until it gives the journey up, and a car that cannot move cannot earn one back on any measure
either. A road with something in the way every two hundred metres is a road a driver genuinely spends on
the other side of.

**Exits.**

| | Successor |
|---|---|
| the swerve is driven | `P-4` |
| the line is no longer a template | `P-4` (failure) |
| the geometry, the ground or the book refuses | back to `P-4` via the successor rules; the blocked clock carries on and the ladder still climbs |

**It is a change to the line and never to the route** (MAN-7). A driver does not re-route to get round
something in its own lane.

**The lateral shift is a function of distance and never of time**, which is why it is laid as geometry
rather than as an offset that grows on a clock. A line that moves on a clock arrives whether or not the car
did, and steers the car into the thing it was avoiding.

## Only on a road segment, never at a junction

**Overtaking is a manoeuvre of a carriageway.** A car may not begin one standing in a junction box, nor once
the box ahead is near enough to have been asked for, nor where the pass would not be finished before
reaching it. Three statements of one rule, and it holds for a car answering a call exactly as for anybody
else — a blue light buys the road, and there is no road here to buy.

**A junction has no centreline to cross.** What licenses the wrong side of the road at all is CAR-6.2b, which
licenses crossing the **centreline**; a box has none. The ground beside a car in a junction is not an oncoming
lane, it is the other movements through the box — and every one of them was arbitrated on the town's table of
what is driven over what (`TER-5c`), which assumes a crossing car follows the join it claimed. A car that
swings off its join is not where the town says it is, and both movements then read the wrong ground.

**And it is the one place the swerve's own claim cannot be laid.** What holds the traffic behind off the
ground the shape swings through is a claim on the stretch of the car's own **lane** it leaves and returns to;
inside a box the car is on no lane, so that claim is silently never made and the ground reads as empty road.

**Where the two halves are asked.** Whether the car is *at* a junction is a fact about where it stands and
belongs with the rest of "is this wanted" ([`DriveScene.OnACarriageway`](../framework/DriveScene.cs)), so
`P-4` and this entry read it from one place and cannot disagree. Whether the pass **fits** before the box is
not known until it is measured, so it sits with the sight-distance bar in `Sa` — a refusal there is an
ordinary exit back to `P-4`, exactly as the ground or the book refusing is.

**The bar is the junction reserve distance** (`SimConfig.CarJunctionReserveM`) and not a figure of this
entry's own — the same one `P-4` hands the junction over at. A car near enough to have asked for the box is a
car negotiating the box, and overtaking and negotiating are alternatives rather than things done at once.
**A second figure would be a second answer to "is this car at a junction yet"** (SIM-7).

**A queue is not an obstruction**, and neither is anything unnamed. A driver stopped on this road is held by
something further up, and that is not this car's to drive round; going round either is a head-on rather than
an overtake. The test is the lane index's
([`LaneOccupancy`](../../../../world/road/LaneOccupancy.cs)) and never a stopwatch's — a car that has stood
at an unlit junction for a minute is a queue, and a car with nobody in it is an obstruction however recently
it stopped.

**A body in the road is one** (`PER-1`), whether it is standing or walking. A walker is an agent like any
other — paint is where a walker's priority lives, and a car owes a crossing its stop short of the band
(`TER-4c.1`) long before this entry is reached. What keeps the swerve off the body is the same thing that keeps
it off a wreck: the body holds a stretch of the book with a margin round it (`PER-15`), and a template over
that stretch is refused by the ground test. **A second rule that refused the same movement would make the
ground test useless** (SIM-7).

**And never at a crossing**, which is the paint's half of "a manoeuvre of open road". A body on the paint
lays the band of the lane it is standing in, so somebody two lanes over leaves this shape a clear run and
the ground test above says yes — and what the car would then be doing is overtaking the queue that stopped
for the zebra, across the paint the people on it are about to reach. **Nothing else refuses that**, which is
why the refusal is here (`DriveScene.ClearOfThePaint`) and is not a second gate.

**The oncoming lane is not claimed.** Crossing the centreline is licensed for exactly this and for nothing
else (CAR-6.2b); a claim on ground the other stream is entitled to would be a car reserving the wrong side
of the road. A car mid-swerve is in the book where it actually lies, so whoever meets it reads it there.
What is claimed is the stretch of its own lane the manoeuvre leaves and comes back to.

**Refs.** CAR-6.1, CAR-6.2b, MAN-7.
