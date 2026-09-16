# Drawing the town — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md).

## 2026-09-16 — a fill has no visible edge, so the line is cut for the picture and the fill for the kerb

A boundary was read once and every layer laid from that reading, at a tolerance argued from the zoom the
camera opens at. Both halves of that were wrong, and the thing that shows it is what a kerb is.

**Every shell filled here carries a kerb along its boundary** — the town's along the driven ground's, the
walk's along the pavement's outer face — and the kerb is laid last of the three (TER-7b). So where a fill
cuts a corner, what shows through is the layer beneath it and two hundred millimetres of stone goes over
both. **A fill has no edge anybody sees**, and the only thing that may not happen is its edge reaching out
from under that stone. `HiddenShare` is the fill's whole budget: three quarters of a kerb's half-width,
75 mm, leaving its edge 25 mm inside the kerb's own. `NeitherFillsEdgeShowsFromUnderTheKerbAlongIt` is that
claim asked of the mesh rather than argued from the tolerance.

**And the line is then free to be cut for the picture**, which is what it is: sag, no thinning worth the
name, and `ChordTurnRad` — because the budgets in metres run out the wrong way on a bend. The step a sag
earns is `2·acos(1 − sag/R)`, which *grows* as the radius shrinks: the boundary's tightest turn is 0.14 m of
radius and came back at sixty degrees a chord, inside a two-centimetre sag the whole way. **What a picture
loses on a bend is direction, and direction has no budget in metres.** The cap holds the flatten's step and
bounds what one stretch of Douglas–Peucker stands for, the turn up to each corner carried once per ring so
the recursion asks in constant time. **It is also what cuts the kerb for its own ribbon**: a stroke's outer
edge goes round a circle a half-width wider than its line's, which only passes a tenth over below a metre of
radius while the cap binds everywhere under fifteen metres.

**The two readings are nested and that is the point.** Both are thinnings of one flattening, and
Douglas–Peucker splits at the furthest corner whatever budget it is given — so every corner of the fill is a
corner of the line, and how far the two part is the fill's own budget rather than the sum of what each
strays from the arcs. That is what makes a 75 mm bound a bound and not an estimate.

**What was tried and is not here.** One reading for both, on the argument that a kerb struck off a different
line is a kerb off its seam: true of the seam and irrelevant, because the seam is under the stone either
way. And a thinning of 120 mm on that shared reading, which bought a fifth of the ground and cost the
kerb — 15° a chord is plainly faceted at 100 px/m, which is where this started.

**And a corner of a stroke is one cross-section rather than two.** A stroke used to lay a corner as the
cross-sections either side of it, quadded across; on a line already read as corners that is a quad at every
one of tens of thousands of them. One cross-section on the bisector at the half-width pinches the ribbon to
`w·cos ½θ`, the same figure a chord bows off its arc, so the sag decides it and the sweep is fanned only
where the sag says the single one will not do. A mitre was refused: it stands `½w(sec ½θ − 1)` outside the
line, and a kerb outside its own line is the one thing TER-3d forbids.

**Odesa, seed 1 — 155 755 triangles to 155 133 at 8° a chord**, the fills from 37 937 corners to 34 737 and
the kerbs holding the rest. **The angle is nearly free above eight degrees and the line's own thinning is
what costs**: 11° comes to 146 868 and 15° to 147 656, so the curve is flat where the cap stops binding and
the two centimetres the line is thinned by carry the rest. That figure is a pixel at fifty to the metre and
is not a knob to turn — it is the faceting this entry exists to remove.

**And a road cannot get much cheaper than this.** Its thinning is bounded by the stone over it, and 75 mm
against the 72 mm the whole ground used to be thinned at is the same number: the fill was already at the
kerb's limit before any of this. Nine per cent more is available by spending the last quarter of the
half-width, and it buys a fill edge standing exactly at the kerb's own — which is not a margin.

## 2026-09-16 — the mesh is laid in parts, and a part is a run of the index buffer

