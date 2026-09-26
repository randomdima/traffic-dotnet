# Decision log — the walker

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- **PER-7.1**: "do not intentionally collide" said as what a walker does — aims at nothing but its route and
  does not step onto a crossing the traffic has — since it steers round nothing (PER-25).
- **PER-26**: the statement is at the rung ordinary traffic states at, not the weakest hold there is, and runs
  as far as the walker would take to come to rest rather than to where it is aiming.

## 2026-09-21 — a walker on a zebra wants the far kerb of it

PER-27, and a rung of its own to hold it at. What a body on the paint said before was what any body says —
the metre it stands on — so the town could not tell somebody crossing from somebody standing in the road,
and a driver's statement of the same metres was the stronger of the two.

**To the far kerb, because half a crossing is not what anybody wants.** The reservation runs from the body
to the end of the paint, plus the band of each lane that paint is laid across. Behind the body it is not
laid at all: paint already crossed is paint the traffic may have (TER-5c.1), and a hold is one stretch
(TER-5c.2), so a reservation laid from the crossing's first metre would be cut at the walker's own near
edge and reserve the half already walked.

**On the one stretch being walked, because the other one is the walk back.** A zebra is two walking lanes
over one carriageway (WLK-15) and it used to be reserved across both. That was not a wider hold on the
same ground — it was the cut above undone, the twin lane running the other way up, so a body half way over
re-reserved from that lane's far end exactly the half it had just finished with. Which of the two a walker
is taking is now read off the route where its crossing is (`PersonFleet.OnCrossingWay`), with the corner
onto the paint carrying the lane it leads to, so a body still at the kerb names the lane it is about to
step onto rather than being on none of them. **The lanes beneath are still the crossing's own** and not
that stretch's: the carriageway is what is being crossed, and the direction the paint is walked in says
nothing about which tarmac a body will stand on. Nothing is left asking a crossing which stretches it is
made of, so the index that answered that went with it.

**p7, so the traffic passes.** It is below everything a movement is granted, so a car drives over a
reservation and takes off it whatever it was granted; it is above a statement, so a car merely saying it
means to use those metres does not. `Binds` names it apart from both — **a reservation refuses nobody at
all**, which is one line and the whole of what would ever be changed to make the traffic wait for one. Put
in the rank comparison instead it would have refused the weakest movement a box admits and nothing else,
which is a rule about turning cars arrived at by accident.

**What waits is the walker, and it waits on an answer.** The reservation was laid and read by nobody, so a
body walked onto a zebra a car was standing on and found out about it in the solver. It is now the one claim
on this side of the town that is answered: the same metres are asked about the traffic
(`ClaimsAsked.Traffic`), and a walker still off the paint holds where it stands until they are nobody's.
**The question and not the lay's own return**, because the lay is cut by every stronger claim and a walker
refused by another walker's body would be a queue on the pavement — which PER-26 put down on purpose.

**And it is laid last of everything in the tick.** A reservation refuses nobody, so nothing above it in the
rebuild reads one and laying it there costs the traffic nothing; what it buys is that the answer is about
the settled town rather than about whoever happened to have been written first. Laid with the walkers'
other two, it was answered against a town with no moving car in it at all.

**Wanting a crossing had to begin off the paint.** `PersonFleet.OnCrossing` was read off the way being
walked, and the corner leading onto one was supposed to be the kerb tick that gave a walker somewhere to be
told no. **There is no such tick**: a zebra is laid from the kerb ends and runs kerb to kerb — the first
band of lane under one begins at its metre 0 — and the pavement meets it at the very point it sets off
from, so the mitre is skipped (`WalkingNetwork.JoinArcs`). **Not one of the 488 crossings in the suite's
city has a corner of any length onto it.** So the crossing a walk *arrives* at is wanted from a stop short
of the kerb, measured with the figure the walker already states ground over — which is where a body held
there comes to rest, and is no number of this rule's own.

