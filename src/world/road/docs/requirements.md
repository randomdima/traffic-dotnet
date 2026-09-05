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

**TER-5c.1** **A body claims the ways it is going to be on, and no others.** The way under it and the
ways its plan takes it down are its own to hold; a way it is merely driven *over* is one it never writes to.
What that ground costs it is instead **looked up**: a driver reads the table above for the way it is on,
and asks each way named there — among that way's own claims, at that way's own metres — what is standing on
it. A grant is cut at the first of those the answer is anybody's.

The rule this exists to hold is that **no two bodies are given the same piece of the world**. A claim
is stated in one way's metres, but the ground it stands for is the town's: two ways that meet inside a
junction are one piece of the world under two names, and a driver that only ever read its own name for it
would be granted the metre two lines meet on at the same time as the driver on the other line. Marked
rather than read, the same fact costs a body a fan of claims across ways it will never touch, and the
ground of a box belongs to whoever aimed at it rather than to whoever is on it.

**And the pavement is not a second town.** A zebra is a walk laid over a carriageway — the same ground is a
band of a crossing way and a stretch of a lane — so the rule above is the rule here with the kinds swapped. A
car writes the stretch of its own lane and nothing at all on the paint; a walker writes the band of the lane
it is standing in and the band it has been granted, and nothing of a lane it has not asked for. Each reads
the other on the way that ground belongs to, through the town's own table of where every lane falls on
every crossing way. **The carriageway under a crossing has one claim and one owner**, which is what stops one
piece of ground being two records that can disagree about who has it — and **the walk over it is still the
walk's**, so a body on foot standing on the paint holds a stretch of the crossing way like any other ground
it stands on. Nothing else reads that stretch: the look-up the paint replaces is about the traffic that is
*coming*, and somebody standing in a lane is not an answer to it.

**Ground of one kind that no other kind runs over is the other case, and there the kind of body decides
nothing.** A footway is not carriageway, a lane is not pavement and a bay is neither, so whoever is standing
on any of them holds it (TER-4c.2): a person in a lane is a stretch of that lane, a car that has mounted a
kerb is a stretch of the footway under it, and **anybody at all** in a bay — a car whether the register knows
it is there or not, and a person on foot — is a stretch of that bay's ways. **What kind of ground a hold goes
onto is a fact about the ground and never about its holder**, so the walk that finds the ways under a body
names every kind, once, for every roster: a kind one caller walks and another forgets is one where half the
town is invisible.

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
piece behind the tail, which would hold nothing the claim was not holding and would count one body as
two.

**The margin is what that reading owes, and it is the same margin wherever a body stands.** A
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
that point a driver is going in whatever anybody has claimed, and one that stalls inside is standing on that
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
the placing is the mechanism: nothing reading the claims learns what a policeman or an ambulance is.

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
whole of them (TER-4c.1), and the holder of one a stronger movement has taken has it withdrawn and the entry
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

## Claims, and how strong they are

**TER-5g** **Everything the road holds is a claim, and a claim is who is claiming, where, and at what
priority.** A way is used when there is a claim on it and not otherwise; there is no second property
saying what kind of thing a claim is, because everything a reader wants to know about one is worked out
from those. Whether a body is standing in it is whether its body edge is past its near edge; whose it is,
is its occupant and roster; the town's own furniture is a claim nobody owns.

**The priority is a named ladder. Low is strong**, the numbers are the ladder rather than the order the
levels happen to be written in, and the gaps in it are levels nothing claims yet.

- **p0 hard** — **a body, and the road that body can no longer give back.** Nothing takes it, whatever
  anybody asks with, because a right of way orders who waits and never who is driven into (TER-5e). It
  needs no rule of its own for a car nobody is driving: a wreck, a parked car and a car under a hand are
  bodies, and a body is this by construction.
- **p1 special** — **ground somebody answering a call has been granted and not reached.** Taken by p0 and
  by nothing else.
- **p5 firm** — **ground anybody else has been granted and not reached**: the far end of a box, a bay being
  backed out of, a swerve about to cross, a road an officer is holding. Taken by a strictly stronger
  movement.
- **p9 soft** — **road a driver has stated it means to use and has not reached.** The weakest there is, and
  the one every stronger movement is entitled to. **An equal right of way takes it, where an equal right of
  way does not take a firm claim** — that single difference is what keeps the two levels apart, and
  collapsing them either deadlocks a junction or leaves every weaker movement waiting on ground the other
  was merely thinking about.
