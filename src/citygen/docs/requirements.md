# The town plan — requirements

**One data structure describes a complete city** (`CityPlan`): pure data that a generator lays, a builder
stands the world up from and a validator judges, which is what lets validation run headless and a new map be
authored without a code change. **Nothing carries it between processes**: a town is its brief and its seed,
or the survey of a real place it is traced from (GEN-57), so what would be written out is a second answer to
where the town is. This is the most load-bearing
structure in the project, and how each part of it is carried is that type's own documentation.

Bay geometry is [world/parking](../../world/parking/docs/requirements.md); the ground, and what each kind
of it permits, is [world/terrain](../../world/terrain/docs/requirements.md); roads and junctions are
[world/road](../../world/road/docs/requirements.md).

## Where a town comes from

**A city is generated from a brief when it is opened** (`TownGenerator`). What is authored is a
`TownBrief` in [towns/](../../../towns/) — a seed, an extent, the water it stands on, how many districts and
how strictly they are laid out, and how many of everything — and **nothing derived is ever stored**: no
district, node, curve, cell or building, because a brief that carried geometry would be a second answer to
where the town is and the one on disk is the one that goes stale. **A map laid to measure one thing is laid
in code**, as arithmetic over the car's own figures rather than a seed; this build lays one, the idle ring
([the maps](#the-maps)), and the laboratories that measured something are
[a known gap](../../../docs/index.md#known-gaps). **A real place is traced from its survey** (`TracedPlan`,
GEN-57): what is authored is what OpenStreetMap holds for it, and nothing of the place is drawn — only the zebras
and props every town lays are the town's own.

**GEN-1** `P3` Generation is driven by the **world seed**, supplied manually or chosen randomly; the same
world seed produces the same city.

**GEN-1a** `P4` A city's streets are generated with everything else about it, or traced off the survey of a
real place (GEN-57). A map that measures one thing is laid in code instead, and all three are one kind of thing
from the plan onward: nothing downstream may ask which of them it is looking at.

**GEN-1b** `P7` No city is built until one is picked: the game opens on a start menu listing the maps, and
nothing a reader has not chosen is built. **What the menu is drawn over is the idle ring** (`IdlePlan`,
`Game.IdleMap`) — the one map the game stands up without being asked, because it is laid to be looked at
and costs a fraction of a city — and the menu stays up over it, in either configuration and on either
head, until a map is picked. A run handed a map on the command line or in the query string opens on that
map instead, and the menu shuts onto it.

- **What the panel is laid as is [app/hud](../../app/hud/docs/requirements.md)'s, and how the ring behind it
  is framed is [app/camera](../../app/camera/docs/requirements.md)'s** (`OBS-1b`). What is this slice's is
  the field the ring is laid to fit: **rectangular because the panel is** (`IdlePlan.CornerShare`), the loop
  rounded off a square rather than drawn as a circle, so a panel wide enough to read does not have to be
  short enough to clear a curve.
- **The menu is up before any town is** on either head (`WEB-6`, `WEB-9`). A page hands the browser its
  animation callback before it lays anything: the menu stands on the few small files the boot fetched, and
  the ring behind it is generated while the reader is looking at it.

**GEN-57** `P4` **A traced map is a real place laid from its survey, and nothing of the place is drawn or lost.** What
is authored is the map's own file (`TracedMap`, `towns/traced/<Map>.map`), **the one file of the place the engine
reads**, imported off the survey and what else is known of the place by the scanner
([tools/osmscan/](../../tools/osmscan/), run by `qq osm`), a tool that knows none of the engine's rules. The survey is
an `OsmExtract` in [towns/traced/](../../../towns/traced/), written by the scanner off OSM.
**The place's boundary draws the survey's rectangle and nothing else**: what the extract holds is every road way
OpenStreetMap has in the rectangle round the roads inside the boundary, inside the boundary or not, every
surface outlining a road (`area:highway`) and the coastline over them, **exactly as OSM holds them, but where
the survey's own corrections put OSM right** — each node's id and its place in OSM's own units of 1e-7 degrees,
each way's id, every tag and its nodes, the tags of every node on them that carries any, and the turn
restriction and lane connectivity relations over them. **Two
things are added, both OSM's conventions**: each road way's lanes as its tags mean them (`OsmCarriageway`),
and the flat frame — Transverse Mercator at one metre to the metre about the middle of the boundary's roads,
the map their extent and a margin. What the engine reads off the map when it is opened (`Survey.Of`) is the
sea its coastline closes against the map's edge, and **a way running on past the map is cut where it crosses
the map's own edge**, a crossing nearer the node inside than the shortest road the map lays being that node. **No
seed draws anything of the place**, and the same map is the same town every time it is opened. **It lays every
road way OSM gives lanes, of every class, its sea, and what else its map says of the place, and nothing else but
what every town lays of its own** — its zebras and its props, which its map holds none of — and no car
park. **What moves is a few cars stood by rule at its bridges over its roads, and the people and cars its map asks
for** (`TracedMap.Population`, GEN-7), stood by rule as well. A road OSM draws as an area is a surface and has no lane.

- **The map holds what the engine lays and nothing else** (`TracedMapImport`, `qq osm --import`): each road way's
  line, class, level and carriageway as OSM means it, and its width as measured; the coast; the turns; each
  junction's control and every building's footprint, height and use — in the map's own frame, so opening it reads
  arrays rather than parsing them. **No crossing and no tree**: the town paints and plants its own. No tag, node id
  or road surface is carried into it. **Facts and no rule**: what a traced town takes of them is what follows.
- **The map is the master, and what it was imported from is kept as it came.** An edit is made to the map in
  place — `qq osm --crop` cuts it down to a box of degrees and its margin, its roads running off at the new edge,
  `qq osm --drop-stumps` drops every road stump that runs into or through a building or is a single lane, and
  `qq osm --footprints` lays its footprints again off the layers, a crop's frame kept — and never to
  what was crawled: every source's answer stays in `towns/traced/<Map>/source/` exactly as it came, and the
  survey and the enrichment's layers beside it. An import over a map that is there replaces it and every edit
  made since, so it is refused unless forced.
- **What OSM had wrong about the place when it was imported is put right in the survey** (`Corrections`,
  `towns/traced/<Map>.osc`): an osmChange the scanner applies over OSM's answer before anything is read off it, so
  a road it corrects is read for its lanes and turns as any other. A node it moves that a tram track or a footway
  shares stays on that way's line, and the enrichment places every node the survey holds where the survey does. A
  correction of a node or way OSM no longer holds is refused at the scan.
- **What else is known of the place is imported off the enrichment's layers** (`PlaceFacts`), each record's one
  answer, and layers laid off another survey than the one imported are refused.

- **Nothing the survey holds is lost on the way in.** A junction stands at every place ways meet, exactly
  there — or amid the places it was gathered from, below; a place two roads carry on through is one road through it (GEN-51);
  every point a way was surveyed through stands within its road's tolerance (`SimConfig.TracedLineToleranceAcrossM`)
  of it, or a corner's sag at the tightest it is rounded at; a ring of ways meeting nothing but each other is
  given a place and laid as the loop it is; and a piece of the network joined to nothing else is kept — but
  for the lanes both ways share that no way of running them leaves drivable, below. **So
  the generator's rules that take something out are not asked of a traced town**: one junction for places
  within a locality (GEN-16) — a traced town gathers only those a short road joins, below — a corner no tighter
  than a class's design speed (GEN-47), and one connected network (GEN-5). How far its lanes stand off OSM's own
  is read by `--bench fidelity`.
- **Junctions a short road joins are one junction** (`TracedStreets.Gathered`): two places of three arms or more a
  road runs between, through nothing but places of two arms, shorter than `SimConfig.CityGen.TracedJunctionsMergedM`
  — a dual carriageway's crossing, a side street meeting the far carriageway through the gap in the median, a
  crossing OSM draws as two tees a few metres apart — are one junction amid them, and so is every junction gathered
  with either while each stays that near every other. The road between them is gone and its ground is the box's.
  **Each road leaves the disc of the place it was surveyed to**, so the box is no bigger than the two were, and
  **the box makes only the movements the roads it gathered made**: a turn off one road onto another is forbidden
  where they gave no way between the two, through every one-way road and every restriction at their places —
  which is where a restriction naming the road that went is laid. A junction is lit where any place it was
  gathered from is signalled. **So is a place of two arms inside a junction's own disc**, reached through nothing
  but such places — a way OSM changes a few metres past the node it crosses at, a flared mouth tagged with lanes
  of its own: kept apart, the road between is too short for either disc and every turn onto its far lanes begins
  past where their lines cross. Not gathered: a junction on a bridge or round a roundabout, a pair a road between
  them would leave running out and back to one junction, and a gathering left with fewer than two roads into it.
- **A road is driven in OSM's lanes** (GEN-15, TER-4d): as many each way as OSM's tagging rules say, **every
  one `SimConfig.CityGen.TracedLaneWidthM` wide** whatever OSM tags or assumes, and **its carriageway laid along
  its middle**, which is the way itself unless OSM places the way off it. **No lane is driven both ways.** A
  single lane both ways share is one lane run whichever way still lets every junction reach every junction it
  reached before, and **is taken out where no way does** — a spur, a dead end however it runs — **as is a piece
  of the network left holding nothing but such lanes**, so none of them dangles (GEN-50). Lanes driven both ways
  down the middle of lanes each way — a tidal pair, a centre turning lane — are laid as lanes of one way each, so
  every lane still stands where OSM puts it. **Where the carriageway changes is a junction** — a lane more or
  less, a roadside, a placement — of two arms where nothing else meets there (GEN-51).
- **What a road was measured wider than its lanes is roadside** (`Survey.Of`): its `width` tag, the surface OSM
  outlines it with, or the paved width read off imagery under a street — never under a service road, a track or
  a link, where a yard's paving or a slip road's merge reads as theirs, and not where OSM gives widths lane by
  lane or places the way off the middle, or the width would leave a lane narrower than
  `SimConfig.CityGen.TracedNarrowestLaneM` or wider than `SimConfig.CityGen.TracedWidestLaneM` — a square, a
  yard or a car park read as the road. The rest past its lanes is **strips of `SimConfig.CityGen.TracedRoadsideWidthM`,
  as many as it holds to the nearest strip and one a kerb at most, a single one beside the kerb the traffic
  along the way keeps to**, and none on a bridge or round a roundabout. **Where OSM only assumes the count, the
  width says it**: as many lanes each way as it holds past a roadside at each kerb, never fewer than OSM assumes
  and never more than any way of its class on the map is tagged with; a way with a lane's turn, change or bus
  entry tagged keeps OSM's count.
- **A street's carriageway is held even along it** (`TracedStreets.Evened`) — a street being its roads run on into
  each other through every place of two arms, and straight on through every junction where nothing else meeting it
  is a street, only a yard's way or a track. **A stretch whose lanes change and change back** within
  `SimConfig.CityGen.TracedLanesHeldM` is laid in the lanes either side of it. **A street losing lanes** within
  `SimConfig.CityGen.TracedLanesHeldShortOfJunctionM` of a junction it ends at keeps the lanes it had up to it, the
  lanes the junction joins nothing to ending there; one gaining lanes into it is as OSM has it. Its roadsides are held
  even along each stretch of it driven the same ways: **a single roadside stands beside the kerb the stretch holds
  more of its roadside along**; a stretch of roadside shorter than `SimConfig.CityGen.TracedRoadsideShortestM` along a
  longer one is not laid, and **the rest is carried on along the whole of it**, every road it is carried along laid
  as though measured wide enough to hold it — except one whose kerb or walk that would stand on another road's
  carriageway. The buildings stand back with the walk.
- **A roadside is lane zero** (`CityPlan.RoadArrays.RoadsideWithM`, `LaneLines.IsRoadside`, TER-4d): a lane of its
  road between the kerb lane and the kerb, as wide as the strip, parted from the lanes by a solid line and joined to
  nothing — no movement leaves or reaches it, no junction counts it among its lanes, and nothing is routed, stood or
  turned onto it. **It runs on into the box** (`RoadsideLanes`): to where its kerb meets the kerb of the next arm
  round, or — where the kerb runs straight on — beside the movement the lane beside it makes, to that movement's
  middle, where the roadside across meets it. **Where the lane beside it runs straight across the box, the roadside
  runs beside that movement and not along its road's line**, as far as the kerb reaches. So the kerb turns the box's
  corner and runs straight past a mouth across the way, and **a turn out of the kerb lane sweeps over it**. **Where its road loses it along the way** — the
  next arm round carrying none on the kerb facing it, at a box of two arms or one the kerb runs straight on through —
  **it stops where its road does**, the end of the line beside it, and the kerb is eased in across the box over a band
  of its own, from the strip's end to the far end of the movement the lane beside it makes (`LaneLines.Tapers`).
- **Its junctions carry lights where its map reads them signalled** (TLT-3 still asks each for its arms), the
  junctions controlled as one — a dual carriageway's crossing — on one clock, each set starting where the node
  it is named by says, so no seed draws it. A sign, a priority road or a roundabout's control is carried and
  not laid: the engine has no sign to lay ([the known gaps](../../../docs/index.md#known-gaps)).
- **Its zebras are painted at every station its kerb ends cut, as a generated town's are** (TER-6, WLK-10,
  `CityPlan.ZebraAtEveryStation`): where the walk is crossed and the traffic held, and nowhere else. Its map holds
  no crossing.
- **Its buildings are prefabs stood on its walk where its footprints are** (`TracedBuildings`), OSM's and the
  machine-traced ones OSM lacks: each footprint, less its courtyards, is one rounded rectangle where it fills one —
  a silo a circle, a pavilion a stadium — or else cut into at most `SimConfig.CityGen.TracedPartsMost` rectangles on
  its own bearing (`FootprintParts`). **Each wears the prefab of its look nearest its size and roundness**, a larger one
  and a rounder or squarer one weighing against it (`SimConfig.CityGen.TracedPrefabLargerWeighs`,
  `TracedPrefabRoundWeighs`), **laid only as it was drawn** — its door's wall along the walk, never turned a quarter
  onto a side street it does not face; a plot narrower at the street than it is deep wears a prefab drawn narrow end
  to the street. **Its look is read off its use** — its own tags, else the land use it stands in
  (`FootprintUse`) — and, for a home they say no more of, off its height or else its ground (`TracedBuildings.LookOf`).
- **Every building fronts the walk, as a generated town's does** (GEN-54): a rectangle is moved through the wall of
  it that faces the walk's outer face nearest it, turned square to the face there, its front on the building line and
  its way in on the walk's outer lane. **A building is stood cleanly or not at all** (GEN-8): not where its front is
  further than `SimConfig.CityGen.TracedFrontageReachM` from the line; not where the walk under its front would leave
  any of it further back than `SimConfig.CityGen.TracedFrontageStraightM` or turns more than
  `SimConfig.CityGen.TracedFrontageTurnDeg` off its bearing — a bend, a jog, a block's corner; not where any of it
  stands off grass or on any paving (GEN-2b, GEN-2c). A rectangle longer than its look's longest prefab is cut into
  equal sections, side by side along the walk and one behind another back from it, each standing only where the one
  in front of it did.
- **The least moved stand first**, so what the survey already put on the walk claims its frontage before anything
  moved onto it, and each takes the nearest of its prefabs that stands clear of those stood before it by more than
  `SimConfig.CityGen.TracedPartyWallM`, or nothing — so neighbours a terrace was surveyed as share a wall, and no
  walkable padding is kept between them (GEN-3 is a generated town's). **The footprints are what its buildings are
  fitted off and nothing more**: the town carries the buildings stood on them and no outline of its own, and the map
  keeps them, with the survey's sources, for what is laid off them before a town is opened.
- **Its props are laid as a generated town's two passes lay them** (`TracedProps`, GEN-6b), drawn off the map's own
  number, its buildings claimed before them at `SimConfig.CityGen.TracedClaimCellM`. **What the first lays along its
  verges are props**, and none stands whose crown comes within a lattice step of driven ground (TER-4c.4): a road's
  carriageway, or a junction's disc and its widest arm, which a traced junction's movements can reach past its
  tarmac. **What the second lays on the open ground beyond is scenery** (`CityPlan.Scenery`) — a whole city's yards,
  parks and waste ground, drawn and standing no body. Its map holds no tree.
- **Its roundabouts are the ways OSM tags as circulating** (GEN-19), a ring of those that meet at their
  junctions, so its kerb ends stand no station on one and its walk does not run round one.
- **A bridge is a road of its own on the level above** (GEN-14a, PHY-1a): a way OSM carries on a bridge is cut
  from its approaches at its bridgeheads, which are places of two arms, laid on `CityPlan.RoadArrays.Over` and
  decked its whole length at its carriageway and a walk either side (TER-3b). Its cars meet nothing of the roads
  it passes over, and its lanes share no ground with theirs (TER-5c, TER-4c.2). **It is driven ground of its own,
  drawn over the road beneath** (TER-7b): the road under it runs on under the deck, kerbed and paved as if the
  bridge were not there.
- **A few cars stand at its bridges over its roads** (`TracedBridgeCars`): at the widest crossings, one each way on
  the bridge and one each way on the road under it, short of the deck — the only bodies a traced map stands.
- **A car turns where OSM says it may** (TER-5j), as the scanner reads OSM's rules for a car (`OsmTurns`):
  each lane is marked with its `turn:lanes` arrows at the junction its way ends at, a turn a restriction forbids
  at a node is not made — an `only_` one forbidding every other turn off its way there — and a turn whose lanes
  a connectivity relation names joins those. **What is not laid is named**, by the scanner and by
  `--bench fidelity`: a restriction in force only at times, since the map keeps no clock of day; one made over
  a way, which is a turn through two junctions no one of them can forbid alone; and one at a node no junction
  stands at.
- **A road is its surveyed line** between the discs of its two junctions (TER-5d), **leaving each where the
  line crosses its edge, on the line's own heading** — each disc a standoff of its junction's own, stood out
  by however much wider than a street of one lane each way its widest arm is, and **a road's own end** never past
  the middle of the way to the junction at its other end less `SimConfig.CityGen.TracedShortestRoadM`. **A short
  road squeezes its own two ends and not its junctions' other arms**, which end where their junction asks but no
  more than `SimConfig.CityGen.TracedArmEndsApartM` further back than the shortest end there: squeezed with it,
  every arm runs into the middle of the crossing and every turn across it winds round to come back to its corner.
  **And a road's end stands back as far as the turns off and onto it need to begin**, within the same room: each
  turn is given `SimConfig.JunctionTurnRoomM` at the corner its lanes' lines make, so it starts sooner rather than
  swinging out over the lanes beside it (TER-5d.2).
  **No lane of it folds back over a
  corner**: one its legs cannot round with the innermost lane at `SimConfig.CityGen.TracedTightestLaneRadiusM`
  is a survey's kink, eased the way that stands least off the survey — taken out, a point beside it taken out,
  or it and a neighbour carried on along their outer legs to where those meet. **A road of one lane on its
  line** — both ways sharing it, or one way of one — has nothing beside the lane to cover its ground, so its
  floor is half its carriageway, where the ground's inner edge comes to a point.
- **A road's line is normalised to the fewest corners and the roundest arcs that keep its survey**
  (`TracedAlignment`), the line and the survey standing within `SimConfig.CityGen.TracedCornerToleranceM` of each
  other both ways: a point that near the straight past it is no corner; two neighbouring corners turning one way
  that one arc rounds that near are one, standing where their outer legs meet — so a bend a mapper drew as a
  polygon is one arc; and **every corner is rounded as wide as keeps its stretch of the survey that near**, never
  tighter than half its carriageway, or as tight as its legs leave room for, a leg shared so each corner keeps
  the tightest radius that holds its survey. A line normalised into a corner its lanes would fold over is laid
  as surveyed instead.
- **And it is laid in the fewest pieces that keep its survey within its tolerance** — `SimConfig.CityGen.TracedLineToleranceM`,
  and `SimConfig.CityGen.TracedLineToleranceShare` of its carriageway's width further, so the wider the road the
  looser (`TracedPieces`): a run of its pieces that one arc, two, one corner between straights, or two bends and the
  straight tangent to both join from the pose it starts on to the pose it ends on is laid as those, and of as many
  pieces the fewest bent — none
  tighter than its corners may round, none off the map, and none further off the survey than the pieces it
  replaces where those already stood past the tolerance. **Every junction lays the movements it does with every
  road unmerged**, which lane joining which as the same turn: a road's end keeps where it stands, and keeps its pose,
  or the piece it ends in, where its junction's movements need them (`TracedStreets`).
- **A road the map cuts runs off it** (`CityPlan.JunctionArrays.RunsOffTheMap`, GEN-2b): where only that road reaches
  the place a way left the map, its junction stands nothing off, so its lanes run up to the map's edge, and its ground
  is laid on past the edge by `SimConfig.PastTheMapEdgeM` and cut by it — the boundary turns round its end off the
  map, and no kerb is struck along the cut. Nothing stands there: no box, no station and no turn.
- **No road is longer than a traced map lays one** (`SimConfig.CityGen.TracedRoadLongestM`): a way surveyed
  further without meeting another is cut into even lengths at places of two arms, which are the only
  junctions nothing meets at a traced town keeps (GEN-51).
- **What the place has and a generated town never does is kept**: a dead end (GEN-5a), a junction of six arms,
  two arms at a shallow angle (GEN-13), a one-way carriageway beside its twin (GEN-49) and a road passing over
  another where the survey says nothing meets. **So a traced town is not held to being driven round**
  (GEN-18, GEN-50): its dead ends are the place's own, and a car routed into one stands there
  ([the known gaps](../../../docs/index.md#known-gaps)).
- **Its shore is the sea moved onto the land as an area** and not as a line (GEN-2c): a pier narrower than
  its shore is shore all through, and the four rings nest by construction.

**GEN-2** `P6` Terrain, objects and agents are placed **plausibly**: the result must read as a small town, not
as noise.

**GEN-2a** `P6` A building stands along the street it fronts, not along a compass axis, and its entry point
sits between its front wall and the kerb on walkable ground clear of both. On a town whose streets run at
an angle the alternative reads as a field of sheds, and a door flush against a carriageway is one nobody
can walk out of.

**GEN-2b** `P3` **A map ends at its own edge, and nothing it carries stands past it.** The extent is the whole of
the world: there is no ground beyond it to walk, drive, classify or draw, so anything laid outside is a shape
hanging over the void. **What a stage draws through past the edge it cuts before the map carries it** — a
shoreline is drawn well past the town because a bank that closed inside it would be a lake, and the outline
the plan carries is that shape cut to the extent.

**GEN-2c** `P6` **Water meets the land at a shore and never at the grass.** A strip of shore of the town's own
width (`SimConfig.CityGen.ShoreWidthM`) runs along every bank, and **nothing the town scatters or builds
stands on it** — it is not the grass those take. The bank it follows is **the same wave the water is drawn
from**, laid a shore's width wider, so the strip is one width everywhere rather than a band fitted round a
curve.

**Each of its two edges carries a line of its own** (`SimConfig.CityGen.ShoreEdgeWidthM`), **in the colour of
the ground it meets and darker than that ground** — green against the grass, blue against the water — so an
edge reads as the shore's own shadow rather than a highlight laid over it. **They are drawn and never
classified**: the ground under a line is the shore either way, so the map carries the rings that leave them
rather than a kind of cell nobody could stand on.

**A bank is drawn through enough points that no chord of it stands off the curve**
(`SimConfig.CityGen.ShoreChordToleranceM`). The rings are the bank the ground is answered off as well as the
one drawn (TER-7), so the tolerance is how far both stand off the wave. The count is derived from the wave's
own curvature and never kept true by eye: a wild meander is drawn through more points and a straight coast
through few.

**GEN-3** `P6` Spacing must leave the city walkable: every building is surrounded by walkable padding, and no
pocket is too narrow for a pedestrian to pass.

**GEN-54** `P6` **A building stands against the pavement's outer face, and its door opens onto the walk.**
The face is the driven ground's own boundary moved by the figure the pavement is struck at (`TER-7b`,
`GroundRings.WalkEdge`) — the whole town's outer kerb in one set of closed lines, round every block, round
the outside of the town and round the mouth of every rank of bays. **It is walked and never swept**, so
nothing about a building's place knows that a road, a junction, a roundabout or a car park exists, and all
four are fronted on the same terms.

- **The front wall stands on the walk's own kerb** (`SimConfig.BuildingLineM`, `TER-3c.2`) and **no part of
  the building is nearer the carriageway than the face is** — its own street's or any other's. What holds it
  off is the distance the walk was struck at, read off the boundary.
- **The way in is on the line the walk beside it runs down** (`SimConfig.BuildingWayInM`, `GEN-2a`,
  `GEN-5`): the walking lane furthest from the carriageway, which is the one that passes the door. Clear of
  both kerbs, and on ground a body is actually held on rather than merely walkable.
- **It is square to the face there** and so to the street it fronts (`GEN-2a`), and **sized by the roof it
  will wear** (`CityGen.BuildingSizes`), so the picture drawn on it is the size the plan authored rather
  than the nearest thing to it.
- **Every place the face affords one is cut before any of them is filled, and they are filled in a drawn
  order.** Filled ring by ring instead, a town whose brief asks for fewer buildings than its frontage
  affords is built solid along whichever rings came first and empty everywhere else.
- **It runs after every stage that lays driven ground and before the props** (`GEN-10`, `GEN-6b`): a
  building is cleared against the ground the finished map answers with, and a verge crowded by buildings
  carries fewer props.
- **A count the ground cannot carry is what fitted** (`GEN-8`), reported and never retried.

**GEN-5** `P3` Connectivity is a hard constraint: the walkable terrain reachable by pedestrians forms **one
connected region**, the drivable terrain likewise, and every building entrance and every parking space
attaches to those regions.

**GEN-5a** `P6` **No road of a generated town ends in nothing.** A junction of one arm is a dead end (TER-5a),
the one junction a town sizes around a turning circle rather than around a crossing, and a generated town
lays every junction as the crossing its arms make — so a car driven into one could never leave it. The ends
a lattice and a spoke leave over are deleted with whatever is left hanging off them (GEN-8), rather than
grown on to meet something or kept as cul-de-sacs nothing planned. **A map laid in code may carry one**,
because it lays the ground that dead end needs along with it. **And a node a car can arrive at and not leave
is one of these however many arms it has**: a one-way street can make a dead end without changing the
count, so the layout's pruning asks it of the flows and not of the arms.

**GEN-6** `P4` Counts are a property of the map, never of a rule. A map declares its own size and roster;
everything else scales to the layout — props to the ground left over, car parks to the buildings the map
plans (GEN-53, `SimConfig.CarParksFor`), lights to what conflicts, crossings to where the walkable region would otherwise split.

**GEN-6a** `P6` **A prop stands wholly on grass.** Its whole girth and not its centre, because a bench half over
a kerb is a bench in the road; and the same test is what keeps a prop on the map (GEN-2b): off the town is
not grass. **And no collar over that**: the ground a candidate is cleared against is the ground that is
drawn (TER-7), the walk's own re-entrant corners included, so a candidate that clears it is clear and a
margin on top would only hold the verge back from the street it is a verge of.

**GEN-6b** `P6` **A prop's kind is a placement and not a picture, and the pass that laid it is what decides
which.** A stump and a planter are different kinds because they stand in different places, not because they
look unalike. **The props are laid in two passes**, and everything they need — the roads, the pavement, the
bays and the buildings — was laid before either of them runs. **On a traced town what the second lays is scenery**
(GEN-57, `CityPlan.Scenery`): drawn, and standing no body.

- **First the walk's outer face is walked**, because a verge is a line and not an area. A candidate stands
  out in the **verge** — the band of grass between `SimConfig.CityGen.PropVergeNearM` and `PropVergeFarM`
  beyond that face — and it is **furniture** or **planting** by the share the town furnishes its streets at
  (`PropFurnitureShare`).
- **The face is the town's own boundary moved by the figure the pavement is struck at** (TER-7b,
  `GroundRings.WalkEdge`) and never a line laid again beside a road. A road's half-width is the ground its
  lanes were laid inside and says nothing about where the concrete ends; the face says it everywhere at
  once, so a junction, a roundabout's island and a bay are lined by the same walk that lines a straight.
- **The band is measured to the prop's own near rim**, so what holds a prop off the walk is the distance it
  was placed at: a narrow look reaches the near edge of the band and a wide one is pushed out by its own
  width.
- **Then the ground the town is not on is swept**, on the stratified lattice, and everything within
  `SimConfig.CityGen.PropWildStandOffM` of the paving is left to the first pass. What is laid there grows
  **wild**. **The stand-off is past the verge and not up against it**, so the strip between the two passes
  reads as the edge of the town rather than as one scatter quietly changing what it is made of.
- **A prop laid along the face carries the face's own bearing there**, and a look with a front — a planter,
  a skip, a stack of crates — is turned onto it, so it runs with the street rather than with the compass.
  **Whether a look has a front is the art's to declare** (`PropVariant.Turns`) and never the placement's to
  assume: a tree seen from above has none, and turning one makes the same look read as several. **No wild
  look may declare one**, because the pass that lays it has no bearing to give it.
- **A verge is not planted end to end.** A share of the planting on any verge is drawn from the wild set
  (`SimConfig.CityGen.PropWildOnAVergeShare`), because a street carrying only what a town plants reads as a
  catalogue laid out along the kerb. Such a prop is a wild one standing where the town put it, so it is
  drawn upright like every other.
- **The pitch the face is walked at is longer than the props are wide** (`SimConfig.CityGen.PropVergePitchM`),
  so what spaces a verge is the step rather than the props' own girth against each other (GEN-6c). A kerb
  walked at a pitch inside a girth carries a continuous line of props and not a street; the town has to be
  visible through its own verges.
- **A kind carries its own size band, because its set was authored in one.** The wild set reaches the great
  trees (`SimConfig.CityGen.PropWildDiameterMaxM`) and the other two stop at the widest thing drawn for them
  (`PropDiameterMaxM`). The catalogue answers a size nothing was authored near with the nearest look it has
  and never resizes the prop, so a prop drawn outside its set's band is a planter stretched to the size of
  an oak — and a wild look on a verge is as wide as it would be anywhere else, which is what a street tree
  is.
- **A look that fits none of the three is not shipped.** The kinds are the whole of what a prop may be, so
  art that could only stand somewhere a prop is not laid — a grate, a hatch, a patch of paving — is deleted
  rather than filed under the kind it is least wrong in.

**GEN-6c** `P6` **Two props stand a clearance of grass apart, girth to girth**
(`SimConfig.CityGen.PropApartM`). Sharing ground is the floor of it: two discs laid over each other are one
obstacle drawn twice, a bush growing out of a tree. **Merely not touching is not enough either** — a row laid
rim to rim along a kerb reads as one long thing rather than several, where a verge wants to be seen through.
**The prop already laid is the one that stays**: nothing is nudged aside to make room, and a candidate that
would come too near one is not a prop (GEN-8, GEN-10). The two passes lay on patterns that know nothing of
each other (GEN-6b), so the rule is answered off a grid of the scatter's own, its squares the widest prop's
width and one clearance across, asking the nine round a candidate and nothing else.

**GEN-6d** `P6` **A prop's picture fits inside the disc the plan kept for it**: the longest side of the sheet
is the prop's own `diameterM` and the other follows the art's aspect. **What is drawn is what a car is held
off** — a sheet drawn to its own height instead reaches past the girth the town gave it, into the prop beside
it and into the road when it is laid along a kerb. The clearance in GEN-6c is grass between two pictures and
not slack for one of them to spill into.

**GEN-9** `P4` A building **declares what it is for**: ordinary, or one of the uses a service is stood at — a
hospital (AMB-1), a police station or a depot (SRV-1). It is a field of the record and therefore a fact
about the map, the same for every run of it and for every agent seed, and it moves only when the map does.
**Which buildings they are is settled as the town is laid** (`GEN-55`), by where their parking went rather
than by a sweep over the buildings afterwards. **A building serves one use at most** — one byte cannot say
two things — and a map that declares none is a map whose services do nothing, which is a state the census
reports rather than one anything papers over.

**GEN-55** `P5` **A service building is stood at the end of the car park that was cut for it.** A hospital,
a police station and a depot each need somewhere for their own vehicles to stand (`GEN-4k`, `AMB-1`,
`SRV-1`), so the parking comes first and the building is stood on it — rather than the buildings being laid
and one of them then found to have a car park outside it.

- **A yard is a car park of its own** (`GEN-53`): one rank, on one side, as wide as a car park gets. One
  side because there is one building and it stands past the far end of the rank; the widest because the
  apron a station holds is the bays nearest its door. **The side faces the town's middle**, because the
  edge of a town is where the ground to stand the building on runs out.
- **The yards are cut before the town's own car parks and on top of their count.** Each car park is cut at
  the site furthest from every car park already cut (`GEN-53`), so taking the services first, one use across
  every district before the next, puts them as far apart as each district's roads allow with no spacing rule
  of its own. The parking a town's buildings ask for is its people's, and a district's services are not a
  share of it.
- **How many there are is the town's districts'** (`GEN-56`), and a district whose ground carries no yard
  stands none of that use (`GEN-8`), which the census reports (`AMB-2`, `SRV-2`).
- **The building stands on the rank's own normal**: past its far end, square to the street, **in the middle
  of it**, with its door on the walk that wraps the rank. **That is the face again and not a second
  placement** (`GEN-54`); what differs is only which stretch of it, and that this one is centred rather than
  stepped to.

**And nothing is built on the rounding round the end of a rank, or down its side.** A car park's stretch of
face is the flat over the line its bays end on, a rounding round each corner of the rank, and the two sides
running back to the street. **A building fronting a car park stands wholly on the flat** — the limit is the
rank's own reach along the street and not a figure of its own. One perched on a rounding fronts the mouth of
the car park at an angle, and one down a side fronts the row of bays edge-on.

- **So a rank narrower than the frontage offered carries nothing behind it** (`GEN-8`): what is behind three
  bays is a strip of verge, which is the rule holding rather than failing.
- **It is why a service is centred rather than stepped to.** The face is walked at a pitch (`GEN-54`), and a
  hospital as wide as its own yard has to be on the middle of the flat to the metre, so the station the
  rank's normal reaches is slid along the flat to the middle before the building is stood on it.

**GEN-56** `P5` **Every district stands one hospital, one police station and one depot, each inside it**, where
the town plans a building at all. A district is one of the pieces the town's wheel cuts the ground into — a
sector between two spokes, inside the orbital or outside it — and **the plan carries the wheel**
(`CityPlan.Districts`), so the district a point stands in is still a question once the town is laid: it is
what a service's beat is kept to (`SRV-5`). A map not laid on a wheel is one district.

- **Inside it is the building's ground and not the road's.** A yard is cut only where its building's near face
  and far face, across the rank on the side it faces, both stand in the district. A district's edge is as often
  as not a spoke or the orbital: asked of the road alone, a yard cut into one stood its building over the road
  in the next district, and a yard cut into one facing into the district is that district's.
- **A district with no street stands nothing**, which is an outer sector the town never reached as often as
  a sector under the water.

**GEN-7** `P5` Initial state: cars start **stopped in parking spaces**, and **a person starts inside the
building the map stood them at**, part way through a dwell (PER-11). A trip ends by walking through a door and
dwelling, so a body that begins there begins in the state every later trip returns it to (`PER-25`). **Which
building is read off the pose the plan left the body in** — the way in it is standing at — so the plan carries
nothing to say it. **Where people live, every car is somebody's** (PER-29): the plan stands no car of its own,
and the town stands each person's in the free bay nearest their door. **A town nobody lives in stands the
brief's cars in bays of its own car parks**, never a service's yard (GEN-55); **and one that cut no car park
stands them on its lanes**, one a lane, and they tour (CAR-8) — the fixture, which asks for no buildings and is
owed no parking. **A traced town cuts no car park** (GEN-57), so nobody living in it is handed a car, and it stands
its map's cars on its lanes beside its people in the same way.

**How many of each is the brief's, or a traced map's own** (`TracedMap.Population`), and **the bound is the town
rather than the count**: a person is stood at a way in, and a car the plan stands in a bay or on a lane long enough
to hold one, so a brief asking for more than the ground carries gets what fitted (GEN-8). A person with no free bay
within a walk of their door owns no car.

**GEN-8** `P6` **No candidate city is ever rejected.** A violation of GEN-3…GEN-5 is a defect in the
arrangement rather than a seed to throw away, and the gate that catches it is the suite. Where the ground
cannot afford what the brief asked for, **the town is what fitted and the shortfall is reported** — by the
census, as every other absence is — and where a piece of a town is left joined to nothing, that piece is
deleted rather than linked up to whatever is nearest.

**GEN-10** `P4` **Every stage of a generation runs once**, in the one order they can run in: the water before
the nodes that avoid it, the districts before the streets laid inside them, the car parks before the
buildings stood on them, the roads before the boundary the buildings front, the buildings before the props
that take what is left, and the bays before the cars standing in them. **A stage constrains the next rather
than checking it afterwards**, which is what makes the properties GEN-3, GEN-4 and GEN-5 name true by
construction rather than true on the attempt that happened to pass.

**A road is drawn as the link is offered, and a link the line cannot be drawn for is not a road** (GEN-47,
GEN-49). Nothing is laid and taken back: no pass refuses a road once the town stands, no repair runs behind
such a refusal, and nothing is drawn twice — so what the layout holds at any moment is roads that can be
drawn where they stand. **The stages that change a road's shape ask first**: a run is offered as the one road
it would be before its pieces are given up (GEN-51), and a junction is opened into a ring only once every arm
the ring moves has been drawn where the ring would leave it (GEN-19).

**What a refusal costs is the road and the connectivity behind it, and never a second attempt at the town.**
The pieces a refusal leaves joined to nothing are deleted (GEN-8, GEN-5), and how much the town paid — the
junctions nothing meets at, the runs that could not be joined — is the census's to report.

**GEN-11** `P4` **Each stage draws on its own stream of the world seed.** Retuning what one stage does may not
move what an earlier one laid, so a change to the props cannot reshuffle the roads and a map is the same
town every time it is opened.

**GEN-46** `P3` **A lane ends where the seed says it ends, and the road is laid to meet it.** Every arm of
every junction carries a **connection point** for each way its road is driven (TER-4d): they stand on the
line square to a bearing drawn for that arm, a standoff (`SimConfig.CityGen.ConnectionStandoffM`) out from
the node, half a lane either side of it on the side the traffic keeps. The bearing is the chord to the
neighbouring node turned by an angle drawn inside `SimConfig.CityGen.ConnectionJitterDeg`, tapered on a
short link by what that link has room to turn through. **The two ends of a link are drawn independently**,
so a road has two bearings to satisfy and they do not agree.

- **They are stored nowhere and drawn again wherever they are wanted**, from the world seed and the two
  junction centres the link joins, which is what keeps the lanes derived from the plan. One function, two
  callers, one answer.
- **Everything a link is drawn with is keyed on the link and never on a walk over the town** (GEN-11): the
  arm's jitter and the road's own wander both. A road's shape is then a function of its two node centres, the
  places it passes and its class, so offering the same link twice draws the same road and deleting one road
  moves nothing that stayed — which is what lets a road be drawn as it is offered (GEN-10).
- **A bridge and a ring piece take their bearings rather than drawing one** (GEN-14a, GEN-19). Both are
  shapes settled elsewhere, so the jitter is nil for those links and a ring arm's lead bends with its own
  circle rather than leaving straight off the tangent to it. **The bend it takes is the ring piece's own** —
  the curvature the layout sized the circle at — never a circle fitted back through three of the ring's
  nodes, which is the same figure worked out a second way.
- **A road laid straight takes the chord and draws nothing** (GEN-47): each arm leaves on the chord to the
  place its road runs for. It is a fact about the road and not a kind of link, so the plan carries it per
  road (`RoadArrays.LaidStraight`) — a derivation that drew the jitter again would draw an arm the road was
  never laid to.
- **The disc a junction is drawn on is the standoff** (`SimConfig.JunctionRadiusM`) and the arms follow it.
  Sized off the arms instead, the standoff would be read back off a disc sized by the arms that end at the
  standoff.

**GEN-47** `P3` **A road is the line that leaves on one of its bearings and arrives on the other**, bending no
tighter than the radius its own class's design speed affords on tarmac (`SimConfig.CarCorneringRadiusM`),
which is derived from a speed and a grip and is never authored as a radius. **A street is laid straight or
it wanders, and every district lays both**: a grid mostly straight ones and a loose district mostly wandering
ones, each district at a share of its own drawn near its kind's (`CityGenFigures.GridStraightShare`,
`LooseStraightShare`), taken as a count of its streets that is never all of them and never none. A street
laid straight has each arm on its chord (GEN-46) and no wander of its own, so it is one straight between two
places, and a corner it was joined through (GEN-51) is rounded at its class's own radius with a straight
either side, tighter only where the legs have no room for that. A street that wanders does so bounded by
the block spacing, so no street may reach the one a block over. A joined road is laid the way most of its
length was. **A road that cannot be
laid inside those bounds is not laid**: the wander gives way first, and a link that still cannot be met is
deleted with the layout repaired behind it (GEN-8, GEN-5). **A roundabout's ring has a floor of its own**
(GEN-19), because the whole of one is a corner.

**A corner a road was joined through has a junction's floor and not a class's** (GEN-51, GEN-48). It was a
junction, and what a car held there was the movement across it; measuring it against the road's own design
speed would refuse the road for a bend the town already had. **The road is what says so either way** — a
driver reads every arc of the line ahead of it and is down to that arc's own cornering speed before it
arrives, so a corner is driven at the speed it affords wherever it stands.

**GEN-48** `P3` **A junction offers the movements a car at its own design speed can hold**
(`SimConfig.CityGen.JunctionDesignSpeedMps`), and no others: between every point a car enters a node on and
every point of a *different* arm it leaves on, the line a car drives is laid and the turn it makes is
classified. A pair of points on one arm is the turn in the road TER-5f bans and is never joined, which is a
fact about the arm and not an angle to be measured. **But a junction may not refuse its way out of being
reachable** (GEN-5): a lane whose every movement is tighter than the bound keeps the loosest of them, and so
does a lane every movement onto which is, because a car that arrives has to leave and a lane nothing reaches
is a hole in the drivable region.

**GEN-15** `P4` **A lane is the width the town is laid in, and every road is laid at it.** A carriageway is its
lanes of the one standard width (`SimConfig.LaneWidthM`) side by side — one each way it is driven on a road
the generator lays, and on a traced one as many each way as its survey says (TER-4d, GEN-57) — and the walk
beside it two walking lanes of theirs (`SimConfig.WalkingLaneWidthM`),
whatever the road is for and wherever it stands: in a town whose roads each chose their own width, nothing
quoted against a lane — a line's offset, a kerb, a bar's span, the room a body has to step round another —
means the same thing twice. A map laid to measure one thing may still lay ground of its own, because a pad
driven in circles is a surface and not a street.

**GEN-13** `P6` **A junction's arms stand square enough to be a junction.** An arm that would lie against one
already there is refused, because two carriageways meeting at a shallow angle overlap for tens of metres
and the fillet, the crossing and the bar on either of them are then laid over the other. What that refusal
leaves unreachable is deleted with its own piece (GEN-8).

**GEN-51** `P4` **A junction is a place roads meet, and a road runs between two of them.** A node two roads
merely carry on through is not a junction: it is where the town's own arithmetic stopped a line — a spacing
along an arterial nothing welded onto, a lattice point the prunes left holding two of its four arms — and
the two arms there are **one road**, laid through the place the node stood so that the corner the town had
is the corner it keeps. **A road laid straight rounds the corner rather than passing its point** (GEN-47):
passing it would bend the whole of both legs.

It is a rule about the layout and not about the picture. A junction is a standoff every lane ends at, a
movement between each pair of arms, a claim on the ground those take and a place the router plans through,
so a node nothing meets at is all of that laid in the middle of a road — and the town's junction count, lane
count and claims then say more about where the generator's arithmetic landed than about the town.

**It holds however sharply the two arms meet and whatever they are.** A corner is not a reason to leave a
junction standing, because a car took that corner at a junction's speed — so a joined road turns at a
junction's floor (GEN-47) rather than at its class's. A street and an arterial are joined and come out as the
arterial (GEN-16); every road runs both ways when this is settled, so which of them later run one way is
GEN-18's and is asked of the town this leaves.

**A run is offered as the one road it would be, and where that road cannot be laid the run is cut in two**
(GEN-10): the place in the middle of what it passes stays the junction it was and each half is offered in its
turn, so a corner that cannot be drawn costs that corner and never every place the run went through.
**Nothing is laid and taken back** — the joined line is drawn before the pieces are given up, and a run
offered as one road is held to the ground of the roads that stayed and of the runs joined before it, because
two joined roads could each clear the other's *pieces* and still be laid into one another.

Two kinds of node stand whatever they carry, and neither is a place the town's arithmetic stopped a line:

- **A bridgehead** (GEN-14a), where the carriageway becomes a deck. A bridge is its own road between its
  own two nodes, laid straight over the span, and is never part of a longer one.
- **A ring node** (GEN-19), which is a piece of one junction laid out as a circle.

And **one shape a road cannot be** leaves one node: a run that comes back where it set off has no second end
to be a road between. It is shortened by one place and the rest of it is still joined.

**Nothing else is a reason.** Two roads already running between one pair of junctions is not one, however
awkward the pair is to merge a boundary through — a merge that cannot close round a shape the town lays is
the merge's to answer for (TER-7b, `LaneShell`) and not a licence to lay the town differently.

**GEN-52** `P3` **A junction may be cut into a road that already stands, and the road does not move.** The
road is parted at a place along its own line into the stretch before the junction and the stretch after it,
the node stands between them, and the town gains whatever arms the cut asked for. **Every metre that was
driven is still driven**: the two pieces are the arcs the road was laid as, cut, and the movement the new
junction draws straight across is a biarc between two poses of one arc, which is that arc. It is the one
place the plan is changed after it is laid, and it is allowed because it changes nothing.

**It inverts the inversion, and the plan says which roads it inverted.** Everywhere else the arms are drawn
first and the road is laid to them (GEN-46); here the road came first, so **both arms of a cut road are read
off the line it already carries** — the stand point is where that line ends, the bearing there is the line's
own, and the lead is the one arc joining the node to it (`CityPlan.RoadArrays.Cut`, `ConnectionPoints`).
**Both and not the cut one**: an arm is drawn toward the far end of its own road, and a cut moves that far
end, so a piece that went on drawing its other bearing would draw one its line was never laid to.

- **A cut lands inside one arc of the road, with the cut's own standoff of that arc either side of it.** A
  lead is one arc, so a node placed across a joint in the line would be one whose arms stand where the road
  does not go — and that is what makes the reading exact rather than a curve fitted through the ground.
  **How far back the road is parted is the cut's to ask for** and not the standoff every other junction
  keeps: a junction whose arms are spread along the street has to stand off the whole of them (GEN-53).
- **A cut node is a junction like any other**: it stands a locality clear of every node that is not the
  cut's own (GEN-16), its arms stand square enough to be a junction (GEN-13), every piece and every arm is
  held to the ground the town already holds (GEN-49), and nothing it lays stands on water or off the map
  (GEN-14, GEN-2b). **The cut's own nodes are exempt from the locality**, on the terms a roundabout's are:
  they are one place laid out with a stub rather than two spacings that landed on the same ground.
- **Nothing is laid and taken back** (GEN-10). Every piece and every arm is drawn and asked about before any
  of it is installed, and a cut that fails one of those leaves the road exactly as it was.
- **It is the last thing done to a layout.** A cut road's arms are its line's own, so an offer weighed
  against the chord it was joined on would be weighed against a bearing it does not leave on.

**No stage cuts one any more**: a car park stands beside its street rather than in it (GEN-53). What is kept is
the reading — a road laid where it already stands has its arms read off its own line — which a car park's bays
are laid by.

**GEN-53** `P6` **A car park is a rank of bays laid off the kerb of a street that stays whole, and every bay is a
short road of its own joined to nothing.** The street is not parted and no junction stands at a car park; a bay
is a lane's width of ground square to the kerb, **driven both ways over its one line** so a body standing in it
is on it whichever way round it stands, on two nodes of its own. What gets a car in and out is its own
manoeuvre (GEN-4f), and nothing downstream carries a rule about how a car park is reached.

- **The bays of one side stand in a rank off the street, a lane apart and centred on the car park's middle**, a
  bay being a lane wide (GEN-15) and a rank sharing the line between each pair of them (GEN-4c). **Every bay of a
  rank runs square to the street at that middle, so a rank is parallel and not a fan**, and their far ends lie
  on one line parallel to the carriageway.
- **A bay is an apron and a space.** The space is one length the whole town over
  (`SimConfig.CityGen.BayLengthM`), **longer than the longest vehicle the town draws** — every bay is one anything
  in the town can stand in — and it stands back from the carriageway behind an apron of its own
  (`SimConfig.CityGen.BaySetbackM`) that a car turning in or backing out swings across rather than across the
  street. **The apron is also what keeps a rank's end a corner the walk can be struck round** (TER-3c.8).
- **Each bay's mouth runs back over the street's own ground by a hair** (`SimConfig.CarParkKerbOverlapM`) — read
  under the bay's own mouth off the kerb that is actually there, so the rank and the street are one piece of
  tarmac on a street that bends a little, and on one that does not, ground the two do not share (TER-5c). **It is
  the street's own lane that the rank stands off, not the line the street was laid down**: a street driven one
  way carries its one lane on the half its traffic was moved onto (TER-4d), so the two sides stand off different
  metres of it and get the same kerb (`CarParks.LaneTowardM`). **A road type is never assumed here.**
- **The bays are counted before the place is chosen.** How much street a car park takes is its longer rank and
  the frontage a car manoeuvres over past either end of it (`SimConfig.CarParkFrontageM`), so the size of a car
  park decides which places can carry one. **A place is never taken and the bays that did not fit taken back off
  it** (GEN-10); a town with nowhere to put the car park it drew lays fewer of them (GEN-8).
- **Nor is a street that bends too far under the rank** (`SimConfig.CarParkCurvatureMax`). A rank is laid off the
  tangent at its middle, so a street turning away under it is one whose kerb the rank no longer faces. **How far
  a street may bend is read off how far its kerb may leave the rank's line** at the rank's end
  (`SimConfig.CityGen.CarParkOffLaneMaxM`) and nothing else, so a wide car park asks for a straighter street than
  a narrow one.
- **Every node of a bay owes the town a locality** (GEN-16), the one over the street as much as the one past the
  far end, and a bay's ground is held to the ground every road already holds (GEN-49) but its own street's.
  **A car park's own nodes are exempt from each other**, on the terms a roundabout's are: one car park laid out
  along a kerb rather than spacings that landed on the same ground.
- **A bay's arms are read off its own line** (GEN-52's reading, `CityPlan.RoadArrays.WasCut`): it was laid where
  the kerb is, and each node stands a lead off an end of it.
- **How many bays a side carries is a handful or none** (GEN-4b), and **not none on both sides** — a car park
  with no bay either side is no car park.
- **A road that runs one way carries car parks like any other** (GEN-18, TER-4d): each bay is reached the one way
  the street runs, which is every way it has.
- **How many the town has is counted off the buildings it plans** (GEN-6, `SimConfig.CarParksFor`): the map
  says how many buildings it is a town of and the engine how many of those one car park stands the cars of
  (`SimConfig.CityGen.BuildingsPerCarPark`), so a map that grows carries the parking for what it grew into.
  **They are spread rather than scattered**: each is laid at the site furthest from every car park already
  laid, and what keeps two of them off each other is the locality every node owes (GEN-16). **What the ground
  cannot carry is what fitted** (GEN-8), reported and never retried.


**GEN-49** `P3` **A junction is the only place two roads may touch.** No road crosses another, runs into the
side of another or lies along one: two roads that are not joined at a junction stand at least one road's
whole width apart (`SimConfig.RoadFootprintM`), **measured between the lines they were laid as**. Ground two
carriageways share outside a junction has no box, no crossing and no stop bar on it, so nothing that drives,
walks or claims a way across it has anything to say about who goes first.

- **It is a bound the laying holds and not a pass that deletes what it missed** (GEN-47, GEN-10): the line a
  link would be laid as is measured against the lines already standing as the link is offered, so a pair that
  would share ground is a road the town never had. A separation measured on chords would say nothing about
  the roads that were laid, a road free to reach its own end bearings not being bounded by its chord.
- **Which of the two gives way is the order they were offered in** (GEN-16): the arterials are laid before
  the lattice and a stub before nothing, so a street offered against ground an arterial holds is the one
  refused and no pass has to weigh the pair afterwards.
- **It holds from the first road** (GEN-16): the nodes are settled before anything is laid, so two roads
  sharing ground are two roads the town may not have and never a pair whose junctions were going to be
  merged.
- It is a rule about **roads**, not about the paint or the ground: what a junction's own arms may do to each
  other is GEN-13's.

**GEN-18** `P6` **One-way streets are scattered over the whole town, no two of them meet, every one of them
arrives where there is still a choice, and every one the town keeps is one it can still be driven round.** It
is a rule about the **streets the scatter chooses** and not about every road that runs one way: a
roundabout's ring is one direction laid at one place and is none of this pass's business (GEN-19), though a
street taken at one of its nodes is still two of them meeting. **A car park parts no street** (GEN-53), so it
makes neither a meeting nor a crowding.

A street runs one way (TER-4d) wherever the scatter puts it — no district, bearing or side of the orbital
decides it — and the scatter is three relations and nothing else: **no junction carries two of them**, so
each is entered and left on roads that admit both ways; **no two of them stand within
`SimConfig.CityGen.OneWayApartMinM`** of each other, measured between the middles of the chords they are laid
on, which spreads them across the town rather than gathering them into one district; and **each arrives at a
junction of four arms or more**. **An arterial never runs one way, nor does any street that meets one**: they
are how the town is carried between districts, and a district entered off a road that admits one way only is
a district drivable one way round.

**The arrival end is the one that costs the junction something.** A street arriving takes one way out away
from every approach there, so at a node of `n` arms whose others all run both ways an approach is left with
`n − 2` ways on: four arms leaves two, and three leaves one — a car driven through a junction it decides
nothing at, which is a shape every layer below would then need a rule about. **Which way a street runs is
therefore where it may arrive**, and one that may arrive at neither end is not taken. **The departure end is
asked nothing**: a street leaving takes no way out away from anybody.

**And a street the scatter takes has to hold its own bends on the half it is driven** (GEN-47, TER-4d). A
one-way street stands on half of the carriageway it was laid as, and a line moved half a lane towards the
inside of its own bend is half a lane tighter there — so a street laid near its class's floor is one the
scatter may not take, asked of the line offset the way the plan will offset it rather than of a figure
standing in for it.

**What the town keeps is settled against the movements and not against the roads.** From every movement on
the network every junction must be reachable, with no turning round in the road (TER-5f) — a block whose
streets all ran inwards has a way out of every junction on it and is still somewhere a car drives into and
never leaves. A street the town cannot afford **runs both ways again** (GEN-8), one at a time and in the
order they were chosen; nothing is laid twice, no seed is thrown away, and opening one costs the scatter a
member and never its shape.

**GEN-19** `P6` **A roundabout is a ring of ordinary junctions joined by one-way arcs, and nothing else.** It is
not a kind of junction, not a shape the plan carries and not a rule anything downstream has to know: a node
is opened out into a circle of nodes — one for every road that met it — joined into a closed carriageway
driven one way round, and each of those roads is cut back to meet the ring **on its own bearing**, so
nothing bends to reach it and every arm arrives square to the circle. What the plan then carries is roads
and junctions, and the only thing said about the ring is **which of its roads circulate on it**, so that a
one-way road the scatter took (GEN-18) can be told from one nobody chose.

- **Circulating traffic is driven over what is entering, and nothing grants it that.** The two ring arcs
  at a node are two pieces of one circle, so the movement between them goes straight on where the movement
  in off the arm is a turn, and the ranking does the rest (TER-5e). It is the whole reason a roundabout is
  laid as a circle rather than as a polygon, whose every circulating movement would be a turn.
- **Which way it is driven is which side the traffic keeps**: the island stands on the side a car does not
  drive against, so a car goes round it turning away from its own kerb.
- **Nothing on the ring is lit** (TLT-3). A timetable over a ring node stops the circle to let an arm in,
  which is the one thing a roundabout is laid instead of.
- **No car park stands on one** (`CutJunctions.SitesOn`, GEN-53). A ring is one junction's worth of ground,
  and a bay off it would be one a car turns into off circulating traffic. A building fronts one on the terms
  it fronts any street, off the walk round it (GEN-54).
- **Every piece of the ring is one arc of one circle, node to node**, so the ring is smooth: there is no
  straight in it and no join a reader can find. Its bend is never tighter than the radius the roundabout's
  own design speed affords (`SimConfig.RoundaboutDesignSpeedMps`), which is the exception GEN-47's floor
  names for it — the whole of a roundabout is one corner — and it is the one road laid to its own circle
  rather than to the bearings its arms were drawn with (GEN-46).
- **What that costs is that the ground of its entries is struck on a curve.** A junction's kerb fillets are
  the arcs tangent to the two kerbs there, and a kerb that bends is a circle rather than a line — so the
  corner is solved between the shapes the kerbs are drawn along and never between the lines their bearings
  make. Struck on the lines, a ring's entries are filleted to points off their own tarmac and the pavement
  round it comes apart at every one of them.
- **Nothing at all is painted on the ring.** No zebra (TER-6): a walk laid across the circulating
  carriageway is a walk across the traffic a roundabout exists to keep moving, and the entries carry the
  crossings, which is where somebody getting round one crosses. **What that leaves is an island nobody walks
  onto**: nothing stands on it, nobody is put down on it and no trip ends there (GEN-5). And no bar either: a
  bar is where a driver holds when the junction refuses them, and circulating traffic is never refused.
- **A ring node is a junction like any other**, so the road between two of them is a road (TER-5a) and no
  piece of it stands over water (GEN-14) — but **what two of them owe each other is neither that road nor a
  locality** (GEN-16). They are one junction laid out as a circle rather than two spacings that happened to
  land on the same ground. What they owe each other is what the two roads *leaving* them do: the ground one
  road takes (GEN-49) and a pavement's width on top of it, since two mouths whose paving abuts is paving
  with nothing to wrap round. **That, and its own design speed's floor, is the whole of what sizes a
  roundabout — so the circle laid is the smallest one its arms and its speed allow.**

**Where they stand is where a district leaves the town**: a junction of **four arms or more** that some
district's own street meets an arterial at, no nearer another roundabout than
`SimConfig.CityGen.RoundaboutApartMinM`, whose arms all stand inside half a turn of each other and off whose
ground every other road and junction already stands clear. **Three arms are a junction and not a
roundabout** — a circle there sorts out one conflict the ranking already sorts out standing still, and it
charges every car through the node a detour to reach the arm opposite. A bridgehead is never one, because a
deck cannot move (GEN-14a). **Nothing is laid and taken back**: all of that is asked before the node is
opened out, and a node that fails any of it stays the junction it was (GEN-8, GEN-10).

**GEN-50** `P3` **No lane dangles**: every lane the town lays is one a car can be driven onto and one it can be
driven off again — by a movement at its ends, or by moving across from or onto a lane beside it running its way
(`CAR-53`), which is how a lane lost or gained at a node is driven (TER-5j). It is a local fact and not a connected one — a movement leaving a node needs some road
other than its own arriving there, and a movement arriving needs some other road leaving — so a node a car
can reach and not leave is a dead end whatever its arms come to, and the layout takes it away with the ones
that carry a single arm (GEN-5a). **Where a road of two ways meets a road of one**, the way back out of that
node is a lane no movement could ever arrive on, since the only thing that could reach it is the turn round
in the road TER-5f bans.

**GEN-16** `P6` **Two of a kind standing inside a locality of each other are one thing and not two**
(`SimConfig.CityGen.LocalityM`). A town is laid at several spacings that know nothing of one another — an
arterial's, a lattice's, a frontage's — and where two of them land almost on the same ground what comes out
is a pair nothing downstream can make sense of: two junction boxes with their fillets, crossings and bars
laid over each other, or two car parks with a stride of pavement pinched between them. **Which of the two is
left is decided by what hangs off it**: a junction the layout placed is merged rather than refused, because
refusing one deletes every road at it (GEN-8), where a car park is simply not laid — it is laid beside a town
that stands without it. Both are nodes, and a node is a point measured centre to centre.

- **Two nodes are one junction**, standing where the node the town cares more about stood, and every road at
  either of them meets at it. A bridgehead cannot move, an arterial's line is the town's and a street is what
  bends to meet either — so where that leaves two arms lying together it is the street that is dropped
  (GEN-13), and whatever that leaves hanging goes with its own piece.
- **A cluster and not a pair, and settled before the first road is laid.** Three nodes a stride apart in a
  chain are one place: asked pair by pair, the third is left where it stood and whatever ran through it
  hangs off a town it no longer reaches. **It is asked of the nodes rather than of the roads at them**, which
  is what makes it cost nothing — a junction moved once a road stands is every road at it drawn again.
- **A car park inside a locality of a junction is not laid** (`CarParks`, GEN-53) — its site and both nodes of
  every bay alike, against every node the town already has, the car parks laid before it included. So two car
  parks are kept apart by the nodes they are made of, and the one that stays is the one laid first. **This is
  the one place the rule refuses rather than merges**, and it can because nothing hangs off a car park: the
  site is left out, and a count the town could not carry is reported by the census (GEN-8).

**And a roundabout's own nodes are exempt** (GEN-19). They stand inside a locality of each other on purpose:
a ring is one junction laid out as a circle by one construction, not the accident this rule is about, and
what its nodes owe each other is the road between them.

**It names those two and nothing else.** Everything else a town lays more than one of is already spaced by a
rule of its own — a building by the padding a walker gets past it (GEN-3), a prop by the corner the pavement
turns (GEN-6a) and by its own girth against the props already laid (GEN-6c) — and a second rule over that ground
would be a second answer to it (SIM-7).

**GEN-14** `P6` **Nothing a junction is made of stands on the water.** No node is placed on it, so no junction,
no kerb fillet, no crossing and no bar is ever laid over it, and a town whose middle falls in its own river
moves its middle to the bank rather than building there.

**GEN-14a** `P6` **A road standing over water is a bridge, and a bridge is its own road**: one straight span
between a bridgehead on each bank, no longer than the deck a town builds
(`SimConfig.CityGen.BridgeDeckLongestM`), carrying that deck the whole of its length (TER-3b). Nothing else
crosses — not a street, not a piece of the orbital's own arc, which is laid straight over its span or not at
all. A crossing these bounds refuse is a road the town does not have, and whatever that leaves unreachable
is deleted with its own piece (GEN-8) rather than reached some longer way round.

**GEN-14b** `P6` **A bridge crosses a river and never the sea**, because a coast has one shore inside the town
and a deck laid over it reaches nothing. **And it crosses as squarely as the layout affords**: the wheel is
turned so a spoke runs down the river's own normal, and an arterial meeting the water carries a node on each
bank — so what spans it is the shortest run that path affords rather than the distance between whichever two
nodes the ordinary spacing happened to leave either side of it.

## The maps

**The list of maps is one list** (`Maps`), read by the start menu, the in-game picker and the command line;
every check, probe and shot names the map its fixtures live on.

**The idle ring** (`IdlePlan`) is one of the two maps laid in code, the one the game opens on (GEN-1b), and it
measures nothing. It is **one loop of road with nothing else on it** — no building, bay, paint, light or
walker — carrying **an escorted convoy one way round and one car the other**: an armoured car between two
police with their beacons up, and a sports car on the opposite lane of the same carriageway. What it is
chosen against is that it never stops being worth watching and never needs anybody's attention; how it is
laid — to one view (`OBS-1b`), as a square with rounded corners, cut into four roads, with the escort held to
its charge's pace and following closer than traffic does — is `IdlePlan`'s own documentation.

- **Nothing drives it that is not already in the town**: with nowhere to be on the map, the rule that drives
  an empty map's cars (`TownWorld.DriveTheEmptyMap`) puts each on the lane under it and the ordinary
  catalogue does the rest.
- **Its cars are dressed by the map and not by the fleet's wrap** (`TownWorld.LookOf`): a look is what a map
  asks for and never a duty — a police car is one with a station (SRV-2), and a car in police paint on a map
  with no station is an ordinary car in service paint, which is the state `EVA-7` already names.
- **Nothing turns at any of its nodes** — each joins one road to the next and offers one way out — so nothing
  on it indicates, gives way, or is refused anything (`CAR-14.1`, `SIM-7`).

**The scenario map** (`Exam`, [exam/](../exam/)) is the other, and it is the one laboratory this build ships:
**a lattice of junctions with one traffic scenario staged at each** — a card of `ExamCards`, saying who is
at the junction, what the engine is to make of it and which of the engine's own rules that exercises. It is
an end-to-end case of this engine and not of anybody's rule book: a car alone, cars whose ways share no
ground, cars whose ways cross, queues, lights, somebody on foot, a car on a call, a one-way street. The
staging and the verdicts are the bench's (`ExamDrive`, `ExamJudge`), read by `--bench exam`, by the panel on
a run of `--map Exam` and by the exam tier alike.

- **A card is written once, in its own frame, and turned onto a cell of the shape it asks for**: a crossroads
  or a ring in the middle of the lattice, a T on its edge, a dead end at the head of a spur from a corner.
  A table asking for more of a shape than the lattice has, or staging a car on an arm its junction lacks or
  against a one-way street, fails when the map is laid.
- **Its roads are laid as the generator lays them** (TER-5d): the town's own draw gives each end its arm, and
  the road runs stand point to stand point on those bearings — a road laid straight from node to node is
  one whose lanes end where its line does not go, and whose junction lays no movement. So the lattice is
  square in its nodes and not in its streets. A ring and a one-way arm are laid as GEN-19 and TER-4d lay
  them.
- **It lights the junctions its cards are about and no other**, where a generated town draws its share of
  them (TLT-3). The bars are the town's own, a setback behind the band the traffic is held at.
- **Somebody on a zebra is stood at the edge of its paint and walked paint to paint**, never from the
  pavement: a walk from the pavement is not reliably routed over a zebra in this build (the known gaps), so
  a walker sent from the kerb can go round the block and the card would ask nothing. It steps into the
  middle of the lane the car arrives in as that car comes within a stated distance, stands there a moment
  and goes on to the far edge — a body crosses a carriageway in about a second at the town's pace, and
  one that did not stand would be out of the car's way before the car could have reached it. **A road
  anybody walks is driven both ways**, because a one-way road is one lane wide and the far side of its
  zebra is the lane its traffic drives.
- **Somebody walking the pavement is walked round a corner**, from one arm to the next, past the kerb ends of
  both zebras and over neither — which is where a pavement's ribbon comes nearest a lane's.
- **It asks nothing the engine refuses to do**: nothing turns round in a box (TER-5f) or at a dead end
  (TER-5a), nothing overtakes, and nothing is held to a rule of the road the town does not make its own.
- **What it claims is the cards**, gathered one claim to a family (`ExamWatch`), and **every card passes**
  ([verification](../../../docs/verification.md#the-scenario-map)): a card that fails is the engine not
  doing what its own rules say.

**The fixture map is not optional.** It is what every detailed check is staged on: small enough to build
in a fraction of the time, and laid from a brief (`towns/Test.json`) that asks for water, three districts
and a dozen cars and for no building — so it carries no car park, no crossing and no walker, and none of
the ground a bay or a zebra is answered as. Detailed questions asked of whatever the big city happens to contain are a
different question every time somebody edits the city. **It is a brief rather than a file**, which costs it
the one thing a fixture is for — a town that stays put when the generator moves — and that cost is named in
[the known gaps](../../../docs/index.md#known-gaps).

**Ask a whole city the shallow questions only** — it validates, its junctions are junctions, no lit
junction shows two conflicting greens, nothing is laid on its water. Detailed geometry is asked of named
places on the fixture map.

**Every map states what it claims, and a run of it says whether it kept it** (`VER-11`,
[verification](../../../docs/verification.md#what-a-map-claims-about-itself)). A map laid to measure one
thing claims that thing and nothing else; every town claims what every town owes (`TownWatch`). **The
fixture map and the idle ring claim nothing of their own on purpose**: one is where the detailed checks are
staged rather than a map with a question, and the other measures nothing. A claim invented for either would
be one the suite already asks better somewhere else.
