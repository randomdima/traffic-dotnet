# Reservations — requirements

**How the town's ways are held, and who gives ground up.** One subject: where a body is, where it means to
be, what two ways share, and which of two holders keeps a piece of ground. What a road, a junction and a
crossing *are* is [requirements.md](requirements.md); the ground itself is
[world/terrain](../../terrain/docs/requirements.md); why any of it reads this way is
[decision-log.md](decision-log.md).

**There are two layers over one numbering of ways, and they are the only two things an agent reads of
another.** The **physical** layer is where the bodies are; the **planned** layer is where they mean to be.
Both are rebuilt from the bodies every tick, both are intervals of one way's own metres, and **what two ways
share is worked out once, when the town is laid** — so nothing about either layer computes geometry while
the town runs.

## The ground

**TER-4c.4** `P3` **Every way of the town is a ribbon, and the ribbons are laid once.** A ribbon is its way's
line swept to **the way's own width** — a lane's, a join's, a bay's, a pavement's, the paint of a crossing —
with square ends. There is no other width a ribbon is laid at: what travels a way is a body on it (TER-4c.2),
and not a second, narrower ribbon inside it. The town samples every ribbon onto a lattice of points when it
is laid, and each point knows which ways it lies under, how far along each of them it stands and how far
outside each one's band. **That lattice is how a body is read onto ways**: which ways a collider is over is
a look-up of the points inside it. Which ways share ground is worked out from the ribbons themselves
(TER-5c).

- **The lattice files a point a reach past every band**, the furthest any ground is from the point standing
  for it, **and a body is on a way only where it reaches past a point further than the point stands outside
  the band.** So a body over the band is never between two points, and a body up to its edge is not on it.
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

**TER-4c.2** `P3` **A body is on every way whose ribbon its collider is over, at p0, over the stretch it
covers.** It is read off the lattice at the pose the solver left it in — the collider and never the drawn
picture, and never the square round it — so a car across the line between two lanes is on both, and one
turned across its own lane holds the corner of the next and not its own shadow down it.

- **Nothing about the write turns on what the body is doing.** Driven, parked, wrecked, under a hand or
  knocked down, a body in a way is in it; a body inside a building is in no way at all.
- **Bodies are never compared, cut or taken**, and two of them over one metre are both laid. The physical
  layer is a record of where things are, and two bodies over one metre is a collision — the solver's
  question (`PHY-1`), not a reservation gone wrong.
- **A body is one stretch of each way it is on** (TER-5c.2). Read at two places — a walker's disc and its
  place on the way it walks — it grows to cover both.
- **A body says whether it is travelling the way it is on**, and if so **which way its line takes next** — or
  that its line runs on down this way and is not yet laid past it, or ends where it stands. A car on a way of
  its own line, or a walker on the way it is walking, is a queue to whoever comes up behind it; anywhere else it
  is standing there, going nowhere down it. **And it says whether it is at rest.** The holder says all of it of
  itself, so no reader looks another agent up to find out. **A car that can neither get past what stands in front
  of it nor back up for the room to says its line ends where it stands** (CAR-50): to whoever comes up behind it,
  it is going nowhere.
- **A coupled pair is one occupant** (`EVA-5`): the car on the bar is laid under the vehicle pulling it, so a
  truck's own plan is never cut at its own trailer. **So are an officer out on duty and their car** (`SRV-11`):
  the officer is the car's closure on foot, and walks to the post beside the car — on the same lane, which a
  reservation has no across to tell apart — without being held off it.
- **A walker is always on the way it walks**, over the stretch its own body takes of it — the one way it must
  never be missing from, however the lattice falls under a body narrower than it.

**TER-4c.6** `P3` **Getting past something standing in the way is a pass, and a pass is laid as a body over
all the ground it will cover** — at p0, where nothing compares it, cuts it or takes it, because a holder that
has left its own way cannot safely go back. So it is asked for only where it can be had whole:

- **Only past a body at rest that is not making the asker's own movement**: not a pass itself, and not
  travelling that way on to the way the asker's own line takes next. Something waiting for what the asker
  would wait for is a queue, and is never passed — **but by a holder on a call**, since what the queue waits
  for is ground a call's rung takes. **A line not yet laid past the way makes every movement**, the holder's
  or the asker's: its holder is further than it can see from the way's end, so what holds it there is on the
  way.
- **A holder on a call gets past traffic that is moving, too**, where it goes slower than the call means to:
  what it gets past is then where that traffic can come to rest — the far end of the ground its plan says it
  can no longer stop short of — and not where it stands, since the pass laid past it is what it is then held
  short of. Never somebody on foot who is moving, who is crossing.
- **Over the lane beside and back**: the asker's own line moved across onto the way running back beside it,
  and back onto that line past what it passes, with room there to come back into. **A junction is no end to
  one**: the line runs on through a box, and so does the pass, wherever its ground can be had.
- **Its ground is the holder's own body swept down the whole of it and read off the atlas, as a body's is**
  (TER-4c.2): from where the holder stands to where it is back on its line, a body stood at every place the
  pass puts it and turned the way the pass turns it there, and whichever ways lie under those bodies are the
  ways it holds. It is laid on the network its holder travels and on no other — a car's on the ways the traffic
  drives, a walker's on the pavement — so neither holds the other's ground, and a pass whose body stands over
  ground the holder's network does not have is a pass run off it, and is not asked for. **The paint of a zebra
  is the one exception**, being the one ground the two share, and only a call's pass is ever over it (below).
- **A movement through a box is held whole**, as a car in a box plans the rest of the join: held in part, a car
  crossing the box is let in and cut in the middle of it, standing over the pass's ground on the movement beside
  its own, the pass and it each waiting on the other. **Whole against plans and other passes, and against a body
  only where the pass's own body goes**: holding the movement whole is about what is let into it, and a body
  standing on it clear of the pass is in nobody's way — where it stands in the box itself, it is what the pass is
  getting past.
- **Only over ground nobody has**: no body on any of it but the asker's, and no ground another holder plans,
  the plans of what is being passed aside — a body at rest plans the room to pull away and nothing it can no
  longer stop short of, and the pass laid over it cuts it. The paint of a zebra a walker's pass only skirts at
  the kerb is aside too: the traffic holds it whole wherever it crosses (TER-5c.3). **A car asks with room to
  spare** — as far as it is let stray off its steps — and lays and keeps without it, so a pass clearing
  something by a hair is not asked one rebuild and withdrawn the next.
- **A holder on a call asks at its call's rung** (`AMB-4.4`): a plan that rung beats holds none of the ground,
  and the pass laid over it cuts it as it cuts the plans of what it passes. What still refuses it is a body, ground
  a holder can no longer stop short of — what it passes among them — and another call's plan. **And a zebra is ground it may have**: its pass
  holds the paint whole, kerb to kerb on every walking lane, as a plan over any of it does (TER-5c.3), so nobody
  on foot steps onto it. Somebody already on the paint refuses nothing and withdraws nothing; the holder stands
  short of the paint until nobody is on it, or, already over it, drives on off it — held on the paint, it would
  stand across the way they walk, each waiting for the other.
- **Asked for, laid, then kept or withdrawn once**, in the rebuild after: withdrawn where a body has stepped
  onto the ground since, and — of two passes asked over one ground on one tick — kept by the lower of the two
  by roster and occupant. Kept, it is never given up, and **what it has covered is given back as it goes**: it
  is laid from where its holder stands every rebuild.
- **Whoever holds it plans from its far end**, the ground up to there being its own, and is held short of its
  end by nothing but a body standing inside it, or on paint it crosses. **And a pass holds nobody already
  standing on its ground**: its holder is held off them instead, since held against each other the two would
  each wait for the other.

