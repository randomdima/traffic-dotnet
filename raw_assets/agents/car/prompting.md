# Prompting cars

What the car briefs taught, set by set. Read before writing the next one; the brief itself is in each
frame's record (`qq sprites trace <id> --full`).

**Never an open-top vehicle** — no convertible, roadster, open cockpit or open load bed with people in it.
Every car is drawn with its roof on: the game has no driver to sit in a seat seen from above (the owner,
2026-10-18).

**The background comes back transparent** (the owner, 2026-10-20; remotegen sends `background: transparent`): the
layout stays on magenta, and the brief says "outside the cars the picture is fully transparent: no magenta, no
background colour, no shadow, nothing half-transparent" in place of the magenta line. Every frame before
`cars_fleet_b` is magenta and chroma-keyed.

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
> #0000FF; each reversing lens pure green #00FF00; chrome trim mid grey #808080. Each lamp zone covers exactly the
> lens drawn in the original…

## cars_fleet_a_lit (2026-10-20) — the lit state as an edit of the sheet, a trial, $0.018

`cars_fleet_a_lit-v1`: image 1 `cars_fleet_a-v3` alone, masked to the seven kept cars' four corners (`mask … id:nose
id:tail`), brief `.tmp/gen/briefs/cars_fleet_a_lit.txt` ("every lamp switched on, change nothing else"). Not kept.

- **The bodywork held**: moved by the unlit variant's own fit (the cut's `fit`), 83–86 % of each car's texels are within
  3 of the sprite, and the rest is the masked corners. A fresh `cut` of the lit frame fits it differently (the halo
  widens the body), so a lit frame is moved by its unlit sheet's fit, never its own.
- **The lenses lit in the shapes the art drew them**, with a white core and a halo two to eight texels onto the
  bodywork: a lit texture far better than the art's unlit glass ramped (`LampAtlasBake.Lit`). Heads and front
  indicators lit as separate lamps.
- **A rear cluster lit as one lamp**: red with an amber-yellow core, no white reversing section, whatever the brief
  said, as the unlit art did. Cut by the zone map's sections, the brake, indicator and reverse states are pieces of one
  lit cluster with square seams, and the reverse burns red.
- **A lit colour does not say which lamp it is**: a red lens's hot core is amber by hue, so the kind is not read off
  the lit frame's colour.

`cars_fleet_a_lit_ind-v1` and `_rev-v1` ($0.036): the same sheet and mask, one group of lamps each (briefs beside
the first). A state is its frame's brightening over the unlit sprite, ramped from 14 to 60 of 255; no zone map.

- **One group a frame works where the art drew the lens**: only the indicators lit, front and the amber section of
  each rear cluster, with the red and the heads left dark — a clean indicator state of each side, by delta alone.
- **A lens the art did not draw is not lit**: the reversing frame changed nothing but redraw noise, though the brief
  said the innermost part of an all-red cluster is the reversing lens. The model lights what is there, never invents one.
- **A state is not had by subtracting one frame from another**: the heads taken as the all-on frame less the
  indicator frame's change came out as broken pieces, the indicator's halo lying over the headlamp. Each state is
  asked as its own frame.
- `cars_fleet_a_lit_hb-v1` ($0.018, heads and brakes only): the heads lit as brightly as in the all-on frame, the
  brakes faintly — a red a little deeper, the amber section turned red. The all-on frame's tails make the better brake.
- **A one-group frame still brightens its neighbours' glass**: the indicator frame lit the headlamp beside each
  indicator too, and the heads' frame the indicator lens. Which lens a texel is stays the zone map's; the lit frames
  give each lens its light, added (never replacing a texel, or a flash darkens the lamp lit beside it), and the glow
  off the body is that light blurred — the frame's own glow off the body exists only inside the mask's boxes.

## lamps_trial (2026-10-20) — isolated lit lamps beside their cars, a trial, $0.019

`lamps_trial-v1`: three kept cars (`hatch_graphite`, `crossover_pearl`, `midsaloon_black`) at the left of three rows,
the rest masked, brief `.tmp/gen/briefs/lamps_trial.txt`: five lamps of the car's top side, lit, isolated, on clear.

- **Clean isolated sprites**: five in each row in the order asked, each a lit glass with a white core and a soft glow
  fading to clear, the cars kept byte for byte. Cut by connected alpha, nothing to match.
- **Not the car's own lamps, and not to scale**: the three rows are the same five generic shapes, 2–4× the size the
  car draws them (a headlamp 46×83 px for a 0.29 m lens at 95 px/m), whatever the brief said about size and shape.
  Fitted onto the zone's box they are squashed (to 0.12 of their height); at one scale they keep their shape and miss
  the lens the art drew (a straight brake bar over a curved cluster).

## lenses_trial (2026-10-20) — the car's own lenses, cut out and lit, a trial, $0.018

`lenses_trial-v1`: image 1 alone, the 15 top-side lenses of the same three cars cut out of each sprite by its zone
map, laid isolated on clear at 4× by whole texels (a 6-texel lens is a smudge at 1×), brief
`.tmp/gen/briefs/lenses_trial.txt`: light each in exactly its own outline, a glow of 40 sheet px.

- **Shape, size and orientation held**: lit glass over its own piece at IoU 0.68–0.93 (0.80 typical), the glow round it
  fading to clear, colours by column as asked — the reversing piece white though cut from a red cluster.
- **Placement is by construction**: each lens goes back where it was cut from, a mirror about the centre line for the
  right side; nothing is fitted, and on the car the lit lamp lands on the lamp the art drew.
- Wording that held it: "exactly its own outline, size, position and orientation … not moved, not enlarged, not
  reshaped, not rotated", the pieces called "lamp glass cut out of car sprites".

## combos_trial (2026-10-20) — every corner combination in one frame, a trial, $0.019

`combos_trial-v1`: four kept cars a row, each pasted three times across it; the mask is each copy's two ends (0.75 m
of body and 0.25 m past it for the glow); brief `.tmp/gen/briefs/combos_trial.txt`: column 1 heads and brakes, column
2 indicators only, column 3 both.

- **One instruction a column held on all twelve copies**: each column lit what it was told and nothing else, the
  lit lenses in the shapes the art drew them, a soft glow onto the bodywork and past the car's edge.
- **A corner is cut as the rectangle it was masked**, split at the car's centre line: no fit, no lens cut, no mirror.
  Laid on the unlit car its edge does not show — the mask's feather is the car's own pixels — and a corner showing
  both lamps is the frame's own picture of both, so nothing is added or subtracted.
- The art's reversing lens is still not there to light; the trial drew reversing as a glow on the ground.

## layers_trial (2026-10-20) — one lamp a corner, the rest erased, a trial, $0.019

`layers_trial-v1`: four kept cars, two copies each; each masked corner box asked for one lamp lit and everything
else in the box erased to clear (copy 1: head, front indicator, brake, rear indicator; copy 2: reverse).

- **One lamp a corner held**: every box lit the lamp it named and no other, the reversing lamp a small white spot at
  the inner end of the cluster.
- **Nothing was erased**: the bodywork came back 98–100 % opaque in every box, redrawn a texel or two off (23–43 % of
  its texels moved by more than 30). A lamp-only layer is not had from the model.
- Its light taken as the brightening over the unlit sheet stacks, but the redraw shows as glints at the body's
  edges, and an indicator added over a lit headlamp beside it washes pale.

## zones_trial (2026-10-20) — the installed sprites zoned at their ends, lit by the tool, a trial, $0.039

`zones_trial-v1`, `-v2`: eight installed unlit sprites at their own size, one a cell, masked to 0.8 m of each end,
with a legend of the keys drawn in the band at the top (template `.tmp/zone_sheet.py`, brief
`.tmp/gen/briefs/zones_trial.txt`). The keys are colours no lamp has: head `#0000FF`, indicator `#00FF00`, tail and
brake `#FF00FF`, reverse `#00FFFF`. Cut and lit by `.tmp/zone_lit.py`: a texel is the nearest key its end may hold
(front: head, indicator; rear: brake, indicator, reverse), on the body and changed from the sprite. A lens is the
sprite's own texels burning in the lamp's colour by their lightness, and its glow a blur of the zone on a padded canvas.

