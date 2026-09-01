# The car agent — decision log

**The manoeuvre catalogue keeps its own log**, in
[maneuvers/docs/decision-log.md](../maneuvers/docs/decision-log.md): what an entry is, how the plan is
chained, why an entry is entered on one thing and left on another. What is here is the body and the
tyres.

## 2026-09-01 — paint is not a speed limit, and `CAR-7b` is retired

**A car slowed at every zebra in the town whether or not the crossing was anybody's.** The pace was a
second term of the speed profile beside the stop short of the paint, owed on every line that crossed a
crossing, and it bound whenever paint was within reach — so the status panel read `P-4 yielding at a
crossing` at 37 km/h on an empty crossing with nothing on the book against it. There was nothing to yield
to: what had bound the car was the pace and not the ground.

**A crossing is ground and the book already says whose it is.** A walker on the paint lays the band of the
lane it stands in and a walker refused one lays an `Awaited` stretch (`TER-4c.1`, `TER-5e`), both of which
cut a driver's grant like anything else on the lane; the stop short of the paint is read off that book.
The pace was a second gate on a movement the book had already answered (`SIM-7`) — and, being owed whether
or not anybody was there, it was the one term of the profile that no reading of the world could switch off.
So it is gone: `CAR-7b` is retired, the number stays retired, and `Driving.CrossingPaceInCarLengthsPerS`
went with it.

**What it cost, measured on the fixture over 1800 ticks.** Car-ticks bound by a crossing fell from 52% to
26% and the mean speed rose from 5.37 to 5.85 m/s — the traffic is quicker and the crossings bind it only
when somebody is actually there. **The stop that is left is arrived at from road speed**, which the same
run says in the manoeuvre trace: `E-2` was never entered with the pace and is entered three times without
it, and `P-6` twice as often. On Odesa that shows up as two broken claims — a staged casualty reached in
332 s against a leg bound of 300, and a run of the city that raised no closure at all inside its window.
**Both are the courtesy stop at the kerb arriving at speed** rather than the yield failing: a car that used
to meet a waiting walker at 8 m/s now meets one at road speed and stops as hard as it can for them.

## 2026-08-31 — the indicator was reading a bend, and a bend is not a turn

**A car indicated its way round every corner of every circuit.** CAR-14.1 read the indicator off the line's
own bend over the next twenty-five metres, which on the idle ring — twenty-four metres of radius, held for
ever — is fifty-eight degrees on every metre of it. Under the old rule that was correct behaviour, which is
what made it worth changing rather than tuning: a road of constant radius offers nowhere else to go, so a
car announcing a turn on one is announcing something nobody can act on.

**What was tried first was subtracting the bend behind from the bend ahead.** It kills the circuit, and it
also kills the second half of every junction turn — a car deep in a left-hander stops indicating, which is
exactly when the traffic waiting to come out of the side road is reading it.

**So the indicator is now gated on the junction rather than measured against it.** It is shown only within
reach of a junction whose movement the road classifies as a turn (`LaneTurn`, already computed for right of
way in `TownWorld.JunctionStopM`) — so what a car announces and what it gives way to are one answer. Which
*side* is still the line's own bend, so no manoeuvre has to declare itself.

**What this loses is the bay exit and the way round an obstruction**, both of which used to indicate off
their template's bend and now have no junction in front of them to answer. That is the cost of the rule
being about junctions, and it is named here rather than left to be discovered.

## 2026-08-30 — the engine was authored in m/s², and no car in the fleet could reach a third of its own pedal

**Read one look at a time, the pad said the same thing sixteen times.** Only three squares of the lightest
row sat on their geometry, and they were `truck_brown`, `apc_olive` and `van_purple` — exactly the three
looks whose authored acceleration was at or under what their driven tyres could put down. Every look that
ran wide down there was a look whose engine was authored two to three times past its rubber. That is not a
correlation to tune against; it is the mechanism.

**`CarFigures.AccelerationMps2` was 11.7 m/s², and a variant multiplied it.** So `sports_pink` asked for
18.0 m/s² and `super_cyan` for 20.5 — 1.8 and 2.1 g — while the most either could put down from rest was
half its grip, near 6 and 11. **The whole of the excess was inert**: above the traction limit the pedal buys
no acceleration at all (CAR-3b), so those figures described nothing that ever happened to the car, and every
metre per second it gained was the compound's. It was authored the one way the file beside it says not to —
`BrakePedalInTyreGrips` is a headroom over the rubber precisely so that it *tracks* when the grip moves, and
the throttle two properties above it was a bare figure in m/s² that did not.

