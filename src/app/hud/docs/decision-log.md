# The interface — decisions

Why the panels read as they do. What they must be is [requirements.md](requirements.md).

## 2026-09-26 — rules reworded to what the code does

The owner ruled the code the source of truth for this audit.

- `OBS-2n`: opening a map is a plan laid from its brief, not a plan read.

## 2026-09-19 — the panel's allocation gate warms until the draw is tier-1 code

**It was failing for the runtime and not for the panel.** Two warm-up passes compiled the draw and did not
promote it, so a method crossing its call-count threshold inside the measured hundred charged the
recompilation — 7 KiB in one pass — to this thread, at a pass that moved whenever anything upstream changed
how often the draw had already been called. The class passed inside the whole tier and failed run alone.
**So it warms 256 passes**, past every tier a call count can cross, and what the measured passes run is the
code a frame runs. The assertion is unchanged — not a byte over a hundred draws (rule 2); the warm-up was
the fiction.

## 2026-09-12 — the switch page is now the taller of the popup's two bounds

An eleventh layer (`OBS-2r`) took the popup to 543 px of a 1 000 px window, which is what the rule says:
the half-window ceiling never cuts into the switch page, whose rows are laid at a pitch. **The layout suite
was measuring the page's length and calling it the ceiling**, so it would have failed on the next layer
whatever the layout did. It now asks the ceiling of a window with room for the switch page above it, and
asks a short window what actually bounds it — the panel against the glass. **A switch page longer than a
tall window's ceiling is the thing to watch**: at this pitch that is some thirty layers, and the answer then
is the rule's own for a long page — it scrolls.

## 2026-09-09 — the picked map is opened after the frame, and on the desktop on a thread of its own

Opening a city is seconds of work and used to happen inside the frame that took the click, so the picture
stopped with the row still lit. Deferring the open to the end of the frame is what gets the card drawn *and
submitted*; both heads write the name down and differ only in who acts on it. That still left the desktop
spending the whole open inside one call — the window stopped being pumped, the compositor put "Not
Responding" over it, and the standing town froze. So the open splits along the line of what the device
owns: laying the plan, its ground and its fleet touches neither the device nor the town on screen and goes
on a pool thread; the renderer, the sheets and the standing sprites stay in the frame. Measured on a debug
build of Odesa that is 26.5 s laid against 0.6 s stood. A page gets neither half: it has no threads, and
what it waits for is a fetch the boot already awaits. The card carries no progress, because the two heads
wait for different things and a bar would measure one in the other's units.

## 2026-09-05 — everything about the selection is in one corner, and the town carries only the mark

A read-out standing at the unit is one line wide and a car is worth ten, so the moment there were rows
worth reading there were two places to read them. The words went to the bottom-left and the town kept the
mark alone (OBS-2m); the mark, not the words, says *which* car. The corner is also the only read-out that
survives the unit leaving the picture — a label clamped to a window edge points at an edge the unit is
nowhere near. A group is counted there and not described, thirty cars having no speed or destination
between them.

## 2026-09-01 — the panel gives way to the window, not the density to the panel

The density was capped by what fitted, so a handset laid 560 interface pixels into 1080 device pixels and
every label on screen was two thirds of the size it was drawn at — a millimetre and a half of cap height on
the one device where a reader is holding the glass. A panel wider than the window is now laid narrower, the
rows already wrapping. The floor that remains is the narrowest window the panels are laid out for at all,
well under any phone, so a desktop window is untouched and every reference frame is the picture it was.
Taking every theme figure down a notch was refused: it changes every reference frame to fix a window nobody
was looking through.

## 2026-09-01 — the screen button is a lever and the other two are switches

`F11` fills the screen and a handset has no `F11`, on the machine where filling it is worth most. The mark
is drawn rather than written, the glyph sheet being printable ASCII.

## 2026-09-01 — a list is scrolled by dragging it, and a row is picked on the way up

The only scroll was the wheel, so on a handset the start menu was a catalogue with a bottom nobody could
reach. Picking on the way down is what made dragging impossible: whichever row a scroll started on was the
map it opened. Travel is spent a row at a time, because rows are as tall as the descriptions wrapped into
them and a scroll divided by an average row drifts a row every screenful.

## 2026-08-31 — the start menu is the same panel laid as the thing it is

A popup in the top-right is furniture beside a town, and at the start there is no town. It is one flag
(`Menu.AtTheStart`) rather than a second panel, because two implementations of one map list is how the
command line and the menu come to disagree (OBS-2a). The name a size larger and the description wrapped
keep it narrow enough to sit inside the ring. It is laid to the window and every other panel to its rows,
because laid to its rows it grew and re-centred as a group opened, walking the list out from under the
pointer.

## 2026-08-30 — a figure takes hold under the hand, not on release

Standing the town up only on release made every drag a guess followed by a wait, when the one gesture the
page exists for is *turn this and watch*. The change is reported as it happens; a pointer resting on a
track is not a move, since the trim is read back after its clamp, so a drag pinned against a stop rebuilds
once.

## 2026-08-30 — the claims went into the status panel, and what names a body went to the body

A panel along the bottom drawn on every map was a laboratory read-out standing over a city somebody opened
to play in, and it shared its corner with the selection line. The claims are the status panel's last
section on scenario maps only, with the broken count on the always-on title — a broken claim two collapses
deep is one nobody sees. A claim is a statement about the town, so what a watch had to say about one body
went to that body: `deepest 8 mm car 7` is a row on that unit's own panel.

## 2026-08-29 — the menu is a popup off the gear, not a panel over the town

A seven-tab panel filling the middle dimmed the town and took every key, to ask questions about the town it
was covering. It stops half way down the window at a share of the window rather than a count of rows,
because what it protects is the view and not the list. The close button went with the scrim: a panel with
two ways to shut it teaches neither.

## 2026-08-29 — the frame read-out is furniture and the corner is one panel

Two boxes said what one run is, with the frame rate reachable only through a settings panel. They are one
panel in the top-left now, and the switch went with the merge, because a thing that is always drawn does not
have one. What the switch was really protecting is the stamping, which is now bound to whether the body is
open — so OBS-2b's price is still paid, by a state a player can see.

## 2026-08-29 — the checks left the menu

A probe is a terminal instrument: its output is tens of lines wanting scrolling, grepping and keeping, and
the panel could do none of those. `--bench <name>` is how they run and the catalogue moved to `bench/`.
OBS-2a now binds the maps, which are what the menu genuinely is a second front door to.

## 2026-08-29 — the seeds and the pace lost their pages

Neither was reached for: the pace is three keys and a backtick, and the seed is on the caption of every
picture the shot path takes. The pace state that matters mid-run — frozen, agents held — is on the status
panel's title. Re-rolling the agent seed went with the page: it was the whole town life cycle behind one
button, producing a town nobody could name afterwards.
