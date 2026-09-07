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

## The follow eases in two places, over real time, and not one

The camera closes on where it is going over about a tenth of a second, because the town is stepped at a
fixed rate and drawn at the window's, so anything nailed to that staircase steps the whole picture;
interpolating every body's drawn position instead would cost a second position per body for one consumer.
The lead swings round over about half a second, which smooths the unit's own manoeuvring rather than the
frame rate and so wants its own span. Both are real time, since a camera easing in sim time would close
three times as fast at three times pace. A jump off the current picture is stood on rather than eased to.
