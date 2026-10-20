"""
Cut a car's props (its lamps) out of its sprite as arc polygons, in a loop of place, look, adjust.

**What a prop is for.** The game lights a prop when its lamp is on, so a prop is the lens the art drew, all of it and
nothing else: a brake lights red over its lens, an indicator blinks amber over its own. Every prop is:

- **symmetric**: drawn once and mirrored (`"mirror": true`), never authored twice. A car is symmetric about its
  centreline and so are its lamps; where the art is not, the shape settles between the two sides.
- **smooth**: a few arcs, near convex, its corners rounded; show flags and snap refuses anything else.
- **on the drawn outline where there is one**: most lamps sit in a dark frame, and a lens ends at the frame's inner
  edge. Where the art draws none, at the edge of the lens's own colour.
- **close, not pixel perfect**: half a texel off its rim is on it, and a texel is good enough where the art disagrees
  with itself.

**What a real lamp looks like.** Reason from this before the pixels, and from the pixels before the numbers.

- A lamp is a housing with one or more lenses, drawn as a smooth light shape inside a dark rim, its shape following
  the body's corner: a crescent, a wedge, a rounded rectangle; never a jagged or hooked one.
- Rear: brake (and tail) red, the largest lens, outboard; indicator amber, a lens of its own beside the brake or a
  section of the same housing; reverse white or clear, small, usually inboard. Front: head white or pale, the
  largest; indicator amber at the outer corner, or a strip of the head's housing.
- Both sides carry the same lamps in mirror image. Where one side is drawn clearly and the other muddled, decide from
  the clear side: the copy follows by construction.
- **Overlaps are real but particular.** A main lamp is its whole housing, and a section of another colour drawn
  inside it (an amber indicator in a red cluster) is its own prop lying over it, which says so with `"over":
  ["brake"]`; the lit car burns the smaller on top. Separate lenses never overlap: pull them apart (`move --grow`
  negative on the edges that face each other). The report lists every overlap of an end that involves the props it
  reports, and flags one not declared; no overlap line means none.
- **No lens of its own**: where the art draws no indicator, it is the outboard half of the main lamp; no reverse, the
  inboard third of the brake — outboard and inboard across the car, whatever the lens's shape. It is written as a
  part, and follows its parent:

      {"name": "ind_rear", "kind": "ind", "part": "brake", "side": "outboard", "share": 0.5}

  Look for a lens of its own first: a part laid over the main lamp covers none of an amber lens drawn beside it. A
  part never lies over another lamp the art drew in the same housing; where it would, take a smaller `share`, or the
  other `side`.

**The loop**, one car, in this order. Each step names what to look at, and nothing else is looked at.

1. `seed CAR` draws both ends (`<car>_show.png`): look at it once. Note which lenses the art draws, which lamps
   have none (parts), and which share a housing (overlaps).
2. Place each prop by its numbers: edit the file from the rulers, on the side with y under the middle, a little
   inside the lens and over all of it, in 4–8 points with their bows 0; snap fits the arcs.
3. `snap CAR --text`, and read the text: a report line a prop, and the overlaps.
4. For each prop the text calls off — **off its rim** over 0.5 on a side, an edge contrast under 0.1, **framed**
   under 30 % on a lamp with a frame (`dark` shows one), NOT A LENS, an overlap not declared — `show --prop` once,
   read its edge table, and fix it by the cheapest move that answers it: `snap --prop --edges` for the edges named,
   `move --grow` by the rim the table gives, `--pull=frame` where a framed lens stops short of its frame, `axis
   --prop` where the copy alone is off, `"over"` or a move apart for an overlap. `--edge=N` only for an edge the
   table cannot settle. Check a fix with `--text`; look again only if the numbers cannot say.
5. A prop is done when it is off its rim by at most 0.5 on both sides; or by about a texel on one side with the table
   saying the sides disagree and `axis --prop` finding no better copy (lopsided art); or when its table finds no rim
   on most edges (art with nothing to fit: say so rather than try again). A done prop takes no hint: snap's dropped
   passes are snap refusing a worse shape, and a "copy sits better" under 0.1 of gain on a done prop is noise.
6. `cut CAR`, and look at `<car>_lit.png` once: each lamp lit over its lens.

**Look only to answer a question you can name** ("does e2 reach the frame?"), never to see how it looks again. The
text answers most questions: read it first, and look where it cannot. One look a question, plain first; `dark` asks
"is there a frame", `sat` or `hue` "is this amber or red"; a sheet of every filter answers nothing. A view is turned
only to lie along an edge: `--edge` turns itself, and `--angle` goes only with `--vertex` or `--at`. A picture costs
about width × height / 750 tokens, and one past 1568 px or 1.15 MP is shrunk before it is seen; `--max-px` fits the
zoom to a budget (a 1024 px square is about 1400 tokens, 512 about 350), and show prints each sheet's cost. Each call picks
its own zoom; pass the same `--zoom` on each to compare two steps. A look at the same part overwrites the last
picture, and `--text` on any action draws none.

**Reading the text.**

- A report line a prop: its points; **edge contrast**, the score just inside its edges less just outside, 0 to 1,
  and the copy's; its weakest edge; **off its rim**, a side: the median distance in texels from the outline to the
  rim the search finds within `--reach`; **framed**, a side: the share of the texels just outside it that are
  frame-dark — half or more on a drawn outline; under 30 % on a lamp with a frame, the outline is short of it or over
  it; low and right on a lens with no frame of its own (a section inside a housing, a lens against paint);
  held and locked edges; its own axis; its pixels, both sides; NOT A LENS.
- **Overlaps**, an end: each pair of props sharing texels, how many, and whether it is declared.
- The **edge table** (show, snap or an edit of one prop): each edge's ends, chord, bow, contrast a side, and where
  its rim is found at each end, + out, both sides together — what snap moves it by; `move --edge --grow` by the mean
  of its two ends is the hand version. Where the sides' own rims are over LOPSIDED_PX apart it says so and gives
  each, and the rim taken is halfway, each side off by half the gap. Nothing is found past `--reach`, so a rim at
  its bound (±2.00 at the default) wants a longer `--reach`.
- Contrast is weak where a pale lens meets pale paint or red meets red, and a pull changes every figure: compare
  figures only under one.

**The props file** (one a car, under `--dir`) is in sprite texel coordinates, 0,0 the top-left corner and a texel's
centre at +0.5, so a number read off a ruler is the number to type:

    {"name": "brake", "kind": "brake", "mirror": true, "points": [[x, y, bow], ...]}

Vertex vN is points[N]; edge eN runs from vN to vN+1 (the last back to v0), and its bow is vN's third number: how
far, in texels, the edge's middle stands off its chord, positive out, at most half the chord. A copy's parts carry the
names of the ones they mirror. Corners are rounded ROUND_PX along their edges, or by the prop's `"round"`. Other keys:
`"axisY"` and `"mirrorDx"` (the prop's own copy placement, which `axis --prop` sets), `"pull": "frame"`, `"over":
["brake"]` (an overlap declared), `"hold": [2]` (edges kept as written and out of the fit: one that crosses a lens
rather than following its rim), `"lock": true` (out of snap).

    seed CAR                      rectangles from the car file's `lamps`: names only, they are misplaced
    snap CAR [--prop=P]           edges pulled onto the part's rim; without --prop, the ends' axes too
    snap CAR --prop=P --edges=1,2   only e1 and e2, the rest held for this snap
    axis CAR [--prop=P]           the ends' mirror axes refitted; with --prop, that prop's copy placed on its own
    move CAR --prop=P --vertex=3 --by=0.5,-1   v3 shifted (--edge: both its ends; neither: all of it)
    move CAR --prop=P --edge=2 --grow=0.5      e2 pushed out (negative in), its ends sliding along the edges beside
                                  it; --vertex along its corner's bisector; neither: the whole prop grown
    bow CAR --prop=P --edge=2 --bow=1.5        e2's bow set
    split CAR --prop=P --edge=2   e2 halved on its arc, a new v3 at its middle (later parts renumber)
    drop CAR --prop=P --vertex=3  v3 taken out, the edge joining its neighbours bowed through where it was
    undo CAR                      back one seed, snap, axis or edit (again to redo)
    cut CAR                       a coverage PNG a kind, clipped to the body, and the car lit
    show CAR [--end=rear]         both ends; or one end, a view a side
    show CAR --prop=P             one prop, a view a side, its parts named
    show CAR --prop=P --vertex=N  round one vertex
    show CAR --prop=P --edge=N    the edge turned flat, outside up, and under it unrolled into a strip: rows texels
                                  out from it, the edge at 0, a dot where each sample finds that side's rim (a grey
                                  cross: too faint to count), and the curve snap would fit through them
    show CAR --at=X,Y             round any texel

Every edit and snap is mirrored with its prop, prints its edge table, and shows the part it touched. Panels
(`--filter`, comma-separated): plain, stretch, luma, sat, hue, edges, dark (what counts as frame), likeness (what snap
pulls to), alpha, lit; stretch, luma, sat and edges are scaled to their panel and compare only within it. Over the
image are only the grid (`--grid`) and the marks (`--marks`), each switchable, and `--bare` adds each panel unmarked;
titles and rulers are in the margins. A turned view is ruled along and up from its part, not in sprite x and y; a
copy's runs the other way along its edge, as a mirror does, and its strip the same way as the authored one's.

**How snap works.** It learns the part from the placement: the colours of the prop's core, two texels in, against a
ring three to seven texels out, both sides. Each texel scores by how much nearer the core's colours it is (the
likeness panel) less the frame's rise, and each edge moves along its normal, within `--reach`, to where that score
falls. Hence placing a little inside and over all of it: a placement on a highlight learns the highlight. The offsets
are fitted an edge as a shift, a tilt and a bow; vertices go where their edges' shifts meet, smoothed with their
neighbours'; an edge that fits badly is split; a bow never turns concave that was not; and a pass that lowers the fit
or leaves no lens (crosses itself, or turns past MAX_TURN_DEG in all — a convex outline turns 360) is dropped, which
snap says, as it says which edges found no rim. `--pull=frame` goes by the dark
frame alone, for a pale lens on pale paint whose highlight the colours read as surround; a snap or axis of one prop
keeps it as the prop's `"pull"`.
"""

