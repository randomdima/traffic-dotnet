# raw_assets

**Where every image the game uses came from, kept so it can be asked for again, adjusted, or compared
with what else was made for the same asset.** Nothing here is read by the build: a sprite under `assets/`
is converted from a variant here, never this file itself.

Art is generated with the [remotegen](../.claude/skills/remotegen/SKILL.md) skill, nine or more assets
to a frame, and cut, kept and installed with `qq sprites`. **A frame is asked in words, with a style picture as its
only image, and comes back on a transparent background** (the owner, 2026-10-25): no layout of outlines is sent, since
the model does not keep to one and every picture sent is paid for. A walker's frame is the exception: it is sent its
pose guide too (the owner, 2026-10-27; [agents/person/prompting.md](agents/person/prompting.md)).

## What lives where

```
.tmp/gen/                          scratch, never committed
  templates/<sheet>_style.png      the style picture a frame is asked with: finished art from the game, none of
                                   it an asset the frame draws
  briefs/<sheet>[-vN].txt          the prompt a frame was asked with (its record holds the copy sent), and
                                   <sheet>.cells.json, the ids it draws in the brief's order
  frames/<name>-vN.png             every frame (a whole sheet), and its record in index.jsonl
  inputs/<sha>.<ext>               every picture a frame was sent, by content
  cuts/<frame>/<id>.png            each drawing taken whole off the transparent frame (a car's cut to the
                                   footprint it measures); _overlay.png boxes each, _cuts.png lays them for
                                   the eye, _mask_<ids>.png the alpha-only mask a redo of those drawings is
                                   asked with

raw_assets/                        committed
  <path under assets/>/<id>/       one folder per asset, at the path its sprite has under assets/
    <id>-vN.png                    a kept variant: one cell of a frame, keyed, at the frame's resolution
    variants.jsonl                 per variant: frame, cell, px/m, score 0-5, used | candidate
    <id>_broken/                   a car's wreck, an asset of its own (`qq sprites` ids `<car>_broken`)
  <family path>/prompting.md       what wording worked and what failed for a family, citing variants
  <family path>/*plan.md           the plan its variant files and briefs are written from: `qq fleet`,
                                   `qq walkers` and `qq townprops` read it by default
  agents/car/lamp_briefs/          the brief each agent cutting one car's lamps is given, by kind
  _frames/<frame>.json             the record of every frame a kept variant came from, and its ancestors':
                                   the exact prompt, model, settings sent, cost, inputs by hash
  _frames/<clip>.mp4               a clip a kept variant was cut from, beside its record (rule 2)
  _inputs/<sha>.<ext>              the pictures those frames were asked from: layouts, style pictures, masks
  legacy.txt                       images under assets/ that trace to nothing yet

assets/<path>/<id>.src.json        beside the sprite: the variant it was made from, its px/m, its encoding
```

A sprite traces through its `.src.json` to its variant, through `variants.jsonl` to its frame, and through
`_frames/` to the first prompt — `qq sprites trace <id>` prints the chain.

## Rules

1. **Every image under `assets/` has a `.src.json` beside it or is on `legacy.txt`.** `qq sprites check`
   (inside `qq checks`) fails otherwise. An entry leaves the legacy list when `qq sprites use` replaces its
   image or the image is deleted, and nothing is ever added to it.
2. **A sheet is never committed; its cells are.** A frame stays in `.tmp/gen/`. A cell with a major defect
   stays there too. A cell worth keeping — the one put in the game and the real alternatives to it — is
   kept with `qq sprites keep`, which brings the frame's record and its inputs along; a parent frame (a
   masked redo's) keeps its record only, never its image. **A clip is the exception** (the owner, 2026-10-29): the
   video a kept variant was cut from is kept beside its record, since a walker is cut again from its clip and a clip
   is never asked twice alike. Its trials and rolls not kept stay in `.tmp/gen/`; `check` fails a kept variant whose
   clip is missing, and `drop` takes a clip no kept variant traces to.
3. **What is kept is never edited or renamed.** A new attempt is a new version; a variant named by a
   `.src.json` is never deleted. A candidate no sprite uses — a trial given up — is unkept with `qq sprites drop`,
   which takes its record, its lamp cut and any lineage no other variant traces to.
4. **Every frame says which asset each drawing is**: its record lists the ids in the brief's order (remotegen
   `--cells`), and `cut` finds the drawings in that order on the transparent frame, a grid's rows top to bottom and
   each row left to right. **What is drawn is the asset**: a car's drawing is its geometry (CAR-12b), measured; any
   other is taken whole and sized by the one figure its file authors — a footprint's width, a round asset's diameter,
   a strip's depth — the rest read off the drawing, a building's rectangles included. **The generator is never limited,
   the result may be corrected** (the owner, 2026-10-25): a drawing is cropped, sized and given a minor correction
   where it still looks right, and taken as drawn where a correction would show — most would. A walker's look is the
   exception that is many drawings: its frame's ids name looks, each a run of columns over the facing rows, and its
   variant is the block of them, aligned ([agents/person/prompting.md](agents/person/prompting.md)). A frame without its ids
   cannot be cut; one whose record names a layout is cut off that layout, so every kept variant can be cut again.
5. **Each kept variant gets its score when it is judged**, and the lesson of a set goes into its family's
   `prompting.md` in the same change, citing the variants it is drawn from. That file is read before the
   family's next brief is written.
6. **A cut is reproducible**: the same frame and record give the same variant, and `qq sprites use`
   gives the same sprite from the same variant.
7. **Every part an asset switches is in its picture, switched off.** A lamp, a beacon, anything the game
   later lights is drawn at rest — unlit glass, no glow, no light cast — where its variant file authors it, or for
   a car described in words where that kind of car has it, its file's lens then read off the zone map;
   and which of its texels each part is, is the lamp cut's: `qq props` cuts each lens off the picture and
   `qq props install` paints the cut as the zone map (`<id>_zones`), and the active state is those texels lit, never
   painted over by hand (CAR-14a). The brief names each part and where on the asset it is, never its shape — a lamp may be round,
   a strip, wrapped round a corner, whatever the asset wants; a drawing missing a part, or drawing one lit, is a
   failed cell. A part added to or moved in a file is had by asking for the asset again, never by painting it in.
8. **Every unit, building and prop look stands on the showroom once, and every change to one is looked at there**
   (`ShowroomPlan`, `--map Showroom`): every car, person and rolling-stock look standing still until a hand takes it,
   every building in the rows round the car park, every prop in the field between them and the yard. A look added to, changed in or dropped from a catalogue is not finished until a captioned
   shot of the showroom shows it standing as it should (`--shot .tmp/showroom.png --map Showroom --caption`, framed
   on it with `--at` and `--view`). The showroom reads the catalogues, so a look is on it once its catalogue lists
   it; a new kind of look the showroom does not stand yet is not finished until `ShowroomPlan` and the town stand it,
   and `ShowroomTests` says it stands once.
