# Decision log — the walker

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-29 — a walker gets past somebody standing on its way

**The owner asked for overtaking for walkers on the same four rules as cars** (PER-28, TER-4c.6). Read as
"never pass somebody making your own next movement", the case it opens is the one a watcher sees most:
somebody waiting at a kerb for their zebra, and a walker going on along the pavement stuck behind them.

**Measured down the route, not along one way**: the person waiting stands at the end of their way, where the
pavement is parted for the zebra, so a pass that stayed on the way it began on could never get round them. The
pass is anchored at the route slot and metre it was asked from (`Sidestep`), which carries on across a joint
and round a corner.

**Straight across, along, and straight back**: the owner asked for the shortest pass, and a walker turns where
it stands. Aimed a stride ahead at the lane beside, a walker had closed on it along a curve, over ground between
the two lanes its pass did not hold — the claim began a lane over from where the walker was. Now each leg is
walked straight at its end, and the ground is the walker's disc swept down all three from where it stands: on a
straight, 2.0 m across, 3.9 m along and 2.0 m back in 1.3 s. **Which leg it is on is read off where it stands**,
to within its own radius across: to within a tick's walk, a body the solver shoved off the lane beside was sent
back across it.

**Held on the pavement and nowhere else**: laid over the kerb end of a zebra's paint, a walker's pass held
somebody crossing towards that kerb out in the road. A walker on a pass is off its way by as far as the lane
beside stands, and is not treated as having lost it (PER-25).

## 2026-09-28 — a walker has no acceleration of its own, and arrives where it aims

**Walkers circled points.** The follower asked full pace at whatever the aim was, along a heading turning
22.5° a tick — a pursuer that cannot reach anything inside its 0.28 m turning circle. Any aim that stopped
moving ahead of the body was overshot and orbited: on Odesa over a minute, 32 of 600 walkers.

**What pinned the aim was a handover asked in float.** A way is walked out at `OnWayM >= end`, and on a way of
several arcs the projection's most is the sum of the arcs in float — an ulp (1e-7 m) short of the way's
stored length. Every one of the 32 stood on the end of such a way aiming at it and was never handed the next.
A way is now walked out within a tick's walk of its end (`HasWalkedOut`).

**The owner asked for the simplest walk: stand, turn on the spot, walk straight at the aim at pace, no
acceleration for its own move — and neither a walker that braces against a car nor one a car cannot throw**
(PER-3). So what it declares it has on the next tick, and only what a contact did to it is taken back at the
foot grip, which now sizes that and not the walker's own starts and stops; the last of a walk, short of a
tick's, is stepped onto whatever way it faces. That needed the velocity it declared kept per walker
(`PersonFleet.DeclaredMps`). Its aim is where the middle of its body gets to — its place plus the grant, where
it had been the grant and a radius, harmless only while the aim was a direction — and it plans a tick's walk
and the gap, that being what it takes to stop. Over the same minute: no walker circles, walks given up
44 → 15, touches 8 → 3; 109 of 109 exam cards pass.

## 2026-09-28 — a walker plans at one rung, one above the traffic

**The owner asked for a walker's plan to outrank a car's by one rung, and for the zebra to follow from
that** (PER-27, TER-5g). A walker had held the pavement at ordinary traffic's rung and the paint — with the
pavement leading to it — at a rung of the paint's own, picked out by asking whether each way was a zebra. It
now holds every way at `ClaimPriority.Afoot`, one above the strongest movement and below a light, and nothing
in the plan asks what the ground is. The traffic still gives way on the paint, because the zebra's marks
(TER-5c.3) bring a walker's rung to every lane under it: 109 of 109 exam cards pass, the walkers' included.

**What else follows is the same rule elsewhere, and was taken with it.** A car's turn over the pavement at a
corner, and a car a walker's hop strikes out across, give way to the walker where the car can still stop;
where it cannot, the car keeps the ground (TER-5e), as it did before.

## 2026-09-28 — every walker on a walk plans it, the hop included

**Two walkers planned nothing and walked on an unbounded grant** (PER-26): one not yet on its way's own
ground — regularly the first stretch of a leg, crossing to the far lane of a pavement — and one on the hop off
the end of its route, onto a door or, under an order, straight across a carriageway. Both plan now: the first
down the way it is walking, the second over the ways the atlas finds under the straight in front of it, and
the hop is aimed no further than it was granted.

**A light holding the crossing is a wait that spends no clock** (TLT-2a), as it is for a driver: a walker at a
red kerb gave up its trip after twenty seconds.

