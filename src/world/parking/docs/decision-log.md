# Decision log — parking

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-12 — the ways at the bays are indexed, because a city lays eight thousand of them

What is driven over what at a bay is measured from the lines themselves, and the ways at the neighbouring
bays were compared pairwise on the boxes the two lines stand in — which the note here said was cheaper than
any index would be to lay, *for a few hundred ways*. A city lays 8 606 of them, and that is 37 million pairs
proving that two lines a district apart do not touch.

**Which pairs are compared is now the geometry grid's to say** (`ChainIndex.Crossing`) and what a pair comes
to is still the two boxes and then the two lines. The candidates are a superset of what the boxes admit — the
index holds the whole way where a box is its driven part grown by the clearance, and the query is asked with
that clearance over — so the table is the table it was. The ways are taken in their own order rather than the
lattice's, since a crossing is filed under both ways it is a crossing of.

**It is inside the noise of standing a town up**, which the boxes always were; what it buys is that the cost
follows how crowded a car park is rather than how many car parks the town has.

## 2026-09-11 — the room beside a car and the room at its ends are two figures

One margin sized both (`ParkingSpaceMarginInCarWidths`), and what sized it was the ends: a parallel bay is
reversed into, so half a car width at either end is the manoeuvre's and not a comfort. Read at the same
figure down the sides, a space came out **4.0 m against a 3.6 m lane** — wider than the traffic lane its
own way is driven out of.

**A way lays the ground its space is wide for every metre it runs** (GEN-4f), and it leaves along its lane,
so every bay in the town stood a 0.2 m lip of tarmac past the kerb over the metres the two ran together.
The lip is in the ground and not only in the reading of it: it shows as a lump in the tarmac, and the
outside of the driven ground (`LaneShell`) has to follow it, stepping off the lane onto the bay's way and
straight back — **461 times on Odesa, 140 of them shorter than a hand**, each one drawn as a spur off the
lane and back because the layer marks lines and not edges (OBS-2p).

