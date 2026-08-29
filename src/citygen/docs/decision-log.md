# CityGen — decision log

## 2026-08-28 — the exam orders its walkers, because three cards were passing on an empty crossing

**The three `StopsForThePaint` cards had never once been asked.** Each claims the car is never on the paint
while somebody on foot is, and each was satisfied by nobody being there: the car and the body were never on
the crossing in the same second of any run. On one card the body crossed at the twenty-first second and the
car at the fifth; on another the body never used its crossing at all. The claim is unfalsifiable that way
round — it can only fail by coincidence — so what it reported was the coincidence not happening.

**The map's walkers wander, and the spawn code said they paced.** A body put down beside a carriageway with
nowhere to be paces into it and back, but only on a map with no pavement on it (`TownWorld.PacesARoad`);
`Exam` lays pavement on every block, so its four walkers draw a destination anywhere in seven hundred metres
of lattice and walk off — through other cards' junctions on the way, which is the traffic nobody staged that
`ExamDrive.Hold` exists to keep the cars from being.

**So the harness orders them, as it already orders every car.** A card about paint paces its own body kerb
to kerb until the subject is near enough that the crossing it is on is the one the card is about; every
other card's body is ordered to stand where it was put down. **Pacing and not one timed crossing**: a body
parked on the paint is a car that stops for it for good, and a single crossing timed by arithmetic is a
rendezvous that a car slowing for the very body it is timed against then misses. **And it stops pacing
inside its own step-out distance** — a body stepping out a car's length in front of a moving one puts PER-15
under test instead of the crossing, which is a different card and not this one.

**The claim under every card also grew a second half.** An arrival was the whole of it, and the lattice is a
grid: the place a driver is sent to is reachable round the block, so an arrival said the car got there and
never that it crossed the junction the card was written for. It is now the arrival *and* the box, off the
`ClearedAt` the harness had been recording and nothing had been reading.

## 2026-08-28 — the exam grew by eleven cards, and all eleven are unregulated boxes

A card is a cell, so asking the exam for more crossings is asking for a bigger lattice: `ExamCards.Rows`
and `Columns` went to six, the table to thirty-six, and the roads, the spurs, the paint, the fleet and the
ground under all of it followed without a line of geometry moving. **That is the arrangement paying for
itself** — the map is derived from the cards, so the cost of a new question is the question.

**The eleven new cards are all boxes nothing governs.** Four lit junctions is enough of them: at a lit box
the timetable decides and the card is about obeying it, so the box worth staging over and over is the one
where the ranking alone decides (TER-5e). The eleven are the pairings the first twenty-five left out —
straight against the near-side turn merging in front of it, near side against across in the arm they both
join, a stem emerging across a road running both ways, a queue whose leader is turning across, and a box
with somebody on all four arms of it.

**Ten of the eleven passed the day they were written, and the eleventh narrowed a finding.** Two turns
across **from arms beside one another** clear each other, where two **opposing** ones deadlock — so what
stops the opposing pair is not that they are the same rank but that being the same rank leaves neither able
to take ground the other's path lies on. The finding stands where it was; what it is about is smaller than
it looked.

## 2026-08-27 — a map laid from the questions asked of it, and the two it could not answer

The proving ground measures what a shape of road costs a car. Nothing measured **what a car does where
roads meet**: the shipped cities have hundreds of junctions and not one of them is staged, so a turn across
the oncoming stream was only ever watched where a city happened to produce one and only ever with whatever
traffic happened to be there. `Exam` is the answer, and the arrangement it settled on is the point of this
entry.

**The cards are the map, and the map is derived from them.** `ExamCards` is a table of crossings written
as data — two arms and a stand-back per car, plus the one claim the card makes — and `ExamPlan` lays
whatever they need. A card that wants a crossroads at the edge of the lattice gets a **spur**, a short road
out to a dead end; a card about lights gets them; the corners get a spur whether they asked or not, because
two arms meeting at a right angle is a road that turns and not a junction (TER-5b). Nothing about the map is
chosen twice: the shape of a cell's junction is a fact about which arms its card asked for.

