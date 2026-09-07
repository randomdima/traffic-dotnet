# The car agent — decision log

**The manoeuvre catalogue keeps its own log**, in
[maneuvers/docs/decision-log.md](../maneuvers/docs/decision-log.md): what an entry is, how the plan is
chained, why an entry is entered on one thing and left on another. What is here is the body and the
tyres.

## 2026-09-07 — a speed cap is a figure, and `CAR-5` is retired

`CAR-5` said reverse has a lower cap and that acceleration and braking are bounded — three numbers, and a
rule states a relation while a number is data on `SimConfig`. The bounds are the tyres' (`CAR-3e`) and the
reverse cap is authored; nothing cited the paragraph.

## 2026-09-01 — paint is not a speed limit, and `CAR-7b` is retired

The pace bound whenever paint was within reach, so the panel read `P-4 yielding at a crossing` on an empty
crossing with nothing claimed against it. A crossing is ground and the claims on it already say whose it is
(`TER-4c.1`, `TER-5e`), so the pace was a second gate on a movement the claims had answered (`SIM-7`) —
and, being owed whether or not anybody was there, the one term of the profile no reading of the world could
switch off. Car-ticks bound by a crossing fell 52% → 26%. The stop that is left is arrived at from road
speed, which is what two broken claims on Odesa are: a car that used to meet a waiting walker at 8 m/s now
meets one at road speed and stops as hard as it can.

## 2026-08-31 — the indicator was reading a bend, and a bend is not a turn

CAR-14.1 read the indicator off the line's bend over twenty-five metres, which on the idle ring is
fifty-eight degrees on every metre of it. That was correct behaviour under the old rule, which is what made
it worth changing rather than tuning: a road of constant radius offers nowhere else to go. Subtracting the
bend behind from the bend ahead was tried and kills the second half of every junction turn — a car deep in
a left-hander stops indicating exactly when the side road is reading it. The indicator is gated on the
junction instead, using `LaneTurn` already computed for right of way, so what a car announces and what it
gives way to are one answer. What it loses is the bay exit and the way round an obstruction, which now have
no junction in front of them.

## 2026-08-30 — the engine was authored in m/s², and no car in the fleet could reach a third of its own pedal

Only three squares of the pad's lightest row sat on their geometry, and they were exactly the three looks
whose authored acceleration was at or under what their driven tyres could put down. `sports_pink` asked for
18.0 m/s² and `super_cyan` for 20.5 while the most either could put down was near 6 and 11 — and above the
traction limit the pedal buys nothing (CAR-3b), so the excess described nothing that ever happened. It was
authored the one way the file beside it says not to: `BrakePedalInTyreGrips` tracks the rubber and the
throttle two properties above it did not. The pedal is a multiple of what the driven axle holds (CAR-45),
authored in four bands. It cost the fleet nothing, which is the point — fifteen of nineteen pull away at
exactly the figure they did. The cap was 270 km/h, which is a derived observable this build cannot derive
(rolling drag is flat in the speed, so no terminal velocity exists); it is a governor at 144 km/h, because
sight distance is stopping distance and `super_cyan` had been planning 786 m ahead in a town of 150 m
blocks. And all of it exposed that the town's spacing was buying margin from the fiction: with honest
pedals the claim is shorter and four of sixteen wreck on the fleet lap where none did. Setting the claim
against the planned speed instead is wrong — a stopped car would hold road it could not have driven over.
**The margin has to come from the driving model**, and it is left standing and failing rather than papered
over by putting the fiction back.

## 2026-08-30 — a hand's ellipse is the hand's own car's, and the pad stopped hunting

`DriveCeilingMps2` measured against the nominal patch while `TyreModel.Step` spent `CarBuild.GripMps2`, so
a grippier look had its throttle shut off at a lateral its own rubber was still holding — the second gate
SIM-7 forbids, and a disagreeing one. It oscillated: a demand that *crosses* the ceiling has no feedback
below it and all of it above, and the lateral it switches on is lagged, which is a relaxation oscillator.
The hand's half moved to the car's own patch and the self-driver's did not, since a hand reads no crossings
and so waits on nothing. Every square settles now, and the pad reads again — the fleet means went 1.49x to
1.86x the axles.

