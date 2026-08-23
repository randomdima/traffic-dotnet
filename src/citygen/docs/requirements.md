# The town plan — requirements

**One data structure describes a complete city.** A builder instantiates the world from it, a validator
judges it, and a file format carries it between processes. It is pure data — no engine types, no node
references, no behaviour — which is what lets validation run headless and a new map be authored without a
code change. This is the most load-bearing structure in the project.

Bay geometry is [world/parking](../../world/parking/docs/requirements.md); the ground is
[world/terrain](../../world/terrain/docs/requirements.md); roads and junctions are
[world/road](../../world/road/docs/requirements.md).

## Rules the structure enforces

- **Both kinds of corner are carried, not re-derived.** A junction's kerb fillets and the pavement's inner
  fillets cannot be read back off any other shape, so they are records.
- **The stop bars carried are the ones that were *painted*, not the ones the plan called for.** A bar
  whose arm is too short to hold one is dropped, and a bar nobody painted is a bar nobody stops at.
- **Lane directions are sparse in the file and dense in memory**, because direction exists only on
  carriageway but the tick asks for it *by position*, and a sparse lookup in the follower's inner loop is
  a hash where a load would do.
- **A road is carried as its curve.** A consumer that wants a polyline samples the arcs itself; anything
  that *draws* uses the arcs, because a ribbon laid on chords has a facet at every one of them.
- **A straight is an arc at zero curvature** and needs no second form.
- **The cell vocabulary is the plan's.** The eight kinds of ground and their count live here, so that the
  plan points at nothing above it; what each kind *permits* is
  [world/terrain](../../world/terrain/docs/requirements.md)'s, and that is the direction every consumer
  goes. Nothing outside `world/terrain/` names a member of the enum (TER-2a).
- **The reader lives with the structure it produces**, not with the cursor that walks the bytes: a
  `.town` file is a plan, and `core/` does not know what a town is.

## Where a town comes from

**This project reads cities and does not lay them.** Maps arrive as `.town` files in
[towns/](../../../towns/). `GEN-2` through `GEN-8` therefore bind whatever exported them and nothing here
checks them — see [decision-log.md](decision-log.md).

**The one exception is the proving ground**, which is not a city: one closed lap cut into ten roads — five
shapes with a link between each pair — no junction anybody meets at, no light, no paint, no pavement and
nobody living on it, only fifteen people and six cars. It is laid by `TrackPlan`, written out through
`TownWriter` by `--lay-track`, and read back as a file like every other map — so nothing downstream can
tell it from one that arrived. Its shapes are chosen against the car's own figures rather than generated,
which is exactly why it is authored where those figures are.

**It is laid twice, and the two are the same road with a different fifteen people on it.** `Track` stands
them beside the carriageway, where each paces into the lane and back and is what brings a car to rest and
lets it go again without anything staging it; `Drunk` stands them in it, where each reels down its own lane
and stands where it stopped every few lurches (`PER-16`). Nothing else differs — the lap, the shapes and the
six cars are laid from the same arithmetic — so a figure that moves between the two tables is a fact about
what is in the road. Both are written by the one command, because either going stale is a probe quoting a
road this build no longer lays.

**The writer is the reader's other half.** Every field, in the file's own order, at the same width — the
round trip over every shipped map is what holds the two to each other, and it is the only reason a plan
this build lays is a map and not a second kind of thing.

**GEN-1** Generation is driven by the **world seed**, supplied manually or chosen randomly; the same
world seed produces the same city.

**GEN-1a** A map's streets need not be generated; everything else about it is, so a traced city and a
generated one are one kind of thing.

**GEN-1b** No city is built until one is picked: the game opens on a start menu listing the maps and
builds nothing before a choice is made.

**GEN-2** Terrain, objects and agents are placed **plausibly**: the result must read as a small town, not
as noise.

**GEN-2a** A building stands along the street it fronts, not along a compass axis, and its entry point
sits between its front wall and the kerb on walkable ground clear of both. On a town whose streets run at
an angle the alternative reads as a field of sheds, and a door flush against a carriageway is one nobody
can walk out of.

**GEN-3** Spacing must leave the city walkable: every building is surrounded by walkable padding, and no
pocket is too narrow for a pedestrian to pass.

**GEN-5** Connectivity is a hard constraint: the walkable terrain reachable by pedestrians forms **one
connected region**, the drivable terrain likewise, and every building entrance and every parking space
attaches to those regions.

**GEN-6** Counts are a property of the map, never of a rule. A map declares its own size and roster;
everything else scales to the layout — props to the ground left over, parking to GEN-4b's relation, lights
to what conflicts, crossings to where the walkable region would otherwise split.

**GEN-7** Initial state: cars start **stopped in parking spaces**, persons start inside or beside
buildings.

**GEN-8** A candidate city violating GEN-3…GEN-5 is rejected and retried up to the attempt bound, after
which it **fails loudly** rather than emitting an invalid city — and the failure reaches the interface
while the town already on screen keeps running.

## The maps

**The list of maps is one list**, read by the start menu, by the in-game picker and by the command line;
every check, probe and shot names the map its fixtures live on.

**The fixture map is not optional.** It is what every detailed check is staged on: small enough to build
in a fraction of the time and fit on one screen, drawn so that every kind of ground is on it at least
once, and furnished at a fraction of the other maps' counts. Detailed questions asked of "whatever the
big city happens to contain" are a different question every time somebody edits the city.

**Ask a whole city the shallow questions only** — it validates, its junctions are junctions, no lit
junction shows two conflicting greens, nothing is laid on its water. Detailed geometry is asked of named
places on the fixture map.
