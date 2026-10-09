# Physics — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md) and
[solver.md](solver.md).

## 2026-10-07 — the bodies' phase in the town's place

**With a device set on the town, phase 4 is the device's**: `TownWorld.StepBodies` hands it to `ITownDevice.StepBodies`,
which `app/devicetick/DeviceTownBodies` implements in one submit and one wait, and the town no longer steps its
`PhysicsWorld`. The town keeps what slides with nothing driving it (a casualty, a wreck's wheels, a towed car's trailer
pair), the motion into the tick that phase 5 judges, and its timing marks; phase 5 reads `BeganTouchingThisStep` as
before. `DeviceTown`, the town's device, steps its bodies this way.

- **The impulses spent are the tick's where the device ticked, the town's everywhere else.** The wheels are the tyre
  pass's for the cars the device ticked and the town's own for the rest, written over the pass's rows. A walker's
  impulse is the walkers' tick's row, but where the town's own differs from what the tick handed back — a casualty's
  slide, a walker under a hand — it is written over that row. A tick held runs nothing on the device, and then every
  impulse is the town's.

- **Only what the town changed is sent.** The table remembers the rows the town changes (`PhysicsWorld.Changes`): a
  body added, put in or out of a container, moved to another layer or channel, reweighed, pushed, or frozen. They go as
  one dense run the device reads straight from mapped memory and writes with a kernel (`kernels/laid_bodies.comp`). The
  first step, and any after the town has stepped itself, lays the moving rows whole. **Every moving body is an owner**,
  and `pairs.comp` passes over one out of the world, so a walker boarding sends one row and no roster.
- **Freezing stays the table's** (`SOL-37`). The device freezes nothing. A body frozen on the host is sent at exactly
  no motion, and `integrate.comp` now leaves a body whose heading the step did not change with the direction it was
  laid with, so it stands on the device to the bit. From what comes back the table works out the owners its own step
  would have had: the awake roster, every frozen body the device moved, and every frozen body touching one of those, and
  so on (`SOL-37a`). Then rest, freezing and held touches are its own code (`SOL-37b`, `SOL-37c`). The device asks every
  pair, so a contact neither of whose bodies the table would have owned is dropped.
- **Handed back**: every car's row off its body (one copy), every walker in the world off its own (a kernel writing
  host memory, only for bodies in the world), and the contacts, as many as were found (`kernels/contacts_taken.comp`).
  What began touching is the table's own merge over those (`SOL-6`). A body that froze has its motion stilled in the
  fleets too, as the host's phase reads them after the freeze.
- **The indexes are laid when a picture or a ray first asks**, not every step: nothing in a device's step reads them.
  That was 0.74 ms of the 0.9 the hand-back cost on OdesaOsm.
- **Which body each car and walker is is laid through `ResidentRows.Lay`**, which keeps what it laid host-side. Reading
  the mapped rows back to see what changed cost 3 ms a tick on OdesaOsm.
- **Held to the host tick for tick** (`DeviceTownBodiesTests`, both solving in contact order): a touring city's cars for
  a second, and a car park shoved into itself — walkers, cars, every pair that began touching, every body's freezing
  and what broke and who was hurt — for three seconds, all within the solver's own agreement and the discrete outcomes
  exact; the half minute after is asked only that no body stays inside another (`PHY-1`) and nobody is hurt whom the host
  left whole. The steady state allocates nothing. With `DeviceTown` ticking the cars and
  walkers too, the shoved car park came out as the host's in a one-off run: 6 wrecks, 10 hurt, 33 pairs began touching,
  111 bodies frozen at the end, casualties at rest; no test holds that, since the cars' own tick parts the two towns.

On OdesaOsm (10 013 cars, 10 000 walkers, 431 070 bodies; RX 9070 XT), the device stepping the bodies alone, with every
wheel and walker impulse the town's and so sent: the town's own phase 4 took a median of 2.48 ms a tick (solver 2.39
mean), and handed to the device 1.92. Laying took 0.08 ms on the host and writing back 0.19. Submit to answer was 1.62,
of which the device spent 0.45 taking in 771 KiB (626 KiB of it the town's wheels, which a device running its own tyre
pass does not send), 0.34 on the kernels and 0.32 handing back 543 KiB. About 0.6 is the device waking from the
tick's idle: an empty submit is 0.03 ms back to back and 0.61 after a sixtieth of a second's sleep. The host's own
figure moved between 2.5 and 3.3 ms from run to run on this machine. The first step, laid whole, was 18 ms and 4 MiB.

