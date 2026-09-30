# The car agent — requirements

The driver and the body it drives: what a car *is*, what the driver does with it, and the standing rules
`S-1…S-7` every tick of one answers to.

## What a driver is

**CAR-15** `P4` **A driver holds a line and nothing else.** It is given a chain of the town's own ways by
one search of the network (`world/routing/`), it is held on each in turn, and the leg is laid again from
wherever the body has got to when a chain runs out — **which is the walker's own tick** (PER-25) and the
same code at the tier where the two are the same thing. **A driver lays no geometry but at a bay**: every
line it drives is the town's lanes and the joins between them — on a pass, the lane it is on aimed across
into the lane beside (CAR-46), which is the town's too — **except its manoeuvre into a bay and out of one**,
which it lays itself from where it stands and on its own circle, because the owner asked for a car park that
lays nothing but its bays (GEN-4f). What a driver has that a walker has not is three things and they are
named: the **line assembled over the next few lanes**, because a car at road speed must see the corners a
walker takes one stride at a time; the **gear**, because a manoeuvre's pieces are driven in whichever one they
were shaped for (GEN-4j); and **a light**, which holds its clock rather than spending it.

**CAR-15a** `P4` **A leg that covers no ground is given up.** The patience is one clock over both agent
kinds (`World.Routing.LegProgress`), measured against what is left of the way the body is on and never
against the point the wheel is aimed at. Past it the road is priced up and the route laid again; past the
last reroute the place is given up for another near where the car got to; past that the leg is over and
the car is stood down where it stands. **There is no other exit**, and it is finite because the clock is.

**CAR-15b** `P4` **A car does one thing at a time, and the thing is named** (`CarAction`): following its route,
getting past something (CAR-46), backing up for the room to (CAR-50), getting into a bay or out of one (GEN-4f),
getting back onto its line (CAR-9) — or standing, under a hand (CTL-5) or on a bar (EVA-5). **An action is entered
and left in one place**, and what it leaves — a pass, a manoeuvre — goes with it; every claim a car lays and every
command it gives is its action's, and nothing works out what a car is doing from its line, its pass or its
manoeuvre. **Each knows its own ends**: what it is for, when it is done and what it hands over to. Which term bound
the speed is a second fact beside it (`DrivingHold`), read off the profile and never the action.

**CAR-46** `P5` **A car gets past what stands in its lane over the lane beside it**, as a pass
([TER-4c.6](../../../world/road/docs/claims.md)): asked for where its grant was ended by a body at rest that
is not making its own next movement — or, on a call, by any body at rest or traffic going slower than it means
to (AMB-4.4) — anywhere on its line, while the lane it is on has a lane running back beside it.

- **Along its own line, through a box as along a street**: the pass is the car's line moved across and back,
  and it is had wherever its ground is carriageway nobody holds — a node nothing turns at, cutting a street
  into short lanes, is no end to one. A stretch with no lane back (TER-4d) offers no pass.
- **Never over a zebra, but on a call**: somebody on the paint is somebody crossing, and a pass holding the rest
  of the zebra in front of them would stand them in the car's way with nowhere to go. A car on a call is above
  everybody on foot (AMB-4.4): its pass claims the paint whole, and it waits short of it for whoever is already
  on it.
- **Not short of a place in the road it was sent to** (AMB-5, EVA-3, SRV-6, CTL-8a): what stands before a car
  sent there is what it was sent to, and a pass ending past the place would drive it by.
- **Made for the room past what it passes**: the first stretch of the car's own line past it long enough to come
  back into — the step back, and the car's length and stand-off past that. Another body the car may pass standing
  in that room is passed too, and the room looked for past it. **Anything else standing there is waited for**:
  the room is still where the car means to come back, so it has decided on the pass (CAR-14.7) and waits until
  the room is its own.
- **Decided once, where the car would begin slowing for what it passes**, which is the place it has to choose
  between stepping out and slowing down — **and committed to**: drawn there in two shapes, from rest and, for a
  car still rolling, for the pace it came up at, each swept once for the ground it covers, and never drawn or
  swept again. The one drawn for its pace is had only while the car can still come up to where it begins at a
  pace it may be driven at; after that the car slows gently (`SimConfig.Driving`) and stands where the one from
  rest begins.
- **Waited for on its own clock**: asked for as it is decided and then every `PassAskEveryS`, and let go —
  to be decided afresh from where the car then stands — once what it passes is gone, has moved, or is no longer
  something it may pass, or once the car has waited `PassPatienceS`.
- **Turned into where it was drawn to begin**: the step out begins at the last place the car's body clears what
  it passes, so it is not over its own lane when it is level with it, and a car that has the pass short of there
  drives on to it at the pace the step was drawn for. **The step back begins as soon past what it passes as the
  body can come back**, so the car is swinging back in while its tail is still alongside, and off the lane beside
  as soon as its steps allow.
