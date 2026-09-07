# Roads, junctions and crossings — requirements

The street network: what a road is, what a junction is, where crossings go and what is painted on any of
it. **How ground is handed out over that network — which movements conflict, who gives way, and what a
claim is — is [claims.md](claims.md).** The ground itself is
[world/terrain](../../terrain/docs/requirements.md).

## Roads

**TER-4** A road runs between **two named intersections and touches no third**; nothing infers topology
from geometry. Its shape is an **arc spline** — a chain of constant-curvature pieces — so a straight is
the same record at zero curvature and not a separate kind. **A road declares its own width**, and
everything derived from it follows the road's own rather than the catalogue default.

**TER-4a** Traffic keeps right: a road's two lanes are assigned by heading, and left turns cross oncoming
traffic and must yield — which is a right of way and is stated as one (TER-5e). **The side of the road is a
single global constant** — lane offset, turn classification, keep-right on foot and which flank a car door is
on all read that one.

**TER-4d** **A road runs one way or both ways, and a one-way road is the narrower for it.** A carriageway
is as many lanes as it has ways, laid at the one lane width either way (GEN-15): two lanes where traffic
runs both ways, and **one lane down the middle of half a road** where it runs one. **That half is the half
its traffic drives, and the road stands on it**: its own half to the driving side of the line its two
intersections are joined on, which is where a carriageway of two ways carries the lane running that way — so
a one-way street meeting one of two ways continues that road's lane and that road's kerb rather than
stepping across them. A one-way road therefore has no reverse lane to come back down, cross round what is
in its way, or park against; no centreline, because it divides nothing; and **a bar only on the arm traffic
comes to the junction on**. Everything else — the crossing on it, the kerb beside it, the claims over it —
is what it always was, read off the road's own width.

**Which way a road runs is the road's own and is never read off its shape.** A road is drawn from one
intersection to the other and may be driven either way along that (`RoadFlow`); nothing infers it from the
width, because a narrow road is not necessarily a one-way one and nothing infers topology from geometry
(TER-4).

**TER-4b** Two carriageways coming within a pavement's width of one another **must both name an
intersection there** — nearer than that and no pavement fits between them, so their tarmac is one surface
and a walker has no way past on foot.

This is not a tidiness rule. With no node, the lane graph has never heard of the spot, so nothing turns,
gives way, is signalled or is crossed on foot, and **the two streams merge through each other** while
coverage, connectivity and alignment all pass over paved, drivable, single-region ground. It is invisible
to a reader of the map file, because each road's topology is impeccable. A road turning a corner *on
another road's carriageway* is this; a road that turns a corner touching nothing is not.

**A road is also cut where it is not a junction.** A slice above may ask for a node of its own on a road —
today only the parking sections, whose rule is
[`GEN-4h`](../../parking/docs/requirements.md) — and the cut it gets is a point rather than a disc, so the
two lanes it makes meet exactly and nothing below hears of it as an intersection. Where such a node may
stand, and what it is for, is that rule's; what it means for a lane is the same thing every other cut
means, which is why it is here and not a second mechanism.

## Junctions

**TER-5** An intersection **has no shape of its own**. The ground inside one is **the ground its own
movements take** — the band every connector between its arms sweeps (TER-5d) — with the wedge between each
pair of neighbouring arms paved back to an **arc tangent to both carriageways**, which is the line a turning
car takes. Arms, movements and fillets come out as one region because each of them is drawn where a car
actually goes, so a box that is skewed, one-way, five-armed or barely a bend is right without anything
having to recognise which of those it is.

**A junction's radius is a planning figure and never a piece of ground.** It says how far back the arms are
cut so the movements have room to be drawn, and it is what crossings and lots measure from — **"how far the
junction reaches"** being that radius plus the corner radius. Nothing reads it as a surface: no ground is
answered from it, no pavement is laid round it, and none is drawn.

