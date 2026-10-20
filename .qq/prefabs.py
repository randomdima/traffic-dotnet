"""
Report or rescale the building catalogue's footprints, look by look.

A footprint is an authored length (CLAUDE.md, "figures flow one way"), so a resize is a rewrite of the variant files
themselves, not a factor kept beside them. `sizes` is read-only: each look's count and footprint quartiles, beside a
3.95 m car. `scale LOOK --by=F` rewrites every prefab of that look (or the three civic roofs, `civic`): footprint and
parts by F, rounded to 0.1 m; a rounded corner (over EAVES_MAX_M) scales with it and an eave's radius stays.
It is not idempotent — run once per look and check the table; git holds the undo.
"""

from __future__ import annotations

import argparse
import json
import re
import struct
from pathlib import Path

from common import cli, paths

PREFABS = Path("assets/world/building/prefabs")
VARIANTS = Path("assets/world/building/variants")
CIVIC = ("hospital", "police_station", "repair_shop")
LOOKS = ("house", "apartments", "tower", "garages", "shed", "kiosk", "retail", "office", "industrial", "school",
         "hospital", "religious", "canopy", "greenhouse")
#: The largest radius a square-cornered prefab is rounded at, at its eaves (citygen decision log, 2026-10-04).
EAVES_MAX_M = 0.8
CAR_M = (3.95, 1.72)


def main():
    a = parse_args()
    base = paths.project_root()
    if a.action == "sizes":
        return sizes(base)
    if a.action == "sheets":
        return sheets(base, a.out, a.left)
    if a.action == "brief":
        return brief(base, a.frame, a.out)
    if not a.target:
        cli.fail("scale names a look or `civic`")
    if a.by is None:
        cli.fail("scale needs --by")
    files = targets(base, a.target)
    for f in files:
        rescale(f, a.by)
    print(f"{a.target}: {len(files)} files x{a.by:g}")
    return sizes(base)


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq prefabs",
        description="Report or rescale the building catalogue's footprints, look by look.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("action", choices=["sizes", "scale", "sheets", "brief"],
                        help="sizes: the table; scale: rewrite one look's files; sheets: the frames to ask the roofs in; "
                             "brief: write one planned frame's prompt")
    parser.add_argument("target", nargs="?", choices=[*LOOKS, "civic"], help="scale: the prefab look, or the civic roofs")
    parser.add_argument("--by", type=factor, default=None, help="scale: the factor, 0.5 to 3")
    parser.add_argument("--frame", type=frame_name, default=None, help="brief: the frame's name in the sheets plan")
    parser.add_argument("--left", action="store_true",
                        help="sheets: plan only the roofs whose sprite traces to no prompt yet, as redo_ frames")
    parser.add_argument("--out", default=".tmp/gen/sheets.md",
                        help="sheets: where the plan is written; brief: the plan read (the brief goes to .tmp/gen/briefs/)")
    return parser.parse_args()


def frame_name(text):
    if not re.fullmatch(r"(roofs|redo)_[a-z]+_[a-z]+", text):
        raise argparse.ArgumentTypeError(f"not a planned frame: {text!r}")
    return text


def factor(text):
    v = float(text)
    if not 0.5 <= v <= 3.0:
        raise argparse.ArgumentTypeError("a factor is between 0.5 and 3")
    return v


def targets(base, target):
    if target == "civic":
        return [base / VARIANTS / i / f"{i}.json" for i in CIVIC]
    return sorted(f for f in variant_files(base / PREFABS)
                  if json.loads(f.read_text(encoding="utf-8")).get("look", "").lower() == target)


def variant_files(folder):
    """The variant files one folder down: not the `.src.json` beside each installed sprite, nor a catalogue list."""
    return [f for f in folder.glob("*/*.json") if not f.name.endswith(".src.json") and f.parent.name != "common"]


