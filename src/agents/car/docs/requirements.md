# The car agent — requirements

The driver and the body it drives. **The manoeuvre catalogue is not here**: `P-*` and `E-*` are
[maneuvers/](../maneuvers/docs/index.md) — one file and one page per entry, the framework rules `MAN-1…7`
and the standing rules `S-1…7`. What is below is what a car *is*, which is what the catalogue is written
against.

## What a car is and does

**CAR-1** A car **acts only while it contains a driver and is intact**. A driverless or broken car is not
an agent.

**CAR-2** A car contains at most one driver.

**CAR-8** A car has no goals of its own; its destination is its driver's.

**CAR-3** Actions: set steering angle; select gear, forward or reverse; set longitudinal acceleration
between the braking and drive bounds; handbrake.

**CAR-3a** **Neither control arrives in the tick it is asked for.** The pedal travels and so does the
wheel: what a driver sets is what it is *asking* for, and what the body carries out is as far towards it as
the rack and the linkage got in a tick. Both rates are the car's own, and **both bind a hand at the wheel
exactly as they bind a follower** — a rack is a fact about the car and not about who is turning it. A wheel
that arrived instantly would let any driver select full lock at speed, which is a circle the tyres cannot
hold and a front axle saturated for the whole of the corner.

**CAR-3b** **The throttle is bounded by what the patch has left, not by what the engine has.** A driven
axle may be asked for the friction circle's remainder along the roll once the corner the car is *actually* taking
has been paid for; past that, throttle buys no acceleration and only takes grip off the turn. This binds
whoever is at the pedals. What does not is the self-driver's own lift while its tyres report a slide, which
is a driver keeping out of trouble rather than a fact about rubber — flooring it stays the player's to do.

**CAR-3e** **One coefficient of friction, at every load and in every direction.** A patch is worth what it
is carrying and nothing else, so a stop and a corner of the same car are worth the same and a transfer costs
the four wheels nothing between them. What the loads decide is **which wheel runs out first** — a rear gone
light under the brakes, an inside wheel gone light in a corner — and never what the car holds in total.
**Any figure that separates a stop from a corner is a fudge**: the mechanisms that would separate them
honestly are each worth about a per cent from the height a town is watched at.

**CAR-45** **A car's pedal is authored against what its own driven tyres can put down, and never as an
acceleration.** What the engine asks for is a multiple of what the driven axle holds at the **static** load —
the load a car pulling away stands on, before it has transferred anything — so a variant states how far past
its rubber its engine reaches and the acceleration is what that comes to. Under one, nothing it does spins a
wheel; over one, the pedal lights the driven axle up somewhere short of the floor, which is a fact about that
car worth authoring. What a figure in m/s² cannot be is **an input**: above the traction limit it buys no
acceleration whatever (CAR-3b), so a pedal authored past every tyre in the fleet describes nothing that ever
happens to the car, and every metre per second it gains is the compound's. It also cannot be recovered by
moving the compound — a friction coefficient solved backwards out of a wanted acceleration is the one thing
this project's figures may never be.

**CAR-4** Steering changes heading **only as a function of travel and steering angle** — a stationary car
does not rotate.

**CAR-5** Reverse has its own much lower speed cap; acceleration and braking are bounded.

**CAR-4a** Every driven line is a line for the **rear axle**, the one point on a car that travels the way
the car is pointing, and every pose that meets a line is measured to it. The middle of the body crabs,
and the tightest circle it can hold is `√(R² + d²)`. A template drawn through the middle of the car at
the car's own minimum radius is therefore not merely hard to follow but **impossible**, and the car rides
it `atan(d/R)` out of square all the way in.

## The line is a recommendation, and the car is its own

**CAR-10** A driven line is a **route the car is asked to follow and never a rail it is placed on**. What
the town precomputes — the lanes of a route, the way in and out of a bay — exists so that a driver does not
search the road network for every metre it covers; how that line is actually driven is the car's own, tick
by tick: what speed to hold, when to turn the wheel and how far, when to wait and when to go. Nothing above
the tyres moves a body.

**CAR-10a** A car therefore **deviates from its line and is expected to**. What binds it is the ground it
may be on (CAR-6.2), the road it was granted (S-2a) and the paint it owes (CAR-7b) — never the metres of
the line itself. A car far enough off its line that it is no longer driving it is a recovery (CAR-9) and
not a correction applied to the body.

**CAR-10b** Where the town's own geometry does not suit the car that turns up, **the car lays its own** —
a bay's way whose start is not where this car's axle stands is replaced by the same shape drawn from the
pose it is actually in. A car is never shuffled onto a line to make a precomputed one fit.

**CAR-11** A car is driven by **its own body**: its footprint and mass, where its axles sit under it, how
wide its track is, what its tyres hold, and what its gearing and brakes are worth. Every one of those is
the variant's, and every decision taken for that car — the wheel, the pedals, the road it asks for, the
gap it keeps, the shape it draws to park — is taken against them. **A fleet whose cars differ only in
their pictures is a fleet of one car.**