- **Each step no shorter than the car can drive at the pace it is drawn for, and driven no faster than its
  length allows**: one swing of the wheel and back, its bend never tighter than the lock or than the tyres hold
  at that speed, and changing no faster than the rack turns (CAR-3a) while the car rolls it. Where the line bends
  under the pass, what it bends is taken off what the step may. A car getting past from a standstill steps out at
  the pace below which a step at the lock is no shorter.
- **Laid as though the car pulls away along it**: the step back is drawn for the pace the car picks up by where
  it begins, at its own acceleration and up to what the road lets it plan for, and the straight between the two
  steps is the car's own to pull away along. A pass drawn for the pace the car came up at never slows it below
  that pace.
- **Waited for with room to step out, and no more**: behind a body going nowhere — at rest and not travelling
  the way on: a wreck, a car stood down, a car whose line ends where it stands, somebody standing in the road — a
  car stands where it could step out round it from a standstill, where the lane it is on has one back beside it,
  and keeps it in its plan while it stands. Everything else, a queue whatever it does next, is waited behind at
  the stand-off (S-2a) — **but by a car on a call**, which keeps that room behind anything at rest it may pass.
- **Driven as the pass bends**: the wheel is turned for the bend of the pass where the car is and corrected by
  pure pursuit of it (S-1) — pursuit alone turns into each step a lookahead early and cuts it — and a car on a
  pass is off its line by how far off the pass it is and no further (CAR-9).

**CAR-50** `P5` **A car too near what it means to get past to step out round it backs up for the room, and one
that cannot is going nowhere** ([TER-4c.7](../../../world/road/docs/claims.md)).

- **At rest, with the pass decided and nothing but its nearness refusing it** (CAR-46) — whatever it passes, a
  queue making another movement as much as a wreck: it backs down its own lane to where the step out it decided
  from rest begins. Where the room past what it passes is held by something it may not pass, it waits where it is.
- **Over ground it asks for behind its tail**, at the weakest rung there is, so whatever else wants that ground has
  it but a car queued behind it; **never behind the start of the lane it is on**, which is the box it came through.
- **Refused the whole of what it needs, it is blocked**: it does not move, and its body says it is going nowhere
  (TER-4c.2), so what comes up behind it keeps room to step out round it and may get past it and what it is stuck
  behind together. A line of such cars comes apart from its tail, where the room is.
- **How far it backs up is the pass's, decided once**, and the room behind it is a grant like the one in front,
  read off the reservations: asked for every rebuild while it rolls back, and on the pass's clock while it is
  refused.

## What a car is and does

**CAR-1** `P5` A car **acts only while something is driving it and it is intact** — an errand (SRV-3), an
order (CTL-8d), a hand at the wheel (CTL-5), or its own round or the tour of a map with no bay a car can reach
(CAR-8). Nobody boards a car, so none of those is a driver in a seat. A car nothing is driving, or a broken one,
is not an agent.

**CAR-2** `P4` A car contains at most one driver.

**CAR-8** `P4` **Where a car goes is whatever is driving it** — an errand's place (AMB-5, EVA-3, SRV-5, SRV-6)
or an order's goal (CTL-8). **A car nothing else is driving runs its own round** (`TownWorld.RunTheRound`): it
stands in its bay for a drawn while, then drives to a free bay near a place drawn within reach of it and parks
there. Nobody boards a car (PER-11), so without the round a town's cars would stand where they were put for the
rest of the run. **On a map with no bay a car can reach** there is no round to run, and every such car takes
the lane it stands on and tours the lanes with no destination at all (`TownWorld.DriveTheEmptyMap`, `LaneTour`).
Three things follow:

- **A car stood down anywhere but a bay sets off for one at once** — a leg given up in the street leaves a body
  standing in a lane, which is an obstruction and not a stand.
- **The while is drawn each time the car parks** (`SimConfig.Driving.ParkedMinS`, `…MaxS`), so a car park stood
  full before the first tick does not empty in one, and most of a town's cars are parked at any moment.
- **The place is drawn near the car** (`SimConfig.Driving.RoundReachM`): drawn anywhere in the town, every trip
  crossed it down the same few arterials and locked their junctions.

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

**CAR-47** `P3` **A driver of the town's own brakes inside what the patch has left.** In the ordinary way it asks
the brake for no more than the friction circle's remainder along the roll once the corner its wheel is asking for
has been paid for, at the share every stop is planned at (S-2): past that the brake buys a slide and costs the
corner, and a car braking in a bend runs straight on out of it. The wheel's corner and not the one the tyres are
carrying, because a sliding car carries less than it asks for. A hazard spends the whole of the tyres at once
(S-2), and a hand brakes as hard as it likes.

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
**the car is seated on it where its axle actually is** and drives on from there. A body stops with its axle
near but not exactly at the end of the line it was given, so the next line — a lane under it, or the next
piece of its manoeuvre — is taken from the metre the body projects onto. **A car is never shuffled onto a line
to make a precomputed one fit**, and it draws no shape of its own to reach one either but at a bay (CAR-15).

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

