# The lane layer, laid from the junction out — migration plan

Replace the whole of how the lines a car is driven on are produced, and stop laying everything else that
stood beside them. **After this the generator lays roads, junctions, car lanes and connectors, and nothing
else.** The kerb band, the pavement, the car parks, the buildings, the zebras, the bars, the signals and the
walking network stop being *laid*; the ground's own layers come back later as the town's boundary moved by a
figure, which is the construction TER-7b already names and not a second one invented here, and the rest come
back on top of that.

**The layer is dropped, not the entity**: `LaneLines` stays the contract every consumer reads, and
everything downstream of it — the road graph, the claims, the ribbons, the boundary merge, the ground
answer, the router — is re-fed rather than rewritten.

The change is an inversion. Today a road's curve is laid first and the lanes are cut out of it afterwards;
the connection points TER-5d names are an *output* of that cut. After this, **the connection points are
drawn first** and both the connectors and the road's own spline are laid to meet them.

## This is a step and not a destination

**The town this milestone lays is deliberately poorer than the town before it and the town after it.** It is
a staging post: the lane layer is torn out and rebuilt the right way up, and everything that stood on the
old one is put down rather than carried across a rewrite it would have to be rewritten for twice. What comes
off is not judged unwanted — it is judged **not worth porting onto a layer that is being replaced this
month**.

Three consequences of reading it that way, and they set the tone of every decision below:

- **Anything whose only cost is that it goes away is cheap.** Where the choice is between paying to preserve
  something across the rework and letting it drop and come back off the new layer, it drops. Parking and the
  buildings are both decided that way, and so is the paint.
- **Nothing is preserved in a second form to bridge the gap.** A layer laid a second way "until the
  boundary comes back" is the second answer TER-7b exists to refuse, and it would outlive the milestone.
