# The train — requirements

A railway's train: what it is made of, how it runs through the town and what it does to whatever is on its
track. The track it runs on, and the crossings where a road meets it, are
[world/rail](../../../world/rail/docs/requirements.md).

**A train is vehicles on rail lanes, coupled by links, and a dumb driver.** Each vehicle is a body of the world
on its own wheels, the way a car is; the locomotive's driver routes the train over the rail network, sets the
switches ahead of it, asks its throttle for the line's speed and claims the ground ahead at the top of the
ladder, as a call does. It reads no refusal and never stops: the town yields to it, and the timetable keeps
trains apart (`TRN-5`).

## What a train is

**TRN-1** `P0` **A train runs through the town and never stops in it.** It enters off the map at one end of
the railway, runs its leg across the map and leaves off the map at another end. There is no station, no
halt and no siding on a map.

**TRN-2** `P0` **A train never brakes or yields for anything on its track.** Its traction holds its line's
speed from the moment it enters to the moment it leaves: a collision may cost it a dip too small to matter,
and its wagons may close up and stretch out on their couplings as it starts and as it stops.

**TRN-3** `P0` **A train is a real and heavy mass, and it takes no damage.** What it meets is moved, turned
and damaged by the weight of the vehicles that met it; the train is never damaged.

**TRN-4** `P0` **A train is one of three kinds**: an **intercity** passenger train — an electric locomotive
and coaches; an **oil** train — a diesel locomotive and tank wagons; and a **coal** train — a diesel
locomotive and open wagons heaped with coal.

## How it runs

**TRN-5** `P3` **Two trains are never sent on legs that share ground.** A leg's ground is every rail way on
it and every way one of those shares ground with — the other way of a track run both ways, a track crossing
it on the level. A train whose drawn leg shares ground with one still out waits and draws again, so trains
run at once on legs apart and never meet by construction rather than by a check.

**TRN-6** `P5` **A train comes now and then**, and when is drawn off the town's seed (`SIM-4`): a line has a
train for every lane its tracks lead onto the map by, and each waits a gap drawn between two figures before it
comes again; its kind and its leg are drawn as it is sent (`SimConfig.Train`). The same town at the same seed
runs the same trains.

**TRN-7** `P3` **A train off the map is out of the world, and its trip there is a wait, not a run.** Every
vehicle a line can field is laid once with the town and held out of the world (`AGT-*`'s fixed roster, rule
2 of [goals](../../../../docs/goals.md)). A train whose last vehicle has left the map is taken out whole; it
is put back on a run-in past the map's edge, moving at its line's speed and coupled, once its line's gap has
run out — a round trip off the map estimated, never simulated. Nothing is created or removed.

**TRN-8** `P6` **A rail vehicle stands on its bogies, and each bogie is one wheel held on its rail.** A
bogie turns freely about its own pivot, so it turns the body it carries not at all; across its rail it
holds without limit — nothing derails — and along it it rolls on almost nothing, or slides on the
wheel-rail friction when braked. A long coach on a curve therefore lies along the chord between its bogies
and overhangs the outside at its ends and the inside in its middle, as one does, and nothing lays it there
but its wheels.

**TRN-9** `P5` **A bogie knows only the track under it, and a switch decides which.** At a fork the track
is one solid line through whichever way its switch is set; a bogie running into a switch takes that way and
one running out of it takes the one way back, however it is set. The driver sets each switch on its leg
before its front reaches it, by a set-ahead distance (`SimConfig.Rail`), and never moves one its own train
is on.
