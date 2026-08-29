# Decision log — roads and junctions

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-08-29 — a body level with the asker is not in front of it

Five minutes of Odesa left twenty-three walkers standing still for the whole run, and every one of them was
in one of three crowds queueing behind a pair that never moved. Each of the pair had a grant of minus two
metres, cut by the other, and each was the other's cut: two bodies at one metre of one way, each held off
ground the other was standing on, for the rest of the run.

The pair is what the **end of a way** makes of everything that reaches it. A walker is placed on the way its
own line is stationed on, and one that has walked past the end of that way is carried back to the last metre
of it (`IsAfoot`); its ask is then a window of no length at that edge, and every body clamped there has the
same front. `NextSpokenFor` already says *in front means the body is*, so a body whose own length stops short
of the asker's front never reaches the cut — what reached it was the body whose front is exactly the asker's,
which is neither in front of nor behind anybody.

`GrantedOn` passes over that one now, on the same argument the claim above it is passed over on: a cut behind
the asker's own front is not a shorter grant, it is a grant that has stopped being a distance to walk. It is
the abreast case and nothing wider — a body whose length reaches past the asker binds exactly as before, and
an overlap still comes back negative and says so. `FootOccupancyTests` states the same relation, which is why
its "in front" test is now inclusive: the pair at a way's end are not two bodies one behind the other, and
asked as though they were, PER-13 has no answer either of them can act on.

Two walkers stayed still for a minute over the same five minutes afterwards, and both were walking again by
the end of it.

## 2026-08-29 — a grant is a distance in front of the nose, and a claim behind it is not a cut

A car crossing a junction would stop dead halfway out of the box and stay there for half a minute with the
road in front of it empty, reporting `P-8 queueing`. Its grant was minus seven metres — a car's length of
negative road, which inverts to a target speed of zero whatever is ahead, and which nothing the traffic
does can ever hand back.

Two things made it. The first is that a way the nose has already left was still being asked. The nose is
carried onto each of a car's ways by clamping (`OnTheWayM`), so on a way that ends behind it the question
became a window of no length at that way's far edge, and anything reaching that edge answered it. The
second is that the stretch reaching it was a claim: the car queueing behind for the same movement lays its
claim from where its own road was cut, which is the leader's near edge — a car's length behind the leader's
nose — and read as a cut, that claim answered the leader from underneath its own body. The pair of them are
a stable trap: neither car can move, so neither ever stops laying what freezes the other.

`WhereTheGroundIsCrossed` already said the half of this that applies to a crossing point — *in front means
in front of the nose*, and a cut behind it is not a shorter grant but a grant that has stopped being a
distance. The grant now says the same about a stretch: `GrantTheGround` asks nothing of a way the nose is
past, and `GrantedOn` passes over a claim whose near edge is behind the asker. **A claim is ground its
holder has not reached** (TER-5e), so one the asker is standing on is ground the asker has — it can never
be a body to be held off, and it is the one kind of stretch that cannot be a contact.

What is deliberately still allowed to answer from behind is a body: a reservation, a wreck or somebody on
foot reaching back past the nose is an overlap, and the grant is left free to come back negative and say
so. That is the whole of the difference between the two halves of the unit case in `LaneOccupancyTests`,
and `NoClaimCutsAGrantBehindTheNoseThatAskedForIt` watches every shipped map for the rest of it.

## 2026-08-29 — the book holds the answer, and one metre is one body's

`TER-4c.1` has always said that ground is asked for, answered, and then it is the asker's, and that a
mechanism which could grant the same metre twice would be no mechanism at all. The code granted correctly and
then threw the answer away: `AskForTheGround` laid the ask — bounded by the rules that stop the car and by
nothing in front of it — `GrantTheGround` worked out where the traffic cut it, wrote that to
`CarFleet.AuthorityM`, and left the ask standing in the book. Every other reader for the rest of the tick
read the question. A car held at a red still held the sweep of road beyond it; the movements that road
crossed were refused by ground its holder had itself been refused. Asked of the shipped maps, two bodies
held one metre by as much as 13.72 m on Fleet, 3.63 m on River and 0.95 m on Odesa.

So there is a third walk: the asks, the grants, and then `CutTheGroundToTheGrant`, which brings every
stretch's far edge back to what its owner was given. It is a walk of its own because it moves far edges,
which is what a movement's crossing question reads — done inside the grant loop, the answer would turn on
which car was asked first, and that is the same reason the asks and the grants are already two.

**On the join a car is crossing, the seam moves and the union does not.** What such a car holds there is its
road and the claim beyond it, laid as one piece of ground with the join between them wherever the road
happened to reach; cut without the claim following, the metres between the answer and the ask fell out of
both, and a car sitting in a box sat on ground a crossing movement was free to be granted.
`ClaimWhatTheAnswerTook` hands them over — as a claim rather than as road, which is their honest name: they
are metres the car has not reached, and a car already committed to the box holds them with the rank that says
so.

**And the credit is gone with it.** A stretch in front used to be worth its holder's own stopping distance,
on the ground that a body under way will have left those metres by the time anybody arrives. That is a true
thing about traffic and the wrong place to say it: once the answer is written back, a credited answer *is*
two bodies holding one metre. The cut is now at the near edge and never past it, and the standstill case is
untouched — a stopped body was always worth nothing.

What that buys is the junction. Odesa's minute abandons 6 cars against 15, gives up 13 places against 18,
reroutes 14 times against 20, makes 50 emergency stops against 64 and takes 803 junctions against 799;
River takes 167 against 158 and holds at a line 70 times against 74. What it costs is station-keeping at
speed, and the proving ground is where that shows: fifteen cars at seventy-three metres a second can no
longer sit in each other's stopping distance, so they overtake — 9 swerves against none — and one of them,
once in twenty minutes, came off its line by 5.49 m of the 3.00 m the lap allows and gave the lap up. **That
claim is broken and left broken**, because it names something real: a swerve at that speed that does not
finish is `E-4`'s to answer, and papering over it in the rig would be measuring the ruler.

`NobodyIsGrantedGroundSomebodyElseWillStopOn` is deleted rather than mended. It asked of one map whether a
grant reached past where the car in front would come to rest, which is the credit's own arithmetic;
`NoTwoBodiesAreGrantedOneMetre` asks every shipped map the stronger question the requirement actually states,
and two tests of one claim is the second mechanism `SIM-7` is about.

## 2026-08-29 — the box refuses a car at a place, and only lets it in where it can wait clear

The gate on a junction answered *whether* and the grant answered *where*, and they were the same question
asked at two resolutions. `TheMovementIsFree` walked every section a movement was driven over and came back
`false` on the first one that was anybody's, and the car was then stopped half a body length short of the
boundary — so a body standing on the far corner of a box held the near half of it against a car that would
never have reached the corner while it was there. The grant already cut at the near edge of the first
section that was held (`WhereTheGroundIsCrossed`), a margin short of it; it never got to say so, because the
ask is clamped to the stop point the gate had already set.

So the gate answers in metres too, on the same figure: `FirstHeldOnTheMovementM` returns the near edge of
the first section that binds, and the car is stopped a body margin short of it. That figure is what keeps
it from deadlocking where the verdict did not — a car held a margin short of a section reserves no metre of
it, so the movement crossing there still reads it free and goes, and the two resolve instead of standing
one on each side of the ground they share.

**What it cost, until the second half of it went in, was cars stranded in the box.** A car let up to the far
side of a wide junction stands on every crossing it went over to get there; the movements behind it are then
refused by a body that is itself waiting, and Odesa's minute abandoned 23 cars against the 15 it abandons
either without the change or with it complete. So a car may only be let in as far as it can come to rest
with its whole body in a gap between the runs (`WaitsClearOfTheCrossings`) — which is the box's own version
of not entering one you cannot clear, measured rather than assumed. Where there is no such gap, and a
crossroads whose arms are one lane each is generally such a box, the car stops at the boundary exactly as it
did before: the shipped towns' tallies are unmoved, and what the change buys is the wide box, where the
free ground is real.

