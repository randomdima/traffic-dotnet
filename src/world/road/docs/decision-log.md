# Decision log — roads and junctions

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md) and [claims.md](claims.md); how
a type works is its own XML docs.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- **TER-5c.1**: ground driven over is settled against the crossed way's claims as a stretch is laid, either
  side being cut, and the grant is what the laying left — not only looked up by the driver.
- **TER-4c.3**: a pair on two ways is settled over the section — a firm hold beats one that is not, two
  firm holds both stay, and a tie goes by roster and occupant rather than to whoever is already there.
- **TER-5e**: a closed road is a police car's claim at a scene, not an officer's, and sits above no paint.
- **TER-5g**: p2 reworded the same way; a walker waits on no statement and states the ground it would come
  to rest in from its pace.
- **TER-5**: a junction's radius is the connection standoff (a car park's, its rank's) and is not sized on
  the arms; nothing measures from it.
- **TER-6**: a town's zebras stand at the kerb ends' stations, not at pedestrian node pairs, spanning the
  carriageway's two edges; a box that forks nothing carries none.

## 2026-09-21 — a body writes every ribbon its box touches, and nothing is held back

A body under way wrote only the lanes running with it that its middle projected inside: the joins it lay
across, the lanes it stood past the end of and every way running against its heading were held back, each on
the grounds that another reading answered for that ground — the crossing table for a join (TER-5c.1), the
box for the ground past a lane's end (TER-5d), the off-the-line bar for the lane running back. **None of
those answers the question a body asks**, which is *what is standing here*, and a car lying across a
junction was a car every driver crossing it read as empty ground.

**Every way the box touches is written now** (TER-4c.2, `ReadTheGroundUnder`), of every kind and in every
direction, at p0. What a body *costs* whoever meets it stays the reader's: the stretch carries how far
across the way its holder stands (`LaneClaim.AsideM`), and the traffic decides whether it can get past.

**It made the town safer and slower**, over 300 s of the laid city: wrecks 59 → 19 and touches 4383 → 3746,
against cars standing still at the end 121 of 461 → 182 of 501 and two rings of cars each waiting on the
next. The stuck probe is where that reads.

**And a hold now ends earlier**: a car in a box stands on the last metres of the arm behind it, so a car
approaching has its road cut there — it holds no part of the box, states it instead, and is still granted
the road past a body it can get by (`StandsAside`). `ACarClaimsTheBoxFromItsNearEdge` is asked with that
shape: a car whose own stretch never reached the boundary is owed nothing past it.

## 2026-09-21 — a body read twice is one stretch, and the pose reading grows it

A body under way is read twice: from the line it is driving (`AskForTheGround`) and from its pose, the box
on every way it touches (`PlaceTheBody`). One body is one stretch of one way (TER-5c.2), so the second
reading gave way to the first — by being **dropped** wherever the two met.

**Met is not covered.** A claim's body edge is the nose of the line, and the leading corner of a car yawed
against that line reaches past it; those metres stayed in the claim as ground *granted*, which a stronger
movement may take (TER-5e) out from under a body standing on it. On the laid city a minute in, 47 of the 53
lanes a box stood on were held by a claim that stopped short of it, by up to 0.48 m.

**The pose reading now grows the stretch it meets** (`LaneOccupancy.StandOutTo`): the near edge back and the
body edge out, each as far as the metre asked for or the nearest stretch that way, so the claims stay
disjoint and the hold stays one stretch. All 53 are held whole.

**What hid it was a float comparison.** Whether a body is inside a lane or past its end is a clamped
projection resampled from arclength, so a body square in the middle of a lane reads microns past it; tested
as `> 0`, that round-off dropped every way under every driving body before the dedupe saw one. The bar is
`CrossesOntoAWayM` — the figure the band is crossed by, said of the other axis.

## 2026-09-21 — one ladder, and the right of way is a rung on it

A claim carried two orders: how strong it was (`ClaimPriority`) and what right of way its holder had
(`RightOfWay`), and every meeting compared both. The pair could disagree — a claim strong in one and weak in
the other — so what settled a junction was a table of pairs wearing a rule's clothes, and every reader was
handed two values to ask one question.

**The movements are rungs now** (TER-5e, TER-5g): straight through, then ordinary traffic, then the turn
across the oncoming stream, with a call and a closure above them and a body above everything. `LaneClaim`
lost its `Right`, and `Binds` and `TakesAClaim` compare one byte against one byte.

**What made one order carry both is the two bands.** A statement must stay told apart from a grant — an
equal movement takes a statement and does not take a grant — and every statement is weaker than every grant,
so no single placing does it. The stated band *mirrors* the granted one seven rungs down, and a statement is
compared at the rung its holder would have been granted (`SaidAhead`). A street's statement refuses the turn
across it as before; the turn's refuses nobody but itself.

**A call states road as a call**, which is why the mirror starts at `Special`: a rescue's stated road refused
ordinary traffic when the rank carried it, and a band beginning at the movements would have quietly handed
that back.

**The committed claim stays `Hard`.** It carries the body and the road the body cannot give back; the
movement decides what the car may be granted *ahead* of it. Laid at the movement's own rung instead, a body
stood on road at p5 with the box beyond it at p0, and `NoCarHoldsGroundStrongerThanTheRoadToIt` said so
within two ticks of Test and Laid.

**What it cost**: nothing the suite can see — `all`, the shipped cities and the visual tier green, `perf`
unmoved. What it buys is one comparison in the town and one place to change it.

## 2026-09-21 — one laying of a town's zebras, read by both networks

The walk was cut and joined at 488 crossings in `Towns.City` and the road knew about none of them. The walk
lays its paint off the town's own kerb ends (`Crossings.Lay`, WLK-10), while `LaneFurniture` and
`CrossingBands` read `CityPlan.Crosswalks`, empty since a junction stopped striking its own crossings
(`RoadStage`). Empty shows up as no answer rather than a wrong one: no lane carried a crossing, no crossing
carried a lane, no walker was ever *on* one, and the stop short of paint a queue would leave a car standing on
(`CAR-15`) had nothing to fire on in any town.

