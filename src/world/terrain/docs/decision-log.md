# Terrain — decision log

## 2026-09-19 — the cost of a ground answer is a figure per kind, and a road that answers nothing is not asked

One figure for what a question costs hid the only thing worth knowing about it. Asked per kind
(`--bench census`), the town answers **grass in 60 ns and carriageway in 555** — nine times as dear, and
five times the mean every judgement had been made against. The two hot paths share almost nothing: a wheel's
ask is the roads and the driven bands and stops there, and a walker's carries on through the water's rings
into the walk. **A mean over the mix says what a town costs and nothing about what either of them costs**,
which is why the rows carry the figure and the headline is now their mean rather than a sweep of its own.

**The figure is taken by asking again rather than by timing in place.** A timestamp costs a fair share of a
130 ns ask, so a per-kind clock inside the sweep would have been mostly the instrument; the sweep classifies,
and a strided quarter-million of each kind is then timed as a tight loop. Strided because a kind lies in
bands across a town — the first quarter-million points answering water are one end of the river, where every
ask stands inside the same ring's box.

**What it found first: four roads in five were in the index to answer no.** A road says two things about a
point, a deck and a stretch of paint, and both are read off its own runs — so a road carrying neither is a
candidate a query reads a cell for, projects onto, and learns nothing from, and there is no early-out to
save it, because the loop has to weigh every candidate to establish that none of them is painted. Odesa
carries 510 crossings and no bridges over 2054 roads. Asked of the roads that can answer, a carriageway ask
went **689 ns → 555** and the mean **136 → 111**; the ring shrank with it, since the radius every query reads
its cells by is the widest road in the *index* and not in the town.

**And the working set is no longer zeroed.** `ChainIndex.Near` fills every slot below the count it returns
before anything reads one, so initialising the frame was a kilobyte of stores a query paid and never read —
worth a flat 6 to 15 ns of every ask. It is also what lets the caps above what one junction can hold be
generous instead of measured: room nothing fills now costs the stack pointer, so the fallback that walks
every driven line in the town sits behind 256 rather than behind a figure argued from a five-armed
crossroads, in a city whose junctions stand three metres apart.

**What the rows say to do next is measure the carriageway row and not the mean.** `Near` projects every
candidate before it hands one back, so the early-out on the driven bands saves the band arithmetic and none
of the projections — which is where the remaining 555 ns is, and it is a question about the cell the driven
lines are binned at rather than about the arithmetic.


## 2026-09-19 — the concrete is the same distance one step further out, and a junction has no kind

The answer knew two grounds beside a road: the tarmac a driven line lays, and grass. The pavement was drawn
and not answered, so a walker stood on concrete it could see and was told it was standing on a verge.

**A layer is one distance off the driven bands, and the table says which.** Nought is the tarmac and a walk
is the concrete (`SimConfig.WalkOuterM`), which is TER-7b's own construction rather than a second reading of
it — so a layer added to the picture is a row added to a table and never a shape to intersect. One query
answers all of them: a distance, where a query per layer was the same index walked again at another offset,
and two walks that could disagree.

**The three sets became two, because they answered two kinds and not three.** A lane and a movement through a
box lay one surface — an intersection has no shape of its own (TER-5) — so they are one set answering
carriageway, and `Ground.Intersection` is gone with the row in the catalogue that permitted exactly what a
road does. TER-3 no longer asks for a kind for the ground roads meet on; what turns on a junction is asked of
the road graph. The ways into a bay stay a set of their own, because a bay is ground a walker may stand on
and a carriageway is not.

**What was checked is the promise the two constructions make each other.** The walking network is the
boundary moved off itself and reads no ground at all (WLK-1), the walk the answer gives is that boundary
moved by its own figure, and TER-3c.3 says the first stands on the second. It does, over every stretch of
pavement lane in both towns. Nothing here asked the other.

