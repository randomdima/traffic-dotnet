# CityGen — decision log

## 2026-09-01 — a prop's kind is where it stands, and the ground decides it rather than a die

**Every prop was a coin toss between three sets.** The stage drew a kind uniformly and the sets were
*tree*, *scatter* and *furniture* — a taxonomy of what the pictures are — so a litter bin stood in the
middle of a field as often as an oak did, and a great tree was as likely to be laid against a kerb as a
bollard. What the town then looked like from above was the same three-way noise everywhere, which is the
one thing a scatter must not be: ground that is not the town has to read differently from ground that is.

**The kinds are now placements** (GEN-6b) — wild, planted, furniture — and the stage reads the grounds
within a verge of each candidate to decide which one it is. It is the same one-pass sweep it was: the
candidate's own cell is tested first because most of a town is not grass, and a disc of the raster is read
only for one that has already passed that. The verge is measured out from the paved edge, so the figure a
reader tunes (`SimConfig.CityGen.PropVergeM`) is the depth of grass a street furnishes and not a distance
from a centreline that means something different on every road.

**A verge still carries wild looks half the time** (`PropWildOnAVergeShare`). A kerb planted only with the
fourteen things a town plants reads as a catalogue laid end to end, and a self-sown bush on a verge is the
commoner sight anyway — so the wild set is drawn on twice, in its own country and as half the planting of
the town's.

**And the size band went with the kind.** The great trees are authored at 2.6 m to 3 m and the draw stopped
at 2.2, so nine looks were unreachable except through the catalogue's own fallback — which does not resize
the prop, it only picks the nearest look, so what the town actually drew was an oak squeezed into a
metre-wide disc. A band is now the set's own: wild up to `PropWildDiameterMaxM` wherever it stands, and the
planted and furniture sets to the widest thing anybody drew for them. A wild look on a verge keeps the wild
band, because a tree at a kerb is a street tree and not a mistake.

**Thirty looks were deleted rather than filed**, and the test each one failed was the same: name what
this is, and say which of the three places it stands in. Six were set *into* paving and a prop only ever
stands on grass — a tree grate, a cellar hatch, a drain grate, a hydrant plate, a patch of cobble setts, a
mosaic medallion. A tram shelter wanted a network this town has not got. A tree stump and a group of sawn
rounds are cut trunks seen end on, which at this scale is a brown disc with rings on it. A hay bale and a
patch of dry scrub are two featureless discs, and one of them is farm plant besides. And **eleven were park
and garden amenities** — a boules court, a chess board, a sandpit, a play roundabout, a pergola, a
reflecting pool, a sundial, a fountain, a drinking fountain, a café table and its parasol. Nothing is wrong
with any of them except that **this town has no park to put them in**: the only urban ground a prop is laid
on is a verge and the grass beside a car park, so every one of them could only ever turn up as a lone
ornament in a strip of grass beside parked cars. The last eight went for the same reason one at a time — a
mushroom ring, a rock outcrop, a reed clump, a vegetable plot, a utility cabinet, a litter bin, a lavender
bed and a bottle bank — each a picture that reads as a coloured patch rather than as the thing it is named
for.

**What furniture means is now the thing a town stands beside its cars** — a bollard, a post box, a cone, a
bin, a skip, pallets, crates — and that is a rule a reader can apply to a picture without having to ask. It
leaves the set with nothing authored between 1.2 m and 1.9 m, so about one furniture draw in a hundred
lands where no look was drawn and takes the catalogue's nearest instead, stretched by less than the band
tolerance already allows. That is the case `PropCatalog` was written for; inventing a look to fill the gap
would be shipping a picture for the sake of an interval.

**What is left of the wild set below a metre is one moss patch**, so about an eighth of everything the
country grows is that one look. It is the smallest thing on the map and the least distinct, which is the
only reason a set this lopsided still reads — the moment a reader can pick it out, the answer is another
small look and not a wider band.

**The verge is walked and no longer swept** (GEN-6b). Deciding a prop's kind by probing the ground around a
lattice square was a sweep answering a question about a *line*: it found the grass near a street without
ever knowing which street, so it had no bearing to give anything and no idea how far out from the kerb it
was standing — the figure that controlled it was a radius round a point, and what a verge actually is is a
band along a road. The kerbs are walked now, both hands of every road, and a candidate stands two to five
metre or so out from the pavement's own edge on **the road's own bearing there**. The second pass keeps the
lattice, because the ground between the built parts of a town really is an area, and takes only what stands
clear of the walk by more than the verge reaches — so the strip between the two is empty on purpose and the
edge of the town is a line rather than a gradient.

**A car park's own edges are walked too** (GEN-6b), because the grass beyond one belonged to nobody. A lot
reaches back over the pavement it fronts, so its road is a lot's depth away and the kerb walk lands on
tarmac there — and the wild sweep keeps its stand-off off the lot's Parking cells, so it will not go there
either. What a reader saw was a car park with a bare apron of grass round it. All four sides are walked now,
and the two that face the street are refused by the ground under them rather than by the stage being told
which way out is: a lot carries its axis and its extent and nothing about where its road was.

**Its verge begins past the walk that wraps it** (GEN-4d), which is what the first attempt got wrong — laid
half a metre out like a kerb's, every candidate was refused, because a lot claims a ring of walkable grass
its own pavement's width all round before anything fills it. That ring is a car park's pavement. Measuring
the band from *past* it is not a special case bolted on: it is the same relation a street's verge stands
in — the walk, then the band — with the walk read off the thing rather than assumed to be zero. It put a
thousand pieces of furniture beside car parks that had none.

**And the collar GEN-6a asks for turned out to be the sweep's and not the verge's.** Every prop was keeping
the radius the pavement turns its corners on clear of itself, which held the whole verge a pavement's width
back from the street it is a verge of — at a metre out, the collar refuses every candidate there is. What
the collar is *for* is a candidate whose only knowledge of the pavement is the cells it read: the ground is
classified where the walk is drawn as one union with its re-entrant corners rounded off, so a blind sweep
can drop a prop in a corner that is drawn and not classified. **A kerb walk is not blind.** It stands a
known distance off the pavement of the road it is walking, and it begins past the stub every junction lays
its ground, its fillets, its paint and its bar across — which is where those corners are. So the verge owes
its girth on grass and nothing else, and the sweep still owes the collar.

**A picture says whether it has a front, and the placement says which way that front points.** The bearing
is the plan's — the road's heading where the prop was laid, zero for anything the wild pass dropped — and
whether it is used at all is the look's (`PropVariant.Turns`). Turning everything would make one tree read
as several; turning nothing left a rectangular planter lying across the kerb it was supposed to stand along.
**Nine of the sixty looks declare a front**, all of them rectangular, and no wild look may: the pass that
lays those has no bearing to give them, which is a claim a test makes rather than a convention anybody
remembers.

