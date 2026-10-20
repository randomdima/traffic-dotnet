"""
The pictures of `qq props`: windows onto a car's sprite at any framing and angle, an edge unrolled into a strip, image
filters, a ruler grid and the props' parts named on them, laid out to fit a reader's pixel budget.

Everything written — titles, rulers, the mirror axis's figure — goes in a panel's margins; inside the image are only the
marks (outlines, vertex dots, part names placed outside the outline) and the grid, each of which can be turned off.
"""

import math
import types

import _props_fit as fit

OUTLINE = {"head": (70, 150, 255), "ind": (255, 170, 0), "brake": (255, 50, 110), "reverse": (0, 240, 240)}
#: The part a view is about, under its outline: a colour no kind's outline and no lamp's lens has.
FOCUS = (120, 255, 0)
LIGHT = {"head": (255, 244, 225), "ind": (255, 140, 10), "brake": (255, 25, 15), "reverse": (235, 242, 255)}
#: A lens burns in its lamp's colour by its own lightness, with a white core, and glows past its edge.
GLOW_SIGMA_PX, GLOW_GAIN, NIGHT = 6.0, 0.8, 0.45
#: What a panel can show. `plain` is the sprite as it is; `stretch`, `luma`, `sat` and `edges` are stretched to the
#: panel's own range, so they show a faint rim and their shades compare only within one panel.
FILTERS = ("plain", "stretch", "luma", "sat", "hue", "edges", "dark", "likeness", "alpha", "lit")
STRETCHED = {"stretch", "luma", "sat", "edges"}
LUMA = (0.299, 0.587, 0.114)
#: Each panel's margins hold its title line and its rulers, so nothing written covers the sprite.
MARGIN_LEFT_PX, MARGIN_TOP_PX, GAP_PX = 40, 30, 6
#: What a view shows past the sprite's edge: lighter than any ground, so it is not mistaken for the car.
BEYOND = (0.88, 0.9, 0.92)
#: An unrolled edge: how far either side of it the strip reaches and how far past each end, in texels; and how far
#: either side its rim dots are looked for.
STRIP_PX, STRIP_END_PX = 5, 2
#: The share of the sheet's height an edge's unrolled strips may take; its turned views have the rest. A strip is
#: drawn at its own zoom, since it is a few texels high.
STRIP_SHARE = 0.4
#: What a reader pays for a picture (Anthropic's vision guide): a token per this many pixels, after scaling one whose
#: long side or area is past these down to fit — which loses the detail a high zoom was for.
PX_PER_TOKEN, READER_LONG_PX, READER_AREA_PX = 750, 1568, 1_150_000
MAX_ZOOM, PANELS_A_ROW = 24, 4
#: How far out from its part a name is set, nearest first, in screen pixels.
LABEL_OUT_PX = (11, 18, 25, 32)
#: A grid line a texel apart is drawn from this zoom, and blended this far toward black or white, whichever the texel
#: under it is not; a labelled line every few texels further.
FINE_GRID_ZOOM, FINE_GRID_ALPHA, RULED_GRID_ALPHA = 6, 0.18, 0.42


def framed(points, angle_deg, pad, bounds=None, label="", origin=None, origin_name="its middle"):
    """A window holding `points` with `pad` texels round them, whose left-to-right runs along `angle_deg` (0 is +x, 90
    is +y, so 90 turns the picture a quarter anticlockwise). An unturned window is cut on whole texels and to
    `bounds` (w, h), and ruled in sprite x and y; a turned one is ruled along and up from `origin` (the points' middle
    by default), since sprite x and y no longer run along its sides."""
    import numpy as np

    c, s = math.cos(math.radians(angle_deg)), math.sin(math.radians(angle_deg))
    p = np.asarray(points, float).reshape(-1, 2)
    u, v = p[:, 0] * c + p[:, 1] * s, -p[:, 0] * s + p[:, 1] * c
    u0, u1 = math.floor(u.min() - pad), math.ceil(u.max() + pad)
    v0, v1 = math.floor(v.min() - pad), math.ceil(v.max() + pad)
    if bounds and angle_deg == 0:
        u0, v0, u1, v1 = max(u0, 0), max(v0, 0), min(u1, bounds[0]), min(v1, bounds[1])
    ox, oy = origin if origin is not None else p.mean(0)
    return types.SimpleNamespace(kind="map", angle=angle_deg, cos=c, sin=s, u0=u0, v0=v0, wt=max(u1 - u0, 1),
                                 ht=max(v1 - v0, 1), label=label, uo=ox * c + oy * s, vo=-ox * s + oy * c,
                                 origin_name=origin_name)


