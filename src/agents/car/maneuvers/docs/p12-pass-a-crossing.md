# `P-12` — pass a crossing

Code: [P12PassACrossing.cs](../planned/P12PassACrossing.cs) · [catalogue](index.md)

**Scenario.** There is paint on the arm being approached. **This is where the yield to somebody on foot
is discharged** — the whole of what a driver owes a pedestrian, in one entry.

**`Sa` — the state it starts in.** A crossing within reach on the lane the car is on, or the one after it:
a junction paints its far arm too, and read off the lane being left alone the crossing about to be driven
over belongs to nobody.

**And a car under its own geometry owes it too.** A swerve, a bay exit and a turn-around cross the same
paint, so the stop is taken there as well — the lane under the car says which crossings there are, and the
template says where they are, since a template is laid over no lane and its metres are its own. The stop and
the pace, and nothing else: there is no approach for a light to govern on ground no lane owns. The pace used
to be left out, on the grounds that every template was held to the reverse cap anyway — which was a
coincidence between two unrelated figures, and stopped being true the moment `E-4` was let off manoeuvring
pace so that it could overtake.

**`Sb` — the state it delivers.** The whole body past the far edge of the paint, with the pace released.

**Line.** The route's, unchanged.

**Do.**
- Arrive at the paint at the **crossing pace** — a pace to arrive at and not a cap to hold from three
  streets away, so it is read like a corner.
- Stop short of it while anybody is on it or stepping onto it, and while a queue ahead would otherwise
  leave this car standing on it.
- Hold the pace until the **tail** is off the paint, not the nose: a car that accelerates the moment its
  bonnet is over a zebra has not slowed for it.

**Guards.** The pace exemption is read off the *pedestrian* side of the signal table, so what a driver may
do and what the people on the kerb have been told can never disagree. It lifts the pace and nothing else:
somebody on the paint anyway is still stopped for.

**Where "somebody on it" comes from.** The **road's own book**
([`LaneOccupancy`](../../../../world/road/LaneOccupancy.cs)), asked of **this car's own lane**. A body
crossing lays the band of the lane it is standing in, as the one use in that book whose occupant is a
walker — so the reading is a fact the crossing body wrote about itself rather than a search of the ground
beside the paint, and a body in the next lane over is not this driver's stop to make until it is in this
one. It cuts the road a driver is granted like anything else on the lane and reads as
`HeadwayKind.Walker`, which is waited behind and never the shape `E-4` drives round.

**Bounds.** The blocked clock. A crossing with a stream of people on it is a wait, and a wait that has
lasted half a minute is a jam.

**Exits.**

| | Successor |
|---|---|
| the paint is behind the body | `P-4` |

**Why the yield is not handed to `E-1`.** `E-1`'s entry conditions are about agents with a claim on a
box; a person on foot cannot satisfy them. Handing the obligation there gets it refused, and a refused
reactive manoeuvre goes to the ladder — which answers a pedestrian by reversing away from them.

**Why it is not scheduled.** A body on the paint is a body that moves.

**Refs.** CAR-7a, TER-6, TLT-3.
