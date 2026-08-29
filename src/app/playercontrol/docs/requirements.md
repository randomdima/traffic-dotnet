# Direct player control — requirements

A hand that can take over the units it has picked out. This is a debugging instrument with an interface,
not a game: it changes what those agents *want*, and in one place what they *do*, and nothing else in the
town.

**CTL-1 — Selection.** Left-click selects a single unit, person or car; left-click elsewhere reselects or
deselects. The selection is **marked on the town by a shape and never by a change to the unit's own
picture** — corner brackets standing outside its box, laid in the frame that box is drawn in — and **its
behaviour state shows in the interface**.

**CTL-1b — One unit or many.** A selection holds **a set of units and not one**, and everything the
interface does to a selection it does to every unit in it: the brackets, the paths, the orders, the keys
and the lever.

- **A box picks out several.** Dragging the left button over the town selects **every unit whose own
  footprint the box covers** — the footprint a click tests, so what the box catches is what a reader can
  see it drawn over. A box that catches nothing deselects, exactly as a click on nothing does.
- **Shift adds and removes.** Shift-clicking a unit takes it into the selection, or drops it if it was
  already there; shift held through a box keeps what was already picked out. Without shift, both replace.
- **A press starts a gesture and the release resolves it.** Which of the two it was is known on the way
  up: a pointer that did not travel is a click, anything further is a box. **While the button is down the
  box is drawn on screen**, and it is drawn in screen pixels because it is a gesture rather than a place.
- **The set is bounded**, and the bound is a figure like any other. A box round more units than it holds
  takes what fits and stops.
- **One line says what a group is, not what each of it is doing.** A single unit's behaviour state shows
  as CTL-1 says; several show as a count of each kind.

**CTL-1a — Where it is going, drawn whole.** The selection carries a second mark: **the whole of the path
the unit is holding**, from under its own body to the end of what it has planned, as one chevronned line
in the interface's own colour — and **a mark on the goal at the end of it**. A place on the ground is
crossed; **a thing rather than a place is wrapped in the same brackets the unit itself wears** — the
building, the car, the bay, and the car being followed (CTL-8c), which is the one of them that moves and
is therefore marked wherever it has got to rather than where the route was last drawn to.

**The selection's units and not every unit**, which is what makes this the interface asking rather than a
layer answering (OBS-2b, OBS-2h): a whole route drawn for every body buries the town under its own plans,
and a selection is somebody asking about the ones they picked out — which is why the set is bounded
(CTL-1b).

**What the unit is holding is read off it**, and **what it is not holding yet is planned**. A route runs
out rather than ends — a body carries a bounded run of it and plans the next one from where it has got to
— so what a long trip has in hand is its near end. The far end is planned **from the end of what the body
holds, over the town's own network, by the town's own planner, at the town's own prices, to the goal the
leg itself is aimed at**: the answer the body will get when it asks, and so a continuation of the route
rather than a second opinion about it. **Only for the selection** (CTL-1b), because it is a search and a
town is tens of thousands of bodies, and **asked for again only when the end of the held route moves** —
never as the body drives along what it already has.

**The one goal that is not planned on to is a moving one** (CTL-8c): a route drawn to where the car being
followed stood this frame is wrong by however far it has gone since, so a follow is drawn what its own
route says and no further.

**A hand at the wheel has no path** (CTL-5): the behaviour is substituted wholesale, so there is no goal
under the unit and nothing to draw — which is also how the picture tells an ordered unit from a driven one.

**CTL-2 — Manual goals.** Right-click orders every selected unit by **pinning the goal its behaviour would
otherwise pick itself**. **Everything below goal selection is untouched** — same action set, routing,
soft rules, recovery and physics. The control layer substitutes only the behaviour concern's goal choice.

One click is **one point and one order each**: the units are all sent to the same place and each routes to
it as itself, since a group that was given a formation would be the control layer deciding where a body
goes rather than which goal it holds.

**CTL-3 — Context orders.** With a person selected, right-clicking a building or a car walks there and
enters. **All containment checks bind unchanged** (PHY-7a).

