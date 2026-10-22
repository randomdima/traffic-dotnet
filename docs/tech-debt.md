# Tech debt

**What is known to be wrong, slow, redundant or unmeasured in what is built, and left so.** Each entry says where it
is, what it costs and what closes it; one fixed is deleted, not struck through. Why something came to be is the
owning slice's `decision-log.md`; an absence big enough to be structural is a
[known gap](index.md#known-gaps), and what is unbuilt is the instruments' to report ([CLAUDE.md](../CLAUDE.md)).
**An entry marked _owner_ waits on a ruling** and is not to be closed without one.

## Failing tests

- **`WayIntentsTests.EveryHolderUnderWayStatesWhereItGoes`** (town) fails: "Walking 29 is under way and states nothing"
  (2026-10-19). Not the railway loop's: it fails as well with the suite's line laid straight, the old train and rail
  figures and the ballast answered over the walk. Not traced; which of `WalkingGround.StateIntents`' two pieces comes
  back empty for walker 29, and why, closes it.
- **A plan draws back with nothing cutting it, under one fleet** (`PlansInATownTests.APlanNothingCutNeverDrawsBack`,
  map "Laid", 2026-10-22): with batch 1's ten cars out of `Fleet.json` it failed, "tick 2764, car 42 drew its plan back
  4.77 m with nothing cutting it, doing 0.1 m/s"; with them in, as now, it passes. No code moved it: the fleet's mix
  hands car 42 a look whose size reaches a plan bug the present mix does not. Tracing why that plan draws back closes
  it; the fleet as it is only hides it.
- **`TownWorldTests.LettingGoOfTheWheelLeavesTheCarWaitingUntilTheReset`** (town) fails: "a car let go of drove on along
  the leg it had before the hand" (2026-10-19, the hand's change in hand). Closed by that change.
- **Four tests fail since the town's arithmetic left libm** (2026-10-20, core log); all four pass on 79dddfaf:
  - `PedestrianWaysTests.ALaneWeldedToOnePointIsTheOnlyOneNothingIsLaidDown` and
    `PedestrianNodesTests.APointStandingOnThePointItsLaneArrivesAtIsOnePointMidwayBetweenThem` (unit, WLK-12): "the
    fixture town welds no lane at all". The fixture had one weld, node 62's lane 1 (983.2, 858.1), whose point is where
    two all but parallel kerb lines meet: the roads' headings a float apart (`citygen/gen`'s `Atan2` and `Asin`) move it
    0.4 m, and its pair now stands 1.083 m apart against `Road.FootConnectorMergeM`'s 1 m. The plan's junctions are the
    same to the bit. Each asks the fixture for a weld it no longer lays; a fixture that welds by its own shape, or the
    point found by a better-conditioned construction than a near-parallel meet, closes them;
  - `PersonActionTests.AWalkerInTheRoadClaimsTheStretchOfItsWayItStepsBackOnto` (town, PER-25): the walker put on the
    nearest street lane's middle is read as walking and not rejoining. Not traced; which walker and lane the probe now
    picks, and why the world does not read that body as in the road, closes it;
  - `WalkingTests.ABodyOnNoWayOfTheNetworkPlansNothing` (town, PER-26): "walker 22 is on no way of the network and
    plans 4535" — the rule broken in the settled town, as the `WayIntents` entry above is. Not traced.

- **`StopBarsTests.ABarStandsASetbackBehindItsCrossing`** (unit) is skipped, failing: the worst bar's gap is 0.0426 m off the setback
  against `FramesM`, a 0.04 m bound measured on the fixture. It fails on 5a973d8, 2b8936c7 and 9f5c05af (2026-09-30)
  alike, so the fixture outgrew the measurement before the claims redesign. Finding which bar and whether its arm's
  bend accounts for it — then re-measuring the bound or reading the gap in the lane's own metres — closes it.
- **`AllocationGateTests.AWholeTickOfAWholeTownAllocatesNothing(Laid)`** (perf, `P0`) failed once at 120 B over 600
  ticks on 2026-10-16, with the machine at a load of ~25 and the test host already on the shipped collector; it has
  passed every run since, five in a row on a quiet machine. What allocated under load is not known; an allocation
  trace (`dotnet-trace`'s GC allocation ticks) of a failing run closes it.
- **Nine exam cards fail** (2026-10-16; 60, 82, 85 and 86 pass since a car short of a box plans none of one it could
  not get out of, and yields what it holds there at rest, the road log):
  - 110, failing on 79dddfaf as on the tree after it (2026-10-20), and not looked at.
  - 39 and 44 (the nearer of two straights goes first; four left turns at once), failing on 2026-10-18's working tree
    with the level-crossing card taken out of the table as well — so they moved with the uncommitted car work and not
    with the railway, which the exam map otherwise lacks. 39's driver 0 is on the box 0.1 s after driver 1; 44's driver
    0 never leaves the box. Not traced.
  - 69, the standing tier (road log, the claims redesign). Not traced.
  - 70 passes on that tree (2026-10-18), and its entry below stands until it is seen to pass on a commit:
  - 70 (a left turn on green gives way to somebody crossing): the car now waits at the box's mouth until the walker is
    off its lane, then crosses the paint behind them; its committed plan takes the paint whole (TER-5c.3), the walker
    stops a second, and their crossing turns red with them 0.7 m short of the far kerb, cut by "light stretch 40". TLT-2a
    walks a walker already on the paint off it, so whichever stretch 40 holds is laid over ground in front of them.
    Before, the car stood on the zebra beside the walker the whole crossing and the card passed. Tracing which way
    stretch 40 holds, and from which end, closes it.
  - 108 and 109 (getting past a car stood in its lane; a call past a queue at a red): no pass is decided — the
    "nobody reads what a body is doing" entry below.
  - 84 and 89 (a car gives way on a zebra): the car stops behind somebody standing at the kerb
    end of the paint, inside the zebra, and is on it when they cross. The room once kept behind a body going nowhere
    held it short; the same entry.
  - 67 (a left turn on green gives way to the oncoming right turn): driver 1 never gets where it was sent. It failed
    before the redesign too, and nothing has looked at why.
- **Sixteen town tests are skipped** for the decisions no claim can make any more: all fourteen of
  `OvertakeInATownTests` (CAR-46, CAR-50, PER-28) and two of `LaneSwitchInATownTests` (getting past a wreck and slower
  traffic, CAR-53). They come back with the entry below.
- **`BuildingsTests.NoBuildingFrontsTheRoundingOfACarPark`** (town, GEN-55) has failed since the world's trigonometry
  became `Trig` (2026-10-15, core log; it passes on 5a973d8): `Heading.Unit` lays the town as well as turning it, so the
  suite's town stands a float off where it stood. Since the buildings grew (2026-10-20) building 163 stands 10.4 m off
  the middle of a rank reaching 10.8 m with 2.6 m of frontage either side — 2.2 m onto the rounding, where it was 0.1 m.
  Not traced: `ZoneBuildings.Stand` asks `Yards.Allows` of the place before `Face.Seat` moves the building to the
  face's true nearest (GEN-15), which may be the whole of it; asking it of the seated wall closes it if so.
- **The kernels have not been held to `Trig` on a GPU** (2026-10-15): `TrigKernelTests` and `RoundedKernelTests` pass
  on lavapipe, which the suite runs. The device tier pointed at the RX 9070 XT by hand closes it.

## Regressions and figures not taken

- **Five cars stand outside the fleet on a magenta-cut picture** (`assets/agents/car/variants/`: `crossover_pearl`,
  `estate_steel`, `fleetsaloon_white`, `kombi_sage`, `midsaloon_black`; 2026-10-21): cut from the magenta
  `cars_fleet_a-v3`, their edges carry a faint magenta fringe, and all but `fleetsaloon_white` show no lamps. Each is
  asked again on a transparent sheet in its `fleet100` batch (`.tmp/gen/fleet_plan.md`: batches 5, 6, 7, 10), which
  closes it.
- **Two cars keep their far side's lamps as drawn** (`citycar_lime`, `cityhatch_silver`, 2026-10-22): their lower
  tail lamp is drawn as another lamp than the upper (a longer arm, elsewhere), and `qq props mirror` left a second red
  shape beside the reflection however far it followed the drawn lamp, so both stay on their unmirrored pictures with
  the lamps cut on them. Painting the copy side's lamp out before the reflection goes on — an in-paint of the body
  there, not a recolour of lamp-like texels, which took windows and red paint (tried) — closes it.
- **`mirror` always paints the lower side from the upper** (`qq props`, 2026-10-22): the authored side is the upper
  half, and where the lower draws a lamp better — `retro500_cream`'s rear indicator, orange below and dark red above —
  the better one is painted over. An authored side chosen per car (`"authored": "lower"` in the props file, the
  reflection the other way) closes it.
- **Nine earlier looks are on disk and in no fleet** (`assets/agents/car/variants/`: `citybox_sky`, `estate_navy`,
  `hatch_coral`, `micro_mint`, `minivan_wine`, `panelvan_forest`, `retro_cream`, `saloon_beige`, `suv_graphite`;
  2026-10-22): trial cars the hundred-car plan does not ask again, so nothing draws them and their raw_assets
  lineage is kept for nothing. _owner_: deleting them with the legacy looks below closes it.
- **A variant's `handling.braking` moves nothing but the pedal's ramp** (`CarBuild.Resolve`, 2026-10-22): the brake
  is `CarBrakingMps2` (three of the road's grips) times it, and every planned stop is the lesser of that and the
  car's own grip, so across the fleet's 0.72–1.2 the tyre always decides and the figure reaches only
  `PedalRateMps3`. The plan's worn cars author weaker brakes that stop as short as new ones. Dropping the figure, or
  a brake authored as a raw term that can bind, closes it.
- **Sixteen legacy looks still stand in `Fleet.json`** (2026-10-21) beside the new fleet's first batch, so a town is
  not all small cars while the hundred are drawn: `muscle_orange` … `pickup_maroon`, art on the legacy list, lamps
  by rectangle. Dropping them once the new fleet spans every size closes it, and with it the entries below on the
  legacy looks.

- **The `debug-collision` scenario frames bare grass** (`tests/e2e/scenarios/InterfaceScenarios.cs`, `Test` at 270, 60):
  its subject is "a lit T junction at close range", and the frame holds scenery and no body at all, so the picture says
  nothing about collision shapes — on the tree before the engine layers as after (2026-10-19). Framing it on the
  junction the `Test` map now lays closes it.

- **A parallel park's tail swings over the kerb and its ground does not cover the pavement** (`BayManoeuvre.ParallelIn`,
  2026-10-17): backed in with the nose turned out, the rear corner stands ~0.25 m past the roadside's kerb edge, and the
  manoeuvre asks only the lane and the roadside — a walker there is met by the world. Sweeping the pavement way beside
  the roadside into `BayManoeuvring.StreetUnder` closes it.
- **Two kerb parkings in 978 stand crooked** (OdesaOsm, 120 s, up to 4.3°): their straightening was refused and is asked
  once. Asking it again on the manoeuvre clock for a short while closes it.
- **Parkings fell 6 % with the walking pace** (1 052 → 992 over 120 s on OdesaOsm), and a kerb gap of 1.25 m stands
  fewer cars along a kerb. `qq tests maps` and `--bench drive` were not taken since; taking them says whether either
  cost a shipped city a claim.
- **A car pulled out from a kerb was handed to a lane 2.2 m off and lost its line** (car 2111, one OdesaOsm run of
  2026-10-17, `ShapeTheWayOffTheKerb` lands on the lane beside whatever lane the route set off down). Not seen in the
  run after; checking whether `TownWorld.LeaveTheBay`'s lane and the shape's can differ at a kerb closes it.
- **OdesaOsm lays a pavement on its railway's ballast** (`RAIL-4`; `--bench trains --map OdesaOsm`, 2026-10-19): where
  the traced main line runs beside Балківська вулиця (about 1275, 12600), the street's imagery width (12–13 m against
  its tags' lanes) puts its east walk 1.8 m off the track's centre, and walkers on it are met by passing wagons at no
  crossing — two in 400 s. A traced town's walks are laid off its roads and never asked of the ballast, as a generated
  town's nodes are (`WaterRules.Holds`). Trimming a walk where it runs onto railway ground, or narrowing a road to its
  tags where the ballast would be reached, closes it.

- **Laying the paint costs three times what it did on OdesaOsm** (`--bench census`, 2026-10-17: 116 → 373 ms of a
  2.8 s ground): every lane and roadside line is asked of the painted bands a crossing's depth at a time, to part it
  round any it runs under (`GroundMesh.LineRound`). Asking only the cells a line's bounds reach, or the line once per
  band in its cell, closes it.

- **Most of Odesa's open is one stage nothing has broken down** (`--bench load`, 2026-10-16): fleets, the tables they
  are numbered in, the roster and the spawn take ~795 ms of the 1 150 ms open — "fleets and places" in its timeline —
  for a town of 12 agents at that stage and 2 494 static bodies. A profile of that stage (`qq prof --load`) closes it.

- **A town of 40 000 moving cars ticks at or past real time on one thread** (device world, 16 cores, RX 9070 XT,
  2026-10-14): OdesaOsm 18.9 ms a tick in its first minute, not taken since; O30 at 40 000 cars with every owner set off at the wheel
  (`Person.StartsAtTheWheelShare` 1, a root under `.tmp/`) now keeps up: the main thread is busy 13.9 ms a frame
  (2026-10-15; 15.9 that morning, 17.0 a frame before a turn took runs of neighbours, root log), so the window runs at
  the display's 59–60 fps, one tick a frame (SIM-9), and the main thread's busy time and not the frame is what a change
  moves. The device steps in about 1.7 ms and is 11 % busy; it waits on the host's tick, on the main thread alone at
  1.6 instructions a cycle. Every tick every car's own tick is 6.0 ms (7.3 before a car waiting at a bay took its tick
  again unread and the fleet's walk fetched the eighth car on, 8.4 before the world aimed the wheel); every tick a sixth
  of the agents take their turn, 6.2 ms — claiming 4.3 and answering 0.4, deciding 1.5 (a free-bay hunt passes by the
  bay lanes with no bay free, `FreeBaysNear` 0.35 → 0.15 ms; a car waiting to pull out sweeps its shape once,
  `PullingOut.Decide` 0.37 → 0.06); the device's begin and read 0.6 and the watch 0.5. `--bench room` times the turns at
  5.71 ms (6.59 that morning). Run to run the tick moves by ±0.3 ms, and `--bench age` on O10 by ±3 %. The turns wait on
  memory, not arithmetic: the claims' own methods 0.2–0.8 instructions a cycle at 10–47 last-level misses a thousand
  instructions, chasing a hold's slots and owners through some 40 MB of tables; fetching the next cars' holds ahead
  took 0.3 ms of it (root log). Visiting a turn's cars in the order of the lanes they stand on gained nothing (6.20 → 6.52 ms
  with the sort), so what misses is the chains and not the lanes' rows. A shipped town's own figures say
  little: OdesaOsm is free-running empty cars that jam within three minutes, and O30 as authored is 95 % parked.
  Running the cars' ticks and the turns over workers, with per-worker signals merged in agent order and nothing
  allocated (`P0`), closes it.