`GroundMesh.Build` already laid the ground one layer at a time and threw the seam away. It now writes down
where each layer started, what it came to and what it cost (`GroundPart`, `GroundTally`), which is one
`Stopwatch` and two counts a layer and nothing at all per frame. **The parts tile the mesh** — each starts
where the last ended — so a layer is left out of the picture by packing the runs that are wanted to the
front of the index buffer and shortening the draw. No recording, no pipeline, no ground laid again, and
putting a layer back is the same copy the other way.

**`RoadFigures.CarriagewayDrawn` was the same idea at the other end** and was deleted in the entry below.
Hiding a layer by not *laying* it costs the town a relay and leaves nothing to read: the figure you want is
what the layer costs, and a layer that was never cut has no figure. Cutting everything and drawing some of
it keeps the tallies whole whatever is showing, which is what makes the page a measurement rather than a
picture of itself.

**Odesa, seed 1 — 155 755 triangles laid in 9.6 s**, and the shape of both numbers is the finding:

| layer | triangles | share | ms |
|---|---|---|---|
| town kerb | 59 006 | 37.9% | 14.6 |
| walk kerb | 58 124 | 37.3% | 15.3 |
| carriageway | 19 335 | 12.4% | 54.6 |
| walk | 19 046 | 12.2% | 78.1 |
| water and shore | 242 | 0.2% | 1.0 |
| grass | 2 | — | 1.6 |

**The two kerbs are three quarters of the ground and a sixth of the time; the two fills are the reverse.**
A stroke is a walk of a chain at a cross-section a station, so it is quick to lay and dense in triangles —
and it welds almost nothing, one corner a triangle, because no other layer stands where it does. A fill is
a boundary thinned and cut into ears: slow, and `n − 2` per ring however it is cut. **If the count is what
hurts, the kerbs are where it is**, which is the reading the entry at the top of this log acted on.

**And none of that is the load.** Laying all nine layers is 165 ms of the 9.6 s; the other 9.4 s is the
boundary, of which the merge behind it (`LaneShell`) is 9.3 s. That is the figure named in *what it costs
at load is the merge and not the cutting* below, now read off the instrument rather than off a stopwatch
somebody held once — and it is still not this slice's to fix.

## 2026-09-16 — a layer is the offset filled whole, and the road is what covers it

The walk was the offset with the driven ground cut back out of it (`GroundRings`, `ArcSubtract`) and then
the carriageway was laid over it anyway, so **the cut took out exactly what the next layer covered**. TER-7b
`P0` says it plainly — a layer reaches the boundary and encloses every layer inside it, and no layer is cut
to leave room for the next — and the verge had been drawn that way all along, one rectangle under the whole
town. The walk is now `shell.Outset(WalkOuterM)` filled as the region it is, and `WalkEdge` is those same
chains named for the stroke that reads them as a line.

**What the cut cost was a second copy of the boundary.** A cut layer's inner edge is that boundary walked
the other way round, so the walk carried every corner of it again, thinned on its own terms (`ThriftM`) and
free to disagree along the same curve with the copy the carriageway's own fill thinned — a sliver of grass
between two fills of the same line, which is the thing the two-fills rework was for and which the town's
kerb was left to cover.

**Over Odesa the ground falls from 174 634 triangles to 155 755**, the walk itself from 37 925 to 19 046,
and the vertex buffer by about 610 KB; the suite's city goes 74 696 to 65 301 and the fixture 17 187 to
14 999. Opening Odesa is about 34 ms shorter — 21 ms of cut, and 13 ms of a fill that no longer reads twice
the rings. **The cut was taking the carriageway and nothing else**, which is what made this safe to drop:
over three towns the walk and the carriageway came to 869 831 m² against 869 842 m² filled whole, eleven
square metres apart on 0.87 km².

**What it buys back is overdraw, and overdraw is what this pass is made of.** There is no depth buffer and
no blending, and the grass rectangle covers every pixel on the glass before anything else is laid, so the
whole ground is already drawn over itself. What is added is the carriageway's share of the screen — six per
cent of Odesa's land, at most a screenful zoomed into a street — through a fragment shader that is one
texture sample and a multiply. Triangles are uploaded once and paid for in memory and in the load; a
fragment at this cost is free.

