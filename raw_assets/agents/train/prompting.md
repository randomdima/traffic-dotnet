# Prompting trains

What the train briefs taught, set by set. Read before writing the next one; the brief itself is in each frame's record
(`qq sprites trace <id> --full`).

**A train's sheet is asked in rows, two or three vehicles to a row**, so twelve fill a 3:2 frame near the ground's grid
(31.5 px/m): a 3×3 grid draws a 25.5 m coach at 19 px/m.

## trains_a (2026-10-18) — twelve vehicles, three attempts, four kept, $0.10

Kept from v2: `diesel_green-v1`, `diesel_blue-v1`, `coal_rust-v1`, `coal_olive-v1` (4 each). Not kept: the three coaches,
the two electrics, `coal_gondola`, `tank_black`, `tank_grey`.

- **Worked:** the look at once — liveries, roof gear, pantographs, fans, coal heaps, a dome and platform on each tank — and
  every vehicle's length to within a few per cent.
- **Failed, every attempt: the width.** A long thin outline is drawn 11 to 16 % too wide, the side walls showing under
  the roof (v1: 110 px for 98; v2, naming the fault in the brief: 114; v3, a masked redo naming each cell's measured
  width: 108). Past `cut`'s 8 % it is not fitted. The diesels, at 3.4 m the widest outlines, and the gondolas fitted.
- **Spilled:** buffers and a tank's ladder past the outline's ends; v2's brief kept the ladders in.
- **A masked redo left one cell as it was** (`tank_grey`, v3).
- **Next:** ask the narrow vehicles a little wider than their footprint and let `cut` fit them down, or a sheet of the
  narrow ones alone with nothing wider beside them to measure against.

## trains_b (2026-10-19) — twelve vehicles, three attempts, eleven kept, $0.10

Used: `electric_blue-v1`, `electric_red-v1`, `coach_green-v1`, `coach_red-v1`, `coach_blue-v1`, `diesel_green-v2`,
`diesel_blue-v2` (v2, 4 each); `coal_rust-v2`, `coal_olive-v2` (v3, 4); `tank_black-v1`, `tank_grey-v1` (v1, 3). Not
kept: `coal_gondola`.

- **Widening the outline does not stop the overshoot: the model scales with it.** At 3.4 m the coaches came back 119 to
  121 px for 107 — the same 12 % as at 3.1 m. Fitted down under the owner's allowance (`FIT_ROWS_MAX`), not asked again.
- **The overshoot is a tilt, and naming it as one helped** (v2): "seen a little from the front … the top edge and the
  bottom edge look exactly alike … flat light from directly above" made the rims symmetric, where v1 drew a strip of side
  wall along every bottom edge. The width stayed.
- **"One driving cab at the right end only … the left end a plain flat hood end, no cab" was drawn as asked** (`diesel_*`,
  v1 and v2): a one-way locomotive is had from the brief alone.
- **Gondolas keep tilting** (`coal_*`, v1 and v2) until a masked redo says "all four walls are the same thin rim seen from
  above, the top rim and the bottom rim exactly alike" (v3). **The same redo drew a tank in a gondola's cell and left a
  tank's empty** (cells 8 and 9): a mask over two unlike neighbours swaps them.
- **A row three to a sheet's width leaves no slack**: the model shifted row 4 sideways and a neighbour's coupler landed in
  the next cell; `cut` now fits only the body joined to the outline's middle, and clears key-coloured pockets a wagon's
  frame encloses.
