# The kernel — decision log

## 2026-08-21 — an angle becomes a direction in one place, and a series covers the sinc

Trigonometry was 18 % of the tick, nearly all of it two shapes written out by hand across twenty sites so
that no single one looked expensive. Both are now `Heading`, where the pair costs one `sincos` (3.11 ns
against 4.47). `ArcSeg.Sinc` guarded `sin x / x` far below any arc a road subtends, so the first four
series terms answer under 0.6 rad at 0.58 ns against 2.36, and the library still answers above it.

## 2026-08-21 — a network is indexed because it cannot change, not because the scan was slow

`NearestEdge` and `NearestLane` scanned every stretch in the town — 7 % of the run on Odesa. Both answer
from `ChainIndex`, which is safe only because neither graph is ever written to after it is laid: an index
that never has to be maintained cannot drift. The index decides what is looked at and never what is
chosen, so ties still go to the lower id and `ChainIndexTests` asserts against the scan it replaced.

## 2026-08-16 — agents stopped thinking every tick, and the clock is in seconds

Every agent re-ran its whole procedure at 60 Hz while 96 % of cars on a jammed town stood still. Agents
now run their catalogue every `AgentDecisionIntervalS`, staggered by index. The interval is stated in
seconds because what it bounds is how far the world moves under a stale answer, and it is a floor rather
than a ceiling — a manoeuvre steering to a pose is a closed loop, and running one at a sixth of the rate
converges at a sixth of the rate, which was measured rather than reasoned. At an interval of 0 the town
must equal the un-clocked one exactly.
