# The car agent — requirements

The driver and the body it drives: what a car *is*, what the driver does with it, and the standing rules
`S-1…S-7` every tick of one answers to.

## What a driver is

**CAR-15** `P4` **A driver holds a line and nothing else.** It is given a chain of the town's own ways by
one search of the network (`world/routing/`), it is held on each in turn, and the leg is laid again from
wherever the body has got to when a chain runs out — **which is the walker's own tick** (PER-25) and the
same code at the tier where the two are the same thing. **A driver lays no geometry**: every line it
drives is the town's lanes and the joins between them, or one of the town's own ways at a bay. What a
driver has that a walker has not is three things and they are named: the **line assembled over the next
few lanes**, because a car at road speed must see the corners a walker takes one stride at a time; the
**gear**, because a bay's ways are driven in whichever one they were laid for (GEN-4j); and **a light**,
which holds its clock rather than spending it.

**CAR-15a** `P4` **A leg that covers no ground is given up.** The patience is one clock over both agent
kinds (`World.Routing.LegProgress`), measured against what is left of the way the body is on and never
against the point the wheel is aimed at. Past it the road is priced up and the route laid again; past the
last reroute the place is given up for another near where the car got to; past that the leg is over and
the car is stood down where it stands. **There is no other exit**, and it is finite because the clock is.

**CAR-15b** `P4` **What a driver is doing is which line it is on and which term bound its speed**
(`DrivingHold`). There is no name beside those to drift from them, no state to be in and no step to be
recorded: which part of a leg a car is at is read off where it is standing — a bay's way under it, a
bay's way in front of it, or the road.

## What a car is and does

**CAR-1** `P5` A car **acts only while something is driving it and it is intact** — an errand (SRV-3), an
order (CTL-8d), a hand at the wheel (CTL-5) or the tour of a map with no bay a car can reach (CAR-8). Nobody
boards a car, so none of those is a driver in a seat. A car nothing is driving, or a broken one, is not an
agent.

**CAR-2** `P4` A car contains at most one driver.

**CAR-8** `P4` A car draws no destination of its own: **where it goes is whatever is driving it** — an
errand's place (AMB-5, EVA-3, SRV-5, SRV-6) or an order's goal (CTL-8). On a map with no bay a car can reach,
every car nothing else is driving takes the lane it stands on and tours the lanes with no destination at all
(`TownWorld.DriveTheEmptyMap`, `LaneTour`).

**CAR-3** `P4` Actions: set steering angle; select gear, forward or reverse; set longitudinal acceleration
between the braking and drive bounds; handbrake.

**CAR-3a** `P3` **Neither control arrives in the tick it is asked for.** The pedal travels and so does the
wheel: what a driver sets is what it is *asking* for, and what the body carries out is as far towards it as
the rack and the linkage got in a tick. Both rates are the car's own, and **both bind a hand at the wheel
exactly as they bind a follower** — a rack is a fact about the car and not about who is turning it. A wheel
that arrived instantly would let any driver select full lock at speed, which is a circle the tyres cannot
hold and a front axle saturated for the whole of the corner.

**CAR-3b** `P3` **The throttle is bounded by what the patch has left, not by what the engine has.** A driven
axle may be asked for the friction circle's remainder along the roll once the corner the car is *actually* taking
has been paid for; past that, throttle buys no acceleration and only takes grip off the turn. This binds
whoever is at the pedals. What does not is the self-driver's own lift while its tyres report a slide, which
is a driver keeping out of trouble rather than a fact about rubber — flooring it stays the player's to do.

**CAR-3e** `P3` **One coefficient of friction, at every load and in every direction.** A patch is worth what it
is carrying and nothing else, so a stop and a corner of the same car are worth the same and a transfer costs
the four wheels nothing between them. What the loads decide is **which wheel runs out first** — a rear gone
light under the brakes, an inside wheel gone light in a corner — and never what the car holds in total.
**Any figure that separates a stop from a corner is a fudge**: the mechanisms that would separate them
honestly are each worth about a per cent from the height a town is watched at.

**CAR-45** `P4` **A car's pedal is authored against what its own driven tyres can put down, and never as an
acceleration.** What the engine asks for is a multiple of what the driven axle holds at the **static** load —
the load a car pulling away stands on, before it has transferred anything — so a variant states how far past
its rubber its engine reaches and the acceleration is what that comes to. Under one, nothing it does spins a
wheel; over one, the pedal lights the driven axle up somewhere short of the floor, which is a fact about that
car worth authoring. What a figure in m/s² cannot be is **an input**: above the traction limit it buys no
acceleration whatever (CAR-3b), so a pedal authored past every tyre in the fleet describes nothing that ever
happens to the car, and every metre per second it gains is the compound's. It also cannot be recovered by
moving the compound — a friction coefficient solved backwards out of a wanted acceleration is the one thing
this project's figures may never be.

