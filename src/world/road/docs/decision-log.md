# Decision log — roads and junctions

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md) and [claims.md](claims.md); how
a type works is its own XML docs.

## 2026-09-27 — a car refused in a box waits there, and nothing moves the answer

The owner ruled the flow pure: a patch beside it goes, and what the flow gets wrong is fixed in it. So
**`WaitsClearOfTheBoxes` went** (TER-4c.1). A car refused by a plan where it would rest on ground its join
shares with any way was sent back to the mouth. It judged from the marks, not the claims, and the ground it
kept a car off was often ground nobody held. It cut at a place nothing stood, as `LaneClaim.Nothing`, so
`TheBoxGiven` and the stuck probe each had to handle a cut with no cause. It spared cars cut by a body. And it
judged "can still stop" by a third stopping distance. `WayCrossings.OwnRuns` went with it, read by nothing
else.

**Measured** over six agent seeds of a minute each, the rule against none:

| | Odesa | River |
|---|---|---|
| wrecked | 27 → 37 | 52 → 46 |
| touches | 214 → 202 | 226 → 224 |
| km driven | 2417 → 2409 | 1980 → 1955 |
| walks given up | 450 → 472 | 405 → 407 |
| car-ticks at rest inside a box | 178 k → 184 k | 242 k → 268 k |
| rebuilds left unsettled | 15 213 → 17 786 | 16 201 → 20 852 |

The wrecks move both ways, inside a spread of 0 to 16 a seed. The three crashes of the Odesa seed that went
0 → 6 involve no car waiting in a box. A walker steps onto a zebra three metres in front of a car at 13 m/s.
Two cars on the two lanes of one road meet as shapes, off each other's ribbons. A car running wide on a corner
at 29 m/s hits one standing off its line. What does move is the waiting in boxes, and with it the rebuilds the
settling leaves at its bound. The exam is 108 of 109 cards either way, card 59 failing on the same 0.2 s.

## 2026-09-27 — a claim is main or secondary, and a main claim reads its own way alone

The owner named the claim a holder places on a way its own crosses a **secondary claim**, and ruled that
**two secondary claims never meet**: main against main on one way, and main against secondary, are the only
meetings. Placing a claim reads the main way alone, and the secondary claims go down with it (TER-5c.1).

**The reads of the other way went, because they were a second copy of one answer.** `Reach` walked each
mark's far way for main claims over its section (`LosesTheSection`), and `Take` cut those main claims when it
placed the section. A mark is filed under both its ways, so each such main claim had already placed a
secondary claim over the asker's own way. The ordinary walk of that way met it at the same metre, on the
same terms. The one term that moved is whether a holder's body stands on the ground: it is now read off
the asker's way rather than the far one. Taking that secondary claim had already cut its holder short of the
mark, too. **So the atlas files a pair only where both sides hold ground**: a side clamped to nothing would
take a secondary claim and place none back. `Beats` lost its second metre, since every meeting is on the
asker's way.

**Measured**: the exam is 108 of 109 cards before and after, card 59 failing both times on the same 0.2 s;
`qq tests all` passes, the gates included.

## 2026-09-27 — a ribbon is its way's own width, and a mark is worked out from the ribbons

The owner ruled that **a ribbon is its way's own width** — a lane's, a join's, a pavement's — and that there
is no ribbon at a car's width; and that **a mark's section runs as far as the two ribbons share ground, up to
where they are almost touching** (TER-4c.4, TER-5c). On the overlay a join's marked section had stopped about
a metre inside the lane it crossed: the block was drawn at the lane's width, and the section had been cut
from ribbons a car wide, sampled on the lattice.

**Marks are worked out from the ribbons and not from the lattice** (`RibbonMarks`, `RibbonPiece`). A piece of
a way's line swept to its width is a rectangle or a sector of an annulus; a metre of one meets another's
ground where the slice across it does, and each end of a section is walked for a lattice step at a time and
then halved to under a micrometre. Lane-width ribbons had failed before on the lattice's reach: laid that much
wider, the two lanes of a carriageway overlapped by 0.7 m and every pair of joins out of neighbouring lanes
was marked. Worked from the ribbons, ribbons laid edge to edge only touch — on Odesa, joins marked against
joins went 71 833 → 72 411, and lanes against joins 1 920 → 12, since a hand-over is no longer one.