def rescale(path, by):
    d = json.loads(path.read_text(encoding="utf-8"))
    d["footprintM"] = [m(v * by) for v in d["footprintM"]]
    if d.get("cornerRadiusM", 0.0) > EAVES_MAX_M:
        d["cornerRadiusM"] = round(d["cornerRadiusM"] * by, 2)
    for p in d.get("partsM", []):
        p["atM"] = [round(v * by, 2) for v in p["atM"]]
        p["sizeM"] = [m(v * by) for v in p["sizeM"]]
    text = json.dumps(d, indent=2, ensure_ascii=False)
    if path.is_relative_to(base_of(path) / VARIANTS):
        # The variant files are hand-laid, a pair and a part to a line.
        text = NUMBER_PAIR.sub(lambda g: f"[{g[1]}, {g[2]}]", text)
        text = PART.sub(lambda g: f'{{ "atM": {g[1]}, "sizeM": {g[2]} }}', text)
    path.write_text(text + "\n", encoding="utf-8")


NUMBER_PAIR = re.compile(r"\[\s*(-?[\d.]+),\s*(-?[\d.]+)\s*\]")
PART = re.compile(r'\{\s*"atM": (\[[^\]]*\]),\s*"sizeM": (\[[^\]]*\])\s*\}')


def base_of(path):
    return next(p for p in path.parents if (p / "assets").is_dir())


def m(v):
    return round(v, 1)


def sizes(base):
    by = {}
    for f in variant_files(base / PREFABS):
        d = json.loads(f.read_text(encoding="utf-8"))
        by.setdefault(d["look"].lower(), []).append(d["footprintM"])
    by["civic"] = [json.loads(f.read_text(encoding="utf-8"))["footprintM"] for f in targets(base, "civic")]
    print(f"car {CAR_M[0]} x {CAR_M[1]} m")
    print(f"{'look':<11} {'n':>3}  {'w p25/50/75 m':>17}  {'d p25/50/75 m':>17}  {'median m2':>9}")
    for look in [*LOOKS, "civic"]:
        v = by.get(look, [])
        if not v:
            continue
        w, d, a = sorted(x[0] for x in v), sorted(x[1] for x in v), sorted(x[0] * x[1] for x in v)
        print(f"{look:<11} {len(v):>3}  {q(w):>17}  {q(d):>17}  {a[len(a) // 2]:>9.0f}")
    return 0


def q(v):
    return "/".join(f"{v[int(len(v) * p)]:g}" for p in (0.25, 0.5, 0.75))


#: `qq sprites template`'s frame and the magenta it keeps round each outline, and the art grid (`ArtPixelsPerMetre`).
SHEET_PX, GAP_PX, GRID = (1536, 1024), 24, 31.5
#: And the wider margin it keeps round a frame of fewer than nine (`BIG_ROOF_GAP_PX`).
BIG_GAP_PX = 64
#: A frame of big roofs is made as large as still draws each at this share of the grid, else two to a frame.
BIG_SHARE = 0.8


def sheets(base, out, left):
    """Each look's roofs, smallest first, in frames of sixteen or nine while those hold the grid, then the big ones as
    many to a frame as still draw them near it (`qq sprites` BIG_ROOF_MIN_CELLS). A look's tail too few for a frame
    is pooled with the other looks' tails, by size. `left`: only the roofs whose sprite traces to nothing yet, in
    frames named `redo_…`, so no name is asked a fourth time."""
    groups = {}
    for f in [*variant_files(base / PREFABS), *variant_files(base / VARIANTS)]:
        d = json.loads(f.read_text(encoding="utf-8"))
        if "footprintM" not in d or (left and f.with_name(f.stem + ".src.json").is_file()):
            continue
        look = d["look"].lower() if "look" in d else "civic" if d["id"] in CIVIC else "ordinary"
        groups.setdefault(look, []).append((d["id"], d["footprintM"]))
    prefix = "redo" if left else "roofs"
    frames, pool = [], []
    for look in sorted(groups):
        tail = plan(prefix, look, groups[look], frames)
        pool += tail
    if pool:
        if leftover := plan(prefix, "mixed", pool, frames):
            # Too few for a frame of their own: the last frame takes them, as many as a 4x4 holds.
            name, ids, _ = frames.pop()
            ids = ids + leftover
            frames.append((name, ids, scale_of(ids, *grid_for(len(ids))) if len(ids) >= 9 else few_scale(ids)))
    lines = ["# Roof frames", "", f"{len(frames)} frames, {sum(len(f[1]) for f in frames)} roofs.", "",
             "| frame | cells | px/m | ids |", "|---|---|---|---|"]
    lines += [f"| {name} | {len(ids)} | {s:.1f} | {' '.join(i for i, _ in ids)} |" for name, ids, s in frames]
    target = paths.resolve_in_root(out, base)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text("\n".join(lines) + "\n", encoding="utf-8")
    under = sum(1 for _, ids, s in frames if s < GRID for _ in ids)
    print(f"{len(frames)} frames, {under} roofs under the grid -> {paths.display(target)}")
    return 0


