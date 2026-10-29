# Map loading: measurements and improvement plan

Measured 2026-10-26 on branch `citygen-rework`, with the generator review's changes uncommitted, in a Release build.
The machine has 16 cores, and runs used 8 (`DOTNET_PROCESSOR_COUNT=8`, `nice`). Figures come from `--bench load`
(managed world) and `--bench deviceload` (device world, which is what the game does). CPU profiles come from
`qq prof --load` (perf, `cycles:u`), and wall-time traces from `dotnet-trace` (thread time). The raw output is in
`.tmp/load/` and `.tmp/prof/`.

## 0. Outcome (applied the same day)

Steps 1–6 are in the working tree, and so are two follow-up rounds (the follow-up rows below). Step 7 as written was
dropped; its exact part, the hashed cells, was done in the second round. On every map the plan, atlas and marks
digests match the morning's figures.

| Map | Open before (managed) | Open after (managed) | Open after (device, the game's path) | Target |
|---|---|---|---|---|
| Saltbay | 2.1 s | 1.23 s | 1.09 s | ≤ 1.3 s |
| Twinford | 9.1 s | 6.9 s | 5.5 s | ≤ 6 s |
| Highmoor | 45.1 s (device 44.0 s) | 25.1 s | 18.2 s, peak 2.9 GB | ≤ 25 s |
| OdesaOsm | 20.6 s | 10.7 s | 8.3 s | ≤ 14 s |

The device path was measured before the changes on Highmoor only. The other device figures have no before.

| Step | What was done | Effect |
|---|---|---|
| 1 | `CarParks.SiteBook` remembers each refusal by site, side and rank. It keeps one ranking per bay draw, and a site leaves only the ranking for the draw it refused. | Highmoor car parks 14.4 → 1.7 s; Twinford 0.82 → 0.31 s |
| 2 | `RoadGraph.LayTheLayingIndex` runs before `LayTheBaysAndDoorsOntoTheLanes` starts its workers. The other lock-guarded lazies were audited: `Paving`'s and `GroundRings.WalkSides` are first touched on the laying thread (`TownPlan`, `GroundShapes.Shell`, `GroundMesh.Build`), so they need no change. No guard was added. | Saltbay world 1.19 → 0.45 s |
| 3 | `--bench load` prints each watched stage's own milliseconds. `TownWorld` reports crossings and trains, fleets and parking, places, and bays and doors as separate stages. `qq prof`'s inline repair (`.qq/_nest.py`) now walks only keys that can still reach the target, with identical output. | A `--focus` read takes 5–7 s instead of 106–212 s |
| 4 | Exact parts only. `Apart` skips arc pairs and 16-point runs that cannot come nearer than the nearest point found or the clearance. The busiest junctions start first. The arc-distance variant was not needed. | OdesaOsm streets 7.6 → 1.0 s |
| 5 | Each cell's nearest middle is found in parallel. The sums stay serial, in cell order. Seeding was 0.1 s of 3.3 s, so it stays serial. | Highmoor authored 4.6 → 2.3 s |
| 6 | `GroundMesh.BuildAside` builds the mesh on a pool thread beside a device-filed world (`LaidTown`, and `--bench deviceload` the same way). This does not apply to the managed path or to a single-threaded page. | Highmoor no longer waits about 5 s for the mesh; peak 3.0 GB |
| follow-up | `KerbEnds.Under`: `LaneShell.Of`'s hole filter took the lanes' reach (a walk over 187 720 lanes) and a scan sized to every lane once per piece; now once. | Highmoor plan ground 4.7 → 3.0 s; OdesaOsm 2.6 → 1.6 s |
| follow-up | `Partition.Middles` files the middles in an array over their box, not a hashed dictionary, visited in the same order. | Highmoor authored 2.3 → 2.1 s |
| follow-up | `ZoneTree` files each ring's edges by grid row (`GroundShapes.EdgeRows`) and keeps its own crossing arithmetic. `TownWorld.Groundwork`'s pavement stage reports pavement lanes and zebras separately. | Highmoor props 2.45 → 2.2 s, buildings 1.75 → 1.6 s |
| follow-up 2 | `GroundShapes.Rings` files its rings by the main grid's rows their boxes span (`BoxRows`); every ground query had tested every water, shore and ballast ring's box. | Highmoor props 2.2 → 2.0 s |
| follow-up 2 | `CellTable` (open-addressed) and `CellChains` (insertion-ordered) replace the hashed dictionaries in `PointCells`, `ArcRings.Place`, `TrackChords`, `RoadLines` and `FootGraph.Builder`. `PointCells` takes a known extent as an array (car parks, `TownLinks`). | Highmoor car parks 1.7 → 1.15 s; crossings and trains 0.69 → 0.38 s; managed open −1.5 s |
| follow-up 2 | `BuildingStage.OffTheRank` asks the face stations filed by cell, in their own order, instead of scanning every station for each service rank. | Highmoor buildings 1.54 → 0.91 s |

Step 7 as written does not apply: `ArcSeg.StartUnit` already caches a piece's heading, and the remaining `Trig.SinCos`
calls are angles that change with each sample, so a cheaper form would move the digest. Its hashed-cell part is done
(follow-up 2).

What still takes the time is recorded in `docs/tech-debt.md` ("An open's remaining seconds…"): the plan's ground (3.1 s
on Highmoor), authored, props, the pavement lanes and the pavement. Four serial `ArcOutset` passes are about 4 s of the
critical path. Unit, town, perf, maps and e2e fail in the same places as before these changes, and every one of those
failures is in the tech-debt file.

