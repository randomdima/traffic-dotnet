# Direct player control — requirements

A hand that can take over one unit at a time. This is a debugging instrument with an interface, not a
game: it changes what one agent *wants*, and in one place what it *does*, and nothing else in the town.

**CTL-1 — Selection.** Left-click selects a single unit, person or car; left-click elsewhere reselects or
deselects. The selection is highlighted and **its behaviour state shows in the interface**.

**CTL-2 — Manual goals.** Right-click orders the selected unit by **pinning the goal its behaviour would
otherwise pick itself**. **Everything below goal selection is untouched** — same action set, routing,
soft rules, recovery and physics. The control layer substitutes only the behaviour concern's goal choice.

**CTL-3 — Context orders.** With a person selected, right-clicking a building or a car walks there and
enters. **All containment checks bind unchanged** (PHY-7a).

**CTL-4 — Manual mode and reset.** An ordered unit is in manual mode: after finishing an order it idles
awaiting the next, and **a failed order runs the normal recovery but ends in idle-awaiting-orders instead
of a new random goal**. A reset returns the unit to autonomous behaviour. Terminal-state units take no
orders.

**CTL-5 — Direct control.** The keys drive the selected unit by hand — throttle/brake and steering for a
car, walk/turn for a person, with the handbrake on its own key.

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

**CTL-6 — Implemented thin.** One slice owns picking, selection state, highlight, order translation and
the drive keys; **each agent slice exposes a goal seam and a direct seam**, and the input is pushed
through the seam **each tick** so the agent loop cannot tell a hand-driven agent from any other. **Never
drive the body behind its driver's back.**

Consequence: seeded runs are reproducible **unattended** only — manual orders and hand driving
legitimately fork the timeline.
