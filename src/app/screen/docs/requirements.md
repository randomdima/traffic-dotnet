# The chrome — requirements

The vocabulary a frame's overlay is written in: one quad, the glyphs, the colours and spacings, and the
buffer text is built in. **It is the bottom of the shell and knows nothing** — not the town, not an
agent, not the renderer that uploads what it produces.

It exists as a slice of its own because the interface and the debug layers both write the same quads,
and while the drawing kit lived inside one of them the other had to reach across
([docs/slice-map.md](../../../../docs/slice-map.md)).

## What it is

- **One instance type for everything drawn over the town** — a panel, a glyph, a tape, a ring, a debug
  line. The interface and the debug layers are nothing else, and one pipeline draws all of it.
- **A quad whose ends may be cut oblique.** The one thing it is not a rectangle for: it is what lets a
  band follow a bend as one shape, each piece cut square to the line so the next begins on the same cut.
  Everything else drawn is a rectangle and says so with a taper of zero.
- **An immediate-mode writer** over a span the renderer already owns: nothing is allocated, nothing is
  uploaded, and a closed panel writes zero quads so its draw becomes a no-op the GPU skips.
- **One theme**, so that two panels drawing their own chrome cannot read as two interfaces. Colours and
  spacings are here rather than at the call sites that use them.
- **One glyph sheet**, cut offline by a workshop tool and committed. Text is measured here and drawn
  here; nothing else knows how wide a character is.

## What binds it

- **Nothing above may be named.** A type here that took a `TownWorld`, a fleet or a renderer would put
  the bottom of the shell above its own top.
- **The interface is in the window's own pixels**, not in world space, so a zoom does not resize it and
  a pointer position is converted once at the boundary
  ([app/hud](../../hud/docs/requirements.md#the-interface-is-in-the-windows-own-pixels)).
- **The steady state allocates nothing**, which is why the writer is a `ref struct` over a borrowed span
  and text is built in a stack buffer.
