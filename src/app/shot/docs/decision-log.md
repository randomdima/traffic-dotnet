# The shot — decisions

Why this slice reads as it does. A superseded decision is deleted from here, never annotated.

## The caption goes under the frame, not into it

The band could have been drawn through the interface's own overlay, which is one pipeline already in
front of the camera and would have cost no compositing at all. It would also have painted a strip of
the town out, and — worse — made every review picture a picture of the game *plus this slice*. The
visual tier lays a frame beside the same ground photographed by the godot-dotnet build; a frame with a
caption burnt into it cannot be laid beside anything.

So the frame comes back from the renderer untouched, and the band is composited under it on the CPU
afterwards. `--shot` alone still writes exactly the pixels it wrote before this slice existed.

## The band is lettered from the game's glyph sheet

Drawing text into an image wants either a font library or a font. ImageSharp's core package cannot draw
text at all — that is `ImageSharp.Drawing`, a second dependency — and any system font would make a
review picture depend on what happens to be installed on the machine that took it.

The interface's own sheet is already embedded in the assembly and already cut to a documented grid, so
`GlyphStamp` resamples it once per text height and blits from it. The same choice pays for the eight
rectangles the band is made of: they are written straight into the pixel rows rather than through a
geometry library taken on for nothing.

## The bar's whole length stands on the ladder, not each graduation

The scale legend inside the frame puts a round number of metres between graduations and lets the bar's
total fall out of the count, because every graduation there carries a figure. The band has room for one
figure, so the same arrangement produced bars reading "14 m" — a number nobody measures with. Here the
round number is the length and the four segments are legibility.

Both instruments still ask `Ladder` what a round number is. Two ladders would be two answers to the
question the pair of pictures exists to compare.

## The tiler moved out of the test project

`ContactSheet` was in `tests/e2e/`, which was right while the visual tier was the only thing that tiled.
`--sheet` tiles for the same reason and to the same format, and a copy in the engine would have been a
second answer about gutters, reading order and what an empty sheet is. It moved to `app/shot/Sheet.cs`;
the visual tier calls it, and `VisualScenario` now derives its cell ceiling from `Sheet.Columns`
instead of repeating the grid rule.

## The notes are the picture's whole name plus .json

`Path.ChangeExtension` was the obvious way to name the report beside the sheet, and it destroyed the
request document the first time a sheet was asked for as `junctions.json` and written as
`junctions.png`. The report is `junctions.png.json`, which cannot collide with anything that asked for
it.

## A nearly flat cell is reported, never refused

The visual tier fails a frame carrying fewer than 32 colours, because a scenario there is always a
picture of ground. A sheet is not: `--ui menu` over no town is legitimately a panel on black. So the
count is still taken and still printed, and the picture is still written — the reader is told, and
decides.
