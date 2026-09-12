# Perimeter extrusion as the town's one boundary construction — upgrade plan

Replace every separate placement of pavement, sidewalk, kerb line, outer line and parking wrap with one
construction: **the shell ring, extruded to a distance**. One ring set, one offset routine, one table of
distances, one signed distance for the answer.

---

## 0. Status — what landed, and what has not

**Every step of §9 landed and the `P0` conflicts of §4 were granted by the owner in as many words.** The
boundary is one construction; every line the town has is a distance off it; the picture and the answer read
one table; the machinery that guessed it is deleted; the rules and decision logs say so. `--bench shell`
reads the distances back on every shipped map, and the four gates — the allocation gate among them — pass.

**What has not landed is fidelity at the last one per cent.** Thirty-six tests fail that did not before
(thirteen others were already failing at `HEAD` and are nothing to do with this). They are three faults and
not thirty-six:

- ~~**The fold closure is walked and not solved.**~~ **Closed, and it was not the fault it looked like.**
  The gap a dropped fold leaves is now traced along the answer rather than straightened across, and the
  clearance a step is held to is sought along a line rather than jumped to — the jump could not converge
  where two pieces are equally near, which is the fold itself. Gaps a city closes with a bare straight:
  **11 657 → 818**, worst 24.4 m → 14.5 m ([core](src/core/docs/decision-log.md)). **But the readings it
  was meant to move barely moved** — worst pavement 8.20 m → 9.07 m off its figure, worst lane unchanged at
  4.19 m — because those come from the *keep* rule and not from the closure. The one worth fixing next is
  therefore the keep rule, and `Extrusion.Of` states the choice: the station's own reach (fails inside, a
  pavement on the road) against a clearance from every band (fails outside, a pavement on the grass). A
  probe cannot rank them; the walking-lane tier can, and has not been run.
- **A prop cleared against the boundary while the town was being laid is read against the finished one.**
  The generator remakes the ground as each stage adds its shapes, and the boundary of a half-laid town is
  not the boundary of the finished one. Five generator tests. The fix is to say when the boundary is
  settled, not to make the clearing more generous.
- **Three places where the town itself is wrong and the picture and the answer agree about it**: a
  boundary crossing water where no bridge carries it, a node nothing is turned through standing on
  pavement, and an exam card whose walk changed. Named in the known gaps.

**This file stays until those are closed.** What is settled is already in the requirements and the decision
logs that own it (§12); what is left is the list above.

### 0a. The staging that follows it

**Nothing places ground beside a road any more except the boundary.** Three leftovers outlived the steps of
§9 and are now deleted: a dead raster painter with its own verge, kerb fillet and mouth disc; the fillet and
slab pieces `Kerbs` answered for, which the boundary itself had never counted; and the pavement a bridge
carried across its own deck, which was the last line beside a road with arithmetic of its own. The
[citygen decision log](src/citygen/docs/decision-log.md) says why each went.

**And the lines off the kerb are held back behind one switch while the boundary is looked at.**
`RoadFigures.LinesOffTheKerbLaid` is off in the shipped figures, so a town is its driven ground and nothing
else: no kerb line, no pavement, no rim, no walking network, and the blocks the town encloses come back as
grass. **One switch, because there is now one construction** — hold the boundary's distances and every line
goes together, which is the plainest demonstration the upgrade has of what it was for. A town opens held
(`RunState.AgentsHeld`) so the ground is read without cars crossing it.

