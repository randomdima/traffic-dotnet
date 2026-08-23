# Containment — requirements

What "inside something" means, and the one rule both container kinds share. The building and car bodies
themselves are [world/statics](../../statics/) and [agents/car](../../../agents/car/docs/requirements.md).

**PHY-7** An object inside a container is **not rendered, has no collision shape and is not physically
simulated**. Only the container is. A contained object's only available actions are those its container
relationship defines (PER-6).

**PHY-7a** On exit, a contained person is placed at the nearest unoccupied walkable position within the
exit search radius of the exit point, and **while no such position exists the exit action is
unavailable**. One rule, both container kinds.

Two consequences that are easy to get wrong and expensive to debug:

- **A person is never teleported out of a container.** Refused means every position round the container is
  occupied: stay contained and ask again next tick. That is not a stall — it is the only legal outcome,
  and the doorway empties as soon as whoever is in it walks off.
- **A container places its occupant; the occupant never places itself.**
- **And it does so without knowing what an agent is.** What is standing about is handed in as spans —
  where the bodies are, how wide they are, and which of them are themselves contained — so this slice
  stays a fact about containers and the arrow keeps pointing down
  ([slice-map](../../../../docs/slice-map.md#the-three-seams-that-keep-the-tiers-apart)).

The point a person enters and leaves a building by is `OBJ-4`, in the
[object catalogue](../../../../docs/requirements.md#the-object-catalogue); that a broken car's driver is
unaffected and may still get out is `PHY-6`, in
[world/physics](../../physics/docs/requirements.md#damage).