- **p10 rejected** — **an ask that was refused, left standing so the traffic can see it.** Nobody's
  ground: it binds nobody, it cuts nothing, and it is **the one thing on a way that is not a claim on
  ground** — so it is laid over the very stretch the traffic holds and is outside TER-4c.3. Made exclusive
  like the rest, it would be cut away by whatever it was marking and the traffic would never learn that
  anybody was waiting.

**The ladder says which comparison is made and the right of way says who wins it.** A right of way is a
fact about the *way* and not about the body on it — a junction's every movement is a way of its own, so the
claim on the left-turn connector is a left turn by the ground it is on. Folded into the priority, a turn
across the oncoming stream and a street going straight through it would be one number: p0 is compared with
nothing, p10 with nothing, and the middle three are settled on TER-5e's ranks.

**The lifecycle is one word throughout.** A claim is **asked** for before any of them is answered
(TER-4c.1); what comes back short is a claim **cut**, and what a stronger movement takes afterwards is a
claim **withdrawn**. Nothing is ordered off ground: a claim cut or withdrawn is simply shorter, and the
speed it inverts to is the whole of the reaction (SIM-7).

**A driver states the road it means to use, and that statement is a claim.** Beyond the stretch it is
committed to (TER-4c.1), a car under way holds what it takes to reach the speed it is planning for, hold
that speed for as long as it says it will, and stop from there. **The holding time is what tells a plan
from a commitment**: the claim a car is committed to already covers one decision interval of travel and a
stop, so a soft claim measured at the same interval is that one again for every car already doing the speed
it is planning for. The interval is data, and what the town can afford of it is the soak's answer. It is
one interval of the way continuing the committed claim's own, so no metre is held twice (TER-5c.2), and it
is answered by the same grant: a car refused the road has stopped saying it is coming, on the tick it was
refused.

**What it buys is that a body's intentions are claimed at all.** A driver reads further up the road
than the ground it is committed to reaches, so before this every car could see what the others could not
undo and none of them said where it was going — and a car pulling out of a side road in front of one
coming at speed was refused by nothing, because nothing said it was coming.

**What makes it affordable is that it can be taken.** The same ask held as ground the car was committed to
was a quarter of a kilometre of empty straight held against everybody, because nothing could ask for it
back. Held at p9 it costs whoever outranks it nothing at all.

**A tie refuses a granted claim and does not refuse a soft one**, and the difference is what each is for. A
granted claim is one movement's ground, settled by whoever took it; a soft claim is laid by everybody at
once, so two movements of one rank that each refused the other's would each be waiting on ground the other
was merely stating and neither would ever ask for it. A tie is settled by the granted claim, exactly as it
was before either of them stated anything.

**And a soft claim is bounded by everything the committed one is bounded by** — a red, a bar, a crossing, a
box this car has not been given. That is the whole of what a signal refusing a soft claim comes to: a
car stopped at a bar states nothing beyond it, so the arm with the green is refused by nothing the arm
with the red is thinking about; a car exempt from the bar (`AMB-4`) is not stopped by it and neither is its
soft claim; and a car past the point it could stop is stopped by nothing and neither is its soft claim.

**A body that is not moving states nothing.** Such a claim says where a car is *going*, and a car at
rest is going nowhere until it moves. Laid from a standstill it is the pull-away horizon of every car in
every queue in the town, held against every movement those queues cross — a car at a give-way line holding
a box shut against the traffic it is itself waiting for.

**A stated claim refuses on the ways a body crosses and never along the way it is driving.** Two cars on
one way are held apart by the road each was granted (TER-5c), and a stated claim is laid *ahead* of its
holder — so along a lane it lies over the traffic in front of that holder rather than behind it. Read as a
refusal there it stops the car in front of a rescue dead, which leaves the rescue behind a body instead of
an empty road.

**Nothing on foot states anything.** Such a claim is in no question about where a body is or what is
coming down a lane — nothing is standing on it — so a walker at a kerb is held off the road by the traffic
and never by the traffic's ambitions.

**And the rank a car asks a box's ground with is the rank it will hold that ground at.** A body past the
point it could stop short of a box is going in whatever anybody has claimed, so it asks with the rank that says
so — refused there instead, it would be stopped in the middle of a box on the strength of somebody else's
intentions, which is the one shape the ranks exist to prevent.

