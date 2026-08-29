# `P-18` — attend the scene

Code: [P18AttendTheScene.cs](../planned/P18AttendTheScene.cs) · [catalogue](index.md)

**Scenario.** The line the car is driving passes a place it was sent to, and the place is near enough to
be stopped for. Today that place is a casualty lying in the road and the car is an ambulance
([agents/ambulance](../../../ambulance/docs/requirements.md)); the entry knows nothing about either.

**`Sa` — the state it starts in.** A finite distance along the line to the place the car was sent to,
measured at the rear axle like everything else. **The distance may be negative**, which is the car half a
length past the body: the crew works from either side and a place astern of the axle by less than their
reach is a place to hold at, not a place to have missed. Further back than that it is not this entry's
business — it is a body the line comes round to later, and holding for one of those is a car standing
still for ever a street away.

**`Sb` — the state it delivers.** The car at rest beside the place, holding there, until the place stops
being one.

**Line.** None of its own. It holds the route's line and imposes one stop point on it.

**Do.** Ask the profile to be stopped within the distance to the place; hold still once it is reached.

**Guards.** Everything the standing rules already impose. **The stop point is a term of the minimum and
never a substitute for it** — a crossing, a queue, a red or a hazard still binds the profile underneath
it, so nothing here can drive a car through something it was already being held off.

**Bounds.** The stuck fuse, like nearly every other entry, and the call's own clock above it
([`AMB-9`](../../../ambulance/docs/requirements.md)). Standing at the place is seconds and the fuse is
half a minute, so being watched costs a rescue nothing and catches the one case the call's clock is too
slow for: a car brought to rest at the place with something in the way of the last car length of it.

**Exits.**

| | Successor |
|---|---|
| the place is no longer there — the crew has what it came for, or the errand was given up | the plan's next step |
| the fuse | the ladder |

**Entered from `P-4` and from `P-6` alike.** The question is one property of the scene rather than a copy
in each, because the entries that can be in charge when the place comes near enough are not one: a casualty
lies where they were struck and an ordered place is often the far side of a bar the car is creeping up to.
An entry that cannot reach this one drives its car past its own destination and round the block.

**Why the approach is `P-4`'s and not this entry's.** `P-4` hands over only once the place is inside the
road the car needs to stop in. Handed over any earlier, the last hundred metres of a rescue would be
driven by an entry with no way past an obstruction — and **getting past what is in the way is `E-4`,
which only `P-4` reaches**. An ambulance held forty metres short of its casualty by a parked van is a
rescue that never arrives.

**Why its end condition is off the car.** What the crew does when it gets there is containment and
belongs to the town, so what this entry knows about it is exactly what a driver would: the errand is over
when there is no longer anywhere to be. That fact is written in one place and the town clears it both
when the casualty is aboard and when the call is given up, so the entry and the errand cannot disagree
about whether it is finished.

**Refs.** MAN-4, MAN-5, S-2, AMB-5, AMB-9.
