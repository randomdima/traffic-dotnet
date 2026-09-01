# The interface — decisions

Why the panels read as they do. What they must be is [requirements.md](requirements.md).

## The start menu is the same panel laid as the thing it is

**A popup in the top-right corner is furniture beside a town, and at the start there is no town.** The menu
the game opens on was the gear's popup with nothing behind it: it hung off a button that could shut it onto
an empty screen, it carried two pages about a run that was not running, and it sat in a corner while the
middle of the window was the subject. It is now laid centred, carrying the map list and the way out alone,
and it cannot be shut — one flag on the panel (`Menu.AtTheStart`) rather than a second panel, because two
implementations of one map list is how the command line and the menu come to disagree (OBS-2a).

**The name is written a size larger and the description wraps, which is what lets both fit the field.** The
panel is read across a room from a screen with nothing else on it, where the popup under the gear is
glanced at beside a town somebody is watching — so the name goes up to the theme's heading size. Taking the
description up with it made the panel as wide as the window and it stood across the road; keeping it at the
small size and **wrapping** it instead is what makes the panel narrow enough to sit in the grass inside the
ring, which is the whole reason the ring has a field in the middle of it.

**So this panel is laid to the window and every other one to its rows.** Every other panel here is as wide
as the widest thing in it, on the argument that a description cut off mid-word is unreadable; this one is a
share of the window's short side — the side the opening view, and so the ring, is a figure across — and the
descriptions are broken to whatever that comes to. Nothing is cut: what does not fit on a line goes on the
next one, and the row grows by exactly the lines it came to.

**It carries no tab strip.** With the debug switches and the trim figures gone there is one page left, and
a strip of one tab is a row of chrome that says nothing and costs the list a row of height. The way out
keeps its place as the last thing across the top, which without a strip is the title's own line.

**It is one size, and that is what stops it moving.** Laid to its rows, it grew and re-centred as a group
opened — the whole list walking up under a pointer that had just clicked the group header, so the row that
appeared where the pointer stood was never the row the reader had aimed at. The fix is not to pin where it
was first put but to take the height off the thing it has to stay inside: the field in the middle of the
ring is a fixed share of the window, so the panel is too, and centring it on every lay puts it back where it
already was. What will not fit scrolls, which is what the popup under the gear already does on a short
window.

**So it is the one panel here that can be showing fewer rows than it has room for.** A fixed frame and
whole-row scrolling do not divide evenly, and the leftover stands empty under the last row rather than
being spread between them — rows that move as the list changes are the thing the fixed height was bought to
prevent, and a row drawn half outside the panel is worse than a gap inside it.

**And it opens on both groups**, where the popup opens on the places alone. What the shut group buys there
is a mis-click that does not lose a running game; behind the start menu nothing is running, and what a
reader is at it for is the catalogue. Which groups are open is set on each of the two transitions rather
than left as whatever the last panel was showing, so each menu reads the way its own rule says.

## A figure takes hold under the hand, not on release

The figures page moved its trim while a slider was dragged but only stood the town up again when the
button came up, on the argument that rebuilding a fleet sixty times a second is work nobody asked for.
**That made every drag a guess followed by a wait** — the one gesture the page exists for is *turn this
and watch* — and the work it was avoiding is sixteen `CarBuild.Resolve` calls and a ground catalogue, on
the frames a hand is actually moving something.

So the change is reported as it happens. **A pointer resting on a track is not a move**: the trim is read
back after its clamp, so a drag pinned against either stop rebuilds once rather than every frame it is
held there, and letting go of a figure already at its value reports nothing at all.

## The menu is a popup off the gear, not a panel over the town

It was a seven-tab panel filling the middle of the screen, dimming the town behind it and taking every
key while it was up. Everything a player actually goes to it for — open a map, tick a layer — is a
question about the town they are looking at, and a panel that covers that town to ask it is a panel that
has to be shut again before the answer can be seen.

**And it stops half way down the window.** Laid to its own page it was a panel over the town again the
moment somebody opened the scenarios — from the gear to within a hand's width of the bottom edge, arrived
at by opening a group rather than by anybody deciding it. The ceiling is a share of the window rather than
a count of rows, because what it is protecting is the view and not the list; what does not fit scrolls, as
it already did on a window too short to hold the page. It never cuts into what the switch page needs whole,
since those rows are laid at a pitch and not scrolled, and a ceiling through them would draw them outside
the panel.

So it hangs off the button that opens it, keeps the town's keys live underneath, and shuts on the same
button, on a click off it, or on Escape. The close button inside it went with the scrim: a panel with
two ways to shut it teaches neither, and the one it taught was the one that is not there on any other
popup.

## The frame read-out is furniture and the corner is one panel

