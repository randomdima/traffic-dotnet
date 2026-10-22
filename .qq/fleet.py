"""
Write civilian car variant files from a fleet plan, and the brief a batch's sheet is asked with.

A plan is a markdown file of `## batch <N>` sections, each one table whose columns are the class figures a car is
planned from (length and width without mirrors, overhangs from bumper to wheel centre, mass, drive and the rest).

`scaffold` turns one batch's rows into `assets/agents/car/variants/<id>/<id>.json`. Its footprint, collision box and
track are the plan's, and stand only until the car's picture is installed: what the generator drew is the car's
geometry (the owner, 2026-10-20), and `qq sprites use` writes the measured figures over these. It lays no lamps:
installing the car's zone map writes them. A car already on disk keeps its picture, what was measured off it and its
lamps; only what the plan authors (AUTHORED) is written over, so the plan stays the one source of those figures.

`brief` writes the sheet's prompt, `.tmp/gen/briefs/<frame>.txt`, and the ids it draws in order,
`<frame>.cells.json`, which remotegen keeps in the frame's record (`--cells`) so `qq sprites cut` can tell which
drawing is which. No layout picture is sent: the brief says the grid, each car's size and proportion in words. Every
car sheet is one base (BASE) and what its kind adds (NEW, or WRECKS with `--wrecks`), then the cars; the one picture
sent is the style row (`qq sprites template --style=... --style-whole`) for new cars, the batch's own cars for wrecks.
A re-roll's fault lines go at the top by hand.

`status` lists each car of a batch against the steps to the fleet (STEPS) and names the first it is missing.
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
        return brief(base, rows, a.frame, a.wrecks)
    if a.action == "status":
        return status(base, rows)
    for r in rows:
        folder = base / VARIANTS / r["id"]
        if folder.exists():
            print(f"{VARIANTS / r['id']}: on disk, {refresh(folder / (r['id'] + '.json'), r)}")
            continue
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
    parser.add_argument("action", choices=["scaffold", "brief", "status"],
                        help="scaffold: write the batch's variant files; brief: write its sheet's prompt and id list; "
                             "status: where each car of the batch stands, and its next step")
    parser.add_argument("--plan", default=".tmp/gen/fleet_plan.md", help="the plan file, inside the project")
    parser.add_argument("--batch", type=cli.counting, default=1, metavar="INT", help="which `## batch <N>` section")
    parser.add_argument("--frame", type=frame_name, default=None,
                        help="brief: the sheet's name, as remotegen's --name (lowercase, digits, underscores)")
    parser.add_argument("--wrecks", action="store_true",
                        help="brief: the batch's wrecks, drawn from its installed cars, ids <car>_broken")
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


#: Every car sheet's prompt opens with this, bodies and wrecks alike: the style, the view, the layout, the shape and the
#: background. {n} cars, {cols}×{rows} grid.
BASE = """\
One image: a sprite sheet of {n} cars for a 2D top-down city game.

Style: match the attached picture's art style exactly — outline weight, shading, highlights, level of detail, pixel size.

View: straight down from above, orthographic. No perspective, no side of the body showing, no ground, no shadow.

Layout: a {cols}×{rows} grid, the cars in the order listed, left to right and top to bottom. Each car lies \
horizontally with its nose to the right, centred in its place, with empty space round it. One shared scale for all.

Every car:
- Its length and width as listed, in that proportion, with straight flanks. Narrow cars stay narrow: leave spare room \
empty rather than draw a car wider.
- Mirror-symmetric about its long centre line, with two small side mirrors just ahead of the windscreen.
- Roof on. No tyres or wheels (the game draws them), no people, no text, logos, badges or licence plates.

Background: fully transparent outside the cars — no colour, no glow or halo, no shadow. Each car's edge ends sharply."""

#: What a sheet of new cars adds: the lamps, which the lamp cut (`qq props`) finds by the dark rim drawn round each.
NEW = """\
The cars in the picture only show the style: draw the cars listed, in their own colours.
- Lamps switched off (unlit glass, no glow). Front corner: a headlamp and, beside it, a separate amber indicator. \
Rear corner: three separate small lenses side by side, each in its own thin dark frame, as distinct as the front's — \
red tail and brake lamp outermost, amber indicator next, white reversing lamp innermost. The rear indicator is the \
same amber as the front one and as large, plainly not red. Never one merged red cluster, never a light bar across \
the car.
- Condition shows only in the paint (faded colour, rust spots, a panel in another shade); the body outline is whole."""

#: What a new car's own line ends with where its describe does not split its tail lamps already.
REAR_LENSES = "; each rear lamp with a separate amber indicator lens and a white reversing lens"

