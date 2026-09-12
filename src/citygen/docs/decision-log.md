# CityGen — decision log

## 2026-09-12 — a hand-over that runs backwards gets no straight, because a centimetre here is a needle out there

The white line struck off the boundary was not smooth, and the fault was not faceting. A stretch carried
past its meeting stops a centimetre *beyond* where the next one starts, so the straight laid between them
pointed back the way the ring came — and the walk turned a half circle onto that straight and a half circle
off it. `Turn` has always said so in as many words; nothing acted on it.

**What made a centimetre matter is the offset.** A station is moved to the left of travel, and across a
backwards straight the travel reverses — so the two sides of the seam are thrown to *opposite* sides of the
line, 2 × (band + distance) apart. Three and a half metres on the kerb, seven on a line struck a lane's half
beyond it: the line darts clean across the road and back as a needle. Nothing downstream could remove it,
every station of it standing honestly clear of every driven line, and the fold rule keeping both.

**Bounded by one place and not by a taste.** Within `Kerbs.OnePlaceM` the two ends *are* one point, so there
is nothing between them to draw; beyond it a straight that runs back is real line the ring has to cover. The
ring is left with its two ends a centimetre apart and no arc between them, which every reader of it already
closes without being told — a chain of arc starts, a station walk per arc, a fill of sampled points.

