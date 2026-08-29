# The car agent — decision log

**The manoeuvre catalogue keeps its own log**, in
[maneuvers/docs/decision-log.md](../maneuvers/docs/decision-log.md): what an entry is, how the plan is
chained, why an entry is entered on one thing and left on another. What is here is the body and the
tyres.

## 2026-08-27 — an indicator is the side of the lamp the artist drew, where there is one

Giving the fleet front lenses meant painting an amber block onto most of the cars, because most of them
had no lamp drawn at the nose. On the three that did — the cyan super, the pink sports car, the tan
pickup — the block went on beside the headlight rather than onto it, and an amber rectangle sitting on
the paint next to a lamp reads as a sticker whether it is lit or dark.

**So where the art draws a lamp, the lens is a section of it** (CAR-14b), and the section is the end
nearest the flank: that is the part of a real cluster the indicator is, and taking the whole of a long
diagonal headlight would flash the main beam amber. The three blocks are painted out and the paint under
them runs on, so what is on screen when the indicator is dark is the headlight the artist drew.

The lens is white glass rather than amber on two of them, since that is what the art paints there — a
clear-lens indicator, which is what the cut turns amber when it burns. The rest of the fleet is left
alone: those whose art wraps an amber corner into the cluster were already a section of a lamp, and the
plain-nosed ones keep the block, because there is nothing there to be a section of.

## 2026-08-27 — a lit lens shades with how solidly it is drawn, never with darkness

The blue muscle car's brake lamp had a black bar down the middle of its glow. Its lens is four texels
wide: three of deep red glass and one of a pale chrome highlight. The highlight set the top of the cut's
range of light and the glass sat near the bottom of it, so the ramp painted the glass at a fifth of the
lamp's colour — a near-black — while the separate ramp that decided how much of a texel to draw had
already reached four-fifths opaque a quarter of the way up. The lens is drawn over its own glow, so what
landed on screen was an opaque black strip punched through a red light. Four thousand texels of the
sheet were darker than the light they sit on; every deep-painted lens in the fleet carried some of it.

**So the two ramps became one.** A texel is drawn as solidly as it is burning, and full opacity arrives
where the lamp's own colour does — nothing is solid until it is that colour. Below there a texel is both
dimmer and thinner, so a bezel reads as a bezel by letting the glow through rather than by covering it
with a darkness of its own, which is the same argument that stopped an unlit lens being drawn at all: the
dark part of a lamp is already on screen in the car's own picture. The ramp's bottom is a dim fraction of
the lamp's colour instead of black, so nothing in the sheet can be darker than the light around it — a
unit test now holds the sheet to that, since it is a property of the shipped picture and not of a run.

The cost is that a lens no longer contributes its own contrast to a lit lamp; what shapes one is the
glow, the filament and the art's alpha. On the deep-glass cars that is the whole of the fix, and on the
brightly painted ones the picture is barely changed.

## 2026-08-26 — a second bar is a crossed pair of fittings and not a phase written down

The ambulance's new art draws a lamp bar across its back doors as well as its roof, and a bar that stays
dark is a picture of a lamp. Lighting it as a second beacon needed a way to say *out of phase with the
other one*, and the obvious one — a phase number on the lens, or a fitting of its own — is a second place
for the swap to be described from.

**What a beacon end carries at rest is already the whole of its phase**, so the rear bar's ends are
entered crossed: blue where the roof bar is red, red where it is blue. The arithmetic that swaps a bar
every half period is untouched and cannot disagree with itself, and at every instant the car carries both
colours at both ends of it. CAR-14.4 states the relation; the file states which end is which.

**The rear bar is four lenses and the brake pair moved off it.** The art draws two lamp blocks either
side of a middle panel that is not a lamp at all, and both blocks of a side belong to the bar — a bar
lighting one block of two reads as a lamp that has failed. So the rear cluster is back on the housings at
the corners of the tail, which is what they are drawn as.

**The lens cap went from six to ten**, which is what a rear pair, a front pair, a roof bar and a rear bar
of four come to. It is the buffer a frame is laid for a car and the width of the lamp sheet, so the sheet
is 800 px across where it was 480 — the cost of the columns an indicator does not use, paid again.

## 2026-08-26 — the service cars were redrawn, so everything measured off their pictures was measured again

