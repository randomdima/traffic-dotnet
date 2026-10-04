# CityGen — decision log

## 2026-10-05 — a traced town stands no building on its footprints

**The owner dropped the prefabs a traced town's footprints were worn as**, for the memory a browser page has: its
footprints are its buildings, drawn flat in their survey's outline as they already were (`GroundMesh.Footprints`), and
nothing stands on them — no roof, no body and no door. Nothing in a traced town asks for a building: it stands nobody,
so no trip ends at one.

On OdesaOsm that is the fitting's 7 CPU-seconds, a tenth of an open's work and all of it on one thread in a page; the
second `GroundShapes` and the kerb scan the plan laid for the fitting and nothing else; and 64 078 of the town's 65 125
static bodies. **What it costs is the collider**: a car that leaves the road crosses a drawn building rather than
striking it.

**The fitter is kept, and no plan runs it** (`TracedBuildings`, `--bench fit`, the prefab catalogue and its art): its
tests fit their footprints directly, so it is the fitter it was if a traced town ever stands buildings again.

## 2026-10-04 — the fitter wears a traced footprint as rounded prefabs on its walk

**The owner asked for a traced map's buildings to be prefabs**: a collider of a rounded rectangle or a few rectangles,
art that fits a rectangle rather than a random outline, a complex footprint worn as two or three simple buildings, and
every building standing on the walk as a generated town's do — **moved onto it where it does not reach it, and not
stood at all where it cannot be placed cleanly**. Steps, as the owner set them: a catalogue of a few hundred prefabs
tagged with what each should look like, placed without art; the art drawn off the descriptions once the placement is
accepted.

- **The catalogue is read off the city** (`assets/world/building/prefabs/`, 355 prefabs in 14 looks). Every section
  OdesaOsm offers the walk was read as a look and a roundness, and each look's sections clustered in log size at a
  budget weighed by how many there are: 40 houses, 34 blocks of flats, 28 works down to 2 greenhouses, square-cornered,
  and 19 more where the city has enough of them to be worth one — rounded houses and flats, round silos. **Every
  prefab is one rounded rectangle** (`cornerRadiusM`): a square-cornered one at its eaves, 0.3 to 0.8 m by look, a
  rounded one at half its half short side, a round one at the whole of it. Each names its look, tags (storeys, roof
  shape and material) and a line of what its roof shows from above.
- **A prefab is laid as it was drawn, door to the walk, and the deep plots have prefabs of their own.** The first
  catalogue drew every prefab wide side first with its door on it, and the fitter laid one either way round; with the
  first art in, 57 % of the town's buildings stood turned a quarter, their porches facing along the street. A prefab is
  now never turned, and 118 deep ones — door on the narrow end, clustered from the 56 % of sections narrower at the
  street than deep — were added after the wide ones, so the prefabs already drawn kept their sizes and places. 498 of
  the sheet table's 512 places are taken.
- **The art is generated a sheet of 25 at a time** over a template of their outlines on magenta, and cut back into
  sprites at the resolution it was drawn at, never past the art grid — about 15 to 30 px/m. A generator draws a house
  near-square whatever its outline, so a roof is stretched to its footprint, by up to 92 % on the first sheet.
- **A footprint's use is a fact the map carries** (`TracedMap` version 3, `FootprintUse`): its tags, else the land use
  it stands in — a garage cooperative's ground, a market, industry, a school's grounds — since more than half the
  city is machine-traced and tagged with nothing. `OdesaOsm.map` had them laid in place (`qq osm --footprints`) rather
  than imported again, so the crop and the dropped stumps stand; one footprint on the frame's edge fell outside it
  by rounding, 91 838 of 91 839.
- **A building away from the walk is moved onto it, and one that cannot be is not stood.** The first placement left
  the yards' sheds and the back wings of courtyard blocks where they were surveyed, which the owner read as buildings
  connected to no road. Now each rectangle is moved through its wall facing the nearest walk, up to 25 m, the least
  moved first; 31 965 of 106 505 rectangles are further than that from any walk. A walk sampled under the whole front
  seats it — a front laid on the tangent dipped centimetres into the walk on every gentle curve, and the kerb's half
  width is all the room there is. **The paths the owner floated for a building too far to move are not laid**: a walk
  off the pavement's ring is a change to `TER-3c.8` and `TER-7b`, which are the owner's.
- **Fitted at 11 to 25 % a side at the median** (`--bench fit`): 85 608 sections offered, 63 743 stood — 19 151 into
  frontage a nearer one took, 815 on a crooked walk, 177 on no clear ground. Laying them is about 3 s of the plan.
- **A traced sea is indexed** (`GroundShapes.EdgeCells`): the shore was one ring of thousands of points whose box is
  most of the city, walked whole for every point the ground was asked about, which made the fitting a minute.

## 2026-10-04 — a traced map holds no tram track

**The owner dropped tram tracks from the traced map: nothing will use them.** The map no longer carries them
(`TracedMap` version 2), so a traced town paints no rails. The enrichment still lays OSM's tracks in its own layer
(`towns/traced/<Map>/tracks.json`), where its checks read them and `--import` does not. `OdesaOsm.map` was rewritten
in place rather than imported again, so the crop and the dropped stumps stand; it is 11 KB smaller.

## 2026-10-04 — a roadside lost along the way stops with its road, and the kerb tapers in across the box

**The owner asked for a roadside close to a corner to be carried to it, for one lost along the way to stop at its white
line, and for the kerb to make a smooth transition where it does.** A roadside whose road ran on into one without
carried on into the box beside the lane's movement to its middle, about 10 m past its line on `OdesaOsm` — 421 of the
490 lost at a place of two arms — and ended square there, and the kerb, the lanes' own outline, turned round that end.
Filling the notch that left is all a rounding can do: the end that sticks out is a corner of the lane, and taking it off
cuts the lane (the entry below).

- **Lost along the way, it stops with its road** (`RoadsideLanes.RunInto`): where the next arm round carries no
  roadside on the kerb facing it, at a box of two arms or one the kerb runs straight on through.
- **The kerb tapers in over a band of its own** (`LaneLines.Tapers`, `LaneShell`): ground and no lane, the strip's
  width, from the strip's end on its own heading to the far end of the lane's movement on that lane's line — a biarc,
  so the kerb leaves the strip's edge and meets the next road's kerb tangent to both, the taper's end wholly inside
  the next lane. The box is the transition: 16 m at the median on `OdesaOsm`, 4.4 m at the shortest and 33 m at the
  longest. None where the box is too short to turn in at the ground's radius; 17 joints keep the step, hairpins most
  of them.
- **Close short of a corner it is carried to it** (`TracedStreets.CarriedOn`): a roadside the survey stops at a place
  of two arms, where its kerb next turns a corner — the next arm round on its side off the straight — or a roadside
  starts again within `TracedRoadsideCarriedM` (40 m) along the survey. Every road between is laid as though measured
  wide enough to hold it, as `Survey` lays any roadside, so its lanes stand off its middle as the strip sets them and a
  road that differed only by the strip is joined into the one before it, the place between gone. Laid outside its lanes
  where they stood instead, it kept that place, and a walk was stood across it a few metres short of the corner's own.
- **What came out on `OdesaOsm`**: 382 roadsides lost at a place of two arms (490) and 887 tapers; 8 786 roadsides
  beside 29 522 lanes (8 757 beside 29 632), the roads joined through taking their places with them. The boundary
  leaves no merged run open (0), 14 carriageway runs (14) and 3 walks (3); 3.21 M triangles (3.24 M).

## 2026-10-04 — the kerb is the outside of the lanes, filled where it turns in and never cut

**The owner found the kerb running through a roadside's end and asked for the boundary to follow the lanes** — the
lanes, bays and roadsides it is laid from on load, and the walk and the rest struck off it. It was laid from them; what
it did after was round itself at the kerb radius on both hands (`GroundRings`, TER-3c.10), so every corner the
tarmac turned away at was cut — 0.58 m into a square lane end at a right angle, more at a sharper corner. Before
roadsides were lanes that was the end of a lane at a dead end; with them it is the end of every roadside a box does
not carry on.

- **Filled and never cut** (`ArcOutset.Corners.Filled`), as the level above already was: a corner turning in is
  rounded at the one radius and a corner turning away is the corner the bands make, so the tarmac covers every lane.
  A concrete spike is the walk's corner and the carriageway's notch, which is filled still.
- **What came out on `OdesaOsm`**: no merged run open (0), 14 carriageway runs (12), 3 walks (3); 3.24 M triangles
  (3.26 M).

## 2026-10-04 — a roadside is lane zero, and runs on into the box

**The owner asked for the roadside to be a real lane, as a bay is one — joined to nothing, but no bay one car holds —
running up to the junction and sized as the strip is.** Laid as a band of the merge that stopped at the junction's
disc, the strip left the box only its movements' ground, which stands a roadside in from the kerb: the kerb stepped
in by 2.2 m across every box, a notch along the straight side of a tee and a knob of pavement at every corner.

- **A lane of its own road** (`LaneLines.IsRoadside`, `RoadsideLanes`): on the strip's middle at the strip's width,
  pointing the way traffic keeping to that kerb would, numbered after every lane a movement joins so no other lane's
  number moves. No movement leaves or reaches it and no junction counts it among its lanes, so no bar, signal or
  arrow is laid for it, and no car is stood, routed, toured or sent on a beat down it (`RoadGraph.IsAStreetLane`).
  `GEN-50` passes over it as it passes over a bay. It is no bay: the parking slice holds nothing of it.
- **It runs on into the box to the corner its kerb makes with the next arm round**, straight, the way its road
  points there — none where that corner stands out along the road, the two carriageways overlapping that far — so
  a corner is square and rounded as all the ground is (1.4 m), and a turn out of the kerb lane sweeps over it.
- **Where the kerb runs straight on it runs beside the movement the lane beside it makes**, to that movement's own
  joint nearest its middle, where the roadside across meets it. Laid straight there, two roadsides beside movements
  bending by a degree left the merge two runs open on `OdesaOsm`: edges a centimetre apart for ten metres, which the
  merge at a city's coarseness reads as neither one edge nor two. Halved at the middle and not at the joint, a hair
  of the movement left a piece a hair long, which no band closes round; one bending tighter than the ground's
  rounding once moved out to the strip runs straight instead.
- **A walk's station never stands in the box** (`KerbEnds`): a kerb running along a roadside reaches into it, and
  what a walk crosses and a driver is held behind is the road.
- **What came out on `OdesaOsm`**: 8 757 roadsides beside 29 632 lanes. The boundary leaves no merged run open (0),
  12 carriageway runs (25) and 3 walks (3); 3.26 M triangles (3.81 M). OSM's lanes stand as far off the engine's as
  before, the roadsides being no lanes of OSM's.

## 2026-10-04 — a traced map drops its dead ends of a single lane

**The owner asked for the dead ends of a single lane dropped, and the stumps the building rule left with them.** An edit
to the map, as the stumps into buildings are (`qq osm --drop-stumps`, `Stump.SingleLane`): a stump every road of which
OSM gives one lane — a driveway, a yard's lane, a track — goes whole back to the first place three ways meet, and again
while dropping one leaves another, so a tree of them goes to its trunk. A stump of more than one lane stays.

- **What it came to on `OdesaOsm`**: 3 295 stumps, 260.5 km — 3 279 of a single lane and 16 into buildings that dropping
  those uncovered — the longest two 1.5 km tracks off the map's west edge. 11 382 road ways became 8 520, 1 233
  restrictions 1 133 and 1 562 crossings 1 517; the town lays 13 817 roads where it laid 19 350, and 620 dead ends where
  it laid 3 633. 378 stumps of more than one lane stay, 41.6 km.

## 2026-10-04 — a traced road's lanes are one width, and what it was measured wider is roadside

**The owner asked for every real lane laid at one width, the width measured past them laid as a roadside with no
connections behind a solid white line, and the roadside's width worked out.** A measured width was shared evenly
between a road's lanes: a street of a lane each way read 12.3 m off imagery was two lanes of 6.15 m, its parked
cars driven over. The difference lane to lane was the sources disagreeing; the difference street to street was
mostly parking.

- **The lane is OSM's, 3.5 m, the owner's choice** (`TracedLaneWidthM`) over the closest fit. Over 1 869 km of
  streets measured off imagery, a surface or a tag, lanes of one width and a roadside at none, one or both kerbs — as
  many as the rest holds to the nearest strip — explain the widths to 0.92 m at the mean at 3.25 m lanes and 2.5 m
  strips, about the imagery's own 0.8 m step; every lane from 3.2 to 3.5 m fits within 4 % of that, and lanes alone
  at 3.5 m leave 3.7 m.
- **The roadside is 2.2 m** (`TracedRoadsideWidthM`): beside 3.5 m lanes, 2.2 m and 2.8 m fit equally (0.956 and
  0.953 m) and 2.5 m worst between them. The 1+1 streets read 8.8 and 10.4 m; at 2.2 m those are one strip and two,
  at 2.8 m one and one, the lanes of a 10.4 m street set 1.4 m off its way. 2.2 m is a parked car's width and keeps
  1 008 km of streets symmetric about their way, against 548. At 2.8 m OSM's lanes stood within a metre of the
  engine's for 75.1 % of their length, at 2.2 m 83.0 %.
