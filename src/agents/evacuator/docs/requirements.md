# The evacuator — requirements

The recovery: the depots a town has, the evacuators standing at each, and what happens between a car being
wrecked in the street and being a car again.

**An evacuator is a service vehicle and drives what every car drives**
([agents/service](../../service/docs/requirements.md), [agents/car](../../car/docs/requirements.md)
`CAR-15`). What it is made of, where it stands and what happens when one is wrecked are `SRV-1` to
`SRV-4`; what is here is the *errand* those legs are run for, and the one thing in this project that
couples two bodies together.

## The call and the place

**EVA-1** `P5` A car in its terminal state (`PHY-3`) is a **wreck**, and a wreck is a call. It is raised where
the car breaks, so nothing searches the fleet for one, and it stays one until an evacuator has it on the
bar. A wreck standing in a yard slot is not a call, and neither is one already somebody's.

**EVA-2** `P5` Each depot keeps a **yard**: its evacuators' own bays, and a run of **slots** beside them held
for wrecks and for nobody else (`GEN-4k`) — one yard however many trucks bring wrecks to it. Three things
follow.

- **A yard slot is a hold that names no vehicle.** An apron bay is held for the one car that stands in it
  for the whole run; a slot is held for whichever wreck was fetched last, and stands empty most of the time
  on purpose.
- **A depot takes the bays the map has**, on `SRV-2`'s terms, so a yard may be smaller than the figure or
  none; `--bench recovery` reports it.
- **A full yard is a depot that has stopped collecting**, and that is a wait rather than a failure — the
  same state a full hospital's door puts an ambulance in (`OBJ-5`).

## The errand

**EVA-3** `P5` The nearest evacuator with nothing else to do — standing on its apron or out on its beat
(SRV-5), and whichever district either is in — takes the nearest wreck nobody is on their way to,
and **nearest is measured against every other free evacuator and not against every other wreck**: a truck
that is not the nearest to the wreck it would have gone to takes nothing and asks again. **One wreck to a
recovery and one recovery to a wreck.**

**Somewhere to put it is part of taking the call.** An evacuator whose yard has no free slot takes nothing,
because one that set off anyway would arrive with a wreck on the bar and nowhere to set it down, and would
then stand at its own yard holding it for the rest of the run. **A slot another truck of the same yard is
already bringing a wreck to is not free**: the yard is the depot's, and two trucks counting its last slot
would bring back two wrecks.

**EVA-4** `P5` An evacuator **on its way to a wreck** carries the whole of an ambulance's priority (`AMB-4`)
and a pace of its own. Two limits, and the second is the point of the rule.

- **The blue light buys the road and never the tyres**, exactly as for a rescue (`AMB-4a`).
- **It is the outbound leg and nothing else.** An evacuator standing at the scene, **hauling**, unhitching
  or driving home is ordinary traffic and holds its road like everybody else. What is urgent about a
  recovery is getting to the wreck; what is left afterwards is a slow vehicle with a load on the back, and
  a load on the back is the last thing that should be hurried through a town.

**EVA-5** `P3` A wreck is **towed and never carried**. It stays a body in the world the whole way (`PHY-5`),
and the tow is five things and no more:

- **One action, worked from the truck.** The arm is **worked** — swung out onto whatever is
  within its reach behind the truck, or back in when there is nothing there — and **working it is the whole
  of a recovery vehicle's action** (`CTL-7`). One call does it, and the errand and a hand on the keys reach
  for the same one, so the arm an evacuator works is exactly the arm a player works. **Nobody gets out to it**
  (`SRV-3`): what covers the last few metres is the winch below, and the same is true of setting a wreck
  down in a yard slot (EVA-6). **What it catches, it catches by either end**, and a wreck
  is not special: anything with a body may go on the bar, and what is on the bar takes no decisions until it
  is let off. **Its wheels are straightened as it goes on**, because the pair left on the ground may be its
  steered one and a car dragged on a wheel wound over is being scrubbed sideways down the road.
- **An arm.** It is **hinged on the evacuator's deck and clamped to the car it has lifted** — swinging
  freely at one end and not at all at the other — and it takes hold of that car **a fixed distance inside
  the end it caught, the same on every car there is**, a fixed reach from the hinge. The coupling is spent
  as **one impulse and its opposite**, which is the only way this engine actuates anything (`SOL-3`), and it
  adds no momentum to the pair. **It is a stiff coupling and not a rigid one**: a turn taken tighter than
  the trailer can follow stretches it and scrubs the trailer round. **A joined pair still collide**: the
  physics knows nothing about the coupling, so a truck that shunts the car on its own arm hits it as it
  would hit anybody.
