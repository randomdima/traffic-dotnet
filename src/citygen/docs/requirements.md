# The town plan — requirements

**One data structure describes a complete city.** A generator lays it, a builder instantiates the world
from it and a validator judges it. It is pure data — no engine types, no node references, no behaviour —
which is what lets validation run headless and a new map be authored without a code change. **Nothing
carries it between processes**: a town is its brief and its seed, so what would be written out is a second
answer to where the town is. This is the most load-bearing structure in the project.

Bay geometry is [world/parking](../../world/parking/docs/requirements.md); the ground is
[world/terrain](../../world/terrain/docs/requirements.md); roads and junctions are
[world/road](../../world/road/docs/requirements.md).

## Rules the structure enforces

- **A junction's kerb fillets are carried, not re-derived.** A kerb fillet cannot be read back off any
  other shape, so it is a record. **The pavement has no corners of its own to carry**: it is the tarmac
  grown by one figure and every corner it turns is a corner of the thing it wraps (TER-3c.3).
- **The stop bars carried are the ones that were *painted*, not the ones the plan called for.** A bar
  whose arm is too short to hold one is dropped, and a bar nobody painted is a bar nobody stops at.
- **Lane directions are sparse in the file and dense in memory**, because direction exists only on
  carriageway but the tick asks for it *by position*, and a sparse lookup in the follower's inner loop is
  a hash where a load would do.
- **A road is carried as its curve.** A consumer that wants a polyline samples the arcs itself; anything
  that *draws* uses the arcs, because a ribbon laid on chords has a facet at every one of them.
- **A straight is an arc at zero curvature** and needs no second form.
- **The ground vocabulary is the plan's, and so is the answer.** The seven kinds of ground live here, and
  so does *what is on the ground at a point* — solved against the plan's own shapes (`GroundShapes`), which
  is what lets a stage ask where a thing may stand while the town is still being laid. What each kind
  *permits* is [world/terrain](../../world/terrain/docs/requirements.md)'s, because a permission is a rule
  about agents and the plan does not know what an agent is; that is the direction every consumer goes, and
  nothing outside `world/terrain/` names a member of the enum (TER-2a).

## Where a town comes from

**A city is generated from a brief when it is opened** (`TownGenerator`). What is authored is a
`TownBrief` in [towns/](../../../towns/) — a seed, an extent, the water it stands on, how many districts and
how strictly they are laid out, and how many of everything — and what a reader sees is whatever that seed
makes of it. **Nothing derived is ever stored**: no district, no node, no curve, no cell and no building,
because a brief that carried geometry would be a second answer to where the town is and the one on disk is
the one that goes stale.

**A map laid to measure one thing is laid in code**, and none of them is a city: each is arithmetic over
the car's own figures rather than a seed, which is why they are authored where those figures are. `Maps` is
the one list both kinds appear on, and a `CityPlan` is where the difference between them ends. **This build
ships one of them**, the idle ring, and the laboratories that measured something are
[a known gap](../../../docs/index.md#known-gaps) rather than a set this document lists.

**The idle ring** (`IdlePlan`) is the one laid map that measures nothing, and the one the game opens on
(GEN-1b). It is **one loop of road with nothing else on it** — no building, no bay, no paint, no light and
nobody on foot — carrying **an escorted convoy one way round and one car the other**: an armoured car
between two police with their beacons up, and a sports car on the opposite lane of the same carriageway.
What it is for is the picture the game idles on, so what it is chosen against is that it never stops being
worth watching and never needs anybody's attention. **One car comes the other way and not a second convoy**:
the same thing twice reads as a staging, where a quick car passing a slow escort is the plainest picture of
traffic there is.

**The escort is held under the pace of what it is escorting** (`IdlePlan.EscortPaceShare`, carried as
`CarFleet.PaceMps`). Police paint is among the quickest looks the fleet ships and an armoured car among the
slowest, so a leading escort left at its own pace drives away from its charge inside a lap. Held under it,
the escorted car closes on the one ahead and the one behind closes on it, and the three keep station under
the ordinary following rule — nothing staged, and nothing told to stay together. **The pace is read against
the loop's tightest corner** (`IdlePlan.CornerRadiusM`), which is where the charge has the least margin over
its escort and so where a convoy comes apart if it is going to.

**And it follows closer than traffic does** (`IdlePlan.ConvoyFollowingShare`, carried as
`CarFleet.FollowingShare`), which is what makes three cars read as one thing. It scales the **following
interval** — the second of travel a driver leaves on top of the road it needs — and nothing else: every
stopping distance, every corner and the ground the car in front has yet to vacate are what they were, so a
convoy running close is still a convoy that can stop. The pace is the other half of the same effect, since
the road a follower is granted is the road it needs to stop in.

**It is laid to fit the view a run opens on** (`OBS-1b`): a circuit wider than the window is a picture of an
empty stretch of road between one car and the next, so the whole ring is on screen and every car in the
frame at once — and the field inside it is where the start menu stands (GEN-1b). **Nothing drives it that is
not already in the town** — with nowhere to be on the map, the rule that drives an empty map's cars
(`TownWorld.DriveTheEmptyMap`) puts each on the lane under it and the ordinary catalogue does the rest.
**Its cars are dressed by the map** and not by the fleet's wrap, on the terms the exam's one look is
(`TownWorld.LookOf`): a look is what a map asks for and never a duty — a police car is one with a station
(SRV-2), and a car in police paint on a map with no station is an ordinary car in service paint, which is
the state `EVA-7` already names. **It is cut into four roads** because a road runs between two named
junctions (`TER-4`) and a loop has no end; one road a side is the fewest that leaves no two nodes joined
twice, and **each is cut at the middle of a straight** so no node stands on a bend. **Nothing turns at any
of them** — each node joins one road to the next and offers one way out — so nothing on this map indicates,
gives way, or is refused anything (`CAR-14.1`, `SIM-7`).

**The loop is a square with rounded corners and not a circle** (`IdlePlan.CornerShare`), because **the field
it encloses is what the start menu stands in** and a panel is a rectangle. A circle spends most of the ground
inside it on corners a panel cannot reach into; rounding a square leaves the middle of the field as wide as
the field is. How far the corners are rounded trades that field against the corner speed — on a loop laid to
one view it is the radius and not the driver that sets the pace, so a boxier loop is a slower convoy.

**GEN-1** `P3` Generation is driven by the **world seed**, supplied manually or chosen randomly; the same
world seed produces the same city.

**GEN-1a** `P4` A city's streets are generated with everything else about it. A map that measures one thing is
laid in code instead, and the two are one kind of thing from the plan onward: nothing downstream may ask
which of them it is looking at.

**GEN-1b** `P7` No city is built until one is picked: the game opens on a start menu listing the maps, and
nothing a reader has not chosen is built. **What the menu is drawn over is the idle ring** (`IdlePlan`,
`Game.IdleMap`) — the one map the game stands up without being asked, because it is laid to be looked at
and costs a fraction of a city — and the menu stays up over it, in either configuration and on either
head, until a map is picked. A run handed a map on the command line or in the query string opens on that
map instead, and the menu shuts onto it.

- **What the panel itself is laid as is [app/hud](../../app/hud/docs/requirements.md)'s** — one size, one
  place, one page, no way to shut it. What is this slice's is the field it is laid to fit: **the field is
  rectangular because the panel is** (`IdlePlan.CornerShare`), the loop being rounded off a square rather
  than drawn as a circle, so a panel wide enough to read does not have to be short enough to clear a curve.
- **The ring is framed like any other town** (`OBS-1b`): the panel is in the middle of the screen and the
  middle of the ring is the field inside it, so what the menu covers is the field and the road is on screen
  either side of it.
- **The menu is up before any town is** on either head (`WEB-6`, `WEB-9`). A page hands the browser its
  animation callback before it lays anything: the menu stands on the few small files the boot fetched, and
  the ring behind it is generated while the reader is looking at it.

**GEN-2** `P6` Terrain, objects and agents are placed **plausibly**: the result must read as a small town, not
as noise.

**GEN-2a** `P6` A building stands along the street it fronts, not along a compass axis, and its entry point
sits between its front wall and the kerb on walkable ground clear of both. On a town whose streets run at
an angle the alternative reads as a field of sheds, and a door flush against a carriageway is one nobody
can walk out of.

**GEN-2b** `P3` **A map ends at its own edge, and nothing it carries stands past it.** The extent is the whole of
the world: there is no ground beyond it to walk, drive, classify or draw, so anything laid outside is a shape
hanging over the void. **What a stage draws through past the edge it cuts before the map carries it** — a
shoreline is drawn well past the town because a bank that closed inside it would be a lake, and the outline
the plan carries is that shape cut to the extent.

**GEN-2c** `P6` **Water meets the land at a shore and never at the grass.** A strip of shore of the town's own
width (`SimConfig.CityGen.ShoreWidthM`) runs along every bank, and **nothing the town scatters or builds
stands on it** — it is not the grass those take. The bank it follows is **the same wave the water is drawn
from**, laid a shore's width wider, so the strip is one width everywhere rather than a band anybody has to
fit round a curve.

**Each of its two edges carries a line of its own** (`SimConfig.CityGen.ShoreEdgeWidthM`), and **each line
takes the colour of the ground it meets** — green where the shore meets the grass, blue where it meets the
water — and is **darker than that ground**, so an edge reads as the shore's own shadow on it rather than as
a highlight laid over it. **They are drawn and never classified**: a line is a picture of an edge, and the
ground under it is the shore either way, so the map carries the rings that leave them rather than a kind of
cell nobody could stand on.

**And a bank is drawn through enough points that no chord of it stands off the curve**
(`SimConfig.CityGen.ShoreChordToleranceM`, half a cell — the finest the ground under it is classified). How
many that is, is derived from the wave's own curvature and is never a count kept true by eye: a wild meander
is drawn through more points and a straight coast through few.

**GEN-3** `P6` Spacing must leave the city walkable: every building is surrounded by walkable padding, and no
pocket is too narrow for a pedestrian to pass.

**GEN-54** `P6` **A building stands against the pavement's outer face, and its door opens onto the walk.**
The face is the driven ground's own boundary moved by the figure the pavement is struck at (`TER-7b`,
`GroundRings.WalkEdge`) — the whole town's outer kerb in one set of closed lines, round every block, round
the outside of the town and round the mouth of every rank of bays. **It is walked and never swept**: what
fronts a street is found by following the line the concrete stops at, so nothing about a building's place
knows that a road, a junction, a roundabout or a car park exists, and all four are fronted on the same
terms.