## 2026-08-29 — a panel may move the road, and a car is not the road

Seven of nine dials spoke for nineteen bodies at once, over figures the fleet already authors per car — a
dial over a figure each body states is one number pretending to be nineteen. What is left is two, and both
are the road: the coefficient between rubber and tarmac, and what each ground costs a wheel going round.
`StaticFrontShare` moved from `TyreFigures` to `CarFigures` on the way, since where a body's mass sits
along its wheelbase is never a fact about the rubber. What it costs is the handling rig, which was already
the wrong tool — winding the whole fleet's mass together holds nothing still.

## 2026-08-29 — a tyre is bolted to a car, not to a town

The fleet ran one wheel across a catalogue whose tracks run 1.44 m to 2.20 m and whose masses run three to
one. They are `wheelM` and `wheelRotatingMassKg` on the variant file now, optional and falling back to the
nominal — the pattern `maxSteeringDeg` already uses. The nominal stays, and that is not the same mistake:
`Car.WheelbaseM` and the rest are the car the *town* is cut against (CAR-11a), and a junction cannot be
laid against nineteen cars.

## 2026-08-29 — the load transfer is read off the tyres, so nothing has to cap it

`LoadTransferInGrips` was a number chosen to hide a wrong input: the transfer was read off the body's total
acceleration, which cannot tell a brake from a kerb, and `a·h/(L·g)` is the quasi-static relation — handing
it a one-tick collision impulse is a category error that pinned three corners at the floor and spun cars
after a nudge. Weight transfer is caused by the horizontal force **at the contact patches**, which the tyre
model already computes, so it is bounded by construction: a tyre cannot push harder than it grips. What it
costs is the kerb, and the kerb was never there — an impact used to pitch a body about as hard as a hard
brake, which is the clamp being simulated. `MinCornerLoadFraction` went with it, replaced by nought and
one, which is not a figure but what a load is.

## 2026-08-29 — there is one coefficient of friction, and a per cent is not a mechanism

Three tyre terms were tried in a day and all three are gone. `LongFriction` beside `Friction` was an
anisotropy factor wearing a coefficient's name. Its replacements were honest physics and went for a
different reason: worked through on the shipped geometry, load sensitivity costs a corner 3.2% against a
stop's 2.0%, so **a stop beats a corner by 1.2%** on a town watched from above, at the price of a term
threaded through the tyre model, the build, the panel and the variant file. And `LoadFactor` cancels the
static balance exactly, so in a steady corner all four wheels sat at the same factor whatever
`frontWeightShare` said — the understeer CAR-3d and two doc-comments claimed never existed. CAR-3d is
retired and **CAR-3e** says the positive version: what the loads decide is which *wheel* runs out first.

## 2026-08-29 — figures flow one way, and half of them were flowing the other

A turning circle was an input, with the steering angle solved backwards out of it; grip was a coefficient
and a gravity already multiplied together, scaled by a `cornering` figure naming nothing measurable. Every
one is a derived observable standing where a raw term belongs, and what it costs is that nobody can check
any of them. What is authored is now raw and what is looked up is derived: `Tyre.Friction` is a coefficient,
a variant states `maxSteeringDeg` at the road wheel, and `CarBuild.TurningCircleM` is worked out from the
lock and the body with nothing deciding on it. Two knife-edges came out and both were already there — a
test whose cancellation is exact in algebra and not in single precision, and a crawl bar the van cleared by
1% and now misses by 2%.

## 2026-08-29 — the fleet's weight distributions were authored, measured, and taken back out