**The bearing is not written to a `.town` file.** The format is at version 3, it refuses anything else, and
the writer that would have produced a version 4 was deleted before this — so a prop read off one of the two
fixture files carries zero, exactly as a spawn read off one carries no patrol point. Both fixtures are on
their way to being laid in code, and neither is where a street's furniture is looked at.

**A prop's picture was bigger than the prop** (GEN-6d). A sheet was drawn `diameterM` *tall* and as wide as
its aspect made it, so anything wider than it was high reached past the disc the town reserved: the flower
planter is authored at 1.9 m and was being drawn 3.45 m across — nearly twice its own girth, and the
commonest thing on a verge. Nothing collided, because the discs were a metre and a half apart; the pictures
overlapped anyway, and a car was being held off half of one. **The longest side of a sheet is the prop's own
diameter now** and the other follows the aspect, so what is drawn is what a car is held off.

**And the art tool went with it.** `qq art` measured a prop by the height that used to be the metres and
measures it by its longest side now, which is the same one change. **It reports forty-eight prop sheets over
their grid as a result** — nearly all of them by a few per cent, because a sheet a little wider than it is
high was on grid under the old reading and is a little over it under the new. Two are worth re-cutting
rather than shrugging at: the flower planter at 1.8× and the flower bed at 1.4×. Nothing is resampled here;
`qq art --fix` is what does that, and it is not a thing to do to shipped art in passing.

**Two props keep a clearance of grass between them, not merely their own skins** (GEN-6c). Rim to rim is
still one long thing to look at, and a verge wants to be seen through. Half a metre costs the verge about an
eighth of what it carried and is the difference between a row of planters and a hedge made of them.

**Nothing overlaps any more, and a grid of its own is the index that makes that free** (GEN-6c). A prop
claimed no ground and tested against none, so two neighbouring candidates jittered toward the edge they
share stood inside each other — about four thousand of Odesa's hundred and nine, which is a bush growing out
of a tree and two static discs the solver has to hold apart. **The index cannot be either pass's own
pattern**, because one walks kerbs and the other sweeps a lattice and neither can see where the other put
anything: `PropScatter` is a grid whose square is the widest prop's own width, so what a candidate is asked
about is the nine squares round it and nothing outside them could reach. **The prop already laid is the one
that stays** — a candidate that would touch one is simply not a prop, which is the same answer this
generator gives everywhere else rather than nudging anything aside.

**And the wild scatter went from six metres of lattice to seven and a half**, in two cuts of a fifth each.
Density goes as the square of the lattice, so a fifth off it is the figure times the root of five quarters —
6 m to 6.6 m and then to 7.4 m — and what comes out follows, because the sweep's acceptance barely moves
with its own spacing. Odesa laid 108,939 props before any of this and lays 78,705 now.

**What the verge carries is not the planted and furnished count**, because the share of it drawn from the
wild set is counted as wild — a kind says which set a look comes from and not which pass put it there. On
Odesa the verges carry about ten and a half thousand props of which 8,602 are planted or furnished, and the
sweep lays the other sixty thousand.

**The kerb is walked at a pitch shorter than the props are wide**, so what spaces a verge is the props' own
girth against each other and not the step: a stretch of kerb carries what fits along it, and one crowded by
buildings or bays carries what is left. Halving the pitch is therefore not a promise of twice as many, and
it happens to have been one — the verge went from 4,633 planted and furnished to 8,602.

**The art folders still say what the pictures are** — `tree/`, `scatter/`, `furniture/` — and the `kind`
field says where the prop stands. They disagree on purpose: a planter and a hedge are furniture to draw and
planting to place, and renaming the folders would only move the disagreement rather than settle it.

## 2026-09-01 — an arm's paint is set back from that arm's own mouth, and the setback is a car length

**A crossing stood a fixed distance from the node, and no junction's ground ends there.** The reach the
paint was measured off was half a carriageway plus a full corner radius — the right answer for a junction
whose arms stand square and the wrong one for every other, because two kerbs meeting at an angle cross well
outside the mouth and the fillet between them lets go of the kerb further out the sharper the corner. The
setback was carrying the difference: six metres, most of which was slack for a skew nobody had measured, so
a square junction's zebra sat two road widths off its own kerb and the bar behind it further still.

**The reach is now solved corner by corner** and an arm stands off the further of its two, which is what
`Furniture` already had the geometry for — the fillet tangent it draws *is* the end of the junction's
ground. What is left of the setback is what the name says: **a stride**, a metre of carriageway so the
zebra's end bars stand on straight kerb rather than on the corner's own arc, and a metre behind the
crossing for the bar. Odesa's paint came in about eight metres an arm and the skew arms kept the room they
need, because their own mouths are further out and the paint went with them.

**The two figures the fillet was borrowing are its own now.** Bounding a corner by the crossing's setback
was what kept a full-sized fillet from paving the paint at a sharp junction, and with the paint measured
off the fillet that circularity is gone: how far back a kerb transition may run is authored on its own
(`JunctionFilletReachInCarWidths`), and the sixty degrees GEN-13 refuses an arm for is a figure on
`SimConfig` rather than a constant in the generator. The straight stub is the worst of those reaches plus
what stands on it, so a road still carries every piece of its junction's paint across a straight arm.

## 2026-09-01 — a car park is three to six bays, because a run of frontage is an apron

**Odesa was laying car parks a block long.** A lot was one slot of frontage, four bays wide, and
neighbouring slots that had both drawn one were merged into a single rectangle covering the run — so where
three slots in a row drew one, what was laid was sixteen bays of unbroken tarmac down one side of a street.
Nothing ever filled it: the town's whole roster is 520 cars over 319 lots.

**The merge was the right answer to the wrong question.** GEN-16 exists because two things a stride apart
are a pair nothing downstream can read, and for a junction the resolution has to be a merge — refusing one
deletes every road at it. Nothing hangs off a car park, and the ground was never asking for one bigger
thing: an apron fronts several buildings at once and puts a rank of parked cars where a street's frontage
should be. GEN-4b now bounds a lot at both ends — `BaysPerLotFewest` to `BaysPerLotMost`, three to six, each
lot drawing its own count — and the second of a pair inside a locality is simply not laid.

**The bounds are what a lot *is* and not a tuning.** The upper one is what makes a car park a car park
rather than a surface; the lower one keeps a lot from being a two-car lay-by that cost a lot's whole
clearance (GEN-4d). A slot is offered room for the widest of them, so a slot that has room has it whatever
its own draw then asks for.

**The clearance between two lots is measured between the rectangles and not along the road.** The old guard
compared arc lengths along the kerb, which on a bend runs longer than the chord the two lots actually stand
on — a pair 29 m apart passed a 30 m locality. It is now the same arithmetic GEN-4d states and the gate
asks: the gap along the first lot's own bearing, rectangle to rectangle.

**What it cost:** Odesa's 319 lots hold 1377 bays where they held about 3500, 4.3 to a lot, against 1200
buildings and 520 cars — so the density GEN-4b actually promises, roughly a bay a building, is what the town
now has rather than three times it.

