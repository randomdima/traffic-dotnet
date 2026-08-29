# `P-14` — park in the bay

Code: [P14ParkInTheBay.cs](../planned/P14ParkInTheBay.cs) · [catalogue](index.md)

**Scenario.** The leg's line has left the road for the way into the bay this leg holds. Past that point
the car is manoeuvring rather than driving, and it is asked on every tick for it.

**`Sa` — the state it starts in.** Either the line in hand already finishes at the bay — which is what the
town laid the bay's own way for — or, where the car has been shuffled off that line, a bay this leg holds
and a template into it that can be laid **from the pose the car is actually standing in**.

**`Sb` — the state it delivers.** The rear axle at the bay's own pose for the standing taken, the body
square in the middle of it, at rest.

**Line.** The bay's own way in ([`BayWays`](../../../../world/parking/BayWays.cs)), threaded onto the end
of the leg's line by the assembler and driven on unbroken; or the same shape laid as a template of its
own, where there is no line to be on. Either way it is the swing away from the bay, the turn into it, and
the run in — drawn for the rear axle, and **ending on a straight**. It is also the line the car will leave
on, driven the other way (`P-2`).

**Whose shape it is.** The town's way in is the nominal car's (`CAR-11a`); the template this entry lays is
**this car's** — its own turning circle, its own straight to settle on, and the axle pose read back through
its own body (`CAR-11`). The bay itself is refused outright to a body the space cannot hold (`CAR-11b`),
which is a retarget and never a squeeze.

**Which way round, and therefore which gear.** The driver's habit where the bay lays it and the other where
it does not (`GEN-4j`). Nosing in, the car comes up the lane and turns off it short of the bay, under
power. Backing in, it has driven past the bay first and reverses from beyond it — a different shape, not a
different traversal, and one the near lane alone lays. Whichever it is, the car will leave in the other
gear.

**A narrow street lays both of them and one sweeps the middle of it.** Nosing in off the lane beside the
bay swings out over the carriageway to do it; what that takes of the oncoming lane is in the table like any
other crossing (`GEN-4j`), so the traffic is held off it and the habit gets its say.

**Do.** Drive it at manoeuvring pace. Nothing else.

**Guards.** The line stays under the car.

**Bounds.** The short fuse — the shape lies across the lane the car came down.

**Exits.**

| | Successor |
|---|---|
| the line is driven in | the plan's next step — `P-17` |
| the car has lost the line and has no template either | `P-4` (failure) |
| no line, and the template cannot be laid from here | `E-6` — a bay that cannot be driven into from here is a retarget and never a squeeze |
| stuck past the fuse, at the bay | the ladder — rung 2 is `P-16` |

**Why the way is the town's and not the car's.** A line laid from whatever pose the car happens to be
standing in is a different line every time it is asked for, so nothing about the ground it takes can be
said until the car is on top of it — which is a manoeuvre no other driver can be held off. Laid once with
the bay, it is a **way of the road's book** like a lane or a junction's join: the reservation runs along
it, the traffic on the lane it crosses is cut by the town's own table of crossings, and the car converges
onto it exactly as it converges onto every other line in the town. The template survives as the recovery,
where there is no line to converge onto.

**Four things about the geometry that must not be re-litigated.**

1. **Every line is drawn for the rear axle** and every pose that meets one is measured to it (CAR-4a).
   A template drawn through the middle of the car instead is impossible for the car to hold, and inside a
   four-metre bay that is the difference between a parked car and one across two spaces.
2. **A quarter turn of radius `R` moves the axle `R` sideways, and no shape moves it less.** A bay standing
   nearer its lane than that is one no single arc reaches — so the template swings *away* from the bay
   first, which brings the sideways travel to `R(2cos φ − 1)`. It is what a driver does turning into a
   perpendicular space off the lane beside it, and it is the whole of why both lanes of a street can work a
   bay.
3. **A margin over the minimum radius must be measured against the road it costs**, not only against the
   squareness it buys. Widening an arc always looks free in a squareness number and never is.
4. **A template that ends on an arc ends with the car still turning.** Aiming on down the final tangent
   converges the car onto the line, but at manoeuvring pace with the rack still unwinding that takes
   ground — measured at **12° off the lane against 1.1°** with a quarter of a car length of straight on
   the end. Every template ends on a straight.

**Why it is not scheduled.** Steering to a pose inside a four-metre bay: the one place in the town where a
tenth of a second of lag is metres.

**Refs.** CAR-4a, CAR-6.5, GEN-4, GEN-4f, GEN-4i, GEN-4j, S-1, S-1b.