**The pedal is now a multiple of what the driven axle holds** (CAR-45), off the same division the model
makes: drive placed by layout, over the load of the axle it stands on, at the static load. A variant states
how far past its own rubber its engine reaches and the acceleration is what that comes to — under one,
nothing it does spins a wheel; over one, it lights the driven axle up short of the floor. The fleet is
authored in four bands: 1.5 for the six that are meant to break traction, 1.2 for the ordinary cars and the
van, 1.0 for the pickups and the recovery truck, 0.8 for `truck_brown` and `apc_olive`, which are the two
vehicles with less engine than tyre.

**It cost the fleet nothing, which is the point.** Fifteen of nineteen looks pull away at exactly the figure
they did before, because they were already traction-limited and the change only stopped them claiming
otherwise; `apc_olive` gained 5% and `truck_brown` lost 3%. What moved is that the number now says what
happens.

**And the lightest row became an instrument.** Every look on the pad now gets round on every pedal it stands
— the first run that has ever said so — and the `ahead, 33%` row went from 2.16x the axles to 1.30x, with
twelve of sixteen looks between 1.00x and 1.25x. `taxi_yellow` astern went 0.43x to 1.00x, `hatch_teal` 0.62x
to 1.00x, `sedan_rust` 0.71x to 1.00x. The crawl now has 92 of 96 cars in it rather than 84, and the arcs
turned inside a car's own axles fell from 17% to 11%.

**`super_cyan` is the one look still wide at a third**, at 1.73x astern and 2.75x ahead against a fleet that
is otherwise inside 1.5x. It is the only four-wheel-drive car in the 1.5 band, so its rubber holds twice what
a two-wheel-drive car's does and its engine is half again past *that* — a third of its pedal is 5.3 m/s²
where `muscle_orange`'s is 2.7. That is a fact about the car and the pad reporting it is the pad working.

**The two frictions that had been solved backwards are gone.** `sports_red` at 1.31 and `sports_pink` at 1.24
were coefficients computed out of a wanted acceleration when CAR-45 was still a threshold on a figure in
m/s², which is the one thing this project's figures may never be. They are 1.12 and 1.10, inside the band a
road tyre lives in, and the two cars are 15% and 11% slower off the line for it. Recovering that belongs to
`frontWeightShare`, which every variant may state and none does.

**The gate went with the rule.** CAR-45 was checked by asserting a third of each pedal against a traction
limit the test worked out itself; the pedal is now derived from that limit, so the old gate refuses what the
construction already refuses (SIM-7). What is checked instead is that the build's figure *is* that multiple
of the arithmetic stated where the rule is, and that no variant's reach leaves the 0.5–1.5 band.

**What the heavier rows separate on is still not the throttle.** Once a car is at its limit the pedal above
that limit buys nothing and every heavier row converges on the lighter one's circle (CAR-3b) — `muscle_blue`
reads 1.53x at 100% and at 66% astern, `super_cyan` 3.42x at both ahead. **What separates two rows is whether
a body has enough throttle to reach the limit at all**, and now that a pedal is a multiple of the rubber
rather than a number three times past it, the lightest row is the one that no longer reaches it. That is why
it became the instrument SkidpadPlan always said it was, and why the row spread above it still is not one.

**The balance is the variant's too, and the drive follows it.** Every look stood 50/50 and none of them
said so — `frontWeightShare` was authorable and unused, so a coupé balanced like a van. Each now states its
own, and **a car that drives all four wheels splits its drive by load rather than evenly**: an even split
made an off-balance car *worse* on all four than on two, because the light axle broke away at a fraction of
the grip the other three still had. What it bought was the fleet's reverse behaviour — the six front-drive
looks had been pivoting inside their own axles at full lock astern, which four rolling wheels cannot do, and
the arcs measured inside a car's own geometry fell from 17% of the pad to 4%.

**And the cap was 270 km/h, which was not free.** A top speed is a derived observable that this build cannot
derive — resistance here is the rolling kind, flat in the speed, so thrust beats drag at every speed and no
terminal velocity exists. What it *is* is a governor, and it is authored as one at 144 km/h, because a car's
sight distance is its stopping distance from that figure: `super_cyan` had been planning 786 m ahead in a
town of 150 m blocks.