With `DeviceTown` (`--bench devicetown`, OdesaOsm, Release, this machine under another run's load) the bodies' column
went from 3.12 ms a tick to 1.72–1.81, against 3.35–3.67 on the host alone, and the device's whole tick from 19.3 to
16.6–17.0 (host alone 25.7–27.3). The phase lays 0.08 ms, waits 1.4–1.5 of which the kernels are 0.40, and writes back
0.20. The town drove the same: 12.85 m/s against the host's 12.86, 79 cars off their lines each, 695 hard brakings
against 714 (691 before the bodies moved), 16 pairs began touching against 15 (16 before).

## 2026-10-07 — the bodies' phase on the device

**The phase between the tyres and the next tick's car tick moves to the device**, in shadow beside
`TownWorld.StepBodies`: `app/devicetick/DeviceBodyPhase` records three kernels round the solver's step in one submit
(`world/town/kernels/bodies_*.comp`, over `kernels/impulse.glsl`), and `--bench devicebodies` compares the two.

- **The wheels are spent off the tyre pass's own table** (`DeviceTyres.ImpulseTable`), one thread a car in wheel
  order; then what the host lays, one row a body; then the step; then every car is read back off its body into the
  tyre pass's poses, which is what the next car tick reads. The lagged acceleration is read off the same impulse table.
- **`DeviceSolver` is split into lay, send and record**, so another submit can carry its step between kernels that
  use its table. `Step` is the three in its own submit, as before.
- **Still laid by the host**: which body each car is and whether it rested parked, sent only where it changed; every
  walker's central impulse and both ends of a tow bar; and the two trailer wheels of a car on a tow, written over the
  tyre pass's rows. The walkers' read-back and the velocities kept for the arbiter stay on the host.
- **The impulses are the host's to the bit** (`DeviceBodyPhaseTests`, City's driven cars and Built's walkers and
  parked cars): the same products and sums in the same order. The exception is the edge of rest. A kernel flushes
  subnormal terms, and four wheel turns below the least normal float, summed on the host to cancel a 1e-38 yaw, are
  dropped one by one on the device. So that edge is held within the flushed terms of nought.
- **`Vector2.Lerp` is fused on the host** (.NET 10), so the acceleration's lag is an `fma` on the device.
- **The read-back is the town's within the solver's own agreement** when both solve in contact order.

On OdesaOsm (10 013 cars, 10 000 walkers; RX 9070 XT, the device in colours) the host's phase was 3.28 ms a tick, of
which the solver's step was 3.02, and the device's kernels 0.41: impulses 0.008, step 0.394, read-back 0.005. The
shadow's sends were apart: 3 MiB a tick, 1.76 ms on the device and 2.35 ms laying it on the host, and 0.27 ms handing
it back. Over 4.2 million bodies no impulse differed. Over 100 130 car-ticks no position differed; the most any
velocity differed was 1.3e-7 m/s, heading 1.2e-7 rad, direction 2.8e-7 and acceleration 1.1e-6 m/s².

## 2026-10-06 — the step on the device

**The tick moves to the device** (root log), and the solver's step is its first piece: `kernels/` holds it pass for
pass, and `app/devicetick/DeviceSolver` lays the table and records it. It runs in shadow for now — handed the CPU's
table before each CPU step — and `--bench devicestep` compares the two.

- **The grids are counted, scanned and filled**, over one window for the town; an owner sorts its own candidates, so
  the order a cell was filled in is never read. **A moving pair is the lower body's**, as on the CPU, so the contacts
  come out by (owner, other) with no global sort — and they are the CPU's to the pair and the order (`DeviceSolverTests`).
- **The contacts are solved in colours**: no two of one colour share a moving body, so a colour runs at once, and the
  colours run in turn inside one workgroup. **A contact's rank in the colouring is a bijection of its number**, not the
  number: numbered by their bodies, a queue's contacts are a chain the lowest-number rule colours one a round.
- **The answer is the CPU's but for the order.** Run in contact order on one thread the device lands a crowded yard
  within a tenth of a millimetre of the CPU; in colours it is a Gauss–Seidel of its own and lands near.
- **Nothing freezes on the device** (SOL-37): every enabled moving body owns its pairs. What freezing saved was the
  CPU's broad phase, and the pairs only frozen bodies add are solved to nothing.
- **`precise` on everything the CPU also computes**, or a fused multiply-add moves the last bit.

On OdesaOsm (14 250 moving bodies, 401 057 pieces of furniture, a dozen contacts a step) the CPU's step was 2.58 ms a
tick and the device's 0.39 ms, of which the moving grid was 0.17 — the town's window is 3.9 million cells, filled and
scanned whole. The contacts agreed pair for pair, and where only frozen bodies met the device found three more.

## 2026-10-05 — a body is on the channels of the ways under it