`ParkingSpaceSideMarginInCarWidths` is the side's own, at a quarter of a car width — a door's swing, and
half a metre either side of the shipped car. Nothing manoeuvres sideways, so the two figures were never one.
What it costs is a narrower bay, which is what a bay is; what it buys is measured in
[citygen's log](../../../citygen/docs/decision-log.md#2026-09-11--the-cut-is-solved-against-the-band-that-makes-it):
Odesa's dips go to nought, its left-over ends to nought, and its shell from 99 rings of 100 to 105 of 105.
GEN-4c carries the relation and `SimConfigTests` gates it.

## 2026-09-03 — a bay's way runs the length of its space, and is driven as far as the pose

A way ending where the car did left the deepest metres of every space belonging to no way, so a person
standing in front of a parked car claimed nothing and the driver aiming there read the space as free.
`LengthM` is the way's own metres and `DrivenLengthM` how many are driven — the split a lane has carried
all along (`TER-5d`). A way out has none: ground behind a leaving car is not ground its line covers, and a
way whose first metre is not its start is one `LineAssembler` cannot begin. The driven figure is the old
one to the metre, so lines, crossings and routes are unchanged; what changed is only what can be claimed.

## 2026-09-02 — a bay is a network, and a parked car is laid onto it like any other body

A car in a bay was the one body the placing walk was never asked about: it went down a branch of its own,
so a car standing across somebody else's bay stood on nothing, and a car shoved half out of a space held
neither the ground it rested on nor the space it came from. The bays are the town's third network
([`BayNetwork`](../BayNetwork.cs)), walked by the same code as the other two, and `PlaceTheBody` has no
branches left. Which networks the claims are laid over is stated once, because naming them at the call site
left the walkers behind — a person standing in a parking space held no metre of it. The band is the space's
width and not the lane's, or every car driving past a frontage would stand on every bay it passed.
**Deleted:** `BayStandings`, `WhereABodyInTheBayStandsM`, `StandingInABay`, `LieInTheBay`, `LyingState.Bay`
and both `ParkingStandingGroundM` figures.

## 2026-09-01 — the swing into a bay was bought with a straight nothing was keeping

A quarter turn carries the rear axle its own radius sideways; the shipped lot stands bays 4.4 m off the
lane and the tightest circle is 3.94 m, so one arc reaches with half a metre to spare. The swing existed
only because a tenth over the minimum radius and a quarter of a car length of settling straight had already
spent it. Both are a twentieth now: the way in went from 11.9 m with a 27° swing over the centreline to
6.8 m with no swing. What decided it was measuring what the straight buys in a town — the follower hands on
about twenty degrees out of square at either figure. The swing is kept for a bay standing nearer its lane
than a turning radius; no bay in the shipped town is one.

## 2026-08-27 — a bay is where a car turns round, and the turn claims it like any other

No junction admits a movement that reverses direction (TER-5f), and a bay already has a way in off either
lane and a way out onto either (GEN-4j). Three things had to be true. The standing is the turn's and not
the driver's habit, since only one standing comes out the other way off a given lane. A leg holds two bays
while it turns, because a claim dropped and re-taken is a leg that loses its place while turning round to
reach it. And a way the car reverses into is not threaded onto the leg's line — a route is driven forwards,
and appending a backed-in entry made half the fleet's parks a car driving forwards down a line drawn for
reverse.

## 2026-08-25 — how far a bay's way reaches over the street is the table's question and nobody else's

The builder held up a bar of its own, because a nose-in way crossed the middle of an eight-metre street and
nothing appeared to hold anybody off it. That misread the table: two lines a car's width apart are two
*bodies* touching (`TER-5c`), and two lines 2.05 m apart are two bodies passing with 5 cm between them. It
was a second gate in front of one already answering (`SIM-7`), stricter by the width of a lane, and it
refused 1260 of Odesa's 1264 bays — leaving every bay on a narrow street reachable from one side of its own
street only. With it gone, Odesa's parks over the measured minute went 22 → 35 and emergency stops 83 → 65.
River pays: 305 of its narrow bays now sweep the middle of their street, taking emergency stops 19 → 31.
That is the table doing its job rather than the bar doing it early.

## 2026-08-24 — a car stands in a bay one of two ways round, and reverses only to the lane beside it

Every car nosed in and reversed out, across the whole carriageway if the far lane suited — which the town
held everybody off correctly and is simply not what happens at a kerb — and nothing ever backed *into* a
space, which is the commoner habit and the one that leaves a car able to drive away. A bay carries a
standing and not just a shape; what tells the four ways apart is `IsEntry ≠ IsNoseIn` and no fifth field.
Only the lane beside the bay lays both ways, so the far lane is kept only in the direction driven forwards.
Which way round a driver parks is a habit drawn once per car, because the line into a bay is laid by asking
twice and two draws would disagree. The price is that a nose-in car's one way out is onto the near lane, so
Odesa parks 35 in the minute against 47 — but the ladder is quieter throughout, and nose-in only with no
far-lane reverse costs 90 emergency stops against 65, which is worse than either.

## 2026-08-24 — one shape at a bay, driven both ways, off either lane

Two shapes — a forward-in solved from the lane and a reverse-out solved from the bay — meant the way out
landed *near* the lane rather than on it, closed by three figures all paying for the same missing
constraint; the two answers could disagree, so `CanBeEntered` and `CanBeLeft` were separate questions; and
the near lane got the way in while the far one got nothing. One shape solved once and driven both ways
closes all three. The template swings away before it turns in, which is what buys the near lane: a quarter
turn of radius `R` moves the axle `R` sideways against a 3.6 m offset and a 4.3 m radius, and swinging `φ`
first brings it to `R(2cos φ − 1)`. So the car stands in the middle of its bay again. What it cost was the
resolution of the crossing measurement, which was the real bug: sampled at the crossing clearance, two
metres of slop could not see the 1.6 m a centred car stands clear, so every parked car read as cutting its
street. Bay ways are walked as finely as the sample budget allows.

## 2026-08-24 — a bay is two of the road's own ways

The last dozen metres of every leg were outside the ways altogether: a car park was somewhere the town's
own mechanisms did not reach. The two lines at a bay are laid once with the town as ways of the road, and
each consequence deleted code — the route reaches the bay through the same assembler that threads a
junction's joins, so `LastLaneToM`, `IsTheApproachLane` and the staging place are gone; and the traffic is
held off by the table through [`LineOverlap`](../../road/LineOverlap.cs), so the projection-onto-nearest-lane
reading went with its `NearestLane` per parked car per tick. The whole bay was asked for first and cannot
be had: laid from the mouth in it takes 0.77 m of the lane's own driven ground, and every parked car cut
the street beside it — Odesa's parks 17 → 4. The picture of a taken bay comes off the register instead.

## 2026-08-24 — the departure is a movement, and the bay's own claim is a register

The way in was one of the road's ways and the way out was not, so leaving a bay held the street off with a sweep, a
gap probe, a patience and a random beat — the town already knows what a car crossing a stream of traffic
does, and a car leaving a bay was doing it by hand. The way out is driven (`CarFleet.LineWay`), leaving the
entry's `Sa` and three lines. A movement is a way and not a turn (`CarFleet.MovementWay`), so one protocol
serves a bay's way out and a junction's join. A bay's own claim is a register and says so — the hold begins
when the walker sets off, with no line to hold ground along yet. Which way out a car takes is the lane
already running its way. Against the state before any bay ways existed, Odesa's emergency stops went
121 → 41 and abandonments 14 → 1; legs settled 1 → 8 is the price, and it is the patience — a car in a bay
now gives way like anything else, which is the yield's to tune rather than the bay's to special-case. A
parked neighbour can still stand in a way out: `LineOverlap` reads a section from first contact to last,
which costs nothing while a row is half empty and will bite a full one.
