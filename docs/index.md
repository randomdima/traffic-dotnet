# Documents — index

**Documents are sliced the way the code is.** Anything about one feature lives in that feature's own
`docs/`; only what belongs to no single slice is here. How work is done is [../CLAUDE.md](../CLAUDE.md);
what the project is made of and how to run it is [../readme.md](../readme.md).

## Cross-cutting

| Document | Holds |
|---|---|
| [goals.md](goals.md) | What the project is for, the quality bar, the two engineering rules, what it refuses to be |
| [priority.md](priority.md) | The rung every rule carries: `P0`/`P1` the owner's, `P2`–`P9` the assistant's, and what bending each costs |
| [requirements.md](requirements.md) | The rules that belong to no slice: `TEC`, `SIM`, `OBJ`, `AGT` |
| [verification.md](verification.md) | The four tiers, the five gates, the fixtures, `VER-1…12` |
| [slice-map.md](slice-map.md) | The slices, which way a dependency may point, how that is checked, and where the code does not comply |
| [decision-log.md](decision-log.md) | Why the cross-cutting rules read as they do |
| [tech-debt.md](tech-debt.md) | What is known wrong, slow, redundant or unmeasured in what is built, and what closes each |

## The slices

| Slice | Requirements | Decisions |
|---|---|---|
| [core/](../src/core/) — the kernel | [requirements](../src/core/docs/requirements.md) | [log](../src/core/docs/decision-log.md) |
| [citygen/](../src/citygen/) — the plan | [requirements](../src/citygen/docs/requirements.md) | [log](../src/citygen/docs/decision-log.md) |
| [world/terrain/](../src/world/terrain/) — the ground | [requirements](../src/world/terrain/docs/requirements.md) | [log](../src/world/terrain/docs/decision-log.md) |
| [world/road/](../src/world/road/) — streets, junctions, paint | [requirements](../src/world/road/docs/requirements.md) · [claims](../src/world/road/docs/claims.md) | [log](../src/world/road/docs/decision-log.md) |
| [world/foot/](../src/world/foot/) — the walking network | [requirements](../src/world/foot/docs/requirements.md) | [log](../src/world/foot/docs/decision-log.md) |
| [world/routing/](../src/world/routing/) — the two tiers | [requirements](../src/world/routing/docs/requirements.md) | [log](../src/world/routing/docs/decision-log.md) |
| [world/physics/](../src/world/physics/) — the wall | [requirements](../src/world/physics/docs/requirements.md) · [solver](../src/world/physics/docs/solver.md) | [log](../src/world/physics/docs/decision-log.md) |
| [world/containment/](../src/world/containment/) — being inside something | [requirements](../src/world/containment/docs/requirements.md) | [log](../src/world/containment/docs/decision-log.md) |
| [world/parking/](../src/world/parking/) — bays and lots | [requirements](../src/world/parking/docs/requirements.md) | [log](../src/world/parking/docs/decision-log.md) |
| [agents/car/](../src/agents/car/) — the driver | [requirements](../src/agents/car/docs/requirements.md) | [log](../src/agents/car/docs/decision-log.md) |
| [agents/ambulance/](../src/agents/ambulance/) — the rescue | [requirements](../src/agents/ambulance/docs/requirements.md) | [log](../src/agents/ambulance/docs/decision-log.md) |
| [agents/service/](../src/agents/service/) — the patrol and what a service vehicle is | [requirements](../src/agents/service/docs/requirements.md) | [log](../src/agents/service/docs/decision-log.md) |
| [agents/evacuator/](../src/agents/evacuator/) — the recovery | [requirements](../src/agents/evacuator/docs/requirements.md) | [log](../src/agents/evacuator/docs/decision-log.md) |
| [agents/person/](../src/agents/person/) — the walker | [requirements](../src/agents/person/docs/requirements.md) | [log](../src/agents/person/docs/decision-log.md) |
| [agents/trafficlight/](../src/agents/trafficlight/) — the signal | [requirements](../src/agents/trafficlight/docs/requirements.md) | [log](../src/agents/trafficlight/docs/decision-log.md) |
| [world/rail/](../src/world/rail/) — the track and the level crossing | [requirements](../src/world/rail/docs/requirements.md) | [log](../src/world/rail/docs/decision-log.md) |
| [agents/train/](../src/agents/train/) — the train | [requirements](../src/agents/train/docs/requirements.md) | [log](../src/agents/train/docs/decision-log.md) |
| [app/camera/](../src/app/camera/) | [requirements](../src/app/camera/docs/requirements.md) | [log](../src/app/camera/docs/decision-log.md) |
| [app/screen/](../src/app/screen/) — the chrome | [requirements](../src/app/screen/docs/requirements.md) | — |
| [app/render/](../src/app/render/) — the picture | [requirements](../src/app/render/docs/requirements.md) | [log](../src/app/render/docs/decision-log.md) |
| [app/hud/](../src/app/hud/) — the interface | [requirements](../src/app/hud/docs/requirements.md) | [log](../src/app/hud/docs/decision-log.md) |
| [app/debug/](../src/app/debug/) — the layers | [requirements](../src/app/debug/docs/requirements.md) | [log](../src/app/debug/docs/decision-log.md) |
| [app/shot/](../src/app/shot/) — the picture taken for review | [requirements](../src/app/shot/docs/requirements.md) | [log](../src/app/shot/docs/decision-log.md) |
| [app/playercontrol/](../src/app/playercontrol/) — the player's hands | [requirements](../src/app/playercontrol/docs/requirements.md) | [log](../src/app/playercontrol/docs/decision-log.md) |
| [app/drive/](../src/app/drive/) — those hands, held by a script | [requirements](../src/app/drive/docs/requirements.md) | [log](../src/app/drive/docs/decision-log.md) |
| [runtime/](../src/runtime/) — the machine | [requirements](../src/runtime/docs/requirements.md) | [log](../src/runtime/docs/decision-log.md) |
| [app/web/](../src/app/web/) — the town in a browser | [requirements](../src/app/web/docs/requirements.md) | [log](../src/app/web/docs/decision-log.md) |

