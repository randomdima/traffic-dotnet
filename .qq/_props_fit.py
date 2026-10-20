"""
The geometry and the fit of `qq props`: a prop's outline, the part it was put on, and pulling it onto that part's rim.

A point is (x, y) in sprite texels; a prop's outward side is told by its winding, so either winding is fine. Edge i
runs from point i to point i + 1, and its bow is point i's third number.
"""

import math
import types

#: What a transparent texel counts as, colour-wise: the ground a car stands on, nearer the black frame than a lens.
GROUND = (0.16, 0.16, 0.18)
#: The part's model: its core is the prop eroded by CORE_PX; its surround the ring RING_PX out; each a few colour
#: clusters, since a lens is its colour and its highlights, and a surround is frame, paint and ground.
CORE_PX, RING_PX, CORE_CLUSTERS, RING_CLUSTERS = 2, (3, 7), 4, 6
#: A lamp's frame: a texel is fully dark at this value (its brightest channel) and not at all from this one, so a
#: deep red, whose red channel is high, is never frame. How much the frame's rise counts beside the colours' fall.
DARK_VAL_FULL, DARK_VAL_NONE, FRAME_WEIGHT = 0.15, 0.4, 0.5
#: How far outside an outline a drawn frame is looked for: the art's rims are one or two texels wide.
FRAME_RING_PX = 2
#: Snap: offsets tried along an edge's normal, the cost of reaching for a far boundary, the contrast a sample needs
#: to count, how much of the reach each pass keeps, and when an edge is split or dropped.
OFFSET_STEP_PX, REACH_PENALTY, MIN_CONTRAST = 0.25, 0.15, 0.1
PASSES = (1.0, 0.75, 0.5, 0.35)
SPLIT_RMS_PX, SPLIT_MIN_CHORD_PX, MAX_POINTS, MIN_CHORD_PX = 1.0, 8.0, 10, 1.5
#: A lens is smooth and near convex: every corner is rounded off this far along its edges unless a prop says
#: `"round"`, and an outline that turns more than this in all (a convex one turns 360) is not a lens — a boomerang's
#: two inner bends are within it, a hook or a spiral is not.
ROUND_PX, MAX_TURN_DEG = 2.0, 540
#: A vertex keeps this share of its own move and takes the rest from its neighbours', so an outline moves as a
#: piece and does not zig-zag; and a bow stays within this share of its chord.
OWN_MOVE, MAX_BOW_SHARE = 0.6, 0.3
#: Two edges' lines meet at a vertex only where they cross this steeply (the sine of the angle between them);
#: nearer parallel, the vertex takes the mean of their moves.
MEET_MIN_SIN = 0.35
#: How far an end's mirror axis is searched either side of where it stands, and how far show looks for a better place
#: for one prop's copy; the gain in contrast that is worth saying.
AXIS_SEARCH_PX, COPY_SEARCH_PX, COPY_GAIN = 2.0, 3.0, 0.05
#: Two sides' rims this far apart, as the mean of an edge's two ends, are lopsided art rather than noise: no one
#: symmetric edge sits on both.
LOPSIDED_PX = 1.0
#: An edge's samples: one every this many texels along it, a few at least.
SAMPLE_PX = 1.5
SUPERSAMPLE = 4


# A prop's place among the others, and its outline on each side.

def parent_of(doc, p):
    return next(q for q in doc["props"] if q["name"] == p["part"])


def root_of(doc, p):
    while "part" in p:
        p = parent_of(doc, p)
    return p


def end_of(doc, car, p):
    p = root_of(doc, p)
    return "front" if sum(q[0] for q in p["points"]) / len(p["points"]) > car["rgba"].shape[1] / 2 else "rear"


def copy_at(doc, car, p):
    """Where a prop's mirrored copy goes: (the row it is reflected about, the shift along x after)."""
    p = root_of(doc, p)
    return float(p.get("axisY", doc["axisY"][end_of(doc, car, p)])), float(p.get("mirrorDx", 0.0))


def mirrored(points, at):
    axis, dx = at
    return [[x + dx, 2 * axis - y, b] for x, y, b in points]


