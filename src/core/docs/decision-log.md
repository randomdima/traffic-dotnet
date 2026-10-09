# The kernel — decision log

## 2026-10-10 — a kernel's sine and cosine are the host's to the bit

**A float or two apart was a different town.** A shoved car's heading a float off the host's left a contact 34 µm
deeper after the push, so the pair froze a step later than the host's (`SOL-37`, `DeviceTownBodiesTests`), and 10 856
of 136 851 switching cars' looks on OdesaOsm came out a figure apart. `MathF.Sin`, `Cos` and `SinCos` are glibc's
`sinf`, `cosf` and `sincosf`, which on x86-64 with FMA run `__sincosf_fma`: reduced and summed in doubles, every sum
fused with the product before it, rounded to a float once.

- **`trig.glsl`'s sine and cosine are that code**: its tables to the bit, its sums fused where that build fuses them —
  read off its disassembly — and the large arguments' 64-bit reduction taken in 32-bit halves. Every kernel that includes
  it computes in doubles, which every kernel of the device's tick needed already (`DeviceAtlas`).
- Held: `TrigKernelTests`, 2.1 M angles — a heading's worth, every exponent, every edge of the reduction — the host's to
  the bit on lavapipe, and 4.2 M on the RX 9070 XT. An unfused reduction fails it; an unfused last sum or a coefficient a
  double apart does not, since rounding to a float hides them.
- The shoved car park, the town ticking its agents and the device stepping its bodies, now stands to the bit as the
  host's for its whole half minute, where it parted by millimetres within seconds. OdesaOsm (`--bench devicedecide`): 19
  of 5.5 M verdicts unlike where 22 were, and 479 looks alike but for a figure where 10 856 were — the arctangent's,
  still Cephes'. `--bench devicetown`: the device's kernels 1.71 → 1.78 ms a tick, the tick 11.67 → 11.69; the town
  drives nearer the host's — 8 949 hard brakings where it braked 8 899 (the host's 8 984), 388 pairs beginning to touch
  (387; 388), 223 lane switches asked and 195 made (220 and 190; 233 and 207).
- **Only against the host it was read off**: another C library, or glibc on a machine without FMA (`__sincosf_sse2`),
  rounds otherwise, and the device stands within a float of it as before.

## 2026-10-07 — a kernel's trigonometry is its own, not the driver's

**Vulkan lets a driver's atan stand 4096 floats off and bounds sin and cos only absolutely**, and RADV's stand about a
part in a hundred thousand off. An arc is read about a centre a radius away and sampled along a chord, so the error comes
back multiplied: on OdesaOsm's bends of 8.6 km a car stood a centimetre from where the host read it on its line, and its
lead point half a millimetre off at the end of a 500 m chord, which the pedal — closing a speed in one tick — turned
into a hundredth of a m/s². **The arcs, the line, the driver and the tyres take theirs from `geometry/kernels/trig.glsl`**:
Cephes' minimax polynomials for atan, within a float or two of `MathF` on every driver alike, and `precise` so none
fuses them; its sine and cosine are the host's own (above). Of 2.93 million car-ticks the device ran beside the town
(`--bench devicecartick`, RX 9070 XT), 702 answered differently with the driver's own and 21 with these; the tick costs
0.13 ms more. Android's drivers bound their trigonometry no tighter than Vulkan does, which is the other half of why the
kernels carry their own.

## 2026-10-06 — phase 3 is every agent's tick, then every decision

`SimLoop` ran one agent's tick and its decision before the next agent's tick, so a decision read a roster ticked up
to itself and no further. **On the device 3a runs for every agent at once** (root log), so the loop runs it that way
already: all the ticks, then the decisions due. What a car's tick used to decide moved onto its decision first (the
car's log), the exam and OdesaOsm's claims came out as before, and the timing stamps a crossing between walkers and
cars on each pass (`TownWorld.AccountFor`).

## 2026-10-04 — a place is read onto an all but straight bend from its start

