# Decision log — the debug layers

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-27 — the switches are shown a section a question, and the words moved off the town

**The debug page was thirteen boxes in a list, and the figures and the ground were pages of their own.** A
session looking at one thing had every switch in front of it, the probe's switch on one page and its sliders
on another, and nothing said what a layer draws or what its colours mean. **So the page is cut into sections
by what a session is opened to look at** (OBS-2y), each carrying its switches with their keys and the
figures that question turns; the sections are the slice's (`DebugLayers`), because which switches belong
together is a fact about the layers and not about the panel.

**The words over every body went, and the inspector took them** (OBS-2t). A label over each car at a close
framing was the densest thing on the glass and the reason a layer read as noise: the reading a reader wants
is about one body, and it is the body they are pointing at. The layers are lines; a card beside the pointer
says what the thing under it is, and a click pins one. **No layer takes the mouse any more** — the geometry
grid did, and a reader who ticked it could not pick a car — because a pin is a reading and not a mode.

**A route is drawn on a dark casing and held to a width on the glass**: a body's colour stands off grass and
tarmac, and a thin line of it over paint, a zebra or a roof did not.

## 2026-09-27 — a secondary claim is drawn as an outline

A secondary claim is ground a holder only crosses ([world/road](../../../world/road/docs/claims.md),
TER-5c.1). Two of them may lie over one stretch, and one butts against the main claim it was weighed
against. Washed like a main claim, two of them read as one stretch held twice, and one beside a main claim
read as the next piece of the same car's road. **So it is the block's outline and no wash**: both long
edges and the end bars, at the edge strength of its rung, so colour still says whose and alpha still says how
strong.

## 2026-09-19 — the solver's grid is drawn as the cells it holds, not as a ruling

The question that asked for the layer was whether the broad phase bins bodies the way the road bins lines,
and it does not and may not: the two indexes are laid over different things from different corners, and
the lanes' own claims are intervals along a way rather than cells at all. **So the layer draws both of the
solver's grids, in a hue each** (OBS-2x), and the two rulings standing offset from one another is half of
what it says.

**Only live cells, because that is what the index holds.** Neither grid clears anything between rebuilds —
a cell is live while its stamp matches — so a full ruling like the geometry grid's (OBS-2r) would be a
lattice invented for the drawing. It is also what makes the moving half affordable: a few hundred bodies
light a few hundred cells, where a ruling is every cell in view.

**And it is read without `EnsureIndex`**, the call every query makes first, which would have made a layer
switched on change the figures on the panel beside it.

## 2026-09-18 — the shape the reader strikes is a layer of its own, not a figure on the perimeter's

Nothing could be asked of the construction behind the boundary and its layers: what the outset does at a
distance the town does not use was visible only where a shipped figure happened to land on it. A dial on
the shipped distances was the obvious way in and is **not** what was built — those lines are the ground the
town stands on, and a layer drawing them somewhere else lies about the picture underneath it (OBS-2u).

**So the probe is a switch and figures of its own** (OBS-2w). **They are the sliders the town is not stood
up for**: a trim rebuilds the fleet, these stale only the overlay's own cache, so the menu's sliders are of
two kinds and where they differ is one place each. **It steps**, a
tenth of a metre, because an outset is cut against the whole town's shape and a track read off the pointer
would strike one a pixel. **The rounding is a second row and not a setting**: the fold the offset cut and
the corner the rounding left look alike in a frame, and only holding one figure still while turning the
other tells them apart. **And it can be photographed** (`--ui shell-<metres>`), as `--ui hide-<layer>`
takes a layer out: a figure that exists only under a hand on a slider cannot be put in front of anybody.

The perimeter's own second line at a fixed distance went on 2026-09-13 with the `Extrusion` it was struck
from, and was never recorded; the probe is where a distance off the boundary is read now.
Retired: `OBS-2q`.

## 2026-09-16 — a line is culled on the box it fits in, and never on the circle over its ends

**The layer drew a hole the town has not got**: a wedge between two roads on Odesa showed pavement and no
walking lane, and the network had 296 m of one down one side of the wedge, round the apex and back. The
cull weighed a chain as the circle on the chord between its own two ends, and a lane that comes back to
where it set off has its ends beside each other with a hundred and fifty metres of line between them.

It is now `ChainIndex.Box` over the chain's pieces, the house's one answer to what holds a piece — the
fault was that there was a second way. **The correct cull is also the cheaper one**: the same three
framings of Odesa came to 4 595 → 3 621, 32 552 → 22 484 and 40 943 → 40 685 quads. **A cull may cost a quad
and may never cost a line**: a frame that omits a lane is a frame somebody goes looking for a defect behind.

## 2026-09-16 — the ground's layers are held apart from the overlay's switches, and they start on

They are switches this slice owns (OBS-2v), and not among the overlay's: **every one of those is a layer
drawn over the town and starts off** (OBS-2b), every one of these is the town itself and starts on. Held in
`GroundSwitches` beside `DebugSwitches`, and drawn under a rule of their own in the ground's section, so a
row of boxes never means opposite things in its two halves.

**The generation is one number** — `DebugSwitches.Generation` carries the ground's — because the wireframe
has to be laid again when a layer is hidden exactly as when a switch is thrown, and a reader comparing two
numbers is a reader that forgets one. **The figures beside them are the mesh's own**: `GroundMesh` tallies
each layer as it lays it, and the menu and `--bench census` read those tallies rather than timing anything.

## 2026-09-14 — a boundary is culled by the stretch, because a ring is the whole town

**One outline was filling the cache at every framing, and it read as the layers after it being broken.** A
chain was drawn if its two ends stood near the view, allowed half its own length of reach; a ring's two ends
stand at one place and its length is a town, so every ring passed at every zoom, and a city's outer ring is
a hundred thousand stretches — twice what the cache holds (`TownQuadCapacity`). **Culled by the stretch it is
the dozen on the glass**: the same framing went from a saturated cache to about a thousand quads. A chain at
a time is right for a lane, whose length is a street; anything that draws a ring wants the stretch.

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