def plan(prefix, look, roofs, frames):
    """Lays one group's frames into `frames`; returns what is left too few for one."""
    rest = sorted(roofs, key=lambda r: -scale_of([r], 3, 3))
    letter = 0

    def add(ids, s):
        nonlocal letter
        frames.append((f"{prefix}_{look}_{letters(letter)}", ids, s))
        letter += 1

    while len(rest) >= 2:
        if len(rest) >= 16 and scale_of(rest[:16], 4, 4) >= GRID:
            k = 16
        elif len(rest) >= 9 and scale_of(rest[:9], 3, 3) >= GRID * BIG_SHARE:
            k = 9
        else:
            k = next((k for k in (8, 6, 4, 3) if len(rest) >= k
                      and few_scale(rest[:k]) >= GRID * BIG_SHARE), 2)
            if any(scale_of([r], 3, 3) >= GRID for r in rest[:k]):
                # A small roof may not go fewer than nine to a frame: nine if there are, else the pool takes them.
                if len(rest) < 9:
                    break
                k = 9
        add(rest[:k], scale_of(rest[:k], *grid_for(k)) if k >= 9 else few_scale(rest[:k]))
        rest = rest[k:]
    return rest


def letters(n):
    """a, b, … z, aa, ab, …"""
    return chr(ord("a") + n) if n < 26 else letters(n // 26 - 1) + chr(ord("a") + n % 26)


def grid_for(k):
    return (4, 4) if k == 16 else (3, 3)


#: What each look's details are, at their true size: the scale a roof is drawn to beside a car (the owner, 2026-10-20).
DETAILS = {
    "house": "a brick chimney about 0.6 × 0.6 m; roof tile courses or corrugations about 0.25 m apart, fine and close; "
             "a skylight about 0.8 × 1 m; a dormer about 1.5–2 m wide; a porch roof about 1.5–2 m deep",
    "apartments": "a chimney or vent stack about 0.6 m; a roof hatch about 1 m; a stairwell exit box about 2.5 × 3 m; "
                  "a TV antenna about 2 m across; a parapet about 0.3 m thick; tile courses or sheet ribs about 0.25 m apart",
    "tower": "a lift machine room about 3 × 5 m; a hatch or vent shaft about 1 m; a TV antenna about 2 m across; "
             "a parapet about 0.4 m thick; drain funnels about 0.4 m",
    "garages": "each lock-up is about 3 m wide with a 2.5 m door on the street edge; a parapet between units about 0.3 m",
    "shed": "corrugations or boards about 0.15 m apart; nothing on the roof bigger than a 0.5 m vent",
    "kiosk": "an air conditioner unit about 0.8 × 0.5 m; a signboard strip about 0.5 m deep along the front, plain colour",
    "retail": "an air conditioner unit about 1 × 1 m; a vent about 0.5 m; a plain sign strip about 0.6 m deep along "
              "the front; a parapet about 0.3 m thick",
    "office": "a plant room about 3 × 4 m; an air conditioner unit about 1–1.5 m; a chiller about 2 × 4 m; "
              "a parapet about 0.4 m thick; a roof hatch about 1 m",
    "industrial": "roof sheet ribs about 0.2 m apart; a skylight strip about 1.5 m wide; a roof vent about 1 m; "
                  "a loading door about 4 m wide on the street edge",
    "school": "an air conditioner unit about 1 m; a skylight about 1.5 m; a parapet about 0.4 m thick; "
              "tile courses about 0.25 m apart on pitched wings",
    "hospital": "a plant room about 3 × 4 m; an air conditioner unit about 1–1.5 m; a parapet about 0.4 m thick",
    "religious": "a small dome or cupola 3–6 m across; a bell tower about 4 m square; tile or metal courses about 0.25 m apart",
    "canopy": "sheet ribs about 0.2 m apart; translucent panels about 1 m wide",
    "greenhouse": "glass panes about 1 × 0.6 m in a fine metal grid; a ridge line every 3–4 m",
    "ordinary": "a chimney about 0.6 m; an air conditioner unit about 1 m; a skylight about 1 m; a parapet about 0.3 m; "
                "tile courses about 0.25 m apart",
    "civic": "a plant room about 3 × 4 m; an air conditioner unit about 1–1.5 m; a skylight about 1.5 m; "
             "a parapet about 0.4 m thick",
}


def brief(base, frame, plan_path):
    """The prompt for one frame of the sheets plan: the house rules, its looks' true detail sizes, its cells in order."""
    if not frame:
        cli.fail("brief needs --frame")
    row = next((r for r in paths.resolve_in_root(plan_path, base).read_text(encoding="utf-8").splitlines()
                if r.startswith(f"| {frame} |")), None)
    if row is None:
        cli.fail(f"{frame} is not in {plan_path}: run `qq prefabs sheets` first")
    ids = row.split("|")[4].split()
    files = {}
    for f in [*variant_files(base / PREFABS), *variant_files(base / VARIANTS)]:
        d = json.loads(f.read_text(encoding="utf-8"))
        if d.get("id") in ids:
            files[d["id"]] = d
    cols, rows = grid_for(len(ids)) if len(ids) >= 9 else best_grid([(i, files[i]["footprintM"]) for i in ids])
    looks = list(dict.fromkeys(look_of(files[i]) for i in ids))
    text = [
        f"Two images are attached. The first is a layout: {len(ids)} building outlines seen from above, in a {cols}×{rows} "
        "grid on a flat magenta background. The second is finished art from the same game, to match in style only — not "
        "its layout, shapes, colours or the size of its details.",
        "",
        f"Make one image with exactly the same {cols}×{rows} layout as the first image. Inside each outline, draw the roof "
        "of the building described for that cell below, as a top-down sprite for a 2D city game.",
        "",
        "Shape comes first. Before painting a cell, trace its outline exactly, then paint only inside the traced border:",
        "- Trace each outline like paint on tracing paper laid over it: do not re-centre, move or resize it, and leave "
        "the margins between outlines and around the picture's edge exactly as wide as they are, empty.",
        "- Keep every outline's proportions: match each one's width and height. Each cell's proportion is given below; "
        "check it. Never make an outline squarer, shorter or fatter.",
        "- Every edge is straight and square to the others. Never slant, skew or taper an edge to show perspective. An "
        "outline built of several rectangles (an L, a T, a stepped block) is followed corner by corner.",
        "- Draw nothing outside the outline: no eaves, steps, porches, canopies, ramps or shadows past it. A porch or an "
        "entrance canopy is drawn inside the outline at its bottom edge. A roof is solid paint right up to its outline: "
        "no hole, no gap and no magenta inside it.",
        "",
        "Scale is the second rule, and it is strict. Every size below is true metres, width × depth, and every detail is "
        "drawn at its true size against them. These buildings stand beside cars 4 m long and 1.8 m wide; keep that in "
        "mind for every detail. True sizes:",
        *[f"- {look}: {DETAILS[look]}." for look in looks],
        "- No detail is bigger than its true size. When in doubt, draw it smaller and keep fewer of them. A large roof "
        "has many small details, never a few huge ones.",
        "",
        "Rules for every cell:",
        "- Look straight down (orthographic). No perspective, no tilt, no ground, no plants, no cars, no people. No wall, "
        "door, window, garage door or shop front is ever seen: stairwells, lift rooms and dormers are flat-topped boxes "
        "seen only from above, a dome is a circle shaded as a dome, a light well is only its flat floor.",
        "- The thick bottom edge of each outline is the street front, where the entrance is. The top edge is the back.",
        "- A gabled roof shows its ridge as one line with the two slopes shaded differently; a lean-to roof is one slope "
        "with no ridge; a flat roof has a parapet round its edge.",
        "- Style: match the style image. Clean, flat-shaded cartoon game art, bold dark outlines, muted natural colours, "
        "simple readable details, soft light from the top-left.",
        "- Outside the roofs the picture is fully transparent: no magenta, no background colour, no shadow, nothing "
        "half-transparent. No text, letters, numbers, labels, logos, grid lines or frames anywhere.",
        "",
        "Cells, left to right, top to bottom:",
    ]
    if (empty := cols * rows - len(ids)) > 0:
        text.insert(-1, ("The grid's last place is" if empty == 1 else f"The grid's last {empty} places are")
                        + " empty: fully transparent, with nothing drawn there.")
        text.insert(-1, "")
    spans = laid_spans(base / ".tmp/gen/templates" / f"{frame}.png", ids)
    for r in range(rows):
        cells = ids[r * cols:(r + 1) * cols]
        if not cells:
            break
        text += ["", f"Row {r + 1}"]
        text += [f"{r * cols + k + 1}. {cell_line(files[i])}{spans.get(i, '')}" for k, i in enumerate(cells)]
    out = base / ".tmp/gen/briefs" / f"{frame}.txt"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(text) + "\n", encoding="utf-8")
    print(f"{frame}: {len(ids)} cells, {cols}x{rows} -> {paths.display(out)}")
    print(" ".join(ids))
    return 0


