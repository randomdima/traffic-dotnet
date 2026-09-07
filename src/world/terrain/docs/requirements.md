# Terrain — requirements

The town's surface: what kinds of it there are, who may be on each, and the geometric rules that make a
pavement look like a pavement. Where each ID lives is [docs/index.md](../../../../docs/index.md); the
figures are on `SimConfig`.

Roads, junctions and what is painted on them are [world/road](../../road/docs/requirements.md).

## The catalogue

**TER-1** The city is fully covered by terrain: no empty space and no holes.

**TER-2** Every terrain type declares which agents may traverse it under soft rules and its effect on
movement (grip, drag, mark threshold). **The movement effect applies to every body occupying it whether or
not it is permitted there** — legality is a soft-rule matter only.

**TER-2a** Rules address terrain by **set** — drivable, walkable, preferred, permitted-to-nobody — and
never by type name. A rule written against `Sidewalk` breaks the day the town gains a boardwalk; one
written against *walkable* does not.

Two slices name a kind and no third may: the **plan**, which lays the ground and solves what is on it, and
**this one**, which says what each kind permits. That split is why a town half-laid can be asked where a
thing may stand without the plan learning what an agent is.

**TER-3** The catalogue is data. It must distinguish at minimum: a default pedestrian ground; a
carriageway; the ground roads share where they meet; a pedestrian-legal way across a carriageway; ground a
car idles on that a pedestrian may stand on; paved pedestrian-only ground; and ground permitted to nobody.
Two types differing only in what they draw are still two types, but no rule may turn on that difference
alone.

**TER-3a** Ground legal to nobody is terrain and not a hole: coverage still holds, and a body pushed onto
it is on ground and can leave under its own power. What makes it impassable is only that no route is ever
planned across it.

**PHY-8** Terrain is not a collider. It modulates the movement of the body occupying it and never blocks
movement outright; what makes ground impassable is permission.

## One geometry

**TER-7** The ground drawn and the ground answered for are **one geometry**: the plan's shapes. What the
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

**TER-7a** **A band of ground ends where its own line ends**, square across, and not in a half-disc of its
own half-width past the last point. It is the difference between a shape and the arithmetic that is
cheapest to measure it by, and the two readers of a band had settled it differently: the answer at a point
said square and the tarmac's own outline said round. What the round end put into the town was tarmac
nothing is drawn on and nothing drives over — half a lane of it past the point a movement starts at, which
is inside the box and therefore invisible, until it reached out under the pavement corner beside the mouth
and took the corner's own line away with it.

## The pavement

**TER-3c** A town is laid with a **pavement**: a band of preferred walkable ground running the whole
length of every carriageway on both sides, touching the kerb, turning the corner of every junction and
wrapping every lot. **It is stamped by what it is not** — it takes only ground nothing else has claimed, so the
carriageway, the crossings, the corner flares and the bridge decks keep their own cells and the band
falls out as the two strips either side, without anything having to know where a kerb is.

**It is laid once, as a step of its own, and everything reads that one laying.** What the pavement is
made of is a list of pieces the town is laid with (`Paving`); the picture draws that list and the answer
to what the ground is at a point is given off the same one. Worked out a second time by whoever needed
it, the two are a figure in two places — and a band widened in the picture and not in the answer is a
walker refused ground it can see it is standing on.

**TER-3c.1** The network a walking route is planned over *is* the pavement, its corners and its
crossings; this is structure, not price. A bounded hop off the network to a nearby door is still allowed,
and a road is still crossed only at a crossing.

**TER-3c.2** The building line stands behind it: a wall is set back from the kerb by the pavement plus
padding, so nothing is built on the walk and a doorstep opens onto it. Street planting stands on the
verge behind the walk for the same reason — a trunk in the middle of a four-metre pavement is a trunk
everyone on that street goes round.

**TER-3c.3** **The pavement is the ground within half a walk of the line it is walked down, and it is
nothing else**, at every angle two arms can meet at. That line is the tarmac's own outline at half a walk —
every piece offering the line that stands that far beyond it, cut to the runs no piece stands nearer to
(TER-3c.5) — so the band is a walk wide the whole way round, **both of its edges are offsets of one
curve**, and a walker walks down the middle of it. Where the thing it wraps turns a corner of its own, the
walk turns that corner on the walk itself: a right angle of tarmac on half the width to the line and half
again to the shell, a kerb fillet by reading its arc in. Nothing is smoothed, patched or rounded on top of
it, and nothing is measured twice — the concrete, the kerb line and the lane a walker follows are one
construction read at one offset.

**TER-3c.7** **The carriageway ends where the pavement starts.** Everything inside the kerb is tarmac —
carriageway, junction and car park, and the pockets the town's own pieces leave between them: a movement
narrower than the arm it leaves, a car park set back off the street it fronts, a street meeting a wider
street. Such a pocket is not a bay of concrete. Drawn as the tarmac's own outline instead, the kerb stepped
and chamfered its way round every mouth in the town while the shell against the grass and the lane between
them ran smoothly past, and the band came out a different width at each of them.

**TER-3c.5** **The pavement is the outside of the tarmac, and only the outside.** Every piece of tarmac
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

**TER-3c.6** **A piece offers the whole of the line that stands outside it, the ends of it included.** A
band offered its two sides and nothing across the end it stops at — so wherever the town's kerb turns a
corner that no other piece stands beside, the shell had no line there to be cut from and the pavement
simply stopped: a street meeting a wider street, a street stopping at a car park, a dead end's head. The
end of a band is square (TER-7a), so what stands the offset outside it is a quarter turn about each of its
two corners and the straight between them, and the three of them start and finish where the band's own
side lines do. **A shell has no ends**: every metre of the outside of a piece is offered, and which metres
of it are pavement is settled by the one rule and nothing else.

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

**TER-3d** **The kerb line stands on the kerb and not in the lane.** It is the innermost stroke of the
pavement — struck on the same curve the band's two edges are struck on (TER-3c.3), a line's width
*outside* the carriageway — so the asphalt from the kerb line to the centreline is the lane the town is
laid at (GEN-15). Struck inside the carriageway — the way an edge shade is struck inside the surface it
rims — the line takes its own width off the lane it marks, and every lane measured off a picture comes out
short of the figure the rest of the build quotes, on the bends as on the straights. **And struck on the
walk's own curve rather than on the tarmac's outline**, because those are two shapes: the outline steps
wherever one piece of tarmac is narrower than the one it meets, and a kerb line that follows those steps
reads as a chamfer cut across a corner the pavement beside it turns smoothly.

## What this slice must produce

- A query `ground at (x, y)` → type, permissions, grip, drag. Continuous position in, no snapping out, and
  a point off the town's own box answered rather than refused.
- The same answer the surface is drawn from, everywhere and exactly
  ([app/render](../../../app/render/docs/requirements.md) owns the drawing).
- Pavement bands of constant width along every straight and correct round every corner, inner and outer.
