# Reservations — requirements

**How the town's ways are held, and who gives ground up.** One subject: where a body is, where it means to
be, what two ways share, and which of two holders keeps a piece of ground. What a road, a junction and a
crossing *are* is [requirements.md](requirements.md); the ground itself is
[world/terrain](../../terrain/docs/requirements.md); why any of it reads this way is
[decision-log.md](decision-log.md).

**There are two layers over one numbering of ways, and they are the only two things an agent reads of
another.** The **physical** layer is where the bodies are; the **planned** layer is where they mean to be.
The first is laid again from the bodies every time the claims are laid (TER-49), and the second asked again by
whoever plans then — a car's plan kept between its turns (TER-4c.1); both are intervals of one way's
own metres, and **what two ways share is worked out once, when the town is laid** — so nothing about either
layer computes geometry while the town runs.

**TER-49** `P4` **Nothing is laid for the town: each agent claims on its own turn**, every
`Sim.AgentDecisionIntervalS` once it has decided, the agents spread evenly over the ticks — its body where it stands, its
plan kept, edited or asked afresh, a light its hold — and what it asked is answered with everything asked that tick.
Between two turns an agent holds to the grant it was given, walked in by the ground it has covered since
(`CarFleet.GrantLeftM`), and reads nothing of what took or stood on its claims until its next; a grant is read only down
the lanes it was read down, and a line begun afresh carries none. **So each plans for the whole of a turn**: a car
commits as far as it could not stop short of at any tick until its next, but never past the grant it drives on
meanwhile; a light holds where it is against now or will be before its next turn.

## The ground

**TER-4c.4** `P3` **Every way of the town is a ribbon, and the ribbons are laid once.** A ribbon is its way's
line swept to **the way's own width** — a lane's, a join's, a bay's, a pavement's, the paint of a crossing —
with square ends. There is no other width a ribbon is laid at: what travels a way is a body on it (TER-4c.2),
and not a second, narrower ribbon inside it. The town samples every ribbon a body can be read onto onto a
lattice of points when it is laid — **a walked one only in a town anybody walks, or where the traffic crosses
it**, a zebra's paint — and each point knows which ways it lies under, how far along each of them it stands
and how far outside each one's band. **That lattice is how the world reads a body onto ways** (TER-4c.2): which ways
a wheel stands on is a look-up of the point nearest it. **It is the world's and no actor's**: an actor knows the lane
or way it is on and where along it, is handed the ways its body is on by the world, and looks nothing up (the root
decision log, 2026-10-13). Which ways share ground is worked out from the ribbons themselves (TER-5c), every one of
them.

- **The lattice files a point a reach past every band**, the furthest any ground is from the point standing
  for it, **and a body is on a way only where it reaches past a point further than the point stands outside
  the band.** So a body over the band is never between two points, and a body up to its edge is not on it.
- **A wheel is on a way where the point nearest it lies inside the band**, so where it stands is answered to
  within that reach of the band's edge.
- **How fine the lattice is, is data**, and it is sized against the ground it must not miss: an overlap
  thinner than its diagonal can fall between points, so that diagonal is held under the narrowest thing a
  body must be seen straddling.
- **Nothing the town's furniture stands on is a ribbon anything drives** (TER-4c). A prop is not a body and
  holds no reservation, so a town that stood one on a driven ribbon would have traffic that could not see it —
  and it is refused when it is laid, rather than driven round.

**TER-5c** `P4` **Two ways share ground where their ribbons overlap, and the town marks every such pair
once.** A **mark** is one piece of shared ground read from both sides: this stretch of one way and that
section of the other. It is worked out from the two ribbons when the town is laid, filed under both ways, and
never asked again. **Shared ground is ground deeper than a touch inside both ribbons**, at their sides and
their ends alike: a pair is marked where there is any, and **each side runs exactly as far as there is** —
from the first metre of its way whose ground lies that deep inside the other ribbon to the last. A section
stops where the two are almost touching, so a graze marks the graze. **It is a table of ground and not a
table of verdicts**: a relation saying two
movements conflict answers one question for a whole junction, so a car crossing one corner of a box shuts the
far corner it never reaches, where a mark says *where*, and nobody is weighed against anybody over ground they
do not share.

