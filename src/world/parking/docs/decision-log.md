# Decision log — parking

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-08-27 — a bay is where a car turns round, and the turn holds it like a booking

No junction admits a movement that reverses the direction of travel any more (TER-5f), so the town needed
somewhere else for a leg to come back the way it came, and it already had one: **a bay has a way in off
either lane and a way out onto either lane, and one standing that pairs them across the street** (GEN-4j).
Nose in from the far lane and reverse out onto the near one, or back in off the near lane and drive out
across it — either way the car leaves by the lane it did not arrive on, over the town's own ways, held and
measured exactly as at any other park (GEN-4l).

Three things had to be true for it and each one is a rule rather than a special case.

**The standing is the turn's and not the driver's habit.** Which way round a car parks is a habit
everywhere else (GEN-4j) precisely so that the two askings that lay a leg's line agree; here it is the
manoeuvre that settles it, because only one standing comes out the other way off a given lane.

**A leg holds two bays while it turns.** The place it is going to has not changed — only the way round to
it — so the destination's booking stays and the turning bay is a second hold of the same kind, given back
the moment the car is out of it. A booking that had to be dropped and re-taken would be a leg that loses
its place to somebody else while it is turning round to reach it.

**A way in the car reverses into is not threaded onto the leg's line.** A route is driven forwards; a
backed-in entry runs the other way, and the assembler had been appending it to the route all along — half
the fleet's parks were a car driving forwards down a line drawn for reverse and losing it. The line now
ends at the mouth of such a way and `P-14` lays the same shape again from the pose the car stopped in,
which is what it already did for a car shuffled off its line.
## 2026-08-25 — how far a bay's way reaches over the street is the table's question and nobody else's

The builder held up a bar of its own before laying a shape off the lane a bay stands beside: the body, a
flank either side of the axle's line, had to stay clear of the oncoming lane's own paint. It was put there
because a nose-in way crossed the middle of an eight-metre street by up to 1.95 m and **nothing appeared to
hold anybody off it** — the crossing table asks whether two *lines* come within a car's width, and the two
lines stood 2.05 m apart.

**That reading was wrong about the table and the bar cost more than anything it bought.** Two lines a car's
width apart are two *bodies* touching, which is what the clearance is stated as (`TER-5c`) and what every
other movement in the town is held apart by; two lines 2.05 m apart are two bodies passing with 5 cm
between them, which is tight and is not a collision the table missed. The bar was a second gate in front of
a first one that was already answering (`SIM-7`), and it was stricter than the first by the width of a lane.

What it cost, measured: on a four-metre lane the swing carries the axle 1.73 m off its own line — twice
`R(1 − cos φ)`, because the axle goes on moving away until its heading crosses the lane's — so the bar
refused **1260 of Odesa's 1264 bays and 31 of Test's 43**. And the nose-in shape is the only one the far
lane can be driven in on, since a car crossing the carriageway to reach a bay is under power and one
backing in is not (`GEN-4j`). So the bar did not merely settle which way round a car parked: **it left
every bay on a narrow street reachable from one side of its own street only**, and 1264 of Odesa's bays
offered a way out across the carriageway and no way in.

With it gone every bay of every shipped map offers both standings, and every bay on a two-way street lays a
way in off each lane. Over the measured minute:

| | Odesa before | Odesa after | Test before | Test after |
|---|---|---|---|---|
| parked in a bay (`P-14`) | 22 | **35** | 4 | **10** |
| stood parked (`P-17`) | 0 | **28** | 1 | **7** |
| emergency stops (`E-2`) | 83 | **65** | 6 | **1** |
| places given up (`E-6`) | 15 | 16 | 4 | **1** |
| reroutes (`E-7`) | 15 | **11** | 2 | **0** |
| legs settled for (`E-9`) | 2 | **0** | 0 | 0 |
| cars abandoned (`E-10`) | 9 | 9 | 1 | **0** |

**River is the map that pays and it is worth saying why.** Its streets are wide enough that 950 of its 1255
bays already laid the nose-in shape, so the change buys it little and hands it the other 305 — the narrow
ones, whose ways now sweep the middle of their street. Emergency stops 19 → 31 and two back-offs where
there were none. That is the price of the rule showing up where the geometry is tightest, and it is the
table doing its job rather than the bar doing it early.

## 2026-08-24 — a car stands in a bay one of two ways round, and reverses only to the lane beside it

Every car in the town nosed into its space and reversed out of it, and it would reverse out onto either
lane — across the whole carriageway if the far one suited the leg better. Two things were wrong with that,
and they are the same thing from opposite ends.

- **A car backing across a stream of moving traffic is not a manoeuvre a driver makes.** The town held
  everybody off it correctly — the way was in the table like any other — so nothing was unsafe; it was
  simply not what happens at a kerb.
