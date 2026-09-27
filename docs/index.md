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
| [world/containment/](../src/world/containment/) — being inside something | [requirements](../src/world/containment/docs/requirements.md) | — |
| [world/parking/](../src/world/parking/) — bays and lots | [requirements](../src/world/parking/docs/requirements.md) | [log](../src/world/parking/docs/decision-log.md) |
| [agents/car/](../src/agents/car/) — the driver | [requirements](../src/agents/car/docs/requirements.md) | [log](../src/agents/car/docs/decision-log.md) |
| [agents/ambulance/](../src/agents/ambulance/) — the rescue | [requirements](../src/agents/ambulance/docs/requirements.md) | [log](../src/agents/ambulance/docs/decision-log.md) |
| [agents/service/](../src/agents/service/) — the patrol and what a service vehicle is | [requirements](../src/agents/service/docs/requirements.md) | [log](../src/agents/service/docs/decision-log.md) |
| [agents/evacuator/](../src/agents/evacuator/) — the recovery | [requirements](../src/agents/evacuator/docs/requirements.md) | [log](../src/agents/evacuator/docs/decision-log.md) |
| [agents/person/](../src/agents/person/) — the walker | [requirements](../src/agents/person/docs/requirements.md) | [log](../src/agents/person/docs/decision-log.md) |
| [agents/trafficlight/](../src/agents/trafficlight/) — the signal | [requirements](../src/agents/trafficlight/docs/requirements.md) | — |
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
| [app/android/](../src/app/android/) — the town in a hand | [requirements](../src/app/android/docs/requirements.md) | [log](../src/app/android/docs/decision-log.md) |

