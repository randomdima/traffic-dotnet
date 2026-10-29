"""
Write civilian car variant files from a fleet plan, and the brief a batch's sheet is asked with.

A plan is a markdown file of `## batch <N>` sections, each one table whose columns are the class figures a car is
planned from (length and width without mirrors, overhangs from bumper to wheel centre, mass, drive and the rest).

`scaffold` turns one batch's rows into `assets/agents/car/variants/<id>/<id>.json`. Its footprint, collision box and
track are the plan's, and stand only until the car's picture is installed: what the generator drew is the car's
geometry (the owner, 2026-10-20), and `qq sprites use` writes the measured figures over these. It lays no lamps:
`qq props install` writes them. A car already on disk keeps its picture, what was measured off it and its
lamps; only what the plan authors (AUTHORED) is written over, so the plan stays the one source of those figures.

`brief` writes the sheet's prompt, `.tmp/gen/briefs/<frame>.txt`, and the ids it draws in order,
`<frame>.cells.json`, which remotegen keeps in the frame's record (`--cells`) so `qq sprites cut` can tell which
drawing is which. No layout picture is sent: the brief says the grid, each car's size and proportion in words. Every
car sheet is one base (BASE) and what its kind adds (NEW, or WRECKS with `--wrecks`), then the cars; the one picture
sent is, for new cars, the style picture `brief` writes beside the templates (`style_cars`: fleet cars spanning the
batch's lengths, of differing lamp designs, at one scale; `--style` names them instead), and for wrecks the batch's
own cars (`qq sprites style ...`).
A re-roll's fault lines go at the top by hand. A wreck sheet draws only the batch's cars with a picture installed, and
`--also` fills it to remotegen's nine with installed cars of other batches, read from their variant files.

`status` lists each car of a batch against the steps to the fleet (STEPS) and names the first it is missing.

`track` measures the track of any installed car off its picture (CAR-12), service ones included; `scaffold` does the
same for a batch's drawn cars. `wheels` lays each car's flanks magnified with its axles ticked, against the tyre shadow
the art shades under the sill: an axle that disagrees is moved in the plan (its overhang), never in the file.
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
#: The fewest assets remotegen asks a sheet for (.claude/skills/remotegen/SKILL.md).
SHEET_LEAST = 9
#: What the plan's footprint adds to the body each side, for the mirrors, until the picture's own is measured.
MIRROR_M = 0.1
#: A corner's words by its radius as a share of the half-width, the first one it is under.
CORNERS = ((0.25, "nearly square corners"), (0.4, "gently rounded corners"), (0.6, "rounded corners"),
           (math.inf, "very round corners"))
ROW = re.compile(r"^\|(.+)\|\s*$")
FRAME = re.compile(r"^[a-z0-9_]+$")

def main():
    a = parse_args()
    base = paths.project_root()
    if a.action == "track":
        return retrack(base, a.also)
    if a.action == "wheels":
        return wheel_sheet(base, a.also or fleet_ids(base), paths.resolve_in_root(a.out, base))
    plan = paths.resolve_in_root(a.plan, base)
    rows = cars_rows(plan, a.cars) if a.cars else batch_rows(plan, a.batch)
    if a.action == "brief":
        if a.also and not a.wrecks:
            cli.fail("--also is for a wreck sheet (--wrecks)")
        if a.wrecks:
            rows = wreckable(base, rows) + [installed_row(base, i) for i in a.also]
        brief(base, rows, a.frame, a.wrecks)
        if not a.wrecks:
            style_picture(base, rows, a.frame, a.style)
        return 0
    if a.action == "status":
        return status(base, rows)
    for r in rows:
        folder = base / VARIANTS / r["id"]
        if (folder / f"{r['id']}.json").exists():
            print(f"{VARIANTS / r['id']}: on disk, {refresh(folder / (r['id'] + '.json'), r)}")
            continue
        folder.mkdir(parents=True, exist_ok=True)
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
    parser.add_argument("action", choices=["scaffold", "brief", "status", "track", "wheels"],
                        help="scaffold: write the batch's variant files; brief: write its sheet's prompt and id list; "
                             "status: where each car of the batch stands, and its next step; track: measure the "
                             "track of the installed cars --also names (any car, service ones too) off their pictures; "
                             "wheels: a review sheet of those cars, or the whole fleet, each flank's edge magnified "
                             "with its axles ticked, against the tyre shadow the art draws under the sill")
    parser.add_argument("--out", default=".tmp/gen/wheels.png", help="wheels: where the review sheet goes")
    parser.add_argument("--plan", default="raw_assets/agents/car/fleet_plan.md", help="the plan file, inside the project")
    parser.add_argument("--batch", type=cli.counting, default=1, metavar="INT", help="which `## batch <N>` section")
    parser.add_argument("--cars", type=lambda s: [i for i in s.split(",") if i], default=None, metavar="IDS",
                        help="in place of --batch: these planned cars, whichever batch each is in, in this order — a "
                             "sheet that draws installed cars again")
    parser.add_argument("--frame", type=frame_name, default=None,
                        help="brief: the sheet's name, as remotegen's --name (lowercase, digits, underscores)")
    parser.add_argument("--wrecks", action="store_true",
                        help="brief: the batch's wrecks, drawn from its installed cars, ids <car>_broken")
    parser.add_argument("--style", type=lambda s: [i for i in s.split(",") if i], default=None, metavar="IDS",
                        help="brief of new cars: the style picture's cars, in place of the ones picked for the batch "
                             "(`<frame>_style.png` under .tmp/gen/templates/)")
    parser.add_argument("--also", type=lambda s: [i for i in s.split(",") if i], default=[], metavar="IDS",
                        help="brief --wrecks: installed cars of other batches, comma-separated, after the batch's own "
                             "(to fill a short sheet to nine); track: the cars to measure")
    return parser.parse_args()


def frame_name(text):
    if not FRAME.fullmatch(text):
        raise argparse.ArgumentTypeError(f"not a frame name: {text!r}")
    return text


def batch_rows(plan: Path, batch: int):
    """The rows of the plan's `## batch <batch>` table, each a dict by COLUMNS, numbers parsed."""
    rows = plan_batches(plan).get(batch, [])
    if not rows:
        cli.fail(f"{plan.name} has no rows under `## batch {batch}`")
    return rows


