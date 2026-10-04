# Drawing the town — requirements

What the picture must be. The machine underneath — the window, the device, the pipelines, the command
buffers and the crossing budget — is [runtime](../../../runtime/docs/requirements.md); what the ground
*is* is [world/terrain](../../../world/terrain/docs/requirements.md). **Every rule the picture answers to
is stated where the thing it is about lives**, and this page says which; how the renderer meets each of
them is on its types. The one thing stated here is how a sheet is stored.

## What the picture answers to

- **The frame's crossing count is O(1) in the size of the town** — [goals.md](../../../../docs/goals.md)
  rule 1, arranged by [runtime](../../../runtime/docs/requirements.md#the-crossing-budget) and gated by
  `CrossingGateTests`. What a frame fills is the memory of the image it is drawn into (`TownRenderer`).
- **The ground drawn is the ground answered for** (`TER-7`): one mesh from the plan's shapes, textured from
  the world origin so a triangulation cannot show (`GroundVertex`).
- **The ground is a stack of layers, each a region of the town's boundary** (`TER-7b`), the two kerbs are
  strokes along their shells (`TER-3d`), and a deck is the one band (`TER-3b.1`). How a shell is cut — the
  line for the picture and the fill for the kerb, a stroke's corners and hooks — is `GroundMesh`. **A bridge
  over other roads is the stack again, drawn over the bodies on the ground and under the bodies on it**
  (`TER-7b`, `PHY-1a`): the ground's last part (`GroundPart.Above`) is a second draw, and the bodies on the level
  above a second run of instances (`TownRenderer.SpritesAbove`).
- **Paint** is engine-drawn and governed by [world/road](../../../world/road/docs/requirements.md#markings);
  it is laid by [GroundMesh.Paint.cs](../GroundMesh.Paint.cs), above the ground from
  `GroundMesh.FirstMarkVertex` on.
- **A body is drawn from a sheet indexed by what the simulation already knows** — a facing row and a walk
  column, a lit lamp, a variant, and a sheet of its own for a state it cannot come back from (`PER-18`):
  `SpriteInstance`, `PersonSprites`, `SignalSprites`, `TownSprites`.
- **What reads the picture back** is [app/debug](../../debug/docs/requirements.md)'s wireframe and ground
  page (`OBS-2o`, `OBS-2v`); a picture with no window under it is [app/shot](../../shot/docs/requirements.md)'s
  (`SHT-1`).

## How a sheet is stored

**A sheet is cut on a grid, and stored at that grid and no finer.** The grids are `ArtPixelsPerMetre` for
the ground, the buildings and the props and `CarSpritePixelsPerMetre` for the cars, both on `ViewFigures`.
`qq art` measures a sheet against its own grid, and moving a grid is moving that figure and re-cutting
every sheet to it. **The zoom runs past the grid** — to `CameraMaxSpriteMagnification` times the car grid
(`Camera2D`) — so a grid is how soft the art may go at the closest framing.

**The fleet's grid is three times the ground's and not one and a half**, because `CAR-12` measures a
tyre's overhang off the silhouette in the picture, and at 96 px/m a texel is 10 mm of car.

**A sheet is stored as WebP**, lossily wherever the error over its opaque pixels allows and losslessly where
it does not, decided per sheet; **the alpha is lossless in every case**, a sprite's edge being its
silhouette. Nothing decodes by name, so both heads read either.