What follows from that rather than being stated beside it:

- **The property belongs to the ways and not to the intersection.** A street bending through a junction
  shares nothing with the other street's straight if their ribbons do not meet; a turn across the oncoming
  stream shares exactly the metres it crosses.
- **Ribbons laid edge to edge share no ground.** The two lanes of a carriageway and a lane and the way that
  carries on from its end touch, and are not marked.
- **Ways on no channel in common share no ground** however their ribbons lie in plan: a bridge's lanes and the
  road under them (`CityPlan.RoadArrays.ChannelOf`, PHY-1a) are never marked. A connector is on both of the
  channels of the lanes it joins, so at a bridgehead it is marked against the ways of either level.
- **Ground two networks share is marked like any other, except a zebra**, which is marked whole (TER-5c.3) —
  and a zebra is the only ground the walk and the traffic share
  ([WLK-16](../../foot/docs/requirements.md)).

**TER-5c.3** `P5` **A zebra is one piece of ground, and its marks with the traffic say so.** Each of its walking
lanes is marked against every way the traffic drives under either of them: **all of the walking lane, against
the whole of what the zebra covers of that way** — from where the first of its lanes comes onto it to where the
last leaves it. So a car's plan over any of the zebra on its lane holds both of its walking lanes from kerb to
kerb, and a walker's plan over any of the paint holds every lane under it, the ones it has crossed and the ones
it has still to cross alike (TER-5c.1). Where the two meet is settled by the ladder (TER-5e, `PER-27`): the
walker plans the paint like any other way, and the car its lane.

- **The traffic's ways and no others.** The pavement a zebra hands over to at a kerb shares ground with the
  end of its paint the way two walks do, and is marked over that ground alone.

## Bodies

**TER-4c** `P3` **Everything that can be in a way is a body in it.** The traffic and anybody on foot, whatever
they are doing and wherever they are standing; a thing a driver must be held off that is in no way is a thing
the driver cannot see, and there is no second mechanism — no ray, no cast — behind the reservations to catch
what they left out. **The town's furniture stands on no driven ribbon** (TER-4c.4), and so is in nobody's way.

**TER-4c.2** `P3` **A body is on every way it stands on, at p0, over the stretch it covers.** It is read off the
lattice at the pose the solver left it in, once a laying (TER-49), and never off the drawn picture. **A car stands
on the ways its wheels stand on** — the ways whose bands hold the lattice point nearest each of its four, the one
reading that also says what each grips on until the next laying (TER-7b) — **over the stretch its wheels on each
stand at, held one reach either side of every wheel** (`Road.HeldAroundAWheelM`): where several stand on one way,
from the hindmost less that reach to the foremost and as much again. So a car with wheels either side of the line
between two lanes is on both, one with all four in its own lane is on that lane alone however its body overhangs
the paint, one with its axles on two ways holds each round the wheels on it, and **one with no wheel on a way is on
the grass and on no way at all**. **A walker stands as a single wheel at its centre**, held its own radius either
side, and on the way it walks at its place on it. **No body's collider is read against the ribbons.** **Only the
ways on its own channels** (PHY-1a): a car on a bridge is on the bridge's lanes and on nothing of the road under it,
one at a bridgehead on the ways of both levels there, and a walker on the ground's.

- **Nothing about the write turns on what the body is doing, and nothing of it is written.** Driven, parked,
  wrecked, under a hand or knocked down, a body in a way is in it — where, and no more; a body inside a building is
  in no way at all.
- **Bodies are never compared, cut or taken.** Two or more over one stretch are one claim with all of them as its
  holders. The physical layer is a record of where things are, and two bodies over one metre is a collision — the
  solver's question (`PHY-1`), not a claim gone wrong.
- **A body is one stretch of each way it is on** (TER-5c.2). Read at more than one place — a car's wheels, or a
  walker's centre and its place on the way it walks — it grows to cover them all.
