"""
Write the walkers' variant files and each frame's brief from a walker plan, and preview a look walking.

**The orbit flow is how a look's walk is made** (raw_assets/agents/person/prompting.md, "The orbit flow"): one 5 s
HeyGen clip from one front picture, the camera cutting round the walker a facing a second, cut into its five drawn
facings by `qq sprites cut`. `orbit ID --cast=FRAME:N` lays figure N of a cast frame (nine looks standing, 3×3, read
left to right, top to bottom) on green as `.tmp/gen/templates/walkorbit_<id>.png`, writes ORBIT spoken of the look its
variant file describes as `.tmp/gen/briefs/walkorbit_<id>.txt`, and prints the remotegen call and the cut, keep and use
after it:

    qq walkers scaffold --sheet=cast_b && qq walkers orbit linen_grey --cast=walkers_cast_b-v2:3

`brief` below is the frame flow it replaced — a walk drawn as a 3:2 sheet of stills — kept for a look a clip cannot
make. A plan is a markdown file of `## sheet <frame>` sections, each one table `id | kind | height | describe`: `kind` is
`walker` (a town's pedestrian, `common/Catalog.json`) or `service` (a crew's uniform, `common/Service.json`), `height`
the person's in metres. A sheet holds one look or two: a frame is always 8 columns by 5 facing rows (`_walkers`), so one
look walks in eight frames, two in four each — and four shuffle (raw_assets/agents/person/prompting.md), so a sheet is
one look. Only the facings from the back round the left side to the front are drawn; `qq sprites use` mirrors the rest.

`brief` writes `.tmp/gen/briefs/walkers_<sheet>.txt` (BASE, the walk cycle its length asks for, then the people) and
`walkers_<sheet>.cells.json`, lays the two pictures sent — the style picture (`qq sprites style`, STYLE_IDS) and the
pose guide (`_walkers.pose_guide`; the owner, 2026-10-27) — and prints the remotegen call.
`scaffold` writes `assets/agents/person/variants/<id>/<id>.json` for a sheet's looks (or every look), keeping the
pictures an existing file names; a look joins its catalogue by hand, once its walk and down pictures are both installed.
`status` names each look's walk and down picture and what it traces to. `preview IDS` writes each installed look's
sheet walking its eight facings, `.tmp/gen/walkers/<id>_walk.gif`, as the engine picks its cells.

    qq walkers scaffold --sheet=guide_a && qq walkers brief --sheet=guide_a     # prints the remotegen call
    qq sprites cut walkers_guide_a-v1     # flags; then look at .tmp/gen/cuts/walkers_guide_a-v1/<id>_walk.gif
    qq sprites keep walkers_guide_a-v1 coat_brown --score=4 && qq sprites use coat_brown-v2
"""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

import _sim
import _walkers
from common import cli, paths, proc

VARIANTS = Path("assets/agents/person/variants")
CATALOGS = {"walker": VARIANTS / "common" / "Catalog.json", "service": VARIANTS / "common" / "Service.json"}
BRIEFS = Path(".tmp/gen/briefs")
TEMPLATES = Path(".tmp/gen/templates")
PREVIEWS = Path(".tmp/gen/walkers")
#: The frame remotegen returns at 3:2, which the pose guide is laid at so its grid is the frame's.
SHEET_PX = (1536, 1024)
#: The style picture: finished art from the game, none of it a walker (remotegen SKILL.md) — the cars the owner picked
#: for the fleet's, for the paint and the finish.
STYLE_IDS = ("crossover_pearl", "estate_steel", "kombi_sage")
STYLE_WIDTH_PX = 1200
SECTION = re.compile(r"^##\s+sheet\s+([a-z0-9_]+)\s*$")
ROW = re.compile(r"^\|(.+)\|\s*$")
ID = re.compile(r"^[a-z0-9_]+$")

