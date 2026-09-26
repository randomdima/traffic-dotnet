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
carriageway; a pedestrian-legal way across a carriageway; ground a car idles on that a pedestrian may stand
on; paved pedestrian-only ground; and ground permitted to nobody. Two types differing only in what they draw
are still two types, but no rule may turn on that difference alone.

**The ground roads share where they meet is not among them** (TER-5): the tarmac inside a junction is the
band its own movements sweep, so a kind for it would be a second name for the ground a car is driven over,
and every rule that could turn on the difference is a junction's — `world/road`'s, asked of the road graph
rather than of the ground under a point.

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

**A raster is scratch and never an answer.** The generator keeps one of what its statics have taken
(`GenClaims`); it agrees with the shapes only to within a cell, it is never shipped, and no rule about a
finished town may be argued from it. What the ground is while a town is being laid is asked of the shapes
laid so far (`GroundPieces`), exactly as the finished town's is.

**TER-7a** `P4` **A band of ground ends where its own line ends**, square across, and not in a half-disc of its
own half-width past the last point. The round end is the arithmetic cheapest to measure a band by and not
its shape: it is tarmac nothing is drawn on and nothing drives over, half a lane of it past the point a
movement starts at, reaching out under the pavement corner beside the mouth.

**TER-7b** `P0` **The ground is a stack of layers, and a layer struck off the town's own boundary is one region
of it.** The town is drawn bottom to top — the grass over the whole world, then the pavement and the kerb
along its outer face, then the water and the decks, then the driven ground at its own size and the slabs,
then the town's own kerb, then the paint — and **the pavement and the driven ground are each the ground
within one distance of the kerb**, filled as the shape it is. The boundary is computed once (`LaneShell`)
and each of the two is it moved by a figure (`GroundRings`), so a layer has an edge that is a line rather
than whatever a heap of overlapping pieces happened to leave. **A layer reaches the boundary and encloses
every layer inside it**, so what the order states is the difference between two of them and no layer is
cut to leave room for the next.

Four consequences follow and all four are the point.

- **The picture and the answer are one distance.** What the ground is at a point is which line of the town
  lays it and how far off the kerb it stands, compared against the same table the drawing is laid from
  (TER-7) — so a distance added to one is added to the other, and the question of whether the two agree
  cannot be asked. This is the whole of what the rule buys.
- **A kerb is a line and is struck along a shell of its own, at its own width** (TER-3d). It belongs to no
  layer: a kerb is where one ground hands over to another, and a stroke laid about the shell that parts them
  is that line said once, at the width a kerbstone is, wherever the shell runs. There are two of
  them and each has its own shell — **the town's kerb off the driven ground's boundary, the walk's own kerb
  off the pavement's outer face** — and neither takes a triangle from any fill. Struck instead as the ground
  between two of the distances, a kerb is the difference between two filled shapes each sampled and thinned
  on its own terms, and what survives of two hundred millimetres is whatever those two left of it. **So a
  kerb is laid after the fill it bounds**, and it is the one thing in the town with corners of its own to
  turn: the corner its shell turns, at the stroke's own width.
- **A rim and an edge line are what a layer leaves of the one under it**, never a shape of their own: two
  layers a line's width apart, the outer in the line's shade and the inner in the surface's own laid over
  it, and what survives is a stroke on the outer one's own edge. Such a line has no ends to close and no
  corners to turn — the layer turned them — and an edge shade is struck *inside* what it rims. **It is what
  is left where there is no shell to strike a line off**, a deck being a ribbon about a road's own line.
- **A block is a hole, and takes no pass of its own**: which ring is an outside and which a hole is read
  off the sign of the area it encloses (`ShellFill`). Outside the town the distances nest inwards; inside a
  block, a ring nearer the kerb leaves *more* of the block beyond it, so the same fills in the same order lay
  a block's kerb, its walk and the grass inside it. What a block shows is the sequence read outwards from
  its own kerb.

**What this rule does not license is drawing the same thing twice in one layer to hide a seam.** Two bands
that abut are two offsets of one curve; laid as two shapes each is sampled to its own curvature and they
stand a chord's sag apart, and grass shows between them. **A layer here has no seams because it has no
bands** — it is one region reaching the boundary, the layer inside it covers it, and a seam inside it would
be a seam in its own boundary. A kerb laid over the place two grounds meet is not that either: there is no
seam there to hide, the two having been laid one over the other, and a line the town is built of is not a
second copy of the ground it stands on.

## The pavement

