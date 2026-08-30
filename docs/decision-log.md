# Decision log — the cross-cutting decisions

Why the project as a whole is shaped this way. **A decision belonging to one slice lives in that slice's
own log** — [index.md](index.md) lists them — and **only decisions still binding are here**: a superseded
one is deleted, not annotated.

Nothing here restates a rule. Rules are [requirements.md](requirements.md); how a thing works is the XML
docs on the type that does it.

## 2026-08-29 — six more figures were relations, and four of them said so themselves

**Swept every authored group for numbers that were really arithmetic.** Six came out, and the tell was
usually the figure's own doc comment: a comment that explains what a constant is *half of*, or *five times*,
or *whatever keeps* something inside something else, is a comment standing in for a derivation nobody wrote.

- **The person model's pace scale.** `WalkSpeedMps` 6.6 and `TurnRateDegPerS` 1350 were both "five times a
  real one" in prose and neither said so in a figure. Now `Person.RealWalkSpeedMps` 1.32, `RealPivotDegPerS`
  270 and **`PaceScale` 5** are authored and both paces derive. The scale was already load-bearing and
  unstated — every acceleration in the person model carries its *square*, which is the trap that once put
  the casualty band under walking speed.
- **`FootGripMps2` 110** was documented as "the relation, not the number: whatever the walk speed is, the
  grip is what keeps a stop inside a fifth of the body's own diameter" — and then authored as a number
  anyway, with a test checking it had not drifted. The fifth is now `Person.StopsWithinDiameters` and the
  grip is derived: 108.9 m/s², a per cent off what had been hand-held.
- **`PavementCornerRadiusM` 2** was "half the walk" beside a 4 m walk.
- **`Car.BrakingMps2` 27** was a deceleration whose entire purpose was to stand clear of the tyres. Now
  `Car.BrakePedalInTyreGrips` 3 — a pedal that can lock a wheel at any load, which is what brakes are — so
  it **tracks the rubber** instead of sitting where it was put. It had already gone stale once: the grip
  moved under it this morning and it did not follow.
- **`Evacuator.HitchMostMps2` 25** was described as "four *g*" and was 2.55 of them. In grips the prose
  cannot drift from the figure again; the figure is kept and the prose corrected, because the number is
  what the tow was measured with and the sentence is what nobody checked.

**Three were left authored on purpose.** `Tyre.TreadPitchM` is the shipped picture's own period and belongs
to the art. `View.ArtPixelsPerMetre`
63 is 21 × 3, but both halves are facts about a sprite sheet rather than terms in a model.
`Driving.WheelTravelS` 0.641 is suspiciously precise and states no relation, so it is flagged and not guessed at.

## 2026-08-28 — what a claim costs is the run it needs, and the suite had stopped asking

The town tier had grown to eighty-three seconds and no single test was to blame. Measured, five per cent
of the cases held ninety per cent of the time, and the two shipped cities held two thirds of it on their
own: fourteen hundred cases came to under two seconds between them while a sweep of one invariant over
every shipped map came to forty-three. Nothing was slow — the same minute of the same town was being
driven again for each question put to it, and the questions had multiplied.

Four things now decide what a claim is allowed to cost, and they are
[verification.md](verification.md)'s.

- **Anything derived from a plan is built once.** The road graph, the ways at the bays, the pavement's
  graph and the ground's triangles are functions of a plan and the figures, and a hundred cases were
  rebuilding the same eight of each. It is the shape `Towns` already used for plans, carried on.
- **One run answers every claim about it.** Where two claims want two different moments, both are taken
  off the one run as it passes them — `WalkedLineTests` reads the shape of a line ten seconds in and the
  arrivals a minute in, off one minute. It is `JunctionClaimTests`'s pattern, which was the only place
  using it.
- **A soak is driven only where there is traffic.** Minutes of driving go to the cities and the fixture;
  a claim answered off a plan, a graph or a town at rest goes on being asked of every shipped map, which
  is where a small map is worth its tenth of a second.
- **A claim that something happened ends the run that shows it.** The tick count is the bound on how long
  a town may honestly take, not a window to watch to the end of. A bound, a maximum or a count that must
  stay zero has no such end and runs its length.

