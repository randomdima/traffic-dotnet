# CityGen — decision log

## 2026-09-14 — a car park is a junction cut into a road, and a cut reads its arms off the line

**The parting had to be exact, and that is what decided everything else.** A car park cut into a street
parts it into two roads with a node between them, and the one thing it may not do is move the street: a
carriageway that stepped sideways at the very place a reader is looking is worse than no car park. So the
two pieces are the arcs the road was already laid as, cut with `Spline.SubChainInto` and never drawn again.

**That inverts the layer's own causality, so the plan had to say where.** Everywhere else an arm is drawn
first — a bearing jittered off the chord to the far end — and the road is laid to satisfy it (`GEN-46`).
A cut has no such bearing to satisfy: the line is there and the node was put into it. `ConnectionPoints`
therefore grew a third source beside the bridge's and the ring's — **read off the road** — and
`CityPlan.RoadArrays.Cut` says which roads take it.

**Both ends of a cut road and not the cut one**, which was the first thing that did not work. An arm is
drawn toward the far end of its own road, and a cut *moves* that far end from the old junction to the new
node — so a piece still drawing its other bearing draws a different one than the line it kept was laid to,
and `RoadSplineTests` catches it on every road in the town.

**And the node is placed a standoff of the road's own arc back from each parting**, which is what makes the
reading exact rather than near. The lead an arm carries is one `ArcSeg`, so the cut is taken **inside one
arc with a standoff of it either side**: the arc from the node to the stand point is then a piece of the
road itself, and `Spline.ArcThrough` reads it back with no case for which end it is. Measured on the suite's
own city at four seeds, the parted lanes come back within the rounding two computations of one distance
disagree by — which is the tolerance, and not a tolerance chosen to pass.

**Two things were tried and are not here.** **Redrawing the halves through `RoadLines`** is what the layout
does for every other change of shape, and it moves the carriageway by the wander and the jitter the two new
links draw — the thing the cut exists to avoid. **Comparing the two towns with `ChainIndex.Nearest`** was
the first shape of the geometry test, and it reports millimetres of drift between a town and *itself*: the
projection over a chain of arcs is the coarser instrument by more than the figure being asked about. The
test walks the parted lane and the lane it was at the same metre instead.

**A one-way street is cut like any other, and the pieces are still one street.** Refusing one read as a
relation `GEN-18` forbids — two one-way roads at a junction — but the scatter chose *streets*, and a cut
parts a street rather than choosing a second one: the pieces meet at the car park and lie on one chord, so
they are neither two of them meeting nor two of them crowding, on the terms a ring's arcs are one
carriageway. `GEN-18` says so and `OneWays.Streets` is how the suite reads it. What the street's one
direction does cost is the bays': each is reached and left the one way there is, which is every way the
street has, so the reached column is read against the street rather than against two.

**And a rank stands off the lane its bays turn off, which is not the line its node stands on.** The lead was
half a lane and the turn — half a lane being where the arriving car is, on a street that carries one each
side of its centre. On a street driven one way that is true of one side and wrong by a whole lane on the
other: its one carriageway is moved onto the half its traffic drives (`RoadStage.OntoTheDrivenHalf`), so the
far rank stood a lane and a half off a kerb the near one stood a metre off — the asymmetry a picture of one
showed straight away. So the lead takes the lane it is measured from (`CarParkBayLeadM(mostBays,
laneTowardM)`), each side of the cut asks for its own, and `CutArm` carries the lead it was laid with rather
than the cut carrying one for all of them. **What reads which side the carriageway went is
`RoadStage.DrivenHalfM`**, the one site that moves it, so a road type the town lays later is a road type
this already follows. **The one thing that
is not square is the arms' own reading**: a one-way carriageway is moved onto its driven half after the
layout is settled (`RoadStage.OntoTheDrivenHalf`, `TER-4d`) while the node stays on the line the layout
laid, so the way out from the node to where the street stands is skewed by that half lane and the lead is
longer than the standoff by it — at a car park exactly as at every other junction that street has. The rank
is laid off the line and read back off the chord between the two stand points, which is free of a step that
is the same step at both ends.

