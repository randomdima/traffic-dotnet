"""
Write civilian car variant files from a fleet plan, and the brief a batch's sheet is asked with.

A plan is a markdown file of `## batch <N>` sections, each one table whose columns are the class figures a car is
planned from (length and width without mirrors, overhangs from bumper to wheel centre, mass, drive and the rest).

`scaffold` turns one batch's rows into `assets/agents/car/variants/<id>/<id>.json` and refuses an id already on disk,
so it never overwrites a car. Its footprint, collision box and track are the plan's, and stand only until the car's
picture is installed: what the generator drew is the car's geometry (the owner, 2026-10-20), and `qq sprites use`
writes the measured figures over these. It lays no lamps: installing the car's zone map writes them.

`brief` writes the sheet's prompt, `.tmp/gen/briefs/<frame>.txt`, and the ids it draws in order,
`<frame>.cells.json`, which remotegen keeps in the frame's record (`--cells`) so `qq sprites cut` can tell which
drawing is which. No layout picture is sent: the brief says the grid, each car's size and proportion in words.
"""

from __future__ import annotations

import argparse
import json
import math
import re
from pathlib import Path

from common import cli, paths

VARIANTS = Path("assets/agents/car/variants")
BRIEFS = Path(".tmp/gen/briefs")
COLUMNS = ("id", "describe", "lengthM", "widthM", "cornerM", "massKg", "frontOverhangM", "rearOverhangM", "drive",
           "steerDeg", "friction", "cgM", "frontShare", "speed", "accel", "brake")
DRIVES = {"R": 0, "F": 1, "A": 2}
#: What the plan's footprint adds to the body each side, for the mirrors, until the picture's own is measured.
MIRROR_M = 0.1
#: How far each wheel centre stands outside the flank: CAR-12 owes a share of every tyre past the bodywork.
TYRE_PROUD_M = 0.03
#: A corner's words by its radius as a share of the half-width, the first one it is under.
CORNERS = ((0.25, "nearly square corners"), (0.4, "gently rounded corners"), (0.6, "rounded corners"),
           (math.inf, "very round corners"))
ROW = re.compile(r"^\|(.+)\|\s*$")
FRAME = re.compile(r"^[a-z0-9_]+$")


def main():
    a = parse_args()
    base = paths.project_root()
    rows = batch_rows(paths.resolve_in_root(a.plan, base), a.batch)
    if a.action == "brief":
        return brief(base, rows, a.frame, a.style)
    taken = [r["id"] for r in rows if (base / VARIANTS / r["id"]).exists()]
    if taken:
        cli.fail(f"already on disk, not overwritten: {', '.join(taken)}")
    for r in rows:
        folder = base / VARIANTS / r["id"]
        folder.mkdir(parents=True)
        (folder / f"{r['id']}.json").write_text(variant_text(r), encoding="utf-8")
        print(f"{VARIANTS / r['id'] / (r['id'] + '.json')}")
    return 0


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq fleet",
        description="Write civilian car variant files from a fleet plan, and the brief a batch's sheet is asked with.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("action", choices=["scaffold", "brief"],
                        help="scaffold: write the batch's variant files; brief: write its sheet's prompt and id list")
    parser.add_argument("--plan", default=".tmp/gen/fleet_plan.md", help="the plan file, inside the project")
    parser.add_argument("--batch", type=cli.counting, default=1, metavar="INT", help="which `## batch <N>` section")
    parser.add_argument("--frame", type=frame_name, default=None,
                        help="brief: the sheet's name, as remotegen's --name (lowercase, digits, underscores)")
    parser.add_argument("--style", action="store_true",
                        help="brief: a style picture is sent as image 1, and the brief says to match it")
    return parser.parse_args()


def frame_name(text):
    if not FRAME.fullmatch(text):
        raise argparse.ArgumentTypeError(f"not a frame name: {text!r}")
    return text


def batch_rows(plan: Path, batch: int):
    """The rows of the plan's `## batch <batch>` table, each a dict by COLUMNS, numbers parsed."""
    rows, inside, header = [], False, None
    for line in plan.read_text(encoding="utf-8").splitlines():
        if line.startswith("## "):
            inside = line.strip() == f"## batch {batch}"
            continue
        m = ROW.match(line) if inside else None
        if not m:
            continue
        cells = [c.strip() for c in m.group(1).split("|")]
        if header is None:
            header = cells
            if tuple(header) != COLUMNS:
                cli.fail(f"{plan.name} batch {batch}: columns are {header}, expected {list(COLUMNS)}")
            continue
        if set(cells[0]) <= {"-"}:
            continue
        r = dict(zip(header, cells))
        for k in COLUMNS[2:]:
            if k != "drive":
                r[k] = float(r[k])
        if r["drive"] not in DRIVES:
            cli.fail(f"{r['id']}: drive {r['drive']} is none of {', '.join(DRIVES)}")
        rows.append(r)
    if not rows:
        cli.fail(f"{plan.name} has no rows under `## batch {batch}`")
    return rows


