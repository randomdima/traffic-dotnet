# Decision log — routing

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- **Where a link ends**: a car park's junction ends a link like any place with a choice (GEN-4h); what needs no
  node is a destination — a doorway, or the metre a bay's way in leaves its lane at.

## 2026-09-20 — the clock a leg is given up by belongs to both agent kinds

The walker's give-up clock and the driver's were the same arithmetic in two places — a record low of what is
left to cover, a clock against it, and a patience — and only the walker's was right: it measured ground that
*shrinks* rather than the point the follower aims at, and counted progress to a body's width. The driver's
measured against a line it regrew every few lanes, which stands at a sight distance whether a leg is going
well or not at all.

**It is `LegProgress` here and both use it**, because the two agent slices are siblings and what they share
is a fact about travelling a chain of ways, which is this slice's subject. What each keeps is the distance
fed in, the patience, and — the driver's alone — a red light, which **holds** the clock rather than giving it
back: a light is there again every cycle, and a wait excused by rewinding is excused for ever.

**The lane handover restarts it, as the walker's way handover always did.** Left running, the clock spent a
whole lane's worth of ground before the car covered any of the next, and a car driving well down a long
street was called stuck at the far end: five minutes of Odesa gave up 1 051 legs and priced up 3 880
stretches without the restart, 654 and 2 729 with it.

## 2026-09-20 — the search reads no geometry, and the graph has none to read

The planner was an A\* bounded by the straight line from a link's far end to the destination, and two
build-time guards existed only to keep that bound admissible: `AddLink` held a weight up to the span between
its own ends, and `Join` refused a join between ends that were not the same point. **All three are gone and
the search is a plain flood**, over a graph with no coordinates in it: every route in the town had depended on
the abstract graph carrying positions that agreed with the ground it was contracted from, which is the fine
tier's fact kept in two places.

**It is 2.6× the work and finds the same routes.** On Odesa: driving 685 → 1756 links settled per route,
walking 1602 → 4146, over 8920 walking links and 4037 driving ones, with 62 of 64 sampled routes found before
and after. Searches happen when a leg is drawn rather than every tick, and the suite's wall clock did not
move; `--bench census` prints the figure.

**The join guard is not replaced.** It caught a contraction that laid links which did not meet — a fault
with no gate on it now — but it was an assertion in the abstract tier about the fine tier's business, and
the fine tier is where a run's pieces are checked to meet end to end (`ARunsPiecesAreTravelledEndToEnd`).

## 2026-09-05 — the travel graph is links and the turns between them, with no node table under it

A node table let the graph name an intersection, and a search state is a link precisely because the
cheapest way *to* a junction is not a fact about the junction. Keeping the nodes and merely not drawing
them was refused: it leaves the fault reachable by anyone reading the type rather than the rule.