- **Every edge is rounded up to the decimetre**, so one metre of a way lands on one decimetre whatever reads it.
- **A coupled pair is one occupant** (`EVA-5`): the car on the bar is laid under the vehicle pulling it, so a
  truck's own plan is never cut at its own trailer. **So are an officer out on duty and their car** (`SRV-11`):
  the officer is the car's closure on foot, and walks to the post beside the car — on the same lane, which a
  reservation has no across to tell apart — without being held off it.
- **A walker is always on the way it walks**, over the stretch its own body takes of it — the one way it must
  never be missing from, however the lattice falls under a body narrower than it.
- **A walker standing clear inside the band of the way it walks is on that way alone**, and is not read off the
  lattice: clear of either side by a touch, of either end by half the way's width, and of every mark on the way
  (TER-5c). Every other ribbon over a band is a mark on it but for those laid edge to edge with it — the way beside
  it and the ways carrying on from its ends — and a disc that clear reaches none of them.

**TER-4c.6** `P3` **Getting past something standing in the way is a pass, and a pass is a plan of its own over
all the ground it will cover** — asked at its holder's rung, and **at p1 once begun** (TER-5g), because a holder
that has left its own way cannot safely go back. A manoeuvre at a bay (`GEN-4f`) and a step across onto the lane
beside (`CAR-53`) are asked the same way. So it is asked for only where it can be had whole:

- **What it gets past is the holder's to decide, off where things stand and not who they are** (TER-4c.5): the
  claims say where a body is and nothing of what it is doing.
- **Over the lane beside and back**: the asker's own line moved across onto the way running back beside it,
  and back onto that line past what it passes, with room there to come back into — drawn inside the lane it begins
  on.
- **Its ground is the lanes and ways its path runs down, and nothing is swept** (the root decision log,
  2026-10-13): its own line from where its step out begins to where it is back on it, and the same metres of the way
  it moves onto — the lane beside for a step across, the lane running back for a pass, read in step with its own as
  the same share of each one's length; for a manoeuvre at a bay, the lanes the bay is worked off, over the stretch
  of each the body stands over somewhere down the shape; for a walker's sidestep, its own route — **and the
  holder's own way from where it stands when it asks up to there**, so every metre it moves from asking to the end of
  the pass is the pass's. A way's marks place it over whatever shares that way's ground (TER-5c.1), as any plan does.
  **The paint of a zebra** is ground only a call's pass is ever over (below).
- **Asked only over ground nobody stands on** but the asker and what it gets past, **and answered after every plan of
  its rung**, so traffic whose plan already reaches over it is let by first. **A car asks with room to spare** — as far as it is let stray off its steps — and lays and keeps without it, so a pass
  clearing something by a hair is not asked one rebuild and withdrawn the next.
- **A holder on a call asks at its call's rung** (`AMB-4.4`), **and a zebra is ground it may have**: its pass holds
  the paint whole, kerb to kerb on every walking lane, as a plan over any of it does (TER-5c.3). A body already on
  the paint holds it short of the paint until nobody is on it, or, already over it, it drives on off it.
- **Asked for, laid, then kept or withdrawn once**, in the rebuild after: kept where all of it was had (TER-4c.1),
  withdrawn where anything refused any of it — and nothing of it taken where anything was. Kept, it is never given
  up, and **what it has covered is given back as it goes**: it is asked from where its holder stands every rebuild.
- **Whoever holds it plans from its far end, in the same hold**, the ground up to there being its own, and stops
  short of whatever refuses that ground where its body would first meet it. **A holder's own claims never refuse
  each other**: its pass and its plan are one holder's ground.

How far a pass runs and how sharply it steps across is the holder's (`CAR-46`, `PER-28`); what it holds is
this rule's.

## Plans

**TER-4c.1** `P3` **Ground is asked for, then taken in one pass, strongest first.** A holder asks for the stretch
of its own line it means to use, from its front forward, a way at a time; **every piece of every plan is then taken
in the order of its rung** (TER-5e) and, within one rung, plans before passes not yet begun (TER-4c.6) and otherwise
in the order it was asked, each against what is already
down — which is never weaker than it — so nothing taken in a laying is given back in it and no answer turns on another
being asked again. What comes back is that stretch cut at the first of two things, both read off the way asked about
and off no other:

- **the first body in front of it** — a plan is never laid over somebody standing there, and a body beside or
  behind the holder cuts nothing;
- **the first metre another holder's claim already holds** — its main claim on the same way, or a secondary claim
  it placed there (TER-5c.1).

**Part of what was asked for is the ordinary answer** rather than a refusal, and a holder granted none of it
stands still. **What comes back is the holder's to move into**: whatever is laid on it later is weighed against
it, and only something that beats it takes it.

- **A plan begins at its holder's front.** Ground behind the nose is ground the body is on or has passed,
  never road it was granted, so nothing behind the nose can be what ends a grant.
- **A car's plan, and a walker's walk down its route, is kept from one laying to the next** (root decision log,
  2026-10-14): between its turns to plan (TER-49), and at a turn while it still serves — asked further from where it
  ends where its holder needs more or what cut it has gone, and let go of behind the front as its holder goes on. A
  light's hold is kept while it would be placed as it stands. **Ground a laying before granted
  is cut where a stronger rung takes it** and its holder has not committed to it (TER-5e), or where a body comes to
  stand on it; of one rung, what is held keeps.
- **Nothing but the answer stops a plan.** A red is ground: the light holds its bar (`TLT-1`), and a plan
  refused there is one stretch that ends there, so a car waiting at a red holds none of the box beyond it. A
  zebra is ground too, cut where anything on it is like any other (TER-5c.3).
- **A plan reaches as far as its holder means to be able to stop**, and no further — but for a call, which
  means a stated multiple of it and may reach as much further (`AMB-4.5`). For a driver that is the
  ground it can no longer stop short of, the room to pull away, and — while it is moving — what it reaches over
  a stated run pulling up to the speed the road lets it plan for and a stop from there; a body at rest plans the
  room to pull away and nothing more, so a queue waiting at a junction plans none of the box. **A moving driver
  means no further than the corners ahead let it be at rest**: the first arc past its front, entered at what that
  arc may be entered at (S-2), and a stop from there. So a car that will take a bend slowly plans the stop the bend
  allows and not the one its approach would, and what it plans only grows as it goes on into the bend. For a
  walker it is the ground it would come to rest in from its pace (`PER-26`).
- **And never further than a plan may reach**: a stated length, the driver's own stop from its top speed, and
  the mouth of the join past the stated number of joins that break its line — a turn, or a join that bends the
  road on. The road carried straight on through a box breaks nothing, however many movements cross it there:
  those are plans, and it is the plans that answer for them. **A driver whose plan was held short by this drives
  to stop by the end of it** (S-2), so the ground it can no longer stop short of never runs past ground it holds.
- **A car in a box plans its way out of it**: at least to the far side of the join its nose is on and its
  own length past that, however slowly it is going.
- **A car refused ground waits short of it, wherever that is.** Refused inside a box, it waits in the box,
  over ground it was given: its body there is on every way it stands over, and what crosses that ground is
  held off it as it is held off any body. Nothing but the answer says where a car waits.
- **Nothing is ever released.** Every plan is laid again from its holder every time the claims are laid (TER-49),
  so a body that stops, is wrecked or is taken over by a hand plans nothing at the laying after.

**TER-4c.8** `P3` **Nothing moves over a way it has not claimed.** Every movement an agent's action makes — a car's
or a walker's, forwards or in reverse, down its own line, over the lane beside, into a bay or back onto its way — is
over ground that action asked for and was granted before it moved: a plan, cut where it was cut, or the whole of a
pass's or a manoeuvre's ground, kept whole. **An action that claimed nothing is granted nothing** and stands, and
one newly taken up stands until its own claim is read; the one claim handed from one action to the next is a plan down
a car's own line. What moves a body that nobody planned — a contact, a shove, the solver — claims nothing and needs
nothing claimed.

- **Ground on no way is nobody's**: a walk across a lawn or a yard has claimed all there is of it.
- **A car nobody in the town drives holds what it can no longer stop short of** — under a hand (`S-7`), or on a bar
  under the truck pulling it (`EVA-5`) — being the whole of what anybody can say of where it goes.