## 1. Where the time goes

### Whole opens

| Map | Roads | Open | Plan | World | Ground mesh | Peak resident |
|---|---|---|---|---|---|---|
| Saltbay | 1 123 | 2.1 s | 0.8 s | 1.2 s | 0.1 s | 0.26 GB |
| Twinford | 26 987 | 9.1 s | 4.6 s | 3.7 s | 0.8 s | 1.6 GB |
| Highmoor | 93 619 | 45.1 s | 30.4 s | 9.6 s | 5.1 s | 5.1 GB |
| OdesaOsm (traced) | 16 001 | 20.6 s | 13.8 s | 5.6 s | 1.1 s | 2.7 GB |

On Highmoor, the game's own path (`deviceload`) takes 44.0 s and peaks at 3.5 GB: the plan takes 30.8 s, the world
8.0 s and the ground mesh 5.3 s. The plan is 70 % of the open, and the plan is where the big map loses its time.

On Highmoor the main thread is busy for the whole open (47 CPU-s in 47 s), and the machine averages 2.9 cores busy.
Every stage runs one after another. The parallel stages are already parallel (props, buildings, atlas filing,
crossing ways); the wall time is in the serial ones.

### Highmoor, by stage (wall time)

| Stage | Time | What it is |
|---|---|---|
| plan: car parks | **14.4 s** | `CarParks.Lay`: one thread, quadratic (finding A) |
| plan: authored | 4.6 s | nearly all `Partition.Cut` on one thread, ~4.7 CPU-s (finding D) |
| plan: ground | 4.7 s | `Paving.Perimeter` 2.9 s and `GroundRings.Of` 1.0 s: the merge of the driven bands |
| ground mesh | 5.0 s | `BandShell`/`ShellFill` lines 3.3 s, layers 1.7 s; laid after the world, never beside it |
| world: atlas | 4.0 s managed / 1.1 s device | already off the host on the game's path |
| world: foot | 2.5 s | `PavementLanes.Of`, `CrossingWays.Of` |
| world: the rest | 2.3–3.5 s | `CrossingHolds.Of` 0.7 s, places and bays 0.5 s, statics and spawn |
| plan: props / buildings | 2.6 / 1.8 s | parallel; `GroundShapes.At` → `Rings.Covers` is the hot query |
| plan: streets | 1.2 s | |

