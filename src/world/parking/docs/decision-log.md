# Decision log — parking

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-10-17 — a car parks alongside a kerb, in a gap the bodies there leave, and in no slot

**The owner asked for cars to park both in the car parks and on the roadside where one runs, the roadside held by
ordinary claims as one continuous stretch with no slots** (GEN-4n).

- **A kerb place is a place of the register opened where the car will stand, not a slot laid along the kerb.** Every
  question already asked of a bay — claimed by a leg, occupied, the street it is worked off, the way in, the manoeuvre,
  the route there and away — is asked of it unchanged, so the register keeps a pool of them after its painted bays,
  two a car (the one it stands in and the one it is aimed at), each closed again once nobody holds it. Laying the kerb
  out as slots would have been the bays GEN-53 lays, which is what was asked against.
- **The gap is read off the claims**: the bodies on the roadside (TER-4c.2) and the places other cars are aimed at,
  which are the one thing the ground does not yet show. Two cars could otherwise drive to one gap; the register's
  claim settles that before either moves, as it does for a bay.
- **In and out on an S.** A bay's shapes turn a car square to its line, and `BayTemplate.SquareEnough` refused a
  place alongside it; an S on the car's own circle slides it across instead — forwards with room behind, backwards
  after pulling on past, and out forwards after backing up a bumper's worth where the car in front is close. The
  roadside is swept with the lane beside it, so a body at the kerb refuses the shape.
- **Only beside a lane that leads on and is entered from the town.** The first runs stood cars at the ends of dead-end
  streets — stretches beside the lane into a dead end, and beside the lane back out of one, which a car reaches only by
  going down the dead end first — and at the last metres of short lanes, where a car pulling out needed to change lanes
  in no room. The stretches are cut to lanes that lead on (`RoadGraph.DrivesOn`), are reached without a dead end, and run
  `KerbParkingLaneRunM` either side of the stretch.
- **A traced map's own cars stand at its kerbs and run the round**, where they toured before: a round finds a kerb
  within a walk of almost any place drawn, which the car parks alone did not (2 of 5 000 drove in two minutes). Over
  120 s on OdesaOsm at 5 000 cars: 1 316 drove, 835 parked at a kerb and 24 in a car park, 2 wrecked, 4 legs given up,
  95 standing at the end — 88 of them queued behind the four that wait at a lane's end for a lane change the route asks
  for and the lane has no room for (tech-debt) — and no ring of cars waiting on each other.

## 2026-10-16 — a manoeuvre is shaped only with its stop dead ahead, and only out of a bay the car stands in

**What the stuck probe found** (Odesa, 300 s, six wrecks): four were bay manoeuvres. Two shaped their way in at
22–24 m/s from 40 m out on a bend, so the first straight ran on along the car's heading — across the grass into a
shed, and diagonally through the rank opposite into a parked car. One was an evacuator registered in the bay it had
left: laid again from the road, its leg shaped a reverse out of that bay down the street, at full reverse
throttle, into the car behind.

**Decided**: the way in is shaped only once the place the car waits for it lies along the car's heading, within the
room its lane leaves beside its body (`ParkingIn.TheStopIsDeadAhead`), and a leg begins out of the registered bay
only where the body stands in it — a registration it has left is given back (`TownWorld.LayTheFirstLine`,
`ParkingRegistry.HoldsTheBody`). **Measured** over 300 s: Odesa wrecks 6 → 0, bays 354 → 363; River wrecks 2 → 0,
cars ever standing a minute 1 → 0.

**Refused: shaping only at manoeuvring pace** (`CarBuild.ReverseMaxMps`). It stopped the wrecks, since a slow car
is near its stop, but a car then shaped only once it had all but stopped, where the decision of 2026-09-30 (car log)
had it trying a shape at every decision on the way up. Two cars stopped abreast on a street of perpendicular bays,
each swinging its nose-in over the other's lane, and neither asked until the leg's clock gave the place up: River
had 13 cars standing a minute against 1.

**And a shape a body stands on is shaped last** (`ParkingIn.ShapeTheWayIn`, `BayManoeuvring.WhatTheShapeTakes`): two
cars abreast still met that way over 20 minutes of River, each nose-in over the other, and backing in from past the bay
was the shape neither tried, since it took more street. Of the two, one nobody stands on is taken first, the least
street settling it only where both are or neither is: a shape a body stands on is never asked while it stands there.

## 2026-09-30 — a car leaves a bay knowing its route

**The owner asked why a car moves before its route is known** (GEN-4f): select the direction, then unpark. The lane
out was ranked by where the destination lay as the crow flies, and the route was searched only once the car stood
on the street — so over six minutes of Odesa 188 of 830 cars that could pull out either way took the dearer lane,
by a median 346 m of route cost, and began their leg by turning round in a bay or going round the block. **The
route is now laid from the bay before the car moves**, one search entered from both lanes at the metre the mouth
stands abeam of, and the manoeuvre is shaped onto the lane that route sets off down (`TownWorld.LeaveTheBay`);
where the car cannot be got onto that one, the route is laid again from the lane it can make before it moves.
Turns in a bay over ten minutes of Odesa went from 126 to 31 with this and the car slice's fixes to where a leg
ends (its log, same date); with those in and this out, 54.

## 2026-09-30 — a bay is a short road of its own, and getting in and out is the car's own manoeuvre

**The owner asked for car parks not to be junctions any more**: a bay laid as a short lane beside the road and
joined to nothing, and parking a car action of its own that reserves the street it needs and lays its own
manoeuvre, differently for each car by what that car is — **reserving as little of the road as it can**. Before,
a car park was a junction cut into its street with every bay an arm, and a car drove a pair of ways laid with
the town, in and out of each bay off each lane.

- **The manoeuvre is shaped from where the car stands, on its own circle** (GEN-4f, `BayManoeuvre`,
  `ParkingIn.ShapeTheWayIn`, `PullingOut.ShapeTheWayOut`): `CarBuild.ParkingTemplateRadiusM`, `ParkingStraightensUpM` and
  the car's own body. Into a bay there are two shapes — nose in, with a swing away first where the car stands
  nearer the bay than its circle, and on past the bay and back in — and **the one whose body takes less of
  the street wins**, read off the lanes the bay is worked off (`WhatTheShapeTakes`); the driver's habit settles
  a tie. Out of a bay there is one shape per lane of the street, and the car takes the one its route sets off down
  (below). Nose in takes less street almost everywhere: Odesa parked 558 nose in and 11 backed in over 300 s.
- **Its ground is a plan of its own, as a pass's is** (TER-4c.6): the street the shape stands over, asked for
  where all of it is free, kept or withdrawn once, and given back as it is driven. A car that has begun a
  manoeuvre holds the street it asked for and nothing takes it; one waiting for it holds none.
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
  bay is found by the bay whose space holds it** (`TownWorld.TheBayItStandsIn`), both still: a car stood down
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
