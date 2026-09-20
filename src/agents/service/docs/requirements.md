# The service vehicles — requirements

The cars a town stands on purpose rather than for somebody to drive to work in: the **police car** at a
police station and the **evacuator** at a depot. The **ambulance** and the **evacuator** each have a slice
of their own, because each one's errand is a whole machine
([agents/ambulance](../../ambulance/docs/requirements.md),
[agents/evacuator](../../evacuator/docs/requirements.md)); what is here is what all three are made of, how
all three do their work in human form, where they stand, and the two errands a police car runs.

**A service vehicle is a car and drives what every car drives**
([agents/car](../../car/docs/requirements.md) `CAR-15`). Nothing here is a second driver. Why it reads this
way is [decision-log.md](decision-log.md).

## The places

**SRV-1** `P4` Some of a town's buildings are **police stations** and some are **depots**. Which ones is a
property of the map — **declared in the file** (GEN-9), never by behaviour and never by a run — so a
map's are the same every time it is opened, exactly as a hospital's are (AMB-1). **A building serves one
use at most**, which one field settles rather than the order anything is read in. A town with a building
on it has one of each.

## The vehicles

**SRV-1a** `P6` A police station **wears the police station's own roof** with its door to the pavement, and a
depot wears the **repair shop's**; no other building may wear either — the whole of AMB-1a said of a
station and of a depot. A depot's roof is the one that says what its yard is for: the wrecks standing in
it are cars waiting on the workshop behind that shutter (EVA-7), and a yard of broken cars behind an
ordinary front door is the town showing the errand without naming it.

**SRV-2** `P5` Each police station stands an **apron** of police cars — the bays nearest it along its own kerb,
held for them for the whole run (GEN-4k), with one car and its crew standing in each — and each depot
**one evacuator**, in a bay held for it on the same terms, from before the first tick. A building with
fewer free bays near it than the apron asks for stands fewer, and one with none stands none: the terms
AMB-2 stands a hospital's ambulances on. **A depot's apron is its evacuator's bay and its yard's slots
besides**, and what a yard is for is [agents/evacuator](../../evacuator/docs/requirements.md) (EVA-2).

**SRV-3** `P5` A service vehicle is an ordinary car with one fact about it: it wears a variant from the
**service list** rather than one of the fleet's. Three things follow.

- **What makes it a car that acts is the errand and not a seat.** CAR-1 asks for a driver and a service
  vehicle has none, so nothing about its own acting is decided by who is inside it, and its light stays on
  regardless (AMB-4b).
- **And what keeps it out of anybody else's hands is the building it stands on the strength of**, and never
  who is sitting in it. A vehicle struck off its building (`EVA-7`) is an ordinary car in service paint.