BASE = """\
Two images are attached.
Image 1 is finished art from the same game, cars seen from above. Match its painting style, finish and light only — \
not its contents, colours or scale.
Image 2 is a pose guide: 40 stick figures in exactly the grid this sheet must have, 8 columns by 5 rows. In it the \
blue limbs are the person's LEFT leg and arm, the red limbs their RIGHT leg and arm, and the black dot is the nose. \
Draw the person in each cell in exactly the pose of the stick figure in the same cell: the same leg in front, the same \
arm swung forward, facing the same way. Take nothing else from it: no sticks, no blue or red, no dots, no grey \
background, and not its thin proportions.

Make one image: a walking-animation sprite sheet for a 2D city game, {who}. Lay it as a grid of 8 columns by 5 rows, \
exactly as image 2, 40 figures in all, each standing in its own equal cell, well apart from its neighbours: no figure \
touches or overlaps another, and nothing reaches into the next cell. {columns}

The view is the same for every figure: from in front of the person and high above, looking down at about 40 degrees, \
as in a classic top-down role-playing game — the top of the head and the shoulders show, the whole body is seen, a \
little shortened, the feet at the bottom. Each figure stands upright on its feet; the ground is not drawn.

Rows — the way the person walks, the same in every column of a row:
1. Straight away from us, up the picture: we see the back of the head, the back and the backs of the legs.
2. Diagonally away and to the left, towards the top-left corner: we see the back and the left side; the face is turned \
away.
3. To the left: the left side in profile, the nose pointing left.
4. Diagonally towards us and to the left, towards the bottom-left corner: we see the front and the left side, the face \
in three-quarter view looking down-left.
5. Straight towards us, down the picture: we see the face and the front.
Nobody in rows 2 to 4 faces or walks to the right.

{cycle}

Every figure of one person is the same person: the same height, build, face, hair, clothes, colours and anything \
carried. All the figures of a person are the same size: the head at the same height and the feet on one line in every \
cell of a row. Within a row the person faces exactly one way in every column — head, shoulders, hips and feet \
pointing the same way as in the row's first column, never turning towards us or away; only the legs and arms move. \
The walk is a relaxed city stroll with short steps, no longer than the guide's: the arms swing gently, opposite to \
the legs.

A real adult in natural proportions: the head about one seventh of the height, slim natural limbs — not a chibi, not \
a big-headed cartoon.

Style: painted game art like the style image — soft detailed painting, a clean readable silhouette, a thin darker \
edge rather than a heavy black outline, natural believable colours, light from the top-left with soft shading on the \
figure. Summer in a Black Sea port city.

Everything outside the figures is fully transparent: no background colour, no ground, no floor line, no cast shadow, \
nothing half-transparent. No text, letters, numbers, labels, grid lines, frames or borders anywhere.

{people}
"""

#: The orbit flow's clip (raw_assets/agents/person/prompting.md, "The orbit flow"): HeyGen at its shortest, the green
#: the first frame is laid on, and the share of the frame's height the figure stands.
ORBIT_MODEL, ORBIT_SETTINGS = "heygen/heygen-video-1", ("--resolution", "480p", "--aspect", "3:4", "--duration", "5")
ORBIT_PX, ORBIT_GREEN, ORBIT_FIGURE_SHARE = (768, 1024), (0, 177, 64), 0.55
#: Paint at least this opaque is a cast figure's, and runs of it this many pixels apart are two figures.
CAST_ALPHA, CAST_GAP_PX = 32, 4

ORBIT = """\
The {who} walks on the spot for the whole clip, always facing the same way: {they} never turns, never stops and never \
moves away from {their} spot. It is the camera that changes, not {them}. The clip is five shots of exactly one second \
each, joined by hard cuts exactly on each one-second tick: within a shot the camera is perfectly still, and at each cut \
it jumps instantly, with no camera movement and no transition, always at the same height and distance, always looking \
down at {them} from about 55 degrees, {they} always in the middle of the picture at the same size.

Shot 1 (0–1 s): the camera is in front of {them}, as in the first frame; {they} is walking from the very first moment.
Shot 2 (1–2 s): the camera is in front of {them} and to {their} left; {they} faces towards us and to the picture's left.
Shot 3 (2–3 s): the camera is at {their} left side; we see {them} in profile, facing the picture's left.
Shot 4 (3–4 s): the camera is behind {them} and to {their} left, halfway between {their} side and {their} back; {they} \
faces away from us and to the picture's left.
Shot 5 (4–5 s): the camera is straight behind {them}; we see {their} back.

{They} walks quickly, like someone in a hurry to catch a tram — but still a walk, never a run or a jog: one foot is \
always on the ground. Quick, short steps, three steps every second, in one steady, even rhythm like a metronome, every \
step as long as the one before it, running on unbroken across the cuts, so every shot holds at least one and a half \
whole stride cycles. The planted foot slides back along the floor under {them} as on a moving walkway, each heel \
landing in front of {them} and the back foot pushing off from its toe behind; legs nearly straight as the heel lands, \
knees never lifted high; arms swinging loosely opposite to the legs, elbows straight. The background stays one flat, \
uniform chroma-key green in every shot: no floor, no shadow, no scenery. {Their} look — {describe} — {their} build and \
the flat-shaded pixel-art game-sprite style stay exactly as in the first frame.
"""

