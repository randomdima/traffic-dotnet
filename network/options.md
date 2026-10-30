# Multiplayer — the options, axis by axis

Each axis lists what was considered, what it costs here, and the pick for the MVP. The picks assemble into
[mvp-plan.md](mvp-plan.md); the facts they rest on are [fit.md](fit.md) and [research.md](research.md).

## 1. Architecture

| Option | Here |
|---|---|
| **A. Authoritative server, snapshot interpolation** | The server already simulates everything; clients draw only. Needs no determinism, and nothing a client is not sent exists for it. |
| B. Deterministic lockstep | Every client would run the whole town — the opposite of a thin client — and the device world is not bit-identical across machines (WEB-10's own wording: "within what a driver may fuse"). Every client holds every car's position. Ruled out. |
| C. State sync, every client simulating | Same objection: a client cannot simulate what it cannot see. |

**Pick: A.**

## 2. What the client runs

| Option | Cost | Risk |
|---|---|---|
| A. A dormant `TownWorld`: lay the whole town with its agents, never tick it, overwrite fleet arrays from snapshots | Least code; the fill, signals and crossing gear work untouched | Two answers for every fleet field (laid vs received); stale grid culling; every car's spawn pose resident; agents' memory and lay time paid for nothing; a slip lets the client's agent layer act |
| **B. A replica: arrays of what is drawn, fed by snapshots; the fill reads a narrow drawn-state seam that the town and the replica both supply** | A refactor of the moving fill and of where signals/crossings are laid from | One seam, reused by the device emit's existing `CarRow` vocabulary; the client provably holds only what it was sent |
| C. A separate client codebase (JS renderer) | Second renderer, second asset story | WEB-1/WEB-4 are written against exactly this |

**Pick: B.** The seam it needs — "what is drawn of a moving thing", handed over as data — is the slice-map's
own way of keeping a lower layer from knowing a higher one, and the desktop's device emit already speaks it.

## 3. The player's own actor

| Option | Input → photon latency | Cost |
|---|---|---|
| **A. Interpolate it like everything else** | RTT + interpolation delay + ≤1 tick ≈ 150–250 ms | Nothing extra |
| B. Extrapolate it to the estimated present from the last snapshot's velocity and yaw rate | ≈ RTT/2 less | Snap-back on contact and hard braking; capped at ≈250 ms |
| C. Predict it with the single-car model on the client and reconcile | ≈ one frame | A CPU car model on the page (the tyre model is managed C# already, `agents/car/body/`); reconciliation and smoothing; the predicted car meets interpolated traffic — Rocket League's problem |
| D. Lockstep the own car (input delay only) | RTT | Every player waits for the slowest |

**Pick for the MVP: A**, measured. Town speeds are far under racing ones; the camera's follow lead
(`CameraFollowLeadS`, OBS-1a) already softens how latency reads. **C is the first post-MVP step** if the
measurement says driving feels late, and the protocol carries input sequence numbers from day one so C needs
no wire change.

## 4. The actor itself

| Option | Here |
|---|---|
| **A. Take over an idle town car** — parked, unowned, not a service vehicle, not a wreck — and give it back to the town on leave (CTL-4's reset) | No change to laying, to the roster or to the plan's digest; exactly what a hand does today |
| B. A reserved fleet of player cars laid with the town | Changes laying and the roster; the server's seat count becomes part of the map's identity |
| C. A walker | CTL-5 already drives one (`HandWalk`); the camera follows a rider into a car |

**Pick: A, cars only.** The protocol names the actor's kind so a walker (C) is a client-side change later.
Since nothing spawns at run time, the seat count is bounded by the takeable cars the town holds — the
server says so instead of failing late.

**When the actor is wrecked**, the town's own recovery runs (the evacuator, EVA); the seat is reassigned a
fresh car after a delay, and the camera stays on the wreck meanwhile.

## 5. Interest management

| Option | Here |
|---|---|
| **A. A server-fixed square around the actor, sized from the farthest the client may zoom out plus a margin, with a leave margin (hysteresis)** | The client names no area; `CarsIn`/`PeopleIn` already answer the query into a span |
| B. The client's reported viewport | Lets a client widen its own view; needs validation that collapses to A |
| C. A + a priority accumulator under a byte budget | Only matters when a view holds more than the budget; at the estimated ≈80 kbit/s it does not |

**Pick: A**, with the margin *derived*, never authored: what an entity can travel in the interpolation delay
plus a snapshot interval at the town's top speed, so nothing pops into view from inside it. **C is held in
reserve** for dense traced cities; the encoder is written so a budget can be added without a format change.

## 6. Snapshot encoding

| Option | Here |
|---|---|
| **A. Hand-written bit packer over `Span<byte>`, quantised fields, delta against the last acknowledged baseline** | Allocation-free, range-checked on read, transport-agnostic |
| B. Delta against the last snapshot sent (valid only over a reliable transport) | Smaller on WebSocket; silently wrong on datagrams |
| C. MemoryPack / a generic serialiser | No quantisation; documented for trusted data only |

**Pick: A.** Quanta are authored as lengths and angles (raw geometric terms); bit widths are derived from
them and from the town's extent — the figures rule in CLAUDE.md. Velocity rides along if Hermite
interpolation at 20 Hz needs it; that is measured, not assumed.

## 7. Transport

| Option | Browser | Server | Fit |
|---|---|---|---|
| **A. WebSocket, binary, server on `TcpListener` + the HTTP upgrade handshake + `WebSocket.CreateFromStream`** | everywhere | in the base class library, no package | No framework owning the loop; one stream per client; head-of-line stalls under loss |
| B. WebSocket on Kestrel | everywhere | ASP.NET Core shared framework and its host | Robust HTTP edge; brings a host that wants the loop, which goals.md refuses |
| C. WebTransport datagrams | Baseline 2026 | Kestrel preview has **no datagrams**; `System.Net.Quic` exposes none | Right answer, not buildable in .NET today |
| D. WebRTC DataChannel | everywhere | SIPSorcery; ICE/DTLS/SCTP | Complexity out of proportion to an MVP |

**Pick: A**, behind a transport seam that carries whole messages, with the protocol written for unreliable
delivery (sequence numbers, acks, acknowledged baselines, redundant inputs) so C replaces A without touching
the codec. If the hand-written handshake or TLS bites, B is the fallback and costs only the edge.

**TLS**: a page served over HTTPS may only open `wss://`. Locally (`qq web --serve`, plain HTTP on
127.0.0.1) `ws://` works. Deployed, either the server wraps its stream in `SslStream` or a reverse proxy
terminates TLS; the MVP takes the proxy.

**On the page**, the socket lives in `town.js`: received messages are copied into a ring in WASM memory and
read in the existing `Pump`; the outgoing input is read from memory the same way. `ClientWebSocket` in .NET
WASM would add crossings and allocations per message.

## 8. Clock

| Option | Here |
|---|---|
| **A. Server tick stamped on every snapshot; the client estimates the server tick from arrivals, renders a fixed interpolation delay behind it, and nudges its playout rate a few per cent rather than jumping** | Standard; follows server time dilation for free |
| B. Explicit ping/time-sync exchange | Needed only for prediction (option 3C); added then |

**Pick: A.** The render tick drives signals, clouds and lamp flashing too, so the computed world and the
received one agree.

## 9. Hosting the server

| Option | Here |
|---|---|
| **A. The desktop executable in a headless serve mode, managed world** | Exists in all but sockets; the device world needs a window (known gap) |
| B. A separate server project | Second entry point to keep in step with the first |
| C. Headless device world | Closes a known gap; not on the MVP's path |

**Pick: A.** Whether the managed world holds 60 Hz for the chosen map is measured before anything else
(`--bench`), and the MVP's map is picked by that figure.

## 10. Keeping debug off the client

| Option | Here |
|---|---|
| **A. One web build; a joined page constructs the player interface only — no menu debug page, no layers, no selection, no orders, no pace** | WEB-1 holds (one code); nothing secret is in the page because the server never sends it |
| B. A third project file compiling the debug slices out | Smaller page; a second build to keep green |

**Pick: A.** Security is the server's: a modified page can only show what it was sent, which is the area
around its own car. B is a size optimisation for later.
