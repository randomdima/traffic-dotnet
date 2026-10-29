"""
A walker's look: asked as a block of drawings on one frame, cut into one variant, and installed as the 8×8 sheet the
engine reads (`PersonCatalog`: eight facing rows anticlockwise from up, eight walk columns, column 0 the stance).

A frame is always GRID_COLS × FACINGS drawings: each row one facing, each look a run of FRAMES columns of its walk —
two looks of four frames, or one of eight. Only the five facings from the back round the walker's left to the front are
drawn; the other three are those mirrored (ENGINE_ROWS), a walker being the same left and right. The frame is asked
with a pose guide of the same grid (`pose_guide`), so which leg is in front is drawn for it. The kept variant is the
look's block at the frame's own resolution, every drawing aligned (`cut_frame`); `install` mirrors and spreads it into
the engine's sheet at the art grid.
"""

from __future__ import annotations

import json
from pathlib import Path

#: Every walker frame's grid. Its columns are LOOK_FRAMES-long runs of one look each.
GRID_COLS, FACINGS = 8, 5
#: The engine's sheet (`PersonCatalog.FacingRows`, `WalkColumns`).
SHEET_ROWS, SHEET_COLS = 8, 8
#: Per engine row, anticlockwise from up: the drawn facing it shows, and whether mirrored. Drawn: 0 back, 1 back and
#: left side, 2 left side, 3 front and left side, 4 front.
ENGINE_ROWS = ((0, False), (1, False), (2, False), (3, False), (4, False), (3, True), (2, True), (1, True))
#: Per drawn walk length, which drawing each engine column shows. Column 0 is what a walker standing still shows
#: (`PersonSprites`), so drawing 0 is a stance; four drawings each hold two columns.
COLUMNS = {4: (0, 1, 1, 2, 2, 3, 3, 0), 8: tuple(range(8))}
#: How much of a figure, from the top, its horizontal anchor is read off: head and shoulders, which a stride does not
#: swing, so a walk does not jitter sideways frame to frame.
ANCHOR_SHARE = 0.35
#: Clear frame pixels kept round the widest and tallest drawing of a look, so no neighbour bleeds in when the sheet is
#: filtered.
PAD_PX = 3
#: A drawn facing more than this off the look's median height is flagged: the generator drew one row at another scale.
HEIGHT_FLAG = 0.06
#: The share of a figure, from the bottom, its stride is compared over: the legs.
LEGS_SHARE = 0.45
#: An eight-frame walk's second half is its first with the legs swapped, so a stride column and the one four after it
#: are opposite strides. Below this share of how far the stride is from the stance, the two are one stride drawn twice:
#: the generator moved one leg (`walkers_solo_a-v1`).
SWAP_FLAG = 0.6
#: The preview's backdrop and scale.
BACKDROP = (58, 60, 64, 255)
PREVIEW_HEIGHT_PX = 160
PREVIEW_FRAME_MS = 110


#: The pose guide (`pose_guide`): the camera's pitch below level, the walking directions of the five drawn facings in
#: the ground plane (x east, z north, away from the camera), and the body it is drawn for, metres.
GUIDE_PITCH_DEG = 40
GUIDE_HEADINGS = ((0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1))
GUIDE_BODY = {"thigh": 0.45, "shin": 0.45, "foot": 0.2, "hip": 0.1, "torso": 0.55, "shoulder": 0.2, "upperArm": 0.3,
              "forearm": 0.27, "neck": 0.08, "head": 0.11}
#: One leg's walk, an eighth of its cycle a row from its heel striking: the thigh forward of upright, the knee's bend
#: and the foot's toe raised above level, degrees — contact, down, mid-stance, heel rising, on its toes, lifting
#: behind, passing knee high, reaching. Keyed rather than a sine, so every in-between is a pose of its own; a relaxed
#: step, the owner finding a 26° thigh too long (2026-10-27).
GUIDE_GAIT_DEG = ((15, 2, 15), (11, 16, 0), (2, 6, 0), (-7, 4, -8), (-13, 20, -30), (-6, 40, -20), (6, 42, 0),
                  (14, 16, 8))
#: An arm swings against the leg on its side by this share of the thigh's angle; the elbow bends this much, more as
#: the arm comes forward.
GUIDE_ARM_SHARE, GUIDE_ELBOW_DEG = 1.0, (10, 20)
GUIDE_LEFT, GUIDE_RIGHT, GUIDE_BODY_INK, GUIDE_NOSE = (40, 90, 230), (225, 45, 40), (120, 120, 120), (20, 20, 20)
GUIDE_BACKDROP = (236, 236, 236)


def is_walker(d) -> bool:
    """A walker's variant file: a walk sheet and a height, nothing with a footprint."""
    return isinstance(d, dict) and "sheet" in d and "heightM" in d


def frames_per_look(looks: int) -> int:
    if looks not in (1, 2):
        raise ValueError(f"a walker frame draws one look or two, not {looks}")
    return GRID_COLS // looks