**CAR-4** `P3` Steering changes heading **only as a function of travel and steering angle** — a stationary car
does not rotate.

**CAR-4a** `P4` Every driven line is a line for the **rear axle**, the one point on a car that travels the way
the car is pointing, and every pose that meets a line is measured to it. The middle of the body crabs,
and the tightest circle it can hold is `√(R² + d²)`. A line drawn through the middle of the car at
the car's own minimum radius is therefore not merely hard to follow but **impossible**, and the car rides
it `atan(d/R)` out of square all the way in.

## The line is a recommendation, and the car is its own

**CAR-10** `P4` A driven line is a **route the car is asked to follow and never a rail it is placed on**. What
the town precomputes — the lanes of a route, the way in and out of a bay — exists so that a driver does not
search the road network for every metre it covers; how that line is actually driven is the car's own, tick
by tick: what speed to hold, when to turn the wheel and how far, when to wait and when to go. Nothing above
the tyres moves a body.

**CAR-10a** `P5` A car therefore **deviates from its line and is expected to**. What binds it is the ground it
may be on (CAR-6.2), the road it was granted (S-2a) and the paint it owes (TER-5e) — never the metres of
the line itself. A car far enough off its line that it is no longer driving it is a recovery (CAR-9) and
not a correction applied to the body.

**CAR-10b** `P5` Where the town's own geometry does not begin where the car that turns up is standing,
**the car is seated on it where its axle actually is** and drives on from there. A bay's ways are laid for
the nominal car and a body stops with its nose at the end of the line it was given, so neither end of one
is where a particular car's axle stands; the line is taken from the metre the body projects onto, over
that way's first car length and no further. **A car is never shuffled onto a line to make a precomputed
one fit**, and it never draws a shape of its own to reach one either (CAR-15).

**CAR-11** `P4` A car is driven by **its own body**: its footprint and mass, where its axles sit under it, how
wide its track is, what its tyres hold, and what its gearing and brakes are worth. Every one of those is
the variant's, and every decision taken for that car — the wheel, the pedals, the road it asks for, the
gap it keeps — is taken against them. **A fleet whose cars differ only in
their pictures is a fleet of one car.**

**CAR-11a** `P4` The **town's geometry is the nominal car's**: lane widths, junction radii, bays and the ways
laid into and out of them are sized against `SimConfig`'s own figures and are the same for whoever turns
up. **No town stands that car**: the one fleet laid on its figures is the crash sandbox's
(`CarBuilds.OfTheNominalCar`), whose cars differ in drive layout and in nothing else.

**CAR-11b** `P5` A space is painted for the nominal car with a margin either side, and a body longer or wider
than that is one parked across the aisle behind it. **Whether a body fits is answerable and nothing asks
it** (`ParkingRegistry.Takes`): no bay is refused a car for its size.

**CAR-12** `P4` A car's **tyres stand outside its bodywork**. Its track is the width of the panels over its
axles — measured off its own picture, mirrors ignored — so each wheel centre stands on a flank and at least
`Tyre.ShowsPastTheBodyworkShare` of every tyre's width is outside the body drawn over it. This is geometry
and not decoration: those same offsets are where the four patches take the ground and where the impulses
are spent, so a track authored to hide the wheels is a car cornering on a narrower base than it looks like
it has. **What a car collides with is still its bodywork**: the rubber standing proud of the box is drawn
and driven on, and is not a second hull. **And the silhouette that is measured is the bodywork alone** — a
sheet carries no fragment standing off the body it was cut from, because such a fragment is both a bright
fleck beside the car on screen and a false flank for a track to be authored against.

**CAR-12a** `P6` A car is **drawn at the footprint it is simulated at** — its own, never the nominal car's. The
picture is what says where the panels end, so a body stretched to another car's size is one whose tyres,
mirrors and overhangs are all in the wrong place.

