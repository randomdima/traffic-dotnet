# Cutting one tow truck's lamps — rules for an agent

Read [base.md](base.md) first: everything there holds, with these differences for a tow truck.

- The truck carries an amber light bar on its cab roof, just behind the windscreen. Its two ends are in the car's file
  as rectangles. **Trace each end's glass** after seed, at the texels your prompt gives:
  `qq props trace CAR --prop beacon_amber_l --at X,Y --lens colour` and the same for `beacon_amber_r`. The bar's
  glass is dark amber, which the default tint reading fails on. Each end is drawn once (not mirrored). If `colour`
  fails too, place the end by hand as a 4-point rectangle a little inside its glass (the manual's hand placement:
  points in `.tmp/props/CAR.json`, bows 0) and `snap --prop` it. Seed lays a `beaconAmber_front` prop over the bar
  from the car file's rectangles: `qq props remove` it.
- Every truck gets head, ind_front, brake, ind_rear — and NO reverse: the beacons take two of the ten lenses, so the
  brake lens lights white in reverse (install writes it as the `rear` fitting). `qq props remove --prop=reverse` only
  makes a seeded reverse a part of the brake again; that is fine — install drops it once the beacons are traced.
- Behind the cab stands a dark steel headboard with a row of round pale work lamps: those are NOT lamps the game
  lights; leave them uncut. The red-and-white stripes along the deck's sides and tail are paint, and so is any amber
  trim on the deck rails: seed often takes the tail bar for a brake or a rail for an indicator — the tail lamps are
  the red lenses at the two rear corners.
- Lens count with the beacons: head 2 + ind_front 2 + brake 2 + ind_rear 2 + beacons 2 = 10. Where the art has no
  separate rear indicator, `remove` ind_rear and say so.
- The cab is yellow; behind it is an open dark steel tow deck, and across the tail a red-and-white striped bar. The
  stripes are paint, not lenses: the tail lamps are the red lenses at the two rear corners. The yellow cab is paint,
  not an indicator.
- Finish with `qq props install CAR` and check its line says 10 lenses.
