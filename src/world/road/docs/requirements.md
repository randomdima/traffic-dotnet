# Roads, junctions and crossings — requirements

The street network: what a road is, what a junction is, where crossings go and what is painted on any of
it. **How ground is held over that network — where the bodies are, what two ways share, who gives way — is
[claims.md](claims.md).** The ground itself is
[world/terrain](../../terrain/docs/requirements.md).

## Roads

**TER-4** `P4` A road runs between **two named intersections and touches no third**; nothing infers topology
from geometry. Its shape is an **arc spline** — a chain of constant-curvature pieces — so a straight is
the same record at zero curvature and not a separate kind. **A road declares its own width**, and
everything derived from it follows the road's own rather than the catalogue default.

**TER-4a** `P4` Traffic keeps right: a road's two lanes are assigned by heading, and left turns cross oncoming
traffic and must yield — which is a right of way and is stated as one (TER-5e). **The side of the road is a
single global constant** — lane offset, turn classification, keep-right on foot and which flank a car door is
on all read that one.

**TER-4d** `P6` **A road runs one way or both ways, and a one-way road is the narrower for it.** A carriageway
is its lanes side by side at the one lane width (GEN-15) and, on a traced road, a roadside between them and a
kerb, a lane joined to nothing that nobody drives (GEN-57) — each way's lanes running in from the kerb its
traffic keeps to, past the roadside there, as far as the line the two ways meet on, and the road's own line down
the middle of the carriageway: one
lane each way on a road the generator lays, and as many each way as its survey says on a traced one
(GEN-57). **A generated one-way street is one lane down the middle of half a road**, and **that half is the
half its traffic drives, and the road stands on it**: its own half to the driving side of the line its two
intersections are joined on, which is where a carriageway of two ways carries the lane running that way — so
a one-way street meeting one of two ways continues that road's lane and that road's kerb rather than
stepping across them. A one-way road therefore has no reverse lane to come back down, cross round what is
in its way, or park against; no line between its two ways, because it divides none; and **a bar only on the
arm traffic comes to the junction on**. **The paint goes between every two lanes that touch** (TER-6), and
**a lane's reverse is the lane the other way across that line from it**, which on a road of more than one
lane a way is only the innermost of each. Everything else — the crossing on it, the kerb beside it, the
claims over it — is what it always was, read off the road's own width.

**Which way a road runs is the road's own and is never read off its shape.** A road is drawn from one
intersection to the other and may be driven either way along that (`RoadFlow`); nothing infers it from the
width, because a narrow road is not necessarily a one-way one and nothing infers topology from geometry
(TER-4).

**TER-4b** `P3` Two carriageways coming within a pavement's width of one another **must both name an
intersection there** — nearer than that and no pavement fits between them, so their tarmac is one surface
and a walker has no way past on foot.

This is not a tidiness rule. With no node, the lane graph has never heard of the spot, so nothing turns,
gives way, is signalled or is crossed on foot, and **the two streams merge through each other** while
coverage, connectivity and alignment all pass over paved, drivable, single-region ground. It is invisible
to a reader of the map file, because each road's topology is impeccable. A road turning a corner *on
another road's carriageway* is this; a road that turns a corner touching nothing is not.

## Junctions

**TER-5** `P4` An intersection **has no shape of its own**. The ground inside one is **the ground its own
movements take** — the band every connector between its arms sweeps (TER-5d) — with the wedge between each
pair of neighbouring arms paved back to an **arc tangent to both carriageways**, which is the line a turning
car takes. Arms, movements and fillets come out as one region because each of them is drawn where a car
actually goes, so a box that is skewed, one-way, five-armed or barely a bend is right without anything
having to recognise which of those it is.

**A junction's radius is a planning figure and never a piece of ground.** It is the standoff its arms'
lanes end at (`GEN-46`, TER-5d) — one figure for every junction the town lays, the arms following the
standoff and never the standoff the arms. Nothing is placed off it and nothing reads it as a surface: no ground is answered from it, no
pavement is laid round it, and none is drawn. The census prints its mean as the junctions' reach.

**A corner is solved on the two arms it stands between** (TER-4d). Where a
one-way street meets a full carriageway their kerbs cross off the bisector, further out along the narrow
arm than along the wide one, and each arm is reached as far as its own tangent point rather than to one
figure both share. **An arm's kerb is where its road actually carries it**, which for a street standing on
the driven half of a carriageway is one kerb nearer its neighbour and the other that much further off.
**Two arms of different widths lying all but against one another turn no corner at all**:
their kerbs are parallel and cross behind the mouth if they cross anywhere, which is a road narrowing at a
junction — a step in the kerb, and nothing for a fillet to be tangent to.