## 2026-09-01 — the crossings were unpicked, because planarity was an argument and not a check

**Odesa laid an orbital and a street across each other with no junction where they met.** The two
carriageways simply shared 20 m of tarmac: no box, no kerb fillets, no crossing, no stop bar, and the paint
each of them carried belonged to a junction twenty metres away. Over sixty fixture seeds, seven towns held
at least one.

**Nothing had ever tested two roads against each other.** The generator's whole case for planarity was the
arrangement — a district's region is convex so its streets stay inside it, an arterial carries a node
wherever a street meets it, and the one region that is not convex is the ground outside the orbital, which
is why `Arterials.CrossesTheRing` exists. The argument was sound and the coverage was not: that test had a
single caller, `Lattice.Reach`, and `Lattice.Hang` — the stub that reaches out of a lattice to the arterial
beside it, and the one road here deliberately laid across a district's own edge — never asked it. A stub
from an outer sector onto a spoke node standing inside the ring is what Odesa laid, and the test would have
refused it.

**Adding the missing call was not the fix.** A rule that holds only where somebody remembered to invoke it
is the same defect again a year later, and the arrangement has other seams than that one: the merge moves
nodes up to a locality after every road has been laid, and a road is drawn as a wandering, arcing shape
rather than as the chord the layout joined it on. GEN-17 states the property over the town instead, and
`TownLayout.UnpickTheCrossings` is one pass that holds it.

**It is a pass over the settled layout and not a test inside `Join`.** The stages lay in the order the
ground is walked: the lattice hangs its streets before `Arterials.Close` joins the arterial they cross, so a
test made as each road was offered would have kept whichever was laid first and deleted the orbital. Taken
afterwards in precedence order — bridges, then arterials, then streets — the arterial stands and the street
gives way, which is the same order the merge already re-offers in and for the same reason.

**Measured against what will be drawn, not against what was joined.** A street strays off its chord by its
own district's wander and an arterial's arc bulges off its chord by its sagitta — 13 m on Odesa's orbital —
so `RoadStage.StraysM` gives the pass a per-road allowance and the clearance is one road's whole width plus
both. It is the bound the wander is clamped to rather than the wander itself, because the draw needs the
road stage's own stream, which cannot run until the roads that survive are known: a street held straight by
its corner floor keeps ground it never uses, which is the cheaper of the two mistakes.

**What it cost:** Odesa loses 6 roads of 316 and River 3 of 227, and both are still one connected piece. It
bought back five failing conformance cases in the town tier — a walk crossing a carriageway off the paint, a
crossing not striped across its own span, a stretch walked over a car park — every one of them a shape laid
over ground another road had already taken.

## 2026-08-31 — two of a kind near enough to be one are merged, and merging beat refusing

**The town was laying pairs of things nothing downstream could read.** Two junctions a stride apart, with
their fillets, crossings and bars over each other and a road between them no car is ever on; two car parks
along one kerb with a stride of pavement pinched between them. Neither is anybody's mistake: the arterials,
the lattice and the frontage are each laid at their own spacing and none of them knows what the others left
there, so the near-coincidence is the ordinary case and not the odd one.

**Merging rather than refusing, where anything hangs off the thing refused.** Refusing the second of a pair
is what the layout already did to a road too short to be one — and it costs the whole piece that hung off it
(GEN-8), which is how a town loses a block to an arithmetic coincidence. A junction is therefore merged: what
the ground was asking for is one junction all the roads meet at.

**The nodes are merged once the layout is joined, not welded as they are placed.** Welding at placement was
tried first and it took half the town: an arterial that records a node on its own line and is handed back
one twenty-five metres off it lays its next piece to somewhere else, the chain breaks, and what is left of
it is deleted with everything hanging off it when the largest piece is kept. Merging afterwards leaves the
arithmetic that puts nodes on a spoke, on the orbital and on a lattice to finish first, and holds the
arrangement it produced to the rule.

**What is merged is offered again road by road, and in the order the town cares about them.** Moving a node
changes what its arms are worth, so each of them has to pass what it passed the first time (GEN-13) — and
offered back in the order they were laid, a street can sever the arterial it was hung off and take half the
town with it. Bridges first, then arterials, then streets: on Odesa that is 51 km of road against 42 km
before, because the arterials that streets were quietly severing now survive.

## 2026-08-31 — a lane is the width the town is laid in, and the straight stub is what stands on it

**A road was three car widths across because somebody wrote three.** The carriageway is now two lanes and a
lane is the standard one — 1.8 car widths, 3.6 m at the shipped car — so a road is 7.2 m rather than 6 m and
every figure quoted against a lane means the same thing on every map (GEN-15). The pavement is the same
question answered for the other traffic: two walking lanes, each two bodies wide, which is what lets two
walkers pass without one of them stepping off. Both are ratios and not metres, because every size in this
town is the car's width and a figure authored in metres beside them would be the one that stopped scaling.

**Widening the road is what found the real defect.** GEN-12 says a road's ends are straight *for the length
everything a junction lays across an arm stands on*, and the stub was authored at four car lengths — 16 m,
against the 20.4 m the junction's own ground, its fillet, the crossing at its setback and the bar behind it
actually take. The crossing sat exactly on the end of the straight at 6 m roads and hung off it onto the
bend at 7.2 m, which is where the pavement's mitres, its mouths at the paint and the walking network's own
corners came apart. **The stub is now derived from what is laid on it** and the wider road lengthens it
rather than pushing its own paint onto a curve; the numbers that were tuned around the old stub are gone.
## 2026-08-31 — the bank is a curve now, and the water is set in a shore

**A shoreline was twenty-four points over four kilometres.** The bank is a sum of three sines and it always
has been — what was rugged was the sampling, one point every hundred and seventy metres, drawn as chords a
reader can measure by eye. **The count is now derived from the wave rather than authored**: a chord stands
off a curve by about its own length squared over eight times the curve's bend, and the bend of a sum of
sines is the sum of what each of them bends, so the step falls out of a tolerance. Half a cell is the
tolerance, because that is the finest the ground under the bank is classified — a bank drawn finer than the
cells that carry it is precision nobody can see. Odesa's coast came out at eighty-seven points a bank; a
straighter brief would take fewer, and nothing has to be kept true by eye.

**And the water is now set in a shore** (GEN-2c) rather than meeting the grass. The strip is the same wave
laid a shore's width wider, so it is one width the whole way round and no band has to be fitted to a curve
afterwards; the ring the map carries is the *outer* edge of it, and what makes it a strip is the water laid
over the middle. It is drawn the way it was classified — the shore polygon first, the water over it — so the
picture and the ground cannot disagree.

