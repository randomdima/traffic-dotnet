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

**TER-4c.4** `P3` **Every way of the town is a ribbon, and the ribbons are laid once, onto one lattice.** A
ribbon is its way's line swept to **the width of what travels it** — a car on anything driven, a body on
anything walked — so a ribbon is the ground that way's traffic actually covers and never the paint it is laid
between. The town samples every ribbon onto a lattice of points when it is laid, and each point knows which
ways it lies under and how far along each of them it stands. **That lattice is the whole of the geometry the
reservations ever use**: which ways a collider is over is a look-up of the points inside it, and which ways
share ground is a look-up of the points two ribbons both hold.

- **A ribbon is laid a lattice's reach wider than its band**, the furthest any ground is from the point
  standing for it — so a body over the band is never between two points, and two ribbons marked against
  each other are always two ways a body on one could be read onto the other from.
- **How fine the lattice is, is data**, and it is sized against the ground it must not miss: an overlap
  thinner than its diagonal can fall between points, so that diagonal is held under the narrowest thing a
  body must be seen straddling.
- **Nothing the town's furniture stands on is a ribbon anything drives** (TER-4c). A prop is not a body and
  holds no reservation, so a town that stood one on a driven ribbon would have traffic that could not see it —
  and it is refused when it is laid, rather than driven round.

**TER-5c** `P4` **Two ways share ground where their ribbons overlap, and the town marks every such pair
once.** A **mark** is one piece of shared ground read from both sides: this stretch of one way and that
section of the other. It is found from the lattice, filed under both ways, and never asked again. **It is a
table of ground and not a table of verdicts**: a relation saying two movements conflict answers one question
for a whole junction, so a car crossing one corner of a box shuts the far corner it never reaches, where a
mark says *where*, and nobody is weighed against anybody over ground they do not share.

What follows from that rather than being stated beside it:

- **The property belongs to the ways and not to the intersection.** A street bending through a junction
  shares nothing with the other street's straight if their ribbons do not meet; a turn across the oncoming
  stream shares exactly the metres it crosses.
- **Ribbons laid edge to edge share no ground.** The two lanes of a carriageway and a lane and the way that
  carries on from its end touch, and are not marked.
- **Ground two networks share is marked like any other.** A zebra's ribbon lies over the lanes it is painted
  across, and the mark between them is the whole of how somebody on the paint and a car in the lane meet.

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
- **A body says whether it is travelling the way it is on.** A car on a way of its own line, or a walker on
  the way it is walking, is a queue to whoever comes up behind it; anywhere else it is standing there, going
  nowhere down it. The holder says it of itself, so no reader looks another agent up to find out.
- **A coupled pair is one occupant** (`EVA-5`): the car on the bar is laid under the vehicle pulling it, so a
  truck's own plan is never cut at its own trailer.
- **A walker is always on the way it walks**, over the stretch its own body takes of it — the one way it must
  never be missing from, however the lattice falls under a body narrower than it.

## Plans

**TER-4c.1** `P3` **Ground is asked for, answered, and then laid.** A holder asks for the stretch of its own
line it means to use, from its front forward, a way at a time; **the answer is read before anything is
laid**, so no ground is taken off another plan for a hold that then does not use it. What comes back is
that stretch cut at the first of three things:

- **the first body in front of it** on any way it is laid on — a plan is never laid over somebody standing
  there, and a body beside or behind the holder cuts nothing;
- **the first metre another plan keeps against it** (TER-5e), on its own way;
- **the first mark whose section another plan keeps against it** (TER-5c.1).

**Part of what was asked for is the ordinary answer** rather than a refusal, and a holder granted none of it
stands still. **What comes back is the holder's to move into**: whatever is laid on it later is weighed against
it, and only something that beats it takes it.

- **A plan begins at its holder's front.** Ground behind the nose is ground the body is on or has passed,
  never road it was granted, so nothing behind the nose can be what ends a grant.
- **A rule that stops a body stops its plan.** A red, a bar, a crossing a car must stop short of: the plan is
  not laid past the place the body is held at, so a car waiting at a red holds none of the box beyond it.
- **A plan reaches as far as its holder means to be able to stop**, and no further. For a driver that is the
  ground it can no longer stop short of, the room to pull away, and — while it is moving — what it reaches over
  a stated run pulling up to the speed it is planning for and a stop from there; a body at rest plans the
  room to pull away and nothing more, so a queue waiting at a junction plans none of the box. For a walker it
  is the ground it would come to rest in from its pace (`PER-26`), and a crossing to the far kerb (`PER-27`).
- **A car in a box plans its way out of it**: at least to the far side of the join its nose is on and its
  own length past that, however slowly it is going.
- **A car refused ground where it would come to rest across another movement waits at the mouth** — where it
  can still be brought to rest there — and never on the ground it was refused. The mouth is short of the
  answer, so waiting there takes nothing the answer did not give.
- **Nothing is ever released.** Every plan is laid again from its holder every tick, so a body that stops,
  is wrecked or is taken over by a hand plans nothing on the tick after.

