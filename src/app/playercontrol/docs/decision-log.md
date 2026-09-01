# Direct player control — decisions

Why this slice reads as it does. A superseded decision is deleted from here, never annotated.

## A car's order is read off the town under the pointer, and it is a goal and never a manoeuvre

The order to a car used to be one thing: the nearest free bay to wherever the player clicked. It was the
wrong shape twice over. It could say only one of the four things somebody wants to say to a car — and it
could not say the obvious one, *stop over there* — and it did not work, because it retargeted a bay
without ever setting the car off, so a right-click on a parked car did nothing at all.

What replaced it reads the answer off the ground rather than off a mode: a car, a car park, drivable
ground, or none of those. **The pointer is already standing on the thing that decides**, and a town top
down is a picture in which a car park looks like a car park — so a modifier key, a palette or an order
menu would all be the player telling the interface something the interface can see. It also means the
four orders cannot be got wrong: there is no state in which the same click means two things.

**None of the four is an entry of the catalogue and none of them may become one.** Each ends in a
destination and a chain, handed to the same call a rescue's leg is handed to; from there it is `P-4`,
`P-14`, the ladder, the road and the tyres. An order that was a manoeuvre would be a second driver, and
the first thing it would need is a rule about what happens when the two disagree.

Two of the four are aimed at a place on a lane rather than at a bay, which the town already had for the
ambulance and the evacuator — so *drive to that spot* and *follow that car* cost one arm each of a
question already being asked, and `P-18` stops the car at all four kinds of place without knowing which
it is at. **That is what says the seam was in the right place**: three errands and a hand share one entry
because what they share is a place on the road that a vehicle has to be stopped at.

## Manual mode and the order in hand are two facts

They were one fact for the walker — `Manual` — because a walker's order never had a second phase. A car's
does: an order that has been carried out has to leave the car waiting rather than free, or the driver
gets out onto the pavement the moment it stops and the vehicle draws itself a trip. Splitting them also
answered the vehicle with an errand: an ordered ambulance runs the order in place of its call rather than
alongside it, and its call's own clock stands still while the player has it — a rescue commandeered and
handed back is a rescue that resumes, not one that timed out in the player's hands.

## A driverless car takes orders, and CAR-1 is not bent to allow it

`CAR-1` makes a driverless car furniture because nothing is choosing for it. A hand giving it goals *is*
that choice, and the rule never had to say otherwise: this is the same substitution `CTL-5` already makes
at the wheel, one seam up. It is also the thing the instrument most obviously wanted — the town is full of
parked cars and every one of them was unusable without first finding somebody to walk over and get in.

What it did cost was one line elsewhere: a car with a leg in hand is no longer a car a passer-by may take.
Until an empty car could be driving, `nobody in it` and `not driving` were the same state, and the trip
that boarded one would have driven the player's car away mid-order.

## The selection is a bounded set, and the gesture is resolved on the way up

The selection used to be one unit — a kind and an index — and everything downstream read it as one:
one set of brackets, one path, one order, one seam. Picking out several is the same question asked of
more than one body, so what changed is the container and not any of the answers. It is an array laid
with the town and a count, because the alternatives both cost something this project will not spend: a
list allocates the first time somebody drags a box, and a flag per agent is a second copy of the answer
that has to be kept in step with the roster it indexes. Membership is a scan over what is actually
selected, so a town with nothing picked out pays one comparison per agent and the tick loop is
unchanged.

The bound is what makes the array possible, and it is a real limit rather than a formality: a box round
a district would otherwise be a selection the size of the town, thirty routes drawn over the streets
they run down, and a read-out with nothing to say. What fits is taken and the rest is left.

**The gesture is resolved on the release, not the press.** A click and a drag begin identically, so a
layer that selected on the way down would select whatever the box was started on top of and then select
the box's contents a moment later — one gesture, two answers, and a flicker on the way through. Shift is
read at the release for the same reason: it modifies the gesture rather than the press. The cost is that
the box is a state the input layer carries between frames, and it is dropped when the town it was drawn
over is rebuilt.

## The selection is bracketed, not tinted

The selected unit used to be drawn with its sprite tint above white, which is the cheapest possible
mark: one number in an instance already being written, no extra quads and no extra pass. It answers the
wrong question, though. A brighter picture is only readable *against the picture beside it* — a white
van, a car alone on a street, or a queue of one make all read the same tinted as untinted — and it
spends the one channel the art has on saying something that is not about the car.

The mark is now four corner brackets drawn through the interface's own overlay, in the frame the unit is
drawn in, standing just outside the box a click tests. It is readable off a single unit at any framing,
it survives the art being recoloured, and it leaves the town's own picture the colours it was painted.

The cost is a handful of overlay quads a frame for one unit, which is nothing next to what the layers
already write, and the mark lives with the interface rather than with the sprites — where the rest of
what the interface says about the selection already lives.

