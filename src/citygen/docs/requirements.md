# The town plan — requirements

**One data structure describes a complete city.** A builder instantiates the world from it, a validator
judges it, and a file format carries it between processes. It is pure data — no engine types, no node
references, no behaviour — which is what lets validation run headless and a new map be authored without a
code change. This is the most load-bearing structure in the project.

Bay geometry is [world/parking](../../world/parking/docs/requirements.md); the ground is
[world/terrain](../../world/terrain/docs/requirements.md); roads and junctions are
[world/road](../../world/road/docs/requirements.md).

## Rules the structure enforces

- **A junction's kerb fillets are carried, not re-derived.** A kerb fillet cannot be read back off any
  other shape, so it is a record. **The pavement has no corners of its own to carry**: it is the tarmac
  grown by one figure and every corner it turns is a corner of the thing it wraps (TER-3c.3). The structure
  still carries a list of them, because the two fixture maps that arrive as files carry one; nothing reads
  it and nothing writes it.
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
- **The reader lives with the structure it produces**, not with the cursor that walks the bytes: a
  `.town` file is a plan, and `core/` does not know what a town is.

## Where a town comes from

**A city is generated from a brief when it is opened** (`TownGenerator`). What is authored is a
`TownBrief` in [towns/](../../../towns/) — a seed, an extent, the water it stands on, how many districts and
how strictly they are laid out, and how many of everything — and what a reader sees is whatever that seed
makes of it. **Nothing derived is ever stored**: no district, no node, no curve, no cell and no building,
because a brief that carried geometry would be a second answer to where the town is and the one on disk is
the one that goes stale.

**The maps laid to measure one thing are laid in code**, and none of them is a city: each is arithmetic
over the car's own figures rather than a seed, which is why they are authored where those figures are.
`Maps` is the one list both kinds appear on, and a `CityPlan` is where the difference between them ends.

**The proving ground** (`TrackPlan`) is one closed lap cut into ten roads — five shapes with a link between
each pair — no junction anybody meets at, no light, no paint, no pavement and nobody living on it, only
fifteen people and six cars.

**The driving exam** (`ExamPlan`) is the other question: not what a shape of road costs a car but **what a
car does where roads meet**. It is a six by six lattice of junctions with one crossing manoeuvre staged
at each, laid from the thirty-six cards of `ExamCards` — a card names the arms its cars come from and
leave by, what else is coming, and the one claim it makes about the first of them. What the map carries is
therefore decided by the cards: the spur that makes a cell's junction a crossroads rather than a T, the
lights over the junctions whose cards are about lights, and somebody standing at the paint a card is about.
**Four of its junctions are lit and the rest are not**, because a lit box is one where the timetable decides
and the box worth staging over and over is the one where the ranking alone does (TER-5e).
Every car on it is one look and one build (CAR-11a), because a card is read against another card and a
fleet of different weights would be a second variable inside every comparison.

**The walking exam** (`FootwayPlan`) is the same question asked of the other agent: not what a car does
where roads meet but **what somebody on foot does there**. It is the exam's own lattice — five rows of four
cells — with one walk staged at each and **nothing driving on it at all**, laid from the twenty cards of
`FootwayCards`: a card names where a body is put down, where it is sent, and the one claim it makes about
the first of them. The two exams share their ground (`ExamGround`, `ExamMap`) and differ in their cards and
in who is stood up, because a junction a walker cannot get round and a junction a car drove through must be
the same junction. **Nothing drives on it on purpose**: what a car owes a crossing is the driving exam's
question, and a walk that failed with traffic on the map would leave a reader unable to say which of the two
agents was wrong.

**The lap is laid three times, and each differs from the others in exactly one thing**, so a figure that
moves between two of the tables is a fact about that one thing. `Track` stands fifteen people beside the
carriageway, where each paces into the lane and back and is what brings a car to rest and lets it go again
without anything staging it. `Drunk` stands the same fifteen in it, where each reels down its own lane and
stands where it stopped every few lurches (`PER-16`) — a driver follows something slow and then gets past
it, which is the only place in this town anything ever does (`E-4`). `Fleet` carries nobody on foot at all
and swaps `Track`'s six of one car for one car of every look, each at its own weight, footprint, axles and
handling: the first two measure a driver stopping for what is in front of it, and this one measures the
car. All three are written by the one command, because any of them going stale is a probe quoting a road
this build no longer lays.