- **What is gone is gone loudly.** Every absence is a nought an instrument prints or a line in
  [docs/index.md](docs/index.md#known-gaps), never a deletion that leaves no trace — which is what makes the
  way back a re-fill of known holes rather than an archaeology.

The order of the road back is roughly: the boundary and the layers off it (TER-7b), then the kerb, the
pavement and the walking network, then the crossings and the signals, then parking, then the buildings and
the errands that need them. **None of it is in this plan**, and no decision here is allowed to be justified
by how one of them will land.

---

## 0. Scope

### What the generator lays when this is done

Roads, junctions, car lanes and connectors. That is the whole of the town's road fabric, and the only thing
rendered off it for now is the car lanes in the debug overlay.

### What stops being laid

- The cut-and-set-back lane construction, and the fold behind it.
- **Parking, entirely.** Not "dropped from generation and kept as an entity with a placement to be restored":
  a town laid by this build has no car park, no bay, no bay way and no parking space anywhere in it, and the
  frontage arithmetic that placed them goes with them. Nothing this milestone lays has parking in it and
  nothing in it is shaped around parking coming back next week.
- **The buildings**, which go with the rest of the slot stage. They are not in this plan's subject and they
  are not worth the couplings keeping them would cost (§6.3, §6.6, §7) — a stage that places nothing places
  nothing at all. The town is roads, junctions and grass, and that is the whole of what it is for a while.
- The junction's furniture: kerb fillets, zebras, stop bars, and whether a junction is lit.
- **The kerb band** — the driven ground as one shape ([Kerbs.cs](src/citygen/Kerbs.cs)) — and **the
  pavement**, which is now only a width carried through `Paving` and a widening in the ground answer. The
  drawing of both went with the perimeter work; this takes the laying.
- The walking network, which already lays nothing ([FootGraph.cs:178-200](src/world/foot/FootGraph.cs#L178-L200)).
- **The road's own shape**, which is no longer settled before the lanes are. This follows from the
  inversion: a road that must arrive on a drawn bearing cannot be a chord that wanders.
- The road paint pass in the ground mesh, whose every source array comes back empty.

### What is untouched, because it neither feeds nor reads the lane layer

Terrain, water and props. They are a different subject and are laid as they are laid today — with one
coupling this plan has to pay for rather than declare away (§6.7): the props ask the ground two questions
about things that stop existing, one of which can be re-sourced and one of which has no answer at all.

**The spawn stage is the exception, and it is the one place outside the lane layer this plan rewrites.**
Today it stands a car in a bay and a person at a door ([SpawnStage.cs](src/citygen/gen/SpawnStage.cs)), and
after this there are neither. A town that stands nothing up is a town nothing drives on, which would leave
the whole milestone unobservable — so **the spawn stage stands cars on the lanes instead**, and stands no
people at all. §6.6 is the whole of that decision.

### What is untouched and re-fed, because it reads what is laid rather than laying anything

`LaneShell`, its merge and its rings; the driven-line index; `GroundShapes`; `RoadGraph` and everything
above it. **Nothing about the ribbons changes**: they take the driven lines the town has and make one
boundary of them, and after this the town has fewer driven lines. That is the whole of the difference from
where they stand.

**Which maps this reaches:** the generated cities, and `Test`, which becomes one (§8). Every other map is
parked for the milestone.

---

## 1. What is there now

### 1.1 The constructions in scope

| Construction | Where | What it does today |
|---|---|---|
| **Car lanes, cut at the discs** | [LaneLines.cs](src/citygen/LaneLines.cs) — `Of` → `Cut` → `Welded` | Offsets each road's own curve by half a lane to each side, cuts the offset chain wherever a junction disc bites it ([RoadCuts.cs](src/citygen/RoadCuts.cs)), classifies a connector between every admissible lane pair, settles one setback per lane end deep enough that every movement off it reaches the junction's corner radius, then draws each connector as the line tangent to the two cut ends |
| **The fold** | [LaneLines.Welds.cs](src/citygen/LaneLines.Welds.cs) | Rewrites a run of lanes whose every join forks nothing as one lane, the join swallowed as a band of tarmac in a `WeldTable` (TER-5h) |
| **Bay ways** | [SlotStage.cs](src/citygen/gen/SlotStage.cs) `LayTheLots`, [RoadFrontages.cs](src/citygen/RoadFrontages.cs), [BayLines.cs](src/citygen/BayLines.cs) | Cuts lots out of road frontage, then hangs a way in and a way out off the nearest lane, each ending at the pose a car comes to rest in |
| **The buildings** | [SlotStage.cs](src/citygen/gen/SlotStage.cs), the other half | Stands a building off each road's frontage at a setback of the carriageway's half plus a walk ([SlotStage.cs:75](src/citygen/gen/SlotStage.cs#L75), [:226](src/citygen/gen/SlotStage.cs#L226)), cleared against a ground answer built over the car parks the same stage has just laid ([SlotStage.cs:171](src/citygen/gen/SlotStage.cs#L171)) |
| **The roster's places** | [SpawnStage.cs](src/citygen/gen/SpawnStage.cs) | A car in a bay and a person at a building's door (GEN-7). **The brief's car count is clamped to the bays there are**, so a town with no bay stands no car up |
| **The kerb band** | [Kerbs.cs](src/citygen/Kerbs.cs) | Every driven line as a band its own width, indexed, so a point can be asked how far off the driven ground it stands. Read by the ground answer's paving question and by nothing else that survives |
| **The pavement** | [Paving.cs](src/citygen/Paving.cs) `WalkM`, [GroundShapes.cs:63-74](src/citygen/GroundShapes.cs#L63-L74) | A width, carried into the ground answer, which widens every road's ground by it and answers "is there paving within reach" off the kerb band. Nothing draws it any more |
| **Walking lanes** | [FootGraph.cs](src/world/foot/FootGraph.cs) + its `Builder`, [WalkingNetwork.cs](src/world/foot/WalkingNetwork.cs) | **Already empty on this branch.** The walking lane was the boundary moved half a walk, and that construction went with the perimeter rework. What is left is a builder with nothing to build, run through a prune, two run-on passes and a de-duplication over an empty graph |
| **The junction's furniture** | [Furniture.cs](src/citygen/gen/Furniture.cs) | Solves each junction's kerb fillets between neighbouring arms, strikes a zebra on every arm, paints a bar behind each zebra on every lane driving in |
| **Signals** | [RoadStage.cs:547-590](src/citygen/gen/RoadStage.cs#L547-L590) | Draws whether a junction is lit and its phase offset, in a stream of its own |
| **The paint** | [GroundMesh.cs:208-238](src/app/render/GroundMesh.cs#L208-L238), [GroundMesh.Paint.cs](src/app/render/GroundMesh.Paint.cs) | Lane dashes, zebras, bars and bay strokes, drawn off the plan's arrays behind a temporary switch |

### 1.2 The road's shape

[RoadStage.cs](src/citygen/gen/RoadStage.cs) gives each layout edge a chain of arcs: a chord with a bounded
wander through drawn via-points, its corners rounded to the class's floor, each end held straight for the
stub a junction needs, and forkless nodes swept into one arc across both arms. `StraysM` then reports how
far each road's drawn shape stands off its chord, which is what
[TownLayout.cs:409](src/citygen/gen/TownLayout.cs#L409) `UnpickTheCrossings` uses to delete roads that would
share ground (GEN-17).

### 1.3 Where the layer sits

Lanes are **not carried by `CityPlan`**. They are derived lazily, once, by
[Paving.cs:182-194](src/citygen/Paving.cs#L182-L194) off `GroundPieces` — the roads, the junctions and the
lots. `Paving` then hands the same lines to `Kerbs`, to the boundary merge and to `GroundShapes`.

**And `GroundPieces` carries no seed.** `CityPlan` does ([CityPlan.cs:33](src/citygen/CityPlan.cs#L33), and
[TownWriter.cs:36](src/citygen/TownWriter.cs#L36) round-trips it), but the layer below it is handed shapes
and nothing else — through `CityPlan.Paving` ([CityPlan.cs:111](src/citygen/CityPlan.cs#L111)), which has
the plan, and through [GroundShapes.cs:54](src/citygen/GroundShapes.cs#L54), which does not and is the ctor
generation itself uses twice ([TownGenerator.cs:93](src/citygen/gen/TownGenerator.cs#L93),
[SlotStage.cs:171](src/citygen/gen/SlotStage.cs#L171)). Anything the new construction needs beyond shapes
has to be threaded to both. §5 is where that is paid for.

The seam above the layer is [RoadGraph.cs:396](src/world/road/RoadGraph.cs#L396) —
`RoadGraph.Build(LaneLines, SimConfig)`. Everything the world and the agents know about driving comes
through it, and it already takes lines that "already exist" rather than laying any.

### 1.4 Why the order is the problem

The layout hands `RoadStage` nodes and chords. The road gets a curve bounded by three things and never by
an arm's bearing (GEN-12). The disc is then sized off however far its widest arm's ground reaches. Only
after all that do lanes appear, by subtraction.

So the bearing a car actually enters a junction on is whatever the road's chord happened to leave, the
connection point is wherever the setback arithmetic landed, and the movement between two of them is a line
drawn to fit — never a line a car was shown to be able to drive. TER-5d already states the model this plan
builds; what exists is that model read backwards.

### 1.5 Why the kerb and the pavement go with it

The perimeter work already struck every line the ground had beside a road — the pavement, its rim, the kerb
line — off the *drawing*, and [docs/index.md](docs/index.md#known-gaps) carries that as a named gap. What is
left of them is laying with no reader: a band index whose one consumer is a ground question, and a width
that widens a road's ground answer. **TER-7b and TER-3c.8 say how they come back** — the boundary is
computed once off the union of the driven ground and every layer is that boundary moved by a figure — and
nothing in this plan is the way back. Laying them a second way in the meantime is the second answer those
two rules exist to refuse.

---

## 2. What stops being laid

**Nothing is left standing that has no subject.** A construction whose input stops existing is deleted in
the commit that stops producing the input — not deprecated, not left compiling behind a switch, not kept
"until the rework settles". The inventory in §2.6 is the whole of it, and it is meant to be checked off
rather than read: an item not on one of its three lists is an item nobody decided about.

**The line between deleting and leaving empty is whether the subject comes back**, and the road back at the
head of this document is what answers that — not how dead the code looks today.

- **Obsolete** is a construction that will never run again *in any form*, because what replaces it is the
  new layer. The cut, the fold, the setbacks, the chord machinery, the frontage arithmetic, the parked
  maps. **Deleted**, and git is where it is kept.
- **Dormant** is a type whose subject is named in the road back and whose code will be re-fed rather than
  rewritten when it returns: `world/parking/`, the crossing machinery, the signals. **Kept, standing over
  nothing**, and each one owes a reading that says it is genuinely inert rather than quietly broken.
- **A thing that is neither** is a decision, not a default. They are called out where they arise and
  gathered in §11.

The test is applied per type and never per slice. A slice can hold an obsolete file and a dormant one, and
`world/foot/` holds both.

### 2.1 The layer

| File | Why |
|---|---|
| [LaneLines.cs](src/citygen/LaneLines.cs) — the cut, the adjacency, the connector classification, the setbacks and the cut-back | The whole cut-and-set-back construction. The type, its arrays and its accessors stay |
| [LaneLines.Welds.cs](src/citygen/LaneLines.Welds.cs) | The fold is a pass over lanes cut at every disc. Lanes laid to drawn points are cut where a driver decides something already — §6.4 |
| [RoadCuts.cs](src/citygen/RoadCuts.cs) | Answers "where does a junction disc bite this chain". Nothing asks it once a lane ends at a drawn point |
| [BayLines.cs](src/citygen/BayLines.cs), [RoadFrontages.cs](src/citygen/RoadFrontages.cs) | Bay ways, and which road a lot hangs off |
| [SlotStage.cs](src/citygen/gen/SlotStage.cs) — **all of it** | Parking's whole placement, and the buildings with it. A stage that places neither is not a stage |
| [SpawnStage.cs](src/citygen/gen/SpawnStage.cs) — both halves of what it stands | Rewritten rather than dropped: cars onto lanes, and no people (§6.6) |
| [Furniture.cs](src/citygen/gen/Furniture.cs) — all of it | Fillets, zebras and bars. A fillet is kerb geometry, and the kerb is not laid here any more |
| [RoadStage.cs](src/citygen/gen/RoadStage.cs) — the `Lit` / `PhaseOffsetS` draw | Signals |
| [Kerbs.cs](src/citygen/Kerbs.cs) — the band, the shards and the grid | The driven ground as one shape, with one reader left. **The rounding and joining figures it carries are read all over the build** and have to land somewhere before the type goes — §11 |
| [Paving.cs](src/citygen/Paving.cs) — `Kerbs`, `Bays`, `WalkM` | What is left is the driven lines and the index over them, which is what the ribbons ask for |
| [GroundShapes.cs](src/citygen/GroundShapes.cs) — the walk widening and the paving question | §6.7 |
| [FootGraph.cs](src/world/foot/FootGraph.cs) `Build`'s body, and the `Builder` passes nothing feeds | A pipeline over an empty graph |
| [GroundMesh.Paint.cs](src/app/render/GroundMesh.Paint.cs) and the paint pass that calls it | Dashes, zebras, bars and bay strokes, all off arrays that come back empty |

### 2.2 Everything else that no longer follows the flow

**The road's shaping**, all of [RoadStage.cs](src/citygen/gen/RoadStage.cs)'s chord machinery: the chain,
the wander and its bound, the rounding, the stub, the strays and the sagitta, and the forkless sweep. A road
is a spline to two drawn bearings now, and none of this describes one. **What stays in that stage** is the
road and junction arrays themselves, the class floor radius, the ring and roundabout readings, and the
one-way road's shift onto the half of the carriageway its traffic drives — which the connection points have
to agree with rather than re-derive (§6.9).

**The crossing unpick**, [TownLayout.cs:409-468](src/citygen/gen/TownLayout.cs#L409-L468). It is the
chord-and-sagitta enforcement of GEN-17, and GEN-17 as written is obsolete: a spline free to reach its own
end bearings is not bounded by its chord, so nothing it reports is true. **Non-intersection becomes a
property of the spline generator** (§4.4) and not a deletion pass after the fact — and where in the order
that leaves the layout's own repairs is §6.8, which is the sharpest edge in this plan.

**Two road shapes survive the drop and are not splines**: a bridge is one straight span and is its own road
(GEN-14a, `RoadClass.Bridge`), and a roundabout's ring piece is one arc of one circle (GEN-19). Both are
shapes settled elsewhere and answered for whole; the spline generator never sees them. Their end bearings
are what they are, and the connection points at their ends take them rather than drawing their own — §6.10.

### 2.3 Emptied, not deleted

Plan arrays that stay in the format and come back with nothing in them: `Crosswalks`, `StopLines`,
`ParkingLots`, `Buildings`, `JunctionCorners`, `PavedAreas`, `PavementCorners`, and `Junctions.Lit` /
`PhaseOffsetS` all false and nil. `Spawns` is the one that does not empty — it carries cars and no people.
A `.town` file still round-trips; it carries less.

### 2.4 Kept, and re-fed

`LaneShell` + `.Merge` + `.Rings`, the driven-line index, `GroundShapes.At` and the readings over it,
`ChainIndex`, `Spline`, `ArcSeg`, and every consumer above the plan tier. **The ribbons are the point of
keeping `LaneLines` as the contract**: they merge whatever driven lines the town has, and after this it has
lanes and connectors.

### 2.5 Kept as entities with no generation behind them

`world/parking/`, `agents/trafficlight/`, the crossing machinery in `world/foot/`,
`world/road/LaneFurniture`, and everything that reads `plan.Buildings` — the census, the standing sprites,
the boarding. They must stand up over a town with none of their subject on it, which is a town the laid maps
already meet.

**One configuration is new, and it is the one §6.6 exists for**: a town with no building and nobody in it,
carrying cars that are stood up anyway. The laid maps have no buildings and no cars, the cities today have
both, and nothing has ever run the half of each. It is a state to check rather than to assume, and it is
checked by running the town rather than by reading the code.

### 2.6 The inventory

Read against the rule at the head of §2. **Everything the sweep found is on one of these three lists**, and
a file that turns out to belong on none of them is a finding rather than an omission.

#### Deleted outright — the file goes

| Slice | Files | Why it has no subject |
|---|---|---|
| `citygen/` | `LaneLines.Welds.cs`, `RoadCuts.cs`, `BayLines.cs`, `RoadFrontages.cs`, `Kerbs.cs` | The fold, the disc cut, the bay ways, the frontage arithmetic, the driven ground as one band. `Kerbs.cs` goes **after** §11.4 rehouses the rounding figures, which are read by the merge, the mesh, the road stage, the debug overlay, the foot graph and six test classes |
| `citygen/gen/` | `Furniture.cs`, `SlotStage.cs`, `Services.cs` | Fillets, zebras and bars; the lots and the buildings; and the service placement, which is eligible-buildings-near-a-car-park and so has lost both its inputs at once |
| `citygen/` — the parked maps | `TrackPlan.cs`, `TrackSection.cs`, `ExamPlan.cs`, `ExamMap.cs`, `ExamCard.cs`, `ExamGround.cs`, `ExamLattice.cs`, `FootwayPlan.cs`, `FootwayCard.cs`, `FootwayLattice.cs`, `SkidpadPlan.cs` | §8 takes them off the shipped list; a plan nothing can open is the definition of dangling. `IdlePlan.cs` is the one that stays |
| `towns/` | `Zebras.town`, `Test.town` | The crossings map is parked; the fixture becomes a brief (§8) |
| `bench/` — the parked maps' tails | `TrackProbe.cs`, `TrackMetrics.cs`, `TrackWatch.cs`, `FleetWatch.cs`, `SkidpadProbe.cs`, `SkidpadWatch.cs`, `SkidpadFigures.cs`, `ExamProbe.cs`, `ExamWatch.cs`, `FootwayProbe.cs`, `FootwayWatch.cs`, `CrossingProbe.cs`, `ZebraWatch.cs` | Each stages its scenario on a map that is gone. The `track` / `drunk` / `fleet` / `skidpad` / `exam` / `footway` / `crossings` entries come out of [CheckCatalogue.cs](src/bench/CheckCatalogue.cs) with them |
| `bench/` — no subject on any map | `SignalProbe.cs` | Nothing is lit anywhere, so there is no lit town to sample |
| `app/render/` | `GroundMesh.Paint.cs` | Dashes, zebras, bars and bay strokes |
| `app/hud/` | `TrackPanel.cs`, and `Interface`'s track figures and their switch | The panel reads `TrackMetrics`, which goes with the lap |
| `tests/` | Every class staged on a parked map, and every class stating a behaviour §2 removes | VER-12: a test asserting nothing passes. Deleted **with** the behaviour, in the same commit, never after it |

#### Gutted — the file stays, the construction goes

| File | What comes out |
|---|---|
| [LaneLines.cs](src/citygen/LaneLines.cs) | `Of` / `Cut` / `Welded`, the adjacency walk, the connector classification, the setbacks, the cut-back. The type, its arrays and its accessors stay — §6.1 |
| [Paving.cs](src/citygen/Paving.cs) | `Kerbs`, `Bays`, `WalkM`, and the run-through query it fed them |
| [GroundShapes.cs](src/citygen/GroundShapes.cs), [.Turns.cs](src/citygen/GroundShapes.Turns.cs) | The walk widening, the paving question, and the third set of driven lines — the bays — which now has nothing in it |
| [GroundPieces.cs](src/citygen/GroundPieces.cs) | `With(lots)`, whose only caller was the slot stage |
| [RoadStage.cs](src/citygen/gen/RoadStage.cs) | All the chord machinery (§2.2) and the `Lit` / `PhaseOffsetS` draw with its seed stream |
| [TownLayout.cs](src/citygen/gen/TownLayout.cs) | `UnpickTheCrossings` and the chord-separation arithmetic under it (§2.2, §6.8) |
| [TownGenerator.cs](src/citygen/gen/TownGenerator.cs) | The slot stage, the services, the signal and slot streams, and the second ground answer built to clear buildings against |
| [SpawnStage.cs](src/citygen/gen/SpawnStage.cs) | Both halves, replaced rather than removed — §6.6 |
| [PropStage.cs](src/citygen/gen/PropStage.cs) | Two ground questions, one re-sourced and one whose rule changes — §6.7 |
| [TownBrief.cs](src/citygen/gen/TownBrief.cs) | `Buildings`, `People`, `ParkingSlotShare`, `Hospitals`, `PoliceStations`, `Depots`, and the roster checks over them. `Cars` stays and means something new (§11) |
| [Maps.cs](src/citygen/Maps.cs) | Every row of `Laid` but the ring, and the whole of `Filed` |
| [MapCatalogue.cs](src/app/hud/MapCatalogue.cs) | The parked maps' rows |
| [GroundMesh.cs](src/app/render/GroundMesh.cs) | The paint pass and the switch that hid it |
| [FootGraph.cs](src/world/foot/FootGraph.cs) | `Build`'s body — the prune, the two run-ons and the de-duplication, all over an empty graph — and the `Builder` passes nothing feeds |
| [SimConfig](src/core/config/SimConfig.Derived.cs) + the `CityGen` group | Every figure authored for a bay, a lot, a frontage, a fillet, a zebra, a bar or a signal, and every derived figure standing on one. **A figure with no reader is the same defect as a literal with no name** |

#### Stands empty, and is not touched

`world/parking/` (its registry is threaded through a dozen `TownWorld` partials, the manoeuvre desk and the
car fleet), `agents/trafficlight/`, the crossing machinery in `world/foot/`, `world/road/LaneFurniture` and
`CentrelineRuns`, `world/statics/`'s building roster, roofs and uses, `agents/ambulance`'s hospital roster
and `agents/service`'s rosters, and the standing sprites.

**Every one of them is dormant rather than obsolete** — parking, the crossings, the signals and the
buildings are all named in the road back — and **every one of them is also a place this milestone could be
silently broken**, because a type that stands over nothing is a type nothing exercises. What they owe is
§10's inert reading and not a deletion.

**Two probes sit on the line and are §11's to settle**: `rescue` and `recovery` stage a casualty at a
hospital and a wreck near a depot, and a hospital and a depot are *buildings*
([RescueProbe.cs](src/bench/RescueProbe.cs), [RecoveryProbe.cs](src/bench/RecoveryProbe.cs)). With none,
neither can stage its scenario at all. They are not parked-map tails and their subject does come back, so
deleting them is wrong — but a probe that cannot stage anything has to *say* so rather than read as a run
that found nothing. The same question, in weaker form, is `trips`, which draws a trip between two buildings.

---

## 3. Goal and success criteria

**A lane is placed, not subtracted.** After this:

1. Every lane end in a generated town is a connection point the seed drew, with a stated bearing, and the
   road's curve arrives on that bearing rather than the other way round.
2. Every connector between two connection points is a line whose curvature a car at the junction's design
   speed can hold, derived from a speed and a grip and never authored as a radius.
3. Every road between two junctions is a spline that meets both ends' bearings exactly, bends no tighter
   than its class affords, crosses neither itself nor any other line, and is straighter or looser according
   to the district it runs in.
4. `LaneLines` is produced by the new construction and consumed unchanged by `RoadGraph` and by the ribbons.
5. **A generated town carries no kerb band, no pavement, no car park, no building, no paint and no signal**,
   and every instrument that counts one reports nought rather than being deleted.
6. **Cars drive on it.** A town that lays a perfect road fabric nothing moves over has not been shown to
   have laid one, and every reading below except the census depends on there being traffic.

**The readings that say so:**

- `qq town Odesa` and `--bench census` report lanes, connectors and road curvature over a generated city
  with nothing dangling and no two roads sharing ground, and nought for everything in §2.3.
- `--bench maneuvers` drives the catalogue on a generated city. **The entries about parking, paint and
  lights are the set nothing entered; the ordinary driving entries are not** — a run where the whole
  catalogue is unentered is the failure mode §6.6 exists to prevent, not the expected shape of the reading.
- `--bench census`'s town watch keeps its three claims over a run, and its arrival reading is understood to
  be nought by construction (§9) rather than read as a town that failed to get anywhere.
- `qq tests all` stays inside its five minutes.
- A `--shot` with `--ui perimeter` closes the rings the perimeter work closed, because the boundary is the
  merge of the ribbons these lines lay and the lines changed, not the merge.

---

## 4. The new flow

Five steps. The first is unchanged; the rest are the work.

**4.1 — The logical graph, as now.** Nodes, districts, arterials, lattice, merges, component keeping,
dead-end pruning, roundabouts, one-way scatter.

**4.2 — Connection points at every node.** For each link out of a node: take the bearing from the node to
its neighbour, jitter it by a bounded angle, and stand a set of points on the line perpendicular to that
bearing at a standoff from the node's centre. Each point is an **enter** or an **exit** depending on which
way its lane is driven, and how many points a link gets is how many lanes its road carries each way
(GEN-15, TER-4d).

**The points are stored nowhere.** They are a pure function of the world seed and the link, drawn on the
link's own type, and whoever needs them draws them again. That is what lets the lanes stay derived from the
plan (§5) while nothing derived is written to disk. The two ends of one link are drawn independently, so a
road has two bearings to satisfy and they do not agree.

**4.3 — Connectors, inside the junction.** Between every enter point and every exit point of *different*
links at the node, lay the line a car drives, and classify the turn it is — the connector table carries a
kind and the router prices off it. Not between two points of the same link: that is the turn in the road
TER-5f bans, and it stays banned by construction rather than by a check.

The line is the one a car can hold at the junction's own design speed: its curvature comes from the
cornering radius a speed and a grip afford, the way GEN-19's ring already does. A pair the geometry cannot
join inside that bound is a movement the junction does not offer — but **a junction may not refuse its way
out of being reachable**, which is §6.8's other half.

**4.4 — The road between two junctions.** Generate a spline whose two ends match the connection-point
bearings exactly, subject to three bounds: it does not cross itself, it does not come within a road's
footprint of any other line, and it bends no tighter than its class's design speed affords. How much it
wanders is drawn from the district it runs in — a strict district lays near-straight roads, a loose one lays
curves.

**4.5 — Lanes along the road.** For each enter/exit pair the two ends agreed on, lay the lane that follows
the road's curve between them. The lane's line is the road's spline offset to that lane's own share of the
carriageway, and its two ends are the connection points themselves — nothing is cut back, because nothing
was laid past them.

---

## 5. Where the lanes come from

**Settled: the plan carries no lanes and no connection points, and lanes stay derived.** `CityPlan` gains
nothing. `Paving` goes on laying `LaneLines` on first ask, from the roads, the junctions and the seed the
plan already carries — the connection points are drawn again there from the same function that drew them
during generation, and they come out the same because a seed and a link are all they ever depended on.

Four consequences, all load-bearing:

- **The drawing function is one function with one home**, and generation and derivation both call it. Two
  copies of it is two towns.
- **The seed has to reach the derivation, and today it does not.** §1.3 has the detail: `Paving` is handed
  `GroundPieces`, which carries no seed, and the ctor generation itself uses has no `CityPlan` to take one
  from either. Whatever the drawing function needs beyond shapes is threaded to **both** entry points, or
  it is not an input to it. This is plumbing rather than design, but it is plumbing that has to land at
  step 3 of §12 and not be discovered at step 7.
- **It must not depend on anything the plan does not carry.** If a link's road class or its district turns
  out to be an input, either the plan carries it or it is not an input. Road width and which ways it is
  driven are carried; a district is not.
- **A link has to be the same link on both sides of the seam.** During generation a link is a layout edge;
  at derivation it is a road in `CityPlan.Roads`, and the two numberings are not the same one — the unpick,
  the component keeping, the dead-end pruning and the roundabout opening all sit between them, and
  `TownLayout.Rebuilt` renumbers the nodes as well as the edges.

  **The two ways out are not equivalent, and only one survives §6.8.** Keying the draw off geometry the plan
  carries verbatim — the junction pair's own centres — works, because a road carries `FromJunction` and
  `ToJunction`, because those indices are the layout's at the moment the roads are laid, and because the
  centres round-trip bit-exact through the `.town` file, which stores them as raw `F32`. Carrying a link
  identity on the road arrays instead only works if the identity is minted *after* the renumbering — and
  §6.8's recommended option draws the points before it. **Recommended: key off the centres**, and settle it
  at step 3 of §12 before anything is laid on top of it.

**And a plan that was never generated still has to answer.** `Paving` derives lanes for every map, including
one laid in code. Whatever survives §8 as a laid map either draws its own points by the same rule off its own
roads and junctions, or lays no lanes at all and says so.

---

## 6. Key design decisions and constraints

**6.1 `LaneLines` is the contract and holds its shape.** Its arrays, its accessors and its connector table
are what `RoadGraph`, the ribbons and the turns read. Two of its members lose their meaning and neither is
removed here: the weld table comes back empty (the fold is gone, §6.4) and the cut-back metres come back
nought (nothing is cut back, §4.5). A reader that treats either as news is a reader to fix, not a field to
delete — and the ones that exist are named in §7.

**6.2 Every figure is authored on `SimConfig`, in the `CityGen` group, and derived on the root.** The
standoff and the jitter bound are raw geometric terms and are authored. **The connector's radius is not**:
it is the cornering radius of a design speed on tarmac. Authoring a turning circle and back-solving anything
out of it is refused by the project's figure rule. A literal in the laying code is a defect.

**6.3 The seed streams.** GEN-11 says retuning one stage may not move what an earlier one laid. The
connection-point jitter and the spline wander are new draws and get a stream of their own. The point drawing
must be indexable rather than sequential — the derivation at `Paving` time asks for one link's points
without walking every link before it.

**The slot stage's stream does not need splitting, because the stage goes whole.** Today one stream fills
both car parks and buildings in a drawn order, drawn in `TownGenerator` and handed down
([TownGenerator.cs:96](src/citygen/gen/TownGenerator.cs#L96)), so dropping only the lots would move every
building in the town. It was worth a paragraph while the buildings were being kept; with the buildings going
too the whole question dissolves, and the stream is deleted rather than split. **This is the clearest case
of the staging-post economics above**: preserving the buildings would have cost a stream split that GEN-11 would then have to
be satisfied about, *and* it would not have worked — the placement is also cleared against a ground answer
that loses its walk widening and its lots, so the buildings would have moved whatever the stream did.

**6.4 The fold goes, and TER-5h with it.** A lane is laid between two drawn points at a node the graph kept,
so there is no join to rub out afterwards. If a forkless node survives step 4.2, it is handled there — by
drawing no points for it, or by the graph not keeping it — and never by a pass over finished lanes. TER-5h
describes the pass and is obsolete with it; what it also describes is why a lane names the road it sets off
on and the road it arrives on separately, and after this those two are always the same road.

**6.5 There is no parking, and `world/parking/` is kept as code rather than as a feature.** The slice stays
compiling and stands up over a town with no bay, which is a town it already meets. What goes is every path
that *places* one — and, because there is now no bay anywhere, every path that *reaches* one is dead code
that happens to compile. It is kept on §0's terms: parking comes back, and moving the code is cheaper than
writing it twice.

**6.6 Cars are stood on the lanes by the spawn stage, and nobody is stood at all.** This is the one decision
outside the lane layer, and it is what keeps the milestone observable.

The chain that has to be got right runs through three facts, none of which is optional:

- **Nothing else stands a car up.** The roster comes from the plan's spawns, and the spawn stage clamps the
  brief's car count to the number of bays there are ([SpawnStage.cs](src/citygen/gen/SpawnStage.cs)). No
  bays, no cars — so the town is empty however good its roads are.
- **`TownWorld.DriveTheEmptyMap` is not a fallback that fires on its own.** It puts a car on the lane under
  it, but only for a plan with **neither** buildings nor parking spaces
  ([TownWorld.Spawn.cs:249](src/world/town/TownWorld.Spawn.cs#L249)). Keeping the buildings would have held
  that gate shut and left the cars standing — which is the second reason the buildings go, and the reason
  this decision and §0's dropping of them are one decision rather than two.
- **A car needs a destination or a lane, and after this it has no destination.** The only two things that
  set one are the parking errand ([TownWorld.Parking.cs:144](src/world/town/TownWorld.Parking.cs#L144)) and
  the ambulance. So the car tours the lane it is on, which is exactly the traffic the exam probe already
  describes as a car touring the lattice.

**So the spawn stage stands the brief's cars on the town's own lanes** — a station on a driven line, clear
of other cars, at the lane's own heading — and stands no people, there being no door to stand one at and no
walking network to walk it on. Where the lanes come from at spawn time is the implementation's: the stage
runs after the roads, and the ground answer built there already has the driven lines on it.

GEN-7 says cars start stopped in parking spaces and people start inside buildings. **Both halves are now
false of a generated town, and GEN-7 is named in the known gaps rather than reworded to match the code.**

**6.7 The kerb and the pavement come back off the boundary, and not before.** TER-3c.8 and TER-7b are the
owner's two `P0` rules and both say the same thing: the boundary is computed once off the union of the
driven ground, and what the ground has beside a road is that boundary moved by a figure. **Neither is
reworded by this plan and neither is reargued.** The build already does not meet them — five of six layers
are absent, and [docs/index.md](docs/index.md#known-gaps) says so — and this milestone widens that absence by
the kerb band and the walk width, then leaves the route back exactly where TER-7b puts it.

**What needs an answer *inside* this milestone is the prop stage, and it asks two questions rather than
one.** They are different questions with different answers:

- *Is there paving within reach* ([PropStage.cs:198](src/citygen/gen/PropStage.cs#L198)), which keeps the
  wild scatter off the roads. Answer it off the road ground the plan already carries, or off the merged
  boundary.
- *Is there a car park within reach* ([PropStage.cs:94](src/citygen/gen/PropStage.cs#L94)), which decides
  whether a verge prop is the kind that stands beside a car park. **There is no answer to this one** — the
  subject is gone — so the rule it feeds is the one that changes: a verge prop's kind stops depending on
  parking. That moves the prop draw whatever stream it is on, and that is accepted rather than worked
  around.

**And the walk figure genuinely does go from generation**, which it would not have done a week ago: its
other live reader was the building setback ([SlotStage.cs:75](src/citygen/gen/SlotStage.cs#L75)), and that
stage is gone. `SimConfig.PavementWidthM` stays authored — it is a figure about the town, not about the
stage that read it — and nothing in a generated town reads it until TER-7b's stack comes back.

**6.8 A link the spline refuses, and a movement the junction refuses, both leave the graph poorer than the
layout left it.** This is the ordering risk and it wants deciding before anything is laid.
[TownGenerator.cs:81-85](src/citygen/gen/TownGenerator.cs#L81-L85) runs the unpick, then keeps the largest
component, then prunes the dead ends, and only afterwards shapes the roads. Move non-intersection into the
spline (§4.4) and the deletions it causes land *behind* the two passes that repair them — so a refused link
can strand a component and a refused movement can leave a junction nothing reaches, with GEN-5 `P3`
(one connected drivable region) and GEN-18a both quietly broken. Three ways out, and one of them is picked
before step 6 of §12:

- **Lay the splines inside the layout**, before the component keeping and the pruning. Cleanest against
  GEN-10 — a stage constrains the next rather than checking it afterwards — and it means the layout stage
  holds geometry it does not hold today.
- **Repeat the repairs behind the laying.** Cheap to write, and it makes the pass order say what it means:
  lay, then keep, then prune, then lay again for whatever the pruning changed.
- **Refuse nothing**: shrink the wander envelope and the jitter until every link is layable and every pair
  joinable. Provable only if the bound can be shown to exist, which on a short link with two ends jittered
  apart it may not.

**Recommended: the first, with the second as the fallback the first is measured against.** Either way,
connectivity is a property the laying owes, not a test that reports it broken afterwards.

**6.9 The standoff, the disc and the one-way shift are one relation.** The disc is sized off the arms'
widths today, which makes deriving the standoff from the disc circular — the disc would be sized off arms
that end at the standoff. **Derive the disc from the standoff**, in one place, and let the arms follow. And
a one-way road sits on the half of the carriageway its traffic drives (`OntoTheDrivenHalf`, TER-4d), so its
points stand where that shift puts them rather than symmetrically about the node's own line.

**6.10 A bridge and a ring arc take their bearings rather than drawing them.** Both are shapes settled
elsewhere (§2.2). The connection points at their ends stand square to the shape that is already there, so
the jitter is nil for those links. Without this a bridgehead's points jitter off a deck that cannot move.

**6.11 The two engineering rules are not touched.** All of this is build-time: it allocates freely, runs
once, and produces blittable arrays. Nothing here is on a tick and nothing here crosses to native.

---

## 7. Blast radius

**§2.6 is the list; this is what each slice has to be thought about.** Where the two could disagree the
inventory is right, because it was swept and this was reasoned.

- **`citygen/`** — §2.6's first two tables are almost entirely this slice. What is left of it afterwards is
  the plan format, the generator, the ribbons, the ground answer and the idle ring.
- **`world/road/`** — compiles unchanged if `LaneLines` holds its shape. `LaneFurniture` and `CentrelineRuns`
  read crossings, bars and bay lines and will answer nothing. The readers that add a lane's cut-back to a
  distance now add nought, which is the right answer and a reading to check rather than assume.
- **`world/parking/`** — §6.5.
- **`world/foot/`** — already answering nothing; the dead passes go.
- **`world/town/`** — the empty-map gate (§6.6) is the one behavioural edit. Boarding, errands and the
  people's whole loop stand over a roster with nobody in it, which is the state the laid maps run in today.
- **`agents/trafficlight/`** — stands over a town with no lit junction.
- **`agents/ambulance/`, `agents/service/`, `agents/evacuator/`, `world/statics/`** — the slice nobody
  expected to be in this plan's radius. A hospital, a police station and a depot are *buildings* the
  generator chose a use for ([Services.cs](src/citygen/gen/Services.cs), GEN-9), so with no buildings every
  service roster is empty: no ambulance has a hospital to take a casualty to, and no evacuator has a yard to
  tow a wreck home to. **Nothing in these slices is deleted** — their subject is in the road back — but a
  town where a wreck is never recovered is a behaviour change and belongs in the gap entry rather than in a
  surprise six weeks from now.
- **`app/render/`** — the paint pass and everything under it goes; the grass, the water, the decks and the
  slabs stay. The building sprites draw nothing, their count being nought
  ([StandingSprites.cs](src/app/render/StandingSprites.cs)). **The debug overlay's lane paths are the whole
  of what says the town has roads**, so whatever they draw today is what this milestone is looked at
  through — over grass, with cars on it and nothing else.
- **`citygen/gen/` — the stages that are *not* in scope and pay anyway.** The prop stage asks two questions
  §6.7 takes the subject of; the spawn stage is rewritten outright (§6.6). Everything the building half of
  the slot stage read — the straight stub, the arm's bend, the frontage — stops being read at all, which is
  what makes §2.2's deletions cheap rather than blocked.
- **`bench/`** — a third of the catalogue goes (§2.6): seven entries with the parked maps, `signals` with
  the lit junctions, and `rescue`, `recovery` and `trips` left holding a decision. `TownCensus` keeps counting
  lots, buildings, crossings, bars, welds and lit junctions and **keeps reporting nought**: the instruments
  are what report an absence, and deleting them is how an absence goes quiet. **Every probe sweeps
  `Maps.Shipped()`**, which is the briefs on disk *plus* the maps laid in code plus the filed ones
  ([Maps.cs:61](src/citygen/Maps.cs#L61)) — so after §8 it is three generated cities and, if the ring stays
  on that list, `Idle`. A sweep that used to lay seven cheap maps now lays three towns, and whether the
  eighth is one of them is §8's to settle.
- **`tests/`** — the largest share of the work. A test that states a behaviour this milestone removes is
  deleted, not weakened (VER-12) — a test asserting nothing passes.

---

## 8. The other maps are parked

**Settled.** `Track` ×3, `Exam`, `Footway`, `Skidpad` and `Zebras` come off `Maps.Shipped()` for this
milestone. **"Parked" means deleted, not disconnected** — the plans that lay them, the probes and watches
that stage on them, their claims, their catalogue entries, their menu rows and their tests all go, and §2.6
lists the files. A map that nothing can open is not parked, it is dangling. `Maps.Filed` empties out.

**This is the one place in the plan where something is deleted whose subject is not in the road back**, and
it is worth being honest about: a laboratory map is cheap to write and expensive to carry across a rework
of the layer it is laid on, and the ones that come back will be laid against the new layer rather than
restored. What is lost with them — the exam's staged junctions, the lap's drivetrain figures, the pad's
circles, the crossings' walks — is named in the known gaps as a suite that got smaller, so that it is a debt
somebody chose rather than a capability that quietly went missing.

**`Idle` is the exception, because the menu stands over it.** GEN-1b (`P7`) says the start menu is drawn
over the idle ring and `Game.IdleMap` stands it up without anybody picking it, so parking it from the
shipped list does not settle what the menu stands over. **Recommended: keep the ring laid** — an empty ring
is still the frame the menu was laid to fit (OBS-1b) and bare ground is not — which means `Maps.Laid` keeps
exactly one row and §5's last paragraph has to answer for it. GEN-1b is the assistant's and can be reworded
if the ring turns out not to be worth its keep.

**Whether the kept ring is still *shipped* is a separate question and wants answering.** `Maps.Shipped()`
is the briefs plus `Laid` plus `Filed`, and it is what every probe and every sweep reads — so a ring kept
for the menu is a ring every probe lays unless it comes off that list while staying laid. The two are not
the same list today and this is the milestone that makes the difference visible. Settle it at step 2 of §12,
whichever way; what may not happen is §7's sweep cost being estimated off one answer while the menu depends
on the other.

**And `Test` as a brief leaves the suite with two towns that are the same kind of thing.**
[Towns.cs](src/tests/citygen/Towns.cs) carries `Fixture` — a file, stable by construction — and `City`, a
whole town laid from a brief at its own seed. Make the fixture a brief and the pair differ only in size and
seed. That is a real loss of a distinction the working agreement names, it is the same loss as the "no
longer a fixture" cost above rather than a second one, and it is named in the known gaps with it.

**`Test` becomes a generated brief** — `towns/Test.json`, a small city at its own seed, laid by
`TownGenerator` off the same path Odesa and River take. `towns/Test.town` goes. It is what every detailed
check is staged on ([Towns.cs:53](src/tests/citygen/Towns.cs#L53)), so the brief is authored to keep what
made the fixture worth having: one screen, one of every kind of ground, furnished thinly. Two costs, both
real:

- **It is no longer a fixture in the sense `docs/verification.md` means** — a generated town moves when the
  generator does, which is what a fixture exists not to do. Named in the known gaps rather than papered over.
- **Every tier now lays it.** The unit tier is four seconds and stages on this map; a brief that lays in
  anything but a moment is a tax on every run of the suite. That is the bound the brief's own figures are
  authored against (§11).

---

## 9. Risks and failure modes

- **The spline cannot meet both end bearings inside the curvature bound.** Two ends jittered apart on a
  short link may leave no admissible curve. The fallback is §6.8's, and it is picked there rather than
  discovered here.
- **Connectivity.** §6.8. The single largest correctness risk in the plan, because it breaks a `P3` rule
  silently and a town that is 98% connected looks right in a picture.
- **Non-intersection is now a search.** It replaces a cheap chord test with a real geometric one over every
  line already laid. This is the step with the uncertainty in it, in both correctness and lay time.
- **The boundary stops closing.** The known gap in [docs/index.md](docs/index.md#known-gaps) is one open ring
  on Odesa and one on the generated city, at 0.122 m and 0.172 m against a 0.1 m weld. Lines of different
  curvature will move that, either way, and so will having fewer of them — the fold already took 26 ribbons
  off Odesa's merge without closing it. Measure before and after; do not let a change in the count pass
  unremarked in either direction.
- **Lay time.** The walking graph is already nothing, so there is no slack bought by dropping it — the
  minute it used to cost was spent when the perimeter work emptied it. The spline search starts from
  today's figure, not from a discount.
- **The town stands still.** The chain in §6.6 has three links and breaking any one of them leaves a town
  with a road network and no traffic — which looks fine in a picture, passes every static test, and makes
  every dynamic reading vacuous at once. It is the cheapest failure to detect (`--bench maneuvers` entering
  nothing at all) and the easiest to mistake for "the catalogue is about things we removed", so **the
  reading to take first after step 7 of §12 is whether anything moved.**
- **The instruments lose their arrival reading, permanently and by construction.** The town watch's "what
  got where it was going" is walk arrivals plus bays parked in
  ([TownWatch.cs:320-322](src/bench/TownWatch.cs#L320-L322)), and after this both are nought on every town:
  no walking network, no bays. Its three claims are unaffected and stay the thing that gates a run. **Do not
  restore the reading by inventing an arrival this town does not have** — a car touring a lane arrives
  nowhere, and saying otherwise is a second answer. Either it reads nought and the census says why, or it
  is replaced by a reading about distance driven; that is a decision, listed in §11.
- **Seed churn.** Every generated town changes, both shipped cities included. There is no longer anything to
  hold still — the buildings and the car parks are gone, and the props move because §6.7 changes what
  decides a verge prop's kind. `qq tests maps` is the tier for that and it is asked deliberately.
- **The suite loses most of its maps at once.** Parking five maps takes the exam, the lap, the pad and the
  crossings with them — a large share of what the e2e and town tiers ask. What is left asks less of the
  build than it did, and that is the milestone's cost rather than a thing to be worked around.

---

## 10. Test intentions

- **Unit, on a generated town:** every connection point stands at the standoff from its node, on the
  perpendicular to its own drawn bearing. The same seed and link draw the same points twice — once in the
  generator and once at `Paving` time — which is the whole of what §5 rests on, and the case that catches
  §5's link identity is the one that derives them off a town read back rather than just laid. No
  connector joins two points of one link. Every connector's tightest radius clears what the junction's
  design speed affords. Every lane's two ends are exactly its two connection points.
- **Unit, on the spline:** a road's end tangents equal the bearings it was given, within a tolerance that is
  a figure and not a taste. Its tightest bend clears its class's floor. A strict district's roads are
  measurably straighter than a loose one's — one reading, against the parameter that drives it.
- **Town tier, over the generator's seeds (`GeneratorTests`):** no two roads share ground, no lane dangles,
  **the drivable region is one connected piece (GEN-5)**, and every junction is reachable from every
  movement. The connectivity case is the one §6.8 exists for and it is written before the laying it guards.
- **Town tier, that the town moves:** a generated city stands its brief's cars up on lanes and they are
  driving a minute later. **This is the case §6.6 exists for** and it is the one test in this plan that
  guards a chain rather than a construction — so it states the end of the chain, that cars moved, and not
  any of the three facts it runs through.
- **That what stands empty is inert and not broken** (§2.6's third list). One run of a generated town with
  the dormant slices on it, asserting that standing a registry, a roster and a crossing table over nothing
  raises nothing and costs nothing — **one case for the lot of them**, not one apiece, because what is being
  checked is that an empty subject is a supported state and not that any one type works.
- **What is no longer asked:** anything about a bay, a building, a kerb band, a pavement, a zebra, a bar or
  a signal on a generated town, and everything staged on a parked map. Deleted, and the absence reported by
  the census.
- **Visual, once:** a `--shot` of a city with `--caption`, and one with `--ui perimeter`, because a road
  network that reads wrong reads wrong immediately and no gate catches it. With the paint gone, the debug
  lane paths are what there is to look at.

---

## 11. Open decisions — handed to implementation

Settled already: the points live nowhere (§5), the maps are parked and `Test` becomes a brief (§8), the kerb
and the pavement come back off the boundary and not in this milestone (§6.7), parking and the buildings go
outright (§0), the cars are stood on lanes (§6.6), and the old road rules are obsolete rather than preserved
(§13). What is left:

1. **The standoff and the jitter bound.** 10 m and ±20° are the starting guesses and are stated as unproven.
   Both are `SimConfig` figures from the first commit; §6.9 fixes the direction the disc is derived in.
2. **The junction design speed.** `CityGen` carries a roundabout, a street and an arterial design speed.
   Whether a junction's connectors take the slower of the two arms', a figure of their own, or the
   roundabout's, is worth its line in the log.
3. **Where the spline's bounds are enforced in the pass order** — §6.8, recommendation given.
4. **Where the rounding and joining figures live** once the kerb band goes. They are figures about how open
   a joint may be and still be one line — nothing to do with kerbs but currently spelled on them
   (`Kerbs.RoundingM`, `.JoinedM`, `.OnePlaceM`). **The move is wider than the survivors suggest**: the
   merge, the mesh, the road stage, the debug overlay and the foot graph read them, and so do six test
   classes, which makes this a sweep of the suite as much as of the build.
5. **How many cars a brief's town stands, now that bays no longer bound it.** The count was clamped to the
   bays there were; on lanes the bound is a different one and wants stating rather than inheriting.
6. **What the town watch's arrival reading becomes** (§9): nought with the census saying why, or a reading
   about distance driven. Either is defensible; carrying on printing a figure that cannot move is not.
7. **The `Test` brief's own figures** — extent, districts, counts. One screen, one of every kind of ground,
   and fast enough that the unit tier does not notice it. "Furnished thinly" no longer means anything: there
   is no furniture, so what the brief authors is its extent, its districts, its water and its cars.
8. **Whether the kept idle ring stays on the shipped list** (§8), which decides what every probe sweeps.
9. **What `rescue`, `recovery` and `trips` do with no building to stage against** (§2.6). Not deleted —
   their subject returns — so the choice is between a probe that reports it could not stage, and one held
   out of the catalogue until the buildings are back. Whichever: **it may not read as a run that found
   nothing**, because that is indistinguishable from the fault it would be hiding.
10. **Lane count per arm.** GEN-15 says one standard width and as many lanes as ways. The connection-point
    model would let an arm carry more than one lane each way. Not this milestone, but the array shape should
    not preclude it.

---

## 12. Sequence

The order matters more than the estimate.

0. **Settle the tree first.** The unstaged perimeter work is in the way; commit it before a change this size.
1. **Take the readings that will be compared against.** `--bench census` and `qq town` on both shipped
   cities, the open-ring figures, and the lay time. They go into the decision log entry as it opens.
2. **Park the maps** (§8) — the plans, the probes, the watches, the catalogue entries, the menu rows and the
   tests, from §2.6's list. The cheapest way to shrink everything that follows, and the step that proves the
   inventory is a list to work from rather than a list to admire.
3. **The point-drawing function**, and its two-callers-one-answer test, including the read-back case. §5's
   seed plumbing and its link identity are both settled here or not at all.
4. **Delete** — §2.6's first two tables, in one commit that does not build, followed by the commits that
   stand the consumers up over an empty layer: the kerb band and the walk out of the ground answer, the
   paint out of the mesh, the slot stage and its stream out of the generator, the props' new sources.
   Delete the tests with the behaviour, not after it. **The build is expected to be red across this step and
   only this step**; every commit after it builds and runs its tier.

   **Then sweep for what the deletions orphaned rather than trusting the list.** A deletion of this size
   leaves a second ring of dead code behind it — the figure nothing reads, the helper with one caller that
   just went, the `using` of a namespace that is gone. The list was swept from the code as it stands today
   and cannot see that ring; finding it is a pass over the diff, and anything it turns up is added to §2.6
   rather than fixed silently, so the next person reads one list and not two.
5. **Stand the cars up** (§6.6) — the spawn stage onto lanes, the empty-map gate, and the case that says
   the town moves. It lands here rather than at the end **because it is what makes every later step
   observable**: from step 6 on, a run that produces no traffic is a fault in what was just laid rather than
   a milestone that has not got there yet.
6. **Connectors** — the junction's own lines, against the design-speed bound, classified as they are laid.
7. **The road spline** — the generator, its bounds, and the pass order §6.8 settles. The real uncertainty is
   here.
8. **Lanes along the road**, and `LaneLines` assembled from the three.
9. **`Test` as a brief**, once there is a generator worth staging checks on.
10. **Re-feed and measure.** The ribbons, the merge, the ground answer, the debug overlay. Compare against
    step 1, and take the traffic reading (§9) before the geometry ones.
11. **The documents** — §13, and `qq doclint` green including on this file (§13's last note).

---

## 13. What the documents owe

**The old city lane and road rules are obsolete, and obsolete is deleted.** They describe constructions that
will not exist, and a rule kept "for reference" is a second answer within a month:

- The chord rules GEN-12, GEN-12a and GEN-12b — the wander bound, the straight stub and the forkless sweep.
  Replaced by the spline's own bounds. **The stub's one surviving reader went with the slot stage**, so
  unlike the plan's earlier reading this is now a plain deletion with nothing to re-source first.
- The separation rule GEN-17 — two roads share no ground, stated as a chord separation and enforced by a
  deletion pass. Replaced by §4.4's third bound. The *property* survives; the rule as written does not.
  **It is cited in five files this plan does not otherwise touch** — the districts, the lattice, the
  roundabouts, the generator and a derived figure — and every one of those citations is part of the
  deletion.
- The dangling rule GEN-18a — no lane dangles, stated as a local fact about forkless nodes. Restated, if at
  all, about connection points.
- The fold, TER-5h, §6.4. Cited in two dozen places across the road slice, the census and the suite; the
  citation sweep is the work, not the rule.

**Kept and read again rather than rewritten:** TER-5d, which finally describes what the code does. TER-5d.1
and GEN-15 stand.

**Not deleted, because their subject is coming back:** the parking rules, TER-6's crossings, the signal
rules, the building rules, and the kerb and pavement rules TER-3c and below. Each absence is named in
[docs/index.md](docs/index.md#known-gaps) as held back for this milestone, with the parked maps and the
fixture's loss beside them.

**The sharpest of those is GEN-7, and it is not reworded.** It says cars start stopped in parking spaces and a
person starts inside a building, and after this both halves are false of a generated town — the cars stand
on lanes and there is nobody. It is the assistant's (`P5`) and could be argued into matching the code, which
is exactly why it is not: the code is the thing that is temporarily wrong here, and the gap entry is what
says so until parking and the buildings come back.

**The two `P0` rules are not touched** — TER-3c.8 and TER-7b are the owner's, they already describe a stack
the build does not draw, and this milestone widens that gap without rewording a word of either. The entry in
the known gaps grows; the rules do not move.

**New rules** for the point drawing, the connector bound and the spline's bounds, each carrying a rung.
**They are `P2`–`P9`**: nothing is argued upward into the owner's band, however load-bearing it feels.

**No ID is renumbered and no retired number is reused**, deletions included.

**And four documents outside the rules describe things that stop existing.** They are prose rather than code
and so are the easiest to leave dangling; each is on §2.6's terms, not softened:

- [docs/verification.md](docs/verification.md) — the tiers, the fixtures and the scenarios. It describes the
  exam, the walking exam and the crossings map at length, names `--bench exam` in its command list, and
  rests the whole of its fixture section on a `Towns.Fixture` that is about to be a brief (§8). **The
  largest documentation edit in the milestone**, and the one most likely to be forgotten because nothing
  cites it by ID.
- [readme.md](readme.md) — the probe list, the map table and the claims paragraph all name maps that are
  gone, and the map table is the only place a reader learns what this build can open.
- [citygen's requirements](src/citygen/docs/requirements.md), "The maps" — what a laid map is for, written
  about maps that are being deleted.
- [docs/index.md](docs/index.md) — the ID map loses the rows for the rules §13 deletes, and the known gaps
  grow by everything this milestone puts down. The gaps entry is the load-bearing half: **it is the only
  record that any of this is temporary**, and it names the parked maps, the fixture's loss, the absent
  layers, GEN-7's two false halves, and the service agents left with no base.

**`citygen/docs/decision-log.md`** — one entry, opened at step 1 with the readings in it. The entries it
supersedes are **deleted** rather than annotated: at minimum the four dated 2026-09-13 about the fold, the
cut, the movement's arc and the forkless node, and 2026-09-12's bay-band and lane-width entries.

**`qq doclint` must pass**: every ID the code cites has to resolve, and nothing may be stated twice.

**Including on this file, which is why the IDs above are no longer bold.** `doclint` reads a `**GEN-17**` at
the head of a line as a *statement* of that rule, wherever the file sits — so a plan written the obvious way
puts three rules on the tree twice and gives each of them no rung, and the check this section ends by
demanding fails on the section itself. Two ways to keep it green and both are fine: cite the IDs in prose as
above, or keep the plan out of what `doclint` reads. **Cite in prose**, because the citations should resolve
— that is half of what the check is for — and only the statement shape is the problem.