def cars_rows(plan: Path, cars):
    """The plan's rows for these ids, whichever batch each is in, in the order asked: a sheet that draws cars again."""
    by_id = {r["id"]: r for rows in plan_batches(plan).values() for r in rows}
    if missing := [c for c in cars if c not in by_id]:
        cli.fail(f"{plan.name} plans no {', '.join(missing)}")
    return [by_id[c] for c in cars]


def plan_batches(plan: Path):
    """Every `## batch <N>` table of the plan by N, each row a dict by COLUMNS, numbers parsed."""
    batches, batch, header = {}, None, None
    for line in plan.read_text(encoding="utf-8").splitlines():
        if line.startswith("## "):
            m = re.fullmatch(r"## batch (\d+)", line.strip())
            batch, header = (int(m.group(1)) if m else None), None
            continue
        m = ROW.match(line) if batch is not None else None
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
        batches.setdefault(batch, []).append(r)
    return batches


#: Every car sheet's prompt opens with this, bodies and wrecks alike: the style, the view, the layout, the shape and the
#: background. {n} cars, {cols}×{rows} grid.
BASE = """\
One image: a sprite sheet of {n} cars for a 2D top-down city game.

Style: match the attached picture's art style exactly — outline weight, shading, highlights, level of detail, pixel size.

View: straight down from directly above the middle of each roof, orthographic, as the attached cars are: the roof \
centred across the body, and neither flank seen more than the other — no doors, sills or side windows showing along \
one side. No perspective, no ground, no shadow.

Layout: a {cols}×{rows} grid, the cars in the order listed, left to right and top to bottom. Each car lies \
horizontally with its nose to the right, centred in its place, with empty space round it. One shared scale for all.

Every car:
- Its length and width as listed, in that proportion, with straight flanks. Narrow cars stay narrow: leave spare room \
empty rather than draw a car wider.
- Its shape mirror-symmetric about its long centre line, with two small side mirrors just ahead of the windscreen; \
wear, rust and odd panels may differ side to side.
- Roof on. No tyres or wheels (the game draws them), no people, no text, logos, badges or licence plates.

Background: fully transparent outside the cars — no colour, no glow or halo, no shadow. Each car's edge ends sharply."""

