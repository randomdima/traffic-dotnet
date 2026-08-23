# The shot — requirements

A picture of this town with no window under it: one frame, or several staged and tiled into a sheet
somebody reviews. What is judged on those pictures is [verification.md](../../../../docs/verification.md)
(tiers 3 and 4); what is drawn on them belongs to [app/render](../../render/docs/requirements.md) and
[app/hud](../../hud/docs/requirements.md). **This slice stages, composes and annotates; it draws no
town.**

**SHT-1** **The frame is the game's own picture and carries nothing about itself.** Everything this
slice has to say about a frame — what it is of, where it was taken, when, at what scale — is composited
**under** it as a band, never over it and never inside it.

- A frame taken through a second drawing path, or with a review's furniture painted into it, is a
  picture of that path rather than of the game. The panels and the layers on it are the same
  `Hud.Interface` the windowed game draws.
- A picture asked for without a caption is byte for byte the picture this build has always written, so
  a frame can still be laid beside another build's frame of the same ground.

**SHT-2** **A picture for review says how to take it again.** The band carries the map, the label, the
`--ui` words, the span in metres, the centre, the pixels per metre, the tick, the seconds and the seed,
and a graduated bar for the scale.

- **The bar stands on the same ladder of round numbers the legend and the ruler are graduated on**
  (`Ladder`, OBS-2e): a review picture whose bar disagreed with the one inside the frame would be two
  answers to one question. It is the bar's whole length that carries the figure, so the length is what
  is rounded.
- **A row that cannot reproduce the picture is worse than a small one**: the figures shrink to fit the
  cell and only the head is allowed to lose its tail.
- It is lettered from the interface's own glyph sheet, so a review picture needs no font installed and
  reads as part of the project.

**SHT-3** **Several subjects are one sheet**, tiled in reading order, separated by a gutter in a colour
the town never draws. A sheet holds at most nine cells, all photographed at one size — cells at two
framings are a comparison nobody can make — and **a sheet with no cell drawn into it is never written**.

**SHT-4** **A sheet is asked for as a document, not as flags.** `--sheet FILE.json` and `--sheet -`
read it; the figures on the document are the defaults and a cell states only what it differs in.

- **A member the schema does not carry is an error.** A misspelt `secondes` that quietly photographed
  tick zero is what the format exists to prevent.
- A cell names what it is of and, where it helps, what a reviewer is being asked to look at.
- Nothing the document can ask for is unavailable to `--shot`: it is the same request, several times.

**SHT-5** **A picture is never separated from its provenance.** The same figures the band draws are
written into the PNG's own text chunks and into a report beside it — `<picture>.png.json`, the whole
name with `.json` after it, because notes named by swapping the extension overwrite the document that
asked for them.

**SHT-6** **What could not be photographed is said, and what was photographed is still written.** A cell
that comes back nearly flat is reported and kept: a staging mistake and a legitimately flat frame — the
menu over an empty world — look identical from here, and refusing to write one of them would make the
sheet unable to photograph the interface.

## What this slice may know

`app/shot/` depends on the shell it photographs — the camera, the renderer, the interface, the debug
layers — and on `bench/` for the proving ground's instrument, exactly as
[app/main](../../../../docs/slice-map.md) does. **Nothing depends on it but `app/main/` and the
workshop**, so the arrow stays pointing down: the e2e visual tier stages its scenarios through
`ShotRun` and tiles them with `Sheet`, and there is no second copy of either.