**CTL-8 — A car's four orders, and the pointer decides which.** With a car selected, one right-click is
one goal, and **what the pointer was over is the whole of what says which goal it is**. There is no mode
to be in and no key to hold: the town under the cursor already says what a driver sent there would do.

| The pointer is over | The order |
|---|---|
| another car | follow it (`CTL-8c`) |
| a car park | park in it (`CTL-8b`) |
| ground a car may drive on | drive to that place and stand there (`CTL-8a`) |
| anything else | park nearest to it, and walk the rest (`CTL-8b`) |

**Every one of them is a goal and nothing else** (CTL-2). What carries an order out is the same
catalogue, the same route search, the same road and the same tyres that carry a trip, so an ordered car
queues, gives way, is held at a red, recovers up the ladder and is bounded by it exactly as any other.
**No order is a manoeuvre**, and none of them is a new entry of the catalogue.

**CTL-8a — A place on the road is arrived at along the lane that reaches it.** The leg is aimed at the
point rather than at a bay, so the route search picks whichever direction of the stretch it reaches first
and the car comes to rest driving that lane. **Aligning to the lane is the line and never a correction
applied after it** — nothing turns the body to face anywhere.

It is `P-18` that stops the car there, which is the same entry that stops a rescue beside its casualty and
a recovery beside its wreck: one entry, three errands and a hand. The order is finished when the car is at
rest within reach of the place — and, like every other order, when the leg ends any other way (CTL-4).

**CTL-8b — Parking is the bay machinery, and a place off the road is a park and then a walk.** A park
order books a bay and drives the ordinary leg to it. **The bay is the free one nearest the point**, over
the whole town rather than within a walk of it: a trip is bounded because nobody parks a mile from the
door they are going to (PER-10a), while a player who clicked a full car park asked for the nearest free
bay to it and a refusal reads as a click that did not land.

Where the point is somewhere no car may be, **the order is the drive and the walk both**: the car parks
nearest, and the rest of it is handed to the driver as a walker's own order (CTL-3) at the moment they are
put down beside it. **Nobody aboard is not a failure** — a driverless car under this order simply parks,
because there is no one to walk.

**This is the one order that puts a driver out.** Under the other three the driver keeps their seat and
idles at the wheel awaiting the next order (CTL-4), which is what stops a car told to stop in the street
emptying itself onto the pavement.

**CTL-8c — Following is a goal that moves.** The leg is aimed at a place a set gap back along the road
from the car being followed, and it is drawn again once that car has moved far enough to be worth a fresh
route. **What holds the gap is the road and not the order** — the follower is granted what is left of the
stretch in front of the car already on it and holds the speed that road affords, every tick (`S-2a`). The
re-planning is only how the goal keeps up, and it is bounded so a leader creeping forward in a queue is
not a route search a second.

**It is the only standing order of the four**: it is still in hand however many legs have served it, and
it ends when the car being followed stops being one — wrecked, or on somebody's arm — or at the reset.

**A wreck is not a car to follow** (`CAR-1`), and neither is the car being ordered: a click on either
falls through to the ground under it, so it reads as an order to drive to where that car stands.

**CTL-8d — An order needs no driver.** `CAR-1` makes a driverless car furniture because nothing is
choosing for it; **a hand giving it goals is exactly that choice**, and it is the same substitution CTL-5
already makes at the wheel. So an empty car takes all four orders and drives itself to them, showing its
lamps like any other car being driven (`CAR-14.5`).

**A car with a leg in hand is not one a passer-by may take** (PER-4): it is a car standing at a red rather
than a car parked, and somebody who got into it would end the order by driving off in it.

**And the reset stands an empty one down where it has got to**, rather than leaving it to finish the last
goal it was given: the hand was the whole of what was choosing for it, and `CAR-1` is back the moment the
hand lets go.

**CTL-4 — Manual mode and reset.** An ordered unit is in manual mode: after finishing an order it idles
awaiting the next, and **a failed order runs the normal recovery but ends in idle-awaiting-orders instead
of a new random goal**. A reset returns the unit to autonomous behaviour. Terminal-state units take no
orders.

