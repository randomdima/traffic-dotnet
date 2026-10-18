---
name: remotegen
description: Generate and edit images through OpenRouter's remote image models for this project's art — sprites, roof sheets, textures — always GPT Image 2.5 Sunburst at quality low unless the owner says otherwise, at least nine assets to a sheet, three attempts a sheet. Billed per image. The only image generator in use — local models are not. Use for any image: game art, sprites, sheets, textures, mockups, "generate an image", "make a sprite", "draw", "edit that image", "another version".
---

# remotegen

Wraps `.claude/skills/remotegen/remotegen.py` (stdlib only). With `--api chat` it calls
`https://openrouter.ai/api/v1/chat/completions` with the model's own output modalities
(`["image","text"]` for Gemini and GPT-5 image, `["image"]` for an image-only model such as
FLUX, Seedream or gpt-image, which refuses the pair with a 404) and
writes what comes back to `.tmp/gen/frames/<name>-v<N>.<ext>`, one record per image in
`.tmp/gen/index.jsonl`: the exact prompt, model, the settings sent, cost, and every picture sent as
`inputs` — each copied to `.tmp/gen/inputs/<sha>.<ext>` as it was when sent, a frame among them named
as its parent. The index, naming, `--transparent` key and `--mask` compositing are imported from
`localgen.py`, kept only as that library: local models are not used ([localgen](../localgen/SKILL.md)).

## Commands

```bash
.claude/skills/remotegen/remotegen.py gen "PROMPT" [--name STEM] [--aspect 1:1] [-n N] [--transparent] [--model ID]
.claude/skills/remotegen/remotegen.py edit --image REF [--image REF ...] -p "PROMPT" [--mask M.png] [--name STEM] [--quality Q] [--image-size S]
.claude/skills/remotegen/remotegen.py models [--json]  # every image-output model, cheapest first
.claude/skills/remotegen/remotegen.py credit     # spent / left on the key
.claude/skills/remotegen/remotegen.py list | show ID
```

`REF` is a file path, an index `id` (`roofs_house_03-v2`) or a bare name (its newest version). Output paths are printed one per line; time and cost go
to stderr (`$?` when the reply stated none), even when a request fails.

Frames go through OpenRouter's Image API (`/api/v1/images`) unless `--api chat` is given: every image
model takes it, and gpt-image, FLUX.3, Krea and Qwen-Image take nothing else. The model's own
`supported_parameters` (`/api/v1/images/models`) decide what is sent: at most its number of
references, its nearest aspect, a seed, `--image-size` and `--quality` only where it takes them
(`min` is its smallest or lowest, `auto` sends none). A dropped reply to a POST is not retried, since
the frame may already be billed.

## The project's default: Sunburst, quality low, nine to a sheet

With no flags a frame is `openai/gpt-image-2.5-sunburst` at quality `low` through the Image API —
the owner's pick after imgbench's train benchmark of ten models (2026-10-18). Make art the
way it was made there:

- **Nine assets to a frame**, a 3×3 sheet: `edit --image layout.png --image style.png -p BRIEF --aspect 3:2`.
  The layout is each asset's outline at its true size on one scale, on flat magenta `#FF00FF`, with the
  front as a thick edge — `qq sprites template` draws it; the style picture is finished art from the
  game, none of it an asset asked for. imgbench's `bld_prompt.txt` and `train_prompt.txt` are the
  pattern for the brief.
- **About $0.034 a sheet**, under half a cent an asset, ~15 s. One asset alone costs $0.024 — 6.5× more
  per asset, and it drifts from its set's palette and light.
- **Quality `low`** halves the default's $0.070 with no loss visible at sprite size; `--quality auto`
  is the model's own choice. 86% of a low sheet's price is the input — the brief and both pictures.
- **Second choice**: `openai/gpt-image-2.5-flare`, same price, reverses details more often. Every other
  model on the catalogue drew trains from the side on a sheet.

## A run: the owner's rules

- **The settings are not yours to change.** Sunburst, quality `low`, the Image API, `-n 1`, `--aspect 3:2`
  for a sheet. Another model (Flare included), quality, aspect, `--image-size`, API, `-n` or number of
  references is asked of the owner first, never tried. The brief's wording and a `--mask` redo on the same
  settings are not settings.
- **At least nine assets to a frame**, 3×3. Smaller assets go more to a sheet (4×4, 5×4), never fewer than
  nine, and never a frame of one. A set short of nine is merged with a like one.
- **Three attempts per generation**: one sheet is one `--name`, and its v1, v2 and v3 — a mask redo
  included — are all it gets. A refusal or a reply with no image counts; a 429 or 5xx the script retried
  does not. The script refuses a fourth frame under one name before it is billed; `--past-cap` is for
  the owner's word and for imgbench.
