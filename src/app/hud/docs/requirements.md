# The interface — requirements

The status panel, the two popups that hang off the corner buttons, and the pieces of furniture that are
always on screen. What all of it is drawn with is [app/screen](../../screen/docs/requirements.md); the
layers and the switches are [app/debug](../../debug/docs/requirements.md) — **this slice draws a switch
and does not own one**; the camera is [app/camera](../../camera/docs/requirements.md).

**OBS-2** `P7` The **menu picks the map**, and **what a run is stays visible while it runs**: its frame rate,
its town and its pace, in one line that is never not on screen.

**OBS-2a** `P7` Every map the project ships is **reachable from the menu**, not only from the command line, and
**the list the menu reads is the list the command line reads**. Guard the list in both directions: every
entry names something that exists, and everything that exists appears in the list.

The maps are cut into two collapsible groups — the places, and the scenarios laid to put one behaviour
under a microscope. **Which of them a menu opens on is a property of which menu it is.** The popup under
the gear opens on the places alone: a menu of cities should not read as a menu of cities and a laboratory,
and a mis-click on the row under a city should not lose somebody's game. **The start menu opens on both**
(`GEN-1b`): nothing is running behind it, and reading the whole catalogue is what somebody is at it for.

**OBS-2n** `P7` **A map picked says so until it is standing.** Opening one is a plan laid from its brief, a
ground laid and a fleet stood up — and in a page a fetch before any of that — so the click is answered by a card in the
middle of the window naming the map, from the press until the town it names is running.

- **The frame that took the click draws the card and opens nothing.** What acts on the name is the loop's
  next turn, or in a page the boot's own wait.
- **No frame waits for the open, on either head.** The window keeps pumping, the town already standing
  keeps ticking and the card keeps being drawn — a run that stops answering its window for the seconds a
  town takes to lay is one the desktop reports as hung. **Only what the device owns may be done in the
  frame**: laying a plan, a ground and a fleet touches neither, so a head with threads does it on one of
  its own.
- **It names the map and claims nothing about how far along it is**: the two heads wait for different
  things — a fetch against a lay — and a bar would measure one in the other's units.
- **While it is up it is the whole of the interface.** No panel, button, popup or layer is drawn and
  nothing takes a click, and what arrived while the wait was on is dropped rather than banked.

**OBS-2g** `P7` **Escape opens and shuts the settings popup, and the way out of the game is the button inside
it.** A scene with no such panel keeps Escape as its own way out. The popup holds two pages and nothing
else — which map to open, and everything a debug session turns, cut into its sections (`OBS-2y`) — and the
way out standing apart from their tabs. **The start menu is the exception and not
such a scene** (`GEN-1b`): it cannot be shut, so Escape does nothing at it and the way out is the tab that
says so.

**OBS-2e** `P7` **How big the town is, is on screen at all times**: a graduated scale legend in the
bottom-right corner.

- **Its length is held and its marks answer the zoom.** The graduations stand at a round number of metres
  at whatever the camera is showing, and it is **the number of them, never the bar, that changes**.
- It is **furniture and not instrumentation**, so it has no switch and shows from the moment a town is
  standing.
- **Nothing is drawn behind it or behind any figure its marks write** — a casing and an outline carry
  them against the town instead.

**OBS-2i** `P8` **What the map on screen claims about itself, and whether it is keeping it, is on screen while
it runs**: the last section of the status panel, a row a claim with the figures behind its verdict under
it.

- **It is there on a scenario map and on no other.** A laboratory read-out over a city is a read-out with
  no question behind it, so on a place the section is not drawn, is not counted in the panel's height and
  takes no clicks. Which a map is, is the catalogue's answer and not the panel's.
- **A broken claim is on the line that is always on screen**: the panel starts shut, so a count of what
  is broken goes on the title itself. Which claim, and on what figures, is what the panel and then the
  section open to; `--ui scenario` opens both.
