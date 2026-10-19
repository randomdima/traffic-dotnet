# Prompting cars

What the car briefs taught, set by set. Read before writing the next one; the brief itself is in each
frame's record (`qq sprites trace <id> --full`).

**Never an open-top vehicle** — no convertible, roadster, open cockpit or open load bed with people in it.
Every car is drawn with its roof on: the game has no driver to sit in a seat seen from above (the owner,
2026-10-18).

**A front lamp is one unit at each corner**: the headlamp's frame on the inboard part of the corner (half-width
share 0.56) with the indicator's touching it at the flank end (0.84), as CAR-14b reads a cluster. A headlamp
frame out in the middle of the nose is drawn as bare bonnet (`cars_civil_b-v1`).

**A wreck is asked from the finished cars**: the layout of `<id>_broken` outlines (no part frames) as image 1 and
the installed cars, in the same order, as image 2 (`qq sprites template … --style=<the car ids>`).

**A sheet's lamps are mapped by its zone map** (CAR-14a): image 1 the sheet the cars were cut from, alone, no mask,
and this brief (the ask only; the record holds it whole). The colours are `qq sprites`' `ZONE_COLOURS`:

> Make the same image as a zone map: keep every car exactly where it is, at exactly its size, outline and shape, and
> repaint each part of it as one flat, solid colour… Bodywork pure white #FFFFFF; every window pure black #000000;
> each tail lamp lens pure red #FF0000; each amber indicator lens pure yellow #FFFF00; each headlamp lens pure blue
> #0000FF; chrome trim mid grey #808080. Each lamp zone covers exactly the lens drawn in the original…

## cars_civil_zones (2026-10-18) — the nine zoned, four sheets at v1, $0.074

`cars_civil_b_zones`, `cars_civil_c1_zones`, `c2_zones`, `c3_zones`; all nine kept from v1 (`<id>_zones-v1`, 4).

- **Worked at once:** flat hard-edged zones on every car, the layout kept, and fitted to the outline as the art is
  they land on its lenses within a texel or two — rears and indicators exactly, headlamps a texel towards the nose on
  the larger bodies. Every one of the 54 lenses found its zone.
- The model zones lenses the template never framed (an amber dot at a rear corner): nothing reads them.
- **Tried first, `cars_civil_lit`** ($0.220): the sheet masked-edited to show every lamp lit, each lamp the
  difference. The lamps burned in place, but the owner did not take it; the zone map replaces it.

## cars_civil_wreck_a (2026-10-18) — the nine civilian wrecks, v1, $0.034

All nine kept from v1 (`<id>_broken-v1`, 4 each). Every one is plainly its car: same paint, roof and trim, the nose
pushed in and the bonnet buckled, cracked windscreen, scrapes to bare metal, nothing past the outline, no tyres,
no fire.

- **Worked:** image 2 being the cars themselves rather than a style picture of other cars; the wreck described as
  a list of damage, with "pushed in, not out" for the nose and "no debris scattered around" — the shipped wrecks'
  debris is what made them carry a `scale`, and these need none.
- **The corners come back rounder than the outline** (1–3 % holes), as the cars' do.

## cars_civil_c (2026-10-18) — the nine again under the new rules, v1 + two masked redos, $0.101

Kept: `citybox_sky-v3`, `saloon_beige-v2` (v1); `suv_graphite-v3`, `panelvan_forest-v3` (4), `minivan_wine-v2`,
`hatch_coral-v2` (3) (v2); `micro_mint-v2`, `retro_cream-v2` (3) (v3). Installed: `saloon_beige`, `suv_graphite`,
`minivan_wine`, `micro_mint`, `retro_cream`; the other four keep their `cars_civil_b-v3` art, which is as good.

- **The width in the cell line itself fixed both misfits**: "very short but as wide as a normal small car, its
  flanks straight and full width" (`micro_mint`, 13 % narrow in every `cars_civil_b` attempt) and "long and slim,
  narrower than a modern car" (`saloon_beige`, 9 % wide) both fitted within 5 % in v1.
- **The indicator is drawn where the model thinks it goes**, on the corner wrap or a little back along the flank,
  whatever the frame says. Naming the fault cell by cell in a mask redo fixed it on four of seven (v2).