**TER-5c.1** `P3` **To hold a marked stretch of its own way, a holder holds the whole of the section the mark
links it to — or it holds neither.** A plan that cannot have the section is answered at the start of its own
side of the mark; a plan laid over a mark writes the section onto the other way as its own, whole; and a plan
whose section is taken from it later is cut at the start of its own side. **That is the whole of how two ways
that share ground meet**: every reader reads only the way it is on, because whatever lies over that way has
already been settled onto it.

- **Two linked sections on one way are no answer to each other.** They are two holders whose ground each lies
  over a third way, and where their own grounds overlap their own ways are marked against each other and meet
  there — so held against each other on the third, two cars would be refused a corner of pavement neither of
  them drives.
- **A holder writes pieces only onto the ways of its own line**, and onto the ways it only crosses nothing but
  the linked sections. A car approaching a box holds no fan of joins it is never going to be on.

**TER-5c.2** `P3` **A hold is one stretch of its holder's line.** Its pieces, read in the order the line runs
over them, each begin where the one before ended; **cut anywhere, it gives up everything past the cut** — the
pieces on the ways after and the sections it wrote through their marks. A hold with ground beyond a gap in
itself is ground whose holder cannot be seen coming, and ground past a place it was refused is ground it could
not have reached without crossing what it was refused.

**TER-4c.3** `P3` **No metre of any way is planned by two holders.** A plan meeting another on one way either
keeps the ground, and the other is cut back to where the two met, or is cut itself; the two abut on an exact
metre and neither reaches into the other. **Two linked sections are the one exception** (TER-5c.1). **Bodies
are not in this at all** (TER-4c.2), and neither is a holder's own ground against itself.

**Which of two keeps a metre does not turn on which was laid first**: the comparison is total and symmetric
(TER-5e). What a cut frees is not handed back inside the tick — a plan cut by ground a later one took away is
laid again, whole, the tick after.

## Right of way

**TER-5e** `P3` **Where two plans meet on one piece of ground, one comparison says which keeps it**, in this
order:

1. **Ground its holder can no longer stop short of is taken by nothing.** A right of way orders who waits and
   never who is driven into, so it beats every rung.
2. **Then ground its holder is already standing on.** A plan over metres somebody's body is on cannot be
   driven until that body has left them, and held against that body it is two holders each waiting for the
   other.
3. **Below that it is the ladder** (TER-5g).
4. **Then a box already given**: of two equal movements, the one that won the box last time keeps it, so a
   box does not change hands under a car on its way into it because another came nearer since.
5. **And last, whoever gets there first** — the holder with less of its own line to cover before the ground in
   question — and two exactly as near by roster and occupant, arbitrary and the same every tick.

**Neither the ladder nor the box is asked between two holders that can no longer stop.** Both are going in,
and what is left to settle is who is there first: the one further off is the one with road left to brake on.

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
order: a closure and a call are placed on it rather than carried beside it, so nothing reading the
reservations learns what a police car or an ambulance is.

- **p0 — a body.** The physical layer (TER-4c.2); never compared, so above every rung by construction.
- **p1 — ground its holder can no longer stop short of.** Carried by a plan rather than laid as a rung of its
  own, and beaten by nothing (TER-5e).
- **p2 — a call** ([agents/ambulance](../../../agents/ambulance/docs/requirements.md), `AMB-4`): ground
  somebody answering one plans to use, above everything a road carries of itself.
- **p3 — a closed road** ([agents/service](../../../agents/service/docs/requirements.md), `SRV-6`): ground a
  police car at a scene holds shut round it, above every ordinary plan and below a call, which is the whole
  of what lets the other services through a road that is shut.
- **p4 — a crossing somebody is walking** (`PER-27`): the paint to the far kerb and, through the marks, the
  lanes under it — above every movement a box admits, so a car gives way to somebody on a zebra and a walker
  at the kerb is not cut short of one by the traffic the zebra gives way to.
- **p5, p6, p7 — the movements** (TER-5e): straight through, then ordinary traffic — the near-side turn and
  every way that is not a join through a box — and then the turn across the oncoming stream.

**TER-5g.1** `P3` **The rung never grows along a hold.** One holder's ground is one run from its front
outward (TER-5c.2), and nothing it holds further along is held more strongly than what it holds this side
of it: **the approach to a box is worth what the box is**, a movement taken after a weaker one is worth no
more than that, and the pavement to a zebra is worth what the paint is. A hold whose rung grew would win the
far side of a junction and lose the road to it — and cut there, give the far side up again for nothing.

## The only two ways

**TER-4c.5** `P3` **The physical and planned layers are the only ways two ordinary agents meet.** Nothing a
car or a walker decides reads another car or walker except through what is laid on the way it is on — no
look-up of another agent's state, no register of whose turn it is, and nothing one agent writes during the
decisions that another reads in the same walk of them. What is left outside is the town's own infrastructure and its
special agents, each named where it is made: **a light**, which is a clock and clips the plan of whoever it
stops; **a call and a closure**, which are rungs of the ladder; **a hand at the wheel, a recovery and a
tow**, which place a body where they put it; and **the solver**, which is what two bodies in one place come
to.