How far a pass runs and how sharply it steps across is the holder's (`CAR-46`, `PER-28`); what it holds is
this rule's.

## Plans

**TER-4c.1** `P3` **Ground is asked for, answered, and then laid.** A holder asks for the stretch of its own
line it means to use, from its front forward, a way at a time; **the answer is read before anything is
laid**, so no ground is taken off another plan for a hold that then does not use it. What comes back is
that stretch cut at the first of two things, both read off the way asked about and off no other:

- **the first body in front of it** — a plan is never laid over somebody standing there, or over ground
  somebody's pass will cover (TER-4c.6), and a body beside or behind the holder cuts nothing;
- **the first metre another plan keeps against it** (TER-5e) — that plan's main claim on the same way, or a
  secondary claim it placed there (TER-5c.1).

**Part of what was asked for is the ordinary answer** rather than a refusal, and a holder granted none of it
stands still. **What comes back is the holder's to move into**: whatever is laid on it later is weighed against
it, and only something that beats it takes it.

- **A plan begins at its holder's front.** Ground behind the nose is ground the body is on or has passed,
  never road it was granted, so nothing behind the nose can be what ends a grant.
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
- **Nothing is ever released.** Every plan is laid again from its holder every tick, so a body that stops,
  is wrecked or is taken over by a hand plans nothing on the tick after.

**TER-4c.8** `P3` **Nothing moves over a way it has not claimed.** Every movement an agent's action makes — a car's
or a walker's, forwards or in reverse, down its own line, over the lane beside, into a bay or back onto its way — is
over ground that action asked for and was granted before it moved: a plan, cut where it was cut, or the whole of a
pass's or a manoeuvre's ground held as a body. **An action that claimed nothing is granted nothing** and stands, and
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
(TER-5g), and is answered from the tail down: the pass's spare short of the far edge of the first body behind it,
and at the nearest metre of any other plan there, since every one of them beats it. **It is asked once every other
plan is settled** and takes nothing any of them keeps, except where its holder is already rolling back: what it can
no longer stop short of is committed like anybody's (TER-5e). It never runs behind the start of the lane.

- **The plan of somebody queued behind it is the one exception**: a plan its own body cut runs up to it only because
  it stands there, and gives up whatever of that ground its holder could still stop short of, cut back to where the
  backing is laid from.

**TER-5c.1** `P3` **A plan is main claims and secondary claims, and only a main claim is ever answered.** A
**main claim** is ground on a way of the holder's own line. A **secondary claim** is the whole of the section
of another way that a mark links a main claim's stretch to, placed with the main claim and never asked for:
a main claim over any of its own side of a mark places all of the other side, and one answered short of the
mark places none of it. **Claims meet in two ways and no others** — a main claim and a main claim on one
way, and a main claim and a secondary claim placed on its way — and **two secondary claims never meet**.
That is the whole of how two ways that share ground meet: every reader reads only the way it is on, because
whatever holds ground that way shares has placed a secondary claim on it.

- **A main claim is answered off its own way alone.** A mark is filed under both its ways, so whatever holds
  the far side has placed its secondary claim over the near side, where the main claim asking meets it.
  Nothing on the far way is read, and a secondary claim placed there cuts nothing.
- **Refused by a secondary claim, a main claim is answered where that begins** — the start of its own side
  of the mark. A car in a turn comes to the section the oncoming straight placed over the turn, and waits
  there.
- **Taken, a secondary claim cuts its holder at its own side of the mark** — the metre its main claim placed
  it from (TER-5c.2).
- **Two secondary claims on one way are no answer to each other.** They are two holders whose ground each
  lies over a third way, and where their own grounds overlap their own ways are marked against each other
  and meet there — so two cars going opposite ways through a box, whose secondary claims lie over each other
  on the turns between them and touch neither one's main claim, both go.
- **A holder places main claims only on the ways of its own line**, and on the ways it only crosses nothing
  but secondary claims. A car approaching a box holds no fan of joins it is never going to be on.