**The skidpad** (`SkidpadPlan`) asks neither of those: not what a road costs a car and not what a junction
does to it, but **what a car's own steering is worth**. It is a hundred-metre grid of nothing but road — a
column for every look the fleet ships, a row for every way of driving a circle — and every car on it has
its wheel held hard over to the left for the whole run while its row holds the pedal — half of it and all
of it, in each gear. **Nothing on it drives anywhere**: the town holds the wheels itself
(`TownWorld.HoldTheWheels`) through the seam a player's hand uses, so what each car does is its own body
answering one command. Its two instruments face each other — the circle the axles ask for, drawn over the
car by the turn-circle layer (`OBS-2j`), and the circle the tyres actually described, written on the road
because **every wheel on this map marks the ground it stands on** (`MarkFigures.PadFloor`) rather than only
a sliding one.

**The gap between the two is read against a third circle rather than against nothing**: what this car's own
lateral grip affords at the speed it reached. A car wide of its axles but sitting on its grip is a car
obeying its tyres, and that is a different finding from one wide of both — so the probe prints the three
radii side by side, with where the centre it is really turning about stands and how far the front wheels
are off the ground they cross. **A car turning *inside* its own axles is on none of the three**: four
rolling wheels cannot describe an arc tighter than the one their axles cross at, so that is a pivot and the
pad quotes how much of the run was one.

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

**The crossings map is neither**, and it is not laid here: `Zebras` arrives as a file like a city does. It
is five isolated streets with a crossing on each and one body apiece, one of those crossings deliberately
laid off square, and it carries no cars and no buildings because the paint is the whole subject — a skewed
crossing is the case that can fail while every square one in a city passes.

**Two fixtures still arrive as files**, and the reader is what opens them: the fixture map every detailed
check is staged on, and the crossings map. Neither may move when the generator does, which is exactly what
a fixture is for — they are on their way to being laid in code rather than generated, and nothing else is
read from a file.

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

- **The start menu is the same panel laid as the thing it is** (`Menu.AtTheStart`), and not the popup under
  the gear. It stands **in the middle of the window**; it carries **the map list and no tab strip**, since
  one page needs no strip to pick it, with **the way out on the title's own line**; each map's **name is
  written a size larger** and the line saying what it is keeps its size and **wraps**; and **it cannot be
  shut** — there is nothing to shut it onto, so the gear and the legend button are not drawn under it,
  Escape does nothing, and a click off it acts on nothing.
- **It is laid to fit the field inside the ring, and the descriptions wrap into it** — a share of the
  window's short side, which is the side the opening view and so the ring itself are figures across. **The
  field is rectangular because the panel is** (`IdlePlan.CornerShare`): the loop is rounded off a square
  rather than drawn as a circle, so a panel wide enough to read does not have to be short enough to clear a
  curve. The popup under the gear is the one laid so that nothing in it ever breaks, and it is laid to the
  longest description in the catalogue.
- **It is one size and one place whatever is open in it**, and both groups are open when it opens. Its
  height is the field's rather than the list's, so a group shut or opened moves no edge and no row out from
  under the pointer, and a list longer than the panel **scrolls**. The popup under the gear is the one that
  is as tall as its own page, and the one that opens on the places alone (`OBS-2a`) — a mis-click there
  loses a running game, and behind the start menu there is none to lose.
- **The read-out and the scale legend are not drawn over it.** They say what a run *is*, and the ring behind
  the panel is a picture rather than a town somebody opened.
- **The ring is framed like any other town** (`OBS-1b`): the panel is in the middle of the screen and the
  middle of the ring is the field inside it, so what the menu covers is the field and the road is on screen
  either side of it.

- **The two are opened separately and in that order.** A desktop run has both on the disk it started
  from; a page has neither, so it hands the browser its animation callback before it opens anything at
  all — the menu stands on the files the boot already fetched, and the town behind it comes down while
  the reader is looking at it (`WEB-6`, `WEB-9`).

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

**GEN-5** `P3` Connectivity is a hard constraint: the walkable terrain reachable by pedestrians forms **one
connected region**, the drivable terrain likewise, and every building entrance and every parking space
attaches to those regions.