**A traced road normalised into the roundest arcs (GEN-57) lays bends tens of kilometres round**, and read about
the centre (`Spline.NearestOnArc`) a place on such an arc comes back millimetres off: `OdesaOsm`'s 420 m roadside
on a bend of 64 km read its own end 2 mm short, so the band's square end read as alongside it, the merge dropped
it as covered and 1.1 km of boundary was left open (TER-7b). **A bend flatter than the crossing arithmetic's own
flat (`Spline.FlatCurvature`, ten kilometres) is read from its start now** — its first heading, put right by
Newton's step (`NearestOnFlat`) — to a fifth of a millimetre over the whole of it; a rounder one is still read about
its centre, for the reason the remark there gives. Every shipped city's boundary closes. The suite's crowded town
first touches at three minutes rather than a minute and a half, so the contact gate runs four.

## 2026-10-03 — a band's own end is asked against the rest of its line

**The merge asks each band once about a place, at the nearest point on its line** (`BandShell.Merge.Reading`),
and off a square end that point is the end itself, which says the place is outside. A band folded back through
its own end is not: `OdesaOsm` laid a service road that hairpins into a dead end inside its own leg out, and
the end's stretch lay 0.86 m from the end and 0.863 m from the leg it stood in, so which was asked was a
millimetre's luck and 1.8 m of boundary was left open (TER-7b). **A place off a band's own end is now asked
against the rest of its line** (`InItsOwnFold`): only beyond half the band's width from that end, so the band's
own ground beside the end is never read as a fold, and only where the line's square lands strictly inside a
piece. **A distance to the nearest piece was tried first** and opened 44 runs, every one at a bend just before
a dead end: inside a bend tighter than the band is wide, a place within half a width of the line is not on the
band, which is the normals off the line and not discs round it.

## 2026-10-01 — the tick is the bodies, the input and the picture

**The owner set it: an actor's behaviour runs on nothing per tick.** What the tick is for is the physics, the
player's input and what is drawn; a decision, a look round, a patience run out and the claims are taken on clocks
of their own — the decision clock, and the claims laid every `Sim.ClaimsIntervalS` (TER-49). What stays the tick's is
what a body does with what it already holds: the wheel and the pedals at the grant walked in, the reflex that brakes
for a hazard, a manoeuvre's or a pass's piece ended where it is driven to its end, and an ask answered in the laying
after it. The decisions are the car and walker slices' logs, and the claims the road slice's.

## 2026-10-01 — an index walks each chain once, on every core

**`ChainIndex.Builder.Seal` was the largest serial cost of opening Odesa**: 29 indexes and 175 ms, every
chain walked twice on one thread — once to count its cells and once to write them — with a mark array the
size of the lattice to keep a cell to once a piece. **Each chain is walked once now, in chunks on every core**
(`Builder.CellsOf`), a piece's cells kept once by skipping the last sample's and sorting out the rest, and
the table is counted and filed off those lists in slot order. All 29 tables are the same bytes as before and
the plan's digest is unmoved. Interleaved, eight opens each: the plan's ground 332 → 279 ms, the foot
graphs 186 → 157, the open 1593 → 1507. What is left is the counting sort over the lattice itself: the merge's
index at a metre a cell is 6.6 million cells for 27 000 pieces, and 14 ms of its 34.

## 2026-09-28 — a ring is joined again once its joints meet

**A straight kerb meets the kerb across a junction a few centimetres short or long**, which is outside the
millimetre a join is measured to, so `ArcRings.Joined` left the two apart. `Tightened` then closed the
joint, and the result was one straight cut in two. With grid streets laid straight (citygen, GEN-47),
that joint is on every street through every junction, and the fixture and the suite's city carried 2 and
8 of them. **So a ring is joined, tightened, joined again and tightened again** (`ArcRings.Closed`). The
second join is ringwise and exact to what the first allowed, and the last tighten closes the joins it made.

## 2026-09-28 — a town past 8 km is worked about its middle, to tolerances a float can hold there

**A millimetre is a float's own step at 8 192 m and two of them at 16 384 m**, and the boundary's figures are
a millimetre or two (`LineTolerance.RoundingM`, `BandShell.Merge`). Odesa's brief tripled each way lost its
carriageway past 8 192 m — the outset left every ring there open — and a town thirty kilometres long at
Odesa's density came back with 1 249 of its merge's runs open and no carriageway at all. Four changes, and a
town inside 8 192 m is answered to the bit as it was:

- **An arithmetic tolerance grows with the distance from the origin** (`LineTolerance.At`): two of a float's
  steps where that is wider than the figure. Four were tried and left twice as many runs open again, a wider
  figure keeping folds as shallow as itself.
