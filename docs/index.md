# Documents — index

**Documents are sliced the way the code is.** Anything about one feature lives in that feature's own
`docs/`; only what belongs to no single slice is here. How work is done is [../CLAUDE.md](../CLAUDE.md);
what the project is made of and how to run it is [../readme.md](../readme.md).

## Cross-cutting

| Document | Holds |
|---|---|
| [goals.md](goals.md) | What the project is for, the quality bar, the two engineering rules, what it refuses to be |
| [priority.md](priority.md) | The rung every rule carries: `P0`/`P1` the owner's, `P2`–`P9` the assistant's, and what bending each costs |
| [requirements.md](requirements.md) | The rules that belong to no slice: `PUR`, `TEC`, `SIM`, `OBJ`, `AGT` |
| [verification.md](verification.md) | The four tiers, the four gates, the fixtures, `VER-1…11` |
| [slice-map.md](slice-map.md) | The slices, which way a dependency may point, and how that is checked |
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
| [app/debug/](../src/app/debug/) — the layers | [requirements](../src/app/debug/docs/requirements.md) | [decisions](../src/app/debug/docs/decision-log.md) |
| [app/shot/](../src/app/shot/) — the picture taken for review | [requirements](../src/app/shot/docs/requirements.md) | [log](../src/app/shot/docs/decision-log.md) |
| [app/playercontrol/](../src/app/playercontrol/) — the player's hands | [requirements](../src/app/playercontrol/docs/requirements.md) | — |
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

**No ID is ever renumbered.** This table is how a code comment citing `PHY-7a` or `TER-3c.3` is resolved.
**Every rule also carries a rung** saying whose it is ([priority.md](priority.md)); `qq req <ID>` prints it
beside the statement and `qq req --rungs` lists the owner's own band.