def aligned(points, i):
    """The angle that lays edge i of a polygon left to right with its outside up."""
    p0, p1 = fit.edge_ends(points, i)
    angle = math.degrees(math.atan2(p1[1] - p0[1], p1[0] - p0[0]))
    # Screen up is the window's -v; the outward normal is there when the winding is positive.
    angle += 0 if fit.winding(points) > 0 else 180
    return (angle + 180) % 360 - 180


def unrolled(points, i, reach, label=""):
    """Edge i of a polygon straightened: left to right along its arc from point i, outside up, its rim looked for
    within `reach` as snap and the edge table look."""
    p0, p1 = fit.edge_ends(points, i)
    bow = points[i][2]
    length = fit.arc_length(p0, p1, bow)
    return types.SimpleNamespace(kind="strip", p0=p0, p1=p1, bow=bow, s=fit.winding(points), length=length,
                                 edge=i, n=len(points), wt=math.ceil(length + 2 * STRIP_END_PX), ht=2 * STRIP_PX,
                                 reach=reach, label=label)


def at_pixels(view, zoom):
    """Every pixel's texel (x, y), and its two ruler coordinates: x and y for a window; along the arc and out from it for
    a strip."""
    import numpy as np

    U = (np.arange(view.wt * zoom) + 0.5) / zoom
    V = (np.arange(view.ht * zoom) + 0.5) / zoom
    if view.kind == "map":
        u, v = view.u0 + U[None, :], view.v0 + V[:, None]
        x, y = u * view.cos - v * view.sin, u * view.sin + v * view.cos
        if view.angle:
            return x, y, (np.broadcast_to(u - view.uo, x.shape), np.broadcast_to(view.vo - v, x.shape))
        return x, y, (x, y)
    along = U - STRIP_END_PX
    pts, nrm = fit.on_edge(view.p0, view.p1, view.bow, view.s, along / max(view.length, 1e-6))
    out = STRIP_PX - V
    x = pts[None, :, 0] + out[:, None] * nrm[None, :, 0]
    y = pts[None, :, 1] + out[:, None] * nrm[None, :, 1]
    return x, y, (np.broadcast_to(along[None, :], x.shape), np.broadcast_to(out[:, None], x.shape))


def to_screen(view, zoom, xy):
    """Texel points of a window as pixel positions in its panel, margins included."""
    import numpy as np

    xy = np.asarray(xy, float).reshape(-1, 2)
    u = xy[:, 0] * view.cos + xy[:, 1] * view.sin - view.u0
    v = -xy[:, 0] * view.sin + xy[:, 1] * view.cos - view.v0
    return np.stack([MARGIN_LEFT_PX + u * zoom, MARGIN_TOP_PX + v * zoom], 1)


def turned(view, n):
    """A texel direction as a direction in the window."""
    return n[0] * view.cos + n[1] * view.sin, -n[0] * view.sin + n[1] * view.cos


# Filters: each a field over the whole sprite, sampled by a view.

