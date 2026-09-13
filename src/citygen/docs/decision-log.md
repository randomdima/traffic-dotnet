# CityGen — decision log

## 2026-09-13 — the lane layer is laid from the junction out, and everything that stood beside it is put down

**The order was the whole of the problem.** A road's curve was laid first and its lanes were cut out of it
afterwards, so the bearing a car entered a junction on was whatever the chord happened to leave, the
connection point was wherever the setback arithmetic landed, and the movement between two of them was a line
drawn to fit rather than a line a car was shown to be able to drive. `TER-5d` already described the model
the other way round; what existed was that model read backwards.

**The points are drawn first and everything is laid to meet them** (`GEN-46`, `GEN-47`, `GEN-48`). Each arm
of each node is given a bearing — the chord to its neighbour turned inside a drawn bound, tapered on a short
link by what that link has room to turn through — and a standoff; the movements are the biarcs between the
points that produces; and the road is the chain of biarcs that leaves on one bearing and arrives on the
other. A biarc meets both poses exactly by construction, which is what makes the bearings a fact about the
road rather than something approached, and a lane is that road's own line moved half a carriageway. Nothing
is cut back, because nothing was laid past the point it ends on.

**What that costs is a curvature nobody chose, so it is measured.** A road that bends tighter than its class
affords is straightened — the wander is what it wanted and the bearings are what it owes — and one that
still does is a link the town cannot lay. Non-intersection is the same kind of bound: two roads that cross,
or that pass closer than the ground they take, are a refusal, and the layout is repaired behind every
refusal and the town laid again until nothing is refused. **A chord test after the fact could not have said
any of this** — a road free to reach its own end bearings is not bounded by the chord between them — which
is why the rule that stated it as one is deleted rather than restated (`GEN-49`).

**Odesa lays 619 lanes and 1 209 movements over 332 roads in 557 ms** against 444, 1 033, 256 and 1 238 ms,
and **its boundary closes all 115 rings** where it used to leave one open by 0.122 m over 30 390 m. The
generated city closes too. The open ring was the merge settling which of two bands laying one stretch of
outline was the outer one; fewer, longer, smoother lines left it nothing to disagree about.

**And a node's centre is the layout's, which nothing moves.** The forkless sweep used to nudge a two-armed
node onto its chain's start, and with the standoff that would have been six metres — enough to put a bend
inside a locality of the junction beside it (`GEN-16`) and enough to make the points drawn at derivation
time different from the points drawn at generation. The centres are the key the whole arrangement is drawn
from, so nothing after the layout may touch them.

**The town it carried was put down rather than ported.** Parking went entirely, the buildings with it, and
with them the services that were eligible-buildings-near-a-car-park; the junction furniture, the signals,
the kerb band, the walk the ground answer widened every road by, the paint and the walking network's passes
all went the same way. **None of that is judged unwanted** — every one of them is named in the known gaps —
it is judged not worth porting onto a layer being replaced this month. What keeps the milestone observable
is the one thing outside the lane layer that was rewritten rather than deleted: the spawn stage stands the
brief's cars on the town's own lanes, one a lane, and `--bench maneuvers` enters the ordinary driving
entries on every map while the parking, paint and light entries are the set nothing entered.

## 2026-09-13 — the boundary turns where the ground turns, and a cut is not a corner

**Three quarters of the points in the town's outline were places nothing happened.** A ribbon is cut at
every crossing anything has with it, and most of those crossings are not hand-overs: two lanes of one
carriageway are cut at every junction either of them passes, a bay's way is cut by each of its neighbours,
and the boundary goes straight on down the same curve through all of it. The suite's own city came back in
7301 pieces, 5303 of which started where the piece before them stopped, on the same circle, at the same
radius. Every reader downstream — the mesh the ground will be triangulated from, the walk to be struck off
this line, the picture — would have had to work out for itself which of those points were corners.

**So a run is joined before it is handed over**, and a piece of a ring is now a piece the town's edge really
turns or changes radius at: 7301 pieces became 2008, and the fixture's 460 became 165. Nothing else moved —
the same 50 rings, the same 39 965 m of boundary, the same stretches kept.

**A join is measured against the first piece laid on from where the second starts**, and this is the whole
of the arithmetic. Laid on from the first piece's own start instead — the obvious form, the two ends
compared — the hair between two computations of the joint is carried the length of the join and comes out
the far end larger than the joint itself ever was: a straight laid in two halves two kilometres from the
origin read as a corner. **And it is a walk rather than a radius**: a road's bends are a curvature of a few
ten-thousandths and a centre kilometres away, so asking how far a place stands off that circle subtracts two
huge numbers and answers in what their last bits left. Measured that way the same straight came back as a
corner 68 times over.

**A ring has no first piece**, so the seam is not a case of its own: the walk set off wherever the
lowest-numbered stretch of the ring happened to be, which is as likely to be the middle of a straight as a
corner, and that straight comes back as the two ends of the run. The last piece takes the first, and having
taken it reaches further and may take the next as well.

**The same reading is taken of the lines before their ribbons are laid** (`ArcRibbon`). A lane laid in three
straights along one bearing is one straight, and every one of its pieces is otherwise weighed against every
piece of every ribbon near it. The city's ribbons went from 31 638 pieces to 26 680.

**And a pair of pieces that cannot reach each other is not solved at all.** Two places on one curve are
never further apart than the curve between them, so a piece's middle and half its length bound it exactly —
a pair standing further apart than their two reaches and a weld has neither a crossing nor an end on the
other. The merge of the suite's city went from 1.06 s to 0.81 s, and hands back the same stretches it did.

**What the boundary is asked in the suite gained a millimetre with it.** A row of bays stands side by side
and two lanes share the edge between them, so the town is full of places strictly inside neither band by a
fraction of a millimetre either way — and a station landing on one reads as ground on neither side of the
boundary. Asked at the middle of 460 pieces that never happened; asked at the middle of 165, it happened
five times. It is a tie and the millimetre settles it, the way the merge settles one.

