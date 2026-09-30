# The person agent — requirements

The walker, and the trip that gives it a reason to move. Containment is
[world/containment](../../../world/containment/docs/requirements.md); the network a walk is searched over
is [world/foot](../../../world/foot/docs/requirements.md); what a claim is and how strong one is, is
[world/road/claims](../../../world/road/docs/claims.md).

**A walker travels a leg and nothing else** (AGT-7), which is what a driver does over the same code at
the tier where the two are the same thing ([agents/car](../../car/docs/requirements.md) `CAR-15`).

## What a person is and does

**PER-1** `P4` A person is an agent **at all times**. Containment does not remove agency — it replaces the
action set.

**PER-3** `P3` **A walker has no acceleration of its own.** It stands, turns on the spot, or walks straight
at where it is going at its pace, on whatever ground (TER-2), and the last of the way — less than a tick's
walk — it steps onto: so it arrives, and never walks past where it was going or at anything it is not
facing. **What it declares it has on the next tick; what a contact did to it is taken back only at the grip
of its feet**, so it is shoved and thrown like any body and braces against nothing.

**PER-6** `P5` While inside a building the only available action is exiting it.

## Soft rules

**PER-7** `P5` The walker's soft rule set. Each is an intention it can fail to keep, and a failure has a
defined recovery (PER-8) rather than a correction applied to the body.

**PER-7.1** `P5` Collide with nothing by choice: a walker aims at nothing but the ways of its route
(PER-25), and the lane beside one while it gets past somebody standing on it (PER-28), and does not step
onto a crossing the traffic has (PER-27). It steers round nothing else, so what it meets anyway is the
solver's (`PHY-1`).

**PER-7.2** `P5` Move only on walkable terrain. This is a fact about the **shape of the network** rather than a
check run afterwards (TER-3c.1): there is no edge that touches a carriageway except a crossing.

**PER-8** `P5` On a soft rule violation — pushed onto a road, say — move to the nearest valid space. It is
PER-25's second walk and not a rule of its own: a body off the line it was laid walks back onto the
network.

**And where the walk back cannot be walked, the body is set down on it.** The straight back to the
pavement is a straight, so a body shoved behind a building would lean on the wall between for as long as
the town runs, a walker steering at nothing (PER-25). **The give-up clock says when** — the one that ends
any leg going nowhere — and only for a body on no way of the network, a body held up on one being held up by
something the solver is already arguing with. **How often a town does it is a figure its instruments
print**: a placement is the town papering over its own ground, and a town doing it often has doors opening
onto places nothing can walk out of.

## Walking

**PER-25** `P4` **A walker holds a route as the ways it is travelled and walks them one at a time, and
there is nothing else it does.** The pavement is contracted once when the town is stood up and a walk is a
search over it, expanded into the network's own ways exactly as a drive is expanded into lanes — so a
walker steers at nothing and avoids nothing: the ways of the route are where it goes and the order it goes in
them, and what it is held short of is what its reservations are answered with (PER-26). **It is held on each way's own line**, which the network already carries, so a walk
originates no geometry of its own beyond the one short hop off the network onto a doorstep — and the step
across onto the lane beside while it gets past somebody (PER-28), which is the pavement's own lane too.

**PER-25b** `P4` **Somebody on foot does one thing at a time, and the thing is named** (`PersonAction`): walking
its route and the hop off the end of it, getting past somebody (PER-28), getting back onto its way (PER-25), an
officer walking to their post (SRV-11) — or standing, inside (PHY-7), down (PER-18) or under a hand (CTL-6). As a
car's is (CAR-15b), an action is entered and left in one place, and a walker walks only the ground its action
claimed (TER-4c.8).

**A body that is on no way of the network walks straight at the nearest point of one.** That is the whole
of the second case and it is the common one, not a corner of the rule — a doorway stands off the walk, a
body shoved off its line is standing on grass, a casualty put back on its feet is wherever the hospital
left it. The route is laid again from where the body has got to and its first leg is the straight back
onto the pavement. **The straight is planned like any walk** (PER-26), over whatever ways it crosses: off the
pavement altogether — in the carriageway, on a verge — the walker claims the lanes between it and its way before
it steps over them. **A straight is decided once**: where it goes is fixed as the walker sets off down it, and
the ground under it swept then — a straight back, a hop onto a doorstep, an officer's to their post — and only
a walker shoved off it, or sent somewhere else, decides it again. **A body between the two lanes of a pavement, or shoved across a corner, walks its route**:
what it steps over getting back onto its way is pavement its own body stands on, and where it goes is its plan's.

