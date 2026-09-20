# The person agent — requirements

The walker, and the trip that gives it a reason to move. Containment is
[world/containment](../../../world/containment/docs/requirements.md); the network a walk is searched over
is [world/foot](../../../world/foot/docs/requirements.md); what a claim is and how strong one is, is
[world/road/claims](../../../world/road/docs/claims.md).

**There is no named walking catalogue yet.** AGT-7 asks for one and this slice does not have it; that is
a gap, recorded in [docs/index.md](../../../../docs/index.md), not a decision.

## What a person is and does

**PER-1** `P4` A person is an agent **at all times**. Containment does not remove agency — it replaces the
action set.

**PER-3** `P3` Forward speed is **constant when moving**, modulated by the occupied terrain. There is **no
acceleration profile** above the foot friction that produces it.

**PER-6** `P5` While inside a building the only available action is exiting it.

## Soft rules

**PER-7** `P5` The walker's soft rule set. Each is an intention it can fail to keep, and a failure has a
defined recovery (PER-8) rather than a correction applied to the body.

**PER-7.1** `P5` Do not intentionally collide with any object.

**PER-7.2** `P5` Move only on walkable terrain. This is a fact about the **shape of the network** rather than a
check run afterwards (TER-3c.1): there is no edge that touches a carriageway except a crossing.

**PER-8** `P5` On a soft rule violation — pushed onto a road, say — move to the nearest valid space. It is
PER-25's second walk and not a rule of its own: a body off the line it was laid walks back onto the
network.

**And where the walk back cannot be walked, the body is set down on it.** The straight back to the
pavement is a straight, so a body shoved behind a building leans on the wall between for as long as the
town runs; a walker steers at nothing and avoids nothing (PER-25), so there is no second answer to find.
**It is the give-up clock that says when** — the same one that ends any leg going nowhere — and it applies
only to a body that is on no way of the network, a body held up on one being held up by something the
solver is already arguing with. **How often a town has to do it is a figure its instruments print**: a
placement is the town papering over its own ground, and a town doing it often has doors opening onto
places nothing can walk out of.

## Walking

**PER-25** `P4` **A walker holds a route as the ways it is travelled and walks them one at a time, and
there is nothing else it does.** The pavement is contracted once when the town is stood up and a walk is a
search over it, expanded into the network's own ways exactly as a drive is expanded into lanes — so a
walker steers at nothing, avoids nothing and plans nothing: the ways of the route are where it goes and the
order it goes in them. **It is held on each way's own line**, which the network already carries, so a walk
originates no geometry of its own beyond the one short hop off the network onto a doorstep.

**A body that is on no way of the network walks straight at the nearest point of one.** That is the whole
of the second case and it is the common one, not a corner of the rule — a doorway stands off the walk, a
body shoved off its line is standing on grass, a casualty put back on its feet is wherever the hospital
left it. The line is laid again from where the body has got to and its first leg is the straight back onto
the pavement.

**Which of the two it is, is read off the line and never searched for.** Every point of a walked line
carries the way it stands on and the metre of that way, so a walker's place on the network costs a
subtraction; a body further off the stretch it is walking than that stretch has pavement either side of it
is on none of it, and that is the same bar the driving side calls a line lost by.

**A line is a bound on work and never a plan.** It carries a fixed number of points, and a route longer
than that is walked as far as it reaches and laid again from there — which is the same thing that happens
to a body that has lost it, and needs no second mechanism.

**A leg that gets nowhere is given up rather than walked down a ladder.** There is no arbitration between
walking manoeuvres because there are no walking manoeuvres (AGT-7): a body that has got no nearer the
**end of the way it is walking** for a stated time draws another destination, and how long that is, is
data. **What that is measured against has to shrink as the walk goes well** — the point the follower aims
at stands a stride in front of the body and travels with it, so a walker at full pace is exactly as far
from it as a walker held against a wall.

## Reservation

**PER-26** `P4` **A walker lays two claims and no more** ([claims](../../../world/road/docs/claims.md),
TER-5g): the ground it is standing on at **p0**, and the ground it is walking at at **p9**. Both are laid
from the body every tick, neither is answered, and nothing else about a walker is written onto any way.

**The first is TER-4c.2 said of somebody on foot**, and it is nothing new: a body holds the ground it
occupies whatever kind of body it is, on every way that ground belongs to — the pavement's two lanes, the
mitres of a corner it is standing across, the lane it is standing in, the joins of a junction it is under
and the ways of a bay. **Nothing takes it**, because its holder is already there.

**The second is a statement of intent and the weakest hold there is.** It runs from the body's own front to
where it is aiming, on each way of **the pavement** that stretch crosses; everything stronger takes it, and a
body on no way of the network states nothing at all, there being no way to state it on. **It is stated on the
walking network and on nothing else** — a walker's intent buys it no standing on a carriageway, there being
nothing on this side of the town that grants one.

**There is no grant on this side of the town, and that is the whole of the difference from a driver.** A
driver asks for road and is handed a distance because a car has a speed profile to spend it on; PER-3
leaves a walker no profile, so a distance in front of one buys nothing the ground it is standing on does
not already say. **A walker therefore never queues** — it walks at what it was laid, and what it walks into
is the solver's (`PHY-1`) rather than a rule's.

**What the two claims are for is that the rest of the town can see a walker**, and the first is the whole of
it: a body on a lane cuts the road a driver was granted like anything else standing there.

