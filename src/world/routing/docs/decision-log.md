# Decision log — routing

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-05 — the travel graph is links and the turns between them, with no node table under it

**The coarse graph used to carry a node table** — an anchor per node, a from-node and a to-node per link —
and built its turn table by looking up the links leaving whichever node a link arrived at. Two things came
of that and neither was wanted. The graph could name an intersection, which is a thing this project spent
the same week deleting from the tier below it (`LanePlaces`, [world/road](../../road/docs/decision-log.md));
and the picture drew what the graph could name, so the debug layer put a dot in the middle of every
junction box and said, plainly and wrongly, that the router plans between intersections.

**It plans between ways.** A link now states which links it may be left for, exactly as a lane states which
lanes it may be left for, and where several of them meet is not a record anything keeps. The contraction
still works the joins out from where the fine network's lanes meet — that is what a place is for — and then
it is done with them, the way the plan's junctions are done with once the lanes are laid.

**The node table was doing one real job, and it was not identity.** Sharing an anchor between the links that
met at it made two relations true for free: a link is never priced below the span between its own two ends,
and the links of a route meet end to end. Together those are what make the straight line to the destination
an admissible bound, and A\* over an inadmissible bound returns routes that are dearer than the cheapest
without ever looking wrong. Split the anchor per link and the first survives on its own; **the second has to
be stated, so `Builder.Join` refuses a join between ends that are not the same point.** That is the whole
price of the change, and it buys a graph that cannot be asked a question about a junction.

**The alternative was to keep the nodes and stop drawing them.** It was not taken because it fixes the
picture and leaves the fault: a search state is a link precisely because the cheapest way *to* a junction is
not a fact about the junction, and a graph that still offers a node to settle is a graph where that bug can
be reintroduced by someone reading the type rather than the rule.