Every look was given the share its kind carries and on the pad it did what it should — front slip at a
third of the pedal fell from 14.0° to 5.5°. Then the town fell over on six tests. The reason is not the
figures: the follower plans on a bicycle model with no balance in it, so a car with a light rear axle
rotates more than the plan and a bay approach overshoots. Nothing reads `FrontWeightShare`, and until
something does, giving a body a real distribution is giving its driver a car it is not driving. The
mechanism ships and the figures do not.

## 2026-08-29 — where a car carries its weight is a figure, and the figures can be turned while the town runs

The even split was a literal in the load arithmetic and is the largest thing in the model nobody had
authored: held at 0.62 the fleet's forward circles collapse from 1.78× its own axles to 0.84×, and its
astern circles blow out in the mirror of that. What it is *not* is why a third of the pedal turns the same
circle as all of it — that is because nothing in this model makes speed cost power, so the steady state
solves `drive = drag`, an equation the throttle does not appear in. So the figures got a panel
(`TrimFigures`): ten of them, each a share of what the build ships, laid out logarithmically. Letting a
slider go changes the figure under the town that is standing rather than laying the map again, and it
deliberately does not re-plan — tearing the town down would lose the marks on the road, which are the thing
the pad exists to show. A trim scales what a car resolved to and not the nominal behind it, so a shipped
run resolves the same car bit for bit.

## 2026-08-29 — the tyre figures are a road tyre's, and a turning circle is a figure off a spec sheet

Every look cornered at 1.05–1.63 g and stopped at 1.72–2.67, which no road car on dry asphalt does, and
every one turned an 8.5–11.2 m circle whatever it weighed — a 4200 kg armoured car turned inside a
hatchback. The nominal patch is 0.88 g and the long axis 1.10 of it: what a stop has that a corner does not
is four patches pulling the same way, which is a little over one and not 1.643. A variant states the circle
it turns rather than a share of somebody's lock, because a turning circle is a figure a person can look up
and a road-wheel angle is not. And a variant states `cgHeightM`, which is the whole of what makes a tall
body handle like one. The proving ground breaks two claims for it, and both are the speed axis rather than
the grip one: the straight is not long enough to give a car that stops at 0.96 g the sight it needs.

## 2026-08-27 — an indicator is the side of the lamp the artist drew, where there is one

An amber block beside a headlight reads as a sticker whether lit or dark. Where the art draws a lamp the
lens is a section of it (CAR-14b), taken at the end nearest the flank — that is the part of a real cluster
the indicator is, and taking a whole diagonal headlight would flash the main beam amber. The lens is white
glass on two of them because that is what the art paints: a clear-lens indicator, which is what the cut
turns amber when it burns. Plain-nosed cars keep the block, there being nothing to be a section of.

## 2026-08-27 — a lit lens shades with how solidly it is drawn, never with darkness

A four-texel lens of deep red glass and one chrome highlight painted the glass near-black at four-fifths
opacity, punching an opaque black strip through a red light; four thousand texels of the sheet were darker
than the light they sit on. The two ramps became one: a texel is drawn as solidly as it is burning, so a
bezel reads as a bezel by letting the glow through rather than by covering it — the same argument that
stopped an unlit lens being drawn at all. The ramp's bottom is a dim fraction of the lamp's colour rather
than black, held by a unit test on the shipped picture.

## 2026-08-26 — a second bar is a crossed pair of fittings and not a phase written down

A phase number on the lens is a second place for the swap to be described from. What a beacon end carries
at rest is already the whole of its phase, so the rear bar's ends are entered crossed: the arithmetic is
untouched and cannot disagree with itself. The rear bar is four lenses and the brake pair moved off it,
since a bar lighting one block of two reads as a lamp that has failed.

## 2026-08-26 — the service cars were redrawn, so everything measured off their pictures was measured again

The file is a description of the picture and cannot be left behind by it. The track widened because the
panels did (CAR-12) — the new bodies are drawn sill to sill — and the track is the picture's figure rather
than the handling model's, so the art was not narrowed to protect the old numbers. The evacuator's works
bar is its own fitting rather than a beacon's ends, because a yellow lamp turning yellow twice a second is
a bar that looks broken; what flashes is which end is burning. Its white middle is a work light and not a
claim on the road. And what it burns for is the work and not the priority (CAR-14.6): lit off `BlueLight`,
the truck standing over a wreck in a live lane went dark exactly when a real one turns its beacon on.

