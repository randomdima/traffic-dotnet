# Decision log — roads and junctions

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md) and [claims.md](claims.md); how
a type works is its own XML docs.

## 2026-09-30 — a holder at rest cannot stop short of its own front

**Five minutes of Odesa left a car and a walker standing at a zebra for the rest of the run**, because a car at
rest held the paint on some rebuilds and not others. Its committed ground ran from its front by its reaction
and a stop, so a car settling at a few millimetres a second held a hair past its nose — and at a zebra that hair
is a metre of the paint, which places both walking lanes committed whole (TER-5c.3). On a rebuild the speed came
back at exactly nought the walker had the paint and took a stride; on the next it was refused again, a stride
further out, until it stood on the lane in front of the car that was waiting for it.

**Dropping the hair was tried and refused.** With nothing committed at rest a car let into a box and cut in the
middle of it lost its way out to the car its own body was holding up, and one stood with its nose on the paint
lost it to walkers its body stood in the way of — both gridlocked on the first run. What the hair had been doing
was keeping ground its holder's front already stood in. **So the end metre is committed** (`CannotStopShortOf`):
at rest the metre at the front is ground the holder cannot stop short of, on every rebuild and not only on those
the solver left a residue on. **It is the ladder's reading only** — a call's pass still takes the plan of a car
it passes, and a place a body is put down on is still refused only by ground somebody will cover
(`LaneClaim.CommittedAt`).

**Read over a secondary claim's whole section, TER-5e's second tier was tried as well and refused.** A zebra's
marks are whole, so the section on a lane and the section on a walking lane are different ground; standing in
one was not standing in the other, the car won the meeting on its lane and the walker the meeting on the paint,
and the rebuilds stopped settling (a tenfold rise in plans laid again).

## 2026-09-30 — what a call's pass gets past moving ends where it can stop

**A call passes traffic that is moving** (TER-4c.6, the ambulance log), and a pass is laid for where what it
passes ends: made for a body standing where it stands, the step back would come down in front of a car still
rolling into it. **A moving body is passed to where it can come to rest** (`LaneOccupancy.StopsByM`) — the
furthest its plan on the way says it can no longer stop short of, read off the planned layer and not off the
car — and the pass laid past that cuts its plan, so it stops short. **What a pass gets past no longer holds none
of its ground, for a call's**: a plan of it the pass would lie over is taken only where its holder can still stop
short (`PassTerms.Takes`), since a moving car cut there drives on into the pass. Every other pass gets past bodies
at rest alone, whose plans hold nothing they cannot give back, and takes them whole as before.

## 2026-09-29 — a call's pass is asked on terms, and holds a zebra whole

**A pass was ground nobody plans, whoever asked** (TER-4c.6), so a call's rung bought it nothing: the oncoming
car's reach, a light's hold over the box and the movements crossing it each refused it as a body would. The
owner asked for a car on a call to overtake with its priority claim (the ambulance log). **A pass is now asked
on terms** (`PassTerms`): a call's takes a plan its rung beats and whose holder can still stop short of it, and
every other is asked at no rung and takes nothing — the same ground as before. It is still laid at p0 and never
given up, so ground an oncoming holder can no longer stop short of refuses it, and the spare it is asked with is
what keeps that ground clear through the rebuild it is laid in.

**The paint is claimed whole, and whoever is on it is waited for.** A zebra's walking lanes are ribbons the
atlas reads under a swept body, and not ways the traffic drives, so a pass over one was refused as a pass run
off the road before the zebra was ever asked about; to a call they are road. Laid on the traffic's ways alone, a
pass over the paint held nobody on foot, who would step out in front of it — so it lays the paint kerb to kerb on
every walking lane (`LayThePaintItCrosses`), the one ground off its holder's network it holds, the zebra being
the one ground the two networks share (TER-5c.3). Somebody already on the paint is not held by it — a pass holds
nobody standing on its ground — and neither refuses nor withdraws it (`PassTerms.WaitsFor`); the car stands
short of the paint until it is empty (`TheBodyInThePass`). Held only by a body in its way, as every other pass
is, it could come to rest with its nose across the walking lane of somebody on the paint in front of it, each
waiting for the other. A car already over the paint drives on off it.

## 2026-09-29 — a car backing up plans behind itself, at a rung of its own

**The ground a car backs up over is a plan like any other** (TER-4c.7, CAR-50), asked and answered on the one
comparison (`LaneOccupancy.ReachBack`, `TakeBack`), read down the way from the holder's tail. **It is asked once every
other plan is settled**: it is weaker than all of them, so nothing it could take would be given back, and laid last
nothing is weighed against it later in the rebuild — which spares the cut and the settling a hold running against
its way's metres. **What its holder can no longer stop short of is laid as its own piece**, committed, since a
piece's committed ground is the stretch below a metre and a car rolling back has it above one.

**It takes the plan of somebody queued behind it** — a hold its holder's body cut — wherever that one could still
stop short (`LaneOccupancy.IsQueuedBehind`). Weaker than that too, it was a queue locking itself: the owner saw a
car stand too near a car in front with the lane beside empty, and the car stopped behind it planned every metre up
to its tail, so no car of a queue could ever back up. The plan is cut with the one cut any taking makes, and the
backing hold keeps the pass's spare off the body behind, which is now all that is left between them.

## 2026-09-29 — a movement a pass holds whole is held whole against plans, not bodies

**A wreck standing in a box could never be passed** (TER-4c.6). The pass holds every movement its body is swept
over whole, and the movement the wreck stood on was the car's own, so the pass's ground held the wreck's body and
was never free. Holding a movement whole is about what is let into it — a car crossing the box let in and cut in
the middle of it — which is a question of plans and of other passes. **A body is now read only where the pass's
body goes** (`LaneOccupancy.IsFreeForAPass`, `KeepsItsPass`, `TownWorld.TheBodyInThePass`); a body elsewhere on the
movement is in nobody's way, and a driven one standing there still plans the room to pull away, which the whole
movement is held against.