def sides_of(doc, car, p):
    """The prop's polygon on each side it is drawn, authored first; None for a part, which has none of its own."""
    if "part" in p:
        return None
    return [p["points"]] + ([mirrored(p["points"], copy_at(doc, car, p))] if p["mirror"] else [])


def rings(doc, car, p):
    """The prop's outline on each side it is drawn, as dense runs of points: authored first."""
    if "part" in p:
        axis = copy_at(doc, car, p)[0]
        return [part_of(r, axis, p.get("side", "outboard"), float(p.get("share", 0.5)))
                for r in rings(doc, car, parent_of(doc, p))]
    return [outline(q, rounding_of(p)) for q in sides_of(doc, car, p)]


def part_of(ring, axis, side, share):
    """The share of an outline's lateral extent nearest (inboard) or farthest from (outboard) the axis."""
    import numpy as np

    lo, hi = ring[:, 1].min(), ring[:, 1].max()
    top = ring[:, 1].mean() < axis
    cut = lo + share * (hi - lo) if top == (side == "outboard") else hi - share * (hi - lo)
    keep_below = top == (side == "outboard")
    out = []
    for a, b in zip(ring, np.roll(ring, -1, 0)):
        a_in, b_in = (a[1] <= cut) == keep_below, (b[1] <= cut) == keep_below
        if a_in:
            out.append(a)
        if a_in != b_in:
            t = (cut - a[1]) / (b[1] - a[1])
            out.append(a + t * (b - a))
    return np.array(out) if len(out) >= 3 else ring[:0]


def rounding_of(p):
    return float(p.get("round", ROUND_PX))


def rounded(points):
    """The points as the props file holds them, so what snap judges is what the file then holds."""
    return [[round(x, 2), round(y, 2), round(b, 2)] for x, y, b in points]


# Geometry.

def winding(points):
    import numpy as np

    p = np.array([q[:2] for q in points])
    area2 = np.sum(p[:, 0] * np.roll(p[:, 1], -1) - np.roll(p[:, 0], -1) * p[:, 1])
    return 1.0 if area2 >= 0 else -1.0


def chord_normal(p0, p1, s):
    import numpy as np

    d = p1 - p0
    return s * np.array([d[1], -d[0]]) / max(math.hypot(*d), 1e-9)


def edge_ends(points, i):
    import numpy as np

    return np.array(points[i][:2], float), np.array(points[(i + 1) % len(points)][:2], float)


def on_edge(p0, p1, bow, s, t):
    """Points at parameters `t` along the edge from p0 to p1 bowed by `bow`, and the shape's outward normal there. A
    parameter is a share of the arc's length; one past 0 or 1 carries on along the same circle or line."""
    import numpy as np

    t = np.asarray(t, np.float64)
    d = p1 - p0
    c = math.hypot(*d)
    n = chord_normal(p0, p1, s)
    h = max(-c / 2, min(c / 2, bow))
    if abs(h) < 1e-3 or c < 1e-6:
        return p0 + t[:, None] * d, np.repeat(n[None], len(t), 0)
    r = (c * c / 4 + h * h) / (2 * abs(h))
    mid = (p0 + p1) / 2
    centre = mid - n * math.copysign(r - abs(h), h)
    a0 = math.atan2(*(p0 - centre)[::-1])
    a1 = math.atan2(*(p1 - centre)[::-1])
    am = math.atan2(*(mid + n * h - centre)[::-1])
    sweep = (a1 - a0 + math.pi) % (2 * math.pi) - math.pi
    to_mid = (am - a0 + math.pi) % (2 * math.pi) - math.pi
    if to_mid * sweep < 0 or abs(to_mid) > abs(sweep):
        sweep -= math.copysign(2 * math.pi, sweep)
    ang = a0 + t * sweep
    radial = np.stack([np.cos(ang), np.sin(ang)], 1)
    return centre + r * radial, radial * math.copysign(1, h)


def arc_length(p0, p1, bow):
    c = math.hypot(*(p1 - p0))
    h = min(abs(bow), c / 2)
    if h < 1e-3:
        return c
    r = (c * c / 4 + h * h) / (2 * h)
    return 2 * r * math.asin(min(1.0, c / (2 * r)))


