# Direct player control — decisions

Why this slice reads as it does. A superseded decision is deleted from here, never annotated.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- `CTL-3`: a right-click on a car walked there and boarded → only a building is entered; a car is walked
  to like any other point.
- `CTL-4`: somebody getting in took a car out of manual mode → being wrecked does.
- `CTL-7`: the town's crews worked the arm through this → the recovery's errand does, and winches first.
- `CTL-8`: an order was carried by what carries a trip → by what carries an errand.

## A car's order is read off the town under the pointer, and it is a goal and never a second driver

One order — nearest free bay to the click — could say only one of the four things somebody wants to say to
a car, not the obvious one, and it retargeted without setting the car off. The answer is read off the
ground: a car, a car park, drivable ground, or none. The pointer is already standing on the thing that
decides, so a modifier key or an order menu would be the player telling the interface what it can see. None
of the four drives the car itself: an order that did would be a second driver, needing a rule for when the
two disagree.

## Manual mode and the order in hand are two facts

One fact sufficed for the walker, whose order has no second phase. A car's has one: an order carried out
must leave the car *waiting* rather than free, or the driver steps onto the pavement and the vehicle draws
itself a trip.

## 2026-10-19 — the wheel puts a car in manual mode, and the panel carries the reset

The owner asked for it. A car whose wheel was given up by a change of selection went straight back to the leg it
held before the hand, so whatever the player had driven it into was undone the moment they looked elsewhere. A
walker already entered manual mode under the hand; a car now does too, and a car let go of with no order in hand
is stood down. A train takes no order, so its manual mode is the town's own flag and the hold is a hand that only
brakes, through the seam the keys already use. Only the player's hand does it, because only the player has the reset. `R` reached the
selection and nothing showed it was there, so the selection's panel carries a release button while it has
something to give back.

## A driverless car takes orders, and CAR-1 is not bent to allow it

The reason is CTL-8d's own. It cost one line elsewhere: a car with a leg in hand is no longer one a
passer-by may take, since `nobody in it` and `not driving` had been the same state.

## The selection is a bounded set, and the gesture is resolved on the way up

Picking out several is the same question asked of more than one body, so the container changed and no
answer did. It is an array laid with the town and a count: a list allocates the first time somebody drags
a box, and a flag per agent is a second copy to keep in step with the roster. The bound is real rather
than formal — a box round a district would otherwise be thirty routes drawn over the streets they run
down. The gesture resolves on release because a click and a drag begin identically, so selecting on the
way down gives one gesture two answers and a flicker between them.

## The selection is ringed, not tinted

A brighter sprite is only readable against the picture beside it — a white van or a car alone on a street
reads the same either way — and it spends the art's one channel saying something that is not about the
car. A ring reads at any framing, survives a recolour, and leaves the town the colours it was painted.

The ring follows the picture's own outline rather than standing as brackets round its box (2026-10-28, the
owner's: "instead of '[ ]' selection — nice highlighting with bold green line"), the way props are outlined.
It is not laid into the art as the props' line is, because what is selected changes from frame to frame. Green,
edged dark so it still reads on a lawn.

**It is drawn by the unit's own sprite instance**, which carries the ring in its sheet's spare bits and is grown by
its width (`SpriteRings`), not by the interface's overlay round a quad worked out a second time (2026-10-28, the
owner's: "how is that even possible"). The overlay's copy read the host's fleet while the device emitted the
bodies off its own, so a walker's ring showed another frame of the stride than the walker. One instance is one
answer: the ring cannot stand off the picture whichever side emits it. It is drawn with the body, so what is laid
over the body afterwards — a lamp's glow, a car over the walker — lies over its ring too.
A goal stays bracketed (`CTL-1a`): a bay has no picture to follow, and the shape tells the unit from where
it is going.

**The ring widens with the zoom, never thinner than two pixels** (2026-10-29, the owner on a walker zoomed in:
"only visible on far zoom and not up close"). Two interface pixels at every zoom were bold beside a car a few
pixels long and a hairline beside a walker two hundred tall, between the picture's dark fringe and the ring's own
edge. It is now the wider of `SelectionRingPx` and `SelectionRingM` of the town at the frame's zoom (`RingLook.At`),
its edge still a pixel. A wider ring at every zoom was tried first (3 px) and set aside: it thickens the far
framing that read well, and still thins up close.

## The selection gets its whole route, and the layers still get two pieces of it

A layer draws two pieces per body because it draws every body on screen at once, and a hundred routes is a
picture of plans with a town underneath; that bound is wrong for the one unit somebody clicked on. The
whole path is drawn by the interface in the layers' own vocabulary, moved out to `PathMarks` when the
second caller appeared.

## The whole route means planning the part the car has not been given yet

A body carries a bounded queue and plans the next stretch when it runs out, so on a shipped town the line
stopped a few streets ahead of the car with the goal mark off in the distance. Enlarging the buffer is per
body across tens of thousands of them; planning the rest for the capped selection alone, with the town's
own `RouteGoalsFor` and `LayRouteLanes` over buffers of the interface's own, draws the answer the car will
be given when it asks.

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
fingers are down and where the first two are.
