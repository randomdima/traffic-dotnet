#!/usr/bin/env python3
"""Benchmark OpenRouter image models on fixed sheets — roofs, cars, trains, one locomotive — against the art
already in the game.

  prepare                                   layout, style picture, in-game composite and brief per suite
  models                                    every OpenRouter image model and its frames
  run     --suite S --model M [-n 2] [--extra TEXT]   n new frames of one model
  sweep   [--suite S] [--models ID,..] [-n 2] [--jobs 4]   tops every model (or those) up to n frames
  score   [--suite S] [--cells]             measured figures, captioned comparison sheets, results.md
  judge   [--suite S]                       blind sheets and the score card to fill in by eye
  reveal                                    joins the filled card to the models: judged.md
  tries   --suite S                         <suite>/tries.pdf, a labelled page per try, verdicts from tries.json
  pdf     FILE.md                           a hand-written report printed to FILE.pdf

Fair by construction: every model gets the same brief, the same two pictures (or the same text-only brief
when it takes no image), the same aspect and the same seed per frame; a frame that fails counts against its
model instead of vanishing; a nudged frame (--extra) is reported apart from the plain brief. The figures
`score` measures are evidence for the judge, never the verdict: the verdict is the card `judge` writes,
scored by eye on sheets that do not name the model.

Everything goes to .tmp/bench/, frames to .tmp/bench/frames/bench_<suite>_<tag>-vN.<ext>. It shares only
remotegen's OpenRouter client (openrouter.py): never remotegen's settings, frames, index or attempt cap, and
nothing here touches assets/ or raw_assets/.
"""

import argparse
import concurrent.futures
import glob
import hashlib
import html as html_lib
import json
import os
import re
import secrets
import shutil
import statistics
import subprocess
import sys
import threading
import time

from PIL import Image, ImageChops, ImageDraw, ImageFont
from PIL.PngImagePlugin import PngInfo

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.join(HERE, "..", "remotegen"))
import openrouter  # noqa: E402
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
WORK = os.path.join(ROOT, ".tmp", "bench")
FRAMES = os.path.join(WORK, "frames")
RUNS = os.path.join(WORK, "runs.jsonl")
CARD = os.path.join(WORK, "judge.md")
CARD_KEY = os.path.join(WORK, "judge_key.json")
KEY = (255, 0, 255)
OUTLINE, FILL, EDGE_PX = (30, 30, 30), (235, 235, 235), 7

# Sheet 9 as it was generated, and sheet 8's roofs as its style picture: not sheet 9's own, which
# would hand the model the answer.
BLD_IDS = ["tower_07", "tower_11", "tower_12", "tower_13", "tower_14", "tower_10",
           "tower_02", "tower_rounded_01", "garages_deep_06"]
BLD_STYLE_IDS = ["tower_06", "tower_09", "tower_deep_06", "tower_deep_04", "tower_04",
                 "tower_deep_03", "tower_08", "tower_deep_08", "tower_deep_07"]
BLD_CELL_PX, BLD_FIT_PX = 400, 330

# Nine of the game's cars to redraw, and six others as the style picture. police_white carries text, so
# it is not shown to a model.
# Each car's outline is a rounded box at its footprint with the corner radius of its body type — the
# one shape term a brief for a car that does not exist yet can still give. Not the sprite's own
# silhouette: that only exists for a car already drawn, and hands the model the answer.
BODY_CORNER_M = {"square": 0.12, "boxy": 0.2, "saloon": 0.32, "sporty": 0.5, "round": 0.7}
CARS = {
    "fleetsaloon_silver": ("saloon", "plain budget four-door saloon, dull silver, one rear wing a slightly darker "
                                     "grey, tall roof, short separate boot lid, black unpainted bumpers"),
    "hatch_blue": ("saloon", "five-door family hatchback, deep metallic blue, long flat roof, near-vertical "
                             "hatchback tail"),
    "cab_yellow": ("saloon", "four-door saloon taxi, bright yellow, a small plain white roof sign with no text "
                             "on it, long low bonnet, short boot"),
    "pickup_silver": ("boxy", "double-cab pickup, silver, tall square bonnet, a hard flat cover over a long bed"),
    "lightvan_white": ("boxy", "worn light van, dull white, rust at the wheel arches, short bonnet, tall boxy "
                               "body, flat roof, two square rear doors"),
    "sportscar_red": ("sporty", "rear-engined sports coupe, glossy red, low wide body, long sloping front lid "
                                "between raised front wings, rounded fastback roof, a slim rear spoiler"),
    "coupe_yellow": ("saloon", "muscle coupe, bright yellow, two black stripes over the bonnet and roof, long "
                               "bonnet, short fastback roof"),
    "citycar_lime": ("round", "five-door city car, lime green, short rounded bonnet, tall roof with one long "
                              "glass area, short hatchback tail"),
    "offroader_black": ("square", "boxy luxury off-roader, gloss black, flat square bonnet, upright square "
                                  "cab, flat roof, a covered spare wheel on the square tail"),
}
CAR_STYLE_BODIES = {"liftback_black": "saloon", "estate_steel": "saloon", "evsaloon_red": "saloon",
                    "retro500_cream": "round", "bigvan_silver": "boxy", "ambulance_white": "boxy"}
CAR_CELL_PX = (600, 400)
CAR_PX_PER_M = 100

# Nine rail vehicles the game does not have yet, a row each of locomotives, freight wagons and passenger cars:
# (length over couplers m, body width m, corner radius m, has a cab, brief line). Sizes are typical of the
# post-Soviet broad-gauge stock of the towns the game is set in, to the nearest 0.1 m. Each outline is a
# rounded box like a collider's; a cab vehicle's thick right edge is its front.
TRAINS = {
    "shunter_red": (17.2, 3.1, 0.3, True,
                    "diesel shunting locomotive, red-orange with yellow-and-black chevron stripes at both ends; "
                    "one cab a third of the way from the rear, a long engine hood in front of it with a row of roof "
                    "hatches and one exhaust stack, a short hood behind it; a railed walkway along both sides"),
    "diesel_green": (18.2, 3.4, 0.5, True,
                     "mainline diesel locomotive, one section, a dark green full-width body with a yellow stripe "
                     "along both long roof edges, the cab at the front; three large round radiator fans on the roof "
                     "at the rear and two exhaust stacks in the middle"),
    "electric_blue": (19.0, 3.1, 0.5, True,
                      "mainline electric locomotive, blue with a light grey roof, a cab at both ends; two "
                      "pantographs on the roof, the front one folded down and the rear one raised, insulators and a "
                      "roof walkway between them"),
    "boxcar_brown": (14.7, 3.3, 0.2, False,
                     "covered box wagon, brown-red, a flat arched roof with thin ribs across it, couplers at both "
                     "ends"),
    "tank_black": (12.0, 3.0, 0.2, False,
                   "tank wagon, a black cylindrical tank seen from above with a highlight along its top, a filling "
                   "dome in the middle with a small railed platform, the frame and couplers showing at both ends"),
    "gondola_coal": (13.9, 3.2, 0.2, False,
                     "open gondola wagon, dark grey steel walls with ribs, the open top heaped with black coal"),
    "coach_green": (25.2, 3.1, 0.3, False,
                    "long-distance sleeping coach, a grey roof with dark green edges along both long sides, a row of "
                    "round vents down the middle and an air-conditioning box at one end, gangway bellows at both "
                    "ends"),
    "emu_head": (19.6, 3.5, 0.8, True,
                 "suburban electric train head car, a grey roof with green edges along both long sides and a "
                 "line of vents down the middle, the cab at the front with a red-orange rounded nose and a wide "
                 "windscreen, a gangway at the rear"),
    "intercity_white": (26.0, 3.0, 0.4, False,
                        "modern intercity coach, a smooth light grey roof with white edges and a blue stripe along "
                        "both long sides, two flush air-conditioning units on the roof, closed gangway ends"),
}
TRAIN_CELL_PX = (600, 400)
TRAIN_PX_PER_M = 21
# Each length drawn and briefed is pulled this share of the way to the nine's mean, so a 26 m coach and a 12 m
# tank sit a little closer on a game's screen (owner's call, 2026-10-18).
TRAIN_LENGTH_PULL = 0.2
# Every vehicle is drawn and briefed this wide, in place of its own width (owner's call, 2026-10-18).
TRAIN_WIDTH_M = 3.5


def train_length_m(length_m):
    mean_m = sum(t[0] for t in TRAINS.values()) / len(TRAINS)
    return length_m + TRAIN_LENGTH_PULL * (mean_m - length_m)

# Layout size and the aspect every model is asked for.
SUITES = {"bld": {"size": (1200, 1200), "aspect": "1:1"},
          "car": {"size": (1800, 1200), "aspect": "3:2"},
          "train": {"size": (1800, 1200), "aspect": "3:2"},
          # One cell of the train sheet, at its scale, asked of each model at its smallest output: what one
          # vehicle per frame costs and looks like against nine to a sheet.
          "loco": {"size": TRAIN_CELL_PX, "aspect": "3:2", "resolution": "min"}}
