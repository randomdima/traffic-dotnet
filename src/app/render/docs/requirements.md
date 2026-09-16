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

**The ground mesh is a stack of layers and each layer is one region**
([TER-7b](../../../world/terrain/docs/requirements.md#one-geometry)), so **the order the pieces are
appended in is the whole of the answer** and depth does no work: one indexed draw, one pass, nothing
sorted, and the piece appended last is the piece that shows.

**Two layers of ground beside a road are drawn, and both of them are the same boundary** (`GroundRings`,
[TER-3c.3](../../../world/terrain/docs/requirements.md#the-pavement)): the walk out to the pavement's outer
face, and the carriageway, **each the ground within one of the figures that boundary is read at**. The
carriageway is inside the walk, so it is laid over it and **the order is the whole of what states the
difference between the two** — the tarmac over the concrete, the grass under everything, the water over the
walk that reaches its shore, the deck over the land.

**The two kerbs are not layers and are not cut out of one.** Each is a stroke laid about a closed
line at a kerbstone's width, with a mesh of its own and no triangle taken from any fill (`GroundMesh.Stroke`,
[TER-3d](../../../world/terrain/docs/requirements.md#the-edge-line)): **the town's kerb along the driven
ground's boundary, drawn last of the ground; the walk's own kerb along the pavement's outer face, drawn
straight after the walk.** A fill is thinned for the picture (`GroundMesh.ThriftM`), so a kerb cut as the
difference between two fills is a kerb whose width is whatever two thinnings left of it — and the strip the
thinning falls short by is the grass that used to show between the layers. A stroke is two hundred
millimetres wherever its line runs, and it covers that strip.

**A shell is read as one line and the fill is that line thinned** (`ShellFill.Outline`, `GroundMesh.Line`
and `Filled`). **A fill has no edge anybody sees**: every shell filled here carries a kerb along its
boundary and the kerb is laid last, so where a fill cuts a corner the layer beneath it shows through and the
stone goes over both. The one thing that may not happen is a fill's edge reaching out from under its kerb,
so **the fill's whole budget is `GroundMesh.HiddenShare` of a kerb's half-width** — and the two readings are
nested, both thinnings of one flattening, so how far they part is that budget rather than the sum of what
each strays from the arcs.

**So the line is cut for the picture and the fill for the kerb**, and they are not the same question. What a
frame can tell apart decides the line; what a kerbstone covers decides the fill.

**And how much of a turn one chord of that line may stand for is a budget of its own**
(`GroundMesh.ChordTurnRad`), because a budget in metres says nothing about a bend: the step a sag earns is
`2·acos(1 − sag/R)`, which grows as the radius shrinks, and a quarter turn of a fifth of a metre bows four
centimetres off its own chord — inside the sag, and a corner drawn as a polygon. **What is lost on a bend is
direction rather than distance**, so it is bounded as an angle. It is also what cuts a kerb for its own
ribbon: a stroke's outer edge goes round a circle a half-width wider than its line's, which the angle covers
several times over, and a segment's offset being a segment there is nothing between two corners left to
refine.

**A stroke is the ground within half its width of its line and nothing outside that** — at a bend, at a
corner and at the tightest hook the boundary has (TER-3d). **A corner is one cross-section on the bisector
wherever that stands for the turn, and the swept sectors where it does not.** A cross-section laid on the
bisector at the half-width pinches the ribbon to `w·cos ½θ` across the corner, which is the figure a chord
bows off the arc it stands for — so it is held to the same sag every bend here is drawn at, and a corner
turning harder than that sag allows is fanned about the place it stands at instead. **A mitre is not the
third option**: struck where the two offset lines meet it stands `½w(sec ½θ − 1)` outside the line, which is
the one thing the rule forbids. **And where the line turns tighter than half the width, that edge stops at
the middle of the turn**: carried further it comes back on the far side of the line, for the same reason.
`--bench outset` reports how tight a boundary turns and how many of those hooks it carries.


**A car park, a junction, a bridge and a dead end are not shapes here and never were.** A junction is the
union of the movements that cross in it (`TER-5`) and a car park the union of the ways that reach into it
(`BayLines`, GEN-4b) — and so is a road, and so is the whole town: what is drawn is the one boundary all of
them share. **The one pass a car park is not in** is the bay stroke, which is a bay's own marking.

**A solid line is a dashed one whose dash is the whole run**, so a bay's stroke is laid by the machinery
that lays a lane's centreline (`GroundMesh.DashRun`) and there is no second way to paint a straight mark.

- **A rim is what a layer leaves of the one under it**: the region twice, a line's width apart, the outer
  pass in the line's shade and the inner in the surface's own. What survives is a stroke on the region's own
  boundary — so a line has no ends to close and no corners to turn, the boundary having turned them. **A
  deck's edge and the shore are drawn that way; neither kerb is.** A kerb is a stroke about a shell (TER-3d),
  which is what lets it be a constant width on a bend as on a straight and stand half either side of the
  line it parts.
- **What breaks the kerb line over a car park's mouth is the boundary itself.** A lot's ways are driven
  lines like any other, so the boundary runs round the outside of a row of bays and the stroke with it —
  there is no stretch of kerb worked out and left unstruck.

**The picture is `GroundShapes.At`'s table, read the other way.** That method asks which line of the town
lays a point and how far off the kerb it stands, against the same distances these layers are filled at — so
the two are one table read in two directions and a distance added to one is added to the other. **This is
what the layering is for**; the picture coming out right is a consequence rather than the reason.

**A bridge is the one piece of ground drawn as a band.** Its deck's width is authored per bridge rather
than derived from anything the boundary knows (`TER-3b.1`), so it is a ribbon about the road's own line —
laid, like the rest, at full size in the edge shade and again a line's width smaller in its own. **And the
deck alone**: the pavement that used to be carried across one at the width it has on land was a line beside
a road with arithmetic of its own, and it is gone (`TER-3c.3`). Until the boundary can cross water the
margin outside the carriageway is deck all the way out, which is the gap
[docs/index.md](../../../../docs/index.md#known-gaps) names.

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
