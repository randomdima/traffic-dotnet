# The driving manoeuvre catalogue

**A car does nothing that is not one of these.** `AGT-7` asks for a closed catalogue per agent type, and
this is the car's: seventeen named, bounded procedures, each with a file of its own, a page of its own, and
exactly one line in [`ManeuverCatalogue`](../framework/ManeuverCatalogue.cs). There is no "and otherwise"
branch anywhere in the driver, because there is no such state here.

**Following is not among them.** A driver is granted the road it will stop in and holds the speed that
road affords ([`LaneOccupancy`](../../../../world/road/LaneOccupancy.cs)); a car behind another is running
its line on a shorter road, which is `P-4` and needs no entry of its own.

**Nor is slowing at a crossing.** The pace over paint (CAR-7b) and the stop short of somebody on it
(`TER-4c.1`, `TER-5e`) are terms of the same speed profile, so a car at a zebra is `P-4` on the road the
zebra left it.

The pages below say **when an entry is the right thing to do, what it delivers, and the state either side
of it**. How each one is written is its own file's XML docs; why any of it reads this way is
[decision-log.md](decision-log.md).

## The two classes

| | Chosen by | Entered from | Ends by |
|---|---|---|---|
| **Planned `P-n`** | the planner, as a step of a chain, or by the entry before it | its own `Sa` holding | naming a successor, or asking for the plan's next step |
| **Reactive `E-n`** | never planned — a condition it watches for fires | the ladder, a reflex, or a discretionary hand-over | handing back to the planned entry it suspended (§1.6) |

## The catalogue

### Planned

| | Entry | When | Page |
|---|---|---|---|
| `P-2` | LeaveTheBay | the car is in a bay and the leg is beginning | [p02-leave-the-bay.md](p02-leave-the-bay.md) |
| `P-4` | RunTheLine | there is road ahead and nothing else applies — **the default**, and queueing | [p04-run-the-line.md](p04-run-the-line.md) |
| `P-6` | HoldAtALine | there is a place ahead the car may not pass | [p06-hold-at-a-line.md](p06-hold-at-a-line.md) |
| `P-8` | TakeTheJunction | the box ahead is within reserve distance and is this car's | [p08-take-the-junction.md](p08-take-the-junction.md) |
| `P-14` | ParkInTheBay | the leg's line has left the road for the way into the bay it holds | [p14-park-in-the-bay.md](p14-park-in-the-bay.md) |
| `P-16` | SquareUpInTheBay | a park attempt failed and the retry needs a different pose | [p16-square-up-in-the-bay.md](p16-square-up-in-the-bay.md) |
| `P-17` | StandParked | the car is in the bay and the leg is over | [p17-stand-parked.md](p17-stand-parked.md) |
| `P-18` | AttendTheScene | there is a place on the line the car was sent to, near enough to be stopped for | [p18-attend-the-scene.md](p18-attend-the-scene.md) |
| `P-19` | ShuntRound | the leg comes back the other way and the road runs out here | [p19-shunt-round.md](p19-shunt-round.md) |

### Reactive

| | Entry | Fires on | Page |
|---|---|---|---|
| `E-2` | EmergencyStop | a hazard inside braking distance — **row 1, asked every tick** | [e02-emergency-stop.md](e02-emergency-stop.md) |
| `E-3` | BackOff | jammed, with room behind and an attempt left | [e03-back-off.md](e03-back-off.md) |
| `E-4` | GoRound | `P-4` has put up with something in the way for longer than a driver waits | [e04-go-round.md](e04-go-round.md) |
| `E-6` | GiveUpThePlace | the destination is the problem | [e06-give-up-the-place.md](e06-give-up-the-place.md) |
| `E-7` | Reroute | the road is the problem | [e07-reroute.md](e07-reroute.md) |
| `E-8` | ReturnToLegalGround | the body is not on ground a car may drive on | [e08-return-to-legal-ground.md](e08-return-to-legal-ground.md) |
| `E-9` | SettleForHere | nothing else worked and where the car stands is not an obstruction | [e09-settle-for-here.md](e09-settle-for-here.md) |
| `E-10` | AbandonTheCar | nothing else is available — **the exit the ladder is finite by** | [e10-abandon-the-car.md](e10-abandon-the-car.md) |