def outline(points, rounding=0.0, step=0.5):
    """The closed outline as a dense run of distinct points, each corner rounded off `rounding` texels along both its
    edges (less where an edge is short): a curve tangent to both, through the corner's quadratic Bézier."""
    import numpy as np

    s, pts, n = winding(points), np.array([q[:2] for q in points]), len(points)
    runs, lengths = [], []
    for i, q in enumerate(points):
        p0, p1 = pts[i], pts[(i + 1) % n]
        c = math.hypot(*(p1 - p0))
        k = max(2, int(math.ceil(c * (1 + abs(q[2]) / max(c, 1e-6)) / step)))
        run = on_edge(p0, p1, q[2], s, np.arange(k + 1) / k)[0]
        runs.append(run)
        lengths.append(np.concatenate([[0], np.cumsum(np.hypot(*np.diff(run, axis=0).T))]))
    if rounding <= 0:
        return distinct(np.vstack([r[:-1] for r in runs]))
    trim = [min(rounding, 0.4 * lengths[j - 1][-1], 0.4 * lengths[j][-1]) for j in range(n)]

    def at(i, d):
        return np.array([np.interp(d, lengths[i], runs[i][:, 0]), np.interp(d, lengths[i], runs[i][:, 1])])

    out = []
    for i in range(n):
        a, b, corner = at(i - 1, lengths[i - 1][-1] - trim[i]), at(i, trim[i]), pts[i]
        u = np.linspace(0, 1, 7)[:, None]
        out.append((1 - u) ** 2 * a + 2 * u * (1 - u) * corner + u ** 2 * b)
        keep = (lengths[i] > trim[i] + 1e-3) & (lengths[i] < lengths[i][-1] - trim[(i + 1) % n] - 1e-3)
        out.append(runs[i][keep])
    return distinct(np.vstack(out))


def distinct(ring):
    """Without a point that repeats the one before it: a step of no length has no direction to turn by."""
    import numpy as np

    step = np.hypot(*np.diff(np.vstack([ring[-1:], ring]), axis=0).T)
    return ring[step > 1e-3]


def smooth_enough(ring):
    """Why a dense outline is not a lamp's shape, or None: it crosses itself, or it turns more than a lens does."""
    import numpy as np

    d = np.diff(np.vstack([ring, ring[:1]]), axis=0)
    ang = np.arctan2(d[:, 1], d[:, 0])
    turn = np.degrees(np.abs((np.diff(np.concatenate([ang, ang[:1]])) + np.pi) % (2 * np.pi) - np.pi).sum())
    if turn > MAX_TURN_DEG:
        return f"turns {turn:.0f} deg in all, past {MAX_TURN_DEG}"
    a, b = ring, np.roll(ring, -1, 0)

    def side(p, q, r):
        return np.sign((q[..., 0] - p[..., 0]) * (r[..., 1] - p[..., 1]) - (q[..., 1] - p[..., 1]) * (r[..., 0] - p[..., 0]))

    A, B, C, D = a[:, None], b[:, None], a[None], b[None]
    cross = (side(A, B, C) * side(A, B, D) < 0) & (side(C, D, A) * side(C, D, B) < 0)
    idx = np.arange(len(ring))
    near = np.abs(idx[:, None] - idx[None]) % (len(ring) - 1) <= 1
    if (cross & ~near).any():
        return "crosses itself"
    return None


def coverage(ring_list, shape):
    """The share of each texel the outlines cover."""
    import numpy as np
    from PIL import Image, ImageDraw

    h, w = shape
    im = Image.new("L", (w * SUPERSAMPLE, h * SUPERSAMPLE), 0)
    draw = ImageDraw.Draw(im)
    for ring in ring_list:
        if len(ring) >= 3:
            draw.polygon([(x * SUPERSAMPLE, y * SUPERSAMPLE) for x, y in ring], fill=255)
    return np.asarray(im.resize((w, h), Image.BOX), np.float32) / 255


