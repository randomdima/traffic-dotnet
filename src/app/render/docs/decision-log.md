# Drawing the town — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md).

## 2026-08-26 — a casualty is art, not a tint

A body in the road (`PER-18`) needed a picture, and the cheap answer was sitting right there: the instance
already carries a per-instance `Tint`, so a red multiply and a quarter turn would have been two lines and
no art at all.

It was refused because **the tint is how a mark says how hard it was pressed**, and nothing else in the
town uses it to say what a body *is*. What a body is, is which sheet it samples — a wrecked car has
carried its own crumpled picture since it existed, and a walker's facing is which cell of eight. A red
multiply over the walk sheet would have been a second grammar for state, and one that says "this walker
is redder" rather than "this walker is on the ground bleeding".

So a look now ships two sheets: the walk grid, and one frame of a body on the ground. It costs one sheet
slot a look, on a list the sprite pipeline indexes by number and never binds, and it buys the pose for
free — a body on the ground is a shape with a direction, so it is drawn the way a car is, one frame turned
to its heading.

**That second sheet is cut from the first offline** by
[tools/personsheets](../../../tools/personsheets/make-down-sheets.py), on the same footing as the lit
frames of a signal head: the plant pose of the away-facing row, turned a quarter and shaded red. Drawing it
fresh was tried first and thrown away — new art of the same character is new art, and seven looks of it
were seven ways to be slightly out of style beside the walker they are supposed to be. A look's casualty
is now literally the same pixels as the look.