**`ArcSubtract` stays.** Nothing in the drawing path calls it now, and it is kept with its own tests as the
tooling for the next shape that wants one shell taken out of another.

## 2026-09-16 — the line a stroke is struck from runs down the middle of it

**A kerb straddles its shell now rather than standing wholly outside it** (TER-3d): half the stone in the
road and half in the concrete, which is where a kerbstone stands. What that costs is the reading a ruler
takes off a frame — kerb to centreline measures the lane less half a stone, where before the whole stone lay
outside the asphalt. What it buys is the one property the old stroke could not state: **no part of a stroke
stands further from the line it was struck from than half its own width**, so a kerb seen to bulge off its
own line is a defect rather than the shape. It also makes the corner and the hook below two cases of one
rule instead of two sides with different answers.

The stroke was one offset cross-section per station with a quad between neighbours, and the remark under it
said there was no corner case. There were two.

**A corner was crossed by a chord.** A cross-section standing at a corner has two sectors to sweep as it
turns through the corner's own angle, and one quad covers the chords of them — a notch of `½w(1 − cos ½θ)`
on the outside of every turn in the town, and the whole half-width at a corner that turns right round. It is
fanned now (`GroundMesh.Turned`), at the chords that bow under `ChordSagM` like every other bend here.
**Swept about the corner, the cross-section fills the outside and overlaps on the inside in one pass**, and
neither leaves the disc of half a width about the corner — which is what a one-sided stroke could not do,
its wedge reaching across the line and out the other side at any corner sharper than a right angle. That
spike, at the sharp apex of an island, is what a kerb looks like drawn without a fan.

**And the width was constant where the line could not afford one.** The merge leaves hooks where a
movement's ribbon folds through itself, and an edge laid at a constant distance along one reaches the middle
of the turn before it has run out — everything past that comes back on the far side of the line. That edge
stops at the middle of the turn instead (`GroundMesh.Reach`), which is the one place the width gives.
Odesa's tightest hook is 0.14 m of radius: it folded a kerb reaching 0.20 m and does not fold one reaching
0.10 m, so **centring bought the whole of that fault as well** and what the clamp now guards is a tighter
hook than any shipped city has. `--bench outset` reports how many a boundary carries, against the figure
the stroke actually reaches.

**The sampling could not have followed the hook either.** `StepM` floored the step at half a metre, which
can only ever bind on a piece under 1.56 m of radius — the pieces the sag asks for a *finer* step on, not a
coarser one — so the floor fired nowhere but where it was worst, and a 0.27 m hook came out as the single
chord across it. It is `Spline.ChordForSagM` now, the same arithmetic without the floor and without the
second copy. A stroke also counts its stations off its outer edge rather than off the line
(`GroundMesh.Stations`), that edge going round a circle half a width wider.

**And `Ribbon` is gone into it**, a bridge's deck being a stroke about a road's own line and nothing else —
it wanted the same fans and the same fold, and two copies of a strip walker would have disagreed inside a
month. A ring says so (`closed`) and a span does not.

The whole of it is 5.6% more ground triangles on Odesa, 165 353 to 174 634, and **most of that is the
straights rather than the corners**: a cross-section that used to have one end on the line laid a degenerate
triangle at every joint that the mesh threw away, and one with both ends off it lays two real ones. The
corners are nearly free — a fan of half a kerb takes one step up to 74° of turn — and the fold costs nothing
at all now.

The town tier asks its coverage claim either side of the line and at the corners as well as along the
pieces, and asks the claim it could not make before: **no corner of either kerb stands further off the line
it was struck from than half a kerb**, measured against the nearest piece of that line rather than sampled.
A walk that asks what covers a place sees a stroke that falls short and never one that reaches too far.

## 2026-09-15 — the thinning was spending more than the picture could afford

