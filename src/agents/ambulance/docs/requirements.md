# The ambulance — requirements

The rescue: the hospitals a town has, the ambulances standing at them, and what happens between somebody
being knocked down and being put back on the pavement healed.

**An ambulance is a service vehicle** ([agents/service](../../service/docs/requirements.md) `SRV-3`) **and
drives what every car drives** ([agents/car](../../car/docs/requirements.md) `CAR-15`). What is here is the
*errand* those legs are run for, and the one thing that errand changes about the road.

## The places and the vehicles

**AMB-1** `P4` Some of a town's buildings are **hospitals**. Which ones is **declared by the map** (GEN-9),
never by behaviour and never by a run, so a map's hospitals are the same every time it is opened. Every
district of a town with a building on it has one (GEN-56).

**AMB-1a** `P6` A hospital **wears the hospital's own roof**, and no other building may. The roof is fitted
inside whatever plot the map chose rather than matched by size, and it is kept out of the catalogue an
ordinary roof is matched from, because a building lettered HOSPITAL that a casualty cannot be delivered to
is the town telling the person watching something untrue. **Its front door faces the pavement**: of the
four ways round the art could be laid, the one whose door points most nearly at the plan's own ways in
(OBJ-4), because a sign that reads down a side street is a building nobody can find the entrance of.

**AMB-2** `P5` Each hospital stands an **apron** of ambulances (GEN-4k), one in each bay, from before the
first tick, with their crews aboard (SRV-3), half of them to patrol its district (SRV-5). A hospital with fewer
free bays near it than the apron asks for stands fewer, and one with none stands none: both are real states and
are reported rather than hidden.

**AMB-3** `P5` An ambulance is an ordinary car with one fact about it: it wears the ambulance's variant from
the service list. The rest of what it is made of is `SRV-3`.

## The priority

**AMB-4** `P5` An ambulance **answering a call** carries a right of way above every other movement
(TER-5e). While it does:

- **AMB-4.1** `P5` Every stretch of road it asks for is held at the call's own rung, so ground another
  movement has merely *claimed* is not ground it is refused by.
- **AMB-4.2** `P5` **A light's hold is below its rung** (TLT-1, TER-5g), so a red holds it nowhere, and a red it
  crosses is not a violation.
- **AMB-4.3** `P5` A body on the paint refuses it exactly as any other body does — which is the ladder's own
  answer (TER-5g) and not a courtesy this rule grants. **There is nothing else at a crossing for a blue
  light to outrank**, a walker being granted nothing of its own.
- **AMB-4.4** `P5` It crosses the centreline to get past what stands in front of it as any car does
  (CAR-46), which spends no patience first — **and its pass is asked at its rung** (TER-4c.6), through a box
  as along a street. A queue at rest making its own movement is something it gets past, since what the queue
  waits for is ground the call takes, **and so is traffic going slower than the call means to** — by more
  than a pedal hunting about the call's own pace (`Ambulance.SlowerToPassMps`) — which is held short of where
  the pass steps back in; ground another movement only plans is no refusal, the lane beside
  included, and only what a holder can no longer stop short of is; and the paint of a zebra is claimed whole,
  whoever is already on it waited for short of the paint and nobody else let onto it.
- **AMB-4.5** `P5` **It plans further than anybody else**: the road it means to use, and how far a plan may reach
  (TER-4c.1), are a stated multiple of any other driver's, so what it will cross gives way to it before it is
  there. What it can no longer stop short of is a fact about its speed, and is not stretched.

**AMB-4a** `P3` **The blue light buys the road and never the tyres.** A rescue keeps every constraint the
speed profile already takes — the corners, the grip, the body in front, the hazard — and is held to a
pace of its own above them. What a priority orders is who waits; it is never a licence to drive into
somebody, and what it takes is only ground its holder has not reached and can give back.

**AMB-4b** `P5` The priority is the **errand** and not the vehicle, **and not who is sitting in it**. An
ambulance standing at its station, on its beat, handing over or driving home is ordinary traffic and holds its
road like anybody else; one standing at a scene with nobody in it is still answering a call, and the light stays on.
What that costs is the ground round the scene held at the call's own rung for as long as the scene lasts,
which is seconds and is bounded by `AMB-9` above that.

**Nobody pulls over.** AMB-4 is priority over *ground*, and no car steers aside to let an ambulance past:
yielding here is a car stopped short of ground the rescue has taken, which is what the road already does.
The instrument that says what this costs is `--bench rescue`.

## The call

**AMB-5** `P5` A person knocked down and left alive (PER-18) is a **casualty**, and a casualty is a call. The
nearest ambulance with nothing else to do takes it — standing on its apron or out on its beat (SRV-5), and
whichever district either is in — and **nearest is measured against every other free
ambulance and not against every other casualty**: an ambulance that is not the nearest to the body it would
have gone to takes nothing and asks again. **One casualty to a call and one call to a casualty**: two
ambulances sent to one body is one of them crossing the town to find the place already attended.

**AMB-6** `P5` An ambulance carries at most one casualty, on a seat that is neither the wheel nor a crew seat.
Getting them aboard takes a bounded interval, spent **standing at the standoff** (AMB-10).

**AMB-10** `P5` **An ambulance is stopped at a standoff short of the casualty rather than beside them**,
and the last of the distance is a placement. Three things follow, and the first is the point of the rule.

- **The vehicle stands clear of the accident.** The standoff is measured back along the lane the body is
  lying beside, because a vehicle can only arrive along the road, and the car is held there as any car is
  held at the place it was sent to (`DrivingHold.Place`). An ambulance parked on the casualty is an
  ambulance in the lane it needs kept clear for itself, and one nobody can work round.
- **The last of the distance is the winch said of a person** (`EVA-5`): once the interval is spent the body
  is taken aboard from where it lies, which is a placement (`PHY-7a`) and not a coupling. **It is where this
  slice stands and not what it wants** — what should cover those metres is somebody walking them, and the
  absence is named in the known gaps ([docs/index.md](../../../../docs/index.md#known-gaps)).
- **The loading is bounded and so is the call over it** (`AMB-9`), so a casualty nothing can get aboard
  does not hold an ambulance out of service for the rest of the run.

**AMB-7** `P5` A casualty inside an ambulance that is **wrecked** is put back in the road as a casualty, so
that another call can reach them.

**AMB-8** `P5` A casualty is delivered through the hospital's own door, on the terms every door is asked on
(OBJ-5) — refused while the building is full, which is a wait and not a failure. Delivered, they are
**healed**, dwell inside for the treatment interval like anybody else who walked in, and are then put
back out on the pavement free to draw a trip of their own.

**AMB-9** `P5` **Every leg of a call is bounded.** A body the traffic never lets an ambulance reach is given
up on and the ambulance goes home, so one unreachable casualty cannot hold a station out of service for
the rest of the run; a delivery that runs out of clock is drawn again from where the car has got to,
because the casualty is aboard and there is no better answer than trying again.

## Where the numbers are

On `SimConfig.Ambulance` ([core](../../../core/docs/requirements.md#where-a-figure-lives)): how far from a
hospital its ambulances may stand, the pace a call is driven at and how much further it plans,
how long loading and treatment take, how far short of the casualty the vehicle is stopped and how near that
mark it has to have got, and the bound on a leg. How many bays an apron holds, who rides in an ambulance and
how many patrol are `SimConfig.Service`'s, being the same figures a police station's are.
