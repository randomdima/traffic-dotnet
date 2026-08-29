# Physics — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md) and
[solver.md](solver.md).

## 2026-08-28 — a casualty stops being something to push around

`PHY-5` said a body in the road keeps its collision shape and can still be pushed, and what that bought
was a body shoved down the street by everything that came near it: walkers on the pavement barged it
along, the ambulance sent to fetch it nudged it out from under its own paramedic, and a crew towing it
back to the vehicle dragged it through the queue. **The impact is the interesting part and everything
after it was noise**, so `PHY-5b` cuts it off at the impact: the arbiter judges the contact after the step
that already spent its impulse, and the body goes onto a layer nothing scans on the same tick.

**A layer and not a flag.** The filter is two words on the body table and the broad phase already reads
them for every pair; a `Wounded` bit tested in `Gather` would have been a branch per candidate pair in the
town's hottest loop, to say what the mask says for free. It also keeps `CollisionLayers`' one rule intact —
who scans whom is still read off the table rather than decided by running code.

**Statics are kept in the row**, which is the one place it is less than "collides with nothing". A body
that slid through a wall would be the thing `SIM-1` exists to stop, and a corpse resting against a kerb
costs a manifold nothing else was going to use. Nothing comes of the contact: `PHY-4a` already makes a
person against static geometry harmless at any energy.

**The road's book was left alone.** A casualty still holds the stretch of lane under it and traffic still
queues behind it (`LaneOccupancy.AnybodyCrossing`), so in an ordinary town no car ever reaches one — what
changed is what happens when something does, and what the rescue itself is able to do without shoving its
own patient down the road.

## 2026-08-26 — the person's tolerance is a distance, and a wreck puts its driver on the road

Two changes to `PHY-3`'s neighbourhood, both of which move a number out of the config and into a relation.

**The band is authored as half a metre of slide.** `PersonShakeKj = 2` and `PersonFatalKj = 6` were
kilojoules nobody could picture; what is authored now is `SlideToCasualtyM`, and the energy is the work of
putting a body that far along the ground on its own sliding grip — `mass × grip × distance`, 0.16 kJ,
which is a car meeting somebody at 2 m/s. **The reduced mass makes it honest rather than exact**: a car is
seventeen times a person, not infinite, so a contact at the tolerance leaves 95% of the closing speed in
the body and 95% of the half metre. That is stated on the derived figure rather than rounded away.

It cost the staged cases their approach. `CrashProbe` gave every pair four tenths of a second of clear
air, and a coasting car sheds about 1.35 m/s² through its own tyres — 8% of the speed that breaks a car
and 27% of the speed that knocks somebody over, so two of the three person cases arrived in the band below
the one they were staged for. The gap is now three ticks at any speed, which is what it was always meant
to be: room to be seen coming, not a run-up.

**`PHY-6` stopped saying "unaffected".** The driver of a car that breaks is put out beside their own door
as a casualty. What made it worth writing down is that it cannot go through the alighting path: `PHY-7a`
searches for clear ground and *waits* while there is none, and a driver still inside a wreck when the
search fails is a casualty no ambulance will ever reach (`AMB-7`). So the body is placed and the solver
sorts out what it landed on — which is what `PHY-9` is for. The ambulance's own crew is not exempt: a
wrecked ambulance now leaves its crew on the road beside the stretcher case it was carrying, and the
stretcher case comes out of the back rather than out of the door so the two do not land on each other.

## 2026-08-26 — the circle stopped being a shape

`SOL-1` said two shapes and now says one. A disc is a rounded box with no core, so `ShapeKind` and the
array behind it are gone: a body carries half-extents and a radius, and a person and a prop set the first
to nothing. Five kinds of body, one shape, one narrow phase.

`Collide` still has three branches and they are **not** three shapes: the general path answers a disc
pair and a disc-against-box pair correctly on its own, and the two closed forms are kept because the
pairs they cover are most of a town's narrow phase — a walker against a walker, and a car against ninety
thousand props. The general path costs a separating axis and up to thirty-two point-and-segment tests
where a disc pair costs a subtraction and a square root. `TheDiscShortcutsAgreeWithTheGeneralShape` is
what stops that being three implementations to keep in step: it gives the general path a tenth of a
millimetre of core — nothing being exactly what selects the shortcut — and holds the two answers together
over forty thousand pairs.

The one place the unified shape is honestly wrong is rotational inertia: a coreless shape's own figure is
`r²/2` and the rounded box's is `2r²/3`. It is unreachable, because every coreless body in this town is
rotation-locked, and it is said on the method rather than worked around.

## 2026-08-26 — a car is collided as its picture, not as the rectangle the picture was drawn in

The rounded corner below fixed the corner and left the flanks, and the flanks were the bigger half: a
footprint is the rectangle art is drawn *into*, so its width is whatever reaches furthest — on the police
car, the wing mirrors, a fifth of a metre outside the panels all the way down the car.

So the shape is now **fitted inside the picture** rather than derived from the outline around it: the
largest rounded box lying within the silhouette, measured off the alpha and authored beside the footprint
as `collisionM`. It takes 1–9% off the length and 5–20% off the width, and by construction it never
stands outside the bodywork at all.