- **On Odesa the device world is slower than the managed one** (`--bench deviceworld`, RX 9070 XT, 2026-10-12): a
  minute takes 1.4 s against 0.9 s, with the same town to the body.
  - A small town cannot fill the device, and the host pays 0.22 ms a tick to record and submit the step.
  - The tick is recorded afresh each time (`DeviceWorld.Begin`, `DeviceSolver.Record`), some 200 Vulkan calls. The
    recording differs from the last only in:
    - its push constants;
    - which of the signals' passes, the pull and the covers run;
    - which view's rows it writes;
    - which way round the touches' lists are.

  Two or four recordings laid once, with the per-tick figures in a small tick buffer and the signals' passes
  dispatched indirect off their counts, close it.
- **Hard braking rose with the wheel claims**: 714 → 1 135 in 600 ticks of the device town then probed, 2 100 → 3 382
  under `--bench drive`, and pairs beginning to touch 15 → 21. A wheel read at the lattice point nearest it puts a car
  on a lane its collider does not reach, within 0.35 m of a band, and the traffic on that lane brakes for it. Proving a
  wheel inside a band instead braked harder still (road log). Nothing closes it yet.
- **What OdesaOsm's laying holds on the device is not broken down past the solver's step**: 1 365 MB device-local and
  87 MB mapped, of which the step 310 MB (`--bench deviceworld --map OdesaOsm`, 2026-10-11). The rest — the atlas, the
  tyres, the views and the covers — is a gigabyte nothing has itemised. The probe printing each part's share closes it.
- **The pairs held between frozen bodies are walked in the stride of the contacts' workgroups** (`touches.comp`): a
  step's held pairs are taken a block of 64 at a time by as many workgroups as its contacts need — one, on a step with a
  handful — so a town holding thousands of frozen touches walks them in one workgroup, twice a step. How many a shipped
  city holds is not measured; dispatching that walk off the held count closes it.
- **The device world allocates once, about a hundred bytes, some seconds past a 600-tick warm-up** of the built town
  (lavapipe, 2026-10-16), and nothing in the 2 400 ticks after; no field of the town or the world grew on that tick, so
  it is the runtime's or a static's first time. `DeviceAllocationGateTests` warms past it as the managed gate does
  (`Towns.SettleTicks`). An allocation trace of that tick names it.
- **The page's device world** (2026-10-15, web log), each found standing it up:
  - **no tier runs it**: `qq web` on River, Odesa, OdesaOsm and O30 is the whole of what has looked at it. A page's
    managed world is the desktop's to the bit — the plan's and the bodies' digests at tick 600 are the desktop
    `--shot --seconds 10`'s on Odesa and OdesaOsm (2026-10-20) — but its device world is not, standing millimetres off
    its managed one by tick 600 on Odesa: WGSL cannot forbid a driver to fuse (core log), and `fma` in `world_view.comp`
    may run unfused. A tolerance probe of the page's device world against the managed one, driven by `qq web --ask`,
    closes it;
  - **what moves is drawn off the CPU's fill and no tyre mark is drawn**: `sprites_count.comp` and `sprites_write.comp`
    bind 19 storage buffers, and the marks' ring is never read back. Packing the emit's tables as the atlas's were, and
    a web half of `DeviceSprites` feeding `TownRenderer.Web`, closes it;
  - **every touched host buffer crosses whole every batch** (`ComputeBatch.Web.cs`): the signals, the resident rows'
    staging, the views and the covers' lists, on OdesaOsm 677–851 MiB through `writeBuffer` in six seconds at about
    80 submits a second (River 37 MiB; a hook on `GPUQueue` through `qq web --ask`, 2026-10-16), where only the rows
    marked or written need to. In the first seconds stood it is 1–13 fps with 74 ms of each frame waiting. Sending marked runs (`ResidentRows`) and reading back the counted rows closes it;
  - **covers that ran past their lists leave that view without covers** (`DeviceWorld.Finish`): the desktop reads
    them again and waits; a page goes a tick without the turns those covers would have fed;
  - **a fill of any word but nought makes a uniform buffer and a bind group each time** (`compute.js`'s `fill`), and
    never frees the buffer — twice a town laid (the solver's `_lowest` and the atlas filing's `refused`), not a tick. A
    fill pipeline holding one window per fill closes it;
  - **the page's Debug build stands River in three minutes**, interpreted; only a published page is worth timing;
  - **the Pages build translates the kernels with another glslc than the one they are written against**: Ubuntu's
    apt package in `pages.yml`, years behind this machine's shaderc 2026.1. Its SPIR-V left `touches.comp`'s
    read-only `WasCount` writable in naga's WGSL, the browser refused a barrier behind a loop on it, and the deployed
    page stood every town with no step (2026-10-15). Nothing after the deploy asks whether a kernel parsed:
    pinning the same shaderc in the workflow, and a `qq web --url` of the deployed page that fails on a WebGPU error,
    close it;
  - **nor does `qq web`**: `rail_track.glsl` wrote infinity as `uintBitsToFloat(0x7f800000u)`, naga folded it to a
    literal the browser refuses, and every page stood with `world_rail.comp` invalid and no world step until
    2026-10-19 — found by reading a shot's log, not by anything failing. The web build now parses every kernel back
    through naga as the page compiles it (`Kernels.Web.targets`, since Firefox refused `world_view.comp`'s `lineNext`
    and `tyre_pass.comp`'s push block on 2026-10-20), but naga is Firefox's validator and not Chrome's, and nothing
    runs the page in Firefox. `qq web` exiting non-zero on a WebGPU error, and a run in Firefox, close it;
  - **the published page lost its `_framework/`** between a publish at 00:40 and 00:53 on 2026-10-20, with only
    `qq tests device` and `qq tests unit town` built in between, so a `qq web --no-build` would have served a page
    that cannot start. Not traced; which build clears `bin/web/Release/net11.0/publish/wwwroot/_framework`, and
    `--no-build` refusing a bundle without one, close it;
  - **nothing asks whether the device can hold the town's tables**: a buffer past the adapter's
    `maxStorageBufferBindingSize` is a WebGPU validation error and a dead world rather than the managed world WEB-10
    promises. This machine's adapter gives 4 GB; a default device 128 MB, which OdesaOsm's atlas (930 MB) is past.
    Weighing the laying's largest table against the limit before the handover closes it.
- **A traced city's open on the page** (2026-10-15, root log; OdesaOsm 40–43 s at 1.87–1.95 GB of 4, the figure varying
  with where the renderer's last tables find room; O30 170 s at 3.6 GB, not taken since its atlas was filed by the
  device):
  - **OdesaOsm's plan is 21.8 s of the page's 43** (`qq web --cpu`, 2026-10-15, after the marks went to the device),
    single-threaded arithmetic at about twice a desktop core's. Inclusive: the walk's outer face (`Paving.Rings`) 8.5 s,
    most of it the band shells' outsets and their merge; the zones' buildings 6.8, of it `ZoneBuildings.Face.Nearest`
    1.8; the props 2.6; the traced streets 2.7; `CrossingWays.Of` 5.0, of it `OntoThePaint` about 3.8;
    `PavementLanes.Of` 2.5; the ground's mesh 1.8; the marks laid out for the device 0.4 and filed off its rows 0.2; the
    interpreter 0.5. By self time nothing passes 6 %. **`CrossingWays.Reading.OntoThePaint` reads a walk's band a touch
    apart along it** — two windings a station — and steps the course a centimetre at a time for the first metre a turn
    leaves from (`DepartsAt`): 5 % of the desktop's open CPU. Sharing the course's stations between the mouths of one
    place was tried and moved nothing, few places having two; a station far inside the walk's face that skipped the
    stations the face could not reach before would. A page given threads (cross-origin isolation, the host's two
    headers) is the lever past the rest;
  - **the device's marks are read back whole**: rows laid for twice the pieces (a traced city's 45 MB), all of which a
    page copies back, where 658 000 used are 16 MB; and a town that finds more rows than that runs the pass again, which
    no town has been seen to. Counting before writing, or reading back only the rows counted, closes the first;
  - **a device's marks cannot be read off its world for a digest**: `--bench deviceload` prints the atlas's but not the
    marks', which `MarksKernelTests` holds to the host's within a millimetre but for touches;
  - **OdesaOsm runs at under half real time on the page** (deployed and local, 2026-10-16): 28.5–29.0 ms a tick, of
    it the cars' own 25.9–26.3, so 0.3–0.4 ticks a frame at 57–60 fps (27.2 ms and 0.5 on 2026-10-15) — the desktop's
    6.0 ms car tick on one thread, at the page's single core and about twice a desktop core's cost. The cars'
    ticks over workers (the desktop entry above) wants a page given threads, which wants cross-origin isolation and so
    headers GitHub Pages cannot send;
  - **the open's figures above were not taken again since the town left libm** (2026-10-20, core log): a page laid
    OdesaOsm's plan in 39.4 s and its town in 31.7 (`?managed-world`, one run), where the desktop's plan is 14.1 s
    before and 14.2 s after. Whether the page's 39.4 is `Trig`'s and `Powers`' cost under WebAssembly or where the
    page already stood on 79dddfaf is not known; `qq web --cpu` on OdesaOsm on both trees closes it;
  - **a page's atlas can stand a float off the host's**: WGSL cannot say `precise`, so a browser may fuse what the host
    rounds twice (`trig.glsl`), and a page has no read-back to hold its digest to the host's. Vulkan's is the host's to
    the bit;
  - **a page's atlas is finished only while its clock is due a tick** (`Game.Advance` asks `WorldLayer.Ready` only
    then): a town opened paused holds the counts' read-back, a word a main cell, until it is let run;
  - **the page keeps what the desktop frees inside the town's lay**: each stage's figure on the page's console
    (`OpenWatch`) has the page using 0.3–0.5 GB more than the desktop after each of the lay's collections, which come
    from frames deep in the lay; the one from the lay's own top frame (`LaidTown.Lay`) frees as the desktop's does.
    Whether it is a standing frame's stale slot or the collector's lazy sweep is not known;
  - **the open's longest stages are each one block**: the plan's ground and buildings and the town's pavement are
    about 10 s each on OdesaOsm, through which the card's bar moves on the compositor at the pace guessed so far while
    its words stand still. The bar's shares are OdesaOsm's (`loading.js`), so another city's bar moves unevenly; shares
    the page learns from each open close that;
  - **the bar's moving through a block is not seen by any tier or tool**: `qq web`'s picture waits for the page's
    thread, so it is taken between stages and never in one.
