# Terrain — decision log

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

## 2026-09-19 — the concrete is the same distance one step further out, and a junction has no kind

The pavement was drawn and not answered, so a walker stood on concrete it could see and was told it was on a
verge. **The walk is one distance off the driven bands** (`SimConfig.WalkOuterM`), TER-7b's own construction:
one query answers every layer where a query per layer walked the same index again. On Odesa 47.65 ha — 6.9 %
of the town, against 5.9 % of carriageway — answers as the concrete it is drawn as, all of it grass before, at
128 ns a question, two index walks where it was three. The promise the two constructions make each other
(TER-3c.3) was checked: every stretch of pavement lane in both towns stands on it.

**The three sets became two.** A lane and a movement through a box lay one surface (TER-5), so they answer
carriageway together and `Ground.Intersection` is gone with its catalogue row; the ways into a bay stay a set,
a bay being ground a walker may stand on. `RoadGround.Carriageway`, computed every query and read by nobody,
went too. **At a corner the picture and the answer part** by the fill's rounding at `Road.LineRoundedM`
against square bands — the rounding deviating from TER-7, not a second geometry.

## 2026-09-12 — the ground answer is a line and a distance

`At` walked a list of shapes backwards; the list is gone. **Which tarmac a point is, is the line that lays
it; where the tarmac stops is the boundary** — one construction asked two ways, the boundary being the
outline of those bands. **The lanes and never the roads**: a road's band runs between its junctions while
its lanes are cut back from them, so read off the road a sliver at every mouth was carriageway with the
pavement standing on top. What is inside the boundary and claimed by no line is the wedge a junction's
corner is paved back over. Gone with the list: the walk band, the kerb fillet as a shape, the grown road band,
and the three roundings the candidate-and-cut scheme needed.

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
