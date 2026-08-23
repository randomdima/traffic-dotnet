# Roads, junctions and crossings — requirements

The street network: what a road is, what a junction is, which movements conflict, where crossings go and
what is painted on any of it. The ground itself is [world/terrain](../../terrain/docs/requirements.md).

## Roads

**TER-4** A road runs between **two named intersections and touches no third**; nothing infers topology
from geometry. Its shape is an **arc spline** — a chain of constant-curvature pieces — so a straight is
the same record at zero curvature and not a separate kind. **A road declares its own width**, and
everything derived from it follows the road's own rather than the catalogue default.

**TER-4a** Traffic keeps right: a road's two lanes are assigned by heading, and left turns cross oncoming
traffic and must yield. **The side of the road is a single global constant** — lane offset, turn
classification, keep-right on foot and which flank a car door is on all read that one.

**TER-4b** Two carriageways coming within a pavement's width of one another **must both name an
intersection there** — nearer than that and no pavement fits between them, so their tarmac is one surface
and a walker has no way past on foot.

This is not a tidiness rule. With no node, the lane graph has never heard of the spot, so nothing turns,
gives way, is signalled or is crossed on foot, and **the two streams merge through each other** while
coverage, connectivity and alignment all pass over paved, drivable, single-region ground. It is invisible
to a reader of the map file, because each road's topology is impeccable. A road turning a corner *on
another road's carriageway* is this; a road that turns a corner touching nothing is not.

## Junctions

**TER-5** An intersection is a **disc** — the ground its arms share, the same shape from every bearing —
with the wedge between each pair of neighbouring arms paved back to an **arc tangent to both
carriageways**, which is the line a turning car takes. Each arm is stamped out to its tangent point so
disc, mouths and fillets come out as one region. **"How far the junction reaches"** — what crossings and
lots measure from — is half the disc plus the corner radius, not the disc's edge.

**A junction is not sized around a car's turning circle.** Turning geometry is the controller's problem.
There is exactly one exception:

**TER-5a** A **dead end** is an intersection with a single arm, and it is the one junction sized around a
turning circle: its disc must contain the turn-around with the car's width clear of the kerb, because
there is no other arm to overrun into and no other manoeuvre to fall back on. A dead end too small to
turn in is a place nothing that drives into it can leave. It carries no crossing and no lights.

**TER-5b** An **inline junction** has exactly two arms leaving in opposite directions — a place *on* a
road rather than a place roads meet. The two carriageways must align exactly, so a driver sees
uninterrupted road, and it is the one intersection that **paves no ground of its own**. It exists to
carry one pedestrian crossing and the signals that govern it, which is what makes a lit mid-block
crossing possible at all.

Two arms meeting at an **angle** are not this: that is a road that turns, rounded to a kerb radius. Worth
saying twice, because it is the commonest authoring mistake — a corner is a road, a mid-block crossing is
a junction, and they look alike in a map file.

**Corners are decided per corner by which arms are present**: two arms give a fillet tangent to both
carriageways; neither arm is the outside of a turn, so the disc's own corner is *cut* back to an arc of
the same radius; one arm is a straight kerb running on, and nothing is drawn. Both kinds pave their
square whole first and then take the ground back along the arc.

**TER-5d** **A lane has one end, whatever is driven off it.** The line across the box is set back into the
two lanes it joins far enough that it reaches the junction's own corner radius, and **the setback belongs to
the lane end rather than to the turn** — the widest any of that end's movements asked for. A straight and a
right-angle turn out of one lane therefore hand over at the same point, and the boundary between a lane and
the box is a place that can be named without naming a movement. The turn-around is the one movement left
out of the reckoning: it reaches no radius at any setback, so taking it into the widest would set every lane
in the town back as far as the town allows.

## What a movement takes off another

**TER-5c** A movement through an intersection is **driven over** the other movements through it: every
stretch of another movement's line that comes within a car's width of its own, on both sides of the
measurement. The town works the table out once, from the lines themselves. A movement whose line goes near
nothing crosses nothing.

**TER-5c.1** **A body reserves the ways it is going to be on, and no others.** The way under it and the
ways its plan takes it down are its own to hold; a way it is merely driven *over* is one it never writes to.
What that ground costs it is instead **looked up**: a driver reads the table above for the way it is on,
and asks each way named there — in that way's own book, at that way's own metres — what is standing on it.
A grant is cut at the first of those the answer is anybody's.

