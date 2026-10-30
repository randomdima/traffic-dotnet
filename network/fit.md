# Multiplayer — what this codebase already gives, and what it fights

How the survey in [research.md](research.md) lands on this engine. Every fact below was read from the
code on branch `citygen-rework` (2026-10-30); paths are from the repository root. The options it leads to
are [options.md](options.md).

## What the engine already has that netcode usually has to build

**Identity is free.** The roster is laid once and never changes: a car, a walker, a wagon keeps one index
for the whole run, and no slot is ever reused (`src/world/physics/BodyId.cs`, `AgentRoster` in
`src/core/simulation/AgentRoster.cs`). A walker entering a building is *contained*, not removed; a wreck
stays; a train's wagons go in and out of the world through a flag (`WagonView.InWorld`, TRN-7). So the wire
can name an entity by its agent index — no network-id allocator, no spawn/despawn protocol, only *enters
view* and *leaves view*.

**The static town is never sent.** Both layers already lay the ground, buildings, ribbons and signals off
the same map (goals.md, "Static things are never communicated"; `Maps.Plan` in `src/citygen/Maps.cs`). A
client given the map name and seed lays the same town, and `CityPlan.Digest()` (`src/citygen/CityPlan.cs`)
can prove it did.

**Much of what moves is a function of the tick.** Traffic-light phases are a fixed-time table
(`SignalService.ForApproach/ForCrossing(lane, timeS)`, offsets laid from the world seed); clouds, wind,
water drift and fans are seeded from `plan.Seed` and read at a tick (`Game.cs` 1020–1024). A client that
knows the server tick computes them all and is sent none of them — and because it computes them at the
*render* tick, a light turning red and the car stopping for it stay in step.

**Inputs are held state, not events.** `HandInput(Held, Throttle, Steer, Handbrake, WalkDirection)`
(`src/world/town/Selection.cs`) is what is held down this tick, applied every tick until replaced. A lost
or late input is simply the previous one repeated — the semantics the field builds input buffers to get.

**A second driver already exists.** `TownWorld.HandOnCar(car, input)` (`src/world/town/TownWorld.Interface.cs:304`)
lets the bot seat (DRV-8, "so that two people can drive one town at once") drive one named car through
the same command path a keyboard uses, with manual mode and the reset (CTL-4) already defined. It holds
exactly one car (`_otherCar`); multiplayer is that, N times.

**There is a vocabulary for "what is drawn of a car".** The desktop's device emit reads a 48-byte
`CarRow` — position, forward, heading, steer, lamps, variant, flags, towing, fork, tilt
(`src/app/devicetick/DeviceSprites.cs`) — and a 24-byte `PersonRow`. That is almost exactly a snapshot's
per-entity record, and reusing it keeps one answer to the question (P4).

**Spatial queries are allocation-free.** `TownWorld.CarsIn` / `PeopleIn(least, most, Span<int> into)`
(`src/world/town/TownWorld.cs:547-552`) query the solver's grid into a caller's span — the area-of-interest
query, already built.

**The town keeps time against a clock, and slows rather than queues.** SIM-9 (`SimClock`): a tick slower
than the time it stands for slows the town, and is never caught up on. That is EVE's time dilation; a
snapshot stamped with the tick carries it to every client for free.

**A headless town runs today.** `--bench`, `--shot` and the suite step the managed world
(`src/world/town/ManagedWorld.cs`) with no window; a server is that, with sockets.

**The browser head exists.** `traffic-dotnet.web.csproj` runs the whole engine as .NET WebAssembly on
WebGPU; its page talks to .NET through one `Pump` a frame over memory JS writes into
(`src/runtime/web/AppWindow.Web.cs`, `src/app/web/wwwroot/town.js`). A socket's bytes can land in that
same memory and be read in that same crossing.

## What it lacks

- **No networking, serialisation, snapshot or replay code at all.** No socket, `HttpListener`, Kestrel or
  WebSocket anywhere in `src/`.
- **The sprite fill reads the town, not a feed.** The web head draws moving things from the CPU fleets via
  `TownSprites.FillMoving(world, …)` (`src/app/render/TownSprites.cs:396-450`), which reads `TownWorld`
  directly — positions *and* actor-layer facts not in `WorldView` (variant, lamps, steer, broken, tow,
  rings) — and culls through the solver's grid. A client with no `TownWorld` has nothing for it to read.
- **Signals, crossing gear and standing pieces hang off `TownWorld`** (`world.Signals`, `world.Heads`,
  `world.CrossingGear`). A client that lays no agents still needs these.