**What a loser does about it is drive to the road it has left**, and there is no second mechanism (SIM-7).
Ground taken back shortens a grant like any other, the grant inverts to a speed like any other, and that is
an ease-off where there is road and a stop where there is none. **A car that cannot stop in what is left
does not stop**, and what happens then is the solver's (`PHY-1`) rather than a rule's.

## Crossings

**TER-6** Crossings and parking are variants of the road/intersection family and need only a type tag
beyond their terrain attributes.

- A crossing is **a band of the same carriageway pedestrians may walk over**. It is a plan entity of its
  own and the road graph never reads it, so **a crossing adds no node and nothing can turn at one**.
- **A crossing has no width of its own.** It names the road it is painted across, and how far it reaches is
  that road's width measured along the paint's own axis — so a crossing laid off square is longer by what
  the skew costs it and still reaches kerb to kerb, and one laid square is the carriageway's width. That
  one figure is what it is drawn, walked, stopped for and asked about at. A span carried beside the road's
  is a second answer to a question the road has already answered (GEN-15), and the two disagree the first
  time either is laid again: a zebra wider than its carriageway stands its end bars on the pavement, and a
  narrower one leaves a strip of road nobody is walking over.
- The terrain carries the rule: crosswalk ground is person-allowed *and* car-allowed, and it is a stretch of
  the road it is painted across rather than a shape of its own — so the lane runs underneath it and a car on
  a crossing is still held to that lane.
- **Placement is one rule, not hand-picked positions**: one crossing on every arm of every junction at a
  fixed setback from the paved junction reach, each tagged with the junction it approaches — so a
  junction's signal bundle greens *its own* arms' crossings. **The reach is that arm's own** — where the
  kerb fillet between it and its furthest neighbour lets go of the kerb, which grows as the corner sharpens
  — and never the distance from the node, which is the same on every arm of every junction and right on
  none of them. The bar behind the crossing is set back from the same place, and so is everything hung off
  either of them.
- **A junction that admits no fork carries one crossing and not one per arm, and none of the junction is in
  where it goes.** Two arms are one road: everything that arrives leaves the only other way, so the node is
  somewhere to cross rather than somewhere to choose, and a second zebra a few metres from the first is the
  same road crossed twice and the same stop asked for twice. **The paint is the crossing's own bundle** —
  the zebra with the bar of each of the two lanes that run over it, one either side and each facing the
  paint — laid on **whichever of the two arms has the most road left behind it**, and it **begins where that
  arm's own bend lets go** rather than a setback past a box: there is no box behind it, only the same road
  swept round its corner (GEN-12a), and what is laid across a straight begins where the arc ends. Every other
  junction's setback is a distance from a place cars turn across; this one's is the curve they drive round.
- **The bars of such a node are the one pair a junction the signals do not govern carries**: nothing at two
  arms is lit (TLT-3), so the whole of what governs the paint is the walker's own right of way (TER-5e) and
  the bars are what say where the stop for one is made. A light there, if a map ever authors one, hangs off
  those bars like every other and stands beside the zebra with them.
- **And no lane line stops for such a node, only for its paint.** What a dash must not be laid down is ground
  the movements through a box are driven across, and there are none here but the one the road itself makes —
  so the line runs from the bundle's own outer bar **through the bend and the node** and on down the other
  arm, as it does along any road that turns a corner. The one thing that still breaks it there is a zebra
  laid **on the node itself**, which is what an authored inline junction carries: paint breaks a lane line
  wherever the paint is.
- **Elsewhere a lane line stops at the ground its junction reaches** and not at the disc that junction is
  drawn on, whether or not the arm carries paint. The metres between an arm's kerb fillet and the disc are
  the same turning ground as the rest of the box, and an arm too short for a crossing has nothing else to
  stop its dashes: that reach is `SimConfig.JunctionArmReachM`, read from the figure and never measured a
  second way.
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

## The claims

**TER-4c** **Everything that can be on a lane has claimed that lane.** A driver looks at the claims and
at nothing else: the traffic, **anybody on foot in the lane**, and **the town's own furniture**, which is
projected onto the lanes it stands on once when the town is laid and never moves again. A thing a driver
must be held off that nothing claims is a thing the driver cannot see, and there is no second mechanism —
no ray, no cast — behind it to catch what the claims left out.

