# Drawing the town — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md).

## 2026-09-08 — a band read in to a point is a fan to the sag, and a joint is one station to a rounding

`GroundMesh.Strips` laid a fan only where consecutive near corners stood within a millimetre, so a band
read in to a centre that the stations reached a few millimetres apart — a run wrapping a corner on a radius
a hair over half a walk — came out as pairs of triangles with one a sliver, and a joint two arcs put a
float apart made a station twice and a strip of no length. A band whose near corners stand within the sag
the arc is drawn to, and whose far corners do not, is now a fan from the first of them; a stretch whose
corners crowd at both edges is still a strip, since fanned, half of it was a hole; and two stations within
a rounding are one. And an offset goes no further across a curve than its centre
(`GroundMesh.ShortOfTheCentre`): inside a bend tighter than half a walk the band's outer edge was a smaller
arc turned the other way, and the strips across it were bow-ties that happened to cover the ground.
Nothing in the picture moved.

## 2026-09-07 — a junction is a box between its arms' cuts, laid once

With everything grown gone, what was left in a junction was the arms' sections crossing one another to
the node. The partition is: every arm cut where the box takes over from it, the box laid once as one
outline, and the ground is covered once (`AJunctionsBoxIsCoveredOnce`). **The box's outline is the
pavement's own kerb line** (`CityGen.Boxes`): across each arm at its cut, in along its kerb to where that
side's run stops, and from there run to run and turn to turn round to the next arm — the hand-over graph
the paving already has (`Paving.Next`) — half a walk in on the road's side. Read off the runs rather than
off the corners the plan carries, a movement that swings out past a fillet's arc is inside the box, so
nothing is grown for it either; the union pass is gone entirely, and a dead end's head is the run that
turns round it, with its own rim. An arm is cut at the further of its two sides' hand-overs, and the nearer
side's kerb runs on past the cut as a stub with its own concrete beside it (`Paving.Stubs`).

**What stays painted over itself**: a box whose outline crosses itself — two arms meeting a step apart and
turning no corner, or three of which two all but run on from one another — is no box, and those arms meet
as they always did; and a movement's few centimetres past a kinked kerb, which is the walk's to draw. The
fixture's crossroads is 1.01 deep; the suite's city's boxes are all covered once but two the outline test
leaves alone.

## 2026-09-07 — a car park is its box, the band round it and the pocket at its mouth

A car park wore two fans from its own centre — the box grown by a walk in the rim's shade, then a line's
width smaller as tarmac — every triangle of them under the box and the band drawn over them, and the band
itself was a skirt with a kerb line skirted over it. Now a run of pavement that wraps anything but a road is
struck as a road's side is: kerb line, walk and rim as three bands of one cross-section on the run's own
line (`GroundMesh.Run`), and `ACarParksBandIsCoveredOnce` holds the band to TER-7b. The box is not grown at
all; what it has beyond the box and the band is the pocket at its mouth, and that is the street's to lay: a
side is bare only where what stands against it is within a walk, so the section's bare band reaching the
whole walk covers the pocket and the box takes over beyond it. A pocket laid off the box's own wrap was
tried first and was a second band under the first, with a fan into the carriageway at each corner.

**The corner two runs hand over at is one fan and not three** (`GroundMesh.Turn`): the wedge between the
bands is the turn's own arc read in to the place it turns about, with the kerb stroke on the same stations,
and no round is struck at an end a turn closes (`Corner.Stops`).

**Two things the fans had been quietly doing came out when they went.** The round that closes a run had no
rim of its own and got one from the grown pass; given one, it drew a dark arc across the neighbouring band,
because a run stops where its wrap dives inside other tarmac and the round stands inside the concrete there
— so it has none, and the one end that faces the grass is a road's dead end, whose rim is the road's own
`Cap`. And the pocket beside a road with something against its kerb was a band of the section, drawn last
with the carriageway, which painted the rounds at a car park's mouth out to a square notch; it is laid
first now, on the same stations, and the carriageway alone is laid last (TER-3d). Both were found by
rasterising the mesh against `GroundShapes` over a car park, decimetre by decimetre — an instrument and
not a test (VER-12), since it checks a drawing against the field it was drawn from.

