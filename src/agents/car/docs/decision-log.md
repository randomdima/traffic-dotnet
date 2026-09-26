# The car agent — decision log

Why this slice reads the way it does — the driver, the body and the tyres. Only decisions still binding
are here: a superseded one is deleted, not annotated.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- `CAR-1`: a car acted while it contained a driver → while something drives it (an errand, an order, a
  hand, the tour); nobody boards a car.
- `CAR-8`: a car's destination was its driver's → it is whatever drives it, and on a map with no reachable
  bay the tour, with none.
- `CAR-9a`: the driver continued on foot → the car is stood down where the leg's clock ends it, and nobody
  gets out.
- `CAR-11`: the bay a car will fit in was among its decisions → nothing decides that (`CAR-11b`).
- `CAR-11a`: the proving ground stood the nominal car → no town does; the crash sandbox's fleet is the one.
- `CAR-11b`: a bay was refused a car too big for it → `ParkingRegistry.Takes` answers it and nothing asks.
- `CAR-13.1`: two courtesies dropped, the red and the uncontrolled crossing → the red alone.
- `TLT-2`: walkers read the signals → cars do; a walker reads none.
- `TLT-2a`: walkers began on green and a car shunted over the bar returned behind it → a car only, and a
  rear axle past the bar has started however it got there.
- The heads (`agents/trafficlight`): a car head stood beside the carriageway → on the bar's own centre line,
  on the tarmac.

## 2026-09-26 — `S-6` said what `SIM-6` says, about suspensions only the catalogue made

`S-6` held the hard rules binding everywhere, a recovery included, and let lane legality and the no-idling
rule be suspended only where an entry's page said so. The first half is `SIM-6`'s own "hard rules never
lift, in planning or in recovery"; the second bounded the catalogue's pages, and every entry that suspended
anything went with them (2026-09-20). Nothing cited it. Retired: `S-6`.

## 2026-09-20 — the manoeuvre catalogue is gone, and a driver is a line, a route and a clock

Seventeen named entries, a desk, a scene of forty-six fields, a nine-rung ladder, a plan skeleton and a
trace — and **the three entries that carried all of the driving imposed nothing**: `P-4`, `P-6` and `P-8`
were names read off the term that had already bound the speed profile, the argument that had retired `E-1`
and `P-12` one at a time. **The measurement decided it**: over a minute of each shipped city, 86–90 % of
car-ticks were spent in no entry at all, driven by the standing rules and the claims alone, and nine of the
seventeen entries were never entered on either map.

A driver is now the walker's own tick (`CAR-15`, `PER-25`), with the leg's clock one class under both
(`World.Routing.LegProgress`) and its three extras stated rather than scattered. **Parking and unparking are
driving the bay's own way**, taken as the next line from rest at its mouth rather than threaded onto the
route's line — threading only ever worked for the half of the templates driven forwards, and dropping it is
what deleted them. **The emergency stop and the place an errand sent a car to became terms of the speed
profile** (`CarFollower.IsAHazard`, `DrivingHold.Place`), neither having imposed anything a term could not.
The read-out lost nothing: what a car is doing is its line and the term that bound it (`CAR-15b`), which is
what the panel drew beside the entry's name anyway.

