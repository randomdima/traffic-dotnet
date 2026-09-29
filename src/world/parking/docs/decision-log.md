# Decision log — parking

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-29 — the bays are laid off their car parks, and nothing reverses into one

**The owner asked for parking and unparking back, for every vehicle**, so a police car can go home to its
station. A car park had been a junction cut into a road with its arms the bays (GEN-53) since 2026-09-14, with
the arms laid as lanes and no bay's ways laid at all.

- **A bay's ways are read off its car park and draw nothing** (GEN-4f, `BayWays.Build`): nose in, the last of
  the lane, the movement onto the arm and the arm; backed in, the arm, the movement off it and the first of the
  lane it lands on. Each runs `ParkingStagedInM` along its street lane: ended on the turn, a way handed its car
  over crosswise in the street, because a line is handed on with the car at rest anywhere in its last car
  length — on Odesa a car stood 4 m off its line in the middle of a street, the traffic queued behind it.
- **Nothing reverses into a bay** (GEN-4j). A car backing in had driven past the car park, the car following it
  stopped at its tail on the ground it had to reverse over, and each waited on the other until both clocks ran
  out — a queue of six on Odesa behind one. A car in a bay has nobody queued behind it, so reversing out cannot
  lock that way. A car stood backed in — a spawn, a service vehicle — still drives out forwards.
- **An arm is never a lane a route or a tour runs down** (GEN-4h). Offered as lanes, every arm was a dead end
  the router could turn a leg at (`WhereALegMayTurn`), and cars leaving one bay were routed nose first into the
  next to turn round — 44 cars on Odesa stood at the back of a neighbour's space for the rest of the run.
- **A body in a bay is found by the bay whose space holds it** as well as by the register
  (`ParkingRegistry.BayHolding`): a leg given up on the way in stood a car down in a space it was never written
  into, and the lane under it was the arm.

On the stuck probe's five minutes of Odesa: 400 cars parked, no leg given up, 32 cars standing still at the
end — against the tour's 46 legs given up and 30 standing.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- `GEN-4e`: a walk to a parked car was aimed at the bay's point → the point is kept and nothing asks for it.
- `GEN-4g`: the hold began when a trip picked the bay → when a leg is sent or re-aimed; a bay is also
  refused while turned in, held or unreachable.
- `GEN-4i`: the pose the ways are drawn to, and a depth priced by the street's crossing → the pose a car is
  stood at and a way ends at.
- `GEN-4k`: a hospital's and a station's apron → a depot's too, and a struck-off vehicle's bay held for
  nobody.
- `GEN-4l`: the only way round, kerb-side against across the carriageway, the habit settling a bay that lays
  both → the router also turns a leg at a dead end where nothing turns the car, and nose in is taken first.

## 2026-09-12 — the ways at the bays are indexed, because a city lays eight thousand of them

The ways at neighbouring bays were compared pairwise on the boxes their lines stand in, which was cheaper
than an index *for a few hundred ways*. A city laid 8 606 of them — 37 million pairs proving that two lines
a district apart do not touch. **Which pairs are compared is now the geometry grid's to say**
(`ChainIndex.Crossing`), and what a pair comes to is still the two boxes and then the two lines, so the
table is the table it was. The cost follows how crowded a car park is rather than how many the town has.

## 2026-09-11 — the room beside a car and the room at its ends are two figures

One margin sized both (`ParkingSpaceMarginInCarWidths`), and what sized it was the ends: a parallel bay is
reversed into, so half a car width at either end is the manoeuvre's and not a comfort. Read at the same
figure down the sides, a space came out **4.0 m against a 3.6 m lane** — wider than the traffic lane its
own way is driven out of — and the outside of the driven ground (`LaneShell`) stepped off the lane onto
the bay's way and straight back **461 times on Odesa**. `ParkingSpaceSideMarginInCarWidths` is the side's
own, at a quarter of a car width — a door's swing. Nothing manoeuvres sideways, so the two figures were
never one. GEN-4c carries the relation and `SimConfigTests` gates it.