- **Zones land in sprite coordinates**: the sprites were laid at their own size and the outside of the mask comes back
  byte for byte, so there is no fit and no move. Every lamp lit where the art drew it, in the art's own shape.
- **v1 did what the old sheet maps did**: every rear cluster magenta, and the indicator painted across the middle of
  the tailgate. v2 says the rear zones lie inside the clusters, nothing on the tailgate, and that the orange section
  is the indicator. It split each cluster magenta, green and cyan, with the cyan at the inner end.
- **A key comes back pale**: `hatch_coral`'s headlamps came back light blue and partly off the lens, nearer cyan than
  blue. That is why an end holds only its own kinds. Its top headlamp is still weak (181 texels against 600–840 on
  the others).
- **The zones are blocky and only roughly mirrored**: the top-against-bottom overlap is 0.5–0.7 for heads and brakes
  and 0.1–0.5 for indicators and reverse. They are a few texels off the lens edge, and the light shows that edge.
- **Against a map read off the sprite alone** (`.tmp/zone_tool.py`): read by texel colour, the tool's zones follow
  the lens where it stands off its paint and fail where it does not — a red car's tails, a white car's heads. Read by
  the regions the art's black lines close off, each classed by its median colour, they find every lamp of the eight
  cars, symmetric and smoothed, with no generation. Its lines are black or a ridge darker than both sides (a lens's
  border is often dark red or grey); a lamp's glass is a region whose median is neutral and at least 0.5 light (lenses
  0.64–0.89, trim ≤ 0.31); red and amber split at 18° (red lenses 354–5°, amber 25–36°). A missing indicator is the
  outboard half of its main lamp, and a missing reversing lens the inboard third of the tail, overlapping both (the
  owner). On red paint only a lens the lines close off counts; where they do not, the tail is short (`supermini_red`).
  The model's map always knows which lamp is which; it never knows the lens edge, so the tool's map is the one.