## 2026-08-28 — the walkers go into the road's book between the asks and the grants

TER-4c says a person in a lane cuts the road a driver is granted exactly as a car standing there would. The
book did not do it. The walkers were written into the road's book as the *last* pass of the rebuild, after
every grant had been taken off it — and the book is wiped at the top of the next rebuild, so no driver ever
read a band while it was deciding how much road it had. What held a car off somebody on the paint was the
crossing's own stop and the headway reading, and nothing at all held one off somebody standing on
bare carriageway except that same headway.

The order is one question with the walkers on both sides of it, so they belong between the two halves of it:
the cars' asks first, because what a body at a kerb may step onto is whether a driver's road is over the
band; then the walkers; then the grants, because a band in the book is ground a driver may not be granted.

River's measured minute went from two people knocked down and two cars wrecked to none of either, and its
touches from 55 to 50.

## 2026-08-28 — what a body is written onto and what a manoeuvre reads are one walk

Two pieces of code answered *which ways is this place on*. Writing a body that is not driving a route laid
it on the lane it was nearest and on every join of the junctions at either end of that lane; reading the
ground under a manoeuvre's template asked the nearest lane and stopped. So a car crossing a junction — whose
road is written on the **join** and on no lane at all (TER-5c.1) — was invisible to every swerve, back-off
and bay exit swinging through the same box. The book was not wrong; nobody was asking it.

There is one walk now (`GroundUnder`), and both sides call it: the lane, **the lane running back the other
way** where the body reaches into its band, and every join of a junction the place is lying under. Writing
and reading cannot drift, because there is nothing left to drift.

**It costs the town its reactive templates where the ground is genuinely somebody's**, and that is the
finding rather than a side effect. Odesa's measured minute went from 13 back-offs to 2 and from 9 cars
abandoned to 15: a back-off happens where a car is stuck, a car is stuck at a junction, and reversing into a
junction is reversing into ground the traffic crossing it is committed to. The ladder escalates instead —
19 places given up against 12, 20 reroutes against 13 — which is the honest answer to *there is nowhere to
back into*, and the alternative is a car reversing into a movement that cannot see it.

## 2026-08-28 — a claim is answered every tick, and its holder is told when it loses

A claim was answered once, at the moment the desk took it, and re-laid unread from the car's own field for
as long as the entry wanted it. Everything else in the book is laid and answered afresh every tick; the
claim was the one hold that remembered an answer. So a right of way took the ground and nothing said so: the
stronger movement was not cut, the claim's holder was not cut either, and the pair drove at the same metres
from opposite sides — the exact failure TER-5c.1 exists to prevent, reintroduced by the one stretch that
skipped the walk.

It is answered again after every body is in the book and before anything is granted off it, and a claim a
stronger rank has taken is withdrawn inside the same tick. **The holder is told**, because the only thing
that knows what a claim was for is the entry that took it: that entry is re-entered through its own `Sa`,
and takes the claim again or hands on.

**A rank above it takes a claim and nothing else does.** The first version also gave a claim back for a body
standing on the ground — which reads well and is wrong twice over. It is the duplicate SIM-7 is about, since
a body on the ground already cuts the claimant's grant on the way it is driving; and it refuses the one
thing a claim is for, because the stretch `E-4` claims is by construction the stretch containing the body it
is swinging round. Measured, it took every swerve and back-off out of the town on the first tick.

## 2026-08-27 — a junction admits no movement that reverses the direction of travel

The turn-around was in the table from the beginning and was drivable by nothing: two opposing lanes a
lane's width apart join on a 1.5 m semicircle, tighter than any car's lock at any setback. It was carried
anyway — classified, laid, measured against every other movement at its node, given the bottom rank of the
right of way, and priced at infinity so that no route could be handed one — which is a great deal of
machinery to say *never*.

It is gone (TER-5f). A lane's successors are the lanes leaving its node **that are not the one running
back down its own stretch**, and everything downstream got shorter for it: no join to draw, no rung to
skip in the setback widening, no row in the crossings table (a quarter of Odesa's 1472 movements were
turn-arounds), no rank at the bottom of `RightOfWay`, and no `continue` in the overlay, the census or the
tests to keep it out of a figure.

**What a route may still do is come back down the other side of one stretch**, and it is priced rather
than joined: a car park's frontage, where the driver parks and unparks (`GEN-4l`), and a dead end, where it
works itself round (`P-19`). Which stretches those are is a fact about the town laid with it — the ways at
the bays, and whether a lane has any way out at all — and it is handed to the network as flags, because
the road is below the car parks that hang off it.
## 2026-08-27 — an obstruction is a reservation that generally reaches nowhere

A body the road was not driving held the metres under it and nothing else: its footprint, no ground ahead.
That is right for a wreck standing in a lane and wrong for the same wreck two seconds earlier, still sliding
— and wrong in the direction that costs, because the traffic behind was granted the road that body was about
to be standing on. The pavement never had this problem: `AskForThePavement` lays every walker, moving or
standing, as one stretch reaching `max(stopping, …)` ahead, so a standing walker is already a reservation
that reaches nowhere. The road's side was the outlier.

So a body that is not driving a route lays the third edge every other body carries — its own stopping
distance past where it stands, from the speed it actually has. **Nothing is a special case any more: static
and slow bodies reserve almost nothing ahead and fast ones reserve a lot, which is the same arithmetic a
driver's reservation is, asked of a body with nowhere to go.** `LaneUse.Obstruction` survives as a *name*,
because `HeadwayKind` and `E-4` turn on it — whether a thing can be got past is a separate question and is
not being answered here.

**Where the body is sweeping a template, the sweep is that ground and is already laid.** The two are one
answer to one question — what this body is committed to — read once off the line it is driving and once off
the speed it is doing, and taking both is a car holding a swerve's worth of lane twice over.

**What the measurement threw out was the tidier version.** Laying the stretch with a margin behind it, so
the credit could read "a body is worth its stopping distance" with no switch on `LaneUse` at all, reads
better and measures worse: a margin on the stretch is not only a gap for whoever is behind, it is a fatter
body in *every* question asked of the book — the templates a manoeuvre may lay, the junction sections a
movement reads as free. Odesa's measured minute went 69 touches to 88 and River's 35 to 54, with peak
interpenetration up from 208 mm to 325 mm. So the margin stays where it was, the credit keeps its one
switch, and the switch is documented for what it is: whether the stretch carries a margin of its own, not
whether the thing on it happens to be moving.

With the ahead-extension alone every map is back to its baseline to the touch — Odesa 69, River 35 — which
is the honest report: the hole it closes is one a measured minute of these towns does not open. It is pinned
by a test instead (`ABodyOffItsRouteHoldsTheRoadItsSpeedStillNeeds`).

## 2026-08-27 — the grant is a question the book answers, and both networks ask it

The road's grant and the pavement's were the same forty lines twice: walk the ways under the ask, walk the
stretches spoken for in front of the body, cut at each holder's near edge plus what the ground beyond that
edge is worth, take the least. Both switched on `LaneUse` to decide the credit — a body under way is worth
its own stopping distance, anything going nowhere is worth less than nothing, and the asker keeps its margin
off that — and both then made a second cut, at a place that is nobody's stretch, with the same margin
subtracted by hand: a junction's crossing point on the driving side, the kerb line of a refused lane on the
walking one. Two copies of one rule, in two slices, free to drift.

So the grant is `LaneOccupancy.GrantedOn`, asked like every other question of the book, and what the asker
brings to it is `LaneCredit` — its braking, the ground it keeps off a body that is going nowhere, whose
reservations it reads as traffic, and the rank it asks with. **The credit rule and `Binds` now exist once**,
and the place-cut reads the same figure by name (`AtAPlaceM`) rather than restating it. A walker asks with
the weakest rank, which is the honest statement of what was already true: no claim on the pavement is a
walker's to take, so every stretch in front of it binds.