**CAR-6.2b** `P5` The centreline may be crossed into the oncoming lane **only to pass a stationary obstacle**
(CAR-46).

**CAR-6.3** `P5` Do not cross a red car light.

**CAR-6.4** `P5` Do not idle, except in a parking space, while obeying a signal, or while waiting for other
agents.

**CAR-6.5** `P5` Reverse **only in a manoeuvre at a bay**, on the piece of it shaped for reverse (GEN-4f,
GEN-4j), **or back down its own lane for the room to step out round what it means to get past** (CAR-50).
There is nowhere else in the town a car goes backwards.

**CAR-7** `P5` Yielding to another agent that blocks the path is legitimate idling and is the normal way cars
resolve conflicts.

**CAR-7a** `P5` A car must yield to any agent already inside the intersection or on a crossing it is taking.

**CAR-13** `P5` **A small share of people do not keep the courtesies**, and which ones is drawn once when the
person is made and holds for the rest of the run. It is a fact about the **person and not the car** — the
same car is driven one way by one owner and another by the next — so it changes nothing until they take a
wheel, and the share is `SimConfig.Driving`.

**CAR-13.1** `P5` **Nothing is dropped.** The one courtesy the habit could drop is **CAR-6.3**, the red, and a
red is ground a light holds on the road (TLT-1) rather than a courtesy a driver pays: the ladder answers it,
and has no rung for a habit (TER-5g). The list is closed — a driver who does not keep the rules is not
thereby exempt from them.

**CAR-13.2** `P3` **A body is never one of them.** Somebody already on the paint, a wreck, a queue, the ground
another movement has committed to and the hazard the profile brakes for all bind a reckless driver exactly
as they bind anybody else. What the habit removes is a courtesy owed to whoever has not started; what
follows from it is a matter for the geometry and the tyres, and never a licence.

**CAR-13.3** `P8` **A red crossed is counted**, whatever carried the car over the bar — ground it could no
longer stop short of when the amber ran out, or a shunt (`TownWorld.RedBarCrossings`). A car on a call is
not: the light's hold is below its rung (AMB-4.2), so it cannot be in breach of it.

## Recovery

**CAR-9** `P5` A car that is no longer driving the line it was given **stops and takes the lane it is
actually standing on** — the shortest way back onto the network, which is the walker's own answer to the
same state said of a driver (PER-25). Nothing is placed and nothing is corrected: the follower steers the
body back onto that lane's line like any other. **It looks for that lane on its own clock** (`RejoinLooksEveryS`),
since a car at rest stands on the same ground from one tick to the next.

**CAR-9a** `P5` Where no lane runs the way the body is pointing, the car covers no ground and **the leg's own
clock ends it** (CAR-15a): it is stood down where it stands, on its handbrake, which makes it no longer an
agent (CAR-1) and exempt from the stuck-agent check (VER-3). Nobody gets out, there being nobody in it.

## The standing rules

These run underneath every tick of every car, and **they are not a step of anything**: they are how a car
is driven at all, and they live in `world/town/TownWorld.Driving.cs`. Nothing selects them and nothing
may repeat them.

**S-1** `P5` Hold the driven line, measured at the rear axle, aiming a speed-scaled distance ahead and
never further than the corner being driven is wide.

**S-2** `P5` Speed is the minimum of every constraint — the gear cap, the corners, the end of the line,
**the road the car was granted**, the end of its own plan where that was held short of what it wanted
(TER-4c.1) and the place the car was sent to — every distance taken a lead ahead of where the car is, against
*usable* grip. The lead is the staleness of the driver's own decision and the travel of the pedal that answers
it, from wherever the pedal is. **The corners are read off the line and not walked**: each arc carries, from
when the line was laid, what it may be entered at and still hold every corner past it, and a car reads the arcs
its lead point has reached at their own corner and the first it has not at that entry.

**S-2a** `P3` **Take the road ahead before driving down it, and keep to what was granted.** Every tick, a
driver plans the stretch of its own line from its nose (TER-4c.1) to where it means to be able to stop, and
is granted what is left of it in front of the first body on it and short of ground another plan keeps, less
**one stand-off, whatever ended it** — or the room to step out round a body it may come to pass (CAR-46). **The
grant is read like every other stop point, a lead ahead, and whatever cut it** — a queue, a wreck, somebody on
foot, a light's hold, a movement it gives way to: a car is
driven so it can come to rest inside the section it holds, and that is **the whole of following**. Nothing
in front is followed or credited with a speed of its own; the car behind has less road to stop in and holds
the speed that road affords, and what is in front moving on is its section growing. **It is cut by the
secondary claims other plans place on its ways where theirs cross them** (TER-5c.1), so the grant means one
plan to a piece of ground across a junction and not only along a lane — a light's hold among them.