**What all of it exposed is that the town's spacing was buying margin from the fiction.** The ground a car
reserves is the ground it could reach in a reaction time, projected against its pedal — so pedals two and a
half times what any tyre could take had been quietly paying for the fleet's following distance. With honest
ones the reservation is shorter and cars run into each other: **four of sixteen wreck on the fleet lap where
none did**, the exam's card 34 stopped completing its turn, and the allocation gate's warm-up doubled twice
because a map now reaches its worst pile-up later. Setting the reservation against the planned speed instead
removes the dependency and is wrong: a stopped car would hold road it could not have driven over, which
`LaneOccupancyInATownTests` refuses and should. **The margin has to come from the driving model rather than
from an engine figure, and that is not a change to a car's figures** — it is left standing, failing, and
named here rather than papered over by putting the fiction back.

## 2026-08-30 — a hand's ellipse is the hand's own car's, and the pad stopped hunting

**A car under a hand was refused by two ellipses that disagreed.** `DriveCeilingMps2` measured the
remainder against the nominal patch while `TyreModel.Step` spent `CarBuild.GripMps2`, so a look grippier
than nominal had its throttle shut off at a lateral its own rubber was still holding. That is the second
gate SIM-7 forbids, and a disagreeing one.

**What it did was oscillate, and it was visible.** `super_cyan` — 10.75 m/s² of grip against a 9.50 nominal
— hunted between 6.0 and 7.8 m/s and between 1.14x and 1.58x of its own geometry for the whole of a 40 s
run on the astern, 33% square, writing a set of nested arcs that never closed. **The full-pedal square
beside it was rock steady**, because a demand far above the ceiling is pinned to it and has feedback the
whole way; a demand that *crosses* the ceiling has none below it and all of it above, and the lateral it
switches on is lagged. That is a relaxation oscillator, and the wider the two grips disagreed the wider it
swung.

**The hand's half moved to the car's own patch and the self-driver's did not.** A hand reads no crossings,
so nothing about it waits on the `world/road` fix the entry below records. Every square of the pad now
settles — the worst swing left in a measured row is 0.16x against `super_cyan`'s 0.44x — and the five looks
that had been turning *inside* their own axles at a crawl came back to it: `van_purple` 0.81x to 0.96x,
`sedan_rust` 0.83x, `taxi_yellow` 0.85x and `compact_green` 0.86x all to 0.96x.

**And the pad reads again.** A car under power runs wide, which is what CAR-3b says it is for: the fleet
means went from 1.49x to 1.86x the axles, and the looks whose own rubber is *under* nominal came in rather
than out — `sedan_rust` 1.97x to 1.43x — because their ceiling had been generous to them by exactly the
amount the nominal patch was wrong.

## 2026-08-29 — a panel may move the road, and a car is not the road

**Seven of the nine dials spoke for nineteen bodies at once.** The figures page carried a steering lock, a
centre of gravity, a front weight share, a mass, an engine pull, a brake pressure and a gear cap — and the
fleet authors every one of them per car: `maxSteeringDeg` 19/19, `cgHeightM` 19/19, `massKg` 19/19, and a
`handling` block on all nineteen scaling the last three. **A dial over a figure each body already states is
one number pretending to be nineteen.** The panel's own doc-comment argued for exactly that and was wrong:
that a variant states its own is the reason not to have the dial, not the reason to have it.

**What is left is two, and both are the road.** The coefficient between rubber and tarmac, and what each
ground costs a wheel simply going round. Both are properties of a *surface* — true of every body standing
on it, and the same figure whichever car is asked. That is what a fleet-wide dial is for, and nothing else
on the page was.

**`StaticFrontShare` moved from `TyreFigures` to `CarFigures` on the way.** Where a body's mass sits along
its wheelbase is a fact about the body and never about the rubber, and it had been filed under the tyre
since it was written. It is a variant's to state (`frontWeightShare`, still 0/19), and the nominal is now
what the car nobody drives stands at rather than a constant the town shares.

**What it costs is the handling rig, and that was already the wrong tool.** Winding the whole fleet's mass
to twice shipped answered a question nobody has — every look moved together, so the one thing a comparison
needs held still did not hold. What replaces it is a figure in a file, which is where a body's own figures
were always going to have to be authored.

## 2026-08-29 — a tyre is bolted to a car, not to a town

