# The evacuator — decision log

## 2026-10-19 — an arm tows anything with a body, one fork to an end

**The owner asked for no limits on what an arm can take**, trains included, with one: an end another arm holds is not
caught, and the other end of that body still can be. The arm passed over a car already on a bar, one already pulling
something and one standing in a yard, and caught nothing but cars. **Decided**: a tow is keyed by the body and the end
it holds (`TowHitches`), so a body takes two arms from its two sides, a truck on one arm may be towing, and a rail
vehicle out on the map is caught as a car is. What follows:

- **A chain is laid under the truck at its front** (`TER-4c.2`); a body held at both ends under the one at its nose.
- **A car on two arms stands on none of its wheels**: both pairs are up.
- **A rail vehicle stays on its rails** (`TRN-8`): the arm pulls it along them, and its train pulls the arm; every arm
  on it lets go as its train leaves the map or is put back on it (`TRN-7`).
- **A mended car standing in a yard stops being mended** when an arm takes it, and is a car again where it is let go.
- **Nothing refuses a loop** of arms; what reads a chain stops after as many steps as there are cars.

## 2026-10-19 — the coupling is the solver's link, the one a train is coupled by

**The owner asked for one real, physics-based solid link, free to turn, for the tow and the train alike**, with "no
special physics treatment for link" ([agents/train](../../train/docs/decision-log.md)). The tow's two impulses before
the step — a settle time, a cap along the bar and a harder one across it — are gone, and the arm is a pin the solver
holds (`SOL-38`): the truck's hinge and the wreck's eye kept on one point. What stays the evacuator's own is where it
takes hold (the eye a reach up the wreck's axis, which is the clamp) and the lift (the trailer wheels' share); the
link is shared. The coupling is rigid now rather than stiff, so a turn tighter than the wreck can follow is the
trailer wheels scrubbing, not the bar stretching.

## 2026-10-16 — a wreck beside no road is not taken

A wreck with no hitching place (`TownWorld.TheHitchingPlace`) was taken, given up at the next decision for want of
one, and taken again at the decision after: a patrolling evacuator was sent to it and back to its beat every
decision, its leg laid afresh each time. Odesa's stuck probe caught one reversing down a street into the car
behind it. **Decided**: `TakeAWreck` passes over a wreck it has no hitching place for, which is what giving the
recovery up would have concluded anyway; one lying in the road again is taken at the next decision.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- `EVA-5`: what an evacuator can do was exactly what a player can → the arm it works is; the errand's winch
  is the difference.
- `EVA-7`: a restored service vehicle's bay went back to the town → it is held for nobody
  (`ParkingRegistry.Claimed`), and a yard slot stays full until the mended car is taken out of it.

## 2026-08-27 — the reach at the wreck is a tolerance, and it cannot be tightened to nothing

Tightening `SceneReachInCarLengths` to one car length recovered nothing on the shipped maps: the leg is
already aimed at the exact place the fork takes hold from, so the figure is a tolerance on settling there
rather than a reach, and a truck refused its own mark lays the leg again until the clock ends the recovery.

## 2026-08-26 — the arm became an action, and the winch shrank to what a drive cannot do

The tow was something the town did to a wreck, with nothing on screen saying why it worked and no way for a
player to do the same. The arm is a lever now (`CTL-7`), one call reached by a hand or by the errand,
catching whatever the fork can swing under an end of. Three things followed: it catches either end, one
sign apart; it catches anything with a body, a wreck never having been the special case; and it
straightens what it picks up, or a car caught by the tail is scrubbed sideways for the length of the tow.
The winch stayed because a truck cannot drive onto a wreck — it queues behind the body like everything
else and comes to rest three or four metres short with the fork pointing the wrong way — but it is now the
last few metres and nothing else. The missing piece is a manoeuvre that backs a truck onto a body.

## 2026-08-26 — the fork holds a car inside its end and not by its axle