- **Cut as shapes by an agent** (`qq props`): blobs from either map are not a lamp's outline. A lens is a few arcs and
  near convex, so a prop is a mirrored arc polygon with rounded corners, put on by eye, snapped to the rim the
  placement's own colours imply, and looked at again; a missing indicator or reverse is a part of its main lamp
  (`"part"`), not a shape of its own. Four agents cut seven cars from the tool's help alone, 3–7 looks a car. The art
  is lopsided by 0.5–3 texels side to side on most of them, along x as well as y, so one axis an end leaves a copy a
  texel or two off; a prop takes its own `axisY` and `mirrorDx`. The cars' `lamps` rectangles are nowhere near the
  lenses on any of the eight and serve only as names. Against the thirteen indicators that have a lens of their own,
  the half-lamp fallback lands on it only where the art put the indicator inside the main lamp's housing (38–97 % of
  it covered: `hatch_coral`'s front, three rears); where it drew a lens beside the main one (the six other fronts,
  three rears) the fallback covers none of it, so it stays a fallback and never stands in for a lens that is there.
  A pale lens on pale paint (`crossover_pearl`'s head) has no colour of its own: snapped by its colours it stops at its
  highlight's edge, a texel or two inside, and snapped to its dark frame (`--pull=frame`) it reaches the frame on both
  sides. An agent tuning it found the unrolled edge strip and the per-side rims in the edge table were what showed the
  art lopsided, and the plain picture at 9–12 screen pixels a texel what decided a move; the filters other than dark
  and lit, and a sheet of several at low zoom, decided none.

## video_hatch (2026-10-20) — the lamps animated by a video model, a trial, $0.17

`video_hatch-v1` (Grok Imagine Video 1.5 Lite, 3 s, 480p, $0.07) and `video_hatch_seedance-v1`, `-v2` (Seedance 1.5
Pro, 4 s, 480p, $0.048 each): `hatch_graphite` at 1.5× on flat asphalt as the first frame; heads and tails on at 0.5 s,
the left indicators blinking from 1 s. v2 forbade glow, halo, bloom and flare in as many words.

- **All three drew flares and never lit the lamp** (the owner, by eye): light was painted over the ends, and the lens
  glass the art drew stayed as it was. The no-flare wording did not change it.
- No alpha on any listed model, an H.264 clip (nothing here decodes it but a browser), and one timeline where the game
  needs every combination: the combination frame (`combos_trial`) is the way, not a clip.

## Style picture size (2026-10-20) — four sizes, one brief, $0.060

`cars_fleet_b_sa`…`_sd`: batch 2's brief with three cars (`crossover_pearl,estate_steel,kombi_sage`) as the style
picture at four sizes, one frame each. Width error is the measured body against the class's width.

| Frame | Style picture | Input tokens | Cost | Cells past 8 % | Mean width error |
|---|---|---|---|---|---|
| `_bare` | none | ~900 | $0.0104 | 5 | 7.5 % |
| `_sb` | row, 1024×177, 62 px/m | 1478 | $0.0132 | 1 | 5.4 % |
| `_sd` | stack, 300×476, 54 px/m | 1696 | $0.0149 | 2 | 6.3 % |
| `_sc` | stack, 494×732, 95 px/m | 1830 | $0.0160 | 5 | 7.6 % |
| `_sa` | row, 1482×244, 95 px/m | 1878 | $0.0164 | 0 | 3.5 % |
| `_style` | six cars, 1536×1024 | — | $0.0227 | 2 | 5.7 % |

- **Without a style picture the view is not straight down** (the owner, by eye): the sides of the bodies and the
  tyres show, as in a three-quarter view, whatever the brief says about orthographic. The style picture is what
  holds the camera overhead.
- **The full-resolution row is the pick** (the owner, 2026-10-20): no cell off, at $0.003 a frame more than the cheapest. The style itself
  came out alike from all four; the stack at full resolution drew tyres on the 4×4.
- Tokens are not by 512-px tile: the small stack cost more than the wide row half its size.
- **Nothing is cached**: the same request sent twice 23 s apart (`cars_fleet_b-v1`, `-v2`: picture A, the same brief,
  1878 input tokens) reported `cached_tokens` 0 both times and was billed the same. The docs agree (2026-10-20):
  OpenRouter's `/images` takes no caching field, and Sunburst's endpoint record prices only `input_image` $8,
  `input_text` $5 and `output_image` $30 a million tokens, passing nothing upstream but `moderation`. OpenAI prices
  cached input for Sunburst ($2 image, $1.25 text) only through its own Responses API, a different provider and
  a chat model's tokens on top; OpenRouter's `openrouter:image_generation` tool sends the image model no
  reference picture. The one way to pay an input once is `n` pictures from one request.
- One frame a size: a difference of a cell or two is within what a re-roll moves.

## cars_fleet_b trial (2026-10-20) — described, no layout, transparent: $0.033 for two frames

The same nine (batch 2) asked twice from `qq fleet brief`: `cars_fleet_b_bare-v1` with no picture ($0.010) and
`cars_fleet_b_style-v1` with batch 1's six installed cars as the style picture ($0.023).

- **The grid held in both**: nine drawings in three rows, the brief's order, found by `cut` with nothing to match.
- **The style picture halves the proportion faults**: 2 of 9 bodies past 8 % off their class's width (the microcar
  12 % narrow, the rear-engined saloon 19 % wide) against 5 of 9 without it (+9 to +14 %, all wide). Without it the
  style is chunkier than batch 1's and two drew tyres.