**The fleet ran one wheel.** `Tyre.WheelLengthM` 0.62, `WheelWidthM` 0.22 and `WheelRotatingMassKg` 25 were
the same on every body in a catalogue whose tracks run 1.44 m to 2.20 m and whose masses run three to one.
A variant already states its own lock, its own compound, its own centre of gravity and its own weight
distribution; the wheel those figures act through was the town's.

**They are `wheelM` and `wheelRotatingMassKg` on the variant file now**, optional, falling back to the
nominal — the pattern `maxSteeringDeg` and `tyreFriction` already use. What was `TyreFigures` is what a
body gets when it names nothing, which is what a nominal figure is for: the town is sized against one car
and driven by nineteen.

**The nominal stays where it is, and that is not the same mistake.** `Car.MaxSteeringDeg`, `WheelbaseM`,
`LengthM` and the rest are the car the *town* is cut against — junction radii, lane widths, bay geometry
(CAR-11a). A junction cannot be laid against nineteen cars, and the figure that lays it is not a figure
anybody drives.

**No variant states one yet**, so the shipped town is the town this suite measures, and the mechanism is
there for the fleet to adopt a body at a time. The tread pattern is deliberately not sized from here: that
is one picture the whole fleet shares and its pitch is a fact about the sheet.

## 2026-08-29 — the load transfer is read off the tyres, so nothing has to cap it

**`Tyre.LoadTransferInGrips` was a number chosen to hide a wrong input.** The transfer was read off the
body: `(v_now − v_was)/dtS` off the solver, which is the car's *total* acceleration and cannot tell a
brake from a kerb. The comment sold that as a virtue — one path, so a shunt pitches a car the way a stop
does. But `a·h/(L·g)` is the **quasi-static** transfer relation, and handing it a one-tick collision
impulse is a category error: several hundred m/s², both clamps saturated, three corners pinned at the 5%
floor for the length of the settle, and a car that spins after a nudge. The cap at two grips truncated
that. It could not be deleted on its own, because the thing it was truncating was real.

**So the input changed instead.** Weight transfer is caused by the horizontal force **at the contact
patches** acting through the centre-of-gravity height — that is the derivation the formula comes from. The
tyre model already computes those four impulses and the town already applies them a loop earlier, so the
sum divided by `m·dtS` was in hand for four vector adds and no new storage. It is **bounded by
construction**: a tyre cannot push harder than it grips, so a transfer read off one cannot run away. The
figure, `LoadTransferCapMps2` and `Limit` are all gone and nothing replaces them.

**What it costs is the kerb, and the kerb was never there.** A collision now moves no load. It did before,
but only after being squashed to 1.76 g — so what was modelled was not an impact pitching a body, it was
every impact pitching it about as hard as a hard brake. That is the clamp being simulated, not the kerb.

**`Tyre.MinCornerLoadFraction` went with it, and its job was already done.** A twentieth of the car was the
floor under a corner, and the reason given for it was the shunted car — a wheel emptied by a collision has
a budget of nothing and a car on three such wheels spins. With the transfer read off the patches that is
unreachable: the worst a combined stop and corner can leave a corner on this geometry is about a tenth,
twice the floor that was guarding it. **What replaces it is nought and one**, which is not a figure but
what a load is — a wheel asked for more transfer than it stands on lifts, and a lifted wheel carries
nothing rather than pulling the car down. The model was already right about that everywhere it mattered:
`loadKg > 0f` guards every division in the tyre step, so a corner at zero has always been safe. The gate
says the invariant instead of the number — the four are the whole car, and none of them is negative.

**What it comes to.** Cornering is unmoved, which is the check that matters: the proving ground still takes
its corners at 1.06 of what the radius affords (1.05 before), and a slowing is still 10.76 m/s² against a
planned 8.20. Throttle is now bounded by the corner **the rubber is paying for** rather than by a lateral
figure a contact could inflate (CAR-3b), so cars are a shade less conservative — the fleet lap's worst line
goes 0.53 → 1.21 m of 3.00 and the softest launch 1.49 → 1.46 m/s². Two claims came back, because a town
whose cars are no longer flattered by phantom pitch puts people down again and `CrewRecallTests` and
`ClosureTests.ATownClosingRoadsStillCollectsAndDelivers` have subjects to watch. Two parking assertions
went the other way, each a car 3.0 m off a 3.0 m bar — and **that bar is a raw maximum, which counts a
lawful `E-4` pass as leaving the line**. What it is measuring is the thing to settle before it is re-cut.