**A junction is not sized around a car's turning circle**, and there is no exception. Turning geometry is
the controller's problem.

**TER-5a** `P6` A **dead end** is an intersection with a single arm, and **it has no ground of its own** like
every other junction: what is there is the road that stops, and the road stops where its own last point is.
It carries no crossing and no lights.

**A dead end is therefore not a place a car can turn round in**: there is no head, and a car turning round
at one has only the width of its own road to do it in. A leg that has to come back the way it came does it in a car park's bay (`GEN-4l`), and
a dead end with no bay off it is a place nothing that drives in can leave. **A map that wants a turning head
has to lay it** — as paved ground of its own, which the plan already carries and every reading of the ground
already answers for.

**TER-5b** `P6` An **inline junction** has exactly two arms leaving in opposite directions — a place *on* a
road rather than a place roads meet. The two carriageways must align exactly, so a driver sees
uninterrupted road. It exists to carry one pedestrian crossing and the signals that govern it, which is what
makes a lit mid-block crossing possible at all.

Two arms meeting at an **angle** are not this: that is a road that turns, rounded to a kerb radius. **A
corner is a road and a mid-block crossing is a junction**, and they look alike on a plan.

**TER-5d** `P4` **A junction is a set of connection points, and the connectors are what run between them.** Every
movement out of a lane starts at that lane's own last point and every movement into one lands on its own
first point, so a lane's line is the whole of what is driven along it and the ground past either end is the
junction's alone. **Nothing runs over a connection point**: no lane carries a spur into the box for a
movement to be drawn over, and no reader adds a figure to a lane's metres to find where they begin.

**A lane end is one point, whatever is driven off it**, and it is the connection point its arm was drawn
with (`GEN-46`): a standoff out along the arm's own line, half a lane across. **Nothing is cut back to make
room for a movement** — the road was laid to arrive on those points, so the ground past them is the
junction's and the ground before them is the lane's, and a straight and a right-angle turn out of one lane
hand over at the same place because there is only one place to hand over at.

**TER-5d.1** `P4` **The ground a movement is driven over is as wide as the narrower of the two lanes it
joins**, and that is one figure the whole town reads — the tarmac's own shape, the answer at a point and
the picture. Its two ends are on lanes that need not be the same width and a band has only one, so the
narrower is the only choice that never claims ground outside the arm it leaves or the arm it arrives on.

