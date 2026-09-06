# Terrain — decision log

## 2026-09-06 — the pavement wraps the outside of the tarmac, and a band ends where its line does

**The walk was laid off every piece of tarmac, and a piece inside the box is not the outside of anything.**
Once a junction stopped being a shape, the ground inside a box became the lines cars are turned through it
on, and those lines went into the tarmac beside the carriageways and the fillets. Each offers the line that
stands half a walk outside it, each such line is kept where nothing stands nearer than that — and a
movement's line passed that test while running up the middle of a pavement the arms had already laid. What
the picture showed was two pavement lanes threaded between two more, a walk crossing from one side of the
band to the other and back at every mouth, and a stub of kerb dead-ending into the middle of the footway.
Odesa carried 406 dead ends in its pavement and 153 nodes where four stretches met; 46 stretches lay wholly
inside another one's band.

**A piece now says whether it is the outside of the tarmac or the inside of something** (TER-3c.5), and a
line the inside offers is kept only where it *leads somewhere*: joined to the rest of the walk at both
ends, which is what closing a gap the arms left open looks like. Dead-ending, it is dropped. This is one
rule about a shell rather than a case about junctions — nothing in it knows what a box is — and it took
Odesa to 82 dead ends, 0 four-way nodes and 0 doubled stretches.

**Dropped in the graph rather than refused at the wrap, because the two are not the same question asked
twice.** Whether a line runs alongside one already laid cannot be settled by measuring how near it passes:
a line that closes a gap is near the two ends the gap is between, and one that duplicates a pavement runs
past a place where two kerb lines meet end to end, which reads the same. Whether it leads anywhere is not
a measurement at all — it is what the graph says once every line is in it.

**And a band of tarmac ends square, where its own line ends** (TER-7a). Measured radially from the last
station it ended in a half-disc of its own half-width, which is tarmac nothing draws and nothing drives
over. Inside a box that never showed, because everything around it is tarmac too — but a movement starts
in the middle of the lane it leaves, so its disc reached out past the arm's kerb, under the pavement corner
at the mouth, and cut the corner's own wrapping line in half. The corner then had no line at all: two
metres of pavement missing at a junction of two streets, on every map. `GroundShapes` had always answered
square; the two readings of one band now agree.

## 2026-09-03 — the cell grid is gone and the ground is solved against the shapes

**There were two answers about one ground and they disagreed by design.** A map carried its shapes *and* a
one-metre classification of them, and TER-7 stated the disagreement between the two as a tolerance: half a
cell. Everything the town actually reads — grip under a wheel, whether a body may stand somewhere, how much
road a driver claims — read the classification. So the error was largest exactly where it mattered most:
on a corner fillet, where the lateral load is highest and no arrangement of metre squares is a kerb running
at 40°. A tyre's contact patch is 0.2 m against a tolerance of 0.5 m.

**It also fed back into traffic.** A car's grip came from the single cell under its centre, and the
stopping distance that sizes a claim goes as v²/2a — so a misclassified cell lengthened a held stretch
of road *quadratically*, and a raster artefact became a traffic artefact.

**The answer is now solved against the shapes, in the reverse of the order they are drawn.** Ground is
drawn by laying each piece over what is already there, so what a point *is* is the last piece laid over it,
and the last piece laid is the first one met walking that list from the end. `GroundMesh.Build` and
`GroundShapes.At` are one list in two directions; there is no tolerance anywhere, and the question
of whether the drawn ground and the answered ground agree can no longer be asked.

**Nested, because roads cannot overlap and lanes can.** The road level answers which road a point is on —
`ChainIndex`, the same bucketed nearest-chain index the router already uses — and projects onto it once.
After that a carriageway, a walk, a deck and a zebra are *intervals* in a frame with the bend taken out of
it, and a crossing is two distances along the road it is painted across rather than a rectangle standing in
the world. Curvature is paid once a query instead of once a feature. Everything that belongs to no road —
junction discs, kerb fillets, the pavement's own inner corners, car parks, paving slabs, the water — is a
shape over a bucket grid.

**A zebra is asked about first because it is struck last.** Under the cells a crossing converted only cells
that were already carriageway, so a crossing whose band lapped into a junction's disc simply stopped being
one — and the walking network had stretches of crossing standing on ground no walker was permitted on.
Drawn, the paint goes down over the disc like everything else; answered for in that order, it does too.

**`Ground.Footway` and the lane direction are deleted rather than kept.** Nothing painted a footway; the
kind existed only in the bytes of already-baked files. `LaneDirs` was two bytes a cell — 13.8 MB on Odesa —
with no production consumer at all: what a lane's direction is, is the lane's, and every caller that wanted
one already had a lane.

