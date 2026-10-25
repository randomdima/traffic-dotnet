---
name: remotegen
description: Generate and edit images through OpenRouter's image models for this project's art — sprites, roof sheets, textures — always GPT Image 2.5 Sunburst at quality low unless the owner says otherwise, at least nine assets to a sheet, three attempts a sheet. Billed per image. The only image generator; local models are not used. Use for any image: game art, sprites, sheets, textures, mockups, "generate an image", "make a sprite", "draw", "edit that image", "another version".
---

# remotegen

`.claude/skills/remotegen/remotegen.py` asks OpenRouter's Image API for one frame per call, on the owner's
settings. Each frame goes to `.tmp/gen/frames/<name>-v<N>.<ext>` (a transparent PNG with every pixel under alpha 16 cleared to transparent
black, the glow the model leaves round each drawing; the owner, 2026-10-22) with one record in `.tmp/gen/index.jsonl`:
the exact prompt, model, the settings sent, cost, and every picture sent as `inputs`, each copied to
`.tmp/gen/inputs/<sha>.<ext>` as it was when sent (a frame among them is named as its parent). Cutting,
keeping and installing are `qq sprites`'s. `openrouter.py` beside it is the bare client (key, catalogue,
billed request), which [imgbench](../imgbench/SKILL.md) shares; a benchmark never touches these frames,
this index or the attempt cap.

## Commands

```bash
R=.claude/skills/remotegen/remotegen.py
$R edit --image REF [--image REF ...] -p "PROMPT" [--cells IDS.json] [--mask M.png] [--name STEM] [--aspect 3:2]
$R gen "PROMPT" [--cells IDS.json] [--name STEM] [--aspect 1:1]
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

- **Settings are not yours to change**: `openai/gpt-image-2.5-sunburst` at quality `low` with a transparent
  background (the owner, 2026-10-20) are the defaults, and `--model`, `--quality`, `--background` and `--past-cap`
  exist for the owner's word only. A sheet is `--aspect 3:2`.
  Another model (Flare included), quality, aspect or number of references is asked of the owner first,
  never tried. **The owner's allowance**: ground surfaces go at `--quality high` ($0.067 a sheet; 2026-10-24, "try again
  with better quality setting") — at low their fine detail came back as speckle mush. The brief's wording and a `--mask` redo are not settings.
- **At least nine assets to a frame**, 3×3. Smaller assets go more to a sheet (4×4, 5×4); never fewer than
  nine, never a frame of one. A set short of nine is merged with a like one. **The owner's allowances**: building
  roofs a 3×3 cell holds under the art grid go as few as two to a frame, in whichever grid draws them largest (2026-10-20);
  `qq prefabs sheets` plans the whole catalogue that way and `qq prefabs brief`
  writes each frame's prompt. The fans' looks (`assets/world/building/fans/`) went one to a frame (2026-10-24: "Generate
  10 good images", "Dont cut them"), asked with `--cells` and taken whole by `cut` — a picture that is no car is never
  fitted or clipped.
- **Get it in one go; a retry is a backoff, not a habit** (the owner, 2026-10-20). Before v1, read what worked and
  failed on the family's earlier frames (its `prompting.md`, and the lessons of frames made this session) and write
  the brief to avoid those faults. A v2 needs a real defect a kept cell cannot carry; a v3 a stronger reason still —
  real issues left after v2, or a change to the brief expected to improve the result significantly. A minor flaw is
  kept at a lower score, not re-rolled.
- **Three attempts per generation at most**: one sheet is one `--name`, and its v1, v2 and v3 — a mask redo
  included — are all it gets. A refusal or a reply with no image counts; a 429 or 5xx the script retried
  does not. The script refuses a fourth frame under one name before it is billed.
- **Between attempts, `Read` the frame** and judge every drawing: the thing asked for, its proportions those of
  the real thing at its size, straight down, every part the brief names drawn and switched off, the asked material,
  no text, no corruption, the style picture's look, and the grid kept so `cut` can tell which is which. v2 and v3 fix
  what v1 got wrong: a `--mask` redo on the frame itself (image 1, `qq sprites mask` over the bad drawings) when a
  few are wrong, so the rest stay byte-identical; a whole re-roll with a line naming each fault when most are. A
  redo's brief opens by saying image 1 is the finished sheet, names the cell and its fault in measured terms ("about
  8% too wide"), and its style line points at image 1's other drawings. Send nothing else with a mask redo: with a
  layout and a style picture beside it, the model redrew the masked cells as fragments (`props_trees_large-v3`).
- **Not good after three: stop and report** — the frames' paths, what is wrong cell by cell, what each
  attempt changed, and the spend. Do not go on to the set's next sheet: what failed one sheet is usually the
  brief, and it fails the next.

Sunburst at low is ~$0.034 and ~15 s a 3×3 sheet; one asset alone is $0.024 and drifts from its set's
palette. It takes no resolution: 3:2 comes back 1536×1024 and 1:1 1024×1024. The pick, and every other model's results, are imgbench's
(2026-10-18); `openai/gpt-image-2.5-flare` was second.

## From frame to the game: every image traces to its prompt

A frame is scratch; what goes into git is each asset's cut, with the record of how it was made
([raw_assets/index.md](../../../raw_assets/index.md) is the whole rule). One set, start to finish:

```bash
# 1. read the family's prompting.md under raw_assets/, if it has one, before writing the brief
qq sprites style house_05 house_09 ... --out=.tmp/gen/templates/roofs_house_a_style.png \
    --cells=.tmp/gen/briefs/roofs_house_a.cells.json   # finished art at one scale, none of it an asset the frame draws
