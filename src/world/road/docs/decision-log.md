# Decision log — roads and junctions

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md) and [claims.md](claims.md); how
a type works is its own XML docs.

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

## 2026-09-05 — the pavement is the tarmac wrapped, and a junction is not a case

The walking network was six constructions and not one was the pavement, each laid off a *record* rather
than off the ground beside it — a centreline, a disc, a fillet, a box — so where the records and the ground
agreed the answer was right and where they did not nothing said so. It is one construction and one rule
(TER-3c.3): `Kerbs` states the tarmac as one shape, every piece offers the line half a walk outside itself,
and a metre of such a line is pavement exactly where nothing else stands nearer. Nothing is matched, pushed
onto anything or joined across a gap, and `FootEdgeKind.JunctionCorner` is gone. The offset is compared
with a rounding's grace, which is the whole of what makes the wrap exist — a junction's disc is drawn to
the width of its arms, so the wrapping circle runs half a walk from both for its whole length, and compared
exactly whether the pavement exists is the last bit of a float. A rounding and never a tolerance: at five
centimetres a tangential meeting overruns by half a metre each and the pavement comes apart into a piece
per corner. And the ends are stitched, because what is ill-conditioned is the crossing of two grazing
curves and what is not is the distance between two cut ends. What is checked is the claim itself and not
the derivation restated (VER-12).

## 2026-09-05 — a connector is an object, and where lanes meet is worked out from them

A movement was a lane paired with a slot index, and the pair travelled together because neither number
meant anything alone. A connector is the id and answers all of it. The plan's node table is gone from the
graph the town runs on: two lane ends are one place when a connector runs between them, or when they are
the two ends of one stretch driven either way, and `LanePlaces` works that out once so the contraction and
the claims cannot disagree about which lane ends are one piece of the world. The second clause is not
tidiness — no box admits the turn-around (TER-5f), so joined only by connectors a dead end's two lanes
would be different places and a leg could not be priced round a bay (GEN-4l). A spatial index over every
way was the alternative and needs no places at all, refused because the span a caller must give the walk
then has no bound the town can state.

## 2026-09-05 — the corner is cut off the lane rather than marked on it

A setback was a number beside a lane rather than a fact about it, so everything downstream carried the
difference — the assembler threaded a sub-chain, a place on a lane had a non-zero origin, and the overlay
drew the ground past each figure twice. The lane is trimmed to what the widening leaves, so its own first
and last points *are* its connection points; `JoinedAtM`, `LeftAtM`, `JoinFromM`, `JoinToM` and
`LaneOriginM` are gone. The other networks keep their two figures, which is the point of the interface: a
pavement hands over at a point per turn and a bay's way runs past the pose because that run is ground and
not line. Still owed: a lane is cut at the junction's *disc* and not at the reach its kerb corners paved —
cutting at the reach was tried and deletes short stretches outright.

## 2026-09-05 — a one-way road is a narrower road, and a corner is solved on the pair it stands between

The lane graph was already directed, so the whole of what a one-way road needed was to lay one lane instead
of two down the middle of half a carriageway; a full-width road with a lane nothing may enter is a rule
every drawer, claimer and walker has to be told about. Which way it runs is carried and never inferred,
since a narrow road is not necessarily a one-way one. Which half of the carriageway it is was the part we
got wrong first: laid down the middle of its own line it is a narrowing rather than a street, so every car
through the junction stepped sideways. It stands on the half it is driven, moved after the bends and never
before them, and a node with no fork goes with its arms. What actually broke was the junction, and it was
already broken — every corner was solved as if both arms were the same width, which until now they always
were. It is the crossing of the two kerb lines each offset by its own road's half, and whether a corner
exists at all is measured off the narrower arm.

## 2026-09-04 — one table of ways, and no metre of one in two claims

Two `LaneOccupancy` instances in two numbering spaces meant nothing could compare a claim in one with a
claim in the other, so a car parked across a footway and the walker coming down it each held the same
ground and each book was right about itself. There is one table now (`TownWays`, TER-4c.2), and the kind is
a property of the ground rather than a table it lives in — it decides how wide the ground is and what must
stand clear of its line, and nothing about who may claim. The claims on a way are disjoint by construction
(TER-4c.3): the insertion was a splice that never looked at what was there, and exclusivity lived only in
the grant passes, which only shortened the asker's own far edge. What decides which of a pair gives up the
ground had to be the body and not the rank, or two bodies of one rank are settled by whichever went in
first. A refused ask is the one exemption, being a mark and not a hold. What is not settled is a stretch
laid from a pose that reaches past a body — truncated, the far half is ground its holder is committed to
and nobody holds; laying it as a second run breaks `Withdraw`, `CutTo` and `AlreadyHolds`, so making them
disjoint needs TER-5c.2 opened first.

