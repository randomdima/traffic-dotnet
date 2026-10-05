# Drawing the town — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md); how a type
works is its own XML docs.

## 2026-10-05 — a corner of the ground is twelve bytes

**A traced city's ground is four million corners, and most of each was not its place**: a texture coordinate that
is the place over a period, three floats of a shade that is one of a handful, and a word for one of six surfaces.
A corner is now its place and one word (`GroundVertex`): three channels of nine bits up to
`GroundVertex.BrightestTint`, a little over paint's, and the surface in the five above them. **The vertex stage
divides the place by its surface's period**, so the texture is anchored to the world origin exactly as before, and
the periods ride the camera — written by the renderer from the mesh it was laid with (`CameraView.SurfacePeriodsM`),
so nothing that builds a camera knows them. A shade is filed to within 0.003 of itself, and a weld is keyed on the
word.

On OdesaOsm the mesh is 133 → 50 MB on the device and as much again off it, and laying it holds 1 774 → 1 655 MiB at
most; the corners are the same 4 165 049. The visual tier passes whole and a shot of each head is the picture it was.

## 2026-10-04 — a prefab with no picture is a plain block, and the sheet table holds 512

A traced town's buildings are 355 prefabs, most with no art yet ([citygen's log](../../../citygen/docs/decision-log.md)).
**An undrawn prefab is drawn from a sheet built for it** (`PrefabSprites`): its rounded rectangle in its look's colour
and clear past its corners, its walls darker and its door's wall lighter, at 4 px/m — so the placement can be judged,
look by look, before any of it is painted. The footprints under them went from tan to grey, so where a prefab parts
from the survey reads against it. **The sheet table went from 192 places to 512** (`SheetSlots`, both shaders): 498
sheets now, and 512 of 32 bytes is the 16 KB uniform range every Vulkan device must offer.

## 2026-10-04 — a bridge over a road is drawn above it, and so are the cars on it

The owner asked for bridges over roads drawn above them as roads of their own (`TER-7b`). **Two draws of the ground,
of its marks and of the bodies, in one recording**: the ground's parts to the paint, the ground's marks, the bodies on
the ground, the level above (`GroundPart.Above` — its decks, carriageway, kerb and paint), its marks, then the bodies
on it. Each pair is one buffer — the index buffer's last part, the underlay past `UnderlayCapacity` and the instances
past `SpriteCapacity` — bound at a second offset, because an indirect draw starting at a non-zero instance needs
`drawIndirectFirstInstance`, which this project does not ask for. The commands are recorded once per image, so a
frame still makes the five crossings it did.

- **A mark goes with the way it is about** (2026-10-05, the owner): a bridge's lanes, joins, ribbons and claims drawn
  in the ground's one run were under its own deck. A join at a bridgehead is on the ground, as its tarmac is.

- **Paint goes with its carriageway**: a run of lane dashes stops at a bridgehead (`CentrelineRuns`), and each mark is
  laid in its own level's stack.
- **The bridge's kerb is not struck across its ends**, where it is the ground's road carried on; the ground runs on
  under the deck there, so the deck's end meets tarmac.
- **The page draws one run over the whole ground**, the level above included: no traced map reaches it (`WEB-4`),
  and no other map lays a level.
- **A skid on a bridge and a walker are on the ground's run**: a mark keeps no level, and a walker is always on the
  ground (`PHY-1a`), so either on a deck is drawn under it.

## 2026-10-03 — the ground lives on the device, copied there once

`OdesaOsm` ran at 3 fps on an RX 9070 XT, each frame 326 ms waiting on the GPU with the CPU idle. The ground's
vertex and index buffers were mapped memory, and the first host-visible type on a discrete card is the host's
own RAM, so every frame fetched the whole ground across PCIe: harmless for `Odesa`'s 6 MB, and 420 MB for a
traced city's 7.57 million triangles. **The ground is now device-local, written by a staged copy**
(`GpuBuffer.Upload`) when the renderer is made and when a layer is switched (`ShowGround`). 3 → 120 fps there,
89 → 120 on `Odesa`, both at the display's own rate, and every pixel of a shot is the same. Asking for memory
both device-local and mapped was 119 fps as well, but only where the whole of the card is mapped (resizable
BAR); without it that window is 256 MB, smaller than the one city that needs it. What a frame writes stays
mapped — a few MB, and written every frame.

