# Terrain — decision log

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