**GEN-5a** `P6` **No road of a generated town ends in nothing.** A junction of one arm is a dead end (TER-5a),
the one junction a town sizes around a turning circle rather than around a crossing, and a generated town
lays every junction as the crossing its arms make — so a car driven into one could never leave it. The ends
a lattice and a spoke leave over are therefore deleted, each with whatever is left hanging off it (GEN-8),
rather than grown on to meet something or kept as cul-de-sacs nothing planned. **A map laid in code may
carry one**, because it lays the ground that dead end needs along with it: the head a car works itself
round in where something drives there (`ExamPlan`), and no head at all where nothing does (`SkidpadPlan`).

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

- **First the paved edges are walked**, because a verge is a line and not an area: what stands along one is
  found by following the thing it belongs to and never by sweeping the ground and asking each square whether
  it happens to be near a street. A candidate stands out in the **verge** — the band of grass between
  `SimConfig.CityGen.PropVergeNearM` and `PropVergeFarM` beyond that edge's own walk — and it is
  **furniture** where there is a car park to stand it beside and **planting** where there is not.
- **A road's two kerbs and a car park's four sides are the same kind of edge**, and the band is measured
  from each one's own walk: a kerb line has the pavement outside it already, and a lot's tarmac has the ring
  of walkable grass a body gets round it (GEN-4d), which its verge begins past. Without this the ground
  beyond a car park belongs to nobody — the road it fronts is a lot's depth away, and the road's own walk
  lands on tarmac there. **All four of a lot's sides are walked and the ones facing the street are refused
  by the ground under them**, because a lot carries its axis and its extent and never which side the road
  was on.
- **Then the ground the town is not on is swept**, on the stratified lattice, and everything within
  `SimConfig.CityGen.PropWildStandOffM` of a walk or a car park is left to the first pass. What is laid
  there is what grows **wild**. **The stand-off is past the verge and not up against it**, so the strip
  between the two passes reads as the edge of the town rather than as one scatter quietly changing what it
  is made of.
- **A prop laid along a paved edge carries that edge's own bearing there**, and a look with a front — a
  planter, a skip, a stack of crates — is turned onto it, so it runs with the street or with the car park it
  stands beside rather than with the compass.
  **Whether a look has a front is the art's to declare** (`PropVariant.Turns`) and never the placement's to
  assume: a tree seen from above has none, and turning one makes the same look read as several. **No wild
  look may declare one**, because the pass that lays it has no bearing to give it.
- **A verge is not planted end to end.** A share of the planting on any verge is drawn from the wild set
  instead (`SimConfig.CityGen.PropWildOnAVergeShare`), because a street carrying only the things a town
  plants reads as a catalogue laid out along the kerb, and a self-sown bush at a kerb is the commoner sight.
  Such a prop is a wild one standing where the town put it, so it is drawn upright like every other.
- **The near edge of the verge is what the ground affords rather than what a figure promises.** A prop owes
  GEN-6a its whole girth on grass, so a narrow look reaches the near edge of the band and a wide one is
  pushed out by its own width — and a candidate that cannot fit is simply not a prop.
- **The pitch the kerb is walked at is shorter than the props are wide**, so what spaces a verge is the
  props' own girth against each other (GEN-6c) rather than the step: a stretch of kerb carries what fits
  along it, and one crowded by buildings or bays carries what is left.
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

**Which buildings they are is settled as the town is laid.** Two things decide a place: a service building
has to have somewhere for its vehicles to stand, and the services have to be spread over the town rather
than dropped into it. Both are known to the stage that cut the slots and filled them, so the sweep that
finds a building with parking outside it and puts the next service across town from the last is part of
laying the map — and a shuffle, which could only ever say which buildings *exist*, would put the one
hospital next door to the police station as often as anywhere else.

**A building serves one use at most**, which one field settles: a byte cannot say two things. A map that
declares none of them is a map whose services do nothing, and that is a state the census reports rather
than a state anything papers over.

**GEN-7** `P5` Initial state: cars start **stopped in parking spaces**, and **a person starts inside the
building the map stood them at**, dwelling out the interval an arrival dwells (PER-11).

