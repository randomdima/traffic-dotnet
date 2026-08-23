# The driving manoeuvre catalogue — decision log

## 2026-08-23 — `E-3` backs away from obstructions and from being one, and never from traffic

The back-off's `Sa` asked how *near* the thing in front was and not what it was: anything inside two car
lengths, or any car held `Waiting`, had "something to back away from". That is the whole of a queue at a
red, and it is a car in front of a claim waiting perfectly correctly for a movement that is about to clear —
so a jam whose real answer was another second of patience got a reversing car instead, into road the traffic
behind was entitled to be standing in.

The door now reads the book's own name for what is in front (`NobodyEntitledIsInTheWay`): a wreck, a car
nobody is in, a body shoved off its line, somebody on foot. `Waiting` survives only where the car is
**itself** across a lane or in a box, because the ground it is reversing out of is then the ground it is
blocking, and that is a reason of a different kind.

Odesa over the measured minute, everything else held (`--bench maneuvers`, `--bench soak`):

| `Sa` | back-offs | given up | abandoned | touches |
|---|---|---|---|---|
| near-or-waiting (before) | 33 | 21 | 9 | 48 |
| entitlement, boundary kept | **26** | 28 | 14 | **46** |
| entitlement, no boundary door | 13 | 33 | 24 | 38 |

**The third row is why the boundary door was kept.** Refusing a car in a box the one recovery that gets it
out of the box is not patience, it is a car that stands there until the ladder abandons it — and the ladder's
bottom rung leaves a driverless car in the road, which is a permanent obstruction rather than a temporary
one. The figures either side of the shipped row are one run apiece of a chaotic town and the spread on them
is wide; what is not noise is the direction, and `E-3` is still entered on every map that has jams.

## 2026-08-23 — a queue keeps a following time, and `E-2` fires above the plan instead of under it

Following worked and read badly. A platoon moved in lurches, and all three reasons were arithmetic rather
than anything about the reservation itself.

**The equilibrium gap had nothing in it.** The grant inverts to a speed through a lead, and that lead was
`CarReactionS` — so setting the two cars to one speed and solving gives `gap = standstill + v·τ`, with the
braking figure cancelling out entirely (the car in front is credited with its own stopping distance out of
the same arithmetic). At a tenth of a second that is **3.5 m at 15 m/s, a 0.23 s headway**: arithmetically
safe against a leader braking at exactly the rate the follower planned for, and with nothing whatever left
over. Anything that made a leader brake harder than that broke it for every car behind at once. The lead
the grant is read at is a **following time** now (`Driving.FollowingHeadwayS`) and the reaction lead stays
on everything else, which is the first thing that has ever made `Reserved` and `Headway` different terms —
at the shipped figures they were algebraically the same number, because half a car's length and the
standstill gap are both 2 m.

**`E-2` was set below what the profile plans with.** It compared the required rate against `GripMargin` of
the tyres, 13.11 m/s², while every stop the profile plans is at `BrakingMargin`, 17.79. So the reflex fired
on ordinary planned braking, took the pedal to its full 27, overshot its own release condition, handed
back, and the profile — seeing slack — went to full throttle. That loop is what the lurching was.
`E-2` is the tick the margin the profile keeps ran out, so it now reads the same figure **without** the
margin, which is what its own doc-comment had always claimed it read.

**And the pedal was a relay.** The command was whatever closed the speed error in one tick, so anything
past a fifth of a metre a second saturated it; a car merely holding a speed snapped between the two stops
several times a second and the tyre model answered each snap with a load transfer. It travels at a bounded
rate now (`Driving.PedalTravelS`), and because a travelling pedal arrives late, the profile's lead gained
the half of that travel it costs — without which cars braked over less ground than they planned for and
made the difference up by braking harder than they planned.

**And a grant is looked for as far as the gap it keeps, which is further than the ask reaches.** What a car
lays into the book is the road it is committed to — a stopping distance and a reaction interval — and that
is shorter than a second of headway at any town speed, so the cut simply went unfound until the car had
closed to inside its own reservation. The profile then held it off on the headway reading, at the reaction
lead, and the pair settled into closing up and falling back rather than into a gap. Looking further can only
make a grant smaller (it is the least of everything found), so nothing about the safety of it turns on the
reach and only the fluency does. On Odesa it took the `Reserved` term from 5 % of driver-ticks to **14 %**
and the `Headway` term from 7 % to **1 %**: the reservation is what following is again, and the reading is
back to being the backstop it is for.