## 2026-08-29 — there is one coefficient of friction, and a per cent is not a mechanism

**Three tyre terms were tried in a day and all three are gone.** `Tyre.LongFriction` 0.97 beside
`Tyre.Friction` 0.88 went first and deserved to: a ratio of 1.102 that rubber does not have, and per car it
was never a coefficient at all — a variant states one `tyreFriction` and the long axis was that times a
fleet-wide constant. It was **an anisotropy factor wearing a coefficient's name**. Its replacements were
honest physics and went the same way for a different reason: `LongToLateralRatio` 1.05 (a carcass really
does peak higher along the roll) and `LoadSensitivity` 0.12 (a tyre really does hold less as it is pressed
harder).

**What killed them is arithmetic, not principle.** Load sensitivity's whole visible effect is that a corner
moves more load across a track than a stop moves along a wheelbase, so it costs the corner more. Worked
through on the shipped geometry — the loss on a pair sharing load with relative transfer `r` is `k·r²` —
that is 3.2% off a corner against 2.0% off a stop. **A stop beats a corner by 1.2%**, on a town watched from
above, at the price of a term threaded through the tyre model, the build, the panel and the variant file.
The anisotropy was worth about the same.

**And one of the two things it was kept for was never true.** `LoadFactor` weighed each patch against
*that corner's own* standing load, which is the only sane reference — but it also cancels the static
balance exactly, so in a steady corner all four wheels sat at the same factor whatever `frontWeightShare`
said. The doc-comments and CAR-3d both claimed it produced understeer. It did not, and had not since it
was written.

**So the model is Coulomb: one coefficient, at every load and in every direction.** CAR-3d is retired —
never renumbered, never reused — and **CAR-3e** says the positive version: a patch is worth what it is
carrying, a transfer costs the four wheels nothing between them, and what the loads decide is which
*wheel* runs out first. That much is real and stays: a rear gone light under the brakes, an inside wheel
gone light in a corner. `OneCoefficientHoldsTheSameWhicheverWayTheLoadIsMoved` is the gate, and it doubles
the centre-of-gravity height to prove a bigger transfer buys no difference — which is where a load
sensitivity would show itself coming back.

**Terrain got the same treatment.** `Terrain.GrassMarkFactor` was 1.01, a one per cent shade on a mark
nobody can see, with a doc-comment claiming it tracked two other figures. Grass takes the bar.

## 2026-08-29 — figures flow one way, and half of them were flowing the other

**A turning circle was an input.** A variant stated the diameter its outer front tyre describes, and the
steering angle — the thing the car is actually driven by — was solved backwards out of it against that
body's wheelbase and track. Grip was authored the same way round: `GripMps2` was a coefficient of friction
and a gravity somebody had already multiplied together, and each variant scaled it by a `cornering` figure
that named nothing a person could measure. The rolling resistance was a deceleration rather than a
coefficient. Every one of those is a **derived observable standing where a raw term belongs**, and the whole
of what it costs is that nobody can check any of them: a `cornering: 0.71` is not a tyre, and a lock nobody
authored cannot be wrong.

**So what is authored is now raw and what is looked up is now derived.** `Tyre.Friction` is a coefficient
(0.88, a road tyre on dry asphalt), as was the along-the-roll figure beside it; the grips in m/s² are
`SimConfig.TyreGripMps2` and its pair, derived once. A variant states `maxSteeringDeg` at the road wheel
and `tyreFriction` as a coefficient; `CarBuild.TurningCircleM` is the kerb-to-kerb figure a maker quotes,
worked out from the lock and the body, and nothing decides on it. `Terrain.*Resistance` are coefficients and
the drags are derived. `handling` keeps the engine, the brakes and the gearing, and has no tyre in it at
all.

**The panel's dials moved with them**: tyre friction, steering lock, rolling resistance —
each naming the raw term it turns rather than what that term comes to. The lock now reads straight through
where the circle read backwards, so twice the dial is twice the angle and a tighter turn.

**Two knife-edges came out of it, and both were already there.** The derived figures moved by under half a
per cent and that was enough to tip `AMovingQueueIsFollowedRatherThanStoppedFor`, whose cancellation is
exact in algebra and not in single precision — it now allows a part in a hundred thousand. And the fleet
lap's crawl bar, which the van cleared by 1% before, it now misses by 2%: that margin was spent earlier
today when the grip and the braking became a road car's, and a claim held to a per cent is measuring the
lap's traffic rather than the van.

