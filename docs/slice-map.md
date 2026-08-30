# The slice map

**A feature is a folder, and everything about it lives in that folder** — its code, its `docs/`, its
requirements, its decisions, its tests, its art. This page names the slices and states which way a
dependency may point.

**All the code is under `src/`, and a slice is named here by its path within it** — `core/` is
`src/core/`. `assets/`, `towns/` and [docs/](index.md) sit beside `src/` at the root and are named in
full; `bin/` and `obj/` are then the only other folders there, which is the point of the arrangement.

## The tiers

Dependencies point **down this table and never up**, and **there are no cycles anywhere**. Two slices in
the same tier may depend on each other only in the one direction the tier's own row gives.

| Tier | Slices | May know about |
|---|---|---|
| **Kernel** | `core/` — config, geometry, persistence, simulation | Nothing else in the project. **Not a town** |
| **Plan** | `citygen/` — the plan, its cell vocabulary, its reader | core |
| **World** | `world/` — terrain, road, foot, routing, physics, containment, statics, parking | core, citygen, and each other in one direction |
| **Composition** | `world/town/` | Everything below it. **This is the seam, and it is the only thing allowed to be** |
| **Agents** | `agents/` — car, person, ambulance, service, evacuator, trafficlight | core, citygen, world |
| **Machine** | `runtime/` — the window, the device, the swapchain, and `runtime/web/` — the canvas and WebGPU | core. **Not the shell, not an agent, not a town** |
| **Chrome** | `app/screen/` — the quad, the glyphs, the theme, the text buffer | Nothing. It is the vocabulary a frame's overlay is written in |
| **Shell** | `app/` — main, camera, render, hud, debug, playercontrol, shot, web | Everything |
| **Workshop** | `tests/`, `bench/`, `tools/` | Everything. They may depend on what the runtime may not |

Inside `world/`, the settled direction is terrain ← road ← foot, both networks → routing, parking →
road, containment → physics. Inside `agents/`, it is `ambulance/` and `service/` → `world/statics/` and
nothing else, and `evacuator/` → nothing at all: each is a roster of buildings and what stands at them, and
the driving they ask for is the car's catalogue, reached from the composition seam like every other leg.
**An errand's slice never depends on `agents/car/`**, which is why the arithmetic of a tow is `TowBar` in
`agents/car/body/` beside the tyre model and not in the slice whose rules it serves: what happens to a car
on a hook is a fact about a car. Inside `app/`, it is screen ← render ← hud, screen ← render ← debug, and
hud → debug because the settings panel draws the switches the layers own and the selection's own path is
drawn in the layers' path vocabulary (`PathMarks`), so one route lands on the same stones at the same
weight whichever of them drew it. `app/shot/` sits under
`app/main/` and over everything it photographs, so a picture has one staging path and the entry point
only chooses it.

**A folder named `web/` is a second answer and never a second question.** `runtime/web/`,
`app/render/web/` and `app/main/web/` hold the browser's half of something the desktop already has, at
the same tier and with the same name, and the two project files pick which half is compiled
([app/web](../src/app/web/docs/requirements.md)). A `web/` folder therefore depends on exactly what the
slice it sits in may depend on, and `app/web/` itself — the page, the module that drives WebGPU, and the
boot — sits under `app/main/` on the same footing as `app/shot/`.

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

## Where a document goes

The same rule, applied to prose:

- **A rule about one feature is that feature's `docs/requirements.md`.** Only what belongs to no single
  slice is in [docs/](index.md).
- **Why it reads that way is the nearest `decision-log.md`** — the slice's own where the decision is the
  slice's, the root one where it is the project's.
- **How a type works is that type's XML docs**, and nowhere else.
- **A slice's `docs/` gets an `index.md` only once it holds more than two documents.** Below that the
  file names are the index.

## Where a number and an asset go

- **Every figure is on `SimConfig`**, authored in the nested groups and derived on the root
  ([core](../src/core/docs/requirements.md#where-a-figure-lives)). A literal in behaviour code is a defect.
- **`assets/` mirrors the code tree** — `assets/agents/car/variants/`, `assets/world/terrain/ground/` —
  so the art for a slice sits at the same path under `assets/` as the code that reads it.
- **`towns/` is input, not an asset**: exported plans, read at startup, belonging to no slice.

## The couplings that are deliberate

Named here so they are not mistaken for breaks:

- **`app/hud/` depends on `bench/`** — the start menu reads the probe list so that `OBS-2a` holds: the
  list the menu reads is the list the command line reads.
- **`tests/e2e/` depends on `app/shot/`** — the visual tier stages its scenarios through the game's own
  shot path and tiles them with the engine's own sheet, because a second staging path or a second
  tiler would be a picture of the test rather than of the game (`SHT-1`, `SHT-3`).
- **`world/parking/ → agents/car/`** — a bay is sized against the car that fits it. One way, and the
  reverse would be a car that knows what a car park is.
- **`world/town/` depends on everything below it.** It is the composition seam and the only slice that
  may be; a second one would mean there is no seam.

## The three seams that keep the tiers apart

Each of these exists because a lower slice needed something a higher one has, and **handing over the data
rather than the type is what keeps the arrow pointing down**:

- **`ExitSpots.Standing`** — the containment slice places a body without learning what an agent is. The
  caller hands over three spans (`PHY-7a`).
- **`TownRenderer.SheetFrameAspect(sheet, columns, rows)`** — the renderer knows how big an image is;
  what it is cut into is a fact about the thing it draws, so the grid comes from the caller.
- **`Grounds.Kinds`** — the plan's reader bounds a cell byte against the plan's own vocabulary, not
  against the terrain catalogue that interprets it.