- It draws **the run's own watches** (`Bench.ScenarioWatch`) and does no arithmetic of its own — one
  machine and three readers ([verification](../../../../docs/verification.md#what-a-map-claims-about-itself)).
- **A claim and a reading are drawn differently and neither is invented here**: a claim carries a verdict
  in the three words the report uses, and a reading carries a figure and no verdict at all.
- **Nothing here is about one body.** What a watch has to say about one unit is a row on that unit's own
  panel (`OBS-2m`).

**OBS-2f** `P7` A distance between two places is measurable **without a rebuild**:

- It is a debug switch like the layers, and **it takes the mouse for as long as it is ticked** — a click
  then measures rather than selecting or ordering, and input is offered to it **before** the selection
  layer.
- It measures between **two** points, graduated on the same ladder as the legend.
- **A finished measurement is kept and the next is laid beside it**; they are dropped together.
- **Every figure it writes carries its own unit**, as that figure suits.

## The status panel

**The top-left corner is one panel, and its title is furniture** (`OBS-2`). Under the title, on the title,
opens **what the frame cost, where it went, and where the tick's own time went under it**; why each of
these is so is [`StatusPanel`](../StatusPanel.cs)'s own account.

- **It is priced on the same footing as what it measures** (`OBS-2b`): nothing but the rate is stamped
  while the body is shut, and the body starts shut.
- **It quotes two rates and says which is whose** — what the town is drawn at, and what this build's own
  work would allow — and **the distance between them is the headroom**.
- **Its rows sum to what they are rows of**, and what no row claimed is printed as `other` rather than
  dropped.
- **It is a per-run instrument and not a per-frame one**: every timing is a window's mean, and the counts
  beside them are not averaged.
- **Each state is one width, and the shut bar is the width of its own line.** Both are budgets rather
  than measurements, so the panel does not move while it is read — and **a scenario map is budgeted for
  its claims in both states**, whether or not any of them is broken or open.
- **Sections collapse, because the panel is read at two depths.**

## The unit read-out

**OBS-2m** `P7` **The bottom-left corner is the selection, and it is the only place the interface writes about
it**: a title naming what is picked out over a body of rows — what the unit is, what it is doing, how
fast, what is claimed in front of it, how much room that leaves, how much of its trip is left, and what
the run's watches have against it.

- **Nothing about a unit is written on the town** (`CTL-1`). What stands at the unit is the mark and only
  the mark, and which unit the corner means is never in doubt, because the brackets are on it and the
  camera can be stood on it (`OBS-1a`).
- **A group is counted, not described** (`CTL-1b`): how many of each kind, and nothing else.
- **It does not need the unit on the picture.** Somebody indoors or riding is not drawn (`PHY-7`), and a
  unit the camera has been panned off has nothing on screen either — this still says what it is doing and
  where.
- **Every figure is read off the body and none is worked out here**, and the words for both kinds are their
  own slices' (`Car.Control.DrivingWords`, `Person.Control.WalkingWords`).
- **It appears with a selection rather than with a switch**, and its title shuts the body like the status
  panel's.

## The popups hang off the buttons that open them

The gear opens the menu and the question mark opens the control legend, each in a popup under its own
button. Both obey the same three rules.

- **A popup opens under its own button and is aligned to that button's trailing edge**, so what was
  pressed and what appeared are visibly the same thing; one site decides that for both. **It reaches no
  further than half way down the window** — one running to the bottom edge is a panel over the very town
  its rows are questions about — and a page longer than that **scrolls**. The ceiling never cuts into what
  the debug page needs whole, since its rows are laid at a pitch rather than scrolled — and **that page is one
  height whichever section is showing**, so clicking through the sections moves no row the pointer is on.
- **The button that opens it shuts it**, and so do a click anywhere off the panel and Escape. There is no
  close button inside a popup: a panel with two ways to shut it teaches neither.
- **A popup is not a mode.** The town keeps its keys and its camera while one is up; only the wheel is
  taken, and only while the pointer is over the panel. A click off an open popup shuts it and is **taken**
  — dismissing a panel and selecting the car under the pointer are two intentions, and one click is one of
  them.

**The start menu obeys none of these** (`GEN-1b`, `Menu.AtTheStart`). It is the same panel and the same
rows, but it is the screen rather than furniture beside a town: it stands in the middle of the window at
**one size whatever is open in it**, carries one page and so no tab strip — the way out stands on the
title's own line — writes its map names a size larger and wraps their descriptions, opens on both groups,
cannot be shut, and has neither the gear, the question mark nor the unit read-out drawn under it.

**A list that scrolls is dragged as well as wheeled, and a row is opened on the way up.** A handset has no
wheel (`CTL-9`), and the gesture is the one the town already answers: **a press starts a gesture rather
than picking** (`CTL-1b`), the rows follow the pointer while it is down, and the row the press landed on
is opened when it comes up without having travelled — the same travel that tells a drag from a click on
the road. **The rows come and go whole**: what a drag has travelled is spent as each row's own height goes
by, and what is left over at either end of the list is dropped rather than banked.

**The control legend is its own popup and not a page of the menu.** The menu is where somebody goes to
change something; the legend is where they go to find out what a key does, and a legend behind a tab of
the settings is a legend read once and never found again.

**The switch rows are drawn here and owned there** (`OBS-2b`, `OBS-2c` —
[app/debug](../../debug/docs/requirements.md)), **and so are the sections they are shown in** (`OBS-2y`).
The menu is where a layer is turned on, and the layer is what a switch means; keeping the state with the
layers is what stops the panel and the overlay reaching into each other.

## The interface is in the window's own pixels

The panels, the legend and the ruler's figures are laid in window pixels and not in world space, so a
zoom does not resize the interface and a turned town does not turn it (`OBS-1c`). A pointer position is
converted **once**, at the boundary, and everything downstream is in one space.

**OBS-2k** `P7` **A label is drawn at the size it was designed, and it is the panel that gives way.** How dense an
interface pixel is drawn is the display's own factor — a 4K screen would otherwise write a 15-pixel label
at a third of its designed size, and a handset reporting three device pixels to the point would write it at
two thirds. **Where a panel wants more room than the window has, the panel is laid narrower**, and the
density is left where the display put it.

- **The floor under it is the narrowest window the panels are still laid out for**, not the width they
  would like: below that the interface is laid denser than the display asked, since a label drawn under a
  pixel a glyph is not a label. It is a size in interface pixels and not a device to detect, and **both
  sides bind**, so a window held either way up is fitted by whichever of them is short.
- **An ordinary window never reaches it**, which is what keeps a reference frame the picture it was.
- **`--ui-scale` is not floored either.** Naming one at all says the guess underneath was wrong, and a
  figure asked for and then quietly moved is a switch that does nothing.
- **The town is unaffected.** The camera opens on a span in metres, so what changes is how much of the
  window the chrome is worth and nothing about what is being looked at.
- **And no panel is ever wider than the window.** What does not fit is cut where the line is drawn, or
  wrapped where the panel wraps, and reads as a line with more behind it.

**OBS-2l** `P7` **The window fills the screen from a button as well as from a key.** `F11` is the key and it is on
the legend; the button is the same lever for a reader who has not got one, which on a handset is every
reader — and a handset is where it is worth most, since the browser's own furniture is a third of a screen
that is already small.

- **It is the one corner button drawn under the start menu** (`GEN-1b`), and it stands in the corner there:
  the gear and the question mark are about a town and there is none yet, and the screen is at its smallest
  exactly while somebody is choosing on it.
- **It says nothing about its own state**, where the other two say whether their popup is showing: a
  window filling the screen is the thing being looked at.
