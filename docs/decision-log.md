# Decision log — the cross-cutting decisions

Why the project as a whole is shaped this way. **A decision belonging to one slice lives in that slice's
own log** — [index.md](index.md) lists them — and **only decisions still binding are here**: a superseded
one is deleted, not annotated.

Nothing here restates a rule. Rules are [requirements.md](requirements.md); how a thing works is the XML
docs on the type that does it.

## 2026-08-20 — the code moved under `src/`, and the project file did not

The root listed nine code folders, three data folders and two build folders as equals, so `bin/` and
`obj/` read as slices at a glance and a slice read as output. The nine — `core`, `citygen`, `world`,
`agents`, `runtime`, `app`, `bench`, `tests`, `tools` — are now under `src/`, and nothing else is: what
a person writes and what a build writes no longer share a folder.

**`traffic-dotnet.csproj` and `traffic-dotnet.slnx` stayed at the root**, which is what keeps `bin/` and
`obj/` there and visibly apart from the source. It also leaves `assets/` and `towns/` where they were,
so `ProjectPaths` still finds the root by the pair of them and every path inside an asset file is
unchanged.

The tiers, the dependency directions and the mirror between `src/`, `assets/` and `src/tests/` are
untouched — this moved the tree, not the slices. Documents name a slice the short way (`core/`, not
`src/core/`) and spell `src/` out only when the path is one a person would type.

## 2026-08-20 — the assets are JSON, and the figures are still not `IOptions`

Two formats were on the table once the Godot syntax came out, and the first answer was wrong. An INI of
`[section]` over `Key = value` was picked because `.tres` already *is* that shape, which made a
135-file conversion diffable line for line — a good reason to migrate that way and not a reason to stay.
It did not survive its own reader: a catalogue's entries had to be a *repeated key*, a footprint a string
hand-split on a comma, and a hull forty tokens on one line.

`assets/` is JSON, read by `System.Text.Json` with a **source-generated** context per slice, so there is
no reflection in it and no package beyond the framework. A path inside a file is now relative to
**that file's own folder** rather than to the project root, which is what makes a variant folder a thing
that can be moved: it names its own art. Every key a file carries is declared on the record that reads
it and an unmapped member is refused, so a misspelling fails the load instead of quietly taking a
default.

**`SimConfig` is not bound by `Microsoft.Extensions.Configuration`, and this is deliberate.** The
authored/derived split is enforced by *refusing* an override of a derived figure, and a derived figure is
a get-only property; `ConfigurationBinder` skips a property it cannot write without a word, leaving the
author believing the override took. `ErrorOnUnknownConfiguration` does not reach it, because the key does
match a property — just not a writable one. `IOptions<T>` is a container lifetime abstraction and there
is no container here, so it would add an interface call on the path that reads a figure and subscribe
nobody to the change token. The fifty reflective lines in `src/core/config/SharedFiguresReader.cs` buy a
guarantee the framework binder cannot express, and they run once, at startup.

## 2026-08-20 — the asset files became the project's own, and stopped being a second engine's

The town's art was still described in Godot's `.tres`, and the tree still carried that engine's
bookkeeping: a `.uid` beside every one of 229 C# files, a `.import` beside every one of 178 pictures, and
in every resource a `script` binding naming a class under `res://Engines/godot-dotnet/`. None of it was
read by anything here. The sidecars pointed at an import cache this repository has never had, and the
script bindings at a tree the split deleted.

They are gone, and the descriptions with them: `assets/` is now this project's own, in the format the
next entry settles. What went with the syntax is four copies of a `res://` resolver and the regexes each
catalogue carried to scrape `ExtResource(…)` and `Vector2(…)` out of a file. Two things that were
positional became named: a catalogue lists its entries rather than relying on declaration order, and a
car's wreck art is a named `wreck` rather than *the second texture the file happens to mention*.

Two Godot mentions are deliberate and stay. [goals.md](goals.md) names the engine among what this project
refuses, which is the point of the sentence. And `src/tests/e2e/expected/` still holds the frames the
godot-dotnet build took: [verification.md](verification.md) already says they are a reference and never a
gate, and a reference from an implementation that is not this one is exactly what the split kept.