**What it came to on the shipped city** (`--bench census`): 47.65 ha of Odesa — 6.9 % of it, against 5.9 %
of carriageway — answers as the concrete it is already drawn as, and every hectare of that was grass. A
question costs **128 ns**, two index walks where it was three.

**And the corner is where the picture and the answer part.** The fill is the boundary rounded at
`Road.LineRoundedM` and offset; the answer measures off the bands, which are square — so at a corner the two
stand up to that radius apart, and it is the rounding that deviates from TER-7 rather than a second geometry.
`RoadGround.Carriageway` went with the change: which road's carriageway a point was on had been computed
every query since the lanes took that answer over, and read by nobody.


## 2026-09-12 — the ground answer is a line and a distance

`At` asked the shapes in the reverse of the order they were drawn and took the first that covered the point,
which held the picture and the answer together by their both walking one list. The list is gone: what is
drawn is a region per distance, so what is answered is the same distance compared against the same table.

**Which tarmac a point is, is the line that lays it; where the tarmac stops is the boundary.** They are one
construction asked two ways — the boundary is the outline of those same bands — so they can no more disagree
than a shape can disagree with its own edge.

**The lanes and never the roads.** A road's band runs the whole length between the junctions at its ends
while its lanes are cut back from them, so it claimed a sliver at every mouth that no car is driven over.
Read off the road it was carriageway, with the pavement — laid off the boundary, which rightly excludes it —
standing on top. Two readings of one edge, and the road was the wrong one.

**What is left inside the boundary and claimed by no line is the wedge a junction's corner is paved back
over**, which is the whole of what an intersection has that its movements do not (TER-5). There is no fillet
to lay: the boundary turned that corner itself.

Gone with them: the walk band, the kerb fillet as a shape, the grown road band, and the three roundings the
candidate-and-cut scheme needed to make coincident lines behave.


## 2026-09-06 — the pavement is the band the walk runs down, and the carriageway ends at its inner edge

The band's outer edge was a dilation, which rounds a convex corner and swallows a narrow notch, while its
inner edge was the tarmac's raw outline, which does neither — so wherever the town's pieces meet at
different widths the band came out a different width at every mouth, and the kerb read as a chamfer across
a corner the shell turned smoothly. The band is struck on one curve, with both edges offsets of it: one
distance to answer, one skirt either side, and a half-round at
a run's end that fills the wedge where two runs give way. Cutting moved down into `Kerbs` so ground, mesh
and foot graph share it (TER-7). Everything inside the kerb is tarmac now, pockets included.

## 2026-09-06 — a band ends square where its own line ends

Measured radially a band ended in a half-disc of its own half-width past the last point, which cut the
pavement corner at every mouth in half (TER-7a). It ends where its line ends, square across.

## 2026-09-03 — the cell grid is gone and the ground is solved against the shapes

A map carried its shapes *and* a one-metre classification of them, with TER-7 stating the disagreement as
half a cell — largest exactly where it mattered, on a corner fillet where no arrangement of metre squares
is a kerb at 40° and a tyre's contact patch is 0.2 m. It fed back into traffic, since a claim goes as
v²/2a and a misclassified cell lengthened a held stretch quadratically. Ground is now solved against the
shapes in the reverse of the order they are drawn, so `GroundMesh.Build` and `GroundShapes.At` are one list
in two directions and the question of whether drawn and answered ground agree can no longer be asked.
Nested, because roads cannot overlap and lanes can: the road level answers which road a point is on, and a
carriageway, walk, deck and zebra are then intervals with the bend taken out. A zebra is asked about first
because it is struck last. `Ground.Footway` and `LaneDirs` were deleted — 13.8 MB on Odesa with no
production consumer. The generator keeps no raster either, so the geometry lives with the plan and the
permissions stay above it, which is what lets a half-laid town be asked what is where. It costs 900 ms
against 384 to lay Odesa and about 300 ns a question against 16–21 ns, which is 4 % of a core.
