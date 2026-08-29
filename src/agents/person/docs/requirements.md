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
given what is left of that in front of the nearest body already on it **that is going somewhere**. **Nobody
is granted ground somebody else will still be standing on once they have stopped**, which is the whole of
what holds one walker off the next.

**What is under way is the whole of what cuts it** (PER-24). A body going nowhere is on the same ground and
in the same book, and it is left out of this one question because standing behind it is not what a walker
does about it.

**And in front is a fact about the two bodies rather than about the ground they hold.** A stretch begins a
margin behind its owner, so one that reaches back over the asker belongs to a body that may be level with it
or past it; only a body whose own length reaches past the asker's front is in front of it. Two abreast at one
metre of a way — which is what the end of a way makes of everything carried back to it — are in front of
nobody, and asked as though each were in front of the other, neither may ever move again.

**A grant is read as a permission and never as a speed.** PER-3 leaves a walker no profile to hand a
distance to, so what a short grant does is stop it where it stands; the pace it walks at when it walks is
unchanged.

**The two directions of a stretch are two lanes and never one.** Somebody coming the other way is on
other ground, so a walk is never held up by one — that is a fact about the shape of the network (PER-7.2)
and not a test anybody runs.

**Waiting behind a body that is under way is not being stuck**, and the clock that gives a leg up (PER-8)
does not run while it is. It is now the only thing a walker waits behind at all, so the clock is left for
a body that is genuinely getting nowhere — one whose step round is refused by the ground on both sides, or
one being pushed.

**The pavement's book holds walkers and nothing else, and a zebra is where that matters.** The paint is a
walk laid over a carriageway, so a car crossing one has a stretch of the *lane* and writes nothing on the
walk (TER-5c.1); what cuts a walker's grant there is that stretch, looked up where the crossing runs over
the lane. The mirror of it holds on the other side: a person standing anywhere on a carriageway is a stretch
of the lane it stands in, and cuts the road a driver is granted
([world/road](../../../world/road/docs/requirements.md)).

## Getting past

**PER-24** A walker **steps round a body that is going nowhere rather than waiting behind it**. What counts
as one is what a driver counts (`E-4`): a wreck, somebody knocked down, a walker standing about or shoved
off its own line — and **never somebody under way along the same lane**, who is followed and never stepped
round. The step is taken the tick the walk runs into one, it is to the walker's **right**, and it is **the
least that gets past** — the two bodies and the room between shoulders, off the aim the walk already had.

**Nothing is planned and nothing is remembered.** The line is untouched, the aim comes back onto it as the
body goes abeam, and a walker that is clear of one across its own walk is not stepping round anything at
all. That is what makes the divergence the smallest thing that could work: it lasts exactly as long as the
thing that caused it, and a walker cannot be left steering round a body that has moved.

**A step may leave the walk, and the pavement is not the bound on it.** A lane's line runs about a body's
width from the edge of its band, so the step round something standing on that line ends up off the walk
nearly every time — on the verge, the frontage, the far side of the pavement or the channel. **Ground the
traffic is not on is a walker's to step onto**, walk or no walk; held inside the band, the rule would be a
rule that almost never applied.

**What bounds it is the carriageway, and that is grazed rather than entered.** The middle of the body may
pass the kerb line by a stated distance and no further: at the channel with the kerb underfoot, which is
what a person does to get round something on a narrow pavement, and never far enough to be standing in a
lane. It is the lane's own band that answers this and never the ground grid, whose cells are wider than the
distance being asked about. **A body already on the carriageway is exempt**, because a walk over a zebra is
a walk on the road (PER-15).

**Where both sides are refused there is no step**: the walker stands short of the body and the clock that
gives up a leg (PER-13) draws it a line round. It is the answer a walled-in walker had before there was a
step at all, and it is what keeps a queue of people from shoving a casualty down the street.

**It is where the two agents part company, and the only place they do.** A driver waits behind a wreck and
is taken round it by a manoeuvre with a template, a look and a wait
([agents/car/maneuvers](../../car/maneuvers/docs/e04-go-round.md)); a walker has feet and a stride of spare
pavement, and needs none of it.

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

**A refusal is written down, and that is what the traffic gives way to.** A body refused the band it asked
for puts the ask itself into the road's book — not as a body and not as road anybody has taken, so no
driver's grant is cut at it — and **a car approaching that paint is stopped short of it** (TER-5e). A body
stopped short of a crossing holds none of it, so the band is free on the next tick and the walker steps into
ground the traffic has given up rather than into a gap it found. A thing a driver must be held off that is
in no book is a thing the driver cannot see (TER-4c), and a right of way nobody can see is not one.

**The stop is bounded by the road it takes to make one**, which is what makes the priority safe rather than
merely absolute: a car too close to stop keeps the paint, the band stays refused, and the wait lasts another
moment. **Nobody steps in front of a body that could not have stopped for them.**

**One body takes a lane it was refused**: the one that has waited past its patience — at the kerb or stopped
at a lane's edge half way over — which is the escape below and the reason nobody is left standing in a road
for as long as the street is busy. It is the single place in the town where ground is taken that somebody
else's road is over, and the cars give way to it. **It is the escape and no longer the ordinary way across**:
where the crossing is uncontrolled the traffic gives way before the patience is spent, and what the clock is
left for is a crossing that never clears.

**A red is not a gap question and no amount of clear road answers it** (PER-7.3): the signal is asked
first and refuses outright, and the ground is asked second. **Past the patience the walker goes anyway** —
a crossing that never clears is a jam rather than traffic, and a pedestrian has priority, which is what the
crossing is for. Cars then stop, because the body on the paint is what cuts *their* grant. **The patience
is spent on standing in the road as much as on standing at its edge**, and what it is spent on is one
lane: it is given back when the body is standing in that lane, and not when the traffic gave way — handed
back then, it buys one tick of ground and the wait begins again.

## The trip

**PER-9** Walk around the city from building to building. Destinations are drawn from the **agent seed**.

**And a walker begins the round where it ends one** — inside a building, dwelling (GEN-7). There is no
first leg that is different from the rest: building, a car where the trip is worth one, the place it was
going, building.

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

**PER-23** A person is **knocked down** by a vehicle when the contact carries enough energy to put a body
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

**PER-18** And a person who is down is a **casualty**: lying where they fell, taking no actions of their
own, off their feet, and waiting for an ambulance
([agents/ambulance](../../ambulance/docs/requirements.md)). **It is not a terminal state** (AGT-5) — a
casualty is collected, treated and put back on the pavement free to draw a trip again. **Nothing that moves
touches it while it is down** — what that means to the solver is `PHY-5b`.

**Going down and losing your feet are one fact.** The body keeps whatever the impact gave it and slides to
a stop on the ground rather than on any intent of its own, and it is still there when it stops: nothing
about being knocked over wears off on a clock, and only a hospital puts somebody back on their feet.

**Everything the trip was holding is given back at the moment they go down.** A casualty is not going to
walk to the building it had claimed or drive the car it had booked, and a claim held by a body lying in
the road is a place removed from the town for as long as the rescue takes.

**A casualty is a body in the road and not somebody crossing it.** They hold the ground they lie on and
cut every grant that runs over it, so a driver is held off them exactly as off a wreck; what they are not
is somebody a car owes a stop *short of the paint* to (TER-5e), which is a courtesy owed to people who are
walking. Read the other way, a body knocked down on a zebra holds that crossing shut against the very
ambulance coming to fetch them.

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