## 2026-09-19 — a frame is filled into the image it will be drawn into, and the image is taken first

Cars blinked as they moved. The instance buffer, the two quad buffers and the three counts were one of
each, written every frame over a swapchain of three images with no wait before the fill, so a draw one or
two frames back was still fetching slots the next fill was rewriting — a mark, a tyre or a lamp where a
car's body was. A standing town writes the same bytes twice, which is why only a moving car showed it. The
camera was per image all along, so this was a miss rather than a design.

**Every buffer a frame fills is now the image's own, and the image is taken and its fence waited on before
the fill** (`TakeImage`) — per image rather than per frame in flight, because the recording that binds them
is written once per image. **The frame is still five crossings**; what it costs is memory, about 22 MB of
overlay quads where it was 7.5 MB, and the sprites again per image. Holding an image across a frame is what
the rebuild on a resize had to learn (`DrainAcquire`).

## 2026-09-18 — each kerb wears the ground it bounds

The town's kerb was the pavement's grain in a shade of its own, and against the tarmac it read as a flat
pale band — a third white beside the dashes and the zebras. The walk's own kerb was `Edge`, the deck's rim
shade, and at 0.58 it read as a shadow across the pavement rather than as the stone at the back of it.

**The town's kerb now wears `Paint` on tarmac**, the white every dash, bar and stripe is drawn in, so the
edge of the road reads as the line down the middle of it; **the walk's own wears `Stone` on the
pavement**, the slab standing on its side, told from the walk by the light on it alone. `Edge` is a deck's
rim and nothing else. Neither stroke moved.

**One white means a kerb is told from a mark by where it stands in the mesh and never by its tint** — it
is welded ground laid before `FirstMarkVertex` — so the suite's `Tinted` stops at the first mark.

## 2026-09-16 — a mark between two ribbons is struck off the line they were offset from

Where two ribbons of driven ground touch, the merged shell says nothing: the seam is interior to it.
Cutting the two bands against each other to paint what they share would be a second merge for a line the
town already knows, so the mark is struck off the line the pair was offset from — **down a street, the
road's own centreline; between two bays, one way's line moved half its own width**, with which pairs
touch measured rather than read off the order they were emitted in.

**Which roads carry two ribbons is asked once** (`RoadArrays.LanesMeetOnItsLine`, read by the lane offset
and by the paint alike), or the picture and the lanes could disagree about it. **Where a run of paint stops,
and how far short of an arm's zebra and bar, is the road's** (`CentrelineRuns`, `Crossings`, `StopBars`);
this pass decides the pitch, the width and the shade.

## 2026-09-16 — a fill has no visible edge, so the line is cut for the picture and the fill for the kerb

A boundary was read once and every layer laid from that reading, at a tolerance argued from the zoom the
camera opens at. But **every shell filled here carries a kerb laid over it**, so where a fill cuts a corner
the layer beneath shows and the stone covers both: the one defect a thinning can cause is a fill's edge
reaching out from under its kerb. So the fill's whole budget is `HiddenShare` of a kerb's half-width —
75 mm, 25 mm inside the stone's own edge, asked of the mesh by
`NeitherFillsEdgeShowsFromUnderTheKerbAlongIt` — and **the line is cut for the picture**: the sag, and
`ChordTurnRad`, because the step a sag earns grows as the radius shrinks and the boundary's tightest turn,
0.14 m, came back at sixty degrees a chord inside a two-centimetre sag. **The two readings are nested
thinnings of one flattening**, so they part by the fill's own budget rather than by the sum of two strays,
which is what makes 75 mm a bound and not an estimate.

**Tried and not kept**: one reading for both, since the seam is under the stone either way; and a 120 mm
thinning of it, which saved a fifth of the ground and left 15° chords plainly faceted at 100 px/m. **A
stroke's corner became one cross-section on the bisector** wherever the sag allows — it pinches the ribbon
to `w·cos ½θ`, the figure a chord bows off its arc — and a fan only where it does not. A mitre was refused:
it stands `½w(sec ½θ − 1)` outside the line (TER-3d).

