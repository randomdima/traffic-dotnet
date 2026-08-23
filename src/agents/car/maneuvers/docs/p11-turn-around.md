# `P-11` — turn around inside a junction

Code: [P11TurnAround.cs](../planned/P11TurnAround.cs) · [catalogue](index.md)

**Scenario.** The route reverses direction of travel — the destination is back the way the car came. A
junction is the **only** place on the network where that is allowed.

**`Sa` — the state it starts in.** The next lane the leg takes is the reverse of the one under the car,
the box ahead is this car's, and the counter-swing fits the ground it would be driven over.

**`Sb` — the state it delivers.** The body on the opposite lane's line, pointing the other way, with the
route picked up from there.

**Line.** The **counter-swing**: two arcs, not one. The car turns first *away* from the lane it is heading
for by an angle, then sweeps back through it. A plain half-circle needs a junction as wide as twice the
turning radius; the counter-swing lands the same lane separation on minimum-radius arcs, and pays for it
by reaching further along the arm.

**Do.** Hold the claim as for `P-8` — this occupies a junction for longer than anything else in the
catalogue — and drive both arcs at manoeuvring pace throughout, because the whole shape is at or near
full lock and there is nothing in hand for a correction.

**Guards.** The line stays a template. The ground under the whole shape was walked before it was laid.

**Bounds.** The blocked clock rather than the short fuse: the shape is across a lane by construction, and
timing it as though standing there were a fault would escalate a manoeuvre that is simply long.

**Commit.** The start of the first arc. There is no aborting a turn-around halfway; a car stopped
mid-shape is standing across the junction — which is also why, when this one does jam, `E-3` is genuinely
useful, since this is the one road manoeuvre where a reverse segment turns an impossible sweep into a
possible one.

**Exits.**

| | Successor |
|---|---|
| the shape is driven out | `P-4` |
| the line is no longer a template | `P-4` (failure) |
| the geometry is refused on the way in | the ladder, or `E-6` — see below |

**Why a refusal is not a reroute.** A junction that cannot hold the shape is not a road problem. The
route asked for a turn-around only because the goal is back the way the car came, and every replan comes
back with the same answer. What changes the problem is the **destination** — a bay on this side of the
road needs no turn-around at all — so a refused `Sa` goes to `E-6`, and where the leg has already spent
that, the honest end is `E-9`.

**Whether it is reachable.** It depends on the town. The shape's reach along the arm is the constraint,
not the lateral one, and it is checked against the terrain rather than against a table of junction sizes:
a wide junction or a square admits it and a six-metre crossroads does not. `--bench maneuvers` reports
whether anything reached it on the shipped maps, which is the honest way to answer a question about a
town rather than about a car.

**Why it is not scheduled.** The one junction movement that sweeps the whole box and crosses the other
stream throughout.

**Refs.** CAR-4a, CAR-6.5, MAN-2, S-4.