- **The front wall stands on the walk's own kerb** (`SimConfig.BuildingLineM`, `TER-3c.2`) and **no part of
  the building is nearer the carriageway than the face is** — its own street's or any other's. What holds
  it off is the distance the walk was struck at, read off the boundary, because the ground answers grass on
  the concrete (`GroundShapes.At`, [the known gaps](../../../docs/index.md#known-gaps)).
- **The way in is on the line the walk beside it runs down** (`SimConfig.BuildingWayInM`, `GEN-2a`,
  `GEN-5`): the walking lane furthest from the carriageway, which is the one that passes the door. Clear of
  both kerbs, and on ground a body is actually held on rather than merely walkable.
- **It is square to the face there** and so to the street it fronts (`GEN-2a`), and **sized by the roof it
  will wear** — the footprints come from the catalogue that read them off the sheets
  (`CityGen.BuildingSizes`), so the picture drawn on a building is the size the plan authored rather than
  the nearest thing to it.
- **Every place the face affords one is cut before any of them is filled, and they are filled in a drawn
  order.** Filled ring by ring instead, a town whose brief asks for fewer buildings than its frontage
  affords is built solid along whichever rings came first and empty everywhere else.
- **It runs after every stage that lays driven ground and before the props** (`GEN-10`, `GEN-6b`): the
  boundary is settled when it starts, so a building is cleared against the ground the finished map answers
  with; and a verge crowded by buildings carries fewer props, which is the scatter taking what is left.
- **A count the ground cannot carry is what fitted** (`GEN-8`), reported and never retried.

**GEN-5** `P3` Connectivity is a hard constraint: the walkable terrain reachable by pedestrians forms **one
connected region**, the drivable terrain likewise, and every building entrance and every parking space
attaches to those regions.

**GEN-5a** `P6` **No road of a generated town ends in nothing.** A junction of one arm is a dead end (TER-5a),
the one junction a town sizes around a turning circle rather than around a crossing, and a generated town
lays every junction as the crossing its arms make — so a car driven into one could never leave it. The ends
a lattice and a spoke leave over are therefore deleted, each with whatever is left hanging off it (GEN-8),
rather than grown on to meet something or kept as cul-de-sacs nothing planned. **A map laid in code may
carry one**, because it lays the ground that dead end needs along with it. **And a node a car can arrive at
and not leave is one of these however many arms it has**: a one-way street can make a junction a dead end
without changing the count, which is what the layout's own pruning asks of the flows and not of the arms.

**GEN-6** `P4` Counts are a property of the map, never of a rule. A map declares its own size and roster;
everything else scales to the layout — props to the ground left over, parking to GEN-4b's relation, lights
to what conflicts, crossings to where the walkable region would otherwise split.

**GEN-6a** `P6` **A prop stands wholly on grass.** Its whole girth and not its centre, because a bench half over
a kerb is a bench in the road; and the same test is what keeps a prop on the map (GEN-2b): off the town is
not grass.

**And no collar over that.** The ground a candidate is cleared against is the ground that is drawn (TER-7),
the walk's own re-entrant corners included — so a candidate that clears it is clear, and a margin on top
would only hold the verge back from the street it is a verge of. Every pass used to owe one, because the
ground was read off a raster painted from the pieces alone: the corners were in neither, and a prop cleared
against the cells could be standing in the middle of a drawn one.

**GEN-6b** `P6` **A prop's kind is a placement and not a picture, and the pass that laid it is what decides
which.** A stump and a planter are different kinds because they stand in different places, not because they
look unalike. **The props are laid in two passes**, and everything they need — the roads, the pavement, the
bays and the buildings — was laid before either of them runs.

- **First the walk's outer face is walked**, because a verge is a line and not an area: what stands along
  one is found by following the line the concrete stops at and never by sweeping the ground and asking each
  square whether it happens to be near a street. A candidate stands out in the **verge** — the band of grass
  between `SimConfig.CityGen.PropVergeNearM` and `PropVergeFarM` beyond that face — and it is **furniture**
  or **planting** by the share the town furnishes its streets at (`PropFurnitureShare`).
- **The face is the town's own boundary moved by the figure the pavement is struck at** (TER-7b,
  `GroundRings.WalkEdge`) and never a line laid again beside a road. A road's half-width is the ground its
  lanes were laid inside and says nothing about where the concrete ends; the face says it everywhere at
  once, so a junction, a roundabout's island and a bay are lined by the same walk that lines a straight,
  without this rule knowing that any of them exists.
- **The band is measured to the prop's own near rim.** A prop owes GEN-6a its whole girth on grass, and the
  concrete is a figure the boundary was struck at rather than something the ground answers with — so what
  holds a prop off the walk is the distance it was placed at. A narrow look reaches the near edge of the
  band and a wide one is pushed out by its own width.
- **Then the ground the town is not on is swept**, on the stratified lattice, and everything within
  `SimConfig.CityGen.PropWildStandOffM` of the paving is left to the first pass. What is laid there is what
  grows **wild**. **The stand-off is past the verge and not up against it**, so the strip between the two
  passes reads as the edge of the town rather than as one scatter quietly changing what it is made of.
- **A prop laid along the face carries the face's own bearing there**, and a look with a front — a planter,
  a skip, a stack of crates — is turned onto it, so it runs with the street it stands beside rather than
  with the compass.
  **Whether a look has a front is the art's to declare** (`PropVariant.Turns`) and never the placement's to
  assume: a tree seen from above has none, and turning one makes the same look read as several. **No wild
  look may declare one**, because the pass that lays it has no bearing to give it.
- **A verge is not planted end to end.** A share of the planting on any verge is drawn from the wild set
  instead (`SimConfig.CityGen.PropWildOnAVergeShare`), because a street carrying only the things a town
  plants reads as a catalogue laid out along the kerb, and a self-sown bush at a kerb is the commoner sight.
  Such a prop is a wild one standing where the town put it, so it is drawn upright like every other.
- **The pitch the face is walked at is longer than the props are wide**, so what spaces a verge is the step
  rather than the props' own girth against each other (GEN-6c). A kerb walked at a pitch inside a girth
  carries a prop wherever one will fit, which is a continuous line of them and not a street; the town has to
  be visible through its own verges. A stretch crowded by buildings or bays carries what is left of that
  scatter.
- **A kind carries its own size band, because its set was authored in one.** The wild set reaches the great
  trees (`SimConfig.CityGen.PropWildDiameterMaxM`) and the other two stop at the widest thing drawn for them
  (`PropDiameterMaxM`). A prop drawn outside its own set's band is a planter stretched to the size of an
  oak: the catalogue answers a size nothing was authored near with the nearest look it has and never
  resizes the prop to match. A wild look on a verge is therefore drawn as wide as it would be anywhere
  else, which is what a street tree is.
- **A look that fits none of the three is not shipped.** The kinds are the whole of what a prop may be, so
  art that could only ever stand somewhere a prop is not laid — a grate, a hatch, a patch of paving — is
  deleted rather than filed under the kind it is least wrong in.

**GEN-6c** `P6` **Two props stand a clearance of grass apart, girth to girth**
(`SimConfig.CityGen.PropApartM`). Sharing ground is the floor of it: two discs laid over each other are one
obstacle drawn twice, and what a reader sees is a bush growing out of a tree. **Merely not touching is not
enough either** — a prop is a picture as well as a disc, and a row of them laid rim to rim along a kerb reads
as one long thing rather than as several, where a verge wants to be seen through.

**A grid of the scatter's own is what makes the rule cost nothing.** The two passes lay on patterns that know
nothing of each other (GEN-6b), so neither pattern can be the index: the squares are the widest prop's own
width and one clearance across, and a candidate is asked about the nine round it and about nothing else —
anything further off is further away than the rule can care about, whatever the jitter did. **The prop
already laid is the one that stays**: nothing is nudged aside to make room, and a candidate that would come
too near one is simply not a prop (GEN-8, GEN-10).

**GEN-6d** `P6` **A prop's picture fits inside the disc the plan kept for it**, so the longest side of the
sheet is the prop's own `diameterM` and the other follows the art's aspect. **What is drawn is what a car is
held off**: a sheet drawn to its own height instead, half again as wide as it is high, reaches half a metre
past the girth the town gave it — standing in the prop beside it, and in the road when it is laid along a
kerb. The clearance in GEN-6c is grass between two pictures and not slack for one of them to spill into.

**GEN-9** `P4` A building **declares what it is for**: ordinary, or one of the uses a service is stood at — a
hospital (AMB-1), a police station or a depot (SRV-1). It is a field of the record and therefore a fact
about the map, the same for every run of it and for every agent seed, and it moves only when the map does.

**Which buildings they are is settled as the town is laid** (`GEN-55`), and it is settled by where their
parking went rather than by a sweep over the buildings afterwards.

**A building serves one use at most**, which one field settles: a byte cannot say two things. A map that
declares none of them is a map whose services do nothing, and that is a state the census reports rather
than a state anything papers over.

**GEN-55** `P5` **A service building is stood at the end of the car park that was cut for it.** A hospital,
a police station and a depot each need somewhere for their own vehicles to stand (`GEN-4k`, `AMB-1`,
`SRV-1`), so the parking comes first and the building is stood on it — rather than the buildings being laid
and one of them then found to have a car park outside it.

- **A yard is a car park of its own** (`GEN-53`): one rank, on one side, as wide as a car park gets. One
  side because there is one building and it stands past the far end of the rank; the widest because the
  apron a station holds is the bays nearest its door, and a yard of three bays stands three vehicles.
- **The yards are cut before the town's own car parks and out of the same count.** Each car park is cut at
  the site furthest from every car park already cut (`GEN-53`), so taking the services first is what puts
  them as far apart as the town's roads allow — and it needs no spacing rule of its own. **A town with
  fewer car parks than its roster asks for services stands fewer services** (`GEN-8`), which the census
  reports (`AMB-2`, `SRV-2`).
- **How many there are is the roster's share of the buildings the map plans** (`GEN-6`,
  `SimConfig.HospitalsFor` and its pair), which is the same figure the fleets are laid off
  (`World.Statics.BuildingRoster`) — a vehicle and a crew apiece, so two readings of it would be an
  ambulance with no hospital to go home to.
