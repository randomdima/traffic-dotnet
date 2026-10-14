# Tech debt

**What is known to be wrong, slow, redundant or unmeasured in what is built, and left so.** Each entry says where it
is, what it costs and what closes it; one fixed is deleted, not struck through. Why something came to be is the
owning slice's `decision-log.md`; an absence big enough to be structural is a
[known gap](index.md#known-gaps), and what is unbuilt is the instruments' to report ([CLAUDE.md](../CLAUDE.md)).
**An entry marked _owner_ waits on a ruling** and is not to be closed without one.

## Failing tests

- **`StopBarsTests.ABarStandsASetbackBehindItsCrossing`** (unit) fails, and fails on the tree before the wheel
  claims too. Nothing has looked at why.
- **The unit tier's allocation tests flake** (2026-10-13): in 6 runs of 10 one of `InspectorTests`,
  `RoutePlannerTests`, `RouteSearchTests` or the panels' `…AllocatesNothing` counts 3–7 KB on its thread, though the
  routing ones read nothing the change before it touched (secondary claims read merged; 0 in 8 on the tree before).
  With `DOTNET_GCgen0size=0x20000000` none does in 8 runs, so it is a collection landing in the window, set off by
  whatever runs beside it, and not a byte allocated. Each of the ten files reads
  `GC.GetAllocatedBytesForCurrentThread` around its window by hand; one helper that measures again when
  `GC.CollectionCount(0)` moved inside the window closes it.
- **`ShippedCityTests.NothingItCarriesStandsOffIt(OdesaOsm)`** (maps) fails on the town that stands 40 000 people and
  40 000 cars (2026-10-09, still on 2026-10-12): spawn 37 177 stands at x −0.15 m, off the map's own edge. Spawning on
  the map's edge, or the map's bounds taking its spawns in, closes it.
- **Eleven exam cards fail** (2026-10-14, since every agent claims on its own turn; 60, 80, 88 and 91 pass since it,
  and 79 since the claims redesign):
  - 44 and 85, which passed since the claims redesign and fail since the turns: not looked at yet. A card run traced to
    the turn its car was granted what it should not have been closes it.
  - 108 and 109 (getting past a car stood in its lane; a call past a queue at a red): no pass is decided — the
    "nobody reads what a body is doing" entry below.
  - 70, 82, 84, 86 and 89 (a car gives way on a zebra): the car stops behind somebody standing at the kerb
    end of the paint, inside the zebra, and is on it when they cross. The room once kept behind a body going nowhere
    held it short; the same entry.
  - 69 (a right turn on green gives way to somebody crossing): the tier "ground its holder already stands on" went
    with the comparison — a walker on the paint is held by a red over its other walking lane. Two ways
    of keeping it in one pass were tried and refused (road decision log, 2026-10-12).
  - 67 (a left turn on green gives way to the oncoming right turn): driver 1 never gets where it was sent. It failed
    before the redesign too, and nothing has looked at why.
- **Sixteen town tests are skipped** for the decisions no claim can make any more: all fourteen of
  `OvertakeInATownTests` (CAR-46, CAR-50, PER-28) and two of `LaneSwitchInATownTests` (getting past a wreck and slower
  traffic, CAR-53). They come back with the entry below.
- **`TrigKernelTests.EveryAnglesSineAndCosineIsTheHostsToTheBit` fails on the RX 9070 XT** (RADV, the device tier pointed
  at it by hand, 2026-10-12; it passes on lavapipe, where the suite runs it): 6 of 2 098 667 angles differ by one float in
  the component next to nought — ±58.119465, ±64.40265, ±116.23893, the device 1.5657128E-06 where the host has
  1.5657129E-06. Every kernel that turns a heading on such a driver is a float off the host there. Finding which step of
  `trig.glsl` rounds otherwise on RADV closes it.
- **Allocation tests fail now and then in a full run and pass alone**:
  - `InspectorTests.AnInspectedFrameAllocatesNothing` and `UnitPanelTests.DrawingThePanelAllocatesNothing` (unit), at
    3–8 KB. Both read the whole process's counter while the tier runs classes side by side.
  - `RouteSearchTests.SectionedPlanningAllocatesNothing`, `StatusPanelClaimsTests.DrawingTheClaimsAllocatesNothing` and
    `StatusPanelTests.DrawingThePanelAllocatesNothing` (unit), at 4–8 KB. These count on the test's own thread, so something on that thread allocates now and then; a
    rejit is the guess.
  - `ScenarioTests.WatchingATownAllocatesNothing(Exam)` (town), once, at 6.5 KB.
  - `AllocationGateTests.ThisEnginesOwnPhasesAllocateNothingOverAWholeTown(Test)` (perf), five times in about twenty
    runs at 2–8 B a tick. A per-agent trace of phases 1–3 found no allocation. A `P0` gate that flickers is either a
    real allocation on a rare path or a measurement that counts something it should not; which is not known.

  Counting on the test's own thread, or running the process-wide ones apart, closes the first two. What allocates
  closes the rest.

## Regressions and figures not taken

- **OdesaOsm runs at 3 fps and a third of real time** (`qq prof --map OdesaOsm --frame`, 16 cores, RX 9070 XT,
  2026-10-12): every frame spends the clock's cap of 8 ticks. Managed world: 47.6 ms a tick, 1.47 cores busy. Device
  world: 37.5 ms a tick, 1.02 cores busy, the card 3 % busy. The actor layer is the whole of the device tick, on
  the main thread alone:
  - the claims laying, every third tick, is 43 % of the samples, about 48 ms a laying. Answering the plans took 18 %
    of that, `Following.Plan` 11 % and the walkers' station, plan and grants 6 % — before the claims redesign. Since
    it, on the managed world against the tree before it (2026-10-12, 30 s windows): the laying 117 → 106 ms a frame,
    the claims' own methods 76 → 60, the frame 374 → 313 ms. Answering is 29 ms a frame (was 50), asking 36 (was 33):
    `AskUp`, `Add` and the sort in `TakeThemInOrder` are 26 ms of self time between them. Since then a main piece and
    its marks are one wish, a laying's ways each have one row, and the marks are kept in decimetres:
    `--bench room --map OdesaOsm` lays in 27.4 ms on all the cores (2026-10-13; 37.4 before each walker kept its way's
    arcs, span, width and zebra beside it, 32.3 before a way's runs were threaded through one table of slots) —
    walkers stationed 3.0, cars' plans 9.6, walks 4.1, answering 5.1, grants 2.2; O10 3.7 (was 5.5). The claims'
    own methods were 38 % of it at 28.6, and what holds them up is the first read of a way's row, a hold and a run's first
    claim, taken in an order that lands on a new way each time — fetching ahead lost (road decision log). There is no
    laying since: every agent claims on its own turn (the entry on turns below), and none of this is measured since. The
    laying runs on one thread at 1.9 instructions a cycle on OdesaOsm (`qq prof --events`): the claims' store is held
    up by what it does more than by memory, and what waits on memory is what a laying reads by way for an agent taken
    by number — the asked piece's way row, marks and bodies (`LaneOccupancy.AskUp`, `Add`), and a car's line held as
    six arrays of twelve lanes a car (`CarFleet.LaneStartM` and the rest), which `Following.Plan`, `HorizonToM`,
    `TheNextBox` and `DrivingGround.WaysAlong` read a line each of: 14 % of the laying at 0.5–1.8 instructions a cycle.
    One record a lane closes the last. The device world is not measured since;
  - the car tick is 41 % (`DriveTheRoute` 25 %);
  - the decisions are 6 %;
  - trig is 9 % (`sincosf`, `atan2f`, `tanf`).

  Device world again (2026-10-14, 30 s windows): frame 140 ms at 7.5 ticks, the car tick 7.5 ms a tick (9.1 before the
  world read each car along its line, the root log 2026-10-14) and the laying 7.8. The tick is about 25 000 moving cars
  a tick — the cars at rest already take their last tick again (`TicksAsBefore`: of 44.8 M full car-ticks in a minute
  of `--bench drive`, 1.8 M were at rest) — spread over `Drive` (24 ms a frame, the speed profile's walk of the arcs
  ahead and the wheel's lookahead sample) with no part of it over a tenth. Trig is 7 ms a frame (`tanf` 3.3, `atanf`
  2.0, `sincosf` 1.5): the wheel's `atan`/`tan` and the lookahead sample.

  Per agent a tick is about 0.45 µs, against Odesa's 0.19 µs. Running the car tick and the laying over workers, with
  per-worker signals merged in agent order and nothing allocated (`P0`), closes it, with the claims entry below.
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
- **The device world is not measured for allocation or crossings**: `AllocationGateTests` and `CrossingGateTests` step the
  managed world. A tick on the device crosses a fixed count — the world's submit and wait, and the emit's submit a frame —
  but no gate holds it, nor that `DeviceWorld.Step` allocates nothing (its signal buffer grows by doubling past twice the
  bodies). A device gate over the suite's built town closes it.
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

## Redundant work left in place

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
  every kernel that reads them — the solver's, `world_view.comp` and `world_signals.comp` — with the reads a furniture
  row answers guarded (a static second's push, motion into the tick and state), and laying the furniture's rows through
  a staging buffer freed after, closes it.
- **What the host keeps on a device-ticked town that it does not read while the town runs** (OdesaOsm, `qq prof
  --memory --device-tick`, 2026-10-13: a 685 MB host heap, 839 MB resident and 3.36 GB at the load's peak, beside
  1.44 GB on the device):
  - a `ManagedWorld` is stood and thrown away (`TownWorld`'s constructor ends in one, and `HandTheWorldTo` builds the
    device world while it is still held): about 150 MB of solver tables twice over at the handover, and the atlas laid
    on the host only to be uploaded — most of the load's peak. Handing the town its world kind before it stands one,
    and laying the atlas on the device, close it;
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
- **A car still does more each tick than claim and follow its line** (OdesaOsm on the device, 2026-10-14: the car
  tick is 56 ms a frame of 140, since the world reads each car along its line — root log). What repeats what only a
  laying, a new line or nothing changes:
  - **the speed profile's stop terms and corner walk every tick** (`CarFollower.DriveTheLine`), though each is a fixed
    metre of the line between layings, and `PlannedMps`, which only the laying reads;
  - **the lamps' turn read off the arcs ahead** (`CarLamps.TurnAheadRad`) though the turn is the line's;
  - **`TownWatch.Saw` every tick in the live game** (5.3 ms), an instrument.

  The steering and the speed profile themselves are a 60 Hz controller on the body's pose fed a view a step stale —
  the world's job by the 2026-10-11 ruling's own terms. The world already reads progress (root log, 2026-10-14);
  handing it each laying's stop points as well would leave a car the claims and its lines (_owner_: the ruling keeps
  behaviour on the CPU).
- **A walker is still placed on its way by the host** (`WalkingGround.PlaceItOnItsWay`, every laying): a projection
  onto its way's arcs around the metre it held, as a car's was before the world read it along its line (root log,
  2026-10-14). The same — a walker naming its way, the world reading it there with the view — closes it.
- **A car is still projected on the host the tick or two after its line is laid again** (`ReadProgressM` NaN until a
  view read along the new follow arrives, two steps on), and every tick of a line with no lanes (a manoeuvre's pieces)
  or a pass's aim. A follow signalled with the reading's own shift, rather than waited for, closes the first.

### Claims laying

- **A walker's body costs more to keep than to lay afresh** (2026-10-14, root decision log): `--bench room --map O10`
  0.26 ms a tick for a sixth of the walkers against 0.70 every third tick for all of them on the laid tree — 1.8 times
  the work a walker. A moving body is moved in place, but each move reads its slot, its run's neighbours and the main
  claims on the ground it comes to cold. Walkers are also still asked afresh at every turn (`WalkingGround.Claim`), their
  walk's metres measured from the body. Keeping a walker's walk as a car's plan is kept closes part of it.
- **OdesaOsm is not measured on the turns** (2026-10-14): the owner asked for small maps while resources are short, so
  the root log's figures are O10's and Odesa's, and the drive census's and room probe's OdesaOsm figures above are of
  trees before. A `--bench room` and `--bench drive` on OdesaOsm close it.
- **Reds rose with the turns**: `--bench drive` O10 28 against 21, Odesa 3 against 0 on the laid tree (2026-10-14).
  Which red is crossed that was not is not traced; a `--bench drive` red traced to the turn it was committed in closes
  it.
- **A light's hold is placed as its first body stood at its turn** (`SignalHolds.Lay`): a body standing over the bar
  then leaves the light holding nothing, and the ground it pulls off the bar is held by nothing until the light's next
  turn — a follower whose turn falls between can plan over the bar. Nothing has been seen to come of it on the small
  maps' reds; the reds above traced close it or name it.
- **A kept plan is asked afresh each time its car's nose reaches another piece of its line** (`DrivingGround.KeepsThePlan`):
  a piece's rung is levelled by what lies ahead of it and capped by what lies behind, and passing a piece can raise the
  rest. About 170 plans a laying on OdesaOsm with every car planning at every laying. Levelling the held claims' rungs
  again where the nose leaves a piece closes it.
- **Walkers' asks are dearer since asking was cheapened** (2026-10-13, road decision log): idle, `--bench room`,
  against the tree before, OdesaOsm 27.1 ms against 27.4 (cars' plans 8.8 against 9.6, walks 4.31 against 4.13,
  answering 5.19 against 5.10) and O10 3.89 against 3.69 (walks 0.77 against 0.66). The walk stage is
  `WalkingGround.Claim`, into which `AskUp` was inlined: the drivers' own-body branch (`OwnBodyAt`) or the larger
  `AskUp` is the suspect. An annotation of `WalkingGround.Claim` against the tree before, and the walk stage back at
  0.66 ms on O10, close it — and the entry above takes the figures then.

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
  - the room kept to step out behind a body going nowhere — the zebra cards above;
  - a light's hold lying over a body only standing on its way: it now ends at the first body of any kind (TLT-1);
  - TER-5e's tier "ground its holder already stands on" — cards 60 and 69 above.

  A reading of the bodies ahead off the world view, by place — at rest or not, travelling which way — handed to the
  actor that asks, and never carried on a claim, closes it, with the skipped tests and the cards.
- **Two town-wide gates went with the claims' hold and line metres** (`ClaimGateTests`): "no hold is in two pieces"
  (TER-5c.2) and "the rung never grows along a hold" (TER-5g.1). The first holds by construction — a refused hold asks
  nothing more — and has a unit test; the second rests on `Following.LevelTheRungs` alone, and a town gate on it, read
  off the pieces each hold asked, closes it.
- **Odesa is not measured on the redesigned claims** (2026-10-12): OdesaOsm is (the entry above) and stands, so its
  ways fit the 6 553.5 m a claim can name; whether Odesa's do is not known — `LaneOccupancy` refuses to lay a town
  that does not. `--bench room` is not taken on either. Opening Odesa and `--bench room --map OdesaOsm` close it.

## Inconsistencies

- **A walker's distance walked is its velocity over the step on the device** (`world_view.comp`) and the change in its
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

## Waiting on the owner