import argparse
import json
import math
import re
import shutil

import _props_fit as fit
import _props_view as view
import _sim
from common import cli, paths

#: Rendering and snapping one car is a few seconds; a whole-car cut with its glow a few more.
TIMEOUT_S = 120

VARIANTS = "assets/agents/car/variants"
ID = re.compile(r"^[a-z0-9_]{1,40}$")
#: How far from each end of the body its lamps can be, in metres: the end a show draws.
END_M = 0.8
#: The lamp fittings of a car file, as prop kinds; `rear` is the older files' tail-and-brake lamp.
KINDS = {"head": "head", "indicator": "ind", "brake": "brake", "rear": "brake", "reverse": "reverse"}
EDITS = ("move", "bow", "split", "drop")
#: Two props sharing fewer texels than this, both sides together, only touch: their rounded rims meet.
OVERLAP_PX = 8


def main():
    a = parse_args()
    base = _sim.root()
    if not ID.match(a.car):
        cli.fail(f"{a.car!r} is not a car id")
    folder = paths.resolve_in_root(a.dir, base)
    if not folder.is_relative_to(base / ".tmp"):
        cli.fail(f"props are scratch until a bake reads them and go under .tmp/: {_sim.rel(base, folder)} is not")
    folder.mkdir(parents=True, exist_ok=True)
    car = load_car(base, a.car)
    car["pull"] = a.pull
    doc_path = folder / f"{a.car}.json"
    if a.action == "seed":
        if doc_path.exists() and not a.force:
            cli.fail(f"{_sim.rel(base, doc_path)} exists: `--force` to seed over it (undo keeps one step)")
        save(doc_path, seeded(car), backup=doc_path.exists())
        print(f"{_sim.rel(base, doc_path)}: seeded from the car file's lamps")
        a.action = "show"
    if a.action == "undo":
        prev = doc_path.with_suffix(".prev.json")
        if not prev.exists():
            cli.fail(f"nothing to undo: no {_sim.rel(base, prev)}")
        swap = doc_path.with_suffix(".swap.json")
        shutil.move(doc_path, swap)
        shutil.move(prev, doc_path)
        shutil.move(swap, prev)
        print(f"{_sim.rel(base, doc_path)}: back one step (undo again to redo)")
        a.action = "show"
    if not doc_path.exists():
        cli.fail(f"no {_sim.rel(base, doc_path)}: `qq props seed {a.car}` first")
    doc = load(doc_path)
    names = {p["name"] for p in doc["props"]}
    for p in doc["props"]:
        if "part" in p and p["part"] not in names - {p["name"]}:
            cli.fail(f"{p['name']} is a part of {p['part']!r}, which is no prop of {_sim.rel(base, doc_path)}")
    chosen = [p for p in doc["props"] if a.prop in (None, p["name"])]
    if not chosen:
        cli.fail(f"no prop {a.prop!r} in {_sim.rel(base, doc_path)}: {', '.join(sorted(names))}")
    one = chosen[0] if a.prop else None
    check_parts(a, one)
    if a.action in ("snap", "axis"):
        if one is not None and a.pull is not None and "points" in one:
            # Kept on the prop, so a later look scores it the way it was fitted without being told again.
            one.pop("pull", None)
            if a.pull != "part":
                one["pull"] = a.pull
        if a.prop is None:
            for end in ("rear", "front"):
                was, doc["axisY"][end] = doc["axisY"][end], fit.best_axis(doc, car, end)
                print(f"{end} axisY {was:.2f} -> {doc['axisY'][end]:.2f}")
        if a.action == "snap":
            for p in chosen:
                print(fit.snap(doc, car, p, a.reach, a.edges))
        elif a.prop is not None:
            for p in chosen:
                print(fit.own_copy(doc, car, p))
        save(doc_path, doc, backup=True)
    if a.action in EDITS:
        was = [q[:2] for q in one["points"]]
        print(edit(a, one))
        if a.action == "move":
            # An edge pushed out drags its ends along the edges beside it: say which moved, and how far.
            moved = [f"v{j} {x - x0:+.2f},{y - y0:+.2f}" for j, ((x0, y0), (x, y, _)) in enumerate(zip(was, one["points"]))
                     if abs(x - x0) + abs(y - y0) > 1e-3]
            print(f"  moved {', '.join(moved) or 'nothing'}")
        if a.action == "drop":
            # What is left to look at is the edge that now spans the gap.
            a.vertex, a.edge = None, (a.vertex - 1) % len(one["points"])
        if wrong := fit.smooth_enough(fit.outline(one["points"], fit.rounding_of(one))):
            print(f"  NOT A LENS: {wrong} (kept; undo steps back)")
        save(doc_path, doc, backup=True)
    if a.action == "cut":
        return cut(base, folder, doc, car)
    tag = "_".join(str(t) for t in (a.prop, f"v{a.vertex}" if a.vertex is not None else None,
                                     f"e{a.edge}" if a.edge is not None else None,
                                     f"at{a.at[0]:g}_{a.at[1]:g}" if a.at else None,
                                     a.end if a.end != "both" and not a.prop and not a.at else None) if t)
    out = folder / (f"{a.car}_{tag}_show.png" if tag else f"{a.car}_show.png")
    report(doc, car, [one] if one else [p for p in doc["props"] if a.end in ("both", fit.end_of(doc, car, p))],
           a.reach)
    if one is not None and "points" in one:
        edge_table(doc, car, one, a.reach)
    if a.text:
        return 0
    lines = view.show(doc, car, a, one, out)
    print(f"{_sim.rel(base, out)}: {lines[0]}")
    for line in lines[1:]:
        print(line)
    return 0


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq props",
        description="Cut a car's props (its lamps) out of its sprite as arc polygons, in a loop of place, look, adjust.",
        epilog=__doc__.split("\n", 2)[2],
        allow_abbrev=False,
        formatter_class=Help,
    )
    parser.add_argument("action", choices=["seed", "show", "snap", "axis", "move", "bow", "split", "drop", "undo", "cut"],
                        help="seed from the car file, look, pull onto the part, refit the axes, edit a part, step "
                             "back, or cut")
    parser.add_argument("car", help="the car's id, a folder under assets/agents/car/variants/")
    parser.add_argument("--prop", default=None, help="the prop of this name: shown close up, snapped, or edited")
    parser.add_argument("--vertex", type=cli.counting, default=None, metavar="N",
                        help="the prop's vertex vN: shown close up, moved or dropped")
    parser.add_argument("--edge", type=cli.counting, default=None, metavar="N",
                        help="the prop's edge eN: shown flat and unrolled, moved, bowed or split")
    parser.add_argument("--at", type=texel_pair, default=None, metavar="X,Y",
                        help="show round this texel (without --prop)")
    parser.add_argument("--angle", type=bounded(-360, 360), default=None, metavar="DEG",
                        help="turn a --vertex or --at view: its left to right runs along DEG (an --edge view turns "
                             "itself to its edge)")
    parser.add_argument("--end", default="both", choices=["both", "rear", "front"],
                        help="which end show draws without --prop; one end is a view a side, cropped to its props")
    parser.add_argument("--filter", type=filter_list, default="plain", metavar="LIST",
                        help=f"the panels, comma-separated, of {', '.join(view.FILTERS)}")
    parser.add_argument("--grid", default="on", choices=["on", "ruled", "off"],
                        help="texel grid over the image: every texel (from x6) and every labelled line, only the "
                             "labelled lines, or none (the margins' ticks stay)")
    parser.add_argument("--marks", default="full", choices=["full", "lines", "off"],
                        help="what is drawn over the image: outlines, dots and part names; no names; nothing")
    parser.add_argument("--bare", action="store_true", help="every panel again beside it with no marks")
    parser.add_argument("--text", action="store_true",
                        help="print the text only and draw no picture: for a check the numbers answer")
    parser.add_argument("--zoom", type=cli.counting, default=0, metavar="INT",
                        help="screen pixels a texel (0 = the most that fits --max-px)")
    parser.add_argument("--max-px", type=int, default=1024, choices=[512, 768, 1024, 1568],
                        help="the sheet's longest side an automatic zoom fits to: what the look costs")
    parser.add_argument("--pad", type=cli.counting, default=12, metavar="INT",
                        help="texels of sprite shown round what a view frames")
    parser.add_argument("--reach", type=float, default=2.0, choices=[1.0, 1.5, 2.0, 3.0, 4.0],
                        help="texels an edge may move in snap's first pass")
    parser.add_argument("--pull", default=None, choices=["part", "frame"],
                        help="what snap, the contrasts and the rim search go by: the part as the placement shows it, "
                             "or only the lamp's dark frame (a pale lens on pale paint); a snap or axis of one --prop "
                             "keeps it on the prop as its own `pull` (default: the prop's own, else part)")
    parser.add_argument("--edges", type=index_list, default=None, metavar="LIST",
                        help="snap only these edges of --prop, comma-separated; the rest held for this snap")
    parser.add_argument("--by", type=texel_shift, default=None, metavar="DX,DY",
                        help="move: texels to shift by")
    parser.add_argument("--grow", type=bounded(-10, 10), default=None, metavar="PX",
                        help="move: texels to push out along the normal (negative in)")
    parser.add_argument("--bow", type=bounded(-20, 20), default=None, metavar="PX",
                        help="bow: the edge's new bow, in texels (positive out)")
    parser.add_argument("--force", action="store_true", help="let seed replace an existing props file")
    parser.add_argument("--dir", default=".tmp/props", help="where the props files and pictures are kept")
    return parser.parse_args()