#: What a sheet of new cars adds: the lamps, which the lamp cut (`qq props`) finds by the dark rim drawn round each.
NEW = """\
The cars in the picture only show the style: draw the cars listed, in their own colours. They are drawn at one \
scale, as the sheet's cars are: a longer car there is a longer drawing.
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
    if len(rows) < SHEET_LEAST:
        print(f"under remotegen's {SHEET_LEAST} a sheet: fill it with --also (wrecks) or merge it with a like set")
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


#: How many fleet cars the style picture shows, in rows of three: one fixed row of three mid-size cars taught one
#: size and one lamp design, and a sheet of 4.9-5.9 m cars came back all one length (fleet100_b11-v1).
STYLE_CARS = 6
#: How far past the batch's own lengths the style picture's lengths reach, either way, in metres.
STYLE_REACH_M = 0.4
#: The least score a car shown as style was kept at.
STYLE_SCORE_LEAST = 4
#: Body classes a car's describe names, for leaning the style picture towards the batch's.
CLASSES = ("van", "minibus", "suv", "off-road", "crossover", "pickup", "estate", "saloon", "liftback", "hatch",
           "coupe", "people carrier", "taxi", "city car", "microcar")
#: The lamps whose own lens, or share of a main lamp, tells one car's lamp design from another's.
OWN_LENSES = ("ind_front", "ind_rear", "reverse")


def style_picture(base, rows, frame, chosen):
    """The style picture for a sheet of new cars, `<frame>_style.png` beside the templates: fleet cars at one scale,
    `chosen` or else picked by `style_cars`, each its installed sprite: the lamps as `qq props mirror` left them, one
    design a side."""
    import sprites

    ids = chosen or [c["id"] for c in style_cars(base, rows)]
    found = sprites.assets(base)
    out = base / ".tmp/gen/templates" / f"{frame}_style.png"
    # A car named by --style on purpose may be on the sheet too: a set drawn again from some of its own pictures.
    sprites.style(base, found, ids, {r["id"] for r in rows} - set(chosen or ()), out, columns=3)


def style_cars(base, rows):
    """STYLE_CARS fleet cars kept at STYLE_SCORE_LEAST or better, none of them asked for: lengths spread evenly over
    the batch's own range (STYLE_REACH_M past it), so the model sees one scale across sizes; at each length the
    nearest car whose lamp design (OWN_LENSES cut as lenses of their own) no car already picked has, a split tail
    first, and of a body class the batch has where one is as near. Prints each pick and why."""
    asked = {r["id"] for r in rows}
    lengths = [float(r.get("lengthM") or sized(base, r)[0]) for r in rows]
    wanted = {c for r in rows for c in CLASSES if c in r["describe"].lower()}
    pool = [c for c in fleet_cars(base) if c["id"] not in asked and c["score"] >= STYLE_SCORE_LEAST]
    low, high = min(lengths) - STYLE_REACH_M, max(lengths) + STYLE_REACH_M
    picked, designs = [], set()
    for k in range(STYLE_CARS):
        target = low + (high - low) * k / max(1, STYLE_CARS - 1)

        def cost(c):
            return (abs(c["lengthM"] - target) + (0.5 if c["design"] in designs else 0.0)
                    - 0.15 * len(c["own"]) - (0.2 if c["classes"] & wanted else 0.0))

        best = min((c for c in pool if c not in picked), key=cost, default=None)
        if best is None:
            break
        picked.append(best)
        designs.add(best["design"])
        print(f"  style {best['id']:<22} {best['lengthM']:.2f} m for {target:.2f}; own lenses "
              f"{', '.join(best['own']) or 'none'}; {', '.join(sorted(best['classes'])) or 'no class word'}")
    return sorted(picked, key=lambda c: c["lengthM"])


def fleet_cars(base):
    """Every car in the fleet with a used variant and its kept lamp cut: id, length, score, which OWN_LENSES are lenses
    of their own, that as a lamp design, and the body classes its describe names."""
    out = []
    for entry in json.loads((base / FLEET).read_text(encoding="utf-8"))["variants"]:
        described = (base / FLEET).parent / entry
        d = json.loads(described.read_text(encoding="utf-8"))
        source = described.with_name(f"{d['id']}.src.json")
        if not source.is_file():
            continue
        variant = base / json.loads(source.read_text(encoding="utf-8"))["variant"]
        record = next((json.loads(line) for line in (variant.parent / "variants.jsonl").read_text(encoding="utf-8")
                       .splitlines() if line.strip() and json.loads(line)["variant"] == variant.stem), {})
        props = variant.with_name(f"{variant.stem}.props.json")
        if not props.is_file():
            continue
        lenses = {p["name"] for p in json.loads(props.read_text(encoding="utf-8"))["props"] if "points" in p}
        own = tuple(n for n in OWN_LENSES if n in lenses)
        out.append({"id": d["id"], "lengthM": d["collisionM"]["sizeM"][0], "score": record.get("score", 0),
                    "own": own, "design": own, "classes": {c for c in CLASSES if c in d.get("describe", "").lower()}})
    return out


def wreckable(base, rows):
    """The batch's rows whose car has a picture installed; the rest are named and left out."""
    kept = []
    for r in rows:
        d = base / VARIANTS / r["id"] / f"{r['id']}.json"
        sprite = json.loads(d.read_text(encoding="utf-8")).get("sprite", "") if d.is_file() else ""
        if sprite and (d.parent / sprite).is_file():
            kept.append(r)
        else:
            print(f"{r['id']}: no picture installed, no wreck asked")
    return kept


