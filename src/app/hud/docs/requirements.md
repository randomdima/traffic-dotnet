# The interface — requirements

The start panel, the settings panel and the two pieces of furniture that are always on screen. What all
of it is drawn with is [app/screen](../../screen/docs/requirements.md); the layers, the switches and the
frame read-out are [app/debug](../../debug/docs/requirements.md) — **this slice draws a switch and does
not own one**; the camera is [app/camera](../../camera/docs/requirements.md).

**OBS-2** The **start panel picks the map and both seeds**, and **both seeds stay visible during the
run**.

**OBS-2a** Every scenario and probe the project ships is **reachable from the start menu**, not only from
the command line, and **the list the menu reads is the list the command line reads**. A check nobody can
launch is a check nobody runs. Guard the list in both directions: every entry names something that
exists, and everything that exists appears in the list.

The menu has three pages — the places, the scenarios, and the checks and probes. A menu of two cities
should not read as a menu of two cities and a laboratory, and a mis-click on the row under a city should
not lose somebody's game.

**OBS-2g** **Escape opens and shuts the settings panel, and the way out of the game is the button inside
it.** A scene with no such panel keeps Escape as its own way out. The panel holds the debug switches, the
seeds with a re-roll that rebuilds the town, the live tuning and the control legend.

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

**OBS-2f** A distance between two places is measurable **without a rebuild**:

- It is a debug switch like the layers, and **it takes the mouse for as long as it is ticked** — a click
  then measures rather than selecting or ordering, and input is offered to it **before** the selection
  layer.
- It measures between **two** points, graduated on the same ladder as the legend.
- **A finished measurement is kept and the next is laid beside it**; they are dropped together.
- **Every figure it writes carries its own unit**, as that figure suits.

**What a map is for shows on the map.** The proving ground carries figures no town has — what each shape
of road costs each drivetrain — and they are drawn as a panel of their own in the top-left, one collapsible
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

**The switch rows are drawn here and owned there** (`OBS-2b`, `OBS-2c` —
[app/debug](../../debug/docs/requirements.md)). The settings panel is where a layer is turned on, and the
layer is what a switch means; keeping the state with the layers is what stops the panel and the overlay
reaching into each other.

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
