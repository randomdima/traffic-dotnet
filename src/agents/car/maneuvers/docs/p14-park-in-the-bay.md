# `P-14` — park in the bay

Code: [P14ParkInTheBay.cs](../planned/P14ParkInTheBay.cs) · [catalogue](index.md)

**Scenario.** The route has run out at the staging place on the lane the bay this leg holds is entered
from. Past that point the car is manoeuvring rather than driving.

**`Sa` — the state it starts in.** At rest at the end of the leg's last lane, holding a reservation on a
bay that can be entered, and the forward-in template can be laid **from the pose the car is actually
standing in**.

**`Sb` — the state it delivers.** The rear axle at the bay's own pose, the body square in it, at rest.

**Line.** The forward-in template: a straight run-in, a fillet at the car's own turning circle, and the
run into the bay. Drawn for the rear axle, and **ending on a straight**.

**Do.** Drive it at manoeuvring pace. Nothing else.

**Guards.** The line stays a template.

**Bounds.** The short fuse — the shape lies across the lane the car came down.

**Exits.**

| | Successor |
|---|---|
| the template is driven in | the plan's next step — `P-17` |
| the line is no longer a template | `P-4` (failure) |
| the template cannot be laid from here | `E-6` — a bay that cannot be driven into from here is a retarget and never a squeeze |
| stuck past the fuse, at the bay | the ladder — rung 2 is `P-16` |

**Four things about the geometry that must not be re-litigated.**

1. **Every line is drawn for the rear axle** and every pose that meets one is measured to it. The rear
   wheels do not steer, so the rear axle is the only point that travels the way the car is pointing; the
   middle of the body crabs, and the tightest circle *it* can hold is `√(R² + d²)`. A template drawn
   through the middle of the car at the car's own minimum radius is not merely hard to follow but
   **impossible**, and the car rides it 20° out of square all the way in.
2. **A bigger turning radius does not park a car better; run-in does.** Every metre of radius is a metre
   off the run-in.
3. **A margin over the minimum radius must be measured against the road it costs**, not only against the
   squareness it buys. Widening an arc always looks free in a squareness number and never is.
4. **A template that ends on an arc ends with the car still turning.** Aiming on down the final tangent
   converges the car onto the line, but at manoeuvring pace with the rack still unwinding that takes
   ground — measured at **12° off the lane against 1.1°** with a quarter of a car length of straight on
   the end. Every template ends on a straight.

**Why it is not scheduled.** Steering to a pose inside a four-metre bay: the one place in the town where a
tenth of a second of lag is metres.

**Refs.** CAR-4a, CAR-6.5, GEN-4, S-1, S-1b.
