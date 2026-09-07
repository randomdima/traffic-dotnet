# The driving manoeuvre catalogue — decision log

## 2026-09-04 — a swerve takes the whole run before it drives any of it

`ManeuverDesk.Look` answers for the instant it is made and reaches only what is *held*, so an oncoming car
whose committed road stopped forty metres short was no answer at all and what it had *stated* (`TER-5g`)
was never asked. The oncoming lane is the one piece of the town where the pair cannot be arbitrated —
nothing is driven between a carriageway's two lanes (`TER-5f`), so both stand across the centreline until a
watchdog gives one up. The ground is taken first and the shape refused without it: two stretches, both or
neither, the crossed lane asked about everything held **and everything stated**. It is a longer question
than the walk's rather than a second copy (`SIM-7`), and the crossed stretch is projected from the shape's
own ends rather than mirrored, since two lanes of one carriageway are not the same length on a bend.

## 2026-09-04 — a manoeuvre a planned entry merely offered is not a rung of the ladder

A swerve the ground had no room for escalated the ladder through `GoTo`, and `Escalate` zeroed `BlockedS`
on the way past. The blocked clock is the car's patience, spent to earn the swerve at the obstruction wait
and the watchdog at the fuse, so zeroing it meant the clock never got past the first: with wrecks staged in
front of Odesa's traffic, the longest any car was ever blocked was 3.1 s against a thirty-second fuse. A
discretionary entry now leaves the car doing what it was doing when its `Sa` refuses — no rung, no reset,
no trace. Longest blocked went 3.1 s → 11.9 s and the ticks reaching the desk's geometry 18 → 537.

## 2026-09-04 — the swerve can only be entered from rest, and from rest its own shape is undrivable

352 of the 353 shapes drawn were refused by the terrain, and nothing else refused anything. `WorthGoingRound`
asks for `AtRest`, and a car at rest has no speed for `CarCorneringRadiusM`, so the radius falls to its
floor: 3.8 m radius, 7.4 m pass straight. A 3.6 m shift at that radius over that distance yaws the body
hard, and a carriageway leaves 1.7 m either side of a centreline, so nose and tail leave the road mid-S.
The floor was written for an edge case that is the only case. Fixing it means drawing for the speed the car
will reach, or lengthening the pass straight — either is a change to what the manoeuvre *is*.

## 2026-08-29 — the two ways the watchdog was never reached

Ninety-six of a hundred and seventy-six drivers stood at the end of five minutes of Odesa, every one
reading a clock well short of its fuse: the ladder was never asked. A light was rewinding the clock rather
than holding it — a red is there again every cycle, so a car within ninety metres of a signal never
accumulated its thirty seconds however many greens went by. And an entry that took the next step and got
itself back read as an entry getting on with something, since `Enter` returns early on `doing == next`; one
car stood 210 seconds with a 210-second clock beside it. The fuse is the car's and not the entry's. Drivers
left standing went 96 → 58 over five minutes and 127 → 28 over ten.

## 2026-08-29 — `P-12` is retired: slowing at a crossing is what the claim already does

The entry set no limits, drove no line, laid no claim and had no bound. The stop short of a body on the
paint is a term of the speed profile taken every tick, read off the grant (`TER-4c.1`, `TER-5e`). Naming it
cost something: while it was in charge the car could not be handed to `P-8`, `P-6` or `E-4`. Two things it
had been hiding came out with it — `P-6` could not reach `P-18`, so a car ordered past a bar it was
creeping up to drove off round the block; and nothing but `P-12` was keeping a swerve off a crossing, since
a walker two lanes over leaves `E-4`'s shape a clear run. That refusal is `DriveScene.ClearOfThePaint` now,
and it is the first gate rather than a second.

## 2026-08-28 — a following time is kept from what is being followed

`S-2a`'s following time was subtracted from every grant whatever had cut it, so a car whose road ran out at
a wreck held a second of travel clear of it *as well as* the margin already taken — a dozen metres of
street shut at town speed. What cut a grant is now carried with it (`CarFleet.GrantCutBy`), because the
walk worked it out to make the cut. The following time is kept where the cut was a queue and nowhere else.

