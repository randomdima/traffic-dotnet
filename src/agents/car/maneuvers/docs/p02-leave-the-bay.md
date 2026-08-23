# `P-2` — leave the bay

Code: [P02LeaveTheBay.cs](../planned/P02LeaveTheBay.cs) · [catalogue](index.md)

**Scenario.** A leg is beginning and the car is standing in a parking space. Getting out of it means
backing onto the lane the space is entered from, across ground other traffic is using — so most of this
manoeuvre is the wait, and only the last part is the driving.

**`Sa` — the state it starts in.** At rest, in a bay this car occupies, whose registered leave-lane
exists and whose reverse-out template can be laid from the pose the car is actually standing in. A bay
that cannot be left is not a refusal to be worked around; it is a bay this leg never had.

**`Sb` — the state it delivers.** The whole body on the leave-lane, pointing along it, the bay given back
to the town, and no line in hand — the plan's next step lays one.

**Line.** The reverse-out template: a run back, a fillet at the car's own turning circle, and the turn
that puts the rear axle on the lane's line. Drawn for the rear axle and driven in reverse, so the
follower steers against the direction of travel.

**Do.**
1. Lay the template and start the wait a random beat *below* zero, so two neighbouring bays that begin
   waiting on the same tick do not take the same gap.
2. While the body has not crossed the mouth of the bay, hold where it stands, **claim** the stretch of
   lane the body will stand on, and ask the lane **how long before anything on it reaches the pose this
   car will occupy** — a time, never a distance.
3. Past the mouth the car is committed and drives the template to its end.

**Guards.** The line stays a template; a car that has lost it fails to `P-4`.

**Bounds.** The give-way patience, jittered per car by the same beat: past it the gap is taken anyway,
because a car waiting out a jam is one more car in it. The bay is also the one piece of road this car is
entitled to occupy, so the watchdog spends no clock while the wait is in force.

**Commit.** The mouth of the bay. Before it the car is somewhere it is allowed to be; after it, it is
across a lane and finishing is cheaper than anything else.

**Exits.**

| | Successor |
|---|---|
| the template is driven out | the plan's next step — `P-4` |
| the line is no longer a template | `P-4` (failure) |
| stuck past the fuse | the ladder — rung 1′ re-lays this same manoeuvre while the car is still inside the bay |

**Why the gap is claimed and not only looked at.** Two cars in neighbouring bays that each found the lane
clear on the same tick have both obeyed a rule that says look, and both are now backing onto it. The claim
is what makes the second of them wait — the same argument a car crossing a junction takes its sections on
(TER-5c), applied to a stretch of lane ([`LaneOccupancy`](../../../../world/road/LaneOccupancy.cs)):
ground that is empty *now* is exactly what a reading off the bodies lets two cars take at once. It needs
no release: a claim is re-laid into the index from the car's own field every tick, so one held by a car
that has been wrecked, unmanned or taken over by a hand is gone without anything having had to notice.

**Why it is not scheduled.** Steering to a pose on a template a few metres long. A control loop run at a
sixth of the rate converges at a sixth of the rate.

**Refs.** CAR-6.4, CAR-6.5, GEN-4.