- **The merge and every move off it are worked about the shape's middle** (`BandShell`), so nothing is
  computed further out than half the town. The merge's own tolerances are the coarser of where a place
  stands in the world, where the lines it weighs were computed, and where it stands about the middle, where
  it is weighed: read about the middle alone the open runs went 27 → 70, and read in the world alone a road
  beside the world's origin — fourteen kilometres from the middle — lost its outline.
- **Where two places far out are compared, the difference is taken before the sum**: a crossing of two
  pieces is solved about the first one's start (`Spline.AlongBoth`), and the fold test measures off the
  piece's start (`ArcOutset.FromThePlace`). Two readings of the chord a corner is swung on were tried the
  same way and made it worse: the end a ring's next piece starts at is the one to read.
- **What a merge or a move still leaves open is shut across its holes for whoever wants the ground**
  (`ArcRings.Shut`), and handed back as the fault it is as well.

The thirty-kilometre town's merge went 1 249 → 25 open runs and its carriageway 236 → 12, and the ring round
its outside, open by 3.5 m in a car park at the town's far edge, is shut across it. Odesa's brief ten times
over each way leaves 323, 103 and 48 of its merge's, carriageway's and walk's runs open among 280 000 roads,
every one of them shut for the ground, and not a prop of its 6.8 million stands on a lane.

**A lattice word is a long** (`RingSides`): twenty-four bits of filing are sixteen million, and that town
files fourteen million pieces of its carriageway.

**A hole is joined to its ring off the edges near it** (`ShellFill.EdgeRows`): the join walked the whole ring
for every hole, and a hundred thousand blocks in one ring of millions of corners had not filled in a quarter
of an hour. The nine layers fill in two minutes now. **Where two corners tie, the ring is walked as it was**,
so every shipped map fills to the same triangles.

**The merge's cuts are counted and then written, a run a piece** (`BandShell.Merge.Filing`): held a thread
at a time and then filed a list a piece, the ten-times town's 1.2 billion cuts were held twice at once, and
opening it peaked at 53 GB. It peaks at 42 GB on the same cuts, and the merge crosses every pair twice to
count them first.

## 2026-09-18 — a rounding is the move run again, at a radius in metres

**The guarded smoothing was a figure nobody could set.** It trimmed each corner by the tangent its turn asked
for, put a biarc across the gap, and refused the fill where it turned further than what it replaced or read
nearer the shape than the distance. Asked of a shipped city's boundary at a metre moved and a half share, it
kept 861 fills of 3 539 corners and left 434 of 1 024 notches — and it was not monotonic: at six metres a full
rounding left 706 notches where five and a half left 65. **It is the move itself now** (`ArcOutset.Of`): out by
`d+r`, in by `2r`, out by `r`, the third skipped where `r ≤ |d|` because there it is the identity. Four hundred
lines of guards went, and the same city came back with 161 notches of 1 024, the sharpest 11° against 142°.

**The radius is a length and not a share of the distance.** Tied to the distance, a line struck a hand's
breadth off a car park had a hand's breadth of radius, and a shape struck at no distance was handed straight
back. Untied, `--bench outset` crosses distances with radii: at nought moved a city's boundary goes from
1 882 corners left at 180 degrees to 8 at 4 as the radius goes 0 → 8 m.

**Past the distance it cuts the corner, and that is stated rather than guarded against**: no construction
rounds a corner turning away without coming nearer the shape. The one-sidedness a kerb needs holds wherever
the radius is inside the distance. **Eight metres of radius is still a coin toss** — a few dozen joins come
back at 180 degrees and a handful of runs open from two metres moved up, the inward move of 16 m leaving a
town's shape in slivers — and the sweep prints it rather than hiding it.

## 2026-09-18 — three corners of the offset were solved wrong

Running the construction at distances the town never asks for found three faults:

- **A piece the move turns inside out was dropped with its two corners** — four two-metre holes in a city,
  both ends at exactly the right distance. It is kept now, as the arc of `|R−d|` walked the other way.
- **A corner's round was bent by the turn's sign rather than the move's.** Where a piece is a hand's breadth
  of tight bend the chord and tangent readings disagree, and the arc was struck about the corner's mirror
  image — spikes a metre inside a pavement, and a walking lane of 656 m between two points 3.8 m apart.