The rule this exists to hold is that **no two bodies are given the same piece of the world**. A reservation
is stated in one way's metres, but the ground it stands for is the town's: two ways that meet inside a
junction are one piece of the world under two names, and a driver that only ever read its own name for it
would be granted the metre two lines meet on at the same time as the driver on the other line. Marked
rather than read, the same fact costs a body a fan of reservations across ways it will never touch, and the
ground of a box belongs to whoever aimed at it rather than to whoever is on it.

**And the two networks are one town.** A zebra is a walk laid over a carriageway — the same ground is a band
of a crossing way and a stretch of a lane — so the rule above is the rule here with the books swapped in. A
car writes the stretch of its own lane and nothing at all on the walk; a walker writes the band of the lane
it is standing in and the band it has been granted, and nothing of a lane it has not asked for. Each reads
the other in the book that ground belongs to, through the town's own table of where every lane falls on
every crossing way. **A body's own network is the only book it writes into**, which is what stops one piece
of ground being two records that can disagree about who has it.

**A crossing is a place and not a period, so it is given back where it is passed.** What a car holds on its
own way through is the crossing points it has still to reach — a body a clearance past one is not going
over it again — and never the box as a whole for as long as the car is in it. A car half way through a turn
refusing the corner behind it is refusing a movement nothing was ever going to be driven into.

**What a committed car does hold is its own join, at the places the others cross it.** A driver's road ahead
is a braking distance and no more, which does not reach the middle of a box until the car is nearly on top
of it; two cars asking from opposite arms would each look the other's join up, find the metres where they
cross still empty, and both go. So the ground where the lines meet is held from the moment a movement is
committed to — on the mover's *own* way, where the traffic crossing it reads it. The ground between two
crossing points is driven over by nothing and is nobody's to hold, and the metres behind the body are the
crossing already spent.

**TER-5c.2** **A body holds one metre of one way once.** One body is one stretch: **the margin it keeps, the
body, and the road it is committed to**, in that order and in one interval of every way it is on. What the
crossing adds is only the ground that stretch has not got to — the metres ahead of it — and never a second
piece behind the tail, which would hold nothing the reservation was not holding and would make the book
count one body as two.

**The margin is what the book's own reading owes, and it is the same margin wherever a body stands.** A
stretch is one interval of one way's arclength, which is the width of the road thrown away; a crossing point
is a place two *lines* meet, and what has to be clear of it is a body off its line by up to the road's
tolerance and swinging wider still at the back. So a body's ground begins a margin behind its tail — on a
lane exactly as on a join, because a body is the same body wherever it stands — and **whoever is cut at it is
cut at the margin rather than at the paintwork**. That is also what a queue at rest stands at: the follower
keeps no gap of its own from the body in front, because the ground it may not enter is that body's to hold.

**The two ends of it are one figure read at two shares**, and the tail's is the shorter of them: what the
margin covers is the same at both ends, but only behind is every metre of it a metre of road the traffic
coming up is queued out of. The share is data (`DrivingFigures.TailMarginShare`), the relation is that the
tail's is never the larger, and which shares the town can afford is the soak's answer and not a rule's.

**What holds no margin is what is not somebody's road**: a wreck, a claim, a body on foot in a lane, the
town's own furniture, the metre where another movement crosses, the kerb line of a lane a walker was refused.
None of those is a body that swings, and **the asker keeps its own margin off them** — so the clearance is
kept exactly once in every case, by whichever of the two has a reason to hold it.

**And the margin is measured, not chosen**, held to whatever the soak says it must be rather than to what
looks tidy on the overlay. A fleet tuned to queue closer than the measured floor gets the floor.

**In front is then a fact about bodies and never about near edges.** Every stretch begins behind its owner,
so the order of the near edges on a way is one margin out of step with the order of the bodies on it: what a
driver is cut at is whoever's *body* is ahead of its own, and the cut is taken at that body's margin.

**A junction is refused by ground and never by a verdict.** There is no relation saying two movements
conflict and no register saying whose turn it is: a car is refused by whatever is standing on the metres it
wants and by nothing else. What follows from that rather than being stated beside it:

- **The property belongs to the movement, not to the intersection.** A street bending 20° through a
  junction is driven over nothing and has nothing to look up; a turn-around sweeps the disc and has to ask
  about everything, because it is driven over everything.
- **An intersection of fewer than three arms admits no crossing car movements**, its arms being the two
  halves of one carriageway.
- **Two cars going straight through in opposite directions clear each other**, on every junction and in
  every town. They are one street's two halves passing side by side a lane apart, which is further apart
  than the measurement reaches — so this follows from the ground rather than being granted by a rule, and
  a junction that stopped for it would be a level crossing.
- **Two cars out of one lane, and two merging into one, are not this rule's business.** They are held
  apart by the road each was granted — a headway and a merge — and a second refusal here would be the
  duplicate SIM-7 is about.
- **A body standing in a box is on the same ground**, whether or not anybody is driving it, and refuses
  what crosses it for exactly that reason.

**What this rule cannot promise.** Ground is granted to a car that can still stop short of the box. Past
that point a driver is going in whatever the book says, and one that stalls inside is standing on that
ground however it got there. Two bodies in one box is PHY-1's question, not this one's.

## Crossings

**TER-6** Crossings and parking are variants of the road/intersection family and need only a type tag
beyond their terrain attributes.

- A crossing is **a band of the same carriageway pedestrians may walk over**. It is a plan entity of its
  own and the road graph never reads it, so **a crossing adds no node and nothing can turn at one**.
- The terrain carries the rule: crosswalk ground is person-allowed, car-allowed *and* directional, so the
  lane direction underneath is left in place and a car on a crossing is still held to its lane.
- **Placement is one rule, not hand-picked positions**: one crossing on every arm of every junction at a
  fixed setback from the paved junction reach, each tagged with the junction it approaches — so a
  junction's signal bundle greens *its own* arms' crossings.
- **An arm too short to hold setback plus band clear of both junctions gets none.** Short spurs and small
  rings therefore have no crossings, and that is correct.
- The inline junction is the exception and takes a single crossing laid on the node itself.

## The book

**TER-4c** **Everything that can be on a lane is in the lane's own book.** A driver looks at the book and
at nothing else: the traffic, **anybody on foot in the lane**, and **the town's own furniture**, which is
projected onto the lanes it stands on once when the town is laid and never moves again. A thing a driver
must be held off that is in no book is a thing the driver cannot see, and there is no second mechanism —
no ray, no cast — behind it to catch what the book left out.

**TER-4c.1** **Ground is asked for, answered, and then it is the asker's.** A body puts the stretch it
wants into the book — **the margin it keeps, itself, and the road from its nose to where it means to be able
to stop** (TER-5c.2) — and what comes back is that stretch **cut at the first metre already somebody else's
and at the first place a rule stops the asker**: a red, a bar, a crossing it must stop short of, the metre
where another movement's ground crosses its own.
**Part of what was asked for is the ordinary answer** rather than a refusal, and a body granted none of it
stands still.

**What comes back is the asker's to move into.** Nobody else is granted it, the holder asks nothing further
at the moment it moves, and whoever arrives at that ground later is the one that gives way. A mechanism that
answered and then asked a second question before letting the body go would be the duplicate SIM-7 is about;
one that could grant the same metre twice would be no mechanism at all.

- **A reservation is anchored at a body, which is what makes it order-free.** Its near edge is the asker's
  own tail, so every ask is laid before any of them is answered and two bodies need no order to be resolved
  in: each is cut at the other's near edge and the answer is the same whichever is asked first.
- **Ground nothing of the asker's own reaches is a claim, and a claim is checked before it is laid** — the
  far side of a box a car has committed to, the lane a car backs onto, the band of a zebra a walker steps
  into. There is no tail to anchor the answer to, so the book is asked first and the ground is taken only if
  the answer is yes.
- **A body driving geometry of its own holds the sweep of it, not the pose it is passing through.** A
  template is laid over no way, so what its driver holds is every way the rest of that line runs over, from
  where the body stands to where the line ends. Ground walked clear at the moment a line was drawn and then
  left open is ground the traffic is granted while the manoeuvre is still driving down it.