def field(name, car, doc, props, cache):
    import numpy as np
    from PIL import Image

    if name in cache:
        return cache[name]
    colour = car["colour"]
    if name in ("plain", "stretch"):
        out = colour
    elif name == "luma":
        out = grey(colour @ np.array(LUMA, np.float32))
    elif name == "sat":
        hi = colour.max(-1)
        out = grey((hi - colour.min(-1)) / np.maximum(hi, 1e-3))
    elif name == "hue":
        hsv = np.asarray(Image.fromarray((colour * 255).astype(np.uint8)).convert("HSV")).copy()
        hsv[..., 2] = np.clip(hsv[..., 1].astype(np.int32) * 2, 0, 255)
        hsv[..., 1] = 255
        out = np.asarray(Image.fromarray(hsv, "HSV").convert("RGB"), np.float32) / 255
    elif name == "edges":
        p = np.pad(colour, ((1, 1), (1, 1), (0, 0)), mode="edge")
        gx = (2 * (p[1:-1, 2:] - p[1:-1, :-2]) + p[:-2, 2:] - p[:-2, :-2] + p[2:, 2:] - p[2:, :-2])
        gy = (2 * (p[2:, 1:-1] - p[:-2, 1:-1]) + p[2:, :-2] - p[:-2, :-2] + p[2:, 2:] - p[:-2, 2:])
        out = grey(np.sqrt((gx ** 2 + gy ** 2).sum(-1)))
    elif name == "dark":
        out = grey(car["dark"])
    elif name == "alpha":
        out = grey(car["rgba"][..., 3])
    elif name == "likeness":
        h, w = colour.shape[:2]
        like = np.zeros((h, w), np.float32)
        for p in props:
            rs = fit.rings(doc, car, p)
            near = fit.window(fit.coverage(rs, (h, w)) > 0, fit.RING_PX[1] + 1, np.max)
            like = np.where(near, np.maximum(like, fit.likeness(car, rs, fit.pull_of(car, p)).core), like)
        out = grey(like)
    elif name == "lit":
        out = lit(car, doc)
    cache[name] = out
    return out


def grey(a):
    import numpy as np

    return np.repeat(np.asarray(a, np.float32)[..., None], 3, 2)


def stretched(img, inside):
    """Each channel spread over 0..1 between its 1st and 99th percentile within the sprite."""
    import numpy as np

    if not inside.any():
        return img
    lo, hi = np.percentile(img[inside], [1, 99], axis=0)
    return np.clip((img - lo) / np.maximum(hi - lo, 1e-3), 0, 1)


def lit(car, doc, by_kind=None):
    """The sprite at night with every prop burning in its lamp's colour, and its glow: RGB 0..1."""
    import numpy as np

    rgba = car["rgba"]
    out = car["colour"] * (NIGHT * rgba[..., 3:4] + (1 - rgba[..., 3:4]))
    lum = rgba[..., :3] @ np.array(LUMA, np.float32)
    solid = (rgba[..., 3] > 0.5).astype(np.float32)
    glow = np.zeros_like(out)
    # The biggest first, so an indicator drawn over its main lamp burns on top of it.
    for kind, cov in sorted((by_kind or kind_coverage(car, doc)).items(), key=lambda kc: -kc[1].sum()):
        cov = cov * solid
        if not (cov > 0.5).any():
            continue
        lo, hi = np.percentile(lum[cov > 0.5], [5, 95])
        l = np.clip((lum - lo) / max(hi - lo, 1e-3), 0, 1)[..., None]
        c = np.array(LIGHT.get(kind, (255, 255, 255)), np.float32) / 255
        burn = c * (0.7 + 0.3 * l) + (1 - c) * np.clip((l - 0.6) / 0.4, 0, 1) ** 2 * 0.7
        out = out * (1 - cov[..., None]) + burn * cov[..., None]
        glow += fit.blur(cov, GLOW_SIGMA_PX)[..., None] * c * GLOW_GAIN
    return np.clip(out + glow, 0, 1)


def kind_coverage(car, doc):
    import numpy as np

    shape = car["rgba"].shape[:2]
    out = {}
    for p in doc["props"]:
        out[p["kind"]] = np.maximum(out.get(p["kind"], 0), fit.coverage(fit.rings(doc, car, p), shape))
    return out