- **Transparent output is a soft matte**: bodies at alpha ~253, never 255, and a faint glow of each car's own
  colours round it at alpha under ~100. `cut` makes the body solid and keeps only a two-pixel rim over alpha 64.

## cars_fleet_a (2026-10-20) — the real-class fleet's first nine, full lamp set, $0.17

Bodies v1, v2 (whole re-rolls) and v3 (masked on v2); zone map v1 and a masked v2. Kept: `supermini_red`,
`hatch_graphite`, `fleetsaloon_white`, `kombi_sage`, `crossover_pearl`, `estate_steel`, `midsaloon_black` (score 3,
from v3), their zone maps (4, from `cars_fleet_a_zones-v2`). Failed after three: `citycar_lime` (12 % wide, v1–v3)
and `boxsaloon_beige` (8 % long and wide in v2 and v3, fitted in v1).

- **Worked at once:** every body is plainly its class, at real dimensions — the Eastern European city mix, a boxy
  1980s saloon, the leisure van with its sliding-door rails. "Trace each outline like paint on tracing paper" and the
  width fault in the cell line fitted the kombi and the crossover in v2 (11 % and 10 % wide in v1).
- **A rear cluster is drawn as one red lamp, whatever the brief says.** v1 framed three sections (brake, indicator,
  reversing) and named their glass; v2 spelt the colours out in order and named the fault; v3 masked the tails alone
  and asked for nothing else. All three drew all-red clusters, v3 with an orange tint at most. The zone map, asked
  with the layout as image 2 ("where the first image draws a cluster all in red, still divide that lamp into these
  three zones"), divided every cluster red, yellow, green on v1. Do not ask the art for it again.
- **A front indicator is drawn at the nose corner, outboard of the headlamp**, not behind it: framed 0.19 m back in
  v1, nothing came there. The frames now stand where it is drawn (`qq fleet`'s `FRONT`).
- **A whole-cell masked redo came back near pixel-identical** (cells 1 and 3 of v3, the fit unchanged to the pixel):
  a mask does not make the model redraw a body it already drew. A size fault wants a new frame.
- **A zone map skips a front indicator at one corner**: all seven painted it at the bottom corner and not the top in
  v1; a masked redo of the noses (`qq sprites mask … id:nose`) with the car sheet as image 2 and the fault named
  fixed all seven.
- Mirrors still come back about twice the size asked ("no longer than the headlamp" in v2); clipped at the footprint.

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
