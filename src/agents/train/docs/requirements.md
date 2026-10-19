# The train — requirements

A railway's train: what it is made of, how it runs through the town and what it does to whatever is on its
track. The track it runs on, and the crossings where a road meets it, are
[world/rail](../../../world/rail/docs/requirements.md).

**A train is not a driver.** It decides nothing, reads no claim and asks the ground nothing: it is a
timetable and a position along a line, and every rule below is a consequence of that. What the town does
about a train is the crossings', which close ahead of it (`RAIL-1`).

## What a train is

**TRN-1** `P0` **A train runs through the town and never stops in it.** It enters at one end of its track,
off the map, runs the whole track and leaves at the other end, off the map again. There is no station, no
halt and no siding on a map.

**TRN-2** `P0` **A train never slows, stops or yields for anything on its track.** Its speed is its kind's,
the same from the moment it enters to the moment it leaves.

**TRN-3** `P0` **In a collision a train is of unbounded mass.** Whatever it meets is moved, turned and damaged
as against a wall that moves; the train is never moved, turned or damaged.

**TRN-4** `P0` **A train is one of three kinds**: an **intercity** passenger train — an electric locomotive
and coaches; an **oil** train — a diesel locomotive and tank wagons; and a **coal** train — a diesel
locomotive and open wagons heaped with coal.

## How it runs

**TRN-5** `P3` **Two trains are never on one line at once.** A line is a railway's tracks taken together, so
a double track is run one train at a time like a single one: that is what keeps one unbounded mass off
another, by construction rather than by a check.

**TRN-6** `P5` **A train comes now and then**, and when is drawn off the town's seed (`SIM-4`): each line
waits a gap drawn between two figures before its next train, and that train's kind and the track and way it
runs are drawn as it is sent (`SimConfig.Train`). The same town at the same seed runs the same trains.

**TRN-7** `P3` **A wagon is a body of the world while any of it is on the map, and nothing otherwise.** Every
wagon a line can field is stood once with the town and held out of the world until it is needed (`AGT-*`'s
fixed roster, rule 2 of [goals](../../../../docs/goals.md)); a wagon is put into the world as it reaches the
map's edge and taken out as it leaves.

**TRN-8** `P6` **A wagon rides its track on two bogies**: both stand on the line, a bogie pitch apart along
it, and the body is laid along the chord between them — so a long coach on a curve overhangs the outside of
it at its ends and the inside in its middle, as one does.
