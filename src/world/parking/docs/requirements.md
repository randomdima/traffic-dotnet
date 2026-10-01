# Parking — requirements

What a bay is, where a lot may stand, and what must be true of one before a car aims at it. How a car drives
into and out of one is its own manoeuvre (`GEN-4f`).

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
the ends is a manoeuvre and what sizes the sides is a door, and a space wider than the lane it is worked off is
a bay reaching further across the street than the ground that serves it.

**What a space is wide sizes the car standing in it, and the ground it is laid on is the tarmac's.** A bay's
road is a lane's width (`GEN-53`) — one width for every metre of driven ground in the town, so a car park is
the same tarmac as the street beside it and the outside of the driven ground (`OBS-2p`) does not step in and
out at every bay — while **the ribbon it is read at is the space's width** (TER-4c.4, `TownWorld.LineOfWay`): a
car turning into one swings a corner over its neighbour's mouth, and the strip between two spaces is nobody's.

**GEN-4i** `P6` **A car stands square in the middle of its bay**, the clearance the space carries along its own
length shared between its nose and its tail. It is the pose a car is stood at wherever one is put in a
bay — a spawn, a service vehicle on its apron, a wreck set down in a yard slot — and the pose a manoeuvre into
it ends at (`GEN-4f`), and where the walk to that car is aimed (`GEN-4e`) is off the body at that pose. **The
space is the deepest bay-length of its road**, behind the apron it stands back from the street by
(`CityGenFigures.BaySetbackM`). **The body stands over the same ground either way round and the axle does not**
(`GEN-4j`, `BayTemplate.RearAxleIntoTheBayM`): nose in, the rear axle is half a wheelbase short of the middle of
the space, and backed in, half a wheelbase past it.

**GEN-4d** `P6` A lot keeps its distance, both figures measured **along the kerb it hangs off**: clear of a
junction, on top of everything the junction already takes, so a car park's flank is not in the face of
anybody waiting to turn out; and clear of the next lot, claimed along the lot's own bearing only and
tested both ways round the pair — two lots facing each other across a carriageway are the two sides of a
street and stay legal, while two sharing a kerb read as one long apron and do not. **What "clear" is worth
is the walk that wraps a lot** (TER-3c.3): two standing closer than two of those have one wrap between
them, which is the apron this refuses — and the verge they pinch out between them is a cusp nothing can be
given ground on.

## The manoeuvre at a bay, and which way round a car stands in it

**GEN-4f** `P4` **A car gets into a bay and out of one by a manoeuvre of its own, laid when it needs it, and the
one it makes is the one that takes the least of the street.** Nothing about it is laid with the town: a bay is a
short road joined to nothing (`GEN-53`), and the lines a car's rear axle is driven along into it and out of it
are shaped from where that car stands, on that car's own circle and for that car's own body
(`BayManoeuvre`, `CarBuild.ParkingTemplateRadiusM`, `CarBuild.ParkingStraightensUpM`) — so a long vehicle with a
wide circle makes a different manoeuvre into the same bay than a small car does.

- **Into a bay there are two shapes, and the least street wins.** Nose first — on down the lane, a swing away
  from the bay where the car stands nearer it than its circle, the turn in, and straight into the space — or
  backwards: on past the bay forwards, and back into it on one turn in reverse. Each is the car's body swept
  down it and read off the ground (`TER-4c.4`), a shape whose body leaves the driven ground or lies over a
  zebra's paint is no shape at all, and **of those left, the one whose body is swept over less of the street's
  own ways is the one made** (`TownWorld.ShapeTheWayIn`). The driver's habit settles a tie (`GEN-4j`).
- **Out of a bay there is one shape to each lane of its street**: straight out of the space, one turn, and a
  little straight along the lane the way the car will then drive it — reversing out of a space it stands nose
  first in, and driving out of one it stands backed into. **A car standing nearer a lane than its own circle
  lands past that lane's middle** by the least that lets the turn fit, and drives back onto it from there. **The
  route is laid before the car moves**, from both lanes at once, and the lane it sets off down is the one the car
  pulls out onto; where the car cannot be got onto that one, the route is laid again from the other before it moves
  (`TownWorld.LeaveTheBay`). Of two it could make with no route to choose, the one taking less street.
- **A car waits for its manoeuvre into a bay where its own turn in would begin** (`TownWorld.StopForTheBayM`):
  the line of a leg aimed at a bay stops there, on the lane the bay is worked off, and is what the car drives
  until the manoeuvre is had.

**Its ground is a pass's, and nothing else holds the street for it** (`TER-4c.6`, `SIM-7`):

- **Asked for whole, laid as a body, kept or withdrawn once.** Its ground is the car's body swept down every
  piece still to be driven, and it is asked for only where all of that is free — no body on it but the car's own,
  and no plan over it — with the pass's own room to spare. Short of that the car lays nothing and waits, holding
  no more of the street than its body does.