class Help(argparse.ArgumentDefaultsHelpFormatter, argparse.RawDescriptionHelpFormatter):
    pass


def bounded(lo, hi):
    def parse(text):
        v = float(text)
        if not lo <= v <= hi:
            raise argparse.ArgumentTypeError(f"{v:g} is outside {lo:g}..{hi:g}")
        return v
    return parse


def pair(text, lo, hi):
    parts = text.split(",")
    if len(parts) != 2:
        raise argparse.ArgumentTypeError(f"{text!r} is not two numbers joined by a comma")
    return tuple(bounded(lo, hi)(q) for q in parts)


def texel_pair(text):
    return pair(text, 0, 4096)


def texel_shift(text):
    return pair(text, -20, 20)


def index_list(text):
    out = {cli.counting(q) for q in text.split(",") if q}
    if not out:
        raise argparse.ArgumentTypeError("no edge given")
    return out


def filter_list(text):
    out = [q for q in text.split(",") if q]
    if bad := [q for q in out if q not in view.FILTERS]:
        raise argparse.ArgumentTypeError(f"no filter {', '.join(bad)}: {', '.join(view.FILTERS)}")
    return out or ["plain"]


def check_parts(a, p):
    """What the options name exists and the action can do with it."""
    asks_part = a.vertex is not None or a.edge is not None or a.edges is not None
    if (asks_part or a.action in EDITS) and p is None:
        cli.fail(f"{a.action} of a part needs --prop")
    if (asks_part or a.action in EDITS) and "points" not in p:
        cli.fail(f"{p['name']} is a part of {p['part']}, and has no vertices or edges: edit {p['part']}")
    n = len(p["points"]) if p is not None and "points" in p else 0
    for name, i in (("vertex", a.vertex), ("edge", a.edge)):
        if i is not None and i >= n:
            cli.fail(f"{p['name']} has no {name} {i}: it has {n}, 0..{n - 1}")
    if a.edges is not None and (bad := sorted(i for i in a.edges if i >= n)):
        cli.fail(f"{p['name']} has no edge {', '.join(map(str, bad))}: it has {n}, 0..{n - 1}")
    if a.vertex is not None and a.edge is not None:
        cli.fail("--vertex or --edge, not both")
    if a.angle is not None and a.vertex is None and a.at is None:
        cli.fail("a view turns only to lie along a part: --edge turns itself, and --angle goes with --vertex or --at")
    if a.at is not None and p is not None:
        cli.fail("--at frames a texel, --prop a prop: one of them")
    need = {"move": a.by is not None or a.grow is not None, "bow": a.edge is not None and a.bow is not None,
            "split": a.edge is not None, "drop": a.vertex is not None}
    if a.action in need and not need[a.action]:
        cli.fail({"move": "move needs --by or --grow", "bow": "bow needs --edge and --bow",
                  "split": "split needs --edge", "drop": "drop needs --vertex"}[a.action])
    if a.action == "drop" and n <= 3:
        cli.fail(f"{p['name']} has 3 vertices: a prop keeps at least that many")


