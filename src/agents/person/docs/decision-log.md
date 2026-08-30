# Decision log — the walker

Why this slice reads the way it does. Only decisions still binding are here: a superseded one is deleted,
not annotated. The rules themselves are [requirements.md](requirements.md); how a type works is its own
XML docs.

## 2026-08-30 — a walker walks against its stop, and a stopped rescue is not a rescue

Five minutes of Odesa put twenty-five walkers in a heap and left one of them standing for fifty-two seconds
with no decision taken at all. The stuck probe now counts both — a heap is four or more bodies nearer than
half the gap they keep, and a hold is a run of ticks the book held one body — and what the two of them
showed was one shape and one accident.

**The shape.** The biggest heap was ten walkers on one way of one pavement at 1.8 m spacing, every one of
them granted −0.20 m, each held by the one in front. The gap the book keeps is 2.0 m and a walker's
stopping distance at its pace is `v²/2a` = 0.20 m, so the numbers were not a queue at all: every body had
set off at full pace on a grant of a centimetre and come to rest exactly one stop inside the ground the
book had given the body in front. Nothing gets that back — feet have no reverse, and a walker the book is
holding takes no decision, so no clock runs behind it. The column could only move one stop at a time, in
lock step, which is what a heap of people looks like from above.

The bar is now what the body needs to come to rest in: this tick's stride and the stop after it. **Not the
pace's own stopping distance** — tried first, and it made things worse. A pair already a little inside one
another's gap both stood for ever, and rings of two walkers each held by the other appeared where there had
been none; the creep out of a violated gap is the only thing that breaks one, and reading the bar off the
speed the body is actually doing leaves it there, because that is nothing at rest.

**The accident.** The one fifty-two-second hold was not behind anybody. It was a body stopped halfway over
a crossing, refused the band in front, patience at 55 s against a bar of 8 — held by an ambulance's road
with the ambulance standing still on it at 0.4 mm/s. PER-15's escape is disabled against a rescue, and the
reason it is says a call lasts seconds and is going to pass. This one was not passing. The exemption now
asks whether the rescue is coming through at all, against the walker's own pace, and walks every stretch
over the band rather than taking the first — a rescue standing on a piece of road cannot hide one moving
through it. **Zero was the wrong bar**: a car held in a queue creeps at fractions of a millimetre a second,
which read as coming through for as long as it sat there.

Over the same five minutes: twenty-five walkers ever in a heap became four, the worst-off walker's time in
one went from 5% of the run to none of it, the longest hold from 52 s to 37 s, and the walks the town gave
up from 147 to 76 against 756 arrived.

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