The switch is a stage and not a state of the engine: it is in
[docs/index.md](docs/index.md#known-gaps) as a gap against `TER-3c.3` and `TER-3d`, and it goes — with the
branches that read it and the held default — when the three faults above are closed.

---

## 1. What is there now

Four independent constructions of what is meant to be one boundary.

| Construction | Where | What it produces |
|---|---|---|
| **Piece union, distance-cut** | [Kerbs.cs](src/citygen/Kerbs.cs) — `Wrapping`, `Shell`, `Clear`, `Owns` | Every tarmac piece (band / fillet / box) offers its own offset line; each is walked at a quarter metre and cut to the spans no piece stands nearer to. Read at `walkM/2` by [Paving.cs:164](src/citygen/Paving.cs#L164) and at `bandM/2` by [FootGraph.Bands.cs:53](src/world/foot/FootGraph.Bands.cs#L53) |
| **Layered overdraw** | [GroundMesh.cs](src/app/render/GroundMesh.cs) — `Grown`, plus `Ribbon`/`Fillet`/`EndCap` | The same piece list drawn four times at four sizes; every boundary the picture has is a rim one fill left of the fill beneath it |
| **Point-in-piece** | [GroundShapes.cs](src/citygen/GroundShapes.cs) and its `.Roads`/`.Turns`/`.Paved`/`.Areas` parts | `At(point)` walks the same list backwards and takes the first shape covering it. Hot path: [TownWorld.Tyres.cs:43](src/world/town/TownWorld.Tyres.cs#L43), [TownWorld.Bodies.cs:178](src/world/town/TownWorld.Bodies.cs#L178) |
| **Shell + extrusion** | [LaneShell.cs](src/citygen/LaneShell.cs), [Extrusion.cs](src/core/geometry/Extrusion.cs) | The real topological boundary of the driven ground as closed rings, and the line that stands any distance to one side of a ring. Currently read by one debug layer ([DebugOverlay.Perimeter.cs](src/app/debug/DebugOverlay.Perimeter.cs), OBS-2p/2q) and by `--bench shell` |

The fourth is newer than the first three and strictly better founded. `--bench shell --map Odesa` today:

```
runs — 105 walked, 105 kept (103.87 km), 0 dropped
ends left over — none
lines the outside runs along and no kept ring carries — 0 of 10871, 0.00 km
```

The boundary closes, on every ring, on a full city. Nothing is left over. That is the licence for this plan.

### Why the first three are the problem

- **Three readings of one shape, kept in step by memory.** TER-7 states that the drawn ground and the
  answered ground are one geometry; what actually holds them together is that `GroundShapes.At` walks the
  same list `GroundMesh.Grown` draws, in reverse, and that both remember to include the same pieces.
  [render/docs/requirements.md](src/app/render/docs/requirements.md) has to say so in prose, twice.
- **The union's boundary is never computed, so it cannot be measured.** A rim is what overdraw leaves. It
  has no length, no arcs, no side and no name — nothing can ask whether the pavement is a walk wide, only
  whether it looks it.
- **The candidate-and-cut mechanism costs a walk per piece per offset and answers one offset at a time.**
  Every new distance is a new full walk of the town with a new `Func<Vector2,bool>` per station.
  `Kerbs.Clear` needs three separate roundings (`RoundingM`, `JoinedM`, `OnePlaceM`) and a tie-break
  (`Owns`) purely to make coincident candidates behave; `Paving.Corner.WeldTheEnds` exists to push apart
  ends that should have been one point.
- **Both edges of the pavement are offsets of one curve only by construction accident.** TER-3c.3 asks for
  that; what is drawn is two grown unions whose boundaries happen to be a line's width apart.

---

## 2. Goal

**One boundary, many distances.** After this:

- The town's driven ground has exactly one stated boundary — the shell's rings.
- Every line and every band in the ground layer is that boundary extruded to a distance out of one table.
- The ground *answer* at a point is one signed distance to that boundary, compared against the same table.
- No second construction of any of it exists, so nothing has to be kept in step with anything.

**Success is measurable, and these are the readings that say so:**

1. Every surface boundary drawn is a chain of arcs with a length, a side and a name — so the pavement's
   width can be measured rather than looked at.
2. `At(point)` and the picture agree by construction: both are the same distance compared against the same
   number, with one number per surface.
3. The distance table is authored on `SimConfig` and nowhere else; no behaviour or draw code carries a
   literal offset.
4. `qq tests all` stays inside its five minutes, and town lay time does not regress past the figure
   measured in step 0 below.

---

## 3. The idea in one page

**The shell ring is the ground's own zero.** Every other line the town has is that ring moved out or in by
a stated distance, and every question about what is under a point is *how far it stands off that ring*.

For the driven ground, the distance family is (names, not numbers — the figures are `SimConfig`'s):

| Distance | What stands there |
|---|---|
| `0` | the kerb: the edge of the carriageway |
| kerb-line width | the outer side of the kerb line stroke (TER-3d: struck **outside** the lane) |
| half a walk | the pedestrian lane's centreline |
| a walk less an edge line | the inner side of the pavement's outer rim |
| a walk | the outer edge of the pavement |

Two things follow, and they are the whole of the plan.

**Drawn:** each surface is one filled region per distance, drawn outermost first, exactly as the ground
layers are drawn today. A region at distance *d* is the interior of the outer ring extruded by *d* less the
interiors of the block rings extruded by *d* — a polygon with holes, which the ear-clipper already in
[GroundMesh.Shapes.cs](src/app/render/GroundMesh.Shapes.cs) (used for water) can cut. The rim-and-kerb-line
trick — a layer twice, a line's width apart — survives unchanged; what changes is that each layer is *one
shape* instead of a union of a thousand pieces, and its boundary is a line the build can hand to anyone.

**Answered:** `At(point)` becomes a signed distance to the ring set, compared against the table. Tarmac at
or inside zero, kerb line within the line's width, pavement within a walk, grass beyond. One distance, one
comparison per surface, in the order the table is written — which *is* TER-7's "one geometry", stated
instead of maintained. The machinery for a signed distance to a large ring set over a uniform grid already
exists twice: `Kerbs.ShardGrid` and `Extrusion.Stations`.

**The shell's own rounding is where the junction corner goes.** Today the tarmac's corner is a fillet —
`CityPlan.JunctionCorners`, an arc tangent to both carriageways (TER-5). The shell walks to the apex where
the two band edges cross, so its corner is sharp, and extruding it outward rounds it at radius = the
distance. That is the right shape for the pavement and the wrong shape for the tarmac: read raw, every
junction gains a triangle of tarmac it does not have now. **The fix belongs on the base ring**: splice each
junction's authored corner arc into the ring at the apex it belongs to, once, before any distance is taken.
Then the fillet is not a piece any more — it is a property of the zero ring, and every derived line inherits
it for free. TER-5's relation is unchanged; the authored figure is still the corner radius.

---

## 4. Two `P0` conflicts — reported, not resolved

Per [CLAUDE.md](CLAUDE.md): a `P0` found inconvenient is a conflict to report and stop on, not a rule to
restate in terms the new code can satisfy. **Both of these are the owner's to settle before the work they
gate is done.** Neither blocks the whole plan; each blocks one part of it, named below.

### Conflict A — the rule at [terrain/requirements.md:164](src/world/terrain/docs/requirements.md#L164), the owner's

*"The walk wraps the tarmac as one shape, and never a piece of it."*

The rule's *guarantee* is exactly what this plan delivers, and more directly: the pavement is the outline of
the union, and a station belongs to one line however many pieces are equally near. But the rule states that
guarantee **through a mechanism** — "every piece offers the line that stands half a walk outside *itself*,
and those lines are candidates" — and this plan deletes the mechanism. There would be no candidates, no
coincident lines, and nothing for `Owns` to break a tie between; the ring is one line because it was never
several.

**What is needed:** the owner's word on whether TER-3c.8 is the guarantee (in which case its second and
third sentences are a mechanism to be rewritten, and the rule keeps its number and its rung) or the
mechanism (in which case items 1 and 3 below do not proceed, and the shell stays a reading).

### Conflict B — the rule at [terrain/requirements.md:66](src/world/terrain/docs/requirements.md#L66), the owner's

*"A union is stated by drawing its pieces over one another."*

This one is a direct conflict. TER-7b says a layer is the union of the shapes in it, that overlap within a
layer is how a union is written down, and that **no piece is ever cut, trimmed, clipped or handed over
against its neighbour**. A filled region bounded by an extruded ring is a union that has been *computed* —
cut against its neighbours at every band crossing, by `LaneShell` — and then drawn as one shape. Item 2 and
item 3's fills cannot be done without changing that rule.

**What is needed:** the owner's word on whether the ground layer may be stated as a computed boundary. Note
what is given up if it may: overlap-as-union is why a junction, a car park's mouth, a bridge and a dead end
cost exactly what a straight costs today, and why no piece of the draw code knows what is beside it. The
shell buys the boundary by knowing *everything* that is beside everything — 16.8 s of it on Odesa at
present measurement, and a single unclosed ring is a town with no pavement at all (§8).

**What proceeds regardless:** items 1, 4 and 5's *lines* — the pedestrian lane, the kerb line as a stroke,
the bay wrap — are lines cut from the shell, not fills, and they answer to TER-3c.3/3c.5/3c.6/3d, all of
which are `P4`–`P6` and mine to restate with a decision-log entry. Item 2 and item 3's *fills* wait.

---

## 5. Options considered

### How the distances are taken

| Option | Trade |
|---|---|
| **A. Extrude the base ring once per distance** (recommended) | Each distance is an independent `Extrusion.Of` call over the ring. Honest: every line really stands its own distance out, and the fold rule is applied at each. Costs one walk of 104 km of ring per distance, five or six times. Rings at different distances have different corner counts, so nothing may assume they zip together |
| B. Extrude once at the largest distance, interpolate inwards | One walk. Wrong at every fold: the distance a fold closes at is a function of the distance, so an interpolated line is neither the offset nor inside it |
| C. Keep the shell's stations and offset every station by every distance in one pass | One walk, all distances. Saves the repeated arc sampling, and the fold grid over the base stations is built once. Worth doing **as an optimisation of A** if step 0's measurement says the repeated walks matter — not as a different construction |

**Recommend A, with C held as the optimisation if measurement calls for it.**

### How the fold rule sees other rings

Today `Extrusion.Of` is asked about one ring and keeps stations no nearer than the offset **to that ring**.
Two distinct rings' extrusions can in principle fold through each other — the town ring's pavement and a
block ring's pavement, where a town is narrower than two walks somewhere.

**Recommend:** let the fold rule be asked of the *ring set* rather than of the one ring being offset — one
station grid over every ring, built once, queried by all of them. It is a small change to `Extrusion.Of`'s
inputs, it removes a whole class of edge case, and it is what "no point of it stands nearer the ring than
the offset" means once there is more than one ring.

### How the ground answer is made

| Option | Trade |
|---|---|
| **D. Signed distance to the ring set, compared against the table** (recommended) | One number answers every surface, and it is provably the same number the drawing used. Needs a segment grid over the rings and a sign (inside/outside), both allocation-free and both already built once elsewhere in the tree. The hot path cost is nearest-segment within a cell — comparable to `Kerbs.ShardGrid` today |
| E. Point-in-region per distance, innermost wins | Needs a crossing-count index per distance and one query per surface. More index, more queries, same answer |
| F. Leave `GroundShapes` as it is | Cheapest now, and forfeits the goal: the picture and the answer go back to being two constructions kept in step by memory, which is the thing this plan exists to end |

**Recommend D.** It is the part of the plan that pays for itself: `Kerbs`, `GroundShapes.Roads`,
`GroundShapes.Turns`, `GroundShapes.Paved` and most of `GroundShapes.Areas` all collapse into it.

**Constraint that decides the implementation, not the plan:** `At` is called per wheel per tick
([TownWorld.Tyres.cs:43](src/world/town/TownWorld.Tyres.cs#L43)). The steady state allocates nothing. Whatever
index is built, it is built at lay time and queried with no allocation and no interface call the JIT cannot
devirtualise — [AllocationGateTests](src/tests/gates/AllocationGateTests.cs) already watches this.

### Where the junction corner comes from

| Option | Trade |
|---|---|
| **G. Splice the authored corner arc into the base ring** (recommended) | Keeps TER-5's arc tangent to both carriageways and the per-corner radius `CityPlan.JunctionCorners` already carries. Needs ring corners matched to plan corners — by the apex point, which both compute from the same band edges |
| H. Round every convex ring corner at one authored radius | Simpler, one figure, no matching. Loses the per-corner radius, and a corner between a wide arm and a narrow one gets the same round as one between two wide ones |
| I. Leave the ring sharp | Every junction gains a triangle of tarmac, and the corner a turning car is drawn for stops matching the corner it is given |

**Recommend G**, falling back to H only if the apex match turns out not to be reliable — which step 0 can
answer before the work starts, by counting ring corners that land on a plan corner.

---

## 6. The five items

### 1. Pedestrian lanes

Today: [FootGraph.Bands.cs](src/world/foot/FootGraph.Bands.cs) asks `Kerbs.Shell` at half a band, gets back a
heap of open runs, and the builder joins them up, drops the ones that lead nowhere, and welds their ends.

After: the pedestrian lane at half a walk **is a closed ring**. A cycle needs no joining, no
dead-end-dropping and no welding — the three hardest parts of the builder become nothing, along with
TER-3c.5's whole subject (a movement's line is pavement only where it leads somewhere: a ring has no ends,
so the question cannot be asked).

What genuinely remains:

- **The terrain veto.** A lane over water or off the map is not a lane however far it stands from the kerb.
  It is the one cut left, and it turns a ring back into runs where it bites — so the builder keeps its
  run-joining path for exactly this case and stops using it everywhere else.
- **Crossings.** `FootGraph.Mouth` walks out along a crossing's own square to the first metre clear of the
  tarmac. Under the new scheme that ray-walk becomes a solve: where the crossing's square meets the ring at
  half a walk. Cheaper, exact, and it removes `BisectionRounds` from this file.
- **Which side the road is on.** `PavedRun.RoadSide` is read off two probes either side of the line today
  ([Paving.cs:176](src/citygen/Paving.cs#L176)). A ring knows: the ground is on the walker's right
  throughout, on every ring. The probe goes away.

### 2. Carriageway, and its outline

Today: `Grown(..., outM: 0)` draws every road, movement, fillet and car park at its own size, and the
carriageway's outline is whatever that union leaves.

After: the region at distance zero, filled once. Its boundary is the base ring — a real line, with the
junction corners spliced in.

**This is the item TER-7b gates.** Until that is settled, what can be done is to *draw the base ring as a
stroke* over the existing fill and check that it lands on the boundary the overdraw leaves — which is the
cheapest possible proof that the two constructions agree, and the right first step regardless.

### 3. Sidewalk, and its outline

Today: two passes of `Grown` at `walkM` and `walkM - edgeM`, the rim between them being the outer edge line.

After: the region at a walk, then the region at a walk less an edge line, both filled, the rim between them
being the same outer edge line — with the difference that both edges are now offsets of one curve, which is
what TER-3c.3 asks for and has never been able to check.

Same gate as item 2.

### 4. Kerb, and outer kerb

Today: `Grown(..., outM: kerbM)` over the *movements only*, struck before the carriageway is drawn over it,
with a prose note that a car park is excluded because a kerb line has no business running up the far side of
one ([GroundMesh.cs:208](src/app/render/GroundMesh.cs#L208)).

After: the kerb line is the band between the ring at zero and the ring at a line's width — TER-3d's
"struck outside the lane", said as two distances instead of as a draw order. **The car park exclusion needs
its own answer**, because the shell does not distinguish a lane's stretch from a bay way's: the ring carries
both. The cleanest reading is that a stretch of ring knows which driven line it came from —
`LaneShell.Stretch` already records it — so the kerb line is the band cut to the stretches whose line is a
lane or a movement, and a bay way's stretches carry no stroke. That makes the exclusion a property of the
ring rather than a pass that is skipped, and it is the same cut GEN-4m wants for item 5.

The "outer kerb" — the pavement's outer edge against the grass — is item 3's rim. It is the same
construction at a different distance, which is the whole point.

### 5. Parking wrap

The good news first: **most of it is already the shell.** A car park is the union of the bay ways that reach
into it (GEN-4b), those ways are driven lines, and `LaneShell` walks them — 6934 of them on Odesa. The lot
box in `Kerbs.Lay` is `WalkedPast.Never` and offers no wrap at all. So there is no separate parking wrap to
replace; there is parking wrap that arrives for free and three loose ends:

- **The bay stroke (GEN-4m).** A bay paints the line it shares with its neighbour and nothing else, because
  the line round the outside of a row is the kerb line the pavement carries there. Under item 4 that
  sentence becomes true by construction rather than by a skipped pass: the outside of the row is the ring,
  the shared line is `GroundMesh.BayStrokes`, and the two cannot overlap.
- **The 15 m straights across a car park's back.** `--bench shell` counts 407 bay–bay cuts totalling 4236 m
  where the ring bridges ground nothing is driven along. Those are honest boundary — the back of a lot is
  cut ground — and everything derived from them inherits them. They want looking at in a picture before
  they are trusted as the edge of the pavement.
- **Lot slabs.** `CityPlan.PavedAreas` is empty for every generated town and non-empty only for authored
  track maps ([TrackPlan.cs](src/citygen/TrackPlan.cs)). It stays its own shape, drawn and answered as a box.
  Not worth a ring.

---

## 7. What is *not* derivable from the shell

Naming these is what keeps the plan honest — "everything is the same math" has five exceptions, and each
one is authored geometry rather than a boundary the driven ground has.

| Thing | Why it stays its own shape |
|---|---|
| **Bridge decks** | A deck's half-width and its pavement width are authored per bridge (`CityPlan.Bridges`); the parapet is not an offset of the carriageway the deck carries |
| **Water and shore** | Already closed rings (`CityPlan.RingArrays`) already drawn by nesting. Good news: the shore bands *are* extrusions of the water outline, so they can move onto `Extrusion` and share the machinery — a separate, smaller win, and one worth taking while the code is open |
| **Lot slabs** | Authored rectangles on track maps only |
| **Paint** | Dashes, zebras, stop bars and bay strokes are marks on a surface, not boundaries of one. `FirstMarkVertex` and TER-7's "paint sits on the surface it belongs to" are unaffected |
| **The terrain veto** | Water and off-map are the ground's own refusal, asked of the ground rather than of a distance |

---

## 8. Risks and failure modes

**The shell is all-or-nothing, and that is now the town's pavement.** `LaneShell.Chains` hands back the
rings that shut and nothing else — deliberately, because a perimeter in pieces is not one. Today that
costs a debug layer. After this plan it costs the pavement, the kerb, the pedestrian network and the ground
answer for the whole town. Odesa closes 105 of 105 rings with nothing left over, which is why this is
proposable at all; a map that closes 104 would have no sidewalks anywhere.

**This needs a stated policy before item 1 ships, and it is an open decision (§11).** The candidates: a map
that cannot close every ring fails its own claim and the run exits non-zero
([bench/](src/bench/), `--bench shell` already has the reading); or the shell degrades to per-ring, and a
town with a broken ring loses that ring's pavement only. The first is honest and brittle; the second needs
`Chains` to mean something it currently refuses to mean.

**The ring is not always on the ground's edge.** `--bench shell` reports 570 straights standing a metre or
more *inside* the driven ground, the worst at 3.45 m, 2038 m in total. Extrude one of those outward by half
a walk and the pedestrian lane is on the tarmac. Today that reading gates nothing. After this plan it is
load-bearing, and **"across" becomes a figure a map's claim watch should hold** rather than a number a probe
prints. The three worst are the 173° doubling-back corners at box lines 3264, 2111 and 2736 — a handful of
places, findable by coordinate, fixable in the ground.

**Meeting error is 538 mm at worst, over the 150 mm `MeetM` calls one place, 103 times.** Fine for a drawn
boundary. Whether it is fine for a boundary the physics answer is derived from is a question to ask before
item 2, not after.

**Lay time.** The shell took 16.8 s on Odesa in the measurement above (town lay included — step 0 separates
them). `Paving.Lay` already pays for the `Kerbs.Shell` walk at load, so some of this is a swap rather than
an addition, and `Kerbs` mostly disappears. But five or six extrusions of 104 km of ring is new work, and
e2e and soak tiers pay it per run. Measure before, measure after, and keep option C in reserve.

**Corner count drift.** Rings at different distances have different corner counts because the fold rule
drops different folds. Anything that assumes two rings zip together corner for corner is wrong. Fills must
be triangulated per ring, never between two.

**Smoothing pulls the line off its distance.** `Extrusion` smooths over a window and the line ends up about
`w²/24R` inside the offset on a bend. Millimetres at the debug layer's five metres and half-offset window.
At half a walk with a matched window it is smaller still — but the pavement's width is now something the
suite can measure, so the smoothing window becomes a figure that has to be chosen against a tolerance
rather than to taste.

---

## 9. Sequencing

**Step 0 — measure and prove, change nothing.** Separate the shell's lay time from the town's. Count the
ring corners that land on a `JunctionCorners` apex (decides G vs H). Draw the base ring as a stroke over the
existing carriageway fill in the debug layer and look at a `--shot` of a junction, a car park and a dead end:
if the ring is not on the boundary the overdraw leaves, nothing after this matters. Take the `--bench shell`
"across" and "met" readings on every shipped map, not just Odesa.

**Step 1 — the pedestrian lane off the ring** (item 1). It is the item with no `P0` in its way, the smallest
blast radius, and the loudest test: a closed walkable ring per block is either there or it is not.

**Step 2 — the distance table onto `SimConfig`**, and the kerb line and outer kerb as strokes cut from ring
stretches (item 4). Still lines, still no fills, still no `P0` touched.

**Step 3 — the signed distance answer** (option D), running *beside* `GroundShapes.At` and asserted equal
over a grid of the whole town on every shipped map, before it replaces anything.

**Step 4 — the fills** (items 2 and 3), and only if TER-7b has been settled.

**Step 5 — deletion.** `Kerbs`, most of `GroundShapes`, `Paving.Corner`, `FootGraph`'s joining path,
`GroundMesh.Grown` and the piece kinds under them. Prefer moving to deleting, and commit before each step.

Each step is independently shippable and independently revertable. No step needs the next one to be correct.

---

## 10. Test intentions

What must be proven, at the cheapest tier that can answer it
([docs/verification.md](docs/verification.md)) — one exact behaviour each, failing for nothing else (VER-12).

- **A ring's extrusion keeps its distance.** For a stated ring and a stated distance, no point of the
  result stands nearer the ring than the distance less the fold rounding. This is `Extrusion`'s own promise
  and the only thing the whole plan rests on. Unit tier, on a laboratory ring with a fold and a slit in it
  — [ExtrusionTests](src/tests/geometry/ExtrusionTests.cs) exists and is where this goes.
- **The band between two distances is that many metres wide.** Measured across, at stations along a ring,
  against the figures the table holds — not against the ring it was drawn from. This is the check TER-3c.3
  has never had.
- **The answer and the picture are one distance.** Over a grid across a fixture town, the surface the fill
  order puts at a point and the surface the distance table names are the same. It replaces
  [GroundLocatorTests](src/tests/world/GroundLocatorTests.cs)' agreement check with a stronger one, and it
  is the test that says TER-7 is now structural.
- **Every block has a walkable cycle.** One closed pedestrian lane per kept ring, on the fixture town —
  which is item 1's whole claim, and it fails loudly rather than degrading.
- **A junction corner is the radius it was authored at.** Measured off the base ring at a corner, against
  `JunctionCorners.RadiusM`, on the fixture. Proves the splice (option G).
- **Lay allocates freely; the tick allocates nothing.** The existing allocation gate covers the answer path
  once it changes; it must be run, not assumed.
- **Pictures.** A junction, a dead end, a car park mouth and a bridge approach, at street framing, with
  `--caption` — before and after, in `.tmp/`. The e2e tier exists for the ones a number cannot answer.

`qq tests --changed` after every edit; `qq tests unit town` before claiming any of the above works;
`qq tests all` before each step's commit; `qq tests maps` only when a shipped city is retuned.

---

## 11. Open decisions

Handed to implementation, or to the owner where marked.

1. **TER-3c.8 — guarantee or mechanism?** *(owner)* §4. Gates items 1 and 3's wording, not their code.
2. **TER-7b — may a ground layer be a computed boundary?** *(owner)* §4. Gates items 2 and 3 entirely.
3. **What a town with an unclosed ring is.** §8. A failed claim, or a town with one ring's pavement missing.
   This is a rule about what the town must be true of, so it wants stating as a requirement with a rung
   before the code assumes either — and it decides whether `LaneShell.Chains` keeps its current meaning.
4. **Whether "across" becomes a claim.** §8. If the pavement is read off the ring, a ring that runs inside
   the tarmac is a defect rather than a reading. Which map's watch in [bench/](src/bench/) holds it, and at
   what threshold, is a decision for whoever writes it.
5. **Where the distance table lives on `SimConfig`.** Authored in a nested group, derived on the root
   ([SimConfig.Derived.cs](src/core/config/SimConfig.Derived.cs)) — but whether the kerb-line and edge-line
   widths move into it or are read from `config.Road` where they already are is an implementation call.
6. **Whether the ring set index and the fold grid are one structure.** Both are a uniform grid over ring
   geometry, built once at lay; the signed-distance answer wants nearest-segment and the fold rule wants
   any-within-reach. Likely one index with two queries, but that is for whoever writes it to see.
7. **The smoothing window per distance.** §8. Zero for the fills (a fill wants its exact offset), non-zero
   for the debug layer (a whole town's worth must read as one line). Whether the pedestrian lane wants any
   is a question for a picture.
8. **Whether the water shore moves onto `Extrusion` in this work or after it.** §7. It is a real
   simplification and it is not on the critical path.

---

## 12. Documentation consequences

Not a list to write later — the point at which each is written is *the moment the thing is learned*.

- The rules TER-3c.3, TER-3c.5, TER-3c.6, TER-3d, TER-7 and TER-7a sit at rungs P4 to P6 and restate
  cleanly in terms of a ring and a distance. TER-3c.5's subject (a movement's line is pavement only where
  it leads somewhere) ceases to exist — a retired rule is deleted from the document and its number is
  never reused.
- The two rules of §4 sit at P0 and are not touched until §4 is answered.
- **[render/docs/requirements.md](src/app/render/docs/requirements.md)** loses most of its layering prose,
  because the layering stops being the thing that makes the answer right.
- **Decision logs** get the *why*: [citygen](src/citygen/docs/decision-log.md) for the shell becoming the
  town's boundary rather than a reading of it, [render](src/app/render/docs/decision-log.md) for the fills,
  [terrain](src/world/terrain/docs/decision-log.md) for the answer. A superseded decision is deleted from
  the log, not annotated.
- **`qq doclint` and `qq req`** are how every citation above is checked; nothing is grepped for.
- **This file is not a document of record.** Once §4 is answered and §9's steps land, what survives of it
  belongs in the requirements and the decision logs that own each part, and the file is deleted.
