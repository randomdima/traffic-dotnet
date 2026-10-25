# Prompting a bridge's art

What the bridge briefs taught. Read before writing the next one; the brief is in each frame's record
(`qq sprites trace walkway_granite --full`).

**A bridge's strips are laid along its span** (TER-3b.2), so unlike the ground's surfaces they may have joints and a
direction: a flag, a slab, a post or a tooth square across the strip is what `qq sprites use` cuts it at
(`at_joints`), and it is sized by its depth alone, as long as it was drawn. **A fitting has its bottom edge toward the
deck**: a lamp's arm, a pier's square-cut edge, a newel's face.

## bridge_strips (2026-10-25) — three walkways, three deck roads, three joints, quality high, one attempt, $0.048

Kept from v1: all at 4 but `joint_fingers` (3, its teeth drawn as one sine wave). Used `walkway_granite`,
`deckroad_mastic`, `joint_strip`.

- **Worked at once**: "one even colour from a distance; its grain very fine … nothing damaged" carried over from the
  surfaces' lessons, and the stated counts ("exactly 5 flags", "exactly every 4 m") came back exact.
- **Every strip came back about twice as long for its depth as asked** (walkways 5.5 : 1 against 2.5 : 1, deck roads
  2.3 : 1 against 1.2 : 1), so the flags are 2.6 m along: harmless, a strip being as long as it was drawn, but ask for
  the count of flags and not the length if their size matters.

## bridge_furniture (2026-10-25) — four parapets, three lamps, three pier noses, two newels, one attempt, $0.013

Kept from v1: all, at 4 but `parapet_balustrade` and `parapet_brick` (3: drawn a little from the side, a face showing
below the coping). Used `parapet_iron`, `lamp_lantern`, `pier_cutwater`, `newel_granite`.

- **"Seen from directly above, so a lamp is its base and the head it holds, never its pole drawn sideways"** gave
  three clean top-down lamps, each off.
- **A lamp drawn with its arm down the picture is taller than wide**: its file's footprint is width then depth, and
  two were authored the wrong way round and cut at twice their size until swapped.
- **A pale balustrade on a pale walkway does not read at 10 px/m**; the dark iron rail on granite does, which is why
  it is the one used.

## bridge_rail (2026-10-25) — three rail bridge sides, three pier noses, three end posts, one attempt, $0.017

Kept from v1: all at 4 but `railparapet_concrete` (3: its lids and upstand the deck's own grey). Used
`railparapet_girder`, `railpier_concrete`, `railend_steel` (`RAIL-12`).

- **A side as two bands in one strip** — "along the bottom two thirds a walkway, along the top third the girder's
  flange" — came back as asked, the bands at about 55 : 45.
- **The style picture of the road bridge's own pieces and the track** (`parapet_iron`, `pier_cutwater`,
  `newel_granite`, `track_concrete`) held the new pieces to their palette and scale; "a little smaller rather than
  larger" landed the posts within 3 % and the piers 10 to 25 % deep, which a strip-free fitting takes as drawn.
- **Strips came back about 4.5 : 1 against 5.5 : 1**, as the road's did: harmless, a strip being as long as drawn.
