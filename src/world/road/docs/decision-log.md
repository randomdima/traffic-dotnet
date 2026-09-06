# Decision log — roads and junctions

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-06 — the graph reads the lines rather than drawing them

**`RoadGraph` used to be two jobs in one type.** It cut the roads, offset the lanes, settled every lane end's
cut back, drew every connector — and then laid the rules over what it had drawn: where the lanes meet, what
each movement takes off the others, the index a body is stood up against. Only the second half is this
slice's. The first half is the town's own geometry, wanted just as much by the ground as by the traffic, and
it is `CityGen.LaneLines` now — laid when the map is generated, off the plan alone. `RoadCuts`,
`ParkingSections` and `RoadFrontages` went down with it; each only ever asked the plan, and each was sitting
here because that is where its first caller happened to live.

**What the move buys is that the tarmac and the network cannot disagree.** The ground a car may drive on is
the ground under these lines, answered off the same arrays the follower is steered by — so a junction needs
no shape of its own (TER-5), and the question of whether the surface and the graph were laid to the same
figures cannot be asked. The graph's own public surface did not move: every property a caller reads is the
same name over the same array.

## 2026-09-05 — the pavement is the tarmac wrapped, and a junction is not a case

**The walking network was six constructions and not one of them was the pavement.** Strips offset from
each road's own centreline and cut at the junction discs; kerb corners read off the plan's fillets and
*matched* to the nearest end of the nearest strip within a band, then pushed onto it; a straight laid
between whichever two ends of a junction had nothing standing between them; a ring round a dead end's
head; a three-sided band round each car park, and the severing that made room for it. **Five of the six
were the junctions and the sixth was the car parks** — and what they had in common was that each was laid
off a *record* rather than off the ground beside it: a centreline, a disc, a fillet, a box. Where the
records and the ground agreed the answer was right, and where they did not nothing said so. What that
looked like on Odesa was a band laid ten metres straight across a kerb corner because the two ends of it
happened to have pavement between them, a lot's band swinging five metres off the kerb it was meant to run
beside, and a corner arc drawn with a fillet's curvature from a point that was not on the fillet's circle.

**It is now one construction and one rule** (TER-3c.3). `Kerbs` states the town's tarmac as one shape and
answers how far a point stands off the nearest of it. Every piece of that shape offers the line that stands
half a walk outside itself, and **a metre of such a line is pavement exactly where nothing else stands
nearer than that**. Two lines give way to one another at the point they cross, which is the point both are
half a walk from both pieces — so nothing is matched to anything, pushed onto anything, or joined across a
gap. A junction's corner is the fillet's own arc read in by half a walk; a dead end's head is the disc's
circle read out by it; the way past a car park is the box's; a street lying inside a car park's mouth is
cut away over the whole of it. **None of them is a case**, `FootEdgeKind.JunctionCorner` is gone with the
constructions that made them, and the severing and the dead-edge list went with the wrap round the lots.

**Where two carriageways merge, the outer edge of the pair is what is wrapped**, because each one's line
runs on into the other's tarmac and is cut there. It is the same rule reaching a second answer rather than
a second rule, which is what the old arm bands, head bands and lot bands each were.

**The offset is compared with a rounding's grace, and the grace is the whole of what makes the wrap exist.**
A line stands the offset from its own piece exactly — and, wherever two pieces are tangent, from a second
piece exactly as well. That case is not rare but the common one: **a junction's disc is drawn to the width
of the arms that leave it**, so the disc and each arm's band touch, and the circle that wraps the disc runs
half a walk from both of them for the whole of its length. Compared exactly, whether metres of pavement
exist at all is the last bit of a float, and what that cost was the apron round every such junction — six
metres of a corner standing at exactly half a walk, on walkable ground, with no line laid on it, and Odesa's
pavement in two halves that no walk could get between.

**A rounding, though, and never a tolerance.** A tolerance ε lets a line that meets another *tangentially* —
which at a kerb fillet is how they always meet — run <b>√(2·R·ε)</b> past the point the two cross: at five
centimetres that is better than half a metre each, their ends then stand a metre apart with no node between
them, and the pavement comes apart into a piece per corner (ninety-seven of them on the exam lattice, where
there should be one). At a millimetre the overshoot is centimetres and the stitch below closes what is left.

**And the ends of the wrap are stitched, because a tangency cannot be cut cleanly either way.** Where the
two pieces do *not* quite touch — a fillet's tangent point sitting a millimetre off the kerb it was drawn to
— the envelope dips inside the offset over half a metre of itself, both lines are cut at that dip, and
neither covers it. What is ill-conditioned is the crossing of two curves that graze; what is not is the
distance between the two ends once they are cut. So the loose ends of the wrap that stand within a body of
each other are joined by the stretch that runs between them (`FootGraph.Builder.Stitch`). Without it Odesa
comes out in eighteen pieces and River in twelve; with it each town's pavement is one.

**Three things it turned up that were already true and could not be seen.** A walker with no walked point
behind it was stationed at its first point's distance along a way less however far it stood from that
point, and only the distance coming out negative ever refused it — which it did while a stretch was short
enough for the first point to stand near the start of one. On a stretch long enough it did not, and a body
that had walked none of its line claimed ground across a field (`TownWorld.IsAfoot`). **The bar has to be
held to the way and not to the one metre**, because stepping back from the first point by the straight to it
takes a body's whole sideways distance off its place as well: a paramedic that has just got out of a cab
reads metres off a line it is standing on, and refused on that reading the crew never reached a casualty at
all. A crossing that
reaches no pavement used to split whatever pavement lay within a walk of where its own span gave up, which
left a stretch ending half a metre off a car park's kerb. And **offsetting a chain joins it only where the
chain is smooth**: where two pieces meet at an angle their offsets meet at a gap of that angle times the
offset, and a chain walked as one line across such a gap lies about where its own metres are — a station a
quarter-metre from the end of one stood a metre and a half away, and the clip that had walked it passed it.

**What is checked is the claim itself** and not the derivation restated: every metre of pavement has no
tarmac inside half a walk of it and has tarmac within half a walk of it, asked of the ground the town
answers with rather than of the shapes the line was cut by
(`FootGraphTests.EveryStretchOfPavementStandsHalfAWalkFromTheTarmac`, VER-12).

## 2026-09-05 — a connector is an object, and where lanes meet is worked out from them

**A movement used to be a lane paired with an index into that lane's slots**, and the pair travelled
together because neither number meant anything alone: the slot said which ground the movement took, the
pair said which lanes it joined, and every reader of one carried the other. A connector is now the id, and
it answers all of it — the lane it leaves, the lane it arrives on, the turn it makes, the right of way that
carries and the line it is driven. The town's second block of ways is that id, so a way number and a
connector are one integer exactly as a way number and a lane already were.

**And the plan's node table is gone from the graph the town runs on.** Where two lanes meet was a record
handed over beside the connectors, which is the same fact said twice: two lane ends are one place when a
connector runs between them, or when they are the two ends of one stretch driven either way. `LanePlaces`
works that out once, and the two things that need it read the same answer — the contraction that decides
where a run of road ends, and the walk that lays a body onto the ground it is standing on. Derived twice,
the router and the claims would be entitled to disagree about which lane ends are one piece of the world.

**The second clause is not tidiness.** No box admits the movement that turns a car round (TER-5f), so the
lane into a dead end has no connector to the lane coming back out of it. Joined only by connectors the two
would be different places — a run arriving with nowhere to go, a run leaving that nothing arrives at — and
a leg could not be priced round a car park's bay (GEN-4l) at all, because nothing would offer the pair.

**What a junction is now is the plan's**: something authored, sized a disc for, paved a corner on. The
graph keeps the lanes one junction lowered into and offers them to the two slices whose subject *is* an
intersection — the signals a bundle governs (TLT-1) and the paint laid on an arm (TER-6). Nothing that
drives, routes or claims can reach it, and a junction is the shape a set of crossed lanes happens to make.

**The alternative was a spatial index over every way**, asked at a radius round the body: it needs no
places at all and would find ways the walk from a lane end cannot see. It was not taken because the span a
caller has to give the walk then has no bound the town can state — the radius is the largest body's, which
is a fact about the fleet's catalogue and not about the road — and a walk that silently truncates is a body
invisible on a way. The places give the same answer with a bound that is counted rather than guessed.

## 2026-09-05 — the corner is cut off the lane rather than marked on it

**The setback used to be a number beside a lane rather than a fact about it.** A lane's line ran the whole
stretch between two discs, and two figures said how far into either end its movements actually handed over
(TER-5d). Everything downstream then carried the difference: the assembler threaded a sub-chain rather than
a lane, a place on a lane had an origin under the line that was not nought, occupancy added the arriving
join's figure to every metre it wrote, and the overlay drew the ground past each figure twice — once as the
lane and once as the join laid over it. What that last one looks like is a spur of lane hanging past the
point every movement leaves from, which is what sent us looking.

**So it is cut off.** The widening still settles what each end needs, and then the lane is trimmed to what
is left, so a lane's own first and last points *are* its connection points. `JoinedAtM` and `LeftAtM` are
nought for every carriageway lane and are gone from the graph; `JoinFromM`, `JoinToM` and `LaneOriginM`
went with them, and the three call sites that used to add them now add nothing. A join runs from one lane's
end to the next lane's start, and no ground carries both.

**The other networks keep their two figures**, and that is the point of the interface rather than a
leftover: a pavement hands over at a point per turn, because a walk running straight through a node gives
up no ground while one turning off it gives up a corner's margin, and a bay's way runs on past the pose to
the end of the space because that run is ground and not line. The carriageway is the one network where the
handover is a property of the end, so it is the one where the line can be cut to it.

**What is still owed**: a lane is cut at the junction's *disc*, not at the reach its kerb corners actually
paved. Cutting at the reach instead was tried and reverted — it moves every lane end out past the crossing
and the stop bar the same reach positions, and it deletes short stretches outright, which costs the town
its connectivity. Until the paint and the cut are settled together, a lane can still lie a little on an
arm it meets at a skew angle; the shipped cities do not, and the hand-laid fixture does by half a metre.

## 2026-09-05 — a one-way road is a narrower road, and a corner is solved on the pair it stands between

**A one-way street could have been a two-lane road with one of its lanes left empty**, and it is not. The
lane graph was already directed — lanes are cut per stretch, assigned by heading, and every consumer of the
other side already asked whether there was one — so the whole of what a one-way road needed was to lay one
lane instead of two, down the middle of half a carriageway. The alternative would have been a road as wide
as any other with a lane nothing may enter, which is a rule every drawer, claimer and walker has to be told
about; this way the road's own declared width carries it (TER-4), and the lane's width, its offset, its
pavement, its zebra and its kerb all follow from that one figure as they always did. `LaneReverse` is
`NoLane` there, and the eighteen places that read it were already written for the answer.

**Which way it runs is carried and never inferred.** A narrow road is not necessarily a one-way one — a map
may declare any width it likes — so the flow is a field on the road and a byte in the format (version 5),
and no reader works it out from the geometry (TER-4).

**Which half of the carriageway it is was the part we got wrong first.** Laid down the middle of the line
its two junctions are joined on, a one-way street is a narrowing rather than a street: the road it meets
carries the same traffic half a lane to one side, so every car through the junction stepped sideways, both
kerbs stepped in, and the lane line, the pavement and the frontage all stepped with them. It stands on the
half it is driven instead — half a lane to the driving side, so its own kerb *is* the kerb of the
carriageway it is half of and the step is where that carriageway's centreline used to be. Nothing about the
lane changed: it is still the middle of the road's own width, and the road is what moved
(`RoadStage.OntoTheDrivenHalf`). **It is moved after the bends and never before them** — two arms swept
onto one tangent stay met when both go to the same side of the travel they share, where two merely joined
at a node come apart by the deflection between them — and **a node with no fork goes with its arms**, so
the disc a junction is drawn on stays on the road. **It costs the layout nothing it had already kept**:
half a carriageway moved half a carriageway over stands inside the full width the crossing pass held two
roads apart by (GEN-17), so no road comes nearer another for it.

**What it costs is the walk on the side the street is not driven**, which now runs where that carriageway's
middle used to be — nearer the node than the kerb it is beside, and bitten deeper by the junction's own
disc. A scrap of pavement a metre long between the paint and the kerb corner is what that leaves, and every
corner at such an end hands over on what the scrap can spare rather than on the half band it wanted. It is
the same debt as the lane cut at the disc rather than at the reach, and it is paid off the same way.

**What actually broke was the junction, and it was already broken.** Every corner in this engine was solved
as if both its arms were the same width: the kerbs crossed on the bisector at `half / sin(half the angle)`,
and the reach along both arms was one figure. That is only true where the two arms are the same road width,
which until now they always were. It is now the crossing of the two kerb lines, each offset by its own
road's half (`SimConfig.JunctionCornerAlongM`), and each arm is reached as far as its own tangent point —
the same answers as before wherever the two halves are equal, and the honest ones where they are not. The
disc is sized on the widest arm the junction has, so a junction only one-way streets meet at is smaller and
one an arterial reaches is the arterial's.

**And whether a corner exists at all is measured off the narrower arm.** Two arms all but straight through
cross their kerbs so far out that the junction never reaches the crossing, and that is the case with no
corner to turn — a carriageway running through, or a step in the kerb where the two are different widths.
Measured off the disc, as the spike test used to be, a narrow street meeting a full carriageway obliquely
came out as no corner at all, and the pavement that ought to have stopped at its kerb ran on over the
carriageway instead. It is measured off the mouth the wedge is a spike out of, which is the narrower of the
two.

