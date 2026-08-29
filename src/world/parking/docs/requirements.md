# Parking — requirements

What a bay is, where a lot may stand, and what must be true of one before a car aims at it. Which bay a
trip claims is the walker's ([agents/person](../../../agents/person/docs/requirements.md)); how a car
gets into one is the driver's catalogue
([agents/car](../../../agents/car/docs/requirements.md)).

The `GEN-` rules below are laid by whatever generates a town. **This project reads plans and does not lay
them** ([citygen](../../../citygen/docs/requirements.md)), so they bind the exporter and are checked here
only in the sense that a bay which fails them cannot be used.

**GEN-4** Every parking space is reachable by car from the road network and by pedestrians from walkable
terrain, and is **enterable *and* exitable by a legal manoeuvre**, reverse permitted.

**GEN-4e** **The way in is the bay's and not the car's**: where a walker is aimed to reach a car parked in
a space is a fact about that space, settled with the ground it was painted on, and it is the ground off the
driver's door of a body standing square in it. Read instead off wherever the car has actually come to rest,
the point moves whenever anything nudges the body, and a walk already under way is re-planned round the lot
by a shove nobody chose. A space has one such point per standing (`GEN-4j`), on opposite flanks, and which
of them a walk aims at is the way round the car is facing.

**GEN-4b** Parking is laid as **lots** — a handful of spaces each, every space square to its kerb — and
the count is whatever satisfies the relation that matters: **every building stands within a walking
distance of a lot**. A lot is an oriented rectangle laid along the chord of the kerb it hangs off, offered
only where that kerb stays close to its own chord over the lot's length. The promise is not "a lot per
building" but a density: any scan of frontage carries roughly as many bays as buildings.

**GEN-4c** A parking space exceeds the car footprint by the clearance margin on all sides, and all of
that ground is the lot's.

**GEN-4i** **A car stands square in the middle of its bay**, the clearance the space carries along its own
length shared between its nose and its tail. It is the pose the bay's ways end at, so it is what the
manoeuvres are solved *to* and not a matter of comfort, and where the walk to that car is aimed (`GEN-4e`)
is off the body at that pose. **The body stands over the same ground either way round and the axle does
not** (`GEN-4j`): the ways are drawn for the axle, so a bay backed into is reached over a way that runs a
wheelbase's half deeper into it. **What the depth behind the tail has to be worth is the street's own
crossing**: the outermost metres of a bay are ground the lane beside it is driven over, and a body standing
inside that ground would cut the street it is parked beside rather than being clear of it.

**GEN-4d** A lot keeps its distance, both figures measured **along the kerb it hangs off**: clear of a
junction, on top of everything the junction already takes, so a car park's flank is not in the face of
anybody waiting to turn out; and clear of the next lot, claimed along the lot's own bearing only and
tested both ways round the pair — two lots facing each other across a carriageway are the two sides of a
street and stay legal, while two sharing a kerb read as one long apron and do not.

