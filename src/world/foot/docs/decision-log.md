# Decision log — the walking network

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-28 — a connection runs down its lane's course and turns about its own inside edge

**The owner ruled that off the paint a walker is never on the traffic's ground** (WLK-16). A junction's
connections were equal-tangent biarcs from the hand-over pose to the paint's, and the kerb-side lane's band
lies flush on the kerb (WLK-1): the biarc's first arc, wider than half the band, swung the band's inside edge
0.13 m over the kerb lane before its tight second arc brought it back. That is past the touch, so every such
connection was marked against the kerb lane, and a car planning past a zebra held a walker still on the
pavement, and the other way round — 43 marks on the fixture, 807 on Odesa, 842 on River, 957 on the scenario
map. The marks were right; the line was not.

- **One arc of at most half the band, taken off the course**: at that radius the inside edge stays on the
  pivot, which on the kerb-side lane is a point of the boundary. A turn laid from the pose's own straight
  instead left the lane's ground wherever the kerb turned a corner near the zebra.
- **A slightly tighter arc where the paint stands a degree or two off square to the kerb**, since the arc
  that wide would land a few centimetres past the mouth; a tighter one folds the edge back onto the walk.
- **The old curve straight from the hand-over point, checked against the boundary**, only where no place
  on the course turns that tight onto the paint: a course rounded well back from a tight corner with the
  paint's line running along it. Refusing those cost 16 connections across the shipped maps; checked, none.

Afterwards: no walked way off the paint marked against a driven one on any shipped map or the suite's own
towns, no connection refused, and the walk laid in 180 ms on Odesa against 153.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- **WLK-10**: a town's zebras stand where its kerb ends put a station — at boxes where three or more roads
  meet — rather than wherever a road end's two pedestrian nodes hand one over; the node pairs' placement is
  named as held in code with the rest of the node network, and "every end of every street has one" is gone.
