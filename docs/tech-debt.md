# Tech debt

**What is known to be wrong, slow, redundant or unmeasured in what is built, and left so.** Each entry says where it
is, what it costs and what closes it; one fixed is deleted, not struck through. Why something came to be is the
owning slice's `decision-log.md`; an absence big enough to be structural is a
[known gap](index.md#known-gaps), and what is unbuilt is the instruments' to report ([CLAUDE.md](../CLAUDE.md)).
**An entry marked _owner_ waits on a ruling** and is not to be closed without one.

## Failing tests

- **`StopBarsTests.ABarStandsASetbackBehindItsCrossing`** (unit) fails, and fails on the tree before the wheel
  claims too. Nothing has looked at why.
- **`DeviceTownBodiesTests.TheTableFreezesAndWakesWhatTheHostsDoes`** (device) fails since the wheel claims (road log,
  2026-10-07). The wheel masks agree in both towns; a pair of cars settling after the shove freezes at step 139 on the
  host and 140 on the device, a pair that never froze inside the window before. Closes when the step the two freeze
  at is traced to the term that differs.
- **Exam cards 44, 79 and 85 fail** on the tree before the wheel claims as well:
  - 44 (four left turns at once): driver 0 never gets out of the box.
  - 79 (left out of the stem on its own green): driver 1 never gets where it was sent.
  - 85 (leaving a roundabout gives way on the zebra): driver 0 is on the zebra for 78.8 s with walker 0 in its half.
- **Exam card 109** (a car on a call gets past a queue at a red) fails since the wheel claims: the car on the call
  reaches the box at 25.0 s, after the queue's head at 17.8 s.
- **`InspectorTests.AnInspectedFrameAllocatesNothing` and `UnitPanelTests.DrawingThePanelAllocatesNothing`** (unit) each
  failed once in a full unit run (3.4 KB and a few KB over) and pass alone every time: both read an allocation counter
  across the whole process while the tier runs classes side by side. Counting on the test's own thread, or running them
  apart, closes it.
- **`RouteSearchTests.SectionedPlanningAllocatesNothing`** (unit) failed twice in about ten unit runs, 4 296 B on the
  test's own thread over 500 searches, and passes alone. The counter is the thread's, so it is not the side-by-side
  classes; something on that thread allocates now and then — a JIT's rejit is the guess, unconfirmed.
- **`AllocationGateTests.ThisEnginesOwnPhasesAllocateNothingOverAWholeTown(Test)`** failed once in four full perf runs,
  at 6.8 B a tick. It passes alone and in three reruns of the tier, and a per-agent trace of phases 1–3 found no
  allocation. A `P0` gate that flickers is either a real allocation on a rare path or a measurement that counts
  something it should not; which is not known.

## Regressions and figures not taken

- **Hard braking rose with the wheel claims**: 714 → 1 135 in 600 ticks of `--bench devicetown`, 2 100 → 3 382 under
  `--bench drive`, and pairs beginning to touch 15 → 21. The bodies' reading and not the tyres': a wheel read at the
  lattice point nearest it puts a car on a lane its collider does not reach, within 0.35 m of a band, and the traffic
  on that lane brakes for it. Proving a wheel inside a band instead braked harder still (road log).
- **Nobody holds what no wheel stands on**: across a lane's end a car on two ways leaves its wheelbase less two
  metres and two half steps unclaimed; a car straddling a zebra narrower than its wheelbase, or nosing over a junction
  its front wheels have not reached, is no body there. The gap per car model is not measured.
- **The device in step is gated only on the town's claims** (`DeviceTownGateTests`: nothing inside anything, nothing past
  its grant, over 600 ticks of the built town): what it keeps on the device and hands back only where it changed — a
  car's wheels (`DeviceTyres.KeptOnTheDevice`), the poses the step left, the walker table (`DeviceWalkerTick.Resident`) —
  is held to handing everything back only by `--bench devicetown`'s counts coming out the same. A test holding the two
  equal tick by tick closes it, as `TheTownHoldsEveryWalkerAsTheDeviceLeftIt` does for the walkers the claims keep.
- **The Android head has not been built since the device tick came in** (branch `gpu-resident-sim`): no Android SDK
  where it was worked on. It compiles all of `src/` against Vulkan, so it takes `app/devicetick` as the desktop does,
  and its shader targets must embed the device's kernels (`copy_runs.comp` among them). A build of it closes this.