def meet(n_in, o_in, n_out, o_out):
    """The move of a vertex whose incoming edge's line shifts `o_in` along `n_in` and outgoing one `o_out` along
    `n_out`: where the two shifted lines cross, or the mean of the shifts where they are near parallel."""
    import numpy as np

    if abs(n_in[0] * n_out[1] - n_in[1] * n_out[0]) > MEET_MIN_SIN:
        return np.linalg.solve(np.stack([n_in, n_out]), [o_in, o_out])
    return (n_in * o_in + n_out * o_out) / 2


def normals_of(points):
    import numpy as np

    s, pts, n = winding(points), np.array([q[:2] for q in points]), len(points)
    return [chord_normal(pts[i], pts[(i + 1) % n], s) for i in range(n)]


def vertex_normal(points, j):
    """The outward direction at vertex j: halfway between its two edges' normals."""
    import numpy as np

    nrm = normals_of(points)
    v = nrm[j - 1] + nrm[j]
    return v / max(float(np.hypot(*v)), 1e-9)


def offset_edges(points, offsets):
    """The polygon with edge i moved `offsets[i]` texels along its outward normal, each vertex where its two edges'
    lines then meet; bows kept."""
    nrm, n = normals_of(points), len(points)
    out = []
    for j in range(n):
        d = meet(nrm[j - 1], offsets[j - 1], nrm[j], offsets[j])
        out.append([points[j][0] + float(d[0]), points[j][1] + float(d[1]), points[j][2]])
    return out


def split_edge(points, i):
    """Edge i halved at its arc's middle, each half bowed so the two lie on the arc the one did."""
    p0, p1 = edge_ends(points, i)
    c, s = math.hypot(*(p1 - p0)), winding(points)
    h = max(-c / 2, min(c / 2, points[i][2]))
    mid = on_edge(p0, p1, h, s, [0.5])[0][0]
    if abs(h) < 1e-3:
        half = 0.0
    else:
        r = (c * c / 4 + h * h) / (2 * abs(h))
        c2 = math.hypot(c / 2, h)
        half = math.copysign(r - math.sqrt(max(r * r - c2 * c2 / 4, 0.0)), h)
    out = [list(q) for q in points]
    out[i][2] = half
    out.insert(i + 1, [float(mid[0]), float(mid[1]), half])
    return out


def drop_vertex(points, j):
    """Vertex j taken out, the edge that then joins its neighbours bowed to pass through where it stood."""
    import numpy as np

    a, b, c = (np.array(points[k % len(points)][:2], float) for k in (j - 1, j, j + 1))
    chord = math.hypot(*(c - a))
    n = chord_normal(a, c, winding(points))
    off = float((b - (a + c) / 2) @ n)
    cross = (b - a)[0] * (c - a)[1] - (b - a)[1] * (c - a)[0]
    if abs(cross) < 1e-6 or abs(off) < 1e-3:
        bow = 0.0
    else:
        # The circle through all three; its sagitta over the new chord, on b's side.
        r = math.hypot(*(b - a)) * math.hypot(*(c - b)) * chord / (2 * abs(cross))
        bow = math.copysign(min(chord / 2, r - math.sqrt(max(r * r - chord * chord / 4, 0.0))), off)
    out = [list(q) for k, q in enumerate(points) if k != j]
    out[j - 1 if j else len(out) - 1][2] = bow
    return out


# The part a prop was put on, and pulling the prop onto its rim.

def pull_of(car, p):
    """What a prop is fitted by: the call's `--pull`, else the prop's own `"pull"`, else the part it was put on."""
    return car.get("pull") or p.get("pull", "part")


def likeness(car, ring_list, pull="part"):
    """How like the outlines' core rather than their surround each texel's colour is, 0 to 1: the distance to the
    surround's nearest colour cluster over the sum of that and the core's, squared, so a texel between the two leans
    to the nearer. Pulled to the frame, how far each texel is from frame-dark instead, whatever the placement: a pale
    lens on pale paint has no colour of its own to learn, but a frame."""
    import numpy as np

    if pull == "frame":
        return types.SimpleNamespace(core=1 - car["dark"], dark=car["dark"])
    colour = car["colour"]
    inside = coverage(ring_list, colour.shape[:2]) > 0.5
    core = ~window(~inside, CORE_PX, np.max)
    if core.sum() < 6:
        core = inside
    ring = window(inside, RING_PX[1], np.max) & ~window(inside, RING_PX[0], np.max)
    d_in = nearest(colour, clusters(colour[core], CORE_CLUSTERS)) ** 2
    d_out = nearest(colour, clusters(colour[ring], RING_CLUSTERS)) ** 2
    return types.SimpleNamespace(core=d_out / (d_in + d_out + 1e-6), dark=car["dark"])


