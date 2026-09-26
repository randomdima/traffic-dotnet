# The shot — decisions

Why this slice reads as it does. A superseded decision is deleted from here, never annotated.

## The caption goes under the frame, not into it

Drawing the band through the interface's own overlay was free and would have made every review picture a
picture of the game *plus this slice* — and the visual tier lays a frame beside the same ground shot by
the godot-dotnet build, which a burnt-in caption cannot be laid beside. The frame comes back untouched
and the band is composited under it on the CPU.

## The band is lettered from the game's glyph sheet

A font library is a second dependency and a system font makes a review picture depend on the machine that
took it. The interface's sheet is already embedded and already cut to a documented grid, so `GlyphStamp`
resamples it; the band's eight rectangles are written straight into the pixel rows for the same reason.

## The bar's whole length stands on the ladder, not each graduation

The in-frame legend puts a round number between graduations because each one carries a figure. The band
has room for one figure, so that arrangement produced bars reading "14 m". Here the round number is the
length and the segments are legibility. Both instruments still ask `Ladder`, because two ladders would be
two answers to the question the pair of pictures exists to compare.

## The tiler moved out of the test project

`--sheet` tiles for the same reason and to the same format as the visual tier, and a copy in the engine
would have been a second answer about gutters, reading order and what an empty sheet is. `ContactSheet`
became `app/shot/Sheet.cs`, and `VisualScenario` derives its cell ceiling from `Sheet.Columns`.

## The notes are the picture's whole name plus .json

`Path.ChangeExtension` destroyed the request document the first time a sheet was asked for as
`junctions.json` and written as `junctions.png`. The report is `junctions.png.json`, which cannot collide
with what asked for it.

## A nearly flat cell is reported, never refused

The visual tier fails a frame under 32 colours because a scenario there is always a picture of ground. A
sheet is not — `--ui menu` over no town is legitimately a panel on black — so the count is printed, the
picture is written, and the reader decides.

## The town is stood up once for a sheet, not once a cell

A cell is `ShotRun`'s own frame, so a sheet of nine stood the same town up nine times — most of a minute
each, against a fraction of a second to draw a frame. `TownStanding` holds the town from cell to cell, and
the sheet of nine framings the perimeter was reviewed on went from **16½ minutes to 2:07**.

**Only a town nobody has ticked is handed on.** A cell that asks for `--seconds` runs the simulation, and a
town cannot be wound back to where the last cell left it; handed on anyway, the caption would say a tick
the picture was not taken at (SHT-2). So a ticked cell takes the town with it and the next stands its own
up. The staging path is still the one path (SHT-4): `ShotRun.Take` lays the town when it is handed none.

## The machine a frame is drawn with is a thing, so a run can be photographed while it is running

`ShotRun.Take` opened a device, laid a renderer and read every sheet, drew one frame and let all of it go —
right for a picture of a moment and wrong for a run being driven, which wants a frame after each step of a
town it is ticking itself. Standing the town up per frame is a minute a picture, and drawing it somewhere
else is a second drawing path, which `SHT-1` refuses. So `ShotStage` is the expensive half held as an
object, with `Draw` the one place a frame is composed; a hand-driven run
([app/drive](../../drive/docs/requirements.md)) keeps one and asks for a frame whenever its script says so.
**A `--shot` is the same pixels it was before the split.**