LOCO = "electric_blue"
# What `report` covers: its sections are written for the roof and car sheets. The train sheets are `tries`'s.
REPORT_SUITES = ("bld", "car")
# A model with no image input gets the brief without the pictures, led by this.
TEXT_LEAD = {
    "bld": "Nine flat building roofs seen straight from above, as top-down sprites for a 2D city game, in a "
           "3×3 grid on a flat magenta #FF00FF background, one per cell, all at one shared scale.",
    "car": "Nine top-down car sprites for a 2D city game in a 3×3 grid on a flat magenta #FF00FF "
           "background, one per cell, every car pointing to the right, all at one shared scale.",
}
# A frame goes through the Image API at the model's own resolution and quality unless a suite or `run` asks
# otherwise, with as many of the two pictures as it takes (remote_models).
# No retry inside a frame: a busy model's backoff held a worker for minutes while others waited their first
# frame. The sweep retries in its own passes instead, once every model has been tried.
FRAME_RETRIES = 0
# Recorded on every frame; a frame of another setup is never compared.
SETUP = "images-api"
SEED0 = 1


def work(suite, name):
    os.makedirs(os.path.join(WORK, suite), exist_ok=True)
    return os.path.join(WORK, suite, name)


def tag_of(model):
    return re.sub(r"[^a-z0-9.]+", "-", model.split("/")[-1].lower())


# ------------------------------------------------------------------ prepare

def building_paths():
    return {os.path.basename(p)[:-5]: p for p in glob.glob(os.path.join(ROOT, "assets/world/building/prefabs/*/*.json"))}