An axle's place under a body is that body's own business — across the fleet the front axle stands 0.95 m
to 1.48 m ahead of the middle — so the daylight between truck and load changed with whatever was on the
hook, and nothing on screen could show why. An underlift goes under the end of the car, which is the same
place on every car, so the fork takes hold a fixed distance inside it: one figure on `SimConfig.Evacuator`
rather than a measurement per variant.

## 2026-08-26 — the arm got a second picture

One picture at one length made an idle evacuator drive about trailing two metres of fork. The arm is drawn
in over its own deck and out only while holding something — two pictures scaled to each other on the winch
drum they share, because an arm drawn in is a different shape and not the same shape shortened.

## 2026-08-26 — the pair collide, and the truck was given the weight to shove what it is holding

The exemption was written when the fork held a car by its axle and brought some of the fleet within a third
of a metre of the deck, where corners closed on every turn and the haul became solver and coupling taking
turns. Holding a fixed distance inside the end leaves two thirds of a metre on every car in the catalogue,
so the exemption went: the fixture town's recovery came in at 112 s against 144 s with the bar's stretch
unchanged. What it buys is a truck that can hit what it is towing, which is the more honest model. Weight
and power went up to 3.8 t and half a nominal car's acceleration, because a coupling is a poor substitute
for an engine.

## 2026-08-26 — the arm is drawn, and what the coupling holds is a point ahead of the wreck

The arm is the only part of any vehicle here that moves against the body it is bolted to, and where it
points was a fact nothing on screen was saying; drawn, a tow reads as a tow at any framing. Its reach is
the distance an artist drew between hinge and fork, so it lives beside the picture like a lamp lens
(CAR-14a). Holding the coupling *at* the fork gives the pair a hinge at each end, and measured over a
straight street the wreck settled a third of a right angle off the truck's line. A real underlift clamps
what it has picked up, and the way to say that in `SOL-3`'s terms is to hold a point a whole reach further
along the wreck's centre line, where the hinge stands when the arm is straight.

## 2026-08-26 — a towed wreck's ground is its truck's

The first tow ever run stopped dead, because the trailer's own stretch cut the grant of the truck towing
it. A second exclusion threaded through every question the lane index answers was refused; the pair is
instead what it looks like — one movement — with the ground the wreck stands on held under the truck's number.

## 2026-08-26 — the wreck is winched onto the hook and set down in the slot, and both are placements

The truck comes to rest within a tolerance of its mark, and a coupling handed metres of stretch to pull out
is a wreck thrown down the street. Winching it into line is what a recovery truck does, is the operation
`PHY-7a` already performs, and leaves the bar under no stretch on the tick the tow begins. Setting down in a
slot is the same argument at the other end, and the only way a trailer reaches a bay at all — reversing an
articulated pair into a space is a manoeuvre this town does not have and would be a poor first one to
write.

## 2026-08-26 — the yard is a run of held bays and not a laid yard of its own

A rectangle of ground cut into slots costs new drivable geometry — a free-ground search, ways in, a place in
the occupancy index. The yard is an apron (`GEN-4k`) with a bigger figure, so every question a slot must
answer is one the parking register already answers about a bay, and a mended wreck is an ordinary parked
car. It costs the town those bays, which is why the figure is small.

## 2026-08-26 — the priority is the outbound leg and the way back is traffic

Nothing aboard an evacuator is running out of anything: the wreck has already happened, and the reason to
hurry stops applying the moment it is on the bar. The light is out for the haul, which is a rule about the
errand on `AMB-4b`'s own terms — and a nine-metre articulated pair is the one vehicle here that should
never be given the right to cross reds and overtake queues.

## 2026-08-26 — a haul that will not get through sets the wreck down

`AMB-9` draws a delivery again because the casualty is aboard and there is nothing better to do with them;
said of a wreck, set down is exactly where it would have been if nobody had come. A town stands two
evacuators to twenty ambulances, so one wedged on a corner is most of the recovery for the rest of the
run. The bound is a count of spent clocks rather than one, so a slow haul is not mistaken for a wedged
one.
