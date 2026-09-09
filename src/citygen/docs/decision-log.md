# CityGen — decision log

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

## 2026-09-08 — a box's outline never doubles back, and a jog at the node is not a side

Nearly every crossroads in a laid city had no box, and its arms' sections ran to the node and crossed one
another there. Two runs that carry on from one another overlap by up to a place at their weld, so the turn
between them (`Corner.To`) is a straight step from the one kerb's end *back* to the other's start — along
the kerb, not across it — and walked into the outline that stretch was laid out, back and out again, a hair
off the line each time: an outline that crossed itself, and no box. `Boxes.TakeBackTheSteps` takes such a
step out and cuts the kerb it doubles back along short by it; a step square across the kerb, which is two
kerbs a place apart joined by a jog, stays. And what survives of the line round an arm's end at a kinked
through road is a jog a hand wide at the node, which `Boxes.NearestEnd` read as the side's own run and cut
the arm at the node with its fillet nine metres out; only a run along the side counts now.

Three more things the walk round a box's corner turned up, once every box was one:

- **A walk that loses its way closes from where it got to**, not from where it started: everything it laid
  is kerb reached end to end from the arm, and the chord back to the arm's cut crossed the whole box.
- **Two ends nothing hands over across are bridged** (`Boxes.LooseEndBeside`): a fillet's line resuming a
  hand's width past where the arm's stopped, or two kerbs crossing at a kink, still stand end to end on the
  ground. The kerb is carried on until it is abreast of the next and stepped square across (`Boxes.Bridge`),
  since a chord between the two kerb ends cut the corner off the box; and two pieces that all but meet are
  welded (`Boxes.Weld`), since a kerb resuming a millimetre behind the line before it crossed that line by a
  hair, and a hair was no box. Odesa went from nine boxes walked round to all of them.
- **Two ends that stop where their lines cross are one point** (`Corner.WeldTheEnds`). Each line is cut a
  joining's width inside the other's band, which is up to root two of that apart at a right angle, and
  `Corner.OnePlace` held them to one joining and a rounding: a kink at which they stopped a hair over that had
  its two rounds cross square off the line between them, no turn was laid, both ends were capped and the wedge
  between the bands was nobody's. Welded, the round about the one place meets the next band exactly, where a
  round about one of two places left a sliver the width of their offset that nothing drew.

## 2026-09-07 — a line offered only where the kerb is open stands only where the kerb is open

`Piece.WalkedPast` said a movement's wrapping line counts only where the arms leave a gap, and nothing
implemented it: `Kerbs.Shell` kept every station no tarmac stood nearer to than the offset, and a movement
out of a lane that fills its road has an edge on the road's own kerb, so its line stood on the road's line
to the last bit of a float and both were kept — a second run of pavement over the first through every box
in the town, with a round at each end. Such a line now stands only where it is more than `Kerbs.JoinedM`
further off every piece that is the outside of the town than the offset asks (`Kerbs.OffTheOutsideM`),
which is the same figure that makes two pieces one line. The wraps that survive say which ends of which
arms reach the outline at all (`Paving.OpenEnds`).

## 2026-09-07 — the kerb line is one line the whole way round, and a box is what it encloses

The picture needed a junction's ground as one outline, and the corners the plan carries do not describe it:
a movement that swings wide of a fillet's arc is tarmac past the arc, and a kinked through road has its
movements a hair past its kerbs. The pavement already knows all of that — each run stops where the next
piece of tarmac takes over and hands over to the run that wraps it — so `Paving.Next` now carries which end
each end hands over to, by carrying on or by a turn (`Paving.TurnFrom`), and `Boxes` walks it from arm to
arm half a walk in on the road's side (`PavedBox`), cutting each arm where its further side gives way
(`Paving.EnterM`, `Paving.ExitM`, `PavedStub`). Four things the pairing had been hiding came out:

- **A hair against the way the round turns is nothing.** Two rounds about places a few centimetres apart
  cross a hair to one side of the kerb's bearing, and read the way the round turns that leg was nearly a
  full circle — the arm's kerb handing over to its fillet's at every other corner was closed with two rounds
  instead of a turn. `Corner.SweepRad` reads that much, and no more, as nothing.
- **Two places a centimetre and a rounding apart are one place** (`Corner.OnePlace`): a movement's run and
  the arm's it takes over from stop exactly that centimetre apart by construction (`Kerbs.Shell`), and two
  rounds about places that close cross wherever the rounding put them.
- **Two runs lying along one another hand over straight from kerb to kerb** (`Corner.AlongOneAnother`): the
  way round through their rounds' crossing is off to one side and was never a turn. Only where the kerbs
  stand no further apart than the places and a place more — further, and a straight step would leave the
  wedge between the bands open.
- **Every turn arrives where the next kerb starts**: one round about this place ends a place short of it,
  and a leg read as nothing ends a hair short, so the last of a turn is laid straight there.

And the line that turns round a band's end stands only where it is strictly outside every other piece, as
a movement's does (`Kerbs.Cut`): on the tie with the arm across it, a T's through arms had runs of pavement
turning round ends nothing was open at, and were drawn as ends that reach the outline.

## 2026-09-07 — an end a turn hands over is closed by the turn

Every corner where two runs give way — both mouth corners of every car park — carried two half-rounds, one
per end, and the kerb's turn over both: a rosette of three fans on one wedge. The turn's arc already rims
exactly that wedge (`Corner.To`), so the wedge is the sector of the round the arc turns about, read in to
the place, and `Corner.Stops` lays a round only for an end no run carries on from *and* no turn hands over.
What the answer measures (`GroundShapes.Paved`) did not move; `EveryMetreOfPavementNearAnEndIsClosedByARound`
now counts a turn's wedge as closing the band, as it always did in the picture.

## 2026-09-07 — a movement through a node the road runs through is no piece of the outline

Where a two-arm node's arms meet as one line (TER-5b), every movement across it lies inside the two arms'
bands, so it can add nothing to the tarmac's outline — but offered, its wrap stood exactly on the arms'
own wherever the lane fills the road, and `Kerbs.Shell` kept both: a second run of pavement over the
first, with a round at each end and a kerb turned round each of those. `RoadCuts.RunsThrough` names such
nodes off their kerb corners, held to `Kerbs.JoinedM`, and `Kerbs` lays no piece for a movement across one.
The band answered is unchanged, being a union; what changed is that it is drawn once.

## 2026-09-07 — a graze is not a run, and a seam is not an end

Both causes are the same mistake twice: treating a point where the band carries straight on as a place it
stops at. A wrapping line meeting another tangentially runs √(2·R·ε) past the crossing before it is that
much inside, which is what `Kerbs.OnePlaceM` bounds — the paving welded at `Kerbs.RoundingM` instead, so
every graze left a run whose two ends are one place, drawn as a circle standing in the band. It welds at
one place now. And a run's ends are mostly seams, since a line cut where it dives inside a neighbour
resumes where it comes back out; `Paving.Caps` is the ends that really stop, asked in both place and
bearing. Odesa's runs went 4322 → 2971 for a tenth of a percent of pavement, and each round is now half a
round rather than a circle. The band is still closed everywhere it stops (TER-7), since the half facing
back along the run is the run's own skirt.

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

## 2026-09-06 — the kerb line is carried round the corner by the rounds the band is closed with

Where the tarmac turns a concave corner the two runs each stop half a walk short of it, so the kerb line
came out missing an L of itself at every car park. The band never was, being closed with a half-round, so
what carries the kerb round is that round's own rim (`Paving.Corners`). Two rounds and not one arc through
both ends: the runs do not stop at one point, so an arc through both is as much as half a place outside the
band in the middle, and the pavement drawn would be wider than the pavement answered. Which kerb a turn
hands over to is judged by the way round it goes and not by the two bearings — two runs lying along one
another stand at one bearing, and by bearings alone the edge went the whole way round the place, drawing a
ring of kerb on the open pavement beside every crossing.

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
