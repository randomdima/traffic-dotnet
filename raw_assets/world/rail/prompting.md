# Prompting the railway

What the railway's briefs taught, set by set. Read before writing the next one; the brief itself is in each frame's
record (`qq sprites trace <id> --full`).

**A track is two strips the ground repeats along every line** (`RAIL-3`, `RAIL-13`): its sleepers and rails on clear
(`Track`, sized by the sleepers' length), and the bed of ballast under them at grade (`Ballast`, sized by the 4.4 m
band); `use` makes each seamless. The gauge and the sleepers' length are the brief's words. Where its rails land off the
gauge is a correction for the cut to make, if a minor one still looks right; nothing yet makes it. The all-in-one
tracks of `railway_a` were dropped on 2026-10-25.

## railway_a (2026-10-19) — four tracks and five pieces of crossing gear, three attempts, four kept, $0.10

Kept from v1: `track_concrete-v1` (used), `track_concrete_b-v1`, `track_timber-v1`, `track_worn-v1` (4 each). Not kept:
`barrier_arm`, `barrier_arm_b`, `crossing_head`, `barrier_drive`, `relay_cabinet`.

- **Worked:** the tracks' look at once — granite, blue-grey and brown ballast, concrete and timber sleepers with their
  clips, rails with a polished head and rusty edges — and **the rails at the gauge** (100 px for 103) **and centred in
  the strip**, square-ended so it tiles. The gear's look too: a banded arm with its lamps and hub, a yellow drive housing,
  a cabinet with a vent.
- **Failed, every attempt: the ballast's width.** The strip stops a shoulder past the sleeper ends, two thirds of the
  outline (189 to 200 px for 284), wherever the rails' guide lines were — the rails are drawn at the gauge about the
  strip's own middle, not on the lines. Neither naming it (v2, a masked redo, which also grew the ballast unevenly) nor
  dashed sleeper-end lines (v3) moved it.
- **Failed, every attempt: the small pieces' size.** In a 3×3 grid laid for the 7.2 m tracks the gear's outlines are
  19 to 58 px, and every piece came back 1.5 to 3 times that; anchoring each to a sleeper's length (v3) brought the arm
  to 13 % long and the head to 43 % tall, the drive and the cabinet still twice.
- **A masked redo over every cell corrupts the small ones** (v2): the arm's tip, a second signal head and a second drive
  housing drawn over the first.
- **Next:** the gear on a sheet of its own at its own scale, so a 4 m arm fills a cell — the rows layout lays it at the
  ground's grid, too small to draw.

## railway_gear_a (2026-10-19) — nine pieces of crossing gear alone, three attempts, eight kept, $0.09

Kept: `barrier_arm-v1` (used), `barrier_arm_b-v1`, `barrier_arm_c-v1` (4 each) from v1; `barrier_drive-v1` (used),
`barrier_drive_b-v1`, `crossing_head-v1` (used), `crossing_head_b-v1` (4 each) and `relay_cabinet-v1` (3) from v3. Not
kept: `relay_cabinet_b`, 16 % short of its outline's height at its best.

- **Worked:** the gear alone, laid at its own 116 px/m, drew the booms right at once — even bands, a square-cut left end,
  a capped tip at the thick right edge — and every piece's look from v1 on: a yellow housing with vents and its slot at
  the front, a galvanised one with striped edges, the heads' hoods open to the front with dark glass in the framed lamp
  areas. Asking for redraws of image 1 (v2, v3) kept that look and moved only the sizes.
- **The boom's end cap stands a few pixels proud of it** every time; `cut` reads a long thin asset across by its long
  edges' median (`THIN_ASPECT`), as it reads a car by its flanks, and clips the cap to the outline.
- **Failed, every attempt: the small pieces' size.** v1 drew them 1.5 to 1.8 times their outlines, "two thirds of the
  size in image 1" (v2) brought that to 1.1 to 1.3, and per-cell percentages (v3) to 1.04 to 1.16 — the model shrinks
  toward an outline by about half the gap each time. The owner allowed `cut` to fit the gear by up to 16 %
  (`FIT_GEAR_MAX`). The heads keep a little of the backboard's side face however "straight from above" is put.
