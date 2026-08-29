---
name: localgen
description: Generate, edit and refine images on the local GPU (ComfyUI in the local-ai toolbox, Z-Image Turbo + pixel-art LoRA) for this project's art — sprites, sheets, tiles, textures, mockups. It is free and takes seconds. Use whenever asked to make, draw, render, redo, refine, recolour or mask-edit an image, or to add art to raw_assets/. Triggers on "generate an image", "make a sprite", "draw", "new texture", "edit that image", "another version", "make it blue".
---

# localgen

Wraps `.claude/skills/localgen/localgen.py` (stdlib only, no pip installs). It drives ComfyUI at
`127.0.0.1:8188`, running inside the `local-ai` toolbox container from
`~/local-ai/ComfyUI/run.sh`. Nothing leaves the machine and nothing is billed, so
a re-roll is cheap — but the model is small, and the traps below are its own.

## Commands

```bash
.claude/skills/localgen/localgen.py gen "PROMPT" [--name STEM] [--size WxH] [-n N] [--transparent] [--seed N]
.claude/skills/localgen/localgen.py edit --image REF -p "PROMPT" [--strength 0..1] [--mask M.png] [--name STEM]
.claude/skills/localgen/localgen.py list [-n 10] [--filter TEXT]
.claude/skills/localgen/localgen.py show ID
.claude/skills/localgen/localgen.py status | up [--restart]
```

`REF` = a file path, an index `id` (`sedan_red-v2`), or a bare name (`sedan_red`,
newest version). Output is `raw_assets/generated/<stem>-v<N>.png`, one path per
line on stdout, and one record per image appended to
`raw_assets/generated/index.jsonl`.

The server starts itself on first use (~25 s) and stays up. `--no-autostart`
turns that off.

## The stack

Z-Image Turbo (int8) + `pixel_art_style_z_image_turbo` LoRA, on the RX 9070 XT.
Defaults: 8 steps, cfg 1.0, `res_multistep`, shift 3.0, LoRA strength 1.0,
1024x1024. `--size` must be a multiple of 16; 1024 is the native size and 512-768
is what most sprites want. `--lora-strength 0` drops the pixel-art LoRA when the
target is not pixel art. Raising `--cfg` above 1 turns the negative prompt on but
this is a distilled turbo model — it does not reward it.

**Measured:** ~8 s wall for one 768x768 image including the model load, and a
`-n 2` batch is the *same* ~8 s because the load dominates. Batch instead of
looping, always.

## Where the output goes

`raw_assets/generated/` is the scratch bench and it is where the script writes.
`raw_assets/` proper is the accepted raw art — [its index](../../../raw_assets/index.md)
states the standing rule that **nothing there is used by the project directly**: a
sprite is converted and adapted first, because a generated frame carries artefacts,
background noise and the wrong resolution.

The adapted sprite belongs in the slice that reads it, under `assets/` mirroring
the code tree — `assets/agents/car/variants/<id>/<id>.png` beside its `<id>.json`,
`assets/world/building/variants/<id>/<id>.png`, and so on
([CLAUDE.md](../../../CLAUDE.md)). The `id` in that JSON, the folder and the PNG
stem are the same string.

So: generate into `raw_assets/generated/`, `mv` the accepted version to
`raw_assets/`, and only the converted sprite lands under `assets/`.

## Rules

- **Never** `cat`, `base64` or otherwise read image bytes into context. The
  script writes files and prints paths. To judge a result, `Read` the PNG.
- Refining = `edit` on the previous version, not a fresh `gen` — that keeps the
  lineage in `parents` and the version chain in the filename. Reuse `--name`
  across a lineage so versions stack (`-v1`, `-v2`, …). Match this project's asset
  naming: lowercase with underscores, and the variant id it will become
  (`sedan_rust`, `pickup_tan`, `hospital`).
- Game art wants `--transparent`; see *Transparency*.
- Seeds are recorded. Re-run with `--seed` from `show` to reproduce a frame, and
  vary only the prompt when comparing wordings.

## What this model will and will not do

- **It will not recolour an existing image.** `edit` on a red car asking for deep
  blue returns a red car — at `--strength` 0.55, 0.6 and 0.8, and with `--cfg 3`
  plus a negative prompt. Above ~0.75 the body deforms before the colour moves.
  **A different colour is a fresh `gen`**, with the colour named in the prompt.
- `edit` is for keeping a layout while re-rendering it: 0.4–0.6 preserves the
  silhouette and cleans up detail, and that is the useful range.
- **It holds a grid well.** A "4 columns by 2 rows" sheet came back on a clean,
  evenly spaced grid — but with 7 cars, one cell empty. `Read` the sheet and count
  the cells before slicing it against a fixed number of columns and rows.
- Text inside an image is beyond it. So is compositing several reference images —
  `edit` takes exactly one `--image`.

## Transparency

`--transparent` renders on a flat field and keys it out afterwards, in the
script, not in the model:

- The prompt asks for flat magenta, but the model returns a *muted* version of
  whatever colour it was asked for (a dusty rose, never `#FF00FF`), so the key
  colour is **measured from the image border**, not assumed.
- Removal is a flood fill inwards from the edges, so a car body that happens to
  match the background keeps its colour.
- The cast shadow the model draws however firmly the prompt forbids it is keyed
  out too, as a darkened shade of the same background. `--keep-shadow` keeps it.
- Fully transparent pixels get their colour zeroed, so bilinear filtering cannot
  bleed the background tint back out.

If the border is not one flat colour the script says so on stderr and keys what
it can — that warning means re-roll, not ship.

## Masked edits

`--mask M.png` is a copy of the image whose *transparent* area is the region to
replace, at the same size as the image. The mask is applied by the CLI after the
pass: every pixel outside it is byte-identical to the input, and `--feather N`
(default 2) blends the seam. ComfyUI's own latent noise mask is not used — it
returns a blank frame on this model stack.

## When every frame comes back blank

The VAE encode path rots after a few dozen prompts on this ROCm/int8 stack: every
image, `gen` or `edit`, decodes to a featureless grey or black frame until the
server is restarted. A prompt that worked earlier in the session is the tell.

The script detects a blank frame, restarts ComfyUI and retries once by itself, so
this is usually invisible — it costs ~30 s. If a second frame is blank it stops
and says so, and that is a real prompt/size/strength problem. `up --restart`
forces the restart by hand.

## Prompting

State subject, view angle, style, palette, background and framing, one clause
each, and lead with `Pixel art.` — the LoRA responds to it. This project is
top-down 2D: say "orthographic top-down view, centred, no perspective" or the
model gives a 3/4 view. Say what must not appear (text, watermark, border); with
`--transparent` the background and shadow clauses are appended for you.

For a sheet, state the grid as a number — "N columns by M rows, evenly spaced,
uniform scale and lighting, each subject centred in its cell" — plus "no
gridlines, no cell borders, no numbers". Budget ≥256 px per cell.

## Reporting back

Give the user the path and a one-line description. Don't paste prompts back or
summarise the index; `list` exists for that.
