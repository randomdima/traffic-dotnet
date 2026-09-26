# Parking — requirements

What a bay is, where a lot may stand, and what must be true of one before a car aims at it. How a car drives
into and out of one is the driver's ([agents/car](../../../agents/car/docs/requirements.md) `CAR-15`).

The `GEN-` rules below bind whatever lays a town ([citygen](../../../citygen/docs/requirements.md)), and are
checked here only in the sense that a bay which fails them cannot be used.

**GEN-4** `P6` Every parking space is reachable by car from the road network and by pedestrians from walkable
terrain, and is **enterable *and* exitable by a legal manoeuvre**, reverse permitted. That every space
demonstrably is, is `VER-2` ([docs/verification.md](../../../../docs/verification.md#the-verification-intentions)).

**GEN-4e** `P4` **The way in is the bay's and not the car's**: where a walker is aimed to reach a car parked in
a space is a fact about that space, settled with the ground it was painted on, and it is the ground off the
driver's door of a body standing square in it. Read instead off wherever the car has actually come to rest,
the point moves whenever anything nudges the body, and a walk already under way is re-planned round the lot
by a shove nobody chose. A space has one such point per standing (`GEN-4j`), on opposite flanks, and which
of them a walk aims at is the way round the car is facing. **Nothing asks for it** (`ParkingRegistry.WayInM`):
no walk is aimed at a car, every trip being walked (PER-11).

**GEN-4b** `P6` Parking is laid as **lots** — a handful of spaces each, every space square to its kerb — and
the count is whatever satisfies the relation that matters: **every building stands within a walking
distance of a lot**. A lot is an oriented rectangle laid along the chord of the kerb it hangs off, offered
only where that kerb stays close to its own chord over the lot's length. The promise is not "a lot per
building" but a density: any scan of frontage carries roughly as many bays as buildings.

**A handful is a bounded number and the bounds are both ends of it**
(`SimConfig.CityGen.BaysPerLotFewest`, `…Most`): no lot in a town holds fewer than the fewest or more than
the most, and each of them draws its own count between the two. **The upper bound is what makes a lot a
lot.** A car park is a few spaces at the side of a street, and one that grew along its kerb until the
ground stopped it is an apron — it fronts several buildings at once, it puts a rank of parked cars where
the street's own frontage should be, and nothing in the town ever fills it. The lower bound is the other
end of the same statement: a rectangle of tarmac holding one or two cars is a lay-by that cost a lot's
whole clearance (GEN-4d).

**GEN-4c** `P6` A parking space exceeds the car footprint by a clearance on all sides, and all of
that ground is the lot's. **A rank of them stands side by side at that width, sharing the line between
each pair** — the room between two parked cars is the margin each of their bays already carries, and a
lot that counted it twice would be a row of detached bays with a stripe of bare tarmac down every join
and no line either of them shares.

**The clearance beside a car and the clearance at either end of it are two figures, and the side one is
bounded by the lane the bay is worked off** (`SimConfig.ParkingSpaceWidthM` under `LaneWidthM`): what sizes
the ends is a manoeuvre and what sizes the sides is a door, and a space wider than the lane its way is driven
out of is a bay reaching further across the street than the ground that serves it.

**What a space is wide sizes the car standing in it and never the ground driven to it.** A bay's way is a
driven line like any other and lays the band the lane it is worked off lays (`GEN-4f`) — one width for every
metre of driven ground in the town, so a car park is the same tarmac as the street beside it rather than a
narrower kind of it, and the outside of the driven ground (`OBS-2p`) does not step in and out at every bay.

**GEN-4i** `P6` **A car stands square in the middle of its bay**, the clearance the space carries along its own
length shared between its nose and its tail. It is the pose a car is stood at wherever one is put in a
bay — a spawn, a service vehicle on its apron, a wreck set down in a yard slot — and the pose a bay's way
ends at (`BayWays.AtTheBayM`), and where the walk to that car is aimed (`GEN-4e`) is off the body at that
pose. **The body stands over the same ground either way round and the axle does not** (`GEN-4j`,
`BayTemplate.RearAxleOfBayM`): nose in, the rear axle is half a wheelbase short of the middle of the space,
and backed in, half a wheelbase past it.

**GEN-4d** `P6` A lot keeps its distance, both figures measured **along the kerb it hangs off**: clear of a
junction, on top of everything the junction already takes, so a car park's flank is not in the face of
anybody waiting to turn out; and clear of the next lot, claimed along the lot's own bearing only and
tested both ways round the pair — two lots facing each other across a carriageway are the two sides of a
street and stay legal, while two sharing a kerb read as one long apron and do not. **What "clear" is worth
is the walk that wraps a lot** (TER-3c.3): two standing closer than two of those have one wrap between
them, which is the apron this refuses — and the verge they pinch out between them is a cusp nothing can be
given ground on.

## The ways at a bay, and which way round a car stands in it

**GEN-4f** `P4` **A bay is reached over the town's own ways, and the way in is the way out.** A bay's ways are
carried per standing and per lane as a pair — in from the lane, and out to it (`BayWays.PairOf`) — and each
is one of the town's own ways: numbered with the rest, carrying metres of its own, in the table of what is
driven over what (`TER-5c`) and driven as a line like any other (`CAR-15`). So a car working into a bay is
held off the traffic, and the traffic off it, by the ground each of them holds and by no second mechanism
(`SIM-7`). **Nothing lays one**: `BayWays.Build` lays no way, so every bay is one no way reaches
(`BayWays.CanBeReached`), and that is the whole of what "cannot be reached" means.

Four consequences of what is there, and the last is the reason for the rule:

- **A way runs the length of the space and is driven as far as the pose.** Its own metres reach the far end of
  the space it serves (`BayWays.LengthM`), so the whole space is on a way; the drive ends where the car comes
  to rest (`BayWays.DrivenLengthM`), which is a setback inside the way's own end exactly as a lane's is
  (`TER-5d`). The metres past the pose are ground and nothing else.
- **A body standing in a bay holds those ways, and holds them like any other body** (TER-4c.2). Its stretch on
  each is the box it stands in projected onto that way's own line, laid by the walk that lays a body onto a
  lane and onto a footway — not by an arithmetic of the bay's, and not from the register that says which bay
  it claimed. So an occupied bay is a fact the town reads rather than a flag it is told; a car standing across
  a bay it never claimed is on that bay's ways too; and what stops a driver aiming at the bay is the body at
  the end of the way, on the headway that stops it behind anything else. **A body and not a car**: a person on
  foot in a space is a stretch of that space's ways on the same terms.
- **The last dozen metres of a leg are driven, not manoeuvred around.** A route's line stops where the bay's
  way leaves its lane and the car takes that way as its next line (`CAR-15`), so a driver working into a bay
  is a driver on a way.
- **Leaving a bay is a movement like a junction's and is nothing else.** The car drives the town's own way
  out; its claim runs along that way; the ground where the way crosses the street is taken before
  the car moves onto it and given back where its body is past it — the protocol of `TER-5c.1` with a bay's
  way for the join. There is no gap looked at, no patience but the leg's own (`CAR-15a`) and no wait of its
  own, because a bay is a place a car gives way at and the town already knows how one of those works.

**GEN-4j** `P5` **A car stands in a bay one of two ways round, and reversing happens on a bay's own ways and
nowhere else** (`CAR-6.5`). Nose first, it drove in and must reverse out; backed in, it reversed in and
drives out (`BayWays.IsDrivenInReverse`). Each standing is a pair of ways all the same, one driven under
power and one in reverse, ending at the axle's own pose in the space (`GEN-4i`).

- **A standing needs both its ways** (`BayWays.CanStand`), and a bay that lays neither standing is a bay
  with no way (`GEN-4f`).
- **What a way takes of the street is the table's question and not a second bar here** (`SIM-7`): it is
  marked for a bay's way exactly as for a junction's join (`TER-5c`), and whoever is
  coming the other way is held off it by that and by nothing else.
- **Which way round a driver parks is a habit and not a decision**, drawn once per car, so the two askings
  that lay a leg's line agree. A bay that lays only the other standing overrules it
  (`BayWays.TheStandingOnOffer`).
- **The standing is read off the pose and never off the register.** Which way a car standing in a bay may
  leave, which flank its driver's door is on (`GEN-4e`), and which end of the body lies along the way it
  stands on are all answered from the direction the body is actually pointing.

## Turning round in a bay

**GEN-4l** `P5` **A car that has to come back the way it came turns in a bay: it parks and it unparks.** No
junction admits a movement that reverses the direction of travel (TER-5f), so a bay is the one place a car is
turned round. **The router also lets a leg come back from a dead end** (`BayWays.WhereALegMayTurn`), and
nothing turns a car round there: it stands at the end until its leg's clock gives the leg up (`CAR-15a`).

- **It is the bay's own two ways and nothing new** (`GEN-4f`): the way in off the lane the car is coming
  down, and the way out onto the lane running back. Both are the road's own, so the traffic is held off the
  car and the car off the traffic by the ground each holds, exactly as at any other park.
- **The standing is the turn's and not the driver's habit** (`GEN-4j`): the one whose way in leaves the lane
  the car is coming down and whose way out lands on the lane running back (`BayWays.TheWayToTurnIn`). Where
  a bay lays both, nose in is taken, being the one driven without stopping to change gear.
- **The bay is held while the turn is made, and that hold is a second claim of the same kind** (`GEN-4g`). A leg turning
  keeps the place it is going to — the destination has not changed, only the way round to it — and gives
  the turning bay back the moment it is out of it. Every way a leg can end gives back both.
- **A frontage with nothing free is not a leg that has failed.** The car drives on and asks again from
  wherever it gets to, because a body standing at a full car park waiting for a bay is an obstruction the
  street queues behind — and on a street whose bays are freed by the cars in that queue, a jam that cannot
  clear.
- **What the router knows is which stretches lay the pair of ways at all**, and never which bay is free:
  the first is a fact about the town, laid with it; the second is a fact about this moment, and it is
  asked at the frontage by the leg that has got there.

## The paint, the car park, and who holds a bay

**GEN-4m** `P6` **A bay paints the line it shares with the next bay, and nothing else.** A car park is a piece
of the town's tarmac like any other: the walk wraps it like any other, and the line round the outside of a
row of bays is the kerb line the pavement carries there
([world/terrain](../../terrain/docs/requirements.md) TER-3c.3, TER-3d). A stroke laid against that is the
same line painted twice, in the one place a driver is looking. What is left for a bay to say is the
boundary it shares with a neighbour — two side by side, two rows head to head — because the town's own
geometry says that nowhere. It is offered by both bays and painted once.

**GEN-4k** `P5` **A special building's bays are held for its own vehicles and for nobody else.** A hospital and
a police station ([agents/ambulance](../../../agents/ambulance/docs/requirements.md),
[agents/service](../../../agents/service/docs/requirements.md)) each keep an **apron** — the free bays
nearest them, up to the figure — and a depot its evacuator's bay and its yard (`EVA-2`). Each bay of an
apron is held for the single vehicle stood in it, for the whole run and not only while that vehicle is in
it; **a vehicle struck off its building leaves its bay held for nobody** (`ParkingRegistry.Claimed`,
`SRV-4`, `EVA-7`). Five consequences:

- **A hold is not a leg's claim** (`GEN-4g`). That is what one leg has and every way a leg can end gives
  it back; a hold outlives every leg its vehicle drives, because the point of it is the bay being there
  when the vehicle comes back.
- **A held bay is free to its holder and to nobody else**, which is the whole of the mechanism: it is
  refused to every leg, to every retarget and to every spawn by the one question those already ask, and
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
  where its own side carries no free bay at all does an apron cross the road.
- **An apron takes the bays the map has.** A building with fewer bays near it than the figure asks for
  stands fewer vehicles, and one with none stands none — which is a real state and is reported (`AMB-2`,
  `SRV-2`).

**GEN-4h** `P4` **A car park is a junction cut into the road, and every bay of it is an arm of that
junction** — the cut of `GEN-52`, which does not move the road, laid as `GEN-53` lays it
([citygen](../../../citygen/docs/requirements.md)): what a car takes to reach a bay is a movement across a
box like every other turn it makes, and a bay's own way (`GEN-4f`) is that arm's lane. Three consequences:

- **A car park has a node, and everything about one is decided on the road.** It is granted, refused and
  ranked by the rules a junction already carries (TER-5c, TER-5e) — so there is no second mechanism for
  turning into a car park, no bar held up before the manoeuvre and no register of who may cross what
  (`SIM-7`).
- **A leg aimed at a bay is routed to that bay's own arm**, which is an ordinary route over the town's own
  lanes ending on the lane the bay is.
- **A car park still needs room**: the ground its junction takes, a locality clear of every other junction
  on the road (GEN-16), and a road straight enough there to carry one (GEN-53). A stretch of street without
  that room carries buildings rather than a car park.

**GEN-4g** `P4` **Which bay a leg is aimed at is a claim, and it lives in a register.** It is a hold on no
piece of road, and it is a register because it has to be: the hold is taken when a leg is sent to the bay
or re-aimed at another, before the car has a line to it and over ground it holds no claim on. It says which
bay and nothing more — a bay is free when a way reaches it, nobody has claimed it, nobody is standing or
turning in it, and nothing holds it (`GEN-4k`), and everything about the ground between the car and that bay is the road's own
claims (`TER-4c.1`). The bays are indexed by where they stand, because what is asked of them is *the free
bays near a place* — an apron's building, an order's point, where a leg has got to.
