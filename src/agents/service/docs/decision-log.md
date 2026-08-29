# The service vehicles — decision log

## 2026-08-27 — a closed road is a rank and not a use, and it stands below a call

SRV-6 needed traffic held off a scene while other service vehicles drove through it. Three shapes were on
the table and only one of them left the rest of the town alone.

**A new `LaneUse` was the obvious one and the worst.** Every query in the road's book names the uses it is
about — what is traffic, what is a body, what is spoken for — so a sixth use is a decision in a dozen
places about whether a closure counts, and a closure that a query forgot is a street that shuts against
nobody or against everybody.

**Weighting the routing was the expensive one.** It would send traffic round rather than letting it queue at
the tape, which is more faithful, and it needs a second thing that has to agree with the book about what is
closed. Two answers to one question is what this project keeps refusing.

**So a closure is an ordinary claim at a rank of its own**, between the paint and a call. `Binds` already
reads the order, so ordinary traffic is refused and an ambulance is not, and the mechanism is one byte
comparison that was already being made. What "the officer lets the other services through" costs, in the
end, is a value inserted in an enum in the right place.

**And it turned up a defect while it was being fitted.** The grant on a car's *own* way was cut at every
spoken-for stretch without asking the rank, while the ways it was merely driven over asked properly —
so `AMB-4.1` was half implemented, a rescue was held by claims it outranks, and a closure would have shut
the road against the rescue it was put there for. `TER-5e` never drew that distinction; the own-way cut
asks now, which only ever widens a grant.

**The officer stands beside the road and not in it.** A body in the lane is a thing the rescue itself has
to be held off (AMB-4a) — the closure working backwards — and it would also have made him hold two stretches
of one way, which `TER-5c.2` forbids. Standing him at the kerb makes the two cases exclusive by
construction: in the lane he is a body, beside it he is a claim, and never both.

**And the closure's own bound came down to ten clocks, which is what everything else here is bounded by.**
It was written at twenty on the argument that a closure is not a leg. What that missed is that a closure
only buys something while somebody is *working* at the scene, and every errand that could be working there
is written off at ten — so the second half of a twenty-clock closure holds a lane for nobody. A city whose
evacuators cannot reach its wrecks (`EVA-8`) stood every one of them to the full ten minutes, and the test
that watches how long a crew is ever left in the street is what found it.

## 2026-08-27 — a crew is two, and what a vehicle is for stops depending on who is in it

SRV-3 used to say a crew never gets out, and that sentence was carrying two other rules on its back: CAR-1
acted because somebody was inside, and PER-4 was refused because somebody was inside. Both had to be given
their own answer before anybody could open a door.

**The errand acts, not the seat**, which is AMB-4b's move applied a second time — the light was already the
errand's rather than the vehicle's, and nothing about a call turned out to depend on the driver's presence.

**The building refuses the passer-by.** `IsAServiceVehicle` already asked both halves — the paint *and* the
hospital, station or depot it stands on the strength of — so PER-4's new answer was the half already
written, promoted from a predicate to a rule. A vehicle struck off its building (EVA-7) is an ordinary car
again, which is the same sentence read the other way.

**Two aboard rather than one who leaves.** One crew member who got out would have left the vehicle
undrivable in the middle of its own errand and would have made every question about it turn on a race. A
driver and a hand is the shape a real crew has, and the seats are a stride over the whole fleet like every
other register here — so "more if needed" is a constant rather than a rewrite. The driver is still only a
fallback: a hand knocked down at a scene is a casualty like anybody else, and the vehicle's next errand is
worked by whoever is left rather than never worked again.

**The recall is bounded and the bound is spent as a placement.** A pavement that will not give a paramedic
back would strand an ambulance mid-errand and leave a hospital one crew short for the rest of the run.
Putting the body in its own doorway past the bound is EVA-5's winch said of a person — the same fallback,
for the same reason, and named in SRV-3 rather than left as a surprise.

## 2026-08-26 — a depot wears the repair shop's roof, and that reverses what SRV-1a used to say

SRV-1a used to give a depot an ordinary roof on the argument that a depot is a yard and not a front door.
The argument was wrong about what the yard is: EVA-7 mends a wreck standing in one, so the workshop is the
whole point of the building and the yard is only where its work is parked. A town that draws a depot next
to a house and then fills the bays beside it with broken cars is showing the errand and refusing to name
it — the same thing AMB-1a refuses for a hospital.

**It is the third civic roof and it cost one line each.** The roof is found by id off `Civic.json` like the
other two, `Match` still cannot reach it, and the fitting, the quarter turn and the door-to-the-pavement
choice are `StandingSprites`' own and were already written for the hospital. What it did not need is a
fourth building use, a second uniform or anything at all in the recovery: the depot, its apron, its yard
and its crew are what they were, and only the picture over them changed.

**No second unit came with it.** The repair man is the recovery crew that is already aboard the evacuator
(SRV-3a), and the repair site is the yard the tow already hauls to (EVA-2, EVA-7). A repairman who lived in
the building and walked out to a van would be a second vehicle answering the one call a wreck raises, and
EVA-3 is written to keep exactly that from happening.

## 2026-08-26 — the uniforms are the person catalogue's own second list, and two facings are mirrored

A crew is a walker, so its look is a walker's look, and the same two-list shape the fleet already has
carries it: the uniforms sit past the end of the wrap ordinary walkers are drawn by, named by id at load.
The alternative — four more entries in `Catalog.json` and a rule somewhere else about which of them a
spawn may not be handed — is a second register of who may look like what, and the wrap is the register
already.

**What the uniform costs is one sheet each, and it is what is on screen at every scene.** A crew is drawn
only when it is on its feet — PHY-7 draws nobody who is inside anything — and a hand out working a call
(SRV-3) is on its feet for the whole of it. It is the body's own look rather than the car's, because the
moment it is the car's it disagrees with the body the town actually stood.