**TER-4c.2** **A body holds the ground it stands on, whatever it is doing.** Every agent writes the space it
occupies onto **every way that space obstructs**, and nothing about that write turns on whether anybody is
driving it, whether it is broken, or on what a reader would call it. It is the one hold in the town that
**cannot be given up or taken**: its holder is already there, so no right of way reaches it (TER-5e) and no
rule releases it — it is re-laid from the pose every tick and it is gone the tick the body moves.

- **What it covers is the box the body stands in, projected onto each way's own line** — its length where it
  lies along one, its width where it lies across one, and neither where it lies at an angle. A body askew
  read at a single radius is wrong on both axes at once.
- **And it is the box clipped to that way's band, never the shadow the whole box casts down it.** The shadow
  of a body standing at an angle is its own length on every way it touches, however little of it is on any
  one of them: a car turned across its lane reaches the corner of the next by a hand's breadth and shadows
  four metres of it. What it holds there is the corner.
- **It is laid from the pose and never from a register.** Which bay a car is standing in is a claim in the
  register (`GEN-4g`), given back by the manoeuvre that drives out of one and by nothing else, so a body
  taken out by a hand at the wheel, a shunt or a recovery arm is a car on the road that the register still
  calls parked. Laid from that claim, such a car held two ways of a bay it was streets from and no metre of
  the lane it was standing in the middle of.
- **A body is *on* a way once it has crossed that way's edge**, and the write asks nothing else — not
  whether it obstructs, and not what anybody would call it. A car half over the paint claims both
  lanes it is half in. Withheld until the body obstructed the band, a car straddling the line left most of
  each lane clear of it and was written onto neither: it stood in the middle of a road that could not see it.
  **Crossed and not merely touched**, by a figure of the town's: a stretch has no width, so a wing mirror
  over the paint would claim the next lane for as long as it hung there, and the two lanes of
  a carriageway would trade bodies on the noise in a pose.
- **And what kind of ground the way is laid on is not a question the write asks** (TER-5c.1). A body
  holds carriageway, footway and parking bay on the same terms, whatever kind of body it is: a person
  standing in a lane is a stretch of that lane, a car that has mounted a kerb is a stretch of the pavement
  under it, and a car standing in a bay is a stretch of every way that bay is worked off (`GEN-4f`) for the
  same reason and by the same walk. The one exception is the car on ground two networks share — a crossing is
  carriageway a walk runs over, so a car on the paint claims the road alone, and what holds a
  walker off it is that stretch of the lane. A body on foot there writes both, because that look-up is never
  asked about it.
- **Every stretch carries the span its holder covers across its way's own line**, which is the one thing it
  says about the third dimension and the whole of what makes the rule above affordable. A stretch has no
  width, so without it a way written onto is a way shut, and a town whose every turning car closed the lane
  beside it is a town that stops.
- **Whether that body is a queue to wait behind, a shape to get past, or nothing at all is the reader's**
  and never the row's. One body is a queue to the lane it is driving, an obstruction to the lane it is only
  lying across, and nothing to the lane it is merely clipping the edge of. **A body is in the way of an
  asker when the two of them are nearer one another across the way than half the width of what travels
  there** — **the line and not the band**, because two metres of lane left over on the far side of a body is
  no use to somebody whose own line runs through it. **Half a car on a carriageway and half a body on a
  footway**: it is one statement about a way and a figure apiece, and a walker takes half its own width
  either side of the line it walks exactly as a driver does.
- **It is a question about the pair and never about the holder alone.** Where across a way the asker is
  belongs in it: an asker travelling the line asks exactly what it always asked, and one that has stepped
  aside asks about the ground it has stepped to. Read off the holder's distance from the line instead, a way
  is two-dimensional for whoever is written into it and one-dimensional for everybody reading it — and then
  a body cannot move out from under an answer it has been given, which is a pavement nobody can ever get
  past anybody on (`PER-24`).
- **It is written where nothing else already answers for the ground** (SIM-7). A driver under way has its
  own claim on the ways of its line and the crossing table on the box it is crossing (TER-5c.1); a
  second copy of either would be one refusal made twice, and a body nobody can give up deadlocks what a rank
  was there to resolve.
- **A coupled pair is one occupant of it** (`TER-5c.2`, `EVA-5`). The car on the bar is one movement's worth
  of body and holds the ground it is dragged over under the number of the vehicle pulling it — so the truck's
  own grant is not cut at its own trailer, and the lane the trailer swings into as the pair turns is ground
  the town can see something in.