- **A body put somewhere rather than moved there** — out of a door, onto the pavement, down in a yard — is put only
  where nobody holds the ground (`PHY-7a`).

**TER-4c.7** `P3` **A car backing up plans behind itself, weaker than every other plan.** Its ground is the
stretch of the lane it is on behind its tail that it means to back over (CAR-50), at the bottom of the ladder
(TER-5g), and is taken from the tail down: the pass's spare short of the far edge of the first body behind it, and
at the nearest metre of any other plan there, every one of which is taken before it. Where its holder is already
rolling back, what it can no longer stop short of is committed like anybody's (TER-5e). It never runs behind the
start of the lane.

**TER-5c.1** `P3` **A plan is main claims and secondary claims.** A **main claim** is ground on a way of the
holder's own line. A **secondary claim** is the whole of the section of another way that a mark links a main
claim's stretch to, asked with the main claim where the main claim comes to the mark: a main piece is split there
into the ground short of the mark, the secondary claim, and the ground on from it. **Claims meet in two ways and
no others** — a main claim and a main claim on one way, and a main claim and a secondary claim on one way — and
**two secondary claims never meet**. That is the whole of how two ways that share ground meet: every reader reads
only the way it is on, because whatever holds ground that way shares has placed a secondary claim on it.

- **A main claim is answered off its own way alone.** A mark is filed under both its ways, so whatever holds
  the far side has placed its secondary claim over the near side, where the main claim asking meets it.
- **Refused by a secondary claim, a main claim ends where that begins.** A car in a turn comes to the section the
  oncoming straight placed over the turn, and waits there.
- **A secondary claim is had whole or not at all**: a main claim already over any of its section refuses it, and
  its holder's hold ends where its own line came to the mark (TER-5c.2).
- **Two secondary claims on one way are no answer to each other**, and those of one rung over one stretch are one
  claim with all their holders. They are holders whose ground each lies over a third way, and where their own
  grounds overlap their own ways are marked against each other and meet there — so two cars going opposite ways
  through a box, whose secondary claims lie over each other on the turns between them, both go.
- **A holder places main claims only on the ways of its own line**, and on the ways it only crosses nothing
  but secondary claims. A car approaching a box holds no fan of joins it is never going to be on.
- **A light's hold is a secondary claim placed with no main claim** (`TLT-1`): on the way it governs and no
  other, from its start to the first body on that way — so it meets the main claims on that way, and what crosses
  that way is held by nothing of it.

**TER-5c.2** `P3` **A hold is one stretch of its holder's line.** Its pieces, read in the order the line runs
over them, each begin where the one before ended; **cut anywhere, it gives up everything past the cut** — the
main claims on the ways after and the secondary claims placed past the cut. A hold with ground beyond a gap in
itself is ground whose holder cannot be seen coming, and ground past a place it was refused is ground it could
not have reached without crossing what it was refused.

**TER-4c.3** `P3` **No metre of any way is planned by two holders.** A plan meeting another on one way is cut
where the other's ground begins; the two abut on an exact decimetre and neither reaches into the other. **Two
secondary claims are the one exception** (TER-5c.1). **Bodies are not in this at all** (TER-4c.2), and neither is a
holder's own ground against itself: two holds of one holder's that meet — a plan and a pass — are one claim.

**Which of two keeps a metre turns on the rung and on nothing laid before it**: the plans are taken strongest first
(TER-4c.1), so whatever is down when a piece is asked is at least as strong as it, nothing taken is freed, and no
ring of holders each taking from the next can form. **What a hand or a truck cannot stop short of is taken at p1**
(TER-4c.8), with whatever else nobody can stop short of.

## Right of way

**TER-5e** `P3` **Where two plans meet on one piece of ground, the stronger rung keeps it**, in this order:

1. **Ground its holder can no longer stop short of is taken first** (p1). A right of way orders who waits and
   never who is driven into, so it beats every rung.