**Slices with no document own no rule.** `world/statics/` is an implementation of rules
stated in [terrain](../src/world/terrain/docs/requirements.md), [routing](../src/world/routing/docs/requirements.md),
[agents/person](../src/agents/person/docs/requirements.md), [agents/ambulance](../src/agents/ambulance/docs/requirements.md),
[agents/service](../src/agents/service/docs/requirements.md) and [requirements.md](requirements.md#the-object-catalogue); `world/town/` is the composition seam;
`app/main/` is the entry; `tests/`, `bench/` and `tools/` are the workshop, and what they must do is
[verification.md](verification.md).

## Where each requirement ID lives

**No ID is ever renumbered.** This table is how a code comment citing `PHY-7a` or `TER-3c.3` is resolved;
`qq req <ID>` does the same and prints the rule, its rung and everything citing it. A range may span a
retired number, which the owning slice's log records.

| IDs | Subject | Document |
|---|---|---|
| `TEC-1`, `TEC-2` | What no engine is taken for, and what the physics layer owes | [requirements.md](requirements.md#purpose-and-scope) |
| `SIM-1`, `SIM-2`, `SIM-6`, `SIM-7` | Hard vs soft, body state, ban vs price, one mechanism | [requirements.md](requirements.md#the-two-rule-classes) |
| `OBJ-2`, `OBJ-4…5a` | The object catalogue, and what a building is collided as | [requirements.md](requirements.md#the-object-catalogue) |
| `AGT-5`, `AGT-7` | The terminal state; every leg bounded | [requirements.md](requirements.md#agents) |
| `VER-1…12` | What must be demonstrated | [verification.md](verification.md) |
| `SIM-3`, `SIM-4`, `AGT-6` | Units, the two seeds, where randomness comes from | [core](../src/core/docs/requirements.md) |
| `GEN-1…3`, `GEN-5…19`, `GEN-46…55` | The brief and the maps, laying a town, buildings and their uses, lane width, water and bridges, one-way streets, roundabouts, junctions as connection points and movements, no dangling lane, car parks cut into a road, where a building stands and which are services | [citygen](../src/citygen/docs/requirements.md) |
| `TER-1…3d`, `TER-7…7b`, `PHY-8` | The ground, the pavement and its kerb, water and decks, and the stack of layers the mesh is | [world/terrain](../src/world/terrain/docs/requirements.md) |
| `TER-4`, `TER-4a`, `TER-4b`, `TER-4d`, `TER-5…5b`, `TER-5d`, `TER-5d.1`, `TER-5f`, `TER-5i`, `TER-6`, `TER-6a` | Roads, junctions, crossings, paint and the arrow a lane carries | [world/road](../src/world/road/docs/requirements.md) |
| `TER-4c…4c.5`, `TER-5c…5c.2`, `TER-5e`, `TER-5g`, `TER-5g.1` | The ribbon atlas and its marks, where the bodies are, where they mean to be, right of way and the ladder | [world/road/claims](../src/world/road/docs/claims.md) |
| `WLK-1`, `WLK-1a…3`, `WLK-8…15` | The pavement's lanes, the zebras and the junction a crossing is, the joints a walk carries on at, and the node network held in code | [world/foot](../src/world/foot/docs/requirements.md) |
| `PHY-1…6`, `PHY-9` | Collision, damage energy, what a body is left in and what a wreck does to its driver | [world/physics](../src/world/physics/docs/requirements.md) |
| `SOL-1…22`, `SOL-35`, `SOL-36` | What this project's own solver must be | [world/physics/solver](../src/world/physics/docs/solver.md) |
| `PHY-7`, `PHY-7a` | Containment and how a container is left | [world/containment](../src/world/containment/docs/requirements.md) |
| `GEN-4…4m` | A car park as a junction whose arms are bays, the ways at one, the claim on one, and the apron held for a service building's vehicles | [world/parking](../src/world/parking/docs/requirements.md) |
| `CAR-1…15b`, `CAR-45`, `S-1…7`, `S-2a` | The driver and its leg, the car, its controls, its tyres and its lamps, and the standing rules every tick answers to | [agents/car](../src/agents/car/docs/requirements.md) |
| `PER-1`, `PER-3`, `PER-6…9`, `PER-11`, `PER-18`, `PER-23`, `PER-25…27` | The walker, its route, its body and its plan, the crossing it plans to the far kerb, the trip and what a car does to it | [agents/person](../src/agents/person/docs/requirements.md) |
| `AMB-1…10` | Hospitals, the apron of ambulances, the priority a call carries, the rescue and the standoff it stops at | [agents/ambulance](../src/agents/ambulance/docs/requirements.md) |
| `SRV-1…6` | Police stations and depots, what a service vehicle is and that none carries a crew, what a wrecked one costs, the beat, and the road a police car closes by its own claim | [agents/service](../src/agents/service/docs/requirements.md) |
| `EVA-1…8` | The wreck as a call, a depot's yard, the recovery, the tow's priority, the arm and the set-down | [agents/evacuator](../src/agents/evacuator/docs/requirements.md) |
| `TLT-1…4` | The signal agent and its cycle | [agents/trafficlight](../src/agents/trafficlight/docs/requirements.md) |
| `OBS-1…1c` | The camera | [app/camera](../src/app/camera/docs/requirements.md) |
| `OBS-2`, `OBS-2a`, `OBS-2e…2g`, `OBS-2i`, `OBS-2k…2n` | The status panel and its claims, the menu, the legend, Escape, the ruler, the unit read-out, the density a panel gives way at, the card a map is opened behind | [app/hud](../src/app/hud/docs/requirements.md) |
| `OBS-2b…2d`, `OBS-2h`, `OBS-2j`, `OBS-2o`, `OBS-2p`, `OBS-2r…2x` | The debug layers: the read-out, the turn circle, the ground's triangulation and layers, the driven ground and its outside, the pointer, the shell probe and the solver's grid | [app/debug](../src/app/debug/docs/requirements.md) |
| `CTL-1…9` | Selection, orders, a car's four of them, hand driving, a hand that names its car, the unit's own action, fingers | [app/playercontrol](../src/app/playercontrol/docs/requirements.md) |
| `SHT-1…6` | The frame taken with no window, its caption, the sheet and the document that asks for one | [app/shot](../src/app/shot/docs/requirements.md) |
| `DRV-1…8` | The script at the wheel: what it may hold, what it reads back, what it photographs, and the second seat | [app/drive](../src/app/drive/docs/requirements.md) |
| `WEB-1…9` | The browser head: what is halved, the crossing budget, the query string, what a page carries and weighs, what a publish holds, the opening card, and what nothing waits for | [app/web](../src/app/web/docs/requirements.md) |
| `AND-1…8` | The handset head: what is halved, the bootstrap, where the files are unpacked, the intent's extras, the fingers, a lost surface, and the driver it asks for | [app/android](../src/app/android/docs/requirements.md) |

## Known gaps

Absences that are gaps rather than decisions, and none of them is silent. Why each came about is the
owning slice's log; this list says only what is absent now and what closes it.

- **No town lays a bay, so no service vehicle is ever stood.** A car park is a junction cut into a road
  that already stands, with its arms the bays (`GEN-52`, `GEN-53`, `GEN-4h`) — but the bays themselves are
  not laid, so an arm ends at a node nothing leaves (`GEN-50`). What follows from that one absence:
  - **No car makes a trip.** With no bay that can be reached, every town runs the empty map's tour
    (`TownWorld.DriveTheEmptyMap`): each car takes the lane it stands on and drives with no destination
    until its leg's clock stands it down, and is then put back on the road.
  - **Every apron is empty** (`GEN-4k`), so no ambulance, police car or evacuator stands in any town. The
    errands (`AMB-*`, `SRV-*`, `EVA-*`) are exercised by the unit tier alone, and `--bench rescue` and
    `--bench recovery` stage calls nothing can answer.
  - **`GEN-7`'s first half is false**: a car is stood on a lane, there being no bay to start it in. The
    second half holds — a person starts inside a building and walks out through its way in.
  - **A park order is refused**, and `CTL-8b`'s park-and-walk would be a park-there order anyway, there
    being no driver to hand the walk to.
  - **Nothing is left for `VER-2` to ask**, a bay being the one way round there is (`TER-5f`).
  - **A lot's own arithmetic is not reworded**: `GEN-4b`, `GEN-4c` and `GEN-4d` still describe kerbside
    lots, which the plan partly reads (`CarParks.BaysPerLotMost`), and what replaces them is laid with the
    bays rather than guessed at now.

  It closes when the bays land. The shipped cities cut car parks, so their geometry can be looked at
  (`qq town --parks Odesa`).
- **Nothing is done on foot but walking.** No leg of a trip is driven (`PER-11`) and no service vehicle
  carries a crew (`SRV-3`), so each errand that had a body working it covers the ground another way: a
  casualty is taken aboard at the ambulance's standoff by a placement (`AMB-10`), a wreck is hitched and
  set down from the truck (`EVA-5`, `EVA-6`), and a police car's closure is the car's own claim round the
  scene (`SRV-6`). **Nobody wears a service uniform** (`SRV-3a`), since nobody is named to.
- **Nothing gets past anything.** A driver is held behind whatever is in front of it until its leg's
  patience runs out and the leg is given up (`CAR-15a`); it does not cross the centreline to pass a wreck,
  a broken-down car or a body standing in its lane (`CAR-6.2b` is not reworded, and nothing exercises it).
  A walker queues behind what is in front of it on its way and waits at a kerb while the crossing is
  somebody else's (`PER-25`…`PER-27`): no signal, no patience, and no step round another walker. It closes
  when getting past something comes back as a movement the road offers rather than a shape an agent draws
  ([agents/car](../src/agents/car/docs/decision-log.md), [agents/person](../src/agents/person/docs/decision-log.md)).
- **A car is neither brought back nor turned round.** A car whose rear axle is off drivable ground gives
  its leg up; on the road it takes the nearest lane only where that runs its way (`CAR-9`). There is no
  straight back onto the carriageway and no reversing out of a jam, and a car routed into a dead end stands
  there until its leg's clock gives the leg up — the route turns at one, and nothing turns the car
  (`GEN-4l`).
- **Nothing is lit, and the plan paints no bar.** Whether a junction carries a timetable is not drawn any
  more, so `agents/trafficlight/` stands over a town with no lit junction (`TLT-3`), no signal head hangs
  anywhere, and `CAR-6.3` has no red to keep. `CityPlan.StopLines` is empty, so
  `LaneFurniture.StopBarAlongM` is infinity on every lane and nothing a signal governs has a line to be held
  at (`TLT-1`, `TER-6`); nor does the ground answer whether a point is on a crossing
  (`GroundShapes.Roads` reads the plan's empty `Crosswalks`), which a slice below the road cannot ask the
  road for. Both close the way the zebras did: one laying — `Crossings`, handed to the walk, the lanes that
  carry it and the bands beneath it — handed to whoever reads it. **No kerb is filleted** (`TER-5`), and **a
  node that forks nothing carries neither crossing nor bar** (`TER-6`, `TER-5b`), so the mid-block crossing
  an inline junction exists to carry is not laid.
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
- **The fixture is generated, and stands nobody.** `towns/Test.json` is a brief like a city's, so the map
  every detailed check is staged on moves when the generator does, which is the one thing a fixture exists
  not to do ([verification.md](verification.md)). It asks for no buildings, so it carries no walker
  (`GEN-7`), no car park and no crossing — the shipped cities carry theirs (`--bench census`). It closes
  when the fixture can carry buildings without the car parks counted off them (`GEN-53`).
- **One laboratory map ships, and it is the scenario map.** `Track` ×3, `Footway`, `Skidpad` and `Zebras`
  were laid against the lane layer that was replaced and went with it; `Exam` came back laid against the new
  one, as a lattice of traffic scenarios driven end to end ([verification](verification.md#the-scenario-map)).
  The ones still gone come back the same way.
- **A deck's pavement is not drawn.** The walk across a bridge is the driven ground's own boundary moved
  like anywhere else (`WLK-1`, `TER-3b.1`), and the ground answers walk there — but the ground stack is laid
  under the deck rather than across it, so the concrete is not drawn. What is owed is a boundary that knows
  where a deck carries the walk.

**The verge is a decision rather than a gap**: it is one rectangle under the whole town, and whether it
must be cut to the complement of the paving is a question for the owner. It costs nothing under `TER-7b`
as it now stands, being the bottom layer of a stack.

Everything else that is unbuilt is reported by the instruments rather than listed here
([verification.md](verification.md#the-instruments-say-what-is-missing)).