The ambulance, the patrol car and the evacuator carry new art, drawn for the lamps: a bar of two lensed
sections either side of a dark centre on the first two, and a column of five deck lamps on the third.
Every number in those three files that had been read off the old pictures is read off the new ones —
**the file is a description of the picture and cannot be left behind by it**: the lens rectangles, the
wreck's scale against its own body, and the two figures below.

**The track widened because the panels did** (CAR-12). The new bodies are drawn sill to sill — the stripe
along the rocker *is* the edge of the car, where the old art ended a few centimetres inside it — so the
median panel at each axle is 1.83 m on the ambulance and 1.89 m on the patrol car. A tyre has to show
past that from above, and the track is the picture's figure rather than the handling model's, so it is
1.80 m and 1.85 m rather than the art being narrowed to protect the old numbers.

**The evacuator's collision corner rounded off** for the same reason: the new cab and deck are cut back
at the corners, and a shape that stood off the bodywork at a sixth of its edge is a shape that has
stopped following the art. The box is the same length and 2 cm narrower, at a 0.60 m corner.

The evacuator's art carries a works bar across the cab: two amber lenses with a white work light between
them. The two ends could have been read as a beacon bar's ends and driven the way an ambulance's are, and
the result is a yellow lamp turning yellow twice a second — a swap nobody can see is a bar that looks
broken rather than one that looks urgent.

**So the fitting is its own**, and what flashes on it is which end is burning rather than which colour it
is. A pair of them is dark at one end and lit at the other, half a period apart, so the rule CAR-14.4
states of every beacon still holds: the car everybody owes the road to is lit in every frame it is in.
The white middle section is not a beacon end and stays what the artist drew, because it is a work light
and a work light is not a claim on the road.

**A single amber lens would simply blink**, which is what the arithmetic already says and needs no rule of
its own — the alternation is a fact about a pair, not about the fitting.

**And what it burns for is the work and not the priority** (CAR-14.6). It was first lit off `BlueLight`,
which is the one leg of a recovery the town gives the road to — so the truck standing over a wreck in a
live lane and the truck hauling that wreck home both went dark at exactly the moment a real one turns its
beacon on. An amber bar claims nothing; it says there is work in this street. So the two facts are two
arrays: `BlueLight` orders traffic, `AtWork` orders nobody, and the amber lens reads either.

**Its light is the indicator's amber**, one entry in the one colour table. Two ambers a few hundredths
apart would be two lamps that photograph as one, and the table is what keeps a lens and the glow around it
agreeing about what colour they are.

## 2026-08-26 — a lit lamp is cut from the car it is on

A lens drawn once for the whole town is a lens drawn for nobody's car. The strip that preceded this held
four colours in eight frames — good art, the right idea, and the wrong resolution for every car it landed
on. Its cells are 64 texels square and were stretched over lenses between 0.1 m and 0.38 m, which is
between 166 and 640 texels a metre; every car sprite in the fleet is drawn at 96. Through a linear
sampler the difference is not subtle: a soft rounded lozenge with its own dark ring, sitting on hard pixel
art, overhanging the outline the artist had drawn around the panel. The lamps read as stickers.

**So the sheet is cut from the cars.** For every lens a variant draws, `--lamps` takes that rectangle of
that variant's own sprite and drives it emissive — each texel's place in the cut's own range of light
carried up a ramp through the lamp's colour to a near-white filament. Nothing is drawn: the
bezel, the shape and the grid are the artist's, and the only thing applied is light. A row a variant and
two columns a lens, so the renderer works out the cell from numbers it already has and nothing is written
down beside the picture to disagree with it. The cost is a workshop step to remember — art or a rectangle
changes, the sheet is re-cut — and it is the same cost the glyph sheet already carries.

**And the unlit lens stops being drawn at all**, which the cut makes obvious: it is the section of the
car's picture the lamp was cut *from*, so it is already on screen, and drawing a second copy over the
first was what put two disagreeing pixel grids on one lamp. That the difference between on and off has to
be legible is still true — an ambulance at its hospital must not look like one on a call — but what
carries it is the art the sheet was cut from, not a second picture laid on top.

