# Terrain — decision log

## 2026-09-27 — a person is not affected by terrain at all

The owner ruled it: **walkers are not affected by terrain; it matters to tyres alone.** A walker's pace and
grip are its own on every surface, a casualty slides on its own sliding grip, and a person leaving a building
is set down on the nearest clear spot whatever the ground under it — so `TER-2`, `PER-3`, `PHY-8` and `PHY-7a`
say so, and `TER-7b`'s first consequence names a wheel alone (at the owner's word). No walker asks the ground
any more: every walker tick, every paused walker and every casualty took one ask each, and a blocked doorway
took up to thirty-three a tick.

## 2026-09-27 — the ground is answered off the shell, and a lane is the actors' alone

The owner ruled it: **a lane is for the actors' logical behaviour and never for physics, and the ground is
the shell.** What a wheel stands on is which side it is of the rings the picture fills
(`GroundRings.Carriageway` and `Walk`), asked through a lattice over their pieces (`RingSides`,
`Terrain.ShellCellsAcrossGridCell`): a cell no ring crosses is a lookup of the winding at its corner, and a cell one does
cross walks two legs from that corner to the point. The answer is the rings' own, so the corner where the
square bands and the rounded fill parted, and the wedge a junction's corner is paved back over, are no longer
a gap — the answer is the picture. `TER-7b`'s first consequence was reworded at the owner's word, from which
line lays a point and how far off the kerb it stands to which side it is of the layers' rings.

The bands made a wheel pay for every lane and movement within a walk of it. Asked per kind on Odesa
(`--bench census`), carriageway went 549 ns → 32, pavement 372 → 74 and grass 62 → 37; on a running Odesa the
tyres went 1.21 → 0.29 ms a frame and the main thread 3.75 → 2.8 ms (`qq prof`). At 4 m a layer is 2.4 MB with a fifth of
its cells crossed; at 2 m it was 9.9 MB and no faster, an ask being bound by the fetch and not by the pieces.
The ways into a bay went with the lanes — a set empty in every town. The generator asks the same ground where
a prop may stand, so the corners the rounded walk gives back to the grass are furnished: Odesa lays 68 855
props where it laid 68 772, and nothing else it lays moved.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- `TER-3c`: the band is the driven ground's boundary moved by `WalkOuterM` with the carriageway laid over
  it (`GroundRings`), not ground stamped by what nothing else claimed.
- `TER-7b` (`P0`, reworded at the owner's word): only the pavement and the driven ground are struck off the
  boundary — the grass is the world, the water the plan's own shore, a deck a ribbon, a slab a rectangle —
  and the order names the slabs where it named "what the blocks take back". A block is a hole read off its
  ring's winding and takes no pass of its own (`GroundMesh.Build`, `ShellFill`).

## 2026-09-19 — the cost of a ground answer is a figure per kind, and a road that answers nothing is not asked

One figure for what a question costs hid the only thing worth knowing about it. Asked per kind
(`--bench census`), the town answers **grass in 60 ns and carriageway in 555** — nine times as dear, and five
times the mean every judgement had been made against, since a wheel's ask stops at the driven bands and a
walker's carries on through the water into the walk. The rows carry the figure now and the headline is their
mean. **Each kind is timed by asking again**, a strided quarter-million of it in a tight loop, because a
timestamp costs a fair share of a 130 ns ask and a kind lies in bands across a town.

**Four roads in five were in the index to answer no.** A road says two things about a point, a deck and a
stretch of paint, and a road carrying neither is a candidate projected onto for nothing — Odesa carries 510
crossings and no bridges over 2 054 roads. Indexed only where a road can answer, a carriageway ask went
689 ns → 555 and the mean 136 → 111. **And the working set is no longer zeroed**: `ChainIndex.Near` fills every
slot it returns, so initialising the frame was 6 to 15 ns of every ask, and room nothing fills now costs the
stack pointer — which is what lets the fallback that walks every driven line sit behind a cap of 256.

## 2026-09-06 — the carriageway ends at the pavement's inner edge, pockets included

The band's outer edge was a dilation, which rounds a convex corner and swallows a narrow notch, while its
inner edge was the tarmac's raw outline, which does neither — so wherever the town's pieces met at different
widths the band came out a different width at every mouth, and the kerb read as a chamfer across a corner the
walk turned smoothly. Everything inside the kerb is tarmac now, pockets included (TER-3c.7).

## 2026-09-06 — a band ends square where its own line ends

Measured radially a band ended in a half-disc of its own half-width past the last point: the answer at a
point said square and the tarmac's own outline said round, and the round end took the pavement corner's own
line away at every mouth (TER-7a). It ends where its line ends, square across.

## 2026-09-03 — the cell grid is gone and the ground is solved against the shapes

A map carried its shapes *and* a one-metre classification of them, with TER-7 stating the disagreement as
half a cell — largest exactly where it mattered, on a corner where no arrangement of metre squares is a kerb
at 40° and a tyre's contact patch is 0.2 m. It fed back into traffic, since a claim goes as v²/2a and a
misclassified cell lengthened a held stretch quadratically. The ground is solved against the shapes now.
`Ground.Footway` and `LaneDirs` went — 13.8 MB on Odesa with no production consumer — and the generator keeps
no ground raster either, which is what lets a half-laid town be asked what is where. It cost 900 ms against
384 to lay Odesa and about 300 ns a question against 16–21 ns, which is 4 % of a core.
