# Tech debt

**What is known to be wrong, slow, redundant or unmeasured in what is built, and left so.** Each entry says where it
is, what it costs and what closes it; one fixed is deleted, not struck through. Why something came to be is the
owning slice's `decision-log.md`; an absence big enough to be structural is a
[known gap](index.md#known-gaps), and what is unbuilt is the instruments' to report ([CLAUDE.md](../CLAUDE.md)).
**An entry marked _owner_ waits on a ruling** and is not to be closed without one.

## Failing tests

- **The generator review's tree moves the suite's city onto a failing `P0` gate** (2026-10-26, _owner_):
  `AllocationGateTests.AWholeTickWithATrainCrossingTheTownAllocatesNothing` allocates in its measured window on the
  town the reviewed generator lays, every run, and passes on `HEAD`'s — 40 392 B then, 65 672 B since intercity roads
  are drawn as one curve each (GEN-63). Nothing in the tick changed: the town did. **It is growth and not a leak**:
  read in 300-tick windows after the gate's own warm-up, the first takes the 65 672 B and the five after it 0 B each,
  so something the world sizes on its first use grows just past the warm-up on this town — the same shape as the
  120 B `LaneOccupancy` wish table the entry below names. An allocation trace of the first window (`dotnet-trace`'s
  GC allocation ticks) names it; pre-sizing it closes it. **The review is held uncommitted until then**. It passes on
  the towns the realism plan lays (2026-10-27, its core at the water): hidden, not fixed.
- **`DeviceAllocationGateTests.AWholeTickOnTheDeviceAllocatesNothing` fails on a town that claims more ways than its
  holders are laid for** (device, `P0`, _owner_): it failed from 2026-10-27, when a bay's kerb was read off every lane its
  street carries (`CarParks.KerbM`, GEN-67), and passes on 2026-10-29's town — hidden, not fixed. The held-back
  GEN-55 fix (`BuildingsTests.NoBuildingFrontsTheRoundingOfACarPark` below) moves `Towns.Built` onto it again,
  68 552 B; not broken down, but the size of the growth below.
  **Named, and growth and not a leak**: 67 912 B at tick 4 083 of the run, none in the 3 000 after it — `LaneOccupancy.Touch`
  doubling `_rows`, `_touched` and `_claimsChangedAt` from 1 696 to 3 392 the first time the town claims its 1 697th way.
  They are laid at `WaysPerHolder` (4) a holder, off OdesaOsm's 3.2; the built town touches more than 8 a holder. Sizing
  the rows off the town rather than its holders closes it, and trades memory a way for rule 2 — the owner's to weigh.
- **`DeviceCrossingGateTests.ATickOnTheDeviceCrossesTheWallAsOftenWhateverTheTownsSize` fails** (device, `P0`,
  2026-10-27, _owner_): the busiest tick crosses 167 times on the suite's built town and 177 on it with four times its
  people. It fails as well with that day's bridgeheads-as-joints and track-bridges-as-one-deck switched off, and the
  device tier had not been run on the generator rework's tree before, so when it began is not known. Which pass the
  larger town runs and the smaller does not — a step of another shape rather than per-body calls, or per-body calls —
  is what a per-tick count of `Vk.Crossings` by call site names; that and the fix close it. Still 167 against 177 on
  2026-10-29.

- **`ShippedCityTests.NothingStandsInItsWater(OdesaOsm)`** (maps) fails: "2 road places, the first at <3430.4446,
  12487.6455>". A service road traced from OSM curves along a pond, and its carriageway's edge stands about a metre
  inside OSM's own outline of the water. No stage laid it there, and the map holds both shapes as surveyed. Closes by
  mending that road off the water in `qq osm --mend`, or by cutting the water back from the roads the survey holds
  when the sea is laid (`TracedSea`).

- **`SolverGateTests.AStepAllocatesNothing(bodyCount: 1000, packed: True)`** (perf) failed once in a light
  `qq tests --changed` run, "Expected: 0, Actual: 1" (2026-10-22), and passed twice on its own after: one allocation
  in the measured window that the solver's own code did not make, or did only when the run was loaded. A run of the
  perf tier that catches it again, with the allocation's stack, closes it.
- **`AsideThreadTests.TheFirstWaitTheCallerHasToBlockOnAllocatesNothing`** (unit) failed once in a light `qq tests`
  run (2026-10-25) and passed on its own straight after; the change under it was a view figure nothing on that thread
  reads. Same shape as the solver gate's above: a run that catches it again, with the allocation's stack, closes it.
- **`SheetAtlasTests.TheShippedArtPacksIntoAHandfulOfPages`** (unit) fails: 8 pages against 5 (2026-10-23). The
  civilian fleet's cars, wrecks and zone maps fill six (the lamp sheet laid four looks a row, `LampAtlas.LooksARow`),
  and the evacuator looks the seventh, which the props' 203 looks still fit (2026-10-25); the twenty-one walker and
  uniform looks cut from orbit clips on 2026-10-28, at the cars' 96 px/m, take an eighth, still 8 with the last three. Each page is 64 MB of sprite
  memory. _owner_: paying for the pages, or holding the fleet and the service looks at what five or six take, closes it.
- **`GroundMeshTests.TheLargestTownsGroundIsLaidOnceAndIsNotEnormous`** (town) asks one content town's ground to
  outweigh another's, which moves with either brief and says nothing of the mesh (VER-12); it failed at one shape of
  2026-10-27's and passes since. _owner_ — whether it is asked of a bound on the mesh alone or dropped.
- **An intercity road reaching the core is filed as ending on the first one** (2026-10-26, GEN-61, GEN-13;
  `Connectors.Intercities`): the core's own cell is marked as the first road's, so every road a route takes to the core
  is laid as ending on that road there, and none is asked whether it meets the core square to the arms already there.
  On `Twinford` the road from the north-east comes in 41° off the north road, its last stretch is refused, and the
  district between it and the railway (37, 0.31 km² at the core) is left with no road in and no street — the middle
  of the city an empty field. Filing a road that reaches the core cell as ending at the core, routing one that would
  meet it under `LinkArmsApartMinRad` from another to a road short of it instead, and leaving the cells within two
  of the class's radii of the core out of a route's goals lays it: `Twinford` stood streets in 75 of 78 districts,
  `Highmoor` 256 of 259. The `P0` claims gate that change moved the fixture onto is fixed (a mark inside one decimetre,
  `LaneOccupancy`), and the two roads half the intercity roads apart now run through the core as one curve (GEN-63),
  so the filing stands only for the roads after them: not measured since.
- **`AllocationGateTests.AWholeTickOfAWholeTownAllocatesNothing(Laid)`** (perf, `P0`) failed once at 120 B over 600
  ticks on 2026-10-16, with the machine at a load of ~25 and the test host already on the shipped collector; it has
  passed every run since, five in a row on a quiet machine. It failed again on 2026-10-23, the same 120 B, on `(Test)`
  inside `qq tests --changed`, and passed alone three times and in two whole perf runs after (and once more that day,
  the same 120 B inside a `--changed` run, passing alone after; and on 2026-10-24, the same 120 B on `(Test)` inside a
  `--changed` run, passing in the whole perf tier after; and later that day the same 120 B on `(Laid)` inside a
  `--changed` run, then on `(Test)` and on `AWholeTickWithATrainCrossingTheTownAllocatesNothing` in one run of the gate
  alone, passing alone twice after); the town is the same every
  run, so what allocates is not the tick's own work. What allocated is not known; an allocation trace (`dotnet-trace`'s
  GC allocation ticks) of a failing run closes it.
- **Seven exam cards fail** (2026-10-29; 70, 93 and 110 pass since the walker's straight and hop were mended and the
  exam watches two minutes — person decision log):
  - 39 (the nearer of two straights goes first): driver 0 on the box at 3.5 s, after driver 1 at 2.8 s, neither held.
    **Traced: the engine is right and the card is staged against its looks.** A card's cars wear the fleet's looks in
    table order, and card 39's pull away at 2.5 m/s² (driver 0, 1 420 kg, 20 m out) and 11.9 m/s² (driver 1, 2 000 kg,
    35 m out): the further car is at the box first, its plan reaches the ground the two share first (1.1 s in), and of
    one rung what is held keeps (TER-5e). The card asks distance where the rule answers time; staged with a margin
    these looks cannot beat it is card 45 (`CloseM` against `FarM`) again. _owner_: recast it as "the one whose plan
    reaches the shared ground first goes first", stage it on looks pinned to its drivers, or retire it — no card can be
    taken out of the table without every later card wearing other looks.
  - 67 (a left turn on green gives way to the oncoming right turn): driver 1 (North-West) drives straight through and
    round the block. **Traced: its junction does not offer the right turn.** `LaneLines.WhatTheJunctionOffers` drops
    lane 264 → 262 at cell 6,4, its tightest arc 2.30 m under `JunctionCorneringRadiusM` (2.41 m): the arms are drawn
    off the chord (`ConnectionPoints.ArmOf`, as the generator draws them), and where two close under a right angle the
    near-side turn between them falls under the floor. Nine near-side turns of the exam's lattice are dropped so; 67's
    is the one a card drives, and a generated town loses its own the same way. A junction stood off far enough for its
    near-side turns to hold the floor at the corner its arms draw closes it; the exam laying its arms on the chord
    would close the card alone.
  - 108 and 109 (getting past a car stood in its lane; a call past a queue at a red): no pass is decided — the
    "nobody reads what a body is doing" entry below.
  - 80, 84 and 89 (a car gives way on a zebra): "driver 0 was on the zebra for 5.6 s (84: 5.2 s; 89: 84.4 s) with
    walker 0 in its half of it". On 84 and 89 the car stops behind somebody standing at the kerb end of the paint,
    inside the zebra, and is on it when they cross; the room once kept behind a body going nowhere held it short — the
    same entry. **Traced on 80, two ways onto the paint.** At its pause point the walker has arrived (`Walking` false)
    and plans nothing, so only its body holds the lane — a stand-off short of the body, which stands on the paint; the
    same entry. And for the laying before that, the end of its walk inside the front of its body and `IsHopping` not
    yet true, `PlansTheWalk` plans nothing ahead of it: the car, cut by the body and not the zebra's marks, accelerates
    for a turn on the room it was handed and then commits (p1) past the mark's start, which nothing takes back — its
    nose 0.27 m over the paint where it would otherwise stop 0.07 m over. Planning those last metres as the hop closed
    the gap and failed card 69: the hop's landing, held either side of a walker standing where it lands
    (`WalkingGround.TheLanding`), was refused behind it by the red — the landing wants the straight's rule (person
    decision log, 2026-10-29) before the gap can be closed so.
