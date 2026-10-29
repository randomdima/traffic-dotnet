# Decision log — roads and junctions

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md) and [claims.md](claims.md); how
a type works is its own XML docs.

## 2026-10-29 — a bar and the lane line stop on one line parallel to the band in front of them

A band is laid square to the walk that placed it (WLK-10), so it is deep along its own axis wherever along the arm
it stands, and its near edge is struck along that axis and never along the lane behind it. **Everything held
behind a band is now struck off that same edge**: a bar stands where its lane crosses the line a setback behind it,
laid parallel to it, and the lane line stops where its run crosses the line a setback and a bar behind it
(`Crossings.BehindM`). Before, the bar was the edge's middle projected onto the lane and laid square to the lane, and
the line was the band's middle projected onto the run less a fixed reach — three frames, which parted by however
far a line stood off the band's middle times the skew between it and the band: a run 1.8 m off a lane where the arm
bent 6° stopped 7 cm past the bar (fixture, road 71), and a bar's gap read 4 cm off its setback. Struck off one
line the two tests that weighed those gaps hold to the arithmetic's rounding. **The lane is still asked which edge
the traffic meets first**, a sign and not a bearing. A bar stood back behind another band's paint keeps its lane's
bearing.

## 2026-10-27 — a node whose movements cross nothing is lanes carried on, and no junction

**The owner asked for a junctionless connection** (TER-5k): "When entering or exiting a road does not require any
regulation or blocking. Main example is intercity road. When lanes and connectors never cross … pure lane continuation,
seamless forks. No stoplines or other junction logic. Lane intersection includes pedestrian or rail." A slip road's node
was a junction short of its light: a zero-depth station filed at each end (`KerbEnds.Cut`) put bars on the intercity and
slip lanes, the lane lines broke there, the slip was ranked a near-side turn under the road's straight, and a car ran
the whole box rule over a twenty-metre join.

- **Worked out, not authored**: every road at the node unwalked, no two movements' lines crossing but two off one lane
  or onto one, and no track crossing any at ground level (`LaneLines.Seamless`). Unwalked rather than "no zebra
  painted", because a walk past a node that forks is cut there (`KerbEnds`) and the stations are read after the lanes.
- **Ranked straight after the lanes are shared**: classified by angle alone a 25° slip is straight on, and two
  straight-ons off one arm are a fork that sends the kerb lane off whole (GEN-68's own entry) — so the lanes are still
  shared as the turns they are and only the kind is written straight afterwards.
- **A merge is the claims' at one rung** — offered three ways (one rung, an acceleration lane beside the kerb lane, the
  slip still giving way), the owner said to implement it, and one rung is what was built: nothing new is taught to the
  cars, and whoever asked first keeps the ground where the two converge. The acceleration lane stays in
  [tech-debt](../../../../docs/tech-debt.md).
- **The paint runs on through to the one arm laid the same** (`CentrelineRuns.Across`), and through none where two are.
- **Measured** (Saltbay): 8 nodes carry lanes on, the 4 that fork or merge being its slips' (the other 4 bridgeheads);
  those carry no bar or arrow, and the other 4 are two-armed; `--bench stuck --seconds 180` gave up no drive leg.

## 2026-10-16 — a car short of a box plans none of one it could not get out of

**What the stuck probe found** (Odesa, 300 s): every ring of cars left standing at the end was two cars at one box.
One stood at the mouth at rest, its plan the room to pull away — a metre or two into the join, cut there by the
body of a car whose nose was already in the box. That car needed the crossing section of the same join whole
(TER-5c.1) and was refused it by the first car's sliver. Neither moved again; legs given up and laid afresh put the
next car in the same place.

**Decided** (TER-4c.1): short of a box, a plan reaches no further than the mouth while any body stands on the car's
own ways from the mouth to its own length and a stand-off past the far side — read off the bodies on its own ways
(`Following.TheWayOutIsClear`), unless the car can no longer stop short of the mouth. **Measured**: Odesa's cars
still at the end 11 → 0, rings 2 → 0, reroutes 47 → 12, bays parked 353 → 364; exam cards 82, 85 and 86 pass (a car stood in the box
and then on the zebra with the walker on it), and 70 fails (`docs/tech-debt.md`).

**Then, over 20 minutes** (`--bench stuck --seconds 1200`): the car held at the mouth still asked its slack a few metres
into the box, at its movement's rung, and a car already inside crossing those metres was refused there while its body
stood on the first car's way out. **Decided**: at rest there, a car asks no slack past the mouth and holds its plan at
the bottom rung (`ClaimPriority.Backing`); still coming up, it keeps both — without them cards 30 and 52 failed, the
right of way through a box going to the turn across it while the straight car closed on the car ahead. **Measured**
with the parking slice's change of the day: Odesa's cars ever standing a minute 32 → 9, reroutes 211 → 31; River's
68 → 5 and 355 → 19; card 60 passes.

## 2026-10-15 — the marks are worked out by the device where a world keeps its own, in floats

**The owner asked for the open's remaining speed-ups, the marks on the device among them, moving them.** Where a world
files its own atlas, the host lays the marks' pieces and their cells as `RibbonMarks.Of` does (`RibbonMarks.Filing`), and
`kernels/ribbon_marks.comp` weighs every pair a thread an entry of the cells, `RibbonMarks.Weigh` line for line; the host
merges and files what it found (`RibbonMarks.OfRows`). OdesaOsm's marks 1.4 s of the page's open from 6.3, and its open
40–43 s from 48.6.

- **In floats, each pair in its own frame**: the start of the piece whose ground is asked about, and an arc's circle asked
  through that start rather than its centre, which a road's gentlest bend stands a thousand kilometres off — so the ends
  stand within a millimetre of the host's doubles (`MarksKernelTests`).
- **But for touches**: two ways leaving one place touch along their trimmed ends' lines, and whether that is ground in
  common falls to the rounding, the host's and the device's either side of it — a section then begins at the trimmed end
  or a lattice step on. 2 % of OdesaOsm's sections; a slack on the device's ends was tried and made it 7 %.
- **The managed world keeps the host's**, to the bit, and a device's world its device's: a town laid both ways differs at
  those touches. A test that holds the two in lockstep may lay its device world with the host's marks
  (`DeviceWorldMaker(marksOnTheDevice: false)`).
- **A page waits on the device a stage at a time** (`TownWorld.Groundwork`), a frame each.

## 2026-10-15 — the atlas is filed by the device where a world keeps its own

**For a world that keeps its own atlas, the host finds no point of it** (`RibbonAtlas.Filing`, `DeviceAtlas.Filing.cs`,
root log): the host lays every piece of every filed way with the box its search reads, and the device finds, counts,
files and orders the points (`kernels/atlas_points.glsl`, `atlas_file.comp`, `atlas_starts.comp`, `atlas_sort.comp`) —
the host's search line for line, so the tables are the host's to the bit wherever the device rounds as the host does
(`AtlasKernelTests`; OdesaOsm's device digest is the host's).

- **A point's bearing about an arc's centre is `Trig.Atan2` on the host as well**, where it was the library's: the
  device's is the same code, and the library's is glibc's. The atlas's digest moved once (OdesaOsm 44b18a6c… →
  15e107f3…, three entries fewer of 116 million); the marks did not.
- **The host lays the slots** (`RibbonAtlas.SlotsOf`) off the cells' counts, which come back between the device's two
  batches: the one place the filing waits on an answer, and what sizes the tables.
- **A row of a piece's box is read over runs worked out in floats, wider than the host's** (`filingRunsAcross`): the
  host's are in doubles and never narrower than the band as `Foot` reads it, so a column past them is one no test
  keeps. A way's pieces settle a point two read on the device by asking each other (`filingKept`), as `PointsOf` does by
  sorting.
- **A run of points counts its entries in a byte**: more than 255 ways at two points is refused, where the most a
  traced city has is 32 at one.
- **The bands are gone**: a host that finds no point holds no band of them.

