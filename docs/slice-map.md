# The slice map

**A feature is a folder, and everything about it lives in that folder** — its code, its `docs/`, its
requirements, its decisions, its tests, its art. This page names the slices and states which way a
dependency may point.

**All the code is under `src/`, and a slice is named here by its path within it** — `core/` is
`src/core/`. `assets/`, `towns/`, `raw_assets/` and [docs/](index.md) sit beside `src/` at the root and
are named in full.

## The tiers

The table runs from the bottom tier up: **a slice may know about the rows before its own and never the
rows after it**, and **there are no cycles anywhere**. Two slices in the same tier may depend on each
other only in the one direction the tier's own row gives.

| Tier | Slices | May know about |
|---|---|---|
| **Kernel** | `core/` — config, geometry, simulation | Nothing else in the project. **Not a town** |
| **Plan** | `citygen/` — the plan, its ground vocabulary, the lines a car is driven on, and the generator that lays it from a brief | core |
| **World** | `world/` — terrain, road, foot, routing, physics, containment, statics, parking | core, citygen, and each other in one direction |
| **Agents** | `agents/` — car, person, ambulance, service, evacuator, trafficlight | core, citygen, world |
| **Composition** | `world/town/` | Everything before it. **This is the seam, and it is the only thing allowed to be** |
| **Machine** | `runtime/` — the window, the device, the swapchain, `runtime/web/` — the canvas and WebGPU, and `runtime/android/` — the glass and the surface made of it | core. **Not the shell, not an agent, not a town** |
| **Chrome** | `app/screen/` — the quad, the glyphs, the theme, the text buffer | Nothing. It is the vocabulary a frame's overlay is written in |
| **Shell** | `app/` — main, camera, render, hud, debug, playercontrol, shot, drive, web, android | Everything |
| **Workshop** | `tests/`, `bench/`, `tools/` | Everything. They may depend on what the runtime may not |

Inside `world/`, the settled direction is terrain ← road ← foot, both networks → routing, parking →
road, containment → physics and terrain. **The ground is split across the Plan tier and the World tier on purpose**:
what shape is at a point is the plan's (`citygen/GroundShapes`), so a town half-laid can be asked where a
thing may stand, and what that kind of ground *permits* is `world/terrain/`'s, because a permission is a
rule about agents and the plan does not know what an agent is (TER-2a). **The lines a car is driven on are
split the same way and for the same reason**: the lanes and the connectors between them are the plan's
(`citygen/LaneLines`), because they are what the tarmac is drawn from, and the rules laid over them — where
they meet, what each movement takes off the others — are `world/road/`'s. Inside `agents/`, it is `ambulance/` and `service/` → `world/statics/` and
nothing else, and `evacuator/` → nothing at all: each is a roster of buildings and what stands at them, and
the driving they ask for is the car's own leg, reached from the composition seam like every other one.
**An errand's slice never depends on `agents/car/`**, which is why the arithmetic of a tow is `TowBar` in
`agents/car/body/` beside the tyre model and not in the slice whose rules it serves: what happens to a car
on a hook is a fact about a car. **What a car or a walker does is `actions/` in its slice**, a folder per
action (CAR-15b, PER-25b) over the slice's `body/` and `control/`: an action reads the world through its slice's
ground (`DrivingGround`, `WalkingGround`) and never the composition, and what only the town has — a car's line, the
standing rules, the tyres — it asks for through `ICarTown`, which the seam implements. Inside `app/`, it is screen ← render ← hud, screen ← render ← debug, and
hud → debug because the settings panel draws the switches the layers own and the selection's own path is
drawn in the layers' path vocabulary (`PathMarks`), so one route lands on the same stones at the same
weight whichever of them drew it. `app/shot/` sits under
`app/main/` and over everything it photographs, so a picture has one staging path and the entry point
only chooses it. `app/drive/` sits beside it on the same footing and over `app/playercontrol/`,
`app/hud/` and `app/shot/`: a script at the wheel holds the hand that slice owns, prints the rows that one
draws and asks this one for its pictures.

**A folder named for a head is a second answer and never a second question.** `runtime/web/`,
`app/render/web/` and `app/main/web/` hold the browser's half of something the desktop already has, and
`runtime/android/` and `app/main/android/` hold the handset's, at the same tier and with the same name;
the three project files pick which half is compiled ([app/web](../src/app/web/docs/requirements.md),
[app/android](../src/app/android/docs/requirements.md)). Such a folder therefore depends on exactly what
the slice it sits in may depend on, and `app/web/` and `app/android/` themselves — the page and the
module that drives WebGPU, the activity and the glass it hands over — sit under `app/main/` on the same
footing as `app/shot/`.

**`tests/` mirrors the tree it tests**, folder for folder: a test for `app/screen/` is
`tests/screen/`, and both sit under `src/`.

## How this is checked

There is no tool for it. The audit is one pass over the `using TrafficSimulation.*` lines plus the
fully-qualified names, folded to slice level; a break shows up as an edge pointing up the table or as a
pair pointing both ways. Run it when a slice gains a dependency, not on a schedule.

**Two smells that are the same break wearing different clothes**, and both were found this way:

- **A fully-qualified name instead of a `using`.** `App.Screen.GlyphSheet.Resource` written out in full
  reads as a small convenience and hides an edge from every grep that looks at import lines.
- **A stale `using` nobody removed.** It costs nothing at run time and makes a slice look coupled to
  something it stopped needing, which is how a false break survives a real audit.

Where a document, a number and an asset go is [CLAUDE.md](../CLAUDE.md#everything-is-a-vertical-slice).

## The couplings that are deliberate

Named here so they are not mistaken for breaks:

- **`app/hud/` depends on `bench/`** — the status panel and the unit panel draw what a map claims about
  itself, and a claim, its watch and its reading are the bench's (`ScenarioWatch`, `Scenarios.For`). A
  panel that worked a claim out for itself would be a second answer to what the probe already asks.

## Where the code does not comply

- **`world/road/` and `world/routing/` point at each other.** `DrivingNetwork` contracts the road through
  `World.Routing`, and `RunNetwork` takes `World.Road.LanePlaces` back — a cycle the World row forbids.
  It closes when `LanePlaces` moves into `world/routing/`, which every network already points at.

## The three seams that keep the tiers apart

Each of these exists because a lower slice needed something a higher one has, and **handing over the data
rather than the type is what keeps the arrow pointing down**:

- **`ExitSpots.Standing`** — the containment slice places a body without learning what an agent is. The
  caller hands over three spans (`PHY-7a`).
- **`TownRenderer.SheetFrameAspect(sheet, columns, rows)`** — the renderer knows how big an image is;
  what it is cut into is a fact about the thing it draws, so the grid comes from the caller.
- **`BuildingCatalog.Roofs`** — a building is sized by the roof it will wear (`GEN-54`), and the roofs are
  read off the sheets by a slice above the plan. What crosses is the measurements (`CityGen.BuildingSizes`),
  handed to `Maps.Plan` by whoever opens a map, so the generator never learns that a catalogue exists.