**So the town lays its zebras once and hands them round** (`TownWorld._zebras`): to the walk cut at them,
the lanes that carry them as furniture, and the bands projected beneath the paint. What the walk crosses is
what the road is crossed by, because it is one laying and not two that agree. **It is the plan's record that
was dropped and not the projection**: `Crossings` already carried everything the two readers took out of
`CityPlan.Crosswalks`, and the renderer was already on it.

**What it switched on**: `CAR-15` in every town, the bands under the paint, and `PER-27`. The suite is
unmoved — `all` in 36.5 s, the shipped cities and the visual tier green — because what a body on a zebra
held was never what held the traffic off it (TER-5e).

## 2026-09-21 — a zebra is a way and a car on one is on it

A car on the paint was written onto the lane and nothing else: the walk over the pavement was made for it
like every other body, and then a filter threw the crossing ways back out (`WalkedAlone`), on the reasoning
that ground two networks share has one owner and a second write is one body held twice.

**The reasoning was wrong about what a second way is.** TER-4c.3 is about one way's metres, so two ways
carrying a stretch apiece is what the table is for — and exactly what a walker on the same paint already
wrote. The filter made the kind of body the one thing the write turned on, which TER-4c.2 says it never is:
a car on a footway was visible to the walk, and the same car three metres away on a zebra was not.

**So the filter is gone** (`ReadTheGroundUnder`): a body on the paint holds its band of the lane and its
stretch of the crossing way, whichever roster it is in, and `PavementWays` no longer needs to know what a
crossing is. **It costs no capacity** — the filter ran after the walk, so `MostPavementRowsPerCar` was
already sized for the rows it discarded; the gates and the suite are unmoved.

**What it buys is that the ground is visible, not that anybody behaves differently.** Nothing on the walking
side reads a claim to decide anything (`PER-26`), and what holds the traffic off somebody on a zebra is still
that body's band of the lane. Two bodies over the same metres of a crossing way settle like any pair — the
one further back keeps them — which can cut a walker's stretch of the paint from under a car, and costs the
walker nothing, its lane claim being the one that refuses anybody.

## 2026-09-21 — the road to a box is worth what the box is worth

The overlay washes by rung (`DebugOverlay.Occupancy`), and it showed a car braking for a crossing with its
own lane washed faintly behind it and the whole turn solid in front: p0 body, a stated road and a granted
box — a hold whose strength grew along it.

**It grew because the two pieces came by different routes.** The box is arbitrated by the junction and
comes back a grant; the metres to its mouth were arbitrated by nobody and laid with every other driver's
statements. Braking made it visible: the ask is bounded by whatever stops the car, so a car slowing for
paint asks for almost no road and the whole approach is statement. What it cost is not a shade — a statement
is the one hold an equal movement may take outright (`Binds`), so another movement could be granted the
exact metres a car had to cross to reach ground it was holding.

**The rung is carried back from the box and never forward from the road** (TER-5g.1,
`LevelTheRungToTheBox`). Granted as far as the mouth, the road to it is raised to the box's rung; cut short
of the mouth, the box is let down to a statement, because a car that is not getting there holds a junction
shut for nothing. A body past the point it could stop is raised either way.

**It is the last pass of the rebuild**, since the rung it carries is the answer's. `TakeTheMovement` runs it
too and `DropTheMovement` undoes it: a movement taken or given back mid-walk is read by every car after it,
and an approach left raised for a box the car no longer holds is worse than one never raised.

**Tried and taken out**: deciding the two cases by walking the hold and asking whether the road back to the
body was unbroken. It is the same verdict by a longer route, and it needed a metre's tolerance at every seam
to tell a touch from a hair, where the grant is one figure already worked out.

**What the soak says it cost**: nothing measurable. Odesa's minute drove 355.6 km against 355.8, with the
same cars wrecked, the same walks given up and eight more touches in 524.

**What the gate cannot tell** is which of the two answers was right: raising the road and letting the box
down both leave the rung flat, so `NoCarHoldsGroundStrongerThanTheRoadToIt` passes with the letting-down
taken out. That branch stands on TER-4c.1 — a claim holds the answer, never the question.

**The exception is named in the gate, not worked around in the code.** A committed car's road to its box
reaches past its grant on the lane as well as on the join, so `NoCarHoldsRoadItWasNotGranted` allows p0 out
to that box's far edge for a car that can no longer stop, and refuses it for everyone else.

## 2026-09-20 — a hold is one unbroken thing, and the statement is what carries it to the box

The overlay showed a car stopped short of a junction holding its own body, then a dozen metres of nobody's
road, then the whole arc of the box it had been granted — each piece working as written, and the whole
wrong in three places.

**The movement was laid as a claim per crossing run**, so one car was several holders of one way with
nobody's ground between them. The metres between two crossing points are driven over by nothing, so holding
them refuses nobody: the claim is one stretch from the first crossing the car has still to reach to the last,
and `Withdraw`'s "an occupant may not hold two independent claims of one priority on one way" stops being a
thing a caller has to remember.

**The approach was held by nobody.** A committed road is a braking distance — at a standstill the car's own
length and a metre — so the gap to the box was structural. The statement closes it (TER-5g): a car short of a
box it holds states the road up to it, moving or not, the one exception to *a body that is not moving states
nothing*. It is affordable because the box is already claimed: what crosses those metres was refused before
the statement existed, and a statement refuses nothing along its own holder's way.

**The seams were one metre worked out twice.** The movement's near edge is now laid over the car's own road
and cut back at it, so the seam is the metre the road actually reached; `ClaimWhatTheAnswerTook` is handed
the metre the cut was made at for the same reason.

**The gate asks the property and not the arithmetic** ([tests/gates](../../../tests/gates/)): no holder's
stretches of one way have road nobody holds between them. It found two faults the day it was written — a
road cut back from past the last crossing to the first handing nothing over, and a statement pushed to a
join's far end by a movement dropped again inside the same walk. Neither would have been found by reading.

**Tried and taken out**: refusing outright any claim whose near edge is inside somebody else's, a body's
included. It costs a car in the middle of a box its whole way out the moment anything stands between it and
the exit, and the car in front already holds those metres — so a body is written wherever it stands and
everything else is bound.

## 2026-09-20 — the answer belongs to the hold and not to one stretch of it

The overlay still showed the gap on a turn and the gate did not: 37,000 a minute on Odesa, and one in the
laid city 34 ticks past the end of the gate's watch. **The answer was written back to one row and everything
else stayed anchored to the question.**

