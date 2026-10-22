"""
Cut a car's props (its lamps) out of its sprite as arc polygons, traced off the art, then looked at and adjusted.

**What a prop is for.** The game lights a prop when its lamp is on, so a prop is the lens the art drew, all of it and
nothing else: a brake lights red over its lens, an indicator blinks amber over its own. Every prop is:

- **symmetric**: drawn once and mirrored (`"mirror": true`), never authored twice. A car is symmetric about its
  centreline and so are its lamps; where the art is not, the shape settles between the two sides.
- **smooth**: a few arcs, near convex, its corners rounded; show flags and snap refuses anything else.
- **simple**: as few points as the lens's shape takes — a rounded rectangle or a wedge 4–6, a crescent 5–7, since
  every corner is rounded ROUND_PX anyway. A vertex is a corner or a change of curve the lens has, never a texel's
  step in its frame or a bump where the two sides disagree. Fewer points a little off the rim beat more points on it,
  while every side stays within half a texel.
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

  Seed writes each lamp it finds no lens for as that part. A part never lies over another lamp the art drew in the
  same housing: one declared over the main lamp (`"over"`) is left out of it, and for one that is not, take a
  smaller `share`, or the other `side`.

**The loop**, one car, in this order. Each step names what to look at, and nothing else is looked at.

**What seed and trace give is a first probe, not a cut.** They read colour classes, so they start you near each
lens rather than from nothing; they also take a highlight for a lens, miss a tinted glass, run a lens on into a
neighbour, stop short of a frame, or settle a lopsided pair halfway. Outlining each lamp correctly is your work:
every prop is checked against the art on both sides and corrected until it is the lens the art drew, whatever the
tool said of it.

1. `seed CAR` lays a first outline of every lamp it can find: the main lamp from the end's biggest patch of its
   colour with the patches of its housing, an indicator from the biggest amber patch, a reverse from a pale patch on
   the brake's housing, a lamp lying inside another's housing declared over it, each copy placed on the other side's
   own lens. Each lamp it finds no lens for is a part. It prints what it traced, each end's lens patches with a texel
   deep in each, and the report, and draws both ends (`<car>_show.png`): look at it once, and decide what the art
   draws at each end — which lenses, of what colour, which share a housing, which lamps have none — and so which
   props are wrong (another lens, too little or too much of it) or missing (a lens a part stands in for).
2. Retrace a wrong or missing one from the patch list: `trace CAR --prop=P --at=X,Y` with the texel it gives;
   several joined by `+` for a housing of several colours, which is traced as one. `--lens=frame` for a pale lens on
   pale paint, `--lens=shade` for a red lens on red paint, where trace says its colour runs on, or with a texel of a
   tinted glass (and `+` one of its highlight) where only the highlight was traced. A lamp the art draws no lens for
   is a part: write it as one in the file. A lens trace cannot read at all (no patch of its colour) is placed by
   hand: points from the rulers, a little inside the lens and over all of it, 4–8 of them, bows 0, then `snap --prop`.
3. Check every prop with points, one look each: `show CAR --prop=P --bare`, both sides with the art unmarked beside
   them. Ask of each side: does the paint fill the lens to its frame (or, with none, to the edge of its colour) and
   stop there, along every edge, tips and corners included? And is every vertex one the lens has: a corner, a tip, a
   change of curve — or a kink on a run the art draws straight or evenly bent? Read the report beside it — **off its
   rim** over 0.5 on a side, edge contrast (the prop's) under 0.1, **framed** under 30 % on a lamp the `dark` panel
   (white is frame) shows a frame round, NOT A LENS, an overlap not declared, SPARE — and the edge table, which names
   the edges and how far their rim is. Correct by the cheapest move that answers what you saw: `snap --prop`
   (`--edges` for the edges named; run again, it goes on from where it stopped), `move --grow` by the rim the table
   gives, `move --vertex` or `bow` for a tip or a corner short of the lens, `drop --vertex` and a snap of the edge
   that spans it for a spare vertex or a kink, `axis --prop` where the copy alone is off, `"over"` or a move apart
   for an overlap, a retrace from better texels. Snap moves vertices and never takes one out: a kink it leaves is
   yours to drop. Check a fix with `--text`, and look again once the numbers say it is done, to see that it is.
4. A prop is done when you have seen it on the lens on both sides, the report names no SPARE vertex and you see no
   kink on a run the art draws smooth, and it is off its rim by at most 0.5 there,
   whatever framed says (a tinted band between a glass and its frame keeps framed low); or by about a texel on one
   side where the sides draw the lens differently (lopsided art: the shape settles between them); or where its table
   finds no rim on most edges (art with nothing to fit: say so rather than try again). A done prop takes no hint:
   snap's dropped passes are snap refusing a worse shape.
5. `cut CAR`, and look at `<car>_lit.png` once: each lamp lit over its lens, the size the art drew it. Every lamp is
   on; a part burns on its copy side only, so the authored side shows its main lamp whole.
6. `install CAR`: the game's lamps. The car's zone map is painted from the props (a channel a glass colour, so a
   section inside a housing burns with both), each prop's lens a side becomes a `lamps` entry boxed on its texels,
   and the props file is kept beside the sprite's variant under raw_assets/. It refuses a lens reaching past the
   lamp sheet's cell (20 texels from its centre) or more than ten. A new picture (`qq sprites use`) drops them.

A look at a prop (`show --prop`, with `--bare` for the art unmarked beside it) paints what it covers in its outline's
colour, see-through: what the game will light, which should fill the lens to its frame and stop there, on both
sides.

**Look only to answer a question you can name** ("is that amber patch a lens?"), never to see how it looks again.
The text answers most questions: read it first. One look a question, plain first; `dark` asks "is there a frame",
`sat` or `hue` "is this amber or red". A picture costs about width × height / 750 tokens; `--max-px` fits the zoom
to a budget, and each sheet's cost is printed. `--text` on any action draws none.

**Reading the text.**

- A report line a prop: its points; **edge contrast**, the score just inside its edges less just outside, 0 to 1,
  and the copy's; its weakest edge; **off its rim**, a side: the median distance in texels from the outline to the
  rim the search finds within `--reach` (judge it at the default: a longer reach finds farther rims); **framed**, a
  side: the share of the texels just outside it that are
  frame-dark — half or more on a drawn outline; under 30 % on a lamp with a frame, the outline is short of it or over
  it; low and right on a lens with no frame of its own (a section inside a housing, a lens against paint);
  held and locked edges; its own axis; its pixels, both sides; NOT A LENS.
- **SPARE vN**, under a prop: the vertex it does best without — dropped, the one edge that then joins its
  neighbours lies within ON_RIM_PX of the rim on both sides, its contrast within SPARE_CONTRAST_LOSS of the weaker
  edge it replaces, and the shape stays a lens's — with the drop and the snap that settle it. Drop one at a time,
  check with `--text`, and `undo` a drop the numbers or a look say cut a corner or a tip; the report then names the
  next.
- **Overlaps**, an end: each pair of props sharing texels, how many, and whether it is declared.
- The **edge table** (show, snap or an edit of one prop): each edge's ends, chord, bow, contrast a side, and where
  its rim is found at each end, + out — what snap moves it by, and `move --edge --grow` by their mean by hand. Where
  the sides' rims are over LOPSIDED_PX apart it gives each, and the rim taken is halfway. A rim at ±`--reach` wants
  a longer one.
- Contrast is weak where a pale lens meets pale paint or red meets red.

**The props file** (one a car, under `--dir`) is in sprite texel coordinates, 0,0 the top-left corner and a texel's
centre at +0.5, so a number read off a ruler is the number to type:

    {"name": "brake", "kind": "brake", "mirror": true, "points": [[x, y, bow], ...]}

Vertex vN is points[N]; edge eN runs from vN to vN+1 (the last back to v0), and its bow is vN's third number: how
far, in texels, the edge's middle stands off its chord, positive out, at most half the chord. A copy's parts carry the
names of the ones they mirror. Corners are rounded ROUND_PX along their edges, or by the prop's `"round"`. Other keys:
`"axisY"` and `"mirrorDx"` (the prop's own copy placement, which `axis --prop` sets), `"pull": "frame"`, `"over":
["brake"]` (an overlap declared), `"hold": [2]` (edges kept as written and out of the fit: one that crosses a lens
rather than following its rim), `"lock": true` (out of snap).

    seed CAR                      a first outline of every lamp the art shows a patch of its colour for, the rest parts
                                  (a lamp of the car file of no kind seed traces stays a rectangle where the file
                                  puts it, which is misplaced); each end's axis from its main lamp's two sides
    find CAR [--end=rear]         each end's lens-coloured patches, big first: colour, size, box, a texel deep in it,
                                  how framed, and the props over it
    trace CAR --prop=P --at=X,Y   P traced off the lens at X,Y, both sides: replaced (a part becomes a lens), or
                                  added, its kind read off its name; X,Y+X,Y for several patches as one lens
    snap CAR [--prop=P]           edges pulled onto the part's rim; without --prop, the ends' axes too
    snap CAR --prop=P --edges=1,2   only e1 and e2, the rest held for this snap
    axis CAR [--prop=P]           the ends' mirror axes refitted; with --prop, that prop's copy placed on its own
    move CAR --prop=P --vertex=3 --by=0.5,-1   v3 shifted (--edge: both its ends; neither: all of it)
    move CAR --prop=P --edge=2 --grow=0.5      e2 pushed out (negative in), its ends sliding along the edges beside
                                  it; --vertex along its corner's bisector; neither: the whole prop grown
    bow CAR --prop=P --edge=2 --bow=1.5        e2's bow set
    split CAR --prop=P --edge=2   e2 halved on its arc, a new v3 at its middle (later parts renumber)
    drop CAR --prop=P --vertex=3  v3 taken out, the edge joining its neighbours bowed through where it was
    undo CAR                      back one seed, trace, snap, axis or edit (again to redo)
    cut CAR                       a coverage PNG a kind, clipped to the body, and the car lit, prop by prop, a
                                  smaller one burning over a bigger
    install CAR                   the zone map and `lamps` written from the props, the props kept under raw_assets/
    show CAR [--end=rear]         both ends; or one end, a view a side
    show CAR --prop=P             one prop, a view a side, its parts named
    show CAR --prop=P --vertex=N  round one vertex
    show CAR --prop=P --edge=N    the edge turned flat, outside up, and under it unrolled into a strip: rows texels
                                  out from it, the edge at 0, a dot where each sample finds that side's rim (a grey
                                  cross: too faint to count), and the curve snap would fit through them
    show CAR --at=X,Y             round any texel

Every trace, edit and snap is mirrored with its prop. Panels (`--filter`): plain, stretch, luma, sat, hue, edges,
dark (what counts as frame), likeness (what snap pulls to), alpha, lit. Over the image are the grid (`--grid`) and
the marks (`--marks`), and `--bare` adds each panel unmarked. A turned view is ruled along and up from its part.

**How trace works.** The lens is the patch of the seed texel's colour class (red, amber, pale) joined to it, its
highlights and small bays filled, a pale one grown out through the tinted glass round it (`glass`); where that is
paint's size, the inside of the frame round the seed, then the seed's own few shades. The other side's lens is read near the mirror image and the copy placed where the two agree; the
outline settles halfway where they differ, as a few arcs, smoothed until it is a lens's shape.

**How snap works.** It learns the part from the placement (the colours two texels in against a ring three to seven
out) and moves each edge, within `--reach`, to where that likeness falls and the frame rises; a pass that lowers the
fit or leaves no lens's shape is dropped, which snap says. Hence a hand placement goes a little inside and over all
of the lens. `--pull=frame` goes by the dark frame alone, for a pale lens on pale paint, and stays on the prop.
"""

