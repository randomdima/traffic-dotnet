"""
A frame asked without a layout (the owner, 2026-10-20): which drawing is which asset, and what each measures.

The brief lists the assets in a grid, left to right and top to bottom, on a transparent background. The drawings are
found as the frame's bands of paint, row by row and column by column, and named by that order. A car's geometry is
read off its drawing: its length is the plan's (the one figure a picture cannot carry), and everything else scales
from it — the body's width as the median of its flanks, which a mirror does not move, its corner radius, the footprint
holding everything drawn, and the body's width over each axle for the track (CAR-12).
"""

from __future__ import annotations

#: A run of paint narrower than this across a band is a speck between drawings, not one of them.
MIN_RUN_PX = 12
#: How many columns a body's end needs painted at least this deep to count as its end.
END_MIN_PX = 3
#: The share of a body's length at each end left out when its flanks are read: the corners round in there.
FLANK_SKIP = 0.2
#: How far the body is grown past its alpha-over-half edge to keep the drawing's own anti-aliased rim, and the least
#: alpha a rim texel keeps: the model's transparent output wraps every drawing in a faint glow of its own colours
#: (alpha under ~100, cars_fleet_b), which is no part of the car.
RIM_PX, RIM_ALPHA_MIN = 2, 64
#: How far inside the flank each wheel centre stands (CAR-12): `Tyre.WheelWidthM` × (½ − `ShowsPastTheBodyworkShare`),
#: so that share of every tyre shows past the bodywork and the rest is under it.
TYRE_INSET_M = 0.22 * (0.5 - 0.25)


#: A building's walls (OBJ-5a) read off its drawing: the silhouette on cells of this side, or coarser so its longer side
#: is at most PART_CELLS_MOST of them, a cell painted where over half of it is; at most PARTS_MOST rectangles, and none
#: kept that covers under PART_LEAST_SHARE of the silhouette on its own.
PART_CELL_M, PART_CELLS_MOST, PARTS_MOST, PART_LEAST_SHARE = 0.25, 80, 6, 0.02