# Panels and the sheet.

def scene(doc, car, a, chosen):
    """The groups of views the options ask for, each with the share of the sheet's height it may take, and the props
    marked on them."""
    h, w = car["rgba"].shape[:2]
    bounds, angle = (w, h), a.angle or 0.0
    if chosen is None and a.at is None:
        if a.end == "both":
            spans = {"rear": (0, car["end"]), "front": (w - car["end"], w)}
            views = [framed([(x0, 0), (x1, h)], angle, 0, bounds, label=e) for e, (x0, x1) in spans.items()]
            return [(views, 1.0)], doc["props"], []
        mine = [p for p in doc["props"] if fit.end_of(doc, car, p) == a.end]
        if not mine:
            return [([framed([(0, 0), (w, h)], angle, 0, bounds, label="whole car")], 1.0)], doc["props"], []
        sides = [fit.rings(doc, car, p) for p in mine]
        views = [framed([pt for s in sides if len(s) > k for pt in s[k]], angle, a.pad, bounds,
                        label=f"{a.end} {('authored', 'copy')[k]}") for k in range(max(map(len, sides)))]
        return [(views, 1.0)], mine, []
    if chosen is None:
        at = framed([a.at], angle, a.pad, bounds, label=f"at {a.at[0]:g},{a.at[1]:g}", origin=a.at,
                    origin_name=f"{a.at[0]:g},{a.at[1]:g}")
        return [([at], 1.0)], doc["props"], []
    p = chosen
    sides = fit.sides_of(doc, car, p)
    names = ("authored", "copy")
    if a.edge is not None:
        maps, strips = [], []
        for k, pts in enumerate(sides):
            p0, p1 = fit.edge_ends(pts, a.edge)
            arc = fit.on_edge(p0, p1, pts[a.edge][2], fit.winding(pts), [0, 0.25, 0.5, 0.75, 1])[0]
            turn = a.angle if a.angle is not None else aligned(pts, a.edge)
            maps.append(framed(arc, turn, a.pad, bounds, label=f"{p['name']} e{a.edge} {names[k]}", origin=p0,
                               origin_name=f"v{a.edge}"))
            strips.append(unrolled(pts, a.edge, a.reach, label=f"{p['name']} e{a.edge} {names[k]} unrolled"))
        return [(maps, 1 - STRIP_SHARE), (strips, STRIP_SHARE)], [p], [("edge", a.edge)]
    if a.vertex is not None:
        views = [framed([pts[a.vertex][:2]], angle, a.pad, bounds, label=f"{p['name']} v{a.vertex} {names[k]}",
                        origin=pts[a.vertex][:2], origin_name=f"v{a.vertex}") for k, pts in enumerate(sides)]
        return [(views, 1.0)], [p], [("vertex", a.vertex)]
    rs = fit.rings(doc, car, p)
    views = [framed(r, angle, a.pad, bounds, label=f"{p['name']} {names[k]}") for k, r in enumerate(rs) if len(r)]
    return [(views, 1.0)], [p], []


