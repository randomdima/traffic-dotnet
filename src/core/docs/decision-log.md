# The kernel — decision log

## 2026-09-12 — the closure's veto is load-bearing, and a bare straight is the wrong thing to count

Three things were tried on the fold closure past the corrector, and measuring them established something
worth more than any of them: **counting the gaps closed with a bare straight is a poor measure of the line
that comes out.** What matters is how many stations of the laid ring stand off the distance that struck
them, and whether each of those is a station of the walk or the middle of a long piece — which the probe now
says. Against that measure, two of the three made the ring worse while making the straights look better.

**Tracing the gap from its other end as well.** A trace is a walk and a walk has a direction, so a gap the
forward trace gives up on is worth walking backwards. It closed 715 more gaps and took the worst walking-lane
reading from 4.02 m to 5.69 m off its figure, because the same curve reached from the other end is not
always reached by the same route. Reverted.

**Letting the continuity veto advance.** A step of the chord fallback is refused when it does not carry on
from the last step accepted, and because the last *accepted* step is what it is measured against, one bad
first step refuses the whole gap: ten thousand of the twelve thousand refusals on a city are that cascade.
Measured against the last step *held* instead, the gaps closed with a bare straight fell from 1 525 to 856
and the worst such straight from 75 m to 3.6 m — and the stations standing off their figure went from 4 017
to 22 318 on the pavement and 3 384 to 11 702 on the walking lane. The veto is not standing in the way of
the closure; it is the only thing keeping the closure's bad points out of the line. Reverted.

**So the remaining gaps are not closed by laying more points**, and that is now measured rather than
assumed. Either the crossing is solved — which this file deliberately does not do, and one attempt at it
found the branches crossing behind the fold rather than ahead — or fewer stations are dropped in the first
place, which is the keep rule and is `Extrusion.Of`'s open choice.

## 2026-09-12 — two pieces are one corner when they are nearest at one place, not at one distance

`RingField` answers which side of the boundary a point is on from the nearest piece's own hand, and where
several pieces tie for nearest it summed their outward normals. That is right for a corner — in the wedge
outside a sharp one, taking whichever piece a float preferred read inside as often as out, and the sum
bisects the wedge. It is wrong for every other tie, and the other tie is the medial axis: a point equally
near the boundary on two *sides* of it has two opposite normals, which sum to nothing, and the side is then
decided by a dot product against a zero vector. That answered outside.

So the middle of every band wide enough to have a middle read as standing clear of the boundary it is in the
middle of. What found it was a node of a road nothing is turned through, sitting on its own centreline and
reading as the grass beside it — a fault filed against junctions for having no ground of their own, which
they do not need and never did.

**The distinction is the foot and not the distance.** Pieces meeting at a corner are nearest at one point;
pieces across a medial axis are nearest at two. Only a shared foot sums, and a tie with two feet keeps the
first piece's normal — either of them gives the right side alone, both saying inside.

Nodes standing on ground no car is driven over, read off `--bench shell`: three of 213 to nought on the
larger shipped city, nought of 158 on the other. The worst pavement on each fell with it, by half a metre
and a quarter of a metre.

## 2026-09-12 — a fold's gap is traced along the answer, and the clearance is sought rather than jumped to

The gap a dropped fold left was stepped across on the straight between the two stations bracketing it, each
step pushed out until it stood the distance clear. On a city that gave up on ten thousand of the sixteen
thousand gaps a town leaves and closed them with a bare straight — up to twenty-four metres of one — and
every line struck off the boundary inherited the cut.

**The push could not settle, and the reason is exactly why it was needed.** It asked the nearest piece where
its own offset stood and went there. Where two pieces are equally near that is two answers taking turns, and
two pieces equally near is not an edge case in this file — it is the fold. Four turns of it and the step was
given up on. What settles it is that the clearance is the *least* of the distances to every piece within
reach, which makes it one continuous figure with a corner and no jump: along any line out of a point it
rises and falls continuously, so a bracket round the distance wanted can be halved down to it. `Sought` does
that, and the gaps a city closes with a bare straight fell from 11 657 to 818, the worst of them from 24.4 m
to 14.5 m.

**And the gap itself is now traced and not stepped across.** Both ends of a gap already stand on the answer
and the field says where the answer goes at every point, so the closure follows it — a station along the
tangent, then back onto the curve, then the direction it actually moved as where it goes next. That is
continuous by construction, which is what the old spike veto was standing in for: laid in the order a chord
was walked, the pushes landed wherever the ground happened to be nearest and drew a star across the ground,
and the veto that suppressed it also suppressed the closure. Fifteen thousand of the twenty-two thousand
gaps a city leaves are traced whole.

**It is not the crossing solved.** Nothing here pairs up which offset crossing bounds which fold; the
file's bargain is unchanged.

**And it was not the whole of the fault, which took two wrong guesses to establish.** The worst pavement on
a city did not fall — it moved by under a metre — so the closure looked innocent and the rule that decides
which stations to drop looked guilty. Then a station reading four metres off its own figure turned out to be
the middle of a long straight, so the closure looked guilty and the rule innocent. Neither was the answer:
the probe now reports the length of the piece each station was sampled on, and **the two faults are
comparable in size**. Of the stations standing over a tenth of a metre off their figure, three in four of
the pavement's are mid-piece — a straight the closure gave up and drew — and more than half of the walking
lane's are stations of the walk, which is the rule. What a worst-case figure could never say, a split can.
`Extrusion.Of` carries the rule's side of it; the closure's side is the eight hundred gaps still given up on.

**A bound on where a trace may wander was written and then taken out again.** A trace that takes a wrong
turn can arrive honestly having drawn tens of metres of somewhere else, so it was held to within the gap's
own width of the straight it closes. It fired nowhere on a city — the step budget already bounds the route
to four times the straight — and a gate that refuses nothing makes the gate before it look unnecessary.

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
