# The train — decision log

## 2026-10-19 — a train is vehicles on rail lanes, coupled by the solver's link

**The owner asked for working trains, not a scripted event**: "they are basically just cars towing each other with
different wheel physics that limits their positions to the rails but allows free rotation". Each tick is a car's —
each wheel, then its body, then the forces in the solver — and nothing about a train is posed, pathed or timed by a
script. The owner's words settled each part:

- **`TRN-2` and `TRN-3` restated by the owner**: a train is a real, heavy mass and takes no damage; it never brakes or
  yields, and its traction holds its line's speed — a collision may cost a negligible dip, and starting and stopping
  may compress it.
- **One solid link for the tow and the train** (`SOL-38`), free to spin, with "no special physics treatment for link".
  What a train gains on top is its wheel base: each axle or bogie is one large wheel, free to rotate, several along a
  vehicle (`TRN-8`). Unbraked a rail wheel rolls on very little; locked, it holds very hard.
- **Rail lanes are real lanes, and a train is a real actor** routed over them: the map may lay any network, and its
  switches are inferred where tracks meet, each a line in one of its positions that the physics runs as one solid
  line; the driver sets one about 100 m ahead (`TRN-9`). A wheel knows only its track — it cannot derail for now —
  and all of it runs on the GPU but the driver, as a car's does.
- **Dumb for now**: the train places a high-rung claim as police do and never stops; it expects everything to yield
  or to have been scheduled ahead of it. More than one train runs at once, placed so their legs never share ground
  ("no need to do any hard math, its all tmp solution", `TRN-5`).
- **No collision exemptions**: vehicles are spaced so they do not touch, and a track that crosses itself makes a
  train meet itself. Bridges work as a car's do.
- **Nothing is removed off the map** (`TRN-7`): a train whose tail has left is taken out whole, waits about two
  minutes for a round trip estimated rather than simulated, and is put back on the track's other side.
- **Traced maps are out of this work**: the target is the generated and laboratory maps, whose tracks meet exactly.

## 2026-10-18 — trains, at the owner's ask

**The owner asked for full trains, not trams**: three kinds — intercity passenger, oil and coal — that stop
nowhere on a map and come in and go out now and then. **What a train is, is the owner's at `P0`** (`TRN-1` to
`TRN-4`).

- **On a bridge a wagon is on a channel of its own** (`RAIL-8`): `OdesaOsm`'s first run met 2 cars and 11 walkers in
  337 s of trains, the line passing over streets on its bridges. Read off the wagon's middle it still met 6 walkers,
  4 of them under a bridge's end a wagon reached over, so **any of a wagon over a bridge puts it on the bridge's
  channel**; the 2 left are beside the track at no crossing (`docs/tech-debt.md`).
- **A train's art is the ground's grid and packed in rows** (`qq sprites`' `rows` layout): a 3×3 sheet draws a coach
  under 20 px/m.

## 2026-10-19 — every vehicle drawn, the freight diesel one way

**The owner asked for all the art generated, "minor size deviations allowed, adjust collider"**, a freight train's
locomotive one-way rather than two, and everything strictly top-down.

- **A diesel has one cab, at its front**: a train's first vehicle is its locomotive and every vehicle is laid facing the
  way its train runs (`Trains.Place`), so the cab leads whichever way a track is run; the electrics keep a cab at each end.
- **The narrow vehicles' collider was widened to what the art draws**: a coach and an electric 3.1 → 3.4 m, a tank 3.0 →
  3.3 m. The model draws a long thin outline some 12 % wide however it is asked, so the rest is fitted down
  (`qq sprites`' rows allowance) rather than the collider widened to 3.8 m, a broad-gauge coach's width and a half again
  of its overhang.
- **The grey gondola is out of the rolling stock** until it is drawn: twice tilted and once never drawn, at the cap. Two
  coal looks remain.