## 2026-09-07 — a junction the road runs through draws nothing, and a movement has no ends

Every bend in the town wore a rosette. A node with two arms is a road that bends (TER-5b), and its two
sections already met edge to edge; but the box pass still turned both arms' ends on a walk, laid every
movement through it as a band grown by a walk with its own two ends turned, and laid the movements again at
their own size — all of it under the sections, twice over. The fixture's one such node was 288 triangles
that drew nothing; the suite's city spent a quarter of its ground on 48 of them.

**Which nodes are such places is the town's answer and not the picture's** (`Paving.Through`, read off
`RoadCuts.RunsThrough`): two arms, and the kerb corners of the one standing where the other's do, held to
the figure that makes two pieces one line. Asked of the corners rather than the node, one predicate covers a
swept bend, an unswept near-straight and a disc laid on a road nothing ends at, and refuses a step in the
kerb — two widths, or a pair that kept its fillet — which is still a box and still drawn as one.

**A movement's band has no ends in the picture anywhere**, not only there: a movement runs from the end of
one lane to the start of another (TER-5d), a lane end stands inside its own arm, and an end turned on a
walk is ground the arm's section lays. The fans at every mouth in the town were buried under the road.

## 2026-09-07 — a road is one cross-section on one set of stations, not four shapes painted over each other

A metre of a straight was written four times: the town's tarmac grown by a walk in the pavement's edge
shade, that same shape a line's width smaller as tarmac, the walk's own band, and the carriageway over the
top. Nothing was wrong with the picture — the shell's rim and the kerb line fell out of it — but a straight
came out as one full-width quad per pass, four deep, and TER-7b says the ground is covered once.

**The seam is the reason it was ever laid that way.** Two bands that abut are two offsets of one curve, and
laid as two shapes each is sampled to its own curvature: they meet along two different chains of chords and
stand a chord's sag apart at worst. Overlapping them hides that, which is what the passes were doing.
`GroundMesh.Strips` closes it instead — the stations are walked once and every band is struck on them, so
the seam is one offset evaluated once and the bands share it. **The seam is one position and two vertices**,
because a surface is a vertex attribute and the band either side needs its own.

**What each side of a road carries is the town's answer and not the picture's** (`Paving.Sections`), cut by
the same predicate the pavement's own runs are cut by, so a run that wraps a road and that road's sections
cannot disagree about where the concrete is. A side with something standing against its kerb carries the
pocket of asphalt that is really there (TER-3c.7) and no concrete at all, stated as bands of no width
rather than as a case of its own.

**It is laid last of the ground, where the carriageway used to be, and that is not tidiness.** A run's
corner turns a kerb stroke round the place two runs hand over, and where the arms are of different widths
that turn crosses the lane; what keeps the stroke off the asphalt is still the road drawn back over it
(TER-3d). That is overlap, and it belongs to the junction rather than to the road.

**What is left is one missing tool and not four problems.** A junction's ground is the union of movements
that cross by construction (TER-5), a car park's is a box and the pockets round it, a bridge's is four
sizes of deck and water, and each needs the boundary of a union of overlapping shapes. The verge is a
question rather than a gap: it is one rectangle under the whole town
([known gaps](../../../../docs/index.md#known-gaps)).

## 2026-09-07 — the band is closed with half a round, and only where it stops

A whole disc at each end of a pavement run buried a fan of triangles in the concrete at nearly every seam
and left a free-standing circle wherever a graze had left a short run. The ends the band actually stops at
are the paving's answer now (`Paving.Caps`) and this slice strikes one half-round apiece, facing out — a
whole circle where two runs hand over says the picture believes both ends are open.

## 2026-09-01 — the kerb line moved onto the kerb, so a lane measures what it is

The ground was always right at 3.6 m kerb to centreline, but the line was struck *inside* the carriageway,
so nothing a ruler could catch on the frame was 3.6 — a drawing that argues with its own numbers. The
paint is struck outside and the road drawn back over it (TER-3d), making it a painted kerb rather than an
edge line. The pavement's rim did not move, being a shade rather than paint with no figure quoted against
it.

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