def show(doc, car, a, chosen, out):
    """Draws the views the options ask for into one sheet at `out`; what it is and what it costs to read, as lines."""
    from PIL import Image

    groups, props, focus = scene(doc, car, a, chosen)
    panels = [(f, True) for f in a.filter] + ([(f, False) for f in a.filter if f != "lit"] if a.bare else [])
    cache, tiles, zooms = {}, [], []
    for views, share in groups:
        # One panel a view: the views side by side; several: a row a view, its panels across, a few a row.
        grid = []
        for row in ([[(v, f, m) for v in views for f, m in panels]] if len(panels) == 1 else
                    [[(v, f, m) for f, m in panels] for v in views]):
            grid += [row[i:i + PANELS_A_ROW] for i in range(0, len(row), PANELS_A_ROW)]
        zoom = a.zoom or best_zoom(grid, a.max_px, share * a.max_px)
        zooms.append(f"{views[0].kind}s x{zoom}" if len(groups) > 1 else f"x{zoom}")
        for row in grid:
            tiles.append([panel(v, zoom, field(f, car, doc, props, cache), f, doc, car, props, focus,
                                a.marks if marked and f != "lit" else "off", a.grid) for v, f, marked in row])
    sheet = compose(tiles)
    Image.fromarray(sheet).save(out)
    h, w = sheet.shape[:2]
    tokens, scale = reader_cost(w, h)
    lines = [f"{w}x{h} px, {', '.join(zooms)} (screen px a texel), about {tokens} tokens to look at"]
    if scale < 1:
        lines.append(f"  past a reader's {READER_LONG_PX} px or {READER_AREA_PX / 1e6:.2f} MP: it is shrunk to "
                     f"{scale:.2f} before it is seen; a lower --zoom or --pad, or fewer filters, keep every pixel")
    return lines


def best_zoom(grid, max_w, max_h):
    for zoom in range(MAX_ZOOM, 0, -1):
        w, h = sheet_size(grid, zoom)
        if w <= max_w and h <= max_h:
            return zoom
    return 1


def sheet_size(grid, zoom):
    widths = [sum(v.wt * zoom + MARGIN_LEFT_PX for v, _, _ in row) + GAP_PX * (len(row) - 1) for row in grid]
    heights = [max(v.ht * zoom + MARGIN_TOP_PX for v, _, _ in row) for row in grid]
    return max(widths), sum(heights) + GAP_PX * (len(grid) - 1)


def reader_cost(w, h):
    scale = min(1.0, READER_LONG_PX / max(w, h), math.sqrt(READER_AREA_PX / (w * h)))
    return math.ceil(w * scale * h * scale / PX_PER_TOKEN), scale


def compose(tiles):
    import numpy as np

    rows = []
    for row in tiles:
        hmax = max(t.shape[0] for t in row)
        cells = []
        for i, t in enumerate(row):
            cells.append(np.pad(t, ((0, hmax - t.shape[0]), (0, GAP_PX if i < len(row) - 1 else 0), (0, 0)),
                                constant_values=255))
        rows.append(np.concatenate(cells, 1))
    wmax = max(r.shape[1] for r in rows)
    return np.concatenate([np.pad(r, ((0, GAP_PX if i < len(rows) - 1 else 0), (0, wmax - r.shape[1]), (0, 0)),
                                  constant_values=255) for i, r in enumerate(rows)], 0)


def panel(view, zoom, rgb, name, doc, car, props, focus, marks, grid):
    """One view through one filter, its title and rulers in the margins and the marks over it."""
    import numpy as np
    from PIL import Image, ImageDraw, ImageFont

    h, w = rgb.shape[:2]
    x, y, ruled = at_pixels(view, zoom)
    ix, iy = np.floor(x).astype(int), np.floor(y).astype(int)
    inside = (ix >= 0) & (iy >= 0) & (ix < w) & (iy < h)
    img = np.where(inside[..., None], rgb[np.clip(iy, 0, h - 1), np.clip(ix, 0, w - 1)], np.array(BEYOND, np.float32))
    if name in STRETCHED:
        img = stretched(img, inside)
    every = rule_steps(view, zoom)
    img = gridded(img, ruled, every, zoom, grid)
    area = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    font, small = ImageFont.load_default(size=11), ImageFont.load_default(size=10)
    # The zoom and the axes are in the text show prints; a title says only what tells one panel from another.
    title = f"{name} | {view.label}"
    if view.kind == "map" and view.angle:
        title += f" | turned {view.angle:.0f} deg, ruled along and up from {view.origin_name}"
    if view.kind == "strip":
        title += f" | v{view.edge} at 0, v{(view.edge + 1) % view.n} at {view.length:.1f}"
    axis = axis_of(doc, car, props, view) if view.kind == "map" and marks != "off" else None
    # A panel narrower than its title is widened to it, so the title is never cut.
    width = max(area.width + MARGIN_LEFT_PX, math.ceil(font.getlength(title)) + 4)
    im = Image.new("RGB", (width, area.height + MARGIN_TOP_PX), (255, 255, 255))
    im.paste(area, (MARGIN_LEFT_PX, MARGIN_TOP_PX))
    draw = ImageDraw.Draw(im, "RGBA")
    rulers(draw, view, ruled, every, small)
    draw.text((2, 2), title, fill=(0, 0, 0), font=font)
    if marks == "off":
        return np.asarray(im)
    # The marks go on a layer the image's size, so none spills into the margins.
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ink = ImageDraw.Draw(layer, "RGBA")
    if view.kind == "map":
        if axis is not None:
            axis_line(ink, view, zoom, axis)
        map_marks(ink, view, zoom, doc, car, props, focus, marks, small)
    else:
        strip_marks(ink, view, zoom, doc, car, props[0], marks, small)
    layer = layer.crop((MARGIN_LEFT_PX, MARGIN_TOP_PX, MARGIN_LEFT_PX + area.width, MARGIN_TOP_PX + area.height))
    im.paste(layer, (MARGIN_LEFT_PX, MARGIN_TOP_PX), layer)
    return np.asarray(im)