**It changes no arithmetic.** The map from a way's own metres back to the line is affine and increasing, so
taking the least on the way and carrying it home is the least carried home; `Binds` at the weakest rank
admits everything, which is what the walking side did by not asking. The suite is green and Odesa's measured
minute is unmoved — 10 wrecked, 69 touches, identical run to run.

**Two smaller things fell out of it.** A rank is now a floor on the walk (`NextOver`) rather than a filter
over what came back, so "is a rescue over this" and "is anybody on the paint" stopped being hand-rolled
loops and became masks like their siblings. And `Nobody` no longer matches itself: the town's furniture
stands under that number, so a question asked by nobody in particular — a walker at a kerb, an overlay —
excluded every bollard in the town from its own answer. Nothing in the town asks one of those with a mask
the furniture is in, so it cost nothing today; it is the trap the furniture was given a use of its own to
escape, sprung from the other end.

## 2026-08-25 — where a road's paint breaks is the road's answer, not the drawing's

The dashes were laid by walking each road at a metre a step and asking whether the point was inside a
junction disc or on a zebra. Two things were wrong with that and only one of them was the sampling. A
crossing is set back onto the arm it approaches, so the disc is several metres behind the paint, and the
metres between them are neither a junction nor a crossing — every arm of every junction in the town was
therefore dashed right up to the mouth of the box, past the bar a driver is meant to stop at. The metre
step was the smaller fault: a run closed on the first blocked sample, so it could carry up to a metre of
zebra with it and lay a dash on the bars.

Both are gone because the boundaries are no longer looked for. `CentrelineRuns` takes them from whoever
already measured them — `RoadCuts` for the discs, the stop-line register for the bars — and a bar closes
the road from itself through the junction it names, which is one span with the zebra inside it. The runs
are exact, and the drawing lays dashes along what it is handed rather than deciding anything.

The kerb line went the same way for the same reason. It survives as a rim on the union of ribbons, discs
and fillets, and a car park was not in that union — so the line was painted straight across the frontage
every car entering the lot drives over. **The lot could not be added to the union**: it is laid flush
against the kerb rather than over it, so its fill covers none of the rim. What breaks the line is the
frontage itself, which `RoadFrontages` now owns for both the slices that need it: this, and the cuts
`ParkingSections` sets back from it. It used to derive the same projection privately, which is the second
copy the markings' first rule is about.

Breaking the kerb line left the same line painted a hand's width away, because a bay was outlined on four
sides and a row of bays laid side by side runs its mouths into one unbroken stroke down the lot's frontage.
A bay is drawn on three sides now. The mouth is the end a car crosses, and which end that is needs no
geometry: the bay's heading points into it, so the mouth is the end behind its centre.

The three are laid end to end and not each to the bay's own size. Laid to size, every one of them stops on
the line the next is *centred* on: half a stroke of each corner painted twice — and paint is a multiplying
tint, so that reads as a bright square — and half of it not painted at all. The sides own both corners,
running to the head stroke's far face, and the head runs between their near faces. `ABayCornerIsPaintedExactlyOnce`
is the gate, and it samples a quarter of a stroke either side of the two centrelines that cross there, which
is the ground the old overlap stood on and the ground the old notch left bare.

Laid to the bay's size the outline also missed the lot it stands on, on all four sides. The bays fill their
lot exactly, so every outermost line of a row stands on the lot's own edge — and a line *centred* on that
edge hangs half its width over it, onto the walk or across the kerb line the lot's edge is against, while
the mouth ends stopped short of the same line by whatever the town's arithmetic left over. Neither slip is
worth a figure of its own. **A stroke within a line's width of the lot's edge is laid against that edge,
inside it**, which is the tolerance `LotFrontage.FrontsTheKerb` is already measured by; a stroke further in
than that is one between two bays and stays centred on the boundary the two share. The reach is the bay's
own line cast at the lot's rectangle, so what the paint ends on is the lot's coordinate rather than a
second measurement of it.

That left the kerb line breaking a hand's width before the lot's own paint began, and the strip of bare
tarmac between the two is at the mouth, where a driver is looking. The frontage was the lot's *centre*
projected onto the road plus the reach of its rectangle in the road's direction — exact on a straight road
and nowhere else, because a corner of a rectangle beside a bend does not stand abeam of the metre that sum
names. `RoadFrontages` projects the four corners themselves now and takes the least and the greatest, which
is the same measure the strip that breaks the line is struck at.

**The last of the gap was that the two lines were laid to two different edges.** A lot is a rectangle and a
kerb is a curve (`GEN-4b`), so a lot's mouth edge is a chord of the carriageway's and stands up to that
chord's sag off it — and paint ended on the rectangle stops that far short of the kerb line it is meant to
turn into. What the road owns is now asked of the road: `ReachToTheKerbM` answers where a stroke's own line
crosses the carriageway's edge, each stroke asking for itself, because two lines a bay's width apart cross a
curve at two different points. The reach it will grant is bounded at half a bay's length, which is the
figure that cannot drag a stroke standing behind another row of bays.

The break in the line is over the lot's **mouth** and not over the shadow its whole rectangle casts, which
on a lot standing askew to the kerb runs past the paint by the depth times the skew — `LotFrontage` carries
both, because the cuts `ParkingSections` sets back still want the shadow. And it stops a line's width short
of either end: the kerb line runs to the far face of the outermost bay stroke, which is the same end-to-end
rule the bay's own three strokes are laid by with the kerb line as the fourth, and it is what fills the
corner square that a line ending exactly on the lot's corner leaves bare. The gate is
`EveryStrokeAtTheMouthOfAKerbedLotEndsOnTheCarriagewaysOwnEdge`, and it is asked of the paint rather than
of what the paint was laid to. Overlap, where the ends meet, costs nothing: the ground carries no blending
and its textures are
anchored to the world, so paint over paint is paint — only a gap is visible, which is why every one of these
ends reaches a chord's sag past the line it meets rather than stopping on it.

## 2026-08-25 — the right of way is a rank on a stretch, and it takes claims and nothing else

Two crossing movements each read the other's ground and each were cut at it, so a junction went to
whichever of them asked first — and "first" is the order the rebuild happens to walk the cars in. A car
turning across the oncoming stream could therefore take a box off the traffic going straight simply by
reaching it a tick earlier, which is not a rule anybody could state.

The alternative considered first was a table of pairs — this movement gives way to that one — which is the
verdict TER-5c exists to avoid: it answers *may I go* for a whole junction at once and says nothing about
where. What is stated instead is a **rank carried by the stretch**, taken from the turn the movement makes,
so the comparison happens exactly where two pieces of ground meet and nowhere else.

**What made it safe rather than merely one-sided is that it takes a claim and never a body.** A claim is
already the town's word for ground somebody has not reached and is not committed to, so it is precisely the
ground that can be handed back; a reservation is the road a body needs to stop in, and taking that would be
a licence to drive into whoever holds it. The one hole in that was a car past the point it could stop short
of its box — its ground there is still a claim, and it is going in whatever anything says — so a committed
car lays the same claim at a rank nothing outranks. That is why `CommittedToTheBox` is a field on the car
rather than a stopping distance worked out a second time where the claim is laid.

**Revocation is then the same fact read the other way**, and it is bounded the same way: a crossing already
taken is given back when something with the right of way over it asks for the same ground, and only while
this car could still stop short. The alternative — recomputing whether the crossing would still be granted —
is the thing the crossing state exists to avoid, because it moves under a car that is merely slowing down.

**Both halves of the exchange happen inside one walk, and the tick they land in ends with both cars holding
a movement.** The weaker one asked first, was given ground nothing had claimed yet, and had it taken off it
a few cars later; it gives the crossing up on its own next ask, which is where revocation lives. So *already
crossing* has to be measured from before the walk and not from the fleet the tick leaves behind — read the
second way, the pair reads as a car waved into one that was crossing, and the one that was crossing had not
been given anything at the moment the other was. `NothingOnTheApproachIsGivenGroundAnotherCarIsCrossingOn`
holds the previous tick's crossings for exactly that, and the exemption it makes for the losing side is
counted and asserted (`GivenUpToAStrongerMovement`), because an exemption nothing exercises excuses anything.
The shipped towns pass that claim either way round; it is any nudge to the parking sections — the metre a
lot's frontage begins at moving a hand's width — that puts the two grants of a pair in the same walk.