**The art is adapted rather than shipped as drawn.** Two of the three raw sheets were laid seven facings
by seven frames, and a facing is an octant here: the missing octant is the mirror of the one opposite it,
taken half a walk cycle on so the mirrored legs stay in step with the rest of the sheet. The frames were
found rather than assumed — the raw rows drift by a frame's own margin over a sheet — and each is
re-laid on one baseline and centred on the body's mass, so a stride no longer wanders sideways under the
walker. What it costs is that a badge worn on one shoulder is worn on the other in the mirrored facings.

## 2026-08-25 — the service list is a second list beside the fleet, and the wrap cannot reach it

A town's traffic draws its looks by wrapping an index over the fleet. The service variants sit past the end
of that wrap in one array, so a police car and an evacuator are sheet slots like everything else and are
still unreachable by accident: the seventeenth ordinary car cannot come out wearing a light bar. Naming
them by id at load rather than by position also means `Service.json` can be reordered without a car
changing what it is.

The alternative was a third catalogue with its own file, its own reader and its own sheet range. What that
buys is a second copy of everything `CarCatalog` already does, and what it costs is a renderer that has to
know which of two catalogues a car's variant came from.

## 2026-08-26 — the evacuator breaks, and the exemption it used to hold stayed in the format

The recovery truck was the one vehicle PHY-4b was true of, and the argument was that the thing sent to
clear a wreck off the road should not become one. What that actually bought was a town with one object in
it that nothing could ever happen to: it could be run into at any speed by anything, and the picture never
changed. A simulation whose only indestructible object is the tow truck is showing the player a rule about
bookkeeping rather than about the town.

It breaks now, with art of its own to break into, and the errand answers for it in one place: a wrecked
evacuator lets go of what it was pulling on the tick it breaks, because a coupling held by a body that is
taking no more ticks is a wreck being dragged by another wreck.

**PHY-4b and the `unbreakable` key stayed.** They are a fact about the damage rule and about the file
format rather than about this vehicle, they cost one field and one line, and nothing in the shipped
catalogue wears them — which is a thing the catalogue test now says out loud rather than a gap.

## 2026-08-25 — a beat is a drawn place and not a search, and it gained no manoeuvre

The patrol is the first errand that is not *for* anything. A rescue has a casualty to be at; a beat has
only the streets, so there is nothing for it to be aimed at and nothing to measure a good beat against.
Two designs were possible and only one of them is cheap.

**Drawn**: a place along one of the town's lanes off the car's own stream, a drawn number of them to a
beat, and home. It is a lane and not a junction because a leg ends by the car standing where it got to,
and the middle of a junction is the one place in this town where standing still is being driven into —
aimed at the junction centres, the fixture town's patrol was wrecked inside the first box it reached.
**Searched**: the quarter of the town nothing has driven through for longest, which needs a coverage map
kept per tick over the whole road network and a search over it on every arrival. What that buys is
something no one watching the town could distinguish from a shuffle, and what it costs is a structure the
size of the road graph and a walk of it on the hot path.

The beat therefore gained **no entry in the manoeuvre catalogue** (AGT-7) and no rule about the road. It is
`TownWorld.Ambulance.cs`'s machine with the urgency taken out — a stage per observable state, the leg
handed to the ordinary drive-leg machinery — and its whole difference from a walker's drive to work is
where the destination comes from.

## 2026-08-25 — a station keeps its bays, and standing four cars costs the town four places

A police station's apron is four bays held for four named cars for the whole run (GEN-4k), which is four
places ordinary traffic can never park in. The alternative — let the cars take whatever is free and come
home to whatever is free then — is what the single stood vehicle used to do, and it does not survive a
vehicle that actually leaves: a patrol that came back to find its bay taken would park somewhere else, and
within an hour the station's cars are scattered across the district and the station is a building nothing
stands at.

The hold is one array on the registry and one clause in the question everything already asks
(`IsFreeFor`), so a held bay is refused to a trip, to a retarget and to a spawn by the mechanism that
already refuses an occupied one, and there is no second register of who may park where (SIM-7).

## 2026-08-25 — an apron is one kerb, and its own kerb only where the map has one

Taken nearest-first, an apron of four landed on both sides of the street and in three different lots: a
crew walked out of the station and across the carriageway to a car, which is a rescue that begins by
crossing the road. So every bay of an apron now has to be on the same side as the **first** one taken —
the first and not the previous, because measured against its predecessor an apron chains round a corner a
bay at a time and comes out on both kerbs of the street it started on.

**The ground is taken before the plan's cars are stood.** Taken after them, an apron got whatever the
spawns had left over: four bays scattered across three lots with civilian cars parked between them, which
is not a station's yard. Claimed first, the four are the four nearest the building and its vehicles are the
only things standing in them. What that costs is a plan car whose own space was one of them — it is stood
in the nearest free bay instead, and only where there is no free bay near it at all is it not stood.
Measured over the shipped maps that is **five cars in a thousand**: Odesa loses none of its 520 and River
four of its 480, and the aprons went from 20 ambulances and 13 patrols to 22 and 14 on Odesa, and from 13
and 7 to 20 and 14 on River.

**Its own side is a preference and not a bar**, and the measurement says why. Refusing the far kerb
outright cost River more than half its ambulances (20 → 9) and stood the fixture town's police station no
cars at all, because that station's only parking is across the road from it. A building whose parking is
over the road is a building rather than a mistake; what would have been a mistake is an apron spread over
both. Preferring the near kerb and settling for one kerb either way, Odesa stands 20 ambulances and 13
patrol cars and River 13 and 7, and no apron straddles its street.
