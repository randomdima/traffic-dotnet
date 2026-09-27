# The debug layers — requirements

What a debug session can be opened for, and what it may cost. **This slice owns the switches as well as
the layers, and the sections they are shown in** — the panel that draws a switch is
[app/hud](../../hud/docs/requirements.md)'s, and it reads them from here rather than the layers reaching back
into the panel. The frame read-out is not one of these:
it is furniture in the corner and is [app/hud](../../hud/docs/requirements.md)'s. What everything is drawn
with is [app/screen](../../screen/docs/requirements.md). How each layer draws what it draws, and why, is on
its own type (`DebugOverlay` and its parts, `PathMarks`, `MarkClaims`, `ShellProbe`).

**The overlay reads the *producers*, never a copy of the shape.** When it draws a junction movement or a
run of pavement it calls the same code the agents use; a second copy of a shape eventually disagrees with
the first, and then the layer is the thing that lies about the town. **And it draws geometry, in the town,
and not text**: a figure that can be drawn where it happens is never written into a label instead.

**OBS-2b** `P8` **The debug overlay is instrumentation and is priced as such**: it is **off by default**, what
it costs to draw is measured on the same footing as what it measures, and **nothing is drawn about a body
that is not on screen**. It binds the frame read-out too, which is furniture and cannot be switched off:
what it costs to gather is not paid while its body is shut
([app/hud](../../hud/docs/requirements.md#the-status-panel)).

**OBS-2c** `P8` **Each thing a debug session can be opened for has a switch of its own, and no switch turns on
anything a second one owns.** Thirteen of them, and **the ground's own layers are not among them** (OBS-2v):
those take the town apart rather than drawing anything over it, and they start on. **A layer covers one kind
of body entirely** — its geometry and its
manoeuvre alike — because the question is about the body, not about the kind of mark; and **what belongs
to the *town* rather than to a body is not switched with a body at all**.

**OBS-2y** `P8` **The switches are shown a section a thing a session is opened to look at, and one section at
a time** ([`DebugLayers`](../DebugLayers.cs)). A section carries every switch and every figure that question
turns — the road's trims beside the car layers they are felt in, the probe's two figures beside the boundary
it is struck off, the ground's own layers (OBS-2v) beside the wireframe drawn over them — so what is on the
glass at once is one question's worth of rows and never every switch the slice owns.

- **Every switch is in exactly one section**: in two it is one switch with two rows, in none a switch nobody
  can reach.
- **A switch says what it draws and what each of its colours means**, on its own row: a key kept anywhere
  else is a key nobody has open while looking at the town.
- **A section says how many of its layers are on without being opened**, which is how a layer still drawing
  over the town is found after the reader has moved on to another section; and **one row turns every layer
  off at once**.
- **A section can be photographed open**: `--ui menu-<section>`.

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

**OBS-2j** `P8` **The one layer that computes rather than reads is the turn circle, and it says so.** Every
other mark here is read off whatever produced it; **there is no producer for this one** — nothing in the
simulation ever works out a centre of rotation, because a car turns by four contact patches spending four
impulses. So the layer works the geometry out itself, from the axles under **that** body and the angle its
own front wheels are actually at, and draws **the construction and not only the answer**: the centre where
the rear axle's line crosses a front wheel's, a spoke to each of the patches that fixed it, and the arc of
**the nearest rear wheel** — the wheel whose track is on the ground beside it, since the reading is a drawn
circle laid over a written one and two circles half a track apart cannot be compared by eye. Where the
wheels are straight there is no centre to draw and nothing is drawn.

**OBS-2o** `P8` **The one layer that is about the picture rather than about the town is the ground's own
triangulation**, drawn as the edges the mesh has. It is the only way to look at the thing
[TER-7](../../../world/terrain/docs/requirements.md) says must be invisible: every surface is textured
from the world origin so that cutting a shape into triangles differently does not change the picture — and
a shape cut wrong therefore draws exactly like one cut right, until something else in the frame disagrees
with it.

**OBS-2v** `P8` **The ground is a stack of layers, and each layer is a switch and a reading.** Every layer
the mesh is laid in — the grass, the walk and its kerb, the water, the decks, the carriageway, the slabs,
the town's kerb and the paint — can be taken out of the picture on its own, and each says what it came to:
the triangles it is, the corners it brought that no earlier layer had already stood at, and how long it took
to cut. Under them the same for the whole mesh, and how much of that time went on the boundary every layer
is struck off rather than on any layer of it.

- **These switches start on, where every switch in OBS-2c starts off**: they are the town, not
  instrumentation drawn over it.
- **A layer is taken out of the draw and never out of the mesh**, so no figure on the page moves when one is
  thrown and putting it back costs nothing to lay.
- **Every figure is the mesh's own**; the page measures nothing, and a report printing the same table reads
  the same tallies.

**OBS-2p** `P8` **The outside of the town's driven ground is a layer**: every lane, every movement through a
box and every way into a bay taken as the ribbon of ground it covers, all of them merged into one shape, and
the boundary of that shape drawn ([`LaneShell`](../../../citygen/LaneShell.cs)). **The boundary of an area
and never a stretch of a line.** The perimeter of a plain road is the outer edge of its outer lanes, and at a
junction it is whatever piece of whichever band reaches past the rest — so what is drawn stands half a band
off the orange lines under it rather than on top of them, and a layer marking the lines instead says which
lines are outermost where the question is where the ground stops.

**Every ring that shuts is drawn** — the one round the town and the one round every block it encloses, both
being the edge of the driven ground — and a run that would not close is drawn as the fault it is.

**OBS-2u** `P8` **The layers struck off the town's boundary are drawn beside it**: every one the picture is
laid from ([`GroundRings`](../../../citygen/GroundRings.cs)), each as **an outline like any other the layer
draws** (OBS-2p) — its own line, its own normals, and the same stretch under the pointer (OBS-2t) — in a
colour that is neither the boundary's nor a shade of it. They are drawn under the perimeter's own switch and
not one of their own, because the reading is the pair and there is nothing to be read from a layer's edge
alone. **Neither kerb is among them**: a kerb is a line at a width and not a shape struck off one (TER-3d),
and both lines are already drawn — the town's kerb is the boundary itself and the walk's own is the outer
edge of the one layer, so drawing either would be a line drawn a second time in another colour.

**OBS-2w** `P8` **A debug session may strike one shape of its own off that boundary, at figures it turns.**
It is a layer of its own with figures of its own (OBS-2c): the switch says whether it is drawn, **the
distance says how far out** — from nothing to a distance several streets wide — and **the rounding says how
tightly the shape struck is allowed to turn anywhere**, as a radius in metres on a track of its own. They are **the figures
on the panel that change what is drawn rather than what the town does**: nothing is laid again for them and
no body moves. What comes back is an outline like any other the layer draws (OBS-2p, OBS-2u), in a colour
that is neither the boundary's nor a shipped layer's, because the whole reading is which of the lines on the
glass the town was actually laid with.

**A picture can be asked for it without a hand on the slider**: `--ui shell-<metres>`, and
`--ui shell-<metres>-<metres>` for the distance and the radius.

**OBS-2s** `P8` **The driven ground itself is a layer beside its outside**: the same lanes, movements and
bay ways taken as the ribbons of ground they cover (OBS-2p), each drawn whole at its own line's width —
**the area and not the edge of it**.

**OBS-2t** `P8` **Where a layer draws everything at once, the pointer asks it about one thing, and a click
pins it.** The one thing is found in the layers that are on, and **drawn over is found first** — a body,
then a stretch of boundary, then a way, then a ribbon, then a cell — so a thin thing is never hidden behind a
broad one that covers it. It is drawn picked out, and what it is, is written on a card beside the pointer:

- **a body** — its outline and its own two pieces of route at the picked weight (OBS-2h), and what it is
  doing, how fast, what it sees ahead and where it must stop;
- **a way**, under the claims or the nodes — the whole of it, and what it is, how long and wide, and with the
  claims on **every stretch of it somebody holds**, whose and how strong, marked where it covers the pointer;
- **a driven line's ribbon** (OBS-2s) — the ribbon it lays, and its length, width and sections;
- **a stretch of boundary** (OBS-2p) — that stretch alone with a dot at each end, and the outline it is a
  stretch of; every outline the layer draws is searched and the nearest of all of them wins;
- **a cell** of the geometry grid (OBS-2r) — its square and every line the index holds in it — or of the
  solver's (OBS-2x), both grids' cells over the place and what each holds.

**The words are on the card and nowhere on the town**: a layer is lines until somebody points. **A click on
the town pins what is under it** — its card docked under the corner buttons, and it stays picked out — and
**a click on nothing a layer draws lets it go**. The click still selects the unit under it: **no layer takes
the mouse**, since a reading the reader asked for is not a mode they have to leave before they can pick a
car. A pin is dropped when every layer that could find it is off.

**OBS-2r** `P8` **The grid the town's geometry is asked over is a layer**: the cells of the index a
question about which line is where is narrowed with ([`ChainIndex`](../../../core/geometry/ChainIndex.cs)),
drawn where they fall, with a wash in each cell that says how many lines it holds.

**OBS-2x** `P8` **The grid the solver asks is a layer beside it**: the cells of the two indexes its broad
phase is narrowed with ([`CellGrid`](../../../world/physics/CellGrid.cs)) — the town's furniture, and the
roster the last step integrated — drawn where they fall, with a wash in each cell that says how many bodies
it holds.

**It is read as it stands and never brought up to date for the picture**: reindexing to draw rewrites the
integrated-body count the panel beside it is reading, and a lattice one step old is what that step was
priced on.
