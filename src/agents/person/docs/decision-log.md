# Decision log — the walker

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-20 — the walking is one line and two claims, and everything else was put down

The walking side had grown a second traffic model: an ask and a grant along the pavement's ways, a
permission read off the grant, a body picked out of it to be stepped round, an offset all three halves of
the step had to agree on, a kerb with a signal and a patience and an escape from the patience, and two
rules for a walker on a map with nothing on it. Eight of its own figures, four hundred lines of claims
arithmetic and a decision log whose every entry was a correction to one of those — and the thing it was
all in aid of, *a walker gets from here to there*, was the part that kept failing.

It is now the two sentences it should have been. **PER-25**: follow the line the network laid, and walk
straight at the network when you are not on it. **PER-26**: hold the ground you are on at p0, state the
ground you are walking at at p9, and read nothing back. The pathfinding was never the problem and is
untouched — the pavement is contracted once when the town is stood up and a walk is a search over it.

**What the p9 statement buys is the crossing, and it is the ladder's own arithmetic rather than a rule of
the walker's.** A stated claim binds whatever ranks below it (TER-5g), a body on the paint outranks the
traffic under it (TER-5e), and a rescue coming through outranks that (AMB-4) — so stating the band in
front is the whole of "the traffic gives way and an ambulance does not", with no gap judged, no patience
spent and no signal read. The kerb, `KerbPatienceS`, `RedWaitSetbackM` and the standstill test went with
it.

**A walker no longer queues, and that is the change to live with.** A grant is a distance and PER-3 leaves
a walker nothing to spend one on, so two walkers wanting one piece of pavement now meet in the solver
rather than in the claims. What that costs is a shove; what it saved is every rule that existed to get a
body past another body.

**The give-up clock had to stop being the driver's.** Nothing freezes it now, so a three-second obstruction
wait — a figure sized for a car that has a ladder to climb — read an ordinary shove at a doorway as a trip
that could not be finished: over five minutes of Odesa, 505 walks given up against 130 arrived. At the
walker's own `GivesUpAfterS` it is 106.

**And a crossing whose bands are not laid is ground all the same.** The paint's projection onto the lanes
under it is an absence rather than a decision ([the known gaps](../../../../docs/index.md#known-gaps)), and
a body that claimed a band where there were none claimed nothing at all — a walker on a zebra no driver
could see. Where the crossing has no bands the ways under the body answer instead, which is TER-4c.2 and
needs nothing new.

## 2026-09-07 — the walker's action set is the code's, and `PER-2` is retired

`PER-2` listed turning, walking, idling and entering a container — the four methods, restated where they
could drift from the type that has them. What each one costs and is bounded by is `PER-3` and the
containment rules; the list itself was cited by nothing.
