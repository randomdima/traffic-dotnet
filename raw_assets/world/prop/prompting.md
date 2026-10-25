# Prompting props

What the prop briefs taught. Read before writing the next one; `qq townprops brief --sheet=<S>` writes a sheet's brief
from a prop plan with every lesson here a sentence can carry, and the brief each frame was asked with is in its record
(`qq sprites trace <id> --full`).

## The first catalogue (2026-10-25) — 203 looks, 16 sheets, $0.38

A plan of 206 looks in three sets (`wild/`, `planted/`, `street/`), 9 to 16 to a sheet grouped by kind and size, each
asked in words with one style picture (in-game grass and two cars at one scale) and cut whole (raw_assets/index.md,
rule 4). 203 kept, 3 dropped.

- **Layouts were not held** (`props_trees_large-v1`, `-v2`, `props_trees_mid-v1`): every crown came back 10–35 % over its
  outline, the smaller ones worst, the model evening the sizes out; giving each outline's place as shares of the picture
  halved it on the big ones only. This, with the cost of a second picture, is why no layout is sent any more. Described,
  a frame costs $0.014 against $0.026 and every drawing was found in its order.
- **The model draws a row's things at one size** whatever metres the brief gives them; `cut` sizes each by its file, so
  that costs nothing. What the metres still buy is the size of the details.
- **Proportion holds for boxes, roughly**: benches, hedges and troughs came back within 10–25 % of their depth
  (`props_d_beds_hedges-v1`, `props_d_street_seats-v1`); the drawing's depth is what `use` writes. A footprint that
  contradicts its words is drawn as the words say: a wheelbarrow planned 1.4 m wide "with the wheel at the front" was
  drawn front to back (`wheelbarrow-v1`).
- **Patches of low plants come back as domed cushions** with a hard round edge (`props_d_growth_small-v1`); "lies flat on
  the ground: an irregular, ragged, soft-edged patch with gaps and stray tufts at its edge, never a round ball, dome or
  cushion" fixed them in one re-roll (`-v2`).
- **A columnar tree is drawn as its side silhouette** — poplar, thuja and juniper as tall leaf shapes with a trunk
  (`props_d_shrubs_large-v1`). The brief now says such a tree is a small round dome from above; untested.
- **A masked redo fails on a transparent described frame**: it leaves places empty or paints fragments
  (`props_d_shrubs_large-v2`, and `props_trees_large-v3` with a layout and a style picture beside it). Re-roll instead.
- **The oblique view is taken freely**: allowed for looks that never turn, it was drawn for turned ones too — troughs,
  cabinets and the post box show their front wall (score 3). Their wall always faces the walk, which reads acceptably.
  Small cylinders (bollards, the ashtray) come back mostly side-on.
- **Gaps inside one drawing split it**: crates and kegs stacked with clear gaps were found as several drawings; `cut`
  now closes gaps up to 32 px when it finds more drawings than ids (`props_d_street_goods-v1`).
- **Generic symbols creep in**: a recycling sign on the paper bank (`bank_paper-v1`, score 3) despite "no logos".
