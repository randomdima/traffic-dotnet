# Decision log — the walker

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-08-29 — the crossing patience is spent where the body is, not where its line still goes

Twenty-five minutes of Odesa left eighty-four walkers standing still, and the worst of them had held one
spot for nineteen of those minutes. Every one was in a crowd, and every crowd had at its head a body
standing **in the carriageway**, refused the band in front of it, with a patience clock reading zero.

`MayStepOnto` already carries PER-15's escape "wherever the body has got to on it" — past the patience the
band is granted and the traffic gives way. What was clearing the clock it spends was `AtTheKerb`, on a test
about the *line* rather than about the body: a walk laid onto a crossing is consumed as the body walks it,
so a walker part way over has no crossing point left **ahead** of it, and that read as "this body is not
crossing anything" and zeroed the patience every tick. The escape could never arm for the one body that
needs it — one already in the road, which is the body a driver is stopped for.

Where the body is standing is what decides it now. On a pavement the clock is nobody's and is cleared; on
ground a car may drive on, with no ground granted in front, it runs — which is what the drivable branch
below it already did, and the two are one rule rather than two.

Two walkers were left standing at the end of the same twenty-five minutes, neither for more than a fifth of
it, and the walks that arrived went from 1868 to 2010.
