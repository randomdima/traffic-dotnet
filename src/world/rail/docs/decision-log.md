# The railway — decision log

## 2026-10-19 — a generated railway loops over itself, at 65 m

**The owner: "one hard turn for trains on maps … even more lets make it self 'intesect' but via a bridge … make trains
longer for this … loop can be tigher 4x"**, and, offered 62 m against 45 m: the radius the stock clears (`RAIL-11`).

- **Three quarters round, square over its own way in.** A 270° loop's way out crosses its way in at right angles a
  radius from either end, so the bridge is short and the crossing needs no skew figure; a brief may turn 240° to 300°,
  within which the crossing falls on the straights either side of the loop (`RailLines.StraightRadii`).
- **The least radius is 65 m**, from 600: a loop at 600 m is some 4 km between its passes, which no train is long
  enough to be over itself on. A quarter of the 180 m first laid (45 m) closes two coaches' inside corners by 0.96 m
  against their 0.70 m of buffers; 62 m is where they touch, and 65 m leaves them 3 cm (`RollingStockTests`).
- **The loop is round the town's middle, off by the brief's offset**, and a brief places it: a loop under the streets
  takes every one that would cross the track twice (`RAIL-5`) — Test at its middle kept a single block — and one by a
  river runs its way out down the water.
- **The line is walked by its heading.** The sine wander laid sideways off a straight could not bend round a loop; as
  a turn of the heading that never averages to one way, it keeps the bearing over any length and composes with a turn.
- **A band is struck round each run of one level**: a track crossing itself banded whole is a ring crossing itself,
  which covers neither pass where they cross.
- **The bridge is as long as the climb to it** — the owner: "make bridges longer - trains cannot get that angle irl",
  then of a 2 % climb, "its now way too long … 3x smaller". It climbs its 6.5 m clearance at 6 % either side of the
  ballast it spans, 108 m, counted on the inner track of a double line, which runs the least radius round the loop.