- **Not measured since they landed**: the walker as one wheel, the car no longer asked its ground, and the
  consolidation after them (`WheelGrounds`, `DeviceGround` gone, the walker's pavement read). The figures in the road
  and terrain logs predate all three; the soak after them kept all twelve of OdesaOsm's claims.

## Redundant work left in place

### Claims laying

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

### Walkers

- **A walker's place on its sidestep is found up to five times a rebuild** (`Sidestepping.Lay`,
  `TheBodyInTheSidestep`, `Consider`, `Aim`, `WalkPlannedFromM`; the same five in `walker_actions.glsl`), each a
  `WalkedSince` and an `OnTheWalkAt`, with nothing moving in between.
- **The device lays sidestep rows nobody reads** in a rebuild that leaves the walkers to the town (`walkers_body.comp`;
  the claims-only device takes passes from the host's unit), and `walkers_plan.comp`'s `ways[]` are read only by the
  shadow.
- **A rebuild that keeps the walkers still writes every walker's `WalkOut`** (`rebuild_agents_out.comp`), which it never
  reads back.
- **A kept walker's aim makes a round trip**: the rebuild's read sets it on the device, the town takes it up
  (`TownWorld.KeptOnTheDevice`), and the next tick writes it back over the walker tick's table
  (`DeviceWalkerTick.LayWhatChanged` — most of its 1 268 walkers a tick on OdesaOsm). The read could write the tick's
  `aimM` and the town's known row with it.
- **A walker's own way is laid as a second piece** (`WalkingGround.BodyCover`, `walkerBodyCover`) when the point read
  already found it; `LayBody` merges the two.
- **A paused casualty's slide is stepped twice** (`WalkerFollower`'s paused branch, then `TownWorld.Settle`; the same in
  `walker.glsl` and `walkers_tick.comp`), the second overwriting the first with the same inputs.

### Device mirror

- **The shadow's `AlongMps` tolerance** (`DeviceRebuild.Shadow`) allows for a heading grown over, though car rows now
  carry the host's float exactly; only walker rows need it. `SlotOf` scans every slot for each host hold with a cut.
- **`DeviceTyres` is laid its grounds and figures once**, at construction: `TownWorld.FiguresChanged` retunes the
  host's `WheelGrounds` and the device keeps the old ones.

## Inconsistencies

- **Two tests of "on its line"**: `Reacquire` takes a lane within `CarOffPathM × OffLineTolerance`, `ReadTheLine` keeps
  a line within `OffTheLineAllowanceM`, which is wider for a tow — so a tow can be refused a lane its line check
  would keep it on. One threshold closes it; it moves tows.
- **A car the device ticks holds only part of its context**: `CarFleet.Context`'s `AuthorityM`, `PassAsideM` and
  `Blocked` are the device's (`DeviceCarTick.BriefRow`), and the rest is what the town last set. Nothing on the host reads
  the rest today; one that did would read a stale figure, which only the shadow (`DeviceCarTick.Shadowed`) would show.
- **Two straights for one walker** while the device keeps the walkers: the device's walks commit them and the town's
  (`WalkingGround.StraightOf`) are left as they were, so a rebuild the town lays itself walks the town's, and a walker new
  to the device is sent the town's. Nothing else reads them; reading the device's back where it commits one closes it.
- **`IsUnderWay` checks `OffLineM` against the allowance again** after `ReadTheLine` has already sent any car past it
  into `Rejoin` (SIM-7). Confirm the order of decide, tick and `Switching.SteppedOut` on both host and device before
  dropping it.

## Dead figures and members

- **Water's figures feed a catalogue row no wheel reads**: `WaterCoefficient`, `WaterResistance`, `WaterDragMps2` and
  `WaterMarkFactor`. `GroundCatalog`'s `Walkable`, `Preferred`, `Ploughs`, `MarkFactor`, `DragMps2` and `Table`, and
  `GroundSample.Coefficient`, are read by tests alone.
- **`ShellCellsAcrossGridCell`'s doc** justifies its fine cell by every wheel asking every tick, which none does.
- **The bridge level's lattice** in `GroundShapes` is built at load for `GroundLocator.At(point, level)`, which only
  `--bench deviceatlas` asks.
- **`CarCorneringRadiusM(atMps, groundCoefficient)`** is only ever handed `PavedCoefficient`, by the generator.
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