**TER-4c.1** **Ground is asked for, answered, and then it is the asker's.** A body puts the stretch it
wants — **the margin it keeps, itself, and the road from its nose to where it means to be able
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

- **A claim is anchored at a body, which is what makes it order-free.** Its near edge is the asker's
  own tail, so every ask is laid before any of them is answered and two bodies need no order to be resolved
  in: each is cut at the other's near edge and the answer is the same whichever is asked first.
- **Ground nothing of the asker's own reaches is a claim ahead, and one is checked before it is laid** — the
  places another way is driven over the one a car has committed to, whether that way is a junction's join or
  a bay's way out, and the band of a zebra a walker steps into. There is no tail to anchor the answer to, so
  the other claims are asked first and the ground is taken only if the answer is yes.
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

**TER-4c.3** **No metre of any way is in two claims at once.** A stretch that would share ground with one
already on the way is **cut back to where that one begins or ends** before it goes in, so the two abut on an
exact metre and neither reaches into the other. There is no tolerance in it and no nearly: touching is the
seam between two claims and is not overlap.

- **What decides which of the pair gives the ground up is the body on it.** A claim whose body stands over
  the shared metres beats one merely reaching across them, whatever either is claiming with — and between
  two bodies it is whichever is further back, which is the same answer the grant arrives at. That is what
  makes the table the same whichever of a queue is laid into it first.
- **Below that it is the ladder** (TER-5g), and a tie goes to whoever is already there. **Two bodies
  genuinely abreast of one another on one way are one stretch and one seam**: which of them holds it is not
  a fact the town has an opinion about, only that exactly one of them does. The one that gave it up is still
  standing there — its own shape reaches past what it holds — and that costs nothing, because the ground is
  claimed either way and it is the ground that a reader is asking about.
- **One stretch and never two.** A claim cut at something in front of it gives up the metres beyond that
  thing rather than resuming on its far side, because a body is one stretch of one way (TER-5c.2) and every
  reader is built on that. **What the loser gives up is ground it could not have reached without crossing
  ground it was refused**, so for everything laid from a line the two say the same thing. It is not yet true
  of a stretch laid from a *pose* that reaches past a body — a swerve's corridor, a movement's runs — and
  those are the open edge of this rule rather than a settled part of it.
- **A refused ask is not a claim on ground and is not in this** (TER-5g, p10). It is a mark left where a
  body was told it could not go, laid over the very stretch the traffic holds, because what it says is that
  somebody is waiting for those metres.

The rule this is: **one that could grant the same metre twice would be no mechanism at all** (TER-4c.1). It
is kept by the structure the claims are held in rather than by the passes that fill it, so it cannot be lost
by adding a caller.

**A claim is on a way, and every way of the town is one table** (TER-4c.2): a lane, the join across a
junction between one lane's end and the next one's start, the ways a parking bay is worked off, and **the two
sides of every pavement with the mitres between them**. **They are told apart by the kind of ground each is,
and by nothing else** — a way is a length and a run of metres, and which of the town's features drew one is
that feature's business. That is what makes a junction answer the same way as the rest of the road, and it is
why the ground where two movements meet (TER-5c) needs no register of its own: each of them is a stretch of a
way somebody is travelling, and the table says which pairs of ways to read against each other.

**One numbering, because a claim that cannot be compared with another is not a claim.** The lanes are
numbered first, the joins after them, the bays after those and the pavement last, so a reader holding a way
number asks the same question of every kind of ground. Held as a network apiece instead, one piece of the
world had two records that nothing could compare: a walker was granted the footway a car was parked across
and each book was right about itself.

**What kind a way is decides what has to be clear of its line and nothing else** — half a car on anything
the traffic drives, half a body on anything it walks (TER-4c.2). It never decides who may claim: a person in
a lane, a car on a kerb and a bollard on a verge are the same kind of fact to the ground under them.

**The table of crossings is therefore indexed by way and not by movement.** A junction's join is only ever
driven over another join, because the lanes hand over clear of the box (TER-5d) — so every lane's row is
empty and the table reads as the junction table it began as. What needs the wider index is a way laid
*along* a street rather than across a box: the line into a parking bay leaves its lane part-way along and
sweeps the lane running back the other way, and a table that could only name joins could not say which
ground that was.