**It closes the loop rather than adding a stage to it.** A trip ends by walking through a door and
dwelling, so a body that begins there begins in the state every later trip returns it to, and everything
after the first dwell is the ordinary round: out of the building, to a car if the trip is worth one
(PER-10), to the destination, in. Started on the pavement instead, everybody's first leg was a leg no rule
of theirs had drawn. **Which building is read off the pose the map left the body in** — the way in it is
standing at — so the format carries nothing to say it, exactly as the pacing and the reeling walkers are
told apart by where they were put down (PER-16). A door with no room behind it leaves the body standing
outside it, which is a state that already has a name.

**GEN-8** `P6` **No candidate city is ever rejected.** A violation of GEN-3…GEN-5 is a defect in the
arrangement rather than a seed to throw away, and the gate that catches it is the suite. Where the ground
cannot afford what the brief asked for, **the town is what fitted and the shortfall is reported** — by the
census, as every other absence is — and where a piece of a town is left joined to nothing, that piece is
deleted rather than linked up to whatever is nearest.

**GEN-10** `P4` **Every stage of a generation runs once**, in the one order they can run in: the water before
the nodes that avoid it, the districts before the streets laid inside them, the roads before the frontage
cut off them, the slots before the props that take what is left, and the bays before the cars standing in
them. **A stage constrains the next rather than checking it afterwards** — which is what makes the
properties GEN-3, GEN-4 and GEN-5 name true by construction rather than true on the attempt that happened
to pass.

**GEN-11** `P4` **Each stage draws on its own stream of the world seed.** Retuning what one stage does may not
move what an earlier one laid, so a change to the props cannot reshuffle the roads and a map is the same
town every time it is opened.

**GEN-12** `P6` **A road is a chord that may wander, bounded by three things and never by taste**: its two ends
are straight for the length everything a junction lays across an arm stands on; its wander is bounded by
the block spacing of the district it is in, so no street may reach the one a block over; and nothing bends
tighter than the radius its own class's design speed affords on tarmac (`SimConfig.CarCorneringRadiusM`),
which is derived from a speed and a grip and is never authored as a radius. A corner too tight for that
floor is not laid at all: the road runs straight through it. **A roundabout's ring has a floor of its own**
(GEN-19), because the whole of one is a corner.

**GEN-12a** `P6` **The corner at a node that forks nothing is the exception to both, and it is the same corner
either way.** Two arms and no fork is a road that bends (TER-5b), so the two roads meeting there are swept
into **one arc arriving on one tangent** — each taking half the turn, the node standing at the middle of it
— and that end of each road is therefore a bend rather than a straight. **The turn is the layout's and not
the sweep's**: a car slowed for it when it was a junction to be turned across and slows for it now that it
is a curve to be driven round, which is why the arc may be tighter than the class's floor. It is never
tighter than `SimConfig.RoadCornerRadiusM` — the bend whose inner kerb stands exactly where a junction would
have flared it — and never wider than the floor, because sweeping wider than the design speed asks only cuts
deeper inside ground the layout put somewhere else. **A pair with room for neither keeps its junction**, and
so does a bridge, which is straight and nothing else (GEN-14a). **What the arm carries stands past the
bend**: the paint of such a node is laid on the straight after the arc ends, exactly as everywhere else it is
laid past the ground its junction reaches (TER-6).

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

**GEN-17** `P3` **A junction is the only place two roads may touch.** No road crosses another, runs into the side
of another or lies along one: two roads that are not joined at a junction stand at least one road's whole
width apart (`SimConfig.RoadFootprintM`), measured between the shapes they are drawn as and not between the
lines they were joined on — a street strays off its chord by its own wander (GEN-12) and an arc by its
sagitta, and a pair that clears on the chords still meets once it is laid. Ground two carriageways share
outside a junction has no box, no kerb fillets, no crossing and no stop bar on it, so nothing that drives,
walks or claims a way across it has anything to say about who goes first.

- **The arrangement keeps them apart and a pass deletes what it missed** (GEN-8, GEN-10): the districts are
  convex and the arterials carry a node wherever a street meets one, so a crossing is rare, and the roads
  are offered to the deletion in the order the town cares about them — a street gives way to the arterial it
  crossed and never the other way round (GEN-13, GEN-16). What that leaves unreachable or dangling goes with
  its own piece.
- It is a rule about **roads**, not about the paint or the ground: what a junction's own arms may do to each
  other is GEN-13's, and where the lots and the buildings stand is GEN-3's and GEN-16's.

