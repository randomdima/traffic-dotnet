# Multiplayer — index

A plan for a multiplayer town: **one authoritative server runs the whole town as today; thin browser
clients each drive one car, see a limited area round it, and get no instruments.** Nothing here is built
yet, and nothing here is a requirement — the rules this feature owns are written, with their rungs, when
its slice is.

| Document | Holds |
|---|---|
| [research.md](research.md) | How the field does it: architectures, prediction, interest management, encoding, browser transports in 2026, trust, pitfalls — with sources |
| [fit.md](fit.md) | What this engine already gives, what it lacks, what the two rules ask of a network layer, which documents it contradicts, and a bandwidth estimate |
| [options.md](options.md) | Ten axes, the options on each, and the MVP's pick with its reason |
| [mvp-plan.md](mvp-plan.md) | The plan: the owner's decisions first, architecture, flows, phases, risks, test intentions, open decisions, what comes after |

Read in that order to see why; read [mvp-plan.md](mvp-plan.md) alone to see what.

## The MVP in one paragraph

Snapshot interpolation from an authoritative server — no lockstep, no determinism asked. The server is the
desktop executable in a headless serve mode on the managed world, with N seats generalising today's single
`HandOnCar`, each on an idle town car it takes and gives back (CTL-4). Every third tick it captures what is
drawn of the town once, and for each client writes a delta against the client's acknowledged baseline of
what lies in a fixed square round its car, quantised and bit-packed, over a binary WebSocket the server
serves with no framework. The page is the existing web head opened with `?join=`: it lays the static town
from the map and seed itself, computes signals, clouds and lamp flashing from the server's tick, receives
through the `Pump` it already makes, interpolates a fixed delay behind the server, and draws through a
drawn-state seam the moving fill is refactored onto — with the camera locked on its car and only a player's
read-out on screen. Own-car prediction, datagrams and larger cities come after, in the order the
measurements ask for them.

## Where it stands (2026-10-30)

The owner's decisions are taken and written into [goals.md](../docs/goals.md) and the
[decision log](../docs/decision-log.md); the rules are [MP-1…10](../src/multiplayer/docs/requirements.md), `MP-1` at
`P0`. Phases 1–6 are built and phase 7, measuring, is not:

- **Seats** — `TownWorld.HandOnCar(seat, car, input)` over `NamedHands`, and `MayBeSeated` (MP-4).
- **The wire** — `src/multiplayer/wire/`: bit packer, `MoverState`, `SnapshotCodec`, messages, `ReceivedSnapshots`.
- **The server** — `src/multiplayer/server/`, run as `--serve [--map M] [--port N] [--page DIR]`: capture, area,
  players, the WebSocket handshake over `TcpListener`, and the published page handed out on the same port, a bare
  address sent on to `?join=` itself (MP-11).
- **The client** — `src/multiplayer/client/` and `Game.Joined.cs`, opened as `?join=ws://host:port`: the link rides
  the page's pump (`ServerLink`, `town.js`), the replica feeds `TownSprites.FillMoving(in MovingScene …)`.

Three departures from [mvp-plan.md](mvp-plan.md), each for the simpler road: the drawn-state seam carries the fleets
themselves rather than a `CarRow`-like record; the car is named by every snapshot rather than by the welcome, since it
is chosen only once the player has laid the town; and a page still lays the whole town to draw its statics. What is
left undone is in [tech-debt.md](../docs/tech-debt.md).

## Before any of it

Multiplayer contradicts [goals.md](../docs/goals.md) twice — *not a game*, and *the unmanaged boundary is
the driver and the window* — and rewords three web rules. Those are the
[owner's decisions](mvp-plan.md#decisions-the-owner-makes-first), and they come first.

When the slice exists, these documents move into its `docs/` and this folder goes.
