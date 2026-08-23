# The car agent — requirements

The driver and the body it drives. **The manoeuvre catalogue is not here**: `P-*` and `E-*` are
[maneuvers/](../maneuvers/docs/index.md) — one file and one page per entry, the framework rules `MAN-1…7`
and the standing rules `S-1…7`. What is below is what a car *is*, which is what the catalogue is written
against.

## What a car is and does

**CAR-1** A car **acts only while it contains a driver and is intact**. A driverless or broken car is not
an agent.

**CAR-2** A car contains at most one driver.

**CAR-8** A car has no goals of its own; its destination is its driver's.

**CAR-3** Actions: set steering angle; select gear, forward or reverse; set longitudinal acceleration
between the braking and drive bounds; handbrake.

**CAR-4** Steering changes heading **only as a function of travel and steering angle** — a stationary car
does not rotate.

**CAR-5** Reverse has its own much lower speed cap; acceleration and braking are bounded.

**CAR-4a** Every driven line is a line for the **rear axle**, the one point on a car that travels the way
the car is pointing, and every pose that meets a line is measured to it. The middle of the body crabs,
and the tightest circle it can hold is `√(R² + d²)`. A template drawn through the middle of the car at
the car's own minimum radius is therefore not merely hard to follow but **impossible**, and the car rides
it `atan(d/R)` out of square all the way in.

## Soft rules

**CAR-6** The driver's soft rule set. Each is an intention it can fail to keep, and a failure has a
defined recovery (CAR-9) rather than a correction applied to the body.

**CAR-6.1** Do not intentionally collide with any object.

**CAR-6.2** Move only on drivable terrain, and on directional terrain only in its permitted direction.

**CAR-6.2a** On non-directional drivable terrain heading is unconstrained, but the car must enter from and
leave to legal ground.

**CAR-6.2b** The centreline may be crossed into the oncoming lane **only to pass a stationary obstacle**.

**CAR-6.3** Do not cross a red car light.

**CAR-6.4** Do not idle, except in a parking space, while obeying a signal, or while waiting for other
agents.

**CAR-6.5** Reverse **only as part of a manoeuvre** that owns it — entering or leaving a bay, squaring up
in one, making a turn-around.

**CAR-7** Yielding to another agent that blocks the path is legitimate idling and is the normal way cars
resolve conflicts.

**CAR-7a** A car must yield to any agent already inside the intersection or on a crossing it is taking.

## Recovery

**CAR-9** On a soft rule violation the car **stops**, then may move off-rule along the shortest path back
to legal ground.

**CAR-9a** If no such path exists, **the driver exits and continues the trip on foot**. The car is then
abandoned, which makes it no longer an agent (CAR-1) and exempt from the stuck-agent check (VER-3).

## The tyre model

A car actuates **nothing but a steering angle and a drive/brake demand**. Turning radius, drift,
stopping, pushes and collision response are all solver output
([world/physics](../../../world/physics/docs/requirements.md)).

One impulse per wheel, spent from a friction **ellipse**: side grip and rolling resistance are separate
quantities drawn from **one budget**, so a wheel already using its grip to turn has less left to brake
with. Each patch is weighed by the load its corner carries as the car pitches and rolls, so weight
transfer is a fact about the model rather than a fudge. Front wheels are **Ackermann**-steered; drive
force is placed by layout and divided by the **driven axle's** load; the handbrake locks the **rear**
wheels only, so the back drags while the front pair keeps rolling and steering. **An unmanned car holds
its handbrake.**

**The budget is split between the two axes on slip velocities, not on force demands.** A split on demands
weighs a lateral ask by the corner's load and a longitudinal one by the rim — two orders of magnitude
apart — and makes braking authority mid-slide depend on the tick rate.

**Three guards in that split must not be removed**, each of which cost real work to find: a **deadband**
on carried slip, or a car pulling away counts the same pedal twice and reports a slide it is not having;
**no overshoot** on the rim, or the wheel rings about road speed instead of settling; and the ellipse
boundary treated as a **ceiling, never a target**, or a wheel resyncing gets the whole budget.

**All four wheels read one snapshot of the body's motion taken at the start of the tick.** An impulse
applies immediately, so reading the live velocity per wheel makes the order the wheels are stepped in
break the axle pair's cancellation.

## Marks

A wheel leaves a mark when it is worked past the surface's own threshold. Slide is tracked **per axis**
and spin **per wheel**, so a locked rear axle and a spinning front pair draw different marks. A parked car
with its handbrake on does not scrub the road. Surfaces that **plough** carry a floor under the mark
instead: ploughing is displacement rather than friction, and priced as power it would die with speed, so
a car creeping onto a lawn would leave it pristine.