## 2026-08-25 — a walker's refusal is written into the book, because a right of way nobody can see is not one

The pedestrian priority at an uncontrolled zebra had nowhere to live. A walker refused the band it asked for
simply waited, and the traffic learned nothing: the only thing a driver ever saw was a body already on the
paint, which is the one case where giving way is too late to be a courtesy. Spending the kerb patience and
stepping out regardless works, and it is what the town did — but it makes the walker force every crossing
and puts a body in front of traffic that had no warning.

So the ask itself goes into the road's book, as a use of its own. It is in no mask: no grant is cut at it,
because a cut is what a *body* is worth and this is somebody on the pavement. What it does is put a stop
point in front of the driver — the same stop a body on the paint produces — and a body stopped short of a
crossing already holds none of it (TER-4c.1), so the band frees itself on the next tick and the walker takes
ground the traffic gave up rather than a gap it found.

**The safety of it is the stop's own bound and not a rule beside it.** What a car asks for is never less
than the road it needs to stop in, so a car too close to the paint keeps it, the band stays refused, and the
wait lasts another moment. Nobody is waved in front of a body that could not have stopped for them, and
nothing had to be written to say so.

## 2026-08-25 — an inline junction's crossing is laid across the lanes at the node

The one thing TER-5b says an inline junction exists for — to carry a mid-block crossing — did not work. The
paint is laid on the node itself, which is further from every lane's end than the paint is wide, so the
projection that puts crossings onto lanes found no lane for it: no driver slowed for it, and a walker
standing on it wrote nothing into the road's book and was invisible to the traffic. Every such crossing in
the shipped towns was lit, and the lights hid it.

It is laid across the lanes that meet at the node now, each at its own end — the arriving one at its length,
the leaving one at nothing — which is where the paint actually is, since an inline junction paves no ground
of its own. The fallback is taken only where the projection found nothing *and* the junction admits no
turns; anywhere else a crossing is set back onto the arm it approaches and is found where it lies, and laying
it on the node regardless would paint one crossing across every arm of a crossroads.

## 2026-08-24 — the town's furniture is a use of its own, and not an occupant number

A bollard in a lane went into the book as an `Obstruction` belonging to `Nobody`, and `Nobody` is also
the integer a query names when the asker is not in the book at all — a walker at a kerb, a body about to
step off one. So the two questions the walkers ask about traffic, `AnyTrafficOver` and `BehindBody`, were
skipping the town's furniture because the exclusion they asked with happened to name it. The answers were
the ones a town wants: a walker does not wait for a bollard, and a bollard is not something that stopped
for it. They were reached by an argument of one question deciding the answer to another, and a query
written next with the same exclusion and a wider mask would have dropped the furniture where TER-4c says
it must be read.

It is `LaneUse.Furniture` now, which puts the fact in the masks where every other such fact already is:
in `Bodies` and in `Spoken`, so a driver's grant is cut at it like anything else on the lane, and out of
`Traffic`, so whoever asks what is **coming** down a lane is asking about wheels. Nothing about the town
moved; what moved is where the answer comes from.

## 2026-08-24 — a road may be cut where nothing crosses it

`RoadCuts` cut a road at the junction discs it passed through and at nothing else, and the node count was
the plan's junction count. A car park wanted a node of its own (`GEN-4h`) and is not an intersection: no
arms meet there, nothing crosses, and taking a disc's worth of ground out of the street for it would be
a box invented to hold nothing.

So a cut is now either a disc's bite or **a point** — the same list, the same sort, the same stretches
between consecutive entries — and the nodes a slice above asks for are numbered after all the plan's
junctions, so nothing is renumbered. What reads the graph did not have to change: a place is a node with
two lanes in and two out whose joins have no length, and the one thing that had to learn about it is that a
join of no length is not a movement to be taken.

**The cut is asked of the plan and never of the graph** (`ParkingSections`). Where a road is cut is what
makes a lane, so a construction that read a lane to decide where to cut would need the graph it is
building; a lot's frontage is its own rectangle projected onto the road's centreline, which is the measure
the junction discs are already taken against.

**It gives way to what the road already carries.** A cut that would leave a stretch too short to drive is
dropped, and so is one that would land on a zebra or a bar: a lane end inside a crossing splits the approach
from the paint, and a driver then first hears of the crossing on the lane *after* the one it is braking on.
That was not a guess — it was `CarsCrossTheirZebrasAtCrossingPace` failing on River at 18 m/s against a
pace of 8.

## 2026-08-24 — the table of crossings is indexed by way, so a way laid off a junction can use it

The table said which *movement* took ground off which, and a movement was a turn slot. That was exactly
right for as long as the only ways that could overlap were the joins through one box: the lanes hand over
clear of the disc (TER-5d), so a join is never driven over a lane and a lane never needed a row.

The way into a parking bay is the case that breaks it. It leaves its lane part-way along, sweeps the lane
running back the other way and ends off the carriageway — so what it takes ground off is a **lane**, and a
section that could only name a turn slot had nothing to name. The alternative was a second table for bays,
read in a second branch of the one query that cuts a grant, which is the duplicate SIM-7 is about and is
the one place it would have been got wrong: where a car park meets a street.

So a section names a **way** and the table is laid over every way the book numbers. Every lane's row is
empty on a street with no car park on it, which is nearly all of them — the walk over an empty row is a
bounds check, and it buys the one index in which a junction's join and a bay's way in are the same kind of
thing. `LineOverlap` is the measurement itself, lifted out of the road graph so that the ways at a bay are
measured by the code that measures the joins, and not by a second implementation that agrees with it until
one of them is changed.

**What it cost was the whole-way fallback.** Two lines crossing square can have every sample of one fall
outside the clearance while the other's fall inside, so a crossing found from one side had to be given
*some* interval on the other; between two joins a dozen metres long, "the whole way" was near enough. A
bay's way in against a two-hundred-metre lane, it was the whole street. The missing end is now the found
one's shadow — the samples of the other line standing nearest it — and where no sample of it stands near
enough, the crossing was an artefact of where the samples fell and is dropped.

## 2026-08-23 — a template holds the ground it sweeps, and not the pose it is passing through