- **A ring closed across a graze was handed back as a hole**, because whether it was shut was read at a weld
  after the walk had paired ends a third of a metre apart. A ring walked back to its own first stretch is
  closed.

## 2026-09-17 — a chain is refused by its box, and a parallel pass is handed chunks

**`ChainIndex` refuses a candidate by its own box before projecting onto it.** The cells are the lattice's
scale and not the question's, so most candidates cost an `Atan2` and a `Sinc` a piece to learn they were
nowhere near. `Spline.ProjectM` fell from 2 626 ms of an open's CPU to 1 655, and the shipped city's tick from
3 262.6 µs to 3 116.0. It is not the floor refused below: a box is two-dimensional and the whole chain's,
which is exactly the case a per-piece bound on chains of one or two pieces could not reach.

**`InChunks.Over` replaces `Parallel.For` on build passes**: 1 171 ms of CPU in the ground stage was the loop's
own per-iteration bookkeeping, and chunks took it to 80. It bought no wall clock on sixteen cores and is kept
because a handset has fewer to waste.

**Every build-time pass takes the shape "ask wide, file in order"** — the pavement's ground veto, the junction
crossings, the props in the road, the car-park cuts. The question is of standing data nothing writes to; the
answer goes into a slot per item and is filed in the walk's order, so no pass rests on its items not
colliding (`RibbonAtlas.Lay` files every thread's marks under their ways, in order, for that reason alone).

## 2026-09-16 — a query takes a scan, and the merge is asked a piece at a time

**Opening Odesa cost six seconds and half of it was the merge**, a third of the open in `Spline.ProjectM`
(`--bench load` now prints the stage). Nothing below moves a figure: the boundary, rings, lane offsets and
every count `--bench outset` and `--bench census` print are identical on all three maps before and after.

- **A projection hands back how far off it landed**, as the loop's own float — a caller measuring back from
  `SampleAt` walks the chain again and can land on the other piece of a joint.
- **A floor that skips a piece before measuring it was tried and is not kept.** The chains asked are one or two
  pieces, the ground's ask read 106–107 ns either way, and what a projection must never buy speed with is the
  measuring: a nearest that moves moves which lane a body snaps to.
- **The merge's index is of pieces at `ChainIndex.FinestCellM`**, not of ribbons at the lanes' lattice, which
  handed a place every piece through seventy square metres of junction. The merge fell 2 700 → 2 200 ms.
- **A query may name its own working set** (`ChainIndex.Scan`), so two threads can ask at once and the merge
  runs its three passes on every core, holding its writes and filing them in order (`Merge.Held`). Laid five
  times, and once on three cores instead of sixteen, the city comes back byte for byte.

## 2026-09-16 — a line beside a line is an offset, and an offset has corners

`Spline.OffsetInto` moves each piece whole, which is the answer only for one smooth curve; at a corner the
moved pieces gap or cross. `ArcOutset.Beside` finishes it for an open line with the construction a ring
already had. **Whether a joint is a corner is asked of the two moved ends and not of the angle**: read off the
angle, Odesa's pavement came back with cuts of up to 24 m at joints turning a thousandth of a radian; read off
the ends, with none.

## 2026-09-15 — a ring is handed back with its pieces meeting

**A frame showed the boundary break**, and the joints reading said why: 1 826 of the shell's 5 757 joints
stood open past a rounding, 779 of them holes. The gap is the walk's own — a cut passes over a piece shorter
than the weld — so closing it is the walk's too: `ArcRings.Tightened`, which `ArcOutset` had kept privately
since a corner struck at exactly the distance has no headroom for two vertices a centimetre apart. Over Odesa
the open joints fell 1 826 → 5, the holes 779 → 0, the walk's outer face 384 → 0, and the fill's net loss
−131 m² → −120. `LaneShellTests` probes at half a weld since, and `ArcSubtract` is told twice `LeastLostM`.

**Tried and not kept**: running the merge's last stretch to the end of its piece (holes to 253, but a stub of
boundary left inside the shape); weighing the tail on its own middle (35 runs open); closing the joint onto
the crossing the two pieces share (reads better, measures worse — it moves a joint by the whole gap).

## 2026-09-15 — which side of a corner a place is on is not a question one piece can answer

