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

**Viewport equals window, 1:1, no letterbox bars** — so the camera alone decides how much world is on
screen, and the interface's own pixels are the window's ([app/hud](../../hud/docs/requirements.md)).