## 2026-08-27 — the turn-around in a junction is gone, and coming back the other way is a manoeuvre

`P-11` swept the box on a 1.5 m semicircle no car in the fleet can hold and the router had priced out of
reach since the day it was laid. The *movement* is gone from the road as well as the entry from the list
(`TER-5f`). What replaces it is what a driver does: park in a bay and unpark the other way (`GEN-4l`), or
work round on the spot at a dead end (`P-19`). The price was set at twenty car lengths first and was wrong
— routes preferred a turn to a loop and Odesa gave up four hundred places in six minutes against eighteen.
It has to read as last resort, which is a hundred.

## 2026-08-27 — overtaking belongs to a road segment, and a junction is not one

A junction has no centreline, and what licenses the wrong side of the road is `CAR-6.2b`. Those movements
are arbitrated on the town's table (`TER-5c`), which says where a crossing car will be only while it
follows the join it claimed. And the swerve's claim cannot be laid there at all: inside a box `LaneOf` is
−1, so the claim was silently never made and the traffic behind read the ground as empty. The rule is in
two halves because the two facts are known at different moments — whether the car is *at* a junction sits
with "is this wanted" so `P-4` and `E-4` read it from one place, and whether the pass *fits* sits in `Sa`.
The bar is `CarJunctionReserveM`, the one `P-4` hands the junction over at, because a figure of this
entry's own would be a second answer to "is this car at a junction yet" (SIM-7).

## 2026-08-25 — `E-1` is retired: yielding is ground, and waiting at a place is `P-6`

The yield drove no line, laid no claim and imposed nothing — the wait was already what the speed profile
was doing. Once a right of way is a rank carried by the town's claims (`TER-5e`), the reason a car is
waiting is stated where the ground is. What it bought was rung 0, and that is `P-6`: a car giving way has
been stopped short of a place, which is what that entry is about, and it carries its own watchdog.

## 2026-08-23 — `E-3` backs away from obstructions and from being one, and never from traffic

The `Sa` asked how *near* the thing in front was and not what it was, which is the whole of a queue at a
red — so a jam whose real answer was another second of patience got a reversing car into road the traffic
behind was entitled to. The door reads `NobodyEntitledIsInTheWay` now. `Waiting` survives only where the
car is itself across a lane or in a box, because the ground it reverses out of is then the ground it is
blocking. The boundary door was kept because refusing a car in a box the one recovery that gets it out is
not patience: measured, dropping it took abandonments 14 → 24 and places given up 28 → 33.

## 2026-08-23 — a queue keeps a following time, and `E-2` fires above the plan instead of under it

Three arithmetic faults made a platoon move in lurches. The equilibrium gap had nothing in it: solving at
one speed gives `gap = standstill + v·τ` with the braking figure cancelling out, which at a tenth of a
second is 3.5 m at 15 m/s — safe against a leader braking at exactly the planned rate and nothing more. The
lead is a following time now (`Driving.FollowingHeadwayS`), which is the first thing that has ever made
`Reserved` and `Headway` different terms. `E-2` compared against `GripMargin` 13.11 while every planned
stop is at `BrakingMargin` 17.79, so the reflex fired on ordinary braking, took the pedal to 27, overshot
and handed back to full throttle — that loop was the lurching. And the pedal was a relay saturating on
anything past a fifth of a metre a second; it travels at a bounded rate now, and the profile's lead gained
the half of that travel it costs. Separately, a grant is looked for as far as the gap it keeps, since what
a car claims is shorter than a second of headway at any town speed. Odesa's `E-2` went 101 → 21 and legs
begun in a minute 47 → 115.

## 2026-08-23 — a walker in the road is an agent and not a rule