`CutTo` was scoped by what a claim looks like, and the only scope naming the committed road was *the stretch
with the body in it*. A hold is a run of ways and only its first carries the body, so the pieces ahead of
the nose — the join about to be crossed, the lane past it — were never cut: the lane ended at the grant, the
join went on holding the far end of the ask, and the metres between were nobody's. They are told from an
occupant's other holds by **the ground they are on** and not by their look (`CutTheAskTo`): the road ahead
of a nose, a box the junction gave and a road an officer is holding are the same shape of row and rank.

**And a statement began where the ask ended**, since no answer exists when it is laid — so the answer moved
one edge and left the statement, the span to the box and the movement's own ground where the question had
put them. The metres an answer takes are now stated by the car that asked for them, which is what they
honestly are, and their near edges are **found** rather than worked out: an ask is shortened where it is
laid as well as where it is answered, so each stretch reaches back to whatever is actually behind it.

**Tried and taken out**: filling those metres at the road's rank. It hands the answer back as the question —
the car is refused them, and holding them firm refuses the traffic it was cut at.

**The one rule that had to bend** is that a bodiless stretch beaten at its own near edge gives itself up whole
(TER-4c.3). The span between a car's road and a box it already holds is not reaching into ground its holder
was refused; it is the middle of a hold whose two ends the car has, and with a walker standing on the lane it
is the difference between a gap and none (`takingUpAgain`). Every other caller asks without it.

**What the gate could not see** is a car holding road past its answer where that leaves no hole — the metres
covered by the ask's own leftovers instead of by the hold that should have them. That is TER-4c.1 and a gate
of its own now, watched for eleven seconds where the gate stopped at ten, so the laid city reaches the shape
at all.

**What is left**, named so nobody hunts it twice: a movement taken or given back *during* a tick's decisions
rewrites claims the rebuild laid, and a statement refused against a box hold that is dropped a moment later
leaves its metres unheld until the next rebuild. Odesa's minute has some hundreds of those against 37,000
before, each gone the following tick.

## 2026-09-20 — a hold is one run of ways, and the picture had to be taken twice

The first pass closed the gap on the overlay and the gate agreed; the overlay still had one the gate could
not see, because the gate walked **a way at a time**, and a car's road on a lane and the box it holds on the
join after it are two claims on two ways with nothing in either to compare. The second gate walks a line's
ways in driven order and asks the same question of the chain. It found three faults, each the same mistake
at a different scale.

**A movement taken during a tick's decisions had no approach.** The movement is laid at the rebuild and by
the gate that takes it mid-walk; the statement carrying the span to it was laid only at the rebuild, so every
car was given its box with a hole in front of it for the rest of that tick. The span is one method called
from both, and a movement given back takes its statement with it.

**The answer cut the span off again.** A statement is cut to the grant, which is right for road a car is
asking for and wrong for the metres between it and ground already given it: a body on the paint in front
left a car holding the far side of a junction and not the way in.

**And a hold laid over several ways was laid a way at a time**, so a way in the middle that gave ground up
left the ways past it holding stretches from their own first metre, with nobody's road between. `Lay`
reports whether the stretch went in **whole**, and the three places that lay one hold over a run of ways stop
at the first that did not.

**What made the last of those reachable** is that a stretch beaten at its own near edge used to take up again
past the winner. Between two of one holder's own stretches that is a seam; against anybody else's ground it
is a hold with a body inside it, which is nobody's road the moment that body moves on. A body is still
written wherever it stands: it is already there.

## 2026-09-20 — the paint is not a rank

A walker crossing held the band of every lane its zebra was painted across, at a rank of its own. Nothing
lays that any more: a body on a crossing is a body on the lane under it and holds what it covers, so the
grant that claim cuts is the whole of what stops the traffic
([agents/person](../../../agents/person/docs/decision-log.md) is why). The rank went rather than stay as a
value nothing writes, which reads as a mechanism to whoever finds it next, and `AnybodyCrossing` and
`AnybodyWaitingFor` went with it, nothing asking them. **`ClaimPriority.Rejected` stays**: nothing lays one,
but the arbitration and the claim gate are both written in terms of it, and pulling it is a change to the
claims core rather than to this.

## 2026-09-19 — the ways one place stands on are read into a frame nobody zeroes

Every path asking whose the ground under a body is takes its answer into a span of `WayUnder`,
`MostWaysUnderAPlace` long because a seven-armed junction is — a few kilobytes a call — and `stackalloc`
zeroed all of it first. **Not one of those zeroes is read**: `GroundUnder.At` fills every slot below the
count it hands back before anything looks, and no caller reads above that count. The zeroing was paid once
per car a tick, again for every template a desk weighs, and again for every walker.

The callers carry `SkipLocalsInit`, and **the contract is stated once, on `GroundUnder.At`**, being that
method's promise and not an observation about each frame. On a standing town (`--bench town`): Odesa
**3390 → 3135 µs a tick**, River 3071 → 2877, the claims phase 1355 → 1237 — six or seven per cent, for an
attribute.

**It is a per-method audit and never a module-wide switch.** Most of the tree's `stackalloc`s have not been
read against this contract, and a frame left uninitialised under a method that reads a slot it did not
write reads whatever the last call left — a bug that reproduces differently every run. Each site here was
checked to the count.

## 2026-09-19 — an arrow is the movement drawn small, not a glyph picked out of a set