def laid_spans(template, ids):
    """Per id, where `qq sprites template` laid its outline, as shares of the picture — the placement wording that held
    big roofs where pixel boxes and plain fault lines did not (roofs_mixed_at, _j, _an). Empty without a template."""
    if not template.is_file():
        return {}
    data, at, layout = template.read_bytes(), 8, None
    while at < len(data):
        size, kind = struct.unpack(">I4s", data[at:at + 8])
        if kind == b"tEXt":
            key, _, value = data[at + 8:at + 8 + size].partition(b"\0")
            if key == b"cells":
                layout = json.loads(value)
        at += size + 12
    if layout is None or [c["id"] for c in layout["cells"]] != ids:
        cli.fail(f"{paths.display(template)} does not lay this frame's cells: run `qq sprites template` with its ids")
    width, height = layout["size"]
    return {c["id"]: f"; its outline spans {100 * c['shapeBox'][0] / width:.0f}–"
                     f"{100 * (c['shapeBox'][0] + c['shapeBox'][2]) / width:.0f} % of the picture's width and "
                     f"{100 * c['shapeBox'][1] / height:.0f}–{100 * (c['shapeBox'][1] + c['shapeBox'][3]) / height:.0f} % "
                     "of its height" for c in layout["cells"]}


def look_of(d):
    if d["id"] in CIVIC:
        return "civic"
    return d["look"].lower() if "look" in d else "ordinary"


