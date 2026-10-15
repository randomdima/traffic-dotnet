# Tech debt

**What is known to be wrong, slow, redundant or unmeasured in what is built, and left so.** Each entry says where it
is, what it costs and what closes it; one fixed is deleted, not struck through. Why something came to be is the
owning slice's `decision-log.md`; an absence big enough to be structural is a
[known gap](index.md#known-gaps), and what is unbuilt is the instruments' to report ([CLAUDE.md](../CLAUDE.md)).
**An entry marked _owner_ waits on a ruling** and is not to be closed without one.

## Failing tests

- **`StopBarsTests.ABarStandsASetbackBehindItsCrossing`** (unit) fails, and fails on the tree before the wheel
  claims too. Nothing has looked at why.
- **`ShippedCityTests.NothingItCarriesStandsOffIt(OdesaOsm)`** (maps) fails on the town that stands 40 000 people and
  40 000 cars (2026-10-09, still on 2026-10-12): spawn 37 177 stands at x −0.15 m, off the map's own edge. Spawning on
  the map's edge, or the map's bounds taking its spawns in, closes it.
- **Nine exam cards fail** (2026-10-14, since every agent claims on its own turn; 60, 80, 88 and 91 pass since it, 69
  and 70 since a plan held across turns holds no mark it could not ask, and 79 since the claims redesign):
  - 44 and 85, which passed since the claims redesign and fail since the turns: not looked at yet. A card run traced to
    the turn its car was granted what it should not have been closes it.
  - 108 and 109 (getting past a car stood in its lane; a call past a queue at a red): no pass is decided — the
    "nobody reads what a body is doing" entry below.
  - 82, 84, 86 and 89 (a car gives way on a zebra): the car stops behind somebody standing at the kerb
    end of the paint, inside the zebra, and is on it when they cross. The room once kept behind a body going nowhere
    held it short; the same entry.
  - 67 (a left turn on green gives way to the oncoming right turn): driver 1 never gets where it was sent. It failed
    before the redesign too, and nothing has looked at why.
- **Sixteen town tests are skipped** for the decisions no claim can make any more: all fourteen of
  `OvertakeInATownTests` (CAR-46, CAR-50, PER-28) and two of `LaneSwitchInATownTests` (getting past a wreck and slower
  traffic, CAR-53). They come back with the entry below.
- **Two town rules broke when the world's trigonometry became `Trig`** (2026-10-15, core log; both pass on 5a973d8):
  `Heading.Unit` lays the town as well as turning it, so the suite's town stands a float off where it stood, and each
  rule was kept by less than that:
  - `BuildingsTests.NoBuildingFrontsTheRoundingOfACarPark` (GEN-55): building 28 stands 1.3 m off the middle of a rank
    reaching 9.0 m with 7.8 m of frontage either side — 0.1 m onto the rounding;
  - `WalkingTests.TheWalkDrawnForAWalkerSetsOffTheWayTheBodyIsWalking` (CTL-1a): walker 47's walk sets off 9 cm behind
    the body.
  Each traced to the margin the generator or the re-plan keeps (or does not) closes it; neither is the trigonometry's.
- **The kernels have not been held to `Trig` on a GPU** (2026-10-15): `TrigKernelTests` and `RoundedKernelTests` pass
  on lavapipe, which the suite runs. The device tier pointed at the RX 9070 XT by hand closes it.
- **Allocation tests fail now and then in a full run and pass alone**:
  - `InspectorTests.AnInspectedFrameAllocatesNothing` and `UnitPanelTests.DrawingThePanelAllocatesNothing` (unit), at
    3–8 KB. Both read the whole process's counter while the tier runs classes side by side: the inspector's failed one
    unit run in six on 2026-10-15, and two in three with the suite's shared towns laid behind `Lazy` (which would lay
    each once where `Towns`' `GetOrAdd` lays one on every class that misses at the same moment, and is not taken for
    this).
  - `RouteSearchTests.SectionedPlanningAllocatesNothing`, `RoutePlannerTests`, `ScreenDrawTests`,
    `StatusPanelClaimsTests.DrawingTheClaimsAllocatesNothing` and `StatusPanelTests.DrawingThePanelAllocatesNothing`
    (unit), at 3–8 KB on the test's own thread — one of them in 6 runs of 10 (2026-10-13). With
    `DOTNET_GCgen0size=0x20000000` none does in 8 runs, so it is a collection landing in the window, set off by whatever
    runs beside it, and not a byte allocated.
  - `ScenarioTests.WatchingATownAllocatesNothing(Exam)` (town), once, at 6.5 KB.
  - `AllocationGateTests.AWholeTickOfAWholeTownAllocatesNothing(Laid)`, and once `(Test)` (perf), at 24 B over the
    600 ticks — one small object — in about half of the tier's runs on 2026-10-15, and in 1 of 3 on the committed tree
    (6078de5); never run alone (`--name`, 0 of 3).
  - `AllocationGateTests.ThisEnginesOwnPhasesAllocateNothingOverAWholeTown(Test)` (perf), five times in about twenty
    runs at 2–8 B a tick. A per-agent trace of phases 1–3 found no allocation. A `P0` gate that flickers is either a
    real allocation on a rare path or a measurement that counts something it should not; which is not known.

  Each file reads its counter around its window by hand. One helper that counts on the test's own thread and measures
  again when `GC.CollectionCount(0)` moved inside the window closes the unit and town ones; what allocates closes the
  gates'.