## 2026-09-13 — a car park's bands are weighed as the run they are, and the boundary closes

**A car park lays a dozen bands down one lane.** Every bay's way opens with a straight along the lane it is
worked off, so a row of bays hands the merge a bundle of bands a millimetre or two apart, tangent to a lane
that is bending away from all of them. The town's outline through one of those came back in pieces: 18 runs
of open boundary on the laid city, 19 km of it, and the same thing drawn in the fault colour over every car
park on the shipped ones. Four things were wrong and each of them is the same thing said at a different
scale — **a figure was being compared against two readings of one gap**.

**The probe's own step was left in the reading.** The cover test stands a millimetre outside the edge it is
weighing, so a band whose edge is exactly coincident reads a millimetre, not nought — and the window that
called two edges the same edge was therefore a millimetre off centre. Two bands 1 to 3 mm apart both read as
the outer one and both were kept; that is one stretch of boundary said twice, and a ring cannot be walked
through it. The step now comes off the reading.

**A cut is a place, and every boundary standing at it is cut there.** A stretch is weighed at its own middle,
so two neighbours cut at different places have different middles — and over a gap that changes by
millimetres along its length, one reads a hair inside where the other reads a hair outside. The crossings
name one pair of pieces each; the cuts are now carried across to every ribbon within a weld of them, which
is what makes the bundle stop in the same places and answer the same question. Without it the city cannot be
closed at all: 127 runs open where there are now none.

**The bands at a place are weighed over the place at once.** Asked pair by pair, a band a hair beyond the
figure covers the edge while the band between them hands that same edge back to it, and all three copies of
one stretch go. They are read now as the run they are — every band whose edge stands within the coincidence
of the last, ordered by where its edge stands — and the run has one lowest number wherever it is asked from.
**Where a band's edge stands is not how far the place is off that band**: the distance is the same for a band
the place is a millimetre outside of and for one whose own ground starts a millimetre further out, and which
of the two it is, is the way that band faces there.

**And where the two readings of one gap still straddle the figure, the pair is kept twice rather than
dropped twice.** Dropped twice is a hole nothing downstream can close; kept twice is one stretch said twice,
which the rings settle when they are strung. So the band a line outranks has to clear the coincidence by the
arithmetic's own error before it covers that line's edge — the error is spent on the side that has an answer.

**A slit between two bands that face each other is nothing up to two centimetres**, which is a wider figure
than the coincidence and answers a different question: how near two edges lying the same way round are one
edge is settled by the number, and must not tie bands that really are apart, while how wide a gap between two
bands facing each other is nothing is settled by the geometry either way round and costs only the slit. What
it must not catch is a band whose square end is cutting *across* the edge rather than lying along it, which
is why it asks how squarely the band faces the walker.

Read off the suite's own city, the boundary went from 18 open runs and 19 km to none, and the stations that
did not have the driven ground on their right and nothing on their left went from 10 to none. The two
shipped cities were never gated on and are not now: River closes every ring, and Odesa is left with one — a
lens in one car park a third of a metre long, where two bands lay the same stretch of outline and only one
of them was cut in the middle of it. The merge hands it back as what it is.

## 2026-09-12 — a band facing you is read off the line and not off the edge you are standing on

**A lane's end cap was coming back as a piece of the town's outside**, three and a half metres of boundary
laid across the mouth of a junction with nothing at either end of it to string it to. The movement that
carries on from the lane starts where the lane stops, so the two square ends are the same segment and the
seam between them is inside the town — and the cover test said it was not.

**The millimetre the test steps out by is what it tripped on.** Beside a line, which way a band faces where
a place stands is the way from the line to the place, and the test read that off the nearest point of the
other band. Off the end of a line there is no such bearing: a place square in front of a square end stands
*on* the end, so the way from it to the place is the millimetre the test itself stepped rather than
anything about the band. Read that way, the lane and the movement each looked to the other like a band it
was walking out of instead of into, the tie fell to the lower-numbered line, and the seam was kept once
rather than dropped twice. **The way a band faces off its own end is the line's own direction**, out of
whichever end it stopped at, and that is now what is answered.

**The town's two computations of one place disagree by about a millimetre**, which is why the tie matters
at all: a lane cut back to where its movements hand over (`TER-5d`) and the movement laid from that hand-over
do not land on the same float. What closes that is the coincidence figure and it is the arithmetic's error
rather than a gap worth papering over — measured at 2, 5, 10, 20 and 50 mm, everything above 2 mm left more
of the city's boundary open, not less.

## 2026-09-12 — two cuts nearer than the weld are one cut, and the hole between them is gone

**Two boundaries that run along one another cross wherever the last bits of a float say they do.** A
movement leaves a lane tangent and at the same width, so its ribbon's edge lies on the lane's edge to within
a tenth of a millimetre for metres — and the crossing solve, asked of two curves that are the same curve,
answers with a handful of points a few centimetres apart rather than the one place they share. That is not a
fault in the arithmetic and there is no better answer to be had: the curves really are indistinguishable
there.

**What was a fault is what the merge did with them.** Each of those crossings cut the edge, the slivers
between them were dropped for being shorter than a stretch worth keeping, and `fromM` moved on past every
cut whether a stretch came of it or not — so the hole left behind was as wide as the whole cluster. Two
ends a weld apart are one place and the ring closes through them; two ends twenty centimetres apart are two
places, and a ring that should have closed came back as a run with two ends, hundreds of metres long.

**A cut within a weld of the one behind it is now passed over**, which makes a cluster one cut and bounds
every hole by the weld that closes it. The shortest stretch kept is the weld itself rather than half of it,
for the same reason read the other way: **a stretch shorter than the weld is its own two ends** — both weld
to one place, and a stretch that leaves a place by arriving at it can never be walked into a ring. The
laboratory map closed on the first of those and the city went from 158 open runs to 36.

## 2026-09-12 — the boundary is the merge of the ribbons the lines lay, and the walk that found it is gone