**Except the one that shuts the ring**, which is laid however it runs. That straight is what makes a ring
closed, and a ring that does not close is thrown away whole: skipping it took a city from 105 rings to 99 and
lost 3.8 km of boundary and 104 driven lines' worth of edge. Eight seams a city remain for that reason and
are named in [the known gaps](../../../docs/index.md#known-gaps).

Read back off `--bench shell` on the two shipped cities, half-circle joints in the shell went 746 → 8 and
579 → 12; and everything struck off it followed — the pavement's stations standing nearer the kerb than
their own figure 473 → 34 and 471 → 6, the kerb's outward normals 175 → 23 and 117 → 12, the nodes standing
off the tarmac 1 → 0 and 2 → 0. Nothing measured worse.

## 2026-09-12 — the first line off the boundary is struck by name, and the debug line it replaces is gone

Every line beside a road is now the same ring moved by a figure, which is what makes them one construction —
and is also what makes them indistinguishable at the call site. `At(1.8f)` and `At(2f)` are two floats, and
which line a reader meant is written down nowhere. So the table is named: `GroundLine` carries the name, the
figure stays on `SimConfig` where a figure belongs, and `GroundRings.OutM` is the one place they are joined.
The kerb is in the table at nought rather than being the absence of an entry, because it is a line like any
other and is what the rest are measured from.

**The first named line is the roadside perimeter, half a lane off the kerb.** Half a *lane* and not half a
walk, which is why naming it was worth doing: it is quoted against the carriageway the boundary is the edge
of (GEN-15), so it means the same thing on a street whose pavement is the map's own figure as on one whose
pavement is the town's.

**And the perimeter layer draws it instead of a test line.** That layer used to draw a second line five
metres out in red — a distance chosen to be visible at a street framing and nothing the town had. The reading
taken from it is the same either way (whether the struck line keeps its distance, and where it cut a corner
the boundary turned), and now what it is taken of is real. White, because it is the line the town has where
the blue one is the construction it comes off.

**The boundary itself is cached on the paving.** Rounding a perimeter hands back a shell of its own and a
shell remembers every distance it has been extruded by, so the picture, the ground answer, the walking
network and the probe were each rounding their own copy and striking the whole table again. `Paving.Boundary`
is one object for the town, which is the difference between striking each line once and striking it once per
reader — and it is what lets a debug layer drawn every frame read a named line without building anything.

## 2026-09-12 — the boundary settles when the car parks are laid, and the buildings are cleared against that

Every stage of the generator reads the ground as it stands, which is right — what is on the ground at a
point is a fact about the shapes there are. What was wrong is that one stage adds driven ground *and* clears
things against it. A car park is the ways driven into it (GEN-4b) and those are driven lines like any other,
so laying one moves the edge of the driven ground the whole town is measured off (TER-3c.3); the buildings
laid after the lots in that same stage were still being cleared against the ground the lots were *chosen*
on. A building could therefore stand well clear of every kerb there was and have a car park laid up to it.

**The stage says when it is settled rather than everyone guessing.** `SlotStage` remakes the ground once its
lots are down, clears its buildings against that, and hands it back — nothing below that stage adds driven
ground, so it is the boundary the finished map answers with. The prop stage takes it instead of building the
same reading again, which is why the fix costs no extra walk of the shell.

**What it changes is where a handful of buildings stand, and nothing else.** A building the finished
boundary refuses is one the town lays at the next slot instead, so both shipped cities come out with the
count they came out with before — 1 200 buildings and 506 car parks on the larger — and the only other
figure that moves is a few props following the buildings that moved.

**And it is read back rather than argued for.** `--bench shell` walks every building's own footprint and
asks the finished ground what is under it, which is the question the generator cannot ask itself: **six of
1 200 buildings on the larger city stood on ground a car is driven over, and none do now**, nor any of the
1 100 on the other.

## 2026-09-12 — what still placed ground of its own is gone, and one switch holds back the rest

The boundary was the town's, but four things still put ground down beside a road by arithmetic of their
own, and each of them would have outlived the upgrade by being nobody's to notice.

**A raster painter with its own verge, fillet and disc.** `GroundPainter` laid cells of pavement either
side of a chain, a wedge behind each kerb fillet and a disc at each mouth. Nothing had called it since the
plan stopped shipping a cell grid; one test comment named it. Deleted rather than kept for the day a raster
comes back — a second description of the pavement that nothing reads is still a second description.

**`Kerbs` answered for tarmac that no car is driven along.** It held a fillet per junction corner and a box
per slab beside its bands, so that `OffTheTarmacM` could answer for the whole of the paving. Only the probe
still asked, and the boundary itself had never counted either (a corner apron pushes the outside off the
lanes at every mouth; a slab buries the bays it is paved under). What is left is the bands — the one shape
the shell is the outside of — and the probe's independent reading of the kerb is now that same driven
ground, which is the reading it was always weighed against. The junction corners stay on the plan because
the crossings and the stop bars are placed off them (`Furniture.Corners`); they are a mouth's figure and no
longer a piece of ground.

**A bridge carried the town's pavement across its deck.** The deck's ground was grown to the road's own half
plus a walk, and the picture laid a pavement ribbon to match — the last line beside a road derived from
something other than the boundary. Both are gone and `TER-3b.1` says so. What it costs is a bridge with no
footway, which is the same gap as a boundary that cannot cross water, and it is named as one rather than
papered over with the arithmetic that was there.

**And the four lines off the kerb are held back behind one switch.** `RoadFigures.LinesOffTheKerbLaid` is
off, so a town is its driven ground: no concrete, no kerb stroke, no walking lane, and the blocks the town
encloses come back as the grass they were laid over. **One switch and not five is the whole point** — every
one of those lines is the same boundary moved by its own distance, so there is a single place to hold them
and holding them cannot leave half of one behind. It is a stage to look at the boundary against, it is in
[docs/index.md](../../../docs/index.md#known-gaps) as such, and it goes with the faults it was raised to
make visible.

## 2026-09-12 — the shell becomes the town's boundary rather than a reading of it

The shell already walked the true edge of the driven ground and closed every ring of it on a city; nothing
but a debug layer read it. Three things were in its way and all three are now gone.

**It ran down the lines and the ground stands half a band out.** A ring carries the town's own arcs, cut out
of the lanes, movements and ways it walks — and the edge of what those lay is half a band beyond, which is a
lane's width along a lane and a space's along the way into a bay. `Extrusion` therefore takes a band per
piece and one distance beyond it, and every line the town has is this one ring moved by a figure. Two such
lines are offsets of one curve, so the band between them is exactly the difference between the distances
that struck them — which is what TER-3c.3 asked for and what no union of separately grown pieces could ever
be measured against.

**A ring could not see the ring beside it.** The fold rule was asked of the ring being moved, so an offset
that folded through a *neighbour* stood while one that folded through itself was dropped. `RingField` is one
index over the whole set answering both halves at once — how far off the rings a point stands and which side
of them it is on, from the nearest piece's own hand, with the pieces meeting at a corner answering together.
It replaced a station grid and a per-ring crossing count, and it is the same index the ground answer reads
on a tick.

**The rule held at the stations and not at the line.** What survives a fold is stations, and the straight
between two of them is not the offset: a walking lane every station of which stood exactly half a walk clear
came back over the kerb at a car park's mouth, by a metre and a half. A gap wider than two stations is now
walked and each step held out to the clearance. **A closure is continuous or it is nothing** — where a whole
tooth of the ring folds away the pushes land wherever the ground happens to be nearest, and a run of them
laid in the order the chord was walked is a star of spikes across the grass.

**The rule is the station's own reach and not the ground's, and that is measured rather than argued.** Asked
the truer way round — every station a clearance from every *band* — the answer is measurably worse, because
the extra stations it drops leave gaps the closure has to walk rather than solve. Both readings are in
`--bench shell`; this is the one that measured better, and the gap between them is the closure and not the
rule.

**A corner is turned on the ring, once.** A ring moved by an offset rounds its own corners on that offset,
so a kerb struck off an unturned ring turns on half a lane where the town is laid to turn on a car's width
times over. The junction's own radius, less the half-band the kerb already stands out by, goes on the ring
before any distance is taken — and every distance inherits it at its own radius. The fillet stops being a
shape anybody draws or answers.

**A line caps its own square end across a straight as wide as the line.** The pairing was bounded at a flat
thirty metres, which is a bound on how wide a road may be before it cannot close its own boundary: a
laboratory map laid as rows a hundred and fifty metres across had two edges running its whole length with
nothing to hand either on to, and no ground drawn at all.


## 2026-09-11 — the cut is solved against the band that makes it

A stretch of the outside ends where one band's edge goes under another's, and that place was **bisected**:
the walk stations a line every half metre, asks whether a probe standing two millimetres outside the edge
is on anybody else's band, and halves the station that changed its mind twelve times. Twelve rounds take
half a metre to a tenth of a millimetre and none of it is accuracy — **the bisection converges on the
probe**, which stands off the edge it is measuring, so the answer is short of the true crossing by that
step divided by however shallowly the two bands meet. A way out of a bay leaves its lane at a few degrees.

**The reading that says so is the meetings themselves** (`--bench shell`, *met*). Two stretches that stop
at one crossing stop at *one point*, so how far their two ends stand apart is the error and nothing else.
Odesa: **every one of 3703 pairs over a millimetre, a median of 10 mm, 496 of them past the 150 mm that is
one place, and the worst 444 mm** — which is the whole reason the pairing has to offer a radius of 1.5 m
for ends that ought to coincide, and a radius that wide in a car park is a bay's width.

**`BandEdges` solves it instead.** A band's edge is its line offset by half a width, which is an arc about
the same centre, so where two of them cross is the closed form the corner already runs on
(`Spline.CrossingsM`) and is exact to a float. The boundary is four pieces — two edges and the square end
at either end (TER-3c.6) — and the crossings against all four are solved.

**The walk still says which crossing and the arithmetic says where it is.** Two bands cross wherever they
happen to and no distance picks the one a cut means; what picks it is that the ground changed hands there,
which is what the walk measured — to a fraction of a metre, which is plenty to choose a crossing and
nowhere near enough to place one. So the bisection runs first and the solve is looked for within half a
metre of it. **Widening that window does not help and hurts**: at 1.5 m Odesa solved seventeen more cuts
and its worst meeting went from 538 mm to 1363 mm, a wrong crossing winning on nearness.

**And a candidate is verified against the band whose boundary it is — by the two directions and never by a
point either side of it.** Several boundaries stand within a stride of a junction corner and only one of
them is this cut, so the edge has to be going *under* that band there. Asked as "does it cover a point a
centimetre along" that test **rejected 2470 of the 3377 it was put to**: a centimetre along a crossing two
bands make at a couple of degrees stands a third of a millimetre inside, and at a town's coordinates that
is the last bit of a float. Read off the edge's own travel against the way that piece of boundary faces
out, nothing is cancelled and 54 are left.

**What it comes to on Odesa**: 5514 of 6475 stretch ends stand on a band's own boundary, the median meeting
goes **10 mm → 0.4 mm**, the pairs over one place **496 → 103**, and those over a millimetre 3703 → 1750 —
a pair is exact only where *both* its ends were solved, and the 961 left are where the ground ends without
any band's edge crossing there: the apron that rounds a junction corner is driven ground no line lays a
band on, and there is nothing to solve against. That is the figure `cuts solved` prints.

**The pairing radius stays at 1.5 m.** It is now loose on Odesa, whose worst meeting is half a metre, but
the fixture needs it: at 0.6 m `Test` came back with an end over and five of its rings gone. The radius is
a rounding on an exact crossing (2026-09-09) and tightening it is worth doing when the ends the walk still
places are the apron's and no longer anybody's.

## 2026-09-09 — the perimeter is the outside of one shape, walked, and not a union of bands assembled

`LaneShell` first read the outside off the union of the driven bands, and a junction has no such union:
the lanes stop at its mouths and the movements crossing it fan apart, leaving a wedge between each pair.
Every one of those movements came back with both edges on the boundary and every box in the town was
filled with lines down the middle of the road. What a road lays is one band its whole width and two of
them crossing leave nothing between, so the question is now asked of that shape — `Kerbs.OffTheDrivenM`,
which is the tarmac less the two things no car is driven *along*: the aprons that round a junction's
corners and the slab under a car park. Counting the aprons in, the outside leaves the lanes at every mouth
and no lane section is left to carry it round; counting the slab in, a car park's bays are buried and its
outside is a slab edge nobody drives.

**A stretch has a side, and the side is what says which way it is walked** — the ground on one hand
throughout. Without it a one-lane street was one stretch that could only shut on itself.

**Two stretches are joined at their edges and not at their lines.** Where two bands cross, their edges
cross at a point and each stretch stops where the other's edge reaches its own, so the two stops are the
same place and the ring is read off rather than guessed at. Said in the lines, the two stops stand half a
width apart and every rule for pairing them preferred the width of the carriageway to the corner of the
junction — each arm of every crossroads came back as a ring of its own.

**What was tried and taken back out**, because the whole town is the test and two maps disagreed: a join
allowed to leave the ground by half a metre (it fixed a handful of Odesa's leftovers and put a line down
the middle of a street beside every car park pocket), and a join that did not ask about its own two lines
(same). Both were paying for a few closed rings with drawn ground nobody drives. Widening the radius at
which two edges count as one point from half a metre to five went the same way and worse: it is a rounding
on an exact crossing, not a search, and read as a search it wires the town up wrong.

**Every ring that shuts is the answer, and the outermost alone is not.** The ground a street grid lays is
bounded on the outside and round every block it encloses. Keeping only the outer ring — told apart, exactly,
by stepping off a stretch's own clear edge and asking whether that point falls within it — gave one closed
loop per map and lost a lane: <b>a plain two-lane street is both its lanes</b>, one carried by the ring round
the town and the other by the ring round the block behind it, and a street between two blocks came back
marked down one side. What is dropped instead is only a ring that goes round nothing — a notch shut on
itself, as long round as a road is wide because it is one length of edge and the straight back along it.
Odesa's is not yet: about 350 of its 8000 stretch ends carry on into nothing, so its shell breaks, and a
break anywhere means it has no shell to show.

**What the breaks are.** They gather at the mouth where a movement leaves the lane it serves. It leaves at
a few degrees, so the two band edges cross at a few degrees, and <b>the point two near-parallel lines cross
at is the ill-conditioned one</b>: the centimetre either edge may be read out by moves the crossing metres
along both of them. The two stretches then stop three or four metres apart although the boundary runs
straight on, and no rule written in terms of how near two ends are can tell that from two ends that have
nothing to do with each other.

**What was tried against them and reverted, each because it cost the `Test` map its single loop**:
offering ends a five-metre radius (wires the town through itself); the same radius but only to pairs
heading the same way; reading a band's end as a whole width rather than half; and handing an end on to
whichever band's edge runs through the point it stopped at, which is the well-conditioned question and
found almost nothing, because the stretch on that band does not start there either.

**What the last of those rules out** is that the leftovers are a matching problem. The partner an unmatched
end wants mostly is not among the unmatched: asked for the nearest one that is free, two ends in three are
offered a straight that crosses the middle of a band or leaves the ground, which is a wrong pair rather
than a refused right one. Suppressing fewer stretches does not answer it either — the tie-break for a
shared edge lifted altogether leaves the city with two closed rings instead of none, so the missing
stretches are not the tie-break's doing.

**So the shape of the answer is probably not decompose-and-match at all.** Cutting every line into stretches
and then pairing the ends asks a global question with local information, and every rule for it trades one
map against another. <b>The boundary should be walked</b>: from a point known to be on it, along the edge it
is on until that edge goes under another band, then on along whichever band's edge carries on from there,
which cannot leave an end over because there is always exactly one way on.

**That walk was written and did not converge**, and what it cost to find out is worth keeping. Its
hand-over — *what lays an edge through the point this one stopped at* — has three cases, and the first two
were found by walking into them:

- **A crossing.** The point is read to about three centimetres, because the stopping point is where a probe
  standing two centimetres past the edge crossed into the other band. Asked to the centimetre, every
  hand-over in the town missed by three.
- **A mouth a line simply ends at.** What carries on starts at the corner the line stopped at, not round its
  end cap. Read only as a turn round the cap, no walk survived its first hand-over.
- **A band's square end laid across the edge** rather than another edge along it. Then the outside turns
  along that end and leaves by the far corner of it — <b>the far one</b>, and picking the corner by which
  way the walk was heading picks the one already stood on about half the time.

Even with all three the walk closed no ring on the fixture town, so it is not finished and it is not what
ships. It is written down because the three cases are the content of it, and the fourth — whatever is still
missing — is what the next attempt has to find.

**A stretch is carried to where its own line crosses the next one, and the crossing is solved.** A stretch
stops where its *edge* goes under the next band, which is short of where its own *line* meets the line
taking over — by half a width at a square crossing and by more at a skew one. Drawn stop to stop, that
difference is a straight over open tarmac with no lane under a metre of it, which is the one thing a
perimeter of lane sections may not be.

**Read as the point of one line nearest the other's stop, the carry is right only where the two meet
square.** Two lines an angle θ apart put that foot cos θ of a half width past the crossing they actually
make, so the line runs past the meeting and the straight comes back to it: every skew junction in Odesa had
a spike out of its corner. Solved instead — arc against arc, one determinant for two straights, a quadratic
for a straight and an arc, the radical line between two circles (`Spline.CrossingsM`) — the two lines end on
the same point and there is nothing between them at all.

**The crossing is solved off the arc's own start and never off its centre** (`Spline.TurnOf`, and
`Spline.AlongOf` under it). A road's bend is a radius of kilometres, so against the centre both
`|start − centre|² − r²` and the angle between two vectors out of it are differences of numbers agreeing to
six figures where a float carries seven: **the crossing that came back named two points a metre and a half
apart**, and the corner drawn on it landed near the junction rather than in it — which is every notch left
in the shell after the carry was otherwise right. Written as `k·|p|² = 2·(p·n)` about the piece's own start,
the curvature is a factor rather than a reciprocal, a straight is the same equation at `k = 0`, and nothing
large is ever cancelled; the along-distance is read off the chord, which stands half the turn off the start
heading. **And a corner is refused unless its two distances name one place** (`Kerbs.OnePlaceM`), because at
a town's coordinates a float is good for a few millimetres and no better, and a pair that disagrees by more
than that is a piece answering for a point it does not stand on.

**Which crossing is this corner is settled by the ground and never by a distance.** Two lines cross wherever
they happen to and a hand-over means the crossing at *this* end of them, so `Spline.CrossingsM` hands back
every crossing the two chains have, nearest first, and refuses none of them: there is no distance that could
choose, since two arms a right angle apart cross half a width past where their edges do and two that are all
but parallel cross that width divided by however shallowly they meet, which is a street away. What chooses
is the question the stretches were cut with (`LaneShell.Walk.Reaches`, and `Outside` under it): a stretch
stopped because its own edge went under somebody else's band, so between that stop and its corner the edge
is covered the whole way, and **a crossing with a length of outside standing in front of it belongs to some
other corner however near it is**.

**And how far off it may stand is the wedge between the two bands, read at the angle the two lines actually
cross at** (`LaneShell.Walk.Wedge`, over `SimConfig.JunctionCornerAlongM`) — the same arithmetic that says
where two kerbs cross, measured rather than assumed. Read at a fixed angle instead, every corner skewer than
that fell outside its own window and kept its spike; read with no bound at all, two lines an inch off
parallel are carried to wherever they finally touch, which came back as a hundred metres of straight.

**And a crossing cuts a stretch as readily as it carries one.** Allowed only to carry, Odesa met at a point
3493 times — worse than the projection it replaced. Where two lines cross before either edge has stopped
being the outside the corner stands behind the stop, and a stretch that keeps the metres past it draws them
twice.

**The two lines are run on past their own ends to find the crossing**, because the arms of a junction stop
at their own mouths and the corner between them stands on neither: read only where both chains reach, a
corner that is plainly there is missed and a straight is drawn across it. The end piece is *run on* — its own
circle carries on turning — and never a tangent struck off it, for the same reason a join is added rather
than bent in. **How far a line may be run on is the straight the corner saves**: a corner reaching further
out than the straight between the two stops is long is not a corner being tightened but a spur being
invented, and it is what two lines all but parallel come back with. No figure says it, the two stops do.

**Where two lines only touch, one is walked onto the other and then the other onto where the first now
is.** Not every pair crosses: two movements out of one mouth have parted before either of them starts, and a
way into a bay runs beside the lane it leaves. Those fall back on the point of one line nearest the other's
stop — and **carried once and at the same time, each runs to the foot of where the other *was***. Two ends
that both moved are no longer the nearest pair, and the straight left between them leaves one of them
pointing backwards, which draws a spike out of the corner however short the straight is.

**A stretch the carry leaves with no metres of its own is dropped and its neighbours handed to one another**
(`LaneShell.Dropped`). Cut back from both ends until the two meet, it is one the outside never runs along:
its corner with the stretch before it stands past its corner with the one after. Left in the ring it draws
nothing and the ring walks past it, so **the straight drawn there spans a pair of lines nobody solved a
corner for** — which is why a hand-over could read as having been carried onto a crossing and still leave a
straight behind. Odesa carries 292 away. Dropping one can carry away the next, so the carry is run again
over the shorter ring.

**Whatever the corner came out as, it is then cut back until it turns forwards** (`LaneShell.Squared`).
Neither of a corner's two ends may run against its own line's travel — that is the whole of what a corner
is, and it is arithmetic rather than a case, so it holds of a junction nobody thought of exactly as it holds
of a crossroads. Whichever end the straight runs backwards from is taken to the foot of the other, which is
the nearest place it stops running backwards; both corrections shorten, so nothing here can carry a stretch
over ground its own edge never bounded, and the rounds terminate. **Backwards is by more than two ends that
are one place may stand apart** (`Kerbs.OnePlaceM`): where the outside really does reverse — the cap on the
end of a band, the back of a car park — it crosses its own straight square on, and a corner square to within
a float would otherwise be nibbled at every round.

**What is left is measured by the turn onto the straight and off it, not by the turn from one line to the
next.** A hand-over that goes out and comes back turns a half turn onto its straight and a half turn off it
while arriving pointed much as it left, so the line-to-line turn reads it as no corner at all — which is
why the first reading of Odesa said the shell was clean. Read the other way, Odesa's hand-overs doubling
back past 135° went from **2540 to 8, of which 1 is off the line it turns back along**; the rest turn at a
point or turn onto the line they were already on, which is the cap on the end of a band and is drawn back
down its own arcs. Its hand-overs meeting at a point went from 3963 to 6391, those left drawing a straight
from 3372 to 757, and the straight drawn across all of them from 6491 m to 5091 m. Nothing that is not a
spike shows as one on this reading: the back of a car park and the end of a one-way street's band both cross
their own straight square on. **One point is one place and not one float** (`Kerbs.OnePlaceM`): counted to
the rounding instead, a thousand corners that had met exactly read as straights and the turn was taken off a
straight three millimetres long, whose direction is noise.

**And a notch in a corner shows on neither reading, so it has one of its own** (`--bench shell`, *corners*):
the straights drawn where nothing cut the ground, with what the two lines had to offer as a corner beside
each. A corner that misses by a metre turns nothing sharp enough to read as doubling back and leaves the
ring where a lane still carries it, so what says it is wrong is only that a straight was drawn over open
tarmac at all. Odesa draws 376 of them, 146 m over a hundred kilometres of perimeter, and **none of them is
a corner the shell solved and then drew across** — that reading went from 18 to 0 and is what the crossing
being verified as one place bought. What is left is lines that offered no crossing here at all.

**A ring's own closing hand-over is one like any other**, and the walk stops before it — what follows the
last stretch has already been strung. Left unsaid it answered to nothing, and the one corner a ring turns
onto itself was the last place a spike could hide.

**What that leaves is not nothing**, and the figure is worth keeping: about one part in forty of a ring is
straight with no lane under it, nearly all of it the cuts — road ends and car park backs — and the rest
steps at mouths a lane's width long.

**A join's ends are its own, but its middle never is.** The metres either side of a join belong to it — it
sets off from a line's own edge and runs beside it, and a band's width in it is still among the bands that
meet there, which is what a car park's back needs. Skipping that at each end and then short-circuiting
anything shorter than twice the skip meant <b>a join of up to two bands' widths was accepted with no
question put to it at all</b>, and a chord across the open middle of a junction box is under that. Its
middle is now asked about however short it is, which is the one point on it that is nobody's end.

**But the walk's first finding paid for the rest.** <b>The step is also the error.</b> A stretch stops where its
own probe crosses into the next band, and the probe stands a step outside the edge it is measuring, so the
stop is short of the true crossing by that step divided by however shallowly the two bands meet — and a
movement leaves the lane it serves at a few degrees. At the centimetre the step used to be, that is metres:
the whole of the gap that no rule about how near two ends are could bridge. Taken down to two millimetres,
with the grace it is read by at one, <b>Odesa's left-over ends went from about 350 to 48 and the fixture
town's from twenty to none</b>. What is left is the same coin the tie-break has always been spent on: the
step has to be wider than the grace, or which side of a shared edge a bay in the middle of a row falls on is
the last bit of a float, and every one of them comes back as the outside.

## 2026-09-09 — the outline belongs to the union, so one piece owns each station of it

`TER-3c.8` is the owner's, and the code did not meet it. Every piece offers the line half a walk outside
itself and the shell keeps the stations no piece stands *nearer* to — which settles every station except
the ones several pieces are the same distance from, and there it kept them all. There was a veto for two
cases somebody had hit (a movement running edge to edge with the arm it leaves, a band's end against the
arm across it) and none for the general one. A car park made of the movements that reach into it hits the
general one hard: four ways converge on one bay pose and end on the same straight, so six candidate lines
lie down one metre of kerb and the walk was laid off all six.

`Kerbs.Owns` breaks the tie by the lowest-numbered piece at the offset — a tie-break needs only to be the
same answer at every station of a run, not to mean anything. **Asked per station and not per span**: a span
that is duplicated over half its length is real over the other half, and dropped whole it leaves a hole in
the pavement — the fixture and the city both came apart that way before this was moved back.

**What it has not fixed**: a run whose ownership changes along it is cut where it changes, so the walk sees
two stretches where the ground has one line. The `Laid` map's mitres and its unbroken-line check are the
ones that say so.

## 2026-09-08 — a roundabout is a ring of ordinary junctions with no paint on it

Nothing about a roundabout is a new kind of thing (GEN-19): a node is opened out into a circle of nodes
joined by one-way arcs, and every rule the town already has answers for the result. The pay-off is the one
that matters — **circulating traffic is driven over what is entering, and nothing had to grant it that**.
Two ring arcs at a node are two pieces of one circle, so the movement between them is straight on and the
movement in off the arm is a turn; `TER-5e` ranks the straighter one over it and the priority a roundabout
exists for falls out of the ranking. The alternative shape, a polygon of straights, loses exactly that: at
three or four nodes every circulating movement is a turn of sixty to ninety degrees, and the ring gives way
to each arm in turn.

**The ring is one arc a node to a node, and making that work meant striking a corner on a curve.** A
junction's kerb fillets were solved between the two *lines* its arms' bearings make, which is right for
every arm that leaves straight and wrong by a metre and a half on a circle of twenty-seven metres — the
entries were filleted to points off their own tarmac, and the walking network round the ring came apart at
each of them. `Furniture.Kerb` now carries each kerb as the shape it is drawn along, a line or a circle,
and the corner and its fillet are the intersections of those: the arc tangent to both is centred where the
two of them offset by its radius meet, and it touches each at that kerb's nearest point to the centre. The
straight case is that construction and not a case beside it.

**The alternative was tried and given up.** A ring laid as straights at the nodes with an arc between them
keeps every junction's ground on a line, but the straights are chords of a circle whose entries need
twenty-odd metres of them apiece — so the ring is two thirds straight, ninety metres across, and reads as a
rounded square. A smooth circle needs a third of that width and is the shape somebody asked for.

**Nothing is painted on the ring, and that is what let it shrink.** A crossing and the bar behind it are
straight bands, so a ring that carried them had to be wide enough for the bundle to read square on its own
bend — about twenty-three metres of radius before anything else was asked. The entries carry both instead:
the zebra because that is where somebody crossing a roundabout crosses, and the bar because a bar is where a
driver holds when the junction refuses them and circulating traffic is never refused. What the ring is left
as is a circular road with entries and exits and no paint at all.

**What sizes it is the two roads leaving two of its nodes, and nothing else.** A ring's nodes are one
junction laid out as a circle rather than the accident GEN-16 is about, so they owe each other neither a
locality nor the road two separate junctions would — they owe each other what the arms leaving them do: the
ground one road takes (GEN-17) and a pavement's width on top of it, because two mouths whose paving abuts is
paving with nothing to wrap round. Held to the road instead, the circle came out nearly twice that wide for
no reason anything downstream could name. Odesa's widest went 54.6 m across → 45.8 → **32.4**.

**Four arms or it stays a junction.** Three arms opened out into a circle is a ring laid to sort out one
conflict the ranking already sorts out standing still, and it charges every car through the node a detour to
reach the arm opposite. Refusing them is free: the node keeps the junction it always was, which is what
`Roundabouts` does with every other condition it fails.

**What it costs is an island nobody walks onto**, which is what an island is: nothing stands on it, nobody
is put down on it and no trip ends there, so the walkable ground GEN-5 is about is still one piece.

**The turn a movement makes is measured off the arcs and the arms off their tangents.** Joining a node
recorded the *chord* a road left on, which for a circle is the polygon's interior angle rather than the
road's own bearing — so two pieces of one ring read as two arms lying together and GEN-13 refused them.
`TownLayout.Bearings` records the tangent now, which is what the carriageway is drawn on and what every
other reading of an arm already used.

**And two car parks are measured against the whole town rather than against their own road.** GEN-16 was
enforced by comparing each lot with the last one laid along the same kerb, which never saw a pair either
side of a junction; a laid city with roundabouts in it found one at twenty-nine metres. The pass asks every
lot now, on the same abeam test the gate does.

**The ring is not moved onto a driven half, because it is the whole of its own corridor.** A street the
scatter takes is one way of the two it was laid as and belongs on the half its traffic drives
(`RoadStage.OntoTheDrivenHalf`); a ring is laid one way round the circle its arms sized, and moved half a
lane off that circle its carriageway left its own nodes. What that cost was the pavement round the island:
the arms then ended on the ring's far kerb, exactly half a walk from the line the island's footway runs
down, so whether that pavement existed at each entry came down to the last bits of a float — three of
Odesa's four entries lost about two thirds of a metre of it, and the band was closed there with a round at
each end, two fans of concrete laid over the band that was already there. On the circle, an arm ends at the
ring's centreline like an arm at any other junction and the island's footway is one closed line.

## 2026-09-08 — one-way streets are scattered over the town rather than laid as a grid

A one-way grid put every one of them in the strict districts inside the orbital and none anywhere else, so
most of a town never met one and one district was nothing but. They are chosen over the whole layout now
(GEN-18), after the deletion passes rather than while the lattice is laid — which is also what lets the
choice be made against the town there actually is, so the settling opens far fewer of them again. What
decides the set is a spacing off the ones already taken and the rule that no junction carries two, and the
lattice no longer proposes a flow at all: every road is laid running both ways.

**Both ends have to fork, and neither may be on an arterial.** A street at a two-armed node dangles a lane
whatever else is true (GEN-18a), so taking one spends a place in the scatter on a street the settling would
open again. The arterials were the sharper find: a one-way street hung off one costs a district its second
way in, and a laid city with them had eight of the town tier's questions fail — claims left held behind a
car in a box, walkers sent over a carriageway, an ambulance that never arrived. Excluded, all eight came
back.

**What is left is two paving faults the old grid never reached** — a three-armed junction with no box, and
a corner turn setting off where no kerb ends, both on the laid city. Keeping one-way streets out of the
districts that wander hides the first and not the second, which is why neither is hidden: they are the
pavement's to answer for and not the arrangement's.

## 2026-09-07 — a movement through a node the road runs through is no piece of the outline

Where a two-arm node's arms meet as one line (TER-5b), every movement across it lies inside the two arms'
bands, so it can add nothing to the tarmac's outline — but offered, its wrap stood exactly on the arms'
own wherever the lane fills the road, and `Kerbs.Shell` kept both: a second run of pavement over the
first, with a round at each end and a kerb turned round each of those. `RoadCuts.RunsThrough` names such
nodes off their kerb corners, held to `Kerbs.JoinedM`, and `Kerbs` lays no piece for a movement across one.
The band answered is unchanged, being a union; what changed is that it is drawn once.

## 2026-09-07 — a graze is not a run

A wrapping line meeting another tangentially runs √(2·R·ε) past the crossing before it is that much
inside, which is what `Kerbs.OnePlaceM` bounds — the paving welded at `Kerbs.RoundingM` instead, so every
graze left a run whose two ends are one place: a walk-wide round of pavement struck off a few centimetres
of line, answered as ground. It welds at one place now, and Odesa's runs went 4322 → 2971 for a tenth of a
percent of pavement.

## 2026-09-07 — a walking lane that stops in mid-pavement is a corner nothing wrapped

Thirty dead ends over two towns, both causes a corner in the carriageway that is not really there — the
wrap was reading the tarmac correctly both times. `RoadStage.Rounded` gave up a too-tight vertex *while
laying the chain*, so the arc before it had already been aimed there and the straight after set off on a
bearing nothing arrived on: a fifth of a turn at five and a half metres out is three and a half metres of
pavement never laid. The vertex is dropped and the line laid again, and the deflection is read as the turn
rather than as its sine. And `JunctionTurnsACorner` asked how far out a kerb crossing stood and not which
side of the arms, so where a one-way street's kerb runs behind its own node (TER-4d) the crossing fell on
the far side — a lens of carriageway hanging 0.43 m off the kerb with no tarmac under it, which the
pavement then wrapped. The crossing must stand out along both arms. Three gates, each a defect in the
carriageway before it is one in the pavement, which is why none is a gate on the wrap.

## 2026-09-06 — the pavement is the tarmac grown by a walk, movements and squared ends included

The walk was laid off the tarmac and drawn off the roads, which are not the same shape: a movement swings
wider than either arm it runs between, so 109 sampled metres of Odesa's footway turned its junction corners
over open grass. Grass is walkable, so nothing refused it. The end of a band is turned the same way
(TER-3c.6) — a distance turns a corner, only a half-width squares one. And the inner-corner solver is gone
rather than fixed (`PavementCorners`, 460 lines): it was a second description of a shape that already had
one, laid off the roads and lots alone, drawing thin wedges of pavement into the verge. There is nothing
for it to round, because growing each piece by one figure and taking the union is growing the union
(TER-3c.3), so the shell has exactly the corners the tarmac has — an arc laid over the verge to hide a
pinch is pavement no walker can be given ground on. It cost 33 ms of Odesa's load and 60 km of walked
outline.

## 2026-09-06 — a junction has no shape, and the lines a car is driven on are laid with the town

`LaneLines` lays every lane and connector when a map is generated, and the graph reads those lines and adds
only the rules over them. The point is that the surface and the network can no longer be two answers: the
tarmac *is* the lines. So the disc is gone — what a box is on the ground is the band its own connectors
sweep, with kerb fillets rounding the wedges, and nothing has to recognise a junction to get it right; a
skewed, one-way or five-armed box comes out correct because each movement was drawn where a car goes. The
stored radius survives as planning only. Two things it had been hiding came back: a band must be read the
way a road's is, squared at both ends, or every point on a bend answers off perpendicular and the road's
centreline comes out as pavement; and a dead end is the one junction whose ground no movement sweeps
(TER-5a), so its head is a shape read off the arms rather than a junction record. The pavement round a box
is the arms' and not the movements' — a second band adds no ground, only another edge for a corner solver
to find.

## 2026-09-05 — the pavement is laid once, and the picture and the answer read that laying

`GroundMesh.Build` laid the pavement as draw calls and `GroundShapes` laid the same four pieces again as
coverage tests, with nothing but memory keeping them in step (TER-7) — and the failure it invites is quiet:
a band widened in the picture and not the answer is a walker refused ground it can see it is standing on.
`Paving.Lay` states the pieces once. The restructure is exactly behaviour-preserving and the frames come
back byte-identical, which is the only test that could have said so.

## 2026-09-05 — a town keeps only the one-way streets it can be driven round with

Drivable is asked of the movements and not the roads (GEN-18): a block whose streets all run inwards keeps
one connected component and is still somewhere a car drives into and never leaves. Reachable everywhere was
not enough either, because the fault is local (GEN-18a) — a one-way street at a two-armed node leaves a lane
nothing ever arrives on, so the town draws a lane, a stop bar and a line no car is on. A movement leaving a
node needs some road other than its own arriving there.

## 2026-09-05 — the lattice is ground, and two exams stand on it

The walking exam wanted the driving exam's map without its cars, and the only way to have it was to copy
four hundred lines of geometry into a second plan — a junction laid twice can pass one exam and fail the
other for a reason nobody can name. `ExamGround` is the ground and `ExamMap` writes one out as a
`CityPlan`; what is left in `ExamLattice` is the cars. A card names a place as an arm, a side and a
distance out and never as a point, so a card cannot drift from the map it is staged on.

## 2026-09-03 — a dead end's head holds the car's body, not the path of its middle

A head was sized `turning circle + the car's width`, but a turning circle is the radius the car's *middle*
sweeps and the furthest corner stands half a length and half a width off it — so the head was always about
two metres short, hidden only by a classifier of metre squares answering *drivable* half a cell past every
kerb. The figure is derived from the body: the circle, the width TER-5a asks to be left clear, and the
half-diagonal of the car.

## 2026-09-01 — a prop's kind is where it stands, and the ground decides it rather than a die

Every prop was a coin toss between *tree*, *scatter* and *furniture* — a taxonomy of what the pictures are
— so a litter bin stood in a field as often as an oak did and the town read as three-way noise everywhere,
which is the one thing a scatter must not be. The kinds are placements now (GEN-6b) — wild, planted,
furniture — read off the grounds within a verge of each candidate. A verge still carries wild looks half
the time, since a kerb planted only with what a town plants reads as a catalogue laid end to end. The size
band went with the kind: the great trees are authored at 2.6–3 m against a draw stopping at 2.2, so nine
looks were unreachable except through a fallback that does not resize. Thirty looks were deleted rather
than filed, each failing the same test — name what this is and say which of the three places it stands in;
eleven were park amenities and **this town has no park to put them in**. The verge is walked and no longer
swept, because deciding by probing a lattice square answered a question about a *line* with no bearing and
no distance from the kerb. A car park's edges are walked too, and its verge begins past the walk that wraps
it (GEN-4d) — laid half a metre out like a kerb's, every candidate was refused. The collar GEN-6a asks for
turned out to be the sweep's and not the verge's: a kerb walk is not blind. A prop's picture was bigger
than the prop (GEN-6d), drawn `diameterM` *tall*, so the flower planter was 3.45 m across against an
authored 1.9. Nothing overlaps any more, indexed by a grid of the widest prop's own width, since neither
pass can see where the other put anything. Odesa laid 108,939 props and lays 78,705.

## 2026-09-01 — an arm's paint is set back from that arm's own mouth, and the setback is a car length

The reach was half a carriageway plus a full corner radius — right for square arms and wrong for every
other, since two kerbs meeting at an angle cross well outside the mouth. The setback was carrying six
metres of slack for a skew nobody had measured, so a square junction's zebra sat two road widths off its
own kerb. The reach is solved corner by corner, and what is left of the setback is what the name says: a
stride, so the zebra's end bars stand on straight kerb. The two figures the fillet was borrowing are its
own now, since bounding a corner by the crossing's setback was the circularity.

## 2026-09-01 — a car park is three to six bays, because a run of frontage is an apron

Merging neighbouring slots laid sixteen bays of unbroken tarmac down one side of a street, and nothing ever
filled it — the town's whole roster is 520 cars over 319 lots. The merge was the right answer to the wrong
question: GEN-16 merges a junction because refusing one deletes every road at it, and nothing hangs off a
car park. GEN-4b bounds a lot at both ends, three to six. The bounds are what a lot *is* rather than a
tuning — the upper makes a car park a car park rather than a surface, the lower keeps it from being a
two-car lay-by that cost a lot's whole clearance. The clearance is measured between the rectangles and not
along the road, since on a bend an arc runs longer than the chord. Odesa's 319 lots hold 1377 bays where
they held about 3500.

## 2026-09-01 — the crossings were unpicked, because planarity was an argument and not a check

Odesa laid an orbital and a street across each other with no junction where they met — 20 m of shared
tarmac, and seven of sixty fixture seeds held at least one. The case for planarity was the arrangement and
it was sound; the coverage was not, since `Arterials.CrossesTheRing` had a single caller and `ExamGround.Hang`
never asked it. Adding the missing call was not the fix: a rule that holds only where somebody remembered
to invoke it is the same defect a year later. GEN-17 states the property over the town and
`TownLayout.UnpickTheCrossings` is one pass. It is a pass over the settled layout rather than a test inside
`Join`, because the lattice hangs its streets before `Arterials.Close`, so a test at offer time would have
deleted the orbital. Measured against what will be drawn, not against what was joined, using
`RoadStage.StraysM`. Odesa loses 6 roads of 316 and bought back five failing conformance cases.

## 2026-08-31 — two of a kind near enough to be one are merged, and merging beat refusing

The arterials, the lattice and the frontage are each laid at their own spacing and none knows what the
others left there, so near-coincidence is the ordinary case. Refusing the second of a pair costs the whole
piece that hung off it (GEN-8), which is how a town loses a block to an arithmetic coincidence. The nodes
are merged once the layout is joined and not welded as they are placed — welding took half the town, since
an arterial handed back a node twenty-five metres off its own line lays its next piece elsewhere and the
chain breaks. What is merged is offered again road by road in the order the town cares about them, or a
street severs the arterial it was hung off: bridges, arterials, streets. Odesa is 51 km of road against 42.

## 2026-08-31 — a lane is the width the town is laid in, and the straight stub is what stands on it

A road was three car widths across because somebody wrote three. A lane is 1.8 car widths, so every figure
quoted against a lane means the same thing on every map (GEN-15), and both road and pavement are ratios
rather than metres — a figure authored in metres beside them would be the one that stopped scaling.
Widening found the real defect: the stub was authored at four car lengths against the 20.4 m the junction's
ground, fillet, crossing and bar actually take, so the crossing hung onto the bend at the wider road. The
stub is derived from what is laid on it.

## 2026-08-31 — the bank is a curve now, and the water is set in a shore

A shoreline was twenty-four points over four kilometres — the bank has always been a sum of three sines and
what was rugged was the sampling. The count is derived from the wave: a chord stands off a curve by about
its own length squared over eight times the bend, so the step falls out of a tolerance of half a cell,
which is the finest the ground under the bank is classified. The water is set in a shore (GEN-2c) laid as
the same wave a shore's width wider, so no band has to be fitted to a curve afterwards. The map carries
four rings drawn largest first, so each fill leaves a line's width of the one under it showing — nothing is
offset by the renderer and nothing is classified twice. Each line is the colour of what it borders and
darker, so it reads as the shore's shadow; the ground under both is shore, because a line is a picture of
an edge. The shore is not grass, which is the whole of why nothing stands on it. It wears the pavement's
texture as a placeholder until there is a picture of a beach.

## 2026-08-31 — the map ends at its edge, and the shore is cut there rather than never drawn

The outline pushes a sea's far bank past the map on purpose, and the raster only took the cells that
existed — but the outline is what the mesh *draws* from, so it drew open sea over the void. The shape is
cut where it becomes a plan (GEN-2b); laying a shore that ends on the edge puts the map's rectangle inside
the meander arithmetic and gives a coast four corner cases, where clipping a ring against four half-planes
has none. `Test`'s river ran thirty metres off the top of its map and nobody had noticed. A prop is refused
with its radius rather than its centre, and the bar is asked of every map now.

## 2026-08-31 — a street that goes nowhere is deleted, and the fixture brief had to become a town

Odesa carried 21 junctions of one arm: dead ends in the sense TER-5a means without being dead ends in the
sense it promises, since the road stage lays the disc its arms need and a car found three metres of tarmac
with no room to turn. They are deleted with whatever hangs off them (GEN-5a), swept to a fixed point —
GEN-8's own answer applied one node at a time, and for the same reason: an arm grown on to close the loop
would have to cross whatever cut the street short. Turning heads were the alternative and are worse, since
a cul-de-sac is a thing a town plans. It costs a city a tenth of its road. It exposed that the property
suite's fixture brief was not a town — two of four seeds laid a layout with no cycle at all, one a pure
tree the sweep correctly deleted in its entirety.

## 2026-08-31 — a bridge is a road, and the wheel is turned so there is one to build

The generator skipped a node that fell in the river and joined whatever dry nodes were left, so a bridge
was however far apart the spacing had left them, and the hub sat in the water. A bridge is a class of road
(GEN-14a): water takes a `Bridge` and nothing else, so a street cannot cross, the orbital gives up its arc
over the span, and a span longer than the deck a town builds is a road not laid. The nodes make a crossing
short rather than a search afterwards (GEN-14b) — a node on each bank, with the stretch between closed to
everything else, because a node between two bridgeheads is a junction on the deck. The wheel is turned so a
spoke runs down the river's normal, since a bridgehead pushed off the ray leaves the sector that ray bounds
— the rotation was a draw anyway. The sea is not bridged at all. And the water question is asked of the
carriageway and not the centreline: Odesa lost about a sixth of its road length to lanes over the river.

## 2026-08-31 — a city is a seed and a brief, and every stage of laying one runs once

Cities arrived as baked `.town` files, so GEN-2 through GEN-8 bound whatever exported them and a city could
not be varied, replayed or repaired when a rule moved. Only hints are persisted and never geometry: the
moment a brief carries a node there are two answers to where the town is, and the one on disk goes stale.
Nothing retries, and four things make GEN-10 affordable, each replacing a search — the districts are convex
so planarity is arranged rather than checked; the arterials carry a node wherever a street meets one; a
slot claims its padding before anything fills it; and what is left over is deleted rather than joined up.
Two bounds came out of measuring the traced cities: their median sinuosity is 1.000, so straight is what a
street is, and bending is concentrated where they put it. A junction's arms must stand square enough to be
a junction (GEN-13), learned by laying towns without the rule — at a shallow angle the fillet on one arm
paves the crossing on the other. A building is sized by the roof it will wear, the footprints crossing the
seam as data because the plan may not read a catalogue above it. It deleted `TownWriter`, `--lay-maps` and
`--place-services`.

## 2026-08-31 — the ring is a rounded square, because what stands inside it is a rectangle

The start menu opens over this map and a panel is a rectangle, so a disc spends its ground on four corners
nothing reaches into — the widest rectangle inside one is 0.7 of its width. The corners keep it a road: a
square would be four right angles no car takes at speed. The cuts moved to the middle of the straights,
since a node on a bend takes a bite out of the one piece of the loop whose shape matters. The escort's pace
is read against the *tightest* corner, which is where the convoy comes apart if it is going to.

## 2026-08-31 — the ring carries an escort and one car, and the escort is held to its charge

Two convoys of three read as a staging rather than as traffic. Police tyres are worth nearly twice the
grip, so the escort cornered a third faster and left its charge inside a lap; the fix is a pace ceiling set
from what the *escorted* build affords on this radius. Building the escort on the armoured car's figures
and painting it white is a police car that corners like an APC — the paint and the physics coming apart is
what the dress-don't-build rule exists to prevent. The ceiling alone did not close the convoy up, because
the gap is the road a follower is granted plus the interval it leaves on top: a per-car share of the
following interval is what closes it. Cutting `Driving.FollowingHeadwayS` was not on the table — it is what
every car in every town keeps.

## 2026-08-30 — the menu is drawn over the ring, and GEN-1b now says which map that is

GEN-1b is about not building a *city* nobody asked for, which says nothing about a map costing a fraction
of one that was laid to be looked at. Standing a town up no longer means dropping the reader into it: the
ring is opened with the menu deliberately left up, since reopening the menu afterwards is the same state
reached by two moves with a flicker in between.

## 2026-08-30 — a map laid to be looked at, and the look rule it had to loosen

Every other map answers a question; this one is the picture the game idles on, so what shaped it is that it
never stops being worth watching and never needs anybody's attention. It stands at the left of the frame,
because the menu hangs from the gear in the top right. Its size is the window's and not the driving's — a
120 m radius made a picture of empty road for twenty seconds at a time, so the radius is whatever the
opening view holds, and what that costs is that the corner sets the speed. Nothing on it is staged, which
is why it is worth shipping: a loop of scripted cars would be an animation. Four roads, because a road ends
at a junction and two would join the same pair of nodes twice. The map dresses its own cars, and that cost
a rule — the service tier had been finding vehicles by their paint, which is an over-fit, since SRV-3
defines one as paint **and** a building.

## 2026-08-28 — the exam orders its walkers, because three cards were passing on an empty crossing

The three `StopsForThePaint` cards had never once been asked: the car and the body were never on the
crossing in the same second of any run, so the claim could only fail by coincidence. The map's walkers
wander and the spawn code said they paced — a body paces only on a map with no pavement, and `Exam` lays
pavement on every block. The harness orders them, pacing rather than timing one crossing, since a body
parked on the paint is a car that stops for it for good and a timed rendezvous is one the car's own slowing
then misses. It stops pacing inside its own step-out distance, or PER-15 is under test instead of the
crossing. The claim grew a second half: the lattice is a grid, so an arrival said the car got there and
never that it crossed the junction the card was written for.

## 2026-08-28 — the exam grew by eleven cards, and all eleven are unregulated boxes

A card is a cell, so asking for more crossings is asking for a bigger lattice — the roads, spurs, paint,
fleet and ground followed without a line of geometry moving, which is the arrangement paying for itself.
The eleven are boxes nothing governs: at a lit box the timetable decides, so the box worth staging over and
over is the one where the ranking alone decides (TER-5e). The eleventh narrowed a finding — two turns
across from arms *beside* one another clear each other where two *opposing* ones deadlock, so what stops
the opposing pair is not that they are the same rank.

## 2026-08-27 — a map laid from the questions asked of it, and the two it could not answer

Nothing measured what a car does where roads meet: the shipped cities have hundreds of junctions and not
one is staged. The cards are the map and the map is derived from them — `ExamCards` is a table written as
data and `ExamPlan` lays whatever they need, so nothing about the map is chosen twice. One make of car and
it is not the police car, since a card is read against another card and in this town a police look *is* a
police car (SRV-2, SRV-5). Paint on every arm, not only where a card is about paint, or every block's
pavement is a closed ring and the walking network is islands. Two things it cannot carry: there is no
inline junction on it — TER-5b promises a lit mid-block crossing and the engine refuses one twice over, so
that promise is a rule with nothing behind it; and the lattice stands half a cell off the whole metre, or
every kerb lands exactly on a cell boundary.

## 2026-08-27 — the map says what a building is for, and its people start behind its doors

A shuffle off the world seed knew which buildings existed and could put a town's only hospital on a
cul-de-sac with no bay within a block. The record carries a use (GEN-9) and the format went to version 3 —
the migration the old decision priced and declined, at one field and one pass. The placement moved to where
a map is authored, laid out by farthest-point: a second of work once, which is exactly the sweep refused
when the answer had to be produced on every load. And the map's people start behind its doors, having
already been stood at them — a trip ends inside a building, so beginning there closes the round rather than
adding a stage. The dwell is drawn per person, so the streets fill over ten seconds. What it costs is that
a question about a body on the pavement can no longer be asked at tick zero.

## 2026-08-17 — the town arrives as data, and the plan is the boundary

`CityPlan` is pure data laid as structure of arrays, and the world is built from that structure and never
from the file. The consequence is deliberate as a design and a real gap as a state of affairs: there is no
generator here, so GEN-2 through GEN-8 bind whatever laid the maps and nothing here checks them. A
validator would be shared by a generator's retry loop and the unit suite as a safety net and not a search
partner — the layouts are meant to satisfy the rules by construction.

## 2026-08-23 — the same lap twice, because the people are the variable

The proving ground's people stop cars by stepping into ground nobody has taken, so a driver there is never
asked to follow anything. `Drunk` is the same lap with the fifteen people put down *in* the carriageway,
and a body with nowhere to be that finds itself on a lane reels down it (`PER-16`) — which rule a walker
follows is the pose the map left it in, so the second map needed no name in any agent and no field in the
format. Three things came out of it. `E-4` is reachable and had never been reached before, the drunks being
the first thing that stands in a lane while a driver has somewhere to be; what the lap found was that the
entry did not work, and all four faults are the catalogue's. A drunk over the centreline is a lap nothing
gets round, since the oncoming lane is the only ground `E-4` may take — it keeps to its own lane for that
reason and not for its own safety. And a body walks at what is in front of it rather than along the road,
so a lurch at full stride cut the chord across a hairpin onto the grass; it is bounded by `sqrt(8·R·sag)`,
the corner formula doing the same job for a walker that it does for a car. What the lap costs is quoted
rather than asserted to zero — tuning until nothing was ever hit is tuning until the instrument can no
longer report what it was laid to find.

## 2026-08-22 — a map this build lays itself, and the writer that makes it a map

Every figure taken on Odesa is a figure about Odesa's corners, traffic and lights at once, so the proving
ground is authored here — a deliberate exception to "this project does not lay plans" rather than a crack
in it. It is one lap and not four circuits, because four circuits could say nothing about a fifth car or a
second drivetrain; the price is traffic, paid for by the holding being *named*, so a pass somebody was in
the way of is thrown away rather than averaged in. The lap closes on the shapes themselves, so the only
thing a link ever is is a straight and the last one is derived. A shape is a road, which is what makes a
measurement local. There is no light and no paint anywhere on it — a light tells a driver where to stop
before it has to look — so the whole of what the track asks is that a driver stops for what it can see. A
shape ends where somebody paces rather than beginning there, or every corner is taken from a standstill;
the beat between two paces is drawn afresh, or a settled lap meets the same walkers at the same point for
ever. A pacer waits for the traffic and never for a clock. Every figure is read off the shape's own slowest
point rather than off a standstill, so a pass nobody stepped out for is a measurement too. It is written
out as a file rather than kept as a plan in code, or it would be a second kind of map invisible to
`--shot`, to the menu and to every sweep.

## The last town laid is kept, and only the last

`Maps.Plan` laid a fresh town every time it was asked, so anything that asked twice paid twice: a review
sheet, a probe taking several readings, the visual tier staging a scenario a cell. A town is laid
deterministically from its own seed, so the second lay is the first town again and all it buys is the wait —
34 seconds of it on a city, and another 30 for the ground mesh that is a pure function of the plan under it.

**One town and never a table of them.** Kept by name, the first sweep over every shipped city would hold all
of them alive at once and a city is tens of megabytes of arrays. Kept as the last, it is never more than the
town whoever asked is about to use anyway — and the pattern that costs is repetition rather than revisiting.

**The figures are part of the name**, because a plan lays its pavement against the configuration it was
first asked with (`CityPlan.Paving`), so a town under other figures is a different town.

**And a plan is as far as it goes.** The walking graph over one is the largest single cost of standing a town
up — 33 seconds against a tenth of a second for the road graph beside it — and it is as pure a function of
the plan as the pavement is, but handing one graph to two towns is not safe: the index it answers
`NearestEdge` from carries a scratch of its own (`ChainIndex`), and two towns asking at once is two walks
over one working set. Tried, it broke a hundred cases of the suite. What that costs is the shot path's to
work around (`TownStanding`), and what would let it be shared is the scratch belonging to whoever asks.

## A straight is drawn where the ground is cut, and a line that carries on is not cut

`LaneShell.Fits` offered a straight wherever the tarmac would hold one up, and at a junction mouth the
tarmac holds up the cap across the carriageway — a band's width, the nearest join on offer anywhere. So a
lane that stopped at its mouth because the box carries the ground on could take its own other edge instead
of going round the movements crossing the box, and the perimeter came back as a rectangle round the street
with the junction inside it left to nothing. `Capping` ranked that last and ranking is no use where the cap
is the only join left.

**A straight is now offered only where the ground really is cut** (`Walk.Cut`): the stretch stopped at its
own line's end and nothing is driven on from there — a dead end, the place a lane runs out at, the back of
a bay. `LaneShell.Stops` reads that off the movements, which is where it is already written: a lane stops
at the end no connector leaves and the start none arrives at, a movement through a box stops at neither
end, and a bay's way stops at the bay and not at the lane.

**And only at the line's own end.** A stretch that stops in the *middle* of its line stopped because a
neighbour covered its edge, and the step across to that neighbour is a length of boundary whatever either
line does further on — the back of a car park is full of them, where the outside comes down one bay's edge
and crosses to the next. Read as a cut as well, fifty-two of Odesa's joins went and thirty of its rings
with them: forty kilometres of perimeter paid for one rule stated a shade too wide.

What it costs where it bites is a ring: Odesa loses one of ninety-eight and River one of seventy, both of
them nothing round. `--bench shell` says how many cuts a town drew and between what kinds of line.

## The other edge of a road is the other lane's, so a cap across a carriageway is one

`Capping` ranks a cap behind every other join, and it read a cap as a line taken against *itself*. Most of
the town's roads are two lanes, so at the end of one the cap on offer is between two different lines — the
lane and the one coming the other way — and it ranked level with a corner. A cap is a lane's width and
nothing else on offer is ever nearer, so where a road runs into a car park the outside took the cap at the
mouth and the lot behind it was left unwrapped: the straight round to the first bay lost on distance to a
straight clean across the carriageway. `Walk.OneCarriageway` reads the pair off `LaneLines.LaneReverse`,
and a road that really stops still takes its cap, being the only join left.

It costs nothing on any shipped town — Test, Zebras, River and Odesa come back to the metre and the ring on
what they were — because the cap it refuses is a car park's mouth and none of the four has a lot at the end
of a road. **The fault was reported from a frame and no town the suite can lay reproduces it**, which is
why what is fixed here is the mechanism named in the report and not a measured difference.

**And a cap is the join that crosses a carriageway, not every join inside one.** Read on line identity
alone, a stretch of one edge handing over to the next stretch of *that same edge* — the boundary stepping
over the few metres a bay's way lays across it and carrying straight on — was ranked a cap and went behind
every join in the town. In a lot that put a bay way fourteen metres off first, and the ring left the row of
bays on a straight over the middle of the tarmac and came back on itself: the spike the report showed. One
side of a road is the left edge of one lane and the right edge of the other, the two running opposite ways,
so the two ends of a join are on opposite sides exactly where that agreement fails, and that is the whole
of the test. Odesa comes back with two more rings and seventy metres, River with one more and thirty, and
both towns' deepest spike — a hand-over doubling back 153° over thirteen metres — is gone; Test and Zebras
are unmoved, having no lot a row deep.

**What was tried and reverted.** Reading a stop in the middle of a line as a cut only towards the line that
covers it there (`Walk.Holds`) is the same rule said as geometry rather than as ranking. It refuses more
than it should: a bay's way is covered by its own bay's other way as often as by its neighbour's, and Odesa
lost four rings and thirty-six kilometres, River twelve ends that had closed.

## The probe says what the cuts are, and where the ring jogs

Four readings were missing and each one hid a fault of its own.

**The corners table was printed behind the spikes** — `Handovers` returned early where nothing doubled
back, and a town whose rings turn no spikes is exactly the town whose notches nobody has looked at. It is
its own section now.

**`cuts`** is the other half of `corners`: the straights drawn where the ground *is* cut, longest first and
counted by the two kinds of line each joins. That is what says a cap was taken at a mouth — a lane–lane cut
in a town whose roads all carry on — and no table that leaves the cuts out can.

**`dips`** is where the ring steps off a line onto one leaving it and straight back onto the same line. A
corner the ring really turns does not come back to the line it left, so every one of these is a jog in what
is one line. It found the fault it was opened for and it was not the shell's: Odesa drew four hundred and
sixty-one, a hundred and forty of them shorter than a hand, because **a bay's way lays the parking space's
width and the lane it is drawn out of lays a lane's** — so for the metres the way ran inside the lane its
band stood a fifth of a metre outside the lane's own edge and the outside had to follow it. The shell was
reading the ground correctly and the ground was lumpy, which is why what was fixed was the figure
([parking's log](../../world/parking/docs/decision-log.md#2026-09-11--the-room-beside-a-car-and-the-room-at-its-ends-are-two-figures)).
Odesa now draws none.

**`across`** is the only table that takes the shell's word for nothing. Every other one is read off how the
pairing classified a join; this one asks the town's own shape where the straight ended up
(`Kerbs.OffTheDrivenM`), so a join the shell calls a cut and the ground calls a chord through the middle of
a road comes back whatever it was called. It is what says a cap stands half a carriageway in while the back
of a car park stands on the edge, and the depth of a cap is exactly what `Carries` allows a join — so
tightening that figure would refuse a road that really stops along with the one that does not, and the
difference is not in the depth.

## The corner is the crossing, and how far out one may stand is the town's own reach

A stretch is carried along its own line to where that line crosses the next one (`LaneShell.Carried`), and
the two lines are run on past their own ends to find it, because the arms of a junction stop at their own
mouths and the corner between them stands on neither. **How far they may be run on is
`SimConfig.JunctionArmReachMaxM`** — the town's reach at the sharpest corner it allows, which is the same
figure that says where an arm's own ground begins.

Run on by the straight between the two stops instead, a right-angled corner whose arms both stop at their
mouths had its crossing a whole box away and came back as no corner at all: a hundred and eighty corners in
Odesa drew a straight over open tarmac that a crossing was standing there to solve. Run on without a bound,
an arc is carried round its own circle and answers for a place it never reaches.

## A join skirts the edge of the ground, and a corner may be solved instead of drawn

Two rules decided which stretch may carry the outside on into which (`LaneShell.Fits`), and both were the
wrong question.

**It asked whether the straight between the two band edges lay inside some other band.** At a junction the
wedge between two arms is paved back to a fillet (TER-5) and the movements crossing the box run under it, so
the straight from one lane's kerb to the next lane's kerb — the corner itself, three metres of it — crosses
a band the whole way and was refused. The lane, left with nowhere to go, capped on its own other edge at the
mouth of the junction and the outside stopped dead there. What replaces it is one question about the town's
own shape: **the straight must stay on the driven ground and no further in from the edge of it than the two
bands are thick together** (`Walk.Skirts`, `Kerbs.OffTheDrivenM`). A join skirts the boundary and is the
chord of whatever corner it turns, so how far in it goes is what says it has left the boundary; and the two
half widths together are what a *cap* needs, being half the carriageway two lanes make up — read at the
narrower of them, every road that really stops came back with its end uncrossed and the ring round it broken.

**And it asked only about the straight.** Carried onto a crossing the pair meets at a point and there is no
straight for the ground to have to hold up, so a pairing fits if *either* the two lines cross where this
corner is *or* the ground carries the straight between their edges. The two are different joins and only the
second draws anything.

## A swap that costs metres is a hole moved, not a hole shut

An end the pairing leaves over is usually not short of a partner but short of *its own*, taken first come by
an end that had somewhere else to go, so the holder is asked to go there instead (`LaneShell.Swap`). Taken in
the order the stretches happen to be numbered, the first legal swap is as likely as not to give up a
hand-over whose two edges met within a hand's breadth and send its holder clean across a junction: at one
Odesa mouth a 0.22 m meeting was traded for a 7.2 m one so that a lane could cap on itself.

**Of every swap the ground allows, the one that costs the fewest metres** — what the two new joins are longer
than the one they replace. Bounding it instead (the holder may go no further than it was going) leaves the
fault where it is, which sounds better and is not: twenty-nine ends came back over and each one broke a ring,
so a third of the town's perimeter went with them.

## A stretch shorter than its own two ends are apart is not one

`LaneShell.Keep` threw away a stretch shorter than the rounding its ends are bisected to, a millimetre, which
is a hundred times too fine. A walk half a metre at a step leaves a centimetre of edge standing wherever it
grazes a crossing — long enough to pass a millimetre and short enough that its two ends are one place, so
nothing meets it on either side and it carries the outside on to nothing. One such hair of a bay's way broke
the ring round a car park and took 43 lines of perimeter with it. The floor is `Kerbs.OnePlaceM`, which is
already what says two ends are the same place.

## The shell probe answers about one place

`--bench shell --at X Y` lists the stretches with an end within a junction's breadth of a place, what each
carries the outside on to, and what its corner came to (`ShellProbe.About`). Every fault fixed above was
found with it and none of them was findable without it: the town-wide tables say a corner is wrong and the
pictures say where, but which of a junction's eight stretches was handed to which is a wiring question, and
guessing at it from the code cost three changes that measured as no-ops before the first one that did not.

The refusal is named as well (`ShellCorner`): a crossing thrown out for naming two places, for standing
outside the wedge, or for having a length of outside still in front of it are three different faults with
three different fixes, and lumped together as *refused* they read as one. On Odesa the wedge refuses nothing
and never has — it was the rule most suspected and it is the rule doing no harm.

## A corner past the end of both lines is not a corner, and a join is drawn on the lines

Two arms of a junction both *end* at it, so between one lane's last metre and the next lane's last metre
there is no line at all — the fillet rounds that corner (TER-5) and nothing runs along it. Carried to where
the two lines cross anyway, the ring ran one lane on two metres past its mouth and the other five and a
half, met them at a point out in the middle of the box and came back: a square spur of perimeter over ground
neither lane covers a metre of, and the thing a reader sees first at a T junction.

**A crossing past the ends of both lines is refused** (`ShellCorner.Beyond`). Past a line's own end there is
no band and nothing to ask, so a crossing out there answers to none of the rules that decide which crossing
is this corner; one of the two has to reach it without being run on at all. One corner in Odesa is refused
by it and it is the one that was drawing the spur.

**And what is then drawn is the straight between the two lines**, which is where the third way of fitting a
join comes from (`LaneShell.Fits`). The edges are where the boundary runs and the lines are what is drawn,
and a junction corner is where those part company: the straight between the two band edges crosses the
pavement the corner goes round and the ground refuses it, while the straight between the two lines is half a
carriageway inside the tarmac the whole way. Asked of the edges alone the corner was refused outright and
the lane, with nowhere to go, capped on its own other edge at the mouth and the outside stopped dead.

## The probe lists the lines that pass, not only the ones the shell used

`--bench shell --at X Y` also names every driven line running through the place, its width, its length, how
much of it the shell found to be the outside, and where it starts and ends (`ShellProbe.Passing`). That is
what says a corner had a line to follow and did not take it, and — at the T junction above — what showed
that it had none: both lanes end at the box, the two movements that pass the corner start at those ends and
run *away* from each other, and no line joins the two points the ring has to get between. A table of the
stretches alone cannot say that, because a line that is never the outside has no stretch to list.

## A corner is the movement that sweeps it, and a fillet is not a band

A junction is paved wider than its own movements (TER-5): the wedge between each pair of neighbouring arms
is filled back to an arc tangent to both carriageways. That fillet is driven ground **no line lays a band
on**, so the movement that sweeps a corner had its outer edge covered — not by another lane but by the
corner itself — and read as covered it was the outside nowhere. Every junction corner in the town was left
to no line at all, and the ring jumped it: at one T junction both arms *end* at the box, so the outside came
down one lane, struck out across five metres of nothing and picked the next lane up. A perimeter is marked
on the town's driven lines and nowhere else (OBS-2p); a length of it standing on none of them is the one
thing it may not be.

**A band's edge is now followed outward until something takes over** (`Walk.Clear`). Off a junction corner
the fillet is the last of the tarmac and the kerb is a step beyond it, so the movement inside it owns that
length of boundary; off a movement in the middle of a box the wedge between it and its neighbour is followed
by the neighbour's own band, so it owns nothing. **Which way out the ground ends is the whole of the
difference**, and it is why this is not the lanes' union — read as that, every wedge inside every box came
back as boundary and the town filled with lines down the middle of the road.

**The seam between two bands belongs to both.** Read as strictly inside either one, the walk slips between
two bays laid against each other and comes out the far side calling a bay in the middle of a row the
outside; a 25 m straight across a car park was that. It is read to the edge with the grace an edge is read
with everywhere else (`Kerbs.JoinedM`).

On Odesa the corner at 1028,419 is now carried by two movements end to end, each handed on at a solved
crossing with nothing drawn between; across the town seventy more hand-overs meet at a crossing, seventy
fewer are joined across a cut, and sixty metres less straight is drawn. It costs three seconds of the
fifteen a city's shell takes to lay, which nothing in the tick pays.
