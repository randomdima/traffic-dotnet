# Physics — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md) and
[solver.md](solver.md).

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
