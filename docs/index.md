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
| [world/routing/](../src/world/routing/) — the two tiers | [requirements](../src/world/routing/docs/requirements.md) | [log](../src/world/routing/docs/decision-log.md) |
| [world/physics/](../src/world/physics/) — the wall | [requirements](../src/world/physics/docs/requirements.md) · [solver](../src/world/physics/docs/solver.md) | [log](../src/world/physics/docs/decision-log.md) |
| [world/containment/](../src/world/containment/) — being inside something | [requirements](../src/world/containment/docs/requirements.md) | — |
| [world/parking/](../src/world/parking/) — bays and lots | [requirements](../src/world/parking/docs/requirements.md) | [log](../src/world/parking/docs/decision-log.md) |
| [agents/car/](../src/agents/car/) — the driver | [requirements](../src/agents/car/docs/requirements.md) | [log](../src/agents/car/docs/decision-log.md) |
| [agents/car/maneuvers/](../src/agents/car/maneuvers/) — the driving catalogue | [the catalogue](../src/agents/car/maneuvers/docs/index.md) | [log](../src/agents/car/maneuvers/docs/decision-log.md) |
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
| [runtime/](../src/runtime/) — the machine | [requirements](../src/runtime/docs/requirements.md) | [log](../src/runtime/docs/decision-log.md) |
| [app/web/](../src/app/web/) — the town in a browser | [requirements](../src/app/web/docs/requirements.md) | [log](../src/app/web/docs/decision-log.md) |
| [app/android/](../src/app/android/) — the town in a hand | [requirements](../src/app/android/docs/requirements.md) | [log](../src/app/android/docs/decision-log.md) |

**Slices with no document own no rule.** `world/foot/` and `world/statics/` are implementations of rules
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
| `AGT-5`, `AGT-7` | The terminal state; the closed-catalogue rule | [requirements.md](requirements.md#agents) |
| `VER-1…12` | What must be demonstrated | [verification.md](verification.md) |
| `TER-1…3a`, `TER-3b…3c.6`, `TER-7`, `TER-7a`, `TER-7b`, `PHY-8` | The ground, the pavement, water and bridges, and the stack of layers the mesh drawing them is | [world/terrain](../src/world/terrain/docs/requirements.md) |
| `TER-4`, `TER-4a`, `TER-4b`, `TER-4d`, `TER-5`…`TER-5b`, `TER-5d`, `TER-5d.1`, `TER-5f`, `TER-6` | Roads, junctions, crossings, paint | [world/road](../src/world/road/docs/requirements.md) |
| `TER-4c`…`TER-4c.3`, `TER-5c`…`TER-5c.2`, `TER-5e`, `TER-5g` | What a movement takes off another, right of way, what a claim is and what is standing on a lane | [world/road/claims](../src/world/road/docs/claims.md) |
| `PHY-1…6`, `PHY-9` | Collision, damage energy, what a body is left in and what a wreck does to its driver | [world/physics](../src/world/physics/docs/requirements.md) |
| `SOL-1…22`, `SOL-35`, `SOL-36` | What this project's own solver must be | [world/physics/solver](../src/world/physics/docs/solver.md) |
| `PHY-7`, `PHY-7a`, `OBJ-4` | Containment and how a container is left | [world/containment](../src/world/containment/docs/requirements.md) |
| `GEN-4…4m` | Bays and lots, the ways at one, which way round a car stands in it, the claim on one, the apron held for a special building's own vehicles, the section's own nodes, and turning round in a bay | [world/parking](../src/world/parking/docs/requirements.md) |
| `GEN-1…3`, `GEN-5…19` | The plan, what laying a town owes, what a building declares it is for, what two of a kind standing on the same ground are, where two roads may touch, which of a grid's streets are driven one way, that no lane dangles, and what a roundabout is made of | [citygen](../src/citygen/docs/requirements.md) |
| `CAR-1…14` | The car agent, its controls, its tyres and its lamps | [agents/car](../src/agents/car/docs/requirements.md) |
| `PER-1…11`, `PER-13…18`, `PER-23` | The walker, the trip, what it follows, how it crosses, when it takes a car and what a car does to it | [agents/person](../src/agents/person/docs/requirements.md) |
| `AMB-1…10` | Hospitals, the roof one wears, the apron of ambulances at them, the priority a call carries, what a rescue is and the standoff its crew walks in from | [agents/ambulance](../src/agents/ambulance/docs/requirements.md) |
| `SRV-1…6` | Police stations and depots, the roofs a station and a repair shop wear, what a service vehicle is made of and how its crew works the street on foot, what a wrecked one costs its building, the beat a police car drives and the road its officer closes | [agents/service](../src/agents/service/docs/requirements.md) |
| `EVA-1…8` | The wreck as a call, a depot's yard, the recovery, the priority a tow carries only outbound, the arm and the two wheels under what it pulls | [agents/evacuator](../src/agents/evacuator/docs/requirements.md) |
| `TLT-1…4` | The signal agent and its cycle | [agents/trafficlight](../src/agents/trafficlight/docs/requirements.md) |
| `OBS-1`, `OBS-1a` | The camera | [app/camera](../src/app/camera/docs/requirements.md) |
| `OBS-2`, `OBS-2a`, `OBS-2e…2g`, `OBS-2i`, `OBS-2k…2n` | The status panel and its claims, the menu, the legend, the ruler, the unit read-out, the card a map is opened behind | [app/hud](../src/app/hud/docs/requirements.md) |
| `OBS-2b…2d`, `OBS-2h`, `OBS-2j`, `OBS-2o` | The debug layers, the read-out, the turn circle and the ground's own triangulation | [app/debug](../src/app/debug/docs/requirements.md) |
| `CTL-1…8d` | Selection, orders, a car's four of them, hand driving, the unit's own action | [app/playercontrol](../src/app/playercontrol/docs/requirements.md) |
| `SHT-1…6` | The frame taken with no window, its caption, the sheet and the document that asks for one | [app/shot](../src/app/shot/docs/requirements.md) |
| `P-*`, `E-*` | The driving manoeuvre catalogue — one page and one file per entry | [agents/car/maneuvers](../src/agents/car/maneuvers/docs/index.md) |
| `MAN-1…7`, `S-1…7`, `S-2a` | Chaining, arbitration, interruption, and the rules that run under every entry | [agents/car/maneuvers](../src/agents/car/maneuvers/docs/index.md#the-framework) |

## Known gaps

Two absences that are gaps rather than decisions, and neither is silent:

- **An end the picture takes for buried is an end it draws square where the answer draws it round.** The
  ground within a walk of a line that stops is the ground within a walk of its last cross-section, which is
  that segment swung round (`GroundShapes.OffTheBandM`). The drawing carries that offset only where a road
  really stops — at a node with no other arm — on the grounds that an end another arm leaves is inside what
  that arm draws or inside the wedge their kerbs turn on, and a movement's ends carry it nowhere at all.
  Where either turns out not to hold, the picture is short of the answer by up to a walk at the end of one
  line. [app/render](../src/app/render/docs/requirements.md).
- **No walking catalogue.** `AGT-7` asks for one per agent type and the walker has none
  — [agents/person](../src/agents/person/docs/requirements.md).

**The verge is a decision rather than a gap**: it is one rectangle under the whole town, and whether it
must be cut to the complement of the paving is a question for the owner. It costs nothing under `TER-7b`
as it now stands, being the bottom layer of a stack.

Everything else that is unbuilt is reported by the instruments rather than listed here
([verification.md](verification.md#the-instruments-say-what-is-missing)).