#: What a sheet of wrecks adds: the picture is the same cars intact.
WRECKS = """\
The picture shows these same cars intact, in this order. Draw each as a wreck after a hard crash, plainly the same car \
(paint, wear, roof, windows, trim):
- Nose crumpled and pushed in, bonnet buckled, one front corner smashed harder; dents and scrapes to bare metal along \
one flank; windscreen and side windows shattered, one gone; lamps smashed and dark; a bumper twisted.
- Same outline: nothing pushed out past it, no debris round the car, no fire or smoke."""


def brief(base, rows, frame, wrecks):
    """The sheet's prompt and its id list, beside each other under BRIEFS: BASE, then NEW or WRECKS, then the cars."""
    if not frame:
        cli.fail("brief takes --frame, the sheet's name")
    # As `qq sprites` lays a 3:2 sheet: rows from the sheet's proportion, never fewer than three a side.
    rows_of = max(3, math.ceil(math.sqrt(len(rows) / 1.5)))
    cols = max(3, math.ceil(len(rows) / rows_of))
    text = BASE.format(n=len(rows), cols=cols, rows=rows_of) + "\n\n" + (WRECKS if wrecks else NEW)
    text += "\n\nCars, body length × width without mirrors, and what each is:\n"
    for n, r in enumerate(rows, 1):
        length, width = sized(base, r) if wrecks else (r["lengthM"], r["widthM"])
        corners = next(words for under, words in CORNERS if r["cornerM"] / (width / 2) < under)
        # The general lamp line alone left six of nine rear clusters one red lamp (fleet100_b02-v1); said again in the
        # car's own line, most drew the amber lens (v2).
        own = "" if wrecks or "split into" in r["describe"] else REAR_LENSES
        text += (f"{n}. {length:.2f} × {width:.2f} m ({length / width:.2f}× longer than wide), {corners} — "
                 f"{r['describe']}{own}\n")
    out = base / BRIEFS / f"{frame}.txt"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(text, encoding="utf-8")
    ids = base / BRIEFS / f"{frame}.cells.json"
    ids.write_text(json.dumps([r["id"] + ("_broken" if wrecks else "") for r in rows]) + "\n", encoding="utf-8")
    print(f"{out.relative_to(base)}\n{ids.relative_to(base)}: {len(rows)} {'wrecks' if wrecks else 'cars'} in a "
          f"{cols}x{rows_of} grid")
    return 0


#: The fleet the game drives, by its variant files' paths relative to it.
FLEET = VARIANTS / "common" / "Fleet.json"
#: A car's steps in the order they are done, each with what it takes (for `status`).
STEPS = (("file", "`qq fleet scaffold`"), ("picture", "a sheet, `qq sprites cut`, `keep`, `use`"),
         ("lamps", "`qq props seed` … `install` (an agent a car)"), ("mirrored", "`qq props mirror`"),
         ("wreck", "`qq fleet brief --wrecks`, a sheet, `cut`, `keep`, `use <id>_broken-vN`"),
         ("fleet", f"listed in {FLEET}, then `--lamps` and `qq proof`"))


def status(base, rows):
    """Each car of the batch: which of STEPS it has, and the first it has not — so nothing is skipped on the way to
    the fleet (a car listed with no lamps is what `CarLampTests` refuses)."""
    listed = {Path(v).parent.name for v in json.loads((base / FLEET).read_text(encoding="utf-8"))["variants"]}
    print(f"{'id':<22} " + " ".join(f"{s:<8}" for s, _ in STEPS) + " next")
    waiting = 0
    for r in rows:
        folder = base / VARIANTS / r["id"]
        described = folder / f"{r['id']}.json"
        d = json.loads(described.read_text(encoding="utf-8")) if described.is_file() else {}
        source = folder / f"{r['id']}.src.json"
        variant = json.loads(source.read_text(encoding="utf-8"))["variant"] if source.is_file() else None
        record = {}
        if variant:
            listing = base / Path(variant).parent / "variants.jsonl"
            stem = Path(variant).stem
            record = next((json.loads(line) for line in listing.read_text(encoding="utf-8").splitlines()
                           if line.strip() and json.loads(line)["variant"] == stem), {})
        has = {"file": bool(d), "picture": bool(variant), "lamps": bool(d.get("lamps")),
               "mirrored": "lampsMirroredFrom" in record,
               "wreck": bool((d.get("wreck") or {}).get("sprite")) and (folder / d["wreck"]["sprite"]).is_file(),
               "fleet": r["id"] in listed}
        nxt = next(((s, how) for s, how in STEPS if not has[s]), None)
        waiting += nxt is not None
        print(f"{r['id']:<22} " + " ".join(f"{'yes' if has[s] else '-':<8}" for s, _ in STEPS)
              + (f" {nxt[0]}: {nxt[1]}" if nxt else " done"))
    print(f"{len(rows) - waiting} of {len(rows)} done")
    return 0


