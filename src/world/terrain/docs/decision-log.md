# Terrain — decision log

## 2026-10-26 — the walk is struck off the walked roads, and an intercity road has a shoulder

**The owner: "intercity roads must not have parkings or sidewalk (meaning also no connected buildings)"**, and, asked how
under `TER-7b`: **"just dont generate it there, dont cut it afterwards - no sidewalk extrusion".** That changes `TER-7b`,
a `P0`, at the owner's word: the pavement is the ground within one distance of the kerb of the walked roads, struck off a
shell of their own (`Paving.Walked`), and no longer of the whole driven ground. Striking it wide and cutting it back, or
drawing its stretch beside an intercity road in another material, was offered and not taken.

- **A shoulder is a layer of its own** (GEN-75): the unwalked roads' shell moved by `ShoulderWidthM`, under the walk, so
  it shows only where no walk is struck — and `GroundShapes.At` answers it, so the picture and the answer stay one line.
- **One asphalt for every road.** The darker chip-sealed asphalt generated for intercity roads was, at the owner's word,
  "new coloring for all roads — just because it looks better, not need to separate roads": it is the tarmac's two looks
  now (`pavement.json`'s c and d, kept from the same frame), the old two kept as trials.

## 2026-10-25 — a bridge is dressed from art of its own

**The owner asked for bridges drawn from new generated art: "new sidewalk, new road, new props along the way and
general props for bridge itself, also connectors to the road".** A deck had been one concrete band with a darkened
rim. Now (TER-3b.2) a walkway and a carriageway are strips laid along the span, a parapet edges the deck in place of the
rim, and each landing end has an expansion joint, newels and wing walls; lamps stand on the parapets and piers nose out
into the water. Twenty-one looks were generated, the first of each kind in `Bridge.json` used and the rest kept to choose
from (`raw_assets/world/terrain/ground/bridge/prompting.md`).

- **Strips and not world-anchored surfaces**, because a span is straight and a flag or a slab joint square across it is
  what a bridge looks like; the ground's surfaces stay directionless (the owner, 2026-10-24) and are not touched.
- **One array, a layer a kind, and the fittings sharing one layer as cells**: a corner files its surface in five bits
  with one taken for a fade, which leaves room for five more surfaces and not seven, and a browser's stage samples
  sixteen textures at most where the ground already took eleven.
- **The deck grew by a parapet either side** (TER-3b.1), and a generated one is sized off its own road's width and
  walk as a traced one already was, so the walkway is the street's walk to the kerb's width.

## 2026-10-19 — the track under the town's ground, its rails over it

**The owner set the stack** (`TER-7b` is theirs), "Tracks <- Road <- Rails": the railway's track is laid right after
the grass, so the walk and the road crossing it are laid over the whole of it, and its rails after the paint, over
them and their marks ([world/rail](../../rail/docs/decision-log.md)). A track on a bridge — over a river or a road — is laid with
the rails. The railway is ground of its own (`Ground.Rail`), legal to nobody.

## 2026-10-12 — a kind of ground is a permission and nothing more

**The owner ruled it: no actor is expected to touch water, and a wheel tells the bridge from what it crosses by the
lanes it is laid on, not by the ground's shapes.** So the catalogue keeps the two surfaces a wheel stands on — the
carriageway's and the grass's (`GroundCatalog.Paved`, `Grass`) — and each kind's permissions; water's grip, drag and
mark threshold, every other kind's surface figures, and the point's coefficient on `GroundSample` are gone, none of
them read by anything but tests. The level above's lattice (`GroundShapes.At(point, level)`) went with them: it was
laid at load for a bench alone. The level above is still drawn (`Paving.Above`, `GroundMesh.Above`); what the ground
is at a point is the ground level's answer.

## 2026-10-07 — the ground is a wheel's, read off the lane ribbons

**The owner ruled it: a wheel asks which lane ribbon it is on, and nothing else asks the ground for a car.** On any way the atlas holds under it a wheel grips as on the carriageway, on none as on
grass (`GroundLocator.UnderAWheel`), and the same reading puts the car's body on those ways at p0 (TER-4c.2) — where
a car's grip and its reservations had been two readings of the ground every tick, the shell's under each wheel and
the atlas's under its collider. **The car itself does not care what ground it is on**: no figure for the ground under
it is kept, and its driver plans on its own rubber (agents/car log). `TER-7b`'s first consequence (`P0`) was
reworded at the owner's word, from *what a wheel stands on is the shell's* to this; `TER-7` and `TER-2` say what
follows.

- **Read with the body's covers and kept** (`CarView.WheelsOnAWay`, a bit a wheel, the world's beside the body): the
  tyres grip on what the step's reading left, and nothing else asks what ground a car is on.
