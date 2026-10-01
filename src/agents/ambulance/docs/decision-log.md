# The ambulance — decision log

## 2026-09-30 — a call passes traffic slower than it by more than a pedal's hunting

**The escort on the idle ring stopped dead a lap in** (AMB-4.4). A call passed traffic going any slower than it
meant to, and a charge following its leading escort holds exactly the escort's pace on a pedal that hunts a
hundredth either side of it: the trailing escort read the dip as slow traffic, laid a pass, and the pass took the
lane in front of the charge from the leading escort, which gave it up and stood. It surfaced when the cars'
drag changed and nothing about the ring did. **A call now passes traffic slower than it by more than
`Ambulance.SlowerToPassMps`** — half a metre a second, well clear of a pedal holding a speed and well inside any
jam a call is held up by.

## 2026-09-30 — a call gets past slower traffic, and plans twice as far

**The owner asked for a call to get to its incident as soon as it can, whatever it takes, and to claim twice as
far** (AMB-4.4, AMB-4.5). A call behind a jam that crept waited for as long as it crept, since only a body at
rest could be passed — the second of the owner's four passing rules, which the owner now lifted for a call.
**It passes traffic going slower than it means to** (`TownWorld.MayGetPast`, the car's own `PlannedMps`): what
goes as fast is no hindrance, and without it an escort on the idle ring, held under its charge's pace and so
never catching it, came to a standstill (`IdleRingTests`). What it passes moving it clears where that can come to rest
(`LaneOccupancy.StopsByM`), and a car that cannot stop short of the pass refuses it (`PassTerms.Takes`), so the
traffic it passes is held short of it rather than driven into it. Nobody on foot who is moving is passed.

**It plans `CallReachShare` times as far** — what it means to use and how far a plan may reach, and not what it
can no longer stop short of, which is its speed's and would take from everybody ground the call could still
give back. The pace it is held to (`CallPaceMps`) is left where it is: uncapped, a rescue crossed River at 75 m/s
and wrecked itself.

## 2026-09-29 — a call's pass is asked at its rung, past a queue and over the paint

**The owner asked for any car with its siren on to overtake with its priority claim** where the lane beside is
only planned by something weaker, and to overtake at junctions too (AMB-4.4). Asked, the owner lifted rule 1 —
nobody passes somebody making their own movement there — for a call, and said a call claims a zebra over the
people on foot, not only an empty one. Before, a call's pass was anybody's: any plan on the lane beside refused
it, so the oncoming car's reach held it as surely as a body; a queue at a red was waited behind like a wreck
nobody may pass; and a junction's zebras refused every pass through its box. What a call had over a queue was
its rung on the ground past it (AMB-4.1), which a body in its lane never let it reach.

**What refuses it now is what its rung does not beat** — a body, ground a holder can no longer stop short of,
another call's plan — read off the reservations like any pass (TER-4c.6). **And no count of bodies does**: one
pass got past four at most, a bound on a stack span that a queue at a red outgrew, and a call behind six stood
cars waited for the green as if nothing had changed. The bound is now past what a line laid its sight ahead
holds nose to tail, so the line and the ground end a long pass. The scenario map stages it (the car on a call
behind a queue of five waiting at a red): the call goes round the whole of it over the lane beside, the paint
and the box, and is through before its head; with the old bound it waits for the green.

## 2026-08-27 — the standoff is a place on the lane

One figure answered both how near the ambulance parks and how near the casualty is worked from, which put an
ambulance on top of every accident it went to. The leg is now aimed ten metres back **along the lane the
body lies beside** (`EVA-5`'s hitching place, measured the same way), because a standoff measured as a
radius lands on a pavement as often as not.

## 2026-08-25 — a hospital wears its own roof, and that roof is fitted rather than matched

An ordinary roof is picked by nearest authored footprint, so the hospital would wear whatever measured
18 × 16.6 m. Hospital and police roofs moved to a second list only a use can reach. The map still picks
the building, so the picture is **fitted inside the plot on its own aspect** rather than stretched —
drawing hospitals only from buildings near the picture's size would leave a village with no hospital,
which AMB-1 forbids. A fitted roof has no matched roof's answer for the quarter turn, so it takes the
pavement's instead.

## 2026-08-25 — the nearest ambulance is measured against the other ambulances, not the other casualties

Asking each ambulance for the nearest casualty *to itself* sent an Odesa call to an ambulance two
kilometres away while three idle ones stood a street from the body. One question is now asked before a call
is taken — is any other free ambulance nearer — and one that is not the nearest asks again next decision.
Ties break on the car's index, so it cannot deadlock.

## 2026-08-25 — the blue light is a rung on the road, not a mode in the driver

Every difference is written where it belongs: the rung a stretch is laid with, the red that stops applying,
the patience an overtake no longer waits out. The claim ladder (`TER-5g`) already ordered ground and already
said a rung may take a claim and never a body, so a call's rung above every movement made an ambulance
absolute over *who waits* and changed nothing about who may be driven into. A mode in the driver would
re-derive that at every site and get it wrong at one.

## 2026-08-25 — a rescue is capped at its own pace, because the road no longer caps it

Uncapped, an ambulance on River reached the gearbox's 75 m/s and wrecked itself with the casualty aboard.
Nothing here posts a speed limit — corners, queues, reds and crossings hold a car down, and an exempt
driver has only corners. The cap is a figure about the ambulance, set well above the traffic so overtaking
still pays (AMB-4a).

## 2026-08-25 — the scene is projected forward along the line, and "behind the axle" is settled by a reach

Projected onto the nearest point, a route leaving a bay beside its own casualty put the stop behind the
car from the first tick. Searched forward instead, the answer is the next time the line comes past the
body — but treating *anything behind the axle* as no place sent the distance to infinity the instant the
axle drew level, and the ambulance drove away. The two cases are told apart by the working reach rather
than by the sign of the distance. Before it, one shipped city delivered a casualty and two did not.