**Shared ground is ground deeper than the touch inside both**, each ribbon worn back by it at the sides and at
the square ends, and a section runs over all of that and no further. Run to where the ribbons last touched, a
2 m pavement edge stepping onto a zebra's kerb end at an angle — its square end dipping a few centimetres
into the lane — was marked whole, and a car passing locked the pavement. Worn, those 1 050 pairs on Odesa
share 0.22 m of pavement edge on average and 0.90 m at most.

**A body is read over only ground it and the band both hold.** The lattice still files a point a reach past
every band, now with how far outside the band it stands, and a point counts where the collider reaches
further past it than that. At lane width the reach alone put a car 0.45 m off its line on the next lane.

**Measured** on Odesa over six agent seeds of a minute each (`--bench soak`), against the tree before:
knocked down 2 → 3, wrecked 34 → 34, touches 183 → 191, walks given up 269 → 448, km driven 2533 → 2415. Taken
while sections still ran to the touch, the same marks and body reading at the old widths gave 22 wrecked, 186
touches, 265 walks given up and 2561 km, and driven ways alone at their own width 35, 251, 360 and 2439 km:
what costs is the width, most of it on the driven side. The atlas on Odesa is 3.5 M points and 6.3 M
entries — 54 MiB — laid in 0.75 s, against 2.7 M, 3.9 M, 30 MiB and 0.7 s.

## 2026-09-26 — what a cut frees goes back inside the tick

Plans were laid once, in the same order every tick — cars by index, then closures, then walkers — and a cut
only ever shortened a hold. Two faults came of it, and both held for as long as the town stood the same way,
not for one tick.

- **Refused by ground a later cut took away.** Car A was cut by car B at a box, then a walker on a zebra cut B
  back short of the box. A still waited on ground nobody held.
- **Refused by a piece its own taking removed.** A hold is answered way by way before it is laid. A straight
  movement beat a near-side turn at the mark where their joins merge, and lost to it on the exit lane on
  arrival. Its taking at the merge then removed the turn's exit-lane piece, and the straight car still braked
  at the far edge of the box.

**Every plan another plan ended is now answered again once all are down, and laid again where the answer
moved** (`TownWorld.SettleThePlans`, `LaneOccupancy.ReopenHold`). Chosen over laying the walkers and closures
first, which would have fixed only the commonest chain.

**The passes are bounded at four, and the bound is not a gate.** Nearly everything moves in the first pass; at
two, four and eight passes the same seeds give the same crashes and walks. Rebuilds still moving at four
are still moving at eight. They are rings, where each holder takes from the next, and whether a town has them
depends on its junctions. On Odesa, over six seeds of a minute each, 0 to 1824 of 4200 rebuilds hit the bound.

**Measured** on Odesa over those six seeds, against the tree before:

- km driven: 2100 → 2542
- walks given up: 316 → 269
- wrecked: 30 → 34 (1.43 → 1.34 per 100 km)
- touches: 195 → 183
- car-ticks spent past the braking margin, per km: 1.52 → 1.53

Five minutes of `--bench stuck`:

- drive legs given up: 603 → 354
- walks given up: 1011 → 721
- cars that ever stood still: 403 → 264
- in that one run, wrecked 14 → 18 and touches 265 → 355

The exam went from 26 failed cards to 23. Cards 20, 50, 92 and 93 now pass. Card 15 now fails — a straight
giving way to a left turn from its right, which the ladder does not state.

## 2026-09-26 — two layers over a ribbon atlas: where the bodies are, and where they mean to be

The owner set the reservations three rules. **A body reserves, at p0, every ribbon its collider overlaps,
and two such reservations may overlap**, being a record of real things. **A plan is settled against its own
way and, through marks laid when the town is, against every ribbon it shares ground with** — the existing
comparison deciding whether it takes the other's ground or cuts itself to the free space — **with no geometry
at runtime**. **And those are the only two ways ordinary agents meet.** Everything in
[claims.md](claims.md) was rewritten to them.