- **A single roadside stands beside the kerb the traffic along the way keeps to** (the owner's: one side only where
  the width does not hold two). On a two-way way that is the way's own direction, which nothing else in the survey
  says. None on a bridge or round a roundabout.
- **The roadside before more lanes**: where OSM assumes the count, the width now holds as many lanes as it has past a
  roadside at each kerb — 75 ways take a count off their width, against 190.
- **No roadside's middle may fold round a corner** any more than a lane's may.
- **What came out on `OdesaOsm`**: OSM's lanes stand within 5 cm of the engine's for 60.9 % of their length (42.2 %),
  within 0.5 m for 80.4 % (66.9 %) and within 2 m for 98.6 % (97.3 %), the engine's lanes off OSM's 1.6 m at the 99th
  percentile (3.8 m); within a metre fell to 83.0 % (85.0 %), a single roadside setting a street's lanes off its way
  by half a strip. The boundary leaves 34 carriageway runs open (79), 1 merged (3) and 4 walks (10). 4.7 M triangles
  (4.0 M). 16 more of OSM's restrictions stand at a node no junction does, two ways of one width now running through
  it as one road.
- **Where a roadside starts or stops is a junction of two arms**, as any change of carriageway is, and its kerb steps
  out by the strip there.

## 2026-10-04 — a traced road is laid in the fewest corners and the roundest arcs that keep its survey

**The owner asked for `OdesaOsm`'s roads to be curved where OSM draws a bend as a polygon, with the points that
add nothing dropped and the pieces on one line merged, a minor divergence from the map allowed.** Every surveyed
point was a corner rounded at half its carriageway: 74 754 arcs, 98.8 % of 1 464 km laid straight, the bends'
radii 1.7 m at the median and 18.6 m at most — a facet at every node.

- **Normalised as the engine lays it, and not as an edit to the map** (`TracedAlignment`, GEN-57): the map keeps
  every node OSM has, so the tolerance (`TracedLineToleranceM`, 0.5 m) is retuned with no import, and the map holds
  no arc to edit into.
- **Douglas–Peucker first, then corners merged pairwise while one arc fits**, the closest fit first: two
  neighbouring corners turning one way are one, standing where their outer legs meet. Each corner is rounded as wide
  as keeps its stretch of the survey within the tolerance, and no tighter than before. Kinks are eased first, on the
  line as surveyed; a normalised line one of whose corners its lanes would fold over is laid as surveyed (none on
  `OdesaOsm`).
- **Two things fell short on the way, both read off `--bench fidelity`.** A merged corner stands off the road, so a
  neighbour taking more of the leg between them tightened its arc out toward that corner, metres off: each corner
  now keeps the tightest radius that holds its survey, and a leg goes to those first. And an arc read only at the
  surveyed points passed every one and bulged up to 61 m between two of them, where a short outer leg sent the
  tangents' meeting a kilometre off: the arc is now read against the surveyed line as well.
- **What came out**: 40 291 arcs; 17.1 % of the length curved, radii 28.7 m at the median. OSM's lanes stand off
  the engine's at 0.15 m at the median (0.01 m), 66.9 % within 0.5 m (67.0 %), 85.0 % within 1 m (85.4 %) and
  99.7 % within 5 m (99.7 %); the shared single lanes 74.9 % within 5 cm (96.7 %) and 97.5 % within 0.5 m (97.9 %).
  750 ways are mostly more than a metre off (748). The streets are laid in 240 ms (108 ms); the boundary leaves
  79 carriageway runs open (97).

## 2026-10-04 — a traced map drops the road stumps that run into buildings

**The owner asked for the road stumps entering buildings dropped**: the engine lays nothing a road may run into or
under — a footprint stands nothing — and a dead end into a building is a way into parking the engine does not lay.

- **An edit to the map, and no rule of the engine's** (`TracedMap.Stumps`, `TracedMap.Without`, `qq osm --drop-stumps`):
  the engine still lays every road the map holds (GEN-57), and the map no longer holds these.
- **A stump is a dead end walked back to the first place three ways meet**, on through any place two ways only run on
  into each other, so a lane mapped as two ways goes whole. A road's last point past the frame is where it runs off
  the map and no dead end.
- **It runs into a building where its dead end stands inside a footprint, or 5 m or more of it does** — an arch into a
  courtyard; less is a road drawn beside a wall, which the survey's roads and footprints stand a few metres into at
  worst. Dropped again while dropping one leaves another; a road through a building between two streets is no stump
  and stays.
- **What it came to on `OdesaOsm`**: 1 512 of 4 878 stumps, 69.6 km — 1 395 of them up to 100 m long, 34 over 200 m,
  the longest a 905 m service road through the port's sheds. 14 195 roads became 11 382; the control and crossings
  standing on them went with them.

## 2026-10-04 — a road the map cuts runs off it

**The owner asked for the roads a crop cuts to run on to the map's edge, lanes and all, with the perimeter cut short
by the edge.** A traced road was cut on the rectangle 60 m inside the map, its margin, and ended in a dead end the kerb
turned round; where a dual carriageway's two ways were cut side by side, the merge left a notch that held the whole
outer boundary open.

- **Cut on the map's own edge** (`Survey.Inside`), and where only that road reaches the place, its junction runs off
  the map (`CityPlan.JunctionArrays.RunsOffTheMap`) and stands nothing off, so the lanes run up to the edge. It has no
  box, no station and no turn, as any dead end has none.
- **Its ground is laid on past the edge and cut there**, GEN-2b's rule for whatever is drawn through past the edge:
  each lane carried straight on until its whole width has left the map and `SimConfig.PastTheMapEdgeM` beyond
  (`LaneShell`, as the ground runs on under a deck), so the boundary turns round off the map; the picture cuts every
  layer at the map (`MapCut`) and strikes no kerb along the cut. The plan carries nothing past the edge.
- **What it came to on `OdesaOsm`**: 14 195 roads, the merge leaving 6 runs open, 7.5 km in all, against 7 and
  420 km with the cut ends turned on the map — the outer ring closes, and what is left open is the merge's own.
- **Not cut**: the walk lines struck off the boundary run on round the ends off the map. Nobody walks a traced map yet.

## 2026-10-04 — a traced map is its own binary file, imported once and edited in place

**The owner asked for the traced map to load from a compact binary file of its own, holding only what the engine
needs, with what was crawled kept as it came and every edit made to that file — and for it to be cut down to central
Odesa.** The engine read the scanner's 11 MB JSON extract, every tag, node tag, relation and road surface in it, and a
6 MB pack beside it, each checked against the other.

- **One file, the map** (`TracedMap`, `towns/traced/OdesaOsm.map`): each road's line, class, level, carriageway as OSM
  means it and width as measured, the coast, the turns, controls, crossings, footprints and trees; places
  in whole millimetres and a footprint's in centimetres, each a step from the one before, counts and ids as varints.
  **Facts and no rule**: `Survey.Of` still applies the rules, so retuning one needs no import. The whole city was
  6.0 MB against 17 MB, imported in 1.7 s, and lays the turns, lane links and arrows `--bench fidelity` read off the
  old files (1 439 of 1 451 restrictions, 10 of 10 links, 221 road ends).
- **The map is the master** (the owner's choice, over patches compiled into it): `qq osm --import` makes it once off
  the survey and the layers, and an edit changes it in place. A second import replaces it and every edit made since,
  so it is refused unless forced; the `.osc` is how the survey was imported, not where the map is edited.
- **What was crawled is kept as it came, in `towns/traced/OdesaOsm/source/`** (the owner's choice), where
  `.tmp/osm/` could be wiped at any time; the scanner keeps and reads every source's answer there.
- **Cut down to central Odesa** (`qq osm --crop 46.388,30.694,46.604,30.826`): the owner's rectangle round the centre,
  the Kotovskoho settlement, Moldavanka and Tairove, widened south and west until Tairove's main street (Nebesnoi
  Sotni) lies whole inside it and east to Kotovskoho's last buildings. 10.3 × 24.1 km and 91 839 footprints in
  3.0 MB, read in 18 ms and surveyed in 32 ms; a whole-map shot is laid and drawn in 20 s, where the whole city took
  116 s to open. A crop keeps the frame's projection and moves every place by whole metres, and a road past its edge
  keeps one point beyond the frame, which is all the engine reads of it.

## 2026-10-04 — a traced map puts right what OSM has wrong, in an osmChange beside its survey

**The owner held Tiraspolska Square's lanes against aerial imagery and found them wrong, and said the fault is
OSM's data rather than the engine's.** OSM draws the square's ring 7 m wide inside a ring painted 9 to 10 m wide,
Preobrazhenska north into it in four lanes where two are driven beside the tram and the kerbside parking, the
ring's east side as two ways 4 m apart where it is one carriageway the street drives along, and Karavanskoho with
no lane count, its line 3 m off its street's middle. The engine laid exactly that, as GEN-57 has it.

- **What OSM has wrong is put right beside the survey, in OSM's own edit format** (`towns/traced/OdesaOsm.osc`,
  `Corrections`). The scanner applies it over OSM's answer before it reads any lane or turn, so a re-scan keeps
  it, and no engine rule changes. An osmChange opens in JOSM over OSM to be looked at; it is not for uploading,
  since it was read off Google's imagery, which OSM's licence does not let be traced into OSM. A correction of a
  node or way OSM no longer holds is refused at the scan, so a fix made upstream is noticed and not laid over.
- **How it was read**: the imagery registered onto OSM's own low buildings with the island pinned — 16.85 px/m at
  0.95°, where OSM's tram loop round the ring lies on the ring's painted outer edge — and the paint read off it in
  metres. The ring is two lanes of 4.5 m between its painted inner line and outer edge, its east side straight
  where Preobrazhenska drives along it, joined 16 m south of the island's middle and left 8 m north of it. North
  into it are two lanes of 3.75 m. Nizhynska leaves it at its south-south-west beside the tram and meets
  Preobrazhenska where OSM divides it, a few metres short of the painted gore's tip. Karavanskoho is three lanes of
  3.75 m on its street's middle and on into the ring, and Preobrazhenska north of the lights is one lane.
- **Where a two-way road divides into two one-way ones at a shallow angle, one of them is drawn along its edge**
  (`placement=left_of:1`): both centred on the dividing node, their lanes overlap for metres past it, and the
  junction there, kept small by a driveway 9 m on, cannot part them, so lanes into one cross lanes out of the other.
  Nizhynska's line is its gore-side edge, half a metre inside the tram there, so its lanes lie beside
  Preobrazhenska's southbound ones and clear of its northbound ones.
- **A node OSM shares with a way the survey does not hold stays on that way's line**: a tram track's or a
  footway's crossing is placed where its way crosses the corrected road, and the enrichment takes every node the
  survey holds from the survey (`Town.Surveyed`), so a track keeps its shape and meets the road at the node they
  share. Where OSM keeps a road's line just beside a tram up its lanes, the correction does too: it lays no new
  crossing of a road and a track without a node (`overpasses`), and the meta-check passes.
- **What is still not as painted**: a gore painted on the tarmac is laid as a kerbed island, since the engine lays
  ground only under a road, and no parking lane is laid.

## 2026-10-03 — a traced town lays its trees, roundabouts and roof heights

**The owner asked for the rest of the metadata applied.** What is laid is what the plan already had a place for,
or what is drawn within a layer the ground already has; what would be a new layer of the ground waits on the
owner, TER-7b fixing the stack (`P0`).

- **Trees are props** at the one size only trees are drawn at (GEN-6b). **A tree's crown is its collider**, so one
  within a lattice step of driven ground is not laid — the world refuses furniture on a driven ribbon (TER-4c.4):
  of 1 892 trees OSM maps, 1 161 stand and 731 do not, most of them in a street's planted verge narrower than a
  crown. Benches, bins and bollards are not laid: a prop is a placement and its look is the catalogue's draw, so a
  bollard would be drawn as a traffic cone as often as a bollard.
- **Roundabouts are the circulating ways**, a ring a component of them at their junctions — membership, as a
  generated one is. Circulating is part of a traced carriageway, so a ring is never one road with a way off it.
- **A roof is lighter the taller its building**, a quarter lighter at ten storeys and above — within the
  footprints' own layer, whatever the owner decides of where that layer stands.
- **What waits on TER-7b**: zones (parks, industry, squares, car parks) as ground, an unpaved road's surface, and
  OSM's own pavements — each a layer of its own, or a carriageway that is no longer one shape.

## 2026-10-03 — a traced bridge is its own road, on the level above

**The owner asked for bridges, their cars crossing the cars below without a collision or a claim.**

- **A bridge is a road of its own** (GEN-14a): a bridge way's ends are places of two arms, because a level is part
  of a carriageway and a carriageway that changes is a place (GEN-51). So a level is a road's
  (`CityPlan.RoadArrays.Level`) and a lane's, and never a stretch of one; the deck runs the whole road at its
  carriageway and a walk either side (TER-3b).
- **Every OSM bridge is the level above**, whatever its `layer`: what is under it is the ground's, and a bridge
  over water alone has nothing below to keep apart from. A generated town lays no level: its bridges span water.