import argparse
import json
import math
import re
import shutil
from pathlib import Path

import _props_fit as fit
import _props_trace as trace
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
#: Two props sharing fewer texels than this, both sides together, only touch: their rounded rims meet, or two lenses
#: traced from patches side by side share the half texel each outline stands over its own.
OVERLAP_PX = 12
#: The lamps every car carries that a car file may list none of, as seed writes them: the manual's fallback part of
#: the main lamp, (prop, kind, its main lamp, side, share), until the art shows a lens of its own.
FALLBACKS = (("ind_front", "ind", "head", "outboard", 0.5), ("ind_rear", "ind", "brake", "outboard", 0.5),
             ("reverse", "reverse", "brake", "inboard", 0.33))
#: How far either way of the body's middle row seed looks for each end's mirror axis, in rows.
SEED_AXIS_PX = 6
#: What seed traces at each end: the main lamp from the end's biggest patch of its colour of at least LENS_MIN_PX,
#: with the patches of these colours it touches (its housing); an indicator from the biggest amber patch of
#: AMBER_MIN_PX, a side marker's size past; a reverse from a pale one of REVERSE_MIN_PX, past a highlight's size,
#: touching the brake's housing.
MAIN = {"rear": ("brake", "r", "ra"), "front": ("head", "w", "w")}
LENS_MIN_PX, REVERSE_MIN_PX, AMBER_MIN_PX = 10, 16, 20
#: Another red patch at the rear, this big and this framed, is a brake lamp of its own.
OTHER_RED_PX, OTHER_RED_FRAMED = 40, 0.4
#: A traced lens lying this much inside another prop is a section of that one's housing: it is declared over it.
SECTION_SHARE = 0.8
#: How many of an end's patches find lists.
SURVEY_ROWS = 8


