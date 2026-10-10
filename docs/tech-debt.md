# Tech debt

**What is known to be wrong, slow, redundant or unmeasured in what is built, and left so.** Each entry says where it
is, what it costs and what closes it; one fixed is deleted, not struck through. Why something came to be is the
owning slice's `decision-log.md`; an absence big enough to be structural is a
[known gap](index.md#known-gaps), and what is unbuilt is the instruments' to report ([CLAUDE.md](../CLAUDE.md)).
**An entry marked _owner_ waits on a ruling** and is not to be closed without one.

## Failing tests

- **`StopBarsTests.ABarStandsASetbackBehindItsCrossing`** (unit) fails, and fails on the tree before the wheel
  claims too. Nothing has looked at why.
- **`ShippedCityTests.NothingItCarriesStandsOffIt(OdesaOsm)`** (maps) fails on the working tree that stands 40 000 people
  and 40 000 cars (2026-10-09): spawn 37 177 stands at x −0.15 m, off the map's own edge. Nothing past the 20 000 the map
  stood before was asked this; spawning on the map's edge, or the map's bounds taking its spawns in, closes it.
- **`DeviceTownRoomsTests` cannot tell a grant written by a rebuild that passed its room from one stood down**
  (`rebuild_grant.comp`, `rebuild_agents_out.comp`): with the stand-down taken out it still passes, the margin the fixture's
  cars read back being the same either way. It does tell the kept walkers put back from left as they were (28 walkers part
  by tick 18). A town where a stood-down grant differs, or a check reading the grants a rebuild laid again left, closes it.
- **Exam cards 44, 79 and 85 fail** on the tree before the wheel claims as well:
  - 44 (four left turns at once): driver 0 never gets out of the box.
  - 79 (left out of the stem on its own green): driver 1 never gets where it was sent.
  - 85 (leaving a roundabout gives way on the zebra): driver 0 is on the zebra for 78.8 s with walker 0 in its half.
- **Exam card 109** (a car on a call gets past a queue at a red) fails since the wheel claims: the car on the call
  reaches the box at 25.0 s, after the queue's head at 17.8 s.
- **`InspectorTests.AnInspectedFrameAllocatesNothing` and `UnitPanelTests.DrawingThePanelAllocatesNothing`** (unit) fail
  now and then in a full unit run — the first four times (3.4 KB over twice, 5.7 KB on 2026-10-09, 3.4 KB on 2026-10-10), the second twice (a few KB, then 8.0 KB) — and pass alone
  every time: both read an allocation counter
  across the whole process while the tier runs classes side by side. Counting on the test's own thread, or running them
  apart, closes it.
- **`RouteSearchTests.SectionedPlanningAllocatesNothing` and `StatusPanelClaimsTests.DrawingTheClaimsAllocatesNothing`**
  (unit) fail now and then in a full unit run and pass alone: the first five times in about twenty runs (two of them in a
  row), 4 296–6 624 B on the test's own thread over 500 searches; the second twice, 7 144 B, then 8 088 B on 2026-10-10. The counter is the thread's, so it is not the side-by-side
  classes; something on that thread allocates now and then — a JIT's rejit is the guess, unconfirmed.
- **`AllocationGateTests.ThisEnginesOwnPhasesAllocateNothingOverAWholeTown(Test)`** failed five times in about twenty
  perf runs, at 8.1, 6.8 and 2.5 B a tick, and again at 5.7 and 1.9 on 2026-10-10. It passes alone and in reruns of the tier, and a per-agent trace of phases 1–3 found no
  allocation. A `P0` gate that flickers is either a real allocation on a rare path or a measurement that counts
  something it should not; which is not known.

## Regressions and figures not taken

- **Hard braking rose with the wheel claims**: 714 → 1 135 in 600 ticks of `--bench devicetown`, 2 100 → 3 382 under
  `--bench drive`, and pairs beginning to touch 15 → 21. The bodies' reading and not the tyres': a wheel read at the
  lattice point nearest it puts a car on a lane its collider does not reach, within 0.35 m of a band, and the traffic
  on that lane brakes for it. Proving a wheel inside a band instead braked harder still (road log).
- **`--bench devicetown`'s tick wanders run to run on one build**: 4.24–5.65 ms a tick over six runs on OdesaOsm
  at 10 000 of each (2026-10-08), where the host town's stayed within 15.7–17.0. Suspected the GPU's clocks and power state, unmeasured; an
  A/B of the device tick needs several runs a side until the device's clocks are pinned or read with the figures. The
  machine's own load moves it too: with another program taking 0.7 of a core, the host town's tick read 18.5–18.8 ms and
  the device town's 6.0–6.5 — so the probe should print the load it ran under, and the device's own figures (its
  kernels' and copies' stamps) are what an A/B is read off meanwhile. `qq prof --gpu` reads the clocks: over a
  windowed device run of OdesaOsm at 40 000 of each (2026-10-09) the shader clock sat at a median of 1.0 GHz and never
  passed 1.3 of the card's 2.52, the GPU 20 % busy in short bursts; the memory clock stayed at its top.
- **A windowed device run allocates in bursts**: OdesaOsm at 40 000 of each with `--device-tick` (2026-10-09, `qq prof
  --counters`) collected nothing but allocated 0.1–0.8 MB at a time a few times a minute, 1.0–1.7 MB a minute over two
  runs, where the same window ticked by the host showed about 0.5. Under both lies a flat ~32 kB/s that is the counters' listener
  itself — the same a second at 1.7 frames a second as at 5. One sampled stack allocates a string under `Interface.Draw`;
  the rest is unattributed. A device's tick of the suite's built town allocates nothing once settled
  (`DeviceAllocationGateTests`), so what is left is the frame's, or growth at OdesaOsm's size that the built town never reaches. An allocation
  trace with stacks over a standing device window closes it.
- **Rule 1 on a device's tick is held by no gate**: `CrossingGateTests` asks only that the town's own tick cross nothing.
  A device's tick records its submits again every tick, each recording branching on what the tick holds, so its count is
  flat in the town but not one figure: 278–301 crossings a plain tick, 328–358 one laying a rebuild that runs on and
  717–783 one waiting for its rebuild, alike on the suite's built town at 132 cars and at 269 (2026-10-10, Debug,
  lavapipe). Recorded once and submitted again — what the frame's own submit is owed (Inconsistencies) — it is one figure
  a gate can hold.
