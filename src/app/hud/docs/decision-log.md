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

## The panel gives way to the window, not the density to the panel

The cap read the other way round: the interface was laid out on no fewer pixels than the widest panel
wanted, and a window with fewer than that was drawn denser to make them. On a desktop it never bound, so
it looked free. On a handset it bound every time — 360 points at three device pixels each is 1080, the
panels wanted 560 interface pixels across, and what came back was an interface pixel worth two thirds of a
point. **Every label on the screen was two thirds of the size it was drawn at**, which is a millimetre and
a half of cap height on the one device where a reader is holding the glass rather than sitting back from
it. The panels fitted, and nobody could read them.

So the order is reversed. A panel wider than the window is laid narrower — the rows already wrap and the
lines already cut, so there was nothing to invent — and the floor that remains is the narrowest window the
panels are laid out for at all, well under any phone. It binds where there is genuinely nothing left to
give: a window narrow enough that a glyph would be drawn under a pixel is one where a smaller panel buys
nothing, and the least bad answer is to draw fewer, larger pixels and let the panel run to the edges.

**A desktop window is untouched**, which is the point of naming a floor rather than a preference: the old
figure was reached on a phone and the new one is not reached anywhere, so every reference frame is the
picture it already was.

## The screen button is a lever and the other two are switches

`F11` fills the screen, and a handset has no `F11`. It is also the machine where filling it is worth most:
the browser's own furniture is a third of a screen that is already small, and the town underneath is the
whole reason the page was opened.

**It is the one corner button drawn under the start menu**, where the gear and the question mark are not.
Those two are about a town — one opens what is already open there, the other explains keys no town is
listening for yet — and this one is about the window a town will stand in. The screen is at its smallest
exactly while somebody is still choosing on it, so a button that appeared only after a map was picked
would arrive one screen too late. It stands in the corner there rather than third along: a lone button
with two empty places beside it reads as two buttons that failed to draw.

**And it says nothing about its own state.** The other two say whether their popup is showing, because a
popup is hidden behind its own button; a window filling the screen is the thing being looked at, and a
button that reported it would be answering a question the screen has already answered.

**The mark is drawn and not written.** The glyph sheet is printable ASCII and has no character that reads
as a screen, so the button carries four corner brackets drawn as lines — the same way the compass carries a
needle rather than an arrow glyph.

## A list is scrolled by dragging it, and a row is picked on the way up

The only scroll the menu had was the wheel, so on a handset the start menu was a catalogue with a bottom
nobody could reach: the page it opens on is taller than the field it stands in, there is no wheel to take,
and the two-finger gesture belongs to the camera. Everything else the panel does a finger already did,
which is what made the gap easy to miss — the rows took taps, the trims took drags, and the one thing
between them took neither.

**The gesture is the town's own and not a second one.** A press on the list starts something rather than
picking, the rows follow the pointer while it is down, and the row opens when it comes up without having
travelled — which is `CTL-1b` word for word, applied to a panel instead of to the ground. Picking on the
way down was what made it impossible: whichever row a scroll was started on top of was the map it opened,
so the list could not be dragged at all. It costs the mouse nothing to be read the same way, and one
threshold for both is what stops a tap meaning two different amounts of travel on two surfaces.

**The travel is spent a row at a time.** The rows are as tall as the descriptions wrapped into them, so
there is no pitch to divide a distance by; a scroll converted through an average row drifts a row every
screenful, and the panel is at its most wrong exactly where the list is longest. What a drag has travelled
is held instead and spent as each row's own height goes past, which is the whole-row scrolling the wheel
already did, asked for in pixels. The remainder at either end is dropped rather than banked, so a list
dragged hard off the bottom comes back on the next stroke instead of on the fourth.

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
a statement about the town; the same two sweeps read at one body are now rows on that unit's own panel,
beside the figures the finding is about.

## Everything about the selection is in one corner, and the town carries only the mark

**A read-out that stands at the unit is one line wide, and a car is worth ten.** What a driver was told —
what was claimed in front of the nose, how much room that left, how far off its line it is running, how
much route is left, what a watch has against it — is the answer to every question that starts "why is it
doing that", and none of it fits beside a car. What was beside the car was therefore the one line that did
fit, and the moment there were rows worth reading there were two places to read them.

So the words all went to the bottom-left and the town kept the **mark alone** (OBS-2m): brackets round the
box, the path ahead of it, and nothing written over the road the car is about to drive down. The argument
that once sent that line out of the corner — `car 21` in the opposite corner does not say *which* car — is
answered by the mark rather than by the words: the brackets are on the unit, the path runs from under it,
and the camera can be stood on it (OBS-1a). None of those cover anything.

**The corner is also the only read-out that survives the unit leaving the picture.** A label draws nothing
for a body inside a building or behind the camera, since one clamped to a window edge points at an edge the
unit is nowhere near — so watching somebody get into a car used to mean watching the read-out about them
disappear. A panel in a corner points at nothing to begin with, and goes on saying what they are doing.

**A group is counted there and not described.** Thirty cars have no speed, no destination and no manoeuvre
between them, so a set writes how many of each kind it holds — which is a row in a panel that is already
open, rather than a box laid on the town to hold a count.

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

## The map picked is opened a frame late, so the card saying so is on screen for the wait

Opening a city is seconds of work — the brief read, the layout settled, the ground rasterised, the fleet
stood up and the renderer rebuilt for it — and it used to happen inside the frame that took the click. The
reader pressed a row and the picture stopped: the menu was still on screen, the row was still lit, and
nothing said the press had been read at all. On a page it is worse, because a plan still carried as a file
has to come off the wire first.

The open is now deferred to the end of the frame. That is one line and it costs nothing, and it is what
makes the difference between a card drawn and a card drawn *and submitted*: a frame that opened the town
before it drew would put the card up at the moment it stopped being needed. The seconds the open takes then
land in the next frame's wait, where the clock already forgets time it was never asked to simulate and the
meter already drops the frame — neither needed anything new, because a stalled frame is a thing this loop
was already built to survive.

**The two heads keep their split and lose their duplicate.** Both now do the same thing with a click — write
the name down and put the card up — and differ only in who acts on it: the desktop's loop on its next turn,
the page's boot on its own `await`, which is the one place there where waiting is allowed. That was already
the browser's arrangement; what changed is that it is now the arrangement, and the desktop's own "a map
picked is a map opened" is gone.

**The card carries no progress and no spinner.** There is nothing honest to animate: on the desktop the frame
the card is drawn in is the last frame there is until the town is standing, so anything moving would stop
moving immediately and read as a hang. A page has frames all through its fetch and could animate, and that is
exactly the reason not to — a piece of furniture that is alive on one head and frozen on the other is two
things with one name, and the picture people take of the browser head would not be the picture of the
desktop's.

**And while it is up it is the whole interface.** The alternative was a card floating over a live menu, which
on a page is several seconds of a map list somebody can go on clicking; the run would then be opening two
towns, and the second click would be a defect nothing else could catch. Drawing nothing else is also what
settles the read-out question — a frame rate, a scale bar and a claims table over the town being left are
answers about a run that is already over.