Over the shipped maps, against all four together:

| | before | after |
|---|---|---|
| `E-2` entered, Odesa · Track | 101 · 57 | **21** · **12** |
| `E-2` share of Track car-ticks | 16 % | **3 %** |
| worst back-and-forth pair | `P-6`↔`E-2` 24 · `P-4`↔`E-2` 17 | `P-4`↔`P-6` 15 · none at all |
| legs begun on Odesa in a minute | 47 | **115** |
| junctions taken · crossings passed | 154 · 144 | 326 · 251 |
| laps a car on the proving ground | 2.8 | **7.3** |
| Odesa mean speed · ground covered | 10.96 m/s · 294 m | 11.01 m/s · 297 m |

Two instruments had to be repaired to read any of this. `--bench drive` collected the `Reserved` hold and
printed no column for it, so the one term that *is* following was invisible — it is the `granted` column
now. And the proving ground's slowing rate was reconstructed from a mean speed and a mean distance, which is
a mean of means and answered for no pass that ever happened; it is worked out per slowing and meaned now.

## 2026-08-23 — a walker in the road is an agent and not a rule

`E-4` could not act on somebody standing in a lane, and `E-1` treated one as exercising a priority. The
reason given was that the ground a swerve takes is the oncoming lane — but that is a fact about the
*ground*, and the ground is already asked: `E-4` walks its template and asks the book whose every point
under it is, and a body on a carriageway is a stretch of that book with a margin round it (`PER-15`). The
rule was a second thing refusing a movement the first one already refuses, which is exactly what `SIM-7`
says makes the first useless.

So `HeadwayKind.Walker` is something to get past. **Paint is where a walker's priority lives** — a car owes
a crossing its stop short of the band (`P-12`) long before this question is reached — and a body on bare
carriageway is in the way of a road it was never entitled to.

## 2026-08-23 — `E-4` becomes an overtake, and stops being rationed

Passing something that is *moving* was a manoeuvre the catalogue did not have, and four separate things
were in the way of it. None of them was the hard part anybody expected.

**The swerve was drawn at the steering lock**, because that is the radius the bay templates need and it
lived in the same place. The profile reads the arcs of a template exactly as it reads the arcs of a road,
so a shape made of 4 m corners is one the corner term holds the car to 6 m/s to drive — a car could not get
past a body walking at 6.6 because it had slowed below it to try. The radius is the **caller's** now, and
`E-4`'s caller asks for the one the speed affords (`CarCorneringRadiusM`, the corner formula turned round),
floored at the lock for a car starting from rest.

**The shape was drawn flat, on a lap that bends.** An S laid tangent to the heading is a chord across
whatever the road was doing, and fifty metres of chord on a forty-metre radius is ten metres off the road —
so `GroundAdmits` refused it, and `E-4` had been reported as "reachable" on the strength of a handful of
swerves on the one straight. Every piece of the shape carries the road's own curvature underneath the S
now, which makes the thing the S *does* — out by an offset, back onto the line — the same relation on a
bend as on a straight. It is the same correction the walker's own lurch needed for the same reason.

**It moved over by the car's own width**, which is right for a wreck sitting on the line and wrong for
everything else: the book carries what is in the way as a stretch of arclength and never as a place across
the road, so where in its lane the thing actually stands is a fact nothing can read. A shift of a body's
width leaves the car spanning both halves of its own lane. It is **a lane over** now, which clears anything
the lane can hold, with the narrow shift kept as the last thing tried for a road that has no room for it.

**And the pass straight was the static gap.** Against something moving, the ground needed to gain a car
length is `clear · v ⁄ (v − u)`, which is the static figure again wherever `u` is zero and is a road nobody
has as `u` approaches `v`. That last part is what refuses an overtake of something nearly as fast as this
car, and no figure of `E-4`'s own is involved: it is refused for asking for more ground than a driver can
see (`CarSightM`).

**What decides it is one reading and both entries read it.** `DriveScene.WorthGoingRound` — `P-4` names
`E-4` off it and `E-4` enters on it, so a car cannot be handed over to something that then refuses it. It
has two doors, because the two things that can be in the way are not the same situation, and **both spend
the obstruction wait**: something stopped may be about to move, and something crossing the carriageway is
out of the lane in a second. What differs is the clock that can answer. A car behind something stopped
stands still and spends the blocked clock; a car behind something slow never stops at all, so it spends
`HeldBackS` — the same patience for the case where the car keeps moving. Reaching for the swerve without
that second clock cost the pacing lap a third of its throughput: a driver swung out past every body that
stepped off a kerb.

