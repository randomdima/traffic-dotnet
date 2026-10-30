# Multiplayer MVP — the plan

One authoritative server runs the town exactly as a desktop run does today. Thin browser clients each drive
one car the server gives them, see only the ground around it, and receive only what is in that ground. This
is the plan to get there; why each choice was made is [options.md](options.md), what it rests on in the
code is [fit.md](fit.md).

## Problem

The town is a single-process simulation with one local hand (CTL-5) and one scripted second seat
(`HandOnCar`, DRV-8). Its browser head runs the *whole* engine on the page. Nothing in it sends or receives
anything over a network, and its documents define it as *not a game* with a debugging hand.

## Goal

- A server process runs a town headless and admits clients.
- Each client is a browser page that lays the static town itself, is handed one car, drives it with the
  keys the page already reads, and sees a camera locked on it, zoomable only within limits.
- A client receives the moving things in a fixed area round its car and nothing else, and sends only its
  held keys.
- A client page shows no debug layer, no menu debug page, no status breakdown, no selection, no orders and
  no pace control.

### Success criteria

1. Four clients on a LAN drive four cars in one town at the same time, each seeing the others' cars and
   the town's traffic move smoothly.
2. No message to a client ever names an entity outside its area of interest.
3. The server's steady state and the client's steady state allocate nothing (rule 2), and the page's
   crossings per frame are unchanged (WEB-2).
4. A client's view at any instant agrees with the server's town at the client's render tick, to within
   the position quantum, for every entity it shows.
5. Bandwidth a client is under the budget the plan sets (≈100 kbit/s target, figures from `--bench`).
6. Input → photon latency is measured and printed (target ≤ 250 ms on a LAN; the figure decides whether
   own-car prediction comes next).

## Decisions the owner makes first

Nothing is built before these are answered, because each rewrites a document the code answers to.

1. **goals.md, "What it is not".** Multiplayer gives each client a player character. The owner rewords
   "Not a game", or states how a networked hand is still an instrument.
2. **goals.md, rule 1.** A socket is a third unmanaged boundary beside the driver and the window. Proposed
   wording: crossings stay O(1) in the size of the town and are at most one send and one receive per
   client per snapshot.
3. **Whether rule 2 binds the network thread.** Proposed: yes — the server's network thread reports into the
   same allocation counter the gates read.
4. **Rungs for the new rules.** The new slice's rules are written by the assistant at `P2`–`P9`; any the
   owner wants at `P0`/`P1` (likely candidates: *a client never receives what is outside its area*, *the
   server is the only authority*) are granted by the owner in so many words.
5. **The web rules that change**: WEB-3 (a joined page carries no instruments), WEB-7 (a server deploys
   beside the static page), WEB-10 (a joined page steps no world). These are `P4`, the assistant's, but they
   follow from 1 and are worded only after it.
6. **Where the slices sit** (below) — a new row or rows on [docs/slice-map.md](../docs/slice-map.md).

## Architecture

```
                    server process (desktop exe, --serve, no window)
 ┌───────────────────────────────────────────────────────────────────────────────┐
 │ SimLoop ─► TownWorld (agents, claims) ─► ManagedWorld (bodies)  60 Hz        │
 │      ▲ seats: one HandInput per client car                                    │
 │      │                       │ every Nth tick: capture drawn state (one copy) │
 │      │                       ▼                                                │
 │  input inbox ◄── net thread ──► per client: area query ► delta vs acked ► send│
 └──────────────────────────────┬────────────────────────────────────────────────┘
                                │ WebSocket (binary): inputs up, snapshots down
 ┌──────────────────────────────┴────────────────────────────────────────────────┐
 │ page (web head, ?join=…): town.js socket ─► ring in WASM memory ─► Pump      │
 │   lays static town from map+seed ─► replica (drawn state, by agent index)    │
 │   clock estimate ─► render tick ─► interpolate ─► sprite fill ─► WebGPU      │
 │   signals, clouds, lamps' flashing computed at the render tick               │
 │   player HUD only; camera locked on own car; keys ─► input ring ─► Pump      │
 └───────────────────────────────────────────────────────────────────────────────┘
```

### The pieces, and where each lives

Names and exact folders are the implementer's, settled with the slice map (decision 6). The tiering they
must respect:

- **The wire** — message kinds, the bit packer, quantisation, the baseline/ack bookkeeping. Knows `core/`
  only: it moves numbers, and is handed data, never a town type.
- **The socket** — a transport seam that carries whole messages: the server's listener and per-client
  streams, and the page's half in `town.js` plus its reader in `runtime/web/`. Machine tier: knows nothing
  of a town. A head-named folder (`runtime/web/`) holds the browser half, as today.
- **Seats** — N held inputs, each on one car, replacing the single `_otherCar`/`_otherHand` in `TownWorld`.
  Composition seam (`world/town/`); the bot seat becomes one of them.
