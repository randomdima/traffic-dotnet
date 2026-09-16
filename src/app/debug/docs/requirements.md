# The debug layers — requirements

What a debug session can be opened for, and what it may cost. **This slice owns the switches as well as
the layers** — the panel that draws a switch is [app/hud](../../hud/docs/requirements.md)'s, and it reads
them from here rather than the layers reaching back into the panel. The frame read-out is not one of these:
it is furniture in the corner and is [app/hud](../../hud/docs/requirements.md)'s. What everything is drawn
with is [app/screen](../../screen/docs/requirements.md).

**OBS-2b** `P8` **The debug overlay is instrumentation and is priced as such**: it is **off by default**, what
it costs to draw is measured on the same footing as what it measures, and **nothing is drawn about a body
that is not on screen**. It binds the frame read-out too, which is furniture and cannot be switched off:
what it costs to gather is not paid while its body is shut
([app/hud](../../hud/docs/requirements.md#the-status-panel)).

**OBS-2c** `P8` **Each thing a debug session can be opened for has a switch of its own, and no switch turns on
anything a second one owns.** Eleven of them, and **the ground's own layers are not among them** (OBS-2v):
those take the town apart rather than drawing anything over it, and they start on. **A layer covers one kind
of body entirely** — its geometry and its
manoeuvre alike — because the question is about the body, not about the kind of mark; and **what belongs
to the *town* rather than to a body is not switched with a body at all**.

**OBS-2d** `P8` **Between them the layers leave nothing out.** Everything that acts on an agent while it moves
is drawn by one of them: the ways it may travel, the movements a junction allows it, the nodes it plans
over, the manoeuvre it is executing **with every place that manoeuvre owns**, the stretch of road the
town's own claims say it is on or has taken, and the collision shape the solver gives it.

**And a block always stands on a line.** A claim names a way, so **every way either network can hold one
of is a line the nodes layer draws, whole** — a lane over the whole of its own line and not only the
stretch a route travels it, since a body inside the setback at either end is written onto it there. A way
with no line under it cannot be read at all: the block is the only thing on screen, and what it says about
where its holder is standing cannot be checked against anything.

**OBS-2h** `P8` **An agent layer draws the action the agent is taking, and not the plan behind it.** What is
drawn for a body is **two pieces of its own line**: the one it is on and the one it has planned to take off
the end of it — the rest of this lane and the junction off it, the junction being crossed and the lane it
lands on, the pavement and the crossing at the kerb, the lane and the bay template that takes the car off
it. One piece leaves out the decision the agent is about to act on and three are the route again, and
the route past the second piece belongs to the nodes layer or to no layer at all.

**A car and a walker are the same picture**: one chevronned line at one weight, a dot where two pieces
meet, a dot where the drawing stops. Nothing about a body is drawn a second time in a second style —
a junction a car holds is the ground of the join its route already runs through.

**The marks down a line stand where it crosses a grid laid over the town, not on a comb laid along the
line.** One falls wherever the line passes a whole number of pitches from the world origin, across or
down, so **where a mark falls is a fact about the ground the line crosses and about nothing the line
itself is** — not where it was cut, not how much of it is being drawn, not which bend it came out of.
Two lines over the same ground therefore carry the same marks: the lanes of one carriageway mark one line
of the grid across it, a movement running along a lane stands its marks on the lane's, and an agent's own
line lands on the stones the network layer under it already drew. Marks laid off a line's own start are a
picture of where the lines were cut instead — and a chain of short pieces, which is what a roundabout ring
and every junction join are, comes out with a mark on every piece however short, because each of them is a
start. **What the bearing decides is how far apart they are, and that is the price**: a line square to the
grid crosses one family of its lines and is marked every pitch, one at forty-five degrees crosses both and
is marked about seven tenths of that, and a line through a corner of the grid is marked once rather than
twice within a stroke.

**And the grid says where a mark may fall, not that every line crossing a stone carries one.** No two marks
saying the same thing stand within a metre of each other, over the whole of a pass and across both networks
([`MarkClaims`](../MarkClaims.cs)) — the movements leaving one lane end all set off from one point, so a
junction otherwise comes out with a chevron for every arm piled on one stone and a reader cannot tell a fan
from a blot. **Two marks saying opposite things are not a blot**: a pair of lines over one piece of ground
is read by its chevrons crossing, so a stone is refused only to a mark saying what the mark already
standing there says.

**A mark says direction only where the ground has one.** Where the two directions of a stretch are laid
on one line — the ground had no room for a lane either side, which is every crossing and every pavement
too narrow for two ([`WalkingNetwork.LaneOffsetM`](../../../world/foot/WalkingNetwork.cs)), and every
bay's way, which is a car's width driven in over and backed out over
([`RoadGraph.LaneOverOneLine`](../../../world/road/RoadGraph.cs), GEN-4f) — the mark is
a bar square across the line instead of a chevron. Chevronned, the ground carries two rows of opposing
arrows on the same stones, which reads as a fault in the picture rather than as a stretch walked both
ways. **And ground a body covers backwards takes a shade of its own network's colour**, never a colour of
its own: the way out of a bay and the way in converge on the one rear axle, so over the last metres they
are a stroke apart, and in one colour they read as a single line whose chevrons cross.

**The pieces are the agent's own geometry, cut at the agent's own boundaries**: the layer reads the line
and the lane spans the assembler wrote ([`CarFleet.LaneStartsOf`](../../../agents/car/body/CarFleet.cs)),
and the walked points and the crossing each stands on, and it computes neither.

**A reading has to be drawn as well as a shape.** Whether the car in front counts as a queue, as something
to get past or as ground somebody is about to take is the one thing about a driver that has no shape of
its own — so the town's claims ([`LaneOccupancy`](../../../world/road/LaneOccupancy.cs)) are drawn as the
stretches they are: **a washed-out block of the way, at that way's full width, curving with the ground
under it**. **Every stretch a body holds takes that body's own colour, on whatever kind of way it is claimed
and whether it is standing on the ground, was granted it or has only claimed it** — a walker's band of a lane
and its stretch of the pavement are one person's ground, so a block can be followed from the pavement onto
the road that body is crossing and a junction says which of the cars in it holds what. What needs a colour
of its own is what belongs to no body at all: the town's own furniture.

**The colour is whose the ground is and the wash is how strong the hold on it is**
([`ClaimPriority`](../../../world/road/ClaimPriority.cs)), each saying one thing and neither saying the
other. The strongest hold there is — a body, and road a body can no longer give back — is drawn at half,
and the weakest — road a driver has only stated it means to use — is drawn at a tenth, off the ladder's own
numbers rather than a table of the layer's. Everything stays transparent enough to read the tarmac, the
paint and the body through: a block says which ground is spoken for, and hiding the ground to say it defeats
the point. Without the shade a busy junction is a single band of overlapping asks with nothing to say which
of them anybody would give up, which is the reading the layer is opened there for.

**And the pieces of one hold are told apart by a bar across the ends of each.** A body's ground is regularly
several stretches at one strength — a lane, the join after it, the ground beyond its own road it has
committed to — and they butt exactly, so under that one wash the joints are invisible and the reading *how
far does this go* cannot be taken. So the edge is a thin bar square across the way at either end of every
stretch, in its own block's colour and wash drawn up rather than down.

**And the one hold that is not a stretch of way is drawn as what it is.** A bay a car is standing in or has
claimed is a place in a register (`GEN-4g`) and not ground on either network, and a bay's own two ways are drawn
to the rear axle and stop there — so a block on one of them can only ever cover the ground behind that axle,
with the car's whole nose past the end of it. The bay is drawn as the bay, in that body's colour like
everything else here: **washed where a body is standing in it, outlined where a leg has only claimed it**.
Those two are different claims — somebody is standing here, against somebody is on their way and nobody else
may take it — and not two shades of one.

**The blocks are a layer of their own and belong to neither kind of body** (OBS-2c). A claim is a
fact about the *ground*: what cuts a driver's grant is as often a walker standing in the lane as another
car, and what holds a body at the edge of a zebra is a car's stretch of the lane under the paint. Held
under the car layer, neither of those could be seen without the car switch on — which is the reading the
block exists for. **And a layer of their own rather than the nodes layer's**, though both are the town's rather than a
body's: the graphs are the ground the town was laid with and never move once it is laid, the claims are
what this tick did to that ground and are re-laid from the bodies every frame, and switched together each
reading came with the other drawn through it. Where both are on the blocks go **over** the graphs, so a
chevron punching through a claim cannot read as the lane still being open.

**A width drawn is a width the model holds.** The block is one lane wide because
[`RoadGraph.LaneWidthM`](../../../world/road/RoadGraph.cs) is one lane wide: half the carriageway the town
file declared, and twice the distance the lane's own line was offset by. The same number is what the
follower is held to a quarter of, where the pavement band starts, and what the tarmac is laid to — a layer
that picked its own figure would be a picture arguing with the town. **On the pavement it is the offset
that stretch was actually laid at** ([`WalkingNetwork.LaneOffsetM`](../../../world/foot/WalkingNetwork.cs))
and never the figure the config asked for: a stretch too tight for a full lane is walked at whatever fits
it, and drawn at the shipped number its two directions overlap where the town has them side by side.

**Geometry, in the town, and not text**: a figure that can be drawn where it happens is never written
into a label instead.

**The overlay reads the *producers*, never a copy of the shape.** When it draws a junction movement or a
run of pavement it calls the same routing code the agents use. A second copy of a shape eventually
disagrees with the first — and when it does, the layer is the thing that lies about the town.

**OBS-2j** `P8` **The one layer that computes rather than reads is the turn circle, and it says so.** Every
other mark here is read off whatever produced it; **there is no producer for this one** — nothing in the
simulation ever works out a centre of rotation, because a car turns by four contact patches spending four
impulses. So the layer works the geometry out itself, from the axles under **that** body and the angle its
own front wheels are actually at, and draws **the construction and not only the answer**: the centre where
the rear axle's line crosses a front wheel's, a spoke to each of the patches that fixed it, and the arc of
**the nearest rear wheel** — the wheel whose track is on the ground beside it, since the reading is a drawn
circle laid over a written one and two circles half a track apart cannot be compared by eye. Where the
wheels are straight there is no centre to draw and nothing is drawn.

**It is a prediction and the only one this overlay makes**, which is what makes it worth a switch of its
own rather than a mark on the car layer: that one draws what the world did to a driver, and the daylight
between this circle and the tracks under it is the whole of what the skidpad exists to show
([citygen](../../../citygen/docs/requirements.md#where-a-town-comes-from)).

**OBS-2o** `P8` **The one layer that is about the picture rather than about the town is the ground's own
triangulation**, drawn as the edges the mesh has. It is the only way to look at the thing
[TER-7](../../../world/terrain/docs/requirements.md) says must be invisible: every surface is textured
from the world origin so that cutting a shape into triangles differently does not change the picture — and
a shape cut wrong therefore draws exactly like one cut right, until something else in the frame disagrees
with it.

**It reads the mesh the renderer was handed** (`GroundMesh`) and lays no triangulation of its own, which is
the same rule the rest of this slice is written to: an outline drawn round a shape nothing on screen was
cut from is a picture of the layer.

**Every triangle that is being drawn, and not a class of them** — the ground, the kerb rims, the dashes and
the zebra stripes alike. The mesh knows where the paint starts (`GroundMesh.FirstMarkVertex`) and this layer
does not ask, because a wireframe over a subset of what is on the glass is a picture of the filter.

**And a layer the ground page has taken out of the picture is out of this too** (OBS-2v). That is not a
filter over the mesh: the renderer is drawing those triangles or it is not, and a net over ground nobody can
see would be this layer arguing with the picture it is a reading of.

**A triangle smaller than a few pixels on the glass is not drawn.** Under that a mesh is a wash rather than
a wireframe: nothing about where the cuts fell can be read out of it, and at a town-wide framing it would
cost the whole buffer to say so. Pulling the camera back thins the picture out instead of filling it, which
is also the honest answer to whether the question can be asked from here.

**It is laid with the town's own graphs, and last of them.** The mesh does not move once the town is laid,
so it is cached and re-emitted on the same terms the nodes layer is — and laid after that layer, because a
city's triangulation is more quads than the cache holds at any framing that admits it, and laid first it
would leave a switch on beside it drawing nothing.

**OBS-2v** `P8` **The ground is a stack of layers, and each layer is a switch and a reading.** Every layer
the mesh is laid in — the grass, the walk and its kerb, the water, the decks, the carriageway, the slabs,
the town's kerb and the paint — can be taken out of the picture on its own, and each says what it came to:
the triangles it is, the corners it brought that no earlier layer had already stood at, and how long it took
to cut. Under them the same for the whole mesh, and how much of that time went on the boundary every layer
is struck off rather than on any layer of it.

- **These switches start on, where every switch in OBS-2c starts off.** They are not instrumentation drawn
  over the town — they are the town, so a run that has asked for nothing draws the whole of it.
- **A layer is taken out of the draw and never out of the mesh.** The triangles are cut once, when the town
  stands; a switch shortens what is drawn out of them. So no figure on the page moves when one is thrown,
  putting a layer back costs nothing to lay, and a reading is of the town rather than of what is showing.
- **Every figure is the mesh's own.** This page measures nothing and remembers nothing, so what it says and
  what the renderer draws cannot come apart — and a report that prints the same table reads the same
  tallies rather than taking the measurement again.
- **What it is opened for is where the ground goes.** Which layer is most of the triangles, which is most of
  the milliseconds, and whether the two are the same layer — a question about the picture rather than about
  the town, like the triangulation it stands beside (OBS-2o).

**OBS-2p** `P8` **The outside of the town's driven ground is a layer**: every lane, every movement through a
box and every way into a bay taken as the ribbon of ground it covers, all of them merged into one shape, and
the boundary of that shape drawn ([`LaneShell`](../../../citygen/LaneShell.cs)). **The boundary of an area
and never a stretch of a line.** The perimeter of a plain road is the outer edge of its outer lanes, and at a
junction it is whatever piece of whichever band reaches past the rest — so what is drawn stands half a band
off the orange lines under it rather than on top of them, and a layer marking the lines instead says which
lines are outermost where the question is where the ground stops.

**And it is read, not worked out here** ([`LaneShell`](../../../citygen/LaneShell.cs)). Where the driven
ground stops is a fact about the ground; a layer that derived its own copy would be the second answer that
disagrees with the first.

**A perimeter is a continuous line, and a picture of one in pieces is a picture of a fault.** Every gap in
it is a metre of the town's edge nothing accounts for, so a break in this layer is the reading it is opened
for — and it must be the town's break rather than the drawing's.

**In a colour that is the driving network's opposite**, since the reading is taken against the very lines
the nodes layer draws underneath: a shade of the same hue says these are more lanes rather than where the
ground those lanes lay comes to an end.

**One solid line per ring, and the normals beside it.** Anything drawn *along* the line could be read as a
break in it, so what stands beside it is another matter: an arrow every few metres, square off the line and
turned to the side the merge believes is ground, leaves the line whole and answers the question a line alone
cannot — **which side of it the shape is on**. A ring is walked with the driven ground on its right
throughout (TER-3c.9), so a run of normals turned out at the grass is a corner that came out the wrong way
round, and the boundary drawn there is as wrong as it looks however continuous it is.

**Every outline the layer draws is drawn that one way, and read that one way.** The rings the merge closed,
the runs it could not, and the layers struck off the town (OBS-2u) are each a set of chains walked with their
own ground on their right, so each is that line and those normals — **and what tells them apart is a colour
and nothing else**. The pointer searches all of them alike and names the one it found (OBS-2t). A layer
whose first outline had the line, the normals and the pointer while the rest had a stroke would be saying
that the others are decoration; they are the same answer about a different shape, and the next one added is
a colour and a line of code.

**And every one of them is culled by the stretch and not by the chain.** A ring's two ends stand at one
place and it reaches half its own length in every direction, so a cull asked of the chain keeps every ring
at every framing — and a city's outer ring is more stretches than the cache holds, which leaves the layer
drawing its first outline and nothing else. That is not a budget to be raised: what is on the glass at a
street framing is a dozen stretches, and a layer that draws a hundred thousand to show them is answering a
question about the town rather than about the view (OBS-2b).

**And the runs that would not close are drawn as what they are.** The boundary of a union of closed bands is
closed, so a run with two ends is a crossing the merge did not find rather than a shape the town has: it is
drawn in the fault colour and never in the boundary's, because a fault drawn as an answer is a fault nobody
looks for. **What an open run adds is its two ends** — a disc at each — which is exactly what a ring has not
got and is not a second way of drawing it. This layer is an instrument, and **what is then fixed is the
geometry**.

**Every side of the ground is the outside of it, so every ring that shuts is drawn.** A street grid bounds
what it lays on the outside and round every block it encloses, and both of those are the edge of the driven
ground. Drawn from the outermost ring alone, every block in the town comes back with no edge at all, which is
half an answer to the question the layer is opened for.

**OBS-2u** `P8` **The layers struck off the town's boundary are drawn beside it**: every one the picture is
laid from ([`GroundRings`](../../../citygen/GroundRings.cs)), each as **an outline like any other the layer
draws** (OBS-2p) — its own line, its own normals, and the same stretch under the pointer (OBS-2t) — in a
colour that is neither the boundary's nor a shade of it. They are drawn under the perimeter's own switch and
not one of their own, because the reading is the pair and there is nothing to be read from a layer's edge
alone. **Neither kerb is among them**: a kerb is a line at a width and not a shape struck off one (TER-3d),
and both lines are already drawn — the town's kerb is the boundary itself and the walk's own is the outer
edge of the one layer, so drawing either would be a line drawn a second time in another colour.

**The town's own layers and never a set struck here.** What a reader checks is that the red line holds one
distance off the blue everywhere and turns where the blue turns — of the ground the town is actually
standing on. A layer striking its own offset draws a shape nobody stands on, and one with a figure somebody
can drag answers a different question — how the shape moves — which is a question for the construction's own
tests.

**What is drawn is the boundary of the moved shape and not the moved lines.** A feature narrower than the
distance is not in the picture at all: the outset of a notch two metres across is no notch, and the outset
of a ring smaller than the distance is nothing. **So the red line having fewer corners than the blue is the
answer and not a fault** — and a red bow-tie anywhere is one, which is the second reading the pair is for.
The runs the construction could not close are drawn in the fault colour like any other (OBS-2p).

**Last of everything the layer draws, so it is first to give way.** The town's graphs are laid into a cache
that truncates rather than slows (OBS-2b), and what a boundary layer is opened for is the boundary
(OBS-2p): at a framing where there are not quads enough for both, the example is the half that stops.

**OBS-2s** `P8` **The driven ground itself is a layer beside its outside**: the same lanes, movements and
bay ways taken as the ribbons of ground they cover (OBS-2p), each drawn whole at its own line's width —
**the area and not the edge of it**.

**It draws what the merge is given, and the boundary layer draws what the merge made of it.** Read together
they are the one reading this pair exists for: a boundary that does not follow the outside of the bands
under it is a merge fault, and a picture of the boundary alone cannot show it — the line looks as continuous
where it is wrong as where it is right.

**A wash and not a fill**, and under everything else the overlay draws. It covers whole streets at once, so
at the weight a line is drawn at there is nothing left on top of it to read the ground against; and the
bands are drawn over one another rather than merged here, so where two ribbons cover the same ground the
wash deepens and says which ones.

**OBS-2t** `P8` **Where a layer draws everything at once, the pointer asks it about one thing.** Three
readings, each drawn only while the layer it is about is on: **the driven line under the pointer**, as the
whole ribbon it lays and the line of that ribbon alone (OBS-2s); **the stretch of boundary
under the pointer**, as that stretch alone with a dot at each of its ends and the outline it is a stretch of
named beside it — every outline the layer draws is searched and the nearest of all of them wins (OBS-2p);
and **the cell of the
geometry grid that was clicked**, as its own square and every line the index holds in it (OBS-2r).

**What is picked out is drawn heavier and in one colour, and what it is is written beside the pointer** —
the numbering the thing is held under, and the figures that number stands for. A layer that draws four
thousand lines answers "which of these" with a colour nobody can point at; the reading is the pointer's, so
it is written where the pointer is rather than on the town, which is the picture it is a reading of.

**And while the grid is on, where the pointer stands is written in the corner, in the town's own metres.**
It is the one reading not put beside the cursor: a coordinate is read while looking somewhere else — written
down, typed into a command line, checked against a figure in a log — so it goes where the eye can return to
it, above the scale bar that already owns that corner for saying how big things are (`OBS-2e`). It is the
grid's because a lattice is the one layer read in coordinates: every other layer draws a thing to look at,
and this one draws where the things are.

**A cell is picked by clicking it, and the grid takes the mouse while it is ticked** — a left click picks,
a right click puts it back, and the ruler is offered the click first (`OBS-2f`). **The pick is a place and
not a cell number**: the lattice is the index's own and it is snapped to the map and grown where a set is
spread too far, so a pick held as a number would be a pick on whichever lattice was current when it was
made.

**The cell shows whole lines and not the part of each inside it.** What a cell answers is which lines a
question asked there is narrowed to, and a line is a candidate in its entirety however little of it reaches
the cell — so lighting only the part inside would be a picture of the cell rather than of its answer.

**OBS-2r** `P8` **The grid the town's geometry is asked over is a layer**: the cells of the index a
question about which line is where is narrowed with ([`ChainIndex`](../../../core/geometry/ChainIndex.cs)),
drawn where they fall, with a wash in each cell that says how many lines it holds.

**It draws the index of every driven line and not of some set of them** (`Paving.DrivenLines`). Pointed at
the lanes' own index instead, the layer drew a car park whose cells were empty beside the bays' own ways —
a picture of that index's subject read as a picture of the grid, and the empty cell was the layer's rather
than the town's. A cell says how many lines are binned into it, so what it is a cell of has to be everything
the town is driven along.

**It is the index's own lattice and never one laid again for the picture.** The grid snaps its origin to a
whole cell and grows its cell where a set is spread further than the one it was asked for can cover, so a
lattice drawn from the cell size and the town's corner is a picture of a grid nothing is asked over — and it
agrees with the real one right up until the day it does not. This is the same rule the rest of the overlay
keeps: read the producer, never a copy of its shape.

**What it is opened for is where the lines crowd**, because that is where a query pays and where the cell
size is worth arguing about: a junction with a movement per pair of arms over it, a car park whose every bay
reaches into one cell. So the wash is scaled to the busiest cell **in the frame** rather than to a figure —
crowding is a comparison, and a fixed scale reads as dark everywhere over a city and empty everywhere over
a street.

**Under a cell that can be told from its neighbour it is not drawn.** A ruling finer than the thing it is
ruled over says nothing about which cell anything is in, and at a town-wide framing a city's cells are a
wash that costs the whole buffer to say so. Pulling the camera back thins this layer out; pulling it in is
what it is read at.

## Two performance rules this layer taught

- **A cull that admits a body is not a cull that admits its whole line.** Find the visible stretch of a
  line coarsely before sampling it finely.
- **Split a drawing item by what can change and by where it is.** The town's own graphs do not move once
  the town is laid; re-emitting them every tick for the bodies' sake was the most expensive thing in the
  frame at a district framing. Draw them when the zoom changes, when the window leaves the stretch that
  was drawn, or when a switch does.