**CAR-11a** The **town's geometry is the nominal car's**: lane widths, junction radii, bays and the ways
laid into and out of them are sized against `SimConfig`'s own figures and are the same for whoever turns
up. The proving ground stands that car too — its cars differ in drive layout and in nothing else — because
a lap whose cars differ in every figure measures nothing.

**CAR-11b** A car **takes a bay its body fits in**. A space is painted for the nominal car with a margin
either side; a body longer or wider than that is one parked across the aisle behind it, and the bay is
refused before a line into it is laid.

**CAR-12** A car's **tyres stand outside its bodywork**. Its track is the width of the panels over its
axles — measured off its own picture, mirrors ignored — so each wheel centre stands on a flank and at least
`Tyre.ShowsPastTheBodyworkShare` of every tyre's width is outside the body drawn over it. This is geometry
and not decoration: those same offsets are where the four patches take the ground and where the impulses
are spent, so a track authored to hide the wheels is a car cornering on a narrower base than it looks like
it has. **What a car collides with is still its bodywork**: the rubber standing proud of the box is drawn
and driven on, and is not a second hull. **And the silhouette that is measured is the bodywork alone** — a
sheet carries no fragment standing off the body it was cut from, because such a fragment is both a bright
fleck beside the car on screen and a false flank for a track to be authored against.

**CAR-12a** A car is **drawn at the footprint it is simulated at** — its own, never the nominal car's. The
picture is what says where the panels end, so a body stretched to another car's size is one whose tyres,
mirrors and overhangs are all in the wrong place.

**CAR-12b** A car is **collided as a rounded box fitted inside the picture of it** — the largest one that
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

**CAR-6** The driver's soft rule set. Each is an intention it can fail to keep, and a failure has a
defined recovery (CAR-9) rather than a correction applied to the body.

**CAR-6.1** Do not intentionally collide with any object.

**CAR-6.2** Move only on drivable terrain, and on directional terrain only in its permitted direction.

**CAR-6.2a** On non-directional drivable terrain heading is unconstrained, but the car must enter from and
leave to legal ground.

**CAR-6.2b** The centreline may be crossed into the oncoming lane **only to pass a stationary obstacle**.

**CAR-6.3** Do not cross a red car light.

**CAR-6.4** Do not idle, except in a parking space, while obeying a signal, or while waiting for other
agents.

**CAR-6.5** Reverse **only as part of a manoeuvre** that owns it — entering or leaving a bay, squaring up
in one, backing off, working round on the spot.

**CAR-7** Yielding to another agent that blocks the path is legitimate idling and is the normal way cars
resolve conflicts.

**CAR-7a** A car must yield to any agent already inside the intersection or on a crossing it is taking.

**CAR-7b** A car **crosses paint at a crossing pace**, which is the car's own figure: arrived at like a
corner rather than held to from three streets away, and kept until the **tail** is off the paint rather
than the nose. It is owed on every line that crosses a crossing — a template of a manoeuvre's own as much
as a route — and it is lifted only where the crossing's own signal is holding the kerbs, read off the
pedestrian side of the table so that what a driver may do and what the people on the kerb were told cannot
disagree. **The stop short of a crossing somebody is on is not here**: it is the ground (TER-4c.1,
TER-5e), and stating it again would be a second gate on a movement that has already been refused (SIM-7).

**CAR-13** **A small share of people do not keep the courtesies**, and which ones is drawn once when the
person is made and holds for the rest of the run. It is a fact about the **person and not the car** — the
same car is driven past a red by one owner and held at it by the next — so it changes nothing until they
take a wheel, and the share is `SimConfig.Driving`.

**CAR-13.1** Exactly two of the soft rules are dropped: **CAR-6.3**, the red, and the stop owed at an
uncontrolled crossing to somebody **waiting** on the kerb (TER-4c.1). Nothing else is, and the list is
closed — a driver who does not keep the rules is not thereby exempt from them.

**CAR-13.2** **A body is never one of them.** Somebody already on the paint, a wreck, a queue, the ground
another movement has committed to and the hazard the profile brakes for all bind a reckless driver exactly
as they bind anybody else. What the habit removes is a courtesy owed to whoever has not started; what
follows from it is a matter for the geometry and the tyres, and never a licence.

**CAR-13.3** **A red they cross is a violation**, which is the whole of how this differs from AMB-4.2. An
ambulance is exempt from the rule and cannot be in breach of it; a reckless driver is in breach and is
counted, so the count and `TownWorld.RecklessDrivers` are read together and neither means anything alone.

## Recovery

**CAR-9** On a soft rule violation the car **stops**, then may move off-rule along the shortest path back
to legal ground.

**CAR-9a** If no such path exists, **the driver exits and continues the trip on foot**. The car is then
abandoned, which makes it no longer an agent (CAR-1) and exempt from the stuck-agent check (VER-3).

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

