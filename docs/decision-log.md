# Decision log — the cross-cutting decisions

Why the project as a whole is shaped this way. A decision belonging to one slice lives in that slice's
own log ([index.md](index.md)); only decisions still binding are here, and a superseded one is deleted
rather than annotated. Rules are [requirements.md](requirements.md); how a thing works is its XML docs.

## 2026-10-26 — two heads: the desktop and the page

**Asked for by the owner (2026-10-26): Android support is dropped.** `traffic-dotnet.android.csproj`, `app/android/`,
`runtime/android/` and `app/main/android/` are gone, and with them `ProjectPaths.FoundAt`, which only the handset's
unpacked archive called. A phone is reached through the page, whose fingers are CTL-9's.

Retired, and named by nothing but this entry: `AND-1`…`AND-8`, the handset head's rules.

## 2026-10-25 — art is asked in words, against a style picture, on a transparent ground

**The owner (2026-10-25): "No outlines should be used for generation. Since models are not following them anyway. And
to save cost. Only style references. And never with background - only transparent."** The rule is
[raw_assets/index.md](../raw_assets/index.md) (rule 4) and the remotegen skill.

- **The layouts were not held.** Every family's sheets came back off their outlines: roofs drifted and evened out,
  trains 11–16 % wide, crossing gear 10–16 % big, kerbs 1.3–1.6× deep, and the first props' crowns 10–35 % big with the
  small ones worst (`props_trees_mid-v1`); each was answered with an allowance to fit further, a re-roll, or a lost
  cell. A layout is also a second input picture paid for on every frame.
- **What is drawn is the asset, and the engine maps it.** A drawing is found by the order its ids were asked in and
  taken whole, sized by the one figure its file authors; its other extents are the drawing's — a footprint's depth, a
  strip's length (sized by its depth, which the ground needs), the rectangles a roof is built of (read off its
  silhouette, `_outline.rectangles`). A car was already asked this way (2026-10-20).