## 2026-10-04 — a traced bridge is drawn above the road it crosses, and cars stand at a few

**The owner asked for bridges over roads rendered above as a separate road, and a few cars near them.** The ground is
now a level at a time (TER-7b, at the owner's word — [terrain](../../world/terrain/docs/decision-log.md)), so a
bridge's deck is no longer one carriageway with the road under it.

- **Cars stand at the four widest crossings** (`TracedBridgeCars`, `CityGen.TracedBridgesWithCars`): one each way on
  the bridge over the road it crosses and one each way on that road a deck and a car short of it, ranked by the
  narrower of the two roads. A car is a car of the map's own: nothing parks in a traced town, so it is driven off by
  the rule a map with nowhere to park on drives its own (CAR-8). On `OdesaOsm` three of the four are the two
  carriageways of one trunk over another, so the cars stand at three places.

## 2026-10-03 — a traced town is laid with what else is known of its place

**The owner asked for the known data applied to the map** — roads more precise and turns correct, the
generated zebras dropped and the known ones placed, lane counts estimated from width where OSM gives none, the
buildings' polygons without texture — off processed data that loads quickly (GEN-57).

- **A road is as wide as it was measured**: 5 149 of 21 413 ways — 4 803 off imagery, 309 off a mapped surface,
  37 by tag. **Imagery is not taken under a service road, a track or a link**, where it reads 8.2 m at the median
  for driveways, and **a width is taken only where its lanes come out between 2.75 and 8 m**. The upper bound
  came from measuring: mapped surfaces up to 67 m lay under single-lane service roads — a square, a car park —
  and the widest lane is the reach every kerb end is searched over, so the ground took 24 s longer to lay.
- **Where OSM assumes the count, the width says it**: as many lanes each way as the width holds at OSM's assumed
  3.5 m, between OSM's assumption and the most a way of its class is tagged with on the map (residential 3,
  tertiary 4, primary 6). 353 ways take it. A way with any lane entry tagged keeps OSM's count.
- **Lights where the pack reads signals**: 500 junctions lit of 802 controls; a cluster — a dual carriageway's
  crossing — shares one clock, started at a place read off its node so no seed draws it. Signs, a priority road
  and a roundabout's control are carried and not laid: the engine has no sign.
- **The zebras are the survey's**: no station is painted on a traced town (`CityPlan.ZebraAtEveryStation`, WLK-10)
  though every station still cuts the walk and holds the traffic, and 1 341 zebras stand where OSM maps a painted
  crossing — painted where its tags say, or untagged with lights for its walkers — across the road its way runs
  along. They are paint only: filed under no road end, they hold nobody.
- **The buildings are their footprints**, 205 659 of them — OSM's with their courtyards, and the machine-traced
  ones OSM lacks a shade greyer — 695 000 triangles laid in 0.1 s, flat, under the carriageway so a road through
  an arch shows. They stand nothing.
- **The turns were already OSM's, and are**: `--bench fidelity` lays 1 439 of 1 451 restrictions (the other 12
  are at nodes no junction stands at), all 10 lane links and the arrows into 221 road ends, and no connector makes
  a turn the plan forbids. What changed is the lanes a turn is made from (TER-5j), now as many as the road holds.
- **What it cost**: the open is 116 s against 111 s, nearly all of it standing the world up — more lanes, more
  walk. **The merge leaves 118 carriageway runs open against 27** (merged 2 against 0): where a street read 11 to
  20 m wide meets a narrower one or a driveway a few metres on, its kerb notches at the corner — a defect of the
  merge the even widths never showed, and the merge's to close. Against OSM's own lanes, 15.6 % of carriageway
  lanes now stand within 5 cm of an engine lane, where an earlier entry read 99.7 %: a lane as wide as its road
  was measured is not where OSM's assumed 3.5 m puts it.

## 2026-10-03 — where sources describe one thing, one record answers and names its source; and the layers are queried off an index

**The owner asked for the new sources merged, and for tooling to query the result fast.**

- **A road has one width**, `widthM`, and says where it came from (`widthFrom`): its tag, the mapper's word; its
  mapped surface, OSM's own outline; the surface read off imagery along over half of it and under it alone; else
  what its lanes make it. The evidence stays beside it. Imagery is below OSM's surface because it is older and reads
  parked cars as road; above the lanes because a lane count is OSM's default as often as its word. 2 063 km of
  street takes the imagery's, 548 km its lanes', 57 km a surface's and 4 km a tag's.
- **A camera's sighting is a control or a crossing where OSM maps none, and only there.** A light, a stop or a
  give-way sign is given to its junction as OSM's own nodes are — the one ahead of the traffic it faces, where its
  facing is known — and decides the junction's control only where OSM maps no signal, sign, roundabout or priority
  road (`controlFrom`: `osm`, `seen`, `rules`); a zebra or a pedestrians' light is a crossing where OSM maps none
  within 15 m. OSM is the survey's own moment and a sighting is a picture of an older one, so OSM decides first.
  Osmose's relay of Mapillary's signs (item 8300) is a sighting too, read once where Mapillary's own stands: it
  signals 14 junctions OSM leaves to the rules. It relays only what OSM lacks, so it is never weighed against OSM.
- **Roads OSM lacks are their own layer, `unmapped`**: the unmapped stretches joined where their ends meet, 1 160
  roads and 193 km, 992 long enough to flag, 737 meeting a survey road. They are never laid in `roads`, which is the
  survey's.
- **`qq meta` asks the layers questions off an SQLite index** in `.tmp/meta/`, built in seconds on first use and
  again whenever the layers are written: every item by layer and id, its place in an integer R-tree, its names
  folded, and every id it names — so everything naming a road is one lookup. A question answers in tens of
  milliseconds against seconds to read the files. It is derived and rebuilt, so it lives in `.tmp/`, not beside the
  layers.

## 2026-10-03 — a traced road's detail is read off every open source that reaches it, each beside OSM's and never over it

**The owner asked for the enrichment to take what other online sources hold, road detail first.**

- **A road's width as imagery shows it** (Microsoft's Road Detections, ODbL): the region's 1.4 GB zip is inflated
  once as it streams in and its Ukrainian rows over the map kept (`MlRoads`). A stretch is laid on the survey road
  running along it within half its width and 5 m, and on any other carriageway under its surface, so each way of a
  dual carriageway says it was read across two. **The width is the paved surface kerb to kerb, kept beside the
  lanes and never in their place**; a street read 8 m wider or 3 m narrower than its lanes make it is flagged — most
  are one-way central streets tagged one lane that read 14 m. **A stretch beside no OSM way is flagged, never laid
  as a road**: the survey says what the roads are, and the imagery is older than it.
- **A road's height is the ground's** (GEDTM30, CC BY 4.0), the surface model kept as a reading against it. The
  terrain is one 430 GB global file of which the map needs one block, read by range and kept as a GeoTIFF of its
  own. Its metadata says a scale of 0.1 its samples do not carry: they read in metres, the surface standing 1.0 m
  over them at road nodes at the median and none of them more than 5 m under.
- **Each road says its OSM version and the day it was last edited**, asked as of the survey's moment by `convert`,
  so who edited it is never asked for; and Osmose's lane, near-junction, access, tag-conflict, number and cycling
  items join the quality layer.
- **What Mapillary's cameras saw is laid when a client token is given** (`MAPILLARY_TOKEN`, sent as a header and
  kept nowhere): every sign, light and marking on its road and junction, with whether OSM maps the same near it.
- **Not taken**: FABDEM, whose licence forbids commercial use, for GEDTM30, which does not; GlobalBuildingAtlas's
  heights, non-commercial too, until the owner says the map never ships for money; Overture, which in Ukraine is
  OSM and these same footprints; Panoramax, 15 pictures over the map. The city's own registers, live transit and
  located accidents are not published.
- **What came out**: all twenty-two checks hold. 79 % of street length has a width read off imagery, against 426
  roads measured off a mapped surface before; where both exist the imagery reads 1.09 of the surface at the median.
  193 km of road on the imagery has no OSM way beside it. Half the street length was last edited within 2.9 years;
  22 % not for over five.

## 2026-10-03 — a traced map's enrichment is one source: one OSM moment, its sources laid as one, and a check that says so

**The owner asked whether the enriched data holds together and its sources agree, to reference it as one source.**

- **`qq osm --meta-check` asks it** (`Audit`), off the files as a reader reads them, and `--meta` runs it last:
  every id one layer gives for another resolves and every fact two layers carry is the same in each
  (`Integrity`), every OSM layer stands at the survey's moment (`Snapshot`) — a broken one fails the run — and,
  as readings, how far the sources describing the same things agree (`Agreement`). It writes `consistency.md`.
- **Its first run found the enrichment's own defects**: the city's GTFS prints 779 stops, 439 trips and 33 161
  calls twice and lists 2 routes under two agencies, and each was laid twice; a lot derived round loose aisles
  left them among the roads of the zone it took them from (161); a likely pavement side dropped the share of it a
  drawn pavement runs (209); a way drawn through its own junction twice gave two arms one name (107); and **the
  OSM layers stood three hours after the survey**, 10 nodes moved and 2 retagged between.
- **Every OSM family is asked as of the survey's moment** (`[date:…]`), its answer kept under that moment, so a
  node the survey and a layer both hold is one node. Osmose and the timetable stand at their own moments, which the
  manifest names; Osmose's elements are read against the layers' tags and none has been edited between.
- **Where two sources describe one thing, one record holds both and says which it takes.** The timetable is laid
  onto OSM's stops and routes — OSM the map, the timetable the service — and what OSM lacks is laid beside them, so
  the separate timetable layers are gone. A crossing is read off its node and its way together, and its junction
  takes that reading. A footprint OSM lacks is moved onto OSM's frame by its neighbours' median shift, measured
  1.4 m east and 0.6 m south over the town and 0.4–3.8 m east by district. Every disagreement left — a crossing's
  node and way, a pavement tag and the pavement drawn, a width tag and the surface, a height and its levels, an
  address and the streets drawn, a timetable stop or route OSM lacks — is a flag in the quality layer.
- **A footprint OSM lacks that stands a third or more on an OSM building or a street's carriageway is not laid**:
  OSM says what is there, and the model traced part of the building or the street's edge. A service road or track
  through one is a driveway into a yard or garage block, as it is through an OSM building.
- **What came out**: all twenty checks hold, over 309 000 items and 350 000 references. Of 189 348 footprints, 2 640
  more stand on an OSM building once moved, 671 overlap one and 2 121 lie on a street, and 143 719 are laid; a
  neighbours' shift leaves a footprint 1.9 m off its OSM building at the median, against 2.7 m as traced. Of 1 685
  timetable stops served, 1 263 lie on OSM stops 7.8 m off at the median and 422 are laid of their own; of 134
  timetable directions, 61 lie on OSM relations and 73 are laid of their own. The timetable's lines run 0.7 m off
  OSM's roads and 1.4 m off its tram track at the median. 2 of 152 crossings tagged on node and way still disagree.

## 2026-10-03 — a traced map has everything else known of its place beside its survey

**The owner asked for Odesa's data to be enriched off every source there is — lanes and turns, road size, zebras,
buildings, pavements, junction control, levels, car parks as zones — as data, with the game left as it is.**

- **It is written beside the survey and not into it**: `qq osm --meta` (`src/tools/osmscan/meta/`) writes
  `towns/traced/OdesaOsm/`, a JSON file a layer with a `manifest.json` and a `coverage.md`. The survey's format is
  unchanged and the folder holds no survey, so nothing lists or publishes it.
  Every place is OSM's 1e-7°, every road the survey's way by id, so a layer outlives a re-read frame.
- **Sources**: OSM through Overpass in ten families, a heavy one a tile at a time so a refusal costs a tile — the
  main server turned away two asks in three while busy, small or large, and the second mirror stopped answering, so
  the enrichment asks the main one alone and patiently; Microsoft's machine-traced footprints (no heights in Ukraine);
  the Copernicus DEM at 30 m; Osmose, whose item 8300 carries the signs Mapillary's cameras saw that OSM lacks; the city's
  GTFS through the Mobility Database (licence unverified); open OSM notes. **Out of reach**: Mapillary itself (a
  token), and every geodata service of the city council (portal down, site behind a browser check, cadastre closed).
- **An Overpass answer reporting a runtime error is refused** like a stale one, for the survey as well: a query
  out of time or memory answers 200 with whatever it had.
- **Read is kept apart from inferred, and each says which.** A junction's control is what is mapped at it or
  before it — a signal walked to the junction ahead of the traffic it faces, a cluster of near junctions one — and
  an unsigned one is read by ПДР 10.2 and 16.11–16.12; a movement's lanes are its arrows where painted, else ПДР
  10.4. A pavement is tagged, drawn apart (matched to a road and side by where it runs), or `likely` where a
  street's buildings leave room for one. A road is a zone's way where it is a car park aisle or a service road
  mostly inside a lot, fuel station, garage block or other place, and aisles no lot holds are given their hull.
