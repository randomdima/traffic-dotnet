# The interface — decisions

Why the panels read as they do. What they must be is [requirements.md](requirements.md).

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
