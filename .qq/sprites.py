"""
Lay a sheet's template, cut a generated frame into sprites, keep and use them, and trace a sprite to its prompt.

Most art is generated (the remotegen skill), nine or more assets to a frame. A frame is scratch in .tmp/gen/;
what is kept is each asset's cut variant, under raw_assets/ at the path its sprite has under assets/, with
the record of the frame it came from and the pictures that frame was asked from — so any sprite in the game
traces back to its first prompt, can be asked again, and has its variants beside it to compare. The rules
are raw_assets/index.md.

    qq sprites template house_03 house_07 ... --out=.tmp/gen/templates/house_a.png
    qq sprites cut roofs_house_a-v1              every cell keyed and clipped; flags holes, spill, a part not drawn,
                                                 and marks them on cuts/<frame>/_overlay.png
    qq sprites mask roofs_house_a-v1 house_07    the frame with those cells transparent, for a remotegen --mask redo
    qq sprites keep roofs_house_a-v1 house_03 house_07 --score=4
    qq sprites use house_03-v2                   the variant becomes the game's sprite, at the art grid
    qq sprites trace house_03                    sprite -> variant -> frame(s) -> prompt and inputs
    qq sprites compare house_03                  every kept variant of one asset beside the sprite
    qq sprites check                             every image under assets/ traces, or is on the legacy list

A car's wreck is asked, kept and used as `<car id>_broken` (`hatch_coral_broken`): the car's outline with nothing
framed in it, installed as the file's `wreck.sprite`. Its zone map is `<car id>_zones`: the sheet its sprite was cut
from, repainted by the generator in flat zone colours (ZONE_COLOURS), installed as the file's `zones` and read by the
lamp bake (CAR-14a):

    remotegen edit --image cars_civil_b-v3 -p "<zone brief>" --name cars_civil_b_zones
    qq sprites cut cars_civil_b_zones-v1          fitted like any frame, quantised to the zones; flags a lamp unzoned
    qq sprites keep cars_civil_b_zones-v1 hatch_coral_zones && qq sprites use hatch_coral_zones-v1

The template carries its cells in the PNG itself (a `cells` text chunk), and remotegen keeps every picture
it sends by content hash, so `cut` needs nothing but the frame's id.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import shutil
import time
from pathlib import Path

import _sim
import art
from common import cli, paths

#: What a call may take: `use` re-encodes a WebP to an error bound, `check` reads every variant file.
TIMEOUT_S = 120

GEN = Path(".tmp/gen")
FRAMES, INPUTS, INDEX = GEN / "frames", GEN / "inputs", GEN / "index.jsonl"
CUTS, COMPARE = GEN / "cuts", GEN / "compare"

RAW = Path("raw_assets")
KEPT_FRAMES, KEPT_INPUTS = RAW / "_frames", RAW / "_inputs"
LEGACY = RAW / "legacy.txt"
VARIANTS = "variants.jsonl"
SOURCE = ".src.json"
IMAGES = (".png", ".webp")

#: GPT Image 2.5 Sunburst at 3:2, the only frame the remotegen skill asks for: it takes no resolution.
SHEET_PX = (1536, 1024)
#: The owner's floor: never fewer assets to a frame (remotegen SKILL.md).
MIN_CELLS = 9
#: The owner's allowance (2026-10-20): building roofs a 3x3 cell holds under their grid may go fewer to a frame, down to
#: this, laid in whichever grid draws them largest — never a frame of one.
BIG_ROOF_MIN_CELLS, BUILDINGS = 2, "assets/world/building/"
#: The magenta such a frame keeps round its cells: the model will not paint within some 25 px of the picture's edge, so
#: an outline laid there came back 10-30 % short (roofs_mixed_ab, _al, _u).
BIG_ROOF_GAP_PX = 64

KEY, OUTLINE, FILL = (255, 0, 255), (30, 30, 30), (235, 235, 235)
BORDER_PX, FRONT_PX = 3, 7
#: Magenta kept between an outline and its cell's edge, so no two drawings touch.
GAP_PX = 24
#: Background cut around an outline, so the key's flood starts on background however far a roof spilled.
MARGIN_PX = 12
#: Past these a cell is flagged for a look: background showing inside the outline, paint outside it.
HOLES_MAX, SPILL_MAX = 0.005, 0.01
#: `cut` fits a drawn body onto its shape (`register`) by at most this much scale per axis; past it the drawing is
#: not the asset asked for. A row or column with fewer solid pixels than FIT_MIN_PX is a speck, not the body's edge.
FIT_SCALE_MAX, FIT_MIN_PX = 0.08, 3
#: A long thin asset laid in rows (a train's vehicle, a track) the model draws some 12 % wide however the brief puts it,
#: and a short one as much too long (raw_assets/agents/train/prompting.md); the owner allowed a minor deviation there
#: (2026-10-19), so such a drawing is fitted this far.
FIT_ROWS_MAX = 0.15
#: The level crossing's small gear (housings, cabinets, heads) the model draws 10 to 16 % over its outline in its own
#: proportions however the brief puts it (railway_gear_a-v1..v3); the owner allowed fitting it that far (2026-10-19).
FIT_GEAR_MAX, GEAR_PREFIX = 0.16, "assets/world/rail/looks/"
#: At least this many times longer than wide, an asset is measured across by the median of its long edges, as a car is
#: by its flanks: a barrier boom's end cap is drawn a few pixels proud of a 35 px boom every time (railway_gear_a-v1, v2),
#: which read off the extent is 15 % too thick, and the cut clips it to the outline anyway.
#: Ten, so no rail vehicle (at most some seven) is measured by it.
THIN_ASPECT = 10
#: How `cut`'s overlay marks them on the frame, for the eye that judges the flag.
OVERLAY_HOLE, OVERLAY_SPILL = (255, 0, 0), (0, 255, 255)
#: Channel distance from the measured key: background below KEY_HARD, paint above KEY_SOFT, an anti-aliased edge
#: between. A model never returns #FF00FF itself (a muted rose), so the key is measured from the frame's border.
KEY_HARD, KEY_SOFT = 40, 110
#: Under this share of the frame's border in the key's colour, the background is not flat and the cut is suspect.
KEY_FLAT_MIN = 0.5
#: A frame asked with a transparent background (the owner, 2026-10-20) whose border is at least this share clear is
#: keyed by its own alpha, which has no key colour to fringe the edge with; one the model painted opaque anyway is
#: chroma-keyed. The overlays lay a transparent frame over SHOWN_KEY, to be looked at.
CLEAR_BORDER_MIN = 0.9
SHOWN_KEY = (255, 0, 255)
#: Below this an island of opaque pixels is a leftover, not part of anything (`CarSpriteTests.DustPx`).
DUST_PX = 8
#: `qq art --error`'s default: how far a WebP may move an opaque pixel.
WEBP_ERROR = 6.0

#: Per family, by its path under assets/: the art grid it is drawn at (a figure `qq art` reads off the engine), the
#: edge its front is on, drawn thick on the template, and how its cells are laid. A car's sheet has its nose along +x
#: (CarCatalog); a building's street front is at the bottom. A grid's cells are equal and the scale is what the
#: largest fits; `rows` packs the cells along rows at the family's own grid, for assets far longer than wide — a
#: train's vehicles (TRN-4), which a 3x3 grid would draw at well under the grid.
FAMILIES = {"agents/car/": ("CarSpritePixelsPerMetre", "right", "grid"), "agents/train/": ("ArtPixelsPerMetre", "right", "rows"),
            "world/rail/": ("ArtPixelsPerMetre", "right", "grid")}
DEFAULT_FAMILY = ("ArtPixelsPerMetre", "bottom", "grid")
#: A variant file of this kind is a strip the ground repeats along its length (RAIL-3): no front to mark, the two
#: rails' lines guided on its template at the gauge `RAIL_FIGURES` states, and installed seamless (`seamless`).
TRACK = "Track"
RAIL_FIGURES = "src/core/config/WorldFigures.cs"
#: How a rail's guide line is drawn on a track's template: thin, in the steel the brief asks it painted over in.
GUIDE, GUIDE_PX = (150, 150, 155), 2
#: And where its sleepers end, the track file's own `sleeperLengthM`: dashed, in the ballast's brown.
SLEEPER_END, DASH_PX = (150, 120, 90), 6
#: What an asset switches, by the key its variant file lists the parts under — each a `fitting`, `atM` from the
#: footprint's centre in the sprite's axes (x right, y down) and `sizeM`, the area it is drawn inside in whatever
#: shape the art gives it — and per fitting the colour its area is framed in on the template and the words the brief
#: asks for it in. The picture draws every part off; its zone map (ZONES) says which of its texels each part is, and
#: the lit state is those texels burning (CAR-14a, raw_assets/index.md).
PARTS = {"lamps": {
    "rear": ((120, 25, 25), "rear lamp, dark red glass"),
    "brake": ((120, 25, 25), "tail and brake lamp, dark red glass"),
    "reverse": ((95, 95, 105), "reversing lamp, clear white glass"),
    "indicator": ((150, 100, 20), "indicator, amber glass"),
    "head": ((30, 110, 150), "headlamp, clear glass"),
    "beaconRed": ((120, 25, 40), "red beacon dome"),
    "beaconBlue": ((25, 40, 130), "blue beacon dome"),
    "beaconAmber": ((150, 100, 20), "amber beacon dome"),
}}
#: A car's wreck is an asset of its own, `<car id>` + this: the car's outline and footprint with no part framed, since
#: a wreck burns nothing (CAR-14), installed as its file's `wreck.sprite` beside the car's own sprite.
WRECK = "_broken"
#: A car's zone map is an asset of its own too, `<car id>` + this: the sheet its sprite was cut from, repainted by
#: the generator with every part of each car one flat colour, and cut with its own fit — it is drawn to the outline
#: as the sheet was, so both land on the shape. A frame is a zone map by its name: `--name <sheet>_zones`.
ZONES = "_zones"
#: The colours a zone map is painted in, the brief's and `LampAtlas.Zone`'s: a lamp lens by its fitting, and the rest
#: of the car. A cut is quantised to the nearest; the bake reads only the lamps'.
ZONE_PARTS = {"rear": (255, 0, 0), "brake": (255, 0, 0), "reverse": (0, 255, 0), "indicator": (255, 255, 0),
              "head": (0, 0, 255)}
ZONE_COLOURS = {**ZONE_PARTS, "body": (255, 255, 255), "glass": (0, 0, 0), "trim": (128, 128, 128)}
#: How far past a part's framed area `cut` looks for its zone: the generator draws a lens a few centimetres off it.
ZONE_REACH_M = 0.1
#: How far past its frames towards the other end `mask id:nose`/`id:tail` reaches: a corner lamp is drawn wrapped
#: onto the flank.
END_WRAP_M = 0.25
#: Under this share of a part's area painted at least PART_CONTRAST (channel distance) off the bodywork's median, the
#: frame drew no part there: a skipped lamp comes back as plain paint, not as a hole. A share and not the area's
#: mean, since a round lens leaves its area's corners bodywork.
PART_SHARE, PART_CONTRAST = 0.2, 30
#: How thick a part's frame is drawn on the template. Two: at one a lens of a dozen pixels in dark amber was
#: drawn over as if it were not there (cars_civil_b-v2, raw_assets/agents/car/prompting.md).
PART_FRAME_PX = 2
#: `cut`'s `_parts.png`: how much of the body around a part area it shows, and how far it magnifies.
PARTS_MARGIN_PX, PARTS_ZOOM = 8, 4

NAME = re.compile(r"^[A-Za-z0-9_.\-]+(:whole|:nose|:tail)?$")
VERSIONED = re.compile(r"^(.+)-v(\d+)$")


def main():
    a = parse_args()
    base = _sim.root()
    return {"template": template, "cut": cut, "keep": keep, "use": use, "trace": trace,
            "compare": compare, "check": check, "mask": mask, "drop": drop}[a.action](base, a)


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq sprites",
        description="Lay a sheet's template, cut a generated frame into sprites, keep and use them, and trace "
                    "a sprite to its prompt.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("action", choices=["template", "cut", "mask", "keep", "use", "drop", "trace", "compare", "check"],
                        help="template IDS | cut FRAME | mask FRAME IDS (an id, id:whole for its grid cell, id:nose "
                             "or id:tail for one end's lamps) | keep FRAME IDS | use VARIANT | drop VARIANTS | trace ID "
                             "| compare ID | check")
    parser.add_argument("refs", nargs="*", type=identifier,
                        help="asset ids (house_03), a frame id (roofs_house_a-v1) or a variant (house_03-v2)")
    parser.add_argument("--out", default=".tmp/gen/templates/sheet.png", help="template: where the layout goes")
    parser.add_argument("--style-cols", type=int, choices=[1, 2, 3], default=3,
                        help="template: how many style sprites to a row")
    parser.add_argument("--style-whole", action="store_true",
                        help="template: style sprites taken whole from the frames they were drawn on, not clipped at "
                             "their outlines (mirrors kept)")
    parser.add_argument("--style-width", type=cli.counting, default=0, metavar="PX",
                        help="template: the widest the style picture may be, its sprites shrunk to fit (0 = the sheet's)")
    parser.add_argument("--style", default="",
                        help="template: ids whose in-game sprites make the style picture, beside --out as <stem>_style.png")
    parser.add_argument("--score", type=int, choices=range(6), default=None,
                        help="keep: the variant's score by eye, 0-5")
    parser.add_argument("--recut", action="store_true",
                        help="keep: keep a cell already kept from this frame again, as a new version naming the one it "
                             "re-cuts — after `cut` itself changed")
    parser.add_argument("--full", action="store_true", help="trace: print every prompt whole")
    return parser.parse_args()


def identifier(text):
    if not NAME.fullmatch(text):
        raise argparse.ArgumentTypeError(f"not an id: {text!r}")
    return text


# ---------------------------------------------------------------------------------------------- template

def template(base, a):
    from PIL import Image, ImageDraw, PngImagePlugin

    found = assets(base)
    bodies = [body(base, found, i) for i in a.refs]
    figures = art.read_figures(base)
    if len(bodies) < MIN_CELLS:
        small = [b["id"] for b in bodies if not big_roof(b, figures)]
        if len(bodies) < BIG_ROOF_MIN_CELLS or small:
            cli.fail(f"a frame holds at least {MIN_CELLS} assets, or {BIG_ROOF_MIN_CELLS} building roofs a 3x3 cell "
                     f"holds under their grid; given {len(bodies)}" + (f", {', '.join(small)} not such a roof" if small else ""))
    layouts = {family(b["asset"])[2] for b in bodies}
    if len(layouts) > 1:
        cli.fail("a frame lays one family's cells: these ids are laid both as a grid and in rows")
    if layouts == {"rows"}:
        scale, slots = packed(bodies, min(grid(figures, b["asset"]) for b in bodies))
        cols, rows = None, len({s[1] for s in slots})
    else:
        few = len(bodies) < MIN_CELLS
        cols, rows = few_grid(bodies) if few else grid_of(len(bodies))
        scale = grid_scale(bodies, cols, rows, BIG_ROOF_GAP_PX if few else GAP_PX)
        cw, ch = SHEET_PX[0] / cols, SHEET_PX[1] / rows
        slots = [(round((n % cols) * cw), round((n // cols) * ch), round((n % cols + 1) * cw), round((n // cols + 1) * ch))
                 for n in range(len(bodies))]

    # Magenta and not clear: outlines on a clear layout do not hold the model, which lays its own grid
    # (fleet100_w01-v1, raw_assets/agents/car/prompting.md).
    sheet = Image.new("RGB", SHEET_PX, KEY)
    draw = ImageDraw.Draw(sheet)
    cells = []
    for n, b in enumerate(bodies):
        w, h = round(b["w"] * scale), round(b["d"] * scale)
        x0, y0, x1, y1 = slots[n]
        x = round(x0 + (x1 - x0 - w) / 2)
        y = round(y0 + (y1 - y0 - h) / 2)
        sw, sh = round(b["sw"] * scale), round(b["sd"] * scale)
        sx, sy = x + (w - sw) // 2 + round(b["offM"][0] * scale), y + (h - sh) // 2 + round(b["offM"][1] * scale)
        r = min(round(b["r"] * scale), sw // 2, sh // 2)
        walls = [[round(x + w / 2 + (a[0] - s[0] / 2) * scale) - sx, round(y + h / 2 + (a[1] - s[1] / 2) * scale) - sy,
                  round(s[0] * scale), round(s[1] * scale)] for a, s in b["walls"]]
        outline(draw, b["shape"], (sx, sy, sw, sh), r, FILL, OUTLINE, walls)
        if b["track"]:
            for offM in (-rail_offset_m(base), rail_offset_m(base)):
                gy = round(sy + sh / 2 + offM * scale)
                draw.line([sx + BORDER_PX, gy, sx + sw - 1 - BORDER_PX, gy], fill=GUIDE, width=GUIDE_PX)
            # And where the sleepers end, dashed: past them is bare ballast to the strip's edge, which a model left to
            # itself draws a pixel or two wide (railway_a-v1).
            for offM in (-b["sleeperM"] / 2, b["sleeperM"] / 2):
                gy = round(sy + sh / 2 + offM * scale)
                for gx in range(sx + BORDER_PX, sx + sw - BORDER_PX, 2 * DASH_PX):
                    draw.line([gx, gy, min(gx + DASH_PX, sx + sw - 1 - BORDER_PX), gy], fill=SLEEPER_END, width=GUIDE_PX)
        elif b["shape"] == "box":
            inset = FRONT_PX // 2
            edge = {"bottom": [sx + r, sy + sh - inset, sx + sw - r, sy + sh - inset],
                    "right": [sx + sw - inset, sy + r, sx + sw - inset, sy + sh - r]}[family(b["asset"])[1]]
            draw.line(edge, fill=OUTLINE, width=FRONT_PX)
        elif walls:
            # The front is the bottom edge of the rectangle reaching furthest towards the street.
            fx, fy, fw, fh = max(walls, key=lambda v: v[1] + v[3])
            gy = sy + fy + fh - FRONT_PX // 2
            draw.line([sx + fx, gy, sx + fx + fw - 1, gy], fill=OUTLINE, width=FRONT_PX)
        shape = Image.new("L", SHEET_PX, 0)
        outline(ImageDraw.Draw(shape), b["shape"], (sx, sy, sw, sh), r, 255, None, walls)
        parts = []
        for p in b["parts"]:
            pw, ph = max(2, round(p["sizeM"][0] * scale)), max(2, round(p["sizeM"][1] * scale))
            px = round(x + w / 2 + p["atM"][0] * scale - pw / 2)
            py = round(y + h / 2 + p["atM"][1] * scale - ph / 2)
            draw.rectangle([px, py, px + pw - 1, py + ph - 1], outline=part_kind(p)[0], width=PART_FRAME_PX)
            parts.append({"fitting": p["fitting"], "box": [px, py, pw, ph]})
            if shape.crop((px, py, px + pw, py + ph)).getextrema()[0] < 255:
                b["outside"] += 1
        cells.append({"n": n + 1, "id": b["id"], "asset": b["asset"], "shape": b["shape"], "box": [x, y, w, h],
                      "shapeBox": [sx, sy, sw, sh], "radiusPx": r, "standOff": b["standOff"], "parts": parts,
                      "bounds": list(slots[n]), "walls": walls})

    info = PngImagePlugin.PngInfo()
    info.add_text("cells", json.dumps({"size": list(SHEET_PX), "pxPerM": round(scale, 3), "cells": cells}))
    out = paths.resolve_in_root(a.out, base)
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out, pnginfo=info)

    asked = set(a.refs)
    if a.style:
        style(base, found, [s for s in a.style.split(",") if s], asked, out.with_name(out.stem + "_style.png"),
              a.style_cols, a.style_width, a.style_whole)

    fronts = sorted({family(b["asset"])[1] for b in bodies if b["shape"] in ("box", "walls") and not b["track"]})
    if any(b["track"] for b in bodies):
        print("a track strip has no front: the two thin grey lines along it are where its rails are painted, over them")
    print(f"{_sim.rel(base, out)}: {f'{cols}x{rows}' if cols else f'{rows} rows'}, {scale:.1f} px/m"
          + (f", front at the thick {' / '.join(fronts)} edge" if fronts else ""))
    if any(b["parts"] for b in bodies):
        print("each thin coloured frame inside an outline is the area a part is drawn in, switched off (unlit glass, "
              "no glow, no light cast), in whatever shape suits the vehicle; the frame itself is not drawn")
    for b, c in zip(bodies, cells):
        under = scale < grid(figures, b["asset"])
        print(f"{c['n']:>2}. {b['id']:<22} {size_phrase(b)}{'  (under the grid)' if under else ''}"
              f"{' - ' + b['describe'] if b['describe'] else ''}{parts_phrase(b['parts'])}")
    outside = [f"{b['id']} ({b['outside']})" for b in bodies if b["outside"]]
    if outside:
        print(f"warning: parts reaching past their asset's shape, which the art cannot draw them on: "
              f"{', '.join(outside)}")
    return 0


def packed(bodies, scale):
    """The cells laid along rows in the order asked, each its own width and a gap either side, a row as tall as its
    tallest — at the grid, or as far under it as the sheet needs to hold them all. (scale, [(x0, y0, x1, y1)])."""
    while True:
        slots, rows, x, row = [], [], 0, []
        for b in bodies:
            w, h = b["w"] * scale + 2 * GAP_PX, b["d"] * scale + 2 * GAP_PX
            if row and x + w > SHEET_PX[0]:
                rows.append(row)
                row, x = [], 0
            row.append((x, w, h))
            x += w
        rows.append(row)
        heights = [max(h for _, _, h in r) for r in rows]
        if sum(heights) <= SHEET_PX[1] and all(x + w <= SHEET_PX[0] for r in rows for x, w, _ in r):
            break
        scale *= 0.97
    y = (SHEET_PX[1] - sum(heights)) / 2
    for r, height in zip(rows, heights):
        spare = (SHEET_PX[0] - sum(w for _, w, _ in r)) / len(r)
        for k, (x, w, _) in enumerate(r):
            left = x + spare * (k + 0.5)
            slots.append((round(left), round(y), round(left + w), round(y + height)))
        y += height
    return scale, slots


def grid_of(cells):
    """(columns, rows) a template lays this many cells in — `cut` reads a cell's bounds off it too."""
    rows = max(3, math.ceil(math.sqrt(cells * SHEET_PX[1] / SHEET_PX[0])))
    return max(3, math.ceil(cells / rows)), rows