**The attempt budget is deleted rather than repaired.** "Two per leg" was a bound on a journey, and a leg
is not a journey — a car with no destination is on one leg for its whole life, so it had two swerves for a
life. Ground covered since the last one was tried instead and is worse: **a car that cannot move cannot
earn any measure of them back**, so a car that swerved past one body and was stopped by the next stood
there until it gave the journey up, and the whole of the proving ground's traffic did exactly that. What
bounds the wrong side of the road is what always bounded it — the obstruction wait, which every swerve
costs, and the ground walk, which lays a shape only where the oncoming half is clear for the whole of it.
A road with something in the way every two hundred metres is a road a driver genuinely spends on the other
side of, and any spacing wide enough to price that in is a spacing that jams it.

**Manoeuvring pace is a fact about the manoeuvre and not about the line.** Every template was held to the
reverse cap for having no lanes under it. That is right for a car easing into a bay and wrong for a swerve,
which is the road's own line moved a lane sideways — and for the curved templates it was doing nothing
anyway, since the corner term already holds them below it. `ManeuverCatalogue.AtManeuveringPace` names the
one exception.

## 2026-08-23 — a car shoved across the centreline takes the lane it is pointing down

Taking the lane under a car that has lost its line refused the nearest one where that one ran the wrong way,
because a car set off down the oncoming lane is a head-on rather than a recovery. It then stopped, which
left the one case the recovery exists for with no answer at all: a body shoved over the centreline stands
nearest the *oncoming* line, still pointing the way it always was. The lane it wants is that one's reverse,
and it was refused it. Standing there, it was on ground a car may drive on, so `E-8` had nothing to say
either — and the ladder took the car all the way down to giving the journey up, where it then read as a
queue nothing behind it could pass. **The nearest lane's reverse is looked at rather than the search
abandoned**; the direction test that was the whole point still stands after it.

## 2026-08-22 — a place on a lane reaches a line by the join's own setback, and by nothing guessed

The town measures its furniture against lanes and a driver meets all of it on one assembled line, so
everything between the two goes through one conversion. That conversion was written out twice — once for
`P-8`'s painted bar and once for `P-12`'s paint — and both copies guessed the offset between the two
measures from the **car's turning radius**, which is a figure that has nothing to do with it. The
assembler trims each lane by the setback its join was actually drawn to
([`RoadGraph`](../../../../world/road/RoadGraph.cs)), and that setback is per turn: it is the first rung
of eight at which the biarc stops being tighter than the corner radius, so on a straight-through movement
it is **nothing at all**. On Odesa 698 of 1472 turns have no setback, and the guess put 3.94 m there;
across all four maps the mean error was 2.4 m.

Every bar and every crossing therefore sat up to a car's length further along the line than it really was,
and a car stopped that much late — over the paint rather than short of it. Nothing could see it: the
soak's red-bar counter reads the same wrong figure the stop rule reads, so the two agreed with each other
all the way past the bar.

The conversion is [`PathAssembler.OnTheLineM`](../../../../world/road/PathAssembler.cs) now, next to the
code that laid the mapping, and the call sites ask for it rather than reconstructing it. Measured against
the guess over the same minute: **red bars crossed 1 → 0**, which is River's one long-standing crossing
and it was this all along. On Odesa `P-8` entered 555 → 605 and `P-6` 320 → 355 — cars stopping at bars
instead of drifting to the mouth of the box — while `E-2` fell 188 → 166 and `E-1` 12 → 6, which is the
same thing seen from the other end. `P-12` entered 191 → 135 for the same reason and not against it: a
crossing three metres late is a crossing a car is told it is still approaching after it has driven over
it. The one figure that moved the wrong way is the `P-4`↔`P-6` churn, 49 → 60 in the worst spot, which
follows the extra holds.

**A lane's metres are its own arclength**, bends and all, and so are a line's; the two run at the same
rate because the line over a lane *is* that lane's arcs. There is no scale between them to get wrong —
only the origin, and the origin is a number the graph already holds.

## 2026-08-22 — a car under its own geometry owes the crossing what a car on its route owes it

