# Roads, junctions and crossings — requirements

The street network: what a road is, what a junction is, which movements conflict, where crossings go and
what is painted on any of it. The ground itself is [world/terrain](../../terrain/docs/requirements.md).

## Roads

**TER-4** A road runs between **two named intersections and touches no third**; nothing infers topology
from geometry. Its shape is an **arc spline** — a chain of constant-curvature pieces — so a straight is
the same record at zero curvature and not a separate kind. **A road declares its own width**, and
everything derived from it follows the road's own rather than the catalogue default.

**TER-4a** Traffic keeps right: a road's two lanes are assigned by heading, and left turns cross oncoming
traffic and must yield — which is a right of way and is stated as one (TER-5e). **The side of the road is a
single global constant** — lane offset, turn classification, keep-right on foot and which flank a car door is
on all read that one.

**TER-4b** Two carriageways coming within a pavement's width of one another **must both name an
intersection there** — nearer than that and no pavement fits between them, so their tarmac is one surface
and a walker has no way past on foot.

This is not a tidiness rule. With no node, the lane graph has never heard of the spot, so nothing turns,
gives way, is signalled or is crossed on foot, and **the two streams merge through each other** while
coverage, connectivity and alignment all pass over paved, drivable, single-region ground. It is invisible
to a reader of the map file, because each road's topology is impeccable. A road turning a corner *on
another road's carriageway* is this; a road that turns a corner touching nothing is not.

**A road is also cut where it is not a junction.** A slice above may ask for a node of its own on a road —
today only the parking sections, whose rule is
[`GEN-4h`](../../parking/docs/requirements.md) — and the cut it gets is a point rather than a disc, so the
two lanes it makes meet exactly and nothing below hears of it as an intersection. Where such a node may
stand, and what it is for, is that rule's; what it means for a lane is the same thing every other cut
means, which is why it is here and not a second mechanism.

## Junctions

**TER-5** An intersection is a **disc** — the ground its arms share, the same shape from every bearing —
with the wedge between each pair of neighbouring arms paved back to an **arc tangent to both
carriageways**, which is the line a turning car takes. Each arm is stamped out to its tangent point so
disc, mouths and fillets come out as one region. **"How far the junction reaches"** — what crossings and
lots measure from — is half the disc plus the corner radius, not the disc's edge.

**A junction is not sized around a car's turning circle.** Turning geometry is the controller's problem.
There is exactly one exception:

**TER-5a** A **dead end** is an intersection with a single arm, and it is the one junction sized around a
turning circle: its disc must hold a car working itself round on the spot (`P-19`) with the car's width
clear of the kerb, because there is no other arm to overrun into and no car park promised there. A dead
end too small to turn in is a place nothing that drives into it can leave. It carries no crossing and no
lights.

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
the box is a place that can be named without naming a movement. Every movement in the reckoning reaches a
radius, because the one that never could is not a movement (TER-5f).

**TER-5f** **No box admits a movement that reverses the direction of travel.** A pair of lanes that would
face each other across an intersection is not joined at all: no turn is classified between them, no line is
drawn, no ground is measured against it and no route may be handed one. The arithmetic is why — the line
between two opposing lanes a lane's width apart is a semicircle of a metre and a half, tighter than any
car's lock at any setback — and the consequence is deliberate: **a leg that has to come back the way it
came does it in a car park's bay (`GEN-4l`) or by working itself round at a dead end (`P-19`, TER-5a),
which are manoeuvres a driver makes and not movements a junction offers.**

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
  junction is driven over nothing and has nothing to look up; a turn across the oncoming stream crosses it
  and has to ask about every metre it does.
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

## Right of way

**TER-5e** **Where two bodies come to one piece of the world, a right of way says which of them gives it
up.** It is carried by the stretch and not by the body — one car is straight through on the lane it is
leaving and a turn across the oncoming stream on the join it is entering, and those are two stretches of two
ways — and it is a fact about the movement, worked out once with the town from the turn that movement makes.
**Straighter is stronger**: a stream that turns out of nobody's way, then the near-side turn, which crosses
nothing of its own carriageway, and last the turn across the oncoming stream (TER-4a), which is the weakest
movement a box admits because it is the last one there is (TER-5f). **A body on a crossing's paint has
the right of way over the traffic in the lanes it is painted across**, which is what the paint is for.
Everything else — every stretch of way that is not a movement through a box — is ordinary traffic, neither
given way to nor taken from.

**Above all of those stand two ranks a road does not carry of itself.** A **closed road** is ground an
officer is holding beside it ([agents/service](../../../agents/service/docs/requirements.md), `SRV-6`),
above every ordinary movement and above the paint; a **call** is above that
([agents/ambulance](../../../agents/ambulance/docs/requirements.md), `AMB-4`), which is the whole of what
lets a rescue and a recovery through a road that is shut to everybody else. The order is one comparison and
the placing is the mechanism: nothing reading the book learns what a policeman or an ambulance is.

