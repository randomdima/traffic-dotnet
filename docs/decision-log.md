# Decision log — the cross-cutting decisions

Why the project as a whole is shaped this way. A decision belonging to one slice lives in that slice's
own log ([index.md](index.md)); only decisions still binding are here, and a superseded one is deleted
rather than annotated. Rules are [requirements.md](requirements.md); how a thing works is its XML docs.

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

## 2026-08-28 — the town's clock runs five times, and every acceleration in it has to know that

Walking pace is scaled by five, so an acceleration here carries a factor of twenty-five that no figure
stated — `FootGripMps2` was scaled and the sliding grip was not, which put `PER-23`'s casualty band under
walking pace. The second grip is now a share of the first, which is the only form the scaling cannot be
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
