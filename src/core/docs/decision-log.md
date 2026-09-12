# The kernel — decision log

## 2026-09-11 — an extrusion is a distance rule, not a pile of offset pieces

`Spline.OffsetInto` moves a chain piece by piece and asks nothing about the rest of it, which is right for a
lane inside its own road and is not a boundary: a ring's corner turned the other way sweeps the offset
through the ring and comes back as a lap standing inside the shape, and two sides of a gap narrower than
twice the offset each lay a line past the other. `Extrusion` keeps one rule instead — no point of the answer
stands nearer the ring than the offset — asked of stations along the line rather than solved between pieces.
Solving it properly means pairing up which of the offset's own crossings bound the answer, which needs a
case for a fold inside a fold; the distance rule needs none and says the same thing about a ring of ten
pieces and one of ten thousand. What it costs is that the fold closes within a station of where it really
does, and the station is a quarter of a metre.

Smoothing is a window over the line's own length and not a corner fillet. Half of what an extrusion has to
smooth is not a corner: it is the notch left where a fold was dropped, and a fillet leaves one exactly as it
found it.

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