- **Nobody holds what no wheel stands on**: across a lane's end a car on two ways leaves its wheelbase less two
  metres and two half steps unclaimed; a car straddling a zebra narrower than its wheelbase, or nosing over a junction
  its front wheels have not reached, is no body there. The gap per car model is not measured.
- **The device in step is gated only on the town's claims** (`DeviceTownGateTests`: nothing inside anything, nothing past
  its grant, over 600 ticks of the built town): what it keeps on the device and hands back only where it changed — a
  car's wheels (`DeviceTyres.KeptOnTheDevice`), the poses the step left — and what a tick hands back after the decisions
  put off a tick, leaving the town's own for a car moved on from and a body put down meanwhile
  (`DeviceTown.DecidesATickBehind`), are held only by `--bench devicetown`'s counts. A test holding the two equal tick by
  tick closes it, as
  `TheTownHoldsEveryWalkerAsTheDeviceLeftIt` does for the walkers the claims keep and
  `EveryTickReadsEveryWalkerAsTheTownHoldsIt` for the walker tick's table.
- **A walker the walker tick comes to hold after a rebuild was laid is left by that rebuild** (`DeviceWalkers.WantTheWalkers`
  marks the walkers held when the rebuild is laid): its tick would then be laid off a town copy older than the rebuild's
  aim. Not seen over the gate's 600 ticks, where `EveryTickReadsEveryWalkerAsTheTownHoldsIt` would count it; no
  fixture forces it. A walker held since the laying taken back with the rebuild's answers closes it.
- **The poses a town is handed back in part are held only on the suite's two towns** (`DeviceTownPosesTests`, root log
  2026-10-10). A host read of a pose the town was not handed, somewhere those runs never reach, reads a stale figure:
  the interface, a tow arm, a closure, a follow order, a debug layer. Every such read named in the 2026-10-10
  inventory takes the pose back first. A probe that runs a shipped city poisoned, held to one handed every pose
  (`DeviceTownBodies.PoisonsWhatIsUnread`, as the test runs it), closes it.