## 2026-09-04 — a way has one property, and it is a claim

There were two words for one row: a use said how a stretch had been measured and a rank said how strong the
hold was, and two of the seven uses meant the same thing as a strength. A way is used when there is a claim
on it: who is claiming, where, and at what priority (TER-5g). Everything a reader used to get from the use
is worked out from those, except whether the body is following this way's line or merely standing on it,
which turns the margin. The masks became a ceiling on the ladder, which is what says the shape is right.
Five levels and not three — `Firm` and `Soft` differ by one `=`, and collapsing them either deadlocks two
crossing movements of one rank or leaves weaker movements waiting on ground the other was only thinking
about.

## 2026-09-04 — a driver states the road it means to use, at a strength anything stronger can take

Every driver could read the others' commitments a reaction interval out and none declared its own; the
read/write asymmetry was the defect rather than the length of either half. It also left the rank ladder
almost nothing to rank. A car now lays a second stretch beyond the one it is committed to, and **the
holding time is what tells a plan from a commitment** — at the decision interval the stated claim is
arithmetically the committed one for any car already doing the speed it plans for. That is the ask the
committed claim deliberately is not (2026-08-22, below, which still stands and is why that one is short);
the whole difference is that this one can be taken. A tie does not refuse, since a stated claim is laid by
everybody in the same rebuild. A body that is not moving states nothing — sized from a standstill it is the
pull-away horizon of every queue in the town, and the weaker movement in seven exam cards never went at
all. The rank a car asks a box's ground with had to become the rank it holds it at. The signals needed no
change, which was the surprise: a red is already a stop point the ask is clamped by.

## 2026-09-03 — a body on foot holds the paint it stands on, and only a car is left off it

The exclusion that keeps a car on a zebra off the pavement was applied to whoever was standing there,
walker included — costing a walker its claim on the only network another walker reads. The argument that
covers a car does not reach a person: what holds a walker off a car on the paint is that car's stretch of
the lane, and that look-up asks what traffic is *coming*, which a person standing in a lane is deliberately
not an answer to. `StandInTheWay` writes every way its own box touches, the paint among them.

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

`StandInTheRoad` asked the terrain grid before it asked the geometry anything, and the grid answers to a
metre cell — so a walker a stride into the road claimed no lane. A band is what says which way a body is
on, for a car and now for a walker; the grid is what a body drives and walks *over* and was never fit to
say where one is. And the claim was widened by `Person.RoadClaimMargin` on top of the two metres a driver's
own `LaneCredit` already stops short of, which is `SIM-7` exactly. The margin belongs to whoever is doing
the braking.

## 2026-09-02 — a body holds the ground it stands on, and what to call it is the reader's

The write was gated on `Driven && !Broken`, which is the reader's conclusion deciding whether the fact got
written at all — so a car under a hand sitting in a box was ground every crossing driver read as empty.
Every body writes the space it occupies and the row says only how it was measured (TER-4c.2). The box is
projected and not approximated, since a body askew reaches its own length across the way beside it and its
own width along the way under it. A body is on a way the moment it touches it, and the stretch says how far
aside it stands — withholding the write until it obstructed the band left a car straddling the line written
onto neither lane. What a body covers is the box clipped to the band and not its shadow: an angled car
reaching the next lane by nineteen centimetres held four metres of it, and a fact eleven times too big is
not a safe approximation but a lane shut by a wing mirror. A sweep is two poses and not a corridor, or a
car swinging into a bay holds seven and a half metres of lane for a body under four. And the line has to be
crossed rather than touched, the allowance across and not along. What is deliberately not written is what
something else already answers for (SIM-7) — writing a body onto the other joins deadlocked the crossroads
outright. The lane running back against a driver is left open, since nothing is driven between a
carriageway's two lanes (TER-5f) and two bodies meeting there cannot be ordered. Two registers were still
deciding where a body was, both of them bodies the town could drive straight through. A car on a tow bar
laid nothing at all — the reasoning held only for the ways of the hauler's own line. The walk stopped at
the node instead of crossing it, both faults being the walk asking about a body's middle rather than about
the body. And half the rule only ever applied to half the town: TER-5c.1 is a fact about a zebra and had
hardened into a fact about the body, so a car that mounted a kerb stood on a footway nothing walking there
could see. A body is written onto whichever network it is standing on, which wanted a name for the shape —
`IWayNetwork`, taken as a generic argument because a virtual call per way is the town's hottest path.
Naming the shape found a third network in the bays.