def cut_frame(pixels, solid, ids, heights_m, fail):
    """Each look's block off a keyed frame (RGBA uint8, its solid mask), the figures aligned: every row scaled to the
    look's median height, feet on one line, head and shoulders on one vertical. Returns per id (block image, info) and
    the boxes found, row by row."""
    import numpy as np
    from PIL import Image

    import _outline

    n_frames = frames_per_look(len(ids))
    cells = [f"{r}:{c}" for r in range(FACINGS) for c in range(GRID_COLS)]
    boxes = _outline.drawings(solid, cells, fail)
    out = {}
    for look, i in enumerate(ids):
        figures = []
        for r in range(FACINGS):
            row = []
            for f in range(n_frames):
                x0, y0, x1, y1 = boxes[f"{r}:{look * n_frames + f}"]
                painted = np.flatnonzero(solid[y0:y1, x0:x1].any(1))
                y0, y1 = y0 + int(painted[0]), y0 + int(painted[-1]) + 1
                crop = pixels[y0:y1, x0:x1].copy()
                crop[~solid[y0:y1, x0:x1]] = 0
                row.append(crop)
            figures.append(row)
        tallest = [max(c.shape[0] for c in row) for row in figures]
        target = int(np.median(tallest))
        flags = [f"facing {r} drawn {t / target - 1:+.0%} off the look's height"
                 for r, t in enumerate(tallest) if abs(t / target - 1) > HEIGHT_FLAG]
        placed = []
        for r, row in enumerate(figures):
            scale = target / tallest[r]
            line = []
            for crop in row:
                img = Image.fromarray(crop, "RGBA")
                if abs(scale - 1) > 0.005:
                    img = img.resize((max(1, round(img.width * scale)), max(1, round(img.height * scale))), Image.LANCZOS)
                line.append((img, anchor_x(np.asarray(img)[..., 3] > 128)))
            placed.append(line)
        half = max(max(ax, img.width - ax) for line in placed for img, ax in line)
        cw, ch = 2 * (half + PAD_PX), target + 2 * PAD_PX
        block = Image.new("RGBA", (cw * n_frames, ch * FACINGS), (0, 0, 0, 0))
        for r, line in enumerate(placed):
            for f, (img, ax) in enumerate(line):
                # Composited, not pasted through its own alpha, which would square every soft edge's alpha.
                block.alpha_composite(img.convert("RGBA"), (f * cw + cw // 2 - ax, r * ch + ch - PAD_PX - img.height))
        info = {"cell": look + 1, "pxPerM": round(ch / heights_m[i], 3), "frames": n_frames, "facings": FACINGS,
                "cellPx": [cw, ch]}
        swaps = legs_swapped(block, info)
        flags += [f"facing {r} strides one leg only ({s:.2f} of the stance's change)" for r, s in enumerate(swaps)
                  if s < SWAP_FLAG]
        out[i] = (block, {**info, "legSwap": [round(s, 2) for s in swaps], "flags": flags})
    return out, boxes


def legs_swapped(block, info) -> list[float]:
    """Per drawn facing, how far its full strides (columns 2 and 6 of eight) differ from each other in the legs'
    silhouette, as a share of how far the first differs from the stance: near 1 or above when the legs swap, near 0
    when one stride is drawn twice. A four-frame walk's strides are columns 1 and 3."""
    import numpy as np

    stance, first, second = (0, 2, 6) if info["frames"] == 8 else (0, 1, 3)
    shares = []
    for row in cells_of_block(block, info):
        legs = [np.asarray(c)[round(c.height * (1 - LEGS_SHARE)):, :, 3] > 128 for c in row]
        apart = lambda a, b: 1 - (a & b).sum() / max(1, (a | b).sum())
        shares.append(apart(legs[first], legs[second]) / max(1e-6, apart(legs[stance], legs[first])))
    return shares


def anchor_x(solid) -> int:
    """The figure's vertical: the middle of its head and shoulders' paint."""
    import numpy as np

    rows = np.flatnonzero(solid.any(1))
    top = solid[rows[0]:rows[0] + max(1, round((rows[-1] - rows[0] + 1) * ANCHOR_SHARE))]
    xs = np.nonzero(top)[1]
    return int(round(xs.mean())) if len(xs) else solid.shape[1] // 2


def cells_of_block(block, info):
    """The block's drawings as [facing][frame] images."""
    cw, ch = info["cellPx"]
    return [[block.crop((f * cw, r * ch, (f + 1) * cw, (r + 1) * ch)) for f in range(info["frames"])]
            for r in range(info["facings"])]


def engine_cells(block, info):
    """The engine's 8×8 cells, [row][column], at the block's resolution: mirrored and spread."""
    from PIL import ImageOps

    drawn = cells_of_block(block, info)
    spread = COLUMNS[info["frames"]]
    return [[ImageOps.mirror(drawn[facing][spread[c]]) if mirrored else drawn[facing][spread[c]]
             for c in range(SHEET_COLS)] for facing, mirrored in ENGINE_ROWS]


def sheet(block, info, height_px: int, shrink):
    """The engine's sheet, each cell `height_px` tall and shrunk on its own (`shrink(image, size)`), so no cell is
    filtered into its neighbour."""
    from PIL import Image

    cw, ch = info["cellPx"]
    w = max(1, round(cw * height_px / ch))
    out = Image.new("RGBA", (w * SHEET_COLS, height_px * SHEET_ROWS), (0, 0, 0, 0))
    for r, row in enumerate(engine_cells(block, info)):
        for c, cell in enumerate(row):
            out.paste(shrink(cell, (w, height_px)), (c * w, r * height_px))
    return out


def preview(block, info, out: Path, label: str = ""):
    """An animated GIF of a cut look walking every engine facing — what the game will show of it."""
    return walked(engine_cells(block, info), *info["cellPx"], out, label)


FACING_NAMES = ("back", "back-left", "left", "front-left", "front", "front-right", "right", "back-right")


def preview_each(block, info, out: Path, stem: str):
    """One animated GIF per engine facing, `<stem>_walk_<row>_<name>.gif`: a facing's loop looked at alone."""
    from PIL import Image

    cw, ch = info["cellPx"]
    scale = 2 * PREVIEW_HEIGHT_PX / ch
    for r, row in enumerate(engine_cells(block, info)):
        frames = []
        for cell in row:
            board = Image.new("RGBA", (round(cw * scale), round(ch * scale)), BACKDROP)
            board.alpha_composite(cell.resize(board.size, Image.LANCZOS))
            frames.append(board.convert("RGB"))
        frames[0].save(out / f"{stem}_walk_{r}_{FACING_NAMES[r]}.gif", save_all=True, append_images=frames[1:],
                       duration=PREVIEW_FRAME_MS, loop=0)


def preview_installed(walk, out: Path, label: str = ""):
    """The same of an installed sheet, its cells as the engine samples them."""
    cw, ch = walk.width // SHEET_COLS, walk.height // SHEET_ROWS
    cells = [[walk.crop((c * cw, r * ch, (c + 1) * cw, (r + 1) * ch)) for c in range(SHEET_COLS)]
             for r in range(SHEET_ROWS)]
    return walked(cells, cw, ch, out, label)


def walked(cells, cw, ch, out: Path, label: str):
    """The engine's 8×8 cells as a GIF: each facing where the compass has it (up at the top), stepped through the eight
    columns."""
    from PIL import Image, ImageDraw

    scale = PREVIEW_HEIGHT_PX / ch
    w, h = max(1, round(cw * scale)), PREVIEW_HEIGHT_PX
    # Compass places, 3×3 with the middle the label: row index -> (col, row) on the board.
    places = {0: (1, 0), 1: (0, 0), 2: (0, 1), 3: (0, 2), 4: (1, 2), 5: (2, 2), 6: (2, 1), 7: (2, 0)}
    frames = []
    for c in range(SHEET_COLS):
        board = Image.new("RGBA", (3 * w + 4 * 8, 3 * h + 4 * 8), BACKDROP)
        for r, (bx, by) in places.items():
            cell = cells[r][c].resize((w, h), Image.LANCZOS)
            board.alpha_composite(cell, (8 + bx * (w + 8), 8 + by * (h + 8)))
        ImageDraw.Draw(board).text((8 + w + 8 + 4, 8 + h + 8 + h // 2 - 6), label or "", fill=(230, 230, 230, 255))
        frames.append(board.convert("RGB"))
    out.parent.mkdir(parents=True, exist_ok=True)
    frames[0].save(out, save_all=True, append_images=frames[1:], duration=PREVIEW_FRAME_MS, loop=0)
    return out


def pose_guide(frames: int, size_px: tuple[int, int], out: Path):
    """The pose guide a walker frame is asked with beside its style picture (the owner, 2026-10-27): the frame's grid of
    stick figures, each facing a row and each walk frame a column, the left limbs GUIDE_LEFT and the right GUIDE_RIGHT,
    the nose a dot — so which leg is in front, and that it changes, is drawn and not described. One walk cycle from a
    stance: the left leg in front a quarter of the way through, the right three quarters (`COLUMNS`' column 0 standing).
    Seen orthographically from in front, GUIDE_PITCH_DEG below level."""
    cw, ch = size_px[0] / GRID_COLS, size_px[1] / FACINGS
    places = [(heading, col * SHEET_COLS // frames, ((look * frames + col + 0.5) * cw, (row + 1) * ch - ch * 0.08))
              for row, heading in enumerate(GUIDE_HEADINGS)
              for look in range(GRID_COLS // frames) for col in range(frames)]
    return stick_figures(places, ch * 0.82, size_px, out)


#: Every facing the engine draws, its rows' order (`PersonCatalog.FacingRow`: anticlockwise from up), as walking
#: directions in the ground plane — x east, z north, away from the camera.
ENGINE_HEADINGS = ((0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1), (1, 0), (1, 1))


def stick_figures(places, figure_px: float, size_px: tuple[int, int], out: Path):
    """Stick figures on GUIDE_BACKDROP, one a place: (walking direction, engine column, (x, y) of its feet's middle),
    each `figure_px` tall at its stance."""
    import math

    from PIL import Image, ImageDraw

    b = GUIDE_BODY
    pitch = math.radians(GUIDE_PITCH_DEG)
    leg = b["thigh"] + b["shin"]
    height_m = leg + b["torso"] + b["neck"] + 2 * b["head"]
    per_m = figure_px / (height_m * math.cos(pitch) + 0.3 * math.sin(pitch))
    img = Image.new("RGB", size_px, GUIDE_BACKDROP)
    draw = ImageDraw.Draw(img)
    width = max(3, round(0.07 * per_m))

    def limb_chain(root, forward, swing, bend, lengths, bend_sign):
        """A two-segment limb hanging from `root`, swung by `swing` forward of down and bent by `bend` at its middle."""
        points, angle, at = [root], swing, root
        for n, length in enumerate(lengths):
            if n == 1:
                angle += bend_sign * bend
            at = tuple(at[k] + length * (forward[k] * math.sin(angle) - (k == 1) * math.cos(angle)) for k in range(3))
            points.append(at)
        return points

    for (hx, hz), column, (ox, oy) in places:
        norm = math.hypot(hx, hz)
        f = (hx / norm, 0.0, hz / norm)
        right = (f[2], 0.0, -f[0])
        # Each leg's eighth of its own cycle at this engine column: the left heel strikes at column 2 (a full stride,
        # left in front), the right at column 6.
        standing = column == 0
        strokes = []
        legs, thighs = {}, {}
        for side, sign in (("left", 1), ("right", -1)):
            gait = (column - 2 + (0 if sign > 0 else 4)) % SHEET_COLS
            thigh, knee, ankle_up = (0.0, 0.0, 0.0) if standing else \
                (math.radians(v) for v in GUIDE_GAIT_DEG[gait])
            thighs[side] = thigh
            hip = tuple(-sign * b["hip"] * right[k] for k in range(3))
            hip = (hip[0], leg, hip[2])
            knee_at, ankle = limb_chain(hip, f, thigh, knee, (b["thigh"], b["shin"]), -1)[1:]
            toe = tuple(ankle[k] + b["foot"] * (f[k] * math.cos(ankle_up) + (k == 1) * math.sin(ankle_up))
                        for k in range(3))
            legs[side] = (hip, knee_at, ankle, toe)
        drop = min(min(p[1] for p in pts) for pts in legs.values())
        lift = lambda p: (p[0], p[1] - drop, p[2])
        pelvis_y = leg - drop
        neck = (0.0, pelvis_y + b["torso"], 0.0)
        head = (0.0, neck[1] + b["neck"] + b["head"], 0.0)
        for side, sign in (("left", 1), ("right", -1)):
            ink = GUIDE_LEFT if side == "left" else GUIDE_RIGHT
            hip, knee_at, ankle, toe = (lift(p) for p in legs[side])
            strokes += [(ink, hip, knee_at), (ink, knee_at, ankle), (ink, ankle, toe)]
            arm = -GUIDE_ARM_SHARE * thighs[side]
            forward = max(0.0, arm) / math.radians(GUIDE_GAIT_DEG[0][0])
            elbow_bend = 0.0 if standing else \
                math.radians(GUIDE_ELBOW_DEG[0] + (GUIDE_ELBOW_DEG[1] - GUIDE_ELBOW_DEG[0]) * forward)
            shoulder = tuple(-sign * b["shoulder"] * right[k] for k in range(3))
            shoulder = (shoulder[0], neck[1] - 0.03, shoulder[2])
            elbow, wrist = limb_chain(shoulder, f, arm, elbow_bend, (b["upperArm"], b["forearm"]), 1)[1:]
            strokes += [(ink, shoulder, elbow), (ink, elbow, wrist)]
            strokes.append((GUIDE_BODY_INK, shoulder, (0.0, neck[1], 0.0)))
            strokes.append((GUIDE_BODY_INK, hip, (0.0, pelvis_y, 0.0)))
        strokes.append((GUIDE_BODY_INK, (0.0, pelvis_y, 0.0), neck))

        def screen(p):
            return ox + p[0] * per_m, oy - (p[1] * math.cos(pitch) + p[2] * math.sin(pitch)) * per_m

        def depth(p):
            return p[2] * math.cos(pitch) - p[1] * math.sin(pitch)

        nose = tuple(head[k] + b["head"] * f[k] for k in range(3))
        items = [(depth(((a[0] + c[0]) / 2, (a[1] + c[1]) / 2, (a[2] + c[2]) / 2)), "line", ink, a, c)
                 for ink, a, c in strokes]
        items.append((depth(head), "head", GUIDE_BODY_INK, head, None))
        items.append((depth(nose), "nose", GUIDE_NOSE, nose, None))
        for _, kind, ink, a, c in sorted(items, key=lambda it: -it[0]):
            if kind == "line":
                draw.line([screen(a), screen(c)], fill=ink, width=width, joint="curve")
                for p in (a, c):
                    x, y = screen(p)
                    draw.ellipse([x - width / 2, y - width / 2, x + width / 2, y + width / 2], fill=ink)
            else:
                x, y = screen(a)
                r = b["head"] * per_m if kind == "head" else width * 0.6
                draw.ellipse([x - r, y - r, x + r, y + r], fill=ink)
    out.parent.mkdir(parents=True, exist_ok=True)
    img.save(out)
    return out


#: A walk clip (`shared_cycle`): how long one cycle, two steps, may last — a relaxed walk is about a second, and a clip
#: of several walkers walks slower, which costs nothing, the engine stepping columns by distance walked — the share of
#: the clip at either end its cycle is not looked for in (a clip asked to end where it began slows into its last
#: frame), and the size the legs are compared at.
CYCLE_S = (0.7, 2.2)
CLIP_EDGE_SHARE = 0.15
LEGS_GRID_PX = (48, 64)


def clip_frames(path: Path):
    """A clip's frames as RGB arrays, and its frames a second."""
    try:
        import av
    except ModuleNotFoundError:
        # PyAV is installed per user (`pip --user`: the system has no H.264 decoder of its own), and `qq` runs
        # `python -I`, which leaves the user's site off the path.
        import site
        import sys

        sys.path.append(site.getusersitepackages())
        import av
    import numpy as np

    with av.open(str(path)) as clip:
        stream = clip.streams.video[0]
        return [np.asarray(f.to_image().convert("RGB")) for f in clip.decode(stream)], float(stream.average_rate)


#: How far a keyed clip pixel's screen channel may stand above its other two before it is the screen's — a shadow or a
#: speck the key left by the feet — and cleared; under it the excess is taken out of the colour (despill).
SPILL_CLEAR = 40


def despilled(rgba, key):
    """A keyed clip frame off a green or blue screen (`key`, its colour) with the screen's channel taken back out: a
    pixel that much more the screen's than SPILL_CLEAR cleared, any other's held to its other two. A look in the
    screen's colour keys out with it, and wants the other screen."""
    import numpy as np

    out = rgba.astype(np.int16)
    c = int(np.argmax(key))
    others = np.maximum(out[..., (c + 1) % 3], out[..., (c + 2) % 3])
    excess = out[..., c] - others
    out[excess > SPILL_CLEAR] = 0
    tinted = (excess > 0) & (excess <= SPILL_CLEAR)
    out[..., c][tinted] = others[tinted]
    return out.clip(0, 255).astype(np.uint8)


#: Columns of clear between two runs of paint that make them two things: a figure and a neighbour's foot reaching into
#: its band, as a share of the frame's height.
FIGURE_GAP_SHARE = 0.01


def figure_of(rgba):
    """The one figure on a keyed clip frame (or a band of one), cropped to its paint, and where the crop's corner is on
    the frame. Of the runs of painted columns, only the widest: anything else is a neighbour's foot or a speck."""
    import numpy as np

    solid = rgba[..., 3] > 128
    gap = max(2, round(rgba.shape[0] * FIGURE_GAP_SHARE))
    painted = np.flatnonzero(solid.any(0))
    runs = np.split(painted, np.flatnonzero(np.diff(painted) > gap) + 1)
    widest = max(runs, key=len)
    x0, x1 = int(widest[0]), int(widest[-1]) + 1
    rows = np.flatnonzero(solid[:, x0:x1].any(1))
    return rgba[rows[0]:rows[-1] + 1, x0:x1], (x0, int(rows[0]))


#: The share of a figure, from the bottom, its feet's spread is read over (`gait_signal`).
FEET_SHARE = 0.25


def feet_apart(figure, height: int) -> float:
    """How far apart a figure's feet are, over `height`: the spread of the paint in its bottom FEET_SHARE, across and
    down together, and how far one foot is above the other (each side of the head-and-shoulders vertical's lowest
    paint) — a side view strides across, a front or back one up the picture, where a foot stepped forward is mostly
    hidden behind the body. High at a heel strike, low at a passing."""
    import numpy as np

    # The solid paint's own rows, so a crop's faint edge rows never move which rows are the feet.
    ys = np.flatnonzero((figure[..., 3] > 128).any(1))
    figure = figure[ys[0]:ys[-1] + 1] if len(ys) else figure
    feet = figure[round(figure.shape[0] * (1 - FEET_SHARE)):, :, 3] > 128
    ys, xs = np.nonzero(feet)
    spread = np.sqrt(xs.var() + ys.var()) if len(xs) else 0.0
    ax = anchor_x(figure[..., 3] > 128)
    lows = [np.flatnonzero(side.any(1)) for side in (feet[:, :ax], feet[:, ax:])]
    rise = abs(int(lows[0][-1]) - int(lows[1][-1])) if all(len(l) for l in lows) else 0
    return float(spread + rise) / height


def gait_signal(figures):
    """Per frame, `feet_apart` over the first figure's height, smoothed over three frames so a strike reads as one
    peak."""
    import numpy as np

    raw = np.array([feet_apart(f, figures[0].shape[0]) for f in figures])
    return np.convolve(np.pad(raw, 1, mode="edge"), np.ones(3) / 3, mode="valid")


#: The bottom of a figure its two feet are told apart in (`feet_side_by_side`), and how much the two legs' unlike widths
#: weigh against their unlike reach down: a foot half hidden behind the other leg is a narrower run.
GROUND_SHARE, UNLIKE_WIDTH_WEIGHT = 0.12, 0.1


def feet_side_by_side(figure) -> float:
    """How far a figure seen from in front or behind is from standing on both feet side by side: in its bottom
    GROUND_SHARE, its two widest separate runs of paint across, how unevenly low they reach over its height and how
    unlike their widths — without bound where the legs make one run. A passing, which `feet_apart` reads as feet
    together, lifts the swinging foot towards the camera or hides it behind the planted leg, and a foot kicked back
    towards a camera behind is drawn as low as the planted one, so neither a spread nor a level reads a stance here."""
    import numpy as np

    ys = np.flatnonzero((figure[..., 3] > 128).any(1))
    figure = figure[ys[0]:ys[-1] + 1] if len(ys) else figure
    ground = figure[round(figure.shape[0] * (1 - GROUND_SHARE)):, :, 3] > 128
    on = np.flatnonzero(ground.any(0))
    runs = sorted((r for r in np.split(on, np.flatnonzero(np.diff(on) > 1) + 1) if len(r) > 1), key=len)[-2:]
    if len(runs) < 2:
        return float("inf")
    lows = [int(np.flatnonzero(ground[:, r].any(1))[-1]) for r in runs]
    return abs(lows[0] - lows[1]) / figure.shape[0] + UNLIKE_WIDTH_WEIGHT * (len(runs[1]) - len(runs[0])) / len(runs[1])


def stance_column(row, facing: int) -> int:
    """Of a facing's columns, the one nearest standing, which a stopped walker is drawn in: from in front or behind,
    both feet down side by side (`feet_side_by_side`), or where no column has both, as from the side or a diagonal, the
    passing, feet closest together (`feet_apart`)."""
    import numpy as np

    near = [feet_side_by_side(c) for c in row] if facing in (BACK, FRONT) else [float("inf")]
    if min(near) == float("inf"):
        near = [feet_apart(c, c.shape[0]) for c in row]
    return int(np.argmin(near))


#: How the cycle a clip is cut to is weighed (`shared_cycle`): its seam against one frame's change, its two steps'
#: difference in length and in time, its start against a passing, how unevenly its columns fall on whole frames, and
#: how far the head and torso turn from the start through it, against the whole figure's change in one frame.
LOOP_WEIGHTS = {"seam": 1.0, "length": 2.0, "timing": 1.0, "start": 0.5, "spacing": 0.5, "drift": 0.2}
#: Past these a facing's cycle is flagged for a look (`shared_cycle`): a seam that shows, steps unlike; and a figure
#: whose head and torso change more than DRIFT_FLAG times the median facing's, which is one that turns — every facing's
#: arms swing through the upper body, so drift is only read against the others.
LOOP_FLAGS = {"seam": 1.0, "length": 0.25, "timing": 0.25}
DRIFT_FLAG = 1.5


def body_of(figure):
    """The whole figure as a small premultiplied picture, its head and shoulders' vertical at the middle and its feet
    on the bottom: what a loop's last frame and its first are compared by."""
    import numpy as np
    from PIL import Image

    ax = anchor_x(figure[..., 3] > 128)
    half = max(ax, figure.shape[1] - ax)
    padded = np.zeros((figure.shape[0], 2 * half, 4), np.uint8)
    padded[:, half - ax:half - ax + figure.shape[1]] = figure
    small = np.asarray(Image.fromarray(padded, "RGBA").resize((LEGS_GRID_PX[0], 2 * LEGS_GRID_PX[1]),
                                                              Image.BILINEAR)).astype(np.float32) / 255
    return small[..., :3] * small[..., 3:4]


def upper_of(figure):
    """The head and torso as a small premultiplied picture on the head-and-shoulders vertical: what a facing that turns
    changes and a stride does not."""
    return body_of(figure[:round(figure.shape[0] * (1 - LEGS_SHARE))])


def looped_cycle(figures, fps: float, columns: int = SHEET_COLS, edge_share: float = CLIP_EDGE_SHARE,
                 cycle_s: tuple[float, float] = CYCLE_S):
    """`shared_cycle` of one figure."""
    picks, parts = shared_cycle([figures], fps, columns, edge_share, cycle_s)
    return picks, parts[0]


def shared_cycle(walkers, fps: float, columns: int = SHEET_COLS, edge_share: float = CLIP_EDGE_SHARE,
                 cycle_s: tuple[float, float] = CYCLE_S):
    """The stretch of a clip that loops best as one walk cycle for every figure walking in it at once (one list of
    frames each, one clip's frames), and its columns at even steps of time through it — one window for all, so a walker
    turning in the game lands on the same step in its new facing. Of every start and every length a cycle may last
    (CYCLE_S) between the clip's ends, the one whose figures' summed weight is lowest (LOOP_WEIGHTS): the seam — the
    frame one cycle on against the start — over the clip's change in one frame; the two halves' strikes (the gait
    signal's peaks) unlike in height and off half a cycle apart; a start away from a passing; columns falling unevenly
    on whole frames; the head and torso turning from the start. `edge_share` 0 searches a clip asked to loop on itself
    end to end (its last frame left out, being its first). Returns the columns' frames and each figure's parts, with
    the flags (LOOP_FLAGS) it raised."""
    import numpy as np

    n = len(walkers[0])
    lo, hi = round(n * edge_share), round(n * (1 - edge_share))
    least, most = max(columns, round(cycle_s[0] * fps)), min(hi - lo - 1, round(cycle_s[1] * fps))
    # A clip asked to loop on itself is a cycle whole, its seam the clip's own last step back into its first frame.
    whole = edge_share == 0 and n <= round(cycle_s[1] * fps)
    if most <= least and not whole:
        raise ValueError(f"a clip of {n} frames holds no walk cycle of {cycle_s[0]}-{cycle_s[1]} s between its ends")
    looked = []
    for figures in walkers:
        bodies = np.stack([body_of(f) for f in figures])
        uppers = np.stack([upper_of(f) for f in figures])
        step = max(1e-6, float(np.mean(np.abs(np.diff(bodies[lo:hi], axis=0)).mean(axis=(1, 2, 3)))))
        signal = gait_signal(figures)
        looked.append((bodies, uppers, step, signal, float(signal[lo:hi].min()),
                       max(1e-6, float(np.ptp(signal[lo:hi])))))

    def weighed(a, length):
        half = length // 2
        gaps = np.diff([round(k * length / columns) for k in range(columns)] + [length])
        spacing = float(np.ptp(gaps) / gaps.mean())
        out = []
        for bodies, uppers, step, signal, floor, span in looked:
            seam = float(np.abs(bodies[a] - bodies[(a + length) % n] if length < n else
                                bodies[n - 1] - bodies[0]).mean()) / step
            first, second = signal[a:a + half], signal[a + half:a + length]
            m1, m2 = int(np.argmax(first)), half + int(np.argmax(second))
            amp = abs(float(first[m1] - second[m2 - half])) / max(1e-6, float(first[m1] + second[m2 - half]) / 2)
            timing = abs((m2 - m1) - length / 2) / (length / 2)
            drift = float(np.abs(uppers[a:a + length] - uppers[a]).mean(axis=(1, 2, 3)).max()) / step
            out.append({"seam": seam, "length": amp, "timing": timing, "start": (float(signal[a]) - floor) / span,
                        "spacing": spacing, "drift": drift})
        return out

    best = None
    for length in [*range(least, most + 1), *([n] if whole else [])]:
        for a in (range(lo, hi - length + 1) if length < n else [0]):
            parts = weighed(a, length)
            score = sum(LOOP_WEIGHTS[k] * v for p in parts for k, v in p.items())
            if best is None or score < best[0]:
                best = (score, a, length, parts)
    _, a, length, parts = best
    usual_drift = float(np.median([p["drift"] for p in parts]))
    report = [{"periodS": round(length / fps, 3), **{k: round(v, 3) for k, v in p.items()},
               "flags": [k for k, bound in LOOP_FLAGS.items() if p[k] > bound]
               + (["drift"] if p["drift"] > DRIFT_FLAG * usual_drift else [])} for p in parts]
    return [a + round(k * length / columns) for k in range(columns)], report


#: An orbit clip's hard cuts (`orbit_shots`): a frame's change past CUT_FACTOR times the clip's median, the strongest
#: first, no shot shorter than MIN_SHOT_SHARE of the shot a facing is asked for (the clip over FACINGS: 0.6 s in a 5 s
#: clip). Read over the frame's upper half, the head and torso of a figure laid in its middle: a stride swings the legs
#: and barely moves them, so a stride peaks under about 1.9 times the median there and a cut between two like views
#: (side to back-left) still passes 2.4. A 10 s clip's 2 s shots hold strides that pass it: held at 0.6 s,
#: `walkorbit_quilted_navy-v5` was cut into eight shots, her front into three.
CUT_FACTOR, MIN_SHOT_SHARE = 2.0, 0.6
#: How long one orbit shot's cycle may last: a shot asked for two cycles is never cut to both at once.
ORBIT_CYCLE_S = (0.7, 1.4)
#: How far a side or diagonal loop may last from the stride its front and back loops took (`stride_bounds`).
STRIDE_SHARE = 0.15


def stride_bounds(taken: dict, cycle_s: tuple[float, float], shot_frames: int, fps: float) -> tuple[float, float]:
    """The cycle a side or diagonal shot's loop may last: within STRIDE_SHARE of the front and back loops' mean, the
    walker walking one pace through the clip. From in front or behind a loop of one step shows only one leg striding;
    from the side the two steps look alike, so a loop of one step and a half looped as well as a stride there — and
    `walkorbit_quilted_navy-v5`'s side took 0.75 s against a front and back of 1.0 s, a step every 10–15 frames. A
    stride the shot cannot hold (a 1 s shot, a 1.2 s stride) leaves `cycle_s` as it was."""
    import numpy as np

    stride = float(np.mean([taken[f][3]["periodS"] for f in (FRONT, BACK)]))
    held = (max(cycle_s[0], stride * (1 - STRIDE_SHARE)), min(cycle_s[1], stride * (1 + STRIDE_SHARE)))
    return held if held[0] * fps < shot_frames - 1 and held[0] <= held[1] else cycle_s


def orbit_shots(frames, fps: float):
    """An orbit clip's shots (`raw_assets/agents/person/prompting.md`: the camera cuts round one walker, a facing a
    shot), as [start, end) frames, found by its hard cuts."""
    import numpy as np

    grey = [f[:f.shape[0] // 2].mean(-1) for f in frames]
    change = np.array([np.abs(b - a).mean() for a, b in zip(grey, grey[1:])])
    # The clip's ends bound a shot as a cut does: HeyGen's first frame, the picture it was given, steps to its own
    # rendering of it (`walkstand_linen_grey-v1`).
    cuts = [0, len(frames)]
    shortest = MIN_SHOT_SHARE * len(frames) / FACINGS
    for i in np.argsort(change)[::-1]:
        if change[i] <= CUT_FACTOR * np.median(change):
            break
        if all(abs(int(i) + 1 - c) >= shortest for c in cuts):
            cuts.append(int(i) + 1)
    cuts.sort()
    return list(zip(cuts, cuts[1:]))


def split_shots(shots, splits):
    """`shots` cut again where `orbit_shots` found no hard cut: each split `(n,)` a cut at frame n, or `(a, b)` a camera
    sweeping from one facing to the next over frames a..b-1, which neither shot keeps (`walkorbit_tee_print-v6`'s side
    to back-left, its strongest change 1.5 times the median, under CUT_FACTOR)."""
    for split in sorted(splits, reverse=True):
        a, b = split if len(split) == 2 else split * 2
        inside = [i for i, (s, e) in enumerate(shots) if s < a and b < e]
        if len(split) not in (1, 2) or a > b or not inside:
            raise ValueError(f"{'-'.join(map(str, split))} is not inside one shot of {shots}")
        s, e = shots[inside[0]]
        shots = shots[:inside[0]] + [(s, a), (b, e)] + shots[inside[0] + 1:]
    return shots


#: A shot whose lowest paint drifts down the picture faster than this share of the figure's height a second walks
#: towards the camera, not on the spot: `held_still` takes it out, and the flag says to look at the loop it left. Set on
#: 2026-10-28's clips: the four-sentence brief's front shots 6–21 % a second, `walkfast_linen-v1` within 5 %.
ADVANCE_FLAG = 0.04


def advance_per_s(crops, fps: float) -> float:
    """How fast a shot's lowest paint moves down the picture, as a share of the figure's median height a second: a
    straight line through every frame's, so a stride's own rise and fall averages out."""
    import numpy as np

    lows = np.array([o[1] + c.shape[0] for c, o in crops], float)
    return float(np.polyfit(np.arange(len(lows)) / fps, lows, 1)[0] / np.median([c.shape[0] for c, _ in crops]))


def held_still(crops, fps: float):
    """A shot's figures with its advance taken out (`advance_per_s`): a walker drifting towards or away from a camera
    high above grows or shrinks, its feet sliding down or up the picture, about evenly through a one-second shot. A
    straight line through every frame's height and lowest paint is that drift, and each frame is scaled about its
    feet's middle and moved so both stay on the line's mean, which a stride's own stretch and bob — up and down within
    each step — leave alone. Without it a loop's last frame stands taller than its first, its seam shows and the look
    changes size as it walks (`walkorbit_flannel_hat-v3`, the owner: "all change size during walk"). A camera panning
    across a shot is held the same way, by a straight line through the head and shoulders' vertical (`anchor_x`), so
    the sway about it stays: `walkorbit_tee_print-v7` slid 10 px a second sideways in four of its five shots, a hop at
    every seam."""
    import numpy as np
    from PIL import Image

    t = np.arange(len(crops)) / fps
    heights = np.array([c.shape[0] for c, _ in crops], float)
    lows = np.array([o[1] + c.shape[0] for c, o in crops], float)
    mids = np.array([x + anchor_x(c[..., 3] > 128) for c, (x, _) in crops], float)
    height_at = np.polyval(np.polyfit(t, heights, 1), t)
    low_at = np.polyval(np.polyfit(t, lows, 1), t)
    mid_at = np.polyval(np.polyfit(t, mids, 1), t)
    out = []
    for (c, (x, y)), k, low, mid, held in zip(crops, height_at.mean() / height_at, lows - (low_at - low_at.mean()),
                                               mids, mids - (mid_at - mid_at.mean())):
        img = Image.fromarray(c, "RGBA").resize((max(1, round(c.shape[1] * k)), max(1, round(c.shape[0] * k))),
                                                Image.LANCZOS)
        out.append((np.asarray(img), (round(held - (mid - x) * k), round(low) - img.height)))
    return out


#: The rows of a figure, from its top, its torso's width is read over (`torso_width`): below the head and shoulders a
#: high camera behind lays over the neck, above a belt — arms hanging at the sides in front as behind. To 0.55 it took
#: in `police_b`'s holster from behind and drew her front 13 % too large.
TORSO_ROWS = (0.25, 0.45)


def torso_width(figure) -> float:
    """The widest row of paint across a figure's torso (TORSO_ROWS)."""
    import numpy as np

    solid = figure[..., 3] > 128
    ys = np.flatnonzero(solid.any(1))
    h = ys[-1] - ys[0] + 1
    rows = solid[ys[0] + round(h * TORSO_ROWS[0]):ys[0] + round(h * TORSO_ROWS[1])]
    return float(max((np.ptp(np.flatnonzero(r)) + 1 for r in rows if r.any()), default=0))


def front_by_width(scales: dict, widths: dict) -> float:
    """The front shot's scale such that its torso stands as wide as the back's: a body is as wide from in front as from
    behind, and a width across the picture does not change with the camera's pitch, which a height does. The front shot
    keeps the cast picture's steep camera while HeyGen takes the others lower (the owner, 2026-10-28: the man in the
    white shirt bigger facing down than down-left), so at one standing height its head and body drew a tenth to a fifth
    larger: `linen_grey`'s head 35 px from in front, 28–30 from every other side, and 29 once matched by width. It only
    ever shrinks the front: a back wider than the front at one height is a back shot taken nearer (`police_b`, 13 %),
    and the front keeps its height. The diagonals, whose widths differ by turn as well as by size, are
    `diagonal_by_widths`'."""
    by_width = scales[BACK] * widths[BACK] / widths[FRONT] if widths[FRONT] else scales[FRONT]
    return min(scales[FRONT], by_width)


#: The diagonals, each matched by width to the front, back and side it lies between (`diagonal_by_widths`). The torso
#: must stand at least TORSO_TURN_SHARE wider from in front than from the side for its width to say a turn: under it the
#: solve answers a turn anyway and sizes by noise — `flannel_hat`, a backpack filling her side out to 157 px against
#: 157 front and back, drew front-left 6 % large; `tee_print` 200 against 118, `quilted_navy` 170 against 113.
DIAGONALS, TORSO_TURN_SHARE = (1, 3), 0.15


def diagonal_by_widths(scales: dict, heads: dict, torsos: dict) -> dict:
    """Each diagonal's scale, and the turn it was taken at, off its head's and torso's widths, which do not change with
    the camera's pitch: a part w wide from in front or behind and s from the side stands √((w·cos θ)² + (s·sin θ)²)
    wide turned θ, and the head (narrower from in front than from the side) and the torso (wider) answer θ differently
    enough that the two together give both θ and the diagonal's size. A height gave neither: HeyGen seldom takes a
    diagonal at 45° nor at the others' pitch, and `tee_print`'s front-left, taken steeper, drew its head as wide as its
    side's. The front, back and side are the reference, at their scales already chosen. A torso about as wide from the
    side as from in front (TORSO_TURN_SHARE: a backpack, a coat's bulk), or a turn the widths put outside 0–90° — a bun
    or a brim widening the head past the model — keeps the diagonal's height, and says so.
    Returns {facing: (scale, turn in degrees or None)}."""
    import math

    import numpy as np

    def at(r, part):
        return part[r] * scales[r]

    hf, tf = (float(np.mean([at(r, p) for r in (FRONT, BACK)])) for p in (heads, torsos))
    hs, ts = at(SIDE, heads), at(SIDE, torsos)
    if tf < ts * (1 + TORSO_TURN_SHARE):
        return {d: (scales[d], None) for d in DIAGONALS}
    out = {}
    for d in DIAGONALS:
        h, t = at(d, heads), at(d, torsos)
        ratio = (h / t) ** 2
        across = ratio * (tf ** 2 - ts ** 2) - (hf ** 2 - hs ** 2)
        cos2 = (hs ** 2 - ratio * ts ** 2) / across if across else -1.0
        if not 0 <= cos2 <= 1:
            out[d] = (scales[d], None)
            continue
        size = t / math.sqrt(ts ** 2 + cos2 * (tf ** 2 - ts ** 2))
        out[d] = (scales[d] / size, math.degrees(math.acos(math.sqrt(cos2))))
    return out


def cut_clip(frames, fps: float, keyed, height_m: float, picked: list[int] | None = None, shots=None,
             cycle_s: tuple[float, float] = ORBIT_CYCLE_S, rescale: dict[int, float] | None = None,
             sized: bool = True):
    """A look's block off an orbit clip: its last FACINGS shots front, front and left side, left side, back and left
    side, back — any shot before them a warm-up — or the shots `picked` names by number, front to back, where the clip
    went wrong in one and another holds its facing; each its best loop (`looped_cycle`) at SHEET_COLS columns, column 0
    the loop's frame nearest standing (`stance_column`) and the rest on from it in order, so a walker standing shows its
    stance and every look's sheet starts its walk alike. Nothing is re-centred: each shot's frames keep
    where the clip put them about its own head-and-shoulders vertical and lowest foot, so the clip's sway and bob stay
    as drawn. Each shot is scaled once, whole, so its stance stands as tall as the facings' median stance — a camera
    that drifts nearer between shots would otherwise grow her as she turns. `keyed(rgb)` is a frame off its screen as
    RGBA. `shots`, where given, are the whole clip's (`orbit_shots`) when `frames` are one tile of it. `rescale`, per
    drawn facing, multiplies the scale all that comes before chose — a size measured by eye where the rules miss it
    (a head outlined in every facing, `qq props sizes`). `sized` False leaves every frame at the size the clip drew it:
    no shot scaled, no drift held still, `rescale` ignored (the owner, 2026-10-28, on `tee_print`: "remove sizing at
    all — show original from video gen"). Returns (block, info)."""
    import math

    import numpy as np
    from PIL import Image

    shots = shots or orbit_shots(frames, fps)
    picked = picked or list(range(len(shots) - FACINGS, len(shots)))
    if len(picked) != FACINGS or min(picked) < 0 or max(picked) >= len(shots):
        raise ValueError(f"{len(shots)} shot(s) found, shots {picked} picked: a facing a shot wants {FACINGS}")
    rows, report, flags = {}, [], []
    shot_of = {FACINGS - 1 - s: shots[p] for s, p in enumerate(picked)}
    taken = {}
    for facing in (FRONT, BACK, *sorted(set(shot_of) - {FRONT, BACK}, reverse=True)):
        a, b = shot_of[facing]
        raw = [figure_of(keyed(frames[i])) for i in range(a, b)]
        crops = held_still(raw, fps) if sized else raw
        within = cycle_s if facing in (FRONT, BACK) else stride_bounds(taken, cycle_s, b - a, fps)
        taken[facing] = (raw, crops, *looped_cycle([c for c, _ in crops], fps, edge_share=0, cycle_s=within))
    for facing in sorted(shot_of, reverse=True):
        a, b = shot_of[facing]
        raw, crops, picks, part = taken[facing]
        advance = advance_per_s(raw, fps)
        placed = [crops[p] for p in picks]
        centre = float(np.median([o[0] + anchor_x(c[..., 3] > 128) for c, o in placed]))
        floor = max(o[1] + c.shape[0] for c, o in placed)
        rows[facing] =[(c, o[0] - centre, o[1] - floor) for c, o in placed]
        report.append({"facing": facing, "frames": [a, b], "picks": [a + p for p in picks], **part,
                       "advancePerS": round(advance, 3)})
        flags += [f"facing {facing}: {k}" for k in part["flags"]]
        if abs(advance) > ADVANCE_FLAG:
            flags.append(f"facing {facing}: feet drift {advance:+.0%} of its height a second, walking "
                         f"{'towards' if advance > 0 else 'away from'} the camera — "
                         f"{'held still (`held_still`)' if sized else 'left as drawn (unsized)'}; look at its loop")
    # Column 0 is the stance the game stands a stopped walker in (PersonSprites): of the loop's columns as drawn, the
    # one nearest standing (`stance_column`). Each shot is scaled by its stance's height and not by its loop's median:
    # from in front or behind a stride drops the forward foot lower in the picture and stretches the figure, from the
    # side it does not, so a median over the loop shrank the front and back about a tenth more than the side.
    stances = {r: stance_column([c for c, _, _ in row], r) for r, row in rows.items()}
    statures = {r: float(row[stances[r]][0].shape[0]) for r, row in rows.items()}
    stature = float(np.median(list(statures.values())))
    scales = {r: stature / statures[r] for r in rows}
    # The front and the diagonals by width (`front_by_width`, `diagonal_by_widths`), the camera's pitch being each
    # shot's own.
    widths = {r: float(np.median([torso_width(c) for c, _, _ in row])) for r, row in rows.items()}
    heads = {r: float(np.median([head_width(c) for c, _, _ in row])) for r, row in rows.items()}
    scales[FRONT] = front_by_width(scales, widths)
    turns = {}
    for d, (k, turn) in diagonal_by_widths(scales, heads, widths).items():
        scales[d], turns[d] = k, turn
        if turn is None:
            flags.append(f"facing {d}: its widths say no turn between front and side (a torso as wide from the side, "
                         f"or a head widened past the model) — kept by height")
    for r, k in (rescale or {}).items():
        scales[r] *= k
    if not sized:
        scales = {r: 1.0 for r in rows}
    for r, row in rows.items():
        k = scales[r]
        rows[r] = [(np.asarray(Image.fromarray(c, "RGBA").resize(
            (max(1, round(c.shape[1] * k)), max(1, round(c.shape[0] * k))), Image.LANCZOS)), dx * k, round(dy * k))
            for c, dx, dy in row]
    for item in report:
        # `scale` matches heights alone, which the eye-level hint reads; `applied` is what the frames were scaled by.
        item["scale"] = round(stature / statures[item["facing"]], 3)
        item["applied"] = round(scales[item["facing"]], 3)
        if item["facing"] == FRONT:
            item["byWidth"] = round(scales[FRONT] / (rescale or {}).get(FRONT, 1.0), 3)
        if turns.get(item["facing"]) is not None:
            item["turnDeg"] = round(turns[item["facing"]], 1)
        if item["facing"] in (rescale or {}):
            item["rescale"] = rescale[item["facing"]]
        # The loop turned to start at its stance and run on in order.
        row = rows[item["facing"]]
        stance = stances[item["facing"]]
        rows[item["facing"]] = row[stance:] + row[:stance]
        item["picks"] = item["picks"][stance:] + item["picks"][:stance]
        item["stanceColumn"] = stance
    half = max(max(-dx, c.shape[1] + dx) for row in rows.values() for c, dx, _ in row)
    tall = max(-dy for row in rows.values() for _, _, dy in row)
    cw, ch = 2 * (math.ceil(half) + PAD_PX), tall + 2 * PAD_PX
    block = Image.new("RGBA", (cw * SHEET_COLS, ch * FACINGS), (0, 0, 0, 0))
    for r, row in rows.items():
        for f, (c, dx, dy) in enumerate(row):
            # Composited, not pasted through its own alpha, which would square every soft edge's alpha.
            block.alpha_composite(Image.fromarray(c, "RGBA"), (f * cw + cw // 2 + round(dx), r * ch + ch - PAD_PX + dy))
    info = {"cell": 1, "pxPerM": round(ch / height_m, 3), "frames": SHEET_COLS, "facings": FACINGS,
            "cellPx": [cw, ch], "shotsAt": [a for a, _ in shots], "fps": fps, "cycles": report}
    swaps = legs_swapped(block, info)
    flags += [f"facing {r} strides one leg only ({s:.2f} of the stance's change)" for r, s in enumerate(swaps)
              if s < SWAP_FLAG]
    flags += eye_level_flags(block, info)
    return block, {**info, "legSwap": [round(s, 2) for s in swaps], "flags": flags}


#: The side shot drawn at eye level, the orbit flow's commonest failure (raw_assets/agents/person/prompting.md): a flat
#: profile, no crown or shoulder tops, the legs full length. Two hints, either of which asks for a look at the side row,
#: set on the eighteen looks of 2026-10-28 — four at eye level, and every one of them raised one. The side shot scaled
#: down past EYE_LEVEL_SCALE of its back and front shots' mean: the camera came lower and nearer, so the figure drew
#: taller. Set with each shot scaled by its stance (`cut_clip`): the four eye-level sides 0.73–0.86, the eighteen kept
#: from above 0.83–1.05; the one eye-level side above 0.8 raises the other hint. Or the side's figure taller than
#: EYE_LEVEL_TALLNESS head-widths (eye level 5.41–5.97, kept 5.09 at most): pitch shortens a height and leaves a width,
#: so an eye-level figure stands more heads tall; hair worn out behind (a bun, a ponytail) widens the head and hides it.
EYE_LEVEL_SCALE, EYE_LEVEL_TALLNESS, HEAD_SHARE = 0.8, 5.3, 0.08
SIDE, BACK, FRONT = 2, 0, 4


def head_width(cell) -> float:
    """A figure's head's width: the widest run of paint in its top HEAD_SHARE."""
    import numpy as np

    solid = cell[..., 3] >= 128
    ys = np.flatnonzero(solid.any(1))
    height = ys[-1] - ys[0] + 1
    widest = 1
    for row in solid[ys[0]:ys[0] + max(1, round(HEAD_SHARE * height))]:
        on = np.flatnonzero(row)
        if on.size:
            widest = max(widest, max(len(p) for p in np.split(on, np.flatnonzero(np.diff(on) > 1) + 1)))
    return float(widest)


def tallness(cell) -> float:
    """A figure's height over its head's width (`head_width`)."""
    import numpy as np

    ys = np.flatnonzero((cell[..., 3] >= 128).any(1))
    return (ys[-1] - ys[0] + 1) / head_width(cell)


def eye_level_flags(block, info) -> list[str]:
    import numpy as np

    scale = {c["facing"]: c["scale"] for c in info["cycles"]}
    lowered = scale[SIDE] / ((scale[BACK] + scale[FRONT]) / 2)
    cw, ch = info["cellPx"]
    pixels = np.asarray(block)
    tall = float(np.median([tallness(pixels[SIDE * ch:(SIDE + 1) * ch, c * cw:(c + 1) * cw])
                            for c in range(SHEET_COLS)]))
    info["sideAtEyeLevel"] = {"scale": round(lowered, 2), "tallness": round(tall, 2)}
    flags = []
    if lowered < EYE_LEVEL_SCALE:
        flags.append(f"facing {SIDE} scaled {lowered:.2f} of back and front: camera lower and nearer? look at it")
    if tall > EYE_LEVEL_TALLNESS:
        flags.append(f"facing {SIDE} {tall:.1f} heads tall: at eye level? look at it")
    return flags


#: How far either way a frame is slid to lie on the first one's head and torso, as a share of its height.
REGISTER_REACH = 0.08


def registered_row(figures, origins, picks, scale: float):
    """The picked figures as a row at one scale, every foot that bears weight on one floor — each crop ends at its
    lowest paint — and each frame slid across to lie best on the first one's head and torso (the most of their paint
    overlapping), so the body stays put through the loop and back into its start. Returns per column (image, x of its
    left edge from the first frame's head-and-shoulders vertical, y of its bottom from the floor)."""
    import numpy as np
    from PIL import Image

    upper = [f[:round(f.shape[0] * (1 - LEGS_SHARE)), :, 3] > 128 for f in (figures[i] for i in picks)]
    height = max(u.shape[0] for u in upper)
    reach = round(figures[picks[0]].shape[0] * REGISTER_REACH)
    lefts = [origins[i][0] for i in picks]
    x0 = min(lefts) - reach
    width = max(l + figures[i].shape[1] for l, i in zip(lefts, picks)) + reach - x0

    def laid(n, dx=0):
        canvas = np.zeros((height, width), bool)
        u = upper[n]
        canvas[:u.shape[0], lefts[n] - x0 + dx:lefts[n] - x0 + dx + u.shape[1]] = u
        return canvas

    reference = laid(0)
    shifts = [0] + [max(range(-reach, reach + 1), key=lambda dx: int((laid(n, dx) & reference).sum()))
                    for n in range(1, len(picks))]
    centre = lefts[0] + anchor_x(figures[picks[0]][..., 3] > 128)
    row = []
    for n, i in enumerate(picks):
        img = Image.fromarray(figures[i], "RGBA")
        img = img.resize((max(1, round(img.width * scale)), max(1, round(img.height * scale))), Image.LANCZOS)
        row.append((img, (lefts[n] + shifts[n] - centre) * scale, 0.0))
    return row


def legs_of(figure):
    """The figure's legs as a small premultiplied picture, its head and shoulders' vertical at the middle: what two
    frames of a walk are compared by. Colour and not only the silhouette, since a near leg and a far one differ in
    shade."""
    import numpy as np
    from PIL import Image

    solid = figure[..., 3] > 128
    ax = anchor_x(solid)
    h = figure.shape[0]
    half = max(ax, figure.shape[1] - ax)
    padded = np.zeros((h, 2 * half, 4), np.uint8)
    padded[:, half - ax:half - ax + figure.shape[1]] = figure
    legs = padded[round(h * (1 - LEGS_SHARE)):]
    small = np.asarray(Image.fromarray(legs, "RGBA").resize(LEGS_GRID_PX, Image.BILINEAR)).astype(np.float32) / 255
    return small[..., :3] * small[..., 3:4]


def info_of(variant: Path) -> dict:
    from PIL import Image

    return json.loads(Image.open(variant).text["cut"])
