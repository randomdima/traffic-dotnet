# traffic-dotnet

A top-down traffic simulation of a small town — pedestrians, cars and traffic lights as agents — with
no game engine under it: C# on .NET 10, managed from the decision loop to the bytes the GPU reads, with
the unmanaged boundary at the graphics driver and the window and nowhere else.

Two rules decide everything else:

- **The frame's managed→native crossing count is O(1) in the size of the town.** A windowed frame is five
  crossings, an offscreen one three, on a town of twelve cars or five hundred
  ([runtime](src/runtime/docs/requirements.md#the-crossing-budget)).
- **The steady state allocates nothing.** The roster is laid once as structure-of-arrays, transient
  working sets come from a pool or the stack, and the hot path holds no LINQ, iterator, closure, boxing
  or interface call the JIT cannot devirtualise. The one exception is the solver's step, which is
  measured and printed.

The physics is not a package: `src/world/physics/` is this project's own broad phase, narrow phase and
contact solver. Box2D.NET is referenced by the unit suite alone, as the independent implementation the
cast and the manifolds are checked against over randomised poses.

## The documents

**Sliced the way the code is**: a rule about one feature lives in that feature's own `docs/`, and none of
them describes how a class works — that is the XML docs on the class.

| | |
|---|---|
| [docs/index.md](docs/index.md) | Every document, and the map from a requirement ID to the one that owns it |
| [docs/goals.md](docs/goals.md) | What the project is for, the quality bar, and what it refuses to be |
| [docs/slice-map.md](docs/slice-map.md) | The slices, which way a dependency may point, and where the code does not comply |
| [CLAUDE.md](CLAUDE.md) | How work is done here: where a finding is written, how a claim is checked |

## Building and running

Needs the .NET 10 SDK, a Vulkan 1.3 driver, and `glslc` (shaderc) on `PATH` — the project file compiles
`src/runtime/shaders/*` to SPIR-V and embeds them, so a missing `glslc` fails the build rather than the
run.

```
dotnet build
qq tests
dotnet run --project traffic-dotnet.csproj -- --map Odesa
```

`qq tests` runs the unit tier; `unit town`, `perf`, `all` and `e2e` name the others, what each is for is
[docs/verification.md](docs/verification.md), and what each costs is [CLAUDE.md](CLAUDE.md#verification).
A plain `dotnet test` runs every tier including the one an agent is paid to judge.

Without `--map` the game opens on its start menu and builds nothing until a map is picked. A windowed
run opens fullscreen on the display the pointer is on and `F11` toggles it; `--display NAME|N` names
that display instead, by the desktop's own name for it, and `--windowed` opens in a window, for a run
to be looked at beside something else. Other
entries: `--check` prints the dependency read-out, `--shot` takes a picture with no window at all,
`--ui` opens the panels and the debug layers, and `--bench <name>` runs one of the probes in `src/bench/`
(`census`, `drive`, `track`, `drunk`, `fleet`, `exam`, `skidpad`, `crossings`, `maneuvers`, `trips`, `rescue`,
`recovery`, `crash`, `soak`, `stuck`, `tick`, `town`, `solver`, `signals`, `walk`); `--bench all` runs the lot, and
the list itself is [`CheckCatalogue`](src/bench/CheckCatalogue.cs). The map list the menu reads is the map
list the command line reads; the probes are the command line's alone.

**A figure can be turned while the town runs.** The menu's `Figures` page (`--ui menu-figures`) carries a
track a figure — **each naming the raw term it moves and never what that term comes to**: the coefficient of
friction between rubber and tarmac, and the ground's own resistance to a wheel going round. **Only what the
whole town stands on is here.** A steering lock, a mass, a centre of gravity or an engine belongs to one car
and is stated in that car's own file, where nineteen bodies keep nineteen answers; a dial over them is one
figure pretending to speak for all of them. Each is a share of what the build ships, a decade either side, with shipped at
the middle of the track. **Dragging one changes it under the town that is standing, as the hand moves** —
every look is built again and the ground is worth what it is now worth, while the marks stay on the road
and every body stays where it was — which is what makes the skidpad a rig rather than a read-out. Nothing is authored by it: every trim is 100% unless the page has been opened, and the shipped
run is the run this suite measures.

**Every map says what it claims about itself and whether it is keeping it.** A windowed run on a scenario
map draws it as the last section of the status panel — a broken claim counted on the panel's own always-on
title, the rows behind it opened by `--ui scenario` or by clicking down to them; a place map has nothing to
claim and shows none of it —
and every headless run prints the same table: a row a claim, the figures behind each verdict, and a last
line a script can read. **A broken claim is a failed run**, so `--bench exam`, `--bench crossings` and
`--map Track --seconds 300` all exit non-zero when the town breaks something it claims. What is quoted
beside the claims — the drunks' swerves, the laps a fleet got round — fails nothing: it is a fact about
that town rather than a bound
([verification](docs/verification.md#what-a-map-claims-about-itself)).

`--sheet FILE.json` is the same picture asked for as a document: several staged frames, each captioned
with the map, the framing, the moment and the seed, tiled into one sheet for review
([app/shot](src/app/shot/docs/requirements.md)). `--caption` puts that band and those notes on a single
`--shot`, and every captioned picture writes its figures beside it as `<picture>.png.json`.

```json
{
  "out": ".tmp/junctions.png", "map": "Test", "size": [640, 480], "view": 45, "seconds": 20,
  "note": "the paint must stop at the give-way line",
  "cells": [
    { "label": "crossroad", "at": [120, 90] },
    { "label": "tee",       "at": [200, 90] },
    { "label": "bend",      "at": [120, 160], "ui": ["nodes"] },
    { "label": "zebra",     "at": [200, 160], "view": 30 }
  ]
}
```

`--place-services` decides which of each shipped map's buildings are its hospitals, its police stations and
its depots, and writes it into the map ([citygen](src/citygen/docs/requirements.md)): the buildings with
somewhere for their vehicles to stand, laid out as far from one another as the town allows. It is a
**workshop step and never a build one** — run it when a map arrives or when the shares those services are
placed at change, and commit the towns it rewrites.

`--lamps` cuts the town's lamp sheet out of the fleet's own sprites — every lens a variant draws, in
each colour it can burn (CAR-14a) — and writes it to `assets/agents/car/variants/common/lamp_atlas.png`.
It is a **workshop step and never a build one**: run it when a variant's art or its lens rectangles
change, and commit the picture. The line it prints per lens is the instrument for the one thing the
arithmetic cannot answer — a rectangle over bodywork nobody painted a lamp on cuts the paint around it
and comes back undistinguished.

**Six maps are laid to measure one thing**, and each claims that one thing and nothing else. **What each
is and what it claims is [citygen](src/citygen/docs/requirements.md#the-maps)**; what follows is only which
command reads which.

| Map | Is | Read by |
|---|---|---|
| `Track` | one closed lap of five shapes, with fifteen people pacing beside the carriageway | `--bench track`, `--ui track` |
| `Drunk` | the same lap with those fifteen reeling **in** it, which is the only place anything overtakes (`E-4`) | `--bench drunk` |
| `Fleet` | the same lap again with one car of every look on it and nobody on foot | `--bench fleet` |
| `Exam` | a six by six lattice of junctions, one staged crossing manoeuvre in each | `--bench exam`, `--map Exam` |
| `Skidpad` | a grid of plain road, a square a car: every look on full left lock, a row per pedal and gear — three pedals each way — each drawing its own circle beside the one its axles ask for | `--bench skidpad`, `--ui turn-circles`, `--ui menu-figures` |
| `Zebras` | five isolated streets with a crossing apiece, one of them laid off square | `--bench crossings` |

`--lay-maps` writes the first five, whose every shape is chosen against the car's own figures — so moving
one of those figures is all five to lay again, always together, since a lap is only comparable with a lap
laid from the same arithmetic. `Zebras` arrives as a file like every city does.

The exam and the crossings map are asserted card by card and crossing by crossing in the town tier off the
probe's own run ([JunctionExamTests](src/tests/world/JunctionExamTests.cs)), so the instrument and the gate
cannot disagree about what a crossing is.

## The same town, in a browser

There is a second head. `traffic-dotnet.web.csproj` compiles the same `src/` against WebGPU and a canvas
instead of Vulkan and a window, and the town it draws is the same code drawing it — no `#if` anywhere in
the shared half, and the machine's two halves named file by file in
[app/web](src/app/web/docs/requirements.md).

```
dotnet workload install wasm-tools wasm-experimental
dotnet publish traffic-dotnet.web.csproj -c Release
cd bin/web/Release/net10.0/publish/wwwroot && python3 -m http.server 8080
```

Then `http://localhost:8080/?map=Test` — **the query string is the command line**, so `?map=Odesa&ui=nodes`
is `--map Odesa --ui nodes`. Without a map it opens on the start menu, exactly as the desktop does.

**It wants a browser with WebGPU** — Chrome or Edge 137+ on Linux, Safari 26, a Firefox where it has
shipped — and says so under the canvas when it has not got one. **Build it Release**: `RunAOTCompilation`
is on there and off in Debug, and the interpreter is about ten times off a 60 Hz loop.

**`qq web` publishes it, `qq web --serve` serves it, and `qq web --shot FILE` takes its picture** —
the browser head's answer to `--shot`, which drives a real browser because headless Chromium runs all
of this except presenting a WebGPU canvas ([decision log](src/app/web/docs/decision-log.md)). **Add
`--debug` for the page in ten seconds instead of ten minutes**: the same tree, the same load path, and
an interpreted town whose frame rate means nothing.

**A frame crosses the wall three times** whatever the town holds — the animation callback in, the input
out, the frame — against the desktop's five, and the counter that says so is
[`WebGpu.Crossings`](src/runtime/web/WebGpu.cs). Rule 1 is the same rule.

**The page carries the visual layers and none of the instruments.** `--shot`, `--sheet`, `--bench`,
`--lamps` and `--place-services` are how a run is measured and they stay on the desktop, which has a file
system and a process that can exit.

**The page downloads the art before the first frame and a map when that map is picked.** A frame cannot
wait on a fetch, so the menu's click writes a name down and the boot's own loop — the one place a
browser run may wait — fetches the plan and stands the town up
([decision log](src/app/web/docs/decision-log.md)).

## Layout

```
src/        every line of C#, and nothing else — the nine slices below
  core/     the kernel: config, geometry, persistence, simulation — and nothing that knows about a town
  citygen/  the city plan as pure data: its structure, its cell vocabulary, its .town reader
  world/    terrain, road, foot, routing, physics, containment, statics, parking, town
  agents/   car, person, ambulance, service, evacuator, trafficlight — body / control, and the maneuvers:
            one file per entry of the closed catalogue (src/agents/car/maneuvers/docs/index.md)
  runtime/  the machine: the window, raw Vulkan, the swapchain, the shaders — and web/, the browser's half
  app/      screen, render, camera, hud, debug, playercontrol, shot, web, main — the shell
  bench/    the census and the probes
  tests/    the unit suite, laid out folder for folder as the tree it tests
  tools/    workshop tools, which may depend on what the runtime may not
assets/     the art and the .json data read at startup, mirroring the code tree
towns/      exported .town files — the simulation's input
bin/, obj/  build output — the only folders at the root the project file writes
```

**The order above is the dependency order**: everything points down it and nothing points up, with
`src/world/town/` as the one composition seam. [docs/slice-map.md](docs/slice-map.md) is the whole rule.

The project file stays at the root and the code sits under `src/`, so what is written by a build and
what is written by a person never share a folder.

`assets/` and `towns/` are found by walking up from wherever the binary landed
(`src/core/config/ProjectPaths.cs`), so a run from an IDE, from `dotnet run` and from
`bin/Debug/net10.0/` all resolve the same.

## Where a figure lives

Every number the simulation runs on is on `SimConfig`, and its shape says which kind it is: the nested
groups — `config.Car.LengthM`, `config.Tyre.Friction`, `config.Signals.CycleS` — are **authored**, and
they are the only figures `assets/shared/config/SimConfig.json` may override. Everything on the root is
**derived** from them (`SimConfig.Derived.cs`), which is why moving one authored ratio moves the whole
town and why the override file refuses a derived key.
