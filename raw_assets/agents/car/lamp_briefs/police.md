# Cutting one police car's lamps — rules for an agent

Read [base.md](base.md) first: everything there holds, with these differences for a police car.

- The car carries a roof light bar (the red/blue unit on the roof). It is NOT a lamp you cut: its two beacon lenses
  are already in the car's file and `qq props install` keeps them. If `seed` makes any prop over the roof bar (e.g.
  `beaconRed_rear`), `qq props remove` it.
- Every police car gets head, ind_front, brake, ind_rear — and NO reverse: the beacons take two of the ten lenses, so
  the brake lens lights white in reverse (install writes it as the `rear` fitting). If seed makes a reverse prop,
  `qq props remove CAR --prop=reverse` (it becomes the brake's part again).
- Lens count with the beacons: head 2 + ind_front 2 + brake 2 + ind_rear 2 + beacons 2 = 10. Where the art has no
  separate rear indicator, `remove` ind_rear and say so.
- The car is white with a dark blue stripe along each flank and a dark blue edge round the bonnet: the stripe and the
  bonnet edge are paint, not lenses; the bonnet shield badge is not a lens.