**S-3** `P5` **What is ahead is read off the reservations, once, as the grant.** There is no second reading of
what is in front — no headway to a body beside the grant, no ray and no speed of the thing ahead — because
**everything that can be on a lane has claimed it**: the traffic, anybody on foot in it, and the town's own
furniture (TER-4c). What cut the grant is kept beside it for the words and the leg's clock, and is no term of
the speed. **The margin is spent where the section is cut shorter than the car can stop in** even at what its
tyres can put down: somebody stepping out, a car pulling in.

**S-4** `P3` Take up the ground **on your own way through** the box ahead, at the places the other movements
cross it, and give back the box behind (TER-5c). Every tick, never on the clock — a red can change
under a car, and nothing here is a claim on the junction. **What another movement's ground costs you
is read on your own ways** (TER-5c.1): a car places main claims on the ways it is going to be on and
secondary claims on the ways those cross, and reads nothing but its own. **And what it costs you turns on the right of way each of you has there**
(TER-5e): ground held by a movement that gives way to yours is ground you are not cut at, and ground
held by a body past the point it could stop short is ground nobody's rank takes. A crossing already taken
is **given back** when something with the right of way over it asks for the same ground — all but what this
car can no longer stop short of, and what it is standing on.

**S-5** `P5` Hold a stop you have already made: the handbrake is pulled only at rest.

**S-7** `P7` A hand at the wheel suspends all of it — **except what the car cannot help**: it holds the road
it can no longer stop short of, straight ahead of it on whatever ways that crosses, as any moving body does
(TER-5e), so the traffic gives way to it and nothing is said about where the hand means to go.

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
holds: the pedal, the gear, the line in front of it, the pass it has decided on and the priority it
carries. **No lamp is state of its own** — there is nothing to set, nothing to clear and nothing that can
disagree with the car it is bolted to.

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
and nothing else but a pass (CAR-14.7). It is shown only while a junction is **within reach** of the car
and only where the movement its own line takes through that junction is a **turn rather than straight
on** — the road's own classification of the pair of lanes the line joins (`TownWorld.ReadTheBoxAhead`,
`CarFleet.TurningAtTheBox`), so what a car announces and what it gives way to are one answer about one
movement.

**A bend is not a turn.** A road of constant radius bends past any threshold for ever, and a car announcing
that all the way round a circuit is announcing something nobody can act on: there is nowhere else for it to
go. The same is true of a car going straight on through a crossroads, and of a car manoeuvring at a bay
with no junction ahead of it at all.

**Which side is read off the geometry**: the side the line bends to over the stretch of it a driver would
be announcing. A line driven the way the rear axle travels rather than the way the car points — a piece of a
manoeuvre in reverse — bends the body the other way round, and is read in the body's frame. It is the **front
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

**CAR-14.7** `P6` A car **getting past something says so with its indicator from the moment it decides to**
(CAR-46) — from where it would begin slowing for what it passes, whether or not the lane beside is free
yet — and not from when the pass is its own. A car waiting behind a wreck for the lane beside is
indicating towards it. **Once it is on the pass**, it indicates the side it steps out to until that step is
done, **nothing alongside what it passes**, and the side it steps back to from a lead short of the step
back — never before the step out is done, so a step out running straight into the step back hands one side
straight to the other. **The pass outranks the junction**: from the decision until the car is back in its
own lane, the turn at a junction beyond it is not announced. A pass the road itself refuses — a line that
ends before there is room to come back onto it, a zebra off a call, a bend, a place the car was sent to — is no
decision and says nothing; room held by something that will move on is waited for, and is.

Where the numbers are: `SimConfig.Lamps` — how much of the line is read for a turn and how far that
stretch must bend, how long before the step back a pass says it, the rates the flashing ones flash at, and
how far the light around a lit one spills.
**Neither where a lamp is nor what it looks like is a number here**: where is the variant's own file,
beside the picture it was measured off, and what is the town's one lamp sheet, cut from those same
pictures (CAR-14a).

## Marks

A wheel leaves a mark when it is worked past the surface's own threshold. Slide is tracked **per axis**
and spin **per wheel**, so a locked rear axle and a spinning front pair draw different marks. A parked car
with its handbrake on does not scrub the road. Surfaces that **plough** carry a floor under the mark
instead: ploughing is displacement rather than friction, and priced as power it would die with speed, so
a car creeping onto a lawn would leave it pristine.