**A lane is an area, and the outside of the town is the outside of the union of those areas.** The boundary
used to be said in the lines themselves: every line walked at a station against every band near it, the
stretches that came back outermost paired off by how near their ends stood, the pairs strung into rings, and
whatever the pairing could not close shut with a straight. It closed, on a city, and what it cost to close
was a construction nobody could state in a sentence — a cut solved against a band's own boundary, a corner
squared over six rounds, a carry that dropped stretches and ran again, a swap that took a hand-over back off
whoever held it, and a fold closure over the top of all of it. Every gap named in
[docs/index.md](../../../docs/index.md#known-gaps) was one of those passes failing somewhere.

**Said as a merge instead, there is nothing to pair.** Each line lays the ribbon of ground it covers — its
two edges half a width out and the square end at either end (`ArcRibbon`) — every ribbon is cut where any
other ribbon's boundary crosses it, and a piece is kept where the ground a hair outside it is on no band at
all. A place is inside the town or it is not; nothing reasons about which line ought to hand over to which,
nothing is drawn across ground no car is driven along, and the rings fall out of the ends themselves.

**And nothing is bent.** An offset of an arc is an arc about the same centre, and a crossing of two of them
is the closed form two circles have — so every piece of the answer is a piece of some ribbon at its own
radius, cut at a place solved to a float. The walk it replaces sampled at half a metre and bisected, which
is why the old construction needed a rounding to call two stops one place.

**What it does not do yet is close on a city.** The laboratory map closes every ring; a city leaves 48 runs
of boundary with two ends, in car parks, where several bay ways lay bands within centimetres of one another
and two boundaries crossing at a fraction of a degree have no crossing a float can find. Those runs are
handed back as what they are (`LaneShell.Loose`) and drawn in the fault colour rather than shut with a
straight, because a merge that papers over its own faults is a merge nobody can read.

**Three figures carry the degeneracy and they are the whole of it** (`LaneShell.Merge`): how far outside a
piece the cover test is taken, how near two band edges stand to be one edge, and how near two cut ends stand
to be one end. **Two coincident edges are settled by the lower-numbered line** — dropping both leaves a hole
and keeping both leaves a crossing with two ways on — and **a piece is cut where another piece's own end
stands on it as well as where one crosses it**, which is the one cut a crossing cannot find: two square ends
laid along each other cross nowhere, and without that cut the overlap between them is weighed whole.

## 2026-09-12 — what still placed ground of its own is gone

The boundary was the town's, but four things still put ground down beside a road by arithmetic of their
own, and each of them would have outlived the upgrade by being nobody's to notice.

**A raster painter with its own verge, fillet and disc.** `GroundPainter` laid cells of pavement either
side of a chain, a wedge behind each kerb fillet and a disc at each mouth. Nothing had called it since the
plan stopped shipping a cell grid; one test comment named it. Deleted rather than kept for the day a raster
comes back — a second description of the pavement that nothing reads is still a second description.

**`Kerbs` answered for tarmac that no car is driven along.** It held a fillet per junction corner and a box
per slab beside its bands, so that `OffTheTarmacM` could answer for the whole of the paving. Only the probe
still asked, and the boundary itself had never counted either (a corner apron pushes the outside off the
lanes at every mouth; a slab buries the bays it is paved under). What is left is the bands — the one shape
the shell is the outside of — and the probe's independent reading of the kerb is now that same driven
ground, which is the reading it was always weighed against. The junction corners stay on the plan because
the crossings and the stop bars are placed off them (`Furniture.Corners`); they are a mouth's figure and no
longer a piece of ground.

**A bridge carried the town's pavement across its deck.** The deck's ground was grown to the road's own half
plus a walk, and the picture laid a pavement ribbon to match — the last line beside a road derived from
something other than the boundary. Both are gone and `TER-3b.1` says so. What it costs is a bridge with no
footway, which is the same gap as a boundary that cannot cross water, and it is named as one rather than
papered over with the arithmetic that was there.

**And nothing is struck off the kerb at all.** There is no line beside a road, no walking lane and no
concrete, and the blocks the town encloses are the grass they were laid over — not held back behind a
switch now but absent, there being no offset of the boundary to hold back. It is in
[docs/index.md](../../../docs/index.md#known-gaps) as the gap it is.

## 2026-09-09 — the outline belongs to the union, so one piece owns each station of it

`TER-3c.8` is the owner's, and the code did not meet it. Every piece offers the line half a walk outside
itself and the shell keeps the stations no piece stands *nearer* to — which settles every station except
the ones several pieces are the same distance from, and there it kept them all. There was a veto for two
cases somebody had hit (a movement running edge to edge with the arm it leaves, a band's end against the
arm across it) and none for the general one. A car park made of the movements that reach into it hits the
general one hard: four ways converge on one bay pose and end on the same straight, so six candidate lines
lie down one metre of kerb and the walk was laid off all six.

`Kerbs.Owns` breaks the tie by the lowest-numbered piece at the offset — a tie-break needs only to be the
same answer at every station of a run, not to mean anything. **Asked per station and not per span**: a span
that is duplicated over half its length is real over the other half, and dropped whole it leaves a hole in
the pavement — the fixture and the city both came apart that way before this was moved back.

**What it has not fixed**: a run whose ownership changes along it is cut where it changes, so the walk sees
two stretches where the ground has one line. The `Laid` map's mitres and its unbroken-line check are the
ones that say so.

## 2026-09-08 — a roundabout is a ring of ordinary junctions with no paint on it

Nothing about a roundabout is a new kind of thing (GEN-19): a node is opened out into a circle of nodes
joined by one-way arcs, and every rule the town already has answers for the result. The pay-off is the one
that matters — **circulating traffic is driven over what is entering, and nothing had to grant it that**.
Two ring arcs at a node are two pieces of one circle, so the movement between them is straight on and the
movement in off the arm is a turn; `TER-5e` ranks the straighter one over it and the priority a roundabout
exists for falls out of the ranking. The alternative shape, a polygon of straights, loses exactly that: at
three or four nodes every circulating movement is a turn of sixty to ninety degrees, and the ring gives way
to each arm in turn.

**The ring is one arc a node to a node, and making that work meant striking a corner on a curve.** A
junction's kerb fillets were solved between the two *lines* its arms' bearings make, which is right for
every arm that leaves straight and wrong by a metre and a half on a circle of twenty-seven metres — the
entries were filleted to points off their own tarmac, and the walking network round the ring came apart at
each of them. `Furniture.Kerb` now carries each kerb as the shape it is drawn along, a line or a circle,
and the corner and its fillet are the intersections of those: the arc tangent to both is centred where the
two of them offset by its radius meet, and it touches each at that kerb's nearest point to the centre. The
straight case is that construction and not a case beside it.

**The alternative was tried and given up.** A ring laid as straights at the nodes with an arc between them
keeps every junction's ground on a line, but the straights are chords of a circle whose entries need
twenty-odd metres of them apiece — so the ring is two thirds straight, ninety metres across, and reads as a
rounded square. A smooth circle needs a third of that width and is the shape somebody asked for.

**Nothing is painted on the ring, and that is what let it shrink.** A crossing and the bar behind it are
straight bands, so a ring that carried them had to be wide enough for the bundle to read square on its own
bend — about twenty-three metres of radius before anything else was asked. The entries carry both instead:
the zebra because that is where somebody crossing a roundabout crosses, and the bar because a bar is where a
driver holds when the junction refuses them and circulating traffic is never refused. What the ring is left
as is a circular road with entries and exits and no paint at all.

**What sizes it is the two roads leaving two of its nodes, and nothing else.** A ring's nodes are one
junction laid out as a circle rather than the accident GEN-16 is about, so they owe each other neither a
locality nor the road two separate junctions would — they owe each other what the arms leaving them do: the
ground one road takes (GEN-17) and a pavement's width on top of it, because two mouths whose paving abuts is
paving with nothing to wrap round. Held to the road instead, the circle came out nearly twice that wide for
no reason anything downstream could name. Odesa's widest went 54.6 m across → 45.8 → **32.4**.

**Four arms or it stays a junction.** Three arms opened out into a circle is a ring laid to sort out one
conflict the ranking already sorts out standing still, and it charges every car through the node a detour to
reach the arm opposite. Refusing them is free: the node keeps the junction it always was, which is what
`Roundabouts` does with every other condition it fails.

**What it costs is an island nobody walks onto**, which is what an island is: nothing stands on it, nobody
is put down on it and no trip ends there, so the walkable ground GEN-5 is about is still one piece.

**The turn a movement makes is measured off the arcs and the arms off their tangents.** Joining a node
recorded the *chord* a road left on, which for a circle is the polygon's interior angle rather than the
road's own bearing — so two pieces of one ring read as two arms lying together and GEN-13 refused them.
`TownLayout.Bearings` records the tangent now, which is what the carriageway is drawn on and what every
other reading of an arm already used.

**And two car parks are measured against the whole town rather than against their own road.** GEN-16 was
enforced by comparing each lot with the last one laid along the same kerb, which never saw a pair either
side of a junction; a laid city with roundabouts in it found one at twenty-nine metres. The pass asks every
lot now, on the same abeam test the gate does.

**The ring is not moved onto a driven half, because it is the whole of its own corridor.** A street the
scatter takes is one way of the two it was laid as and belongs on the half its traffic drives
(`RoadStage.OntoTheDrivenHalf`); a ring is laid one way round the circle its arms sized, and moved half a
lane off that circle its carriageway left its own nodes. What that cost was the pavement round the island:
the arms then ended on the ring's far kerb, exactly half a walk from the line the island's footway runs
down, so whether that pavement existed at each entry came down to the last bits of a float — three of
Odesa's four entries lost about two thirds of a metre of it, and the band was closed there with a round at
each end, two fans of concrete laid over the band that was already there. On the circle, an arm ends at the
ring's centreline like an arm at any other junction and the island's footway is one closed line.

## 2026-09-08 — one-way streets are scattered over the town rather than laid as a grid

A one-way grid put every one of them in the strict districts inside the orbital and none anywhere else, so
most of a town never met one and one district was nothing but. They are chosen over the whole layout now
(GEN-18), after the deletion passes rather than while the lattice is laid — which is also what lets the
choice be made against the town there actually is, so the settling opens far fewer of them again. What
decides the set is a spacing off the ones already taken and the rule that no junction carries two, and the
lattice no longer proposes a flow at all: every road is laid running both ways.

**Both ends have to fork, and neither may be on an arterial.** A street at a two-armed node dangles a lane
whatever else is true (GEN-18a), so taking one spends a place in the scatter on a street the settling would
open again. The arterials were the sharper find: a one-way street hung off one costs a district its second
way in, and a laid city with them had eight of the town tier's questions fail — claims left held behind a
car in a box, walkers sent over a carriageway, an ambulance that never arrived. Excluded, all eight came
back.

**What is left is two paving faults the old grid never reached** — a three-armed junction with no box, and
a corner turn setting off where no kerb ends, both on the laid city. Keeping one-way streets out of the
districts that wander hides the first and not the second, which is why neither is hidden: they are the
pavement's to answer for and not the arrangement's.

## 2026-09-07 — a movement through a node the road runs through is no piece of the outline

Where a two-arm node's arms meet as one line (TER-5b), every movement across it lies inside the two arms'
bands, so it can add nothing to the tarmac's outline — but offered, its wrap stood exactly on the arms'
own wherever the lane fills the road, and `Kerbs.Shell` kept both: a second run of pavement over the
first, with a round at each end and a kerb turned round each of those. `RoadCuts.RunsThrough` names such
nodes off their kerb corners, held to `Kerbs.JoinedM`, and `Kerbs` lays no piece for a movement across one.
The band answered is unchanged, being a union; what changed is that it is drawn once.

## 2026-09-07 — a graze is not a run

A wrapping line meeting another tangentially runs √(2·R·ε) past the crossing before it is that much
inside, which is what `Kerbs.OnePlaceM` bounds — the paving welded at `Kerbs.RoundingM` instead, so every
graze left a run whose two ends are one place: a walk-wide round of pavement struck off a few centimetres
of line, answered as ground. It welds at one place now, and Odesa's runs went 4322 → 2971 for a tenth of a
percent of pavement.

## 2026-09-07 — a walking lane that stops in mid-pavement is a corner nothing wrapped

Thirty dead ends over two towns, both causes a corner in the carriageway that is not really there — the
wrap was reading the tarmac correctly both times. `RoadStage.Rounded` gave up a too-tight vertex *while
laying the chain*, so the arc before it had already been aimed there and the straight after set off on a
bearing nothing arrived on: a fifth of a turn at five and a half metres out is three and a half metres of
pavement never laid. The vertex is dropped and the line laid again, and the deflection is read as the turn
rather than as its sine. And `JunctionTurnsACorner` asked how far out a kerb crossing stood and not which
side of the arms, so where a one-way street's kerb runs behind its own node (TER-4d) the crossing fell on
the far side — a lens of carriageway hanging 0.43 m off the kerb with no tarmac under it, which the
pavement then wrapped. The crossing must stand out along both arms. Three gates, each a defect in the
carriageway before it is one in the pavement, which is why none is a gate on the wrap.

## 2026-09-06 — the pavement is the tarmac grown by a walk, movements and squared ends included

The walk was laid off the tarmac and drawn off the roads, which are not the same shape: a movement swings
wider than either arm it runs between, so 109 sampled metres of Odesa's footway turned its junction corners
over open grass. Grass is walkable, so nothing refused it. The end of a band is turned the same way
(TER-3c.6) — a distance turns a corner, only a half-width squares one. And the inner-corner solver is gone
rather than fixed (`PavementCorners`, 460 lines): it was a second description of a shape that already had
one, laid off the roads and lots alone, drawing thin wedges of pavement into the verge. There is nothing
for it to round, because growing each piece by one figure and taking the union is growing the union
(TER-3c.3), so the shell has exactly the corners the tarmac has — an arc laid over the verge to hide a
pinch is pavement no walker can be given ground on. It cost 33 ms of Odesa's load and 60 km of walked
outline.

## 2026-09-06 — a junction has no shape, and the lines a car is driven on are laid with the town

`LaneLines` lays every lane and connector when a map is generated, and the graph reads those lines and adds
only the rules over them. The point is that the surface and the network can no longer be two answers: the
tarmac *is* the lines. So the disc is gone — what a box is on the ground is the band its own connectors
sweep, with kerb fillets rounding the wedges, and nothing has to recognise a junction to get it right; a
skewed, one-way or five-armed box comes out correct because each movement was drawn where a car goes. The
stored radius survives as planning only. Two things it had been hiding came back: a band must be read the
way a road's is, squared at both ends, or every point on a bend answers off perpendicular and the road's
centreline comes out as pavement; and a dead end is the one junction whose ground no movement sweeps
(TER-5a), so its head is a shape read off the arms rather than a junction record. The pavement round a box
is the arms' and not the movements' — a second band adds no ground, only another edge for a corner solver
to find.

## 2026-09-05 — the pavement is laid once, and the picture and the answer read that laying

`GroundMesh.Build` laid the pavement as draw calls and `GroundShapes` laid the same four pieces again as
coverage tests, with nothing but memory keeping them in step (TER-7) — and the failure it invites is quiet:
a band widened in the picture and not the answer is a walker refused ground it can see it is standing on.
`Paving.Lay` states the pieces once. The restructure is exactly behaviour-preserving and the frames come
back byte-identical, which is the only test that could have said so.

## 2026-09-05 — a town keeps only the one-way streets it can be driven round with

Drivable is asked of the movements and not the roads (GEN-18): a block whose streets all run inwards keeps
one connected component and is still somewhere a car drives into and never leaves. Reachable everywhere was
not enough either, because the fault is local (GEN-18a) — a one-way street at a two-armed node leaves a lane
nothing ever arrives on, so the town draws a lane, a stop bar and a line no car is on. A movement leaving a
node needs some road other than its own arriving there.

## 2026-09-05 — the lattice is ground, and two exams stand on it

The walking exam wanted the driving exam's map without its cars, and the only way to have it was to copy
four hundred lines of geometry into a second plan — a junction laid twice can pass one exam and fail the
other for a reason nobody can name. `ExamGround` is the ground and `ExamMap` writes one out as a
`CityPlan`; what is left in `ExamLattice` is the cars. A card names a place as an arm, a side and a
distance out and never as a point, so a card cannot drift from the map it is staged on.

## 2026-09-03 — a dead end's head holds the car's body, not the path of its middle

A head was sized `turning circle + the car's width`, but a turning circle is the radius the car's *middle*
sweeps and the furthest corner stands half a length and half a width off it — so the head was always about
two metres short, hidden only by a classifier of metre squares answering *drivable* half a cell past every
kerb. The figure is derived from the body: the circle, the width TER-5a asks to be left clear, and the
half-diagonal of the car.

## 2026-09-01 — a prop's kind is where it stands, and the ground decides it rather than a die

Every prop was a coin toss between *tree*, *scatter* and *furniture* — a taxonomy of what the pictures are
— so a litter bin stood in a field as often as an oak did and the town read as three-way noise everywhere,
which is the one thing a scatter must not be. The kinds are placements now (GEN-6b) — wild, planted,
furniture — read off the grounds within a verge of each candidate. A verge still carries wild looks half
the time, since a kerb planted only with what a town plants reads as a catalogue laid end to end. The size
band went with the kind: the great trees are authored at 2.6–3 m against a draw stopping at 2.2, so nine
looks were unreachable except through a fallback that does not resize. Thirty looks were deleted rather
than filed, each failing the same test — name what this is and say which of the three places it stands in;
eleven were park amenities and **this town has no park to put them in**. The verge is walked and no longer
swept, because deciding by probing a lattice square answered a question about a *line* with no bearing and
no distance from the kerb. A car park's edges are walked too, and its verge begins past the walk that wraps
it (GEN-4d) — laid half a metre out like a kerb's, every candidate was refused. The collar GEN-6a asks for
turned out to be the sweep's and not the verge's: a kerb walk is not blind. A prop's picture was bigger
than the prop (GEN-6d), drawn `diameterM` *tall*, so the flower planter was 3.45 m across against an
authored 1.9. Nothing overlaps any more, indexed by a grid of the widest prop's own width, since neither
pass can see where the other put anything. Odesa laid 108,939 props and lays 78,705.

## 2026-09-01 — an arm's paint is set back from that arm's own mouth, and the setback is a car length

The reach was half a carriageway plus a full corner radius — right for square arms and wrong for every
other, since two kerbs meeting at an angle cross well outside the mouth. The setback was carrying six
metres of slack for a skew nobody had measured, so a square junction's zebra sat two road widths off its
own kerb. The reach is solved corner by corner, and what is left of the setback is what the name says: a
stride, so the zebra's end bars stand on straight kerb. The two figures the fillet was borrowing are its
own now, since bounding a corner by the crossing's setback was the circularity.

## 2026-09-01 — a car park is three to six bays, because a run of frontage is an apron

Merging neighbouring slots laid sixteen bays of unbroken tarmac down one side of a street, and nothing ever
filled it — the town's whole roster is 520 cars over 319 lots. The merge was the right answer to the wrong
question: GEN-16 merges a junction because refusing one deletes every road at it, and nothing hangs off a
car park. GEN-4b bounds a lot at both ends, three to six. The bounds are what a lot *is* rather than a
tuning — the upper makes a car park a car park rather than a surface, the lower keeps it from being a
two-car lay-by that cost a lot's whole clearance. The clearance is measured between the rectangles and not
along the road, since on a bend an arc runs longer than the chord. Odesa's 319 lots hold 1377 bays where
they held about 3500.

## 2026-09-01 — the crossings were unpicked, because planarity was an argument and not a check

Odesa laid an orbital and a street across each other with no junction where they met — 20 m of shared
tarmac, and seven of sixty fixture seeds held at least one. The case for planarity was the arrangement and
it was sound; the coverage was not, since `Arterials.CrossesTheRing` had a single caller and `ExamGround.Hang`
never asked it. Adding the missing call was not the fix: a rule that holds only where somebody remembered
to invoke it is the same defect a year later. GEN-17 states the property over the town and
`TownLayout.UnpickTheCrossings` is one pass. It is a pass over the settled layout rather than a test inside
`Join`, because the lattice hangs its streets before `Arterials.Close`, so a test at offer time would have
deleted the orbital. Measured against what will be drawn, not against what was joined, using
`RoadStage.StraysM`. Odesa loses 6 roads of 316 and bought back five failing conformance cases.

## 2026-08-31 — two of a kind near enough to be one are merged, and merging beat refusing

The arterials, the lattice and the frontage are each laid at their own spacing and none knows what the
others left there, so near-coincidence is the ordinary case. Refusing the second of a pair costs the whole
piece that hung off it (GEN-8), which is how a town loses a block to an arithmetic coincidence. The nodes
are merged once the layout is joined and not welded as they are placed — welding took half the town, since
an arterial handed back a node twenty-five metres off its own line lays its next piece elsewhere and the
chain breaks. What is merged is offered again road by road in the order the town cares about them, or a
street severs the arterial it was hung off: bridges, arterials, streets. Odesa is 51 km of road against 42.

## 2026-08-31 — a lane is the width the town is laid in, and the straight stub is what stands on it

A road was three car widths across because somebody wrote three. A lane is 1.8 car widths, so every figure
quoted against a lane means the same thing on every map (GEN-15), and both road and pavement are ratios
rather than metres — a figure authored in metres beside them would be the one that stopped scaling.
Widening found the real defect: the stub was authored at four car lengths against the 20.4 m the junction's
ground, fillet, crossing and bar actually take, so the crossing hung onto the bend at the wider road. The
stub is derived from what is laid on it.

## 2026-08-31 — the bank is a curve now, and the water is set in a shore

A shoreline was twenty-four points over four kilometres — the bank has always been a sum of three sines and
what was rugged was the sampling. The count is derived from the wave: a chord stands off a curve by about
its own length squared over eight times the bend, so the step falls out of a tolerance of half a cell,
which is the finest the ground under the bank is classified. The water is set in a shore (GEN-2c) laid as
the same wave a shore's width wider, so no band has to be fitted to a curve afterwards. The map carries
four rings drawn largest first, so each fill leaves a line's width of the one under it showing — nothing is
offset by the renderer and nothing is classified twice. Each line is the colour of what it borders and
darker, so it reads as the shore's shadow; the ground under both is shore, because a line is a picture of
an edge. The shore is not grass, which is the whole of why nothing stands on it. It wears the pavement's
texture as a placeholder until there is a picture of a beach.

## 2026-08-31 — the map ends at its edge, and the shore is cut there rather than never drawn

The outline pushes a sea's far bank past the map on purpose, and the raster only took the cells that
existed — but the outline is what the mesh *draws* from, so it drew open sea over the void. The shape is
cut where it becomes a plan (GEN-2b); laying a shore that ends on the edge puts the map's rectangle inside
the meander arithmetic and gives a coast four corner cases, where clipping a ring against four half-planes
has none. `Test`'s river ran thirty metres off the top of its map and nobody had noticed. A prop is refused
with its radius rather than its centre, and the bar is asked of every map now.

## 2026-08-31 — a street that goes nowhere is deleted, and the fixture brief had to become a town

Odesa carried 21 junctions of one arm: dead ends in the sense TER-5a means without being dead ends in the
sense it promises, since the road stage lays the disc its arms need and a car found three metres of tarmac
with no room to turn. They are deleted with whatever hangs off them (GEN-5a), swept to a fixed point —
GEN-8's own answer applied one node at a time, and for the same reason: an arm grown on to close the loop
would have to cross whatever cut the street short. Turning heads were the alternative and are worse, since
a cul-de-sac is a thing a town plans. It costs a city a tenth of its road. It exposed that the property
suite's fixture brief was not a town — two of four seeds laid a layout with no cycle at all, one a pure
tree the sweep correctly deleted in its entirety.

## 2026-08-31 — a bridge is a road, and the wheel is turned so there is one to build

The generator skipped a node that fell in the river and joined whatever dry nodes were left, so a bridge
was however far apart the spacing had left them, and the hub sat in the water. A bridge is a class of road
(GEN-14a): water takes a `Bridge` and nothing else, so a street cannot cross, the orbital gives up its arc
over the span, and a span longer than the deck a town builds is a road not laid. The nodes make a crossing
short rather than a search afterwards (GEN-14b) — a node on each bank, with the stretch between closed to
everything else, because a node between two bridgeheads is a junction on the deck. The wheel is turned so a
spoke runs down the river's normal, since a bridgehead pushed off the ray leaves the sector that ray bounds
— the rotation was a draw anyway. The sea is not bridged at all. And the water question is asked of the
carriageway and not the centreline: Odesa lost about a sixth of its road length to lanes over the river.

## 2026-08-31 — a city is a seed and a brief, and every stage of laying one runs once

Cities arrived as baked `.town` files, so GEN-2 through GEN-8 bound whatever exported them and a city could
not be varied, replayed or repaired when a rule moved. Only hints are persisted and never geometry: the
moment a brief carries a node there are two answers to where the town is, and the one on disk goes stale.
Nothing retries, and four things make GEN-10 affordable, each replacing a search — the districts are convex
so planarity is arranged rather than checked; the arterials carry a node wherever a street meets one; a
slot claims its padding before anything fills it; and what is left over is deleted rather than joined up.
Two bounds came out of measuring the traced cities: their median sinuosity is 1.000, so straight is what a
street is, and bending is concentrated where they put it. A junction's arms must stand square enough to be
a junction (GEN-13), learned by laying towns without the rule — at a shallow angle the fillet on one arm
paves the crossing on the other. A building is sized by the roof it will wear, the footprints crossing the
seam as data because the plan may not read a catalogue above it. It deleted `TownWriter`, `--lay-maps` and
`--place-services`.

## 2026-08-31 — the ring is a rounded square, because what stands inside it is a rectangle

The start menu opens over this map and a panel is a rectangle, so a disc spends its ground on four corners
nothing reaches into — the widest rectangle inside one is 0.7 of its width. The corners keep it a road: a
square would be four right angles no car takes at speed. The cuts moved to the middle of the straights,
since a node on a bend takes a bite out of the one piece of the loop whose shape matters. The escort's pace
is read against the *tightest* corner, which is where the convoy comes apart if it is going to.

## 2026-08-31 — the ring carries an escort and one car, and the escort is held to its charge

Two convoys of three read as a staging rather than as traffic. Police tyres are worth nearly twice the
grip, so the escort cornered a third faster and left its charge inside a lap; the fix is a pace ceiling set
from what the *escorted* build affords on this radius. Building the escort on the armoured car's figures
and painting it white is a police car that corners like an APC — the paint and the physics coming apart is
what the dress-don't-build rule exists to prevent. The ceiling alone did not close the convoy up, because
the gap is the road a follower is granted plus the interval it leaves on top: a per-car share of the
following interval is what closes it. Cutting `Driving.FollowingHeadwayS` was not on the table — it is what
every car in every town keeps.

## 2026-08-30 — the menu is drawn over the ring, and GEN-1b now says which map that is

GEN-1b is about not building a *city* nobody asked for, which says nothing about a map costing a fraction
of one that was laid to be looked at. Standing a town up no longer means dropping the reader into it: the
ring is opened with the menu deliberately left up, since reopening the menu afterwards is the same state
reached by two moves with a flicker in between.

## 2026-08-30 — a map laid to be looked at, and the look rule it had to loosen

Every other map answers a question; this one is the picture the game idles on, so what shaped it is that it
never stops being worth watching and never needs anybody's attention. It stands at the left of the frame,
because the menu hangs from the gear in the top right. Its size is the window's and not the driving's — a
120 m radius made a picture of empty road for twenty seconds at a time, so the radius is whatever the
opening view holds, and what that costs is that the corner sets the speed. Nothing on it is staged, which
is why it is worth shipping: a loop of scripted cars would be an animation. Four roads, because a road ends
at a junction and two would join the same pair of nodes twice. The map dresses its own cars, and that cost
a rule — the service tier had been finding vehicles by their paint, which is an over-fit, since SRV-3
defines one as paint **and** a building.

## 2026-08-28 — the exam orders its walkers, because three cards were passing on an empty crossing

The three `StopsForThePaint` cards had never once been asked: the car and the body were never on the
crossing in the same second of any run, so the claim could only fail by coincidence. The map's walkers
wander and the spawn code said they paced — a body paces only on a map with no pavement, and `Exam` lays
pavement on every block. The harness orders them, pacing rather than timing one crossing, since a body
parked on the paint is a car that stops for it for good and a timed rendezvous is one the car's own slowing
then misses. It stops pacing inside its own step-out distance, or PER-15 is under test instead of the
crossing. The claim grew a second half: the lattice is a grid, so an arrival said the car got there and
never that it crossed the junction the card was written for.

## 2026-08-28 — the exam grew by eleven cards, and all eleven are unregulated boxes

A card is a cell, so asking for more crossings is asking for a bigger lattice — the roads, spurs, paint,
fleet and ground followed without a line of geometry moving, which is the arrangement paying for itself.
The eleven are boxes nothing governs: at a lit box the timetable decides, so the box worth staging over and
over is the one where the ranking alone decides (TER-5e). The eleventh narrowed a finding — two turns
across from arms *beside* one another clear each other where two *opposing* ones deadlock, so what stops
the opposing pair is not that they are the same rank.

## 2026-08-27 — a map laid from the questions asked of it, and the two it could not answer

Nothing measured what a car does where roads meet: the shipped cities have hundreds of junctions and not
one is staged. The cards are the map and the map is derived from them — `ExamCards` is a table written as
data and `ExamPlan` lays whatever they need, so nothing about the map is chosen twice. One make of car and
it is not the police car, since a card is read against another card and in this town a police look *is* a
police car (SRV-2, SRV-5). Paint on every arm, not only where a card is about paint, or every block's
pavement is a closed ring and the walking network is islands. Two things it cannot carry: there is no
inline junction on it — TER-5b promises a lit mid-block crossing and the engine refuses one twice over, so
that promise is a rule with nothing behind it; and the lattice stands half a cell off the whole metre, or
every kerb lands exactly on a cell boundary.

## 2026-08-27 — the map says what a building is for, and its people start behind its doors

A shuffle off the world seed knew which buildings existed and could put a town's only hospital on a
cul-de-sac with no bay within a block. The record carries a use (GEN-9) and the format went to version 3 —
the migration the old decision priced and declined, at one field and one pass. The placement moved to where
a map is authored, laid out by farthest-point: a second of work once, which is exactly the sweep refused
when the answer had to be produced on every load. And the map's people start behind its doors, having
already been stood at them — a trip ends inside a building, so beginning there closes the round rather than
adding a stage. The dwell is drawn per person, so the streets fill over ten seconds. What it costs is that
a question about a body on the pavement can no longer be asked at tick zero.

## 2026-08-17 — the town arrives as data, and the plan is the boundary

`CityPlan` is pure data laid as structure of arrays, and the world is built from that structure and never
from the file. The consequence is deliberate as a design and a real gap as a state of affairs: there is no
generator here, so GEN-2 through GEN-8 bind whatever laid the maps and nothing here checks them. A
validator would be shared by a generator's retry loop and the unit suite as a safety net and not a search
partner — the layouts are meant to satisfy the rules by construction.

## 2026-08-23 — the same lap twice, because the people are the variable

The proving ground's people stop cars by stepping into ground nobody has taken, so a driver there is never
asked to follow anything. `Drunk` is the same lap with the fifteen people put down *in* the carriageway,
and a body with nowhere to be that finds itself on a lane reels down it (`PER-16`) — which rule a walker
follows is the pose the map left it in, so the second map needed no name in any agent and no field in the
format. Three things came out of it. `E-4` is reachable and had never been reached before, the drunks being
the first thing that stands in a lane while a driver has somewhere to be; what the lap found was that the
entry did not work, and all four faults are the catalogue's. A drunk over the centreline is a lap nothing
gets round, since the oncoming lane is the only ground `E-4` may take — it keeps to its own lane for that
reason and not for its own safety. And a body walks at what is in front of it rather than along the road,
so a lurch at full stride cut the chord across a hairpin onto the grass; it is bounded by `sqrt(8·R·sag)`,
the corner formula doing the same job for a walker that it does for a car. What the lap costs is quoted
rather than asserted to zero — tuning until nothing was ever hit is tuning until the instrument can no
longer report what it was laid to find.

## 2026-08-22 — a map this build lays itself, and the writer that makes it a map

Every figure taken on Odesa is a figure about Odesa's corners, traffic and lights at once, so the proving
ground is authored here — a deliberate exception to "this project does not lay plans" rather than a crack
in it. It is one lap and not four circuits, because four circuits could say nothing about a fifth car or a
second drivetrain; the price is traffic, paid for by the holding being *named*, so a pass somebody was in
the way of is thrown away rather than averaged in. The lap closes on the shapes themselves, so the only
thing a link ever is is a straight and the last one is derived. A shape is a road, which is what makes a
measurement local. There is no light and no paint anywhere on it — a light tells a driver where to stop
before it has to look — so the whole of what the track asks is that a driver stops for what it can see. A
shape ends where somebody paces rather than beginning there, or every corner is taken from a standstill;
the beat between two paces is drawn afresh, or a settled lap meets the same walkers at the same point for
ever. A pacer waits for the traffic and never for a clock. Every figure is read off the shape's own slowest
point rather than off a standstill, so a pass nobody stepped out for is a measurement too. It is written
out as a file rather than kept as a plan in code, or it would be a second kind of map invisible to
`--shot`, to the menu and to every sweep.

## The last town laid is kept, and only the last

`Maps.Plan` laid a fresh town every time it was asked, so anything that asked twice paid twice: a review
sheet, a probe taking several readings, the visual tier staging a scenario a cell. A town is laid
deterministically from its own seed, so the second lay is the first town again and all it buys is the wait —
34 seconds of it on a city, and another 30 for the ground mesh that is a pure function of the plan under it.

**One town and never a table of them.** Kept by name, the first sweep over every shipped city would hold all
of them alive at once and a city is tens of megabytes of arrays. Kept as the last, it is never more than the
town whoever asked is about to use anyway — and the pattern that costs is repetition rather than revisiting.

**The figures are part of the name**, because a plan lays its pavement against the configuration it was
first asked with (`CityPlan.Paving`), so a town under other figures is a different town.

**And a plan is as far as it goes.** The walking graph over one is the largest single cost of standing a town
up — 33 seconds against a tenth of a second for the road graph beside it — and it is as pure a function of
the plan as the pavement is, but handing one graph to two towns is not safe: the index it answers
`NearestEdge` from carries a scratch of its own (`ChainIndex`), and two towns asking at once is two walks
over one working set. Tried, it broke a hundred cases of the suite. What that costs is the shot path's to
work around (`TownStanding`), and what would let it be shared is the scratch belonging to whoever asks.