**The owner asked for it simpler**: a channel mask on every way, a body on the ones it stands over, and a pair
meeting where the masks share a bit — so a car on a bridge misses the car under it and one at a bridgehead meets
both (PHY-1a). It replaced a layer of its own for a car on a bridge, entered at the start of a bridge's lane and
left at the start of the next one, which had a car on the junction a bridge lands at on one level while its body
was on both.

- **A mask beside the layer and not more layers.** A layer is what a body is and a channel is where it is; the
  product of the two in one mask would be a bit per kind per level, and the broad phase pays one more `&` instead.
- **Read off the laying and never worked out again**: the ways under a car's wheels are already read for its
  claims (TER-4c.2), so its channels are what those ways are on, taken as they are laid. The claims and the
  collisions are one reading of the ground.
- **Read on its own channels**, which is what keeps a car on a bridge off the road under it, and **kept over
  none**. So a body only changes level through a way on both, and one put down somewhere new is put on the
  channels of whatever put it there — a wreck set down takes its truck's.
- **A connector is on both its lanes' channels**, and a bridgehead is where they meet. Every one on Odesa's survey
  is 2.5 m long or more, so each of a car's axles is read standing on one on its way off a bridge — the claims are
  laid every 0.05 s, and an axle takes longer than that to cross 2.5 m below 50 m/s.
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

## 2026-08-26 — the person's tolerance is a distance, and a wreck puts its driver on the road

`PersonShakeKj` and `PersonFatalKj` were kilojoules nobody could picture; what is authored now is
`SlideToCasualtyM` and the energy is the work of sliding a body that far on its own grip. The reduced mass
makes it honest rather than exact — a car is seventeen times a person, not infinite — and that is stated
on the derived figure. `PHY-6` stopped saying "unaffected": a wrecked car's driver is placed beside their
door as a casualty rather than sent through `PHY-7a`, which waits for clear ground and would leave a
driver inside a wreck no ambulance can reach (`AMB-7`).

## 2026-08-26 — the circle stopped being a shape

`SOL-1` said two shapes and says one: a disc is a rounded box with no core, so `ShapeKind` is gone. The
general path answers every pair and the two closed forms are kept because they cover most of a town's narrow
phase; `TheDiscShortcutsAgreeWithTheGeneralShape` holds them together over forty thousand pairs.

## 2026-08-26 — a car is collided as its picture, not as the rectangle the picture was drawn in

A footprint is the rectangle art is drawn *into*, so its width is whatever reaches furthest — the police
car's wing mirrors, a fifth of a metre outside the panels. The shape is now the largest rounded box
*within* the silhouette, measured off the alpha and authored as `collisionM`, which by construction never
stands outside the bodywork. That retires `hullM` as a source, since it traces the mirrors too. Two
authoring mistakes both produced plausible answers — an ellipse in metres from eroding a squashed picture,
and a notch closing up at a sixth resolution — and the gate caught both, which is the argument for asking
the art rather than another figure in the same file.

## 2026-08-26 — the box got a corner radius, and the town did not get polygons

A square footprint corner reaches 0.21–0.43 m past the bodywork, which is a car stopped by a car it
visibly is not touching. Convex hulls are already authored and were refused: twenty axes against twenty
projections where a box pair is four against eight, on a solver step already 0.277 ms of a 1.768 ms tick,
and they would have taken `SOL-1` and `OBJ-2` with them. A radius costs nothing and takes most of it. Two
things had to be right: the face-clip choice is the *cores'*, or pairs overlapping by a hand's depth come
back as missed; and the separating axis can only rule a pair out, so the margin is enforced again on the
real distance.

## 2026-08-21 — going into a container no longer marks the moving index stale

`Contain` rebuilt the whole moving grid mid-phase for a body that had merely had a bit cleared; every reader
already tests the bit, so the stale entry is filtered rather than found. Coming back out still marks it.

## 2026-08-19 — the solver stopped being a package

Box2D.NET was defensible — pure C#, no native asset — and cost more than it saved: a step allocated
several hundred bytes, against a `SOL-20` that cannot be kept *nearly*; the port turned v3's
data-oriented arrays into classes, which is the exact shape of C# this project exists to stop being
confused with; and it ran a general soft-step solver where the town wanted a bespoke one. This slice's own
is five times cheaper a step and allocates nothing. Box2D.NET is still referenced by the unit suite alone,
as the independent implementation the manifolds are checked against — it settled `SOL-19` and found a
reference-face disagreement neither implementation would have found alone.

## Undated — the overlap is pushed out on an accumulator that is thrown away

`SOL-12`'s positional term is kept on a second accumulator that is discarded rather than folded into the
body's real velocity, because a correction folded in is energy the collision never had: a resting pair
breathes, a queue jitters, and the damage arbiter reads a closing speed nothing caused. Carrying no
momentum is also why the position solve converges in far fewer iterations.