#: The words a look's own description names it by, and how the brief speaks of it.
PRONOUNS = {"woman": ("she", "her", "her"), "girl": ("she", "her", "her"), "man": ("he", "him", "his"),
            "boy": ("he", "him", "his")}

CYCLE = {
    4: """\
Columns — the steps of one walk, the same pose in every row of a column:
1. Standing still: feet side by side under the body, arms relaxed at the sides.
2. Left foot forward in a full stride, its heel down; right foot behind, on its toes; right arm swung forward, left \
arm back.
3. Passing: the left foot flat under the body carrying the weight, the right foot lifted and swinging past it; arms \
close to the sides.
4. Right foot forward in a full stride, its heel down; left foot behind, on its toes; left arm swung forward, right \
arm back.""",
    8: """\
Columns — two steps of one walk, in order, as the guide's columns show. Both legs walk: the leg in front changes \
from step to step.
1. Standing, feet side by side, arms at the sides.
2. Reaching: the left foot swinging forward, about to land; the right heel rising behind.
3. Contact: the left heel lands in front, toe raised; the right foot behind, on its toes. Right arm forward.
4. Down: weight onto the left leg, its knee a little bent; the right foot leaves the ground behind.
5. Passing: standing tall on the left leg; the right knee bent, its foot lifted and passing the left.
6. Reaching: the right foot swinging forward, about to land; the left heel rising behind.
7. Contact: the right heel lands in front, toe raised; the left foot behind, on its toes. Left arm forward.
8. Down: weight onto the right leg, its knee a little bent; the left foot leaves the ground behind.""",
}


def main():
    a = parse_args()
    base = paths.project_root()
    if a.action == "preview":
        return preview(base, a.ids)
    if a.action == "orbit":
        return orbit(base, a.ids, a.cast)
    sheets = read_plan(paths.resolve_in_root(a.plan, base))
    if a.sheet and a.sheet not in sheets:
        cli.fail(f"the plan has no sheet {a.sheet}: {', '.join(sheets)}")
    chosen = {a.sheet: sheets[a.sheet]} if a.sheet else sheets
    if a.action == "brief":
        if not a.sheet:
            cli.fail("brief writes one sheet's: name it with --sheet")
        brief(base, a.sheet, sheets[a.sheet])
    elif a.action == "scaffold":
        for rows in chosen.values():
            for r in rows:
                print(scaffold(base, r))
    else:
        status(base, chosen)
    return 0


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq walkers",
        description="Write the walkers' variant files and each frame's brief from a walker plan, and preview a look "
                    "walking.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("action", choices=["orbit", "brief", "scaffold", "status", "preview"],
                        help="orbit: one look's clip brief | brief one sheet | scaffold variant files | status | "
                             "preview installed looks")
    parser.add_argument("ids", nargs="*", help="orbit: the one look; preview: the looks to preview")
    parser.add_argument("--cast", default="", metavar="FRAME:N",
                        help="orbit: the cast frame the look's front picture is figure N of (walkers_cast_b-v2:3)")
    parser.add_argument("--plan", default=".tmp/gen/walkers_plan.md", help="the walker plan, a markdown file")
    parser.add_argument("--sheet", default="", help="one sheet of the plan (default every sheet)")
    return parser.parse_args()