- **A rail bridge is of the level above, as a road's is** — the owner: "trains are rendered on top of the bridge even
  when they are under … they should follow car logic". Its deck, track and rails are the ground's last part, drawn
  over the bodies on the ground, and a wagon any of which is on it (on the bridge's channel, which its view carries)
  is drawn in the run above, over the deck.
- **A car park's bay asks the ground at both its ends**: its near node, beside the street, was asked only its spacing,
  and the suite's city stood one inside the ballast's clearance once the line ran past it (`CarParks.At`, RAIL-5).

## 2026-10-18 — a railway, its ground and its crossings

**The owner asked for railways with new road crossings, rails and the rest.** The crossing's closing is the
owner's at `P0` (`RAIL-1`), and so is where the railway is drawn in the ground's stack (`RAIL-3`, `TER-7b`).

- **A crossing holds the way a light does, and is not a lane.** Laying the track as a way of the town would
  have put it in the ribbon atlas and let the marks the claims already compute close the crossings; it would
  also have put a few kilometres of ribbon on the device and a train's main claim through every pass. A hold
  per crossing stretch, placed like a light's (`SignalHolds`), is the same answer for a fraction of it.
- **Rung `p0` is the owner's** (`RAIL-1`): at the light's `p4`, a call (`p2`) would drive through a lowered
  barrier in front of a train that cannot stop.
- **The track is a line and its ballast a band struck off it**, held in the plan as rings the way the water
  is, so the ground answers it off them (`TER-7`); the picture is the same band swept along the line (2026-10-19).
- **Ground a car can no longer stop short of is never taken from it, at `p0` as at any rung**: answering takes committed
  ground first (`TER-5e`), so a car that could not stop when its crossing closed is let over, and the hold ends short of
  it. The warning time is what keeps that rare, and `--bench trains` counts what a train met.
- **What a crossing shows is the host's, handed to the device each frame**: when a crossing closes depends on where a
  train is a warning time short of it, before any wagon of it is on the map, which no device state holds. Each
  approach's barrier length and lit lamp are written into a mapped row beside the frame's words (`DeviceSprites`) — no
  call of its own, and no agent's pose: what is sent is the crossing's state, as a light's phase is the clock's.

## 2026-10-19 — the railway drawn from art

**The owner asked for the railway's visuals improved and every asset generated**, the track drawn "similar to car
lanes": an object of its own a ribbon is extruded from and textured, no plain primitives, and across a road "don't
paint over pavement, it should only have rails" — which is `RAIL-3`'s, theirs, restated in those words.

- **The track's strip is a sixth ground surface swept along its line** (`GroundMesh.TrackStrip`, `Surface.Track`): every
  chord a quad mitred to the next, the picture repeating along the track by how far along it a corner stands. Laid
  as sprites over the ground it would cover the road that crosses it, and as a world-anchored surface its sleepers
  could not follow a bend. **A corner files where on the strip it stands in the word that is a tint's elsewhere**, so the
  vertex stays twelve bytes, and **what the picture leaves clear is discarded** — the one cut-out in a pass with no blending.
- **The strip's picture is generated, its ballast as narrow as the model draws it**: three attempts drew the rails at
  the gauge and centred and the ballast a shoulder past the sleepers, never to the 4.4 m asked; `qq sprites cut` fits a
  track by its rails and leaves the rest of the band clear, a verge the grass shows through. The ground still keeps the
  whole band clear (`RAIL-4`), and a picture authored at another width than the ballast is refused.
- **The track under the road, the rails over it** — the owner's stack, "Tracks <- Road <- Rails" (`RAIL-3`, `TER-7b`):
  the strip is laid whole right after the grass, with no cut at a crossing, and each rail's own band of its picture is
  laid along the whole track after the paint, over a road's marks too, so nothing asks where a road crosses it. A track on a bridge is
  laid with the rails, over the ground it crosses. Cutting the strip at each crossing's walk left its ends to match the
  road's edge, which looked right on grass and not on roads.

## 2026-10-19 — the crossing's gear generated, its barrier a body

**The owner asked for the stop line moved off the track, the gear drawn from generated art, and the barrier a body of
its own that stops cars — "move back and forth instead of up and down for simplicity"** (`RAIL-7`, `RAIL-10`).

- **The barrier is lowered and raised**, the owner's second word on it after a first cut that slid it along its own line
  ("lets not move bar to the side when its open, lets do some up lifting animation"). **Its body is the whole arm lying
  down, in the world while it is anything but all the way up** and out of it otherwise — a body whose length changed with
  the swing is one neither world has, and a `Place` as it starts down and a `Contain` once it is up are paths both
  already take. No weight, on a layer of its own that meets cars only (`CollisionLayer.Barrier`); its lights flash a
  while first (`BarrierDelayS`), so a car that could not stop is past its line before it starts down.
- **A lane stays held until the barrier is all the way up.** Released at the opening, cars drove into arms still on
  their way: twenty met over five minutes of Odesa, none once the hold waited (`--bench trains`, "met by an arm").
- **The gear's sheet (`railway_gear_a`) is the gear alone, at its own scale**, as `railway_a`'s lesson said: the booms
  fit at once, the housings, cabinets and heads came back 10 to 16 % over in their own proportions in all three attempts,
  and the owner allowed `cut` to fit the gear that far (`FIT_GEAR_MAX`).

## 2026-10-19 — a level crossing is a junction

**The owner: "it's a junction after all — maybe we don't need to add graph nodes, but logically it should be"**: regular
stop lines, lights "in form of an X light", the lines inside it solid, the barrier "2 m from the closest train track point
along the line (2 m if it's perpendicular, but if it's angled it can be more)", an open barrier not hidden whole, and "a
bit of vertical offset, so it feels a bit more 3D" (`RAIL-7`). No node is added: the crossing stays holds on its lanes.

- **The barrier's line is placed off the rails**: square to the road, moved along it until whichever end — the arm's
  tip, or the walk's far edge its drive stands on — is 2 m along the road from the nearest rail's outer face
  (`BarrierRailClearanceM`, `LevelCrossings.Barrier`). Read as "track point", the rail rather than the ballast. A walk's
  walkers wait the same 2 m short of a rail. Earlier cuts stood it a fixed distance off the ballast at the road's middle,
  which put a skewed crossing's near end closer than its far.
- **The stop bar is a junction's, on the lanes coming in only, 3 m short of the barrier** (`StopLineSetbackM`), where
  the lane's hold begins (`CrossingStretches`, read by the holds and the ground alike) — earlier cuts laid a line parallel
  to the track, one square across the whole road, and a bar a fixed 6 m off the ballast. **The lines down the road are
  unbroken from stop bar to stop bar** (`GroundMesh.LaneDashes`), as a junction's approach is.
- **The light is an X over each lane coming in, drawn as a junction's head is** (`CrossingLight`): flat across the lane,
  midway between its stop bar and the barrier, dark while open and flashing lit while closed. Its art (`railway_lights_a`)
  is a lit twin per light, the model asked to copy the dark cell and light only the X; the upright two-lamp head it
  replaces, and its glow, are gone.

## 2026-10-19 — a raised arm stands the same way at every crossing

**The owner: a raised arm "looks different on different junctions" — keep it like the one "not touching the road (or
almost touching)"; and the lowered arm's collider is off its picture** (`RAIL-7`).

- **Height is drawn aside along the road, away from the rails, and measured from the pivot** (`HeightDrawnAside`, a
  quarter; `CrossingSprites.ArmDrawn`). Drawn north, as first cut, a raised arm on a road north of its drive leaned out
  over the lanes and one south of it folded back along the kerb, and a lowered arm — a metre up at its pivot — was drawn a
  quarter metre north of its body. Drawn aside, every arm stands the same way against its own road whatever its heading
  or the camera's turn, and lowered it lies on its body.
- **A raised arm stands upright** (`BarrierRaisedDeg`), not 80°: at 80° a 5 m arm's tip still stood 0.8 m over the kerb,
  more on a wider road, and its drive meets the road 0.2 m from the pivot. Upright, raised it is drawn only aside, along
  the walk.