`P-12` was defined over the route and read off the lane chain, so a car driving a template had a lane
count of zero and was handed no crossing at all. A swerve past an obstruction, a swing out of a bay and a
turn-around are all short shapes over the same tarmac as everything else, and two of the three happen at
junctions, where the paint is.

What stopped this being fixed by putting a template in the lane chain is that it would be a lie: a
template is laid over no lane, its arclength is its own, and `WaysAlong` would carry its metres onto a
lane one for one and put the car somewhere it is not. So the two halves are asked of the two things that
know them — **the lane under the car says which crossings there are, and the template says where they
are**, by the same projection the town used to put the paint on the lane.

There is no approach for a light to govern on ground no lane owns, so the stop and the pace are the whole
of it — and the pace comes off the same projection, since the paint has already been found and the pace is
owed whether or not anybody is standing on it.

## 2026-08-21 — a body crossing the road says so, and neither side searches for the other

The two agent kinds met each other twice at a crossing, and both times by looking rather than by being
told. `P-12` asked the proximity index, per crossing per approaching car per tick, whether anybody stood
within a stride of the paint; the walker at the kerb asked **every car in the town** how long before it
reached the point the body was about to occupy. Two searches of the ground for two facts each body already
knew about itself.

Both are readings of the books now:

- **A body on a crossing writes itself into the road's book**, as the band of the lane it is standing in
  ([`LaneUse.OnFoot`](../../../../world/road/LaneOccupancy.cs)). `P-12` asks the lane it is driving
  whether that band is spoken for. The use is its own for a reason that is the whole of why the two books
  were separate to begin with: a walker read as a reservation would cut the grant of a car three lanes from
  where it stands, and one read as an obstruction is one nothing could tell from a wreck.
- **The walker asks the road's book what `P-2` asks it**: the nearest body behind the band on each of the
  lanes the paint crosses, as a time (§8 rule 8). Looking *both ways* falls out of the two lanes of a
  stretch running opposite ways, rather than out of a radius that also counted cars on the next street.

Two behaviours moved, and both were the point. A body merely walking down the pavement past a zebra no
longer stops the traffic — it was inside the old approach box, and it was never going to cross. And a body
at a kerb whose signal is refusing it no longer holds the traffic on its own green, which the old geometry
did unless the red-wait setback had already walked it out of the box. Over a minute of Odesa: `P-12`
entered 178 → 191, kerb waits 570, none begun on a red, nobody standing on paint, and the fleet scan gone
from the walkers' tick.

## 2026-08-21 — a driver takes the road before it drives down it, and `P-5` is retired

Following was an entry of the catalogue and a term of the speed profile: `P-5` named it, and the profile
held the car at whatever gap three ray chains had measured to the shape in front. Both halves were wrong
about what following *is*. A driver is not measuring a gap to a bumper; it is planning to stop in road
that has to still be there when it gets to it, and the town's own book already knew which road that was.

So the lane index carries a **reservation** now (S-2a). Every driver under way asks for the stretch of its
own way from its tail to where it plans to be able to stop, and is granted what is left of that in front
of the nearest car already on it. The grant inverts straight into a speed — what may be held here to be at
rest by the far end of it — and **`P-5` is retired with its number**: a car behind another is running its
line on a shorter road, which is `P-4`. The one decision `P-5` owned that `P-4` could not take, that the
thing in front is an obstruction and the way past it is round it, moved to `P-4` with it.

Four things had to be true of the arrangement, and each of them cost a run to find:

- **What is asked for begins at the body, and what is granted is credited past it.** A stretch measured
  from where a car will have *stopped* reaches past a slower car in front of it, and cutting that car at
  it holds up a driver on behalf of the one behind him. The near edge is the tail, and what the ground
  beyond a tail is worth to a follower is that car's own stopping distance — so two cars at the same speed
  keep a standing gap instead of opening out to a stopping distance apiece, which is the behaviour the
  headway term already had and the reason the town did not have to be re-tuned around this.
- **The least resting place in range binds, and never the nearest tail.** A car at speed rests further up
  the road than a slower car ahead of it, so the first stretch a walk meets is regularly not the one that
  matters — and a grant cut at it runs straight through whatever is beyond.
- **No order is needed and none is imposed.** Near edges are facts about bodies, so every ask is laid
  before any of them is answered and two cars get the same answer whichever is asked first. The one place
  order does decide is a junction, where somebody has to win: a crossing is *taken* rather than granted,
  and first come holds it.