- **The building stands on the rank's own normal**: past the far end of it, square to the street, **in the
  middle of it**, with its door on the walk that wraps the rank. **That is the face again and not a second
  placement** (`GEN-54`) — the pavement wraps a rank of bays exactly as it wraps a street, so a hospital is
  stood against the boundary on the terms every other building is, and what differs is only which stretch
  of it and that this one is centred rather than stepped to.

**And nothing is built on the rounding round the end of a rank, or down its side.** The stretch of face a
car park adds is the flat over the line its bays end on, a rounding round each corner of the rank, and the
two sides running back to the street. **A building fronting a car park stands on the flat, and the whole of
its frontage stands there** — the limit is the rank's own reach along the street and not a figure of its
own. One perched on a rounding fronts the mouth of the car park at an angle and one down a side fronts the
row of bays edge-on, and neither reads as a building beside its own parking.

- **So a rank narrower than the frontage offered carries nothing behind it** (`GEN-8`). What is behind three
  bays is a strip of verge, and the building the draw wanted there is not built — which is the rule holding
  rather than failing.
- **It is why a service is centred rather than stepped to.** The face is walked at a pitch (`GEN-54`), and a
  hospital as wide as its own yard has to be on the middle of the flat to the metre; the station the rank's
  normal reaches is therefore slid along that flat to the middle before the building is stood on it.