def installed_row(base, car):
    """An installed car of any batch as a plan row, as far as a wreck's line needs one (`--also`)."""
    d = base / VARIANTS / car / f"{car}.json"
    if not d.is_file():
        cli.fail(f"--also {car}: no {d.relative_to(base)}")
    v = json.loads(d.read_text(encoding="utf-8"))
    return {"id": car, "describe": v["describe"], "cornerM": v["collisionM"]["cornerRadiusM"]}


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
    if (path.parent / d.get("sprite", "")).is_file():
        # CAR-12: the track is the drawn width over the axles, measured again whatever moved.
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


def retrack(base, cars):
    """Write each named car's track as its picture measures it (CAR-12)."""
    if not cars:
        cli.fail("track: name the cars with --also")
    for car in cars:
        path = base / VARIANTS / car / f"{car}.json"
        if not path.is_file():
            cli.fail(f"track: no {path.relative_to(base)}")
        text = path.read_text(encoding="utf-8")
        d = json.loads(text)
        track = measured_track(path.parent / d["sprite"], d)
        path.write_text(re.sub(r'"trackM":\s*[0-9.]+', f'"trackM": {track}', text, count=1), encoding="utf-8")
        print(f"{car}: trackM {d['trackM']}→{track}")
    return 0


#: The nominal tyre's length along its roll (`Tyre.WheelLengthM`), for a file naming none.
WHEEL_LENGTH_M = 0.62
#: The review sheet: how much each flank is magnified, how many texels in from its edge are shown and how far out, the
#: ticks' length, and their colours (axle, tyre ends).
WHEELS_ZOOM, WHEELS_IN_PX, WHEELS_OUT_PX, WHEELS_TICK_PX = 3, 12, 4, 16
WHEELS_AXLE_INK, WHEELS_TYRE_INK = (230, 0, 150), (0, 120, 255)


def fleet_ids(base):
    """The civilian fleet's ids, in Fleet.json's order."""
    listed = json.loads((base / VARIANTS / "common" / "Fleet.json").read_text(encoding="utf-8"))["variants"]
    return [Path(v).stem for v in listed]


