# Decision log — the walker

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-20 — the walk drawn, the door kept, and the clock that measured a carrot

Three faults in the route-chain work above, found by looking at a walker in the window and asking why the
line drawn for it pointed back the way it had come.

**What was drawn for a walker was a fresh search from under its body, not the chain it was holding.** A
car's picture is the line it is driving plus the lanes its route has left; a walker's was planned again
every frame from where the body then stood. The pavement offers a walk **both ways** along the stretch a
body is on (WLK-8), so the second opinion was regularly the other one — a U-turn drawn across the pavement
while the body walked on. It is now stationed off `RouteWays` and the interface plans only past the end of
it, from where the chain stops, which is where the body will plan from itself.

**A chain that ran out of room moved the goal onto its own end, and the door was lost.** Every route on
Odesa fills 64 ways, so this was not the corner it looked like: a walker reached the end of its first
chain, the arrival check found it standing on the goal it was carrying, and it walked into a building it
was nowhere near. `--bench trips` read 84 entered a minute where 12 walks had been walked. The goal now
stands, the chain end is what the body aims at while `RouteRunsOut`, and the leg is laid again from there —
which is what the rule always said and what the dead branch in `StandingStill` was written for.

**And the give-up clock was measured against the point the follower aims at**, which stands a stride in
front of the body and travels with it. The distance to it is that stride whether a walk is going well or
not at all, so the clock ran on a walker at full pace and was reset by the jitter of one going nowhere.
It is measured against what is left of the way now, with the ground back onto the line counted as ground
still to be walked — and **to a body's width rather than to the millimetre**, because the mark is a record
low and a body being leaned on rocks where it stands.

**The freeze under all of it: a lost line restarted that clock.** A body whose straight back to the
pavement ran through a building leaned on the wall, re-laid, restarted and re-laid for the rest of the
run — walking, never moving, never given up, and counted by nothing. Ninety-one of Odesa's six hundred
were in that state at the end of five minutes, the longest for 263 s of it. The clock runs through a
re-lay now, and PER-8's own answer came back for the case the walk cannot walk: a body still off the
network when its patience runs out is **set down on the nearest point of it**, counted, and printed by the
probes and in every town's own readings. Odesa ends five minutes with **none** standing still, against
ninety-one, and the longest anybody holds a spot is 53 s.

**What that cost is honesty about the rest.** Walks given up went from 933 to 1319 over five minutes,
because ninety-one walkers who used to be frozen out of the count are now given up and recovered; walks
arrived stayed at 65. **The 84 a minute this log used to quote was the fake arrival**, and the walking on
a town the size of Odesa completes about a sixth of what it draws. That is the number to work on, and it
is now the number the instruments print.

## 2026-09-20 — a walker holds a route, the way a driver does, and over the same code

The walking side searched the same graph a driver does, was handed the same run-links — and then threw
them away, flattening the whole route into 64 stationed points under the body. A driver keeps two tiers:
the lane chain it is travelling (`CarFleet.RouteLanes`) and a short line assembled over the next few of
them. A walker kept only the second, and its "line" was the entire route.

**It was the same tier by every name and not by its shape.** `PersonFleet.WalkedRunsOut`'s own doc-comment
read *"the car's `RouteRunsOut` for walkers"* while meaning something else — the car's says the route did
not fit in 64 lanes, the walker's said the *line* did not fit in 64 points. The routing slice already
stated that both agent kinds use the same two tiers and the same search; the walker met half of it.

**Now both expand the search's links through one piece of code** (`RouteChain`, `IRouteJoins`): the
pieces of each link in order, from where the body joins the first to where the destination stands on the
last. What the two networks answer differently is three questions — where a link is joined from the way
behind, whether the way behind reaches a piece at all, and what is travelled *between* two pieces. A road
answers the last with nothing, because a junction's ground is assembled into the driven line; a pavement
answers it with the mitre, because a walker is held on the network's own corner.

**A walker is then held on each way's own arc** rather than on chords between stationed points. What went
with the points: the sag tolerance and the chord-stepping that existed to keep a polyline out of the
carriageway, and the arithmetic that recovered a body's place on the network by stepping back from the
point it was walking at. `OnWay` and `OnWayM` are written where the walk is worked out and read by
everything else, instead of being two derivations a tick apart.

**64 ways carries a walk much further than 64 points did, and on a town the size of Odesa it still runs
out on every route.** A way is a stretch of the fine graph or a corner onto one, so the chain spends a
slot at every kerb; what it buys is that a walk is re-planned at the end of a chain rather than every few
hundred metres. It is also the fix for two walkers set off at a goal with no route at all: a search that
came back empty left `Walking` true, and
the body struck out in a straight line for a door across town over whatever lay between, with the goal
getting nearer every tick so the give-up clock never ran.

