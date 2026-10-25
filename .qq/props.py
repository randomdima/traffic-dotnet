"""
Cut a car's props (its lamps) out of its sprite as arc polygons, traced off the art, then looked at and adjusted; and a
rail vehicle's or a roof's (its fans).

**A rail vehicle's props are its fans, and so are a roof's** (an id under assets/agents/train/variants/, TRN-13; a
prefab's under assets/world/building/prefabs/<look>/ or a civic roof's under assets/world/building/variants/, OBJ-6):
`seed` finds every round dark opening with blades in a pale box (`_props_trace.fan_hubs`) and traces each from its hub;
`find` lists what it found and the near misses. It is a first probe: it takes a roof drain with a pale flange for a
fan and misses one a unit's neighbour shades, so look at every roof, `remove` what is none, and `trace V --prop=fanN
--at=X,Y` from a texel of a missed hub, which reads the dark round opening (`--lens=disc`, an ellipse through the rims
found along rays out of the hub), drawn once; look at it bare and `snap` it as any prop; `install` writes the picture's `fans` — each its hub, sought where the rings round it are most even, and
its rim, the ellipse fitted through the outline — and keeps the props beside the picture's variant. A fan's opening is
drawn over in one of the fans' looks (`assets/world/building/fans/`), its middle on the hub and its edge on the rim, so a
fan the art drew squashed turns round and covers all of it. Put the outline on
the opening's outer rim. A roof's fan is a round grille with blades behind it;
a louvred or square vent, or a twin unit a few texels across, is not one. Nothing else here (lamp kinds, mirror,
audit) applies to either.

**What a prop is for.** The game lights a prop when its lamp is on, so a prop is the lens the art drew, all of it and
nothing else: a brake lights red over its lens, an indicator blinks amber over its own. Every prop is:

- **symmetric**: drawn once and mirrored (`"mirror": true`), never authored twice. A car is symmetric about its
  centreline and so are its lamps. The shape is the authored side's (the upper half), and `snap` fits it there
  alone; `mirror` then places each copy over the copy side's own lamp and paints that lamp from the authored one.
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
neighbour, or stop short of a frame. Outlining each lamp correctly is your work: every prop is checked against the
art on the authored side and corrected until it is the lens the art drew, whatever the tool said of it. The copy side
is `mirror`'s (7): its lamps are painted from these.

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
3. Check every prop with points, one look each: `show CAR --prop=P --bare`, with the art unmarked beside it. Ask of
   the authored side: does the paint fill the lens to its frame (or, with none, to the edge of its colour) and
   stop there, along every edge, tips and corners included? And is every vertex one the lens has: a corner, a tip, a
   change of curve — or a kink on a run the art draws straight or evenly bent? Read the report beside it — **off its
   rim** over 0.5 on a side, edge contrast (the prop's) under 0.1, **framed** under 30 % on a lamp the `dark` panel
   (white is frame) shows a frame round, NOT A LENS, an overlap not declared, SPARE — and the edge table, which names
   the edges and how far their rim is. Correct by the cheapest move that answers what you saw: `snap --prop`
   (`--edges` for the edges named; run again, it goes on from where it stopped), `move --grow` by the rim the table
   gives, `move --vertex` or `bow` for a tip or a corner short of the lens, `drop --vertex` and a snap of the edge
   that spans it for a spare vertex or a kink, `"over"` or a move apart
   for an overlap, a retrace from better texels. Snap moves vertices and never takes one out: a kink it leaves is
   yours to drop. Check a fix with `--text`, and look again once the numbers say it is done, to see that it is.
4. A prop is done when you have seen it on the lens on the authored side, the report names no SPARE vertex and you
   see no kink on a run the art draws smooth, and it is off its rim by at most 0.5 there, whatever framed says (a
   tinted band between a glass and its frame keeps framed low); or where its table finds no rim on most edges (art
   with nothing to fit: say so rather than try again). The copy side's numbers say only how differently that side is
   drawn, which `mirror` paints over. A done prop takes no hint:
   snap's dropped passes are snap refusing a worse shape.
5. `cut CAR`, and look at `<car>_lit.png` once: each lamp lit over its lens, the size the art drew it. Every lamp is
   on; a part burns on its copy side only, so the authored side shows its main lamp whole.
6. `install CAR`: the game's lamps. The car's zone map is painted from the props (a channel a glass colour, so a
   section inside a housing burns with both), each prop's lens a side becomes a `lamps` entry boxed on its texels,
   and the props file is kept beside the sprite's variant under raw_assets/, as `<variant>.props.json` — one a
   picture, never written over by another's. It refuses a lens reaching past the lamp sheet's cell (INSTALL_CELL_PX:
   under 56 texels a side, 28 from its centre) or more than ten. A new picture (`qq sprites use`) drops them; `restore CAR` brings back the props
   kept for the picture the sprite is, and `install` puts them back in the game.
7. `mirror CAR`: the copy side's lamp art painted from the authored side's, each lamp reflected where its prop's copy
   lies, on the picture at its own resolution — the body, its outline and its paint as drawn (paint is never painted
   over paint). Kept as a new variant naming the picture and props it came from, installed, and the same props
   installed onto it. Run it once a car's props are done: the copy side then draws each lens the authored side does.

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
    divide CAR --prop=P --at=X,Y+X,Y --into=Q   P cut along the line through the two texels: P keeps the larger
                                  half, Q (a new prop, or a part of that name replaced) the smaller, the cut held on
                                  both — for a housing past the cell, or a red and an amber section drawn as one.
                                  Into a lamp that is a part (ind_rear, reverse), so the car stays at ten lenses
    remove CAR --prop=P           P taken out: ind_front, ind_rear or reverse becomes its main lamp's part again (its
                                  fallback), anything else (a seeded brake2) goes
    undo CAR                      back one seed, trace, snap, axis, edit or restore; again for the one before, up
                                  to UNDO_STEPS
    cut CAR                       a coverage PNG a kind, clipped to the body, and the car lit, prop by prop, a
                                  smaller one burning over a bigger
    install CAR                   the zone map and `lamps` written from the props, the props kept under raw_assets/
    restore CAR                   the props kept for the sprite's picture back as the working file
    mirror CAR                    the copy side's lamp art painted from the authored side's, kept as a new picture,
                                  installed with these props
    audit all|CAR,CAR             each installed car's kept props against its art: a lens on its frame (ON THE
                                  FRAME, lit as a dark hole), a lens-coloured patch a lens covers only part of
                                  (SHORT), a framed one none covers (UNCUT) — the cars to restore and look at
    show CAR [--end=rear]         both ends; or one end, the authored side (`--copy`: the copy side's view too)
    show CAR --prop=P             one prop, the authored side, its parts named
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
fit or leaves no lens's shape is dropped, which snap says, and so is one the part learnt where it lands scores lower
(a shrink onto a soft lens's bright core); the figures it prints are the report's. Hence a hand placement goes a
little inside and over all of the lens. `--pull=frame` goes by the dark frame alone, for a pale lens on pale paint,
and stays on the prop: `undo`, or a snap with `--pull=part`, takes it off.
"""