def sized(base, r):
    """A car's length and width as its installed picture measured them, which its wreck is drawn to."""
    d = json.loads((base / VARIANTS / r["id"] / f"{r['id']}.json").read_text(encoding="utf-8"))
    if not (base / VARIANTS / r["id"] / d.get("sprite", "")).is_file():
        cli.fail(f"{r['id']} has no installed picture to draw its wreck from")
    return tuple(map(float, d["collisionM"]["sizeM"]))


def authored(r):
    """The variant file's lines the plan authors, by key, in the file's layout."""
    return {
        "describe": f'"describe": "{r["describe"]}"',
        "massKg": f'"massKg": {int(r["massKg"])}',
        "frontAxleM": f'"frontAxleM": {round(r["lengthM"] / 2 - r["frontOverhangM"], 3)}',
        "rearAxleM": f'"rearAxleM": {round(-(r["lengthM"] / 2 - r["rearOverhangM"]), 3)}',
        "drivetrain": f'"drivetrain": {DRIVES[r["drive"]]}',
        "maxSteeringDeg": f'"maxSteeringDeg": {r["steerDeg"]:g}',
        "tyreFriction": f'"tyreFriction": {r["friction"]:g}',
        "cgHeightM": f'"cgHeightM": {r["cgM"]:g}',
        "frontWeightShare": f'"frontWeightShare": {r["frontShare"]:g}',
        "handling": f'"handling": {{ "maxSpeed": {r["speed"]:g}, "acceleration": {r["accel"]:g}, '
                    f'"braking": {r["brake"]:g} }}',
    }


#: One authored line of a variant file: its key, then a string, an object or a bare value up to the line's end.
AUTHORED = re.compile(r'"(\w+)":\s*("(?:[^"\\]|\\.)*"|\{[^}]*\}|[^,\n]+)')


def refresh(path: Path, r):
    """Write the plan's authored lines over a variant file on disk; what the picture measured is kept."""
    text = path.read_text(encoding="utf-8")
    lines = authored(r)
    changed = []

    def put(m):
        key = m.group(1)
        if key not in lines or m.group(0) == lines[key]:
            return m.group(0)
        changed.append(key)
        return lines[key]

    text = AUTHORED.sub(put, text)
    if missing := [k for k in lines if f'"{k}"' not in text]:
        cli.fail(f"{path.name} has no {', '.join(missing)} line to write the plan's over")
    d = json.loads(text)
    if {"frontAxleM", "rearAxleM"} & set(changed) and (path.parent / d.get("sprite", "")).is_file():
        # CAR-12: the track is the width over the axles, so axles moved on a drawn car are measured over again.
        track = measured_track(path.parent / d["sprite"], d)
        if track != d["trackM"]:
            text = re.sub(r'"trackM":\s*[0-9.]+', f'"trackM": {track}', text, count=1)
            changed.append(f"trackM {d['trackM']}→{track} (measured)")
    length = d["collisionM"]["sizeM"][0]
    if abs(length - r["lengthM"]) > 0.005:
        # The picture was cut to the file's length: a new one is a new cut, never a figure written here.
        print(f"  {r['id']}: drawn at {length} m, the plan says {r['lengthM']} m — cut it again to change that")
    if changed:
        path.write_text(text, encoding="utf-8")
    return f"plan's {', '.join(changed)} written over" if changed else "already the plan's"


def measured_track(sprite: Path, d):
    """The track over a drawn car's axles, measured off its installed sprite as `qq sprites use` measures a cut."""
    import numpy as np
    from PIL import Image

    import _outline

    solid = np.asarray(Image.open(sprite).convert("RGBA"))[..., 3] >= 128
    body = _outline.body_in(solid, (0, 0, solid.shape[1], solid.shape[0]))
    m = _outline.measure(body, float(d["collisionM"]["sizeM"][0]), (float(d["frontAxleM"]), float(d["rearAxleM"])))
    return round(max(m["axleWidthsM"]) + 2 * TYRE_PROUD_M, 3)


def variant_text(r):
    """The variant file, in the hand-written files' layout: one field a line, short arrays inline."""
    length, width = r["lengthM"], r["widthM"]
    a = authored(r)
    return f"""{{
  "id": "{r['id']}",
  "sprite": "{r['id']}.webp",
  {a['describe']},
  "footprintM": [{length:.2f}, {width + 2 * MIRROR_M:.2f}],
  "collisionM": {{ "sizeM": [{length:.2f}, {width:.2f}], "cornerRadiusM": {r['cornerM']} }},
  {a['massKg']},
  {a['frontAxleM']},
  {a['rearAxleM']},
  "trackM": {round(width + 2 * TYRE_PROUD_M, 3)},
  {a['drivetrain']},
  {a['maxSteeringDeg']},
  {a['tyreFriction']},
  {a['cgHeightM']},
  {a['frontWeightShare']},
  {a['handling']},
  "lamps": []
}}
"""


if __name__ == "__main__":
    cli.entry(main)