**Slices with no document own no rule.** `world/statics/` is an implementation of rules
stated in [terrain](../src/world/terrain/docs/requirements.md), [routing](../src/world/routing/docs/requirements.md),
[agents/person](../src/agents/person/docs/requirements.md), [agents/ambulance](../src/agents/ambulance/docs/requirements.md),
[agents/service](../src/agents/service/docs/requirements.md) and [requirements.md](requirements.md#the-object-catalogue); `world/town/` is the composition seam;
`app/devicetick/` is where the slices' kernels meet the device ([slice-map.md](slice-map.md)); `app/main/` is the entry; `tests/`, `bench/` and `tools/` are the workshop, and what they must do is
[verification.md](verification.md).

## Where each requirement ID lives

**No ID is ever renumbered.** This table is how a code comment citing `PHY-7a` or `TER-3c.3` is resolved;
`qq req <ID>` does the same and prints the rule, its rung and everything citing it. A range may span a
retired number, which the owning slice's log records.

| IDs | Subject | Document |
|---|---|---|
| `TEC-1`, `TEC-2` | What no engine is taken for, and what the physics layer owes | [requirements.md](requirements.md#purpose-and-scope) |
| `SIM-1`, `SIM-2`, `SIM-6`, `SIM-7` | Hard vs soft, body state, ban vs price, one mechanism | [requirements.md](requirements.md#the-two-rule-classes) |
| `OBJ-2`, `OBJ-4…8` | The object catalogue, what a building is collided as, its roof's fans and chimneys, and the clouds' shadows | [requirements.md](requirements.md#the-object-catalogue) |
| `AGT-5`, `AGT-7` | The terminal state; every leg bounded | [requirements.md](requirements.md#agents) |
| `VER-1…12` | What must be demonstrated | [verification.md](verification.md) |
| `SIM-3`, `SIM-4`, `SIM-8`, `SIM-9`, `AGT-6` | Units, the two seeds, the one grid, real time at any display rate, where randomness comes from | [core](../src/core/docs/requirements.md) |
| `GEN-1…3`, `GEN-5…19`, `GEN-46…72`, `GEN-74…77` | A generated city's brief and its three stages — water, connectors, districts, a town as big as its roster, its blocks' own uses, roads by class, an intercity road's walkless verge, links between districts, bridges, lattices — a map's file and its zones, the maps, a city traced off its survey, laying a town, buildings and their uses, lane width, water and bridges, one-way streets, roundabouts, junctions as connection points and movements, no dangling lane, car parks laid off a street, a traced car park laid as a mall's, where a building stands, which are services and the district each serves | [citygen](../src/citygen/docs/requirements.md) |
| `TER-1…3d`, `TER-7…7b`, `PHY-8` | The ground, the pavement and its kerb, water and decks, and the stack of layers the mesh is | [world/terrain](../src/world/terrain/docs/requirements.md) |
| `TER-4`, `TER-4a`, `TER-4b`, `TER-4d`, `TER-5…5b`, `TER-5d`, `TER-5d.1`, `TER-5d.2`, `TER-5f`, `TER-5i`, `TER-5j`, `TER-5k`, `TER-6`, `TER-6a` | Roads, junctions, nodes that only carry lanes on, crossings, paint, the arrow a lane carries and the movements an unmarked arm makes | [world/road](../src/world/road/docs/requirements.md) |
| `TER-49`, `TER-4c…4c.8`, `TER-5c…5c.3`, `TER-5e`, `TER-5g`, `TER-5g.1` | Each agent claiming on its own turn, the ribbon atlas and its marks, a zebra held whole, where the bodies are, the ground a pass covers, where they mean to be, the ground a car backs up over, no movement over ground unclaimed, right of way and the ladder | [world/road/claims](../src/world/road/docs/claims.md) |
| `WLK-1`, `WLK-1a…3`, `WLK-8…16` | The pavement's lanes, the zebras and the junction a crossing is, the joints a walk carries on at, the walk kept off the road, and the node network held in code | [world/foot](../src/world/foot/docs/requirements.md) |
| `PHY-1…6`, `PHY-9` | Collision, damage energy, what a body is left in and what a wreck does to its driver | [world/physics](../src/world/physics/docs/requirements.md) |
| `SOL-1…22`, `SOL-35…38` | What this project's own solver must be: its step, a body at rest frozen and woken, and the bar between two bodies | [world/physics/solver](../src/world/physics/docs/solver.md) |
| `PHY-7`, `PHY-7a` | Containment and how a container is left | [world/containment](../src/world/containment/docs/requirements.md) |
| `GEN-4…4n` | A car park as a rank of bays joined to nothing, the manoeuvre into and out of one, the claim on one, turning round in one, the apron held for a service building's vehicles, and parking alongside a kerb in a gap rather than a slot | [world/parking](../src/world/parking/docs/requirements.md) |
| `CAR-1…15b`, `CAR-45…47`, `CAR-50…54`, `S-1…7`, `S-2a` | The driver and its leg, getting past what stands in its lane and backing up for the room to, moving across a lane, the car, its controls, its brakes, its tyres, its lamps and its smoke, and the standing rules every tick answers to | [agents/car](../src/agents/car/docs/requirements.md) |
| `PER-1`, `PER-3`, `PER-6…9`, `PER-11`, `PER-18`, `PER-23`, `PER-25…29` | The walker, its route, its body and its plan, the one rung it plans at, getting past somebody standing on its way, the trip and what a car does to it, and the car of their own a trip too far to walk is driven in | [agents/person](../src/agents/person/docs/requirements.md) |
| `AMB-1…10` | Hospitals, the apron of ambulances, the priority a call carries, the rescue and the standoff it stops at | [agents/ambulance](../src/agents/ambulance/docs/requirements.md) |
| `SRV-1…11` | Police stations and depots, what a service vehicle is and the crew it carries, what a wrecked one costs, the district beat half of every fleet drives, the station's parking a police car starts on, and the road it closes by standing at its entrances with those lanes out of every route | [agents/service](../src/agents/service/docs/requirements.md) |
| `EVA-1…8` | The wreck as a call, a depot's yard, the recovery, the tow's priority, the arm and the set-down | [agents/evacuator](../src/agents/evacuator/docs/requirements.md) |
| `TLT-1…4` | The signal agent and its cycle | [agents/trafficlight](../src/agents/trafficlight/docs/requirements.md) |
| `RAIL-1…11` | The track and the lanes it is, the railway's ground, its bridges and its loop, and the level crossing and its barrier that close ahead of a train | [world/rail](../src/world/rail/docs/requirements.md) |
| `TRN-3…15` | The train: what it is, its unbounded mass, one a line, its timetable, its bodies, how it is driven, its fans, its smoke and the showroom's rolling stock | [agents/train](../src/agents/train/docs/requirements.md) |
| `OBS-1…1c` | The camera | [app/camera](../src/app/camera/docs/requirements.md) |
| `OBS-2`, `OBS-2a`, `OBS-2e…2g`, `OBS-2i`, `OBS-2k…2n` | The status panel and its claims, the menu, the legend, Escape, the ruler, the unit read-out, the density a panel gives way at, the card a map is opened behind | [app/hud](../src/app/hud/docs/requirements.md) |
| `OBS-2b…2d`, `OBS-2h`, `OBS-2j`, `OBS-2o`, `OBS-2p`, `OBS-2r…2z`, `OBS-3` | The debug layers: the read-out, the turn circle, the ground's triangulation and layers, the driven ground and its outside, the pointer, the shell probe, the solver's grid, the sections, the zones and the engine drawn off its own tables | [app/debug](../src/app/debug/docs/requirements.md) |
| `CTL-1…9` | Selection, orders, a car's four of them, hand driving, a hand that names its car, the unit's own action, fingers | [app/playercontrol](../src/app/playercontrol/docs/requirements.md) |
| `SHT-1…6` | The frame taken with no window, its caption, the sheet and the document that asks for one | [app/shot](../src/app/shot/docs/requirements.md) |
| `DRV-1…8` | The script at the wheel: what it may hold, what it reads back, what it photographs, and the second seat | [app/drive](../src/app/drive/docs/requirements.md) |
| `WEB-1…10` | The browser head: what is halved, the crossing budget, the query string, what a page carries and weighs, what a publish holds, the opening card, what nothing waits for, and where a page steps its world | [app/web](../src/app/web/docs/requirements.md) |
## Known gaps

Absences that are gaps rather than decisions, and none of them is silent. Why each came about is the
owning slice's log; this list says only what is absent now and what closes it.

- **The world layer runs on the device only in a window.** [goals.md](goals.md) puts it on the device. A windowed
  desktop run and a page step it there by default ([decision-log.md](decision-log.md), WEB-10); `--managed-world`,
  `--shot`, `--sheet`, `--bench`, an unwatched `--drive`, a page whose device cannot hold the kernels, and every tier
  of the suite but `device` step the managed world. **No tier runs the page's device world**: `qq web` is the only
  look at it. And a page draws what moves off the CPU's sprite fill, since the emit's kernels bind more storage buffers
  than a page is given.
- **A lot's own arithmetic is not reworded**: `GEN-4b` and `GEN-4d` still describe kerbside lots laid along a
  chord, which the plan partly reads (`CarParks.BaysPerLotMost`). A car park is a rank of bays laid off a street
  that stays whole (`GEN-53`, `GEN-4h`), and the rules that size and space one are `GEN-53`'s and the
  locality's (`GEN-16`) — but a traced car park's, whose aisles are its own streets ranked end to end (`GEN-53a`).
- **A bay and a roadside are lanes nothing drives onto** (`GEN-50`): a bay is a road of its own joined to nothing,
  got into and out of by a car's own manoeuvre (`GEN-4f`) and never routed down (`GEN-4h`), and a traced road's
  roadside is lane zero of its street, joined to nothing and driven by nobody (`GEN-57`). That is what keeps them
  from mattering, and they are the two exemptions `GEN-50` is asked with.
- **One errand is worked on foot and the rest are not.** Every service vehicle carries a crew (`SRV-3`), but
  only a police car's officer ever gets out — to stand at a closure's mouth (`SRV-11`). The other crews stay in
  their seats: a casualty is taken aboard at the ambulance's standoff by a placement (`AMB-10`), and a wreck is
  hitched and set down from the truck (`EVA-5`, `EVA-6`).
- **A park-and-walk order is a park.** A car ordered to a place off the road parks nearest it (`CTL-8b`); an
  owner aboard gets out there and walks on to their own trip's door (`PER-29`), and nobody walks the rest of the
  way to the point the order named.
- **A car is neither brought back nor turned round.** A car whose rear axle is off drivable ground gives
  its leg up; on the road it takes the nearest lane only where that runs its way (`CAR-9`). There is no
  straight back onto the carriageway and no reversing out of a jam, and a car routed into a dead end stands
  there until its leg's clock gives the leg up — the route turns at one, and nothing turns the car
  (`GEN-4l`). **A car turns round only in a bay** (`GEN-4l`), so a lane is one a leg may come back from only
  where a car park stands near its end; everywhere else a leg comes back the other way by going round the
  block.
- **The ground does not answer whether a point is on a crossing** (`GroundShapes.Roads` reads the plan's
  empty `Crosswalks`), which a slice below the road cannot ask the road for. It closes as the zebras and the
  bars are handed out: one laying — `Crossings`, handed to the walk, the lanes that carry it, the bands beneath
  it and the lights that hold it — handed to whoever reads it. **No kerb is filleted** (`TER-5`), and **a
  node that forks nothing carries neither crossing nor bar** (`TER-6`, `TER-5b`), so the mid-block crossing
  an inline junction exists to carry is not laid.
- **No walker is ever held by a light.** A lit crossing's paint is held while it shows red (`TLT-1`), but no
  walk chooses a zebra (below), so the hold refuses nobody in a town; the unit tier asks it of the
  reservations alone.
- **A town stands no pedestrian node, so the walk is the courses and the crossings cut into them.** The
  pavement is two moves of the driven ground's boundary, each closed line one lane, parted wherever a zebra
  meets it and joined across the carriageway (`WLK-1`, `WLK-8`, `WLK-15`). The node network — `WLK-1a`,
  `WLK-2`, `WLK-3`, `WLK-9`, `WLK-11`, `WLK-12` and `FootMovements`' part of `WLK-13` — is held in code and
  asked its questions by the unit tier only. A zebra is painted off the kerb ends (`WLK-10`,
  `CityGen.KerbEnds`), which keep an end only where three or more roads meet, so a bend or a dead end has
  none and a block with no zebra is walked round and not left
  ([world/foot](../src/world/foot/docs/decision-log.md)).
- **And no walk chooses a zebra.** All of `Towns.City`'s are welded to the pavement at both ends and
  offered by the contracted graph, yet not one walked route over two minutes crosses one, and a walker
  ordered to the far end of a 7 m zebra it stands 3 m from walks 56 m round. It is not connectivity, and
  the search is a plain Dijkstra with a walker's turn priced at nothing; what is left is the shape of the
  contracted runs at a zebra's mouth, where the lane a body stands on runs past the mouth rather than into
  it. The exam map shows the same at its lattice's zebras: from a metre off the kerb some are walked over
  and some are walked round, a walker in the middle of the paint sent to the far pavement walks back to the
  near one and round, and only a walk from one edge of the paint to the other is the zebra every time —
  which is how the exam stages its pedestrians.
- **The fixture is generated, and it stands nobody.** It is laid off a brief kept in code (`Maps.FixtureBrief`,
  `GEN-60`) when it is opened, so the map every detailed check is staged on moves whenever any stage of the
  generator does, which is the one thing a fixture exists not to do
  ([verification.md](verification.md)). It asks for no buildings, so it carries no walker (`GEN-7`), no car park and
  no crossing — the shipped cities carry theirs (`--bench census`). It closes when the fixture can carry buildings
  without the car parks counted off them (`GEN-53`).
- **A map's roads are all fixed or all generated** (`GEN-58`, `GEN-60`): a traced map lays no street of a zone's, and
  a generated city lays every road of its own, so no map can fix its main roads and leave its side streets to its
  districts.
- **Nothing enters or leaves a generated city by its intercity roads** (`GEN-63`): a lane out past the map's edge
  leads to a world the town does not hold, and no car arrives from it or goes into it. The generator and the suite
  treat the edge as that world; the traffic does not.
- **A catchment's services are hoped for, not reserved** (`GEN-56`): the district chosen to stand one is chosen before
  its streets are laid, and they are laid without keeping a straight frontage for its yard, so a crooked district hands
  its catchment's on to its neighbours, a few at most, and past them the catchment goes without — which `--brief` and
  the census say.
- **A railway crosses a river at whatever angle its line meets it** (`GEN-70` is the roads'): the track is routed
  before the bridges are, as RAIL-8 raises it over the water.
- **One laboratory map ships, and it is the scenario map**: `Exam`, a lattice of traffic scenarios driven end to
  end ([verification](verification.md#the-scenario-map)). No map is laid to measure a track, a footway, a skid pad
  or the zebras alone.
- **A traced city is laid for the map and not yet for a car** (`GEN-57`): a corner is rounded as wide as keeps
  its survey within a tolerance, and one sharper than that as tight as its innermost lane is still a turn the config
  car steers round, rather than at a speed anything is driven at (`GEN-47`) or for the fleet's wider-steering
  variants, and a junction a few metres from the
  next has its disc shrunk to keep the road between them, which leaves its connectors no room to turn in.
  Its lanes are as many as OSM counts, or as many as the road's measured width holds where OSM only assumes a
  count.
- **A traced city's junctions are controlled by rule and not as signed** (`GEN-57`): its map holds no control, so
  its lights stand where main roads meet rather than where OSM maps them, and the engine has no stop or give-way
  sign and no priority road, so an unlit junction is first come, first served.
- **A traced city's turns are the town's own and not OSM's** (`GEN-57`, `TER-5j`): its map holds no lane arrow,
  turn restriction or lane connectivity, so a turn OSM forbids is made where the lanes would make it, and a lane is
  marked as an unmarked one is. The plan still carries marked and forbidden turns and named lanes, and nothing but a
  gathered junction fills them.
- **A traced city's buildings are its zones' and not its footprints** (`GEN-57`, `GEN-58`): each zone builds its
  measured frontage, front, skew, looks and the ground behind it, but a building's place is drawn and not surveyed,
  a building behind the frontage looks as the zone's front row does, and the prefabs are smaller than an old town's
  courtyard blocks — so most kinds of zone cover seven- to nine-tenths of the ground the place's buildings do, a
  residential or high-rise quarter a little more, and its squares of a hundred metres correlate with the place's at
  about 0.44 (`--bench zones`).
- **A traced city's bridges over its roads carry no walk** (`PHY-1a`, `TER-7b`). A bridge is a level of its own,
  drawn over the road under it with its cars over that, but the walk is struck off the ground's boundary and a
  walker is on the ground, so a walk reaching a bridgehead turns round the ground's end under the deck. A bridge
  over a bridge is one level with it, and a tunnel is not a level.
- **A traced city's lanes leave its map's inside its junctions** (`--bench fidelity`). Across a junction the map's
  lanes run straight to the node and the engine's connectors turn off its disc. And a corner too tight for its lanes
  is eased off the survey: a U-turn spur OSM draws as two one-way legs meeting at a point, 2 to 5 m apart, turns
  back 10 to 24 m before the tip, and a driveway turning hard just past a junction's disc is laid straight to the
  next leg.
- **A traced city keeps its streets' dead ends, and a car cannot turn round in one** (`GEN-57`, `GEN-4l`): a
  street of lanes each way that OSM ends nowhere ends so in the town — a lane both ways share never does, being
  run one way or taken out — so a leg routed into one stands there until its clock gives it up. A car going nowhere
  in particular is never toured down a lane every way off which only ends, and none is stood on one
  (`LaneLines.LanesDrivenOn`), but a route is not asked whether it can be driven round.
- **A traced city reaches the page on a desktop browser alone.** Opening OdesaOsm grows the page's heap to 2.8 GB
  of the 4 GB a 32-bit page can address, which a phone's browser does not give a page, and stands it up on the page's
  one thread in 84 s with the tab frozen meanwhile; `Highmoor` is not opened there at all.
- **A deck's pavement is not drawn.** The walk across a bridge is the driven ground's own boundary moved
  like anywhere else (`WLK-1`, `TER-3b.1`), and the ground answers walk there — but the ground stack is laid
  under the deck rather than across it, so the concrete is not drawn. What is owed is a boundary that knows
  where a deck carries the walk.
- **No car keeps a level crossing clear** (`RAIL-1`): a crossing holds its approach while a train closes it, but nothing
  stops a car pulling onto the track behind a queue that has no room past it, and one left standing there when the
  crossing closes is met by the train. `--bench trains` counts what the trains met.
- **A rail bridge is a level only to the claims and the solver**: a chord of a traced map's track on a bridge makes no
  crossing and a wagon on it meets nothing under it, but the railway is drawn at grade, under the road it passes over
  (`RAIL-3`), and a tunnel is not a level at all. `OdesaOsm`'s railway is one line: its other main lines meet it in
  yards, which no route is laid through (`qq osm --rails`).

**The verge is a decision rather than a gap**: it is one rectangle under the whole town, and whether it
must be cut to the complement of the paving is a question for the owner. It costs nothing under `TER-7b`
as it now stands, being the bottom layer of a stack.

Everything else that is unbuilt is reported by the instruments rather than listed here
([verification.md](verification.md#the-instruments-say-what-is-missing)).