**A bay is an arm, and its way is that arm's lane.** The first cut gave each side of a car park one ordinary
two-way arm and carried the bay count beside it as data, which made every car park the same 2×2 junction
whatever it was a car park of. It is one arm a bay instead: a rank of them a lane apart on a line a reach
past the road's edge, centred on the node.

**A rank is parallel, which meant an arm that does not leave from the node.** An arm had until then been a
bearing off the tangent and a distance along it, so a rank drawn that way fans out of one point and every
bay but the middle one of an odd rank stands askew to the street — which is not a bay. A car drives straight
into one and backs straight out of it, so every arm of a rank runs **square** to the road and what tells two
of them apart is where each one's foot stands along it (`CutArm.AsideM`). What joins such an arm to the
carriageway is then the movement the junction draws between two lane ends, exactly as for every other turn,
and the bearing the cut reads back is the one the arm's own line carries. **A rank is an apron and not a fan
of carriageways**, so its arms are not held square to each other (`GEN-13`): that rule measures the angle
between two arms and a rank's are all one bearing, so the lane between two feet is not a thing it can see,
and nothing is filleted, crossed or barred between two bays anyway.

**A bay joins the street and no other bay**, which is the one thing a car park's junction does that an
ordinary junction does not. The arithmetic that wires a junction pairs every lane arriving with every lane
leaving, and at a car park that is a turn out of every bay into every other — a fan of hairpins across
ground that is the car park's to cross rather than a road with a right of way on it. The pair is refused in
`LaneLines.Connectors`, beside the reversal `TER-5f` refuses, so the movement is never drawn rather than
drawn and priced out. **And a bay joins both ways of it**, which took the junction being made as long as its rank. With the bays
refused each other, a bay standing further along the street than the lane end a car arrives on is a bay that
car can only loop back into — a turn far tighter than the 2.4 m the junction corners at, so no movement at
all, and on the first cut only about four bays in ten had both ways. The fix is not to price that turn
differently but to move the arrival: **the street is parted the rank's own reach further back**
(`SimConfig.CarParkStandoffM`), so the whole rank stands inside the junction and every bay is ahead of both
arrivals. Odesa then reads `n/n both ways` on every car park it cuts. **The standoff is what the sites are
found with, so the bays are counted first and the place chosen for them** — a wide car park asks for a
longer, straighter, emptier stretch and is laid where one is, rather than being laid and then cut down
(`GEN-10`). What it costs is sites rather than bays: the stretch a wide one asks for is not everywhere, and
a town that asks for more car parks than its straights carry lays what fitted (`GEN-8`).

**And the turn is made where it belongs rather than shared out.** Every other movement in the town is an
equal-tangent biarc, which starts turning the moment a car enters the junction — at a car park that has a
car on its way to park drifting out of its lane across the mouths of every bay before its own, and a car
that has left one still curving a box later. So a movement at a bay is **one turn with a straight either
side of it** (`Spline.StraightArcStraightInto`). **The lane ends do not move**: both lines join the same two
poses, and the place the turn begins is arithmetic rather than a search — the corner the two lines make,
stood off by the tangent either side.

**And the turn is the car's own and not the room's.** The first pass solved the radius out of the poses,
which made it whatever the box happened to afford: a car turning into a bay off a two-lane street swung
round on nearly eight metres where the car can hold four, and looked from above like a lorry making a
delivery. Nothing about parking happens at a design speed — a car turning into a bay is at a walking pace
or stopped, and what decides its line is the hook it makes. So the radius is given to the construction
rather than solved for, and what is given is `CarParkTurnRadiusM`.

