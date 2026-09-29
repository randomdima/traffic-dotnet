# The service vehicles — requirements

The cars a town stands on purpose rather than for somebody to drive to work in: the **police car** at a
police station, the **ambulance** at a hospital and the **evacuator** at a depot. The ambulance and the
evacuator each have a slice of their own, because each one's errand is a whole machine
([agents/ambulance](../../ambulance/docs/requirements.md),
[agents/evacuator](../../evacuator/docs/requirements.md) — `EVA-1` to `EVA-8` and `SimConfig.Evacuator`);
what is here is what all three are made of, where they stand, and the two errands a police car runs.

**A service vehicle is a car and drives what every car drives**
([agents/car](../../car/docs/requirements.md) `CAR-15`). Nothing here is a second driver.

## The places

**SRV-1** `P4` Some of a town's buildings are **police stations** and some are **depots**, declared by the
map on the terms a hospital is (AMB-1). **A building serves one use at most**, which one field settles
rather than the order anything is read in. A town with a building on it has one of each.

## The vehicles

**SRV-1a** `P6` A police station **wears the police station's own roof** with its door to the pavement, and a
depot wears the **repair shop's**; no other building may wear either — the whole of AMB-1a said of a
station and of a depot. A depot's roof is the one that names its yard: the wrecks standing in it are cars
waiting on the workshop behind that shutter (EVA-7).

**SRV-2** `P5` Each police station stands an **apron** of police cars (GEN-4k), one in each bay, and each
depot **one evacuator** in a bay held for it, from before the first tick and on the terms AMB-2 stands a
hospital's ambulances on: a building with fewer free bays near it stands fewer, and one with none stands
none. **A depot's apron is its evacuator's bay and its yard's slots besides**, and what a yard is for is
`EVA-2`. How many of each a map has room for is the roster line of `--bench census`.

**SRV-7** `P1` **A police car starts on its police station's parking**: stood in a bay of the station's own
yard (GEN-55) before the first tick, and home to it when its errands are done.

**SRV-3** `P5` A service vehicle is an ordinary car with one fact about it: it wears a variant from the
**service list** rather than one of the fleet's. Three things follow.

- **What makes it a car that acts is the errand and not a seat** (CAR-1). What drives a service vehicle is its
  errand, so nothing about its own acting is decided by who is inside it, and its light stays on regardless
  (AMB-4b). **A police car carries its officer** (SRV-11) **in a crew seat and never at the wheel**; an
  ambulance and an evacuator carry nobody.
- **And what keeps it out of anybody else's hands is the building it stands on the strength of**, and never
  who is sitting in it. A vehicle struck off its building (`EVA-7`) is an ordinary car in service paint.
