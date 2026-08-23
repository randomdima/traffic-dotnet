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
qq tests                  the unit tier: two seconds, and what is run after every edit
qq tests unit town        a shipped city as well: half a minute
qq tests all              the gates too: under three minutes
qq tests e2e              the frames, unjudged
qq tests e2e --judge      the frames, judged by an agent: money, and about half an hour
dotnet run --project traffic-dotnet.csproj -- --bench maneuvers --map Odesa
dotnet run --project traffic-dotnet.csproj -- --shot .tmp/town.png --map Test
dotnet run --project traffic-dotnet.csproj -- --sheet .tmp/junctions.json
```

## The suite is selected by tier, not by folder

**Cost does not follow the slice tree.** One feature's folder holds a microsecond of walker arithmetic
beside two seconds of Odesa, so `tests/` goes on mirroring `src/` folder for folder and the cost lives
in a trait: every test class carries exactly one `[Trait(Tier.Key, …)]`
([tests/Tier.cs](../src/tests/Tier.cs)), and `TierTests` fails the suite for one that carries none — a
class in no tier's filter is never run again and nothing says so.

| Tier | What it asks | Config | Cost | Run it when |
|---|---|---|---|---|
| `Unit` | engine-free arithmetic, and the fixture map where a question needs a place | Release | **2 s** | after every edit |
| `Town` | a question asked of a *shipped* city — read, laid out over, or ticked | Release | 25 s | before saying a behaviour change works |
| `Perf` | the four gates below: what is measured over a whole town | Debug | 2 m 15 s | after touching the tick, the solver or a submit path |
| `E2E` | the visual tier, tier 4 above | Debug | 28 s unjudged | after changing anything that draws |

**The untiered suite is four minutes**, and the two multipliers behind that are worth knowing: Debug
costs roughly four times Release across the whole suite, and `Perf` is serialised on purpose. The unit
tier is 382 of the 807 cases and 0.4 seconds of the four minutes.

`Perf` and `E2E` are taken in **Debug** deliberately — the counter behind `CrossingGateTests` and the
crossing read-out the shot path prints are both `[Conditional("DEBUG")]`, and a Release run of either
measures nothing while reporting green. The reasons are on `Tier.Perf` itself.

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

## The verification intentions

Each is a *relation holding over a sample*, not a list of cases to tick off.

**VER-1** A generated city satisfies the connectivity, spacing and parking rules for a large sample of
world seeds, within the attempt bound. Internal rejection is not a failure; exhausting the bound is.

**VER-2** Every parking space can be entered and left by a legal manoeuvre, reverse permitted, and a car
can turn around within a single intersection.

**VER-3** Over a long unattended run, **no dynamic body ends up overlapping another** and **no agent is
permanently stuck**: every agent either progresses toward a goal, is legitimately idling, or is in a
terminal state. Abandoned cars are not agents and are exempt.

**VER-4** Agents are observed to obey their soft rules in the ordinary case — every walker rule and every
driver rule, plus the yield.

**VER-5** Soft rule violations recover: a pedestrian pushed onto a road returns to valid terrain, and a
car forced off-road returns to drivable terrain or is abandoned.

**VER-6** Damage outcomes are the energy arithmetic **and nothing else**, for every ordered pair of
participant kinds and every band of contact energy — including that the same contact may break one
participant and not the other — plus the three exemptions, the terminal-state rules and the survivable
band's run-clear.

**VER-7** Reproducibility: the same world seed regenerates the same city; the same agent seed with a
different world seed still produces a valid simulation, and vice versa.

**VER-8** A whole trip completes end to end.

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

**A scenario laid to be hard is quoted and not gated.** The two proving grounds are the same lap with a
different fifteen people on it, so `--bench track` and `--bench drunk` are read against each other — and
only the first of them has a tier-3 case behind it. Nobody can be knocked down on `Track`, because a body
there steps into ground nobody has taken, and that is asserted; a body on `Drunk` does not ask, so what
happens to it is a reading and not a bound. **What that count is for is being read**, and asserting it to
zero would be tuning the drunks until the instrument could no longer report the thing it was laid to find.
The swerves, the back-offs and the laps given up on that the same table prints are quoted for the same
reason: they are what a slow thing in the lane costs, which is the whole question the second map asks.
