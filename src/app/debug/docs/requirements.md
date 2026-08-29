# The debug layers — requirements

What a debug session can be opened for, and what it may cost. **This slice owns the switches as well as
the layers** — the panel that draws a switch is [app/hud](../../hud/docs/requirements.md)'s, and it reads
them from here rather than the layers reaching back into the panel. The frame read-out is not one of these:
it is furniture in the corner and is [app/hud](../../hud/docs/requirements.md)'s. What everything is drawn
with is [app/screen](../../screen/docs/requirements.md).

**OBS-2b** **The debug overlay is instrumentation and is priced as such**: it is **off by default**, what
it costs to draw is measured on the same footing as what it measures, and **nothing is drawn about a body
that is not on screen**. It binds the frame read-out too, which is furniture and cannot be switched off:
what it costs to gather is not paid while its body is shut
([app/hud](../../hud/docs/requirements.md#the-status-panel)).

**OBS-2c** **Each thing a debug session can be opened for has a switch of its own, and no switch turns on
anything a second one owns.** Eight of them. **A layer covers one kind of body entirely** — its geometry and its
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

**The marks down a line stand on a comb laid over the town, not over the line.** One falls wherever the
distance from the world origin along the line's own bearing is a whole number of pitches, so **nothing
about where a line begins, or how much of it is being drawn, moves a single mark**. Marks placed from a
line's own start are a picture of where the lines were cut: two lanes of one carriageway drift against
each other by whatever their ends happen to differ by, an agent's own line disagrees with the network
layer under it, and a reader comparing two streets is reading the cuts rather than the ground. The
bearing is the one the run sets off on, so a run that bends walks off its comb as it turns — the price of
the rule, paid over the metres of a junction join and not over the straights anybody is comparing across.

**A mark says direction only where the ground has one.** Where the two directions of a stretch are laid
on one line — the ground had no room for a lane either side, which is every crossing and every pavement
too narrow for two ([`WalkingNetwork.LaneOffsetM`](../../../world/foot/WalkingNetwork.cs)) — the mark is
a bar square across the line instead of a chevron. Chevronned, the ground carries two combs of opposing
arrows on the same stones, which reads as a fault in the picture rather than as a stretch walked both
ways. **And ground a body covers backwards takes a shade of its own network's colour**, never a colour of
its own: the way out of a bay and the way in converge on the one rear axle, so over the last metres they
are a stroke apart, and in one colour they read as a single line whose chevrons cross.

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

**And the one hold that is not a stretch of way is drawn as what it is.** A bay a car is standing in or has
booked is a place in a register (`GEN-4g`) and not ground in either book, and a bay's own two ways are drawn
to the rear axle and stop there — so a block on one of them can only ever cover the ground behind that axle,
with the car's whole nose past the end of it. The bay is drawn as the bay, in that body's colour like
everything else here: **washed where a body is standing in it, outlined where a leg has only booked it**.
Those two are different claims — somebody is standing here, against somebody is on their way and nobody else
may take it — and not two shades of one.

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

**OBS-2j** **The one layer that computes rather than reads is the turn circle, and it says so.** Every
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

## Two performance rules this layer taught

- **A cull that admits a body is not a cull that admits its whole line.** Find the visible stretch of a
  line coarsely before sampling it finely.
- **Split a drawing item by what can change and by where it is.** The town's own graphs do not move once
  the town is laid; re-emitting them every tick for the bodies' sake was the most expensive thing in the
  frame at a district framing. Draw them when the zoom changes, when the window leaves the stretch that
  was drawn, or when a switch does.