That every space is demonstrably enterable and leavable is `VER-2`, in
[docs/verification.md](../../../../docs/verification.md#the-verification-intentions).

## The ways at a bay, and which way round a car stands in it

**GEN-4f** **A bay is reached over the town's own ways, and the way in is the way out.** One shape is
solved per standing and per lane the bay can be worked off — **laid once with the town and not per car**,
like the join across a junction — and it is carried as the pair of ways that shape is driven as: in from
the lane, and out to it. Every one of them is drawn for the rear axle, carries metres of its own, and is in
the town's table of what is driven over what (`TER-5c`): a car working into a bay is held off the traffic,
and the traffic off it, by the ground each of them holds and by no second mechanism (`SIM-7`).

Five consequences, and the last three are the reason for the rule:

- **The last dozen metres of a leg are driven, not manoeuvred around.** A route's line finishes on the way
  in, so the whole of a leg is one chain over the town's ways and a driver working into a bay is a driver
  on a way.
- **A way is the manoeuvre and not the approach to it.** It begins where the car stops driving straight
  down the lane; the metres before that are the lane's own, driven under the lane's own reservation and not
  driven back up on the way out.
- **A bay that can be driven into can be driven out of**, because it is the same line — a shape laid at all
  is laid in both directions off the lane its standing is settled on (`GEN-4j`). A way out lands on the
  lane's own centreline by construction rather than by a second solve aimed back at it, and there is no
  overshoot to allow for and no bay whose two answers disagree.
- **Either lane of the carriageway, and the arithmetic says which of them lay.** A bay standing far enough
  off the lane beside it is turned into off that lane; one standing nearer is turned into off it over a
  template that swings away before it turns in. The oncoming lane is asked the same question and kept where
  it answers — so a car may work into a bay from either side of the street, **crossing the carriageway to do
  it**, which is what a driver does and what the table holds everyone else off. What it may do from over
  there is bounded by `GEN-4j`. A bay whose geometry admits no line at all is a bay no trip may claim, and
  that is the whole of what "cannot be reached" means.
- **Leaving a bay is a movement like a junction's and is nothing else.** The car drives the town's own way
  out; its reservation runs along that way; the ground where the way crosses the street is taken before
  the car moves onto it and given back where its body is past it — the protocol of `TER-5c.1` with a bay's
  way for the join. There is no gap looked at, no patience spent and no wait of its own, because a bay is a
  place a car gives way at and the town already knows how one of those works.

**GEN-4j** **A car stands in a bay one of two ways round, and reversing happens between that bay and the
lane beside it and nowhere else.** Nose first, it drove in and must reverse out; backed in, it reversed in
and drives out. The two are different shapes rather than one shape driven differently — the axle they end
at is a wheelbase's half either side of the middle of the space, and the approach runs up the lane for one
and back down it for the other — and each is a pair of ways all the same, one driven under power and one in
reverse.

- **The near lane is what a standing is settled off**, because a standing needs both its ways and only the
  lane beside the bay lays both. A bay that lays neither is a bay with no way (`GEN-4f`).
- **How far over the street that manoeuvre reaches is the table's question and not a second bar here**
  (`SIM-7`). The turn into a bay swings away from it before it turns in, which is out over the carriageway,
  and on a four-metre lane the swing carries the body over the centreline. What that takes of the oncoming
  lane is measured for a bay's way exactly as it is measured for a junction's join (`TER-5c`), and whoever
  is coming the other way is held off it by that and by nothing else. A bar held up before the measurement
  — the body clear of the oncoming lane's own paint — refuses shapes the table has already found nobody
  meets on, and refusing a shape costs the bay the standing it served: on a four-metre lane, every nose-in
  in the town, which is also every way in a car can take from across the street.
- **The far lane is kept in the forward direction only.** A car may nose into a bay across the carriageway,
  and one that backed in may drive out across it. Neither reverses over a lane of moving traffic to do it.
  So the far lane adds an approach and a departure and never a standing of its own.
- **Which way round a driver parks is a habit and not a decision**, drawn once per car, so the two askings
  that lay a leg's line agree. A bay that lays only the other standing overrules it.
- **The standing is read off the pose and never off a booking.** Which way a car standing in a bay may
  leave, which flank its driver's door is on (`GEN-4e`), and which end of the body lies along the way it
  stands on are all answered from the direction the body is actually pointing.

## Turning round in a bay

**GEN-4l** **A car that has to come back the way it came turns in a bay: it parks and it unparks.** No
junction admits a movement that reverses the direction of travel (TER-5f), so this and a dead end
(`P-19`) are the two ways round a town has, and this is the one an ordinary street offers.

- **It is the bay's own two ways and nothing new** (`GEN-4f`): the way in off the lane the car is coming
  down, and the way out onto the lane running back. Both are of the book, so the traffic is held off the
  car and the car off the traffic by the ground each holds, exactly as at any other park.
- **The standing is the turn's and not the driver's habit** (`GEN-4j`). Only one standing comes out the
  other way off a given lane: nose in across the carriageway and reverse out onto the kerb-side lane, or
  back in off the kerb-side lane and drive out across the carriageway. Which one a frontage offers is the
  arithmetic's, and where it offers both the habit settles it.
- **The bay is held while the turn is made, and that hold is a second booking** (`GEN-4g`). A leg turning
  keeps the place it is going to — the destination has not changed, only the way round to it — and gives
  the turning bay back the moment it is out of it. Every way a leg can end gives back both.
- **A frontage with nothing free is not a leg that has failed.** The car drives on and asks again from
  wherever it gets to, because a body standing at a full car park waiting for a bay is an obstruction the
  street queues behind — and on a street whose bays are freed by the cars in that queue, a jam that cannot
  clear.
- **What the router knows is which stretches lay the pair of ways at all**, and never which bay is free:
  the first is a fact about the town, laid with it; the second is a fact about this moment, and it is
  asked at the frontage by the leg that has got there.

**GEN-4k** **A special building's bays are held for its own vehicles and for nobody else.** A hospital and
a police station ([agents/ambulance](../../../agents/ambulance/docs/requirements.md),
[agents/service](../../../agents/service/docs/requirements.md)) each keep an **apron** — the free bays
nearest them, up to the figure — and each bay of one is held for the single vehicle stood in it, for the
whole run and not only while that vehicle is in it. Three consequences:

- **A hold is not a booking** (`GEN-4g`). A booking is what one leg has and every way a leg can end gives
  it back; a hold outlives every leg its vehicle drives, because the point of it is the bay being there
  when the vehicle comes back.
- **A held bay is free to its holder and to nobody else**, which is the whole of the mechanism: it is
  refused to every trip, to every retarget and to every spawn by the one question those already ask, and
  there is no second register of who may park where (`SIM-7`).
- **An apron is claimed before the town's own cars are stood, and filled once they have been.** The ground
  is taken first, so a bay a plan's car already stands in is never one a station wants; and the vehicles go
  in afterwards, so every bay of an apron holds its own service vehicle from before the first tick and no
  ordinary car ever stands among them. A plan car whose space was taken this way is stood in the nearest
  free bay instead, and where there is none it is not stood — which is the whole of what an apron costs the
  plan.
- **An apron stands along one kerb, and that kerb is the building's own where the map has one.** Every bay
  of an apron is on the same side of the road as the first — a yard is a yard and not two halves of a
  street — and the first is looked for on the building's own side before it is looked for anywhere. Only
  where its own side carries no free bay at all does an apron cross the road, which shipped maps do make
  necessary: a building whose only parking is over the road from it is a building, not a mistake.
- **An apron takes the bays the map has**, which is the bays still free on that kerb: the plan's own cars
  are stood before it and it takes none of theirs. A building with fewer bays near it than the figure asks
  for stands fewer vehicles, and one with none stands none — which is a real state and is reported
  (`AMB-2`, `SRV-2`).

**GEN-4h** **A parking section is a stretch of the road network in its own right.** The road it hangs off
is cut at either end of it, so the frontage its bays are reached over is bounded by two nodes of the graph
and a leg aimed at one of those bays is routed to a node like every other leg. Three consequences:

- **A section is a stretch and therefore has two nodes, not one.** Its bays stand along tens of metres of
  kerb and are reached from both directions, so no single point on the road has all of them ahead of it.
  The cuts are set back from the frontage by the run-in a way in is staged over — at both ends, which is
  also what a bay backed into needs, since that one is staged past the bay rather than short of it
  (`GEN-4j`) — so the last dozen metres either side of a bay are the section's own ground. Lots whose setbacks touch — the two sides of one street, or two
  lots closer together than the run-in — are one section.
- **A place is a cut and not a disc.** Its two lanes meet at a point: no ground is taken off the road, the
  movement between them is a join of no length, and there is therefore no box to be granted, to be refused
  or to stop short of. A junction has a disc because two carriageways cross there; nothing crosses here.
- **It gives way to whatever the road already carries, by moving outward.** A cut may not stand on a
  junction's own ground, on a zebra or on a bar — a lane end inside a crossing hands the paint to the lane
  after the one a driver is braking on — nor leave a stretch too short to drive. Where the place it was
  asked for is one of those, it moves **away from its own frontage and never into it**, because the metres
  between the cut and the first bay are the run-in that bay's way in is staged over. A section with no room
  on its road for either cut keeps the node the road already ends at.

**GEN-4g** **Which bay a leg is aimed at is a booking, and a booking is a register.** It is the one hold in
the town that is not a piece of road, and it is a register because it has to be: the hold begins when the
trip picks the bay and the walker sets off, which is minutes before anybody is at the wheel and over ground
the car has no line to. It says which bay and nothing more — a bay is free when nobody has booked it and
nobody is standing in it, and everything about the ground between the car and that bay is the road's book
(`TER-4c.1`). The bays are indexed by where they stand, because what a trip asks is *the free bays within a
walk of this door*.

## What this slice must produce

- The ways at every bay and the standing each of them serves (`GEN-4j`), <b>which stretches a leg may turn
  at</b> (`GEN-4l`), and the sections of the carriageway and of its neighbours' ways that each of them is
  driven over — measured with the same code a
  junction's joins are measured with.
- The choice layer: the free bays near a place, nearest first, off an index of where the bays stand.
- The booking (`GEN-4g`), the turn's own (`GEN-4l`) and the hold (`GEN-4k`): which bay each leg is aimed
  at, which one it is turning in, which bay each car has been left in, and which bay belongs to which
  vehicle for the whole run.
- Where a walk to a car left in a bay is aimed (`GEN-4e`).

Where the road is cut for a section (`GEN-4h`) is the road's own, in
[world/road](../../road/docs/requirements.md): a cut is what makes a lane, so it is settled before there
are lanes to read.