2. **Below that it is the ladder** (TER-5g).
3. **Of one rung, a plan before a pass not yet begun** (TER-4c.6), **and otherwise whichever was asked first** —
   arbitrary, and the same for as long as the town asks in the same order.

**Nothing is remembered from one rebuild to the next**: who had a box last time is no term of who has it now, and a
car already in one keeps what it can no longer stop short of, and nothing more.

**The right of way is carried by the ground and not by the body.** One car holds the lane it is leaving at one
rung and the turn across the oncoming stream at another. **Straighter is stronger**: a stream that turns out of
nobody's way, then the near-side turn, which crosses nothing of its own carriageway, and last the turn across
the oncoming stream (TER-4a), the weakest movement a box admits because it is the last one there is (TER-5f).

**What a loser does about it is drive to the road it has left**, and there is no second mechanism (SIM-7). A
grant cut short inverts to a speed like any other, which is an ease-off where there is road and a stop where
there is none; **a car that cannot stop in what is left does not stop**, and what happens then is the solver's
rather than a rule's.

## The ladder

**TER-5g** `P4` **A plan is who holds it, where, and at what rung.** Low is strong, and the ladder is one
order: a call and a light are placed on it rather than carried beside it, so nothing reading the reservations
learns what an ambulance or a signal is.

- **p0 — a body.** The physical layer (TER-4c.2); never compared, so above every rung by construction.
- **p1 — ground its holder can no longer stop short of**, and a pass or a manoeuvre once begun (TER-4c.6): the
  piece of a plan it covers is asked at this rung, and taken before every other (TER-5e).
- **p2 — a call** ([agents/ambulance](../../../agents/ambulance/docs/requirements.md), `AMB-4`): ground
  somebody answering one plans to use, above everything a road carries of itself.
- **p4 — a light's hold** ([agents/trafficlight](../../../agents/trafficlight/docs/requirements.md),
  `TLT-1`): an approach past its bar while it is not green, and the paint of a crossing while it is red, as
  secondary claims of the way they are placed on and no other — above a walker and every movement, and
  below a call, which goes through it.
- **p5 — somebody on foot** (`PER-27`): every way a walker plans, one rung above the strongest movement and
  below a light. Nothing about it names a zebra: a car gives way on the paint because the paint's marks
  (TER-5c.3) bring this rung to its lane, and gives up the pavement its turn sweeps at a corner the same way.
- **p6, p7, p8 — the movements** (TER-5e): straight through, then ordinary traffic — the near-side turn and
  every way that is not a join through a box — and then the turn across the oncoming stream.
- **p9 — a car backing up** (TER-4c.7): below every movement and never level with one, so whatever else wants the
  ground behind it has it.

**TER-5g.1** `P3` **The rung never grows along a hold.** One holder's ground is one run from its front
outward (TER-5c.2), and nothing it holds further along is held more strongly than what it holds this side
of it: **the approach to a box is worth what the box is**, and a movement taken after a weaker one is worth no
more than that. A hold whose rung grew would win the far side of a junction and lose the road to it — and
cut there, give the far side up again for nothing.

## The only two ways

**TER-4c.5** `P3` **The physical and planned layers are the only ways two ordinary agents meet.** Nothing a
car or a walker decides reads another car or walker except through what is laid on the way it is on — no
look-up of another agent's state, no register of whose turn it is, and nothing one agent writes during the
decisions that another reads in the same walk of them. What is left outside is the town's own infrastructure and its
special agents, each named where it is made: **a light and a call**, which are rungs of the ladder, a light's
placed where it stands (`TLT-1`); **a closure**, which is an officer's body standing at the mouth of the
lanes it holds and those lanes out of every route and every tour (`SRV-9`, `SRV-10`); **a hand at the wheel
and a tow**, which move a car where they take it, holding what it can no longer stop short of like any moving
body (`S-7`, `EVA-5`); **a recovery**, which puts a body down only where nobody holds the ground (TER-4c.8);
**a pass**, which asks ground its holder has still to reach (TER-4c.6); and **the solver**, which is what two bodies
in one place come to. **A claim says where, and how strong; never who** — who laid one is for the instruments
alone.