- **What came out**: 21 511 junctions — 486 signalled, 161 signed, 126 on a roundabout, the rest unsigned, 50 of
  those with a signal sighted near them — and 197 736 movements, 1 761 forbidden and 23 forbidden at times; 1 782
  pedestrian crossings, 860 painted; 3 079 roads (481 km) a zone's ways, beside 1 121 mapped lots and 108 derived;
  61 895 OSM buildings and 143 719 footprints OSM lacks, mostly private houses at the edges; 1 998 ways off the
  ground, 1 368 of them roads through a courtyard arch. **Pavements are the thin layer**: of 5 344 km of street
  sides OSM says 252 km, frontage makes 2 107 km likely, and 2 985 km stay unknown.

## 2026-10-03 — a traced junction's turns are OSM's arrows, restrictions and lane connectivity

**The owner asked for OSM's turn information to be used where lane connections are made.** The extract held
it and nothing read it: 570 lanes with `turn:lanes` on 218 ways, 1 508 turn restrictions and 9 lane
connectivity relations, while every junction made every turn its geometry allowed.

- **The scanner reads them, as it reads lanes** (`OsmTurns`), so the engine still interprets no tag: a lane's
  arrows, and for a car the restrictions and connectivity at a node. 1 451 restrictions and 10 lane links (off
  4 relations) are read; of 62 relations not read, 21 are in force only at times, 18 lack a way from or onto,
  13 turn over a way, 9 name a way not on the map and 1 a node off its ways. The extract is otherwise the same
  bytes, from the same OSM base.
- **A time-limited restriction is not laid**, the map keeping no clock of day, and **neither is one over a
  way**: a U-turn across a median is two junctions, and forbidding its first turn forbids everyone else's.
- **A way's arrows are for the junction it ends at** (Key:turn), so a road ending where its way runs on is
  unmarked there. **A marked lane makes only what its arrows name**, among what the junction offers; a lane
  whose arrows the junction offers none of is made from as an unmarked lane, so a mapper's mismatch leaves no
  lane with nothing; and a turn no lane of a marked arm names is not made from it. A slight turn is its side's
  turn where the junction makes one and straight on where it does not, since the angle a turn is classified by
  calls a gentle fork straight.
- **A forbidden turn is left out before the arm's lanes are shared**, so the lanes go to the turns there are.
  A restriction is laid where a junction stands at its node and each way has one road end there.
- **What came out**, `--bench fidelity`: 1 429 restrictions laid as 1 885 forbidden turns, the other 22 at nodes
  where two ways merely carry on; all 10 lane links; arrows on the lanes into 216 road ends; 1 149 fewer
  connectors. **6 lanes are left with no turn, every one OSM's own**: a living street every turn off which is
  forbidden, a primary forbidden left, straight and back at a node, and a one-way street forbidden straight on
  with no other way out. They are laid as OSM has them.

## 2026-10-03 — a traced road leaves its junction on its survey's line, and a kink keeps the legs beside it

**The owner asked for the map's road curvature to be checked against the most detailed OSM there is.** The
extract was that: 100 ways and their 4 615 nodes, read back off the OSM API itself, stand at the same 1e-7°
integers with the same node lists, and Overpass knew no road way or node changed since the extract's base.
What fell short was the laying, twice.

- **A road left its junction on the chord from the centre to its first point outside the disc** — GEN-46's
  arm, which nothing reads of a traced plan. A way bending inside the disc had its whole first leg swung off
  the survey: a 171 m driveway whose first node stood 6.3 m from its junction was laid 6 m off end to end.
  **It now leaves where the survey crosses the disc's edge, on the survey's own heading.**
- **A kink its legs could not round was taken out**, which swings both legs onto the straight between their
  far ends: a street's 87 m leg ended 2.2 m off where three nodes 1.5 m apart turned it through 92°. **A kink
  is now eased the way that stands least off the survey** — taken out, a point beside it taken out, or it and a
  neighbour carried on along their outer legs to where those meet — read at the points one line has and the
  other has not.
- **The boundary then left 1.8 m open** at a hairpin ending inside its own leg out (TER-7b), which was the
  merge's and is the kernel's log.
- **What came out**, `--bench fidelity`: 99.7 % of the lanes of a carriageway within 5 cm of OSM's (98.6 %),
  98.8 % of the shared ones (97.4 %), 94.5 % of all 8 009 km (93.3 %); 205 ways, 3.7 km, mostly more than a
  metre off (310, 13 km), every one a short way the junctions' discs take most of. Of the engine's own lanes
  99.6 % stand within 5 cm of an OSM lane (98.4 %), and no road is mostly off one (186, 13 km).

## 2026-10-03 — a road's lanes are OSM's own, read by a scanner that knows no engine rule

**The owner asked for the engine's lanes to be OSM's lanes, all of them**, read off the most precise thing OSM
holds by a scanner apart from the game and with none of its rules. The port before it laid every lane at the
engine's own width (3.6 m, a car's width and four fifths), split a single lane both ways share into two full
lanes, read `lanes` and nothing else, laid 13 classes of OSM's 20 — 10 639 of its 21 976 lanes — and was
measured centreline against centreline, on the classes it laid.

- **The scanner is a tool of its own** (`src/tools/osmscan/`, `osm-scan`, run by `qq osm`), compiling the
  extract's format and OSM's lane rules from the engine's own files, so what it writes is what the engine
  reads. It asks Overpass for the roads, their tagged nodes, restriction and connectivity relations, the
  `area:highway` surfaces (298 in Odesa) and the coastline, and **refuses an answer more than two days behind
  OSM**: a mirror answered at once from data four months old.
- **A way's lanes are OSM's tagging rules and nothing else** (`OsmCarriageway`): Key:lanes for the counts and
  its assumptions where untagged — one each way on a main or residential street, one shared on any other
  two-way way, two on a one-way motorway or trunk; `width:lanes`, else `width` shared, else 3.5 m, the width
  OSM's own lane renderer (JOSM's lane_features) assumes, OSM stating none; the placement proposal for where
  the way lies across them; `turn:`, `change:` and `psv:lanes` kept lane by lane. 2 369 of Odesa's 15 898 road
  ways tag their count, 94 a width and 25 a placement. **A count more than both sides claim is lanes driven
  both ways**: 22 primary ways tag six lanes, two each way, and give the middle pair a direction only by the
  hour (`lanes:forward:conditional`), and read as four they lost 7 m of carriageway each.
- **The frame is the scanner's as well**: about the middle of the boundary's roads, their extent and 60 m. So
  `TracedEdgeMarginM` left `SimConfig`, and `Survey.Of` reads no tag of a lane.
- **The boundary draws the rectangle and every road in the rectangle is laid**, inside the city's limits or
  not: read off the boundary alone, 5 526 road ways of the suburbs stood inside the map's own edge as grass. A
  way crossing the rectangle is taken whole and cut by the engine where it crosses; a crossing nearer the node
  inside than the shortest road a traced map lays is that node, since one 18 cm past a junction left a road too
  short for the junction to have a disc.
- **The engine lays what it is given, every class of it**: each road at OSM's carriageway width and along its
  middle, and a place wherever the width or placement changes as well as the count. `service` and `track`
  are 11 279 of the 15 896 road ways — driveways, aisles, courtyard lanes — and are laid like any street; a
  road drawn as an area is a surface, and has no lane.
- **A single lane both ways share is one lane each way over one line**, as a bay's are: 2 024 km of OSM lane,
  most of it service roads. So "over one line" is no longer "a bay": `CityPlan.RoadArrays.SharedLane` marks the
  traced ones and `LaneLines.LaneIsBay` is what the bay's own readers ask — the spawn, the tour, the routes and
  the closures.
- **The boundary did not close over them, and two things in it changed** (TER-7b). A lane and its reverse over
  one line were merged as two bands whose ends lie on one another facing opposite ways; at a dead end nothing
  else covers, the merge kept half of one end and neither half of the other. They are one band now
  (`LaneShell`). And a road of one lane on its line rounded down to the lanes' floor folded its own ground,
  with no lane beside it to cover the fold; its floor is half its carriageway now.
- **Measured lane against lane, every OSM lane**: `--bench fidelity` holds each against the engine's lanes and
  connectors running the same way, whatever was laid, and names every way that is laid nowhere near, which a
  share of the whole would hide. Of 21 414 ways, 29 976 lanes and 8 009 km, 93.3 % lies within 5 cm of an
  engine lane: 98.6 % of the lanes of a carriageway, 97.4 % of the shared ones, and 57.1 % of the 928 km
  crossing a junction, where OSM's lanes run straight to the node and the engine's connectors turn. 310 ways,
  13 km, are mostly more than a metre off — every one of them laid, bent near a junction. 98.4 % of what the
  engine lays is within 5 cm of an OSM lane.
- **What was checked and is not laid**: in the rectangle every way of every road class is in the extract, 8 of
  them lying wholly in its margin; footways, paths, steps, platforms, cycleways and pedestrian streets are not
  roads, though 14 cycleways and 4 pedestrian streets carry lanes or motor access; cycle lanes on 466
  carriageways and parking lanes on 28 are not lanes Key:lanes counts.
- **`BigOdesa` went back to `towns/disabled/`**, the owner calling it broken: a minute of it ran past ten
  minutes and the maps tier with it.

## 2026-10-03 — the survey is OSM's own extract, and the port loses nothing of it

**The owner set the goal as the OSM map as close as it can be got, everything else second, and the first
port of it lossless.** Two things fell short of that, and both were measured before either was changed.

- **The survey kept less than OSM holds.** It wrote projected metres rounded to a centimetre, no node or way
  ids, four tags of a way's dozens, no tag of any node, no turn restriction and no island. It is now an
  `OsmExtract`: OSM's integers of 1e-7° as Overpass prints them, every id and tag, tagged nodes, restrictions
  and the coastline ways raw — 63 606 nodes, 15 898 road ways of every vehicle class, 62 coastline ways, 6 780
  tagged nodes and 1 380 restrictions, 5.5 MB. Read against the old survey it is the same 4 607 laid ways with
  the same tags, every point within 8 mm of where it stood after a frame shift of 24 m. The old extent was read
  off the projected corners of the lat/lon box and the new one off the points.
- **The port threw the place away to suit the generator.** Places within 30 m were one junction (GEN-16), a
  line was thinned to 1 m, a corner was rounded at its class's design radius (GEN-47), a corner its legs
  could not round at a car's cornering floor was cut, and every piece not joined to the largest went (GEN-5).
  Measured centreline against centreline, 61.9 % of the surveyed length lay within 5 cm of the town's lines,
  94.8 % within a metre, and 6.5 km nowhere near any. **None of those rules is asked of a traced town now**
  (GEN-57): a junction at every place, a junction's disc shrunk so the road to a close neighbour is short
  rather than gone, and every piece kept.
- **The corner floor is the lanes' and not a car's.** A traced road may not fold a lane back over a corner,
  so the floor is the innermost lane's offset plus `TracedTightestLaneRadiusM`; how tightly a car can be
  driven round it is the drivers' to answer later. A hairpin drawn to a point — a U-turn spur whose two
  one-way legs run 2 to 5 m apart — is still rounded short of its tip, because no lane can turn round between
  legs closer than that.
- **A leg is shared between its two corners by what each needs**, not half and half: a sharp corner beside a
  gentle one takes nearly all of the leg between them (`Spline.RoundedInto` with a reach a corner).
- **A ring of one-way ways joined to nothing else had no place on it to be walked from** and was never laid —
  a turnaround loop whose service-road links are not laid. One of its points is made a place.

**What came out**: 98.2 % of the surveyed length within 5 cm of the town's lines and 99.7 % within a metre;
the worst five places are those hairpins, 10 to 24 m short of their tips. 7 302 roads, 5 065 junctions and
15 733 lanes, laid in 1.4 s and stood up in 8.7 s, with a ribbon atlas of 848 MiB. **The walk's junctions
needed one bound to stand it up**: an outer kerb ring passes a junction twice on a street running out into a
tree of dead ends, and a junction owned the 13 km of kerb round the tree between its two hand-overs, longer
than the atlas can file. A stretch a junction owns now stays within its hand-overs' reach of it plus a merge
(`CrossingWays.StretchOf`), and one that does not is counted as a connection no turn joins (WLK-14).

## 2026-10-02 — a real city is traced off its survey, and nothing in it is drawn

**The owner asked for Odesa as it is**: its roads and its sea line, at one to one, with nothing standing on it
for now, no seed and no road the engine made up — and stored, not fetched (GEN-57). So a third kind of map
joins the brief and the code: a survey in `towns/traced/`, written once by `qq osm` off OpenStreetMap and
laid by `TracedPlan` when the map is opened.

- **A survey is not a town carried as a file.** It holds OSM's ways and the sea, which are what was authored
  about the place; no junction, arc or lane is in it, so the 2026-09-14 entry's reason — a stored town is a
  second answer to where the town is — does not reach it. Retuning a figure relays the town off the same
  survey.
- **Stored rather than fetched at open**, because OSM moves under a map and Overpass is a busy public server:
  a fetch at open would be a different town on a different day, and none at all offline. `qq osm --refetch`
  is how it is moved deliberately, and the survey says which OSM moment it is.