## 2026-09-28 — a walker aims on this tick's grant, and is said to be at a zebra by its hold

**The aim is read after the grant** (`AimTheWalker`): it was set while the chain was walked, before the plans
were laid, so every walker stepped on the grant the tick before had left it — a tick of walking into ground
refused since, where a driver brakes on the grant it was given. **And which zebra a walker is at is read off
its route and its hold** (`PersonFleet.OnCrossing`): the paint its route is on, or the paint it was refused
on while it stands. It had a look-ahead of its own for the crossing a walk was arriving at — a second reading
of how far the plan reaches.

## 2026-09-26 — a walker plans its walk and queues, on a driver's terms

The reservations were reworked into a body and a plan for everybody
([world/road](../../../world/road/docs/decision-log.md)), and a walker is laid on exactly the terms a
driver is (PER-26): its body on every way its disc is over and always on the way it walks, and a plan from
the front of the body down its walk, answered — cut at the first body in front, weighed against every plan it
meets — and granted. **What came back was a grant, so a walker spends one**: it aims no further than it was
given and stands where it is when given nothing. That undoes the entry that put queueing down (a walker's
statement read by nothing, two walkers meeting in the solver): the reservations are now the one way two
agents meet, and a walker read by nobody is one they did not reach.

**A walk that ran out on a corner of no length walked at the town's origin.** Two stretches that meet at a
point are joined by a mitre with no line, and a chain that ran out of room on one (`RouteRunsOut`) sampled
its end off that empty line — the origin — and walked there, straight across whatever carriageway lay
between, until something re-laid it. The chain now ends at the stretch before the corner
(`WalkingNetwork.EndOfTheWalk`), and so does the goal moved onto a chain. On Odesa over six seeds of a minute
it was most of the knockdowns: 25 → 2, and walks given up 561 → 316.

**The walking router prices its own links.** It had been reading the driving side's table of route costs by
link id, so a blocked road raised the price of whichever walk shared its number.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- **PER-7.1**: "do not intentionally collide" said as what a walker does — aims at nothing but its route and
  does not step onto a crossing the traffic has — since it steers round nothing (PER-25).

## 2026-09-21 — a walker on a zebra takes the stretch it is walking, and wants it from the kerb

**On the one stretch being walked, because the other one is the walk back.** A zebra is two walking lanes
over one carriageway (WLK-15) and it used to be held across both: a body half way over held, from the twin
lane's far end, exactly the half it had just finished with.

**Wanting a crossing had to begin off the paint.** A zebra is laid from the kerb ends and runs kerb to kerb,
and the pavement meets it at the very point it sets off from, so the mitre is skipped
(`WalkingNetwork.JoinArcs`) — **not one of the 488 crossings in the suite's city has a corner of any length
onto it**, and there is no kerb tick in which a walker is on the pavement and asking. So the crossing a walk
*arrives* at is wanted from a stop short of the kerb, measured with the figure the walker already plans over
— which is where a body held there comes to rest, and is no number of this rule's own.

**No walk chooses a zebra yet** ([the known gaps](../../../../docs/index.md#known-gaps)), so what fires any of
it in a town today is a walk put on the paint by hand — which is what the cases that ask it do.

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

## 2026-09-20 — the walking is one line, and the kerb's second traffic model was put down

The walking side had grown a second traffic model: a body picked out of a grant to be stepped round, a kerb
with a signal, a patience and an escape from the patience, and two rules for a walker on a map with nothing
on it — eight figures of its own, while *a walker gets from here to there* was the part that kept failing.
**It is one sentence now**: follow the line the network laid, and walk straight at the network when not on it
(PER-25). The pathfinding was never the problem and is untouched. **The kerb went with it** —
`KerbPatienceS`, `RedWaitSetbackM`: no gap judged, no patience spent and no signal read. **What is kept is
the one rule that was nobody's second copy**: a car may not come to rest on the paint, or a queue stands
across a zebra with a body the solver will not move.

**A walker's give-up patience is its own** (`GivesUpAfterS`): a driver's three-second obstruction wait read
an ordinary shove at a doorway as a trip that could not be finished — over five minutes of Odesa 505 walks
given up against 130 arrived, where the walker's own figure gives up 106.

## 2026-09-07 — the walker's action set is the code's, and `PER-2` is retired

`PER-2` listed turning, walking, idling and entering a container — the four methods, restated where they
could drift from the type that has them. What each one costs and is bounded by is `PER-3` and the
containment rules; the list itself was cited by nothing.
