# The camera — decisions

Why it moves the way it does. What it must do is [requirements.md](requirements.md).

## Follow is offered to any single selection, and not only to a unit under a hand

**A car nobody is driving is the thing this town is for watching.** Follow used to be offered only to a
unit being driven on the keys, on the argument that an autonomous agent is watched by panning to it — and
panning to a car at 50 km/h is a reader chasing a sprite off the edge of a window they cannot pan as fast
as it moves. The one thing a reader has already said about which unit they care about is the selection, so
that is what the camera stands on: **exactly one unit, driven or not**.

**Exactly one, because a group has no place to stand.** The middle of a box drawn round a district is a
point no member of it is at, and a camera that framed the whole set would zoom, which is the reader's.

## Free pan wins by comparison rather than by a flag

`Follow` keeps what it left the camera at and compares against it on the next frame. **A gesture that
moves the camera therefore ends the follow without knowing the follow exists** — the pan, the wheel, the
twist between two fingers, and whatever is written next. The alternative was a flag every gesture had to
remember to clear, which is a rule that holds until somebody adds the gesture that forgets.

**The comparison covers the zoom and the turn too**, and that is a choice rather than a side effect: both
move the middle of the view — one about the pointer, the other about the point it is turned at — so a
follow that survived them would drag the picture back the moment the reader let go, and the wheel would
feel like fighting the town.

## What is asked for is a selection, not a unit

**A click on the unit already picked out changes nothing about the selection** (CTL-1: it deliberately
does not even give up the wheel), so the follow cannot be re-armed by watching the set change — a reader
who panned away from the car they had selected would have to click something else and click back. The
gesture layer therefore reports **that a selection was asked for** rather than that one changed, and a
click on the same car puts the camera back on it. This is the only reason `PlayerHands.Pointer` has a
return value.

## The lead is a time, and it is capped against the view

**A second of the road in front** rather than a distance: a fixed lead in metres is the whole street ahead
of a walker and half a car length ahead of a car at speed. It is capped at a share of the half-view
because the lead is measured off the unit's speed and the picture is not — at 100 km/h and a framing
close enough to read a number plate, an uncapped lead puts the subject off its own picture.
