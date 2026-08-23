# Documents — index

**Documents are sliced the way the code is.** Anything about one feature lives in that feature's own
`docs/`; only what belongs to no single slice is here. How work is done is [../CLAUDE.md](../CLAUDE.md);
what the project is made of and how to run it is [../readme.md](../readme.md).

## Cross-cutting

| Document | Holds |
|---|---|
| [goals.md](goals.md) | What the project is for, the quality bar, the two engineering rules, what it refuses to be |
| [requirements.md](requirements.md) | The rules that belong to no slice: `PUR`, `TEC`, `SIM`, `OBJ`, `AGT` |
| [verification.md](verification.md) | The four tiers, the four gates, the fixtures, `VER-1…10` |
| [slice-map.md](slice-map.md) | The slices, which way a dependency may point, and how that is checked |
| [decision-log.md](decision-log.md) | Why the cross-cutting rules read as they do |

## The slices

| Slice | Requirements | Decisions |
|---|---|---|
| [core/](../src/core/) — the kernel | [requirements](../src/core/docs/requirements.md) | [log](../src/core/docs/decision-log.md) |
| [citygen/](../src/citygen/) — the plan | [requirements](../src/citygen/docs/requirements.md) | [log](../src/citygen/docs/decision-log.md) |
| [world/terrain/](../src/world/terrain/) — the ground | [requirements](../src/world/terrain/docs/requirements.md) | — |
| [world/road/](../src/world/road/) — streets, junctions, paint | [requirements](../src/world/road/docs/requirements.md) | [log](../src/world/road/docs/decision-log.md) |
| [world/routing/](../src/world/routing/) — the two tiers | [requirements](../src/world/routing/docs/requirements.md) | — |
| [world/physics/](../src/world/physics/) — the wall | [requirements](../src/world/physics/docs/requirements.md) · [solver](../src/world/physics/docs/solver.md) | [log](../src/world/physics/docs/decision-log.md) |
| [world/containment/](../src/world/containment/) — being inside something | [requirements](../src/world/containment/docs/requirements.md) | — |
| [world/parking/](../src/world/parking/) — bays and lots | [requirements](../src/world/parking/docs/requirements.md) | — |
| [agents/car/](../src/agents/car/) — the driver | [requirements](../src/agents/car/docs/requirements.md) | [log](../src/agents/car/docs/decision-log.md) |
| [agents/car/maneuvers/](../src/agents/car/maneuvers/) — the driving catalogue | [the catalogue](../src/agents/car/maneuvers/docs/index.md) | [log](../src/agents/car/maneuvers/docs/decision-log.md) |
| [agents/person/](../src/agents/person/) — the walker | [requirements](../src/agents/person/docs/requirements.md) | — |
| [agents/trafficlight/](../src/agents/trafficlight/) — the signal | [requirements](../src/agents/trafficlight/docs/requirements.md) | — |
| [app/camera/](../src/app/camera/) | [requirements](../src/app/camera/docs/requirements.md) | — |
| [app/screen/](../src/app/screen/) — the chrome | [requirements](../src/app/screen/docs/requirements.md) | — |
| [app/render/](../src/app/render/) — the picture | [requirements](../src/app/render/docs/requirements.md) | — |
| [app/hud/](../src/app/hud/) — the interface | [requirements](../src/app/hud/docs/requirements.md) | — |
| [app/debug/](../src/app/debug/) — the layers | [requirements](../src/app/debug/docs/requirements.md) | — |
| [app/shot/](../src/app/shot/) — the picture taken for review | [requirements](../src/app/shot/docs/requirements.md) | [log](../src/app/shot/docs/decision-log.md) |
| [app/playercontrol/](../src/app/playercontrol/) — the player's hands | [requirements](../src/app/playercontrol/docs/requirements.md) | — |
| [runtime/](../src/runtime/) — the machine | [requirements](../src/runtime/docs/requirements.md) | [log](../src/runtime/docs/decision-log.md) |