**The marks hold less at once**, the same marks to the bit (`--bench load`'s marks digest): their pieces laid at their
size rather than through a list copied out, their cells filed without a table of cursors, the threads' pairs merged into
the first's rather than a fresh set, and the sections filed into one table counted first rather than a list a way —
340 MB off the marks' peak on OdesaOsm.

## 2026-10-15 — the atlas files off a second walk of every way, after the marks

**Making a traced city open in a page** (root log, same date) found the atlas's lay to be the open's peak, holding at
once every point between being found and being filed (eight bytes an entry), the entries themselves, and the marks'
own working set. A page keeps the most it was ever given.

- **Every way's points are found twice** (`RibbonAtlas.Filed`): once to count each cell's, once to file them straight
  into their cells. The blocks they waited in are gone — a gigabyte on OdesaOsm — for a second run of the geometry,
  about 7 CPU s there and nothing on the wall's clock.
- **The marks are laid before the filing** (`RibbonAtlas.Lay`), and the town collects between the two
  (`TownWorld.CollectTheLay`): each working set is about the entries' size, and laid the other way round both stood at
  once.
- **A way's readings are sorted by their place alone** (`RibbonAtlas.PointsOf`), a 64-bit key a reading, and a point's
  kept reading is chosen in the scan after: through a comparison of the whole reading the sort was a twentieth of a
  page's open. **Two readings tied on every figure are settled by the nearer the way's start**, where the sort's own
  order settled them before; on OdesaOsm some were, and the atlas's digest moved (`--bench load`).
- **Both sorts are `KeyedSort`'s and not the framework's**: a page's runtime interprets the framework's sort over a
  pair of value types.

Measured with `qq prof --bench load --memory`, OdesaOsm (the world's peak is the resident set's most inside the stage;
the CPU every thread's, so it carries the door and lane fixes of the root log's entry too):

| | before | after |
|---|---|---|
| atlas | 2.8 s | 2.8 s |
| the world's peak | 3 243 MiB | 1 959 MiB |
| the open's CPU | 88 s | 75 s |

## 2026-10-13 — a line is laid within an arc budget, not room for the town's curviest lane twelve times

**Each car had room for 343 arcs on OdesaOsm** (`LineAssembler.ArcsFor`: twelve of its longest lanes and the joins
between), and its lines held a median of 8 and at most 57 over 400 layings (`--bench room`): 446 MB of arcs and
corner entries for the fleet. Now a line has room for `LineAssembler.MostArcs` (96), or two of the town's longest
lanes where that is more, and `TownWorld.LayLine` asks the route for another lane only while the longest would still
fit — asking takes it off the route — so a line ends short only past 67 arcs, and the car asks for more as it runs
out, as it does past `MostLanes`. About 125 MB; O10's digests the same.

## 2026-10-13 — a way's runs are threaded through one table of slots, not packed apart

**Tried at the owner's asking, with a second idea of theirs and one more.** Each claim is a slot in one table in the
order laid, naming the next of its run by near edge; a way's row holds the first of each run. A body is threaded in
as it is laid and kept one by one — what overlaps is one claim only where an instrument reads it — and a main or a
secondary claim as it is taken. That spares the passes that counted each way's room, placed its bodies, sorted and
merged them and gave its plans room, and a question reads a claim's holder off the claim and not off a second table.
On `--bench room` against the packed runs, run back to back on all cores and exact on the age probe's digests,
OdesaOsm laid in 29.1 ms against 32.3 and O10 in 3.85 against 4.36.

**Why it does not lose to pointer-chasing**: a run is a claim or two long. Counted on OdesaOsm, a question walked
0.2 to 2.6 slots on average, and more than eight in about 1 % of them.

**Tried and refused**:

- *Ten-metre sections*, each naming where a question about it begins: 30.6 ms against 29.0. Indexing them cost
  1.2 ms a laying, and the extra read cost more than the one or two slots it skips.
- *Each way's row indexed by the way itself*, marked with the laying instead of a row given at first touch: 31.4 ms
  against 29.2. The rows of a laying's 94 000 ways packed together stay in cache; a row for each of the town's
  1.1 million ways does not.
- *Fetching ahead* (`Sse.Prefetch0`) the rows, hold and first claims of the wish eight and four ahead in turn order:
  answering took 8.0 ms against 6.3, and the rows and hold alone 6.7. Finding the claims to fetch is itself a read
  that waits, and the core already overlaps what it can.
- *A way's row and where its marks begin side by side in one table*, so a piece asked reads one line for both:
  28.6 ms against 28.6.
- *`CornerLimits.RestToM` walked from the car's own cursor* (`CarFleet.LineCursor`, exact) rather than the line's
  head: cars' plans 9.34 and 9.54 ms against 9.68 and 9.41. What it waits on is the first read of the car's line, not
  the arcs walked.

**A wish and a slot are written field by field where they lie** (`LaneOccupancy.Add`, `NewSlot`). Built on the stack
and copied in, a wish was 55 % of `AskUp`'s own time and 88 % of `Add`'s — two overlapping 16-byte copies of a
record just written. 29.2 → 28.6 ms on OdesaOsm, exact.

**Secondary claims are kept as taken and read merged, as bodies are**: the owner's merge per rung (below) is what a
reader sees — `FirstSecondary`, `IsTakenByOthers` and the instruments' views — and no pass merges them after
answering, so no claim names its holders in a second table and a slot is 16 bytes. Answering 6.3 → 5.1 ms on
OdesaOsm, the laying 28.8 → 27.4, exact; the car tick, which reads the lights' holds, the same.

**A piece is asked with one read of its way's row and its hold, and a mark's cut is worked out only if it is
refused** (`LaneOccupancy.AskUp`, `CutLineM`): a mark's secondary claim carries its main piece's line and the
decimetre the mark begins at, and nothing is divided per mark asked. 28.05 → 27.45 ms on OdesaOsm, exact, first with a
metre kept on each mark — which cost 5.4 MB of marks, and is gone. **A driver's own body is read off its own stretches**
(`OwnBodyAt`): one stretch of it on the way stands over the decimetre or not, so the way's run — a queue, where it is
long — is walked only for two. Cars' plans 9.8 → 9.1 ms, the cars' bodies 0.12 dearer for the index, exact; a
walker, two stretches on a way more often than not, still walks the run, and indexing walkers too lost 0.6 ms on
their walks. With no own-body check at all asking was 1.7 ms cheaper, which is the bound on this.

**The tables are laid for what a laying uses** (`SlotsPerHolder` and the rest): about twice OdesaOsm's busiest
laying, against four to eight times before — 139 MB of tables to 52, the marks' 16 MB and the 4.4 MB row of every
way the most of it. A wish names its way only through its row (24 bytes, was 28), and **what answering reads of a
hold is apart from what it came to** (`HoldState`, 8 bytes): read off the whole row, the hold was a fifth of
`Take`'s time. Answering 8.11 → 7.77 ms on a loaded machine, two pairs of four; exact.

**Tried and refused**:

- *A crossed way given its row only when its secondary claim is taken*, not when asked: asking 0.6 ms cheaper,
  answering 1.1 ms dearer, the row then read cold in `Take`.
- *Fewer secondary claims*: none to drop. Of a laying's 117 000 a car asks on OdesaOsm, 58 000 are where connectors
  cross in a box and 45 000 the zebras its lanes cross (3.9 m of lane each); the rest are the same two kinds of
  ground (`--bench room`, by way kinds).
- *Each turn's pieces linked as they are asked*, instead of sorted by turn before answering: answering 8.52 ms
  against 8.06. Following the links serialises the reads the sorted order lets the core run ahead on.

## 2026-10-12 — the claims are runs of decimetres, taken in one pass strongest first, and say nothing of who

**The owner asked for the claims to be simpler and exact**: a claim's edges as `ushort` decimetres from the way's
start, no floats; claims of a run ordered with no intersections, one ending on the decimetre the next begins;
overlapping secondary claims merged; a claim storing its holders and its rung and nothing else. Their rulings, in
order: of one rung, who gets the ground does not matter; a claim may hold several holders; what a body is doing is
not the claim's to carry; **actors never read who laid a claim** — a claim is what makes others act, not its actor —
and every feature that did is dropped and tracked; edges round up; every reader is ported, with no branch kept for an
edge case.

- **Three runs a way** (`LaneOccupancy`): bodies, one claim where they overlap with every one of them a holder; main
  claims, one holder each and never overlapping; secondary claims, merged per rung. The town's longest way has to fit
  65 535 dm (6 553.5 m); the constructor refuses a town that does not.
- **One pass**: bodies laid and merged first; then every piece every hold asked, sorted stably by rung and taken in
  that order, each against what is down — a main piece refused at the first body, main claim or secondary claim of
  another holder, a secondary claim refused whole by a main claim. Whatever is down is at least as strong, so
  nothing taken is freed, nothing is answered twice and no ring can form. The passes, the settling bound
  (`MostSettlingPasses`) and the comparison (`Beats`) all went.
- **A holder's own claims never refuse it**, compared by holder inside the claims and never handed to an actor: a
  car's pass lies over its own plan, and two main claims of one holder over one stretch are merged into one. **A piece
  begins past its holder's own body** where that stands over its start.
- **A pass, a step across and a manoeuvre are plans of their own** (TER-4c.6, the owner's point 7): asked at the
  holder's rung, at p1 once begun, kept where all of it was had. **Before it begins one is a hold had whole**:
  checked as a whole and taken whole or not at all, after every plan of its rung — so a withdrawn pass keeps no
  ground from anybody, and it never outranks oncoming traffic asked in the same laying, whichever was asked first.
  The claims half of the step's and the manoeuvre's own look before asking went with it, being a second gate on
  what the laying already refuses. **Once begun the holder's plan carries on in the same hold**, and each piece of
  ground swept off its line names where the holder stops if it is refused (`AskSwept`), so what the hold came to is
  the grant: the second search for a body inside it (`FirstBodyIn`, the `Asks…` of each manoeuvre, a refusal made up
  by `Cut`) went.
- **Every question about one body reads that body** (`FirstBodySeen`): a merged body run was the answer to "is it
  still there", and a car queuing onto what a pass got past made it gone. The merged runs are what is drawn.
- **Rounding up needs a grain**: 0.3 m × 10 is 3.0000001 in floats, so a metre read off a claim and asked again
  rounded to the decimetre after its own; `DmOf` takes 0.001 dm off before rounding up.

**Dropped, because each read what a body was doing or who it was** (`docs/tech-debt.md` brings them back off a reading
of the bodies themselves): deciding a pass round what cut a grant (CAR-46, `Overtaking.MayGetPastWhatCutIt`), and so
backing up for one (CAR-50) and a call passing at-rest or slow traffic (AMB-4.4); moving across to get past what holds a
car up (CAR-53, `Switching.IsHeldUp`); a walker's sidestep (PER-28); the room kept to step out behind a body going
nowhere (`KeptOffM`); the backing that took the plan of somebody queued behind it (`IsQueuedBehind`); a light's hold
lying over a body only standing on its way (it now ends at the first body of any kind); the queue / obstruction /
walker / passing kinds of what cut a grant (`HeadwayKind` is now nothing, body, claimed, light); the arrival
tie-break (TER-5g's last tier — of one rung, the first asked keeps it); **and "ground its holder already stands on"**
(TER-5e's second tier), which the plan had not listed. Overtaking, backing up and sidestepping are kept whole but
for the decision, which nothing calls.

**Tried and refused**: *a secondary claim refused by a body standing in its section* restored that tier for a zebra
— a car stops short of paint somebody stands on — and failed 34 exam cards, every box deadlocked: a queued car's
wheel cover reaches a metre into the joins at its mouth, and refused every movement crossing them. *A plan
committed through every mark its own body stands in* fixed card 69 and failed 29, 46, 52, 53 and 96: a car with a
wheel cover a metre into a box outranked the right of way.

**Measured** (fixture towns only): unit, town, device and gate tiers pass — the gates once the tables are laid for
the town's holders at the start (`AllocationGateTests` caught a table growing inside its window). The exam fails 13
cards against 5 before: 44, 79 and 85 now pass; 60, 69, 70, 80, 82, 84, 86, 88, 89, 91 and 108 now fail, 67 and 109
still do. 108 and 109 are a pass no longer decided; the zebra cards are a car stopped behind somebody standing at
the kerb end of the paint, inside the zebra, with no room kept behind them; 60 and 69 are the standing tier.

## 2026-10-07 — a body is on the ways its wheels stand on, and a walker is one wheel

**The owner ruled it: one reading of the ground a laying, and no collider read against the ribbons for a body.**
Once each time the claims are laid a car's four wheels are each read at the lattice point nearest them
(`RibbonAtlas.UnderWheels`); the car is on every way any of them stands on, over the least and most of where its
wheels on that way stand, held a metre either side (`Road.HeldAroundAWheelM`, the owner's figure), and which of them
stand on a way is what its tyres grip on until the next laying (terrain log). A car with no wheel on a way is on the
grass and holds nothing. **A walker is a single wheel at its centre** (`RibbonAtlas.UnderAWheel`), held its radius
either side, and on the way it walks at its place on it; whether it stands on the pavement at all, which decides
whether a walk back onto its way is an action of its own, is read at that same point (`WalkingBack`). Four point look-ups for a car and one for a walker replaced
a read of every lattice point inside the collider, and the recall that kept a walker's last read, and the walk that skipped it for a walker
clear on its own way, went with it.

**A wheel is read at the lattice point nearest it**, so a wheel within the reach (0.35 m) of a band can read the
point across the paint. Asked to prove instead — a way kept only where one of the four points round the wheel lies in
its band and none bounds the wheel outside it — the town braked harder (1 663 hard brakings against 1 093): the atlas
files how far outside a band a point stands and nothing of how far inside, so nothing proves a wheel inside one.

**What no wheel stands on is held by nobody.** A car with its axles on two ways holds each round its own wheels, so
across a lane's end its middle — the wheelbase less two metres and two half steps — is held on neither; one whose
wheels straddle a zebra narrower than its wheelbase, or whose nose is over a junction its front wheels have not
reached, is no body there. A walker 0.1 m short of a kerb is not on the zebra, so a light holding the paint holds it
there, where its disc reaching over the paint had made it a body on the crossing the light gave way to.

**Measured on OdesaOsm** (10 013 cars, 10 000 walkers, the town ticked on the host and on the device, two runs each,
the three builds interleaved): before; the cars and their tyres on their wheels alone; and with the walker as one wheel, the car no
longer asked its ground, and what that left redundant gone (terrain and car logs).

| | before | cars' wheels | all of it |
|---|---|---|---|
| a car's body read, host | 0.406 µs | 0.130 µs | 0.130 µs |
| claims, host, ms a tick | 6.94, 6.95 | 5.57, 5.58 | 5.82, 5.86 |
| whole tick, host, ms | 23.60, 23.76 | 18.66, 18.58 | 15.57, 16.14 |
| whole tick, device, ms | 12.12, 12.08 | 11.48, 11.50 | 11.11, 11.12 |
| device kernels, ms a tick | 1.29 | 0.64 | 0.46 |
| whole tick, host, µs (`--bench town`) | 24 403 | 20 047 | 17 150 |
| hard brakings in 600 ticks | 714 | 1 135 | 1 147 |
| hard brakings (`--bench drive`) | 2 100 | 3 382 | 3 419 |
| pairs began touching in 600 ticks | 15 | 21 | 26 |

**The claims rose a quarter of a millisecond with the walkers** (5.58 → 5.84): in the build before, two walkers'
reads in three were answered from the last reading (`--bench deviceatlas`), and every one is read fresh now. What the
town gained after the cars' wheels is the car's tick no longer asking its ground (car log).

**The braking is the bodies' and not the tyres'**: with the tyres on the ribbons and the bodies still read off the
collider the town braked hard 714 times, as before. A car is put on a lane its collider does not reach and the traffic
on it brakes for it. Exam card 109 (a car on a call gets past a queue at a red) fails since: the car on the call
reaches the box after the queue's head.

## 2026-10-07 — which ways a body stands over is read on the device off the atlas's own tables

**The ribbons each moving body stands on are the world's to read** (root log, 2026-10-11), and every claims laying
lays every body onto the ways under it (TER-4c.2). `kernels/atlas.glsl` is `RibbonAtlas.Read`, `Grow` and `Widened` line for line, over the very tables the
host reads (`RibbonAtlas.Table`, laid by `app/devicetick/DeviceAtlas`), so the answer is the host's and never a second
filing of the ribbons.

- **A box is handed its forward unit vector**, the host's, so no corner is the device's own trigonometry and the whole
  read is compared exactly.
- **A quotient and a root are put right in doubles** (`core/geometry/kernels/rounded.glsl`): RADV divides by a
  reciprocal, and with the device's own `/` and `sqrt` 5 822 of OdesaOsm's 13 242 bodies came out over other
  stretches or other ways — mostly a stretch's tenths turned into metres a last bit apart.

Held to the host over 2 400 boxes and 2 400 discs on the fixture town and 2 400 bodies on a deck over a street, every
channel (`AtlasKernelTests`): the same ways, order, stretches and drops to the bit. On OdesaOsm after 600 ticks
(`--bench deviceatlas`, RX 9070 XT), 10 013 cars and 3 229 walkers with a body took 5.3–6.1 ms on the host, uncached, and
0.73–0.80 ms on the device over two runs, and no body differs.

## 2026-10-05 — a lane lost at a node ends there, and a lane gained is moved onto

**The owner asked for it, reviewing OdesaOsm's connections** (TER-5j): N lanes onto M join lane for lane and
nothing more. Of more onto fewer the lanes over end at the node; of fewer onto more the lanes over are reached by
none. It replaces the spread a node drew wherever lane for lane stranded a lane: every lane lost merged across the
ones kept, and every lane gained was fanned onto. **One lane joins one lane** (2026-10-06): the last lane in joined
the extra lane of a road one lane wider as well as its own, until the owner asked for every connection to be one
lane onto one.

- **A car moves across before the lane ends** (CAR-53): the router already prices a turn from the lane beside, so a
  lane that ends is left by moving across and one nothing reaches is entered from beside; a toured car does the
  same (the car slice's log). A car that has not found the room stops at the end, which `--bench stuck` reports.
- **Straight on is numbered from the kerb where nothing forks.** Read off its few degrees of bend, a carriageway
  OSM widens across a box — one lane, three for 32 m, one again — was entered by its edge lane at the line and left
  by its kerb lane, two lanes over in 32 m, and a car stood in it for the rest of the run. Numbered from the kerb,
  one lane runs through. At a fork the side it bears to still decides, so the branches cross nothing.
- **What no step across can leave**: OSM's way pieces of a metre or none inside a box carry lanes that end with no
  room beside them to move across in. A car drawn onto one stands until it is recovered; `--bench stuck` on
  OdesaOsm showed one, held 131 s.
- **GEN-50 counts moving across** as driving onto and off a lane, so a lane that ends beside one that carries on
  does not dangle.
- **The ground the spread covered is still paved** (`LaneUse.Eases`, `LaneLines.Tapers`): a box has no shape of its
  own and is the union of its movements, so dropping the merge left a kerbed island where the lane stopped and, on
  OdesaOsm, a boundary that did not close. The spread's lines are laid as tapers — paved, run beside by a roadside,
  never driven — so the ground is what it was.

## 2026-10-05 — the atlas files the walk only where it is read, and a piece reads only the rows it reaches

**Half of a traced city's atlas was walk nobody stood on.** OdesaOsm stands nobody on foot and files 5 200 km of
pavement two metres wide; the walk is read only under walkers and the claims they hold, so it was 56 million entries
read by nothing. **A walked way is now filed only in a town that stands anybody on foot, or where the traffic crosses
it** — a zebra's paint (`RibbonAtlas.Lay`, TER-4c.4). The marks are worked out from the ribbons and hold every way
either way. The roster is counted before the atlas is laid, since it is the plan's and the figures' alone. The
suite's own city stands nobody on foot either, and its town tier is the town it was.

**And a piece measured every point of its box** (`RibbonAtlas.PointsOf`): a piece running corner to corner of its
box covers a small part of it, and every point was a projection. Each row is now read only along the runs a piece's
band can reach (`RunsAcross`) — one across a straight's rectangle, up to two across an arc's ring, grown by a lattice
step and an arc's by a share of its radius, so rounding can only widen them. A way already in order skips its sort.
**The atlas is the same to the bit**: hashed whole on the suite's city and on OdesaOsm, runs against the whole box,
and with the walk filed against the atlas before this and the entry below it.

Measured on OdesaOsm with `qq prof --bench load`:

| | before | after |
|---|---|---|
| atlas | 88.2 M points, 123.9 M entries, 1 093 MiB | 46.5 M points, 67.5 M entries, 625 MiB |
| laying it | 2.4 s | 1.1 s |
| finding its points | 22.7 CPU s | 5.1 CPU s |

**Not done: the points filed in a second pass rather than kept for it.** The blocks a lay keeps its points in stand
under the entries they are filed into, 540 MB at once on OdesaOsm and the open's peak; filed off a second run of the
geometry they are gone, for 5 CPU seconds more — every one of them a second on a page's one thread.

## 2026-10-04 — the line two ways meet on is unbroken where the road has more than two lanes

**The owner asked for it, with the rule it paints**: overtaking into the oncoming lane is not allowed on a road of
more than two lanes, and the line there is drawn solid. **One relation answers both**
(`CityPlan.RoadArrays.LineCrossedToPass`): only a carriageway of one lane each way is crossed to pass (CAR-6.2b).
Its lanes carry the answer to the car (`LaneLines.LaneCrossesToPass`), and its centreline run carries it to the paint
(`CentrelineRuns.IsUnbroken`). A run carries on only where the lanes each way are the same, so it is dashed or solid
all its length, and it is painted as a dash the length of the run, as a roadside line is. A car on such a road gets
past over a lane of its own way (CAR-53).

## 2026-10-04 — a lane turns onto the lane of its own number

**The owner asked for it** (TER-5j): a movement joins each lane making it to the lane of the same number on the
road taken, so a turn onto a road of one lane is made from the edge lane alone and never from the second lane
onto the first. It replaces spreading the lanes over the road taken, which merged every lane of a three-lane
street into a one-lane road forking off it at under `TurnStraightToleranceDeg` — straight on, so made from every
lane, the inner two cutting across the kerb lane to reach it.

- **Numbered from the side the movement bears to**: the kerb for the near side, the line for the far, so a turn
  to the far side is the inner lane's onto the inner lane. Straight on is numbered from the kerb, and only at a
  fork — two roads taken straight on off one arm — from the side its few degrees bear to, so the branch bearing
  off the far side is taken from the line and crosses nothing either.
- **What it costs**: a car can no longer pick its lane of the road taken at the turn. It lands on the lane of its
  number and moves across after where its route needs another (CAR-53).

## 2026-10-05 — a way has collision channels, and two on none in common share no ground

**A bridge over a traced town's roads must claim none of them** (the owner, with PHY-1a). Two places held the
ground of a deck and the road under it together, and both ask a way's channels (`IRibbonLines.ChannelsOf`, a
lane's road's level, a connector's two lanes' both, the walk's the ground's): **the marks** pair no two ways on
no channel in common (TER-5c), so no plan over the deck places a secondary claim below it; and **the lattice**
reads a body onto the ways on its own channels alone (TER-4c.2), so a car on the deck stands on nothing under
it. **The channels and not a level**
because a car's collisions are what those ways are on (physics, 2026-10-05): a bridgehead's connector is on
both, so a car crossing one claims and meets the ground's ways there and the bridge's alike, where a level put
it on one. A town with no bridge over a road carries no channel table and its reads ask nothing more. Every
car-side read passes the car's channels; every walker's is the ground's.

## 2026-10-02 — a point past a joint is read off the piece it is on

**A body near the joint of two pieces of its way came out longer than it is**, by up to two metres. A piece is
read a half-width and a reach past its ends, so the inside of a bend is covered, and a point both pieces reached
kept the reading it lay deepest in — at the metre its foot fell on, held to the piece. **A piece carried on past its
end is its circle, not the next piece**: outside a bend it runs nearer the points out there than the bend does, so
those points a couple of metres down the next piece were filed at the joint's metre. A car on O10 driving from 74.2
to 75.0 m along its lane was read from 72.95 m all the way, its body reaching back over the joint behind it.

**A point is now read off the piece its foot falls on** (`RibbonAtlas.PointsOf`), and only one whose foot falls on
neither — outside a bend, where the two part — off the deeper of them, at the joint. Over five minutes of O10 the
town was the one it had been: 8 394 bays against 8 423, 85 legs given up against 83, cars blocked with none behind
them for 146 508 car-ticks against 150 313.

## 2026-10-02 — the claims are not skipped by cell where nothing moves

**Asked whether a lane whose ground holds only frozen bodies (`SOL-37`) could be left as it was laid.** It cannot,
because a way's claims are not made of the bodies near it. A plan is laid from its holder's nose down the line
for as far as its grant runs, cut at the first body and answered against every other plan, and a light's hold turns on its own clock: a queue frozen at a red re-plans when it turns
green without a body in the town having moved, and a cut far up a line moves where a plan ends on a way nowhere
near it. A cell with no moving body in it says nothing about either, and a test that did would be a graph of
which plans read which ways — dearer than laying them.

**What a frozen body does cost the claims was measured instead.** On O10 at one, three and five minutes, laying
every frozen car's body a second time — a no-op, the digests unchanged — added 330, 660 and 545 µs to the index
phase a tick against 4 788, 3 963 and 4 191; inserting the same claims a second time from the last answer without
asking anything else added 30, 120 and 55. So neither the atlas nor the inserts are the cost: it is the per-car
questions asked before them (what it is hooked to, whether it is under way, what it is built as). Some of that is a
queued car's, whose claim reads its route and is laid again however still it stands.

**So a standing car is not asked again** (`TownWorld.StandsAsLaid`): one whose action is `Stand`, unhooked, frozen
in the same freezing, handed no other action since and at the same pose in the fleet as at the laying that last
asked it. Its body is laid from that laying's atlas answer, and the passes after the bodies — pass, manoeuvre,
plan, backing, grant — walk only the cars that were asked, since each of them leaves a standing car as
it found it.

## 2026-10-01 — a start stands for a run of two points, and an entry names its column

**A start for every point was a fifth of a large town's memory.** The atlas kept 257 starts a cell, so that a
lookup read the entries of the points inside a body and no others; at thirty times Odesa's area (O30) that was
391 MB of a 4.4 GB working set, against 1.4 GB of entries.

- **An entry names its point's column in the cell** (`Entry.Column`), in what was the record's padding: it is
  still eight bytes.
- **A start stands for a run of points along a row** (`RibbonAtlas.PointsAStartShift`), and a lookup reads the
  runs a body reaches and passes over the columns outside it — at most one point either end.

The entries a lookup reaches for the points it covers, and their order, are the ones it read before, so the
town is the same to the bit (`--bench age` digests at 1, 3 and 5 minutes). Measured on `--bench age --map
Odesa`, interleaved runs, µs a tick of the index phase:

| a start every | 1 min | 3 min | 5 min | starts at O30 |
|---|---|---|---|---|
| point | 142–147 | 173–188 | 161–173 | 391 MB |
| two points | 143–149 | 180–190 | 168–187 | 196 MB |
| four points | 145–151 | 182–191 | 172–191 | 98 MB |

**Two, and not four**: the cost is in the five-minute window's crowded junctions, where a point carries many
ways and every extra point read is all of them, and two points holds it to about 2 % of the tick.

## 2026-10-01 — a point waits to be filed in eight bytes, and a cell's keys are made where it is sorted

**The atlas's lay was most of an open's peak**, and almost none of it was the atlas. At ten times Odesa's area
(O10, the people and districts at Odesa's density) it kept 566 MiB and held, at once, every point's `Sample`
between being found and being filed (16 bytes an entry, 920 MB) and a sort key for every entry of the town
(8 bytes, 460 MB).

- **A cell's sort keys are made on the thread that sorts it**, in a buffer as long as the fullest cell, from a
  byte an entry naming its point; the key and so the order are what they were.

The atlas is the same to the bit: every array it keeps hashed equal on O1 and O10 before and after. Measured on
`qq prof --bench load`:

| | atlas before | atlas after | peak before | peak after |
|---|---|---|---|---|
| O1 (Odesa) | 199 ms | 203 ms | 354 MiB | 336 MiB |
| O10 | 2158 ms | 2057 ms | 3159 MiB | 2490 MiB |

## 2026-10-01 — the atlas keeps a thread's points in blocks, and files them on every thread

**The atlas was the largest stage of Odesa's load, about 300 ms of 1.5 s, and most of it was not geometry.**
Every way's points were an array of their own, built through a second list: 160 MiB of small allocations
from every thread at once, and the workstation heap hands out room under one lock, so a third of the pass's
CPU was waiting on it. Filing the points into their cells, and building the entries after the sort, were
then two passes over five million entries on one thread.

- **A point is filed by whichever thread reaches it**, at a place taken atomically from its cell's cursor,
  and its entry is built there. The order points land in is lost to the cell's sort, whose key — point,
  then way — no two entries share, so the atlas is the same to the bit: every array it keeps hashed equal
  on Odesa before and after.
- **Each cell is sorted and its starts read on one thread**, every cell in parallel.
- **A way too long to file is refused before anything is laid**, rather than when its first entry is filed.

Measured on `--bench load`, Odesa, three runs each:

| | before | after |
|---|---|---|
| atlas | 298–316 ms | 214–231 ms |
| open | 1505–1542 ms | 1435–1460 ms |

## 2026-09-30 — nothing moves over a way it has not claimed

**The owner asked that no action drive or walk the road without a claim on it** — any lane, the pavement's too,
forwards or in reverse — and that anything not claimed be approved by name. Their answer to the list put to them:
every planned movement claims the lanes it crosses, ground on no way needs nothing, and a push or an accident plans
nothing (TER-4c.8). Before, a grant was infinite by default and only a plan made it less, so a holder that laid
nothing could go anywhere:

- **a car back on its line after losing it** drove a tick on a line nothing had planned, the rebuild before having
  seen it still off;
- **the last metres of a car's line** were never planned: a plan stopped where the axle's line did, and the nose of a
  car at rest there stands a nose further on. Granted nothing there, every car pulling up at its bay stood on the
  brakes — 204 → 1 493 car-ticks on Odesa — until the plan reached where the nose would be;
- **a walker off the pavement** walked back over the carriageway holding only its body, and **an officer** walked to
  the post and back with no plan at all.

**A grant is zero unless the action laid a claim this rebuild** (`DrivingGround.ReadTheGrant`,
`WalkingGround.ReadTheGrant`), and goes with the action that laid it: the one claim handed from one action to the next
is a plan down a car's own line. **The claim nobody drives is committed ground**: a hand's (S-7) and now a tow's, held under the truck. **A
placement is put down only where nobody holds the ground** — the door and the pavement already were; the winch and
the yard now wait for it. **An officer on duty and their car are one occupant**, as a tow is: the officer walks to the
post beside the car on the same lane, and a reservation has no across to tell the two apart.

**A walk back is claimed only off the pavement.** Claimed from anywhere off its own way, walkers shoved across a
corner stood in each other's straight lines back and each waited for the other: 439 → 776 walks given up on Odesa.
One between a pavement's two lanes walks its route, its body and its plan holding what it steps over. And **a body a
walker is level with cuts nothing**, read off where the walker's own body lies on the way rather than where its
straight comes onto it — the words TER-4c.1 always had.

Five minutes each (`--bench stuck`, against the commit before these changes): Odesa 477 bays, 438 walks given up
(439), 173 car-ticks spent the braking margin (204), no wreck; River 451 bays (445), 333 walks given up (342), no
wreck. `CarActionTests` and `PersonActionTests` hold every car and walker of the suite's city to it every tick.

## 2026-09-29 — a moving car plans no further than its corners let it rest

**A plan drew back on its own as its car slowed for a bend** — the owner saw a car's hold reach round a
right-angle turn and down the road past it, then shrink to the join as the car braked for the turn, with nothing
cutting it: a car that knows it will crawl round a turn should never have planned that much. The planned run
held the speed the car was planning for (TER-4c.1), which on the approach to a bend is the approach's and not the
bend's. **What a moving car means to use now ends where the corners ahead let it be at rest**
(`CornerLimits.RestToM`): the first arc begun past its nose, entered at its entry figure, and a stop from there.
It is read off the arcs the line was laid with and it is every build's alike, since the grip a car corners on and
the grip it stops on cancel; and it only moves on as the nose does, so a plan bounded by it grows as the car goes
into the bend and never draws back.

**Read at the nose and not at the lead point the follower brakes from**: the lead point carries the pedal, and a
foot coming off the throttle pulled it back over an arc's start and the plan with it, by as much as 12 m. **The
room to pull away is bounded with the run**, or a car cornering a little faster than the entry figures (a build
whose pedal stops it before its tyres would) planned past the bound by that and drew it back as it slowed. Over a
minute of each of the suite's towns, ticks on which a plan nothing cut drew back went from 10 155 and 2 018 to 28
and 31, and to 7 and 8 once a car braking in a bend stopped running out of it (`PlansInATownTests`; the wheel out of
the planned speed and CAR-47 are the car log's). What is left is a brake applied for a light still coming off the
pedal, and a car read a few metres back along its line for a tick in a tight arc.

## 2026-09-29 — a plan reaches no further than a plan may

**The owner asked for plans bounded by the road rather than by speed alone** (TER-4c.1): a stated length
(`DrivingFigures.PlanMostM`), the car's own stop from its top speed, and no more than two joins that break its
line — "connectors give unpredictability and extra curvature, so actors should not plan too much ahead on
those". **Which joins count is geometry and not a junction's arms**: one whose curvature, or the curvature of
the lane it lands on, departs from the lane it leaves by more than a step (`JoinBendStepPerM`,
`RoadGraph.BreaksTheLine`), so a turn counts and the road carried straight on through a crossroads does not.
The movements crossing it there are plans, and it is the plans that answer for them.

**A plan held short is a stop point** (S-2, `DrivingHold.Reach`), or the ground a car can no longer stop short
of would run past ground it holds and meet nothing there — a light's hold past the end of the plan included.
So the length is what bounds pace on an open road: at 40 m the drive probe tops out at 27 m/s against 46
before, the mean is unmoved, and the reach binds about two car-ticks in five. **The amber was not derived from
it**: a car at the nominal build's braking caught at the edge of what it can stop short of reaches the bar
about 1.5 s later, which is the amber's own length, and a softer-braking build a little over it.

## 2026-09-29 — a zebra is planned like any other ground

**The whole-or-nothing zebra went** (TER-5c.3): a plan reaching a zebra ran on to the car's own length clear of
the far edge, and anything refusing it in between answered it where the zebra began. The grant credits nothing
in front with a speed (S-2a), so the car ahead pulling away over the paint refused it as surely as one parked
past it: **every car of a queue leaving a green stopped at its bar** — the bar stands a setback short of the
zebra, and the stand-off off it put the car there — and pulled away from rest once the car ahead was a car's
length past the paint. The owner asked for it dropped. A plan over a zebra is now cut where anything on it is,
like a plan over any other ground, so a car can come to rest on the paint behind a queue standing just past it;
the exam card that asked otherwise went with it.

## 2026-09-28 — a box is not remembered

**Who had a box last time went out of the comparison** (TER-5e): a car carried the movement it had won from one
rebuild to the next (`CarFleet.MovementWay`), its plan to and through that box won ties against an equal
movement nearer since (`LaneClaim.Held`), and once it could not stop short of the mouth the whole join was
committed. The owner asked for the reservations to be the whole of it, and the scenario map holds without any
of it: **109 of 109 cards pass on ground a car stands on and ground it can no longer stop short of**, and
nothing else.

**Committing a whole box statelessly was tried both ways and refused.** Committed through the join once the car
could not stop short of its mouth, a through car too fast to stop at the mouth took the box from a car on a call
it had been refused against (the call card never arrived); committed once the car was in the box, a car waiting
inside it at the metre it was refused held the car it was giving way to (five tee cards). A car in a box still
plans the rest of the join and its own length of the way out.

## 2026-09-28 — a zebra is marked whole

**The owner asked for a zebra to be one piece of ground to both networks** (TER-5c.3): a car reserving any of
it places secondary claims on both of its walking lanes whole, and a walker reserving any of it places them on
every lane under it. The ribbons had marked only the ground the two actually share — a lane's band of each
walking lane, and each walking lane's stretch over each lane — so a car was held off the walker's band of its
own lane and nothing more, and a walker had to plan the paint to the far kerb, and be sent back to the kerb
when refused anywhere on it, to hold the lanes it had not reached. **The marks now say it**, laid with every
other mark and read the same way, and the walker's special cases went with the reason for them (`PER-27`).

**Driven ways only.** The walk a zebra hands over to at each kerb shares the end of its paint as any two walks
do; marked whole it would hold the zebra for somebody walking past the end of it.

**The per-lane bands went with it** (`CrossingEdges`, `LaneFurniture`): which lanes a zebra is painted across,
and where on each walking lane each of them falls, was read by nothing once the marks carried it.

## 2026-09-28 — a light is a rung of the ladder, and its hold the one secondary claim with no main claim

**A light stopped clipping the plan and started holding ground** (TER-5g, TER-5c.1): the owner asked for the
lights to be a secondary reservation on the lanes they block, high on the ladder and not above the police or
an ambulance. So the ladder gained a rung, **p4**, above a walker, and everything below
it moved down one — a walker to p5 and the movements to p6, p7 and p8. The numbers are the ladder's order
and nothing but the ladder and its own tests names a rung by one. Why the hold is secondary, where it
runs and why it is placed first is [agents/trafficlight](../../../agents/trafficlight/docs/decision-log.md).

**The bars are laid once** (`StopBars`, TER-6): the lanes' furniture read the plan's `StopLines`, which only
the scenario map filled, while the picture painted the town's own. The furniture, the lights and the heads
read the one laying now.

## 2026-09-27 — the marks' pairs are hashed from both ways, and the atlas lays in time linear in the town

**The atlas's time was the square of the town, and all of it was the marks' merge.** Every pair of ways
that share ground is keyed as the two numbers in one `long`, whose own hash is the two xored — and ways that
share ground are numbered close together, so the pairs fell in a few buckets. Odesa's atlas laid in
0.8 s and Odesa's brief with its extent doubled each way, 4.6 times the entries, in 26 s; the merge alone
was a third of the whole load.

The key is now hashed with both halves mixed (`RibbonMarks.PairHash`). The same two towns lay in 0.39 s and
1.8 s. What is filed is unchanged: the doubled town's census is the same line for line, and every body of
every shipped map stands to the bit where it did after 3600 ticks.

## 2026-09-27 — a body is read off the atlas point by point, and nothing it already knows is asked again

The owner asked that a lookup not work out again what the town already knows, the physics grid's cells
above all. **Which cells a car is in is not what a lookup pays for.** They are shifts of the rows of
points the atlas needs anyway. The physics index files 1.02 cars to each 8 m cell on Odesa, so walking it a
cell at a time shares nothing between cars.

**What a lookup paid for was reading entries it then passed over.** A car's row, read from its first entry,
held 172 entries for the 50 the car stood on, and deciding which to skip was branches. So:

- **A lookup reads the points inside the body and next to nothing else**, off starts kept inside each cell
  (how fine they are is the 2026-10-01 entry on runs).
- **A way's stretch is gathered in the tenths the atlas files**, and turned into metres once per way rather
  than divided once per entry. They are floats: integer least and most compile to branches, and those cost
  a lookup a fifth.

**Measured** on the fixed-tick soak, microseconds a tick, the atlas lookup alone:

| | before | after |
|---|---|---|
| Odesa, 520 cars | 287–288 | 227–236 |
| River, 480 cars | 254–256 | 201 |
| Odesa walkers | 77 | 62–64 |

Tried and left out:

- **Remembering a cell's slot across its rows**: no gain, and a call a row.
- **Integer tenths**: 20 % slower than before.
- **The solver's own direction for a car**, in place of a sine and cosine of the fleet's heading. The two
  are one in a running town, but a fixture stands a body by writing the fleet, and a box read from the
  fleet's place and the solver's direction is neither. It saves 4 µs a tick.
- **A point's depth taken once for all the ways over it, and each point's start read once**: no gain
  (Odesa's index 484 µs a tick before, 483 after). The profile put a quarter of the lookup on the depth
  test, but the arithmetic was not what it was paying for.

The soak's figures on all four maps are identical line for line before and after.

## 2026-09-27 — a car refused in a box waits there, and nothing moves the answer

The owner ruled the flow pure: a patch beside it goes, and what the flow gets wrong is fixed in it. So
**`WaitsClearOfTheBoxes` went** (TER-4c.1). A car refused by a plan where it would rest on ground its join
shares with any way was sent back to the mouth. It judged from the marks, not the claims, and the ground it
kept a car off was often ground nobody held. It cut at a place nothing stood, as `LaneClaim.Nothing`, so
`TheBoxGiven` and the stuck probe each had to handle a cut with no cause. It spared cars cut by a body. And it
judged "can still stop" by a third stopping distance. `WayCrossings.OwnRuns` went with it, read by nothing
else.

**Measured** over six agent seeds of a minute each, the rule against none:

| | Odesa | River |
|---|---|---|
| wrecked | 27 → 37 | 52 → 46 |
| touches | 214 → 202 | 226 → 224 |
| km driven | 2417 → 2409 | 1980 → 1955 |
| walks given up | 450 → 472 | 405 → 407 |
| car-ticks at rest inside a box | 178 k → 184 k | 242 k → 268 k |
| rebuilds left unsettled | 15 213 → 17 786 | 16 201 → 20 852 |

The wrecks move both ways, inside a spread of 0 to 16 a seed. The three crashes of the Odesa seed that went
0 → 6 involve no car waiting in a box. A walker steps onto a zebra three metres in front of a car at 13 m/s.
Two cars on the two lanes of one road meet as shapes, off each other's ribbons. A car running wide on a corner
at 29 m/s hits one standing off its line. What does move is the waiting in boxes, and with it the rebuilds the
settling leaves at its bound. The exam is 108 of 109 cards either way, card 59 failing on the same 0.2 s.

## 2026-09-27 — a claim is main or secondary, and a main claim reads its own way alone

The owner named the claim a holder places on a way its own crosses a **secondary claim**, and ruled that
**two secondary claims never meet**: main against main on one way, and main against secondary, are the only
meetings. Placing a claim reads the main way alone, and the secondary claims go down with it (TER-5c.1).

**The reads of the other way went, because they were a second copy of one answer.** `Reach` walked each
mark's far way for main claims over its section (`LosesTheSection`), and `Take` cut those main claims when it
placed the section. A mark is filed under both its ways, so each such main claim had already placed a
secondary claim over the asker's own way. The ordinary walk of that way met it at the same metre, on the
same terms. The one term that moved is whether a holder's body stands on the ground: it is now read off
the asker's way rather than the far one. Taking that secondary claim had already cut its holder short of the
mark, too. **So the atlas files a pair only where both sides hold ground**: a side clamped to nothing would
take a secondary claim and place none back.

**Measured**: the exam is 108 of 109 cards before and after, card 59 failing both times on the same 0.2 s;
`qq tests all` passes, the gates included.

## 2026-09-27 — a ribbon is its way's own width, and a mark is worked out from the ribbons

The owner ruled that **a ribbon is its way's own width** — a lane's, a join's, a pavement's — and that there
is no ribbon at a car's width; and that **a mark's section runs as far as the two ribbons share ground, up to
where they are almost touching** (TER-4c.4, TER-5c). On the overlay a join's marked section had stopped about
a metre inside the lane it crossed: the block was drawn at the lane's width, and the section had been cut
from ribbons a car wide, sampled on the lattice.

**Marks are worked out from the ribbons and not from the lattice** (`RibbonMarks`, `RibbonPiece`). A piece of
a way's line swept to its width is a rectangle or a sector of an annulus; a metre of one meets another's
ground where the slice across it does, and each end of a section is walked for a lattice step at a time and
then halved to under a micrometre. Lane-width ribbons had failed before on the lattice's reach: laid that much
wider, the two lanes of a carriageway overlapped by 0.7 m and every pair of joins out of neighbouring lanes
was marked. Worked from the ribbons, ribbons laid edge to edge only touch — on Odesa, joins marked against
joins went 71 833 → 72 411, and lanes against joins 1 920 → 12, since a hand-over is no longer one.

**Shared ground is ground deeper than the touch inside both**, each ribbon worn back by it at the sides and at
the square ends, and a section runs over all of that and no further. Run to where the ribbons last touched, a
2 m pavement edge stepping onto a zebra's kerb end at an angle — its square end dipping a few centimetres
into the lane — was marked whole, and a car passing locked the pavement. Worn, those 1 050 pairs on Odesa
share 0.22 m of pavement edge on average and 0.90 m at most.

**A body is read over only ground it and the band both hold.** The lattice still files a point a reach past
every band, now with how far outside the band it stands, and a point counts where the collider reaches
further past it than that. At lane width the reach alone put a car 0.45 m off its line on the next lane.

**Measured** on Odesa over six agent seeds of a minute each (`--bench soak`), against the tree before:
knocked down 2 → 3, wrecked 34 → 34, touches 183 → 191, walks given up 269 → 448, km driven 2533 → 2415. Taken
while sections still ran to the touch, the same marks and body reading at the old widths gave 22 wrecked, 186
touches, 265 walks given up and 2561 km, and driven ways alone at their own width 35, 251, 360 and 2439 km:
what costs is the width, most of it on the driven side. The atlas on Odesa is 3.5 M points and 6.3 M
entries — 54 MiB — laid in 0.75 s, against 2.7 M, 3.9 M, 30 MiB and 0.7 s.

## 2026-09-26 — two layers over a ribbon atlas: where the bodies are, and where they mean to be

The owner set the reservations three rules. **A body reserves, at p0, every ribbon it stands on, and two such
reservations may overlap**, being a record of real things. **A plan is settled against its own
way and, through marks laid when the town is, against every ribbon it shares ground with** — the existing
comparison deciding whether it takes the other's ground or cuts itself to the free space — **with no geometry
at runtime**. **And those are the only two ways ordinary agents meet.** Everything in
[claims.md](claims.md) was rewritten to them.

**The atlas is laid once** (TER-4c.4, `RibbonAtlas`): every way's ribbon sampled onto a lattice a quarter
of a car apart, each point knowing which ways it lies under and how far along each. It replaced a per-body
walk of the nearest lanes with a band test every tick, a crossing table measured from centrelines for joins
alone, a zebra's bands projected while the town ran, and the furniture's claims: which ways a body is on is now
a look-up of lattice points. **Joins that diverge from one lane or merge into one are marked**,
their ribbons sharing their first or last metres: that ground is one piece of the world and two bodies cannot
both be on it.

**Ground a car can no longer stop short of became the first term of the comparison, not a p0 claim** — p0
is the body and nothing else. The ladder was redrawn round it (TER-5g): body, committed, call,
light, somebody on foot, then the three movements. **The stated band, `Reserved` and `Rejected` went**: a plan is one hold
from the nose, answered before it is laid, so there is no weaker band to tell apart from it.

**A plan is never laid past what its answer gave** (`PlanTheDrive`). A car's refusal placed at its committed
distance instead rode ahead of the car at exactly its stopping distance: a car refused a box held 19 m/s into
it, and the plan laid past its answer took the box from the car that had won it.

**What is outside the two layers is named** (TER-4c.5): placing a body leaving a building (`ExitSpotsOf`, a
placement and not a decision), route-cost memory (`LinkSurcharges`, now one table per network — the walking
router had been pricing its links off the driving table by id), building capacity, and the special agents.

**The owner's open decisions were taken at the plan's recommendations**: the new rules at P3, not P0 or P1;
committed ground as a plan nothing takes; a prop on a driven ribbon refused when the town is laid; the
walker's rung above every movement and below committed ground; route cost and
capacity exempt by name; a bay's ways planned like any other when bays return; and a body's own place on its
own line as its control rather than the reservation path.

**Tried and taken out**: extending a car's committed ground through the box it was in, which handed every car
creeping through a junction absolute priority and put them into each other.

**Measured** on Odesa over six agent seeds of a minute each, against the tree before: wrecked 34 → 26, knocked
down 14 → 2, touches 2719 → 187, walks given up 666 → 316, 1878 → 2092 km driven; over five minutes of
`--bench stuck`, touches 3799 → 265 and walks arrived 72 → 170. Two faults outside this slice were fixed on the
way and carry much of it — a corner the look-ahead reached a wheel's lookahead late
([agents/car](../../../agents/car/control/CarFollower.cs)), and a walk that ran out on a corner of no length
([agents/person](../../../agents/person/docs/decision-log.md)).

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- **TER-5**: a junction's radius is the connection standoff (a car park's, its rank's) and is not sized on
  the arms; nothing measures from it.
- **TER-6**: a town's zebras stand at the kerb ends' stations, not at pedestrian node pairs, spanning the
  carriageway's two edges; a box that forks nothing carries none.

## 2026-09-21 — one laying of a town's zebras, read by both networks

The walk was cut and joined at 488 crossings in `Towns.City` and the road knew about none of them. The walk
lays its paint off the town's own kerb ends (`Crossings.Lay`, WLK-10), while `LaneFurniture` and
`CrossingBands` read `CityPlan.Crosswalks`, empty since a junction stopped striking its own crossings
(`RoadStage`). Empty shows up as no answer rather than a wrong one: no lane carried a crossing, no crossing
carried a lane, no walker was ever *on* one, and the stop short of paint a queue would leave a car standing on
(`CAR-15`) had nothing to fire on in any town.

**So the town lays its zebras once and hands them round** (`TownWorld._zebras`): to the walk cut at them,
the ways laid across them, and the lights and their heads. What the walk crosses is what the road is crossed
by, because it is one laying and not two that agree.

## 2026-09-19 — an arrow is the movement drawn small, not a glyph picked out of a set

Three shapes and four more for the pairs, at a fixed right angle, would be wrong twice. **A fixed right angle
lies about the junction**: arms stand anywhere from `ArmsApartMinDeg` apart to nearly opposite, so most turns
are not right angles. **And a set of glyphs is a set to extend**: the first one-way street offering a turn and
a straight would want an eighth drawn by hand. So a branch is bent by the angle its turn was classified from
(`Spline.TurnedRad` of the movement's own line, the number `LaneLines.ConnectorKind` was decided by), and
nothing anywhere names *ahead-and-left* (TER-6a).

**The curl is sized by the lane and not the turn.** An authored radius has no answer for a shallow turn: at a
fixed small radius a thirty-degree branch leaves the bend early and runs straight off the side of the lane.

**The bar is laid once and read twice.** The arrow stands a setback behind it in the lane's own metres, the
figure `StopBars` placed the bar by, so the bar carries `AlongM` rather than the arrow projecting the bar's
centre back onto the lane and getting a centimetre's different answer over a bend.

**The glyph is anchored at its front.** Fixing the tail is the easier arithmetic and puts a turn-only arrow's
paint a metre and a half further back than the arrow beside it, a bend spending most of its run going
sideways — so a row of approaches reads as marks at four distances from four bars.

## 2026-09-19 — the walk hands the road two answers, and only the zebra follows the shorter one

A street too short to be crossed twice is crossed once midway (`WLK-10a`), and the first cut of that moved
everything hanging off a crossing with it: one band filed at both arms, so both lanes stopped behind a bar
in the middle of the street and the lane line had nothing left to be painted down. A bar is where a driver
holds for the box in front — a fact about the end of the street, not about where the paint went — so the
walk hands down two lists, `KerbEnds.HeldM` for where it is cut and `KerbEnds.CrossedM` for where it
crosses, the same list on every street but a welded one.

**The road reads both through one type** (`Crossings.Lay` of either span) rather than growing a second
registry of bands: two constructions of a band would disagree about a corner the week after either moved.
The paint is laid from the crossings; the bars and the lane line's trim from the stations.

**A bar with nothing in front of it falls back to the kerb end.** A station stands half a zebra out
from where the kerb ends, so a bar left at a station the paint has gone from holds a carriageway's width out
from its box. The second answer carries the kerb end at those arms, flagged as bearing no paint, and the road
takes it as a band of no depth — one figure, and the setback, the bar and the trim come out right without any
of them learning a second case. **Which leaves the dashes running under the stripes of a welded crossing**,
as they must: a run trimmed towards a band in its own middle is cut from both ends and no line is left.

## 2026-09-16 — an arm's paint is one bundle, and `Crossings` is where it stands

The band, the bar behind it and the end of the dashes behind that are one bundle read off one answer.
`StopBars` asks `Crossings` where the band is rather than subtracting a depth from the lane's own metres —
over a bend a lane and its road run at different rates, and the bar came out centimetres out of the gap it
was meant to leave — and `CentrelineRuns.PaintedM` reads the same band. Rule 3 of the markings is that
relation stated: a bar on an arm with a crossing is placed by the crossing alone. **The registries are
derived and not planned**: `Crossings` and `StopBars` are laid off the plan the way `CentrelineRuns` is, so
the paint reads one answer.

## 2026-09-16 — a lane line is painted down a carriageway, and a carriageway is not a road

Under the old layer a road ran through many junctions and a run was a stretch cut out of one road. Under this
one a road ends at every node something meets it at, so a run is **several roads joined into the carriageway
they are pieces of** (`CentrelineRuns`).

**The car park is what decides it.** A lot is a junction cut into a street that already stood (`GEN-52`), so
a street with three lots down it is four roads; painted road by road it came out as four lines with a gap at
every lot and four dash phases, against rule 4 of the markings. The same is true of the two-armed nodes a
bridge, a loop or a refused join leaves, so the test is **what stands at the node** and never what put it
there: two road arms and any number of bays is one carriageway.

**The ground between two roads is crossed on the biarc the movements over it are drawn on** (`LaneLines`),
since the two lanes either side of it are that line offset half a lane each way; `GroundMeshTests` measures
the paint against it rather than taking it on trust.

## 2026-09-14 — the town does not lay the junction that decides nothing, so a lane never merges

**A merge could not be expressed in the graph the search runs on.** A lane carrying on through a node left
movements landing partway along the lane they joined; the contraction was meant to read that relation, and
[DrivingNetwork](../DrivingNetwork.cs)'s view of the lanes did not answer for it — so `LanePlaces` was
derived a second time, every merge silently unioning a node with the *start* of the run it joined, up to
`1132 m` away. The coarse graph refused the join outright (`TravelGraph.Builder.Join` holds links to meeting
end to end, which keeps the search's bound admissible), and a link that can be entered at two places is two
links.

**So the shape that needed the merge is not laid.** Only the scatter made it — a one-way street arriving at a
three-armed node leaves each two-way approach one movement — and `GEN-18` refuses that arrival. Odesa's `42`
merges and `17` lanes with one way out are `0` and `25`, all `25` a roundabout's entry (`GEN-19`) or a corner
every other movement was refused for (`GEN-48`) — places a driver really is committed.

**Four layers of rule went with it**: the merge's own rule and its bounds, the station a movement lands at, the places a
lane is *driven through*, the ground a merge takes off the lane it joins, the merge's own right of way, the
assembler's third figure and the census row. **The contraction does that work** — it ends a link where a body
can go more than one way. **And the places are worked out once and handed over** (`SIM-7`):
`RunNetwork.Contract` takes them rather than deriving its own, which is what let the two answers disagree.

## 2026-09-07 — what the road *is* and what it hands out are two documents

One page carried nine sections and eleven thousand words, against a rule that a document covering eight
things is eight documents. The seam is that half of it described the network — a road, a junction, a
crossing, the paint — and half described a protocol over it, so `TER-4c`, `TER-5c`, `TER-5e` and `TER-5g`
went to [claims.md](claims.md) and nothing else moved or changed. Split by section instead, the claim
ladder would have been read without the rule it ranks.

## 2026-09-06 — a movement is as wide as the narrower lane it joins, and one figure says so

Three readers each took a movement's width off the lane it arrives on, which is fine while every lane in a
town is the same width — on a hand-authored map where a four-metre street meets a five-metre one it put
half a metre of tarmac past the narrow street's kerb. The narrower of the two lanes is the only width a
single band can have that never claims ground outside either arm (TER-5d.1), answered once by
`LaneLines.ConnectorWidthM`.

## 2026-09-06 — the graph reads the lines rather than drawing them

`RoadGraph` cut the roads, offset the lanes and drew every connector, and then laid the rules over what it
had drawn. Only the second half is this slice's: the first is the town's own geometry, wanted as much by
the ground as by the traffic, and it is `CityGen.LaneLines` now. What the move buys is that the tarmac and
the network cannot disagree — the question of whether the surface and the graph were laid to the same
figures cannot be asked.

## 2026-09-05 — a connector is an object, and where lanes meet is worked out from them

A movement was a lane paired with a slot index, and the pair travelled together because neither number
meant anything alone; a connector is the id and answers all of it. The plan's node table is gone from the
graph the town runs on: two lane ends are one place when a connector runs between them, or when they are
the two ends of one stretch driven either way, and `LanePlaces` works that out once so the contraction and
the claims cannot disagree about which lane ends are one piece of the world. The second clause is not
tidiness — no box admits the turn-around (TER-5f), so joined only by connectors a dead end's two lanes
would be different places and a leg could not be priced round a bay (GEN-4l). A spatial index over every
way was the alternative and needs no places at all, refused because the span a caller must give the walk
then has no bound the town can state.

## 2026-09-05 — a one-way road is a narrower road, and a corner is solved on the pair it stands between

The lane graph was already directed, so a one-way road needed only one lane laid instead of two; a
full-width road with a lane nothing may enter is a rule every drawer, claimer and walker has to be told
about. Which way it runs is carried and never inferred, since a narrow road is not necessarily a one-way
one. Laid down the middle of its own line it was a narrowing rather than a street, so every car through the
junction stepped sideways: it stands on the half it is driven, moved after the bends and never before them,
and a node with no fork goes with its arms. What broke was the junction, and it was already broken — every
corner was solved as if both arms were the same width, which until then they always were. A corner is the
crossing of the two kerb lines each offset by its own road's half, and whether one exists at all is
measured off the narrower arm.

## 2026-09-03 — a lane is the ground it is walked over, and the corners come off the line

A pavement lane was its whole offset line with each corner's ground remembered against it as margins, so a
metre or two at both ends of every lane was on the lane and on nothing else — real, since a body standing
inside a corner claimed it, and drawable only as a line ending in mid-pavement. The margins come off the
line itself, so a mitre sets off from the arriving lane's last point and lands on the onward lane's first.
Two ends of a short stretch could want the whole of it, so they are held back in proportion to leave the
lane four fifths of itself.

## 2026-09-03 — turning round on the spot lays no ground

`LayJoins` laid a mitre for every way out of a node including a stretch's own reverse — 7292 on Odesa
carrying 22.9 km — and nothing else in the network treated it as a way. That made it a way with claims on
it and no line anywhere, so the claims layer drew a fan of blocks the nodes layer had nothing to draw,
which is exactly what OBS-2d forbids. It also spent slots. The turn stays in the table, since a walk at a
dead end needs it, and lays nothing.

## 2026-09-01 — a zebra spans the road it names, and carries no span of its own

A crossing carried a span every planner filled with the width of the carriageway it was laying, so the field
agreed with the road until something laid one of the two again. The reach is solved and never carried
(TER-6). The depth stays the crossing's own, since how much of a road's length the paint covers is nothing
the road decides. The skew is part of the relation and not an exception: `Zebras`' off-square crossing was
8.83 m of an 8.00 m road, which is what the file held and what the derivation gave without being told.

## 2026-08-27 — a junction admits no movement that reverses the direction of travel

The turn-around was in the table from the beginning and drivable by nothing — two opposing lanes join on a
1.5 m semicircle — and was classified, laid, measured, ranked and priced at infinity, which is a great deal
of machinery to say *never*. It is gone (TER-5f), and a quarter of Odesa's 1472 movements went with it. What
a route may still do is come back down the other side of one stretch in a car park's bay, priced rather than
joined (`GEN-4l`).

## 2026-08-25 — where a road's paint breaks is the road's answer, not the drawing's

Dashes were laid by walking each road and asking whether a point was inside a disc or on a zebra, so
every arm was dashed right up to the mouth of the box, past the bar a driver stops at; the metre step was the
smaller fault. The boundaries are not looked for any more — `CentrelineRuns` takes them from whoever measured
them.

## 2026-08-23 — a junction is committed to at the rate the car actually brakes at

The claim distance and the point past which a crossing is kept were the only stopping distances in the town
read off the pedal's cap, while every stretch of road is sized by the follower's braking figure. The cap is
larger, so both erred the way that costs: a car past the point it could stop gave the sections back for a bar
it was going to cross anyway, and in between two ticks they read free. Neither noticed wet ground, where the
gap is widest.

## 2026-08-23 — a stretch runs out at the box's near edge

`WaysAlong` stopped walking when the *next lane* began, and the next lane begins on the far side of the
junction — so nothing was laid on a junction until the stretch reached clear across it. A car approaching a
box claimed none of it, was granted its road as though the box were empty, and could see nothing standing in
it. The guard is the near edge now.

## 2026-08-22 — neither network's claims are one roster's

A walker on a crossing claimed the road and was in none of the road's questions — half right, since a walker
read as an obstruction is one a driver is held off, and one read as a committed claim cuts a car three lanes
away. What that cost was invisible until the ray went: nothing cut a driver's road at a body standing in it. A
body on foot is in every query a grant is taken against and carries its own reading, so what it must never be
is a property of which query happened to skip it. An occupant is an index into one of two rosters and the
stretch has to carry which, or the first walker whose index matched a car's is read out of the wrong fleet. The
walker's give-way arithmetic went with it: the claim *is* that arithmetic, already done, from fresher numbers.

## 2026-08-22 — braking has its own margin, and it is nearly all of the grip

Using the cornering margin for braking put the planned stop at 13.1 m/s² against the 21 the tyres actually
delivered, and every claim is sized by the planned figure — so a car held half again as much street as its
stop was going to use. A corner is held for as long as it lasts and its margin covers a bump, a camber and
the wheel still being turned; a stop is aimed at, straight, and over in seconds. Corner speeds are
untouched, which is the point of the figure being its own.