def edit(a, p):
    """One edit of a prop's own points; what it did, as a line."""
    import numpy as np

    pts, name = p["points"], p["name"]
    if a.action == "move":
        done = []
        if a.by is not None:
            dx, dy = a.by
            which = [a.vertex] if a.vertex is not None else \
                [a.edge, (a.edge + 1) % len(pts)] if a.edge is not None else range(len(pts))
            for j in which:
                pts[j][0] += dx
                pts[j][1] += dy
            done.append(f"{part_name(a)} shifted {dx:+g},{dy:+g}")
        if a.grow is not None:
            if a.vertex is not None:
                d = fit.vertex_normal(pts, a.vertex) * a.grow
                pts[a.vertex][0] += float(d[0])
                pts[a.vertex][1] += float(d[1])
            else:
                pts = fit.offset_edges(pts, [a.grow if a.edge in (None, i) else 0.0 for i in range(len(pts))])
            done.append(f"{part_name(a)} grown {a.grow:+g}")
        p["points"] = fit.rounded(pts)
        return f"  {name}: {', '.join(done)}"
    if a.action == "bow":
        p0, p1 = fit.edge_ends(pts, a.edge)
        half = float(np.hypot(*(p1 - p0))) / 2
        pts[a.edge][2] = max(-half, min(half, a.bow))
        p["points"] = fit.rounded(pts)
        return f"  {name}: e{a.edge} bowed {pts[a.edge][2]:+.2f}" + (" (half its chord, a semicircle)"
                                                                     if abs(a.bow) > half else "")
    if a.action == "split":
        p["points"] = fit.rounded(fit.split_edge(pts, a.edge))
        renumber(p, lambda i: [i] if i < a.edge else [i, i + 1] if i == a.edge else [i + 1])
        return f"  {name}: e{a.edge} split at v{a.edge + 1}; later parts are one up"
    p["points"] = fit.rounded(fit.drop_vertex(pts, a.vertex))
    # eN and eN-1 become one edge, eN-1's place: it is held where eN-1 was.
    renumber(p, lambda i: [] if i == a.vertex else [i] if i < a.vertex else [i - 1])
    return f"  {name}: v{a.vertex} dropped; later parts are one down"