**The generator does not keep a raster either.** It kept one at first — a scratch grid the layout stages
read to decide where a building or a bench may stand — and that was still two descriptions of one ground,
agreeing to within a cell. So the shape query was split in two: the *geometry* lives with the plan
(`CityGen.GroundShapes`), which is what lets a half-laid town be asked what is where, and the *permissions*
stay above it (`World.Terrain.GroundLocator`), because what a kind of ground allows is a rule about agents
and the plan does not know what an agent is. `GroundPieces` is what a stage hands over: the shapes laid so
far, and `None` for the rest.

**Every collar the placement stages kept comes off with it.** A prop was cleared against the raster and
then held a margin back from it — the whole radius the pavement turns its corners on for the wild sweep,
because nothing had painted those corners in. The corners are in the answer now, so a candidate that clears
it is clear, and GEN-6a is the whole of the rule again.

**It costs the generator time.** Odesa laid in 384 ms against the raster and 900 ms against the shapes. Most
of the difference was one question asked the wrong way — *is any paving within seven metres* — which by
sampling needs a lattice fine enough not to step over a four-metre band, a hundred and fifty questions
apiece over a hundred thousand candidates. Asked of the shapes instead (`GroundShapes.PavingWithin`) it is
one index query, and that alone took 3.5 s back to 900 ms. What is left is the honest price of an exact
answer, and it is paid once when a town is laid.

**It costs about 300 ns a question on Odesa** — the census sweeps the whole town and prints the figure —
against the 16–21 ns a cell lookup cost. That is 4 % of a core for four wheels on five hundred and fifty
cars at 60 Hz, and it sits inside the 750–900 ns a moving body already pays `GroundUnder.At` every tick.
The road level is nearly all of it: one projection onto a road's whole curve per candidate, which is the
same arithmetic `RoadGraph.NearestLane` has always paid. What would take it back down is the fast path a
body already on a lane does not need — it holds its lane, its progress and the arc, so a wheel's offset is
a dot product — and that is a change to the physics rather than to the ground.

**Format 4 drops both blocks and there is a writer now.** The two fixtures still carried as files were
re-baked through `TownWriter`, which the format never had — a `.town` that could be read and not written is
a format that cannot move. `Test.town` went from 235 KiB to 28.

## 2026-08-29 — the pavement's inner corners are solved and no longer read off the map

**Every shipped map records fewer of them than it has.** The rule said the plan carried the list, and the
build drew from it: Odesa's file names 916 corners, and the ground it lays has around 1150. A scan of the
drawn pavement for sharp notches — a point of verge with more than seven tenths of a 2.5 m ring round it
paved — found 205 left standing on Odesa, 79 on River and 122 on the exam lattice, which records none at
all and was therefore square at every junction it has.

**The largest family is a shape the exporter never saw.** A car park's walk is a wrap the *build* lays,
`halfExtent + walk` rounded on half the walk (TER-3c.3), and where it runs into the street's own band it
leaves two right angles nothing had a record for — 112 of Odesa's 197 unrecorded notches and 49 of River's
69. The rest are roads meeting at angles the exporter's list skipped.

**So the build solves them, which is what the rule already said the corner was.** TER-3c.4 has always
held that a corner is a fact about the pair of shapes and nothing else, solved against the finished ground
rather than enumerated per kind of neighbour; the only part that has changed is who does the solving. Each
piece of pavement — a road's band, a junction's ring, a bridge's walk, a car park's wrap — has its outline
walked, and every crossing into another piece is a corner, measured off the two outward normals there.
Nothing in it knows a band from a wrap, so the day the generator puts a new pair together they are rounded
without a line moving.

**Signed distance is the whole of it.** Inside is negative, the outward normal is the gradient, and the
three kinds of piece differ in that one function — which is what lets the crossing search, the normals and
the spike test be written once each rather than per pair. A crossing becomes a corner only if the ground
round it is mostly paved: two pieces meeting leave a spike of verge or a corner of pavement, and reading
which off the ground itself is what keeps the answer independent of how either outline was wound.

**It is load-time work and it is not free**: 33 ms over Odesa, against 64 ms for the whole mesh. That is
the price of not enumerating, and it is paid once when the ground is laid.

**The file format still carries the field.** A shipped `.town` has the bytes and the reader-writer round
trip over every shipped map is what makes a plan a map rather than a second kind of thing, so the array
survives on `CityPlan` with nothing reading it. The census prints both numbers, which is where the gap
between what a map claims and what its ground has stays visible.