A subtraction and an offset both asked the single nearest piece, which is a coin toss wherever the nearest
point is a corner — and `ChainIndex.Nearest` projected without clamping, so a long arc's circle could beat
the piece a place stood beside. **Both now sum the readings of every piece tied for nearest**, the 2D
angle-weighted pseudonormal, exact rather than heuristic. The fixture's kerb had two runs open at 30.9 km and
the generated city seven at 4.5 km; the sum closed all of them.

## 2026-09-15 — which way a corner opens is read off the chords

One hole survived the sum: a 0.628 m half circle at a cusp, where two arcs meet with tangents exactly opposed,
the cross product is nought and `Atan2` returns +π whatever the shape. The chords the pieces subtend differ
from the tangents nowhere but at a cusp, and there they are the only reading left.

## 2026-09-15 — a merged ring's area carries its open joints

A fill of Odesa covered 1 868 m² more than its boundary enclosed, at any sag. `Spline.EnclosedM2` summed each
piece's own chord and so left out the ground under every open joint; it now carries the closing straight as
a second term, and the fill's net loss reads −13.8 m², 0.003 %. `--bench fill` prints the joint count and the
worst of them above its tables.

## 2026-09-15 — cutting a shell and shaping it are two questions

**The fewest triangles answered only the first.** The clipper took long boundaries off as fans of slivers, and
a corner sixty-four triangles meet at lights as one point. Delaunay flips after the cut change no count; over
Odesa's 26 241 triangles they took slivers under 5° from 84 % to 21 %, the busiest corner from 64 edges to 12,
the longest edge from 690 m to 60.

- **The hub pass that followed is gone.** Thinning the boundary removed the corners the fans grew from, so it
  found two edges' worth of relief for a whole traversal; and on a perfect disc, already Delaunay, it still
  left fifteen.
- **The circle test is two angles, not the determinant.** Squared town coordinates put a tie in the last bit
  of a double, and a straight-sided strip is cocircular by construction: the pass oscillated.
- **The work list reaches one ring past the turn**, or several hundred edges of a city stay quietly illegal.

## 2026-09-15 — an index counts the chains it holds, not the chains it was offered

`BandShell.Of` checked `ChainCount` equal to its lines, but `ChainIndex.Builder.Add` drops an empty chain —
and the idle ring, half of whose driven lines are empty, crashed the game's start map. It is a bound now.
The ring stays unswept on purpose, being a frame for the menu; a unit claim on the merge's contract catches it.

## 2026-09-15 — a shell is the solid, and filling one is the second thing it can be asked

**The construct had no name, so every reader named it after its own input** — `BandShell` for what made it,
`Paving.Perimeter` for its edge. The word is *shell*: the region, whose boundary is one question
(`ArcOutset` the same shell a distance away, `ShellFill` the triangles over it).

**The fill is earcut, written down as such** (Mapbox, ISC): ear clipping, holes bridged at their leftmost
corner, a Z-order index, and the two recovery passes a pinching ring needs. A clipper written from the
problem's shape would have been the O(n²) scan the water rings are cut by, which a city's boundary turns into
a lunch break. Every predicate is `double`: a float has two figures left after these differences of products.

## 2026-09-15 — a moved piece is a cutter, so nothing is cut back before the cut

**Solving the closing corner at the corner** solved it twice — once there and once as a fold crossing — and
trimming the pieces took away a cutter: a shipped city's boundary came back with sixteen holes of a third of
a metre, each with both ends at the right distance and each taking thirty kilometres of ring open. Two cuts
nearer than the shortest walkable stretch are one cut. **What a corner can be short is the walk's figure**
(`√(2·d·ε)`, a third of a metre at five metres and a centimetre — the width of the holes measured), so
`ArcRings.Of` takes what its caller can lose. **Widening the fold test instead was tried**: nothing is cut where
that reading changes, so it keeps slivers of fold with ends in mid-air. Every fault here was found by measuring
(`--bench outset`, `qq town --outset`), none by reasoning.

## 2026-09-15 — a mitre is not an offset, and two things were hiding behind it

**The corner of an offset is the arc of the distance about the corner, and nothing else is**: a mitre stands
`d/cos(θ/2)` off and turns sharper than the shape did, which is a fault in a line something is laid along.
Taking it away surfaced two defects: the ring's centimetre gaps, which took twenty-four kilometres of ring open
at three corners and are closed by the walk now (`ArcRings.Tightened`); and **a distance test that says
nothing about a place inside the shape** — the arc round the far corner of a filled slot sweeps into the solid
ground behind it — so the test asks which side too. A slot's mouth leaves the scallop `2·d·asin(w/2d)` and
not the straight `w`; the test that said otherwise was reading back the mitre's own error.

