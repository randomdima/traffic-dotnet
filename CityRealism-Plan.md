# Generated cities against real ones — review and improvement plan

Review of what `citygen/gen/` lays for `Saltbay`, `Twinford` and `Highmoor` (seed 1, branch `citygen-rework`),
compared by eye and by figure with how real towns are put together. Pictures are in
[.tmp/citygen-review/](.tmp/citygen-review/) — whole maps (`*-whole.png`, `*-all.png`, `Twinford-zones.png`) and
three sheets of close-ups (`saltbay-sheet.png`, `twinford-sheet.png`, `highmoor-sheet.png`), each captioned so it can
be taken again. Figures quoted are from `--brief <map>` and the code cited.

The plan respects the house rules: every number it adds is an authored raw term on `SimConfig`/the brief, and every
realism figure it quotes (coverage, density, buildings per km²) is something an instrument *measures*, never an input.
No rule touched here is `P0`/`P1`, but several were asked for by the owner in as many words (decision log,
2026-10-26); those are marked **owner** and are questions, not proposals to act on.

---

## 1. What a real town looks like, for comparison

Reference ranges, from planning literature and from the traced `OdesaOsm` the project already holds. They are what
the instruments in phase 0 should be compared with, not targets to author.

| Aspect | Real towns | Generated (observed) |
|---|---|---|
| Built-up extent | Sized by population; a compact urban area, fields/woods/villages beyond | Districts fill the map edge to edge regardless of roster |
| Density gradient | Steep: densest at the centre, falling outward (Clark's exponential) | Flat; the core itself is empty |
| Centre | At the historic crossing point: river ford/bridge, harbour, crossroads of old roads | An intercity-road junction in an empty field, kept ≥200 m off water |
| Block size | Old town 40–80 m; inner residential 80–150 m; suburb long and thin (≈60 × 200 m); industrial 150–400 m | 95–170 m × aspect 1.2–2.2 everywhere (`CityGenFigures.BlockSpacingAlong*`, `BlockAspect*`) — up to 170 × 370 m |
| Building coverage of a block | Old town 50–70 %; flats 25–40 %; suburb 15–25 %; industrial 30–50 % | One sparse ring on the face, interiors grass |
| Main roads | Continuous radials and/or a ring, lined with shops and buildings; motorways bypass | Curvy district-edge links; intercity roads run through the middle with a 15 m empty verge |
| Land use grain | Mixed at block scale: high street, schools, parks, squares, churches, workshops | One `ZoneKind` per 0.75 km² district, five kinds only |
| Public services | Hospital per ~100–200 k people; police station per ~30–60 k; depot per city or sector | One hospital, police station and depot per district: 73 in Twinford, 252 in Highmoor |
| Rivers | Dendritic: one main river, tributaries joining at acute angles upstream | Parallel bands on one bearing; tributaries enter from the map edge |
| Railway | Station near the centre, goods yard, industry along the line | No station; line ignores the town; a balloon loop in a field |
| Coast / river bank | Harbour, quays, promenade or beach; embankment streets | Bare grass strip |
| Parking | On-street parallel along residential streets; lots attached to shops, flats, stations | Perpendicular bay pads cut into both kerbs at a regular pitch, often with no building near |

---

## 2. Findings

Ranked by how much each one makes a generated city read as "not a city". Each says what is seen, where, and the
cause in the code where it was traced.

### A. Macro form — the biggest gap

**A1. The map is filled edge to edge with districts at one size.** `Highmoor` (16.4 × 12.6 km) is 254 districts of
0.16–1.00 km², `Twinford` 75. Nothing is countryside: no farmland, woods or villages, no urban fringe
(`Highmoor-all.png`, `Twinford-all.png`). Cause: `Partition` cuts all dry land into districts (GEN-64/65); the only
open land is what is too small or unreachable.

**A2. Streets are laid for the area, buildings for the roster, and the two disagree.** The briefs plan 2 400 /
24 000 / 72 000 buildings against a frontage of about 3 500 / 53 000 / 191 000 (`docs/tech-debt.md`, _owner_ entry).
`ZoneThinFalloff` thins away from the core, so the outer districts are whole street grids with almost nothing on them
— an abandoned subdivision look (`highmoor-sheet.png` "outer edge", "map corner"; `twinford-sheet.png` "yellow
district", "brown district"). Even inner districts read sparse.

**A3. The core is an empty interchange.** In all three cities the point named as core is an intercity road in open
grass (`twinford-sheet.png` and `highmoor-sheet.png` "core close"; Saltbay's core is in open land, already in
tech-debt). Causes, together: the core is where the intercity roads converge (`Connectors`), an intercity road is a
district barrier (GEN-64), it carries no walk and nothing within `UnwalkedVergeM` (GEN-75, **owner**), and the core
prefers to stand `CoreOffWaterLeastM`/`BestM` (200/400 m) off the water. A real centre is the opposite: the densest
place, on the old main street, usually at the water.

**A4. Wide empty corridors split the town into islands.** Along every intercity road and railway there is a band of
grass 50–200 m wide (`highmoor-sheet.png` "green corridor", `Highmoor-whole.png`). The barrier corridor plus the verge
plus district edges add up. Real railways have backs of buildings, yards and allotments against them; only a motorway
has an empty verge, and it does not run through the centre.

**A5. Saltbay is four fragments.** 2 of 3 intercity roads laid; 2 of 6 districts (0.77 and 0.94 km², the whole west
half) carry no street; the town is four separate clusters round an empty middle (`Saltbay-whole.png`).

### B. Street network

**B1. Weak hierarchy.** No ring road or bypass, no continuous radials from the centre. Roads between districts follow
district edges (curvy, discontinuous); the intercity roads are the only long lines. Real towns have arterials every
0.5–1.5 km that run straight through, and through-traffic routed round the centre.

**B2. Blocks are too big, and the same scale everywhere.** Old town takes 95–132 m spacing, the rest 132–170 m, both
stretched by 1.2–2.2 (`Districts.Plan`). An old town block is two to four times a real one; a suburb block is too
square (real ones are long and thin, two rows of plots back to back).

**B3. Industrial districts are laid like residential ones**: the same dense, curvy lattice with small sheds in it
(`twinford-sheet.png` "industrial"). Real industrial land is few streets and large plots.

**B4. Bridges are sparse for a town centre.** `BridgesApartMostM` = 1 500 m and `DistrictLinksApartM` = 1 200 m. A
river through a real centre is bridged every 300–600 m; further out 1–2 km is fine.

### C. Land use

**C1. Five zone kinds only.** A generated district is `OldTown`, `Residential`, `Industrial`, `HighRise` or `Suburb`
(`Districts.Plan`). `Park`, `Civic`, `Commercial`, `Open`, `Farmland`, `Wild`, `Garages`, `Parking` exist in
`ZoneKind` and are built for traced maps, but a generated town never lays one: no park, school, square, high street,
cemetery or allotment anywhere.

**C2. One kind for a whole district.** 0.75 km² of one use is monotone. Real use changes at block grain: shops along
main roads, a school and a park per neighbourhood, a church or square at a node.

**C3. A full service set in every district** (GEN-56, **owner**). 73 hospitals in Twinford and 252 in Highmoor; 4 in a
2 400-building port. It is the most visible unrealism at city scale; the sim reason (a beat per district, `SRV-5`) is
real, so the question is whether a beat can stay per district while the buildings serve several.

**C4. Incompatible buildings side by side.** Saltbay's north-west district stands a hospital, a gas holder, flat-roof
sheds and houses in one block; the north-east one silos beside houses (`saltbay-sheet.png`). Not traced to a cause
yet — candidates are the services (centred on their yard regardless of kind) and `Variety`/`Looks` mixing.

### D. Buildings on the block

**D1. Old town is not terraces.** `ZoneFigures.OldTown` asks `Frontage` 0.95 and no interior, but the old town as
seen is gaps and scattered blocks (`twinford-sheet.png` "core old town"). Thinning and the 15 m verge round the core
likely eat it; to be measured (phase 0).

**D2. One ring on the face, empty interiors.** Buildings stand only against the pavement's outer face (GEN-54);
`Interior` is 0 for old town and suburb and 0.05 for residential. With blocks of 150 × 300 m most of each block is
grass. Real blocks are either small enough that the frontage fills them, or built behind (courtyards, back rows,
gardens with sheds).

**D3. Irregular setbacks and headings within one street.** Houses and flats stand at different distances and small
skews from the kerb (`saltbay-sheet.png` "NW close", "coastal close"). Real streets hold a building line.

**D4. No plots.** No gardens, drives, fences or hedges; a lone shed in a field reads as debris.

### E. Water

**E1. Rivers are parallel bands.** Highmoor's four rivers run on one bearing, evenly spaced; Twinford's two northern
rivers run ~250 m apart into the sea with a strip district between (`twinford-sheet.png` "twin rivers"). Real rivers
converge into one network; two that close would have merged long before. Cause: each river is drawn independently
about the brief's bearing (`TerrainStage`, GEN-62), and "a river that would cross another is not drawn".

**E2. Tributaries enter from the map edge**, never rising inside it, never joining at the acute upstream angle real
confluences have.

**E3. Nothing uses the water.** Saltbay "a small port" has no harbour or quay; the coast is a bare strip
(`twinford-sheet.png` "coast"); river banks have no embankment road or riverside park.

### F. Railway

**F1. No station.** The line passes `RailPassesCoreM` (450 m) off the core and serves nothing. A town's railway has a
station near the centre, a square or road to it, and industry and yards along it.

**F2. The balloon loop stands in a field** (`saltbay-sheet.png` "rail loop", RAIL-11 `P7`). A real loop is at a
terminus, a yard or a works.

**F3. A railway crosses a river at whatever angle** — already a known gap in `docs/index.md`.

### G. Parking

**G1. Perpendicular bay pads in empty streets.** Ranks of bays are cut into both kerbs of residential streets at a
regular pitch, mirrored, often with no building within 50 m (`.tmp/citygen-review/tw-pads.png`). Residential
streets in reality park along the kerb (GEN-4n already models kerbside parking); lots belong to destinations.
Likely cause: car parks are sited off the streets before the roster thins the buildings, so they stay where the
buildings went — to confirm.

---

## 3. Goal

A generated city should read, from the whole-map view down to a street, as a plausible town of its roster's size:
a compact built-up area with a dense centre at its historic crossing, falling off to suburbs and then countryside;
main roads that run through and carry the town's frontage; blocks and plots at the scale of their use; mixed land
use including open space; water and railway that the town uses. Measured, not judged: phase 0's instruments show the
generated figures moving toward `OdesaOsm`'s and the reference ranges above.

Not goals: changing the stage order (GEN-61), the brief's shape beyond a few raw terms, or any engine rule.

---

## 4. Plan

Ordered so each phase is useful alone and the cheap, high-value ones come first. Phases 1 and 2 change the most;
3–7 are local to one stage each.

### Phase 0 — instruments first

A realism probe (likely an extension of `--bench census` / `TownShape` rather than a new probe) that prints, per map
and per district kind: buildings per km², building coverage of block area, block size distribution, share of street
length with no building within reach, density against distance from the core, open-land share, service buildings per
1 000 buildings. Run on `OdesaOsm` too, so the traced real city is the comparison in-project.

- Reports figures; gates nothing (VER-12: whether a run had anything to say is the instruments').
- Gives every later phase a before/after number instead of a picture.

### Phase 1 — size the built-up area by the roster (A1, A2, A5)

Invert the order of the question: the roster (and each kind's plot sizes) says how much land is town; districts are
built outward from the core, nearest and best-connected first, until the roster is housed. What remains is
`Farmland`, `Wild` or `Open`, with the intercity roads crossing it as country roads.

- **Option a (recommended):** keep `Partition` as is, rank districts by distance/accessibility from the core, and
  build streets only in as many as the roster needs; the rest become countryside zones. Smallest change; GEN-65
  ("a town grows by adding districts") already says this.
- **Option b:** grow the urban area as a field (distance along roads from the core), then partition only that. More
  organic edge; more rework of `Partition`.
- Optional later: a village or two at intercity junctions far from the core.
- **Owner:** the briefs' building counts are already an owner question; this phase makes the answer "the map extent
  is the region, the roster is the town" rather than raising the counts. Load time and sprite cost fall with it.

### Phase 2 — a centre that is a centre (A3, A4, B1)

- **Core siting:** prefer the water rather than avoiding it — the first bridge site of the main river, or the coast's
  most sheltered point — still on dry land. Invert `CoreOffWater*` to a preference for nearness.
- **Intercity roads stop being barriers inside the town (owner — GEN-63, GEN-75):** two options to put to the owner.
  - *Bypass:* intercity roads meet a ring (or partial ring) round the built-up area; radial arterials carry on inward
    as roads between districts, with frontage. The intercity road keeps every property the owner asked for (four
    lanes, smooth, no walk, no verge building) and simply does not enter the centre.
  - *Boulevard:* inside the built-up area an intercity road becomes an arterial with walk and frontage.
  Recommend the bypass: it keeps the owner's rules intact and is how most real towns are now.
- **Corridors:** narrow the empty band along railways to what the track's ground needs, and let industrial/garages/
  allotment zones stand against it.
- **Arterials:** a few continuous radials from the centre and one ring at the old-town edge, laid before district
  links so links join them rather than replace them.

### Phase 3 — block and plot scale by kind (B2, B3, D1, D2, D3)

- Block spacing and aspect authored per kind (raw lengths): old town small and near-square, residential medium,
  suburb long and thin, industrial coarse with few streets, high-rise as superblocks with an internal service road.
- Old town: terraced frontage kept whole within the centre's radius (exempt from thinning), courtyard buildings
  behind. Residential: a back row or courtyard share. Suburb: one house a plot at a regular pitch, a common setback,
  a shed or garage behind.
- A street holds one building line; skew and setback spread drawn per street, not per building.

### Phase 4 — mixed land use (C1, C2, C4)

Use the zone kinds the engine already builds:

- `Park`: leftover and awkward pieces, riverside strips, one per neighbourhood; a square at the core (`Open`).
- `Civic`: schools at a pitch through residential districts; a church or town hall at the core.
- `Commercial`: frontage along arterials near the centre — the high street.
- `Garages`, `Farmland` (allotments) and `Industrial` against railways and on the town's edge.
- Sub-district zoning: a district is a mix of zones rather than one kind. Decide in implementation whether zones are
  cut per block or per frontage run.
- Trace C4 to its cause before fixing it.

### Phase 5 — services by catchment (C3, owner)

Ask the owner whether GEN-56 can become "every district is *served* by a hospital, police station and depot" with a
catchment of several districts (by population or by distance), while each police beat stays a district (`SRV-5`).
If yes: place services at the most central district of each catchment, hospitals fewest, police most. If no: leave
it, and note it under the decision log as a chosen unrealism.

### Phase 6 — water (E1, E2, E3)

- **Rivers as a network:** one main river per brief direction; tributaries generated upstream-to-downstream so they
  join it at acute angles, some rising inside the map. Two rivers drawn within a valley's width of each other merge
  rather than run parallel. Keep "water flowing into water is one water" (GEN-2c) and the bridge-span cap (GEN-14a).
- **Using the water:** a harbour/quay district at the core where there is a sea (a `Commercial`/`Industrial` front
  with a quay road); embankment streets along the river through the centre; riverside parks elsewhere. Respect
  `WaterfrontClearM` (GEN-71).
- **Bridges in the centre:** spacing tighter where the river crosses the built-up core; `BridgesApartMostM` can stay
  for the rest.

### Phase 7 — railway (F1, F2)

- A station on each line at its point nearest the core, with a road link and a forecourt; goods yard and industry
  along the line near it.
- The loop (RAIL-11) moved to where loops are: a terminus beside the station, or a yard. **Owner** if the loop's
  position is part of why it was asked for.
- Leave F3 to its existing gap entry.

### Phase 8 — parking where cars go (G1)

- Residential streets park along the kerb (GEN-4n); ranks of bays only for destinations — shops, flats, stations,
  civic buildings, the services' yards.
- Count and site car parks after the buildings the roster keeps, not before (confirm the order first).

---

## 5. Constraints and risks

- **The two engine rules are not at stake** — all of this is load-time generation — but **load time is**:
  `Highmoor` authors in 14 s. Phase 1 should shorten it; phases 3–4 add work per block. Measure with `--bench load`.
- **The fixture moves with the generator** (known gap): every phase moves `Towns.Fixture` and `Towns.City`, and a
  dozen town tests already fail on the reworked towns (`docs/tech-debt.md`). Expect each phase to need a pass over
  them; consider freezing the fixture first.
- **Connectivity rules hold throughout**: GEN-69 (one network, district-to-neighbour links), GEN-5a (no dangling
  lane), GEN-13 (junction angles). Phase 1 and 2 are the ones most likely to break them — countryside districts with
  no streets must still be crossed by the links that join built ones.
- **Figures flow one way**: no coverage, density or buildings-per-km² is ever authored. Authored are lengths, shares
  of draws, counts; the instruments say what they come to.
- **Determinism** (GEN-1, GEN-11): new draws go on keyed streams so one stage's change does not move another's.
- **Art supply**: phases 3–4 want terraces, courtyard blocks, schools, a station, a quay, park props. Check the
  catalogue (`qq prefabs report`) before planning a look that does not exist; new art goes through remotegen.

## 6. Test intentions

Each a requirement stated in `citygen/docs/requirements.md` under a new ID (`qq req --next`), one behaviour each:

- Streets stand only in districts the roster builds; the rest carry a countryside zone and no street.
- The core stands within reach of water where the map has any, on dry land.
- No intercity road enters the old town (if the bypass option is chosen).
- A district carries more than one zone kind; at least one park per N districts.
- A station stands on each railway within reach of the core, reached by a road.
- A tributary meets its river at an angle under a bound, downstream-pointing.
- Service counts follow whatever the owner decides for GEN-56.

The realism figures stay instruments; none becomes a gate.

## 7. Decisions

The owner chose (2026-10-27, recorded in `src/citygen/docs/decision-log.md`):

1. **The town fills the map, less dense toward its edges**, its districts unalike and not all packed; the building
   count may rise freely. (First chosen: the roster sizes the town and the rest is country — reversed once seen laid,
   the town in one corner of an empty map.)
2. **Intercity roads bypass the centre** on a ring.
3. **Services serve catchments** of districts; a police beat stays a district.
4. **The rail loop was a test and is dropped**; railways laid as real ones.

Still open for the owner: whether to freeze the fixture (every phase moved it), and
`GroundMeshTests.TheLargestTownsGroundIsLaidOnceAndIsNotEnormous` (tech-debt).

## 8. Status (2026-10-27)

| Phase | State | Where |
|---|---|---|
| 0 instruments | done | `--bench form` (`src/bench/TownForm.cs`) |
| 1 town fills the map, thinning to its edge | done; opens several times slower (tech-debt) | GEN-76, `TownExtent`, `districts.country` |
| 2 centre and bypass | done, revised: no ring — intercity roads pass the town edge to edge as one curve each, crossing each other carried over. Met only at interchanges — one-side accesses (two slips off and onto the kerb lane at 25°), accesses that cross, and roads carried over meeting nothing; unlit, carried over railways; no acceleration lane (tech-debt) | GEN-63, GEN-68, `Connectors.Intercities`, `TownLinks.Interchanges` |
| 3 blocks by kind, building line | done; block interiors still thin | GEN-74, `CityGenFigures.*Blocks`, `ZoneBuildings.Along` |
| 4 mixed land use | done; parks drawn as plain grass (tech-debt) | GEN-77, `BlockUses` |
| 5 services by catchment | done | GEN-56, `ServiceBuildings.Serving` |
| 6 water | rivers kept apart, fewer per brief; harbour, quays, embankments not started | GEN-62, `RiversApartLeastM` |
| 7 railway | loops dropped from the briefs; stations not started (needs art and a train that stops) | briefs |
| 8 parking | no town car park in a suburb; kerbside parking as the default not started | GEN-53 |

Measured against `OdesaOsm` (`--bench form`, `.tmp/citygen-review/form-*.txt`):

| | before (Twinford) | after (Twinford) | OdesaOsm |
|---|---|---|---|
| Streets with no building, share of map | 7.6 % | 1.5 % | 2.0 % |
| Street length fronted | 85 % | 96 % | 87 % |
| Median block side | 133 m | 99 m | 87 m |
| Old town covered | 24 % | 26 % | 37 % |
| Residential covered | 11 % | 20 % | 27 % |
| Suburb covered | 4 % | 15 % | 19 % |
| Hospitals | 72 | 3 | — |

Second pass (same day): towns grow along their roads in with a ragged edge; room behind the frontage is reckoned and
built (inside blocks, in a generated district); a port's core stands at its harbour on dry ground and its roads come
in over land; narrow districts lay their blocks the other way round; catchments hand a service on where a yard won't
fit; parks are planted; the bypass's open stretches run on into the roads that reach them; long country roads are
cut at nodes for the ribbon atlas; and a shell bug that paved whole blocks over is fixed. Final figures, Twinford /
Highmoor (`.tmp/citygen-review/form-final-*.txt`): rosters housed in full; residential covered 25 % / 20 %, old town
29 % / 29 %, median block 111 m / 101 m.

Third pass (same day, the owner on seeing the towns in a corner of empty maps): the town fills the map, built solid
within `TownSolidShare` of the way to its furthest land and each district further out filled less, down to
`TownEdgeFill`, by a draw no two alike; half the thinning is wider blocks, half gaps on the streets; some rim districts
are farmland or wild; the bypass runs round the solid middle; the rosters rose to what stands (Saltbay 3 000, Twinford
53 000, Highmoor 197 000). Figures, Twinford / Highmoor (`.tmp/citygen-review/form-graded-*.txt`): built over 77 % /
89 % of the map, streets with no building 1.8 % / 1.5 %, street length fronted 95 % / 97 %, old town covered 26 % /
27 %, residential 21 % / 21 %, suburb 8 % / 9 %, median block 118 m. **Cost: Highmoor opens in 151 s, Twinford in
38 s** (tech-debt).

Next, by value: the open's time on a town that fills its map (owner: extent, or load work); a harbour district and
quay at a sea core; stations (art, and a train that stops); kerbside parking as a residential street's own; acceleration
lanes at the slip roads; a large district's lattice reaching its edge.

For implementation:

- Option a or b in phase 1; how accessibility is measured.
- Whether sub-district zoning is per block or per frontage run.
- Per-kind block figures and plot pitches — set by looking at `OdesaOsm` through the phase-0 probe.
- Whether the probe extends `--bench census` or stands as its own.