## 2026-09-04 — one table of ways, and no metre of one in two claims

There were two sets of claims: the road's, over its lanes, its junction joins and the ways its bays are
worked off, and the pavement's, over the two sides of every stretch and the mitres between them. They were
two `LaneOccupancy` instances in two numbering spaces, and the only thing that said which of them a way
number belonged to was a `bool` on the row a body's pose laid. **Nothing could compare a claim in one with a
claim in the other**, so a car parked across a footway and the walker coming down it each held the same
ground and each book was right about itself. The overlay drew it: two washes over one piece of pavement, one
per network, one per body.

**There is now one table.** Every way in the town is numbered once — lanes, joins, bays, footways, mitres —
and the kind is a property of the ground rather than a table it lives in (`TownWays`, TER-4c.2). What the
kind decides is how wide the ground is and what has to stand clear of its line: half a car on anything the
traffic drives, half a body on anything it walks. It decides nothing about who may claim, which was already
the rule and now has nowhere left to be violated.

**And the claims on a way are disjoint by construction** (TER-4c.3). The insertion was a splice that never
looked at what was already there; exclusivity lived only in the grant passes, and those only ever shortened
the *asker's own* far edge — so the table was full of overlapping asks by design and two cars queued for one
turn each held that join's own runs. A stretch is now cut back to the edge of whatever it would have shared
ground with, before it goes in, and the gate walks every way of every shipped map every tick to say so.

**What decides which of a pair gives up the ground had to be the body and not the rank.** Read off the
priorities, two bodies of one rank were settled by whichever went into the table first — and a follower
close enough for its nose to reach into the margin the leader keeps could take the leader's own ground out
from under it. A claim whose body stands over the shared metres now beats one merely reaching across them,
and between two bodies it is whichever is further back that yields: the same answer the grant arrives at,
and the same answer whichever order the pair is laid in.

**A refused ask is outside it**, and that is the one exemption. It is a mark and not a hold — nobody's
ground, binding nobody — laid deliberately over the stretch the traffic holds, because what it says is that
somebody is waiting for exactly those metres (TER-5g). Made exclusive with the rest, it was cut away by the
thing it was marking and the traffic stopped giving way at uncontrolled crossings.

**What is not settled is the stretch laid from a pose that reaches past a body.** A swerve's corridor runs
over the very body it is swinging round and a movement's runs reach past whatever is standing in the box;
truncated at it, the far half is ground its holder is committed to and nobody holds. Laying the far half as
a second run of the same claim was tried and is worse: an occupant with two stretches of one way breaks
`Withdraw`, `CutTo` and `AlreadyHolds`, which all take a hold on a way to be one interval (TER-5c.2), and
the town gave way less rather than more. So the truncation stands and three cases fail with it — a
template's far end, the hand-back across a box, and a rescue behind a stopped one. **Making them disjoint
needs TER-5c.2 opened first**, and that is a decision about what a body is allowed to hold rather than about
how the table is filled.

## 2026-09-04 — a way has one property, and it is a claim

There were two words for one row. `LaneUse` said how a stretch had been measured — from a line, from
a pose, reached for and not arrived at, a walker's band, the town's furniture, an ask refused — and the
rank said how strong the hold was, and every question was a mask over the first with a floor
under the second. Two of the seven uses then turned out to mean the same thing as a strength, and the
vocabulary drifted three ways at once: the same stretch was an *intent* in the enum, an *announcement* in
the method that laid it and a *want* in the field that sized it.

**There is now one row and one word.** A way is used when there is a claim on it: who is claiming, where,
and at what priority (TER-5g). Everything a reader used to get from the use is worked out from those —
whether a body is standing in it is whether its body edge is past its near edge, whose it is is the
occupant and the roster, the town's furniture is a claim nobody owns, and the one thing left that none of
those say is whether the body is following this way's line or merely standing on it, which is a flag on the
claim because it turns the margin and what a walker may step round.

**The masks became a ceiling on the ladder**, and that is the thing that says the shape is right: what a
grant is cut by is every claim at `Firm` or stronger, and what the driving side reads is every claim at
`Soft` or stronger. A question now names what it is about (`ClaimsAsked`) and the answer is derived from the
claim, so the two can no longer disagree.

**The priority ladder needed five levels and not three.** `Firm` and `Soft` differ by one `=` — ground
granted is taken by a strictly stronger movement, ground merely stated by an equal one — and collapsing them
either deadlocks two crossing movements of one rank or leaves every weaker movement waiting on ground the
other was only thinking about. `Rejected` is the walker's refused band, which binds nobody and is the
lifecycle's own word for what it is.

**The right of way stayed, and it should not have to.** A junction's every movement is a way of its own, so
the rank of a claim is a fact about the ground it is on and the field is a cached copy of it
(`TownWorld.RightOfWayOn`). What still reads the copy is the asker's own rank on its own way, and the
marker `AnyRescueOver` spots a rescue by. Taking it off is a change of its own.

## 2026-09-04 — a driver states the road it means to use, at a strength anything stronger can take

What was claimed was what every car was committed to and nothing about where any of them was going, while
the grant already looked further up the road than that (`LookForTheCutToM`). Every driver could therefore
read the others' commitments a reaction interval out and none of them declared its own, and the read/write
asymmetry was the defect rather than the length of either half. What it cost was a car pulling out of a
side road in front of one coming at speed: the approacher's ground reached nowhere near the box, so nothing
said it was coming and the only move left to it was a hard stop.

It also left the rank ladder with almost nothing to rank. Seven levels were compared in two places, and the
only revocable hold in the town was the claim across a box — so a blue light held its road at `Emergency`
and that bought it nothing on a lane, where the traffic ahead held a committed claim which binds
unconditionally and correctly.

A car now lays a second stretch beyond the one it is committed to: what it takes to reach the speed it is
planning for, hold that speed for as long as it says it will, and stop from there. **The holding time is
what tells a plan from a commitment.** Measured at the decision interval — a tenth of a second — the stated
claim is arithmetically the committed one again for any car already doing the speed it is planning for,
which is every car on an open road and exactly the ones whose intentions are worth having claimed. It is a
figure of its own (`DrivingFigures.StatedRunS`) for that reason. **That is the ask the committed claim
deliberately is not**
(2026-08-22, below, which still stands and is why that one is short) — and the whole of the
difference is that this one can be taken. Held as ground the car was committed to, the same figure was a
quarter of a kilometre of empty straight nothing could ask back; held at p9 on the named ladder (TER-5g) it
costs whoever outranks it nothing at all.

**A tie does not refuse.** A granted claim is one movement's ground and is settled by whoever took it, so
ground held at an equal rank binds; a stated claim is laid by everybody in the same rebuild, so two
movements of one rank that each refused the other's would each be waiting on ground the other was merely
thinking about and neither would ever ask for it. Stated claims are therefore taken by a strictly stronger
movement only, and a tie is settled by the granted claim exactly as it was before anybody stated anything.

**And a body that is not moving states nothing**, which is the half of it the exam paid for. Sized from
a standstill the stated claim is the pull-away horizon of every car in every queue in the town, held
against every movement those queues cross: a car waiting at a give-way line held the box shut against the
very traffic it was waiting for, and the weaker movement in seven cards of thirty-six never went at all.
Such a claim says where a car is going, and a car at rest is going nowhere until it moves.

**The rank a car asks a box's ground with had to become the rank it holds that ground at.** A car past the
point it could stop short was asking with its movement's own rank while holding the ground with
`RightOfWay.Committed`, which nothing had ever noticed because a claim at an equal rank binds anyway.
Against a stated claim it noticed at once: a car committed to a box was cut at ground the oncoming stream
had merely stated, stopped in the middle of the junction, and stayed there — card 11's failure, and the
same shape as card 9's standing finding.

**One exam card was ordering two cars to one metre of road.** Card 21 stages a turn across and a straight
into the same arm and both were sent to the same place, so it passed only while the weaker movement got
there first. Giving way properly is now what it does, so the card was reporting the order they went in
rather than what it is about; the straight is sent further on and the card is named for what it tests.

**The signals needed no change at all**, which was the surprise. A red is already a stop point the ask is
clamped by, so clamping the stated claim by the same point is the whole of "a red refuses the soft
claim, and refuses it for nobody exempt from the bar": a car stopped at a bar states nothing
beyond it, a car with a blue light is not stopped by the bar and neither is what it states, and a car
past the point it could stop is stopped by nothing. Expressed instead as ranked ground laid at the bar, the
light would have decided in two places and been free to disagree with itself.

**What it costs is room.** The two asks together reach as far as the assembled line, so a car's share
of the index went from eight stretches to the ways of its own line and two, and every walk of a busy way is
longer. The line itself was not grown to fit the ask: it is drawn as far as the car can see (`CAR-11`),
the stated claim is clamped to it, and whether that sight distance should grow is a separate measured
question.

## 2026-09-03 — a body on foot holds the paint it stands on, and only a car is left off it

The exclusion that keeps a car on a zebra from claiming the pavement was applied to whoever was standing
there, walker included. A zebra is several lanes crossing one walk and nothing else — the same shape as a
junction, where a body standing in the box goes onto every join under it — so the special case cost a walker
standing on the paint its claim on the only network another walker reads: no cut to a grant, no step round it
(PER-24), a walk straight through it, and one block in the overlay where there should be two.

**The argument that covers a car does not reach a person.** What holds a walker off a car on the paint is
that car's stretch of the lane, looked up where the crossing crosses — and that look-up asks what traffic is
*coming* (`LaneOccupancy.AnyTrafficOver`), of the one band the body is about to step into. A person standing
in a lane is deliberately not an answer to that question, and a walker already on the same band asks nothing
at all, so nothing anywhere held one walker off another.

`StandInTheWay` now writes every way its own box touches, the paint among them, as `LaneUse.Lying` like any
other body going nowhere. `WalkedAlone` is left where the argument does hold: the car's write.

## 2026-09-03 — a lane is the ground it is walked over, and the corners come off the line

A pavement lane was the whole of its stretch's offset line, with the ground each corner takes remembered
against it as a pair of margins. The walk started a margin in and ended a margin short; the metres between
the margin and the line's own end were on the lane and on nothing else. That spur was real — a body standing
inside a corner claimed it — and it was a metre or two at **both** ends of **every** lane,
which the picture could only show as a line ending in mid-pavement. Hidden, the blocks on it stood on
nothing; drawn, the sidewalk read as a web of severed stubs where it is in fact continuous.

The margins now come off the line itself. `Carrying` cuts each lane down to the span the mitres leave it, so
`WalkedFromM` is nought, `WalkedToM` is the lane's own length, and a mitre sets off from the arriving lane's
last point and lands on the onward lane's first. There is nothing left to hand over and nothing left to
draw twice: the pavement is one line through every node. What is left over is the inside of each corner,
which is ground on no way at all — the answer the town already gives for a verge.

**Two ends of a short stretch could want the whole of it.** Each is bounded by half of the shorter of the two
lanes it stands between, so on a stretch shorter than both its neighbours the pair took everything and the
lane cut to a point. Three lanes on Odesa and one on River did. They are now held back in proportion to
leave the lane four fifths of itself, which is the one figure this arrangement needs that the geometry does
not give.

## 2026-09-03 — turning round on the spot lays no ground

`LayJoins` laid a mitre for every way out of the node a stretch arrives at, and a stretch's own reverse is
one of them. Nothing else in the network treated it as a way: it is excluded from how far a lane is walked,
from the corner a lane may carry, and from the count that decides whether a corner is anybody's choice. But
the arcs were laid all the same — 7292 of them on Odesa carrying 22.9 km of ground, 5252 on River, one per
directed stretch in the town — and every one took a number of the pavement's own.

That made it a way with claims on it and no line anywhere. `GroundUnder.WriteTheJoins` wrote every
body standing at a node onto it, so the claims layer drew a fan of blocks across the corner in the
walker's own colour, over ground the nodes layer had nothing to draw — the picture could not account for
the blocks, which is exactly what OBS-2d forbids. It also spent slots: a body's ways come out of a fixed
span, and a bogus one at the front of it is a real one at the back going unwritten.

A body that turns round is already standing where it ends up, so the turn stays in the table — a walk at a
dead end still needs it — and lays nothing. The debug layer no longer skips it either: it draws every mitre
the town lays, and what it draws is now the whole of what can be claimed.

## 2026-09-03 — a walker is laid on a car's terms, and on nothing of its own

Two things stood between a body on foot and the reading every other body in the town gets, and both were on
the walker's side alone.

**The terrain grid decided whether a walker was on the road at all.** `StandInTheRoad` asked
`Terrain.At(centre).Drivable` before it asked the geometry anything, and the grid answers to the cell it was
painted at — a metre across on every map this build lays. So a walker within half a cell of a kerb regularly
sampled ground the carriageway never reached, and a body a stride into the road claimed no lane:
somebody stepping out in front of traffic that had nothing to read. The test that says a body in a junction
is on the joins under it had already been written round it, standing its walker on a join's own line because
the middle of the box "is ground the terrain classifier does not always call drivable". A band is what says
which way a body is on, for a car and now for a walker, and the walk is the same `GroundUnder.At` either
way; the grid is what a body drives and walks *over*, and it was never fit to say where one is.

