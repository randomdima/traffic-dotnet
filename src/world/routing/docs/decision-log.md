# Decision log — routing

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-20 — the clock a leg is given up by belongs to both agent kinds

The walker's give-up clock and the driver's were the same arithmetic in two places and two vocabularies —
a record low of what is left to cover, a clock against it, and a patience — and only one of them was
right. The walker's had already been taught the two things that break such a clock: measure against
ground that *shrinks* rather than the point the follower is aiming at, and count progress to a body's
width rather than to the millimetre. The driving side measured against a line it regrew every few lanes,
which stands at a sight distance whether a leg is going well or not at all.

It is `LegProgress` here now and both use it. **This is where it belongs and not in either agent**: the
two slices are siblings and the thing they share is a fact about travelling a chain of ways, which is
this slice's subject. What each side keeps is its own: the distance fed in, the patience it is asked
against, and — the driver's alone — a red light, which **holds** the clock rather than giving it back,
because a light is there again every cycle and a wait excused by rewinding is a wait excused for ever.

**The lane handover restarts it, as the walker's way handover always did.** Left running across a
handover the clock spends a whole lane's worth of ground before the car has covered any of the next one,
and a car driving perfectly well down a long street is called stuck at the far end of it. That was
measured rather than reasoned: with the restart missing, five minutes of Odesa gave up 1 051 legs and
priced up 3 880 stretches; with it, 654 and 2 729.

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