**TER-5d.2** `P5` **A movement is drawn the way a driver takes it: on along the lane it leaves for as long as it
can, across as late and as short as its circle allows, and on along the lane it joins** (`Spline.MovementInto`) — so
the stretch it spends across other lanes' ground is the least its two ends ask for, and no stretch of it runs
diagonally on a line of its own. **A turn is turned at the corner its two lanes' lines make**, on
`SimConfig.JunctionTurnRoomM` or the widest circle the room either side holds; **a lane carried on to an arm offset
across** runs on along its own line and shifts late, on two opposite turns of that circle; **a U-turn** is driven on
and turned across. **Only lanes carried on as one line keep the biarc between their ends** — through a place their
road only bends at, or less than half a lane across — so lanes side by side, and the paint between them, stay side
by side. **No turn is swung out of onto the lanes beside it**, however tight: a turn with too little room is given
room by its lanes ending sooner (a traced map's), and what still winds, a lane ending past where the line of the lane
it turns onto crosses its own, is the standoff's to mend and not the line's. **Two turns across each other's way off arms facing each other are made in front of each other** — a turn
to the far side and the one opposite, or two U-turns — each turned at once off its lane and last onto the next,
where turned round their corners their ground would overlap. `--bench connectors` names every one left.

**TER-5f** `P5` **No box admits a movement that reverses the direction of travel.** A pair of lanes that would
face each other across an intersection is not joined at all: no turn is classified between them, no line is
drawn, no ground is measured against it and no route may be handed one. The arithmetic is why — the line
between two opposing lanes a lane's width apart is a semicircle of a metre and a half, tighter than any
car's lock at any setback — and the consequence is deliberate: **a leg that has to come back the way it
came does it in a car park's bay (`GEN-4l`),
which are manoeuvres a driver makes and not movements a junction offers.**

**TER-5i** `P5` **A lane is one way of one road, and it is cut nowhere at all.** Its two ends are its road's
own connection points (`GEN-46`) — the exit point of the arm it sets off on and the entry point of the arm it
arrives at — so a lane is that road's line moved to its share of the carriageway and it runs the whole of it,
and no ground carries both a lane and a line drawn across it (TER-5d).

**Every movement therefore hands over at a lane's own start**, and a lane is arrived at and left and never
driven through. **What spares a driver the junctions that decide nothing is the layout and not the lane**: a
run of roads through nodes nothing meets at is one road before a lane is laid on it (`GEN-51`), and a one-way
street arrives only where the traffic it meets still has a choice (`GEN-18`). What is left holding one
movement is a corner every other movement was refused for (`GEN-48`) or an entry to a roundabout (`GEN-19`) —
both of them places a driver really is committed, and both reported by the census rather than papered over.

**TER-5j** `P5` **A movement is made from the lanes of its arm a carriageway marked with nothing else would make
it from** (`CityGen.LaneUse`): carrying straight on from any of them, turning to the near side from the kerb
lane and to the far side from the lane beside the line the two ways meet on. **Where nothing carries straight
on, the lanes are shared between the turns there are** — the kerb half to the near side, the rest to the far,
the middle lane of an odd count to both, and all of them to a turn that is the only kind on offer — so **no
lane arrives at a junction that offers it nothing**. **A marked lane makes the turns its arrows name** that the
junction offers, an unmarked one beside it what it would make unmarked, and a turn no lane of a marked arm
names is not made from it. **A turn the plan forbids is not offered at all**, before the lanes are shared,
and a turn whose lanes the plan names joins those and no others — a traced map's survey's (`GEN-57`), which
leaves an arm nothing where OSM forbids every turn off it. **A lane making a movement joins the lane of its own
number on the road it takes**, both numbered from the side the movement bears to — the kerb for the near side,
the line for the far, and the kerb for straight on — so a turn onto a road of one lane is made from the edge lane
alone, never from the second lane onto the first, and **no two movements off one arm onto one road cross**. **A
fork shares its lanes as two turns would**: the branch whose lanes set off nearer the kerb takes the kerb half,
numbered from the kerb, and the other the rest, numbered from the line, so no lane carried onto one branch crosses
the next one carried onto the other — and the ground between the branches is paved as it was when every lane
was carried onto both. **A U-turn is made from the lane beside the line onto the lane beside the line**, and from
no other. **A lane joins one lane, and one with no lane of its number there joins nothing**: of
more lanes onto fewer the ones over end at the node, and of fewer onto more the ones over are reached by none.
Nothing is merged or fanned at a node; a car gets off a lane that ends and onto one nothing reaches by
moving across (`GEN-50`). A road of one lane each way is offered every turn its junction makes, from its one lane onto
the one lane each takes. **A car moves across onto a lane beside running its way along the street**
(`CAR-53`), so the lane a turn is made from is one it can get to.

## Crossings

**TER-6** `P6` Crossings and parking are variants of the road/intersection family and need only a type tag
beyond their terrain attributes.

- A crossing is **a band of the same carriageway pedestrians may walk over**. It is an entity of its own
  (`Crossings`) and the road graph never reads it, so **a crossing adds no node and nothing can turn at one**.
- **A crossing has no width of its own.** How far it reaches is the two points it is handed (`WLK-10`) —
  the carriageway's two edges at the place it stands, read off the width of the lanes there. That one
  figure is what it is drawn, walked, stopped for and asked about at. A span carried beside it is a second answer to a question
  the walk has already answered (GEN-15), and the two disagree the first time either is laid again: a zebra
  wider than what it crosses stands its end bars on the pavement, and a narrower one leaves a strip of road
  nobody is walking over.
- The terrain carries the rule: crosswalk ground is person-allowed *and* car-allowed, and it is a stretch of
  the road it is painted across rather than a shape of its own — so the lane runs underneath it and a car on
  a crossing is still held to that lane.
- **Placement is not the road's** (`WLK-10`): a town paints a zebra wherever its kerb ends put a station
  (`CityGen.KerbEnds`) — across each end of each street at a box where three or more roads meet,
  `Road.FootNodeClearM` out along the road past the further of the street's two kerb ends, from one edge of
  the carriageway to the other. **Nothing in this slice decides it** — not how far back the arm's lanes hand
  the car over, not how much road is left behind the paint. Each is still tagged with the junction it
  approaches, so a junction's signal bundle greens *its own* arms' crossings.
- **The kerb ends hand over two answers and the road reads both** (`WLK-10a`): where the walk crosses, which
  is what is painted, and what each end of each street is held behind, which is where the bar stands and
  what the lane line stops behind. They are the same list but for a street crossed once midway between its
  ends, which is held at its two kerb ends — places carrying no paint, so the bar's setback is taken clear of
  the place itself.
- **A road end the kerb ends put no station at carries no paint**: a bay, a roundabout's circulating
  carriageway (GEN-19), and every end at a box where fewer than three
  roads meet. **Nor does the ring carry a bar**: a
  bar is where a driver holds when the junction refuses them, and circulating traffic is never refused — the
  entries hold for it and it holds for nothing, so the ring is a road with no paint on it at all.
- **Nothing refuses a band for what it lands on.** Two arms meeting at a sharp angle can lay paint over the
  corner they share. **How far one reaches past its carriageway is the census's to report** (`--bench
  census`, the crossings row) rather than a case to add here.
- **A lane line stops for paint and not for a node.** A lane line is the seam between two ribbons, and a
  node nothing turns at leaves that seam whole: the two straight movements over its ground meet along the
  line the roads either side of it hand over on, so the line runs **through the bend and the node** and on
  down the other arm, as it does along any road that turns a corner. What breaks it is the paint standing
  there, wherever the paint is.
- **A junction injected into a street is one of those** (`GEN-52`). Nothing turns there, so the roads either
  side of it are **one carriageway**, and one line runs the length of it — which is also what it is centred
  on, two pieces of line being two phases and a half dash at the node. Which roads are one carriageway is
  `CentrelineRuns`.
- **A band in the middle of a street stops nothing** (`WLK-10a`). What a line stops for is the paint at the
  end of the arm it runs into, and a street crossed once between its ends carries its zebra nowhere near
  either: trimmed to that, a run would be cut from both ends towards a band in the middle of it and there
  would be no line left. The dashes run under the stripes there and stop behind the bar at each kerb end, as
  they stop behind the bar on any other street.
- **Elsewhere a lane line stops at the outermost paint its arm carries**, which is the far edge of the bar
  at that arm's station, and where the arm carries none it stops where the carriageway does: the
  ground past a junction that forks is driven over by movements crossing one another rather than by two
  ribbons running side by side, and there is nothing there for a line to be between.
- The inline junction is the exception and takes a single crossing laid on the node itself. **Being on the
  node, it is past the end of every lane there** — a lane ends a standoff out from the node (`GEN-46`),
  further than the paint is wide — so it
  is laid across the lanes that meet at the node, each at its own end, rather than found by projecting it
  down one of them. A crossing no lane carries is paint no driver slows for and a walker no driver can see
  (TER-4c).

**A crossing with no conflicting traffic to phase against carries no lights** (TLT-3), and what governs an
uncontrolled one is the reservations alone (TER-5e): the zebra is one piece of ground (TER-5c.3), a walker
plans every way at a rung above every movement (PER-27), and a body on the paint is on the lane under it.

## Markings

**TER-6a** `P6` **A lane that holds at a bar is painted with an arrow behind it**, saying which of the
movements the junction in front offers may be taken from it. It is **one shaft down the lane's own line and
one branch off that shaft per turn the lane offers**, each branch ending in a head: near side, straight on
and far side are the same arithmetic at three angles, so **a lane offering two is one arrow with two branches
and never a glyph of its own** — there is no catalogue of shapes and no combination to author.

- **A branch is bent by its own movement's turn** — the heading the line a car is driven over that movement
  spends — and never by a right angle standing in for it, so a slip taken at thirty degrees and a square
  corner are drawn as what they are. Two movements to the same hand are one branch, bent by the sharper.
- **What the bend's radius is solved out of is the room across the lane**, and the sweep is kept whatever
  that comes to: the sharper the turn the tighter the curl, and a gentle one sweeps the whole arrow and
  reaches less far across for it. **The whole glyph stands on the lane it is painted on**, the far corner of
  every head included.
- **Every arrow is one length down its lane, whatever it says**, and **the shaft is what its branches did not
  spend reaching the end of it**: a turn advances less than a straight for the room it takes, so a lane
  offering only turns carries a longer shaft rather than a shorter arrow. A row of approaches is then paint
  of one size standing at one setback, which is the whole of why a driver reads them at a glance.
- **It is placed by the bar alone** (rule 3): a setback of clear road behind it, in the lane's own metres. An
  arm with no crossing carries no bar and no arrow either, and **a lane with no room for a whole arrow
  behind its bar carries none** rather than a shortened one, a stub of a glyph read at a glance being a
  different glyph.

Everything painted on the ground is **engine-drawn primitives, never art**: lane centrelines (dashed, and
**laid between two lane ribbons that touch and nowhere else** — so a one-way street of one lane and a bay
have nothing to part and carry none; **unbroken where it is the line two ways meet on down a carriageway of
more than one lane each way**, which nobody crosses to pass (`CityPlan.RoadArrays.LineCrossedToPass`,
CAR-6.2b); stopping behind the outermost paint an arm carries, TER-6), kerb lines (broken exactly
where the pavement's edge is, and over a car park's mouth, where the ground on the far side of the line
is the lot's own tarmac and there is no kerb to be the edge of), pavement and deck edge lines, stop bars
(square across *that arm's* direction, covering one lane only — the one driving at the paint — and
stopping at the kerb; **standing a setback clear of the crossing in front of them**, so an arm with no
crossing carries no bar and an arm the traffic only leaves on carries its crossing without one), zebras
(spanning kerb to kerb, running along the direction of the traffic that crosses them, between their bar and
the junction without overlapping the bar, or midway along a street crossed once, between the bars at its two
ends, `WLK-10a`), lane arrows (a shaft and a branch per turn, behind the bar and on the lane's own line,
TER-6a), bay strokes (**solid, and the line one bay shares with the next and nothing else** —
[GEN-4m](../../parking/docs/requirements.md), which is the same relation as the lane centreline's with the
dash the length of the run), roadside lines (**solid, down the edge a road's lanes share with its roadside**
— [GEN-57](../../../citygen/docs/requirements.md), a lane ribbon touching ground nobody drives — stopping where
the lane lines beside it stop and carried through no junction) and drift marks.

Six rules govern all of it:

1. **A coordinate is read from whatever owns it, never re-derived.** One shape, one pattern, one lane
   offset. A figure that exists in two places eventually disagrees with itself.
2. **Everything is drawn in its own frame.** A crossing on a road running north-east carries the same
   zebra as one running due east.
3. **A bar is placed by the one thing in front of it alone**, never by the junction as well, or the two
   answers differ by metres: a setback clear of the near edge of the paint at its own arm, and — where the
   paint went to the middle of the street (`WLK-10a`) — a setback clear of the end of that arm's kerb, which
   carries no paint and so no edge to step in from.
4. **Every run of marks is centred on the stretch it is on**, so a dashed line does not begin with a half
   dash.
5. **Paint sits on the surface it belongs to**, checked on rendered frames because no numeric check
   answers it (VER-9).
6. **A mark laid along a road is laid on the road's own curve**, never on the chord of it. A dash struck
   straight across a bend stands its own sag off the line it marks, which at the radius a town's tightest
   bends are laid to is most of a line's width — the line reads as a row of tangents rather than a curve.

## What this slice must produce

- **Directed lanes and the connectors between them, and no node table.** A lane runs between its road's two
  connection points (TER-5i, `GEN-46`) and is cut nowhere.
  **Where two lane ends are the same ground is worked out from the connectors** — a
  connector runs between them, or they are the two ends of one stretch driven either way — so a junction is
  the shape a set of crossed lanes makes and is nothing the network carries. **Worked out once and handed to
  whoever needs it**, the router included (SIM-7): derived twice, the router and the claims would be entitled
  to disagree about which lane ends are one piece of the world.
- **The plan's junctions, and the lanes each of them lowered into**, for the two slices whose subject *is*
  an intersection: the signals a bundle governs (`TLT-1`) and the paint laid on an arm (TER-6). Nothing
  that drives, routes or claims may reach it.
- A turn on every connector — straight / near-side / far-side — **filled once when the town is laid** and
  read off thereafter, and **no connector at all between a lane and the one running back down its own
  stretch** (TER-5f). Which turn a connector makes is a fact about the road, not about the car on it.
- A crossing registry queryable by junction, and a stop-line registry carrying the bars actually painted.
- **A ribbon atlas over every way of the town** (TER-4c.4), laid once from the lines and widths each network
  hands over as data: which ways a collider stands over, and the marks — every pair of ways whose ribbons share
  ground, in both ways' own metres (TER-5c). **There is no register of who is inside a junction**: the marks
  place a plan's secondary claims as it is laid, and the answer comes off the reservations everything else
  reads (TER-5c.1).
- **The reservations over every way of the town in one numbering** (TER-4c.2) — the lanes, the connectors
  between them, the ways a slice above lays off them and the pavement's — carrying every body where it is and
  every plan where it means to be, so that **who is in front and how much road is whose** are answered from
  the town's own reservations rather than from geometry (`S-2a`). **Ways are told apart by the kind of ground
  each is and never by which kind of body is standing on it.**