## 2026-09-14 — the merge of bands is the kernel's, and an outset is what it was missing

The merge asked for chains, widths and an index and never knew what a lane was, so it is `BandShell` in
`core/geometry/`; what stayed in `citygen/` is which lines the town hands it (`LaneShell`). **The examples in
its docs name the shapes that decided its tolerances** and stay, or four constants in the kernel lose their
reason. What it could not do was stand off itself, and `ArcOutset` is that.

## 2026-09-14 — an offset is a fact about the area

**Moving the lines and joining the corners was written first**, and a notch narrower than the distance, a
courtyard smaller than it, and two rings closer than twice it each came back as a bow-tie. The corners are
local and the fold is not. **One question settles all three**: every moved piece is cut at every crossing and
a stretch is kept where nothing of the original is nearer than the distance. It is strung by the merge's own
walk (`ArcRings`, lifted out for the two to share).

## 2026-09-12 — a distance along a bend is read as a chord

**The closed form `2·atan2(…)/k` divides a microradian of float noise by the curvature.** At `<2000, 1500>` a
crossing on a bend of 200 km radius came back half a metre off, one of 2 000 km four metres off, and the
shallowest found no crossing at all — and every ribbon edge a town lays is such a bend. The chord over the
`sinc` of the half turn carries no such factor (`ArcSeg.PointAtM` read backwards). **Its sign is the chord's
too**: signed off the half turn, a crossing plainly ahead of a near-straight piece read as behind it. The two
took Odesa's open boundary 153 → 104 → 100 runs and River's 79 → 51 → 47.

**`NearestOnArc` still measures against the centre, and that was measured.** From the start it is a millimetre
better and moves which lane a body between two snaps to: the city's boundary closed less (18 open runs → 22)
and a rescue stopped arriving inside its bound. A nearest is not a cut.

## 2026-09-12 — the geometry grid answers "which lines could" as well as "which line is nearest"

The shell paired its stretches and the bay ways found their crossings by weighing everything against
everything — quadratic, on a build path. `ChainIndex.Around` and `Crossing` hand back the candidate superset
and measure nothing, so each caller's own test is unchanged. **The lattice is snapped to the map** so two
indexes' cells are the same ground (OBS-2r), and **a piece is binned by walking it, not by its box**: the boxes
held 1.11 entries per real cell over the city — a tenth as a count, and as a picture a three-by-three wash of
cells over open grass from one diagonal lane.

The shell probe came back line for line. The lay is a fortieth shorter (3 537/3 498/3 549 ms against
3 645/3 630/3 630, interleaved), from the tighter binning; **what is gone is the quadratic term**, so a town twice
the size now costs twice. Two orders were made total, since a tie settled on enumeration order was settled on
the lattice. **Figures across sessions on this machine are not comparable** — one `HEAD` read 3 093 ms and
3 630 an hour apart — so every reading here is interleaved against its own baseline.

## 2026-08-21 — an angle becomes a direction in one place, and a series covers the sinc

Trigonometry was 18 % of the tick, nearly all of it two shapes written out by hand across twenty sites. Both
are `Heading`, where the pair costs one `sincos` (3.11 ns against 4.47), and `ArcSeg.Sinc` answers by series
under 0.6 rad at 0.58 ns against 2.36.

## 2026-08-21 — a network is indexed because it cannot change, not because the scan was slow

`NearestEdge` and `NearestLane` scanned every stretch in the town — 7 % of the run on Odesa. Both answer from
`ChainIndex`, which is safe only because neither graph is written after it is laid. The index decides what is
looked at and never what is chosen, so ties still go to the lower id and `ChainIndexTests` asserts against the
scan it replaced.

## 2026-08-16 — agents stopped thinking every tick

Every agent re-ran its whole procedure at 60 Hz while 96 % of cars on a jammed town stood still, so agents
decide on the clock `AgentDecisionIntervalS` states. It is a floor and not a ceiling because that was
measured: a manoeuvre steering to a pose, run at a sixth of the rate, converged at a sixth of the rate.