**Seven tenths of the car's own parking circle, which is inside its lock, and that is the owner's call.**
Laid at the full circle the hook still read as a sweep from above — the owner asked for a third off it
twice, having watched cars pivot from a standstill, and the second time after the alternative had been put:
that the rank and not the turn is two thirds of the box. `BayTurnInParkingCircles` is what carries it, so
the tightness is one authored share against the car's own figure rather than a length nobody can check. A
car tracking one of these lines has the wheel on its stop and runs a little wide of it, which is the
driver's to answer for.

**Once the turn was the car's, the junction was the turn's — at both ends of it.** A turn of a given radius
stands off the corner it turns by its tangent, so the street is parted the rank's own reach and one tangent
back (`CarParkStandoffM`) and the bay's way begins one tangent off the street (`CarParkBayLeadM`). Both had
been figures the geometry happened to leave lying about — the standoff a quarter turn the width of the
street, the bay's lead the standoff every road end in the town keeps. A six-a-side car park's box went from
thirty-four metres to twenty-seven, and the lead stopped being a coincidence: it had been within a
hand's breadth of the tangent a car actually needs, which is why the first cut of the sharp turn fell back
to the biarc on the near-side movements and nothing else.

**And an arm is a bay and its turn, with nothing left over at either end.** The lead had a floor under it —
the standoff every other road end in the town keeps — which left every movement laying a metre of the bay's
own line before the bay's lane began: the same ground, the same bearing, drawn twice, once as a junction and
once as a road. The floor is gone, so a bay's way begins where its turn ends. At the far end the arm had a
setback authored for it (ten metres past the kerb) and a bay was whatever that left over — eight and a half
metres of it, for a car that is four. Now the bay is the length and the arm is what it needs: **five metres**
(`CityGenFigures.BayLengthM`), against the longest vehicle the catalogue draws, which is the recovery
truck's 4.40 m and not the nominal car's 4.0. The plan is laid before a car exists, so what holds the two
together is a pair of gates rather than a derivation: `CarCatalogTests` fails the build for a variant longer
than `Car.LongestLengthM`, and `SimConfigTests` for a bay that does not clear it.

**And the bound on the bend is now the drift it was always about.** It had been an angle across the box,
retuned when bays lost a way in. But what a bending street really costs is that a movement holding the
street holds a *straight*, and a straight parts from the lane it is following by the tangent offset —
growing with the square of the run, and a car park's longest run is its whole box and its rank again. So
what is authored is that departure (`CarParkOffLaneMaxM`, a fifth of a metre, a ninth of what the car has
spare in its lane) and the curvature, the swing and both standoffs are read off it. It works out near a
degree across a wide car park's box, where the old figure said five. What reports it is `--bench parks`,
whose rows read `n/n both ways` and `m/m one turn`.

**And the plan carries the bays once rather than twice.** The flag per road was `OneLine`, read for the lane
offset; this rule wanted "is it a bay", and two arrays that must always agree are two answers. So the array
is `RoadArrays.Bay` and `DrivenOverOneLine` is a reading of it — a bay's way being the only ground in this
town driven both ways over one line.

**And a bay's lane is driven both ways over one line** (`GEN-4f`), which is the first road in this engine
that is: a car goes in over the same ground it comes back out over, so the arm carries one lane's width and
its two connection points stand on the line rather than half a lane either side of it. That is the same
exception a ring arc already was, so it is the same flag read two ways
(`ConnectionPoints.Arm.OnTheLine`) rather than a second one — and it is what wires every bay to every way in
and every way out of the junction without anything saying so.