def clusters(samples, k):
    """k colour centres of the samples by Lloyd's iterations, seeded by lightness so a run is the same every time."""
    import numpy as np

    if len(samples) <= k:
        return samples if len(samples) else np.zeros((1, 3), np.float32)
    order = np.argsort(samples.sum(1))
    centres = samples[order[np.linspace(0, len(samples) - 1, k).astype(int)]]
    for _ in range(10):
        label = np.argmin(((samples[:, None] - centres[None]) ** 2).sum(-1), 1)
        centres = np.stack([samples[label == i].mean(0) if (label == i).any() else centres[i] for i in range(k)])
    return centres


def nearest(colour, centres):
    import numpy as np

    return np.sqrt(((colour[..., None, :] - centres) ** 2).sum(-1)).min(-1)


def window(a, r, reduce):
    import numpy as np

    p = np.pad(a, r, mode="edge")
    h, w = a.shape
    return reduce(np.stack([p[y:y + h, x:x + w] for y in range(2 * r + 1) for x in range(2 * r + 1)]), 0)


def blur(a, sigma):
    import numpy as np

    r = max(1, int(3 * sigma))
    k = np.exp(-np.arange(-r, r + 1) ** 2 / (2 * sigma ** 2))
    k /= k.sum()
    p = np.pad(a, r, mode="edge")
    p = np.apply_along_axis(lambda v: np.convolve(v, k, "valid"), 0, p)
    return np.apply_along_axis(lambda v: np.convolve(v, k, "valid"), 1, p)


def sample(field, xy):
    """Bilinear, texel centres at +0.5."""
    import numpy as np

    h, w = field.shape
    x = np.clip(xy[..., 0] - 0.5, 0, w - 1.001)
    y = np.clip(xy[..., 1] - 0.5, 0, h - 1.001)
    x0, y0 = x.astype(int), y.astype(int)
    fx, fy = x - x0, y - y0
    return (field[y0, x0] * (1 - fx) * (1 - fy) + field[y0, x0 + 1] * fx * (1 - fy)
            + field[y0 + 1, x0] * (1 - fx) * fy + field[y0 + 1, x0 + 1] * fx * fy)


def contrast(like, pts, nrm, at=None):
    """How much more like the core the texels just inside each point are than those just outside; with `at`, the mean
    of that and the same at the mirrored copy's point."""
    if at is None:
        return across(like, pts, nrm)
    axis, dx = at
    flip, nflip = pts.copy(), nrm.copy()
    flip[..., 0] += dx
    flip[..., 1] = 2 * axis - flip[..., 1]
    nflip[..., 1] = -nflip[..., 1]
    return (across(like, pts, nrm) + across(like, flip, nflip)) / 2


def across(like, xy, nrm):
    """The likeness falling from inside to outside, and the dark of a lamp's frame rising across the same step: the
    frame is what tells a red lens from red paint, which the colours alone do not."""
    def step(field):
        inner = (sample(field, xy - 0.5 * nrm) + sample(field, xy - 1.5 * nrm)) / 2
        outer = (sample(field, xy + 0.5 * nrm) + sample(field, xy + 1.5 * nrm)) / 2
        return inner - outer

    return (step(like.core) - FRAME_WEIGHT * step(like.dark)) / (1 + FRAME_WEIGHT)