**What is drawn is stationed for the picture and by nobody else** (`WalkedLine`). A screen draws straights
and the ground is arcs; that conversion now happens in the interface, at the tolerance a picture needs,
rather than under every body in the town at a tolerance the town had to be safe at.

**Which of two ways a body is "on" stopped being one answer, and a test stopped asserting it was.** The
route says which way a body is walking and the ground walk says which ways its box is over. Where two of
the town's lines run within a body's width — courses meeting, a crossing's mouth — the two name different
ways for one patch of ground and both are right. `AWalkerOutOfDoorsHoldsTheGroundItStandsOn` asserted they
agreed, which was asserting a coincidence; it now asks that the claim is there at all, and the case that
knows the exact metre is the one that stands a body on a lane's centreline itself.

## 2026-09-20 — the walking is one line and two claims, and everything else was put down

The walking side had grown a second traffic model: an ask and a grant along the pavement's ways, a
permission read off the grant, a body picked out of it to be stepped round, an offset all three halves of
the step had to agree on, a kerb with a signal and a patience and an escape from the patience, and two
rules for a walker on a map with nothing on it. Eight of its own figures, four hundred lines of claims
arithmetic and a decision log whose every entry was a correction to one of those — and the thing it was
all in aid of, *a walker gets from here to there*, was the part that kept failing.

It is now the two sentences it should have been. **PER-25**: follow the line the network laid, and walk
straight at the network when you are not on it. **PER-26**: hold the ground you are on at p0, state the
ground you are walking at at p9, and read nothing back. The pathfinding was never the problem and is
untouched — the pavement is contracted once when the town is stood up and a walk is a search over it.

**The kerb went with it** — `KerbPatienceS`, `RedWaitSetbackM` and the standstill test: no gap judged, no
patience spent and no signal read.

**And then the crossing went too, which is the second half of the same cut.** What replaced the kerb was a
band: a walker on a zebra wrote itself onto the *lane's* way as well as its own, at a rank of its own
(`OnThePaint`), so a driver reading its lane would see somebody on the paint. That is one patch of ground
recorded twice — the walker already holds the crossing way it is standing on and states the crossing ways
its line runs onto — and the second copy existed only because the reader was asking the wrong row. It is
gone. **A walker on the paint is a body standing on a lane** and holds what its own box covers of it, like
a wreck, a parked car or somebody knocked down; the traffic is held off it by the grant that claim cuts,
which is the mechanism that was already there (TER-4c.2, SIM-7). With it went `RightOfWay.OnThePaint`,
`AnybodyCrossing`, `AnybodyWaitingFor`, `ClaimsAsked.Refused` and `Person.RoadClaimMargin`, none of which
anything laid or asked once the band was not written.

**On paper it gives up anticipation; measured, it gives up nothing, because nothing ever reached it.** The
band held the whole depth of the paint and a stride either side, so traffic stopped for a body *anywhere*
along a zebra; now a car is held off the metre a body actually covers, from the moment it covers it. But
**not one walk in the suite's city carries a crossing point at all** — no walker there routes over a zebra,
so no band was ever laid and no driver ever read one. `--bench trips` was unchanged to the digit on Odesa
and River across the cut, which is what that looks like from outside. **The mechanism was deleted for being
wrong, and the measurement says it was also dead.**

**So the anticipation is a thing to reckon with when walks start using crossings and not before**
([the known gaps](../../../../docs/index.md#known-gaps) names the absence). A walker who steps out in front
of a car that cannot stop is knocked over (PER-23); the band would have stopped the car sooner. That is the
town being a town rather than a rule, and it is the thing to watch when the casualty count climbs.

**What is kept is the one rule that was nobody's second copy**: a car may not come to rest on the paint.
Nothing else says it, and without it a queue stands across a zebra and the walk is blocked by a body the
solver will not move.

**A walker no longer queues, and that is the change to live with.** A grant is a distance and PER-3 leaves
a walker nothing to spend one on, so two walkers wanting one piece of pavement now meet in the solver
rather than in the claims. What that costs is a shove; what it saved is every rule that existed to get a
body past another body.

**The give-up clock had to stop being the driver's.** Nothing freezes it now, so a three-second obstruction
wait — a figure sized for a car that has a ladder to climb — read an ordinary shove at a doorway as a trip
that could not be finished: over five minutes of Odesa, 505 walks given up against 130 arrived. At the
walker's own `GivesUpAfterS` it is 106.

## 2026-09-07 — the walker's action set is the code's, and `PER-2` is retired

`PER-2` listed turning, walking, idling and entering a container — the four methods, restated where they
could drift from the type that has them. What each one costs and is bounded by is `PER-3` and the
containment rules; the list itself was cited by nothing.
