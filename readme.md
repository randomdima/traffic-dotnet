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

Without `--map` the game opens on its start menu with the idle ring running behind it, and builds no city
until a map is picked — the same start in Debug and in Release, and on either head. A windowed
run opens fullscreen on the display the pointer is on and `F11` toggles it; `--display NAME|N` names
that display instead, by the desktop's own name for it, and `--windowed` opens in a window, for a run
to be looked at beside something else. Other
entries: `--check` prints the dependency read-out, `--shot` takes a picture with no window at all,
`--ui` opens the panels and the debug layers, and `--bench <name>` runs one of the probes in `src/bench/`
(`census`, `load`, `shape`, `joints`, `parks`, `shapes`, `drive`, `maneuvers`, `trips`, `rescue`, `recovery`,
`crash`, `soak`, `stuck`, `tick`, `town`, `solver`, `walk`); `--bench all` runs the lot, and
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
and every body stays where it was — which is what makes the page a rig rather than a read-out. Nothing is authored by it: every trim is 100% unless the page has been opened, and the shipped
run is the run this suite measures.

**The last two rows of that page are not trims**: they are the shell probe's own figures (`OBS-2w`) — the
town's driven boundary moved out by nought to twenty metres, and how round the corners turning in on it come
back, as a share of that distance. It is drawn in violet under its own switch (`--ui shell`, or `--ui
shell-6` and `--ui shell-6-0.5` for six metres at half rounding, with no hand on the slider). They are the
figures there that change what is *drawn* rather than what the town does, and what they are for is the
outset itself: where the move swallows a corner, where two kerbs come back as one line, and which notch is
the distance's and which the rounding's.

**And the ground can be taken apart while the town runs.** The menu's `Ground` page (`--ui menu-ground`)
carries a row a layer of the town's standing ground — the grass, the walk and its kerb, the water, the
decks, the carriageway, the slabs, the town's kerb and the paint — each saying what it came to in triangles
and in the milliseconds it took to cut, with the whole mesh and the boundary's share of that time under
them. **A box unticked takes that layer out of the picture**, and out of the wireframe over it, so what is
under it can be looked at; the triangles are cut once when the town stands and the switch only shortens the
draw, so no figure moves and putting a layer back costs nothing. A picture wants it by name:
`--ui hide-carriageway`, and `grass`, `walk`, `walk-kerb`, `water`, `decks`, `slabs`, `kerb` and `paint`
for the rest. The same table is printed headless by `--bench census`.