def rim_search(p0, p1, bow, s, like, at, reach):
    """Per sample along an edge: its parameter, the offset along the outward normal within `reach` where the rim is
    sharpest, and that rim's contrast."""
    import numpy as np

    k = int(np.clip(round(math.hypot(*(p1 - p0)) / SAMPLE_PX), 5, 40))
    t = np.linspace(0.08, 0.92, k)
    pts, nrm = on_edge(p0, p1, bow, s, t)
    offs = np.arange(-reach, reach + 1e-6, OFFSET_STEP_PX)
    cand = pts[:, None, :] + offs[None, :, None] * nrm[:, None, :]
    score = contrast(like, cand, np.repeat(nrm[:, None, :], len(offs), 1), at)
    best = np.argmax(score - REACH_PENALTY * np.abs(offs) / reach, 1)
    return t, offs[best], score[np.arange(k), best]


def edge_fit(p0, p1, bow, s, like, at, reach):
    """The offsets along the normal that put an edge's samples on the part's rim, fitted as shift + tilt + bow
    change: (offset at p0, offset at p1, bow change, weighted rms, confidence). With `at`, where each side finds its own
    rim over LOPSIDED_PX from the other's, halfway between them: the art is lopsided there, the summed contrast peaks on
    one side's rim and leaves the other's whole gap, and halfway leaves each side half of it."""
    import numpy as np

    if at is not None:
        own = edge_fit(p0, p1, bow, s, like, None, reach)
        c0, c1 = (np.array([x + at[1], 2 * at[0] - y]) for x, y in (p0, p1))
        copy = edge_fit(c0, c1, bow, -s, like, None, reach)
        if own[4] and copy[4] and abs(own[0] + own[1] - copy[0] - copy[1]) / 2 > LOPSIDED_PX:
            return ((own[0] + copy[0]) / 2, (own[1] + copy[1]) / 2, (own[2] + copy[2]) / 2, max(own[3], copy[3]),
                    min(own[4], copy[4]))
    t, o, wgt = rim_search(p0, p1, bow, s, like, at, reach)
    wgt = np.where(wgt >= MIN_CONTRAST, wgt, 0)
    if (wgt > 0).sum() < 3:
        return 0.0, 0.0, 0.0, 0.0, 0.0
    basis = np.stack([np.ones_like(t), t - 0.5, 4 * t * (1 - t)], 1)
    sw = np.sqrt(wgt)[:, None]
    coef = np.linalg.lstsq(basis * sw, o * sw[:, 0], rcond=None)[0]
    rms = math.sqrt(float((wgt * (o - basis @ coef) ** 2).sum() / wgt.sum()))
    a, b, bend = coef
    # The ends are extrapolated past the samples, and a tilt through a few of them can throw an end far past where
    # any rim was looked for; nothing is found beyond the reach.
    o0, o1, bend = (float(np.clip(v, -reach, reach)) for v in (a - b / 2, a + b / 2, bend))
    return o0, o1, bend, rms, float(wgt.mean())