**One make of car, and it is not the police car.** A card is read against another card, so the fleet's
spread of weights and drivetrains would be a second variable inside every comparison — the exam stands the
nominal car (CAR-11a) as the measured lap does. The look is an ordinary one because in this town **a police
look is what a police car is** (SRV-2, SRV-5): every car wearing it belongs to a station, stands on its
apron and answers calls, so a lattice of them would be a lattice of service vehicles running errands
instead of cards.

**Paint on every arm, not only where a card is about paint.** The first arrangement painted four crossings
— one per card that watches one — and left every block's pavement a closed ring with no way off it, which
is a walking network of islands. TER-6's placement rule is the fix and it costs the cards
nothing: a crossing on every arm slows every approach to a crossing pace, which is what a junction does.

**And two things it cannot carry, found by trying to.**

- **There is no inline junction on it.** TER-5b promises a lit mid-block crossing and the engine refuses
  one twice over: a node with two arms admits no conflicting movements, so it is never lit (TLT-3), and its
  two arms' lane ends lie over each other under the paint, so the crossing's bands come out overlapping and
  no walker can be ordered along them. The map carries a **mid-block crossing belonging to no junction**
  instead (TER-6), which is what `Zebras` does, and the promise in TER-5b is a rule with nothing behind it.
- **The lattice stands half a cell off the whole metre.** A carriageway is laid either side of its own
  centreline, so a lattice on whole metres puts every kerb exactly on a cell boundary — and a sample a hair
  short of one, which is all a straight laid at an angle read back through a sine is, lands in the cell
  beyond it. Half a cell over, nothing the map is measured against sits on a boundary at all.

## 2026-08-27 — the map says what a building is for, and its people start behind its doors

Which building was the hospital used to be a shuffle taken off the world seed when a town was opened,
because the format carried no kind on a building and an authored use would have been a use no shipped map
had. What that bought was reproducibility and nothing else: the shuffle knew which buildings *existed* and
could put a town's only hospital on a cul-de-sac with no bay within a block of it, where its four
ambulances stood nowhere and were reported as a count that did not match the roster.

So the record carries a **use** (GEN-9), the format went to version 3, and the shipped maps were rewritten
through the reader and the writer that already round-trip them. That is the migration the old decision
priced and declined, and it cost one field and one pass.

**The placement moved to where a map is authored**, which is what makes it worth doing properly:
`--place-services` takes the buildings with somewhere for their vehicles to stand and lays the services
out by farthest-point, so the next one goes as far from every service already placed as the town allows.
It is a sweep over every eligible building for every place — a second of work, once, and never again — and
it is exactly the sweep that was refused when the answer had to be produced on every load. It is a
workshop step on `--lamps`' terms: run it when a map arrives or when the shares move, and commit the file.

**And the map's people now start behind its doors.** They were already stood at them — every person spawn
on every shipped map is a stride off a way in — so what changed is that the town lets them in before the
first tick and gives them the dwell an arrival gets. A trip ends inside a building, so beginning there is
the round closing rather than a stage added to it, and the first leg anybody walks is one their own rule
drew. The dwell is drawn per person, so the doors do not all open on the same tick; the streets fill over
the first ten seconds rather than starting full of people who were never anywhere.

**What it costs is that a question about a body on the pavement can no longer be asked at tick zero.** The
suite's own answer to that is to run the town on until somebody's dwell is up, which is bounded, and the
one test that wants a walker standing still in a town for ever asks it of the proving ground — where
nobody has anywhere to be, and nobody ever went indoors.

## 2026-08-17 — the town arrives as data, and the plan is the boundary

`CityPlan` is pure data: no engine types, no node references, no behaviour, laid as structure of arrays
with a flat array and an offsets array beside it for every variable-length run. **The world is built from
that structure and never from the file**, and the `.town` format is the only thing that crosses a process
boundary.