**Only the lit states are in the sheet**, so a lens holds at most two cells: red and white for a rear
cluster, its own colour and its partner's for a beacon end, one amber for an indicator. The floor that
used to widen a lens too small to see moved onto the glow, where it belongs — a lamp of four texels is
drawn at four texels and carried by the light around it, because widening the lens moves it off the
panel it was measured onto.

**Twelve variants gained a front lens, and eight had their rectangles re-measured.** Most of the fleet
had never had an indicator painted on it, so the old rectangles lit a corner of bodywork. A pair of them
is now a mirrored pair by construction — the suite already refused a crooked one, which is how that was
caught.

## 2026-08-25 — a lamp is a section of the picture

The first build put a round glow at a place arithmetic worked out — a share of the body in from the nose,
a share in from the flank — which is a light that lands wherever the sum lands. On a hatchback it sat on
the tailgate, on the pickup it sat on the bed, and on nobody's car did it sit on the lens the artist had
drawn there. The lamps read as glows stuck to cars rather than as parts of them.

**So a lens is authored art**, measured off the variant's own picture and written in its file beside the
hull and the track (CAR-14a). The cost is real: twenty files carry six numbers each, and a new variant is
not finished until its lenses are measured. What it buys is that the lamp *is* the car — the section that
lights is the section drawn to light — and that the beacon bar a police car and an ambulance already had
painted on stops being a lie.

**The rear cluster shows one thing at a time and the pedal outranks the gear** (CAR-14.3). The art gives
a car one lamp at each rear corner, not the three a real cluster has, and inventing a second lens inboard
of it would put a reversing lamp on bodywork nobody drew one on — the very thing this change is undoing.

## 2026-08-25 — an indicator is read off the line, and a lamp is not state

The obvious build was a signal the manoeuvre sets: `P-8` knows which way it is turning, `P-2` knows which
way it is pulling out. That is eighteen entries each having to remember to announce itself, a flag to
clear on every exit, and a car that indicates left for the rest of its trip the first time an entry
forgets. The line is the same intent already written down as geometry — so the side is the side the line
bends to over the stretch ahead, and it costs a walk of the arcs already in hand.

What it buys past the plumbing is that the lamps cannot disagree with the car. Every one of them is a
read of something else — the pedal, the gear, the line, `BlueLight` — so there is nothing to step, nothing
to serialise, and a frame drawn twice at the same tick draws the same lamps. The only clock in it is the
town's own, which is what makes the flash a function of the frame rather than a counter.

**The beacon shows the priority and nothing else** (CAR-14.4). Lighting a patrol's bar because a police
car looks better with it on would put a light on a car that SRV-5 grants nothing to — the town would be
showing a claim on the road that the road does not honour. Police cars therefore drive dark until
something calls one out, and the day something does the beacon needs no work.

## 2026-08-25 — recklessness is the person's, and it drops two courtesies rather than the rule set

The habit had to sit somewhere, and a flag on the car was the cheaper read: one array lookup instead of
two, on a path taken by every car on every tick. It is on the person anyway, because a car changes hands.
Mirrored onto the car it would have to be written on boarding and cleared on alighting, on a wreck and on
an abandonment — four sites that can disagree about one fact, bought with one array read saved on a path
that is already doing spline work.

**What it drops is two courtesies and not the rule set**, and the list is closed in the requirement rather
than left to the reader. The tempting version was a driver that ignores the road's book — it makes crashes
immediately, which is what a rescue with nothing to fetch appears to want. But `RightOfWay` is the whole
of why nobody is ever driven into on purpose, and a driver exempt from it is not a bad driver, it is a
second physics. So a reckless driver runs the red and does not wait for somebody still on the kerb, and
then meets the same profile, the same hazard check and the same bodies as everybody else. Whether that
produces a casualty is the geometry's answer and not the habit's, which is the honest shape of it.

**The red they cross is counted.** AMB-4.2 exempts an ambulance from the rule, so a rescue crossing a red
is not in breach and is not counted; this is the opposite case and reads the opposite way, which is why
the signal probe now prints how many reckless drivers the town has beside how many bars were crossed.

Adding the column is what showed the probe's closing line to have been wrong all along. It read "every
column but the counts must be zero", and red bars crossed was one of the columns it meant — but Odesa
crosses eleven of them in a minute with **nobody** reckless on it, and did before any of this was written.
Two sources, not one: a shunt puts a car over a line, and a car committed when the phase turns is past it.
The line now says which columns it means, and the count is read against the reckless one. **What that
baseline is made of has not been looked into and is not this change.**