**What went, and what it cost.** The four entries that drew geometry went with the rest — the swerve
(`E-4`), the back-off (`E-3`), the straight back to legal ground (`E-8`) and the shunt (`P-19`) — and the
swerve's own log had recorded 352 of 353 shapes refused by the terrain. Over five minutes of Odesa against
the tree this replaced: **cars standing still at the end 350 → 170, the longest any one held a spot 300 s
→ 142 s, never moved 1 → 0**, and the drive probe unchanged — mean 11.79 → 11.85 m/s, off-line 0.193 →
0.190 m. The catalogue was not driving. What is given up is in
[the known gaps](../../../../docs/index.md#known-gaps).

## 2026-09-07 — a speed cap is a figure, and `CAR-5` is retired

`CAR-5` said reverse has a lower cap and that acceleration and braking are bounded — three numbers, and a
rule states a relation while a number is data on `SimConfig`. The bounds are the tyres' (`CAR-3e`) and the
reverse cap is authored; nothing cited the paragraph.

## 2026-09-01 — paint is not a speed limit, and `CAR-7b` is retired

The pace bound whenever paint was within reach, so the panel read `yielding at a crossing` on an empty
crossing. A crossing is ground and the claims on it already say whose it is (`TER-4c.1`, `TER-5e`), so the
pace was a second gate on a movement the claims had answered (`SIM-7`) — and, owed whether or not anybody
was there, the one term of the profile no reading of the world could switch off. Car-ticks bound by a
crossing fell 52 % → 26 %. The cost is that the stop that is left is made from road speed.

## 2026-08-31 — the indicator was reading a bend, and a bend is not a turn

CAR-14.1 read the indicator off the line's bend over twenty-five metres, which on the idle ring is
fifty-eight degrees on every metre of it — correct under the old rule, which is what made it worth changing
rather than tuning. Subtracting the bend behind from the bend ahead was tried and kills the second half of
every junction turn: a car deep in a left-hander stops indicating exactly when the side road is reading it.
The indicator is gated on the junction instead, on the `LaneTurn` already computed for right of way. What
it loses is the bay exit, which has no junction in front of it.

## 2026-08-30 — the engine was authored in m/s², and no car in the fleet could reach a third of its own pedal

Only three squares of the pad's lightest row sat on their geometry — exactly the three looks whose authored
acceleration was at or under what their driven tyres could put down. `sports_pink` asked for 18.0 m/s² and
`super_cyan` for 20.5 where the most either could put down was near 6 and 11, and above the traction limit
the pedal buys nothing (CAR-3b). The brake was already authored against the rubber
(`BrakePedalInTyreGrips`); the throttle is now too (CAR-45), in four bands, and fifteen of nineteen pull
away exactly as they did. The 270 km/h cap was a derived observable this build cannot derive — rolling drag
is flat in the speed, so no terminal velocity exists — and is a governor at 144 km/h, because sight
distance is stopping distance and `super_cyan` had been planning 786 m ahead in a town of 150 m blocks.

**Honest pedals shortened the claim, and four of sixteen wrecked on the fleet lap where none had.** Setting
the claim against the planned speed instead is wrong — a stopped car would hold road it could not have
driven over. The margin has to come from the driving model, and it is left failing rather than bought back
by putting the fiction back.

## 2026-08-30 — a hand's ellipse is the hand's own car's, and the pad stopped hunting

`DriveCeilingMps2` measured against the nominal patch while `TyreModel.Step` spent `CarBuild.GripMps2`, so
a grippier look had its throttle shut off at a lateral its own rubber was still holding — the second gate
SIM-7 forbids, and a disagreeing one. It oscillated: a demand that crosses the ceiling has no feedback below
it and all of it above, and the lateral it switches on is lagged, which is a relaxation oscillator. The
hand's half moved to the car's own patch; the self-driver's stays nominal for the reason 2026-08-25 gives,
and a hand reads no crossings so waits on nothing. Every square settles, and the fleet means went 1.49× →
1.86× the axles.

## 2026-08-29 — a panel may move the road, and a car is not the road

A trim (`TrimFigures`) scales what a car resolved to and not the nominal behind it, so a shipped run
resolves the same car bit for bit; letting a slider go changes the figure under the town that is standing
rather than laying the map again, because tearing the town down would lose the marks on the road. Seven of
nine dials spoke for nineteen bodies at once, over figures the fleet authors per car — one number
pretending to be nineteen. What is left is two, both the road's: the coefficient between rubber and
tarmac, and what each ground costs a wheel going round. `StaticFrontShare` moved from `TyreFigures` to
`CarFigures` on the way, since where a body's mass sits along its wheelbase is never a fact about the
rubber. It cost the handling rig, which was already the wrong tool: winding the whole fleet's mass together
holds nothing still.

## 2026-08-29 — a tyre is bolted to a car, not to a town

The fleet ran one wheel across a catalogue whose tracks run 1.44 m to 2.20 m and whose masses run three to
one. `wheelM` and `wheelRotatingMassKg` are the variant's now, optional and falling back to the nominal as
`maxSteeringDeg` does. The nominal stays, and that is not the same mistake: `Car.WheelbaseM` and the rest
are the car the *town* is cut against (CAR-11a), and a junction cannot be laid against nineteen cars.

## 2026-08-29 — the load transfer is read off the tyres, so nothing has to cap it

`LoadTransferInGrips` was a number chosen to hide a wrong input: the transfer was read off the body's total
acceleration, which cannot tell a brake from a kerb, and handing the quasi-static `a·h/(L·g)` a one-tick
collision impulse pinned three corners at the floor and spun cars after a nudge. The transfer is read off
the horizontal force at the patches, which the tyre model already computes and which is bounded by
construction. What it costs is the kerb, and the kerb was never there — an impact pitched a body about as
hard as a hard brake, which is the clamp being simulated. `MinCornerLoadFraction` went with it, replaced by
nought and one, which is not a figure but what a load is.

## 2026-08-29 — there is one coefficient of friction, and a per cent is not a mechanism

Three tyre terms were tried in a day and all three are gone. `LongFriction` beside `Friction` was an
anisotropy factor wearing a coefficient's name. Its replacements were honest physics and went for a
different reason: on the shipped geometry load sensitivity costs a corner 3.2 % against a stop's 2.0 %, so
**a stop beats a corner by 1.2 %** on a town watched from above, at the price of a term threaded through the
tyre model, the build, the panel and the variant file. And `LoadFactor` cancelled the static balance
exactly, so the understeer CAR-3d and two doc-comments claimed never existed. CAR-3d is retired and
**CAR-3e** says the positive version.

## 2026-08-29 — figures flow one way, and half of them were flowing the other

A turning circle was an input with the steering angle solved backwards out of it, and grip was a
coefficient and a gravity already multiplied together, scaled by a `cornering` figure naming nothing
measurable — derived observables standing where raw terms belong, so nobody could check any of them.
`Tyre.Friction` is a coefficient now, a variant states `maxSteeringDeg` at the road wheel, and
`CarBuild.TurningCircleM` is worked out from the lock and the body. Two knife-edges came out that were
already there: a test whose cancellation is exact in algebra and not in single precision, and a crawl bar
the van cleared by 1 % and now misses by 2 %.

## 2026-08-29 — where a car carries its weight is a figure, and the fleet's own figures were taken back out

The even split was a literal in the load arithmetic and the largest thing in the model nobody had
authored: held at 0.62 the fleet's forward circles collapse from 1.78× its own axles to 0.84×. It became a
figure. Every look was then given the share its kind carries, and on the pad front slip at a third of the
pedal fell from 14.0° to 5.5° — then the town fell over on six tests, because the follower plans on a
bicycle model with no balance in it, so a car with a light rear axle rotates more than the plan and a bay
approach overshoots. The mechanism ships and the fleet's figures were taken back out.

## 2026-08-29 — the tyre figures are a road tyre's

Every look cornered at 1.05–1.63 g and stopped at 1.72–2.67, which no road car on dry asphalt does, and
every one turned an 8.5–11.2 m circle whatever it weighed — a 4200 kg armoured car turned inside a
hatchback. The figures became a road tyre's, and a variant states `cgHeightM`, which is the whole of what
makes a tall body handle like one.

## 2026-08-27 — an indicator is the side of the lamp the artist drew, where there is one

An amber block beside a headlight reads as a sticker whether lit or dark. Where the art draws a lamp the
lens is a section of it (CAR-14b), at the end nearest the flank — the part of a real cluster the indicator
is; taking a whole diagonal headlight would flash the main beam amber. Two of them are white glass because
the art paints a clear-lens indicator, which the cut turns amber when it burns. Plain-nosed cars keep the
block, there being nothing to be a section of.

## 2026-08-27 — a lit lens shades with how solidly it is drawn, never with darkness

A four-texel lens of deep red glass and one chrome highlight painted the glass near-black at four-fifths
opacity, punching a black strip through a red light; four thousand texels of the sheet were darker than
the light they sit on. The two ramps became one: a texel is drawn as solidly as it is burning, so a bezel
reads by letting the glow through rather than by covering it — the argument that stopped an unlit lens
being drawn at all. The ramp bottoms out at a dim fraction of the lamp's colour rather than black, held by
a unit test on the shipped picture.

## 2026-08-26 — a second bar is a crossed pair of fittings and not a phase written down

A phase number on the lens is a second place for the swap to be described from. What a beacon end carries
at rest is already the whole of its phase, so the rear bar's ends are entered crossed: the arithmetic is
untouched and cannot disagree with itself. The rear bar is four lenses and the brake pair moved off it,
since a bar lighting one block of two reads as a lamp that has failed.

## 2026-08-26 — the service cars were redrawn, so everything measured off their pictures was measured again

The file describes the picture and cannot be left behind by it. The track widened because the panels did
(CAR-12) — the new bodies are drawn sill to sill — and the art was not narrowed to protect the old numbers,
the track being the picture's figure. The evacuator's works bar is a fitting of its own rather than a
beacon's ends, because a yellow lamp turning yellow twice a second looks broken: what flashes is which end
is burning. Its white middle is a work light and not a claim on the road. And it burns for the work, not
the priority (CAR-14.6): lit off `BlueLight`, the truck standing over a wreck in a live lane went dark
exactly when a real one turns its beacon on.

## 2026-08-26 — a lit lamp is cut from the car it is on

A strip drawn once for the whole town has cells 64 texels square stretched over lenses of 166 to 640
texels a metre, on car sprites drawn at 96 — the lamps read as stickers. `--lamps` cuts each lens
rectangle out of that variant's own sprite and drives it emissive, so the bezel, the shape and the grid are
the artist's and the only thing applied is light; the unlit lens is not drawn at all, being the section the
lamp was cut from. The floor that widened a lens too small to see moved onto the glow, because widening the
lens moves it off the panel it was measured onto.

## 2026-08-25 — a lamp is a section of the picture

A glow placed by arithmetic lands wherever the sum lands — on a hatchback's tailgate, on a pickup's bed,
and on nobody's car on the lens the artist drew. A lens is authored art measured off the variant's own
picture (CAR-14a); twenty files carry six numbers each, and what that buys is that the lamp *is* the car.
The rear cluster shows one thing at a time and the pedal outranks the gear (CAR-14.3), since inventing a
second lens inboard would put a reversing lamp on bodywork nobody drew one on.

## 2026-08-25 — an indicator is read off the line, and a lamp is not state

A signal the manoeuvre set was eighteen entries each having to remember to announce itself, and a car that
indicated left for the rest of its trip the first time one forgot. The line is the same intent already
written as geometry. Every lamp is a read of something else — the pedal, the gear, the line, `BlueLight` —
so a frame drawn twice at the same tick draws the same lamps. The beacon shows the priority and nothing
else (CAR-14.4): lighting a patrol's bar because it looks better would show a claim on the road that SRV-5
does not honour.

## 2026-08-25 — recklessness is the person's, and it drops two courtesies rather than the rule set

The habit is read off the driver and not flagged on the car (`TownWorld.RecklessAtTheWheel` says why). A
driver that ignores the road's claims was tempting and is a second physics: the claims' right of way
(TER-5e) is the whole of why nobody is ever driven into on purpose. So a reckless driver runs the red and
does not wait for somebody on the kerb, then meets the same profile and the same bodies as everybody else.
The red they cross is counted, where AMB-4.2 exempts a rescue and so counts nothing.

## 2026-08-25 — the wheel travels, and the throttle is bounded by the corner

A key press was a selection rather than a control: `A` put the steering on its stop in the tick it was
pressed. The wheel travels now (`WheelTravelS`, CAR-3a) and binds the hand and the follower alike, because
a rack is a fact about the car. A car under
power lifts for the corner it is in (CAR-3b) — the driven axle's ceiling is the ellipse's remainder — and a
hand gets the remainder and nothing else. The rear-drive complaint was not the tyre model: a rear-driven car
at full lock runs wide because it *accelerates*, and a front-driven one cannot because its driven axle is
its steered one. **The self-driver's ceiling stays on the nominal patch**, the one figure left in the tyre
path that is not the car's own: moving it is correct by CAR-11 and was tried, and a faster car gets a
shorter approach to a crossing because `CrossingOnTheTemplate` reads only the nearest lane.

## 2026-08-25 — a variant's wheels are read off its own picture

Every `trackM` came out at about half its own width, and several axle figures were the mirrors rather than
the arches; the figures are taken from the art, whose silhouette bulges where the arches are. Then the
wheels disappeared, which is how the track got its rule: authored at a real car's track, every tyre was
under bodywork drawn to the edge of its own sheet. Two faults at once — every car was drawn at the nominal
4.0 × 2.0 m however big it was (CAR-12a), and the track is now the width of the bodywork over the axles
(CAR-12), measured off each picture's alpha. Showing the tyres showed thirty-eight loose fragments painted
outside the body outlines, which were erased; lights, mirrors and tow eyes were left, being pictures of
something.

## 2026-08-25 — a car is the car it is drawn as, and the line is a recommendation

Every car was the nominal car with somebody else's picture on it, the variants' figures unread. `CarBuild`
resolves a variant once against `SimConfig`. The town's geometry stayed the nominal car's (CAR-11a),
because re-sizing a town per car is nineteen towns — from which follows the doctrine this is really about:
what the town precomputes is a recommendation (CAR-10), not a rail. It found a real defect in the junction
protocol: ranks are compared off claims laid at the top of a tick, and with cars that brake at their own
rates a car could cross into "cannot stop" inside the tick it was traded against, so a stronger movement
was waved across a body already committed. Commitment is judged a decision ahead now.

## 2026-08-22 — the claims are the only thing a driver looks at

The rays went, once everything that can be on a lane claimed it: the traffic, anybody on foot in a lane,
and the town's own furniture projected onto the lanes once at load. Furniture was the one case a ray was
still earning, and the shipped cities have **no** prop in a carriageway at all. The deeper reason is that
the two answers disagreed: a cast found a shape and could not say whose it was, so a body the network never
had came back `Unknown`, which is never driven round, and a claim with nothing standing on it yet came back
as empty road. Odesa's cars went 375 → 171 µs of the ranked tick.

## 2026-08-22 — a driver looks as far as it needs to stop, and not as far as the pedal could ask for

The reach was the stopping distance at the brake pedal's cap, 27 m/s², against the 17.79 the profile plans
with — 112 m of looking for a 158 m stop, so a car at top speed could not stop for anything only a ray would
find. It never showed while every stop was a painted bar, which a driver knows the place of from any
distance; the proving ground's pacers found it. The reach is a reaction interval and a stop at the rate the
profile brakes at, the figure the line is grown to (`SimConfig.CarSightM`).

## 2026-08-22 — a car drives through the end its own variant drives through

The tyre model has always spent the variant's drivetrain and the fleet has always shipped one per variant;
only the nominal figure ever reached it, so every car in every town was front-wheel drive whatever it was
drawn as. What it bought was a comparison: a front-drive car tops out a sixth under the other two down the
straight and takes a quarter longer to get back up to speed.

## 2026-08-20 — which procedure runs is the line's question

The standing rules pick the route procedure or the bay-way one off the line itself, because anything keyed
on what the car was last told to do can hand a car to the route procedure with no lanes under its line.

## 2026-08-19 — a car at a standstill has no tyres to work out

A standstill has no slip to resolve and no mark to leave, so the whole four-wheel solve is skipped below a
threshold — the largest single saving in the car's tick, and free because the result it skips is
arithmetically zero.
