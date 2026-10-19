# The railway — decision log

## 2026-10-18 — a railway, its ground and its crossings

**The owner asked for railways with new road crossings, rails and the rest.** The crossing's closing is the
owner's at `P0` (`RAIL-1`), and so is where the railway is drawn in the ground's stack (`RAIL-3`, `TER-7b`):
asked, the owner approved a layer of its own between the decks and the driven ground, the road laid over it at
a crossing.

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
- **At a crossing the strip stops at the walk's outer edge, cut along the road's slant**, and each rail's own band of the
  picture is laid back over road and walk alike: the rail over the tarmac is the rail on the ballast.
- **The crossing's gear is still laid in code** (`CrossingSprites`): its sheet (`railway_a`) drew every piece 1.5 to 3
  times its outline in all three attempts, the cap; whether to ask again is the owner's (`docs/tech-debt.md`).