**Odesa, seed 1: 155 755 triangles to 155 133 at 8° a chord**, the fills from 37 937 corners to 34 737.
Above 8° the angle is nearly free (11° comes to 146 868) and the line's own two-centimetre thinning, a pixel
at 50 px/m, carries the rest. **A road cannot get much cheaper**: 75 mm is the 72 mm the ground was
already thinned at, and the last quarter of the half-width buys 9% for a fill edge standing at the kerb's
own, which is no margin.

## 2026-09-16 — the mesh is laid in parts, and a part is a run of the index buffer

`GroundMesh.Build` writes down where each layer started, what it came to and what it cost (`GroundPart`,
`GroundTally`). **The parts tile the mesh**, so a layer is left out of the picture by packing the wanted
runs to the front of the index buffer and shortening the draw: no recording, no pipeline, no ground laid
again. Hiding a layer by not laying it, as `RoadFigures.CarriagewayDrawn` did, costs a relay and leaves no
figure to read; cutting everything and drawing some of it keeps the tallies whole.

**Odesa, seed 1 — 155 755 triangles laid in 9.6 s.** The two kerbs are three quarters of the triangles
(59 006 and 58 124) and a sixth of the time; the two fills are the reverse (19 335 and 19 046, in 54.6 and
78.1 ms). A stroke is quick, dense and welds almost nothing; a fill is slow and `n − 2` a ring however it
is cut. **None of that is the load**: every layer together is 165 ms, and 9.3 s of the rest is the merge
behind the boundary (`LaneShell`), which is not this slice's to fix.

## 2026-09-16 — a layer is the offset filled whole, and the road is what covers it

The walk was the offset with the driven ground cut back out of it (`ArcSubtract`), and the carriageway was
then laid over it anyway: **the cut removed exactly what the next layer covered**, and carried the boundary
a second time, thinned on its own terms and free to leave grass between it and the carriageway's own copy.
TER-7b says no layer is cut to leave room for the next, so the walk is the offset filled whole.

**Odesa's ground fell from 174 634 triangles to 155 755**, the walk from 37 925 to 19 046, and opening it
by about 34 ms. Over three towns the cut layers and the whole ones came to 869 831 m² against 869 842 m²,
so the cut had been taking the carriageway and nothing else. What it adds back is overdraw, which with no
depth buffer and the grass under everything is the carriageway's six per cent of the land through a
one-sample shader. `ArcSubtract` stays, with its tests, as tooling.

## 2026-09-16 — the line a stroke is struck from runs down the middle of it

**A kerb straddles its shell** (TER-3d), half in the road and half in the concrete, where it had stood
wholly outside. It costs the ruler reading — kerb to centreline measures the lane less half a stone — and
buys what a one-sided stroke could not state: **no part of a stroke stands further from its line than half
its width**, so a kerb bulging off its own line is a defect rather than the shape.

The one-sided stroke had two corner faults. **A corner was crossed by a chord**, a notch of
`½w(1 − cos ½θ)` outside every turn and the whole half-width at a turn right round; it is swept now
(`GroundMesh.Turned`). **And the width was held through hooks** where a movement's ribbon folds, so an edge
came back on the far side of its own line; it stops at the middle of the turn (`GroundMesh.Reach`).
Odesa's tightest hook, 0.14 m, folded a kerb reaching 0.20 m and does not fold one reaching 0.10 m, so
centring cured the shipped case and the clamp guards tighter ones (`--bench outset` counts them). The
half-metre floor under `StepM` fired only where it was worst and went; `Ribbon` went into `Stroke`, a
deck being a stroke about a road's line.

**It cost 5.6% more ground triangles on Odesa, 165 353 to 174 634**, mostly on the straights, where a
cross-section with both ends off the line lays two real triangles in place of a degenerate one.

## 2026-09-15 — two fills and two kerbs, and a kerb is a stroke with a mesh of its own

The ground beside a road was four bands, each two offsets subtracted, so the curve two neighbours share
was carried twice, thinned differently, and grass showed between them. It is now **two fills and two
strokes**: the carriageway is the boundary filled, the walk is that boundary moved by one figure
(`GroundRings`) and covered by the carriageway, and **each kerb is a stroke along its own line at
`KerbWidthM`, taking no point from any fill** — two hundred millimetres wherever the line runs, which a
difference of two thinned fills cannot promise.