The ground a swerve takes is already asked — `E-4` walks its template and asks whose every point under it
is, and a body on a carriageway has claimed it with a margin (`PER-15`). The rule was a second thing
refusing a movement the first already refuses (`SIM-7`). Paint is where a walker's priority lives; a body
on bare carriageway is in the way of a road it was never entitled to.

## 2026-08-23 — `E-4` becomes an overtake, and stops being rationed

Four things were in the way and none was the hard part anybody expected. The swerve was drawn at the
steering lock, so the corner term held the car to 6 m/s and it could not get past a body walking at 6.6;
the radius is the caller's now. The shape was drawn flat on a lap that bends, so fifty metres of chord on a
forty-metre radius is ten metres off the road; every piece carries the road's own curvature underneath the
S. It moved over by the car's own width, which leaves the car spanning both halves of its lane, since a
claim never says where across the road a thing stands — it is a lane over now. And the pass straight was
the static gap, where against something moving the ground needed is `clear · v ⁄ (v − u)`. One reading
decides it and both entries read it (`DriveScene.WorthGoingRound`), with two doors and two clocks: a car
behind something stopped spends the blocked clock, one behind something slow never stops and spends
`HeldBackS`. The attempt budget is deleted rather than repaired — "two per leg" gave a car with no
destination two swerves for a life, and ground covered since the last is worse, because a car that cannot
move cannot earn any back.

## 2026-08-23 — a car shoved across the centreline takes the lane it is pointing down

Refusing the nearest lane where it ran the wrong way left the one case the recovery exists for with no
answer: a body shoved over the centreline stands nearest the *oncoming* line, still pointing the way it
was. Standing on ground a car may drive on, `E-8` had nothing to say either, and the ladder took it down to
giving the journey up. The nearest lane's reverse is looked at rather than the search abandoned; the
direction test that was the whole point still stands after it.

## 2026-08-22 — a place on a lane reaches a line by the join's own setback, and by nothing guessed

The conversion was written out twice and both copies guessed the offset from the car's turning radius,
which has nothing to do with it. The assembler trims each lane by the setback its join was drawn to, and
that setback is per turn — nothing at all on a straight-through movement. On Odesa 698 of 1472 turns have
none and the guess put 3.94 m there. Every bar sat up to a car's length further along the line than it
really was, and nothing could see it: the soak's counter reads the same wrong figure the stop rule reads.
It is `LineAssembler.OnTheLineM` now. Red bars crossed went 1 → 0.

## 2026-08-22 — a car under its own geometry owes the crossing what a car on its route owes it

A car driving a template had a lane count of zero and was handed no crossing at all, though two of the
three template shapes happen at junctions where the paint is. Putting a template in the lane chain would be
a lie — its arclength is its own, and `WaysAlong` would carry its metres onto a lane one for one. So the
lane under the car says which crossings there are and the template says where they are, by the same
projection the town used to put the paint on the lane.

## 2026-08-21 — a body crossing the road says so, and neither side searches for the other

Both agent kinds met at a crossing by looking rather than by being told: one asked the proximity index per
crossing per car per tick, the other asked *every car in the town*. Both are readings of the claims now — a
body on a crossing claims the road as the band of the lane it stands in, and the walker asks the road's
claims for the nearest body behind the band on each lane the paint crosses. Looking both ways falls out of
the two lanes running opposite ways rather than out of a radius that also counted the next street. A body
merely walking past a zebra no longer stops the traffic, and one refused by its signal no longer holds
traffic on its own green.

## 2026-08-21 — a driver takes the road before it drives down it, and `P-5` is retired

Following was an entry and a term, and both were wrong about what following is: a driver is not measuring a
gap to a bumper, it is planning to stop in road that has to still be there. The lane index carries a claim
(S-2a) and the grant inverts straight into a speed. Four things each cost a run to find. What is asked for
begins at the body and what is granted is credited past it, or a stretch measured from where a car will
have stopped holds up a driver on behalf of the one behind him. The least resting place in range binds and
never the nearest tail. No order is needed and none is imposed, since near edges are facts about bodies —
the one place order decides is a junction, where a crossing is taken rather than granted. And the rays stay:
a claim holds a body as an interval of arclength, so it cannot say how near the *shape* of a car mid-turn
is, and suppressing the headway term cost 290 emergency stops in a minute of Odesa.