def rule_steps(view, zoom):
    """Texels between labelled lines, for each ruler coordinate."""
    if view.kind == "strip":
        return (5 if zoom >= 6 else 10), (1 if zoom >= 8 else 2)
    step = 5 if zoom >= 6 else 10 if zoom >= 3 else 20
    return step, step


def crossings(g, step):
    """Where a coordinate passes a multiple of `step` between neighbouring pixels: a boolean map."""
    import numpy as np

    f = np.floor(g / step)
    edge = np.zeros(g.shape, bool)
    edge[:, 1:] |= f[:, 1:] != f[:, :-1]
    edge[1:, :] |= f[1:, :] != f[:-1, :]
    return edge


def gridded(img, ruled, every, zoom, grid):
    import numpy as np

    if grid == "off":
        return img
    ink = np.where((img @ np.array(LUMA, np.float32))[..., None] > 0.55, 0.0, 1.0)
    out = img
    if grid == "on" and zoom >= FINE_GRID_ZOOM:
        fine = (crossings(ruled[0], 1) | crossings(ruled[1], 1))[..., None]
        out = np.where(fine, out * (1 - FINE_GRID_ALPHA) + ink * FINE_GRID_ALPHA, out)
    major = (crossings(ruled[0], every[0]) | crossings(ruled[1], every[1]))[..., None]
    return np.where(major, out * (1 - RULED_GRID_ALPHA) + ink * RULED_GRID_ALPHA, out)


def rulers(draw, view, ruled, every, font):
    """The labelled lines' figures and ticks where they meet the top and left edges of the image."""
    import numpy as np

    if view.kind == "map" and not view.angle:
        fmt = [lambda v: f"{v:g}", lambda v: f"{v:g}"]
    else:
        fmt = [lambda v: f"{v:g}", lambda v: f"{v:+g}" if v else "0"]
    for edge in ("top", "left"):
        last = -1e9
        # One coordinate a margin, whichever crosses it more often: on a turned view both cross both, and two
        # interleaved scales read as one wrong one.
        found = []
        for k in (0, 1):
            g = ruled[k][0, :] if edge == "top" else ruled[k][:, 0]
            f = np.floor(g / every[k])
            found.append([(i + 1, max(f[i], f[i + 1]) * every[k], fmt[k]) for i in np.nonzero(f[1:] != f[:-1])[0]])
        for pos, value, show in max(found, key=len):
            text = show(value)
            if edge == "top":
                px = MARGIN_LEFT_PX + pos
                draw.line([(px, MARGIN_TOP_PX - 4), (px, MARGIN_TOP_PX - 1)], fill=(0, 0, 0))
                width = draw.textlength(text, font=font)
                if px - width / 2 > last + 3:
                    draw.text((px, MARGIN_TOP_PX - 5), text, fill=(0, 0, 0), font=font, anchor="ms")
                    last = px + width / 2
            else:
                py = MARGIN_TOP_PX + pos
                draw.line([(MARGIN_LEFT_PX - 4, py), (MARGIN_LEFT_PX - 1, py)], fill=(0, 0, 0))
                if py - 5 > last + 1:
                    draw.text((MARGIN_LEFT_PX - 6, py), text, fill=(0, 0, 0), font=font, anchor="rm")
                    last = py + 5


