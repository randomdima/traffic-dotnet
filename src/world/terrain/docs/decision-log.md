# Terrain — decision log

## 2026-09-07 — a zebra stands a stride clear of the bend, and is walked a lane each way

At a node with no fork the bundle began where the corner's ground ends, so the paint stood its stride past
an **arc** that curves away under it — the tightest zebras on both shipped towns, and the only ones that
read as laid on a curve. The bundle now stands off the corner by the same stride, because the paint has to
lie on straight kerb. And the offset a crossing is walked at is asked of the paint rather than of the whole
kerb-to-kerb line: a station standing *on* the kerb refused every offset there is, since a step sideways at
a kerb runs along it and lands on a junction mouth as often as not. Gated by
`CrosswalkGeometryTests` and `WalkingNetworkTests`, the second asked only of generated towns — the
hand-written fixtures carry what they were authored with.

## 2026-09-07 — a walking lane turns the corner the pavement turns, at its own offset

Every lane end gave up half a band to its corners, so the walk left the kerb it was laid off and the
outside of every bend was pavement no lane reached. That is the figure the *crossing* case needs, charged
to corners that are not crossings. What a corner costs is the arc it is turned on — `offset × tan(half the
turn)` — which is nothing where the pavement runs straight and grows only as the corner sharpens. A
crossing keeps its band, where the overlap really is a band. The gate that moved is that a lane never
strays further from the pavement's line than its own offset: it failed on every shipped map at up to a
third of a metre, and is the whole of what following the pavement means.

## 2026-09-06 — the pavement is the band the walk runs down, and the carriageway ends at its inner edge

The band's outer edge was a dilation, which rounds a convex corner and swallows a narrow notch, while its
inner edge was the tarmac's raw outline, which does neither — so wherever the town's pieces meet at
different widths the band came out a different width at every mouth, and the kerb read as a chamfer across
a corner the shell turned smoothly. The band is struck on one curve, the same one the walking lanes were
laid on, with both edges offsets of it: one distance to answer, one skirt either side, and a half-round at
a run's end that fills the wedge where two runs give way. Cutting moved down into `Kerbs` so ground, mesh
and foot graph share it (TER-7). Everything inside the kerb is tarmac now, pockets included.

## 2026-09-06 — a pavement laid twice is dropped down to one

Two pieces of tarmac lying along one another wrap into two lines lying along one another, and the cost is
not the wasted stretch but the corner the town then lays and offers — a loop hanging off a footway that
turns a body round. It is asked of a shared *node* rather than a shared pair of ends, since the two lines
are cut by what each was laid off and rarely stop in the same place. Both halves are needed: a pavement
closing on itself also leaves one node by two ways, and two short pieces meeting end to end each lie within
a body's width of the other. What tells them apart is that a doubled way ends further down the other's line
than it set off. Asked after the seams are run together, with the prune and the pass run again after it.

## 2026-09-06 — a node of the pavement is a place a walk chooses, not a seam in the construction

Odesa's footway came out as 4 896 stretches over 4 384 nodes, of which 3 313 forked nothing. Two stretches
that are one line where they meet are now one stretch: across the line nothing may move, so the grace is
the rounding divided by the offset the lanes are laid at; along it the weld has already had its say, so a
joint may be open by as much as the weld put it. Nearly all of them were overruns — two pieces of the shell
meeting tangentially are cut by two bisections of their own, so one runs past the other's cut by up to
17 cm. It came to 2 748 stretches over 2 244 nodes, and the check is that every fork in the town is a
crossing's mouth.

## 2026-09-06 — a shell has no ends

A band offered its two sides and nothing across the end it stops at, so wherever the kerb turned a corner
no other piece stood beside, the pavement simply stopped — a hole of a few metres in a footway drawn
continuous. A band ends square (TER-7a), so what stands outside one of its ends is a quarter turn about
each corner and the straight between them, offered like any other line and cut by the one rule (TER-3c.6).
Ends are joined *before* what leads nowhere is dropped: where two lines graze, the ends they are cut to
stand centimetres apart, so the line closing the gap read as dead-ending and was dropped before the stitch
ran. Odesa's pavement dead ends went 406 → 18. What is left is a pinch and not a hole, where a wedge
between two pieces of tarmac is narrower than a walk.

## 2026-09-06 — the pavement wraps the outside of the tarmac, and a band ends where its line does

Once a junction stopped being a shape, the lines cars are turned through it on went into the tarmac beside
the carriageways — and a movement's line offered a pavement up the middle of one the arms had already laid.
A piece now says whether it is the outside of the tarmac or the inside of something (TER-3c.5), and a line
the inside offers is kept only where it leads somewhere. Dropped in the graph rather than refused at the
wrap, because whether a line runs alongside one already laid cannot be settled by measuring how near it
passes: a line closing a gap is near the two ends the gap is between. Separately, a band ends square
(TER-7a) — measured radially it ended in a half-disc that cut the pavement corner at every mouth in half.

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