- **And nothing in the town ever backed *into* a space**, which is the commoner habit of the two, and the
  one that leaves a car able to drive away.

**So a bay carries a standing and not just a shape.** Nose-in and backed-in are two shapes, because the
axle a way is drawn to sits a wheelbase's half either side of the middle of the space and the approach runs
up the lane for one and back down it for the other. Each is still a pair of ways over one piece of ground,
and what tells the four apart is which end is the bay's and which gear the car is in — which is one line of
arithmetic (`IsEntry ≠ IsNoseIn`) and no fifth field.

**The near lane is what a bay is usable off at all.** A standing needs both of its ways, and only the lane
beside the bay lays both; the far lane is asked the same question and kept only in the direction driven
forwards. So a car may nose into a bay from across the street and drive out of one across it, and neither
manoeuvre reverses over a lane. What that costs is stated below.

**Which way round a driver parks is a habit drawn once per car** and not a decision taken per bay. It has
to be settled rather than drawn on the spot because the line into a bay is laid by asking twice — once to
learn whether the route ends on a way and once to assemble it — and two draws would disagree. Half the
fleet, because nothing about this town makes one habit likelier than the other and a shape nothing drives
is a shape not worth laying.

Odesa over the measured minute, against the same build with the standing forced to nose-in and the far
lane's reverse-out put back — which is the model as it was:

| Odesa | before | after |
|---|---|---|
| emergency stops (`E-2`) | 83 | 65 |
| back-offs (`E-3`) | 15 | 10 |
| reroutes (`E-7`) | 14 | 11 |
| legs settled for (`E-9`) | 2 | 0 |
| cars abandoned (`E-10`) | 7 | 9 |
| parked in a bay (`P-14`) | 47 | 35 |
| stood parked (`P-17`) | 41 | 28 |

**The last two lines are the price of the rule and they are not a defect.** A nose-in car has one way out
and it is onto the near lane, so a leg whose route wanted the other side of the street now drives to a
junction to turn round, and some of those legs run out of patience on the way. The ladder is quieter for
it — everything above the line is down — and the town parks fewer cars in the minute. Backing in is what
buys most of that back: at nose-in only, with the far lane's reverse-out gone, the same minute costs 90
emergency stops, 16 back-offs and 19 reroutes, which is worse than either.

## 2026-08-24 — one shape at a bay, driven both ways, off either lane

A bay carried two shapes: a forward-in solved from the lane, and a reverse-out solved from the bay and
aimed back at the lane. Everything awkward about a car park came out of their being two.

- **The way out did not land on the lane.** It was a second solve at a pose the first one never saw, so it
  ended *near* the lane rather than on it — closed by spending the template's radius margin, then by an
  overshoot allowance, then by parking the car a metre deeper in its bay to buy the depth the turn needed.
  Three figures, all of them paying for the same missing constraint.
- **The two answers could disagree.** A bay whose entry laid and whose exit refused was enterable and not
  leavable, so `CanBeEntered` and `CanBeLeft` were separate questions with separate call sites.
- **And one lane got the way in while the other got nothing.** The near lane was tried first and the far
  one only as a fallback, because a single arc cannot turn into a bay standing nearer the lane than the
  radius it turns at — so which side of the street a car park is worked off was decided by which lane the
  arithmetic happened to allow rather than by where the car was coming from.

**One shape solved once and driven both ways closes all three.** The way out is the way in reversed, so it
lands on the lane's own centreline by construction; a bay that can be driven into can be driven out of; and
both are the same ground, so a car retraces what it arrived over. The pair is still two ways of the book,
because a way's metres run in the direction it is driven and every reader of one — a reservation, a grant,
a crossing — counts from its start. The two directions of a street are two lanes here for the same reason.

**The template swings away before it turns in, and that is what buys the near lane.** A quarter turn of
radius `R` moves the rear axle `R` sideways, and a bay in the middle of its space stands 3.6 m off the lane
beside it against a template radius of 4.3 — no single arc reaches it, and none ever could, since `R` is
the least sideways travel a quarter turn has. Swinging `φ` the other way first brings it to `R(2cos φ − 1)`,
which is the manoeuvre a driver makes without thinking and the one piece of arithmetic that lets a car turn
into a bay off the lane beside it. The shape still ends on a straight (`P-14`), so `φ` is solved for the
straight rather than the straight left over from `φ`.

**So the car stands in the middle of its bay again**, which is where the paint says a car goes. What made
that impossible before was the reverse-out needing depth to turn in; nothing needs it now.

**What it cost was the resolution of the crossing measurement, and that was the real bug.** A section's
ends are read to the step the line was sampled at (`LineOverlap`), and a way at a bay was sampled at the
crossing clearance — two metres of slop at the end of a way whose last four metres are the bay itself. A
car centred in its bay stands 1.6 m clear of the ground the street is driven over, which two metres of slop
cannot see, so every parked car read as cutting the street beside it. Bay ways are now walked as finely as
the sample budget allows — a hand's breadth over a dozen metres, for the same walk — and the answer is the
geometry rather than the sampling.

