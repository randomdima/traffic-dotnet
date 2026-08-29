# The evacuator — decision log

## 2026-08-27 — the man works the arm, and the reach that lets him get to it cannot be tightened to nothing

`CTL-7` made the arm a lever a player pulls; `SRV-3` made it a lever somebody has to be *standing at*. The
recovery man gets out, walks to the wreck, and the hitching interval does not begin until he is there — and
the same at the yard, because setting a wreck down in a slot is core work too and a crew doing it from the
cab is the thing this whole change was about.

**The reach came down and then went back up.** The obvious reading of "drive much closer" was to tighten
`SceneReachInCarLengths` from three car lengths to one. At one the shipped maps recovered nothing at all:
the leg is already aimed at the exact place the fork takes hold from, so the figure is a tolerance on
settling there — and a truck refused its own mark lays the leg again until `EVA-8` ends the recovery, while
a man who never gets out never reaches the winch either. Two is what the fixture town still recovers at, and
what the figure is now is a stated tolerance rather than something that reads like a reach.

## 2026-08-26 — the arm became an action, and the crew's winch shrank to what a drive cannot do

The tow used to be something the town did to a wreck: a crew that had stopped within three car lengths put
the wreck on the bar, wherever it was lying and whichever way round. Nothing on screen said why that
worked, and a player watching had no way to do the same thing.

So the arm is a **lever** now (`CTL-7`) rather than a passage of the errand — one call, reached by `E` from
a selected car and by the crew from its own decision, and the crew's version has no powers the key does
not. What it catches, it catches because the fork can be swung under an end of it: within the arm's reach
of the hinge, and behind the truck, which is the half of the road an arm bolted to a deck can work in.

Three things followed from making it a machine rather than a rule. It catches **either end** — the fork
goes under a tail exactly as it goes under a nose, and the whole of the difference is one sign, from where
the coupling holds to which two wheels are left on the ground. It catches **anything with a body**: a wreck
was never the special case, it was only the case anybody had written down, and a car on the bar is simply a
car that has stopped deciding until it is let off. And it **straightens what it picks up**, because a car
caught by the tail rolls on its own steered pair, and a wheel left wound over where the crash left it is a
car being scrubbed sideways for the length of the tow.

What did not follow is the half that would have made the two ends identical. A crew cannot drive its truck
onto a wreck: coming up the lane it queues behind the body like everything else, and the catalogue has no
entry that takes a car past an obstruction and stops it a set-down beyond. Measured, the truck comes to
rest three or four metres short of the wreck with the fork pointing the wrong way, and every recovery on
every shipped map ends in `Hitching`. So the winch stayed — but it is now the **last few metres and
nothing else**, spent only when the truck is standing in reach and the arm has come up empty, and it hands
what it has pulled to the same one call. The missing piece has a name and a shape: a manoeuvre that backs a
truck onto a body.

## 2026-08-26 — the fork holds a car by its nose and not by its axle

"The fork lifts the front axle" was read off what a recovery truck does and it is true of the metal, but it
made the geometry answer to the wrong fact. Where an axle sits under a body is that body's own business:
across the shipped fleet the front axle stands anywhere from 0.95 m to 1.48 m ahead of the middle, so the
same arm sat most of a hand's breadth deeper into one car than into another, and the daylight between truck
and load changed with whatever happened to be on the hook. Nothing on screen could show why.

What an underlift actually goes under is the **front of the car**, which is the same place on every car
there is. So the fork now takes hold a fixed distance inside the nose — one figure on `SimConfig.Evacuator`
rather than a measurement off each variant — and everything downstream falls out of it: the set-down, the
road the pair reserves and the gap behind the truck are the same for a coupé and for a van.

## 2026-08-26 — the arm got a second picture

One picture at one length made an idle evacuator drive about with two metres of fork trailing behind it,
which is not what a recovery truck looks like parked. The arm is drawn in over its own deck now, and out
only while it is holding something — two pictures of one machine, scaled to each other on the winch drum
they share, because an arm drawn in is a different shape and not the same shape shortened.

## 2026-08-26 — the pair collide, and the truck was given the weight to shove what it is holding