**Manual mode and the order in hand are two facts and not one.** The order is carried out and then gone;
manual mode outlives it, which is what makes a unit that has arrived wait to be told what to do next
rather than draw a goal of its own the moment it stops. **For a vehicle with an errand of its own, the
errand is the behaviour that is substituted** — an ordered ambulance runs the order in place of its call
and picks the call back up at the reset, its own clocks having stood still while the player held it.

**The reset reaches the selection and not the town.** A unit ordered somewhere and then deselected is
still under orders; picking it out again and pressing the key is how it is handed back. The one other way
a car leaves manual mode is **somebody getting in and driving it somewhere of their own** — a trip and an
order cannot both say where a car goes.

**CTL-5 — Direct control.** The keys drive the selected units by hand — throttle/brake and steering for a
car, walk/turn for a person, with the handbrake on its own key. **One hand reaches all of them**: the same
command is pushed through each unit's own seam, and each answers it with its own body, so a group under
one hand is still a group of bodies. A unit in a terminal state takes no hand while the rest of the
selection keeps driving.

This substitutes the behaviour concern **wholesale**, not just its goal, so **it is the one place where
soft rules stop being consulted**: the player may cross the centreline, leave the carriageway, ignore the
queue ahead and drive into things on purpose.

**Nothing else changes.** The unit keeps its own action set and its **whole hard-rule envelope**:
per-gear speed caps, bounded acceleration and braking, the turning circle and no rotating on the spot,
the person's turn rate and constant walk speed, plus terrain effects, collisions, damage and terminal
states. **The rest of the world is not told** — other cars look, queue and yield around a hand-driven
car exactly as around any other, and it still reserves the crossing it is entering so they can.

**CTL-5a — The handbrake is the car's own action, not the player's.** A car driving its route pulls it
whenever the speed profile asks for a dead stop it has **already made** — the end of the route, the car
in front, a junction that is not yet its — so a waiting car holds its spot instead of creeping or being
nudged into the crossing. **It is never asked for on the way down to a stop.**

**CTL-5b — Holding a drive key takes the wheel and keeps it.** Releasing the keys **coasts**; it does not
hand the unit back. The wheel is given up by a right-click order, the reset, a change of selection, or a
terminal state. The arrow keys pan the camera whenever no unit is being driven.

**And the wheel suspends an order rather than ending it** (`S-7`, CTL-8). A hand substitutes the whole
behaviour concern and the order is part of what it substitutes, so the order stands and is picked up
again from the pose the player leaves the car in.

**CTL-5c — A hand at the wheel runs the beacon.** Every car under that hand whose art draws a beacon bar — the police car,
the ambulance and the evacuator, and nothing else the town stands (CAR-14a) — runs it for as long as the
player has its wheel, and it goes out with the wheel. **It buys nothing**: this is the picture and not the road, so
there is no right of way, no exemption from a red or a bar and no pace of its own. A hand-driven rescue
that is also answering a call carries AMB-4 because of the call and never because of the hand.

**CTL-7 — The unit's own action.** `E` works each selected unit's **one action**, if it has one, and does
nothing at all for the ones that have not. It is a **lever and not a pedal**: a press, taken once, on the vehicle's own
machinery rather than on its controls — so it needs no hand at the wheel, and giving the wheel up does not
give up what the vehicle is holding.

**It is the same call the town's own crews make.** The one action anything in this town has is the
evacuator's arm (`EVA-5`), and a crew reaching for it reaches through this and nothing else. That is what
makes the recovery a thing that can be watched being done rather than a rule the player is outside of: a
player who has backed a truck onto a car can pick it up, and a crew that has not got its truck there
cannot.

**CTL-6 — Implemented thin.** One slice owns picking, selection state, order translation and the drive
keys, and the interface draws the mark; **each agent slice exposes a goal seam and a direct seam**, and the input is pushed
through the seam **each tick** so the agent loop cannot tell a hand-driven agent from any other. **Never
drive the body behind its driver's back.**

Consequence: seeded runs are reproducible **unattended** only — manual orders and hand driving
legitimately fork the timeline.
