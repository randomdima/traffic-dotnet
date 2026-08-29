# The ambulance — decision log

## 2026-08-27 — the standoff is a place on the lane, and the crew's reach stopped doing two jobs

Before AMB-10 there was one figure and it was answering two questions at once: how near the casualty
`P-18` parks, and how near the crew can work from. Aiming the leg at the body and then calling ten metres
"reach" is what put an ambulance on top of every accident it went to.

**Splitting them was most of the work.** The leg is now aimed at a place ten metres back **along the lane
the body lies beside** — the truck's own hitching place (`EVA-5`) measured the same way and for the same
reason, since a vehicle can only arrive along the road and a standoff measured as a radius lands on a
pavement or the far carriageway as often as not. What is left of the old figure is a tolerance on settling
there, and it kept its width: what covers the last of the distance is somebody walking it, so how precisely
the vehicle parks stopped mattering.

**The crew walked into its own ambulance for an afternoon.** A hand is put out through `PHY-7a` like any
other exit, and the way in a walker is aimed at is the flank *away from the traffic* — while a casualty
lying in a carriageway is on the traffic side. So the paramedic was set down on the far side of his own
vehicle and then walked straight at it for the whole of the call, because the follower steers at its aim and
goes round nothing (PER-13). The door is chosen by the work now: a point clear of the footprint in whichever
direction the thing being reached for lies, at the larger half-extent so it is outside the body whichever
way the vehicle is pointing.

**The tug is a placement and not a coupling.** A tow is two vehicles the solver holds together with an
impulse and its opposite because both have mass, wheels and a line to scrub against; a person with somebody
over their shoulder has none of that, and a spring between two walkers would be a joint nothing else in this
town has. Setting the casualty down a stride behind the walker on every decision is `EVA-5`'s winch said of
a body, and it keeps the pair hittable, which is the half that matters.

## 2026-08-25 — a hospital wears its own roof, and that roof is fitted rather than matched

An ordinary roof is picked by the nearest authored footprint, which is a rule about size and knows nothing
about use. Left alone, the hospital picture would land on whichever building happened to measure 18 by 16.6
metres and the actual hospital would wear a warehouse. So the hospital and the police station roofs were
taken out of the catalogue `Match` draws from and put in a second list nothing but a use can reach — the
same two-lists-one-array shape the car catalogue keeps its service vehicles in.

That leaves the size problem the other way round: the map picks the building, so the picture has to sit on
whatever plot it landed on. It is **fitted inside the plot on its own aspect** rather than stretched to it,
because a stretched roof leans and a fitted one merely leaves a margin. The alternative — draw hospitals
only from buildings near the picture's own size — is a use that clusters on one size of building and a
village with a building on it that has no hospital, which AMB-1 says it must have.

Fitting then takes a second decision with it. A matched roof is laid across the building's axes or along
them by **which way round the picture is nearer the plot's own shape**, and the half turn after it picks
between the two walls that leaves; a fitted roof has no such answer, and taking the quarter turn from
which way round the picture came out bigger put the fixture town's police station door on a side wall with
its sign reading down the street. The quarter turn is the pavement's instead — the pair of walls the
plan's ways in sit off — and the half turn goes on doing what it did.

## 2026-08-25 — the nearest ambulance is measured against the other ambulances, not the other casualties

AMB-5 has always read "the nearest ambulance with nothing else to do takes it", and what the code did was
ask each waiting crew for the nearest *casualty* to itself. With one ambulance to a hospital the difference
rarely showed. With an apron of four it showed immediately: the call went to whichever crew's decision ran
first in the tick, which on Odesa was regularly an ambulance two kilometres away while three idle ones
stood a street from the body.

The fix is one question asked before the call is taken — is any other free ambulance nearer to this
casualty — and a crew that is not the nearest takes nothing and asks again on its next decision. It cannot
deadlock: the tie is broken on the car's index, and a crew that defers is deferring to one that will either
take this call or take a nearer one and free this one up. It is a walk of the fleet, asked only while
somebody is lying in the road.