**The atlas is laid once** (TER-4c.4, `RibbonAtlas`): every way's ribbon sampled onto a lattice a quarter
of a car apart, each point knowing which ways it lies under and how far along each. It replaced a per-body
walk of the nearest lanes with a band test every tick, a crossing table measured from centrelines for joins
alone, a zebra's bands projected while the town ran, and the furniture's claims: which ways a collider is over
is now a look-up of the points inside it. **Joins that diverge from one lane or merge into one are marked**,
their ribbons sharing their first or last metres: that ground is one piece of the world and two bodies cannot
both be on it.

**Ground a car can no longer stop short of became the first term of the comparison, not a p0 claim** — p0
is the collider and nothing else. The ladder was redrawn round it (TER-5g): body, committed, call, closure,
crossing, then the three movements. **The stated band, `Reserved` and `Rejected` went**: a plan is one hold
from the nose, answered before it is laid, so there is no weaker band to tell apart from it.

**The comparison is committed, standing, rung, box already given, arrival** (TER-5e), and each term is a
fault it closed. **Standing**: a walker on the paint and a car whose secondary claim lay over it each waited
for the other. **A box already given**: a tie-break on the metre each hold entered the box at jumped as cars
passed marks, and boxes changed hands under cars on their way in — into crashes. **Arrival alone between two
holders that can no longer stop**, since the one further off has road left to brake on and the box it was
given last time is no reason to drive into the car already there.

**A plan is never laid past what its answer gave** (`PlanTheDrive`). A car's refusal placed at its committed
distance instead rode ahead of the car at exactly its stopping distance: a car refused a box held 19 m/s into
it, and the plan laid past its answer took the box from the car that had won it.

**A light is infrastructure and clips the plan** (TER-4c.5): a red is where the plan ends, so nothing waiting
at one holds the box beyond it, and `JunctionStopM` reads the light and nothing else.

**What is outside the two layers is named** (TER-4c.5): placing a body leaving a building (`ExitSpots`, a
placement and not a decision), route-cost memory (`LinkSurcharges`, now one table per network — the walking
router had been pricing its links off the driving table by id), building capacity, and the special agents.

**The owner's open decisions were taken at the plan's recommendations**: the new rules at P3, not P0 or P1;
committed ground as a plan nothing takes; a prop on a driven ribbon refused when the town is laid; the
crossing's rung above every movement and below committed ground; lights as infrastructure; route cost and
capacity exempt by name; a bay's ways planned like any other when bays return; and a body's own place on its
own line as its control rather than the reservation path.

**Tried and taken out**: extending a car's committed ground through the box it was in, which handed every car
creeping through a junction absolute priority and put them into each other.

**Measured** on Odesa over six agent seeds of a minute each, against the tree before: wrecked 34 → 26, knocked
down 14 → 2, touches 2719 → 187, walks given up 666 → 316, 1878 → 2092 km driven; over five minutes of
`--bench stuck`, touches 3799 → 265 and walks arrived 72 → 170. Two faults outside this slice were fixed on the
way and carry much of it — a corner the look-ahead reached a wheel's lookahead late
([agents/car](../../../agents/car/control/CarFollower.cs)), and a walk that ran out on a corner of no length
([agents/person](../../../agents/person/docs/decision-log.md)).

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- **TER-5**: a junction's radius is the connection standoff (a car park's, its rank's) and is not sized on
  the arms; nothing measures from it.
- **TER-6**: a town's zebras stand at the kerb ends' stations, not at pedestrian node pairs, spanning the
  carriageway's two edges; a box that forks nothing carries none.

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

## 2026-09-01 — a zebra spans the road it names, and carries no span of its own

A crossing carried a span every planner filled with the width of the carriageway it was laying, so the field
agreed with the road until something laid one of the two again. The reach is solved and never carried
(TER-6). The depth stays the crossing's own, since how much of a road's length the paint covers is nothing
the road decides. The skew is part of the relation and not an exception: `Zebras`' off-square crossing was
8.83 m of an 8.00 m road, which is what the file held and what the derivation gave without being told.

## 2026-08-29 — the box refuses a car at a place

