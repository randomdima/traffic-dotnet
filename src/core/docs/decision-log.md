# The kernel — decision log

## 2026-09-12 — which way along a bend a point stands is read off the chord, never off the turn's sign

**The chord fixed the magnitude and left the sign reading noise.** A distance along a piece is the chord
over the `sinc` of the half turn it subtends, and the half turn is the angle between the chord and the
start heading — so on a piece that barely bends it is a fraction of a microradian, while the point handed
in carries tens of them at a town's own coordinates. Signed off that angle, a crossing plainly *ahead* of a
near-straight piece came back as a distance *behind* it, `AlongOf` wrapped it a whole circle away, and the
crossing was dropped for standing off the piece.

**A ribbon edge offset off a straight road is exactly that piece**: the lane the merge lost a crossing on
carried a curvature of four millionths, and its edge crossed a junction corner two metres along. The merge
then cut neither of them, the boundary ran straight through the ground it was meant to hand over, and the
ring it belonged to could not close.

**The chord says which way it stands without being asked.** A chord standing within a radian of the start
heading is a point ahead of the start whatever the curvature is doing, and a point behind stands a half
turn off that heading however shallow the bend — so it comes back through the turn's own case, as the far
side of a whole circle, which is what `AlongOf`'s wrap is for. Nothing decides the sign now, which is why
it cannot be decided wrongly. Odesa's open boundary fell from 104 runs to 100 and River's from 51 to 47.

## 2026-09-12 — a distance along a bend is read as a chord, because the turn carries the radius into the error

**Where two lines cross was right and how far along them it stood was wrong by metres.** The crossing
itself is solved in the piece's own frame and lands where the two lines actually meet; turning that point
back into a distance along the piece was the closed form `2·atan2(…)/k`, and that divides by the curvature.
A road's bend subtends a fraction of a degree over a piece, so the angle being divided is small, and it is
read off two town-sized coordinates — a float carries a coordinate of a couple of kilometres to a quarter
of a millimetre, which over a chord of a few metres is tens of microradians. Divided by a hundred-thousandth
that is metres.

**Measured rather than reasoned**: the same geometry at the origin answered to a micron, and at
`<2000, 1500>` a crossing on a bend of 200 km radius came back **half a metre** from the meeting point, one
on a bend of 2 000 km **four metres**, and the shallowest bends came back with **no crossing at all** — the
distance landing off the piece, and `AlongOf` rejecting it. Every ribbon edge a town lays is one of those
bends. What the merge did with it is what a picture of the boundary showed: a cut in the middle of a ribbon
with nothing there, and a plain crossing of two bands with no cut at either side of it.

**The chord carries no such factor** and is `ArcSeg.PointAtM` read backwards: the distance is the chord's
own length over the `sinc` of the half turn it subtends, both of them quantities the size of the piece.
Above a radian of half turn the `sinc` is small enough to cancel and the turn is exact enough to use, the
curvature there being large — that is the only case the old form still answers. On the shipped maps the
merge's open boundary fell by a third on the strength of it alone (Odesa 153 runs to 104, River 79 to 51).

**`NearestOnArc` still measures against the centre, and that was measured too.** It is the same fault in
the same family — a centre a hundred kilometres off the map, its last bit a centimetre — and writing it
from the start instead is a millimetre or two better. It is also what decides which lane a body standing
between two of them is on, so the millimetre moves routes: the generated city's boundary closed *less*
(18 open runs became 22) and a rescue that arrived inside its bound stopped arriving. A nearest is not a
cut, nothing downstream of it is cut to the millimetre, and the accuracy on offer is not worth what it
costs.

## 2026-09-12 — the offset and the fold rule are gone, and a pair of pieces answers where it crosses

`Extrusion` and `RingField` are deleted. Between them they were a distance rule — the line standing a fixed
distance to one side of a ring, kept only where no point of it stood nearer the ring than that — and every
line the town struck off its boundary was one of them. Nothing strikes such a line now
([citygen](../../citygen/docs/decision-log.md)), and a construction with no reader is a second description
of a shape waiting to disagree with the first.