- **Nobody holds what no wheel stands on**: across a lane's end a car on two ways leaves its wheelbase less two
  metres and two half steps unclaimed; a car straddling a zebra narrower than its wheelbase, or nosing over a junction
  its front wheels have not reached, is no body there. Measuring the gap per car model closes the reading.
- **The Android head has not been built since the device tick came in** (branch `gpu-resident-sim`): no Android SDK
  where it was worked on. It compiles all of `src/` against Vulkan, so it takes `app/devicetick` as the desktop does,
  and its shader targets must embed the device world's kernels. A build of it closes this.
- **The solver's damping is an undeclared margin the driver depends on** (`SolverFigures.LinearDamping`,
  `AngularDamping`): about 1.4 m/s² of drag at 14 m/s that no stopping figure, committed ground or top speed is derived
  with. Set to nought, hard brakings rose 6 → 569 on Exam and 121 → 584 on O10 and reds 2 → 8 on Odesa (physics log,
  2026-10-12); a wider braking margin or a higher brake ceiling did not stand in for it. Finding what the profile
  overruns without it — the hazards were grants cut by a body across the lane and cars creeping at the stop speed into
  their grant's end — and retuning the driver on a drag-free world closes it.
- **A car whose line ends at a red's bar can be committed past the bar by the time its line grows past it**
  (`TownWorld.LayTheRestOfTheLine`, `Following.Plan`): the plan stops at the line's end, so the light's hold just past it
  is not met, and the line end binds the car only to stop at the end itself. On River (`--bench drive`, 2026-10-12)
  car 81 at 22 m/s had its line end 0.01 m past a bar; when the line grew on the next decision its committed ground
  already reached 2 m past the bar, so the light never held it and it crossed 4.6 s into the red. Growing the line
  before its end comes within the car's committed ground and a stand-off, or binding the line's end as the plan's end is
  bound where the line will grow, closes it.
- **The device world throws on a step that overflows its candidates, its contacts or its woken bodies' contacts**
  (`DeviceWorld.Finish`, `DeviceSolver.Overflowed`) rather than growing them, as it does for the pairs begun touching.
  The room is 16 candidates and 8 contacts a moving body, and one woken body's contact for every four; OdesaOsm's
  80 013 bodies came to 3 614 candidates and 22 contacts at most over 240 ticks (2026-10-11), and nothing in the suite
  comes near it, but a town dense enough to fill it would stop. Growing the room between steps — the candidates up to
  the scan's own ceiling, `ScanKernels.MostCounts` — closes it.
- **A walker on Odesa stands still for minutes off its line on a zebra's paint** (2026-10-11, the age probe's town):
  walker 420, walking to its car, stands at (1614.0, 765.9) on crossing 129 from about tick 6 000 to past 18 000, 2.00 m
  off the way it walks, its plan refused by the crossing's light about half its decisions, and its walk given up on once
  and drawn again to the same car. What holds it there and why its leg's clock does not end the walk is not known.
  Others did the same for shorter stretches (walker 160, minutes 27–33). Finding what refuses it closes it.
- **An owner can be kept at the wheel for over a minute in the suite's built town** (person log, 2026-10-02): two
  owners stayed seated because every exit spot near the door was within their own car's panels or held by someone else.
  The door point is a person's breadth off a nominal car's flank, which a wider car reaches past. Checking that the
  spots past it lie on the next bay's way closes it.

## Redundant work left in place

### A car's tick

- **A car's tick works out again what only changes at its turn** (2026-10-15, O30 at 40 000 moving cars: the cars' own
  ticks 6.0 ms of the main thread's 13.9 a tick). Of `CarFollower.DriveTheLine`, `TownWorld.ReadTheLine` and the gate
  round them:
  - the walk of the arcs ahead for the tightest and the `sqrt` of the next one's entry figure (`TargetSpeedMps`), fixed
    while the line is;
  - the grant, the place and the wait-at, each a stop metre a lead ahead, which share one lead and one braking;
  - `ToTheSceneM` up to three times a tick (`SetTheContext`, `TicksAsBefore`, `KeepTheTick`), a projection over twice the
    sight for a car on an errand;
  - `LineReader.RunsShort`, the lost-the-line test and `IsOffTheLaneOfItsPass`, which only a decision reads or which only
    change at a lane handed over;
  - `CarLamps.Showing` past the brake bit, and `TurnAheadRad` inside a junction's reach;
  - `StandsParked`, `TicksAsBefore` and `KeepTheTick` over every car, parked or not, where a list of the woken would do;
  - the speed along the line, which the world's view already holds, and the cursor walked on (`Spline.MoveTheCursor`,
    0.3 ms) only so the speed profile's walk of the arcs begins where the world's reading already is;
  - about thirty per-car arrays read a tick, where one record written at the turn would be one line of cache.

  `Drive`'s own time is spread over its whole body, no block over a ninth of it, so each item is worth tenths of a
  millisecond; the record is the largest. Each moved to the turn, with `--bench age`'s digests equal, closes it.
- **A car manoeuvring at a bay is read along its piece on the host every tick** (`BayManoeuvring.DriveThePiece`, about
  0.3 ms a tick on O30 since a car waiting at its bay takes its tick again unread, 1.6 before): its pieces are no lanes, so it follows nothing the world reads (`LineFollowed.None`)
  and the host projects it from the piece's first arc and aims its wheel itself. A car following its lanes is read and
  aimed by the world (root log 2026-10-15). Handing the world a piece's arcs when it is laid, for the view to read along as
  it reads lanes, closes it.
- **`BayManoeuvring.Lay` sweeps the piece a begun manoeuvre is on at every turn** (`StreetUnder` 0.3 ms a tick on O30,
  `sincosf` 0.18 more, 2026-10-15): from where the car stands on it, each station a flank on from there, so no two
  turns sweep the same stations. The pieces after it are swept once an ask (`SweptWhole`), which on O30 is little —
  most manoeuvres laid are on their last piece. Stations on the piece's own grid from its start, the ones the car is past
  dropped, would be the whole-piece sweep cut short and kept from the ask; it moves where a manoeuvre's ground begins by
  up to a flank, so it is a change to the town and not only to the cost. Kept from a turn the car had not moved since,
  exact, it gained nothing (`--bench room` passes 0.90 → 0.90 ms): a begun manoeuvre is a car moving.

### The world layer

- **The ground is painted three and four deep on a road's pixels** (`TownRenderer.Record`): grass, walk, carriageway
  and paint are drawn bottom to top over each other with no depth attachment, and the frame's pass has none to give an
  early depth test — drawing the parts top first at a depth apiece would spare the overdraw, at the cost of a depth image
  and of the blended marks and sprites left to sort against it. Nothing on the desktop measured; a handset's fill rate
  measured, and frames compared to the pixel, closes it.
- **Paths kept for the device's tests alone**: `DeviceTyres.Step` and its read-backs and scrub (`TyrePassKernelTests`,
  `handsBack`), and `DeviceSolver`'s laying from a host table each step — `Lay`, `Step` and `RecordReadBack(everything)` —
  and its lists of what woke and froze (`ListsWhatSettled`; `DeviceSolverTests`, `DeviceSettlingTests`). `DeviceWorld`
  drives none of it. Holding the kernels to the host through `DeviceWorld` (`DeviceWorldTests`) instead closes it.
- **The managed world's three broad-phase grids each keep a dense table over their window** (`CellGrid`): 12 B a
  cell and half as much again for room, so on OdesaOsm's 3.9 million cells some 70 MB a grid, though the moving and
  frozen sets reach a few hundred thousand of them. A moving and a frozen grid hashed to the cells their bodies reach,
  each body filed once a bucket as the device's moving grid is (`step.glsl`), closes it.