**`ThriftM` moves from 8 cm to 7.2 cm**, which is about a third more corners on the same boundary. The
entry below chose 8 cm on the count and the loss, and both readings still say it is the right end of the
trade; what neither reading could see is the wireframe. A ground drawn at 8 cm reads as a polygon at the
zoom the camera opens at — the thinning takes out the near-collinear corners first, and a bend a driver is
looking along is made almost entirely of those, so the tolerance was spent exactly where a straight is most
visible as one.

Odesa, seed 1, the same columns as below and re-measured here — the sag alone is 26 015 corners and the
thinning is read against that rather than against the capped variant the table below opens with:

| thrift | corners | triangles | quality | slivers | gone | 1 px |
|---|---|---|---|---|---|---|
| 0.080 | 14 616 | 14 842 | 0.654 | 5.1% | 0.879% | 11 px/m |
| **0.072** | **19 077** | **19 303** | **0.623** | **7.1%** | **0.555%** | **12 px/m** |
| 0.070 | 19 762 | 19 988 | 0.620 | 7.7% | 0.507% | 13 px/m |
| 0.060 | 21 906 | 22 132 | 0.607 | 9.7% | 0.377% | 13 px/m |
| 0.047 | 23 959 | 24 185 | 0.580 | 16.3% | 0.303% | 13 px/m |

**The count climbs faster than the tolerance falls**, which is why the figure is 7.2 and not the 4.7 the
arc relation predicts: a Peucker count on a circular arc goes as `1/√t`, but this boundary is 5757 pieces
over 110.6 km and most of them are shorter than the tolerance is wide, so the corners come back nearly as
`1/t` and the knob is far more sensitive here than the geometry alone says. It was found by measuring, at
four points either side.

**Below about 6 cm the shape gives way.** Slivers double between 7.2 cm and 4.7 cm and quality falls off
with them, because the corners being handed back are the near-collinear ones the thinning was added to
remove. That is the floor of this knob, and 7.2 cm is the most corners that can be had before it.

**The sag is untouched at 2 cm.** It is what lays the corners the thinning then chooses between, and at 3.6
times the thrift it still has the headroom to offer more than the thinning keeps; a finer sag alone would
have been taken straight back out.

## 2026-09-15 — two fills and two kerbs, and a kerb is a stroke with a mesh of its own

The ground beside a road was four bands, and grass showed between them. **A band is two offsets
subtracted**, so the curve two neighbouring bands share is carried twice — once in each band's rings, walked
opposite ways and cut at different places. Each fill is then thinned on its own terms (`ThriftM`, 7 cm), the
two copies are thinned differently, and what stands in the sliver between them is whatever is under both.

It is now **two fills and two lines**. The carriageway is the boundary filled as the shape it is; the walk is
that boundary moved by one figure (`GroundRings`), so it reaches the boundary and the carriageway simply
covers it. **The kerbs are strokes** (`GroundMesh.Stroke`)
— the town's along the boundary itself, the walk's along the walk's own offset — each laid outward at
`KerbWidthM` with a mesh built from its own line, **taking no point from any fill** and drawn after the fill
it bounds. A stroke is two hundred millimetres wherever its line runs, which is the one thing a difference
of two thinned fills cannot promise; and each covers exactly the strip a thinning could fall short by.

**A stroke needs a corner rule, which a subtraction had for free.** A corner that opens the outward side is
filled with a fan of the stroke's own width — the arc the offset of that corner used to carry — and one that
closes it needs nothing, the stroke lapping itself and one tint laid twice being the same tint.

**This is a change to TER-7b and the owner made it.** The rule said a kerb line was what a layer leaves of
the one under it and never a shape of its own; it now says that of a rim and an edge line, and names a kerb
a stroke along a shell of its own. What the rule was protecting is intact: a kerb is still a line the
boundary struck, still measured from the boundary and never from the line before it, and still wholly
*outside* the ground it bounds (TER-3d) — which is what
`TheTownsKerbCoversTheWholeOfTheDrivenGroundsBoundary` and `TheWalksOwnKerbCoversTheWholeOfItsOuterFace`
read back off the finished mesh.

