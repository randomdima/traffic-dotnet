# raw_assets

**Where every image the game uses came from, kept so it can be asked for again, adjusted, or compared
with what else was made for the same asset.** Nothing here is read by the build: a sprite under `assets/`
is converted from a variant here, never this file itself.

Art is generated with the [remotegen](../.claude/skills/remotegen/SKILL.md) skill, nine or more assets
to a frame, and cut, kept and installed with `qq sprites`.

## What lives where

```
.tmp/gen/                          scratch, never committed
  templates/<sheet>.png            the layout a frame is asked from: outlines on #FF00FF, cells in the PNG
  frames/<name>-vN.png             every frame (a whole sheet), and its record in index.jsonl
  inputs/<sha>.<ext>               every picture a frame was sent, by content
  cuts/<frame>/<id>.png            each cell keyed and clipped to its outline

raw_assets/                        committed
  <path under assets/>/<id>/       one folder per asset, at the path its sprite has under assets/
    <id>-vN.png                    a kept variant: one cell of a frame, keyed, at the frame's resolution
    variants.jsonl                 per variant: frame, cell, px/m, score 0-5, used | candidate
  <family path>/prompting.md       what wording worked and what failed for a family, citing variants
  _frames/<frame>.json             the record of every frame a kept variant came from, and its ancestors':
                                   the exact prompt, model, settings sent, cost, inputs by hash
  _inputs/<sha>.<ext>              the pictures those frames were asked from: layouts, style pictures, masks
  legacy.txt                       images under assets/ that trace to nothing yet

assets/<path>/<id>.src.json        beside the sprite: the variant it was made from, its px/m, its encoding
```

A sprite traces through its `.src.json` to its variant, through `variants.jsonl` to its frame, and through
`_frames/` to the first prompt — `qq sprites trace <id>` prints the chain.

## Rules

1. **Every image under `assets/` has a `.src.json` beside it or is on `legacy.txt`.** `qq sprites check`
   (inside `qq checks`) fails otherwise. An entry leaves the legacy list when `qq sprites use` replaces its
   image, and nothing is ever added to it.
2. **A sheet is never committed; its cells are.** A frame stays in `.tmp/gen/`. A cell with a major defect
   stays there too. A cell worth keeping — the one put in the game and the real alternatives to it — is
   kept with `qq sprites keep`, which brings the frame's record and its inputs along.
3. **What is kept is never edited or renamed.** A new attempt is a new version; a variant named by a
   `.src.json` is never deleted.
4. **Every frame is asked from a `qq sprites template` layout** on flat `#FF00FF`, so a cut is a crop and a
   key and nothing else. A frame asked without one cannot be cut.
5. **Each kept variant gets its score when it is judged**, and the lesson of a set goes into its family's
   `prompting.md` in the same change, citing the variants it is drawn from. That file is read before the
   family's next brief is written.
6. **A cut is reproducible**: the same frame and template give the same variant, and `qq sprites use`
   gives the same sprite from the same variant.

## Before these rules

The images directly in this folder (`Hospital.png`, `police.png`, …) are source art from before them;
they trace to nothing and are left as they are until their assets are made again. Everything generated
before 2026-10-18 was dropped.