def grid_scale(bodies, cols, rows, gap=GAP_PX):
    """The px/m the largest of these fits a cell of a cols x rows grid at, `gap` of magenta kept round each."""
    cw, ch = SHEET_PX[0] / cols, SHEET_PX[1] / rows
    return min(min((cw - 2 * gap) / b["w"], (ch - 2 * gap) / b["d"]) for b in bodies)


def few_grid(bodies):
    """The grid a frame of fewer than MIN_CELLS big roofs is laid in: of those that hold them all, the one drawing
    them largest. The template records each cell's bounds, so `cut` never has to know which it was."""
    n = len(bodies)
    shapes = [(c, -(-n // c)) for c in range(1, n + 1)]
    return max(shapes, key=lambda s: grid_scale(bodies, *s, BIG_ROOF_GAP_PX))


def big_roof(b, figures):
    """Whether an asset is a building roof a 3x3 cell holds under its grid (BIG_ROOF_MIN_CELLS)."""
    return b["asset"].startswith(BUILDINGS) and grid_scale([b], 3, 3) < grid(figures, b["asset"])


def cell_bounds(layout, c):
    """The cell a template cell's box was centred in, as (x0, y0, x1, y1): as the template recorded it, or its grid's."""
    if "bounds" in c:
        return tuple(c["bounds"])
    cols, rows = grid_of(len(layout["cells"]))
    cw, ch = layout["size"][0] / cols, layout["size"][1] / rows
    n = c["n"] - 1
    return round((n % cols) * cw), round((n // cols) * ch), round((n % cols + 1) * cw), round((n // cols + 1) * ch)


def style(base, found, ids, asked, out, columns=3, widest_px=0, whole=False):
    """The style picture: the in-game sprites of other assets, laid on a clear background at one scale, as a frame is
    now asked to come back. It holds none of the assets asked for, so it shows the look and not the answer. `whole`
    takes each from the frame it was drawn on instead, keyed and not clipped at its outline, so a mirror or a bumper
    drawn past the footprint is shown as drawn."""
    from PIL import Image

    if not ids:
        cli.fail("--style names no asset")
    if overlap := sorted(asked.intersection(ids)):
        cli.fail(f"the style picture would hand the model its answer: {', '.join(overlap)} also asked for")
    sprites = []
    for i in ids:
        if i not in found:
            cli.fail(f"no variant file under assets/ has the id {i}")
        if whole:
            sprites.append(drawn_whole(base, found, i))
            continue
        sprite = sprite_of(found[i], i)
        if not sprite or not (found[i].parent / sprite).is_file():
            cli.fail(f"{i} has no sprite to show as style")
        b = body(base, found, i)
        sprites.append((Image.open(found[i].parent / sprite).convert("RGBA"), b["w"], b["d"]))
    # As small as it can be without losing a texel: an input picture is paid for by its area, and the style is in
    # the sprites' own pixels, so they are laid at their own density (the sharpest of them), in rows of three, and
    # shrunk only to fit the sheet's width, or `widest_px`.
    cols = min(columns, len(sprites))
    rows = math.ceil(len(sprites) / cols)
    widest = widest_px or SHEET_PX[0]
    scale = max(img.width / wm for img, wm, _ in sprites)
    scale = min(scale, min((widest / cols - 2 * GAP_PX) / wm for _, wm, _ in sprites))
    cw = max(wm for _, wm, _ in sprites) * scale + 2 * GAP_PX
    ch = max(dm for _, _, dm in sprites) * scale + 2 * GAP_PX
    sheet = Image.new("RGBA", (round(cw * cols), round(ch * rows)), (0, 0, 0, 0))
    for n, (img, wm, dm) in enumerate(sprites):
        w, h = round(wm * scale), round(dm * scale)
        x, y = round((n % cols) * cw + (cw - w) / 2), round((n // cols) * ch + (ch - h) / 2)
        drawn = img.resize((w, h), Image.LANCZOS)
        sheet.paste(drawn, (x, y), drawn)
    sheet.save(out)
    print(f"{_sim.rel(base, out)}: style, {', '.join(ids)} at {scale:.1f} px/m" + (", whole from their frames" if whole else ""))


#: How far past its kept box a drawing is taken whole for a style picture, along it and across: the drawing lands up to
#: 0.3 m off its box (the cut's fit) and a mirror reaches past that, while the next cell along is closer than across.
WHOLE_MARGIN_M = (0.3, 0.6)


def drawn_whole(base, found, i):
    """The asset as its used variant's frame drew it, (picture, width m, depth m): the cell's box widened by
    WHOLE_MARGIN_M, keyed, and trimmed to the paint in it."""
    import numpy as np
    from PIL import Image

    listing = raw_dir(base, found[i], i) / VARIANTS
    used = next((r for r in read_jsonl(listing) if r["status"] == "used" and "box" in r), None)
    if used is None:
        cli.fail(f"{i} has no used variant cut from a cell of a frame ({_sim.rel(base, listing)})")
    keyed, solid = frame_pixels(base, used["frame"])
    mx, my = (round(v * used["pxPerM"]) for v in WHOLE_MARGIN_M)
    x, y, w, h = used["box"]
    x0, y0 = max(0, x - mx), max(0, y - my)
    x1, y1 = min(keyed.shape[1], x + w + mx), min(keyed.shape[0], y + h + my)
    ys, xs = np.nonzero(solid[y0:y1, x0:x1])
    crop = keyed[y0 + ys.min():y0 + ys.max() + 1, x0 + xs.min():x0 + xs.max() + 1]
    return Image.fromarray(crop, "RGBA"), crop.shape[1] / used["pxPerM"], crop.shape[0] / used["pxPerM"]


def body(base, found, i):
    """An asset's cell (w x d m, its sprite's rectangle) and the authored shape drawn in it (sw x sd m, radius
    r). A car authors its shape as `collisionM`, centred in a footprint that also holds what stands off the
    bodywork (CAR-12b): `standOff`, so its cut keeps paint between the shape and the footprint."""
    if i not in found:
        cli.fail(f"no variant file under assets/ has the id {i}")
    path = found[i]
    d = json.loads(path.read_text(encoding="utf-8"))
    walls, offM = [], (0.0, 0.0)
    if "footprintM" in d:
        w, depth = map(float, d["footprintM"])
        shape, r, (sw, sd), stand = "box", float(d.get("cornerRadiusM", 0.0)), (w, depth), False
        if d.get("partsM"):
            # A roof built of rectangles (OBJ-5a) is drawn to them: the outline is their union, its box theirs.
            shape, walls = "walls", [(p["atM"], p["sizeM"]) for p in d["partsM"]]
            x0 = min(a[0] - s[0] / 2 for a, s in walls)
            x1 = max(a[0] + s[0] / 2 for a, s in walls)
            y0 = min(a[1] - s[1] / 2 for a, s in walls)
            y1 = max(a[1] + s[1] / 2 for a, s in walls)
            sw, sd, offM = x1 - x0, y1 - y0, ((x0 + x1) / 2, (y0 + y1) / 2)
        if "collisionM" in d:
            (sw, sd), r, stand = map(float, d["collisionM"]["sizeM"]), float(d["collisionM"]["cornerRadiusM"]), True
    elif "diameterM" in d:
        w = depth = sw = sd = float(d["diameterM"])
        shape, r, stand = "disc", w / 2, False
    else:
        cli.fail(f"{_sim.rel(base, path)} states neither footprintM nor diameterM: no outline to draw")
    wreck = is_wreck(d, i)
    parts = [] if wreck else [dict(p, kind=key) for key in PARTS for p in d.get(key, [])]
    for p in parts:
        if p["fitting"] not in PARTS[p["kind"]]:
            cli.fail(f"{_sim.rel(base, path)}: {p['kind']} fitting {p['fitting']!r} has no mark in sprites.PARTS")
    describe = d.get("describe", "")
    return {"id": i, "asset": _sim.rel(base, path), "w": w, "d": depth, "shape": shape, "sw": sw, "sd": sd,
            "r": r, "standOff": stand, "describe": f"WRECK of the {describe}" if wreck and describe else describe,
            "parts": parts, "outside": 0, "track": d.get("kind") == TRACK, "sleeperM": float(d.get("sleeperLengthM", 0.0)),
            "walls": walls, "offM": offM}


def rail_offset_m(base):
    """How far each rail's middle stands off the track's line: half the gauge and half a rail's head, as the engine
    lays them (`RailFigures`), read off the file that states them rather than copied."""
    text = (base / RAIL_FIGURES).read_text(encoding="utf-8")
    figure = {n: float(v) for n, v in re.findall(r"public float (GaugeM|RailHeadWidthM) \{ get; init; \} = ([\d.]+)f;", text)}
    if len(figure) != 2:
        cli.fail(f"{RAIL_FIGURES} no longer states GaugeM and RailHeadWidthM — a track's template reads them from there")
    return (figure["GaugeM"] + figure["RailHeadWidthM"]) / 2


def is_wreck(d, i):
    """Whether the id asks for the wreck look of the variant file `d` rather than the file's own."""
    return i == d["id"] + WRECK


def is_zones(d, i):
    """Whether the id asks for the variant file `d`'s zone map (ZONES)."""
    return i == d["id"] + ZONES


def sprite_of(path, i):
    """The sprite file the id's look is installed as, by its variant file's name for it, or None."""
    d = json.loads(path.read_text(encoding="utf-8"))
    if is_wreck(d, i):
        return (d.get("wreck") or {}).get("sprite")
    return d.get("zones") if is_zones(d, i) else d.get("sprite")


def part_kind(p):
    """The part's template colour and the words it is asked for in."""
    return PARTS[p["kind"]][p["fitting"]]


def parts_phrase(parts):
    counts = {}
    for p in parts:
        words = part_kind(p)[1]
        counts[words] = counts.get(words, 0) + 1
    return "; off: " + "; ".join(f"{n}x {words}" for words, n in counts.items()) if counts else ""


def size_phrase(b):
    if b["shape"] == "disc":
        return f"disc {b['w']:g} m across"
    k = b["sw"] / b["sd"]
    # A front at the right edge makes x the length: a car is long and wide, a building wide and deep.
    wider, deeper = (("longer than wide", "wider than long") if family(b["asset"])[1] == "right"
                     else ("wider than deep", "deeper than wide"))
    ratio = f"{k:.1f}x {wider}" if k >= 1.05 else f"{1 / k:.1f}x {deeper}" if k <= 0.95 else "square"
    held = f" in a {b['w']:g} x {b['d']:g} m footprint" if b["standOff"] else ""
    built = f", outline built of {len(b['walls'])} rectangles" if len(b["walls"]) > 1 else ""
    return f"{round(b['sw'], 2):g} x {round(b['sd'], 2):g} m ({ratio}){held}{built}"


def outline(draw, shape, box, r, fill, line, walls=None):
    """An asset's shape in its box: a disc, a rounded box, or the union of its walls (each [x, y, w, h] from the
    box's corner), outlined as one where `line` is given — the walls filled in it and then their insides over it."""
    x, y, w, h = box
    if shape == "walls":
        if line:
            for wx, wy, ww, wh in walls:
                draw.rectangle([x + wx, y + wy, x + wx + ww - 1, y + wy + wh - 1], fill=line)
        inset = BORDER_PX if line else 0
        for wx, wy, ww, wh in walls:
            draw.rectangle([x + wx + inset, y + wy + inset, x + wx + ww - 1 - inset, y + wy + wh - 1 - inset], fill=fill)
        return
    if shape == "disc":
        draw.ellipse([x, y, x + w - 1, y + h - 1], fill=fill, outline=line, width=BORDER_PX if line else 0)
    else:
        draw.rounded_rectangle([x, y, x + w - 1, y + h - 1], radius=r, fill=fill, outline=line,
                               width=BORDER_PX if line else 0)


# --------------------------------------------------------------------------------------------------- cut

def cut(base, a):
    if len(a.refs) != 1:
        cli.fail("cut takes one frame id")
    import numpy as np
    from PIL import Image, ImageDraw, PngImagePlugin

    fid = a.refs[0]
    rec = frame_record(base, fid)
    layout = cells_of(base, rec)
    if "described" in layout:
        return cut_described(base, fid, rec, layout["described"])
    frame, rgb, key, flat = opened(base / rec["file"])
    if list(frame.size) != layout["size"]:
        cli.fail(f"{fid} is {frame.size[0]}x{frame.size[1]}, its template {layout['size'][0]}x{layout['size'][1]}: "
                 "a cut does not guess a scale")

    out = base / CUTS / fid
    out.mkdir(parents=True, exist_ok=True)
    zoning = zoned_sheet(base, fid, rec) is not None
    suffix = ZONES if zoning else ""
    keyed_by = "its alpha" if key is None else f"rgb{key}"
    print(f"{fid}: {len(layout['cells'])} cells at {layout['pxPerM']} px/m, keyed by {keyed_by} -> {_sim.rel(base, out)}"
          + (", a zone map" if zoning else ""))
    if key is not None and flat < KEY_FLAT_MIN:
        print(f"warning: only {flat:.0%} of the frame's border is the key's colour: the background is not flat")
    overlay = on_key(rgb).copy()
    for c in layout["cells"]:
        x, y, w, h = c["box"]
        x0, y0 = max(0, x - MARGIN_PX), max(0, y - MARGIN_PX)
        x1, y1 = min(frame.width, x + w + MARGIN_PX), min(frame.height, y + h + MARGIN_PX)
        track = json.loads((base / c["asset"]).read_text(encoding="utf-8")).get("kind") == TRACK
        moved, fit = register(frame, rgb, key, c, cell_bounds(layout, c), track)
        crop = (np.asarray(moved).astype(np.int16) if moved else rgb)[y0:y1, x0:x1]
        keyed = Image.fromarray(chroma_key(crop, key), "RGBA")
        overlay[y0:y1, x0:x1] = on_key(crop)

        sx, sy, sw, sh = c["shapeBox"]
        mask = Image.new("L", keyed.size, 0)
        outline(ImageDraw.Draw(mask), c["shape"], (sx - x0, sy - y0, sw, sh), c["radiusPx"], 255, None, c.get("walls"))
        inside = np.asarray(mask) > 127
        if c["standOff"]:
            # Mirrors and tyres may stand off the shape as far as the footprint: the cut keeps the cell.
            mask = Image.new("L", keyed.size, 0)
            ImageDraw.Draw(mask).rectangle([x - x0, y - y0, x - x0 + w - 1, y - y0 + h - 1], fill=255)
        kept = np.asarray(mask) > 127
        solid = np.asarray(keyed)[..., 3] > 128
        holes = 1 - (solid & inside).sum() / inside.sum()
        spill = (solid & ~kept).sum() / inside.sum()
        here = overlay[y0:y1, x0:x1]
        here[inside & ~solid] = OVERLAY_HOLE
        here[solid & ~kept] = OVERLAY_SPILL

        pixels = np.asarray(keyed).copy()
        pixels[..., 3] = (pixels[..., 3].astype(np.uint16) * np.asarray(mask) // 255).astype(np.uint8)
        if zoning:
            pixels = quantised(pixels)
        pixels[pixels[..., 3] == 0] = 0
        variant = Image.fromarray(pixels, "RGBA").crop((x - x0, y - y0, x - x0 + w, y - y0 + h))
        info = PngImagePlugin.PngInfo()
        info.add_text("cut", json.dumps({"frame": fid, "cell": c["n"], "box": c["box"], "pxPerM": layout["pxPerM"],
                                         "fit": fit if moved else None}))
        variant.save(out / f"{c['id']}{suffix}.png", pnginfo=info)

        paint = np.asarray(keyed)[..., :3].astype(np.int16)
        if zoning:
            reach = round(ZONE_REACH_M * layout["pxPerM"])
            missing = [p["fitting"] for p in c.get("parts", [])
                       if not zoned(pixels, p, (x0, y0), reach)]
        else:
            bodywork = np.median(paint[solid & inside], axis=0) if (solid & inside).any() else None
            missing = [p["fitting"] for p in c.get("parts", []) if not drawn(paint, solid, bodywork, p["box"], x0, y0)]
        flags = [f for f, bad in (("holes", holes > HOLES_MAX), ("spill", spill > SPILL_MAX)) if bad]
        flags += [f"{'unzoned' if zoning else 'no'} {missing.count(f)}x {f}" for f in dict.fromkeys(missing)]
        flags += [] if moved else [f"not fitted: {fit['refused']}"]
        shown = (f"fit {fit['shiftPx'][0]:+3d},{fit['shiftPx'][1]:+3d} px x{fit['scale'][0]:.3f} y{fit['scale'][1]:.3f}"
                 if "shiftPx" in fit else f"{'unfitted':<30}")
        print(f"{c['n']:>2} {c['id']:<22} {shown}  holes {holes:6.1%}  spill {spill:6.1%}  "
              f"{', '.join(flags) or 'ok'}")
    marked = Image.fromarray(overlay.astype(np.uint8), "RGB")
    draw = ImageDraw.Draw(marked)
    for c in layout["cells"]:
        x, y, w, h = c["box"]
        draw.rectangle([x, y, x + w - 1, y + h - 1], outline=OVERLAY_SPILL)
        for p in c.get("parts", []):
            px, py, pw, ph = p["box"]
            draw.rectangle([px, py, px + pw - 1, py + ph - 1], outline=(255, 255, 255))
    marked.save(out / "_overlay.png")
    print(f"{_sim.rel(base, out / '_overlay.png')}: holes red, spill and the kept box cyan, part areas white")
    if parts_sheet(out, layout["cells"], suffix):
        print(f"{_sim.rel(base, out / '_parts.png')}: every part area magnified in its own colour, a row a cell — "
              "`drawn` only says paint differs there, this says whether it is the part")
    return 0


#: How far a drawing's proportion may stray from its file's before `cut` says so: the drawing is the car either way
#: (the owner, 2026-10-20), this only tells the eye which cells to look at first.
PROPORTION_FLAG = 0.08
#: The lamp zones a car's zone map is read for, by fitting.
LENS_ZONES = {f: ZONE_PARTS[f] for f in ("head", "indicator", "brake", "reverse")}
#: How far each wheel centre stands outside the flank it is measured under: CAR-12 owes a share of every tyre past it.
TYRE_PROUD_M = 0.03


def frame_pixels(base, fid):
    """A frame keyed whole, RGBA uint8, by its alpha or its border's colour (`opened`), and its solid mask."""
    rec = frame_record(base, fid)
    _, pixels, key, _ = opened(base / rec["file"])
    keyed = chroma_key(pixels, key)
    return keyed, keyed[..., 3] > 128


def cut_described(base, fid, rec, ids):
    """Cut a frame asked without a layout: each drawing found and named by the brief's order, measured (`_outline`),
    and cut to the footprint it measures; a zone map's drawing laid onto its car's, quantised."""
    import numpy as np
    from PIL import Image, ImageDraw, PngImagePlugin

    import _outline

    found = assets(base)
    car_frame = zoned_sheet(base, fid, rec)
    zoning = car_frame is not None
    wanted_ids = [i.removesuffix(ZONES) for i in ids] if zoning else ids
    if unknown := [i for i in wanted_ids if i not in found]:
        cli.fail(f"{fid} draws {', '.join(unknown)}, which no variant file under assets/ names")
    pixels, solid = frame_pixels(base, fid)
    boxes = _outline.drawings(solid, wanted_ids, lambda m: cli.fail(f"{fid}: {m}"))
    if zoning:
        car_pixels, car_solid = frame_pixels(base, car_frame)
        car_boxes = _outline.drawings(car_solid, wanted_ids, lambda m: cli.fail(f"{car_frame}: {m}"))
    out = base / CUTS / fid
    out.mkdir(parents=True, exist_ok=True)
    print(f"{fid}: {len(ids)} drawings, asked without a layout" + (f", a zone map of {car_frame}" if zoning else "")
          + f" -> {_sim.rel(base, out)}")
    overlay = Image.fromarray(on_key(pixels.astype(np.int16)).astype(np.uint8), "RGB")
    draw = ImageDraw.Draw(overlay)
    for n, i in enumerate(wanted_ids, 1):
        d = json.loads(found[i].read_text(encoding="utf-8"))
        length = float(d["collisionM"]["sizeM"][0])
        axles = (float(d["frontAxleM"]), float(d["rearAxleM"]))
        box = boxes[i]
        body = _outline.body_in(solid, box)
        m = _outline.measure(body, length, axles)
        if zoning:
            car_box = car_boxes[i]
            car_body = _outline.body_in(car_solid, car_box)
            car_m = _outline.measure(car_body, length, axles)
            car_m["bodyFramePx"] = [v + o for v, o in zip(car_m["bodyPx"], (car_box[0], car_box[1]) * 2)]
            _, origin = _outline.footprint_crop(car_pixels, car_box, car_body, car_m)
            size = (round(car_m["footprintM"][0] * car_m["pxPerM"]), round(car_m["footprintM"][1] * car_m["pxPerM"]))
            moved, (kx, ky) = _outline.aligned(pixels, box, body, m, car_m, origin, size)
            cut_pixels = quantised(moved.copy())
            missing = []
            _outline.lenses(cut_pixels, car_m["pxPerM"], LENS_ZONES, missing.append)
            info = {"frame": fid, "cell": n, "pxPerM": car_m["pxPerM"], "zoneScale": [round(kx, 4), round(ky, 4)]}
            flags = [m.removeprefix("the zone map paints ") for m in missing]
            shown = f"laid onto {car_frame} x{1 / kx:.3f} y{1 / ky:.3f}"
            name = f"{i}{ZONES}"
        else:
            cut_pixels, origin = _outline.footprint_crop(pixels, box, body, m)
            info = {"frame": fid, "cell": n, "pxPerM": m["pxPerM"], "measured": m}
            filed = float(d["collisionM"]["sizeM"][1])
            off = m["widthM"] / filed - 1
            flags = [f"{off:+.0%} wider than its file" if off > 0 else f"{-off:.0%} narrower than its file"] \
                if abs(off) > PROPORTION_FLAG else []
            shown = (f"{m['lengthM']:.2f} x {m['widthM']:.2f} m r{m['cornerRadiusM']:.2f}  footprint "
                     f"{m['footprintM'][0]:.2f} x {m['footprintM'][1]:.2f}  over axles "
                     f"{m['axleWidthsM'][0]:.2f}/{m['axleWidthsM'][1]:.2f}  at {m['pxPerM']:.1f} px/m")
            name = i
        png = PngImagePlugin.PngInfo()
        png.add_text("cut", json.dumps(info))
        Image.fromarray(cut_pixels, "RGBA").save(out / f"{name}.png", pnginfo=png)
        x0, y0, x1, y1 = box
        bl, bt, br, bb = m["bodyPx"]
        draw.rectangle([x0 + bl, y0 + bt, x0 + br - 1, y0 + bb - 1], outline=OVERLAY_SPILL)
        if not zoning:
            draw.rectangle([origin[0], origin[1], origin[0] + cut_pixels.shape[1] - 1, origin[1] + cut_pixels.shape[0] - 1],
                           outline=(255, 255, 255))
        print(f"{n:>2} {name:<26} {shown}  {', '.join(flags) or 'ok'}")
    overlay.save(out / "_overlay.png")
    print(f"{_sim.rel(base, out / '_overlay.png')}: each body's measured box cyan, its footprint white")
    if not zoning:
        cuts = [Image.open(out / f"{i}.png").convert("RGBA") for i in wanted_ids]
        looked_at(cuts, out)
        print(f"{_sim.rel(base, out / '_cuts.png')}, {_sim.rel(base, out / '_ends.png')}: the cuts in the brief's order, "
              "and each one's tail and nose magnified, three to a row")
    return 0


#: `_ends.png`: the share of a cut's length each end is taken from, and how far it is magnified.
ENDS_SHARE, ENDS_ZOOM = 0.2, 3
LOOK_BACKDROP = (40, 40, 40, 255)


def looked_at(cuts, out):
    """The cuts of one frame laid for the eye: `_cuts.png`, three to a row on a dark backdrop, and `_ends.png`, each
    one's tail and nose magnified side by side — where the lamps are judged."""
    from PIL import Image

    def backed(img):
        b = Image.new("RGBA", img.size, LOOK_BACKDROP)
        b.alpha_composite(img)
        return b

    cw, ch = max(c.width for c in cuts) + 2 * GAP_PX, max(c.height for c in cuts) + GAP_PX
    sheet = Image.new("RGBA", (cw * 3, ch * math.ceil(len(cuts) / 3)), LOOK_BACKDROP)
    for n, c in enumerate(cuts):
        sheet.alpha_composite(c, ((n % 3) * cw + (cw - c.width) // 2, (n // 3) * ch + (ch - c.height) // 2))
    sheet.save(out / "_cuts.png")
    rows = []
    for c in cuts:
        e = round(c.width * ENDS_SHARE)
        rows.append([backed(c.crop(box)).resize((e * ENDS_ZOOM, c.height * ENDS_ZOOM), Image.NEAREST)
                     for box in ((0, 0, e, c.height), (c.width - e, 0, c.width, c.height))])
    rw = max(sum(t.width for t in r) for r in rows) + GAP_PX
    rh = max(t.height for r in rows for t in r) + GAP_PX // 2
    per_row = 3
    ends = Image.new("RGBA", (rw * per_row, rh * math.ceil(len(rows) / per_row)), (0, 0, 0, 255))
    for n, (tail, nose) in enumerate(rows):
        x, y = (n % per_row) * rw, (n // per_row) * rh
        ends.paste(tail, (x, y))
        ends.paste(nose, (x + tail.width, y))
    ends.save(out / "_ends.png")


def write_measured(base, described: Path, m):
    """The car's geometry in its variant file, as its picture measures it: footprint, collision box and track."""
    text = described.read_text(encoding="utf-8")
    track = round(max(m["axleWidthsM"]) + 2 * TYRE_PROUD_M, 3)
    for pattern, line in ((r'"footprintM":\s*\[[^\]]*\]', f'"footprintM": [{m["footprintM"][0]:.3f}, {m["footprintM"][1]:.3f}]'),
                          (r'"collisionM":\s*\{[^}]*\}',
                           f'"collisionM": {{ "sizeM": [{m["lengthM"]:.3f}, {m["widthM"]:.3f}], '
                           f'"cornerRadiusM": {m["cornerRadiusM"]:.3f} }}'),
                          (r'"trackM":\s*[0-9.]+', f'"trackM": {track}')):
        text, n = re.subn(pattern, line, text, count=1)
        if not n:
            cli.fail(f"{_sim.rel(base, described)} has no {pattern.split(chr(34))[1]} line to write the measured one over")
    described.write_text(text, encoding="utf-8")


def write_lamps(base, described: Path, rows):
    """The variant file's `lamps` array, replaced whole by the lenses its zone map paints."""
    text = described.read_text(encoding="utf-8")
    at = text.find('"lamps"')
    start = text.find("[", at)
    if at < 0 or start < 0:
        cli.fail(f"{_sim.rel(base, described)} has no lamps array to write")
    depth, end = 0, start
    for end in range(start, len(text)):
        depth += {"[": 1, "]": -1}.get(text[end], 0)
        if depth == 0:
            break
    lines = ",\n".join(f'    {{ "fitting": "{f}", "atM": [{x}, {y}], "sizeM": [{w}, {h}] }}' for f, (x, y), (w, h) in rows)
    described.write_text(text[:start] + ("[\n" + lines + "\n  ]" if rows else "[]") + text[end + 1:], encoding="utf-8")


def drop_lamps(base, described: Path, zones_name, then="`qq props seed` it again"):
    """A new picture's lamps are not the old one's: its zone map, that map's trace and its `lamps` go, to be cut again
    by `qq props` and installed — `then` says how."""
    for gone in (described.parent / zones_name, described.parent / f"{Path(zones_name).stem}{SOURCE}"):
        gone.unlink(missing_ok=True)
    text = re.sub(r'\n[ \t]*"zones":\s*"[^"]*",', "", described.read_text(encoding="utf-8"), count=1)
    described.write_text(text, encoding="utf-8")
    if '"lamps"' in text:
        write_lamps(base, described, [])
    print(f"{_sim.rel(base, described)}: lamps and zone map dropped with the old picture; {then}")


def parts_sheet(out, cells, suffix=""):
    """Each cell's part areas cut from its keyed cut and magnified, framed in their template colour, a row a
    cell. A lamp drawn beside its area rather than in it still reads as `drawn` when the area takes in the
    body's dark edge; at this size the eye sees which."""
    from PIL import Image, ImageDraw

    rows = []
    for c in cells:
        if not c.get("parts"):
            continue
        cutting = Image.open(out / f"{c['id']}{suffix}.png").convert("RGBA")
        x, y = c["box"][:2]
        tiles = []
        for p in c["parts"]:
            px, py, pw, ph = p["box"]
            area = (px - x - PARTS_MARGIN_PX, py - y - PARTS_MARGIN_PX, px - x + pw + PARTS_MARGIN_PX,
                    py - y + ph + PARTS_MARGIN_PX)
            tile = Image.new("RGBA", (area[2] - area[0], area[3] - area[1]), (60, 60, 60, 255))
            tile.alpha_composite(cutting.crop(area))
            tile = tile.resize((tile.width * PARTS_ZOOM, tile.height * PARTS_ZOOM), Image.NEAREST)
            colour = next(v[0] for kind in PARTS.values() for f, v in kind.items() if f == p["fitting"])
            ImageDraw.Draw(tile).rectangle(
                [PARTS_MARGIN_PX * PARTS_ZOOM, PARTS_MARGIN_PX * PARTS_ZOOM,
                 (PARTS_MARGIN_PX + pw) * PARTS_ZOOM - 1, (PARTS_MARGIN_PX + ph) * PARTS_ZOOM - 1],
                outline=tuple(min(255, ch * 2) for ch in colour), width=2)
            tiles.append(tile)
        rows.append(tiles)
    if not rows:
        return False
    gap = 6
    width = max(sum(t.width + gap for t in r) for r in rows)
    height = sum(max(t.height for t in r) + gap for r in rows)
    sheet = Image.new("RGB", (width, height), (20, 20, 20))
    top = 0
    for r in rows:
        left = 0
        for t in r:
            sheet.paste(t, (left, top))
            left += t.width + gap
        top += max(t.height for t in r) + gap
    sheet.save(out / "_parts.png")
    return True


def mask(base, a):
    """The frame's size with the named cells' boxes transparent, which is what a remotegen `--mask` redo
    replaces: every other cell stays byte-identical.

    Alpha only, the colour black: the redo reads nothing else of it, and a mask is kept with the frame's
    lineage, where a copy of the frame would be the parent's image kept under another name (raw_assets/index.md,
    rule 2).

    An id is its outline's box; `id:whole` is its whole grid cell, so a cell drawn past its outline is redrawn and
    not kept byte for byte; `id:tail` (`id:nose`) is only the part areas behind (ahead of) its middle, where the
    drawing landed (the cut's fit), so those lamps are redrawn and the rest of the car is kept.
    """
    if len(a.refs) < 2:
        cli.fail("mask takes a frame id and the asset ids to ask for again")
    import numpy as np
    from PIL import Image, ImageDraw

    fid, specs = a.refs[0], [r.partition(":") for r in a.refs[1:]]
    rec = frame_record(base, fid)
    layout = cells_of(base, rec)
    if "described" in layout:
        return mask_described(base, fid, rec, layout["described"], specs)
    cells = {c["id"]: c for c in layout["cells"]}
    if unknown := [i for i, _, _ in specs if i not in cells]:
        cli.fail(f"{fid} has no cell for {', '.join(unknown)}")
    if bad := [f"{i}:{how}" for i, _, how in specs if how not in ("", "whole", "nose", "tail")]:
        cli.fail(f"{', '.join(bad)}: a cell is named as id, id:whole, id:nose or id:tail")
    frame, rgb, key, _ = opened(base / rec["file"])
    alpha = Image.new("L", frame.size, 255)
    draw = ImageDraw.Draw(alpha)
    for i, _, how in specs:
        c = cells[i]
        if how == "whole":
            draw.rectangle(cell_bounds(layout, c), fill=0)
        elif how in ("nose", "tail"):
            for box in end_parts_drawn(frame, rgb, key, layout, c, fid, how == "nose"):
                draw.rectangle(box, fill=0)
        else:
            x, y, w, h = c["box"]
            draw.rectangle([x - MARGIN_PX, y - MARGIN_PX, x + w + MARGIN_PX - 1, y + h + MARGIN_PX - 1], fill=0)
    size = frame.size
    held = Image.new("RGBA", size, (0, 0, 0, 255))
    held.putalpha(alpha)
    out = mask_path(base, fid, specs)
    out.parent.mkdir(parents=True, exist_ok=True)
    held.save(out)
    print(_sim.rel(base, out))
    return 0


#: The longest a mask's name may spell its specs out before it is named by their hash: a file name has 255 bytes.
MASK_NAME_MAX = 120


def mask_path(base, fid, specs):
    """Where a mask goes: `_mask_<specs>.png` beside the frame's cuts, or `_mask_<n>_<hash>.png` for a long list."""
    spelt = "_".join(i if not how else f"{i}-{how}" for i, _, how in specs)
    if len(spelt) > MASK_NAME_MAX:
        spelt = f"{len(specs)}_{hashlib.sha256(spelt.encode()).hexdigest()[:12]}"
    return base / CUTS / fid / f"_mask_{spelt}.png"


def mask_described(base, fid, rec, ids, specs):
    """A redo mask over a frame asked without a layout: each named drawing's box, MARGIN_PX round it — or, as
    `id:whole`, half the gap to its neighbours as well. It frames no parts, so it has no `:nose` or `:tail`."""
    from PIL import Image, ImageDraw

    import _outline

    if bad := [f"{i}:{how}" for i, _, how in specs if how in ("nose", "tail")]:
        cli.fail(f"{', '.join(bad)}: {fid} was asked without a layout and frames no lamps; mask the drawing")
    if unknown := [i for i, _, _ in specs if i not in ids]:
        cli.fail(f"{fid} draws no {', '.join(unknown)}")
    pixels, solid = frame_pixels(base, fid)
    boxes = _outline.drawings(solid, ids, lambda m: cli.fail(f"{fid}: {m}"))
    alpha = Image.new("L", (solid.shape[1], solid.shape[0]), 255)
    for i, _, how in specs:
        x0, y0, x1, y1 = boxes[i]
        pad = MARGIN_PX + (GAP_PX if how == "whole" else 0)
        ImageDraw.Draw(alpha).rectangle([x0 - pad, y0 - pad, x1 + pad - 1, y1 + pad - 1], fill=0)
    held = Image.new("RGBA", alpha.size, (0, 0, 0, 255))
    held.putalpha(alpha)
    out = mask_path(base, fid, specs)
    out.parent.mkdir(parents=True, exist_ok=True)
    held.save(out)
    print(_sim.rel(base, out))
    return 0


def end_parts_drawn(frame, rgb, key, layout, c, fid, nose):
    """One frame box a side over the cell's part areas at one end — ahead of its middle for the nose (the right edge),
    behind it for the tail — where the cut's fit puts them on the drawing: padded by ZONE_REACH_M, since the generator
    draws a lens a few centimetres off, and by END_WRAP_M towards the other end, since it wraps a corner lamp round
    onto the flank."""
    _, fit = register(frame, rgb, key, c, cell_bounds(layout, c), False)
    if "shiftPx" not in fit or fit.get("refused"):
        cli.fail(f"{fid} {c['id']}: not fitted ({fit.get('refused')}), so its ends cannot be placed; mask it whole")
    sx, sy, sw, _ = c["shapeBox"]
    bx, by = sx - fit["shiftPx"][0], sy - fit["shiftPx"][1]
    kx, ky = 1 / fit["scale"][0], 1 / fit["scale"][1]
    pad, wrap = round(ZONE_REACH_M * layout["pxPerM"]), round(END_WRAP_M * layout["pxPerM"])
    sides = {}
    for p in c.get("parts", []):
        px, py, pw, ph = p["box"]
        if (px + pw / 2 >= sx + sw / 2) != nose:
            continue
        x0, y0 = bx + (px - sx) * kx, by + (py - sy) * ky
        box = [round(x0) - pad - (wrap if nose else 0), round(y0) - pad,
               round(x0 + pw * kx) + pad + (0 if nose else wrap), round(y0 + ph * ky) + pad]
        side = py + ph / 2 > sy + c["shapeBox"][3] / 2
        held = sides.setdefault(side, box)
        sides[side] = [min(held[0], box[0]), min(held[1], box[1]), max(held[2], box[2]), max(held[3], box[3])]
    if not sides:
        cli.fail(f"{fid} {c['id']}: frames no part at its {'nose' if nose else 'tail'}")
    return list(sides.values())


def drawn(paint, solid, bodywork, box, x0, y0):
    """Whether the frame painted something other than bodywork in a part's area. Whether it drew it off is
    judged by eye."""
    if bodywork is None:
        return False
    px, py, pw, ph = box
    area = (slice(py - y0, py - y0 + ph), slice(px - x0, px - x0 + pw))
    distinct = solid[area] & (abs(paint[area] - bodywork).max(axis=-1) >= PART_CONTRAST)
    return distinct.mean() >= PART_SHARE


def quantised(pixels):
    """A zone map's cut with every texel the nearest zone colour and either solid or clear: a zone is a set of
    texels, and an edge blended between two of them is in neither."""
    import numpy as np

    palette = np.array(list(ZONE_COLOURS.values()), np.int32)
    rgb = pixels[..., :3].astype(np.int32)
    nearest = ((rgb[..., None, :] - palette) ** 2).sum(-1).argmin(-1)
    out = np.zeros_like(pixels)
    out[..., :3] = palette[nearest]
    out[..., 3] = np.where(pixels[..., 3] >= 128, 255, 0)
    return out


def zoned(pixels, part, origin, reach):
    """Whether a zone map paints its part's zone anywhere within `reach` of the area the part is framed in."""
    colour = ZONE_PARTS.get(part["fitting"])
    if colour is None:
        return False
    px, py, pw, ph = part["box"]
    x0, y0 = max(0, px - reach - origin[0]), max(0, py - reach - origin[1])
    area = pixels[y0:py + ph + reach - origin[1], x0:px + pw + reach - origin[0]]
    return bool(((area[..., :3] == colour).all(-1) & (area[..., 3] > 0)).any())


def register(frame, rgb, key, c, cell, track=False):
    """The frame resampled so the body the model drew in a cell lands on the cell's authored shape, and the
    shift and scale it took; or no frame, and why, when the drawing is too far off to be the asset asked for.

    The model draws to the outline only within a few pixels — a row of a sheet now and then a few dozen —
    and a cut clipped at the outline then loses the body's own edge where it overshot and leaves a hole where
    it fell short. The shape is the authority (CAR-12b): the picture is moved onto it, never the shape measured
    off the picture. A car's width is the median of its flanks over the middle of its length, which a mirror
    does not move, and so is a thin asset's (THIN_ASPECT), which an end cap or a clamp does not. The body is looked for over its whole grid cell, and one that reaches the cell's edge may be
    cut off by it or run into the next cell's, so it is not fitted.
    """
    import numpy as np
    from PIL import Image

    # Looked for a gap past its cell: only the body joined to the outline's middle is kept, so a neighbour's paint
    # this reaches is dropped, and a drawing the model shifted a little out of its cell is still found whole.
    mx0, my0 = max(0, cell[0] - GAP_PX), max(0, cell[1] - GAP_PX)
    mx1, my1 = min(rgb.shape[1], cell[2] + GAP_PX), min(rgb.shape[0], cell[3] + GAP_PX)
    solid = body_at_centre(chroma_key(rgb[my0:my1, mx0:mx1], key)[..., 3] > 128, c, (mx0, my0))
    if solid[0].any() or solid[-1].any() or solid[:, 0].any() or solid[:, -1].any():
        return None, {"refused": "drawn up to its cell's edge"}
    columns = np.flatnonzero(solid.sum(0) >= FIT_MIN_PX)
    if columns.size == 0:
        return None, {"refused": "nothing drawn"}
    left, right = columns[0], columns[-1] + 1
    if c["standOff"] or c["shapeBox"][2] >= THIN_ASPECT * c["shapeBox"][3]:
        middle = solid[:, left + (right - left) // 5: right - (right - left) // 5]
        filled = middle.any(0)
        tops = np.argmax(middle, 0)[filled]
        bottoms = middle.shape[0] - np.argmax(middle[::-1], 0)[filled]
        top, bottom = int(np.median(tops)), int(np.median(bottoms))
    else:
        rows = np.flatnonzero(solid.sum(1) >= FIT_MIN_PX)
        top, bottom = rows[0], rows[-1] + 1
    bx, by, bw, bh = mx0 + left, my0 + top, right - left, bottom - top
    sx, sy, sw, sh = c["shapeBox"]
    kx, ky = bw / sw, bh / sh
    if track:
        # A track's authority across it is its rails' gauge, not its extent: the model draws the strip's rails at the
        # gauge and centred in it, and the ballast as narrow as it likes (railway_a-v1..v3). So across, it keeps the
        # scale it was drawn at and is centred; what it leaves of the outline is clear, ground showing through.
        by, bh, ky = by + (bh - sh) / 2, sh, 1.0
    fit = {"shiftPx": [int(round(sx - bx)), int(round(sy - by))], "scale": [round(1 / kx, 4), round(1 / ky, 4)]}
    most = (FIT_ROWS_MAX if family(c["asset"])[2] == "rows"
            else FIT_GEAR_MAX if c["asset"].startswith(GEAR_PREFIX) and not track else FIT_SCALE_MAX)
    if max(abs(kx - 1), abs(ky - 1)) > most:
        return None, dict(fit, refused=f"drawn {bw}x{bh} px for a {sw}x{sh} px shape")
    moved = frame.transform(frame.size, Image.AFFINE, (kx, 0, bx - sx * kx, 0, ky, by - sy * ky),
                            resample=Image.BICUBIC, fillcolor=key or (0, 0, 0, 0))
    return moved, fit


def body_at_centre(solid, c, origin):
    """The one drawing joined to the middle of the cell's outline, and nothing else in the cell: a model that shifts a
    row's drawings sideways lands a neighbour's coupler in this cell, which is the neighbour's and not this body's
    edge. The whole cell where nothing is drawn at the middle, for `register` to refuse."""
    import numpy as np
    from PIL import Image, ImageDraw

    x, y, w, h = c["box"]
    seed = (x + w // 2 - origin[0], y + h // 2 - origin[1])
    if not (0 <= seed[1] < solid.shape[0] and 0 <= seed[0] < solid.shape[1]) or not solid[seed[1], seed[0]]:
        return solid
    marked = Image.fromarray(solid.astype(np.uint8) * 255).copy()
    ImageDraw.floodfill(marked, seed, 128)
    return np.asarray(marked) == 128


def cells_of(base, rec):
    """The cells of the template the frame was asked from: the first input picture carrying them, or a parent
    frame's — a zone map is asked from its sheet alone. A frame asked without a layout names its ids in its record
    (remotegen `--cells`): {"described": ids}, which `_outline` finds on the frame."""
    from PIL import Image

    if rec.get("cells"):
        return {"described": rec["cells"]}
    for inp in rec.get("inputs", []):
        if inp.get("frame") and frame_record(base, inp["frame"]).get("cells"):
            return cells_of(base, frame_record(base, inp["frame"]))
    for inp in rec.get("inputs", []):
        path = picture(base, inp)
        if path and inp["ext"] == "png":
            text = Image.open(path).text.get("cells")
            if text:
                return json.loads(text)
    for inp in rec.get("inputs", []):
        if inp.get("frame"):
            return cells_of(base, frame_record(base, inp["frame"]))
    cli.fail(f"{rec['id']} was not asked from a `qq sprites template` layout: no input carries cells")


def zoned_sheet(base, fid, rec):
    """The sheet a zone map maps (its image 1, or for a redo of a zone map that map's own sheet), or None for a frame
    that is not a zone map."""
    named = VERSIONED.fullmatch(fid)
    if not named or not named.group(1).endswith(ZONES):
        return None
    first = next((inp for inp in rec.get("inputs", []) if inp["role"] == "image 1"), None)
    if not first or not first.get("frame"):
        cli.fail(f"{fid} is named as a zone map, and its image 1 is no frame: zone a sheet with `edit --image <frame>`")
    return zoned_sheet(base, first["frame"], frame_record(base, first["frame"])) or first["frame"]


def opened(path):
    """The frame as a PIL image and an int16 array, with its key: a frame asked with a transparent background
    (its border at least CLEAR_BORDER_MIN clear) is RGBA and keyed by its own alpha, key None; any other is RGB
    and keyed by its border's colour. And the share of the border the key holds."""
    import numpy as np
    from PIL import Image

    image = Image.open(path)
    if "A" in image.getbands():
        alpha = np.asarray(image.getchannel("A"))
        border = np.concatenate([alpha[0], alpha[-1], alpha[:, 0], alpha[:, -1]])
        if (clear := (border < 16).mean()) >= CLEAR_BORDER_MIN:
            rgba = image.convert("RGBA")
            return rgba, np.asarray(rgba).astype(np.int16), None, clear
    rgb_image = image.convert("RGB")
    rgb = np.asarray(rgb_image).astype(np.int16)
    key, flat = border_colour(rgb)
    return rgb_image, rgb, key, flat


def on_key(pixels):
    """An RGBA crop laid over the key colour, for a picture to look at; an RGB one as it is."""
    import numpy as np

    if pixels.shape[-1] == 3:
        return pixels
    share = pixels[..., 3:4].astype(np.float32) / 255
    return (pixels[..., :3] * share + np.array(SHOWN_KEY, np.float32) * (1 - share)).astype(np.int16)


def border_colour(rgb):
    """The modal colour of the frame's one-pixel border, in 16-level buckets, as its mean; and its share."""
    import numpy as np

    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
    b = border >> 4
    codes = (b[:, 0] << 8) | (b[:, 1] << 4) | b[:, 2]
    values, counts = np.unique(codes, return_counts=True)
    mode = values[counts.argmax()]
    return tuple(int(v) for v in border[codes == mode].mean(0)), counts.max() / len(codes)


def chroma_key(rgb, key):
    """A crop as RGBA with its background flooded out from the crop's edges.

    Flooding rather than keying every near pixel keeps paint inside an outline that happens to be near the key.
    A cast shadow (the key darkened, same hue) floods as background: the model draws one however firmly the
    brief forbids it. An anti-aliased edge pixel the flood reaches is faded and has the key's tint pulled out;
    a cleared pixel's colour is zeroed too, so bilinear filtering cannot bleed the key back in.
    """
    import numpy as np
    from PIL import Image, ImageDraw

    if key is None:
        # A transparent frame is keyed already: its alpha is the model's own edge, anti-aliasing and all.
        out = rgb.clip(0, 255).astype(np.uint8).copy()
        out[out[..., 3] == 0] = 0
        return out
    k = np.array(key, np.int16)
    dist = np.abs(rgb - k).max(-1)
    level = rgb.sum(-1) / max(1, int(k.sum()))
    # The tolerance darkens with the shadow: a fixed one passes every dark grey roof as the key in shadow.
    shadow = ((level >= 0.35) & (level <= 1.0)
              & (np.abs(rgb - k * level[..., None]).max(-1) <= KEY_HARD * level))
    # The pad joins every border pixel into one seed. The copy matters: an image sharing the array's buffer drops
    # the flood's writes.
    passable = Image.fromarray(np.pad((dist <= KEY_HARD) | shadow, 1, constant_values=True).astype(np.uint8) * 255).copy()
    ImageDraw.floodfill(passable, (0, 0), 128)
    # And a pocket of the key itself that paint encloses — magenta between a wagon's frame posts — is background as
    # well: KEY_HARD is the key and nothing a brief asks for.
    background = ((np.asarray(passable) == 128)[1:-1, 1:-1]) | (dist <= KEY_HARD) | shadow
    reached = background.copy()
    reached[1:] |= background[:-1]
    reached[:-1] |= background[1:]
    reached[:, 1:] |= background[:, :-1]
    reached[:, :-1] |= background[:, 1:]
    edge = reached & ~background & (dist < KEY_SOFT)

    out = np.empty(rgb.shape[:2] + (4,), np.int16)
    out[..., :3], out[..., 3] = rgb, 255
    out[edge, 3] = (dist[edge] - KEY_HARD) * 255 // (KEY_SOFT - KEY_HARD)
    out[edge, 0] -= (k[0] - k[1]) // 3
    out[edge, 2] -= (k[2] - k[1]) // 3
    out[background] = 0
    return out.clip(0, 255).astype(np.uint8)


# -------------------------------------------------------------------------------------------------- keep

def keep(base, a):
    if len(a.refs) < 2:
        cli.fail("keep takes a frame id and the asset ids to keep from it")
    from PIL import Image

    fid, ids = a.refs[0], a.refs[1:]
    frame_record(base, fid)
    found = assets(base)
    for i in ids:
        cutting = base / CUTS / fid / f"{i}.png"
        if not cutting.is_file():
            cli.fail(f"no cut of {i} from {fid} in {_sim.rel(base, cutting.parent)}: run `qq sprites cut {fid}`")
        if i not in found:
            cli.fail(f"no variant file under assets/ has the id {i}")
        folder = raw_dir(base, found[i], i)
        folder.mkdir(parents=True, exist_ok=True)
        records = read_jsonl(folder / VARIANTS)
        kept = next((r for r in records if r["frame"] == fid), None)
        if kept and not a.recut:
            cli.fail(f"{i} is already kept from {fid} ({_sim.rel(base, folder / VARIANTS)}): a cell is kept once — "
                     "`--recut` keeps the frame's new cut beside it, after `cut` itself changed")
        if a.recut and not kept:
            cli.fail(f"{i} is not kept from {fid}: --recut keeps a cell again, and it was never kept")
        info = json.loads(Image.open(cutting).text["cut"])
        version = 1 + max((int(m.group(2)) for p in folder.glob(f"{i}-v*.png")
                           if (m := VERSIONED.fullmatch(p.stem))), default=0)
        name = f"{i}-v{version}"
        # A drawing asked without a layout has no template box: its cell is the order it was found in. A cell cut
        # again is a new version naming the one it re-cuts, which stays as it was kept (raw_assets/index.md, rule 3).
        recut = [r["variant"] for r in records if r["frame"] == fid and r["cell"] == info["cell"]]
        append(folder / VARIANTS, {"variant": name, "frame": fid, "cell": info["cell"],
                                    **({"box": info["box"]} if "box" in info else {}),
                                    **({"recutOf": recut[-1]} if recut else {}),
                                    "pxPerM": info["pxPerM"], "score": a.score, "status": "candidate",
                                    "ts": time.strftime("%Y-%m-%dT%H:%M:%S")})
        shutil.copyfile(cutting, folder / f"{name}.png")
        print(f"kept {name} <- {fid} cell {info['cell']}" + (f", a re-cut of {recut[-1]}" if recut else "")
              + f": {_sim.rel(base, folder / (name + '.png'))}")
    for kept in keep_lineage(base, fid):
        print(f"  lineage: {kept}")
    return 0


def drop(base, a):
    """Unkeep candidates no sprite is cut from — a trial given up — with their records; then every frame record and
    input picture under raw_assets/ that no kept variant traces to any more. A variant a sprite uses is never dropped
    (raw_assets/index.md, rule 3)."""
    if not a.refs or not all(VERSIONED.fullmatch(r) for r in a.refs):
        cli.fail("drop takes variants, as <id>-v<N>")
    found = assets(base)
    for name in a.refs:
        i = VERSIONED.fullmatch(name).group(1)
        if i not in found:
            cli.fail(f"no variant file under assets/ has the id {i}")
        folder = raw_dir(base, found[i], i)
        records = read_jsonl(folder / VARIANTS)
        record = next((r for r in records if r["variant"] == name), None)
        if record is None:
            cli.fail(f"{name} is not kept ({_sim.rel(base, folder / VARIANTS)})")
        if record["status"] == "used":
            cli.fail(f"{name} is the sprite's variant: `use` another before dropping it")
        for r in records:
            # A re-cut of what is dropped re-cuts nothing kept.
            if r.get("recutOf") == name:
                r.pop("recutOf")
        (folder / VARIANTS).write_text("".join(json.dumps(r) + "\n" for r in records if r is not record), encoding="utf-8")
        (folder / f"{name}.png").unlink(missing_ok=True)
        # The lamps cut on the picture (`qq props install`) go with it.
        (folder / f"{name}.props.json").unlink(missing_ok=True)
        print(f"dropped {_sim.rel(base, folder / (name + '.png'))}")
    for gone in unkept_lineage(base):
        gone.unlink()
        print(f"  lineage no variant traces to: {_sim.rel(base, gone)}")
    return 0


def unkept_lineage(base):
    """The frame records and input pictures under raw_assets/ no kept variant traces to, through its frame's ancestors."""
    todo = [r["frame"] for listing in (base / RAW).rglob(VARIANTS) for r in read_jsonl(listing)]
    frames, inputs = set(), set()
    while todo:
        f = todo.pop()
        kept = base / KEPT_FRAMES / f"{f}.json"
        if f in frames or not kept.is_file():
            continue
        frames.add(f)
        for inp in json.loads(kept.read_text(encoding="utf-8")).get("inputs", []):
            if inp.get("frame"):
                todo.append(inp["frame"])
            else:
                inputs.add(f"{inp['sha']}.{inp['ext']}")
    return sorted([p for p in (base / KEPT_FRAMES).glob("*.json") if p.stem not in frames]
                  + [p for p in (base / KEPT_INPUTS).iterdir() if p.name not in inputs])


def keep_lineage(base, fid):
    """The frame's record and every ancestor's into git, with the pictures they were asked from — a parent
    that was itself a frame (a sheet) keeps its record and never its image."""
    written, todo, seen = [], [fid], set()
    while todo:
        f = todo.pop()
        if f in seen:
            continue
        seen.add(f)
        rec = frame_record(base, f)
        target = base / KEPT_FRAMES / f"{f}.json"
        if not target.exists():
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(json.dumps(rec, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
            written.append(_sim.rel(base, target))
        for inp in rec.get("inputs", []):
            if inp.get("frame"):
                todo.append(inp["frame"])
                continue
            kept = base / KEPT_INPUTS / f"{inp['sha']}.{inp['ext']}"
            if kept.exists():
                continue
            scratch = base / INPUTS / kept.name
            if not scratch.is_file():
                cli.fail(f"{f}'s {inp['role']} ({inp['sha']}) is gone from {INPUTS}: its lineage cannot be kept")
            kept.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(scratch, kept)
            written.append(_sim.rel(base, kept))
    return written


# --------------------------------------------------------------------------------------------------- use

def use(base, a):
    if len(a.refs) != 1 or not VERSIONED.fullmatch(a.refs[0]):
        cli.fail("use takes one variant, as <id>-v<N>")
    name = a.refs[0]
    i = VERSIONED.fullmatch(name).group(1)
    found = assets(base)
    if i not in found:
        cli.fail(f"no variant file under assets/ has the id {i}")
    described = found[i]
    folder = raw_dir(base, described, i)
    variant = folder / f"{name}.png"
    if not variant.is_file():
        cli.fail(f"no kept variant {_sim.rel(base, variant)}: `qq sprites keep` it first")

    from PIL import Image

    # A drawing asked without a layout is the car's geometry: its file takes what the cut measured before it is sized.
    measured = json.loads(Image.open(variant).text.get("cut", "{}")).get("measured")
    # A wreck is drawn to its car's geometry and never writes it: it is shrunk to the car's footprint.
    if measured and not i.endswith(WRECK):
        write_measured(base, described, measured)
    b = body(base, found, i)
    vw = Image.open(variant).width
    d = json.loads(described.read_text(encoding="utf-8"))
    density = min(vw / b["w"], grid(art.read_figures(base), b["asset"]))
    if "collisionM" in d:
        # A car's lamps are cut and drawn at the art grid (LampAtlas.ArtPxPerM), so its picture is on it even where the
        # drawing came back coarser.
        density = grid(art.read_figures(base), b["asset"])
    wanted = (max(1, round(b["w"] * density)), max(1, round(b["d"] * density)))
    zones = is_zones(d, i)
    if zones:
        # The bake reads a zone texel's channels as glass colours (LampAtlasBake.Holds), which a generated palette map
        # misreads: yellow is red and amber glass, green amber.
        cli.fail(f"{name}: a car's lamps are cut with `qq props` and installed by `qq props install {d['id']}`, "
                 "not from a generated zone map")
        zone_frame =next(r["frame"] for r in read_jsonl(folder / VARIANTS) if r["variant"] == name)
        if (maps := zoned_sheet(base, zone_frame, frame_record(base, zone_frame))) != (car := sheet_of(base, described, d["id"])):
            cli.fail(f"{name} maps {maps}, and {d['id']}'s sprite is cut from {car}: a zone map is of the sheet its "
                     f"car's sprite came from")
        # A palette, so a PNG: lossy colour would make every texel a zone it is not.
        shrunk_zones = zones_shrunk(variant, wanted)
        data, how, fmt = art.encode(shrunk_zones, "png", 0.0)[0], "lossless", "png"
        if "described" in cells_of(base, frame_record(base, zone_frame)):
            import numpy as np

            import _outline

            # Its lamps are where its zone map paints them (the owner, 2026-10-20): nothing authored them first.
            rows = _outline.lenses(np.asarray(shrunk_zones), wanted[0] / b["w"], LENS_ZONES,
                                   lambda m: cli.fail(f"{name}: {m}"))
            write_lamps(base, described, rows)
    else:
        shrunk = without_dust(art.shrink(variant, wanted))
        data, how = art.encode(seamless(shrunk, density) if b["track"] else shrunk, "webp", WEBP_ERROR)
        fmt = "webp"

    now = f"{i}.{fmt}"
    was = sprite_of(described, i)
    if is_wreck(d, i):
        name_wreck(base, described, now)
    elif zones:
        name_zones(base, described, now)
    elif was is None:
        text, n = re.subn(rf'(\n([ \t]*)"id":\s*"{re.escape(i)}",)', rf'\1\n\2"sprite": "{now}",',
                          described.read_text(encoding="utf-8"), count=1)
        if not n:
            cli.fail(f"cannot place a sprite line after the id in {_sim.rel(base, described)}")
        described.write_text(text, encoding="utf-8")
    if was is not None and was != now:
        art.name(base, described.parent / was, now)
        (described.parent / was).unlink(missing_ok=True)
    (described.parent / now).write_bytes(data)
    if not is_wreck(d, i) and not zones and d.get("zones"):
        drop_lamps(base, described, d["zones"], *([a.lamps_then] if getattr(a, "lamps_then", None) else []))
    if not is_wreck(d, i) and (kept := variant.with_name(f"{name}.props.json")).is_file():
        print(f"{_sim.rel(base, kept)}: lamps were cut on this picture — `qq props restore {i}` and "
              f"`qq props install {i}` put them back")
    source = described.parent / f"{i}{SOURCE}"
    source.write_text(json.dumps({"variant": _sim.rel(base, variant), "pxPerM": round(density, 2),
                                  "encoding": f"{fmt} {how}"}, indent=1) + "\n", encoding="utf-8")

    records = read_jsonl(folder / VARIANTS)
    for r in records:
        r["status"] = "used" if r["variant"] == name else ("candidate" if r["status"] == "used" else r["status"])
    (folder / VARIANTS).write_text("".join(json.dumps(r) + "\n" for r in records), encoding="utf-8")

    gone = {_sim.rel(base, described.parent / s) for s in (was, now) if s}
    legacy_path = base / LEGACY
    if legacy_path.is_file():
        lines = legacy_path.read_text(encoding="utf-8").splitlines()
        legacy_path.write_text("".join(l + "\n" for l in lines if l not in gone), encoding="utf-8")
    print(f"{_sim.rel(base, described.parent / now)} <- {name}: {wanted[0]}x{wanted[1]} px at "
          f"{density:.1f} px/m ({fmt} {how})")
    return 0


#: The least a track's sleepers are apart: what bounds the pitch `seamless` looks for, below the ballast's own grain.
SLEEPER_PITCH_LEAST_M = 0.4
#: At or above this a track texel is solid; below it, clear (`cut_out`): a keyed edge under it still carries the key.
CUT_OUT_ALPHA = 240
#: How near the profile's best match a lag must score to be read as the sleepers' pitch rather than a half of it.
SLEEPER_PEAK_SHARE = 0.8


def seamless(image, density):
    """A track strip made to repeat along x at its own width: the ground lays it end to end along every track.

    The model draws a strip, not a tile, so its two ends do not meet. Its sleepers' pitch is read off the drawing's
    own column profile (the autocorrelation's first peak), the strip is cut to the most whole pitches that leave one
    pitch over, and that last pitch is cross-faded into the first — the two in the same phase, so a sleeper fades
    into a sleeper — then stretched back to the width the art grid gives it: a few per cent, along the track only.
    """
    import numpy as np
    from PIL import Image

    rgba = np.asarray(image.convert("RGBA")).astype(np.float32)
    height, width = rgba.shape[:2]
    # Between the rails, where a sleeper and the ballast beside it differ most and the rails themselves do not run.
    band = rgba[round(height * 0.42):round(height * 0.58)]
    profile = (band[..., :3].mean(-1) * band[..., 3] / 255).mean(0)
    profile -= profile.mean()
    lags = range(max(4, round(SLEEPER_PITCH_LEAST_M * density)), width // 3)
    score = [float((profile[:-lag] * profile[lag:]).mean()) for lag in lags]
    # The pitch scores best, and so does every whole number of it: the first peak near the best is the pitch itself.
    best = max(score)
    peak = next(k for k in range(1, len(score) - 1) if score[k] >= score[k - 1] and score[k] >= score[k + 1]
                and score[k] >= SLEEPER_PEAK_SHARE * best) if best > 0 else int(np.argmax(score))
    pitch = lags[peak]
    period = (width - pitch) // pitch * pitch
    out = rgba[:, :period].copy()
    fade = (np.arange(pitch, dtype=np.float32) + 0.5) / pitch
    out[:, :pitch] = rgba[:, period:period + pitch] * (1 - fade)[None, :, None] + rgba[:, :pitch] * fade[None, :, None]
    tile = Image.fromarray(out.clip(0, 255).astype(np.uint8), "RGBA")
    print(f"seamless: sleepers every {pitch} px, {period // pitch} of them in {period} px, stretched to {width} px")
    return cut_out(np.asarray(tile.resize((width, height), Image.LANCZOS)).copy())


def cut_out(rgba):
    """The strip as the ground draws it, which is a cut-out and not a blend (`ground.frag` discards what is clear): every
    texel solid or clear, the half-keyed edge — the key's tint still in it — clear, and each clear texel given the colour
    of the nearest solid one in its column, so a filtered sample at the strip's edge mixes in ballast and not black."""
    import numpy as np
    from PIL import Image

    solid = rgba[..., 3] >= CUT_OUT_ALPHA
    rgba[..., 3] = np.where(solid, 255, 0)
    for column in range(rgba.shape[1]):
        rows = np.flatnonzero(solid[:, column])
        if rows.size == 0:
            continue
        nearest = rows[np.abs(np.arange(rgba.shape[0])[:, None] - rows[None, :]).argmin(1)]
        rgba[~solid[:, column], column, :3] = rgba[nearest[~solid[:, column]], column, :3]
    return Image.fromarray(rgba, "RGBA")


def name_wreck(base, described: Path, now: str):
    """The variant file told its wreck is `now`. Any `scale` it carried goes: that was legacy art's own debris
    reaching past the footprint, and art cut to the footprint is drawn at the car's size."""
    text = described.read_text(encoding="utf-8")
    line = f'"wreck": {{ "sprite": "{now}" }}'
    text, n = re.subn(r'"wreck":\s*\{[^{}]*\}', line, text, count=1)
    if not n:
        text, n = re.subn(r'(\n([ \t]*)"sprite":\s*"[^"]*",)', rf'\1\n\2{line},', text, count=1)
    if not n:
        cli.fail(f"cannot place a wreck line after the sprite in {_sim.rel(base, described)}: `use` the car first")
    described.write_text(text, encoding="utf-8")


def zones_shrunk(path: Path, wanted: tuple[int, int]):
    """A zone map brought to its sprite's size the way the sprite was, by area: each texel takes the zone (or the
    clear) that covers most of it. The sprite is the same cell shrunk to the same size, so the two land texel on
    texel; picking the nearest source texel instead would put a zone's edge wherever the sampling grid fell."""
    import numpy as np
    from PIL import Image

    pixels = np.asarray(Image.open(path).convert("RGBA"))
    colours = list(ZONE_COLOURS.values())
    solid = pixels[..., 3] > 0
    planes = [~solid] + [solid & (pixels[..., :3] == c).all(-1) for c in colours]
    votes = np.stack([np.asarray(Image.fromarray(p.astype(np.float32), "F").resize(wanted, Image.BOX)) for p in planes])
    winner = votes.argmax(0)
    out = np.zeros((wanted[1], wanted[0], 4), np.uint8)
    for n, c in enumerate(colours, 1):
        out[winner == n] = (*c, 255)
    return Image.fromarray(out, "RGBA")


def name_zones(base, described: Path, now: str):
    """The variant file told its zone map is `now`, on the line after its sprite."""
    text = described.read_text(encoding="utf-8")
    line = f'"zones": "{now}"'
    text, n = re.subn(r'"zones":\s*"[^"]*"', line, text, count=1)
    if not n:
        text, n = re.subn(r'(\n([ \t]*)"sprite":\s*"[^"]*",)', rf'\1\n\2{line},', text, count=1)
    if not n:
        cli.fail(f"cannot place a zones line after the sprite in {_sim.rel(base, described)}: `use` the car first")
    described.write_text(text, encoding="utf-8")


def sheet_of(base, described: Path, i: str):
    """The frame the id's installed picture was cut from, or None for one that traces to nothing."""
    source = described.parent / f"{i}{SOURCE}"
    if not source.is_file():
        return None
    variant = Path(json.loads(source.read_text(encoding="utf-8"))["variant"])
    return next((r["frame"] for r in read_jsonl(base / variant.parent / VARIANTS) if r["variant"] == variant.stem),
                None)


def without_dust(image):
    """The sprite with every island of fewer than DUST_PX opaque pixels cleared — a fleck the key or the shrink
    left off the body, which CAR-12 refuses on a car's sheet (`CarSpriteTests.NoCarSheetCarriesPixelDust`, whose
    island this is: 8-connected, alpha over 128)."""
    import numpy as np

    rgba = np.asarray(image).copy()
    solid = rgba[..., 3] > 128
    seen = np.zeros_like(solid)
    height, width = solid.shape
    for y0, x0 in zip(*np.nonzero(solid)):
        if seen[y0, x0]:
            continue
        island, todo = [], [(y0, x0)]
        seen[y0, x0] = True
        while todo:
            y, x = todo.pop()
            island.append((y, x))
            for ny in range(max(0, y - 1), min(height, y + 2)):
                for nx in range(max(0, x - 1), min(width, x + 2)):
                    if solid[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        todo.append((ny, nx))
        if len(island) < DUST_PX:
            for y, x in island:
                rgba[y, x] = 0
    from PIL import Image
    return Image.fromarray(rgba, "RGBA")


# ------------------------------------------------------------------------------------------ trace, compare

def trace(base, a):
    if len(a.refs) != 1:
        cli.fail("trace takes one asset id")
    i = a.refs[0]
    found = assets(base)
    if i not in found:
        cli.fail(f"no variant file under assets/ has the id {i}")
    source = found[i].parent / f"{i}{SOURCE}"
    if not source.is_file():
        print(f"{i}: no {SOURCE} — " + ("on the legacy list" if listed_legacy(base, found[i], i) else "traces to nothing"))
        return 1
    src = json.loads(source.read_text(encoding="utf-8"))
    name = Path(src["variant"]).stem
    record = next(r for r in read_jsonl(base / Path(src["variant"]).parent / VARIANTS) if r["variant"] == name)
    print(f"{i}: {_sim.rel(base, source)} -> {src['variant']} ({src['encoding']}, {src['pxPerM']} px/m)")
    print(f"  {name}: {record['status']}, score {record['score']}, cell {record['cell']} of {record['frame']}")
    todo, seen = [record["frame"]], set()
    while todo:
        f = todo.pop()
        if f in seen:
            continue
        seen.add(f)
        r = frame_record(base, f)
        print(f"  frame {f}: {r['op']} {r['model']} {json.dumps(r.get('settings', {}))} ${r.get('costUsd', '?')} "
              f"{r['ts']}")
        for inp in r.get("inputs", []):
            print(f"    {inp['role']}: " + (f"frame {inp['frame']}" if inp.get("frame")
                                             else f"{inp['sha']}.{inp['ext']} (was {inp['was']})"))
            if inp.get("frame"):
                todo.append(inp["frame"])
        prompt = r["prompt"] if a.full else r["prompt"][:240].replace("\n", " ") + ("..." if len(r["prompt"]) > 240 else "")
        print("    prompt: " + prompt)
    return 0


def compare(base, a):
    if len(a.refs) != 1:
        cli.fail("compare takes one asset id")
    from PIL import Image, ImageDraw

    i = a.refs[0]
    found = assets(base)
    if i not in found:
        cli.fail(f"no variant file under assets/ has the id {i}")
    folder = raw_dir(base, found[i], i)
    records = read_jsonl(folder / VARIANTS)
    tiles = [(folder / f"{r['variant']}.png", f"{r['variant']} {r['status']} {r['score'] if r['score'] is not None else '-'}")
             for r in records]
    sprite = sprite_of(found[i], i)
    if sprite:
        tiles.append((found[i].parent / sprite, "in game"))
    if not tiles:
        cli.fail(f"{i} has no kept variant and no sprite")

    height, caption = 256, 18
    scaled = []
    for path, label in tiles:
        img = Image.open(path).convert("RGBA")
        scaled.append((img.resize((max(1, round(img.width * height / img.height)), height), Image.LANCZOS), label))
    sheet = Image.new("RGB", (sum(s.width for s, _ in scaled) + 8 * (len(scaled) + 1), height + caption + 16), (40, 40, 40))
    draw, x = ImageDraw.Draw(sheet), 8
    for img, label in scaled:
        sheet.paste(img, (x, 8), img)
        draw.text((x, height + 12), label, fill=(230, 230, 230))
        x += img.width + 8
    out = base / COMPARE / f"{i}.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    print(_sim.rel(base, out))
    for r in records:
        print(f"  {r['variant']:<18} {r['status']:<9} score {r['score']}  from {r['frame']} cell {r['cell']}")
    return 0


# --------------------------------------------------------------------------------------------- the gate

def check(base, a):
    problems = audit(base)
    for p in problems:
        print(p)
    print(f"{len(problems)} problem(s)" if problems else "every image under assets/ traces or is legacy")
    return 1 if problems else 0


def audit(base) -> list[str]:
    """What breaks raw_assets/index.md's rules: an image tracing to nothing, a lineage with a hole in it."""
    problems = []
    legacy_set = legacy_entries(base)
    images = sorted(p for p in (base / "assets").rglob("*") if p.suffix in IMAGES)
    for img in images:
        rel = _sim.rel(base, img)
        source = img.parent / f"{img.stem}{SOURCE}"
        if source.is_file():
            traced = json.loads(source.read_text(encoding="utf-8"))
            variant = traced["variant"]
            if not (base / variant).is_file():
                problems.append(f"{rel}: its variant {variant} is gone")
            # A zone map `qq props install` painted names the picture its lamps were cut on.
            sprite_src = img.parent / f"{img.stem.removesuffix(ZONES)}{SOURCE}"
            if "cutOn" in traced and sprite_src.is_file() and \
                    json.loads(sprite_src.read_text(encoding="utf-8"))["variant"] != traced["cutOn"]:
                problems.append(f"{rel}: its lamps were cut on {traced['cutOn']}, not the sprite's picture")
            if rel in legacy_set:
                problems.append(f"{rel}: traced and still on the legacy list")
        elif rel not in legacy_set:
            problems.append(f"{rel}: traces to nothing ({SOURCE} beside it, or the legacy list)")
    for rel in sorted(legacy_set):
        if not (base / rel).is_file():
            problems.append(f"{LEGACY}: {rel} is gone")
    for i, path in assets(base).items():
        d = json.loads(path.read_text(encoding="utf-8"))
        if not is_zones(d, i) or not d.get("zones") or not (zone_frame := sheet_of(base, path, i)):
            continue
        maps, car = zoned_sheet(base, zone_frame, frame_record(base, zone_frame)), sheet_of(base, path, d["id"])
        if maps != car:
            problems.append(f"{_sim.rel(base, path)}: its zone map is of {maps}, its sprite is cut from {car}")
    for listing in sorted((base / RAW).rglob(VARIANTS)):
        for r in read_jsonl(listing):
            if not (listing.parent / f"{r['variant']}.png").is_file():
                problems.append(f"{_sim.rel(base, listing)}: {r['variant']}.png is gone")
            problems += lineage_holes(base, r["frame"])
    return sorted(set(problems))


def lineage_holes(base, fid):
    holes, todo, seen = [], [fid], set()
    while todo:
        f = todo.pop()
        if f in seen:
            continue
        seen.add(f)
        kept = base / KEPT_FRAMES / f"{f}.json"
        if not kept.is_file():
            holes.append(f"{_sim.rel(base, kept)}: missing, so a kept variant traces to nothing")
            continue
        for inp in json.loads(kept.read_text(encoding="utf-8")).get("inputs", []):
            if inp.get("frame"):
                todo.append(inp["frame"])
            elif not (base / KEPT_INPUTS / f"{inp['sha']}.{inp['ext']}").is_file():
                holes.append(f"{f}: its {inp['role']} {inp['sha']}.{inp['ext']} is not in {KEPT_INPUTS}")
    return holes


# ---------------------------------------------------------------------------------------------- helpers

def assets(base) -> dict[str, Path]:
    """Every variant file under assets/ that states an id and an outline, by id."""
    found = {}
    for path in sorted((base / "assets").rglob("*.json")):
        if path.name.endswith(SOURCE):
            continue
        try:
            d = json.loads(path.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, UnicodeDecodeError):
            continue
        if isinstance(d, dict) and "id" in d and ("footprintM" in d or "diameterM" in d):
            found[d["id"]] = path
            if "collisionM" in d:
                found.setdefault(d["id"] + WRECK, path)
            if d.get("lamps") or "collisionM" in d:
                found.setdefault(d["id"] + ZONES, path)
    return found


def raw_dir(base, described: Path, i: str) -> Path:
    """raw_assets/ at the path the variant file has under assets/, one folder per asset id."""
    mirrored = base / RAW / described.parent.relative_to(base / "assets")
    return mirrored if described.parent.name == i else mirrored / i


def family(asset_rel: str) -> tuple[str, str, str]:
    """(grid figure, front edge, layout) of the family the variant file belongs to."""
    under = asset_rel.removeprefix("assets/")
    return next((f for prefix, f in FAMILIES.items() if under.startswith(prefix)), DEFAULT_FAMILY)


def grid(figures, asset_rel: str) -> float:
    return figures[family(asset_rel)[0]]


def frame_record(base, fid):
    kept = base / KEPT_FRAMES / f"{fid}.json"
    if kept.is_file():
        return json.loads(kept.read_text(encoding="utf-8"))
    for r in reversed(read_jsonl(base / INDEX)):
        if r.get("id") == fid:
            return r
    cli.fail(f"no record of frame {fid} in {KEPT_FRAMES} or {INDEX}")


def picture(base, inp):
    for where in (KEPT_INPUTS, INPUTS):
        path = base / where / f"{inp['sha']}.{inp['ext']}"
        if path.is_file():
            return path
    return None


def legacy_entries(base) -> set[str]:
    path = base / LEGACY
    if not path.is_file():
        return set()
    return {l.strip() for l in path.read_text(encoding="utf-8").splitlines() if l.strip() and not l.startswith("#")}


def listed_legacy(base, described: Path, i: str) -> bool:
    sprite = sprite_of(described, i)
    return bool(sprite) and _sim.rel(base, described.parent / sprite) in legacy_entries(base)


def read_jsonl(path: Path) -> list[dict]:
    if not path.is_file():
        return []
    out = []
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            out.append(json.loads(line))
        except json.JSONDecodeError:
            continue
    return out


def append(path: Path, record: dict):
    with path.open("a", encoding="utf-8") as f:
        f.write(json.dumps(record) + "\n")


if __name__ == "__main__":
    cli.entry(main)