| IDs | Subject | Document |
|---|---|---|
| `TEC-1`, `TEC-2` | What no engine is taken for, and what the physics layer owes | [requirements.md](requirements.md#purpose-and-scope) |
| `SIM-1`, `SIM-2`, `SIM-6`, `SIM-7` | Hard vs soft, body state, ban vs price, one mechanism | [requirements.md](requirements.md#the-two-rule-classes) |
| `SIM-3`, `SIM-4`, `AGT-6` | Units, the two seeds, where randomness comes from | [core](../src/core/docs/requirements.md) |
| `WEB-1…9` | The browser head: what is halved, the crossing budget, what a page does not carry, what it weighs, what a publish must hold, and what nothing waits for | [app/web](../src/app/web/docs/requirements.md) |
| `AND-1…8` | The handset head: what is halved, how little the bootstrap is, where the town's files are unpacked, what it does not carry, the intent's extras, the fingers, a lost surface, and the driver it asks for | [app/android](../src/app/android/docs/requirements.md) |
| `OBJ-2`, `OBJ-4…5a` | The object catalogue, and what a building is collided as | [requirements.md](requirements.md#the-object-catalogue) |
| `AGT-5`, `AGT-7` | The terminal state; what an agent does, which is a leg | [requirements.md](requirements.md#agents) |
| `VER-1…12` | What must be demonstrated | [verification.md](verification.md) |
| `TER-1…3a`, `TER-3b…3c.6`, `TER-7`, `TER-7a`, `TER-7b`, `PHY-8` | The ground, the pavement, water and bridges, and the stack of layers the mesh drawing them is | [world/terrain](../src/world/terrain/docs/requirements.md) |
| `TER-4`, `TER-4a`, `TER-4b`, `TER-4d`, `TER-5`…`TER-5b`, `TER-5d`, `TER-5d.1`, `TER-5f`, `TER-5i`, `TER-6`, `TER-6a` | Roads, junctions, crossings, paint, the arrow a lane carries, and the road a lane is one way of | [world/road](../src/world/road/docs/requirements.md) |
| `WLK-1`, `WLK-1a…3`, `WLK-8…13` | The pavement as the driven ground's boundary moved off itself once per walking lane, and — held in code and laid by no town — the pedestrian nodes placed off the road ends, what merges them into a junction, the points each of them hands its three ways over at, what welds two of those points into one, the lines laid between them, the turns a place is walked through on, and what a stretch is walked at | [world/foot](../src/world/foot/docs/requirements.md) |
| `TER-4c`…`TER-4c.3`, `TER-5c`…`TER-5c.2`, `TER-5e`, `TER-5g` | What a movement takes off another, right of way, what a claim is and what is standing on a lane | [world/road/claims](../src/world/road/docs/claims.md) |
| `PHY-1…6`, `PHY-9` | Collision, damage energy, what a body is left in and what a wreck does to its driver | [world/physics](../src/world/physics/docs/requirements.md) |
| `SOL-1…22`, `SOL-35`, `SOL-36` | What this project's own solver must be | [world/physics/solver](../src/world/physics/docs/solver.md) |
| `PHY-7`, `PHY-7a`, `OBJ-4` | Containment and how a container is left | [world/containment](../src/world/containment/docs/requirements.md) |
| `GEN-4…4m` | Bays and lots, the ways at one, which way round a car stands in it, the claim on one, the apron held for a special building's own vehicles, the section's own nodes, and turning round in a bay | [world/parking](../src/world/parking/docs/requirements.md) |
| `GEN-1…3`, `GEN-5…19`, `GEN-51…55` | The plan, what laying a town owes, what a building declares it is for, what two of a kind standing on the same ground are, where two roads may touch, which of a grid's streets are driven one way, that no lane dangles, what a roundabout is made of, that a junction is a place roads meet, how one is cut into a road that already stands, what a car park is, where a building stands and which of them are the services | [citygen](../src/citygen/docs/requirements.md) |
| `CAR-1…15b` | The driver and its leg, the car agent, its controls, its tyres and its lamps | [agents/car](../src/agents/car/docs/requirements.md) |
| `PER-1`, `PER-3`, `PER-6…9`, `PER-11`, `PER-18`, `PER-23`, `PER-25`, `PER-26` | The walker, the line it follows, the two claims it lays, the trip and what a car does to it | [agents/person](../src/agents/person/docs/requirements.md) |
| `AMB-1…10` | Hospitals, the roof one wears, the apron of ambulances at them, the priority a call carries, what a rescue is and the standoff its crew walks in from | [agents/ambulance](../src/agents/ambulance/docs/requirements.md) |
| `SRV-1…6` | Police stations and depots, the roofs a station and a repair shop wear, what a service vehicle is made of and how its crew works the street on foot, what a wrecked one costs its building, the beat a police car drives and the road its officer closes | [agents/service](../src/agents/service/docs/requirements.md) |
| `EVA-1…8` | The wreck as a call, a depot's yard, the recovery, the priority a tow carries only outbound, the arm and the two wheels under what it pulls | [agents/evacuator](../src/agents/evacuator/docs/requirements.md) |
| `TLT-1…4` | The signal agent and its cycle | [agents/trafficlight](../src/agents/trafficlight/docs/requirements.md) |
| `OBS-1`, `OBS-1a` | The camera | [app/camera](../src/app/camera/docs/requirements.md) |
| `OBS-2`, `OBS-2a`, `OBS-2e…2g`, `OBS-2i`, `OBS-2k…2n` | The status panel and its claims, the menu, the legend, the ruler, the unit read-out, the card a map is opened behind | [app/hud](../src/app/hud/docs/requirements.md) |
| `OBS-2b…2d`, `OBS-2h`, `OBS-2j`, `OBS-2o…2v` | The debug layers, the read-out, the turn circle, the ground's own triangulation and the layers it is laid in, the driven ground and its outside | [app/debug](../src/app/debug/docs/requirements.md) |
| `CTL-1…9` | Selection, orders, a car's four of them, hand driving, a hand that names its car, the unit's own action, fingers | [app/playercontrol](../src/app/playercontrol/docs/requirements.md) |
| `SHT-1…6` | The frame taken with no window, its caption, the sheet and the document that asks for one | [app/shot](../src/app/shot/docs/requirements.md) |
| `DRV-1…8` | The script at the wheel: what it may hold, what it reads back, what it photographs, what a claim is worth while it drives, and the second seat a driver who is not the reader sits in | [app/drive](../src/app/drive/docs/requirements.md) |
| `S-1…7`, `S-2a` | The standing rules every tick of a car answers to: the line, the profile, the looking, the grant and the movements | [agents/car](../src/agents/car/docs/requirements.md#the-standing-rules) |

## Known gaps

Absences that are gaps rather than decisions, and none of them is silent:

- **The walking is deliberately simple, and three things it used to do are gone.** A walker holds a route
  as the ways it is travelled and lays two claims (`PER-25`, `PER-26`); what was put down with the old layer
  was every rule about getting past somebody — the grant along the pavement, the step round a body, the
  wait at a kerb with its signal and its patience, and the two rules for a walker on a map with nothing on
  it. **Two walkers wanting one piece of pavement now meet in the solver rather than in the claims.** The
  slice's [decision-log.md](../src/agents/person/docs/decision-log.md) is why. **The driving was cut to
  the same shape and for the same reason** ([agents/car](../src/agents/car/docs/decision-log.md)), so what
  is absent here is absent from both: nothing in this town gets past anything.
- **Nothing in this town is done on foot but walking.** No leg of a trip is driven (`PER-25`) and no
  service vehicle carries a crew (`SRV-3`), so four errands lost the body that used to work them and each
  covers the ground another way, named where it happens rather than hidden:
  - **A casualty is got aboard at the ambulance's standoff by a placement** rather than fetched and tugged
    (`AMB-10`) — the winch's own fallback (`EVA-5`) said of a person.
  - **A wreck is hitched and set down from the truck** (`EVA-5`, `EVA-6`), which is what the winch already
    covered the last few metres of.
  - **A police car's closure is the car's own claim** round the scene rather than an officer's beside it
    (`SRV-6`). The rule is reworded to that; what it gives up is a body standing there to look at.
  - **And `CTL-8b`'s park-and-walk order is a park-there order**, there being no driver to hand the walk
    to.
  **Nobody wears a service uniform** (`SRV-3a`), since nobody is named to.
- **A town stands no pedestrian node, so the walk is the courses and the crossings cut into them.** The
  pavement is laid and it is walkable, and the zebras a town paints are walked over: two moves of the driven
  ground's boundary, each closed line one lane, parted wherever a crossing meets them and joined across the
  carriageway (`WLK-1`, `WLK-8`, `WLK-15`, [world/foot](../src/world/foot/docs/requirements.md)) — so a walk
  does now leave the block it is on. **What no town stands is the node network** (`WLK-1a`, `WLK-2`,
  `WLK-3`, `WLK-9`, `WLK-12`, held in code and asked its questions by the unit tier): where a walk is cut
  and crossed is read off the kerb ends instead (`CityGen.KerbEnds`), and what those rules place — the walk
  down a road, the walk round a junction, and the corner a merge makes of two nodes — no town has. **Every
  place a town's walk chooses at is a crossing's junction** (`WLK-15`), and a pavement's two lanes are joined
  only across a carriageway, so a block with no zebra on it is walked round and not left.
  Why the node network was put down is the slice's
  [decision-log.md](../src/world/foot/docs/decision-log.md).
- **And no walk actually uses one.** The zebras are laid and they are joined: all 488 in `Towns.City` are
  welded to the pavement at both ends, carry a link of their own, and the contracted graph offers 1102 turns
  onto them and 1102 off. **Yet not one line laid carries a crossing point** — 0 of 46 over two minutes —
  and a walker ordered to the far end of a 7 m zebra it is standing 3 m from walks 56 m round instead of
  crossing. **It is not connectivity and it is not the claims**: a body on the paint is a body on a lane and
  nothing more (`PER-26`), and the search is a plain Dijkstra with a walker's turn priced at nothing. What
  is left is the shape of the contracted runs at a zebra's mouth — the lane a body stands on runs past the
  mouth rather than into it, so reaching it costs a run the crossing never repays. Why a walker could not
  turn round at all until 2026-09-20, and what fixing that was worth, is
  [world/foot](../src/world/foot/docs/decision-log.md).
- **The fixture map stands nobody, because it has no buildings to stand them at.** A walker begins inside
  a building (`GEN-7`) and `towns/Test.json` asks for none, so every detailed check is staged on a town
  with no walkers on it — the shipped cities carry theirs (`--bench census`). It closes when the fixture
  can carry buildings without carrying the car parks counted off them (`GEN-53`).
- **The lane layer was rebuilt and the town it carried was put down with it.** The lines a car is driven on
  are laid from the junction out now — a bearing and a standoff drawn for every arm, the movements laid
  between the points that produces, and the road drawn as the link is offered to arrive on them (`GEN-46`,
  `GEN-47`, `GEN-48`, `GEN-10`).
  **What stood beside the old layer was not ported across the rework**, and every one of these comes back
  off the boundary the driven lines lay (`TER-7b`) or off the layers struck from it:
  - **A car park is a junction now, and its bays are not laid.** The placement is back on a new footing
    (`GEN-52`, `GEN-53`): a car park is a junction cut into a road that already stands, with one arm a bay,
    and the road it is cut into does not move. `GEN-4h` is reworded to that, being what it directly
    contradicted. **What is not laid is the bays themselves** — no parking space, no standing, no frontage
    arithmetic — so **an arm ends at a node nothing leaves**: a lane a car is driven onto
    and not off, which is the one thing `GEN-50` is about. It closes when the bays land, and until then **no
    town the suite asks its ordinary questions of carries a car park**; the shipped city does, so its
    geometry can be looked at (`qq town --parks Odesa`, `--at` a car park). **`GEN-4b` and `GEN-4d` are not
    reworded either**: a lot as a rectangle along a kerb and a lot's clearance from a junction are the bays'
    own arithmetic, and what replaces them is the next thing laid rather than something to guess at now.
  - **The buildings are back, and they are back off that boundary** (`GEN-54`, `GEN-55`). A building stands
    against the pavement's outer face with its wall on the walk's own kerb and its door on the concrete
    behind it, so nothing about where one goes knows that a road, a junction or a car park exists; and every
    hospital, police station and depot the roster asks for is stood at the end of a car park cut for it.
    **What is still empty is the apron** (`GEN-4k`): a station has a yard and the yard has no bays, so what
    holds a bay for an ambulance finds none — which closes with the entry above this one and not with
    another placement.
  - **No kerb fillet** (`TER-5`), and **nothing is lit** (`TLT-3`): whether a junction carries a timetable
    was drawn in the road stage and is not drawn any more, so `agents/trafficlight/` stands over a town with
    no lit junction. **The crossing and the bar came back as paint alone**, hung off the end of the arm
    rather than off the ground a junction reaches ([world/road](../src/world/road/docs/decision-log.md)):
    the plan's own `Crosswalks` and `StopLines` are still empty, so no ground answers crosswalk where a
    zebra is, no signal head hangs off a bar and nothing holds at one. **A walker is owed a way over one
    now and a driver still cannot see it**: the walking network lays a pair of lanes over every zebra the
    town paints and over no other (`WLK-15`, `CrossingWays`, all 510 of Odesa's), but what projects a
    crossing onto the lanes under it reads the plan's empty array (`LaneFurniture`, `CrossingBands`) — so a
    body on the paint holds no road, and the traffic it is in front of is not told. **It is what PER-26's
    second claim waits on**: a walker states the band it is stepping onto at p9 with the paint's own right
    of way, and with no band to state it states nothing — what holds the traffic off a body on a zebra
    today is the ordinary ground under it (TER-4c.2) and never the paint. It closes when those two read the
    bands the paint is laid from (`World.Road.Crossings`).
    **And a node that forks nothing carries neither** (`TER-6`, `TER-5b`): the mid-block crossing an inline
    junction exists to carry is not laid, which is the one placement rule of `TER-6` nothing answers.
  - **The roster rule GEN-7 is still false of a generated town in one half, and is not reworded.** It
    says a car starts stopped in a parking space and a person starts inside a building. **The second half
    holds now** — a brief asks for people and each is stood at a way in and walks through it before the
    first tick — and the first does not, a car being stood on a lane because there is no bay to stand one
    in. The code is what is temporarily wrong here.
  - **The suite got smaller with the laboratories.** `Track` ×3, `Exam`, `Footway`, `Skidpad` and `Zebras`
    were laid against the layer that has been replaced, so they were deleted rather than carried across it —
    and with them the exam's staged junctions, the lap's drivetrain figures, the pad's circles and the
    crossings' walks. The ones that come back will be laid against the new layer.
  - **And the fixture is a generated town.** `towns/Test.json` is a brief like a city's, so the map every
    detailed check is staged on moves when the generator does — which is the one thing a fixture exists not
    to do ([verification.md](verification.md)). It leaves the suite with two towns that differ only in size
    and seed, and it is why nothing is carried as a file any more: with no town on disk, the `.town` format
    and the reader, writer and byte cursor under it were deleted rather than kept for a map nobody ships.
  - **What it closed, it closed loudly**: the merge leaves no ring of any town open now. Odesa left one run
    of 30 390 m with its two ends 0.122 m apart and the generated city one of 485 m; both close on lines
    that are fewer, longer and smoother, and what is left of the old entry is the figure it used to carry.
  - **And a ring now closes onto itself and not merely to within a weld.** The walk hands every ring back
    with its pieces made to meet (`ArcRings.Tightened`), which a frame did show before it did: over Odesa
    the boundary's joints past a rounding fell 1 826 → 5 and the holes a line drawn down the ring shows
    779 → 0. `--bench outset` reports the joints of both lines and **does not gate on them**.

- **The ground beside a road is answered as one distance and drawn as two rings, and a corner is where they
  part.** The answer is the layer `TER-7b` states — the ground within one figure of the driven bands, tarmac
  at nought and concrete at a walk (`GroundShapes.At`) — while the picture fills the boundary *rounded* at
  `Road.LineRoundedM` and offset by the same figure (`GroundRings`). Along a street the two are the same
  region; **at a corner they stand up to that radius apart**, the rounding being a cut into the shape the
  distance measures square. `TER-7` is deviated from by the picture's own rounding and not by a second
  construction, which is as small as this gets without the fill following a line a kerbstone cannot bend to.
  - **What closed with it is the pavement.** A point on the concrete was drawn as concrete and answered as
    grass; it is answered as the walk now, and **the lanes the walking network holds a walker on run over it
    for the whole of both towns** (`EveryPavementLaneRunsOverGroundAWalkerPrefers`) — which is what `TER-3c.3`
    says the two owe each other, checked rather than reasoned about.
  - **And the ground answer still lacks the wedge a junction's corner is paved back over** (`TER-3c.7`,
    `TER-5`). It is ground inside the boundary that no driven line claims, which only a boundary can say: the
    answer reads
    the bands, so a junction's corner answers as the walk beside it where it stands within a walk of a
    movement and as grass where the box is wide enough that it does not. It closes when the answer can be
    asked which side of a ring a point is on for what a wheel costs.
  - **What no longer waits on it is the scatter**, which asks the boundary where the paving is and places
    off the walk's outer face rather than beside a road (`GEN-6b`, `GroundRings.PavedWithin`). A prop's
    girth is cleared against the ground answer as well now (`GEN-6a`), so the concrete a prop may not stand
    on is held twice: by the distance it was laid at, and by what the ground says about the point.
- **A deck's pavement is not drawn.** The walk across a bridge is the driven ground's own boundary moved
  like anywhere else (`WLK-1`, `TER-3b.1`) — but the ground stack is laid under the deck rather than across it,
  so the concrete a walk would run on is not drawn and the deck answers as deck. What is owed is a boundary
  that knows where a deck carries the walk.
- **The paint is back except for the drift mark** — the dash, the bay stroke, the zebra and the bar — laid
  off the plan rather than carried in it, and `RoadFigures.PaintDrawn` is gone with the switch that hid it.
  `RoadFigures.CarriagewayDrawn` is gone too: it hid the road's own surface while the boundary was the thing
  being looked at, and the road wears its own surface again.
- **Nothing gets past anything.** A driver is held behind whatever is in front of it for as long as its
  patience lasts and then gives the leg up (`CAR-15a`); it does not cross the centreline to pass a wreck, a
  broken-down car or a body standing in its lane, and no walker steps round another. The overtake the
  driving used to carry was **entered nought times on either shipped city** and its own log records 352 of
  353 shapes refused by the terrain, so what went with it was a mechanism that had never run — but the
  absence is real and it is what a jammed street now waits out.
  The rule that permits crossing the centreline to pass a stationary obstacle is **not reworded**, and
  nothing exercises it. It closes when getting past something comes back as a movement the road offers
  rather than a shape a driver draws.
- **A car that is off the road and pointing the wrong way is not recovered.** It takes the lane under it
  where one runs its way (`CAR-9`) and stands until its leg is given up otherwise; there is no straight
  back onto the carriageway and no reversing out of a jam, both of which the driving used to carry. Over
  five minutes of Odesa the two together fired 16 back-offs and 20 ground recoveries, and what replaces
  them is 31 lines taken again and a leg ended.

**The verge is a decision rather than a gap**: it is one rectangle under the whole town, and whether it
must be cut to the complement of the paving is a question for the owner. It costs nothing under `TER-7b`
as it now stands, being the bottom layer of a stack.

**The skidpad carrying a slab is a decision rather than a gap.** That map is tarmac edge to edge, which is a
rectangle and not a road network, and a boundary said in the lines a car is driven on has nothing to say
about a shape no line is the edge of. It says what it is.

Everything else that is unbuilt is reported by the instruments rather than listed here
([verification.md](verification.md#the-instruments-say-what-is-missing)).