**This is a change to TER-7b and the owner made it**: a kerb had been what a layer leaves of the one under
it, which is now said of a rim and an edge line alone, and a kerb is a stroke along a shell of its own,
still struck from the boundary and never from the line before it. It costs a third of what it did — one
move of the outline against three moves and three cuts — and no new surface or art.
`RoadFigures.CarriagewayDrawn`, which hid the road's surface while the boundary was being looked at, was
deleted rather than turned on.

## 2026-09-15 — the ground is thinned, and the only lever on the count was the corners

**Every triangulation of the same corners is the same count** — `n − 2` a ring by any partition — and added
points only climb (`T = N + 2I + 2H − 2`), so the whole trade is how many corners the boundary is read as
against the accuracy given up. Nine ways were built and measured on Odesa (seed 1, 414 249 m² of driven
ground over 110.6 km of boundary), and eight were deleted with the menu page and the `--ui cut=WORD` that
chose between them. **Douglas–Peucker over the drawn sag won**: at 8 cm, 14 842 triangles against the
sag alone's 28 895, the best shape of the nine, and three times as quick to lay.

**Compared at equal loss, which is the only reading that means anything**, it beat a coarser sag (15 169
triangles, 7.8% slivers against 5.1%), because a sag is spent evenly and a thinning where the boundary
bends — and it improved the shape too, the corners it drops being the near-collinear ones that made the
slivers. **Visvalingam–Whyatt was rejected**: about 3% fewer corners for 43% more time. **The cap on the
longest edge was dropped** — 10% of the triangles, bought for a symptom the flips had since fixed — because
a ground vertex's texture coordinate is affine in its position: triangle shape buys nothing in this
renderer, so the count is the metric and shape the tiebreak.

## 2026-09-15 — the driven ground is drawn as the one shape it is

`ShellFill` turns the boundary's rings into triangles, so **the driven ground is one region, cut once,
with every block the streets enclose left as the hole it is** — not a ribbon per road at several sizes: a
road, a movement and a junction's wedge are one shape on the ground and one shape here. **It is cut at the
tolerance a picture is worth** (`ChordSagM`, 2 cm) and not at the fill's own tenth of a millimetre, which on
a city is a hundred times the triangles for no difference a frame shows. What it costs at load is the
merge, not the cutting.

## 2026-09-12 — a layer is a region and not a heap of pieces

The ground was a union stated by over-painting: every road, movement, wedge and car park laid at several
sizes until their outlines agreed. It cost nothing per junction, and it could never **hand anybody the
boundary** — a rim was what overdraw left, with no length, no arcs and no side, so nothing could ask
whether a pavement was a walk wide. **Each layer is now the ground within one distance of the kerb, filled
as the shape it is. This is a change to TER-7b and the owner made it.** It gives up that no piece knew what
was beside it, and buys a line with a figure on it at every edge in the picture, the answer being the same
figure compared the other way.

## 2026-08-30 — the art was never pixel art, and was stored as though it were

`ArtPixelsPerMetre` claimed 21 px/m blown up ×3; tested, the block structure is absent and the sheets hold
96 000–263 000 distinct colours each, which cost 26 MB of art in a format chosen for flat colour. The grid
moved to 31.5 px/m — not 21, which would go soft long before the zoom runs out — and storage to WebP:
2.7 MB, and one atlas page from three. The fleet did not move: `CAR-12`'s 9 mm tyre overhang is under half
a texel at 48 px/m. Halving everything was tried first, and the unit tier named exactly what could not be
resampled.

## 2026-08-30 — a sheet is a rectangle of a page, not a descriptor

Descriptor indexing is a Vulkan extension neither WebGPU nor WebGL2 has, so a browser would have needed a
second answer to "which picture"; and on the desktop a wave spanning quads from different sheets is a
divergent index the driver scalarises. [SheetAtlas](../SheetAtlas.cs) packs the sheets into one array
texture, where a layer is a coordinate. The five ground surfaces stay out, being different sizes and
wrap-seamless, and so does the tread, whose coordinates run outside the unit square.

## 2026-08-26 — a casualty is art, not a tint

A red multiply on the per-instance `Tint` was two lines and refused: the tint says how hard a mark was
pressed, and what a body *is* is which sheet it samples. A look ships a second sheet, cut from the first
offline by [tools/personsheets](../../../tools/personsheets/make-down-sheets.py) on the same footing as a
signal head's lit frames. Drawing it fresh was refused too: seven looks of new art are seven ways to be
out of style beside the walker they belong to.