def cell_line(d):
    w, h = d["footprintM"]
    parts = d.get("partsM") or []
    if parts:
        w = max(p["atM"][0] + p["sizeM"][0] / 2 for p in parts) - min(p["atM"][0] - p["sizeM"][0] / 2 for p in parts)
        h = max(p["atM"][1] + p["sizeM"][1] / 2 for p in parts) - min(p["atM"][1] - p["sizeM"][1] / 2 for p in parts)
    k = w / h
    ratio = (f"{k:.1f}× wider than deep" if k >= 1.05 else f"{1 / k:.1f}× deeper than wide" if k <= 0.95 else "square")
    if k >= 4 or k <= 0.25:
        # Below that, "a thin strip" drew roofs thinner than their outlines (roofs_mixed_p).
        ratio += ", a long strip"
    r = d.get("cornerRadiusM", 0.0)
    if r > EAVES_MAX_M:
        ratio += ", corners widely rounded" if r < min(w, h) / 2 - 0.05 else ", round ends"
    if len(parts) > 1:
        ratio += f", outline built of {len(parts)} rectangles"
    return f"{round(w, 1):g} × {round(h, 1):g} m ({ratio}) — {d.get('describe', d['id'].replace('_', ' '))}"


def best_grid(roofs):
    n = len(roofs)
    return max(((c, -(-n // c)) for c in range(1, n + 1)), key=lambda g: scale_of(roofs, *g, BIG_GAP_PX))


def few_scale(roofs):
    """A frame of fewer than nine, as `qq sprites template` lays it."""
    return scale_of(roofs, *best_grid(roofs), BIG_GAP_PX)


def scale_of(roofs, cols, rows, gap=GAP_PX):
    cw, ch = SHEET_PX[0] / cols - 2 * gap, SHEET_PX[1] / rows - 2 * gap
    return min(min(cw / w, ch / d) for _, (w, d) in roofs)


if __name__ == "__main__":
    cli.entry(main)