**The numbering has gaps and they stay.** `P-1`, `P-3`, `P-5`, `P-7`, `P-9`, `P-10`, `P-11`, `P-12`,
`P-13`, `P-15` are the walker's or are retired, and `E-1` and `E-5` are retired. A retired number is never
reused, so a code printed by a trace resolves to the same entry it always did.

**`P-11` was the turn-around inside a junction, and what retired it is that no junction admits one**
(`TER-5f`): the line between two opposing lanes is a semicircle no car can hold, so the movement was never
drivable and the router priced it out of reach from the day it was written. Coming back the way you came is
now a bay's (`GEN-4l`) or `P-19`'s — [decision-log.md](decision-log.md).

**`P-12` was the crossing, and what retired it is that a car slows at one without being told to**: the
pace is the car's own (CAR-7b) and the stop short of somebody on the paint is the ground it was granted
(`TER-4c.1`, `TER-5e`), both of them terms of a profile taken every tick. The entry set no limits, drove no
geometry and had no bound of its own — it named the term that had already won and handed the car back when
the paint was behind it, which is `P-4` with a second name on it —
[decision-log.md](decision-log.md).

**`E-1` was the yield, and what retired it is that yielding is now ground rather than a manoeuvre**
(`TER-5e`): a right of way is carried by the stretches in the town's own book, the body that gives way is
stopped short by the same speed profile that stops it at everything else, and what a car does while it waits
is `P-6`. An entry whose whole content was a name and a bound was a second way of saying what the road had
already said — [decision-log.md](decision-log.md).

## The framework

### 1.1 The driving state

Every `Sa` and every exit below is written in terms of these, and they are the fields of
[`DriveScene`](../framework/DriveScene.cs).

| Field | Values |
|---|---|
| **Pose** | position and heading, always read at the **rear axle** (CAR-4a) |
| **Motion** | speed along the direction the line is driven in; at rest or not |
| **Ground** | bay · lane · junction box · crossing · off drivable ground |
| **Line relation** | on the line · how far off it · on a route, on one of the town's own ways, or on a template of a manoeuvre's own |
| **Holdings** | the movement it is committed to, the bay it stands in, the bay it has booked |
| **Plan** | the remaining chain and the route cursor it is measured against |
| **Counters** | time in the entry; attempts left on each bounded recovery |

### 1.2 What an entry is

A **scenario** it is for, an entry state **`Sa`** it requires, a goal state **`Sb`** it guarantees, the
**line** it drives, an ordered **procedure**, **guards** that hold throughout, **bounds** that make it
finite, and **exits** — success, and a named successor for every way it can fail.

In code that is three things and no more: `Begin` (the `Sa`, and the take-up), `Tick` (the procedure and
the exits), and the traits `ThinksEveryTick` and `Watched`. An entry has **no state of its own** — a car
is an index, and everything about what it is doing lives in the fleet's arrays.

### 1.3 Planning rules

- **MAN-1 — Chaining.** A chain is valid only if each step's `Sb` satisfies the next one's `Sa`. It is
  not asserted, it is *checked*: a successor whose `Sa` does not hold is re-derived rather than driven.
- **MAN-2 — Partial plans are normal.** [`DrivePlan`](../framework/DrivePlan.cs) carries the leg's
  **skeleton** — leave this bay, run the route, park in that bay, stand in it — and nothing about the
  queues, junctions and crossings between them. Those are reached from `P-4`'s own exits as the road
  produces them, because everything past the next junction is a prediction about other agents.
- **MAN-3 — Replanning.** The chain is re-derived, never patched, and **always from the car's actual
  pose**: when a place is retargeted, when a road is priced up, and after anything that drove geometry
  of its own.
