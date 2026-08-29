# The evacuator — requirements

The recovery: the depots a town has, the evacuator standing at each, and what happens between a car being
wrecked in the street and being a car again.

**An evacuator is a service vehicle and drives the car's catalogue**
([agents/service](../../service/docs/requirements.md), [agents/car](../../car/docs/requirements.md),
[the manoeuvres](../../car/maneuvers/docs/index.md)). What it is made of, where it stands and what happens
when one is wrecked are `SRV-1` to `SRV-4`; what is here is the *errand* those manoeuvres are run for, and
the one thing in this project that couples two bodies together. Why it reads this way is
[decision-log.md](decision-log.md).

## The call and the place

**EVA-1** A car in its terminal state (`PHY-3`) is a **wreck**, and a wreck is a call. It is raised where
the car breaks, so nothing searches the fleet for one, and it stays one until an evacuator has it on the
bar. A wreck standing in a yard slot is not a call, and neither is one already somebody's.

**EVA-2** Each depot keeps a **yard**: its evacuator's own bay, and a run of **slots** beside it held for
wrecks and for nobody else (`GEN-4k`). Three things follow.

- **A yard slot is a hold that names no vehicle.** An apron bay is held for the one car that stands in it
  for the whole run; a slot is held for whichever wreck was fetched last, and stands empty most of the time
  on purpose.
- **A depot takes the bays the map has**, on `SRV-2`'s terms: one with fewer free bays near it than the
  figure asks for keeps a smaller yard, and one with none keeps none. Both are real states and are reported
  (`--bench recovery`).
- **A full yard is a depot that has stopped collecting**, and that is a wait rather than a failure — the
  same state a full hospital's door puts an ambulance in (`OBJ-5`).

## The errand

**EVA-3** The nearest evacuator with nothing else to do takes the nearest wreck nobody is on their way to,
and **nearest is measured against every other free evacuator and not against every other wreck**: a crew
that is not the nearest to the wreck it would have gone to takes nothing and asks again. **One wreck to a
recovery and one recovery to a wreck.**

**Somewhere to put it is part of taking the call.** An evacuator whose yard has no free slot takes nothing,
because one that set off anyway would arrive with a wreck on the bar and nowhere to set it down, and would
then stand at its own yard holding it for the rest of the run.

**EVA-4** An evacuator **on its way to a wreck** carries the whole of an ambulance's priority (`AMB-4`):
the rank above every other movement and above the paint, the red that does not apply, the kerb it owes no
stop to, the overtake it does not spend patience on, and a pace of its own. Two limits, and the second is
the point of the rule.

- **The blue light buys the road and never the tyres** here exactly as it does for a rescue (AMB-4a): every
  constraint the speed profile already takes is still taken, and a priority is never a licence to drive
  into anybody.
- **It is the outbound leg and nothing else.** An evacuator standing at the scene, **hauling**, unhitching
  or driving home is ordinary traffic and holds its road like everybody else. What is urgent about a
  recovery is getting to the wreck; what is left afterwards is a slow vehicle with a load on the back, and
  a load on the back is the last thing that should be hurried through a town.

**EVA-5** A wreck is **towed and never carried**. It stays a body in the world the whole way (`PHY-5`),
and the tow is five things and no more:

- **One action, worked by somebody standing at it.** The arm is **worked** — swung out onto whatever is
  within its reach behind the truck, or back in when there is nothing there — and **working it is the whole
  of a recovery vehicle's action** (`CTL-7`). One call does it, and a crew and a hand on the keys reach for
  the same one, so what an evacuator can do is exactly what a player can. **The crew reaches it on foot**
  (`SRV-3`): the recovery man gets out, walks to the wreck, and the interval the hitch takes does not begin
  until he is standing at it — nothing about the arm is reached from inside the cab, and the same is true of
  setting a wreck down in a yard slot (EVA-6). **What it catches, it catches by either end**, and a wreck
  is not special: anything with a body may go on the bar, and what is on the bar takes no decisions until it
  is let off. **Its wheels are straightened as it goes on**, because the pair left on the ground may be its
  steered one and a car dragged on a wheel wound over is being scrubbed sideways down the road.
- **An arm.** It is **hinged on the evacuator's deck and clamped to the car it has lifted** — swinging
  freely at one end and not at all at the other — and it takes hold of that car **a fixed distance inside
  the end it caught, the same on every car there is**, a fixed reach from the hinge. What an underlift goes
  under is the bodywork; where that car's axle happens to sit is its own business. The coupling is spent as **one impulse and its opposite**, which is the
  only way this engine actuates anything (`SOL-3`), and it adds no momentum to the pair. **It is a stiff
  coupling and not a rigid one**: a turn taken tighter than the trailer can follow stretches it and scrubs
  the trailer round, which is what a real one does at walking pace. **A joined pair still collide**: the arm
  holds the load a fixed distance behind the deck and the physics knows nothing about the coupling, so a
  truck that shunts the car on its own arm hits it as it would hit anybody, and a corner that closes the
  daylight the arm holds is a contact and not an exemption.
