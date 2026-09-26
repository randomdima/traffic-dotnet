# The chrome — requirements

The vocabulary a frame's overlay is written in: one quad, the glyphs, the colours and spacings, and the
buffer text is built in. **It is the bottom of the shell and knows nothing** — not the town, not an
agent, not the renderer that uploads what it produces. It is a slice of its own because the interface and
the debug layers both write the same quads, and while the kit lived inside one of them the other had to
reach across ([docs/slice-map.md](../../../../docs/slice-map.md)).

## What it is

- **One instance type for everything drawn over the town**, and one pipeline for all of it
  (`OverlayQuad`) — a rectangle, except that its ends may be cut oblique so a band follows a bend as one
  shape.
- **An immediate-mode writer** over a span the renderer already owns (`ScreenDraw`): nothing allocated,
  nothing uploaded, and a closed panel writes zero quads.
- **One theme** (`Theme`), so two panels cannot read as two interfaces, and **one glyph sheet**
  (`GlyphSheet`), cut offline by a workshop tool and committed; nothing else knows how wide a character is.

## What binds it

- **Nothing above may be named.** A type here that took a `TownWorld`, a fleet or a renderer would put
  the bottom of the shell above its own top.
- **The interface is in the window's own pixels**, not in world space
  ([app/hud](../../hud/docs/requirements.md#the-interface-is-in-the-windows-own-pixels)).
- **The steady state allocates nothing** ([goals.md](../../../../docs/goals.md) rule 2), which is why the
  writer is a `ref struct` over a borrowed span and text is built in a stack buffer.