## Regressions and figures not taken

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
- **The device world is not measured for allocation or crossings**, though it is a window's default since 2026-10-15:
  `AllocationGateTests` and `CrossingGateTests` step the managed world. A tick on the device crosses a fixed count — the world's submit and wait, and the emit's submit a frame —
  but no gate holds it, nor that `DeviceWorld.Step` allocates nothing (its signal buffer grows by doubling past twice the
  bodies). A device gate over the suite's built town closes it.
- **The page's device world** (2026-10-15, web log), each found standing it up:
  - **no tier runs it**: `qq web` on River, OdesaOsm and O30 is the whole of what has looked at it. A lockstep would not hold to the bit
    in any case — WGSL cannot forbid a driver to fuse (core log), and `fma` in `world_view.comp` may run unfused — so a
    tolerance probe of the page's world against the managed one, driven by `qq web --ask`, closes it;
  - **what moves is drawn off the CPU's fill and no tyre mark is drawn**: `sprites_count.comp` and `sprites_write.comp`
    bind 19 storage buffers, and the marks' ring is never read back. Packing the emit's tables as the atlas's were, and
    a web half of `DeviceSprites` feeding `TownRenderer.Web`, closes it;
  - **every touched host buffer crosses whole every batch** (`ComputeBatch.Web.cs`): the signals, the resident rows'
    staging, the views and the covers' lists, a few megabytes a tick on OdesaOsm where only the rows marked or written
    need to. Sending marked runs (`ResidentRows`) and reading back the counted rows closes it;
  - **covers that ran past their lists leave that view without covers** (`DeviceWorld.Finish`): the desktop reads
    them again and waits; a page goes a tick without the turns those covers would have fed;
  - **a fill of any word but nought makes a uniform buffer and a bind group each time** (`compute.js`'s `fill`), which
    the solver's laying and `FillRows` ask for. A fill pipeline holding one window per fill closes it;
  - **the page's Debug build stands River in three minutes**, interpreted; only a published page is worth timing;
  - **the Pages build translates the kernels with another glslc than the one they are written against**: Ubuntu's
    apt package in `pages.yml`, years behind this machine's shaderc 2026.1. Its SPIR-V left `touches.comp`'s
    read-only `WasCount` writable in naga's WGSL, the browser refused a barrier behind a loop on it, and the deployed
    page stood every town with no step (2026-10-15). Nothing after the deploy asks whether a kernel parsed:
    pinning the same shaderc in the workflow, and a `qq web --url` of the deployed page that fails on a WebGPU error,
    close it;
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
  - **OdesaOsm runs at half real time on the page** (deployed, 2026-10-15): 27.2 ms a tick, of it the cars' own
    24.5, so 0.5 ticks a frame at 60 fps — the desktop's 6.0 ms car tick on one thread, at the page's single core and
    about twice a desktop core's cost. The cars' ticks over workers (the desktop entry above) wants a page given
    threads, which wants cross-origin isolation and so headers GitHub Pages cannot send;
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

- **`qq prof --memory` quotes a frame rate the window it sampled did not have** (`.qq/prof.py`): the game's read-out is
  its last window at exit, which `--memory` puts 120 s after the samples end, and every `ms/frame` is divided by that
  rate. OdesaOsm sampled at 7 fps was quoted at 60, because by the exit the town had jammed. Reading the frame meter
  over the sampled window, or naming the read-out's own window beside it, closes it.
- **`Person.StartsAtTheWheelShare` has no test** (2026-10-14): owners laid at their wheel and set off on a driven trip
  (`TownWorld.SeatTheOwners`), a load figure, none by default. A town test that an owner laid so is aboard with a driven
  trip under way, and one that a share of none moves no person's draws, closes it.
- **An owner can be kept at the wheel for over a minute in the suite's built town** (person log, 2026-10-02): two
  owners stayed seated because every exit spot near the door was within their own car's panels or held by someone else.
  The door point is a person's breadth off a nominal car's flank, which a wider car reaches past. Checking that the
  spots past it lie on the next bay's way closes it.
- **The unit read-out misreads a driver's car** (`UnitReadout`, drive log "the second watched drive"): a wrecked car
  still prints `wheel` *in the player's hand* off the selection, `off line` reads 0.00 m for a car with no line, and
  `ahead` reads *clear* against a held or hand-driven agent, which takes no claims. A row for each closes it.

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

### The debug layers, while the camera moves

Measured by the review of 2026-10-16 with every layer on (`Test`, `Laid`): nothing allocates, and a still frame is
kept, but a moving one lays its layers again.

- **A quad is turned by an angle**: `ScreenDraw.Bar`/`BandM` take an `atan2` a quad, most of the nodes layer's 0.1–1.3 ms
  relay, the perimeter's up to 1.5 ms, the shell's 0.5–0.8 ms and the wireframe's 0.2–0.6 ms. A quad carrying its
  direction rather than its angle, through its layout and the overlay shader, closes it.
- **The zones wash asks the zone tree a point at a time**, ~5 000 `ZoneTree.At` a moving frame, 0.5–0.9 ms; a grid of
  zone kinds laid once a town closes it.
- **The claims layer samples each claim's ends before culling it** (two `Spline.SampleAt`, ~50 µs at 12–60 cars and
  growing with the town); culling per way off `ChainBoxes` first closes it.