def renumber(p, to):
    if "hold" in p:
        p["hold"] = sorted({j for i in p["hold"] for j in to(i)})


def part_name(a):
    return f"v{a.vertex}" if a.vertex is not None else f"e{a.edge}" if a.edge is not None else "all"


def load_car(base, car):
    import numpy as np
    from PIL import Image

    folder = base / VARIANTS / car
    described = folder / f"{car}.json"
    if not described.exists():
        cli.fail(f"no car file {_sim.rel(base, described)}")
    d = json.loads(described.read_text(encoding="utf-8"))
    src = folder / f"{car}.src.json"
    if not src.exists():
        cli.fail(f"no {_sim.rel(base, src)}: the sprite's density is read from its trace")
    ppm = json.loads(src.read_text(encoding="utf-8"))["pxPerM"]
    rgba = np.asarray(Image.open(folder / d["sprite"]).convert("RGBA")).astype(np.float32) / 255
    a = rgba[..., 3:4]
    colour = rgba[..., :3] * a + np.array(fit.GROUND, np.float32) * (1 - a)
    dark = np.clip((fit.DARK_VAL_NONE - colour.max(-1)) / (fit.DARK_VAL_NONE - fit.DARK_VAL_FULL), 0, 1)
    return {"id": car, "file": d, "rgba": rgba, "colour": colour, "dark": dark, "ppm": ppm,
            "end": int(round(END_M * ppm))}


