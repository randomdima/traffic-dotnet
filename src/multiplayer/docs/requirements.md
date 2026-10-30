# Multiplayer — requirements

One town served to several players: the server runs it exactly as a watched run does, and each player is one person of
it — on foot, or in whatever it has got into — sees only the ground round itself and is sent nothing else. The survey, the options and the plan this slice
was built from are [network/](../../../network/index.md); what a player's hands are is
[app/playercontrol](../../app/playercontrol/docs/requirements.md) (`CTL-5d`).

The slice is three parts at three tiers: `wire/` moves numbers and knows `core/` alone; `server/` and `client/` are
the shell's, and the server's half is the desktop's only.

**MP-1** `P0` **A player is never sent anything outside its area.** No message to a player names, places or describes
a moving thing that is not inside the area round the player — its body, or the car or train it is in — and that area
is the server's to decide from where the player stands — a player names no area, no zoom and no unit. A player that
cannot be sent its area is sent nothing.

**MP-2** `P6` **The area is a square round the player, wider than anything it may look at.** Its half-side is
half the widest view a player may zoom out to (`WidestViewM`), the furthest its camera stands in front of what it
chases and the furthest it may be dragged off it (MP-9), and a margin — what the fastest car covers in the time a
player draws behind the server and one snapshot more — so nothing enters a view without having been sent first. A
mover is taken up inside the square and let go of only once it is that margin further out, so one at the edge does not
come and go.

**MP-3** `P2` **The server is the only authority.** A player sends the keys it holds — throttle, wheel, handbrake —
the count of its presses to get in or out, and the newest snapshot it holds, and nothing else; the server reads them as
a hand on whatever the player's body is in (`CTL-5d`), or walking the body on foot north-up, and through nothing but
that seam. A player's keys are clamped to what a key could have produced, and a player is answered at most once a tick.
A player sends only when its keys, its presses or its newest snapshot have changed, never more often than the server
ticks, and at least every `InputKeepAliveS` while nothing has.

**MP-4** `P5` **A player is a person of the town, stood up new on the pavement.** A served town lays `BodiesPerSeat`
bodies for each of its seats, offstage, and a player admitted is one of its seat's put down on the pavement nearest the
town's middle. The body is drawn, struck, rescued and treated as anybody is and decides nothing: the player's keys are
its whole behaviour. **A player down** — knocked down, or thrown from a wreck (`PHY-6`) — leaves that body to the town
and after `RespawnS` is stood up afresh as another of its seat's, off the door of the hospital nearest where it fell, or
at the town's middle in a town with none; a body treated comes back offstage, and a seat whose every body is in the
town's care waits for one. Letting the player go — a closed connection, or `ReleaseAfterSilenceS` with nothing heard
— takes its body offstage and hands what it drove back to its own driving; `HoldAfterSilenceS` with nothing heard holds
it braked first.

**MP-5** `P6` **The static town is never sent.** A player lays the town itself off the map and figures the server
names, and refuses a server whose plan it does not lay to the same digest (`CityPlan.Digest`) — a page and a server of
different builds draw different towns, and saying so is the only honest answer.

**MP-6** `P6` **What is a function of the tick is never sent.** The signals' phases, the clouds, the wind, the water
and the fans are worked out by the player at the tick it draws, from the same seeds the server laid them from, so a
light turning and the car stopping for it are drawn at one instant.

**MP-7** `P6` **A player draws the town a fixed delay behind the server.** It estimates the server's tick from the
snapshots' arrival, draws `InterpolationDelayS` behind it, and places each mover between the two snapshots either
side of that instant. Past its newest snapshot it carries each mover on at its last speed for at most
`LongestExtrapolationS`, then holds the picture. Its estimate follows a server that slows (`SIM-9`) rather than
running ahead of it. **Its own keys are shown at once on what it is** — its car's road wheels and brake and reversing
lamps, the way its body on foot faces while a key is held — worked out by the server's own hand from the keys held now;
where it stands, and everything else, is the server's as drawn.