def rectangles(solid, per_m):
    """The rectangles a building's silhouette is built of, each (centre x, centre y, width, depth) in metres from the
    picture's middle, +y down it — as `partsM` authors them. One where the drawing is a plain block. Greedy: each pass
    takes the all-painted rectangle covering the most not yet covered, until what is left is under PART_LEAST_SHARE."""
    import numpy as np

    step = max(1, round(PART_CELL_M * per_m), -(-max(solid.shape) // PART_CELLS_MOST))
    h, w = solid.shape[0] // step, solid.shape[1] // step
    cells = solid[:h * step, :w * step].reshape(h, step, w, step).mean(axis=(1, 3)) > 0.5
    painted = cells.tolist()
    left = cells.copy()
    total = int(cells.sum())
    found = []
    while len(found) < PARTS_MOST and left.sum() >= PART_LEAST_SHARE * total:
        # Summed area of what is left, so a candidate's gain is four lookups.
        area = np.zeros((h + 1, w + 1), dtype=int)
        area[1:, 1:] = left.cumsum(0).cumsum(1)
        area = area.tolist()
        best, gain = None, 0
        heights = [0] * w
        for y in range(h):
            heights = [heights[x] + 1 if painted[y][x] else 0 for x in range(w)]
            for x0 in range(w):
                low = heights[x0]
                for x1 in range(x0, w):
                    low = min(low, heights[x1])
                    if not low:
                        break
                    y0 = y - low + 1
                    new = area[y + 1][x1 + 1] - area[y0][x1 + 1] - area[y + 1][x0] + area[y0][x0]
                    if new > gain:
                        best, gain = (x0, y0, x1 + 1, y + 1), new
        if best is None or gain < PART_LEAST_SHARE * total:
            break
        x0, y0, x1, y1 = best
        left[y0:y1, x0:x1] = False
        found.append(best)
    cx, cy = solid.shape[1] / 2, solid.shape[0] / 2
    return [(round((x0 + x1) * step / 2 / per_m - cx / per_m, 2), round((y0 + y1) * step / 2 / per_m - cy / per_m, 2),
             round((x1 - x0) * step / per_m, 2), round((y1 - y0) * step / per_m, 2)) for x0, y0, x1, y1 in found]


def track_m(axle_widths_m):
    """The track over a body whose width over each axle is measured: the wheel centres in from the wider flank."""
    return round(max(axle_widths_m) - 2 * TYRE_INSET_M, 3)


def installed_track_m(sprite, footprint_length_m, axles_m):
    """The track measured off an installed sprite where the game lays it: the sprite spans the footprint, and the axles
    stand off its middle. What CarSpriteTests measures, so the file and the gate read one ruler."""
    import numpy as np
    from PIL import Image

    solid = np.asarray(Image.open(sprite).convert("RGBA"))[..., 3] >= 128
    per_m = solid.shape[1] / footprint_length_m
    return track_m([width_at(solid, solid.shape[1] / 2 + a * per_m) / per_m for a in axles_m])


def runs(any_paint):
    """(start, end) of each run of True, end exclusive, at least MIN_RUN_PX long."""
    out, start = [], None
    for n, on in enumerate(list(any_paint) + [False]):
        if on and start is None:
            start = n
        elif not on and start is not None:
            if n - start >= MIN_RUN_PX:
                out.append((start, n))
            start = None
    return out


def drawings(solid, ids, fail):
    """Each id's box (x0, y0, x1, y1) on the frame, in the brief's order: bands of rows, then columns in each band.
    Only where that finds more drawings than ids are gaps up to BRIDGE_PX closed, narrowest first: a stack of crates or
    kegs is drawn with clear gaps between its pieces, which split one drawing into several."""
    for bridge in (0, *BRIDGE_PX):
        boxes = []
        for y0, y1 in bridged(runs(solid.any(1)), bridge):
            boxes += [(x0, y0, x1, y1) for x0, x1 in bridged(runs(solid[y0:y1].any(0)), bridge)]
        if len(boxes) <= len(ids):
            break
    if len(boxes) != len(ids):
        fail(f"found {len(boxes)} drawings for {len(ids)} ids: the grid was not kept, so which is which is a guess")
    return dict(zip(ids, boxes))


#: The gaps `drawings` closes in turn, frame pixels: well under the room the model keeps between two drawings.
BRIDGE_PX = (8, 16, 32)


def bridged(spans, gap):
    """Runs closer than `gap` joined into one."""
    out = []
    for start, end in spans:
        if out and start - out[-1][1] < gap:
            out[-1] = (out[-1][0], end)
        else:
            out.append((start, end))
    return out


def flanks(body):
    """(left, right, top, bottom) of a body mask, nose to the right: its ends where END_MIN_PX deep, and its flanks as
    the median edge of its middle columns, which a mirror or a rounded corner does not move."""
    import numpy as np

    deep = np.flatnonzero(body.sum(0) >= END_MIN_PX)
    left, right = int(deep[0]), int(deep[-1]) + 1
    skip = round((right - left) * FLANK_SKIP)
    middle = body[:, left + skip:right - skip]
    filled = middle.any(0)
    tops = np.argmax(middle, 0)[filled]
    bottoms = middle.shape[0] - np.argmax(middle[::-1], 0)[filled]
    return left, right, int(np.median(tops)), int(np.median(bottoms))


def body_in(solid, box):
    """The one drawing in the box: the paint joined to the painted pixel nearest its middle, as a mask the box's size.
    A speck beside it, or a mirror the drawing left unjoined, is not the body."""
    import numpy as np
    from PIL import Image, ImageDraw

    x0, y0, x1, y1 = box
    crop = solid[y0:y1, x0:x1]
    ys, xs = np.nonzero(crop)
    nearest = np.argmin((xs - crop.shape[1] / 2) ** 2 + (ys - crop.shape[0] / 2) ** 2)
    marked = Image.fromarray(crop.astype(np.uint8) * 255).copy()
    ImageDraw.floodfill(marked, (int(xs[nearest]), int(ys[nearest])), 128)
    return np.asarray(marked) == 128


def measure(body, length_m, axles_m):
    """The body's geometry from its mask (True where painted), the nose to the right: pixels per metre from the plan's
    length, and in metres the body's width and corner radius, the footprint, and the width over each axle."""
    import numpy as np

    left, right, top, bottom = flanks(body)
    per_m = (right - left) / length_m
    cx, cy = (left + right) / 2, (top + bottom) / 2
    ys, xs = np.nonzero(body)
    half_l = max(cx - xs.min(), xs.max() + 1 - cx)
    half_w = max(cy - ys.min(), ys.max() + 1 - cy)

    return {
        "pxPerM": round(per_m, 3),
        "lengthM": round(length_m, 3),
        "widthM": round((bottom - top) / per_m, 3),
        "cornerRadiusM": round(corner_radius_px(body[top:bottom, left:right]) / per_m, 3),
        "footprintM": [round(2 * half_l / per_m, 3), round(2 * half_w / per_m, 3)],
        "axleWidthsM": [round(width_at(body, cx + a * per_m) / per_m, 3) for a in axles_m],
        "centrePx": [round(cx, 1), round(cy, 1)],
        "bodyPx": [left, top, right, bottom],
    }


def width_at(body, x_px):
    """The body's width in pixels across column `x_px`: the median span of the five columns about it, which a mirror's
    edge or a stray texel does not move."""
    import numpy as np

    column = int(round(x_px))
    spans = [body[:, c] for c in range(column - 2, column + 3) if 0 <= c < body.shape[1]]
    return float(np.median([np.flatnonzero(s)[-1] + 1 - np.flatnonzero(s)[0] for s in spans if s.any()]))


def corner_radius_px(inside):
    """The corner radius of the rounded rectangle that best matches the body inside its own box."""
    import numpy as np

    h, w = inside.shape
    ys, xs = np.mgrid[0:h, 0:w] + 0.5
    best, best_score = 0, -1.0
    for r in range(0, min(h, w) // 2 + 1):
        dx = np.maximum(np.maximum(r - xs, xs - (w - r)), 0)
        dy = np.maximum(np.maximum(r - ys, ys - (h - r)), 0)
        shape = dx * dx + dy * dy <= r * r
        score = (shape & inside).sum() / (shape | inside).sum()
        if score > best_score:
            best, best_score = r, score
    return best


def kept_rim(body, alpha):
    """The body grown by RIM_PX where the drawing still has RIM_ALPHA_MIN, so its anti-aliased edge comes with it."""
    import numpy as np

    grown = body.copy()
    for _ in range(RIM_PX):
        g = grown.copy()
        g[1:] |= grown[:-1]
        g[:-1] |= grown[1:]
        g[:, 1:] |= grown[:, :-1]
        g[:, :-1] |= grown[:, 1:]
        grown = g
    return grown & (alpha >= RIM_ALPHA_MIN)


def footprint_crop(rgba, box, body, m):
    """The drawing cut to its footprint, centred on its body, everything but the body clear: (pixels, frame origin)."""
    import numpy as np

    x0, y0 = box[0], box[1]
    keep = np.zeros(rgba.shape[:2], bool)
    keep[y0:y0 + body.shape[0], x0:x0 + body.shape[1]] = kept_rim(body, rgba[y0:y0 + body.shape[0], x0:x0 + body.shape[1], 3])
    held = rgba.copy()
    held[~keep] = 0
    # The body itself is solid: the model's comes back at alpha ~253, a car one per cent see-through.
    inside = np.zeros_like(keep)
    inside[y0:y0 + body.shape[0], x0:x0 + body.shape[1]] = body
    held[inside, 3] = 255
    per_m = m["pxPerM"]
    w, h = round(m["footprintM"][0] * per_m), round(m["footprintM"][1] * per_m)
    ox, oy = round(x0 + m["centrePx"][0] - w / 2), round(y0 + m["centrePx"][1] - h / 2)
    out = np.zeros((h, w, 4), np.uint8)
    sx0, sy0 = max(0, ox), max(0, oy)
    sx1, sy1 = min(held.shape[1], ox + w), min(held.shape[0], oy + h)
    out[sy0 - oy:sy1 - oy, sx0 - ox:sx1 - ox] = held[sy0:sy1, sx0:sx1]
    return out, (ox, oy)


def aligned(zone_rgba, zone_box, zone_body, zone_m, car_m, car_origin, car_size):
    """A zone map's drawing laid onto its car's: scaled so its body box is the car's, then cut to the car's footprint
    crop (car_origin on the car frame, car_size) — so the zone texels land on the sprite's."""
    import numpy as np
    from PIL import Image

    zx0, zy0 = zone_box[0], zone_box[1]
    keep = np.zeros(zone_rgba.shape[:2], bool)
    keep[zy0:zy0 + zone_body.shape[0], zx0:zx0 + zone_body.shape[1]] = kept_rim(
        zone_body, zone_rgba[zy0:zy0 + zone_body.shape[0], zx0:zx0 + zone_body.shape[1], 3])
    held = zone_rgba.copy()
    held[~keep] = 0
    zl, zt, zr, zb = (v + o for v, o in zip(zone_m["bodyPx"], (zx0, zy0, zx0, zy0)))
    cl, ct, cr, cb = car_m["bodyFramePx"]
    kx, ky = (zr - zl) / (cr - cl), (zb - zt) / (cb - ct)
    # The affine maps an output pixel (on the car frame) to the zone frame's.
    image = Image.fromarray(held, "RGBA")
    moved = image.transform(car_size, Image.AFFINE,
                            (kx, 0, zl + (car_origin[0] - cl) * kx, 0, ky, zt + (car_origin[1] - ct) * ky),
                            resample=Image.NEAREST, fillcolor=(0, 0, 0, 0))
    return np.asarray(moved), (kx, ky)


def lenses(zones, per_m, palette, fail):
    """The lamp rows a zone map paints, on the sprite's own grid (the nose to the right, +y down, from its centre):
    per fitting, one lens each end and side it is expected at, its centre and its box. Fails on a lens missing."""
    import numpy as np

    h, w = zones.shape[:2]
    expected = {"head": ("front",), "indicator": ("front", "rear"), "brake": ("rear",), "reverse": ("rear",)}
    rows, missing = [], []
    for fitting, ends in expected.items():
        mask = (zones[..., 3] > 0) & (zones[..., :3] == palette[fitting]).all(-1)
        ys, xs = np.nonzero(mask)
        for end in ends:
            for side in ("left", "right"):
                pick = ((xs >= w / 2) if end == "front" else (xs < w / 2)) & ((ys < h / 2) if side == "left" else (ys >= h / 2))
                if not pick.any():
                    missing.append(f"{end} {side} {fitting}")
                    continue
                px, py = xs[pick], ys[pick]
                centre = ((px.mean() + 0.5) / per_m - w / per_m / 2, (py.mean() + 0.5) / per_m - h / per_m / 2)
                size = ((px.max() + 1 - px.min()) / per_m, (py.max() + 1 - py.min()) / per_m)
                rows.append((fitting, (round(centre[0], 3), round(centre[1], 3)), (round(size[0], 3), round(size[1], 3))))
    if missing:
        fail(f"the zone map paints no {', '.join(missing)}")
    return rows