- **WLK-14** (`P1`, reworded at the owner's word): what the census reports is the connections no curve would
  join at a crossing's junction — not refused turns, places nothing turns through or places handed more
  than one way, none of which it counts any more. The rule's own statement is unchanged.

## 2026-09-20 — a walker joins the network both ways, and could not before

`EntriesNear` and `GoalsAt` offered the way back only where the fine graph had a reverse, and
`FootGraph.Reverse` is `-1` for everything by design (WLK-8) — so the branch never ran, and a walker could
only set off whichever way the single nearest lane happened to run: **anything behind it cost a lap of the
ring**. Measured: one entry, always, for all 8611 edges of the suite's city.

**The second entry is the nearest lane whose line heads against the first** (`FootGraph.EdgesNear`, within a
pavement's width). **Nearest and not merely opposed**, because at a corner the lanes of every course meeting
there are within reach.

**What it was worth**: an ordered walk to a point across the street fell from 215 m to 56 m, and over a
shipped minute the trips finished went from 5 to 13 on Odesa and 6 to 20 on River. **It did not make anybody
walk a zebra** ([the known gaps](../../../../docs/index.md#known-gaps)).

## 2026-09-19 — the reach is asked per lane, and where the course passes

**A junction stood on every lane of the walk or on none**, so one course rounded away from a tight corner
deleted the crossing it stood at — no place, no paint, no way over the road. The reach
(`Road.CrossingMeetsTheWalkWithinM`) was there to refuse a connection laid to another street's pavement, **and
what it refused instead was the zebra**. So it is asked per lane and the place is not asked at all (WLK-15,
`CrossingWays.Reading.HandsOver`): a crossing whose boundary answers from too far off is still not laid, but a
lane out of reach is left out of that junction and out of nothing else. **A lane is reached only where every
mouth merged into the place answered on it** — kept from the mouths that did, the setback could stand level
with one that did not, which is the corner no curve holds (WLK-14). Odesa: 1 016 crossing lanes became
1 020, 912 junctions 913, one lane unreached where 2 zebras had been refused; the census prints the two
readings apart, a zebra refused and a lane unreached being different losses.

**And the reach is read where the course passes the junction** (`KerbLines.NearestTo`), not at the hand-over
point a setback along it. The last unreached lane stood at the apex of a fork, where the course is rounded
back up a wedge too narrow to carry it, 6.4 m off against the 6 m reach — a crossing's own pavement refused
for being a pace further off than a straight street's. The question the figure asks is whose pavement this
is; the setback then says where along it to hand over. Odesa reads no unreached lane.

## 2026-09-19 — a course takes the walk's own radius, and is filled and never cut

**One radius for every line the town lays was one too few** (TER-3c.10): the kerb, the pavement's outer face
and the courses were all rolled at `Road.LineRoundedM`, so a smoother walk meant a smoother kerb — and at
1.8 m the kerb's own ring closed over every carriageway in Odesa and a quarter of the kerb ends the walk is
cut at went with the tarmac (510 crossings to 376). **So a course takes its own figure**
(`Road.WalkRoundedM`) and the ground keeps its 1.4 m. A course is a line a body is held on, nothing is laid
along it, so two layers of concrete rounding one corner two ways is not a thing it can be.

**And the roll was cutting it.** `ArcOutset` rounds by running its own move three times — out by
<em>d</em>+<em>r</em>, in by 2<em>r</em>, out by <em>r</em> — and the last of those cuts a corner the shape
turns away at; above <em>r</em> = <em>d</em> it pulled the courses towards the tarmac, which cost the
kerb-side course its closure on Odesa at 2.5 m. **A course is filled and never cut**
(`ArcOutset.Corners.Filled`): the first two moves alone close a notch at the radius asked for and leave a
corner turning away as the arc of the distance moved. The ground keeps the three moves — a kerb is the line
a ball rolls — and the walk keeps two.

**So `Road.WalkRoundedM` has no bound**: 2.5 m now closes both of Odesa's courses, 111 rings each, 1 020
crossing lanes, nothing refused and nothing unreached. What the radius costs is the pockets it closes — at a
sharp fork the walk stands off the apex by about the radius — which is a shape to look at rather than a
fault to fail on.

## 2026-09-19 — a crossing is a junction cut into the walk, and the walk keeps its line

**The courses were two closed rings a block and nothing joined them**, so a walker could go round the block
it was put down on and nowhere else. What joins them is the paint the town already lays at its kerb ends
(`CityGen.KerbEnds`), walked by a pair of lanes over the carriageway (WLK-15). Odesa: 510 zebras, 1 020
crossing lanes, none refused; 12 363 lanes over 12 363 nodes became 21 234 over 17 960, and 222 runs — one per
ring — became 8 957.

**Each end is a junction centred between the pavement's two lanes, not at the kerb** (TER-5d): what a place
buys that a point on one line cannot is that a walker on the outer lane may cross and one off the paint may
take either lane, so a block with a zebra on it is one a walk can leave.

**Two mouths within a merge are one junction** (WLK-3), the figure being twice the setback a junction hands
over at — below it the two places' ground overlaps. Odesa: 1 020 mouths at 913 junctions, 107 merged. **The
hand-over points stand a setback beyond the outermost mouth, not off the merged centre**: struck off the
centre they fell level with the mouths, and a connection onto paint beside its own hand-over point is a
corner no curve holds — 154 of them, against none either side.

**Every arrival connects to every departure but the U-turn** (WLK-13). Laid, the U-turns were 2 038 of
Odesa's walking lanes and the only two connections no curve would join; dropped, the two lanes are joined
across the carriageway instead.

**The paint runs kerb to kerb**, so the ground a walker asks the road about (TER-5c.1) is the carriageway and
not a metre of footway at each end. **The walk is parted and never laid again**: the move is the whole
town's (WLK-1), so a stretch struck a second time folds wherever the shape swallowed more than its
neighbours could see — the course is cut at the hand-over metres instead, as a car park is cut into a street
(GEN-52).

**A connection is the curve between two poses, not the straight between two points** (WLK-14, TER-5d). As
straights they met the walk at whatever angle the geometry left and the corner was folded into the lane
behind: 62 of Odesa's 12 816 lane joints turned past five degrees, worst 71°. As curves, 0 past five degrees
over 5 436 joints, and nothing refused. **A way square across the footway was tried and is not here**: a
two-metre connector onto the outer lane turned 98 joints past five degrees and bought nothing the junction
does not.

## 2026-09-18 — the move's own answer is the lane, and the fine graph holds lanes rather than stretches

The pavement was offset twice: `BandShell.Outset` struck each course against every piece of the town, and
`WalkingNetwork.LayLanes` then struck a lane beside each stretch again with `ArcOutset.Beside`, which can
weigh a corner only against the pieces either side of it — so it holed the outside of a hairpin and folded
through the inside. **The second offset is the one that cannot work, and the first had already answered.**

**So a course is a lane where it is struck** (`PavementLanes`), `FootGraph` takes the lanes as they are, cut
at the joints the move came back with, and `LayLanes` offsets by nought. Odesa: 12 542 pieces in, 12 362 lanes
over 108.25 km, no joint open past a millimetre and none turning past a degree. **A fine edge is a lane and
no longer a stretch carrying two**, so `Reverse` is `-1` and a node's arrivals are their own list — which is
what makes a lane one-way (WLK-8) without a figure saying so. **What it costs is the build**: the foot graph
went from nothing to 427 ms on Odesa, nearly all of it the weld.

## 2026-09-18 — a town's pavement is the two courses, and the node network is not laid

The node pass answered a question the offset had already answered. **One move lays both kerbs of every road
at once** — round every block, round the mouth of every rank, round the outside of the ring — without asking
anything about a road, and everything the node pass added was cutting those rings into stretches and hanging
crossings off them, through the whole apparatus of WLK-1a to WLK-14, to arrive back at the lines the move had
already drawn. **So a town lays the courses** (WLK-1). Odesa's walking side went from 1 072 nodes, 991
places, 1 523 ways and 5 927 turns to two rings.

**The claim every map carried, that every end of every street carries a crossing, is out of the watch**
(VER-11) rather than kept as a claim nothing can answer: a claim about a construction the town does not lay
is a different town's.

**The construction is kept and is not called.** `FootJunctions`, `FootConnectors`, `FootWays` and
`FootMovements` still carry their rules and are asked their questions by the unit tier off a plan of their
own; the gap says so ([docs/index.md](../../../../docs/index.md#known-gaps)) so that nothing reads the silence
as the pass being correct and unused.

## 2026-09-18 — which kerb a node found is settled at its own carriageway

At a fork the two crossings crossed each other in an X over the crotch: `Road.FootNodeAsideM` reaches over
the arm opposite, so the boundary nearest the node was the other street's kerb and both arms anchored their
zebra on the same tip. **Asking the boundary at the node asks two questions at once** — which kerb, and where
along it — and the first is the road's: the point on the node's own carriageway edge it stands off (WLK-9).
Odesa went from 10 of 1 072 nodes on a kerb not their street's, worst 2.38 m out, to 2 at 0.37 m; movements
no line would join without a pivot fell from 535 to 473.

**Moving the node to the mouth of the street was tried and is not here**: the mouth is 0.19 m into the
street on average over 534 ends. What a fork gets wrong is how far the node stands *aside*, not how far back.

## 2026-09-18 — a car park is a hole in the pavement and not a corner of it

A car park's junction stood no node and the street it was cut into carried no walk either side of the cut:
Odesa laid 184 road walks over 507 streets, and 486 of its 1 022 street ends stood nothing. **A rank is a
hole in the pavement a walk goes round, not a place a street stops** — so the junction stands nothing and
**the walk down the street runs through it** (WLK-2), one side of a street being one lane end to end.

**Standing the pair and dropping only its crossing was tried and is not here**: it puts a corner where the
street has none. Odesa's 239 ranks stood 956 of 2 028 nodes, and the walk past a rank came back as three ways
and two pivots. Run through instead: 1 072 nodes at 991 places, lanes stopping short 168 → 96, lines over a
half turn's share of their straight 148 → 4. **What it costs is length**: the longest road walk is 812 m
where it was 366, and refused turns went 444 → 463 as a longer stretch was fitted at each end.

## 2026-09-18 — a pavement lane is a stretch of the offset, and the point is moved to the line

A lane was cut from its course and then **fitted at both ends**: the point a node hands it over at was struck
square off the boundary at the lane's distance, the course is that distance taken against the whole shape,
and the two are a millimetre apart where the move left the corner standing and a pavement's width apart where
it swallowed one. A biarc closed the gap: Odesa carried 117 m of it, worst 3.48 m off the line, and 96 lanes
stopped short because no corner would hold.

**The gap was the point standing off the line**, so the point is dropped onto the course when it is struck
(WLK-9) and the lane is the stretch between two of them (WLK-11). Odesa reads 100.00% of 206.11 km on its
course, no lane short, and 900 fewer arcs. **What it costs is turns**: a place's movements are joined
against the course's heading at the point rather than the boundary's, and 538 of Odesa's 6 400 will not join
without a pivot where 463 would not before (WLK-14). **The turn is the thing to improve, and a lane bent off
its own line to flatter it is not the way to.**

## 2026-09-17 — a connection point is placed off the boundary, not off the road's own half-width

The first placement took every point from the road record — a zebra ended at half a carriageway, a road walk
began at the node, a corner walk ran from one place straight to the next — and **all three are wrong at a
mouth**, where the driven ground reaches past every arm's edge (TER-5): a zebra stopped in the middle of the
road and a corner walk set off across the junction. **The boundary is the one line that has already merged
all of it** (TER-3c.8), so every point is placed off it (`KerbLines`, WLK-9).

**Indexed a piece at a time and not a ring at a time**: a ring is the whole outside of a block, so a query
that took the ring walked a kilometre of arcs for a two-metre answer — the town's walk went from 259 ms to
53. **Which ring a piece came off is kept beside it**, so a setback can be measured *along* the boundary
through a fillet rather than stopping at the end of a piece.

## 2026-09-17 — a place is walked through on a turn per arrival and departure, curve first

**A turn for every lane arriving at a place onto every lane setting off from it** (WLK-13, `FootMovements`)
— what the car side calls a movement. **A ring round the place was the alternative and is not here**: joining
a place's points in bearing order makes the pavement continuous and says nothing about what connects to
what, and a directional network (WLK-8) needs which arrival reaches which departure.

**A turn is one curve between its two poses, and the course is what the long ones fall back on.** The order
was the other way first, and every turn routed onto the departing lane's course came back as an S onto a
crossing or a loop at a corner: curve first, Test's turns cut to a straight fell from 288 of 736 to 40. The
course is still what the turn a single curve would cut falls back on, and those arrive at it by the curve's
own guards refusing them.

**The curve's guard was the walking network's own** — a six-centimetre step that drew a 26 m loop, a third
of a town's corners tighter than the feet can hold, Odesa's given-up walks going from 37 a minute to 211 — so
it was lifted into `Spline.CorneredInto` beside the biarc it guards, refused where it turns tighter than
`WalkerTightestTurnM`, and both callers share it. **`Spline.StraightArcStraightInto` is not the tool**: its
straights run out to where the two pose lines cross, hundreds of metres away at nearly parallel poses —
Test's longest turn went from 17 m to 1 782 m and all 81 pivots stayed.

## 2026-09-17 — a line through a place is bounded in heading and in ground, never in length

The guard refused a corner **longer than twice the span it bridges** — a length standing in for a shape — and
let through a loop a couple of metres across and the S at nearly parallel poses, both heading spent going
nowhere. **So a corner spends at most half a turn** (`Spline.SweptRad`, `Spline.HalfATurnRad`), the most any
two poses ask for, and **the whole turn is held to it as well as each corner** (`FootMovements.WorthWalking`):
two corners onto a course with a stretch between them were each inside the bound while the three together
were a ring. Test's winding turns went from 3 of 736 to none.

**And a course that meanders spends no heading and still goes round the houses**, so the ground is bounded
too: no more than a half turn covers of its chord (`Spline.HalfATurnOfItsChord`, π/2). Twice the straight is
a fifth again too loose — at 2× the bulging turns at a merged corner passed. Turns over that share went 7 → 0
on Test and 56 → 0 on Odesa, and the worst pivot halved on both, to 60°.

**Weighing the curve against the pivot by what each costs was tried twice and is not here.** The exchange rate
is exact — a radian pivoted costs `WalkerTightestTurnM` of walking (`Spline.PivotedM`) — but at a walker's
turn rate the pivot wins nearly every corner: Test's cut turns went 40 → 165, Odesa's 297 → 889, and a full
turn on the tightest circle costs precisely what pivoting through it costs, so the ring ties and passes.
**The pivot is what is left where no line is worth walking, not what a walkable curve is talked out of.**

## 2026-09-17 — a rough joint is not laid at all, and the place is short a movement instead

**WLK-14 is the owner's, stated in as many words**: no rough connections between lanes, all good curves. So
every joint of every turn is read against the lane either side of it and **what cannot be joined without a
pivot is not laid** (`FootMovements.Smooth`).

**It is refusal and not better curve-fitting, because the fitting was measured first.** Of Test's 42 turns
that had come out as a straight, none was a pose pair a curve exists for; of Odesa's 297, three were. The
rest ask for what no line tangent to both ends can do: **set off behind where the walk arrived** (9 and 28),
**turn a body round on the spot** (1 and 6), or turn onto a lane welded to one point (WLK-12), which faces
nowhere. A biarc family search would have bought three movements on a city.

| | Test | Odesa |
|---|---|---|
| rough joints | 20 → **0** of 1 388 | 75 → **0** of 6 312 |
| movements laid | 736 → 700 | 3 454 → 3 166 |
| movements refused | 36, 4.9% | 288, 8.3% |
| places nothing turns through | 0 | 4 → 6, two of them handed more than one way |

**What it costs is named rather than hidden**: 8.3% of Odesa's movements and two places a walk can arrive at
and not leave. The road side pays the same price at the same kind of corner — 155 of Odesa's driving lanes
are offered one movement because the junction refused the others as too tight.

**The remedy is where the points stand, not how the line is drawn.** A movement that sets off behind its own
arrival is a place handing its ways over at points metres apart (WLK-9); pulling those in moves every
crossing head in the town, so it is the owner's to ask for.

## 2026-09-17 — a course is taken only where it is a curve at both ends

**Every bound read a line and none read the joints between lines**: Test had a lane arriving and a 5.27 m turn
leaving 180° the other way, which a picture showed and no figure named. What it found is that a course route
was taken even where it could not be joined — `Joined` fell back to the straight between the two points,
which points wherever they stand, including back down the lane. **A route a walk pivots onto and then walks
the long way round is worse than the one pivot the straight costs**, so the course is taken only where both
its corners hold (WLK-13). The worst pivot inside a turn went from 60° to 0° on both towns.

**A crossing's course is the boundary**, WLK-9 striking a crossing's pair on it, so a turn between two
crossings runs along the kerb — consistent with its own ends. **The turn back down the way it arrived is
left out**, as for a car: a pedestrian can turn round, but a place needs no line laid for it. It costs a
place with one way handed over at it everything — four of Odesa's 991 places were walked out of by nothing.

## 2026-09-17 — a lane with nothing left of it is welded into one point, and the weld is a lane's

Where a walk turns into a corner rather than round one, a lane's two ends stand on each other and what was
laid between them was a stride of walk joining two places that are one place. **So the two points are welded
and nothing is laid down that lane** (WLK-12). **Asked of the pair it never fired**: a pavement's two lanes
pinch out at different places, so the nearest pair on Test read 2.37 m and nothing welded; asked of the lane,
eight weld and the nearest point touching reads 0.03 m. The eight were stubs the straight fallback had been
drawing, so Test's ways laid as a straight went from five to none.

**A second merge and not a widening of the first**: `Road.FootNodeMergeM` asks how near two nodes stand
(WLK-3), this how near the two ends of one lane. Merging the nodes instead would make two junctions one,
which they are not — they keep their crossings, corners and arms, and give up a lane's length. **A dropped
pair and a shared point are asked apart** (`FootConnectors.Shares`): the first stands for nothing, the second
is somewhere a walk arrives.

## 2026-09-17 — a way between two points is a stretch of the boundary already moved off itself

**A biarc through the two poses was the obvious alternative and is wrong where it matters**: a street is a
curve and a mouth is a corner, so a line that only has to arrive right cuts across the tarmac in between.
The way is a stretch of the boundary moved off itself by the offset its points stand at (`KerbLines.Between`,
WLK-11) — the reading the points were struck off, carried along. **The shorter way round a closed line is not
a tie-break**: the long way round a block is the rest of the town.

**The move is the whole town's, taken once per lane, and the stretch is cut out of the answer** (WLK-1): a
stretch moved on its own (`ArcOutset.Beside`) cannot see a corner whose radius the offset swallows, and every
rounded mouth on Test drew a knot of chords over the pavement. It took the walk's share of opening Odesa
from 35 ms to 91 ms of 1.8 s (`--bench load`).

**Which way a lane is walked is the way's and not the lane's**, read once off the direction of the boundary
under the way (TER-4a, WLK-8, `FootWays.SetsOff`). Read off each lane's own line, a way's two lanes disagreed
about which end they set off from — two walks one way and none the other — and taken twice, one lane went
round the corner and the other round the rest of the block.

## 2026-09-17 — a wedge is a corner only where the arms either side of it carry a walk

`WedgeNeighbour` sorted only the arms that stood a node, so at a roundabout's arm the ring's two ends were not
counted and the arm's own two sides came out consecutive — a corner round a head that is not there, laid as
1 774 m of walk round the outside of the whole town. **So every arm is counted and only the pairs that both
stand are joined** (WLK-2): an arm's mouth is a hole the walk goes round. A dead end still pairs its own two
sides, the wedge really being the head of the street.

## 2026-09-17 — a node hands over at a pair of points per way

One point per way cannot say where two lanes start: a line struck between single points is a centreline the
two directions are offset off, so where a way entered a node was an answer the mitre had to invent. **So a
node hands over at a pair laid across the way** (WLK-9). **Per node and not per pedestrian junction**: which
side of a node is the road's and which the junction's is a fact about the one road end it came off, which a
merged corner of two arms has no single answer to.

**A merge drops a pair rather than moving one** (WLK-3), and **which pair is structural** — each of the three
ways has a node it runs to. Read off the geometry instead, a skew corner picks the wrong one of the three, and
two nodes almost on top of one another have no direction to read at all.

## 2026-09-07 — a corner gives up the arc it is turned on, and only a crossing gives up its band

Every lane end gave up half a band to its corners, so the outside of every bend was pavement no lane
reached — the figure a *crossing* needs, charged to corners that are not crossings. What a corner costs is the
arc it is turned on, `offset × tan(half the turn)`, nothing where the walk runs straight on; a crossing keeps
its band, where the overlap really is a band (`WalkingNetwork.Margins`).