**What a rank takes, it takes on every way alike.** Ground somebody has merely *claimed* is not ground a
stronger movement is refused by — on the way that movement is driving as much as on the ways it is only
driven over. A cut made one way and not the other is a rescue held up by a claim it outranks, and a closure
that shuts the road against the rescue it was put there for.

**What a right of way takes is a claim and nothing else.** A claim is ground its holder has not reached and
is not committed to (TER-4c.1), so it can be handed back; a body, and the road a body is committed to being
able to stop in, cannot be, **and a rule that took those would not be a right of way — it would be a licence
to drive into somebody**. So the ground of a box is given up the moment a stronger movement asks for it, and
the same ground held by a car past the point it could stop short is held against everything, whatever ranks
anything else has.

**It is therefore one-sided where the old arrangement was mutual, and that is the whole of what it buys.**
Two crossing movements each read the other's ground and each were cut at it, so the box went to whichever
asked first — which is an order dependency dressed as a rule, and it is why a car turning across a stream
could take a junction from the traffic going straight on simply by getting there a tick earlier. Read against
the ranks, the weaker of the two is cut and the stronger is not, and the pair resolve the same way round
whichever of them looked first.

**And what is taken is taken from somebody, who is told.** A claim is answered again every tick against the
whole book (TER-4c.1), and the holder of one a stronger movement has taken has it withdrawn and the entry
that took it re-entered through its own entry state — which either takes the claim again or gives way to
something else. Nothing here stops the body: what holds it is the ground the stronger movement is now
standing on, cut off its grant like everything else (SIM-7).

**And it is spent by the traffic giving ground up, never by anybody being ordered off it.** A body that gives
way is stopped short of what it is giving way to, and **a body stopped short holds none of the ground beyond
the stop** (TER-4c.1) — so the ground is free on the next tick and whoever had the right of way simply takes
it. There is no second mechanism here and no register of whose turn it is: what the ranks decide is which of
two askers is cut, and everything after that is the one arrangement the rest of the town runs on (SIM-7).

**A stop is bounded by the road it takes to make one**, which is what keeps this a rule about who waits. A
car too close to stop keeps what it holds, the ground stays taken, and whoever was waiting waits another
moment — so nobody is ever waved in front of a body that could not have stopped for them.

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
- The inline junction is the exception and takes a single crossing laid on the node itself. **Being on the
  node, it is past the end of every lane there** — the disc reaches further than the paint is wide — so it
  is laid across the lanes that meet at the node, each at its own end, rather than found by projecting it
  down one of them. A crossing no lane carries is paint no driver slows for and a walker no driver can see
  (TER-4c).

**A crossing with no conflicting traffic to phase against carries no lights** (TLT-3), and an uncontrolled
crossing is where the walker's right of way is the whole of what governs it (TER-5e): the traffic gives way
to whoever is standing at the kerb, which is what the paint is there to say.

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
at the moment it moves, and whoever arrives at that ground later is the one that gives way — **unless what
arrives has the right of way over it** (TER-5e), and then what it holds there was never its to keep. A
mechanism that answered and then asked a second question before letting the body go would be the duplicate
SIM-7 is about; one that could grant the same metre twice would be no mechanism at all.

- **A reservation is anchored at a body, which is what makes it order-free.** Its near edge is the asker's
  own tail, so every ask is laid before any of them is answered and two bodies need no order to be resolved
  in: each is cut at the other's near edge and the answer is the same whichever is asked first.
- **Ground nothing of the asker's own reaches is a claim, and a claim is checked before it is laid** — the
  places another way is driven over the one a car has committed to, whether that way is a junction's join or
  a bay's way out, and the band of a zebra a walker steps into. There is no tail to anchor the answer to, so
  the book is asked first and the ground is taken only if the answer is yes.
- **A body driving geometry of its own holds the sweep of it, not the pose it is passing through.** A
  template is laid over no way, so what its driver holds is every way the rest of that line runs over, from
  where the body stands to where the line ends. Ground walked clear at the moment a line was drawn and then
  left open is ground the traffic is granted while the manoeuvre is still driving down it.
- **And it reads every way under each place that line would put it**, which is the same set of ways a body
  standing there is written onto: the lane, the lane running back the other way where the body reaches into
  it, and every join of a junction it is lying under. **Asked of a narrower set than it is written to, a
  manoeuvre cannot see what a body standing in the same place wrote** — and that is exactly a junction,
  where every car crossing holds its road on a *join* and on no lane at all, so a swerve or a back-off
  through a box read the whole box as empty.
- **A claim is answered every tick and not only on the tick it was taken.** It is re-laid from the body
  like everything else, so what a re-laid claim needs is the same question asked again: a claim over ground
  a stronger movement has since taken is given back, and **the body that had it is told**, because the only
  thing that knows what the claim was for is the manoeuvre that took it. Laid unread instead, the stronger
  movement drove through and the claim's holder was never cut, and the two of them held one piece of the
  world between them for as long as the claim lasted.
