# Prompting the railway

What the railway's briefs taught, set by set. Read before writing the next one; the brief itself is in each frame's
record (`qq sprites trace <id> --full`).

**A track is a strip the ground repeats along every line** (`RAIL-3`): its template carries the rails' lines at the
gauge and, dashed, where the sleepers end; `cut` fits it by its rails rather than its extent, and `use` makes it seamless.

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
