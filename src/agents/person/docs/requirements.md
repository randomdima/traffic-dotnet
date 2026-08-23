# The person agent — requirements

The walker, and the trip that gives it a reason to move. Containment is
[world/containment](../../../world/containment/docs/requirements.md); the bay a trip claims is
[world/parking](../../../world/parking/docs/requirements.md).

**There is no named walking catalogue yet.** AGT-7 asks for one and this slice does not have it; that is
a gap, recorded in [docs/index.md](../../../../docs/index.md), not a decision.

## What a person is and does

**PER-1** A person is an agent **at all times**. Containment does not remove agency — it replaces the
action set.

**PER-2** Actions: turn in place at a constant angular rate; move forward at a constant speed; idle;
enter and exit a container.

**PER-3** Forward speed is **constant when moving**, modulated by the occupied terrain. There is **no
acceleration profile** above the foot friction that produces it.

**PER-4** A person may only enter a car that is **free, stopped and intact**.

**PER-5** A car may name an **owner**, and ownership is a property of the map (world seed), never of the
behaviour.

**PER-6** While inside a building the only available action is exiting it; while inside a car, exiting it
and driving it.

## Soft rules

**PER-7** The walker's soft rule set. Each is an intention it can fail to keep, and a failure has a
defined recovery (PER-8) rather than a correction applied to the body.

**PER-7.1** Do not intentionally collide with any object.

**PER-7.2** Move only on walkable terrain. This is a fact about the **shape of the network** rather than a
check run afterwards (TER-3c.1): there is no edge that touches a carriageway except a crossing.

**PER-7.3** Do not cross a red pedestrian light. **Red means do not *begin* crossing** (TLT-2a).

**PER-8** On a soft rule violation — pushed onto a road, say — move to the nearest valid space.

## Following

**PER-13** A walker is **granted the pavement in front of it** and walks only into ground it has been
granted. The grant is the driver's, over the walking network's own ways: every walker asks for the lane
from its own back to where it can come to rest at the pace it is walking, plus the gap it keeps, and is
given what is left of that in front of the nearest body already on it. **Nobody is granted ground somebody
else will still be standing on once they have stopped**, which is the whole of what holds one walker off
the next.

**A grant is read as a permission and never as a speed.** PER-3 leaves a walker no profile to hand a
distance to, so what a short grant does is stop it where it stands; the pace it walks at when it walks is
unchanged.

**The two directions of a stretch are two lanes and never one.** Somebody coming the other way is on
other ground, so a walk is never held up by one — that is a fact about the shape of the network (PER-7.2)
and not a test anybody runs.

**Waiting behind a body that is under way is not being stuck**, and the clock that gives a leg up (PER-8)
does not run while it is. Waiting behind one that is going nowhere *is*: the leg is given up on and
another is drawn, which is how a walker comes to be handed a line round it rather than a second rule about
stepping over things.

**The pavement's book holds walkers and nothing else, and a zebra is where that matters.** The paint is a
walk laid over a carriageway, so a car crossing one has a stretch of the *lane* and writes nothing on the
walk (TER-5c.1); what cuts a walker's grant there is that stretch, looked up where the crossing runs over
the lane. The mirror of it holds on the other side: a person standing anywhere on a carriageway is a stretch
of the lane it stands in, and cuts the road a driver is granted
([world/road](../../../world/road/docs/requirements.md)).

## Crossing

**PER-15** **A walker steps onto a crossing when the lane it is stepping into is inside nobody's road, and
never on a gap in the traffic.** It is `PER-14`'s rule said of paint: a reservation runs from a car's own
tail to where that car is committed to being able to stop, so a car far enough away to stop for this body
holds none of that ground and one that is not, does. **The time something would take to arrive is the
arithmetic behind that reservation** and is not computed a second time here.

**A zebra is a road and not one thing, and it is crossed a lane at a time.** A body on the paint holds the
band of the lane it is standing in and the band in front of it, and never a third: a lane it has cleared is
given back to the traffic in it, and a lane two along was never this body's to ask for. **The band in front
is asked for and answered** on TER-4c.1's terms — granted where no car's road is over it, refused where one
is — and **the kerb is only where the body happens to be standing when it asks**. One at a lane's edge half
way over asks the same question about the same strip of road, and the answer cannot turn on which side of a
kerb line the asker is.

