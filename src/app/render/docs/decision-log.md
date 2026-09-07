# Drawing the town — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md).

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