- **The rays stay, and they are not a second gate on the same movement.** The book straightens a way's
  curvature out and holds every body as an interval of arclength, so what it cannot say is how near the
  *shape* of a car mid-turn, one straddling a join or one cutting a corner really is — and a walker is in
  no such book at all. Suppressing the headway term wherever the index had a name for what was ahead cost
  **290 emergency stops in a minute of Odesa**. Kept, the same minute takes E-2 from **262 entries to 185**
  against the mechanism it replaced, with half again as many junctions taken.

## 2026-08-21 — a queue is what the road says it is, not what a stopwatch says

The driver decided whether the thing in front was a queue or an obstruction from a **speed and a clock**:
stationary for longer than the obstruction wait, with nobody visibly exercising priority, and `E-4` was
free to cross the centreline and drive round it. The reading it was working from had no idea what it was
about — `DriveContext` carried a distance and a closing speed, and the identity the cast had found was
thrown away before any entry saw it.

That is wrong for the case it will meet most often. Third car in a queue held by an **unlit** junction:
the headway term binds tighter than the junction term, so the hold reads `Headway` rather than `Waiting`;
there is no light, so `LightAheadM` is infinite; the car in front is stopped, so the speed clause says
nothing. Three seconds later every car behind the second swings out into the oncoming lane, each of them
separately justified.

What tells the two apart is not available to geometry at all, so it is now held as a book: the **lane
index** ([`LaneOccupancy`](../../../../world/road/LaneOccupancy.cs)) carries every body on the network as
a stretch of the way it stands on, rebuilt from the fleet's own arrays in phase 2, and says which of them
are drivers on their own route. `DriveContext` gained one field for what it answers, and three things
changed shape around it:

- **`E-4` acts on what the index has named and on nothing else.** A wreck, a car with nobody in it, a body
  off its own line. A driver queueing is not one however long it has stood, because whatever holds the car
  at the head of that queue is not this car's to drive round; and **a body the index cannot name is never
  gone round either**, because a reading that cannot say what is in the way cannot license crossing the
  centreline to pass it.
- **What gets a car out from behind a queue that never moves is the blocked-road clock**, thirty seconds,
  and not the obstruction wait, which is three. `E-1` is the rung it reaches first and is itself bounded
  by that clock, so the ladder still ends where it always did.
- **The claim is the second half.** Reading who is *on* the road cannot see a car about to back onto it,
  so `P-2` and `E-4` mark the stretch they are about to occupy before they occupy it — the same argument a
  car crossing a junction makes about the sections of it its own line is driven over. It is a **field on
  the car re-laid every tick** rather than a register: a claim cannot outlive its claimant, cannot leak on
  a wreck, and is given back where the entry that took it is left, so no entry can forget one.