**Slices with no document own no rule.** `world/foot/` and `world/statics/` are implementations of rules
stated in [terrain](../src/world/terrain/docs/requirements.md), [routing](../src/world/routing/docs/requirements.md),
[agents/person](../src/agents/person/docs/requirements.md) and [requirements.md](requirements.md#the-object-catalogue); `world/town/` is the composition seam;
`app/main/` is the entry; `tests/`, `bench/` and `tools/` are the workshop, and what they must do is
[verification.md](verification.md).

## Where each requirement ID lives

**No ID is ever renumbered.** This table is how a code comment citing `PHY-7a` or `TER-3c.3` is resolved.

| IDs | Subject | Document |
|---|---|---|
| `PUR-1…4`, `TEC-1…3`, `SIM-5` | Purpose, non-goals, technology | [requirements.md](requirements.md#purpose-and-scope) |
| `SIM-1`, `SIM-2`, `SIM-6`, `SIM-7` | Hard vs soft, body state, ban vs price, one mechanism | [requirements.md](requirements.md#the-two-rule-classes) |
| `SIM-3`, `SIM-4`, `AGT-6` | Units, the two seeds, where randomness comes from | [core](../src/core/docs/requirements.md) |
| `OBJ-1…5` | The object catalogue | [requirements.md](requirements.md#the-object-catalogue) |
| `AGT-1…5`, `AGT-7` | What an agent is; the closed-catalogue rule | [requirements.md](requirements.md#agents) |
| `VER-1…10` | What must be demonstrated | [verification.md](verification.md) |
| `TER-1…3a`, `TER-3b…3c.4`, `TER-7`, `PHY-8` | The ground, the pavement, water and bridges | [world/terrain](../src/world/terrain/docs/requirements.md) |
| `TER-4…6` | Roads, junctions, what a movement takes off another, crossings, paint | [world/road](../src/world/road/docs/requirements.md) |
| `PHY-1…6`, `PHY-9` | Collision, damage energy, terminal states | [world/physics](../src/world/physics/docs/requirements.md) |
| `SOL-1…36` | What this project's own solver must be | [world/physics/solver](../src/world/physics/docs/solver.md) |
| `PHY-7`, `PHY-7a`, `OBJ-4` | Containment and how a container is left | [world/containment](../src/world/containment/docs/requirements.md) |
| `GEN-4…4d` | Bays and lots | [world/parking](../src/world/parking/docs/requirements.md) |
| `GEN-1…3`, `GEN-5…8` | The plan, and what laying a town owes | [citygen](../src/citygen/docs/requirements.md) |
| `CAR-1…9a` | The car agent and its tyres | [agents/car](../src/agents/car/docs/requirements.md) |
| `PER-1…16` | The walker, the trip, what it follows, and how it crosses | [agents/person](../src/agents/person/docs/requirements.md) |
| `TLT-1…4` | The signal agent and its cycle | [agents/trafficlight](../src/agents/trafficlight/docs/requirements.md) |
| `OBS-1`, `OBS-1a` | The camera | [app/camera](../src/app/camera/docs/requirements.md) |
| `OBS-2`, `OBS-2a`, `OBS-2e…2g` | Start panel, settings, the legend, the ruler | [app/hud](../src/app/hud/docs/requirements.md) |
| `OBS-2b…2d`, `OBS-2h` | The debug layers and the read-out | [app/debug](../src/app/debug/docs/requirements.md) |
| `CTL-1…6` | Selection, orders, hand driving | [app/playercontrol](../src/app/playercontrol/docs/requirements.md) |
| `SHT-1…6` | The frame taken with no window, its caption, the sheet and the document that asks for one | [app/shot](../src/app/shot/docs/requirements.md) |
| `P-*`, `E-*` | The driving manoeuvre catalogue — one page and one file per entry | [agents/car/maneuvers](../src/agents/car/maneuvers/docs/index.md) |
| `MAN-1…7`, `S-1…7`, `S-2a` | Chaining, arbitration, interruption, and the rules that run under every entry | [agents/car/maneuvers](../src/agents/car/maneuvers/docs/index.md#the-framework) |

## Known gaps

Two absences that are gaps rather than decisions, and neither is silent:

- **No generator.** Towns arrive as `.town` files, so `GEN-2`, `GEN-3` and `GEN-5…8` bind whatever
  exported them — [citygen](../src/citygen/docs/decision-log.md).
- **No walking catalogue.** `AGT-7` asks for one per agent type and the walker has none
  — [agents/person](../src/agents/person/docs/requirements.md).

Everything else that is unbuilt is reported by the instruments rather than listed here
([verification.md](verification.md#the-instruments-say-what-is-missing)).