def building_cells(ids):
    """Outline box (left, top, w, h, radius) per id on a 3x3 grid, as template.py lays sheets out."""
    paths, cells = building_paths(), []
    for at, i in enumerate(ids):
        p = json.load(open(paths[i]))
        W, D = p["footprintM"]
        scale = BLD_FIT_PX / max(W, D)
        w, h = W * scale, D * scale
        left = (at % 3) * BLD_CELL_PX + (BLD_CELL_PX - w) / 2
        top = (at // 3) * BLD_CELL_PX + (BLD_CELL_PX - h) / 2
        sprite = os.path.join(os.path.dirname(paths[i]), p["sprite"]) if p.get("sprite") else None
        cells.append({"id": i, "box": [left, top, w, h], "radius": p["cornerRadiusM"] * scale,
                      "cell": [(at % 3) * BLD_CELL_PX, (at // 3) * BLD_CELL_PX, BLD_CELL_PX, BLD_CELL_PX],
                      "sizeM": [W, D], "sprite": sprite, "edge": "bottom"})
    return cells


def car_cells(bodies, cols=3):
    """bodies: {variant id: body type}."""
    cw, ch = CAR_CELL_PX
    cells = []
    for at, (i, body) in enumerate(bodies.items()):
        path = os.path.join(ROOT, f"assets/agents/car/variants/{i}/{i}.json")
        p = json.load(open(path))
        L, W = p["footprintM"]
        w, h = L * CAR_PX_PER_M, W * CAR_PX_PER_M
        x0, y0 = (at % cols) * cw, (at // cols) * ch
        cells.append({"id": i, "box": [x0 + (cw - w) / 2, y0 + (ch - h) / 2, w, h],
                      "radius": min(BODY_CORNER_M[body] * CAR_PX_PER_M, h / 2), "body": body,
                      "cell": [x0, y0, cw, ch], "sizeM": [L, W],
                      "sprite": os.path.join(os.path.dirname(path), p["sprite"]), "edge": "right"})
    return cells


def draw_layout(cells, size):
    img = Image.new("RGB", size, KEY)
    d = ImageDraw.Draw(img)
    for c in cells:
        l, t, w, h = c["box"]
        r = c["radius"]
        d.rounded_rectangle([l, t, l + w, t + h], radius=r, fill=FILL, outline=OUTLINE, width=3)
        # The thick edge: the street front of a building, the nose of a car.
        if c["edge"] == "bottom":
            d.line([l + min(r, w / 2), t + h - 2, l + w - min(r, w / 2), t + h - 2], fill=OUTLINE, width=EDGE_PX)
        elif c["edge"] == "right":
            d.line([l + w - 2, t + min(r, h / 2), l + w - 2, t + h - min(r, h / 2)], fill=OUTLINE, width=EDGE_PX)
    return img


def draw_sprites(cells, size):
    img = Image.new("RGB", size, KEY)
    for c in cells:
        l, t, w, h = c["box"]
        s = Image.open(c["sprite"]).convert("RGBA").resize((round(w), round(h)), Image.LANCZOS)
        img.paste(s, (round(l), round(t)), s)
    return img


def train_cells(ids=None):
    cw, ch = TRAIN_CELL_PX
    cells = []
    for at, (i, (length, _, corner, cab, _)) in enumerate((i, TRAINS[i]) for i in ids or TRAINS):
        length, width = train_length_m(length), TRAIN_WIDTH_M
        w, h = length * TRAIN_PX_PER_M, width * TRAIN_PX_PER_M
        x0, y0 = (at % 3) * cw, (at // 3) * ch
        cells.append({"id": i, "box": [x0 + (cw - w) / 2, y0 + (ch - h) / 2, w, h],
                      "radius": corner * TRAIN_PX_PER_M, "cell": [x0, y0, cw, ch], "sizeM": [length, width],
                      "sprite": None, "edge": "right" if cab else "none"})
    return cells


def train_prompt():
    rows, heads = [], ("Locomotives", "Freight wagons", "Passenger cars")
    for at, (i, (length, _, _, cab, line)) in enumerate(TRAINS.items()):
        if at % 3 == 0:
            rows.append(f"\nRow {at // 3 + 1}: {heads[at // 3]}")
        rows.append(f"{at + 1}. {train_length_m(length):.1f} × {TRAIN_WIDTH_M:.1f} m"
                    f"{', cab at the thick right edge' if cab else ''} — {line}")
    with open(os.path.join(HERE, "train_prompt.txt")) as f:
        return f.read().rstrip() + "\n" + "\n".join(rows) + "\n"


def loco_prompt():
    length, _, _, cab, line = TRAINS[LOCO]
    with open(os.path.join(HERE, "loco_prompt.txt")) as f:
        return (f.read().rstrip() + f" {train_length_m(length):.1f} × {TRAIN_WIDTH_M:.1f} m"
                f"{', cab at the thick right edge' if cab else ''} — {line}\n")


def car_prompt(ids):
    rows = []
    for at, i in enumerate(ids):
        if at % 3 == 0:
            rows.append(f"\nRow {at // 3 + 1}")
        L, W = json.load(open(os.path.join(ROOT, f"assets/agents/car/variants/{i}/{i}.json")))["footprintM"]
        rows.append(f"{at + 1}. {L:.2f} × {W:.2f} m, {CARS[i][0]} body — {CARS[i][1]}")
    with open(os.path.join(HERE, "car_prompt.txt")) as f:
        return f.read().rstrip() + "\n" + "\n".join(rows) + "\n"


def cmd_prepare(args):
    suites = [args.suite] if args.suite else list(SUITES)
    if "bld" in suites:
        bld = building_cells(BLD_IDS)
        draw_layout(bld, SUITES["bld"]["size"]).save(work("bld", "layout.png"))
        draw_sprites(bld, SUITES["bld"]["size"]).save(work("bld", "ingame.png"))
        draw_sprites(building_cells(BLD_STYLE_IDS), SUITES["bld"]["size"]).save(work("bld", "style.png"))
        json.dump(bld, open(work("bld", "cells.json"), "w"), indent=1)
        with open(os.path.join(HERE, "bld_prompt.txt")) as f:
            open(work("bld", "prompt.txt"), "w").write(f.read())
    car_style = draw_sprites(car_cells(CAR_STYLE_BODIES), (CAR_CELL_PX[0] * 3, CAR_CELL_PX[1] * 2))
    if "car" in suites:
        car = car_cells({i: body for i, (body, _) in CARS.items()})
        draw_layout(car, SUITES["car"]["size"]).save(work("car", "layout.png"))
        draw_sprites(car, SUITES["car"]["size"]).save(work("car", "ingame.png"))
        car_style.save(work("car", "style.png"))
        json.dump(car, open(work("car", "cells.json"), "w"), indent=1)
        open(work("car", "prompt.txt"), "w").write(car_prompt(list(CARS)))
    if "train" in suites:
        # The game has no rail art to show, so its road vehicles are the style; there is no in-game answer.
        train = train_cells()
        draw_layout(train, SUITES["train"]["size"]).save(work("train", "layout.png"))
        car_style.save(work("train", "style.png"))
        json.dump(train, open(work("train", "cells.json"), "w"), indent=1)
        open(work("train", "prompt.txt"), "w").write(train_prompt())
    if "loco" in suites:
        loco = train_cells([LOCO])
        draw_layout(loco, SUITES["loco"]["size"]).save(work("loco", "layout.png"))
        car_style.save(work("loco", "style.png"))
        json.dump(loco, open(work("loco", "cells.json"), "w"), indent=1)
        open(work("loco", "prompt.txt"), "w").write(loco_prompt())
    for s in suites:
        print(os.path.relpath(work(s, ""), ROOT), "brief", brief_of(s))


def brief_of(suite):
    """What a frame was asked for: the brief and both pictures. Frames of different briefs are never compared."""
    h = hashlib.sha1()
    for name in ("prompt.txt", "layout.png", "style.png"):
        with open(work(suite, name), "rb") as f:
            h.update(f.read())
    return h.hexdigest()[:8]


def text_only(suite, prompt):
    """The brief without the two pictures, for a model that takes none: the rules and the cell list."""
    rules = prompt[prompt.index("Rules for every"):]
    return TEXT_LEAD[suite] + "\n\n" + rules.replace("Style: match the style image. ", "Style: ")


def layout_only(prompt):
    """The brief for a model that takes one picture: the layout, with the style in words only."""
    head, rest = prompt.split("\n\n", 1)
    head = head[:head.index(" The second")].replace("Two images are attached. The first is a layout:",
                                                    "One image is attached, a layout:")
    return head + "\n\n" + (rest.replace("Style: match the style image. ", "Style: ")
                            .replace("as the first image", "as the attached image"))


BRIEF_FOR_INPUT = {"ref": lambda suite, p: p, "layout": lambda suite, p: layout_only(p), "text": text_only}


# ------------------------------------------------------------------- models

def remote_models():
    """{id: {input, priceOut}} for every OpenRouter image model that takes a text prompt; a -preview twin
    of a released model is left out."""
    out, catalogue = {}, openrouter.image_models()
    ids = {m["id"] for m in catalogue}
    for m in catalogue:
        arch, price = m.get("architecture") or {}, m.get("pricing") or {}
        inputs = arch.get("input_modalities") or []
        if "text" not in inputs or (m["id"].endswith("-preview") and m["id"][:-8] in ids):
            continue
        # A model that takes one reference gets the layout and the style in words.
        most = (openrouter.references_most(m["id"]) or 0) if "image" in inputs else 0
        out[m["id"]] = {"input": "ref" if most >= 2 else "layout" if most == 1 else "text",
                        "priceOut": float(price.get("image_output") or 0) + float(price.get("image") or 0)}
    return out


def cmd_models(args):
    remote, records = remote_models(), read_runs()
    rows = sorted(remote.items(), key=lambda kv: kv[1]["priceOut"])
    print(f"{'model':<44} {'input':<6} " + " ".join(f"{s:>7}" for s in SUITES)
          + "  (ok/tried on the current brief and setup)")
    briefs = {s: brief_of(s) for s in SUITES if os.path.exists(work(s, "layout.png"))}
    for m, c in rows:
        done = []
        for s in SUITES:
            rs = [r for r in records if r["model"] == m and r["suite"] == s and not r.get("transient")
                  and s in briefs and comparable(r, briefs[s])]
            done.append(f"{sum(r['ok'] for r in rs)}/{len(rs)}")
        print(f"{m:<44} {c['input']:<6} " + " ".join(f"{d:>7}" for d in done))


# ---------------------------------------------------------------------- run

def comparable(r, brief):
    """On the current comparison: the current brief and the current setup."""
    return r.get("brief", brief) == brief and r.get("setup") == SETUP


def read_runs():
    """Every attempt, oldest first. Records from before failures were kept lack ok/brief/input: each of
    those was a frame made on the current brief."""
    out = []
    if os.path.exists(RUNS):
        for line in open(RUNS):
            if line.strip():
                r = json.loads(line)
                r.setdefault("ok", True)
                r.setdefault("input", "ref")
                if not r["ok"] and r.get("error") and not r["error"].startswith("not a raster image"):
                    # Classified by today's rules, so one rule decides what counts against a model.
                    r["kind"] = classify(r["error"])
                    r["transient"] = r["kind"] in ("transient", "fatal", "unavailable")
                out.append(r)
    return out


NETWORK = ("cannot reach", "Connection refused", "IncompleteRead", "timed out",
           "RemoteDisconnected", "Connection reset")


def classify(err):
    """fatal: the key or the account (report and stop, never route around); unavailable: the provider's own
    quota, which no retry today fixes; transient: the service or the network, worth a later retry; model:
    the model's own failure, which counts against it. Only model and unsupported count in ok/tried."""
    m = re.search(r"refused \((\d+)\)", err)
    code = int(m.group(1)) if m else None
    if code in (401, 402, 403):
        return "fatal"
    if code == 429 and "exceeded your current quota" in err:
        return "unavailable"
    if code == 429 or (code or 0) >= 500 or any(n in err for n in NETWORK):
        return "transient"
    return "unsupported" if code in (400, 404, 413, 422) else "model"


def frame_meta(rec):
    return {k: rec.get(k) for k in ("suite", "model", "input", "brief", "extra", "seed", "setup", "settings",
                                    "seconds", "costUsd", "size", "ts")}


def stamp(path, meta, caption_lines=None, img=None):
    """Bake what made a picture into it: a visible caption when lines are given, and a `bench` text chunk."""
    img = img or Image.open(path)
    info = PngInfo()
    info.add_text("bench", json.dumps(meta))
    if caption_lines:
        info.add_text("Description", " | ".join(caption_lines))
        img = captioned(img, caption_lines)
    img.save(path, pnginfo=info)


def run_frame(suite, model, extra, seed, remote, options=None):
    """One frame, one record. Never raises: a failure is a record too. `options` are the quality and resolution
    asked for, over the suite's and the model's own."""
    layout, style = work(suite, "layout.png"), work(suite, "style.png")
    inp = remote.get(model, {}).get("input", "ref")
    brief = open(work(suite, "prompt.txt")).read()
    prompt = BRIEF_FOR_INPUT[inp](suite, brief)
    if extra:
        # A per-model nudge, after the brief so it is read last; recorded with every frame it made.
        prompt = prompt.rstrip() + "\n\n" + extra + "\n"
    refs = {"ref": [layout, style], "layout": [layout], "text": []}[inp]
    asked = {"quality": "auto", "resolution": SUITES[suite].get("resolution"), **(options or {})}
    rec = {"ts": time.strftime("%Y-%m-%dT%H:%M:%S"), "suite": suite, "model": model, "input": inp,
           "brief": brief_of(suite), "extra": extra, "seed": seed, "ok": False, "setup": SETUP}
    t0 = time.time()
    try:
        rec["settings"] = openrouter.settings_for(model, refs, aspect=SUITES[suite]["aspect"], seed=seed, **asked)
        blobs, cost, seconds, rec["usage"] = openrouter.request(model, prompt, refs, rec["settings"],
                                                                 retries=FRAME_RETRIES, echo=False)
    except SystemExit as e:
        rec.update(seconds=round(time.time() - t0, 1), error=str(e.code)[:300], kind=classify(str(e.code)))
        rec["transient"] = rec["kind"] in ("transient", "fatal", "unavailable")
        return rec
    # The generator's own clock: the time to the image, without the catalogue lookup.
    rec["seconds"] = round(seconds, 1)
    if cost is not None:
        rec["costUsd"] = cost
    blob, ext = blobs[0]
    os.makedirs(FRAMES, exist_ok=True)
    name, n = f"bench_{suite}_{tag_of(model)}", 1
    while os.path.exists(os.path.join(FRAMES, f"{name}-v{n}.{ext}")):
        n += 1
    path = os.path.join(FRAMES, f"{name}-v{n}.{ext}")
    with open(path, "wb") as f:
        f.write(blob)
    rec["file"] = os.path.relpath(path, ROOT)
    try:
        img = Image.open(path)
        img.load()
        rec["size"] = f"{img.width}x{img.height}"
    except OSError as e:
        rec.update(error=f"not a raster image: {e}"[:300], kind="unsupported", transient=False)
        return rec
    rec["ok"] = True
    if img.format == "PNG":
        stamp(path, frame_meta(rec), img=img)
    return rec


class Guard:
    """The sweep's shared state: the log, the money spent, and the stop that a fatal refusal pulls."""

    def __init__(self, budget, max_frame_usd):
        self.lock, self.spent, self.budget, self.max_frame_usd = threading.Lock(), 0.0, budget, max_frame_usd
        self.stopped = None

    def log(self, rec):
        with self.lock:
            self.spent += rec.get("costUsd") or 0
            with open(RUNS, "a") as f:
                f.write(json.dumps(rec) + "\n")
            if rec.get("kind") == "fatal":
                self.stopped = rec["error"]
        state = (f"{rec['file']} {rec['seconds']:.0f}s ${rec.get('costUsd') or 0:.3f}" if rec["ok"]
                 else f"FAILED ({rec['kind']}): {rec['error']}")
        print(f"[{rec['suite']}] {rec['model']}: {state}", flush=True)

    def affords(self):
        # In flight frames are not yet counted: one dearest frame per worker is what the reserve is for.
        return self.budget is None or self.spent + self.max_frame_usd <= self.budget


def frames(guard, suite, model, seeds, extra, remote, options=None):
    """Runs one model's frames in order; stops at the first frame a later one would fail the same way."""
    for seed in seeds:
        if guard.stopped:
            return
        if not guard.affords():
            print(f"[{suite}] {model}: skipped, budget spent", flush=True)
            return
        rec = run_frame(suite, model, extra, seed, remote, options)
        guard.log(rec)
        if rec.get("kind") in ("unsupported", "fatal", "unavailable"):
            return
        if (rec.get("costUsd") or 0) > guard.max_frame_usd:
            print(f"[{suite}] {model}: ${rec['costUsd']:.2f} a frame is over --max-frame-usd, no more frames", flush=True)
            return


def cmd_run(args):
    if not os.path.exists(work(args.suite, "layout.png")):
        sys.exit("run `prepare` first")
    guard = Guard(None, args.max_frame_usd)
    brief = brief_of(args.suite)
    start = 1 + max((r.get("seed") or 0 for r in read_runs() if r["suite"] == args.suite and r["model"] == args.model
                     and comparable(r, brief)), default=SEED0 - 1)
    remote = remote_models()
    options = ({"resolution": "min", "quality": "min"} if args.cheapest
               else {"quality": args.quality} if args.quality else {})
    frames(guard, args.suite, args.model, range(start, start + args.count), args.extra, remote, options)
    if guard.stopped:
        sys.exit(f"stopped: {guard.stopped}")


def pick(args, remote):
    if not args.models:
        chosen = sorted(remote, key=lambda m: remote[m]["priceOut"])
    else:
        chosen = args.models.split(",")
        for m in chosen:
            if m not in remote:
                sys.exit(f"unknown model {m}; `models` lists them")
    skip = set(filter(None, args.skip.split(",")))
    return [m for m in chosen if m not in skip]


def cmd_sweep(args):
    """Breadth first: every model gets one frame of each suite before any gets its second, cheapest first,
    so a budget that runs out leaves the most models covered."""
    suites = [args.suite] if args.suite else list(SUITES)
    for s in suites:
        if not os.path.exists(work(s, "layout.png")):
            sys.exit("run `prepare` first")
    remote = remote_models()
    models = pick(args, remote)
    budget, left = None, openrouter.credit()[2]
    if left is not None:
        budget = left - args.reserve
        print(f"credit ${left:.2f}, budget ${budget:.2f} (reserve ${args.reserve:.2f})", flush=True)
    guard = Guard(budget, args.max_frame_usd)
    briefs = {s: brief_of(s) for s in suites}

    def todo(depth):
        """(suite, model, seeds) still short of `depth` counted attempts on the current brief."""
        records = read_runs()
        out = []
        for m in models:
            for s in suites:
                mine = [r for r in records if r["model"] == m and r["suite"] == s and not r.get("extra")
                        and comparable(r, briefs[s])]
                counted = [r for r in mine if not r.get("transient")]
                if any(r.get("kind") in ("unsupported", "unavailable") for r in mine) or len(counted) >= depth:
                    continue
                seeds = sorted({r.get("seed") for r in counted if r.get("seed")})
                nxt = [k for k in range(SEED0, SEED0 + depth) if k not in seeds][:depth - len(counted)]
                out.append((s, m, nxt))
        return out

    # Each depth twice: once for every model, then once more for what the service failed, so every model has
    # its first frames, retries included, before any model gets a second — money that runs out mid-sweep
    # still leaves one frame of each.
    for depth in (d for d in range(1, args.count + 1) for _ in range(2)):
        tasks = todo(depth)
        if not tasks or guard.stopped:
            continue
        print(f"-- pass to {depth} frame(s): {len(tasks)} model-suite pairs", flush=True)
        with concurrent.futures.ThreadPoolExecutor(args.jobs) as pool:
            jobs = [pool.submit(frames, guard, s, m, seeds, "", remote) for s, m, seeds in tasks]
            for j in jobs:
                j.result()
    if guard.stopped:
        sys.exit(f"stopped: {guard.stopped}")
    print(f"spent ${guard.spent:.2f}")


# -------------------------------------------------------------------- score

def key_mask(img):
    """Foreground mask: far from the border's dominant colour (a model's magenta is never #FF00FF)."""
    w, h = img.size
    px = img.load()
    border = [px[x, y] for x in range(0, w, 4) for y in (0, h - 1)] + [px[x, y] for y in range(0, h, 4) for x in (0, w - 1)]
    buckets = {}
    for c in border:
        buckets.setdefault((c[0] >> 4, c[1] >> 4, c[2] >> 4), []).append(c)
    mode = max(buckets.values(), key=len)
    key = tuple(sum(c[i] for c in mode) // len(mode) for i in range(3))
    diff = ImageChops.difference(img, Image.new("RGB", img.size, key)).convert("L")
    return diff.point(lambda v: 255 if v > 40 else 0)


def outline_mask(c, size):
    m = Image.new("L", size, 0)
    l, t, w, h = c["box"]
    ImageDraw.Draw(m).rounded_rectangle([l, t, l + w, t + h], radius=c["radius"], fill=255)
    return m


def count(mask):
    return mask.histogram()[255]


def iou(a, b):
    union = count(ImageChops.lighter(a, b))
    return count(ImageChops.multiply(a, b)) / union if union else 0.0


def score_frame(path, cells, size, mirror):
    """Per cell: IoU where the outline is (placement), and once the drawing is centred on it (shape alone, so
    a sheet drawn a few pixels off is not marked as a wrong shape) the IoU, paint outside and holes inside;
    aspect error. A frame of another aspect is stretched onto the layout, as a cutter would read it."""
    img = Image.open(path).convert("RGB")
    got = img.size
    img = img.resize(size, Image.LANCZOS)
    fg = key_mask(img)
    rows = []
    for c in cells:
        x, y, cw, ch = c["cell"]
        box = (x, y, x + cw, y + ch)
        want = outline_mask(c, size).crop(box)
        have = fg.crop(box)
        bbox = have.getbbox()
        l, t, ow, oh = c["box"]
        row = {"id": c["id"], "iou": iou(want, have)}
        if bbox:
            bw, bh = bbox[2] - bbox[0], bbox[3] - bbox[1]
            row["aspectErr"] = abs((bw / bh) / (ow / oh) - 1)
            centred = Image.new("L", have.size, 0)
            centred.paste(have, (round(l - x + ow / 2 - (bbox[0] + bbox[2]) / 2),
                                 round(t - y + oh / 2 - (bbox[1] + bbox[3]) / 2)))
        else:
            row["aspectErr"], centred = 1.0, have
        row["shapeIou"] = iou(want, centred)
        row["outside"] = count(ImageChops.subtract(centred, want)) / count(want)
        row["holes"] = count(ImageChops.subtract(want, centred)) / count(want)
        if mirror and bbox:
            body = have.crop(bbox)
            row["mirror"] = iou(body, body.transpose(Image.FLIP_TOP_BOTTOM))
        rows.append(row)
    return {"size": got, "cells": rows}


def font(px):
    return ImageFont.load_default(size=px)


def captioned(img, lines, px=22):
    pad, lh = 8, px + 6
    head = pad * 2 + lh * len(lines)
    out = Image.new("RGB", (img.width, img.height + head), (24, 24, 24))
    d = ImageDraw.Draw(out)
    for at, line in enumerate(lines):
        d.text((pad, pad + at * lh), line, fill=(235, 235, 235), font=font(px))
    out.paste(img.convert("RGB"), (0, head))
    return out


def tiles_sheet(suite, tiles, scale):
    """tiles: [(label, path)], side by side on the layout's grid at `scale`, each labelled on its top-left."""
    size = SUITES[suite]["size"]
    tw, th = round(size[0] * scale), round(size[1] * scale)
    sheet = Image.new("RGB", (tw * len(tiles) + 8 * (len(tiles) - 1), th), (40, 40, 40))
    d = ImageDraw.Draw(sheet)
    for at, (label, path) in enumerate(tiles):
        x = at * (tw + 8)
        sheet.paste(Image.open(path).convert("RGB").resize((tw, th), Image.LANCZOS), (x, 0))
        box = d.textbbox((x + 6, 4), label, font=font(20))
        d.rectangle([box[0] - 4, box[1] - 3, box[2] + 4, box[3] + 3], fill=(0, 0, 0))
        d.text((x + 6, 4), label, fill=(255, 255, 255), font=font(20))
    return sheet


def group_of(r):
    return r["model"] + (" +nudge" if r.get("extra") else "")


def mean(xs):
    return sum(xs) / len(xs) if xs else 0.0


def cmd_score(args):
    records = read_runs()

    def cost_of(r):
        return r.get("costUsd")

    lines =["# Image model benchmark — measured", "",
             "Evidence for the judge, not a ranking: `judge` and `reveal` give the verdict. IoU is the outline's "
             "cover where the layout put it, shape IoU the same once the drawing is centred on it; ok/tried counts "
             "every attempt but the service's own failures (429, 5xx)."]
    for suite in ([args.suite] if args.suite else list(SUITES)):
        cells = json.load(open(work(suite, "cells.json")))
        size, mirror, brief = SUITES[suite]["size"], suite == "car", brief_of(suite)
        current = [r for r in records if r["suite"] == suite and comparable(r, brief)]
        groups = {}
        for r in current:
            groups.setdefault(group_of(r), []).append(r)
        # The train sheets have no in-game art to measure.
        ingame = [("in-game", work(suite, "ingame.png"))] if os.path.exists(work(suite, "ingame.png")) else []
        scored = {g: [score_frame(p, cells, size, mirror)] for g, p in ingame}
        for g, rs in groups.items():
            scored[g] = [score_frame(os.path.join(ROOT, r["file"]), cells, size, mirror) for r in rs
                         if r["ok"] and os.path.exists(os.path.join(ROOT, r["file"]))]
        lines += ["", f"## {suite} — brief {brief}", ""]
        head = ["model", "input", "ok/tried", "s (median)", "$/frame", "IoU", "shape IoU", "aspect err max",
                "outside", "holes"] + (["mirror"] if mirror else [])
        lines += ["| " + " | ".join(head) + " |", "|" + "---|" * len(head)]
        for g in [g for g, _ in ingame] + sorted(groups):
            rs = groups.get(g, [])
            tried = [r for r in rs if not r.get("transient")]
            ok = [r for r in rs if r["ok"]]
            cs = [c for s in scored[g] for c in s["cells"]]
            if not cs and not tried:
                continue
            cost = [c for c in (cost_of(r) for r in ok) if c is not None]
            row = [g, "" if g == "in-game" else rs[0]["input"],
                   "" if g == "in-game" else f"{len(ok)}/{len(tried)}",
                   f"{statistics.median(r['seconds'] for r in ok):.0f}" if ok else "",
                   f"{mean(cost):.3f}" if cost else "",
                   f"{mean([c['iou'] for c in cs]):.3f}" if cs else "",
                   f"{mean([c['shapeIou'] for c in cs]):.3f}" if cs else "",
                   f"{max(c['aspectErr'] for c in cs):.0%}" if cs else "",
                   f"{mean([c['outside'] for c in cs]):.1%}" if cs else "",
                   f"{mean([c['holes'] for c in cs]):.1%}" if cs else ""]
            if mirror:
                row.append(f"{mean([c.get('mirror', 0) for c in cs]):.3f}" if cs else "")
            lines.append("| " + " | ".join(row) + " |")
        failed = [r for r in current if not r["ok"]]
        if failed:
            lines += ["", "Failed attempts:", ""]
            lines += [f"- {group_of(r)}, seed {r.get('seed')}: {r.get('kind')} — {r['error']}" for r in failed]
        lines += ["", "Per frame:", "", "| frame | size | s | $ | IoU | shape IoU | aspect err max | outside | holes |",
                  "|---|---|---|---|---|---|---|---|---|"]
        for g in sorted(groups):
            ok = [r for r in groups[g] if r["ok"] and os.path.exists(os.path.join(ROOT, r["file"]))]
            for r, s in zip(ok, scored[g]):
                c = s["cells"]
                lines.append(f"| {os.path.basename(r['file'])} | {s['size'][0]}x{s['size'][1]} | {r['seconds']:.0f} | "
                             f"{cost_of(r) if cost_of(r) is not None else ''} | {mean([x['iou'] for x in c]):.3f} | "
                             f"{mean([x['shapeIou'] for x in c]):.3f} | {max(x['aspectErr'] for x in c):.0%} | "
                             f"{mean([x['outside'] for x in c]):.1%} | {mean([x['holes'] for x in c]):.1%} |")
                if args.cells:
                    lines.extend(f"| ↳ {x['id']} | | | | {x['iou']:.3f} | {x['shapeIou']:.3f} | {x['aspectErr']:.0%} | "
                                 f"{x['outside']:.1%} | {x['holes']:.1%} |" for x in c)
            if ok:
                tiles = ingame + [
                    (f"{os.path.basename(r['file'])}  {r.get('size', '')}  {r['seconds']:.0f}s"
                     + (f"  ${cost_of(r):.3f}" if cost_of(r) else ""), os.path.join(ROOT, r["file"]))
                    for r in ok]
                caption = [f"{suite} · {g} · brief {brief} · input {ok[0]['input']}",
                           f"extra: {ok[0].get('extra') or 'none (plain brief)'}"[:200],
                           f"scored {time.strftime('%Y-%m-%d %H:%M')} · .claude/skills/imgbench/bench.py score"]
                out = work(suite, f"compare_{tag_of(g)}.png")
                stamp(out, {"suite": suite, "group": g, "brief": brief, "frames": [r["file"] for r in ok]},
                      caption, tiles_sheet(suite, tiles, 0.5))
    out = os.path.join(WORK, "results.md")
    open(out, "w").write("\n".join(lines) + "\n")
    print(os.path.relpath(out, ROOT))


# -------------------------------------------------------------------- judge

CRITERIA = {
    "style": "matches the in-game art: palette, outline weight, shading, level of finish",
    "detail": "rich, crisp, readable detail at game scale; no mush or noise",
    "brief": "each cell holds what its line asks for, in order: material, equipment / car type, colour, features",
    "shape": "fills its outline at its proportions, street edge or nose on the thick edge, straight down, no perspective",
    "clean": "no text, watermark, frame or grid; flat key background; cells cut cleanly",
}
CARD_HEAD = ["suite", "code", *CRITERIA, "notes"]


def read_card():
    rows = {}
    if os.path.exists(CARD):
        for line in open(CARD):
            cols = [c.strip() for c in line.strip().strip("|").split("|")]
            if len(cols) == len(CARD_HEAD) and cols[0] in SUITES:
                rows[(cols[0], cols[1])] = cols
    return rows


def cmd_judge(args):
    """Blind sheets: in-game | frame at full size, named by a random code. The card lists codes in code
    order, which is unrelated to model or run order."""
    key = json.load(open(CARD_KEY)) if os.path.exists(CARD_KEY) else {}
    known = {v["file"] for v in key.values()}
    wanted = set(args.models.split(",")) if args.models else None
    for suite in ([args.suite] if args.suite else list(SUITES)):
        brief = brief_of(suite)
        os.makedirs(work(suite, "judge"), exist_ok=True)
        for r in read_runs():
            if (r["suite"] != suite or not r["ok"] or not comparable(r, brief) or r["file"] in known
                    or (wanted and r["model"] not in wanted)
                    or not os.path.exists(os.path.join(ROOT, r["file"]))):
                continue
            code = secrets.choice("BCDFGHJKLMNPQRSTVWXZ") + secrets.token_hex(1).upper()
            while code in key:
                code = secrets.choice("BCDFGHJKLMNPQRSTVWXZ") + secrets.token_hex(1).upper()
            key[code] = {"suite": suite, "file": r["file"]}
            caption = [f"{suite} · blind {code} · brief {brief} · input {r['input']} · "
                       f"{'nudged' if r.get('extra') else 'plain brief'}",
                       "left: in the game now · right: the frame, stretched onto the layout's grid"]
            stamp(work(suite, f"judge/{code}.png"), {"suite": suite, "code": code, "brief": brief}, caption,
                  tiles_sheet(suite, [("in-game", work(suite, "ingame.png")), (code, os.path.join(ROOT, r["file"]))], 1))
    json.dump(key, open(CARD_KEY, "w"), indent=1)
    for suite in SUITES:
        # Four frames to a look, in code order, for scoring; the per-code sheets beside the in-game art stay
        # the reference.
        codes = sorted(c for c, v in key.items() if v["suite"] == suite)
        for at in range(0, len(codes), 4):
            group = codes[at:at + 4]
            pairs = [group[:2], group[2:]]
            rows = [tiles_sheet(suite, [(c, os.path.join(ROOT, key[c]["file"])) for c in p], 0.5) for p in pairs if p]
            sheet = Image.new("RGB", (max(r.width for r in rows), sum(r.height for r in rows) + 8 * (len(rows) - 1)),
                              (40, 40, 40))
            for i, r in enumerate(rows):
                sheet.paste(r, (0, i * (rows[0].height + 8)))
            sheet.save(work(suite, f"judge/contact_{at // 4 + 1:02d}.png"))
    card = read_card()
    lines = ["# Blind score card", "",
             "Score each sheet 0–5 by eye, from `.tmp/bench/<suite>/judge/<code>.png` only — not from "
             "`judge_key.json`, `results.md` or the compare sheets, which name the model. `reveal` joins them.", ""]
    lines += [f"- **{k}** — {v}" for k, v in CRITERIA.items()]
    lines += ["", "| " + " | ".join(CARD_HEAD) + " |", "|" + "---|" * len(CARD_HEAD)]
    for code, v in sorted(key.items(), key=lambda kv: (kv[1]["suite"], kv[0])):
        cols = card.get((v["suite"], code), [v["suite"], code] + [""] * (len(CARD_HEAD) - 2))
        lines.append("| " + " | ".join(cols) + " |")
    open(CARD, "w").write("\n".join(lines) + "\n")
    print(os.path.relpath(CARD, ROOT), f"{len(key)} sheets")


def cmd_reveal(args):
    key, card = json.load(open(CARD_KEY)), read_card()
    by_file = {r.get("file"): r for r in read_runs()}
    records = read_runs()
    lines = ["# Image model benchmark — judged", "",
             "Scores are the judge's, by eye, blind to the model; this page only joins and averages them. "
             "Total is out of 25. ok/tried is every attempt but the service's own failures.", ""]
    for suite in SUITES:
        groups = {}
        for (s, code), cols in card.items():
            if s != suite or code not in key:
                continue
            try:
                marks = [float(c) for c in cols[2:2 + len(CRITERIA)]]
            except ValueError:
                continue
            r = by_file[key[code]["file"]]
            if not comparable(r, brief_of(suite)):
                continue   # scored under a setup since replaced
            groups.setdefault(group_of(r), []).append((marks, cols[-1], code))
        if not groups:
            continue
        head = ["model", "frames", "ok/tried", *CRITERIA, "total", "best", "$/frame", "s"]
        lines += [f"## {suite}", "", "| " + " | ".join(head) + " |", "|" + "---|" * len(head)]
        rows = []
        for g, judged in groups.items():
            rs = [r for r in records if r["suite"] == suite and group_of(r) == g and not r.get("transient")
                  and comparable(r, brief_of(suite))]
            ok = [r for r in rs if r["ok"]]
            per = [mean([m[i] for m, _, _ in judged]) for i in range(len(CRITERIA))]
            totals = [sum(m) for m, _, _ in judged]
            cost = [r["costUsd"] for r in ok if r.get("costUsd")]
            rows.append((sum(per), [g, str(len(judged)), f"{len(ok)}/{len(rs)}", *(f"{p:.1f}" for p in per),
                                    f"{sum(per):.1f}", f"{max(totals):.0f}", f"{mean(cost):.3f}" if cost else "0",
                                    f"{statistics.median(r['seconds'] for r in ok):.0f}" if ok else ""]))
        lines += ["| " + " | ".join(r) + " |" for _, r in sorted(rows, key=lambda x: -x[0])]
        notes = [(g, code, note) for g, js in groups.items() for _, note, code in js if note]
        if notes:
            lines += ["", "Notes:", ""] + [f"- {g} ({code}): {n}" for g, code, n in sorted(notes)]
        lines.append("")
    out = os.path.join(WORK, "judged.md")
    open(out, "w").write("\n".join(lines))
    print(os.path.relpath(out, ROOT))


VERDICT = os.path.join(WORK, "verdict.md")
# The judge's own pros and cons per model, written after the card is filled: a `## <model id>` heading and
# the text under it.
NOTES = os.path.join(WORK, "notes.md")


def read_notes():
    notes, model = {}, None
    if os.path.exists(NOTES):
        for line in open(NOTES):
            if line.startswith("## "):
                model = line[3:].strip()
                notes[model] = ""
            elif model:
                notes[model] += line
    # Markdown starts a list only after a blank line; a note written "**Pros:**" then "- ..." would run on.
    return {m: re.sub(r"(?m)^(?!- )(.+)\n(?=- )", r"\1\n\n", text.strip()) for m, text in notes.items()}
REPORT_CSS = """
@page { size: A4; margin: 14mm 12mm; }
body { font: 10pt/1.4 sans-serif; color: #111; }
h1 { font-size: 20pt; } h2 { font-size: 14pt; break-before: page; } h3 { font-size: 11pt; margin: 14px 0 4px; }
table { border-collapse: collapse; font-size: 8pt; margin: 6px 0; }
th, td { border: 1px solid #bbb; padding: 2px 5px; text-align: left; }
.grid { display: grid; grid-template-columns: repeat(var(--cols), 1fr); gap: 10px; margin-bottom: 10px; }
figure { margin: 0; break-inside: avoid; } figure img { width: 100%; display: block; border: 1px solid #ccc; }
figcaption { font-size: 8.5pt; color: #222; margin-top: 2px; }
.model { break-inside: avoid; } pre { white-space: pre-wrap; font-size: 7.5pt; }
"""


def given_section(models, best, names):
    """What every model was handed, read off the harness itself: the pictures, the brief, which input each
    model took, and proof that the style pictures hold none of the cells asked for."""
    inputs = {}
    for (s, m), (_, _, _, r) in best.items():
        inputs.setdefault(r["input"], set()).add(m)
    targets = {"bld": BLD_IDS, "car": list(CARS)}
    styles = {"bld": BLD_STYLE_IDS, "car": list(CAR_STYLE_BODIES)}
    words = {s: len(open(work(s, "prompt.txt")).read().split()) for s in REPORT_SUITES}
    lines = ["", "## What the models were given", "",
             "Every model got the same brief for a sheet and, as far as it takes pictures, the same two:", "",
             f"- **The layout** — the nine cells as outlines on flat magenta, each at its true footprint: roofs "
             f"{SUITES['bld']['size'][0]}×{SUITES['bld']['size'][1]} px with the street front as a thick bottom "
             f"edge, cars {SUITES['car']['size'][0]}×{SUITES['car']['size'][1]} px at {CAR_PX_PER_M} px/m with the "
             "nose as a thick right edge and corners rounded by body type.",
             "- **The style sheet** — other in-game art of the same kind, to show the look. It holds none of the "
             "cells asked for (table below), so it shows the style, not the answer.",
             f"- **The brief** — about {words['bld']} words for roofs and {words['car']} for cars: what the "
             "pictures are, the shape rules (proportions, straight down, nothing outside an outline), the style "
             "in words, and one line per cell: its size in metres and what is on it.",
             "- **Not given:** the game's own art for the nine cells, shown beside each sheet above only as the "
             "judge's reference.", "",
             "| | asked to draw | style sheet |", "|---|---|---|"]
    lines += [f"| {names[s]} | {', '.join(targets[s])} | {', '.join(styles[s])} |" for s in REPORT_SUITES]
    cells = {"bld": (building_cells(BLD_IDS), building_cells(BLD_STYLE_IDS)),
             "car": (car_cells({i: body for i, (body, _) in CARS.items()}), car_cells(CAR_STYLE_BODIES))}
    shared = [(s, a["id"], b["id"]) for s, (ts, ss) in cells.items() for a in ts for b in ss
              if a["id"] == b["id"] or open(a["sprite"], "rb").read() == open(b["sprite"], "rb").read()]
    lines += ["", ("No id and no sprite file is in both columns (checked when this report was built). "
                   if not shared else f"**In both columns: {shared}.** ")
              + "Some style cars share a body type with a target (a muscle car, a saloon), which is what teaches "
                "the look; the target's own colour and features are in its brief line only.", "",
              "How much of that a model got depends on how many pictures it takes:", "",
              "| input | given | models |", "|---|---|---|"]
    given = {"ref": "brief + layout + style sheet",
             "layout": "brief + layout; the style in words instead of the style sheet",
             "text": "the brief's rules and cell lines only, led by a sentence describing the 3×3 grid"}
    lines += [f"| `{k}` | {given[k]} | {', '.join(sorted(inputs[k]))} |" for k in given if k in inputs]
    lines += ["", "Each model was asked for the sheet's aspect (1:1 roofs, 3:2 cars) or its nearest, and got the "
              "pictures at full size. A nudged frame is the same input plus one line for that model alone, quoted "
              "where it is shown."]
    return lines


def figure(path, caption, base):
    return (f'<figure><img src="{os.path.relpath(path, base)}"><figcaption>{caption}</figcaption></figure>')


def cmd_report(args):
    """The judged result as one document: verdict, ranking, every model's best frame per suite as generated,
    and the inputs every model was given. Markdown is the source; the PDF is printed from it."""
    key, card = json.load(open(CARD_KEY)), read_card()
    by_file = {r.get("file"): r for r in read_runs()}
    records = read_runs()
    judged = {}   # (suite, model) -> [(total, marks, note, record)]
    for (suite, code), cols in card.items():
        if suite not in REPORT_SUITES:
            continue
        try:
            marks = [float(c) for c in cols[2:2 + len(CRITERIA)]]
        except ValueError:
            continue
        r = by_file[key[code]["file"]]
        if not comparable(r, brief_of(suite)):
            continue   # scored under a setup since replaced
        judged.setdefault((suite, r["model"]), []).append((sum(marks), marks, cols[-1], r))
    best = {k: max(v, key=lambda j: (j[0], -j[3]["seconds"])) for k, v in judged.items()}
    models = sorted({m for _, m in judged})
    base = WORK
    names = {"bld": "Roofs", "car": "Cars"}
    lines = ["# Image model benchmark", "",
             f"{time.strftime('%Y-%m-%d')} · {len(models)} models · two 3×3 sheets from the game, roofs and cars", "",
             "Every model got the same brief and, as far as it takes pictures, the same two: the layout and a style "
             "sheet of in-game art. Each image below is a model's best frame, as generated, unedited: of two on "
             "the plain brief, and of any made after it with a line added for that model alone (marked nudged). "
             "Scored by eye, blind to the model, out of 25. The numbers are at the end."]
    if os.path.exists(VERDICT):
        lines += ["", "## Verdict", "", open(VERDICT).read().strip()]
    for suite in REPORT_SUITES:
        ranked = sorted((best[k] for k in best if k[0] == suite), key=lambda j: (-j[0], j[3]["seconds"]))
        lines += ["", f"## {names[suite]}", "", '<div class="grid" style="--cols:3">',
                  figure(work(suite, "layout.png"), "given: the layout", base),
                  figure(work(suite, "style.png"), "given: the style sheet", base),
                  figure(work(suite, "ingame.png"), "the game's own art for these cells (not given)", base),
                  "</div>", "", '<div class="grid" style="--cols:2">']
        lines += [figure(os.path.join(ROOT, r["file"]),
                         f"<b>{i + 1}. {r['model']}</b> · {tot:.0f}/25" + (" · nudged" if r.get("extra") else "")
                         + (f" · {note}" if note else ""), base)
                  for i, (tot, _, note, r) in enumerate(ranked)]
        lines.append("</div>")
    notes = read_notes()
    overall = sorted(models, key=lambda m: -sum(best[(s, m)][0] for s in REPORT_SUITES if (s, m) in best))
    lines += ["", "## Ranking", "",
              f"Both sheets together, out of {25 * len(REPORT_SUITES)}: each model's best roof frame plus its best car "
              "frame."]
    for i, m in enumerate(overall):
        scores = " · ".join(f"{names[s].lower()} {best[(s, m)][0]:.0f}" for s in REPORT_SUITES if (s, m) in best)
        total = sum(best[(s, m)][0] for s in REPORT_SUITES if (s, m) in best)
        lines += ["", '<div class="model" markdown="1">', "",
                  f"### {i + 1}. {m} — {total:.0f} ({scores})", "", '<div class="grid" style="--cols:2">']
        lines += [figure(os.path.join(ROOT, best[(s, m)][3]["file"]),
                         names[s] + (f" · nudged: “{best[(s, m)][3]['extra']}”" if best[(s, m)][3].get("extra") else ""),
                         base) for s in REPORT_SUITES if (s, m) in best]
        lines += ["</div>", "", notes.get(m, "_No verdict written for this model._"), "", "</div>"]
    lines += given_section(models, best, names)
    lines += ["", "## Numbers", "",
              "Criteria, 0–5 each: " + "; ".join(f"**{k}** — {v}" for k, v in CRITERIA.items()) + ". "
              "Input: `ref` both pictures, `layout` the layout only with the style in words, `text` none. Every "
              "model ran through OpenRouter's Image API at its own default resolution and quality. Shape IoU and "
              "paint outside are measured on the best frame with each drawing centred on its outline: evidence, "
              "not the score."]
    for suite in REPORT_SUITES:
        cells = json.load(open(work(suite, "cells.json")))
        size = SUITES[suite]["size"]
        rows = []
        for m in models:
            js = judged.get((suite, m))
            if not js:
                continue
            tot, marks, _, r = best[(suite, m)]
            s = score_frame(os.path.join(ROOT, r["file"]), cells, size, suite == "car")["cells"]
            tried = [x for x in records if x["suite"] == suite and x["model"] == m and comparable(x, brief_of(suite))
                     and not x.get("transient") and not x.get("extra")]
            per = [mean([j[1][i] for j in js]) for i in range(len(CRITERIA))]
            cost = [x["costUsd"] for x in tried if x["ok"] and x.get("costUsd")]
            rows.append((tot, mean([j[0] for j in js]), [
                m, r["input"], f"{tot:.0f}", f"{mean([j[0] for j in js]):.1f}",
                *(f"{p:.1f}" for p in per), f"{mean([c['shapeIou'] for c in s]):.2f}",
                f"{mean([c['outside'] for c in s]):.0%}", f"{sum(x['ok'] for x in tried)}/{len(tried)}",
                f"{mean(cost):.3f}" if cost else "0",
                f"{statistics.median(x['seconds'] for x in tried if x['ok']):.0f}"]))
        head = ["#", "model", "input", "best", "mean", *CRITERIA, "shape IoU", "outside", "ok/tried",
                "$/frame", "s"]
        lines += ["", f"### {names[suite]}", "", "| " + " | ".join(head) + " |", "|" + "---|" * len(head)]
        lines += [f"| {i + 1} | " + " | ".join(row) + " |"
                  for i, (_, _, row) in enumerate(sorted(rows, key=lambda x: (-x[0], -x[1])))]
        lines += ["", "Best frames:", ""]
        for tot, marks, note, r in sorted((best[k] for k in best if k[0] == suite), key=lambda j: -j[0]):
            seed = r.get("seed") if "seed" in (r.get("settings") or {}) else "not taken"
            seed = f"{seed} · nudged: “{r['extra']}”" if r.get("extra") else seed
            lines.append(f"- {r['model']}: `{os.path.basename(r['file'])}` · {r.get('size', '')} · seed {seed} · "
                         f"{r['seconds']:.0f} s · ${r.get('costUsd') or 0:.3f} · "
                         + ", ".join(f"{k} {v:.0f}" for k, v in zip(CRITERIA, marks)) + (f" — {note}" if note else ""))
    lines += ["", "## The briefs"]
    for suite in REPORT_SUITES:
        lines += ["", f"### {suite}", "", "```", open(work(suite, "prompt.txt")).read().strip(), "```"]
    md = os.path.join(WORK, "report.md")
    open(md, "w").write("\n".join(lines) + "\n")
    print(os.path.relpath(md, ROOT))
    md_to_pdf(md, "Image model benchmark", REPORT_CSS, os.path.join(WORK, "report.pdf"))
    html = os.path.join(WORK, "report.html")
    if args.preview:
        # A copy small enough to send (an upload limit of 30 MB against ~45 MB of full frames): every picture
        # as a JPEG of at most PREVIEW_PX. The report itself keeps the generated files.
        shrunk = os.path.join(WORK, "preview")
        os.makedirs(shrunk, exist_ok=True)

        def shrink(m):
            src = os.path.normpath(os.path.join(WORK, m.group(1)))
            dst = os.path.join(shrunk, hashlib.sha1(src.encode()).hexdigest()[:12] + ".jpg")
            img = Image.open(src).convert("RGB")
            img.thumbnail((PREVIEW_PX, PREVIEW_PX), Image.LANCZOS)
            img.save(dst, quality=85)
            return f'<img src="{os.path.relpath(dst, WORK)}">'
        small = os.path.join(WORK, "report-preview.html")
        open(small, "w").write(re.sub(r'<img src="([^"]+)">', shrink, open(html).read()))
        print_pdf(small, os.path.join(WORK, "report-preview.pdf"))


PREVIEW_PX = 1000


def print_pdf(html, pdf):
    browser = shutil.which("chromium-browser") or shutil.which("chromium") or shutil.which("google-chrome")
    if not browser:
        sys.exit(f"no Chromium to print {os.path.relpath(html, ROOT)} to PDF")
    subprocess.run([browser, "--headless", "--no-pdf-header-footer", f"--print-to-pdf={pdf}", "file://" + html],
                   capture_output=True, check=True)
    print(os.path.relpath(pdf, ROOT))


TRIES_CSS = """
@page { size: A4 landscape; margin: 10mm 12mm; }
.mark { font-weight: bold; } .good { color: #1d7a34; } .partial { color: #a86a00; } .fail { color: #b3261e; }
.own, .top, .all { padding: 0 4px; font-weight: bold; white-space: nowrap; }
.own { background: #ffe9a8; } .top { background: #1f5fbf; color: #fff; } .all { background: #7a1fbf; color: #fff; }
"""


def cmd_tries(args):
    """<suite>/tries.md and .pdf: a section per try, every model's n-th frame on the current brief embedded as
    generated, captioned with the line added to the brief if any and the verdict by eye from <suite>/tries.json:
    {"order": [model id], "overall_best": frame file name of the best of every frame,
     "try_best": {"<n>": frame file name of the best of try n across every model},
     "frames": {frame file name: ["good"|"partial"|"fail", note, true if the model's own best of all its tries]}}."""
    suite, brief = args.suite, brief_of(args.suite)
    verdicts = json.load(open(work(suite, "tries.json")))
    by_model = {}
    for r in read_runs():   # in the order they ran
        if r["suite"] == suite and comparable(r, brief) and not r.get("transient"):
            by_model.setdefault(r["model"], []).append(r)
    order = verdicts.get("order") or sorted(by_model)
    tries = max(map(len, by_model.values()))
    base = work(suite, "")

    try_best = {int(n): f for n, f in verdicts.get("try_best", {}).items()}

    def verdict(r, n):
        """(mark, note, badges): the model's own best of its tries, the best of try n across models, and the
        best of everything."""
        name = os.path.basename(r["file"])
        mark, note, *own = verdicts["frames"].get(name, ["unjudged", ""])
        badges = (' <span class="own">OWN BEST</span>' if own and own[0] else "") + \
                 (' <span class="top">TRY BEST</span>' if try_best.get(n) == name else "") + \
                 (' <span class="all">OVERALL BEST</span>' if verdicts.get("overall_best") == name else "")
        return mark, note, badges

    def model_of(name):
        return next((m for m, rs in by_model.items() for r in rs if os.path.basename(r.get("file") or "") == name), "?")

    spent = sum(r.get("costUsd") or 0 for rs in by_model.values() for r in rs)
    lines = [f"# {suite.capitalize()} sheet — every try", "",
             f"{time.strftime('%Y-%m-%d')} · brief {brief} · {len(order)} models · {tries} tries each · "
             f"${spent:.2f} in all", "",
             "Each frame is embedded as the model returned it, at its own resolution: zoom in for detail. "
             "A try is the plain brief, or the brief with one line of mine added after looking at the earlier "
             "tries (quoted under the frame). The verdict is by eye: "
             '<span class="mark good">GOOD</span> usable as asked, <span class="mark partial">PARTIAL</span> '
             'right view but off in size or detail, <span class="mark fail">FAIL</span> wrong view or wrong '
             'layout. <span class="own">OWN BEST</span> marks the one frame kept for each model, of all its '
             'tries; <span class="top">TRY BEST</span> the best frame of that try across every model; '
             '<span class="all">OVERALL BEST</span> the best frame of all.', "",
             "| # | model | " + " | ".join(f"try {n}" for n in range(1, tries + 1)) + " |",
             "|---|---|" + "---|" * tries]
    for at, m in enumerate(order, 1):
        cells = []
        for n, r in enumerate(by_model.get(m, [])[:tries], 1):
            mark, _, badges = verdict(r, n) if r["ok"] else ("fail", "", "")
            cells.append(f'<span class="mark {mark}">{mark.upper()}</span>' + badges)
        lines.append(f"| {at} | {m} | " + " | ".join(cells + ["—"] * (tries - len(cells))) + " |")
    lines += ["", "### Given to every model", "", '<div class="grid" style="--cols:3">',
              figure(work(suite, "layout.png"), "the layout", base),
              figure(work(suite, "style.png"), "the style sheet", base), "</div>"]
    for n in range(1, tries + 1):
        top = f" — best: {model_of(try_best[n])}" if n in try_best else ""
        lines += ["", f"## Try {n}{top}", "", '<div class="grid" style="--cols:3">']
        for at, m in enumerate(order, 1):
            rs = by_model.get(m, [])[n - 1:n]
            head = f"<b>{at}. {m}</b>"
            if not rs:
                lines.append(f"<figure><figcaption>{head} · no try {n}</figcaption></figure>")
                continue
            r = rs[0]
            asked = f"+ “{html_lib.escape(r['extra'])}”" if r.get("extra") else "plain brief"
            if not r["ok"]:
                lines.append(f'<figure><figcaption>{head} · {asked}<br><span class="mark fail">FAILED</span> '
                             f"({r.get('kind')}): {html_lib.escape(r.get('error', ''))}</figcaption></figure>")
                continue
            mark, note, badges = verdict(r, n)
            asked += "".join(f" · {k} {v}" for k, v in (r.get("settings") or {}).items() if k in ("resolution", "quality"))
            lines.append(figure(os.path.join(ROOT, r["file"]),
                                f"{head}{badges} · {r.get('size', '')} · {r['seconds']:.0f} s · ${r.get('costUsd') or 0:.3f}"
                                f"<br>{asked}<br><span class=\"mark {mark}\">{mark.upper()}</span> {note}", base))
        lines.append("</div>")
    lines += ["", "## The brief", "", "```", open(work(suite, "prompt.txt")).read().strip(), "```"]
    md = work(suite, "tries.md")
    open(md, "w").write("\n".join(lines) + "\n")
    print(os.path.relpath(md, ROOT))
    md_to_pdf(md, f"{suite.capitalize()} tries", REPORT_CSS + TRIES_CSS, work(suite, "tries.pdf"))


def cmd_pdf(args):
    """A Markdown report written by hand, printed beside it in the tries' landscape style."""
    md = os.path.abspath(args.md)
    title = next((l[2:].strip() for l in open(md) if l.startswith("# ")), os.path.basename(md))
    md_to_pdf(md, title, REPORT_CSS + TRIES_CSS, os.path.splitext(md)[0] + ".pdf")


def md_to_pdf(md, title, css, pdf):
    try:
        import markdown
    except ImportError:
        sys.exit("the PDF needs the `markdown` package: run this with a Python that has it (.tmp/venv/bin/python)")
    html = os.path.splitext(md)[0] + ".html"
    body = markdown.markdown(open(md).read(), extensions=["tables", "fenced_code", "md_in_html"])
    open(html, "w").write(f"<!doctype html><meta charset=utf-8><title>{title}</title><style>{css}</style>{body}")
    print_pdf(html, pdf)


def main():
    ap = argparse.ArgumentParser(prog="bench.py", description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("prepare")
    p.add_argument("--suite", choices=SUITES, help="only this suite (default every one)")
    p.set_defaults(func=cmd_prepare)
    sub.add_parser("models").set_defaults(func=cmd_models)
    r = sub.add_parser("run")
    r.add_argument("--suite", choices=SUITES, required=True)
    r.add_argument("--model", required=True)
    r.add_argument("-n", "--count", type=int, default=2)
    r.add_argument("--extra", default="", help="text appended to the brief for this model only")
    r.add_argument("--quality", help="a remote model's quality tier, or min; recorded in the frame's settings")
    r.add_argument("--cheapest", action="store_true",
                   help="a remote model at its smallest resolution and lowest quality, as far as it takes either")
    r.add_argument("--max-frame-usd", type=float, default=1.0, help="no further frames after one dearer than this")
    r.set_defaults(func=cmd_run)
    w = sub.add_parser("sweep")
    w.add_argument("--suite", choices=SUITES)
    w.add_argument("--models", default="", help="comma-separated ids (default every image model, cheapest first)")
    w.add_argument("--skip", default="", help="comma-separated ids to leave out")
    w.add_argument("-n", "--count", type=int, default=2, help="frames per model and suite on the plain brief")
    w.add_argument("--jobs", type=int, default=4, help="models at once")
    w.add_argument("--reserve", type=float, default=0.5, help="USD of credit left untouched")
    w.add_argument("--max-frame-usd", type=float, default=0.5,
                   help="a model whose frame costs more gets no more frames; also the per-worker budget margin")
    w.set_defaults(func=cmd_sweep)
    s = sub.add_parser("score")
    s.add_argument("--suite", choices=SUITES)
    s.add_argument("--cells", action="store_true", help="a row per cell as well")
    s.set_defaults(func=cmd_score)
    j = sub.add_parser("judge")
    j.add_argument("--suite", choices=SUITES)
    j.add_argument("--models", help="comma-separated ids to put on the card (default every comparable frame)")
    j.set_defaults(func=cmd_judge)
    sub.add_parser("reveal").set_defaults(func=cmd_reveal)
    p = sub.add_parser("report", help="verdict, ranking and best frames as .tmp/bench/report.md and report.pdf")
    p.add_argument("--preview", action="store_true",
                   help=f"also report-preview.pdf, every picture a JPEG of at most {PREVIEW_PX} px, small enough to send")
    p.set_defaults(func=cmd_report)
    t = sub.add_parser("tries", help="tries.pdf, a page per try: every model's frame of it with the verdict by eye")
    t.add_argument("--suite", choices=SUITES, required=True)
    t.set_defaults(func=cmd_tries)
    f = sub.add_parser("pdf", help="print a hand-written Markdown report to PDF beside it, landscape")
    f.add_argument("md")
    f.set_defaults(func=cmd_pdf)
    args = ap.parse_args()
    args.func(args)


if __name__ == "__main__":
    main()