- **No car backs up in the town the device's own tick is gated on** (`Towns.Built`, in `DeviceTownGateTests`,
  `DeviceTownKeptCarsTests`, `DeviceTownDecidesTests`), so a backing laid on the device under a layer that lands later is
  held only to itself: `DeviceTownRunOnTests` runs a car backing up past a wreck and holds a rebuild that runs on to one
  waited for, both the device's; `DeviceRebuildTests` and `DeviceTownClaimsTests` hold the backing to the town's, laid and
  mirrored at once. A car stood too near a wreck in a gated town (`OvertakeInATownTests.StandTooNearAWreck`) closes it.
- **A tow's ground is laid on the device and held by no gate** (`rebuild_standing.comp`, root log 2026-10-10): a hand's
  is, by `DeviceRebuildTests` and `DeviceTownClaimsTests`, and a tow's differs only in whose ground it is — the truck's
  (`TownWorld.LaidAs`). A truck towing a car through a staged town, held to the town's own laying, closes it.
- **The Android head has not been built since the device tick came in** (branch `gpu-resident-sim`): no Android SDK
  where it was worked on. It compiles all of `src/` against Vulkan, so it takes `app/devicetick` as the desktop does,
  and its shader targets must embed the device's kernels (`copy_runs.comp` among them). A build of it closes this.
- **Nothing since the claims laid where they stand is measured** (root log 2026-10-10: those claims, a car on a pass
  planned on the device, the host walking only the cars and walkers it holds or touched, the device finding its cars'
  decisions): the one `--bench devicetown`
  run on OdesaOsm since was taken beside a game holding three and a half cores and the GPU, the host town's own tick
  90.1 ms against 74.1 before, so none of it compares. A run on a quiet machine closes it — the cars and walkers walked a
  tick among it.
- **A route searched on the device is slower than the host's** (`route_search.comp`, `--bench deviceroutes`, RX 9070 XT):
  a batch of 64 searches on OdesaOsm takes 16.0 ms median and 119 ms at worst driving (host, one thread: 7.3), 25.2 ms
  walking (host 12.2). A search is one thread's chain of dependent loads, so memory latency is the whole cost. Recorded
  inline in the resident tick, a tick that asks a cross-town route waits that long. A search shaped for the device (a
  workgroup a search relaxing in parallel, or nearer searches) closes it — a change to the algorithm, _owner_.
- **The resident town is measured only with no decision running** (`--bench resident`, OdesaOsm, 2026-10-11: 3.03 ms a
  tick with the device finished, 36 976 of 40 013 cars moved). Every figure moves as the decisions, routes and errands
  land in it; a run once they do closes it.
