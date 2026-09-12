# Terrain — requirements

The town's surface: what kinds of it there are, who may be on each, and the geometric rules that make a
pavement look like a pavement. Where each ID lives is [docs/index.md](../../../../docs/index.md); what the
rung after each ID means is [docs/priority.md](../../../../docs/priority.md); the figures are on
`SimConfig`.

Roads, junctions and what is painted on them are [world/road](../../road/docs/requirements.md).

## The catalogue

**TER-1** `P3` The city is fully covered by terrain: no empty space and no holes.

**TER-2** `P3` Every terrain type declares which agents may traverse it under soft rules and its effect on
movement (grip, drag, mark threshold). **The movement effect applies to every body occupying it whether or
not it is permitted there** — legality is a soft-rule matter only.

**TER-2a** `P4` Rules address terrain by **set** — drivable, walkable, preferred, permitted-to-nobody — and
never by type name. A rule written against `Sidewalk` breaks the day the town gains a boardwalk; one
written against *walkable* does not.

Two slices name a kind and no third may: the **plan**, which lays the ground and solves what is on it, and
**this one**, which says what each kind permits. That split is why a town half-laid can be asked where a
thing may stand without the plan learning what an agent is.

**TER-3** `P4` The catalogue is data. It must distinguish at minimum: a default pedestrian ground; a
carriageway; the ground roads share where they meet; a pedestrian-legal way across a carriageway; ground a
car idles on that a pedestrian may stand on; paved pedestrian-only ground; and ground permitted to nobody.
Two types differing only in what they draw are still two types, but no rule may turn on that difference
alone.

**TER-3a** `P3` Ground legal to nobody is terrain and not a hole: coverage still holds, and a body pushed onto
it is on ground and can leave under its own power. What makes it impassable is only that no route is ever
planned across it.

**PHY-8** `P3` Terrain is not a collider. It modulates the movement of the body occupying it and never blocks
movement outright; what makes ground impassable is permission.

## One geometry

**TER-7** `P3` The ground drawn and the ground answered for are **one geometry**: the plan's shapes. What the
ground is at a point is *solved* against those same shapes, in the reverse of the order they are drawn in,
so the answer is the last piece drawn over that point — exactly, and at every angle. There is no second
representation, so there is nothing for the answer to disagree with and no tolerance anywhere.

The consequence is the one that matters: **a marking always sits on the surface it belongs to**, because
there is only one surface. Where the answer looks wrong the geometry is wrong, not the picture. A kerb
running at 40° is a kerb running at 40°.

**A shape the ground has is a shape the plan carries.** Anything laid into the town has to be in the plan
as its own record, or it is ground that is drawn from nothing and answered for by nothing — and the two
readers of that record, the picture and the query, take it off the one array.

**A raster is scratch and never an answer.** The generator paints one while a town is being decided, so a
stage can ask what has been laid where; it agrees with the shapes only to within a cell, it is never
shipped, and no rule about a finished town may be argued from it.

**TER-7a** `P4` **A band of ground ends where its own line ends**, square across, and not in a half-disc of its
own half-width past the last point. It is the difference between a shape and the arithmetic that is
cheapest to measure it by, and the two readers of a band had settled it differently: the answer at a point
said square and the tarmac's own outline said round. What the round end put into the town was tarmac
nothing is drawn on and nothing drives over — half a lane of it past the point a movement starts at, which
is inside the box and therefore invisible, until it reached out under the pavement corner beside the mouth
and took the corner's own line away with it.

**TER-7b** `P0` **The ground is a stack of layers, and a layer is one region of the town's own boundary.**
The town is drawn bottom to top — the grass, then the pavement and its rim, then the water and the decks,
then the kerb line and the driven ground at its own size, then what the blocks take back, then the paint —
and **each of those layers is the ground within one distance of the kerb**, filled as the shape it is. The
boundary is computed once (`LaneShell`) and every layer is it moved by a figure (`GroundRings`), so a layer
has an edge that is a line rather than whatever a heap of overlapping pieces happened to leave.

Three consequences follow and all three are the point.

- **The picture and the answer are one distance.** What the ground is at a point is which line of the town
  lays it and how far off the kerb it stands, compared against the same table the drawing is laid from
  (TER-7) — so a distance added to one is added to the other, and the question of whether the two agree
  cannot be asked. This is the whole of what the rule buys.
- **A rim, a kerb line and an edge line are what a layer leaves of the one under it**, never a shape of
  their own: two layers a line's width apart, the outer in the line's shade and the inner in the surface's
  own, and what survives is a stroke on the region's boundary. A line therefore has no ends to close and no
  corners to turn — the boundary turned them. **Which of the two distances is the surface's own is the
  line's to say** — an edge shade is struck inside what it rims and a kerb line outside (TER-3d).
- **A block is a hole and is filled after every region, in the other order.** Outside the town the
  distances nest inwards; inside a block, a ring nearer the kerb leaves *more* of the block beyond it, so
  the same order paves a block kerb to kerb. What a block shows is the sequence read outwards from its own
  kerb.

**What this rule does not license is drawing the same thing twice in one layer to hide a seam.** Two bands
that abut are two offsets of one curve; laid as two shapes each is sampled to its own curvature and they
stand a chord's sag apart. A layer here has no such seams because it has no bands — it is one region, and a
seam inside it would be a seam in its own boundary.

## The pavement

**TER-3c** `P6` A town is laid with a **pavement**: a band of preferred walkable ground running the whole
length of every carriageway on both sides, touching the kerb, turning the corner of every junction and
wrapping every lot. **It is stamped by what it is not** — it takes only ground nothing else has claimed, so the
carriageway, the crossings, the corner flares and the bridge decks keep their own cells and the band
falls out as the two strips either side, without anything having to know where a kerb is.

