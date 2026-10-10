# Decision log — the cross-cutting decisions

Why the project as a whole is shaped this way. A decision belonging to one slice lives in that slice's
own log ([index.md](index.md)); only decisions still binding are here, and a superseded one is deleted
rather than annotated. Rules are [requirements.md](requirements.md); how a thing works is its XML docs.

## 2026-10-10 — the CPU keeps no copy of the engine

**The owner's goal (2026-10-10), in their words: "CPU dont need to know state of the world constantly. So no per tick
regular sync needed. CPU dont need a copy of the engine."** This extends 2026-10-06 and 2026-10-08 (goals.md).

- **What is to go is the copy, not its cost.** Tracking which agents the host changed, so that only those are compared
  against the device, makes a copy cheaper to keep. That is not the way.
- **Reads:** the host reads on an event, and only what that event needs — a decision's own rows, a picked unit, a call's
  nearest truck — handed to it or taken back then, as the poses already are (2026-10-10).
- **Writes:** what the host decides goes to the device as a command.
- **No loop by the roster:** nothing the host does walks every agent on a tick or a rebuild.
- **Measured by** the host's work and the bytes crossing a tick, both of which should follow the events, not the town's
  size.

## 2026-10-10 — the host walks only the cars it holds or touched

**Under the owner's goal above.** Every tick the host walked every car to find what to send (`DeviceCarTick.Prepare`),
then every car the device ticked to take back what came back (`DeviceTown.TakeBackTheCars`), then to say which it reads
(`LayTheWanted`), then every car again for which ones leave the device (`Leaving`). At OdesaOsm's 40 000 cars, about 39 000
of them were cars the device keeps, of which the host sets nothing.

- **The device keeps a car the town does not hold** (`DeviceCarTick.TownHolds`): handed back and left there unread. Laying
  one off the town's copy already throws. So the town can have set a figure the device reads only of a car it holds,
  unless something marks the car.
- **What marks a car** (`TownWorld.TouchedCars`): anything that sets what the device reads of a car the town may not hold.
  For now that is an action entered (`CarActions.Enter`) and a wreck (`TownWorld.Wreck`). The walks take those cars and the
  held ones, in order (`AgentSet`). The take-back adds those whose own tick asked something of the town.
- **Every car is still walked** where what each is read or told for changes at once: a watch begun or let go, the town
  deciding or reading every car, or the town laying the claims itself (`DeviceCarTick.VisitsEveryCar`).
- **Held by a check run in every device town the suite builds** (`DeviceCarTick.ChecksTheVisits`). Every car not walked is
  held to what walking it would have laid — taken or not, its action, line, pass, reading, wheels, grant, clock and what
  it is told — and whether the town reads it. A difference throws. The check found the two marks above: a car wrecked
  while the device kept it, and the decisions' own check writing the device's row into the town. The device tier passes
  with it on.

## 2026-10-10 — a car on a pass is planned on the device

**Under the owner's goal above.** Every plan down a car's line is now the device's. Until now the town laid one itself:
the plan of a car on a pass begun, from where it is back in its own lane (`Following.Plan`, `Overtaking.PlannedFromM`).
That plan is the same as any other except where it begins.

- **`plan.glsl` plans such a car from where its pass ends** (`PlanCarRow.passEndsM`). The town sends that metre with the
  car's plan row, or with its terms where the device holds the car. A pass that reaches past all the car means to plan
  grants it the whole of the pass (`CarClaimRow.OnAPass`), as the town's does.
- **Whether a body stands inside the pass is asked of the device's bodies as before** (`rebuild_asks.comp`). Where one
  does, the device's plan is never filed and the car's claim is the hold short of the body.
- **What is gone:** the town's plan of a car on a pass, its host unit, the cars' host and hold units
  (`CarClaimRow.HostUnit`, `HoldUnit`, `HoldOffset`), and `Following.AsksTheBodiesLater`. The host's units now carry only
  the passes.
- **Gates:** `DeviceRebuildTests` and `DeviceTownClaimsTests` hold the plan, its hold and its grant to the town's own. Both
  fail where a car on a pass plans a metre short of its pass's end. Both also fail where a pass that reaches past the plan
  grants nothing.

## 2026-10-10 — the claims laid where they stand are laid on the device

**The owner asked for all the claims' logic on the device (2026-10-10).** This is the first step: what a car holds where
it stands. Until now, a town whose device lays the claims laid the whole rebuild itself in two cases: a car under a hand or
on a tow bar moving faster than its stop speed, or a manoeuvre begun at a bay (`LaysWhatOnlyTheTownAnswers`). Only the
town's own laying could answer those against the bodies and the lights. So every car was taken back and the whole layer
was sent as cut, on every rebuild while the player drove a car or a truck towed one. At OdesaOsm's 40 000 cars that is the
host's own rebuild: its claims phase is 26.3 ms a tick, a rebuild every third tick.

- **The town asks; the device lays** (`TownWorld.AsksWhereItStands`). The town says how each car that does not plan down its
  line stands. A hand's or a truck's ground is sent as the stretches under it, each as its main claim would lay it. The
  geometry is the town's own atlas and lines, through the same code its own laying runs
  (`DrivingGround.WhatCannotBeStoppedShortOf`). A manoeuvre waiting to begin is sent with where its claim was cleared to. A
  begun manoeuvre is sent as where it looks for a body (`BayManoeuvring.AsksOfTheManoeuvre`, answered on the host by
  `LaneOccupancy.FirstBodyIn`).
