# Verification

**Four tiers, cheapest first. A claim is checked at the cheapest tier that can answer it**, and a claim
no tier can answer has not been written falsifiably.

| Tier | Answers | Cost | Where |
|---|---|---|---|
| **1. Unit tests** | Anything engine-free: plan geometry, damage arithmetic, routing, the signal table | seconds | [tests/](../src/tests/) |
| **2. Headless probes** | Whole-town behaviour: counters, invariants, stuck agents | minutes | [bench/](../src/bench/) |
| **3. Render checks** | Claims about the picture **that can be stated as a threshold** | a build + a frame | `--shot` |
| **4. Agent checks** | Claims about the picture that **cannot** be a threshold | an agent's verdict | [tests/e2e/](../src/tests/e2e/) |

**Budget.** A headless soak is for a milestone. **A side feature gets a few unit tests on the engine-free
model, not a new soak scene.**

```
qq tests [--changed] [tiers]   one or more tiers, each in the configuration its answer is true in
qq tests e2e --judge           the frames, judged by an agent
qq doclint                     every rule stated once, and every ID the code cites resolving
dotnet run --project traffic-dotnet.csproj -- --bench maneuvers --map Odesa
dotnet run --project traffic-dotnet.csproj -- --bench exam        # a scenario's claims, and an exit code
dotnet run --project traffic-dotnet.csproj -- --shot .tmp/town.png --map Test
dotnet run --project traffic-dotnet.csproj -- --sheet .tmp/junctions.json
```