## 2026-09-01 — a junction with two arms is swept into the bend it always was

A node of two arms had the whole apparatus of a junction for a place where nothing turns across anything,
and TER-5b has always said a corner is a road that turns. The two arms are swept into one arc with the node
in the middle of it. The radius is the widest the two roads can spare and never wider than the class's
floor: the arc is tangent to both arms, so every extra metre cuts further inside the corner the layout
chose, and below the floor the bend is worse than the junction it replaces. Two things had been leaning on
the box without saying so — a lane line stopped at a junction only because the paint closed the ground back
to the disc, and a car park's section was kept clear of the disc rather than of the ground the junction
reaches.

## 2026-09-01 — a junction with two arms is crossed once

A crossing on every arm gave a two-armed node two of them, so what the pair stood for was one road crossed
twice. The node carries one, on whichever arm has the most road left behind it; Odesa lost 152 zebras
without losing a way over a single carriageway. The bars are the crossing's and not the arms', since there
is no box to hold traffic out of and a bar half a corner short of the paint stops a car for nothing it can
see; the bundle begins where the corner's ground lets go. The lane line runs up to the bundle and through
the node behind it, because what a dash must not be laid on is ground the movements through a box are
driven across, and this node has none — three claims stated the box rule and in each the claim was wrong
rather than the paint. The bars are painted though nothing there is lit: the only thing governing the
crossing is the walker's right of way.

## 2026-09-01 — a centreline stops at whatever paint the arm carries, not at the bar

The rule keeping a lane out of a junction's throat was written round the stop bar, and a junction the
ranking governs carries no bar (TLT-3) with the same metres of turning ground — so nine metres between the
disc and the zebra came out dashed. Each piece of paint on an arm names the junction it approaches and
closes the ground from itself to the far side of that disc. One loop and no new figure.

## 2026-09-01 — a zebra spans the road it names, and carries no span of its own

A crossing carried a span every planner filled with the width of the carriageway it was laying, so the
field agreed with the road until something laid one of the two again. A crossing names the road it is
painted across and `CityPlan.CrossingSpanM` solves the reach off that road's width (TER-6). The depth stays
the crossing's own, since how much of a road's length the paint covers is nothing the road decides. The
skew is part of the relation and not an exception: `Zebras`' off-square crossing is 8.83 m of an 8.00 m
road, which is what the file's field held and what the derivation gives without being told.

## 2026-08-29 — a body level with the asker is not in front of it

Three crowds queued behind a pair that never moved, each of the pair granted minus two metres and each the
other's cut. A walker past the end of its way is carried back to the last metre of it, so its ask is a
window of no length and every body clamped there has the same front — and what reached the cut was the body
whose front is exactly the asker's, which is neither in front of nor behind anybody. `GrantedOn` passes
over that one: a cut behind the asker's own front is not a shorter grant, it is a grant that has stopped
being a distance to walk.

## 2026-08-29 — a grant is a distance in front of the nose, and a claim behind it is not a cut

A car would stop halfway out of a box with the road ahead empty, its grant minus seven metres — negative
road, which inverts to zero whatever is ahead and which nothing can hand back. A way the nose has left was
still being asked, and the stretch reaching it was the queueing car's claim laid from the leader's near
edge, answering the leader from underneath its own body. A claim is ground its holder has not reached
(TER-5e), so one the asker is standing on is ground the asker has. What is still allowed to answer from
behind is a body: a wreck reaching back past the nose is an overlap, and the grant is left free to come
back negative and say so.

## 2026-08-29 — the claim holds the answer, and one metre is one body's

