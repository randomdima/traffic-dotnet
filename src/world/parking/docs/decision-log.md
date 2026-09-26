# Decision log — parking

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- `GEN-4e`: a walk to a parked car was aimed at the bay's point → the point is kept and nothing asks for it.
- `GEN-4f`: one shape solved per standing and per lane, near and far, swinging away → the ways' structure
  as it is, and nothing lays one (`BayWays.Build`).
- `GEN-4g`: the hold began when a trip picked the bay → when a leg is sent or re-aimed; a bay is also
  refused while turned in, held or unreachable.
- `GEN-4i`: the pose the ways are drawn to, and a depth priced by the street's crossing → the pose a car is
  stood at and a way ends at.
- `GEN-4j`: near lane and far lane, and the swing and its two figures → a standing is a pair of ways, and
  what one takes of the street is the table's.
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

## 2026-09-01 — the swing into a bay was bought with a straight nothing was keeping

A quarter turn carries the rear axle its own radius sideways; the shipped lot stood bays 4.4 m off the
lane and the tightest circle is 3.94 m, so one arc reaches with half a metre to spare. The swing existed
only because a tenth over the minimum radius and a quarter of a car length of settling straight had already
spent it. Both are a twentieth now: the way in went from 11.9 m with a 27° swing over the centreline to
6.8 m with no swing, and the follower hands on about twenty degrees out of square at either figure.

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

## 2026-08-24 — a car stands in a bay one of two ways round, and reverses only to the lane beside it

Every car nosed in and reversed out, across the whole carriageway if the far lane suited, and nothing ever
backed *into* a space — the commoner habit, and the one that leaves a car able to drive away. A bay carries
a standing and not just a shape; what tells the four ways apart is `IsEntry ≠ IsNoseIn` and no fifth field.
Only the lane beside the bay lays both ways, so the far lane is kept only in the direction driven forwards.
Which way round a driver parks is a habit drawn once per car, because two draws would disagree. The price
was Odesa parking 35 in the minute against 47; nose-in only with no far-lane reverse cost 90 emergency stops
against 65, which is worse than either.

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
