# The evacuator — decision log

## 2026-08-27 — the man works the arm, and the reach that lets him get to it cannot be tightened to nothing

`CTL-7` made the arm a lever and `SRV-3` made it one somebody has to be standing at, so the hitching
interval does not begin until the recovery man is there — and the same at the yard, since a crew setting a
wreck down from the cab is what this change was about. Tightening `SceneReachInCarLengths` to one recovered
nothing on the shipped maps: the leg is already aimed at the exact place the fork takes hold from, so the
figure is a tolerance on settling there rather than a reach.

## 2026-08-26 — the arm became an action, and the crew's winch shrank to what a drive cannot do

The tow was something the town did to a wreck, with nothing on screen saying why it worked and no way for a
player to do the same. The arm is a lever now (`CTL-7`), one call reached by `E` or by the crew's own
decision, catching whatever the fork can swing under an end of. Three things followed: it catches either
end, one sign apart; it catches anything with a body, a wreck never having been the special case; and it
straightens what it picks up, or a car caught by the tail is scrubbed sideways for the length of the tow.
The winch stayed because a truck cannot drive onto a wreck — it queues behind the body like everything
else and comes to rest three or four metres short with the fork pointing the wrong way — but it is now the
last few metres and nothing else. The missing piece is a manoeuvre that backs a truck onto a body.

## 2026-08-26 — the fork holds a car by its nose and not by its axle

An axle's place under a body is that body's own business — across the fleet the front axle stands 0.95 m
to 1.48 m ahead of the middle — so the daylight between truck and load changed with whatever was on the
hook, and nothing on screen could show why. An underlift goes under the front of the car, which is the
same place on every car, so the fork takes hold a fixed distance inside the nose: one figure on
`SimConfig.Evacuator` rather than a measurement per variant.

## 2026-08-26 — the arm got a second picture

One picture at one length made an idle evacuator drive about trailing two metres of fork. The arm is drawn
in over its own deck and out only while holding something — two pictures scaled to each other on the winch
drum they share, because an arm drawn in is a different shape and not the same shape shortened.

## 2026-08-26 — the pair collide, and the truck was given the weight to shove what it is holding

The exemption was written when the fork held a car by its axle and brought some of the fleet within a third
of a metre of the deck, where corners closed on every turn and the haul became solver and coupling taking
turns. Holding a fixed distance inside the nose leaves two thirds of a metre on every car in the
catalogue, so the exemption went: the fixture town's recovery came in at 112 s against 144 s with the bar's
stretch unchanged. What it buys is a truck that can hit what it is towing, which is the more honest model.
Weight and power went up to 3.8 t and half a nominal car's acceleration, because a coupling is a poor
substitute for an engine.

## 2026-08-26 — the arm is drawn, and what the coupling holds is a point ahead of the wreck

The arm is the only part of any vehicle here that moves against the body it is bolted to, and where it
points was a fact nothing on screen was saying; drawn, a tow reads as a tow at any framing. Its reach is
now the distance an artist drew between hinge and fork, so it lives beside the picture like a lamp lens
(CAR-14a). Holding the coupling *at* the fork gives the pair a hinge at each end, and measured over a
straight street the wreck settled a third of a right angle off the truck's line. A real underlift clamps
what it has picked up, and the way to say that in `SOL-3`'s terms is to hold a point a whole reach further
along the wreck's centre line, where the hinge stands when the arm is straight.

## 2026-08-26 — the coupling is two impulses and the solver gained no joint

A constraint row is the rigid answer and would never stretch. What it costs is a new thing the solver *is*:
`SOL-1`…`SOL-36` describe a component presenting two shapes and two ways to actuate a body, and every one
of its costs is argued against that shape. The coupling is spent like the tyres — impulses at points, in
the phase the wheel impulses already use — which is `SOL-3` used rather than extended, allocates nothing,
and puts the tow in one file of arithmetic judgeable without a body. The bar stretches by a few
centimetres, and what holds the wreck in line is its own back axle, as with a real trailer.

## 2026-08-26 — the bar is priced at the effective mass at its two points, and capped harder sideways

Priced on the two masses alone the coupling overshoots asymmetrically, because an impulse two and a half
metres off a body's centre spends part of itself turning that body; the ringing came out as yaw on the
vehicle with a line to hold. It is priced instead at the two masses and two inertias each through its own
moment arm — the contact solver's own arithmetic asked about a coupling. The sideways axis is capped at a
small fraction because across the drawbar the same impulse buys several times the yaw, all of it landing
on the truck: uncapped, a corner at walking pace spun the tractor through fifty degrees in two seconds.

## 2026-08-26 — a towed wreck lays no ground, and the truck's claim reaches back over it

The first tow ever run stopped dead, because the trailer's own stretch cut the grant of the truck towing
it. A second exclusion threaded through every question the lane index answers was refused; the pair is
instead what it looks like — one movement, one stretch (`TER-5c.2`) — with the truck asking for the ground
both stand on and the wreck for none. One term in the claim's near edge and one early return, and no
second register of who is on what.

## 2026-08-26 — the wreck is winched onto the hook and set down in the slot, and both are placements

The truck stops within the crew's reach, which is three car lengths, and a coupling handed that much
stretch to pull out is a wreck thrown down the street. Winching it into line is what a recovery truck
does, is the operation `PHY-7a` already performs, and leaves the bar under no stretch on the tick the tow
begins. Setting down in a slot is the same argument at the other end, and the only way a trailer reaches a
kerbside bay at all — reversing an articulated pair into a space is a manoeuvre the catalogue does not
have and would be a poor first one to write.

## 2026-08-26 — the yard is a run of held bays and not a laid yard of its own

A rectangle of ground cut into slots costs new drivable geometry — a free-ground search, a cut in the road
(`GEN-4h`), ways in, a place in the occupancy index — all laid by a project that reads plans and does not
lay them. The yard is an apron (`GEN-4k`) with a bigger figure, so every question a slot must answer is one
the parking register already answers about a bay, and a mended wreck is an ordinary parked car. It costs
the town those bays, which is why the figure is small.

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
