# Containment — requirements

What "inside something" means, and the one rule both container kinds share. The building and car bodies
themselves are [world/statics](../../statics/) and [agents/car](../../../agents/car/docs/requirements.md). A served
town's players add two places only a player is ever in — a train's cab and offstage
([multiplayer](../../../multiplayer/docs/requirements.md) `MP-4`, `MP-13`) — and PHY-7 holds of both.

**PHY-7** `P3` An object inside a container is **not rendered, has no collision shape and is not physically
simulated**. Only the container is. A contained object's only available actions are those its container
relationship defines (PER-6).

**PHY-7a** `P3` On exit, a contained person is placed at the nearest unoccupied position **on the pavement off the
exit point** — along the pavement way the exit stands beside, a body's width at a time either way, out to the exit
search radius — and **while no such position exists the exit action is unavailable**. One rule, both container
kinds. **A served town's player is the one exception** (MP-13): getting out, or putting somebody out, is an act and
not an arrangement, so with no free position it is the ground off the vehicle's door, as a crash's is (`PHY-6`). Which of those positions are clear of the town's furniture is worked out with the map, and only whether
anybody has the ground is asked while the town runs (the root decision log, 2026-10-13).

**A person is never teleported out of a container, and a container places its occupant — the occupant
never places itself.** Refused means every position round the container is occupied: stay contained and
ask again next tick, which is the only legal outcome and not a stall. **Occupied is the reservations'
word** (`TER-4c`): a position is occupied where somebody's body or plan holds the stretch of the pavement way it
stands on, or somebody has ground there they can no longer stop short of — so nobody is put down on a walker or in
front of a car that could not stop for them. The town answers the question.

The point a person enters and leaves a building by is `OBJ-4`, in the
[object catalogue](../../../../docs/requirements.md#the-object-catalogue); that a broken car's driver is put
down beside it as a casualty rather than let out is `PHY-6`, in
[world/physics](../../physics/docs/requirements.md#damage).