**GEN-7** `P5` Initial state: cars start **stopped in parking spaces**, and **a person starts inside the
building the map stood them at**, dwelling out the interval an arrival dwells (PER-11).

**It closes the loop rather than adding a stage to it.** A trip ends by walking through a door and
dwelling, so a body that begins there begins in the state every later trip returns it to, and everything
after the first dwell is the ordinary round: out of the building, to the destination, in
(`PER-25`). Started on the pavement instead, everybody's first leg was a leg no rule
of theirs had drawn. **Which building is read off the pose the plan left the body in** — the way in it is
standing at — so the format carries nothing to say it. A door with no room behind it leaves the body
standing outside it, which is a state that already has a name.

**How many of each is the brief's**, and **the bound is the town rather than the count**: a car is stood
on a lane long enough to hold one and a person at a way in, so a brief asking for more than the ground
carries gets what fitted (GEN-8).

**GEN-8** `P6` **No candidate city is ever rejected.** A violation of GEN-3…GEN-5 is a defect in the
arrangement rather than a seed to throw away, and the gate that catches it is the suite. Where the ground
cannot afford what the brief asked for, **the town is what fitted and the shortfall is reported** — by the
census, as every other absence is — and where a piece of a town is left joined to nothing, that piece is
deleted rather than linked up to whatever is nearest.

**GEN-10** `P4` **Every stage of a generation runs once**, in the one order they can run in: the water before
the nodes that avoid it, the districts before the streets laid inside them, the car parks before the
buildings stood on them, the roads before the boundary the buildings front, the buildings before the props
that take what is left, and the bays before the cars standing in them. **A stage constrains the next rather
than checking it afterwards** — which is what makes the
properties GEN-3, GEN-4 and GEN-5 name true by construction rather than true on the attempt that happened
to pass.

**A road is drawn as the link is offered, and a link the line cannot be drawn for is not a road** (GEN-47,
GEN-49). Nothing is laid and taken back: no pass refuses a road once the town stands, no repair runs behind
such a refusal, and nothing is drawn twice — so what the layout holds at any moment is roads that can be
drawn where they stand. **The stages that change a road's shape ask first**: a run is offered as the one road
it would be before its pieces are given up (GEN-51), and a junction is opened into a ring only once every arm
the ring moves has been drawn where the ring would leave it (GEN-19).

**What a refusal costs is the road and the connectivity behind it, and never a second attempt at the town.**
The pieces a refusal leaves joined to nothing are deleted (GEN-8, GEN-5), and the reading of how much the
town paid — the junctions nothing meets at, the runs that could not be joined — is the census's to report.

**GEN-11** `P4` **Each stage draws on its own stream of the world seed.** Retuning what one stage does may not
move what an earlier one laid, so a change to the props cannot reshuffle the roads and a map is the same
town every time it is opened.

**GEN-46** `P3` **A lane ends where the seed says it ends, and the road is laid to meet it.** Every arm of
every junction carries a **connection point** for each way its road is driven (TER-4d): they stand on the
line square to a bearing drawn for that arm, a standoff (`SimConfig.CityGen.ConnectionStandoffM`) out from
the node, half a lane either side of it on the side the traffic keeps. The bearing is the chord to the
neighbouring node turned by an angle drawn inside `SimConfig.CityGen.ConnectionJitterDeg`, tapered on a
short link by what that link has room to turn through. **The two ends of a link are drawn independently**,
so a road has two bearings to satisfy and they do not agree.

- **They are stored nowhere and drawn again wherever they are wanted**, from the world seed and the two
  junction centres the link joins — which is what lets the lanes stay derived from the plan while nothing
  derived is written to disk. One function, two callers, one answer.
- **Everything a link is drawn with is keyed on the link and never on a walk over the town** (GEN-11): the
  arm's jitter and the road's own wander both. A road's shape is then a function of its two node centres, the
  places it passes and its class, so offering the same link twice draws the same road and deleting one road
  moves nothing that stayed — which is what lets a road be drawn as it is offered rather than after the town
  stands (GEN-10).
- **A bridge and a ring piece take their bearings rather than drawing one** (GEN-14a, GEN-19). Both are
  shapes settled elsewhere, so the jitter is nil for those links and a ring arm's lead bends with its own
  circle rather than leaving straight off the tangent to it. **The bend it takes is the ring piece's own** —
  the curvature the layout sized the circle at, and the curvature that piece's arc carries — never a circle
  fitted back through three of the ring's nodes, which is the same figure worked out a second way.
- **The disc a junction is drawn on is the standoff** (`SimConfig.JunctionRadiusM`) and the arms follow it.
  Sized off the arms instead, the standoff would be read back off a disc sized by the arms that end at the
  standoff.

**GEN-47** `P3` **A road is the line that leaves on one of its bearings and arrives on the other**, bending no
tighter than the radius its own class's design speed affords on tarmac (`SimConfig.CarCorneringRadiusM`),
which is derived from a speed and a grip and is never authored as a radius. How much it wanders between
them is drawn from the district it runs in: a strict district lays near-straight roads and a loose one lays
curves, bounded by the block spacing so no street may reach the one a block over. **A road that cannot be
laid inside those bounds is not laid**: the wander gives way first, and a link that still cannot be met is
deleted with the layout repaired behind it (GEN-8, GEN-5). **A roundabout's ring has a floor of its own**
(GEN-19), because the whole of one is a corner.

