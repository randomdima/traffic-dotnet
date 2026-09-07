# Decision log — routing

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-09-05 — the travel graph is links and the turns between them, with no node table under it

A node table let the graph name an intersection, and a search state is a link precisely because the
cheapest way *to* a junction is not a fact about the junction. The nodes are gone; the one relation they
made true for free — that a route's links meet end to end, which is what keeps the straight-line bound
admissible — is now stated, and `Builder.Join` refuses a join between ends that are not the same point.
Keeping the nodes and merely not drawing them was refused: it leaves the fault reachable by anyone reading
the type rather than the rule.