**The radius is sized on the arm whose ground reaches furthest from the node — its own half and however far
off the node its road stands — and a corner is solved on the two it stands between** (TER-4d). Where a
one-way street meets a full carriageway their kerbs cross off the bisector, further out along the narrow
arm than along the wide one, and each arm is reached as far as its own tangent point rather than to one
figure both share. **An arm's kerb is where its road actually carries it**, which for a street standing on
the driven half of a carriageway is one kerb nearer its neighbour and the other that much further off.
**Two arms of different widths lying all but against one another turn no corner at all**:
their kerbs are parallel and cross behind the mouth if they cross anywhere, which is a road narrowing at a
junction — a step in the kerb, and nothing for a fillet to be tangent to.

**A junction is not sized around a car's turning circle**, and there is no exception. Turning geometry is
the controller's problem.

**TER-5a** A **dead end** is an intersection with a single arm, and **it has no ground of its own** like
every other junction: what is there is the road that stops, and the road stops where its own last point is.
It carries no crossing and no lights.

**A dead end is therefore not a place a car can turn round in**, which is a change from what this rule used
to promise: there is no head, and a car working itself round on the spot (`P-19`) has only the width of its
own road to do it in. A leg that has to come back the way it came does it in a car park's bay (`GEN-4l`), and
a dead end with no bay off it is a place nothing that drives in can leave. **A map that wants a turning head
has to lay it** — as paved ground of its own, which the plan already carries and every reading of the ground
already answers for.

**TER-5b** An **inline junction** has exactly two arms leaving in opposite directions — a place *on* a
road rather than a place roads meet. The two carriageways must align exactly, so a driver sees
uninterrupted road. It exists to
carry one pedestrian crossing and the signals that govern it, which is what makes a lit mid-block
crossing possible at all.

Two arms meeting at an **angle** are not this: that is a road that turns, rounded to a kerb radius. Worth
saying twice, because it is the commonest authoring mistake — a corner is a road, a mid-block crossing is
a junction, and they look alike in a map file.

**Corners are decided per corner by which arms are present**: two arms give a fillet tangent to both
carriageways; neither arm is the outside of a turn, so the disc's own corner is *cut* back to an arc of
the same radius; one arm is a straight kerb running on, and nothing is drawn. Both kinds pave their
square whole first and then take the ground back along the arc.

**TER-5d** **A junction is a set of connection points, and the connectors are what run between them.** Every
movement out of a lane starts at that lane's own last point and every movement into one lands on its own
first point, so a lane's line is the whole of what is driven along it and the ground past either end is the
junction's alone. **Nothing runs over a connection point**: no lane carries a spur into the box for a
movement to be drawn over, and no reader adds a figure to a lane's metres to find where they begin.

**A lane end is one point, whatever is driven off it.** How far back from the disc it stands is far enough
that the arc from it reaches the junction's own corner radius, and it is the deepest any of that end's
movements asked for — so a straight and a right-angle turn out of one lane hand over at the same place, and
what the corner takes is **cut off the lane** rather than marked on it. It is never more than the stretch
can spare, because a lane cut away is a lane the town has not got. Every movement in the reckoning reaches
a radius, because the one that never could is not a movement (TER-5f).

**TER-5d.1** **The ground a movement is driven over is as wide as the narrower of the two lanes it
joins**, and that is one figure the whole town reads — the tarmac's own shape, the answer at a point and
the picture. Its two ends are on lanes that need not be the same width and a band has only one, so the
narrower is the only choice that never claims ground outside the arm it leaves or the arm it arrives on:
drawn at the arriving lane's width, a movement out of a narrow street onto a wide one stood half a metre
past the narrow street's own kerb, in the pavement.

**TER-5f** **No box admits a movement that reverses the direction of travel.** A pair of lanes that would
face each other across an intersection is not joined at all: no turn is classified between them, no line is
drawn, no ground is measured against it and no route may be handed one. The arithmetic is why — the line
between two opposing lanes a lane's width apart is a semicircle of a metre and a half, tighter than any
car's lock at any setback — and the consequence is deliberate: **a leg that has to come back the way it
came does it in a car park's bay (`GEN-4l`) or by working itself round at a dead end (`P-19`, TER-5a),
which are manoeuvres a driver makes and not movements a junction offers.**

## Crossings

**TER-6** Crossings and parking are variants of the road/intersection family and need only a type tag
beyond their terrain attributes.