**A corner a road was joined through has a junction's floor and not a class's** (GEN-51, GEN-48). It was a
junction, and what a car held there was the movement across it; measuring it against what the road's own
design speed affords would refuse the road for a bend the town already had and put the junction back. **The
road is what says so either way** — a driver reads every arc of the line ahead of it and is down to that
arc's own cornering speed before it arrives, so a corner is driven at the speed it affords wherever it
stands.

**GEN-48** `P3` **A junction offers the movements a car at its own design speed can hold**
(`SimConfig.CityGen.JunctionDesignSpeedMps`), and no others: between every point a car enters a node on and
every point of a *different* arm it leaves on, the line a car drives is laid and the turn it makes is
classified. A pair of points on one arm is the turn in the road TER-5f bans and is never joined, which is a
fact about the arm and not an angle to be measured. **But a junction may not refuse its way out of being
reachable** (GEN-5): a lane whose every movement is tighter than the bound keeps the loosest of them, and so
does a lane every movement onto which is, because a car that arrives has to leave and a lane nothing reaches
is a hole in the drivable region.

**GEN-15** `P4` **A lane is the width the town is laid in, and every road is laid at it.** A carriageway is as
many lanes of the one standard width (`SimConfig.LaneWidthM`) as it has ways — two both ways and one
one way (TER-4d) — and the walk beside it two walking lanes of theirs
(`SimConfig.WalkingLaneWidthM`), whatever the road is for and wherever it stands: a town whose roads each
chose their own width is a town where nothing quoted against a lane — a line's offset, a kerb, a bar's
span, the room a body has to step round another — means the same thing twice. A map laid to measure one
thing may still lay ground of its own, because a pad driven in circles is a surface and not a street.

**GEN-13** `P6` **A junction's arms stand square enough to be a junction.** An arm that would lie against one
already there is refused, because two carriageways meeting at a shallow angle overlap for tens of metres
and the fillet, the crossing and the bar on either of them are then laid over the other. What that refusal
leaves unreachable is deleted with its own piece (GEN-8).

**GEN-51** `P4` **A junction is a place roads meet, and a road runs between two of them.** A node two roads
merely carry on through is not a junction: it is where the town's own arithmetic stopped a line — a spacing
along an arterial nothing welded onto, a lattice point the prunes left holding two of its four arms — and
the two arms there are **one road**, laid through the place the node stood so that the corner the town had
is the corner it keeps.

It is a rule about the layout and not about the picture. A junction is a standoff every lane ends at, a
movement between each pair of arms, a claim on the ground those take and a place the router plans through,
so a node nothing meets at is all of that laid in the middle of a road — and the town's junction count, lane
count and claims then say more about where the generator's arithmetic landed than about the town.

**It holds however sharply the two arms meet and whatever they are.** A corner is not a reason to leave a
junction standing, because the corner was a junction and a car took it at a junction's speed — so a joined
road turns at a junction's floor (GEN-47) rather than at its class's. A street and an arterial are joined and
come out as the arterial (GEN-16); every road runs both ways when this is settled, so which of them later run
one way is GEN-18's and is asked of the town this leaves.

**A run is offered as the one road it would be, and where that road cannot be laid the run is cut in two**
(GEN-10): the place in the middle of what it passes stays the junction it was and each half is offered in its
turn, so a corner that cannot be drawn costs that corner and never every place the run went through.
**Nothing is laid and taken back** — the joined line is drawn before the pieces are given up, and a run
offered as one road is held to the ground of the roads that stayed and of the runs joined before it, because
two joined roads could each clear the other's *pieces* and still be laid into one another.

Two kinds of node stand whatever they carry, and neither is a place the town's arithmetic stopped a line:

- **A bridgehead** (GEN-14a), where the carriageway becomes a deck. A bridge is its own road between its
  own two nodes, laid straight over the span, and is never part of a longer one.
- **A ring node** (GEN-19), which is a piece of one junction laid out as a circle.

And **one shape a road cannot be** leaves one node: a run that comes back where it set off has no second end
to be a road between. It is shortened by one place and the rest of it is still joined.

**Nothing else is a reason.** Two roads already running between one pair of junctions is not one, however
awkward the pair is to merge a boundary through — a merge that cannot close round a shape the town lays is
the merge's to answer for (TER-7b, `LaneShell`) and not a licence to lay the town differently.

**GEN-52** `P3` **A junction may be cut into a road that already stands, and the road does not move.** The
road is parted at a place along its own line into the stretch before the junction and the stretch after it,
the node stands between them, and the town gains whatever arms the cut asked for. **Every metre that was
driven is still driven**: the two pieces are the arcs the road was laid as, cut, and the movement the new
junction draws straight across is a biarc between two poses of one arc, which is that arc. It is the one
place the plan is changed after it is laid, and it is allowed because it changes nothing.

**It inverts the inversion, and the plan says which roads it inverted.** Everywhere else the arms are drawn
first and the road is laid to them (GEN-46); here the road came first, so **both arms of a cut road are read
off the line it already carries** — the stand point is where that line ends, the bearing there is the line's
own, and the lead is the one arc joining the node to it (`CityPlan.RoadArrays.Cut`, `ConnectionPoints`).
**Both and not the cut one**: an arm is drawn toward the far end of its own road, and a cut moves that far
end, so a piece that went on drawing its other bearing would draw one its line was never laid to.

- **A cut lands inside one arc of the road, with the cut's own standoff of that arc either side of it.** A
  lead is one arc, so a node placed across a joint in the line would be one whose arms stand where the road
  does not go — and that is what makes the reading exact rather than a curve fitted through the ground.
  **How far back the road is parted is the cut's to ask for** and not the standoff every other junction
  keeps: a junction whose arms are spread along the street has to stand off the whole of them (GEN-53).
- **A cut node is a junction like any other**: it stands a locality clear of every node that is not the
  cut's own (GEN-16), its arms stand square enough to be a junction (GEN-13), every piece and every arm is
  held to the ground the town already holds (GEN-49), and nothing it lays stands on water or off the map
  (GEN-14, GEN-2b). **The cut's own nodes are exempt from the locality**, on the terms a roundabout's are:
  they are one place laid out with a stub rather than two spacings that landed on the same ground.
- **Nothing is laid and taken back** (GEN-10). Every piece and every arm is drawn and asked about before any
  of it is installed, and a cut that fails one of those leaves the road exactly as it was.
- **It is the last thing done to a layout.** A cut road's arms are its line's own, so an offer weighed
  against the chord it was joined on would be weighed against a bearing it does not leave on.

