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

**TER-7b** `P0` **The ground is a stack of layers, and a layer is the union of the shapes in it.** The town
is drawn bottom to top — the grass, then every piece of the town grown by a walk, then the water and the
decks, then every piece at its own size, then the paint — and **a union is stated by drawing its pieces
over one another**. Overlap within a layer is how a union is written down rather than a fault in it: two
pieces that meet, meet by covering the same ground, and no piece is ever cut, trimmed, clipped or handed
over against its neighbour. A shape is drawn at one size in one layer, and what is beneath it is an earlier
layer.

Three consequences follow and all three are the point.

- **The picture and the answer are one list.** What the ground is at a point is that same list walked from
  the top and the first shape that covers it (TER-7), so a shape added to the drawing is added to the
  answer at the same place in the order, and the question of whether the two agree cannot be asked. This is
  the whole of what the rule buys and it is why the rule is stated as a stack rather than as a partition.
- **A rim, a kerb line and an edge line are what a layer leaves of the one under it**, never a shape of
  their own: the layer is laid at full size in the line's shade and again a line's width smaller in the
  surface's own, and what survives is a stroke on the union's outer boundary and nothing where two of its
  pieces meet. A line therefore has no ends to close, no corners to turn and no geometry of its own to
  come apart.
- **The boundary of a union is never computed.** A junction, a car park's mouth, a bridge and a dead end
  cost what a straight costs, because none of them is a shape somebody has to work out — which is what a
  partition would demand of every one of them, and the tool for it is a polygon clipper this project does
  not have.

**What this rule does not license is drawing the same thing twice in one layer to hide a seam.** Two bands
that abut are two offsets of one curve; laid as two shapes each is sampled to its own curvature and they
stand a chord's sag apart. A layer here has no such seams because it has no bands — it has whole pieces of
the town at one size, and the sag falls inside the piece under it.

Paint is the one thing above the ground rather than in it — a dash, a bar, a zebra stripe sits *on* the
surface it belongs to, which is what TER-7 asks for — and it does **not** obey the layer rule within its
own layer: **no mark overlaps another mark**, because paint is a multiplying tint and two of them over one
another read as a third.

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

**TER-3c.3** `P4` **The pavement is the ground within half a walk of the line it is walked down, and it is
nothing else**, at every angle two arms can meet at. That line is the tarmac's own outline at half a walk —
every piece offering the line that stands that far beyond it, cut to the runs no piece stands nearer to
(TER-3c.5) — so the band is a walk wide the whole way round, **both of its edges are offsets of one
curve**, and a walker walks down the middle of it. Where the thing it wraps turns a corner of its own, the
walk turns that corner on the walk itself: a right angle of tarmac on half the width to the line and half
again to the shell, a kerb fillet by reading its arc in. Nothing is smoothed, patched or rounded on top of
it, and nothing is measured twice — the concrete, the kerb line and the lane a walker follows are one
construction read at one offset.

**TER-3c.7** `P6` **The carriageway ends where the pavement starts.** Everything inside the kerb is tarmac —
carriageway, junction and car park, and the pockets the town's own pieces leave between them: a movement
narrower than the arm it leaves, a car park set back off the street it fronts, a street meeting a wider
street. Such a pocket is not a bay of concrete. Drawn as the tarmac's own outline instead, the kerb stepped
and chamfered its way round every mouth in the town while the shell against the grass and the lane between
them ran smoothly past, and the band came out a different width at each of them.

**TER-3c.5** `P6` **The pavement is the outside of the tarmac, and only the outside.** Every piece of tarmac
offers the line that stands half a walk beyond it and a metre of such a line is pavement where nothing
stands nearer than that — but a piece the rest of the tarmac encloses passes that test in the middle of a
pavement that is already laid. **The lines a car is turned through a box on are such pieces**: what a box
is walked round is the arms that meet at it, so a movement's line is pavement only **where it leads
somewhere** — joined to the rest of the walk at both of its ends, closing a gap the arms left open.
Dead-ending, it is the same pavement said twice, and what it laid was a second line up the middle of every
mouth in the town: two lanes threaded between two, a walk crossing from one side of the pavement to the
other and back at every corner, and four hundred stubs of kerb leading nowhere on one map.

**It is one rule for a shell and not a case for junctions**, and nothing here knows what a box is: a piece
says whether it is the outside of the tarmac or the inside of something, and the walk is laid off whichever
lines are left.

**TER-3c.6** `P6` **A piece offers the whole of the line that stands outside it, the ends of it included.** A
band offered its two sides and nothing across the end it stops at — so wherever the town's kerb turns a
corner that no other piece stands beside, the shell had no line there to be cut from and the pavement
simply stopped: a street meeting a wider street, a street stopping at a car park, a dead end's head. The
end of a band is square (TER-7a), so what stands the offset outside it is a quarter turn about each of its
two corners and the straight between them, and the three of them start and finish where the band's own
side lines do. **A shell has no ends**: every metre of the outside of a piece is offered, and which metres
of it are pavement is settled by the one rule and nothing else.

## Water and bridges

**TER-3b** `P6` A carriageway crossing ground legal to nobody carries a **bridge**: a deck wider than the
carriageway, its exposed edges walkable, **running the whole road rather than only the wet part**, so
what it carries reaches standable ground at both ends.

**TER-3b.1** `P6` The deck carries the town's pavement **at the pavement's own width** and stands clear of it
on both sides by a margin. A deck sized to a walk of its own is a deck the street's pavement does not fit
on. The margin is what a parapet stands on and what tells a deck from a road that happens to be over
water; the pavement width is on the deck's own plan record, because the deck is laid first.

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

**It stands on the tarmac's own outline, and so does the pavement's inner edge**, because they are one
boundary: the pavement is the tarmac grown by a walk (TER-3c.3), so wherever the outline steps — one piece
of tarmac narrower than the one it meets, a movement leaving an arm, a car park set back off its street —
the concrete beside it steps with it, a walk out and parallel. Struck on a curve of its own instead, a
kerb line reads as a chamfer cut across a corner the pavement beside it turns smoothly.

## What this slice must produce

- A query `ground at (x, y)` → type, permissions, grip, drag. Continuous position in, no snapping out, and
  a point off the town's own box answered rather than refused.
- The same answer the surface is drawn from, everywhere and exactly
  ([app/render](../../../app/render/docs/requirements.md) owns the drawing).
- Pavement bands of constant width along every straight and correct round every corner, inner and outer.