- **Once begun it holds that ground until it has driven it**, at p0 where nothing takes it, given back as it is
  driven: a car half across a street cannot give the street back, so whoever is coming is held short of it by
  the ground and by nothing else. What stops the car itself is a body standing in what is left of it.
- **A car backing into a bay asks for the whole of it before it pulls past the bay**, so whoever follows it is
  held short of the ground it reverses over rather than stopping on it behind the car.

**GEN-4j** `P5` **A car stands in a bay one of two ways round, and reverses only in a manoeuvre at a bay**
(`CAR-6.5`). Nose first, it reverses out; backed in, it drives out. Each shape ends at the axle's own pose in the
space (`GEN-4i`).

- **Which way round a car is stood in a bay is its driver's habit**, drawn once per car — a spawn, a service
  vehicle on its apron — and the same habit settles which way it parks where both shapes take the same street.
- **The standing is read off the pose and never off the register.** Which way a car standing in a bay leaves,
  which flank its driver's door is on (`GEN-4e`), and which end of the body leads out of the space are all
  answered from the direction the body is actually pointing.

## Turning round in a bay

**GEN-4l** `P5` **A car that has to come back the way it came turns in a bay: it parks and it comes straight back
out.** No junction admits a movement that reverses the direction of travel (TER-5f), so a bay is the one place a
car is turned round. **The router turns a leg at the end of a lane**, so a lane is one a leg may come back from
only where a bay off it stands near its end (`SimConfig.TurnAtALotWithinM`, `BayStreets.WhereALegMayTurn`), and
the bay the car turns in is **the one furthest along that lane**: turned further back, a car lands on the lane
running back further along it than its route was planned for. **The router also lets a leg come back from a dead
end**, and nothing turns a car round there: it stands at the end until its leg's clock gives the leg up
(`CAR-15a`).

- **It is a manoeuvre in and a manoeuvre out and nothing new** (`GEN-4f`): in off the lane the car is coming
  down, and out onto the lane running back, whichever way the place it is going to lies — the route out of the bay
  laid from that lane alone.
- **The bay is held while the turn is made, and that hold is a second claim of the same kind** (`GEN-4g`). A leg
  turning keeps the place it is going to — the destination has not changed, only the way round to it — and gives
  the turning bay back the moment it is out of it. Every way a leg can end gives back both.
- **A frontage with nothing free is not a leg that has failed.** The car drives on and asks again from wherever
  it gets to, because a body standing at a full car park waiting for a bay is an obstruction the street queues
  behind — and on a street whose bays are freed by the cars in that queue, a jam that cannot clear.
- **What the router knows is which lanes have a bay near their end at all**, and never which bay is free: the
  first is a fact about the town, laid with it; the second is a fact about this moment, and it is asked at the
  frontage by the leg that has got there.

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
nearest them, up to the figure — and a depot its evacuators' bays and its yard (`EVA-2`). Each bay of an
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

**GEN-4h** `P4` **A car park is a rank of bays beside a street that stays whole, and no bay is joined to
anything** — laid as `GEN-53` lays it ([citygen](../../../citygen/docs/requirements.md)): the street is not
parted, no junction stands at a car park, and what gets a car from the street into a bay is its own manoeuvre
(`GEN-4f`). Three consequences:

- **A leg aimed at a bay is routed to its street**, the place on each lane of it the bay's mouth stands abeam
  of (`BayStreets`), which is an ordinary route over the town's own lanes. **A bay is never a lane a route or a
  tour runs down**: nothing joins one, a tour never draws one (`LaneTour`), and a car taking the lane under it
  takes the carriageway's (`RoadGraph.NearestStreetLane`).
- **A bay is a lane of the town all the same**, so a body standing in it is laid on it like on any other
  (TER-4c.2) — an occupied bay is a fact the town reads rather than a flag it is told, and a manoeuvre into a
  neighbour's space is refused by the body standing there and by nothing else.
- **A car park still needs room**: a locality clear of every junction on the road and of every other car park
  (GEN-16), and a street straight enough there to square a rank to (GEN-53). A stretch of street without that
  room carries buildings rather than a car park.

**GEN-4g** `P4` **Which bay a leg is aimed at is a claim, and it lives in a register.** It is a hold on no
piece of road, and it is a register because it has to be: the hold is taken when a leg is sent to the bay
or re-aimed at another, before the car has a line to it and over ground it holds no claim on. It says which
bay and nothing more — a bay is free when a street runs past it, nobody has claimed it, nobody is standing or
turning in it, and nothing holds it (`GEN-4k`), and everything about the ground between the car and that bay is the road's own
claims (`TER-4c.1`). The bays are indexed by where they stand, because what is asked of them is *the free
bays near a place* — an apron's building, an order's point, where a leg has got to.