The car variants carry `hullM` and a `handling` group, which the other engine read and this one does not.
They were kept rather than dropped: authored per-car data is expensive to make and cheap to hold, and a
hull is a real fact about a car whether or not the physics asks for it yet.

## 2026-08-20 — the project stands alone, and the two rules outlived the reason for them

It began as one cell of a four-way comparison: the same town implemented with and without a game engine
under it, in C# and in C, so that the two variables could be told apart. That is where **the frame's
crossing count is O(1)** and **the steady state allocates nothing** came from — a figure taken across a
GC pause is a figure nobody can quote, and a frame making one call per car measures the boundary rather
than the simulation.

Standing alone, the comparison is gone and the rules are not. They stopped being a measurement protocol
and became what the thing *is*: a managed simulation that does not pay the costs managed simulations are
assumed to pay. Both now have a gate in [tests/gates/](../src/tests/gates/) rather than a habit, which is the
only form a rule of this kind survives in.

What went with the split: the parity tables, the cross-engine figures, and every document about an
implementation that is not this one. What did not: the requirement IDs, which are kept verbatim because
the code cites them.

## 2026-08-20 — the tiers were audited, and four slices were in the wrong place

The tree was laid as vertical slices from the start, but nothing had ever checked which way the arrows
pointed. One pass over the imports found four breaks, and each was a file that had been put where it was
first needed rather than where it belonged:

- **`core/` held the `.town` reader**, so the kernel knew what a junction was. The reader moved to
  `citygen/`, which owns the structure it produces; `core/persistence/` kept the cursor that walks bytes.
- **`runtime/` held `TownRenderer`**, so the machine reached up into the shell and, through it,
  transitively into the whole world. It moved to `app/render/`, and `runtime/` went back to being the
  window and the device.
- **`citygen` and `world/terrain` pointed at each other**, because the plan took its cell type from the
  terrain slice. The enum is the plan's vocabulary and moved down; what a kind *permits* stayed up.
- **`app/hud` and `app/debug` pointed at each other**, because the drawing kit lived inside the panel and
  the switches lived there too. The kit became `app/screen/`, which knows nothing and which both read;
  the switches and the frame read-out went to the layers that own them.

Three seams came out of it, and they are the general lesson: **where a lower slice needs something a
higher one has, hand over the data and not the type.** `ExitSpots` takes spans instead of a fleet,
`SheetFrameAspect` takes the grid instead of reading a catalogue, and the plan's reader bounds a cell
against the plan's own vocabulary. All 698 tests passed before and after, which is what says the moves
were moves.

Two smells worth knowing, because both hid a real edge from a real audit: **a fully-qualified name used
instead of a `using`**, and **a stale `using` nobody removed**.

## 2026-08-17 — structure of arrays is pinned, and it is the one thing pinned

The requirements deliberately leave the machinery open — they say what must be true of the physics, never
which library provides it. **This is the single place that is narrowed, in every line of code: no
reference type per body, per agent, per shape or per contact.**

An implementation holding a `Car` object per car and then reporting what C# costs has measured
object-per-agent C#, which nobody was in doubt about.

Everything else — the renderer's API, the window, the typeface, the test runner — follows from the two
rules and is argued where it is declared: the project file for the dependencies, the slice's own log for
the rest.

## Undated — the second gate that made the first one useless

`SIM-7` reads the way it does because of one measurement. A lit junction's phase table had already
refused every conflicting movement, and an exclusive box claim was asked for on top of it. The queue
crossed on green **in single file**, each car braking to the bar until the one in front was fully out.

Lifting the duplicate and nothing else: junction entries on a green 44 → 93, crossings passed lawfully
50 → 109, ticks spent stopped at a red 6804 → 3909, ticks standing on a zebra 100 → 0, with unlawful
crossings and wrecks both unchanged at zero.

**Before adding a check that refuses a movement, name what has already refused it.**
