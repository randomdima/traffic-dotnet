# The service vehicles — decision log

## 2026-09-29 — the police close a road by standing at its entrances, and the lanes leave the router

**The owner asked for the police back** — no police car had stood in any town since the bays went — and said
what one is for, as rules of their own at `P1`: it starts on its station's parking (SRV-7), its goal is the scene
of an incident (SRV-8), it marks a lane or the whole road inaccessible by standing at the lanes' entrances and an
officer standing there is what blocks one (SRV-9), a scene across both lanes gets two cars, one at each end
(SRV-9), a closed lane is out of path finding (SRV-10), and the officer gets out and stands on the road (SRV-11).

- **The closure is no longer a claim, and `ClaimPriority.Closed` is gone.** A claim round the scene was a
  second mechanism beside the officer's body and the ban (SIM-7): the body refuses a car the lane, as any body
  does (TER-4c.1), and the ban keeps anybody planning to go there. It also refused nothing a route had not
  already sent a car into.
- **A closure is a stretch walked back to a junction a driver chooses at** (`RoadClosure`). Stood at the scene's
  own lane, the entrance was regularly a car park's cut or a bend, and a car turned back there had no way on.
  **A lane that leads only into the closure is closed with it**: a single lane feeding the entrance, and one of
  several — the suite's city had a lane at a junction whose one movement was into a scene's lane, and a car on it
  stood at the officer for the whole closure.
- **The ban is a flag per run of the router, and a tour's per lane** (`RoutePlanner`, `LaneTour`), read off the
  closures only when one begins or ends. **A call is routed as if nothing were closed** — SIM-6's lifting for the
  one agent whose goal lies inside — and **the officer steps to the kerb for a call coming down the lane short of
  them**; stepping aside for any call whose line named the lane, they stood at the kerb for the evacuator
  already working at the wreck.
- **A route through a closed lane is dropped when the closure begins, for every car**: skipped for a car on a
  bay's way, a patrol leaving its yard took its stale queue up at the end of the way and drove into the officer.
- **A police car is stopped at its stand only in the entrance lane** — within half that lane. At the
  ambulance's working reach it stopped in the other lane beside its stand.
- **The officer is laid with the car** (`StandTheOfficer`), in a crew seat and in uniform, since the walker
  roster is never grown once a town stands. On the road they walk straight at their post under their own stage
  (`TripStage.OnDuty`), with no route and no clock: the trip's machinery stood them still for want of a way of
  the network and would have given the walk up.
- **Four faults in the old dispatch are gone with it**: a patrol's legs were re-aimed at a bay near where it was;
  a patrol driving home counted as free and held back every patrol behind it; a wreck already on a hook read
  as a scene; and a wrecked police car kept its call.

## 2026-09-28 — what an ambulance stops at is where it was sent

**What an ambulance stops at is where it was sent** (AMB-10): the standoff is worked out when the call is
taken, and the profile no longer reads the casualty's pose and searches for the lane under it every tick.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- `SRV-3`: CAR-1 asked for a driver a service vehicle lacks → CAR-1 asks for something driving it, which
  the errand is.
- `SRV-4`: a wrecked evacuator's bay went back to the town → it is held for nobody
  (`ParkingRegistry.Claimed`).

## 2026-08-26 — a depot wears the repair shop's roof

A depot was given an ordinary roof on the argument that it is a yard and not a front door. EVA-7 mends a
wreck standing in one, so the workshop is the point of the building and the yard is where its work is
parked; drawing a depot as a house and filling the bays with broken cars shows the errand and refuses to
name it. It is the third civic roof and cost one line each — found by id off `Civic.json`, unreachable by
`Match`, with the fitting and turns already written for the hospital.

## 2026-08-26 — the evacuator breaks, and the exemption it used to hold stayed in the format

Making the vehicle that clears wrecks unbreakable bought a town with one object nothing could happen to,
which shows the player a rule about bookkeeping rather than about the town. It breaks now, with art of its
own, and lets go of what it was pulling on the tick it breaks — a coupling held by a body taking no more
ticks is a wreck dragging a wreck. PHY-4b and the `unbreakable` key stayed: they are facts about the
damage rule and the file format, and nothing in the shipped catalogue wears them.

## 2026-08-26 — the uniforms are the person catalogue's own second list, and two facings are mirrored

The uniforms sit past the end of the wrap ordinary walkers are drawn by, named by id at load. Four more
entries in `Catalog.json` plus a rule about which a spawn may not be handed is a second register of who may
look like what. It is the body's own look rather than the car's, because the moment it is the car's it
disagrees with the body the town stood. The art is adapted: two raw sheets were seven facings by seven
frames, so the missing octant is the mirror of its opposite taken half a cycle on, re-laid on one baseline
and centred on the body's mass. The cost is a badge that changes shoulder.

## 2026-08-25 — the service list is a second list beside the fleet, and the wrap cannot reach it

Service variants sit past the end of the fleet's wrap in one array, so a police car is a sheet slot like
everything else and the seventeenth ordinary car cannot come out wearing a light bar. Named by id at load,
so `Service.json` can be reordered. A third catalogue with its own file and reader is a second copy of
`CarCatalog` and a renderer that has to know which one a variant came from.

## 2026-08-25 — a beat is a drawn place and not a search

A beat is the first errand that is not *for* anything, so there is nothing to aim it at. Drawn: places
along the town's lanes off the car's own stream. Searched: the quarter nothing has driven through for
longest, which needs a coverage map kept per tick and a walk of it on the hot path, to buy something
indistinguishable from a shuffle. It is a lane and not a junction because a leg ends with the car standing
where it got to, and the fixture town's patrol was wrecked inside the first box it reached.

## 2026-08-25 — a station keeps its bays, and standing four cars costs the town four places

Four bays held for four named cars for the whole run (GEN-4k). Letting them take whatever is free does not
survive a vehicle that actually leaves — a patrol coming back to a taken bay parks elsewhere, and within
an hour the station's cars are scattered and the station is a building nothing stands at. The hold is one
array and one clause in `IsFreeFor`, so a held bay is refused to a trip, a retarget and a spawn by the
mechanism that already refuses an occupied one (SIM-7).

## 2026-08-25 — an apron is one kerb, and its own kerb only where the map has one

Taken nearest-first, an apron of four landed on both sides of the street, so a rescue began by crossing
the road. Every bay must now be on the same side as the **first** one taken — the first and not the
previous, or an apron chains round a corner and comes out on both kerbs. The ground is taken before the
plan's cars are stood, or an apron gets what the spawns left over; that costs five plan cars in a
thousand. Its own side is a preference and not a bar, measured: refusing the far kerb cost River more than
half its ambulances and stood the fixture town's police station no cars at all, because that station's
only parking is across the road.