def seeded(car):
    """One rectangle per lamp of the car file on the side with y under the middle, mirrored, about the body's own
    middle row at each end."""
    d = car["file"]
    length, width = d["footprintM"]
    h, w = car["rgba"].shape[:2]
    props, names = [], {}
    for lamp in d.get("lamps", []):
        (xm, ym), (sx, sy) = lamp["atM"], lamp["sizeM"]
        if ym > 1e-3:
            continue
        kind = KINDS.get(lamp["fitting"], lamp["fitting"])
        name = kind if kind in ("head", "brake", "reverse") else f"{kind}_{'front' if xm > 0 else 'rear'}"
        names[name] = names.get(name, 0) + 1
        if names[name] > 1:
            name = f"{name}{names[name]}"
        x0, x1 = (xm - sx / 2 + length / 2) * w / length, (xm + sx / 2 + length / 2) * w / length
        y0, y1 = (ym - sy / 2 + width / 2) * h / width, (ym + sy / 2 + width / 2) * h / width
        props.append({"name": name, "kind": kind, "mirror": abs(ym) > 1e-3,
                      "points": [[x0, y0, 0.0], [x1, y0, 0.0], [x1, y1, 0.0], [x0, y1, 0.0]]})
    return {"car": car["id"], "axisY": {e: body_middle(car, e) for e in ("rear", "front")}, "props": props}