It costs 4 % of the frame on Odesa and takes the share of driver-ticks that lay rays from 12 % to 10 %,
because a leader is now acquired from one walk of a lane's list rather than from three ray chains. **The
gap itself is still read off the body's own pose and never off the index**, so nothing about the reading is
a tick old — which is the finding [the car's own log](../../docs/decision-log.md) paid for.

## 2026-08-20 — an entry is a file, not a case in a switch

The catalogue used to be a **name**: an enum, a function that read which entry a car was in off the term
that had bound its speed profile, and a ladder that could hand a car to one of nine recovery routines
scattered across the composition seam. Everything a manoeuvre *did* lived somewhere else, and the seam
grew a method per entry.

Every entry is now a file with three things in it and no more — its `Sa` (with the take-up, so a refusal
has written nothing), its procedure and its exits, and the two traits that say how it is scheduled and
whether the fuse watches it. Adding an entry is a file, a page, and one line in each switch of
`ManeuverCatalogue`. Four things came out of doing it:

- **A manoeuvre may not be handed a town.** An entry that could reach the composition could reach the
  signal heads and the walkers' stages, and the discipline that keeps a manoeuvre a bounded procedure is
  that it can only ask what a driver could see and only do what a driver could do. So it gets
  `DriveScene` (the facts), `ManeuverDesk` (the ground questions and the templates), and **three**
  `DriveOrder`s for the things that genuinely need the whole town at once. Anything that wants a fourth is
  a sign the seam is in the wrong place.
- **An order the town cannot carry is a refused `Sa`.** It is what lets `E-6` mean "retarget, and if
  there is nowhere to retarget to, take the next rung" without the entry knowing what a bay register is.
- **The limits are the whole of what an entry does to a car.** A manoeuvre that could write a command
  directly could put a car somewhere the tyres could not have taken it. It sets a cap, a stop point or a
  hold, and the standing rules fold that into the minimum the speed profile was taking anyway.
- **A car is still an index.** No entry has state of its own; the fleet's arrays carry everything, the
  entries are static classes, and a hand-over allocates nothing.

## 2026-08-20 — entered on the binding term, exited on the fact

The naming scheme it replaced had a hysteresis on it: an entry was taken up only once the term binding
the speed profile had read the same for a whole decision interval. Without it, `P-4` and the entry that
named a queue swapped a stationary car back and forth five hundred times in one spot, because a queue at
rest sits exactly on the threshold between the headway term and the stop-point term.

The hysteresis is gone and the flicker with it, because the two halves are now asked of different things:

- **Entered on the term that bound the profile.** A car's speed is the minimum of everything that limits
  it, and the term that won is the least ambiguous reading there is of what the car is actually doing.
- **Exited on the fact that entry is about.** `P-12` gives the car back when the paint is behind the body,
  `P-8` when the box is behind it — not when some other term won the minimum.

Measured over a run of the shipped maps, the worst back-and-forth pair went from **103 swaps in one spot
to 20**, over 325 800 car-ticks. One entry needed a second half to the test and it is worth knowing:

- **`P-6` exits on the body moving, not on the line disappearing.** A car at rest at a junction is bound
  by the box one tick and by the queue in front the next, and neither means the thing it stopped for has
  gone. The entry imposes nothing, so the profile pulls the car away the moment it may — taking the exit
  from that is what removed the last of the flicker.

## 2026-08-20 — a reflex keeps its name for a beat

`E-2` fires, brakes, drops below the closing speed that triggered it, is let go, accelerates into the same
gap and fires again. That is **one** emergency stop and not twenty, and counting it as twenty buries the
reading the entry exists to give — a high count is a planning failure upstream, and it can only be read if
the count means what it says.

So a reflex holds its name for a second after the thing that fired it has gone, **imposing nothing while
it does**: the behaviour is exactly what it was, and the instrument now reports events rather than
triggerings. Entries in `E-2` over a lit map went from 43 to 6 with no change to a single command.

## 2026-08-20 — the plan is a skeleton the planner fills in

The route search now hands a leg a **parametrised chain** rather than a starting state: leave this bay,
run the route, park in that bay, stand in it. Two properties are what make it worth being a structure
rather than a sequence of implicit hand-overs:

- **The steps are parametrised**, so two legs that park in different bays are the same chain with a
  different subject, and the planner needs to know nothing about how an entry is driven.
- **It stops at the skeleton** (MAN-2). Queues, junctions and crossings are not in it and cannot be,
  because everything past the next junction is a prediction about other agents; those entries are reached
  from `P-4`'s own exits as the road produces them, and each hands back to `P-4`.

An entry that succeeds without naming a successor is asking for the next step, and an empty chain answers
`P-4` — which re-derives from the pose the car actually reached, which is what MAN-3 asks for anyway.

## 2026-08-20 — `P-11` and `E-4` are built, and the town decides whether they are reachable

Both had been named in the enum and had nothing behind them. Both are now laid as geometry — the
counter-swing and the swerve, in `RoadTemplates` — and both ask **the terrain** whether the shape fits
rather than a table of junction sizes beside a table of car radii. That is the only way to have one
answer to the question, and it makes "is a turn-around possible here" a fact about a town rather than
about a car.

On the shipped maps `E-4` is reachable and `P-11` is not: a six-metre crossroads cannot hold the shape at
this car's turning radius. That is the honest outcome and it is reported by `--bench maneuvers` rather
than written down here, where it would be stale the week a wider town is exported.

## 2026-08-19 — the catalogue names what is not built yet

The `Maneuver` enum carries **every** entry of AGT-7's closed list, not only the ones with code behind
them, and `--bench maneuvers` ends every run with the set of entries nothing entered.

That is the whole point: **an entry nothing can reach is a finding, and an entry that does not exist at
all cannot be one.** It is also how an unbuilt manoeuvre and an unreachable one are told apart without
anybody keeping a list in a document that goes stale. Retired numbers keep their gaps for the same
reason — a reused number makes an old finding resolve to the wrong thing.