The gate answered *whether* and the grant answered *where*, the same question at two resolutions, so a body
on a box's far corner held the near half against a car that would never have reached it. The gate answers in
metres on the same figure, and the body margin keeps it from deadlocking — a car held a margin short claims
no metre of the section, so the crossing movement still reads it free.

## 2026-08-28 — the walkers claim the road before the grants

The walkers claimed the road as the *last* pass of the rebuild, after every grant had been taken off it, so no
driver ever read a band while deciding how much road it had. Their claims go in before the grants. River went
from two knocked down and two wrecked to none of either.

## 2026-08-27 — a junction admits no movement that reverses the direction of travel

The turn-around was in the table from the beginning and drivable by nothing — two opposing lanes join on a
1.5 m semicircle — and was classified, laid, measured, ranked and priced at infinity, which is a great deal
of machinery to say *never*. It is gone (TER-5f), and a quarter of Odesa's 1472 movements went with it. What
a route may still do is come back down the other side of one stretch in a car park's bay, priced rather than
joined (`GEN-4l`).

## 2026-08-25 — where a road's paint breaks is the road's answer, not the drawing's

Dashes were laid by walking each road and asking whether a point was inside a disc or on a zebra, so
every arm was dashed right up to the mouth of the box, past the bar a driver stops at; the metre step was the
smaller fault. The boundaries are not looked for any more — `CentrelineRuns` takes them from whoever measured
them.

## 2026-08-25 — an inline junction's crossing is laid across the lanes at the node

The one thing TER-5b says an inline junction exists for did not work: the paint stands on the node itself,
further from every lane's end than the paint is wide, so the projection found no lane and a walker on it was
invisible to the traffic — hidden because every such crossing in the shipped towns was lit. It is laid
across the lanes that meet at the node, each at its own end (`LaneFurniture`), and that fallback is taken
only where the projection found nothing *and* the junction admits no turns.

## 2026-08-23 — a claim stops where a rule stops the car

`AskForTheGround` clamped the road at the place the car is held and then added the margin on top of the
clamp, so a car waiting for a zebra held a metre of the zebra — and a signalled crossing behaved like an
unsignalled one, the people getting over on their patience eight seconds later. The gap is part of what the
car asks for and is clamped with the rest. Nothing about following changes, and the ask only ever shrinks at a
stop.

## 2026-08-23 — a junction is committed to at the rate the car actually brakes at

The claim distance and the point past which a crossing is kept were the only stopping distances in the town
read off the pedal's cap, while every stretch of road is sized by the follower's braking figure. The cap is
larger, so both erred the way that costs: a car past the point it could stop gave the sections back for a bar
it was going to cross anyway, and in between two ticks they read free. Neither noticed wet ground, where the
gap is widest.

## 2026-08-23 — a stretch runs out at the box's near edge

`WaysAlong` stopped walking when the *next lane* began, and the next lane begins on the far side of the
junction — so nothing was laid on a junction until the stretch reached clear across it. A car approaching a
box claimed none of it, was granted its road as though the box were empty, and could see nothing standing in
it. The guard is the near edge now.

## 2026-08-22 — neither network's claims are one roster's

A walker on a crossing claimed the road and was in none of the road's questions — half right, since a walker
read as an obstruction is one a driver is held off, and one read as a committed claim cuts a car three lanes
away. What that cost was invisible until the ray went: nothing cut a driver's road at a body standing in it. A
body on foot is in every query a grant is taken against and carries its own reading, so what it must never be
is a property of which query happened to skip it. An occupant is an index into one of two rosters and the
stretch has to carry which, or the first walker whose index matched a car's is read out of the wrong fleet. The
walker's give-way arithmetic went with it: the claim *is* that arithmetic, already done, from fresher numbers.

## 2026-08-22 — braking has its own margin, and it is nearly all of the grip

Using the cornering margin for braking put the planned stop at 13.1 m/s² against the 21 the tyres actually
delivered, and every claim is sized by the planned figure — so a car held half again as much street as its
stop was going to use. A corner is held for as long as it lasts and its margin covers a bump, a camber and
the wheel still being turned; a stop is aimed at, straight, and over in seconds. Corner speeds are
untouched, which is the point of the figure being its own.