- **The exam names the wrong holder of what cut a plan** (`ExamDrive.Cut`, `CutSeen.Of`, 2026-10-29): read after the
  claims are laid again, `LaneOccupancy.HolderAt` reads the slot the refusal pointed at in the laying before, now
  somebody else's — card 80's car read as "held by the main claim of driver 0 of card 80 at Afoot", its own, where the
  walker held that ground. The rung and the place are right; a trace that follows the name is misled. Keeping the
  holder with the refusal when the hold is cut closes it.
- **An exam card reads a car out onto its arm anywhere along that arm** (`ExamDrive.Read`, `ClearedAt`, 2026-10-29):
  "cleared" is the car's middle further out along the arm it was sent to than the box and half itself, wherever it is
  — so card 67's driver 1, which drove straight through and round the block, read "out 25.6 s" 130 m south of the box,
  and had it reached its place that way the card would have passed. Reading it only off a car leaving the box onto that
  arm's own lanes closes it.
- **Sixteen town tests are skipped** for the decisions no claim can make any more: all fourteen of
  `OvertakeInATownTests` (CAR-46, CAR-50, PER-28) and two of `LaneSwitchInATownTests` (getting past a wreck and slower
  traffic, CAR-53). They come back with the entry below.
- **`BuildingsTests.NoBuildingFrontsTheRoundingOfACarPark`** (town, GEN-55) fails: "building 139 at <1944.03, 302.92>
  fronts 8.04 m off the middle of a rank reaching 7.20 m" (2026-10-29; the test now reads the front wall's two ends,
  not the middle of a building 45 m deep). It has failed since 2026-10-15 on one reworked suite town or another.
  **Traced**: `ZoneBuildings.Along`'s `Stand` asks `Yards.Allows` at the station plus half a front along the face's
  tangent there. Building 139's station is on the rank's rounding (along −8.0 m), where the tangent points off the face,
  so the place asked stands past the walk, `SquareOnItsRank` finds no rank owning it and allows it. **The fix is
  written and held back**: asked after the seat, at the face under the wall's middle (`Face.Nearest`), as wide as the
  front reaches along the face there, the test passes — and `Towns.Built` changes enough to fail the `P0`
  `DeviceAllocationGateTests` again (68 552 B), which is the _owner_'s `LaneOccupancy` sizing entry above. It lands
  once that entry is ruled on.
- **On a GPU the kernels flush what the host keeps subnormal** (the device tier pointed at the RX 9070 XT by hand,
  2026-10-29; `Lavapipe`'s `deviceNamed`): every class passes but `RoundedKernelTests.EveryQuotientIsTheHostsToTheBit`
  (4 099 of 2 045 004 quotients) and `TrigKernelTests.EveryArctangentAndBearingIsTheHostsToTheBit` (2 658 of 4 197 298).
  Every one differing is a subnormal result the device makes nought, or an `atan2` of operands past 1e38 whose
  intermediate it flushes (−1.28e38, −2.87e38: host −2.72, device −π). RADV flushes by default and lavapipe keeps them,
  so the suite never sees it. A town's metres never come near either, but a velocity damped toward nought would,
  so a frozen body's last bits can part host from device on the GPU alone. Declaring `DenormPreserve` on the kernels
  (`SPV_KHR_float_controls`; RADV gives `shaderDenormPreserveFloat32`, lavapipe does not, and naga's WGSL has no word
  for it) closes it, or the samplers held to the range the town uses, if the owner rules subnormals out of the parity.

## Regressions and figures not taken

- **A walker look stands shorter than its `heightM`, by how far its walk reaches** (2026-10-28, `.qq/_walkers.py`
  `cut_clip`): a look's cell is its tallest frame and `pxPerM` its cell over `heightM`, so the stance a stopped walker
  is drawn in fills about 0.8–0.9 of the height its file authors, look by look. From in front a stride's forward foot
  lands lower in the picture than the stance's; `held_still` took out what a walker drifting towards the camera added,
  for all 21. Since the front is matched to the back by width (`front_by_width`) its stance stands shorter still, as a
  steeper view of the same body (0.73 for `linen_grey` and `police_a`; `tee_print` 348 px standing in a 391 px cell,
  0.89). Not the cut's alone to close: the engine draws a standing walker's quad `heightM` tall from the cell's top to
  its bottom (`PersonSprites.Body`, `DeviceSprites.PersonLooks`) and `use_walker` makes the cell `heightM` at the art
  grid. Closed by the sheet carrying how much taller than its stance its cell is, the engine drawing the quad that much
  over `heightM`, `use_walker` sizing by the stance, and the 21 cut again.
- **A walker's walk seams where its loop closes, by what its one-second shot gives** (2026-10-28, the owner: "walk is
  not looping properly"): each orbit shot holds one stride cycle (0.85–1.0 s) and a few frames, so `looped_cycle` has
  two or three starts to choose from; seams run 0.3–2.1 of a frame's change, `flannel_hat`'s front-left the worst.
  Neither a faster pace asked nor a walkway asked moved them (raw_assets/agents/person/prompting.md). Longer shots
  do: the three photo looks' 10 s clips of 2 s shots seam 0.30–0.80 in every facing. The other eighteen are still on
  5 s clips — _owner_: the 10 s allowance is theirs to extend.
- **`tee_print`'s diagonals still turn a little inside their shots** (2026-10-29, `tee_print-v10` off
  `walkorbit_tee_print-v10`, kept at 4): his torso widens 9.6 % a second through front-left and narrows 5.7 % through
  back-left — a change of angle, which no scale or shift in `held_still` takes out; `walkorbit_tee_print-v7`'s were
  −21 % and −9 %. Closed by a roll that holds the camera still within a shot, if it shows walking.
- **Nothing flags a loop that walks on one leg** (2026-10-29, `_walkers.legs_swapped`): it compares columns 2 and 6
  against the stance and reads a pause between steps as a swap, so `flannel_hat-v5`'s front, one leg's step and a cut,
  passed (raw_assets/agents/person/prompting.md). `stride_bounds` now holds the side and diagonals to the front and
  back loops' stride, but nothing checks the front and back themselves. Closed by a flag on a front or back loop in
  which one foot leads for far fewer columns than the other (the lowest paint either side of the head's vertical).
- **The 5 s looks may still walk on one leg** (2026-10-29): `flannel_hat` and `quilted_navy` did — their clips walk
  step, pause, step, a stride longer than the 1 s shot — and were made again from 10 s clips of 2 s shots, the owner's
  allowance for those two and `tee_print`. Every other look's 1 s shots hold a stride only if HeyGen walked it without the pause
  (`curly_navy` did). Closed by looking at each look's front walking, and a 10 s clip for any that fails, if the owner
  extends the allowance.
- **Only `tee_print`, `quilted_navy` and `flannel_hat` are cut with the pan held still and their diagonals sized by
  width** (2026-10-28/29, `_walkers.held_still`, `diagonal_by_widths`; `tee_print-v10`, `quilted_navy-v5`,
  `flannel_hat-v6`): the other eighteen looks were cut before both, so any whose camera panned still hops sideways at
  its seam, and their diagonals stand at whatever pitch their shot was taken. Closed by cutting
  them again from their kept variants' records and `qq sprites use`; a look whose hair widens its head past the model
  is flagged and keeps its diagonal's height.
- **The walkers' order pass has not been timed with thousands of walkers on screen** (`sprites_order.comp`, PER-31,
  2026-10-28): 3 µs for Saltbay's 195 at its whole-town framing, RX 9070 XT. It is the square of the walkers shown, so
  a town of `Twinford`'s size framed whole is where it would show. `--bench deviceworld --map Twinford`'s `order`
  figure at the `town` framing closes it.
- **`Highmoor`'s open is 157 s** (`--bench load --map Highmoor`, 2026-10-29, half the cores): the atlas alone 25.4 s and
  1.76 GiB; `Twinford`'s is 40.7 s. Whether that is the desktop map the readme calls it is the owner's _owner_.
- **Every figure taken on `Odesa`, `O10`, `O30`, `River` or the old `Test.map` is a figure not taken since**
  (2026-10-26): those maps are gone with the wheel (GEN-60), and the entries below that quote them quote towns nobody
  can open. The probes' default town is now `Saltbay` (`CheckCatalogue.ProbedTown`). Each figure taken again on
  `Saltbay`, `Twinford` or `Highmoor` closes it entry by entry; `qq tests maps` passes on all three (2026-10-26).
- **No generated town in the suite has a tributary** (2026-10-25): the suite's city (`Towns.Brief`) asks for none, so
  `GeneratorTests` and `TownGenTests` never see one. `TerrainStageTests` asks only about the water. A seed of
  `Towns.Brief` with tributaries, run through those properties, closes it.