**Which of the two it is, is read off the way the route handed it and never searched for**: the body's
place is where it projects onto that way's own line, worked out once a tick, and a body further off that
line than the way has pavement either side of it is on none of it — the same bar the driving side calls a
line lost by.

**A route is a bound on work and never a plan.** It carries a fixed number of ways, and a route longer than
that is laid again from wherever the body has got to before what it plans runs off the end — which is the same
thing that happens to a body that has lost it, and needs no second mechanism. **Before, and not at the end**:
a plan reaches no further than the route does, so the last of one walked out first is walked blind, and a
crossing past its end is asked for from the kerb rather than from a stop short of it (PER-27).

**A leg that gets nowhere is given up.** There is nothing to arbitrate between, because a leg is the
whole of what a walker does (AGT-7): a body that has got no nearer the **end of the way it is walking**
for a stated time draws another destination, and how long that is, is data. It is one clock over both
agent kinds (`World.Routing.LegProgress`), and **what it is measured against has to shrink as the walk goes
well** — never the point the follower aims at, which travels a stride in front of the body.

## Reservation

**PER-26** `P4` **A walker is a body and a plan** ([reservations](../../../world/road/docs/claims.md)): the
ground it is standing on, and the ground in front of it down its walk. Both are laid from the body every
tick, and **the plan is answered exactly as a driver's is** — cut at the first body in front of it, weighed
against every other plan's main or secondary claim it meets on its own ways, and granted what comes back.

**The body is TER-4c.2 said of somebody on foot**, and it is nothing new: a body is on every way its
collider stands over whatever kind of body it is — the pavement's two lanes, the mitres of a corner it is
standing across, the lane it is standing in, the joins of a junction it is under and the ways of a bay — and
always on the way it is walking, over the stretch its own body takes of it. **Nothing takes it**, because its
holder is already there.

**The plan runs from the front of the body down the ways of its walk**, for as far as it would take to come
to rest from its pace, and the gap it keeps — **whether or not the body is on its way's own ground yet**,
since what it plans is where it is going. **The hop off the end of the route is planned too**: the ways the
atlas finds under the straight from the body towards where it is walking, so the ground round a door and the
road an ordered walk strikes out over are held like any other. A body walking no route plans nothing, there
being no way to plan it on. All of it is held at one rung, wherever it runs (PER-27).

**What comes back is a grant, and a walker spends it the way a driver does, on the tick it is given**: it aims
no further ahead than it was granted, and a walker granted nothing stands where it is. **So a walker queues** — behind the body in
front of it on its way, and behind a plan it gives way to, unless it can get past (PER-28) — and what it
walks into anyway is the solver's (`PHY-1`) rather than a rule's.

**What the two are for is that the rest of the town can see a walker, and a walker the rest of the town**: a
body on a lane cuts the road a driver was granted like anything else standing there, and the pavement a car
swings across at a corner is ground a walker is cut short of once the car can no longer stop short of it.

**PER-27** `P5` **A walker plans every way at one rung, one above the strongest movement** (TER-5g), and
**a crossing like any other way**: the paint in front of it on **the one stretch of the crossing it is
taking**, as far as its plan reaches — and through the marks every lane under the zebra, whole (TER-5c.3).
**The traffic gives way to somebody on a zebra because of that and nothing else** — no rung of the paint's
own — and the rung is below a light's hold, so a red holds the walker, and below ground a driver can no
longer stop short of, so nobody is waved in front of a car that could not have stopped for them.

**The zebra is held whole by both sides, and that is the whole of what makes it a crossing.** A metre of the
paint in a walker's plan holds every lane under the zebra from its near edge to its far one, and a car's plan
over any of that holds both of the zebra's walking lanes from kerb to kerb. So a walker is refused a zebra the
traffic holds where the paint begins, and waits at the kerb; one already on it walks on as far as it was
granted, the lanes under it being its body's already (PER-26).