## 2026-08-29 — the fleet's weight distributions were authored, measured, and taken back out

**Every look was given the front weight share its kind actually carries** — 0.63 for the transverse
front-driven hatchback down to 0.43 for the mid-engined supercar — and on the pad it did what it should.
The forward rows went from 1.78× their own axles to 1.44 / 1.38 / 1.20 across the three pedals, monotone in
the throttle for the first time; the front slip angle at a third of the pedal fell from 14.0° to 5.5°; and
`×held` came to 1.09, which is a fleet sitting on the circle its grip allows rather than wide of it.
**Then the town fell over.** Six tests: the fleet lap could no longer gather three passes for two looks, a
car on River held a line into a bay it could not reach, another crossed a zebra at 15.5 m/s against a pace
of 8.0, and a road closure deadlocked against the call that raised it.

**The reason it broke is not the figures, it is that the driver does not know about balance.** The follower
plans on a bicycle model with none: it asks for the angle the geometry implies and expects the body to go
there. A car with a light rear axle does not — it rotates more than the plan, so a bay approach overshoots
and a stop for paint arrives late. Nothing in the planner reads `FrontWeightShare`, and until something
does, giving a body a real distribution is giving its driver a car it is not driving.

**So the mechanism ships and the figures do not.** `frontWeightShare` is on the variant file, resolved onto
the build and clamped there, and the panel's own slider moves the whole fleet's share while a town runs —
so what it is worth can be seen in one drag. No variant names one, so every car is the even split the model
has always used and the town is exactly the town this suite measures. The figures go in when the follower
can be told about them.

## 2026-08-29 — where a car carries its weight is a figure, and the figures can be turned while the town runs

**The even split was a literal in the load arithmetic, and it is the largest thing in the model nobody had
authored.** `TyreModel.Loads` put half a car on each axle before any transfer, whatever the body was; it is
now `TyreFigures.StaticFrontShare`, still 0.5, and the pad says that figure decides more about how wide a
car runs than grip and lock together. Held at 0.62, which is what a front-driven hatchback actually
carries, the whole fleet's forward circles collapse from 1.78× its own axles to 0.84× — the truck from
1.70× to 0.42×, the muscle car from 2.43× to 0.93× — and its astern circles blow out in exactly the mirror
of that, because reversing swaps which end of the car is leading the turn. Held at 0.38 the pair swaps
back. **It is one figure per body and it wants authoring per variant**, which is a pass of its own; what
is written down here is that the knob was found and what it is worth.

**What it is not is why a third of the pedal turns the same circle as all of it.** That survives every
weight split tried — 0.62 gives 0.84 / 0.86 / 0.95 across the three pedals, which is the same circle three
times. Two separate things: *how wide* is the weight, and *why the pedals agree* is that nothing in this
model makes speed cost power. Rolling drag is a constant m/s², there is no speed-squared term anywhere, and
`AccelerationMps2` is flat with a cliff at the top speed — so the steady state solves `drive = drag`, an
equation the throttle does not appear in. Any pedal that can beat the drag settles in the same place and
only the time taken differs, which the probe's settle window drops. An aerodynamic term is what would
separate them, and it would earn the top speed instead of cliffing it.

