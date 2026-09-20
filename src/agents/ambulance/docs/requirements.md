# The ambulance — requirements

The rescue: the hospitals a town has, the ambulances standing at them, and what happens between somebody
being knocked down and being put back on the pavement healed.

**An ambulance is a car and drives the car's catalogue** ([agents/car](../../car/docs/requirements.md),
[the manoeuvres](../../car/maneuvers/docs/index.md)). Nothing here is a second driver: what is below is
the *errand* those manoeuvres are run for, and the one thing that errand changes about the road. Why it
reads this way is [decision-log.md](decision-log.md).

## The places and the vehicles

**AMB-1** `P4` Some of a town's buildings are **hospitals**. Which ones is a property of the map — **declared
in the file** (GEN-9), never by behaviour and never by a run — so a map's hospitals are the same every
time it is opened. A town with a building on it has at least one.

**AMB-1a** `P6` A hospital **wears the hospital's own roof**, and no other building may. Which building is one
is the map's answer and not a search over sizes, so the picture is fitted inside whatever plot it landed on
rather than drawn at the size it was painted; and it is kept out of the catalogue an ordinary roof is
matched from, because a building lettered HOSPITAL that a casualty cannot be delivered to is the town
telling the person watching something untrue.

**Its front door faces the pavement.** An ordinary roof is turned so that a wide picture lands on a wide
building and the door then picks between the two walls that leaves; a civic roof is fitted rather than
matched, so nothing about its size settles which pair of walls it is laid across, and the pavement settles
it instead — of the four ways round the art could be laid, the one whose door points most nearly at the
plan's own ways in (OBJ-4). A sign that reads down a side street is a building nobody can find the
entrance of.

**AMB-2** `P5` Each hospital stands an **apron** of ambulances — the bays nearest it along its own kerb, held
for them for the whole run (GEN-4k), one ambulance standing in each — from before the
first tick. A
hospital with fewer free bays near it than the apron asks for stands fewer, and one with none stands none:
both are real states and are reported rather than hidden.

**AMB-3** `P5` An ambulance is an ordinary car with one fact about it: it wears the service variant rather
than one of the fleet's. What a service vehicle is made of, and what keeps one out of anybody else's hands
now that it stands empty, is `SRV-3`.

## The priority

**AMB-4** `P5` An ambulance **answering a call** carries a right of way above every other movement
(TER-5e). While it does:

- **AMB-4.1** `P5` Every stretch of road it asks for is held at that rank, so ground another movement has
  merely *claimed* is not ground it is refused by.
- **AMB-4.2** `P5` A red light and a painted bar do not apply to it, and a red it crosses is not a violation.
- **AMB-4.3** `P5` A body on the paint refuses it exactly as any other body does — which is the ladder's own
  answer (TER-5g) and not a courtesy this rule grants. **There is nothing else at a crossing for a blue
  light to outrank**, a walker carrying no rank of its own.
- **AMB-4.4** `P5` It crosses the centreline to get past what is in front of it without first spending the
  patience every other driver spends, and a queue counts as something to get past.
**AMB-4a** `P3` **The blue light buys the road and never the tyres.** A rescue keeps every constraint the
speed profile already takes — the corners, the grip, the body in front, the hazard — and is held to a
pace of its own above them. What a priority orders is who waits; it is never a licence to drive into
somebody, and what it takes is only ground its holder has not reached and can give back.

**AMB-4b** `P5` The priority is the **errand** and not the vehicle, **and not who is sitting in it**. An
ambulance standing at its station, handing over or driving home is ordinary traffic and holds its road like
anybody else; one standing at a scene with nobody in it is still answering a call, and the light stays on.
What that costs is the ground round the scene held at the emergency rank for as long as the scene lasts,
which is seconds and is bounded by `AMB-9` above that.

## The call

**AMB-5** `P5` A person knocked down and left alive (PER-18) is a **casualty**, and a casualty is a call. The
nearest ambulance with nothing else to do takes it, and **nearest is measured against every other free
ambulance and not against every other casualty**: a crew that is not the nearest to the body it would have
gone to takes nothing and asks again. **One casualty to a call and one call to a casualty**: two
ambulances sent to one body is one of them crossing the town to find the place already attended.

**AMB-6** `P5` An ambulance carries at most one casualty, on a seat that is neither the wheel nor a crew seat.
Getting them aboard takes the crew a bounded interval, spent **at the vehicle** with the body already
brought to it (AMB-10).

**AMB-10** `P5` **An ambulance is stopped at a standoff short of the casualty rather than beside them**,
and the last of the distance is a placement. Three things follow, and the first is the point of the rule.

- **The vehicle stands clear of the accident.** The standoff is measured back along the lane the body is
  lying beside, because a vehicle can only arrive along the road — and it is where `P-18` stops the car, on
  the terms every other place the catalogue is stopped at is asked on. An ambulance parked on the casualty
  is an ambulance in the lane it needs kept clear for itself, and one nobody can work round.
- **The last of the distance is the winch said of a person** (`EVA-5`): the body is set down at the
  vehicle's own door and got aboard over a bounded interval, which is a placement (`PHY-7a`) and not a
  coupling. **It is where this slice stands and not what it wants** — what should cover those metres is
  somebody walking them, and the absence is named in the known gaps
  ([docs/index.md](../../../../docs/index.md#known-gaps)).
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

On `SimConfig.Ambulance` ([core](../../../core/docs/requirements.md#where-a-figure-lives)): how many of a
town's buildings are hospitals, the pace a call is driven at, how long the crew and the treatment take, how
far short of the casualty the vehicle is stopped and how near that mark it has to have got, and the bound
on a leg. **A crew's own reach, and the bound on getting one back aboard, are `SimConfig.Service`** —
they belong to a crew and not to a rescue — and so is how many bays an apron holds, because it is the same
figure a police station's is ([agents/service](../../service/docs/requirements.md)).

## What is not built

**Nobody pulls over.** AMB-4 is priority over *ground* — an ambulance is never refused a claim, a red or
a kerb, and it overtakes without waiting — but no car steers aside to let one past. Yielding here is a
car being stopped short of ground the rescue has taken, which is what the road already does; a manoeuvre
that moved a body out of a lane on somebody else's behalf would be a new entry in the catalogue and is
not one. The instrument that says what this costs is `--bench rescue`.

**A crew on foot goes round nothing.** A walker follows the line the pavement's own network laid for it,
and the last hop of a crew's walk is off that network by however far the body it is fetching lies from it
(AMB-10) — walked straight, because a casualty in the middle of a carriageway is a place the pavement graph
has never heard of. What that costs is a paramedic who can be pinned against whatever happens to stand
between them and the body; what stops it costing the town an ambulance is the recall (`SRV-3`), and what
would fix it is a route the walking side does not draw.