def main():
    a = parse_args()
    base = _sim.root()
    if not ID.match(a.car):
        cli.fail(f"{a.car!r} is not a car id")
    folder = paths.resolve_in_root(a.dir, base)
    if not folder.is_relative_to(base / ".tmp"):
        cli.fail(f"props are worked on under .tmp/ and kept by `install`: {_sim.rel(base, folder)} is not")
    folder.mkdir(parents=True, exist_ok=True)
    car = load_car(base, a.car)
    car["pull"] = a.pull
    doc_path = folder / f"{a.car}.json"
    if a.action == "seed":
        if doc_path.exists() and not a.force:
            cli.fail(f"{_sim.rel(base, doc_path)} exists: `--force` to seed over it (undo keeps one step)")
        doc, notes = seeded(car)
        save(doc_path, doc, backup=doc_path.exists())
        print(f"{_sim.rel(base, doc_path)}: seeded, a first outline of each lamp traced off the art where a patch of its "
              "colour shows it: a probe to start from, each to check against the art and correct")
        for line in notes:
            print(line)
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
    if a.action == "find":
        for end in ("rear", "front") if a.end == "both" else (a.end,):
            for line in survey(doc, car, end):
                print(line)
        return 0
    if a.action == "trace":
        if a.prop is None or a.at is None:
            cli.fail("trace needs --prop and --at: the prop, and a texel of its lens (`find` lists them)")
        if not ID.match(a.prop):
            cli.fail(f"{a.prop!r} is not a prop name")
        p, line = trace_prop(doc, car, a.prop, a.at, a.lens)
        if p is None:
            cli.fail(line)
        print(line)
        save(doc_path, doc, backup=True)
        a.at = None
    elif a.at is not None:
        if len(a.at) > 1:
            cli.fail("--at frames one texel; several are for trace")
        a.at = a.at[0]
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
    if a.action == "install":
        return install(base, doc_path, doc, car)
    tag ="_".join(str(t) for t in (a.prop, f"v{a.vertex}" if a.vertex is not None else None,
                                     f"e{a.edge}" if a.edge is not None else None,
                                     f"at{a.at[0]:g}_{a.at[1]:g}" if a.at else None,
                                     a.end if a.end != "both" and not a.prop and not a.at else None) if t)
    out = folder / (f"{a.car}_{tag}_show.png" if tag else f"{a.car}_show.png")
    report(doc, car, [one] if one else [p for p in doc["props"] if a.end in ("both", fit.end_of(doc, car, p))],
           a.reach)
    if one is not None and "points" in one and a.action != "trace":
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
        description="Cut a car's props (its lamps) out of its sprite as arc polygons, traced off the art, then looked "
                    "at and adjusted.",
        epilog=__doc__.split("\n", 2)[2],
        allow_abbrev=False,
        formatter_class=Help,
    )
    parser.add_argument("action", choices=["seed", "find", "trace", "show", "snap", "axis", "move", "bow", "split", "drop",
                                           "undo", "cut", "install"],
                        help="seed and trace every lamp it can, list each end's lens patches, trace one prop off the "
                             "art, look, pull onto the part, refit the axes, edit a part, step back, cut, or install "
                             "into the game")
    parser.add_argument("car", help="the car's id, a folder under assets/agents/car/variants/")
    parser.add_argument("--prop", default=None, help="the prop of this name: shown close up, snapped, or edited")
    parser.add_argument("--vertex", type=cli.counting, default=None, metavar="N",
                        help="the prop's vertex vN: shown close up, moved or dropped")
    parser.add_argument("--edge", type=cli.counting, default=None, metavar="N",
                        help="the prop's edge eN: shown flat and unrolled, moved, bowed or split")
    parser.add_argument("--at", type=texel_list, default=None, metavar="X,Y",
                        help="show round this texel (without --prop); trace: a texel of the lens, or several joined "
                             "by + for a housing of several colours (X,Y+X,Y)")
    parser.add_argument("--lens", default="auto", choices=["auto", "colour", "frame", "shade"],
                        help="trace: what tells the lens from what is round it — its colour class, the dark frame "
                             "round it (a pale lens on pale paint), or its own few shades (a red lens on red paint); "
                             "auto takes the first that stays within the lamp")
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
    parser.add_argument("--marks", default="fill", choices=["fill", "full", "lines", "off"],
                        help="what is drawn over the image: each prop's coverage painted its outline's colour, with "
                             "outlines, dots and part names; no paint; no names either; nothing")
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