Three shapes and four more for the pairs, at a fixed right angle, would be wrong twice. **A fixed right angle
lies about the junction**: arms stand anywhere from `ArmsApartMinDeg` apart to nearly opposite, so most turns
are not right angles. **And a set of glyphs is a set to extend**: the first one-way street offering a turn and
a straight would want an eighth drawn by hand. So a branch is bent by the angle its turn was classified from
(`Spline.TurnedRad` of the movement's own line, the number `LaneLines.ConnectorKind` was decided by), and
nothing anywhere names *ahead-and-left* (TER-6a).

**The curl is sized by the lane and not the turn.** An authored radius has no answer for a shallow turn: at a
fixed small radius a thirty-degree branch leaves the bend early and runs straight off the side of the lane.

**The bar is laid once and read twice.** The arrow stands a setback behind it in the lane's own metres, the
figure `StopBars` placed the bar by, so the bar carries `AlongM` rather than the arrow projecting the bar's
centre back onto the lane and getting a centimetre's different answer over a bend.

**The glyph is anchored at its front.** Fixing the tail is the easier arithmetic and puts a turn-only arrow's
paint a metre and a half further back than the arrow beside it, a bend spending most of its run going
sideways — so a row of approaches reads as marks at four distances from four bars.

## 2026-09-19 — the walk hands the road two answers, and only the zebra follows the shorter one

A street too short to be crossed twice is crossed once midway (`WLK-10a`), and the first cut of that moved
everything hanging off a crossing with it: one band filed at both arms, so both lanes stopped behind a bar
in the middle of the street and the lane line had nothing left to be painted down. A bar is where a driver
holds for the box in front — a fact about the end of the street, not about where the paint went — so the
walk hands down two lists, `KerbEnds.HeldM` for where it is cut and `KerbEnds.CrossedM` for where it
crosses, the same list on every street but a welded one.

**The road reads both through one type** (`Crossings.Lay` of either span) rather than growing a second
registry of bands: two constructions of a band would disagree about a corner the week after either moved.
The paint is laid from the crossings; the bars and the lane line's trim from the stations.

**A bar with nothing in front of it falls back to the kerb end.** A station stands `Road.FootNodeClearM` out
from where the kerb ends, so a bar left at a station the paint has gone from holds a carriageway's width out
from its box. The second answer carries the kerb end at those arms, flagged as bearing no paint, and the road
takes it as a band of no depth — one figure, and the setback, the bar and the trim come out right without any
of them learning a second case. **Which leaves the dashes running under the stripes of a welded crossing**,
as they must: a run trimmed towards a band in its own middle is cut from both ends and no line is left.

## 2026-09-18 — a band's near edge is struck along the band's own axis and not along the lane behind it

A crossing's near edge was struck by stepping half the band's depth along **the lane's direction at the
lane's own end**, which is the band's own axis only where the arm has straightened by its junction. A band is
laid square to the walk that placed it (WLK-10), so which way it is deep holds wherever the paint stands;
struck along the lane, the step goes partly across the band and the bar lands out of its gap — 0.58 m once a
crossing stood tens of metres from the arm's end. **The lane is still asked which edge the traffic meets
first**, which is a sign and not a bearing and costs nothing however the arm bends. Two fitted test bounds
on the fixture grew with it; neither is a figure the claim leans on.

## 2026-09-17 — a junction's crossings are worked out a place at a time, and filed in place order

`LayCrossings` walked every junction on one thread — 91 ms of a town's standing cost, each place a question
about its own connectors' lines alone. It runs on every thread now, each carrying its own sample buffers
(`Placing`), and the slice's cost fell under 30 ms. **Sections are held per place and strung afterwards in
place order**: appended where found, their order would be a fact about the numbering rather than this loop,
and the next person to renumber anything would have to know it.

## 2026-09-16 — an arm's paint is one bundle, and `Crossings` is where it stands

The band, the bar behind it and the end of the dashes behind that are one bundle read off one answer.
`StopBars` asks `Crossings` where the band is rather than subtracting a depth from the lane's own metres —
over a bend a lane and its road run at different rates, and the bar came out centimetres out of the gap it
was meant to leave — and `CentrelineRuns.PaintedM` reads the same band. Rule 3 of the markings is that
relation stated: a bar on an arm with a crossing is placed by the crossing alone. **The registries are
derived and not planned**: `Crossings` and `StopBars` are laid off the plan the way `CentrelineRuns` is, so
the paint reads one answer.

## 2026-09-16 — a lane line is painted down a carriageway, and a carriageway is not a road

Under the old layer a road ran through many junctions and a run was a stretch cut out of one road. Under this
one a road ends at every node something meets it at, so a run is **several roads joined into the carriageway
they are pieces of** (`CentrelineRuns`).

**The car park is what decides it.** A lot is a junction cut into a street that already stood (`GEN-52`), so
a street with three lots down it is four roads; painted road by road it came out as four lines with a gap at
every lot and four dash phases, against rule 4 of the markings. The same is true of the two-armed nodes a
bridge, a loop or a refused join leaves, so the test is **what stands at the node** and never what put it
there: two road arms and any number of bays is one carriageway.

**The ground between two roads is crossed on the biarc the movements over it are drawn on** (`LaneLines`),
since the two lanes either side of it are that line offset half a lane each way; `GroundMeshTests` measures
the paint against it rather than taking it on trust.

## 2026-09-14 — the town does not lay the junction that decides nothing, so a lane never merges

**A merge could not be expressed in the graph the search runs on.** A lane carrying on through a node left
movements landing partway along the lane they joined; the contraction was meant to read that relation, and
[DrivingNetwork](../DrivingNetwork.cs)'s view of the lanes did not answer for it — so `LanePlaces` was
derived a second time, every merge silently unioning a node with the *start* of the run it joined, up to
`1132 m` away. The coarse graph refused the join outright (`TravelGraph.Builder.Join` holds links to meeting
end to end, which keeps the search's bound admissible), and a link that can be entered at two places is two
links.

**So the shape that needed the merge is not laid.** Only the scatter made it — a one-way street arriving at a
three-armed node leaves each two-way approach one movement — and `GEN-18` refuses that arrival. Odesa's `42`
merges and `17` lanes with one way out are `0` and `25`, all `25` a roundabout's entry (`GEN-19`) or a corner
every other movement was refused for (`GEN-48`) — places a driver really is committed.

**Four layers of rule went with it**: `TER-5j` and its bounds, the station a movement lands at, the places a
lane is *driven through*, the ground a merge takes off the lane it joins, the merge's own right of way, the
assembler's third figure and the census row. **The contraction does that work** — it ends a link where a body
can go more than one way. **And the places are worked out once and handed over** (`SIM-7`):
`RunNetwork.Contract` takes them rather than deriving its own, which is what let the two answers disagree.

## 2026-09-07 — what the road *is* and what it hands out are two documents

One page carried nine sections and eleven thousand words, against a rule that a document covering eight
things is eight documents. The seam is that half of it described the network — a road, a junction, a
crossing, the paint — and half described a protocol over it, so `TER-4c`, `TER-5c`, `TER-5e` and `TER-5g`
went to [claims.md](claims.md) and nothing else moved or changed. Split by section instead, the claim
ladder would have been read without the rule it ranks.

## 2026-09-06 — a movement is as wide as the narrower lane it joins, and one figure says so

Three readers each took a movement's width off the lane it arrives on, which is fine while every lane in a
town is the same width — on a hand-authored map where a four-metre street meets a five-metre one it put
half a metre of tarmac past the narrow street's kerb. The narrower of the two lanes is the only width a
single band can have that never claims ground outside either arm (TER-5d.1), answered once by
`LaneLines.ConnectorWidthM`.

## 2026-09-06 — the graph reads the lines rather than drawing them

`RoadGraph` cut the roads, offset the lanes and drew every connector, and then laid the rules over what it
had drawn. Only the second half is this slice's: the first is the town's own geometry, wanted as much by
the ground as by the traffic, and it is `CityGen.LaneLines` now. What the move buys is that the tarmac and
the network cannot disagree — the question of whether the surface and the graph were laid to the same
figures cannot be asked.

## 2026-09-05 — a connector is an object, and where lanes meet is worked out from them

A movement was a lane paired with a slot index, and the pair travelled together because neither number
meant anything alone; a connector is the id and answers all of it. The plan's node table is gone from the
graph the town runs on: two lane ends are one place when a connector runs between them, or when they are
the two ends of one stretch driven either way, and `LanePlaces` works that out once so the contraction and
the claims cannot disagree about which lane ends are one piece of the world. The second clause is not
tidiness — no box admits the turn-around (TER-5f), so joined only by connectors a dead end's two lanes
would be different places and a leg could not be priced round a bay (GEN-4l). A spatial index over every
way was the alternative and needs no places at all, refused because the span a caller must give the walk
then has no bound the town can state.

## 2026-09-05 — a one-way road is a narrower road, and a corner is solved on the pair it stands between

The lane graph was already directed, so a one-way road needed only one lane laid instead of two; a
full-width road with a lane nothing may enter is a rule every drawer, claimer and walker has to be told
about. Which way it runs is carried and never inferred, since a narrow road is not necessarily a one-way
one. Laid down the middle of its own line it was a narrowing rather than a street, so every car through the
junction stepped sideways: it stands on the half it is driven, moved after the bends and never before them,
and a node with no fork goes with its arms. What broke was the junction, and it was already broken — every
corner was solved as if both arms were the same width, which until then they always were. A corner is the
crossing of the two kerb lines each offset by its own road's half, and whether one exists at all is
measured off the narrower arm.

## 2026-09-04 — one table of ways, and no metre of one in two claims

Two `LaneOccupancy` instances in two numbering spaces meant a car parked across a footway and the walker
coming down it each held the same ground, and each book was right about itself. There is one table now
(`TownWays`, TER-4c.2), and the kind is a property of the ground: it decides how wide the ground is and what
must stand clear of its line, never who may claim. The claims on a way are disjoint by construction
(TER-4c.3): the insertion had been a splice that never looked at what was there, and exclusivity lived only
in the grant passes, which only shortened the asker's own far edge. Which of a pair gives up the ground had
to be decided by the body and not the rank, or two bodies of one rank are settled by whichever went in first.
A refused ask is the one exemption, being a mark and not a hold. **Still open**: a stretch laid from a pose
that reaches past a body — truncated, the far half is ground its holder is committed to and nobody holds;
laid as a second run it breaks `Withdraw`, `CutTo` and `AlreadyHolds`, so it needs TER-5c.2 opened first.

## 2026-09-04 — a way has one property, and it is a claim

A use said how a stretch had been measured and a rank how strong the hold was — two words for one row, and
two of the seven uses meant the same thing as a strength. A way is used when there is a claim on it: who,
where and at what priority (TER-5g). Everything a reader used to get from the use is worked out from those
(`Counts`), except whether the body is following this way's line or merely standing on it, which turns the
margin (`OnItsLine`).

## 2026-09-04 — a driver states the road it means to use, at a strength anything stronger can take

Every driver could read the others' commitments a reaction interval out and none declared its own, so a car
pulling out of a side road in front of one coming at speed was refused by nothing, because nothing said it
was coming. The read/write asymmetry was the defect rather than the length of either half, and it left the
ladder almost nothing to rank. A car now lays a second stretch beyond the one it is committed to, and **the
holding time is what tells a plan from a commitment** — at the decision interval the stated claim is
arithmetically the committed one for any car already doing the speed it plans for. That is the ask the
committed claim deliberately is not (2026-08-22, below); the whole difference is that this one can be taken.
A tie does not refuse, since a stated claim is laid by everybody in the same rebuild. A body that is not
moving states nothing — sized from a standstill it is the pull-away horizon of every queue in the town, and
the weaker movement in seven exam cards never went at all. The rank a car asks a box's ground with had to
become the rank it holds it at. The signals needed no change: a red is already a stop point the ask is
clamped by.

## 2026-09-03 — a lane is the ground it is walked over, and the corners come off the line

A pavement lane was its whole offset line with each corner's ground remembered against it as margins, so a
metre or two at both ends of every lane was on the lane and on nothing else — real, since a body standing
inside a corner claimed it, and drawable only as a line ending in mid-pavement. The margins come off the
line itself, so a mitre sets off from the arriving lane's last point and lands on the onward lane's first.
Two ends of a short stretch could want the whole of it, so they are held back in proportion to leave the
lane four fifths of itself.

## 2026-09-03 — turning round on the spot lays no ground

`LayJoins` laid a mitre for every way out of a node including a stretch's own reverse — 7292 on Odesa
carrying 22.9 km — and nothing else in the network treated it as a way. That made it a way with claims on
it and no line anywhere, so the claims layer drew a fan of blocks the nodes layer had nothing to draw,
which is exactly what OBS-2d forbids. It also spent slots. The turn stays in the table, since a walk at a
dead end needs it, and lays nothing.

## 2026-09-03 — a walker is laid on a car's terms, and on nothing of its own

`StandInTheRoad` asked the terrain grid before the geometry, and the grid answers to a metre cell — so a
walker a stride into the road claimed no lane. A band is what says which way a body is on, for a car and for
a walker; the grid is what a body drives and walks *over* and was never fit to say where one is. The claim was
also widened by a person's own margin on top of the two metres a driver's `LaneCredit` already stops short
of, which is `SIM-7` exactly: the margin belongs to whoever is doing the braking.

## 2026-09-02 — a body holds the ground it stands on, and what to call it is the reader's

The write was gated on `Driven && !Broken` — the reader's conclusion deciding whether the fact got written —
so a car under a hand sitting in a box was ground every crossing driver read as empty. Every body writes the
space it occupies (TER-4c.2). The box is projected and not approximated, since a body askew reaches its own
length across the way beside it and its own width along the way under it. A body is on a way once it touches
it and the stretch says how far aside it stands: withheld until it obstructed the band, a car straddling the
line was written onto neither lane. What it covers is the box clipped to the band and not its shadow — an
angled car reaching the next lane by nineteen centimetres held four metres of it, a fact eleven times too big
and a lane shut by a wing mirror. A sweep is two poses and not a corridor, or a car swinging into a bay holds
seven and a half metres of lane for a body under four. The line is crossed rather than touched, the allowance
across and not along. A car on a tow bar laid nothing at all, the reasoning having held only for the ways of
the hauler's own line, and the walk stopped at a node instead of crossing it — both the walk asking about a
body's middle rather than the body. And half the rule applied to half the town: TER-5c.1, a fact about a
zebra, had hardened into a fact about the body, so a car that mounted a kerb stood on a footway nothing
walking there could see. A body is written onto whichever network it is standing on, which wanted a name for
the shape — `IWayNetwork`, taken as a generic argument because a virtual call per way is the town's hottest
path. Naming the shape found a third network in the bays.

## 2026-09-01 — a zebra spans the road it names, and carries no span of its own

A crossing carried a span every planner filled with the width of the carriageway it was laying, so the field
agreed with the road until something laid one of the two again. The reach is solved and never carried
(TER-6). The depth stays the crossing's own, since how much of a road's length the paint covers is nothing
the road decides. The skew is part of the relation and not an exception: `Zebras`' off-square crossing was
8.83 m of an 8.00 m road, which is what the file held and what the derivation gave without being told.

## 2026-08-29 — a grant is a distance in front of the nose, and a claim behind it is not a cut

A car would stop halfway out of a box with the road ahead empty, its grant minus seven metres — negative
road, which inverts to zero whatever is ahead and which nothing can hand back. A way the nose had left was
still being asked, and the stretch reaching it was the queueing car's claim laid from the leader's near edge,
answering the leader from underneath its own body. A claim is ground its holder has not reached (TER-5e), so
one the asker is standing on is ground the asker has. What may still answer from behind is a body: a wreck
reaching back past the nose is an overlap, and the grant is left free to come back negative and say so.
**And a body level with the asker is not in front of it**: three crowds once queued behind a pair each granted
minus two metres and each the other's cut, the body whose front is exactly the asker's being neither in front
of nor behind anybody. `GrantedOn` passes over both.

## 2026-08-29 — the claim holds the answer, and one metre is one body's

The code granted correctly and then threw the answer away, leaving the ask standing as the claim — so every
other reader for the rest of the tick read the *question*, and a car held at a red still held the sweep of
road beyond it. Two bodies held one metre by as much as 13.72 m on Fleet. `CutTheGroundToTheGrant` is a walk
of its own because it moves far edges, which is what a movement's crossing question reads; on a join a car is
crossing, the seam moves and the union does not, so `ClaimWhatTheAnswerTook` hands the metres over as a
claim. And the credit went with it: a stretch worth its holder's stopping distance is, once the answer is
written back, two bodies holding one metre. What it buys is the junction — Odesa abandons 6 cars against 15.
What it costs is station-keeping at speed, and one proving-ground claim is broken and left broken, because
papering over it in the rig would be measuring the ruler.

## 2026-08-29 — the box refuses a car at a place, and only lets it in where it can wait clear

The gate answered *whether* and the grant answered *where*, the same question at two resolutions, so a body
on a box's far corner held the near half against a car that would never have reached it. The gate answers in
metres on the same figure, and the body margin keeps it from deadlocking — a car held a margin short claims
no metre of the section, so the crossing movement still reads it free. Until the second half went in it
stranded cars in the box, so a car is only let in as far as it can come to rest with its whole body in a gap
between the runs (`WaitsClearOfTheCrossings`).

## 2026-08-28 — the walkers claim the road before the grants

The walkers claimed the road as the *last* pass of the rebuild, after every grant had been taken off it, so no
driver ever read a band while deciding how much road it had. Their claims go in before the grants. River went
from two knocked down and two wrecked to none of either.

## 2026-08-28 — what a body is written onto and what a manoeuvre reads are one walk

Reading the ground under a template asked the nearest lane and stopped, so a car written on a join was
invisible to every swerve and bay exit swinging through the same box. The claims were not wrong; nobody was
asking them. There is one walk (`GroundUnder`) and both sides call it. It costs the town its reactive
templates where the ground is genuinely somebody's, which is the finding rather than a side effect: reversing
into a junction is reversing into ground the traffic crossing it is committed to, so the ladder escalates
instead.

## 2026-08-28 — a claim is answered every tick, and its holder is told when it loses

A claim was answered once and re-laid unread for as long as the entry wanted it — the one hold that
remembered an answer — so a right of way took the ground and nothing said so, and the pair drove at the same
metres from opposite sides. It is answered again after every body has claimed and before anything is granted
off it, and the holder is told, because the only thing that knows what a claim was for is the entry that took
it. Only a stronger rung takes a claim: giving one back for a body standing on the ground is the duplicate
SIM-7 is about, and refuses the one thing a claim is for, the stretch a movement claims being by construction
the one containing the body it is driven over.

## 2026-08-27 — a junction admits no movement that reverses the direction of travel

The turn-around was in the table from the beginning and drivable by nothing — two opposing lanes join on a
1.5 m semicircle — and was classified, laid, measured, ranked and priced at infinity, which is a great deal
of machinery to say *never*. It is gone (TER-5f), and a quarter of Odesa's 1472 movements went with it. What
a route may still do is come back down the other side of one stretch in a car park's bay, priced rather than
joined (`GEN-4l`).

## 2026-08-27 — an obstruction is a claim that generally reaches nowhere

A body the road was not driving held the metres under it and nothing else — right for a wreck standing in a
lane and wrong for the same wreck two seconds earlier, in the direction that costs. Such a body lays its own
stopping distance past where it stands, from the speed it actually has, so nothing is a special case: it is
the same arithmetic a driver's claim is, asked of a body with nowhere to go. Where the body is sweeping a
template the sweep is that ground and is already laid. The measurement threw out the tidier version — a
margin behind the stretch reads better and is a fatter body in *every* question asked of the claims, taking
Odesa 69 touches to 88.

## 2026-08-27 — the grant is a question the claims answer, asked once

The road's grant and the pavement's were the same forty lines twice, both switching on the use to decide the
credit and both making a second cut at a place with the same margin subtracted by hand. The grant is
`LaneOccupancy.GrantedOn` and what the asker brings is `LaneCredit`, so the credit rule and `Binds` exist
once. It changes no arithmetic: the map from a way's metres back to the line is affine and increasing. Two
smaller things fell out — a rank is a floor on the walk rather than a filter over what came back, and
`Nobody` no longer matches itself, which had been excluding every bollard in the town from its own answer.

## 2026-08-25 — where a road's paint breaks is the road's answer, not the drawing's

Dashes were laid by walking each road and asking whether a point was inside a disc or on a zebra, so
every arm was dashed right up to the mouth of the box, past the bar a driver stops at; the metre step was the
smaller fault. The boundaries are not looked for any more — `CentrelineRuns` takes them from whoever measured
them.

## 2026-08-25 — the right of way is carried by the stretch, and it takes claims and nothing else

Two crossing movements each read the other's ground and each were cut at it, so a junction went to whichever
asked first — the order the rebuild happens to walk the cars in. A table of pairs was considered and is the
verdict TER-5c exists to avoid: it answers *may I go* for a whole junction and says nothing about where.
Carried by the stretch, the comparison sits exactly where two pieces of ground meet. What makes it safe rather
than merely one-sided is that it takes a claim and never a body — a committed claim is the road a body needs
to stop in, and taking that is a licence to drive into whoever holds it. A car past the point it could stop
lays the same claim at the rung nothing outranks. Revocation is the same fact read the other way and is
bounded the same way; both happen inside one walk, so *already crossing* is measured from before the walk
and not from the fleet the tick leaves behind.

## 2026-08-25 — an inline junction's crossing is laid across the lanes at the node

The one thing TER-5b says an inline junction exists for did not work: the paint stands on the node itself,
further from every lane's end than the paint is wide, so the projection found no lane and a walker on it was
invisible to the traffic — hidden because every such crossing in the shipped towns was lit. It is laid
across the lanes that meet at the node, each at its own end (`LaneFurniture`), and that fallback is taken
only where the projection found nothing *and* the junction admits no turns.

## 2026-08-24 — the town's furniture is a claim nobody owns, and not an occupant number

A bollard was claimed as an obstruction belonging to `Nobody`, which is also the integer a query names when
the asker has claimed nothing — so the walkers' traffic questions skipped the furniture because the exclusion
they asked with happened to name it: the right answers, reached by one question deciding another. A prop is
in neither roster (`LaneClaim.IsFurniture`); nothing about the town moved, only where the answer comes from.

## 2026-08-24 — the table of crossings is indexed by way, so a way laid off a junction can use it

The table said which *movement* took ground off which, right while the only ways that could overlap were the
joins through one box. A bay's way in leaves its lane part-way along and sweeps the lane running back, so what
it takes ground off is a **lane**; a second table for bays is the duplicate SIM-7 is about. A section names a
way, the table is laid over every numbered way, and `LineOverlap` is lifted out so the ways at a bay are
measured by the code that measures the joins. What it cost was the whole-way fallback — near enough between
two joins a dozen metres long, the whole street against a two-hundred-metre lane. The missing end is now the
found one's shadow.

## 2026-08-23 — a template holds the ground it sweeps, and not the pose it is passing through

A car driving geometry of its own claimed the footprint it stood on and nothing more, so the line it was
about to drive was left open — every other driver read it as free road and could come to rest in it. Odesa
found it as two wrecks a minute. Such a body is laid over the whole sweep its line has still to make, read
from both ends and laid once, since the ways under one end are regularly not the ways under the other. What
it does not do is make the reverser see: what stops the collision is that nobody else is granted the ground,
which is the same mechanism and not a second one (SIM-7). Odesa after it: 0 wrecked and 46 touches against 4
and 56.

## 2026-08-23 — the tail keeps a share of the margin, not the whole of it

The margin sits at both ends of a claim and the two ends are not paid for by the same traffic: in front it is
this car's own cover, and behind it costs whoever comes up behind — every metre is road the follower is queued
out of. The tail keeps a share, and the standing gap at rest falls to 1.2 m, which was never the follower's to
choose. What it cost was wrecks, and they were not the margin's: the step is at the first metre under a
body's width and does not deepen below it, and every wreck was a back-off reversing into a car stopped inside
its straight. With the template hole above closed, 0.6 runs 0 wrecked and 46 touches — fewer
than the full margin gave.

## 2026-08-23 — one body, one stretch: the margin is part of the claim

A car in a junction held a release margin behind its tail as a claim of its own: two occupants to every walk
of the join, and only true *in junctions* though nothing about the reason is. The margin is in the body's own
claim on every way the body is on, and the release figure and the follower's standstill gap are one figure —
the ground a body keeps around itself. The measurement is kept as a floor rather than a second figure, so a
fleet tuned to queue closer than the soak's floor gets the floor; what makes one figure safe here is that the
*union* is taken. *In front* stopped meaning "its near edge is", since every stretch now begins a margin
behind its owner.

## 2026-08-23 — a claim stops where a rule stops the car

`AskForTheGround` clamped the road at the place the car is held and then added the margin on top of the
clamp, so a car waiting for a zebra held a metre of the zebra — and a signalled crossing behaved like an
unsignalled one, the people getting over on their patience eight seconds later. The gap is part of what the
car asks for and is clamped with the rest. Nothing about following changes, and the ask only ever shrinks at a
stop.

## 2026-08-23 — a car claims the ways it drives and looks up the ways it is driven over

A movement wrote its crossing points onto both joins, half of which is a body claiming ground it is never
going to be on. It is a lookup: the table was already symmetric and carried both ends of every section. The
crossing claim on the mover's own join stays, since a driver's road ahead is a braking distance and does not
reach the middle of a box — two cars from opposite arms would each find the other's join empty. The grant is
where this had to bite, and not just the commit test.

## 2026-08-23 — the margin a body keeps is not the clearance the sections are drawn at

They take the same value and answer different questions — how near two lines pass before they are driven over
each other, and what a one-dimensional reading of a two-dimensional body owes whoever comes next. Read as one
figure they read as one rule, which hid that only the first had ever been measured.

## 2026-08-23 — a body is on a way across its band and along it

Whether a body stands on a way was a lateral question only, and a projection is clamped to the way's ends — so
anything lined up with an end answered at that end however far up the road it stood, and one car in a box
could shut movements on the far side of it. The test is taken along the line as well, against how far the body
reaches; inside a way it decides nothing, and it bites only where the clamp did.

## 2026-08-23 — a junction is committed to at the rate the car actually brakes at

The claim distance and the point past which a crossing is kept were the only stopping distances in the town
read off the pedal's cap, while every stretch of road is sized by the follower's braking figure. The cap is
larger, so both erred the way that costs: a car past the point it could stop gave the sections back for a bar
it was going to cross anyway, and in between two ticks they read free. Neither noticed wet ground, where the
gap is widest.

## 2026-08-23 — a crossing is given back where it is passed

Ground taken for a junction was held until the car was out the far side, which on screen is a car half way
through a turn still washing the corner it came in by. A section is a place and a body passes a place once, so
a car gives one back when its own tail is a clearance beyond it. The tail alone is a metre too eager and it
wrecks cars — a section is drawn where two *lines* pass, and what has to be off it is a body, which on a turn
swings wider at the back. The car's own committed claim is not released with them: it looks like the same fact
and is not, and slid forward with the tail, Odesa's touching count went 51 → 232.

## 2026-08-23 — a junction is refused by ground, not by a verdict

Nothing drew the registry or the conflict relation, so what actually stopped a car at a junction was
invisible. Worse, the relation was almost complete: an average movement conflicted with 81 % of the others at
its junction, three quarters of it asserted rather than measured. It is ground now and only ground — per
movement, the section of every other join its own line is driven over — and it can be looked at on screen.
Three things had to be true, each costing a defect to find: a car crossing must hold its own join, since its
road ahead does not reach where two lines meet; a car making the same movement is not an answer, or every
queue refused its own second car; and the two out of one lane and the two merging into one are not in it at
all, both being the duplicate SIM-7 is about. Movements that shut a whole junction on their own: 416 → 0.

## 2026-08-23 — a stretch runs out at the box's near edge

`WaysAlong` stopped walking when the *next lane* began, and the next lane begins on the far side of the
junction — so nothing was laid on a junction until the stretch reached clear across it. A car approaching a
box claimed none of it, was granted its road as though the box were empty, and could see nothing standing in
it. The guard is the near edge now.

## 2026-08-23 — a way through is kept until it is given back

The claim was not a claim: every tick recomputed whether the car was *entitled* to the movement it was already
making, from two figures that move under a car merely slowing down — so a driver easing off found its own
junction refused it. Worse, nothing wrote the field away, so an arm sitting at a red refused the arm the phase
had just given the green to. It is one state, held by the car and laid from the car, taken only by a driver
nothing but the box is holding up. Past the point it could have stopped it is kept whatever anything says,
because ground given back there is handed straight back next tick and in between the sections read free to
whoever crosses them.

## 2026-08-22 — a body is one stretch of a way, never two

A driver under way was laid twice on the same ground, and the two shared a near edge exactly, so every walk of
a way counted one car as two occupants. Nothing computed a wrong answer; what it cost was that a claim could no
longer be read as what it says it is. It is one stretch carrying two far edges — `ToM` is the ground taken and
`StandsToM` is where the body ends — and for everything that is only a body the two are equal, so the
distinction costs nothing to lay and nothing to ask.

## 2026-08-22 — neither network's claims are one roster's

A walker on a crossing claimed the road and was in none of the road's questions — half right, since a walker
read as an obstruction is one a driver is held off, and one read as a committed claim cuts a car three lanes
away. What that cost was invisible until the ray went: nothing cut a driver's road at a body standing in it. A
body on foot is in every query a grant is taken against and carries its own reading, so what it must never be
is a property of which query happened to skip it. An occupant is an index into one of two rosters and the
stretch has to carry which, or the first walker whose index matched a car's is read out of the wrong fleet. The
walker's give-way arithmetic went with it: the claim *is* that arithmetic, already done, from fresher numbers.

## 2026-08-22 — a crossing is ground, and it is taken by the band

A zebra was treated as a unit when it is a strip of carriageway a lane at a time, and both readings were wrong
the same way. The car's half was inert as well as coarse: laid from the crossing way's own start, every such
stretch sat behind every walker that could have been cut at one — 562,782 claims over a minute of Odesa and not
one grant cut. The walker's half was live and over-held, taking the band of every lane the crossing crosses:
6,003 of 7,921 crossing stops were for somebody not in that driver's lane. So the missing projection was built
(`CrossingBands`): what either side has of a zebra is a band and never the whole of it, and a band's near edge
is a place on the ground rather than the start of a way, which is what makes a grant cut at it at all.

## 2026-08-22 — braking has its own margin, and it is nearly all of the grip

Using the cornering margin for braking put the planned stop at 13.1 m/s² against the 21 the tyres actually
delivered, and every claim is sized by the planned figure — so a car held half again as much street as its
stop was going to use. A corner is held for as long as it lasts and its margin covers a bump, a camber and
the wheel still being turned; a stop is aimed at, straight, and over in seconds. Corner speeds are
untouched, which is the point of the figure being its own.

## 2026-08-22 — a claim is the ground a car is committed to, not the ground its plan would need

Asking for the whole stopping distance from the speed the profile was driving towards came out at a few tens
of metres on a town street and 215 m on open road — a quarter-kilometre of empty straight held by a car doing
a third of that speed. The ask is what the car cannot undo: one reaction interval of ground and a stop from
there, with the profile's figure as the ceiling. It still leaves room to pull away, since what it asks for
grows with the pedal rather than with the speed the pedal has produced. And a car nothing cut is held by
nobody: against an ask the car is merely committed to, the grant inverts to the speed one reaction interval
reaches, so a car alone on an empty straight read as `queueing`, behind itself.