That retires the hull as a source. `hullM` traces the outline *including* the mirrors, which is exactly
the figure that was wrong; it goes back to being authored art that no code reads, which is what its file
comment said before the radius was briefly derived from it.

**Two authoring mistakes are worth recording, because both produced a plausible answer.** The fit eroded
the silhouette by a disc in a picture squashed to a square, so the disc was an ellipse in metres and the
radius was only right on one axis. And it was run at a sixth of the art's resolution, where a notch in
one car's nose closed up. Both were caught by the gate rather than by looking, which is the argument for
the gate asking the *art* and not another figure in the same file.

## 2026-08-26 — the box got a corner radius, and the town did not get polygons

Measured against the shipped art, a car's square footprint corner reaches **0.21 m to 0.43 m past the
bodywork drawn under it** — worst on the police car and the APC — and three to sixteen per cent of a
car's perimeter carries more than 0.3 m of empty box. That is a car stopped by a car it visibly is not
touching, which is the complaint this answers.

Convex hulls were the obvious fix and are already authored: every variant file carries a ten-point
outline. They were refused. The separating axis over a ten-gon pair is twenty axes against twenty
projections where a box pair is four against eight — an order of magnitude on the narrow phase, against
a solver step that is already 0.277 ms of a 1.768 ms tick on Odesa — and they would have taken `SOL-1`,
the solver's own exclusion table and `OBJ-2` with them. What they buy over a rounded box is the last few
centimetres of one car.

**A radius on the box costs nothing and takes most of it.** The separating axis, the reference face and
the clip all run on the pair's cores exactly as they did; the two radii widen the margin going in and
come off the separations coming out. Two things had to be got right and neither was obvious:

- **The choice between the face clip and the nearest point is the cores' own.** Asked of the rounded
  shapes it sends pairs that overlap by a hand's depth — but whose cores stand a whole radius apart —
  into a clip that has nothing to clip, and they come back as *missed*.
- **The separating axis can only ever rule a pair out.** It understates how far apart two disjoint boxes
  are, corner to corner by most, so the margin has to be enforced again on the real distance once
  `Nearest` has found it. That hole predates the radius; it was rare enough at 20 mm of speculative
  distance to survive one random stream and not another, and a radius widened it until it showed.

Box2D v3 carries a radius on a polygon in the same terms, so the difference tests cover the rounded box
against the incumbent over forty thousand poses per pair type and cost nothing to extend.

## 2026-08-21 — going into a container no longer marks the moving index stale

`Contain` cleared a body's `Enabled` bit and marked the broad-phase index stale, so the next query to
ask anything of the world rebuilt it. The queries are headway rays, and they are cast in phase 3 — so a
walker getting into a car halfway down the roster cost the town its whole moving grid over again, in
the middle of the phase, for a body that had merely had a bit cleared.

Nothing needed the rebuild. **Every reader of that index already tests the bit** — the broad phase, the
ray sweep and the moving roster all skip a body that is not enabled — so the entry left behind is
filtered rather than found, and the index is not wrong, only stale in a way nothing can observe.

One thing did need it and was missed at first: `IntegratedBodyCount`. The rebuild was retaking the
census as a side effect, so dropping it left the count reading one too many until the next step, which
`SolverBehaviourTests` caught. The count is now kept where it changes.

**Coming back out still marks it**, and that asymmetry is the point: a released body stands somewhere
new, and an index that was not told would answer about where it went in.

## 2026-08-19 — the solver stopped being a package

The physics was the Box2D.NET package first, and taking it was defensible: it is pure C# with no native
asset, so it was not a way around the thing being measured. Three things cost more than it saved.

- **A step allocated several hundred bytes.** `SOL-20` is kept everywhere else in the project, and a rule
  of that kind cannot be kept *nearly*.
- **The port's own layout was the mistake.** Box2D v3's speed is its data-oriented arrays; the port turned
  its simulation records into classes, so the inner loop chased pointers to scattered heap objects — the
  exact shape of C# this project exists to stop being confused with C#.
- It ran a general soft-step solver over a tree where the town wanted a bespoke one over uniform grids of
  static geometry.

`world/physics/` is now this project's own broad phase, narrow phase and contact solver: **five times
cheaper a step, and it allocates nothing.**

**Box2D.NET is still referenced — by the unit suite alone**, as the independent implementation the cast
and the manifolds are checked against over randomised poses. That is worth more than the code it
replaced: it settled `SOL-19` against what this slice had written down, and it found a reference-face
disagreement neither implementation would have found alone.

## Undated — the overlap is pushed out on an accumulator that is thrown away

`SOL-12` asks that an overlap be pushed out without the push becoming motion, and the positional term is
kept on a second accumulator that is discarded rather than folded into the body's real velocity.

A correction folded into the real velocity is **energy the collision never had**: a resting pair breathes
against each other, a queue jitters, and the damage arbiter reads a closing speed that nothing caused.
The accumulator carrying no momentum is also why the position solve converges in far fewer iterations
than the velocity solve.