**What the kernel gained instead is one method**: where two pieces cross, as the distance along each
(`Spline.CrossingsOf`). It is the closed form `CrossingsM` was already built out of, handed to a caller that
is arranging pieces rather than following a chain — such a caller has no place along either chain to rank
crossings against, and wants every one of them rather than the nearest few.

## 2026-09-12 — the geometry grid answers "which lines could" as well as "which line is nearest"

`ChainIndex` was a uniform grid over the town's arc chains with one question on it: which chain a point is
nearest. Every caller that needed the *set* of lines round a place either asked `Near`, which projects each
candidate onto the point and measures it, or asked nothing and walked the whole town. So the shell paired its
stretches by weighing every end against every other end, and the bay ways found their crossings by weighing
every way against every other way — quadratic in the size of the town, on a build path, to prove that two
lines a district apart do not touch.

**The grid already knew the answer and had no way to say it.** `Around` hands back the chains with a piece in
the cells round a point and `Crossing` the chains sharing a cell with a whole chain, each once, measuring
nothing. Both are supersets, and that is what makes them safe: **the caller's own test is unchanged**, so the
answer can only differ by a pair the grid failed to offer — and two lines that cross share a cell by
construction, the crossing point lying in some cell and each line having a piece binned into it. The boxes a
query reads the cells by are the builder's own method, because a tighter box at a bend is the one pair that
would be missed.

**The lattice is snapped to the map rather than to the set.** The origin was the least corner of whatever
chains were fed in, so two indexes covering different ground laid their cells on different lines and a cell
was a place in a set rather than a place in the town. Snapped down to a whole cell they agree, which is what
lets one debug layer draw the grid every index is asked over (`OBS-2r`).

**A piece is binned into the cells it runs through and not the cells its box covers.** The builder took each
piece's axis-aligned box and wrote the piece into every cell that box touched, which for a diagonal piece is
most of a square: over the city the boxes held 1.11 entries for every cell a piece really runs through, 1.03
on the laboratory map. As a candidate count that is a tenth; **as a picture it was the whole of a fault** —
the debug layer washed a three-by-three block of cells over open grass a street from any line, because one
long diagonal lane really was indexed there. The piece is walked instead, at the step the box was already
taken with, and each sample claims the cells within half a step of it. A query may still read by the box and
stays sound: a crossing lies in one cell, that cell is inside the asking line's box, and the crossed line was
written into it by the walk.

**What it bought and what it did not.** The shell's own pairing was exactly the answer it was — the shell probe
is identical on both shipped maps, line for line. **The lay is about a fortieth shorter**, and that is the
tighter binning rather than the narrowing: interleaved against `HEAD` on one machine state it runs
3 537/3 498/3 549 ms against 3 645/3 630/3 630 ms, the two sets not overlapping, because every query that
projects its candidates — the walk's own `Banded` and `Owns` among them — now gets handed fewer of them. The
narrowing itself is not separable from the noise: the pairing was never the dominant term, and the 22 million
distance tests a city's 4 699 stretches asked of one another are a few tens of milliseconds of a
three-and-a-half-second lay. **What is gone is the quadratic term**, which is the whole reason to have done
it: the cost now follows how crowded the ground is rather than how large the town is, and a figure that used
to quadruple for a town twice the size now doubles.

**Figures across sessions on this machine are not comparable.** The same `HEAD` measured 3 093 ms one hour
and 3 630 ms the next with nothing changed, which is a sixth. Every reading above is interleaved against its
own baseline for that reason, and a claim measured any other way here is a claim about the machine.

**Two orders had to be made total for it.** The cells are walked row by row, so a caller that settled a tie on
which candidate it met first would have settled it on the lattice; where the shell ranked joins on a distance
and a turn it now ranks them on the stretches' own numbers behind those, and the swap takes the
lowest-numbered of equally cheap swaps. Those ties were being settled by the enumeration order before, and
the enumeration order was the thing that changed.


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
