# Drawing the town — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md).

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
