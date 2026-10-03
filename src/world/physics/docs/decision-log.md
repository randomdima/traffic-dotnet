# Physics — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md) and
[solver.md](solver.md).

## 2026-10-02 — a body at rest is frozen out of the step

The owner asked for it. Profiled on O10 (6 120 cars, 6 240 walkers on the ground), the step was a third of the
CPU and the solve none of it: `FindContacts` alone was a quarter, and over half of that was owners walking
static cells — a parked car asking every tick for the trees round it. A frozen body is no owner, so what it
saves is the broad phase, and `SOL-37` took the exclusion row's "sleeping" off.

- **Per body and not per cell.** The broad phase is walked by owner, so nothing walks cells for a cell tag to
  skip; a moving body has to meet the frozen bodies in its cells to wake them; and a main cell holds a bay and
  the lane beside it, so one walker past a car park would unfreeze the lot.
- **Rest is read off the motion, never off the impulses**: a parked car's tyres correct a residue every tick, and
  a count reset by impulses never let one freeze.
- **The narrow phase takes a pair lower index first and the contacts are solved in pair order**, whichever end
  of a pair the step reached first. A town frozen only at exact rest (`RestSpeedMps` 0) gave the age probe's
  digests to the bit on Odesa and O10, which is the evidence that a frozen body is left out exactly.
- **The shipped rest speed gave the same digests too**: a standing car's tyres hold it at exactly zero
  (`TyreModel.HoldStill`), so nothing in either town rests between zero and the figure. The figure is there for
  whatever does — and a town that never needs it is the same town frozen or not.
- **Nothing frozen is walked by a step.** The awake roster is kept rather than listed — what wakes joins it at the
  next step, what freezes leaves it at the end of this one — and the frozen bodies are filed in an index of their
  own, laid again only once the bodies frozen since outnumber an eighth of it. The moving index the step lays is
  the awake and those few. On O10 the solver's step went from 1.9–2.2 ms to 1.5–2.0 ms a tick with this, the
  digests unchanged.
- **An entry of the frozen index is true while its body stands where it was filed** (`BodyFlags.FiledFrozen`), so
  a body woken and not yet moved is still found there, and the step that moves it lets it go.

Two savings came with it and change nothing a body does: the moving index is laid once a step and not twice,
and a body met in several cells is kept in the first one it shares with the query (`CellGrid.FirstShared`)
rather than marked as seen in a table the size of the town.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- `PHY-5b`: a casualty is moved by being taken aboard at an ambulance's standoff (`AMB-10`), not dragged by
  a stretcher crew.

## 2026-09-07 — how the solver is written is the project's rule, not the solver's

`SOL-27`…`SOL-34` named structure of arrays, spans, `Vector2`, one thread and no LINQ — which is
[CLAUDE.md](../../../../CLAUDE.md)'s second rule and what [goals.md](../../../../docs/goals.md) says the
requirements deliberately leave open, so this slice held the only second copy of the engineering rules and
one that contradicted them. A technique stated as a requirement is also a technique nothing may be
measured against, where `SOL-20` and `SOL-22` state relations and have a gate. Two more are retired for
pointing at `PHY-2` and `PHY-4` a second time, and four for refining `SOL-22` with nothing measuring them.
Retired: `SOL-16`, `SOL-18`, `SOL-23`…`SOL-26`, `SOL-27`…`SOL-34`.

## 2026-08-28 — a casualty stops being something to push around

`PHY-5` left a body in the road to be shoved down the street by everything near it, including the
ambulance sent to fetch it. The impact is the interesting part and everything after it was noise, so
`PHY-5b` cuts it off there: the body goes onto a layer nothing scans on the same tick. A layer and not a
flag, because the broad phase already reads the mask and a `Wounded` bit would be a branch per candidate
pair in the hottest loop. Statics are kept in the row — a body sliding through a wall is what `SIM-1`
exists to stop — and the road's claims were left alone.

## 2026-08-26 — the person's tolerance is a distance, and a wreck puts its driver on the road

`PersonShakeKj` and `PersonFatalKj` were kilojoules nobody could picture; what is authored now is
`SlideToCasualtyM` and the energy is the work of sliding a body that far on its own grip. The reduced mass
makes it honest rather than exact — a car is seventeen times a person, not infinite — and that is stated
on the derived figure. `PHY-6` stopped saying "unaffected": a wrecked car's driver is placed beside their
door as a casualty rather than sent through `PHY-7a`, which waits for clear ground and would leave a
driver inside a wreck no ambulance can reach (`AMB-7`).

## 2026-08-26 — the circle stopped being a shape

`SOL-1` said two shapes and says one: a disc is a rounded box with no core, so `ShapeKind` is gone. The
general path answers every pair and the two closed forms are kept because they cover most of a town's narrow
phase; `TheDiscShortcutsAgreeWithTheGeneralShape` holds them together over forty thousand pairs.

## 2026-08-26 — a car is collided as its picture, not as the rectangle the picture was drawn in

A footprint is the rectangle art is drawn *into*, so its width is whatever reaches furthest — the police
car's wing mirrors, a fifth of a metre outside the panels. The shape is now the largest rounded box
*within* the silhouette, measured off the alpha and authored as `collisionM`, which by construction never
stands outside the bodywork. That retires `hullM` as a source, since it traces the mirrors too. Two
authoring mistakes both produced plausible answers — an ellipse in metres from eroding a squashed picture,
and a notch closing up at a sixth resolution — and the gate caught both, which is the argument for asking
the art rather than another figure in the same file.

## 2026-08-26 — the box got a corner radius, and the town did not get polygons

A square footprint corner reaches 0.21–0.43 m past the bodywork, which is a car stopped by a car it
visibly is not touching. Convex hulls are already authored and were refused: twenty axes against twenty
projections where a box pair is four against eight, on a solver step already 0.277 ms of a 1.768 ms tick,
and they would have taken `SOL-1` and `OBJ-2` with them. A radius costs nothing and takes most of it. Two
things had to be right: the face-clip choice is the *cores'*, or pairs overlapping by a hand's depth come
back as missed; and the separating axis can only rule a pair out, so the margin is enforced again on the
real distance.

## 2026-08-21 — going into a container no longer marks the moving index stale

`Contain` rebuilt the whole moving grid mid-phase for a body that had merely had a bit cleared; every reader
already tests the bit, so the stale entry is filtered rather than found. Coming back out still marks it.

## 2026-08-19 — the solver stopped being a package

Box2D.NET was defensible — pure C#, no native asset — and cost more than it saved: a step allocated
several hundred bytes, against a `SOL-20` that cannot be kept *nearly*; the port turned v3's
data-oriented arrays into classes, which is the exact shape of C# this project exists to stop being
confused with; and it ran a general soft-step solver where the town wanted a bespoke one. This slice's own
is five times cheaper a step and allocates nothing. Box2D.NET is still referenced by the unit suite alone,
as the independent implementation the manifolds are checked against — it settled `SOL-19` and found a
reference-face disagreement neither implementation would have found alone.

## Undated — the overlap is pushed out on an accumulator that is thrown away

`SOL-12`'s positional term is kept on a second accumulator that is discarded rather than folded into the
body's real velocity, because a correction folded in is energy the collision never had: a resting pair
breathes, a queue jitters, and the damage arbiter reads a closing speed nothing caused. Carrying no
momentum is also why the position solve converges in far fewer iterations.