Across the whole open's CPU, the hottest self time is `Trig.SinCos` (10 %, called from spline sampling in a dozen
places) and `Dictionary.FindValue` (6 %, `PointCells` and `RoadLines` cells kept in hash maps rather than grid
arrays).

### Ruled out

- **Garbage collection.** The open allocates 33 GB on Highmoor, but server GC and a 256 MB gen0 budget each moved the
  open by no more than the run-to-run noise (±2 s).
- **JIT and startup.** A ReadyToRun publish saved 0.1 s on Saltbay and nothing on Twinford, and startup before the
  plan is about 1.5 s.
- **The review's changes.** On `HEAD`, car parks take 15.9 s and the open 47.9 s. The cost was already there.

## 2. Findings

**A. Car parks offer the same refused sites again for every car park.** Highmoor makes 1 282 540 offers to lay 10 797
car parks out of 40 945 sites, so each site is offered about 31 times. 99 % of offers are refused: 82 % at a lead node
(`Dry`/`StandsClear`), 17 % at the rank's middle, and 0.1 % at `Clear`. The cause is in the book:
`SiteBook` puts a refused site back at the distance it had (`CarParks.Lay`, "A refusal costs that site and never the
car park"). A refused site stays the furthest from every car park, so it is at the head of the queue for the next car
park too. Every later car park walks back through all of them. The cost grows as roads²: Saltbay 24 ms, Twinford
0.8 s, Highmoor 14.4 s.

A refusal cannot be undone while car parks are being laid. Water is fixed, nodes and roads are only ever added, and
the kerb comes from the road's line, which does not change. So a site refused for one bay layout will be refused for
that layout every time it is asked again. Remembering the refusal changes no outcome.

Separately, Highmoor asks for 18 762 car parks and lays 10 797. That shortfall is about what the town gets, not about
speed, and is noted here only because the book runs dry at the end.

**B. A lane index built lazily inside a parallel loop stalls the thread pool.**
`TownWorld.LayTheBaysAndDoorsOntoTheLanes` hands every worker `_roads.NewLaneScan()`. That call reaches
`RoadGraph.Nearest`, which builds the lane index under a lock the first time it is asked, and building it is itself a
parallel job (`ChainIndex.Builder.Seal`). The other workers block on the lock while the builder waits for pool
threads, so the pool grows by injection. The stall costs about 0.75 s on every open, whatever the map's size.
Building the index once before the loop (a probe, since reverted) cut that step from 847 ms to 96 ms on Saltbay and from
974 ms to 461 ms on Twinford. That is 35 % of Saltbay's whole open. It is the cause of the tech-debt entry "Most of
Odesa's open is one stage nothing has broken down". Other lazy members follow the same lock-and-`??=` pattern:
`Paving.Perimeter`, `Above`, `KerbEnds` and `Rings`, and `GroundRings`' walk sides. Any of them first touched from
inside a parallel loop would stall the same way.

**C. On a traced map, keeping turns apart takes 7–8 s.** On OdesaOsm, `LaneLines.OnTheirOwnSides` is almost all of the
7.4 s street stage. The main thread waits in `InChunks.Over` while workers check junctions. `Apart` measures every
pair of opposing turns by brute force over points 0.25 m apart, and when a pair is too close it does so again for up
to 9 earlier turning points. Junctions are spread over the workers, so the one junction with the most turns sets the
time. Generated towns barely pay for it, since their junctions are small.

**D. The district partition runs on one thread.** `Partition.CutRegion`'s furthest-point seeding is cells × districts,
and each settle assigns every cell to its nearest middle (`Middles.Nearest`). Both are repeated for each recut. This is
4.7 s on Highmoor.

**E. The ground mesh is laid after the world, never beside it.** `LaidTown.Laying` lays the world first and the mesh
after it, so the mesh is not built while the atlas is at its peak. That was chosen for memory. On the game's path the
atlas now lives on the device, and the mesh's 5 s could overlap the world's 8 s.

## 3. Goal

Open each map faster on the desktop, with nothing the generator lays changing unless a step says so.

| Map | Now | Target |
|---|---|---|
| Highmoor (device path) | 44 s | ≤ 25 s |
| Twinford | 9.1 s | ≤ 6 s |
| Saltbay | 2.1 s | ≤ 1.3 s |
| OdesaOsm | 20.6 s | ≤ 14 s |

The plan digest and the atlas digest that `--bench load` prints must stay the same for every step marked *exact*. A
step that moves a digest says so before it lands and lands on its own, so the move is easy to check.

## 4. Plan, in order

Each step is measured with `--bench load` before and after on all four maps.

### Step 1: remember refusals in the car-park book (exact; Highmoor −14 s, Twinford −0.7 s)

- A site refused at its middle (`Dry`/`StandsClear` of the rank's middle) does not depend on the bay layout, so it
  leaves every book for good.
- A refusal of the bays (lead node, `Clear`) depends on the layout that was drawn, so it is remembered against that
  layout. That site is never offered with that layout again, but other layouts still can be.
- The order of offers and the choice for each car park must stay as they are now, so the plan digest does not move.
  Only offers already known to fail are skipped.
- **Decision for the implementer:** what key a remembered layout has (the per-side ranks, or something coarser that
  is still exact), and where it lives (the book or the site).
- Update the "kept rather than sorted again" remarks on `SiteBook`, which do not account for refusals.

### Step 2: build lazy indexes before the parallel loop that needs them (exact; −0.5 to −0.75 s on every open)

- Build `RoadGraph`'s lane index before `LayTheBaysAndDoorsOntoTheLanes` starts its workers. It could be built
  explicitly in `Groundwork.Lay`, or touched once on the calling thread.
- Check each other lock-and-`??=` lazy (`Paving`'s four, `GroundRings.WalkSides`) for a first touch from inside
  `InChunks`/`Parallel`, and build it beforehand in the same way.
- Consider a guard that fails loudly when a lazy index is first built on a pool thread, so the stall cannot come back
  unnoticed. Whether that is worth adding is the implementer's call.

### Step 3: split the probe's lumped stages (instrument, no behaviour change)

- `--bench load`'s "the rest" covers crossing holds, places and bays, kerbs, statics and spawn. Report each, as the
  plan's stages already are. Several of the findings above had to be found with temporary stopwatches because of this.
- `qq prof` was fixed in this session: perf's default event and call graph are refused at `perf_event_paranoid` 2,
  and it now records `cycles:u` with user call chains. Reading a report back from a 2.6 MB folded file takes 2–3
  minutes for each `--focus`, and that is worth looking into before the next profiling round.

### Step 4: keep traced junctions' turns apart cheaply (exact first; OdesaOsm −5 to −7 s)

- **Exact:** before the point-by-point measurement, compare the two lines' bounding boxes. If they are further apart
  than the clearance, the pair is already clear, and that is the same decision `TurnedApart` makes now (if the pair is
  clear, the loop takes the first try and stops).
- **Exact:** start the busiest junctions first, so the largest is not the last to begin.
- **Moves the digest:** replace the sampled distance between point sets with an exact distance between the arcs. This
  is a second, separate change, landed only if the exact steps are not enough.

### Step 5: run the partition's cell loops in parallel (Highmoor −3 s; digest moves unless the sums are kept in order)

- Furthest-point seeding (an argmax over cells) and assigning each cell to its nearest middle are independent per
  cell. Both can run in chunks.
- Moving each middle sums positions in floating point, and that order is what the middles come out of. The chunking
  must not depend on the core count, because the digest must be equal across processes and builds. Summing in a
  different order moves the digest once.
- **Decision for the implementer:** keep the sums sequential (parallel assignment, serial sum), or accept one digest
  move with a fixed chunking.
- First count how many recuts Highmoor's regions actually make. If most of the cost is recuts, starting a recut from
  the previous cut's middles may be worth more than threads.

### Step 6: lay the ground mesh beside the world on the desktop (−4 to −5 s on Highmoor; costs peak memory)

- Only on the desktop's path with a device world. The page has one thread and stays as it is.
- Do Step 2 first. Both stages read `Paving`'s lazies, and two stages building them at once under one lock is
  finding B again.
- Measure the peak resident set. The reason for ordering them in `LaidTown` was a peak that no longer exists on the
  device path (3.5 GB now). If the overlap brings it back on Highmoor, drop the step or limit it to maps under a size.

### Step 7: cheaper hot primitives (exact if the bits are kept; mostly CPU, with some wall time)

- `Trig.SinCos` per spline sample: keep a segment's heading sine and cosine where it is sampled many times. Result
  bits are unchanged if they come from the same `SinCos`.
- `PointCells` and `RoadLines`' cell index: arrays over the grid in place of dictionary keys (SIM-8 already puts every
  index on one grid). Do it together with the tech-debt entry "The generator indexes the map off the town's grid".
- These matter most on the page, which runs single-threaded and spends about twice a desktop core's time
  (tech-debt: OdesaOsm's plan is 21.8 s of the page's 43 s).

## 5. Considered and not recommended

- **Caching a laid plan on disk**, keyed by brief, seed and build. It would turn 30 s into a read, but the readme
  says "No file stands between the brief and the town" (GEN-60), and every code change would invalidate the cache.
  That trade is the owner's to make, not this plan's.
- **GC settings and ReadyToRun.** Measured; no gain (section 1).
- **Reworking `Paving.Perimeter` / the band shells (4.7 s plan ground plus 3.3 s mesh lines).** The cost is real, but
  it is geometry with a digest behind it, and steps 1–6 come first. Profile it again once they land.

## 6. Risks

- **Digest drift.** Steps 1, 2, 3, 4 (bounding-box check) and 7 must leave the plan and atlas digests equal. Steps 4
  (exact arc distance) and 5 may move them, and each lands on its own.
- **Determinism across core counts.** Any new parallel reduction is chunked independently of
  `Environment.ProcessorCount`.
- **The car-park key.** If a remembered refusal is keyed more coarsely than what decides the refusal, it is no longer
  exact, and car parks move without anything failing. The digest check before and after is what catches this.
- **Memory on overlap (Step 6).** The peak resident set is part of the page's budget (4 GB) and of whether the
  desktop stays usable during an open.
- **The P0 rules are not touched.** Every step is load-time work. The allocation gates measure the tick, and the
  tick is not changed.

## 7. Test intentions

- **Plan and atlas digests are equal before and after** each exact step, on Saltbay, Twinford, Highmoor and
  OdesaOsm, and on the suite's own towns (`Towns.Fixture`, `Towns.City`).
- **The car-park book makes the same choices with fewer offers:** a unit test on a small layout in which a site is
  refused and the next car park does not offer it again under the same bay layout.
- **No lazy index is first built on a pool thread during an open,** if the guard in Step 2 is added.
- **`qq tests unit town`** after each step. **`qq tests all`** before a commit.
- **Load figures taken again** for each map, and the tech-debt load entries updated or removed.

## 8. Open decisions

- The key for a remembered car-park refusal (Step 1).
- Whether the partition's sums stay sequential or the digest moves once (Step 5).
- Whether Step 6's memory cost is acceptable, and up to what map size.
- Whether a plan cache on disk is wanted at all (section 5). This one is the owner's.
- Whether to look at the car-park shortfall (18 762 asked, 10 797 laid on Highmoor) as its own piece of work.
