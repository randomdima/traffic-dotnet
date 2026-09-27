# Containment — requirements

What "inside something" means, and the one rule both container kinds share. The building and car bodies
themselves are [world/statics](../../statics/) and [agents/car](../../../agents/car/docs/requirements.md).

**PHY-7** `P3` An object inside a container is **not rendered, has no collision shape and is not physically
simulated**. Only the container is. A contained object's only available actions are those its container
relationship defines (PER-6).

**PHY-7a** `P3` On exit, a contained person is placed at the nearest unoccupied position on the map within the
exit search radius of the exit point, and **while no such position exists the exit action is
unavailable**. One rule, both container kinds. The ground under the position is not asked (TER-2).

**A person is never teleported out of a container, and a container places its occupant — the occupant
never places itself.** Refused means every position round the container is occupied: stay contained and
ask again next tick, which is the only legal outcome and not a stall. What is standing about is handed in
as data, so this slice never learns what an agent is
([slice-map](../../../../docs/slice-map.md#the-three-seams-that-keep-the-tiers-apart)).

The point a person enters and leaves a building by is `OBJ-4`, in the
[object catalogue](../../../../docs/requirements.md#the-object-catalogue); that a broken car's driver is put
down beside it as a casualty rather than let out is `PHY-6`, in
[world/physics](../../physics/docs/requirements.md#damage).