- **`rebuild_standing.comp` lays them after the lights and before any plan.** It is one thread in the town's order, since
  each car is laid against those before it, and it does what `LaneOccupancy.Reach`, `Take` and `CutHold` do:
  - each stretch reaches against the bodies, the lights' stretches and the unit's own claims;
  - each takes what it meets, which cuts a light's hold or an earlier hand's hold;
  - it places the marks over each stretch.

  The rows and holds are numbered from the unit's own first, and `rebuild_file.comp` files them where the scans put the
  unit. It also files each light's stretch as they cut it. A car's grant is read off the hold it stands in
  (`CarClaimRow.Stands`).
- **What is gone:** the host-laid standing unit, the host's edits to what the device laid (`RowEdit`, `HoldEdit`,
  `rebuild_edits.comp`), and the town-only rebuild. The town now lays a rebuild itself only while its agents are held, or
  where more cars are on a pass than there is room to ask for.
- **Gates:**
  - `DeviceRebuildTests` covers a hand driving flat out over a red light's bar and cars at bays. It fails where a light's
    cut is not filed, or a waiting manoeuvre's hold is laid a metre off.
  - `DeviceTownClaimsTests` gains a staged walked town: a manoeuvre begun with a wreck in what is left of it, one waiting
    on a wreck, and a hand flat out over a bar. It runs 200 rebuilds, all laid on the device, with 610 holds laid where
    they stand. It fails where a begun manoeuvre's hold is laid a metre off, which no gate saw before: the town laid those
    itself.

## 2026-10-10 — the town is handed back only the poses it reads

**The owner's ruling (2026-10-10)**: the host's copy of a pose may go stale on purpose. A town a device steps writes back
after each step only the poses it reads. A town held, or watched by an instrument (TownWatch), still reads every pose.

- **A car's pose is written back where the town holds the car** (`DeviceCarTick.TownHolds`): one it ticks, decides,
  reads every tick (`TownWorld.ReadsTheTickOf`), or one every car is read for, as a frame drawn off the town's copy is
  (`ReadsEveryCarAfterTheTick`). **A walker's** is written back where the town reads it whole: indoors, wounded, on duty,
  picked out or under a hand (`TownWorld.ReadsTheWalker`). Every pose is still copied back into host memory, into one
  of two buffers in turn (`DeviceBodyPhase.Side`). What the host no longer does is write each one into its fleets and its
  table.
- **A pose left unread is taken back when something asks for it** (`ITownDevice.TakeThePoseBack`, `TakeEveryPoseBack`),
  off the buffer the last finished step wrote. The next step writes the other buffer, so the decisions put off, taken
  while that step runs, can ask too. The town asks:
  - for an agent before deciding it, and for a car the device hands it to decide;
  - for a car as it leaves the device;
  - for a walker about to be written over the device's table;
  - for a car's door, its first line and its lane;
  - for a free ambulance, patrol or evacuator weighed against a call, and for a wreck's occupants thrown clear;
  - for every agent where the town lays the claims itself, works a tow arm, picks or boxes units, or lets a device go.
- **The table is no longer laid whole because it grew.** That would send stale poses over the device's own; a body added
  is a changed row like any other (`RowChange.Whole`).
- **Held by `DeviceTownPosesTests`**: two device towns of one plan, one written every pose and one only what it reads,
  held to each other's device every tick over half a minute of the shoved car park and of the city brief. The partial
  town writes nought-over-nought into every pose it leaves unread (`DeviceTownBodies.PoisonsWhatIsUnread`), so a read
  of one cannot pass unnoticed. It caught the kept walkers' patch, and it fails with the take-back for a car the device
  hands the town left out.

On OdesaOsm at 40 000 of each (`--bench devicetown`) about 950 cars' poses and no walker's are written back a tick, where
all 40 013 and 16 775 were. The bodies' writing back fell from 0.66 to 0.43 ms a tick (0.63 to 0.36 with the bodies
stepped after the decisions). The tick is within noise of before (12.0 ms), and the town drove the same to the count:
388 pairs began touching, 9 016 hard brakings, 157 cars off their lines.

## 2026-10-09 — a switching car's look for room is taken on the device

**Under the owner's 2026-10-08 ruling.** At OdesaOsm's 40 000 cars a switching car's decision was the town's whatever it
came to — 507 a tick asked of the device only to be handed back — and its look for room (`Switching.Shape`, `Sweep`,
`IsUnheld`) was 1.9 ms of a windowed frame, swept on the host off the atlas and the claims the device already holds.