**And it costs a third of what it did**: one move of the town's outline, against three moves and three cuts,
about a second and a half a move over Odesa, paid once when a town opens. The two strokes are a walk
of two chain sets and no boolean work at all.

**No new surface and no new art.** Both fills and both kerbs wear the pavement's own texture and are told
apart by the shade, which is the rule the shore and the deck's rim were already drawn by — an edge is the
surface darkened and a kerb is it brightened, with the grain coming through both, and the walk's own kerb
takes the edge shade because it is the face of the stone that looks away from the street. A kerb as a sixth
texture would have been a sixth binding, a sixth period and a sheet to keep at density, to say something two
floats already say.

**`RoadFigures.CarriagewayDrawn` is deleted rather than turned on.** It hid the road's own surface while the
boundary was the thing being looked at, and what it was waiting for is what landed here; its one branch
guarded the slab loop, which draws nothing today because nothing lays a slab.

## 2026-09-15 — the ground is thinned, and the only lever anything had on the count was the corners

**Nine ways of cutting the ground were built, measured against one another and eight of them deleted.**
What it settled on is the drawn sag at 2 cm with Douglas–Peucker over it: **14 842 triangles at 8 cm
against the 28 895 the sag alone came to**, the best triangle shape of the nine, and three times as quick
to lay.

The reason the comparison was worth running is that the obvious knob does nothing. **Every triangulation
of the same corners is the same count** — `n − 2` per ring by ears, by monotone pieces, by a trapezoid
sweep, by Delaunay, by any partition into convex pieces at all — so no choice of *cutting* moves the
figure and the whole of the trade is how many corners the boundary is read as. Adding points cannot help
either: an interior point costs two triangles and a boundary point one, so `T = N + 2I + 2H − 2` only ever
climbs. That leaves accuracy as the only currency, and the question becomes which reading of the boundary
buys the most corners back per metre of accuracy given up.

Odesa, seed 1 — 414 249 m² of driven ground over 110.6 km of boundary. `quality` is `4√3·A/(a²+b²+c²)`
averaged, where 1.0 is 60-60-60; `gone` is the ground that changed hands, integrated along the true
boundary; `1 px` is the zoom at which the worst stray first covers a pixel, against a camera that will go
to 576 px/m.

| how | corners | triangles | ms | quality | slivers | gone | 1 px |
|---|---|---|---|---|---|---|---|
| chords, capped at a carriageway | 28 669 | 28 895 | 48.3 | 0.621 | 14.7% | 0.226% | 20 px/m |
| chords | 26 015 | 26 241 | 29.2 | 0.582 | 16.3% | 0.269% | 19 px/m |
| arcs joined first | 25 763 | 25 989 | 28.1 | 0.587 | 15.8% | 0.269% | 19 px/m |
| sag 0.05 | 17 850 | 18 076 | 17.1 | 0.621 | 9.7% | 0.619% | 15 px/m |
| sag 0.08 | 14 943 | 15 169 | 13.5 | 0.614 | 7.8% | 0.943% | 10 px/m |
| **Peucker 0.08** | **14 616** | **14 842** | **15.5** | **0.654** | **5.1%** | **0.879%** | **11 px/m** |
| Visvalingam 0.08 | 13 900 | 14 126 | 22.2 | 0.661 | 6.8% | 0.933% | 9 px/m |
| Peucker 0.20 | 10 612 | 10 838 | 9.3 | 0.614 | 4.8% | 1.720% | 5 px/m |
| Peucker 0.50 | 6 796 | 7 022 | 6.2 | 0.556 | 0.8% | 4.069% | 2 px/m |

**The tolerance is worth thirty to seventy per cent and the algorithm two to seventeen**, so this is a
tolerance decision wearing an algorithm's clothes — with one real algorithmic win in it. Read the rows in
pairs at equal loss, which is the only way they mean anything: sag 0.08 against Peucker 0.08 is the
comparison that decides it, and Peucker wins every column at less loss, because a sag is spent evenly
whether a stretch bends or not and a thinning spends it where the boundary actually bends.