- **A light's hold is a secondary claim placed with no main claim** (`TLT-1`): on the way it governs and no
  other, before any plan is laid, from its start to the first body travelling that way — so it meets the main
  claims on that way, and what crosses that way is held by nothing of it.

**TER-5c.2** `P3` **A hold is one stretch of its holder's line.** Its pieces, read in the order the line runs
over them, each begin where the one before ended; **cut anywhere, it gives up everything past the cut** — the
main claims on the ways after and the secondary claims placed past the cut. A hold with ground beyond a gap in
itself is ground whose holder cannot be seen coming, and ground past a place it was refused is ground it could
not have reached without crossing what it was refused.

**TER-4c.3** `P3` **No metre of any way is planned by two holders.** A plan meeting another on one way either
keeps the ground, and the other is cut back to where the two met, or is cut itself; the two abut on an exact
metre and neither reaches into the other. **Two secondary claims are the one exception** (TER-5c.1). **Bodies
are not in this at all** (TER-4c.2), and neither is a holder's own ground against itself.

**Which of two keeps a metre does not turn on which was laid first**: the comparison is total and symmetric
(TER-5e). **What a cut frees goes back inside the tick**: once every plan is down, each one another plan ended
is answered again against what the rest came to, and laid again, whole, where the answer has moved. So a plan
ends where something that beats it still stands, and waiting at a kerb binds whichever holder was laid first. A ring of holders each taking from the next never settles, and is left disjoint at a bound on the
work.

## Right of way

**TER-5e** `P3` **Where two plans meet on one piece of ground, one comparison says which keeps it**, in this
order:

1. **Ground its holder can no longer stop short of is taken by nothing.** A right of way orders who waits and
   never who is driven into, so it beats every rung. **The metre at a holder's own front is such ground, at
   rest as much as moving**: shared ground its front is already inside is ground it keeps, and what it places
   from there across a mark is held whole — so a car let into a box and cut short in the middle of it drives on
   out, and one stood with its nose on the paint drives on off it. It is the ladder's reading and no other: a
   pass still takes the plan of a body at rest, which will cover nothing (TER-4c.6).
2. **Then ground its holder is already standing on.** A plan over metres somebody's body is on cannot be
   driven until that body has left them, and held against that body it is two holders each waiting for the
   other.
3. **Below that it is the ladder** (TER-5g).
4. **And last, whoever gets there first** — the holder with less of its own line to cover before the ground in
   question — and two exactly as near by roster and occupant, arbitrary and the same every tick.

**The ladder is not asked between two holders that can no longer stop.** Both are going in, and what is left
to settle is who is there first: the one further off is the one with road left to brake on. **Nothing is
remembered from one rebuild to the next**: who had a box last time is no term of who has it now, and a car
already in one keeps what it stands on and what it can no longer stop short of, and nothing more.

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

- **p0 — a body.** The physical layer (TER-4c.2), and the ground a pass will cover (TER-4c.6); never
  compared, so above every rung by construction.
- **p1 — ground its holder can no longer stop short of.** Carried by a plan rather than laid as a rung of its
  own, and beaten by nothing (TER-5e).
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
- **p9 — a car backing up** (TER-4c.7): below every movement and never level with one, since a tie goes to
  whoever is nearer, and a car backing onto the ground behind its own tail always is.

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
placed before any plan is laid (`TLT-1`); **a closure**, which is an officer's body standing at the mouth of the
lanes it holds and those lanes out of every route and every tour (`SRV-9`, `SRV-10`); **a hand at the wheel
and a tow**, which move a car where they take it, holding what it can no longer stop short of like any moving
body (`S-7`, `EVA-5`); **a recovery**, which puts a body down only where nobody holds the ground (TER-4c.8);
**a pass**, which places a body over ground its holder has still to reach (TER-4c.6); and **the solver**, which is
what two bodies in one place come to.