**Two tests went, and for the same reason as each other.** `RecoveryEndToEndTests` swept a staged recovery
over every shipped map and returned before asserting anything on seven of them — no city hitches a wreck
inside the probe's four hundred seconds — so it spent thirty-nine seconds a run producing rows it threw
away; what a city's recovery comes to is `--bench recovery`'s row and the claim under it. And
`SimConfigTests` quoted eleven figures back at themselves, under two names that promised a document this
project does not have: a test that repeats a number fails on the day the town is tuned and passes on
every other, whatever the arithmetic between the numbers is doing.

`qq tests all` is a minute and a quarter where it was two and a bit, and the tier table in
[CLAUDE.md](../CLAUDE.md) is the measured figures again — it had been quoting numbers about a third of
the truth, which is the way a table of costs stops being read.

## 2026-08-28 — a map states what it claims, and the panel, the probe and the tier read one machine

The maps laid to measure one thing each had a different way of saying how they had done. `Track` drew a
table of figures nobody had to decide anything about; `Exam` printed a verdict a card, but only under
`--bench`, and opening `--map Exam` staged nothing at all; `Zebras` did neither, and the claim its own
name makes — a car crosses paint at a crossing pace — was asserted on two other maps and never on it. A
run of any of them said what it *did* and never whether that was right, so the answer lived in whichever
of the three readers happened to be looking.

**A map now carries its claims and one watch answers them** (`VER-11`, `Bench.ScenarioWatch`). The status
panel's claims section (`OBS-2i`), the table a headless run prints on its way out and the tier that asserts
on that map read the same watch, and what differs between them is only how long the town has been watched.
A broken claim is a failed run, and the exit code is that line.

Three things fell out of it, and the second is the one worth having.

- **The thresholds moved out of the tests and into the map's own watch.** What a corner affords, what a
  slowing may differ from the plan by, how few passes a mean is worth taking over: they were consts in
  `TrackFiguresTests`, which meant a player watching the lap and a suite asserting on it could not
  possibly have disagreed — because only one of them was asking.
- **The distinction between a claim and a reading became a thing the code carries.** It was already the
  project's rule and was kept by hand: `--bench drunk` prints swerves nobody may assert on. A watch has
  claims and readings as separate lists, a probe that gates nothing says so where it is listed, and the
  drunks' lap now claims one thing and quotes the rest rather than claiming the pacing lap's answer of a
  road nobody is driving.
- **A claim nobody has answered is not a pass.** Every claim is `waiting`, `kept` or `BROKEN`, the last
  line of a report carries all three counts, and the tier fails a claim its own run left waiting.

**A scenario may stage what it is about.** The exam already did — thirty-six orders on the first tick —
and the crossings map now does the same with its five walkers, because a body left to itself takes a
crossing only by luck. What is claimed is what the town did with the order, never that it was given one,
and the staging is the watch's rather than the probe's: opening the map in the game stages it exactly as
`--bench` does, which is what makes the panel worth looking at.

## 2026-08-28 — the town's clock runs five times, and every acceleration in it has to know that

`Person.WalkSpeedMps` is 6.6 — about five times a real walk, because the town is watched at speed. The
distances are real, so an acceleration in this town carries a factor of **twenty-five** that no figure
states: `a = v²/2d`, and only `v` was scaled. `FootGripMps2` was authored that way and the sliding grip
was not, so the two lived side by side at scales a factor of twenty-five apart.

**It was the casualty band that made it visible.** `PER-23`'s tolerance is the work of sliding a body
half a metre on the sliding grip, so an unscaled grip put the band at 2 m/s of closing speed — a third of
the town's own walking pace. A pedestrian did not have to be struck by anything: reaching a parked car
was ten times the energy needed, and the contact *was* the knock-down. The same figure had casualties
skidding fifty metres from a strike that should carry them two.

**The second grip is now a share of the first**, which is the only form in which the scaling cannot be
applied to one and forgotten on the other. The band lands half again over walking pace, so it takes a
vehicle actually carrying speed — and every strike at road pace is still far over it, which is what the
rescue slice needs.

The lesson generalises past this one figure: **a real-world number is not portable into this town unless
its dimensions are checked against the pace.** Speeds scale by five, accelerations by twenty-five,
energies by twenty-five, and distances not at all.

## 2026-08-26 — nobody in this town dies, and the band that used to kill is where a body starts moving

`PHY-3` had a person carrying two tolerances where every other kind of body carries one, so a pedestrian
saw three bands and everything else saw two. The top band was death: terminal, permanent, and the one
state in the town with no way out of it. It is gone. **A contact does one thing to a person — puts them
in the road — and the rescue takes it from there** (`PER-18`).

