# traffic-dotnet

A top-down traffic simulation of a small town — pedestrians, cars and traffic lights as agents — with
no game engine under it: C# on .NET 10, managed from the decision loop to the bytes the GPU reads, with
the unmanaged boundary at the graphics driver and the window and nowhere else.

Two rules decide everything else:

- **The frame's managed→native crossing count is O(1) in the size of the town.** One command buffer per
  swapchain image, recorded once; draw counts live in a buffer the CPU writes rather than in the calls.
  A windowed frame is five crossings, an offscreen one three, on a town of twelve cars or five hundred.
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

`qq tests` runs the unit tier, which is two seconds; `unit town`, `perf`, `all` and `e2e` name the
others, and what each is for is [docs/verification.md](docs/verification.md). A plain `dotnet test`
runs every tier including the one an agent is paid to judge, and is four minutes.

Without `--map` the game opens on its start menu and builds nothing until a map is picked. Other
entries: `--check` prints the dependency read-out, `--shot` takes a picture with no window at all,
`--ui` opens the panels and the debug layers, and `--bench <name>` runs one of the probes in `src/bench/`
(`census`, `drive`, `track`, `drunk`, `maneuvers`, `trips`, `crash`, `soak`, `tick`, `town`, `solver`,
`signals`, `walk`). The list the menu reads is the list the command line reads.

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

`--lay-track` writes `towns/Track.town` and `towns/Drunk.town`, the only maps this build lays itself
([citygen](src/citygen/docs/requirements.md#where-a-town-comes-from)): a proving ground of **one closed lap
cut into ten roads** — five shapes with a link between each pair. The shapes are a straight, a half turn, a
snake, a long arc and a quarter turn. There is no light and no paint on it. Six cars drive it, two of each
drivetrain and identical in everything else, and the two maps are the same lap with a different fifteen
people on it:

- **`Track`** stands them **beside** the road — one at the end of each shape, the rest spread along the lap
  — and each paces into the lane and back, so a car brakes to rest for a body in front of it and pulls away
  again. `--bench track` prints what each shape costs each drivetrain: the speed it allows, the ground it
  takes to slow down to it, the run back up to speed. `--ui track` shows the same figures on screen while
  it happens.
- **`Drunk`** stands them **in** the road, and each reels down its own lane and stands where it stopped
  every few lurches — so a driver follows something slow, and then overtakes it (`E-4`), which is the only
  place in this town anything ever does. `--bench drunk` prints the same table for that lap, which is what
  it is read against, plus what getting round it cost: the swerves, the back-offs and the laps given up on.

The shapes are chosen against the car's own figures, so a change to those is a track to lay again — both of
them, since the two are only comparable while they are the same road.

## Layout

```
src/        every line of C#, and nothing else — the nine slices below
  core/     the kernel: config, geometry, persistence, simulation — and nothing that knows about a town
  citygen/  the city plan as pure data: its structure, its cell vocabulary, its .town reader
  world/    terrain, road, foot, routing, physics, containment, statics, parking, town
  agents/   car, person, trafficlight — body / control, and the car's maneuvers: one file per entry
            of the closed catalogue (src/agents/car/maneuvers/docs/index.md)
  runtime/  the machine: the window, raw Vulkan, the swapchain, the shaders
  app/      screen, render, camera, hud, debug, playercontrol, shot, main — the shell
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
groups — `config.Car.LengthM`, `config.Tyre.GripMps2`, `config.Signals.CycleS` — are **authored**, and
they are the only figures `assets/shared/config/SimConfig.json` may override. Everything on the root is
**derived** from them (`SimConfig.Derived.cs`), which is why moving one authored ratio moves the whole
town and why the override file refuses a derived key.
