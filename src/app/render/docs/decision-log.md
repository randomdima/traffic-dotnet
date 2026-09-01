# Drawing the town — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md).

## 2026-09-01 — the kerb line moved onto the kerb, so a lane measures what it is

The kerb line was struck a line's width *inside* the carriageway, the way the pavement's edge shade is
struck inside the walk. The ground was always right — a lane is 3.6 m from the kerb to the centreline,
straight or bent, and the picture was measured at the tightest bend the shipped maps carry to prove it:
7.20 m kerb to kerb, the centre dash within 2 cm of the middle. But nothing a ruler could catch on the
frame *was* 3.6: between the paint and the dash lay 3.23 m, and to the middle of the dash 3.35 m. The
figure was measured off a picture three times and disagreed with the build three times, which is a
drawing that argues with its own numbers.

The paint is now struck outside the carriageway and the road drawn back over it (TER-3d), so the asphalt
is exactly the carriageway and the line stands on the walk's innermost quarter of a metre — a painted
kerb rather than an edge line. A tape from the asphalt's edge to the middle of the centre line now reads
3.6 m, which is the figure `SimConfig.LaneWidthM` carries.

**The pavement's own rim did not move.** It is a shade and not paint, and no figure is quoted against
the width of a walk the way one is against a lane. What did move with the line is the strip that breaks
it over a car park's mouth: it erases the line where the line now stands, or the break would be painted
across the lane instead.

## 2026-08-30 — the art was never pixel art, and was stored as though it were

`ArtPixelsPerMetre` read *"21 art pixels per metre blown back up ×3"*, which said the sheets carried
21 px/m of information stored at 63. **They did not.** Tested for the ×3 block structure and it is not
there; the sheets hold between 96,000 and 263,000 distinct colours each, at about 1.2 bytes a pixel.
This is continuous-tone art that PNG cannot compress, and the grid in that comment was a claim the
files never kept. Both halves of it were costing something: 26 MB of art for a town drawn at 13 px/m,
and a format chosen for flat colour holding an image that has none.

So the grid moved to 31.5 px/m and the storage to WebP. **31.5 and not the 21 the comment claimed**:
the default view is 70 m over the short side and the zoom runs well past it, and 21 would have gone
soft long before the zoom ran out. **The fleet did not move at all** — `CAR-12` measures a 9 mm tyre
overhang off the silhouette in the picture, which at 48 px/m is under half a texel, and the cars are a
sixth of the art and the thing most closely looked at.

**The suite is what found both of those.** Halving everything was tried first, and the unit tier came
back naming exactly what could not be resampled: `CarSpriteTests` that the tyres no longer showed past
the bodywork and that the sheets had picked up loose pixels, `PersonCatalogTests` that a walk sheet was
no longer a whole number of cells, `CarSpriteTests` again that the tread's aspect had moved off
`TreadPitchM`. Each of those is a relation between a picture and a figure, and none of them is
something a person looking at a frame would have caught.

26.0 MB of art to 2.7, and the atlas from three 4096 pages to one.

## 2026-08-30 — a sheet is a rectangle of a page, not a descriptor

Every picture the town draws with used to be a texture of its own, reached out of an unsized array of
samplers by a number in the instance and `nonuniformEXT`. It read well and it cost the town nothing that
could be seen. It is gone anyway, for two reasons that arrived together.

The first is that **descriptor indexing is a Vulkan extension and nothing else has it**. Neither WebGPU
nor WebGL2 can index an array of samplers at run time — WGSL has no such thing at all — so a browser
could not have drawn this town without a second answer to "which picture", and a second answer is two
grammars for the same question and two shaders that disagree within a month.

The second is that it was never free. The bodies are one instanced draw over mixed looks, so a wave
routinely spans quads drawn from different sheets, and a divergent descriptor index is what a driver
scalarises. An array **layer** is a coordinate: no divergence, no waterfall, one descriptor.

So [SheetAtlas](../SheetAtlas.cs) packs the lot into the layers of one array texture and the instance's
sheet number now indexes a table of *places* — a uniform block the vertex stage reads, which is a legal
dynamic index everywhere. Nothing above the renderer moved: `TownSprites` and the rest still write a
sheet number and a coordinate inside that sheet, and the transform to the page happens in the shader.

**The five ground surfaces were not atlased.** They are different sizes, wrap-seamless and mipped, and an
array texture forces one size — which would have resampled the ground the whole town stands on to fix a
problem the ground does not have. Five bindings and a `switch` keep every texel and cost a branch that is
uniform over a triangle. **The tread was not atlased either**, for the opposite reason: it is a tile, its
coordinates run outside the unit square by however many pitches a wheel lays, and a page neither repeats
nor carries mips. It has a binding of its own, and a second tiling sheet is refused at load rather than
drawn wrongly.

What it cost: the pages are three of 4096 square where the sheets were about 156 MB of textures, so the
GPU holds around 200 MB instead — the price of packing pictures of thirty different shapes into
rectangles. Two thousand square was tried first and took **seventeen** pages, because a building is
around 1200 across and a 2048 page holds one of those with a wasted strip beside it.

## 2026-08-26 — a casualty is art, not a tint

A body in the road (`PER-18`) needed a picture, and the cheap answer was sitting right there: the instance
already carries a per-instance `Tint`, so a red multiply and a quarter turn would have been two lines and
no art at all.

It was refused because **the tint is how a mark says how hard it was pressed**, and nothing else in the
town uses it to say what a body *is*. What a body is, is which sheet it samples — a wrecked car has
carried its own crumpled picture since it existed, and a walker's facing is which cell of eight. A red
multiply over the walk sheet would have been a second grammar for state, and one that says "this walker
is redder" rather than "this walker is on the ground bleeding".

So a look now ships two sheets: the walk grid, and one frame of a body on the ground. It costs one sheet
slot a look, on a list the sprite pipeline indexes by number and never binds, and it buys the pose for
free — a body on the ground is a shape with a direction, so it is drawn the way a car is, one frame turned
to its heading.

**That second sheet is cut from the first offline** by
[tools/personsheets](../../../tools/personsheets/make-down-sheets.py), on the same footing as the lit
frames of a signal head: the plant pose of the away-facing row, turned a quarter and shaded red. Drawing it
fresh was tried first and thrown away — new art of the same character is new art, and seven looks of it
were seven ways to be slightly out of style beside the walker they are supposed to be. A look's casualty
is now literally the same pixels as the look.