- **A wheel is read at the lattice point nearest it**, so where it stands is answered to within the lattice's reach
  of a band's edge — 0.35 m at the shipped step.
- **What the ribbons do not cover is grass to a wheel**: a pocket of paving inside the kerb, a car park's apron, the
  pavement of a town nobody walks, and water.
- **The two grounds are worked out once** for the figures as they stand (`WheelGrounds`) and picked by a wheel's bit,
  on the host and in the kernel alike — the device is laid the two as rows, mark threshold and all, and hands no
  wheel's ground back.
- **The device is handed the bits** with each car's tyre row, read where it reads the body's covers
  (`world_covers.comp`), so the tyre pass binds neither the atlas nor the shell.
- **Nothing on the device asks the shell**: the kernel that read it under each car's middle (`car_ground.comp`) went,
  and with it the shell's tables laid on the device and the query over them (`DeviceGround`, `ground_at.comp`,
  `--bench deviceground`), which tyres and that middle were the only askers of.

On OdesaOsm 62 of 40 052 wheels stand otherwise than the shell has them — 29 on the carriageway's kind and on no way,
29 of 245 on the pavement's and on one, 4 of 23 on grass and on one (`--bench deviceatlas`). With the wheels read
off the ribbons, the whole fleet's tyre pass went 4.93 → 2.13 ms a tick on the host and 0.704 → 0.015 ms on the
device; with the two grounds worked out once and none handed back it reads 2.04 ms on the host and
0.018 on the device, and taking the rows in and handing them back 3.18 → 2.68 ms.

## 2026-10-04 — a bridge over a road is a level of the ground of its own, drawn above

**The owner asked for it: a bridge over roads is rendered above them, as a separate road.** `TER-7b` and
`TER-3c.8`, both `P0`, now say a bridge over other roads is a level of its own, at the owner's word. Until now the
deck's carriageway and the road under it were one driven shape, kerbed and paved round as a crossing, with stop bars
standing at its corners.

- **Each level's boundary is struck from its own lines** (`LaneShell`, `Paving.Perimeter`, `Paving.Above`). The
  ground runs on under every bridgehead by its rounded corner and a kerb (`SimConfig.UnderTheDeckM`), so the end it
  turns round, and the walk struck round that, stand under the deck drawn over them.
- **The level above's corners are filled and never cut** (`ArcOutset.Corners.Filled`): a bridge's end is square where
  it lands, and a corner rounded off there is a bite out of the road at every bridgehead.