**Its two edges are lines, and they are the same trick the pavement's rim is.** The map carries **four**
rings rather than two — the shore, the shore less a line, the water plus a line, the water — and they are
drawn largest first, so each fill leaves a line's width of the one under it showing. Nothing is offset by
the renderer and nothing is classified twice: all four are the one wave at four offsets, so a line runs true
along a bank however it meanders. **Each line is the colour of what it borders** — green against the grass,
blue against the water, and darker than it, so the edge reads as the shore's own shadow on that ground
rather than as a highlight drawn over it. The ground under both is shore, because **a line is a picture of
an edge** and not a kind of cell anybody stands on.

**The shore is not grass, and that is the whole of why nothing stands on it.** Props take the ground that is
left over and slots take grass, so a beach that is its own kind of ground is a beach with nothing scattered
on it and nothing built on it, with no rule anywhere naming a shore. The test the town owes is the plainer
one — **no cell of water touches a cell of grass** — which is what a reader actually sees.

**It wears the pavement's texture for now**, and that is a placeholder rather than a claim: sand has no art
yet, and a `Ground` of its own is a kind in the catalogue, a surface in the renderer and a version of the
map format. When there is a picture of a beach, the kind arrives with it and this stops being pavement.

## 2026-08-31 — the map ends at its edge, and the shore is cut there rather than never drawn

**Odesa's sea was painted to its own horizon.** The outline a coast is laid from pushes its far bank a map
diagonal and two hundred metres past the edge, on purpose: a bank that closed inside the extent would be a
lake, and a sea has no far shore on the map at all. The raster only ever took the cells that existed, so
nothing downstream was wrong — but the outline is also what the ground mesh *draws* the water from, and what
that drew was open sea over the void beyond the world.

**The shape is still drawn through and is now cut where it becomes a plan** (GEN-2b). The alternative was to
lay a shore that ends on the edge in the first place, which puts the map's own rectangle inside the meander
arithmetic and gives a coast four special cases at the corners; clipping a ring against four half-planes has
none, and it is the same cut for a town that is generated and a fixture that arrives as a file — `Test`'s
river ran thirty metres off the top of its map and nobody had noticed.

**And a prop keeps its own girth on the map.** The scatter lattice is jittered inside its own cell, so a
candidate lands a hand's breadth from the edge of the world perhaps twice a town, and what stood there was
half a tree over ground that does not exist. It is refused with its radius rather than with its centre.

**The bar is asked of every map now** (`MapConformanceTests.NothingItCarriesStandsOffIt`), because "nothing
stands off the map" is a shallow question of the kind a whole city can be asked: the outlines, the junctions,
every metre of every road, the buildings with their footprints, the bays, the props with their radii and
everybody standing at the first tick.

## 2026-08-31 — a street that goes nowhere is deleted, and the fixture brief had to become a town

**Every generated city was ending a dozen streets in a field.** Odesa carried 21 junctions of one arm and
River 20 — the outer end of every spoke, where the ray runs to the margin and stops, and every lattice row
whose next point fell in the water, in an arterial's corridor or off the edge of the world. They were dead
ends in the sense TER-5a means (a junction of one arm) without being dead ends in the sense it *promises*:
the road stage lays every junction as the disc its own arms need, so what a car found down there was three
metres of tarmac and no room to turn. Nothing in the generator was looking for them.

**They are deleted, with whatever is left hanging off them** (GEN-5a). Deleting one road can leave the
junction behind it standing on one arm, so the sweep runs to a fixed point and what survives is the part of
the layout every road of which runs between two places worth being at. That is GEN-8's own answer — a piece
joined to nothing is dropped rather than linked up — applied one node at a time instead of one component at
a time, and it is the same argument: an arm grown on to close the loop would have to cross whatever cut the
street short in the first place. **Giving them turning heads was the alternative and it is worse**: a
cul-de-sac is a thing a town plans, and one that appears wherever a lattice ran out of ground is noise with
a kerb round it.

**It costs a city about a tenth of its road and buys back the junctions nobody could use** — Odesa 195
junctions to 174, River 184 to 164, and both read as more of a town for it.

**What it exposed is that the property suite's fixture brief was not a town.** At 1500 × 1200 m with four
districts the wheel is most of what fits: the orbital's own radius is a third of the short side, the
lattice inside a sector is a handful of points, and two of the four seeds laid a layout with **no cycle in
it at all** — one of them a pure tree, which the sweep quite correctly deleted in its entirety. The brief is
now 2000 × 1500 with six districts and a river narrow enough for the deck bound to span, which lays 56 to
100 junctions and a dozen or more independent loops on every seed, and the whole class still runs in a
second. A fixture that answers questions about a chain of streets answers them about no arrangement this
project ships.

**And the census counts them**, beside how many junctions are lit. It is the line that says a generated
town has none and that a laid map's are the ones it laid on purpose — the exam's spurs, which come with the
head that turns a car round, and the skidpad's pads, which nothing is ever routed down.

## 2026-08-31 — a bridge is a road, and the wheel is turned so there is one to build

**The generator had no idea what water was.** It skipped a node that fell in the river and joined whatever
dry nodes were left either side, so a "bridge" was however far apart the two-hundred-metre spacing had
happened to leave them — a quarter-kilometre of deck at whatever angle the spoke met the bank at, and the
same trick over the *sea*, where the far end is off the map. The hub sat at the middle of the map, which for
a river town is in the river, so the town centre was a five-armed junction in the water.

**A bridge is now a class of road** (GEN-14a) rather than a stretch of one. That is the change everything
else falls out of: the layout asks the water of every road it is offered, and water takes a `Bridge` and
nothing else — so a street cannot cross, the orbital's own arc is given up over its span because a deck is a
straight thing, and a span longer than the deck a town builds is a road that is simply not laid. **The deck
then runs the whole road**, which is what TER-3b asked for all along and what the old wet-part-plus-a-margin
deck only approximated.

**The nodes are what make a crossing short, not a search afterwards** (GEN-14b). Where a spoke or the
orbital meets the water it carries a node on each bank, the abutment's own width back from it, and the
stretch between the two is closed to everything else — the lattice may not hang a street on it and no
spacing may insert into it, because a node between two bridgeheads is a junction on the deck. What is left
between them is the water that path actually crosses.

**And the wheel is turned so that a spoke runs down the river's own normal.** "Right across" cannot be
arranged by moving a node: a bridgehead pushed off the spoke's ray leaves the sector the ray bounds, which
is the one thing that makes the districts' streets planar without any intersection arithmetic. Turning the
whole wheel buys the same thing for free — the rotation was a draw, and for a river town it is the normal
instead. **The orbital's crossings are left to the length bound**: an oblique one is a long one, so the deck
cap refuses it without any angle needing to be authored.

**The sea is not bridged at all** (GEN-14b). A coast has one shore inside the town and whatever is past it
is off the map, so every deck over it went nowhere; Odesa now stops at the beach and the pieces the sea cut
off are deleted with their own component, as GEN-8 already says.

