# world/foot — the walking network

**What a walker follows.** The pavement is the terrain's
([TER-3c](../../terrain/docs/requirements.md#the-pavement)), though a walker reads none of it (TER-2), and
the paint a zebra is made of is the road's
([TER-6](../../road/docs/requirements.md)); what is stated here is the **network** laid over them — where a
walk may go, where it chooses, and where it crosses.

**What a town lays is the courses and the crossings cut into them**: two moves of the driven ground's
boundary, each closed line one lane walked one way (WLK-1, WLK-8), parted wherever a zebra meets them and
joined across the carriageway by the junction at each end of it (WLK-15) — so a walk may leave the block it
is on. Where a town crosses is read off its kerb ends (`CityGen.KerbEnds`, WLK-10), which stand only where
three or more roads meet.

**The node network is held in code and laid by no town**
([docs/index.md](../../../../docs/index.md#known-gaps)): the pedestrian nodes, their merge, the points they
hand over at, the ways between those and the turns through a place (WLK-1a, WLK-2, WLK-3, WLK-9, WLK-11,
WLK-12, and WLK-13 as `FootMovements` lays it), and the zebras its node pairs ask for
(`FootConnectors.Crossed`). The unit tier still asks its questions of it off a plan of
its own; no instrument reads it, there being no town with one to read.

Every rule carries a rung ([priority.md](../../../../docs/priority.md)); all of these are the assistant's
but WLK-14, which is the owner's.

## The pavement

**WLK-1** `P4` **A pavement is the driven ground's own boundary moved off itself, once per walking lane, and
it is placed off nothing else.** The lines a car is driven on lay a shape, its outside is one boundary
([TER-7b](../../terrain/docs/requirements.md)), and the move taken at each lane's own distance
(`WalkingLaneAtM`) is that lane's course — so **two moves are the whole town's pavement**, both sides of every
road at once, because a boundary has the road between its two hands and the move keeps it. **Nothing here
asks about a road**: there is no end to stand a corner on, no arm to count, no bay or ring or rank to exclude,
and **every line the move closes is a lane a walk may run down**. A street's pavement crosses a bridge deck
because the driven ground does (TER-3b.1), the walk goes round the mouth of a rank because the tarmac does,
and a course round a block is the frontage of every street on it.

**The move is the whole shape's, taken once for the town, and never a stretch of it moved on its own.** A
shape's offset is cut against every piece of the shape; a stretch moved by itself can weigh a corner only
against the pieces either side of it, so it folds through itself wherever the offset swallows more than
that. **What the move owes is closure, and the census reports it** (`--bench census`): a course that comes
back as a run rather than a ring is a pavement with two ends in the middle of the town. **Every corner of a
course is rounded, at the courses' own radius and not the ground's** (`Road.WalkRoundedM`, TER-3c.10).

**WLK-8** `P4` **A pavement is walked a lane each way, and each lane's own line is one whole move of the
boundary** (WLK-1) — so the two lanes stand a lane's width apart, two walkers passing each keep to their own
and neither is on the other's ground. **A lane is made a lane where it is struck and is not cut out of a
band afterwards**: nothing between the move and the walker moves the line again. A pavement's band is the
walk's width and a crossing's is the paint's depth, so one statement covers both, and **nothing asks the
ground how much room a lane has**: a lane is laid where a lane may be.

**A lane is walked one way, and which way is the side the town keeps** (TER-4a): a walker keeps the hand a
driver keeps, so **a lane is walked the way that leaves it on its own walker's side of the pair it belongs
to** — a pavement with the kerb on the keeping hand, a crossing with the traffic one is about to meet on it —
and never the order the way happened to be laid in. **It is one way and not two**: a move comes back wound
with the driven ground on one hand throughout (TER-3c.9), so which way a lane is walked is one reading taken
once for the town, and the lane beside it is the other move walked the other way rather than this one read
backwards. A walker turns round by reaching the end of a lane and setting off down the one going back.

**WLK-16** `P3` **The walk shares ground with the traffic on the paint and nowhere else.** No way a walker is
laid on but a zebra's own lanes has a ribbon lying over one the traffic drives, deeper than a touch
([TER-5c](../../road/docs/claims.md)) — so off the paint no walker's claim is ever weighed against a car's,
and neither body stands on the other's ground. **It is asked of the ribbon and not the line**: a way's ground
is its line swept to its own width (TER-4c.4), so a line that never leaves the pavement can still carry its
band over the kerb. The census reports the pairs that break it (`--bench census`).

**A lane lies against the kerb and not over it**: the lane beside the road stands half its own width off the
boundary (WLK-1), so its edge is the carriageway's. **And a turn off it keeps that edge**: a turn on a circle
of half the band pivots about its own inside edge, which on that lane is a point of the kerb — where any
wider circle swings the inside edge out past it, onto the road, and a tighter one folds it back onto the walk.

## Where the walk crosses the road

**WLK-10** `P4` **A town's zebras stand where its kerb ends put a station, and nowhere else**
(`CityGen.KerbEnds`). A station is struck across each end of each street at a box where three or more roads
meet, `Road.FootNodeClearM` out along the road past the further of the street's two kerb ends, and runs from
the carriageway's edge on that kerb's side to its other edge; the band runs between those two points.
**Where a walk crosses is the whole of the placement** — an end the kerb ends put no station at asks for no
paint, which is what leaves a bay, a roundabout's ring and every end at a bend or a dead end with none.

**The paint is the road's and the placement is not.** What a zebra *is* — a band of carriageway
pedestrians may walk over, the width it reaches, what a bar behind it does — is
[TER-6](../../road/docs/requirements.md), and so is the band that overruns its carriageway at a mouth; what
is stated here is only which of them the town has and where. **The answer goes down as points and never as
anything of this slice's**, the road tier standing below the walk ([slice-map.md](../../../../docs/slice-map.md)).

**A street end at a box that forks nothing has none.** A bend and a dead end carry the traffic straight
through, so the kerb turning there is a corner the pavement turns with rather than a place the walk stops and
is crossed; and a street runs past a car park with no end there at all, the rank's mouth being a hole in the
pavement. **The node network's own placement** — a zebra wherever a road end's two pedestrian nodes
hand a crossing over to each other (`FootConnectors.Crossed`) — is held in code with the rest of it and laid
by no town (below).

**WLK-10a** `P4` **A street whose two stations stand closer together than `Road.CrossedOnceBelowM` is
crossed once, midway between them.** Two zebras a walker can stand between are one zebra in the wrong two
places: cut by the same rule every other street's ends are, a short street leaves a choice between
crossings neither of which is where anybody would cross. The one band is the pair welded: it stands between
the two ends, belongs to neither, and the traffic meets it whichever way it drives.

**Nothing else follows the paint.** A street crossed once is still held at both of its ends
([TER-6](../../road/docs/requirements.md)), and since a station is a station because a zebra stands at it,
the hold there goes to the end of the road's own kerb — a bar left at the station would stand a
carriageway's width out from the box it is held for. **So the kerb ends hand the road two answers**: where
the walk crosses, and what each end of each street is held behind — the same list everywhere but on a
street crossed once, the second carrying whether there is paint at the place at all.

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
along each lane's own course — so a walker arriving on either lane may set off over the road, and one
arriving off the road may take either lane.

**Two mouths standing within `Road.FootNodeMergeM` of one another are one junction** (WLK-3), and
transitively: two zebras at one corner are two crossings a walker steps between, not two places to arrive
at, and merged each hands over to the other directly. **The place stands midway between the mouths merged
into it and its own hand-over points stand a setback beyond the outermost of them**, so every connection has
the ground it would have had unmerged.

**Every arrival is connected to every departure but the one that would turn a walk back down the lane beside
it, or back over the paint it just crossed** (WLK-13). A pavement's two lanes are walked opposite ways
(WLK-8), so a way from one to the other at the same junction is a walker turning round; **what joins the two
lanes is the crossing** — out over the paint and back over it.

**A connection is the lane's own course and one turn onto the paint** (WLK-14, WLK-13, WLK-16), as a
junction's connectors are on the road side ([TER-5d](../../road/docs/requirements.md)): down the stretch of
course the junction owns, from the first place one arc no wider than half the walk's band and no tighter than
the feet can hold at pace (`WalkerTightestTurnM`) turns it onto the paint's own line, and straight down that
line to the kerb — so a body steps onto a crossing and off it without pivoting, and on no ground the lane and
the paint do not already cover. **Where no place on the course turns that tight onto the paint** — a course
rounded well back from a tight corner — the connection is the curve straight from the hand-over point, laid
only where its band stays off the road. **What neither joins is a movement the place does not offer** and is
not laid. A connection between two crossings merged into one place is the same turn between their two lines,
**widened as far as the two fit where the tight one would leave the walk**; where no turn stays on it, it is the
lane's own course between them — up one paint, one turn onto the course, one turn off it down the other — and
last the curve straight from one paint to the other.

**No connection goes beyond the pavement.** Whatever shape it takes, its band stands off the verge the whole
way — no further past the walk's outer face than a touch, the face the ground answers the walk off (TER-3c.3) —
and a shape that would leave it is not laid. So where two crossings stand back from a corner further than the
walk is wide, the walk between them goes round the corner on the pavement rather than across the grass the
two paint lines meet on.
Where the walk turns a corner within a weld of a hand-over point the place carries two headings, and the
corner there is the pavement's own rather than the connection's.

**The paint stops at the kerb.** A crossing's own stretch runs boundary to boundary, which is the ground the
stripes cover, so what a walker's secondary claims hold of the road (TER-5c.1) is the carriageway and nothing
else. Its
two lanes stand in the middle of each half of the paint's depth (WLK-8), each walked the way that puts the
traffic it is about to meet on its own walker's side (TER-4a).

**The walk keeps its own line and is parted rather than laid again** (WLK-1): the course is cut where a
junction hands over and every metre of it stays where the move put it — the same injection a junction is cut
into a street with ([GEN-52](../../../citygen/docs/requirements.md)). **A place within a weld of a corner is
not cut at all**, the node being there already.

**Where a point falls on a line is that line's answer** (WLK-9): each point is moved onto the line it belongs
to, and no line is ever fitted to a point. **A line answering from further off than
`Road.CrossingMeetsTheWalkWithinM` is not this crossing's** — it is the walk across the road or round the
block — and nothing is run to it. **The reach is asked where the line passes the junction**, not at the
hand-over point a setback along it lands on: a course rounded back off a tight corner (TER-3c.10) still runs
past the place it was rounded away from.

**What that costs is the reading that could not be taken and never more.** A crossing whose boundary answers
from too far off has nowhere to begin and is not laid at all. **A course out of reach costs that lane its
connections at that junction and nothing else**: the place stands, the paint is laid, the lanes that
answered are connected as they were, and the lane that did not is left uncut and walks past — a junction
deleted for it would be a street with no way over it. The census reports the merges, the zebras refused, the
lanes unreached and the connections no curve joins (`--bench census`).

## What is walked through a place

**WLK-13** `P4` **A place is walked through, and what it is walked through on is a turn for every lane
arriving at it and every lane setting off from it** (WLK-3, WLK-11) — so a walk reaching a corner off any way
may leave it by any other, and between the ways and the turns every walk in the town is a run of lines that
meet end to end. **Per place and not per node**: two nodes at one corner are one place to arrive at, so a
walk arriving off either may leave by a way either of them hands over.

**A turn back down the way it came is not one.** Every other pair of an arrival and a departure is, the pair
leaving by another way of the same kind at a merged corner included; what is left out is only the walker who
arrives and at once retraces the lane beside the one they came in on.

**A turn is the curve between its own two poses, no tighter than the circle the feet hold at pace**
(`WalkerTightestTurnM`): it leaves along the lane it arrives on and joins along the lane it sets off down, so
a walk carries straight on through a place rather than stopping to pivot at each end of it. **Only what no
such curve reaches is routed along a course** (WLK-11, WLK-9) — the course the departing lane is walked on,
with a curve onto it at each end and the course giving up the ground those curves need — and **only where it
is a curve at both of those ends**. The curve is preferred and the course is the fallback: routed along a
course when one arc would do, a turn comes back as an S or a loop, and a route a walk pivots onto and then
walks the long way round is worse than the one pivot the straight costs. **A crossing's course is the
boundary itself**, its pair being struck on that boundary rather than a lane's offset off it. **A turn's line
begins and ends exactly where the lanes either side of it do** (WLK-9), so no tolerance is left for anybody
reading the network to allow for.

**Nothing walked through a place spends more than half a turn of heading over what its own two ends ask
for, or covers more of the straight between them than a half turn covers of its chord.** The heading
between two poses is never more than half a turn, so a line spending more than that over it is turning twice
where once would do — at the limit a ring walked all the way round to arrive where it already stood. **The
ground is the bound the heading cannot see**: a course that meanders at a corner spends no extra heading and
still runs three times the straight its ends stand apart. **Neither figure is a tolerance** — an arc of any
turn covers `(θ/2)/sin(θ/2)` of its own chord and no more, which at a half turn is π/2, so a line past either
bound is not an arc between its two ends at all. **Both are on the whole line and not on each part of it**:
two curves onto a course with a stretch of course between them can each be a corner while the three together
are that ring.

**WLK-14** `P1` **A walk carries on at every joint of every line it is taken through a place on, or that
turn is not laid.** The lane arriving against the turn's own first piece, and the turn's last against the
lane setting off, each within the angle at which a joint is one line carrying straight on — **no rough
connection between two lanes anywhere in the town**. Turning on the spot is possible and it is not free, but
a line handed out with a pivot built into the middle of it is a body stopping dead at a corner, and **what
cannot be joined is a movement the place does not offer** rather than a shape the walk has to carry. It is
the same answer the road side gives a junction movement its steering cannot hold (TER-5f).

**What that costs is connectivity, and the census reports it rather than a rule hiding it**: at the
junctions a crossing makes, how many connections no curve would join (`--bench census`). **A turn whose two poses no
curve joins is almost never a curve that could be fitted better**: it is a movement asking to set off behind
where it arrived, or to turn a body round on the spot, which is the place's own geometry (WLK-9) and not this
line's.

## The node network

Held in code and laid by no town (above). What follows is the construction `FootJunctions`,
`FootConnectors`, `FootWays` and `FootMovements` carry.

**WLK-1a** `P4` **The node network is placed off the road ends, and off nothing else.** A road is laid
first, its lanes end at the connection points its arms were drawn with (TER-5d), and the walk is then laid
between the places those ends put it — a pair of pedestrian nodes at each end of each street (WLK-2), and the
points each node hands its ways over at. **Nothing there reads the ground and nothing there wraps a shape**:
where a walk may run is settled by the road being there, and no second reading of the terrain can take away
pavement the town says is there.

**WLK-2** `P4` **A road end carries a pair of pedestrian nodes where the street it belongs to ends**: one
either side of the carriageway, standing `Road.FootNodeBackM` back along the road from the end its lanes hand
the car over at, and `Road.FootNodeAsideM` beyond the carriageway's own edge. Both figures are authored —
where the corner of a street stands is a choice about the town and not a figure to derive — and the node's
own place is the point a walk beside that road begins at.

**What has no frontage carries none.** A bay (GEN-53) is ground a car is put down on, so the walk goes past
the mouth of a rank and not round every space in it. A roundabout's ring (GEN-19) is a carriageway with no
walk beside it, while its arms are ordinary and carry theirs. A junction cut into a street that carries on
past it (GEN-52) is a hole in the pavement a walk goes round, not a corner: no node stands there, no zebra is
asked for (WLK-10), and **the walk down the street runs through the junction in one stretch** (WLK-11).
**It is not the same question as whether a junction forks**: a bend and a dead end fork nothing and are
ordinary ends of a street, while a node cut into one is a street's middle.

**And the pieces a cut parted a street into are one street.** The node a walk reaches down a road is the one
on the same physical side at the far end of it, and past every cut after that until an end that stands one —
**a side of a street is walked from one end of it to the other whatever it was parted into**. The two arms of
a cut leave on opposite bearings, so keeping to one side means naming the other hand at each hop.

**What carries no walk is still an arm of the junction it meets.** A junction's arms, taken in the order
their bearings turn, bound one wedge between each consecutive two, and **a walk round the junction joins
that pair only where both of them stand a node** — an arm's mouth being ground the walk goes round rather
than ground it turns at. It is what leaves a roundabout's arm with no walk from one of its sides to the other,
and a cut junction pairing nothing across its wedges, so **a town's pavement is broken by nothing a street
runs past**.

**WLK-3** `P4` **Two pedestrian nodes standing within `Road.FootNodeMergeM` of one another are one place.**
A corner a walk crosses in two strides is one place to arrive at, not two to choose between — so the four
arms of a crossroads leave four corners and not eight. **Asked of where the nodes stand and not of which
junction they came off**, and transitively: two junctions laid a few metres apart merge exactly as two arms
of one do.

**The place stands midway between the nodes merged into it, and nothing they hand over at moves**, so the
merge changes where a walk *arrives* and never where it *leaves* (WLK-9). **And a pair whose way ran to a
node the merge has made this same place is dropped**, such a way joining a corner to itself. Which way that
is, is structural — the crossing to the node across its own carriageway, the road walk to the far end of the
same road, the junction walk round the corner — and never read back off the points. It is what leaves the
corner of a crossroads handing over to four ways rather than six.

**WLK-9** `P4` **A pedestrian node hands over to three ways, and reaches each of them at a pair of points
struck off the driven ground's own boundary.** The three are the crossing over the carriageway, the walk
down the road, and the walk round the junction; **a pair and not a point, because every way is walked a lane
each way** (WLK-8) and the two points of a pair are the two lanes' own lines — the middle of each half of
that way's band, so a walking lane apart — laid *across* the way they belong to.

- **The crossing's pair lies along the boundary**, on it, its band centred on the place the node stands off
  it — so the way leaves square to the kerb and goes straight over the carriageway.
- **The road's pair lies square to the boundary**, `Road.FootConnectorAlongM` along it from that place and
  away from the junction, **its band laid against the kerb** — so the way runs along the kerb.
- **The junction's pair is the same, the other way along the boundary.**

**And each of the road's and the junction's points is then dropped onto the course its own lane is walked
on** (WLK-11), which passes *near* the point struck square off the boundary rather than through it. **The
point is moved to the line and the line is never fitted to the point.** A crossing's pair is not moved: it is
struck on the boundary, which is the line that way is walked over.

**The boundary is the driven ground's own and never the road's half-width**
([TER-7b](../../terrain/docs/requirements.md)): at a mouth the ground a junction's movements are driven over
reaches past the arm's own edge, so a point placed square to a centreline that runs straight on while the
kerb turns lands on the tarmac. **It is what makes a corner a place.** **Which of the town's kerbs is settled
at the road's own edge, and only then where along it**: in the crotch of a fork `Road.FootNodeAsideM`
reaches over the arm opposite, so the boundary is asked for at the point on the node's own carriageway edge
that the node stands off — a point of this road, which nothing standing over the node can answer for — and
what is found is still a place on the boundary, so a mouth hands its pair over on the fillet.

**WLK-12** `P4` **The place a lane sets off from and the place it arrives at, standing within
`Road.FootConnectorMergeM` of one another, are one place.** Both are moved onto the point between them and
**nothing is laid down that lane**: a stride of walk between two places a stride apart is not a walk anybody
takes, and a walk reaching the one place has reached both.

**Weighed and applied a lane at a time, not a pair at a time.** Where the walk turns into a corner rather than
round one, a pavement's two lanes pinch out at different places, so weighed on the pair nothing would ever
weld. **It is not the node merge and it does not cascade**: WLK-3 asks how near two *nodes* stand, this how
near the two ends of *one lane*, and a point is the end of exactly one lane. **A shared point is somewhere a
walk arrives**, which is what parts it from the pair WLK-3 drops: the pair it belongs to still hands its way
over, having lost the length of one of that way's lanes.

**WLK-11** `P4` **A way is laid between every pair of points still handed over at, a lane at a time**
(WLK-9, WLK-3): the <em>n</em>th point of one pair to the <em>n</em>th of the other, both being struck the
same way round off the boundary, so a way's two lanes run beside one another rather than crossing in the
middle of it. **A way is one line and not two** — it is laid once from either of its two ends, which name
each other.

- **A crossing is the straight between its two points**, over the carriageway a zebra is painted on (WLK-10).
- **A walk down a road or round a junction is a stretch of its lane's own course** (WLK-1), read between the
  two places its points fall on — so it bends where the kerb bends and keeps the same distance off the
  tarmac. **The shorter of the two ways round a closed line**, the long way round a block being the rest of
  the town. **A walk down a street is one such stretch however many roads the street was parted into**
  (WLK-2), so it runs round the mouth of every rank between its two ends as the kerb does.

**And the lane is that stretch and nothing else — nothing is fitted to either of its ends.** Its two points
were dropped onto this very line when they were struck (WLK-9); a line fitted to the two ends instead cuts the
corner at a mouth and the bend of a street, and a corner between a point and a line passing near it is a line
the shape never drew. **Where the two ends fall on two different lines of that course there is nothing
between them, and no lane is laid**: a straight drawn instead is a walk over whatever stands in the way — a
carriageway, a block, the water — which is worse than a way the network says it has not got.