def axis_of(doc, car, props, view):
    """The mirror axis worth drawing: the one prop's own, or the end's the view looks at."""
    if len(props) == 1:
        return fit.copy_at(doc, car, props[0])[0]
    w = car["rgba"].shape[1]
    cx = (view.u0 + view.wt / 2) * view.cos - (view.v0 + view.ht / 2) * view.sin
    return doc["axisY"]["front" if cx > w / 2 else "rear"]


def axis_line(ink, view, zoom, axis):
    import numpy as np

    span = view.wt + view.ht
    cx = (view.u0 + view.wt / 2) * view.cos - (view.v0 + view.ht / 2) * view.sin
    a, b = to_screen(view, zoom, [(cx - span, axis), (cx + span, axis)])
    length = float(np.hypot(*(b - a)))
    for d in np.arange(0, length, 12):
        p, q = a + (b - a) * d / length, a + (b - a) * min(d + 6, length) / length
        ink.line([tuple(p), tuple(q)], fill=(255, 255, 255, 170), width=1)


def stroke(ink, pts, colour, width, dashed=False):
    """A polyline over a dark under-stroke, so it reads on a bright lens; a dashed one draws every other stretch."""
    pts = [tuple(p) for p in pts]
    step, run = (4, 2) if dashed else (1, 1)
    for under in (True, False):
        for i in range(0, len(pts) - 1, step):
            seg = pts[i:i + run + 1]
            ink.line(seg, fill=(0, 0, 0, 170) if under else colour + (255,), width=width + 2 if under else width)


def map_marks(ink, view, zoom, doc, car, props, focus, marks, font):
    import numpy as np

    width = 2 if zoom >= 6 else 1
    named = len(props) == 1 and "points" in props[0]
    for p in props:
        colour = OUTLINE.get(p["kind"], (230, 230, 230))
        rs = fit.rings(doc, car, p)
        sides = fit.sides_of(doc, car, p) or []
        for kind, i in focus:
            for pts in sides:
                s = fit.winding(pts)
                if kind == "edge":
                    p0, p1 = fit.edge_ends(pts, i)
                    arc = fit.on_edge(p0, p1, pts[i][2], s, np.linspace(0, 1, 24))[0]
                    ink.line([tuple(q) for q in to_screen(view, zoom, arc)], fill=FOCUS + (150,), width=width + 6)
                else:
                    sx, sy = to_screen(view, zoom, [pts[i][:2]])[0]
                    ink.ellipse([sx - 7, sy - 7, sx + 7, sy + 7], outline=FOCUS + (230,), width=2)
        for k, ring in enumerate(rs):
            if len(ring) >= 3:
                scr = to_screen(view, zoom, np.vstack([ring, ring[:1]]))
                stroke(ink, scr, colour, width, dashed=k > 0)
        for k, pts in enumerate(sides):
            scr = to_screen(view, zoom, [q[:2] for q in pts])
            for sx, sy in scr:
                ink.ellipse([sx - 2.5, sy - 2.5, sx + 2.5, sy + 2.5], fill=colour + (255,), outline=(0, 0, 0, 255))
            if marks != "full" or not named:
                continue
            s, placed = fit.winding(pts), []
            for i in range(len(pts)):
                placed.append(name_out(ink, placed, scr[i], turned(view, fit.vertex_normal(pts, i)), f"v{i}",
                                       (255, 255, 255), font))
                p0, p1 = fit.edge_ends(pts, i)
                mid, nrm = fit.on_edge(p0, p1, pts[i][2], s, [0.5])
                placed.append(name_out(ink, placed, to_screen(view, zoom, mid)[0], turned(view, nrm[0]), f"e{i}",
                                       colour, font))
        if marks == "full" and not named and len(rs) and len(rs[0]):
            # Beside the prop rather than on it, so the name hides no lens.
            scr = to_screen(view, zoom, rs[0])
            label(ink, (scr[:, 0].max() + 4, scr[:, 1].min() - 4), p["name"], colour, font, anchor="ls")