**TER-3c** `P6` A town is laid with a **pavement**: a band of preferred walkable ground running the whole
length of every carriageway on both sides, touching the kerb, turning the corner of every junction and
wrapping every lot. **It is the kerb line moved off itself** — the driven ground's own boundary
(`LaneShell`) moved by `SimConfig.WalkOuterM` and filled whole (`GroundRings.Walk`) — and the carriageway is
laid back over what it covers, so the band is what the carriageway leaves of it: the two strips either side
of every street, round every corner the boundary turns.

**It is laid once, as a step of its own (`Paving`), and the picture and the answer both read that one
laying.** Worked out a second time by whoever needed it, a band widened in the picture and not in the answer
is a walker refused ground it can see it is standing on.

**TER-3c.1** `P4` The network a walking route is planned over *is* the pavement, its corners and its
crossings; this is structure, not price. A bounded hop off the network to a nearby door is still allowed,
and a road is still crossed only at a crossing. **What that network is made of is
[world/foot](../../foot/docs/requirements.md)** (WLK-1): the driven ground's own boundary moved off itself,
and not a line discovered in this ground.

**TER-3c.2** `P6` The building line stands behind it: a wall is set back from the carriageway by the whole
of the pavement, standing on the walk's own outer kerbstone (`SimConfig.BuildingLineM`, `GEN-54`), so
nothing is built on the walk and a doorstep opens onto it. Street planting stands on the verge behind the
walk for the same reason — a trunk in the middle of a four-metre pavement is a trunk everyone on that
street goes round.

**TER-3c.3** `P4` **The pavement is the ground between the kerb and a walk beyond it**, at every angle two
arms can meet at. The kerb's own line is the boundary of the driven ground said as closed rings
(`LaneShell`) and every line the town has is that boundary moved by a figure (`GroundRings`) — the
carriageway at nought, the kerb's outer face at half a kerb, the pavement's outer face at a kerb and a walk,
and the walk's own kerb half a kerb beyond that. So **every one of them is an offset of one curve** and the
ground between any two is exactly the difference between the distances that struck them. **Every figure is
measured from the boundary and never from the line before it**, which is what keeps that difference true: an
offset taken off an offset inherits whatever the first one rounded, so a walk struck that way comes out
narrower on the bends than on the straights. Where the boundary turns a corner it turns it once, on the ring
itself (TER-5), and every distance inherits that corner at its own radius. Nothing is patched and nothing is
measured twice — the kerb, the concrete and the answer are one construction read at four figures.

**One of the four is a shape and the rest are lines.** The walk out to its outer face is the region; the two
kerbs and the walk's outer face itself are lines, handed over as the closed lines they are and given a
thickness by whoever draws them (TER-7b) — so no offset is struck at a figure only a line stands at.

**TER-3c.10** `P4` **Every line struck off that boundary is rounded, and every line the ground is built of
at the one radius.** A kerb is laid in stone and a pavement is walked, and neither follows a corner a fold
in an offset cut: left sharp it is a spike of concrete nobody walks round and no kerbstone bends to. So the
rounding is part of striking the line rather than a pass over it (`ArcOutset`), **and `Road.LineRoundedM` is
one figure for the whole of the ground** — the carriageway, the kerb and the pavement's outer face. Two
layers of concrete rounded at two radii disagree about the same corner, and the band between them is then
wider on one bend than on the next.

**The boundary itself is rounded at that radius too, and there it is a cut** — a corner the town turns away
at loses up to 0.41 of the radius — which is why the radius is under half a lane.

**A course a walking lane is a stretch of is not a layer of the ground, and takes its own radius**
(`Road.WalkRoundedM`, WLK-1). It is a line a body is held on rather than a thing the town is built of, so
what it owes is a walk nobody has to pick their way round rather than agreement with a kerbstone — and it
fills without cutting, so no radius pulls a walk towards the tarmac or takes a course its closure. What that
costs, a pocket narrower than twice the radius closed over and a crossing at a sharp fork meeting its course
further off, is a reading and not a fault (`Road.CrossingMeetsTheWalkWithinM`, WLK-15).

**This is the ground and not the network.** Where a walk may go is the boundary moved off itself
([WLK-1](../../foot/docs/requirements.md)) and is no reading of these rings; what they owe each other is
that the lanes a walker is held on stand on the concrete these strike.

**TER-3c.7** `P6` **The carriageway ends where the pavement starts.** Everything inside the kerb is tarmac —
carriageway, junction and car park, and the pockets the town's own pieces leave between them: a movement
narrower than the arm it leaves, a car park set back off the street it fronts, a street meeting a wider
street. Such a pocket is not a bay of concrete: a kerb drawn round it steps and chamfers at every mouth
while the walk beyond runs smoothly past, and the band comes out a different width at each.

