# Prompting building roofs

What the roof briefs taught. Read before writing the next one; `qq prefabs brief --frame=<F>` writes a frame's brief
with every lesson here that a sentence can carry, and the brief each frame was asked with is in its record
(`qq sprites trace <id> --full`). The frames themselves are planned by `qq prefabs sheets`.

**Scale is said in metres against a car, look by look** (`DETAILS` in `.qq/prefabs.py`). Without it a 5 m cottage
came back with a car-sized chimney, as the legacy art has. With it: chimneys ~0.7 m, tile courses fine
(`roofs_house_a-v1`, `house_01`…`house_12`). Details still run 1.5–2× true on many flat roofs (AC units, stacks), so
many cells keep score 3; naming the fault as "three times too big" in a redo brought a 1.8 m chimney to 1.2 m
(`roofs_house_e`).

**A porch is part of the roof.** "A lean-to porch on the street side" was drawn as a box past the roof's bottom edge on
seven of seven cottages (`roofs_house_a-v1`); saying it is drawn inside the roof at its street edge fixed six of them in
a masked redo (`-v2`).

**The model will not paint within ~25 px of the picture's edge**: a roof asked there came back 10–30 % short
(`roofs_mixed_ab`, `_al`, `_u`). A frame of fewer than nine is planned with 64 px round each (`qq prefabs`
`BIG_GAP_PX`).

**No wall is ever seen.** Without saying so, stairwell boxes and lift rooms show a door face, dormers a window wall,
light wells their walls, a dome its drum with windows, and small shops a shop front (`roofs_apartments_u`, `_h`,
`roofs_mixed_d`, `_h`). "No wall, door, window or shop front seen; a light well is only its flat floor; a dome is a
circle" worked first time (`roofs_mixed_av`, `_o`).

**Words that draw the wrong thing**:
- "cross-shaped roof" draws a cross plan with the rectangle's corners empty (`roofs_mixed_f`, `_n`, `_h`); "a roof
  filling its whole rectangle with two ridges crossing at the middle" fills it (`roofs_mixed_o`).
- "a thin strip" on a cell 2.5–3× wider than deep drew it thinner still (`roofs_mixed_p`, `_m`); it is said only past 4×.
- a lean-to "with its ridge running from the street to the back" drew a gable (`roofs_mixed_c`); the describes say
  "its one slope falling to a long side".
- "rusty sheet metal" drew red planks; "grey-brown corrugated metal with rust patches" did not (`roofs_shed_a`). A shed
  is said to have no chimney.
- a barrel vault is drawn as a cylinder in perspective unless it is "a plain rectangle with straight ribs, the curve
  only as shading" (`roofs_industrial_p`); a sawtooth roof wants "seen from directly above, no zigzag edge"
  (`roofs_mixed_ay`).

**What a redo can and cannot fix.** A masked redo keeps everything outside the mask byte for byte, so it cannot fix a
drawing's size: the drawn extent comes back (`roofs_apartments_d`, `_e`, `roofs_house_d`). It fixes content — walls, a
shop strip, dormer faces (`roofs_apartments_o`, `roofs_mixed_am`, `roofs_ordinary_b`). For size the whole frame is
re-rolled with each fault measured.

**Round-ended roofs come back ~12 % wide** unless the shape is spelled out: "a short straight top and bottom
edge with a big half-circle at each side" fitted `house_round_01` (`redo_mixed_b`).

## The redo pass (2026-10-20) — 50 roofs, 17 frames, 5 v2s, no v3, $0.73

- **Quote the earlier fault in v1.** "Earlier attempts drew it 20–45 % too shallow" fitted six cells that had failed
  three times each (`tower_02`, `garages_12`, `industrial_16`, `industrial_26`, `industrial_deep_11`, `school_13`).
- **An onion dome from above**: "a gold circle shaded as a dome, the cross lying flat on it as a thin plus sign — never
  a side view, no drum, no standing cross", with "a roof filling its whole rectangle right into all four corners",
  worked on all six churches (`redo_mixed_a`, `_b`).
- **Still not cured by any wording**: stair and lift boxes show a thin front face, and AC units come out 1.5–2× their
  size; both are kept at score 3. A porch on a narrow-end house is drawn as a gable below a shortened roof
  (`house_deep_19`): drop the porch from such a brief.

## Fans (2026-10-24) — ten looks, a frame each, all kept first time, $0.12

The fans every roof's and vehicle's fan is drawn in (OBJ-6), asked one to a frame (the owner: "Generate 10 good
images", "Dont cut them") with eight of the art's largest fans as the style picture, and taken off the frame whole.

- **"One perfect circle, centred, about 70 % of the picture across … only the round fan: no square housing, no box, no
  bolts, no corners"** drew ten clean round fans on a clear ground, none clipped (`fan_*-v1`).
- **A blade count is a wish, not an order**: four of ten drew another — `fan_six_grey` eight, `fan_five_curved` six,
  `fan_seven_sickle` eight, `fan_four_rust` five — kept at score 3. Name a fan by its blades' shape and finish, not
  their number, or ask the count again in the brief's checklist.
- A wire guard drawn as rings and spokes (`fan_ring_guard`) turns with the blades, which a real guard does not; rings
  alone would read as standing still.