**Every map says what it claims about itself and whether it is keeping it.** A windowed run draws it as the
last section of the status panel — a broken claim counted on the panel's own always-on title, the rows
behind it opened by `--ui scenario` or by clicking down to them —
and every headless run prints the same table: a row a claim, the figures behind each verdict, and a last
line a script can read. **A broken claim is a failed run**, so `--bench soak` and
`--map Odesa --seconds 300` both exit non-zero when the town breaks something it claims. What is quoted
beside the claims fails nothing: it is a fact about that town rather than a bound
([verification](docs/verification.md#what-a-map-claims-about-itself)).

`--sheet FILE.json` is the same picture asked for as a document: several staged frames, each captioned
with the map, the framing, the moment and the seed, tiled into one sheet for review
([app/shot](src/app/shot/docs/requirements.md)). `--caption` puts that band and those notes on a single
`--shot`, and every captioned picture writes its figures beside it as `<picture>.png.json`.

**A car can be driven by hand with nobody at the keyboard.** `--drive FILE|-` reads a script of steps —
pick a car out, hold the pedals and the wheel for a while, give an order, work the lever, take a frame —
and pushes them through the very seam the player's keys go through, a tick at a time
([app/drive](src/app/drive/docs/requirements.md)). **Nothing in it can ask for more than a key can**: the
pedals are shares of their own travel, the hard envelope binds, and the town queues around the car
exactly as it would around any other. Every step prints what the unit is doing — the unit panel's own
rows — and a `shot` step writes a captioned frame into `--frames DIR` (`.tmp/drive` by default);
`--out FILE.md` writes the whole drive as one document. The same script over the same map is the same
run, so a drive is steered by appending a step and playing it again. `qq drive` is the tool that does
that.

**`--drive FILE --live` is the same drive in a window somebody is watching.** The run opens as usual and
follows the file as it is written: a step appended while the town runs is driven when it arrives, and its
reading is printed as it is taken. `pace N` runs the town at a fraction of real time — which is what makes
a hand with seconds of lag able to drive at all — `agents on` lets go of the hold a run opens with, and
`--frame-width PX` writes the frames a driver reads back as a scaled copy while the window keeps drawing
at full size.

```
dotnet run --project traffic-dotnet.csproj -- --map Odesa --live --drive .tmp/drive/live.txt \
  --frames .tmp/drive/live --frame-width 800 --ui car-lines --seconds 1800
```

```
cars 320 190            # what is standing near a place, nearest first
people 320 190          # and who is on foot near one, on the same terms
select nearest 320 190  # the car there, picked out as a click on it
select walker 320 190   # or the walker there, which a script cannot click on by hand
shot standing           # a captioned frame, riding the car
drive 2 throttle=1      # the keys, held for two seconds of the town
drive 1.5 throttle=0.4 steer=-0.6
order 360 240           # the right-click order CTL-8 already offers
release                 # the reset: the wheel and manual mode given back
```

**`--bot FILE --bot-car N` seats a second driver beside you.** It is the same steps through the same seam,
with the car named instead of picked out — so the selection, the camera, the panels, the keys and every
other car stay yours while it drives (`CTL-5d`, `DRV-8`). It looks through an eye of its own rather than
through the window: an offscreen frame of the town pinned on its car, **with no interface drawn in it at
all**, written into `--bot-frames DIR` with the car's dashboard beside it as a `.txt`. `--bot-eye PX` and
`--bot-view METRES` are how big that picture is and how much town it spans; `--bot-out FILE.md` writes the
whole of what it drove. A run may carry a bot, a live drive, or both.

**It is told what it is driving.** `handbook.md` lands beside the frames when the seat is taken: this car's
own mass and dimensions, what a pedal and the wheel are worth on it, the radius its tyres hold at the speed
these streets were laid for, and how many pixels of the frame a metre is. Every figure is read off the build
the solver drives, so nothing briefing a driver can quote a car the town does not have.

**`--bot-waits` stops the town's clock whenever that driver has run out of steps**, so what it spends
thinking costs it no ground and the frame it answers is still true when the answer arrives. The panel says
`waiting` while it does.

`qq pilot` is the harness that puts a model in that seat: it opens the run, reads the handbook to it, waits
for each frame, asks OpenRouter what to hold, checks the answer is something a key could have done and
appends it. The key comes from `OPENROUTER_API_KEY` or `~/.qq/openrouter.key` and no part of it is written
into the project.

```
qq pilot --map Odesa --car 387 --minutes 10        # the model drives; the window is yours
qq pilot --car 387 --freeze=off --pace 0.3         # or let the town run on while it thinks
dotnet run --project traffic-dotnet.csproj -- --map Odesa --windowed --bot-waits \
  --bot .tmp/pilot/steps.txt --bot-car 387 --bot-frames .tmp/pilot/eye --bot-eye 768
```

**A picture can be taken with the pointer somewhere**, which is how the layers' own readings are asked for
without a window (`OBS-2t`): `--point X Y` stands the pointer on that place in the town, so the ribbon and
the boundary under it are drawn picked out and named, and `--pick X Y` clicks there, which picks the cell of
the geometry grid and lights every line the index holds in it.

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

`--bench census --map NAME` says what a town holds and what the graphs made of it; `--bench shape`,
`--bench joints` and `--bench parks` say what shape it came out — how its roads bend, where its junctions
stand, which of them are places nothing meets (GEN-51) and which of them are car parks cut into a road
(GEN-53). `--bench outset` moves that town's own boundary off itself and **exits non-zero if any run of it
is left open**, printing the boundary and the candidate set either side of the first hole. `--bench fill`
cuts that boundary into the triangles the ground is drawn out of and reads the cut on the four things that
say whether it is the right one: **how long it takes, how many triangles it comes to, how near those
triangles are to equilateral, and how much of the shell it lost** against the area the arcs themselves
enclose. It prints the shell's open joints beside them, since the straights a fill draws across them are
ground nothing else accounts for. `qq town` is the same five
readings from the shell.

**`--bench load --map NAME` says what opening that map cost**, stage by stage: the plan, the ground — the
merge, the boundary struck off it and the layers cut from those — and the town stood up, with its three
graphs. It is a cold open in a fresh process and never
a benchmark loop: every stage here keeps its answer, so a second run of any of it measures the cache.

`--lamps` cuts the town's lamp sheet out of the fleet's own sprites — every lens a variant draws, in
each colour it can burn (CAR-14a) — and writes it to `assets/agents/car/variants/common/lamp_atlas.png`.
It is a **workshop step and never a build one**: run it when a variant's art or its lens rectangles
change, and commit the picture. The line it prints per lens is the instrument for the one thing the
arithmetic cannot answer — a rectangle over bodywork nobody painted a lamp on cuts the paint around it
and comes back undistinguished.

**One map is laid in code**, and it is laid to be looked at rather than to measure anything. **What it is
is [citygen](src/citygen/docs/requirements.md#the-maps)**; what follows is only which command reads it.

| Map | Is | Read by |
|---|---|---|
| `Idle` | one loop of road and nothing else — a square with rounded corners, an armoured car between two police running it one way and a sports car the other — the picture the game idles on, and what a run that names no map opens over | `--map Idle` |

**The laboratories are parked.** `Track` ×3, `Exam`, `Footway`, `Skidpad` and `Zebras` were laid against
the lane layer this build replaced, so they were deleted with it rather than carried across a rework they
would have had to be written for twice; what they measured is named in
[the known gaps](docs/index.md#known-gaps), and the ones that come back will be laid against the new layer.

**Every other map is generated.** `towns/Odesa.json`, `towns/River.json` and `towns/Test.json` are briefs —
a seed, an extent, the water, the districts and the counts — and the town is laid from one when the map is
opened ([citygen](src/citygen/docs/requirements.md#where-a-town-comes-from)). The same brief at the same
seed is the same town, every time. `Test` is the fixture every detailed check is staged on.

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

Then `http://localhost:8080/` — **the query string is the command line**, so `?map=Odesa&ui=nodes`
is `--map Odesa --ui nodes`. Without a map it opens on the start menu over the idle ring, exactly as the
desktop does; the page differs only in the order, since it shows the menu the moment the engine runs and
fetches the town behind it.

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

**The page carries the visual layers and none of the instruments.** `--shot`, `--sheet`, `--bench` and
`--lamps` are how a run is measured and they stay on the desktop, which has a file system and a process
that can exit.

**The page downloads the art before the first frame and a map when that map is picked.** A frame cannot
wait on a fetch, so the menu's click writes a name down and the boot's own loop — the one place a
browser run may wait — fetches the plan and stands the town up
([decision log](src/app/web/docs/decision-log.md)).

## The same town, in a hand

There is a third head. `traffic-dotnet.android.csproj` compiles the same `src/` against the **same
Vulkan** — the same device, the same swapchain, the same renderer and the same SPIR-V the desktop
draws with — and the whole of what differs is the bootstrap: an activity that takes its own window's
surface in place of a window, and fingers in place of a mouse
([app/android](src/app/android/docs/requirements.md)).

```
dotnet workload install android
dotnet build traffic-dotnet.android.csproj -t:Run -c Release
adb shell am start -n dev.trafficdotnet.town/.TownActivity -e map Odesa -e ui nodes
adb logcat -s town
```

**The intent's extras are the command line**, so `-e map Odesa -e ui nodes` is `--map Odesa --ui
nodes`, and `-e seconds` and `-e ui-scale` are the words they look like. Without a map it opens on its
start menu over the idle ring, exactly as the other two heads do. Everything the engine prints comes
out under the `town` tag, which is what a handset has in place of a terminal.

**It is one activity and nothing else** — no view, no layout, no platform control anywhere: what a
reader touches is the town's own interface, drawn by the same code that draws it on a desk, and the
fingers are read as the mobile browser's are (CTL-9). **The floor is the driver's**: the instance is
created at Vulkan 1.3, the manifest asks for that version as required, and the build is 64-bit only.

**A run ends when the activity leaves the screen** and the town is laid again when it comes back. It is
this head's one stated limit and the reason is in its [decision log](src/app/android/docs/decision-log.md).

**The release is cut by a pipeline of its own** — [`.github/workflows/android.yml`](.github/workflows/android.yml),
on a tag push or by hand. It builds an APK and an AAB, signed with the keystore in the repository's
secrets when there is one and debug-signed when there is not. **A tag ends in a GitHub release** with
both packages attached and the signing state in its notes; a `workflow_dispatch` leaves them as the
run's artifacts and makes no release. `git tag v0.1.0 && git push origin v0.1.0` is the whole of cutting
one, and `v0.1.0-rc1` — any tag with a suffix — is marked a pre-release. **Build it Release**:
`RunAOTCompilation` is on there and off in Debug, and the interpreter is about ten times off a 60 Hz
loop — the same figure the browser head quotes.

## Layout

```
src/        every line of C#, and nothing else — the nine slices below
  core/     the kernel: config, geometry, simulation — and nothing that knows about a town
  citygen/  the city plan as pure data: its structure, its cell vocabulary, and gen/ — the generator
            that lays one from a brief
  world/    terrain, road, foot, routing, physics, containment, statics, parking, town
  agents/   car, person, ambulance, service, evacuator, trafficlight — body / control, and the maneuvers:
            one file per entry of the closed catalogue (src/agents/car/maneuvers/docs/index.md)
  runtime/  the machine: the window, raw Vulkan, the swapchain, the shaders — and web/, the browser's half
  app/      screen, render, camera, hud, debug, playercontrol, shot, web, android, main — the shell
  bench/    the census and the probes
  tests/    the unit suite, laid out folder for folder as the tree it tests
  tools/    workshop tools, which may depend on what the runtime may not
assets/     the art and the .json data read at startup, mirroring the code tree
towns/      a city's brief — the seed and the intent it is generated from, a few hundred bytes each,
            and the whole of what a town is carried as
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