def texel_list(text):
    return [tuple(int(v) for v in pair(q, 0, 4096)) for q in text.split("+")]


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
    traced = json.loads(src.read_text(encoding="utf-8"))
    ppm, variant = traced["pxPerM"], traced["variant"]
    rgba = np.asarray(Image.open(folder / d["sprite"]).convert("RGBA")).astype(np.float32) / 255
    out = {"id": car, "file": d, "rgba": rgba, "ppm": ppm, "variant": variant, "end": int(round(END_M * ppm))}
    a = rgba[..., 3:4]
    out["colour"] = colour = rgba[..., :3] * a + np.array(fit.GROUND, np.float32) * (1 - a)
    out["dark"] = np.clip((fit.DARK_VAL_NONE - colour.max(-1)) / (fit.DARK_VAL_NONE - fit.DARK_VAL_FULL), 0, 1)
    out["classes"] = trace.classes(out)
    return out


def sprite_cut_on(car):
    """The picture a props file is cut on: the variant it was made from, and its size in texels."""
    h, w = car["rgba"].shape[:2]
    return {"variant": car["variant"], "sizePx": [w, h]}


def onto_sprite(doc, car):
    """The props moved onto the car's sprite as it now stands: the same drawing installed at another size is scaled
    to it (`qq sprites use` puts a car on the art grid), another drawing refused. A file from before it named its
    picture is taken as cut on this one. True if it was scaled."""
    now, was = sprite_cut_on(car), doc.get("sprite")
    doc["sprite"] = now
    if was is None or was["sizePx"] == now["sizePx"]:
        return False
    if was["variant"] != now["variant"]:
        cli.fail(f"{car['id']}'s props were cut on {was['variant']}, and its sprite is now {now['variant']}: "
                 f"`qq props seed {car['id']} --force` and cut it again")
    kx, ky = now["sizePx"][0] / was["sizePx"][0], now["sizePx"][1] / was["sizePx"][1]
    doc["axisY"] = {e: y * ky for e, y in doc["axisY"].items()}
    for p in doc["props"]:
        if "points" in p:
            p["points"] = [[x * kx, y * ky, b * (kx + ky) / 2] for x, y, b in p["points"]]
        if "axisY" in p:
            p["axisY"] *= ky
        if "mirrorDx" in p:
            p["mirrorDx"] *= kx
    return True