## 2026-09-29 — a moving car plans no further than its corners let it rest

**A plan drew back on its own as its car slowed for a bend** — the owner saw a car's hold reach round a
right-angle turn and down the road past it, then shrink to the join as the car braked for the turn, with nothing
cutting it: a car that knows it will crawl round a turn should never have planned that much. The planned run
held the speed the car was planning for (TER-4c.1), which on the approach to a bend is the approach's and not the
bend's. **What a moving car means to use now ends where the corners ahead let it be at rest**
(`CornerLimits.RestToM`): the first arc begun past its nose, entered at its entry figure, and a stop from there.
It is read off the arcs the line was laid with and it is every build's alike, since the grip a car corners on and
the grip it stops on cancel; and it only moves on as the nose does, so a plan bounded by it grows as the car goes
into the bend and never draws back.

**Read at the nose and not at the lead point the follower brakes from**: the lead point carries the pedal, and a
foot coming off the throttle pulled it back over an arc's start and the plan with it, by as much as 12 m. **The
room to pull away is bounded with the run**, or a car cornering a little faster than the entry figures (a build
whose pedal stops it before its tyres would) planned past the bound by that and drew it back as it slowed. Over a
minute of each of the suite's towns, ticks on which a plan nothing cut drew back went from 10 155 and 2 018 to 28
and 31, and to 7 and 8 once a car braking in a bend stopped running out of it (`PlansInATownTests`; the wheel out of
the planned speed and CAR-47 are the car log's). What is left is a brake applied for a light still coming off the
pedal, and a car read a few metres back along its line for a tick in a tight arc.

## 2026-09-29 — a plan is laid to the metre it was answered

**Cars were driving reds seconds into the red, at full speed** — the owner saw them "not even trying to stop".
The light's hold was gone from under them: a plan refused at the bar was answered at the hold's first metre,
carried to the line and back to be laid, and came home a hair past it. Laid there it took that hair off the hold
(TER-4c.3), and a secondary claim cut anywhere goes whole (TER-5c.2) — so the whole of the light went, the plan
was answered again against a road with no light on it, and it did so every tick. On the drive probe three reds
in five on Odesa and on River were this, and none of them could be told from a car committed when the amber
ran out without asking how far into the red it was. **The piece a plan was refused on is laid to the
metre it was answered at** (`PlanAnswer.CutWayM`), for drivers and walkers alike; only the pieces before it are
carried over from the line, and they end at their own ends. `LightsInATownTests` holds every red's bar tick by
tick.

## 2026-09-29 — a plan reaches no further than a plan may

**The owner asked for plans bounded by the road rather than by speed alone** (TER-4c.1): a stated length
(`DrivingFigures.PlanMostM`), the car's own stop from its top speed, and no more than two joins that break its
line — "connectors give unpredictability and extra curvature, so actors should not plan too much ahead on
those". **Which joins count is geometry and not a junction's arms**: one whose curvature, or the curvature of
the lane it lands on, departs from the lane it leaves by more than a step (`JoinBendStepPerM`,
`RoadGraph.BreaksTheLine`), so a turn counts and the road carried straight on through a crossroads does not.
The movements crossing it there are plans, and it is the plans that answer for them.

**A plan held short is a stop point** (S-2, `DrivingHold.Reach`), or the ground a car can no longer stop short
of would run past ground it holds and meet nothing there — a light's hold past the end of the plan included.
So the length is what bounds pace on an open road: at 40 m the drive probe tops out at 27 m/s against 46
before, the mean is unmoved, and the reach binds about two car-ticks in five. **The amber was not derived from
it**: a car at the nominal build's braking caught at the edge of what it can stop short of reaches the bar
about 1.5 s later, which is the amber's own length, and a softer-braking build a little over it.

## 2026-09-29 — a zebra is planned like any other ground

**The whole-or-nothing zebra went** (TER-5c.3): a plan reaching a zebra ran on to the car's own length clear of
the far edge, and anything refusing it in between answered it where the zebra began. The grant credits nothing
in front with a speed (S-2a), so the car ahead pulling away over the paint refused it as surely as one parked
past it: **every car of a queue leaving a green stopped at its bar** — the bar stands a setback short of the
zebra, and the stand-off off it put the car there — and pulled away from rest once the car ahead was a car's
length past the paint. The owner asked for it dropped. A plan over a zebra is now cut where anything on it is,
like a plan over any other ground, so a car can come to rest on the paint behind a queue standing just past it;
the exam card that asked otherwise went with it.

## 2026-09-29 — p0 carries a pass as well as a body

**The owner asked for overtaking with the claim at p0 where it can be had** (TER-4c.6): a holder that has left
its own way cannot safely go back, so the ground it will cover is laid where nothing compares it — p0, beside
the bodies. It is the one new thing the ladder holds (TER-5g), and it needed three facts a body now says of
itself, since a reader may not look the holder up (TER-4c.5): **the way its line takes next** (`LaneClaim.Onward`),
**whether it is at rest** (`Still`), and **whether it is ground a pass covers rather than a collider**
(`Passing`). Rule 1 of the request — never pass somebody making your own movement in that section or
intersection — is `LaneClaim.MayBePassedBy`, read off the first and the third.

**Asked, laid, then kept or withdrawn**: two passes asked over one ground on one tick read the same layer and
would both be laid; the rebuild after, the lower by roster and occupant keeps its own and the other withdraws,
which is the only way two holders reading one layer can come to one answer without a register of whose turn it
is. **A pass holds nobody already standing on its ground** (`LaneOccupancy.Reach`): its holder is held off them
instead, or each waits for the other.

**Its ground is swept and read off the atlas** rather than mapped lane to lane. The first cut mapped a car's lane
onto its reverse and refused any pass that reached a box; the owner saw cars not passing where car parks cut a
street into short lanes and asked for junctions to stop mattering: if the claim can be placed, pass. Read off
the atlas, the pass holds whatever a box or a mark lays under it, as a body does, with no second geometry beside
the one the physical layer already reads. **Each network lays its passes on its own ways**: a car's pass laid
over a pavement's band where it pokes over the kerb (WLK-16) held walkers standing on it, whose bodies then held
the car — a ring that stood both for as long as the leg's clock let it.

**The swept ground is the holder's own body, from where it stands.** A line moved across and swept a body's
width wide from the front of the holder left the ground its body swung over as it turned out held by nobody;
the owner saw the claim begin too far on. A body stood at every place the pass puts it now lays the pass, so one
way is covered many times over, and `LayPass` grows a stretch of the same pass where it meets another — only
there, since the lane a pass leaves and comes back to is two stretches with what it passes between them.
**A movement through a box is held whole**, as a car in a box plans the rest of the join: held in part, a car
crossing the box was let in and cut in the middle of it, over the pass's ground on the movement beside its own.

## 2026-09-28 — a box is not remembered

**Who had a box last time went out of the comparison** (TER-5e): a car carried the movement it had won from one
rebuild to the next (`CarFleet.MovementWay`), its plan to and through that box won ties against an equal
movement nearer since (`LaneClaim.Held`), and once it could not stop short of the mouth the whole join was
committed. The owner asked for the reservations to be the whole of it, and the scenario map holds without any
of it: **109 of 109 cards pass on ground a car stands on and ground it can no longer stop short of**, and
nothing else.

**Committing a whole box statelessly was tried both ways and refused.** Committed through the join once the car
could not stop short of its mouth, a through car too fast to stop at the mouth took the box from a car on a call
it had been refused against (the call card never arrived); committed once the car was in the box, a car waiting
inside it at the metre it was refused held the car it was giving way to (five tee cards). A car in a box still
plans the rest of the join and its own length of the way out.

## 2026-09-28 — a zebra is marked whole

**The owner asked for a zebra to be one piece of ground to both networks** (TER-5c.3): a car reserving any of
it places secondary claims on both of its walking lanes whole, and a walker reserving any of it places them on
every lane under it. The ribbons had marked only the ground the two actually share — a lane's band of each
walking lane, and each walking lane's stretch over each lane — so a car was held off the walker's band of its
own lane and nothing more, and a walker had to plan the paint to the far kerb, and be sent back to the kerb
when refused anywhere on it, to hold the lanes it had not reached. **The marks now say it**, laid with every
other mark and read the same way, and the walker's special cases went with the reason for them (`PER-27`).

**Driven ways only.** The walk a zebra hands over to at each kerb shares the end of its paint as any two walks
do; marked whole it would hold the zebra for somebody walking past the end of it.

**The per-lane bands went with it** (`CrossingEdges`, `LaneFurniture`): which lanes a zebra is painted across,
and where on each walking lane each of them falls, was read by nothing once the marks carried it.

## 2026-09-28 — a light is a rung of the ladder, and its hold the one secondary claim with no main claim

**A light stopped clipping the plan and started holding ground** (TER-5g, TER-5c.1): the owner asked for the
lights to be a secondary reservation on the lanes they block, high on the ladder and not above the police or
an ambulance. So the ladder gained a rung, **p4**, between a closed road and a walker, and everything below
it moved down one — a walker to p5 and the movements to p6, p7 and p8. The numbers are the ladder's order
and nothing but the ladder and its own tests names a rung by one. Why the hold is secondary, where it
runs and why it is placed first is [agents/trafficlight](../../../agents/trafficlight/docs/decision-log.md).

**The bars are laid once** (`StopBars`, TER-6): the lanes' furniture read the plan's `StopLines`, which only
the scenario map filled, while the picture painted the town's own. The furniture, the lights and the heads
read the one laying now.

## 2026-09-28 — a crossing asks the lanes near it which it is painted across

**Finding the lanes under each crossing was the town's crossings times its lanes** (`LaneFurniture`): every
crossing projected onto every lane in the town. On a town thirty kilometres long at Odesa's density it was
most of the 21 s standing the town cost past its graphs. **Each crossing now asks the road graph's own lane
index for the lanes that could pass within its half-span** (`RoadGraph.LanesAround`) and asks those the
questions it asked of all of them, in lane order — the same pairs in the same order. That part of standing
that town went to 0.85 s, and Odesa's from 128 to 77 ms.

## 2026-09-27 — the marks' pairs are hashed from both ways, and the atlas lays in time linear in the town

**The atlas's time was the square of the town, and all of it was the marks' merge.** Every pair of ways
that share ground is keyed as the two numbers in one `long`, whose own hash is the two xored — and ways that
share ground are numbered close together, so the pairs fell in a few buckets. Odesa's atlas laid in
0.8 s and Odesa's brief with its extent doubled each way, 4.6 times the entries, in 26 s; the merge alone
was a third of the whole load.

The key is now hashed with both halves mixed (`RibbonMarks.PairHash`). The same two towns lay in 0.39 s and
1.8 s. What is filed is unchanged: the doubled town's census is the same line for line, and every body of
every shipped map stands to the bit where it did after 3600 ticks.

## 2026-09-27 — a body is read off the atlas point by point, and nothing it already knows is asked again

The owner asked that a lookup not work out again what the town already knows, the physics grid's cells
above all. **Which cells a car is in is not what a lookup pays for.** They are shifts of the rows of
points the atlas needs anyway. The physics index files 1.02 cars to each 8 m cell on Odesa, so walking it a
cell at a time shares nothing between cars. No car's pose is ever exactly the last tick's, since nothing
sleeps, so a last answer is never reusable as it stands.

**What a lookup paid for was reading entries it then passed over.** A car's row, read from its first entry,
held 172 entries for the 50 the car stood on, and deciding which to skip was branches. So:

- **Every point of a kept cell has a start**, and a lookup reads the entries of the points inside the body
  and no others. The atlas grows from 49.2 to 58.9 MiB on Odesa.
- **A way's stretch is gathered in the tenths the atlas files**, and turned into metres once per way rather
  than divided once per entry. They are floats: integer least and most compile to branches, and those cost
  a lookup a fifth.

**Measured** on the fixed-tick soak, microseconds a tick, the atlas lookup alone:

| | before | after |
|---|---|---|
| Odesa, 520 cars | 287–288 | 227–236 |
| River, 480 cars | 254–256 | 201 |
| Odesa walkers | 77 | 62–64 |

Tried and left out:

- **A start every 2 m**: 2 MiB instead of 10, and a fifth of the gain.
- **Remembering a cell's slot across its rows**: no gain, and a call a row.
- **Integer tenths**: 20 % slower than before.
- **The solver's own direction for a car**, in place of a sine and cosine of the fleet's heading. The two
  are one in a running town, but a fixture stands a body by writing the fleet, and a box read from the
  fleet's place and the solver's direction is neither. It saves 4 µs a tick.
- **A point's depth taken once for all the ways over it, and each point's start read once**: no gain
  (Odesa's index 484 µs a tick before, 483 after). The profile put a quarter of the lookup on the depth
  test, but the arithmetic was not what it was paying for.

The soak's figures on all four maps are identical line for line before and after.

## 2026-09-27 — a car refused in a box waits there, and nothing moves the answer

The owner ruled the flow pure: a patch beside it goes, and what the flow gets wrong is fixed in it. So
**`WaitsClearOfTheBoxes` went** (TER-4c.1). A car refused by a plan where it would rest on ground its join
shares with any way was sent back to the mouth. It judged from the marks, not the claims, and the ground it
kept a car off was often ground nobody held. It cut at a place nothing stood, as `LaneClaim.Nothing`, so
`TheBoxGiven` and the stuck probe each had to handle a cut with no cause. It spared cars cut by a body. And it
judged "can still stop" by a third stopping distance. `WayCrossings.OwnRuns` went with it, read by nothing
else.

**Measured** over six agent seeds of a minute each, the rule against none:

| | Odesa | River |
|---|---|---|
| wrecked | 27 → 37 | 52 → 46 |
| touches | 214 → 202 | 226 → 224 |
| km driven | 2417 → 2409 | 1980 → 1955 |
| walks given up | 450 → 472 | 405 → 407 |
| car-ticks at rest inside a box | 178 k → 184 k | 242 k → 268 k |
| rebuilds left unsettled | 15 213 → 17 786 | 16 201 → 20 852 |

The wrecks move both ways, inside a spread of 0 to 16 a seed. The three crashes of the Odesa seed that went
0 → 6 involve no car waiting in a box. A walker steps onto a zebra three metres in front of a car at 13 m/s.
Two cars on the two lanes of one road meet as shapes, off each other's ribbons. A car running wide on a corner
at 29 m/s hits one standing off its line. What does move is the waiting in boxes, and with it the rebuilds the
settling leaves at its bound. The exam is 108 of 109 cards either way, card 59 failing on the same 0.2 s.

## 2026-09-27 — a claim is main or secondary, and a main claim reads its own way alone

The owner named the claim a holder places on a way its own crosses a **secondary claim**, and ruled that
**two secondary claims never meet**: main against main on one way, and main against secondary, are the only
meetings. Placing a claim reads the main way alone, and the secondary claims go down with it (TER-5c.1).

**The reads of the other way went, because they were a second copy of one answer.** `Reach` walked each
mark's far way for main claims over its section (`LosesTheSection`), and `Take` cut those main claims when it
placed the section. A mark is filed under both its ways, so each such main claim had already placed a
secondary claim over the asker's own way. The ordinary walk of that way met it at the same metre, on the
same terms. The one term that moved is whether a holder's body stands on the ground: it is now read off
the asker's way rather than the far one. Taking that secondary claim had already cut its holder short of the
mark, too. **So the atlas files a pair only where both sides hold ground**: a side clamped to nothing would
take a secondary claim and place none back. `Beats` lost its second metre, since every meeting is on the
asker's way.

**Measured**: the exam is 108 of 109 cards before and after, card 59 failing both times on the same 0.2 s;
`qq tests all` passes, the gates included.

## 2026-09-27 — a ribbon is its way's own width, and a mark is worked out from the ribbons

The owner ruled that **a ribbon is its way's own width** — a lane's, a join's, a pavement's — and that there
is no ribbon at a car's width; and that **a mark's section runs as far as the two ribbons share ground, up to
where they are almost touching** (TER-4c.4, TER-5c). On the overlay a join's marked section had stopped about
a metre inside the lane it crossed: the block was drawn at the lane's width, and the section had been cut
from ribbons a car wide, sampled on the lattice.

**Marks are worked out from the ribbons and not from the lattice** (`RibbonMarks`, `RibbonPiece`). A piece of
a way's line swept to its width is a rectangle or a sector of an annulus; a metre of one meets another's
ground where the slice across it does, and each end of a section is walked for a lattice step at a time and
then halved to under a micrometre. Lane-width ribbons had failed before on the lattice's reach: laid that much
wider, the two lanes of a carriageway overlapped by 0.7 m and every pair of joins out of neighbouring lanes
was marked. Worked from the ribbons, ribbons laid edge to edge only touch — on Odesa, joins marked against
joins went 71 833 → 72 411, and lanes against joins 1 920 → 12, since a hand-over is no longer one.

**Shared ground is ground deeper than the touch inside both**, each ribbon worn back by it at the sides and at
the square ends, and a section runs over all of that and no further. Run to where the ribbons last touched, a
2 m pavement edge stepping onto a zebra's kerb end at an angle — its square end dipping a few centimetres
into the lane — was marked whole, and a car passing locked the pavement. Worn, those 1 050 pairs on Odesa
share 0.22 m of pavement edge on average and 0.90 m at most.

**A body is read over only ground it and the band both hold.** The lattice still files a point a reach past
every band, now with how far outside the band it stands, and a point counts where the collider reaches
further past it than that. At lane width the reach alone put a car 0.45 m off its line on the next lane.

**Measured** on Odesa over six agent seeds of a minute each (`--bench soak`), against the tree before:
knocked down 2 → 3, wrecked 34 → 34, touches 183 → 191, walks given up 269 → 448, km driven 2533 → 2415. Taken
while sections still ran to the touch, the same marks and body reading at the old widths gave 22 wrecked, 186
touches, 265 walks given up and 2561 km, and driven ways alone at their own width 35, 251, 360 and 2439 km:
what costs is the width, most of it on the driven side. The atlas on Odesa is 3.5 M points and 6.3 M
entries — 54 MiB — laid in 0.75 s, against 2.7 M, 3.9 M, 30 MiB and 0.7 s.

## 2026-09-26 — what a cut frees goes back inside the tick

Plans were laid once, in the same order every tick — cars by index, then closures, then walkers — and a cut
only ever shortened a hold. Two faults came of it, and both held for as long as the town stood the same way,
not for one tick.

- **Refused by ground a later cut took away.** Car A was cut by car B at a box, then a walker on a zebra cut B
  back short of the box. A still waited on ground nobody held.
- **Refused by a piece its own taking removed.** A hold is answered way by way before it is laid. A straight
  movement beat a near-side turn at the mark where their joins merge, and lost to it on the exit lane on
  arrival. Its taking at the merge then removed the turn's exit-lane piece, and the straight car still braked
  at the far edge of the box.

**Every plan another plan ended is now answered again once all are down, and laid again where the answer
moved** (`TownWorld.SettleThePlans`, `LaneOccupancy.ReopenHold`). Chosen over laying the walkers and closures
first, which would have fixed only the commonest chain.

**The passes are bounded at four, and the bound is not a gate.** Nearly everything moves in the first pass; at
two, four and eight passes the same seeds give the same crashes and walks. Rebuilds still moving at four
are still moving at eight. They are rings, where each holder takes from the next, and whether a town has them
depends on its junctions. On Odesa, over six seeds of a minute each, 0 to 1824 of 4200 rebuilds hit the bound.

**Measured** on Odesa over those six seeds, against the tree before:

- km driven: 2100 → 2542
- walks given up: 316 → 269
- wrecked: 30 → 34 (1.43 → 1.34 per 100 km)
- touches: 195 → 183
- car-ticks spent past the braking margin, per km: 1.52 → 1.53

Five minutes of `--bench stuck`:

- drive legs given up: 603 → 354
- walks given up: 1011 → 721
- cars that ever stood still: 403 → 264
- in that one run, wrecked 14 → 18 and touches 265 → 355

The exam went from 26 failed cards to 23. Cards 20, 50, 92 and 93 now pass. Card 15 now fails — a straight
giving way to a left turn from its right, which the ladder does not state.

## 2026-09-26 — two layers over a ribbon atlas: where the bodies are, and where they mean to be

The owner set the reservations three rules. **A body reserves, at p0, every ribbon its collider overlaps,
and two such reservations may overlap**, being a record of real things. **A plan is settled against its own
way and, through marks laid when the town is, against every ribbon it shares ground with** — the existing
comparison deciding whether it takes the other's ground or cuts itself to the free space — **with no geometry
at runtime**. **And those are the only two ways ordinary agents meet.** Everything in
[claims.md](claims.md) was rewritten to them.

**The atlas is laid once** (TER-4c.4, `RibbonAtlas`): every way's ribbon sampled onto a lattice a quarter
of a car apart, each point knowing which ways it lies under and how far along each. It replaced a per-body
walk of the nearest lanes with a band test every tick, a crossing table measured from centrelines for joins
alone, a zebra's bands projected while the town ran, and the furniture's claims: which ways a collider is over
is now a look-up of the points inside it. **Joins that diverge from one lane or merge into one are marked**,
their ribbons sharing their first or last metres: that ground is one piece of the world and two bodies cannot
both be on it.

**Ground a car can no longer stop short of became the first term of the comparison, not a p0 claim** — p0
is the collider and nothing else. The ladder was redrawn round it (TER-5g): body, committed, call, closure,
light, somebody on foot, then the three movements. **The stated band, `Reserved` and `Rejected` went**: a plan is one hold
from the nose, answered before it is laid, so there is no weaker band to tell apart from it.

**The comparison is committed, standing, rung, box already given, arrival** (TER-5e), and each term is a
fault it closed. **Standing**: a walker on the paint and a car whose secondary claim lay over it each waited
for the other. **A box already given**: a tie-break on the metre each hold entered the box at jumped as cars
passed marks, and boxes changed hands under cars on their way in — into crashes. **Arrival alone between two
holders that can no longer stop**, since the one further off has road left to brake on and the box it was
given last time is no reason to drive into the car already there.

**A plan is never laid past what its answer gave** (`PlanTheDrive`). A car's refusal placed at its committed
distance instead rode ahead of the car at exactly its stopping distance: a car refused a box held 19 m/s into
it, and the plan laid past its answer took the box from the car that had won it.

**What is outside the two layers is named** (TER-4c.5): placing a body leaving a building (`ExitSpots`, a
placement and not a decision), route-cost memory (`LinkSurcharges`, now one table per network — the walking
router had been pricing its links off the driving table by id), building capacity, and the special agents.

**The owner's open decisions were taken at the plan's recommendations**: the new rules at P3, not P0 or P1;
committed ground as a plan nothing takes; a prop on a driven ribbon refused when the town is laid; the
walker's rung above every movement and below committed ground; route cost and
capacity exempt by name; a bay's ways planned like any other when bays return; and a body's own place on its
own line as its control rather than the reservation path.

**Tried and taken out**: extending a car's committed ground through the box it was in, which handed every car
creeping through a junction absolute priority and put them into each other.

**Measured** on Odesa over six agent seeds of a minute each, against the tree before: wrecked 34 → 26, knocked
down 14 → 2, touches 2719 → 187, walks given up 666 → 316, 1878 → 2092 km driven; over five minutes of
`--bench stuck`, touches 3799 → 265 and walks arrived 72 → 170. Two faults outside this slice were fixed on the
way and carry much of it — a corner the look-ahead reached a wheel's lookahead late
([agents/car](../../../agents/car/control/CarFollower.cs)), and a walk that ran out on a corner of no length
([agents/person](../../../agents/person/docs/decision-log.md)).

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- **TER-5**: a junction's radius is the connection standoff (a car park's, its rank's) and is not sized on
  the arms; nothing measures from it.
- **TER-6**: a town's zebras stand at the kerb ends' stations, not at pedestrian node pairs, spanning the
  carriageway's two edges; a box that forks nothing carries none.

## 2026-09-21 — one laying of a town's zebras, read by both networks

The walk was cut and joined at 488 crossings in `Towns.City` and the road knew about none of them. The walk
lays its paint off the town's own kerb ends (`Crossings.Lay`, WLK-10), while `LaneFurniture` and
`CrossingBands` read `CityPlan.Crosswalks`, empty since a junction stopped striking its own crossings
(`RoadStage`). Empty shows up as no answer rather than a wrong one: no lane carried a crossing, no crossing
carried a lane, no walker was ever *on* one, and the stop short of paint a queue would leave a car standing on
(`CAR-15`) had nothing to fire on in any town.

**So the town lays its zebras once and hands them round** (`TownWorld._zebras`): to the walk cut at them,
the lanes that carry them as furniture, and the bands projected beneath the paint. What the walk crosses is
what the road is crossed by, because it is one laying and not two that agree. **It is the plan's record that
was dropped and not the projection**: `Crossings` already carried everything the two readers took out of
`CityPlan.Crosswalks`, and the renderer was already on it.

## 2026-09-19 — an arrow is the movement drawn small, not a glyph picked out of a set

Three shapes and four more for the pairs, at a fixed right angle, would be wrong twice. **A fixed right angle
lies about the junction**: arms stand anywhere from `ArmsApartMinDeg` apart to nearly opposite, so most turns
are not right angles. **And a set of glyphs is a set to extend**: the first one-way street offering a turn and
a straight would want an eighth drawn by hand. So a branch is bent by the angle its turn was classified from
(`Spline.TurnedRad` of the movement's own line, the number `LaneLines.ConnectorKind` was decided by), and
nothing anywhere names *ahead-and-left* (TER-6a).

**The curl is sized by the lane and not the turn.** An authored radius has no answer for a shallow turn: at a
fixed small radius a thirty-degree branch leaves the bend early and runs straight off the side of the lane.

**The bar is laid once and read twice.** The arrow stands a setback behind it in the lane's own metres, the
figure `StopBars` placed the bar by, so the bar carries `AlongM` rather than the arrow projecting the bar's
centre back onto the lane and getting a centimetre's different answer over a bend.

**The glyph is anchored at its front.** Fixing the tail is the easier arithmetic and puts a turn-only arrow's
paint a metre and a half further back than the arrow beside it, a bend spending most of its run going
sideways — so a row of approaches reads as marks at four distances from four bars.

## 2026-09-19 — the walk hands the road two answers, and only the zebra follows the shorter one

A street too short to be crossed twice is crossed once midway (`WLK-10a`), and the first cut of that moved
everything hanging off a crossing with it: one band filed at both arms, so both lanes stopped behind a bar
in the middle of the street and the lane line had nothing left to be painted down. A bar is where a driver
holds for the box in front — a fact about the end of the street, not about where the paint went — so the
walk hands down two lists, `KerbEnds.HeldM` for where it is cut and `KerbEnds.CrossedM` for where it
crosses, the same list on every street but a welded one.

**The road reads both through one type** (`Crossings.Lay` of either span) rather than growing a second
registry of bands: two constructions of a band would disagree about a corner the week after either moved.
The paint is laid from the crossings; the bars and the lane line's trim from the stations.

**A bar with nothing in front of it falls back to the kerb end.** A station stands `Road.FootNodeClearM` out
from where the kerb ends, so a bar left at a station the paint has gone from holds a carriageway's width out
from its box. The second answer carries the kerb end at those arms, flagged as bearing no paint, and the road
takes it as a band of no depth — one figure, and the setback, the bar and the trim come out right without any
of them learning a second case. **Which leaves the dashes running under the stripes of a welded crossing**,
as they must: a run trimmed towards a band in its own middle is cut from both ends and no line is left.

## 2026-09-18 — a band's near edge is struck along the band's own axis and not along the lane behind it

A crossing's near edge was struck by stepping half the band's depth along **the lane's direction at the
lane's own end**, which is the band's own axis only where the arm has straightened by its junction. A band is
laid square to the walk that placed it (WLK-10), so which way it is deep holds wherever the paint stands;
struck along the lane, the step goes partly across the band and the bar lands out of its gap — 0.58 m once a
crossing stood tens of metres from the arm's end. **The lane is still asked which edge the traffic meets
first**, which is a sign and not a bearing and costs nothing however the arm bends. Two fitted test bounds
on the fixture grew with it; neither is a figure the claim leans on.

## 2026-09-16 — an arm's paint is one bundle, and `Crossings` is where it stands

The band, the bar behind it and the end of the dashes behind that are one bundle read off one answer.
`StopBars` asks `Crossings` where the band is rather than subtracting a depth from the lane's own metres —
over a bend a lane and its road run at different rates, and the bar came out centimetres out of the gap it
was meant to leave — and `CentrelineRuns.PaintedM` reads the same band. Rule 3 of the markings is that
relation stated: a bar on an arm with a crossing is placed by the crossing alone. **The registries are
derived and not planned**: `Crossings` and `StopBars` are laid off the plan the way `CentrelineRuns` is, so
the paint reads one answer.

## 2026-09-16 — a lane line is painted down a carriageway, and a carriageway is not a road

Under the old layer a road ran through many junctions and a run was a stretch cut out of one road. Under this
one a road ends at every node something meets it at, so a run is **several roads joined into the carriageway
they are pieces of** (`CentrelineRuns`).

**The car park is what decides it.** A lot is a junction cut into a street that already stood (`GEN-52`), so
a street with three lots down it is four roads; painted road by road it came out as four lines with a gap at
every lot and four dash phases, against rule 4 of the markings. The same is true of the two-armed nodes a
bridge, a loop or a refused join leaves, so the test is **what stands at the node** and never what put it
there: two road arms and any number of bays is one carriageway.

**The ground between two roads is crossed on the biarc the movements over it are drawn on** (`LaneLines`),
since the two lanes either side of it are that line offset half a lane each way; `GroundMeshTests` measures
the paint against it rather than taking it on trust.

## 2026-09-14 — the town does not lay the junction that decides nothing, so a lane never merges

**A merge could not be expressed in the graph the search runs on.** A lane carrying on through a node left
movements landing partway along the lane they joined; the contraction was meant to read that relation, and
[DrivingNetwork](../DrivingNetwork.cs)'s view of the lanes did not answer for it — so `LanePlaces` was
derived a second time, every merge silently unioning a node with the *start* of the run it joined, up to
`1132 m` away. The coarse graph refused the join outright (`TravelGraph.Builder.Join` holds links to meeting
end to end, which keeps the search's bound admissible), and a link that can be entered at two places is two
links.

**So the shape that needed the merge is not laid.** Only the scatter made it — a one-way street arriving at a
three-armed node leaves each two-way approach one movement — and `GEN-18` refuses that arrival. Odesa's `42`
merges and `17` lanes with one way out are `0` and `25`, all `25` a roundabout's entry (`GEN-19`) or a corner
every other movement was refused for (`GEN-48`) — places a driver really is committed.

**Four layers of rule went with it**: `TER-5j` and its bounds, the station a movement lands at, the places a
lane is *driven through*, the ground a merge takes off the lane it joins, the merge's own right of way, the
assembler's third figure and the census row. **The contraction does that work** — it ends a link where a body
can go more than one way. **And the places are worked out once and handed over** (`SIM-7`):
`RunNetwork.Contract` takes them rather than deriving its own, which is what let the two answers disagree.

## 2026-09-07 — what the road *is* and what it hands out are two documents

One page carried nine sections and eleven thousand words, against a rule that a document covering eight
things is eight documents. The seam is that half of it described the network — a road, a junction, a
crossing, the paint — and half described a protocol over it, so `TER-4c`, `TER-5c`, `TER-5e` and `TER-5g`
went to [claims.md](claims.md) and nothing else moved or changed. Split by section instead, the claim
ladder would have been read without the rule it ranks.

## 2026-09-06 — a movement is as wide as the narrower lane it joins, and one figure says so

Three readers each took a movement's width off the lane it arrives on, which is fine while every lane in a
town is the same width — on a hand-authored map where a four-metre street meets a five-metre one it put
half a metre of tarmac past the narrow street's kerb. The narrower of the two lanes is the only width a
single band can have that never claims ground outside either arm (TER-5d.1), answered once by
`LaneLines.ConnectorWidthM`.

## 2026-09-06 — the graph reads the lines rather than drawing them

`RoadGraph` cut the roads, offset the lanes and drew every connector, and then laid the rules over what it
had drawn. Only the second half is this slice's: the first is the town's own geometry, wanted as much by
the ground as by the traffic, and it is `CityGen.LaneLines` now. What the move buys is that the tarmac and
the network cannot disagree — the question of whether the surface and the graph were laid to the same
figures cannot be asked.

## 2026-09-05 — a connector is an object, and where lanes meet is worked out from them

A movement was a lane paired with a slot index, and the pair travelled together because neither number
meant anything alone; a connector is the id and answers all of it. The plan's node table is gone from the
graph the town runs on: two lane ends are one place when a connector runs between them, or when they are
the two ends of one stretch driven either way, and `LanePlaces` works that out once so the contraction and
the claims cannot disagree about which lane ends are one piece of the world. The second clause is not
tidiness — no box admits the turn-around (TER-5f), so joined only by connectors a dead end's two lanes
would be different places and a leg could not be priced round a bay (GEN-4l). A spatial index over every
way was the alternative and needs no places at all, refused because the span a caller must give the walk
then has no bound the town can state.

## 2026-09-05 — a one-way road is a narrower road, and a corner is solved on the pair it stands between

The lane graph was already directed, so a one-way road needed only one lane laid instead of two; a
full-width road with a lane nothing may enter is a rule every drawer, claimer and walker has to be told
about. Which way it runs is carried and never inferred, since a narrow road is not necessarily a one-way
one. Laid down the middle of its own line it was a narrowing rather than a street, so every car through the
junction stepped sideways: it stands on the half it is driven, moved after the bends and never before them,
and a node with no fork goes with its arms. What broke was the junction, and it was already broken — every
corner was solved as if both arms were the same width, which until then they always were. A corner is the
crossing of the two kerb lines each offset by its own road's half, and whether one exists at all is
measured off the narrower arm.

## 2026-09-03 — a lane is the ground it is walked over, and the corners come off the line

A pavement lane was its whole offset line with each corner's ground remembered against it as margins, so a
metre or two at both ends of every lane was on the lane and on nothing else — real, since a body standing
inside a corner claimed it, and drawable only as a line ending in mid-pavement. The margins come off the
line itself, so a mitre sets off from the arriving lane's last point and lands on the onward lane's first.
Two ends of a short stretch could want the whole of it, so they are held back in proportion to leave the
lane four fifths of itself.

## 2026-09-03 — turning round on the spot lays no ground

`LayJoins` laid a mitre for every way out of a node including a stretch's own reverse — 7292 on Odesa
carrying 22.9 km — and nothing else in the network treated it as a way. That made it a way with claims on
it and no line anywhere, so the claims layer drew a fan of blocks the nodes layer had nothing to draw,
which is exactly what OBS-2d forbids. It also spent slots. The turn stays in the table, since a walk at a
dead end needs it, and lays nothing.

## 2026-09-01 — a zebra spans the road it names, and carries no span of its own

A crossing carried a span every planner filled with the width of the carriageway it was laying, so the field
agreed with the road until something laid one of the two again. The reach is solved and never carried
(TER-6). The depth stays the crossing's own, since how much of a road's length the paint covers is nothing
the road decides. The skew is part of the relation and not an exception: `Zebras`' off-square crossing was
8.83 m of an 8.00 m road, which is what the file held and what the derivation gave without being told.

## 2026-08-29 — the box refuses a car at a place

The gate answered *whether* and the grant answered *where*, the same question at two resolutions, so a body
on a box's far corner held the near half against a car that would never have reached it. The gate answers in
metres on the same figure, and the body margin keeps it from deadlocking — a car held a margin short claims
no metre of the section, so the crossing movement still reads it free.

## 2026-08-28 — the walkers claim the road before the grants

The walkers claimed the road as the *last* pass of the rebuild, after every grant had been taken off it, so no
driver ever read a band while deciding how much road it had. Their claims go in before the grants. River went
from two knocked down and two wrecked to none of either.

## 2026-08-27 — a junction admits no movement that reverses the direction of travel

The turn-around was in the table from the beginning and drivable by nothing — two opposing lanes join on a
1.5 m semicircle — and was classified, laid, measured, ranked and priced at infinity, which is a great deal
of machinery to say *never*. It is gone (TER-5f), and a quarter of Odesa's 1472 movements went with it. What
a route may still do is come back down the other side of one stretch in a car park's bay, priced rather than
joined (`GEN-4l`).

## 2026-08-25 — where a road's paint breaks is the road's answer, not the drawing's

Dashes were laid by walking each road and asking whether a point was inside a disc or on a zebra, so
every arm was dashed right up to the mouth of the box, past the bar a driver stops at; the metre step was the
smaller fault. The boundaries are not looked for any more — `CentrelineRuns` takes them from whoever measured
them.

## 2026-08-25 — an inline junction's crossing is laid across the lanes at the node

The one thing TER-5b says an inline junction exists for did not work: the paint stands on the node itself,
further from every lane's end than the paint is wide, so the projection found no lane and a walker on it was
invisible to the traffic — hidden because every such crossing in the shipped towns was lit. It is laid
across the lanes that meet at the node, each at its own end (`LaneFurniture`), and that fallback is taken
only where the projection found nothing *and* the junction admits no turns.

## 2026-08-23 — a claim stops where a rule stops the car

`AskForTheGround` clamped the road at the place the car is held and then added the margin on top of the
clamp, so a car waiting for a zebra held a metre of the zebra — and a signalled crossing behaved like an
unsignalled one, the people getting over on their patience eight seconds later. The gap is part of what the
car asks for and is clamped with the rest. Nothing about following changes, and the ask only ever shrinks at a
stop.

## 2026-08-23 — a junction is committed to at the rate the car actually brakes at

The claim distance and the point past which a crossing is kept were the only stopping distances in the town
read off the pedal's cap, while every stretch of road is sized by the follower's braking figure. The cap is
larger, so both erred the way that costs: a car past the point it could stop gave the sections back for a bar
it was going to cross anyway, and in between two ticks they read free. Neither noticed wet ground, where the
gap is widest.

## 2026-08-23 — a stretch runs out at the box's near edge

`WaysAlong` stopped walking when the *next lane* began, and the next lane begins on the far side of the
junction — so nothing was laid on a junction until the stretch reached clear across it. A car approaching a
box claimed none of it, was granted its road as though the box were empty, and could see nothing standing in
it. The guard is the near edge now.

## 2026-08-22 — neither network's claims are one roster's

A walker on a crossing claimed the road and was in none of the road's questions — half right, since a walker
read as an obstruction is one a driver is held off, and one read as a committed claim cuts a car three lanes
away. What that cost was invisible until the ray went: nothing cut a driver's road at a body standing in it. A
body on foot is in every query a grant is taken against and carries its own reading, so what it must never be
is a property of which query happened to skip it. An occupant is an index into one of two rosters and the
stretch has to carry which, or the first walker whose index matched a car's is read out of the wrong fleet. The
walker's give-way arithmetic went with it: the claim *is* that arithmetic, already done, from fresher numbers.

## 2026-08-22 — braking has its own margin, and it is nearly all of the grip

Using the cornering margin for braking put the planned stop at 13.1 m/s² against the 21 the tyres actually
delivered, and every claim is sized by the planned figure — so a car held half again as much street as its
stop was going to use. A corner is held for as long as it lasts and its margin covers a bump, a camber and
the wheel still being turned; a stop is aimed at, straight, and over in seconds. Corner speeds are
untouched, which is the point of the figure being its own.
