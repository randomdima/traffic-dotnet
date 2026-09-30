# Decision log — parking

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-30 — a bay is a short road of its own, and getting in and out is the car's own manoeuvre

**The owner asked for car parks not to be junctions any more**: a bay laid as a short lane beside the road and
joined to nothing, and parking a car action of its own that reserves the street it needs and lays its own
manoeuvre, differently for each car by what that car is — **reserving as little of the road as it can**. Before,
a car park was a junction cut into its street with every bay an arm, and a car drove a pair of ways laid with
the town, in and out of each bay off each lane.

- **The manoeuvre is shaped from where the car stands, on its own circle** (GEN-4f, `BayManoeuvre`,
  `ParkingIn.ShapeTheWayIn`, `PullingOut.ShapeTheWayOut`): `CarBuild.ParkingTemplateRadiusM`, `ParkingStraightensUpM` and
  the car's own body. Into a bay there are two shapes — nose in, with a swing away first where the car stands
  nearer the bay than its circle, and on past the bay and back in — and **the one whose swept body takes less of
  the street wins**, read off the atlas as a pass's ground is (`WhatTheShapeTakes`); the driver's habit settles
  a tie. Out of a bay there is one shape per lane of the street, ranked by where the car is going and then by the
  street it takes. Nose in takes less street almost everywhere: Odesa parked 558 nose in and 11 backed in over
  300 s.
- **Its ground is laid as a pass's** (TER-4c.6, `OverTheGround`): the body swept down the shape, asked for where
  all of it is free with the pass's spare, laid at p0, kept or withdrawn once, and given back as it is driven.
  A car that has begun a manoeuvre holds the street it swept and nothing takes it; one waiting for it holds none.
  This replaces the claim-whole-or-not-begun rule a bay's way needed, and the same protocol now does both jobs.
- **Reversing into a bay is allowed again**, which it was not while a car backed in over a way the follower had
  already stopped on: the whole manoeuvre is asked for before the car pulls past the bay, so whoever follows
  is held short of it by the ground and not caught behind the car on it.
- **A bay's ribbon is the space's width and not the tarmac's** (`TownWorld.LineOfWay`): a car turning into one
  swings a corner over its neighbour's mouth, and read at the lane's width that stood it on the neighbour's
  ground whenever a car stood there — which is every rank a town fills.
- **A turn at a lot is made in the bay furthest along its lane and near its end** (GEN-4l,
  `TurnAtALotWithinM`), and the car comes straight back out onto the lane running back, its leg kept. The router
  turns a leg at a lane's end; turned at the first bay it came to, a car landed on the lane back further along
  than its route was planned for, and the two routes that followed each turned it round again — a police car
  parked and left the same frontage five times over. **The cost is turns**: a car park that is not near its
  lane's end is no longer somewhere a leg may turn, where every car park was one while it was a junction.
- **A body standing in a box takes the join it stands in** (`TownWorld.TheCarriagewayUnder`) and **a body in a
  bay is found by the bay whose space holds it** (`ParkingRegistry.BayHolding`), both still: a car stood down
  anywhere in a space it was never written into still leaves by that space's manoeuvre.

Load (`--bench load`, Release): Odesa opens in 2 968 → 1 522 ms and River in 2 122 → 1 250 ms — the world
1 569 → 604 ms and the plan's ground 757 → 319 ms on Odesa. Odesa's atlas is 106.0 → 48.9 MiB and 12.4 → 5.0
million entries, its lanes 8 272 → 5 200. The tick and the stuck probe are in the car slice's log of the same
day.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- `GEN-4e`: a walk to a parked car was aimed at the bay's point → the point is kept and nothing asks for it.
- `GEN-4g`: the hold began when a trip picked the bay → when a leg is sent or re-aimed; a bay is also
  refused while turned in, held or unreachable.
- `GEN-4k`: a hospital's and a station's apron → a depot's too, and a struck-off vehicle's bay held for
  nobody.

## 2026-09-11 — the room beside a car and the room at its ends are two figures

One margin sized both (`ParkingSpaceMarginInCarWidths`), and what sized it was the ends: a parallel bay is
reversed into, so half a car width at either end is the manoeuvre's and not a comfort. Read at the same
figure down the sides, a space came out **4.0 m against a 3.6 m lane**. `ParkingSpaceSideMarginInCarWidths`
is the side's own, at a quarter of a car width — a door's swing. Nothing manoeuvres sideways, so the two
figures were never one. GEN-4c carries the relation and `SimConfigTests` gates it.