- **A generated city's frame rate on the page is not taken past Saltbay** (2026-10-29): `qq web` stands Saltbay in
  3.1 s (320 MiB held) at 60 Hz, `Twinford` in 36.2 s (plan 19.0, town 14.3; 1.12–1.16 GiB held) and `Highmoor` in
  127.3 s (plan 72.5, town 46.7; 3.23 GiB held of the 4 a page has). Twinford's running town leaves the page's thread
  98 % idle (`--cpu-steady`), but every run that day read the compositor's 1 Hz for a window out of view, the idle ring
  as well. A `qq web --map Twinford --ui frame` with the window in view closes it; Highmoor's 3.2 GiB says whether it
  is the desktop map the readme calls it.

- **The ground pass's GPU time is not taken since the water's waves** (`WaterWaves`, 2026-10-25): three sines and three
  lumps of value noise a water pixel and one varying every ground corner, judged negligible but not measured; the browser head held 60 Hz on River
  at 40 m across. A `qq prof --gpu` on a water map, before and after, closes it.

- **A page fills four times the scenery it did at a district's framing and wider** (`GEN-6g`, `ScenerySprites.Fill`,
  2026-10-25): Odesa's scenery grew from 28 266 pieces to 104 509, past `SimConfig.View.SceneryMostDrawn` (65 536), so
  `--bench sprites` reads the fill at 1 058 µs a frame at 1 500 m across, where it was 317, and 943 µs over the whole
  town, where it was 540 — an eighth of a 120 Hz frame on the browser head, which fills the scenery on the CPU. The desktop
  hands the device runs (`ScenerySprites.Spans`) and is untouched, 43 µs at 300 m. A budget halved for a view wide enough
  that a tree is a pixel or two, or the page drawing runs as the desktop does, closes it; `qq prof` on the page, which was
  not taken, says whether it is felt.

- **The ground's looks cost up to six texture samples and the patches' field per ground fragment, not measured**
  (`ground.frag`, `town.wgsl`, `GroundPatches`, 2026-10-25): four octaves of value noise, then each look read by the
  three hexes it is scattered in — three samples where one look is whole, six where two hand over — where there was one
  sample; a fade adds a second field for its ragging, and the whole ground pass is blended for the fades
  (`GroundMesh.Fade`) where it was opaque. The browser head was seen at 120 Hz on River; `qq prof --gpu` on OdesaOsm is
  the figure that closes it.
- **The smoke's cost was not taken against itself** (`CAR-54`, `OBJ-7`, 2026-10-23): OdesaOsm's 16.6 % busy with every
  chimney and car smoking is set beside the 16–19 % the render log quotes from before any smoke, another session's. The
  over run is laid 2 × 57 instances a car, 2 × 234 a rail vehicle (a stack's longest-lived grid, laid for every wagon and
  not only a diesel) and 16 384 for the chimneys larger, and the emit walks a pipe's whole fine grid of puffs twice a
  frame — a wide pot's where a house's is 31, and a twin stack's two pots each. Not retaken since puffs vary with
  throttle and pot, nor since a twin stack smokes from both its pots, which doubled the pots smoking (OdesaOsm 69 753),
  nor since 75 more roofs' flues and cowls were marked (2026-10-24, 198 of 376 roofs hold chimneys), nor since every pot
  is laid and its fire takes turns (2026-10-24, Odesa 4 291 pots where two in five were laid). Two `qq prof --gpu`
  runs one after the other, the second with `SimConfig.Chimneys.SmokingShare` and the opacities in `SimConfig.Smoke` at
  nought, close it.
- **A car past 45 km/h strings its puffs out again** (`SmokeSprites`, 2026-10-23): the finest grid a puff is split to
  (`SimConfig.Smoke.PuffSplits`) leaves them a fifth of a metre apart there and further the faster, and at 90 km/h the
  young ones read as blots. A split of eight, at the price of twice the emit's walk, or a puff stretched along its
  velocity, closes it.
- **A rushing diesel's trail is thin** (`TRN-15`, 2026-10-23): a stack's puffs are split over its way at speed, each its
  share of the time's thickness, so at 40 km/h its 40 m trail reads fainter than its plume standing and pulling, where
  real soot behind a working diesel is darkest. A stack's puff grown wider or thicker with the speed it left at, an
  authored relation in `SimConfig.Smoke`, closes it, if the owner wants it darker.
- **`pickup_silver` and `hightopvan_white` keep their far side's lamps as drawn** (2026-10-22): `qq props mirror` left
  grey paint on the bonnet inboard of the copy-side headlamp (the copy side drawn 1.75–2 texels off the mirror image),
  so both are on their unmirrored pictures (`-v3`) with their lamps cut on them. A mirror that paints only the lamp's
  own texels, or an in-paint of the copy side's lamp before the reflection, closes it.
- **`qq props restore` cannot take back a mirror** (2026-10-22): `mirror` installs its picture, so `restore` brings
  back the props kept for the mirrored one, stray and all. Going back is `qq sprites use <car>-v<N>` on the unmirrored
  variant, then `restore` and `install`; the manual's step 7 saying so, or a `qq props unmirror`, closes it.