- **Not measured since they landed**: the walker as one wheel, the car no longer asked its ground, and the
  consolidation after them (`WheelGrounds`, `DeviceGround` gone, the walker's pavement read). The figures in the road
  and terrain logs predate all three; the soak after them kept all twelve of OdesaOsm's claims.

## Redundant work left in place

### Claims laying

- **The host's claim table is laid for every agent's worst case at once** (`TownWorld`'s `LaneOccupancy`): each driver's
  room is every way its body, its line and its backing could be over, with every mark linked to them — 95 % of it
  `MostCrossedByOne` (58 on OdesaOsm, a footway's) times every one of a line's 24 ways. At 40 013 cars and 40 000 people
  that is 77.1 M slots, 5.07 GB of the managed heap committed and 49 MB of it resident, against 327 k laid at most
  (`--bench room`). The device's rooms grow to what is laid (root decision log, 2026-10-09); a `DeviceRebuild` held to
  the town's (`Watch`), and every check that feeds the device off the host's table, still lay theirs at this one.
  Closes when the town's table grows (`LaneOccupancy`'s `grows`) and those checks size by what it holds.
- **What else the GPU holds for a worst case** (`qq prof --buffers`, OdesaOsm at 40 000 of each, after the rooms grew to
  fit: 4.66 GiB device-local, 1.92 GiB mapped): every car's line laid for twelve lanes each as long as the town's longest
  (`CarFleet.LineArcs` → `DeviceCarTick._arcs`, `_entries`: 0.84 GiB, on the device and staged), the solver's contacts
  for 48 candidates a moving body (`DeviceSolver`, ~0.9 GiB), and a walker's straight 128 runs whatever it walks
  (`DeviceWalkers._straights`, 0.2 GiB). Closes room by room, each sized to what it holds and grown as the claims' are.
- **The ribbon atlas is on the GPU twice**: `DeviceBodies` and `DeviceWalkers` each lay a `DeviceAtlas` off
  `world.Atlas.Table`, about 1.0 GiB apiece. One shared closes it.
- **A rebuild that runs on is still waited for 0.70 ms at 40 000 cars** (`DeviceRebuild.IdleMs`, `--bench devicetown`,
  OdesaOsm, 2026-10-09): its kernels take 3.3 ms of the device's, 1.4 of them the answering passes, and the host has
  ~3 ms of the tick's own to lay beside them before it needs the answers and the decisions' verdicts — the tick's head
  queued behind the rebuild on the one queue. Closes with the head on a queue of its own, or the rebuild's kernels
  shorter than the host's work beside them.
- **No fixture moves a hold whose only news reaches another over a way past its first piece's**: with
  `claims_show.comp` marking only a moved hold's first way, the device tier passes, and only `--bench devicerebuild` on
  OdesaOsm finds it (42 of 5.6 M answers unlike), a probe that reports and fails nothing. A hold that did not move
  needs answering again only through two holds moving, since one alone only ever tightens what is shown. Closes with a
  staged answering on the device (`DeviceClaims.Lay`) held to `LaneOccupancy`'s.
- **Arrays still kept in scratch memory** (`RADV_DEBUG=shaderstats`, 2026-10-09): `lineWaysAlong`'s
  `out LineWayRow ways[LINE_MOST_WAYS]` in `car_tick.comp` and `plan_cars.comp` (23 KiB a wave; 0.18 ms of a tick and
  0.15 ms of a rebuild), and the atlas's covers (`atlasFound`, 36 KiB a wave), each car's thread in `car_bodies.comp`
  still ~195 µs whatever it stands on. An array passed through a function is what cost the atlas's readers 3–4×
  (root decision log, 2026-10-09); these are untried. Closes with each written straight to where it is read.
- **A layer sent back in part reads past its bound on 9 of 200 rebuilds at 40 000 cars** (`DeviceRebuild.ReadTheWaysPast`),
  a second submit and wait each, the bound being the last landing's count and a thirty-second more. Closes with a bound
  that follows how far the count moves between landings.
- **The backing walks a way's list once for every row it steps over** (`rebuild_backing.comp`'s `backingNextOn`, every row
  the backing laid so far scanned for the next on the way), on one thread: 0.30 ms of the device's with the grants and
  walkers' readings at 125 asks in 100 rebuilds of OdesaOsm, unmeasured alone and quadratic in the asks.
- **A parked car's wheels are read again every laying** (`TownWorld.LayTheStandingBody`) at the pose they were read at
  last time. Keeping the covers needs up to `RibbonAtlas.MostWaysUnderABody` (48) a car, or an arena. The host reads
  them on the channels kept with the car (`StandingCars.ReadOn`) while `DeviceBodies` reads on `CarFleet.Channels`,
  so the two can differ at a bridgehead.
- **The body cut is redone on every answering pass** (`LaneOccupancy` around `AnswerThePlans`, `claims.glsl`): bodies
  do not change while plans are answered, and pass 0 already yields each main claim's limit and cutter.
- **The device lists every body row twice a rebuild** (`DeviceRebuild`, both `RecordLists` calls); only planned rows
  change in between, and bodies come first in the order.
- **A lane join is looked up again wherever it is read** — per cover in `DrivingGround.IsOnItsLine`, per plan in
  `WaysAlong`, in `line_onward.glsl` and `line_ways.glsl`, and every tick in `drive_context.glsl` and
  `TownWorld.Signals` — though it is found when the line is laid. A join a slot beside `CarFleet.JoinBreaksOf`
  closes it.
- **`DeviceBodies` keeps its own connector tables** (`connectorAt`, `connectorTo`), laid once beside the car tick's
  same two; and laid apart from a town's device (`DeviceClaimsProbe`, `DeviceBodiesTests`) it still sends every car's
  line row and chain a laying.
- **The wheel mask makes a round trip**: the device's laying writes it (`car_bodies.comp`), the host mirrors it, and
  the shadow sends the whole car row back (`DeviceCarTick.Shadow`). The laying could write the tyre row's bits 8–11
  itself.
- **Small overwrites**: `car_bodies.glsl` sets `standsToM`, which `rebuild_bodies_lay.comp` overwrites; the host
  builds a cutter for "laid" edits (`DeviceRebuild.Shadow`) that `rebuild_edits.comp` reads only when nothing was
  laid.
- **`signal_holds.comp`'s `HoldOf` binding** is bound in the rebuild to a buffer nothing writes, and
  `rebuild_file.comp` overwrites the piece's hold anyway; `SignalHoldsKernelTests` still binds it with data.

### Car tick

- **The light ahead is found every tick** (`TownWorld.LightAheadM`, `drive_context.glsl`) by `WaysAlong` and
  `AheadPlanned`, though light holds change only at a laying and only the leg clock decides anything off it.
- **The distance to a fixed place is projected every tick** (`ToThePlaceOnTheLineM`, through `SetTheContext`): the
  place does not move and the line changes only in `LayLine`. Watch the projection window's edges if it is kept per
  line.
- **A frame lays every walker whose decision the town takes itself** (`DeviceSprites.LayWhatChanged`, 2026-10-10): about
  3 400 a tick at OdesaOsm — inside, down or on duty — each held by its take-back (`DeviceWalkerTick.TakeTheTickBack`)
  whether its decision set anything or not, and most of the 4 045 walkers a frame lays; an inside one's row is only that
  it is inside, so few are sent. Closes with those decisions taken on the device, or a walker marked only where its
  decision set something.
- **A car the town moves after the step and before a frame is laid by no test** (`DeviceSprites.LayWhatChanged`'s
  `physics.Changes` and `_posedByTheTown`): a step taken with the tick forgets the town's moves before the frame, so the
  suite never reaches it. Closes with a staging that puts a car down between a tick and its frame.
- **The town's watch counts the wounded and the wrecked by walking every walker and car** every tick (`TownWatch.Saw`):
  `TownWorld`'s own sets of them (`_wounded`, `_wrecked`) would answer it, but `PersonFleet` and `CarFleet` clear the
  flags on a slot used again without either set hearing of it. The sets held to the flags close it.
- **The decisions' poses are gathered for every agent put off** (`DeviceTownBodies.RecordTheDecided`), though the device
  takes most of those decisions by the clock alone and the town then reads no pose: about 24 KB a step at OdesaOsm, where
  the town's decisions read some 300 poses a tick (2026-10-10). Closes with the gather reading the verdicts the head wrote
  before it.
- **The hand-back's gather is a submit of its own behind the step** (`DeviceTownBodies.GatherTheHeld`): the agents the
  town holds change with the tick's take-back, which comes after the step is recorded. The bodies' writing back rose from
  0.16 to 0.23 ms a tick at OdesaOsm. Closes with the take-back's changes to what the town holds known on the device, so
  the step's own submit gathers them.
- **A frame with a second seat or a layer over the town still has every car handed back** after its last tick
  (`TownWorld.ReadsEveryCarAfterTheTick`, `Game.Advance`; 1.5 ms of that tick at 40 000 cars): the eye is the shot path's
  fill off the town's copy, and the layers and the inspector read the cars they draw there. Closes with each reading what
  it draws off the device, as the window's moving runs do.
- **What every taken car is told is worked out every tick** (`DeviceCarTick.LayWhatItIsTold`, 1.8 ms of sending a tick at
  OdesaOsm's 40 000 cars with the rest of `Prepare`, 2026-10-09): its scene, pace, allowance and a switch's or a pass's
  figures, for 39 000 cars the town has not read since, though only an order after another car or a call's scene moves
  without the car's own decision. The rows are compared and sent a block at a time (`ResidentRows.LayEvery`). Closes with
  a car's row worked out only where the town changed what it is made of.
- **A switching car's look is the town's on a third of the ticks** (`DeviceTown.LayTheDecisions`): on a tick laying a
  rebuild the device holds claims newer than the readers', and has no copy of the readers' own lists to ask. And a look
  that finds room is the town's to take again from the start (`car_switch_look.comp`'s `SwitchAsks`), swept a second time
  on the host. Closes with the readers' lists kept on the device a rebuild behind, and the step and its swept ground handed
  back with the verdict.
- **A switching car's look costs the device 0.39 ms a tick at OdesaOsm's 40 000 cars** (2026-10-09), 0.37 ms of it waited
  for at the tick's head and another 0.47 at the bodies' step behind it: a read of the atlas (`atlasRead`) walks every point
  of a body's rows one after another, about a third of a millisecond on one thread, and a look reads a step's every station
  at once but each read alone. Closes with a read's rows split across a workgroup.
- **Why a switching car with a step is read every tick is reasoned and held by no test** (`TownWorld.ReadsTheTickOf`):
  keeping or withdrawing the step and the rebuild laying its ground read where it stands, but
  `DeviceTownKeptCarsTests`' staging resolves its step alike when such a car is never read. Closes with a staging whose
  step a stale reading would keep, withdraw or lay otherwise.
- **Letting go of a walked car whose decision the device finds is held by no test** (`DeviceTown.LetGoOfTheWalked`):
  skipping it passes the device tier, because nothing in the suite changes what decides such a car between its take-back
  and its next walk — only a wreck after the decisions or a player's order before the tick can. Closes with a staging that
  wrecks or orders such a car on its turn.
- **Whether a car finds its own decision reads its owner's stage and bay** (`TownWorld.LeavesItsDecisionToItsLine`,
  `ItsErrandLeavesItToTheLeg`), which the owner's own decision sets without marking the car. The visits' check would
  throw on it, and has not in the suite; no shipped city has been run with the check on. Closes with the car marked
  touched wherever its driver's stage or bay is set, or a soak with the check on.