- **Between attempts, `Read` the frame** and judge every cell: its outline traced and filled with nothing
  outside, the proportions kept, straight down, the asked material, no text, no corruption, the style
  picture's look. v2 and v3 fix what v1 got wrong: a `--mask` redo on the frame itself (image 1, the
  layout image 2, the bad cells' boxes transparent in the mask) when a few cells are wrong, so the rest
  stay byte-identical and the palette holds; a whole re-roll with a line naming each fault when most are.
- **Not good after three: stop and report** — the frames' paths, what is wrong cell by cell, what each
  attempt changed, and the spend. Do not go on to the set's next sheet: what failed one sheet is usually
  the brief, and it fails the next.

## Key and money

The key is read from `OPENROUTER_API_KEY` or `~/.qq/openrouter.key` (shared with
`qq pilot`); nothing about it is in the project and it is never printed. Every frame is
a billed request — `-n 3` is three requests, there is no batch. A 401/402/403 is the
key's or the account's: report it and stop. Run `credit` before a long series.

**Measured** on `google/gemini-3.1-flash-image`: a 1024x1024 sprite in 8 s for $0.067; a
two-reference roof edit in 12 s for $0.068. `models` lists the others, about
sixty: OpenRouter's plain `/models` listing leaves the image-only ones out, so `models`
reads `/models?output_modalities=image`. A 429 is retried after 30, 60 and 90 s.
Per-frame costs of the whole catalogue on a two-reference 3x3 sheet are in
`.tmp/bench/results.md` once [imgbench](../imgbench/SKILL.md) has run.

## Size is an aspect ratio, not pixels

Sunburst takes no resolution: 3:2 comes back 1536×1024 and 1:1 1024×1024 (imgbench, 2026-10-18). Draw a
sheet's layout at that size, so the outlines' pixels are the frame's and a cut is a crop.

Gemini models take one of `1:1 2:3 3:2 3:4 4:3 4:5 5:4 9:16 16:9 21:9` and pick the
pixels themselves (~1 MP). `edit` snaps the first image's proportions to the nearest
one, so a reference whose aspect is not on the list is **stretched**: a 1024x352 roof
template (2.9:1) came back 1584x672 (21:9). For a footprint, pad the layout template
with background to a listed ratio before sending it, and crop the result back.

## Rules

- **Never** `cat`, `base64` or otherwise read image bytes into context; `Read` the PNG to judge it.
- Refine with `edit` on the previous version and the same `--name`, so the lineage is in `inputs` and
  the versions stack. Name a frame for the variant id it will become: lowercase with underscores
  (`sedan_rust`, `hospital`), a sheet for its set (`roofs_house_03`).
- References may be PNG, JPEG or WebP. `--transparent` and `--mask` need the model to return PNG.
- `--transparent` asks for a flat field, measures the key colour from the border (a model returns a muted
  one, never `#FF00FF`) and floods it out from the edges, cast shadow too unless `--keep-shadow`. A
  warning that the border is not one flat colour means re-roll, not ship.
- `--mask M.png` is a copy of image 1 whose transparent area is what gets replaced; every pixel outside it
  stays byte-identical, `--feather N` (default 2) blending the seam.

## From frame to the game: every image traces to its prompt

A frame is scratch; what goes into git is each asset's cut, with the record of how it was made
([raw_assets/index.md](../../../raw_assets/index.md) is the whole rule). One set, start to finish:

```bash
# 1. read the family's prompting.md under raw_assets/, if it has one, before writing the brief
qq sprites template house_01 house_02 ... --out=.tmp/gen/templates/roofs_house_a.png  # ≥9 ids, sorted by size
#    prints each cell's line — size, proportion, `describe` — for the brief, and px/m against the art grid
R=.claude/skills/remotegen/remotegen.py
$R edit --image .tmp/gen/templates/roofs_house_a.png --image STYLE.png -p "$(cat BRIEF)" --aspect 3:2 --name roofs_house_a
qq sprites cut roofs_house_a-v1          # each cell keyed and clipped; flags holes and spill
# 2. Read the frame and the flagged cuts; judge each cell (the rules above), v2/v3 for what failed
qq sprites keep roofs_house_a-v1 house_01 house_02 --score=4      # the good cells, with their lineage
qq sprites use house_01-v1               # into assets/ at the art grid, with house_01.src.json
qq sprites compare house_01              # every kept variant of it beside the sprite in the game
qq sprites trace house_01                # sprite -> variant -> frame(s) -> prompt and inputs
# 3. the set's lesson into the family's prompting.md, citing the variants
```

- A frame not asked from a `qq sprites template` layout cannot be cut — always lay one.
- Keep a cell only if it has no major defect: the one put in the game, and real alternatives to it,
  with a score each. A broken cell stays in `.tmp/`.
- To ask for an asset again, `trace` it: the prompt and the inputs (`raw_assets/_inputs/<sha>`) are
  what to send. A frame's parent that was itself a sheet keeps its record only, never its image.
- `qq sprites check` (inside `qq checks`) fails on any image under `assets/` that traces to nothing and
  is not on `raw_assets/legacy.txt`.

## Reporting back

Path, a one-line description, the attempt it took, and the cost.