def end_columns(car, end):
    w = car["rgba"].shape[1]
    return slice(0, car["end"]) if end == "rear" else slice(w - car["end"], w)


def kind_of(name):
    kind = next((k for k in ("head", "brake", "reverse", "ind") if name.startswith(k)), None)
    if kind is None:
        cli.fail(f"a new prop's kind is read off its name, which starts head, brake, reverse or ind: {name!r} does not")
    return kind


def trace_prop(doc, car, name, seeds, by):
    """The prop of this name traced off the art from texels of its lens: replaced if there is one (a part becomes a
    lens of its own), added if not; its copy placed where the copy side draws the same lens, and declared over the
    prop it lies inside. (the prop, what was done as a line), or (None, why)."""
    import numpy as np

    w = car["rgba"].shape[1]
    end = "front" if seeds[0][0] >= w / 2 else "rear"
    if any((x >= w / 2) != (end == "front") for x, _ in seeds):
        return None, "a lens's texels are all at one end"
    axis = doc["axisY"][end]
    t, why = trace.traced(car, seeds, axis, by)
    if t is None:
        return None, why
    p = next((q for q in doc["props"] if q["name"] == name), None)
    if p is None:
        p = {"name": name, "kind": kind_of(name), "mirror": True}
        doc["props"].append(p)
    # Judged on the authored side, which was traced: the copy side may draw a section otherwise. A lens lying in a
    # main lamp's housing is a section over it; any other lens it touches it keeps off, or the two would share the
    # half texel each outline stands over its own.
    shape = car["rgba"].shape[:2]
    others = [q for q in doc["props"] if q is not p and "points" in q and fit.end_of(doc, car, q) == end
              and name not in q.get("over", [])]
    cover = {q["name"]: fit.coverage(fit.rings(doc, car, q)[:1], shape) > 0.5 for q in others}
    host = max(((float((t["mask"] & cover[q["name"]]).sum() / t["mask"].sum()), q["name"]) for q in others
                if q["kind"] in ("brake", "head") and p["kind"] not in ("brake", "head")), default=(0.0, None))
    if host[0] < SECTION_SHARE and (away := np.any(list(cover.values()), 0) if cover else None) is not None \
            and (away & t["mask"]).any():
        t, _ = trace.traced(car, seeds, axis, by, away=away)
    for key in ("part", "side", "share", "hold", "lock", "pull", "round", "axisY", "mirrorDx"):
        p.pop(key, None)
    p["mirror"] = True
    p["points"] = fit.rounded(t["points"])
    said = f"  {name}: traced by {', '.join(t['how'])} from {len(seeds)} texel{'s' if len(seeds) > 1 else ''}, " \
           f"{len(p['points'])} pts"
    if t["copy"] is None:
        said += "; the copy side draws no such lens, so the copy is the end's mirror image"
    else:
        a_y, dx, agree = t["copy"]
        if (a_y, dx) != (axis, 0):
            p["axisY"], p["mirrorDx"] = a_y, float(dx)
        said += f"; the copy side's lens agrees {agree:.0%}, copy at axisY {a_y:.2f} mirrorDx {dx:+d}"
    if host[0] >= SECTION_SHARE:
        p["over"] = [host[1]]
        said += f"; {host[0]:.0%} inside {host[1]}, so a section of its housing, over it"
        if t["copy"] is None:
            # A section the copy side draws otherwise goes where its housing's copy does.
            parent = next(q for q in doc["props"] if q["name"] == host[1])
            for key in ("axisY", "mirrorDx"):
                if key in parent:
                    p[key] = parent[key]
            said += ", its copy placed as the housing's"
    elif "over" in p:
        p.pop("over")
    return p, said


