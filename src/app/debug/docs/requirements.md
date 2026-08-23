# The debug layers — requirements

What a debug session can be opened for, and what it may cost. **This slice owns the switches and the
read-out as well as the layers** — the panel that draws a switch is [app/hud](../../hud/docs/requirements.md)'s,
and it reads them from here rather than the layers reaching back into the panel. What everything is
drawn with is [app/screen](../../screen/docs/requirements.md).

**OBS-2b** **The debug overlay is instrumentation and is priced as such**: it is **off by default**, what
it costs to draw is measured on the same footing as what it measures, and **nothing is drawn about a body
that is not on screen**.

**OBS-2c** **Each thing a debug session can be opened for has a switch of its own, and no switch turns on
anything a second one owns.** **A layer covers one kind of body entirely** — its geometry and its
manoeuvre alike — because the question is about the body, not about the kind of mark; and **what belongs
to the *town* rather than to a body is not switched with a body at all**.

**OBS-2d** **Between them the layers leave nothing out.** Everything that acts on an agent while it moves
is drawn by one of them: the ways it may travel, the movements a junction allows it, the nodes it plans
over, the manoeuvre it is executing **with every place that manoeuvre owns**, the stretch of road the
town's own book says it is on or has reserved, and the collision shape the solver gives it.

**OBS-2h** **An agent layer draws the action the agent is taking, and not the plan behind it.** What is
drawn for a body is **two pieces of its own line**: the one it is on and the one it has planned to take off
the end of it — the rest of this lane and the junction off it, the junction being crossed and the lane it
lands on, the pavement and the crossing at the kerb, the lane and the bay template that takes the car off
it. One piece leaves out the decision the agent is about to act on and three are the route again, and
the route past the second piece belongs to the nodes layer or to no layer at all.

**A car and a walker are the same picture**: one chevronned line at one weight, a dot where two pieces
meet, a dot where the drawing stops. Nothing about a body is drawn a second time in a second style —
a junction a car holds is the ground of the join its route already runs through.

**The pieces are the agent's own geometry, cut at the agent's own boundaries**: the layer reads the line
and the lane spans the assembler wrote ([`CarFleet.LaneStartsOf`](../../../agents/car/body/CarFleet.cs)),
and the walked points and the crossing each stands on, and it computes neither.

**A reading has to be drawn as well as a shape.** Whether the car in front counts as a queue, as something
to get past or as ground somebody is about to take is the one thing about a driver that has no shape of
its own — so both books ([`LaneOccupancy`](../../../world/road/LaneOccupancy.cs)) are drawn as the
stretches they are: **a washed-out block of the way, at that way's full width, curving with the ground
under it**. **Every stretch a body holds takes that body's own colour, in whichever book it is written and
whether it is standing on the ground, was granted it or has only claimed it** — a walker's band of a lane
and its stretch of the pavement are one person's ground, so a block can be followed from the pavement onto
the road that body is crossing and a junction says which of the cars in it holds what. What needs a colour
of its own is what belongs to no body at all: the town's own furniture.

**One colour and one wash, and the pieces told apart by a bar across the ends of each.** A body's ground is
regularly several stretches — a lane, the join after it, the ground beyond its own road it has committed
to — and they butt exactly, so under one wash the joints are invisible and the reading *how far does this
go* cannot be taken. Said with a second wash instead, the pieces read as different **kinds** of ground
standing on one street, which is a stronger claim than the picture has any business making: a claim and a
reservation are the same body's, held for the same reason, and what differs is only where one stops. So the
edge is a thin bar square across the way at either end of every stretch, in the same colour drawn up rather
than down.

**The blocks are a layer of their own and belong to neither kind of body** (OBS-2c). A reservation is a
fact about the *ground*: what cuts a driver's grant is as often a walker standing in the lane as another
car, and what holds a body at the edge of a zebra is a car's stretch of the lane under the paint. Held
under the car layer, neither of those could be seen without the car switch on — which is the reading the
block exists for. **And a layer of their own rather than the nodes layer's**, though both are the town's rather than a
body's: the graphs are the ground the town was laid with and never move once it is laid, the books are
what this tick did to that ground and are re-laid from the bodies every frame, and switched together each
reading came with the other drawn through it. Where both are on the blocks go **over** the graphs, so a
chevron punching through a reservation cannot read as the lane still being open.

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

## Two performance rules this layer taught

- **A cull that admits a body is not a cull that admits its whole line.** Find the visible stretch of a
  line coarsely before sampling it finely.
- **Split a drawing item by what can change and by where it is.** The town's own graphs do not move once
  the town is laid; re-emitting them every tick for the bodies' sake was the most expensive thing in the
  frame at a district framing. Draw them when the zoom changes, when the window leaves the stretch that
  was drawn, or when a switch does.

## The frame read-out

The frame and tick cost with a per-row ranking, plus body and agent counts. **It ranks the tick by phase
and must account for the frame** — a read-out whose rows do not sum to the thing they are rows of is a
read-out nobody can act on. It is a per-run instrument and not a per-frame one: a figure that changes
sixty times a second cannot be read.