- A crossing is **a band of the same carriageway pedestrians may walk over**. It is a plan entity of its
  own and the road graph never reads it, so **a crossing adds no node and nothing can turn at one**.
- **A crossing has no width of its own.** It names the road it is painted across, and how far it reaches is
  that road's width measured along the paint's own axis — so a crossing laid off square is longer by what
  the skew costs it and still reaches kerb to kerb, and one laid square is the carriageway's width. That
  one figure is what it is drawn, walked, stopped for and asked about at. A span carried beside the road's
  is a second answer to a question the road has already answered (GEN-15), and the two disagree the first
  time either is laid again: a zebra wider than its carriageway stands its end bars on the pavement, and a
  narrower one leaves a strip of road nobody is walking over.
- The terrain carries the rule: crosswalk ground is person-allowed *and* car-allowed, and it is a stretch of
  the road it is painted across rather than a shape of its own — so the lane runs underneath it and a car on
  a crossing is still held to that lane.
- **Placement is one rule, not hand-picked positions**: one crossing on every arm of every junction at a
  fixed setback from the paved junction reach, each tagged with the junction it approaches — so a
  junction's signal bundle greens *its own* arms' crossings. **The reach is that arm's own** — where the
  kerb fillet between it and its furthest neighbour lets go of the kerb, which grows as the corner sharpens
  — and never the distance from the node, which is the same on every arm of every junction and right on
  none of them. The bar behind the crossing is set back from the same place, and so is everything hung off
  either of them.
- **A junction that admits no fork carries one crossing and not one per arm, and none of the junction is in
  where it goes.** Two arms are one road: everything that arrives leaves the only other way, so the node is
  somewhere to cross rather than somewhere to choose, and a second zebra a few metres from the first is the
  same road crossed twice and the same stop asked for twice. **The paint is the crossing's own bundle** —
  the zebra with the bar of each of the two lanes that run over it, one either side and each facing the
  paint — laid on **whichever of the two arms has the most road left behind it**, and it **begins where that
  arm's own bend lets go** rather than a setback past a box: there is no box behind it, only the same road
  swept round its corner (GEN-12a), and what is laid across a straight begins where the arc ends. Every other
  junction's setback is a distance from a place cars turn across; this one's is the curve they drive round.
- **The bars of such a node are the one pair a junction the signals do not govern carries**: nothing at two
  arms is lit (TLT-3), so the whole of what governs the paint is the walker's own right of way (TER-5e) and
  the bars are what say where the stop for one is made. A light there, if a map ever authors one, hangs off
  those bars like every other and stands beside the zebra with them.
- **And no lane line stops for such a node, only for its paint.** What a dash must not be laid down is ground
  the movements through a box are driven across, and there are none here but the one the road itself makes —
  so the line runs from the bundle's own outer bar **through the bend and the node** and on down the other
  arm, as it does along any road that turns a corner. The one thing that still breaks it there is a zebra
  laid **on the node itself**, which is what an authored inline junction carries: paint breaks a lane line
  wherever the paint is.
- **Elsewhere a lane line stops at the ground its junction reaches** and not at the disc that junction is
  drawn on, whether or not the arm carries paint. The metres between an arm's kerb fillet and the disc are
  the same turning ground as the rest of the box, and an arm too short for a crossing has nothing else to
  stop its dashes: that reach is `SimConfig.JunctionArmReachM`, read from the figure and never measured a
  second way.
- **An arm too short to hold setback plus band clear of both junctions gets none.** Short spurs and small
  rings therefore have no crossings, and that is correct.
- The inline junction is the exception and takes a single crossing laid on the node itself. **Being on the
  node, it is past the end of every lane there** — the disc reaches further than the paint is wide — so it
  is laid across the lanes that meet at the node, each at its own end, rather than found by projecting it
  down one of them. A crossing no lane carries is paint no driver slows for and a walker no driver can see
  (TER-4c).

**A crossing with no conflicting traffic to phase against carries no lights** (TLT-3), and an uncontrolled
crossing is where the walker's right of way is the whole of what governs it (TER-5e): the traffic gives way
to whoever is standing at the kerb, which is what the paint is there to say.

## Markings

