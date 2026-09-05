# Decision log — the walker

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-05 — the patience buys a road, and a car standing on the band is not one

**Walkers were shoving stopped cars down their own lanes**, on the paint, in traffic that had done nothing
wrong. The claims were right about all of it and the walker never read them: a car standing on a zebra is a
stretch of the *lane* and writes nothing on the walk (TER-5c.1), so on a crossing way the only thing a car
can ever cut a walker's grant with is the band refusal — and past `KerbPatienceS` the escape granted the
band whatever was on it. Eight seconds behind a car queued over a crossing and the walker had an
unobstructed permission through a tonne of steel, which it then spent.

**The escape takes a road and never a body.** A driver's road is a claim handed back by driving on, so
taking one past the patience is a walker stepping out in front of a car that stops for it — which is what a
pedestrian's priority is. A body over the paint hands nothing back and no permission moves anybody through
it. **What tells the two apart is the bar the pavement already holds a body to** (PER-24), the walker's own
pace, so this is the rule the walk already ran on said of the road: one coming through the band is traffic,
one going nowhere on it is a standstill.

**And a standstill at a kerb is not a wait, so the clock had to be let run.** `HeldAtTheKerb` froze the
clock that gives up a leg, on the true ground that a red ends and a gap arrives; a car parked over the paint
does neither. Refused by a body, the walker now names it (`RefusedBy`) and is held by it like any other body
going nowhere — which is what the clock needs to see, since a walk cut at a band edge was previously held by
nobody at all.

**The rescue exemption is one instance of this and no longer its own argument.** A rescue standing over the
paint was let through by the escape because it "is a stopped car, which is what the walker taking the band
would have made of it anyway" — and the walker made nothing of it, because there was nothing on the walk to
make it of. It is now refused as a car standing on the band, and what answers it is the clock.

**What it cost.** Five minutes of Odesa: walks arrived 448 against 446, walkers still held at the end 6
against 14, the longest hold 97 s against 121 s, 2 rings of walkers against 3, and 5.19% of the run spent
inside the gap they keep against 5.89%. **Walks given up went from 533 to 743**, and that is the honest
price rather than a defect: a walker that cannot get past a body has one rung — a clock, and then the trip
is thrown away — where a driver walks ten, and the number is what that missing catalogue costs when the town
stops letting walkers push their way through. It is AGT-7's gap, priced.

## 2026-09-05 — a claim has a width, and the step round is an ask rather than an aim

**The walker was given the driver's model on ground the driver's model is wrong about.** A carriageway lane
holds one car abreast and a claim on it is honestly one interval of arclength; a walking lane is two bodies
wide on purpose, and the same interval makes a single file of it. Everything walker-only in the town path had
grown to work around that one hole — a re-aim with its own geometry, a walled-in flag, a permission read off
the ground for a body off its line — and none of it could work, because the step turned off the line while
the grant stayed on it and the walker stood a gap short of the body with its shoulders pointed at the gap.

**So a claim now carries the span its holder covers across the way** rather than its distance from that
way's line, and whoever reads it says where across the way *it* is (TER-4c.2). At nought it is the question
it has always been, which is every driver and every walker on its line; what it buys is that a body can ask
about ground it has stepped to instead of ground it has left. Read off the holder alone, a way was
two-dimensional for whoever was written into it and one-dimensional for everybody reading it, and nothing
could move out from under an answer.

**And the step is that ask and not a re-aim** (PER-24). The offset comes off the body being got past — the
far edge of its span, the room a reader needs, two shoulders — the ground is asked whether it will take it,
and then the whole walk is asked for again from there and kept only if it buys more pavement than standing
still did. One offset, written once, read by the ask, by the grant and by the feet: the fault this keeps
out is the one this log keeps recording, which is two answers about one piece of ground.

**What went with it**: the step's own geometry (`IsInTheWay`, `PassM`) and its unit tests, and the walled-in
flag the follower carried. The step is decided where the grant is taken, so there was nothing left for them
to decide.