**And the claim was widened by a margin of its own.** A walker's stretch went in `Person.RoadClaimMargin`
longer at each end than its box covered — seven centimetres at the shipped figures, sitting on top of the
two metres of `CarBuild.BodyMarginM` a driver's own `LaneCredit` already stops short of anything laid where
it lies. That is `SIM-7` exactly: the second gate does not make the walker safer, it makes it harder to say
what the first one is worth. The margin belongs to whoever is doing the braking, so the road claim is the
box and nothing more, and `Person.RoadClaimMargin` is now the crossing paint's alone — where it means
something, since a body on a zebra may be anywhere along the marked ground before a driver arrives.

## 2026-09-02 — a body holds the ground it stands on, and what to call it is the reader's

**The write was gated on the verdict.** A body was laid where it lies only when nothing was driving it, and
even then not on the join it was making its movement on — the ground there was covered instead by the whole
runs of that join, claimed. A claim is the one hold a right of way can take and it is in no question about
where a body *is*, so a car under a hand sitting in a box was a car every crossing driver read as empty
ground. The gate was `Driven && !Broken`, which is the reader's conclusion — obstruction or not — deciding
whether the fact got written at all.

**Now every body writes the space it occupies, and the row says only how it was measured** (TER-4c.2).
`LaneUse.Reserved` is a stretch taken from the line its owner is following, so its near edge carries that
owner's margin; `LaneUse.Lying` is the bare box a body stands in, taken from its pose. That is the one thing
`LaneCredit` needs and the only thing either name claims. Queue or obstruction is `TownWorld.KindOf`, off
the occupant and the row together: a body is a queue on the way it is driving and an obstruction on the way
it is only lying across, and the same car is both at once.

**The box is projected and not approximated.** A body askew reaches its own length across the way beside it
and its own width along the way under it, and the two swap over as it turns — so one radius is wrong on both
axes at once, leaving a broadside wreck off the lane it is lying across while writing two car lengths onto
the lane it covers the width of. `BodyFootprint` reduces the pose against each way's own tangent.

**A body is on a way the moment it touches it, and the stretch says how far aside it stands.** The first
attempt withheld the write until the body obstructed the band — which is a verdict, and the wrong one twice
over. A car straddling the line left two thirds of each lane clear of it and so was written onto neither: it
stood square across a road that could not see it, claiming nothing. The reasoning behind that gate was
still sound as far as it went — a stretch has no width, so a way written onto is a way shut, and read as bare
overlap alone every turning car closed the lane beside it and the town ground to a halt. What was wrong was
the place. `LaneSlot.AsideM` carries how far clear of the way's own line the body stands; the write records
it and `LaneOccupancy.StandsAside` is what the traffic makes of it, at half the width of whatever drives
there. **Against the line and not the band**: two metres of lane left over past a body is no use to a car
whose own line runs through the body.

**Only the walks taken along a line ask it.** What is in front, what a grant is cut at, what is coming up
behind. A question about a named piece of ground — a template asking whose the ground under it is, a junction
asking about the metres another way crosses — gets every claim unfiltered, because a manoeuvre runs over ground
no line goes down and a body it would merely reach past is a body it is about to be inside of.

**And what a body covers of a way is the box clipped to the band, not the box's shadow.** Projected whole, an
angled body claims its own length of every way it touches: a car turned forty-five degrees across its lane
reaches the corner of the next one by nineteen centimetres and held four metres of it — as much as of the
lane it was standing in. `BodyFootprint.CoversOn` clips the four edges of the box against the band and
projects what survives, which for that car is thirty-seven centimetres. It is exact rather than conservative
and that is the point: the write is the fact, and a fact eleven times too big is not a safe approximation of
one, it is a lane shut by a wing mirror. The pair of it is that a caller asking whether a body's ground is
taken has to ask with that body's box — `GroundAhead.TakenAt` takes one — since a circle at the same middle
covers a different piece of a way it meets at an angle.

**And a sweep is two poses and not a corridor.** A body driving a template holds the ground it has still to
sweep (TER-4c.1), which was laid as everything between where it stood and where it was committed to being,
on every way either end reached. On a way it was in the act of *leaving* that is a claim on the whole
manoeuvre's length: a car swinging off a lane into a bay held seven and a half metres of that lane for a body
under four. Both poses are now read — the far one at the heading the template ends on, not the heading the
car happens to be at — and a way is laid from the ends that are actually on it, joined where both are. The
same car now holds four metres and a third, which is its own box where it stands.

**And the line has to be crossed rather than touched** (`SimConfig.CrossesOntoAWayM`). Bare overlap is the
right shape and one grazing pose too many: a stretch has no width, so a body written onto a way is a body the
traffic there is told about, and a wing mirror over the paint claimed the next lane for as long
as it hung there. The bar is three twentieths of a car's width past the way's own edge, so a body has to
be leaning into the next lane rather than brushing it. **The allowance is across and not along**: a first
attempt gave the stretch a margin at each end instead, which is a different thing altogether — it lengthens
what a body holds of a way it is already on rather than deciding whether it is on one, and the near end of a
stretch is where the traffic behind is cut, so the fixture town's rescue gates priced it out at three
twentieths of a car's width.

**What is deliberately not written is what something else already answers for** (SIM-7). A driver under way
has its own claim on its line's ways and the crossing table on the box (TER-5c.1). Writing a body onto
the other joins as well deadlocked the crossroads outright — four cars each holding ground the other three
waited for, and a body is the one hold no rank can take, so the resolution the ranks exist for never ran.

**The lane running back against a driver is a gap and is left open.** Nothing is ever driven between a
carriageway's two lanes (TER-5f), so two bodies meeting there cannot be ordered: a car that stops while
still angled across the line holds the oncoming lane for the rest of the run, and the fixture town's rescue
gates gridlocked when it did. The honest place to close it is `OffTheLineAllowanceM`, which lets a car sit a
full lane's width off its line and still count as driving one; a body that far out should stop being under
way, and then it is written here like anything else.

**Two registers were still deciding where a body was.** The write stopped being gated on the verdict but was
still taken from a register in two cases, and both are bodies the town could drive straight through. A car
standing in a bay was laid onto the bay's own ways from `ParkingRegistry.BayOf` — a register given back only
by the manoeuvre that drives out of one (GEN-4g), so a car taken out of its bay by a hand at the wheel, by a
shunt or by a recovery arm went on holding two ways of a bay it was streets from and no metre of the road it
had stopped in the middle of. `ParkingRegistry.HoldsTheBody` is the second question the shortcut now has to
pass, and everything that fails it is laid from its pose like every other body.

**And a car on a tow bar laid nothing at all.** The reasoning was right — the pair is one movement and one
stretch (TER-5c.2), and the hauler's own claim already reaches back over the bar — but it only holds for
the ways of the hauler's own line. The lane the trailer swings into as the pair turns was ground with a car
in it and no claim on it. It is now laid like anything else and filed under the hauler's number
(`TownWorld.LaidAs`), which is what one movement being one occupant actually means: the truck's grant cannot
be cut at its own trailer, and the traffic behind is held off the pair as the single thing it is.

**And the walk stopped at the node instead of crossing it.** A body lying over a node held ground up to the
seam and nothing past it, so the block drawn for it ended at the mouth of the junction and the ground under
the far half of the car belonged to nobody. Two things did it and both were the walk asking about the middle
of a body rather than about the body. **The node was asked at all only where the middle projected past the
metre the lane is left at** — which for a lane whose setback is nought is the lane's own last metre
(TER-5d) — so a car a metre short of it with its nose in the box was a car `GroundUnder` never asked the node
about. It is now asked wherever the body *reaches* that metre, which is `BodyFootprint.ReachOn` against the
lane's own tangent at that end. **And a node was read as its joins alone**, which is true of a junction and
false of a node cut into a road: those carry a movement of no length (`RoadGraph.IsAPlace`) — nine hundred of
Odesa's two thousand three hundred — so the two lanes butt and there is nothing over the seam to hold what
lies across it. A node is now its lanes as much as its joins, and a body over one is on both ends. The room
the walk needs grew with it (`RoadGraph.MostLanesAtANode`), and a lane met twice — once as the nearest, again
as a lane of its own node — is written once, because one way twice is one body laid twice (TER-5c.2).

**And half the rule only ever applied to half the town.** A person standing in a lane was a stretch of that
lane and had been for as long as there were claims; a car standing on a pavement claimed nothing at all. The
reason was a rule read one turn too far: TER-5c.1 says ground the two networks *share* has one owner, which
is a fact about a zebra, and it had hardened into "a body claims the network it is on and
never into the other" — a fact about the body. Ground the walk has to itself is not shared with anything, so
a car that has mounted a kerb was standing on a footway nothing walking there could see, and a walk went
straight through it.

**A body is now written onto whichever network it is standing on, and the exception is the shared ground
alone.** The two networks were already the same arithmetic over two sets of ways, so what was missing was a
name for that shape: `IWayNetwork` — lanes with lines, widths and setbacks, nodes with lanes at their ends,
a movement over each node — implemented by `RoadWays` and `PavementWays` and taken by `GroundUnder.At` as a
generic argument. Struct arguments rather than an interface reference, because the walk runs per body per
tick and a virtual call per way is the town's hottest path. The pavement's setbacks are its mitres'
(`WalkingNetwork.WalkedFromM`), which is TER-5d in the other network's words, and a footway lane is half a
band exactly as a traffic lane is half a carriageway.

