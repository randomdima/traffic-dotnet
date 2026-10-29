# Prompting ground surfaces

What the ground briefs taught. Read before writing the next one; the brief each frame was asked with is in its record
(`qq sprites trace grass --full`). A surface's looks are asked tile by tile as `<surface>_<look letter><n>` from its
variant file's `looks`, and `qq sprites use` joins them into the surface's picture (`.qq/_surfaces.py`).

## What the owner ruled (2026-10-24)

- **No grid and no direction**: "dont use 'tiled' textures - they dont rotate nicely". The ground is anchored to the
  world's axes, so slab joints, ripples or streaks running one way are square to the world, not to the street.
- **Painted, like the game's old grass**, not photographic — and the brief says so, with the old grass beside the roofs
  and cars in the style picture.

## What the frames taught

- **Uniform fine detail comes back as speckle mush** at quality low (`ground_a-v1` all over, `ground_b-v1` grass). What
  reads as quality is the old grass's build: **"a calm, smooth base with gentle broad variation, and on it, scattered at
  random and spaced apart, distinct readable painted details … about a third of the area left as calm base"**, details
  a few decimetres across (`ground_b-v2` grass, `ground_c-v1`).
- **"Squares that share a description are the same ground … so they can be joined without a visible seam"** gave tiles
  of one look in one colour and density (`ground_b`, `ground_c`).
- **Every ground square is packed towards the top of its row**, 7–49 px; the scale holds within 2 %. `cut` looks for a
  square `SURFACE_REACH_PX` past its cell instead of asking again.
- **A feature asked for is a feature repeated**: a patch repair and a crack drew a 2 m square and a 6 m crack in every
  period (`ground_a-v1`); "rust-brown drip stains" drew streaks running down the picture (`ground_c-v1` cells 11, 12)
  until asked as "small round rust-brown stain spots" (`bridge_b*-v1` from `ground_c-v2`).
- **A worn look comes back yellower and darker than meant** (`grass_b*`, `sidewalk_b*`); the join halves that
  (`LOOK_TONE_KEPT`), so the brief need not fight it.
- **Pale stains on the deck repeat visibly** along a bridge (`bridge_a*-v1`): "a few small soft pale round water
  stains no bigger than a hand" fixed it (`bridge_a*-v2`).

## The walk's trials (2026-10-24)

- **Rustic is wrong for the walk**: flags, cobbles, pebble aggregate and warm sandy concrete (`walk_trial-v1`) were all
  "style is not fitting". The cobbles became the shore (`shore_*-v1`, kept `--as` from `sidewalk_e2`…`_g2`).
- **A row is drawn as one thing**: asked as 2 + 1 cells an option in reading order, the model drew whole rows of one
  option and shifted the rest (`walk_trial-v1`); laid as one option a row, it drew them in order (`walk_trial_b-v1`).
- **Asked-for leaves and cracks come back big and orange** — 20 cm leaves, cracks metres long (`walk_trial_b-v1`
  cells 3, 4, 7, 8) — and repeat visibly: leave them out of a worn look. A masked redo asking "no leaves at all …
  nothing orange or brown" cleared them (`sidewalk_l*-v1` from `walk_trial_b-v2`).
- **The owner chose the footway asphalt** (`sidewalk_k*`, `sidewalk_l*`): a city walk in the road's own material,
  lighter and finer. The concrete tiles before it stay kept as a trial look.

## Quality high (2026-10-24, the owner's allowance) — `ground_d`, `ground_e`, $0.21

- **Grass is the gain**: the same brief at high drew crisp blade tufts, clover, daisies and earth mounds at the old
  grass's sharpness, more painterly (`grass_*-v2`, score 5), where low drew soft speckle (`grass_*-v1`).
- **The grass's recipe ruins a paved surface**: "scattered distinct readable details" on asphalt and concrete drew
  ~10 cm diamond studs, cracks and oil blotches, and the owner judged the road "too 'damaged' all in some rubble"
  (`ground_e-v1`, dropped). What a paved surface wants is **"a smooth, even surface whose grain is very fine — tiny
  specks a pixel or two across, so close together they read as a texture and never as separate stones — with only
  gentle, soft, broad variations of tone"**, the faults named as not to be drawn and the worn look "still smooth and in
  good repair" (`ground_f-v1`, `pavement_*-v3`, `sidewalk_*-v3`, `bridge_*-v3`).
- **High draws water as rows of waves** running across the picture (`ground_d-v1` row 3); asked for "scattered ripple
  marks, never waves, rows, bands or lines", it drew raindrop rings on a diamond mesh of light (`ground_d-v2`). Neither
  kept: the water is still `water_*-v1` from low, whose soft ripples curve every way.

## The intercity road (2026-10-26) — `ground_highway-v1`, high, $0.058

Asked in words, with the style picture laid by `qq sprites style pavement_a1 sidewalk_k1 grass_a1 estate_steel
kombi_sage` (the town's road beside its walk and grass, and two cars), so the brief could say "clearly darker than the
town road in the attached picture".

- **Naming the surface it must differ from works in one go**: both highway looks came back near black (mean 49 and 41
  against the road's 75), distinct from the town's road where they meet at a junction (`highway_a*-v1`, chip seal,
  used; `highway_c*-v1`, smooth blue-black, kept as a trial). The worn look came back 15 lighter, near the road's own
  fresh tone, which the join halves.
- **A gravel shoulder may have its stones**: asked "densely scattered small stones one to three centimetres across",
  the shoulder drew readable pebbles at a calm even density, which repeat without a seam (`shoulder_*-v1`, score 4) —
  what ruins a paved surface is right for loose ground.
- **Twelve squares asked without a layout came back as twelve, in order**, about 2 % short of square; `cut` found each.