def body_middle(car, end):
    """The row halfway between the body's top and bottom edges, over the end's columns."""
    import numpy as np

    solid = car["rgba"][..., 3] > 0.5
    s = solid[:, :car["end"]] if end == "rear" else solid[:, solid.shape[1] - car["end"]:]
    has = s.any(0)
    top = np.argmax(s, 0)[has]
    bottom = s.shape[0] - np.argmax(s[::-1], 0)[has]
    return round(float(np.median((top + bottom) / 2)) * 4) / 4


def load(path):
    doc = json.loads(path.read_text(encoding="utf-8"))
    for p in doc["props"]:
        if "points" in p:
            p["points"] = [list(map(float, q)) + [0.0] * (3 - len(q)) for q in p["points"]]
            p.setdefault("mirror", True)
        p.setdefault("kind", p["name"])
    return doc


def save(path, doc, backup):
    """One point a line, so a hand edit is one line."""
    if backup and path.exists():
        shutil.copy(path, path.with_suffix(".prev.json"))
    lines = ["{", f'  "car": "{doc["car"]}",',
             '  "axisY": {' + ", ".join(f'"{e}": {v:.2f}' for e, v in doc["axisY"].items()) + "},", '  "props": [']
    for i, p in enumerate(doc["props"]):
        head = ", ".join(f'"{k}": {json.dumps(v)}' for k, v in p.items() if k != "points")
        comma = "," if i < len(doc["props"]) - 1 else ""
        if "points" not in p:
            lines.append(f"    {{{head}}}{comma}")
            continue
        pts = ",\n".join(f"      [{x:.2f}, {y:.2f}, {b:.2f}]" for x, y, b in p["points"])
        lines.append(f'    {{{head}, "points": [\n{pts}\n    ]}}{comma}')
    lines += ["  ]", "}", ""]
    path.write_text("\n".join(lines), encoding="utf-8")


def report(doc, car, props, reach):
    """A line a prop: its contrast, its weakest edge, how far each side sits off the rim the search finds within
    `reach` (the median over the samples clear enough to count; - where none is), and what is wrong with it."""
    for end in ("rear", "front"):
        mine = [p for p in props if fit.end_of(doc, car, p) == end]
        if not mine:
            continue
        print(f"{end}: axisY {doc['axisY'][end]:.2f}")
        for p in mine:
            sides = fit.rings(doc, car, p)
            area = fit.coverage(sides, car["rgba"].shape[:2]).sum()
            if "part" in p:
                print(f"  {p['name']:<12} the {p.get('side', 'outboard')} {float(p.get('share', 0.5)):.2f} of "
                      f"{p['part']}  {area:.0f} px")
                continue
            # The part as snap judges it, learnt off both sides; where the copy would sit better, off the authored
            # side alone, so the copy's own placement does not teach the answer.
            at = fit.copy_at(doc, car, p)
            pull = fit.pull_of(car, p)
            like, own = fit.likeness(car, sides, pull), fit.likeness(car, sides[:1], pull)
            held = set(p.get("hold", []))
            per = fit.edge_contrasts(p["points"], like)
            free = [i for i in range(len(per)) if i not in held] or list(range(len(per)))
            worst = min(free, key=per.__getitem__)
            twin = fit.fit_of(fit.mirrored(p["points"], at), like, None, held) if p["mirror"] else None
            wrong = fit.smooth_enough(sides[0])
            gaps = [fit.rim_gap(q, like, reach) for q in fit.sides_of(doc, car, p)]
            gap = "/".join("-" if g is None else f"{g:.2f}" for g in gaps)
            frame = "/".join(f"{fit.framed(car, r):.0%}" for r in sides)
            print(f"  {p['name']:<12} {len(per):>2} pts  edge contrast{' by frame' if pull == 'frame' else ''} "
                  f"{sum(per[i] for i in free) / len(free):.2f}"
                  + (f", copy {twin:.2f}" if twin is not None else "")
                  + f"  weakest e{worst} {per[worst]:.2f}  off its rim {gap} px  framed {frame}"
                  + (f"  held {fit.edge_list(held)}" if held else "") + (" locked" if p.get("lock") else "")
                  + (f"  own axisY {at[0]:.2f} mirrorDx {at[1]:+.2f}" if "axisY" in p or "mirrorDx" in p else "")
                  + f"  {area:.0f} px" + (f"  NOT A LENS: {wrong}" if wrong else ""))
            if p["mirror"] and (hint := fit.better_copy(p, own, at)):
                print(f"      {hint}")
        for line in overlaps(doc, car, end, {p["name"] for p in mine}):
            print(line)


