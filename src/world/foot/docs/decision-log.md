# Decision log — the walking network

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-20 — a walker joins the network both ways, and could not before

`EntriesNear` and `GoalsAt` both offered the way back only where the fine graph had a reverse
(`_foot.Reverse(edge) >= 0`), and `FootGraph.Reverse` is **`-1` for everything, by design** (WLK-8): a lane
is walked one way and the lane beside it is a line of its own a lane's width away, not this one read
backwards. So the branch never ran. Both methods documented "both ways along the stretch it stands on" and
handed back one — a walker could only ever set off whichever way the single nearest lane happened to run,
and **anything behind it cost a lap of the ring**. Measured: one entry, always, for all 8611 edges of the
suite's city.

The second entry is now the nearest lane whose line heads *against* the first, looked up over the same
chain index the first came from (`FootGraph.EdgesNear`, within a pavement's width). **Nearest and not
merely opposed**, because at a corner the lanes of every course meeting there are within reach.

**What it was worth**: an ordered walk to a point across the street fell from 215 m to 56 m, and over a
shipped minute the trips a town finishes went from 5 to 13 on Odesa and 6 to 20 on River — the same
walkers, no longer walking round a block to reach what was behind them.

**What it did not fix is the zebras**, which are still walked by nothing
([the known gaps](../../../../docs/index.md#known-gaps)).

## 2026-09-19 — a walk out of reach costs its own lane, and no longer the junction

**A junction stood on every lane of the walk or on none**, so one course rounded away from a tight corner
deleted the crossing it stood at: no place, no paint, no way over the road, and a street a walker could only
look at from the pavement. The reading was there to refuse a connection laid across whatever stands between
the paint and another street's pavement (`Road.CrossingMeetsTheWalkWithinM`) — **and what it refused instead
was the zebra**.

**So the reach is asked per lane and the place is not asked at all** (WLK-15, `CrossingWays.Reading.HandsOver`).
A mouth is still the paint's own end and a crossing whose boundary answers from too far off is still not laid,
because paint with nowhere to begin is nothing; but a hand-over point is a place on one course, so the lane
that did not answer is left out of that junction and out of nothing else. The lane is not cut, the walk runs
past it as the move laid it, and the lanes that did answer connect exactly as they did. **A lane is reached
only where every mouth merged into the place answered on it** — kept from the mouths that did, the setback
could stand level with one that did not, which is the corner no curve holds (WLK-14).

Odesa: 1 016 crossing lanes became 1 020 and 912 junctions 913, with 1 junction lane left unreached where
the census read 2 zebras refused. **The two readings are now different sizes** — a zebra refused is a
crossing the town has not got, a lane unreached is a connection it has not got — and the census prints both,
the second with where it stands and how far off its course really was.

## 2026-09-19 — the reach is asked where the course passes, and the one lane left out was a stride short

**The last unreached lane was not a walk round the block.** It stood at the apex of a fork, where the wedge
of pavement between two roads is too narrow to carry the outer course into the tip: the course is rounded
back up the wedge, and the junction's own centre sat 6.4 m from it against the 6 m reach. Read as it was
written, the figure was refusing this crossing's own pavement for being a pace further off than a straight
street's.

**What was being measured was the wrong point.** The reach was read at the hand-over point — a setback of
`Road.FootConnectorAlongM` along the course from the junction — so a course that passes a place at arm's
length is refused for where it runs two and a half metres on. **The question the figure asks is whose
pavement this is**, and that is answered where the line passes the junction (`KerbLines.NearestTo`); the
setback then says where along it to hand over. Odesa reads no unreached lane at all, on either course, and
the fork's two zebras connect on every lane.

## 2026-09-19 — a course is filled and never cut, and the walk's radius has no bound

**The roll that smoothed a course was also cutting it.** `ArcOutset` rounds by running its own move three
times — out by <em>d</em>+<em>r</em>, in by 2<em>r</em>, out by <em>r</em> — and the last of those is the
one that takes a corner the shape turns away at. Below <em>r</em> = <em>d</em> it is the identity, so
nothing noticed; above it the courses were being pulled in towards the tarmac at every corner tighter than
the radius, which cost the kerb-side course its closure on Odesa at 2.5 m and left the walk at a fork
standing where no crossing could reach it.

**A course is a line a body is held on, so it is filled and never cut** (`ArcOutset.Corners.Filled`,
TER-3c.10). The first two moves are the fill on their own: a notch a fold left is closed at the radius
asked for, and a corner the shape turns away at comes back as the arc of the distance moved, which is what
an offset with no rounding at all gives. **The ground keeps the three moves** — a kerb is the line a ball
rolls and a corner cut into it is a corner the town has — and the walk keeps the two.

**So `Road.WalkRoundedM` has no bound.** The figure that was 2.4 m because 2.5 broke a course now closes
both of Odesa's at 2.5 as well: 111 rings each, 1 020 crossing lanes, nothing refused and nothing unreached.
What the radius still costs is the pockets it closes — at a sharp fork the walk stands off the apex by about
the radius — which is a shape to look at rather than a fault to fail on.

## 2026-09-19 — a course is rounded at the walk's radius and not the ground's

**One radius for every line the town lays was one radius too few** (TER-3c.10). The kerb, the pavement's
outer face and the courses a walking lane is a stretch of were all rolled at `Road.LineRoundedM`, so asking
for a smoother walk meant asking for a smoother kerb — and the ground cannot have one: raised to 1.8 m the
roll's diameter is a lane exactly, the kerb's own ring closed over every carriageway in Odesa, and a quarter
of the kerb ends the walk is cut at went with the tarmac (510 crossings to 376).

**So a course takes its own figure** (`Road.WalkRoundedM`), and the ground keeps its 1.4 m. What parts them
is what they are: a course is a line a body is held on, nothing is laid along it and no kerbstone bends to
it, so the disagreement TER-3c.10 was written against — two layers of concrete rounding one corner two ways
— is not a thing a course can be in. **It may be the wider of the two safely**, because it fills without
cutting: every corner a course turns away at comes back as the arc of the distance moved, and only the
notches a fold left are filled.

Odesa at the shipped 2.4 m: both courses closed, 111 rings each, the inner 108.72 km over 6 277 pieces
against 109.10 km over 6 332 unrounded, and nothing about the roads moved.

## 2026-09-19 — a crossing is a junction cut into the walk, and the walk keeps its line

**The courses were two closed rings a block and nothing joined them**, so a walker could go round the block
they were put down on and nowhere else. What joins them is the paint the town already lays: a zebra stands
wherever a kerb ends (WLK-10, `CityGen.KerbEnds`), and it is walked by a pair of lanes over the carriageway
(WLK-15). Odesa: 510 zebras, 1 020 crossing lanes, none refused; 12 363 lanes over 12 363 nodes became
21 234 over 17 960, and 222 runs — one per ring — became 8 957.

**Each end of one is a junction and hands over at a point per connected lane** (TER-5d). **Its centre stands
between the pavement's two lanes**, not at the kerb: six lanes meet there — the walk arriving and leaving on
each lane, and the crossing's two — so each lane is cut twice and the crossing hands over on the boundary.
**What the place buys is what a point on one line cannot**: a walker on the outer lane may cross and one off
the paint may take either lane, so a block with a zebra on it is one a walk can leave.

**Two mouths within a merge of one another are one junction** (WLK-3), and the figure it is asked at is
twice the setback a junction hands over at — below that the two places' own ground overlaps, and what stands
between them is a stride of pavement with a hand-over at each end rather than a walk anybody takes. Odesa:
1 020 mouths at 913 junctions, 107 merged. **The place stands midway between them and its hand-over points
stand a setback beyond the outermost**, which is not the same thing: struck off the merged centre instead
they fell level with the mouths, and a connection onto paint standing beside its own hand-over point is a
corner no curve holds — 154 of them, against none either side of the change.

**Every arrival connects to every departure but the U-turn** (WLK-13). The two lanes of a pavement are
walked opposite ways, so the way from one to the other at one junction is a walker arriving and at once
retracing the lane beside the one they came in on. Laid, they were 2 038 of Odesa's walking lanes and the
only two connections in the town that no curve would join; dropped, the two lanes are joined across the
carriageway instead — out over the paint and back — which is the way round anybody would really take.

**And the paint is the paint**: a crossing's own stretch runs kerb to kerb, so the ground a walker has to
ask the road about (TER-5c.1) is the carriageway and not the carriageway with a metre of footway at each
end, and a body stepping off is on the pavement before it is anywhere a car can be.

**The walk is parted and never laid again.** The move is the whole town's and is cut against every piece of
it (WLK-1), so a stretch struck a second time folds through itself wherever the shape swallowed more than
its neighbours could see. A junction wants somewhere to hand over and nothing more, so the course is cut at
those metres and every piece stands on exactly the ground the one piece did — the same injection a car park
is cut into a street with (GEN-52). **Each place is its own line's answer** (WLK-9): the point the paint
stops at is dropped onto the boundary, the place the walk is reached is dropped onto the course, and the
graph's weld joins each of them to what was already there without standing a second node beside it.

**A connection is the curve between two poses and not the straight between two points** (WLK-14, TER-5d).
Laid as straights they met the walk at whatever angle the geometry left, and the corner was then folded into
the lane behind them (WLK-13) where the biarc that would round it does not always fit: 62 of Odesa's 12 816
lane joints turned past five degrees, worst 71°, and a body stepping off a zebra pivoted there. **Drawn as
the curve from the arriving lane's pose to the departing one's, none of them does** — 0 past five degrees
over 5 436 joints. **What no curve joins is not laid**, which is the answer WLK-14 already gives a movement
a place cannot offer; over Odesa that is now none of them.

**A way square across the footway is not here either.** The first cut ran the crossing on to the outer lane
over a two-metre connector meeting the course at a right angle; that turned 98 joints past five degrees and
bought nothing, the outer lane being reachable through the junction instead.

## 2026-09-18 — the move's own answer is the lane, and the fine graph holds lanes rather than stretches

The pavement was offset twice. `BandShell.Outset` struck each course against every piece of the town, and
then `WalkingNetwork.LayLanes` struck a lane beside each stretch again with `ArcOutset.Beside` — which can
weigh a corner only against the pieces either side of it, so it holes the outside of a hairpin and folds
through the inside. **The second offset is the one that cannot work, and the first had already answered.**

**So a course is a lane where it is struck.** `PavementLanes` lays the two moves and says which way each is
walked; `FootGraph` takes them as the lanes they are, cut at the joints between the pieces the move came
back with; `LayLanes` offsets by nought and `LaneOf` hands back the line the graph holds. Odesa: 12 542
pieces in, 12 362 lanes over 12 363 nodes and 108.25 km, contracted to 222 runs — one per course ring, two
rings a block — with no joint open past a millimetre and none turning past a degree.

**A fine edge is a lane and no longer a stretch carrying two**, so the pair model goes with it: `Reverse` is
`-1`, a node's arrivals are their own list rather than its departures read backwards, and the nearest-lane
index carries every lane rather than every second one. It is what makes a lane one-way (WLK-8) without a
figure saying so — the move comes back wound with the driven ground on one hand, so the direction is one
reading for the whole town and the lane beside it is the other move walked the other way.

**The seams are the shape's own corners.** A course is closed and nothing meets it, so a graph over it has
to be cut somewhere; cut at the pieces the offset produced, every node is of degree two and the contraction
folds each course back into the one run it is. **No node here is a place a walk chooses between ways** —
there is nothing anywhere in this graph to choose until a crossing joins two courses, which is the gap
([docs/index.md](../../../../docs/index.md#known-gaps)).

**What it costs is the build**: the foot graph went from nothing to 427 ms on Odesa, nearly all of it the
weld that finds each piece's two ends among the ones already standing.

## 2026-09-18 — a town's pavement is the two courses, and the node network is not laid

The node pass answered a question the offset had already answered. A course is the whole driven shape's
boundary moved off itself once, so **one move lays both kerbs of every road at once** — round every block,
round the mouth of every rank, round the outside of the ring — and it does so without asking anything about
a road: no end to stand a corner on, no arm to count, no bay or ring or rank to exclude. **Two moves are the
whole town's pavement.** Everything the node pass added on top of that was cutting those two rings into
stretches and hanging crossings off them, and it needed the whole apparatus of WLK-1a to WLK-14 — a merge, a
weld, a setback, a wedge walk, a fallback course — to arrive back at the lines the move had already drawn.

**So a town lays the courses and nothing else** (WLK-1): `TownWorld` holds `WalkLines`, the `nodes` overlay
draws the two rings as the outlines they are, and the census reports what each move closed. Odesa's walking
side went from 1 072 nodes, 991 places, 1 523 ways and 5 927 turns to two rings.

**What that costs is the crossings, and they are gone with it.** A zebra is placed where two pedestrian
nodes hand a crossing to each other (WLK-10) and nothing else places one, so no map is painted with any —
and the claim every map carried, that every end of every street carries a crossing, is out of the watch
(`VER-11`) rather than kept as a claim nothing can answer. A claim about a construction the town does not
lay is not a weaker claim, it is a different town's.

**The construction is kept and is not called.** `FootJunctions`, `FootConnectors`, `FootWays` and
`FootMovements` still stand, still carry their rules and are still asked their questions by the unit tier
off a plan of their own — what changed is that no town lays them. The gap says so
([docs/index.md](../../../../docs/index.md#known-gaps)) so that nothing reads the silence as the pass being
correct and unused.

## 2026-09-18 — which kerb a node found was settled at the node, and is now settled at its own carriageway

At a fork, the two crossings crossed each other in an X over the crotch. A node stands
`Road.FootNodeAsideM` clear of its own tarmac, and where two arms leave at a sharp angle that figure reaches
over the arm opposite — so the boundary *nearest the node* was the other street's kerb, and every point the
node handed over was struck round the notch between them. Both arms anchored their zebra on the same tip.

**Asking the boundary at the node asks two questions at once** — which kerb, and where along it — and only
the second is what a setback along the boundary is for. The first is answered by the road: the point on the
node's own carriageway edge that the node stands off is a point of this road whatever stands over the node,
and at every end where nothing does, the two questions have the same answer. Odesa went from 10 of its 1 072
nodes holding a kerb that was not their street's, worst 2.38 m out, to 2 at 0.37 m; the movements no line
would join without a pivot fell from 535 to 473.

**Moving the node instead was tried and is not here.** Placing the pair off the mouth of the street — where
the boundary stops running round the junction and starts following the road — reads well and answers
nothing: the mouth is 0.19 m into the street on average over 534 ends, the boundary following an arm to its
connection point almost everywhere. What a fork gets wrong is how far the node stands *aside*, not how far
back.

## 2026-09-18 — a car park is a hole in the pavement and not a corner of it

A car park's junction stood no pedestrian node and the street it was cut into carried no walk either side of
the cut: Odesa laid 184 road walks over 507 streets, and 486 of its 1 022 street ends stood nothing. The
kerb, the paving and the ground were all there and continuous; **only the network was missing**, which a
picture at any rank showed as a street with a pavement nobody could be placed on.

**A rank is a hole in the pavement a walk goes round, not a place a street stops.** So the junction stands
nothing — no corner to arrive at, nothing to cross and nothing to turn through — and **the walk down the
street runs through it** (WLK-2): a road's walk reaches the node on the same side past the cut, and past
every further cut after it, so one side of a street is one lane from one end of the street to the other.

**Standing the pair and dropping only its crossing was tried and is not here.** It puts a corner where the
street has none. Odesa's 239 ranks stood 956 of the town's 2 028 pedestrian nodes and half of its 1 947
places, and the walk past a rank came back as three ways and two pivots — lane, turn, the stretch round the
mouth, turn, lane — with a biarc fitted at each of the four cut ends. Run through instead, the same town
stands 1 072 nodes at 991 places, `short of` falls from 168 lanes to 96, `round about` from 148 to 4, and
what is *not* on its course from 1 967 m to 117 m. **The 956 nodes were paying for the corners they made.**

**What it costs is the length of what is left.** The longest road walk is 812 m where it was 366, and `wound`
is 614 lanes of 3 046 against 783 of 4 958 — a lane that runs round every mouth in a street spends heading
the straight between its two ends never asked for, which is the ground and not a defect. The refused turns
(WLK-14) went 444 to 463 as the corner at each end of a longer stretch was fitted against a longer course; no
place lost every movement, and the rate reads worse only because there are half as many movements to ask for.

**And the lanes are weighed against the courses rather than taken on trust** (`on its course`). Every
pavement lane is cut from `BandShell.Outset` of the town's merged driven ground — the same call at two more
distances that `CityGen.GroundRings` draws the paving with — and the reading is what said the ends were still
being fitted rather than cut, which the entry below settles.

**The notch a fold leaves at a corner turning in on the shape is the outset's answer** (TER-7b) and not this
slice's: ten of Odesa's lane joints stand open past a centimetre, all of them inherited from the course.

## 2026-09-18 — a pavement lane is a stretch of the offset, and the point is moved to the line

A lane was cut from its course and then **fitted at both ends**: the point a node hands it over at was struck
square off the boundary at the lane's own distance, the course is that same distance taken against the whole
shape, and **the two are not the same place** — a millimetre apart where the move left the corner under them
standing, a pavement's width where it swallowed one. What closed the gap was a biarc onto the course at each
end, with the course giving up the ground the corner needed. Odesa carried 117 m of that, worst 3.48 m off
the line the walk is supposed to be on, and 96 lanes that stopped short because no corner would hold.

**The gap was never the corner's to close: it was the point standing off the line.** So the point is dropped
onto the course when it is struck (WLK-9) and the lane is the stretch between the two, with nothing fitted to
either end (WLK-11). Odesa now reads **100.00% of 206.11 km on its course**, `short of` 0 of 3 046 lanes,
`0` lanes laid as a straight for want of a course, 3 turns cut to a straight against 24, and 900 fewer arcs
over the same network. **A line the shape drew beats a line fitted to it**, and the instrument that says so
is the same one that found the fitting.

**What it costs is turns**, which is the trade to know: the movements a place offers are joined against the
*course's* heading at the point rather than the boundary's, and 538 of Odesa's 6 400 will not join without a
pivot where 463 would not before (WLK-14). No place lost every movement, and no lane and turn meet at a
gap — `pivoting at` is 0 of 11 718 joints. **The turn is the thing to improve, and a lane bent off its own
line to flatter it is not the way to.**

**The courses are struck at the radius every line beside a road is struck at** (`WalkLines`,
`RoadFigures.LineRoundedM`, TER-3c.10) and no longer at a share of their own distance. A lane is a line down
the middle of a pavement, so a course rounded unlike the kerb it runs beside is a lane that leaves the
middle; why the town has one figure for all of them is CityGen's entry to tell (2026-09-18, `GroundRings`).
Left unrounded the notch is a spike of course standing in open pavement — a walk out to a point and back,
which is a line nobody walks.

**And the rounding means what it says now.** It was a pass over the finished offset that refused most of what
it was offered, so the courses came back with 434 of their 1 024 notches at a metre and a spike a metre deep
at one place; it is the move itself now, run again (2026-09-18, `ArcOutset`). Over Odesa the pavement keeps
`100.00%` of its 205.58 km on its course and `short of` 0 of 3 046, and what moved is the shape of the line:
**the worst detour is 1.7× its own straight against 174×**, and a thousand fewer arcs carry the same network.
Turns refused for want of a joint are 535 of 6 400, where the fitting left them.

## 2026-09-17 — the walk is placed off the road ends, and no longer struck off the town's boundary

The pavement used to be the driven ground's own boundary moved by a kerb and half a walk
(`GroundRings.WalkLane`), cut to the runs the ground would carry, stitched where the veto had bitten,
pruned of stubs, dropped where it had been said twice, and run together where a seam was not a corner.
A zebra was then spliced into it by finding where the paint's square met the line.

**What that construction could not say is where two zebras meet.** The walk was a line and the crossings
were attached to it wherever they landed, so two arms of one junction put their crossings' exits a
half-metre apart on two different pieces of the same ring — a walk arriving at a corner had four places to
choose between where a person has one, and the picture showed the tangle. Every question that had to be
answered about the ring — which loose end meets which, which line leads somewhere, which pavement is another
one said twice — existed because the line was *discovered* rather than placed.

**Placed instead, the questions do not arise** (WLK-1a). A road end is given a pair of nodes, the nodes
standing near one another are one junction (WLK-3), and every way a node hands over to is named: the crossing
over the carriageway, the walk down the road, the walk round the junction. There is no stitch, no prune, no
drop-what-was-said-twice and no run-together, because nothing was ever cut out of anything.

**It also took the walk off the critical path of standing a town up.** The old construction asked the ground
at every tenth of a metre of every stretch on both hands, which on a city was most of a minute and the
largest single cost of opening a map; the placement is 27 ms over the same city, and the whole test suite
fell from 1 m 25 s to 29 s with it.

## 2026-09-17 — a connection point is placed off the boundary, not off the road's own half-width

The first placement took every point from the road record: a zebra ended at half a carriageway, a walk beside
a road began at the node itself, and a walk round a corner ran from one junction's place straight to the
next one's. **All three are wrong at exactly the place they matter**, which is a mouth. The ground a
junction's movements are driven over reaches past every arm's edge (TER-5), so half a carriageway is inside
the tarmac there and a zebra stopped in the middle of the road; the kerb turns into the corner while the
arm's centreline runs straight on, so a corner walk set off across the junction rather than along it; and
with no reach of their own the ways met wherever their own geometry ran out.

**The boundary is the one line that has already merged all of it** (TER-3c.8), so it is what every point is
placed off (`KerbLines`, WLK-9). **Indexed a piece at a time and not a ring at a time** — a ring is the whole
outside of a block, so a query that took the ring walked a kilometre of arcs for a two-metre answer, and the
town's walk went from 259 ms to 53. **Which ring a piece came off is kept beside it**, which is what lets a
setback be measured *along* the boundary: the arc under a point is a couple of metres of fillet, so a step
taken inside it would run off the end of the piece and stop there instead of following the corner round.

## 2026-09-17 — a place is walked through on a turn per arrival and departure, and a turn changes lane first

The ways were laid and every one of them ended at a place nothing carried on from: a corner was several walks
that happened to stop near each other. **What joins them is a turn for every lane arriving at a place onto
every lane setting off from it** (WLK-13, `FootMovements`), which is what the car side has called a movement
all along — the ways are the stretches between places and these are the turns within one.

**A ring round the place was the alternative and is not here.** Joining a place's points in bearing order
makes the pavement continuous and says nothing about what connects to what, so a walk from a zebra onto the
street becomes a run of hops whose number depends on how many points happen to lie between them. **A lane is
directional** (WLK-8), and what a directional network needs at a junction is which arrival reaches which
departure, which is the product and not a ring.

**A turn is one curve between its two poses, and the course is what the long ones fall back on.** The order
was the other way round first, and routing every turn onto the departing lane's course made the short ones
worse in exactly the way a picture shows: **a turn onto a crossing came back as an S where one arc would do,
and a turn at a corner as a loop where the course doubled back**. Put the curve first and Test's turns cut
to a straight fell from 288 of 736 to 40, the line they come to from 3.43 km to 3.11, and the crossings
stopped being 259 of the 288 — the course was the whole of what was defeating them.

**What the course is still for is the turn a single curve would cut.** Two ends five metres apart round a
corner, where one arc between them cuts that corner and at a mouth cuts across the carriageway; those are
the ones the curve's own guards refuse, so they arrive at the course route by being refused rather than by
being measured for it.

**A turn is joined to its ends by a curve the feet can hold, and the construction for it already existed.**
A walk meeting its course at an angle stops and pivots, which costs a walker time; so each join is the biarc
between the two poses, refused where it turns tighter than `WalkerTightestTurnM` (`Spline.CorneredInto`).
**That guard was the walking network's own** — its evidence is a six-centimetre step that drew a 26 m loop and
a third of a town's corners coming out tighter than the feet can hold, with Odesa's given-up walks going from
37 a minute to 211 — so what was done here was to lift it out of `WalkingNetwork` into `Spline` beside the
biarc it guards, and have both callers share the one construction.

**`StraightArcStraightInto` looked like the right tool and is not.** It lays the turn on a circle the caller
names, which is exactly "a curve at the walker's turn rate", but its straights run along the two pose lines
out to wherever those lines cross: at nearly parallel poses that is hundreds of metres away, and Test's
longest turn went from 17 m to 1 782 m while the pivots it was meant to remove stayed at all 81 of them.

## 2026-09-17 — what a corner may not do is wind, and the bound is read in heading rather than in length

The guard on the biarc refused a corner **longer than twice the span it bridges**, which is a length standing
in for a shape. It let through the two lines a picture of the fixture shows for what they are: **a loop a
couple of metres across at two ends facing opposite ways**, and the S it draws at nearly parallel poses.
Both are the same failure — heading spent going nowhere — and neither is a length.

**So the bound is the heading: a corner spends at most half a turn** (`Spline.SweptRad`, `HalfATurnRad`). The
heading between two poses is never more than that, so a corner turning one way through it never needs more,
and the runaway spends a whole turn to arrive facing the way it set off. **Asked of the whole turn as well as
of each corner in it** (`FootMovements.Winds`): two corners onto a course with a stretch of course between
them were each inside the bound while the three together were the ring, which is the one reading that catches
it. Test's turns that spend half a turn over what their ends ask for went from 3 of 736 to none, its longest
turn from 14.4 m to 10.1, and the turns cut to the straight only from 40 to 43 — **the curves survive and the
rings do not**, which is the whole point. Odesa: 3 454 turns, none winding, longest 11.5 m, 301 cut.

**Weighing the curve against the pivot by what each costs was tried first and is not here.** Turning on the
spot is not free and the exchange rate is exact — the tightest circle anything holds is its pace over its
turn rate, so a radian pivoted costs exactly `WalkerTightestTurnM` of walking — which makes the arithmetic
tempting: take the curve where it is no longer than the straight plus the pivots at its ends. **What it
answers is the wrong question.** At a walker's turn rate the pivot wins nearly every corner, so Test's cut
turns went from 40 to 165 and a quarter of the town's turns became a body stopping to pivot at a corner it
was already rounding. Worse, it cannot even see the ring it was brought in for: a full turn on the tightest
circle costs *precisely* what pivoting through it costs, by that same exchange rate, so the two tie and the
ring passes. The economics are real and they are in the requirement; what enforces the shape is the heading.

## 2026-09-17 — a line through a place is weighed as ground as well as as heading, and the course is rounded

The heading bound alone left two shapes on the pavement, and **both of them are ground and not heading**.

**A course kept its notches, and a notch is a spike.** The offset was taken with no smoothing, on the ground
layer's grounds (TER-3c.3): a corner opening away from the town comes back as the arc of the distance moved,
so there is nothing to round. **But the corners turning *in* are the notches the fold cut left**, and at a
junction's inner corner that notch is a point of course standing in open pavement — a walk along it went out
to the tip and back. Asked for as round as the distance allows (`WalkLines.Smoothest`), the tightest the
courses turn anywhere on Test went from 0.29 m to 0.31, which is now outside the circle the feet hold rather
than inside it, and the spike is gone from the picture.

**And a turn was bounded in heading and not in ground.** A course that meanders at a corner spends no heading
over what the turn's ends ask for — it just goes round the houses — so the winding bound could not see it:
Test had 7 turns running more than twice the straight between their own ends, the worst 3.5× over 13.5 m, and
Odesa 56 of them. **A turn is ground a walker crosses**, so the bound is on the ground too, and it is asked of
the whole line however that line was laid (`FootMovements.WorthWalking`) as well as of each corner in it.

**The figure is the shape's own and not a tolerance** (`Spline.HalfATurnOfItsChord`). An arc covers
`(θ/2)/sin(θ/2)` of its own chord — 1.11 at a quarter turn, π/2 at a half — and a half turn is the most a
corner asks for, so **a line over π/2 of the straight between its ends is not an arc between them at all**
but two arcs bowing out past the ground between them. Twice the straight was what the guard used to say, and
it is a fifth again too loose: at 2× the bulging turns at a merged corner passed, and at π/2 they do not.

| | Test | Odesa |
|---|---|---|
| turns over a half turn's share of their own straight | 7 → **0** | 56 → **0** |
| worst any turn runs against its straight | 3.5× → 1.4× | 3.4× → 1.5× |
| longest turn | 13.5 → 9.5 m | 14.6 → 9.7 m |
| worst pivot | 123.6° → **60.1°** | 143.0° → **60.0°** |
| turns cut to the straight | 42 | 297 |

**The worst pivot halving on both towns is the reading that says it worked.** What is left where a course is
refused is a straight meeting its lanes at whatever angle they stand at; the turns that now fall back to it
are the ones whose ends are a couple of metres apart across open pavement, and a straight between those meets
both lanes nearly along them. A town where that figure is 143° has a walker spinning on the spot at a corner.

**Preferring whichever of the curve and the pivot is quicker was tried here too, and is not here.** The
exchange rate is exact — a radian pivoted costs `WalkerTightestTurnM` of walking (`Spline.PivotedM`) — so the
curve can be weighed against standing and turning. Measured, it takes the town the wrong way: Odesa's turns
cut to the straight went from 297 to 889, a quarter of them, and the tightest curve anywhere fell from 0.28 m
to 0.14 because the refusals landed on course routes carrying the town's own corners. **The pivot is what is
left where no line is worth walking, not what a walkable curve is talked out of.**

**What is left is measured and not fixed: 42 of Test's 736 turns and 297 of Odesa's 3 454 are cut to the
straight**, and **two of Odesa's ways are laid as a straight for want of a course their two ends share**, the
longer of them 154.50 m at 2028,1724. The readings are `--bench census`'s `cut off` row and the `of them`
row under `walks`.

## 2026-09-17 — a rough joint is not laid at all, and the place is short a movement instead

**WLK-14 is the owner's, stated in as many words**: no rough connections between lanes, all good curves. So
every joint of every turn is read against the lane either side of it and **what cannot be joined without a
pivot is not laid** (`FootMovements.Smooth`). Both towns now read nought rough joints, of 1 388 and 6 312.

**It is refusal and not better curve-fitting, because the fitting was measured first.** Of Test's 42 turns
that had come out as a straight, **none** was a pose pair a curve exists for; of Odesa's 297, three were. The
rest ask for something no line tangent to both ends can do: **set off behind where the walk arrived** (9 and
28) or **turn a body round on the spot** (1 and 6), the remainder being turns onto a lane welded to one point
(WLK-12), which faces nowhere at all. A biarc family search would have bought three movements on a city.

| | Test | Odesa |
|---|---|---|
| rough joints | 20 → **0** of 1 388 | 75 → **0** of 6 312 |
| movements laid | 736 → 700 | 3 454 → 3 166 |
| movements refused | 36, 4.9% | 288, 8.3% |
| places nothing turns through | 0 | 4 → 6, two of them handed more than one way |
| km of turn line | 3.04 → 2.88 | 13.88 → 12.55 |

**What it costs is named rather than hidden.** Odesa gives up 8.3% of its movements and **two places a walk
can arrive at and not leave** — the reading is the `refused` row beside `turns`. The road side pays the same
kind of price at the same kind of corner: 155 of Odesa's driving lanes are offered one movement because the
junction refused the others as too tight.

**The remedy for the rest is where the points stand, not how the line is drawn.** A movement that asks to set
off behind its own arrival is a place handing its ways over at points several metres apart (WLK-9); pulling
those in would give most of the refused movements a corner to be laid on, and it moves every crossing head in
the town, so it is the owner's to ask for.

## 2026-09-17 — the joint between a lane and the turn after it is measured, and a course has to be a curve

**Every bound so far read a line and none of them read the joints between the lines.** The `turned at` row
measures how far a turn kinks inside its own line, so a walk arriving at a point and setting off on a line
pointing somewhere else was invisible: Test had a lane arriving and a 5.27 m turn leaving **180° the other
way**, which is the shape a picture shows and no figure named. `pivoting at` reads both joints of every turn —
the lane in against the turn, the turn against the lane out — and counts the ones past half a degree.

**What it found is that a course route was being taken even when it could not be joined.** `Joined` lays a
corner onto the course at each end and falls back to the straight between the two points where no corner
holds; that straight points wherever the two points stand, including straight back down the lane. **A route a
walk pivots onto and then walks the long way round is worse than the one pivot the straight costs**, so the
course is now taken only where both of its corners hold.

| | Test | Odesa |
|---|---|---|
| joints past half a degree | 19 → 20 of 1 408 | 73 → 75 of 6 388 |
| worst joint | 180° onto 5.27 m → 176° onto **0.86 m** | 180° onto 4.10 m |
| worst pivot inside a turn | 60.1° → **0.0°** | 60.0° → **0.0°** |
| tightest curve a turn is joined on | 0.31 → 0.79 m | 0.28 m |

**No turn's own line kinks anywhere on either town now**, which is what the internal pivot going to nought
says: every turn that is laid is smooth from end to end, and what is left is the joint at either end of it.

**What is left is the movement itself and not the line.** A place hands its ways over at points metres apart
(WLK-9), so a movement can ask for a departure point *behind* the arrival — and the honest line for that is
the straight back to it, which is a walker turning round. Odesa still has one at 180° onto 4.10 m. **The lever
is where the points stand and not how the turn is drawn**, and moving them moves every crossing and every way
in the town, so it is not something to change while chasing a shape.

**Two readings were added because the shapes were being argued about from pictures.** `round about` counts
the lines that run over a half turn's share of the straight between their own two ends and names the worst;
`crossing` counts the lanes that cross themselves, which is the unit tier's question about the fixture town
(`PedestrianWaysTests`) asked of a shipped city — both towns read nought. **And the `wound` row over the ways
is a reading about the streets and not a defect**: a lane down a winding road spends its heading following
the road, which is why three of Odesa's lanes read 499° over while none of them crosses itself or bows out
past the ground between its ends.

**A crossing's course is the boundary, so a turn between two crossings runs along the kerb.** That is where
both of its ends already stand — WLK-9 strikes a crossing's pair *on* the boundary rather than off it — so
the turn is consistent with its own ends, and what would make it a walk on the pavement is a change to where
those points stand rather than to this.

**The turn back down the way it arrived on is left out**, as it is for a car. A pedestrian can of course turn
round; what is claimed is only that a place needs no line laid for it. **What that costs is the census's to
report, and it costs a place with one way handed over at it everything** — none of Test's 122 places and four
of Odesa's 991 are walked out of at all, and the reading names them as single-way places rather than leaving
a reader to wonder whether a corner is isolated.

## 2026-09-17 — a lane with nothing left of it is welded into one point, and the reading is a lane's

Where a walk turns into a corner rather than round one, the two ends of a lane stand on each other and what
the placement laid between them was a stride of walk joining two places that are one place. **So the two
points are welded into the place between them and nothing is laid down that lane** (WLK-12): a walk reaching
either of the ways either side of it has reached the other.

**The reading is a lane's and the first cut of it was a pair's, which never fired.** A pavement's two lanes
stand a walking lane apart across the kerb, and at a corner the walk turns *into* they pinch out at different
places — the concrete runs out at the offset the outer lane is walked at while the inner lane still has
metres of it. Asked of the pair, with both lanes having to be inside the figure, **the nearest way on Test
read 2.37 m and nothing in the town welded**; asked of the lane, the same town welds eight of them and the
nearest point a reader can see touching reads 0.03 m. **The count that caught it is the census's**
(`--bench census`): points of two different pairs standing within the figure, which said nine while the weld
said none.

**Every one of them was already being laid as something.** The eight lanes the weld now takes are eight of
the stubs the straight fallback was drawing — a way whose two ends fell on two lines of the same course with
a stride between them — so Test's count of ways laid as a straight for want of a course went from five to
none. **A stub of walk and a shared point are the two answers to that geometry, and the point is the true
one.**

**A second merge and not a widening of the first.** `Road.FootNodeMergeM` asks how near two *nodes* stand and
is answered at the width of a corner a walk crosses in two strides (WLK-3); this asks how near the two ends
of one *lane* stand and is answered at a stride, welding only where there is nothing left of it. Merging the
nodes instead would make two junctions one junction, which they are not — they keep their own crossings,
their own corners and their own arms, and all they give up is a lane's length.

**A dropped pair and a shared point are not the same thing**, so the two are asked apart (`Shares`): WLK-3's
dropped pair stands for nothing, and a shared point is somewhere a walk arrives. A pair with a shared point
still hands its way over — what it has lost is one of that way's two lanes.

## 2026-09-17 — a way between two points is a stretch of the boundary already moved off itself

The points were placed and nothing ran between them. What runs between them now is not a line fitted to
their two poses but **a stretch of the driven ground's own boundary moved off itself by the offset they
stand at** (`WalkLines`, `KerbLines.Between`, WLK-11). A biarc through the two poses was the obvious
alternative and is wrong at the only place it matters: a street is a curve and a mouth is a corner, so a
line that only has to *arrive* right cuts across the tarmac in between. **The boundary already is the walk's
shape** — the points were struck off it for the same reason (WLK-9) — so the way is the same reading
carried along instead of taken twice.

**The shorter of the two ways round a closed line, and it is not a tie-break**: a ring is the outside of a
block and the long way round it is the rest of the town. What the rule picks is what a walker would take —
along the street between two of its corners, round the corner between two arms, round the head of a
cul-de-sac.

**The move is the whole town's and is taken once per lane** (`BandShell.Outset`, which is what the ground
beside a road is struck with), and the stretch is cut out of the answer rather than the answer taken over a
stretch. A shape's offset is cut against every piece of the shape; a stretch moved on its own can weigh a
corner only against the pieces either side of it (`ArcOutset.Beside`), and **that is exactly what a corner
with a radius under the offset defeats** — the fold is deeper than the two pieces that opened it, no
crossing is found within them, and what is laid instead is the chord across it. **Every rounded mouth on the
Test map drew a knot of two or three chords over the pavement**, arriving at the point from the wrong side of
the corner. **What it costs is one move of the town's outline per lane** — at 1.00 m and 3.00 m, beside the
one the ground already takes at 4.20 m — which took the walk's whole share of opening Odesa from 35 ms to
91 ms of that map's 1.8 s (`--bench load`).

**A lane is laid without a direction and turned round after, and the direction is the way's rather than the
lane's.** Which way a lane is walked is the side the town keeps (TER-4a, WLK-8) and has nothing to do with
which of its two nodes the line happened to be cut from — a stretch is cut forward from whichever end the
arc begins at. **What settles it is the direction of the boundary under the way**, read once off the one
line every course was struck from: every ring of a merged shape is walked with the covered ground on its
right and moved off itself to the walker's left (`BandShell.Outset`), so every course is walked with the
tarmac on the same hand as the boundary beneath it, and the lane against the kerb is the one whose walker
has that tarmac on the keeping hand.

**Read off each lane's own line instead, the two lanes of a way disagree about which end they set off from**
— which is two walks in one direction and none in the other. A lane that fell back to a stride of straight
has no direction worth reading at all, and two courses cut apart from one another can begin the same way's
arc at two different ends. **The same reading settles which side of a block the way runs down**, carried onto
each course as a place the way is known to pass: taken twice, one lane went round the corner and the other
round the rest of the block.

**Where the two ends fall on two different lines of the course there is nothing between them**, and the way
is laid as the straight between its points — one line drawn where a walker would go rather than none at
all. **A straight is weighed by its length and not counted**, because two points a stride apart are a
stride apart along any line between them: five of the Test map's 182 ways and forty-four of Odesa's 1 171
are laid as one, and **two of all of them are longer than the pavement is wide** — the same two chords of a
curving street that the reading before this one left, the longest 154 m. That is the figure `--bench census`
reports.

## 2026-09-17 — a wedge is a corner only where the arms either side of it carry a walk

`WedgeNeighbour` took the arms of a junction that **stood a node**, sorted them by bearing and paired each
consecutive two. A bay and a roundabout's ring stand none (WLK-2), so at a roundabout's arm the ring's two
ends were not there to be counted and the arm's own two sides came out consecutive — a corner from one side
of the road to the other, round a head that is not there. Laid along the boundary, that way **set off round
the outside of the whole town**: 1 774 m of walk from one side of an arm to the other.

So every arm is counted and only the pairs that both stand are joined. **An arm's mouth is a hole in the
ground the walk goes round**, whoever may walk on it, so a wedge reaching over one bounds no corner at all.
A dead end still pairs its own two sides, its junction having the one arm and the wedge really being the
head of the street.

## 2026-09-17 — a node hands over at a pair of points per way

The ways were laid, and the points they were laid between were the wrong points: one point per way at one
reach out of the merged junction, with the pavements, the zebras and the corner walks strung between them.
**A way is walked a lane each way, and one point cannot say where two lanes start.** A line struck between
single points is a centreline the two directions are then offset off, so where a way *enters* a node was an
answer the mitre had to invent rather than one the node gave it.

So the node hands over at **a pair, laid across the way it belongs to** (WLK-9): along the boundary for a
crossing, so the way leaves square over the carriageway; square to the boundary for the road and the
junction, so the way leaves along the kerb. **Per node and not per pedestrian junction** — the merge (WLK-3)
says which nodes are one place, but which side of a node is the road's and which the junction's is a fact
about the one road end it came off, and a merged corner of two arms has no single answer to it.

**A merge drops a pair rather than moving one.** Two nodes at one corner are one place (WLK-3), and what
that costs them is the one way each that ran to the other — so the corner hands over to four ways and the
eight points left stand exactly where the boundary put them. **Which way that is, is structural and is not
read back off the points**: each of the three has a node it runs to, and a pair is dropped when that node is
now this same place. Read off the geometry instead — the pair pointing most nearly at the other node — a
skew corner picks the wrong one of the three, and a corner whose two nodes stand almost on top of one
another has no direction to read at all.

## 2026-09-16 — a walking lane is laid beside its stretch and not moved off it a piece at a time

**The pavement read rugged in a frame, and the line under it was not.** A stretch turns real corners, and
the two directions were struck off it with `Spline.OffsetInto`, which moves each piece and joins nothing —
so **the line a body is actually held on came apart at every one of them**: 2 475 of 10 606 lane joints
stood open past a centimetre and the worst was 1.88 m of nothing on the outside of a hairpin, with the
inside folded through itself. Laid beside the stretch instead (`ArcOutset.Beside`), the corner is the arc of
the lane's own offset where it opens and the crossing of the two pieces where it closes, and what is left
open is the stretch's own worst joint inherited and nothing this construction made.

**A corner of the pavement is where the pavement turns a corner**; what was wrong was the lane, not the line
it is laid beside. Rounding the corners out of the *stretch* was tried and is not here.

## 2026-09-07 — a walking lane turns the corner the pavement turns, at its own offset

Every lane end gave up half a band to its corners, so the walk left the line it was laid off and the outside
of every bend was pavement no lane reached. That is the figure the *crossing* case needs, charged to corners
that are not crossings. What a corner costs is the arc it is turned on — `offset × tan(half the turn)` —
which is nothing where the walk runs straight on and grows only as the corner sharpens. A crossing keeps its
band, where the overlap really is a band. The gate that moved is that a lane never strays further from its
stretch's line than its own offset: it failed on every shipped map at up to a third of a metre, and is the
whole of what following the pavement means.