The code granted correctly and then threw the answer away, leaving the ask standing as the claim — so every
other reader for the rest of the tick read the *question*, and a car held at a red still held the sweep of
road beyond it. Two bodies held one metre by as much as 13.72 m on Fleet. There is a third walk,
`CutTheGroundToTheGrant`, and it is its own because it moves far edges, which is what a movement's crossing
question reads. On a join a car is crossing the seam moves and the union does not, so
`ClaimWhatTheAnswerTook` hands the metres over as a claim. And the credit is gone with it: a stretch worth
its holder's stopping distance is, once the answer is written back, two bodies holding one metre. What it
buys is the junction — Odesa abandons 6 cars against 15. What it costs is station-keeping at speed, and one
proving-ground claim is broken and left broken, because papering over it in the rig would be measuring the
ruler.

## 2026-08-29 — the box refuses a car at a place, and only lets it in where it can wait clear

The gate answered *whether* and the grant answered *where*, the same question at two resolutions, so a body
on a box's far corner held the near half against a car that would never have reached it. The gate answers
in metres on the same figure, and the body margin is what keeps it from deadlocking — a car held a margin
short claims no metre of the section, so the crossing movement still reads it free. What it cost until the
second half went in was cars stranded in the box, so a car may only be let in as far as it can come to rest
with its whole body in a gap between the runs.

## 2026-08-28 — the walkers claim the road between the asks and the grants

The walkers claimed the road as the *last* pass of the rebuild, after every grant had been taken off it, so
no driver ever read a band while deciding how much road it had. The order is one question with the walkers
on both sides of it: the cars' asks, then the walkers, then the grants. River went from two knocked down
and two wrecked to none of either.

## 2026-08-28 — what a body is written onto and what a manoeuvre reads are one walk

A car crossing a junction is written on the join and on no lane (TER-5c.1), and reading the ground under a
template asked the nearest lane and stopped — so such a car was invisible to every swerve and bay exit
swinging through the same box. The claims were not wrong; nobody was asking them. There is one walk
(`GroundUnder`) and both sides call it. It costs the town its reactive templates where the ground is
genuinely somebody's, which is the finding rather than a side effect: reversing into a junction is
reversing into ground the traffic crossing it is committed to, so the ladder escalates instead.

## 2026-08-28 — a claim is answered every tick, and its holder is told when it loses

A claim was answered once and re-laid unread for as long as the entry wanted it — the one hold that
remembered an answer — so a right of way took the ground and nothing said so, and the pair drove at the
same metres from opposite sides. It is answered again after every body has claimed and before anything is
granted off it, and the holder is told, because the only thing that knows what a claim was for is the entry
that took it. A rank above it takes a claim and nothing else does: giving one back for a body standing on
the ground is the duplicate SIM-7 is about, and refuses the one thing a claim is for, since the stretch
`E-4` claims is by construction the one containing the body it is swinging round.

## 2026-08-27 — a junction admits no movement that reverses the direction of travel

The turn-around was in the table from the beginning and drivable by nothing — two opposing lanes join on a
1.5 m semicircle — and was classified, laid, measured, ranked and priced at infinity, which is a great deal
of machinery to say *never*. It is gone (TER-5f), and a quarter of Odesa's 1472 movements went with it.
What a route may still do is come back down the other side of one stretch, priced rather than joined: a car
park's frontage (`GEN-4l`) and a dead end (`P-19`).

## 2026-08-27 — an obstruction is a claim that generally reaches nowhere

A body the road was not driving held the metres under it and nothing else — right for a wreck standing in a
lane and wrong for the same wreck two seconds earlier, in the direction that costs. The pavement never had
this problem. Such a body now lays its own stopping distance past where it stands, from the speed it
actually has, so nothing is a special case: it is the same arithmetic a driver's claim is, asked of a body
with nowhere to go. Where the body is sweeping a template the sweep is that ground and is already laid. The
measurement threw out the tidier version — a margin behind the stretch reads better and is a fatter body in
*every* question asked of the claims, taking Odesa 69 touches to 88.

## 2026-08-27 — the grant is a question the claims answer, and both networks ask it

The road's grant and the pavement's were the same forty lines twice, both switching on the use to decide
the credit and both making a second cut at a place with the same margin subtracted by hand. The grant is
`LaneOccupancy.GrantedOn` and what the asker brings is `LaneCredit`, so the credit rule and `Binds` exist
once. A walker asks with the weakest rank, which is the honest statement of what was already true. It
changes no arithmetic: the map from a way's metres back to the line is affine and increasing. Two smaller
things fell out — a rank is a floor on the walk rather than a filter over what came back, and `Nobody` no
longer matches itself, which had been excluding every bollard in the town from its own answer.