- **What takes a claim is a rank above its own and nothing else** (TER-5e). Ordinary traffic over it, a
  wreck shoved onto it, somebody on foot across it — all of those cut the claimant's grant already, on the
  way it is driving, and a second refusal here would be the duplicate SIM-7 is about. It would also refuse
  the one thing a claim exists for: the stretch a swerve claims is the stretch containing the body it is
  swinging round.
- **Nothing is ever released.** All of them are re-laid from the body every tick, so nothing leaks and a body
  that stops, is wrecked or is taken over by a hand holds nothing on the tick after.
- **A grant is how far a nose may go, so nothing behind that nose can be what ends it.** A stretch begins a
  margin behind its owner's tail and a crossing point is a place two lines meet, so the ground between a
  body's own tail and its own nose is ground it has *arrived at* rather than road it was granted. Cut there,
  a grant stops being a distance at all — it reads as a car metres deep inside somebody's road while it does
  nothing but stand on a junction it has crossed, and it brakes for the corner it came in by.
- **A rule that stops a body stops what it holds.** Ground beyond the place a body is held at is ground it
  is not committed to, and holding it queues the town further up the road than anybody is going to get —
  and, where the stop is short of a zebra, holds the crossing shut against the very people it was made for.

**The book is over ways and not over lanes**: a lane, the join across a junction between one lane's end
and the next one's start, and **whatever a slice above the road lays off it** — the line into a parking
bay and the line back out of one. That is what makes a junction answer to the same book as the rest of the
road, and it is why the ground where two movements meet (TER-5c) needs no register of its own: each of them
is a stretch of a way somebody is driving, and the table says which pairs of ways to read against each other.

**The road owns the numbering and not the ways.** A way is a length and a run of metres; which of the
town's features drew one is that feature's business, and the road neither knows nor needs to. What the
road keeps is that the lanes are numbered first, the joins after them, and anything laid off the road
after those — so one book and one table hold all of it, and a reader holding a way number asks the same
question of every band.

**The table of crossings is therefore indexed by way and not by movement.** A junction's join is only ever
driven over another join, because the lanes hand over clear of the box (TER-5d) — so every lane's row is
empty and the table reads as the junction table it began as. What needs the wider index is a way laid
*along* a street rather than across a box: the line into a parking bay leaves its lane part-way along and
sweeps the lane running back the other way, and a table that could only name joins could not say which
ground that was.

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
stopping at the stop bar rather than running on into the junction behind it), kerb lines (broken exactly
where the pavement's edge is, and over a car park's mouth, where the ground on the far side of the line
is the lot's own tarmac and there is no kerb to be the edge of), pavement and deck edge lines, stop bars
(square across *that arm's* direction, covering the approaching lane only, stopping at the kerb), zebras
(spanning kerb to kerb, running along the direction
of the traffic that crosses them, between their bar and the junction without overlapping the bar), bay
outlines (three-sided, open at the mouth, so a row of bays leaves no line across the ground a car enters
the lot over, and laid against the lot's own edge — inside it — wherever they stand within a line's width
of one) and drift marks.

**A car park's paint and the road's are one line where they meet.** The strokes at a lot's mouth end on
**the carriageway's own edge** and not on the lot's rectangle, which is a chord of that edge and stands up
to its sag inside it; the kerb line is broken over **the mouth** — the lot's road-facing edge, not the
shadow its whole rectangle casts along the road — and stops a line's width short of either end of it, so
the corner where the two turn into one another is painted and painted once. A gap at that corner is a gap
in the one place a driver entering the lot is looking.

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
  through rather than only the two it ends at, and at the places a slice above asked for (`GEN-4h`).
- A turn classification per pair of lanes — straight / near-side / far-side — **filled once when the town
  is laid** and read off thereafter, and **nothing at all between a lane and the one running back down its
  own stretch** (TER-5f). Which turn joins two lanes is a fact about the road, not about the car on it.
- A crossing registry queryable by junction, and a stop-line registry carrying the bars actually painted.
- A table, filled once from the lines themselves and **indexed by way**, of where each of the town's ways
  is driven over the others, in both ways' own metres (TER-5c). **There is no register of who is inside a
  junction**: the table is looked up and the answer comes out of the same book everything else does
  (TER-5c.1). It is laid over every way the book numbers, so a slice above the road can measure its own
  ways into it with the same code and be read by the same walk.
- A lane occupancy index over the ways of TER-4c — the lanes, the joins between them, and the ways a
  slice above lays off them — carrying every
  body on the network and the stretch each driver has taken, so that **who is in front and how much road
  is whose** are answered from the town's own book rather than from geometry (`S-2a`). It is laid over
  ways the caller measures, so the pavement keeps a second book of the same kind (PER-13). **The two books
  are told apart by which network the ground belongs to and never by which kind of body is standing on
  it.**