- **Every laying still walks every car the town asks** (`TownWorld.AskTheCars`, `LayThePasses`, `ClearTheClaim`,
  `HoldTheCarsTheTownHolds`, and the asked flags built off the whole list in `LayTheCarsTerms` and the shadow's
  `DrivesDue`), a rebuild every third tick: for a car the device keeps and nobody set anything of since the last laying,
  each does what it did then. The device's own walks lay only the cars set since (root log 2026-10-10). The one figure of
  such a car the town reads off its own copy is its hold number (`PlanHold`), after a take-back and for a decision it
  lists. Closes with the hold number handed back with the car, and the town asking only the cars set since the last.

### Walkers

- **A walker's place on its sidestep is found up to five times a rebuild** (`Sidestepping.Lay`,
  `TheBodyInTheSidestep`, `Consider`, `Aim`, `WalkPlannedFromM`; the same five in `walker_actions.glsl`), each a
  `WalkedSince` and an `OnTheWalkAt`, with nothing moving in between.
- **The device lays sidestep rows nobody reads** in a rebuild that leaves the walkers to the town (`walkers_body.comp`;
  the claims-only device takes passes from the host's unit), and `walkers_plan.comp`'s `ways[]` are read only by the
  shadow.
- **A rebuild that keeps the walkers still writes every walker's `WalkOut`** (`rebuild_agents_out.comp`), which it never
  reads back.
- **A walker's own way is laid as a second piece** (`WalkingGround.BodyCover`, `walkerBodyCover`) when the point read
  already found it; `LayBody` merges the two.
- **A paused casualty's slide is stepped twice** (`WalkerFollower`'s paused branch, then `TownWorld.Settle`; the same in
  `walker.glsl` and `walkers_tick.comp`), the second overwriting the first with the same inputs.

