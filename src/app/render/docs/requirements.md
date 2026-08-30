# Drawing the town — requirements

What the picture must be, and the shape of the work that produces it. The machine underneath — the
window, the device, the pipelines, the command buffers — is [runtime](../../../runtime/docs/requirements.md);
what the ground *is* is [world/terrain](../../../world/terrain/docs/requirements.md).

## The frame's shape

**The frame's managed→native crossing count is O(1) in the size of the town.** Draw counts live in a
buffer the CPU writes rather than in the calls themselves, so a town of twelve cars and a town of five
hundred cost the same number of crossings. **A frame that makes one call per car is the cardinal sin
here**, and the gate is `src/tests/gates/CrossingGateTests.cs` rather than a habit.

**Everything an instance needs is in the instance.** A sprite is a row in an array the GPU reads; adding
a per-body branch to the draw path is how the count stops being constant.

## Ground

**Ground is drawn as one continuous surface per type**, its texture **anchored to the world origin** and
not to the shape being painted. That anchoring is what makes the triangulation invisible: cut a shape into
triangles differently and the picture does not change.

Every ground texture must be **wrap-seamless and mipmapped** — un-mipped tarmac shimmers the moment the
camera pulls back.

**Draw order is painter's work with nothing testing what is underneath**: the verge over the whole world;
the pavement (every ribbon, disc, corner band and lot read out a walk's width bigger and drawn twice for
its edge line); the water; the decks; the paved slabs; the lots; the carriageway; the junction discs; the
corner fillets.

## Paint

**Everything painted is engine-drawn primitives, never art**, and the rules that govern it belong to the
thing that owns the coordinate ([world/road](../../../world/road/docs/requirements.md#markings)). Two
bind the renderer:

- **Everything is drawn in its own frame.** Anything laid in the world's frame draws square while the
  ground underneath it draws true.
- **Paint sits on the surface it belongs to**, which follows from TER-7 and is checked on rendered frames
  because no numeric check answers it (VER-9).

## Sprites

A body is drawn from a sheet indexed by what the simulation already knows — a walk column and a facing
row, a signal's lit lamp, a car variant — so the picture reads state rather than being told it. **The lit
frames of a signal head are made from the dark one offline**, not drawn separately.

**A state a body cannot come back from the same tick gets its own picture**, and there are two: a wrecked
car and a body lying in the road (`PER-18`). Both are one frame with the head or the nose along `+x` and
both are turned to their own heading, which is what separates them from a walker on its feet — that is
drawn upright from a sheet of eight facings, because a standing body looks the same whichever way the
camera is held.

## How a sheet is stored

**A sheet is cut on a grid, and stored at that grid and no finer.** The grids are `ArtPixelsPerMetre`
for the ground, the buildings and the props and `CarSpritePixelsPerMetre` for the cars, both on
`ViewFigures`. **The zoom stops where one art texel is one display pixel**, so a texel past the grid
can never reach the screen: it is downloaded, decoded, packed into the atlas and uploaded to be
minified. `qq art` is what measures a sheet against its own grid, and moving a grid is moving that
figure and re-cutting every sheet to it.

**The fleet's grid is three times the ground's and not one and a half.** `CAR-12` asks that a
variant's tyres show past its own bodywork by a few millimetres, measured off the silhouette in the
picture rather than against another number in the same file — at 96 px/m a texel is 10 mm of car and
that rule has somewhere to live.

**A sheet is stored as WebP.** The art is continuous-tone rather than palettised and PNG holds it at
around four times the size. **Lossily wherever the sheet can take it and losslessly where it cannot**,
decided per sheet by measuring the error over its opaque pixels — a building's flat wall costs chroma
subsampling nothing and a walker cut into sixty-four small frames a great deal, so one quality across
a town is the wrong instrument. **The alpha is lossless in every case**: a sprite's edge is its
silhouette, and a soft one shows at every zoom where a softened colour does not. Nothing decodes by
name — ImageSharp reads a file by its header — so the two heads take either without being told which.

## A shot needs no window

An offscreen frame is the same recording against a different target, and it is what every render check is
taken with. Three consequences the recording never sees on its own, and each must be arranged by whatever
takes the shot: the interface's pixels are the image's own with no desktop under them; a town that has
never ticked is a town of bodies standing on their spawns; and the pointer is put outside the frame, so
nothing is drawn hovered.