**Thinning improved the triangle shape as well as the count**, which is the one result that was not
expected: dropping corners is usually a quality cost. The corners Peucker takes out are the near-collinear
ones, and those were exactly the ones making slivers — 5.1% against 14.7%.

**Visvalingam–Whyatt was measured and rejected.** It is about 3% ahead of Peucker on count at matched loss
and costs 43% more to run, being a heap with lazy invalidation against a recursion. **Joining the arcs
first was a dead end**: the merge already joins them, so it found 252 corners in 26 000.

**And the cap on the longest edge was dropped.** It bought corners along the straights at `RoadWidthM` —
10% of the triangles — and was added when long sides were a live symptom. The flips fixed that cause, and
nothing downstream reads edge length: a ground vertex carries its texture coordinate as its own position
over a period, so the coordinate is affine in the position and a barycentric across a sliver is as exact
as across an equilateral. **Triangle shape buys nothing in this renderer**, which is why the count is the
metric and shape is the tiebreak.

The eight losing ways are deleted rather than kept behind a switch, along with the menu page that picked
one and the `--ui cut=WORD` that named one. A catalogue of recipes nothing draws is a set of measurements
of nothing; the table above is what it was for, and it is here.

## 2026-09-15 — the driven ground is drawn again, as the one shape it is rather than the pieces it is of

The boundary the driven lines lay has been the town's own answer since the merge moved into the kernel, and
nothing could draw it: a picture wants triangles and a shell hands back rings. `ShellFill` closed that, so
the first layer of TER-7b's stack is back — **the whole of the driven ground as one region, cut once, with
every block the streets enclose left as the hole it is**. It is not the old ribbon-per-road drawn at four
sizes and it is not coming back as that: a road, a movement and a junction's wedge are one shape here
because they are one shape on the ground, and the edge of it in the picture is the same arc the answer is
read off.

**It wears the pavement while it is the thing being looked at.** A carriageway is tarmac and this is not
that layer — it is the boundary itself, drawn so the shape can be judged — so it takes the surface that is
plainly not the road's own. When the stack comes back off the offsets of this boundary, the fill under the
carriageway is this same call at another distance and another surface, and the tarmac goes back on.

**It is cut at the tolerance a picture is worth and not at the fill's own.** `ChordSagM` is two centimetres,
which is what every other bend here is sampled to; the fill's default is a tenth of a millimetre, and a
city's boundary cut that fine is a hundred times the triangles at a difference no frame can show.

**And thinned afterwards** (`ThriftM`), which is the entry above.

**What it costs at load is the merge and not the cutting.** Nothing on the ordinary path asked for the
town's boundary before — the perimeter layer did, on the first frame it was ticked — and a shipped city's
merge is about ten seconds. That is the figure to attack if the load is too long, and it is not this
slice's.

## 2026-09-12 — the paint is one switch and not four

