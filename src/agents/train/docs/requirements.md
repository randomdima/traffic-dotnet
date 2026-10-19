# The train — requirements

A railway's train: what it is made of, how it runs through the town and what it does to whatever is on its
track. The track it runs on, and the crossings where a road meets it, are
[world/rail](../../../world/rail/docs/requirements.md).

**A train is vehicles on rail lanes, coupled by links, and a dumb driver.** Each vehicle is a body of the world
on its own wheels, the way a car is; the locomotive's driver routes the train over the rail network, sets the
switches ahead of it, pulls for its pace and claims the ground ahead at the top of the ladder, as a call does.
Its own driver reads no refusal: the town yields to it, and the timetable keeps trains apart (`TRN-5`). A hand
may take it, as it takes a car (`CTL-5`), with the very controls its driver has (`TRN-10`).

## What a train is

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
2 of [goals](../../../../docs/goals.md)). A train whose last vehicle has left the map is taken out whole — and so
is one backed off it by the end of its leg it came in by, once its front is off the map or its tail at the
track's end; it is put back on a run-in past the map's edge, moving at its pace and coupled, once its line's gap
has run out — a round trip off the map estimated, never simulated. Nothing is created or removed.

**TRN-8** `P6` **A rail vehicle stands on its bogies, and each bogie is one wheel held on its rail.** A
bogie turns freely about its own pivot, so it turns the body it carries not at all; across its rail it
holds without limit — nothing derails — and along it it rolls on almost nothing, or slides on the
wheel-rail friction when braked. A long coach on a curve therefore lies along the chord between its bogies
and overhangs the outside at its ends and the inside in its middle, as one does, and nothing lays it there
but its wheels.

**TRN-9** `P5` **A bogie knows only the track under it, and a switch decides which.** At a fork the track
is one solid line through whichever way its switch is set; a bogie running into a switch takes that way and
one running out of it takes the one way back, however it is set. The driver sets each switch on its leg
before its front reaches it, by a set-ahead distance (`SimConfig.Rail`) — one it trails through as well as one
it faces, so a train backing runs back the way it came — and never moves one its own train is on.

## How it is driven

**TRN-10** `P5` **A train is driven by one set of controls, whoever drives it**: a share of its locomotive's
tractive effort, forward or back along its leg; its brake, on every vehicle's wheels at once; and a step through
the branches of the switch ahead (`TRN-11`). Its own driver (`TRN-12`) and a hand on it (`CTL-5`) say the same
four things and nothing else, and the wheels hold either to the same physics (`TRN-8`): there is no top speed
but what its traction and its weight come to.

**TRN-11** `P5` **The next switch a train faces may be thrown to the branch beside its own**, left or right as
it is faced, while it stands within the set-ahead distance ahead of the train's front (`TRN-9`). The leg is drawn
again from that branch to any loose end of the railway it can be routed to, and the throw is refused — the
switch left as it was — where there is none, or where the new leg would share ground with a leg still out
(`TRN-5`).

**TRN-12** `P6` **A train left to its own driver runs its leg through at its pace.** It comes on at one loose
end and goes off at another; its pace is its kind's (`SimConfig.Train`), which it pulls for whatever is on its
track, and the one thing about the track that sets it is a curve: no curve under the train is taken faster than
pulls it sideways by its driver's most, and it brakes for one ahead in time to come to that by it. It brakes
otherwise only to stop rolling back — a hand let go of it backing. A collision may cost it a dip too small to
matter, and its wagons close up and stretch out on their couplings as it pulls away. The crossings and its claim
are laid ahead of it at its pace, which a curve only makes it later to; under a hand, at how fast it
moves and as far as the whole of its traction could pull it either way in the time, since a hand may ask that at
any moment (`RAIL-1`).