A joined pair used to be exempt from each other — one slot on a body naming one other, tested at the broad
phase — on the argument that a car on an underlift is resting on the truck rather than standing behind it.
The exemption was written when the fork held a car by its axle and brought some of the fleet up within a
third of a metre of the deck; there the corners closed on every turn and the haul became the solver and the
coupling taking turns, a metre of progress a second.

Holding a car a fixed distance inside its nose bought the daylight back. The set-down is the hinge, the
reach and that grip added up, and it leaves two thirds of a metre between the boxes on every car in the
catalogue rather than a figure that changed with whatever was on the hook — enough that a straight haul
never touches and a corner taken too tight touches as a corner should. Measured on the fixture town with
the exemption gone, the whole recovery came in at 112 s against 144 s and the bar's stretch stayed at two
centimetres.

What that buys is a truck that can hit the car it is towing, which is the more honest of the two models:
the one pair in this town that the physics had to be told about is now told nothing, and a player reversing
a loaded evacuator into a wall shunts the load like anybody else. It also cost the solver a branch per
gathered pair and a table the width of the town.

The weight and the power went up with it — 3.8 t and half the nominal car's acceleration, against 3.2 t and
a third — because a truck hauling most of its own mass again was the slowest thing on the road, and a
coupling is a poor substitute for an engine.

## 2026-08-26 — the arm is drawn, and what the coupling holds is a point ahead of the wreck

The tow used to be a metre of daylight and no picture, on the argument that a drawbar would be the first
thing here drawn for something the simulation holds as a pair of impulses. What that argument missed is
that the arm is the only part of any vehicle in this town that *moves against the body it is bolted to*:
where it points is a fact the town holds and nothing on screen was saying. Drawn, a tow reads as a tow at
any framing; undrawn, it reads as two cars driving unusually close together.

Drawing it settled two things that had been free. Its **reach** is now the distance an artist drew between
the hinge and the fork, so it lives beside the picture like a lamp lens does (CAR-14a) and left
`SimConfig.Evacuator`; and the wreck is set down where the fork actually is, so the picture and the
placement cannot drift apart.

**And the point the coupling holds moved forward.** The obvious reading of "the fork lifts the front of the
car" is to hold where the fork is — and held there, the pair has a hinge at each end, with nothing but the
trailer's own back tyres deciding which way it points. Measured over a straight street, the wreck settled a
third of a right angle off the truck's line and stayed there: a tow being dragged sideways down a road.

A real underlift clamps the front it has picked up, so the arm cannot pivot against the car it is holding
— and the way to say that in the only terms this engine has (SOL-3) is to hold a point **a whole reach
further along the wreck's own centre line**, which is where the hinge stands when the arm is straight. It
is one point and one impulse, exactly as before; the lever it acts through is what does the work, and it is
the same trick the old coupling used at both ends. What is drawn is still hinge to fork, because that is
where the metal is.

## 2026-08-26 — the coupling is two impulses and the solver gained no joint

The obvious design was a constraint row inside the solver: the two hook points held coincident, prepared
with the contacts, solved with them and position-corrected like an overlap. It is the rigid answer and it
would never stretch.

What it costs is a new thing the solver *is*. `SOL-1` to `SOL-36` describe a component that presents two
shapes and two ways to actuate a body and nothing else, and every one of its costs — the allocation gate,
the determinism digest, the per-tick budget — is argued against that shape. A joint is a new capability,
a new rule, a new row in the step and a new pass over a second table, added for one vehicle.

The coupling is instead spent the way the tyres are: **impulses at points, in the phase the wheel impulses
are already spent in, before the same step**. That is `SOL-3` used rather than extended, it allocates
nothing, and it puts the whole of the tow in one file of arithmetic that can be judged without a body.

What it costs is that the bar stretches. Measured over a recovery on the fixture town the stretch stays
inside a few centimetres, and what actually holds the wreck in line behind the truck is its own back axle
rather than the coupling — which is also true of a real trailer.

## 2026-08-26 — the bar is priced at the effective mass at its two points, and capped harder sideways

Priced on the two masses alone the coupling overshoots, and it overshoots asymmetrically: an impulse at a
point two and a half metres off a body's centre spends part of itself turning that body, and the share it
spends turning is what a mass-only price does not know about. The first version rang, and the ringing came
out as yaw on the vehicle doing the pulling — the one with a line to hold.