**GEN-18** `P6` **One-way streets are scattered over the whole town, no two of them meet, and every one the town
keeps is one it can still be driven round.** It is a rule about the **streets the scatter chooses** and not
about every road that runs one way: a roundabout's ring is one direction laid at one place and is none of
this pass's business (GEN-19), though a street taken at one of its nodes is still two of them meeting. A
street runs one way (TER-4d) wherever the scatter puts it —
no district, bearing or side of the orbital decides it — and the scatter is two relations and nothing else:
**no junction carries two of them**, so each is entered and left on roads that admit both ways; and **no two
of them stand within `SimConfig.CityGen.OneWayApartMinM`** of each other, measured between the middles of
the chords they are laid on, which is what spreads them across the town rather than gathering them into one
district. **An arterial never runs one way, nor does any street that meets one**: they are how the town is
carried between districts, and a district entered off a road that admits one way only is a district drivable
one way round.

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
  own design speed affords (`SimConfig.RoundaboutDesignSpeedMps`), which is the exception GEN-12's floor
  names for it — the whole of a roundabout is one corner — and it is **the one road whose ends are not
  straight**, as a piece of the orbital already is.
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
  roads *leaving* them do: the ground one road takes (GEN-17) and a pavement's width of ground on top of it,
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

**GEN-18a** `P6` **No lane dangles**: every lane the town lays is one a car can be driven onto and one it can be
driven off again. **A node that forks nothing may not change how many lanes there are** — where a road of
two ways meets a road of one, the way back out of that node is a lane no movement ever arrives on, since
the only thing that could reach it is the turn round in the road that TER-5f bans. It is a local fact and
not a connected one: a movement leaving a node needs some road other than its own arriving there, and a
movement arriving needs some other road leaving. **A node of one arm is not what this is about** — a dead
end is a place only turning round leaves, and dropping it is GEN-5a's.

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
  bends to meet either — so where the merge leaves two arms lying together it is the street that is dropped
  (GEN-13), and whatever that leaves hanging goes with its own piece.
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
in a fraction of the time and fit on one screen, drawn so that every kind of ground is on it at least
once, and furnished at a fraction of the other maps' counts. Detailed questions asked of "whatever the
big city happens to contain" are a different question every time somebody edits the city.

**Ask a whole city the shallow questions only** — it validates, its junctions are junctions, no lit
junction shows two conflicting greens, nothing is laid on its water. Detailed geometry is asked of named
places on the fixture map.

**Every map states what it claims, and a run of it says whether it kept it** (`VER-11`,
[verification](../../../docs/verification.md#what-a-map-claims-about-itself)). A map that is laid to
measure one thing claims that thing and nothing else, which is the whole of what makes it worth shipping:

| Map | What it claims about itself |
|---|---|
| `Track` | Every shape driven often enough to quote, each corner at what its radius affords, the tighter one slower, the straight accelerated down and braked for, nobody knocked down |
| `Drunk` | That a car gets round it without losing its line. The swerves, the back-offs and the laps given up on are quoted, because they are what the map is for |
| `Fleet` | That every look drives the lap, stays on it, gets itself moving rather than crawling, and pulls at the rate its own file states |
| `Exam` | One claim a kind of card, and that every card this build does not pass is a known finding |
| `Footway` | The same, said of the walker: one claim a kind of card — that a walk arrives, that nothing holds one nothing is in the way of, that a road is crossed on the paint, that nobody steps out on a red, that a body standing in the way is got past and that one under way is followed — and that every card this build does not pass is a known finding |
| `Skidpad` | That every car turns under every pedal it stands, that each goes round the way its wheel is turned, and that nothing leaves its own square. **What the pedal costs the circle is quoted and never claimed**: the lightest pedal here is half, under which every car is being asked for more than its rubber holds, so how far it runs wide of its own axles is a fact about these tyres rather than a bound |
| `Zebras` | That every crossing is walked kerb to kerb and nobody on foot is on a carriageway off the paint |
| `Idle`, `Test`, and every city | The two every town owes: nothing is left inside anything else, and no car stands still with no clock running for it |

**The fixture map and the idle ring claim nothing of their own on purpose.** One is where the detailed
checks are staged rather than a map with a question; the other is laid to be looked at and measures
nothing. A claim invented for either would be a claim the suite already asks better somewhere else.