## 2026-08-26 — a lit lamp is cut from the car it is on

A strip drawn once for the whole town has cells 64 texels square stretched over lenses between 166 and 640
texels a metre, on car sprites drawn at 96 — the lamps read as stickers. The sheet is cut from the cars:
`--lamps` takes each lens rectangle of that variant's own sprite and drives it emissive, so the bezel, the
shape and the grid are the artist's and the only thing applied is light. The unlit lens stops being drawn
at all, being the section of the picture the lamp was cut *from*. The floor that widened a lens too small
to see moved onto the glow, because widening the lens moves it off the panel it was measured onto.

## 2026-08-25 — a lamp is a section of the picture

A glow placed by arithmetic lands wherever the sum lands — on a hatchback's tailgate, on a pickup's bed,
and on nobody's car on the lens the artist drew. A lens is authored art measured off the variant's own
picture (CAR-14a). The cost is real: twenty files carry six numbers each. What it buys is that the lamp
*is* the car. The rear cluster shows one thing at a time and the pedal outranks the gear (CAR-14.3), since
inventing a second lens inboard would put a reversing lamp on bodywork nobody drew one on.

## 2026-08-25 — an indicator is read off the line, and a lamp is not state

A signal the manoeuvre sets is eighteen entries each having to remember to announce itself, and a car that
indicates left for the rest of its trip the first time one forgets. The line is the same intent already
written as geometry. Every lamp is a read of something else — the pedal, the gear, the line, `BlueLight` —
so a frame drawn twice at the same tick draws the same lamps. The beacon shows the priority and nothing
else (CAR-14.4): lighting a patrol's bar because it looks better would show a claim on the road that
SRV-5 does not honour.

## 2026-08-25 — recklessness is the person's, and it drops two courtesies rather than the rule set

A flag on the car is the cheaper read and would have to be written on boarding, alighting, a wreck and an
abandonment — four sites that can disagree about one fact, for one array read saved. A driver that ignores
the road's claims was tempting and is a second physics: `RightOfWay` is the whole of why nobody is ever
driven into on purpose. So a reckless driver runs the red and does not wait for somebody on the kerb, then
meets the same profile and the same bodies as everybody else. The red they cross is counted, where AMB-4.2
exempts a rescue and so counts nothing. Adding the column showed the probe's
closing line had always been wrong — Odesa crosses eleven red bars in a minute with nobody reckless on it,
from a shunt and from a car committed when the phase turns. What that baseline is made of has not been
looked into.

## 2026-08-25 — the wheel travels, the throttle is bounded by the corner, and the fleet gets a lap of its own

A key press was a selection rather than a control: `A` put the steering on its stop in the tick it was
pressed. The wheel now travels (`WheelTravelS`, CAR-3a) and binds the hand and the follower alike, because
a rack is a fact about the car. Lock to lock is 1 s, which is what a hand at the keys wants; it is free on
the proving ground and costs shunts in the town, where the angles are large and the speeds small. Leading
the pursuit demand by the wheel's own travel is what would buy those back and is not done here. A car under
power lifts for the corner it is in (CAR-3b) — the driven axle's ceiling is the ellipse's remainder — and a
hand gets the remainder and nothing else. The rear-drive complaint turned out not to be the tyre model: a
rear-driven car at full lock runs wide because it *accelerates*, and a front-driven one cannot because its
driven axle is its steered one. The self-driver's ceiling stayed on the nominal patch, the one figure left
in the tyre path that is not the car's own: moving it is correct by CAR-11 and was tried, and a faster car
gets a shorter approach to a crossing because `CrossingOnTheTemplate` reads only the nearest lane. `Fleet`
is the third proving ground — one car of every look, watched from a standing start because sixteen cars
whose tops differ by 2.4× otherwise end up one queue behind the armoured car.

