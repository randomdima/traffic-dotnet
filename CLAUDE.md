# traffic-dotnet — working agreement

A top-down multi-agent traffic simulation of a small town with **no engine under it**: C# on .NET 10,
managed from the decision loop to the bytes the GPU reads. What it is for is
[docs/goals.md](docs/goals.md); what it is made of and how to run it is [readme.md](readme.md); every
document is indexed by [docs/index.md](docs/index.md).

## Everything is a vertical slice

**A feature is a folder, and everything about that feature lives in it** — its code, its `docs/`, its
requirements, its decisions, and its art at the matching path under `assets/`. This is the rule that
decides where a new thing goes, and it applies to prose and pictures exactly as it does to code.

**All the code is under `src/`**, and a slice is named by its path within it — `core/` is `src/core/`,
and this page names slices the short way. `assets/`, `towns/` and [docs/](docs/index.md) sit beside
`src/` at the root, so `bin/` and `obj/` are the only other folders there.

- **Code.** `agents/car/`, `world/terrain/`, `app/hud/`. A dependency points **down** the tiers in
  [docs/slice-map.md](docs/slice-map.md), never up, and **there are no cycles**. Where a lower slice
  needs something a higher one has, hand over the *data* and not the type.
- **Tests.** `tests/` mirrors the tree it tests, folder for folder.
- **Docs.** A rule about one feature is that feature's `docs/requirements.md`. Only what belongs to no
  single slice goes in [docs/](docs/index.md).
- **Decisions.** The nearest `decision-log.md` — the slice's own where the decision is the slice's, the
  root one where it is the project's.
- **Assets.** `assets/` mirrors the code tree: `assets/agents/car/variants/` for what
  `src/agents/car/body/` reads. `towns/` is input rather than an asset and belongs to no slice.
- **Numbers.** On `SimConfig`, authored in the nested groups and derived on the root. **A literal in
  behaviour code is a defect.**

**Nothing grows past being readable.** A document that has to cover eight things is eight documents and
an index, and the same is true of a type. A slice's `docs/` gets an `index.md` only once it holds more
than two documents; below that the file names are the index.

## The two rules everything else answers to

Both have a gate in [tests/gates/](src/tests/gates/), so breaking one fails the suite rather than a habit.

1. **The frame's managed→native crossing count is O(1) in the size of the town.** A frame that makes one
   call per car is the cardinal sin here. Nothing is marshalled: blittable structs, `Span<T>` over memory
   the driver already owns, function pointers.
2. **The steady state allocates nothing.** Not little. No LINQ, iterator, closure, `params`, boxing or
   interface call the JIT cannot devirtualise on a hot path; transient working sets come from a pool or
   the stack.

A change that costs either of them is not a trade to be weighed in passing — it is a change to
[docs/goals.md](docs/goals.md) first.

## Which document to open first

| Doing | Read |
|---|---|
| Anything, before the first edit | this file, then [docs/goals.md](docs/goals.md) |
| Working on one feature | that feature's own `docs/`, found from [docs/index.md](docs/index.md) |
| Changing what the simulation *does* | the rule that owns it — the ID map in [docs/index.md](docs/index.md) says which document |
| Adding a file, or wondering where something goes | [docs/slice-map.md](docs/slice-map.md) |
| Changing a number | `SimConfig` in [core/](src/core/), never a literal at the call site |
| Deciding how a change gets checked | [docs/verification.md](docs/verification.md) — four tiers, cheapest that can answer it |
| Asking "why is it like this?" | the nearest `decision-log.md` — never the code, which carries no history |
| Understanding how one type works | that type's XML docs. There is no second description of it anywhere |
| Opening a file you have not read | `qq outline <file>` first — the members and their line ranges, so the read is a range |
| Resolving a rule the code cites | `qq req TER-5c` — what it says, which document owns it, and everything citing it |
| Building, running, the command line | [readme.md](readme.md) |

## Where the documentation lives, and why there is so little of it

**The code is the documentation.** Types carry `<summary>` and `<remarks>`, and a gotcha, an invariant or
an external constraint belongs on the type that has it. A separate page describing how a class works is a
second copy that disagrees with the first within a month.

What is written down is only what the code cannot state: what the town must be true of, and why. Four
rules bind all of it.

