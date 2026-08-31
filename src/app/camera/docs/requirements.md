# The camera — requirements

**OBS-1** Top-down, pannable and zoomable, so that **both the whole town and individual agents can be
watched**.

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
would otherwise leave what the reader opened on half off the screen. The first pan or zoom is theirs and
ends it (OBS-1a).