**MP-8** `P2` **Serving a town costs the tick nothing it can count.** What the thread stepping the town does for its
players — reading their keys and laying down what was drawn for the snapshot — allocates nothing and crosses nothing
(goals.md, the two rules); a snapshot is encoded and sent on the network thread, one send to a player, so the town's
sockets cost one send and one receive a player per snapshot whatever the size of the town.

**MP-9** `P7` **A player's page carries no instrument.** It builds no debug layer, no menu debug page, no status
breakdown, no ruler, no selection, no orders, no pace and no map choice; the player — its body, or what it drives — wears the
selection's ring, in its own colour (MP-15), and nothing else wears one. **Its camera is the page's own**: it chases
the player and nothing else, a lead in front of it, settling smoothly rather than carrying every step of a body
drawn off snapshots onto the town (`Chase`). The wheel zooms it in as far as a reader's camera goes and out to
`WidestViewM` along the screen's long side; a drag moves it up to `MostPanM` off the player and, let go, it comes back. It is
never turned.

**MP-12** `P8` **A player's page carries a map of the town round the player.** North up, in the screen's lower left, it
shows `MapSpanM` of the town as roads and water only, laid off the plan the player already lays (MP-5) and drawn round
the player. `M` or a click on it opens the whole town, fitted to the screen, over the dimmed picture; `M`,
`Escape` or a click shuts it, and while it is open the camera is neither dragged nor zoomed. Each player is an arrow in
its own colour (MP-15), the same on every player's map: the player's own, and every other player in the player's area —
on foot, or what it drives — each other one named beside its arrow. A player is told which mover in its area a seat is,
and who that seat is, and nothing more, so a player outside the area is not on the map (MP-1).

**MP-13** `P7` **`E` or `Enter` gets a player into the nearest vehicle, and out again at once.** On foot, a press gets
it into whichever car or train has its body nearest the player's within `BoardReachM` — at a car's wheel, at a train's
controls — and **whoever drove it is put out**, a town's driver or another player alike; a town's driver walks off on a
trip drawn afresh, and a vehicle's crew and stretcher stay aboard. Any car that is whole and off a tractor's arm may be
taken, a service vehicle's errand waiting under the hand as it does under any (`CTL-5d`). In a vehicle, a press puts
the player out beside it **at once**, moving or not, and what it drove is let go of where it is. Nobody is ever kept in:
where the pavement beside the vehicle has no free spot the body is put down off its door (`PHY-7a`).

**MP-14** `P6` **A player is never sent more than `MostSnapshotsUnacknowledged` past the newest snapshot it has
acknowledged.** Past that it is sent nothing until it acknowledges again — a page nobody is looking at, or one on a line
slower than its feed, is not queued snapshots it cannot read — and the baseline it then acknowledges is still held on
both sides, so it comes back to a difference and not a whole area.

**MP-10** `P3` **Every message is read as untrusted.** A field is range-checked as it is read, a message longer than
`LargestInboundBytes` or one that does not read to its end cleanly is a connection closed, and nothing a player sends
grows a buffer or throws past the connection it came on.

**MP-11** `P9` **A served town hands out its own page.** The port players join on also answers a browser asking for the
published page, file for file and nothing outside it (`PageHost`), each file marked as the town's, and a page so marked
opens on the lobby (MP-15) joining that very host — so one address is the whole of what a player is given, and
nothing is added to it. A town with no page published
answers players alone, and they join from a page served anywhere else.

**MP-15** `P8` **A player says who it is, and is drawn as it said.** The lobby (`lobby.js`, in the page's own place) asks a
name — offered at random, two words — a colour and the server's address, filled with the page's own host, and joins on
one button, handing all three to the engine rather than to the address. The hello carries the name, printable ASCII of at most `SeatCard.LongestName`, and the colour, which the server
raises to at least `SeatLeastSaturation` and `SeatLeastValue` so no player is a grey or white that reads as a road. A
player is told a seat's name and colour in the first snapshot whose area holds that seat's mover, and again once the
seat changes hands — never of a seat outside it (MP-1). It draws each other player's name over what that player is drawn
as and beside its arrow on the map (MP-12), both in that player's colour, and its own ring in its own (MP-9); its own name
is never written.
