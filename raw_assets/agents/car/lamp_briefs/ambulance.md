# Cutting one ambulance's lamps — rules for an agent

Read [base.md](base.md) first: everything there holds, with these differences for an ambulance.

- The vehicle carries a roof light bar (the red/blue unit on the roof, just behind the windscreen). It is NOT a lamp
  you cut: its two beacon lenses are already in the car's file and `qq props install` keeps them. If `seed` makes any
  prop over the roof bar (e.g. `beaconRed_rear`), `qq props remove` it.
- Every ambulance gets head, ind_front, brake, ind_rear — and NO reverse: the beacons take two of the ten lenses, so
  the brake lens lights white in reverse (install writes it as the `rear` fitting). If seed makes a reverse prop,
  `qq props remove CAR --prop=reverse` (it becomes the brake's part again).
- Lens count with the beacons: head 2 + ind_front 2 + brake 2 + ind_rear 2 + beacons 2 = 10. Where the art has no
  separate rear indicator, `remove` ind_rear and say so.
- The vehicle is white with a thin red stripe along each flank: the stripe is paint, not a lens. The red cross on the
  roof and the blue star on the bonnet are paint, not lenses.