It was a debug switch in the top-right and the run's own furniture — the map and both seeds — was a
second box in the top-left. Two boxes saying what one run is, and the frame rate reachable only through
a settings panel.

They are now one panel in the top-left: a title that is always on screen, and the read-out's sections
under it. The switch went with the merge, because a thing that is always drawn does not have one. What
the switch was really protecting is the *stamping*, and that is now bound to whether the body is open —
so the price OBS-2b asks for is still paid, and it is paid by the state a player can see.

## The claims went into the status panel, and what names a body went to the body

The claims were a panel of their own along the bottom of the screen, drawn on every map. Two things were
wrong with it. **It was drawn over cities**, where every claim reads `waiting` and the town has no
question behind it — a laboratory read-out standing over a run somebody opened to play in. And it shared
the bottom-left corner with the line saying what the selected unit was doing, so picking a car drew one
box over the other.

So the claims became the status panel's last section, on scenario maps only, and the count of what is
broken went onto the panel's own always-on title — a broken claim two collapses deep is a broken claim
nobody sees, which is the whole of what the bottom panel was buying with the space it took. The corner
it left is not reused: the middle of the view is the town's, and one fewer box on it is the point.

**And what a watch had to say about one body went to that body.** The claims table named the unluckiest
car in the town — `deepest 8 mm car 7` — which is a finding somebody then had to go and find. A claim is
a statement about the town; the same two sweeps read at one body are now on the label standing beside the
selected unit, where the eye already is.

## The selected unit's state left the corner and stands at the unit

It was one line in the bottom-left, which is as far from the unit it describes as a 1600-pixel window
allows. Picking a car out and then reading about it in the opposite corner is two places to look, and
with several cars on screen the line does not say which of them it is about — it says `car 21`, and
finding car 21 is exactly the thing the reader was doing when they clicked.

It now stands beside the box the brackets wrap and follows it, flipping to the other side rather than
being pinned into a margin over the unit, and drawing nothing at all for a unit that is not on the picture
— inside a building, or behind the camera. A label clamped onto a window edge points at an edge the unit
is nowhere near, which is worse than no label.

## The checks left the menu

OBS-2a used to bind the probe list as well as the map list: every check the build ships had to be
launchable from the menu, and the menu ran one and showed what it printed in a panel beside itself.

A probe is a terminal instrument. Its output is tens of lines that want scrolling, grepping and keeping,
and the panel that showed them could do none of those; nobody ran one that way twice. `--bench <name>`
is the way they are run, `--bench all` runs the lot, and the catalogue moved to `bench/` with them.
OBS-2a now binds the maps, which are the thing the menu genuinely is the second front door to.

## The seeds and the pace lost their pages

The seed pair was on screen the whole run and had a page of its own with a re-roll that rebuilt the
town; the pace had another. Neither was a thing anybody reached for: the pace is three keys and a
backtick, and the seed is on the caption of every picture the shot path takes, which is where it is
actually quoted from. The pace state that does matter mid-run — frozen, agents held — is on the status
panel's title, in the pace's own place, because that is what the pace *is* while either holds.

Re-rolling the agent seed went with the page. It was the whole town life cycle behind one button, and
what it produced was a town nobody could name afterwards; a run worth keeping is opened by name from
the map list.

## The interface's density is capped by what fits, and the layout was left alone

The panels are laid out in interface pixels, and how many of the display's own pixels one of those is worth
was the platform's factor and nothing else. That is right on a desktop and wrong on a handset: a phone
reports three device pixels to the point over a viewport 390 points across, so the interface was laid out
on 390 pixels — narrower than the menu, which then hung off both edges of the screen.

Two ways out were open. One was to take every figure in the theme down a notch, which shrinks the chrome
everywhere and changes every reference frame to fix a window nobody was looking at through. The other is a
cap: the density is the display's own right up until the window would hold fewer interface pixels than the
panels were laid for, and then it is whatever leaves them on the glass. That is the one taken. An ordinary
desktop window never reaches it, so the pictures are the pictures they were; a handset reaches it at once,
and what it gets is the same layout at the density that fits.

**It is a size and not a device test.** A phone held either way up and a desktop window dragged down to a
strip are the same problem, and the figure that answers it is the width of the widest panel rather than
anything about who is holding the screen.

## The compass is drawn only while the town is turned

North-up used to be the only way the town could be, so there was nothing to say about it. Now that it turns
(`OBS-1c`) there has to be a way back, and there are two candidates: a spring that puts the town level
whenever it is near north, or a button.

A spring cannot work here. The turn arrives a degree at a time — a frame of a twist, a notch of the wheel —
so a camera that snapped back inside a few degrees would undo every step before the next one arrived, and
the town could never be nudged off north at all. The button is exact, it is one press, and drawing it only
while the town is turned means it is never a control that does nothing: a needle standing straight up on a
town already north-up is the whole of what it would have said.