**GEN-53** `P6` **A car park is a junction cut into a road, and every bay of it is an arm of that junction.**
So a car park of four bays one side and five the other is an eleven-armed junction: the road either side of
it, and one arm a bay. **A bay's way is its arm's lane** — one lane wide, **driven both ways over that one
line** (GEN-4f), because a car drives into a bay over the same ground it comes back out over. The junction's
own arithmetic then connects every bay to every way in and every way out, and nothing downstream carries a
rule about car parks at all.

- **The bays of one side stand in a rank off the street, a lane apart and centred on the node**, a bay being
  a lane wide (GEN-15) and a rank sharing the line between each pair of them (GEN-4c). **A bay is one length
  of ground the whole town over** (`SimConfig.CityGen.BayLengthM`), **and it is longer than the longest
  vehicle the town draws** — the longest and not the nominal, because every bay is one anything in the town
  can stand in, and not much longer than that because a bay driven straight into is not one anything
  reverses into. **So an arm is a bay and the turn into it and
  nothing besides**: where the rank stands off the kerb is what that turn spends reaching it
  (`SimConfig.CarParkArmStandM`), rather than a setback chosen for it.
- **And it is the lane the bays are turned off that the rank stands off, not the line the node stands on**
  (`SimConfig.CarParkBayLeadM`). A street of two ways carries a lane each side of that line, so both ranks
  stand the same reach from it; a street driven one way carries its one lane on the half its traffic was
  moved onto (TER-4d), **so the two sides of it ask for different reaches and get the same clearance** —
  the side the carriageway was moved off stands its rank that half lane nearer. **A road type is never
  assumed here**: how far toward a side that side's lane runs is read off the road being cut.
- **Every arm of a rank runs square to the street, so a rank is parallel and not a fan.** A bay is ground a
  car drives straight into and backs straight out of, so what tells two bays of a side apart is where each
  one's way meets the street and never which way it points — the ways are parallel to each other and their
  far ends lie on one line parallel to the carriageway. **An arm therefore need not leave from the node**:
  its foot stands its own step along the street, and what joins it to the road is the movement the junction
  draws between two lane ends, as for every other turn.
- **A bay joins the street the car park was cut into and nothing else**, so **no movement joins one bay to
  another** — the ground between two ranks is the car park's to cross and not a road with a right of way on
  it, and a turn offered out of one bay into another is a movement no car makes. It is left out of the table
  rather than priced out of it.
- **And it joins the street every way that street runs**: every bay is reached from each of them and leaves
  onto each of them, which is every movement the junction has to offer it — two of each on an ordinary
  street and one on a street the scatter took one way (GEN-18). **That is what the car park's own standoff
  is for** (`SimConfig.CarParkStandoffM`): the street is parted far enough back that the whole rank stands
  inside the junction, so every bay lies ahead of every arrival. A rank reaching past the lane end a
  car arrives on would put its far bays behind that car, and the turn back into one is tighter than the
  junction corners (GEN-5) — a bay laid where it cannot be driven into, which is a bay laid to be taken back.
- **And it is parted no further back than that.** The standoff is **the place the turn into the furthest bay
  of the rank runs out onto the street**, and nothing beyond it: a junction longer than that is ground with
  a movement drawn over it that no car ever turns on, and a stretch of ordinary street taken out of the town
  for nothing.
- **A bay is turned into on one circle of its own, and the junction is that circle's size.** A car parks
  from a standstill, so what decides the line it takes off the street is the hook it makes and not a design
  speed — a share of the car's own parking circle (`SimConfig.CarParkTurnRadiusM`,
  `SimConfig.CityGen.BayTurnInParkingCircles`) rather than the junction's cornering radius, which is the
  answer to a question nobody asked beside a bay. **So the junction is as big as that turn and no bigger, in
  both directions**: the street is parted the rank's reach and one tangent back (`SimConfig.CarParkStandoffM`)
  and the bay's own way begins one tangent off the street (`SimConfig.CarParkBayLeadM`). A turn laid at
  whatever the room between two lane ends affords is one no car would make and a box nothing needed. **The
  bays themselves do not move for it**: where they stand is the arm's own reach past the road's edge, and
  what the turn decides is only how much street the junction takes and how sharply a car comes off it.
- **A car holds the street until its own bay, and leaves a bay onto the street at once.** A movement at a
  bay is **one turn with a straight either side of it** — where every other movement in the town shares its
  turn evenly between the two lane ends it joins, so a car on its way to park would be drifting out of its
  lane across the mouths of every bay before its own from the moment it entered the junction, and one that
  had left a bay would still be curving a box later. **It moves no lane end**: both lines join the same two
  poses, and which of them a movement gets is the whole of the difference.
- **The bays are counted before the place is chosen.** How far back the street stands off is how far the
  longer rank reaches along it, so the size of a car park is what decides which places can carry one. **A
  place is never taken and the bays that did not fit taken back off it** (GEN-10); a town with nowhere to
  put the car park it drew lays fewer of them (GEN-8).
- **A rank is an apron and not a fan of carriageways, so its arms are not held square** (GEN-13). That rule
  measures the angle between two arms and every arm of a rank leaves on the same bearing, so what stands two
  of them apart — the lane between their feet — is not a thing it can see. What they make between them is one
  piece of tarmac, where GEN-13 is about two carriageways lying against each other with the fillet, the
  crossing and the bar of one landing on the other, and nothing is filleted, crossed or barred between two
  bays.
- **How many bays a side carries is a handful or none** (GEN-4b), and **not none on both sides** — a car
  park with no bay either side is a junction cut into a road for nothing.
- **A road that runs one way is cut like any other** (GEN-18, TER-4d). The two pieces are that street parted
  rather than two streets meeting, and what its one direction costs is the bays': each is reached and left
  the one way the street runs, which is every way it has.
- **Nor is a road that bends too far across the ground the junction takes**
  (`SimConfig.CarParkCurvatureMax`). A rank is laid off the tangent at the node and a movement holds the
  street straight, so a road turning away across the box is one whose kerb that rank no longer faces and
  whose lane the straight leaves. **How far a road may bend is read off how far that straight may leave its
  lane** (`SimConfig.CityGen.CarParkOffLaneMaxM`) and off nothing else, so a wide car park — whose longest
  movement runs the length of the whole box and the rank again — asks for a straighter road than a narrow
  one.
- **How many the town has is counted off the buildings it plans** (GEN-6, `SimConfig.CarParksFor`): the map
  says how many buildings it is a town of and the engine says how many of those one car park stands the cars
  of (`SimConfig.CityGen.BuildingsPerCarPark`), so a map that grows carries the parking for what it grew
  into without anybody authoring a second count. And **they are spread rather than scattered**: each is cut
  at the site furthest from every car park already cut, which needs no spacing of its own — what keeps two
  of them off each other is the locality every pair of junctions owes (GEN-16). **What the ground cannot
  carry is what fitted** (GEN-8), reported and never retried.