import argparse
import json
import math
import re
import shutil
import time
from pathlib import Path

import _props_fit as fit
import _props_trace as trace
import _props_view as view
import _sim
from common import cli, paths

#: Rendering and snapping one car is a few seconds; a whole-car cut with its glow a few more.
TIMEOUT_S = 120

VARIANTS = "assets/agents/car/variants"
#: A rail vehicle's props are its fans, cut here as a car's lamps are and installed as the geometry it turns; and so
#: are a roof's, a prefab's in its look's folder or a civic roof's in its own.
TRAINS = "assets/agents/train/variants"
PREFABS = "assets/world/building/prefabs"
ROOFS = "assets/world/building/variants"
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
    if a.action == "audit":
        return audit(base, a.car)
    if not ID.match(a.car):
        cli.fail(f"{a.car!r} is not a car id")
    folder = paths.resolve_in_root(a.dir, base)
    if not folder.is_relative_to(base / ".tmp"):
        cli.fail(f"props are worked on under .tmp/ and kept by `install`: {_sim.rel(base, folder)} is not")
    folder.mkdir(parents=True, exist_ok=True)
    car = load_car(base, a.car)
    car["pull"] = a.pull
    if car["fans_only"] and a.action == "mirror":
        cli.fail(f"{a.car} is {car['what']}: its props are fans, drawn once, and nothing of it is mirrored")
    doc_path = folder / f"{a.car}.json"
    if a.action == "seed":
        if doc_path.exists() and not a.force:
            cli.fail(f"{_sim.rel(base, doc_path)} exists: `--force` to seed over it (`undo` steps back)")
        if car["fans_only"]:
            doc = seeded_fans(car)
            notes = traced_fans(doc, car)
            said = (f"seeded, every fan found traced from its hub: look at each, `remove` one that is none, and trace "
                    f"one missed, `qq props trace {a.car} --prop=fanN --at=X,Y` (`find` lists the near misses)")
        else:
            doc, notes = seeded(car)
            said = ("seeded, a first outline of each lamp traced off the art where a patch of its colour shows it: a "
                    "probe to start from, each to check against the art and correct")
        save(doc_path, doc, backup=doc_path.exists())
        print(f"{_sim.rel(base, doc_path)}: {said}")
        for line in notes:
            print(line)
        a.action = "show"
    if a.action == "restore":
        kept = kept_props(base, car)
        if not kept.is_file():
            cli.fail(f"no lamps were kept for {car['variant']} ({_sim.rel(base, kept)}): `seed` it")
        if doc_path.exists():
            remember(doc_path)
        shutil.copy(kept, doc_path)
        print(f"{_sim.rel(base, doc_path)}: the lamps kept for {Path(car['variant']).name}, as installed")
        a.action = "show"
    if a.action == "undo":
        _, kept = history(doc_path)
        if not kept:
            cli.fail(f"nothing to undo: {_sim.rel(base, doc_path)} has no earlier state")
        shutil.move(kept[-1], doc_path)
        print(f"{_sim.rel(base, doc_path)}: back one step, {len(kept) - 1} more to undo")
        a.action = "show"
    if not doc_path.exists():
        cli.fail(f"no {_sim.rel(base, doc_path)}: `qq props seed {a.car}` first")
    doc = load(doc_path)
    if a.action == "find" and car["fans_only"]:
        for (x, y), r, rounded, blades, why in trace.fan_hubs(car):
            print(f"  {x},{y}: rim voted at {r:.0f} texels, {rounded:.2f} round, {blades:.2f} blades — "
                  f"{why or 'a fan'}")
        return 0
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
    elif a.at is not None and a.action != "divide":
        if len(a.at) > 1:
            cli.fail("--at frames one texel; several are for trace and divide")
        a.at = a.at[0]
    if car["fans_only"] and any(p["kind"] != FAN for p in doc["props"]):
        cli.fail(f"{a.car} is {car['what']}, whose only props are fans: "
                 f"{', '.join(p['name'] for p in doc['props'] if p['kind'] != FAN)} is not one")
    if car["fans_only"] and a.action == "install":
        return install_fans(base, doc_path, doc, car)
    names = {p["name"] for p in doc["props"]}
    for p in doc["props"]:
        if "part" in p and p["part"] not in names - {p["name"]}:
            cli.fail(f"{p['name']} is a part of {p['part']!r}, which is no prop of {_sim.rel(base, doc_path)}")
    chosen = [p for p in doc["props"] if a.prop in (None, p["name"])]
    # A picture seeded empty for its fans is still looked round, to find the hub its first fan is traced from.
    if not chosen and (a.prop is not None or not car["fans_only"]):
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
    if a.action in ("divide", "remove"):
        end = fit.end_of(doc, car, one)
        print(divide(doc, one, a.at, a.into, a.force) if a.action == "divide" else remove(doc, one))
        save(doc_path, doc, backup=True)
        a.end, a.at, a.prop, one = end, None, None, None
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
    if a.action == "mirror":
        return mirror(base, doc_path, doc, car)
    tag ="_".join(str(t) for t in (a.prop, f"v{a.vertex}" if a.vertex is not None else None,
                                     f"e{a.edge}" if a.edge is not None else None,
                                     f"at{a.at[0]:g}_{a.at[1]:g}" if a.at else None,
                                     a.end if a.end != "both" and not a.prop and not a.at else None) if t)
    out = folder / (f"{a.car}_{tag}_show.png" if tag else f"{a.car}_show.png")
    report(doc, car, [one] if one else near(doc, car, a.at, a.pad + AT_NEAR_PX) if a.at else
           [p for p in doc["props"] if a.end in ("both", fit.end_of(doc, car, p))], a.reach)
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
                                           "divide", "remove", "undo", "cut", "install", "restore", "mirror", "audit"],
                        help="seed and trace every lamp it can, list each end's lens patches, trace one prop off the "
                             "art, look, pull onto the part, refit the axes, edit a part, divide a housing in two, "
                             "step back, cut, install "
                             "into the game, restore the lamps kept for the sprite's picture, mirror the authored "
                             "side's lamp art onto the copy side, or audit installed cars' lamps against their art")
    parser.add_argument("car", help="the car's id, a folder under assets/agents/car/variants/, a rail vehicle's "
                                    "under assets/agents/train/variants/, or a roof's: a prefab's under "
                                    "assets/world/building/prefabs/<look>/ or a civic roof's under "
                                    "assets/world/building/variants/; audit: car ids comma-separated, or all")
    parser.add_argument("--prop", default=None, help="the prop of this name: shown close up, snapped, or edited")
    parser.add_argument("--into", default=None, metavar="NAME",
                        help="divide: the new prop the smaller half becomes (a part of that name is replaced)")
    parser.add_argument("--vertex", type=cli.counting, default=None, metavar="N",
                        help="the prop's vertex vN: shown close up, moved or dropped")
    parser.add_argument("--edge", type=cli.counting, default=None, metavar="N",
                        help="the prop's edge eN: shown flat and unrolled, moved, bowed or split")
    parser.add_argument("--at", type=texel_list, default=None, metavar="X,Y",
                        help="show round this texel (without --prop); trace: a texel of the lens, or several joined "
                             "by + for a housing of several colours (X,Y+X,Y)")
    parser.add_argument("--lens", default="auto", choices=["auto", "colour", "frame", "shade", "tint", "disc"],
                        help="trace: what tells the lens from what is round it — its colour class, the dark frame "
                             "round it (a pale lens on pale paint), its own few shades (a red lens on red paint), "
                             "its hue however dark (a beacon's glass, which a beacon_* prop is read by), or a fan's "
                             "dark round opening out from its hub (which a fan* prop is read by); auto takes "
                             "the first that stays within the lamp")
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
    parser.add_argument("--copy", action="store_true",
                        help="show: draw the copy side's view as well as the authored side's (mirror paints it over)")
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
    if a.action == "remove":
        if p is None:
            cli.fail("remove needs --prop")
        return
    if a.action == "divide":
        if p is None or "points" not in p:
            cli.fail("divide needs --prop, a prop with its own outline")
        if a.at is None or len(a.at) != 2 or not a.into:
            cli.fail("divide needs --at=X,Y+X,Y, two texels on the cutting line, and --into, the new prop's name")
        return
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


