# CityGen — decision log

## 2026-09-30 — a car park is a rank of bays laid off the kerb, joined to nothing

**The owner asked for car parks not to be junctions any more** (GEN-53; the parking slice's log has the
manoeuvre). A car park no longer parts its street: each bay is a short road of its own, laid square off the kerb
at the street's own lane, with a node a `ConnectionStandoffM` lead off each end (`TownLayout.Stand`).

- **Every bay owes the locality at both nodes** (GEN-16), the one over the street too: checking the far node
  alone let a bay's near node stand inside another junction's locality across the street.
- **A bay stands back from the kerb behind an apron** (`CityGenFigures.BaySetbackM`). Laid flush, the walk
  wrapped each rank's end as a notch the outset could not close — open runs on every shipped city
  (TER-3c.8). The apron is also what a car swings across rather than the street.
- **Its mouth runs back over the street's ground by half a ribbon touch** (`SimConfig.CarParkKerbOverlapM`),
  measured off the kerb actually under the mouth, so the rank and the street are one piece of tarmac on a
  street that bends a little and share no more ground than that on one that does not (TER-5c).
- **The kerb ends round into a car park rather than stopping at it** (`KerbEnds.RoundsIntoAPark`): a rank's
  mouth is no road end, and read as one it stood stray zebras and lights at car parks.
- **`BayTurnInParkingCircles` is retired with the turn it sized.** Seven tenths of the parking circle was the
  owner's call for a turn laid with the town; nothing is laid with the town now, and each car's manoeuvre is
  shaped on that car's own circle, which the owner asked for in as many words ("each car might do this
  differently depending on stats").

## 2026-09-30 — a hospital, a police station and a depot in every district

**The owner asked for every district to have its own services** (GEN-56). A town had a share of its buildings
as services, capped — Odesa six hospitals, four stations and two depots — spread over the whole town, and
nothing knew a district once the streets were laid. **The plan now carries the wheel the districts were laid
on** (`CityPlan.Districts`), and each district is cut a yard for each use inside it; the shares and their caps
are gone. **The yards are cut on top of the town's own count** rather than out of it: taken out of it, the
suite's town of fifty car parks gave eighteen to its six districts' services and a third of its parking with
them.

**Inside is asked of the building's ground and not of the road.** Asked of the road, two of the suite's town's
eighteen stood across a spoke in the next district — a district's edge is as often as not a spoke or the
orbital. Asked of the road and the building both, a small district ran out of sites for its third yard; a yard
on a boundary road facing into the district is that district's, so the building's two faces are what is
asked. **A district the town never reached stands nothing**: two of River's outer sectors hold three
junctions between them.

## 2026-09-29 — a car is stood in a bay again, and on a lane only where a town cut none

**The spawn stage stands the brief's cars in the town's own car parks** (GEN-7), spread over their bays as it
spread them over the lanes, now that a bay can be driven out of ([parking](../../world/parking/docs/decision-log.md)).
**A yard is not one of them** (GEN-55): its bays are its service's apron and are claimed before the plan's cars
are stood (GEN-4k), so a car stood in one was a car the town moved elsewhere or did not stand. **A town that cut
no car park keeps one car a lane** — the fixture, which asks for no buildings and so for no parking.

## 2026-09-28 — which junctions are lit is drawn again, on a stream of its own

**The roads stage lights the brief's share of the junctions that can carry lights** (`LitJunctions`, TLT-3),
on the signal stream it had before the lights were taken out (GEN-11) — so bringing them back moved no road,
building or prop of any town. Why the draw is exact and weighted is the
[trafficlight log](../../agents/trafficlight/docs/decision-log.md). **The plan carries no bars any more**
(`CityPlan.StopLines`): the town lays its own off its kerb ends, and the scenario map's second laying of the
same bars went with the field.

## 2026-09-28 — every district lays straight streets and wandering ones

**No street in the town was straight.** Every arm was jittered off its chord (`ConnectionJitterDeg`) and
the two ends of a link drawn apart, so every road was a biarc; half a strict district's streets took a
virtual node besides, a metre off the chord. Odesa was 83.9 % curved by length, and nearly all that was
straight was bays. **The owner asked for a grid's streets to be straight or mostly straight, about four
fifths of them — and then for every district to carry both kinds**, a grid laid all straight and a loose
district all wandering reading as two towns stitched together.

- **Laid straight is a fact about a road, not a kind of link** (`LayoutEdge.Straight`,
  `RoadArrays.LaidStraight`): the arms take the chord, the line wanders nowhere, and the plan carries it
  because the lanes draw the arms again. It borrows nothing from a bridge's settled bearing, being the
  owner's call that it stand alone; a bridge may take it up later.
- **A district lays a count of straight streets, not a chance of one** (`Lattice`, `District.StraightShare`):
  its share, drawn near its kind's (0.9 for a grid and 0.3 for a loose district, 0.08 either way, set by
  hand against the four fifths asked for), rounded and held off all and none. Drawn street by street, a
  district at 0.97 laid seventy straight and none that wandered. Which streets is each one's own keyed
  draw, ranked. A street that is not straight wanders like any other, wherever it runs.
- **A joined road is laid the way most of its length was.** Straight only where every piece was, a
  district's edge (a run of six or eight pieces) came out wandering however straight its district.
- **A corner a straight road was joined through is rounded rather than passed** (`Spline.RoundedInto`):
  straight legs, the corner at the class's own radius, tighter only where the legs are short, and refused
  under the junction's floor as before. A biarc through the corner's point bent the whole of both legs,
  and left grid streets 65.9 % straight on Odesa when they were all laid straight.
- **A yard now faces the town's middle** (GEN-55). Straight edge roads carry car parks, and furthest-first
  cut Odesa's sixth hospital yard into one at the map's west edge. The side was drawn, so its rank faced
  off the map and the hospital stood nowhere. The draw is gone with it.
- **Turning the jitter off everywhere was tried and not taken.** It moved the exam's own lanes, and it
  left one carriageway run and one walk run open on Odesa.
- **Snapping near-straight arcs onto lines was not an option.** The gentlest radius Odesa carried was
  9.3 km, and at a p90 of 1.8 km a hundred-metre piece bows 0.7 m. A line laid in its place moves the
  carriageway and breaks the bearing the arm was drawn on.

**What it came to**, by length of street on straight pieces: Odesa's grids 70–90 % a district and 80 % over
all of them, its loose districts 20–34 %; River's grids 74–94 % and 80 %, its loose districts 21–26 %.
River's smallest grid, eighteen streets, lays all of them straight: its one wandering street was joined
into a run laid straight. Curved length over the whole network: Odesa 84 → 35 %, River 85 → 47 %.

