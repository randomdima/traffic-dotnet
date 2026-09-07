# Direct player control — decisions

Why this slice reads as it does. A superseded decision is deleted from here, never annotated.

## A car's order is read off the town under the pointer, and it is a goal and never a manoeuvre

One order — nearest free bay to the click — could say only one of the four things somebody wants to say to
a car, not the obvious one, and it retargeted without setting the car off. The answer is now read off the
ground: a car, a car park, drivable ground, or none. The pointer is already standing on the thing that
decides, so a modifier key or an order menu would be the player telling the interface what it can see.
None of the four is a catalogue entry and none may become one — an order that was a manoeuvre would be a
second driver needing a rule for when the two disagree.

## Manual mode and the order in hand are two facts

One fact sufficed for the walker, whose order has no second phase. A car's does: an order carried out must
leave the car *waiting* rather than free, or the driver steps onto the pavement and the vehicle draws
itself a trip. It also answers the vehicle with an errand — an ordered ambulance's call clock stands still,
so a rescue commandeered and handed back resumes rather than times out.

## A driverless car takes orders, and CAR-1 is not bent to allow it

`CAR-1` makes a driverless car furniture because nothing is choosing for it; a hand giving it goals *is*
that choice, which is the substitution `CTL-5` already makes one seam up. It cost one line elsewhere: a
car with a leg in hand is no longer one a passer-by may take, since `nobody in it` and `not driving` had
been the same state.

## The selection is a bounded set, and the gesture is resolved on the way up

Picking out several is the same question asked of more than one body, so the container changed and no
answer did. It is an array laid with the town and a count: a list allocates the first time somebody drags
a box, and a flag per agent is a second copy to keep in step with the roster. The bound is real rather
than formal — a box round a district would otherwise be thirty routes drawn over the streets they run
down. The gesture resolves on release because a click and a drag begin identically, so selecting on the
way down gives one gesture two answers and a flicker between them.

## The selection is bracketed, not tinted

A brighter sprite is only readable against the picture beside it — a white van or a car alone on a street
reads the same either way — and it spends the art's one channel saying something that is not about the
car. Four corner brackets through the interface's overlay read at any framing, survive a recolour, and
leave the town the colours it was painted.

## The selection gets its whole route, and the layers still get two pieces of it

A layer draws two pieces per body because it draws every body on screen at once, and a hundred routes is a
picture of plans with a town underneath. That bound is wrong for the one unit somebody clicked on. The
whole path is drawn by the interface rather than a layer, in the layers' own vocabulary moved out to
`PathMarks` when the second caller appeared. The goal is marked as what it is: a bay, a building and a car
are wrapped in the brackets that already say "this one", and bare ground gets a cross.

## The whole route means planning the part the car has not been given yet

A body carries a bounded queue and plans the next stretch when it runs out, so on a shipped town the line
stopped a few streets ahead of the car with the goal mark off in the distance. Enlarging the buffer is
per body across tens of thousands of them. The interface plans the rest with the same network, planner,
surcharges and goals — `RouteGoalsFor` and `LayRouteLanes` called with its own buffers — so what it draws
is the answer the car will be given when it asks. It is asked only for the capped selection, and only
about the far end of the queue in hand, which does not move as the body drives. A followed car is not
planned past: that goal moves every tick, so the tail would be re-searched every frame to draw a stale
line.

## The left button moves the town, and the box moved on to shift

The left drag was the box, with the camera on arrows, wheel and middle button — a mouse's vocabulary
written down as everybody's, leaving a phone able to look at a town only from wherever it opened. The two
swapped: a plain drag pans, and the box is shift-drag, which puts every way of picking out more than one
unit behind the same modifier. Shift moved to being read at the press, since it now decides which gesture
this is. The middle drag was kept — it costs a clause and can be neither of the other two.

## Two fingers are read here and not in the page

A pinch, a twist and a two-finger pan are what a pair of contacts did between two frames, which is
arithmetic rather than a platform fact; reading it in JavaScript would put the town's feel where nothing
tests it and make the page a second thing with an opinion about the camera. The page writes down how many
fingers are down and where the first two are. All three movements are read every frame and none is a mode,
because a gesture classified before it was applied picks wrong on the frame the hand had not decided yet.
The twist alone gets a dead zone: no two fingers spread perfectly square, and a believed angle leaves the
town a couple of degrees off north every time it is zoomed.