## 2026-09-03 — a bay's way runs the length of its space, and is driven as far as the pose

A way ending where the car did left the deepest metres of every space belonging to no way, so a person
standing in front of a parked car claimed nothing and the driver aiming there read the space as free.
`LengthM` is the way's own metres and `DrivenLengthM` how many are driven — the split a lane has carried
all along (`TER-5d`). A way out has none: ground behind a leaving car is not ground its line covers.

## 2026-09-02 — a bay is a network, and a parked car is laid onto it like any other body

A car in a bay was the one body the placing walk was never asked about, so a car standing across somebody
else's bay stood on nothing, and a car shoved half out of a space held neither the ground it rested on nor
the space it came from. The bays are the town's third network ([`BayNetwork`](../BayNetwork.cs)), walked by
the same code as the other two, and `PlaceTheBody` has no branches left. Which networks the claims are laid
over is stated once, because naming them at the call site left the walkers behind. The band is the space's
width and not the lane's, or every car driving past a frontage would stand on every bay it passed.

## 2026-08-27 — a bay is where a car turns round, and the turn claims it like any other

No junction admits a movement that reverses direction (TER-5f), and a bay already has a way in off either
lane and a way out onto either (GEN-4j). The standing is the turn's and not the driver's habit, since only
one standing comes out the other way off a given lane. A leg holds two bays while it turns, because a claim
dropped and re-taken is a leg that loses its place while turning round to reach it.

## 2026-08-25 — how far a bay's way reaches over the street is the table's question and nobody else's

The builder held up a bar of its own, because a nose-in way crossed the middle of an eight-metre street and
nothing appeared to hold anybody off it. That misread the table: two lines a car's width apart are two
*bodies* touching (`TER-5c`), and two lines 2.05 m apart are two bodies passing with 5 cm between them. It
was a second gate in front of one already answering (`SIM-7`) and it refused 1260 of Odesa's 1264 bays.
With it gone, Odesa's parks over the measured minute went 22 → 35 and emergency stops 83 → 65; River's
narrow bays swept the middle of their street, taking emergency stops 19 → 31, which is the table doing its
job rather than the bar doing it early.

## 2026-08-24 — one shape at a bay, driven both ways

Two shapes — a forward-in solved from the lane and a reverse-out solved from the bay — meant the way out
landed *near* the lane rather than on it, closed by three figures all paying for the same missing
constraint, and the two answers could disagree. One shape solved once and driven both ways closes both. The
real bug was the resolution of the crossing measurement: sampled at the crossing clearance, two metres of
slop could not see the 1.6 m a centred car stands clear, so every parked car read as cutting its street.
Bay ways are walked as finely as the sample budget allows.

## 2026-08-24 — a bay is two of the road's own ways

The last dozen metres of every leg were outside the ways altogether: a car park was somewhere the town's
own mechanisms did not reach. The two lines at a bay are laid once with the town as ways of the road, so
the traffic is held off by the marks ([`RibbonAtlas`](../../road/RibbonAtlas.cs)) like any other. The whole bay was asked for first
and cannot be had: laid from the mouth in, it takes 0.77 m of the lane's own driven ground, and every parked
car cut the street beside it — Odesa's parks 17 → 4.

## 2026-08-24 — the departure is a movement, and the bay's own claim is a register

Leaving a bay held the street off with a sweep, a gap probe, a patience and a random beat — the town
already knows what a car crossing a stream of traffic does, and a car leaving a bay was doing it by hand.
The way out is driven like any way, and a movement is a way and not a turn, so one protocol serves a bay's
way out and a junction's join. A bay's own claim is a register — the hold begins before anybody has a line
to hold ground along. Odesa's emergency stops went 121 → 41 and abandonments 14 → 1; legs settled 1 → 8 is
the price, and it is the yield's to tune rather than the bay's to special-case. A parked neighbour can
still stand in a way out: a mark reads a section from first contact to last, which costs nothing while a
row is half empty and will bite a full one.