**A crossing is ground and not a rule.** A walker on the paint holds the lane under it exactly as a walker
shoved onto bare tarmac does, and is owed nothing more for being on a zebra — no band, no rank, and nothing
stated in front of it on a carriageway. What holds the traffic off somebody crossing is the same grant the
same claim cuts anywhere else in the town (TER-5e), which is one mechanism rather than two (SIM-7).

**Nothing waits at a kerb.** There is no gap to be judged, no patience to be spent and no signal to be read:
a walker steps out when its line says to, and what stops it walking into a car standing there is the car's
own body and the solver, exactly as on any other ground. **Whether a driver can stop in time is the
driver's arithmetic and the solver's**, and a contact that follows is PER-23's.

## The trip

**PER-9** `P5` Walk around the city from building to building. Destinations are drawn from the **agent seed**.

**And a walker begins the round where it ends one** — inside a building, dwelling (GEN-7). There is no
first leg that is different from the rest: building, the place it was going, building.

**PER-11** `P5` On arrival the person enters if the building has spare capacity and **dwells inside** before
drawing the next destination.

**Arriving is not a radius.** A search will happily prove that a body within some distance of a door has
arrived when it is on the wrong side of a wall; arrival is a fact about the leg being finished.

**Every trip is walked.** No leg of one is driven, no car is chosen and no bay is claimed on anybody's
behalf — what a car does in this town is its own (`CAR-1`), and the two rosters meet only on the ground
they share. It is where this slice stands rather than a rule about people: the walking was put back on a
simple footing first and the driven leg comes back onto it
([docs/index.md](../../../../docs/index.md#known-gaps)).

## Damage

**PER-23** `P3` A person is **knocked down** by a vehicle when the contact carries enough energy to put a body
off its feet further than a stated distance along the ground — the work of sliding their own mass that far
on the sliding grip, and nothing anybody chose in kilojoules. **A car is the only thing that can do it**
(`PHY-4a`), and **who was moving carries no weight**: the closing speed and the two masses are the whole
of the arithmetic, so a body that arrives at a car is judged as a car that arrives at a body.

**The band sits above the town's own walking pace**, and that is what makes the sentence before it
liveable. A tolerance below the pace is one a walker meets by arriving at a parked car, and then a
knock-down is a contact rather than an impact — nobody has to be struck for the town to fill with
casualties. Half again over the pace is what the shipped figures give.

**There is no band above it.** The energy that breaks a car does no more to a person than the energy that
just moves them, because a person has one tolerance like every other kind of body (`PHY-3`, `PHY-4`).

**PER-18** `P5` And a person who is down is a **casualty**: lying where they fell, taking no actions of their
own, off their feet, and waiting for an ambulance
([agents/ambulance](../../ambulance/docs/requirements.md)). **It is not a terminal state** (AGT-5) — a
casualty is collected, treated and put back on the pavement free to draw a trip again. **Nothing that moves
touches it while it is down** — what that means to the solver is `PHY-5b`.

**Going down and losing your feet are one fact.** The body keeps whatever the impact gave it and slides to
a stop on the ground rather than on any intent of its own, and it is still there when it stops: nothing
about being knocked over wears off on a clock, and only a hospital puts somebody back on their feet.

**Everything the trip was holding is given back at the moment they go down.** A casualty is not going to
walk to the building it had claimed, and a claim held by a body lying in the road is a place removed from
the town for as long as the rescue takes.

**A casualty is a body in the road and not somebody crossing it.** They hold the ground they lie on and
cut every grant that runs over it, so a driver is held off them exactly as off a wreck.

## The foot model

A person is a rigid body like everything else that moves, with **rotation locked and gravity off**. It
actuates exactly one thing: a **desired velocity declared for this tick** by whatever has charge.
Everything else is the solver's.

**Foot friction is the whole acceleration model.** Turn the declared velocity into one central impulse:
ask for the full correction `(desired − v) × m` and spend **no more than `grip × m × dt`** of it. There is
no acceleration curve anywhere above it, which is what makes "pace is a cap, never a profile" honest.

**An impulse of nothing is never applied.** An impulse call is typically what keeps a body out of the
solver's sleep, so a walker standing still that is asked for zero must be left alone.

**Two grips.** On its feet, a sole pressed into the ground; off its feet, a body along it. A walker is off
its feet exactly while it is a casualty (`PER-23`), which is what makes the impulse of an impact visible
after the impact is over — a body sent down the road rather than stopped where it was hit. Both are scaled
by the terrain's own grip factor. **The sliding grip is what sizes the band**: half a metre of it is what
being knocked over costs, so the two numbers are one decision.

> **The relation that is the requirement — the number is not:** a walker reaches its pace, and loses it,
> **inside a fifth of its own body.** Whatever the walk speed is set to, the grip is whatever makes that
> true.

> **And the second grip is a share of the first, never a figure of its own.** This town's distances are
> real and its pace is five times a real one, so every acceleration in the model carries a factor of
> twenty-five that no figure states. A sliding grip authored as though the pace were real is twenty-five
> times too cheap, and the band it sizes lands underneath walking pace.

**Heading is intent, not solver output.** Rotation is locked and set by code, so a walker may turn on the
spot at its turn rate regardless of where it is travelling; the follower turns first and steps second.

**One thing this model gets wrong, stated rather than fixed**: a walker's feet resist a car pushing them
at `mass × grip`, which is about half a car's drive, so a car leaning on a pedestrian *below* `PER-23`'s
band is pushing something that braces rather than something that gives — and, still on its feet, it can be
shoved several metres by a car that never knocked it over. The fix needs a contact count kept per walker,
and a count that fails to come back down is a walker who slides for the rest of the run.