- **One errand is worked on foot, and it is the closure** (SRV-11). The casualty is got aboard at the standoff
  (AMB-10) and the arm is worked from the truck (`EVA-5`); what that costs is recorded in the known gaps
  ([docs/index.md](../../../../docs/index.md#known-gaps)) rather than written here as a decision.

**SRV-3a** `P6` **A police officer wears the police uniform, and nobody else in the town wears a service
uniform.** The uniforms are a second list in the person catalogue on the terms SRV-3's service list is the
fleet's: a walker's look is drawn by wrapping the ordinary list, and that wrap cannot reach past it, so a
uniform is worn only by somebody named to wear one — an officer stood with their car (SRV-11).

**SRV-4** `P5` **A service vehicle breaks like every other car** (PHY-3), the evacuator included. Four
things follow from what a broken one can be in the middle of.

- **A wrecked evacuator drops what it was pulling where it stands.** The car on the arm is a call again
  from that moment, no worse off than where it fell — EVA-8's own argument about a haul that will not get
  through, said of a crash instead of a clock — and the errand it was on is given up.
- **Its depot has no evacuator until somebody else clears it.** The truck is a call like any other wreck,
  and the bay held for it is held for nobody from then on (`ParkingRegistry.Claimed`) — it does not go
  back to the town; the yard's slots stay held for the wrecks standing in them. A depot whose evacuator broke and whose town has no other one is a town that has stopped
  collecting, which is `EVA-2`'s own state and is counted rather than hidden.
- **And a mended one comes back as an ordinary car**, on `EVA-7`'s terms: nothing hands a depot its truck
  back.
- **A wrecked police car gives its closure up**, and an officer standing out on the road is an ordinary walker
  from then, still in uniform; one aboard is thrown clear like anybody in a wreck (PHY-6).

## The beat

**SRV-5** `P5` A police car **patrols**: it stands on its station's apron for a drawn interval, then drives to
a drawn place in the town, then to another, for a drawn number of places, and then home to its own bay to
stand again. Five things follow, and the third is the point of the rule:

- **A beat is drawn and never searched for.** Nothing in the town asks for a police car on its beat, so a beat
  is aimed at nothing: it is a place along one of the town's lanes, taken from the car's own stream (AGT-6),
  and the driving to it is an ordinary leg (CAR-15). **A lane and not a junction**, because a leg ends by
  the car standing where it got to and a junction's middle is the one place standing still is being driven
  into — **and a street and not a car park's arm**, which is a bay (GEN-4h).
- **Every leg is bounded**, on AMB-9's argument said of a patrol: a place the traffic will not let a
  police car reach costs it the next street and nothing more, because a patrol has nowhere it must be.
- **A patrol carries no priority.** None of AMB-4 applies to a beat: no rung above other movements, no
  exemption from a red or a bar, no pace of its own. A police car crossing this town is ordinary traffic
  that happens to be going somewhere nobody lives. **A call is the other errand** (SRV-6), and the leg out
  to a scene does carry it.
- **The interval before a beat is drawn per car and not per station**, so an apron of four cars stood in
  the same instant does not leave in it.
- **A beat gives way to a call** (SRV-6, SRV-8) — standing, patrolling or on the way home. A place drawn out of
  a hat is never worth more than a road that has to be shut, and the beat is picked up again from wherever the
  scene left the car.

## The closure

**SRV-8** `P1` **A police car's goal is the scene of an incident**: it drives there and closes the road round
it (SRV-9).

**SRV-9** `P1` **A closure marks a lane, or the whole road, inaccessible by standing at the entrances of the
lanes it closes**, blocking the way into them — **and an officer standing at an entrance is what blocks it**.
A scene lying across both lanes of its road closes the whole road, **with two police cars, one at each end**.

**SRV-10** `P1` **A closed lane is out of path finding**: no route is planned into one.

**SRV-11** `P1` **The officer gets out of the car and stands on the road.**

**SRV-6** `P5` **A scene is a call, and each lane it lies across is closed by a police car of its own.** A
casualty lying in the street (`AMB-5`) and a wreck standing in one (`EVA-1`) each raise one, taken on the terms
a rescue and a recovery take theirs: the nearest free patrol, **nearest measured against every other free
patrol and not against every other scene**, one lane of a scene to a patrol. Eight things follow, and the fourth
is the point of the rule.

- **The lanes a scene lies across are read off the ground its body was laid on** (TER-4c.2): the lane nearest
  it, and the other lane of its street where the body stands on that one too — which is what makes it one
  police car or two (SRV-9).
- **A closure holds the scene's lane, every lane upstream that leads nowhere else, and every lane that leads
  only into those** (`RoadClosure.Stretch`), walked back to a junction a driver chooses at and bounded by the
  room it is laid into (`SimConfig.Service.ClosureMostLanes`). **Its entrance is never a bend nor a car park's
  cut**: a car turned back there would be left a lane whose one way on is closed, and no junction turns a car
  round (TER-5f).
- **The leg out carries the priority and nothing else does** — the whole of `AMB-4` for that one leg, on
  `EVA-4`'s terms, aimed at the entrance lane itself and never the other side of its street. What is urgent
  about a closure is getting the road shut before somebody else drives into the scene; the drive home
  afterwards is a police car going back to work.
- **The car stands in the entrance and its officer at the mouth, between it and the traffic**
  (`OfficerIntoTheLaneM`, `PoliceCarPastTheOfficerM`). What refuses a car the lane is the officer's body, which
  every car stops short of like any other body (TER-4c.1); what keeps cars from planning to go there is the
  ban (SRV-10) — a route never enters a closed lane's run (`RoutePlanner`) and a tour never draws one
  (`LaneTour`). **No claim of the closure's own is laid** (SIM-7): a body is already the thing the road refuses
  a car, and a ban is already the thing a plan cannot cross.
- **A route already under way through a closed lane is dropped when the closure begins**, so a car on its way is
  sent round rather than into the officer — every car but one that can no longer stop short of the box.
- **A call goes through** (AMB-4, EVA-4): a car carrying one is routed as if nothing were closed, the scene it is
  aimed at lying inside the closure (SIM-6's ban lifted for that agent on that plan), and **the officer steps to
  the kerb while one is coming down the lane short of them**.
- **A closure ends when its scene does, and is bounded besides.** The casualty collected and the wreck on the
  bar are both the scene over; the bound is what stops a scene nothing ever clears holding a street out of the
  town for the rest of the run. **The lanes stay closed until the officer is back aboard**, walked to the door or
  put in their seat once the recall runs out (`ServiceRecallS`).
- **A car with no officer aboard takes no call** — nobody to put at the mouth — and an officer knocked down or
  taken by a hand is not waited for.

## Where the numbers are

On `SimConfig.Service` ([core](../../../core/docs/requirements.md#where-a-figure-lives)): how many of a
town's buildings are police stations and how many are depots, how near its own building one may stand, how
many bays an apron holds — a hospital's as well as a station's — the three the beat is drawn from (the
places on one, the interval between two, and the bound on a leg), and **the four a closure is**: how many lanes
back it may reach, how far into the entrance its officer stands and the car past them, and how long one may
stand — with **the two its officer walks by**, how near a place counts as there and how long they have to get
back to their seat.