**The exam is clean for the first time** — 20 of 20 cards, both findings out of date, and the claim that a
walker gets past a body standing in its way is answered rather than waiting. Over five minutes of Odesa:
446 walks arrived against 433 and 533 given up against 617, walkers still held at the end 14 against 21, the
longest hold 121 s against 208, rings of walkers each held by the next 3 against 5, and time spent inside the
gap they keep 5.9% against 7.4%. The drivers got it back too — 22 cars standing at the end against 33, and 96
bays against 89 — which is what a pavement that clears itself is worth to the road beside it.

**What this does not touch is the reason the number is still 533.** A walker that cannot get past still has
one rung: a clock, and then the whole trip is thrown away and a new destination drawn. A driver in the same
position walks ten. That is `AGT-7`'s missing catalogue and it is still missing.

## 2026-09-05 — the walker has an exam, and the map it is asked on has no traffic

**What a walker does was only ever asked of a city.** The arithmetic under it is unit-tested to death and
the shipped towns report walks given up by the dozen, and there was nothing in between: a count off Odesa
says the town is jammed and never which shape of ground jammed it, because a walk in a city is whatever
that city happens to hold. `Footway` is the answer ([citygen](../../../citygen/docs/requirements.md#the-maps)) —
the driving exam's own lattice with one walk staged at each cell — and what a card claims is what the
walker did with an order, never that it was given one.

**Nothing drives on it.** Half of what a walker owes the road is what the road owes it back, and a card
that failed with a car on the map leaves nobody able to say which of the two agents was wrong. The car's
half is asked, with the traffic staged, on the driving exam's four cards about paint.

**The exam's first run named the crowd fault exactly**, which is what it is for: a walk down an empty
pavement, round a corner, over one arm, over two, past a lit crossing, round a dead end's head and four
blocks up a street all pass, and the two cards where somebody is **standing** in the way — on the pavement
and on the paint — do not. The two cards carried it as findings, and the day the claim gained a width the
suite said the findings were out of date rather than leaving them to be re-discovered. That is what a
finding is for.

**And a leg running out is not a walk given up.** The instrument read the first one as the end of the walk
and reported a long walk that crossed four blocks and arrived as a failure. A line is laid again from
wherever the body has got to (PER-8), so a body standing still and a body about to walk on are one state,
and only arriving tells them apart.

## 2026-09-04 — a walker off every line was granted the whole town

The cut at every body held, and then the walker walked through it anyway a few seconds later. What let it was
not the cut: it was that the pavement's grant is taken **along a way**, and a walker that is on no way was
handed back the permission it started the pass with, which is *everything*. **A body with nothing to be cut by
was a body nothing had cut.**

**And that state is the town's commonest, not a corner of it.** A walk was not counted as being on a way until
it had walked a point of its own line, so every walk in the town began off the network — and the clock that
gives up a leg hands a held walker a fresh line, which put it back there. So the one thing that reliably
freed a walker stopped at a parked car was the tick after it stopped being stopped by anything.

**Two answers, because there were two faults.** A walker standing at the first point of a line it has not
walked yet *is* on the way that point is on, and reading it as being nowhere threw away the whole ordered
machinery — the queue, the precedence, the grant — for the commonest state a walker is in. That is fixed
where it was wrong. What is genuinely off every way — a body shoved aside, one under a hand, the last stride
onto a doorstep — is asked of the ground instead: the ways under the box swept from the body to where it is
going, and whether anybody's body is standing over them. **It is a permission and not a distance**, because a
body off every line has no metre to measure one along.

**Bodies bind it and reaches do not.** Asked of everything held, every walker in a crowd holds the metres in
front of it and nobody in it can take a step; asked of bodies, a step onto ground somebody was merely granted
is a body outranking a reach, which is what the table already says happens. And it is read the way a body on
the way reads it — to the body edge, and past whatever stands aside of the line that travels there — or the
same piece of the town would answer two ways depending on who was asking.

**And nothing holds a body off the place it is walking to.** Arriving is what ends that walk, so a
permission read off the ground that could say *you may not reach where you are going* strands a paramedic a
body's width short of the casualty it was sent to and leaves it there for the rest of the run. On the way it
is one more thing to be cut at; at the end of it, it is the end of the walk.

**The clock had to be told what a standstill is, again.** It was reading one off the walker's own step-round,
which is set for the bodies a walker can get past and for no others — so a walker held by a body standing at
its own destination, and a walker held on the ground with nowhere to step, both stood with the clock frozen.
It is read off the holder now: a body on no way is a standstill to whoever it holds, however busily it is
walking, because what is on a way is in a queue with an order to it and two bodies off every way can hold each
other. Left out, Odesa ran thirteen rings of walkers each waiting on the next.

Over five minutes of Odesa the town ended with twelve walkers held rather than twenty-three, six rings rather
than thirteen, and 223 walks arrived against 222 — bought with 27 legs given up against 12, which is the
recovery doing the work that walking through a parked car used to do.

## 2026-09-04 — the walk is cut at every body, and the step is what it does about one

A car parked across a footway showed its claims correctly, the walker's own stretch stopped exactly at them
— and the walker walked on through anyway. The claims were right and the picture was right; what was wrong
was that the *permission* taken off them was computed a second time, from a scope that left a body going
nowhere out. **Two answers about one piece of ground, and the walker was reading the wrong one.**

**So every body in front cuts the walk, whatever it is doing.** A body going nowhere holds the stretch it is
standing on for as long as it stands there, and a grant reaching past it is a walker crossing ground it was
refused (TER-4c.3). What the body's movement decides is the *reply* and never the cut — waited for where it
stands, or stepped round with whatever room the cut leaves in front.

**The step round survives and is bounded by the grant**, which is what it is asked as.

**Which needed the clock to be running, and it was not.** Being held by the claims returned a walker out of
its decision entirely — right for a queue, whose ground ends itself, and wrong the moment a body going
nowhere could hold one. A walker cut at one was left standing with no clock, no leg given up and no line
drawn round it. Fixing that alone cleared four recovery runs and a rescue that had been failing.

**And a body standing where the walk is *going* is not one to get past.** PER-24 already said so and the
step's own geometry already tested for it; the grant did not, so it named the casualty a paramedic had
walked at as an obstruction and the crew was walked off its own errand. The two now ask it in the same
terms, which is the whole of why it is one predicate and not two.

## 2026-09-03 — going nowhere is a speed and not a use

A car driven by hand over a pavement had a walker step out in front of it and keep walking. The car had
claimed the whole time — its own box and the ground it could not stop short of, on the footway's ways like
any other body standing there — and the walker read it, picked it as the body in its way, and aimed *past*
it. Nothing was missing from the index; the reading of it was wrong.

**The walk was cut at a use rather than at a body.** The pavement's grant is taken over the stretches laid
from a line somebody is following, and a body laid from its own pose is left out on purpose: that is PER-24,
and it is what lets a walker get past a wreck instead of queueing behind it for the rest of the run. But
which of the two ways a stretch is written down says how it was *measured* — from a line with a margin, or
from a pose at the body's true extent — and never whether the body is moving. A hand at the wheel, a shove,
a slide and a car crossing the pavement are all written from the pose, and every one of them can be doing
50 km/h.

**So the walker asks the body and not the row.** Every stretch laid where it lies is now walked rather than
answered with the nearest one, and each is put to the town: coming through, and the walk stops short of it
with the walker's own gap; otherwise it is the body to be stepped round. Asked the same way of a car and of
a person, because which fleet a number is in is not what a person in the way of one is deciding.

**What "coming through" is took a second go, and the first one made a heap.** Written as *moving at all*, it
was right about the car and wrong about the walkers: a body that has stepped aside for a moment is off its
own line and moving, so every walker behind it stopped dead instead of stepping round, and the town's crowds
came back — the evacuator never reached a wreck in four minutes. **The bar is what the feet can get past**,
which PER-24 already said in the words *under way along the same lane*: a body going the walker's way no
faster than a walker walks is one a step gets round, and one coming across the walk or down it faster than a
walk is not. It is the same relation the kerb holds a rescue to (PER-15) and the same figure, so there is no
new number in it.

**And a car standing still on a pavement was walked through, which was a second fault behind the same
complaint.** Every stretch says how far aside of its way's own line its holder stands, and a reader passes
whatever stands further aside than half the width of what travels there (TER-4c.2) — half a car on the road.
The pavement was built with that bar at **nought**, so a body stopped being in the way the moment it
was a hair clear of the line, and a walker takes half its own width either side of the line it walks: a car
parked alongside a footway was a car the walk went straight through. The bar there is now half a body, which
is the road's own statement in the walking side's figures and no new rule.

**The driving side needed nothing**, and that is not an inconsistency. A driver is held off the same
stretches by the same claims already; what it decides on top of that is whether to *go round* one, and every
swerve costs an obstruction wait first — a body crossing a lane is gone before the wait is up. A walker has
no such wait and steps round in the tick it meets a body, so the moving test is what stands in for it.

## 2026-08-30 — a walker walks against its stop, and a stopped rescue is not a rescue

Five minutes of Odesa put twenty-five walkers in a heap and left one of them standing for fifty-two seconds
with no decision taken at all. The stuck probe now counts both — a heap is four or more bodies nearer than
half the gap they keep, and a hold is a run of ticks a claim held one body — and what the two of them
showed was one shape and one accident.

**The shape.** The biggest heap was ten walkers on one way of one pavement at 1.8 m spacing, every one of
them granted −0.20 m, each held by the one in front. The gap a claim keeps is 2.0 m and a walker's
stopping distance at its pace is `v²/2a` = 0.20 m, so the numbers were not a queue at all: every body had
set off at full pace on a grant of a centimetre and come to rest exactly one stop inside the ground the
body in front had been given. Nothing gets that back — feet have no reverse, and a walker somebody else's
claim is holding takes no decision, so no clock runs behind it. The column could only move one stop at a time, in
lock step, which is what a heap of people looks like from above.

The bar is now what the body needs to come to rest in: this tick's stride and the stop after it. **Not the
pace's own stopping distance** — tried first, and it made things worse. A pair already a little inside one
another's gap both stood for ever, and rings of two walkers each held by the other appeared where there had
been none; the creep out of a violated gap is the only thing that breaks one, and reading the bar off the
speed the body is actually doing leaves it there, because that is nothing at rest.

**The accident.** The one fifty-two-second hold was not behind anybody. It was a body stopped halfway over
a crossing, refused the band in front, patience at 55 s against a bar of 8 — held by an ambulance's road
with the ambulance standing still on it at 0.4 mm/s. PER-15's escape is disabled against a rescue, and the
reason it is says a call lasts seconds and is going to pass. This one was not passing. The exemption now
asks whether the rescue is coming through at all, against the walker's own pace, and walks every stretch
over the band rather than taking the first — a rescue standing on a piece of road cannot hide one moving
through it. **Zero was the wrong bar**: a car held in a queue creeps at fractions of a millimetre a second,
which read as coming through for as long as it sat there.

Over the same five minutes: twenty-five walkers ever in a heap became four, the worst-off walker's time in
one went from 5% of the run to none of it, the longest hold from 52 s to 37 s, and the walks the town gave
up from 147 to 76 against 756 arrived.

## 2026-08-29 — the crossing patience is spent where the body is, not where its line still goes

Twenty-five minutes of Odesa left eighty-four walkers standing still, and the worst of them had held one
spot for nineteen of those minutes. Every one was in a crowd, and every crowd had at its head a body
standing **in the carriageway**, refused the band in front of it, with a patience clock reading zero.

`MayStepOnto` already carries PER-15's escape "wherever the body has got to on it" — past the patience the
band is granted and the traffic gives way. What was clearing the clock it spends was `AtTheKerb`, on a test
about the *line* rather than about the body: a walk laid onto a crossing is consumed as the body walks it,
so a walker part way over has no crossing point left **ahead** of it, and that read as "this body is not
crossing anything" and zeroed the patience every tick. The escape could never arm for the one body that
needs it — one already in the road, which is the body a driver is stopped for.

Where the body is standing is what decides it now. On a pavement the clock is nobody's and is cleared; on
ground a car may drive on, with no ground granted in front, it runs — which is what the drivable branch
below it already did, and the two are one rule rather than two.

Two walkers were left standing at the end of the same twenty-five minutes, neither for more than a fifth of
it, and the walks that arrived went from 1868 to 2010.