**It is asked for when the body's own ask reaches it** and not on entering the lane before it, which is the
same bar a car's road is held to: a stride into the near lane is not a reason to stop the traffic in the far
one. What the walker is asking for is a stride and a gap, so the band it is entering is asked for about a
stride before the foot goes down — early enough that nothing is ever walked into unheld, and late enough
that a lane is not held by somebody who is still a crossing's width from it.

**Granted, the lane is this body's and it walks into it.** The traffic in that lane is cut at the band, the
walker needs nobody's leave, and nothing asks a second time at the moment the foot goes down. **Refused, it
is granted the walk up to that lane's edge and no further**, which is what a wait for a gap is: the body
stands at the kerb line of a lane a car has, and the road is what says when it may have it.

**The car's side of it is the same arrangement, read from the other end.** A car has the stretch of its own
lane and takes nothing on the walk; it is refused a zebra by the body standing on it, which is a stretch of
that same lane. **And a car stopped short of a crossing holds none of it** (TER-4c.1): the ground beyond a
stop is not the stopper's, or a car waiting at its own red would hold the paint shut against the people
whose green it is.

**One body takes a lane it was refused**: the one that has waited past its patience — at the kerb or stopped
at a lane's edge half way over — which is the escape below and the reason nobody is left standing in a road
for as long as the street is busy. It is the single place in the town where ground is taken that somebody
else's road is over, and the cars give way to it.

**A red is not a gap question and no amount of clear road answers it** (PER-7.3): the signal is asked
first and refuses outright, and the ground is asked second. **Past the patience the walker goes anyway** —
a crossing that never clears is a jam rather than traffic, and a pedestrian has priority, which is what the
crossing is for. Cars then stop, because the body on the paint is what cuts *their* grant. **The patience
is spent on standing in the road as much as on standing at its edge**, and what it is spent on is one
lane: it is given back when the body is standing in that lane, and not when the traffic gave way — handed
back then, it buys one tick of ground and the wait begins again.

## The trip

**PER-9** Walk around the city from building to building. Destinations are drawn from the **agent seed**.

**PER-17** **Whether a trip is walked is structural and never a weighted coin.** It is walked when the
route to the destination never sets foot on a carriageway — the same block, however far round it is — or
when the destination is inside the walk-worth distance; anything else is worth a car. The route the planner
actually laid is what answers the first half, so "the same block" is a fact about that route rather than a
reading taken off the distance, and a town's traffic is therefore a property of how it was laid out and
comes out the same for the same seed.

**PER-10** A car trip is: walk to this trip's car if it is free, stopped, intact and within a walk; enter
it; drive to a bay near the destination; park; walk the rest.

**PER-10a** **No leg of a trip is a long walk, whether the trip chose the leg or not.** The bay a car aims
at is claimed **within a walk of the destination and only once a route to it exists** — claiming first and
routing afterwards strands the car at a bay nothing can reach.

**PER-11** On arrival the person enters if the building has spare capacity and **dwells inside** before
drawing the next destination.

**Arriving is not a radius.** A search will happily prove that a body within some distance of a door has
arrived when it is on the wrong side of a wall; arrival is a fact about the leg being finished.

## Nowhere to be

**PER-14** A walker on a map with **nowhere to go and no pavement to walk along paces the road beside it**:
out from where it was put down into the middle of the nearest lane, a stand, and back again. It is what the
proving ground has instead of a light — nothing warns a driver it is coming, so the whole of what stops a
car there is the driver looking at what is in front of it.

**It is the one place PER-7.2 is set aside, and what sets it aside is there being no network to be on.**
That rule is a fact about the shape of the walking network, and a map with no pavement has laid no edges at
all. Where there is a network, a walker with no trip wanders it and crosses on the paint like any other.

**What it waits for is ground nobody has taken, and never a gap in the traffic.** A reservation is the road
a driver is committed to ([world/road](../../../world/road/docs/requirements.md)), so a body put down
beyond every one of them is a body every car on the road can still stop for. Waiting for an empty road
instead would be a walker who only ever stepped out when nothing was coming, which is a walker no driver is
ever tested by. The clear ground past the far edge of that road has to be worth the walk across it, at the
speed whatever owns the road behind is closing at.

**The stand in the lane ends when something has come to rest for it, and at nothing else.** That is what it
stepped out for; a clock that ended the stand early would let a driver already braking pick the throttle up
again. It walks off the moment there is a car standing in front of it and not a moment after, because a body
still in the way of a driver who has already answered is measuring that driver's patience.