def overlaps(doc, car, end, names):
    """Every pair of an end's props that share texels, one of them in `names`: how many, and whether the file says
    they should — one a part of the other, or either naming the other in its `"over"`."""
    import itertools

    shape = car["rgba"].shape[:2]
    here = [p for p in doc["props"] if fit.end_of(doc, car, p) == end]
    cover = {p["name"]: fit.coverage(fit.rings(doc, car, p), shape) for p in here}
    lines = []
    for a, b in itertools.combinations(here, 2):
        if not {a["name"], b["name"]} & names:
            continue
        shared = float((cover[a["name"]] * cover[b["name"]]).sum())
        if shared < OVERLAP_PX:
            continue
        why = ("a part of it" if a.get("part") == b["name"] or b.get("part") == a["name"]
               else "declared" if b["name"] in a.get("over", []) or a["name"] in b.get("over", []) else None)
        lines.append(f"  overlap {a['name']} / {b['name']}: {shared:.0f} px, "
                     + (why or f'NOT DECLARED — `"over": ["{b["name"]}"]` on {a["name"]} if the art draws one lens '
                                f"inside the other's housing, else pull them apart"))
    return lines


def edge_table(doc, car, p, reach):
    """Every edge of one prop: its ends, chord and bow, its contrast on each side, and where the rim search snap and
    the unrolled strips make, within `reach`, finds its rim at each end — positive out — with the bow that would follow
    it: both sides together, which is what snap moves by, and each side alone where they disagree."""
    pts = p["points"]
    at = fit.copy_at(doc, car, p) if p["mirror"] else None
    like = fit.likeness(car, fit.rings(doc, car, p), fit.pull_of(car, p))
    own = fit.edge_contrasts(pts, like)
    copy = fit.mirrored(pts, at) if at else None
    twin = fit.edge_contrasts(copy, like) if at else None
    held, n = set(p.get("hold", [])), len(pts)
    print(f"  {'edge':<10}{'from':>18}{'to':>18}{'chord':>7}{'bow':>7}  contrast{'/copy' if at else ''}"
          f"   rim found at its two ends (+ out)")
    for i in range(n):
        p0, p1 = fit.edge_ends(pts, i)
        rim = "held" if i in held else ""
        if not rim:
            rim = rim_text(fit.edge_fit(p0, p1, pts[i][2], fit.winding(pts), like, at, reach))
            if at:
                one = fit.edge_fit(p0, p1, pts[i][2], fit.winding(pts), like, None, reach)
                c0, c1 = fit.edge_ends(copy, i)
                other = fit.edge_fit(c0, c1, copy[i][2], fit.winding(copy), like, None, reach)
                apart = abs((one[0] + one[1]) - (other[0] + other[1])) / 2
                if one[4] and other[4] and apart > fit.LOPSIDED_PX:
                    rim += (f"; sides {apart:.1f} apart (over {fit.LOPSIDED_PX:g}), so halfway: authored {rim_text(one, False)}, "
                            f"copy {rim_text(other, False)}")
        print(f"  e{i} v{i}-v{(i + 1) % n:<4}({pts[i][0]:7.2f},{pts[i][1]:7.2f})"
              f" ({pts[(i + 1) % n][0]:7.2f},{pts[(i + 1) % n][1]:7.2f}){math.hypot(*(p1 - p0)):7.1f}"
              f"{pts[i][2]:+7.2f}  {own[i]:.2f}" + (f"/{twin[i]:.2f}" if twin else "     ") + f"   {rim}")


def rim_text(found, whole=True):
    o0, o1, bend, rms, conf = found
    if not conf:
        return "none clear enough"
    return f"{o0:+.2f} {o1:+.2f}" + (f", bow {bend:+.2f}, rms {rms:.2f}" if whole else "")


def cut(base, folder, doc, car):
    import numpy as np
    from PIL import Image

    solid = (car["rgba"][..., 3] > 0.5).astype(np.float32)
    by_kind = view.kind_coverage(car, doc)
    for kind, cov in by_kind.items():
        path = folder / f"{car['id']}_{kind}.png"
        Image.fromarray((cov * solid * 255).round().astype(np.uint8)).save(path)
        print(f"{_sim.rel(base, path)}: {float((cov * solid).sum()):.0f} px")
    im = Image.fromarray((view.lit(car, doc, by_kind) * 255).astype(np.uint8))
    out = folder / f"{car['id']}_lit.png"
    im.resize((im.width * 2, im.height * 2), Image.NEAREST).save(out)
    print(f"{_sim.rel(base, out)}: the whole car lit, every prop on")
    return 0


if __name__ == "__main__":
    cli.entry(main)