def wheel_sheet(base, cars, out: Path):
    """Each car's two flanks, magnified, with its axles ticked and its tyres' ends either side, outside the paint. The
    art draws no wheels, but most of it shades a tyre's shadow under the sill — a dark stretch on the outermost
    texels — and that is where the wheel it was drawn with stands. Light paint shows it best; dark paint hides it."""
    import numpy as np
    from PIL import Image, ImageDraw, ImageFont

    font = ImageFont.load_default(size=20)
    band = WHEELS_IN_PX + WHEELS_OUT_PX
    strips = []
    for car in cars:
        path = base / VARIANTS / car / f"{car}.json"
        if not path.is_file():
            cli.fail(f"wheels: no {path.relative_to(base)}")
        d = json.loads(path.read_text(encoding="utf-8"))
        art = Image.open(path.parent / d["sprite"]).convert("RGBA")
        solid = np.asarray(art)[..., 3] >= 128
        # The flanks where the mirrors are not: the median edge of the body's middle columns.
        middle = solid[:, round(art.width * 0.3):round(art.width * 0.7)]
        top = int(np.median([np.flatnonzero(c)[0] for c in middle.T if c.any()]))
        bottom = int(np.median([np.flatnonzero(c)[-1] for c in middle.T if c.any()])) + 1
        flanks = [art.crop((0, top - WHEELS_OUT_PX, art.width, top + WHEELS_IN_PX)),
                  art.crop((0, bottom - WHEELS_IN_PX, art.width, bottom + WHEELS_OUT_PX))]
        h = band * WHEELS_ZOOM
        y_top = 26 + WHEELS_TICK_PX
        y_bottom = y_top + h + 6
        strip = Image.new("RGBA", (art.width * WHEELS_ZOOM, y_bottom + h + WHEELS_TICK_PX), (235, 235, 235, 255))
        for flank, y in zip(flanks, (y_top, y_bottom)):
            strip.alpha_composite(flank.resize((art.width * WHEELS_ZOOM, h), Image.NEAREST), (0, y))
        draw = ImageDraw.Draw(strip)
        per_m = art.width / float(d["footprintM"][0]) * WHEELS_ZOOM
        half_tyre_m = float(d.get("wheelM", [WHEEL_LENGTH_M])[0]) / 2
        for axle in (float(d["frontAxleM"]), float(d["rearAxleM"])):
            for off, ink, width in ((0, WHEELS_AXLE_INK, 3), (-half_tyre_m, WHEELS_TYRE_INK, 2),
                                    (half_tyre_m, WHEELS_TYRE_INK, 2)):
                x = strip.width / 2 + (axle + off) * per_m
                draw.line([(x, 26), (x, y_top - 2)], fill=ink, width=width)
                draw.line([(x, y_bottom + h + 2), (x, strip.height)], fill=ink, width=width)
        draw.text((4, 2), f"{car}  front {d['frontAxleM']:+.2f}  rear {d['rearAxleM']:+.2f} m   nose right",
                  fill=(0, 0, 0), font=font)
        strips.append(strip)
    sheet = Image.new("RGB", (max(s.width for s in strips), sum(s.height + 8 for s in strips)), (200, 200, 200))
    y = 0
    for s in strips:
        sheet.paste(s, (0, y))
        y += s.height + 8
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    print(f"{out.relative_to(base)}: {len(strips)} cars, each flank's outer texels with its axles (magenta) and tyre "
          "ends (blue) ticked")
    return 0


def measured_track(sprite: Path, d):
    """The track over a drawn car's axles, measured off its installed sprite (`_outline.installed_track_m`)."""
    import _outline

    return _outline.installed_track_m(sprite, float(d["footprintM"][0]), (float(d["frontAxleM"]), float(d["rearAxleM"])))


def variant_text(r):
    """The variant file, in the hand-written files' layout: one field a line, short arrays inline."""
    import _outline

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
  "trackM": {_outline.track_m([width])},
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