- **`mirror` moves the report it is judged by** (`qq props`, 2026-10-22, three agents): after it, SPARE flags and
  authored-side rim figures change though the authored shapes did not, and each copy re-placed on its own axis can
  make an undeclared overlap (`wedgesaloon_cherry`'s head and front indicator, cleared by `axis --prop=ind_front`).
  A report that reads the authored side alone after a mirror, as before it, closes it.
- **`trace` keeps a section clear of its housing below `SECTION_SHARE`** (2026-10-22): a traced amber section under
  80 % inside the brake is not declared `over` it but re-traced outside it, a sliver (`oka_red`, `citymini_green`);
  the agents grew the housing, traced, and snapped it back. The manual saying so, or `trace --over=brake`, closes it.
- **`qq props undo` steps past a step that changed nothing** (2026-10-22): a snap of zero passes, or a failed
  `trace`, writes no history, so the undo after it takes back the step before — on `offroad4x4_khaki` and
  `coupe_yellow` a good snap, redone by hand. Every step recording itself whatever it kept, or an undo that refuses
  when the last step wrote nothing, closes it.
- **Lamp readings on the thirty-five redrawn cars that are guesses** (2026-10-22, cut by agents). _owner_: a look at
  each car's `qq proof --looks=<car>` settles it; a retrace (`qq props trace`) changes one.
  - `fleet100_r04`: the amber in a red tail taken as an indicator on `liftback90s_green`, `execsaloon90s_beige`,
    `panelvan_white`, `pickup_silver` and `hightopvan_white`, as a highlight on `classic_red`, `cab_white` (the
    closest call) and `offroader_black`. `coupe_yellow` draws two boxes a corner, not three bars: the outer the brake,
    the inboard's amber-brown square the indicator, the inboard's red bar unlit (ten lenses at most).
    `panelvan_white`'s tail, past the lamp cell, is divided into `reverse`, so its inboard red lights white.
    `hightopvan_white`'s front indicator is the upper half of its headlamp housing, holding two amber glows.
  - A soft unframed amber glow inside a red tail is taken as the rear indicator on `samara_silver`, `golf3_red`,
    `budgethatch_orange` and `boxsaloon_beige`, and as a highlight on `kombiold_red` and `saloon00s_green` (smaller,
    brighter); `remove --prop=ind_rear` turns one back into the brake's half.
  - `kombiold_red`'s and `saloon00s_green`'s front indicators are a cream tip and a tan band `divide` cut off the
    headlamp; `samara_silver`'s is the whole amber-and-pale strip along its headlamp's top.
  - `estate90s_red`'s tail runs on round the corner as a red wedge `trace` cannot read with any `--lens`, left unlit;
    `kombiold_red`'s red-on-red tail was placed by hand for the same reason.
  - `loafvan_grey`'s long rear housing (past the lamp cell) is divided: the red outboard end the brake, the most
    amber segment the indicator; its head takes in the grey glass under the pale lens.
  - `budgetsuv_brown`'s rear indicator blinks over a small red wedge `divide` cut off the brake: the art draws no amber.
  - `offroad4x4_khaki`'s tail has an orange-pink patch, taken as a highlight, not a lens.
  - `citycar_lime`'s front indicator is the bright amber spot in the head strip, not the darker orange tip beyond it.
  - `oka_red`'s brake is the whole rear housing, most of it drawn amber; the red is a small corner.
  - `citymini_green`'s amber spot in the red tail is read as a highlight; `citymini_yellow`'s edged column as a lens.
  - `rearengine_sky`'s front indicator is an amber area in the headlamp, maybe a warm reflection.
  - `niva_olive`'s amber rear lens is three times its red brake: it may be an orange tail lamp.
  - `wedgesaloon_cherry` and `saloon70s_teal` draw one orange-red tail strip with no sections, though asked for them.
  - `vanbus_white`'s rear indicator is the red tip `divide` cut off its long crescent; a yellow strip beside it was
    read as a highlight.
- **`mirror` always paints the lower side from the upper** (`qq props`, 2026-10-22): the authored side is the upper
  half, and where the lower draws a lamp better — `retro500_cream`'s rear indicator on its earlier picture, orange
  below and dark red above —
  the better one is painted over. An authored side chosen per car (`"authored": "lower"` in the props file, the
  reflection the other way) closes it.
- **A variant's `handling.braking` moves nothing but the pedal's ramp** (`CarBuild.Resolve`, 2026-10-22): the brake
  is `CarBrakingMps2` (three of the road's grips) times it, and every planned stop is the lesser of that and the
  car's own grip, so across the fleet's 0.72–1.2 the tyre always decides and the figure reaches only
  `PedalRateMps3`. The plan's worn cars author weaker brakes that stop as short as new ones. Dropping the figure, or
  a brake authored as a raw term that can bind, closes it.

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

- **Laying the paint costs three times what it did on OdesaOsm** (`--bench census`, 2026-10-17: 116 → 373 ms of a
  2.8 s ground): every lane and roadside line is asked of the painted bands a crossing's depth at a time, to part it
  round any it runs under (`GroundMesh.LineRound`). Asking only the cells a line's bounds reach, or the line once per
  band in its cell, closes it.

- **An open's remaining seconds are geometry spread over many stages** (`--bench load`/`deviceload`, 2026-10-26,
  every digest unmoved since the morning): Highmoor opens in 18.2 s on the device path (44.0 before), of which plan
  ground 3.1 s (the band merge 0.9 on the main thread, `GroundRings.Of`'s two outsets 0.8 each, one a thread, the
  ground's own indexes 0.7), authored 2.1 s (the partition's serial seeding and halvings, `Connectors.Lay` 0.7),
  props 1.9 s (`PropStage.Clumps` 1.0 on one thread), car parks 1.2 s, buildings 0.9 s, the world's pavement lanes
  1.3 s (`PavementLanes.Of`, two outsets of the whole shell, one a thread) and pavement 1.3 s; the mesh's 5 s is laid
  beside the world. No stage is over 15 % of the open. `ArcOutset.Struck` is four of those seconds on the critical
  path, each serial, and what it spends is `Trig.SinCos` and `ArcRings.Place`'s nine weld cells an end read out of a
  table of millions — memory, not arithmetic. `Trig.SinCos` (10 % of self time) is not the cached heading Step 7 of
  `MapLoad-Plan.md` assumed — `ArcSeg.StartUnit` already is — but the angles a sample turns through
  (`Spline.NearestOnFlat`'s Newton steps, `ChainIndex.Builder.CellsOf`), which a cheaper form would move the digest
  for. Closing more takes an outset cut into pieces worked apart with the same answer, which nothing yet states. The
  page pays all of it at twice a desktop core's time on one thread.
- **The car-park book's exactness is held by the digests and no test** (2026-10-26): `CarParks.SiteBook` keeps a
  refusal per site and side's rank and a ranking per draw, and lays the car parks it laid before only because a
  refusal is never undone in the stage. `--bench load`'s plan digest, equal on Saltbay, Twinford, Highmoor and
  OdesaOsm, is what showed it; a test stating it would be the derivation written out twice (VER-12). A change to
  what `CarParks.At` refuses on — anything that can make a refused site clear again — reopens it.
- **Highmoor asks 18 762 car parks and lays 10 797** (`--bench load`, 2026-10-26, _owner_): the book runs dry with
  42 % of the count unlaid, the sites refused at a lead node (`Dry`/`StandsClear`) or at the rank's middle. Whether
  the count, the sites or the refusal is what should give is what the town gets, not speed. Taken on the brief's 72 000
  buildings; not taken since it plans 197 000 (2026-10-27).

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
- **Walkers going opposite ways down one footway hold each other for a minute and more** (2026-10-27, Saltbay,
  `--bench stuck --seconds 180`): walkers 178, 189 and 133 on footway way 7086 at (1874, 426), and 114 on way 7065 at
  (1937, 450), each granted nothing, cut by the body of the next a metre or two along the same way — 178 and 189 each by
  the other. Nothing on the road side of the town; whether it predates the run's tree is not known. A sidestep asked
  between two walkers facing on one way (PER-28), or why none was, closes it.
- **An owner can be kept at the wheel for over a minute in the suite's built town** (person log, 2026-10-02): two
  owners stayed seated because every exit spot near the door was within their own car's panels or held by someone else.
  The door point is a person's breadth off a nominal car's flank, which a wider car reaches past. Checking that the
  spots past it lie on the next bay's way closes it.

## Redundant work left in place

### The ground

- **A bridge on the ground fills its whole deck with the deck surface and then covers all of it** (2026-10-25,
  `GroundMesh.Decks`): the walkways, the carriageway and the parapets are laid over the fill edge to edge, so the deck
  surface (`bridge.webp`) shows only where a parapet's picture is clear — nowhere, with the iron parapet in use — and
  on the level above's junction decks. A few hundred triangles a bridge and one surface loaded for little; laying the
  fill only where no strip is, or dropping it once a parapet with gaps is ruled out, closes it.

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
    (`Game.Desktop`'s `OpenWhatWasPicked`): it is laid off the loop's thread, which may not submit to the device, so it
    cannot have its world made as it is laid (`IWorldMaker`). The open's peak there is the 2.5 GB the command line's
    open no longer pays (1.55 GB, `--bench deviceload`). Making the maker's device work wait for the loop's thread
    closes it; the exam map (`Scenarios.Stages`) is handed over the same way, and is small;
  - the nearest-point indexes kept for a player's order and the camera's opening alone (`RoadGraph`'s street indexes,
    43 MB; `FootGraph`'s, 33 MB; `ParkingRegistry`'s bay grid), which the 2026-10-13 ruling lets a player use — laid
    again on the first order rather than kept closes it;
  - the marks in metres (`WayCrossings`, 31 MB) beside the claims' own in decimetres, for `CrossesAZebra`,
    `Overtaking` and `Closure`;
  - the host's tyre-marks ring (`DriftMarks`, 3 MB), which the device world never fills.
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
- **OdesaOsm is not measured on the turns** (2026-10-14): the owner asked for small maps while resources are short, so
  the root log's figures are O10's and Odesa's, and its OdesaOsm figures are of trees before. A `--bench room` and
  `--bench drive` on OdesaOsm close it.
- **A light's hold is placed as its first body stood at its turn** (`SignalHolds.Lay`): a body standing over the bar
  then leaves the light holding nothing, and the ground it pulls off the bar is held by nothing until the light's next
  turn — a follower whose turn falls between can plan over the bar. Nothing has been seen to come of it; a
  `--bench drive` red traced to the turn it was committed in closes it or names it.
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
    third bullet);
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

Every file of `src/` was read against .NET 11 practice and performance; what was
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
  shot); the web window works `UiScale` out every pointer read where the desktop keeps it a pump.
- **Setup and the open**: `GlyphStamp.Blend` goes through the image's indexer a pixel; `KerbLines.Of` copies its pieces three times and keeps
  two; `ShellFill.Ears` allocates a `Node` a corner; `Spline.RoundedInto` a `float[]` a road; `TownLayout.StandsClear`
  a delegate a call; `ArcRings.Of`'s stringing is one thread; `TownWorld.Places.LayTheExits` stays serial until
  `StaticBodies.InBox` is laid before it rather than on its first ask; `Roundabouts`/`TownLayout.ArmsAt` scan every edge a node (~40 ms on O30).
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

- **Whether a point is inside a ring is written seven times** (2026-10-26): `Polygons.Encloses` in core, which the
  generator's rework added, and private copies in `ShellFill`, `TracedCarParks.Bays`, `ZoneTree` (filed
  by row, so not a plain swap), `TownMap.Stumps`, `WaterOutline.Inside` and the scanner's `ZoneHints` (which compiles
  without the engine). A copy's arithmetic order may differ, so a swap in the engine is checked against the digests. Every copy in the engine reading
  `Polygons.Encloses` closes it.
- **`ZoneKind.Wheel` and the wheel's zone settings are kept for a map nobody lays** (2026-10-26): no generated town
  is a wheel since GEN-60, and `OdesaOsm.map` is the only map file left. Reading whether `OdesaOsm.map` holds any, and
  retiring the kind with explicit numbers kept for the file format, closes it.
- **`--bench` probes that run every shipped map by default now take `Highmoor`** (`DriveProbe`, `TownProbe`, 2026-10-26):
  53 s and 4 GB to lay before the probe starts. Naming the probed maps, or leaving the largest out by default, closes it.

- **One union-find root, three copies** (`FootJunctions`, `CrossingWays`, `LanePlaces`) — only the root: each join
  keeps a different representative (the first, the lower, the other), which the order of what they lay rests on, and
  no folder of `core/` is a home for four lines; **three `Spread`/`Percentile`/`Median`** in the bench
  (`TownShape`, `ConnectorShape`, `ZoneFidelity`), which print differently, so one copy moves probe output.
- **Kept for nothing a run reaches**: `ArcSubtract` (295 lines) and its tests, kept "as tooling" by the render log of
  2026-09-16 though no tool calls it _owner_; `CityPlan.ZebraAtEveryStation` is false only in `CrossingsTests`, so no
  map lays the `Crosswalks` path.
- **Read by the tests alone, and not plainly a seam** (the 2026-10-16 sweep of what `qq unused` lists as test-only):
  `ConnectionPoints`' `ConnectionPoint`, both `At`s, `Point`, `ArmOf(GroundPieces…)` and `MostPerArm` work out where
  lanes end a second time, beside `RoadLines`, as `ConnectionPointTests`' yardstick — whether that may live in `src/`
  is VER-12's question; `TownMap`'s water kind
  `Lake` is written by nothing (whether a shipped `.map` holds it decides); `CarFigures.LongestLengthM` is authored and
  read only by two tests, where GEN-53 could be asked of the catalogue directly; `SpriteInstance.AsDrawn` repeats
  `Halves`' cast.
- **The instruments miss two kinds of stale name**: `qq unused` counts by spelling, so an overload sharing a live name
  and a chain of members only calling each other read as used; `qq doclint` resolves crefs and IDs but not a
  backticked `Type.Member` in prose, so a renamed member's old name lives on in a requirement until someone reads it.
