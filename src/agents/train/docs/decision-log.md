# The train — decision log

## 2026-10-23 — the showroom's yard

**The owner asked for trains in the showroom, "same logic as cars there"**: every look once, standing, driven only by
hand (TRN-14).

- **A yard of sidings along the map's foot** (`ShowroomPlan.Yard`): straight tracks a double line's spacing apart, each
  its own line run one way past the map at both ends, so each fields exactly one train and the run-in, the claims and the
  hand's controls are the ones every train has. Six, for the four locomotive looks and room for more, as the car park
  lays more bays than the fleet has looks.
- **A train a locomotive look, never a look alone**: a lone coach is no train of TRN-4's, so each wagon look is dealt to a
  locomotive that hauls its kind, the diesels taking tanks and coal in turn — 4 trains of the 11 looks.
- **Stood, not sent** (`Trains.Stand`): put down braked mid-map at the first tick however the town is held; the timetable
  never runs. Its own driver is the brake on, so a train let go stops where it was left instead of pulling on at the last
  hand's throttle. The pace claim (`RailWatch`) is not laid where the trains stand: there is no driver to hold one.

## 2026-10-23 — a diesel's fans cut off its picture and turned

**The owner asked for the trains' vent props cut and turning, with the existing cutting tool.** The round radiator fans
on the two diesels are what turns; a coach's roof vents are square and stay as drawn, and the electrics draw none.

- **Cut with `qq props`, as a car's lamps are** (TRN-13): a `fan*` prop is traced from its hub as the dark round opening
  (`--lens=disc`, an ellipse fitted through the rims along rays, so a spoke or a pale stretch of shroud does not bite
  into it), drawn once, and `install` writes the vehicle's `fans` and keeps the props beside the picture's variant.
- **The hub is where the rings round it are most alike all the way round**, out to a share of the reach: out to the
  shroud, `diesel_green`'s ring drawn a texel off the hub pulled the centre onto itself, and turning the disc by a blade
  read the blue fans' 8 blades as 7 or 9 off their speckled ground.
- **A fan is made round, and sized by us, not by the art** (the owner: "they need to be circular to spin nicely"): the
  four are drawn 7–10 % out of round, and the first cut — the largest disc about the hub inside the outline, the
  picture's texels as drawn — turned a squashed fan's blades and wobbled. The file keeps the hub and the rim's fitted
  ellipse (`DrawnFan`), and the opening is warped onto a circle about the hub as wide as the rim can reach from it, read
  a texel and a half short of the rim, since the art's rim wanders off the ellipse and turned reads as nicks. So a fan
  at rest is no longer the picture's texels exactly. How it is drawn is the render log's (2026-10-23).
- **It turns at a drawn rate, not a real one** (`ViewFigures.FanTurnsPerS`): a radiator fan's fifteen or so turns a
  second strobes at any frame rate.

## 2026-10-19 — a train long enough to pass over itself, slowed by a curve

**The owner: "make trains longer for this"** — the loop over itself (`RAIL-11`), 450 m between its passes on a double
line; offered that trains would slow for curves at a radius this tight, the owner took it.

- **18 coaches, 34 coal wagons, 39 tanks**: 478 m, 491 m and 486 m, each longer than the loop between its passes
  (`RailLoopTests`). The run-off is 900 m, so the longest still stands off the map a warning short of it (`RAIL-1`).