## 2026-08-25 — the wheel travels, the throttle is bounded by the corner, and the fleet gets a lap of its own

**A car was hard to drive because a key press was not a control, it was a selection.** `A` put the steering
on its stop in the tick it was pressed and `W` put the throttle on the floor in the same one — so a hand at
the wheel was asking a body at speed for a circle of 3.9 m, which the front tyres spend the whole corner
saturated against. The follower had the same gap in the other direction: pure pursuit produced whatever
angle the lead point implied, and the angle arrived whole. The pedal already travelled
(`Driving.PedalTravelS`); the wheel now does too (`WheelTravelS`, CAR-3a), and both bind the hand and the
follower alike, because a rack is a fact about the car.

**Lock to lock is 1 s — half a second from the straight to full lock, and the same again to come
back — and what it costs was measured rather than guessed.** The figure is what a hand at the keys
wants: a wheel wound in a third of a second is still a lock being selected as far as the thumb is
concerned. On the proving ground it is free — the fleet lap is identical to three decimal places on
every column, and the track lap moves in the third one — because a car running a line at speed asks
for angles the rack reaches long before the corner does. What it costs is in the town, where the
angles are large and the speeds are small: Odesa's ladder takes the same 79 rungs but shunts twice
as often to get into a bay (14 back-offs at 0.6 s, 27 at 1 s), and no car is left standing. That is
the ladder doing its job rather than a controller failing, so the rack keeps the figure the hand
wants; leading the pursuit demand by the wheel's own travel, the way the speed profile already leads
by the pedal's, is what would buy the shunts back and it is not done here.

**And a car under power now lifts for the corner it is in** (CAR-3b). The driven axle's ceiling used to be
the whole longitudinal budget whatever the wheel was doing, so a rear-driven car with the throttle pinned
spent it all pushing and ran wide; it is now the ellipse's remainder, read off the lateral acceleration the
solver measured — lagged and capped exactly as the loads are. **A hand gets the remainder and nothing
else**: the traction-control lift underneath it is a self-driver keeping its own tyres out of trouble, and
flooring it stays the player's to do.

**What the rear-drive complaint turned out to be.** The tyre model was not the fault: a car at a fixed lock
holds its radius, its body slip stays a couple of degrees, and the figures converge from 30 Hz to 240 Hz.
A rear-driven car with the throttle pinned at full lock runs from a 4 m circle out to a 37 m one because it
*accelerates* — at 20 m/s the tightest circle 11.4 m/s² of grip can hold is 35 m, and it is the front axle
that has run out. A front-driven car cannot do it because its driven axle is its steered one and the drive
takes the grip the corner needs, which is the whole of the difference between the two layouts. Both are
right. What was missing was a driver, and for a hand that is the player — so what this entry buys is a
throttle and a wheel that can be *held part way*, which digital keys could not do before.

**The ceiling stayed on the nominal patch, which is the one figure in the tyre path that is not the car's
own.** Moving it to `CarBuild.LongGripMps2` is correct by CAR-11 and was tried: it lets the grippier looks
use more of their pedal, they arrive at crossings faster, and one car on River crosses the paint at 12.1
m/s against a 11.7 m/s bar. **The crossing is only found on the lane the car is standing over**
(`CrossingOnTheTemplate` reads `CrossingsOn(NearestLane)`), so a faster car gets a shorter approach to it —
that is what has to be fixed before the ceiling can be the variant's, and it is a `world/road` change
rather than this one.

**The fleet lap is the third proving ground.** `Track` deliberately stands six of the nominal car so that a
difference between its rows is a difference about drive layout (CAR-11a); nothing anywhere measured the
cars a town actually hands out. `Fleet` is the same lap with one car of every look on it — 1050 kg to 4200,
an acceleration factor of 0.32 to 1.75, tops from 45 to 109 m/s. All sixteen drive it, none is wrecked,
none gives up, and the worst any of them runs off its line is 0.79 m against the 3.0 m the town calls
losing it.