**A person in a lane is a body like any other and carries a reading of its own.** It cuts the road a driver
is granted exactly as a car standing there would; it is waited behind while it is moving, and once it has
come to rest it is something the rule that drives round an obstruction (`E-4`) may act on. **What keeps a
swerve off it is the ground it holds and never a name it is refused by** — its own claim carries
a margin (PER-15), and a template laid over that stretch is refused by the same test that refuses one over
a wreck. Naming a second rule to refuse the same movement would make the first useless (SIM-7).

**A body's own claim is the one thing that is never held against it.** An occupant is an index into one
of two rosters and the stretch carries which, so a car excluding itself by number does not also excuse the
walker that happens to hold the same number.

**A crossing is carriageway and not a unit**, on both sides: what a body has of a zebra is the band of it
one lane wide that it is on. A car has the stretch of its own lane, which is the same stretch the rest of
its road is and is claimed where the rest of its road is claimed — on the road, once. A walker has
the band of the lane it stands in, and the band in front of it once that one has been granted (PER-15).
**A band is a stretch of ground like any other**, so the two sides of the paint are held apart by the answer
that holds the rest of the town apart, and **neither of them is ever refused by the paint**: a body that
cannot cross is one the lane under the paint already belongs to.

## Markings

Everything painted on the ground is **engine-drawn primitives, never art**: lane centrelines (dashed,
stopping at the outermost paint an arm carries — its bar where it has one and its crossing where the
junction is unlit — rather than running on into the junction behind it), kerb lines (broken exactly
where the pavement's edge is, and over a car park's mouth, where the ground on the far side of the line
is the lot's own tarmac and there is no kerb to be the edge of), pavement and deck edge lines, stop bars
(square across *that arm's* direction, covering one lane only — the one driving at the paint — and
stopping at the kerb), zebras
(spanning kerb to kerb, running along the direction
of the traffic that crosses them, between their bar and the junction without overlapping the bar, or
between their two bars at a node that forks nothing), bay
outlines (three-sided, open at the mouth, so a row of bays leaves no line across the ground a car enters
the lot over, and laid against the lot's own edge — inside it — wherever they stand within a line's width
of one) and drift marks.

**A car park's paint and the road's are one line where they meet.** The strokes at a lot's mouth end on
**the carriageway's own edge** and not on the lot's rectangle, which is a chord of that edge and stands up
to its sag inside it; the kerb line is broken over **the mouth** — the lot's road-facing edge, not the
shadow its whole rectangle casts along the road — and stops a line's width short of either end of it, so
the corner where the two turn into one another is painted and painted once. A gap at that corner is a gap
in the one place a driver entering the lot is looking.

Six rules govern all of it:

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
6. **A mark laid along a road is laid on the road's own curve**, never on the chord of it. A dash struck
   straight across a bend stands its own sag off the line it marks, which at the radius a town's tightest
   bends are laid to is most of a line's width — the line reads as a row of tangents rather than a curve.

## What this slice must produce

- A road graph: one node per junction, directed lane edges, lanes cut at **every** junction a road runs
  through rather than only the two it ends at, and at the places a slice above asked for (`GEN-4h`).
- A turn classification per pair of lanes — straight / near-side / far-side — **filled once when the town
  is laid** and read off thereafter, and **nothing at all between a lane and the one running back down its
  own stretch** (TER-5f). Which turn joins two lanes is a fact about the road, not about the car on it.
- A crossing registry queryable by junction, and a stop-line registry carrying the bars actually painted.
- A table, filled once from the lines themselves and **indexed by way**, of where each of the town's ways
  is driven over the others, in both ways' own metres (TER-5c). **There is no register of who is inside a
  junction**: the table is looked up and the answer comes off the claims everything else reads
  (TER-5c.1). It is laid over every numbered way, so a slice above the road can measure its own
  ways into it with the same code and be read by the same walk.
- A lane occupancy index over the ways of TER-4c — the lanes, the joins between them, and the ways a
  slice above lays off them — carrying every
  body on the network and the stretch each driver has taken, so that **who is in front and how much road
  is whose** are answered from the town's own claims rather than from geometry (`S-2a`). It is laid over
  ways the caller measures, so the pavement keeps a second set of the same kind (PER-13). **The two
  are told apart by which network the ground belongs to and never by which kind of body is standing on
  it.**