**Car parks are the town's gain.** A car park needs a straight stretch to be cut into, and Odesa cut
483 of the 600 it asked for where it had cut 217, and River 285 of 550 where it had cut 138. Odesa stands
2328 of its 2400 buildings where it stood them all, the ranks taking face the buildings had, and River 1689
of 1703. The town opens somewhat slower for its extra bays (Odesa 1.8 → 2.2 s). **The tick is not the
reason for any of this.** A car on an arc pays a few `SinCos` and an `Atan2` more than one on a straight,
all of it under a tenth of a profiled tick, and with every arm on its chord Odesa's cars phase read
408 → 397 µs, inside the probe's own spread.

## 2026-09-28 — the ground's layers are filled from rings shut across what a move left open

**A layer is a region, so it is filled from closed rings** (`GroundRings`): what the outset leaves open is
shut across its holes (`ArcRings.Shut`) and handed back beside the rings as the fault it still is. Filled
from the closed rings alone, one hole in the ring round the outside of a town took every square metre of
its carriageway with it — the ground read grass on every road, props were stood on the lanes and the world
refused the town. A town thirty kilometres long at Odesa's density left its ring round the outside open by
3.5 m, and Odesa's brief ten times over each way left 88 runs of its carriageway open. **River moves by it**: its one open run of walk is now walk, and a prop or two with
it. Odesa and the fixture have none and are the same to the bit.

**A place on the face asks the ranks near it which owns it** (`BuildingStage.Ranks`), in the list's own
order: asked of every rank, placing the buildings was the town's stations times its car parks, 129 s of a
town thirty kilometres by twenty-three.

## 2026-09-27 — car parks are cut from sites kept between cuts, and a cut changes the layout where it stands

**Cutting the car parks grew faster than the square of the town.** Every car park read every place on every
road again, asked each whether it stood a locality clear of every node, ranked each against every car park
already cut, and the cut then copied and filed again every road in the town. On Odesa's brief with its
extent scaled each way the stage took 0.2 s at one, 1.5 s at two, 47 s at four, and at ten had not
finished in ten minutes.

- **A book of sites for each size of car park** (`CarParks.SiteBook`): read once, then read again only on
  the roads a cut laid again, since a cut changes the line of the road it parts and of no other. Whether a
  site still stands a locality clear of every node is asked as it comes up.
- **Furthest-first is kept rather than sorted again.** A site's distance from the nearest car park only
  falls, so the one at the head is brought up to date as it comes up and is the furthest once it has not
  fallen.
- **The nodes and the car parks are filed by cell** (`PointCells` on the main level,
  `TownLayout.StandsClear`), and **a cut parts its road where the layout stands** (`TownLayout.Part`,
  `RoadLines.Refile`) rather than the layout being rebuilt round it.

**Two sites at the same distance are now offered by road and then by place along it.** The sort that
ranked them was unstable, so a tie came out in whatever order the runtime's introsort left it: the order
was .NET's and not this engine's. **Odesa has such a tie at its 188th car park and moves by it** — a few car
parks stand elsewhere, and seven props with them. River and the fixture are the same to the bit. The stage
is now 0.07 s on Odesa, 0.18 s at two and 1.5 s at four.

## 2026-09-27 — the exam is a scenario map of this engine, and going round a ring is straight on