**Nobody is on foot on it, and that is the whole of what makes the figures the car's.** The other two laps
carry fifteen people because what they measure is a driver stopping for what is in front of it; a body in
the road here would only be a second thing setting a car's speed. It is not a small difference to the
table: with the pacers on, the worst off-line read 1.70 m — and that was cars queueing behind a stopped
one and stopping hard for a body in the lane, not any of the five shapes. Off the same lap without them
the worst is 0.79 m, inside a lane's own half-width, and every look gets half again as many laps and
passes.

**It is watched from the standing start, and that is forced.** Sixteen cars on one single-lane circuit
whose tops differ by 2.4× end up one queue behind the armoured car, and no car may cross the centreline to
get past a moving one (CAR-6.2b) — so after that every figure on the lap is the armoured car's. At `t=0`
they stand 177 m apart with the road in front of each of them, and that start is also the only pull away
from rest the lap has. For the same reason the pull-away figure is **the best a car ever managed and not a
mean**, read off its own speed trace from the slowest it has been rather than off a leg of the lap: traffic
can only ever have made it smaller, so the best of them is a floor under what the car is worth. It runs
from 2.18 m/s² for the armoured car to 12.07 for the red sports car, in the order the files say it should.

## 2026-08-25 — a variant's wheels are read off its own picture, and its weight off what it is

The fleet's axles and tracks had been authored against nothing in particular. Every variant's `trackM`
came out at about half its own width, which is what a *half* track would be for wheels at the corners of
the footprint — so read as the whole track the code called for, it stood every car on wheels tucked into
the middle of it, and read as half of one it stood them outside the bodywork. Several axle figures were
the mirrors rather than the arches: `sedan_rust` carried a front axle at 1.42 m, which is the wing mirror
at 1.66 m rounded in, on a car whose front arch is at 1.10 m.

The figures are now taken from the art. A sprite is 96 px to the metre and its silhouette bulges where the
arches are, so the arch centres are measurable, and eleven of the nineteen gave both axles that way; the
flat-sided ones — the ambulance, the van bodies, the box of a truck — were placed by their own overhangs
against the cab and the bed the picture draws. **`trackM` is now the whole track**, as
`SimConfig.CarTrackM` is, and `CarVariant` halves it: what the file states is the figure a person measures
between two wheel centres.

**Then the wheels disappeared, which is how the track got its rule.** Authored at what a real car's track
is — a hand's width inside the panels — every tyre was under the bodywork drawn over it, and the art is a
car seen from above whose panels reach the edge of its own sheet: four impulses were acting on a body that
showed nothing of them. Two things were wrong at once. Every car was being *drawn* at the nominal 4.0 ×
2.0 m however big it actually was, which stretched a 3.4 m hatchback over four metres and spread its
panels out over its tyres — that is CAR-12a, and the quad is now the build's own box, the same one the
solver is handed. And the track itself is now the width of the bodywork over the axles (CAR-12), measured
off each picture's alpha with the mirrors ignored, so a wheel centre stands on its own flank as the
nominal car's stand on the corners of its footprint and about half of every tyre is outside the panels.
**It is geometry and not a drawing trick**: that is where the patch takes the ground, so the wider base is
what the car now rolls and corners on. What it collides with is still the bodywork — the rubber outside
the box is not a second hull, which is the one thing about this a reader will want to check.

**A weight came with them.** `massKg` is authored per variant — 1050 kg for the compact, 4200 for the
armoured car, 3200 for the evacuator — and the entry below spends it.

**Showing the tyres showed what else was out there.** Fifteen of the nineteen sheets were carrying small
light fragments painted outside the body outline — three white blocks over the silver sedan's roofline, a
row of grey dashes under the coupe's sill, a slab under the sports car's tail, a block under the supercar's
sill — and once the bodies were drawn at their own size these read as pale flecks beside the car. Thirty-
eight of them were erased and the anti-aliased halo taken with each; the lights, mirrors, bumper ends and
the tow eye that the same search turns up were left alone, because those are pictures of something. The
wreck sheets keep their shed panels for the same reason, but the ambulance's carried thirty-one loose
pixels down its nose edge, which are a crop nobody cleaned up rather than debris, and they went too.