**The water question is asked of the carriageway and not of the centreline.** A road whose middle runs a
metre inside the bank has its far kerb and its pavement over the water, and the ground under them is painted
road exactly as the middle is — which is a lane on the river that the terrain tier catches and nothing here
was refusing. Odesa lost about a sixth of its road length to that and reads better for it.

**What it cost the briefs:** `River` is a narrower river. A crossing has to fit inside the deck bound with
its two abutments, and a fifteen-metre span cap over a two-hundred-metre river is a town with no bridges at
all — so the share went to 0.045, which is about a hundred metres of water and a hundred and thirty of deck.
The bound is authored (`SimConfig.CityGen.BridgeDeckLongestM`) because how much bridge a small town can
afford is a fact about the town and not about any car that drives over it.

## 2026-08-31 — a city is a seed and a brief, and every stage of laying one runs once

**The gap this closes is the one the 2026-08-17 entry named.** There was no generator: cities arrived as
baked `.town` files, so GEN-2 through GEN-8 bound whatever exported them, nothing here checked them, and a
city could not be varied, replayed at another seed or repaired when a rule moved. `Odesa` and `River` are
now briefs — a seed, an extent, the water, the districts, the counts — and the town is laid when the map is
opened, in about a second for three thousand metres square.

**Only hints are persisted, and never geometry.** A brief carries what a person would say about a place;
everything a reader would call the map is derived from it. The moment a brief carries a node or an outline
there are two answers to where the town is, and the one on disk is the one that goes stale — which is the
same argument that keeps the pavement's inner corners out of the format.

**Nothing retries, and that is a property of the arrangement rather than a rule anybody obeys.** Four
things make the one-pass rule (GEN-10) affordable, and each of them replaces a search:

- **The districts are convex.** A town is a wheel — a hub, its spokes and one orbital — and a district is a
  sector of it. A street is laid between two lattice points of one district, and a segment between two
  points of a convex region stays inside it, so no district's streets can cross another's. Planarity is
  arranged rather than checked.
- **The arterials carry a node wherever a street meets one.** A spoke leaves the hub on its own bearing and
  the orbital carries a node at every spoke's bearing, so the crossings that would otherwise have to be
  found and split are junctions that were placed there.
- **A slot claims its own padding before anything fills it**, so GEN-3 holds of a building that was placed
  rather than of the attempt that happened to pass.
- **What is left over is deleted rather than joined up.** A piece of town the water or a refused junction
  cut off is dropped with its own component, because a link drawn to reach it would cross whatever cut it
  off. A town is what stayed connected, and the census reports what did not fit.

**Two bounds came out of measuring the traced cities rather than out of taste** (`qq town` reads the old
format and printed them). Their streets are single arcs at the ninetieth percentile and their median
sinuosity is 1.000, so **straight is what a street is**: the wander a generated street is allowed is
bounded by the district's block spacing, so no street can reach the one a block over, and by the radius its
class's design speed affords on tarmac — derived from a speed and a grip, never authored as a radius
(GEN-12). Bending is concentrated where the traced cities put it: the orbital is an arc by construction and
the spokes are straight, because everything that asks whether a point stands clear of an arterial asks it
of a ray through the hub.

**A junction's arms have to stand square enough to be a junction** (GEN-13), and that was learned by
laying towns without the rule. Two carriageways meeting at a shallow angle overlap for tens of metres: the
kerb fillet on one arm paves the crossing on the other, the crossing's own ends land on the neighbouring
carriageway, and the pavement that turns the corner stands in the road. Sixty degrees is where all three
stop happening.

**The painter now lays a shape to its own edge.** A stride is shorter than a cell, so a shape whose edge
fell between two strides left the cell under that edge unpainted — a sliver of pavement inside a kerb
fillet, which every walking check found and no map had ever shown before, because the traced cities' cells
were painted by whatever exported them.

**A building is sized by the roof it will wear.** The footprints come from the catalogue through the
caller (`BuildingCatalog.OrdinaryFootprintsM`), because the plan may not read a catalogue that sits above
it: the data crosses the seam and the type does not. Sized by a draw instead, a building wore the nearest
roof there was, which is a picture that does not fit the box it stands on.

**What the change deleted:** `TownWriter` and its round trip, `--lay-maps`, `--place-services` and
`ServicePlacement`'s sweep at the workshop. The laid maps are produced in code when they are opened, so
writing them to a file bought nothing; the services are placed by the stage that already knows which
buildings have parking outside them, which is the sweep the old entry priced and declined because it could
not be paid for at load.

**What it has not finished:** the fixture map and the crossings map still arrive as files and are still
read by `TownReader`; the browser fetches towns rather than briefs; and the town tier has findings left at
generated junctions — the kerb fillet against the pavement that turns it, the strokes at a car park's
mouth, and a walk that crosses a carriageway off the paint.

## 2026-08-31 — the ring is a rounded square, because what stands inside it is a rectangle

**A circle is the wrong shape to put a panel in.** The start menu opens over this map and a panel is a
rectangle, so the ground the ring encloses was being spent on four corners nothing could reach into: the
widest rectangle inside a disc is 0.7 of its width, and the panel had to be measured against its own
*corners* rather than its width to keep it off the road. Rounding a square instead leaves the middle of the
field as wide as the field is, and the panel is laid against a straight side like any other rectangle.

**The corners are what keeps it a road.** A square would be four right angles no car takes at speed and a
picture nobody would call traffic; the corner radius is the one figure here that trades the field against
the pace, since on a loop laid to a single view it is the radius and not the driver that sets the speed.
Two fifths of the half-side is where the field is rectangular enough to lay a panel against and the corner
is still a corner.

**The cuts moved to the middle of the straights.** Four roads is still the fewest that leaves no pair of
nodes joined twice, but a node on a bend is a junction disc taking a bite out of the one piece of the loop
whose shape matters — so each road is half a straight, a corner, and half the next. The four stay the same
length, which is why a quarter of the lap is still a road and the convoy still stands in the middle of one.

**And the escort's pace moved with it.** It was a share of what the escorted car's grip affords on the
ring's one radius; a loop with straights on it has no such figure, so it is now read against the *tightest*
corner. That is where the charge has the least margin over its escort and so where the convoy comes apart if
it is going to — read against a straight, the same margin would be a leading car the charge could not catch
on the bends. The share went up with the reference and the convoy runs at the speed it did before.

## 2026-08-31 — the ring carries an escort and one car, and the escort is held to its charge

**Two convoys of three read as a staging.** The ring was laid symmetric — the same three cars each way
round — which is a picture of an arrangement rather than of traffic. What replaced the second convoy is one
sports car coming the other way: the closing speed changes lap to lap, and a quick car passing a slow
escort is the plainest thing a circle of road can show.

**An escort in police paint outruns an armoured car, and no rule was stopping it.** Police tyres are worth
nearly twice the grip, so on a constant-radius ring the leading car cornered a third faster and left its
charge inside a lap — three cars in a row rather than a convoy. The fix is a pace ceiling on the car
(`CarFleet.PaceMps`), set from what the *escorted* build's own grip affords on this radius. **The
alternative was building the escort on the armoured car's figures and painting it white**, which is a
police car that corners like an APC — the paint and the physics coming apart is exactly what the rule that
a map dresses looks rather than builds them exists to prevent.