### Resident town

- **The same static tables are on the device up to four times**: `RoadGraph.LaneLengthM` laid by `DeviceCarTick`,
  `DevicePlans`, `DeviceSwitchLook` and `TownState`; the connectors by three of them; the lanes beside by
  `DeviceSwitchLook` twice and `TownState`. Load-time memory and upload. Closes with the hybrid's kernels reading
  `TownState`'s tables through the root, or going with the hybrid.
- **Every route slot's scratch is sized to the larger network** (`DeviceRoutes`): 24 B a link a slot, 498 MiB on OdesaOsm
  for 64 slots. Fewer slots, a pool a network or packed link state closes it.
- **A device search reads every surcharge laid, spent or live, on every relaxation** (`route_planner.glsl`); the host
  reads the live list (`LinkSurcharges._live`). Same answer, up to 64 reads a turn once the town marks ways. The kernel
  that lays the marks keeping a live list closes it.
- **Route requests and answers sit in host-visible memory** (`DeviceRoutes`) so the suite's and the probe's helpers reach
  them; every kernel read of one crosses the bus. Device-local tables once the asking kernels exist closes it.
- **`route_search.comp` keeps an idle second lane in every workgroup for lavapipe**, which in a workgroup of one runs a
  loop's body once more with no lane active and reads past a table. A minimal reproduction reported to Mesa, or a
  workgroup a search, closes it.
- **A resident town is laid off one tick of the hybrid** (`ResidentTown`, `DeviceTown`): the hybrid's whole host machinery
  runs once at load. Laying each table directly closes it, and goes with the hybrid.

