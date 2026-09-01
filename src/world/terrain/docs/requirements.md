# Terrain — requirements

The town's surface: what kinds of it there are, who may be on each, and the geometric rules that make a
pavement look like a pavement. Where each ID lives is [docs/index.md](../../../../docs/index.md); the
figures are on `SimConfig`.

Roads, junctions and what is painted on them are [world/road](../../road/docs/requirements.md).

## The catalogue

**TER-1** The city is fully covered by terrain: no empty space and no holes.

**TER-2** Every terrain type declares which agents may traverse it under soft rules, its effect on
movement (grip, drag, mark threshold) and whether it is directional. **The movement effect applies to
every body occupying it whether or not it is permitted there** — legality is a soft-rule matter only.

**TER-2a** Rules address terrain by **set** — drivable, walkable, preferred, permitted-to-nobody — and
never by type name. A rule written against `Sidewalk` breaks the day the town gains a boardwalk; one
written against *walkable* does not.

**TER-3** The catalogue is data. It must distinguish at minimum: a default pedestrian ground; a
directional carriageway of two opposing lanes; the ground roads share where they meet; a pedestrian-legal
way across a carriageway; ground a car idles on that a pedestrian may stand on; paved pedestrian-only
ground; and ground permitted to nobody. Two types differing only in what they draw are still two types,
but no rule may turn on that difference alone.

**TER-3a** Ground legal to nobody is terrain and not a hole: coverage still holds, and a body pushed onto
it is on ground and can leave under its own power. What makes it impassable is only that no route is ever
planned across it.

**PHY-8** Terrain is not a collider. It modulates the movement of the body occupying it and never blocks
movement outright; what makes ground impassable is permission.

## Two views of one geometry

**TER-7** The ground drawn and the ground classified are the same ground: both are read off the plan's
shapes, and the cell grid is a **classifier** agreeing with them to within half a cell. Neither is
derived from the other by re-tracing.

The consequence is the one that matters: **a marking always sits on the surface it belongs to**, because
paint and cells are two views of one geometry. Where they disagree the geometry is wrong, not the
picture. No arrangement of metre squares is a kerb running at 40°.

**A stamp returns where it actually landed and the caller carries that** rather than recomputing it. A
fill that gives a cell to a rectangle when the cell's centre is inside can stand half a cell proud on one
side and half a cell short on the other. Snap what is stamped, return the snapped shape, and carry the
snapped shape in the plan.

## The pavement

**TER-3c** A town is laid with a **pavement**: a band of preferred walkable ground running the whole
length of every carriageway on both sides, touching the kerb, ringing every junction and wrapping every
lot. **It is stamped by what it is not** — it takes only ground nothing else has claimed, so the
carriageway, the crossings, the corner flares and the bridge decks keep their own cells and the band
falls out as the two strips either side, without anything having to know where a kerb is.

**TER-3c.1** The network a walking route is planned over *is* the pavement, its corners and its
crossings; this is structure, not price. A bounded hop off the network to a nearby door is still allowed,
and a road is still crossed only at a crossing.

**TER-3c.2** The building line stands behind it: a wall is set back from the kerb by the pavement plus
padding, so nothing is built on the walk and a doorstep opens onto it. Street planting stands on the
verge behind the walk for the same reason — a trunk in the middle of a four-metre pavement is a trunk
everyone on that street goes round.

**TER-3c.3** The pavement turns every corner on the curve of what it runs beside and stays its own width
doing it: the ground within a width of that thing, at every angle two arms can meet at. Where the thing
it wraps turns a right angle of its own, **the walk turns it on half its own width** — rounded on the
full width the band reads pinched, because a walker rounding a corner has further to go across it, and
square takes a bite of verge.

**TER-3c.4** It turns its inner corners too. Where two pieces of it run into one another they leave a
re-entrant spike of verge, rounded on an arc tangent to both edges at half the walk and bounded by how far
the fillet would reach in. **A corner is a fact about the pair of shapes and nothing else**, so it is
solved against the finished ground rather than enumerated per kind of neighbour or per map — a pair the
generator has never put together before is rounded the first time it appears, and a map recording no
corners of its own is rounded exactly as one that does. **The build solves them**, from the pieces it
lays the pavement out of, and reads no list of them from anywhere.

## Water and bridges

**TER-3b** A carriageway crossing ground legal to nobody carries a **bridge**: a deck wider than the
carriageway, its exposed edges walkable, **running the whole road rather than only the wet part**, so
what it carries reaches standable ground at both ends.

**TER-3b.1** The deck carries the town's pavement **at the pavement's own width** and stands clear of it
on both sides by a margin. A deck sized to a walk of its own is a deck the street's pavement does not fit
on. The margin is what a parapet stands on and what tells a deck from a road that happens to be over
water; the pavement width is on the deck's own plan record, because the deck is laid first.

## The edge line

The outside of the pavement and of a bridge deck each carry a line, the way the carriageway carries a
kerb line. **An edge is the surface drawn darker; paint is the surface drawn brighter**, and the grain of
the ground comes through both. Drawn as every piece of pavement twice — once at full size in the edge
shade, once a line's width smaller in the surface shade over it — so what survives is a rim on the
union's own outer boundary and nowhere two pieces meet. Nothing walks an edge or probes a region.

**TER-3d** **The kerb line stands on the kerb and not in the lane.** It is struck a line's width
*outside* the carriageway and the road drawn back over it at its full width, so the asphalt from the
kerb line to the centreline is the lane the town is laid at (GEN-15) and the paint stands on the
innermost strip of the walk. Struck inside the carriageway — the way an edge shade is struck inside the
surface it rims — the line takes its own width off the lane it marks, and every lane measured off a
picture comes out short of the figure the rest of the build quotes, on the bends as on the straights.

## What this slice must produce

- A query `terrain at (x, y)` → type, permissions, grip, drag, lane direction where directional.
  Continuous position in, no snapping out.
- A drawn surface matching that query to within half a cell everywhere
  ([app/render](../../../app/render/docs/requirements.md) owns the drawing).
- Pavement bands of constant width along every straight and correct round every corner, inner and outer.
