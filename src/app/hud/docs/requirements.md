# The interface — requirements

The status panel, the two popups that hang off the corner buttons, and the pieces of furniture that are
always on screen. What all of it is drawn with is [app/screen](../../screen/docs/requirements.md); the
layers and the switches are [app/debug](../../debug/docs/requirements.md) — **this slice draws a switch
and does not own one**; the camera is [app/camera](../../camera/docs/requirements.md).

**OBS-2** The **menu picks the map**, and **what a run is stays visible while it runs**: its frame rate,
its town and its pace, in one line that is never not on screen.

**OBS-2a** Every map the project ships is **reachable from the menu**, not only from the command line, and
**the list the menu reads is the list the command line reads**. Guard the list in both directions: every
entry names something that exists, and everything that exists appears in the list.

The maps are cut into two collapsible groups — the places, and the scenarios laid to put one behaviour
under a microscope. **The places are open and the scenarios are not**: a menu of two cities should not
read as a menu of two cities and a laboratory, and a mis-click on the row under a city should not lose
somebody's game.

**OBS-2g** **Escape opens and shuts the settings popup, and the way out of the game is the button inside
it.** A scene with no such panel keeps Escape as its own way out. The popup holds which map to open and
the debug switches, and nothing else.

**OBS-2e** **How big the town is, is on screen at all times**: a graduated scale legend in the
bottom-right corner.

- **Its length is held and its marks answer the zoom.** The graduations stand at a round number of metres
  at whatever the camera is showing, and it is **the number of them, never the bar, that changes**.
- It is **furniture and not instrumentation**, so it has no switch and shows from the moment a town is
  standing.
- **Nothing is drawn behind it or behind any figure its marks write** — a casing and an outline carry
  them against the town instead.
- It reads the zoom off the viewport transform and metres off the conversion helper, so it is handed
  nothing and cannot be pointed at the wrong camera.

**OBS-2i** **What the map on screen claims about itself, and whether it is keeping it, is on screen while
it runs**: the last section of the status panel, a row a claim with the figures behind its verdict under
it.

- **It is there on a scenario map and on no other.** A place is a town somebody plays, and a laboratory
  read-out over a city is a read-out with no question behind it — so on a place the section is not drawn,
  is not counted in the panel's height and takes no clicks. Which a map is, is the catalogue's answer and
  not the panel's.
- **A broken claim is on the line that is always on screen.** The panel is shut by default and so is
  everything under it, so a count of what is broken goes on the title itself: a town that has broken one
  of its own claims says so without being asked. Which claim, and on what figures, is what the panel and
  then the section open to — a scenario is read at two depths. `--ui scenario` opens both.