- **More read by the tests alone** (the 2026-10-25 sweep): `LaneOccupancy.CanKeep`, `PhysicsWorld.AddStaticDisc`,
  `ChimneySprites.FireOf`, `GroundPatches.ShoreRow`, `DrawnFan.RimReachM`, `SimConfig.CarAccelerationMps2` and
  `SimConfig.PavementCornerReachM`, and `KerbEnds.PaintedAcross`, whose two halves `bench/ZebraShape.cs` asks apart.
  Production reading the one copy, or the test asking the production path, closes each. The device readbacks (`DeviceSolver`'s `LeftOver`, `Contacts`, `ContactNormals`,
  `Woke`, `Froze`; `DeviceTyres`' `CarsAfter`, `WheelsAfter`, `Impulses`, `MarksLaid`) and `Menu`'s `*MiddlePx` are
  seams and stay.

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
- **Assertions that are not tests (VER-12)** (2026-10-25): `ATownThatMovesTests.cs:82` asserts only `moved > 0` after a
  minute; `RoadGraphTests.cs:42-50` asserts indices in range; `GroundLocatorTests.cs:373` an enum value below
  `Kinds`; `SimConfigTests.cs:40-47` writes the pace-scale derivation out twice, which its own remarks call no test
  (`P0` class: _owner_); standalone `> 0` counts over a run in `OwnCarsInATownTests:104,112`,
  `JunctionClaimTests:222` and `WalkingTests:150,207,264,367,773`. Each restated as the behaviour it stands for, or
  deleted, closes it.
- **`tests/` does not mirror `src/` folder for folder** _owner_: `tests/physics/` and `tests/routing/` stand at the top
  while `tests/world/{foot,parking,rail,statics}` are nested; `tests/world/` holds road, terrain and town tests flat;
  `routing/DrivingNetworkTests` tests `world/road/`; `citygen/TownGenTests` and `ExamPlanTests` belong under `gen/`
  and `exam/`; `hud/InterfaceScaleTests` tests `runtime/` (beside `PointerSpaceTests` on purpose, its remarks say).
  A layout chosen and moved to closes it.
- **Test helpers written several times**: `Advance(PhysicsWorld, n)` four times, a bit-exact `Same` four times (the
  kernel tests), `Cross` five, `Straight` and `Square` in geometry and again in `ZoneTreeTests`/`TownMapTests`, the
  status panel's `Fill` twice, and `new TownWorld(Towns.Of(Towns.Fixture), Config)` twenty-nine times; and two classes
  named `Claims` (`tests/bench/`, `tests/world/`). One shared helper each closes it.
- **No test asserts a scenario map's watch keeps its claims** _owner_: `Claims.AssertKept` was read by nothing and is
  gone, while CLAUDE.md says the tier reads a new claim from the map's own watch. `qq tests exam` asserts the exam's
  cards; whether the tier should assert the watch too is the question.

## Inconsistencies

- **`blouse_bun` may have its north diagonals swapped** (2026-10-28, raw_assets/agents/person/prompting.md): its
  up-left row reads as facing up-right by eye, as `recovery_a`'s and `recovery_c`'s did before they were cut again with
  `--mirror 3`; a silhouette comparison against the side rows does not settle it. Looking at its
  `_walk_1_back-left.gif` from `qq sprites cut <frame> --as blouse_bun`, and cutting it again with `--mirror 3` if it
  walks up-right, closes it.
- **`curly_navy`'s back-left is near a profile** (2026-10-28, `walkorbit_curly_navy`): its three rolls are used — v3
  drew back-left as a second profile — so it stays on v2's clip. A fourth roll with the back-left cue now in `ORBIT`,
  which the owner has to allow past the three-attempt cap, closes it.
- **A walker on the showroom cannot be ordered to walk** (2026-10-28, PER-30): the people's row stands on no way of a
  network, so an order (CTL-8) leaves it "under orders" at nought — a walk look is judged off its cut's previews, not
  on the showroom. A way laid across the island, or the town walking a hand's order off the network, closes it.
- **The showroom shows no ground surface, kerb or signal head on its own** (2026-10-28, `ShowroomPlan`, _owner_): it
  stands every car, person, train, building and prop look once, and the rule that every asset change is looked at
  there (CLAUDE.md, raw_assets/index.md rule 8) covers those; a pavement texture or a kerb look is judged in a town
  that happens to lay it, and the showroom's own road shows one of each. The owner saying whether the showroom should
  lay a patch of every surface and kerb look too closes it.
- **A walker's drawn step is about half the step it is moved by** (2026-10-27, `PersonSprites`): the walk cycle is
  stepped one cycle per `heightM` walked — two steps of some 0.85 m — while the keyed pose guide draws steps of about
  0.45 m, so the feet slide forward under the body. A stride a figure of the walker's own (a share of its height, on
  `SimConfig`) that the guide is drawn from and the column is stepped by closes it.
- **The remotegen skill's car style picture cannot be laid** (2026-10-27): `crossover_pearl`, `estate_steel` and
  `kombi_sage` with `--style-whole` fail ("has no used variant cut from a cell of a frame"); without it they lay. The
  skill naming the ids and flag that work closes it.

- **A slip road refused as it is laid leaves its access one way** (2026-10-27, GEN-68): the plan lays both an access's
  slip roads or neither, but the layout may still refuse one (GEN-13, GEN-49) — laid before the roads between districts,
  none of the three briefs loses one now (`--brief`: 8, 72 and 152 slip roads, four to an interchange that crosses and
  two to an access). A town where one is refused is one way off or on there, which `ACityCanBeDrivenRound` names.
  Laying an interchange's roads together and taking them all back where any is refused closes it.
- **A span's bend has two answers** (2026-10-27, GEN-14a): the layout reads it off the road's own curvature
  (`LayoutEdge.Curvature`), the plan off the deck road being laid as one arc (`ConnectionPoints.SpanCurvature`), the
  plan carrying neither a road's class nor its layout curvature. A deck road laid as one curved arc that is no span would
  have its arms drawn on a bearing the layout never drew. None is laid so today; the plan carrying which roads are spans
  closes it.
- **A piece of a town met by intercity roads only can lose its streets** (2026-10-27, GEN-68, GEN-69, `--brief`):
  `Saltbay` builds streets in 5 of its 6 districts and `Twinford` in 72 of 74 since the town's roads meet them only at
  interchanges, `Highmoor` 238 of 239; the district a road between districts was refused at a terminal is cut
  off and deleted with its piece (`TownLayout.KeepTheLargestComponent`). Which districts, and why their road in was
  refused, is untraced; tracing it closes it.
- **A slip road joins its intercity road at the node, with no lane to gather speed in** (2026-10-27, GEN-68, TER-5k): the
  node is no junction now and the merge is the claims' at one rung, so neither stream always gives way — but whichever
  asked second still eases off or stops short of where the two converge, and the kerb lane's traffic behind brakes or
  moves across (CAR-53). No probe has watched a merge under load: Saltbay's 180 s stuck run gave up no leg, and how many
  cars came through its slips in it is uncounted. An acceleration lane alongside for a stretch — a third lane, past
  `LayoutEdge.MostLanes`, ending on a lane change rather than a merge — closes it.
- **A generated city that fills its map opens several times slower** (2026-10-27, GEN-76, `--bench load`, _owner_):
  laid edge to edge, `Highmoor` is 84 220 roads and 193 051 buildings and opens in 151 s managed (plan 68, world 73 —
  foot 30 % of it — ground 10), `Twinford` 23 510 roads and 52 028 buildings in 38 s, where the roster-sized towns
  opened in 18.2 s and 5.5 s on the device path. `MapLoad-Plan.md`'s targets were taken on the smaller towns and are
  no longer met. Smaller briefs (the owner chose the map's extent), sparser edges (`TownSparseBlockShare` toward one,
  `TownEdgeFill` down), or load work on the stages that scale with roads — the foot network and the atlas first —
  closes it. Not taken again since the intercity roads passed the town edge to edge (`Highmoor` 75 440 roads, `Twinford`
  21 058, both fewer).
- **`Saltbay` lays one of its two roads passing the town** (2026-10-27, GEN-63, `--brief Saltbay`): "intercity 1 of 2
  passing the town". The second's every try is refused; tracing which step refuses it — the land's edge, the touch, the
  route or the curve — closes it.
- **A large district's lattice leaves much of it bare** (2026-10-27, GEN-72): on `Saltbay` the districts are 0.5–0.9
  km² and their streets stand in a part of each, the rest grass inside the district's own zone (`--ui zones`) — the
  more visible since the edge's blocks were widened. Why the lattice stops short of the district's edge, beyond the
  waterfront's clearance (GEN-71), is untraced; tracing it, or districts sized to the map, closes it.
- **A park, a farm and the wild are drawn as the grass round them** (2026-10-27, GEN-76, GEN-77): each zone differs only
  in how thickly the props grow (`ZoneDefaults.Growth`), so a park block reads as an empty block and the country as an
  open field. A ground of each kind's own, or props a kind is planted with, closes it.
- **The generator indexes the map off the town's grid** (2026-10-26, SIM-8): `CellGrid` (the partition's and the
  routes' rasters), `Partition.Middles`, `SegmentCells` and `ZoneTree` (250 m) each file the map on squares of their
  own, where SIM-8 has every index over the map on `WorldGrid`. A coarse level for the grid — it has only levels
  under the main cell, a few metres — or a ruling that a stage's own working index is no index over the map, closes it.
- **Two of a town's railways cross where the first carries a loop** (2026-10-26, GEN-63, RAIL-11): a looped line's way
  out leaves on its bearing turned by the loop, so every line inland of a coast crosses it at grade, and
  `Connectors.Railways` lays the first dry draw where none keeps `RailLinesApartLeastM`. No shipped brief loops since
  2026-10-27 (the owner: "the loop was a test and can be dropped"), but a brief may still ask for one, and
  `RailLoopTests` asks it of the suite's. Taking RAIL-11 out with its tests, or turning the loop's way out back onto
  the bearing, closes it.