A dash, a zebra, a bar and a bay stroke are one layer (TER-7b's "then the paint") — marks *on* a surface
rather than boundaries of one, and the one layer that is furniture's rather than the boundary's — so there
is one switch for the four of them and not four. `RoadFigures.PaintDrawn` off leaves the ground mesh on a
city at 244 triangles, against 66 506 with the whole stack drawn.

**A mark is the one thing here that may be held back rather than laid**, because nothing hangs off it: the
ground under a zebra is carriageway either way, and what a crossing *means* is the walking network's. The
surfaces cannot go the same way — `Ground.Road` carries the grip a tyre is solved against, the permission
that says who may be there and the lane a body is written onto, so a town whose driven ground stopped
being laid is a town whose cars fall through it.

## 2026-09-12 — a layer is a region and not a heap of pieces

The ground was a union stated by over-painting: every road, movement, wedge and car park laid at four sizes
until their outlines happened to agree. It cost nothing per junction, which is why it was built that way,
and it had one thing it could never do — **hand anybody the boundary**. A rim was what overdraw left. It had
no length, no arcs and no side, so nothing could ask whether the pavement was a walk wide; only whether it
looked it.

Each layer is now the ground within one distance of the kerb, filled as the shape it is, bounded by the
boundary the shell computes. **This is a change to TER-7b and the owner made it**: a union computed once and
drawn as one shape, rather than written down by laying its pieces over one another. What it gives up is that
no piece knew what was beside it; what it buys is that every edge in the picture is a line with a figure on
it, and the answer is the same figure compared the other way.

**A block is a hole and the holes are laid last, in the other order.** Outside the town the distances nest
inwards; inside a block a ring nearer the kerb leaves *more* of it beyond, so the regions' own order paves
every block kerb to kerb — which is exactly what the first run of it did. Which a ring is, is the sign of
the area it covers, the ring walking with the ground on its right throughout.

**A bridge is the one band left.** Its deck's width and the pavement it carries are authored per bridge and
are no part of what the boundary knows, so it stays a ribbon about the road's own line.


## 2026-09-09 — a car park is a junction, so nothing here draws one

The ground carried a car park twice: an oriented rectangle for its tarmac and a rounded rectangle a walk
bigger for its wrap, in a pass of their own, answered by a second pair in `GroundShapes`. A stretch of its
own road was tried next and was still a shape of its own. **A car park is the union of the movements that
reach into it**, exactly as `TER-5` says a junction is the union of the ones that cross in it, and it now
goes through the loop that draws those — `Grown` walks `Paving.MovementCount` and has no case for one.
`GroundShapes` answers it with the same `Sweeps`/`OffTheBandM` a junction gets. The only thing that tells
the two apart is where they answer in `At`: a bay's way starts on the carriageway it leaves, so asked
before the roads it would call the near lane of every street a car park. `RoundedRect` had no callers left
and went, and a bay's stroke is `DashRun` with the dash the length of the run — a solid line is a dashed
one that fits in one dash.

**What made it possible**: `BayTemplate` came down from `agents/car/control/` to `core/geometry/`, taking
the three figures it used off `CarBuild` as scalars — so `citygen/BayLines` lays the town's ways at the
nominal car's figures (`CarParkingTemplateRadiusM` and the rest, derived on `SimConfig` where `CAR-11a`
always said they were) and a car still lays its own at its own circle. `world/parking/BayWays` kept every
bit of what the driving makes of a way and lost all of the geometry: a hundred and seventy lines of laying
became twelve of reading.

## 2026-09-09 — a round corner is cut at the corner, and an end stops being swung right round

`RoundedRect` fanned the whole shape from its middle: one spoke per arc station, each as long as the shape
is wide, so a car park's wrap came out a wheel of chord-wide slivers and the wireframe read as a hub rather
than as ground. It is cut where the corners start instead — the slab between the four pivots, an end slab
past each straight side, a quarter turn about each pivot — which is the same area in the same number of
triangles, none of them longer than the corner it turns.

The end of a road that stops was the same shape swung right round the last cross-section, and the half of
that swing pointing back up the road is inside the ribbon laid at `halfM + outM` by construction: within
the road's own extent and within that half-width, so nothing of it could ever show. `GroundMesh.EndCap`
lays the outward half only. Odesa's ground came out within a percent of where it was, and the frame within
one level of one channel — which is what world-anchored texture coordinates promise and what makes a recut
free to make.

## 2026-09-09 — the ground is a stack again, because the answer never stopped being one

`TER-7b` asked for a partition and `GroundShapes.At` answered as a stack — every shape of the town at its
own size, then every shape grown by a walk, taken from the top. Everything expensive in this slice was the
cost of making a partition agree with that: `Paving` cut each road into the stretches over which what stood
beside it did not change, worked out where each junction's box took over from its arms, walked the box's
outline run to run and turn to turn, closed each run with a half-round, turned a wedge at every hand-over,
bridged the ones a wedge did not cover, and struck a rim only over the stretch whose outer edge was really
the outline; this slice then struck all of it as bands between consecutive offsets of one curve at one set
of stations. Sixteen constructions, and every one of them existed because two shapes that abut must not
overlap.

