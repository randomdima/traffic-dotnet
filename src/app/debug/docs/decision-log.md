# Decision log — the debug layers

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

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
