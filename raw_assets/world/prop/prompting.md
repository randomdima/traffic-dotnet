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

## The intercity road's barriers (2026-10-26) — `props_street_guardrail`, 2 frames, $0.035

Twelve looks under `street/guardrail/`, planned apart from the town's ([guardrail_plan.md](guardrail_plan.md)), the brief
`townprops brief`'s with the plant lines swapped for barrier ones: a section lies along the width, "cut straight and
square … the beam reaching right to it", the beam along the bottom edge and the posts behind it.

- **Sections butt cleanly as asked**: every rail came back square-ended and in line, posts near the quarter points, so
  4 m and 2 m sections run on without a step (`guardrail_4m-v1`, `guardrail_2m-v1`, score 4).
- **A long thin thing comes back deeper than asked, and asking harder makes it worse**: rails asked 0.45 m deep came
  back 0.53–0.56 m (`-v1`); told they were 25 % too deep, they came back 0.66–0.71 m (`props_street_guardrail-v2`).
  The double-sided median came back 0.96–1.06 m of 0.6 both times (`guardrail_median_4m-v1`, score 3). Take the depth
  the first frame draws.
- **A concrete barrier is drawn in three-quarter view** — its front face and sloped ends showing like a brick
  (`props_street_guardrail-v1` cell 4); "its outline is a long thin rectangle with square ends, the top ridge a lighter
  line along its middle" drew it from above (`concrete_median_4m-v1`, score 3).
- **A drum is drawn as a cone, side-on** (`-v1` cell 11) until described as rings: "concentric rings, the orange top in
  the middle, a white band ring round it and the black base ring outside" (`traffic_drum-v1`, score 4).