The consequence is deliberate as a design and a real gap as a state of affairs: **there is no generator
here.** Maps are exported elsewhere and read from `towns/`, so `GEN-2` through `GEN-8` bind whatever laid
them and nothing in this project checks them. Writing a generator is the obvious way to close that, and
the plan structure is exactly what one would emit — the validator would then be shared by the generator's
retry loop and by the unit suite, as a **safety net and not a search partner**: the layouts are meant to
satisfy the rules by construction.

Until then, `src/tests/citygen/MapConformanceTests.cs` asks the shipped maps the shallow questions, which is
what a plan nobody here laid can honestly be asked.

## 2026-08-23 — the same lap twice, because the people are the variable

The proving ground answers "what does this shape of road cost a car" and could not answer "what does a slow
thing in the lane cost one". Its people are what stop the cars, and they stop them by stepping into ground
**nobody has taken** — so a driver there is never asked to follow anything, only to arrive at it and wait.

So `TrackPlan` lays two maps off one arithmetic. `Drunk` is the same lap, the same shapes and the same six
cars in the same poses; the fifteen people are put down **in** the carriageway instead of beside it, and a
body with nowhere to be that finds itself on a lane reels down it (`PER-16`). **Which rule a walker follows
is the pose the map left it in**, so the second map needed no name in any agent, no spawn kind and no field
in the format — the whole of it is where fifteen bodies stand.

Three things came out of laying it, and two of them are about the driving rather than about the map:

- **`E-4` is reachable, and had never been reached before.** Every shipped map reported "0 swerves" and
  listed the entry under never-entered; the drunks are the first thing in this town that stands in a lane
  while a driver has somewhere to be. What the lap then found was that the entry did not work — it was drawn
  flat on a road that bends, at the steering lock on a road that affords speed, and rationed by a count a
  stuck car can never earn back. All four are the [catalogue's](../../agents/car/maneuvers/docs/decision-log.md)
  and were fixed there; what this map contributed was being the only place any of it showed.
- **A drunk that wandered over the centreline was a lap nothing got round.** The oncoming lane is the only
  ground `E-4` may take (CAR-6.2b) and the only other ground is a verge, so a body free to stand anywhere
  across a 6 m carriageway is one no driver may lawfully pass. It keeps to its own lane for that reason and
  not for its own safety.
- **A body walks at what is in front of it and not along the road**, so a lurch taken at its full stride
  down a 15 m hairpin cut the chord clean across the oncoming lane and onto the grass — with a car coming
  round the bend at it. The lurch is bounded by the chord that stays inside the lane, which is
  `sqrt(8·R·sag)`: the corner formula, doing the same job for a walker that it does for a car.

**What the lap costs is quoted rather than asserted to zero**: the people knocked down, the swerves, the
back-offs, the laps given up on and the cars that ended wrecked. A pacer asks the road before it steps out
and a drunk does not, which is the whole difference between the two maps — tuning until nothing was ever hit
would be tuning until the instrument could no longer report the thing it was laid to find
([verification](../../../docs/verification.md#the-instruments-say-what-is-missing)). **Two of the six end
the run wrecked**, both in the same contact, and that is the reading the lap is currently worth arguing
about: it is a pair meeting at an angle at a shade over eight metres a second, which is what the damage
model prices a wreck at, and no driving rule refused it.

## 2026-08-22 — a map this build lays itself, and the writer that makes it a map

A measurement of what a shape of road costs a car needs a road that is one shape and nothing else, and no
city has one: every figure taken on Odesa is a figure about Odesa's corners, its traffic and its lights at
once. So the proving ground is **authored here** — `TrackPlan`, five shapes chosen against the car's own
config figures — and that is a deliberate exception to "this project does not lay plans", not a crack in
it: it is not a city, nothing about it is generated, and GEN-1 through GEN-8 have nothing to say about a
straight, a snake, an arc and two turns in a field.

**It is one lap and not four circuits.** Four separate circuits measured four shapes with one car apiece
and could say nothing about a fifth car or a second kind of drivetrain — every question of the form "is
this car slower here than that one?" needed the two cars to have driven the same road. One lap carries as
many cars as it has room for, each meeting every shape in turn, and the price is traffic: a car held by the
one in front is a car the road is no longer the reason for. What pays for it is that the holding is
*named*, so a pass somebody else was in the way of is thrown away rather than averaged in.

**The lap closes on the shapes themselves.** There is no neutral corner anywhere: the arc's three quarters
of a turn is exactly what the half turn and the quarter turn back leave over, so every bend on the map is
one of the five and the only thing a link ever is is a straight. The last link is derived rather than
chosen — whatever brings the lap home — which is why a shape that grows moves a straight instead of leaving
a step in the road.

**A shape is a road**, and that is what makes a measurement local: every consumer already knows which road
a car is on, so asking which shape it is driving is two loads rather than a search of the geometry, and no
figure can be quoted against a shape the car had already left.

**What stops a car at the end of a shape is somebody standing in the lane, and there is no light and no
paint anywhere on the lap.** A light would have been a metronome — nothing meets at a node here and there is
nothing to give way to — and it tells a driver where to stop before it has to look. A body in the road tells
it nothing, so the whole of what the track asks is that a driver stops for what it can see. Two things
about the pacers were paid for by measurement:

- **A shape ends where somebody paces rather than beginning there.** Paced at the entry instead, every
  corner would be taken from a standstill and the corner figures would be facts about the pacer.
- **The beat between two paces is drawn afresh.** A lap settles into a period, so a fixed beat meets the
  same walkers at the same point of their pacing for ever: two of the five shapes were blocked on almost
  every pass and two on almost none.

**Fifteen of them, and nobody in the middle of the straight.** The lap carries one pacer at the end of each
shape because that is where a shape's stop is measured, and ten more spread along it because a proving
ground for a body stepping into the road should be a road somebody might step into anywhere. The straight
is the one shape they are kept out of the middle of: its figure is the one speed the whole lap builds up
to, and a body halfway down it is a car that reaches 67 m/s against the gear's own 75. Every other shape is
held to a speed its radius sets, which a stop in the middle of it does not change.

**And a body that stops every car makes one platoon of the field.** Whichever car arrives first is stopped
and the ones behind close up — within ten minutes the six of them are nose to tail and stay that way, and
every pass after that is a pass somebody else was in the way of. What the probe quotes is what the field
gathered while it was still strung out, which is why watching the lap for longer buys nothing: at fifteen
bodies about seven passes in ten are thrown away for traffic, and the ten or so a shape keeps are all
gathered early.

**A pacer waits for the traffic and never for a clock.** It goes back out the moment the car it stopped —
and the queue that closed up behind that car — has gone past, which is what makes the stop the road's own
period rather than a beat's: the probe now records a stop on very nearly every pass it keeps. Sitting a
walker out on the pavement for a fixed wait was the same rig measuring whichever shapes the wait happened to
fall on, and it is the reason `Person.StandAboutS` no longer times anything a driver ever meets — what is
left of it is the bound on a stand nothing comes down the road to end.

**And every figure is read off the shape's own slowest point rather than off a standstill** — the ground
down to it, the speeds either side of it, the run back up from it. A rig that could only measure a stop was
a rig whose sample size was however often somebody happened to be in the way; this way a pass nobody
stepped out for is a measurement too, and one they did only makes the slowest point zero.

**It is written out as a file rather than kept as a plan in code.** A `CityPlan` built at run time would
have been a second kind of map — openable by whatever built it and by nothing else, invisible to `--shot`,
to the menu and to every sweep that asks a question of every shipped town. `TownWriter` is what makes it
an ordinary map instead, and the round trip over the shipped towns (`TownWriterTests`) is what keeps the
writer honest about the reader.

**Two sweeps were widened rather than worked around.** A scenario now carries "the thing it is for" as
either paint to watch or a road that bends, and the walking network's questions are asked of the maps that
have a pavement — because a map laid without one answers them vacuously rather than correctly. The
alternative was to hang a crossing and a pedestrian on a proving ground so that the rules about towns
would recognise it, which is a map lying about what it is.