# the brief, .tmp/gen/briefs/roofs_house_a.txt: the grid, then each cell in reading order — its size in metres, its
# proportion, what it is; the ids in that order, one JSON list, as roofs_house_a.cells.json
$R edit --image .tmp/gen/templates/roofs_house_a_style.png --cells .tmp/gen/briefs/roofs_house_a.cells.json \
    -p "$(cat .tmp/gen/briefs/roofs_house_a.txt)" --aspect 3:2 --name roofs_house_a
qq sprites cut roofs_house_a-v1          # each drawing found in the brief's order and taken whole
# 2. Read the frame and cuts/<frame>/_cuts.png; judge each drawing (the rules above), v2/v3 for what failed. A
#    drawing is kept from whichever attempt drew it well.
qq sprites mask roofs_house_a-v1 house_07   # the --mask for a redo of that drawing; car_id:nose / car_id:tail only
#    that end's lamps
qq sprites keep roofs_house_a-v1 house_01 house_02 --score=4      # the good drawings, with their lineage
qq sprites use house_01-v1               # into assets/ at the art grid, with house_01.src.json
qq sprites compare house_01              # every kept variant of it beside the sprite in the game
qq sprites trace house_01                # sprite -> variant -> frame(s) -> prompt and inputs
# a car set, in game before it is in Fleet.json: the showroom, captioned (readme)
dotnet run --project traffic-dotnet.csproj -- --map Showroom --fleet ID,ID,... --shot .tmp/gen/showroom.png --caption --seconds 3
# 3. the set's lesson into the family's prompting.md, citing the variants
```

- **A car is described, and what is drawn is the car** (the owner, 2026-10-20; CAR-12b, CAR-14a): no layout is
  sent. `qq fleet brief --batch=N --frame=F [--wrecks]` writes the brief (one shared base, then new cars or wrecks) from the plan and `F.cells.json`, the ids in
  order, which `$R edit --image <style> ... --cells .tmp/gen/briefs/F.cells.json` keeps in the frame's record;
  `cut` finds the drawings in that order, scales each to its file's length and measures the rest, and `use`
  writes footprint, `collisionM` and `trackM` over the plan's; `qq props install` writes the `lamps`. The
  style picture is the only input (a trial: it halves the proportion faults, `cars_fleet_b`): three of the
  best installed cars that differ most in shape and paint — `crossover_pearl,estate_steel,kombi_sage` (the owner,
  2026-10-20), taken whole from their frame with `--style-whole` so mirrors are not clipped (the owner, 2026-10-21) —
  in one row at their own density (1482×244, $0.016 a frame): smaller pictures saved $0.003 and doubled the
  proportion faults, and nothing is cached between frames (`raw_assets/agents/car/prompting.md`). Cars only: anything
  else in it is something else the sheet may draw. `cut` flags a
  body more than 8 % off its file's width — the car is what was drawn, the flag says which to look at.
- **Everything else is described too, and what is drawn is the asset** (the owner, 2026-10-25: no outlines, only
  style references, never a background). The brief gives the grid and, cell by cell in reading order, each asset's
  true size in metres against a car beside it, its proportion, where its front is (a building's street side at the
  bottom, a train's nose at the right) and what it is. `cut` takes each drawing whole and sizes it by the one figure
  its file authors (a footprint's width, a round asset's diameter, a strip's depth); `use` writes what the drawing
  says of the rest (a footprint's depth, a strip's length, a building's rectangles). It flags a drawing whose
  proportion is more than 8 % off its file's: the eye decides whether it is still the asset. **Words describe the
  asset as well as they can, and the engine maps what comes back** (the owner, 2026-10-25): a result may be
  cropped, sized or given a minor correction where it still looks right, never forced into a shape — most assets look
  bad if they are — and the generator is never limited to get it. **A car is drawn without tyres**: the game
  draws them under the body (`CarSheets`). **And never open-top**: the game has no driver to show in a seat (the
  owner, in `raw_assets/agents/car/prompting.md`). The style picture is finished art from the game, none of it an
  asset asked for — `style` refuses an id that is also asked.
- **The frame comes back transparent**, the setting every frame is asked with, and the brief says so: everything
  outside the drawings fully transparent, no ground, no backdrop, no frame. `cut` keys by alpha; a frame the model
  painted opaque anyway is chroma-keyed off the colour its border measures.
- **Every switchable part is drawn, off** (rule 7 of raw_assets/index.md): lamps, beacons, whatever the game
  lights is named in its cell's line with where on the asset it is (`off: 2x rear lamp, dark red glass; ...`) —
  never its shape, which is the art's. The base picture holds them at rest so their lit state can be cut from it; a
  frame that skips one or draws it glowing fails that cell.
- A family's last brief is in its frames' records (`qq sprites trace <id> --full`); briefs go in
  `.tmp/gen/briefs/`. imgbench's prompts and a brief whose frame was asked from a layout describe what is not sent:
  take their wording about the assets, never their lines about outlines, tracing or magenta.
- `use` clears islands under 8 px the shrink leaves off the body (CAR-12's pixel-dust gate).
- **A ground surface is asked tile by tile** (`kind: "Surface"` under `assets/world/terrain/ground/surfaces/`): its
  file's `looks` (a kept lawn, a worn one) each name a count of tiles, ids `<surface>_<look letter><n>`, each a square
  drawn edge to edge with no border, no grid and no direction. Once every look has a tile used, `use` joins them all
  into the surface's picture — each look a 2x2 repeat at its `ViewFigures` period, the looks stacked as the layers the
  ground stage blends (`.qq/_surfaces.py`, `GroundPatches`). Its `.src.json` names every variant joined. Read
  `raw_assets/world/terrain/ground/surfaces/prompting.md` before the brief.
- **A car is not in the fleet until it has a wreck sheet** (`CarSpriteTests`): ask the set's wrecks as their own
  sheet once the cars are installed, described like the bodies — ids `<car>_broken` as `--cells`, the installed cars
  as the only image (`qq sprites style <car ids>` makes it), `use` writing the file's
  `wreck.sprite` at the car's footprint. Every look is two sheets of `SheetAtlas.Slots`; a car added to `Fleet.json`
  also wants `--lamps` re-run, and is seen on `--map Showroom`.
- **A car's lamps are cut, not generated** (CAR-14a): `qq props` outlines each lens on the installed sprite (an agent
  a car, `qq props --help` is its manual) and `qq props install <car>` writes the zone map and `lamps`. A car's files
  are written from a plan by `qq fleet scaffold`; `use` puts its picture on the art grid and drops lamps cut on an
  older one.
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