def seeded(car):
    """Every lamp traced off the art where an end shows a patch of its colour: the main lamp from the biggest, with
    the patches of its housing's colours it touches; an indicator from the biggest amber patch; a reverse from a pale
    patch touching the brake's housing. Each other lamp of the car file is a rectangle where the file puts it, to
    place by hand, and each lamp every car carries that is still missing is the part of its main lamp the manual's
    fallback gives it. (the props file, a line on what was traced and from what)."""
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
    # Traced first, among traced lenses only: a rectangle where the car file puts a lamp would push a lens off it.
    doc = {"car": car["id"], "sprite": sprite_cut_on(car), "axisY": {e: seed_axis(car, e) for e in ("rear", "front")},
           "props": []}
    notes, done = [], set()
    for end in ("rear", "front"):
        notes += traced_end(doc, car, end, done)
    # A lamp the art shows no lens for is its fallback part, wherever the car file put it.
    doc["props"] += [p for p in props if p["name"] not in done and p["name"] not in {f[0] for f in FALLBACKS}]
    have = {p["name"] for p in doc["props"]}
    for name, kind, parent, side, share in FALLBACKS:
        if name not in have and parent in have:
            doc["props"].append({"name": name, "kind": kind, "part": parent, "side": side, "share": share})
    if hand := [p["name"] for p in doc["props"] if "points" in p and p["name"] not in done]:
        notes.append(f"  placed from the car file only, to place by hand or trace: {', '.join(hand)}")
    for end in ("rear", "front"):
        notes += survey(doc, car, end)
    return doc, notes


def traced_end(doc, car, end, done):
    """seed's tracing of one end, each prop traced added to `done`; a line a lamp traced or wanting."""
    import numpy as np

    axis = doc["axisY"][end]
    pats = trace.patches(car, end_columns(car, end), axis)
    main, main_class, housing = MAIN[end]
    notes, group = [], []
    first = next((q for q in pats if q[0] == main_class and q[1].sum() >= LENS_MIN_PX), None)
    if end == "rear" and (first is None or first[1].sum() < OTHER_RED_PX):
        # No red lens to speak of: a red-orange brake reads as amber, and the indicator is then a part of it.
        first = next((q for q in pats if q[0] == "a" and q[1].sum() >= OTHER_RED_PX), first)
    if first is not None:
        group = [first]
        while more := [q for q in pats if q[0] in housing and not any(q is g for g in group)
                       and (trace.dilate4(q[1]) & np.any([g[1] for g in group], 0)).any()]:
            group += more
    seeds = {main: [g[2] for g in group]}
    amber = next((q for q in pats if q[0] == "a" and q[1].sum() >= AMBER_MIN_PX), None)
    if amber is not None and not (first is not None and first[0] == "a"):
        seeds[f"ind_{end}"] = [amber[2]]
    if end == "rear" and group:
        body = np.any([g[1] for g in group], 0)
        # A red lens apart from the cluster, framed: a third brake lamp, or one on the tailgate.
        apart = [q for q in pats if q[0] == "r" and q[1].sum() >= OTHER_RED_PX and not any(q is g for g in group)
                 and trace.framed(car, q[1]) >= OTHER_RED_FRAMED]
        for k, q in enumerate(apart[:2]):
            seeds[f"brake{k + 2}"] = [q[2]]
        pale = next((q for q in pats if q[0] == "w" and q[1].sum() >= REVERSE_MIN_PX
                     and (trace.dilate4(q[1]) & body).any() and not (q[1] & trace.holes(body)).any()), None)
        if pale is not None:
            seeds["reverse"] = [pale[2]]
    for name, at in seeds.items():
        if not at:
            continue
        p, line = trace_prop(doc, car, name, at, "auto")
        notes.append(line if p is not None else f"  {name}: not traced, {line}")
        if p is not None:
            done.add(name)
        if p is not None and name == main and "axisY" in p:
            # The main lamp's copy places the end: every other prop there mirrors about it unless it says otherwise.
            doc["axisY"][end] = p.pop("axisY")
            if not p["mirrorDx"]:
                p.pop("mirrorDx")
    return notes


def survey(doc, car, end):
    """An end's lens-coloured patches on the authored side, big first, as lines: what trace reads a lens from."""
    import numpy as np

    axis = doc["axisY"][end]
    shape = car["rgba"].shape[:2]
    cover = {p["name"]: fit.prop_coverage(doc, car, p, shape) > 0.5 for p in doc["props"]
             if fit.end_of(doc, car, p) == end}
    lines = [f"{end}: axisY {axis:.2f}; lens-coloured patches above it, big first: colour, px, box, a texel deep in it "
             "(trace's --at), hue and lightness (0-9) medians, framed, the props over it (a small pale or amber patch "
             "all inside a lens of another colour is a highlight on it)"]
    pats = trace.patches(car, end_columns(car, end), axis)
    hue, light = trace.hue_deg(car["colour"]), car["colour"].max(-1)
    for c, m, (x, y) in pats[:SURVEY_ROWS]:
        ys, xs = np.nonzero(m)
        over = [f"{n} {(m & q).sum() / m.sum():.0%}" for n, q in cover.items() if (m & q).any()]
        tone = f"hue {int(np.median(np.where(hue[m] >= 300, hue[m] - 360, hue[m])))} " if c != "w" else ""
        lines.append(f"  {dict(r='red', a='amber', w='pale')[c]:<5} {int(m.sum()):>4} px  x {xs.min()}-{xs.max()} "
                     f"y {ys.min()}-{ys.max()}  at {x},{y}  {tone}light {min(int(np.median(light[m]) * 10), 9)}  "
                     f"framed {trace.framed(car, m):.0%}" + (f"  {', '.join(over)}" if over else ""))
    if len(pats) > SURVEY_ROWS:
        lines.append(f"  and {len(pats) - SURVEY_ROWS} smaller")
    return lines


