# Physics — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md) and
[solver.md](solver.md).

## 2026-10-30 — a touch carries each body's own speed into the other

A person's tolerance became the vehicle's own speed into them (`PER-23`, `PHY-4`), which a closing speed cannot say:
a walker at 4 m/s into a parked car and a car at 4 m/s into a standing walker close alike. `Touch.Into` is an
`Approach`, each body's speed along the normal into the other, and the closing speed is their sum; the device writes
both into the begun row's spare words (`touches.comp`), so its size is unchanged.

## 2026-10-19 — the solver gained one joint, the link, and damping became each body's

**The owner asked for a real, physics-based solid link for both the tow and the train**, free to spin, with "no
special physics treatment for link" (`SOL-38`). It is a row in the velocity passes beside the contacts — bilateral
along the bar and nothing across it — and its stretch is pushed out with the overlaps, so a chain of a dozen wagons
converges in the solver's own iterations and the GPU's colouring keeps two links of one chain apart. It reverses the
evacuator's 2026-08-26 "two impulses and no joint": that coupling had a settle time and two caps, which a train of
heavy wagons could not be priced with.

**Damping is laid with each body.** A train's wheels pay its rolling losses themselves, and the shared 0.1/s at 22 m/s
asks a 2.2 m/s² of traction a locomotive does not have — meganewtons over a few hundred tonnes. A car keeps the
figures the 2026-10-12 measurement kept; a rail vehicle gets none.

## 2026-10-12 — the damping stays, because the driver's margin is in it

**Measured and not taken: `SolverFigures.LinearDamping` and `AngularDamping` to nought.** Every body has an actuator
to rest by — a car its tyres, a walker its foot or sliding grip, a casualty the sliding grip — and the two worlds keep
the same bits either way, the keep factor being exactly one. What moved was the driving, `--bench drive` against the
tree with both on:

| | hard brakings Exam / Odesa / O10 / OdesaOsm | reds Exam / Odesa / O10 | mean m/s Odesa |
|---|---|---|---|
| both on | 6 / 0 / 121 / 22 714 | 1 / 2 / 11 | 9.90 |
| both off | 569 / 96 / 584 / 40 918 | 10 / 8 / 57 | — |
| linear off | 539 / 3 / 438 / — | 7 / 8 / 57 | 10.49 |
| linear 0.05/s | 61 / 4 / 201 / — | 3 / 4 / 14 | 10.18 |
| linear off, `BrakingMargin` 0.85 | 47 / 3 / 131 / — | 4 / 5 / 31 | 10.18 |
| linear off, brake ceiling at the whole patch | 675 / 2 / 391 / — | 10 / 8 / 58 | 10.52 |
| angular off | 0 / 2 / 137 / 18 357 | 1 / 3 / 13 | 10.00 |

Both off, the soak of every map lost 1 → 5 walkers and 0 → 31 cars on O10, 0 → 55 and 0 → 46 on O30, and the exam
16 cards. **The linear drag is a margin nobody declared**: about 1.4 m/s² at 14 m/s, so every car stops shorter than
its profile planned and is slower than it would be, and neither a wider braking margin nor a higher brake ceiling
stood in for it. **The angular one alone** traded Odesa's soak over 24 seeds (8 → 11 down, 6 → 8 wrecked, 146 → 171
pairs touching) against the Exam's (18 → 10 wrecked) and OdesaOsm's hard brakings, so it stays too.

## 2026-10-12 — the ray cast is retired, and nothing looks down the road at a body

Nothing in the town cast a ray: what is ahead of a driver is the claims', with no second mechanism behind them
(`TER-4c`). The cast was kept for its tests alone, and with it a moving index re-laid between steps for a query
nobody asked, a grid walk and a segment test per shape. `CastRay`, the grid's segment walk,
the segment casts and the stale-index flag went with it, and the package-against-package cast test with them; the
contained body's identity is read off the grids a step lays instead. Retired: `SOL-7`, `SOL-19`.

## 2026-10-11 — the managed step trimmed to the bit, and what was measured and left

Measured with `--bench age` on Odesa (692 cars, 784 walkers), a baseline build against the change, interleaved, with each
part of the managed world's step timed on its own; the digests are the same to the bit at 60, 180 and 300 s.