### Device mirror

- **`DeviceRebuild.Shadow.SlotOf` scans every slot** for each host hold with a cut.
- **`DeviceTyres` is laid its grounds and figures once**, at construction: `TownWorld.FiguresChanged` retunes the
  host's `WheelGrounds` and the device keeps the old ones.
- **Which poses a step writes back is found by walking every car and walker** (`DeviceTown.MarkWhatTheTownLeavesUnread`,
  `DeviceTownBodies.HandBack`), every tick, though about 950 of OdesaOsm's 40 013 cars and none of its walkers are
  written. That walk is most of what is left of the bodies' 0.43 ms of writing back. A list of the agents the town
  reads, kept as each comes to be read or stops being read, closes it.

## Inconsistencies

- **The device town's tick and the frame's emit cross the wall in submits of their own** (`DeviceTown.TickTheRest`: the
  head's, the tick's and the step's each tick, each waited for; `DeviceSprites.EmitForTheFrame`, one a frame), where the
  root decision of 2026-10-06 and `runtime/docs/requirements.md` have a windowed tick's dispatches ride the frame's one
  submit. Each is a fixed count whatever the town, so rule 1 holds; the arrangement written down is not the one built.
  Closes with the two recorded into the frame's command buffer, or the arrangement restated — _owner_, since the
  decision is theirs.

- **What draws the claims reads a layer sent back in part** in a town the device ticks (`DeviceRebuild.LandsLater`): the
  debug overlay (`DebugOverlay.Occupancy`, `.Pointer`) and the instruments that walk a way's lists (`StuckProbe`,
  `ExamProbe`) see only the ways the readers asked about lately and the lights' holds of the rest, and a hold's pieces
  (`LaneOccupancy.PiecesOf`) not at all. A whole layer read back while the overlay is open closes it.

- **A rebuild that runs on has which wheels stand on a way read before it is taken up** (`DeviceRebuild.AnswersDue`,
  `TownWorld.OnTheGroundOfItsBody`): the rebuild writes it into every car's tyre row itself (`rebuild_agents_out.comp`),
  and the tick lays it over from the town's figure for a car the device does not take or lays whole that tick; and a
  wreck's tyres are settled before the tick is handed over (`TownWorld.Settle`, in `ReadyTheStep`) on the town's figure
  too. Neither moved `--bench devicetown` or `DeviceTownRunOnTests`. The tyres of a car the town lays reading the
  device's wheel bits, and the step readied after the answers, closes it.
- **Two tests of "on its line"**: `Reacquire` takes a lane within `CarOffPathM × OffLineTolerance`, `ReadTheLine` keeps
  a line within `OffTheLineAllowanceM`, which is wider for a tow — so a tow can be refused a lane its line check
  would keep it on. One threshold closes it; it moves tows.
- **A car the device ticks holds only part of its context**: `CarFleet.Context`'s `AuthorityM`, `PassAsideM` and
  `Blocked` are the device's (`DeviceCarTick.BriefRow`), and the rest is what the town last set. Nothing on the host reads
  the rest today; one that did would read a stale figure, which only the shadow (`DeviceCarTick.Shadowed`) would show.
- **Two straights for one walker** while the device keeps the walkers: the device's walks commit them and the town's
  (`WalkingGround.StraightOf`) are left as they were, so a rebuild the town lays itself walks the town's, and a walker new
  to the device is sent the town's. Nothing else reads them; reading the device's back where it commits one closes it.
- **The host's rest counts and touching pairs stand still while the device steps the bodies** (`PhysicsWorld._restTicks`,
  `_previousKey`; `kernels/settle.comp`, `kernels/touches.comp`). They are what the device was handed, with the town's
  own resets since. A town whose device is unset goes on from those. A body can then freeze late or early once, by up to
  the rest time, and a pair the device saw begin and stop touching can begin again or go unreported once. The probes
  unset it only as they finish, and nothing steps a town after that today. Reading both back from the device when it is
  unset closes it.