**The owner ruled that the map is an end-to-end test of this engine and not an exam in anybody's rules of
the road**: the name was a way to fill it. So every card now says what the engine is to make of the
scenario and which of the engine's own rules that exercises (`ExamCard.Expects`, `ExamCard.Rules`), and
**every card is expected to pass** ([verification](../../../docs/verification.md#the-scenario-map)). The
tier stays outside `all`, as the frames do, because it is a whole town driven for a minute and a half.

- **Most of it is now the plainest cases**: a car alone through every movement of every shape, and cars
  whose ways never meet — two passing each other through a junction and on a road, a car driving past one
  or a line of cars standing in the oncoming lane, right turns on their own corners, two cars round opposite
  halves of a ring. None of them may be held at all.
- **A right of way is asked as the engine means it** (TER-5e: it orders who waits): the car it favours is
  never made to wait, and which of the two is over the shared ground first is not asked. Priority to the
  right, first-to-arrive and every other rule-book ordering went with the rule books; the cards that asked
  them ask now that everybody gets through and nobody touches.
- **What the engine refuses to do is not asked**: turning round in a box (TER-5f) or at a dead end
  (TER-5a), overtaking, and keeping a box or a junction clear of a queue past it. The dead end asks a car to
  drive out of one.
- **Somebody walking round a corner** was added — past the kerb ends of two zebras and over neither, which
  is where a pavement's ribbon comes nearest a lane's (TER-5c) — and **a road anybody walks is driven both
  ways**: a one-way road is one lane wide, so the far side of its zebra is its traffic's lane, and the first
  lattice left a walker standing there and a left turn behind it for good.
- **The lattice is eleven by eleven**, for 109 cards.

**Going round a roundabout was the weakest movement at every ring node.** A movement is classified by the
angle between the two lane ends, and a ring's lanes stop a standoff short of each node on both sides, so
read end to end the ring turned 60–75° towards the island: a turn across (p7), below every entry (p6) it
met, and a car on the ring gave way to a car coming onto it — against the road's own rule that circulating
traffic holds for nothing. **So the angle is taken against the arriving lane carried on along its own curve
across the node** (`LaneLines`): the ring comes out straight on and the entries and exits keep their
near-side turns; a straight lane carries on straight, so no other movement changes class. On six seeds of
Odesa, one minute each, against the same six before it: knocked down 3 → 3, wrecked 34 → 34, walks given up
448 → 447, km driven 2415 → 2407, touches 191 → 202.

## 2026-09-26 — the scenario lattice is laid like a town, and its pedestrians walk paint to paint

**Its roads are laid the way the generator lays them**, stand point to stand point on the arms the town's
own draw gives each end. Laid straight from node to node, as the parked exam's were, the lanes the plan is
read back into end where the lines do not go and the junctions lay no turn: the first lattice routed every
turn round the block. **It lights the junctions its cards are about and no other**, so a card about a light
is about the one it is staged at.

**Its pedestrians walk paint to paint and stand in the lane.** A walk from the pavement is not reliably
routed over a zebra (the known gaps), and a body crosses a carriageway in about a second at the town's
pace, so one sent kerb to kerb is either walked round the block or out of the car's lane before the car
arrives. Two stagings were tried and dropped: three walkers side by side walk into one another on the
zebra's one line, and one pacing back and forth steps back out at the far kerb as a car arrives too near to
stop — both cards about the walkers rather than the car.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- `GEN-2c`: the shore's chord tolerance is how far the drawn and answered bank stand off the wave, not half
  a cell of a classification.
- `GEN-6`: car parks are counted off the buildings a map plans (`SimConfig.CarParksFor`), not off
  `GEN-4b`'s relation.
- `GEN-16`: two car parks are kept apart by the locality every node owes (`TownLayout.StandsClear`), measured
  node to node, not as rectangles along a kerb.
- `GEN-19`: a building fronts a ring like any street; what is refused is a car park laid off one.

## 2026-09-19 — the verge is spaced by its own step, not by the props' girth

**The step the face was walked at was shorter than the props are wide**: a candidate every metre and a half,
against a girth of up to two metres and a bit plus a clearance, put a prop wherever one would fit — a hedge
down both sides of every street. **So the step is longer than the widest prop**
(`SimConfig.CityGen.PropVergePitchM`, `GEN-6b`) and it, rather than `GEN-6c`, spaces a verge; the clearance
still holds and now almost never bites. **A step and not a share**, because refusing a fraction of the
candidates cannot put a gap anywhere: three kept in a row are still girth to girth. The figure was read off
Odesa's verge count rather than worked out — the old pitch being girth-limited, doubling it did not halve
the props — and the verge now lays about half what it did.

## 2026-09-19 — a building fronts the boundary, not a road, and a service is stood on its parking

**The old stage cut frontage slots off every road**, so it had to know a road's half-width, its class, its
ends and its bends — none of which says where the concrete stops, and the lane layer that replaced the old
one carries none of the shapes it read. **So the face is walked instead** (`GEN-54`): a building fronts a
street, a block, a roundabout or a car park without knowing which, on the same line the props were moved
onto for the same reason (`GEN-6b`).

**The wall stands on the walk's own kerb rather than a setback behind it** (`SimConfig.BuildingLineM`): a
building line of the pavement plus a padding share left a strip of nobody's grass between every wall and
every walk. What is asked of the whole footprint rather than the wall is that no part of it is nearer the
carriageway than the face, because a deep building against one street is the one that reaches into the
pavement of the next.

**A service is placed by cutting its parking first** (`GEN-55`). The old placement laid every building,
swept for one near a car park and marked it a hospital — two passes over one question, and a hospital
wherever a lot happened to land. Cut before the town's own car parks, the yards are spread for free, the
sites being ranked by distance from every car park already cut.

**Only the flat of a car park's face is frontage.** Walked at a pitch, the whole of it was offered, and
buildings perched across the rounding at a rank's corner or down its sides. The frontage now has to stand
wholly on the flat, and **a service is slid to the middle of its rank**: the three civic roofs are 18 m wide
against a six-bay yard's 21.6 m, so a step of the pitch either way put one over the corner.

## 2026-09-19 — a kerb's end is read at the figure two places are one place at, and every handover is one

**The reading asked a built line for an exact offset.** A piece of the boundary is a road's kerb when it
stands half a lane off that lane's line, and that was asked at a weld (`LineTolerance.JoinedM`, a
centimetre) of a line that has been offset, merged, rounded and joined (`Spline.JoinedInto`) and drifts off
the half-width the longer the run: 1.8000 m off its lane down one Odesa street and 1.7887 m eighteen metres
on. **So one unbroken kerb read as its street for 17 m and as nothing for the 19 after.** Asked at
`LineTolerance.OnePlaceM` and settled on the nearest line that answers, a kerb reads as its street for its
whole length.

**A piece was read at its two ends and halved once between them**, which finds the first of several changes
and no more: one 100.31 m piece of Odesa's outline read road 1116, then no road, then road 1384, and yielded
one end 20 m up road 1116's kerb with road 1384's name on it. **Every piece is walked at a metre now**
(`KerbEnds.Along`), each step that changes hands is halved inside itself, and each change is named by what
stood either side of it.

**A road that takes the outline back from itself has not ended.** The rounding cuts the corner of a bend
inside a road (`TER-3c.10`), the outline leaves the kerb and picks it up a stride later, and both places read
as that road's end — 72 m out of its box on Odesa, where the zebra and the bar then stood. A pair naming one
road twice goes (`KerbEnds.StepsRound`); an honest cul-de-sac head is refused already, for standing at a box
that does not fork (`GEN-5a`).

**The census reads the answer back** — how far out of its box a town's kerb ends stand, at the middle and at
the furthest — because the furthest end of a road is the one its paint is laid off. Odesa's went 6.69 →
6.20 m at the middle and 75.79 → 15.52 m at the furthest; what is left there is a gore whose arms part at a
shallow angle, where one kerb really does leave the outline long before the other.

## 2026-09-19 — a verge is the walk's own outer face, and the props are laid along it

**The props were laid beside a road, and the road stopped being where the concrete ends.** Half a road plus
half a metre out was a verge while nothing laid a pavement, and the middle of the pavement once the walk was
struck off the boundary (`TER-7b`): benches and planters on the concrete down every street, which the ground
could not refuse because it then answered grass on the concrete. **So the pass walks the face itself**
(`GroundRings.WalkEdge`), and what that deleted was worth more than what it fixed: the stub a road's walk
stopped short of each junction at (the face wraps a junction, and a roundabout's island is lined by the same
walk), the choice of hand (a shell is walked with its ground on the right, `BandShell.Chains`), and leaning
on the ground to refuse an overhanging girth (the band is measured to the prop's own near rim).

**The sweep's stand-off moved to the boundary rather than to the face.** The face has its pockets closed —
a strip of grass narrower than two walks is concrete end to end and the face runs round the whole block —
so a wild prop in one measured thirteen metres from the paving and stood two from a carriageway. Odesa's
wild scatter went 82 643 → 77 783, and its verge 20 479 → 29 726: the concrete round every junction,
roundabout and block is kerb the old pass had no line for.

## 2026-09-19 — a reading of the kerb ends carries its own scan of the index

`KerbEnds.Of` asks the town's one `ChainIndex` once per piece of the boundary, and that index's convenience
overloads answer from a working set it owns. **Two readings of one paving on two threads were two walks over
one scratch**, and came back with ends at places the kerb never changed at, differently on each run. So the
reading takes a scan of its own (`ChainIndex.NewScan`): eight bytes a chain once per reading, on a path that
already allocates. The suite is where it showed — two classes reading one shared fixture — but a town laid
while another is being read would have got the same answer.

## 2026-09-18 — every line beside a road is rounded, and the boundary is rounded with them

**The lines were exact and looked it**: every fold in an offset came back as the corner its cut made — a
spike of concrete at a car park's mouth, a notch where two roads pass, a kerb turning a right angle at a
junction wedge. No kerbstone bends to a point. **So the rounding is struck with the line** (`ArcOutset`,
TER-3c.10) **at one radius for the whole town** (`RoadFigures.LineRoundedM`), because two layers rounded
differently are two answers about one corner and the concrete between them changes width along the bend;
the walking courses take it too, a lane being the middle of its pavement.

**The boundary is rounded too, which is a cut** — 0.41 of the radius off a right-angled corner the town turns
away at, and no construction avoids it. **What holds it is the figure being under half a lane**: a feature
narrower than twice the radius does not survive the roll, and at 1.4 m that is 2.8 m against a 3.6 m lane,
so no mouth a car is driven through is closed over. Past half a lane it starts eating the town, as the
probe's sweep shows.

**What it cost on Odesa**: every metre of 205.58 km of pavement lane still stands on its own course, the
turns refused for want of a line fell 538 → 535, and **the lanes that cross themselves rose 4 → 13 of
3 046** — the radius is past the inner course's own metre, so that course is cut at its corners. It is a
reading and not a gate, and the one to watch if the figure is ever raised.

## 2026-09-17 — the generator's three slowest questions, and the one that could not be moved

**Laying Odesa was 918 ms on one core, a third of the whole open.**

- **Where a junction could be cut** (`CutJunctions.Sites`) reads the layout and writes nothing, and asks a
  locality test of all 2 150 nodes for every place on every road. A road at a time on every thread, strung
  in road order (`InChunks`), took it 119 → about 80 ms.
- **`CarParks.Spread`'s sort asked its key twice per comparison.** Worked out once and carried in, 113 →
  13 ms — and the order is the same order, a comparison sort's permutation being a function of the count and
  of what the comparisons answered, both unchanged.
- **The cells a road's line runs through are filled into the caller's room rather than yielded**: every cut
  has `RoadLines.Reset` walk every road again, and the iterator was a fifth of that and a few hundred
  thousand allocations.

**The props could not be moved, because of the stream.** `PropStage`'s sweep draws a position, asks the
ground, and *only then* draws the kind and the girth, so how far the stream has advanced by cell *n*
depends on every ground answer before it. Drawing unconditionally is a different town: **it is a real
bound, and taking the draw and the test apart is a change to what the generator lays**, not to how fast.

## 2026-09-16 — what a plan lays on the first ask, it lays once for everybody

The suite stands several towns over one plan, and two of them asking its `Paving` met on a merge not laid
yet, both merges walking one `ChainIndex` scratch. **What came back was not a second copy but a wrong one**:
the city's boundary with 164 runs it could not close against 135 it did, and every shape struck off it
failing with it — nine tests over four classes, each of which passed alone. The products laid on the first
ask are behind one lock (`Paving`), build-time and uncontended after the first ask, so it costs a frame
nothing.

## 2026-09-14 — a cut reads its arms off the line

**The parting had to be exact.** A cut parts a street into two roads with a node between them and may
not move the street, so the pieces are the arcs the road was laid as, cut with `Spline.SubChainInto` and
never drawn again. **That inverts the layer's causality** — everywhere else an arm is drawn first and the
road is laid to it (`GEN-46`) — so `ConnectionPoints` grew a third source beside the bridge's and the
ring's, **read off the road**, and `CityPlan.RoadArrays.Cut` says which roads take it. **Both ends of a cut
road read it and not only the cut one**: a cut moves the far end an arm is drawn toward, and `RoadSplineTests`
caught the piece still drawing its old bearing on every road in the town. **The node stands a standoff of
one arc back from each parting**, so the lead is a piece of the road itself and `Spline.ArcThrough` reads it
back exactly; on the suite's city at four seeds the parted lanes come back within the rounding two
computations of one distance disagree by.

**Two things were tried and are not here.** Redrawing the halves through `RoadLines` moves the carriageway
by the wander and jitter the two new links draw, which is what the cut exists to avoid. Comparing the two
towns with `ChainIndex.Nearest` reported millimetres of drift between a town and itself, so the test walks
the parted lane and the lane it was at the same metre instead.

**A rank stands off the lane its bays are worked off, not the line the street was laid down.** A one-way
carriageway is moved onto its driven half (`RoadStage.OntoTheDrivenHalf`), so a rank measured off the layout's
line stood the far rank a lane and a half off its kerb and the near one a metre. It is measured off the lane,
read off `RoadStage.DrivenHalfM`, the one site that moves the carriageway (`CarParks.LaneTowardM`).

**The bay is five metres** (`CityGenFigures.BayLengthM`) against the longest vehicle the catalogue draws, the
recovery truck's 4.40 m. The plan is laid before a car exists, so `CarCatalogTests` and `SimConfigTests` gate
the pair rather than a derivation.

**The bound on the bend is the drift from the rank's line.** A rank is laid off the tangent at its middle, and
a street bending under it leaves the rank's line by the tangent offset — the square of the run. So the
departure is authored (`CarParkOffLaneMaxM`, a fifth of a metre) and the curvature is read off it. `--bench
parks` reports it.

**The plan carries the bays once.** A per-road `OneLine` flag beside "is it a bay" was two arrays that must
always agree, so the array is `RoadArrays.Bay` and `DrivenOverOneLine` reads it — a bay being the only ground
driven both ways over one line (`GEN-53`), the exception a ring arc already was
(`ConnectionPoints.Arm.OnTheLine`).

**How many a town has is counted off its buildings rather than authored beside them**: a car park count on
the brief drifts silently from the town it describes, so the brief carries the buildings and the engine one
car park per `CityGenFigures.BuildingsPerCarPark` of them (four, the owner's ratio). Asking for many more
showed the supply bound — a straight, long, empty stretch per car park: Odesa then asked 300 and laid 239,
River 275 and 134.

## 2026-09-14 — the nodes are settled before the first road is laid, and nothing is merged afterwards

**The merge was the last pass that laid the town twice**: `MergeTheLocalNodes` made one node of each cluster
(`GEN-16`) after every road was drawn and then offered every road again — Odesa merged 12 nodes of 257 and
redrew 351 roads. **It is one pass now: place, settle, lay** (`TownLayout.SettleTheNodes`), the arterials
laid first so a street offered against an arterial's ground is the one refused, and the ground bound
(`GEN-49`) holds from the first road.

**A cluster is not a pair.** Welding each new node onto the first one near it leaves the third of a chain
where it stood, and on the suite's city cost 24% of the network — 140 roads against 184. The merge's
union-find, run over the nodes before any road exists, closes the chain for none of the cost. **The node
placed first is the one that stays** — the hub, the bridgeheads and the arterials before any lattice point —
and a deck cannot move at all.

**What came out**: Odesa 163 junctions, 272 roads and 51.4 km against 160, 269 and 51.0; River
119/193/36.1 against 118/191/36.3; the fixture 28/36/7.4 against 26/34/7.7. The suite's own city moved
further, and the gate proving the contact path runs asks for 800 cars where it asked for 400 — under one car
a lane (`SpawnStage`), the same ask as every lane the town affords.

**Measured and left alone.** The run joined into one road (`GEN-51`) still reshapes the layout after it is
laid, taking 63 of Odesa's 220 junctions away, because what makes a node a place nothing meets is what was
deleted after it was placed; a roundabout (`GEN-19`) is opened on the town there actually is. Laying the
arterials with a node only where a street welds on was tried and cost the fixture 26% of its network: the
spacing is also the granularity at which a refusal over water is contained.

## 2026-09-14 — no town is carried as a file, so the format that carried one is gone

**The `.town` format outlived the last town written in it.** Every map is a brief or is laid in code, so
`TownReader`, `TownWriter`, `core/persistence/`'s byte cursor and tape, `--export` and `TownReaderTests` —
**787 lines whose only reader was each other** — went together, with the fields only the round trip kept
alive (`CityPlan.PavementCorners`, which `TER-3c.3` says the pavement does not have). The web head's fetch
of a town file went too: a city is its brief on the wire as on disk. **`qq town` was a second reader of the
format**, five hundred lines of Python kept in step by hand, so its readings moved into the engine
([`TownShape`](../../bench/TownShape.cs), `--bench shape`, `--bench joints`, `--bench shapes`) and the tool
only chooses the map. The one test that went with the format was a round trip reading the same floats
back; with no file, asking one plan twice is a derivation written out twice (VER-12).

## 2026-09-14 — a road is drawn as it is offered, and the round of refusals is gone

**The road stage laid the whole town, refused what clashed, repaired the layout and laid it all again**,
each round redrawing every street's wander, so one refusal re-shaped roads that could then be refused — a
search over wander draws dressed as a repair, and what `GEN-10` said the generator does not do. **The line a
link would be laid as now says whether it is a road** (`RoadLines`, [TownLayout.Join](../gen/TownLayout.cs)),
refused when offered for its class's floor, the world's edge, the water or ground another road holds
(`GEN-49`). That needs **a road's shape to be a function of its link** — the wander keyed on the two node
centres as the arm's jitter already was (`GEN-11`) — so deleting a road moves nothing that stayed.

**The stages that move a road ask before they move it**: a run is offered as the one road it would be and
cut in two where it cannot be laid, and a ring is drawn arm by arm before its node is opened out. **Two
joined roads are held to each other and not to each other's pieces**, since a run's smoother line can bow
metres off the pieces it replaces.

**What not searching costs is reported rather than hidden**: Odesa keeps 269 roads over 50.97 km against
262 over 51.2, and 12 of its 160 junctions are places nothing meets against 1 of 152 — eleven runs the join
could not lay as one road. One of its two roundabouts is a junction again, its arms undrawable where the
ring would leave them (`GEN-19`).

## 2026-09-13 — a junction is a place roads meet, and a run through nodes nothing meets at is one road

**A third of Odesa's junctions were places nothing met** — 75 of 223, River 60 of 163, the fixture 26 of 40:
arterial spacing nodes nothing welded onto and lattice points the prunes left with two arms, each a
standoff, a pair of movements, a claim and a routing node in the middle of a road. The old layer hid them
behind a fold; the rework deleted the fold without writing the replacement.

**The corner is joined rather than straightened.** The median deflection at a two-armed junction is 85°,
so a chord between the far ends would take the carriageway off the ground both pieces were laid on. The
node's place is handed to the joined road as somewhere it passes (`LayoutEdge.ThroughM`) and each end's
bearing is drawn toward the first place passed; those places are in the plan because the connection points
are drawn again off it, and a plan without them would draw a different town.

**A refusal halves the run and never costs it.** Laid as one road a run is one refusal, and the first
attempt lost 14% of Odesa's network and 40% of the fixture's that way. A refused joined road comes apart into
its pieces with exactly its middle place standing as a junction again; holding *every* place brought back a
line of junctions nothing met at. Odesa 49.6 → 51.2 km, River 35.4 → 36.4, the fixture 7.4 → 7.8. **A road
that comes apart runs both ways again** (`GEN-18`), which is what makes joining a one-way street safe.

**A road is asked whether it is still on the ground**, since the line bows hardest at a corner it was joined
through: two shipped maps had a carriageway a tenth of a metre over the edge and a generated one a street in
a river, before the question was asked of the line rather than of the nodes (`GEN-14`).

**One rule, not a rule with exceptions.** Of the first cut's five, the corner bound went — the corner was a
junction, so a joined road turns at a junction's floor (`GEN-47`, `GEN-48`) and a driver reads every arc
ahead of it; the one-way bound went, coming apart being what keeps `GEN-18`; the ring bound came straight
back, joining two arcs dismantling the circle `GEN-19` laid; and the already-joined bound went for good once
its boundary left open by 0.104 m against a 0.100 m weld proved to be the merge's hole (below). **What is left
is structure**: a bridgehead, a ring node, and a run that comes back where it set off. Odesa keeps one
two-armed junction of 152, a loop; River ten of 113 and the fixture seven of 21, all but three bridgeheads and
loops, and those three are refusals — `qq town --joints` lists each as `bridge`, `loop` or `refused`.

## 2026-09-13 — a ring the merge keeps as one stretch is still a ring

**A boundary made of exactly one stretch could never be closed**: the pairing at a place will not let a
stretch take itself up, so a ring that came back as one stretch was walked as a run with two ends, and
`Shut` refused it again for having fewer than two pieces. The town lays one: a movement whose radius
(1.85 m) barely exceeds its half-width (1.80 m) folds its inner edge into a 0.05 m hook, and what its
neighbours leave of that is one stretch 0.104 m long whose ends stand 0.087 m apart — inside the weld.
**So a run is shut when it comes back to where it set off, however few stretches it is made of.** The hole
had been read as a fact about the town: `GEN-51` carried an exception for a fortnight on it. **It is the cusp
and not the fold that is hard** — a ribbon turning tighter than its half-width inverts and merges cleanly,
and of −1.32, +0.39 and +0.05 m inside the fold on the suite's city only the last left a hole.

## 2026-09-13 — the lane layer is laid from the junction out, and everything that stood beside it is put down

**The order was the whole of the problem.** A road's curve was laid first and its lanes cut out of it, so
the bearing a car entered a junction on was whatever the chord left and a movement was a line drawn to fit
rather than one a car was shown to drive — `TER-5d`'s model read backwards. **The points are drawn first and
everything is laid to meet them** (`GEN-46`, `GEN-47`, `GEN-48`): a biarc meets both poses exactly by
construction, so the bearings are a fact about the road and nothing is cut back. A road bending tighter than
its class affords is straightened, then refused — which a chord test after the fact could not say, so the
rule stated as one was deleted rather than restated (`GEN-49`).

**Odesa lays 619 lanes and 1 209 movements over 332 roads in 557 ms** against 444, 1 033, 256 and
1 238 ms, and **its boundary closes all 115 rings** where it had left one open by 0.122 m over 30 390 m:
fewer, longer, smoother lines left the merge nothing to disagree about.

**A node's centre is the layout's, which nothing moves.** The forkless sweep's nudge would have been six
metres with the standoff — a bend inside another junction's locality (`GEN-16`), and connection points drawn
at derivation differing from those drawn at generation.

**The town it carried was put down rather than ported** — the parking, the buildings and the services on
them, the junction furniture, the signals, the kerb band, the paint and the walking network's passes — not
as unwanted but as not worth porting onto a layer being replaced that month. What kept the milestone
observable was the spawn stage standing the brief's cars on the town's own lanes, one a lane.

## 2026-09-13 — the boundary turns where the ground turns, and a cut is not a corner

**Three quarters of the points in the town's outline were places nothing happened.** Every crossing cuts a
ribbon, so two lanes of one carriageway, a bay's way and its neighbours all left joints where the boundary
went straight on: the suite's city came back in 7 301 pieces, 5 303 continuing the one before on the same
circle. **So a run is joined before it is handed over**, and a piece is somewhere the edge really turns or
changes radius — 7 301 → 2 008 and the fixture's 460 → 165, with the same 50 rings, the same 39 965 m and the
same stretches kept.

**A join is measured against the first piece laid on from where the second starts** — laid on from the first
piece's own start, the hair between two computations of the joint is carried the length of the join, and a
two-kilometre straight laid in halves read as a corner — **and by a walk rather than a radius**, since
subtracting two centres kilometres away answers in their last bits (68 false corners). **A ring has no first
piece**, so the last piece takes the first and may reach on. The lines are joined the same way before their
ribbons are laid (`ArcRibbon`, 31 638 → 26 680 pieces), and **a pair of pieces further apart than their two
reaches and a weld is not solved at all**, which took the city's merge 1.06 → 0.81 s with the same stretches.

**The suite's boundary check gained a millimetre.** Bays and lanes sharing an edge leave places strictly
inside neither band by a fraction of a millimetre, and a station asked at the middle of 165 pieces landed on
one five times where 460 never had. It is a tie, and the millimetre settles it as the merge's does.

## 2026-09-13 — a car park's bands are weighed as the run they are, and the boundary closes

**A car park lays a dozen bands down one lane**, a millimetre or two apart and tangent to a lane bending away
from them, and the outline through one came back in pieces: 18 open runs over 19 km on the laid city. Four
faults, each **a figure compared against two readings of one gap**:

- **The probe's own step was left in the reading.** The cover test stands a millimetre out, so a coincident
  edge read a millimetre and the window calling two edges one sat off centre; bands 1–3 mm apart were both
  kept. The step comes off the reading.
- **A cut is a place, and every boundary standing at it is cut there**: cuts are carried to every ribbon
  within a weld, so a bundle stops in the same places. Without it 127 runs stayed open.
- **The bands at a place are weighed over the place at once**, as a run ordered by where each edge stands —
  asked pair by pair, all three copies of one stretch were dropped — and where an edge stands is read off the
  way that band faces there, not off the distance.
- **Where two readings still straddle the figure, the pair is kept twice rather than dropped twice**: the band
  a line outranks has to clear the coincidence by the arithmetic's own error before it covers that edge. **A
  slit between two bands facing each other is nothing up to two centimetres**, a separate figure from the
  coincidence, asked only of a band lying along the edge and not of a square end cutting across it.

The suite's city went from 18 open runs and 19 km to none, and its stations without the driven ground on
their right from 10 to none. River closes every ring; Odesa kept one, a lens a third of a metre long in one
car park, handed back as what it is.

## 2026-09-12 — a band facing you is read off the line and not off the edge you are standing on

**A lane's end cap was coming back as three and a half metres of the town's outside** across a junction
mouth. The movement carrying on from a lane shares its square end, so the seam is inside the town — but the
cover test read which way a band faces off the nearest point of that band, and off a square end that is the
millimetre the test itself stepped, so lane and movement each saw the other as a band walked out of. **Off
its own end a band faces along its line**, out of whichever end it stopped at. The tie matters because the
town's two computations of one place differ by about a millimetre (`TER-5d`): measured at 2, 5, 10, 20 and
50 mm, every coincidence above 2 mm left more of the city's boundary open.

## 2026-09-12 — two cuts nearer than the weld are one cut, and the hole between them is gone

**Two boundaries that run along one another cross wherever a float's last bits say they do**: a movement
leaves a lane tangent at the same width, its edge within a tenth of a millimetre of the lane's for metres,
and the crossing solve answers with a cluster of points centimetres apart. **The fault was the merge's**:
each crossing cut the edge, the slivers between were dropped as too short, and the cursor moved past every
cut, leaving a hole as wide as the cluster — a ring that should close came back as a run hundreds of metres
long. **A cut within a weld of the one behind it is passed over**, and **a stretch shorter than the weld is
its own two ends** and is not kept. The laboratory map closed on the first, and the city went 158 → 36 open
runs.

## 2026-09-12 — the boundary is the merge of the ribbons the lines lay, and the walk that found it is gone

**A lane is an area, and the outside of the town is the outside of the union of those areas.** The boundary
used to be walked out of the lines — every line weighed at stations against every band near it, the
outermost stretches paired by their ends, strung into rings and shut with a straight where the pairing
failed — and what it cost to close was a construction nobody could state in a sentence, every known gap of
the day one of its passes failing. **Said as a merge, there is nothing to pair**: each line lays its ribbon
(`ArcRibbon`), every ribbon is cut where another's boundary crosses it, and a piece is kept where the ground a
hair outside it is on no band. **Nothing is bent**: an offset of an arc is an arc about the same centre and
two of them cross in closed form, where the walk sampled at half a metre and needed a rounding to call two
stops one place. What a merge cannot close it hands back as loose (`BandShell.Loose`), drawn in the fault
colour rather than shut with a straight.

**Three figures carry the degeneracy** (`BandShell.Merge`): how far outside a piece the cover test is taken,
how near two band edges stand to be one edge, and how near two cut ends stand to be one end. Two coincident
edges are settled by the lower-numbered line, and a piece is cut where another piece's own end stands on it
as well as where one crosses it — two square ends laid along each other cross nowhere.

## 2026-09-12 — the last town laid is kept, and only the last

`Maps.Plan` laid a fresh town every time it was asked, so a review sheet, a probe taking several readings or
the visual tier staging a scenario a cell paid 34 s a city each time, and 30 s more for the ground mesh.
**One town kept and never a table of them**: kept by name, the first sweep over every shipped city would hold
all of them alive at tens of megabytes each. **And a plan is as far as it goes**: the walking graph is the
largest cost of standing a town up — 33 s against a tenth of a second for the road graph — and as pure a
function of the plan, but its `NearestEdge` index carries a scratch of its own (`ChainIndex`), and handing
one graph to two towns broke a hundred cases of the suite. The shot path works round it (`TownStanding`).

## 2026-09-08 — a roundabout is a ring of ordinary junctions with no paint on it

**Nothing about a roundabout is a new kind of thing** (`GEN-19`), and the pay-off is that **circulating
traffic is driven over what is entering without anything granting it that**: the movement between two ring
arcs at a node goes straight on, and `TER-5e` ranks it over the turn in off the arm. A polygon of straights
loses exactly that, every circulating movement a turn of sixty to ninety degrees. A ring laid as straights at
the nodes with arcs between them needed twenty-odd metres of chord per entry — two thirds straight, ninety
metres across, a rounded square — so it is one arc node to node, and **a kerb corner is solved between the
shapes the kerbs are drawn along**, a line or a circle: solved between the lines of the arms' bearings it was
a metre and a half out on a 27 m circle, and the walk round the ring came apart at every entry.

**Nothing is painted on the ring, and that is what let it shrink**: straight bands read square on its bend
only at about 23 m of radius. **What sizes it is the two roads leaving two of its nodes** — the ground one
road takes and a pavement on top — and not the road or the locality two separate junctions owe; held to the
road, the circle came out nearly twice as wide. Odesa's widest went 54.6 → 45.8 → **32.4 m** across.

**Four arms or it stays a junction**: at three, a circle sorts out one conflict the ranking already settles
standing still and charges every car a detour to the arm opposite. **An arm is read off its tangent**
(`TownLayout.Bearings`): the chord a road left a ring node on is the polygon's interior angle, and made two
pieces of one ring read as two arms lying together (`GEN-13`).

**The ring is not moved onto a driven half**, being the whole of its own corridor. Moved half a lane off its
circle, its arms ended on the far kerb exactly half a walk from the island's footway line, and whether that
pavement existed at each entry came down to a float — three of Odesa's four entries lost about two thirds of
a metre of it.

## 2026-09-08 — one-way streets are scattered over the town rather than laid as a grid

A one-way grid put every one of them in the strict districts inside the orbital: most of a town never met
one and one district was nothing but. They are chosen over the whole layout (`GEN-18`) after the deletion
passes, against the town there actually is, by a spacing off the ones already taken and no two at a
junction; the lattice proposes no flow at all. **Both ends have to fork, and neither may be on an arterial**:
a street at a two-armed node dangles a lane (`GEN-50`), and one hung off an arterial costs a district its
second way in — a laid city with them failed eight of the town tier's questions, and all eight came back
once they were excluded.

## 2026-09-05 — the pavement is laid once, and the picture and the answer read that laying

`GroundMesh.Build` laid the pavement as draw calls and `GroundShapes` laid the same pieces again as coverage
tests, kept in step by memory (TER-7) — and a band widened in one and not the other is a walker refused
ground it can see it is standing on. `Paving.Lay` states the pieces once; the restructure was exactly
behaviour-preserving and the frames came back byte-identical.

## 2026-09-05 — a town keeps only the one-way streets it can be driven round with

Drivable is asked of the movements and not the roads (`GEN-18`): a block whose streets all run inwards keeps
one component and is still somewhere a car drives into and never leaves. Reachable everywhere is not enough
either, because the fault is local (`GEN-50`): a one-way street at a two-armed node leaves a lane nothing
arrives on, so a movement leaving a node needs some road other than its own arriving there.

## 2026-09-01 — a prop's kind is where it stands, and the ground decides it rather than a die

Every prop was a coin toss between *tree*, *scatter* and *furniture*, so a litter bin stood in a field as
often as an oak and the town read as three-way noise. **The kinds are placements** (`GEN-6b`) — wild,
planted, furniture — and a verge still carries wild looks, since a kerb planted only with what a town plants
reads as a catalogue. **The size band went with the kind**: the great trees are authored at 2.6–3 m against
a draw stopping at 2.2, so nine looks were reachable only through a fallback that does not resize. **Thirty
looks were deleted rather than filed**, each failing one test — name what this is and which of the three
places it stands in; eleven were park amenities, and this town has no park. **A prop's picture was bigger
than the prop** (`GEN-6d`): drawn `diameterM` tall, the flower planter was 3.45 m across against an authored
1.9. Props no longer overlap, indexed by a grid of the widest prop's own width because neither pass can see
where the other put anything. Odesa laid 108 939 props and then 78 705.

## 2026-09-01 — a car park is three to six bays, because a run of frontage is an apron

Merging neighbouring slots laid sixteen bays of unbroken tarmac down one side of a street, and nothing ever
filled it — the town's whole roster was 520 cars over 319 lots. `GEN-16` merges a junction because refusing
one deletes every road at it, and nothing hangs off a car park, so `GEN-4b` bounds a lot at both ends: the
upper keeps a car park from being a surface and the lower from being a two-car lay-by that cost a lot's whole
clearance. Odesa's 319 lots held 1 377 bays where they had held about 3 500.

## 2026-08-31 — two of a kind near enough to be one are merged, and merging beat refusing

The arterials, the lattice and the frontage are each laid at their own spacing, so near-coincidence is the
ordinary case, and **refusing the second of a pair costs the whole piece that hung off it** (`GEN-8`) — a town
losing a block to an arithmetic coincidence. So two junctions inside a locality are merged rather than one
refused, and Odesa went 42 → 51 km of road. When they are merged is the 2026-09-14 entry's.

## 2026-08-31 — a lane is the width the town is laid in

A road was three car widths across because somebody wrote three. A lane is 1.8 car widths
(`RoadFigures.LaneWidthInCarWidths`), so every figure quoted against a lane means the same thing on every map
(`GEN-15`), and road and pavement are ratios rather than metres — a figure authored in metres beside them
would be the one that stopped scaling.

## 2026-08-31 — the bank is a curve now, and the water is set in a shore

A shoreline was twenty-four points over four kilometres, though the bank has always been a sum of three
sines. **The count is derived from the wave**: a chord stands off a curve by about its own length squared
over eight times the bend, so the step falls out of a tolerance of half a cell, the finest the ground under
the bank is classified. **The water is set in a shore** (`GEN-2c`) laid as the same wave a shore's width
wider, so no band is fitted to a curve; the map carries four rings drawn largest first, each fill leaving a
line's width of the one under it, so nothing is offset by the renderer or classified twice. The shore is not
grass, which is the whole of why nothing stands on it; it wears the pavement's texture until there is a
picture of a beach.

## 2026-08-31 — the map ends at its edge, and the shore is cut there rather than never drawn

The outline pushes a sea's far bank past the map on purpose, and the mesh draws from the outline, so it drew
open sea over the void. **The shape is cut where it becomes a plan** (`GEN-2b`): a shore that ends on the
edge puts the map's rectangle inside the meander arithmetic and gives a coast four corner cases, where
clipping a ring against four half-planes has none. `Test`'s river ran thirty metres off its map and nobody
had noticed. A prop is refused with its radius rather than its centre.

## 2026-08-31 — a street that goes nowhere is deleted, and the fixture brief had to become a town

Odesa carried 21 junctions of one arm — dead ends in `TER-5a`'s sense without its promise, a car finding
three metres of tarmac with no room to turn. **They are deleted with whatever hangs off them** (`GEN-5a`),
swept to a fixed point: `GEN-8`'s own answer one node at a time, since an arm grown on to close the loop would
have to cross whatever cut the street short, and a turning head is a thing a town plans. It costs a city a
tenth of its road, and it exposed that the property suite's fixture brief was not a town — two of four seeds
laid no cycle at all, one a pure tree the sweep deleted entirely.

## 2026-08-31 — a bridge is a road, and the wheel is turned so there is one to build

A node in the river was skipped and whatever dry nodes remained were joined, so a bridge was however far
apart the spacing had left them and the hub sat in the water. **A bridge is a class of road** (`GEN-14a`):
water takes a `Bridge` and nothing else, the orbital gives up its arc over the span, and a span longer than
the deck a town builds is not laid. **The nodes make the crossing short** (`GEN-14b`) — a node on each bank,
the stretch between closed to everything else, since a node between two bridgeheads is a junction on the
deck — and **the wheel is turned so a spoke runs down the river's normal**, the rotation having been a draw
anyway. The sea is not bridged. The water question is asked of the carriageway and not the centreline:
Odesa lost about a sixth of its road length to lanes over the river.

## 2026-08-31 — a city is a seed and a brief, and every stage of laying one runs once

Cities arrived as baked `.town` files, so GEN-2 through GEN-8 bound whatever exported them and a city could
not be varied, replayed or repaired when a rule moved. **Only hints are persisted, never geometry**: the moment
a brief carries a node there are two answers to where the town is. **Nothing retries** (`GEN-10`), made
affordable by four things that each replace a search — convex districts, so planarity is arranged rather
than checked; arterials carrying a node wherever a street meets one; a slot claiming its padding before
anything fills it; and deleting what is left over rather than joining it up. The traced cities' median
sinuosity is 1.000, so straight is what a street is. **A junction's arms stand square enough to be a
junction** (`GEN-13`), learned by laying towns without the rule: at a shallow angle the fillet on one arm
paves the crossing on the other. A building is sized by the roof it will wear, the footprints crossing the
seam as data because the plan may not read a catalogue above it. It deleted `TownWriter`, `--lay-maps` and
`--place-services`.

## 2026-08-31 — the ring is a rounded square, because what stands inside it is a rectangle

The start menu opens over this map and a panel is a rectangle, so a disc spends its ground on corners
nothing reaches into — the widest rectangle inside one is 0.7 of its width. Rounded corners keep it a road,
where a square is four right angles no car takes at speed. The cuts are at the middle of the straights,
since a node on a bend takes a bite out of the one piece of the loop whose shape matters, and the escort's
pace is read against the *tightest* corner, where a convoy comes apart if it is going to.

## 2026-08-31 — the ring carries an escort and one car, and the escort is held to its charge

Two convoys of three read as a staging rather than as traffic. Police tyres are worth nearly twice the grip,
so the escort cornered a third faster and left its charge inside a lap: **a pace ceiling set from what the
escorted build affords on this radius**, rather than an escort built on the armoured car's figures and
painted white — a police car that corners like an APC is the paint and the physics coming apart. The gap
the three keep is the road each is granted and nothing on top of it (`S-2a`), so a slower convoy is a closer
one.

## 2026-08-30 — the menu is drawn over the ring, and GEN-1b now says which map that is

`GEN-1b` is about not building a *city* nobody asked for, which says nothing about a map costing a fraction of
one that was laid to be looked at. The ring is opened with the menu deliberately left up, since reopening the
menu afterwards reaches the same state by two moves with a flicker between.

## 2026-08-30 — a map laid to be looked at, and the look rule it had to loosen

The idle map is the picture the game idles on, so it is chosen for never stopping being worth watching and
never needing anybody's attention. Its size is the window's and not the driving's — at a 120 m radius it was
twenty seconds of empty road at a time — so the corner sets the speed. Nothing on it is staged, which is why
it is worth shipping. Four roads, because a road ends at a junction and two would join one pair of nodes
twice. **The map dresses its own cars, and that cost a rule**: the service tier had found vehicles by their
paint, an over-fit, since `SRV-3` defines one as paint **and** a building.

## 2026-08-27 — the map says what a building is for, and its people start behind its doors

A shuffle off the world seed could put a town's only hospital on a cul-de-sac with no bay within a block, so
**the record carries a use** (`GEN-9`) — one field and one pass. And **the map's people start behind its
doors** (`GEN-7`): a trip ends inside a building, so beginning there closes the round rather than adding a
stage, and the dwell is drawn per person so the streets fill over ten seconds. What it costs is that a
question about a body on the pavement cannot be asked at tick zero.