- **Topology is OSM's shared nodes and nothing else.** Two ways meet where they share a point; a bridge over
  a street shares none and meets nothing, which is what the place is.
- **What GEN-5a, GEN-13, GEN-18, GEN-49 and GEN-50 refuse is the city** — dead ends, shallow arms, a
  carriageway beside its twin — so they are not asked of it. 613 dead ends carry 100 km of Odesa's 993 km, and
  pruning them as the generator does would delete a tenth of the place.

**`Spline.RoundedInto` laid a straight with a bearing of noise.** Two corners that all but share a leg leave
a sliver of straight between them, and its bearing was read off its own two ends — at 15 km from the origin a
float's step in any direction. The lane offset off it jumped sideways and the merge could not close round
it: half the traced town's open runs. A straight now takes its leg's bearing, and its length along it.

## 2026-10-01 — where people live, the plan stands no car

**Every car of a town people live in is somebody's** (PER-29, the person slice's log). Which bay a person's car
stands in is the free one nearest their door once the services' aprons are held, which is a question for the
town's parking registry rather than the plan — so the spawn stage stands no car where it stands people, and the
brief's `cars` is read only for a town nobody lives in (GEN-7). The shipped briefs keep the field: a car count is
what a town of no people still asks for.

## 2026-09-30 — a car park is a rank of bays laid off the kerb, joined to nothing

**The owner asked for car parks not to be junctions any more** (GEN-53; the parking slice's log has the
manoeuvre). A car park no longer parts its street: each bay is a short road of its own, laid square off the kerb
at the street's own lane, with a node a `ConnectionStandoffM` lead off each end (`TownLayout.Stand`).

- **Every bay owes the locality at both nodes** (GEN-16), the one over the street too: checking the far node
  alone let a bay's near node stand inside another junction's locality across the street.
- **A bay stands back from the kerb behind an apron** (`CityGenFigures.BaySetbackM`). Laid flush, the walk
  wrapped each rank's end as a notch the outset could not close — open runs on every shipped city
  (TER-3c.8). The apron is also what a car swings across rather than the street.
- **Its mouth runs back over the street's ground by half a ribbon touch** (`SimConfig.CarParkKerbOverlapM`),
  measured off the kerb actually under the mouth, so the rank and the street are one piece of tarmac on a
  street that bends a little and share no more ground than that on one that does not (TER-5c).
- **The kerb ends round into a car park rather than stopping at it** (`KerbEnds.RoundsIntoAPark`): a rank's
  mouth is no road end, and read as one it stood stray zebras and lights at car parks.
- **`BayTurnInParkingCircles` is retired with the turn it sized.** Seven tenths of the parking circle was the
  owner's call for a turn laid with the town; nothing is laid with the town now, and each car's manoeuvre is
  shaped on that car's own circle, which the owner asked for in as many words ("each car might do this
  differently depending on stats").

## 2026-09-30 — a hospital, a police station and a depot in every district

**The owner asked for every district to have its own services** (GEN-56). A town had a share of its buildings
as services, capped — Odesa six hospitals, four stations and two depots — spread over the whole town, and
nothing knew a district once the streets were laid. **The plan now carries the wheel the districts were laid
on** (`CityPlan.Districts`), and each district is cut a yard for each use inside it; the shares and their caps
are gone. **The yards are cut on top of the town's own count** rather than out of it: taken out of it, the
suite's town of fifty car parks gave eighteen to its six districts' services and a third of its parking with
them.

**Inside is asked of the building's ground and not of the road.** Asked of the road, two of the suite's town's
eighteen stood across a spoke in the next district — a district's edge is as often as not a spoke or the
orbital. Asked of the road and the building both, a small district ran out of sites for its third yard; a yard
on a boundary road facing into the district is that district's, so the building's two faces are what is
asked. **A district the town never reached stands nothing**: two of River's outer sectors hold three
junctions between them.

## 2026-09-29 — a car is stood in a bay again, and on a lane only where a town cut none

**The spawn stage stands the brief's cars in the town's own car parks** (GEN-7), spread over their bays as it
spread them over the lanes, now that a bay can be driven out of ([parking](../../world/parking/docs/decision-log.md)).
**A yard is not one of them** (GEN-55): its bays are its service's apron and are claimed before the plan's cars
are stood (GEN-4k), so a car stood in one was a car the town moved elsewhere or did not stand. **A town that cut
no car park keeps one car a lane** — the fixture, which asks for no buildings and so for no parking.

## 2026-09-28 — which junctions are lit is drawn again, on a stream of its own

**The roads stage lights the brief's share of the junctions that can carry lights** (`LitJunctions`, TLT-3),
on the signal stream it had before the lights were taken out (GEN-11) — so bringing them back moved no road,
building or prop of any town. Why the draw is exact and weighted is the
[trafficlight log](../../agents/trafficlight/docs/decision-log.md). **The plan carries no bars any more**
(`CityPlan.StopLines`): the town lays its own off its kerb ends, and the scenario map's second laying of the
same bars went with the field.

## 2026-09-28 — every district lays straight streets and wandering ones

**No street in the town was straight.** Every arm was jittered off its chord (`ConnectionJitterDeg`) and
the two ends of a link drawn apart, so every road was a biarc; half a strict district's streets took a
virtual node besides, a metre off the chord. Odesa was 83.9 % curved by length, and nearly all that was
straight was bays. **The owner asked for a grid's streets to be straight or mostly straight, about four
fifths of them — and then for every district to carry both kinds**, a grid laid all straight and a loose
district all wandering reading as two towns stitched together.

- **Laid straight is a fact about a road, not a kind of link** (`LayoutEdge.Straight`,
  `RoadArrays.LaidStraight`): the arms take the chord, the line wanders nowhere, and the plan carries it
  because the lanes draw the arms again. It borrows nothing from a bridge's settled bearing, being the
  owner's call that it stand alone; a bridge may take it up later.
- **A district lays a count of straight streets, not a chance of one** (`Lattice`, `District.StraightShare`):
  its share, drawn near its kind's (0.9 for a grid and 0.3 for a loose district, 0.08 either way, set by
  hand against the four fifths asked for), rounded and held off all and none. Drawn street by street, a
  district at 0.97 laid seventy straight and none that wandered. Which streets is each one's own keyed
  draw, ranked. A street that is not straight wanders like any other, wherever it runs.
- **A joined road is laid the way most of its length was.** Straight only where every piece was, a
  district's edge (a run of six or eight pieces) came out wandering however straight its district.
- **A corner a straight road was joined through is rounded rather than passed** (`Spline.RoundedInto`):
  straight legs, the corner at the class's own radius, tighter only where the legs are short, and refused
  under the junction's floor as before. A biarc through the corner's point bent the whole of both legs,
  and left grid streets 65.9 % straight on Odesa when they were all laid straight.
- **A yard now faces the town's middle** (GEN-55). Straight edge roads carry car parks, and furthest-first
  cut Odesa's sixth hospital yard into one at the map's west edge. The side was drawn, so its rank faced
  off the map and the hospital stood nowhere. The draw is gone with it.
- **Turning the jitter off everywhere was tried and not taken.** It moved the exam's own lanes, and it
  left one carriageway run and one walk run open on Odesa.
- **Snapping near-straight arcs onto lines was not an option.** The gentlest radius Odesa carried was
  9.3 km, and at a p90 of 1.8 km a hundred-metre piece bows 0.7 m. A line laid in its place moves the
  carriageway and breaks the bearing the arm was drawn on.

**What it came to**, by length of street on straight pieces: Odesa's grids 70–90 % a district and 80 % over
all of them, its loose districts 20–34 %; River's grids 74–94 % and 80 %, its loose districts 21–26 %.
River's smallest grid, eighteen streets, lays all of them straight: its one wandering street was joined
into a run laid straight. Curved length over the whole network: Odesa 84 → 35 %, River 85 → 47 %.

**Car parks are the town's gain.** A car park needs a straight stretch to be cut into, and Odesa cut
483 of the 600 it asked for where it had cut 217, and River 285 of 550 where it had cut 138. Odesa stands
2328 of its 2400 buildings where it stood them all, the ranks taking face the buildings had, and River 1689
of 1703. The town opens somewhat slower for its extra bays (Odesa 1.8 → 2.2 s). **The tick is not the
reason for any of this.** A car on an arc pays a few `SinCos` and an `Atan2` more than one on a straight,
all of it under a tenth of a profiled tick, and with every arm on its chord Odesa's cars phase read
408 → 397 µs, inside the probe's own spread.

## 2026-09-28 — the ground's layers are filled from rings shut across what a move left open

**A layer is a region, so it is filled from closed rings** (`GroundRings`): what the outset leaves open is
shut across its holes (`ArcRings.Shut`) and handed back beside the rings as the fault it still is. Filled
from the closed rings alone, one hole in the ring round the outside of a town took every square metre of
its carriageway with it — the ground read grass on every road, props were stood on the lanes and the world
refused the town. A town thirty kilometres long at Odesa's density left its ring round the outside open by
3.5 m, and Odesa's brief ten times over each way left 88 runs of its carriageway open. **River moves by it**: its one open run of walk is now walk, and a prop or two with
it. Odesa and the fixture have none and are the same to the bit.

**A place on the face asks the ranks near it which owns it** (`BuildingStage.Ranks`), in the list's own
order: asked of every rank, placing the buildings was the town's stations times its car parks, 129 s of a
town thirty kilometres by twenty-three.

## 2026-09-27 — car parks are cut from sites kept between cuts, and a cut changes the layout where it stands

**Cutting the car parks grew faster than the square of the town.** Every car park read every place on every
road again, asked each whether it stood a locality clear of every node, ranked each against every car park
already cut, and the cut then copied and filed again every road in the town. On Odesa's brief with its
extent scaled each way the stage took 0.2 s at one, 1.5 s at two, 47 s at four, and at ten had not
finished in ten minutes.

- **A book of sites for each size of car park** (`CarParks.SiteBook`): read once, then read again only on
  the roads a cut laid again, since a cut changes the line of the road it parts and of no other. Whether a
  site still stands a locality clear of every node is asked as it comes up.
- **Furthest-first is kept rather than sorted again.** A site's distance from the nearest car park only
  falls, so the one at the head is brought up to date as it comes up and is the furthest once it has not
  fallen.
- **The nodes and the car parks are filed by cell** (`PointCells` on the main level,
  `TownLayout.StandsClear`), and **a cut parts its road where the layout stands** (`TownLayout.Part`,
  `RoadLines.Refile`) rather than the layout being rebuilt round it.

**Two sites at the same distance are now offered by road and then by place along it.** The sort that
ranked them was unstable, so a tie came out in whatever order the runtime's introsort left it: the order
was .NET's and not this engine's. **Odesa has such a tie at its 188th car park and moves by it** — a few car
parks stand elsewhere, and seven props with them. River and the fixture are the same to the bit. The stage
is now 0.07 s on Odesa, 0.18 s at two and 1.5 s at four.

## 2026-09-27 — the exam is a scenario map of this engine, and going round a ring is straight on