**The budget is split between the two axes on slip velocities, not on force demands.** A split on demands
weighs a lateral ask by the corner's load and a longitudinal one by the rim — two orders of magnitude
apart — and makes braking authority mid-slide depend on the tick rate.

**Three guards in that split must not be removed**, each of which cost real work to find: a **deadband**
on carried slip, or a car pulling away counts the same pedal twice and reports a slide it is not having;
**no overshoot** on the rim, or the wheel rings about road speed instead of settling; and the ellipse
boundary treated as a **ceiling, never a target**, or a wheel resyncing gets the whole budget.

**All four wheels read one snapshot of the body's motion taken at the start of the tick.** An impulse
applies immediately, so reading the live velocity per wheel makes the order the wheels are stepped in
break the axle pair's cancellation.

## What a car shows

**CAR-14** A car **says what it is doing with its lamps**, and every lamp is a fact the car already
holds: the pedal, the gear, the line in front of it and the priority it carries. **No lamp is state of
its own** — there is nothing to set, nothing to clear and nothing that can disagree with the car it is
bolted to.

**CAR-14a** A lamp is a **section of the car's own picture**, and the variant's file says which one: every
lens is measured off the art it is drawn on, so a lamp lights the panel an artist drew a lens on and
nothing arithmetic put somewhere near it. **A lit lamp is that section cut from that sprite and driven
emissive**, at the resolution the sprite is drawn at — it therefore wears the shape, the bezel and the
pixel grid the artist gave it, and cannot overhang an outline it was cut from the inside of. **An unlit
lamp is drawn by nobody**: the dull lens is the car's own picture and is already on screen. A variant
that draws no lens for a fitting cannot show it.

**CAR-14b** Where the art **already draws a lamp**, the lens is a section of that lamp and not a shape
beside it: a front cluster's indicator is the end of it nearest the flank, so what flashes is the part
of the light a car actually indicates with. A block painted onto bodywork is what a variant gets **only
where the art draws no lamp there at all** — it is a lens invented for a car that has none, and beside a
lamp the artist did draw it reads as a sticker on the paint.

**CAR-14.1** The **indicator announces the turn a car is about to make at the junction in front of it**,
and nothing else. It is shown only while a junction is **within reach** of the car and only where the
movement its own line takes through that junction is a **turn rather than straight on** — the road's own
classification of the pair of lanes the line joins (`TownWorld.JunctionStopM`, `CarFleet.TurningAtTheBox`),
so what a car announces and what it gives way to are one answer about one movement.

**A bend is not a turn.** A road of constant radius bends past any threshold for ever, and a car announcing
that all the way round a circuit is announcing something nobody can act on: there is nowhere else for it to
go. The same is true of a car going straight on through a crossroads, and of a car on a way or a template
with no junction ahead of it at all.

**Which side is still read off the geometry** and never off the manoeuvre: the side the line bends to over
the stretch of it a driver would be announcing, so no entry of the catalogue has to declare itself. A line
laid the way the rear axle travels (`E-3`, the bay templates) bends the body the other way round, and is
read in the body's frame. It is the **front corner pair** that flashes.

**CAR-14.2** The **brake lamps are the pedal**: what the driver is asking of the brakes, never what the
tyres are doing about it. A car standing on its handbrake with nobody's foot down shows none.

**CAR-14.3** The **reversing lamps are the gear**, on the same rear cluster the brake lamps are — which
is the one lamp the art gives a car back there. **The pedal outranks the gear on it**: a car braking as
it backs out of a bay shows red, because what the driver behind has to read first is that it is stopping.

**CAR-14.4** The **coloured beacon is the priority the car is carrying (AMB-4) and never the vehicle** — an
ambulance not on a call and a police car on its beat (SRV-5) show nothing, and a car that is granted the
road shows it for as long as it holds it. **A hand at the wheel runs it too** (CTL-5c), and that one is
the picture alone. **The bar is never dark while it is on**, so a car everybody owes the road to is lit in
every frame it is in: a bar of two colours swaps them end for end rather than blinking, and a bar of one —
the amber a works vehicle carries (CAR-14.6) — burns its two ends in turn, each blinking against its own
dull glass. **A vehicle whose art draws a second bar runs it against the first**: the two carry opposite
colours at every instant, so the end a driver behind is looking at is changing whichever bar is in front
of them.

**CAR-14.5** A car **nothing is driving shows nothing lit**, on CAR-1: a lamp is something a car is doing,
and what a parked one shows is its own dull glass. **A hand at the wheel is one of the things that drives
one** (CTL-5c) — a car taken over from a stand shows its lamps from the tick it is taken, because standing
down is what it was doing until then. **A wreck shows nothing at all** — the lenses were
measured off the car it was and not the crumpled picture it now wears.

**CAR-14.6** An **amber bar is the work and never the priority**. It is up for as long as the vehicle is
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
