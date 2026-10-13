# Decision log — the cross-cutting decisions

Why the project as a whole is shaped this way. A decision belonging to one slice lives in that slice's
own log ([index.md](index.md)); only decisions still binding are here, and a superseded one is deleted
rather than annotated. Rules are [requirements.md](requirements.md); how a thing works is its XML docs.

## 2026-10-14 — the world reads where a car is on its lane

**The owner (2026-10-14), in their words: "Wait cpu calculates where the car is on the lane? That was supposed to be done
by gpu … lets rework it so gpu gives back that instead. What lanes car touching and how its positioned along the lane.
So cpu needs to only decide what to do" … "Gpu should already know lane geometry, since ribbons are basically that just
2d".** Every tick the host projected each moving car's rear axle onto its line and sampled the line again for how far
off it stood — the trig of the car tick.

- **A car names what it follows and the world reads it there** (`LineFollowed`, `ControlAction.Follow`): the lane its
  line is on, the join off it and where the stretch stops, signalled when the line is laid, shifted or the car put
  somewhere. The world reads the rear axle along that stretch every step, on from where it read it last, and hands
  back the progress, the distance off and the follow's serial with the view (`CarView`). Chosen by the owner over every
  way under the axle: a junction mouth holds a lane and up to four joins starting at one point, and a list capped
  below that reads a car whose own join was dropped as off its line.
- **The ribbons' own centre lines are on the device** (`DeviceLanes`): the lanes' and joins' arcs, laid once. Chosen by
  the owner over reading the lattice, which holds a way and its metre in decimetres at half-metre points and nothing
  across a band — 5 cm along and nothing across, where the progress drives the speed profile and its covered metres
  are summed tick to tick.
- **To the bit**: the stretch is the line's own head (a lane laid into a line is its own arcs, the last cut,
  `Spline.HeadInto`), and the reading is the host's projection and sample over it (`LineReading`,
  `line_reading.glsl`). OdesaOsm's drive census came out the same to the digit with the world reading as without, and
  the device's reading is the managed world's to the bit.