**The owner ruled that the map is an end-to-end test of this engine and not an exam in anybody's rules of
the road**: the name was a way to fill it. So every card now says what the engine is to make of the
scenario and which of the engine's own rules that exercises (`ExamCard.Expects`, `ExamCard.Rules`), and
**every card is expected to pass** ([verification](../../../docs/verification.md#the-scenario-map)). The
tier stays outside `all`, as the frames do, because it is a whole town driven for a minute and a half.

- **Most of it is now the plainest cases**: a car alone through every movement of every shape, and cars
  whose ways never meet — two passing each other through a junction and on a road, a car driving past one
  or a line of cars standing in the oncoming lane, right turns on their own corners, two cars round opposite
  halves of a ring. None of them may be held at all.
- **A right of way is asked as the engine means it** (TER-5e: it orders who waits): the car it favours is
  never made to wait, and which of the two is over the shared ground first is not asked. Priority to the
  right, first-to-arrive and every other rule-book ordering went with the rule books; the cards that asked
  them ask now that everybody gets through and nobody touches.
- **What the engine refuses to do is not asked**: turning round in a box (TER-5f) or at a dead end
  (TER-5a), overtaking, and keeping a box or a junction clear of a queue past it. The dead end asks a car to
  drive out of one.
- **Somebody walking round a corner** was added — past the kerb ends of two zebras and over neither, which
  is where a pavement's ribbon comes nearest a lane's (TER-5c) — and **a road anybody walks is driven both
  ways**: a one-way road is one lane wide, so the far side of its zebra is its traffic's lane, and the first
  lattice left a walker standing there and a left turn behind it for good.
- **The lattice is eleven by eleven**, for 109 cards.

**Going round a roundabout was the weakest movement at every ring node.** A movement is classified by the
angle between the two lane ends, and a ring's lanes stop a standoff short of each node on both sides, so
read end to end the ring turned 60–75° towards the island: a turn across (p7), below every entry (p6) it
met, and a car on the ring gave way to a car coming onto it — against the road's own rule that circulating
traffic holds for nothing. **So the angle is taken against the arriving lane carried on along its own curve
across the node** (`LaneLines`): the ring comes out straight on and the entries and exits keep their
near-side turns; a straight lane carries on straight, so no other movement changes class. On six seeds of
Odesa, one minute each, against the same six before it: knocked down 3 → 3, wrecked 34 → 34, walks given up
448 → 447, km driven 2415 → 2407, touches 191 → 202.

## 2026-09-26 — the scenario lattice is laid like a town, and its pedestrians walk paint to paint

**Its roads are laid the way the generator lays them**, stand point to stand point on the arms the town's
own draw gives each end. Laid straight from node to node, as the parked exam's were, the lanes the plan is
read back into end where the lines do not go and the junctions lay no turn: the first lattice routed every
turn round the block. **It lights the junctions its cards are about and no other**, so a card about a light
is about the one it is staged at.

**Its pedestrians walk paint to paint and stand in the lane.** A walk from the pavement is not reliably
routed over a zebra (the known gaps), and a body crosses a carriageway in about a second at the town's
pace, so one sent kerb to kerb is either walked round the block or out of the car's lane before the car
arrives. Two stagings were tried and dropped: three walkers side by side walk into one another on the
zebra's one line, and one pacing back and forth steps back out at the far kerb as a car arrives too near to
stop — both cards about the walkers rather than the car.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- `GEN-2c`: the shore's chord tolerance is how far the drawn and answered bank stand off the wave, not half
  a cell of a classification.
- `GEN-6`: car parks are counted off the buildings a map plans (`SimConfig.CarParksFor`), not off
  `GEN-4b`'s relation.
- `GEN-16`: two car parks are kept apart by the locality every node owes (`TownLayout.StandsClear`), measured
  node to node, not as rectangles along a kerb.
- `GEN-19`: a building fronts a ring like any street; what is refused is a car park laid off one.

## 2026-09-19 — the verge is spaced by its own step, not by the props' girth

**The step the face was walked at was shorter than the props are wide**: a candidate every metre and a half,
against a girth of up to two metres and a bit plus a clearance, put a prop wherever one would fit — a hedge
down both sides of every street. **So the step is longer than the widest prop**
(`SimConfig.CityGen.PropVergePitchM`, `GEN-6b`) and it, rather than `GEN-6c`, spaces a verge; the clearance
still holds and now almost never bites. **A step and not a share**, because refusing a fraction of the
candidates cannot put a gap anywhere: three kept in a row are still girth to girth. The figure was read off
Odesa's verge count rather than worked out — the old pitch being girth-limited, doubling it did not halve
the props — and the verge now lays about half what it did.

## 2026-09-19 — a building fronts the boundary, not a road, and a service is stood on its parking

**The old stage cut frontage slots off every road**, so it had to know a road's half-width, its class, its
ends and its bends — none of which says where the concrete stops, and the lane layer that replaced the old
one carries none of the shapes it read. **So the face is walked instead** (`GEN-54`): a building fronts a
street, a block, a roundabout or a car park without knowing which, on the same line the props were moved
onto for the same reason (`GEN-6b`).

**The wall stands on the walk's own kerb rather than a setback behind it** (`SimConfig.BuildingLineM`): a
building line of the pavement plus a padding share left a strip of nobody's grass between every wall and
every walk. What is asked of the whole footprint rather than the wall is that no part of it is nearer the
carriageway than the face, because a deep building against one street is the one that reaches into the
pavement of the next.

**A service is placed by cutting its parking first** (`GEN-55`). The old placement laid every building,
swept for one near a car park and marked it a hospital — two passes over one question, and a hospital
wherever a lot happened to land. Cut before the town's own car parks, the yards are spread for free, the
sites being ranked by distance from every car park already cut.

**Only the flat of a car park's face is frontage.** Walked at a pitch, the whole of it was offered, and
buildings perched across the rounding at a rank's corner or down its sides. The frontage now has to stand
wholly on the flat, and **a service is slid to the middle of its rank**: the three civic roofs are 18 m wide
against a six-bay yard's 21.6 m, so a step of the pitch either way put one over the corner.

## 2026-09-19 — a kerb's end is read at the figure two places are one place at, and every handover is one

**The reading asked a built line for an exact offset.** A piece of the boundary is a road's kerb when it
stands half a lane off that lane's line, and that was asked at a weld (`LineTolerance.JoinedM`, a
centimetre) of a line that has been offset, merged, rounded and joined (`Spline.JoinedInto`) and drifts off
the half-width the longer the run: 1.8000 m off its lane down one Odesa street and 1.7887 m eighteen metres
on. **So one unbroken kerb read as its street for 17 m and as nothing for the 19 after.** Asked at
`LineTolerance.OnePlaceM` and settled on the nearest line that answers, a kerb reads as its street for its
whole length.

**A piece was read at its two ends and halved once between them**, which finds the first of several changes
and no more: one 100.31 m piece of Odesa's outline read road 1116, then no road, then road 1384, and yielded
one end 20 m up road 1116's kerb with road 1384's name on it. **Every piece is walked at a metre now**
(`KerbEnds.Along`), each step that changes hands is halved inside itself, and each change is named by what
stood either side of it.

**A road that takes the outline back from itself has not ended.** The rounding cuts the corner of a bend
inside a road (`TER-3c.10`), the outline leaves the kerb and picks it up a stride later, and both places read
as that road's end — 72 m out of its box on Odesa, where the zebra and the bar then stood. A pair naming one
road twice goes (`KerbEnds.StepsRound`); an honest cul-de-sac head is refused already, for standing at a box
that does not fork (`GEN-5a`).

**The census reads the answer back** — how far out of its box a town's kerb ends stand, at the middle and at
the furthest — because the furthest end of a road is the one its paint is laid off. Odesa's went 6.69 →
6.20 m at the middle and 75.79 → 15.52 m at the furthest; what is left there is a gore whose arms part at a
shallow angle, where one kerb really does leave the outline long before the other.

## 2026-09-19 — a verge is the walk's own outer face, and the props are laid along it

**The props were laid beside a road, and the road stopped being where the concrete ends.** Half a road plus
half a metre out was a verge while nothing laid a pavement, and the middle of the pavement once the walk was
struck off the boundary (`TER-7b`): benches and planters on the concrete down every street, which the ground
could not refuse because it then answered grass on the concrete. **So the pass walks the face itself**
(`GroundRings.WalkEdge`), and what that deleted was worth more than what it fixed: the stub a road's walk
stopped short of each junction at (the face wraps a junction, and a roundabout's island is lined by the same
walk), the choice of hand (a shell is walked with its ground on the right, `BandShell.Chains`), and leaning
on the ground to refuse an overhanging girth (the band is measured to the prop's own near rim).

**The sweep's stand-off moved to the boundary rather than to the face.** The face has its pockets closed —
a strip of grass narrower than two walks is concrete end to end and the face runs round the whole block —
so a wild prop in one measured thirteen metres from the paving and stood two from a carriageway. Odesa's
wild scatter went 82 643 → 77 783, and its verge 20 479 → 29 726: the concrete round every junction,
roundabout and block is kerb the old pass had no line for.

## 2026-09-19 — a reading of the kerb ends carries its own scan of the index

`KerbEnds.Of` asks the town's one `ChainIndex` once per piece of the boundary, and that index's convenience
overloads answer from a working set it owns. **Two readings of one paving on two threads were two walks over
one scratch**, and came back with ends at places the kerb never changed at, differently on each run. So the
reading takes a scan of its own (`ChainIndex.NewScan`): eight bytes a chain once per reading, on a path that
already allocates. The suite is where it showed — two classes reading one shared fixture — but a town laid
while another is being read would have got the same answer.

## 2026-09-18 — every line beside a road is rounded, and the boundary is rounded with them

**The lines were exact and looked it**: every fold in an offset came back as the corner its cut made — a
spike of concrete at a car park's mouth, a notch where two roads pass, a kerb turning a right angle at a
junction wedge. No kerbstone bends to a point. **So the rounding is struck with the line** (`ArcOutset`,
TER-3c.10) **at one radius for the whole town** (`RoadFigures.LineRoundedM`), because two layers rounded
differently are two answers about one corner and the concrete between them changes width along the bend;
the walking courses take it too, a lane being the middle of its pavement.

**What it cost on Odesa**: every metre of 205.58 km of pavement lane still stands on its own course, the
turns refused for want of a line fell 538 → 535, and **the lanes that cross themselves rose 4 → 13 of
3 046** — the radius is past the inner course's own metre, so that course is cut at its corners. It is a
reading and not a gate, and the one to watch if the figure is ever raised.

## 2026-09-17 — the generator's three slowest questions, and the one that could not be moved

**Laying Odesa was 918 ms on one core, a third of the whole open.**

- **Where a junction could be cut** (`CutJunctions.Sites`) reads the layout and writes nothing, and asks a
  locality test of all 2 150 nodes for every place on every road. A road at a time on every thread, strung
  in road order (`InChunks`), took it 119 → about 80 ms.
- **`CarParks.Spread`'s sort asked its key twice per comparison.** Worked out once and carried in, 113 →
  13 ms — and the order is the same order, a comparison sort's permutation being a function of the count and
  of what the comparisons answered, both unchanged.
- **The cells a road's line runs through are filled into the caller's room rather than yielded**: every cut
  has `RoadLines.Reset` walk every road again, and the iterator was a fifth of that and a few hundred
  thousand allocations.

**The props could not be moved, because of the stream.** `PropStage`'s sweep draws a position, asks the
ground, and *only then* draws the kind and the girth, so how far the stream has advanced by cell *n*
depends on every ground answer before it. Drawing unconditionally is a different town: **it is a real
bound, and taking the draw and the test apart is a change to what the generator lays**, not to how fast.

## 2026-09-16 — what a plan lays on the first ask, it lays once for everybody

The suite stands several towns over one plan, and two of them asking its `Paving` met on a merge not laid
yet, both merges walking one `ChainIndex` scratch. **What came back was not a second copy but a wrong one**:
the city's boundary with 164 runs it could not close against 135 it did, and every shape struck off it
failing with it — nine tests over four classes, each of which passed alone. The products laid on the first
ask are behind one lock (`Paving`), build-time and uncontended after the first ask, so it costs a frame
nothing.

## 2026-09-14 — a cut reads its arms off the line

**The parting had to be exact.** A cut parts a street into two roads with a node between them and may
not move the street, so the pieces are the arcs the road was laid as, cut with `Spline.SubChainInto` and
never drawn again. **That inverts the layer's causality** — everywhere else an arm is drawn first and the
road is laid to it (`GEN-46`) — so `ConnectionPoints` grew a third source beside the bridge's and the
ring's, **read off the road**, and `CityPlan.RoadArrays.Cut` says which roads take it. **Both ends of a cut
road read it and not only the cut one**: a cut moves the far end an arm is drawn toward, and `RoadSplineTests`
caught the piece still drawing its old bearing on every road in the town. **The node stands a standoff of
one arc back from each parting**, so the lead is a piece of the road itself and `Spline.ArcThrough` reads it
back exactly; on the suite's city at four seeds the parted lanes come back within the rounding two
computations of one distance disagree by.

**Two things were tried and are not here.** Redrawing the halves through `RoadLines` moves the carriageway
by the wander and jitter the two new links draw, which is what the cut exists to avoid. Comparing the two
towns with `ChainIndex.Nearest` reported millimetres of drift between a town and itself, so the test walks
the parted lane and the lane it was at the same metre instead.

**A rank stands off the lane its bays are worked off, not the line the street was laid down.** A one-way
carriageway is moved onto its driven half (`RoadStage.OntoTheDrivenHalf`), so a rank measured off the layout's
line stood the far rank a lane and a half off its kerb and the near one a metre. It is measured off the lane,
read off `RoadStage.DrivenHalfM`, the one site that moves the carriageway (`CarParks.LaneTowardM`).

**The bay is five metres** (`CityGenFigures.BayLengthM`) against the longest vehicle the catalogue draws, the
recovery truck's 4.40 m. The plan is laid before a car exists, so `CarCatalogTests` and `SimConfigTests` gate
the pair rather than a derivation.

**The bound on the bend is the drift from the rank's line.** A rank is laid off the tangent at its middle, and
a street bending under it leaves the rank's line by the tangent offset — the square of the run. So the
departure is authored (`CarParkOffLaneMaxM`, a fifth of a metre) and the curvature is read off it. `--bench
parks` reports it.

**The plan carries the bays once.** A per-road `OneLine` flag beside "is it a bay" was two arrays that must
always agree, so the array is `RoadArrays.Bay` and `DrivenOverOneLine` reads it — a bay being the only ground
driven both ways over one line (`GEN-53`), the exception a ring arc already was
(`ConnectionPoints.Arm.OnTheLine`).

**How many a town has is counted off its buildings rather than authored beside them**: a car park count on
the brief drifts silently from the town it describes, so the brief carries the buildings and the engine one
car park per `CityGenFigures.BuildingsPerCarPark` of them (four, the owner's ratio). Asking for many more
showed the supply bound — a straight, long, empty stretch per car park: Odesa then asked 300 and laid 239,
River 275 and 134.

## 2026-09-14 — the nodes are settled before the first road is laid, and nothing is merged afterwards

**The merge was the last pass that laid the town twice**: `MergeTheLocalNodes` made one node of each cluster
(`GEN-16`) after every road was drawn and then offered every road again — Odesa merged 12 nodes of 257 and
redrew 351 roads. **It is one pass now: place, settle, lay** (`TownLayout.SettleTheNodes`), the arterials
laid first so a street offered against an arterial's ground is the one refused, and the ground bound
(`GEN-49`) holds from the first road.

**A cluster is not a pair.** Welding each new node onto the first one near it leaves the third of a chain
where it stood, and on the suite's city cost 24% of the network — 140 roads against 184. The merge's
union-find, run over the nodes before any road exists, closes the chain for none of the cost. **The node
placed first is the one that stays** — the hub, the bridgeheads and the arterials before any lattice point —
and a deck cannot move at all.

**What came out**: Odesa 163 junctions, 272 roads and 51.4 km against 160, 269 and 51.0; River
119/193/36.1 against 118/191/36.3; the fixture 28/36/7.4 against 26/34/7.7. The suite's own city moved
further, and the gate proving the contact path runs asks for 800 cars where it asked for 400 — under one car
a lane (`SpawnStage`), the same ask as every lane the town affords.

**Measured and left alone.** The run joined into one road (`GEN-51`) still reshapes the layout after it is
laid, taking 63 of Odesa's 220 junctions away, because what makes a node a place nothing meets is what was
deleted after it was placed; a roundabout (`GEN-19`) is opened on the town there actually is. Laying the
arterials with a node only where a street welds on was tried and cost the fixture 26% of its network: the
spacing is also the granularity at which a refusal over water is contained.

## 2026-09-14 — no town is carried as a file, so the format that carried one is gone

**The `.town` format outlived the last town written in it.** Every map is a brief or is laid in code, so
`TownReader`, `TownWriter`, `core/persistence/`'s byte cursor and tape, `--export` and `TownReaderTests` —
**787 lines whose only reader was each other** — went together, with the fields only the round trip kept
alive (`CityPlan.PavementCorners`, which `TER-3c.3` says the pavement does not have). The web head's fetch
of a town file went too: a city is its brief on the wire as on disk. **`qq town` was a second reader of the
format**, five hundred lines of Python kept in step by hand, so its readings moved into the engine
([`TownShape`](../../bench/TownShape.cs), `--bench shape`, `--bench joints`, `--bench shapes`) and the tool
only chooses the map. The one test that went with the format was a round trip reading the same floats
back; with no file, asking one plan twice is a derivation written out twice (VER-12).

## 2026-09-14 — a road is drawn as it is offered, and the round of refusals is gone

**The road stage laid the whole town, refused what clashed, repaired the layout and laid it all again**,
each round redrawing every street's wander, so one refusal re-shaped roads that could then be refused — a
search over wander draws dressed as a repair, and what `GEN-10` said the generator does not do. **The line a
link would be laid as now says whether it is a road** (`RoadLines`, [TownLayout.Join](../gen/TownLayout.cs)),
refused when offered for its class's floor, the world's edge, the water or ground another road holds
(`GEN-49`). That needs **a road's shape to be a function of its link** — the wander keyed on the two node
centres as the arm's jitter already was (`GEN-11`) — so deleting a road moves nothing that stayed.

**The stages that move a road ask before they move it**: a run is offered as the one road it would be and
cut in two where it cannot be laid, and a ring is drawn arm by arm before its node is opened out. **Two
joined roads are held to each other and not to each other's pieces**, since a run's smoother line can bow
metres off the pieces it replaces.

**What not searching costs is reported rather than hidden**: Odesa keeps 269 roads over 50.97 km against
262 over 51.2, and 12 of its 160 junctions are places nothing meets against 1 of 152 — eleven runs the join
could not lay as one road. One of its two roundabouts is a junction again, its arms undrawable where the
ring would leave them (`GEN-19`).

## 2026-09-13 — a junction is a place roads meet, and a run through nodes nothing meets at is one road

**A third of Odesa's junctions were places nothing met** — 75 of 223, River 60 of 163, the fixture 26 of 40:
arterial spacing nodes nothing welded onto and lattice points the prunes left with two arms, each a
standoff, a pair of movements, a claim and a routing node in the middle of a road. The old layer hid them
behind a fold; the rework deleted the fold without writing the replacement.

**The corner is joined rather than straightened.** The median deflection at a two-armed junction is 85°,
so a chord between the far ends would take the carriageway off the ground both pieces were laid on. The
node's place is handed to the joined road as somewhere it passes (`LayoutEdge.ThroughM`) and each end's
bearing is drawn toward the first place passed; those places are in the plan because the connection points
are drawn again off it, and a plan without them would draw a different town.

**A refusal halves the run and never costs it.** Laid as one road a run is one refusal, and the first
attempt lost 14% of Odesa's network and 40% of the fixture's that way. A refused joined road comes apart into
its pieces with exactly its middle place standing as a junction again; holding *every* place brought back a
line of junctions nothing met at. Odesa 49.6 → 51.2 km, River 35.4 → 36.4, the fixture 7.4 → 7.8. **A road
that comes apart runs both ways again** (`GEN-18`), which is what makes joining a one-way street safe.

**A road is asked whether it is still on the ground**, since the line bows hardest at a corner it was joined
through: two shipped maps had a carriageway a tenth of a metre over the edge and a generated one a street in
a river, before the question was asked of the line rather than of the nodes (`GEN-14`).

**One rule, not a rule with exceptions.** Of the first cut's five, the corner bound went — the corner was a
junction, so a joined road turns at a junction's floor (`GEN-47`, `GEN-48`) and a driver reads every arc
ahead of it; the one-way bound went, coming apart being what keeps `GEN-18`; the ring bound came straight
back, joining two arcs dismantling the circle `GEN-19` laid; and the already-joined bound went for good once
its boundary left open by 0.104 m against a 0.100 m weld proved to be the merge's hole (below). **What is left
is structure**: a bridgehead, a ring node, and a run that comes back where it set off. Odesa keeps one
two-armed junction of 152, a loop; River ten of 113 and the fixture seven of 21, all but three bridgeheads and
loops, and those three are refusals — `qq town --joints` lists each as `bridge`, `loop` or `refused`.

## 2026-09-13 — a ring the merge keeps as one stretch is still a ring

**A boundary made of exactly one stretch could never be closed**: the pairing at a place will not let a
stretch take itself up, so a ring that came back as one stretch was walked as a run with two ends, and
`Shut` refused it again for having fewer than two pieces. The town lays one: a movement whose radius
(1.85 m) barely exceeds its half-width (1.80 m) folds its inner edge into a 0.05 m hook, and what its
neighbours leave of that is one stretch 0.104 m long whose ends stand 0.087 m apart — inside the weld.
**So a run is shut when it comes back to where it set off, however few stretches it is made of.** The hole
had been read as a fact about the town: `GEN-51` carried an exception for a fortnight on it. **It is the cusp
and not the fold that is hard** — a ribbon turning tighter than its half-width inverts and merges cleanly,
and of −1.32, +0.39 and +0.05 m inside the fold on the suite's city only the last left a hole.

## 2026-09-13 — the lane layer is laid from the junction out, and everything that stood beside it is put down

**The order was the whole of the problem.** A road's curve was laid first and its lanes cut out of it, so
the bearing a car entered a junction on was whatever the chord left and a movement was a line drawn to fit
rather than one a car was shown to drive — `TER-5d`'s model read backwards. **The points are drawn first and
everything is laid to meet them** (`GEN-46`, `GEN-47`, `GEN-48`): a biarc meets both poses exactly by
construction, so the bearings are a fact about the road and nothing is cut back. A road bending tighter than
its class affords is straightened, then refused — which a chord test after the fact could not say, so the
rule stated as one was deleted rather than restated (`GEN-49`).

**Odesa lays 619 lanes and 1 209 movements over 332 roads in 557 ms** against 444, 1 033, 256 and
1 238 ms, and **its boundary closes all 115 rings** where it had left one open by 0.122 m over 30 390 m:
fewer, longer, smoother lines left the merge nothing to disagree about.

**A node's centre is the layout's, which nothing moves.** The forkless sweep's nudge would have been six
metres with the standoff — a bend inside another junction's locality (`GEN-16`), and connection points drawn
at derivation differing from those drawn at generation.

**The town it carried was put down rather than ported** — the parking, the buildings and the services on
them, the junction furniture, the signals, the kerb band, the paint and the walking network's passes — not
as unwanted but as not worth porting onto a layer being replaced that month. What kept the milestone
observable was the spawn stage standing the brief's cars on the town's own lanes, one a lane.

## 2026-09-13 — the boundary turns where the ground turns, and a cut is not a corner

**Three quarters of the points in the town's outline were places nothing happened.** Every crossing cuts a
ribbon, so two lanes of one carriageway, a bay's way and its neighbours all left joints where the boundary
went straight on: the suite's city came back in 7 301 pieces, 5 303 continuing the one before on the same
circle. **So a run is joined before it is handed over**, and a piece is somewhere the edge really turns or
changes radius — 7 301 → 2 008 and the fixture's 460 → 165, with the same 50 rings, the same 39 965 m and the
same stretches kept.

**A join is measured against the first piece laid on from where the second starts** — laid on from the first
piece's own start, the hair between two computations of the joint is carried the length of the join, and a
two-kilometre straight laid in halves read as a corner — **and by a walk rather than a radius**, since
subtracting two centres kilometres away answers in their last bits (68 false corners). **A ring has no first
piece**, so the last piece takes the first and may reach on. The lines are joined the same way before their
ribbons are laid (`ArcRibbon`, 31 638 → 26 680 pieces), and **a pair of pieces further apart than their two
reaches and a weld is not solved at all**, which took the city's merge 1.06 → 0.81 s with the same stretches.

**The suite's boundary check gained a millimetre.** Bays and lanes sharing an edge leave places strictly
inside neither band by a fraction of a millimetre, and a station asked at the middle of 165 pieces landed on
one five times where 460 never had. It is a tie, and the millimetre settles it as the merge's does.

## 2026-09-13 — a car park's bands are weighed as the run they are, and the boundary closes

**A car park lays a dozen bands down one lane**, a millimetre or two apart and tangent to a lane bending away
from them, and the outline through one came back in pieces: 18 open runs over 19 km on the laid city. Four
faults, each **a figure compared against two readings of one gap**:

- **The probe's own step was left in the reading.** The cover test stands a millimetre out, so a coincident
  edge read a millimetre and the window calling two edges one sat off centre; bands 1–3 mm apart were both
  kept. The step comes off the reading.
- **A cut is a place, and every boundary standing at it is cut there**: cuts are carried to every ribbon
  within a weld, so a bundle stops in the same places. Without it 127 runs stayed open.
- **The bands at a place are weighed over the place at once**, as a run ordered by where each edge stands —
  asked pair by pair, all three copies of one stretch were dropped — and where an edge stands is read off the
  way that band faces there, not off the distance.
- **Where two readings still straddle the figure, the pair is kept twice rather than dropped twice**: the band
  a line outranks has to clear the coincidence by the arithmetic's own error before it covers that edge. **A
  slit between two bands facing each other is nothing up to two centimetres**, a separate figure from the
  coincidence, asked only of a band lying along the edge and not of a square end cutting across it.

The suite's city went from 18 open runs and 19 km to none, and its stations without the driven ground on
their right from 10 to none. River closes every ring; Odesa kept one, a lens a third of a metre long in one
car park, handed back as what it is.

## 2026-09-12 — a band facing you is read off the line and not off the edge you are standing on

**A lane's end cap was coming back as three and a half metres of the town's outside** across a junction
mouth. The movement carrying on from a lane shares its square end, so the seam is inside the town — but the
cover test read which way a band faces off the nearest point of that band, and off a square end that is the
millimetre the test itself stepped, so lane and movement each saw the other as a band walked out of. **Off
its own end a band faces along its line**, out of whichever end it stopped at. The tie matters because the
town's two computations of one place differ by about a millimetre (`TER-5d`): measured at 2, 5, 10, 20 and
50 mm, every coincidence above 2 mm left more of the city's boundary open.

## 2026-09-12 — two cuts nearer than the weld are one cut, and the hole between them is gone

**Two boundaries that run along one another cross wherever a float's last bits say they do**: a movement
leaves a lane tangent at the same width, its edge within a tenth of a millimetre of the lane's for metres,
and the crossing solve answers with a cluster of points centimetres apart. **The fault was the merge's**:
each crossing cut the edge, the slivers between were dropped as too short, and the cursor moved past every
cut, leaving a hole as wide as the cluster — a ring that should close came back as a run hundreds of metres
long. **A cut within a weld of the one behind it is passed over**, and **a stretch shorter than the weld is
its own two ends** and is not kept. The laboratory map closed on the first, and the city went 158 → 36 open
runs.

## 2026-09-12 — the boundary is the merge of the ribbons the lines lay, and the walk that found it is gone

**A lane is an area, and the outside of the town is the outside of the union of those areas.** The boundary
used to be walked out of the lines — every line weighed at stations against every band near it, the
outermost stretches paired by their ends, strung into rings and shut with a straight where the pairing
failed — and what it cost to close was a construction nobody could state in a sentence, every known gap of
the day one of its passes failing. **Said as a merge, there is nothing to pair**: each line lays its ribbon
(`ArcRibbon`), every ribbon is cut where another's boundary crosses it, and a piece is kept where the ground a
hair outside it is on no band. **Nothing is bent**: an offset of an arc is an arc about the same centre and
two of them cross in closed form, where the walk sampled at half a metre and needed a rounding to call two
stops one place. What a merge cannot close it hands back as loose (`BandShell.Loose`), drawn in the fault
colour rather than shut with a straight.

**Three figures carry the degeneracy** (`BandShell.Merge`): how far outside a piece the cover test is taken,
how near two band edges stand to be one edge, and how near two cut ends stand to be one end. Two coincident
edges are settled by the lower-numbered line, and a piece is cut where another piece's own end stands on it
as well as where one crosses it — two square ends laid along each other cross nowhere.

## 2026-09-12 — the last town laid is kept, and only the last

`Maps.Plan` laid a fresh town every time it was asked, so a review sheet, a probe taking several readings or
the visual tier staging a scenario a cell paid 34 s a city each time, and 30 s more for the ground mesh.
**One town kept and never a table of them**: kept by name, the first sweep over every shipped city would hold
all of them alive at tens of megabytes each. **And a plan is as far as it goes**: the walking graph is the
largest cost of standing a town up — 33 s against a tenth of a second for the road graph — and as pure a
function of the plan, but its `NearestEdge` index carries a scratch of its own (`ChainIndex`), and handing
one graph to two towns broke a hundred cases of the suite. The shot path works round it (`TownStanding`).

## 2026-09-08 — a roundabout is a ring of ordinary junctions with no paint on it

**Nothing about a roundabout is a new kind of thing** (`GEN-19`), and the pay-off is that **circulating
traffic is driven over what is entering without anything granting it that**: the movement between two ring
arcs at a node goes straight on, and `TER-5e` ranks it over the turn in off the arm. A polygon of straights
loses exactly that, every circulating movement a turn of sixty to ninety degrees. A ring laid as straights at
the nodes with arcs between them needed twenty-odd metres of chord per entry — two thirds straight, ninety
metres across, a rounded square — so it is one arc node to node, and **a kerb corner is solved between the
shapes the kerbs are drawn along**, a line or a circle: solved between the lines of the arms' bearings it was
a metre and a half out on a 27 m circle, and the walk round the ring came apart at every entry.

**Nothing is painted on the ring, and that is what let it shrink**: straight bands read square on its bend
only at about 23 m of radius. **What sizes it is the two roads leaving two of its nodes** — the ground one
road takes and a pavement on top — and not the road or the locality two separate junctions owe; held to the
road, the circle came out nearly twice as wide. Odesa's widest went 54.6 → 45.8 → **32.4 m** across.

**Four arms or it stays a junction**: at three, a circle sorts out one conflict the ranking already settles
standing still and charges every car a detour to the arm opposite. **An arm is read off its tangent**
(`TownLayout.Bearings`): the chord a road left a ring node on is the polygon's interior angle, and made two
pieces of one ring read as two arms lying together (`GEN-13`).

**The ring is not moved onto a driven half**, being the whole of its own corridor. Moved half a lane off its
circle, its arms ended on the far kerb exactly half a walk from the island's footway line, and whether that
pavement existed at each entry came down to a float — three of Odesa's four entries lost about two thirds of
a metre of it.

## 2026-09-08 — one-way streets are scattered over the town rather than laid as a grid

A one-way grid put every one of them in the strict districts inside the orbital: most of a town never met
one and one district was nothing but. They are chosen over the whole layout (`GEN-18`) after the deletion
passes, against the town there actually is, by a spacing off the ones already taken and no two at a
junction; the lattice proposes no flow at all. **Both ends have to fork, and neither may be on an arterial**:
a street at a two-armed node dangles a lane (`GEN-50`), and one hung off an arterial costs a district its
second way in — a laid city with them failed eight of the town tier's questions, and all eight came back
once they were excluded.

## 2026-09-05 — the pavement is laid once, and the picture and the answer read that laying

`GroundMesh.Build` laid the pavement as draw calls and `GroundShapes` laid the same pieces again as coverage
tests, kept in step by memory (TER-7) — and a band widened in one and not the other is a walker refused
ground it can see it is standing on. `Paving.Lay` states the pieces once; the restructure was exactly
behaviour-preserving and the frames came back byte-identical.

## 2026-09-05 — a town keeps only the one-way streets it can be driven round with

Drivable is asked of the movements and not the roads (`GEN-18`): a block whose streets all run inwards keeps
one component and is still somewhere a car drives into and never leaves. Reachable everywhere is not enough
either, because the fault is local (`GEN-50`): a one-way street at a two-armed node leaves a lane nothing
arrives on, so a movement leaving a node needs some road other than its own arriving there.

## 2026-09-01 — a prop's kind is where it stands, and the ground decides it rather than a die

Every prop was a coin toss between *tree*, *scatter* and *furniture*, so a litter bin stood in a field as
often as an oak and the town read as three-way noise. **The kinds are placements** (`GEN-6b`) — wild,
planted, furniture — and a verge still carries wild looks, since a kerb planted only with what a town plants
reads as a catalogue. **The size band went with the kind**: the great trees are authored at 2.6–3 m against
a draw stopping at 2.2, so nine looks were reachable only through a fallback that does not resize. **Thirty
looks were deleted rather than filed**, each failing one test — name what this is and which of the three
places it stands in; eleven were park amenities, and this town has no park. **A prop's picture was bigger
than the prop** (`GEN-6d`): drawn `diameterM` tall, the flower planter was 3.45 m across against an authored
1.9. Props no longer overlap, indexed by a grid of the widest prop's own width because neither pass can see
where the other put anything. Odesa laid 108 939 props and then 78 705.

## 2026-09-01 — a car park is three to six bays, because a run of frontage is an apron

Merging neighbouring slots laid sixteen bays of unbroken tarmac down one side of a street, and nothing ever
filled it — the town's whole roster was 520 cars over 319 lots. `GEN-16` merges a junction because refusing
one deletes every road at it, and nothing hangs off a car park, so `GEN-4b` bounds a lot at both ends: the
upper keeps a car park from being a surface and the lower from being a two-car lay-by that cost a lot's whole
clearance. Odesa's 319 lots held 1 377 bays where they had held about 3 500.

## 2026-08-31 — two of a kind near enough to be one are merged, and merging beat refusing

The arterials, the lattice and the frontage are each laid at their own spacing, so near-coincidence is the
ordinary case, and **refusing the second of a pair costs the whole piece that hung off it** (`GEN-8`) — a town
losing a block to an arithmetic coincidence. So two junctions inside a locality are merged rather than one
refused, and Odesa went 42 → 51 km of road. When they are merged is the 2026-09-14 entry's.

## 2026-08-31 — a lane is the width the town is laid in

A road was three car widths across because somebody wrote three. A lane is 1.8 car widths
(`RoadFigures.LaneWidthInCarWidths`), so every figure quoted against a lane means the same thing on every map
(`GEN-15`), and road and pavement are ratios rather than metres — a figure authored in metres beside them
would be the one that stopped scaling.

## 2026-08-31 — the bank is a curve now, and the water is set in a shore

A shoreline was twenty-four points over four kilometres, though the bank has always been a sum of three
sines. **The count is derived from the wave**: a chord stands off a curve by about its own length squared
over eight times the bend, so the step falls out of a tolerance of half a cell, the finest the ground under
the bank is classified. **The water is set in a shore** (`GEN-2c`) laid as the same wave a shore's width
wider, so no band is fitted to a curve; the map carries four rings drawn largest first, each fill leaving a
line's width of the one under it, so nothing is offset by the renderer or classified twice. The shore is not
grass, which is the whole of why nothing stands on it; it wears the pavement's texture until there is a
picture of a beach.

## 2026-08-31 — the map ends at its edge, and the shore is cut there rather than never drawn

The outline pushes a sea's far bank past the map on purpose, and the mesh draws from the outline, so it drew
open sea over the void. **The shape is cut where it becomes a plan** (`GEN-2b`): a shore that ends on the
edge puts the map's rectangle inside the meander arithmetic and gives a coast four corner cases, where
clipping a ring against four half-planes has none. `Test`'s river ran thirty metres off its map and nobody
had noticed. A prop is refused with its radius rather than its centre.

## 2026-08-31 — a street that goes nowhere is deleted, and the fixture brief had to become a town

Odesa carried 21 junctions of one arm — dead ends in `TER-5a`'s sense without its promise, a car finding
three metres of tarmac with no room to turn. **They are deleted with whatever hangs off them** (`GEN-5a`),
swept to a fixed point: `GEN-8`'s own answer one node at a time, since an arm grown on to close the loop would
have to cross whatever cut the street short, and a turning head is a thing a town plans. It costs a city a
tenth of its road, and it exposed that the property suite's fixture brief was not a town — two of four seeds
laid no cycle at all, one a pure tree the sweep deleted entirely.

## 2026-08-31 — a bridge is a road, and the wheel is turned so there is one to build

A node in the river was skipped and whatever dry nodes remained were joined, so a bridge was however far
apart the spacing had left them and the hub sat in the water. **A bridge is a class of road** (`GEN-14a`):
water takes a `Bridge` and nothing else, the orbital gives up its arc over the span, and a span longer than
the deck a town builds is not laid. **The nodes make the crossing short** (`GEN-14b`) — a node on each bank,
the stretch between closed to everything else, since a node between two bridgeheads is a junction on the
deck — and **the wheel is turned so a spoke runs down the river's normal**, the rotation having been a draw
anyway. The sea is not bridged. The water question is asked of the carriageway and not the centreline:
Odesa lost about a sixth of its road length to lanes over the river.

## 2026-08-31 — a city is a seed and a brief, and every stage of laying one runs once

Cities arrived as baked `.town` files, so GEN-2 through GEN-8 bound whatever exported them and a city could
not be varied, replayed or repaired when a rule moved. **Only hints are persisted, never geometry**: the moment
a brief carries a node there are two answers to where the town is. **Nothing retries** (`GEN-10`), made
affordable by four things that each replace a search — convex districts, so planarity is arranged rather
than checked; arterials carrying a node wherever a street meets one; a slot claiming its padding before
anything fills it; and deleting what is left over rather than joining it up. The traced cities' median
sinuosity is 1.000, so straight is what a street is. **A junction's arms stand square enough to be a
junction** (`GEN-13`), learned by laying towns without the rule: at a shallow angle the fillet on one arm
paves the crossing on the other. A building is sized by the roof it will wear, the footprints crossing the
seam as data because the plan may not read a catalogue above it. It deleted `TownWriter`, `--lay-maps` and
`--place-services`.

## 2026-08-31 — the ring is a rounded square, because what stands inside it is a rectangle

The start menu opens over this map and a panel is a rectangle, so a disc spends its ground on corners
nothing reaches into — the widest rectangle inside one is 0.7 of its width. Rounded corners keep it a road,
where a square is four right angles no car takes at speed. The cuts are at the middle of the straights,
since a node on a bend takes a bite out of the one piece of the loop whose shape matters, and the escort's
pace is read against the *tightest* corner, where a convoy comes apart if it is going to.

## 2026-08-31 — the ring carries an escort and one car, and the escort is held to its charge

Two convoys of three read as a staging rather than as traffic. Police tyres are worth nearly twice the grip,
so the escort cornered a third faster and left its charge inside a lap: **a pace ceiling set from what the
escorted build affords on this radius**, rather than an escort built on the armoured car's figures and
painted white — a police car that corners like an APC is the paint and the physics coming apart. The gap
the three keep is the road each is granted and nothing on top of it (`S-2a`), so a slower convoy is a closer
one.

## 2026-08-30 — the menu is drawn over the ring, and GEN-1b now says which map that is

`GEN-1b` is about not building a *city* nobody asked for, which says nothing about a map costing a fraction of
one that was laid to be looked at. The ring is opened with the menu deliberately left up, since reopening the
menu afterwards reaches the same state by two moves with a flicker between.

## 2026-08-30 — a map laid to be looked at, and the look rule it had to loosen

The idle map is the picture the game idles on, so it is chosen for never stopping being worth watching and
never needing anybody's attention. Its size is the window's and not the driving's — at a 120 m radius it was
twenty seconds of empty road at a time — so the corner sets the speed. Nothing on it is staged, which is why
it is worth shipping. Four roads, because a road ends at a junction and two would join one pair of nodes
twice. **The map dresses its own cars, and that cost a rule**: the service tier had found vehicles by their
paint, an over-fit, since `SRV-3` defines one as paint **and** a building.

## 2026-08-27 — the map says what a building is for, and its people start behind its doors

A shuffle off the world seed could put a town's only hospital on a cul-de-sac with no bay within a block, so
**the record carries a use** (`GEN-9`) — one field and one pass. And **the map's people start behind its
doors** (`GEN-7`): a trip ends inside a building, so beginning there closes the round rather than adding a
stage, and the dwell is drawn per person so the streets fill over ten seconds. What it costs is that a
question about a body on the pavement cannot be asked at tick zero.