def divide(doc, p, line, into, force):
    """P cut along the line through `line`'s two texels, which must cross its outline twice: P keeps the larger half,
    `into` the smaller, each with the cut as its last edge, held. A crossed edge's bow is shared out as a parabola's
    sagitta (the share of the chord squared). What it did, as a line."""
    (ax, ay), (bx, by) = line
    pts = p["points"]
    n = len(pts)
    side = [(bx - ax) * (y - ay) - (by - ay) * (x - ax) for x, y, _ in pts]
    crossed = [i for i in range(n) if (side[i] > 0) != (side[(i + 1) % n] > 0)]
    if len(crossed) != 2:
        cli.fail(f"the line {ax:g},{ay:g} - {bx:g},{by:g} crosses {p['name']}'s outline {len(crossed)} times: it must "
                 "cross it twice")
    at = {}
    for i in crossed:
        t = side[i] / (side[i] - side[(i + 1) % n])
        (x0, y0, bow), (x1, y1, _) = pts[i], pts[(i + 1) % n]
        at[i] = (t, [x0 + t * (x1 - x0), y0 + t * (y1 - y0)], bow)

    def half(i, j):
        """From edge i's crossing round the outline to edge j's, then the cut back."""
        ti, xi, bi = at[i]
        tj, xj, bj = at[j]
        out = [[*xi, bi * (1 - ti) ** 2]]
        k = (i + 1) % n
        while True:
            out.append([pts[k][0], pts[k][1], bj * tj ** 2 if k == j else pts[k][2]])
            if k == j:
                break
            k = (k + 1) % n
        out += [[*xj, 0.0]]
        # A cut beside a vertex would leave an edge under a chord, which snap then refuses to move: the vertex goes,
        # and the cut stays where it was asked.
        if len(out) > 3 and math.dist(out[0][:2], out[1][:2]) < fit.MIN_CHORD_PX:
            out[0][2] = out.pop(1)[2]
        if len(out) > 3 and math.dist(out[-2][:2], out[-1][:2]) < fit.MIN_CHORD_PX:
            out.pop(-2)
        return out

    halves = sorted((half(*crossed), half(*crossed[::-1])), key=lambda h: -abs(polygon_area(h)))
    if min(map(len, halves)) < 3:
        cli.fail(f"a half of {p['name']} would have under 3 vertices: move the line further in")
    other = next((q for q in doc["props"] if q["name"] == into), None)
    if other is not None and "points" in other and not force:
        cli.fail(f"{into} is a prop with its own outline: `--force` to replace it")
    if other is None and (lenses := lens_count(doc) + (2 if p["mirror"] else 1)) > INSTALL_MOST_LENSES:
        parts = [q["name"] for q in doc["props"] if "part" in q]
        cli.fail(f"a new prop {into} makes {lenses} lenses, and a car draws {INSTALL_MOST_LENSES}: divide into "
                 + (f"a part ({', '.join(parts)}), which it replaces" if parts else "a prop `remove` has freed"))
    new = {"name": into, "kind": kind_of(into), "mirror": p["mirror"],
           **{k: p[k] for k in ("axisY", "mirrorDx", "pull") if k in p}}
    p["points"], new["points"] = fit.rounded(halves[0]), fit.rounded(halves[1])
    p["hold"], new["hold"] = [len(halves[0]) - 1], [len(halves[1]) - 1]
    p.pop("lock", None)
    if other is not None:
        doc["props"][doc["props"].index(other)] = new
    else:
        doc["props"].insert(doc["props"].index(p) + 1, new)
    return (f"  {p['name']}: divided along {ax:g},{ay:g} - {bx:g},{by:g}; keeps {len(halves[0])} points, "
            f"{into} takes {len(halves[1])}{' (the part it was replaced)' if other is not None else ''}, the cut "
            "held on both")


def near(doc, car, at, reach_px):
    """The props with an outline on either side within `reach_px` of the texel `at`: what `show --at` reports."""
    return [p for p in doc["props"]
            if any(len(r) and min(math.dist(at, q) for q in r) <= reach_px for r in fit.rings(doc, car, p))]


def lens_count(doc):
    """The lenses install writes: a prop or part a side it is drawn on, but a reverse beside traced beacons, whose glass
    is the brake's (install)."""
    beacons = any(p["kind"] in BEACON_KINDS for p in doc["props"])
    return sum(2 if fit.root_of(doc, p)["mirror"] else 1 for p in doc["props"]
               if not (beacons and p["kind"] == "reverse"))


def remove(doc, p):
    """P taken out: a lamp every car carries becomes its main lamp's part again (FALLBACKS), any other goes. What it
    did, as a line."""
    name = p["name"]
    if leaning := [q["name"] for q in doc["props"] if q.get("part") == name]:
        cli.fail(f"{', '.join(leaning)} {'is a part' if len(leaning) == 1 else 'are parts'} of {name}: remove "
                 f"{'it' if len(leaning) == 1 else 'them'} first")
    for q in doc["props"]:
        if name in q.get("over", []):
            q["over"] = [o for o in q["over"] if o != name]
            if not q["over"]:
                del q["over"]
    fallback = next((f for f in FALLBACKS if f[0] == name), None)
    i = doc["props"].index(p)
    if fallback is not None and any(q["name"] == fallback[2] for q in doc["props"]):
        _, kind, parent, side, share = fallback
        doc["props"][i] = {"name": name, "kind": kind, "part": parent, "side": side, "share": share}
        return f"  {name}: the {side} {share:.2f} of {parent} again"
    del doc["props"][i]
    return f"  {name}: removed"