def name_out(ink, placed, at, direction, text, colour, font):
    """A part's name set out from it along `direction`, further out where nearer would cover a name already set:
    the box it took."""
    half_w, half_h = font.getlength(text) / 2 + 2, 7
    for d in LABEL_OUT_PX:
        x, y = at[0] + d * direction[0], at[1] + d * direction[1]
        box = (x - half_w, y - half_h, x + half_w, y + half_h)
        if not any(box[0] < b[2] and b[0] < box[2] and box[1] < b[3] and b[1] < box[3] for b in placed):
            break
    label(ink, (x, y), text, colour, font)
    return box


def label(ink, at, text, colour, font, anchor="mm"):
    ink.text(at, text, fill=colour + (255,), font=font, anchor=anchor, stroke_width=2, stroke_fill=(0, 0, 0, 255))


def strip_marks(ink, view, zoom, doc, car, p, marks, font):
    """The edge as the line at 0, its ends as uprights, and where each of its samples finds the rim on this side:
    a dot where that rim is clear enough for snap to count, a grey cross where not, and the line snap's shift, tilt
    and bow would fit through the dots."""
    import numpy as np

    colour = OUTLINE.get(p["kind"], (230, 230, 230))

    def at(along, out):
        return (MARGIN_LEFT_PX + (np.asarray(along) + STRIP_END_PX) * zoom,
                MARGIN_TOP_PX + (STRIP_PX - np.asarray(out)) * zoom)

    stroke(ink, [at(a, 0) for a in (-STRIP_END_PX, view.length + STRIP_END_PX)], colour, 1)
    for a in (0, view.length):
        x0, y0 = at(a, STRIP_PX)
        x1, y1 = at(a, -STRIP_PX)
        for y in np.arange(y0, y1, 8):
            ink.line([(x0, y), (x0, min(y + 4, y1))], fill=(255, 255, 255, 200), width=1)
    if marks != "full":
        return
    like = fit.likeness(car, fit.rings(doc, car, p), fit.pull_of(car, p))
    t, offs, score = fit.rim_search(view.p0, view.p1, view.bow, view.s, like, None, view.reach)
    o0, o1, bend, _, conf = fit.edge_fit(view.p0, view.p1, view.bow, view.s, like, None, view.reach)
    if conf:
        tt = np.linspace(0, 1, 24)
        curve = [at(u * view.length, o0 + (o1 - o0) * u + bend * 4 * u * (1 - u)) for u in tt]
        ink.line(curve, fill=(0, 0, 0, 200), width=4)
        ink.line(curve, fill=(255, 230, 0, 255), width=2)
    for ti, o, c in zip(t, offs, score):
        x, y = at(ti * view.length, o)
        if c >= fit.MIN_CONTRAST:
            ink.ellipse([x - 4, y - 4, x + 4, y + 4], fill=(255, 230, 0, 255), outline=(0, 0, 0, 255), width=1)
        else:
            ink.line([(x - 3, y - 3), (x + 3, y + 3)], fill=(150, 150, 150, 255), width=2)
            ink.line([(x - 3, y + 3), (x + 3, y - 3)], fill=(150, 150, 150, 255), width=2)