**CAR-12b** `P3` A car is **collided as a rounded box fitted inside the picture of it** — the largest one that
lies within the bodywork — and never as the footprint that picture was drawn in. A footprint is a
rectangle art is drawn into: its corners are empty, and its width is set by whatever reaches furthest,
which on a police car is the mirrors. Collided at the footprint a car is stopped by a car it visibly is
not touching, half a car's width of it. The fit is measured off the art and authored beside it, it is
**inside the panels and so inside the mirrors and the tyres**, and it takes about a twentieth off the
length and a tenth off the width. A variant that names no fit is collided at its footprint with square
corners, and so is the nominal car, which is a figure rather than a picture and has nothing to be fitted
inside.

**A car is still drawn, driven, parked and measured at its footprint** (CAR-11, CAR-12a). What the fit
changes is one thing: the shape the solver is handed.

## Soft rules

**CAR-6** `P5` The driver's soft rule set. Each is an intention it can fail to keep, and a failure has a
defined recovery (CAR-9) rather than a correction applied to the body.

**CAR-6.1** `P5` Do not intentionally collide with any object.

**CAR-6.2** `P5` Move only on drivable terrain, and on directional terrain only in its permitted direction.

**CAR-6.2a** `P5` On non-directional drivable terrain heading is unconstrained, but the car must enter from and
leave to legal ground.

**CAR-6.2b** `P5` The centreline may be crossed into the oncoming lane **only to pass a stationary obstacle**.

**CAR-6.3** `P5` Do not cross a red car light.

**CAR-6.4** `P5` Do not idle, except in a parking space, while obeying a signal, or while waiting for other
agents.

**CAR-6.5** `P5` Reverse **only along one of the town's own ways at a bay**, in the gear that way was laid
for (GEN-4j). There is nowhere else in the town a car goes backwards.

**CAR-7** `P5` Yielding to another agent that blocks the path is legitimate idling and is the normal way cars
resolve conflicts.

**CAR-7a** `P5` A car must yield to any agent already inside the intersection or on a crossing it is taking.

**CAR-13** `P5` **A small share of people do not keep the courtesies**, and which ones is drawn once when the
person is made and holds for the rest of the run. It is a fact about the **person and not the car** — the
same car is driven past a red by one owner and held at it by the next — so it changes nothing until they
take a wheel, and the share is `SimConfig.Driving`.

**CAR-13.1** `P5` Exactly one of the soft rules is dropped: **CAR-6.3**, the red. Nothing else is, and the
list is closed — a driver who does not keep the rules is not thereby exempt from them.

**CAR-13.2** `P3` **A body is never one of them.** Somebody already on the paint, a wreck, a queue, the ground
another movement has committed to and the hazard the profile brakes for all bind a reckless driver exactly
as they bind anybody else. What the habit removes is a courtesy owed to whoever has not started; what
follows from it is a matter for the geometry and the tyres, and never a licence.

**CAR-13.3** `P8` **A red they cross is a violation**, which is the whole of how this differs from AMB-4.2. An
ambulance is exempt from the rule and cannot be in breach of it; a reckless driver is in breach and is
counted, so the count and `TownWorld.RecklessDrivers` are read together and neither means anything alone.

## Recovery

**CAR-9** `P5` A car that is no longer driving the line it was given **stops and takes the lane it is
actually standing on** — the shortest way back onto the network, which is the walker's own answer to the
same state said of a driver (PER-25). Nothing is placed and nothing is corrected: the follower steers the
body back onto that lane's line like any other.

**CAR-9a** `P5` Where no lane runs the way the body is pointing, the car covers no ground and **the leg's own
clock ends it** (CAR-15a): it is stood down where it stands, on its handbrake, which makes it no longer an
agent (CAR-1) and exempt from the stuck-agent check (VER-3). Nobody gets out, there being nobody in it.

## The standing rules

These run underneath every tick of every car, and **they are not a step of anything**: they are how a car
is driven at all, and they live in `world/town/TownWorld.Driving.cs`. Nothing selects them and nothing
may repeat them.

**S-1** `P5` Hold the driven line, measured at the rear axle, aiming a speed-scaled distance ahead and
never further than the corner being driven is wide.

**S-2** `P5` Speed is the minimum of every constraint — the gear cap, the corners, the end of the line, the
headway, **the road the car was granted**, the stop point, the stop short of a crossing and the place the
car was sent to — every distance taken a lead ahead of where the car is, against *usable* grip. The lead
is the staleness of the driver's own decision and the travel of the pedal that answers it.