- **A switching car's decision is asked of the device as a following car's is** (`DecideTermsRow.Switching`,
  `TownWorld.LeavesItsSwitchToItsLine`): whether it still means to move across the same way (`car_decide.glsl`), how far
  across the lane beside stands (`car_switch_aside.comp`, off the road's own lanes), whether it looks again yet, and its look
  (`car_switch_look.comp`) — the step drawn (`pass_shape.glsl`), the body read down it off the atlas, the runs merged, and
  their ground asked of the claims the readers hold (`claims_free.glsl`). The town takes what it came to as it takes its own
  (`Switching.Consider`, `Take`, `TakeTheLook`), the ways the look asked about marked asked, so the layer goes on holding them.
- **Only a look that changes nothing the car does is the device's**: one that finds room and asks for a step, or more than
  the device's rooms hold, is more than the clock, and the town takes the decision whole. **And only where the device holds
  the readers' claims**: on a tick laying a rebuild, or where the town laid the claims itself, the town looks.
- **A look is a workgroup**, a thread a station's read, one merging the runs in order and a thread a run asked: the first,
  a thread a look, took the device 8.1 ms a tick where the host had taken 1.9 — a read of the atlas is a long walk, and a
  look of ninety of them in turn was latency nothing hid.
- Held: `DeviceTownDecidesTests` on the traced two-lane tee, a car held up by a wreck on its lane and a second on the lane
  beside holding its step's ground — a town whose device looks held to one whose town looks, every car's pose, hold, action,
  step and switching state every tick, to the bit; every look the device takes to the town's own, figure for figure; and,
  the second wreck left and a patience short enough to run out, the ground it looks at kept known and its clock run out as
  the town's. Each fails with the aside a part in ten thousand off, the claims never free, no way marked, or the clock not
  kept; the runs merged out of order pass, the verdict reading their union. `--bench devicedecide` holds every verdict on a map.
- OdesaOsm (`--bench devicedecide`, 600 ticks): of 5.5 M verdicts 22 unlike — each a look the device found held where the
  town found room — and 10 856 of 136 851 looks alike but for a figure, at most 3.8 mm apart (none of either since: the
  kernel's log, 2026-10-10). `--bench
  devicetown`: the decisions put off 3.54 → 2.27 ms a tick, the device's kernels 1.31 → 1.71 (the looks 0.39), the tick
  12.43 → 11.73; 75 looks a tick taken on the device and 152 decisions not looking yet. The town drives as a town whose
  device looks: 8 899 hard brakings where it braked 9 001, 220 switches asked and 190 made where 226 and 197 were.

## 2026-10-09 — a switching car with no step is left on the device as a following car is

**Under the owner's 2026-10-08 ruling.** At OdesaOsm's 40 000 cars about 2 700 cars a tick were still handed back, and
1 800 of them only because they were moving across (CAR-53): a car waiting on its lane for room beside it waits long
in a full town, and the town read every switching car every tick (`TownWorld.ReadsTheTickOf`).

- **A switching car with no step asked is read as a following car is** — where its decision is due, its row coming back
  by the decisions' own path (`DeviceTown.DecidedOnTheDevice`). Nothing of the town reads it between: what it is told is
  the town's own (`Switching.AsideM`, `Switching.WaitsShortOfTheEnd`), and a step only its decision asks for. **One with
  a step asked or begun is still read every tick**: keeping or withdrawing it reads where it stands, and so does the
  rebuild that lays its ground.
- **`--bench devicetown` says why each car handed back was**: laid whole, decided by the town, switching, overtaking,
  rejoining, read for anything else, or asked by its own tick.
- Held: `DeviceTownKeptCarsTests` stages a car on a street of two lanes sent up an arm only the inner lane turns onto,
  a wreck on the inner lane holding its step's ground, and holds the town to one handing every car back — every car's
  pose, hold, action and step every tick, to the bit, over 30 s in which it was left on the device moving across with no
  step. Reading every switching car, it fails its guard. Never reading one with a step passes too: the staging's step
  resolves alike either way (tech-debt).
- OdesaOsm: handed back 2 700 → 645 cars a tick (switching 1 800 → 38), left on the device 37 103 → 38 835; the town
  driving as before to the count. The tick 12.64 → 12.43 ms, writing what came back 1.98 → 1.79. Its decisions are the
  town's still — 507 a tick asked of the device and handed to the town, against 161.

## 2026-10-09 — a kernel passes no array through a function, and an answering pass skips a hold with nothing new

**A rebuild that runs on was still waited for 2.70 ms at OdesaOsm's 40 000 cars**: its kernels took 5.46 ms of the
device's, the tick's head queued behind them. Timed dispatch by dispatch (stamps after each, taken out again), three
kernels were 65 % of it: the answering passes 1.87 ms, `car_bodies.comp` 1.06 and `walkers_plan.comp` 0.72.

- **An array passed into or out of a function is copied whole** by the compiler (glslang to SPIR-V, RADV): the atlas's
  48-entry room went in and out of every read, and every car's thread took ~720 µs whether it stood on one way or twelve
  — with the room cut to 16 for the experiment, 0.23 ms for the whole dispatch. **A read's covers are the thread's own,
  at module scope** (`atlasFound`, `atlas.glsl`), read by the caller before its next read, and a car's body rows go
  straight to the output (`car_bodies.glsl`'s `BODY_ROWS`). `car_bodies.comp` 168 → 36 VGPRs and 8 → 32 waves a SIMD;
  `walkers_plan.comp` 256 VGPRs with 3 546 spilled → 108 with none, its code 190 → 56 KB.
- **An answering pass answers again only a hold with something new to be answered against**, as the host's
  `LaneOccupancy.NeedsAnswer` does: `claims_show.comp` marks the ways of a hold that moved with the pass, and
  `claims_answer.comp` skips a hold none of whose ways was marked the pass before. Exact, since a hold's answer reads
  nothing but the rows and what is shown on its own ways; the host's own skip rests on the same. The device still runs
  every pass the host could, and a pass with nothing marked costs a read of each hold's ways.
- **Whether two holders stand where they meet is read only where their commitments leave the meeting open**
  (`claimsBeatsOn`): each read walks the way's bodies, and `claimsBeats` decides on commitment first. 1.91 → 1.74 ms of
  the passes alone.
- Held: the device tier, and `--bench devicerebuild` on OdesaOsm — 0 of 31 M slots, 5.6 M holds, 5.6 M answers, 4 M
  grants and 4 M walkers' readings differ over 100 rebuilds. With no way marked, ten device tests fail; with only a
  moved hold's first way marked, none does and the bench finds 42 answers unlike (tech-debt).
- OdesaOsm (`--bench devicetown`): the rebuild's kernels 5.46 → 3.27 ms (the bodies 2.09 → 1.04, the plans 1.42 →
  0.83, the answers 1.95 → 1.40), waited for 2.70 → 0.70 ms a rebuild; the tick 12.98 → 12.10 ms, watched 13.89 →
  13.04 wall; the town driving as before to the count. `--bench devicerebuild`: 5.95 → 3.97 ms chained. The window
  (`qq prof --device-tick`): 19 → 40 fps, 1.5 ticks a frame where it ran 3.2, still keeping time.

## 2026-10-09 — a watch reads every car's grant left off the device, and only a frame has every car handed back

**Under the owner's 2026-10-08 ruling that what is to stop is the copying of the world's state.** Every windowed run is
watched (`TownWatch`), and its past-the-grant claim read every car's grant left off the town's copy, so a watched
device town handed every car back every tick: at OdesaOsm's 40 000 cars, 15.41 ms a tick where an unwatched one took
13.50.

- **The car tick hands back a float a car where the town watches every agent** (`TownWorld.WatchesEveryAgent`,
  `car_out.comp`): its grant left, as the town would read it off the rows it leaves on the device, which the watch reads
  through `TownWorld.GrantLeftM` — the town's own where it holds the car, or moved on from what the device read of it (a
  wreck, whose grant went with its action), as a hand-back leaves it.
- **A frame still reads every car's command, junction ahead and line off the town's copy** — the lamps, the
  indicators, the front wheels — so the last tick before each draw hands every car back
  (`TownWorld.ReadsEveryCarAfterTheTick`, `Game.Advance`), and the ticks between them leave the cars the town does not
  read on the device.
- Held: `DeviceTownKeptCarsTests` holds the figure a watch reads to a town handing every car back, every car every
  tick and to the bit over 600 ticks, one car wrecked while left on the device. Read off the town's copy, it fails at
  tick 1; without the moved-on branch, at the wreck.
- OdesaOsm (`--bench devicetown`, its watched run new): watched, 16.13 → 13.89 ms a tick with the watch (the tick 15.41
  → 13.09, the watch 0.72 → 0.79), the town driving as the unwatched one to the braking. The window
  (`qq prof --device-tick`): 7 → 19 fps, the sim 16.9 → 16.0 ms a tick — inside the 16.7 ms of a tick at 60 Hz, so it
  keeps time at 3.2 ticks a frame where it fell behind at the 8-tick cap.

## 2026-10-09 — a rebuild runs on past the cars the town ticks itself

**Under the owner's 2026-10-08 ruling.** At OdesaOsm's 40 000 cars the town ticks 4.8 driven cars a tick itself — backing
up, at a bay, past a wreck — and no rebuild ran on: each was waited for where its stages reach the plans, the host idle
5.6 ms of it.

- **While a rebuild runs on, a car the town ticks itself is put off** (`DeviceTown.Takes`): left out of the town's turn,
  and ticked by the town (`TownWorld.TickTheCar`) once the tick's own submit is behind it and the rebuild's answers are
  written back — before the layer the last rebuild sent back lands and the holds asked are read, as a rebuild waited for
  leaves it to its turn. Only a hand keeps a rebuild waited for (`TownWorld.HandsOn`): a walker under it is the town's to
  walk in its turn, on what the rebuild made of it.
- **So the bodies' step is laid after them**: the poses the tick reads before its rows are sent
  (`DeviceTownBodies.LayThePosesForTheTick`), the rest — the town's own wheels, every row it changed — once the town has
  ticked and the answers are in, the step's submit behind it. The channels a rebuild finds a car's body on reach the step
  a rebuild waited for would have.
- **A device begins on the town's claims**: until it lays a rebuild, the claims the town laid before it was set are sent
  as cut with its first tick (`DeviceTownClaims.TakeLaidByTheTown`). It had ticked on memory nothing wrote until its first
  rebuild — 224 s a tick on lavapipe, where another test had used that memory before.
- Held: `DeviceTownRunOnTests` holds a town whose car backs up for room past a wreck and drives its pass to one whose
  rebuilds were waited for, tick by tick and to the bit. With the put-off cars ticked before the answers, it fails at
  tick 41.
- OdesaOsm (`--bench devicetown`, two runs): 14.07 → 13.08 and 12.99 ms a tick, the claims 4.28 → 2.19 and 2.23. 199 of
  200 rebuilds run on, and the host waits 2.6 ms of one where it waited 5.6: the tick's head queues behind the rebuild on
  the device. The town drives as before to the braking: 9 001 hard brakings, 387 pairs beginning to touch, 226 lane
  switches asked and 197 made.

## 2026-10-09 — the backing is laid on the device

**Under the owner's 2026-10-08 ruling that what is to stop is the copying of the world's state.** At OdesaOsm's 40 000
cars every rebuild had a car backing up (200 of 200), and the host laid the backing off the whole answered layer: 19.1 MB
of it back in the rebuild's own submit and written into the town (7.7 ms), every walker read and aimed by the town and
sent back whole (2.4 MB), and no layer landing later.

- **The town names each car's ask before the rebuild is recorded** (`TownWorld.TakeTheBackingAsk`) — where its tail
  stands, what it asked for, what it can no longer stop short of: the car's own state, which nothing in a rebuild moves.
- **The device lays it after the answers and the cut** (`rebuild_backing.comp`): one thread over every ask in the town's
  order, each laid against those before it, as `LaneOccupancy.ReachBack` and `TakeBack` lay it — the plan queued behind
  cut back, with its hold's answer and cutter — and every way's lists laid again after. How far each reached comes back,
  a float a car (`TownWorld.BackedUpOnTheDevice`).
- **So every rebuild reads its grants on the device**, and lands later where the walkers are kept; the host's backing,
  its cuts after the answers and the head of the tick that filed them are gone.
- **A row taken out before the lists are laid is on none**, so the lists' order is as long as the scan's total, kept in
  the tally (`Tally.Listed`); a backing that passes the rows' room says the most it could have laid (`Tally.BackedRows`).
- Held to the town's own laying (`--bench devicerebuild`, OdesaOsm, 100 rebuilds, 125 cars' backing): 0 of 31.0 M slots,
  5.6 M holds and answers, 4.0 M grants and 4.0 M walkers' readings differ; the backing, the grants and the walkers'
  readings 0.30 ms of the device's. `DeviceRebuildTests` fails where the backing's cut, or its exception for the plan
  queued behind, is taken out.
- OdesaOsm (`--bench devicetown`): 18.69 → 14.07 ms a tick, the claims 8.63 → 4.28. A rebuild sends 0.30 MB against 2.66
  and reads 0.79 against 21.95 in its own submit, its layer 1.80 MB later in part; the host writes it back in 0.55 ms
  against 7.71, and writes 79 walkers whole a tick against 4 979. **Every layer there lands a tick later now**, which none
  did: 9 001 hard brakings against 8 934 (the host's own 8 984), 387 pairs beginning to touch against 379 (388), 226 lane
  switches asked and 197 made against 245 and 216 (233 and 207).

## 2026-10-09 — the device's claims are laid in rooms grown to what the town lays

**The owner chose rooms grown to fit over a tighter fixed bound.** What the device laid was every agent's worst case — a
car's plan 24 ways times the busiest way's 58 marks, at OdesaOsm's 40 013 cars and 40 000 people 77.1 M slots, a 48 B row
each kept five times over — where a rebuild lays 327 k at most (0.42 %), a car's plan 44 pieces and a walk 35 rows
(`--bench room`). 34 GB of the GPU's for it, 3.5 GB of that past the card and in system memory.

- **Every room the device lays into begins near what a town lays, and grows** (`DeviceRebuild.Rooms`): the rows, a car's
  plan and a walk, kept at twice what the last rebuild laid — to the power of two past it — grown ahead of the next
  rebuild, and never past what could ever be laid. A buffer grows in place (`GpuBuffer.Grow`), every set bound over it
  pointed at the new one. What the host lays itself — its own rows, its cuts, its cars' asks — grows before it is written.
- **A rebuild that passes a room did not happen** (`claims_tally.glsl`'s `TallyOverflowed`): a plan or a walk past its
  room lays what fits and counts the rest, the tally says a room was passed and every count after it is nought, and every
  kernel that writes what outlives the rebuild — its rows, the grants, the tyre rows, the kept walkers handed back —
  stands down. The host grows the rooms to what was laid, puts the kept walkers back as the rebuild read them in, and lays
  it again at the same instant. The straights the first laying committed stay: laid again off the same rows, every walker
  commits the very same.
- **A rebuild that runs on is never laid again**: the tick beside it read what it left. None runs on before a rebuild has
  fitted, and one that passes a room there is a defect, and throws.
- **The host's own table is still laid for every worst case**: it is address space and not memory (49 MB of its 5 GB
  resident at 40 000), and every check that feeds the device off it sizes by it.
- OdesaOsm: 1 048 576 rows (48 MiB a copy, against 3 527), 128 pieces a car's plan (1 416), 64 rows a walk (295), grown
  once and no rebuild laid again in 600 ticks. The device's buffers 4.66 GiB device-local and 1.92 GiB mapped, against
  15.1 and 16.5; the process 6.3 GB of VRAM and 2.0 GB of system memory the GPU maps, against 17.0 and 20.4. The town
  drives to the figure as before — 8 934 hard brakings, 379 pairs beginning to touch, 245 lane switches asked and 216 made
  — at 18.69 ms a tick against 18.74 (`--bench devicetown`). `DeviceTownRoomsTests` holds a town begun with room for
  nothing, and told every seventh rebuild it has none, to one whose rooms never ran short, tick by tick.

## 2026-10-08 — the engine is the device's, and the host runs a tick behind it

**The owner ruled it, for a town whose tick the device runs**, extending 2026-10-06: the engine runs on the GPU, and the
host keeps only work that is occasional, light and copies little. What was measured against it (OdesaOsm, 5.80 ms a
tick): ~88 % of the 4.3 MB crossing a tick went back only to keep the host's copy of the town current, and the host and
the device took turns rather than working side by side.

- **A decision may take its claims answers from the device a rebuild later** (at most three ticks), rather than read a
  layer copied back to the host.
- **The host may run a tick behind the device**: it decides on tick N while the device runs tick N + 1, so a decision
  reads the town a tick older and takes effect a tick later (16.7 ms, against a reaction of 0.1 s).
  - **Built as decisions put off a tick** (`DeviceTown.DecidesATickBehind`, `TownWorld.TakeTheDecisionsPutOff`): the
    decisions due on a tick are taken while the next tick's submit runs. What that tick hands back leaves the town's own
    where a decision changed it meanwhile — a car laid a line or set doing something else keeps the town's reading and is
    laid whole next tick; a body put down or put away keeps the town's pose.
  - OdesaOsm (`--bench devicetown`, two runs): 5.56 → 5.31–5.37 ms a tick. The decisions (0.70 ms) are hidden only where
    the host has nothing else in the wait: on the tick a layer lands, landing it (1.5 ms) and the decisions outlast the
    tick's submit (1.7 ms). 23 cars a tick are moved on from. The town drives a tick later: 1 139 hard brakings against
    1 152, 104 lane switches asked and 99 made against 96 and 95 (the host's own: 102 and 96), 27 pairs beginning to touch
    against 26; the speed and the cars off their lines equal.
  - **And as a claims rebuild that runs on while the town goes on** (`DeviceTown.RebuildsRunOn`,
    `DeviceRebuild.AnswersDue`): submitted where its stages reach the plans and not waited for there, its answers
    written back once the tick's own submit is behind it and before the decisions read them. Nothing else reads them in
    between but which wheels of a car stand on a way, which the device takes a tick late (tech debt). OdesaOsm: 4.81 →
    4.49 ms a tick, the town bit for bit as before (`DeviceTownRunOnTests` holds the two towns equal tick by tick).
- **The owner, later the same day: what is to stop is the copying of the world's state** — the decisions move to the
  device where they are what makes it be copied, and not for their own sake. What the host still reads is the deciding
  agent's own rows (its neighbours come off the claims), every pose for the host's bodies, and every car for a few
  instruments.
  - **A car the town does not read after a tick is left on the device** (`DeviceTown.ReadsEveryCarBack` off): the town
    names the cars it reads before the device ticks them again (`TownWorld.ReadsTheTickOf` — a decision due on the tick
    or taken while it ran, a switch, a pass, a rejoin, a call, a tow, the selection), the tick hands those back and every
    car whose tick asked something of the town (`car_out.comp`), and every other car's reading, command and tyres stay
    there until it leaves the device's tick or the town reads every car (`ITownDevice.TakeEveryCarBack`). The rebuild
    clears a held car's claim on the device too (`car_claims_gather.comp`), where the town's clearing had sent every
    moving car's reading again every third tick. A car about to be laid off a copy older than the device's is a defect,
    and throws. OdesaOsm: car rows sent 112 → 25 KiB a tick, 6 430 of 10 005 cars a tick left on the device, the tick's
    submit 0.91 → 0.51 ms; the town bit for bit as before (`DeviceTownKeptCarsTests`).
  - **A car's decision put off a tick is asked of the device first** (`DeviceTown.DecidesTheCars`, `car_decide.glsl`): at
    the head of the next tick, of what the last left, whether it is the car's leg's clock alone
    (`TownWorld.TakesOnlyTheClock`) — and where it is, the clock is run there and nothing comes back; where it is more, what
    the town decides it on is handed back and the town decides it once the tick's submit is waited for, the walkers'
    decisions still taken while it runs. What only the town knows is told (`LeavesItsDecisionToItsLine`, what of a switch
    the road and the route answer), and each car's clock is the device's while it holds the car. A car laid whole is
    handed back the tick after, so nothing the device's tick alone writes is read off a copy older than it. A decision is
    asked of the claims the town's readers read, which on the tick a rebuild is laid whose layer lands later are the
    claims before it (`DeviceCarTick.ReadersKeepUp`): the device keeps a copy of every grant, cutter and hold bit as the
    readers have them. OdesaOsm: 9 577 of 10 005 cars a tick left on the device, 1 584 decisions a tick run there and 12
    handed to the town, car rows in and out 0.25 → 0.11 ms, 4.35 → 4.14 ms a tick; the town bit for bit as before
    (`DeviceTownDecidesTests`).
  - **And a walker's** (`DeviceTown.DecidesTheWalkers`, `walkers_decide.comp`), of the kept table, where its body stands
    and its hold as the readers read it (`walkers_readers.comp`) — asked only of a walker the kept table holds as the town
    does (`DeviceWalkers.HoldsAsTheTownHas`), its clock restarted there where a rebuild walked it on. The tick's head —
    what is sent and the decisions asked — is a submit of its own, and every decision is taken while the rest of the tick
    runs. OdesaOsm: 620 walkers' decisions a tick run on the device and 81 handed to the town; the town bit for bit as
    before. It buys little host time — a quiet walker's decision was cheap, the town's time going on trips drawn and
    routes searched — and is what lets a walker's rows stay on the device next.
  - **A rebuild hands back a car's plan and grant only where the town holds the car** (`DeviceRebuild.AnswersOnlyWhatTheTownHolds`,
    `rebuild_cars_out.comp`): of every asked car only the hold its plan was numbered, which the readers' layer is read
    by; of a car found on other channels than the town laid it on, its channels; and of a car the town holds as the
    device left it, the rest. No host code reads another car's plan or grant — its neighbours come off the claims — so a
    car left on the device keeps its plan and grant there, and they come back with its reading
    (`DeviceCarTick.OutRow.Grant`, and which wheels stand on a way with its tyre row). A grant the town would lay off a
    copy a rebuild left behind is a defect, and throws. OdesaOsm: a rebuild's own read 1.75 → 1.22 MB and 1.24 → 0.87 ms
    of the device's; the town bit for bit as before (`DeviceTownKeptCarsTests`).
  - **And a layer that lands later sends back no hold; the town asks for the holds it reads** (`DeviceRebuild.HoldsAsAsked`,
    `readers_holds_out.comp`). A hold is read only by its own holder — in its decision, or the town's own tick of it;
    what reads another's, the checks, reads a layer sent back whole. So each tick's head asks for the
    holds of the agents the town decides — of one whose decision the device was asked, only where it hands it over —
    and of the cars it drives itself next tick, off copies of the layer the device keeps as the readers have it; a hold
    read that was not sent throws (`LaneOccupancy.KnowsEveryHold`). Only where the town takes every decision once the
    device's verdicts are in. While the agents are held, the layer sent back lands before the decisions put off are
    taken, their holds waited for; a town the device lets go has every hold read back, and every car it keeps handed
    back, before it takes them. OdesaOsm: a layer sent back 1.80 → 0.30 MB; the town bit for bit as before.
  - **A walker a rebuild changed is handed back only where the town reads it** (`DeviceWalkers.LeavesTheUnread`,
    `walkers_kept_out.comp`): whose decision the town takes on the rebuild's tick, one indoors, wounded, on duty or
    selected, or every one where the town watches every agent; and one whose rebuild raised anything
    of it. Any other is left on the device, the town holding it as the rebuild before left it, and is taken back with
    the row its decision was asked of where the device hands that decision over — or whole, where the town lays the
    claims itself, strikes it down, takes it in hand or is let go. Its walk's clock comes with it: the device's, where it
    ran on the way taken since, or restarted as the town's own taking up restarts it — a restart is the same whenever it
    comes before the walker's next decision. A walker the town would write over the device's row while it is left there
    is a defect, and throws. Every walker's hold comes back with them, numbered on the device. OdesaOsm: 1 136 of 4 165
    walkers a rebuild changed handed back, the walkers' read 0.40 → 0.16 MB a rebuild; the town bit for bit as before.

## 2026-10-08 — the readers' layer is sent back in part, and a decision asks for the ways it reads

**Under the ruling above** that a decision may take its claims answers a rebuild later. The whole layer came back every
rebuild (4.7 MB on OdesaOsm, written into the readers' layer in 1.5 ms) for what the decisions ask of it: about ten
thousand questions a tick about an agent's own hold, and a few dozen about bodies and plans on particular ways.

- **Of the holds, only those the town reads come back**, asked for as it reads them (`DeviceRebuild.HoldsAsAsked`, under
  the ruling above), so a question about an agent's own hold is answered as before.
- **Of the rows, only the lists of the ways the readers asked about lately** (`LaneOccupancy.Knows`, kept asked for a
  second), and the lights' holds of every signalled way, so a light ahead is read on every way
  (`rebuild_ways_out.comp`).
- **A reader asks before it reads a way's lists, and a way not yet sent is asked for**: a pass, a step, a manoeuvre or a
  sidestep is drawn, asked, looked round for or kept at the next decision or tick instead, its clock left as it was; a
  spot to put a body down on such a way is taken until it is sent; a lane of a scene is closed as though the scene stood
  on it. **The ground outside a way out is asked for whole** before any spot on it is tried: tried a spot at a time,
  every spot was refused in turn and the decisions took a quarter longer.
- OdesaOsm (`--bench devicetown`): a layer is 1.80 MB against 4.71 (2 279 ways asked about, the lights' holds of 2 654
  more), written into the town in 0.61 ms against 1.52; 5.31–5.37 → 5.04 ms a tick, the decisions' time unchanged. The
  town drives the same as the host's own to within a car: 1 131 hard brakings (the host's 1 147), 102 lane switches asked
  and 97 made (102 and 96), 27 pairs beginning to touch (26).

## 2026-10-08 — a car the device ticks is laid for the claims off what the device holds of it

**Under the ruling above.** A rebuild sent every car's body and plan's rows from the host (0.96 MB on OdesaOsm), though
the device held all of it: the pose its bodies' step left, the reading its tick left, the line, the grant and the context.
Now a car whose reading the car tick holds as its own (`DeviceCarTick.HoldsTheReadingOf`) is laid on the device
(`car_claims_gather.comp`), off those tables and a few terms only the town knows, sent where they change; the town lays
only the cars it ticks itself.

- **The way a body faces is its own**, the forward the bodies' step left, where the town laid it on the unit vector of
  its heading; every other figure is the town's to the bit (`DeviceTownGateTests.EveryCarTheDeviceLaysIsLaidAsTheTownWouldLayIt`).
- OdesaOsm (`--bench devicetown`): a rebuild sends 0.15 MB against 1.10 (0.17 ms against 0.68), 5.71 → 5.56 ms a tick;
  the town drives the same to the car.

## 2026-10-08 — a layer lands on the second tick after its rebuild, copied back in halves

**Within the owner's 2026-10-07 ruling that a decision may read a layer one laying old.** Copied whole behind the
rebuild's tick, a layer (4.7 MB on OdesaOsm) outlasted the host's time before the next submit, and that submit — behind
it on the device's one queue — waited 1.45 ms a rebuild for the rest. Now half is copied behind each of two ticks'
submits and the layer lands on the second (`DeviceRebuild.SendTheLayerBack`). A rebuild comes every third tick, so the
readers read the newest layer one tick in three rather than two, and never one two rebuilds old.

- OdesaOsm (`--bench devicetown`): 7.08 → 6.64 ms a tick, the agents' share 4.10 → 3.65. What is decided on the layer
  waits a tick longer: 96 lane switches asked against 102 (95 made against 97; the host's own makes 96), and 1 152 hard
  brakings against 1 147; the speed, the cars off their lines and the touches equal.

## 2026-10-08 — the walkers' side of the claims is the device's, and so are its numbers

**Under the owner's 2026-10-06 decision, for a town whose walkers the device ticks** (`DeviceWalkers.Keeps`).

- **The device keeps a walker table of its own for the claims**, and every rebuild runs each stage over it in turn:
  every walker stationed, its body and its pass laid, its walk planned and — with the cars' grants — its grant read, its
  pass's word given and its aim set. Each walker is read in where the bodies' step left it; the town sends only the
  walkers it changed since, and takes back only those the rebuild changed (`TownWorld.KeptOnTheDevice`), with its own
  code for what follows from them — a way taken restarts the give-up clock, a pass made or withdrawn is counted.
- **Its numbers are the device's**: a walker's place on its way is the device's projection, and how fast it goes the
  way it faces is the device's trigonometry's, where the town stationed it to the bit before (`--bench devicewalkers`:
  2 of 2 M stations apart, 7.5 % not to the bit). What is still held to the bit is the claims-only device
  (`--bench devicemirror`), which leaves the walkers to the town.
- **A sidestep's word is given in the rebuild, off its own answers**, so it no longer waits for the layer to land; and
  a layer only lands later where the device keeps the walkers (`DeviceRebuild.MirrorsAtOnce`).
- **The walkers' passes are filed where the town files its own**, after the cars' (`rebuild_walker_passes.comp`).
- OdesaOsm (`--bench devicetown`): 7.61 → 7.08 ms a tick, the claims 2.86 → 2.30. A rebuild sends 1.10 MB against
  2.52 and reads 1.71 against 1.48; the town's own share of it is 1.22 ms against 2.20. 541 of 10 000 walkers are
  written over a rebuild and 4 182 handed back. The town drives the same to the car: 1 147 hard brakings, 97 of 102
  lane switches, 77 cars off their lines, 26 touches.

## 2026-10-07 — a decision's effect on a body is stepped a tick later, and the claims it reads may be a laying old

**The owner ruled both, for a town whose tick the device runs.**

- **The bodies are stepped in the agents' own submit** (`DeviceTown.StepsWithTheTick`, `ITownDevice`): the town readies
  the step at the end of phase 3a rather than the start of phase 4 (`TownWorld.ReadyTheStep` — a casualty's slide, a
  wreck's wheels, the motion into the tick), the decisions read the town the step left, and what a decision does to a
  body — boarding, a car parked or put down, a coupling — is stepped a tick later. One submit a tick instead of two.
- **It bought no time on the link it was measured on**: OdesaOsm (`--bench devicetown`, both orders of the two runs)
  reads 9.10 against 9.39 ms a tick in one order and 9.42 against 8.98 in the other, the town's counts equal to the car.
  A round trip there costs about a tenth of a millisecond besides its bytes, and the bytes are what a submit waits on.
  The rest of a tick at a tick's lag is overlap — host work beside the device's — which this is the first step towards.
- **A decision may read a claims layer one laying old**: the rebuild's own steps take what they need of each agent
  from the device, and the whole layer may come back later than the laying that made it.
  - **What the tail reads comes back with the answers**: every asked car's grant (`rebuild_grant.comp` in the rebuild's
    own submit) and every walker's (`rebuild_agents_out.comp`), so nothing in a rebuild reads a grant off the layer.
  - **The layer lands on a later tick** (`DeviceRebuild.LandsLater`): copied back behind a tick's submit, in the host's
    time between submits, and written into the readers' layer while a later tick's submit runs. Until then every
    reader reads the last laying's layer, with the hold numbers, held-as and asks it was laid with (`LayerBound`); the town
    lays its own few rows into a laying of its own meanwhile. **What is decided on the layer waits for it**: a pass's,
    step's or manoeuvre's keep-or-withdraw, a tick or two later than before.
  - OdesaOsm (`--bench devicetown`): 8.81 → 7.78 ms a tick, the claims 4.30 → 2.96; a rebuild reads back 1.5 MB against
    5.5. The town drives the same — 1 147 hard brakings against 1 127 (the host's own: 1 147), 97 of 102 lane switches
    made against 99 of 105, the speed, the cars off their lines and the touches equal.

## 2026-10-06 — the tick moves onto the device

**The owner decided it: everything that can move to the GPU moves, and its state stays there.** OdesaOsm cannot
run in real time on the CPU tick — 23.5 ms against a 16.7 ms budget at 10 013 cars and 10 000 walkers
(`--bench town`): the claims 6.8, the cars' own tick 12.8, the solver 3.3.

- **Every per-tick reader and writer of a body moves with it**, not the solver alone: the solver is a seventh of
  the tick, and a solver on the device under a car tick on the CPU is a round trip every tick.
- **A tick reads one copy and writes the other**, and the copies swap. Reading only the old copy is what gives
  every agent the same instant, which the proximity index gives now.
- **The CPU keeps the decision clock**, the errands and routing, the instruments and the load. A decision taken
  on tick T's snapshot lands at a fixed tick T + L, never when the readback happens to arrive, so the digest
  does not depend on frame pacing.
- **The tick still crosses nothing** (rule 1): its dispatches ride the frame's one submit.

What each slice gives up for it is that slice's own log, as each one lands.

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
  after it, it is laid in what the atlas freed.
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

**Not done: the paving's build-time indexes let go once the town stands** (`Paving.DrivenLines`, `GroundRings`' kerb
scan, 51 MB). Let go after the peak, they give a page nothing back. **Nor the street lanes' third index**
(`RoadGraph`'s, 20 MB), answered off the two a level each: the nearest of a bridge's lanes from anywhere in town is a
ring grown kilometres wide. **Nor the filing a block at a time**, letting each go once filed: the entries are laid out
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

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for the documentation audit.

- `OBJ-2`: four kinds, not five — a traffic light has no body, its heads being drawn and never collided.
- `SIM-6`: a lifted ban is performed by whatever performs the act elsewhere; the manoeuvres it named are gone.
- `VER-2`: one way round, a bay; the dead-end shunt it cited (`P-19`) is gone.

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

`PUR-1`…`PUR-4` restated [goals.md](goals.md) sentence for sentence — the quality bar verbatim — and
`AGT-1`…`AGT-4`, `OBJ-1`, `OBJ-3`, `SIM-5` and `TEC-3` were definitions, pointers to other rules, or a
constraint on a grid the terrain has never had. Nothing but this page's own ID map named any of them, so
what a reader loses is a second copy that could disagree with the first. `VER-4`, `VER-5` and `VER-7` are
retired with them, as intentions no tier answers and that `VER-3`, `SIM-4` and `SOL-35` already carry.
Retired: `PUR-1`…`PUR-4`, `AGT-1`…`AGT-4`, `OBJ-1`, `OBJ-3`, `SIM-5`, `TEC-3`, `VER-4`, `VER-5`, `VER-7`.

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
