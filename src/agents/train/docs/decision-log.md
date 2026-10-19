# The train — decision log

## 2026-10-18 — trains, at the owner's ask

**The owner asked for full trains, not trams**: three kinds — intercity passenger, oil and coal — that stop
nowhere on a map and come in and go out now and then. **What a train is, is the owner's at `P0`** (`TRN-1` to
`TRN-4`): it runs through, never slows or yields, and is of unbounded mass in a collision — the owner's words
were "basically infinite mass" and "P0 all the way", and asked which they meant, both: the rules are the
owner's, and the crossing's hold is the top of the claim ladder (`RAIL-1`).

- **A train is a timetable and a position, never a driver.** It reads no claim and is refused nothing, so it
  has no leg, no patience and no ladder rung of its own; the town's whole answer to it is the crossings.
- **One train a line at a time** (`TRN-5`) is the cheapest way two unbounded masses never meet, and a train
  coming "now and then" asks no more.
- **The wagons are bodies laid once and held out of the world** (`TRN-7`), the way a walker inside a building
  is, so a train arriving allocates nothing.
- **A wagon is put where its train is every tick, moving as it does** (`ControlAction.Run`): pose and motion set, never
  integrated on — so a wagon of no inverse mass shoves what it meets and its own pose is never the solver's answer.
  It carries the look it wears in the signal's flags, as a car's lamps are signalled, and the world hands it back in its
  view for the picture.
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