- **Only one remote hand**, and the camera has no locked mode: a pan or turn takes the camera off its
  follow (`Follow.TakenFrom`, `src/app/camera/Follow.cs`).
- **The device world runs only in a window** (docs/index.md, known gaps). A headless server steps the
  managed world.
- **Laying a city takes time on a page**: most of a minute for a generated city, 84 s and 2.8 GB of heap for
  OdesaOsm (docs/index.md, known gaps). A client that lays its own town pays that at join.

## What the per-entity state actually is

What the CPU fill reads per moving thing, and so what a client must be given or can compute:

| Thing | Changes every tick | Changes rarely (state) | Constant (sent on entering view) | Computed by the client |
|---|---|---|---|---|
| Car | position, heading, steer, tilt | lamps (`CarLampSet`, 7 bits), wreck, level, tow hitch | variant (look) | lamp flashing (phased off the tick) |
| Walker | position, heading | walking, wounded, inside | variant | stride frame (from distance walked) |
| Wagon | position, heading | in world, on a bridge | look | — |
| Level crossing | — | closed, and when | — | barrier swing from that |
| Signal heads, clouds, wind, water, fans, chimneys | — | — | — | all of it, from tick and seed |

The page draws exhaust smoke and tyre marks as the desktop does (WEB-12). Smoke is worked out on the device from what
moves and is not sent. Tyre marks are strokes of knots with a log of every place written (`DriftMarks.Written`,
`CopyChangedInto`): what changed since a snapshot is a handful of 24-byte knots, the shape a snapshot would carry them
in.

## What the two rules ask of a network layer

**Rule 1 — crossings O(1) in the size of the town.** A socket call is a managed→native crossing. One send
per client per snapshot is O(players), never O(cars), and passes the spirit of the rule — but goals.md
says *"the unmanaged boundary is at the graphics driver and the window and nowhere else"*, and a socket is
a third boundary. That sentence changes before any code does. On the page, received bytes written by JS
into memory the existing `Pump` reads cost no crossing at all; WEB-2's three-a-frame budget can hold.

**Rule 2 — the steady state allocates nothing.** The gates measure the sim thread and
`AllocatedAsideBytes` (`src/tests/gates/AllocationGateTests.cs`); a network thread is invisible to them
until it reports into that counter. `WebSocket.ReceiveAsync(Memory<byte>)` / `SendAsync(ReadOnlyMemory)`
return `ValueTask`s that do not allocate in steady state; encode buffers from a pool; a join is not the
steady state.

## What it contradicts in the documents

These are not edits to make in passing; each is the owner's or a rule's own change, and the list is what
[mvp-plan.md](mvp-plan.md#decisions-the-owner-makes-first) asks first.

- **goals.md, "What it is not"** — *"Not a game. No player character … a hand that can take over one unit
  at a time … is a debugging instrument."* Multiplayer is a player character per client.
- **goals.md, rule 1** — the unmanaged boundary named as the driver and the window only.
- WEB-3 (rung P4): the page carries the interface and the debug layers. A joined page carries neither.
- WEB-7 (P4): what is deployed is a static site and nothing runs beside it. The page stays static; the
  server is a new deployable beside it.
- WEB-10 (P4): a page steps its world. A joined page steps none.
- CTL-5 (P7): one hand reaches all the selected units. Every client is a hand on one unit; the local hand
  stays as it is.
- SIM-9 (P2): real time against the display's rate. A server has no display; its ticks keep wall time on
  their own, and clients follow its tick.
- Determinism (SIM-4, AGT-6) is not asked of anything here: snapshots carry state, and a town driven by
  several hands forks its timeline exactly as one hand does today.

No `P0` or `P1` rule (`qq req --rungs`) touches transport, camera, the interface or control.

## Sizing the feed

An estimate, to be replaced by `--bench` figures once the feed exists. Assumptions: snapshots at 20 Hz
(every third 60 Hz tick), a view of up to ≈150 m square, ≤100 cars and ≤100 walkers in it of which half
move.

| Record | Absolute | Delta, moving | Delta, at rest |
|---|---|---|---|
| Car | ≈80 bits (index, 2 × ~20-bit position at 1 cm, 12-bit heading, steer, tilt, lamps, flags) | ≈35–45 bits | 1 bit |
| Walker | ≈60 bits | ≈25–35 bits | 1 bit |

≈50 moving cars × 40 + 50 walkers × 30 + 100 at rest ≈ 3.6 kbit a snapshot, ≈70–90 kbit/s a client
with headers — a third of the 256 kbit/s budget the field quotes. A full snapshot at join is ≈2 KB. For
tens of clients the server's upstream is a few Mbit/s, and its encode work is O(clients × visible), not
O(town).