**And on the stretch it is walking and not on the crossing's other one.** A zebra is two walking lanes over
one carriageway ([WLK-15](../../../world/foot/docs/requirements.md)), the second being the walk back the
other way, and a body's plan is on the one it takes.

**Wanting a crossing begins off the paint.** A zebra of this town runs kerb to kerb, so the first metre of
one is carriageway: a walker that asked for a crossing only once it was walking one would be asking from the
middle of the road. **The crossing a walk arrives at is wanted from a stop short of it** — the same distance a
walker plans over (PER-26), so a walker held there comes to rest at the kerb and no distance of this rule's own
is authored.

**A walker that waits is a walker on a leg going nowhere**, and the give-up clock runs through the wait like
any other standstill (PER-25). A crossing that never clears ends the leg and another destination is drawn,
rather than a body standing at a kerb for the rest of the run.

**Whether a driver can stop in time is still the driver's arithmetic and the solver's**, and a contact that
follows is PER-23's: what this rule keeps a walker out of is ground somebody else is already on or has been
given, and never the path of something coming.

**PER-28** `P5` **A walker gets past somebody standing on its way over the lane beside it**, as a pass
([TER-4c.6](../../../world/road/docs/claims.md)): asked for where its grant was ended by a body at rest that
is not making its own next movement, on a stretch of pavement with a lane running back beside it (WLK-8).

- **Somebody waiting at a kerb to cross is passed by a walker going on along the pavement**, which is the
  common case: their next way is the zebra and the passer's is the pavement past it. **That is why a pass runs
  down the route and not along one way** — they stand at the end of their way, where the pavement is parted
  for their zebra — and it runs round a corner the same way.
- **On the pavement and nowhere else**: never over ground the traffic drives, and never held on a zebra's
  paint, where somebody crossing towards the kerb would be held in the road.
- **As short as a walker makes it**: straight across onto the lane beside from where it stands, along it until
  its back is the gap it keeps past them, and straight back onto its own route there — a walker turns where it
  stands (PER-3), so no step of it is angled along the route.
- **A walker on a pass is off its way on purpose**, by as far as the lane beside stands off it, and has not
  lost it (PER-25).
- **Decided once, and committed to**: drawn and swept where the walker stands, then asked for every
  `SidestepAskEveryS` (`SimConfig.Person`) — and let go, to be decided afresh, once the one it passes has gone
  or moved, or once it has waited `SidestepPatienceS`.

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
they share. It is where this slice stands rather than a rule about people
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
actuates exactly one thing: a **desired velocity declared for this tick**, turned into one central impulse —
the whole of the change from what it declared the tick before, and whatever else the body is doing taken
back no faster than its foot friction affords (`WalkerFollower`, PER-3). Everything else is the solver's.

**Two grips.** On its feet, a sole pressed into the ground, bounding only what was done to the body; off its
feet, a body along it, bounding everything, there being nothing it does. A walker is off
its feet exactly while it is a casualty (`PER-23`), which is what makes the impulse of an impact visible
after the impact is over — a body sent down the road rather than stopped where it was hit. Neither is
scaled by the ground under it (TER-2). **The sliding grip is what sizes the band**: half a metre of it is what
being knocked over costs, so the two numbers are one decision.

> **The relation that is the requirement — the number is not:** a walker shoved at its own pace is carried
> **inside a fifth of its own body.** Whatever the walk speed is set to, the grip is whatever makes that
> true.

> **And the second grip is a share of the first, never a figure of its own.** This town's distances are
> real and its pace is five times a real one, so every acceleration in the model carries a factor of
> twenty-five that no figure states. A sliding grip authored as though the pace were real is twenty-five
> times too cheap, and the band it sizes lands underneath walking pace.

**One thing this model gets wrong, stated rather than fixed**: a walker's feet resist a car pushing them
at `mass × grip`, which is about half a car's drive, so a car leaning on a pedestrian *below* `PER-23`'s
band is pushing something that braces rather than something that gives — and, still on its feet, it can be
shoved several metres by a car that never knocked it over. The fix needs a contact count kept per walker,
and a count that fails to come back down is a walker who slides for the rest of the run.