**The ceiling alone did not close the convoy up.** At three quarters of the pace and at half of it the three
ran at the same spacing: the gap is the road a follower is granted to stop in plus the interval it leaves on
top, and the pace moves only part of that. What closes it is a second per-car figure — a share of the
**following interval** (`CarFleet.FollowingShare`), which is exactly the part of the gap that is a habit
rather than a stopping distance. **Cutting `Driving.FollowingHeadwayS` instead was not on the table**: it is
what every car in every town keeps, and a convoy on the idle ring is not a reason to move it.

**Measured, the two together halve the spacing** — a quarter of the interval and a little over half the
pace, against the same frame at the same tick. Neither on its own does: the interval alone stops about a
third short of it, and taking it to zero — a driver leaving no margin at all — stops short of it too.

## 2026-08-30 — the menu is drawn over the ring, and GEN-1b now says which map that is

**A start menu over an empty screen was the one frame of this game nobody had made anything of.** GEN-1b
is about not building a *city* nobody asked for — a two-second lay of a town the reader may not want —
and that argument says nothing about a map that costs a fraction of one and was laid to be looked at. So
the ring is what the menu now stands over, on both heads and in both configurations, and the rule says so
rather than leaving it to whichever entry point happened to pass a name.

**Standing a town up no longer means dropping the reader into it.** Opening a map shuts the menu, because
a map on the menu is a map somebody picked; the ring is opened with the menu deliberately left up
(`Interface.TownChanged(behindTheMenu)`). The alternative — reopening the menu after the open — is the
same state reached by two moves, and the frame in between is the reader watching the panel they were
already looking at flicker.

## 2026-08-30 — a map laid to be looked at, and the look rule it had to loosen

**The idle ring is the first laid map that measures nothing.** Every other one answers a question and is
shaped by it; this one is the picture the game idles on, so what shaped it is that it never stops being
worth watching and never needs anybody's attention. A circle gives that for nothing: a closed loop nobody
can reach the end of, and one carriageway carrying traffic both ways.

**It stands at the left of the frame.** The menu hangs from the gear in the top right, so a ring in the
middle of the screen is a ring with a panel over it; the camera is moved half the difference between the
view's long and short sides to the right of what it was looking at (`Opening.AsideTheMenuM`), which stands
the circuit against the left edge with the panel clear of it. **Only for a town opened behind the menu** —
a map somebody picked is framed the way every other map is.

**Its size is the window's and not the driving's.** The first ring was 120 m across the radius, so that
what set a car's speed was the driver rather than the corner — and the picture it made was an empty
stretch of road for the twenty seconds between one car passing the camera and the next, because a 70 m
view of a 750 m lap holds a tenth of it. So the radius is now whatever the view a run opens on will hold
(`ViewFigures.CameraDefaultViewM`, less the road and a little grass), the whole circuit is on screen, and
every car is in the frame the whole time. **What that costs is that the corner sets the speed** — 13 m/s,
held for ever, with what each look is worth barely showing against the others — and for a map whose whole
job is to be looked at, a picture with six cars in it beats a table with a bigger number in it.

**Nothing on it is staged.** No wheel is held over and no car is ordered anywhere: with no building and no
bay on the map, `TownWorld.DriveTheEmptyMap` puts each car on the lane under it and the ordinary catalogue
drives it. That is why the map is worth shipping at all — a loop of scripted cars would be an animation, and
this one is the town's own driving with nothing else in the way.

**Four roads, because a road ends at a junction and a ring has no end.** Two would have joined the same pair
of nodes twice, which nothing else in this engine's geometry ever does; four quarters is the fewest that
avoids it, and each node is a lane's half-width of ground nothing drives — the same seams the proving
ground's ten are.

**Its cars are two convoys and not six of a kind.** A police car, the armoured car it escorts and a second
police car, each way round: the escort is held to the pace of what it escorts, so three cars read as one
thing rather than as three that happen to be in a row, and the two convoys meeting head to head twice a lap
is the whole of the movement on the map.

**The map dresses its own cars, and that cost a rule.** The fleet's wrap cannot reach a service look, which
is right — an ambulance handed to the seventeenth ordinary car would be a school run in one. But the
catalogue also said that a police look *is* a car with a station, and the service tier found its vehicles by
their paint. That is an over-fit: SRV-3 defines a service vehicle as paint **and** a building, and `EVA-7`
already names an ordinary car in service paint as a state the town has. So the rule is now the narrow one —
**a look is what a map asks for and a duty is what a station gives** — and `ServiceVehicleTests` finds a
patrol by its station and a recovery by its depot, then asserts the paint, rather than the other way round.
What is unchanged is the thing that mattered: no town's own traffic can be handed a service look by
accident.

## 2026-08-28 — the exam orders its walkers, because three cards were passing on an empty crossing

**The three `StopsForThePaint` cards had never once been asked.** Each claims the car is never on the paint
while somebody on foot is, and each was satisfied by nobody being there: the car and the body were never on
the crossing in the same second of any run. On one card the body crossed at the twenty-first second and the
car at the fifth; on another the body never used its crossing at all. The claim is unfalsifiable that way
round — it can only fail by coincidence — so what it reported was the coincidence not happening.

**The map's walkers wander, and the spawn code said they paced.** A body put down beside a carriageway with
nowhere to be paces into it and back, but only on a map with no pavement on it (`TownWorld.PacesARoad`);
`Exam` lays pavement on every block, so its four walkers draw a destination anywhere in seven hundred metres
of lattice and walk off — through other cards' junctions on the way, which is the traffic nobody staged that
`ExamDrive.Hold` exists to keep the cars from being.

**So the harness orders them, as it already orders every car.** A card about paint paces its own body kerb
to kerb until the subject is near enough that the crossing it is on is the one the card is about; every
other card's body is ordered to stand where it was put down. **Pacing and not one timed crossing**: a body
parked on the paint is a car that stops for it for good, and a single crossing timed by arithmetic is a
rendezvous that a car slowing for the very body it is timed against then misses. **And it stops pacing
inside its own step-out distance** — a body stepping out a car's length in front of a moving one puts PER-15
under test instead of the crossing, which is a different card and not this one.

**The claim under every card also grew a second half.** An arrival was the whole of it, and the lattice is a
grid: the place a driver is sent to is reachable round the block, so an arrival said the car got there and
never that it crossed the junction the card was written for. It is now the arrival *and* the box, off the
`ClearedAt` the harness had been recording and nothing had been reading.

## 2026-08-28 — the exam grew by eleven cards, and all eleven are unregulated boxes