**S-2a** `P3` **Take the road ahead before driving down it.** Every tick, a driver plans the stretch of
its own line from its nose (TER-4c.1) to where it means to be able to stop, and is granted what is left of
it in front of the first body on it and short of ground another plan keeps. Nobody is granted ground
another car is standing on, and **that is the whole of following**: the car behind has less road to stop
in and holds the speed that road affords. **The grant alone is read at a following time** rather than at
the lead above, which is what settles a queue at the standstill gap and a second of travel rather than at
a tenth of one — **and that time is kept from what is being followed and from nothing else**: a grant cut
at a wreck, at somebody on foot, at ground somebody has claimed or at the place two movements meet
already ends the asker's own margin short of it, and a second of travel on top of that is a car holding a
street shut at speed for something it needed only to stop short of. **And it is cut by the secondary claims
other plans place on its ways where theirs cross them** (TER-5c.1), so the grant means one plan to a piece of
ground across a junction and not only along a lane. **What is asked for stops where a rule stops the
car** (TER-4c.1) — a red, a bar, a zebra it must stop short of — the gap it keeps included, so a car
standing at a stop holds the ground it is on and none of what it stopped for.

**S-3** `P5` Watch ahead along the line actually being driven, in the gear it is being driven in. **Every
tick, and out of the town's own claims** — what is in front, what it is and how far off it is are one walk
of the ways being driven, over the same stretches the grant in S-2a was taken against, so the reading
and the road the car was given can never disagree. **Everything that can be on a lane has claimed it**:
the traffic, anybody on foot in it, and the town's own furniture (TER-4c).

**S-4** `P3` Take up the ground **on your own way through** the box ahead, at the places the other movements
cross it, and give back the box behind (TER-5c). Every tick, never on the clock — a red can change
under a car, and nothing here is a claim on the junction. **What another movement's ground costs you
is read on your own ways** (TER-5c.1): a car places main claims on the ways it is going to be on and
secondary claims on the ways those cross, and reads nothing but its own. **And what it costs you turns on the right of way each of you has there**
(TER-5e): ground held by a movement that gives way to yours is ground you are not cut at, and ground
held by a body past the point it could stop short is ground nobody's rank takes. A crossing already taken
is **given back** when something with the right of way over it asks for the same ground — while this car
can still stop short of the box, and never after.

**S-5** `P5` Hold a stop you have already made: the handbrake is pulled only at rest.

**S-7** `P7` A hand at the wheel suspends all of it.

## The tyre model

A car actuates **nothing but a steering angle and a drive/brake demand**. Turning radius, drift,
stopping, pushes and collision response are all solver output
([world/physics](../../../world/physics/docs/requirements.md)).

One impulse per wheel, spent from a friction **ellipse**: side grip and rolling resistance are separate
quantities drawn from **one budget**, so a wheel already using its grip to turn has less left to brake
with. Each patch is weighed by the load its corner carries as the car pitches and rolls, so weight
transfer is a fact about the model rather than a fudge. Front wheels are **Ackermann**-steered; drive
force is placed by layout and divided by the **driven axle's** load, and bounded by what the ellipse has
left (CAR-3b); the handbrake locks the **rear** wheels only, so the back drags while the front pair keeps
rolling and steering. **An unmanned car holds its handbrake.**

**Which end the drive is placed on is the whole of what a layout is**, and it needs no rule of its own: a
front-driven car spends the steered axle's grip to accelerate, so the throttle takes from the corner
directly and the car limits itself; a rear-driven one spends the other axle's, accelerates freely, and runs
wide because the *front* pair runs out of grip at the speed it reaches. Neither is a defect and neither is
corrected. What answers them is a driver — the profile's own corner speed for one that drives itself, and
a wheel and a pedal that can be held part way (CAR-3a) for a hand.

How the budget is split between the two axes, the three guards in that split and the one snapshot every
wheel reads are `TyreModel`'s and `CarPose`'s own.

## What a car shows

**CAR-14** `P6` A car **says what it is doing with its lamps**, and every lamp is a fact the car already
holds: the pedal, the gear, the line in front of it and the priority it carries. **No lamp is state of
its own** — there is nothing to set, nothing to clear and nothing that can disagree with the car it is
bolted to.

**CAR-14a** `P6` A lamp is a **section of the car's own picture**, and the variant's file says which one: every
lens is measured off the art it is drawn on, so a lamp lights the panel an artist drew a lens on and
nothing arithmetic put somewhere near it. **A lit lamp is that section cut from that sprite and driven
emissive**, at the resolution the sprite is drawn at — it therefore wears the shape, the bezel and the
pixel grid the artist gave it, and cannot overhang an outline it was cut from the inside of. **An unlit
lamp is drawn by nobody**: the dull lens is the car's own picture and is already on screen. A variant
that draws no lens for a fitting cannot show it.