def polygon_area(points):
    return 0.5 * sum(x0 * y1 - x1 * y0 for (x0, y0, _), (x1, y1, _) in zip(points, points[1:] + points[:1]))


def renumber(p, to):
    if "hold" in p:
        p["hold"] = sorted({j for i in p["hold"] for j in to(i)})


def part_name(a):
    return f"v{a.vertex}" if a.vertex is not None else f"e{a.edge}" if a.edge is not None else "all"


def load_car(base, car):
    import numpy as np
    from PIL import Image

    folder, what = base / VARIANTS / car, None
    if not folder.is_dir() and (base / TRAINS / car).is_dir():
        folder, what = base / TRAINS / car, "a rail vehicle"
    elif not folder.is_dir() and (base / ROOFS / car).is_dir():
        folder, what = base / ROOFS / car, "a roof"
    elif not folder.is_dir() and (prefab := next((base / PREFABS).glob(f"*/{car}.json"), None)):
        folder, what = prefab.parent, "a roof"
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
    out = {"id": car, "file": d, "rgba": rgba, "ppm": ppm, "variant": variant, "end": int(round(END_M * ppm)),
           "folder": folder, "fans_only": what is not None, "what": what or "a car"}
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
    kind = next((k for k in (*BEACON_KINDS, FAN, "head", "brake", "reverse", "ind") if name.startswith(k)), None)
    if kind is None:
        cli.fail(f"a new prop's kind is read off its name, which starts head, brake, reverse, ind, {FAN} or "
                 f"{', '.join(BEACON_KINDS)}: {name!r} does not")
    return kind


#: A beacon bar's ends as prop kinds. Each is its own glass and its own colour, so one is never the other's mirror.
BEACON_KINDS = ("beacon_red", "beacon_blue", "beacon_amber")
#: A rail vehicle's fan: the round opening it turns in, drawn once wherever it stands across the roof, read off the art
#: as a disc from its hub (`--lens=disc`).
FAN = "fan"
#: The props drawn once, never mirrored.
ONCE_KINDS = (*BEACON_KINDS, FAN)


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
    p = next((q for q in doc["props"] if q["name"] == name), None)
    kind = p["kind"] if p is not None else kind_of(name)
    # A beacon end is dark glass of any hue, drawn once: read by its tint, and never looked for on the other side. A fan
    # is drawn once too, its disc read out from its hub.
    once = kind in ONCE_KINDS
    if by == "auto":
        by = "tint" if kind in BEACON_KINDS else "disc" if kind == FAN else by
    t, why = trace.traced(car, seeds, axis, by, mirror=not once)
    if t is None:
        return None, why
    if p is None:
        p = {"name": name, "kind": kind, "mirror": not once}
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
        # Kept off what it touches; where that leaves nothing, the lens as traced, its overlap for the report to name.
        t = trace.traced(car, seeds, axis, by, mirror=not once, away=away)[0] or t
    for key in ("part", "side", "share", "hold", "lock", "pull", "round", "axisY", "mirrorDx"):
        p.pop(key, None)
    p["mirror"] = p["kind"] not in ONCE_KINDS
    p["points"] = fit.rounded(t["points"])
    said = f"  {name}: traced by {', '.join(t['how'])} from {len(seeds)} texel{'s' if len(seeds) > 1 else ''}, " \
           f"{len(p['points'])} pts"
    if p["kind"] == FAN:
        said += "; a fan, drawn once: a fan's look is laid over this outline, its edge on it, and turns"
    elif not p["mirror"]:
        said += "; a beacon end, drawn once: the other end is a lens of its own colour"
    elif t["copy"] is None:
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


def seeded_fans(car):
    """A rail vehicle's or a roof's props file before its first fan is traced: no props, each end's axis its picture's
    middle."""
    return {"car": car["id"], "sprite": sprite_cut_on(car),
            "axisY": {e: body_middle(car, e) for e in ("rear", "front")}, "props": []}


def traced_fans(doc, car):
    """seed's fans: each the picture draws (`trace.fan_hubs`) traced from its hub as fan1, fan2, … by its hub's row,
    then column; a line a fan."""
    import numpy as np

    hubs = sorted((hub for hub, _, _, _, why in trace.fan_hubs(car) if why is None), key=lambda h: (h[1], h[0]))
    lines = []
    for hub in hubs:
        # A rim voted on a blade's arc leaves the hub off the middle: traced again from the middle of what it read.
        if (m := trace.disc(car, hub)[0]) is not None and m.any():
            ys, xs = np.nonzero(m)
            hub = (int(xs.mean()), int(ys.mean()))
        p, said = trace_prop(doc, car, f"fan{len(doc['props']) + 1}", [hub], "disc")
        lines.append(said if p is not None else f"  hub {hub[0]},{hub[1]}: {said}")
    return lines or [f"  no fan found: {car['id']} draws none, or `find` lists it among the near misses"]


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


#: How many earlier states of a props file `undo` can step back through: 30 fell short of a divide two agents wanted
#: back (batches 9 and 10).
UNDO_STEPS = 60
#: How far past `--pad` from the texel `show --at` reports a prop.
AT_NEAR_PX = 8


def history(path):
    """The earlier states of a props file, oldest first, kept beside it for `undo`."""
    folder = path.with_suffix(".history")
    return folder, sorted(folder.glob("*.json")) if folder.is_dir() else []


def remember(path):
    """The props file as it stands, pushed onto its history before it is written over."""
    folder, kept = history(path)
    folder.mkdir(exist_ok=True)
    number = int(kept[-1].stem) + 1 if kept else 0
    shutil.copy(path, folder / f"{number:05d}.json")
    for old in kept[:max(0, len(kept) + 1 - UNDO_STEPS)]:
        old.unlink()


