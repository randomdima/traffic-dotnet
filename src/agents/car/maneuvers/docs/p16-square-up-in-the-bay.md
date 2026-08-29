# `P-16` — square up in the bay

Code: [P16SquareUpInTheBay.cs](../planned/P16SquareUpInTheBay.cs) · [catalogue](index.md)

**Scenario.** A park attempt did not deliver its `Sb` — the car is in or beside the bay at the wrong angle,
or jammed on the way in — and MAN-6 forbids trying again from the pose the failed attempt ended in.

**`Sa` — the state it starts in.** At the bay this leg holds (or standing in one), that bay has a way, and
the template out to where that way leaves its lane can be laid from where the car is.

**`Sb` — the state it delivers.** The car back at the mouth of the bay **on a different axis from the one
it failed on**, pointing along the lane, ready for `P-14` to lay a fresh way in.

**Line.** Two templates in sequence, and this entry drives the first: the same shape laid out to the place
on the lane the bay's own way leaves, then `P-14`'s that turns it back in. **Each is driven in the gear its
standing gives it** (`GEN-4j`) — out in reverse where the car is nose-first, out under power where it
backed in — so a square-up is the same shape either way round and never the same gear.

**Do.** Drive out on that template. When it is spent, hand to `P-14`, which lays its own way in from the
new pose.

**Guards.** The line stays a template.

**Bounds.** **One attempt.** A second square-up from a pose the first one chose is exactly the failure the
rule below forbids, and the ladder carries on from there.

**Exits.**

| | Successor |
|---|---|
| the way out is driven | `P-14` |
| the line is no longer a template | `P-4` (failure) |
| `P-14` then refuses its own geometry | `E-6` |

**The rule this entry exists for.** **Two poses on one line cannot rotate a car.** Any "back up and try
again" that leaves the car on the axis it failed on reproduces the failure to the degree, because two
poses on one line are joined by a straight, a straight has no curvature, and a car driven along one
arrives at exactly the angle it left at. This is the **only** place in the catalogue that needs a
procedure for it: everywhere else the retry needs no manoeuvre of its own, because `P-4` draws its line
from the pose the car actually ended up in — a car standing off the lane at an angle gets an arc onto the
line, not a straight down it.

**Why it is not scheduled.** Steering to a pose.

**Refs.** MAN-6, CAR-4a, CAR-6.5, GEN-4j.