**No walk chooses a zebra yet**, so what fires any of it in a town today is a walk put on the paint by hand
([the known gaps](../../../../docs/index.md#known-gaps)) — which is what the cases that ask it do. The
rung's own arithmetic is the unit tier's; that a body walking a crossing reserves the paint ahead of it and
the lanes beneath, and that a body arriving at one a car is standing on stops at the kerb, are asked of a
town.

## 2026-09-20 — the walk drawn, the door kept, and the clock that measured a carrot

Three faults in the route-chain work (the entry below), found by asking why the line drawn for a walker
pointed back the way it had come.

**What is drawn for a walker is the chain it holds** (`RouteWays`), and the interface plans only past the
end of it. A fresh search from under the body every frame was regularly the other of the two ways the
pavement offers (WLK-8) — a U-turn drawn across the pavement while the body walked on.

**A chain that runs out of room leaves the goal where it is.** Moved onto the chain's end, the door was lost:
every route on Odesa fills 64 ways, so a walker reached the end of its first chain, was found standing on the
goal it carried, and walked into a building it was nowhere near — `--bench trips` read 84 entered a minute
where 12 walks had been walked. The chain end is what the body aims at while `RouteRunsOut`, and the leg is
laid again from there.

**The give-up clock is measured against what is left of the way**, the ground back onto the line counted as
ground still to walk, **and to a body's width rather than the millimetre**, the mark being a record low
that a body leaned on rocks about. Measured against the point the follower aims at, it ran on a walker at
full pace and was reset by the jitter of one going nowhere.

**And the clock runs through a re-lay.** Restarted there, a body whose straight back to the pavement ran
through a building leaned on the wall and re-laid for the rest of the run, never given up and counted by
nothing: 91 of Odesa's 600 at the end of five minutes, the longest for 263 s. So PER-8's own answer came
back for the case the walk cannot walk — a body still off the network when its patience runs out is **set
down on the nearest point of it**, and counted. Odesa then ends five minutes with none standing still and
nobody holding a spot longer than 53 s; walks given up went 933 → 1319, the frozen now counted, and arrivals
stayed at 65 — **the walking on Odesa completes about a sixth of what it draws**, which is the number to work
on.

## 2026-09-20 — a walker holds a route, the way a driver does, and over the same code

The walking side searched the same graph a driver does and was handed the same run-links — then flattened
the whole route into 64 stationed points under the body. A driver keeps the lane chain it is travelling
(`CarFleet.RouteLanes`) and a short line over the next few lanes; a walker kept only the line, and its line
was the entire route.

**So both expand the search's links through one piece of code** (`RouteChain`, `IRouteJoins`), the two
networks answering three questions differently — where a link is joined from the way behind, whether the
way behind reaches a piece, and what is travelled *between* two pieces: nothing on a road, whose junction
ground is assembled into the driven line, and the mitre on a pavement, a walker being held on the network's
own corner. **A walker is held on each way's own arc**, so the sag tolerance, the chord-stepping and the
arithmetic that recovered a body's place by stepping back from its aim went with the points; `OnWay` and
`OnWayM` are written where the walk is worked out, instead of being two derivations a tick apart.

**64 ways still runs out on every route on Odesa**, a corner onto the pavement costing a slot at every kerb;
what it buys is a re-plan at the end of a chain rather than every few hundred metres. It also stopped a
search that came back empty leaving `Walking` true, which sent a body in a straight line for a door across
town with the goal getting nearer every tick, so the clock never ran.

**What is drawn is stationed for the picture and by nobody else** (`WalkedLine`), at the tolerance a picture
needs rather than under every body at the tolerance the town had to be safe at.

**Which way a body is "on" is two answers, and both are right**: the route says which way it is walking,
the ground walk which ways its box is over, and where two lines run within a body's width they differ.
`AWalkerOutOfDoorsHoldsTheGroundItStandsOn` had asserted they agreed; it now asks that the claim is there.

## 2026-09-20 — the walking is one line and two claims, and everything else was put down

The walking side had grown a second traffic model: an ask and a grant along the pavement's ways, a body
picked out of the grant to be stepped round, a kerb with a signal, a patience and an escape from the
patience, and two rules for a walker on a map with nothing on it — eight figures of its own and four hundred
lines of claims arithmetic, while *a walker gets from here to there* was the part that kept failing. **It is
now two sentences**: follow the line the network laid, and walk straight at the network when not on it
(PER-25); hold the ground you are on and state the ground you are walking at (PER-26). The pathfinding was
never the problem and is untouched.

**The kerb went with it** — `KerbPatienceS`, `RedWaitSetbackM`: no gap judged, no patience spent and no
signal read. **And so did the band on the paint** (`OnThePaint`): a walker on a zebra wrote itself onto the
lane's way as well as its own, one patch of ground recorded twice because a reader was asking the wrong row.
**A walker on the paint is a body standing on a lane**, held off by the grant its claim cuts like a wreck or
a parked car (TER-4c.2, SIM-7), and `AnybodyCrossing`, `AnybodyWaitingFor` and the rest went with the band.

**On paper that gives up anticipation; measured, it gave up nothing**: not one walk in the suite's city
carried a crossing, so no band was ever laid, and `--bench trips` was unchanged to the digit on Odesa and
River. A walker who steps out in front of a car that cannot stop is knocked over (PER-23) where the band
would have stopped the car sooner — the thing to watch when walks use crossings and the casualty count
climbs. **What is kept is the one rule that was nobody's second copy**: a car may not come to rest on the
paint, or a queue stands across a zebra with a body the solver will not move.

**A walker no longer queues**, PER-3 leaving it nothing to spend a distance on, so two walkers wanting one
piece of pavement meet in the solver: what that costs is a shove, and what it saved is every rule that got a
body past another body. **Its give-up patience is its own** (`GivesUpAfterS`): a driver's three-second
obstruction wait read an ordinary shove at a doorway as a trip that could not be finished — over five
minutes of Odesa 505 walks given up against 130 arrived, where the walker's own figure gives up 106.

## 2026-09-07 — the walker's action set is the code's, and `PER-2` is retired

`PER-2` listed turning, walking, idling and entering a container — the four methods, restated where they
could drift from the type that has them. What each one costs and is bounded by is `PER-3` and the
containment rules; the list itself was cited by nothing.