It is therefore priced at what an impulse in that direction at those two points is actually worth: the two
masses and the two inertias, each through its own moment arm. That is the contact solver's own arithmetic
asked about a coupling instead of about a contact, and it is the same expression.

The two axes are then capped separately, and the sideways one at a small fraction. The reason is the same
arithmetic read the other way round: across the drawbar the same impulse buys several times the yaw it buys
along it, and all of that yaw lands on the truck. On one budget a corner taken at walking pace spun the
tractor through fifty degrees inside two seconds and left it circling at full lock; capped, the coupling
stretches instead and the trailer scrubs round.

## 2026-08-26 — a towed wreck lays no ground, and the truck's reservation reaches back over it

A wreck is a body that is not driving, so the lane index lays it where it lies as the obstruction it is —
and the first tow ever run came to a dead stop the moment it began, because the trailer's own stretch cut
the grant of the truck towing it. The truck was queueing behind itself.

Excluding a second body from the grant would have meant a second exclusion threaded through every question
the lane book answers. Instead the pair is what it looks like: **one movement, one stretch** (`TER-5c.2`).
The truck asks for the ground both of them stand on and the wreck asks for none. That needed one term in
the reservation's near edge and one early return in the lying pass, it holds the traffic behind off the
trailer rather than off the truck, and there is no second register of who is on what.

## 2026-08-26 — the wreck is winched onto the hook and set down in the slot, and both are placements

The crew spends a bounded interval beside the wreck and then it is on the bar. What happens in between is a
placement — the wreck is put where the coupling holds it, a few metres behind the truck and in line with it
— and the same thing happens in reverse at the yard.

The alternative was to let the bar drag the wreck in from wherever it lay. The evacuator stops within the
crew's reach of it, which is three car lengths, and a coupling handed three car lengths of stretch to pull
out is a wreck thrown down the street. Winching it into line is what a recovery truck does, it is the
operation a container already performs when it puts a body down (`PHY-7a`), and it leaves the bar under no
stretch at all on the tick the tow begins.

Setting it down in a slot is the same argument at the other end, and it is also the only way a trailer gets
into a kerbside bay at all: reversing an articulated pair into a space is a manoeuvre the catalogue does not
have and would be a poor first one to write.

## 2026-08-26 — the yard is a run of held bays and not a laid yard of its own

"A large wreck park" wants to be a rectangle of ground beside the depot, cut into slots, with its own way in
off the road. What that costs is new drivable geometry: a free-ground search, a cut in the road (`GEN-4h`),
ways in for every slot, and a place in the occupancy book — all of it laid by this project, which reads
plans and does not lay them.

The yard is instead an apron (`GEN-4k`) with a bigger figure and a hold of its own kind. Every question a
slot has to answer — can it be reached, is anything standing in it, may anybody else park there, where is a
walk to a car in it aimed — is a question the parking register already answers about a bay, and a wreck set
down in one is an ordinary parked car to everything downstream the moment it is mended.

What it costs the town is the bays: a depot's yard is places ordinary traffic can never park in, which is
why the figure is small and why a yard takes only the bays a map actually has.

## 2026-08-26 — the priority is the outbound leg and the way back is traffic

An ambulance keeps its blue light on the way to the hospital because what is aboard is a person who is
running out of time. Nothing aboard an evacuator is running out of anything: the wreck has already
happened, and the reason to hurry to it — a lane blocked, a junction fouled — stops applying the moment it
is on the bar and moving.

So the light is out for the haul, and that is a rule about the errand rather than about the vehicle, on
`AMB-4b`'s own terms. It is also the safer half of the design: a nine-metre articulated pair given the right
to cross reds and overtake queues is the one vehicle in this town that should never be given it.

## 2026-08-26 — a haul that will not get through sets the wreck down

`AMB-9` says a delivery is drawn again and never given up, and the argument is that the casualty is aboard
and there is nothing better to do with them. That argument does not survive being said of a wreck: set
down, it is exactly where it would have been if nobody had come, and the town has its evacuator back.

A town stands two evacuators where it stands twenty ambulances, so one wedged on a corner it cannot get
round is most of the recovery in that town, for the rest of the run. The bound is therefore a count of
spent clocks rather than one clock, so an ordinary slow haul through traffic is not mistaken for a wedged
one — and it is the reason `--bench recovery` can report a city where the tow does not get home without
that city also losing its evacuator.