def snap(doc, car, p, reach0, only=None):
    """The part is learnt afresh each pass from where the prop now is; a pass is kept only if the outline then sits
    on a sharper rim than before, judged by the same model, and is still a lens's shape. With `only`, every other edge
    is held for this snap. What it did, as a line."""
    import numpy as np

    if "part" in p:
        return f"  {p['name']}: a part of {p['part']}, follows it"
    if p.get("lock"):
        return f"  {p['name']}: locked"
    at = copy_at(doc, car, p) if p["mirror"] else None
    rounding, kept, why = rounding_of(p), 0, set()
    # Each edge carries a tag through the splits, so a hold — the file's, or every edge but `only` — follows its edge.
    tags = list(range(len(p["points"])))
    frozen = set(p.get("hold", [])) | (set(tags) - set(only) if only is not None else set())
    # Moves follow the part as each pass relearns it; every pass, and the line printed, is judged by the part as the
    # placement first showed it, so the numbers compare.
    pull = pull_of(car, p)
    judge = likeness(car, rings(doc, car, p), pull)
    start = fit_of(p["points"], judge, at, frozen)
    for share in PASSES:
        reach = reach0 * share
        like = likeness(car, rings(doc, car, p), pull)
        held = {i for i, tag in enumerate(tags) if tag in frozen}
        was = fit_of(p["points"], judge, at, held)
        pts = np.array([q[:2] for q in p["points"]])
        bows = np.array([q[2] for q in p["points"]])
        n, s = len(pts), winding(p["points"])
        fits = [(0.0, 0.0, 0.0, 0.0, 1.0) if i in held else
                edge_fit(pts[i], pts[(i + 1) % n], bows[i], s, like, at, reach) for i in range(n)]
        if share == PASSES[0]:
            blind = [i for i in range(n) if not fits[i][4]]
        normals = normals_of(p["points"])
        moves = np.zeros_like(pts)
        for j in range(n):
            (_, o_in, _, _, c_in), (o_out, _, _, _, c_out) = fits[j - 1], fits[j]
            n_in, n_out = normals[j - 1], normals[j]
            if c_in and c_out:
                d = meet(n_in, o_in, n_out, o_out)
            else:
                d = n_in * o_in if c_in else n_out * o_out if c_out else np.zeros(2)
            moves[j] = d * min(1.0, reach / max(math.hypot(*d), 1e-9))
        moves = OWN_MOVE * moves + (1 - OWN_MOVE) / 2 * (np.roll(moves, 1, 0) + np.roll(moves, -1, 0))
        # A held edge stays on its line: its ends slide along it and no further, and an end held both ways stays put.
        for j in range(n):
            lines = [normals[i] for i in (j - 1, j) if i % n in held]
            if len(lines) == 2:
                moves[j] = 0
            elif lines:
                moves[j] -= (moves[j] @ lines[0]) * lines[0]
        moved = pts + moves
        points, now_tags = [], []
        for i in range(n):
            chord = math.hypot(*(moved[(i + 1) % n] - moved[i]))
            if chord < MIN_CHORD_PX:
                continue
            bow = float(np.clip(bows[i] + fits[i][2], -MAX_BOW_SHARE * chord, MAX_BOW_SHARE * chord))
            bow = bows[i] if i in held else max(bow, 0.0) if bows[i] >= 0 else bow
            points.append([float(moved[i][0]), float(moved[i][1]), bow])
            now_tags.append(tags[i])
            if (i not in held and fits[i][3] > SPLIT_RMS_PX and chord > SPLIT_MIN_CHORD_PX and share != PASSES[-1]
                    and n + len(points) - i - 1 < MAX_POINTS):
                mid = on_edge(moved[i], moved[(i + 1) % n], bow, s, [0.5])[0][0]
                points[-1][2] = bow / 4
                points.append([float(mid[0]), float(mid[1]), bow / 4])
                now_tags.append(tags[i])
        points = rounded(points)
        if len(points) < 3:
            why.add("an edge shrank to nothing")
            continue
        if wrong := smooth_enough(outline(points, rounding)):
            why.add(wrong)
            continue
        if fit_of(points, judge, at, {i for i, tag in enumerate(now_tags) if tag in frozen}) <= was:
            why.add("the fit fell")
            continue
        p["points"], tags, kept = points, now_tags, kept + 1
    if "hold" in p:
        p["hold"] = [i for i, tag in enumerate(tags) if tag in set(p["hold"])]
    end = fit_of(p["points"], judge, at, {i for i, tag in enumerate(tags) if tag in frozen})
    return (f"  {p['name']}: {kept} of {len(PASSES)} passes kept, edge contrast"
            f"{' (both sides)' if at else ''}{' over ' + edge_list(only) if only is not None else ''}"
            f" {start:.2f} -> {end:.2f}" + (f"; dropped where {', '.join(sorted(why))}" if why else "")
            + (f"; {edge_list(blind)} found no rim, and moved only with the edges beside {'it' if len(blind) == 1 else 'them'}"
               " (`--edges` to leave them be)" if blind else ""))


def edge_list(edges):
    return " ".join(f"e{i}" for i in sorted(edges))


def rim_gap(points, like, reach):
    """How far one side's outline sits off the rim it was put on: the median distance, in texels, from each sample to
    where the rim search finds the rim, over the samples whose rim is clear enough for snap to count; None if none is."""
    import numpy as np

    s, gaps = winding(points), []
    for i in range(len(points)):
        p0, p1 = edge_ends(points, i)
        _, offs, score = rim_search(p0, p1, points[i][2], s, like, None, reach)
        gaps.append(np.abs(offs[score >= MIN_CONTRAST]))
    gaps = np.concatenate(gaps)
    return float(np.median(gaps)) if len(gaps) else None