def save(path, doc, backup):
    """One point a line, so a hand edit is one line. A save that changes nothing (a snap that kept no pass) is no
    step for `undo`."""
    lines =["{", f'  "car": "{doc["car"]}",'] + ([f'  "sprite": {json.dumps(doc["sprite"])},'] if "sprite" in doc else []) + [
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
    text = "\n".join(lines)
    if path.exists():
        if path.read_text(encoding="utf-8") == text:
            return
        if backup:
            remember(path)
    path.write_text(text, encoding="utf-8")


def report(doc, car, props, reach):
    """A line a prop: its contrast, its weakest edge, how far each side sits off the rim the search finds within
    `reach` (the median over the samples clear enough to count; - where none is), and what is wrong with it."""
    import numpy as np

    if (lenses := lens_count(doc)) > INSTALL_MOST_LENSES:
        print(f"TOO MANY LENSES (install refuses): {lenses}, a car draws {INSTALL_MOST_LENSES} — `remove` a prop, "
              "which makes a lamp every car carries its main lamp's part again")
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
            # The part as snap judges it, learnt off both sides.
            at = fit.copy_at(doc, car, p)
            pull = fit.pull_of(car, p)
            like = fit.likeness(car, sides, pull)
            held = set(p.get("hold", []))
            per = fit.edge_contrasts(p["points"], like)
            free = [i for i in range(len(per)) if i not in held] or list(range(len(per)))
            worst = min(free, key=per.__getitem__)
            twin = fit.fit_of(fit.mirrored(p["points"], at), like, None, held) if p["mirror"] else None
            wrong = fit.smooth_enough(sides[0])
            gaps = [fit.rim_gap(q, like, reach) for q in fit.sides_of(doc, car, p)]
            gap = "/".join("-" if g is None else f"{g:.2f}" for g in gaps)
            frame = "/".join(f"{fit.framed(car, r):.0%}" for r in sides)
            ys, xs = np.nonzero(fit.coverage(sides[:1], car["rgba"].shape[:2]) > 0.5)
            past = cell_overrun(xs, ys) if len(xs) else None
            print(f"  {p['name']:<12} {len(per):>2} pts  edge contrast{' by frame' if pull == 'frame' else ''} "
                  f"{sum(per[i] for i in free) / len(free):.2f}"
                  + (f", copy {twin:.2f}" if twin is not None else "")
                  + f"  weakest e{worst} {per[worst]:.2f}  off its rim {gap} px  framed {frame}"
                  + (f"  held {fit.edge_list(held)}" if held else "") + (" locked" if p.get("lock") else "")
                  + (f"  own axisY {at[0]:.2f} mirrorDx {at[1]:+.2f}" if "axisY" in p or "mirrorDx" in p else "")
                  + f"  {area:.0f} px" + (f"  NOT A LENS: {wrong}" if wrong else "")
                  + (f"  PAST THE CELL (install refuses): {past}" if past else ""))
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
        # The smaller is the section and names the housing it lies over (the manual).
        small, big = sorted((a, b), key=lambda q: float(cover[q["name"]].sum()))
        fix = (f'a part never lies over a lens: `"over": ["{part["part"]}"]` on {(b if part is a else a)["name"]} if '
               f"it is a section of {part['part']}'s housing (the part then leaves it out), else a smaller `share` or "
               f"the other `side` for {part['name']}" if part else
               f'`"over": ["{big["name"]}"]` on {small["name"]} if the art draws one lens inside the other\'s '
               f"housing, else pull them apart")
        lines.append(f"  overlap {a['name']} / {b['name']}: {shared:.0f} px, " + (why or f"NOT DECLARED — {fix}"))
    return lines


#: Audit: a lens this share of whose texels are frame-dark sits on its frame rather than its glass; a patch of lens
#: colour of AUDIT_PATCH_PX that the props cover partly, under AUDIT_COVERED of it and AUDIT_SHORT_PX short past their
#: rim, is a lens cut smaller than drawn; one of AUDIT_UNCUT_PX they miss, framed, a lens nobody cut.
AUDIT_DARK, AUDIT_PATCH_PX, AUDIT_COVERED, AUDIT_SHORT_PX, AUDIT_UNCUT_PX = 0.25, 12, 0.8, 8, 30


def audit(base, which):
    """Every installed car's kept props held against its art: a line for each lens cut on its frame, each patch of
    lens colour a lens covers only part of, and each framed one none covers. What `show` then looks at."""
    import numpy as np

    folder = base / VARIANTS
    ids = sorted(d.name for d in folder.iterdir() if (d / f"{d.name}_zones.png").is_file()) if which == "all" \
        else which.split(",")
    flagged = 0
    for cid in ids:
        car = load_car(base, cid)
        kept = kept_props(base, car)
        if not kept.is_file():
            print(f"{cid}: no props kept for {Path(car['variant']).name}")
            continue
        doc = load(kept)
        if doc.get("sprite", {}).get("variant", car["variant"]) != car["variant"]:
            print(f"{cid}: props cut on another picture")
            continue
        onto_sprite(doc, car)
        shape = car["rgba"].shape[:2]
        solid = car["rgba"][..., 3] > 0.5
        lines = []
        for p in doc["props"]:
            if p["kind"] in BEACON_KINDS:
                continue
            for side, ring in zip(("authored", "copy"), fit.rings(doc, car, p)):
                inside = (fit.coverage([ring], shape) > 0.5) & solid
                if inside.any() and (dark := float((car["dark"][inside] > 0.5).mean())) >= AUDIT_DARK:
                    ys, xs = np.nonzero(inside)
                    lines.append(f"  {p['name']:<12} ON THE FRAME ({side}): {dark:.0%} frame-dark over {int(inside.sum())} "
                                 f"px, x {xs.min()}-{xs.max()} y {ys.min()}-{ys.max()}")
        for end in ("rear", "front"):
            here = [p for p in doc["props"] if fit.end_of(doc, car, p) == end]
            cover = {p["name"]: fit.coverage(fit.rings(doc, car, p)[:1], shape) > 0.5 for p in here}
            under = np.any(list(cover.values()), 0) if cover else np.zeros(shape, bool)
            # A texel next to a lens is its anti-aliased rim, which the outline rightly leaves at the frame.
            near = fit.window(under, 1, np.max)
            for c, m, (x, y) in trace.patches(car, end_columns(car, end), doc["axisY"][end]):
                px = int(m.sum())
                got = px - int((m & ~near).sum())
                colour = dict(r="red", a="amber", w="pale")[c]
                names = ", ".join(n for n, q in cover.items() if (m & q).any())
                if px >= AUDIT_PATCH_PX and names and got < AUDIT_COVERED * px and px - got >= AUDIT_SHORT_PX:
                    lines.append(f"  {end:<12} SHORT: {colour} patch {px} px at {x},{y}, {got / px:.0%} under {names}")
                elif not (m & under).any() and px >= AUDIT_UNCUT_PX and trace.framed(car, m) >= OTHER_RED_FRAMED:
                    lines.append(f"  {end:<12} UNCUT: {colour} patch {px} px at {x},{y}, framed {trace.framed(car, m):.0%}")
        if lines:
            flagged += 1
            print(cid)
            for line in lines:
                print(line)
    print(f"{flagged} of {len(ids)} cars flagged")
    return 0


def edge_table(doc, car, p, reach):
    """Every edge of one prop: its ends, chord and bow, its contrast, and where the rim search snap and the unrolled
    strips make, within `reach`, finds its rim at each end — positive out — with the bow that would follow it: on the
    authored side, which snap fits (`mirror` paints the copy side from it)."""
    pts = p["points"]
    at = None
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


#: How far past a copy's outline `mirror` paints the authored side's art over the copy side, and over how many texels
#: more it fades into the copy side's own: the frame round a lens and the paint just off it come with the lens. And how
#: far from the outline the copy side's own lamp is followed — its lens texels joined to the copy — and painted over
#: as well: a lamp drawn in another shape there left its arm showing at 6 (`citycar_lime`). Erasing every lamp-like
#: texel across the end instead took windows and a red car's paint for lamps.
MIRROR_GROW_PX, MIRROR_FEATHER_PX, MIRROR_REACH_PX = 3, 2, 12
#: The ring round a lamp, on each side, whose median is that side's paint there; and how near it (channel distance,
#: 0-255) a texel is paint. Paint is never painted over paint, so a wing in primer or a rust spot on the copy side
#: stays (`classic_red`); paint over a copy lens drawn larger is recoloured to the copy side's.
MIRROR_PAINT_RING_PX, MIRROR_PAINT_NEAR = (4, 8), 40


#: How far `mirror` looks for where a lamp's reflection lies best over the copy side's own lamp: an axis this many
#: texels either way of the prop's copy, in half texels, and a shift this many whole texels along the car.
MIRROR_SEEK_AXIS_PX, MIRROR_SEEK_DX_PX = 4, 4


def aligned_copy(doc, car, p):
    """Where the authored side's lamp, reflected, lies best over the copy side's own drawing of it — (axisY,
    mirrorDx), by the least colour difference over the lamp and its frame. The copy side's lamp is drawn a few texels
    off the plain reflection often enough (`citycar_lime`, `cityhatch_silver`), and painted there the reflection
    would leave the drawn lamp showing beside it."""
    import numpy as np

    h, w = car["rgba"].shape[:2]
    own = fit.coverage(fit.rings(doc, car, p)[:1], (h, w)) > 0.5
    for _ in range(2):
        own = trace.dilate4(own)
    ys, xs = np.nonzero(own)
    colour = car["colour"]
    axis0, dx0 = fit.copy_at(doc, car, p)
    best, best_error = (axis0, dx0), np.inf
    for axis in np.arange(axis0 - MIRROR_SEEK_AXIS_PX, axis0 + MIRROR_SEEK_AXIS_PX + 1e-6, 0.5):
        y2 = np.round(2 * axis - (ys + 0.5) - 0.5).astype(int)
        for dx in range(-MIRROR_SEEK_DX_PX, MIRROR_SEEK_DX_PX + 1):
            x2 = xs + round(dx0) + dx
            ok = (y2 >= 0) & (y2 < h) & (x2 >= 0) & (x2 < w)
            if ok.mean() < 0.9:
                continue
            error = float(np.abs(colour[y2[ok], x2[ok]] - colour[ys[ok], xs[ok]]).sum(-1).mean())
            if error < best_error:
                best, best_error = (float(axis), float(round(dx0) + dx)), error
    return best


def mirror(base, doc_path, doc, car):
    """The copy side's lamps painted from the authored side's, each reflected where its prop's copy lies, on the
    sprite's picture at its own resolution — the body, its outline and everything else as drawn. Kept as a new variant
    naming the picture and the props it was painted from, installed, and the same props installed onto it: the art
    under each copy is then the authored lens to the texel."""
    import argparse as ap

    import numpy as np
    from PIL import Image, PngImagePlugin

    import sprites

    if doc["sprite"]["variant"] != car["variant"]:
        cli.fail(f"the props were cut on {doc['sprite']['variant']}, the sprite is {car['variant']}: `restore` or `seed`")
    mirrored = [p for p in doc["props"] if "points" in p and p["mirror"]]
    if not mirrored:
        cli.fail("no mirrored prop with an outline: nothing to paint the copy side from")
    for p in mirrored:
        # Each copy put over the copy side's own lamp first: the reflection is painted there and the lamp lit there.
        axis, dx = aligned_copy(doc, car, p)
        end_axis = doc["axisY"][fit.end_of(doc, car, p)]
        p.pop("axisY", None), p.pop("mirrorDx", None)
        if (axis, dx) != (end_axis, 0.0):
            p["axisY"], p["mirrorDx"] = axis, dx
    save(doc_path, doc, backup=True)
    shutil.copy(doc_path, kept_props(base, car))

    source = base / car["variant"]
    picture = Image.open(source)
    cut_info = json.loads(picture.text.get("cut", "{}"))
    pixels = np.asarray(picture.convert("RGBA")).astype(np.float32)
    big_h, big_w = pixels.shape[:2]
    h, w = car["rgba"].shape[:2]
    kx, ky = big_w / w, big_h / h
    lens_coloured = np.isin(car["classes"], list("raw"))
    sprite_rgb = car["colour"] * 255

    def upsampled_paint_like(paint):
        """The sprite's texels within MIRROR_PAINT_NEAR of a paint colour."""
        return np.abs(sprite_rgb - paint).max(-1) <= MIRROR_PAINT_NEAR
    solid = car["rgba"][..., 3] > 0.5
    best = np.zeros((big_h, big_w), np.float32)
    painted = pixels.copy()
    ys, xs = np.mgrid[0:big_h, 0:big_w]

    def grown(mask, px):
        for _ in range(px):
            mask = trace.dilate4(mask)
        return mask

    def paint_round(mask):
        """The median colour of the picture's body in a ring round a mask on the sprite's grid."""
        near, far = (grown(mask, px) for px in MIRROR_PAINT_RING_PX)
        ring = np.asarray(Image.fromarray(((far & ~near & solid) * 255).astype(np.uint8)).resize((big_w, big_h)))
        return np.median(pixels[ring > 127][:, :3], axis=0)

    def reflected(at, axis, dx):
        """The authored side's texels each picture texel in `at` reflects onto, about `axis` and shifted `dx`."""
        sx = (xs[at] + 0.5) / kx - dx
        sy = 2 * axis - (ys[at] + 0.5) / ky
        return np.stack([fit.sample(pixels[..., c], np.stack([sx * kx, sy * ky], -1).astype(np.float32))
                         for c in range(4)], -1)

    for p in mirrored:
        axis, dx = fit.copy_at(doc, car, p)
        rings = fit.rings(doc, car, p)
        copy = fit.coverage(rings[1:2], (h, w)) > 0.5
        own_paint, copy_paint = paint_round(fit.coverage(rings[:1], (h, w)) > 0.5), paint_round(copy)
        # The copy side's own lamp, however it is drawn: its lens texels joined to the copy within reach — a lens's
        # colour, and not the paint's there, so neither a window nor a red car's own body is taken for it.
        # Followed across the dark line between two lenses of one lamp (a texel or two), and taken with its frame.
        lens = lens_coloured & ~upsampled_paint_like(copy_paint)
        within = grown(lens, 1) & grown(copy, MIRROR_REACH_PX)
        own_lamp = grown(copy, 1) & within
        while (more := grown(own_lamp, 1) & within & ~own_lamp).any():
            own_lamp |= more
        own_lamp &= lens
        frame = grown(own_lamp, 2) & (car["dark"] > trace.FRAME)
        core = grown(copy | own_lamp | frame, MIRROR_GROW_PX)
        # Fully painted in the core, fading over the feather: a texel's weight on the sprite's grid, then the picture's.
        weight, ring = core.astype(np.float32), core
        for k in range(1, MIRROR_FEATHER_PX + 1):
            wider = trace.dilate4(ring)
            weight[wider & ~ring] = 1 - k / (MIRROR_FEATHER_PX + 1)
            ring = wider
        big = np.asarray(Image.fromarray(weight).resize((big_w, big_h), Image.BILINEAR))
        at = big > best
        if not at.any():
            continue
        took = reflected(at, axis, dx)
        took_paint = np.abs(took[:, :3] - own_paint).max(-1) <= MIRROR_PAINT_NEAR
        here_paint = np.abs(painted[at, :3] - copy_paint).max(-1) <= MIRROR_PAINT_NEAR
        # The authored side's paint, where it lands on what was the copy side's lens, in the copy side's paint.
        colour = np.where(took_paint[:, None], took[:, :3] * copy_paint / np.maximum(own_paint, 1), took[:, :3])
        # Paint off the authored side's body is no lens: weighed by its alpha, so the copy side keeps its own there.
        wt = np.where(took_paint & here_paint, 0.0, big[at] * took[:, 3] / 255)
        painted[at, :3] = painted[at, :3] * (1 - wt[:, None]) + colour * wt[:, None]
        best[at] = big[at]

    folder, name_was = source.parent, source.stem
    records = sprites.read_jsonl(folder / sprites.VARIANTS)
    was = next((r for r in records if r["variant"] == name_was), None)
    if was is None:
        cli.fail(f"{name_was} has no record in {_sim.rel(base, folder / sprites.VARIANTS)}")
    i = car["id"]
    version = 1 + max((int(m.group(2)) for q in folder.glob(f"{i}-v*.png")
                       if (m := sprites.VERSIONED.fullmatch(q.stem))), default=0)
    name = f"{i}-v{version}"
    png = PngImagePlugin.PngInfo()
    png.add_text("cut", json.dumps({**cut_info, "lampsMirrored": {"from": name_was, "props": _sim.rel(base, kept_props(base, car))}}))
    Image.fromarray(np.clip(painted, 0, 255).astype(np.uint8), "RGBA").save(folder / f"{name}.png", pnginfo=png)
    sprites.append(folder / sprites.VARIANTS, {
        "variant": name, **{k: was[k] for k in ("frame", "cell", "box", "pxPerM", "score") if k in was},
        "lampsMirroredFrom": name_was, "props": _sim.rel(base, kept_props(base, car)), "status": "candidate",
        "ts": time.strftime("%Y-%m-%dT%H:%M:%S")})
    print(f"kept {name}: {name_was} with its copy side's lamps painted from the authored side's "
          f"({len(mirrored)} props), {_sim.rel(base, folder / (name + '.png'))}")

    sprites.use(base, ap.Namespace(refs=[name], lamps_then="the same props are installed on it next"))
    now = load_car(base, i)
    now["pull"] = car["pull"]
    doc["sprite"] = sprite_cut_on(now)
    return install(base, doc_path, doc, now)


def kept_props(base, car):
    """Where the props cut on the sprite's picture are kept: beside that variant, named by it — one a picture, so
    another picture's lamps are never written over them."""
    variant = base / car["variant"]
    return variant.with_name(f"{variant.stem}.props.json")


def install(base, doc_path, doc, car):
    """The props into the game: the car's zone map painted from them, a lens a side of each in its `lamps`, and the
    props file kept under raw_assets/ as what both were made from. A service car's beacon lenses are kept, and with
    them its brake lamp is the `rear` fitting (red braking, white reversing), which is what fits CarLamps.MostLenses."""
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
    traced_beacons = any(p["kind"] in BEACON_KINDS for p in doc["props"])
    # A bar whose ends are traced props is cut like every other lamp; one that is not keeps `qq beacons fit`'s lenses.
    beacons = [] if traced_beacons else \
        [lamp for lamp in car["file"].get("lamps", []) if lamp["fitting"].startswith("beacon")]
    rows, problems = [], []
    for p in doc["props"]:
        cov = (fit.prop_coverage(doc, car, p, (h, w)) > 0.5) & solid
        if (beacons or traced_beacons) and p["kind"] == "reverse":
            # A car with a beacon bar has no lens to spare for it: its glass is the brake's, and the brake lens lights
            # white in reverse (`rear`).
            zones[cov, INSTALL_GLASS["brake"]] = 255
            print(f"{p['name']}: no lens of its own, brake glass under the `rear` fitting beside the beacons")
            continue
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
            if fault := cell_overrun(xs, ys):
                problems.append(f"{p['name']} ({side}): {fault}")
            fitting = "rear" if (beacons or traced_beacons) and p["kind"] == "brake" else INSTALL_FITTING[p["kind"]]
            rows.append((fitting, (round(cx / per_m[0] - foot[0] / 2, 3), round(cy / per_m[1] - foot[1] / 2, 3)),
                         (round((x1 - x0) / per_m[0], 3), round((y1 - y0) / per_m[1], 3))))
    # The beacons are `qq beacons fit`'s, laid on the picture rather than cut off it: kept as they stand.
    rows += [(lamp["fitting"], tuple(lamp["atM"]), tuple(lamp["sizeM"])) for lamp in beacons]
    if len(rows) > INSTALL_MOST_LENSES:
        problems.append(f"{len(rows)} lenses{f' ({len(beacons)} of them beacons)' if beacons else ''}, and a car may "
                        f"draw {INSTALL_MOST_LENSES}")
    if problems:
        cli.fail("not installed:\n  " + "\n  ".join(problems))

    folder = base / VARIANTS / car["id"]
    described = folder / f"{car['id']}.json"
    sprite_src = car["variant"]
    raw = kept_props(base, car)
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


def install_fans(base, doc_path, doc, car):
    """A rail vehicle's or a roof's fans into the game, as its file's `fans` (DrawnFan): each its hub and its rim — the
    ellipse square to the picture fitted through its outline, as the art drew the opening, squashed or not — which the game warps
    onto a circle about the hub to turn it. The props file is kept under raw_assets/ as what they were cut from. The hub
    is where the rings round it are most alike all the way round (`unevenness`), in HUB_SEEK_PX of the outline's
    middle, to the half texel."""
    import numpy as np

    if onto_sprite(doc, car):
        print(f"{_sim.rel(base, doc_path)}: scaled onto the sprite at {doc['sprite']['sizePx'][0]}x"
              f"{doc['sprite']['sizePx'][1]} px")
    save(doc_path, doc, backup=True)
    fans = [p for p in doc["props"] if "points" in p]
    if not fans:
        cli.fail(f"{_sim.rel(base, doc_path)} has no fan to install: `trace {car['id']} --prop=fan1 --at=X,Y` first")
    h, w = car["rgba"].shape[:2]
    foot = car["file"]["footprintM"]
    per_m = (w / foot[0], h / foot[1])
    lum = car["colour"].mean(-1)
    rows = []
    for p in fans:
        ring = fit.rings(doc, car, p)[0]
        cov = fit.prop_coverage(doc, car, p, (h, w)) > 0.5
        ys, xs = np.nonzero(cov)
        middle = (xs.mean() + 0.5, ys.mean() + 0.5)
        inscribed = lambda c: float(np.hypot(ring[:, 0] - c[0], ring[:, 1] - c[1]).min())
        seek = np.arange(-HUB_SEEK_PX, HUB_SEEK_PX + 1e-6, 0.5)
        reach = HUB_SHARE * inscribed(middle)
        hub = min(((round(middle[0] * 2) / 2 + dx, round(middle[1] * 2) / 2 + dy) for dx in seek for dy in seek),
                  key=lambda c: unevenness(lum, c, reach))
        (cx, cy), (rx, ry) = rim_of(ring)
        to_m = lambda x, y: (round(x / per_m[0] - foot[0] / 2, 4), round(y / per_m[1] - foot[1] / 2, 4))
        rows.append((to_m(*hub), to_m(cx, cy), (round(rx / per_m[0], 4), round(ry / per_m[1], 4))))
        print(f"{p['name']}: hub {hub[0]:.1f},{hub[1]:.1f}; rim about {cx:.1f},{cy:.1f}, {rx:.1f} by {ry:.1f} texels "
              f"({abs(rx - ry) / max(rx, ry):.0%} out of round), {2 * rx / per_m[0]:.2f} by {2 * ry / per_m[1]:.2f} m")

    described = car["folder"] / f"{car['id']}.json"
    raw = kept_props(base, car)
    shutil.copy(doc_path, raw)
    text = described.read_text(encoding="utf-8")
    listed = ",\n".join(f'    {{ "atM": [{hx}, {hy}], "rimAtM": [{cx}, {cy}], "rimM": [{rx}, {ry}] }}'
                        for (hx, hy), (cx, cy), (rx, ry) in rows)
    block = f'"fans": [\n{listed}\n  ]'
    if (at := text.find('"fans"')) >= 0:
        depth, end = 0, text.index("[", at)
        for end in range(end, len(text)):
            depth += {"[": 1, "]": -1}.get(text[end], 0)
            if depth == 0:
                break
        text = text[:at] + block + text[end + 1:]
    else:
        text, n = re.subn(r'\n\}\s*$', f',\n  {block}\n}}\n', text.rstrip() + "\n", count=1)
        if not n:
            cli.fail(f"cannot place a fans array at the end of {_sim.rel(base, described)}")
    described.write_text(text, encoding="utf-8")
    print(f"{_sim.rel(base, described)}: {len(rows)} fan{'s' if len(rows) != 1 else ''}")
    print(f"{_sim.rel(base, raw)}: what they were cut from")
    return 0


def rim_of(ring):
    """A fan's rim as the ellipse square to the body through its outline, A·x² + B·y² + C·x + D·y = 1 about the
    outline's middle: ((centre x, y), (half-axis x, y)), in texels."""
    import numpy as np

    mx, my = ring[:, 0].mean(), ring[:, 1].mean()
    xs, ys = ring[:, 0] - mx, ring[:, 1] - my
    a, b, c, d = np.linalg.lstsq(np.stack([xs * xs, ys * ys, xs, ys], 1), np.ones(len(xs)), rcond=None)[0]
    ex, ey = -c / (2 * a), -d / (2 * b)
    scale = 1 + a * ex * ex + b * ey * ey
    return (float(mx + ex), float(my + ey)), (math.sqrt(scale / a), math.sqrt(scale / b))


#: How far from its outline's middle a fan's hub is looked for, in texels: the art draws the shroud a texel or so off
#: the hub its blades turn about (`diesel_green`).
HUB_SEEK_PX = 2
#: The share of a fan's reach its hub is judged over, and the rays each ring of it is read along. The hub only: out
#: to the shroud, a ring drawn a texel off the hub pulls the centre onto itself, and the blades' speckled ground
#: matches itself turned too poorly for the blades to say where they turn (`diesel_blue`'s 8 read as 7 or 9).
HUB_SHARE, HUB_RAYS = 0.4, 96


def unevenness(lum, centre, radius):
    """How much the rings about a centre change going round, out to `radius`: least about the hub, whose rings are
    each one shade all the way round, the spokes crossing every one of them alike."""
    import numpy as np

    h, w = lum.shape
    angles = np.linspace(0, 2 * math.pi, HUB_RAYS, endpoint=False)
    total = 0.0
    for r in np.arange(1.0, radius, 0.5):
        # Bilinear, so a centre half a texel over is judged on what lies there rather than on the texel it rounds to.
        fx, fy = centre[0] + r * np.cos(angles) - 0.5, centre[1] + r * np.sin(angles) - 0.5
        x0, y0 = np.floor(fx).astype(int), np.floor(fy).astype(int)
        tx, ty = fx - x0, fy - y0
        pick = lambda rows, cols: lum[np.clip(rows, 0, h - 1), np.clip(cols, 0, w - 1)]
        ring = (pick(y0, x0) * (1 - tx) * (1 - ty) + pick(y0, x0 + 1) * tx * (1 - ty)
                + pick(y0 + 1, x0) * (1 - tx) * ty + pick(y0 + 1, x0 + 1) * tx * ty)
        total += float(ring.var())
    return total


#: A prop's kind as the bake's glass channel (LampAtlasBake.Holds) and as a fitting of the car file. A beacon end
#: shares its colour's channel with the lamps at the car's ends, which no beacon's cell reaches.
INSTALL_GLASS = {"brake": 0, "ind": 1, "head": 2, "reverse": 2, "beacon_red": 0, "beacon_amber": 1, "beacon_blue": 2}
INSTALL_FITTING = {"brake": "brake", "ind": "indicator", "head": "head", "reverse": "reverse",
                   "beacon_red": "beaconRed", "beacon_blue": "beaconBlue", "beacon_amber": "beaconAmber"}
#: LampAtlas.CellPx and CarLamps.MostLenses.
INSTALL_CELL_PX, INSTALL_MOST_LENSES = 56, 10


def cell_overrun(xs, ys):
    """Why the lamp sheet's cell cannot hold a lens of these texels, or None. The bake finds a lens's texels only
    within a cell centred on it, and a lens the cell's whole width long is written past the cell, which
    `LampAtlasTests.EveryLensFitsTheCellItIsCutInto` refuses (`hatch90s_black`, `budgetsuv_brown` at the old 40)."""
    x0, x1, y0, y1 = int(xs.min()), int(xs.max()) + 1, int(ys.min()), int(ys.max()) + 1
    cx, cy = round((x0 + x1) / 2), round((y0 + y1) / 2)
    reach = max(cx - x0, x1 - cx, cy - y0, y1 - cy)
    faults = [f"its box is {side} texels {axis}, the cell takes {INSTALL_CELL_PX - 1}"
              for side, axis in ((x1 - x0, "along the car (x)"), (y1 - y0, "across it (y)")) if side >= INSTALL_CELL_PX] + \
             ([f"{reach} from its centre, the cell takes {INSTALL_CELL_PX // 2}"] if reach > INSTALL_CELL_PX // 2 else [])
    return "; ".join(faults) + " — trim a tip, or `divide` the housing" if faults else None


if __name__ == "__main__":
    cli.entry(main)