**GEN-49** `P3` **A junction is the only place two roads may touch.** No road crosses another, runs into the
side of another or lies along one: two roads that are not joined at a junction stand at least one road's
whole width apart (`SimConfig.RoadFootprintM`), **measured between the lines they were laid as**. Ground two
carriageways share outside a junction has no box, no crossing and no stop bar on it, so nothing that drives,
walks or claims a way across it has anything to say about who goes first.

- **It is a bound the laying holds and not a pass that deletes what it missed** (GEN-47, GEN-10): the line a
  link would be laid as is drawn as the link is offered and measured against the lines already standing, so a
  pair that would share ground is a road the town never had. A separation measured on chords would say nothing
  about the roads that were laid, a road free to reach its own end bearings not being bounded by its chord.
- **Which of the two gives way is the order they were offered in** (GEN-16): the arterials are laid before
  the lattice and a stub before nothing, so a street offered against ground an arterial holds is the one
  refused and no pass has to weigh the pair afterwards and choose.
- **It holds from the first road** (GEN-16), because there is no pair of junctions left that is about to
  become one: the nodes are settled before anything is laid, so two roads sharing ground are two roads the
  town may not have and never a pair whose junctions were going to be merged.
- It is a rule about **roads**, not about the paint or the ground: what a junction's own arms may do to each
  other is GEN-13's.

**GEN-18** `P6` **One-way streets are scattered over the whole town, no two of them meet, every one of them
arrives where there is still a choice, and every one the town keeps is one it can still be driven round.** It
is a rule about the **streets the scatter chooses** and not
about every road that runs one way: a roundabout's ring is one direction laid at one place and is none of
this pass's business (GEN-19), though a street taken at one of its nodes is still two of them meeting. **And
the pieces a car park's cut parted one street into are that street** (GEN-53): they meet at the car park and
they are laid on one chord, so they are neither two of them meeting nor two of them crowding. A
street runs one way (TER-4d) wherever the scatter puts it —
no district, bearing or side of the orbital decides it — and the scatter is three relations and nothing else:
**no junction carries two of them**, so each is entered and left on roads that admit both ways; **no two
of them stand within `SimConfig.CityGen.OneWayApartMinM`** of each other, measured between the middles of
the chords they are laid on, which is what spreads them across the town rather than gathering them into one
district; and **each arrives at a junction of four arms or more**. **An arterial never runs one way, nor does
any street that meets one**: they are how the town is
carried between districts, and a district entered off a road that admits one way only is a district drivable
one way round.

**The arrival end is the one that costs the junction something.** A street arriving takes one way out away
from every approach there, so at a node of `n` arms whose others all run both ways an approach is left with
`n − 2` ways on: four arms leaves two, and three leaves one — a car driven through a junction it decides
nothing at, which is a lane with nowhere to choose anything and a shape every layer below would then need a
rule about. **Which way a street runs is therefore where it may arrive**, and one that may arrive at neither
end is not taken. **The departure end is asked nothing**: a street leaving takes no way out away from
anybody.

**And a street the scatter takes has to hold its own bends on the half it is driven** (GEN-47, TER-4d). A
one-way street stands on half of the carriageway it was laid as, and a line moved half a lane towards the
inside of its own bend is a line half a lane tighter there — so a street laid near its class's floor is one
the scatter may not take, and it is asked of the line offset the way the plan will offset it rather than of a
figure standing in for it.

**What the town keeps is settled against the movements and not against the roads.** From every movement on
the network every junction must be reachable, with no turning round in the road (TER-5f) — a block whose
streets all ran inwards has a way out of every junction on it and is still somewhere a car drives into and
never leaves. A street the town cannot afford **runs both ways again** (GEN-8), one at a time and in the
order they were chosen; nothing is laid twice and no seed is thrown away, and opening one costs the scatter
a member and never its shape.

**GEN-19** `P6` **A roundabout is a ring of ordinary junctions joined by one-way arcs, and nothing else.** It is
not a kind of junction, not a shape the plan carries and not a rule anything downstream has to know: a node
is opened out into a circle of nodes — one for every road that met it — joined into a closed carriageway
driven one way round, and each of those roads is cut back to meet the ring **on its own bearing**, so
nothing bends to reach it and every arm arrives square to the circle. What the plan then carries is roads
and junctions, and the only thing said about the ring is **which of its roads circulate on it**, so that a
one-way road the scatter took (GEN-18) can be told from one nobody chose.

- **Circulating traffic is driven over what is entering, and nothing grants it that.** The two ring arcs
  at a node are two pieces of one circle, so the movement between them goes straight on where the movement
  in off the arm is a turn, and the ranking does the rest (TER-5e). It is the whole reason a roundabout is
  worth laying as a circle rather than as a polygon, whose every circulating movement would be a turn.
- **Which way it is driven is which side the traffic keeps**: the island stands on the side a car does not
  drive against, so a car goes round it turning away from its own kerb.
- **Nothing on the ring is lit** (TLT-3). A timetable over a ring node stops the circle to let an arm in,
  which is the one thing a roundabout is laid instead of.
- **Nothing fronts one.** A ring is one junction's worth of ground: a door on it opens onto circulating
  traffic and a car park on it is a lot entered off a junction, so no frontage is cut along one (GEN-4b).
- **Every piece of the ring is one arc of one circle, node to node**, so the ring is smooth: there is no
  straight in it and no join a reader can find. Its bend is never tighter than the radius the roundabout's
  own design speed affords (`SimConfig.RoundaboutDesignSpeedMps`), which is the exception GEN-47's floor
  names for it — the whole of a roundabout is one corner — and it is the one road laid to its own circle
  rather than to the bearings its arms were drawn with (GEN-46).
- **What that costs is that the ground of its entries is struck on a curve.** A junction's kerb fillets are
  the arcs tangent to the two kerbs there, and a kerb that bends is a circle rather than a line — so the
  corner is solved between the shapes the kerbs are drawn along and never between the lines their bearings
  make (`Furniture.Kerb`). Struck on the lines, a ring's entries are filleted to points off their own
  tarmac and the pavement round it comes apart at every one of them.
- **Nothing at all is painted on the ring.** No zebra (TER-6): a walk laid across the circulating
  carriageway is a walk across the traffic a roundabout exists to keep moving, and the entries carry the
  crossings, which is where somebody getting round one crosses. **What that leaves is an island nobody walks
  onto**, which is what an island is: nothing stands on it, nobody is put down on it and no trip ends there
  (GEN-5). And no bar either: a bar is where a driver holds when the junction refuses them, and circulating
  traffic is never refused. **What the ring is, is a circular road with entries and exits and no paint.**
- **A ring node is a junction like any other**, so the road between two of them is a road (TER-5a) and no
  piece of it stands over water (GEN-14) — but **what two of them owe each other is neither that road nor a
  locality** (GEN-16). They are one junction laid out as a circle rather than two spacings that happened to
  land on the same ground, which is the case those rules are about. What they owe each other is what the two
  roads *leaving* them do: the ground one road takes (GEN-49) and a pavement's width of ground on top of it,
  since two mouths whose paving abuts is paving with nothing to wrap round. **That, and its own design
  speed's floor, is the whole of what sizes a roundabout — so the circle laid is the smallest one its arms
  and its speed allow and never a wider one.**