- **Next:** ask the small pieces at about 0.85 of their outline in the brief's words from the start, so the first frame
  lands where the third did.

## railway_signs_a (2026-10-19) — three heads and six signs, upright, one attempt, six kept, $0.03

Kept from v1: `crossing_head-v2` (used), `crossing_head_b-v2`, `crossing_head_c-v1`, `crossing_cross_b-v1`,
`crossing_countdown-v1`, `crossing_countdown_b-v1` (5 each). Not kept: `crossing_cross` (14 % wide),
`crossing_warning`, `crossing_warning_b` (20 % wide, 15 % tall).

- **A head is drawn upright, north up, from the front** (the owner: a head drawn straight down shows its hoods sideways
  once turned with the road): "standing upright, seen from the front and a little above, its top at the top of its
  outline and its post's foot at the bottom", "every piece faces the viewer", gave lamps with their hoods on top, the
  glass in the framed areas, first time.
- **Saying the earlier sheets drew 10 to 20 % big, and to keep inside the outline**, held the posts and boards to
  their outlines; the crosses and triangles still came out wide, their arms and corners pushed to the cell's width.
- `cut` flags every upright piece with holes — magenta stands around the post inside its box — which is the shape, not a fault.

## railway_lights_a (2026-10-19) — five X lights, four with a lit twin, one attempt, all kept, $0.03

Kept from v1: `crossing_light-v1` and `crossing_light_lit-v1` (used), `crossing_light_b`, `_b_lit`, `_e` (5 each);
`crossing_light_c`, `_c_lit`, `_d`, `_d_lit` (4 each).

- **A light's lit state as its own cell, beside the dark one**: "copy the cell just before exactly — the same housing,
  rim, bolts and lens, pixel for pixel — and change only the X" drew each pair matching, the lit X with its halo inside
  the housing, first time.
- **Square, symmetric, flat from the front**, "no visor, hood, post or side face", gave housings that read the same at
  any turn — what a light turned with its lane needs. Every cell landed within 1 % of its outline.

## rail_track_layers (2026-10-25) — four sleeper strips and five ballast beds, one attempt, eight kept, $0.01

Kept from v1 at 4: `sleepers_concrete` (used), `sleepers_timber`, `sleepers_worn`, `ballast_granite` (used),
`ballast_bluegrey`, `ballast_dusty`, `ballast_weedy`, `ballast_rusty`. Not kept: `sleepers_twinblock`, drawn as plain
concrete sleepers.

- **"The game draws a track in two layers, and this sheet is both"**, then "NO ballast and NO ground: fully transparent
  between the sleepers" and "the beds have NO sleepers and NO rails", gave both halves clean at once.
- **"Frays out the way a gravel path's edge frays into a lawn … lit evenly from above with no dark underside"** cured
  both faults `rail_shoulders` could not shake: the beds came back flat, their edges scattered at random.
- **The beds fray at their short ends too**, which repeats as a step in the bed's width; `use` crops a bed to the
  columns as solid as its median one before making it seamless.
- **Rails at about the gauge** (1.47 m between centres for 1.6) with "inner faces 1.52 m apart, a little more than half
  the strip's depth", the sleepers the strip's whole depth.

## rail_shoulders (2026-10-25) — nine ballast shoulders, two attempts, all dropped, $0.03

The loose stone past the sleepers' ends, composed into the old track's clear margins: given up for a bed of its own
(`rail_track_layers`), and dropped.

- **Worked:** the stones at the track's own size from the style picture of `track_concrete` alone, the nine tones
  asked, a straight solid bottom edge.
- **Failed, both attempts:** every strip a low heap seen a little from the side — each stone shaded underneath, a dark
  line along the bottom — and its top edge one even row of bumps rather than stones scattering. Naming both faults
  with v1 as image 1 (v2) changed almost nothing: the edit held image 1 too closely.
- **Next:** a fresh frame from the style picture alone, describing the strip as a flat bed "like a gravel path's edge
  fraying into a lawn" rather than a heap; strips came back 4 : 1, not the 9 : 1 asked.
