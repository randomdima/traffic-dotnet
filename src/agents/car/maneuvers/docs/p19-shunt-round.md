# `P-19` — shunt round

Code: [P19ShuntRound.cs](../planned/P19ShuntRound.cs) · [catalogue](index.md)

**Scenario.** The leg has to come back the way it came, and there is no bay to turn in: the road runs out
here. The car works itself round on the spot, a leg of the turn at a time, forwards and back at full lock —
what a driver does at the end of a cul-de-sac.

**`Sa` — the state it starts in.** At rest, on a stretch this leg comes back the other way from (`TER-5f`),
not round yet, and a leg of the turn the ground and the book both admit. **It is entered part-way round as
readily as at the start**: the entry state is the pose, so a car whose turn was interrupted by a reflex
(§1.6) asks for the leg that suits the pose it is in rather than beginning again.

**`Sb` — the state it delivers.** The body on the opposite lane's line, pointing the other way, with the
route picked up from there.

**Line.** One arc a leg, at this car's own lock, as long as the ground and the book will hold and never
further round than one sweep. **The wheel goes the same way in every leg and the gear alternates**, which
is the whole of what turns a car round rather than rocking it on one spot; and the way round is the way the
middle of the road lies, settled once — half a turn is as near one way round as the other, so an answer
re-read each leg flips on the first degree of the first one. The last leg is not a sweep but a **line-up**:
a join from the pose the sweeping left the car in onto the lane it is now pointing along, because a car
handed a line it is standing five metres off is a car the follower calls lost on the first tick.

**Do.** Drive each leg out, look at where that leaves the body, lay the next.

**Guards.** Every leg is walked over the ground and asked of the book before it is driven, in the gear it
is driven in. **A leg that will not lay is waited on and never escalated** — what refuses one is the ground
being somebody else's, which is a fact about this moment and not about this dead end.

**Bounds.** A clock of its own, and it has to be: a car shunting is a car *moving*, and every other
watchdog in the town measures stillness. Past it the ladder.

**Commit.** Nothing. Each leg is a metre or two and the car is legally stopped between them, so there is no
point of no return to name — which is exactly why this is the shape a dead end gets and a junction does
not.

**Exits.**

| | Successor |
|---|---|
| round, and standing on the lane it is about to be given | `P-4` |
| the line was lost | `P-4` (failure) |
| the clock ran out | the ladder |

**Why not at a junction.** Because a junction has arms and the router can use them: three sides of a block
cost less than this and take nobody else's ground for as long. What this is for is the place with no arms —
a dead end, the one intersection a town sizes around a turning circle (`TER-5a`) — and the place a leg's
own car park has nothing free (`GEN-4l`).

**Why the town promises the room.** It is a fact about a map and not about a car: a dead end too small to
turn in is a place nothing that drives into it can leave, which is why `TER-5a` is the one junction sized
around a car at all. `--bench maneuvers` reports whether anything reached this entry on the shipped maps,
which is the honest way to answer a question about a town rather than about a driver.

**Refs.** CAR-6.5, CAR-10b, CAR-11, MAN-4, TER-5a, TER-5f, GEN-4l, S-6.