## 2026-08-21 — a queue is what the road says it is, not what a stopwatch says

Deciding from a speed and a clock is wrong for the commonest case: third car in a queue at an unlit
junction reads `Headway`, has no light, and has a stopped car in front — so three seconds later every car
behind the second swings into the oncoming lane, each separately justified. What tells the two apart is not
available to geometry, so the lane index carries every body as a stretch and says which are drivers on
their own route. `E-4` acts on what the index has named and on nothing else, and a body it cannot name is
never gone round. What gets a car out from behind a queue that never moves is the thirty-second blocked
clock and not the three-second obstruction wait. And the claim is the second half, since reading who is
*on* the road cannot see a car about to back onto it: it is a field re-laid every tick rather than a
register, so it cannot outlive its claimant or leak on a wreck.

## 2026-08-20 — an entry is a file, not a case in a switch

The catalogue was a name: an enum, a function reading which entry a car was in off the term that bound its
speed profile, and nine recovery routines scattered across the composition seam. Every entry is now a file
with its `Sa`, its procedure and its exits. A manoeuvre may not be handed a town — one that could reach the
composition could reach the signal heads and the walkers' stages — so it gets `DriveScene`, `ManeuverDesk`
and three `DriveOrder`s, and anything wanting a fourth says the seam is in the wrong place. An order the
town cannot carry is a refused `Sa`. The limits are the whole of what an entry does to a car, or a
manoeuvre could put a car somewhere the tyres could not have taken it. And a car is still an index: no
entry has state, so a hand-over allocates nothing.

## 2026-08-20 — entered on the binding term, exited on the fact

The naming scheme it replaced needed a hysteresis, because a queue at rest sits exactly on the threshold
between the headway term and the stop-point term. The two halves are now asked of different things:
entered on the term that bound the profile, which is the least ambiguous reading of what the car is doing,
and exited on the fact that entry is about. The worst back-and-forth pair went from 103 swaps in one spot
to 20 over 325 800 car-ticks. `P-6` exits on the body moving rather than the line disappearing, since a car
at rest at a junction is bound by the box one tick and the queue the next.

## 2026-08-20 — a reflex keeps its name for a beat

`E-2` fires, brakes, drops below its own trigger, is let go, accelerates into the same gap and fires again.
That is one emergency stop and not twenty, and counting it as twenty buries the reading the entry exists to
give. A reflex holds its name for a second after the thing that fired it has gone, imposing nothing while
it does. Entries over a lit map went 43 → 6 with no change to a single command.

## 2026-08-20 — the plan is a skeleton the planner fills in

A leg is handed a parametrised chain rather than a starting state. The steps are parametrised, so two legs
that park in different bays are the same chain with a different subject. And it stops at the skeleton
(MAN-2): queues, junctions and crossings cannot be in it, because everything past the next junction is a
prediction about other agents. An entry that succeeds without naming a successor is asking for the next
step, and an empty chain answers `P-4`.

## 2026-08-20 — `E-4` is built, and the town decides whether it is reachable

It is laid as geometry in `RoadTemplates` and asks the terrain whether the shape fits, rather than a table
of street widths beside a table of car radii. That is the only way to have one answer, and it makes "is
there room to go round here" a fact about a town rather than about a car. Which entries are unreachable is
`--bench maneuvers`' to report rather than this document's.

## 2026-08-19 — the catalogue names what is not built yet

The `Maneuver` enum carries every entry of AGT-7's closed list, not only the ones with code behind them.
An entry nothing can reach is a finding, and an entry that does not exist at all cannot be one — which is
how an unbuilt manoeuvre and an unreachable one are told apart without a list in a document that goes
stale. Retired numbers keep their gaps for the same reason.