## 2026-08-25 — where a road's paint breaks is the road's answer, not the drawing's

Dashes were laid by walking each road and asking whether the point was inside a disc or on a zebra, so
every arm was dashed right up to the mouth of the box past the bar a driver stops at; the metre step was
the smaller fault. The boundaries are no longer looked for — `CentrelineRuns` takes them from whoever
measured them. The kerb line went the same way: a car park is laid flush against the kerb rather than over
it, so its fill covers none of the rim, and what breaks the line is the frontage itself, owned by
`RoadFrontages` for both slices that need it. A bay is drawn on three sides, since a row of four-sided bays
runs its mouths into one unbroken stroke. The three are laid end to end and not each to the bay's own size,
or half a stroke of each corner is painted twice and half not at all. A stroke within a line's width of the
lot's edge is laid against that edge, inside it. The frontage projects the four corners rather than the
centre plus a reach, which is exact on a straight road and nowhere else. And the two lines were laid to two
different edges — a lot is a rectangle and a kerb is a curve — so `ReachToTheKerbM` answers where each
stroke's own line crosses the carriageway's edge. Overlap costs nothing, since the ground carries no
blending; only a gap is visible.

## 2026-08-25 — the right of way is a rank on a stretch, and it takes claims and nothing else

Two crossing movements each read the other's ground and each were cut at it, so a junction went to whichever
asked first — the order the rebuild happens to walk the cars in. A table of pairs was considered and is the
verdict TER-5c exists to avoid: it answers *may I go* for a whole junction and says nothing about where. A
rank carried by the stretch puts the comparison exactly where two pieces of ground meet. What made it safe
rather than merely one-sided is that it takes a claim and never a body — a committed claim is the road a
body needs to stop in, and taking that is a licence to drive into whoever holds it. The one hole was a car
past the point it could stop, which lays the same claim at a rank nothing outranks. Revocation is the same
fact read the other way and is bounded the same way. Both halves happen inside one walk, so *already
crossing* is measured from before the walk and not from the fleet the tick leaves behind.

## 2026-08-25 — a walker's refusal is a claim of its own, because a right of way nobody can see is not one

A walker refused the band it asked for simply waited, and the only thing a driver ever saw was a body
already on the paint, which is the one case where giving way is too late to be a courtesy. The ask itself
is claimed on the road at a rank of its own. It is in no scope that cuts, because a cut is what a *body* is
worth; what it does is put a stop point in front of the driver, and a body stopped short of a crossing
holds none of it (TER-4c.1), so the band frees itself on the next tick. The safety of it is the stop's own
bound and not a rule beside it: a car too close to the paint keeps it, so nobody is waved in front of a
body that could not have stopped for them.

## 2026-08-25 — an inline junction's crossing is laid across the lanes at the node

The one thing TER-5b says an inline junction exists for did not work: the paint is laid on the node itself,
further from every lane's end than the paint is wide, so the projection found no lane and a walker standing
on it was invisible to the traffic. Every such crossing in the shipped towns was lit, and the lights hid
it. It is laid across the lanes that meet at the node, each at its own end. The fallback is taken only
where the projection found nothing *and* the junction admits no turns.

## 2026-08-24 — the town's furniture is a claim nobody owns, and not an occupant number

A bollard was claimed as an obstruction belonging to `Nobody`, which is also the integer a query names when
the asker has claimed nothing — so the walkers' traffic questions were skipping the furniture because the
exclusion they asked with happened to name it. The answers were the ones a town wants, reached by an
argument of one question deciding the answer to another. A prop is in neither roster
(`LaneClaim.IsFurniture`); nothing about the town moved, and what moved is where the answer comes from.

## 2026-08-24 — a road may be cut where nothing crosses it

A car park wants a node of its own (`GEN-4h`) and is not an intersection: taking a disc's worth of ground
out of the street for it would be a box invented to hold nothing. A cut is a disc's bite or a point, and
the nodes are numbered after all the plan's junctions so nothing is renumbered. The cut is asked of the
plan and never of the graph, since a construction that read a lane to decide where to cut would need the
graph it is building. It gives way to what the road already carries — a cut on a zebra splits the approach
from the paint, which was not a guess but River, where cars met such a crossing at 18 m/s.

## 2026-08-24 — the table of crossings is indexed by way, so a way laid off a junction can use it