- **A narrow district lays no street** (2026-10-26, GEN-65, GEN-72): a district narrower across its own axis than the
  block it drew (its kind's aspect times the spacing along, up to 414 m an industrial one) holds one row of lattice points or none, so
  roads are planned into it and no street stands to meet them — `Saltbay`'s 5, 6 and 8 (0.17, 0.50 and 0.20 km²),
  `Twinford`'s 5 and 6 at the top edge, `Highmoor`'s 111, 116, 123 and 239; and a bridge or a link into such a
  district leads nowhere and is pruned (the suite's seed 7, laid with the connectors on streams of their own, kept no
  bridge). **Holding a district's blocks to three rows across its narrow way was tried and taken out**: it laid streets
  in all but one district of the three cities, and its shorter blocks laid the corner the next entry is about. Holding
  the blocks to the district once that corner is no junction a car is trapped at closes both. **Since 2026-10-27 a
  district too narrow for two of its blocks' long sides lays them the other way round** (`Districts.Plan`), which laid
  streets in every district but one of the three cities (`Highmoor`'s 61); one narrower than two short sides still lays
  none.
- **A two-arm junction can stand at a corner no car can turn** (2026-10-26, GEN-51, TER-5a): where two short pieces of
  a run meet at a right angle, the join is refused for its corner (it cannot round it at a junction's floor on so little
  length) and the run is cut there; the junction that stands has a standoff a few metres out, its inner lane's turn is
  tighter than `JunctionCorneringRadiusM`, and `LaneLines.LanesDrivenOn` counts it no way on. Seen on `Twinford` laid
  with the blocks held to three rows: "7968 of 7990 lanes cannot be driven to junction 315". Asking at the cut whether
  the junction left can be turned — the standoff times the tangent of half the angle between the arms, less half a lane
  — was tried and did not catch that node, so the junction's own turn is not that simple; reading it off the lane
  builder at the cut closes it.
- **Two edges between districts meeting at a corner are never asked GEN-13 of each other** (2026-10-26, GEN-69):
  `TownLinks.Seams` plans every shared edge to its corner, the paths share the corner's node, and where two meet there
  under `ArmsApartMinRad` the second's first piece is refused at every node after it (`Paths.ClosedFrom`), so the
  districts either side of it reach each other only over an intercity road. Seen on the suite's seed 1 laid with the
  blocks held to three rows ("district 0's streets reach district 2's only over an intercity road"). Planning an edge
  that meets another under the bound to end on it a junction's spacing along (`Lands`), as a link does, closes it.
- **`Highmoor` laid with the connectors on streams of their own leaves 1 975 m of boundary open** (2026-10-26, TER-7):
  "left 1 runs of boundary open … from <5920.4, 4735.8>", nothing visibly wrong at its start. Not traced; it is the
  ground's boundary meeting some shape that seed lays, as `ItsBoundaryCloses` caught on the earlier maps.
- **`TerrainStage` builds every water's rings twice** (2026-10-26): `Lay` draws each course's rings to ask whether it
  fits, joins a tributary's to its parent's to ask whether they join, and throws both away; `Rings` draws and joins them
  all again off the arrays. Keeping the joined rings from `Lay` closes it, at no measured cost today (terrain is
  0.03 s of `Highmoor`'s 27 s).
- **A tributary is laid by its river's figures and nothing of its own** (2026-10-26): `TributaryWanderShare` went with
  the wheel, unread, and a tributary now wanders by its brief's river meander off its own run. Whether a stream needs a
  wander of its own again is the next look at a tributary's shape.

- **A double-track rail bridge's piers are placed along each track on its own** (2026-10-25, `RAIL-12`): each span
  pitches its piers by its own length, and the inner ones are dropped as standing on the other's deck, so the two
  outer rows need not stand opposite each other. No shipped map has a double track on a bridge; pitching both from one
  span's line closes it.

- **No town has stood a car on other than two axles of two** (2026-10-25): a variant may lay out its own wheels
  (`wheels`, CAR-57) and the unit and device tiers hold a three-axle body to the model and to the host
  (`Layouts.SixWheels`), but no shipped variant lays any out, so no town tier, scenario or drawn frame has seen one —
  nor a tow of one, whose axle down is `TowBar.AxleOnTheGround`'s guess at the far end from the fork. A shipped
  multi-axle look and a `--shot` of it, laden and towed, closes it.
- **Every per-wheel row keeps room for eight wheels a car** (2026-10-25): the tyre table, its impulses and scrub on the
  device and `TyreCarries`/`WheelScratch` on the host stride `TyreModel.MostWheels`, twice what the fleet's four-wheeled
  cars use. Nothing reads the room past a car's own wheels, so it costs memory and not time in a town — 64 B a car on
  the device — but a pass that hands its rows back reads twice the bytes (0.36 → 0.7 ms over 20 000 cars on lavapipe).
  The tyre pass itself costs a fifth to a third more there than before CAR-57 (1.8–2.1 → 2.3–2.5 ms over 20 000 driven
  cars), its loads and arrays sized for eight; a real GPU's town is one dispatch either way. Striding by the
  catalogue's most wheels, a figure the kernels are handed, closes the room; sizing their arrays the same needs a
  specialisation constant WGSL may not give.
- **A car slowing for a corner can still swap between its pedals** (2026-10-29): the stops no longer hunt
  (`CarFollower.StopMps`, the car slice's decision log), but over a minute under 3 m/s Laid still swaps throttle and
  brake 26 times (84 before) — 10 under a `Corner` hold, 8 `Claimed`, 7 `Reach`, 1 `Waiting` — and Test 3 (15), all
  `Corner`; each swap pitches the body (CAR-56). The corner approach in `CarFollower.TargetSpeedMps` still reads its
  reaction lead and its lookahead at the speed the car is doing, the shape the stops had. Solving it for the speed on
  its own profile, as `StopMps` does, closes the corners' share; the `Claimed` and `Reach` swaps are untraced and may be
  holds changing under the car.
- **A page weighs three times what WEB-6 allows** _owner_ (2026-10-25): WEB-6 promises 6–8 MB before the first frame
  with "2.8 MB of art", and WEB-9's table and `town.js` say about three megabytes, but `assets.tar.gz` is 21.0 MB in a
  Release publish. WEB-6 also counts the art before the first frame where WEB-4 fetches it after. Re-measuring a
  publish and restating the budget, or holding the art to it, closes it.
- **`CarFigures.MaxSteeringDeg = 35.42` carries no doc, and its precision reads as back-solved from a turning circle**
  _owner_ — which "figures flow one way" forbids. An authored road-wheel angle, its source named, closes it.
- **Most rail looks are in no list** (`assets/world/rail/looks/`): 33 look folders, 11 named by `Railway.json` — the
  `_b`/`_c` alternatives never chosen or dropped _owner_. Choosing, and deleting what is
  not chosen, closes it.
- **A building's `tags` repeats its `describe` and nothing reads it** _owner_ (`BuildingVariantFile.Tags`, 365 files):
  dropping the field from the type and the files, or naming what it is kept for, closes it.

- **A page draws no clouds' shadows** (`OBJ-8`, 2026-10-24): the Vulkan heads draw them (`CloudShadows`, `cloud.vert`,
  `cloud.frag`) and the browser's `town.wgsl` has no pass for them, so the same town at the same tick has a sky on the
  desktop and none in a browser. A page's camera block already carries `CameraView.Clouds`; a WGSL triangle and its
  pipeline in `town.js`, drawn where `TownRenderer.Recording.cs` draws it, closes it.
- **The clouds' field is stated twice** (`OBJ-8`, 2026-10-24): `CloudShadows`' `FieldMean`, `FieldSpread` and
  `FieldSlope` were measured off `cloud.frag`'s noise and are not re-derived from it, so a change to its octaves moves
  the cover and the edge the figures ask for and nothing fails. A device-tier test reading the shaded share of a frame
  against `CloudFigures.CoverShare` closes it.

- **Some drawn fans stand still** (found 2026-10-23, `OBJ-6`): the catalogue's fans were cut with `qq props seed` and
  looked over roof by roof, and these were left out. A roof that draws one fan and not its twin looks half-built only
  where both are cut, so each is all or nothing.
  - The bladeless brown twin units on `industrial_*`, `retail_03`, `retail_05`, `retail_06`, `retail_09`,
    `retail_deep_02` and `school_deep_02`: each worth cutting, a fan being drawn in a look of its own and not turned
    off its picture.
  - Fans drawn too small to separate: `warehouse_saw`'s pair, and `hospital_deep_04`'s four upright units.
  - `retail_deep_06`'s second unit, whose right fan is drawn grey as a dome is.
  - `retail_deep_07`'s smaller second fan on four of its seven units, which now stands beside its twin's look.

  Cutting each by hand (`qq props trace … --at=X,Y`), or a sheet drawn again with blades, closes it.
- **A sliver of a roof's own fan shows past its look** (found 2026-10-24, `OBJ-6`, `FanTable.RimOutsetPx`): the look is
  laid out to the ellipse fitted through the art's rim and a texel and a half past it, and where the art's opening
  wanders wider — one side of `retail_deep_07`'s units at a close framing — a dark crescent of it shows. A wider outset
  closes it at the cost of covering the housing round the tidy ones; a rim fitted to the opening's widest reach instead
  closes it outright.
- **Four fans' looks are named for a blade count they do not draw** (found 2026-10-24): `fan_six_grey` draws eight,
  `fan_five_curved` six, `fan_seven_sickle` eight, `fan_four_rust` five. Nothing reads the name for it; renaming them by
  their blades' shape, or asking them again, closes it.
- **The shipped lamp sheet can disagree with the catalogue's lenses, and nothing fails** (found 2026-10-23,
  `assets/agents/car/variants/common/lamp_atlas.webp`): it is cut only by `dotnet run -- --lamps`, which `qq props
  install` does not run, so lenses reinstalled under another fitting keep burning their old colours in the game. The
  ambulances' bars, repainted blue, burned red until the sheet was cut again. `qq proof --looks` bakes a sheet of its own
  and cannot show it, and no test compares the shipped sheet with a fresh bake. `qq beacons swap` cuts it; a unit test
  baking the sheet and comparing it, or `qq props install` cutting it, closes it.
- **A district may stand no depot** (2026-10-20, GEN-55, GEN-56; accepted by the owner): a yard is as wide as its
  building's frontage (`CarParks.YardBays`), eight bays for the 23.4 m civic roofs, and one district of `Odesa` finds no
  kerb straight for that long (depots 7 of 8 in `--bench census`). A site search that holds a long yard on a curving
  kerb, or a shallower yard, closes it.
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
  long; which of the three moved it is not traced, and OdesaOsm is set aside for now (_owner_). Where the traced main
  line runs beside Балківська вулиця (about 1275, 12600) the street's imagery width (12–13 m against its tags' lanes)
  puts its east walk 1.8 m off the track's centre, and a traced town's walks are never asked of the ballast, as a
  generated town's nodes are (`WaterRules.Holds`). The walk over the ballast only where a crossing's stretch holds it,
  trimming a walk where it runs onto railway ground, or narrowing a road to its tags there, closes it.
- **The solver's contact arrays grow until the fixture's second minute** (2026-10-19, `Towns.SettleTicks`): 40 KB on
  the world's thread at 68 s, with a 19-vehicle train out over the loop, so the allocation gate warms the fixture for
  two minutes. The arrays start empty and double (`PhysicsWorld.RoomForContact`); laid for the town's bodies — its
  wagons' couplings among them — they would not grow, and the warm-up could come back to a minute. The figure is of
  the looped fixture before it was laid off a brief (2026-10-26), and so is `Towns.SettleTicks`' remark explaining it.
- **A crossing's cabinet, signs and spare lights are drawn nowhere** (2026-10-19): `relay_cabinet`, `crossing_cross_b`,
  `crossing_countdown` and `crossing_countdown_b` have kept variants and no sprite; `relay_cabinet_b`, `crossing_cross`,
  `crossing_warning` and `crossing_warning_b` have none (`railway_gear_a`, `railway_signs_a` drew them past `cut`'s fit).
  The upright head `crossing_head` and the X lights `crossing_light_b` to `_e` with their lit twins are installed and
  worn by no crossing (`crossing_head_b` and `_c` have no picture), which wears `crossing_light`. Placing a cabinet at each crossing and its signs on its
  approaches, or deleting what no crossing will wear, closes it.
- **A car meeting a crossing's arm allocates** (2026-10-19, `RAIL-10`): with arms that met cars inside its window,
  `AWholeTickWithATrainCrossingTheTownAllocatesNothing` read some 40 KB over 600 ticks, and none with the arms on a layer
  meeting nothing; it passes because no car meets an arm since lanes stay held until the arm is all the way up — but
  failed once by 120 bytes in a `qq tests --changed` run on 2026-10-24 and passed run alone straight after. Where
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
- **The unit panel's train and wagon rows, the train's path and its ring have no test** (2026-10-19, `CTL-1`,
  `CTL-1a`): `UnitReadoutTests`, `SelectionPathTests` and `SpriteRingsTests` stage cars only (the fixture stands no
  walker). A case each on a town with a railway and one with a walker closes it; the ring round a train has not been
  seen either, as neither of Saltbay's trains is on the map when a drive starts. A walker's ring is held to the
  device's by `--bench deviceworld`, which picks out everything it frames.
- **The selection's ring in the web head is unseen** (2026-10-28, `CTL-1`): `town.wgsl`'s `spriteVertex` and
  `spriteFragment` draw it as `quad.glsl`/`sprite.frag` do, and naga parses the module, but no shot of a page has had a
  unit selected. `qq web --shot` with a unit selected (`--click`) closes it.
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
- **The lamp sheet is decoded twice when the town loads** (2026-10-22, `LampSprites.Glow`): once to blur the glow
  sheet out of it and again when its page is filled, about 17 MB held for the first. Load time only — 50 ms on the
  host for the 111-look fleet, not measured on the web head; blurring from the page's own decode closes it.
- **`trace` refuses a lens past 30 texels where the lamp cell takes 56** (2026-10-23, the ambulances; `REACH_PX`), so a
  long brake housing is placed by hand — though the same reach is what stops a red-on-red trace running on, so it is
  raised only with a stop of another kind. The tow trucks (2026-10-23) add two: `trace`'s default tint mode fails on
  dark amber bar glass, which `--lens colour` or `frame` reads; and
  `remove --prop=reverse` on a seeded reverse makes it a part of the brake again rather than taking it out, so five
  agents deleted it from the props file by hand.
- **No map stages a tow to be looked at** (2026-10-23): the new arms were judged on a still composed by hand from
  `FillBeams`' placement, not on a frame the engine drew, since no lab map or exam card
  stands an evacuator towing. A showroom step, or an exam card, that hitches one closes it.
- **Three ambulances' wrecks carry a longer bar than the car** (2026-10-23): `ambulance_white`, `ambulancelight_white`
  and `ambulancebig_white` were drawn wrecked with a `podbig` bar, then refitted `podlong` because `podbig`'s ends
  traced past the 56-texel cell on a van that wide. The wreck's bar is about a quarter longer; seen only on a wreck.
  Their wrecks asked again from the refitted cars closes it.
- **`policehybrid_white`'s brake strip outruns even the 56-texel lamp cell** (2026-10-23): it runs x 17–74, 57 texels,
  so its last 3 (x 72–74) stay dark; the car's two spare lenses are its beacons, so no second brake prop fits.
  Trimming the art's strip, or a lens allowed past one cell, closes it.
- **Small lamp-cut leftovers of the 2026-10-23 re-cut**: `policesaloon_white`'s and `vanbus_white`'s brake tips stand
  about a texel past the rim; the edges once cut to the old cell are still marked held on six brakes, so `snap` passes
  them over — placed by hand. `qq props` has no action for a prop's `hold`, `over` or own `axisY`/`mirrorDx`, so they
  are hand-edited in the props file; an action for each closes it.
- **`qq props audit` flags what is not a lens** (2026-10-23): SHORT takes body paint, a red lens's blend into paint,
  or chrome beside a head for lens colour, and ON THE FRAME takes a dark streak drawn inside the glass for frame.
  Standing false: `oka_red` (three), `budgetsuv_brown`, `citycar_lime`, `hybrid_white`, `kombiold_red`,
  `loafvan_grey` (body shading past the brake tip), `compactsaloon_white` (a streak in the amber glass). Costs a look
  at each on every audit; reading paint off the car's common colours (`LampAtlasBake.Common`) and a frame only at a
  lens's rim closes it.
- **Lamps the art draws past a car's ten lenses stay dark** (2026-10-22 review, `CarLamps.MostLenses`): a red marker
  at `lightvan_white`'s rear corner, a second red lens in `coupe_yellow`'s rear housing, a red strip along
  `luxsuv_black`'s upper rear, an amber spot inside `policevan_white`'s headlamp, and an amber tip on
  `citycar_lime`'s headlamp that lights white with it, and the red arm along `cityhatch_silver`'s tailgate beside its
  corner lamp (`install` writes a lens a side of every prop and part, five lamps a side). _owner_: more lenses a look —
  three bits a lens in one word of the sprite emit, so a second word — or these merged into a neighbour.
- **A few far-side lamps are drawn unlike the near side's** (2026-10-22 review): the copy outline sits a texel or
  more off, over paint or a divider — `longestate_green` `ind_rear` (4 texels of paint), `samara_silver` `ind_front`,
  `crossover_pearl` `ind_rear`, `bigsaloon90s_white` `ind_front`; `hightopvan_white`'s `head` and `ind_front` copies
  overlap by 25 texels; `minihatch_green`'s `ind_front` is cut twice. `qq props mirror` on each closes it.
- **Some lamps burn a light their glass is not painted in** (2026-10-22 review): amber glows with no drawn edge inside
  red clusters are cut as `ind_rear` sections (`budgethatch_orange`, `cityhatch_silver`, `compactsuv_green`,
  `midsaloon_black`, `hybrid_white`, `evsaloon_red`, `premsaloon_white`, `bigsaloon90s_white`, `cab_white`,
  `hatch_graphite`, `pickup_silver`, `mpv_silver`) — _owner_: indicator sections or highlights. And art that has no
  lens for a light: `saloon70s_teal`'s rear cluster is all amber, so it brakes red over amber;
  `bigsaloon70s_black`'s `reverse` sits on an amber section; `compactsuv_black`, `budgetsuv_brown` and `evsuv_grey`
  blink `ind_rear` over red glass; `family90s_silver`'s pale glass at the outer tip of its headlamp housing is no
  lamp's. Redrawn art, or the owner's word that they stand, closes it.
- **`qq beacons fit` never sees a windscreen's black frame** (2026-10-22): it reads the glass (bluish, or `--glass=neutral`
  on a light roof), so on a black roof (`policecrossover_white`) the big bar's ends sat on the frame and the car was given the long
  bar instead. Taking the frame in where it is darker than the roof beside it closes it.
- **Without its glow a lit brake, indicator or reversing lens barely differs from its dull glass** (2026-10-20,
  `qq proof --no-glow`, `cars_fleet_a`): the bake carries a lens up its colour's ramp by the art's own luminance
  (`LampAtlasBake.Lit`), and the generator's bright red and orange glass is already near the top of it, so braking
  moves a texel by at most 125 of 255 and reversing by 74; only the headlamp reads lit. The glow is what says it in
  the game. A ramp whose floor stands clear of the dull glass, or a check of the lit lens against the dull one,
  closes it.
- **What the batch 1 lamp cuts found wrong in `qq props`** (2026-10-21, ten agents): seed
  takes paint for lenses (a chrome bumper or pale bumper strip as a reverse, the orange fringe between a red lens and
  lime paint as an indicator, a one-sided highlight of the brake's own hue as a lens); `axis --prop` stops at the
  copy it starts from ("already where it fits best") on four cars, and finds a far better axis once moved by hand,
  which agents did by writing `axisY`/`mirrorDx` themselves; SPARE is offered for a tip and again after its drop made
  the copy worse; `trace` on an unmirrored prop writes `"mirror": true`, `show --prop` calls a copy-side prop
  "authored", and `cut`'s lit picture never burns an authored-side part; red on red runs on past 30 texels in every
  `--lens`; a brown lens frame is not frame to `dark`. Each cost an agent a retrace or a hand edit; fixing them in
  that order closes it.
- **What the batch 3 lamp cuts added** (2026-10-22, nine agents): `find` lists one side's patches only;
  `trace` reads grey glass as no lamp; seed's amber class merges amber with orange-red. Each cost an agent a hand
  placement; fixing them in that order closes it.
- **What the batch 5 lamp cuts added** (2026-10-22, eight agents): seed takes a taxi's yellow chequer strip for the
  rear indicator (`cab_white`) and, on a red body, outlines only the amber patch as the brake (`crossover_red`); it
  reports a headlamp traced to its bright strip as 0.50 off its rim, 3 texels short of the frame
  (`family90s_silver`); `drop --vertex` bows the joining edge out over paint (`budgetsuv_white`, 437 → 539 px);
  `snap --pull=frame` drags an edge that found no rim into the next lamp; `trace` says "the copy side draws no such
  lens" where it does (`crossover_red`'s, `kombi_sage`'s). Fixing each closes its part.
- **What the batch 6 lamp cuts added** (2026-10-22, nine agents): a SPARE drop made the rim worse both times it was
  taken (`hybrid_white`, `archsaloon_silver`: predicted 0.00/0.40, got 0.75); `hold` keeps an edge out of the fit but
  snap still moves its ends, so two split edges drifted into an undeclared overlap (`mpv_silver`); snap stretched a
  brake tail onto a dim trim strip and added a spike vertex outside a headlamp the report did not flag
  (`hybrid_silver`); "off its rim" is a median and hides
  a corner 2–3 texels onto the frame (`smoothsaloon_white`). Fixing each
  closes its part.
- **What the police lamp cuts found in `qq props`** (2026-10-22, nine agents): `seed` traces `ind_front` on the bonnet's
  shield badge (five of nine) and
  `reverse` along the pale rear bumper; there is no trim action (agents `divide` into reverse then `remove` it),
  a second `divide` drops the first's `hold`, and `hold` indices are not renumbered when a vertex is added by hand. A
  `seed` that skips the badge, and a `trim`, close them.
- **What the batch 9 and 10 lamp cuts added** (2026-10-22, eighteen agents): `snap` grows a lens past the lamp cell
  without refusing (`offroader_black`, `midsaloon_black`); seed adds a `brake2` that takes the car to 12 lenses
  (`coupe_yellow`); a reverse share mostly under an `over` indicator lights a sliver (`premsuv_white`, 49 px) and
  nothing warns; after a snap the two sides of a `divide` drift a sub-texel apart (`premsuv_black`); snap still made a
  hooked notch twice though the manual says it refuses one. Fixing each closes its part.
- **What the batch 7 and 8 lamp cuts added** (2026-10-22, eighteen agents): seed traces a tall tail housing as one
  brake past the cell on most 4.6 m cars and never offers `divide`; seed takes a beige bumper (`execsaloon90s_beige`)
  or a pale bumper strip (`premsaloon_white`) for a lamp, puts a prop's authored outline below the axis, and overlaps
  `head` with `ind_front` where the art draws two lenses; on black paint the `dark` panel counts the body as frame
  (`bigsaloon70s_black`); `trace --lens=frame` on a framed headlamp runs on past 30 texels on four cars, and a glass
  trace swallows the bumper; `move --edge --grow` slides the edge's ends (seen again, 1.9 and 3.3 texels); the report
  does not flag a lens declared `over` another that sticks out of it. Fixing each closes its part.
- **What the lamp cuts of batches 1 and 4 found in `qq props`** (2026-10-22, 29 agents): seed takes body paint for a
  reverse (`microcar_white`, `cityhatch_silver`), an orange fringe or a paint patch for an indicator (`citycar_lime`,
  `citymini_green`), a bumper edge for a brake (`estate90s_red`), and stops a headlamp 4–6 texels short of its tip
  without saying so (`hatch_blue`, `budgetsaloon_grey`); `find` lists a pale pink highlight as amber
  (`citymini_yellow`) and misses an amber front lens the hue panel shows (`hatch_white`); `--lens=frame` cannot read a
  pale headlamp drawn with no rim (`tavria_white`, `retro500_cream`, `cityhatch_silver`, `saloon90s_white`: placed by
  hand); `move --edge --grow` slides the edge's ends along their neighbours (a tip 4 texels past the lens on
  `citycar_lime`); `hold` has no command; snap grew a section 17 px past the lamp it is declared over
  (`retro500_cream`). Fixing each closes its part.
- **What the polygon loop still gets wrong** (2026-10-21, `.qq/props.py`, `.qq/_props_fit.py`): `axis --prop`
  searches only near the copy it has, and called one three texels off "already where it fits best"; snap never turns
  a bow concave that was not, so a hand-placed crescent keeps its inner arc bowed out (a traced one is bowed right);
  the framed figure cannot tell a frame from dark paint and flags a lens framed on part of its rim;
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
- **remotegen's `video --last-image` has never run and is documented nowhere** (2026-10-20,
  `.claude/skills/remotegen/remotegen.py`): `video` itself is the walker looks' (SKILL.md), the last frame was added for
  the lamp trials the owner set aside. A SKILL.md line and one run, or deleting the option, closes it.
