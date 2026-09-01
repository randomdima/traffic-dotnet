# The camera — requirements

**OBS-1** Top-down, pannable, zoomable and turnable, so that **both the whole town and individual agents
can be watched**.

**OBS-1a** **Free pan always wins.** A manual pan or zoom takes the camera off any unit it was following
and **keeps it off until the follow is asked for again**. Follow is offered only for a unit that is
*moving under a hand*; an autonomous agent is watched by panning to it, not by being chased. **A followed
unit is led by its own speed and heading**, so the ground it is about to cover is on screen rather than
the ground behind it.

The zoom is about the **pointer**, and the view opens on a fixed span at the middle of the town rather
than on a whole-town fit, which on a small map is unreadably small.

**OBS-1b** A run **opens looking at the middle of the town, or at the nearest ground a car could be on**
where no road is in the frame there at all. The middle of a city is a street and the middle of a ring is
the field inside it, so a camera left on the geometric centre opens on grass wherever a map's subject is
not in the middle of its bounding box — anything laid around a park, a lake or a bay. **It is the nearest
such ground and never a fit to the whole town**: the opening span is a figure, and what moves is where the
camera stands rather than how much it shows.

**A town standing behind the start menu is framed like any other** (GEN-1b). The panel stands in the middle
of the screen and the middle of the ring is the field inside it, so the menu sits in the hole and the road
is on screen all the way round; a town shoved aside to clear a panel that is no longer in a corner would be
half off the window instead. **That framing follows the window** until somebody moves the camera
themselves: a canvas that settles its size a moment after the town stood up, or a window dragged wider,
would otherwise leave what the reader opened on half off the screen. The first pan, zoom or turn is theirs
and ends it (OBS-1a).

**OBS-1c — The town turns.** A street runs the way it runs, and a reader following one along the bottom of
the window is reading it sideways. So the view carries a **turn**: how far the town is drawn clockwise from
north-up, about **the point the turn is asked for at** — the world under that pixel is the one thing that
does not move, exactly as it is under a zoom.

- **The town turns and the camera is never aimed.** What is stored is the angle the picture is at, which is
  the thing a reader is looking at; a bearing anybody would have to invert appears nowhere.
- **It is one transform and it is applied in one place** — where a position in metres becomes a position on
  screen, on the processor and in every vertex stage alike. Nothing that carries a rotation of its own —
  a body's heading, a band down a bending line — knows the town is turned at all.
- **What is culled against is the turned view.** A town off the axes covers a larger box than the window's
  own rectangle, and a body just outside that rectangle is inside the picture.
- **Nothing on the interface turns with it.** The panels, the labels, the scale bar and the box a drag lays
  are laid in the window's own pixels: they are the reader's furniture and not the town's.
- **North-up is a button and never a spring.** The compass in the corner is drawn only while the town is
  turned and puts it back level when it is pressed. A turn that snapped to north on its own could never be
  nudged a degree at a time, since every degree would be undone before the next one arrived.

**How it is asked for** is the input layer's (`CTL-9`): a twist between two fingers, or the wheel with
control held.
