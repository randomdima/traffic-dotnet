---
name: remotegen
description: Generate and edit images through OpenRouter's image models for this project's art — sprites, roof sheets, textures — always GPT Image 2.5 Sunburst at quality low unless the owner says otherwise, at least nine assets to a sheet, three attempts a sheet. Billed per image. The only image generator; local models are not used. Use for any image: game art, sprites, sheets, textures, mockups, "generate an image", "make a sprite", "draw", "edit that image", "another version".
---

# remotegen

`.claude/skills/remotegen/remotegen.py` asks OpenRouter's Image API for one frame per call, on the owner's
settings. Each frame goes to `.tmp/gen/frames/<name>-v<N>.<ext>` with one record in `.tmp/gen/index.jsonl`:
the exact prompt, model, the settings sent, cost, and every picture sent as `inputs`, each copied to
`.tmp/gen/inputs/<sha>.<ext>` as it was when sent (a frame among them is named as its parent). Cutting,
keeping and installing are `qq sprites`'s. `openrouter.py` beside it is the bare client (key, catalogue,
billed request), which [imgbench](../imgbench/SKILL.md) shares; a benchmark never touches these frames,
this index or the attempt cap.

## Commands

```bash
R=.claude/skills/remotegen/remotegen.py
$R edit --image REF [--image REF ...] -p "PROMPT" [--mask M.png] [--name STEM] [--aspect 3:2]
$R gen "PROMPT" [--name STEM] [--aspect 1:1]
$R credit            # spent / left on the key
$R list | show ID
```

`REF` is a path, a frame id (`roofs_house_a-v2`) or a name (its newest frame). Output paths print one per
line; settings, usage, time and cost go to stderr (`$?` when the reply stated none), even when a request
fails. The model's own `supported_parameters` decide what is sent: at most its number of references, its
nearest aspect, `--quality` only where it takes one. A 429 is retried after 30, 60 and 90 s; a dropped reply
to a POST is not, since the frame may already be billed.

`--mask M.png` is a PNG of image 1's size whose transparent area is what gets replaced; every pixel outside
it stays byte-identical, `--feather N` (default 2) blending the seam. Only its alpha is read: make it with
`qq sprites mask FRAME IDS`, which writes alpha on black — a mask is kept with the lineage, and one that
copies the frame is the parent's image kept under another name.

## The owner's rules for a run

- **Settings are not yours to change**: `openai/gpt-image-2.5-sunburst` at quality `low` are the defaults,
  and `--model`, `--quality` and `--past-cap` exist for the owner's word only. A sheet is `--aspect 3:2`.
  Another model (Flare included), quality, aspect or number of references is asked of the owner first,
  never tried. The brief's wording and a `--mask` redo are not settings.
- **At least nine assets to a frame**, 3×3. Smaller assets go more to a sheet (4×4, 5×4); never fewer than
  nine, never a frame of one. A set short of nine is merged with a like one.
- **Three attempts per generation**: one sheet is one `--name`, and its v1, v2 and v3 — a mask redo
  included — are all it gets. A refusal or a reply with no image counts; a 429 or 5xx the script retried
  does not. The script refuses a fourth frame under one name before it is billed.
- **Between attempts, `Read` the frame** and judge every cell: its outline traced and filled with nothing
  outside, the proportions kept, straight down, every framed part drawn in its area and switched off, the
  asked material, no text, no corruption, the style
  picture's look. v2 and v3 fix what v1 got wrong: a `--mask` redo on the frame itself (image 1, the layout
  image 2, `qq sprites mask` over the bad cells) when a few cells are wrong, so the rest stay
  byte-identical; a whole re-roll with a line naming each fault when most are. A redo's brief opens by
  saying image 1 is the finished sheet and image 2 its layout, names the cell and its fault in measured
  terms ("about 8% too wide"), and its style line points at the first image's other cells.
- **Not good after three: stop and report** — the frames' paths, what is wrong cell by cell, what each
  attempt changed, and the spend. Do not go on to the set's next sheet: what failed one sheet is usually the
  brief, and it fails the next.

Sunburst at low is ~$0.034 and ~15 s a 3×3 sheet; one asset alone is $0.024 and drifts from its set's
palette. It takes no resolution: 3:2 comes back 1536×1024 and 1:1 1024×1024, which is the size `qq sprites
template` lays, so a cut is a crop. The pick, and every other model's results, are imgbench's
(2026-10-18); `openai/gpt-image-2.5-flare` was second.

## From frame to the game: every image traces to its prompt

A frame is scratch; what goes into git is each asset's cut, with the record of how it was made
([raw_assets/index.md](../../../raw_assets/index.md) is the whole rule). One set, start to finish:

```bash
# 1. read the family's prompting.md under raw_assets/, if it has one, before writing the brief
qq sprites template house_01 house_02 ... --out=.tmp/gen/templates/roofs_house_a.png \
    --style=house_05,house_09,...        # ≥9 ids, sorted by size; the style picture beside it as roofs_house_a_style.png
#    prints each cell's line — size, proportion, `describe` — for the brief, and px/m against the art grid
$R edit --image .tmp/gen/templates/roofs_house_a.png --image .tmp/gen/templates/roofs_house_a_style.png \
    -p "$(cat .tmp/gen/briefs/roofs_house_a.txt)" --aspect 3:2 --name roofs_house_a
qq sprites cut roofs_house_a-v1          # each body fitted onto its shape, keyed, clipped; flags holes, spill, parts
# 2. Read the frame, cuts/<frame>/_overlay.png and _parts.png (every part area magnified: `cut`'s "drawn"
#    only says paint differs there); judge each cell (the rules above), v2/v3 for what failed. A cell is
#    kept from whichever attempt drew it well.
qq sprites mask roofs_house_a-v1 house_07   # the --mask for a redo of those cells
qq sprites keep roofs_house_a-v1 house_01 house_02 --score=4      # the good cells, with their lineage
qq sprites use house_01-v1               # into assets/ at the art grid, with house_01.src.json
qq sprites compare house_01              # every kept variant of it beside the sprite in the game
qq sprites trace house_01                # sprite -> variant -> frame(s) -> prompt and inputs
# a car set, in game before it is in Fleet.json: the showroom, captioned (readme)
dotnet run --project traffic-dotnet.csproj -- --map Showroom --fleet ID,ID,... --shot .tmp/gen/showroom.png --caption --seconds 3
# 3. the set's lesson into the family's prompting.md, citing the variants
```

- **The shape comes first and the art is made to it**: nothing is ever measured off a picture. The layout is
  each asset's authored outline at its true size on one scale, on flat magenta `#FF00FF`, the front a thick
  edge (a building's street side at the bottom, a car's nose at the right). A car's outline is its
  `collisionM` box, which the bodywork fills; its mirrors may stand off it as far as the footprint, and its
  cut keeps the whole footprint (CAR-12b). **A car is drawn without tyres**: the game draws them under the
  body (`CarSheets`). **And never open-top**: the game has no driver to show in a seat (the owner, in
  `raw_assets/agents/car/prompting.md`). The style picture is finished art from the game, none of it an asset asked for —
  `--style` refuses an id that is also asked.
- **`cut` fits the drawing to the shape**: the model lands a body a few pixels off its outline, so each
  cell's drawn extent (a car's width as the median of its flanks, which a mirror does not move) is shifted
  and scaled onto the authored shape before it is keyed, and the fit is printed and kept in the cut. A
  drawing more than 8 % off in either axis is not fitted and fails its cell: ask for it again. The owner's
  allowances are `FIT_ROWS_MAX` (15 %, a train's rows) and `FIT_GEAR_MAX` (16 %, a level crossing's gear); an
  asset ten times longer than wide is read across by its long edges' median, as a car by its flanks.
- **Every switchable part is drawn, off** (rule 7 of raw_assets/index.md): lamps, beacons, whatever the game
  lights has the area it is drawn in framed on the template at its authored place, in its colour — the area,
  never the shape, which is the art's — and the cell lines `template` prints name each one (`off: 2x rear
  lamp, dark red glass; ...`) for the brief, with its line on how a frame is read.
  The base picture holds them at rest so their lit state can be cut from it; a frame that skips one or draws
  it glowing fails that cell. `template` warns of a part past its asset's shape: re-author it first.
- imgbench's `bld_prompt.txt` and `train_prompt.txt` are the pattern for the brief, and a family's last
  brief is in its frames' records (`qq sprites trace <id> --full`). Briefs go in `.tmp/gen/briefs/`. The
  brief asks for the flat magenta background; `cut` measures the colour the model actually drew from the
  frame's border.
- `use` clears islands under 8 px the shrink leaves off the body (CAR-12's pixel-dust gate).
- **A car is not in the fleet until it has a wreck sheet** (`CarSpriteTests`): ask the set's wrecks as their own
  sheet once the cars are installed — ids `<car>_broken`, the cars themselves as the second image
  (`--style=<car ids>`), `use` writing the file's `wreck.sprite`. Every look is two sheets of
  `SheetAtlas.Slots`; a car added to `Fleet.json` also wants `--lamps` re-run, and is seen on `--map Showroom`.
- **A car's lamps are mapped by its sheet's zone map** (CAR-14a): `$R edit --image <sheet>-vN --name <sheet>_zones`
  with the zone brief in `raw_assets/agents/car/prompting.md`; `cut` fits it like any frame, quantises it to the
  zones and flags a lamp it left unzoned, and `keep ... <car>_zones` / `use <car>_zones-vN` install the file's
  `zones`, which must map the sheet its sprite came from. Then `--lamps`.
- **A set is done when `qq proof` says so**: `qq proof --looks=<the set's ids>` writes `.tmp/proof/lamps.gif`, every
  look through every lamp state and wrecked, from the engine's own instances. Look at it before reporting the set.
- To ask for an asset again, `trace` it: the prompt and the inputs (`raw_assets/_inputs/<sha>`) are what
  to send.

## Key and money

The key is read from `OPENROUTER_API_KEY` or `~/.qq/openrouter.key` (shared with `qq pilot`); nothing about
it is in the project and it is never printed. Every frame is a billed request — there is no batch. A
401/402/403 is the key's or the account's: report it and stop. Run `credit` before a long series.

## Rules

- **Never** `cat`, `base64` or otherwise read image bytes into context; `Read` the PNG to judge it.
- Refine with `edit` on the previous version and the same `--name`, so the lineage is in `inputs` and the
  versions stack. Name a sheet for its set (`roofs_house_a`), lowercase with underscores.
- References may be PNG, JPEG or WebP; `--mask` needs the model to return PNG.

## Reporting back

Path, a one-line description, the attempt it took, and the cost.