A car driving geometry of its own — a recovery straight, a bay exit, a swerve — was in the book as the
footprint it stood on and nothing more. The path it was about to drive down had been walked before the line
was laid (`GroundAhead`, the desk's own check) and was then **left open**: every other driver read it as free
road, was granted it, and could come to rest in it while the manoeuvre was still a second from arriving.
Odesa's soak found it as two wrecks a minute — a car reversing at manoeuvring pace into a driver that had
stopped inside the straight, itself pinned by the reverser's whole-join claim and unable to move.

So a body that is not driving a route is laid over **the whole sweep its line has still to make**, from where
it stands to where that line ends, on every way that sweep runs over. TER-4c.1's "and then it is the asker's"
now covers a template as it covers a lane: walked before it is laid, held for as long as it is driven, and
re-laid from the body every tick so nothing has to be released.

**Read from both ends and laid once.** Which ways a sweep is over is a question about a pose, and the ways
under one end of it are regularly not the ways under the other — a straight of the reverse bound is twice the
length of a body. Both ends are asked, each lays the whole interval, and `LaneOccupancy.AlreadyHolds` is what
keeps one body to one stretch of one way (TER-5c.2) rather than the order the two readings were taken in.

**A bay exit is no longer one of them**, and that is the shape the rest of this should take. The way out of a
bay is a way of the book, so its driver is a driver on a way: what it holds is a reservation along that way
and the crossings on it, and no sweep is read off its geometry at all
([parking](../../parking/docs/decision-log.md)). What is left here is the recovery straight, the swerve and
the legs of a turn on the spot — the lines the town did not lay.

**What it does not do is make the reverser see.** A car on a template still senses nothing — its context is
empty and its authority infinite — and that is unchanged: what stops the collision is that nobody else is
granted the ground, which is the same mechanism as everywhere else and not a second one (SIM-7).

Odesa after it, at a tail share of 0.6: **0 wrecked, 46 touches, 3 stuck ticks, 118 mm at the deepest**,
against 4 wrecked, 56 touches, 15 stuck ticks and 827 mm before.

## 2026-08-23 — the tail keeps a share of the margin, not the whole of it

The margin sits at both ends of a reservation and the two ends are not paid for by the same traffic. In
front it is this car's own cover, and it costs the car that keeps it. Behind it is what the book's
one-dimensional reading owes the width it threw away, and **it costs whoever comes up behind**: a stretch
begins there, so every metre of it is a metre of road the follower is queued out of and a metre of a join
that reads taken after the body is off it.

So the tail keeps a share of the figure rather than all of it — `DrivingFigures.TailMarginShare` at 0.6,
`SimConfig.CarTailMarginM` — and `ReserveFromM` is `nose − length − tail margin`. Everything downstream
follows from that one site: `PastOnTheCrossing` is still the near edge of the reservation, the queue still
settles at whatever the leader holds behind itself, and the front of the ask is untouched.

**What it changes is the standing gap.** A queue at rest now stands at 1.2 m rather than 2 m, because the
gap at rest was never the follower's to choose — it is the ground the leader holds. `StandstillGapInCarLengths`
still sets the floor of the *figure* and the tail reads its share of that floor.

**What it cost was wrecks, and they were not the margin's.** Odesa over the measured minute, everything else
held:

| tail share | tail margin | wrecked | touches |
|---|---|---|---|
| 1.0 | 2.0 m | 0 | 50 |
| 0.8 | 1.6 m | 4 | 55 |
| 0.6 | 1.2 m | 4 | 56 |

The step is at the first metre under a body's width and does not deepen below it, which is the shape of a
threshold being crossed rather than of a margin being spent — and the wrecks, read one by one, were every one
of them a back-off reversing into a car that had stopped inside its straight. What the shorter tail did was
put a standing car 0.8 m nearer, which is the difference between a blind reverse that reaches and one that
does not. The hole is above: a template held no ground. With it closed, **0.6 runs 0 wrecked and 46
touches** — fewer than the 50 the full margin gave before any of this.

## 2026-08-23 — one body, one stretch: the margin is part of the reservation

A car in a junction held a claim of its own behind its tail — the release margin — so that whoever crossed
there met the ground a swinging body might still be on. It worked, and it was a second stretch of one way
for one body: two occupants to every walk of the join, two bars across the road on the overlay, and a
trailing block behind a car that had visibly left it. It was also only true *in junctions*, though nothing
about the reason is: the book throws away the width of the road wherever a body stands, not only where two
lines cross.

So the margin is where it always belonged, in the body's own reservation, on every way the body is on:
`ReserveFromM` is now `nose − length − margin`, the crossing claim covers only the ground ahead of that, and
`PastOnTheCrossing` is the near edge of the reservation rather than a second sum over the same pose. The
release figure and the follower's standstill gap were the same 2 m answering two questions, and they are one
figure now (`SimConfig.CarBodyMarginM`) — **the ground a body keeps around itself**. The queue arithmetic is
unchanged by construction: what the follower used to subtract for itself, the leader now holds.

**The measurement is kept as a floor rather than as a second figure.** The margin is `max(a body's width,
the standstill gap)`, so a fleet tuned to queue closer than the soak's floor gets the floor: nothing at the
tail was 2 wrecked and 923.6 mm of interpenetration, half a width 2 wrecked and 263 touches, a full width 0
wrecked. Reusing one figure for the other is what an earlier decision refused, and rightly — what makes it
safe here is that the *union* is taken rather than one being quietly read as the other, and the figure's own
doc-comment carries both questions.

Odesa's soak after the move: **0 wrecked, 53 touches, 15 stuck ticks**, and every map's deepest body is a
walker in a crowd rather than anything that drives (`--bench soak`, which now names the body a peak belongs
to as well as the one that stayed stuck).

**Two things had to move with it, and each was a defect the suite found.**

- **In front stopped meaning "its near edge is".** Every stretch now begins a margin behind its owner, so
  the near edges on a way run one margin out of step with the bodies on it, and `NextSpokenFor` was skipping
  a stopped car whose ground began behind the asker's own — granting a driver road through a body it could
  see. It is `StandsToM` that answers it now: **a stretch whose body has not reached this far is behind**,
  which is the same test `AheadBody` already used, and the cut is still taken at the ground's near edge.
- **A body that is not driving its movement lays no claim.** Off its line or under a hand, a car asks for no
  road, and it used to claim the runs of its join whole — over the very metres `LieInTheBox` was already
  holding it on as an obstruction. One body, two stretches, in two measures that could not agree. What
  refuses the traffic crossing such a body is now the ground it is lying on, which is every join of that
  junction it is under and the wider answer of the two.

## 2026-08-23 — a zebra is ground with a lane under it, and not a thing the traffic holds

The same decision as the junction one below, taken over the other network, and it was left half made: a car
crossing a zebra wrote **a second copy of itself into the pavement's book** — its lane's band of every way
that crossing is made of — so that a walker's grant would be cut by it. One body then held one piece of
ground twice, under two names, in two books whose answers could differ; the overlay drew the copy as a wash
over the whole paint, which is a picture of a crossing being *shut* rather than of a lane being somebody's.

It is a lookup now, and it points the way the walker is going. `CrossingBands` already carried the band each
lane covers of each crossing way, measured once when the town is laid, so nothing new had to be worked out:
a body asks the road's book for the band in front of it, and where the answer is no, the same band's near
edge is where its walk is cut (`WhereTheWalkRunsOut`). The refusal is made once and spent twice —
`MayStepOnto` is the ask, `PersonFleet.RefusedWay` is what it answered, and the grant over the walk is that
same answer in the other network's metres. **It is the answer that is carried and never the question**: a
body past its patience is granted a band the book would refuse, and a grant that re-asked instead of reading
would hold that body at the edge of ground it had just been given.

`TakeTheCrossingsAhead` and its bound on the pavement's book are gone with it, and three tests hold what
replaced them: `NoCarIsEverInThePavementsBook`, `ABodyRefusedALaneIsGrantedNoFurtherThanItsEdge` and
`ABodyRefusedALaneWalksIntoItOnceItIsGranted`.

**The kerb stopped being a special case with it.** A body was allowed to take the band in front only while
it stood on a pavement; half way over it was refused its own next lane and held by the car's copy instead.
Two arrangements for one question, and the answer differed by which side of a kerb line the asker stood on.
It is one question now — the same band, the same book, wherever the body is standing when it asks — and
`PER-15` reads accordingly.

**A body holding the lane in front of it was tried once and rejected, and what makes it right now is the
reach.** Held from the moment the body entered the near lane, a walker stopped the traffic in the far one
for the whole width of the crossing; and with the car's band laid after the walker's claim, the claim won
every race and the band went back to holding nothing. Both are answered rather than argued with: a body
asks for the band its **own ask reaches**, which at a walking pace and a standstill gap is about a stride,
so the far lane is held for the last step before the foot goes down and not for the crossing; and the cars'
reservations go into the book before any walker's claim is checked against them, so the race now goes the
other way — a car committed over the paint cannot be claimed out from under.

## 2026-08-23 — a reservation stops where a rule stops the car

`AskForTheGround` clamped the road a car asks for at the place it is held — a red, a bar, a crossing — and
then **added the margin it keeps in front on top of the clamp**. So every car in the town held two metres of
ground past every bar it stood at, and the stand-off a crossing stops a car at is one metre: a car waiting for a
zebra held a metre of the zebra. Measured on the Test crossroads, a car standing at its own red had its
reservation 0.33 m inside the paint's near edge, and the band a body at the kerb asks about reaches
`PaintClaimM` ≈ 2.9 m either side of the paint's centre — so the crossing read as taken, on the pedestrian
phase, by a car that had stopped precisely to let those people cross. They got over on their patience, eight
seconds later, which made a signalled crossing behave like an unsignalled one.

The gap is part of what the car asks for and is clamped with the rest of it. Nothing about following
changes: a follower's grant already subtracts the gap from the near edge of the body in front, so the queue
spacing is where it was, and the ask only ever shrinks at a stop. What a stopped car holds is now the ground
it is standing on, which is what a stopped car is.

## 2026-08-23 — a car reserves the ways it drives and looks up the ways it is driven over

A movement wrote its crossing points onto **both** joins: a stretch on every other way through the box its
line came near, and the matching runs on its own. Half of that is a body reserving ground it is never going
to be on. On screen it is the whole reason a junction under one approaching car was a fan of teal over
every way through it, most of them movements that car would never make; in the book it is a car holding up
to `MostCrossedByOne` stretches of other people's road, which is what the index was sized for.

It is a lookup now. The table was already symmetric and already carried both ends of every section
([`WayCrossings`](../WayCrossings.cs)), so nothing new had to be measured: a driver looks its own
way up, reads the metres named there **in the other way's own book**, and its grant is cut at the near edge
of the first section anybody is standing on (`WhereTheGroundIsCrossed`). What one car holds on its own join
is exactly what the other finds when it looks — that is the same interval, filed under both movements.

**The crossing claim stays, and it is now the only thing a mover writes.** A driver's road ahead is a
braking distance and no more, which does not reach the middle of a box until it is nearly on top of it; two
cars asking from opposite arms would each look the other's join up, find those metres empty and both go.
The runs of a movement's own join are held from the tick it commits, and that is what the other car reads.

**The grant is the place this had to bite, not just the commit test.** `TheCrossingIsFree` was already
asking about the crossed joins, so the entry into a box was covered either way; what was not was the road a
car is *granted* on the approach — cut only by its own ways, it ran straight through the metres two lines
meet on and the ground of a junction was one car's and another's at once. Cut by the lookup as well, a
reservation stated in one lane's metres means something about the whole town:
`NoGrantReachesGroundAnotherBodyHasOnACrossingWay` is that property, and
`ACarTakesNoGroundOnAWayItIsOnlyDrivenOver` is the half of it that keeps the ways clean.

**A section is a named piece of ground and not the road under the asker, and the walk had to say so.**
`NextSpokenFor` skips a stretch whose near edge is behind the window — right for a driver reading the
occupants of its own way in the order they are in, and wrong here: a car whose reservation entered a join
*before* the metres two lines cross was invisible to the movement crossing there, and both went.
`LaneOccupancy.NextSpokenForOver` is the overlap walk, and the all-or-nothing `SpokenForByAnother` is now
its first answer rather than a second copy of the loop.

## 2026-08-23 — the margin a body keeps is not the clearance the sections are drawn at

They take the same value and answer different questions: one is how near two lines pass before they are
driven over each other (`SimConfig.JunctionCrossingClearanceM`), the other is what a one-dimensional reading
of a two-dimensional body owes whoever comes next (`SimConfig.CarBodyMarginM`). Read as one figure, they read
as one rule, and the reading hid that only the first of them had ever been measured. Kept apart, the second
can be moved without redrawing the sections — which is what the soak numbers on it were taken by.

## 2026-08-23 — a crossing claim is the run less the road, not the run

A car in a box held the crossing points on its own join twice: once as the road it was driving, and once
as the claim laid from the table. On a join a driver was well inside, the two came out as the same
interval to the centimetre — `Reserved 0.00–14.25` and `Claimed 0.00–14.25` of one way, one car. Two
occupants to every walk of that way, two washes to the overlay, and a picture in which a car appears to
have reserved the same ground twice.

Nothing computed a wrong answer, because the two uses are read as one set and their union was right. What
it cost was that the book stopped saying what it says it says, and `NobodyHoldsTwoStretchesOfOneWay` could
not catch it: that test exempted claims outright, on the reasoning that a claim is ground its owner is not
on yet. True of the reasoning, not true of the stretch.

The claim is now the run **less** the reservation, and the exemption is gone — the test asks for overlap
rather than for a second appearance, so a claim may stand beside a body's own road and never over it. The
ask is laid before the crossing for it, since a claim clipped against the stretch the car held a tick ago
is clipped against the wrong metres.

**It is two pieces where the road ends inside a run, and the near one is load-bearing.** A reservation
begins at the tail and a run is given up a clearance behind that, so the metres between them are ground the
body is still swinging over — the clearance that stopped River's soak wrecking cars. Dropped as
redundant on the first attempt, the give-back test caught it in one run: a car 0.30 m into a join had let
go of the first 0.30 m of it. So one run may split, one reservation being one interval, and the slot budget
is `MostOwnRuns + 1`.

**And the far half of the give-back is not observable while a car is in the box**, which the counting in
that test had been hiding. The runs of a busy junction merge into one spanning the whole join, and a car
advances its chain — dropping the crossing whole — long before its tail is a clearance past the far end of
that. What moves is the near edge, so that is what is now counted and asserted from both sides.

## 2026-08-23 — a movement holds the crossing points on its own join, not the span between them

The near side of a crossing was held as one interval from a movement's first crossing point to its last,
and held whole for as long as the movement was. On a wide box that is the whole of a car's own way through
it: a straight crossing the two turns off the side arms shut every metre between them, including the middle
where nothing comes near, and it shut them from the tick the crossing was taken until the tick it was
dropped. A car whose own movement crossed only those middle metres was refused ground nobody was ever going
to be driven over, and a car half way through kept holding the metres behind its own tail.

The near side is now the same places the far side is, merged where two of them overlap so that one body
cannot appear twice over one metre, and given back on the same test: a run whose far end is behind the tail
is gone, and the near edge of the one the body is in walks up with it. What is under the body on its own
join is the car's reservation, which was always there and carries the length and the swing the interval was
being asked to stand for.

## 2026-08-23 — a body is on a way across its band and along it

Whether a body stands on one of the town's ways was a lateral question only: project the body onto the
way's line, and compare the offset across the line against the band. A projection is clamped to the way's
own ends, so anything lined up with a way's end answered at that end however far up the road it stood —
and a body ten metres past a join measured no offset at all. What that put in the book was cars lying on
joins they were nowhere near, most visibly for a body standing in a junction, which is asked of *every*
join at the node: one car in a box could shut movements on the far side of it that nothing was near.

The test is now taken along the line as well, against how far the body itself reaches that way. Inside a
way the nearest point is square to the line and the second comparison decides nothing; it bites only where
the clamp did, which is exactly the case that was wrong.

## 2026-08-23 — a junction is committed to at the rate the car actually brakes at

Two figures decide the life of a crossing — the reserve distance a car takes one up at, and the point past
which it keeps one whatever else is holding it up — and both are a stopping distance. They were the only
stopping distances in the town read off the pedal's own cap, `Car.BrakingMps2`, while every stretch of road
a car holds is sized by `CarFollower.BrakingMps2`: the same cap against the tyres, at the braking margin,
on the ground under this car.

The cap is the larger figure, so both readings erred the one way that costs. A car past the point it could
stop at was still judged able to, so it gave the sections back for a bar it was going to cross anyway — and
in between that tick and the next, when the book hands them straight back as a fact, they read free to
whoever crosses them. And a car took its crossing later than the tick it committed on, which is the window
two cars commit in together. Neither figure noticed wet ground at all, where the gap between the two rates
is widest.

Both are `StoppingM` at `CarFollower.BrakingMps2` now, which is the same call the reservation pass makes a
few lines earlier. `NothingStoppedAtARedHoldsAWayThroughTheJunctionBeyondIt` had the third copy of the
formula and read the cap too; it asks the town's own figure now, which is what caught this.

## 2026-08-23 — a crossing is given back where it is passed

Ground taken for a junction was taken whole and held until the car was out the far side. On screen that is
a car half way through a turn still washing the corner it came in by, and in the book it is the traffic on
that corner held off a movement nothing was ever going to be driven into.

A section is a *place* two lines meet, and a body passes a place once. The table now carries both ends of
each of them — the metres of the crossed join and the metres of the crossing one — and a car gives a
section back once its own **tail** is a clearance beyond it. Per tick a car is crossing on: **7.93 → 7.31
stretches and 49.4 → 46.3 m** held on Odesa, **5.94 → 5.43 and 40.7 → 37.9 m** on River.

**The tail alone is a metre too eager, and it wrecks cars.** A section is drawn where two *lines* pass, at
exactly the width that makes the bodies on them touch; what has to be off it is a body, which on a turn is
off its own line by the road's tolerance and swings wider at the back besides. Released at the bare tail,
River's soak came back with two cars wrecked and 421 mm of interpenetration against a hundred. Released a
margin later (`SimConfig.CarBodyMarginM`, which the table above is sized apart from) it is back to the
100 mm the design without any release gives.

**The car's own reservation is not released with them.** It looks like the same fact and is not: a crossing
point is a place, and the only question about it is whether this body is over it, while the reservation is
the road the car is *driving*, carrying its length, its swing and wherever it comes to rest. Slid forward
with the tail, Odesa's touching count went from 51 to 232 and its reactive rungs from 10 to 27 with nothing
else changed. It is held whole and given back with the crossing.

The table's own measurement is symmetric now, which it had not needed to be: a pair used to be recorded
whenever the first line was found near the second, with the reverse measurement taken but not required, and
a section whose other end came back empty has nothing to say about when the car is past it. Both directions
are asked, and where only one finds anything the missing end is taken to be the whole join — which is that
section held to the far side, as it was before.

## 2026-08-23 — a junction is refused by ground, not by a verdict

`JunctionRegistry` counted the cars making each movement and `RoadGraph.Conflicting` said which movements
could not both be made. Nothing drew either of them, so what actually stopped a car at a junction was
invisible: the overlay showed the road's book, and the road's book was not the thing deciding.

Worse, the relation was almost complete. Measured over Odesa, **an average movement conflicted with 81 %
of the other movements at its junction**, and one car in a box refused **70 %** of them. Three quarters of
that was asserted rather than measured — a shared entry lane, a shared exit lane and the turn-around the
road still carried then each came back true on sight, and 416 of Odesa's 1472 movements were turn-arounds,
every one of which shut its whole junction on its own. So a car entering a junction stopped very nearly
everything else, which is neither what TER-5c says nor what a junction does. (The turn-around itself is
gone from the road since — TER-5f — and the table lost those rows with it.)

It is ground now, and only ground. The table says, per movement, **the section of every other join its own
line is driven over** ([`WayCrossings`](../WayCrossings.cs)), measured on both sides at the same
clearance the old relation used. A car is refused by whatever is standing on those metres and by nothing
else, and it can be looked at on screen. The registry, the relation and `CarFleet.HeldMovement` are gone;
the field left on the car names a reservation rather than a permission.

Three things had to be true of it, and each cost a defect to find:

- **A car crossing must hold its own join.** Its own road ahead is a braking distance and no more, which
  does not reach the place two lines meet. Two cars asking from opposite arms each looked at the other's
  join, found those metres empty, and both went. Every section carries its own end of itself, and that is
  what the other car reads.
- **A car making the same movement is not an answer.** It is on the same line over the same ground,
  so read literally every queue at a junction refused its own second car. What holds one off the next is
  the road each was granted (S-2a) — a headway, not a crossing.
- **The two out of one lane, and the two merging into one, are not in it at all.** Both were in the old
  relation and both are the duplicate SIM-7 is about: a follower is cut by the car in front, and a merge
  is cut on the lane merged into. Measured, they are simply not driven over each other.

Each design's own refusal test, asked of every node-tick somebody is in a box: the mean share of a
junction's movements one car refuses falls **70 % → 64 %** on Odesa, **67 % → 49 %** on River and
**67 % → 50 %** on the fixture. The static density falls **81 % → 52 %**, and movements that shut a whole
junction on their own **416 → 0**. A whole node being shut stays where it was, under a tenth of a per cent
of those node-ticks either way.

## 2026-08-23 — a stretch runs out at the box's near edge

`WaysAlong` is the one walk that turns a run of a car's line into the town's own ways, and all three
questions a driver has go through it: the road it reserves, the road it is granted, and the road it can
see. It stopped walking when the *next lane* began — and the next lane begins on the far side of the
junction, because the join is the ground between one lane's end and the next one's start.

So nothing was laid on a junction until the stretch reached clear across it. A car approaching a box
reserved none of the box, was granted its road as though the box were empty, and could see nothing
standing in it; its own block appeared half way through the turn, once the tail was inside and the far
edge was within a stopping distance. The guard is the near edge now — `ends[index]` — and the overlap that
follows was already written to clamp, so a stretch ending anywhere inside a box lays the part of it that
is.

## 2026-08-23 — a way through is kept until it is given back

The claim was not a claim. Every tick recomputed whether the car was *entitled* to the movement it was
already making, from the two figures that decide whether a fresh one may be taken — is anything queueing
between here and the boundary, and is the boundary inside a stopping distance — and told the driver the
answer as `BoxIsOurs`. Both figures move under a car that is merely slowing down: a stopping distance
shrinks with the square of the speed, so a driver easing off found its own junction refused it, `P-8`
failed to `P-6`, and the pair swapped back on the next tick for the length of the approach.

Worse was the half that never moved. Nothing wrote the field away. A car that claimed at speed and then
stopped at a bar went on holding the ground until it crossed — so the arm the phase had just given the
green to was refused by the arm sitting at the red, which is the phase's own decision undone and exactly
the duplicate SIM-7 is about.

It is one state now, held by the car and laid from the car. It is taken only by a driver nothing but the
box is holding up, and given back the moment something else is: a bar showing anything but green, or
traffic near enough that the two of them at rest would leave this one short of the boundary. `BoxIsOurs`
is then not a second opinion — it *is* whether the field is set.

**Past the point it could have stopped at, it is kept whatever anything says.** Ground given back there
is handed straight back on the next tick, because a car inside a box is standing on it; and in between
those two ticks the sections read free to whoever crosses them, which is a car waved into a junction
somebody is already in.

`LaneOccupancy.Withdraw` is what makes the giving back mean anything inside the tick that does it: the
book is laid from the cars once, before any driver decides, so stretches left standing after their holder
let go would refuse everything that crosses them until the next rebuild.

## 2026-08-23 — a body in a box is on the joins, not on a lane

A junction's ground was defended by one thing, and that thing only knew about cars that are driving. A
wreck, a car under a hand, a body shoved into the middle of a crossroads — none of them is crossing on
anything, and `PlaceTheCrossing` gives back the ground of anything nobody is driving. So nothing had
anything to say about them, and the traffic crossing the box was granted the ground they were lying on.

The book should have caught it and could not. A standing body was laid onto the lane it lies nearest, and
a body in a box is past that lane's own setback (TER-5d) — where the lane's line runs on under a movement
rather than under itself, and no driver's line is laid over it. The stretch went somewhere nothing walks.

It is laid on the joins now: both ends of the nearest lane are asked, and every join of the junction the
body is lying under gets its stretch, on the same band test a lane gets. That is what makes the whole of a
junction's ground answer to the same book as the rest of the road (TER-4c), and it is one mechanism rather
than two — a body in a box refuses what crosses it in the same way a car crossing does.

## 2026-08-22 — a body is one stretch of a way, never two

A driver under way used to be laid twice on the same ground: a `Travelling` stretch for the body, and a
`Reserved` one from the same tail out to where the car could stop. The two shared a near edge exactly —
`noseM - LengthM` is the axle less the overhang — so the reservation always contained the body, and every
walk of a way counted one car as two occupants.

Nothing computed the wrong answer, because the masks kept them apart: `Travelling` was in the body
questions and out of the spoken-for ones, `Reserved` the other way round. What it cost was that the book
could no longer be read as what it says it is. The overlay drew both, so a car sat under a double wash of
its own colour and a reader had to know the model to know that was one block; and every query that walked
a way had to be written knowing which of a car's two entries it would meet.

It is one stretch now, carrying two far edges: `ToM` is the ground taken and `StandsToM` is where the body
ends. A question about where somebody **is** reads `StandsToM` on every slot alike — `AheadBody`,
`BehindBody` — and a question about what ground is **spoken for** reads `ToM`. For everything that is only
a body the two are equal, so the distinction costs nothing to lay and nothing to ask.

`LaneUse.Travelling` went with it: the walking side had already settled this shape — a walker's ask begins
at its own back and it was never given a body stretch — so both networks now name the same thing
`Reserved` and are laid by the same call. `NobodyHoldsTwoStretchesOfOneWay` holds it, over both books, with
a claim the one stretch a body may hold beside its own — a claim is ground its owner is not on yet.

## 2026-08-22 — neither book is one roster's

A walker on a crossing used to be in the road's book and in none of the road's questions. That was
deliberate and it was half right: a walker read as an obstruction is a walker `E-4` crosses the centreline
to drive round, and a walker read as a reservation cuts the grant of a car three lanes away. So it was put
in a use nothing queried, and the crossing rule read it through a keyhole.

What that cost was invisible until the ray went: **nothing cut a driver's road at a body standing in it.**
The car stopped, but it stopped because a separate rule computed a stop point off the paint — and a person
standing in a lane with no paint under it cut nothing at all and was seen only by a cast.

Both halves are now the same mechanism. `OnFoot` is in every query a grant is taken against, and it carries
its own reading (`HeadwayKind.Walker`) so the one thing it must never be — something to get past — is a
property of the reading and not of which query happened to skip it. Its mirror is that **a car takes its
stretch of the zebra it drives over, in the pavement's book**, so a walker's grant is cut by traffic on
exactly the terms a driver's is cut by a body on the paint.

One thing fell out that had to be built rather than found: **an occupant is an index into one of two
rosters, and the stretch has to carry which.** Inferring it from the book was fine while each book held one
roster; the first walker whose index matched a car's was read out of the wrong fleet. It also broke
exclusion — a car excusing its own stretch by number excused a walker's too.

The walker's give-way arithmetic went with all this. It asked how long something would take to reach the
paint, and the reservation *is* that arithmetic, already done, from fresher numbers: a car far enough away
to stop for this body is committed to ground short of the crossing, and one that is not, is not.
`GiveWayReachWalkSeconds` went with it.

## 2026-08-22 — a crossing is ground, and it is taken by the band

Both sides of the paint took it whole, and both readings were wrong in the same way — **a zebra was treated
as a unit when it is a strip of carriageway a lane at a time**.

The car's half was **inert as well as coarse**. A car's stretch of a zebra was laid from the crossing way's
own start, and `NextSpokenFor` skips a stretch whose near edge is behind the asker's — a walk enters a
crossing lane at the mitre hand-over, measured never nearer than 1.03 m on Odesa, so every one of those
stretches sat behind every walker that could have been cut at one. Measured over a minute of Odesa:
**562 782 reservations laid, 40 469 walker asks reaching a crossing way, and not one grant cut.** What
actually held a walker at a kerb was the give-way question on the road's side, and the pavement's book was
paying for a stretch per car per tick to hold nothing.

The walker's half was live and it over-held: a body on the paint took the band of **every** lane the
crossing was painted across. Of 162 410 such stretches in that minute, **127 976 were on lanes the body was
nowhere near**, and **6 003 of the 7 921 crossing stops a driver made were for somebody who was not in that
driver's lane at all** — 3 226 of them for somebody more than a whole lane clear of it.

So the projection that was missing got built ([`CrossingBands`](../../foot/CrossingBands.cs)): where each
lane's band falls on each way of each crossing, turned over from the projection that already put the paint
on the lanes. **What either side has of a zebra is a band and never the whole of it**, and the near edge of
a band is a place on the ground rather than the start of a way, which is what makes a grant cut at it at
all.

**The patience is spent on a named lane and given back when the body is standing in it** (PER-15). Reset
the tick the traffic gave way, it buys one tick of ground and starts the wait again — a body stuttering at
a lane's edge for as long as the street is busy. Latched until the far kerb instead, a body that waited
once holds the lane in front of it for the rest of the crossing, which is the whole-zebra picture this was
opened on, back again.

What it cost: a driver reads a walker in its lane less often, because a body in the next lane is no longer
in this lane's book — `CarLookingInATownTests` moved from the fixture to Odesa for that reason, the reading
being a matter of being the car in that lane rather than of the rule still working.

## 2026-08-22 — braking has its own margin, and it is nearly all of the grip

`GripMargin` is 0.7, and using it for braking as well as for cornering put the profile's planned stop at
13.1 m/s² against the 21 the tyres and the rolling drag actually delivered. Every reservation on the road
is sized by that planned figure, so a car held half again as much street as its own stop was going to use.

Braking now has `BrakingMargin`, at 0.95. A corner is held for as long as it lasts and the margin there
covers a bump, a camber and the wheel still being turned; a stop is aimed at, straight, and over in a few
seconds, and what is left over is the rolling resistance — which the tyre model spends outside the
traction budget and hands back as a stop shorter than the plan. `TrackFiguresTests` is what holds the two
together: every slowing on the proving ground, into every one of its five shapes, comes out within a
quarter of the planned rate.

Corner speeds are untouched, which is the point of the figure being its own.

## 2026-08-22 — a reservation is the ground a car is committed to, not the ground its plan would need

A driver used to ask the book for its whole stopping distance from the speed the profile was driving
towards, so that a car stopped in a queue asked for the road to pull away into rather than the nothing a
standstill needs. On a town street the profile's answer is always some corner or the end of the assembled
line, and the ask came out at a few tens of metres. On open road it is the car's own top speed — 75 m/s,
215 m of it — and one car held a quarter of a kilometre of empty straight it was doing a third of that
speed on.

The ask is now what the car cannot undo: one reaction interval of ground at the fastest that interval can
leave it doing, and a stop from there, with the profile's figure as the ceiling. It still leaves the room
to pull away, because a stopped car is committed to whatever its own acceleration reaches in that interval
and what it asks for therefore grows with the pedal rather than with the speed the pedal has produced.

**And a car nothing cut is now held by nobody**, where the grant used to come back as the length of the
car's own ask. That was harmless while the ask was the whole of what the profile planned for — the grant
inverted to exactly the speed the profile had already chosen, and bound nothing. Against an ask the car is
merely committed to it inverts to the speed one reaction interval reaches, which is under the profile's
figure and therefore binds: a car alone on an empty straight read as `queueing`, behind itself. The grant
is about other bodies, so it is written only where another body cut it.

## 2026-08-22 — a lane end has one setback, not one per turn

Each turn used to be set back into its two lanes by exactly as much as its own arc needed to reach the
junction's corner radius, which is the least ground taken and the tidiest straight. It also meant a lane
had no single end: a straight handed over to the box at the lane's own end, a right-angle turn out of the
same lane handed over up to 4.5 m earlier, and two turns out of one lane could differ from each other
because the cap is half of the *shorter* of each pair. Everything downstream that wants to name that
boundary — the assembler cutting the chain, the occupancy book, a debug layer drawing where the driving
changes — had to name a movement to do it.

The setback is now the lane end's, and it is the widest its own movements asked for (TER-5d). Straights
give up some metres of lane and drive a short straight across the box for them, which costs nothing
geometrically and a little junction ground. Widening runs in rounds rather than turn by turn, because
setting one turn back changes the arc of every other turn through the same lane end.

**Every movement in the reckoning reaches a radius**, because the one that never could — two opposing
lanes a lane's width apart, a semicircle whatever setback you give them — is no longer a movement at all
(TER-5f). Counting it would have pegged every lane in the town at the cap.
