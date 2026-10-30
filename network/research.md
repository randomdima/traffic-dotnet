# Multiplayer netcode — what the field does

A survey of how authoritative-server games keep thin clients in step, read for one question: what a
server that simulates a whole town owes a handful of clients that each see a small part of it. Sources
were read 2026-10-29/30; where a claim rests on a secondary or community source it says so. How this
applies to the town is [fit.md](fit.md).

## The three architectures

**Snapshot interpolation.** The server simulates everything and sends each client the state it may see;
the client draws the past, interpolating between the two snapshots that bracket its render time. No
determinism is needed, and the client runs no game logic for anything it does not control.
([Gaffer On Games — Snapshot Interpolation](https://gafferongames.com/post/snapshot_interpolation/),
[SnapNet — Netcode Architectures 3](https://www.snapnet.dev/blog/netcode-architectures-part-3-snapshot-interpolation/))

- Source engine defaults: 20 snapshots/s and a 100 ms interpolation delay, which survives one lost
  snapshot; past that it extrapolates linearly for at most 0.25 s.
  ([Valve — Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking),
  [Interpolation](https://developer.valvesoftware.com/wiki/Interpolation))
- Fiedler's rule: buffer enough to survive two lost packets — about three send intervals (≈150 ms at
  20–30 Hz). Hermite interpolation with the sent velocity removes the artefacts linear interpolation shows
  at low rates.
- Costs: one delta payload per client (server CPU), and a locally predicted object interacting with
  interpolated ones looks inconsistent.

**Deterministic lockstep.** Only inputs cross the wire, so bandwidth is independent of how many objects
exist — but it needs bit-exact determinism across machines, every client simulates the whole world, a late
input stalls everyone, and one divergence is a desync and a rejoin.
([Gaffer On Games — Deterministic Lockstep](https://gafferongames.com/post/deterministic_lockstep/),
[Factorio FFF-188](https://factorio.com/blog/post/fff-188), [FFF-76](https://factorio.com/blog/post/fff-76))

**State synchronisation.** Both sides simulate; inputs plus prioritised state are exchanged and both sides
quantise identically so their extrapolations agree. Needs more precision than snapshots.
([Gaffer On Games — State Synchronization](https://gafferongames.com/post/state_synchronization/))

**Reference points.** Quake 3 keeps the last 32 snapshots per client and deltas against the last one the
client acknowledged, falling back to a full update when that is too old; loss needs no special handling
because every delta carries everything unacknowledged ([Fabien Sanglard — Quake 3 network model](https://fabiensanglard.net/quake3/network.php)).
Overwatch steps fixed 16 ms command frames and has the client run ahead by RTT/2 plus a buffered frame,
time-dilating to keep the server's input buffer fed (secondary: [Edgegap on the GDC 2017 talk](https://edgegap.com/blog/game-backend-deep-dive-overwatch-2016-netcode-architecture-rollback)).

## Predicting the controlled actor

- **Prediction with reconciliation**: inputs carry sequence numbers, the server echoes the last it applied,
  the client resets to the server's state and replays the rest.
  ([Gambetta — Client-Side Prediction](https://www.gabrielgambetta.com/client-side-prediction-server-reconciliation.html))
- **Vehicles extrapolate well** — "a racing car can't do a 180º turn instantly" — agile avatars do not
  ([Gambetta — Entity Interpolation](https://www.gabrielgambetta.com/entity-interpolation.html)).
- **Rocket League** predicts the player's car against the server; its predecessor moved to full rollback
  because a predicted car meeting an interpolated ball was the core problem. Remote cars' predicted input
  decays 100 % → ⅔ → ⅓ → 0, since undershooting looks better than snapping back (secondary:
  [SnapNet — Rollback](https://www.snapnet.dev/blog/netcode-architectures-part-2-rollback/), citing the GDC 2018 talk).
- **Rollback is CPU-bound**: fifteen resimulated frames in one 16.6 ms frame leaves ≈1.1 ms each;
  overrunning it spirals.
- **Delay tolerance**: a racing-simulator study found ≤50 ms uncritical and >100 ms damaging to realism
  (Pantel & Wolf, NOSSDAV 2002 — thresholds from secondary summaries,
  [paper](https://www.cs.ubc.ca/~krasic/cpsc538a/papers/p23-pantel.pdf)). Town speeds are far below racing
  speeds, so the threshold is a ceiling here, not a floor.
- **Lag compensation** (server rewinding for hit tests) answers instant hit-scan weapons; a town has none.

## Interest management

- **Relevance first, priority second.** Unreal gates replication on a cull distance, then ranks what is
  relevant by base priority × time since last sent
  ([Unreal — Relevancy](https://docs.unrealengine.com/4.27/en-US/InteractiveExperiences/Networking/Actors/Relevancy)).
- **Grid, not pairwise distance.** Unreal's Replication Graph keeps per-cell actor lists; Mirror's spatial
  hash gives each connection its own and neighbouring cells, ≈30× faster than per-pair checks
  ([Replication Graph](https://www.unrealengine.com/en-US/tech-blog/replication-graph-overview-and-proper-replication-methods),
  [Mirror — Spatial Hashing](https://mirror-networking.gitbook.io/docs/manual/interest-management/spatial-hashing)).
- **Priority accumulator.** Each object accumulates its priority every send; the highest are written until
  the byte budget is spent and theirs reset, so nothing starves
  ([Gaffer On Games — State Synchronization](https://gafferongames.com/post/state_synchronization/)).
- **Hysteresis** — enter at r, leave at r + margin — is standard practice without one canonical source;
  Unreal's time-limited destruction of dormant actors is the nearest documented analogue.
- **The server decides the area.** FiveM culls entities outside a fixed focus zone per player on the
  server, so a client never receives them ([FiveM — OneSync](https://docs.fivem.net/docs/scripting-reference/onesync)).

## Encoding a snapshot

- **Quantise to what the picture needs.** Fiedler takes a cube from 321 to 80 bits: position bounded at
  512 steps a metre, orientation in 29 bits, velocity dropped at 60 Hz
  ([Gaffer On Games — Snapshot Compression](https://gafferongames.com/post/snapshot_compression/)).
- **Delta against the acknowledged baseline.** An unchanged object costs one bit; changed indices
  relative-encode at ≈5.5 bits; a scene at rest is mostly headers. Bandwidth goes from 17.4 Mbit/s to
  under a 256 kbit/s target.
- **A hand-written bit packer** beats generic serialisers, and **every read is range-checked** or a
  crafted packet overflows or loops
  ([Gaffer On Games — Reading and Writing Packets](https://gafferongames.com/post/reading_and_writing_packets/)).
  MemoryPack documents itself as for trusted data only ([README](https://github.com/Cysharp/MemoryPack)).
- **State over events.** Anything that is state (a lamp, a flag, entering view) rides the delta and heals
  itself; only true one-off events need a reliable resend.
- **Packet size.** Quake 3 cut at 1400 B; QUIC guarantees only 1200 B datagrams
  ([RFC 9000 §14](https://www.rfc-editor.org/rfc/rfc9000#section-14)).

## Browser transports, 2026-10

| Transport | Delivery | Browser | .NET server |
|---|---|---|---|
| WebSocket | reliable, ordered (TCP head-of-line blocking) | everywhere | `WebSocket.CreateFromStream` over any stream, Kestrel, `HttpListener` |
| WebTransport | datagrams + streams over HTTP/3 | Baseline since March 2026, Safari 26.4 ([MDN](https://developer.mozilla.org/en-US/docs/Web/API/WebTransport)) | Kestrel preview only; **datagrams unimplemented** ([aspnetcore#42784](https://github.com/dotnet/aspnetcore/issues/42784), blocked on runtime QUIC datagrams) |
| WebRTC DataChannel | unreliable/unordered available | everywhere | SIPSorcery; ICE/DTLS/SCTP complexity ([Fiedler](https://gafferongames.com/post/why_cant_i_send_udp_packets_from_a_browser/)) |

- Under 15 % loss on localhost, WebSocket delivery degraded where WebRTC and WebTransport did not
  ([Sh3b0/realtime-web](https://github.com/Sh3b0/realtime-web), 2023).
- `System.Net.Quic` is stable since .NET 9 but exposes no datagram API and needs `libmsquic` on Linux
  ([QUIC overview](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-overview)).
- Compression on a WebSocket under TLS carries CRIME/BREACH risk
  ([ASP.NET Core WebSockets](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets)).
- **The field's advice**: ship on WebSocket, but write the protocol for unreliable delivery from the start —
  sequence numbers, acknowledgements, deltas against acknowledged baselines, redundant inputs — so the
  transport can be swapped for datagrams later without touching the codec.

## .NET libraries

LiteNetLib, Riptide and ENet-CSharp are UDP libraries for desktop peers; none reaches a browser
([LiteNetLib](https://github.com/RevenantX/LiteNetLib), [Riptide](https://github.com/RiptideNetworking/Riptide)).
.NET 8 added `SocketAddress` receive overloads that remove per-packet allocations on UDP
([Enclave](https://enclave.io/high-performance-udp-sockets-net8/)). For a browser-first, allocation-free
server the field's tools add nothing a `Span<byte>` bit writer over pooled buffers does not.

## Trust

- Under server authority a cheating client changes only its own screen (Gambetta, above).
- Inputs are the whole of what a client sends: clamp them, accept at most one per tick, repeat the last on
  a gap.
- netcode.io's connect tokens — short-lived, signed by a web backend, checked by the game server — are the
  reference for admission ([Fiedler](https://gafferongames.com/post/why_cant_i_send_udp_packets_from_a_browser/)).
- A client that never receives what it cannot see cannot reveal it, whatever its code does.

## The pitfalls everyone names

- **Clock drift** — a fixed playout delay is not enough; the client's playout rate has to follow the
  server's, and Overwatch and Rocket League adjust it continuously.
- **Jitter** — packets arrive clumped; the interpolation buffer is the jitter buffer.
- **Extrapolation past ≈250 ms** snaps back visibly (Valve caps at 0.25 s).
- **Popping at the edge of view** — hysteresis, and an area of interest larger than the view.
- **The join spike** — the first full snapshot is the largest message; spread it through the priority
  budget.
- **Loss** — never resend a snapshot; a delta against the acknowledged baseline heals itself.
- **An overloaded server** — EVE's time dilation slows the game clock for everyone in fine steps
  ([EVE — TiDi](https://www.eveonline.com/news/view/introducing-time-dilation-tidi)) rather than letting
  queues grow; snapshots stamped with simulation time make that free for the client.