- **Two pictures.** The arm is the **one part of a vehicle in this town drawn as a picture of its own**,
  because it is the one part that moves against the body it is bolted to: **drawn in over its own deck with
  nothing on it, and reaching out at the car it is holding when there is**. **Its reach is a distance
  somebody drew** and lives in that variant's own file beside the picture it was measured off, on
  `CAR-14a`'s terms — the fork on screen and the point the tow is spent at are one number. **It has two
  lengths and nothing in between**, the picture swapped on the tick a car goes on the bar, because nothing
  else in this town animates.
- **Two wheels.** The caught end is lifted onto the arm, so its pair leaves the ground and **the far pair**
  carries what the arm is not holding up. Those two wheels roll — they are not the locked block `PHY-5`
  describes, because nothing is braking them — and their sideways grip is what makes the wreck track the
  vehicle pulling it rather than swing about behind the arm.
- **One movement, and so one occupant.** A coupled pair is one thing moving down one road
  (`TER-5c.2`): the evacuator's own claim reaches back over the wreck, and the ground the wreck stands
  on that the truck's line does not name — the lane the trailer swings into as the pair turns — is held under
  the **truck's** number and not the wreck's (`TER-4c.2`). Both halves matter: unclaimed, the trailer's
  swing is ground the traffic beside it cannot see; claimed under its own number, it cuts its own hauler's
  grant.

**EVA-6** `P5` A wreck is **set down in a free yard slot**, once the evacuator is standing within reach of
one and the hitching interval has been spent on it. It is a placement — a container's own operation
(`PHY-7a`) over the width of a parking space — and it is refused while no slot is within reach, which is a
wait and not a failure.

**EVA-7** `P5` A wreck standing in a yard slot is **restored** after the repair interval: put back together
where it stands and left there, an ordinary parked car in an ordinary space. Two consequences.

- **A restored service vehicle comes back as an ordinary car**: the hospital or station it belonged to lets
  it go — a depot let its evacuator go when it broke (`SRV-4`) — and the bay held for it is held for nobody
  from then on (`ParkingRegistry.Claimed`); it does not go back to the town.
- **The slot stays full until something takes the car out of it.** A yard that fills with mended cars
  nothing has come for is a depot that has stopped collecting, which is `EVA-2`'s own state and is counted
  rather than hidden.

**EVA-8** `P5` **Every leg of a recovery is bounded.** A wreck the traffic never lets an evacuator reach is
given up on and the evacuator goes home — or back to its beat (`SRV-5`) — so one unreachable wreck cannot hold
an evacuator out of service for the rest of the run. A **haul** that runs out of clock is drawn again from where the truck
has got to — and only so many times: past that the wreck is **set down where it stands** and becomes a call
again. A rescue's delivery is never given up because the casualty is aboard and there is nothing better to
do with them; a wreck set down is no worse off than where it fell, and what giving it up buys is the town's
evacuator back.

## Where the numbers are

On `SimConfig.Evacuator` ([core](../../../core/docs/requirements.md#where-a-figure-lives)): how many slots a
yard holds, how long the hitch and the workshop take, how near the wreck and how near a slot the truck has
to have stopped, the bound on a leg and how many hauls a wreck is worth, how far inside a car's end the fork
takes hold, how much of a towed car's weight stays on its own wheels, how much wider of its line a tow may
run than a car — and the coupling's own three: how quickly the arm pulls its stretch out, the most it may
spend and what share of that it may spend sideways. Where the arm is and how far it reaches are the
variant's own (EVA-5).

## What the tow cannot do

**A tow cannot take every corner a car can.** The town's corners are laid for the nominal car (`CAR-11a`);
an evacuator is half again as long as one and a coupled pair is more than twice, so a line drawn round a
tight junction or into a bay is a line the pair goes wide of. It is allowed to run wider of its line than a
car before the road calls that line lost, and past that it is a car off its line like any other (`CAR-9`)
— on a dense city there is geometry a tow gets no further through than a rerouting, and `EVA-8` is what
stops that costing the town its evacuator. The instrument that says how far each map's recovery actually
gets is `--bench recovery`.

**The errand winches the last few metres.** `CTL-7`'s action is the same call for a player and for the
errand, but a player drives the truck onto the car and an errand cannot: an evacuator coming up a lane
behind a wreck queues behind it like everything else (`CAR-15`). So a truck standing within reach of its
wreck and finding the arm empty **pulls the wreck onto the fork** — a placement (`PHY-7a`) over the last
few metres — and works the arm on it. The two ends of `CTL-7` are the same action reached from the same
place but not the same drive, and how often the winch is reached for is the tolerance on settling at the
fork's mark, which cannot be tightened to nothing ([decision-log.md](decision-log.md)).
