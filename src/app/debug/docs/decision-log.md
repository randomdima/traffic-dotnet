# Decision log — the debug layers

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-19 — the solver's grid is drawn as the cells it holds, not as a ruling

The question that asked for the layer was whether the broad phase bins bodies the way the road bins lines,
and the answer is that it does not and may not: the two indexes are laid over different things from
different corners, and the lanes' own claims are intervals along a way rather than cells at all. **So the
layer draws both of the solver's grids, in a hue each** (OBS-2x), and the picture of the two rulings sitting
offset from one another is half of what it says.

**Only live cells, which fell out of the index rather than out of the picture.** Neither grid clears
anything between rebuilds — a cell is live while its stamp matches — so an empty cell is not a cell that
exists, and the full ruling the geometry grid draws (OBS-2r) would here be a lattice invented for the
drawing. It is also what makes the moving half affordable: a few hundred bodies light a few hundred cells,
where a ruling over the same frame is every cell in view.

**And it is read without being reindexed.** The obvious call before drawing is the one every query makes,
`EnsureIndex`; it retakes every bound and rewrites the integrated-body count the status panel is reading,
which would have made a switched-on layer change the figures beside it. A lattice one step old is what the
step that has been taken was priced on, so that is what is drawn.

## 2026-09-18 — the shape the reader strikes is a layer of its own, not a figure on the perimeter's

The perimeter layer drew the boundary and the layers the picture is laid from, and nothing could be asked
of the construction behind them: what the outset does at a distance the town does not use — the corner it
swallows, the two kerbs it brings back as one line, the ring it loses — was visible only where a shipped
figure happened to land on it. A dial on the shipped distances was the obvious way in and is **not** what
was built: those lines are the ground the town is standing on, and a layer that draws them somewhere other
than where the town has them is a layer that lies about the picture underneath it (OBS-2u).

**So the probe is a switch and a figure of its own** (OBS-2w), in a colour neither the boundary's nor a
shipped layer's, and the figure sits on the panel where the figures already are. Two things fall out of
that and both are why it is worth a row rather than a test:

- **It is the one figure on that page the town is not stood up for.** A trim is read where a car is built
  and where the ground is catalogued, so moving one rebuilds the fleet; this moves a line the overlay
  draws, so what it stales is the overlay's own cache and nothing else. The figures page therefore holds
  two kinds of row, and where they differ is one place each — where a value stands on its track and what
  the pointer asks for — because the row that is drawn and the figure that moves coming apart is the
  mistake this panel has already made once.
- **It steps.** An outset is cut against the whole town's shape, tens of milliseconds on a city, and a
  track read straight off the pointer strikes one shell a pixel. A tenth of a metre is finer than anything
  being looked at and turns a sweep of the track into a few dozen answers.

**The rounding is a second row and not a setting.** The move and the smoothing leave notches that look alike
in a frame — one is the fold the offset cut and the other is what the rounding did not take off — and the
only way to tell them apart is to hold one figure still and turn the other. Struck at a fixed rounding, the
layer would answer the question that is easy to ask and not the one that is hard.

**And it can be photographed**: `--ui shell-<metres>` turns it on at a distance, as `--ui hide-<layer>`
takes a layer out. A figure that exists only under a hand on a slider cannot be put in front of anybody.

## 2026-09-16 — a line is culled on the box it fits in, and never on the circle over its ends

**The layer drew a hole the town has not got.** A frame of the wedge between two roads on Odesa showed
pavement on it and no walking lane at all, and the network had one all along — 296 m of stretch running
down one side of the wedge, round the apex and back up the other. What rejected it was the cull: a chain
was weighed as the circle on the chord between its own two ends, and **a chain is not inside that circle**.
A lane that comes back to where it set off has its two ends beside each other with a hundred and fifty
metres of line between them, so the circle is a few metres across and sits nowhere near the part of the
line on screen.

It is now `ChainIndex.Box` over the chain's pieces, which is the house's one answer to what holds a piece —
a second way of boxing one would be a second answer, and the reason this went wrong is that there was a
second way. **The correct cull is also the cheaper one**: the circle admitted every chain whose chord
happened to pass near while rejecting the ones that bulge, so the same three framings of Odesa came to
4 595 → 3 621, 32 552 → 22 484 and 40 943 → 40 685 quads.

**A cull may cost a quad and may never cost a line.** A picture of a network is read as the network, and a
frame that omits a lane is a frame somebody goes and looks for a defect behind — this one cost an
afternoon of chasing a pavement that was never missing.

## 2026-09-16 — the ground's layers are a page of their own, and they start on

They are switches this slice owns (OBS-2v), and they are not on the debug page. **Every row there is a layer
drawn over the town and starts off** (OBS-2b); every row here is the town itself and starts on, and a page
whose boxes meant opposite things in its top half and its bottom half is a page that has to be read twice.
Held in `GroundSwitches` beside `DebugSwitches` rather than as nine more fields on it, for the same reason.

**The generation is one number and this one is folded into it.** The wireframe is a picture of what is being
drawn, so a layer switched off has to lay that cache again exactly as a switch does — `DebugSwitches.Generation`
is now its own count plus the ground's, which leaves every reader of it asking one question. Compared side by
side instead, the layer that forgot the second comparison would have drawn a net over ground that is not
there and looked like a triangulation fault.

**The figures on the page are the mesh's and are not taken here.** `GroundMesh` writes down what each layer
came to as it lays it, and the page, `--bench census` and this slice all read that one set of tallies. A
panel that timed the ground itself would have been a second answer that disagrees with the first the week a
layer moved.

## 2026-09-14 — a boundary is culled by the stretch, because a ring is the whole town

**One outline was filling the cache at every framing, and it read as the layers after it being broken.**
The perimeter layer culled a chain at a time: a chain was drawn if its own two ends stood near the view,
allowed half its own length of reach. A ring's two ends stand at the same place and its length is a town,
so the test passed every ring at every zoom — and a city's outer ring is a hundred thousand stretches, which
is twice the quads the town cache holds (`TownQuadCapacity`). Everything after the first outline got
nothing. The symptom was the second outline drawing its line and not its normals, which is what the last
few hundred quads buy.

**Culled by the stretch, it is the dozen on the glass.** The same framing went from a saturated cache to
about a thousand quads, and the three outlines the layer draws are all drawn. The test is the stretch's own
start and its own length, which bounds it without the arc being walked — a point of a piece is never further
along it than the piece is long.

**The lesson is about the cull and not about the budget.** A chain-at-a-time cull is right for a lane, whose
length is a street; it is meaningless for anything closed, whose reach is its own diameter however small the
part of it on the glass. Anything that draws a ring wants the stretch.

## 2026-09-04 — the wash on a block is the strength of the claim

One wash for every stretch kept the pieces of one hold reading alike and cost the layer its whole point at
a junction: half a dozen asks over each other with nothing saying which holder would give way. The colour
now says whose and the wash says how strong, taken off
[`ClaimPriority`](../../../world/road/ClaimPriority.cs)'s own rung number so a level added between two
costs nothing.

## 2026-09-03 — the nodes layer draws every lane whole, and every mitre the town lays

Drawing a lane only between the stations a route uses hid ground either network can still claim, and a
block with no line under it cannot be checked against anything. The layer now draws both networks whole
with no filter, which is affordable only because the producers were fixed to match — turning round lays no
mitre, and a lane is cut to the ground it is walked over
([world/road](../../../world/road/docs/decision-log.md)).
