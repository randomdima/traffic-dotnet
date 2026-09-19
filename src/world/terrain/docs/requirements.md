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

**The ground roads share where they meet is not among them, and that is TER-5 read here.** An intersection
has no shape of its own: the tarmac inside one is the band its own movements sweep, which is the surface a
lane lays. A kind for it would be a second name for the ground a car is driven over, told apart by which
line happened to lay it and permitting exactly the same things — and every rule that could turn on the
difference is a rule about a *junction*, which is `world/road`'s and is asked of the road graph rather than
of the ground under a point.

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
The town is drawn bottom to top — the grass, then the pavement and the kerb along its outer face, then the
water and the decks, then the driven ground at its own size and what the blocks take back, then the town's
own kerb, then the paint — and **each of those layers is the ground within one distance of the kerb**, filled
as the shape it is. The boundary is computed once (`LaneShell`) and every layer is it moved by a figure
(`GroundRings`), so a layer has an edge that is a line rather than whatever a heap of overlapping pieces
happened to leave. **A layer reaches the boundary and encloses every layer inside it**, so what the order
states is the difference between two of them and no layer is cut to leave room for the next.

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
- **A block is a hole and is filled after every region, in the other order.** Outside the town the
  distances nest inwards; inside a block, a ring nearer the kerb leaves *more* of the block beyond it, so
  the same order paves a block kerb to kerb. What a block shows is the sequence read outwards from its own
  kerb.

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
and the walk's own kerb a kerb beyond that. So **every one of them is an offset of one curve** and the
ground between any two is exactly the difference between the distances that struck them. **Every figure is
measured from the boundary and never from the line before it**, which is what keeps that difference true: an
offset taken off an offset inherits whatever the first one rounded, so a walk struck that way comes out
narrower on the bends than on the straights. Where the boundary turns a corner it turns it once, on the ring
itself (TER-5), and every distance inherits that corner at its own radius. Nothing is patched and nothing is
measured twice — the kerb, the concrete and the answer are one construction read at four figures.

**TER-3c.10** `P4` **Every line struck off that boundary is rounded, and every line the ground is built of
at the one radius.** A kerb is laid in stone and a pavement is walked, and neither follows a corner a fold
in an offset cut: left sharp it is a spike of concrete nobody walks round and no kerbstone bends to. So the
rounding is part of striking the line rather than a pass over it (`ArcOutset`), **and `Road.LineRoundedM` is
one figure for the whole of the ground** — the carriageway, the kerb and the pavement's outer face. Two
layers of concrete rounded at two radii disagree about the same corner, and the band between them is then
wider on one bend than on the next.

**A course a walking lane is a stretch of is not a layer of the ground, and takes its own**
(`Road.WalkRoundedM`, WLK-1). It is a line a body is held on rather than a thing the town is built of:
nothing is laid along it, no kerbstone bends to it, and what it owes is a walk nobody has to pick their way
round. **And it fills without cutting**: the notch a fold left is filled at the course's own radius, and a
corner the course turns away at comes back as the arc of the distance moved whatever that radius says. It is
what the ground's own rounding may not do — a kerb is the line a ball rolls and not a shape moved — and it
is what a line a body is held on needs, a radius that could cut being a radius that pulls a walk towards the
tarmac. **So the course's figure has no bound**, and no radius takes a course its closure (WLK-1).

**What it costs is the pockets, and the junctions that stand in them.** A dip in a course narrower than
twice the radius is closed over rather than walked into, so at a sharp fork the walk stands off the apex by
about the radius and a crossing there meets its course further off than one down a straight street does.
**That is a reading and not a fault** (`Road.CrossingMeetsTheWalkWithinM`, WLK-15): what the reach refuses
is the walk across the road or round the block, and how far off a course that was refused really stood is
the census's to report.

**The boundary itself is rounded by it, which costs the corner it rounds.** A corner the town turns away at
already stands where the distance put it, so rounding it is a cut into the ground rather than a fill beside
it — a right angle loses 0.41 of the radius. **The radius is therefore under half a lane**, which is what
holds the cost to a corner: nothing a car is driven through is narrow enough to be closed over, and no
ribbon of tarmac is thin enough to be swallowed by a ball of that size.