- **MAN-4 — Bounded by construction.** Every entry carries a time bound, a distance bound or an attempt
  count. No entry may wait indefinitely; one that would stand still forever takes the next rung.
- **MAN-5 — No undefined failure.** Every failure names its successor. `E-9` and `E-10` are the terminal
  ones and every ladder ends at them.
- **MAN-6 — Retry from a different pose.** A manoeuvre that failed may not be re-attempted from the pose
  the failed attempt ended in. Two poses on one line are joined by a straight, a straight has no
  curvature, and a car driven along one arrives at exactly the angle it left at. Only a bay needs a
  procedure for this (`P-16`); everywhere else `P-4` draws its line from the pose the car is actually in.
- **MAN-7 — The line, not the route.** Going round an obstruction is a change to the *driven line*; the
  route, the progress measure and the junction claims stay exactly as they were.

### 1.4 Arbitration

The highest row wins, and nothing below a row may pre-empt it.

| | Class | Rule |
|---|---|---|
| 1 | Hard rules | never drive into something on purpose — `E-2` outranks everything and is asked on **every** tick |
| 2 | Priority | a red, a box somebody holds, a body on the paint → the entry that owns that obligation |
| 3 | Continuation | the running entry's own procedure |
| 4 | Escalation | past its bound, the next rung of the ladder |
| 5 | Discretionary | `E-4`, `E-7` — only after their own wait bounds have elapsed |

### 1.5 How often an entry is ticked

A body moves every physics tick and always will. A **procedure** runs on the driver's decision clock,
staggered by the car's own index — unless the entry declares `ThinksEveryTick`, which two kinds do:
**negotiating with something that is itself moving**, where a tenth of a second of staleness is a gap
that had already closed; and **steering to a pose**, which is a control loop and converges at a sixth of
the rate if it is run at a sixth of the rate. The second was measured, not reasoned.

### 1.6 Interruption and resumption

A reactive entry records which planned entry it interrupted. On completion the interrupted one is
**re-entered through its own `Sa`**, never resumed mid-procedure; if `Sa` no longer holds the chain is
re-derived (MAN-3). Only one interruption deep — a reactive entry that itself needs help escalates.

### 1.7 The standing rules

These run underneath *every* entry. **They are not manoeuvres and are never selected**; they are how a
car is driven at all, and they live in `src/world/town/TownWorld.Driving.cs`. No entry repeats them.

- **S-1** Hold the driven line, measured at the rear axle, aiming a speed-scaled distance ahead and
  never further than the corner being driven is wide.
- **S-2** Speed is the minimum of every constraint — the gear cap, the corners, the end of the line, the
  headway, **the road the car was granted**, the stop point, the crossing pace, **and whatever the entry in
  charge asked for** — every distance taken a lead ahead of where the car is, against *usable* grip. The
  lead is the staleness of the driver's own decision and the travel of the pedal that answers it.
