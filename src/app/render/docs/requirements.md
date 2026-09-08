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

**The ground mesh is a partition and not a stack** ([TER-7b](../../../world/terrain/docs/requirements.md#one-geometry)):
no two of its triangles cover the same square metre, surfaces meet edge to edge along shared vertices,
and every rim and edge line is a strip of its own rather than the residue of a larger piece repainted
smaller. Depth does no work here — nothing is underneath anything — so the order the pieces are appended
in decides nothing about the picture, and the wireframe
([OBS-2o](../../debug/docs/requirements.md)) reads as the town's surfaces and their seams.

**What keeps it, where it is kept, is one primitive**: a set of bands between consecutive offsets of one
curve, **struck at one set of stations**. Two shapes laid separately are each sampled to their own
curvature and meet along two different chains of chords, so they stand a chord's sag apart at worst —
and the only ways to close that are to overlap them or to leave the ground beneath showing. Struck
together they share the seam, because the seam is one offset evaluated once. **A road is laid this way
end to end**: the carriageway, the kerb line either side of it, the two bands of pavement and the two
rims are seven bands of one cross-section, and what each side carries over each stretch is the town's own
answer rather than the picture's (`Paving.Sections`). **A band of no width is how a side says what it has
not got**, so there is one shape and not a case per side.

**The pavement's own runs are laid the same way** — a run that wraps anything but a road is the kerb line,
the walk and the rim as three bands of one cross-section on the run's own line (`GroundMesh.Run`), the
round that closes a run is that run's own concrete and carries no rim of its own, the corner two runs hand
over at is the turn's own wedge, and a car park is its box and that band with nothing grown beneath either
— the pocket at its mouth is the street's bare side, laid a whole walk out.

**A junction is a box between its arms' cuts, laid once** (`Paving.Boxes`, `GroundMesh.Box`): each arm's
section stops where the box takes over from it, and the box is the outline the pavement's own kerb line
encloses, walked run to run and turn to turn round the corner. Nothing is grown under anything. **A
junction the road runs through as one line has no box** (`Paving.Through`,
[TER-5b](../../../world/road/docs/requirements.md)): its two sections meet edge to edge.

**Where the ground is still painted over itself** — a junction whose outline crosses itself, a car park's
mouth, a bridge, and the verge under all of it — that is the gap rather than the design
([known gaps](../../../../docs/index.md#known-gaps)). **Union ground goes under the pavement and the
carriageway over it**: the pocket beside a road with something standing against its kerb is laid before the
rounds at a car park's mouth, on the road's own stations, and the carriageway is laid last of all (TER-3d).

Marks are the one layer above the ground rather than in it — a dash, a bar, a stripe sits *on* the
surface it belongs to (`TER-7`) — and they do not overlap one another either. `GroundMesh.FirstMarkVertex`
is where the second layer starts.

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