**And it steps out again the moment that car has gone past, and waits on the pavement for nothing else.**
Ground somebody is standing on is ground they have taken, so the same test that let it out the first time
holds it there while the car it stopped is still in front of it and while a queue closed up behind that car
is going by — and lets it straight back out behind the last of them. There is no beat between two paces:
what times a pace is the traffic, and a walker made to sit out a clock on the pavement is a body the road
stops being tested by for as long as the clock runs.

**The one clock is the bound on the stand in the lane**, and it is there for the road nothing ever comes
down. It sits under the driver's own blocked-road fuse
([agents/car](../../car/docs/requirements.md)), so a car that arrived at the start of a stand never walks
the ladder around a body that is about to move off anyway.

**PER-16** A walker on such a map that was put down **in a carriageway rather than beside one reels down
it**: a lurch a few seconds long further along the lane it is on, thrown anywhere across the width of that
lane, and every few lurches a stand where it stopped. **Which of the two rules a body follows is the pose
the map left it in and never a name**, so a scenario is a map rather than a special case in the agents.

**It keeps to its own lane and to the way it is facing.** The two lanes of a carriageway run opposite ways,
so a body that took the nearest lane's answer wherever it had wandered would reel a few metres one way and a
few the other and never leave the place it started; and a body over the centreline is one nothing may
lawfully pass, on a road whose only other ground is a verge. **A bend is not cut across** — a body walks at
what is in front of it and not along the road, so the lurch is no longer than the chord that stays inside
the lane, which is the corner formula the traffic is held to.

**It asks nothing of the traffic and the traffic is what keeps it alive.** A reservation cut at this body is
a car committed to stopping short of it, so the road in front of it is its own to walk down; what it will
not walk into is a **body** — a car that has stopped, a wreck, somebody else reeling down the same lane —
because those it walks into rather than the other way round, and nobody is holding off on its behalf.

**Standing is what makes it two things to a driver rather than one.** Walking, it is something slow to be
followed and then overtaken once a driver has been held below the road's pace for longer than it waits;
stopped, it is something in the way, which a driver waits the same time behind and then gets past
([agents/car/maneuvers](../../car/maneuvers/docs/e04-go-round.md)). Both are wanted, so the stand is longer
than that wait and well under the blocked-road clock.

## Damage

**PER-12** A person **dies** when a contact carries at least their fatal energy, and then cannot act
(AGT-5).

**PER-12a** A person who **survives** a contact with a car — above their shake energy, below their fatal
one — is taken **off their feet** for the stumble window and then gets up.

## The foot model

A person is a rigid body like everything else that moves, with **rotation locked and gravity off**. It
actuates exactly one thing: a **desired velocity declared for this tick** by whichever manoeuvre has
charge. Everything else is the solver's.

**Foot friction is the whole acceleration model.** Turn the declared velocity into one central impulse:
ask for the full correction `(desired − v) × m` and spend **no more than `grip × m × dt`** of it. There is
no acceleration curve anywhere above it, which is what makes "pace is a cap, never a profile" honest.

**An impulse of nothing is never applied.** An impulse call is typically what keeps a body out of the
solver's sleep, so a walker standing still that is asked for zero must be left alone.

**Two grips.** On its feet, a sole pressed into the ground; off its feet, a body along it. A walker is off
its feet when it is dead, or for the stumble window after a vehicle struck it and it survived — the whole
difference between being knocked over and being sent down the road, and what leaves the impulse of an
impact visible after the impact is over. Both are scaled by the terrain's own grip factor. **Intent is
never suspended by the stumble**; only the friction that could act on the declaration is.

> **The relation that is the requirement — the number is not:** a walker reaches its pace, and loses it,
> **inside a fifth of its own body.** Whatever the walk speed is set to, the grip is whatever makes that
> true.

**Heading is intent, not solver output.** Rotation is locked and set by code, so a walker may turn on the
spot at its turn rate regardless of where it is travelling; the follower turns first and steps second.

**One thing this model gets wrong, stated rather than fixed**: a walker's feet resist a car pushing them
at `mass × grip`, which is about half a car's drive, so a car nudging a pedestrian *below* the shake
energy is pushing something that braces rather than something that gives. The fix needs a contact count
kept per walker, and a count that fails to come back down is a walker who slides for the rest of the run.