- **The resident car tick takes an overtaking car off the lane of its pass** (`resident_lists.comp`'s `takes`), which
  `DeviceCarTick.Takes` leaves to the town's own tick. `overtaking_IsOffTheLaneOfItsPass` asked there closes it.
- **A resident rebuild reads the car terms and claim rows the town last laid** (`DeviceRebuild.RecordResident`): no car
  is asked anew, so every car is asked as at the hand-over and none is held standing. A kernel laying them off the device
  (`AskTheCars`, `TermsOf`, the claim rows) closes it.
- **A resident rebuild never grows a room**: the rooms are grown ahead off a count read back (`GrowAhead`), which nothing
  reads now, so a town past them lays short. The device telling the host it passed one, and the host growing it then,
  closes it.
- **Three of a walker's fields have several copies on the device**: whether it walks, is wounded or inside in the walk
  row's flags, the walker tick's flags and `people_Inside`; where it aims in the walk row and the tick row. Each is laid
  once and kept by whichever kernel writes it; a decision setting one and not the others leaves them apart. One copy each
  closes it.
- **The walk row keeps two fields otherwise than the C#**: a straight never committed to is a flag where
  `WalkingGround._straightToM` is NaN, and the roster a walker's ground is held as is one bit where the C# has three
  values, so a walker held as `Signal` cannot be held. One encoding closes it.
- **Strides and sizes copied from the C# into the device's headers and hosts** (`CarFleetRouteLanesPerCar`,
  `OvertakingMostRunsOfAPass` and the rest in `town_*.glsl`; `DeviceRoutes.MostLinks`, `MostMarks` and the entry room off
  `TownWorld`'s private ones): `TownStateTests` reads past row 0 of some, not all. Each laid in a state row or `TownCounts`
  and read from there closes it.
- **`IsUnderWay` checks `OffLineM` against the allowance again** after `ReadTheLine` has already sent any car past it
  into `Rejoin` (SIM-7). Confirm the order of decide, tick and `Switching.SteppedOut` on both host and device before
  dropping it.

## Dead figures and members

- **The host's units are numbered among the plans and carry holds, and nothing lays either now** (`rebuild_host.comp`'s
  `HostPlans` filing and its holds, `DeviceRebuild`'s `_hostHolds`, `_hostHoldUnit`, `Numbering.Plans`; root log 2026-10-10):
  only the passes are a host unit, rows and no holds. Laying the passes on the device takes the host's units away whole.
- **Water's figures feed a catalogue row no wheel reads**: `WaterCoefficient`, `WaterResistance`, `WaterDragMps2` and
  `WaterMarkFactor`. `GroundCatalog`'s `Walkable`, `Preferred`, `Ploughs`, `MarkFactor`, `DragMps2` and `Table`, and
  `GroundSample.Coefficient`, are read by tests alone.
- **`ShellCellsAcrossGridCell`'s doc** justifies its fine cell by every wheel asking every tick, which none does.
- **The bridge level's lattice** in `GroundShapes` is built at load for `GroundLocator.At(point, level)`, which only
  `--bench deviceatlas` asks.
- **`CarCorneringRadiusM(atMps, groundCoefficient)`** is only ever handed `PavedCoefficient`, by the generator.
- **`CarFleet.DrivenFrontShare`** is written once in `Add` and read nowhere: the tyre pass reads the build's.
- **`WalkingNetwork.LaneMOf` floors an edge's length at a literal `1e-4f`**, mirrored on the device as
  `WalkingNetworkLeastEdgeM`. A named figure read by both closes it.
- **`DrivingGround.PlanHold` and `WalkingGround.WalkHold` start at 0**, a real hold, rather than `NoHold`, until the
  first laying. Filling them with `NoHold` at allocation closes it, once what reads them before then is checked.
- **`DeviceWalkers.ProposedRoom`** has no reader outside its class; **`WheelScratch`'s ground** is written and read
  inside one call and could be the stack's.

## Waiting on the owner

- **Setting a body down still reads a disc or a box** (_owner_): `DoorStep.IsTaken`, `BesideTheCar.IsTaken` and
  `Recovery.IsFreeToSetDown`, though the body is then laid by its centre or its wheels. Read the same way, a spot is
  refused for what the body would hold; but for a walker the disc is the only thing keeping it from being put down on
  somebody standing just across a lane's edge, since `ExitSpots.IsFree` asks the static props alone.
- **A car in a bay manoeuvre reads a box under itself every laying** (_owner_): `BayManoeuvring.TheStationItStandsAt`,
  with `_standingOf` and `_standingCovers` kept only so `Lay` and `Hold` share it — a collider read for where a body
  stands. The overtake and the switch answer the same question off their swept runs; that holds up to a flank's
  length less ground behind the car.