- **The host still projects** a line with no lanes (a manoeuvre's pieces), a pass's aim, and the tick or two after a
  line is laid again before the world has read the new one.

OdesaOsm on the device: the frame 159 → 140 ms, the car tick 9.1 → 7.5 ms a tick, `atan2f` off the profile and
`sincosf` 4.6 → 1.5 ms a frame.

## 2026-10-13 — the CPU holds no geometry but the lanes its actors follow

**The owner (2026-10-13), in their words: "Cpu should not own geometry. Only minimal basic math" … "make sure we drop
all unused geometry. Only geometry we nees is lane path for actors tp follow. Nothing else".** It narrows the ruling
below: the host keeps the paths a car or a walker follows and the claims' topology over them, and nothing of the
ribbons, the ground, the furniture or the mesh once the device has them.

**And on what the actor layer still read the atlas for, in their words: "We dont need that. Actors follow lanes and
straigh to lane if offlane. Technically follow the lane should already cover this" … "Same for ground - they dont need
that" … "Same for grids and cells - there should be no lookups at all" … "Gpu should give actors their lane they are
on".** So the host does no spatial look-up of any
kind — no atlas, no ground lattice, no grid, cell or nearest-point index. An actor knows the lane or way it is on and
where along it, and asks the ground of the lanes it will drive or walk: a pass, a step across and a manoeuvre at a bay
ask stretches of the lanes and ways their path runs down, and an actor off every lane goes straight back to one. Where
a body stands is the world's to say: the device reads every body onto its ways and hands each actor the lanes it is on
with the view (TER-4c.2), and that is the only place an actor learns it.

**And where the line falls, in their words: "Player actions could defy the rules. Since they dont affect perf
meaningfully. Other only map build calculations are allowesld, no runtime".** So a look-up is the map's to make while it
is built — a door's way, a bay's lane, an exit spot, a spawn's lane — and a player's, for what a click or an order
names; nothing else asks one while the town runs. **And, in their words: "Special units are allowed to do minimal
calculations when they arrive"** — an ambulance, a tow, a patrol at its scene may work out where it puts a body down
or sets a wreck down, off the ways it and the scene stand on, at the moment it gets there.

What it came to (OdesaOsm on the device):
- **The actions ask the lanes they run down** (the car and walker logs): no sweep, no atlas, no swept tables.
- **An actor's place is the world's covers or a place it carries**: a car takes its lane off its wheels' covers, and
  one off every lane goes back to the lane it was driving; a walk is laid from the walker's last place on the pavement
  to a goal's place (`FootPlace`); a leg to a place in the road carries its lane and metre (`RoadPlace`), read off the
  body it is going to.
- **Everything a place needs is tabled while the map is built** (`TownWorld.Places`): each door's and bay's way in and
  the kerb beside every lane on the pavement, with which spots off each are clear of the furniture; the street beside
  each building and each stretch of pavement; each lane's bay lanes within a walk, and its buildings. A trip and a
  round are drawn down the road from where the actor is.
- **The host lets go of what the device has**: its atlas and furniture once the world is handed over, the ground's
  lattices and rings, the door grid. Heap 2.35 GB → 685 MB, resident 2.13 GB → 839 MB, the load's peak 5.1 → 3.4 GB;
  the frame 168 → 161 ms, the tick's own share outside the cars and the claims 38 → 8 ms.

## 2026-10-11 — the device runs the world, the CPU runs the actors

**The owner (2026-10-11), in their words: "Goal is changed for GPU work to be more limited. GPU is to run full physics
engine and calcuate collider to lane ribbon intersections. CPU is to run actor behavior and to send control signals to
GPU. Each layer owns its state and does not require resending it. Meaning GPU run next physics step and calculate ribbon
intersection without any CPU input ( by reusing its pref state buffer ). GPU will read control signal buffer each tick (
written by CPU actors ). It should contain minimal info - object id, action id, params. … CPU will read shallow world
state each tick ( dynamic object positon, angle, speed, lanes they intersect ). Static objects dont need to
communicated. CPU runs full claim logic ( without geometry ). And full actor behavior logic. GPU simulating the world
and running geometry calculations. … GPU renders its own state, no need for CPU to send actor info. We want
communication between layers to be minimal."** It supersedes every ruling of 2026-10-06 to 2026-10-11 that moved the
tick's decisions, the claims and the routes onto the device ([goals.md](goals.md)).

- **What went**: the fully resident town (`ResidentTown`, `TownState`, `TownRoot`, `DeviceRoutes`) and the hybrid's
  behaviour on the device — the car and walker ticks, their decisions, the switch look, the plans, the claims rebuild,
  and the mirror, shadows and visits' checks that kept the host's copy in step with them, down to the body table's own
  record of which rows the town changed between two steps. That path had grown to ~18 000 lines of host code and
  ~10 000 of kernels to keep two engines bit-equal, and was half-way.
- **The world layer** (`world/town/WorldLayer`) is the seam: control signals in, shallow state out, the touches the
  step began. The managed world (the browser head, the suite) and the device world (`app/devicetick/DeviceWorld`) are
  its two implementations, and the actor layer reads nothing else of a body.
- **Neither layer holds the other's state.** The town lays its bodies as plain data (`WorldLaying`: the furniture, each
  agent's pose and build), and each world stands its own body table off it. A signal names an agent by its roster index
  and never a body, so which row a body is is the world's alone. The town keeps only the furniture (`StaticBodies`), to
  find where a body may be put down; the world keeps what its tyres write on the ground. Handing the world a body table
  the town had filled instead left the town holding the managed world's live solver.
- **The world runs a step ahead of what the actors read** (`WorldLayer.StepsAhead`), at the owner's word: "implement
  processing pipeline, so they run concurrently — CPU actors dont usually change decisions each tick anyway". A tick
  waited on its step whole — a 12-car town took 3.9 ms a tick on the device against 0.02 managed, nearly all of it the
  submit's round trip — and the device idled through the decisions as the host did through the step. Now a step waits
  for the one begun the tick before and begins its own, so actors decide off a view a tick older. The managed world
  shows its answer as late, so the two worlds stay comparable step for step, and steps on a thread of its own the same
  way (`ManagedWorld`, `AsideThread`) — the same arithmetic in the same order, so a town steps to the bit as it would at
  once, and the browser's one thread steps at once; a body the town sets somewhere keeps the town's pose until a view
  that took the signal arrives.
- **The view is read where the world wrote it** (`WorldView.ReadOver`): the device writes each step's rows into one of
  two mapped buffers and the host reads them in place, the step in flight writing the other. A row carries what the
  actor layer reads every tick and nothing else — a body's covers' ranges come with the covers, on the claims' clock,
  and the step a car froze at is read off its flags (frozen, and not through the last step), since the town reads every
  view.
- **The tyres and a walker's footing are the world's**: they turn a car's wheel and pedals (`DriveCommand`) and a
  walker's heading and pace into impulses, so they are physics. Which ways a wheel grips on comes off the world's own
  reading of the ribbons.
- **The ribbons a body stands on are read by the world on the claims' clock** (`Sim.ClaimsIntervalS`, TER-49), which
  both layers read off the same figures — so it needs no signal. What the actor layer adds to a cover — whether the body
  travels that way, where its line goes next — is behaviour, and is the actor layer's.
- **What is static is laid once and never signalled** — the owner, in their words: "Car weight cannot change in runtime -
  we dont need mechanics for updates of a static properties". A body's mass and shape are its variant's, laid with the
  world; the figures panel's trims are surfaces' (`TrimFigures`), handed over whole when turned
  (`WorldLayer.FiguresChanged`).
- **Why the split falls there**, measured before the device took the tick (OdesaOsm, 10 013 cars and 10 000 walkers,
  `--bench town`, 2026-10-06): 23.5 ms a tick against a 16.7 ms budget — the claims 6.8, the cars' own tick 12.8 (of it
  the tyres), the solver 3.3. The solver and the tyres are the arithmetic the device is for; the claims and decisions are
  branching over sparse lists, which the device took 18 000 lines to keep equal to the host's.