The table said which *movement* took ground off which, which was right while the only ways that could
overlap were the joins through one box. A bay's way in leaves its lane part-way along and sweeps the lane
running back, so what it takes ground off is a **lane**; a second table for bays is the duplicate SIM-7 is
about and is the one place it would have been got wrong. A section names a way and the table is laid over
every numbered way, and `LineOverlap` is lifted out so the ways at a bay are measured by the code that
measures the joins. What it cost was the whole-way fallback: near enough between two joins a dozen metres
long, it was the whole street against a two-hundred-metre lane. The missing end is now the found one's
shadow.

## 2026-08-23 — a template holds the ground it sweeps, and not the pose it is passing through

A car driving geometry of its own claimed the footprint it stood on and nothing more, so the line it was
about to drive was left open — every other driver read it as free road and could come to rest in it. Odesa
found it as two wrecks a minute. Such a body is laid over the whole sweep its line has still to make. Read
from both ends and laid once, since the ways under one end are regularly not the ways under the other. A
bay exit is no longer one of them, and that is the shape the rest should take. What it does not do is make
the reverser see: what stops the collision is that nobody else is granted the ground, which is the same
mechanism and not a second one (SIM-7). Odesa after it: 0 wrecked and 46 touches against 4 and 56.

## 2026-08-23 — the tail keeps a share of the margin, not the whole of it

The margin sits at both ends of a claim and the two ends are not paid for by the same traffic: in front it
is this car's own cover, and behind it costs whoever comes up behind — every metre is road the follower is
queued out of. The tail keeps a share, and the standing gap at rest falls to 1.2 m, which was never the
follower's to choose. What it cost was wrecks, and they were not the margin's: the step is at the first
metre under a body's width and does not deepen below it, and every wreck was a back-off reversing into a
car stopped inside its straight. With the template hole above closed, 0.6 runs 0 wrecked and 46 touches —
fewer than the full margin gave.

## 2026-08-23 — one body, one stretch: the margin is part of the claim

A car in a junction held a release margin behind its tail as a claim of its own: two occupants to every
walk of the join, and only true *in junctions* though nothing about the reason is. The margin is in the
body's own claim on every way the body is on, and the release figure and the follower's standstill gap are
one figure — the ground a body keeps around itself. The measurement is kept as a floor rather than a second
figure, so a fleet tuned to queue closer than the soak's floor gets the floor; what makes reusing one
figure safe here is that the *union* is taken. Two things moved with it: *in front* stopped meaning "its
near edge is", since every stretch now begins a margin behind its owner; and a body not driving its
movement lays no claim, having claimed the runs of its join whole over the very metres it was already held
on.

## 2026-08-23 — a zebra is ground with a lane under it, and not a thing the traffic holds

A car crossing a zebra laid a second copy of itself on the pavement, so one body held one piece of ground
twice under two names, and the overlay drew a crossing being *shut* rather than a lane being somebody's. It
is a lookup now, pointing the way the walker is going, off the `CrossingBands` already measured when the
town is laid. It is the answer that is carried and never the question: a body past its patience is granted
a band the claims would refuse, and a grant that re-asked would hold it at the edge of ground it had just
been given. The kerb stopped being a special case with it. A body holding the lane in front of it was tried
once and rejected, and what makes it right now is the reach — a body asks for the band its own ask reaches,
about a stride, so the far lane is held for the last step and not for the crossing.

## 2026-08-23 — a claim stops where a rule stops the car

`AskForTheGround` clamped the road at the place the car is held and then added the margin on top of the
clamp, so a car waiting for a zebra held a metre of the zebra — and a signalled crossing behaved like an
unsignalled one, the people getting over on their patience eight seconds later. The gap is part of what the
car asks for and is clamped with the rest. Nothing about following changes, and the ask only ever shrinks
at a stop.

## 2026-08-23 — a car claims the ways it drives and looks up the ways it is driven over

A movement wrote its crossing points onto both joins, half of which is a body claiming ground it is never
going to be on. It is a lookup: the table was already symmetric and carried both ends of every section. The
crossing claim stays and is the only thing a mover writes, since a driver's road ahead is a braking
distance and does not reach the middle of a box — two cars from opposite arms would each find the other's
join empty. The grant is where this had to bite and not just the commit test. And a section is a named
piece of ground and not the road under the asker: `NextSpokenFor` skips a stretch whose near edge is behind
the window, so a car whose claim entered a join before the crossing metres was invisible to the movement
crossing there.