- **On a very round body no wording moves a lens onto its frame**: `micro_mint` and `retro_cream` drew their tail
  lamps on the rear curve and their front lenses towards the nose tip in v1, v2 and v3 alike — v3, with the way each
  lens is off spelled out, came back near pixel-identical. The frames are what to change (`docs/tech-debt.md`).
- **The bigger bodies' corners come back rounder than the outline**: 2–3 % holes at the corners on all five.

## cars_civil_b (2026-10-18) — the set again with headlamps, three whole re-rolls, $0.107

Kept: `citybox_sky-v2`, `hatch_coral-v1`, `suv_graphite-v2`, `estate_navy-v2`, `panelvan_forest-v2` (4 each),
all from v3. Failed after three: `micro_mint` (13 % too narrow every time), `retro_cream` (indicators on bare
paint), `saloon_beige` (9 % too wide), `minivan_wine` (a tail lamp beside its frame).

- **v1, headlamp frames between the indicators and the nose's middle:** every car drew one lamp unit at each
  corner over the amber frame and left the blue frames as bonnet. `cut`'s `drawn` passed them, the frames
  taking in the nose's dark edge; `_parts.png` is what showed it.
- **v2, frames moved to the corner unit, the fault named:** headlamps landed in their frames on eight of nine,
  and seven dropped the indicator. Re-rolling also moved the shapes: the bottom row came 34 px low (fitted
  once `cut` looked over the whole cell) and three bodies 9–13 % off in width.
- **v3, part frames drawn at 2 px and the missing lenses named:** indicators came back on most, as their own
  amber lens or as the flank end of the headlamp, which CAR-14b takes. The narrow microcar and the wide old
  saloon came back as they had in v2: those two need their width in the cell line itself, not a sentence
  after it.
- **Each re-roll trades one cell for another**: three whole re-rolls of nine never gave nine good cells. A set
  whose cells fail for different reasons wants its good cells kept from whichever attempt drew them and a
  mask redo for the rest, not another whole frame.

## cars_civil_a (2026-10-18) — nine civilian city cars, v1 + two masked redos, $0.106

Variants: `micro_mint-v1`, `citybox_sky-v1`, `retro_cream-v1`, `saloon_beige-v1`, `estate_navy-v1`,
`suv_graphite-v1`, `minivan_wine-v1`, `panelvan_forest-v1` (4 each). The ninth, an open roadster
(`cabrio_white`), was dropped: open-top is refused (above). Its cell is cell 5 of `cars_civil_a-v3`.

- **Worked:** one cell line per car from its file's `describe`, with the body's length × width, its ratio
  and its corner rounding in words; the style picture from six in-game cars none of which was asked for
  (`qq sprites template --style`). Every body type came back as asked.
- **Say "do not draw tyres or wheels":** the game draws them under the body (`CarSheets`), and a drawn
  tyre is a second wheel and a false flank for CAR-12. None came back with tyres.
- **The model draws a wide body to the footprint, not the outline:** the 1.8 m SUV, people carrier and van
  came back 6–8 % wider (`cut`'s fit squeezed them; the van at 8.3 % was refused and redrawn). Naming the
  fault in the redo ("about 8% too wide, its sides on the outline's edges") fixed it at once
  (`panelvan_forest-v1`, v3).
- **Every body lands a few pixels off its outline** (shifts up to 21 px, scale within 7 %): that is what
  `cut` fits, so a clean cut does not need the brief to fix it.
- **A lamp frame on a very round tail can be skipped:** `retro_cream` drew one rear lamp of two in v1;
  "BOTH rear tail lamps … one at the top corner and one at the bottom corner, mirror-symmetric" fixed it
  (v2). On round noses the headlights fill the indicator frames rather than a separate amber lens.
- **Mirrors reach past the 0.1 m a side the footprint allows** and are clipped flat at its edge. Harmless at
  game zoom; a brief asking for smaller mirrors has not been tried.
- **The finish is finer than the old fleet's**: smoother shading and thinner outlines than the chunky
  in-game sprites the style picture shows. Consistent within the set; "chunky pixel art" in the brief does
  not pull it further.