**CAR-14b** `P9` Where the art **already draws a lamp**, the lens is a section of that lamp and not a shape
beside it: a front cluster's indicator is the end of it nearest the flank, so what flashes is the part
of the light a car actually indicates with. A block painted onto bodywork is what a variant gets **only
where the art draws no lamp there at all** — it is a lens invented for a car that has none, and beside a
lamp the artist did draw it reads as a sticker on the paint.

**CAR-14.1** `P6` The **indicator announces the turn a car is about to make at the junction in front of it**,
and nothing else. It is shown only while a junction is **within reach** of the car and only where the
movement its own line takes through that junction is a **turn rather than straight on** — the road's own
classification of the pair of lanes the line joins (`TownWorld.JunctionStopM`, `CarFleet.TurningAtTheBox`),
so what a car announces and what it gives way to are one answer about one movement.

**A bend is not a turn.** A road of constant radius bends past any threshold for ever, and a car announcing
that all the way round a circuit is announcing something nobody can act on: there is nowhere else for it to
go. The same is true of a car going straight on through a crossroads, and of a car on a bay's own way
with no junction ahead of it at all.

**Which side is read off the geometry**: the side the line bends to over the stretch of it a driver would
be announcing. A line driven the way the rear axle travels rather than the way the car points — a bay's
way in reverse — bends the body the other way round, and is read in the body's frame. It is the **front
corner pair** that flashes.

**CAR-14.2** `P6` The **brake lamps are the pedal**: what the driver is asking of the brakes, never what the
tyres are doing about it. A car standing on its handbrake with nobody's foot down shows none.

**CAR-14.3** `P6` The **reversing lamps are the gear**, on the same rear cluster the brake lamps are — which
is the one lamp the art gives a car back there. **The pedal outranks the gear on it**: a car braking as
it backs out of a bay shows red, because what the driver behind has to read first is that it is stopping.

**CAR-14.4** `P6` The **coloured beacon is the priority the car is carrying (AMB-4) and never the vehicle** — an
ambulance not on a call and a police car on its beat (SRV-5) show nothing, and a car that is granted the
road shows it for as long as it holds it. **A hand at the wheel runs it too** (CTL-5c), and that one is
the picture alone. **The bar is never dark while it is on**, so a car everybody owes the road to is lit in
every frame it is in: a bar of two colours swaps them end for end rather than blinking, and a bar of one —
the amber a works vehicle carries (CAR-14.6) — burns its two ends in turn, each blinking against its own
dull glass. **A vehicle whose art draws a second bar runs it against the first**: the two carry opposite
colours at every instant, so the end a driver behind is looking at is changing whichever bar is in front
of them.

**CAR-14.5** `P6` A car **nothing is driving shows nothing lit**, on CAR-1: a lamp is something a car is doing,
and what a parked one shows is its own dull glass. **A hand at the wheel is one of the things that drives
one** (CTL-5c) — a car taken over from a stand shows its lamps from the tick it is taken, because standing
down is what it was doing until then. **A wreck shows nothing at all** — the lenses were
measured off the car it was and not the crumpled picture it now wears.

**CAR-14.6** `P6` An **amber bar is the work and never the priority**. It is up for as long as the vehicle is
out on the job it exists for — an evacuator from the tick it takes a wreck until it is back in its own bay,
both ways round and through the standing still between them — and it is up whether or not the town is
giving that truck the road (EVA-4). **It grants nothing**: no movement gives way to it and no rule of the
road reads it. What it says is that there is a truck working in this street.

Where the numbers are: `SimConfig.Lamps` — how much of the line is read for a turn and how far that
stretch must bend, the rates the flashing ones flash at, and how far the light around a lit one spills.
**Neither where a lamp is nor what it looks like is a number here**: where is the variant's own file,
beside the picture it was measured off, and what is the town's one lamp sheet, cut from those same
pictures (CAR-14a).

## Marks

A wheel leaves a mark when it is worked past the surface's own threshold. Slide is tracked **per axis**
and spin **per wheel**, so a locked rear axle and a spinning front pair draw different marks. A parked car
with its handbrake on does not scrub the road. Surfaces that **plough** carry a floor under the mark
instead: ploughing is displacement rather than friction, and priced as power it would die with speed, so
a car creeping onto a lawn would leave it pristine.