- **A generated car's mirrors are clipped flat at its footprint** (2026-10-18): the model draws them about 0.05 m past
  the 0.1 m a side a footprint allows (`qq sprites cut`'s overlay), and the cut keeps the footprint; `cars_fleet_a` drew them about twice the size asked even when told "no longer than the headlamp".
  Barely visible at game zoom; footprints authored to the mirrors a body has closes it.
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
  4.0 to 5.4 m (a pickup 4.9); `--bench connectors` lists 681 near-side and 161 far-side movements tighter
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
- **A vehicle wider than a roadside never parks at a kerb** (GEN-4n): it runs the round to car parks only, and a traced
  map's spawn stands none at a kerb, so its car count falls short by their share.
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

- **A body put down is laid where it stood** until its own next turn, and then off covers a view reads two steps behind
  (`SimFigures.WorldStepsAhead`): a placement reaches a view only with the step that took its signal. The fleet keeps the
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
- **The metre↔pixel conversion has no one site** (CLAUDE.md, SIM-3): the screen's factor is `Camera2D`'s and the art
  grid is `qq art`'s. Naming `Camera2D` as the site, or routing both through one, closes it _owner_ (the wording is the
  working agreement's).
- **`Road.CrossingSetbackM` is an owner's figure nothing reads.** Whether a zebra stands that margin off the junction's
  movements in the code (WLK-10) or the figure goes is the owner's _owner_.
- **The self-driver's throttle ceiling is the nominal car's** (`TyrePass.DriveCeilingMps2`) where CAR-11 has a car
  decide on its own figures, and nothing needs it nominal. Moving it moves behaviour; measure first.
- **osmscan has two answers for a metres tag**: `Roads.Metres` and `OsmCarriageway`'s refuse 0 and `Buildings.Metres`
  takes it. One answer
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
- **The guardrail art is read by nothing** (2026-10-26, GEN-75): the twelve looks under
  `assets/world/prop/variants/street/guardrail/` are installed, and `Catalog.json` lists none of them on purpose — the
  town's scatter must not stand a crash barrier in a park, and the verge keeps every scattered prop off an intercity
  road. A placer standing the sections end to end along the shoulder's outer edge (`GroundRings.Shoulder`), broken at
  every mouth, closes it.
- **The shoulder repeats over the walk's period** (2026-10-26, GEN-75): it is laid in the walk's array after the walk's
  looks (`ProjectPaths.GroundSurfaceLooksAfter`), so the vertex stages read the walk's period and patches for it and
  `.qq/art.py` joins it at `GroundPeriodPavementM`. A period of its own needs a slot of its own in `CameraView`, both
  heads' camera blocks and `GroundPatches`.
- **The town's kerb is struck along a road with no walk too** (2026-10-26, GEN-75): it is laid off the whole driven
  ground's boundary (TER-3d), so a kerbstone stands between an intercity road's asphalt and its gravel shoulder, where
  a highway has an edge line. A kerb struck off the walked shell alone, with the carriageway's edge left to the
  shoulder elsewhere, closes it.
- **`GroundRings.PavedWithin` reads a walk beside a road with no walk** (2026-10-26, GEN-75): it measures to the
  carriageway's edge and adds the walk's width, so beside an intercity road it answers paving a walk's width further out
  than there is any. The verge holds props and buildings further off still (`GroundShapes.InAVerge`), so nothing stands
  differently for it; measuring to the walk's own outer face where there is one closes it.
- **How many crossings at a walk's end fall back to a turn is uncounted** (2026-10-27, GEN-75): `CrossingWays.HandsOver`
  takes a walk to end past a place only where both its lanes find the end within three setbacks, and otherwise lays the
  junction and the turn back as before. Fixture and Saltbay's intercity crossings looked at end straight; no instrument
  says how many fall back on a whole town. A count beside `Unreached` in `TownCensus`'s foot reading closes it.
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
  number is never reused". The log cites only the new one; renumbering it is the owner's.