- **The device's per-body buffers are sized for every body, the furniture's 400 000 rows among them**
  (`DeviceSolver`'s push, overlap, motion into the tick, rest, state and lowest): about 19 MB on OdesaOsm that no
  furniture row uses — and the staging rows the table is laid from (`_poseIn` and the six beside it), 35 MB of mapped
  memory written whole once and only the moving rows after. Indexing the first by moving row (`body − _firstMoving`) in
  every kernel that reads them — the solver's, the view's two passes and `world_signals.comp` — with the reads a furniture
  row answers guarded (a static second's push, motion into the tick and state), and laying the furniture's rows through
  a staging buffer freed after, closes it.
- **What the host keeps on a device-ticked town that it does not read while the town runs** (OdesaOsm, `qq prof
  --memory`, 2026-10-13: a 685 MB host heap, 839 MB resident and 3.36 GB at the load's peak, beside
  1.44 GB on the device):
  - **a map picked on a desktop's menu still stands a `ManagedWorld` and the whole host atlas** and hands them over
    (`Game.Vulkan.OpenWhatWasPicked`): it is laid off the loop's thread, which may not submit to the device, so it
    cannot have its world made as it is laid (`IWorldMaker`). The open's peak there is the 2.5 GB the command line's
    open no longer pays (1.55 GB, `--bench deviceload`). Making the maker's device work wait for the loop's thread
    closes it; the exam map (`Scenarios.Stages`) is handed over the same way, and is small;
  - the nearest-point indexes kept for a player's order and the camera's opening alone (`RoadGraph`'s street indexes,
    43 MB; `FootGraph`'s, 33 MB; `ParkingRegistry`'s bay grid), which the 2026-10-13 ruling lets a player use — laid
    again on the first order rather than kept closes it;
  - the marks in metres (`WayCrossings`, 31 MB) beside the claims' own in decimetres, for `CrossesAZebra`,
    `Overtaking` and `Closure`;
  - the host's tyre-marks ring (`DriftMarks`, 3 MB), which the device world never fills.
- **Two figures moved with the lanes, measured and not yet weighed** (Odesa, `--bench stuck`, 300 s, 2026-10-13, the
  build before against this one): trips drawn down the road and not over the map arrive more often (walks 407 → 770,
  given up 201 → 99, drive legs given up 10 → 0, touches 104 → 19) — shorter, so whether a town's trips still cross
  it as PER-9 means is not measured; and a manoeuvre's street read as its lanes' stretches is 17.6 → 36.0 m a
  manoeuvre, which is the shape's choice's figure and not a fault. A trip-length distribution against the build
  before closes the first.
- **`TownWatch.Saw` runs every tick in the live game** (OdesaOsm on the device, 2026-10-14: 5.3 ms a frame), an
  instrument. What it can skip without missing a claim, which is answered over runs of ticks, is not worked out.
- **A walker is still placed on its way by the host** (`WalkingGround.PlaceItOnItsWay`, every tick): a projection
  onto its way's arcs around the metre it held, as a car's was before the world read it along its line (root log,
  2026-10-14). The same — a walker naming its way, the world reading it there with the view — closes it.
- **A car is still projected on the host the tick or two after its line is laid again** (`ReadProgressM` NaN until a
  view read along the new follow arrives, two steps on), and every tick of a line with no lanes (a manoeuvre's pieces)
  or a pass's aim. A follow signalled with the reading's own shift, rather than waited for, closes the first.

### Claims

- **A walker's body costs more to keep than to lay afresh** (2026-10-14, root decision log): `--bench room --map O10`
  0.26 ms a tick for a sixth of the walkers against 0.70 every third tick for all of them on the laid tree — 1.8 times
  the work a walker. A moving body is moved in place, but each move reads its slot, its run's neighbours and the main
  claims on the ground it comes to cold. It is now the largest stage of a turn but the walkers' stationing (0.15 ms and
  0.19 of 0.78 on O10, root log 2026-10-14, a walker's walk kept).
- **One car wrecked on Odesa in `--bench trips` since walkers' walks are kept** (2026-10-14, root log), against none on
  6078de5 and none with walking pace moved by 0.001 m/s. Not traced; the stuck probe's first-wreck trace on Odesa closes
  it or shows it chaotic.
- **OdesaOsm is not measured on the turns** (2026-10-14): the owner asked for small maps while resources are short, so
  the root log's figures are O10's and Odesa's, and its OdesaOsm figures are of trees before. A `--bench room` and
  `--bench drive` on OdesaOsm close it.
- **Reds rose with the turns on Odesa**: `--bench drive` 3 against 0 on the laid tree (2026-10-14); O10 is back at 21,
  as on the laid tree, since walkers' walks and lights' holds are kept. Which red is crossed that was not is not traced;
  a `--bench drive` red traced to the turn it was committed in closes it.
- **A light's hold is placed as its first body stood at its turn** (`SignalHolds.Lay`): a body standing over the bar
  then leaves the light holding nothing, and the ground it pulls off the bar is held by nothing until the light's next
  turn — a follower whose turn falls between can plan over the bar. Nothing has been seen to come of it on the small
  maps' reds; the reds above traced close it or name it.
- **A kept plan is asked afresh each time its car's nose reaches another piece of its line** (`DrivingGround.KeepsThePlan`):
  a piece's rung is levelled by what lies ahead of it and capped by what lies behind, and passing a piece can raise the
  rest. It was about 170 plans a laying on OdesaOsm when every car planned at every laying, and is not measured
  since each plans at its own turn. Levelling the held claims' rungs again where the nose leaves a piece closes it.

- **Nobody reads what a body is doing, so every decision that turned on it is dropped** (since the claims redesign,
  2026-10-12; road decision log): the claims say where a body is and never who or what it is doing (TER-4c.5), and
  what read the body's state off the claim it laid is gone with it:
  - deciding a pass round what cut a grant (CAR-46) — `Overtaking.DrawThePass` and `ComesUpTo` are kept and called by
    nothing, so backing up for one (CAR-50, `BackingUp`) and a call getting past at-rest or slow traffic (AMB-4.4)
    are unreachable too;
  - moving across to get past what holds a car up (CAR-53): `Switching`'s `RoadShape.Asks` and `WantsWhenHeldUp` are
    still worked out and read by nothing, and `Driving.SlowerToSwitchMps` and `Ambulance.SlowerToPassMps` are figures
    nothing reads;
  - a walker's sidestep (PER-28): `Sidestepping.DrawThePass` is called by nothing;
  - the room kept to step out behind a body going nowhere (S-2a, CAR-50's "says it is going nowhere") — the zebra
    cards above;
  - a light's hold lying over a body only standing on its way: it now ends at the first body of any kind (TLT-1's
    second bullet);
  - TER-5e's tier "ground its holder already stands on".

  A reading of the bodies ahead off the world view, by place — at rest or not, travelling which way — handed to the
  actor that asks, and never carried on a claim, closes it, with the skipped tests and the cards.
- **Two town-wide gates went with the claims' hold and line metres** (`ClaimGateTests`): "no hold is in two pieces"
  (TER-5c.2) and "the rung never grows along a hold" (TER-5g.1). The first holds by construction — a refused hold asks
  nothing more — and has a unit test; the second rests on `Following.LevelTheRungs` alone, and a town gate on it, read
  off the pieces each hold asked, closes it.

### The device world's step

Measured by the two kernel reviews of 2026-10-16 on the RX 9070 XT (O30, a device step 0.70 ms after them); each
untaken.

- **`world_covers.comp` is the dearest kernel, 134 µs** with the four wheels' runs asked at once: a wave holds 36 KB of
  scratch for the ways it finds, and each wheel still passes over other columns' entries. An atlas laid a run a
  lattice point closes it, with the host's `RibbonAtlas` alike.
- **The covers cross to the host every tick**: 76 µs of the device's and ~1.4 MB copied on the host. A covers list a
  view slot, as the views already are, closes it.
- **The step's uploads, the signal rows among them, take ~70 µs**; reading the rows in place cost the same. Sending
  fewer — a `Drive` the same as the last one sent left out — closes it.
- **`tyre_pass.comp` (70 µs) and the rest of `world_view.comp` (~100 µs) are arithmetic**: 23 000 instructions a
  thread, most of them the exact-rounding helpers the device answers the host to the bit with. Fewer registers bought
  nothing (168 → 144 a thread, 70.7 → 70.1 µs). Nothing short of rounding that is no longer the host's closes it.
- **`world_signals.comp`'s events run on one thread** (~10.6 µs); and the scans are eight dispatches of ~4 µs, mostly
  barrier latency, which a subgroup scan closes where the page's WGSL has subgroups.
- **A window's sprites are counted, scanned and written every frame over every unit**, which is what bounds O30's
  frame rate on the device world now that the step is not.

### Found by the per-file sweep and left (2026-10-16)

Every file of `src/` was read against .NET 11 practice and performance (`.tmp/coverage/*.md` names each); what was
applied is in the log, and each of these was judged not worth its cost, or not provably bit-identical, and left.

- **The tick**:
  - `CarBuilds.Of` takes an integer modulo on every `BuildOf`, many times a car a tick; unmeasured, and an A/B on
    `--bench age`'s cars column decides it;
  - `PhysicsWorld.FindContacts` inlines `Gather` three times into 7 KB of code that spills its loop locals; keeping
    `Gather` out of line is to be measured on a quiet machine;
  - `SignalHolds.Lay` asks `PhaseOf` twice a stretch, and again for every stretch of one junction; a junction's phase
    kept a turn closes it;
  - `Switching.WaitAtM` works `TheLineStopsToMoveAcross` out again every tick for a switching car — kept off the cached
    road shape only because that cache's key misses a lane's shift; `Overtaking`/`Switching` copy the 40-byte
    `Overtake` where reading it in place would alias the row they write back.
- **A frame**: `PersonSprites`/`TownSprites` read the fleet's arrays again a sprite (the managed world, the page, a
  shot); the web and handset windows work `UiScale` out every pointer read where the desktop keeps it a pump.
- **Setup and the open**: `GlyphStamp.Blend` goes through the image's indexer a pixel; `KerbLines.Of` copies its pieces three times and keeps
  two; `ShellFill.Ears` allocates a `Node` a corner; `Spline.RoundedInto` a `float[]` a road; `TownLayout.StandsClear`
  a delegate a call; `ArcRings.Of`'s stringing is one thread; `TownWorld.Places.LayTheExits` stays serial until
  `StaticBodies.InBox` is laid before it rather than on its first ask; `ZoneTree.Holds` files nothing by row as
  `EdgeRows` now does; `Roundabouts`/`TownLayout.ArmsAt` scan every edge a node (~40 ms on O30).
- **Serial because order is the answer**: `FootGraph.Builder`'s welding numbers nodes in order (~0.4 s on O30),
  `PropStage.OverWhatIsLeft`'s draws read the ground before them (~0.9 s), `CarParks.Lay` places each against the last.
- **Faster only with a digest moved**: `KerbLines.Stepped` and `RoadLines` sample a ring from its start
  (`Spline.SampleAt`), and jumping to the piece rounds otherwise (a large part of `CrossingWays`' ~0.5 s on O30);
  `BayManoeuvre`'s `Sin` and `Cos` of one angle are not proven the bits of one `SinCos`.
- **osm-scan**: `MlRoads.Lay` is one thread (~0.7 s; each thread wants its own lookup sets), and `Gtfs.Table` makes a
  dictionary a row of the timetable (~1.4 s with transit).
- **The probes**: `SpriteProbe.Us` calls its part through a `Func<int>`, 1–2 ns against parts of ~0.1 µs; a struct
  functor closes it.

### The debug layers, while the camera moves

Measured by the review of 2026-10-16 with every layer on (`Test`, `Laid`): nothing allocates, and a still frame is
kept, but a moving one lays its layers again.

- **A quad is turned by an angle**: `ScreenDraw.Bar`/`BandM` take an `atan2` a quad, the perimeter's relay up to
  1.5 ms, the shell's 0.5–0.8 ms and the wireframe's 0.2–0.6 ms. A quad carrying its direction rather than its angle,
  through its layout and the overlay shader, closes it. The figures predate the ways moving to the engine layers and
  were not taken again.
- **The zones wash asks the zone tree a point at a time**, ~5 000 `ZoneTree.At` a moving frame, 0.5–0.9 ms; a grid of
  zone kinds laid once a town closes it.
- **`SelectionPath.CarPath` draws the whole route left**, unculled by the view.
- **The zones layer keeps a framing only while it is exact**, so a camera following a unit lays it every frame; a
  kept framing a margin wider than the view closes it.

### The engine layers (2026-10-19)

- **Not run in a browser**: the page's half (`town.js` `engine`, `Game.Web.cs` `RunTheEngine`) builds and its kernels
  translate to WGSL, but no page has drawn them. A page stepping the managed world sends every host-written slot buffer
  whole ahead of each batch — the body rows of the whole table a frame; sending the moving rows alone closes it.
- **Figures not taken**: what the ways table costs the device on a traced city (an arc a row, 32 B, laid the first
  time a layer of them is on — OdesaOsm holds 1.1 million ways), and what a frame of the kernels costs at a district
  framing. `qq prof --gpu` with every engine layer on takes both.
- **The kernels append from many threads**, so translucent quads that overlap — two claims over one stretch — blend in
  an order that can change from frame to frame and shimmer by a shade. Count, scan and write in table order, as
  `DeviceSprites` does, closes it.
- **The solver-grid layer still reads the managed solver's own grids**, so on a world stepped on the device it draws
  nothing and the inspector finds no solver cell. Drawing it off the device's grid buffers closes it.

### Code kept twice, or kept for nothing

Found by the cleanup of 2026-10-15 (`qq unused`, the analyzers) and left because closing each crosses slices, changes
a layout or changes behaviour.

- **One union-find root, three copies** (`FootJunctions`, `CrossingWays`, `LanePlaces`) — only the root: each join
  keeps a different representative (the first, the lower, the other), which the order of what they lay rests on, and
  no folder of `core/` is a home for four lines; **three `Spread`/`Percentile`/`Median`** in the bench
  (`TownShape`, `ConnectorShape`, `ZoneFidelity`), which print differently, so one copy moves probe output.
- **Kept for nothing a run reaches**: `ArcSubtract` (295 lines) and its tests, kept "as tooling" by the render log of
  2026-09-16 though no tool calls it _owner_; `CityPlan.ZebraAtEveryStation` is false only in `CrossingsTests`, so no
  map lays the `Crosswalks` path; `ParkingRegistry.Takes`, named by CAR-11b as answerable and asked by nothing.
- **Read by the tests alone, and not plainly a seam** (the 2026-10-16 sweep of what `qq unused` lists as test-only):
  `ConnectionPoints`' `ConnectionPoint`, both `At`s, `Point`, `ArmOf(GroundPieces…)` and `MostPerArm` work out where
  lanes end a second time, beside `RoadLines`, as `ConnectionPointTests`' yardstick — whether that may live in `src/`
  is VER-12's question; `RoadStage.CreaseRad` is a test tolerance production never reads; `TownMap`'s water kind
  `Lake` is written by nothing (whether a shipped `.map` holds it decides); `CarFigures.LongestLengthM` is authored and
  read only by two tests, where GEN-53 could be asked of the catalogue directly; `SpriteInstance.AsDrawn` repeats
  `Halves`' cast.
- **`WorldLayer.StepsAhead` is a second name for `SimFigures.WorldStepsAhead`**, read only by crefs, while the car's
  follower reads the figure itself. One name in the docs closes it.
- **`figures.trim.xml` says only the browser head reads it**, and the handset's project file includes it too — where it
  names the web assembly and so keeps nothing. Naming both assemblies, or the handset not including it, closes it.
- **The instruments miss two kinds of stale name**: `qq unused` counts by spelling, so an overload sharing a live name
  and a chain of members only calling each other read as used (the junction corner reach, removed 2026-10-16, was
  one); `qq doclint` resolves crefs and IDs but not a backticked `Type.Member` in prose, which is how
  `CutJunctions.SitesOn` outlived its rename in the citygen requirements.

### The suite's own runs

What a 2026-10-16 sweep of `src/tests/` found and did not take, each keeping its assertions as they are:

- **`LaneOccupancyInATownTests` stands the city seven times** (about 1 s apiece): each case moves car 1 by hand, so
  sharing needs each case to stand a car of its own.
- **Read-only fixtures still laid per case in the unit tier**, tenths of a second each: `FootWays`/`FootMovements` in
  `PedestrianWaysTests`/`PedestrianMovementsTests`, the lanes and arrows in `LaneArrowsTests`, the brute-force half
  of `RingSidesTests` per cell size, `SelectionTests`' world, `ChannelsInATownTests`' 900-tick watch twice.
- **The device tier redraws and redispatches the same seeded samples** in `TrigKernelTests` and `RoundedKernelTests`, and
  `AtlasKernelTests` stands the fixture and the street-over-a-street worlds once a case though none ticks them.
- **`VisualShotTests` lays and runs a town once a cell** of a multi-cell sheet (`ShotRun.Take`, 600 ticks a cell):
  sharing a run needs `app/shot`, not the test.
- **Three allocation tests warm their draw two passes before measuring** (`InspectorTests`, `UnitPanelTests`,
  `StatusPanelClaimsTests`), the count `StatusPanelTests` records as failing now and then when the draw tiers up inside
  the measured window; none has failed in five consecutive unit runs.

## Inconsistencies

- **A district may stand no depot** (2026-10-20, GEN-55, GEN-56; accepted by the owner): a yard is as wide as its
  building's frontage (`CarParks.YardBays`), eight bays for the 23.4 m civic roofs, and one district of `Odesa` finds no
  kerb straight for that long (depots 7 of 8 in `--bench census`). A site search that holds a long yard on a curving
  kerb, or a shallower yard, closes it.
- **`qq doclint` finds `FNV-1a` cited by code and stated nowhere** (found 2026-10-20, `src/citygen/CityPlan.cs:152` and
  one more): an ID-shaped name in a doc comment, not a rule. Rewording it so it does not read as a citation, or the
  lint knowing an algorithm's name, closes it.
- **`TurnAtALotWithinM` is read off the widest lot and not the widest yard** (2026-10-20, `SimConfig.Derived`): it takes
  `BaysPerLotMost`, six bays, and a service's yard is now up to eight, so a bay at a yard's far end may stand past where
  a leg may turn at it. Nothing has been seen to fail by it; the figure taking the widest rank a town lays closes it.

- **The ground answers the ballast over the decks, the water and the shore** (2026-10-19, `GroundShapes.At`, RAIL-3,
  TER-7): the mesh lays the track at grade under all of them and a bridged track over everything, and the answer is
  meant to be that order walked back. Only the walk is asked first now. One ring set (`RailArrays.Ballast`) holds both
  levels — each run of one level its own ring since the loop (RAIL-11), but not told apart — so neither placing is right
  for both; no agent is moved by it today, since water is permitted to nobody either, but a track along a shore is
  ballast in the answer and pavement in the picture, and the ground under a rail bridge's climb round a loop is ballast
  to a road crossing under it. The ballast filed by level, each asked at its place in the order, closes it — a change to
  what the wheel reads while it lays streets (`WaterRules.Crosses`), so every generated map is laid again by it.
- **A pavement running along a traced track is pavement over its ballast** (2026-10-19, `GroundShapes.At`, RAIL-4):
  the walk is asked before the ballast so a walk crossing a track is laid over it (RAIL-3), which also makes walkable a
  traced walk that runs beside the track inside its band. `--bench trains --map OdesaOsm` met 28 walkers in 300 s where
  it met 2 (the train log, 2026-10-18), with its rails laid again for the 900 m run-off and its freight trains 35 to 40
  long; which of the three moved it is not traced, and OdesaOsm is set aside for now (_owner_). The walk over the ballast
  only where a crossing's stretch holds it, or the met walkers traced to another cause, closes it.
- **The fixture's track meets no road** (2026-10-19, `towns/briefs/Test.json`): where its loop keeps the staging the unit
  tests guard, its line crosses no street, so the fixture runs trains but no level crossing (0 crossing stretches). The
  suite stages its crossings on briefs of its own (`Towns.Brief(railTracks: …)`), so nothing is unasked; a placement that
  keeps the staging and crosses a street closes it.
- **A bogie stands 83 mm off its line on the fixture** (2026-10-19, `--bench trains --map Test`), against 8 mm on Odesa
  and 5 mm on the fixture before its loop — a reading, never a gate. Where and at what speed is not traced.
- **The solver's contact arrays grow until the fixture's second minute** (2026-10-19, `Towns.SettleTicks`): 40 KB on
  the world's thread at 68 s, with a 19-vehicle train out over the loop, so the allocation gate warms the fixture for
  two minutes. The arrays start empty and double (`PhysicsWorld.RoomForContact`); laid for the town's bodies — its
  wagons' couplings among them — they would not grow, and the warm-up could come back to a minute.
- **O10 holds twenty times Odesa's people, not ten** (2026-10-19, `towns/briefs/O10.json`): 12 000 against Odesa's 600,
  where its description and O30's 18 000 say its density. 6 000, authored again, closes it — after which O10's figures
  in the root log are of the old town.
- **A crossing's cabinet, signs and spare lights are drawn nowhere** (2026-10-19): `relay_cabinet`, `crossing_cross_b`,
  `crossing_countdown` and `crossing_countdown_b` have kept variants and no sprite; `relay_cabinet_b`, `crossing_cross`,
  `crossing_warning` and `crossing_warning_b` have none (`railway_gear_a`, `railway_signs_a` drew them past `cut`'s fit).
  The upright heads `crossing_head`, `_b` and `_c`, and the X lights `crossing_light_b` to `_e` with their lit twins, are
  installed and unlisted since the X light replaced the head. Placing a cabinet at each crossing and its signs on its
  approaches, or deleting what no crossing will wear, closes it.
- **A car meeting a crossing's arm allocates** (2026-10-19, `RAIL-10`): with arms that met cars inside its window,
  `AWholeTickWithATrainCrossingTheTownAllocatesNothing` read some 40 KB over 600 ticks, and none with the arms on a layer
  meeting nothing; it passes because no car meets an arm since lanes stay held until the arm is all the way up. Where
  the contact path grows is not traced. A gate that stages a car against an arm, and the growth pooled, closes it.
- **The arms on the device are not compared with the managed world's** (2026-10-19): `TrainsRunOnTheDeviceAsInTheManagedWorld`
  lays no cars, so nothing checks an arm stops a car on the device; both take the same `Place` and `Contain` and the
  same solver, and the device tier passes. A device case with a car driven at a closed arm closes it.
- **An arm starting down shoves a car standing under it** (2026-10-19): its body is the whole arm lying down from the
  moment it starts down, it has no weight to stop with, and no crossing holds it while something stands in its way. None
  was met over five minutes of Odesa (`--bench trains`); a queue backed over its line from beyond the crossing would be.
- **A train backing throws no switch** (2026-10-19, `TRN-11`): `Trains.ThrowTheSwitchAhead` reads only the next switch
  faced ahead of the front going on, so A/D while backing do nothing and a backing train retraces its leg. A throw at the
  next switch faced behind the tail, re-routing the leg's start from it, closes it.
- **A train backed to the end of a short run-in is taken out in sight** (2026-10-19, `TRN-7`): it is taken out once its
  tail reaches the track's loose end, and where a run-in is shorter than the train its front is still on the map then.
  None of the generated maps' run-ins are (`RailFigures`'s run-on is a whole train past a warning); a traced map's may be.
- **The unit panel's train and wagon rows, the train's path and its brackets have no test** (2026-10-19, `CTL-1`,
  `CTL-1a`): `UnitReadoutTests`, `SelectionPathTests` and `SelectionMarkTests` stage cars and walkers only. A case each on
  a town with a railway closes it.
- **`coal_gondola` has no picture and is out of the rolling stock** _owner_ (2026-10-19): `trains_b` drew it tilted twice
  and swapped it for a tank on the third (`raw_assets/agents/train/prompting.md`); a coal train wears two looks, not three.
  It returns to `Rolling.json` with a kept picture.
- **The rolling stock's narrow vehicles are fitted down some 12 % across** (2026-10-19): the model draws a long thin
  outline that much wide at any width asked, and `qq sprites cut`'s rows allowance (`FIT_ROWS_MAX`, the owner's minor
  deviation) squeezes it onto the collider, so a coach's roof detail is that much narrower than drawn. A sheet that fits
  unsqueezed closes it.
- **A crossing's walk is drawn as walk and answered as railway** (`TER-7`, 2026-10-19): a crossing road's walk is
  drawn over the track and only the rails over it (`RAIL-3`), while `GroundShapes.At` puts the ballast over the walk there.
  Nothing permitted on a walk is placed at a crossing, so no prop or route reads the difference yet. Answering the walk
  over the ballast inside a crossing's stretch closes it.

- **A third of the building roofs are drawn under the art grid** (2026-10-20): all 376 were drawn again, as few as
  two to a frame where the owner allowed it, and 133 are still under 25 px/m against `ArtPixelsPerMetre` 31.5 — the
  median 28.7, the coarsest 8.5 (`industrial_deep_11`, `industrial_26`, `school_13`, each some 60 m across). One frame
  at 1536×1024 holds no roof past ~47 m at the grid, so only a roof drawn in pieces would close it; `qq art` saying
  which sheets are under would keep the rest from slipping.
- **120 of the 376 roofs are kept at score 3** (2026-10-20, `raw_assets/world/building/prompting.md`): AC units and
  chimneys 1.5–2× their true size, stair and lift boxes showing a thin front face, `house_deep_19`'s porch leaving 2.8 %
  holes at its bottom corners. No wording cured the first two; a frame asked again with a better cure closes each.
- **Thirty prefab describes contradict their roofs** (2026-10-20): sixteen flat-roofed blocks of flats ask for "1 lift
  machine rooms", fourteen ask for hatches "evenly along the ridge line" of a flat roof (`apartments_*`, `tower_*`), and
  "stairwell exit hatches" are meant as boxes. The art is drawn and kept; the next brief of any of them repeats the
  slip until the describe is reworded.
- **`qq sprites mask` cannot mend a cell drawn the wrong size** (2026-10-20): the mask is the outline's box, so paint
  past it or short of it comes back byte for byte and a redo of a size fault is a whole re-roll. A mask padded to the
  cut's drawn extent closes it.
- **`apartments_17-v2` and `apartments_deep_08-v2` are duplicates** of their `-v1` (2026-10-20): one frame's cells
  kept twice, before `keep` refused a cell kept once. Unused candidates; deleting the two records closes it.
- **Without its glow a lit brake, indicator or reversing lens barely differs from its dull glass** (2026-10-20,
  `qq proof --no-glow`, `cars_fleet_a`): the bake carries a lens up its colour's ramp by the art's own luminance
  (`LampAtlasBake.Lit`), and the generator's bright red and orange glass is already near the top of it, so braking
  moves a texel by at most 125 of 255 and reversing by 74; only the headlamp reads lit. The glow is what says it in
  the game. A ramp whose floor stands clear of the dull glass, or a check of the lit lens against the dull one,
  closes it.
- **`qq sprites` still carries the generated zone-map path** (2026-10-21, `.qq/sprites.py`): `ZONE_PARTS`,
  `ZONE_COLOURS`, `LENS_ZONES`, `ZONE_REACH_M`, `zoned_sheet`, `cut_described`'s zoning branch and `use`'s, which now
  refuses to install one (lamps are `qq props install`'s, CAR-14a). Dead code that misleads a reader; deleting it, and
  the blob trial `.tmp/zone_tool.py`, closes it.
- **What the batch 1 lamp cuts found wrong in `qq props`** (2026-10-21, ten agents, `.tmp/fleet/props_notes.md`): seed
  takes paint for lenses (a chrome bumper or pale bumper strip as a reverse, the orange fringe between a red lens and
  lime paint as an indicator, a one-sided highlight of the brake's own hue as a lens); `axis --prop` stops at the
  copy it starts from ("already where it fits best") on four cars, and finds a far better axis once moved by hand,
  which agents did by writing `axisY`/`mirrorDx` themselves; `snap` adds and removes vertices though the manual says
  it only moves them; `undo` is one step and a second redoes; SPARE is offered for a tip and again after its drop made
  the copy worse; `trace` on an unmirrored prop writes `"mirror": true`, `show --prop` calls a copy-side prop
  "authored", and `cut`'s lit picture never burns an authored-side part; red on red runs on past 30 texels in every
  `--lens`; a brown lens frame is not frame to `dark`. Each cost an agent a retrace or a hand edit; fixing them in
  that order closes it.
- **What the batch 3 lamp cuts added** (2026-10-22, nine agents, `.tmp/fleet/props_notes.md`): no step before
  `install` warns that a lens passes the lamp cell, and `install` rounds a lens exactly the cell's 40 texels up to
0.417 m, which `LampAtlasTests.EveryLensFitsTheCellItIsCutInto` refuses (`hatch90s_black`'s head trimmed to 39); the
overlap hint puts
  `"over"` on the bigger prop, the manual on the section (two cars); `find` lists one side's patches only; `trace`
  reads grey glass as no lamp; seed's amber class merges amber with orange-red. Each cost an agent a hand placement;
  fixing them in that order closes it.
- **What the lamp cuts of batches 1 and 4 found in `qq props`** (2026-10-22, 29 agents): seed takes body paint for a
  reverse (`microcar_white`, `cityhatch_silver`), an orange fringe or a paint patch for an indicator (`citycar_lime`,
  `citymini_green`), a bumper edge for a brake (`estate90s_red`), and stops a headlamp 4–6 texels short of its tip
  without saying so (`hatch_blue`, `budgetsaloon_grey`); `find` lists a pale pink highlight as amber
  (`citymini_yellow`) and misses an amber front lens the hue panel shows (`hatch_white`); `--lens=frame` cannot read a
  pale headlamp drawn with no rim (`tavria_white`, `retro500_cream`, `cityhatch_silver`, `saloon90s_white`: placed by
  hand); `move --edge --grow` slides the edge's ends along their neighbours (a tip 4 texels past the lens on
  `citycar_lime`); `hold` has no command; snap grew a section 17 px past the lamp it is declared over
  (`retro500_cream`). Fixing each closes its part.
- **A car may cut five lamps, its tail one housing** (2026-10-21, `cityhatch_silver`): `install` writes a lens a side
  of every prop and part, and `CarLamps.MostLenses` is ten, so the red arm the art draws along the tailgate beside the
  corner lamp stays unlit. Widening the emit's word (below) closes it.
- **The eight trial cars' polygon cuts predate seed's tracing and two misread a lens** (2026-10-21, `.tmp/props/`):
  they were placed by hand before seed traced lenses off the art, before it named every lamp and before a part left
  out the sections over its lamp; turned into texels for the pixel trials they read `supermini_red`'s brake 32 % red
  and `hatch_coral`'s front indicator 0 % amber. Seed alone, on those eight and the isolated trial's three, is drawn
  beside the agents' cuts in `.tmp/trace_dev/sheet*.png`. Their pixel cuts are kept apart (`.tmp/props_px/`, `qq
  props-px`), as are the trials' (`.tmp/props_text/`, `.tmp/props_vis/`, `.tmp/iso/`, `.tmp/iso2/`). Recutting the
  eight by the loop closes it.
- **What the polygon loop still gets wrong** (2026-10-21, `.qq/props.py`, `.qq/_props_fit.py`): `axis --prop`
  searches only near the copy it has, and called one three texels off "already where it fits best"; snap never turns
  a bow concave that was not, so a hand-placed crescent keeps its inner arc bowed out (a traced one is bowed right);
  the framed figure cannot tell a frame from dark paint (`estate_navy`) and flags a lens framed on part of its rim;
  snap stops short where it is run once and goes further run again; `axis --prop` reports the copy's gain and not the
  authored side's loss; the edge table gives one rim figure an edge for both sides once they are under
  `LOPSIDED_PX` apart, so a copy-only miss has no edge named; a lamp on the centreline (a high-mounted brake) cannot
  be authored unmirrored by trace, which then places a copy on bare paint and reports it on its rim.
- **What trace misreads** (2026-10-21, `.qq/_props_trace.py`, seed in `.qq/props.py`): a red-orange lens reads as
  amber, so `supermini_red`'s brake is traced off an amber patch and its indicator left a part; a pale lens's glass
  is grown from the texels just round its highlight, and is lost where they are partly paint; a frame open on one
  side lets `--lens=frame` run on; seed takes a pale patch touching the brake's housing for a
  reverse and the biggest amber patch for an indicator, which on `supermini_red` is a 44 px patch at the nose that
  its copy side does not draw.
  Each is the agent's to judge on the seed picture and retrace; a class read off the lamp's own colours rather than
  fixed hue bounds closes the first.
- **Trace still spends two vertices on a rounded corner** (2026-10-21, `arcs_of` in `.qq/_props_trace.py`): its arcs
  are fitted to the contour as if every vertex were sharp, but the prop is drawn with each corner rounded `ROUND_PX`,
  so a lens's rounded corner fails as one vertex and keeps a pair a texel or two apart (`fleetsaloon_white`'s brake,
  bottom outboard). `MAX_POINTS` and the `SPARE` hint bound it rather than close it; fitting the arcs against the
  outline as `fit.outline` rounds it closes it.
- **`qq props-px` has no keep-or-revert check on `smooth`, and figures that mislead** (2026-10-21,
  `.qq/props-px.py`, `.qq/_props_px.py`): `smooth` lowered edge agreement on four props of three cars and puts back
  texels a prop's `"not"` holds; a snap refused still writes its `--pull`; a part's texels leave its parent's colour
  figure; the copy-placement suggestion was wrong in four of six runs; tinted head glass and pale paint misread as
  classes; its reverse part is a straight slice of the brake. It is the tool for the rare lens a polygon cannot
  follow, and none has needed it yet.
- **remotegen's `video` command is documented nowhere and its `--last-image` has never run** _owner_ (2026-10-20,
  `.claude/skills/remotegen/remotegen.py`): added for the lamp trials, which the owner set aside (no clip lit a lamp).
  Whether it stays is the owner's. If it stays, a SKILL.md section and one `--last-image` run close it; if not,
  deleting `cmd_video` and `openrouter.video_request` does.
- **The bigger civilian cars' noses are rounder than their collision boxes** (2026-10-18): `cars_civil_c`'s five largest
  bodies and all nine wrecks leave 1–3 % of their outline bare at the corners (`qq sprites cut`, holes), so a car
  is struck a few centimetres outside its paint there. Barely visible; a brief naming the corner radius in words,
  or `collisionM.cornerRadiusM` authored rounder for those bodies, closes it.
- **The ambulance cannot take a pair of headlamps** (2026-10-18): its ten lenses are the most a car may draw
  (`CarLamps.MostLenses`, three bits a lens in one word of the sprite emit). Widening the word to two closes it.
- **A generated car's mirrors are clipped flat at its footprint** (2026-10-18): the model draws them about 0.05 m past
  the 0.1 m a side the nine civilian cars' footprints allow (`qq sprites cut`'s overlay, cyan), and the cut keeps the
  footprint; `cars_fleet_a` drew them about twice the size asked even when told "no longer than the headlamp".
  Barely visible at game zoom; footprints authored to the mirrors a body has closes it.
- **Sixteen of the nineteen cars author lenses past their own shape** (2026-10-18): the `lamps` were measured off the
  old art, whose bodywork overhung `collisionM`, so rear lamps sit on the rounded corners and indicators past the nose;
  `qq sprites template` warns of each (ambulance 4, evacuator 4, the muscles, pickups, police and sedan_rust 4, the
  rest 2). The art drawn to the shape has no paint there, so it cannot draw them. Moving each lens inside its shape
  as the set is generated again closes it; moved before, it misses the lamp on the sprite in the game now.
- **The nineteen legacy looks still light their lamps by rectangle** (`LampAtlasBake.Paste`, 2026-10-18): with no zone
  map, a lens burns its whole `sizeM` area of sprite by luminance, so a round or strip lens lights the paint in its
  corners and a lens drawn off its rectangle lights paint only. Two ways of finding a lamp stand in the bake until
  then. Generating each set again with its zone map (CAR-14a) closes it and deletes `Paste`; a beacon needs a zone
  colour of its own first (`LampAtlasBake.ZoneOf`, `ZONE_PARTS`).
- **`qq proof --looks=all` peaks at 1.2 GB** (`LampProof`, 2026-10-18): ImageSharp holds every distinct frame as full
  RGBA until the GIF is written — 39 frames of 1728×3874 for the 28 looks; nine looks are a fifth of it. Writing
  frames to the encoder as they are made, or a frame a tile row, closes it.
- **The zone map's glass channels are written twice** (2026-10-21): `LampAtlasBake.Holds` and `qq props`'
  `INSTALL_GLASS`, with `INSTALL_CELL_PX` and `INSTALL_MOST_LENSES` copying `LampAtlas.CellPx` and
  `CarLamps.MostLenses`. `AZonedLampBurnsOnlyItsZone` fails if a fitting's channel drifts; the two figures nothing
  checks. One reading the other closes it.
- **Signal heads stand in the middle of a junction cluster** whose boxes are 27–34 m apart, each road between them
  long enough to be a street once both mouths are taken (OdesaOsm 2830,13491, survey junctions 617, 651, 3040 and
  6255), so nothing gathers them (`TracedStreets.Squeezed`) and the short streets hold at bars inside what reads as one
  junction. A rule for a ring of short streets enclosing no block — gathered as one box — closes it.
- **The 80 stubs left between two boxes** (`--bench close`'s `stubs`) are 52 a roundabout's ring, which is never
  gathered (GEN-19), and 28 one-way or two-way roads whose ends are on a bridge or a ring. None is listed as a fault;
  a picture of each kind is what would say whether any is.
- **A street drawn twice in OSM is laid twice** (OdesaOsm 9300,5234: ways 716666621 Лісова and 205208659 run 3.8 m apart
  for 150 m, both a lane each way): two carriageways over each other, four lanes painted on a street of two. It is the
  only such case `--bench faults` finds (two-way roads over each other by more than a lane along most of the shorter);
  the other hits are pieces inside a junction cluster. Merging the two ways in OSM closes it; dropping one in the
  importer would need its side streets re-attached at both ends.
- **Movements in a large gathered box loop round** (`--bench connectors`: 25 heading away from both their ends on
  2026-10-17, 24 of them U-turns, since a movement drawn round the long way is no longer kept; worst at
  OdesaOsm 4049,18549, an 8-arm box 45 m across): the lanes into the box end past each other, so a near-side turn is drawn
  270° the long way. A biarc chosen to turn least was tried and could not help without bends tighter than the turn circle;
  what closes it is the lanes into a gathered box ending short of where their lines cross (`TracedStreets.Standoffs`).
- **Cars at OdesaOsm's busy boxes lock each other** (`--bench stuck --map OdesaOsm --seconds 120` at 5 000 cars,
  2026-10-17, with its car parks laid and every car touring): 152 of 5 005 cars stood still at the end, 16 rings of two
  cars each waiting on the claim the other holds in a box (2816,21897; 3085,17263; 2735,21536; 2785,19490). With the
  cars parked at the kerbs and running the round (GEN-4n) a third as many are on the road at once, and the same run has
  no ring — which hides the lock rather than closing it. Tracing one ring to the claim that should have been yielded closes it.
- **The fleet steers wider than the town lays its turns** (the same run: 38 cars driven off their line, 24 of them in
  a box, 48 legs given up; 7 off their line with the cars parked at the kerbs, a third as many on the road): the turn room (`SimConfig.JunctionTurnRoomM`) is the config car's 3.94 m circle, and most variants' is wider,
  4.0 to 5.4 m (a pickup 4.9, the APC 5.4); `--bench connectors` lists 681 near-side and 161 far-side movements tighter
  even than 3.94 m, kept above the 2.4 m cornering floor. Sizing the turn room for the fleet's widest common circle — a
  figure the catalog would hand down to the town — closes it.
- **105 of OdesaOsm's 250 car parks have no room for a ladder and 42 no street in reach** (`qq osm --parking`,
  2026-10-17): they stand as bare ground, and the cars of the doors round them park further off. Most of the first are
  under 2 500 m² or narrower than an aisle and its two ranks once they stand a walk off the street; the second are
  courtyards reached down a drive that was dropped as the place's own ground. A comb — one aisle across, the rest dead
  ends a car turns round in at a bay (GEN-4l) — and an entrance that bends to meet its street square close most of it.
- **A car's round finds no bay in a town whose car parks stand far apart and that has no roadside** (CAR-8,
  `TownWorld.SetOffForAnotherBay`): it draws a place within `Driving.RoundReachM` (600 m) and asks for a free place within
  a walk of it, so on OdesaOsm with its car parks alone 2 of 5 000 cars drove in two minutes. Its kerbs (GEN-4n) are
  what the round finds there now, and only 24 of 1 133 parked in a car park; drawing the round's place off the places
  within reach rather than off a drawn point closes it, and would send more of them to the car parks.
- **A move across is priced at a car length however little lane is left to make it in** (`Driving.LaneSwitchPriceCarLengths`):
  a route still asks a car for a move across at a lane's end that has no room for one, and the car drives on past it
  and is routed again (CAR-53, `TownWorld.DriveOnPastTheMoveAcross`; 11–21 a 120 s run on OdesaOsm). Pricing a move
  across by the lane left to make it in closes it, and the detour with it.
- **A body read off its wheels reaches past its band by half the lattice's diagonal** (`RibbonAtlas.UnderWheels`, 0.35 m
  at the 0.5 m step): a car whose wheels stand within that of the next way is read on it. A car parked at a kerb is laid
  over its place instead (GEN-4n), but one standing a hair off its place, and a car on a lane's edge, are read across
  the line. Reading a wheel against the bands rather than off the nearest lattice point closes it.
- **A vehicle wider than a roadside never parks at a kerb** (GEN-4n): the pickups and the APC run the round to car parks
  only, and a traced map's spawn stands none of them at a kerb, so its car count falls short by their share.
- **One car was wrecked in the 120 s run with no other car wrecked beside it** (OdesaOsm 4032.9,13934.5, car 9, tick
  2 999, coming off a box onto lane 896 at 8 m/s, 2026-10-17): what it struck is not said — a parked car laid over its
  kerb place and swept by a turn out of the box, a prop, or a wall. A shot at that tick did not stand the car there;
  the probe naming what a lone wreck touched closes it.
- **A traced town hands its people no car at a kerb** (PER-29, `TownWorld.HandThemTheirCar`): an owner is handed a car
  only where a painted bay stands free within a walk of their door, so on OdesaOsm almost nobody owns one, though a kerb
  runs past most doors. Handing the free kerb place nearest the door (GEN-4n) closes it — at 40 000 people, that many
  more bodies standing, which is the cost to weigh.
- **A traced map's cars are spread over roadsides the town then refuses some of** (`SpawnStage.TheKerbs`): the plan
  lays no zebra and does not know which kerb lanes lead on, so a car the plan stands beside a crossing, a dead end or a
  lane too short is not stood at all (`TownWorld.KerbPlaceUnder`), and the town stands fewer than its map asks. Laying the
  plan's kerb places off the same stretches the town parks along closes it.
- **Laying a map's car parks again leaves the points the last entrances joined its streets at** (`TracedCarParks.Parked`):
  a street keeps a point on its straight where an entrance no longer meets it, which joins nothing and changes no line.
  Thinning a street's points after the aisles are taken out (`TracedStreets.Thinned`) closes it.
- **The pad of walk at an aisle's T carries a sliver of grass** (OdesaOsm 860,10010): the walk ring struck round the
  corner between a rank's end and the junction leaves a hole a metre across inside the pad. Filling a hole of the walk
  narrower than a walking lane closes it.
- **A dual carriageway's halves converging into each other short of where they join are laid one over the other**
  along the converging stretch (OdesaOsm 3874,4971, ways 485394562 and 973057281, 58 m of 261 m): the lanes of the two
  ways interleave and no lane line is painted. `TracedStreets.Parted` moves a road's middle by one figure along all of
  it and parts only roads over each other along most of their length; a middle that varies along a road, or a road cut
  where its twin starts to converge, closes it. `--bench faults` lists 25 carriageways over each other in all.
- **A block between two whose single roadside stands at opposite kerbs keeps its own width** (OdesaOsm 2605,21009,
  road 2+2 bare between two 2+2 with one roadside; `--bench widths` lists 61 such roads in all): `RoadsidesHeldThrough`
  evens a kerb at a time, and `Roadsides` puts a single roadside on one side per street, not per avenue. Choosing that
  side along the avenue closes the case; the rest are one-way blocks between two-way ones and stretches past a block.
- **The kerb has a cusp where a roadside begins at a slip's joint** (OdesaOsm 1811,12175): the roadside's taper is
  declined when the box is too short to ease in over (`RoadsideLanes.Tapered`), and the kerb steps round the strip's
  square end. Easing it in along the lane past the box, where the box has no room, closes it.

- **A walker's distance walked is its velocity over the step on the device** (`world_view_walkers.comp`) and the change in its
  position on the host (`TownWorld.ReadTheWorld`); its gait is drawn off the device's, so a frame of the whole of Odesa
  draws 3 instances of 2 034 otherwise than the host's fill (`--bench deviceworld`, 2026-10-12).
- **A body put down is laid where it stood** until its own next turn, and then off covers a view reads two steps behind
  (`WorldLayer.StepsAhead`): a placement reaches a view only with the step that took its signal. The fleet keeps the
  placed pose meanwhile (`TownWorld._carSetBy`), so the body's covers and its pose disagree for that long; a staging
  lays the claims itself (`TownWorld.LayTheClaims`). Laying a placed body's claims at the turn after its signal is
  taken closes it.
- **Two tests of "on its line"**: `Reacquire` takes a lane within `CarOffPathM × OffLineTolerance`, `ReadTheLine` keeps
  a line within `OffTheLineAllowanceM`, which is wider for a tow — so a tow can be refused a lane its line check
  would keep it on. One threshold closes it; it moves tows.
- **`--bench recovery` hitches no staged wreck on any map** (2026-10-19): raised on O10, O30, Odesa and River, and no
  evacuator reaches it in 400 s — "nearest m" is blank, so none is even sent. It reads the same on 68d66060, before the
  tow took any body, so it is older than that change. Tracing why `TakeAWreck` takes nothing the probe stages closes it.
- **A chain's allowance off its line is a pair's** (`TownWorld.OffTheLineAllowanceM`, EVA-5, 2026-10-19): any vehicle
  with something on its arm is given `TowedLineAllowance`, however long the chain behind it, so a truck pulling a towing
  truck swings wider than the figure was measured for and is called off its line sooner. Measuring what a chain of
  three swings and scaling the allowance by the chain closes it.
- **A body held at both ends is laid under the arm at its nose alone** (`TowHitches.Hauler`, `TownWorld.LaidAs`,
  2026-10-19): the truck at its tail claims its own ground, and can be refused ground its own load stands on. Laying the
  body under both trucks closes it.
- **A truck on a rail vehicle's arm is dragged where the train goes and nothing claims for it** (EVA-5, RAIL-9,
  2026-10-19): the train's claim covers its rail lanes and its crossings close ahead of its front, but the truck behind
  its tail is a car the claims never see — on the track and through a crossing whose arms have risen. The train's claim
  reaching over what it drags closes it.
- **A recovery hauls whatever is on the bar** (`TowHitches.CarOnTheBar`, `TownWorld.HaulToTheYard`, 2026-10-19): the
  errand works the arm only on its wreck, but a hand working an evacuator's arm mid-recovery leaves it hauling what the
  hand caught — a car not its wreck to the yard, or a rail vehicle home behind it. The haul checking that the load is
  its wreck, and giving the recovery up where it is not, closes it.
- **`IsUnderWay` checks `OffLineM` against the allowance again** after `ReadTheLine` has already sent any car past it
  into `Rejoin` (SIM-7). Confirm the order of decide, tick and `Switching.SteppedOut` before dropping it.
- **On the device, a car's wheel, lamps and wreck are drawn as last signalled and its place as the view shows it**
  (`sprites_world.comp`): the emit lays where a body is and how it points off the view the host is showing, which is
  what the selection, the paths and the debug layers read, but its steer off the commands the step in flight was sent
  and its flags off the world's own rows — a tick ahead of the place. The steer and the flags carried in the view's rows
  close it.
- **The device's tyre pass runs a car on a tow bar** (`tyre_pass.comp`) where the host's never does
  (`ManagedWorld.StepTheTyres` hands it to `TrailerWheels`): the bar's pass overwrites all four impulses after it, so
  the bodies agree, but the device winds the towed car's throttle back-off, moves its wheels' carry and may lay its marks.
  The tyre pass told which cars are on a bar, and skipping them as the host does, closes it.
- **The coloured solve rounds as the host does** (`precise` and `roundedQuotient` in `solve.comp` and `compact.comp`),
  which the review of 2026-10-11 took to buy nothing since colours are not the host's order. A colour is order-free, and
  a town's contacts are nearly all pairs that share no moving body, which the coloured solve lands to the bit where the
  host does only with them; so they were kept. Nothing holds that parity: lavapipe divides exactly and fuses nothing,
  and the device tier passed on the RX 9070 XT with them stripped as with them (2026-10-12). A lockstep case of disjoint
  contacts run on a driver that contracts, or a ruling that the coloured solve need not keep the host's bits, closes it.
- **A frozen body put down without being contained first is thawed on the device and left frozen on the host**
  (`world_signals.comp`'s placing, `DeviceWorld.RowOf`, against `PhysicsWorld.Release`): the device thaws it and counts
  its rest afresh, the host keeps it frozen, keeps its count and only names it to join the step. A body contained first
  agrees, which is every placing the suite stages; one that is not would freeze at different steps. Which of the two is
  meant (SOL-37) and the other made to match closes it.
- **The device world keeps no grid the host reads** (`DeviceWorld.InBox` answers none, `FrozenBodyCount` nought): a
  picture of a street walks the whole fleet's places (`TownWorld.CarsPlacedIn`), and the census reads no frozen bodies.
  The managed world answers both off its solver.
- **A town stepped on the device leaves the host's ring of marks empty** (`WorldLayer.Marks`): the tyres lay their marks
  into a ring on the device that a frame draws from (`DeviceTyres.GroundMarks`), and nothing is read back, so a reader of
  `TownWorld.Marks` — a page's fill, `--bench sprites` — sees none there, and a tick's marks land in the order its
  threads claimed places rather than the host's (car, wheel) order. Nothing decides on a mark; a reader that needs them
  on the device's host would read the ring back.
- **The metre↔pixel conversion has no one site** (CLAUDE.md, SIM-3): `WorldScale` claimed it and nothing called it, so
  it went with the cleanup of 2026-10-15; the screen's factor is `Camera2D`'s and the art grid is `qq art`'s. Naming
  `Camera2D` as the site, or routing both through one, closes it _owner_ (the wording is the working agreement's).
- **`Road.CrossingSetbackM` is an owner's figure nothing applies**: its only reader was `IntersectionReachM`, read by one
  test and removed with it. Whether a zebra stands that margin off the junction's movements in the code (WLK-10) or the
  figure goes is the owner's _owner_.
- **The self-driver's throttle ceiling is the nominal car's** (`TyrePass.DriveCeilingMps2`) where CAR-11 has a car
  decide on its own figures; the reason it was nominal (`CrossingOnTheTemplate`) went on 2026-09-28. Moving it moves
  behaviour; measure first.
- **osmscan has two answers for a metres tag**: `Roads.Metres` refuses 0 and `Buildings.Metres` takes it. One answer
  changes what a trace keeps _owner_.
- **The claims say two things about held ground**: `claims.md` ("nothing taken is freed") and the road log of
  2026-10-12 against the root log of 2026-10-14 (a stronger rung takes held ground not yet committed). Reading which
  the code does and correcting the other closes it.
- **A movement laid tighter than a vehicle can steer wedges it** (`LaneLines` connectors, `SimConfig.JunctionTurnRoomM`):
  where two arms meet at an acute angle the far-side turn between them gets only the room the lane ends leave — Odesa's
  fork at (1953, 521), connector 331, is 2.49 m against the nominal car's 3.94 m and an evacuator's 5.4 m. Odesa's
  stuck probe (2026-10-16) has evacuator 675, on no tow, run wide onto the pavement there at ~55 s and car 590 behind
  it wedge against it, both at full throttle, for ~150 s, holding a car at the box mouth and a walker on the
  pavement. 85 of Odesa's 1 150 connectors (65 of River's 764) are tighter than the nominal car steers, nearly all
  near-side corners ridden wide without harm, so refusing them to every route would move the town. Laying such a
  turn with the room the car needs, or refusing it per vehicle where its own circle is wider (an exact search for
  those few), closes it. **Once the wedge clears, car 590 stands on the pavement for the rest of a 20-minute run**:
  `Rejoining` takes only a lane within twice `CarOffPathM` pointing its way (`TownWorld.Reacquire`), so a car left
  3.6 m off one is sent on leg after leg, each given up where it stands — the two of them are 51 of the 55 legs Odesa
  gives up over 20 minutes (`--bench stuck --seconds 1200`). A way back onto the nearest lane closes that.
- **A call may be taken for a casualty with no standoff**, as a wreck with no hitching place was (evacuator log,
  2026-10-16): `TakeACall` does not ask `TheStandoff` before taking it. Not seen in a run — a casualty on a pavement
  has its street — and not traced.
- **A bay is driven into and out of at the reverse cap**, 8 m/s (`CarFollower.DriveTheLine`'s `laneless` cap,
  `Car.ReverseMaxMps`): one figure is both what a hand may reverse at and the pace of every manoeuvre at a bay, so a
  car swings into a space at 29 km/h. A manoeuvring pace of its own on `SimConfig.Driving` closes it.
- **Two cars inside one box can still hold each other** (TER-4c.1, `Following.Plan`): over 20 minutes of Odesa
  (2026-10-16) one ring stood ~100 s at (905, 1504) — car 639 at rest on connector 5290, cut by car 666's body, holding
  4.4–5.5 m of that connector at `FirmStraight`, which 666 crosses to get out. A car at rest inside a box with a body
  in its way could yield what it plans as one short of a box does, but `DrivingGround.PlanHold` is released before
  `Plan` runs, so the cut is not there to read; carrying the last answer's cut kind on the car closes it.
- **No town the suite lays has a bay off a bend** (`LegEndsInATownTests.ACarDrivesIntoABayOverTheRoad`): the test
  states GEN-4f's way in kept on the road and passes with `ParkingIn.TheStopIsDeadAhead` taken out, so what holds the
  rule is Odesa's and River's stuck probes (decision log of the parking slice, 2026-10-16). A fixture street with a
  car park just past a bend closes it.
- **A train on `OdesaOsm` swings about its line's speed and off its line** (2026-10-19; traced maps are out of the trains
  work, the owner's call): over 120 s its locomotive ran 21.6–23.6 m/s against 22 and a bogie stood up to 0.26 m off the
  track, where on `Odesa` it holds 15.00–15.01 m/s and 4 mm. The traced line turns sharply at its points, which a coach's
  bogies take as kinks. Laying a traced track's points on a curve no tighter than the least radius closes it.
- **A chain of links is not solved whole in a step** (SOL-38, 2026-10-19; the owner asked it kept track of): the links
  are Gauss–Seidel rows, so a pull on the head of ten bodies on nine bars leaves the head at 7.0 m/s of a common 5.0
  after the sixteen passes, and the chain moves as one only some ticks later (`LinkTests`). A train therefore stretches
  and closes up on its couplings as its traction changes, which the owner accepted for starting and stopping. A chain
  of bars solved whole — its matrix is tridiagonal, so one sweep each way — in each pass closes it.

## Waiting on the owner

- **The physics step waits on the actors' planning** _owner_ (2026-10-14): the owner's model is the physics tick as the
  atom, drawn every tick, and the actors planning on their own every `AgentDecisionIntervalS` of game time in staggered
  groups, with only the holding of the lane taken every tick. A step needs only that tick's `DriveCommand`s, yet
  `SimLoop.Advance` takes the turns and the decisions (8.2 ms of O30's 18.5 ms tick) before `StepBodies` submits it, all
  on the main thread. Planning off the step's path closes it, once the owner rules what gives way when a group's plans
  are late (the step waits at a fixed lag, keeping a run reproducible; the interval stretches; or plans land when
  ready) — and the lane holding taken every tick made cheap: the speed profile evaluated at the car's progress rather
  than walked down the arcs again, and the cars' ticks spread over cores.
- **Whether a car's speed profile and steering are the world's** _owner_ (2026-10-14): they are a 60 Hz controller on
  the body's pose fed a view a step stale — the world's job by the 2026-10-11 ruling's own terms — and the world already
  reads each car along its line and aims its wheel (root log, 2026-10-14 and 2026-10-15). Handing it each turn's stop
  points as well would leave a car the claims and its lines; the ruling keeps behaviour on the CPU.
- **The ground rule TER-7b, a P0, still says it is taken "once a laying (TER-49)"** _owner_: there is no laying, and the
  wheels' ground is read on every world step (`WorldLayer.Step`). The code meets the rule more often than it says; the
  wording is the owner's.
- **`TER-5j` was retired and its number given to another rule** _owner_ (road log, 2026-09-14), against "a retired
  number is never reused". The log no longer cites the old one; renumbering the new one is the owner's.