**And how many a town has is counted off the buildings it plans rather than authored beside them.** A car
park count on the brief is a statement about a town's traffic written where nothing holds it to the town: a
map that grows a district keeps the parking it had, and the two numbers drift silently. So the brief carries
what a person would say about the place — how many buildings it is — and the engine carries how many of them
one car park stands the cars of (`CityGenFigures.BuildingsPerCarPark`, four, the owner's ratio). Nothing
stands a building yet, so that count is all the field does today; the stage that plants them, when it lands,
plants the same number. **Asking for many more than the old figure is what showed the supply bound**: what
the ground affords is a straight, long, empty enough stretch per car park, and a town asks for more of those
than it has.

**What it came out as**: Odesa asks for `300` car parks off its `1200` buildings and cuts `239`, with
`1748` bays and one arm each, `18` of them on a street that runs one way; River asks `275` off `1100` and
cuts `134`, its straights being shorter between the bends of a river city. Both are `GEN-8`'s "what fitted", reported by `--bench parks` against
what was asked — junctions of five to fourteen arms, spread by taking each time the site furthest from every
car park already cut. No spacing figure of its own; the locality every pair of junctions owes does the rest.
**The bays
themselves are not laid**, so each arm ends at a node nothing leaves, which is
[a known gap](../../../docs/index.md#known-gaps) and why no town the suite asks its ordinary questions of
carries one.

## 2026-09-14 — the nodes are settled before the first road is laid, and nothing is merged afterwards

**The merge was the last pass that laid the town twice.** Every node the generator places went down, every
road was drawn, and then `MergeTheLocalNodes` made one node of each cluster standing inside a locality of
itself (`GEN-16`) and **offered every road in the town again** — which is also where the ground bound
(`GEN-49`) began, two junctions a stride apart being a pair about to become one. Odesa merged `12` nodes of
`257` and redrew `351` roads to do it.

**It is one pass now: place, settle, lay.** The arterials place their nodes, the lattice places its own and
the ones its stubs meet arterials at, `TownLayout.SettleTheNodes` makes the clusters one node, and only then
is a line drawn — the arterials first, so a street offered against ground an arterial holds is the one
refused. Nothing is merged afterwards and no road is drawn twice; the bound holds from the first road.

**Two things had to be true, and the first one was not obvious.** **A cluster is not a pair**: three nodes a
stride apart in a chain are one place, and welding each new node onto the first one near it — the obvious
way to do this at placement — leaves the third where it stood. Measured on the suite's own city, that cost
`24%` of the network: `140` roads against `184`, with a piece of `30` nodes and another of `9` severed from
the town and deleted as stranded (`GEN-8`). The union-find the merge used is what closes the chain, and
running it over the nodes before any road exists is the same answer for none of the cost.

**And the node placed first is the node that stays**, which is the precedence the merge used to weigh: the
hub, the bridgeheads and the arterials are placed before any lattice point, so a street standing too near an
arterial's junction is the one that moves onto it. A deck cannot move at all.

**What the towns came out as**: Odesa `163` junctions over `272` roads and `51.4` km against `160`, `269`
and `51.0`; River `119`/`193`/`36.1` against `118`/`191`/`36.3`; the fixture `28`/`36`/`7.4` against
`26`/`34`/`7.7`. **The suite's own city moved further than a shipped one** — every town a seed makes is a
different town when the arrangement changes — and the gate that proves the contact path runs at all had to
be told: it asks for `800` cars where it asked for `400`, which under one car a lane (`SpawnStage`) is the
same ask as *every lane the town affords*.

**What was measured and left alone.** Two passes still reshape the layout after it is laid, and neither is
the merge's kind of redoing. **The run joined into one road** (`GEN-51`, `ThroughRoads`) takes `63` of
Odesa's `220` junctions away, and what makes one of them a place nothing meets is *what was deleted after it
was placed* — the water, the edge of the world, a refusal, a prune — which is not known when it is placed.
**A roundabout** (`GEN-19`) is opened out of a junction whose arms are counted on the town there actually is.
Laying the arterials with a node only where a street welds onto one was tried, to stop the first at its
source: it costs the fixture `26%` of its network, because the spacing is also the granularity at which a
refusal is contained — one long piece refused over water takes the whole stretch with it where four short
ones lose one.

## 2026-09-14 — no town is carried as a file, so the format that carried one is gone

**The `.town` format outlived the last town written in it.** Every map is a brief or is laid in code, so
nothing read a `.town` any more: the writer was reachable from `--export` alone and the reader from the
round trip that checked it against the writer. `TownReader`, `TownWriter`, `core/persistence/`'s byte
cursor and tape, `--export` and `TownReaderTests` are deleted together — **787 lines whose only reader was
each other**, and with them the fields nothing but the round trip kept alive (`CityPlan.PavementCorners`,
which `TER-3c.3` says the pavement does not have).

**What the page fetched for a town goes with it.** The web head carried a name, an empty file and a fetch
for "a town still carried as a file", and the build gzipped every plan into the published folder. A city is
its brief on the wire as it is on disk, so the manifest is the briefs and the one wait before a map opens
is the art.

**`qq town` was the second reader of that format** — five hundred lines of Python unpacking the same
records, kept in step with the first by hand — so the readings moved into the engine
([`TownShape`](../../bench/TownShape.cs), `--bench shape`, `--bench joints`, `--bench shapes`) and the tool
is what asks for them. It is the same arrangement the census already had: the town is read by the code that
laid it, and what a shell tool does is choose the map.

**And the one test that went with the format was a round trip and not a property.** What it guarded is that
a connection point is a function of the seed and the link, which the town read back proved by being a
different set of arrays holding the same floats; with no file there is no second set of arrays, and asking
the same plan twice is a derivation written out twice (VER-12). What is left of `ConnectionPointTests` is
where the points stand and what they are drawn from.

## 2026-09-14 — a road is drawn as it is offered, and the round of refusals is gone

**The road stage laid the whole town, refused what clashed, repaired the layout behind the refusal and laid
the whole town again.** Each round drew every street's wander from a stream of its own, so one refused link
re-shaped every road in the town, which could refuse others: the loop was a search over wander draws dressed
as a repair, and it was what `GEN-10` said the generator does not do.

**The line a link would be laid as is now what says whether it is a road** (`RoadLines`,
[TownLayout.Join](../gen/TownLayout.cs)). A link is drawn when it is offered and refused there — the floor its
class affords, the world's own edge, the water, and the ground a road already standing holds (`GEN-49`) — so
nothing is laid and taken back and nothing is drawn twice. Two things had to be true for that to work, and
each is worth more than the loop was:

- **A road's shape is a function of its link**, the wander keyed on the two node centres exactly as the arm's
  jitter already was (`GEN-11`). Deleting a road now moves nothing that stayed.
- **The ground bound begins at the locality merge and not before** (`GEN-16`). Offered earlier it deleted four
  arterials whose two junctions were a stride apart and about to become one, and `KeepTheLargestComponent`
  then took half the town with them — `125` roads and `72` junctions against `269` and `160`.

**The stages that move a road ask before they move it.** A run is offered as the one road it would be before
its pieces are given up, and cut in two where that road cannot be laid; a ring is drawn arm by arm before the
node is opened out, and the node stays a junction where one of them cannot be laid. **Two joined roads are
held to each other and not to each other's pieces**: a run's line is smoother than the pieces it replaces and
can bow metres off their path, so a pair that each cleared the other's pieces could still be laid into one
another.

**What the town pays for not searching is reported rather than hidden.** Odesa keeps `269` roads over `50.97`
km against `262` over `51.2`, and `12` of its `160` junctions are places nothing meets against `1` of `152` —
eleven runs the join could not lay as one road, each cut once. One of its two roundabouts is a junction again,
its arms being undrawable where the ring would leave them, which is what `GEN-19` asks for in as many words.

## 2026-09-13 — a junction is a place roads meet, and a run through nodes nothing meets at is one road

**A third of Odesa's junctions were places nothing met.** The arterials carry a node every 220 m along the
orbital and every 200 m along a spoke so a lattice street has somewhere to weld onto, and a node nothing
welded to — or one the prunes left holding two of its four arms — stayed a junction: a standoff both lanes
ended at, a pair of movements across it, a claim on the ground those took and a place the router planned
through, all laid in the middle of a road. `75` of `223` on Odesa, `60` of `163` on River, `26` of `40` on
the fixture. The old layer hid it behind a fold that rewrote a forkless run as one lane; the rework deleted
the fold and named the replacement, and the replacement had not been written.

**The corner is joined rather than straightened, so nothing moves.** The obvious join — replace the two arms
with the chord between their far ends — would have been wrong on more than half of them: the median
deflection at a two-armed junction is `85°`, not nought, so these are corners and not cuts, and a chord
would have taken the carriageway off the ground both pieces were laid on and through whatever the corner was
drawn round. The node's place is handed to the joined road as somewhere it *passes*
(`LayoutEdge.ThroughM`), and the bearing the road leaves each junction on is drawn toward the first place it
passes rather than toward the far end it never points at.

**Which is why the places a road passes are in the plan and the format carries them** (version `7`). The
connection points live nowhere and are drawn again off the plan wherever they are wanted, so a plan that did
not carry them would draw one town when it was laid and another when it was read back. They are what a road
*is*, not a record of what it was.

**A refusal halves the run and never costs it.** Laid as one road, a run is one refusal — and the first
attempt lost `14%` of Odesa's network and `40%` of the fixture's that way, a joined road taking five roads'
worth of length with it. A refused road that was joined out of several now **comes apart into the pieces it
was made of**, and exactly one place — the middle of what it passed — stands as a junction again and is
never offered back. Holding *every* place instead was the first answer and it was too literal: what the
stage refused was the road, not each corner in it, so a road that needed cutting in two came back as a line
of junctions nothing met at. Halving still ends the sequence, and faster: each refusal halves what is left,
and a road that passes nowhere is refused by being deleted. The network came back whole — Odesa
`49.6 → 51.2 km`, River `35.4 → 36.4`, the fixture `7.4 → 7.8`.

**A road that comes apart runs both ways again** (`GEN-18`). The scatter takes whole streets one way, and a
street taken one way and then broken into four is four one-way streets meeting each other, which is the one
thing the scatter is a scatter to avoid. **That is what makes joining a one-way street safe** rather than
refusing to join one: the rule the scatter settled is held by what happens when a road comes apart, not by
never letting one be joined.

**And a road is asked whether it is still on the ground.** A node stands a margin in from the edge and never
on the water, which is what makes the *places* a road joins fit; the line is free to bow off its chord and
bows hardest where it turns a corner it was joined through. Two shipped maps had a carriageway a tenth of a
metre over the edge, and a generated one had a street in a river it was no bridge over, before the question
was asked of the line rather than of the nodes (`GEN-14`).

**It is one rule and not a rule with a list of exceptions.** The first cut carried five: a corner too tight
for the road's class, a street already taken one way, a ring arc, a run whose ends a road already joined,
and a corner the road stage had refused. Four of them were conveniences and went.

- **The corner bound went**, because the corner *was* a junction: a joined road turns at a junction's own
  floor (`GEN-47`, `GEN-48`) and nothing is measured and refused before it is laid. That is safe because a
  driver reads every arc of the line ahead of it and is down to that arc's cornering speed before it
  arrives — the same reading it always made, on a line that is now the road's instead of a movement's.
- **The one-way bound went.** What keeps `GEN-18` true is not refusing to join, it is that a joined road
  which later comes apart runs both ways again.
- **The ring bound went and came straight back**, and that is the useful half of the experiment: joining
  two arcs of a roundabout dismantles the one junction `GEN-19` laid as a circle. A ring node and a
  bridgehead are not places the arithmetic stopped a line; they are places the carriageway itself changes.
- **The already-joined bound went, came back with a measurement, and then went for good.** Two roads between
  one pair of junctions left the fixture's boundary **open by 0.104 m** against a weld of 0.100 m, so the
  bound looked load-bearing. It was not: the hole was the merge's (below), and a merge that cannot close
  round a shape the town lays is the merge's to answer for rather than a reason to lay the town differently.

**What is left is two structures and not a taste.** A bridgehead and a ring node, where the carriageway
changes; and the one shape a road cannot be — a run that comes back where it set off. **Odesa keeps one
two-armed junction out of 152**, and it is a loop. River keeps ten of 113 and the fixture seven of 21, all
but three of them bridgeheads and loops.

**The three that are neither are refusals and are named as such.** A joined road that bows off the map, or
onto ground another road has (`GEN-49`), is cut back at the place it was refused over — so the reading `qq
town --joints` gives is `bridge`, `loop` or `refused`, and a junction in the third group is the town failing
to lay a road rather than the rule making an allowance.

**And the fixture stopped crowding.** Sixty cars on a town with a third fewer places to stop at touch each
other not once over five minutes, where they used to inside thirty seconds — so the gate that exists to
prove the contact path runs at all now stands its own town up with 240 (`AllocationGateTests`). The town
got better and the instrument had to be told.

**The instruments report the rest**: `qq town` prints how many arms each junction has, how far off half a
turn a two-armed one stands, and how near a junction's nearest two arms come — and `qq town --joints` lists
every two-armed junction there is, where it stands, and which of the rule's two exceptions kept it. The arms
reading is what caught a duplicate road mid-way, at 0.0° apart; the joints listing is what says whether the
rule still holds on a map nobody has looked at.

## 2026-09-13 — a ring the merge keeps as one stretch is still a ring

**A boundary made of exactly one stretch could never be closed.** The walk strings the kept stretches by
their ends, and the pairing at a place will not let a stretch take *itself* up — a place with one boundary
arriving and the same one leaving offers no pair at all — so a ring that came back as a single stretch was
walked as a run with two ends and handed back as a hole. `Shut` then refused it a second time, for having
fewer than two pieces.

**The town does lay one.** A movement whose own radius is barely wider than the lane it carries folds its
ribbon's inner edge into a hook a few centimetres across: at a radius of `1.85 m` against a half-width of
`1.80 m`, the inner edge is an arc of radius `0.05 m`. Cut by its neighbours, what is left of that hook is
one stretch `0.104 m` long whose two ends stand `0.087 m` apart — inside the weld, and so one place.

**So a run is shut when it comes back to where it set off, however few stretches it is made of**, and the
two walks file their runs the same way. The piece count was never the question. What made this worth finding
rather than tolerating is that the hole it left was being read as a fact about the town — `GEN-51` carried an
exception for a fortnight on the strength of it.

**It is the cusp and not the fold that is hard.** A ribbon whose line turns tighter than its own half-width
inverts completely and merges cleanly; one that turns at very nearly its half-width leaves the near-degenerate
hook. Measured on the suite's own city: `-1.32 m` of ribbon inside the fold before the junctions were joined
away, `+0.39 m` after, and `+0.05 m` once the last exception went — only the last of the three left a hole.

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
handed back as what they are (`BandShell.Loose`) and drawn in the fault colour rather than shut with a
straight, because a merge that papers over its own faults is a merge nobody can read.

**Three figures carry the degeneracy and they are the whole of it** (`BandShell.Merge`): how far outside a
piece the cover test is taken, how near two band edges stand to be one edge, and how near two cut ends stand
to be one end. **Two coincident edges are settled by the lower-numbered line** — dropping both leaves a hole
and keeping both leaves a crossing with two ways on — and **a piece is cut where another piece's own end
stands on it as well as where one crosses it**, which is the one cut a crossing cannot find: two square ends
laid along each other cross nowhere, and without that cut the overlap between them is weighed whole.

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
ground one road takes (GEN-49) and a pavement's width on top of it, because two mouths whose paving abuts is
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
whatever else is true (GEN-50), so taking one spends a place in the scatter on a street the settling would
open again. The arterials were the sharper find: a one-way street hung off one costs a district its second
way in, and a laid city with them had eight of the town tier's questions fail — claims left held behind a
car in a box, walkers sent over a carriageway, an ambulance that never arrived. Excluded, all eight came
back.

**What is left is two paving faults the old grid never reached** — a three-armed junction with no box, and
a corner turn setting off where no kerb ends, both on the laid city. Keeping one-way streets out of the
districts that wander hides the first and not the second, which is why neither is hidden: they are the
pavement's to answer for and not the arrangement's.

## 2026-09-05 — the pavement is laid once, and the picture and the answer read that laying

`GroundMesh.Build` laid the pavement as draw calls and `GroundShapes` laid the same four pieces again as
coverage tests, with nothing but memory keeping them in step (TER-7) — and the failure it invites is quiet:
a band widened in the picture and not the answer is a walker refused ground it can see it is standing on.
`Paving.Lay` states the pieces once. The restructure is exactly behaviour-preserving and the frames come
back byte-identical, which is the only test that could have said so.

## 2026-09-05 — a town keeps only the one-way streets it can be driven round with

Drivable is asked of the movements and not the roads (GEN-18): a block whose streets all run inwards keeps
one connected component and is still somewhere a car drives into and never leaves. Reachable everywhere was
not enough either, because the fault is local (GEN-50) — a one-way street at a two-armed node leaves a lane
nothing ever arrives on, so the town draws a lane, a stop bar and a line no car is on. A movement leaving a
node needs some road other than its own arriving there.

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

## 2026-09-01 — a car park is three to six bays, because a run of frontage is an apron

Merging neighbouring slots laid sixteen bays of unbroken tarmac down one side of a street, and nothing ever
filled it — the town's whole roster is 520 cars over 319 lots. The merge was the right answer to the wrong
question: GEN-16 merges a junction because refusing one deletes every road at it, and nothing hangs off a
car park. GEN-4b bounds a lot at both ends, three to six. The bounds are what a lot *is* rather than a
tuning — the upper makes a car park a car park rather than a surface, the lower keeps it from being a
two-car lay-by that cost a lot's whole clearance. The clearance is measured between the rectangles and not
along the road, since on a bend an arc runs longer than the chord. Odesa's 319 lots hold 1377 bays where
they held about 3500.

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

## 2026-08-27 — the map says what a building is for, and its people start behind its doors

A shuffle off the world seed knew which buildings existed and could put a town's only hospital on a
cul-de-sac with no bay within a block. The record carries a use (GEN-9) and the format went to version 3 —
the migration the old decision priced and declined, at one field and one pass. The placement moved to where
a map is authored, laid out by farthest-point: a second of work once, which is exactly the sweep refused
when the answer had to be produced on every load. And the map's people start behind its doors, having
already been stood at them — a trip ends inside a building, so beginning there closes the round rather than
adding a stage. The dwell is drawn per person, so the streets fill over ten seconds. What it costs is that
a question about a body on the pavement can no longer be asked at tick zero.

## The last town laid is kept, and only the last

`Maps.Plan` laid a fresh town every time it was asked, so anything that asked twice paid twice: a review
sheet, a probe taking several readings, the visual tier staging a scenario a cell. A town is laid
deterministically from its own seed, so the second lay is the first town again and all it buys is the wait —
34 seconds of it on a city, and another 30 for the ground mesh that is a pure function of the plan under it.

**One town and never a table of them.** Kept by name, the first sweep over every shipped city would hold all
of them alive at once and a city is tens of megabytes of arrays. Kept as the last, it is never more than the
town whoever asked is about to use anyway — and the pattern that costs is repetition rather than revisiting.

**And a plan is as far as it goes.** The walking graph over one is the largest single cost of standing a town
up — 33 seconds against a tenth of a second for the road graph beside it — and it is as pure a function of
the plan as the pavement is, but handing one graph to two towns is not safe: the index it answers
`NearestEdge` from carries a scratch of its own (`ChainIndex`), and two towns asking at once is two walks
over one working set. Tried, it broke a hundred cases of the suite. What that costs is the shot path's to
work around (`TownStanding`), and what would let it be shared is the scratch belonging to whoever asks.