## The selection gets its whole route, and the layers still get two pieces of it

The car and walker layers draw two pieces of a body's line and no more, because they draw it for every
body on screen at once: a town of a hundred cars each showing a route across the district is a picture of
plans with a town somewhere underneath it. That bound is right for a layer and wrong for the one unit
somebody has clicked on — "where is this one going" is exactly the question a selection asks, and two
stretches of lane answer it with the next fifty metres.

So the whole path is drawn, and it is drawn by the interface rather than by a layer: nothing about it is
switched, it follows the selection, and it goes away when the selection does. What it is drawn *with* is
the layers' own vocabulary, moved out to `PathMarks` when the second caller appeared — one width, one
mark, one comb, so the selection's line and the network under it agree stone for stone instead of being
two pictures of one street.

The goal is marked as what it is rather than as a point in all cases. A bay, a building and a car are
things a unit goes *into*, and the brackets already say "this one" about the unit; wrapping the target in
the same shape says the two are the ends of one order. Bare ground gets a cross, which is the only place
left where a mark has nothing to wrap.

## The whole route means planning the part the car has not been given yet

Drawing "the whole path" from what the unit is carrying draws the wrong picture on any trip worth taking.
A car holds a bounded queue of lanes and a walker a bounded run of points; each plans the next stretch when
the one in hand runs out. On a fixture town that is the whole trip and the difference never shows, but on a
town the size of the shipped ones a cross-town route is several times what a body carries — so the line
stopped a few streets ahead of the car and the goal mark stood somewhere off in the distance with nothing
joining the two.

Enlarging what a body carries was the alternative and is not affordable: the buffer is per body, and a town
carries tens of thousands of them, so making the longest trip fit costs megabytes to draw a line for the one
unit somebody clicked on.

So the interface plans the rest itself, and the whole of what makes that honest is that it is not a second
route. It is the same contracted network, the same planner, the same surcharges, and the same goals the leg
is aimed at — `RouteGoalsFor` and `LayRouteLanes` are the drive's own steps, called with the interface's
buffers instead of the car's. What it draws is the answer the car will be given when it asks. Where the town
prices something up in between, the drawn route changes on the frame the car's own does, which is the same
thing happening to both.

Two bounds keep it cheap. It is only ever asked for the selection, which is capped at a couple of dozen
units; and the question is the *far end* of the queue in hand, which does not move as the body drives along
it — so a plan is made when a body plans, and read from a buffer on every frame in between. A slot per
selected unit holds the answer and what it was the answer to.

The one thing it does not plan past is a car being followed: that goal moves every tick, so a planned tail
would be re-searched every frame to draw a line that was already stale. A follow is drawn what its own route
says and stops there, with the lead car wrapped wherever it has got to.

`RouteRunsOut` and `WalkedRunsOut` are what say whether there is anything to ask for. Without them the
interface would have to plan to find out, and for the ordinary body — one standing on the last lane of its
own route — the search from the end of that lane comes back with the way round the block.

## The left button moves the town, and the box moved on to shift

The left drag used to be the box and nothing else, and the camera was moved with the arrows, the wheel and
a middle-button drag. That is a mouse's vocabulary written down as though it were everybody's. A handset
has no middle button, no wheel and no arrows, so a town in a browser on a phone could be looked at from
wherever it happened to open and nowhere else — and the gesture the left button *did* have was the one a
finger is least likely to want.

So the two swapped. A plain drag is the pan, which is the commonest thing anybody wants of a top-down town
and the one gesture a finger has as well as a mouse; the box is shift-drag, which puts every way of picking
out more than one unit behind the same modifier. Shift moved to being read at the **press** at the same
time, because it is now what decides which of the two gestures this is rather than what the release does
with what it caught.

The middle drag was kept. It costs a clause, it is what a hand used to this town already does, and it can
be neither of the other two — so there is no state in which it means something else.

## Two fingers are read here and not in the page

The browser reports where its contacts are; a pinch, a twist and a two-finger pan are what a *pair* of them
did between one frame and the next, and that is arithmetic rather than a platform fact. Reading it in
JavaScript would have put the town's feel on the far side of the wall, where nothing tests it and the
desktop head cannot see it — and would have made the page the second thing with an opinion about the
camera.

So the page writes down how many fingers are down and where the first two of them are, exactly as it writes
down a key or a wheel notch, and the run turns that into a movement. What that costs is four numbers in the
axis array, which crosses the wall in the same copy everything else does.

**All three movements are read every frame and none of them is a mode.** A gesture classified before it was
applied picks wrong on the frame the hand had not decided yet and then holds the wrong answer for the rest
of the movement. The twist alone gets a dead zone, because it is the one reading that is wrong rather than
merely small: no two fingers spread perfectly square, and a pinch whose angle was believed leaves the town
a couple of degrees off north every single time it is zoomed.