- **Nothing is ever released.** All of them are re-laid from the body every tick, so nothing leaks and a body
  that stops, is wrecked or is taken over by a hand holds nothing on the tick after.
- **A rule that stops a body stops what it holds.** Ground beyond the place a body is held at is ground it
  is not committed to, and holding it queues the town further up the road than anybody is going to get —
  and, where the stop is short of a zebra, holds the crossing shut against the very people it was made for.

**The book is over ways and not over lanes**: a lane, and the join across a junction between one lane's
end and the next one's start. That is what makes a junction answer to the same book as the rest of the
road, and it is why the ground where two movements meet (TER-5c) needs no register of its own: each of them
is a stretch of a way somebody is driving, and the table says which pairs of ways to read against each other.

**A person in a lane is a body like any other and carries a reading of its own.** It cuts the road a driver
is granted exactly as a car standing there would; it is waited behind while it is moving, and once it has
come to rest it is something the rule that drives round an obstruction (`E-4`) may act on. **What keeps a
swerve off it is the ground it holds and never a name it is refused by** — its stretch of the book carries
a margin (PER-15), and a template laid over that stretch is refused by the same test that refuses one over
a wreck. Naming a second rule to refuse the same movement would make the first useless (SIM-7).

**A body's own stretch is the one thing a book will not hold against it.** An occupant is an index into one
of two rosters and the stretch carries which, so a car excluding itself by number does not also excuse the
walker that happens to hold the same number.

**A crossing is carriageway and not a unit**, on both sides: what a body has of a zebra is the band of it
one lane wide that it is on. A car has the stretch of its own lane, which is the same stretch the rest of
its road is and is written where the rest of its road is written — in the road's book, once. A walker has
the band of the lane it stands in, and the band in front of it once that one has been granted (PER-15).
**A band is a stretch of ground like any other**, so the two sides of the paint are held apart by the answer
that holds the rest of the town apart, and **neither of them is ever refused by the paint**: a body that
cannot cross is one the lane under the paint already belongs to.

## Markings

Everything painted on the ground is **engine-drawn primitives, never art**: lane centrelines (dashed,
stopping before a junction rather than running into it), kerb lines (broken exactly where the pavement's
edge is), pavement and deck edge lines, stop bars (square across *that arm's* direction, covering the
approaching lane only, stopping at the kerb), zebras (spanning kerb to kerb, running along the direction
of the traffic that crosses them, between their bar and the junction without overlapping the bar), bay
outlines and drift marks.

Five rules govern all of it:

1. **A coordinate is read from whatever owns it, never re-derived.** One shape, one pattern, one lane
   offset. A figure that exists in two places eventually disagrees with itself.
2. **Everything is drawn in its own frame.** A crossing on a road running north-east carries the same
   zebra as one running due east.
3. **A bar on an arm with a crossing is placed by the crossing alone**, never by the junction as well, or
   the two answers differ by metres.
4. **Every run of marks is centred on the stretch it is on**, so a dashed line does not begin with a half
   dash.
5. **Paint sits on the surface it belongs to**, checked on rendered frames because no numeric check
   answers it (VER-9).

## What this slice must produce

- A road graph: one node per junction, directed lane edges, lanes cut at **every** junction a road runs
  through rather than only the two it ends at.
- A turn classification per pair of lanes — straight / near-side / far-side / turn-around — **filled once
  when the town is laid** and read off thereafter. Which turn joins two lanes is a fact about the road,
  not about the car on it.
- A crossing registry queryable by junction, and a stop-line registry carrying the bars actually painted.
- A table, filled once from the lines themselves, of where each movement through a junction is driven over
  the others, in both ways' own metres (TER-5c). **There is no register of who is inside a junction**: the
  table is looked up and the answer comes out of the same book everything else does (TER-5c.1).
- A lane occupancy index over the ways of TER-4c — the lanes and the joins between them — carrying every
  body on the network and the stretch each driver has taken, so that **who is in front and how much road
  is whose** are answered from the town's own book rather than from geometry (`S-2a`). It is laid over
  ways the caller measures, so the pavement keeps a second book of the same kind (PER-13). **The two books
  are told apart by which network the ground belongs to and never by which kind of body is standing on
  it.**