**What each run costs and which change is worth it is [CLAUDE.md](../CLAUDE.md#verification)**, and it is
stated there and nowhere else — a figure that drifts is worse than no figure, and four copies of it drift
apart.

**Judging is spelled `--judge` and happens nowhere else.** It is the one thing in this project that
spends money, so no command reaches it by default and every route to the suite that is not
`qq tests e2e --judge` takes the frames unjudged or leaves the tier out.

## The suite is selected by tier, not by folder

**Cost does not follow the slice tree.** One feature's folder holds a microsecond of walker arithmetic
beside two seconds of Odesa, so `tests/` goes on mirroring `src/` folder for folder and the cost lives
in a trait: every test class carries exactly one `[Trait(Tier.Key, …)]`
([tests/Tier.cs](../src/tests/Tier.cs)), and `TierTests` fails the suite for one that carries none — a
class in no tier's filter is never run again and nothing says so.

| Tier | What it asks | Config |
|---|---|---|
| `Unit` | engine-free arithmetic, and the fixture map where a question needs a place | Release |
| `Town` | a question asked of a *shipped* city — read, laid out over, or ticked | Release |
| `Perf` | the four gates below: what is measured over a whole town | Release, and Debug for one class |
| `E2E` | the visual tier, tier 4 above | Debug |

**Three multipliers separate a tier from the untiered suite**: Debug costs roughly four times Release
across the whole suite, `Perf` is serialised on purpose, and a town is only ticked once however many
claims are asked of that minute of it. **The unit tier is about a third of the cases and under a second of
the time**, which is the whole reason it is the one run after every edit.

**Two things want Debug and neither is a tier.** `CrossingGateTests` reads a counter that is
`[Conditional("DEBUG")]`, so `qq tests perf` takes that one class in Debug and the rest of the gates in
Release; the e2e tier stays in Debug for the same counter, which the shot path prints beside every
frame. Everything else is measured in Release, where it asserts the same things four times faster —
allocation is counted by `GC.GetAllocatedBytesForCurrentThread`, which counts the same bytes in either
configuration, and this engine has no `Debug.Assert` anywhere.

**A town is ticked once and every claim about that minute is read off the one run.** A shipped city is
seconds and the questions to ask of it are dozens, so the classes that watch a running town keep a
memoised run per map — the shape [Towns](../src/tests/citygen/Towns.cs) already uses for plans, carried
on to the towns those plans are stood up as. A claim that has to watch the ticks go past records what it
saw *during* that one run rather than standing a second world, and where two claims want two different
moments of it, both moments are taken off the one run as it passes them. **What is memoised is anything
derived from a plan and never written to** — the road graph, the ways at the bays, the pavement's graph
and the ground's triangles are functions of the plan and the figures, and a claim about one of them is
one build of it however many claims there are.

**A soak is asked of the towns that can answer it.** Driving a town for minutes is how a state that turns
up rarely is caught, and what makes one turn up is traffic: the maps behind the scenario submenu are laid
to put one behaviour under a microscope, so a ten-minute run over five streets with a walker apiece
witnesses nothing the first ten seconds did not and costs the whole of the run to say so. Minutes of
driving go to `Towns.EveryMapWorthASoak`; a claim answered off a plan, a graph or a town at rest goes on
being asked of every shipped map, where a small map costs a tenth of a second and is the one that catches
the odd shape.

**A claim that something happened ends the run that shows it.** Where every assertion is existential — a
mark reached the ground, a road was closed and a casualty still delivered, both lamps lit — the tick that
answers the last of them is the end of the question, and the tick count is the bound on how long a town
may honestly take rather than a window to watch to the end of. A claim about a bound, a maximum or a
count that must stay zero has no such end and runs its length.

## The gates

Four tests in [tests/gates/](../src/tests/gates/) that assert what would otherwise be a habit, and each is
re-taken on the largest town the project can open:

| Gate | Asserts |
|---|---|
| `AllocationGateTests` | The steady state allocates nothing (rule 2, [goals.md](goals.md)) |
| `CrossingGateTests` | The frame's managed→native crossings are flat in the size of the town (rule 1) |
| `OverlapGateTests` | `PHY-1` on a town that is *running*, not on a staged pair |
| `SolverGateTests` | `SOL-20`, including **across contact churn** as bodies touch and separate |

`OverlapGateTests` asserts that no **one** body stays inside another, and not that nothing is ever inside
anything — the second is not a fact about this town: a soft-step solver answers an approach by letting a
pair touch and pushing them apart over the ticks that follow. **Recovery is a handful of ticks; being
stuck is every tick until something moves**, and that distinction is the whole gate.

## A figure taken off a cold process is not a figure

**Tiered compilation settles on a clock and not on a call count**, so a warm-up counted in ticks warms
the town and leaves the code alone. A method is quick-jitted, re-jitted with instrumentation for
Dynamic PGO, and only then optimised, and each promotion waits on a background timer — a run measured
before that is partly a measurement of the compiler, and the process warms once while every probe warms
per map, so **whichever map ran first carried the whole cost**.

It is not a small effect and it is not a correction to apply afterwards. Odesa and River read 966 µs and
240 µs a tick with the warm-up by ticks alone, and 519 and 258 on the same build with the process warmed
first: most of the four-fold gap between the two towns was the JIT, and the empty loop's own figure at a
thousand agents was 18.9 µs where the settled one is 1.3.

**Every probe that prints a time calls [`Warmup.TheProcess`](../src/bench/Warmup.cs) before its first
row**, which stands a town of its own and runs it against the clock. Each map's own warm-up is left at
its tick count on purpose: that one decides how *old* the town being measured is, which is a different
question and one the probe is entitled to answer for itself.

## Fixtures

**Every town the suite asks a question of is read once and handed out** — reading a city is a tenth of a
second and there are a dozen questions to ask of it.

**A shared plan must not be written to.** A test that breaks a town on purpose reads its own copy, and
the day this project has a generator its two determinism tests take a fresh town twice on purpose:
handed the shared one they would compare a town to itself and pass whatever the generator did.

**Ask a whole city the shallow questions only**; detailed geometry is asked of named places on the
fixture map ([citygen](../src/citygen/docs/requirements.md#the-maps)).

## What a map claims about itself

**A map laid to measure one thing states what it claims, and the claims are answered while it runs** —
`Bench.ScenarioWatch`, one watch per question and one per town. What each map claims is its own
([citygen](../src/citygen/docs/requirements.md#the-maps)); what follows is how a claim is read.

**One machine and three readers.** The same watch answers the panel along the bottom of a windowed run
(`OBS-2i`), the table a headless one prints on its way out, and the tier that asserts on that map. A
second implementation of any of them would be a second answer, and a panel disagreeing with an exit code
is not something anybody could settle by looking at the town.

| Read | Where |
|---|---|
| A player watching | the panel, shut to a line of counts, opened by its title or by `--ui scenario` |
| A script | `--bench <name>`, or `--map NAME --seconds N`: the table, and **a broken claim is a failed run** |
| The suite | the town tier, asserting the same claims off the same watch |

**A claim fails a run and a reading never does.** The split is the project's own: what must hold on every
map is a claim, and what is a fact about one town — the drunks' swerves, the laps a fleet got round, how
far an articulated pair gets through a dense city — is quoted beside it, because asserting it would be
tuning the towns until the instrument could no longer report the thing it was laid to find. **A probe that
gates nothing says so at the point it is listed** (`CheckCatalogue.Quoted`), rather than leaving a caller
to read a bare exit code as a pass.

**A claim nobody has answered yet is not a pass.** Every claim is `waiting`, `kept` or `BROKEN`: a lap
nobody has been round is not a lap driven badly, and a run cut short before its subject arrives has asked
the engine nothing. The last line of a report carries the three counts, and the suite fails a claim its
own run left waiting — a test chooses how long it watches.

**A scenario may stage what it is about**, on the exam's terms: the exam orders its thirty-six cars on the
first tick and the crossings map sends its five walkers over their own paint, because a body left to
itself takes a crossing only by luck. **What is claimed is what the town did with that order** and never
that it was given one — and the staging is the watch's, so a run of that map in the game stages it exactly
as the probe does.

## The verification intentions

Each is a *relation holding over a sample*, not a list of cases to tick off.

**VER-1** A generated city satisfies the connectivity, spacing and parking rules for a large sample of
world seeds, within the attempt bound. Internal rejection is not a failure; exhausting the bound is.

**VER-2** Every parking space can be entered and left by a legal manoeuvre, reverse permitted, and a car
that has to come back the way it came can do it — in a bay of a car park (`GEN-4l`) or by working itself
round in a dead end (`P-19`), which are the two ways round there are (TER-5f).

**VER-3** Over a long unattended run, **no dynamic body ends up overlapping another** and **no agent is
permanently stuck**: every agent either progresses toward a goal, is legitimately idling, or is in a
terminal state. Abandoned cars are not agents and are exempt.

**VER-4** Agents are observed to obey their soft rules in the ordinary case — every walker rule and every
driver rule, plus the yield.

**VER-5** Soft rule violations recover: a pedestrian pushed onto a road returns to valid terrain, and a
car forced off-road returns to drivable terrain or is abandoned.

**VER-6** Damage outcomes are the energy arithmetic **and nothing else**, for every ordered pair of
participant kinds and every band of contact energy — including that the same contact may break one
participant and not the other — plus the three exemptions, the spent-body rules, and that the band a
person is put down at is the slide it leaves in them.

**VER-7** Reproducibility: the same world seed regenerates the same city; the same agent seed with a
different world seed still produces a valid simulation, and vice versa.

**VER-8** A whole trip completes end to end.

**VER-11** **Every map states what it claims about itself, and every run of it says whether it kept it** —
in the panel a player is looking at and in the output a script reads, off one watch. A map laid to measure
one thing claims that thing; every town, laid or traced, claims the three above it: `PHY-1`, that nothing
goes on driving into ground the book refused it (`TER-4c.1`), and that nothing stands still with no clock
running for it. A claim the run has not answered is reported as unanswered rather than counted either way.

**What the second of those counts is a body going *deeper*, never a body being past.** A grant is worked
out from the pose every tick, so a body that stopped where it was told to and overshot by a stride latches
there: read as a state it is a walker at a kerb reported for the whole minute it waits, and the claim then
says nothing about anybody driving on. Read as a run of ticks each deeper than the last, it says exactly
what it is for — and the run it allows is longer than any stop from town speed, because the ticks a body
spends arriving at rest are the mechanism working rather than a body ignoring it.

**VER-9** Claims about the picture that **can** be stated as a threshold — markings sit on the road, a
signal head hangs where its doctrine puts it and shows the lamp its cycle publishes — are checked on
rendered frames, because every other kind of check answers them about the numbers instead.

**VER-10** Claims about the picture that **cannot** be a threshold — that a dashed line is evenly pitched
and runs down the middle of its road, that a texture tiles along a wheel rather than stretching and
seaming, that a pavement sweeps round a corner rather than kinking, that traffic looks like traffic — are
checked by **staging the scene, photographing it, and judging the photograph against expectations written
down beside it**. The expectations are versioned with the code and **each one is a single claim a
reviewer answers yes or no to**; *"does this look right?"* is not one of them.

The scenarios are [tests/e2e/scenarios/](../src/tests/e2e/scenarios/), a few per feature, each anchored on a
named place of a shipped map rather than on a search of the town. `qq tests e2e --judge`
stages every one through the game's own shot path ([app/shot/ShotRun.cs](../src/app/shot/ShotRun.cs), shared
with `--shot` so there is no second drawing path), leaves each frame in `.tmp/e2e/`, and then **hands the
frames and the claims to a Claude Code agent, whose verdict is the test's**
([VisualJudge](../src/tests/e2e/VisualJudge.cs), asking what [JudgeBrief](../src/tests/e2e/JudgeBrief.cs) says).
A scenario is green only when the agent answers PASS; a red one carries the agent's own reasoning, claim
by claim, and keeps it in `.tmp/e2e/<scenario>.verdict.md` beside the question it was asked
(`<scenario>.brief.md`).

**The harness is asserted first and cheaply** — a frame of the asked size, from the asked place, that is
not a flat fill — and a frame that fails any of that is never sent to be judged.

Each scenario also names the frame the **godot-dotnet build** takes of it, kept in
[tests/e2e/expected/](../src/tests/e2e/expected/), and the agent is given it as a reference and told plainly
that it is not a gate: a difference from it is reported, never failed. Otherwise this tier would quietly
become a pixel-diff against another engine's art.

**Where a reference was taken from a named place, this build photographs the same ground to the metre** —
same centre, same width — so the two frames can be laid side by side. Two kinds of reference cannot be
matched, and each says so in its own scenario rather than being quietly compared anyway: the frames the
other build located **at run time** (wherever its own cars or walkers happened to be, which is empty road
here), and the frames it took with **one-off scripts** that recorded no framing at all. For those the
claim is about what is drawn and never about where.

**Several subjects of one scenario are tiled into a contact sheet** — magenta gutters, reading order,
every claim asked of every cell — which is the reference build's format too, so a sheet is compared
against a sheet cell for cell. Tiling saves nothing: the cost of a frame is its pixel area. What it buys
is one judgement over N subjects, which is the only way "each of these is individually correct" can be
asked at all. The tiler is the engine's own ([app/shot/Sheet.cs](../src/app/shot/Sheet.cs)), shared with
`--sheet`.

## Looking at something by hand

**A picture taken to be looked at rather than asserted on is asked for as a document** — `--sheet`,
which stages several framings, captions each with the map, the framing, the moment and the seed, and
tiles them into one picture with its figures beside it ([app/shot](../src/app/shot/docs/requirements.md)).
It is not a tier and nothing gates on it: it is what tiers 3 and 4 are debugged with, and what a finding
about the picture is reported as. A `--shot` says the same about itself with `--caption`.

| Knob | Does |
|---|---|
| `TRAFFIC_E2E_JUDGE=off` | takes the frames and skips the judging — what to set while iterating on a scenario's framing |
| `TRAFFIC_E2E_MODEL` | which model judges (default `sonnet`; `haiku` is four times cheaper and measurably looser) |
| `TRAFFIC_E2E_JUDGE_TIMEOUT_S` | how long one scenario may take before the run is a failure rather than a hang (default 300) |

**A verdict is not perfectly repeatable**, and that is the price of asking a question no threshold can
answer. Two runs can disagree on a marginal claim; the reasoning is kept per claim so a red run is read
rather than merely counted. Judging costs a few cents and about a minute a scenario, so the set is a few
dollars and half an hour — it is not the check to run on every edit, which is what the first three tiers
are for.

## The instruments say what is missing

**No document holds a list of what is unbuilt**, because such a list is stale the week after it is
written. The last line of `--bench maneuvers` is the set of catalogue entries nothing entered, which is
how an unbuilt entry and an unreachable one are told apart; `--check` prints the dependency read-out; and
every figure is quoted with the census that says whether the town it ran was a town.

**A probe with claims behind it ends with them**, and its exit code is the answer: the table is what a
person reads and the last line is what a script does. The ones that gate nothing say so where they are
listed rather than answering a bare zero.

**`--bench rescue` and `--bench recovery` are the same reading said of the ambulance and the evacuator**
([agents/ambulance](../src/agents/ambulance/docs/requirements.md),
[agents/evacuator](../src/agents/evacuator/docs/requirements.md)). Each stages one casualty or one wreck a
town — through the damage roster, so what it does to the town is what a car hitting something does — and
prints raised against collected against delivered, with the times beside them. **Both are read by a tier as
well as by a person**: `RescueEndToEndTests` and `RecoveryEndToEndTests` assert off the probe's own row
rather than off a run of their own, so the gate and the instrument cannot disagree about what a rescue is.
**What each gates is what must hold on every map** — that a casualty is collected and delivered inside the
errand's own give-up bound (`AMB-9`), that a recovery which begins actually drags something — and every
bound it is held to is a figure on `SimConfig` rather than a wall-clock reading. **What is quoted and never
gated is the arrival**: whether a dense city's geometry lets a nine-metre articulated pair get all the way
home is a fact about that city (`EVA-8`), which is a reading and not a claim.

**`--bench exam` and `--bench crossings` are the same arrangement said of a junction and of the paint**
([citygen](../src/citygen/docs/requirements.md#the-maps)). Each prints a verdict a subject — the movement,
the claim its card makes, and what the cars actually did — and under that table the same run said as
claims, and a tier reads that same run: `JunctionExamTests` asserts card by card off the probe's own
verdict, so the instrument and the gate cannot disagree about what a crossing is. **A card this build does
not pass carries what it does instead**, and the tier asserts that card *still fails*: the day the engine
passes it, the suite says so and the finding is deleted rather than left standing as a note nobody
re-reads.

**The documents have two instruments of their own**, for the same reason and read the same way.
`qq doclint` asks whether every rule is stated exactly once and every ID the code cites resolves — a
citation to a renumbered or retired rule compiles, passes and misleads, and nothing else in the suite can
see it; it runs inside `qq checks`. `qq outline`, given no argument, prints the longest files in `src/`,
which is where "nothing grows past being readable" is checked rather than asserted.

**A map laid to be hard claims less, not more.** The three proving grounds are one lap read against each
other, so what each of them may claim is bounded by what that lap can honestly answer: `Drunk` has a body
reeling down the carriageway stopping the field where it stands, so no shape on it gets the passes a mean
is worth taking over and the shape claims stay the pacing lap's; a `Fleet` whose cars differ in every
figure is a table to read rather than a bound to hold, so what it gates is that every look drives at all
and never what any of them is worth. Everything a hard map is *for* — the swerves, the back-offs, the laps
given up on, what each look pulls at — is quoted rather than claimed, on the split above. What each of the
six claims is [citygen](../src/citygen/docs/requirements.md#the-maps)'s.