## 2026-08-24 — a parking section is a stretch of the network, bracketed by two nodes

A leg into a car park ended at a metre inside a link. Everything else in the town is routed node to node,
so the last piece of a drive to a bay was the one piece of a route the search could not name, and the
price, the reroute and the goal all meant slightly different places.

**A node per section on the road is what fixes it, and there are two of them per section rather than one.**
A section's bays stand along tens of metres of kerb and are reached from both directions, so no single
point on the road has all of them ahead of it; a node at the frontage's middle leaves half the bays behind
whichever lane arrives there. Bracketing the frontage — a cut at each end, set back by the run-in the way
in wants — gives every bay of the section a lane that arrives at a node with the whole frontage still in
front of it, whichever way the car came. That is what `GEN-4h` states.

**What was asked for first, and why it is not what was built.** The shape wanted was one node whose box
spans the frontage, with the bays as arms of it — a T junction with N perpendicular arms. Two things stop
it and both are worth writing down. A junction box is ground held by a *movement* and not by a lane, so a
box spanning a frontage is a frontage held whole while anybody manoeuvres on it; and a bay inside a box has
no lane to hand back to, because the departing lane starts at the box's edge and one reverse pass does not
reach it — the way out would end on the through join, and `PathAssembler` can begin a line at a lane and
nowhere else. Neither is a law of the model: a join *is* a lane in every respect but how it is drawn, and
the day the assembler can begin part-way along one, the box shape becomes available. It was not built here
because it was not needed for the node.

**A place is a cut and not a disc**, and that had three consequences the build found rather than predicted.
The two lanes of a place meet at a point, so the biarc between them has a chord of a fraction of a
millimetre — float noise off two sub-chains of one curve — and drawn rather than recognised it is two tiny
arcs of enormous curvature, which failed the corner test and set every lane at every car park back a metre
and a quarter (`RoadGraph.SameEndM`). A movement with no ground under it is not a movement, so a car does
not negotiate one: read as a box, every car park in the town put a junction across the street in front of
it and `P-8` was entered three times as often. And a slot in the book spent on a join of no length is a
slot the reservation has not got for the lane past it.

**What it costs, measured on Odesa over a minute.** Lanes 414 → 1704, the contracted network 412 → 1702
runs, and a mean run of 151 m → 36 m. The town itself drives the same: mean speed 3.84 → 3.83 m/s, 84 cars
stuck either way, the same two arrivals, and the trip probe within one of itself on every column. What
does move is the ladder — places given up 32 → 66, reroutes 9 → 26, legs settled 8 → 16, and the
`P-4 ↔ E-6` shuttle 15 → 48. The likely reason is that a reroute surcharges the one link a car is stuck on
and a link is now a quarter as long, so the alternative it finds rejoins the same street just past the jam.
It is left standing rather than tuned, because it is the ladder's figure and not the section's, and because the
counters that say whether the town works did not move.

## 2026-08-24 — a bay is two ways of the road's book

The last dozen metres of every leg were outside the ways altogether — the route stopped at a staging place
three car lengths short, `P-14` laid a fresh template from wherever the car had actually got to, and the
ground that template swept was held by projecting the body onto whatever lane happened to be nearest, from
both ends, every tick.

The fault: **a car park was somewhere the town's own mechanisms did not reach.**

So the two lines at a bay are laid once with the town and are ways of the road's book — arcs, a length,
metres of their own, and a row in the table of what is driven over what. Two things follow, and each of
them deleted code rather than adding it.

- **The route reaches the bay.** The way in is threaded onto the end of the leg's line by the same
  assembler that threads a junction's joins, so `LastLaneToM`, `IsTheApproachLane` and the staging place
  are gone; `P-14` drives on down the chain it was handed instead of laying geometry, and keeps the
  template only as the recovery from a pose the route did not choose.
- **The traffic is held off it by the table.** A car working into a bay crosses the oncoming lane, and
  which metres of that lane it takes is measured once, by the code that measures a junction
  ([`LineOverlap`](../../road/LineOverlap.cs)) — so the projection-onto-the-nearest-lane reading is gone
  with it, and with it the `NearestLane` per parked car per tick that pass used to cost.

**The stretch is the tail of the body and not half a car either way**, and that distinction is worth a
paragraph because it cost a day. Every way is drawn for the rear axle and every one of them meets it at the
bay's own pose, so everything of the car that is not behind the axle is nose-deep in the bay, past the end
of each of them. Read as half a car either way, a parked body reached back into the metres of its way that
the street is driven over, and every parked car in the town held the street it was parked beside.