## 2026-10-05 — an open's peak is what stands while the atlas is filed

**A page keeps the most it was ever given**: a browser never shrinks a WebAssembly heap, and a 32-bit one holds 4 GB
at most, so the open's peak is the whole of what the browser head is judged on. OdesaOsm's was 4.0 GB, and most of
it was not the town.

- **A coarser level of the grid was a finer one.** `GridLevel.Coarser` built the level above from its own cell as
  though it were the main cell, so from two halvings down a step coarser halved the cell again. An index refused at
  its cell cap (`ChainIndex.Seal`) walked down to the main level at the cell it started from: the driven ground's merge
  (`BandShell.Merge`) and each of the sea's outsets laid a table of 248 million cells, a gigabyte twice over at once.
- **The atlas's filing held what it worked in through the marks** (`RibbonAtlas.Filed`): every point's block and a
  table of each entry's place, a gigabyte and an eighth, stood under the marks' own working set. Filed in a method of
  its own, both are gone before the marks are laid, and a point's place rides in its entry's column until its cell is
  sorted, so the place table is gone altogether. The atlas is the same to the bit.
- **The world is laid before the ground** (`LaidTown.Lay`): the mesh's 171 MB stood under the atlas's peak, and laid
  after it, it is laid in what the atlas freed.
- **A grid of nothing covers one cell** (`BucketGrid.Over`): a car park's bays in a town with none were three words
  a cell of the whole town.
- **The atlas files the walk only where it is read**, and a piece reads only the rows it reaches, which are the roads
  slice's ([its log](../src/world/road/docs/decision-log.md)); **a corner of the ground is twelve bytes**, which is
  the renderer's ([its log](../src/app/render/docs/decision-log.md)).

Measured on OdesaOsm with `qq prof --bench load --memory`, the live peak bisected by the heap limit
(`DOTNET_GCHeapHardLimit`) the load still finishes under, the GC's committed heap fifteen seconds into a window, and
the published page's own heap a minute after the map was named:

| | before | after |
|---|---|---|
| peak resident | 4 011 MiB, the plan's | 1 998 MiB, the atlas's filing |
| live peak | 3.0 to 3.5 GB | under 1.75 GB |
| open | 15.7 s, 66 CPU s | 11.8 s, 43 CPU s |
| standing, committed | 2 046 MB | 1 419 MB |
| the page's heap | out of memory at its 2 GB | 2 387 MiB of 4 GB |

The coarsening moved the ground by five triangles of three million: the indexes it put right are coarser, and the
merge reads its candidates off a coarser cell.