- **A curve sets the pace its driver wants** (`TRN-12`): at 65 m and its 28.6 m/s an intercity pulled 12.6 m/s²
  sideways, and a coach on the old 22 m/s wheel test at 45 m stood 15.7 m off its line. The one figure authored is the
  sideways pull a driver allows (1 m/s², a main line's comfort limit without cant); each chord's speed is derived off
  its radius once, when the trains are laid, and its brake's reach off steel-on-steel friction.
- **It brakes only past a tick's braking over the pace it wants**: at its pace on a straight, a pull laid to land on it
  overshoots by rounding, and braking for that would flicker every brake of the train.
- **The watch's claim is the pace it wants, and a train closing on it keeps it** (`RailWatch`): pulling back up past
  the loop, a train is off its pace by more than the allowance for as long as its traction takes, and is not held up.

## 2026-10-19 — coupled buffer to buffer

**The owner: the collider "should exclude that connector part, so wagons touch with it".** A vehicle is collided as its
body (`BodyM`), its footprint short of the buffers it authors at each end (`bufferM`, read off its art: 0.35 m a coach
or locomotive, 0.5 m a coal wagon, 0.55 m a tank); it is still drawn and picked by its footprint. The solver's bar runs
from body end to body end, as long as the two buffers, so the buffers meet. The fixed 0.3 m gap between buffer faces
that the bar held before is retired.

## 2026-10-19 — a train is selected, followed and driven as a car is

**The owner asked for a train to be picked out, followed and taken by hand as a car and a walker are**, with "train
actions — not hardcoded for manual mode, just actions AI driver can also do — accel, deccel, brake, switch track along
the way (first up to 100 m)".

- **`TRN-1` and `TRN-2` are retired**, the owner's words: "drop that P0, that was a tmp note. If we can make AI drivers
  act — that's ok". What was left of them that is still so is the own driver's (`TRN-12`, ours); the run-in past the map
  that cited `TRN-1` is `TRN-7`'s.
- **A rail vehicle is a unit of its own** (`CTL-1`), the owner's: "let's not select its wagons — they are different
  units, only loco; well, you can select a wagon as a separate unit". A click on the locomotive picks out the unit the
  train is driven by; a click on a wagon picks out that wagon, followed and read, and driven by nobody.
- **One set of controls for both drivers** (`TRN-10`): a share of traction, forward or back, the brake on every
  vehicle and a branch step. The keys are a car's: "same as car — you first brake then go backwards", so the key against
  the way the train is going brakes it and, from a stop, pulls it back.
- **No top speed under a hand**: "actors can drive as fast as they physically can, if they decide to … suggestions for
  trains are not needed, since it will always keep its track". The pace a train's own driver holds is its kind's, and is
  what it decides, not what the track says.
- **A/D throw the next facing switch within the set-ahead distance** (`TRN-11`), left or right as it is faced — the
  branches are ordered by their turn from the common end, so a step is a position up or down. The leg is routed again
  from the branch, and a throw onto ground another train's leg holds is refused, so `TRN-5` still holds by construction.
- **A train backing retraces its leg**, because the driver now sets every switch on its leg — the ones it trails through
  as well as the ones it faces (`TRN-9`); a switch is set the way the train came, and nothing about a reversed train needs
  a route of its own. Backed off the map by the way it came, it is taken out as at the other end (`TRN-7`).
- **A crossing is closed against how far a hand-driven train could be pulled in the warning time**, either way, and not
  against what it asks this tick: a hand may floor it at any moment, and `RAIL-1` is a warning before it gets there. A
  train standing under a hand near a crossing holds it shut. The own driver closes them at its pace, as before.

## 2026-10-19 — how the vehicles, the claim and the crossings were built

- **A rail vehicle is an agent past the roster, not a car of the fleet.** It is a body like a car's — mass, its own
  wheels, the solver's couplings, every step on the device as on the host — but a car of the fleet is read by parking,
  lamps, wrecks, road routes and the road's line reading, none of which a rail vehicle has. Its driver is its train's,
  run every tick for the few trains a town has (`Trains`), rather than a turn of its own.
- **A crossing keeps its own hold, fed from the real trains.** A train's claim reaching a road through the marks would be
  a secondary claim over the crossing, and a secondary claim is refused by no body: a car already standing on the
  crossing when it closed would be held on the track, against `RAIL-6`. The crossing's hold ends at the first body on its
  approach, as before, and is laid off where each train actually is on each track its leg runs, a warning ahead of its
  front. The train's own claim lies on its rail lanes alone.
- **A train a way onto the map.** A double track's two ways share no ground, so a line has a train for each lane its
  tracks lead onto the map by, each with its own pool and clock (`TRN-6`), and two run at once where their legs keep
  apart (`TRN-5`).
- **What a locomotive pulls with is its own file's** (`tractiveEffortN`), as its mass and its bogies are: a figure of the
  vehicle and not of the town.

## 2026-10-19 — a train is vehicles on rail lanes, coupled by the solver's link

**The owner asked for working trains, not a scripted event**: "they are basically just cars towing each other with
different wheel physics that limits their positions to the rails but allows free rotation". Each tick is a car's —
each wheel, then its body, then the forces in the solver — and nothing about a train is posed, pathed or timed by a
script. The owner's words settled each part:

- **`TRN-3` restated by the owner**: a train is a real, heavy mass and takes no damage.
- **One solid link for the tow and the train** (`SOL-38`), free to spin, with "no special physics treatment for link".
  What a train gains on top is its wheel base: each axle or bogie is one large wheel, free to rotate, several along a
  vehicle (`TRN-8`). Unbraked a rail wheel rolls on very little; locked, it holds very hard.
- **Rail lanes are real lanes, and a train is a real actor** routed over them: the map may lay any network, and its
  switches are inferred where tracks meet, each a line in one of its positions that the physics runs as one solid
  line; the driver sets one about 100 m ahead (`TRN-9`). A wheel knows only its track — it cannot derail for now —
  and all of it runs on the GPU but the driver, as a car's does.
- **Dumb for now**: the train places a high-rung claim as police do and never stops; it expects everything to yield
  or to have been scheduled ahead of it. More than one train runs at once, placed so their legs never share ground
  ("no need to do any hard math, its all tmp solution", `TRN-5`).
- **No collision exemptions**: vehicles are spaced so they do not touch, and a track that crosses itself makes a
  train meet itself. Bridges work as a car's do.
- **Nothing is removed off the map** (`TRN-7`): a train whose tail has left is taken out whole, waits about two
  minutes for a round trip estimated rather than simulated, and is put back on the track's other side.
- **Traced maps are out of this work**: the target is the generated and laboratory maps, whose tracks meet exactly.

## 2026-10-18 — trains, at the owner's ask

**The owner asked for full trains, not trams**: three kinds — intercity passenger, oil and coal — that stop
nowhere on a map and come in and go out now and then. **What a train is, is the owner's at `P0`** (`TRN-3`,
`TRN-4`).