- It draws **the run's own watches** (`Bench.ScenarioWatch`) and does no arithmetic of its own, so the
  section, the table a headless run prints and the tier that asserts on the map are three readings of one
  machine ([verification](../../../../docs/verification.md#what-a-map-claims-about-itself)).
- **A claim and a reading are drawn differently and neither is invented here**: a claim carries a verdict
  in the three words the report uses, and a reading carries a figure and no verdict at all.
- **Nothing here is about one body.** A claim is a statement about the town; what a watch has to say about
  one unit is drawn beside that unit (`CTL-1`), where the eye already is.

**OBS-2f** A distance between two places is measurable **without a rebuild**:

- It is a debug switch like the layers, and **it takes the mouse for as long as it is ticked** — a click
  then measures rather than selecting or ordering, and input is offered to it **before** the selection
  layer.
- It measures between **two** points, graduated on the same ladder as the legend.
- **A finished measurement is kept and the next is laid beside it**; they are dropped together.
- **Every figure it writes carries its own unit**, as that figure suits.

## The status panel

**The top-left corner is one panel, and its title is furniture.** The rate, the map and the pace are what
somebody watching a run quotes, so they are on screen from the moment a town is standing and have no
switch. Under that title, on the title, opens **what the frame cost, where it went, and where the tick's
own time went under it**.

- **It is priced on the same footing as what it measures** (`OBS-2b`). The title is a rate the frame loop
  measures anyway; **nothing else is stamped while the body is shut**, so a run that did not ask for the
  partition takes no timestamps at all. The body starts shut.
- **It quotes two rates and says which is whose.** What the town is drawn at is the display's figure
  under FIFO and moves not at all with the size of the town; beside it is the rate this build's own
  work would allow, and **the distance between them is the headroom** — the one thing on the panel that
  answers whether a town costing twice as much would still be drawn at the same rate. The second is a
  ceiling on the work and not a promise about the machine: what it leaves out is exactly what the
  blocked row holds.
- **It ranks the tick by phase and must account for the frame.** A read-out whose rows do not sum to the
  thing they are rows of is a read-out nobody can act on: the row somebody is about to go and optimise
  might be three percent of the frame, and until the rows close there is no way to know it from thirty.
  What no row claimed is printed as `other` rather than dropped.
- **It is a per-run instrument and not a per-frame one**: every timing is a window's mean, because a
  figure that changes sixty times a second cannot be read. The counts beside them are not averaged — a
  body count is the state of the town rather than a measurement of it.
- **Each state is one width, and the shut bar is the width of its own line.** A bar sized on the body it
  hides reached a third of the way across the town to say four words. Both widths are budgets rather than
  measurements: a panel that grew a character when the rate went from 9 to 10 fps, narrowed as a section
  collapsed, or widened the moment a claim broke, is a panel that moves while it is being read. **A
  scenario map is budgeted for its claims in both states**, whether or not any of them is broken or open.
- **Sections collapse because the panel is read at two depths.** Watching a run wants the frame, its rate
  and its worst; chasing a row wants ten more lines under it.

**What a map is for shows on the map.** The proving ground carries figures no town has — what each shape
of road costs each drivetrain — drawn as a panel of their own under the status panel, one collapsible
section per shape:

- It is **a debug switch like the layers** and it shows on the proving ground and nowhere else: every other
  map is a town, and a town has no shapes to name. **It is the one switch that starts on**, because it draws
  the only thing that map is for and a rig whose read-out has to be found in a settings panel is a rig
  nobody reads.
- It reads the same instrument `--bench track` prints (`Bench.TrackMetrics`) and does no arithmetic of its
  own. A second implementation would be a second answer, and the two disagreeing is not something anybody
  could settle by looking at the track.
- **The header carries the figure and the rows carry the account of it**, because the panel is read at two
  depths: watching the lap wants the top speed of each shape, and asking why one drivetrain is slower wants
  four more lines under it.
- **Where it starts is handed to it** by the panel above it, which changes height as its body opens. A
  figure copied from that panel stops being that panel's the first time it grows a row.

## The popups hang off the buttons that open them

There are two buttons in the top-right corner and a popup under each: the gear opens the menu, and the
question mark opens the control legend. Both obey the same three rules.

- **A popup opens under its own button and is aligned to that button's trailing edge**, so what was
  pressed and what appeared are visibly the same thing. One site decides that for both.
- **The button that opens it shuts it**, and so does a click anywhere off the panel, and so does Escape.
  There is no close button inside a popup: a panel with two ways to shut it teaches neither.
- **A popup is not a mode.** The town keeps its keys and its camera while one is up; only the wheel is
  taken, and only while the pointer is over the panel. A click off an open popup shuts it and is **taken**
  — dismissing a panel and selecting the car that happened to be under the pointer are two intentions, and
  one click is one of them.

**The control legend is its own popup and not a page of the menu.** The menu is where somebody goes to
change something; the legend is where they go to find out what a key does, and a legend behind a tab of
the settings is a legend read once and never found again.

**The switch rows are drawn here and owned there** (`OBS-2b`, `OBS-2c` —
[app/debug](../../debug/docs/requirements.md)). The menu is where a layer is turned on, and the layer is
what a switch means; keeping the state with the layers is what stops the panel and the overlay reaching
into each other.

## The interface is in the window's own pixels

The panels, the legend and the ruler's figures are laid in window pixels and not in world space, so a
zoom does not resize the interface and a scale factor does not have to be threaded through every
measurement. A pointer position is converted **once**, at the boundary, and everything downstream is in
one space.

## Rebuilding a town

- **Clear and refill the rosters rather than replacing them** — the overlay, the player control and every
  debug rig hold those very list instances.
- **A generation failure replaces nothing**: the message goes to the panel and the town on screen keeps
  running (GEN-8).
- A rebuild tells the ruler only that the town has changed; its two points are world coordinates on a
  town that no longer exists.