def read_plan(plan: Path) -> dict[str, list[dict]]:
    """Every sheet's looks, in plan order."""
    sheets, current, seen = {}, None, set()
    for line in plan.read_text(encoding="utf-8").splitlines():
        if line.startswith("## "):
            m = SECTION.match(line)
            current = sheets.setdefault(m.group(1), []) if m else None
            continue
        m = ROW.match(line)
        if current is None or not m:
            continue
        cells = [c.strip() for c in m.group(1).split("|")]
        if len(cells) != 4 or cells[0] in ("id", "") or set(cells[0]) <= {"-"}:
            continue
        i, kind, height, describe = cells
        if not ID.fullmatch(i) or i in seen:
            cli.fail(f"{plan}: {i!r} is not an id, or is planned twice")
        if kind not in CATALOGS:
            cli.fail(f"{plan}: {i} is a {kind!r}, neither {' nor '.join(CATALOGS)}")
        seen.add(i)
        current.append({"id": i, "kind": kind, "heightM": float(height), "describe": describe})
    for name, rows in sheets.items():
        if len(rows) not in (1, 2):
            cli.fail(f"{plan}: sheet {name} holds {len(rows)} looks; a walker frame holds one or two")
    if not sheets:
        cli.fail(f"{plan} has no `## sheet <name>` section")
    return sheets


def path_of(i: str) -> Path:
    return VARIANTS / i / f"{i}.json"