**Naming the shape found a third of them.** The ways at a parking bay are lines of the road's own with a
node at one end, so they are an `IWayNetwork` too, and a car standing in a bay had been laid by an arithmetic
of its own for want of anywhere to say so ([`BayNetwork`](../../parking/BayNetwork.cs), and the parking
slice's [decision log](../../parking/docs/decision-log.md)). The only thing the walk itself wanted for it was
a guard for a lane that ends at nothing: a bay's way runs out onto the carriageway, which is a node of
another network, and what the body standing there holds of the street is that network's own walk to say.

**A crossing is left out of a car's write on purpose** (TER-5c.1). The paint is carriageway a walk runs over,
so a car on it is a stretch of the *lane* and what holds a walker off it is that stretch looked up where the
crossing crosses; written on the walk as well it would be one body holding one piece of ground twice, in two
records free to disagree — and the one a walker reads says a body going nowhere may be stepped round, which
is a walker stepping into the lane beside a car it was already being held off.

**What it cost was one roster confusion, which is the trap `LaneRoster` exists for.** A walker steps round
the nearest body lying on the ground it asked for, and that number was read straight out of the walkers'
fleet; the first car on a pavement made it an index into the wrong array. `PersonFleet.StepsRoundOf` now
carries which fleet, as every claim already does. A car is stepped round at the circle holding
the whole of it — half its length, since it may be lying any way across the walk — so most of the time there
is no step and the walker is walled in, which hands the leg to the clock that gives up on one (PER-8).

**Three of Odesa's five hundred and fifty-five cars hold pavement after a minute**, and a bayed car holds
none: a bay stands off the kerb and the footway's band does not reach it, so the rule costs the pavement
nothing it did not owe.

## 2026-09-01 — a junction with two arms is swept into the bend it always was

**The corner read as a mistake because it was one.** A node of two arms had the whole apparatus of a
junction — a disc, a fillet on each kerb, a mouth stamped out to the tangent points — for a place where
nothing turns across anything, and the two carriageways met at a hard angle with the lane line kinked
through ninety degrees at the node. TER-5b has always said that a corner is a road that turns and only a
mid-block crossing is a junction; the generator was laying the authoring mistake the rule warns about.

**The two arms are now swept into one arc and the node stands in the middle of it.** Each road takes half
the turn, so both arrive on the same tangent and the pair is one continuous curve — kerb, carriageway and
centreline together — and what is left at the node is an inline junction: two arms leaving in opposite
directions, no corner to fillet, no ground of its own. The paint stands past the end of the arc, which is
the same rule as everywhere else with the bend in place of the box (GEN-12a).

**The radius is the widest the two roads can spare and never wider than the class's floor.** Wider is not
better here: the arc is tangent to both arms, so every extra metre of radius cuts further inside the corner
the layout chose, and past the floor there is nothing to buy — the speed the road is laid for already holds.
Below `SimConfig.RoadCornerRadiusM` there is nothing to sell either: that is the bend whose inner kerb stands
where the junction's fillet would have, so anything tighter is a worse corner than the one it replaces and
the node keeps its junction instead. **The turn itself is the layout's**, which is why the arc may be tighter
than the floor GEN-12 binds the wander to: a car slowed for that corner when it was a box and slows for it
now that it is a curve.

**Two things had been leaning on the box without saying so**, and the sweep found both. A lane line stopped
at a junction only because the paint on its arm closed the ground back to the disc, so an arm that carried no
paint — one too short for a crossing — was dashed into the throat; and a car park's section was kept clear of
the disc rather than of the ground the junction reaches, so a cut could stand on the corner an arm flares
through. Both now read `RoadCuts.ReachesM`, which is `SimConfig.JunctionArmReachM` and the bend at a node
with no fork, in the one place either of them asks.

## 2026-09-01 — a junction with two arms is crossed once

**Every corner in a generated town was painted twice.** The placement rule is a crossing on every arm, and a
node of two arms — a bend the layout put a junction at, a mid-block crossing — has two of them, so a driver
turning one corner met a zebra a few metres before the node and a second a few metres after it, each with the
setback and the paint the rule gives an arm of a crossroads. Nothing turns at such a node: everything that
arrives leaves the only other way, so what the two zebras stood for was one road crossed twice.

**The node now carries one**, on whichever arm has the most road left behind it, and the paint of the two
arms is one bundle rather than two. Seventy-six of Odesa's two hundred and ten junctions are these, and the
town lost a hundred and fifty-two zebras without losing a way over a single carriageway: one zebra joins the
same two pavements the pair did.

**The bars are the crossing's and not the arms', which is the same argument again.** One per arm was tried
first, the lane coming the other way stopping before the node it turns through — where every other junction
in the town puts a bar. It is the wrong picture: there is no box here to hold traffic out of, so a bar half a
corner short of the paint stops a car for nothing it can see. The pair stands either side of the zebra now,
each on the lane driving at it, and **the bundle begins where the corner's ground lets go** rather than a
setback past it. The setback everywhere else is a distance from ground cars turn across, and here nothing
turns; laid this way the far bar's outer edge falls exactly on `SimConfig.StraightStubM`, so the deepest
bundle the town paints still lies wholly on the straight a road leaves its junctions on and no road had to be
lengthened to hold it.

**The lane line runs up to the bundle and through the node behind it.** Nothing at such a node closes ground
off the line — not the paint, which everywhere else closes back through its own disc, and not the disc, which
everywhere else is the box itself: what a dash must not be laid down is ground the movements through a box
are driven across, and a node of two arms has none of it. Three claims stated the box rule and had to learn
the exception — `NoDashIsLaidBetweenAStopBarAndItsJunction`,
`NoDashIsLaidBetweenACrossingAndTheJunctionItApproaches` and `NoDashIsLaidInAJunctionOrOnACrossing` — and in
each the claim was wrong rather than the paint. The gaps inside the bundle are shorter than one dash, so
nothing is drawn in them, and the two arms' lines meet at the node the way a bend's does.

**The disc still closes where the node's own crossing is laid on it**, which is the inline junction of an
authored map (TER-6): that zebra stands past the end of every lane there, so nothing else would break the
line and the dashes came out drawn down the middle of its bars —
`EveryCrossingIsStripedRightAcrossItsOwnSpan` on the fixture map, which is the check that found it.

**The bars are painted though nothing there is lit.** A node of two arms admits no conflicting movement and
so carries no bundle (TLT-3), and a bar at an unlit junction is elsewhere the ranking's job to make
unnecessary — but there is no ranking here either. The only thing that governs the crossing is the walker's
right of way, and the bars are what say where the stop for one is made. A head, if a map ever authors one,
hangs off a bar like every other and stands beside the zebra with it.

**The far bar of the pair is on a lane leaving the node**, so `LaneFurniture` reads the lanes out of a
junction as well as the lanes into it. A lane keeps the bar at the end it drives toward and takes the one
behind it only where nothing was painted ahead — the other way round, a lane running from one of these to a
lit junction would hold the paint behind it and be stopped at a red the junction in front never showed.

## 2026-09-01 — a centreline stops at whatever paint the arm carries, not at the bar

**Every unlit junction in the town was dashed up to its own mouth.** The rule that keeps a lane out of a
junction's throat was written round the stop bar: a bar closed the ground from itself back to the disc
behind it, and the crossing in between was inside that span. A junction the ranking governs carries no bar
(TLT-3) and has the same metres of turning ground, so what it got was the disc alone — and the nine metres
between the disc and the zebra came out dashed, drawing a lane running into a box nothing is lit for.

**The junction is now carried by the paint rather than by the bar.** Each piece of paint on an arm names
the junction it approaches, and every one of them closes the ground from itself to the far side of that
junction's disc; a lit arm's bar stands further out than its crossing and closes the longer span, an unlit
arm's crossing closes what there is, and a crossing struck mid-block names no junction and closes only its
own bars. It costs one loop and no new figure, and it is the same rule the markings section always stated.

## 2026-09-01 — a zebra spans the road it names, and carries no span of its own

A crossing used to carry a span beside its centre, its axis and its depth, and every planner filled it with
the width of the carriageway it was laying — so the field was a copy that agreed with the road until
something laid one of the two again, and nothing in the suite could say which of the two a disagreement
made wrong.

A crossing now names the road it is painted across, the way a stop bar already did, and
`CityPlan.CrossingSpanM` solves the reach off that road's width — one answer, wherever the span is drawn,
walked, stopped for or tested (TER-6). What each crossing is *for* stays its own: the depth is the
crossing's figure, because how much of a road's length the paint covers is nothing the road decides.

**The skew is part of the relation and not an exception to it.** `Zebras` carries one crossing laid off
square on purpose, and kerb to kerb along that paint's own axis is 8.83 m of an 8.00 m road — which is what
the file's field held, and what the derivation now gives without being told. It is a projection, so it is
solved at load: `LaneFurniture` keeps the answer for the driver, who asks it of every crossing ahead of it
every tick.

The two fixtures still arriving as files keep a span in the format, because the format has no writer left
to drop it with. The reader takes the field and throws it away, finding the road under the paint instead —
which is the thing the field would have had to agree with.

## 2026-08-29 — a body level with the asker is not in front of it

Five minutes of Odesa left twenty-three walkers standing still for the whole run, and every one of them was
in one of three crowds queueing behind a pair that never moved. Each of the pair had a grant of minus two
metres, cut by the other, and each was the other's cut: two bodies at one metre of one way, each held off
ground the other was standing on, for the rest of the run.

The pair is what the **end of a way** makes of everything that reaches it. A walker is placed on the way its
own line is stationed on, and one that has walked past the end of that way is carried back to the last metre
of it (`IsAfoot`); its ask is then a window of no length at that edge, and every body clamped there has the
same front. `NextSpokenFor` already says *in front means the body is*, so a body whose own length stops short
of the asker's front never reaches the cut — what reached it was the body whose front is exactly the asker's,
which is neither in front of nor behind anybody.

`GrantedOn` passes over that one now, on the same argument the claim above it is passed over on: a cut behind
the asker's own front is not a shorter grant, it is a grant that has stopped being a distance to walk. It is
the abreast case and nothing wider — a body whose length reaches past the asker binds exactly as before, and
an overlap still comes back negative and says so. `FootOccupancyTests` states the same relation, which is why
its "in front" test is now inclusive: the pair at a way's end are not two bodies one behind the other, and
asked as though they were, PER-13 has no answer either of them can act on.

Two walkers stayed still for a minute over the same five minutes afterwards, and both were walking again by
the end of it.

## 2026-08-29 — a grant is a distance in front of the nose, and a claim behind it is not a cut

A car crossing a junction would stop dead halfway out of the box and stay there for half a minute with the
road in front of it empty, reporting `P-8 queueing`. Its grant was minus seven metres — a car's length of
negative road, which inverts to a target speed of zero whatever is ahead, and which nothing the traffic
does can ever hand back.

Two things made it. The first is that a way the nose has already left was still being asked. The nose is
carried onto each of a car's ways by clamping (`OnTheWayM`), so on a way that ends behind it the question
became a window of no length at that way's far edge, and anything reaching that edge answered it. The
second is that the stretch reaching it was a claim: the car queueing behind for the same movement lays its
claim from where its own road was cut, which is the leader's near edge — a car's length behind the leader's
nose — and read as a cut, that claim answered the leader from underneath its own body. The pair of them are
a stable trap: neither car can move, so neither ever stops laying what freezes the other.

`WhereTheGroundIsCrossed` already said the half of this that applies to a crossing point — *in front means
in front of the nose*, and a cut behind it is not a shorter grant but a grant that has stopped being a
distance. The grant now says the same about a stretch: `GrantTheGround` asks nothing of a way the nose is
past, and `GrantedOn` passes over a claim whose near edge is behind the asker. **A claim is ground its
holder has not reached** (TER-5e), so one the asker is standing on is ground the asker has — it can never
be a body to be held off, and it is the one kind of stretch that cannot be a contact.

What is deliberately still allowed to answer from behind is a body: a claim, a wreck or somebody on
foot reaching back past the nose is an overlap, and the grant is left free to come back negative and say
so. That is the whole of the difference between the two halves of the unit case in `LaneOccupancyTests`,
and `NoClaimCutsAGrantBehindTheNoseThatAskedForIt` watches every shipped map for the rest of it.

## 2026-08-29 — the claim holds the answer, and one metre is one body's

`TER-4c.1` has always said that ground is asked for, answered, and then it is the asker's, and that a
mechanism which could grant the same metre twice would be no mechanism at all. The code granted correctly and
then threw the answer away: `AskForTheGround` laid the ask — bounded by the rules that stop the car and by
nothing in front of it — `GrantTheGround` worked out where the traffic cut it, wrote that to
`CarFleet.AuthorityM`, and left the ask standing as the claim. Every other reader for the rest of the tick
read the question. A car held at a red still held the sweep of road beyond it; the movements that road
crossed were refused by ground its holder had itself been refused. Asked of the shipped maps, two bodies
held one metre by as much as 13.72 m on Fleet, 3.63 m on River and 0.95 m on Odesa.

So there is a third walk: the asks, the grants, and then `CutTheGroundToTheGrant`, which brings every
stretch's far edge back to what its owner was given. It is a walk of its own because it moves far edges,
which is what a movement's crossing question reads — done inside the grant loop, the answer would turn on
which car was asked first, and that is the same reason the asks and the grants are already two.

**On the join a car is crossing, the seam moves and the union does not.** What such a car holds there is its
road and the claim beyond it, laid as one piece of ground with the join between them wherever the road
happened to reach; cut without the claim following, the metres between the answer and the ask fell out of
both, and a car sitting in a box sat on ground a crossing movement was free to be granted.
`ClaimWhatTheAnswerTook` hands them over — as a claim rather than as road, which is their honest name: they
are metres the car has not reached, and a car already committed to the box holds them with the rank that says
so.

**And the credit is gone with it.** A stretch in front used to be worth its holder's own stopping distance,
on the ground that a body under way will have left those metres by the time anybody arrives. That is a true
thing about traffic and the wrong place to say it: once the answer is written back, a credited answer *is*
two bodies holding one metre. The cut is now at the near edge and never past it, and the standstill case is
untouched — a stopped body was always worth nothing.

What that buys is the junction. Odesa's minute abandons 6 cars against 15, gives up 13 places against 18,
reroutes 14 times against 20, makes 50 emergency stops against 64 and takes 803 junctions against 799;
River takes 167 against 158 and holds at a line 70 times against 74. What it costs is station-keeping at
speed, and the proving ground is where that shows: fifteen cars at seventy-three metres a second can no
longer sit in each other's stopping distance, so they overtake — 9 swerves against none — and one of them,
once in twenty minutes, came off its line by 5.49 m of the 3.00 m the lap allows and gave the lap up. **That
claim is broken and left broken**, because it names something real: a swerve at that speed that does not
finish is `E-4`'s to answer, and papering over it in the rig would be measuring the ruler.

`NobodyIsGrantedGroundSomebodyElseWillStopOn` is deleted rather than mended. It asked of one map whether a
grant reached past where the car in front would come to rest, which is the credit's own arithmetic;
`NoTwoBodiesAreGrantedOneMetre` asks every shipped map the stronger question the requirement actually states,
and two tests of one claim is the second mechanism `SIM-7` is about.

## 2026-08-29 — the box refuses a car at a place, and only lets it in where it can wait clear

The gate on a junction answered *whether* and the grant answered *where*, and they were the same question
asked at two resolutions. `TheMovementIsFree` walked every section a movement was driven over and came back
`false` on the first one that was anybody's, and the car was then stopped half a body length short of the
boundary — so a body standing on the far corner of a box held the near half of it against a car that would
never have reached the corner while it was there. The grant already cut at the near edge of the first
section that was held (`WhereTheGroundIsCrossed`), a margin short of it; it never got to say so, because the
ask is clamped to the stop point the gate had already set.

So the gate answers in metres too, on the same figure: `FirstHeldOnTheMovementM` returns the near edge of
the first section that binds, and the car is stopped a body margin short of it. That figure is what keeps
it from deadlocking where the verdict did not — a car held a margin short of a section claims no metre of
it, so the movement crossing there still reads it free and goes, and the two resolve instead of standing
one on each side of the ground they share.

**What it cost, until the second half of it went in, was cars stranded in the box.** A car let up to the far
side of a wide junction stands on every crossing it went over to get there; the movements behind it are then
refused by a body that is itself waiting, and Odesa's minute abandoned 23 cars against the 15 it abandons
either without the change or with it complete. So a car may only be let in as far as it can come to rest
with its whole body in a gap between the runs (`WaitsClearOfTheCrossings`) — which is the box's own version
of not entering one you cannot clear, measured rather than assumed. Where there is no such gap, and a
crossroads whose arms are one lane each is generally such a box, the car stops at the boundary exactly as it
did before: the shipped towns' tallies are unmoved, and what the change buys is the wide box, where the
free ground is real.

## 2026-08-28 — the walkers claim the road between the asks and the grants

TER-4c says a person in a lane cuts the road a driver is granted exactly as a car standing there would. It
did not happen. The walkers claimed the road as the *last* pass of the rebuild, after
every grant had been taken off it — and the claims are wiped at the top of the next rebuild, so no driver ever
read a band while it was deciding how much road it had. What held a car off somebody on the paint was the
crossing's own stop and the headway reading, and nothing at all held one off somebody standing on
bare carriageway except that same headway.

The order is one question with the walkers on both sides of it, so they belong between the two halves of it:
the cars' asks first, because what a body at a kerb may step onto is whether a driver's road is over the
band; then the walkers; then the grants, because a band claimed is ground a driver may not be granted.

River's measured minute went from two people knocked down and two cars wrecked to none of either, and its
touches from 55 to 50.

## 2026-08-28 — what a body is written onto and what a manoeuvre reads are one walk

Two pieces of code answered *which ways is this place on*. Writing a body that is not driving a route laid
it on the lane it was nearest and on every join of the junctions at either end of that lane; reading the
ground under a manoeuvre's template asked the nearest lane and stopped. So a car crossing a junction — whose
road is written on the **join** and on no lane at all (TER-5c.1) — was invisible to every swerve, back-off
and bay exit swinging through the same box. The claims were not wrong; nobody was asking them.

There is one walk now (`GroundUnder`), and both sides call it: the lane, **the lane running back the other
way** where the body reaches into its band, and every join of a junction the place is lying under. Writing
and reading cannot drift, because there is nothing left to drift.

**It costs the town its reactive templates where the ground is genuinely somebody's**, and that is the
finding rather than a side effect. Odesa's measured minute went from 13 back-offs to 2 and from 9 cars
abandoned to 15: a back-off happens where a car is stuck, a car is stuck at a junction, and reversing into a
junction is reversing into ground the traffic crossing it is committed to. The ladder escalates instead —
19 places given up against 12, 20 reroutes against 13 — which is the honest answer to *there is nowhere to
back into*, and the alternative is a car reversing into a movement that cannot see it.

## 2026-08-28 — a claim is answered every tick, and its holder is told when it loses

A claim was answered once, at the moment the desk took it, and re-laid unread from the car's own field for
as long as the entry wanted it. Every other claim is laid and answered afresh every tick; the
claim was the one hold that remembered an answer. So a right of way took the ground and nothing said so: the
stronger movement was not cut, the claim's holder was not cut either, and the pair drove at the same metres
from opposite sides — the exact failure TER-5c.1 exists to prevent, reintroduced by the one stretch that
skipped the walk.

It is answered again after every body has claimed and before anything is granted off it, and a claim a
stronger rank has taken is withdrawn inside the same tick. **The holder is told**, because the only thing
that knows what a claim was for is the entry that took it: that entry is re-entered through its own `Sa`,
and takes the claim again or hands on.

**A rank above it takes a claim and nothing else does.** The first version also gave a claim back for a body
standing on the ground — which reads well and is wrong twice over. It is the duplicate SIM-7 is about, since
a body on the ground already cuts the claimant's grant on the way it is driving; and it refuses the one
thing a claim is for, because the stretch `E-4` claims is by construction the stretch containing the body it
is swinging round. Measured, it took every swerve and back-off out of the town on the first tick.

## 2026-08-27 — a junction admits no movement that reverses the direction of travel

The turn-around was in the table from the beginning and was drivable by nothing: two opposing lanes a
lane's width apart join on a 1.5 m semicircle, tighter than any car's lock at any setback. It was carried
anyway — classified, laid, measured against every other movement at its node, given the bottom rank of the
right of way, and priced at infinity so that no route could be handed one — which is a great deal of
machinery to say *never*.

It is gone (TER-5f). A lane's successors are the lanes leaving its node **that are not the one running
back down its own stretch**, and everything downstream got shorter for it: no join to draw, no rung to
skip in the setback widening, no row in the crossings table (a quarter of Odesa's 1472 movements were
turn-arounds), no rank at the bottom of `RightOfWay`, and no `continue` in the overlay, the census or the
tests to keep it out of a figure.

**What a route may still do is come back down the other side of one stretch**, and it is priced rather
than joined: a car park's frontage, where the driver parks and unparks (`GEN-4l`), and a dead end, where it
works itself round (`P-19`). Which stretches those are is a fact about the town laid with it — the ways at
the bays, and whether a lane has any way out at all — and it is handed to the network as flags, because
the road is below the car parks that hang off it.
## 2026-08-27 — an obstruction is a claim that generally reaches nowhere

A body the road was not driving held the metres under it and nothing else: its footprint, no ground ahead.
That is right for a wreck standing in a lane and wrong for the same wreck two seconds earlier, still sliding
— and wrong in the direction that costs, because the traffic behind was granted the road that body was about
to be standing on. The pavement never had this problem: `AskForThePavement` lays every walker, moving or
standing, as one stretch reaching `max(stopping, …)` ahead, so a standing walker is already a claim
that reaches nowhere. The road's side was the outlier.

So a body that is not driving a route lays the third edge every other body carries — its own stopping
distance past where it stands, from the speed it actually has. **Nothing is a special case any more: static
and slow bodies claim almost nothing ahead and fast ones claim a lot, which is the same arithmetic a
driver's own claim is, asked of a body with nowhere to go.** Whether a thing can be got past is a separate
question and is not being answered here; it is the reader's, off the occupant.

**Where the body is sweeping a template, the sweep is that ground and is already laid.** The two are one
answer to one question — what this body is committed to — read once off the line it is driving and once off
the speed it is doing, and taking both is a car holding a swerve's worth of lane twice over.

**What the measurement threw out was the tidier version.** Laying the stretch with a margin behind it, so
the credit could read "a body is worth its stopping distance" with no switch on `LaneUse` at all, reads
better and measures worse: a margin on the stretch is not only a gap for whoever is behind, it is a fatter
body in *every* question asked of the claims — the templates a manoeuvre may lay, the junction sections a
movement reads as free. Odesa's measured minute went 69 touches to 88 and River's 35 to 54, with peak
interpenetration up from 208 mm to 325 mm. So the margin stays where it was, the credit keeps its one
switch, and the switch is documented for what it is: whether the stretch carries a margin of its own, not
whether the thing on it happens to be moving.

With the ahead-extension alone every map is back to its baseline to the touch — Odesa 69, River 35 — which
is the honest report: the hole it closes is one a measured minute of these towns does not open. It is pinned
by a test instead (`ABodyOffItsRouteHoldsTheRoadItsSpeedStillNeeds`).

## 2026-08-27 — the grant is a question the claims answer, and both networks ask it

The road's grant and the pavement's were the same forty lines twice: walk the ways under the ask, walk the
stretches spoken for in front of the body, cut at each holder's near edge plus what the ground beyond that
edge is worth, take the least. Both switched on `LaneUse` to decide the credit — a body under way is worth
its own stopping distance, anything going nowhere is worth less than nothing, and the asker keeps its margin
off that — and both then made a second cut, at a place that is nobody's stretch, with the same margin
subtracted by hand: a junction's crossing point on the driving side, the kerb line of a refused lane on the
walking one. Two copies of one rule, in two slices, free to drift.

So the grant is `LaneOccupancy.GrantedOn`, asked like every other question of the claims, and what the asker
brings to it is `LaneCredit` — its braking, the ground it keeps off a body that is going nowhere, whose
claims it reads as traffic, and the rank it asks with. **The credit rule and `Binds` now exist once**,
and the place-cut reads the same figure by name (`AtAPlaceM`) rather than restating it. A walker asks with
the weakest rank, which is the honest statement of what was already true: no claim on the pavement is a
walker's to take, so every stretch in front of it binds.

**It changes no arithmetic.** The map from a way's own metres back to the line is affine and increasing, so
taking the least on the way and carrying it home is the least carried home; `Binds` at the weakest rank
admits everything, which is what the walking side did by not asking. The suite is green and Odesa's measured
minute is unmoved — 10 wrecked, 69 touches, identical run to run.

**Two smaller things fell out of it.** A rank is now a floor on the walk (`NextOver`) rather than a filter
over what came back, so "is a rescue over this" and "is anybody on the paint" stopped being hand-rolled
loops and became masks like their siblings. And `Nobody` no longer matches itself: the town's furniture
stands under that number, so a question asked by nobody in particular — a walker at a kerb, an overlay —
excluded every bollard in the town from its own answer. Nothing in the town asks one of those with a mask
the furniture is in, so it cost nothing today; it is the trap the furniture was given a claim of its own to
escape, sprung from the other end.

## 2026-08-25 — where a road's paint breaks is the road's answer, not the drawing's

The dashes were laid by walking each road at a metre a step and asking whether the point was inside a
junction disc or on a zebra. Two things were wrong with that and only one of them was the sampling. A
crossing is set back onto the arm it approaches, so the disc is several metres behind the paint, and the
metres between them are neither a junction nor a crossing — every arm of every junction in the town was
therefore dashed right up to the mouth of the box, past the bar a driver is meant to stop at. The metre
step was the smaller fault: a run closed on the first blocked sample, so it could carry up to a metre of
zebra with it and lay a dash on the bars.

Both are gone because the boundaries are no longer looked for. `CentrelineRuns` takes them from whoever
already measured them — `RoadCuts` for the discs, the stop-line register for the bars — and a bar closes
the road from itself through the junction it names, which is one span with the zebra inside it. The runs
are exact, and the drawing lays dashes along what it is handed rather than deciding anything.

The kerb line went the same way for the same reason. It survives as a rim on the union of ribbons, discs
and fillets, and a car park was not in that union — so the line was painted straight across the frontage
every car entering the lot drives over. **The lot could not be added to the union**: it is laid flush
against the kerb rather than over it, so its fill covers none of the rim. What breaks the line is the
frontage itself, which `RoadFrontages` now owns for both the slices that need it: this, and the cuts
`ParkingSections` sets back from it. It used to derive the same projection privately, which is the second
copy the markings' first rule is about.

Breaking the kerb line left the same line painted a hand's width away, because a bay was outlined on four
sides and a row of bays laid side by side runs its mouths into one unbroken stroke down the lot's frontage.
A bay is drawn on three sides now. The mouth is the end a car crosses, and which end that is needs no
geometry: the bay's heading points into it, so the mouth is the end behind its centre.

The three are laid end to end and not each to the bay's own size. Laid to size, every one of them stops on
the line the next is *centred* on: half a stroke of each corner painted twice — and paint is a multiplying
tint, so that reads as a bright square — and half of it not painted at all. The sides own both corners,
running to the head stroke's far face, and the head runs between their near faces. `ABayCornerIsPaintedExactlyOnce`
is the gate, and it samples a quarter of a stroke either side of the two centrelines that cross there, which
is the ground the old overlap stood on and the ground the old notch left bare.

Laid to the bay's size the outline also missed the lot it stands on, on all four sides. The bays fill their
lot exactly, so every outermost line of a row stands on the lot's own edge — and a line *centred* on that
edge hangs half its width over it, onto the walk or across the kerb line the lot's edge is against, while
the mouth ends stopped short of the same line by whatever the town's arithmetic left over. Neither slip is
worth a figure of its own. **A stroke within a line's width of the lot's edge is laid against that edge,
inside it**, which is the tolerance `LotFrontage.FrontsTheKerb` is already measured by; a stroke further in
than that is one between two bays and stays centred on the boundary the two share. The reach is the bay's
own line cast at the lot's rectangle, so what the paint ends on is the lot's coordinate rather than a
second measurement of it.

That left the kerb line breaking a hand's width before the lot's own paint began, and the strip of bare
tarmac between the two is at the mouth, where a driver is looking. The frontage was the lot's *centre*
projected onto the road plus the reach of its rectangle in the road's direction — exact on a straight road
and nowhere else, because a corner of a rectangle beside a bend does not stand abeam of the metre that sum
names. `RoadFrontages` projects the four corners themselves now and takes the least and the greatest, which
is the same measure the strip that breaks the line is struck at.

**The last of the gap was that the two lines were laid to two different edges.** A lot is a rectangle and a
kerb is a curve (`GEN-4b`), so a lot's mouth edge is a chord of the carriageway's and stands up to that
chord's sag off it — and paint ended on the rectangle stops that far short of the kerb line it is meant to
turn into. What the road owns is now asked of the road: `ReachToTheKerbM` answers where a stroke's own line
crosses the carriageway's edge, each stroke asking for itself, because two lines a bay's width apart cross a
curve at two different points. The reach it will grant is bounded at half a bay's length, which is the
figure that cannot drag a stroke standing behind another row of bays.

The break in the line is over the lot's **mouth** and not over the shadow its whole rectangle casts, which
on a lot standing askew to the kerb runs past the paint by the depth times the skew — `LotFrontage` carries
both, because the cuts `ParkingSections` sets back still want the shadow. And it stops a line's width short
of either end: the kerb line runs to the far face of the outermost bay stroke, which is the same end-to-end
rule the bay's own three strokes are laid by with the kerb line as the fourth, and it is what fills the
corner square that a line ending exactly on the lot's corner leaves bare. The gate is
`EveryStrokeAtTheMouthOfAKerbedLotEndsOnTheCarriagewaysOwnEdge`, and it is asked of the paint rather than
of what the paint was laid to. Overlap, where the ends meet, costs nothing: the ground carries no blending
and its textures are
anchored to the world, so paint over paint is paint — only a gap is visible, which is why every one of these
ends reaches a chord's sag past the line it meets rather than stopping on it.

## 2026-08-25 — the right of way is a rank on a stretch, and it takes claims and nothing else

Two crossing movements each read the other's ground and each were cut at it, so a junction went to
whichever of them asked first — and "first" is the order the rebuild happens to walk the cars in. A car
turning across the oncoming stream could therefore take a box off the traffic going straight simply by
reaching it a tick earlier, which is not a rule anybody could state.

The alternative considered first was a table of pairs — this movement gives way to that one — which is the
verdict TER-5c exists to avoid: it answers *may I go* for a whole junction at once and says nothing about
where. What is stated instead is a **rank carried by the stretch**, taken from the turn the movement makes,
so the comparison happens exactly where two pieces of ground meet and nowhere else.

**What made it safe rather than merely one-sided is that it takes a claim and never a body.** A claim is
already the town's word for ground somebody has not reached and is not committed to, so it is precisely the
ground that can be handed back; a committed claim is the road a body needs to stop in, and taking that would be
a licence to drive into whoever holds it. The one hole in that was a car past the point it could stop short
of its box — its ground there is still a claim, and it is going in whatever anything says — so a committed
car lays the same claim at a rank nothing outranks. That is why `CommittedToTheBox` is a field on the car
rather than a stopping distance worked out a second time where the claim is laid.

**Revocation is then the same fact read the other way**, and it is bounded the same way: a crossing already
taken is given back when something with the right of way over it asks for the same ground, and only while
this car could still stop short. The alternative — recomputing whether the crossing would still be granted —
is the thing the crossing state exists to avoid, because it moves under a car that is merely slowing down.

**Both halves of the exchange happen inside one walk, and the tick they land in ends with both cars holding
a movement.** The weaker one asked first, was given ground nothing had claimed yet, and had it taken off it
a few cars later; it gives the crossing up on its own next ask, which is where revocation lives. So *already
crossing* has to be measured from before the walk and not from the fleet the tick leaves behind — read the
second way, the pair reads as a car waved into one that was crossing, and the one that was crossing had not
been given anything at the moment the other was. `NothingOnTheApproachIsGivenGroundAnotherCarIsCrossingOn`
holds the previous tick's crossings for exactly that, and the exemption it makes for the losing side is
counted and asserted (`GivenUpToAStrongerMovement`), because an exemption nothing exercises excuses anything.
The shipped towns pass that claim either way round; it is any nudge to the parking sections — the metre a
lot's frontage begins at moving a hand's width — that puts the two grants of a pair in the same walk.

## 2026-08-25 — a walker's refusal is a claim of its own, because a right of way nobody can see is not one

The pedestrian priority at an uncontrolled zebra had nowhere to live. A walker refused the band it asked for
simply waited, and the traffic learned nothing: the only thing a driver ever saw was a body already on the
paint, which is the one case where giving way is too late to be a courtesy. Spending the kerb patience and
stepping out regardless works, and it is what the town did — but it makes the walker force every crossing
and puts a body in front of traffic that had no warning.

So the ask itself is claimed on the road, at a rank of its own. It is in no scope that cuts: no grant is cut at it,
because a cut is what a *body* is worth and this is somebody on the pavement. What it does is put a stop
point in front of the driver — the same stop a body on the paint produces — and a body stopped short of a
crossing already holds none of it (TER-4c.1), so the band frees itself on the next tick and the walker takes
ground the traffic gave up rather than a gap it found.

**The safety of it is the stop's own bound and not a rule beside it.** What a car asks for is never less
than the road it needs to stop in, so a car too close to the paint keeps it, the band stays refused, and the
wait lasts another moment. Nobody is waved in front of a body that could not have stopped for them, and
nothing had to be written to say so.

## 2026-08-25 — an inline junction's crossing is laid across the lanes at the node

The one thing TER-5b says an inline junction exists for — to carry a mid-block crossing — did not work. The
paint is laid on the node itself, which is further from every lane's end than the paint is wide, so the
projection that puts crossings onto lanes found no lane for it: no driver slowed for it, and a walker
standing on it claimed nothing of the road and was invisible to the traffic. Every such crossing in
the shipped towns was lit, and the lights hid it.

It is laid across the lanes that meet at the node now, each at its own end — the arriving one at its length,
the leaving one at nothing — which is where the paint actually is, since an inline junction paves no ground
of its own. The fallback is taken only where the projection found nothing *and* the junction admits no
turns; anywhere else a crossing is set back onto the arm it approaches and is found where it lies, and laying
it on the node regardless would paint one crossing across every arm of a crossroads.

## 2026-08-24 — the town's furniture is a claim nobody owns, and not an occupant number

A bollard in a lane was claimed as an obstruction belonging to `Nobody`, and `Nobody` is also
the integer a query names when the asker has claimed nothing at all — a walker at a kerb, a body about to
step off one. So the two questions the walkers ask about traffic, `AnyTrafficOver` and `BehindBody`, were
skipping the town's furniture because the exclusion they asked with happened to name it. The answers were
the ones a town wants: a walker does not wait for a bollard, and a bollard is not something that stopped
for it. They were reached by an argument of one question deciding the answer to another, and a query
written next with the same exclusion and a wider scope would have dropped the furniture where TER-4c says
it must be read.

The fact is the claim's own now: a prop is in neither roster (`LaneClaim.IsFurniture`), so a driver's grant
is cut at it like anything else on the lane and whoever asks what is **coming** down a lane is asking about
wheels. Nothing about the town moved; what moved is where the answer comes from.

## 2026-08-24 — a road may be cut where nothing crosses it

`RoadCuts` cut a road at the junction discs it passed through and at nothing else, and the node count was
the plan's junction count. A car park wanted a node of its own (`GEN-4h`) and is not an intersection: no
arms meet there, nothing crosses, and taking a disc's worth of ground out of the street for it would be
a box invented to hold nothing.

So a cut is now either a disc's bite or **a point** — the same list, the same sort, the same stretches
between consecutive entries — and the nodes a slice above asks for are numbered after all the plan's
junctions, so nothing is renumbered. What reads the graph did not have to change: a place is a node with
two lanes in and two out whose joins have no length, and the one thing that had to learn about it is that a
join of no length is not a movement to be taken.

**The cut is asked of the plan and never of the graph** (`ParkingSections`). Where a road is cut is what
makes a lane, so a construction that read a lane to decide where to cut would need the graph it is
building; a lot's frontage is its own rectangle projected onto the road's centreline, which is the measure
the junction discs are already taken against.

**It gives way to what the road already carries.** A cut that would leave a stretch too short to drive is
dropped, and so is one that would land on a zebra or a bar: a lane end inside a crossing splits the approach
from the paint, and a driver then first hears of the crossing on the lane *after* the one it is braking on.
That was not a guess — it was River, where cars met such a crossing at 18 m/s having never been told it
was there.

## 2026-08-24 — the table of crossings is indexed by way, so a way laid off a junction can use it

The table said which *movement* took ground off which, and a movement was a turn slot. That was exactly
right for as long as the only ways that could overlap were the joins through one box: the lanes hand over
clear of the disc (TER-5d), so a join is never driven over a lane and a lane never needed a row.

The way into a parking bay is the case that breaks it. It leaves its lane part-way along, sweeps the lane
running back the other way and ends off the carriageway — so what it takes ground off is a **lane**, and a
section that could only name a turn slot had nothing to name. The alternative was a second table for bays,
read in a second branch of the one query that cuts a grant, which is the duplicate SIM-7 is about and is
the one place it would have been got wrong: where a car park meets a street.

So a section names a **way** and the table is laid over every numbered way. Every lane's row is
empty on a street with no car park on it, which is nearly all of them — the walk over an empty row is a
bounds check, and it buys the one index in which a junction's join and a bay's way in are the same kind of
thing. `LineOverlap` is the measurement itself, lifted out of the road graph so that the ways at a bay are
measured by the code that measures the joins, and not by a second implementation that agrees with it until
one of them is changed.

**What it cost was the whole-way fallback.** Two lines crossing square can have every sample of one fall
outside the clearance while the other's fall inside, so a crossing found from one side had to be given
*some* interval on the other; between two joins a dozen metres long, "the whole way" was near enough. A
bay's way in against a two-hundred-metre lane, it was the whole street. The missing end is now the found
one's shadow — the samples of the other line standing nearest it — and where no sample of it stands near
enough, the crossing was an artefact of where the samples fell and is dropped.

## 2026-08-23 — a template holds the ground it sweeps, and not the pose it is passing through

A car driving geometry of its own — a recovery straight, a bay exit, a swerve — claimed the
footprint it stood on and nothing more. The line it was about to drive down had been walked before it
was laid (`GroundAhead`, the desk's own check) and was then **left open**: every other driver read it as free
road, was granted it, and could come to rest in it while the manoeuvre was still a second from arriving.
Odesa's soak found it as two wrecks a minute — a car reversing at manoeuvring pace into a driver that had
stopped inside the straight, itself pinned by the reverser's whole-join claim and unable to move.

So a body that is not driving a route is laid over **the whole sweep its line has still to make**, from where
it stands to where that line ends, on every way that sweep runs over. TER-4c.1's "and then it is the asker's"
now covers a template as it covers a lane: walked before it is laid, held for as long as it is driven, and
re-laid from the body every tick so nothing has to be released.

**Read from both ends and laid once.** Which ways a sweep is over is a question about a pose, and the ways
under one end of it are regularly not the ways under the other — a straight of the reverse bound is twice the
length of a body. Both ends are asked, each lays the whole interval, and `LaneOccupancy.AlreadyHolds` is what
keeps one body to one stretch of one way (TER-5c.2) rather than the order the two readings were taken in.

**A bay exit is no longer one of them**, and that is the shape the rest of this should take. The way out of a
bay is a numbered way, so its driver is a driver on a way: what it holds is a claim along that way
and the crossings on it, and no sweep is read off its geometry at all
([parking](../../parking/docs/decision-log.md)). What is left here is the recovery straight, the swerve and
the legs of a turn on the spot — the lines the town did not lay.

**What it does not do is make the reverser see.** A car on a template still senses nothing — its context is
empty and its authority infinite — and that is unchanged: what stops the collision is that nobody else is
granted the ground, which is the same mechanism as everywhere else and not a second one (SIM-7).

Odesa after it, at a tail share of 0.6: **0 wrecked, 46 touches, 3 stuck ticks, 118 mm at the deepest**,
against 4 wrecked, 56 touches, 15 stuck ticks and 827 mm before.

## 2026-08-23 — the tail keeps a share of the margin, not the whole of it

The margin sits at both ends of a claim and the two ends are not paid for by the same traffic. In
front it is this car's own cover, and it costs the car that keeps it. Behind it is what a
one-dimensional reading owes the width it threw away, and **it costs whoever comes up behind**: a stretch
begins there, so every metre of it is a metre of road the follower is queued out of and a metre of a join
that reads taken after the body is off it.

So the tail keeps a share of the figure rather than all of it — `DrivingFigures.TailMarginShare` at 0.6,
`SimConfig.CarTailMarginM` — and `ClaimFromM` is `nose − length − tail margin`. Everything downstream
follows from that one site: `PastOnTheCrossing` is still the near edge of the claim, the queue still
settles at whatever the leader holds behind itself, and the front of the ask is untouched.

**What it changes is the standing gap.** A queue at rest now stands at 1.2 m rather than 2 m, because the
gap at rest was never the follower's to choose — it is the ground the leader holds. `StandstillGapInCarLengths`
still sets the floor of the *figure* and the tail reads its share of that floor.

**What it cost was wrecks, and they were not the margin's.** Odesa over the measured minute, everything else
held:

| tail share | tail margin | wrecked | touches |
|---|---|---|---|
| 1.0 | 2.0 m | 0 | 50 |
| 0.8 | 1.6 m | 4 | 55 |
| 0.6 | 1.2 m | 4 | 56 |

The step is at the first metre under a body's width and does not deepen below it, which is the shape of a
threshold being crossed rather than of a margin being spent — and the wrecks, read one by one, were every one
of them a back-off reversing into a car that had stopped inside its straight. What the shorter tail did was
put a standing car 0.8 m nearer, which is the difference between a blind reverse that reaches and one that
does not. The hole is above: a template held no ground. With it closed, **0.6 runs 0 wrecked and 46
touches** — fewer than the 50 the full margin gave before any of this.

## 2026-08-23 — one body, one stretch: the margin is part of the claim

A car in a junction held a claim of its own behind its tail — the release margin — so that whoever crossed
there met the ground a swinging body might still be on. It worked, and it was a second stretch of one way
for one body: two occupants to every walk of the join, two bars across the road on the overlay, and a
trailing block behind a car that had visibly left it. It was also only true *in junctions*, though nothing
about the reason is: a claim throws away the width of the road wherever a body stands, not only where two
lines cross.

So the margin is where it always belonged, in the body's own claim, on every way the body is on:
`ClaimFromM` is now `nose − length − margin`, the crossing claim covers only the ground ahead of that, and
`PastOnTheCrossing` is the near edge of it rather than a second sum over the same pose. The
release figure and the follower's standstill gap were the same 2 m answering two questions, and they are one
figure now (`SimConfig.CarBodyMarginM`) — **the ground a body keeps around itself**. The queue arithmetic is
unchanged by construction: what the follower used to subtract for itself, the leader now holds.

**The measurement is kept as a floor rather than as a second figure.** The margin is `max(a body's width,
the standstill gap)`, so a fleet tuned to queue closer than the soak's floor gets the floor: nothing at the
tail was 2 wrecked and 923.6 mm of interpenetration, half a width 2 wrecked and 263 touches, a full width 0
wrecked. Reusing one figure for the other is what an earlier decision refused, and rightly — what makes it
safe here is that the *union* is taken rather than one being quietly read as the other, and the figure's own
doc-comment carries both questions.

Odesa's soak after the move: **0 wrecked, 53 touches, 15 stuck ticks**, and every map's deepest body is a
walker in a crowd rather than anything that drives (`--bench soak`, which now names the body a peak belongs
to as well as the one that stayed stuck).

**Two things had to move with it, and each was a defect the suite found.**

- **In front stopped meaning "its near edge is".** Every stretch now begins a margin behind its owner, so
  the near edges on a way run one margin out of step with the bodies on it, and `NextSpokenFor` was skipping
  a stopped car whose ground began behind the asker's own — granting a driver road through a body it could
  see. It is `StandsToM` that answers it now: **a stretch whose body has not reached this far is behind**,
  which is the same test `AheadBody` already used, and the cut is still taken at the ground's near edge.
- **A body that is not driving its movement lays no claim.** Off its line or under a hand, a car asks for no
  road, and it used to claim the runs of its join whole — over the very metres `LieInTheBox` was already
  holding it on as an obstruction. One body, two stretches, in two measures that could not agree. What
  refuses the traffic crossing such a body is now the ground it is lying on, which is every join of that
  junction it is under and the wider answer of the two.

## 2026-08-23 — a zebra is ground with a lane under it, and not a thing the traffic holds

The same decision as the junction one below, taken over the other network, and it was left half made: a car
crossing a zebra laid **a second copy of itself on the pavement** — its lane's band of every way
that crossing is made of — so that a walker's grant would be cut by it. One body then held one piece of
ground twice, under two names, in two claims whose answers could differ; the overlay drew the copy as a wash
over the whole paint, which is a picture of a crossing being *shut* rather than of a lane being somebody's.

It is a lookup now, and it points the way the walker is going. `CrossingBands` already carried the band each
lane covers of each crossing way, measured once when the town is laid, so nothing new had to be worked out:
a body asks the road's claims for the band in front of it, and where the answer is no, the same band's near
edge is where its walk is cut (`WhereTheWalkRunsOut`). The refusal is made once and spent twice —
`MayStepOnto` is the ask, `PersonFleet.RefusedWay` is what it answered, and the grant over the walk is that
same answer in the other network's metres. **It is the answer that is carried and never the question**: a
body past its patience is granted a band the claims would refuse, and a grant that re-asked instead of reading
would hold that body at the edge of ground it had just been given.

`TakeTheCrossingsAhead` and its bound on the pavement are gone with it, and three tests hold what
replaced them: `NoCarEverClaimsThePavementOnAZebra`, `ABodyRefusedALaneIsGrantedNoFurtherThanItsEdge` and
`ABodyRefusedALaneWalksIntoItOnceItIsGranted`.

**The kerb stopped being a special case with it.** A body was allowed to take the band in front only while
it stood on a pavement; half way over it was refused its own next lane and held by the car's copy instead.
Two arrangements for one question, and the answer differed by which side of a kerb line the asker stood on.
It is one question now — the same band, the same claims, wherever the body is standing when it asks — and
`PER-15` reads accordingly.

**A body holding the lane in front of it was tried once and rejected, and what makes it right now is the
reach.** Held from the moment the body entered the near lane, a walker stopped the traffic in the far one
for the whole width of the crossing; and with the car's band laid after the walker's claim, the claim won
every race and the band went back to holding nothing. Both are answered rather than argued with: a body
asks for the band its **own ask reaches**, which at a walking pace and a standstill gap is about a stride,
so the far lane is held for the last step before the foot goes down and not for the crossing; and the cars'
claims are laid before any walker's is checked against them, so the race now goes the
other way — a car committed over the paint cannot be claimed out from under.

## 2026-08-23 — a claim stops where a rule stops the car

`AskForTheGround` clamped the road a car asks for at the place it is held — a red, a bar, a crossing — and
then **added the margin it keeps in front on top of the clamp**. So every car in the town held two metres of
ground past every bar it stood at, and the stand-off a crossing stops a car at is one metre: a car waiting for a
zebra held a metre of the zebra. Measured on the Test crossroads, a car standing at its own red had its
claim 0.33 m inside the paint's near edge, and the band a body at the kerb asks about reaches
`PaintClaimM` ≈ 2.9 m either side of the paint's centre — so the crossing read as taken, on the pedestrian
phase, by a car that had stopped precisely to let those people cross. They got over on their patience, eight
seconds later, which made a signalled crossing behave like an unsignalled one.

The gap is part of what the car asks for and is clamped with the rest of it. Nothing about following
changes: a follower's grant already subtracts the gap from the near edge of the body in front, so the queue
spacing is where it was, and the ask only ever shrinks at a stop. What a stopped car holds is now the ground
it is standing on, which is what a stopped car is.

## 2026-08-23 — a car claims the ways it drives and looks up the ways it is driven over

A movement wrote its crossing points onto **both** joins: a stretch on every other way through the box its
line came near, and the matching runs on its own. Half of that is a body claiming ground it is never going
to be on. On screen it is the whole reason a junction under one approaching car was a fan of teal over
every way through it, most of them movements that car would never make; in the index it is a car holding up
to `MostCrossedByOne` stretches of other people's road, which is what the index was sized for.

It is a lookup now. The table was already symmetric and already carried both ends of every section
([`WayCrossings`](../WayCrossings.cs)), so nothing new had to be measured: a driver looks its own
way up, reads the metres named there **among the other way's own claims**, and its grant is cut at the near edge
of the first section anybody is standing on (`WhereTheGroundIsCrossed`). What one car holds on its own join
is exactly what the other finds when it looks — that is the same interval, filed under both movements.

**The crossing claim stays, and it is now the only thing a mover writes.** A driver's road ahead is a
braking distance and no more, which does not reach the middle of a box until it is nearly on top of it; two
cars asking from opposite arms would each look the other's join up, find those metres empty and both go.
The runs of a movement's own join are held from the tick it commits, and that is what the other car reads.

**The grant is the place this had to bite, not just the commit test.** `TheCrossingIsFree` was already
asking about the crossed joins, so the entry into a box was covered either way; what was not was the road a
car is *granted* on the approach — cut only by its own ways, it ran straight through the metres two lines
meet on and the ground of a junction was one car's and another's at once. Cut by the lookup as well, a
claim stated in one lane's metres means something about the whole town:
`NoGrantReachesGroundAnotherBodyHasOnACrossingWay` is that property, and
`ACarTakesNoGroundOnAWayItIsOnlyDrivenOver` is the half of it that keeps the ways clean.

**A section is a named piece of ground and not the road under the asker, and the walk had to say so.**
`NextSpokenFor` skips a stretch whose near edge is behind the window — right for a driver reading the
occupants of its own way in the order they are in, and wrong here: a car whose claim entered a join
*before* the metres two lines cross was invisible to the movement crossing there, and both went.
`LaneOccupancy.NextSpokenForOver` is the overlap walk, and the all-or-nothing `SpokenForByAnother` is now
its first answer rather than a second copy of the loop.

## 2026-08-23 — the margin a body keeps is not the clearance the sections are drawn at

They take the same value and answer different questions: one is how near two lines pass before they are
driven over each other (`SimConfig.JunctionCrossingClearanceM`), the other is what a one-dimensional reading
of a two-dimensional body owes whoever comes next (`SimConfig.CarBodyMarginM`). Read as one figure, they read
as one rule, and the reading hid that only the first of them had ever been measured. Kept apart, the second
can be moved without redrawing the sections — which is what the soak numbers on it were taken by.

## 2026-08-23 — a crossing claim is the run less the road, not the run

A car in a box held the crossing points on its own join twice: once as the road it was driving, and once
as the claim laid from the table. On a join a driver was well inside, the two came out as the same
interval to the centimetre — `0.00–14.25` committed and `0.00–14.25` claimed ahead, of one way, one car. Two
occupants to every walk of that way, two washes to the overlay, and a picture in which a car appears to
have claimed the same ground twice.

Nothing computed a wrong answer, because the two are read as one set and their union was right. What
it cost was that the claims stopped saying what they say they say, and `NobodyHoldsTwoStretchesOfOneWay`
could not catch it: that test exempted the claim ahead outright, on the reasoning that it is ground its
owner is not on yet. True of the reasoning, not true of the stretch.

The claim ahead is now the run **less** the committed one, and the exemption is gone — the test asks for
overlap rather than for a second appearance, so it may stand beside a body's own road and never over it. The
ask is laid before the crossing for it, since a claim clipped against the stretch the car held a tick ago
is clipped against the wrong metres.

**It is two pieces where the road ends inside a run, and the near one is load-bearing.** A committed claim
begins at the tail and a run is given up a clearance behind that, so the metres between them are ground the
body is still swinging over — the clearance that stopped River's soak wrecking cars. Dropped as
redundant on the first attempt, the give-back test caught it in one run: a car 0.30 m into a join had let
go of the first 0.30 m of it. So one run may split, one claim being one interval, and the slot budget
is `MostOwnRuns + 1`.

**And the far half of the give-back is not observable while a car is in the box**, which the counting in
that test had been hiding. The runs of a busy junction merge into one spanning the whole join, and a car
advances its chain — dropping the crossing whole — long before its tail is a clearance past the far end of
that. What moves is the near edge, so that is what is now counted and asserted from both sides.

## 2026-08-23 — a movement holds the crossing points on its own join, not the span between them

The near side of a crossing was held as one interval from a movement's first crossing point to its last,
and held whole for as long as the movement was. On a wide box that is the whole of a car's own way through
it: a straight crossing the two turns off the side arms shut every metre between them, including the middle
where nothing comes near, and it shut them from the tick the crossing was taken until the tick it was
dropped. A car whose own movement crossed only those middle metres was refused ground nobody was ever going
to be driven over, and a car half way through kept holding the metres behind its own tail.

The near side is now the same places the far side is, merged where two of them overlap so that one body
cannot appear twice over one metre, and given back on the same test: a run whose far end is behind the tail
is gone, and the near edge of the one the body is in walks up with it. What is under the body on its own
join is the car's committed claim, which was always there and carries the length and the swing the interval was
being asked to stand for.

## 2026-08-23 — a body is on a way across its band and along it

Whether a body stands on one of the town's ways was a lateral question only: project the body onto the
way's line, and compare the offset across the line against the band. A projection is clamped to the way's
own ends, so anything lined up with a way's end answered at that end however far up the road it stood —
and a body ten metres past a join measured no offset at all. What that claimed was cars lying on
joins they were nowhere near, most visibly for a body standing in a junction, which is asked of *every*
join at the node: one car in a box could shut movements on the far side of it that nothing was near.

The test is now taken along the line as well, against how far the body itself reaches that way. Inside a
way the nearest point is square to the line and the second comparison decides nothing; it bites only where
the clamp did, which is exactly the case that was wrong.

## 2026-08-23 — a junction is committed to at the rate the car actually brakes at

Two figures decide the life of a crossing — the claim distance a car takes one up at, and the point past
which it keeps one whatever else is holding it up — and both are a stopping distance. They were the only
stopping distances in the town read off the pedal's own cap, `Car.BrakingMps2`, while every stretch of road
a car holds is sized by `CarFollower.BrakingMps2`: the same cap against the tyres, at the braking margin,
on the ground under this car.

The cap is the larger figure, so both readings erred the one way that costs. A car past the point it could
stop at was still judged able to, so it gave the sections back for a bar it was going to cross anyway — and
in between that tick and the next, when they are claimed straight back as a fact, they read free to
whoever crosses them. And a car took its crossing later than the tick it committed on, which is the window
two cars commit in together. Neither figure noticed wet ground at all, where the gap between the two rates
is widest.

Both are `StoppingM` at `CarFollower.BrakingMps2` now, which is the same call the claim pass makes a
few lines earlier. `NothingStoppedAtARedHoldsAWayThroughTheJunctionBeyondIt` had the third copy of the
formula and read the cap too; it asks the town's own figure now, which is what caught this.

## 2026-08-23 — a crossing is given back where it is passed

Ground taken for a junction was taken whole and held until the car was out the far side. On screen that is
a car half way through a turn still washing the corner it came in by, and on the ground it is the traffic on
that corner held off a movement nothing was ever going to be driven into.

A section is a *place* two lines meet, and a body passes a place once. The table now carries both ends of
each of them — the metres of the crossed join and the metres of the crossing one — and a car gives a
section back once its own **tail** is a clearance beyond it. Per tick a car is crossing on: **7.93 → 7.31
stretches and 49.4 → 46.3 m** held on Odesa, **5.94 → 5.43 and 40.7 → 37.9 m** on River.

**The tail alone is a metre too eager, and it wrecks cars.** A section is drawn where two *lines* pass, at
exactly the width that makes the bodies on them touch; what has to be off it is a body, which on a turn is
off its own line by the road's tolerance and swings wider at the back besides. Released at the bare tail,
River's soak came back with two cars wrecked and 421 mm of interpenetration against a hundred. Released a
margin later (`SimConfig.CarBodyMarginM`, which the table above is sized apart from) it is back to the
100 mm the design without any release gives.

**The car's own committed claim is not released with them.** It looks like the same fact and is not: a
crossing point is a place, and the only question about it is whether this body is over it, while the claim is
the road the car is *driving*, carrying its length, its swing and wherever it comes to rest. Slid forward
with the tail, Odesa's touching count went from 51 to 232 and its reactive rungs from 10 to 27 with nothing
else changed. It is held whole and given back with the crossing.

The table's own measurement is symmetric now, which it had not needed to be: a pair used to be recorded
whenever the first line was found near the second, with the reverse measurement taken but not required, and
a section whose other end came back empty has nothing to say about when the car is past it. Both directions
are asked, and where only one finds anything the missing end is taken to be the whole join — which is that
section held to the far side, as it was before.

## 2026-08-23 — a junction is refused by ground, not by a verdict

`JunctionRegistry` counted the cars making each movement and `RoadGraph.Conflicting` said which movements
could not both be made. Nothing drew either of them, so what actually stopped a car at a junction was
invisible: the overlay showed the road's claims, and those were not the thing deciding.

Worse, the relation was almost complete. Measured over Odesa, **an average movement conflicted with 81 %
of the other movements at its junction**, and one car in a box refused **70 %** of them. Three quarters of
that was asserted rather than measured — a shared entry lane, a shared exit lane and the turn-around the
road still carried then each came back true on sight, and 416 of Odesa's 1472 movements were turn-arounds,
every one of which shut its whole junction on its own. So a car entering a junction stopped very nearly
everything else, which is neither what TER-5c says nor what a junction does. (The turn-around itself is
gone from the road since — TER-5f — and the table lost those rows with it.)

It is ground now, and only ground. The table says, per movement, **the section of every other join its own
line is driven over** ([`WayCrossings`](../WayCrossings.cs)), measured on both sides at the same
clearance the old relation used. A car is refused by whatever is standing on those metres and by nothing
else, and it can be looked at on screen. The registry, the relation and `CarFleet.HeldMovement` are gone;
the field left on the car names a claim rather than a permission.

Three things had to be true of it, and each cost a defect to find:

- **A car crossing must hold its own join.** Its own road ahead is a braking distance and no more, which
  does not reach the place two lines meet. Two cars asking from opposite arms each looked at the other's
  join, found those metres empty, and both went. Every section carries its own end of itself, and that is
  what the other car reads.
- **A car making the same movement is not an answer.** It is on the same line over the same ground,
  so read literally every queue at a junction refused its own second car. What holds one off the next is
  the road each was granted (S-2a) — a headway, not a crossing.
- **The two out of one lane, and the two merging into one, are not in it at all.** Both were in the old
  relation and both are the duplicate SIM-7 is about: a follower is cut by the car in front, and a merge
  is cut on the lane merged into. Measured, they are simply not driven over each other.

Each design's own refusal test, asked of every node-tick somebody is in a box: the mean share of a
junction's movements one car refuses falls **70 % → 64 %** on Odesa, **67 % → 49 %** on River and
**67 % → 50 %** on the fixture. The static density falls **81 % → 52 %**, and movements that shut a whole
junction on their own **416 → 0**. A whole node being shut stays where it was, under a tenth of a per cent
of those node-ticks either way.

## 2026-08-23 — a stretch runs out at the box's near edge

`WaysAlong` is the one walk that turns a run of a car's line into the town's own ways, and all three
questions a driver has go through it: the road it claims, the road it is granted, and the road it can
see. It stopped walking when the *next lane* began — and the next lane begins on the far side of the
junction, because the join is the ground between one lane's end and the next one's start.

So nothing was laid on a junction until the stretch reached clear across it. A car approaching a box
claimed none of the box, was granted its road as though the box were empty, and could see nothing
standing in it; its own block appeared half way through the turn, once the tail was inside and the far
edge was within a stopping distance. The guard is the near edge now — `ends[index]` — and the overlap that
follows was already written to clamp, so a stretch ending anywhere inside a box lays the part of it that
is.

## 2026-08-23 — a way through is kept until it is given back

The claim was not a claim. Every tick recomputed whether the car was *entitled* to the movement it was
already making, from the two figures that decide whether a fresh one may be taken — is anything queueing
between here and the boundary, and is the boundary inside a stopping distance — and told the driver the
answer as `BoxIsOurs`. Both figures move under a car that is merely slowing down: a stopping distance
shrinks with the square of the speed, so a driver easing off found its own junction refused it, `P-8`
failed to `P-6`, and the pair swapped back on the next tick for the length of the approach.

Worse was the half that never moved. Nothing wrote the field away. A car that claimed at speed and then
stopped at a bar went on holding the ground until it crossed — so the arm the phase had just given the
green to was refused by the arm sitting at the red, which is the phase's own decision undone and exactly
the duplicate SIM-7 is about.

It is one state now, held by the car and laid from the car. It is taken only by a driver nothing but the
box is holding up, and given back the moment something else is: a bar showing anything but green, or
traffic near enough that the two of them at rest would leave this one short of the boundary. `BoxIsOurs`
is then not a second opinion — it *is* whether the field is set.

**Past the point it could have stopped at, it is kept whatever anything says.** Ground given back there
is handed straight back on the next tick, because a car inside a box is standing on it; and in between
those two ticks the sections read free to whoever crosses them, which is a car waved into a junction
somebody is already in.

`LaneOccupancy.Withdraw` is what makes the giving back mean anything inside the tick that does it: the
claims are laid from the cars once, before any driver decides, so stretches left standing after their holder
let go would refuse everything that crosses them until the next rebuild.

## 2026-08-23 — a body in a box is on the joins, not on a lane

A junction's ground was defended by one thing, and that thing only knew about cars that are driving. A
wreck, a car under a hand, a body shoved into the middle of a crossroads — none of them is crossing on
anything, and `PlaceTheCrossing` gives back the ground of anything nobody is driving. So nothing had
anything to say about them, and the traffic crossing the box was granted the ground they were lying on.

The claims should have caught it and could not. A standing body was laid onto the lane it lies nearest, and
a body in a box is past that lane's own setback (TER-5d) — where the lane's line runs on under a movement
rather than under itself, and no driver's line is laid over it. The stretch went somewhere nothing walks.

It is laid on the joins now: both ends of the nearest lane are asked, and every join of the junction the
body is lying under gets its stretch, on the same band test a lane gets. That is what makes the whole of a
junction's ground answer the same way as the rest of the road (TER-4c), and it is one mechanism rather
than two — a body in a box refuses what crosses it in the same way a car crossing does.

## 2026-08-22 — a body is one stretch of a way, never two

A driver under way used to be laid twice on the same ground: a `Travelling` stretch for the body, and a
`Reserved` one from the same tail out to where the car could stop. The two shared a near edge exactly —
`noseM - LengthM` is the axle less the overhang — so the road always contained the body, and every
walk of a way counted one car as two occupants.

Nothing computed the wrong answer, because the masks kept them apart: `Travelling` was in the body
questions and out of the spoken-for ones, `Reserved` the other way round. What it cost was that a claim
could no longer be read as what it says it is. The overlay drew both, so a car sat under a double wash of
its own colour and a reader had to know the model to know that was one block; and every query that walked
a way had to be written knowing which of a car's two entries it would meet.

It is one stretch now, carrying two far edges: `ToM` is the ground taken and `StandsToM` is where the body
ends. A question about where somebody **is** reads `StandsToM` on every slot alike — `AheadBody`,
`BehindBody` — and a question about what ground is **spoken for** reads `ToM`. For everything that is only
a body the two are equal, so the distinction costs nothing to lay and nothing to ask.

`LaneUse.Travelling` went with it: the walking side had already settled this shape — a walker's ask begins
at its own back and it was never given a body stretch — so both networks now name the same thing
`Reserved` and are laid by the same call. `NobodyHoldsTwoStretchesOfOneWay` holds it, over both networks, with
the claim ahead the one stretch a body may hold beside its own — that one is ground its owner is not on yet.

## 2026-08-22 — neither network's claims are one roster's

A walker on a crossing used to claim the road and be in none of the road's questions. That was
deliberate and it was half right: a walker read as an obstruction is a walker `E-4` crosses the centreline
to drive round, and a walker read as a committed claim cuts the grant of a car three lanes away. So it was
put in a use nothing queried, and the crossing rule read it through a keyhole.

What that cost was invisible until the ray went: **nothing cut a driver's road at a body standing in it.**
The car stopped, but it stopped because a separate rule computed a stop point off the paint — and a person
standing in a lane with no paint under it cut nothing at all and was seen only by a cast.

Both halves are now the same mechanism. `OnFoot` is in every query a grant is taken against, and it carries
its own reading (`HeadwayKind.Walker`) so the one thing it must never be — something to get past — is a
property of the reading and not of which query happened to skip it. Its mirror is that **a car takes its
stretch of the zebra it drives over, on the pavement**, so a walker's grant is cut by traffic on
exactly the terms a driver's is cut by a body on the paint.

One thing fell out that had to be built rather than found: **an occupant is an index into one of two
rosters, and the stretch has to carry which.** Inferring it from the network was fine while each held one
roster; the first walker whose index matched a car's was read out of the wrong fleet. It also broke
exclusion — a car excusing its own stretch by number excused a walker's too.

The walker's give-way arithmetic went with all this. It asked how long something would take to reach the
paint, and the claim *is* that arithmetic, already done, from fresher numbers: a car far enough away
to stop for this body is committed to ground short of the crossing, and one that is not, is not.
`GiveWayReachWalkSeconds` went with it.

## 2026-08-22 — a crossing is ground, and it is taken by the band

Both sides of the paint took it whole, and both readings were wrong in the same way — **a zebra was treated
as a unit when it is a strip of carriageway a lane at a time**.

The car's half was **inert as well as coarse**. A car's stretch of a zebra was laid from the crossing way's
own start, and `NextSpokenFor` skips a stretch whose near edge is behind the asker's — a walk enters a
crossing lane at the mitre hand-over, measured never nearer than 1.03 m on Odesa, so every one of those
stretches sat behind every walker that could have been cut at one. Measured over a minute of Odesa:
**562 782 claims laid, 40 469 walker asks reaching a crossing way, and not one grant cut.** What
actually held a walker at a kerb was the give-way question on the road's side, and the pavement was
paying for a stretch per car per tick to hold nothing.

The walker's half was live and it over-held: a body on the paint took the band of **every** lane the
crossing was painted across. Of 162 410 such stretches in that minute, **127 976 were on lanes the body was
nowhere near**, and **6 003 of the 7 921 crossing stops a driver made were for somebody who was not in that
driver's lane at all** — 3 226 of them for somebody more than a whole lane clear of it.

So the projection that was missing got built ([`CrossingBands`](../../foot/CrossingBands.cs)): where each
lane's band falls on each way of each crossing, turned over from the projection that already put the paint
on the lanes. **What either side has of a zebra is a band and never the whole of it**, and the near edge of
a band is a place on the ground rather than the start of a way, which is what makes a grant cut at it at
all.

**The patience is spent on a named lane and given back when the body is standing in it** (PER-15). Reset
the tick the traffic gave way, it buys one tick of ground and starts the wait again — a body stuttering at
a lane's edge for as long as the street is busy. Latched until the far kerb instead, a body that waited
once holds the lane in front of it for the rest of the crossing, which is the whole-zebra picture this was
opened on, back again.

What it cost: a driver reads a walker in its lane less often, because a body in the next lane is no longer
claiming this lane — `CarLookingInATownTests` moved from the fixture to Odesa for that reason, the reading
being a matter of being the car in that lane rather than of the rule still working.

## 2026-08-22 — braking has its own margin, and it is nearly all of the grip

`GripMargin` is 0.7, and using it for braking as well as for cornering put the profile's planned stop at
13.1 m/s² against the 21 the tyres and the rolling drag actually delivered. Every claim on the road
is sized by that planned figure, so a car held half again as much street as its own stop was going to use.

Braking now has `BrakingMargin`, at 0.95. A corner is held for as long as it lasts and the margin there
covers a bump, a camber and the wheel still being turned; a stop is aimed at, straight, and over in a few
seconds, and what is left over is the rolling resistance — which the tyre model spends outside the
traction budget and hands back as a stop shorter than the plan. `TrackFiguresTests` is what holds the two
together: every slowing on the proving ground, into every one of its five shapes, comes out within a
quarter of the planned rate.

Corner speeds are untouched, which is the point of the figure being its own.

## 2026-08-22 — a claim is the ground a car is committed to, not the ground its plan would need

A driver used to ask for its whole stopping distance from the speed the profile was driving
towards, so that a car stopped in a queue asked for the road to pull away into rather than the nothing a
standstill needs. On a town street the profile's answer is always some corner or the end of the assembled
line, and the ask came out at a few tens of metres. On open road it is the car's own top speed — 75 m/s,
215 m of it — and one car held a quarter of a kilometre of empty straight it was doing a third of that
speed on.

The ask is now what the car cannot undo: one reaction interval of ground at the fastest that interval can
leave it doing, and a stop from there, with the profile's figure as the ceiling. It still leaves the room
to pull away, because a stopped car is committed to whatever its own acceleration reaches in that interval
and what it asks for therefore grows with the pedal rather than with the speed the pedal has produced.

**And a car nothing cut is now held by nobody**, where the grant used to come back as the length of the
car's own ask. That was harmless while the ask was the whole of what the profile planned for — the grant
inverted to exactly the speed the profile had already chosen, and bound nothing. Against an ask the car is
merely committed to it inverts to the speed one reaction interval reaches, which is under the profile's
figure and therefore binds: a car alone on an empty straight read as `queueing`, behind itself. The grant
is about other bodies, so it is written only where another body cut it.

## 2026-08-22 — a lane end has one setback, not one per turn

Each turn used to be set back into its two lanes by exactly as much as its own arc needed to reach the
junction's corner radius, which is the least ground taken and the tidiest straight. It also meant a lane
had no single end: a straight handed over to the box at the lane's own end, a right-angle turn out of the
same lane handed over up to 4.5 m earlier, and two turns out of one lane could differ from each other
because the cap is half of the *shorter* of each pair. Everything downstream that wants to name that
boundary — the assembler cutting the chain, the occupancy index, a debug layer drawing where the driving
changes — had to name a movement to do it.

The setback is now the lane end's, and it is the widest its own movements asked for (TER-5d). Straights
give up some metres of lane and drive a short straight across the box for them, which costs nothing
geometrically and a little junction ground. Widening runs in rounds rather than turn by turn, because
setting one turn back changes the arc of every other turn through the same lane end.

**Every movement in the reckoning reaches a radius**, because the one that never could — two opposing
lanes a lane's width apart, a semicircle whatever setback you give them — is no longer a movement at all
(TER-5f). Counting it would have pegged every lane in the town at the cap.
