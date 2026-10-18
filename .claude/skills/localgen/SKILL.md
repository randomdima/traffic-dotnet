---
name: localgen
description: Not to be used. Local image models (ComfyUI, FLUX.2 klein, Z-Image, Qwen-Image-Edit, LongCat, OmniGen2) never produce worthwhile results, by the owner's ruling (2026-10-18); every image goes to remotegen. The script stays only as the library remotegen imports.
disable-model-invocation: true
---

> **Not to be used** (owner, 2026-10-18): local models never produce worthwhile results. Every image is
> made with [remotegen](../remotegen/SKILL.md). `localgen.py` stays because remotegen imports its index,
> naming, keying and mask compositing; nothing here is a reason to run a local model.

# localgen

Wraps `.claude/skills/localgen/localgen.py` (stdlib only, no pip installs). It drives ComfyUI at
`127.0.0.1:8188`, running inside the `local-ai` toolbox container from
`~/local-ai/ComfyUI/run.sh`. Nothing leaves the machine and nothing is billed, so
a re-roll is cheap — but the model is small, and the traps below are its own.

## Commands

```bash
.claude/skills/localgen/localgen.py gen "PROMPT" [--name STEM] [--size WxH] [-n N] [--transparent] [--seed N]
.claude/skills/localgen/localgen.py edit --image REF [--image REF ...] -p "PROMPT" [--mask M.png] [--name STEM]
.claude/skills/localgen/localgen.py edit --model zimage --image REF -p "PROMPT" [--strength 0..1]
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

## The stacks

Chosen with `--model`; `~/local-ai/ComfyUI/fetch-models.sh <stack>` downloads one. All are
Apache 2.0 and ungated. Klein and Z-Image are the everyday two and share the Qwen3-4B text
encoder; the others came in for [imgbench](../imgbench/SKILL.md):

| model | what | refs | steps / cfg | bld frame |
|---|---|---|---|---|
| `kleinbase` | FLUX.2 [klein] 4B undistilled, fp8 | 2 | 20 / 5 | 54 s |
| `qwen`, `qwen8` | Qwen-Image-Edit-2511 fp8mixed / int8 + Lightning LoRA; 20 GB, half offloaded to RAM, encoder on CPU | 3 | 4 / 1 | 80–90 s |
| `longcat` | LongCat-Image-Edit 6B, bf16 | 1 | 50 / 4.5 | 175 s |
| `omnigen2` | OmniGen2, fp16; dual guidance (prompt 5, pictures 2) | 2 | 20 / 5 | 100 s |
| `zimagebase` | Z-Image undistilled, int8, text only | 0 | 25 / 4 | 96 s |

Defaults are each maker's ComfyUI template. OmniGen2 needs a real negative prompt (its own is
the default): an empty one at cfg 5 smears every frame.

**`klein` (default): FLUX.2 [klein] 4B distilled, fp8**, Apache 2.0. `edit` takes
**several `--image` references** — name them in the prompt as "image 1", "image 2" —
and draws a fresh image conditioned on all of them, so there is no `--strength`. The
output takes the first image's size unless `--size` is given. Defaults: 4 steps,
cfg 1.0, `euler`. Each reference is scaled to 1 megapixel before encoding.

**`zimage`: Z-Image Turbo (int8) + `pixel_art_style_z_image_turbo` LoRA.** `edit`
redraws exactly one image at `--strength`. Defaults: 8 steps, cfg 1.0,
`res_multistep`, shift 3.0, LoRA strength 1.0. `--lora-strength 0` drops the
pixel-art LoRA when the target is not pixel art. Raising `--cfg` above 1 turns the
negative prompt on but this is a distilled turbo model — it does not reward it.

`--size` must be a multiple of 16; 1024 is the native size for both, and 512-768 is
what most sprites want.

**Measured:** Klein, two 1200x1200 frames from two references in one `-n 2` batch:
20 s. Z-Image, one 768x768 image: ~8 s including the model load, and a `-n 2` batch
is the *same* ~8 s because the load dominates. Batch instead of looping, always.

**Compared on a roof sheet** (nine building outlines on magenta, `.tmp` layout plus
a style picture): Klein kept every outline's position, proportions and rounded
corners exactly and left the magenta clean, but drew photo-like grime rather than
the style picture's cartoon look and little rooftop equipment — a prompt to tune.
Z-Image at strength 0.7 barely painted the outlines; at 0.8–0.85 it returned blank
or corrupt frames or crashed the server.

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

## What the models will and will not do

- **Klein holds a layout from a reference.** Give the layout as image 1 and a
  finished example as image 2, and say which is which; the shapes come from the first.
  Its look drifts toward photographic texture — name the style in words as well
  ("flat-shaded cartoon game art, bold dark outlines").

The rest of this section is measured on `zimage`:

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
  `--model zimage` takes exactly one `--image`; that is Klein's job.

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

The VAE encode path rots after a few dozen prompts on the ROCm/int8 Z-Image stack:
every image, `gen` or `edit`, decodes to a featureless grey or black frame until the
server is restarted. A prompt that worked earlier in the session is the tell. int8
diffusion models are reported to give black frames on this GPU (gfx1201) generally,
which is why Klein is fetched as fp8.

The script detects a blank frame, restarts ComfyUI and retries once by itself, so
this is usually invisible — it costs ~30 s. If a second frame is blank it stops
and says so, and that is a real prompt/size/strength problem. `up --restart`
forces the restart by hand.

## The server holds VRAM between prompts

After a frame, ~6 GB of the card stays held by the server outside torch's allocator — `/free`
cannot release it — and the next model runs offloaded on ~1 GB: LongCat came back as grey mush
there and clean on a fresh server. For a frame that matters, `up --restart` first. The server
reads its settings at start, from the environment of whoever starts it:

| variable | default | effect |
|---|---|---|
| `LOCALGEN_RESERVE_VRAM` | 3 | GB kept back for the desktop; imgbench uses 0.5 |
| `LOCALGEN_VAE` | bf16 | `fp32` or `cpu`. **Qwen needs fp32 or cpu**: in bf16 its VAE decodes any prompt over ~200 words to NaN — a flat grey frame and `invalid value encountered in cast` in the log. fp32 on the card runs out of memory beside a loaded model, falls back to tiled decoding, and that fails too: black and grey blocks in the frame, "Ran out of memory when regular VAE decoding" in the log. `cpu` is fp32 and safe, a few seconds slower |
| `LOCALGEN_ATTENTION` | pytorch | `split` for plain-matmul attention |
| `LOCALGEN_SERVER_ARGS` | — | appended to ComfyUI's flags |

## When the GPU is overloaded

This card also drives the display. On 2026-10-17 Klein runs with three references at
1200x1200 first returned garbage — horizontal streaks, mud or coloured noise — and the
next heavy run froze the whole machine. So:

- `run.sh` starts ComfyUI with `--reserve-vram 3` (left for the desktop) and
  `--disable-pinned-memory` (it had page-locked 45 GB of the 62 GB RAM).
- `localgen.py` refuses a Klein run with more than two references or more than
  ~1.5 megapixels of output (width x height x `-n`), and scales references to 0.5 MP.
  One roof or sprite per run at its own size is the light load, and gave the best
  results anyway.
- **A corrupt frame is not detected — only a blank one is.** No simple measure tells
  streaks, mud or noise from a real sprite on a flat field (tried on every image in
  `raw_assets/generated/`: both sit on either side of any threshold). `Read` every frame
  before using it; a suspiciously small file next to its batch-mate is a hint. Garbage
  frames mean the GPU is near its limit: restart the server and lighten the run.
- Close VRAM-heavy apps while generating; VS Code alone has held 3.8 GB.

## Prompting

State subject, view angle, style, palette, background and framing, one clause
each. On `zimage`, lead with `Pixel art.` — the LoRA responds to it. This project is
top-down 2D: say "orthographic top-down view, centred, no perspective" or the
model gives a 3/4 view. Say what must not appear (text, watermark, border); with
`--transparent` the background and shadow clauses are appended for you.

For a sheet, state the grid as a number — "N columns by M rows, evenly spaced,
uniform scale and lighting, each subject centred in its cell" — plus "no
gridlines, no cell borders, no numbers". Budget ≥256 px per cell.

## Reporting back

Give the user the path and a one-line description. Don't paste prompts back or
summarise the index; `list` exists for that.
