# The car agent — decision log

**The manoeuvre catalogue keeps its own log**, in
[maneuvers/docs/decision-log.md](../maneuvers/docs/decision-log.md): what an entry is, how the plan is
chained, why an entry is entered on one thing and left on another. What is here is the body and the
tyres.

## 2026-08-22 — the book is the only thing a driver looks at

The rays went. A driver on its route now reads what is in front of it, what that is and how far off it is
out of the town's own book, in one walk of the ways it is driving — and a car on geometry of its own
(`GroundAhead`) walks the ground under that geometry and asks the book who has it.

**What made it possible was putting everything that can be on a lane into the book.** It already held the
traffic. It now holds **anybody on foot in a lane** — on the paint or on bare tarmac, with a reading of its
own, so that what a driver does about one is decided by what it is — and **the town's own furniture**, projected onto the lanes it
stands on once when the town is laid. That last is the one a ray was genuinely still earning: the instrument
now says how much of it there is, and the answer on the shipped cities is **none** — Odesa and River have
no prop in a carriageway at all, and the `Test` map has 38. The ray was paying every tick for a case the
real towns do not contain and the fixture map does.

**The two answers used to disagree, and that was the deeper reason.** A cast found a shape and could not
say whose it was, so the distance was the geometry's and the naming was the book's. Where they were not
talking about the same body the reading came back `Unknown`, which is never driven round — and a
reservation with nothing standing on it *yet* came back as an empty road, because a ray finds bodies and a
reservation is empty ground. A driver was reading one world and being granted road out of another.
`HeadwayKind.Unknown` survives for exactly one caller now: a car under its own template, whose ways are not
the ways it is driving.

It is also most of what a car cost. Odesa's cars went **375 → 171 µs** of the ranked tick and the frame
**519 → 407 µs**, with the corridor box, the moving-grid query, the followed-body projection, the clearance
scan and the three ray chains all gone. Nothing is a tick behind the world: the book is rebuilt from the
bodies in phase 2, before any driver decides, so every reader sees the same one.

## 2026-08-22 — a driver looks as far as it needs to stop, and not as far as the pedal could ask for

The reach the chains and the corridor were laid over was the car's stopping distance **at the brake pedal's
own cap** — 27 m/s², against the 17.79 the profile actually plans with once the tyres and the braking margin
have had their say. At the gear's own cap that is 112 m of looking for a 158 m stop, so **a car at top speed
could not stop for anything it had only a ray to find it with**. It never showed while every stop on an open
road was a painted bar: a bar is furniture, projected onto the lane at load, and a driver knows where it is
from any distance. The proving ground's pacers are what found it — the first body to stand in a lane with
nothing announcing it was hit at 6.6 m/s by a car that had braked the whole way down from 75.

The reach is now a reaction interval and a stop at the rate the profile brakes at, which is the same figure
`ManeuverDesk.SightM` grows the line to and for the same reason. It cost Odesa's frame 462 → 528 µs at the
time, because looking further meant finding more and each of those was a cast; the claim was worth paying
for, and it is the one the whole speed profile rests on.

## 2026-08-22 — a car drives through the end its own variant drives through

The tyre model has always been documented as spending the *variant's* drivetrain, and the fleet has always
shipped one per variant; the only thing that ever reached it was the nominal figure on `SimConfig`, so
every car in every town was front-wheel drive whatever it was drawn as. The comment that stood in its place
was that taking one per-variant figure while ignoring the rest is a car that behaves like a picture it is
not.

That objection holds for the dimensions and not for this one. **A footprint, a wheelbase and a track are
what the town's geometry was sized against** — junctions, bays and lane widths are all written against the
nominal car, and a variant's own would have to move all of them. **Which wheels the engine reaches is not a
dimension.** It changes how the car behaves without changing what any of that geometry has to hold, and it
is the one per-variant figure the model can spend on its own.

What it buys is a comparison. Six cars on the proving ground are the nominal car in every respect but this,
two of each layout, so a difference between the rear row and the front row of `--bench track` is a
difference about drive layout and about nothing else — and the first thing it said was that a front-drive
car tops out a sixth under the other two down the straight and takes a quarter longer to get back up to
speed out of a corner.

## 2026-08-20 — which procedure runs is the line's question, not the name's

A reactive entry that lays no line of its own leaves the car driving whatever it was already on, so the
standing rules pick the route procedure or the template procedure by **looking at the line**. A dispatch
keyed on the entry's name would hand a car in `E-1` — which lays nothing — to the route procedure with no
lanes under its line.

It survived the catalogue being rebuilt as a file per entry, and it is the reason the two halves of the
driver are split the way they are: the entry decides *what*, the line decides *how it is driven*.

## 2026-08-19 — a car at a standstill has no tyres to work out

The tyre model ran its full four-wheel solve for every car every tick, including the great majority
standing still in bays or queues. A standstill has no slip to resolve and no mark to leave, so the whole
solve is skipped for a body below the threshold — the largest single saving in the car's tick, and free
because the result it skips is arithmetically zero.
