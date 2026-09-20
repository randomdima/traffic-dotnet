# Decision log — routing

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-20 — the search reads no geometry, and the graph has none to read

The travel graph carried each link's two end points, and the planner was an A\* whose bound was the
straight line from a link's far end to the destination. Two build-time guards existed only to keep that
bound admissible: `AddLink` held a weight up to the span between its own ends, and `Join` refused a join
between ends that were not the same point.

**All three are gone and the search is a plain flood.** The graph is now a weight per link, the links each
may be left for, and a price on each of those turns — no coordinates, so nothing in a search *can* read
one. What it bought was a smaller frontier; what it cost was that every route in the town depended on the
abstract graph carrying positions that agreed with the ground it was contracted from, which is the fine
tier's fact and was being kept in two places.

**It is 2.6× the work and finds the same routes.** On Odesa: driving 685 → 1756 links settled per route,
walking 1602 → 4146, over 8920 walking links and 4037 driving ones, with 62 of 64 sampled routes found
before and after. Route searches happen when a leg is drawn rather than every tick, and the full suite's
wall clock did not move. `--bench census` prints the figure, which is why it is a figure and not a guess.

**The join guard went with it and is not replaced.** It was catching a contraction that laid links which
did not meet, which is now a fault with no gate on it — but it was a build-time assertion written in the
abstract tier about the fine tier's business, and the fine tier is where a run's pieces are already
checked to meet end to end (`ARunsPiecesAreTravelledEndToEnd`).

## 2026-09-05 — the travel graph is links and the turns between them, with no node table under it

A node table let the graph name an intersection, and a search state is a link precisely because the
cheapest way *to* a junction is not a fact about the junction. Keeping the nodes and merely not drawing
them was refused: it leaves the fault reachable by anyone reading the type rather than the rule.