- **`SelectionPath.CarPath` draws the whole route left**, unculled by the view.
- **The zones and collision layers keep a framing only while it is exact**, so a camera following a unit lays them
  every frame; a kept framing a margin wider than the view closes it.

### Code kept twice, or kept for nothing

Found by the cleanup of 2026-10-15 (`qq unused`, the analyzers) and left because closing each crosses slices, changes
a layout or changes behaviour.

- **The device helpers are copied file to file**: `Groups`, `Kernel`, `Device<T>`, `Own`, `Laid<T>`, `Room<T>` and
  `Readback<T>` in `DeviceSolver`, `DeviceWorld`, `DeviceTyres`, `DeviceSprites`, `DeviceAtlas` and `DeviceLanes`;
  `DeviceSolver.Groups` divides by a literal 64, and the `CarBuildRow` array is built twice in `DeviceTyres`. One
  shared helper in `app/devicetick/` closes it.
- **Rows the device carries and no kernel or host reads**: nine fields of `car_build.glsl`'s row and their
  `CarBuildRow` mirror (`maxSpeedMps`, `turningRadiusM`, `sightM` among them), `DeviceSprites.Figures.LensColumns` and
  `LensRows`, and `PersonFleet.Declares` / `WalkerView.Declares` (written by `TownWorld.World`, `ManagedWorld` and
  `world_view_walkers.comp`'s `ViewDeclares`). Each goes with its std430 layout on both sides.
- **`InUiPx` is written three times** (desktop, web, Android heads) and only the desktop's is tested; `InterfaceScale`
  is the one home it would have.
- **The town repeats itself**: `StandCarIn` / `StandAServiceVehicle`, `SendHome` / `GoBackToTheDepot`,
  `IsTheNearestFree{Ambulance,Patrol,Evacuator}To`, the nose-in test and the yard-slot test.
- **One union-find root, three copies** (`FootJunctions`, `CrossingWays`, `LanePlaces`); **three `Median`s and two
  `Units(string)`** in osmscan (`Corrections`, `Gtfs`); **three `Spread`/`Percentile`/`Median`** in the bench
  (`TownShape`, `ConnectorShape`, `ZoneFidelity`), which print differently, so one copy moves probe output.
- **Names of a design that is gone**: the claims' "laying" (`_layings`, `LayingStage`, `LayingTicks`,
  `ClaimsLaidThisTick`, "this rebuild" in the occupancy and footfall docs) though `TownWorld.Occupancy` says there is
  none, and `CutJunctions`, which now only finds sites. A rename across town, bench and tests closes it.
- **Kept for nothing a run reaches**: `ArcSubtract` (295 lines) and its tests, kept "as tooling" by the render log of
  2026-09-16 though no tool calls it _owner_; `CityPlan.ZebraAtEveryStation` is false only in `CrossingsTests`, so no
  map lays the `Crosswalks` path; `ParkingRegistry.Takes`, named by CAR-11b as answerable and asked by nothing;
  `PhysicsWorld.RoomForBodies`, named only by a `DeviceSolver` cref.

## Inconsistencies

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