The drivetrains were already right for what each variant is drawn as and none moved. What would have
caught any of this is now three tests: the wheel *centres* have to fit inside the variant's own footprint;
every tyre has to show past the panels by `Tyre.ShowsPastTheBodyworkShare` of its width, measured against
the art rather than against another number in the same file; and no sheet may carry an opaque island too
small to be a picture of anything.

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

## 2026-08-25 — a car is the car it is drawn as, and the line is a recommendation

Every car in every town was the nominal car with somebody else's picture on it: one footprint, one weight,
one wheelbase, one set of brakes. The variants' own figures sat in their files unread. What is here now is
`CarBuild` — a variant resolved once against `SimConfig` into the body a driver actually drives — and
everything a car decides for itself is taken against it: the wheel (its wheelbase and its lock), the pedals
(its gearing, its brakes, its rubber), the road it asks for, the gap it keeps, the box it commits to, the
paint it creeps over, the shape it draws to get into a bay, and the box the solver collides it at.

**The town's geometry stayed the nominal car's** (CAR-11a), and that is the whole of the line drawn under
this. Lane widths, junction radii, bays and the ways laid into and out of them were sized against one body,
and re-sizing a town per car is not a fleet, it is nineteen towns. What follows from keeping them is the
doctrine the change is really about: **what the town precomputes is a recommendation** (CAR-10). A route is
a chain of lanes so that a driver does not search the network every metre; a bay's way is a shape with a
reservation and a right of way already attached to it. Neither is a rail. A car holds its line with its own
steering at its own speed, cuts a corner a shorter car takes cleanly, and where the town's own shape does
not suit the body that turned up — a van whose axle sits further back than the nominal car's, a truck whose
circle is wider — it lays the same shape again from the pose it is actually standing in (CAR-10b). Nothing
is shuffled onto a line to make a drawing fit.

**It found a real defect in the junction protocol.** A stronger movement takes ground from a weaker one
while the weaker one can still stop short of the box (TER-5e), and the ranks are compared off a book laid at
the top of a tick from what the last decision wrote. With one car repeated, the two readings never
disagreed; with cars that brake at their own rates, a car could cross into "cannot stop" inside the tick it
was traded against — a stronger movement waved across a body already committed. Commitment is now judged a
decision ahead, which is the same lead every distance in the speed profile is measured at, and the wave is
gone. Two minutes of every shipped town is what it took to see it at all: one minute of Odesa, which is what
the claim used to be watched over, never reached the case.

**The proving ground keeps the nominal car** (CAR-11a). Its six cars differ in drive layout and in nothing
else, so a difference between the rear row and the front row of `--bench track` is a difference about drive
layout — a front-drive car tops out a sixth under the other two down the straight and takes a quarter longer
to get back up to speed out of a corner. A lap that varied weight, length and grip as well would be three
anecdotes. `--bench crash` and the solver probe stand it for the same reason.

## 2026-08-22 — a car drives through the end its own variant drives through

The tyre model has always been documented as spending the *variant's* drivetrain, and the fleet has always
shipped one per variant; the only thing that ever reached it was the nominal figure on `SimConfig`, so
every car in every town was front-wheel drive whatever it was drawn as.

What it bought was a comparison. Six cars on the proving ground are the nominal car in every respect but
this, two of each layout, so a difference between the rear row and the front row of `--bench track` is a
difference about drive layout and about nothing else — and the first thing it said was that a front-drive
car tops out a sixth under the other two down the straight and takes a quarter longer to get back up to
speed out of a corner.

## 2026-08-20 — which procedure runs is the line's question, not the name's

A reactive entry that lays no line of its own leaves the car driving whatever it was already on, so the
standing rules pick the route procedure or the template procedure by **looking at the line**. A dispatch
keyed on the entry's name would hand a car in `E-2` — which lays nothing — to the route procedure with no
lanes under its line.

It survived the catalogue being rebuilt as a file per entry, and it is the reason the two halves of the
driver are split the way they are: the entry decides *what*, the line decides *how it is driven*.

## 2026-08-19 — a car at a standstill has no tyres to work out

The tyre model ran its full four-wheel solve for every car every tick, including the great majority
standing still in bays or queues. A standstill has no slip to resolve and no mark to leave, so the whole
solve is skipped for a body below the threshold — the largest single saving in the car's tick, and free
because the result it skips is arithmetically zero.
