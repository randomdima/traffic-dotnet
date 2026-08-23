# The kernel — decision log

## 2026-08-21 — an angle becomes a direction in one place, and a series covers the sinc

Trigonometry was 18 % of the tick's cycles. Nearly all of it was two shapes repeated across twenty
sites: `new Vector2(MathF.Cos(x), MathF.Sin(x))`, which reduces one argument twice, and a `Right`
property beside a `Direction` property, so that a caller wanting the pair paid four library calls for
one angle.

Both are now `Heading`. The pair costs one `sincos` — measured at 3.11 ns against 4.47 — and the
quarter turn is taken off the vector, because a caller holding the direction already holds the answer.

`ArcSeg.Sinc` is the other half. It guarded `sin x / x` at 10⁻⁴, which is so far below any half-turn a
road arc subtends that every sample in the tick took the library path for a value the first four terms
of the series give exactly: the worst error under 0.6 rad is 9.1 × 10⁻⁸, inside one ulp near 1, and it
costs 0.58 ns against 2.36. Above the limit the library still answers, because the series is only
exact where it is truncated tightly.

**What made this worth doing at all was that it is one edit and not twenty.** The same two shapes had
been written out by hand everywhere, so no single site looked expensive.

## 2026-08-21 — a network is indexed because it cannot change, not because the scan was slow

`FootGraph.NearestEdge` and `RoadGraph.NearestLane` scanned every stretch and every lane in the town,
projecting a point onto each. The road one carried a note saying that was deliberate because only a car
being stood up asks — which had stopped being true: a walk asks for one at each end of every leg, and a
car that has lost its line reacquires through the other. On Odesa's 2 812 stretches it was 7 % of the
whole run.

Both now answer from `ChainIndex`. The rule that made it safe to add is that **neither graph is ever
written to after it is laid**, so an index of one cannot drift from it — which is the objection the old
note was really making, and it only applies to an index that has to be maintained.

**The index decides what is looked at and never what is chosen.** The survivors are put in ascending
order and measured by the arithmetic the scan used, so a tie still goes to the lower id; the ring is
grown until the best distance found fits inside the ring already searched, which is what makes a chain
outside it provably not nearer. `ChainIndexTests` asserts the answer against the scan it replaced, at
three cell sizes, including points off the far corner of the grid — and Odesa's soak row came out
identical afterwards, which is the check that mattered.

## 2026-08-16 — agents stopped thinking every tick, and the clock is in seconds

Every agent decided at 60 Hz, re-running its whole procedure — the manoeuvre, the watchdog, the limits,
the junction booking — whatever it was doing. On a jammed town 96 % of cars stand still, and each was
still doing all of it sixty times a second.

Agents now run their catalogue every `AgentDecisionIntervalS`, staggered by their own index. Two things
about the clock cost something to learn:

- **It is stated in seconds, never in ticks**, because what it bounds is how far the world moves under a
  stale answer — a tenth of a second is about a metre at town speed, against a 13.8 m mean following
  distance. In ticks the same figure means something different the day the timestep changes.
- **It is a floor on the rate, never a ceiling.** A manoeuvre **steering to a pose** is a closed loop on
  an error, and running a control loop at a sixth of the rate makes it converge at a sixth of the rate —
  braking to a line, creeping over paint, backing off and squaring up in a bay are all that shape. This
  one is counter-intuitive and was measured rather than reasoned.

The equivalence test is what keeps it honest: at an interval of 0 the clock hands back the tick's own
delta, every agent thinks every tick, and the town must be the un-clocked one exactly.