Three things fall out, and the third is the reason it was worth doing.

- **The arithmetic lost its only special case.** One tolerance per kind of body, two bands per pairing,
  and `DamageOutcome` is `None`, `Wounded`, `Broken`. The exhaustive table next to it lost a fifth of its
  rows and none of its coverage.
- **The stumble window went with it.** It existed to give the survivable band an end that death did not
  have: a quarter of a second off your feet, then up and walking. With one band, going down and losing
  your feet are the same event lasting the same time — the casualty is off its feet until a hospital has
  had it — so a field, a figure and a rule (`PER-12a`) all described the same fact twice.
- **The ambulances have something to do.** The two old bands were fixed speeds nobody derived, and at the
  shipped figures almost nothing ever reached either: a slice with a hospital, a duty roster and a
  stretcher ran on staged casualties alone. `PER-23` is now a distance a body is put down the road, which
  a retune of the pace or the mass moves with it.

`PER-12` and `PER-12a` are retired. Both numbers stay retired; the code that cited `PER-12` was citing it
for the fact that a body in the road is an agent like any other, which is `PER-1` and always was.

## 2026-08-26 — a building is stood as its picture and not as its plot

`OBJ-5a`. The plot's box and the roof's quad were never the problem: measured over every building of
every shipped map they differ by 19 mm, and by 116 mm on the four roofs that are fitted rather than
matched. **The gap was inside the picture.** A roof is drawn as a rectangle and painted as an L, a U, a
dome or a block with a porch, so twenty to sixty per cent of a building's perimeter carried more than
0.3 m of empty box in front of the wall — five to six metres of it on the town hall, the hospital and the
police station.

Static geometry is the one place in this engine where more bodies are almost free: statics are never
integrated, per-tick work is linear in the moving roster alone (`SOL-22`), and their grid is built once
and never rebuilt (`SOL-21`). Odesa's twelve hundred buildings become about three thousand boxes on a
static population of a hundred and thirty thousand. So the fix is more rectangles, not a new shape —
which is also why this and the car's rounded corners are different answers to the same complaint.

**Which roof a building wears moved down out of the renderer** into `world/statics/BuildingRoofs`. The
parts are authored in the picture's axes, so whatever turns the picture has to turn the walls: the
quarter turn for a swapped match, the half turn that puts the door on the right wall, and the scale a
civic roof is fitted by. Two constructions of that answer would be a town whose walls and pictures agree
until somebody edits one of them, and the disagreement is invisible until the collision layer is
switched on.

## 2026-08-24 — the suite is four minutes' worth of question asked in forty seconds

Three findings, and none of them dropped a claim: `qq tests all` was 2 m 50 s and is 36 s, with the same
number of assertions passing.

**A town is ticked once per map and every claim about that minute is read off the one run.** The classes
that watch a running town each stood their own `TownWorld` and drove it 3 600 ticks to watch one
invariant — thirty-odd runs of the same six towns, of which Odesa and River were seventy per cent of the
tier. They now keep a memoised run per map, exactly as [Towns](../src/tests/citygen/Towns.cs) already did
for the plans underneath them, and each claim records the first tick it was broken on instead of throwing
on it: one broken claim no longer costs the other five their answer. What still stands its own world is
what is *about* the ticks rather than about the state they arrive at.

**`SolverCollection` is for what the machine being busy could break, and nothing else.** It exists to
serialise the gates' measurement windows, and eleven classes that merely assert about a town had kept the
attribute from when the incumbent solver was unsafe across threads. Serialised, they were a 25-second
single-file tail on the end of the town tier — half of it, on a sixteen-core machine, running one class at
a time. The one non-measuring class still in it is the one that stands a world belonging to the incumbent
package, whose worlds live in a static roster.

**The gates are measured in Release.** Debug was the whole tier's configuration for the sake of one class:
`CrossingGateTests` reads a `[Conditional("DEBUG")]` counter and measures nothing without it. Everything
else there counts bytes through `GC.GetAllocatedBytesForCurrentThread`, which counts the same bytes in
either configuration, so the tier is now two takes — Release for the gates, Debug for that one class —
and 1 m 40 s became 25 s. A `Tier.Perf` note recorded an unexplained Release failure of the ground gate
when the gates ran as their own group; it did not reproduce in eight runs across both routes, and the
figure is taken off the configuration everything else here is measured in.

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