- **Two pictures.** The arm is drawn, and it is the **one part of a vehicle in this town drawn as a picture
  of its own**, because it is the one part that moves against the body it is bolted to: **drawn in over its
  own deck with nothing on it, and reaching out at the car it is holding when there is**. Two pictures and
  not one turned, because an arm that is out is a different shape. **Its reach is a distance somebody drew**
  and lives beside the picture it was measured off, on `CAR-14a`'s terms — the fork on screen and the point
  the tow is spent at are one number, so the two cannot disagree.
- **Two wheels.** The caught end is lifted onto the arm, so its pair leaves the ground and **the far pair**
  carries what the arm is not holding up. Those two wheels roll — they are not the locked block `PHY-5`
  describes, because nothing is braking them — and their sideways grip is what makes the wreck track the
  vehicle pulling it rather than swing about behind the arm.
- **One movement.** A coupled pair is one thing moving down one road (`TER-5c.2`): the evacuator's own
  reservation reaches back over the wreck, and the wreck asks for no ground of its own. That is also what
  holds the traffic behind off the trailer rather than off the truck.

**EVA-6** A wreck is **set down in a free yard slot** by the crew, once the evacuator is standing within
their reach of one, **the man is out and standing at it** (`SRV-3`), and the hitching interval has been
spent on it. It is the one placement in this errand — a container's own operation (`PHY-7a`) over the width
of a parking space — and it is refused while no slot is within reach, which is a wait and not a failure.

**EVA-7** A wreck standing in a yard slot is **restored** after the repair interval: put back together
where it stands and left there, an ordinary parked car in an ordinary space, free for whoever walks past to
drive away (`PER-4`). Two consequences.

- **A restored service vehicle comes back as an ordinary car.** Its crew got out when it broke and is not
  coming back, so the hospital, station or depot it belonged to lets it go and the bay held for it is given
  back to the town.
- **The slot is the town's until somebody takes the car out of it.** A yard that fills with mended cars
  nobody has walked to is a depot that has stopped collecting, which is `EVA-2`'s own state and is counted
  rather than hidden.

**EVA-8** **Every leg of a recovery is bounded.** A wreck the traffic never lets an evacuator reach is
given up on and the evacuator goes home, so one unreachable wreck cannot hold a town's only evacuator out
of service for the rest of the run. A **haul** that runs out of clock is drawn again from where the truck
has got to — and only so many times: past that the wreck is **set down where it stands** and becomes a call
again. A rescue's delivery is never given up because the casualty is aboard and there is nothing better to
do with them; a wreck set down is no worse off than where it fell, and what giving it up buys is the town's
evacuator back.

## Where the numbers are

On `SimConfig.Evacuator` ([core](../../../core/docs/requirements.md#where-a-figure-lives)): how many slots a
yard holds, how long the crew and the workshop take, how near the wreck and how near a slot the crew can
work from, the bound on a leg and how many hauls a wreck is worth, how far inside a car's nose the fork takes hold —
and the coupling's own three: how quickly the arm pulls its stretch out, the most it may spend and what
share of that it may spend sideways.

**Where the arm is and how far it reaches are not among them.** They are measurements off its picture and
off the truck's, so they live in that variant's own file beside both — `CAR-14a`'s rule about a lamp lens,
said of the one moving part a vehicle here has.

## What is not built

**A tow cannot take every corner a car can.** The town's corners are laid for the nominal car (`CAR-11a`);
an evacuator is half again as long as one and a coupled pair is more than twice, so a line drawn round a
tight junction or into a bay is a line the pair goes wide of. It is allowed to run wider of its line than a
car before the road calls that line lost, and past that the ladder recovers it like anybody else — but on a
dense city there is geometry a tow gets no further through than a rerouting, and `EVA-8` is what stops that
costing the town its evacuator. The instrument that says how far each map's recovery actually gets is
`--bench recovery`.

**A crew still winches the last few metres.** `CTL-7`'s action is the same call for both, but a player
drives the truck onto the car and a crew cannot: an evacuator coming up a lane behind a wreck queues behind
it like everything else, and nothing in the catalogue (`MAN-4`) will take a body past an obstruction and
stop it a set-down beyond. So a crew standing within reach of its wreck and finding the arm empty **pulls
the wreck onto the fork** — a placement (`PHY-7a`) over the last few metres — and works the arm on it. What
is missing is a manoeuvre that backs a truck onto a body, and until there is one the two ends of `CTL-7`
are the same action reached from the same place but not the same drive.

**Getting the truck onto its mark is the whole of what the winch is covering**, and how much of the last of
it is left to cover is a figure rather than a rule: the leg is aimed at the exact place the fork takes hold
from, and the tolerance on settling there decides how often the winch is reached for at all. **It cannot be
tightened to nothing.** A truck refused its own mark lays the leg again until the clock ends the recovery,
and a man who never gets out never reaches the winch either — at one car length the shipped maps stopped
recovering anything at all, which is why `EVA-5`'s reach is a tolerance and is stated as one.

**The arm has two lengths and nothing in between.** It is drawn in or it is out, and the change is a
picture swapped on the tick the wreck goes on the bar. Nothing extends it over a second or two, because
nothing else in this town animates.