**TER-3c.6** `P6` **A pavement is a ring and has no ends.** The boundary of the driven ground closes on
itself — one ring round the outside of the town and one round every block it encloses — so the concrete laid
a walk off it closes too, and the stack of layers beside a road is a set of closed shapes rather than a heap
of pieces with ends to reconcile. **A band the boundary is merged from has square ends** and the ring turns
them, which is why no piece of it ever finishes in mid-air.

**TER-3c.8** `P0` **The walk wraps the tarmac as one shape, and never a piece of it.** What the pavement is,
is a distance off the outline of the **union** of the driven ground, so a place on it belongs to exactly one
line by construction rather than by a tie broken between candidates. Two coincident lines are not two
pavements — a car park whose bays' ways converge on one pose would offer six down one metre of kerb, a
movement running edge to edge with the arm it leaves two — and a boundary computed once has none of them to
break: it is one line because it was never several.

**TER-3c.9** `P3` **A line the town strikes off its boundary is struck by name, and its normal points inside
the perimeter.** The name and the figure are joined in one place (`GroundLayer`'s `Named` and `OutwardM`) so that
a line gains a reader without gaining a literal, and every one of them is walked with the driven ground on
the walker's right — on the ring round the town and on the ring round every block it encloses alike. **So the
right of travel is the inward side everywhere**, and whatever is laid along such a line reads its own inward
side off the line's own direction: nothing to look up, no ring to identify as the outermost, and no ground
query to ask. A ring whose walk came out the other way round hands every one of those back inverted while
still looking like a perfectly good closed line, which is why this is a rule and not a convention.

## Water and bridges

**TER-3b** `P6` A carriageway crossing ground legal to nobody carries a **bridge**: a deck wider than the
carriageway, its exposed edges walkable, **running the whole road rather than only the wet part**, so
what it carries reaches standable ground at both ends.

**TER-3b.1** `P6` A deck is **wide enough for the town's pavement to cross it** and **carries none of its
own**: it is the deck out to its own half-width, and the margin either side of the carriageway is what a
parapet stands on. A deck sized to a walk of its own is a deck the street's pavement does not fit on, which
is what the width on its plan record is for — but a deck that lays that walk itself is a line beside a road
struck by arithmetic of its own, which is the one thing no line beside a road may be (TER-3c.3). **The walk
across a deck is the street's own, carried over it because the street is** (WLK-1): a road over water is a
road, and what is walked beside it is walked beside it there as anywhere else.

## The edge line

The outside of the pavement and of a bridge deck each carry a line, the way the carriageway carries a
kerb. **An edge is the surface drawn darker; paint is the surface drawn brighter**, and the grain of
the ground comes through both.

**A deck's edge is a rim** (TER-7b), a deck being a ribbon about a road's own line with no shell to strike
anything off; **the pavement's is a kerb**, struck along the walk's outer face (TER-3d). Nothing walks an
edge or probes a region.

**TER-3d** `P6` **A kerb straddles the ground it bounds, and is a stroke along that ground's own shell.**
The town's kerb is the driven ground's boundary laid at a kerb's width with that boundary running down the
middle of it, and the walk's own kerb is the pavement's outer face laid the same way (TER-7b, TER-3c.3) — a
kerbstone half in the road and half in the concrete, which is where a kerbstone stands. **So no part of a
kerb ever stands further from the line it was struck from than half its own width**, on a bend, at a corner
and at the tightest hook the boundary has alike, and a kerb seen to bulge off its own line is a defect in
the stroke rather than a shape the town has.

**A fill has no edge anybody sees**: every shell drawn here carries a kerb along its boundary, laid over it,
so the fill beneath may be thinned by what the kerb hides while the kerb's own line is cut for the picture.
**A fill whose edge reaches out from under its kerb is the defect**, and the only one the thinning can cause.

**Its two faces are that line at half its width either side**, so wherever the shell steps the kerb steps
with it, and it borrows no triangle from either fill (TER-7b). **Where the shell turns tighter than half a
kerb, that side of the stroke stops at the middle of the turn** — the one place the width gives, since an
edge carried further comes back on the far side of its own line.

## What this slice must produce

- A query `ground at (x, y)` → type, permissions, grip, drag. Continuous position in, no snapping out, and
  a point off the town's own box answered rather than refused.
- The same answer the surface is drawn from, everywhere and exactly
  ([app/render](../../../app/render/docs/requirements.md) owns the drawing).
- Pavement bands of constant width along every straight and correct round every corner, inner and outer.