## 2026-08-23 — the margin a body keeps is not the clearance the sections are drawn at

They take the same value and answer different questions — how near two lines pass before they are driven
over each other, and what a one-dimensional reading of a two-dimensional body owes whoever comes next. Read
as one figure they read as one rule, which hid that only the first had ever been measured.

## 2026-08-23 — a crossing claim is the run less the road, not the run

A car in a box held its own join's crossing points twice, once as road driven and once as claim, coming out
the same interval to the centimetre. Nothing computed a wrong answer, since the two are read as one set;
what it cost was that the claims stopped saying what they say. The claim ahead is the run less the
committed one and the test's exemption is gone. It is two pieces where the road ends inside a run, and the
near one is load-bearing: the metres between the tail and the clearance behind it are ground the body is
still swinging over — dropped as redundant once, and the give-back test caught it in one run. And the far
half of the give-back is not observable while a car is in the box, so it is the near edge that is counted.

## 2026-08-23 — a movement holds the crossing points on its own join, not the span between them

Held as one interval from first to last crossing point, a straight crossing two turns shut every metre
between them including the middle where nothing comes near. The near side is the same places the far side
is, merged where two overlap, given back on the same test. What is under the body on its own join is the
committed claim, which carries the length and the swing the interval was being asked to stand for.

## 2026-08-23 — a body is on a way across its band and along it

Whether a body stands on a way was a lateral question only, and a projection is clamped to the way's ends —
so anything lined up with an end answered at that end however far up the road it stood, and one car in a
box could shut movements on the far side of it. The test is taken along the line as well, against how far
the body reaches; inside a way it decides nothing, and it bites only where the clamp did.

## 2026-08-23 — a junction is committed to at the rate the car actually brakes at

The claim distance and the point past which a crossing is kept were the only stopping distances in the town
read off the pedal's cap while every stretch of road is sized by the follower's braking figure. The cap is
larger, so both erred the way that costs: a car past the point it could stop gave the sections back for a
bar it was going to cross anyway, and in between two ticks they read free. Neither noticed wet ground,
where the gap is widest.

## 2026-08-23 — a crossing is given back where it is passed

Ground taken for a junction was held until the car was out the far side, which on screen is a car half way
through a turn still washing the corner it came in by. A section is a place and a body passes a place once,
so a car gives one back when its own tail is a clearance beyond it. The tail alone is a metre too eager and
it wrecks cars — a section is drawn where two *lines* pass, and what has to be off it is a body, which on a
turn swings wider at the back. The car's own committed claim is not released with them: it looks like the
same fact and is not, and slid forward with the tail, Odesa's touching count went 51 → 232.

## 2026-08-23 — a junction is refused by ground, not by a verdict

Nothing drew the registry or the conflict relation, so what actually stopped a car at a junction was
invisible. Worse, the relation was almost complete: an average movement conflicted with 81 % of the others
at its junction, three quarters of it asserted rather than measured. It is ground now and only ground — per
movement, the section of every other join its own line is driven over — and it can be looked at on screen.
Three things had to be true, each costing a defect to find: a car crossing must hold its own join, since
its road ahead does not reach where two lines meet; a car making the same movement is not an answer, or
every queue refused its own second car; and the two out of one lane and the two merging into one are not in
it at all, both being the duplicate SIM-7 is about. Movements that shut a whole junction on their own: 416
→ 0.

## 2026-08-23 — a stretch runs out at the box's near edge

`WaysAlong` stopped walking when the *next lane* began, and the next lane begins on the far side of the
junction — so nothing was laid on a junction until the stretch reached clear across it. A car approaching a
box claimed none of it, was granted its road as though the box were empty, and could see nothing standing
in it. The guard is the near edge now.

## 2026-08-23 — a way through is kept until it is given back

The claim was not a claim: every tick recomputed whether the car was *entitled* to the movement it was
already making, from two figures that move under a car merely slowing down — so a driver easing off found
its own junction refused it. Worse was the half that never moved: nothing wrote the field away, so an arm
sitting at a red refused the arm the phase had just given the green to. It is one state, held by the car
and laid from the car, taken only by a driver nothing but the box is holding up. Past the point it could
have stopped it is kept whatever anything says, because ground given back there is handed straight back
next tick and in between the sections read free to whoever crosses them.