- **The server** — the serve mode: admission, seat assignment and release, capture, area of interest,
  per-client encode. Shell tier.
- **The client** — the join mode: replica, clock, interpolation, locked camera, player interface. Shell tier,
  under `app/main/` like `app/shot/`.
- **The drawn-state seam** — what the moving fill reads, handed over as data, supplied by the town on a
  local run and by the replica on a joined one. Lives with the render.

### The drawn-state seam

The one refactor the client cannot do without. `TownSprites.FillMoving` and the fills under it read
`TownWorld` (fleets, lamps, steer, tow hitches, rings) and cull through the solver's grid. They are to read
instead a narrow record per moving thing — the device emit's `CarRow`/`PersonRow` vocabulary is the model,
so the desktop's device path, the CPU fill and the wire describe a drawn car one way — plus a visible set,
plus the tick-derived pieces (signals, crossing gear, clouds) laid from the plan rather than from
`TownWorld`. A local run's pictures do not change; the e2e frames prove it.

### Flows

**Join.**
1. The page opens with `?join=<url>` (WEB-5: the query string is the command line) and connects.
2. It says hello: build identity (code, figures, art), and nothing else.
3. The server answers with the map, seed, tick rate, snapshot interval, area-of-interest span, zoom limits,
   the current tick, the plan digest, and the car it is given — or refuses: build mismatch, server full, no
   takeable car.
4. The page loads and lays the map (the progress card, WEB-8, covers it), checks the plan digest against
   the server's, and says ready.
5. The server takes the car (manual mode, CTL-4) and starts streaming, the first snapshot against an empty
   baseline.

**Steady state.**
- The server ticks at 60 Hz. Each tick applies every seat's latest input — the last one held if none came.
- Every Nth tick the server copies the drawn state of the whole town once into a capture ring, O(town)
  memcpy on the sim thread and nothing more.
- The net thread, for each client: queries the area round its car, diffs against that client's acknowledged
  baseline, writes one message, sends it. If the previous send has not completed, this snapshot is skipped
  for that client and never queued.
- The page: JS copies each message into the inbound ring; `Pump` hands it to .NET; the replica applies it;
  the clock estimate updates; the frame interpolates between the two snapshots bracketing the render tick
  and draws. Each frame the page writes the held keys and the sequence of the newest snapshot it holds
  (the ack) into the outbound ring; JS sends them.

**Leave and timeout.** No input for a short while: the car is held braked. Longer: the seat is released and
the car goes back to the town (CTL-4's reset). A dropped socket goes straight to the longer case.

**Wreck.** The town's own recovery runs. The client is told its car is a wreck (it is in the car's state
anyway); after a delay the seat is moved to another takeable car and the camera follows it there.

### What a message carries (at the level of intent)

- **Down, a snapshot**: the server tick it stands for, its sequence, the baseline it is a delta against, the
  last input sequence the server applied, the own car's index, then entities sorted by agent index: entered
  (with their constants — kind and look), changed (only the fields that changed), left. Level crossings in
  the area ride as state.
- **Up, an input**: its sequence, the newest snapshot received (the ack), and the held keys — throttle,
  steer, handbrake — for the last few ticks redundantly. Nothing else: no area, no zoom, no car index.
- **Control**: hello, welcome/refusal, ready, and a reason on close.

The byte layout, field widths and the redundancy depth are the implementer's, derived from authored quanta
and the town's extent.

### Figures

A new group on `SimConfig` (CLAUDE.md: a literal in behaviour code is a defect), authored as raw terms:

- *Authored*: snapshot interval in ticks; position quantum (m); heading quantum (deg); the farthest a client
  may zoom out, as a view span (m); interpolation delay (in snapshot intervals or s — implementer's call);
  input-silence holds and releases (s); wreck reassignment delay (s); most seats.
- *Derived*: bit widths, from quanta and the town's extent; the area-of-interest span, from the widest view
  plus the margin; the margin, from the town's top speed × (interpolation delay + one snapshot interval).

## Phases

Each phase ends green on the tier named and is reviewable alone.

1. **Owner decisions** and the document changes they unlock.
2. **Seats.** N held inputs on N cars; assignment of a takeable car; release; wreck reassignment. Driven by
   unit and town-tier tests with no network at all; the bot seat moves onto it.
3. **The drawn-state seam.** The moving fill reads the seam; signals, crossing gear and clouds are laid from
   the plan. Local runs unchanged — e2e frames compared before and after.
4. **The wire.** Bit packer, quantisation, snapshot diff/apply, acks and baselines, input messages. Pure,
   unit-tested, allocation-gated.
5. **The server.** Serve mode, headless managed world, capture ring, area of interest, per-client encode,
   WebSocket transport, admission. Proven in-process over loopback against a test client.
6. **The client.** Join mode on the page: socket in `town.js`, rings through `Pump`, replica, clock,
   interpolation, locked camera, zoom limits, player interface.
7. **Measure.** Bandwidth, server tick and encode time per client, latency, popping at the edge. The
   latency figure decides whether own-car prediction is next.

## Risks and edge cases

- **The managed world cannot hold 60 Hz on the chosen map headless.** Measure first; the MVP map follows
  the figure. SIM-9 slows the town rather than queuing, and clients follow the tick.
- **Laying the town on the page at join is slow or does not fit** (OdesaOsm: 84 s, 2.8 GB). The MVP serves
  a generated city small enough to lay quickly; shipping a pre-laid static town is post-MVP.
- **Build skew.** A page and a server from different builds lay different towns. The hello carries the build
  identity and the welcome the plan digest; any mismatch refuses, with the reason shown.
- **Takeable cars run out.** Nothing spawns at run time. The server counts them at start and refuses joins
  past what remains.
- **A car taken while somebody is aboard or on a trip.** The take must use the same rule a hand uses today
  and pick only cars with no walker's trip on them.
- **Head-of-line stalls** on a lossy link freeze the stream for a round trip. Accepted for the MVP; the
  protocol is ready for datagrams.
- **Slow client.** Skipping snapshots instead of queuing keeps the server's memory flat; the acknowledged
  baseline keeps the delta correct after a skip.
- **A crafted message.** Every read range-checked; a message that fails is a closed connection, never an
  exception path that allocates; inputs clamped; at most one consumed a tick.
- **The drawn-state refactor changes a local picture.** Caught by the e2e frames before phase 3 lands.
- **The page's crossing budget.** All network I/O on the page goes through the existing `Pump`; the crossing
  gate covers a joined frame.
- **Clock drift and jitter.** Playout rate nudged, never jumped; extrapolation capped when the buffer runs
  dry, then the picture holds.
- **TLS.** A page on HTTPS can open only `wss://`; the deployment puts a TLS-terminating proxy in front.
  Nothing in the plan disables certificate checks.

## Test intentions

Each a behaviour or a stated requirement, failing for nothing else (VER-12).

- **Wire.** A field round-trips to within its quantum and exactly onto its quantised value. An entity
  unchanged against the baseline costs nothing but its presence. A snapshot applied to its baseline gives
  the state it was taken from. A truncated or out-of-range message is refused without throwing.
- **Area of interest.** An entity enters inside the span and leaves only past the span plus the leave
  margin. An entity outside the area is never written into any client's message — asserted over a town
  run, against every message.
- **Seats.** Each seat's input moves only its car. A seat released returns its car to the town (CTL-4). A
  wrecked seat is moved to a takeable car after the delay. The server refuses a join with no takeable car.
- **Clock.** The estimate converges on the server tick under jittered arrivals and follows a server that
  slows.
- **Loopback.** A server and a client in one process over a real socket on `Towns.Fixture`: at every render
  tick the client's pose of every entity it holds is the server's pose at that tick, interpolated, within
  the quantum.
- **Gates.** The server's capture + area + encode + send loop and the client's receive + apply +
  interpolate + fill allocate nothing in steady state. A joined page's frame crosses no more than WEB-2
  allows. The server's sends per snapshot are the client count, whatever the town's size.
- **Picture.** A captioned shot of a joined page (`qq web --shot`) against a local server shows the car
  centred, the town moving round it, and none of the instruments.
- **Instruments, not gates.** A `--bench` reading of bytes a client a second, entities a snapshot, the
  server's encode time per client and the measured input → photon latency.

## Open decisions, handed to implementation

- Snapshot rate: 20 Hz is the starting figure; 30 Hz if 20 looks soft with Hermite interpolation.
- Whether velocity rides the snapshot (Hermite) or is differenced on the client.
- Exact quanta and bit widths; the redundancy depth of inputs.
- Whether the capture ring is copied on the sim thread or read from the world layer's double-buffered view.
- North-up or heading-up for the locked camera; whether a turn is offered at all.
- How the player interface reads: speed, look, wrecked — DRV-8's dashboard is the precedent — plus link
  state (round-trip, loss).
- Folder names and the slice-map rows (owner decision 6 settles the tiers; names are free).
- Whether the hand-written WebSocket handshake suffices or Kestrel is taken for the edge.

## After the MVP, in the order the measurements are likely to ask for them

1. Own-car prediction with the single-car model and smoothed reconciliation (options 3C).
2. A priority accumulator under a byte budget, for dense traced cities.
3. WebTransport datagrams once a .NET server can serve them; the codec is already written for loss.
4. A walker as the actor.
5. Reconnect into the same seat; signed join tokens for admission.
6. A pre-laid static town shipped to the page, so a large city joins without laying.
7. A desktop thin client over the same protocol.
8. The server stepping the device world headless (closes a known gap besides).