**It is laid once, as a step of its own, and everything reads that one laying.** What the pavement is
made of is a list of pieces the town is laid with (`Paving`); the picture draws that list and the answer
to what the ground is at a point is given off the same one. Worked out a second time by whoever needed
it, the two are a figure in two places — and a band widened in the picture and not in the answer is a
walker refused ground it can see it is standing on.

**TER-3c.1** `P4` The network a walking route is planned over *is* the pavement, its corners and its
crossings; this is structure, not price. A bounded hop off the network to a nearby door is still allowed,
and a road is still crossed only at a crossing.

**TER-3c.2** `P6` The building line stands behind it: a wall is set back from the kerb by the pavement plus
padding, so nothing is built on the walk and a doorstep opens onto it. Street planting stands on the
verge behind the walk for the same reason — a trunk in the middle of a four-metre pavement is a trunk
everyone on that street goes round.

**TER-3c.3** `P4` **The pavement is the ground between the kerb and a walk beyond it**, at every angle two
arms can meet at. The kerb is the boundary of the driven ground said as closed rings (`LaneShell`) and
every line the town has is that boundary moved by a figure (`GroundRings`) — the kerb at nought, the kerb
line at a line's width, the lane a walker follows at half a walk, the pavement's outer edge at a walk. So
**every one of them is an offset of one curve**, the band between any two is exactly the difference between
the distances that struck them, and a walker walks down the middle of it because the middle is where the
half-walk line is. Where the boundary turns a corner it turns it once, on the ring itself (TER-5), and
every distance inherits that corner at its own radius. Nothing is smoothed, patched or rounded on top of
it and nothing is measured twice — the concrete, the kerb line, the lane and the answer are one
construction read at four figures.

**TER-3c.7** `P6` **The carriageway ends where the pavement starts.** Everything inside the kerb is tarmac —
carriageway, junction and car park, and the pockets the town's own pieces leave between them: a movement
narrower than the arm it leaves, a car park set back off the street it fronts, a street meeting a wider
street. Such a pocket is not a bay of concrete. Drawn as the tarmac's own outline instead, the kerb stepped
and chamfered its way round every mouth in the town while the shell against the grass and the lane between
them ran smoothly past, and the band came out a different width at each of them.

**TER-3c.6** `P6` **A pavement is a ring and has no ends.** The boundary of the driven ground closes on
itself — one ring round the outside of the town and one round every block it encloses — so the lane laid
half a walk off it closes too, and the questions a heap of separate lines had to answer do not arise:
which loose end meets which, which line leads somewhere, which pavement is another one said twice. **The
one thing that cuts a ring is the ground's own veto**: a lane over water or off the map is not a lane
however far it stands from the kerb, and that is the only place a pavement stops.

**TER-3c.8** `P0` **The walk wraps the tarmac as one shape, and never a piece of it.** What the pavement is,
is a distance off the outline of the **union** of the driven ground, so a place on it belongs to exactly one
line by construction rather than by a tie broken between candidates. Two coincident lines are not two
pavements — a car park whose bays' ways converge on one pose would offer six down one metre of kerb, a
movement running edge to edge with the arm it leaves two — and a boundary computed once has none of them to
break: it is one line because it was never several.

## Water and bridges

**TER-3b** `P6` A carriageway crossing ground legal to nobody carries a **bridge**: a deck wider than the
carriageway, its exposed edges walkable, **running the whole road rather than only the wet part**, so
what it carries reaches standable ground at both ends.

**TER-3b.1** `P6` A deck is **wide enough for the town's pavement to cross it** and **carries none of its
own**: it is the deck out to its own half-width, and the margin either side of the carriageway is what a
parapet stands on. A deck sized to a walk of its own is a deck the street's pavement does not fit on, which
is what the width on its plan record is for — but a deck that lays that walk itself is a line beside a road
struck by arithmetic of its own, which is the one thing no line beside a road may be (TER-3c.3). The walk
across a deck is the boundary's to strike, at the distance every other metre of pavement is struck at.

## The edge line

The outside of the pavement and of a bridge deck each carry a line, the way the carriageway carries a
kerb line. **An edge is the surface drawn darker; paint is the surface drawn brighter**, and the grain of
the ground comes through both. It is what its layer leaves of the one under it (TER-7b) — the layer laid
at full size in the line's shade and again a line's width smaller in the surface's own — and never a shape
of its own. Nothing walks an edge or probes a region.

**TER-3d** `P6` **The kerb line stands on the kerb and not in the lane.** It is what the carriageway leaves
of the stroke struck a line's width *outside* it (TER-7b) — so the asphalt from the kerb line to the
centreline is the lane the town is laid at (GEN-15). Struck inside the carriageway — the way an edge shade
is struck inside the surface it rims — the line takes its own width off the lane it marks, and every lane
measured off a picture comes out short of the figure the rest of the build quotes, on the bends as on the
straights.

**It stands on the driven ground's own boundary, and so does the pavement's inner edge**, because they are
one boundary read at two figures (TER-3c.3), so wherever the boundary steps — one band narrower than the
one it meets, a movement leaving an arm, a car park set back off its street —
the concrete beside it steps with it, a walk out and parallel. Struck on a curve of its own instead, a
kerb line reads as a chamfer cut across a corner the pavement beside it turns smoothly.

## What this slice must produce

- A query `ground at (x, y)` → type, permissions, grip, drag. Continuous position in, no snapping out, and
  a point off the town's own box answered rather than refused.
- The same answer the surface is drawn from, everywhere and exactly
  ([app/render](../../../app/render/docs/requirements.md) owns the drawing).
- Pavement bands of constant width along every straight and correct round every corner, inner and outer.
