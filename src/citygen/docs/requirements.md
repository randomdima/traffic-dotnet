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
  other shape, so it is a record. **The pavement's own inner fillets are not**: they fall out of the pieces
  the walk is laid from, and the build solves them against the finished ground (TER-3c.4). The file format
  still moves a list of them, because a shipped `.town` carries one and the round trip is what makes it a
  map; nothing reads it.
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

**The exceptions are the maps laid to measure one thing**, and none of them is a city. They are written out
through `TownWriter` by `--lay-maps` and read back as files like every other map — so nothing downstream
can tell one of them from a map that arrived — and each is laid against the car's own figures rather than
generated, which is exactly why they are authored where those figures are.

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

**The crossings map is neither**, and it is not laid here: `Zebras` arrives as a file like a city does. It
is five isolated streets with a crossing on each and one body apiece, one of those crossings deliberately
laid off square, and it carries no cars and no buildings because the paint is the whole subject — a skewed
crossing is the case that can fail while every square one in a city passes.

**The writer is the reader's other half.** Every field, in the file's own order, at the same width — the
round trip over every shipped map is what holds the two to each other, and it is the only reason a plan
this build lays is a map and not a second kind of thing.

**It is also what lets this build write one field into a map it did not lay.** `--place-services` reads
every shipped town, decides which of its buildings serve which use (GEN-9) and writes it back — a workshop
step, run when a map arrives or when the shares move, committing the file it produces. Reading a city and
authoring one field of it are the same round trip, so nothing else about the map moves when it runs.

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

**GEN-9** A building **declares what it is for**: ordinary, or one of the uses a service is stood at — a
hospital (AMB-1), a police station or a depot (SRV-1). It is a field of the record and therefore a fact
about the map, the same for every run of it and for every agent seed, and it moves only when the map does.

**Which buildings they are is settled where the map is authored and never at load.** Two things decide a
place and neither is affordable every time a town is opened: a service building has to have somewhere for
its vehicles to stand, and the services have to be spread over the town rather than dropped into it. A
shuffle taken at load could only ever say which buildings *exist*, and would put the one hospital next
door to the police station as often as anywhere else. `--place-services` is the step that decides, and
what ships is the file it writes.

**A building serves one use at most**, which one field settles: a byte cannot say two things. A map that
declares none of them is a map whose services do nothing, and that is a state the census reports rather
than a state anything papers over.

**GEN-7** Initial state: cars start **stopped in parking spaces**, and **a person starts inside the
building the map stood them at**, dwelling out the interval an arrival dwells (PER-11).

**It closes the loop rather than adding a stage to it.** A trip ends by walking through a door and
dwelling, so a body that begins there begins in the state every later trip returns it to, and everything
after the first dwell is the ordinary round: out of the building, to a car if the trip is worth one
(PER-10), to the destination, in. Started on the pavement instead, everybody's first leg was a leg no rule
of theirs had drawn. **Which building is read off the pose the map left the body in** — the way in it is
standing at — so the format carries nothing to say it, exactly as the pacing and the reeling walkers are
told apart by where they were put down (PER-16). A door with no room behind it leaves the body standing
outside it, which is a state that already has a name.

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

**Every map states what it claims, and a run of it says whether it kept it** (`VER-11`,
[verification](../../../docs/verification.md#what-a-map-claims-about-itself)). A map that is laid to
measure one thing claims that thing and nothing else, which is the whole of what makes it worth shipping:

| Map | What it claims about itself |
|---|---|
| `Track` | Every shape driven often enough to quote, each corner at what its radius affords, the tighter one slower, the straight accelerated down and braked for, nobody knocked down |
| `Drunk` | That a car gets round it without losing its line. The swerves, the back-offs and the laps given up on are quoted, because they are what the map is for |
| `Fleet` | That every look drives the lap, stays on it, gets itself moving rather than crawling, and pulls at the rate its own file states |
| `Exam` | One claim a kind of card, and that every card this build does not pass is a known finding |
| `Skidpad` | That every car turns under every pedal it stands, that each goes round the way its wheel is turned, and that nothing leaves its own square. **What the pedal costs the circle is quoted and never claimed**: the lightest pedal here is half, under which every car is being asked for more than its rubber holds, so how far it runs wide of its own axles is a fact about these tyres rather than a bound |
| `Zebras` | That every crossing is walked kerb to kerb and nobody on foot is on a carriageway off the paint |
| `Test`, and every city | The two every town owes: nothing is left inside anything else, and no car stands still with no clock running for it |

**The fixture map claims nothing of its own on purpose.** It is where the detailed checks are staged
rather than a map with a question, so a claim invented for it would be a claim the suite already asks
better somewhere else.