**This is the ground and not the network.** Where a walk may go is the boundary moved off itself
([WLK-1](../../foot/docs/requirements.md)) and is no reading of these rings; what they owe each other is
that the lanes a walker is held on stand on the concrete these strike, which is what sizing both off the
carriageway's own width buys.

**One of the four is a shape and the rest are lines**, and a line is not cut out of a shape. The walk out to
its outer face is the region, struck as the offset of the boundary with the driven ground taken back out of
it. The two kerbs and the walk's outer face itself are lines: **a line is handed over as the closed line it
is and given a thickness by whoever draws it** (TER-7b), which is one mesh and no offset, against the two
offsets and the cut a region costs. **So no offset is struck at a figure only a line stands at** — the
kerb's outer face and the walk's own kerb are figures a line ends at, and neither is a shape the town holds.

**TER-3c.7** `P6` **The carriageway ends where the pavement starts.** Everything inside the kerb is tarmac —
carriageway, junction and car park, and the pockets the town's own pieces leave between them: a movement
narrower than the arm it leaves, a car park set back off the street it fronts, a street meeting a wider
street. Such a pocket is not a bay of concrete. Drawn as the tarmac's own outline instead, the kerb stepped
and chamfered its way round every mouth in the town while the shell against the grass and the lane between
them ran smoothly past, and the band came out a different width at each of them.

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
the perimeter.** The name and the figure are joined in one place (`GroundLine`, `GroundRings.OutM`) so that
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

**A deck's edge is what its layer leaves of the one under it** (TER-7b) — the deck laid at full size in the
line's shade and again its own width smaller in the surface's own — because a deck is a ribbon about a
road's own line and has no shell to strike anything off. **The pavement's is a kerb and not a rim**: the
walk's outer face is a closed line the boundary struck, so the concrete's outer edge is a stroke laid along
it at a kerbstone's width, drawn after the walk and covering whatever the walk's own thinning left short of
it. Nothing walks an edge or probes a region.

**TER-3d** `P6` **A kerb straddles the ground it bounds, and is a stroke along that ground's own shell.**
The town's kerb is the driven ground's boundary laid at a kerb's width with that boundary running down the
middle of it, and the walk's own kerb is the pavement's outer face laid the same way (TER-7b, TER-3c.3) — a
kerbstone half in the road and half in the concrete, which is where a kerbstone stands. **So no part of a
kerb ever stands further from the line it was struck from than half its own width**, on a bend, at a corner
and at the tightest hook the boundary has alike, and a kerb seen to bulge off its own line is a defect in
the stroke rather than a shape the town has.

**And the ground beside it is drawn to a coarser line than the kerb is.** A shell is read as corners before
anything is laid from it; the kerb is struck from that reading and the fill beneath it is that same reading
thinned, because **a fill has no edge anybody sees** — every shell drawn here carries a kerb along its
boundary, the kerb is laid over it, and where the fill cuts a corner what shows through is the layer under
it. So what the fill may be got wrong by is what the kerb hides, and the kerb's own line is cut for the
picture instead. **A fill whose edge reaches out from under its kerb is the defect**, and it is the only one
the thinning can cause.

**Its two faces are that line at half its width either side**, so wherever the shell steps — one road
narrower than the one it meets, a movement leaving an arm, a car park set back off its street — the kerb
steps with it. **And it is a constant width because it is a stroke and not a difference**: struck as the
ground between a line and an offset of it, it is two filled shapes subtracted, each thinned for the picture
on its own terms, and it comes out a kerb wide only where the two thinnings happened to agree. **It borrows
no triangle from either fill and is drawn after them both.** Struck on a curve of its own instead, a kerb reads
as a chamfer cut across a corner the pavement beside it turns smoothly.

**Where the shell turns tighter than half a kerb, that side of the stroke stops at the middle of the turn**
rather than carrying on past it, which is the one place the width gives: an edge carried further comes back
on the far side of the line it is an edge of, and half a kerb off its own line is the rule the width serves.

## What this slice must produce

- A query `ground at (x, y)` → type, permissions, grip, drag. Continuous position in, no snapping out, and
  a point off the town's own box answered rather than refused.
- The same answer the surface is drawn from, everywhere and exactly
  ([app/render](../../../app/render/docs/requirements.md) owns the drawing).
- Pavement bands of constant width along every straight and correct round every corner, inner and outer.
