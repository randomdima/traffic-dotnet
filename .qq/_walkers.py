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
                block.paste(img, (f * cw + cw // 2 - ax, r * ch + ch - PAD_PX - img.height), img)
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


#: How far a keyed clip pixel's green may stand above its red and blue before it is the screen's — a shadow or a speck
#: the key left by the feet — and cleared; under it the excess is taken out of the colour (despill).
SPILL_CLEAR = 40


def despilled(rgba):
    """A keyed clip frame off a green screen with the green taken back out: a pixel greener than SPILL_CLEAR cleared,
    any other's green held to its red and blue. A look in green clothes wants another screen colour."""
    import numpy as np

    out = rgba.astype(np.int16)
    excess = out[..., 1] - np.maximum(out[..., 0], out[..., 2])
    out[excess > SPILL_CLEAR] = 0
    tinted = (excess > 0) & (excess <= SPILL_CLEAR)
    out[..., 1][tinted] = np.maximum(out[..., 0], out[..., 2])[tinted]
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


def gait_signal(figures):
    """Per frame, how far apart the feet are, over the stance's height: the spread of the paint in the figure's bottom
    FEET_SHARE, across and down together, and how far one foot is above the other (each side of the head-and-shoulders
    vertical's lowest paint) — a side view strides across, a front or back one up the picture, where a foot stepped
    forward is mostly hidden behind the body. High at a heel strike, low at a passing."""
    import numpy as np

    height = figures[0].shape[0]
    raw = []
    for f in figures:
        feet = f[round(f.shape[0] * (1 - FEET_SHARE)):, :, 3] > 128
        ys, xs = np.nonzero(feet)
        spread = np.sqrt(xs.var() + ys.var()) if len(xs) else 0.0
        ax = anchor_x(f[..., 3] > 128)
        lows = [np.flatnonzero(side.any(1)) for side in (feet[:, :ax], feet[:, ax:])]
        rise = abs(int(lows[0][-1]) - int(lows[1][-1])) if all(len(l) for l in lows) else 0
        raw.append((spread + rise) / height)
    raw = np.array(raw)
    return np.convolve(np.pad(raw, 1, mode="edge"), np.ones(3) / 3, mode="valid")


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
#: first, no shot shorter than MIN_SHOT_S. Read over the frame's upper half, the head and torso of a figure laid in its
#: middle: a stride swings the legs and barely moves them, so a stride peaks under about 1.9 times the median there and
#: a cut between two like views (side to back-left) still passes 2.4.
CUT_FACTOR, MIN_SHOT_S = 2.0, 0.6
#: How long one orbit shot's cycle may last: a shot asked for two cycles is never cut to both at once.
ORBIT_CYCLE_S = (0.7, 1.4)


def orbit_shots(frames, fps: float):
    """An orbit clip's shots (`raw_assets/agents/person/prompting.md`: the camera cuts round one walker, a facing a
    shot), as [start, end) frames, found by its hard cuts."""
    import numpy as np

    grey = [f[:f.shape[0] // 2].mean(-1) for f in frames]
    change = np.array([np.abs(b - a).mean() for a, b in zip(grey, grey[1:])])
    cuts = []
    for i in np.argsort(change)[::-1]:
        if change[i] <= CUT_FACTOR * np.median(change):
            break
        if all(abs(int(i) + 1 - c) >= MIN_SHOT_S * fps for c in cuts):
            cuts.append(int(i) + 1)
    cuts.sort()
    return list(zip([0] + cuts, cuts + [len(frames)]))


def cut_clip(frames, fps: float, keyed, height_m: float, picked: list[int] | None = None, shots=None,
             cycle_s: tuple[float, float] = ORBIT_CYCLE_S):
    """A look's block off an orbit clip: its last FACINGS shots front, front and left side, left side, back and left
    side, back — any shot before them a warm-up — or the shots `picked` names by number, front to back, where the clip
    went wrong in one and another holds its facing; each its best loop (`looped_cycle`) at SHEET_COLS columns, column 0
    nearest a passing, so a walker standing shows the feet together. Nothing is re-centred: each shot's frames keep
    where the clip put them about its own head-and-shoulders vertical and lowest foot, so the clip's sway and bob stay
    as drawn. Each shot is scaled once, whole, so its median figure stands as tall as the facings' median — a camera
    that drifts nearer between shots would otherwise grow her as she turns. `keyed(rgb)` is a frame off its screen as
    RGBA. `shots`, where given, are the whole clip's (`orbit_shots`) when `frames` are one tile of it. Returns (block,
    info)."""
    import math

    import numpy as np
    from PIL import Image

    shots = shots or orbit_shots(frames, fps)
    picked = picked or list(range(len(shots) - FACINGS, len(shots)))
    if len(picked) != FACINGS or min(picked) < 0 or max(picked) >= len(shots):
        raise ValueError(f"{len(shots)} shot(s) found, shots {picked} picked: a facing a shot wants {FACINGS}")
    rows, report, flags = {}, [], []
    for s, (a, b) in enumerate(shots[p] for p in picked):
        crops = [figure_of(keyed(frames[i])) for i in range(a, b)]
        picks, part = looped_cycle([c for c, _ in crops], fps, edge_share=0, cycle_s=cycle_s)
        placed = [crops[p] for p in picks]
        centre = float(np.median([o[0] + anchor_x(c[..., 3] > 128) for c, o in placed]))
        floor = max(o[1] + c.shape[0] for c, o in placed)
        facing = FACINGS - 1 - s
        rows[facing] = [(c, o[0] - centre, o[1] - floor) for c, o in placed]
        report.append({"facing": facing, "frames": [a, b], "picks": [a + p for p in picks], **part})
        flags += [f"facing {facing}: {k}" for k in part["flags"]]
    statures = {r: float(np.median([c.shape[0] for c, _, _ in row])) for r, row in rows.items()}
    stature = float(np.median(list(statures.values())))
    for r, row in rows.items():
        k = stature / statures[r]
        rows[r] = [(np.asarray(Image.fromarray(c, "RGBA").resize(
            (max(1, round(c.shape[1] * k)), max(1, round(c.shape[0] * k))), Image.LANCZOS)), dx * k, round(dy * k))
            for c, dx, dy in row]
    for item in report:
        item["scale"] = round(stature / statures[item["facing"]], 3)
    half = max(max(-dx, c.shape[1] + dx) for row in rows.values() for c, dx, _ in row)
    tall = max(-dy for row in rows.values() for _, _, dy in row)
    cw, ch = 2 * (math.ceil(half) + PAD_PX), tall + 2 * PAD_PX
    block = Image.new("RGBA", (cw * SHEET_COLS, ch * FACINGS), (0, 0, 0, 0))
    for r, row in rows.items():
        for f, (c, dx, dy) in enumerate(row):
            img = Image.fromarray(c, "RGBA")
            block.paste(img, (f * cw + cw // 2 + round(dx), r * ch + ch - PAD_PX + dy), img)
    info = {"cell": 1, "pxPerM": round(ch / height_m, 3), "frames": SHEET_COLS, "facings": FACINGS,
            "cellPx": [cw, ch], "shotsAt": [a for a, _ in shots], "fps": fps, "cycles": report}
    swaps = legs_swapped(block, info)
    flags += [f"facing {r} strides one leg only ({s:.2f} of the stance's change)" for r, s in enumerate(swaps)
              if s < SWAP_FLAG]
    return block, {**info, "legSwap": [round(s, 2) for s in swaps], "flags": flags}


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