A card is a cell, so asking the exam for more crossings is asking for a bigger lattice: `ExamCards.Rows`
and `Columns` went to six, the table to thirty-six, and the roads, the spurs, the paint, the fleet and the
ground under all of it followed without a line of geometry moving. **That is the arrangement paying for
itself** — the map is derived from the cards, so the cost of a new question is the question.

**The eleven new cards are all boxes nothing governs.** Four lit junctions is enough of them: at a lit box
the timetable decides and the card is about obeying it, so the box worth staging over and over is the one
where the ranking alone decides (TER-5e). The eleven are the pairings the first twenty-five left out —
straight against the near-side turn merging in front of it, near side against across in the arm they both
join, a stem emerging across a road running both ways, a queue whose leader is turning across, and a box
with somebody on all four arms of it.

**Ten of the eleven passed the day they were written, and the eleventh narrowed a finding.** Two turns
across **from arms beside one another** clear each other, where two **opposing** ones deadlock — so what
stops the opposing pair is not that they are the same rank but that being the same rank leaves neither able
to take ground the other's path lies on. The finding stands where it was; what it is about is smaller than
it looked.

## 2026-08-27 — a map laid from the questions asked of it, and the two it could not answer

The proving ground measures what a shape of road costs a car. Nothing measured **what a car does where
roads meet**: the shipped cities have hundreds of junctions and not one of them is staged, so a turn across
the oncoming stream was only ever watched where a city happened to produce one and only ever with whatever
traffic happened to be there. `Exam` is the answer, and the arrangement it settled on is the point of this
entry.

**The cards are the map, and the map is derived from them.** `ExamCards` is a table of crossings written
as data — two arms and a stand-back per car, plus the one claim the card makes — and `ExamPlan` lays
whatever they need. A card that wants a crossroads at the edge of the lattice gets a **spur**, a short road
out to a dead end; a card about lights gets them; the corners get a spur whether they asked or not, because
two arms meeting at a right angle is a road that turns and not a junction (TER-5b). Nothing about the map is
chosen twice: the shape of a cell's junction is a fact about which arms its card asked for.

**One make of car, and it is not the police car.** A card is read against another card, so the fleet's
spread of weights and drivetrains would be a second variable inside every comparison — the exam stands the
nominal car (CAR-11a) as the measured lap does. The look is an ordinary one because in this town **a police
look is what a police car is** (SRV-2, SRV-5): every car wearing it belongs to a station, stands on its
apron and answers calls, so a lattice of them would be a lattice of service vehicles running errands
instead of cards.

**Paint on every arm, not only where a card is about paint.** The first arrangement painted four crossings
— one per card that watches one — and left every block's pavement a closed ring with no way off it, which
is a walking network of islands. TER-6's placement rule is the fix and it costs the cards
nothing: a crossing on every arm is paint on every approach, which is what a junction has.

**And two things it cannot carry, found by trying to.**

- **There is no inline junction on it.** TER-5b promises a lit mid-block crossing and the engine refuses
  one twice over: a node with two arms admits no conflicting movements, so it is never lit (TLT-3), and its
  two arms' lane ends lie over each other under the paint, so the crossing's bands come out overlapping and
  no walker can be ordered along them. The map carries a **mid-block crossing belonging to no junction**
  instead (TER-6), which is what `Zebras` does, and the promise in TER-5b is a rule with nothing behind it.
- **The lattice stands half a cell off the whole metre.** A carriageway is laid either side of its own
  centreline, so a lattice on whole metres puts every kerb exactly on a cell boundary — and a sample a hair
  short of one, which is all a straight laid at an angle read back through a sine is, lands in the cell
  beyond it. Half a cell over, nothing the map is measured against sits on a boundary at all.

## 2026-08-27 — the map says what a building is for, and its people start behind its doors

Which building was the hospital used to be a shuffle taken off the world seed when a town was opened,
because the format carried no kind on a building and an authored use would have been a use no shipped map
had. What that bought was reproducibility and nothing else: the shuffle knew which buildings *existed* and
could put a town's only hospital on a cul-de-sac with no bay within a block of it, where its four
ambulances stood nowhere and were reported as a count that did not match the roster.

So the record carries a **use** (GEN-9), the format went to version 3, and the shipped maps were rewritten
through the reader and the writer that already round-trip them. That is the migration the old decision
priced and declined, and it cost one field and one pass.

**The placement moved to where a map is authored**, which is what makes it worth doing properly:
`--place-services` takes the buildings with somewhere for their vehicles to stand and lays the services
out by farthest-point, so the next one goes as far from every service already placed as the town allows.
It is a sweep over every eligible building for every place — a second of work, once, and never again — and
it is exactly the sweep that was refused when the answer had to be produced on every load. It is a
workshop step on `--lamps`' terms: run it when a map arrives or when the shares move, and commit the file.

**And the map's people now start behind its doors.** They were already stood at them — every person spawn
on every shipped map is a stride off a way in — so what changed is that the town lets them in before the
first tick and gives them the dwell an arrival gets. A trip ends inside a building, so beginning there is
the round closing rather than a stage added to it, and the first leg anybody walks is one their own rule
drew. The dwell is drawn per person, so the doors do not all open on the same tick; the streets fill over
the first ten seconds rather than starting full of people who were never anywhere.

**What it costs is that a question about a body on the pavement can no longer be asked at tick zero.** The
suite's own answer to that is to run the town on until somebody's dwell is up, which is bounded, and the
one test that wants a walker standing still in a town for ever asks it of the proving ground — where
nobody has anywhere to be, and nobody ever went indoors.

## 2026-08-17 — the town arrives as data, and the plan is the boundary

`CityPlan` is pure data: no engine types, no node references, no behaviour, laid as structure of arrays
with a flat array and an offsets array beside it for every variable-length run. **The world is built from
that structure and never from the file**, and the `.town` format is the only thing that crosses a process
boundary.

The consequence is deliberate as a design and a real gap as a state of affairs: **there is no generator
here.** Maps are exported elsewhere and read from `towns/`, so `GEN-2` through `GEN-8` bind whatever laid
them and nothing in this project checks them. Writing a generator is the obvious way to close that, and
the plan structure is exactly what one would emit — the validator would then be shared by the generator's
retry loop and by the unit suite, as a **safety net and not a search partner**: the layouts are meant to
satisfy the rules by construction.

Until then, `src/tests/citygen/MapConformanceTests.cs` asks the shipped maps the shallow questions, which is
what a plan nobody here laid can honestly be asked.

## 2026-08-23 — the same lap twice, because the people are the variable

The proving ground answers "what does this shape of road cost a car" and could not answer "what does a slow
thing in the lane cost one". Its people are what stop the cars, and they stop them by stepping into ground
**nobody has taken** — so a driver there is never asked to follow anything, only to arrive at it and wait.

So `TrackPlan` lays two maps off one arithmetic. `Drunk` is the same lap, the same shapes and the same six
cars in the same poses; the fifteen people are put down **in** the carriageway instead of beside it, and a
body with nowhere to be that finds itself on a lane reels down it (`PER-16`). **Which rule a walker follows
is the pose the map left it in**, so the second map needed no name in any agent, no spawn kind and no field
in the format — the whole of it is where fifteen bodies stand.

