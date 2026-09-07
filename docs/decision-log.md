# Decision log — the cross-cutting decisions

Why the project as a whole is shaped this way. A decision belonging to one slice lives in that slice's
own log ([index.md](index.md)); only decisions still binding are here, and a superseded one is deleted
rather than annotated. Rules are [requirements.md](requirements.md); how a thing works is its XML docs.

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
question put to it. Four rules in [verification.md](verification.md) now price a claim: derive once, one
run answers every claim about it, soak only where there is traffic, and a claim that something happened
ends its run.

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

## 2026-08-24 — the suite is four minutes' worth of question asked in forty seconds

`qq tests all` went from 2 m 50 s to 36 s with the same assertions passing: a town is ticked once per map
and every claim about that minute is read off the one run, `SolverCollection` is for what the machine
being busy could break and nothing else, and the gates are measured in Release.

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
belonged: the `.town` reader, `TownRenderer`, the plan's cell type, and the drawing kit that became
`app/screen/`. The lesson is general — **where a lower slice needs something a higher one has, hand over
the data and not the type.**

## 2026-08-17 — structure of arrays is pinned, and it is the one thing pinned

The requirements say what must be true of the physics and never which library provides it; this is the
single place narrowed, in every line: no reference type per body, agent, shape or contact. An
implementation holding a `Car` object per car has measured object-per-agent C#, which nobody was in doubt
about.

## Undated — the second gate that made the first one useless

`SIM-7`. A lit junction's phase table had already refused every conflicting movement and an exclusive box
claim was asked for on top, so the queue crossed on green in single file. Lifting the duplicate took
junction entries on a green from 44 to 93 and ticks spent stopped at a red from 6804 to 3909.