Everything painted on the ground is **engine-drawn primitives, never art**: lane centrelines (dashed,
stopping at the outermost paint an arm carries — its bar where it has one and its crossing where the
junction is unlit — rather than running on into the junction behind it), kerb lines (broken exactly
where the pavement's edge is, and over a car park's mouth, where the ground on the far side of the line
is the lot's own tarmac and there is no kerb to be the edge of), pavement and deck edge lines, stop bars
(square across *that arm's* direction, covering one lane only — the one driving at the paint — and
stopping at the kerb), zebras
(spanning kerb to kerb, running along the direction
of the traffic that crosses them, between their bar and the junction without overlapping the bar, or
between their two bars at a node that forks nothing), bay
outlines (three-sided, open at the mouth, so a row of bays leaves no line across the ground a car enters
the lot over, and laid against the lot's own edge — inside it — wherever they stand within a line's width
of one) and drift marks.

**A car park's paint and the road's are one line where they meet.** The strokes at a lot's mouth end on
**the carriageway's own edge** and not on the lot's rectangle, which is a chord of that edge and stands up
to its sag inside it; the kerb line is broken over **the mouth** — the lot's road-facing edge, not the
shadow its whole rectangle casts along the road — and stops a line's width short of either end of it, so
the corner where the two turn into one another is painted and painted once. A gap at that corner is a gap
in the one place a driver entering the lot is looking.

Six rules govern all of it:

1. **A coordinate is read from whatever owns it, never re-derived.** One shape, one pattern, one lane
   offset. A figure that exists in two places eventually disagrees with itself.
2. **Everything is drawn in its own frame.** A crossing on a road running north-east carries the same
   zebra as one running due east.
3. **A bar on an arm with a crossing is placed by the crossing alone**, never by the junction as well, or
   the two answers differ by metres.
4. **Every run of marks is centred on the stretch it is on**, so a dashed line does not begin with a half
   dash.
5. **Paint sits on the surface it belongs to**, checked on rendered frames because no numeric check
   answers it (VER-9).
6. **A mark laid along a road is laid on the road's own curve**, never on the chord of it. A dash struck
   straight across a bend stands its own sag off the line it marks, which at the radius a town's tightest
   bends are laid to is most of a line's width — the line reads as a row of tangents rather than a curve.

## What this slice must produce

- **Directed lanes and the connectors between them, and no node table.** Lanes are cut at **every**
  junction a road runs through rather than only the two it ends at, and at the places a slice above asked
  for (`GEN-4h`). **Where two lane ends are the same ground is worked out from the connectors** — a
  connector runs between them, or they are the two ends of one stretch driven either way — so a junction is
  the shape a set of crossed lanes makes and is nothing the network carries. Derived twice, the router and
  the claims would be entitled to disagree about which lane ends are one piece of the world.
- **The plan's junctions, and the lanes each of them lowered into**, for the two slices whose subject *is*
  an intersection: the signals a bundle governs (`TLT-1`) and the paint laid on an arm (TER-6). Nothing
  that drives, routes or claims may reach it.
- A turn on every connector — straight / near-side / far-side — **filled once when the town is laid** and
  read off thereafter, and **no connector at all between a lane and the one running back down its own
  stretch** (TER-5f). Which turn a connector makes is a fact about the road, not about the car on it.
- A crossing registry queryable by junction, and a stop-line registry carrying the bars actually painted.
- A table, filled once from the lines themselves and **indexed by way**, of where each of the town's ways
  is driven over the others, in both ways' own metres (TER-5c). **There is no register of who is inside a
  junction**: the table is looked up and the answer comes off the claims everything else reads
  (TER-5c.1). It is laid over every numbered way, so a slice above the road can measure its own
  ways into it with the same code and be read by the same walk.
- A lane occupancy index over the ways of TER-4c — the lanes, the connectors between them, and the ways a
  slice above lays off them — carrying every
  body on the network and the stretch each driver has taken, so that **who is in front and how much road
  is whose** are answered from the town's own claims rather than from geometry (`S-2a`). It is laid over
  ways the caller measures, so the pavement keeps a second set of the same kind (PER-13). **The two
  are told apart by which network the ground belongs to and never by which kind of body is standing on
  it.**