def brief(base, rows, frame, style):
    """The sheet's prompt and its id list, beside each other under BRIEFS."""
    if not frame:
        cli.fail("brief takes --frame, the sheet's name")
    # As `qq sprites` lays a 3:2 sheet: rows from the sheet's proportion, never fewer than three a side.
    rows_of = max(3, math.ceil(math.sqrt(len(rows) / 1.5)))
    grid = (max(3, math.ceil(len(rows) / rows_of)), rows_of)
    lines = [
        f"One image: a sprite sheet of {len(rows)} different cars for a 2D top-down city game, every car seen straight "
        "down from above (orthographic), on a fully transparent background.",
    ]
    if style:
        lines += ["", "The attached image shows finished cars from the same game. Match its art style exactly — outline "
                      "weight, shading, highlights, level of detail, pixel size — but not its cars or their colours."]
    lines += [
        "",
        f"Layout: a {grid[0]}×{grid[1]} grid, the cars in the order listed below, left to right and top to bottom. "
        "Every car lies horizontally with its nose pointing right, centred in its place in the grid, with clear empty "
        "space between neighbours and round the picture's edge. All cars are drawn on one shared scale: a car listed "
        "as 4.5 m long is half as long again as one listed as 3 m.",
        "",
        "Every car:",
        "- Its length and width as listed, and exactly the proportion given: how many times longer than wide it is. "
        "Seen from above its body is a rounded rectangle with straight flanks.",
        "- Each is a believable everyday car of a real kind, as seen in an Eastern European city today.",
        "- No tyres or wheels visible: the game draws them under the body. Roof on: no convertible, no open top, no "
        "seats or people. Mirror-symmetric about its long centre line. Two small side mirrors just ahead of the "
        "windscreen.",
        "- Every lamp a real car has, all switched off — unlit glass with one small pale glint, no glow, no light cast: "
        "a headlamp at each front corner with an amber indicator at its outer end; at each rear corner a rear lamp "
        "cluster holding a red tail and brake lamp, an amber indicator and a white reversing lamp. Draw them in the "
        "shapes this kind of car has.",
        "- Bonnet, roof and boot read as separate panels, divided by the windscreen and the rear window.",
        "- No text, logos, brand badges, licence plates, numbers or watermark anywhere.",
        "- Outside the cars the picture is fully transparent: no background colour, no ground, no shadow, nothing "
        "half-transparent.",
    ]
    if not style:
        lines += ["- Style: pixel art with crisp hard-edged pixels and no blur, a dark near-black outline about 2 pixels "
                  "thick, flat colour with one highlight and one shadow tone, soft light from the top-left, glass dark "
                  "slate blue with one pale glint."]
    lines += ["", "Cars, body length × width (not counting the mirrors):"]
    for n, r in enumerate(rows, 1):
        h = r["widthM"] / 2
        corners = next(words for under, words in CORNERS if r["cornerM"] / h < under)
        lines.append(f"{n}. {r['lengthM']:.2f} × {r['widthM']:.2f} m ({r['lengthM'] / r['widthM']:.2f}× longer than "
                     f"wide), {corners} — {r['describe']}")
    out = base / BRIEFS / f"{frame}.txt"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    ids = base / BRIEFS / f"{frame}.cells.json"
    ids.write_text(json.dumps([r["id"] for r in rows]) + "\n", encoding="utf-8")
    print(f"{out.relative_to(base)}\n{ids.relative_to(base)}: {len(rows)} cars in a {grid[0]}x{grid[1]} grid")
    return 0


def variant_text(r):
    """The variant file, in the hand-written files' layout: one field a line, short arrays inline."""
    length, width = r["lengthM"], r["widthM"]
    front_axle = round(length / 2 - r["frontOverhangM"], 3)
    rear_axle = round(-(length / 2 - r["rearOverhangM"]), 3)
    return f"""{{
  "id": "{r['id']}",
  "sprite": "{r['id']}.webp",
  "describe": "{r['describe']}",
  "footprintM": [{length:.2f}, {width + 2 * MIRROR_M:.2f}],
  "collisionM": {{ "sizeM": [{length:.2f}, {width:.2f}], "cornerRadiusM": {r['cornerM']} }},
  "massKg": {int(r['massKg'])},
  "frontAxleM": {front_axle},
  "rearAxleM": {rear_axle},
  "trackM": {round(width + 2 * TYRE_PROUD_M, 3)},
  "drivetrain": {DRIVES[r['drive']]},
  "maxSteeringDeg": {r['steerDeg']:g},
  "tyreFriction": {r['friction']:g},
  "cgHeightM": {r['cgM']:g},
  "frontWeightShare": {r['frontShare']:g},
  "handling": {{ "maxSpeed": {r['speed']:g}, "acceleration": {r['accel']:g}, "braking": {r['brake']:g} }},
  "lamps": []
}}
"""


if __name__ == "__main__":
    cli.entry(main)