def scaffold(base, r) -> str:
    path = base / path_of(r["id"])
    old = json.loads(path.read_text(encoding="utf-8")) if path.is_file() else {}
    d = {"id": r["id"], "sheet": old.get("sheet", f"{r['id']}.webp"), "down": old.get("down", f"{r['id']}_down.webp"),
         "heightM": r["heightM"], "describe": r["describe"]}
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(d, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    listed = f"../{r['id']}/{r['id']}.json" in json.loads((base / CATALOGS[r["kind"]]).read_text(encoding="utf-8"))["variants"]
    return f"{path_of(r['id'])}" + ("" if listed else f" — not in {CATALOGS[r['kind']].name} yet")


def brief(base, sheet, rows):
    frame = f"walkers_{sheet}"
    cells = base / BRIEFS / f"{frame}.cells.json"
    cells.parent.mkdir(parents=True, exist_ok=True)
    cells.write_text(json.dumps([r["id"] for r in rows]) + "\n", encoding="utf-8")
    style = TEMPLATES / "walkers_style.png"
    laid = proc.run(["qq", "sprites", "style", *STYLE_IDS, f"--out={style}", f"--cells={BRIEFS / cells.name}",
                     f"--style-width={STYLE_WIDTH_PX}"], cwd=base)
    print(laid.text)
    frames = _walkers.frames_per_look(len(rows))
    guide = TEMPLATES / f"walkers_pose_guide_{frames}.png"
    _walkers.pose_guide(frames, SHEET_PX, base / guide)
    if len(rows) == 1:
        who = "of one person walking"
        columns = "All 40 figures are the same person."
        people = f"The person: {person_words(rows[0])}"
    else:
        who = "of two people walking"
        columns = ("Columns 1 to 4 are the first person and columns 5 to 8 the second; each person's four columns are "
                   "the four steps below, in order.")
        people = "\n".join(f"Person {n} (columns {4 * n - 3} to {4 * n}): {person_words(r)}" for n, r in enumerate(rows, 1))
    text = BASE.format(who=who, columns=columns, cycle=CYCLE[frames], people=people)
    out = base / BRIEFS / f"{frame}.txt"
    out.write_text(text, encoding="utf-8")
    print(f"{_sim.rel(base, out)}: {len(rows)} look(s), {frames} frames each; ids in {_sim.rel(base, cells)}")
    print(f".claude/skills/remotegen/remotegen.py edit --image {style} --image {guide} --cells {BRIEFS / cells.name} "
          f"-p \"$(cat {BRIEFS / out.name})\" --aspect 3:2 --name {frame}")


def orbit(base, ids, cast_ref):
    """The orbit flow's first frame and brief for one look: figure N of a cast frame (`FRAME:N`, read left to right,
    top to bottom) on green, and ORBIT spoken of the look its variant file describes."""
    import numpy as np
    from PIL import Image

    if len(ids) != 1:
        cli.fail("orbit briefs one look: name it")
    i = ids[0]
    path = base / path_of(i)
    if not path.is_file():
        cli.fail(f"no variant file {path_of(i)}: `qq walkers scaffold` it from the plan first")
    describe = json.loads(path.read_text(encoding="utf-8"))["describe"]
    noun = next((w for w in re.findall(r"[a-z]+", describe.lower()) if w in PRONOUNS), None)
    if noun is None:
        cli.fail(f"{i}'s describe names no {'/'.join(PRONOUNS)}: the brief speaks of the look by it")
    they, them, their = PRONOUNS[noun]
    frame, _, n = cast_ref.partition(":")
    if not n.isdigit():
        cli.fail("--cast is FRAME:N, the cast frame and the figure's number on it from 1")
    cast = Image.open(base / ".tmp/gen/frames" / f"{frame}.png").convert("RGBA")
    solid = np.asarray(cast)[..., 3] >= CAST_ALPHA

    def runs(mask):
        on = np.flatnonzero(mask)
        return [(int(r[0]), int(r[-1]) + 1) for r in np.split(on, np.flatnonzero(np.diff(on) > CAST_GAP_PX) + 1)]

    boxes = [(x0, y0, x1, y1) for y0, y1 in runs(solid.any(1)) for x0, x1 in runs(solid[y0:y1].any(0))]
    if not 1 <= int(n) <= len(boxes):
        cli.fail(f"{frame} holds {len(boxes)} figures")
    figure = cast.crop(boxes[int(n) - 1])
    figure = figure.crop(figure.getchannel("A").point(lambda a: 255 if a >= CAST_ALPHA else 0).getbbox())
    w, h = ORBIT_PX
    scale = ORBIT_FIGURE_SHARE * h / figure.height
    figure = figure.resize((round(figure.width * scale), round(figure.height * scale)), Image.LANCZOS)
    first = Image.new("RGBA", ORBIT_PX, ORBIT_GREEN + (255,))
    first.alpha_composite(figure, ((w - figure.width) // 2, (h - figure.height) // 2))
    name = f"walkorbit_{i}"
    image = TEMPLATES / f"{name}.png"
    (base / image).parent.mkdir(parents=True, exist_ok=True)
    first.convert("RGB").save(base / image)
    text = ORBIT.format(who=noun, they=they, them=them, their=their, They=they.capitalize(),
                        Their=their.capitalize(), describe=describe[0].lower() + describe[1:].rstrip("."))
    out = BRIEFS / f"{name}.txt"
    (base / out).parent.mkdir(parents=True, exist_ok=True)
    (base / out).write_text(text, encoding="utf-8")
    print(f"{out}: {i}, figure {n} of {frame} laid at {image}")
    print(f".claude/skills/remotegen/remotegen.py video --image {image} -p \"$(cat {out})\" --name {name} "
          f"--model {ORBIT_MODEL} {' '.join(ORBIT_SETTINGS)}")
    print(f"qq sprites cut {name}-v1 --as {i} && qq sprites keep {name}-v1 {i} --score=4 && qq sprites use {i}-vN")
    return 0


def person_words(r) -> str:
    return f"{r['heightM']:g} m tall. {r['describe']}"


def status(base, sheets):
    for name, rows in sheets.items():
        print(f"sheet {name}:")
        for r in rows:
            path = base / path_of(r["id"])
            if not path.is_file():
                print(f"  {r['id']:<20} no variant file: `qq walkers scaffold --sheet={name}`")
                continue
            d = json.loads(path.read_text(encoding="utf-8"))
            parts = []
            for key in ("sheet", "down"):
                picture = path.parent / d[key]
                source = picture.with_name(Path(d[key]).stem + ".src.json")
                parts.append(f"{key} " + ("missing" if not picture.is_file()
                                          else json.loads(source.read_text(encoding="utf-8"))["variant"] if source.is_file()
                                          else "legacy"))
            print(f"  {r['id']:<20} {', '.join(parts)}")


def preview(base, ids):
    """Each installed look's sheet laid as the engine's 8×8 cells and walked through its columns."""
    if not ids:
        cli.fail("preview names the installed looks to walk")
    from PIL import Image

    for i in ids:
        path = base / path_of(i)
        if not path.is_file():
            cli.fail(f"no variant file {path_of(i)}")
        walk = Image.open(path.parent / json.loads(path.read_text(encoding="utf-8"))["sheet"]).convert("RGBA")
        out = base / PREVIEWS / f"{i}_walk.gif"
        _walkers.preview_installed(walk, out, i)
        print(_sim.rel(base, out))
    return 0


if __name__ == "__main__":
    cli.entry(main)