**The owner retired the rule.** A layer is a union now, and a union is stated by drawing its pieces over one
another — so `GroundMesh.Build` is `GroundShapes.At`'s list read forwards, at three sizes, and the two are
one list in two directions rather than two constructions kept in step by whoever remembers.

**What it deleted**: `Strips`, `Section`, `Side`, `Run`, `Rim`, `Round`, `Turn`, `Bridge`, `ShellCorner`,
`Box`, `Stub` and the stations they were struck on; and in the paving, the sections, the boxes, the stubs,
the caps, the corners, the shell corners and the hand-over graph — everything the picture alone read.
`Ribbon`, `RoundedRect` and `Fillet` are what is left, and each is the shape of one thing at one size.

**It costs about no triangles**, which is the surprise: a laid city went 179,194 → 170,690 and the fixture
6,019 → 6,285. A partition needs a strip wherever two shapes meet and there are more of those than there
are pieces to overlap; what the stack spends it back on is the round each road that really stops carries
at its end, which the fixture pays five times over twelve junctions and a city hardly pays at all. What it
does cost is the wireframe (OBS-2o), which now reads as three layers of triangles rather than as the
town's surfaces and their seams — that reading was the partition's, and it went with it.

**Three things the change had to be told, and one it did not.** A slab offers the town's outline no line to
walk (`Kerbs.Lay`, `WalkedPast.Never`), so nothing pavements round one and nothing draws it — grown with
the rest, it laid concrete on ground the answer calls grass. A road's ends are square where the answer
swings the growth round the band's last cross-section, so an end that really stops carries the offset of
that segment — only one that really stops, since capping every end instead put a fifth of a city's ground
into rounds buried in junctions; a movement's ends carry nothing at all, every one of them standing inside
a junction. And what breaks the kerb
line over a car park's mouth is the lot's own tarmac, laid between the stroke and the carriageway because
that is where the answer puts it — where the partition needed the frontages walked and the stretch left
unstruck. The one it did not: the mouth, the self-crossing junction outline and the bridge were three
[known gaps](../../../../docs/index.md#known-gaps) that all wanted the boundary of a union of overlapping
shapes. None of them is a shape any more.

## 2026-08-30 — the art was never pixel art, and was stored as though it were

`ArtPixelsPerMetre` claimed 21 px/m blown up ×3; tested, the block structure is absent and the sheets hold
96,000–263,000 distinct colours each. That cost 26 MB of art and a format chosen for flat colour. The grid
moved to 31.5 px/m — not 21, which would go soft long before the zoom runs out — and the storage to WebP,
giving 2.7 MB and one atlas page from three. The fleet did not move: `CAR-12`'s 9 mm tyre overhang is
under half a texel at 48 px/m. Halving everything was tried first, and the unit tier named exactly what
could not be resampled.

## 2026-08-30 — a sheet is a rectangle of a page, not a descriptor

Descriptor indexing is a Vulkan extension neither WebGPU nor WebGL2 has, so a browser would have needed a
second answer to "which picture" — two grammars that disagree within a month. It was never free either: a
wave spanning quads from different sheets is a divergent descriptor index the driver scalarises, where an
array layer is a coordinate. [SheetAtlas](../SheetAtlas.cs) packs them into one array texture and the
instance indexes a table of places. The five ground surfaces were not atlased, being different sizes and
wrap-seamless, and the tread was not either, being a tile whose coordinates run outside the unit square.

## 2026-08-26 — a casualty is art, not a tint

A red multiply on the existing per-instance `Tint` was two lines and refused: the tint says how hard a
mark was pressed, and what a body *is* is which sheet it samples. A look now ships a second sheet, cut
from the first offline by
[tools/personsheets](../../../tools/personsheets/make-down-sheets.py) on the same footing as a signal
head's lit frames. Drawing it fresh was thrown away — seven looks of new art are seven ways to be slightly
out of style beside the walker they belong to.