## 2026-08-25 — a variant's wheels are read off its own picture, and its weight off what it is

Every `trackM` came out at about half its own width, so read as the whole track it stood cars on wheels
tucked into the middle and read as half it stood them outside the bodywork; several axle figures were the
mirrors rather than the arches. The figures are taken from the art, whose silhouette bulges where the
arches are. Then the wheels disappeared, which is how the track got its rule: authored at a real car's
track, every tyre was under bodywork drawn to the edge of its own sheet. Two faults at once — every car was
drawn at the nominal 4.0 × 2.0 m however big it was (CAR-12a), and the track is now the width of the
bodywork over the axles (CAR-12), measured off each picture's alpha. It is geometry and not a drawing
trick: that is where the patch takes the ground. What it collides with is still the bodywork. Showing the
tyres showed thirty-eight loose fragments painted outside the body outlines, which were erased; lights,
mirrors and tow eyes were left, being pictures of something.

## 2026-08-22 — the claims are the only thing a driver looks at

The rays went. What made it possible was claiming everything that can be on a lane: the traffic, anybody on
foot in a lane, and the town's own furniture projected onto the lanes once at load. That last is the one a
ray was still earning, and the instrument says the shipped cities have **no** prop in a carriageway at all
— the ray was paying every tick for a case the real towns do not contain. The two answers used to disagree,
which is the deeper reason: a cast found a shape and could not say whose it was, so where the two were not
talking about the same body the reading came back `Unknown`, which is never driven round, and a claim with
nothing standing on it yet came back as empty road. Odesa's cars went 375 → 171 µs of the ranked tick.

## 2026-08-22 — a driver looks as far as it needs to stop, and not as far as the pedal could ask for

The reach was the stopping distance at the brake pedal's cap, 27 m/s², against the 17.79 the profile plans
with — 112 m of looking for a 158 m stop, so a car at top speed could not stop for anything only a ray
would find. It never showed while every stop was a painted bar, which is furniture a driver knows the place
of from any distance; the proving ground's pacers found it. The reach is a reaction interval and a stop at
the rate the profile brakes at, which is the figure `ManeuverDesk.SightM` already grows the line to.

## 2026-08-25 — a car is the car it is drawn as, and the line is a recommendation

Every car was the nominal car with somebody else's picture on it, the variants' figures unread. `CarBuild`
resolves a variant once against `SimConfig`. The town's geometry stayed the nominal car's (CAR-11a),
because re-sizing a town per car is nineteen towns — from which follows the doctrine this is really about:
what the town precomputes is a recommendation (CAR-10), not a rail. It found a real defect in the junction
protocol: ranks are compared off claims laid at the top of a tick, and with cars that brake at their own
rates a car could cross into "cannot stop" inside the tick it was traded against, so a stronger movement
was waved across a body already committed. Commitment is judged a decision ahead now. The proving ground
keeps the nominal car so a difference between its rows is about drive layout and nothing else.

## 2026-08-22 — a car drives through the end its own variant drives through

The tyre model has always been documented as spending the variant's drivetrain and the fleet has always
shipped one per variant; only the nominal figure ever reached it, so every car in every town was
front-wheel drive whatever it was drawn as. What it bought was a comparison: a front-drive car tops out a
sixth under the other two down the straight and takes a quarter longer to get back up to speed.

## 2026-08-20 — which procedure runs is the line's question, not the name's

A reactive entry that lays no line leaves the car driving what it was already on, so the standing rules
pick the route or the template procedure by looking at the line. A dispatch keyed on the entry's name would
hand a car in `E-2` — which lays nothing — to the route procedure with no lanes under its line. The entry
decides *what*, the line decides *how it is driven*.

## 2026-08-19 — a car at a standstill has no tyres to work out

A standstill has no slip to resolve and no mark to leave, so the whole four-wheel solve is skipped below a
threshold — the largest single saving in the car's tick, and free because the result it skips is
arithmetically zero.