## 2026-08-25 — the blue light is a rank on the road, not a mode in the driver

Every way an ambulance differs from ordinary traffic is written where that difference belongs: the rank a
stretch is laid with, the red that stops applying, the kerb that is not given way to, the patience an
overtake no longer waits out. The driver holds one boolean and the catalogue sees one field.

The load-bearing half is that `RightOfWay` already ordered ground and already said what a rank may take —
a claim, and never a body or the road a body is committed to stopping in. Inserting `Emergency` between the
paint and `Committed` therefore made an ambulance absolute over *who waits* and changed nothing about who
may be driven into. A mode in the driver would have had to re-derive that property at every site, and would
have got it wrong at one of them.

## 2026-08-25 — a rescue is capped at its own pace, because the road no longer caps it

Uncapped, an ambulance on River reached 75 m/s — the gearbox's own ceiling — and wrecked itself with the
casualty aboard. Nothing in this town posts a speed limit: what holds a car's speed down is the corners,
the queues, the reds and the crossings, and a driver exempted from three of those has only the corners
left.

So the pace is a figure about the *ambulance* rather than a rule about the road, and it sits well above what
the traffic manages so that overtaking is still worth doing. It is the honest statement of AMB-4a: the blue
light buys the road and never the tyres.

## 2026-08-25 — the approach belongs to `P-4` and only the last few metres to `P-18`

`P-18` was first written to own the whole run-in, and an ambulance held forty metres short of its casualty
by a stopped van stayed there: the entry has no swerve of its own, and `E-4` is reached from `P-4`. Handing
over only once the place is inside the road the car needs to stop in leaves the entry that knows how to get
past things in charge for as long as there is road to do it in.

The same episode is why `P-18` is watched although standing still is what it does. The crew's work is
seconds and the fuse is half a minute, so the watchdog cannot fire on an ambulance doing its job — and it
catches the one thing the call's own clock is far too slow for.

## 2026-08-25 — a casualty on a zebra is a body and not somebody crossing

The driver's "is there anybody on the paint" read every walker's stretch of the lane, so a body knocked down on
a crossing held the traffic short of that crossing for ever — including the ambulance coming to fetch them,
which is a deadlock made entirely of bookkeeping. The question now asks for the crossing's own right of way
(`RightOfWay.OnThePaint`), which a body lying in the road does not hold.

Nothing about the casualty's safety changed: their stretch of the lane still cuts every grant that runs over
it, so a driver is held off them exactly as off a wreck. What they stopped being is somebody a car must stop
*short of the paint* for, which is a courtesy owed to people who are walking.

## 2026-08-25 — the scene is projected forward along the line, and "behind the axle" is settled by the crew's reach

A route out of a bay regularly begins beside the very body it is going to fetch and then runs the other way
round the block. Projected onto the nearest point, the stop was behind the car from the first tick, and the
ambulance held still fifteen metres past a casualty it had never reached — for the whole of a run.

Searched forward from where the car is, the answer is the next time this line comes past the body, which is
a distance the speed profile can brake against. But "forward" was then enforced as *anything behind the axle
is not a place at all*, and that is a different rule with a cost of its own: the instant the axle drew level
with the body the distance went to infinity, `P-18` reported its errand over and the ambulance drove away.
The entry's own "past the place is still at the place" branch could never run, because the only value that
would have reached it was the one being turned into infinity first.

So the two cases are told apart by **the crew's reach** rather than by the sign of the distance. A place
astern by less than the reach is where the crew actually works and the car holds there; a place astern by
more than it is the body this line comes round to later, and is no more a place to stop at than it ever was.
Both readings come from one measurement and the old failure cannot come back. What it was worth is the whole
slice: before it, one shipped city delivered a casualty and two did not.