**Where they stand is where a district leaves the town**: a junction of **four arms or more** that some
district's own street meets an arterial at, no nearer another roundabout than
`SimConfig.CityGen.RoundaboutApartMinM`, whose arms all stand inside half a turn of each other and off whose
ground every other road and junction already stands clear. **Three arms are a junction and not a
roundabout** — a circle there is laid to sort out one conflict the ranking already sorts out standing still,
and it charges every car through the node a detour to reach the arm opposite. A bridgehead is never one,
because a deck cannot move (GEN-14a). **Nothing is laid and taken back**: all of that is asked before the
node is opened out, and a node that fails any of it stays the junction it was (GEN-8, GEN-10).

**GEN-50** `P3` **No lane dangles**: every lane the town lays is one a car can be driven onto and one it can be
driven off again. It is a local fact and not a connected one — a movement leaving a node needs some road
other than its own arriving there, and a movement arriving needs some other road leaving — so a node a car
can reach and not leave is a dead end whatever its arms come to, and the layout takes it away with the ones
that carry a single arm (GEN-5a). **Where a road of two ways meets a road of one**, the way back out of that
node is a lane no movement could ever arrive on, since the only thing that could reach it is the turn round
in the road TER-5f bans.

**GEN-16** `P6` **Two of a kind standing inside a locality of each other are one thing and not two**
(`SimConfig.CityGen.LocalityM`). A town is laid at several spacings that know nothing of one another — an
arterial's, a lattice's, a frontage's — and where two of them land almost on the same ground what comes out
is a pair nothing downstream can make sense of: two junction boxes with their fillets, their crossings and
their bars laid over each other, or two car parks with a stride of pavement pinched between them. **Which of
the two is left is decided by what hangs off it**: a junction is merged rather than refused, because
refusing one deletes every road at it (GEN-8), where the second car park is simply not laid — nothing hangs
off a car park, and a lot is a bounded handful of bays rather than something a second one could enlarge
(GEN-4b). A node is a point and is measured centre to centre; a car park is a rectangle and is measured
between the two of them.

- **Two nodes are one junction**, standing where the node the town cares more about stood, and every road at
  either of them meets at it. A bridgehead cannot move, an arterial's line is the town's and a street is what
  bends to meet either — so where that leaves two arms lying together it is the street that is dropped
  (GEN-13), and whatever that leaves hanging goes with its own piece.
- **A cluster and not a pair, and settled before the first road is laid.** Three nodes a stride apart in a
  chain are one place, not a pair and a stranger: asked pair by pair, the third is left where it stood and
  whatever ran through it hangs off a town it no longer reaches. **And it is asked of the nodes rather than
  of the roads at them**, which is what makes it cost nothing — a junction moved once a road stands is every
  road at it drawn again, where a junction moved before any is drawn is simply where the town is.
- **Two car parks sharing a kerb are one car park and not two** — and here the one that stays is the one
  that was laid, because a lot holds a bounded handful of bays (GEN-4b) and two of them joined end to end
  would be an apron rather than a bigger lot. It is measured along the kerb, as every figure about a lot's
  clearance is (GEN-4d): two lots facing each other across a carriageway are the two sides of a street and
  stay two. **This is the one place the rule refuses rather than merges**, and it can because nothing hangs
  off a car park: the second is a lot the town does not have and the census reports the shortfall (GEN-8),
  where refusing a *junction* would take the roads at it as well.

**And a roundabout's own nodes are exempt** (GEN-19). They stand inside a locality of each other on purpose:
a ring is one junction laid out as a circle, placed by one construction, so it is not the accident this rule
is about — and what its nodes owe each other is the road between them.

**It names those two and nothing else.** Everything else a town lays more than one of is already spaced by a
rule of its own — a building by the padding a walker gets past it (GEN-3), a prop by the corner the pavement
turns (GEN-6a) and by its own girth against the props already laid (GEN-6c) — and a second rule over that
ground would be a second answer to it (SIM-7).

**GEN-14** `P6` **Nothing a junction is made of stands on the water.** No node is placed on it, so no junction,
no kerb fillet, no crossing and no bar is ever laid over it, and a town whose middle falls in its own river
moves its middle to the bank rather than building there.

**GEN-14a** `P6` **A road standing over water is a bridge, and a bridge is its own road**: one straight span
between a bridgehead on each bank, no longer than the deck a town builds
(`SimConfig.CityGen.BridgeDeckLongestM`), carrying that deck the whole of its length (TER-3b). Nothing else
crosses — not a street, not a piece of the orbital's own arc, which is laid straight over its span or not at
all. A crossing these bounds refuse is a road the town does not have, and whatever that leaves unreachable
is deleted with its own piece (GEN-8) rather than reached some longer way round.

**GEN-14b** `P6` **A bridge crosses a river and never the sea**, because a coast has one shore inside the town
and a deck laid over it reaches nothing. **And it crosses as squarely as the layout affords**: the wheel is
turned so a spoke runs down the river's own normal, and an arterial meeting the water carries a node on each
bank — so what spans it is the shortest run that path affords rather than the distance between whichever two
nodes the ordinary spacing happened to leave either side of it.

## The maps

**The list of maps is one list**, read by the start menu, by the in-game picker and by the command line;
every check, probe and shot names the map its fixtures live on.

**The fixture map is not optional.** It is what every detailed check is staged on: small enough to build
in a fraction of the time, laid so that every kind of ground is on it at least once, and furnished at a
fraction of a city's counts. Detailed questions asked of "whatever the big city happens to contain" are a
different question every time somebody edits the city. **It is a brief rather than a file**, which costs it
the one thing a fixture is for — a town that moves when the generator does — and that cost is named in
[the known gaps](../../../docs/index.md#known-gaps) rather than papered over.

**Ask a whole city the shallow questions only** — it validates, its junctions are junctions, no lit
junction shows two conflicting greens, nothing is laid on its water. Detailed geometry is asked of named
places on the fixture map.

**Every map states what it claims, and a run of it says whether it kept it** (`VER-11`,
[verification](../../../docs/verification.md#what-a-map-claims-about-itself)). A map that is laid to
measure one thing claims that thing and nothing else, which is the whole of what makes it worth shipping:

| Map | What it claims about itself |
|---|---|
| `Idle`, `Test`, and every city | The two every town owes: nothing is left inside anything else, and no car stands still with no clock running for it |

**The fixture map and the idle ring claim nothing of their own on purpose.** One is where the detailed
checks are staged rather than a map with a question; the other is laid to be looked at and measures
nothing. A claim invented for either would be a claim the suite already asks better somewhere else.