def seed_axis(car, end):
    """The row an end's art mirrors about best, within SEED_AXIS_PX of the body's middle: the edges of its lamp-like
    texels (frame-dark, coloured, pale) copied across it. The body's outline alone is often a few rows off its lamps."""
    import numpy as np

    h, w = car["rgba"].shape[:2]
    colour = car["colour"]
    hi = colour.max(-1)
    sat = (hi - colour.min(-1)) / np.maximum(hi, 1e-3)
    lamp = (car["dark"] > 0.5) | ((sat >= 0.35) & (hi > 0.4)) | ((sat < 0.25) & (hi > 0.6))
    lamp &= car["rgba"][..., 3] > 0.5
    cols = np.zeros(w, bool)
    cols[slice(0, car["end"]) if end == "rear" else slice(w - car["end"], w)] = True
    band = fit.window(lamp, 1, np.max) & ~fit.window(lamp, 1, np.min) & cols[None, :]
    mid = round(body_middle(car, end) * 2) / 2
    ys, xs = np.nonzero(band & (np.arange(h)[:, None] + 0.5 < mid))
    best = (math.inf, mid)
    for axis in np.arange(mid - SEED_AXIS_PX, mid + SEED_AXIS_PX + 1e-6, 0.5):
        ys2 = int(round(2 * axis)) - 1 - ys
        ok = (ys2 >= 0) & (ys2 < h)
        if ok.sum() < 0.5 * len(ys):
            continue
        dist = float(np.sqrt(((colour[ys[ok], xs[ok]] - colour[ys2[ok], xs[ok]]) ** 2).sum(-1)).mean())
        best = min(best, (dist, float(axis)))
    return best[1]


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
    lines = ["{", f'  "car": "{doc["car"]}",'] + ([f'  "sprite": {json.dumps(doc["sprite"])},'] if "sprite" in doc else []) + [
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
            if spare := fit.spare_vertex(doc, car, p, like, reach):
                j, sides = spare
                print(f"      SPARE v{j}: the edge joining its neighbours off its rim "
                      f"{'/'.join(f'{g:.2f}' for g in sides)} px; "
                      f"`drop --prop={p['name']} --vertex={j}`, then `snap --prop={p['name']} "
                      f"--edges={(j - 1) % (len(p['points']) - 1)}`")
        for line in overlaps(doc, car, end, {p["name"] for p in mine}):
            print(line)


def overlaps(doc, car, end, names):
    """Every pair of an end's props that share texels, one of them in `names`: how many, and whether the file says
    they should — one a part of the other, or either naming the other in its `"over"`."""
    import itertools

    shape = car["rgba"].shape[:2]
    here = [p for p in doc["props"] if fit.end_of(doc, car, p) == end]
    cover = {p["name"]: fit.prop_coverage(doc, car, p, shape) for p in here}
    lines = []
    for a, b in itertools.combinations(here, 2):
        if not {a["name"], b["name"]} & names:
            continue
        # Texels a share covered, multiplied: an edge texel half under each counts a quarter.
        shared = float((cover[a["name"]] * cover[b["name"]]).sum())
        if shared < OVERLAP_PX:
            continue
        why = ("a part of it" if a.get("part") == b["name"] or b.get("part") == a["name"]
               else "declared" if b["name"] in a.get("over", []) or a["name"] in b.get("over", []) else None)
        part = next((q for q in (a, b) if "part" in q), None)
        fix = (f'a part never lies over a lens: `"over": ["{part["part"]}"]` on {(b if part is a else a)["name"]} if '
               f"it is a section of {part['part']}'s housing (the part then leaves it out), else a smaller `share` or "
               f"the other `side` for {part['name']}" if part else
               f'`"over": ["{b["name"]}"]` on {a["name"]} if the art draws one lens inside the other\'s housing, else '
               f"pull them apart")
        lines.append(f"  overlap {a['name']} / {b['name']}: {shared:.0f} px, " + (why or f"NOT DECLARED — {fix}"))
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
    im = Image.fromarray((view.lit(car, doc) * 255).astype(np.uint8))
    out = folder / f"{car['id']}_lit.png"
    im = im.resize((im.width * 2, im.height * 2), Image.NEAREST)
    im.save(out)
    tokens, _ = view.reader_cost(im.width, im.height)
    print(f"{_sim.rel(base, out)}: the whole car lit, every prop on, a part on its copy side only; "
          f"{im.width}x{im.height} px, about {tokens} tokens "
          "to look at")
    return 0


def install(base, doc_path, doc, car):
    """The props into the game: the car's zone map painted from them, a lens a side of each in its `lamps`, and the
    props file kept under raw_assets/ as what both were made from."""
    import numpy as np
    from PIL import Image

    if onto_sprite(doc, car):
        print(f"{_sim.rel(base, doc_path)}: scaled onto the sprite at {doc['sprite']['sizePx'][0]}x"
              f"{doc['sprite']['sizePx'][1]} px")
    save(doc_path, doc, backup=True)
    h, w = car["rgba"].shape[:2]
    solid = car["rgba"][..., 3] > 0.5
    zones = np.zeros((h, w, 4), np.uint8)
    zones[..., 3] = 255
    foot = car["file"]["footprintM"]
    per_m = (w / foot[0], h / foot[1])
    rows, problems = [], []
    for p in doc["props"]:
        cov = (fit.prop_coverage(doc, car, p, (h, w)) > 0.5) & solid
        zones[cov, INSTALL_GLASS[p["kind"]]] = 255
        # A prop drawn once (lopsided art: one a side) and its parts cover one side, a mirrored one both.
        both = fit.root_of(doc, p)["mirror"]
        for side, rows_of in (("top", slice(0, h // 2)), ("bottom", slice(h // 2, h))):
            ys, xs = np.nonzero(cov[rows_of])
            if not len(xs):
                if both:
                    problems.append(f"{p['name']}: nothing of it on the {side} side's paint")
                continue
            ys = ys + rows_of.start
            x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
            cx, cy = round((x0 + x1) / 2), round((y0 + y1) / 2)
            # The bake finds a lens's texels only within a cell centred on it.
            reach = max(cx - x0, x1 - cx, cy - y0, y1 - cy)
            if reach > INSTALL_CELL_PX // 2:
                problems.append(f"{p['name']} ({side}): reaches {reach} px from its centre, past the lamp sheet's "
                                f"{INSTALL_CELL_PX // 2}")
            rows.append((INSTALL_FITTING[p["kind"]], (round(cx / per_m[0] - foot[0] / 2, 3), round(cy / per_m[1] - foot[1] / 2, 3)),
                         (round((x1 - x0) / per_m[0], 3), round((y1 - y0) / per_m[1], 3))))
    if len(rows) > INSTALL_MOST_LENSES:
        problems.append(f"{len(rows)} lenses, and a car may draw {INSTALL_MOST_LENSES}")
    if problems:
        cli.fail("not installed:\n  " + "\n  ".join(problems))

    folder = base / VARIANTS / car["id"]
    described = folder / f"{car['id']}.json"
    sprite_src = json.loads((folder / f"{car['id']}.src.json").read_text(encoding="utf-8"))["variant"]
    raw = base / Path(sprite_src).parent / f"{car['id']}.props.json"
    shutil.copy(doc_path, raw)
    zones_name = f"{car['id']}_zones.png"
    Image.fromarray(zones, "RGBA").save(folder / zones_name)
    (folder / f"{car['id']}_zones.src.json").write_text(json.dumps(
        {"variant": _sim.rel(base, raw), "cutOn": sprite_src, "pxPerM": car["ppm"], "encoding": "png lossless"},
        indent=1) + "\n", encoding="utf-8")

    text = described.read_text(encoding="utf-8")
    if '"zones"' in text:
        text = re.sub(r'"zones":\s*"[^"]*"', f'"zones": "{zones_name}"', text, count=1)
    else:
        text, n = re.subn(r'(\n([ \t]*)"sprite":\s*"[^"]*",)', rf'\1\n\2"zones": "{zones_name}",', text, count=1)
        if not n:
            cli.fail(f"cannot place a zones line after the sprite in {_sim.rel(base, described)}")
    lamps = ",\n".join(f'    {{ "fitting": "{f}", "atM": [{x}, {y}], "sizeM": [{sw}, {sh}] }}' for f, (x, y), (sw, sh) in rows)
    block = f'"lamps": [\n{lamps}\n  ]'
    if (at := text.find('"lamps"')) >= 0:
        depth, end = 0, text.index("[", at)
        for end in range(end, len(text)):
            depth += {"[": 1, "]": -1}.get(text[end], 0)
            if depth == 0:
                break
        text = text[:at] + block + text[end + 1:]
    else:
        text, n = re.subn(r'\n\}\s*$', f',\n  {block}\n}}\n', text.rstrip() + "\n", count=1)
        if not n:
            cli.fail(f"cannot place a lamps array at the end of {_sim.rel(base, described)}")
    described.write_text(text, encoding="utf-8")
    print(f"{_sim.rel(base, folder / zones_name)}: {len(rows)} lenses, the zone map painted from the props")
    print(f"{_sim.rel(base, raw)}: what they were cut from")
    return 0


#: A prop's kind as the bake's glass channel (LampAtlasBake.Holds) and as a fitting of the car file.
INSTALL_GLASS = {"brake": 0, "ind": 1, "head": 2, "reverse": 2}
INSTALL_FITTING = {"brake": "brake", "ind": "indicator", "head": "head", "reverse": "reverse"}
#: LampAtlas.CellPx and CarLamps.MostLenses.
INSTALL_CELL_PX, INSTALL_MOST_LENSES = 40, 10


if __name__ == "__main__":
    cli.entry(main)
