# CityGen — decision log

## 2026-08-31 — the ring is a rounded square, because what stands inside it is a rectangle

**A circle is the wrong shape to put a panel in.** The start menu opens over this map and a panel is a
rectangle, so the ground the ring encloses was being spent on four corners nothing could reach into: the
widest rectangle inside a disc is 0.7 of its width, and the panel had to be measured against its own
*corners* rather than its width to keep it off the road. Rounding a square instead leaves the middle of the
field as wide as the field is, and the panel is laid against a straight side like any other rectangle.

**The corners are what keeps it a road.** A square would be four right angles no car takes at speed and a
picture nobody would call traffic; the corner radius is the one figure here that trades the field against
the pace, since on a loop laid to a single view it is the radius and not the driver that sets the speed.
Two fifths of the half-side is where the field is rectangular enough to lay a panel against and the corner
is still a corner.

**The cuts moved to the middle of the straights.** Four roads is still the fewest that leaves no pair of
nodes joined twice, but a node on a bend is a junction disc taking a bite out of the one piece of the loop
whose shape matters — so each road is half a straight, a corner, and half the next. The four stay the same
length, which is why a quarter of the lap is still a road and the convoy still stands in the middle of one.

**And the escort's pace moved with it.** It was a share of what the escorted car's grip affords on the
ring's one radius; a loop with straights on it has no such figure, so it is now read against the *tightest*
corner. That is where the charge has the least margin over its escort and so where the convoy comes apart if
it is going to — read against a straight, the same margin would be a leading car the charge could not catch
on the bends. The share went up with the reference and the convoy runs at the speed it did before.

## 2026-08-31 — the ring carries an escort and one car, and the escort is held to its charge

**Two convoys of three read as a staging.** The ring was laid symmetric — the same three cars each way
round — which is a picture of an arrangement rather than of traffic. What replaced the second convoy is one
sports car coming the other way: the closing speed changes lap to lap, and a quick car passing a slow
escort is the plainest thing a circle of road can show.

**An escort in police paint outruns an armoured car, and no rule was stopping it.** Police tyres are worth
nearly twice the grip, so on a constant-radius ring the leading car cornered a third faster and left its
charge inside a lap — three cars in a row rather than a convoy. The fix is a pace ceiling on the car
(`CarFleet.PaceMps`), set from what the *escorted* build's own grip affords on this radius. **The
alternative was building the escort on the armoured car's figures and painting it white**, which is a
police car that corners like an APC — the paint and the physics coming apart is exactly what the rule that
a map dresses looks rather than builds them exists to prevent.

**The ceiling alone did not close the convoy up.** At three quarters of the pace and at half of it the three
ran at the same spacing: the gap is the road a follower is granted to stop in plus the interval it leaves on
top, and the pace moves only part of that. What closes it is a second per-car figure — a share of the
**following interval** (`CarFleet.FollowingShare`), which is exactly the part of the gap that is a habit
rather than a stopping distance. **Cutting `Driving.FollowingHeadwayS` instead was not on the table**: it is
what every car in every town keeps, and a convoy on the idle ring is not a reason to move it.

**Measured, the two together halve the spacing** — a quarter of the interval and a little over half the
pace, against the same frame at the same tick. Neither on its own does: the interval alone stops about a
third short of it, and taking it to zero — a driver leaving no margin at all — stops short of it too.

## 2026-08-30 — the menu is drawn over the ring, and GEN-1b now says which map that is

**A start menu over an empty screen was the one frame of this game nobody had made anything of.** GEN-1b
is about not building a *city* nobody asked for — a two-second lay of a town the reader may not want —
and that argument says nothing about a map that costs a fraction of one and was laid to be looked at. So
the ring is what the menu now stands over, on both heads and in both configurations, and the rule says so
rather than leaving it to whichever entry point happened to pass a name.

**Standing a town up no longer means dropping the reader into it.** Opening a map shuts the menu, because
a map on the menu is a map somebody picked; the ring is opened with the menu deliberately left up
(`Interface.TownChanged(behindTheMenu)`). The alternative — reopening the menu after the open — is the
same state reached by two moves, and the frame in between is the reader watching the panel they were
already looking at flicker.

## 2026-08-30 — a map laid to be looked at, and the look rule it had to loosen

**The idle ring is the first laid map that measures nothing.** Every other one answers a question and is
shaped by it; this one is the picture the game idles on, so what shaped it is that it never stops being
worth watching and never needs anybody's attention. A circle gives that for nothing: a closed loop nobody
can reach the end of, and one carriageway carrying traffic both ways.

**It stands at the left of the frame.** The menu hangs from the gear in the top right, so a ring in the
middle of the screen is a ring with a panel over it; the camera is moved half the difference between the
view's long and short sides to the right of what it was looking at (`Opening.AsideTheMenuM`), which stands
the circuit against the left edge with the panel clear of it. **Only for a town opened behind the menu** —
a map somebody picked is framed the way every other map is.

**Its size is the window's and not the driving's.** The first ring was 120 m across the radius, so that
what set a car's speed was the driver rather than the corner — and the picture it made was an empty
stretch of road for the twenty seconds between one car passing the camera and the next, because a 70 m
view of a 750 m lap holds a tenth of it. So the radius is now whatever the view a run opens on will hold
(`ViewFigures.CameraDefaultViewM`, less the road and a little grass), the whole circuit is on screen, and
every car is in the frame the whole time. **What that costs is that the corner sets the speed** — 13 m/s,
held for ever, with what each look is worth barely showing against the others — and for a map whose whole
job is to be looked at, a picture with six cars in it beats a table with a bigger number in it.

**Nothing on it is staged.** No wheel is held over and no car is ordered anywhere: with no building and no
bay on the map, `TownWorld.DriveTheEmptyMap` puts each car on the lane under it and the ordinary catalogue
drives it. That is why the map is worth shipping at all — a loop of scripted cars would be an animation, and
this one is the town's own driving with nothing else in the way.

**Four roads, because a road ends at a junction and a ring has no end.** Two would have joined the same pair
of nodes twice, which nothing else in this engine's geometry ever does; four quarters is the fewest that
avoids it, and each node is a lane's half-width of ground nothing drives — the same seams the proving
ground's ten are.

**Its cars are two convoys and not six of a kind.** A police car, the armoured car it escorts and a second
police car, each way round: the escort is held to the pace of what it escorts, so three cars read as one
thing rather than as three that happen to be in a row, and the two convoys meeting head to head twice a lap
is the whole of the movement on the map.

**The map dresses its own cars, and that cost a rule.** The fleet's wrap cannot reach a service look, which
is right — an ambulance handed to the seventeenth ordinary car would be a school run in one. But the
catalogue also said that a police look *is* a car with a station, and the service tier found its vehicles by
their paint. That is an over-fit: SRV-3 defines a service vehicle as paint **and** a building, and `EVA-7`
already names an ordinary car in service paint as a state the town has. So the rule is now the narrow one —
**a look is what a map asks for and a duty is what a station gives** — and `ServiceVehicleTests` finds a
patrol by its station and a recovery by its depot, then asserts the paint, rather than the other way round.
What is unchanged is the thing that mattered: no town's own traffic can be handed a service look by
accident.

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
