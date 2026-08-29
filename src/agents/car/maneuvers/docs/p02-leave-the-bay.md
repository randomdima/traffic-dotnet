# `P-2` — leave the bay

Code: [P02LeaveTheBay.cs](../planned/P02LeaveTheBay.cs) · [catalogue](index.md)

**Scenario.** A leg is beginning and the car is standing in a parking space. Getting out of it means
driving the town's own way out of that space, across ground other traffic is using — backwards if the car
nosed in, and forwards if it backed in.

**`Sa` — the state it starts in.** At rest, in a bay this car occupies, whose ways the town laid and at
whose bay-end pose the car is standing. A bay with no way is not a refusal to be worked around; it is a bay
this leg never had.

**`Sb` — the state it delivers.** The whole body on the leave-lane, pointing along it, the bay given back
to the town, and no line in hand — the plan's next step lays one.

**Line.** The bay's own way ([`BayWays`](../../../../world/parking/BayWays.cs)), **which is the line the car
was parked over, travelled the other way**: the turn out of the bay, the swing that squares it up, and the
metre of lane it lands on. It is a way of the road's book and is taken up as it stands; drawn for the rear
axle. Where the car is not standing at the bay's own pose — after a recovery, after `P-16`, or because this
car's axle sits somewhere else under its body than the nominal car's does (`CAR-11a`) — the same shape is
laid from the pose the car is actually in, **at this car's own turning circle**, and driven instead
(`CAR-10b`). The town's way is an offer and not a rail: what it buys where it fits is a reservation and a
right of way already written into the book.

**Which gear.** The one the way is driven in, which is the standing's and not this entry's (`GEN-4j`). A
car that nosed into its space reverses out, and the follower steers against the direction of travel; one
that backed in drives out under power. Nothing here reads the gear except to hand it to the follower.

**Which way out.** Read off the pose. **The standing settles it first**: nose-first there is one way out, it
lands on the lane beside the bay, and it stays on it — nobody reverses across a carriageway and nobody backs
out over the lane coming the other way either (`GEN-4j`), which is why a car is only ever found nose-in
where the street had the room. Backed in there may be two, and the one taken is the lane already running the
way this leg is going — a car may cross the carriageway to reach it, under power, and the table holds the
traffic off it either way. Setting off into the stream running the other way is a leg that starts by driving
round the block — or, where there is no block to take, by turning at the first car park it can (`GEN-4l`).

**Do.** Drive it out. Nothing else.

**Guards.** The line stays in hand; a car that has lost it fails to `P-4`.

**Bounds.** The blocked-road clock, like any other car giving way. There is no patience of its own.

**Commit.** The mouth of the bay, which is where the movement is taken (below). Before it the car is
somewhere it is allowed to be; after it, it is across a lane and finishing is cheaper than anything else.

**Exits.**

| | Successor |
|---|---|
| the way is driven out | the plan's next step — `P-4` |
| the line is no longer in hand | `P-4` (failure) |
| stuck past the fuse | the ladder — rung 1′ re-lays this same manoeuvre while the car is still inside the bay |

**Why there is no wait here.** Because the town already has one. The way out is a way of the book, so what
holds the car in the bay is the road it is granted — cut at the first metre of that way the traffic on the
street is driven over, by the table walk that cuts a car at a junction (`TER-5c.1`) — and what stops two
cars in neighbouring bays taking the same gap is that the first of them takes the ground before it moves
onto it. A gap looked at as a time, a give-way patience, and a random beat to break a row of bays apart
were one mechanism standing in for that one, and naming a second gate on a movement the first already
refuses is what `SIM-7` is about.

**What that costs, and it is deliberate.** A car in a bay used to take the gap anyway once its patience ran
out. It now gives way like anything else, so a leg on a busy kerb waits and some of those legs end in the
place being given up ([the log](../../../../world/parking/docs/decision-log.md)). That is the trade every
yield in the town makes, and it belongs to the ground the car was refused (`TER-5e`) rather than to the bay.

**Why it is not scheduled.** Steering to a pose on a line a few metres long. A control loop run at a sixth
of the rate converges at a sixth of the rate.

**And it is the second half of a turn at a car park** (`GEN-4l`): a leg that came in here to come back the
other way leaves by the lane its destination lies down, which is the question this entry's own choice of
way already asks. Nothing about it is special but the bay it is in being one this leg is only passing
through.

**Refs.** CAR-6.4, CAR-6.5, GEN-4, GEN-4f, GEN-4i, GEN-4j, GEN-4l.