- **A pose read off a body takes its direction from the body** (`ManagedWorld.PoseOf`, `CarPose`'s forward): the
  solver's rotation is `Shape.Rotation` of the heading, which is `Heading.Unit` itself, so no `sincos` is taken again.
- **The integration leaves a rotation and a bounds alone where the step left their bits alone**
  (`IntegrateAndCountTheRest`) — compared as bits, so −0 to +0 is turned again — and counts each body's rest on the same
  pass; the damping empties the push on its pass (`DampAndEmptyThePush`). The integration went 6.4 → 4.6 µs a tick.
- **Each entry's bounds filed beside it in its cell, and a body's layer, mask and channels packed in one word, were
  tried and taken out**: the broad phase read 26.5 → 27.3 µs a tick and laying the indexes 12.9 → 14.4, and the packed
  word alone 26.1 → 26.7. Odesa's three
  grids are 375 × 279 cells or so each, about 1.9 MB with their room, and stay in the cache; what the broad phase pays
  is the entries it walks and not where it reads them from.
- **The three grids keep a dense table each**: the cell lookups one shared table would save are 0.4 % of the probe's
  samples against the broad phase's 3.9 %, and a shared dense table is as many cells, so it saves no memory on Odesa.
  A hashed one does, and needs each body filed once a bucket so a pair is not found twice (tech debt).
- **The moving index is laid whole every step**: laying all the indexes takes 12.6 µs a tick, and a re-file of only
  the bodies that changed cells would keep a cell's entries in order under insertion and removal.
- **The motion into the step is read for every body**, frozen or not: both rosters' reading is 1.2 µs a tick, and
  skipping the frozen would need a stamp a body read back at every touch.

## 2026-10-11 — the device's step costs what moves, not the town

**A step on the device is sized by the moving bodies and what they meet, and a frozen body owns no pairs** (SOL-37),
so OdesaOsm's 80 013 moving bodies among 401 057 pieces of furniture lay and step. Before, the moving grid was the
town's window of 3.9 million cells, filled and scanned whole every step, and every buffer of the contacts was the
candidates' size — 48 a moving body, 3.8 million — with mapped read-backs as big, for a dozen contacts a step.

- **The moving grid is hashed** (`kernels/step.glsl`'s `slotOf`): its window's cells are numbered as before (SIM-8) and
  filed in as many buckets as the moving bodies can cover cells, to a power of two, each entry with the cell it is for.
  The fill counts each bucket back down as it files into it, so the counts are nought again for the next step and
  nothing is cleared between steps. The furniture's grid keeps the window cell for cell, filed once.
- **The contacts have room of their own**, 8 a moving body against the candidates' 16 — a pair whose boxes meet is
  seldom one that touches; OdesaOsm came to 3 614 candidates and 22 contacts at most. The met flags are scanned only as
  far as the candidates found, dispatched off the candidates' scan (`PrefixScan.RecordIndirect`), and nothing is
  cleared over the room. Each room is overflow-checked, and the contacts' read-backs are made only by a step held to the
  CPU's that asks for them.
- **No scan whose length grows with the town is longer than one scan covers**: the buckets and the roster are refused
  at laying, the candidates' room is held to `ScanKernels.MostCounts`, and the furniture's grid to its window.
- **A frozen body owns no pairs, and the pairs are the host's pair for pair.** An awake body takes every moving body
  above it and the furniture; a frozen one takes only the awake bodies above it, walking the moving grid alone; so every
  pair is filed with its lower body and the list stays in the host's order with no sort. A frozen body a manifold meets
  is woken as it is met (`narrow.comp`), and what the woken own is gathered round by round in one workgroup
  (`woken.comp`) — rare, and so not worth a pass of its own — into a side list merged into the contacts as they are
  packed. Every contact then has an owner, so nothing is woken along the contacts afterwards; a touch between two frozen
  bodies is no longer found again each step, so the touches merge the held pairs into what is touching (`touches.comp`),
  as `PhysicsWorld.ReportBegun` does. A frozen body's overlap is left as it froze, as the host's is.

`--bench deviceworld` (RX 9070 XT, 97–99 % busy with another process, so no wall clock is read off it): Test 27.3 → 26.3
dispatches and 28.3 → 27.3 barriers a tick, Odesa 29.6 → 28.6 and 30.6 → 29.6; Odesa laid 65.0 → 52.3 MB device-local
and 5.9 → 2.0 MB mapped, the step's own 7.2 MB. OdesaOsm laid 1 365 MB device-local and 87 MB mapped, the step's own
310 MB, and recorded 33.3 dispatches a tick. Test and Odesa stood 0 m from the host's town at every tick asked, and
`DeviceSolverTests` holds a frozen row woken from both ends to the host's contacts.

## 2026-10-11 — the device's tick is the passes that need a barrier, and nothing beside them

**A device tick records what has to wait for the pass before it, and folds the rest in** (`DeviceWorld.Begin`,
`DeviceSolver.Record`). The tick is latency-bound — a dozen cars or seven hundred, the device idles between passes — so
the figure is dispatches and barriers, not bytes. `--bench deviceworld` (RX 9070 XT, shared with another process):
Test 45.3 → 27.3 dispatches and 48.3 → 28.3 barriers a tick, Odesa 46.3 → 29.6 and 49.3 → 30.6, one submit; the
device's minute of Odesa 18.0 → 14.2 s, managed 10.8. Every answer is the host's to the bit, as before.

- **A pass is fused into another only where it reads nothing that pass writes for another body**: the damping into the
  moving grid's count, the contacts' preparation into their packing, the rest counted afresh into the integration,
  a tow bar's trailing wheels into every wheel's spending (which takes `bodies_wheels.comp`'s place), and the touches'
  first pass beside the freezing, which reads only whether a body was owned.
- **A scan one block covers is one dispatch**, written out of place where the counts are read again (the moving grid,
  the candidates), and the scan of a run a later pass is sized by writes that pass's indirect dispatch — at least one
  workgroup, so a pass that keeps a count of its own (the touches') writes it on a step with no contacts.
- **What only a test reads is behind a flag**: the lists of what woke and froze (`DeviceSolver.ListsWhatSettled`), the
  tyres' rows and scrub (`DeviceTyres` `handsBack`).
- **Nothing is copied back behind the step**: the totals come back from the touches' last pass, and the covers' counts
  and the marks laid from the view's first thread.
- **A car's tyres read the pose the last step's view wrote**, which the signals keep where they move the body, rather
  than a pass reading every car off its body first. Whether a car comes into a step frozen is kept the same way
  (`CarFrozenNext`), apart from whether it came into the last one, which an off-clock view reads.
- **The signals cross into device-local memory** and the events sit at the back of the buffer, so no thread walks the
  commands to find them, and a kind with none runs no pass. The off-clock read of the covers takes its signals in the
  same submit (`WorldLayer.TakeTheSignalsAndReadCovers`). The pull of the tow bars runs only while one is up.
- **A body's filter is one word** (`kernels/filter.glsl`): flags, layer, mask and channels a byte each, which this
  town's four layers fit.

`DeviceWorldTests` holds what the restructuring could have moved and the lockstep does not compare: a car's frozen
flags in every view, and a car put down between steps.

## 2026-10-10 — a touch's closing speed is the device's

**The device says how fast each pair that began touching closed** (`Touch.ClosingMps`, `kernels/touches.comp`), so
phase 5 no longer reads every agent's motion into the tick off the town's fleets. The device keeps that motion itself
(`DeviceSolver._motionIntoTick`): what the last step left once frozen bodies were stilled, plus what the town set
outright since (a body put down, added or contained). A push is left out, as the town's fleets leave
it out. The closing speed is the host's own dot product on those figures, to the bit.

## 2026-10-10 — the device settles what it steps

**With the device stepping the bodies, it also works out what froze, what woke and which pairs began touching**
(`SOL-37`, `SOL-37a`, `SOL-37b`, `SOL-6`, `SOL-37c`; `kernels/settle.comp`, `kernels/touches.comp`,
`DeviceSolver.Settles`), so **no contact is handed back to the host** — only the pairs that began touching, with their
normals and the speed they closed at (`DeviceWorld`, `WorldView.TouchesBegun`), which is all phase 5 reads, and whether
each car is frozen, in its view.

- **Rest is worked over the host step's owners**, and so is the rule that a pair touching settles together, with the
  host's arithmetic on the host's figures. The reach goes in the shape's last word, so nothing is worked out twice.
- **What the unsettling passes along the contacts gets one parallel pass first.** `settle_spread.comp` then passes it on
  in one workgroup until nothing changes, and it does so only when that parallel pass changed something. The two lists,
  where asked for, are written by a prefix scan. A first cut ran everything in one workgroup and cost 0.36 ms a tick on
  OdesaOsm at 40 000 of each, bound by latency. Split up, it costs 0.05.
- **The rest counts are the device's from the first settling step on.** The host lays its own counts whole once, when
  the world is handed over; after that it names only the bodies a signal put down or contained
  (`DeviceSolver.CountAfresh`), which the signals' kernel thaws. A body refiltered is thawed and keeps its count, as
  `PhysicsWorld.PutOnLayer` keeps it: counted afresh, a car thawed by its channels froze half a second after the host's.
- **What was touching is one sorted list of pairs**, searched by the next step and swapped with the one it writes. The
  host lays its own touching pairs at the same times as its rest counts (`PhysicsWorld.TouchingKeys`).
- **The host's table holds no contacts after such a step.** How deep a body is into what it touches comes back in each
  body's row of the world's view (`world_view.comp`), not in the table. `OverlapOf` throws when it was not handed back,
  rather than answer with a stale figure.
- **Held to the host's step** by `DeviceSettlingTests`, which stages what a shoved car park never asks for: a frozen row
  woken from its far end, the unsettling run against the contacts' order, and pairs held through a step that moves
  neither.

On OdesaOsm at 40 000 of each (RX 9070 XT, the whole town then ticked on the device), the device took 0.07 ms of the
bodies' 0.75 ms of kernels to settle and find what began touching. The device's tick was 12.1 ms, and the town drove
the same to the count as with the host doing both: 388 pairs began touching, as they did on the host's. Writing back held at
0.63–0.73 ms: on this map that cost is every car's and walker's pose, not the contacts.

## 2026-10-07 — the bodies' phase on the device

**The phase between the tyres and the solver's step is the device's**, recorded round the step in the world's one
submit (`world/town/kernels/world_tow.comp`'s first pass, over `kernels/impulse.glsl`).

- **The wheels are spent off the tyre pass's own table** (`DeviceTyres.ImpulseTable`), one thread a car in wheel
  order. The lagged acceleration is read off the same impulse table.
- **`DeviceSolver` is split into lay, send and record**, so another submit can carry its step between kernels that
  use its table.
- **The impulses are the host's to the bit**: the same products and sums in the same order. The exception is the edge
  of rest. A kernel flushes subnormal terms, and four wheel turns below the least normal float, summed on the host to
  cancel a 1e-38 yaw, are dropped one by one on the device. So that edge is held within the flushed terms of nought.
- **`Vector2.Lerp` is fused on the host** (.NET 10), so the acceleration's lag is an `fma` on the device.

On OdesaOsm (10 013 cars, 10 000 walkers; RX 9070 XT, the device in colours) the host's phase was 3.28 ms a tick, of
which the solver's step was 3.02, and the device's kernels 0.41: impulses 0.008, step 0.394, read-back 0.005. Over
4.2 million bodies no impulse differed. Over 100 130 car-ticks no position differed; the most any velocity differed
was 1.3e-7 m/s, heading 1.2e-7 rad, direction 2.8e-7 and acceleration 1.1e-6 m/s².

## 2026-10-06 — the step on the device

**The solver's step is the device's** (root log, 2026-10-11): `kernels/` holds it pass for pass, and
`app/devicetick/DeviceSolver` lays the table and records it, held to the CPU's step from the same table.

- **The grids are counted, scanned and filled**; an owner sorts its own candidates, so the order a cell was filled in
  is never read. **A moving pair is filed with the lower body**, so the contacts come out by (first, second) with no
  global sort — and they are the CPU's to the pair and the order (`DeviceSolverTests`).
- **The contacts are solved in colours**: no two of one colour share a moving body, so a colour runs at once, and the
  colours run in turn inside one workgroup. **A contact's rank in the colouring is a bijection of its number**, not the
  number: numbered by their bodies, a queue's contacts are a chain the lowest-number rule colours one a round.
- **The answer is the CPU's but for the order.** Run in contact order on one thread the device lands a crowded yard
  within a tenth of a millimetre of the CPU; in colours it is a Gauss–Seidel of its own and lands near.
- **`precise` on everything the CPU also computes**, or a fused multiply-add moves the last bit.

On OdesaOsm (14 250 moving bodies, 401 057 pieces of furniture, a dozen contacts a step) the CPU's step was 2.58 ms a
tick and the device's 0.39 ms, of which the moving grid was 0.17.

## 2026-10-05 — a body is on the channels of the ways under it

**The owner asked for it simpler**: a channel mask on every way, a body on the ones it stands over, and a pair
meeting where the masks share a bit — so a car on a bridge misses the car under it and one at a bridgehead meets
both (PHY-1a). It replaced a layer of its own for a car on a bridge, entered at the start of a bridge's lane and
left at the start of the next one, which had a car on the junction a bridge lands at on one level while its body
was on both.

- **A mask beside the layer and not more layers.** A layer is what a body is and a channel is where it is; the
  product of the two in one mask would be a bit per kind per level, and the broad phase pays one more `&` instead.
- **Read with the ways under it and never worked out again**: the ways under a car's wheels are already read for
  its claims (TER-4c.2), so its channels are what those ways are on, taken as they are read. The claims and the
  collisions are one reading of the ground.
- **Read on its own channels**, which is what keeps a car on a bridge off the road under it, and **kept over
  none**. So a body only changes level through a way on both, and one put down somewhere new is put on the
  channels of whatever put it there — a wreck set down takes its truck's.
- **A connector is on both its lanes' channels**, and a bridgehead is where they meet. Every one on Odesa's survey
  is 2.5 m long or more, so each of a car's axles is read standing on one on its way off a bridge — the covers are
  read every step, and an axle takes longer than one to cross 2.5 m.
- One channel per level and two levels: a bridge over a bridge is on the one above's, which is rarer than a
  third channel is cheap, and is named as a gap.

## 2026-10-02 — a body at rest is frozen out of the step

The owner asked for it. Profiled on O10 (6 120 cars, 6 240 walkers on the ground), the step was a third of the
CPU and the solve none of it: `FindContacts` alone was a quarter, and over half of that was owners walking
static cells — a parked car asking every tick for the trees round it. A frozen body is no owner, so what it
saves is the broad phase, and `SOL-37` took the exclusion row's "sleeping" off.

- **Per body and not per cell.** The broad phase is walked by owner, so nothing walks cells for a cell tag to
  skip; a moving body has to meet the frozen bodies in its cells to wake them; and a main cell holds a bay and
  the lane beside it, so one walker past a car park would unfreeze the lot.
- **Rest is read off the motion, never off the impulses**: a parked car's tyres correct a residue every tick, and
  a count reset by impulses never let one freeze.
- **The narrow phase takes a pair lower index first and the contacts are solved in pair order**, whichever end
  of a pair the step reached first. A town frozen only at exact rest (`RestSpeedMps` 0) gave the age probe's
  digests to the bit on Odesa and O10, which is the evidence that a frozen body is left out exactly.
- **The shipped rest speed gave the same digests too**: a standing car's tyres hold it at exactly zero
  (`TyreModel.HoldStill`), so nothing in either town rests between zero and the figure. The figure is there for
  whatever does — and a town that never needs it is the same town frozen or not.
- **Nothing frozen is walked by a step.** The awake roster is kept rather than listed — what wakes joins it at the
  next step, what freezes leaves it at the end of this one — and the frozen bodies are filed in an index of their
  own, laid again only once the bodies frozen since outnumber an eighth of it. The moving index the step lays is
  the awake and those few. On O10 the solver's step went from 1.9–2.2 ms to 1.5–2.0 ms a tick with this, the
  digests unchanged.
- **An entry of the frozen index is true while its body stands where it was filed** (`BodyFlags.FiledFrozen`), so
  a body woken and not yet moved is still found there, and the step that moves it lets it go.

Two savings came with it and change nothing a body does: the moving index is laid once a step and not twice,
and a body met in several cells is kept in the first one it shares with the query (`CellGrid.FirstShared`)
rather than marked as seen in a table the size of the town.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- `PHY-5b`: a casualty is moved by being taken aboard at an ambulance's standoff (`AMB-10`), not dragged by
  a stretcher crew.

## 2026-09-07 — how the solver is written is the project's rule, not the solver's

`SOL-27`…`SOL-34` named structure of arrays, spans, `Vector2`, one thread and no LINQ — which is
[CLAUDE.md](../../../../CLAUDE.md)'s second rule and what [goals.md](../../../../docs/goals.md) says the
requirements deliberately leave open, so this slice held the only second copy of the engineering rules and
one that contradicted them. A technique stated as a requirement is also a technique nothing may be
measured against, where `SOL-20` and `SOL-22` state relations and have a gate. Two more are retired for
pointing at `PHY-2` and `PHY-4` a second time, and four for refining `SOL-22` with nothing measuring them.
Retired: `SOL-16`, `SOL-18`, `SOL-23`…`SOL-26`, `SOL-27`…`SOL-34`.

## 2026-08-28 — a casualty stops being something to push around

`PHY-5` left a body in the road to be shoved down the street by everything near it, including the
ambulance sent to fetch it. The impact is the interesting part and everything after it was noise, so
`PHY-5b` cuts it off there: the body goes onto a layer nothing scans on the same tick. A layer and not a
flag, because the broad phase already reads the mask and a `Wounded` bit would be a branch per candidate
pair in the hottest loop. Statics are kept in the row — a body sliding through a wall is what `SIM-1`
exists to stop — and the road's claims were left alone.

## 2026-08-26 — a wreck puts its driver on the road

`PHY-6` stopped saying "unaffected": a wrecked car's driver is placed beside their
door as a casualty rather than sent through `PHY-7a`, which waits for clear ground and would leave a
driver inside a wreck no ambulance can reach (`AMB-7`).

## 2026-08-26 — the circle stopped being a shape

`SOL-1` said two shapes and says one: a disc is a rounded box with no core, so `ShapeKind` is gone. The
general path answers every pair and the two closed forms are kept because they cover most of a town's narrow
phase; `TheDiscShortcutsAgreeWithTheGeneralShape` holds them together over forty thousand pairs.

## 2026-08-26 — the box got a corner radius, and the town did not get polygons

A square footprint corner reaches 0.21–0.43 m past the bodywork, which is a car stopped by a car it
visibly is not touching. Convex hulls are already authored and were refused: twenty axes against twenty
projections where a box pair is four against eight, on a solver step already 0.277 ms of a 1.768 ms tick,
and they would have taken `SOL-1` and `OBJ-2` with them. A radius costs nothing and takes most of it. Two
things had to be right: the face-clip choice is the *cores'*, or pairs overlapping by a hand's depth come
back as missed; and the separating axis can only rule a pair out, so the margin is enforced again on the
real distance.

## 2026-08-19 — the solver stopped being a package

Box2D.NET was defensible — pure C#, no native asset — and cost more than it saved: a step allocated
several hundred bytes, against a `SOL-20` that cannot be kept *nearly*; the port turned v3's
data-oriented arrays into classes, which is the exact shape of C# this project exists to stop being
confused with; and it ran a general soft-step solver where the town wanted a bespoke one. This slice's own
is five times cheaper a step and allocates nothing. Box2D.NET is still referenced by the unit suite alone,
as the independent implementation the manifolds are checked against — it found a reference-face disagreement neither implementation would have found alone.

## Undated — the overlap is pushed out on an accumulator that is thrown away

`SOL-12`'s positional term is kept on a second accumulator that is discarded rather than folded into the
body's real velocity, because a correction folded in is energy the collision never had: a resting pair
breathes, a queue jitters, and the damage arbiter reads a closing speed nothing caused. Carrying no
momentum is also why the position solve converges in far fewer iterations.
