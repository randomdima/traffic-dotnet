#!/usr/bin/env python3
"""Cut each walker look's 'lying in the road' frame out of the walk sheet it already ships.

Run it from the project root when a walker's art changes, and commit what it writes;
it is never run at build time. The output is `<id>_down.png` beside `<id>.png`, which
is the one frame a casualty is drawn as (PER-18) — see app/render's decision log for
why it is cut from the walk sheet rather than drawn.

Needs Pillow, which nothing else here does: this runs by hand a few times a decade.
"""
import pathlib
from PIL import Image

VARIANTS = pathlib.Path("assets/agents/person/variants")

COLUMNS, ROWS = 8, 8

# The plant pose of the away-facing row, which is the one frame that reads as a whole
# body rather than as a stride.
STANDING_COLUMN, AWAY_ROW = 0, 0

BLOOD = (168, 22, 26)

# Far enough to read as blood at a district framing, near enough to leave the look's
# own colours legible: a walker's casualty has to still look like that walker.
SHADE = 0.3
DARKEN = 0.88


for sheet in sorted(VARIANTS.glob("*/*.png")):
    if sheet.stem.endswith("_down"):
        continue

    walk = Image.open(sheet).convert("RGBA")
    frameW, frameH = walk.width // COLUMNS, walk.height // ROWS
    frame = walk.crop(
        (
            STANDING_COLUMN * frameW,
            AWAY_ROW * frameH,
            (STANDING_COLUMN + 1) * frameW,
            (AWAY_ROW + 1) * frameH,
        )
    )

    box = frame.getbbox()
    if box:
        frame = frame.crop(box)

    # Clockwise, so the head — which the sheet draws at the top — ends up along +x,
    # which is where the renderer turns the quad from.
    frame = frame.rotate(-90, expand=True)

    pixels = frame.load()
    for y in range(frame.height):
        for x in range(frame.width):
            r, g, b, a = pixels[x, y]
            if a == 0:
                continue

            pixels[x, y] = (
                round((r + ((BLOOD[0] - r) * SHADE)) * DARKEN),
                round((g + ((BLOOD[1] - g) * SHADE)) * DARKEN),
                round((b + ((BLOOD[2] - b) * SHADE)) * DARKEN),
                a,
            )

    into = sheet.with_name(f"{sheet.stem}_down.png")
    frame.save(into)
    print(f"{into}  {frame.width}x{frame.height}")