Three things came out of laying it, and two of them are about the driving rather than about the map:

- **`E-4` is reachable, and had never been reached before.** Every shipped map reported "0 swerves" and
  listed the entry under never-entered; the drunks are the first thing in this town that stands in a lane
  while a driver has somewhere to be. What the lap then found was that the entry did not work — it was drawn
  flat on a road that bends, at the steering lock on a road that affords speed, and rationed by a count a
  stuck car can never earn back. All four are the [catalogue's](../../agents/car/maneuvers/docs/decision-log.md)
  and were fixed there; what this map contributed was being the only place any of it showed.
- **A drunk that wandered over the centreline was a lap nothing got round.** The oncoming lane is the only
  ground `E-4` may take (CAR-6.2b) and the only other ground is a verge, so a body free to stand anywhere
  across a 6 m carriageway is one no driver may lawfully pass. It keeps to its own lane for that reason and
  not for its own safety.
- **A body walks at what is in front of it and not along the road**, so a lurch taken at its full stride
  down a 15 m hairpin cut the chord clean across the oncoming lane and onto the grass — with a car coming
  round the bend at it. The lurch is bounded by the chord that stays inside the lane, which is
  `sqrt(8·R·sag)`: the corner formula, doing the same job for a walker that it does for a car.

**What the lap costs is quoted rather than asserted to zero**: the people knocked down, the swerves, the
back-offs, the laps given up on and the cars that ended wrecked. A pacer asks the road before it steps out
and a drunk does not, which is the whole difference between the two maps — tuning until nothing was ever hit
would be tuning until the instrument could no longer report the thing it was laid to find
([verification](../../../docs/verification.md#the-instruments-say-what-is-missing)). **Two of the six end
the run wrecked**, both in the same contact, and that is the reading the lap is currently worth arguing
about: it is a pair meeting at an angle at a shade over eight metres a second, which is what the damage
model prices a wreck at, and no driving rule refused it.

## 2026-08-22 — a map this build lays itself, and the writer that makes it a map

A measurement of what a shape of road costs a car needs a road that is one shape and nothing else, and no
city has one: every figure taken on Odesa is a figure about Odesa's corners, its traffic and its lights at
once. So the proving ground is **authored here** — `TrackPlan`, five shapes chosen against the car's own
config figures — and that is a deliberate exception to "this project does not lay plans", not a crack in
it: it is not a city, nothing about it is generated, and GEN-1 through GEN-8 have nothing to say about a
straight, a snake, an arc and two turns in a field.

**It is one lap and not four circuits.** Four separate circuits measured four shapes with one car apiece
and could say nothing about a fifth car or a second kind of drivetrain — every question of the form "is
this car slower here than that one?" needed the two cars to have driven the same road. One lap carries as
many cars as it has room for, each meeting every shape in turn, and the price is traffic: a car held by the
one in front is a car the road is no longer the reason for. What pays for it is that the holding is
*named*, so a pass somebody else was in the way of is thrown away rather than averaged in.

**The lap closes on the shapes themselves.** There is no neutral corner anywhere: the arc's three quarters
of a turn is exactly what the half turn and the quarter turn back leave over, so every bend on the map is
one of the five and the only thing a link ever is is a straight. The last link is derived rather than
chosen — whatever brings the lap home — which is why a shape that grows moves a straight instead of leaving
a step in the road.

**A shape is a road**, and that is what makes a measurement local: every consumer already knows which road
a car is on, so asking which shape it is driving is two loads rather than a search of the geometry, and no
figure can be quoted against a shape the car had already left.

**What stops a car at the end of a shape is somebody standing in the lane, and there is no light and no
paint anywhere on the lap.** A light would have been a metronome — nothing meets at a node here and there is
nothing to give way to — and it tells a driver where to stop before it has to look. A body in the road tells
it nothing, so the whole of what the track asks is that a driver stops for what it can see. Two things
about the pacers were paid for by measurement:

- **A shape ends where somebody paces rather than beginning there.** Paced at the entry instead, every
  corner would be taken from a standstill and the corner figures would be facts about the pacer.
- **The beat between two paces is drawn afresh.** A lap settles into a period, so a fixed beat meets the
  same walkers at the same point of their pacing for ever: two of the five shapes were blocked on almost
  every pass and two on almost none.

**Fifteen of them, and nobody in the middle of the straight.** The lap carries one pacer at the end of each
shape because that is where a shape's stop is measured, and ten more spread along it because a proving
ground for a body stepping into the road should be a road somebody might step into anywhere. The straight
is the one shape they are kept out of the middle of: its figure is the one speed the whole lap builds up
to, and a body halfway down it is a car that reaches 67 m/s against the gear's own 75. Every other shape is
held to a speed its radius sets, which a stop in the middle of it does not change.

**And a body that stops every car makes one platoon of the field.** Whichever car arrives first is stopped
and the ones behind close up — within ten minutes the six of them are nose to tail and stay that way, and
every pass after that is a pass somebody else was in the way of. What the probe quotes is what the field
gathered while it was still strung out, which is why watching the lap for longer buys nothing: at fifteen
bodies about seven passes in ten are thrown away for traffic, and the ten or so a shape keeps are all
gathered early.

**A pacer waits for the traffic and never for a clock.** It goes back out the moment the car it stopped —
and the queue that closed up behind that car — has gone past, which is what makes the stop the road's own
period rather than a beat's: the probe now records a stop on very nearly every pass it keeps. Sitting a
walker out on the pavement for a fixed wait was the same rig measuring whichever shapes the wait happened to
fall on, and it is the reason `Person.StandAboutS` no longer times anything a driver ever meets — what is
left of it is the bound on a stand nothing comes down the road to end.

**And every figure is read off the shape's own slowest point rather than off a standstill** — the ground
down to it, the speeds either side of it, the run back up from it. A rig that could only measure a stop was
a rig whose sample size was however often somebody happened to be in the way; this way a pass nobody
stepped out for is a measurement too, and one they did only makes the slowest point zero.

**It is written out as a file rather than kept as a plan in code.** A `CityPlan` built at run time would
have been a second kind of map — openable by whatever built it and by nothing else, invisible to `--shot`,
to the menu and to every sweep that asks a question of every shipped town. `TownWriter` is what makes it
an ordinary map instead, and the round trip over the shipped towns (`TownWriterTests`) is what keeps the
writer honest about the reader.

**Two sweeps were widened rather than worked around.** A scenario now carries "the thing it is for" as
either paint to watch or a road that bends, and the walking network's questions are asked of the maps that
have a pavement — because a map laid without one answers them vacuously rather than correctly. The
alternative was to hang a crossing and a pedestrian on a proving ground so that the rules about towns
would recognise it, which is a map lying about what it is.