## 2026-08-23 — a body in a box is on the joins, not on a lane

A junction's ground was defended by one thing that only knew about cars that are driving. The claims should
have caught it and could not: a standing body was laid onto the lane it lies nearest, and a body in a box
is past that lane's setback (TER-5d), so the stretch went somewhere nothing walks. It is laid on the joins
— both ends of the nearest lane asked, and every join of the junction the body lies under — which makes a
body in a box refuse what crosses it in the same way a car crossing does.

## 2026-08-22 — a body is one stretch of a way, never two

A driver under way was laid twice on the same ground, and the two shared a near edge exactly, so every walk
of a way counted one car as two occupants. Nothing computed the wrong answer, because the masks kept them
apart; what it cost was that a claim could no longer be read as what it says it is. It is one stretch
carrying two far edges — `ToM` is the ground taken and `StandsToM` is where the body ends — and for
everything that is only a body the two are equal, so the distinction costs nothing to lay and nothing to
ask.

## 2026-08-22 — neither network's claims are one roster's

A walker on a crossing claimed the road and was in none of the road's questions: half right, since a walker
read as an obstruction is one `E-4` crosses the centreline to drive round and one read as a committed claim
cuts a car three lanes away. What that cost was invisible until the ray went — nothing cut a driver's road
at a body standing in it. `OnFoot` is in every query a grant is taken against and carries its own reading,
so the one thing it must never be is a property of the reading and not of which query happened to skip it.
An occupant is an index into one of two rosters and the stretch has to carry which, or the first walker
whose index matched a car's is read out of the wrong fleet. The walker's give-way arithmetic went with it:
the claim *is* that arithmetic, already done, from fresher numbers.

## 2026-08-22 — a crossing is ground, and it is taken by the band

A zebra was treated as a unit when it is a strip of carriageway a lane at a time, and both readings were
wrong the same way. The car's half was inert as well as coarse: laid from the crossing way's own start,
every such stretch sat behind every walker that could have been cut at one — 562,782 claims laid over a
minute of Odesa and not one grant cut. The walker's half was live and over-held, taking the band of every
lane the crossing crosses: 6,003 of 7,921 crossing stops were for somebody not in that driver's lane. So
the missing projection got built (`CrossingBands`): what either side has of a zebra is a band and never the
whole of it, and the near edge of a band is a place on the ground rather than the start of a way, which is
what makes a grant cut at it at all. The patience is spent on a named lane and given back when the body is
standing in it (PER-15) — reset each tick it buys one tick and starts again, latched to the far kerb it is
the whole-zebra picture back again.

## 2026-08-22 — braking has its own margin, and it is nearly all of the grip

Using the cornering margin for braking put the planned stop at 13.1 m/s² against the 21 the tyres actually
delivered, and every claim is sized by the planned figure — so a car held half again as much street as its
stop was going to use. A corner is held for as long as it lasts and its margin covers a bump, a camber and
the wheel still being turned; a stop is aimed at, straight, and over in seconds. Corner speeds are
untouched, which is the point of the figure being its own.

## 2026-08-22 — a claim is the ground a car is committed to, not the ground its plan would need

Asking for the whole stopping distance from the speed the profile was driving towards came out at a few
tens of metres on a town street and 215 m on open road — a quarter-kilometre of empty straight held by a
car doing a third of that speed. The ask is what the car cannot undo: one reaction interval of ground and a
stop from there, with the profile's figure as the ceiling. It still leaves room to pull away, since what it
asks for grows with the pedal rather than with the speed the pedal has produced. And a car nothing cut is
now held by nobody — against an ask the car is merely committed to, the grant inverts to the speed one
reaction interval reaches, so a car alone on an empty straight read as `queueing`, behind itself.

## 2026-08-22 — a lane end has one setback, not one per turn

Each turn was set back by exactly what its own arc needed, which is the least ground taken and meant a lane
had no single end — a straight handed over at the lane's end and a right-angle turn out of the same lane up
to 4.5 m earlier. Everything wanting to name that boundary had to name a movement to do it. The setback is
the lane end's, the widest its own movements asked for (TER-5d), and widening runs in rounds because
setting one turn back changes the arc of every other through the same lane end. Every movement in the
reckoning reaches a radius, because the one that never could is no longer a movement at all (TER-5f) —
counting it would have pegged every lane in the town at the cap.