- **No walk crosses a bridge over a road.** The walk is struck off the ground's boundary and a walker is on the ground,
  so a walker reaching a bridgehead turns round its end ([the known gaps](../../../../docs/index.md#known-gaps)).

## 2026-09-27 — a person is not affected by terrain at all

The owner ruled it: **walkers are not affected by terrain; it matters to tyres alone.** A walker's pace and
grip are its own on every surface, a casualty slides on its own sliding grip, and a person leaving a building
is set down on the nearest clear spot whatever the ground under it — so `TER-2`, `PER-3`, `PHY-8` and `PHY-7a`
say so, and `TER-7b`'s first consequence names a wheel alone (at the owner's word). No walker asks the ground
any more: every walker tick, every paused walker and every casualty took one ask each, and a blocked doorway
took up to thirty-three a tick.

## 2026-09-27 — the ground is answered off the shell

The owner ruled it: **the ground is the shell.** What the ground is at a point is which side it is of the rings
the picture fills (`GroundRings.Carriageway` and `Walk`), asked through a lattice over their pieces (`RingSides`,
`Terrain.ShellCellsAcrossGridCell`): a cell no ring crosses is a lookup of the winding at its corner, and a cell one does
cross walks two legs from that corner to the point. The answer is the rings' own, so the corner where the
square bands and the rounded fill parted, and the wedge a junction's corner is paved back over, are no longer
a gap — the answer is the picture. `TER-7b`'s first consequence was reworded at the owner's word, from which
line lays a point and how far off the kerb it stands to which side it is of the layers' rings.

The bands made an ask pay for every lane and movement within a walk of it. Asked per kind on Odesa
(`--bench census`), carriageway went 549 ns → 32, pavement 372 → 74 and grass 62 → 37; on a running Odesa the
tyres went 1.21 → 0.29 ms a frame and the main thread 3.75 → 2.8 ms (`qq prof`). At 4 m a layer is 2.4 MB with a fifth of
its cells crossed; at 2 m it was 9.9 MB and no faster, an ask being bound by the fetch and not by the pieces.
The ways into a bay went with the lanes — a set empty in every town. The generator asks the same ground where
a prop may stand, so the corners the rounded walk gives back to the grass are furnished: Odesa lays 68 855
props where it laid 68 772, and nothing else it lays moved.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- `TER-3c`: the band is the driven ground's boundary moved by `WalkOuterM` with the carriageway laid over
  it (`GroundRings`), not ground stamped by what nothing else claimed.
- `TER-7b` (`P0`, reworded at the owner's word): only the pavement and the driven ground are struck off the
  boundary — the grass is the world, the water the plan's own shore, a deck a ribbon, a slab a rectangle —
  and the order names the slabs where it named "what the blocks take back". A block is a hole read off its
  ring's winding and takes no pass of its own (`GroundMesh.Build`, `ShellFill`).

## 2026-09-19 — the cost of a ground answer is a figure per kind, and a road that answers nothing is not asked

One figure for what a question costs hid the only thing worth knowing about it. Asked per kind
(`--bench census`), the town answers **grass in 60 ns and carriageway in 555** — nine times as dear, and five
times the mean every judgement had been made against, since a wheel's ask stops at the driven bands and a
walker's carries on through the water into the walk. The rows carry the figure now and the headline is their
mean. **Each kind is timed by asking again**, a strided quarter-million of it in a tight loop, because a
timestamp costs a fair share of a 130 ns ask and a kind lies in bands across a town.

**Four roads in five were in the index to answer no.** A road says two things about a point, a deck and a
stretch of paint, and a road carrying neither is a candidate projected onto for nothing — Odesa carries 510
crossings and no bridges over 2 054 roads. Indexed only where a road can answer, a carriageway ask went
689 ns → 555 and the mean 136 → 111. **And the working set is no longer zeroed**: `ChainIndex.Near` fills every
slot it returns, so initialising the frame was 6 to 15 ns of every ask, and room nothing fills now costs the
stack pointer — which is what lets the fallback that walks every driven line sit behind a cap of 256.

## 2026-09-06 — the carriageway ends at the pavement's inner edge, pockets included

The band's outer edge was a dilation, which rounds a convex corner and swallows a narrow notch, while its
inner edge was the tarmac's raw outline, which does neither — so wherever the town's pieces met at different
widths the band came out a different width at every mouth, and the kerb read as a chamfer across a corner the
walk turned smoothly. Everything inside the kerb is tarmac now, pockets included (TER-3c.7).

## 2026-09-06 — a band ends square where its own line ends

Measured radially a band ended in a half-disc of its own half-width past the last point: the answer at a
point said square and the tarmac's own outline said round, and the round end took the pavement corner's own
line away at every mouth (TER-7a). It ends where its line ends, square across.

## 2026-09-03 — the cell grid is gone and the ground is solved against the shapes

A map carried its shapes *and* a one-metre classification of them, with TER-7 stating the disagreement as
half a cell — largest exactly where it mattered, on a corner where no arrangement of metre squares is a kerb
at 40° and a tyre's contact patch is 0.2 m. It fed back into traffic, since a claim goes as v²/2a and a
misclassified cell lengthened a held stretch quadratically. The ground is solved against the shapes now.
`Ground.Footway` and `LaneDirs` went — 13.8 MB on Odesa with no production consumer — and the generator keeps
no ground raster either, which is what lets a half-laid town be asked what is where. It cost 900 ms against
384 to lay Odesa and about 300 ns a question against 16–21 ns, which is 4 % of a core.
