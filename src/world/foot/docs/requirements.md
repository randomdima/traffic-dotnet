# world/foot — the walking network

**What a walker follows.** The ground a walker stands on is the terrain's
([TER-3c](../../terrain/docs/requirements.md#the-pavement)) and the paint a zebra is made of is the road's
([TER-6](../../road/docs/requirements.md)); what is stated here is the **network** laid over them — where a
walk may go, where it chooses, and where it crosses.

**What the town lays is the courses and the crossings cut into them.** Two lines, each the whole driven
shape's boundary moved off itself at one lane's distance (WLK-1), parted wherever a zebra meets them and
joined across the carriageway by the ways over it (WLK-15) — so a walk may leave the block it is on. **The
node network is not laid**: WLK-1a and everything under it states a construction this slice holds in code
and the suite asks its questions of, and which no town stands at present
([docs/index.md](../../../../docs/index.md#known-gaps) names it). The places a town really crosses at are
read off its kerb ends instead (WLK-10, `CityGen.KerbEnds`).

Every rule carries a rung ([priority.md](../../../../docs/priority.md)); all of these are the assistant's.

## Where the pavement comes from

**WLK-1** `P4` **A pavement is the driven ground's own boundary moved off itself, once per walking lane, and
it is placed off nothing else.** The lines a car is driven on lay a shape, its outside is one boundary
([TER-7b](../../terrain/docs/requirements.md)), and the move taken at each lane's own distance
(`WalkingLaneAtM`) is that lane's course — so **two moves are the whole town's pavement**, both sides of every
road at once, because a boundary has the road between its two hands and the move keeps it. **Nothing here
asks about a road**: there is no end to stand a corner on, no arm to count, no bay or ring or rank to exclude,
and **every line the move closes is a lane a walk may run down**. A street's pavement crosses a bridge deck
because the driven ground does (TER-3b.1), the walk goes round the mouth of a rank because the tarmac does,
and a course round a block is the frontage of every street on it.

**What the move owes is closure, and the instruments report it** (`--bench census`): a course that comes back
as a run rather than a ring is a pavement with two ends in the middle of the town. **Every corner of a course
is rounded, at the courses' own radius and not the ground's** (`Road.WalkRoundedM`, TER-3c.10) — a corner
turning away comes back as the arc of the distance moved, and one turning in is the notch the fold cut left,
which left sharp is a spike of course standing in open pavement. **The figure is the walk's because a course
is walked rather than built**: nothing is laid along it and no kerbstone bends to it, so what it owes is a
line nobody has to pick their way round rather than agreement with the concrete beside it.

**And the move is the whole shape's, taken once for the town, and never a stretch of it moved on its own.**
A shape's offset is cut against every piece of the shape, because a feature anywhere may swallow an offset
anywhere: two kerbs nearer than twice the distance come back as one line, and a corner turning tighter than
the distance comes back as whatever the shape leaves rather than as a fold. **A stretch moved by itself knows
none of that** — it can weigh a corner only against the pieces either side of it — so it folds through itself
wherever the offset swallows more than that.

## Where the node network came from

The node network itself — the pedestrian nodes, their merge and the points they hand over at (WLK-1a,
WLK-2, WLK-3, WLK-9, WLK-12) — **is not laid by a town at present**
([docs/index.md](../../../../docs/index.md#known-gaps)). What a town does lay is the courses (WLK-1,
WLK-8), the crossings cut into them (WLK-10, WLK-15) and the turns a walk is taken through a place on
(WLK-11, WLK-13, WLK-14).

**WLK-1a** `P4` **The node network is placed off the road ends, and off nothing else.** A road is laid
first, its lanes end at the connection points its arms were drawn with (TER-5d), and the walk is then laid
between the places those ends put it — a pair of pedestrian nodes at each end of each street (WLK-2), and the
points each node hands its ways over at. **Nothing there reads the ground and nothing there wraps a shape**: where a walk may
run is settled by the road being there, so a street's pavement crosses a bridge deck because the street does
(TER-3b.1), and no second reading of the terrain can take away pavement the town says is there.

**WLK-2** `P4` **A road end carries a pair of pedestrian nodes where the street it belongs to ends**: one
either side of the carriageway,
standing `Road.FootNodeBackM` back along the road from the end its lanes hand the car over at, and
`Road.FootNodeAsideM` beyond the carriageway's own edge. Both figures are authored — where the corner of a
street stands is a choice about the town and not a figure to derive — and the node's own place is the point
a walk beside that road begins at. **A bay carries none** (GEN-53): a car park's space is ground a car is
put down on rather than a street with a frontage, so the walk goes past the mouth of a rank and not round
every space in it.

**A roundabout's ring carries none either** (GEN-19): it is a carriageway with no frontage on either hand,
so there is no walk beside it to stand a corner on. **Its arms are ordinary and carry theirs** — an arm
really does meet the ring, so the corner it makes at the mouth is a corner like any other, and what is asked
is about the road rather than about its junction.

**A junction cut into a street that carries on past it carries none either** (GEN-52) — a car park's, which
is a rank of bays hung off a street rather than a place a street stops. **Nothing there is a corner**: the
mouth of a rank is a hole in the pavement a walk goes round, not ground it steps off the kerb at or turns
through, so no node stands there, no zebra is asked for (WLK-10), and **the walk down the street runs through
the junction in one stretch** (WLK-11). **It is not the same question as whether a junction forks**: a bend
and a dead end fork nothing and are ordinary ends of a street, while a car park's node is a street's middle.

**And the pieces a cut parted a street into are one street.** The node a walk reaches down a road is the one
on the same physical side at the far end of that road, and at the far end of the road after it wherever the
road runs into a cut, and so on until an end that stands one — **a side of a street is walked from one end of
it to the other whatever it was parted into**. The two arms of a cut leave on opposite bearings, so keeping
to one side of the street means naming the other hand at each hop.

**What carries no walk is still an arm of the junction it meets**, and a wedge with one of them on either
side of it is no corner. The arms of a junction taken in the order their bearings turn bound one wedge
between each consecutive two, and **a walk round the junction joins that pair only where both of them stand
a node** — an arm's mouth being ground the walk goes round rather than ground it turns at. It is what leaves
a roundabout's arm with no walk from one of its sides to the other: between them stand the two ends of the
ring, and the way round would be the way round the town.

**A cut junction pairs nothing across its wedges**, neither of its two arms standing a node: what runs round
the mouth of the rank is the street's own walk and not a way from one side of a wedge to the other, so **a
town's pavement is broken by nothing a street runs past**.


**WLK-3** `P4` **Two pedestrian nodes standing within `Road.FootNodeMergeM` of one another are one place.**
A corner a walk crosses in two strides is one place to arrive at, not two to choose between — so the four
arms of a crossroads leave four corners and not eight, and the exits of two zebras that all but touch are one
place. **Asked of where the nodes stand and not of which junction they came off**, and transitively: two
junctions laid a few metres apart merge exactly as two arms of one do, and nothing has to decide which of
those a run of near neighbours is.

**The place stands midway between the nodes merged into it, and nothing they hand over at moves.** A point
was struck off the boundary where its own node stood (WLK-9) and the boundary has not moved, so the merge
changes where a walk *arrives* and never where it *leaves*.

**And a pair whose way ran to a node the merge has made this same place is dropped.** Such a way would join a
corner to itself. Where each of the three runs is structural — the crossing to the node across its own
carriageway, the road walk to the node at the far end of the same road, the junction walk to the node round
the corner — so it is never read back off the points. **It is what leaves the corner of a crossroads handing
over to four ways rather than six**: two nodes, two crossings, two roads, and the walk that would have run
between them gone.

## What a node hands over at

**WLK-9** `P4` **A pedestrian node hands over to three ways, and reaches each of them at a pair of points
struck off the driven ground's own boundary.** The three are the crossing over the carriageway, the walk
down the road, and the walk round the junction; **a pair and not a point, because every way is walked a lane
each way** (WLK-8) and the two points of a pair are the two lanes' own lines — the middle of each half of
that way's band, so a walking lane apart — laid *across* the way they belong to.

- **The crossing's pair lies along the boundary**, on it, its band centred on the place the node stands off
  it — so the way between them leaves square to the kerb and goes straight over the carriageway.
- **The road's pair lies square to the boundary**, `Road.FootConnectorAlongM` along it from that place and
  away from the junction, **its band laid against the kerb** rather than beyond it — so the way between
  them runs along the kerb with its near lane the width of half a lane clear of the tarmac.
- **The junction's pair is the same, the other way along the boundary**: the same setback and the same two
  offsets, on the junction's side of the node rather than the road's.

**And each of those two points is then dropped onto the course its own lane is walked on** (WLK-11): the
course is the whole shape's offset at that lane's distance, which passes *near* the point struck square off
the boundary rather than through it — by a millimetre where the move left the corner under it standing, and
by a pavement's width where it swallowed one. **The point is moved to the line and the line is never fitted
to the point**, so the lane laid between two of them is a stretch of the course and nothing else. A crossing's
pair is not moved: it is struck on the boundary, which is the line that way is walked over.

**The boundary is the driven ground's own and never the road's half-width** ([TER-7b](../../terrain/docs/requirements.md)).
At a mouth the ground a junction's movements are driven over reaches past the arm's own edge, so a point
placed square to a centreline that runs straight on while the kerb turns into the corner lands inside the
tarmac — and a setback measured along the boundary follows the corner round while one measured along the
road does not. **It is what makes a corner a place**: taken from the road's line instead, a zebra's end lands
on the tarmac and the walk round a junction sets off across it rather than along it.

**Which of the town's kerbs is settled at the road's own edge, and only then where along it.** A node stands
`Road.FootNodeAsideM` clear of its own carriageway (WLK-2), and **in the crotch of a fork that figure reaches
over the arm opposite** — so the boundary nearest the *node* is that other street's kerb, and a pair struck
off it lays the crossing at a slant across the mouth and sets the walk down the wrong road. The place the
boundary is asked for at is the point on the node's own carriageway edge that the node stands off: the same
place on the road's line, the same hand, half the carriageway out instead of half the carriageway and the
figure beyond it. **It is a point of this road**, so nothing standing over the node can answer for it, and at
every end where nothing does the two questions have the same answer. **What is found is still a place on the
boundary** and never the edge itself, so a mouth still hands its pair over on the fillet.

**WLK-12** `P4` **The place a lane sets off from and the place it arrives at, standing within
`Road.FootConnectorMergeM` of one another, are one place.** Both are moved onto the point between them and
**nothing is laid down that lane**: a stride of walk between two places a stride apart is not a walk anybody
takes, so what was two ends with a lane between them is one place the ways either side of it meet at, and a
walk reaching it has reached both.

**Weighed and applied a lane at a time, not a pair at a time.** A pavement's two lanes stand a walking lane
apart across the kerb (WLK-8), and **where the walk turns into a corner rather than round one they pinch out
at different places**: the pavement runs out at the offset the outer lane is walked at while the inner lane
still has metres of it, so the outer lane's two ends meet and the inner lane's do not. Weighed on the pair,
every one of those reads as a way with something left of it and nothing ever welds.

**It is not the node merge and it does not cascade.** WLK-3 asks how near two *nodes* stand and answers it at
the width of a corner a walk crosses in two strides; this asks how near the two ends of *one lane* stand, and
welds only where there is nothing left of it. And a point is the end of exactly one lane whose relation is
its own inverse, so a weld joins two points and there is no run of near neighbours to close.

**A shared point is somewhere a walk arrives and is not a point standing for nothing**, which is what parts
it from the pair WLK-3 drops. The pair it belongs to still hands its way over; what it has lost is the length
of one of that way's lanes. **How near two points stand that no weld will join is the census's to report**
(`--bench census`) rather than a figure to quote here.

## What runs between two points

**WLK-11** `P4` **A way is laid between every pair of points still handed over at, a lane at a time**
(WLK-9, WLK-3): the <em>n</em>th point of one pair to the <em>n</em>th of the other, both being struck the
same way round off the boundary, so a way's two lanes run beside one another rather than crossing in the
middle of it. **A way is one line and not two** — it is laid once from either of its two ends, which name
each other.

- **A crossing is the straight between its two points.** What it runs over is the carriageway, which the
  boundary is the edge of rather than a line across, and a zebra is painted over the same run (WLK-10).
- **A walk down a road or round a junction is a stretch of its lane's own course**: the driven ground's
  whole boundary moved off itself by the offset that lane's points stand at (WLK-9), read between the two
  places those points fall on it — so it bends where the kerb bends and keeps the same distance off the
  tarmac along its whole length. **The shorter of the two ways round a closed line**, a ring being the
  outside of a block and the long way round it the rest of the town. **A walk down a street is one such
  stretch however many roads the street was parted into** (WLK-2), so it runs round the mouth of every rank
  between its two ends as the kerb does. **How long the longest of them is, and how far round the houses it
  goes, is the census's to report.**

**The boundary carries the shape and a line fitted to the two ends does not.** A way that only has to leave
one point and arrive at the other may take any route between them, and the route it takes cuts the corner at
a mouth and the bend of a street; what is wanted is the line the walk is already placed off.

**And the lane is that stretch and nothing else — nothing is fitted to either of its ends.** Its two points
were dropped onto this very line when they were struck (WLK-9), so the stretch already begins and ends where
the place hands the lane over. **A corner between a point and a line passing near it is a line the shape
never drew**, and a lane carrying one is a walk laid over pavement the offset had already answered for.

**And the move is the whole shape's** (WLK-1), which is why a stretch is read off a course rather than
struck for itself: a fold is a knot of walk laid over the pavement it should have run down.

**Where the two ends fall on two different lines of that course there is nothing between them, and no lane is
laid.** A straight drawn between them instead is a walk over whatever stands in the way — a carriageway, a
block, the water — and a line nobody would follow is worse than a way the network says it has not got. **How
many of those a town has is the census's to report.**

## What is walked through a place

**WLK-13** `P4` **A place is walked through, and what it is walked through on is a turn for every lane
arriving at it and every lane setting off from it** (WLK-3, WLK-11) — so a walk reaching a corner off any way
may leave it by any other, and between the ways and the turns every walk in the town is a run of lines that
meet end to end. **Per place and not per node**: two nodes at one corner are one place to arrive at, so a
walk arriving off either may leave by a way either of them hands over.

**A turn back down the way it came is not one.** Every other pair of an arrival and a departure is, the pair
leaving by another way of the same kind at a merged corner included; what is left out is only the walker who
arrives and at once retraces the lane beside the one they came in on. **How many places nothing can be
walked out of is the census's to report** — such a place is one with a single way handed over at it, whose
only turn would be that one.

**A turn is the curve between its own two poses, no tighter than the circle the feet hold at pace**
(`WalkerTightestTurnM`). A turn is a couple of metres of ground between the lane arriving and the lane
leaving far more often than it is a walk down a street, and **one curve between the two is the line a walker
takes**: it leaves along the lane it arrives on and joins along the lane it sets off down, so a walk over the
network carries straight on through a place rather than stopping to pivot at each end of it.

**Only what no such curve reaches is routed along a course** (WLK-11, WLK-9) — the course the departing lane
is walked on, with a curve onto it at each end and the course itself giving up the ground those curves need.
That is the long turn: the two ends metres apart round a corner, where one curve between them would cut the
corner and at a mouth cut across the carriageway. **A crossing's course is the boundary itself**, its pair
being struck on that boundary rather than a lane's offset off it.

**Routed along a course whatever its length, a turn is worse and not better**: a turn onto a crossing came
back as an S where one arc would do, and a turn at a corner as a loop where the course doubled back on
itself. **The curve is what is preferred and the course is the fallback**, not the other way round.

**And a course is taken only where it is a curve at both of the turn's own ends.** What the course is for is
the curve a line between the two poses would not lay; a route a walk has to pivot onto and then walk the long
way round is worse than the one pivot the straight costs. **How far a walk turns at the joints between the
lines it is made of is the census's to report** — the joint between a lane and the turn after it is a pivot
nothing else measures.

**Nothing walked through a place spends more than half a turn of heading over what its own two ends ask
for, or covers more of the straight between them than a half turn covers of its chord.** The heading
between two poses is never more than half a turn, so a line spending more than that over it is turning twice
where once would do — and at the limit it is a ring a body walks all the way round to arrive where it already
stood. **The ground is the bound the heading cannot see**: a course that meanders at a corner spends no extra
heading and still runs three times the straight its ends stand apart, and a turn is ground a walker crosses.
**Neither figure is a tolerance** — an arc of any turn covers `(θ/2)/sin(θ/2)` of its own chord and no more,
which at a half turn is π/2, so a line past either bound is not an arc between its two ends at all. **Both
are on the whole line and not on each part of it**: two curves onto a course with a stretch of course between
them can each be a corner while the three together are that ring. **How much heading the town's walks spend
against how much their ends ask for, and how far round the houses they go, is the census's to report.**

**WLK-14** `P1` **A walk carries on at every joint of every line it is taken through a place on, or that
turn is not laid.** The lane arriving against the turn's own first piece, and the turn's last against the
lane setting off, each within the angle at which a joint is one line carrying straight on — **no rough
connection between two lanes anywhere in the town**. Turning on the spot is possible and it is not free, but
a line handed out with a pivot built into the middle of it is a body stopping dead at a corner, and **what
cannot be joined is a movement the place does not offer** rather than a shape the walk has to carry. It is
the same answer the road side gives a junction movement its steering cannot hold (TER-5f).

**What that costs is connectivity, and the instruments report it rather than a rule hiding it**: how many
turns were refused, how many places nothing turns through at all, and how many of those were handed more than
one way — a place of the last kind is one a walk can arrive at and not leave. **A turn whose two poses no
curve joins is almost never a curve that could be fitted better**: it is a movement asking to set off behind
where it arrived, or to turn a body round on the spot, which is the place's own geometry (WLK-9) and not this
line's.

**A turn's line begins and ends exactly where the lanes either side of it do**, which is at the points the
place hands them over at (WLK-9): those points stand on the courses the lanes are cut from, so a lane begins
at its own point by construction (WLK-11) — there is no last stretch left for a turn to close and no
tolerance somebody reading the network has to allow for. **What that costs is turns**: a turn now sets off
along the course rather than along the boundary the point was struck off, and the movements no line will
join without a pivot are the census's to count (WLK-14).

## What the walk asks the road for

**WLK-10** `P4` **A zebra is wanted wherever a road end's two pedestrian nodes hand a crossing over to each
other, and nowhere else.** The band runs from the kerb one of them stands off to the kerb the other does,
and those two points are its middle; the pair each node hands over at are that band's own lane lines
(WLK-9, WLK-8). **Where a walk crosses is the whole of the placement** — a road end that hands no crossing
over asks for no paint, which is what leaves a bay, a roundabout's ring and a car park's cut with none
(WLK-2), none of them standing the pair that would hand one over.

**The paint is the road's and the placement is the walk's.** What a zebra *is* — a band of carriageway
pedestrians may walk over, the width it reaches, what a bar behind it does — is
[TER-6](../../road/docs/requirements.md); what is stated here is only which of them the town has and where.
**The answer goes down as points and never as anything of this slice's**, the road tier standing below the
walk ([slice-map.md](../../../../docs/slice-map.md)).

**Nothing refuses a band for what it lands on.** A zebra at a mouth reaches past the arm's own edge, because
the ground a junction's movements are driven over does — so two arms meeting at a sharp angle can lay paint
over the corner they share. **How far one overruns its carriageway is the census's to report** and not a rule
to bend here.

**And every end of every street has one.** A street is a road that carries a walk, counted from where it
really stops: a bay's end is no street's, nor a ring's, nor the mouth of a rank the street runs past — so
**the pieces a cut parted a street into are one street with two ends between them** (WLK-2). A street end
standing a pair that hands no crossing over is a corner a walk arrives at and cannot cross from.

**WLK-10a** `P4` **A street whose two stations stand closer together than `Road.CrossedOnceBelowM` is
crossed once, midway between them.** Two zebras a walker can stand between are one zebra in the wrong two
places: the two ends of a short street are cut by the same rule every other street's are, and what that
leaves is a choice between crossings neither of which is where anybody would cross. The one band is the pair
welded: it stands between the two ends and belongs to neither, and the traffic meets it whichever way it
drives.

**Nothing else follows the paint, and what is left behind falls back to the kerb.** A street crossed once is
still held at both of its ends ([TER-6](../../road/docs/requirements.md)): what holds a driver is the box in
front of them and not the zebra behind it. **But a station is a station because a zebra stands at it** — the
bar holds a setback clear of that paint, and clear of the paint is how far out along the street the bar ends
up — so with the paint gone to the middle the hold goes to the last thing there really is on the way in,
which is the end of the road's own kerb. A bar left at the station would stand a carriageway's width out
from the box it is held for.

**The walk hands the road two answers**: where it crosses, and what each end of each street is held behind.
They are the same list everywhere else in a town, and the second carries whether there is paint at the place
at all — the road holds a setback clear of a band's near edge where there is one and clear of the place
itself where there is not.

## Where a crossing meets the walk

**WLK-15** `P4` **Each end of a zebra is a junction cut into the walk beside the road; it stands between the
lanes of that walk, hands over at a point per connected lane, and every lane at it is connected to every
other.** Six lanes meet at one: the walk arriving and the walk leaving on each of the pavement's two lanes,
and the crossing's two lanes over the road. **So each lane of pavement is cut twice** — into the walk up to
the junction, the stretch of it the junction owns, and the walk on past — and **each of the crossing's two
lanes hands over on the driven ground's own boundary** (TER-7b), where its paint stops. It is the road
side's rule for an arm (TER-5d) asked of a walk: a lane ends where it hands over, and the ground between two
such points belongs to the junction rather than to any one way through it.

**Its centre stands between the two lanes of the pavement**, half the walk's own width off the kerb on the
line the crossing runs along, and its connection points stand `Road.FootConnectorAlongM` either side of that
along each lane's own course. **It is what makes the place a place** rather than a point on one line: a
walker arriving on either lane may set off over the road and one arriving off the road may take either lane,
so the outer lane's way off the block it runs round is this junction and only this junction.

**Two mouths standing within `Road.FootNodeMergeM` of one another are one junction** (WLK-3), and
transitively. Two zebras at one corner are two crossings a walker steps between, not two places to arrive
at: merged, the walk is cut once rather than twice, **what stood between them is no longer a stride of
pavement with a hand-over at each end**, and each crossing hands over to the other directly. **The place
stands midway between the mouths merged into it and its own hand-over points stand a setback beyond the
outermost of them**, so every connection has the ground it would have had unmerged. **How many a town merges
is the census's to report.**

**Every arrival is connected to every departure but the one that would turn a walk back down the lane beside
it, or back over the paint it just crossed** (WLK-13). A pavement's two lanes are walked opposite ways
(WLK-8), so a way from one of them to the other at the same junction is a walker arriving and at once
retracing their steps — turning round, which is not a turn and is not laid. **What joins the two lanes is
the crossing**: out over the paint and back over it, which is the way round a walker would really take.

**A connection is the curve between the two poses it joins** (WLK-14, WLK-13): it leaves the lane it sets
off from along that lane's own heading and arrives on the next along its, so **the walk carries on at both
of its joints** and a body steps onto a crossing and off it without pivoting. It is the same thing a
junction's connectors are on the road side ([TER-5d](../../road/docs/requirements.md)), asked of a walk and
held to the circle the feet can hold at pace (`WalkerTightestTurnM`). **What no such curve joins is a
movement the place does not offer** and is not laid — a line handed out with a pivot built into the middle
of it is worse than a way the network says it has not got — and **how many those are is the census's to
report**. Where the walk turns a corner within a weld of a hand-over point the place carries two headings,
and the corner there is the pavement's own rather than the connection's.

**The paint stops at the kerb.** A crossing's own stretch runs from the boundary on one side to the boundary
on the other, which is the ground the stripes cover — so what a walker has to ask the road about (TER-5c.1)
is the carriageway and nothing else, and what carries on from there to the walk is one of the junction's own
connections and is pavement. The pair stands in the middle of each half of the paint's own depth (WLK-8),
and each of the two is walked the way that puts the traffic it is about to meet on its own walker's side
(TER-4a), so a crossing is two-way for the same reason a pavement is.

**The walk keeps its own line and is parted rather than laid again.** A course is the whole town's boundary
moved off itself once (WLK-1) and what a junction wants of it is somewhere to hand over, so the line is cut
at those places and every metre of it stays where the move put it. **It is the same injection a car park is
cut into a street with** ([GEN-52](../../../citygen/docs/requirements.md)): the road that already stands is
parted, keeps its own geometry, and what is new is the arms and the nodes between the pieces. **A place
within a weld of a corner is not cut at all**, the node being there already.

**Where a point falls on a line is that line's answer** (WLK-9): the boundary passes near the place the
paint ends rather than through it, and the course passes near the place the kerb was met — so each point is
moved onto the line it belongs to, no line is ever fitted to a point, and a junction stands on the ground
the town already laid. **A line answering from further off than `Road.CrossingMeetsTheWalkWithinM` is not
this crossing's** — it is the walk across the road or round the block — and nothing is run to it. **The
reach is asked where the line passes the junction**, not at the hand-over point a setback along it lands on:
the setback is where to hand over, and a course rounded back off a tight corner (TER-3c.10) still runs past
the place it was rounded away from.

**What that costs is the reading that could not be taken and never more.** A mouth is the paint's own end and
stands on the boundary, so a crossing whose boundary answers from too far off has nowhere to begin and is not
laid at all, both its lanes and both its ends. **A hand-over point is a place on one lane's course, so a
course out of reach costs that lane its connections at that junction and nothing else**: the place stands,
the paint is laid, the lanes that answered are connected as they were, and the lane that did not is left
uncut and walks past. **A course is cut away from a tight corner by the rounding its own move was struck
with** (TER-3c.10) — the walk being out of reach is the town's geometry and not a fault in the crossing, and
a junction deleted for it is a street with no way over it. **How many of each the town has is the census's to
report.**

## What a walk is walked at

**WLK-8** `P4` **A pavement is walked a lane each way, and each lane's own line is one whole move of the
boundary** (WLK-1) — so the two lanes stand a lane's width apart, two walkers passing each keep to their own
and neither is on the other's ground. **A lane is made a lane where it is struck and is not cut out of a
band afterwards**: nothing between the move and the walker moves the line again, because an offset taken a
stretch at a time folds through itself wherever the shape swallowed more than the pieces either side of it
could see. A pavement's band is the walk's width and a crossing's is the paint's depth, so one statement
covers both, and **nothing asks the ground how much room a lane has**: a lane is laid where a lane may be.

**A lane is walked one way, and which way is the side the town keeps** (TER-4a): a walker keeps the hand a
driver keeps, so **a lane is walked the way that leaves it on its own walker's side of the pair it belongs
to**. One statement covers a pavement, whose two lanes stand across the kerb, and a crossing, whose two
stand along it — walking a pavement with the kerb on the keeping hand and crossing with the traffic one is
about to meet on it. **It is not the order the way was laid in**, which is an artefact of which of its two
nodes came first.

**And it is one way and not two, so there is no way back down it.** A move comes back wound with the driven
ground on one hand throughout (TER-3c.9), so which way a whole lane is walked is one reading taken once for
the town and not a decision at each piece of it — and the lane beside it is the other move, walked the other
way, rather than this one read backwards. A walker turns round by reaching the end of a lane and setting off
down the one going back.

**It is what places the points of WLK-9**: a pair is that way's band with a lane's line in the middle of each
half of it, which is the same statement made at a node rather than along a stretch.