1. **A rule states a relation; a number is data.**
2. **History lives in a `decision-log.md` and nowhere else** — not in a requirement, not in a comment.
   A superseded decision is deleted from the log, not annotated.
3. **No requirement ID is ever renumbered**, and a retired number is never reused. The code cites them.
4. **Nothing about this project is remembered anywhere else.** No assistant memories, no private notes,
   no session logs. Write a finding into the document or the doc-comment that owns it, at the moment it
   is learned, or it goes nowhere.

**No document holds a list of what is unbuilt.** Such a list is stale the week after it is written: the
instruments report it instead — the last line of `--bench maneuvers` is the set of catalogue entries
nothing entered. The two absences big enough to be structural are named in
[docs/index.md](docs/index.md#known-gaps).

## Verification

**A claim is checked at the cheapest tier that can answer it.** A side feature gets a few unit tests on
the engine-free model, never a new soak. The tiers, the gates and the fixtures are
[docs/verification.md](docs/verification.md).

```
dotnet build
dotnet run --project traffic-dotnet.csproj -- --map Odesa
dotnet run --project traffic-dotnet.csproj -- --shot .tmp/town.png --map Test --caption
dotnet run --project traffic-dotnet.csproj -- --sheet .tmp/junctions.json
dotnet run --project traffic-dotnet.csproj -- --bench maneuvers --map Odesa
```

**A map says whether it kept what it claims, and a broken claim fails the run.** Every run draws the
claims in a panel along the bottom and prints the same table on its way out; `--bench <name>` exits
non-zero when one of them is broken, and what is quoted beside them fails nothing
([docs/verification.md](docs/verification.md#what-a-map-claims-about-itself)). **A new claim goes on the
map's own watch in [bench/](src/bench/)** and is read from there by the panel, the probe and the tier — a
second copy of it anywhere is two answers.

**A picture to be looked at is asked for with its caption on** — `--caption`, or `--sheet` for several
framings at once ([app/shot](src/app/shot/docs/requirements.md)). It carries the map, the framing, the
scale bar, the tick and the seed, so a frame in `.tmp/` is still readable a week later and the same one
can be taken again.

**Run the tier the change can have moved, and never the whole suite by habit.** `qq tests` selects by
the `Tier` trait every test class carries ([tests/Tier.cs](src/tests/Tier.cs)); the untiered
`dotnet test` is four minutes and is not a command to type here.

| Ran | Costs | After |
|---|---|---|
| `qq tests` | 4 s | **every edit, no exceptions** |
| `qq tests --changed` | what it picks | **any edit worth more than the unit tier** — it reads the tree and names the tiers those paths can have moved |
| `qq tests unit town` | 45 s | a change to behaviour, before saying it works |
| `qq tests all` | 1 m 15 s | touching the tick, the solver, a submit path — or before a commit |
| `qq tests e2e` | 1 m 35 s | changing anything that draws, to look at the frames |
| `qq tests e2e --judge` | **money**, ~30 min | a milestone, or when asked for by name — never unprompted |
| `qq tests --name=Kerb town` | — | one class or one case, while fixing it |

**A test run is waited for, never backgrounded and polled.** A poll is a round trip that costs more than
waiting does; `--changed` is how a run is made short. And **`qq tests all` is still the commit's tier
rather than the edit loop's** — it is two builds and every gate, and the tier that fits the change is
always shorter than the one that covers everything.

**Never open a windowed run to look at something `--shot` can answer** — it needs no window, no
compositor and no desktop, and the image is the same recording against a different target.

## House rules

- **Edit with the file tools**, not `sed`/`python3` one-liners. Shell text processing is read-only
  inspection.
- **Scratch goes in `.tmp/`**, which is gitignored and wiped without asking. Never the system temp
  directory and never a per-session scratchpad — a session's scratch dies with the session, and nobody
  can look at the pictures again.
- **Metric units, and the unit is part of the name** — `…M`, `…Mps`, `…S`, `…Deg`, `…Px`. The metre↔pixel
  conversion has exactly one site.
- **Prefer moving code to deleting it**, and commit before a change large enough to want an undo.
- **A rule is cited by ID and the citation is checked**: `qq doclint` (inside `qq checks`) fails when a
  cited ID resolves to nothing, and `qq req <ID>` is how one is looked up rather than grepped for.
- **Name what has already refused it** before adding a check that refuses a movement (SIM-7). A second
  gate does not make the town safer; it makes the first one useless.