def framed(car, ring):
    """The share of the texels just outside one side's outline that are a lamp's frame: high where the outline lies on
    a drawn rim, low where it falls short of one, over it, or where the art draws none."""
    import numpy as np

    inside = coverage([ring], car["dark"].shape) > 0.5
    rim = window(inside, FRAME_RING_PX, np.max) & ~inside
    return float((car["dark"][rim] > 0.5).mean()) if rim.any() else 0.0


def edge_contrasts(points, like, at=None):
    """The mean rim contrast under each edge of an outline."""
    import numpy as np

    s, pts = winding(points), np.array([q[:2] for q in points])
    per = []
    for i, q in enumerate(points):
        xy, nrm = on_edge(pts[i], pts[(i + 1) % len(pts)], q[2], s, np.linspace(0.08, 0.92, 9))
        per.append(float(contrast(like, xy, nrm, at).mean()))
    return per


def fit_of(points, like, at, held=frozenset()):
    per = [c for i, c in enumerate(edge_contrasts(points, like, at)) if i not in held]
    return sum(per) / len(per) if per else 0.0


def best_axis(doc, car, end):
    """The axis, within AXIS_SEARCH_PX, that lays the end's mirrored props' copies on the sharpest rim of the part each
    was put on. Props with an axis of their own and derived props take no part."""
    import numpy as np

    props = [p for p in doc["props"] if "points" in p and p["mirror"] and "axisY" not in p and end_of(doc, car, p) == end]
    if not props:
        return doc["axisY"][end]
    likes = [likeness(car, rings(doc, car, p)[:1], pull_of(car, p)) for p in props]
    here = doc["axisY"][end]
    tried = np.arange(here - AXIS_SEARCH_PX, here + AXIS_SEARCH_PX + 1e-6, 0.25)
    score = [np.mean([fit_of(mirrored(p["points"], (a, p.get("mirrorDx", 0.0))), like, None, set(p.get("hold", [])))
                      for p, like in zip(props, likes)]) for a in tried]
    return float(tried[int(np.argmax(score))])


def copy_moves(p, like, at):
    """Where the mirrored copy would sit on a sharper rim: (axisY, mirrorDx, contrast there, contrast now), or None.
    The part is learnt off the authored side alone, so the copy's own placement does not teach the answer."""
    import numpy as np

    held = set(p.get("hold", []))
    copy = mirrored(p["points"], at)
    here = fit_of(copy, like, None, held)
    steps = np.arange(-COPY_SEARCH_PX, COPY_SEARCH_PX + 1e-6, 0.5)
    score, dx, dy = max((fit_of([[x + dx, y + dy, b] for x, y, b in copy], like, None, held), dx, dy)
                        for dx in steps for dy in steps)
    # A best at the bound of the search is a slope, not a place: some other rim pulling.
    if score - here < COPY_GAIN or max(abs(dx), abs(dy)) >= COPY_SEARCH_PX:
        return None
    return at[0] + dy / 2, at[1] + dx, score, here


def better_copy(p, like, at):
    moves = copy_moves(p, like, at)
    if moves is None:
        return None
    axis, dx, score, here = moves
    return (f'copy sits better at "axisY": {axis:.2f}, "mirrorDx": {dx:.2f} ({here:.2f} -> {score:.2f});'
            f" `axis --prop={p['name']}` sets them")


def own_copy(doc, car, p):
    """The prop's copy put where it sits on the sharpest rim, as its own axisY and mirrorDx."""
    if "points" not in p or not p["mirror"]:
        return f"  {p['name']}: no copy of its own to place"
    moves = copy_moves(p, likeness(car, rings(doc, car, p)[:1], pull_of(car, p)), copy_at(doc, car, p))
    if moves is None:
        return f"  {p['name']}: the copy is already where it fits best"
    p["axisY"], p["mirrorDx"] = round(moves[0], 2), round(moves[1], 2)
    return f"  {p['name']}: copy at axisY {p['axisY']:.2f}, mirrorDx {p['mirrorDx']:.2f} ({moves[3]:.2f} -> {moves[2]:.2f})"
