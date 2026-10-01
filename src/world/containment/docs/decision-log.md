# Decision log — containment

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own XML
docs.

## 2026-10-01 — furniture refuses a place by its shape, not its bounds

**Nearly half of Odesa's walkers never left the building they started in.** `PhysicsWorld.StaticInBox` answered
off each static body's axis-aligned bounds, and a building turned off the grid is bounded by a box far bigger than
itself that covers the pavement at its own door — so every spot within the exit search radius read as furniture,
and the exit stayed unavailable for good (PHY-7a). Over six minutes 600 000 exits were asked for and 599 000
refused, every refusal on the furniture, by about 360 people inside and waiting; each refusal was 33 spots
tried, which was most of the walkers' share of the tick. The fault was older than the last week's work: the
same counts at `ce573fb`.

**The bounds now only find the candidates, and the shape answers** (`Shape.Collide` at no margin). Measured on
`--bench age --map Odesa`, two runs each, µs a tick: the walkers' share fell from 32 / 41 / 50 at one, three and
five minutes to 17 / 24 / 31, and the whole tick rose by 4–6 % (717 → 755–767 at one minute), since the people let
out are now bodies, plans and claims like everyone else.

## 2026-09-28 — a place to put somebody down is asked of the reservations

**Occupied was the people standing about, read off a grid of walkers of its own** — rebuilt every tick and read
by nothing else — so a person was put down clear of every walker and in front of any car (PHY-7a). It is the
reservations' word now: a position is taken where a body stands on the ground under it, or somebody has ground
there they can no longer stop short of. The town answers it through `IStandingGround`, so the slice still never
learns what an agent is, and the grid went. A walker lifted back onto the pavement (PER-8) is set down only
where the same question says nobody has the ground.