- **S-2a** **Take the road ahead before driving down it.** Every tick, a driver asks for the stretch of
  its own way from a margin behind its tail (`TER-5c.2`) to where it plans to be able to stop, and is
  granted what is left of it in front of the nearest car already on it. Nobody is granted ground another car will still be standing on
  once that car has stopped, and **that is the whole of following**: the car behind has less road to stop
  in and holds the speed that road affords ([the catalogue's log](decision-log.md)). **The grant alone is
  read at a following time** rather than at the lead above, which is what settles a queue at the standstill
  gap and a second of travel rather than at a tenth of one — **and that time is kept from what is being
  followed and from nothing else**: a grant cut at a wreck, at somebody on foot, at ground somebody has
  claimed or at the place two movements meet already ends the asker's own margin short of it, and a second
  of travel on top of that is a car holding a street shut at speed for something it needed only to stop
  short of. **And it is cut by the ways this car is driven
  *over* as well as by the ways it is driving** (`TER-5c.1`), so the grant means one body to a piece of
  ground across a junction and not only along a lane. **What is asked for stops where a rule stops the
  car** (`TER-4c.1`) — a red, a bar, a zebra it must stop short of — the gap it keeps included, so a car
  standing at a stop holds the ground it is on and none of what it stopped for.
- **S-3** Watch ahead along the line actually being driven, in the gear it is being driven in. **Every
  tick, and out of the town's own book** — what is in front, what it is and how far off it is are one walk
  of the ways being driven, over the same stretches the grant in `S-2a` was taken against, so the reading
  and the road the car was given can never disagree ([the car's log](../../docs/decision-log.md)).
  **Everything that can be on a lane is in the book**: the traffic, anybody on foot in it, and the town's
  own furniture (`TER-4c`). **What is found is named and never guessed at from its speed.** A car under
  geometry of its own is the one exception — its ways are not the ways it is driving, so what it finds
  there is named unknown and unknown is never driven round.
- **S-4** Take up the ground **on your own way through** the box ahead, at the places the other movements
  cross it, and give back the box behind (`TER-5c`). Every tick, never on the clock — a red can change
  under an entry, and nothing here is a claim on the junction. **What another movement's ground costs you
  is looked up and never marked** (`TER-5c.1`): a car reserves the ways it is going to be on, and reads the
  ways it is only driven over. **And what it costs you turns on the right of way each of you has there**
  (`TER-5e`): ground held by a movement that gives way to yours is ground you are not cut at, and ground
  held by a body past the point it could stop short is ground nobody's rank takes. A crossing already taken
  is **given back** when something with the right of way over it asks for the same ground — while this car
  can still stop short of the box, and never after.
- **S-5** Hold a stop you have already made: the handbrake is pulled only at rest.
- **S-6** Hard rules bind everywhere, including inside a recovery. Only lane legality and the no-idling
  rule may be suspended, and only where an entry's page says so.
- **S-7** A hand at the wheel suspends all of it.

### 1.8 The escalation ladder

One ordered ladder every stuck situation walks, in [`DrivingLadder`](../framework/DrivingLadder.cs). A
rung whose conditions do not hold is **skipped**, never attempted; the ladder never stops early; and
**it rewinds on road covered, never on manoeuvres completed** — a jammed car completes manoeuvres
continuously with the body exactly where it started.

| Rung | Entry | Only when |
|---|---|---|
| 0 | `P-6` | the obstruction has priority — wait at the place the car was stopped short at |
| 1′ | `P-2` | the car is still inside the bay it holds |
| 1 | `E-3` | something to back away from, room behind, an attempt left |
| 2 | `P-16` | at the bay this leg holds |
| 3 | `E-3` | again — what it buys is the fuse between the two |
| 4 | `E-6` | there is a place to give up |
| 5 | `E-7` | on a route, with a reroute left |
| 6 | `E-8` | a straight along the car's axis reaches drivable ground |
| 7 | `E-9` | where the car stands is not itself an obstruction |
| 8 | `E-10` | always |

## Adding an entry

1. A number from the brief that has never been used, added to [`Maneuver`](../framework/Maneuver.cs).
2. A file under `planned/` or `reactive/` with `Begin`, `Tick`, `ThinksEveryTick` and `Watched`.
3. A page here, on the same template as the others.
4. One line in each switch of [`ManeuverCatalogue`](../framework/ManeuverCatalogue.cs).

Nothing else. If a fifth thing is needed, the seam is in the wrong place.

**An entry nothing reaches is a finding**, and the last line of `--bench maneuvers` reports the set of
them — which is why [`Maneuver`](../framework/Maneuver.cs) names every entry of `AGT-7`'s list and not
only the ones with code behind them ([decision-log.md](decision-log.md)).

## Where the folders are, and why the namespace is flat

`framework/`, `planned/` and `reactive/` are folders and not namespaces. A catalogue is read as one
list — the grouping is for whoever opens the directory, and `using` lines that changed with it would
make an entry's file say which drawer it happens to be in.