- **A result may be corrected, the generator never limited** (the owner, the same day: "cutting and sizing part of the
  gen images fine if it looks correct … not to try to limit gen models … minor corrections – its ok. Just dont overuse
  it. Most assets will look bad if you do this"; "Do allow complex building … describing it with words as best as we
  can. And try to map results in the engine").
- **The old cut stays for old frames**, so a variant kept from a layout can be cut again (rule 6); `qq sprites` lays no
  layout.
- **Props may reach past their colliders** (the owner, the same day: "minor visuals beyond collider … can even be good
  for some of them"), and **a prop that never turns need not be strictly top-down** ("since we dont need to rotate some
  of them").

## 2026-10-19 — every authored map runs a railway, the fixture's laid clear of its staging

**The owner (2026-10-19): "add it on all maps"**, asked why O10 had no trains (RAIL-2).

- **O10 and O30 take Odesa's double track** (bearing, offset share and meander alike): they are its brief at scale, and
  the offset and wander are shares of the short side, so the line stands where Odesa's does.
- **Test takes a single track parallel to its river, 0.35 of its short side off the middle on the west bank, looped
  −270°** so its way out turns away from the river (RAIL-11). The fixture is what the detailed checks are staged on,
  and a track reshapes the wheel's streets past it (RAIL-5): River's line (12°, 0.18), and 12° at −0.3 and 0.3, each
  left the fixture with no street short enough to be cut twice within 60 m and no arrow laid onto a zebra — the
  staging four unit tests guard; a loop at the middle kept a single block. Where it runs now it keeps the staging and
  meets no road. Moved, it is re-chosen by the same tests.
- **River's loop is laid 0.15 of the long side west along its line** (`railLoopAlongShare`): at the middle it stood in
  the river.
- **`BigOdesa` takes none**: it is authored into no map.

## 2026-10-17 — the town keeps real time whatever the display's rate

**The owner (2026-10-14): "atomic tick is physics tick, render happens every physics tick up to vsync" … "I mean that
depends on game speed control, but yeah"; (2026-10-17): "on 30 fps game is running slower than on 60 … we want game
clock to be synced with real clock".** SIM-9.

- **A frame runs every tick its span owes and draws the last.** The clock had owed one tick a frame at pace 1, read off
  "every physics tick" without its "up to vsync": a 30 Hz display ran the 60 Hz town at half speed, and every pace with it.
- **A tick slower than the time it stands for is still not caught up on** (`SimClock.MayRunAnother`): past the ticks its
  pace asks, a frame runs another only while the ones it ran kept real time. Catching up on slow ticks never wins the
  time back — O30 at 40 000 cars, the device world, 30 s windows: 7 fps at 8 ticks a frame (146 ms) against 54 fps at
  one (18.65 ms), the tick 18.3 and 18.5 ms, the town at 0.92 of real time either way.
- **Of the ticks owed and not run one is kept for the next frame** and the rest dropped (`SimClock.Unrun`): a display
  beating against the tick's period owes none on a frame a hair early and two on the next, and dropping the second ran
  a town with time to spare a tenth slow (2026-10-15).
- **A frame catches up over `LongestCaughtUpFrameS` at most** (0.1 s), so a hitch is not repaid as one frame many ticks
  long.
- **A page runs the ticks a frame could not take as its device's answer lands** (`Game.TickBetweenFrames`), not at the
  next frame. WebGPU cannot be waited on: a read-back lands only once the run has handed the thread back to the browser,
  so a page's frame can step its device once, where a desktop's waits on a fence and steps it as often as it is owed. The
  pacing — the clock, the tick loop, `WorldLayer.Ready` — is one path on both heads; the page alone is woken between
  frames, and a tick put off for an unready world is kept whole (`SimClock.Postpone`) where a slow one is not. Waiting
  on the device instead would want the run on a worker with `Atomics.wait`, so cross-origin isolation, which GitHub
  Pages cannot send the headers for.
- **Measured** (published page, Test, the device world, `qq web --ui frame --hz N`, 40 s): 60 Hz 1.0 tick a frame,
  30 Hz 2.0, 20 Hz 3.0, the town at 2 385–2 470 ticks over each run — real time at every rate.

## 2026-10-16 — a box is road to whoever asks where on the road a body is

`TownWorld.RoadPlaceOf` read only street lanes and pavements, so a body standing in a junction on nothing but its
joins was nowhere on the road. A car stood down there — a leg given up in a jammed box — was never sent anywhere
again (CAR-8's "sets off for a bay at once"), and Odesa's junction queued round it for the rest of the run.
**Decided**: a body on joins alone stands at the start of the lane the first of them leads onto — for a car being
sent on, a wreck being fetched and a casualty being reached alike, each of which needs a place on the road and had
none.

## 2026-10-16 — the desktop head is not published ReadyToRun until a build of it is handed out

**The owner (2026-10-16), shown the figures below: "go"** — on recording them and leaving the project file as it is.

- **ReadyToRun was measured against the plain JIT** (`dotnet publish -r linux-x64`, one with `PublishReadyToRun`,
  Odesa, 8 cores, alternated): the open (`--bench load`) 1 147–1 151 ms against 1 059–1 062, five runs each, −8 %;
  `--bench age` 4–6 % cheaper in its windows 0.8–2.5 s into the process; the steady tick (`--bench town`, a 60 s window)
  71.8, 70.9 and 72.9 µs against 69.9, 70.0 and 70.1, −2.6 %, at 1.6 B a tick both; every `--bench age` digest equal.
  The image doubles, 5.1 → 9.9 MB.
- **One stage of the open got dearer**: fleets, the tables, the roster and the spawn, 795 → 921 ms. Code laid ahead of
  time and run once is never tiered up, so a long loop called once keeps the precompiled code and Dynamic PGO never
  sees it; the rest of the open gained more than that.
- **It is not taken because it would reach nothing that runs**: crossgen acts only on a publish, and nothing publishes
  this head — every run, tier, probe and profile is the build's own output. The day a desktop build is handed out, it
  is `PublishReadyToRun` and a runtime identifier for that publish. A `.mibc` profile on top wants `dotnet-pgo`, which
  the SDK does not carry.
- **The browser head has no counterpart**: Mono compiles it whole ahead of time and nothing tiers it, and the SDK's AOT
  profile (`WasmAotProfilePath`), as Mono's profile-driven AOT is documented, chooses which methods are compiled
  rather than how, leaving the rest to the interpreter — what .NET 11's targets do with it was not tried.

## 2026-10-16 — a town follows the runtime's arithmetic, and ImageSharp stays at 3.1.12

**The owner (2026-10-16), asked both: "Follow the runtime" and "Leave as is".**

- **A town is laid off the runtime's own conversions** (`float.DegreesToRadians` and the like) rather than the
  engine's: a runtime that rounds one otherwise moves the towns it lays, as .NET 11 moved O30 once
  (`ConnectionPoints.BoundRad`, 391 more static bodies), and the digests are what say so. Only the world layer's
  trigonometry is the engine's own (`Trig`), because the device has to answer it to the bit.
- **`SixLabors.ImageSharp` stays at 3.1.12**, the last 3.1.x, with the three high and two moderate advisories NuGet's
  audit prints on every build (GHSA-jjfr-hcj7-qf5w, -j9gm-c75j-xc9q, -j3p4-wp97-rph4, -wmxv-xphr-5c9g, -gwg2-r3hj-4w44):
  it decodes the project's own art and the frames its own runs wrote, and 4.x wants a licence key.

## 2026-10-15 — branches a heading cannot predict are taken in bits, and an open's independent pieces run side by side

**Asked for by the owner (2026-10-15): the codebase reviewed for .NET 11's practice, performance first.** The review
read every slice against the O30 profiles; what was taken changes no answer — `--bench age`'s digests and O30's plan,
atlas and marks digests are unchanged — and what was not is [tech-debt.md](tech-debt.md).

- **`Trig` selects in bits** where it branched on a quadrant, a sign or a fold. A heading is in any quadrant, so each
  branch was a guess: on random headings `SinCos` is 4.1 ns (was 14.4) and `Atan2` 4.0 (was 12.3), the same to the bit
  over every float.
- **`EveryAgent`'s passes inline into the world's own method**: a class world is shared generic code, which called
  each agent through the interface.
- **An open's independent pieces run side by side**: the walk's and the carriageway's fills and the ground rings' two
  outsets (`InChunks.Both`); and as passes into slots of their own read back in order (`InChunks.Over`), the pavement's
  lanes, the crossing places' connections, an outset's cuts and kept pieces, the places and the bays near each lane,
  the kerb rings and the lane beside each lane of the walk — each thread on scans of its own. The one-way streets ask a
  movement table laid once rather than the road graph built again for every street.
- **No `stackalloc` is zeroed** (`[module: SkipLocalsInit]`): every buffer was audited, and the two read before
  written — a census's counts, a road's wander along an edge of no length — clear themselves.
- **What the open asks again is kept**: a car park site is measured against the car parks laid since it last was,
  and a street made one way is checked against the movements that ran onto the one it took, not the whole town —
  exact, because any route through the movement taken passed through one of those first (checked against the whole
  town's answer on every street of O30, Odesa, River and Test).
- **A queued car's line reading and aim are kept** while every figure they read is the same to the bit, and a frame
  keeps the scale legend's quads, the status title's figures and the window's pointer while what they read holds. A
  roster's cull is kept for the frames between two ticks: nothing moves a body between them but a placement, which
  counts itself (`TownWorld.KeptCull`).
- **The performance analyzers are warnings** (`AnalysisModePerformance`), so an allocation they know the shape of —
  a constant array argument, an uncached serializer option — fails nothing but is in every build's output.
- **The device's step writes nothing scattered into host-visible memory** (2026-10-16): the covers are gathered in
  device-local lists and copied out in whole lines, the cars' view is written a workgroup's rows at a time, and the
  step's copy and clear are kernels of the run's own rather than the driver's 4 kB copy path. O30's device step 0.90 →
  0.75 ms, the host's recording of it 0.19 → 0.06 ms a tick.
- **The aside thread blocks on a gate of its own** that each side has blocked on once before the first run: a
  `ManualResetEventSlim`'s first blocking wait allocated, on whichever tick the machine was slow. The suite runs the
  collector the game ships with, under which an allocation window counts only what was allocated.
- **A body's two corners stay in two arrays.** One array of both, a cache line a candidate, measured 3% slower in the
  solver over three interleaved pairs: the broad phase's first test reads the far corner's x alone.

| O30, 16 cores, interleaved | .NET 11, before | after |
|---|---|---|
| the town probe's tick, µs (window 1 / the timed window; three runs) | 8 128 / 7 141 | 7 949 / 7 060 |
| the open / plan / world / ground, s (two runs) | 35.0 / 14.0 / 15.9 / 5.0 | 20.5 / 8.4 / 8.5 / 3.6 |
| the open's CPU / peak resident | 86.9 s / 3 965 MiB | 90.8 s / 4 072 MiB |
| a window's allocation, the device's world | ~2.1 MB/s | the counters' own ~31 kB/s |

## 2026-10-15 — the engine runs on .NET 11

**Asked for by the owner (2026-10-15).** Every head, the suite and the two workshop tools target `net11.0`,
built by SDK 11.0.100-rc.2; TEC-1 says so. Nothing in the engine changed for it.
**The owner (2026-10-16): the page builds on the newest .NET 11, and .NET 10 is no longer built for** — CI installs the
newest 11.0 published and `global.json` rolls forward to it.
On O30 the tick and the open are a few percent cheaper and nothing else moved past the noise between runs:

| O30, 16 cores, RX 9070 XT | .NET 10.0.13 | .NET 11.0 rc.2 |
|---|---|---|
| the town probe's tick, µs (window 1 / the timed window) | 8 436 / 7 260 | 8 179 / 7 208 |
| allocated a tick, B (the solver's; the engine's own) | 1 945; 0 | 1 887; 0 |
| the open (`--bench load`) / the CPU it spent | 36.7 s / 92.2 s | 35.4 s / 87.6 s |
| a window's frames a second uncapped, the device's world / the managed one | 3 767 / 5 445 | 3 706 / 5 025 |
| peak resident, the open / a window on the device's world | 3 875 / 2 453 MiB | 3 943 / 2 373 MiB |

Three things the move found:

- **`float.DegreesToRadians` rounds differently**, and through `ConnectionPoints.BoundRad` it moved O30's plan, atlas
  and marks digests once (`f8820717f063b85a` → `005be742f9d398de`; 391 more static bodies). With .NET 10's
  `(deg * π) / 180` written there, 11 lays O30 to the bit as 10 did, so it is the only arithmetic the runtime changed
  under the town; whether it stays the runtime's is the owner's ([tech-debt.md](tech-debt.md#waiting-on-the-owner)).
- **The call-counting delay is still read** under its undocumented name: O30 opens in 34.9 s with it at nothing and
  42.7 s with the runtime's default put back (`DOTNET_TC_CallCountingDelayMs=100`).
- **A float cast to a narrower integer now saturates** on CoreCLR and not on the page's Mono. Every such cast here is
  clamped first, which is why they stay clamped.

**The owner (2026-10-15), in their words: "Yes we can move geometry logic to the GPU".** Where a town's world is the
device's, the device files its atlas (the road log): the host lays the pieces and the slots, and works out the marks
while the device files — and on a page, which cannot wait for the counts the slots are laid off, the world is laid over a
word standing in for the tables and reads its first covers once they are filed (`DeviceWorld.Ready`). The device's
tables are the host's to the bit (`AtlasKernelTests`, and OdesaOsm's digest), and a town laid either way steps alike
(`DeviceWorldTests`). The furniture is checked on the device against what it filed (`atlas_furniture.comp`).

| OdesaOsm | before | after |
|---|---|---|
| the page's open | 63.3 s | 52.3 s (the atlas 17.6 → 7.3 s, all but 0.9 of it the marks) |
| the page's memory, stood | 1 765 MiB | 1 779 MiB |
| one desktop core's open, the device's world | 36.8 s | 27.8 s |
| the desktop's peak resident, the device's world | 1 554 MiB | about 1 070 MiB |

## 2026-10-15 — a device world is made as its town is laid

**The owner, of the page's memory: "Can we keep it under 2Gb at all times? … Ok try to optimize memory usage and load
times".** The page held 2.8 GB after opening OdesaOsm because the host stood the whole atlas (930 MB of entries in one
block) and a managed world, only to upload the one and throw the other away. Now a town whose world is the device's is
laid with it (`IWorldMaker`, `LaidTown.Lay`): its atlas is filed by the device (above), and no managed world is stood.
**Not for a map whose watches stage it**
(`Scenarios.Stages`): a page's device cannot answer off the clock, so that one is still handed over after staging.

- **On a page a car of a map with no bay takes its lane on its decision clock and not at the first tick**: the
  device's first view lands a frame later, and the empty map's drive asks it (`TownWorld.DriveTheEmptyMap`). Every
  other map, and every desktop, stands as before; the two ways of laying a town step to the bit alike
  (`DeviceWorldTests`).
- **Each stage of an open says the memory it ended at** (`OpenWatch`): on the page's console, the page's own memory
  and what the collector holds, and in `--bench load` and `--bench deviceload` the process's.

| OdesaOsm | before | after |
|---|---|---|
| the page's memory, stood | 2 804 MiB | 1 775–1 889 MiB |
| the desktop's peak resident, as a window opens it | 2 523 MiB | 1 554 MiB |
| the desktop's open | 22.5 s | 19.5 s, with three scans made a cell's (below) |

**Three scans of every thing for every thing are a cell's** (SIM-8), each the same answer to the bit: two turns' lines
read apart (`LaneLines.Apart`, the other's points read once), crossing mouths merged (`CrossingWays.Places`) and the
crossing an edge is (`CrossingEdges.Of`, `PointCells.Nearest`).

## 2026-10-15 — a traced city opens in a page, and an open's peak is what it is judged on

**The owner (2026-10-15), in their words: "Make sure big maps also able to load on web. Try to optimize how we load them
in terms of both speed and memory usage".** OdesaOsm ran the page out of memory in `DeviceWorld`'s constructor after
105 s; it now stands in 84 s at 2.8 GB of the 4 GB a 32-bit page can address, and O30 in 170 s at 3.6 GB. A page keeps
the most it was ever given, so what was cut is the open's peak and its churn as well as its time:

- **The world is handed to the device between the world and the ground** (`LaidTown.Lay`), where the caller is the thread
  that may (`Game.Open`; a desktop's map picked on the menu is laid off the loop and handed over as it stands): the device
  world lets go of the host's atlas, and the ground is laid into what it held.
- **The managed world is let go of before the device's is laid** (`TownWorld.HandTheWorldTo`), and **what both left is
  collected from a frame that never held them, with the stack below written over** (`TownWorld.CollectWhatTheLayLeft`,
  `ClearTheStack`): a page's runtime reads its stack conservatively, and a standing frame that once held the managed
  world kept it and the gigabyte of atlas with it.
- **The atlas files without holding its points twice, and after its marks** (the road log).
- **What a whole-town scan asked of every agent is asked of a cell**: a person's door (`TownWorld.Doors`, forty thousand
  people times every door in the town) and a parked car's level (`ChainIndex.Nearest` within a reach — a car on the
  ground searched the whole town for one of 39 bridges).
- **A page's one thread is one loop** (`InChunks`), and **the sorts an open leans on are this assembly's**
  (`KeyedSort`): its ahead-of-time compiler leaves the framework's parallel loop and its sort over a pair of value types
  to the interpreter, and the two were a sixth of the page's open.

Measured on the published page (Chromium, `qq web --map OdesaOsm`, the page's own figures on its console) and the
desktop's `--bench load` (every thread's CPU, and the resident set's most):

| OdesaOsm | before | after |
|---|---|---|
| the page's open | out of memory at 105 s | 84 s |
| the page's memory | past 4 GB | 2 805 MiB |
| the desktop's open | 35.7 s, 88 CPU s | 22.5 s, 75 CPU s |
| the desktop's peak | 3 246 MiB | 2 523 MiB |

## 2026-10-15 — a page steps its world on its device too

**The owner (2026-10-15), in their words: "lets make it work in web"**, and of the doubles the kernels computed in:
**"Try f32 everywhere. We want builds to ne the same in the end. Keep track on sideeffects."** The kernels are one GLSL
source for Vulkan and, translated by naga, for WebGPU; they compute in floats alone (the core log), and the world's
trigonometry is `Trig` on both sides. How the page does it is the web log; what it cost is
[tech-debt.md](tech-debt.md).

## 2026-10-15 — a window steps its world on the device by default

**The owner (2026-10-15), in their words: "Make hybrid gpu default mode."** A windowed desktop run hands its town's
world to the device (`DeviceWorld`) unless `--managed-world` is named; `--device-tick` is gone. A picture, a sheet, a
probe and an unwatched drive keep the managed world — each stands its own town to be measured, and the suite's figures
are the managed world's — and so does a device with no doubles in its kernels, which cannot lay the atlas.

## 2026-10-15 — a walk of the fleet asks memory for the car eight ahead

**The owner (2026-10-15), in their words: "Go thought top hot paths one by one and consider improvements … test them
implementation. Keep what is reasonable".**

- **Where agents are walked in order and each reads scattered rows of its own, the walk fetches the eighth one on
  ahead** (`Sse.Prefetch0`, `TownWorld.PrefetchAhead`): the car's line at its cursor, its lanes' metres, chain and joins
  on every tick (`CarFleet.PrefetchTheLine`), and with it the plan's hold in the drives stage and the body's hold, then
  its first slot four on, in the bodies stage (`LaneOccupancy.Prefetch`, `PrefetchTheBody`). A hint, so exact.
- **Measured** (O30, 40 000 moving cars, device world): every car's tick 6.92 → 6.17–6.34 ms, alternated twice; with
  the chain and joins as well 5.91–5.96 (four and sixteen on: 5.93–6.19). `--bench room` drives 2.37 → 2.11 ms, bodies
  1.06 → 1.02. `DriveTheRoute` went from 0.85 to 2.2 instructions a cycle.
- **Where it lost or gained nothing**, and is not done: the answer (the road log's refusal stands — 0.51 → 0.51 here),
  the lights' stretches (0.36 → 0.37), a decision's car (1.43 → 1.40–1.44), the cut way's row a plan reads, and the
  body's hold alone.

## 2026-10-15 — a turn is runs of neighbours

**The owner (2026-10-15), in their words: "actors act in some offset groups, so they dont plan all at once" … "Try
potential improvementa".**

- **A turn takes runs of `TickGroups.RunOfMembers` neighbouring members, every `Groups`-th run**, where it took every
  `Groups`-th member: the agents, and a light's stretches. A member's row in every per-member array sits beside its
  neighbours', and a turn of every sixth brought every cache line of every array in to read a sixth of it. Runs and not
  one block a group, because the roster lays people before cars: blocks would put the walkers on some ticks and the
  cars on others, and the peak is what the stagger is for.
- **It is a change to the town**: which agents decide together moved, so every digest moved once. What it showed was a
  beat place drawn on the lane the patrol stood on, moved to the lane's middle where the patrol had stopped, and driven
  to round the block (`TownWorld.DriveTheBeat`, now never the lane it is on).
- **Measured** (O30 at 40 000 moving cars, device world): `--bench room` turns 6.59 → 6.27 ms, plans 2.75 → 2.41; the
  frame 17.45 → 16.99 ms with the two changes beside it that move nothing (a manoeuvre's later pieces swept once an ask,
  a car standing as laid not laid again). O10's `--bench age` car phase −1 to −10 % over the three windows. Sorting a
  turn's cars by their lane on top gained nothing (6.20 → 6.52 ms): what the claims miss on is a hold's chains.

## 2026-10-15 — the world aims each car's wheel down its line

**The owner (2026-10-15), in their words: "We were supposed to be doing some precalculation for this on gpu. Since it
already knows the car, position, lanes … Can it also do that lane projection? In a simple manner" … "lets do all logical
improvements. Ones that natually fit."**

- **The view's reading carries the wheel pure pursuit asks for** (`CarView.LeadAtM`, `CarView.AimRad`): the stretch the car
  follows sampled a lookahead past the reading, at the speed along its way the step left (`LineReading.Lead`), and the
  circle through it (`CarFollower.AimAt`) — in the same pass that reads it along the line, on the device and in the
  managed world alike, to the bit (`DeviceWorldTests`).
- **The driver takes it only where it is its own sum**: the metre it was aimed at equal to its progress and its lookahead,
  off a reading of the line in hand. A lookahead past the stretch — the line runs on past the next lane — is sampled
  on the host as before, so a town ticks the same to the bit (`--bench age` digests unchanged).
- **A manoeuvre at a bay is still read on the host**: its pieces are no lanes, and the world holds nothing of them to read
  along. Handing it them is a channel of its own, not a reading of what it already has (tech debt).
- **Measured** (O30 at 40 000 moving cars, device world, 30 s): the tick 18.4–18.5 ms against 17.6–17.9, the cars' own
  ticks 8.4 against 7.8–7.9, `sincosf` and `atanf` off the driving path.

## 2026-10-14 — a walker's walk and a light's hold are kept as a car's plan is

**The owner (2026-10-14): "Review the CPU bottlenet and continue improvements"**, on small maps while resources are short.
On O10 the turns were the largest share of the CPU tick, and walkers and lights the largest share of the turns — each
walker let go of its walk and asked it afresh at every turn, and each light its hold.

- **A walker's walk down its route is a plan** (`WalkingGround.KeepsTheWalk`, `WalkingItsRoute.PlansTheWalk`): kept while
  it is asked down the same chain by the same holder, let go of behind the walker's front half a body on
  (`Person.TrimSlackInDiameters`, so what it still holds behind is in front of its middle, PER-26), asked further where it
  needs more or what cut it has gone. A pass, a hop off the chain's end, a rejoin and a walk to a post are asked afresh.
- **Its metres are measured from a place on its chain**, kept beside it and moved on to the way the walker is on, as the
  walk itself measures them (`WalkingGround.WalkedAlong`); measured from the body, as an afresh walk is, every kept piece
  would move with it.
- **It is asked a slack past what it needs** (`Person.ClaimSlackInDiameters`, 1 m), never onto a crossing's paint it does
  not need yet — so the lanes under a zebra are held from when the walker comes to it, as before. Slack 0 kept almost
  nothing as it was (967 of 998 asked further a tick) and cost what asking afresh did.
- **A light's hold is kept while it would be placed as it stands** (`SignalHolds.Lay`): against, uncut, and ending at the
  same first body. Placed again every turn it woke every car waiting at its bar for nothing; `--bench trips` on O10 and
  Odesa came out identical with and without.
- **A plan held across turns must hold nothing it could not ask** (`LaneOccupancy.Take`, `CutHeld`): a piece refused where
  it begins takes no mark, and a cut that leaves nothing of a main claim lets its marks go. Asked afresh each turn, such a
  mark — a lane under a zebra with no ground of its holder's leading to it — lived one turn; kept, it headed its hold's
  claims, `TrimBehind` stopped there, and the hold kept everything its walker walked over (one of 48 claims). Eleven walks
  on O10 in a run held lanes so, and cars parked 2 % fewer and rerouted three times as often.
- **Measured** (Release, one machine, against 6078de5, back to back): `--bench room --map O10` 0.78 ms a tick against
  0.99 — walks 0.12 against 0.22, lights 0.05 against 0.10, answering 0.03 against 0.09; 872 walks a tick kept as they
  were, 123 asked further, 3 afresh. `--bench trips`, O10: 15 087 walks arrived and 15 087, 7 345 cars parked against
  7 338, 8 225 given up against 8 397; Odesa: 768 against 767, 84 given up against 80 (a 0.001 m/s change of pace moves
  O10's by ±40 and Odesa's by ±2), one car wrecked against none. `--bench drive`, O10: 9.98 m/s against 9.83, reds 21
  against 28, stuck 93 against 92, hard brakings 69 and 69; Odesa: 10.29 against 10.24, the rest the same. The exam fails
  nine cards: 69 and 70 pass.

## 2026-10-14 — no laying: every agent claims on its own turn and holds what it claimed

**The owner (2026-10-14), in their words: "The reason why persistant claims is the way that we want to go - is processing
offset. We dont want all actors to think at the same time" … "Well you dont do that anymore - never. Only actor plan
exist - they decide to release or prolong the claim on their own Nth tick" … "Its ok that actors dont react instantly -
neither do real humans / 0.1s reaction is more than enough" … "Dont do anything every tick. We need fix this by actor
planning better".** Their rulings: a body's ground is its holder's own; a car commits on its turn as far as it could not
stop short of until its next; a stronger rung takes held ground its holder has not committed to, committed ground and a
body's never, and of one rung what is held keeps; a plan is asked past what it needs by a slack. Kept claims were tried
twice before (2026-10-12 and 13, the road log) and taken out because every moving body and plan was still edited at
every laying; and a rarer laying was measured and not taken because a staler grant braked harder (2026-10-12).

- **A turn, not a laying** (TER-49, `TownWorld.TakeTheTurns`): the agents whose decision it is this tick decide, then
  claim — their bodies where the world reads them, their passes, manoeuvres and plans — then everything asked that
  tick is answered and each reads what it came to. Nobody else is touched; a cut between turns is read at the next. A
  line laid by the decision is planned down the same tick, or the car would drive a turn on no grant.
- **Every hold is its holder's until it lets go of it** (`LaneOccupancy.Release`): an actor that asks again lets go of
  what it asked last. A car's plan (`BeginPlan`) is kept and edited at its two ends — let go of behind the nose past
  `TrimSlackM`, asked further from where it ends where the car needs more or what cut it has gone (`AskFurther`,
  `IsWoken`), the further piece made one claim with the one it carries on; a hold cut at a mark is asked afresh.
- **Committed is a line a hold, not a rung on its claims** (`CommitTo`, `CommittedToDm`), read only where a stronger
  piece meets a weaker held claim: written on the claims, it cost what asking a whole plan again had.
- **A body is a hold of the body's own, its stretches held as whoever its ground is** (`PlaceBody`) — a walker going to
  its car is the car's ground, a car on a bar the truck's — moved in place over the ways it stood on, and laid again
  where those change; it cuts any other holder's plan on the ground it comes to.
- **Each actor plans for the whole of a turn.** A car commits what it covers until its next turn at the speed it may
  reach, and a stop from there, **but never past the grant it drives on meanwhile** unless it cannot stop short of that
  even now: unbounded, committed ground beat the lights' holds — a light's is whole or nothing — and O10 crossed 681
  reds against 21. A light holds where it is against now or will be before its next turn, to the first body on its
  way then.
- **Measured** (Release, one machine, against the laid tree, 8847e78): `--bench room --map O10` 1.62 ms a tick against
  6.9 ms every third — walkers' bodies are the one stage dearer, the rest cheaper. `--bench drive`, O10: 9.83 m/s against
  9.93, waiting 14 % against 15, stuck 92 against 86, hard brakings 69 against 120, reds 28 against 21; Odesa: 10.24
  against 10.32, stuck 6 and 6, hard 0 against 9, reds 3 against 0. The exam fails eleven cards where it failed thirteen:
  60, 80, 88 and 91 pass, 44 and 85 fail.

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
- **The world runs a step ahead of what the actors read** (`SimFigures.WorldStepsAhead`), at the owner's word: "implement
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
  actor layer reads every tick and nothing else — a body's covers' ranges come with the covers, and the step a car froze at is read off its flags (frozen, and not through the last step), since the town reads every
  view.
- **The tyres and a walker's footing are the world's**: they turn a car's wheel and pedals (`DriveCommand`) and a
  walker's heading and pace into impulses, so they are physics. Which ways a wheel grips on comes off the world's own
  reading of the ribbons.
- **The ribbons a body stands on are read by the world every step**, since some agent takes its turn to claim on every
  tick (TER-49) — so it needs no signal. What the actor layer adds to a cover — whether the body
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
  after it, it is laid in what the atlas freed. **Where the device files the atlas, the ground is laid beside the
  world** (2026-10-26, `GroundMesh.BuildAside`): that peak is not the host's, and Highmoor's open on the desktop no
  longer waits the mesh's 5 s after the world, its peak resident 3.0 GB. A page has one thread and lays them in turn.
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

Retired, and named by nothing but the ID map: `PUR-1`…`PUR-4`, which restated [goals.md](goals.md) sentence for
sentence; `AGT-1`…`AGT-4`, `OBJ-1`, `OBJ-3`, `SIM-5` and `TEC-3`, which were definitions, pointers to other rules, or a
constraint on a grid the terrain has never had; and `VER-4`, `VER-5` and `VER-7`, intentions no tier answers and that
`VER-3`, `SIM-4` and `SOL-35` already carry.

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
