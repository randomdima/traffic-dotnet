# The camera — decisions

Why it moves the way it does. What it must do is [requirements.md](requirements.md).

## Follow is offered to any single selection, and not only to a unit under a hand

A car nobody is driving is the thing this town is for watching, and panning to one at 50 km/h is a reader
chasing a sprite off the edge of a window. The selection is the one thing a reader has already said about
which unit they care about, so the camera stands on exactly one — a group's middle is a point no member
is at, and framing a set would zoom, which is the reader's.

## Free pan wins by comparison rather than by a flag

`Follow` keeps what it left the camera at and compares on the next frame, so any gesture that moves the
camera ends the follow without knowing the follow exists. A flag every gesture must clear holds until
somebody adds the gesture that forgets. The comparison covers zoom and turn deliberately: both move the
middle of the view, so a follow surviving them would drag the picture back the moment the reader let go.

## What is asked for is a selection, not a unit

A click on the unit already picked out changes nothing about the selection (CTL-1), so a follow re-armed
by watching the set change could not be recovered without clicking something else and back. The gesture
layer reports that a selection was *asked for*, which is the only reason `PlayerHands.Pointer` has a
return value.

## The lead is a time, and it is capped against the view

A second of road ahead rather than a distance, since a fixed lead in metres is the whole street ahead of a
walker and half a car length ahead of a car at speed. It is capped at a share of the half-view because the
lead is measured off the unit's speed and the picture is not.

## The lead eases and the camera's own place does not

A body is drawn where its last tick left it, so a camera easing towards that place spent the part of each
step it had not covered on the picture — a swim of a few pixels on the followed body itself, worst under a
hand at speed, the one case the follow exists for. Moving the camera by the unit's own step makes the body
still on the glass and leaves the staircase on the town going past, where it is the tick rate and reads as
one. What is left to ease is the lead, over about half a second of real time, since a lead easing in sim
time would swing three times as fast at three times pace.

Drawing every body at an interpolated position would smooth the town going past as well, and costs a
second position per body for one consumer. It is worth that only once something other than the camera
wants it.