- **On a bridge a wagon is on a channel of its own** (`RAIL-8`): `OdesaOsm`'s first run met 2 cars and 11 walkers in
  337 s of trains, the line passing over streets on its bridges. Read off the wagon's middle it still met 6 walkers,
  4 of them under a bridge's end a wagon reached over, so **any of a wagon over a bridge puts it on the bridge's
  channel**; the 2 left are beside the track at no crossing (`docs/tech-debt.md`).
- **A train's art is the ground's grid and asked in rows**, two or three vehicles to a row: a 3×3 sheet draws a coach
  under 20 px/m.

## 2026-10-19 — every vehicle drawn, the freight diesel one way

**The owner asked for all the art generated, "minor size deviations allowed, adjust collider"**, a freight train's
locomotive one-way rather than two, and everything strictly top-down.

- **A diesel has one cab, at its front**: a train's first vehicle is its locomotive and every vehicle is laid facing the
  way its train runs (`Trains.Place`), so the cab leads whichever way a track is run; the electrics keep a cab at each end.
- **The narrow vehicles' collider was widened to what the art draws**: a coach and an electric 3.1 → 3.4 m, a tank 3.0 →
  3.3 m. The model draws a long thin outline some 12 % wide however it is asked, so the rest is fitted down
  (`qq sprites`' rows allowance) rather than the collider widened to 3.8 m, a broad-gauge coach's width and a half again
  of its overhang.
- **The grey gondola is out of the rolling stock** until it is drawn: twice tilted and once never drawn, at the cap. Two
  coal looks remain.