**The build-time indexes are let go once the town stands** (`Paving.LetGoOfTheLayingIndexes`,
`RoadGraph.LetGoOfTheLayingIndex`, 2026-10-13, at the owner's "drop all unused geometry"), and laid again if asked: they
give a page nothing back, but a desktop's standing memory 60 MB. **Not done: the filing a block at a time**, letting
each go once filed: the entries are laid out
while every block still stands, so the peak is the same. A filing that keeps no points at all is a second run of the
geometry, and waits for that run to be cheaper.

## 2026-10-04 — a building may be one rounded rectangle

**The owner asked for a prefab building's collider to be a rounded rectangle** (`OBJ-2`): a traced town's buildings
are prefabs, each one rounded rectangle, drawn and collided as that — a car slides off a corner rather than catching
on it, and a silo is a disc. It is the solver's one shape with a radius, as a car's is (`PhysicsWorld.AddStaticBox`);
a building drawn of several rectangles keeps them square-cornered (`OBJ-5a`), and a generated town's roofs are radius
nought, so nothing they stand moves ([citygen's log](../src/citygen/docs/decision-log.md)).

## 2026-10-01 — a town hands back what opening it cost

**Rule 2 has a cost nobody had written down: a standing town never runs a full collection**, so whatever
the lay left behind stays committed for the whole run. Most of it is large-object arrays — the atlas's
working sets, the mesh's grown lists — which only a full collection frees and only a compacting one returns
to the machine. `Game.Stand` now ends with one aggressive, compacting collection, after the old town is let
go of and before the new one's first tick. A forced collection alone was measured first and is not enough:
it keeps what it freed committed and gives it back a little at each collection after it, of which a
standing town has none.

Measured with `qq prof --memory` on Odesa's brief at its own area (O1) and at ten times it, the people and
districts at Odesa's density (O10), a windowed run's twenty seconds after the warmup:

| | O1 before | O1 after | O10 before | O10 after |
|---|---|---|---|---|
| working set | 684 MiB | 375 MiB | 3 648 MiB | 1 987 MiB |
| GC committed | 532 MB | 189 MB | 3 860 MB | 1 781 MB |
| live heap | 191 MB | 191 MB | 1 807 MB | 1 807 MB |

**The open's peak is the lay's, and it was the atlas's**: the fleets, the claims and the bodies were laid on top
of the atlas's working sets, and those on top of what laying the networks left. A town of
`Sim.CollectTheLayAboveKm2` or more is collected either side of the atlas (`TownWorld.CollectTheLay`). Aggressive
here too, and for a second reason: a collection that keeps what it freed committed lays the next stage into reused
room the runtime clears page by page, so a table that is mostly room — the claims', the swept ground's — becomes
resident; on pages nothing has written it holds none of that room. Smaller towns are left alone, because the
suite lays one a test and a full collection each put a fifth on the town tier. Measured with `qq prof --bench load
--memory`, the open's peak resident set:

| | before | after |
|---|---|---|
| O10 | 2 460 MiB | 1 887 MiB, now the plan's own |
| O30, thirty times the area | 7 103 MiB | 5 881 MiB, the atlas's lay |

## 2026-09-27 — one grid under every index

The owner ruled it: **one grid indexes all geometry, and no other grid is allowed**; a level finer than the
main cell is taken only where it differs by at least a halving and there is a reason for it (`SIM-8`, at `P4`
until the owner says otherwise).

- **The main cell is 8 m, four car widths** (`Sim.GridCellInCarWidths`): the solver, the proximity index and
  the ground's road index were already there, and everything asked about a car or a street moved to it — the
  lanes' and pavements' nearest-line indexes (14.4 m), a car park's bays (a lot's diagonal), and the standing
  sprites' cull (32 m, now widened by the widest sprite's own reach rather than by half a cell).
- **Two authored levels under it, each for a reason it names**: the boundary's 4 m
  (`ShellCellsAcrossGridCell`; 2 m measured four times the memory for no speed, terrain log) and the ribbon
  atlas's 0.5 m (`RibbonPointsAcrossGridCell`; a quarter car is what holds a car to the lanes under it). A
  build-time search keyed to a tolerance — the foot graph's weld, the ring stringer's two, the merge's
  pieces, the generator's props and roads — takes the finest level that covers the tolerance and reads as
  many cells round as it needs.
- **Only halvings**, because then a point's cell on every level is one scaled coordinate shifted: the relation
  between two indexes' cells costs nothing and never disagrees at a boundary.
- **The atlas is kept a main cell at a time**, 16 × 16 points each with where each row begins, so a body reads
  a cell or two of contiguous memory rather than searching a town-long row per lattice row. The same points
  and entries (3 520 613 and 6 271 856 on Odesa) in 49.2 MiB rather than 54.1; on a running Odesa `UnderBox`
  went 0.69 → 0.43 ms a tick and the reservation rebuild 1.38 → 1.10 (`qq prof`).
- **What moved.** The road-spacing search reads the cells within the footprint and a station's half-step,
  where a 3 × 3 of footprint-wide cells could miss a pair whose stations fell far apart; Odesa lays
  identically (`--bench census`). The solver's window is capped with the rest held in its rim, where it used
  to coarsen its cell. Path marks stand on the 2 m level and barbs on the 4 m (1.5 m and 5 m before).
- **Not indexes, and left alone**: the mesh's weld key, a texture's period, and lattices that are the town's
  content rather than a way of finding it (a district's streets, the exam's cards).

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for the documentation audit.

- `OBJ-2`: four kinds, not five — a traffic light has no body, its heads being drawn and never collided.
- `SIM-6`: a lifted ban is performed by whatever performs the act elsewhere; the manoeuvres it named are gone.
- `VER-2`: one way round, a bay; the dead-end shunt it cited (`P-19`) is gone.

## 2026-09-20 — every town claims two things, and the clock is a leg's rather than a claim's

`VER-11` had every town claim that nothing stands still with no clock running for it. The claim went from
`TownWatch` with the driver's catalogue, when every leg of both agent kinds came to be bounded by one clock
(`LegProgress`, `AGT-7`), and `VER-11` is reworded to the two claims the watch holds.

## 2026-09-17 — a load is not a steady state, and tiering was tuned for the other one

A third of the time it took to open a map was spent running code the JIT had not optimised yet. Tiering
holds a method at tier 0 until the call-counting delay expires, and that delay restarts whenever any
method is called for the first time — so a load, which meets new methods continuously from the generator
through to the spawn, pushes the timer ahead of itself and finishes before anything is promoted. The
delay is set to nothing in [traffic-dotnet.csproj](../traffic-dotnet.csproj), where the reasoning and the
figures sit beside it. Quick JIT and Dynamic PGO stay on because both are faster than the alternatives
measured, and the tick does not move.

The general lesson is the one in the name: **every figure this project defends is a steady state, and
the load is the one thing it measures that is not.** A knob picked for the tick is not thereby right for
the open, and the two want reading separately.

## 2026-09-07 — every rule says whose it is, because a rule nobody asked for was arguing against one they did

Asked why the ground mesh draws a road four times over, the answer given cited a line in
[app/render](../src/app/render/docs/requirements.md) — *draw order is painter's work with nothing testing
what is underneath* — as though it settled the question. Nobody had ever asked for that. It was written by
the assistant to describe what the renderer already did, filed among the requirements, and then read back
as a rule with authority over the owner's own instruction that render geometry must not overlap. That is
the failure this scale exists to make impossible: **read back later, nothing distinguished a rule the owner
had asked for from one the assistant had inferred and then cited as though it had been handed down.**

So every rule now carries a rung after its ID ([priority.md](priority.md)), named on the same shape of
ladder [tests/Priority.cs](../src/tests/Priority.cs) already uses deliberately rather than given a second
alphabet to learn. **The existing 331 all took an assistant rung**, and that is the honest starting point:
the assistant wrote these documents and cannot now say which sentences began as the owner's, so claiming
any of them would be the same fabrication in the other direction.

The first `P0` is `TER-7b`, and the code did not meet it for as long as it demanded a partition — which is
the arrangement working: the rule stood as stated, the gap was named in [index.md](index.md#known-gaps),
and nothing was quietly reworded to match the renderer. What closed it was the owner changing the rule.

## 2026-09-07 — the suite gets five minutes, and what gives way is chosen from the bottom of a ladder

A suite with no budget grows until somebody stops running it, and then the tiers stop meaning anything.
`qq tests all` has five minutes and prints what it spent of them — a reading and never an assertion,
because a wall clock measures the machine as much as the suite, but the figure a new test is weighed
against.

Weighing needs a second axis, because a tier says what a question *costs* and nothing about what its answer
is *worth*: the cheapest class in the suite guards the solver and one of the dearest checked where a lamp
was drawn. So every class names a rung too ([tests/Priority.cs](../src/tests/Priority.cs)), and what is
dropped or made cheaper when the budget is spent comes off the bottom — the first three drops were a `P9`
lamp count, a `P6` glyph sweep and a drivability check `GeneratorTests` already made.

## 2026-09-07 — a shipped city is content, so the suite lays its own town and asks a city nothing

Every tier but `Unit` asked its questions of Odesa and River, which made the suite a function of whichever
maps the build happens to carry: `qq tests` was forty-eight seconds against a documented four, `all` was
six minutes and forty, and a second city at a second seed would have added both again for no new claim
about this engine. Three things already covered what a city could say — `GeneratorTests` over four seeds,
each laboratory map's own watch, and the fixture — so the cities were paying for coverage that existed.

The suite now owns two towns ([Towns](../src/tests/citygen/Towns.cs)): the fixture file, and `Towns.City`,
laid from a brief in that same class. `Tier.Maps` holds what is asked of a shipped city — the shallow bar,
drivability, the services it declares, a minute of it — and is outside `all`, run by name when a city is
added or retuned. The bar is one machine ([Conformance](../src/tests/citygen/Conformance.cs)) so the two
readers cannot disagree. `qq tests` is four seconds again and `all` is one minute forty.

`GeneratorTests` laid a fresh two-kilometre town per case — a hundred and nine of them for eight distinct
towns — which was three quarters of the tier run after every edit; it memoises per seed now and has moved
to `Town`, because eight towns is not engine-free arithmetic however well it is cached.

## 2026-09-07 — a test that names a figure to assert that figure is not a test

The audit of the suite found fifteen assertions comparing an output against the expression it was computed
from: the walker's impulse against grip × mass × dt, the amber phase against `AmberTailS`, a bay's way in
against half a car and a body, the ground catalogue against the coefficients handed to it. Each was
already forbidden by `VER-12`'s first shape and each had the same failure mode — it can only go red on the
day somebody retunes the figure deliberately, and on that day it is edited rather than read.

What replaced them is the relation the claim was actually about: the impulse *saturates*, so asking for
ten times as much buys nothing; the ground scales pace and grip *by the same factor*; the tow arm's inset
is *the same* at every variant and both ends; a bay's stand-off is *the same* at every bay. `count > 0`
over a driven minute went the same way where it guarded nothing, since a census goes red when the town is
given room rather than when the rule breaks — and a soak's subject is now staged rather than waited for.

## 2026-09-07 — the purpose was written twice, and the copy that nothing cites is the one that goes

`PUR-1`…`PUR-4` restated [goals.md](goals.md) sentence for sentence — the quality bar verbatim — and
`AGT-1`…`AGT-4`, `OBJ-1`, `OBJ-3`, `SIM-5` and `TEC-3` were definitions, pointers to other rules, or a
constraint on a grid the terrain has never had. Nothing but this page's own ID map named any of them, so
what a reader loses is a second copy that could disagree with the first. `VER-4`, `VER-5` and `VER-7` are
retired with them, as intentions no tier answers and that `VER-3`, `SIM-4` and `SOL-35` already carry.
Retired: `PUR-1`…`PUR-4`, `AGT-1`…`AGT-4`, `OBJ-1`, `OBJ-3`, `SIM-5`, `TEC-3`, `VER-4`, `VER-5`, `VER-7`.

## 2026-08-29 — six more figures were relations, and four of them said so themselves

Six authored numbers were arithmetic, and the tell was each one's own doc comment explaining what it was
*half of* or *five times*. `Person.PaceScale`, `StopsWithinDiameters` and `Car.BrakePedalInTyreGrips` are
now the authored terms and the rest derive. Three figures that state no relation stay authored.

## 2026-08-28 — what a claim costs is the run it needs, and the suite had stopped asking

The town tier was eighty-three seconds because the same minute of the same town was re-driven for every
question put to it; four days earlier the same fault had `qq tests all` at 2 m 50 s for what one run per
map answered in 36 s. Four rules in [verification.md](verification.md) now price a claim: derive once, one
run answers every claim about it, soak only where there is traffic, and a claim that something happened
ends its run. `SolverCollection` is for what the machine being busy could break and nothing else, and the
gates are measured in Release.

## 2026-08-28 — a map states what it claims, and the panel, the probe and the tier read one machine

Three measuring maps each reported differently, so whether a run was right lived in whichever reader
happened to be looking. A map now carries its claims and one watch answers them (`VER-11`,
`Bench.ScenarioWatch`): every claim is `waiting`, `kept` or `BROKEN`, and a broken one is the exit code.

## 2026-08-28 — every acceleration in the person model carries the square of its pace scale

Walking pace is scaled (`Person.PaceScale`), so an acceleration here carries the square of the scale where no
figure stated it — `FootGripMps2` was scaled and the sliding grip was not, which put `PER-23`'s casualty band
under walking pace. The second grip is now a share of the first, which is the only form the scaling cannot be
forgotten in.

## 2026-08-26 — nobody in this town dies, and the band that used to kill is where a body starts moving

`PHY-3`'s death band is gone; a contact puts a person in the road (`PER-18`) and the rescue takes it from
there. It was the one state in the town with no way out of it, and at the shipped figures nothing ever
reached either old band. `PER-12` and `PER-12a` are retired.

## 2026-08-26 — a building is stood as its picture and not as its plot

`OBJ-5a`. A roof drawn as a rectangle is painted as an L or a U, so up to sixty per cent of a building's
perimeter carried empty box in front of the wall. The answer is more rectangles rather than a new shape —
statics are never integrated (`SOL-22`) and their grid is built once (`SOL-21`) — and which roof a
building wears is `world/statics/BuildingRoofs`, not the renderer's.

## 2026-08-20 — the code moved under `src/`, and the project file did not

The root listed nine code folders, three data folders and two build folders as equals, so `bin/` read as
a slice. The nine are now under `src/` and nothing else is; `traffic-dotnet.csproj` and `.slnx` stayed at
the root, which keeps the build's output visibly apart and leaves `ProjectPaths` finding the root by
`assets/` and `towns/`.

## 2026-08-20 — the assets are JSON, and the figures are still not `IOptions`

An INI of `[section]` over `Key = value` was picked because it made a 135-file conversion diffable, and it
did not survive its own reader. `assets/` is JSON, source-generated per slice, with a path relative to its
own file's folder. `SimConfig` is not bound by `Microsoft.Extensions.Configuration`: `ConfigurationBinder`
skips a get-only derived figure without a word, leaving the author believing the override took.

## 2026-08-20 — the asset files became the project's own, and stopped being a second engine's

Godot's `.tres`, a `.uid` beside 229 C# files and a `.import` beside 178 pictures described art nothing
here read, pointing at an import cache this repository never had. All gone, along with four copies of a
`res://` resolver. `hullM` and `handling` were kept: authored per-car data is expensive to make and cheap
to hold.

## 2026-08-20 — the project stands alone, and the two rules outlived the reason for them

Both rules came from a four-way engine comparison, where a figure taken across a GC pause is one nobody
can quote. Standing alone, they stopped being a measurement protocol and became what the thing is, and
both have a gate in [tests/gates/](../src/tests/gates/) rather than a habit. The requirement IDs were kept
verbatim because the code cites them.

## 2026-08-20 — the tiers were audited, and four slices were in the wrong place

One pass over the imports found four files put where they were first needed rather than where they
belonged — `TownRenderer`, the plan's cell type, the drawing kit that became `app/screen/`, and a reader
since deleted. The lesson became [CLAUDE.md](../CLAUDE.md#everything-is-a-vertical-slice)'s: **hand over the
data and not the type.**

## 2026-08-17 — structure of arrays is pinned, and it is the one thing pinned

The requirements say what must be true of the physics and never which library provides it; this is the
single place narrowed, in every line: no reference type per body, agent, shape or contact. An
implementation holding a `Car` object per car has measured object-per-agent C#, which nobody was in doubt
about.

## Undated — the second gate that made the first one useless

`SIM-7`. A lit junction's phase table had already refused every conflicting movement and an exclusive box
claim was asked for on top, so the queue crossed on green in single file. Lifting the duplicate took
junction entries on a green from 44 to 93 and ticks spent stopped at a red from 6804 to 3909.