**So the figures got a panel** (`TrimFigures`, the menu's third page). Ten of them — the two grips, the
turning circle, the centre-of-gravity height, the front weight share, mass, acceleration, braking, top
speed and rolling drag — each as a share of what the build ships, a decade either side, laid out
logarithmically so shipped is the middle of the track. **Letting a slider go changes the figure under the
town that is standing** rather than laying the map again: the builds are made again into the array the
fleet already holds, the ground catalogue is re-read, and the standing cars take the weight their new build
gives them (`TownWorld.FiguresChanged`). What it deliberately does not do is re-plan — a car halfway round
a line drawn for the car it used to be keeps that line until its manoeuvre ends. Tearing the town down
would have lost the marks on the road, which are the thing the pad exists to show.

**A trim scales what a car resolved to and not the nominal figure behind it.** A variant states its own
grip, its own circle and its own centre of gravity, so a panel that moved `SimConfig` would move nothing
for most of the fleet. They are spent in `CarBuild.Resolve`, in the loads, and in the ground catalogue —
one site each, none of them a hot path — and every trim is 1 unless the panel has been opened, so a shipped
run resolves the same car bit for bit.

## 2026-08-29 — the tyre figures are a road tyre's, and a turning circle is a figure off a spec sheet

**The fleet gripped like a race car and turned like a go-kart, and the skidpad is what showed it.** Every
look cornered at 1.05–1.63 g and stopped at 1.72–2.67, which no road car on dry asphalt does; and every
one of them turned a 8.5–11.2 m circle whatever it weighed, because the lock was fleet-wide and the only
thing varying the radius was a wheelbase measured off a sprite. A 4200 kg armoured car turned inside a
hatchback.

**The nominal patch is now 0.88 g and the long axis 1.10 of it, so the nominal car stops at 0.96 g.** A
tyre is nearly isotropic; what a stop has that a corner does not is four patches pulling the same way with
the weight on the end doing most of it, which is a little over one and not the 1.643 that was there. Each
variant's `cornering` was re-authored against what its kind actually holds — 0.50 g for the armoured car,
0.62 for the truck, 0.72 for the vans and pickups, 0.85–0.95 for the ordinary cars, 1.10 for the supercar.

**A variant now states the circle it turns rather than a share of somebody's lock** (`turningCircleM`,
kerb to kerb, as a maker quotes it). The steering angle is worked back out of it against that body's own
wheelbase and track, because a turning circle is a figure a person can look up and check and a road-wheel
angle is not. It is why a truck is no longer a hatchback with a heavier file: the pad's *asked* column now
runs 3.55 m for the compact to 5.41 for the armoured car, where before every look asked for 3.05.

**And a variant states how high it carries its weight** (`cgHeightM`), where the whole fleet used to carry
the nominal 0.55 m. That figure is the whole of what makes a tall body handle like one — `a·h/(base·g)` is
the share of the load that leaves an axle — so a van at 0.78 and an armoured car at 1.15 now lift a flank
where a coupe at 0.47 does not.

**The pad is six pedals and its squares are half again as big.** Three each way — all of it, two thirds, a
third — because what a part throttle costs the circle is the thing the pad is for, and two points do not
show a curve. The lightest row is where the fleet now sits on its own geometry: the truck, the van and the
compact turn 1.01–1.02× their own axles at a third of the pedal, where under the old figures every row of
the pad was saturated. The square went to 150 m because the circles grew with the locks, and the pad
reports a car leaving its own square rather than letting two of them meet.

**What the gate on that pad is, changed with it.** "Every car is turning, under every pedal" was a claim
the fleet no longer owes: whether a third of an armoured car's throttle will push four scrubbing patches
round is a fact about that vehicle, not a bound. The gate is the whole pedal in both gears; what a lighter
one will not move is quoted beside it.

**What it cost, and what is still owed.** The proving ground breaks two claims it kept before. The straight
is no longer driven to 90% of the gear's own cap — 55.9 m/s of a 75 m/s cap, because a car that stops at
0.96 g needs twice the sight it needed at 1.91 and the straight is not long enough to give it. And a
slowing is 1.29× the planned rate against a ±25% band, where it was 1.10× before: the overshoot above what
the tyres hold is roughly constant, so halving the planned figure doubled it as a share. **Both are the
speed axis rather than the grip one** — `MaxSpeedMps` at 75 (270 km/h) is not a town car's top speed,
`AccelerationMps2` at 11.7 is 1.19 g of tractive effort that no road car produces, and the straight's own
length is not derived from the stopping distance it has to allow. That pass is not this one.
`ClosureTests.ACasualtyBringsAPoliceCarWhoseOfficerClosesTheRoad` regressed with them and is unexplained:
the patrol reaches the scene and no officer gets out of the car.

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

**The self-driver's ceiling stayed on the nominal patch, which is the one figure left in the tyre path that
is not the car's own.** Moving it to `CarBuild.GripMps2` is correct by CAR-11 and was tried: it lets the
grippier looks use more of their pedal, they arrive at crossings faster, and one car on River crosses the
paint at 12.1 m/s against a 11.7 m/s bar. **The crossing is only found on the lane the car is standing
over** (`CrossingOnTheTemplate` reads `CrossingsOn(NearestLane)`), so a faster car gets a shorter approach
to it — that is what has to be fixed before that half of the ceiling can be the variant's, and it is a
`world/road` change rather than this one.

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