**It is the tail as a floor and the table as the ceiling** ([`BayStandings`](../BayStandings.cs)). What a
parked body may hold is every metre at the bay's own end of its ways that nothing else in the town is driven
over — 1.80 m of way on Odesa against the tail's 0.60 m, 2.20 m on River, and no bay anywhere falling back
to the floor. It is free because a stretch nothing crosses can cut nobody's grant: the manoeuvre bench, the
trip probe and the soak are identical to the metre either way.

**The whole bay was asked for first, and it cannot be had.** Laid from the mouth in, the stretch takes
0.77 m of the ground the lane itself is driven over, because a bay's mouth stands half a metre off the
carriageway's edge and a crossing is measured at a body's width. Every parked car then cut the street beside
it and every neighbour's way in with it: on Odesa, places given up 24 → 99, reroutes 17 → 51, and cars that
got parked 17 → 4. **So the picture of a taken bay comes off the register instead** — the overlay draws the
bay itself, washed for a body standing in it and outlined for a booking, which is a fact about a place and
was never a stretch of road ([app/debug](../../../app/debug/docs/requirements.md)).

## 2026-08-24 — the departure is a movement, and the booking is a register

The way in was a way of the book and the way out was not. `P-2` laid the exit shape from the pose the car
was standing in, and what held the street off it was the sweep plus a claim on the lane at the mouth,
decided by a gap probe of its own: a look down the lane, a time-to-arrival, a give-way patience and a
random beat to stop two neighbouring bays taking the same gap. Beside it, the bay a leg was aimed at was
held as a claim on the way in — a claim unlike every other in the town, because it was not conditional on
anybody being at the wheel.

Both were the same fault from opposite ends: **the town already knows what a car crossing a stream of
traffic does, and a car leaving a bay was doing it by hand.**

- **The way out is driven.** A line may now *be* one of the town's ways (`CarFleet.LineWay`), so the car's
  reservation runs along it, what is in front comes off the index, and its grant is cut by the table. The
  sweep, the gap probe, the patience, the beat and the clock the wait was excused from are all gone; what
  is left is the entry's `Sa` and three lines of driving.
- **A movement is a way and not a turn.** `CarFleet.MovementWay` names the way a car is committed to
  crossing on, so the take-it-or-stop-short protocol, the runs it holds and the giving-back as the body
  passes serve a bay's way out and a junction's join without knowing which they have.
- **The booking is a register, and says so.** It is not a claim on ground, because the hold begins when the
  walker sets off and there is no line to hold ground along yet. It lives in `ParkingRegistry` with an index
  of where the bays stand, and it says which bay and nothing else.
- **Which way out a car takes is the lane already running its way.** Every way out of a bay begins at the
  same pose in it, so the only thing that tells them apart is the lane each lands on; taking the other one
  is a leg that starts by driving to the next junction to turn round.

Odesa over the measured minute, against the state before any of the bay ways existed:

| Odesa | before | after |
|---|---|---|
| emergency stops (`E-2`) | 121 | 41 |
| back-offs (`E-3`) | 26 | 10 |
| swerves (`E-4`) | 5 | 0 |
| reroutes (`E-7`) | 19 | 9 |
| cars abandoned (`E-10`) | 14 | 1 |
| stood parked (`P-17`) | never entered | 6 |
| searches per leg | 1.9 | 1.2 |
| places given up (`E-6`) | 28 | 32 |
| legs settled for (`E-9`) | 1 | 8 |

**The last two lines are the price and they are the patience.** A car in a bay used to take the gap anyway
after twenty seconds; now it gives way like anything else at a junction, so a leg on a busy kerb waits, and
some of those legs end in the place being given up. That is the same trade every yield in the town makes,
and it is the yield's to tune rather than the bay's to special-case.

**A parked neighbour can still stand in a way out, and it is not fixed here.** A car backing out of one bay
sweeps past the mouth of the next, and the town measures that as ground the neighbour's own way is driven
over — so the last metres of that way, which is where a parked body lies, read as taken. What makes it a
measurement rather than a fact is `LineOverlap`'s conservative reading: a section runs from the first
contact to the last, and two bays share a lot's own kerb at four metres. It costs nothing while a row is
half empty and it will bite a full one.

**What it is not is a node.** The global tier never hears about any of this: a car park is still a place on
a link and not a node ([routing](../../routing/docs/requirements.md)), the travel graph is unchanged, and
the contraction that makes the search cheap is untouched. What changed is the local tier's line and the
book underneath it. A node per bay cannot be laid at all — the way in needs three car lengths of run-in
where bays stand four metres apart, so its join would have to span its neighbours' nodes, which `TER-5d`
forbids — and a node per lot is a change to `TER-4` and `TER-6` before it is a change to any code.
