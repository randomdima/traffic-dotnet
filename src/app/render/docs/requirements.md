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

**The ground mesh is a stack of layers and each layer is a union**
([TER-7b](../../../world/terrain/docs/requirements.md#one-geometry)), so **the order the pieces are
appended in is the whole of the answer** and depth does no work: one indexed draw, one pass, nothing
sorted, and the piece appended last is the piece that shows.

**The two layers of ground are one list of shapes read at four sizes** (`GroundMesh.Grown`,
`GroundMesh.Pavement`): every road, every line a car is turned through a box on, every wedge their kerbs
turn on and every car park, at a walk beyond its own size and a line's width inside that, then a line's
width beyond its own size and at it. Under them is the grass, between them the water, the shore and the
decks, and above them the paint.

- **A union is stated by drawing its pieces over one another.** Nothing is trimmed, clipped, cut short or
  handed over to a neighbour, and no piece here knows what is beside it. A junction, a car park's mouth, a
  bridge and a dead end therefore cost exactly what a straight costs.
- **A rim and a kerb line are what a layer leaves of the one under it**: the layer twice, a line's width
  apart, the outer pass in the line's shade and the inner in the surface's own. Since every fill follows
  every rim, what survives is a stroke on the union's outer boundary and nothing where two of its pieces
  meet — so a line has no ends to close and no corners to turn. Which pass is the surface's own size is the
  line's to say: the pavement's rim is struck inside the band (`walkM`, then `walkM - edgeM`) and the kerb
  line outside the lane (`halfM + kerbM`, then `halfM`), which is TER-3d. The shore is drawn by the same
  trick and always was.
- **What breaks the kerb line over a car park's mouth is the lot's own tarmac** (`GroundMesh.Build`), laid
  between the stroke and the carriageway because that is where the answer puts it — not a stretch of kerb
  worked out and left unstruck.

**The picture is `GroundShapes.At`'s order, forwards.** That method walks this same list from the end and
takes the first shape covering the point, so the two are one list read in two directions and a shape added
to one is added to the other at the same place. **This is what the layering is for**; the picture coming
out right is a consequence rather than the reason.

**The two lists disagree only about their ends.** A ribbon's ends are square where the answer swings the
growth round the band's last cross-section, so **an end that really stops carries the offset of that
segment** (`GroundMesh.Grown`) — a rectangle a growth deep whose corners turn about the road's two end
corners. **Only one that really stops**: an end another arm leaves is inside what that arm draws where the
two are one line and inside the wedge their kerbs turn on where they are not, and a movement's ends stand
inside a junction to a one. Capped everywhere regardless, a city spent a fifth of its ground on rounds
nothing could see.

**Shared is shared, and the mesh holds one corner per corner** (`GroundMesh.Vertex`) — but here that is a
dedupe and not a seam. It is what keeps a ribbon laid at a size and the same ribbon laid a line's width
inside it from each carrying their own copy of the stations they agree on. **The marks are not welded**
(`GroundMesh.FirstMarkVertex`) — a dash, a bar and a stripe are quads of four corners each, and anything
reading back what was painted reads them that way.

Marks are the one layer above the ground rather than in it — a dash, a bar, a stripe sits *on* the
surface it belongs to (`TER-7`) — and they are the one layer that does **not** overlap within itself, paint
being a multiplying tint. `GroundMesh.FirstMarkVertex` is where it starts.

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