- **No errand is worked on foot**, and that is where this slice stands rather than what it wants: the
  casualty is got aboard at the standoff (AMB-10), the arm is worked from the truck (`EVA-5`) and the
  closure is the police car's own claim (SRV-6). What it costs is recorded in the known gaps
  ([docs/index.md](../../../../docs/index.md#known-gaps)) rather than written here as a decision.

**SRV-3a** `P6` **Nobody in the town wears a service uniform.** The uniforms are a second list in the
person catalogue on the terms SRV-3's service list is the fleet's: a walker's look is drawn by wrapping
the ordinary list, and that wrap cannot reach past it, so a uniform is worn only by somebody named to wear
one — and while no vehicle carries a crew, nobody is. It is what a service vehicle's paint is, said of the
body rather than the car, and it is what will dress a crew the day one goes back out.

**SRV-4** `P5` **A service vehicle breaks like every other car** (PHY-3), the evacuator included. Three
things follow from the one that can be towing something when it happens.

- **A wrecked evacuator drops what it was pulling where it stands.** The car on the arm is a call again
  from that moment, no worse off than where it fell — EVA-8's own argument about a haul that will not get
  through, said of a crash instead of a clock — and the errand it was on is given up.
- **Its depot has no evacuator until somebody else clears it.** The truck is a call like any other wreck,
  and the bay held for it goes back to the town; the yard's slots stay held for the wrecks standing in
  them. A depot whose evacuator broke and whose town has no other one is a town that has stopped
  collecting, which is `EVA-2`'s own state and is counted rather than hidden.
- **And a mended one comes back as an ordinary car**, on `EVA-7`'s terms: nothing hands a depot its truck
  back.

## The beat

**SRV-5** `P5` A police car **patrols**: it stands on its station's apron for a drawn interval, then drives to
a drawn place in the town, then to another, for a drawn number of places, and then home to its own bay to
stand again. Five things follow, and the third is the point of the rule:

- **A beat is drawn and never searched for.** Nothing in the town asks for a police car, so a beat is
  aimed at nothing: it is a place along one of the town's lanes, taken from the car's own stream (AGT-6),
  and the driving to it is the car's catalogue like every other leg. **A lane and not a junction**,
  because a leg ends by the car standing where it got to and a junction's middle is the one place standing
  still is being driven into.
- **Every leg is bounded**, on AMB-9's argument said of a patrol: a place the traffic will not let a
  police car reach costs it the next street and nothing more, because a patrol has nowhere it must be.
- **A patrol carries no priority.** None of AMB-4 applies to a beat: no rank above other movements, no
  exemption from a red or a bar, no pace of its own. A police car crossing this town is ordinary traffic
  that happens to be going somewhere nobody lives. **A call is the other errand** (SRV-6), and the leg out
  to a scene does carry it.
- **The interval before a beat is drawn per car and not per station**, so an apron of four cars stood in
  the same instant does not leave in it.
- **A beat gives way to a call** (SRV-6). A place drawn out of a hat is never worth more than a road that
  has to be shut, and the beat is picked up again from wherever the scene left the car.

## The closure

**SRV-6** `P5` **A scene is a call, and the police car sent to it closes the road round it.** A casualty lying in the street
(`AMB-5`) and a wreck standing in one (`EVA-1`) each raise one, taken on the terms a rescue and a recovery
take theirs: the nearest free patrol, **nearest measured against every other free patrol and not against
every other scene**, one call to a scene and one scene to a call. Six things follow, and the third is the
point of the rule.

- **The leg out carries the priority and nothing else does** — the whole of `AMB-4` for that one leg, on
  `EVA-4`'s terms. What is urgent about a closure is getting the road shut before somebody else drives into
  the scene; the drive home afterwards is a police car going back to work.
- **What is laid is a claim and never a body** (SRV-3). What a closure is, is ground spoken for, and
  nothing has to be standing in the lane to say so — one that did would be a thing the rescue itself has to
  be held off (`AMB-4a`), which is the closure working backwards. The car stands at its own standoff and
  the claim does the refusing.
- **A closed road is a claim at a rank of its own** (`TER-5e`), above every ordinary movement and below a
  call. That one placing is the whole mechanism: ordinary traffic is refused it and stops short, and an
  ambulance or an evacuator answering a call is not refused it and drives through. **Nothing reading the
  road learns a new word**, and neither service is ever told that a policeman exists.
- **It is a claim and takes only what a claim may take.** A body, and the road a body is committed to being
  able to stop in, are no more an officer's than anybody's — a closure orders who waits and is never a way
  to stop somebody who cannot stop.
- **A closure is one stretch of one way and is held once** (`TER-5c.2`), laid round the scene rather than
  round the car — what is being kept clear is the ground somebody is working on and not the ground the
  patrol happened to stop on.
- **A closure ends when its scene does, and is bounded besides.** The casualty collected and the wreck on
  the bar are both the scene over; the bound is what stops a scene nothing ever clears holding a street out
  of the town for the rest of the run.

## Where the numbers are

On `SimConfig.Service` ([core](../../../core/docs/requirements.md#where-a-figure-lives)): how many of a
town's buildings are police stations and how many are depots, how near its own building one may stand, how
many bays an apron holds — a hospital's as well as a station's — the three the beat is drawn from (the
places on one, the interval between two, and the bound on a leg), **the two a crew is held to** (how near a
thing somebody on foot has to be to take hold of it, and how long they have to get back to their seat before
they are put in it), and **the three a closure is** (how far short of a scene the car stands, how much road
is held either side of it, and how long one may stand).

**A crew's reach is metres and not car lengths**, unlike every reach a vehicle is held to: what it measures
is a person reaching, and a person does not get longer because the town's nominal car does.

## What is not here

**What an evacuator does** — the wreck it is called to, the yard it takes one to and the bar it drags one
on — is `EVA-1` to `EVA-8` and `SimConfig.Evacuator`
([agents/evacuator](../../evacuator/docs/requirements.md)). The instrument that says how many of each a map
has room for is the roster line of `--bench census`.
