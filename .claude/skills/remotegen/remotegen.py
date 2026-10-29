#!/usr/bin/env python3
"""Generate and edit the project's art through OpenRouter's Image API, on the owner's settings.

Frames, their records and every picture they were asked from go to .tmp/gen/; `qq sprites` cuts a
frame and keeps what is used, with its lineage, under raw_assets/. Stdlib, and PIL for --mask.

  edit --image style.png --cells roofs_a.cells.json -p "$(cat roofs_a.txt)" --aspect 3:2 --name roofs_a
  gen  "a red hatchback, top-down" --name sedan_red
  credit
  list -n 10 | show sedan_red-v2

The model catalogue and prices are imgbench's (`bench.py models`): choosing a model is a benchmark's work.

The key is read from OPENROUTER_API_KEY or ~/.qq/openrouter.key (the one `qq pilot` uses); the
project holds none and it is never printed. Every call is billed to it.
"""

import argparse
import hashlib
import json
import os
import re
import sys
import time

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import openrouter  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
# A frame is scratch: a sheet never goes into git, only the variants cut from it and kept (raw_assets/index.md).
GEN_DIR = os.path.join(ROOT, ".tmp", "gen")
FRAMES_DIR = os.path.join(GEN_DIR, "frames")
INDEX = os.path.join(GEN_DIR, "index.jsonl")
# Every picture a frame was asked from, by content, as it was when sent: a style picture redrawn under the same
# path afterwards must not rewrite what an older frame's record says it was given.
INPUTS_DIR = os.path.join(GEN_DIR, "inputs")
# The records of frames a kept variant traces to: they outlive .tmp/, so a version or an attempt is counted here too.
KEPT_FRAMES_DIR = os.path.join(ROOT, "raw_assets", "_frames")

# The owner's pick for sprite sheets: the only model to draw every sheet of imgbench's train benchmark top-down
# and right, and at quality low half the price with no visible loss (2026-10-18).
DEFAULT_MODEL = "openai/gpt-image-2.5-sunburst"
DEFAULT_QUALITY = "low"
# The owner's call (2026-10-20): a frame comes back with a transparent background where the model offers one, so a
# sprite's edge is the model's own alpha and not a magenta key pulled out of it.
DEFAULT_BACKGROUND = "transparent"
# A trial (2026-10-20), not an owner's pick: the cheapest image-to-video on OpenRouter at 480p, $0.02 a second.
DEFAULT_VIDEO_MODEL = "x-ai/grok-imagine-video-1.5-lite"
# The owner's limit: one generation (one --name) gets this many frames, then it is reported back rather than
# tried again.
MAX_ATTEMPTS = 3


def read_index():
    """Every frame's record, oldest first; a line a crash left unreadable is skipped."""
    if not os.path.exists(INDEX):
        return []
    out = []
    with open(INDEX) as f:
        for line in f:
            try:
                out.append(json.loads(line))
            except json.JSONDecodeError:
                pass
    return out


def append_index(rec):
    with open(INDEX, "a") as f:
        f.write(json.dumps(rec) + "\n")


def next_path(name, ext):
    """.tmp/gen/frames/<name>-v<N>.<ext>, N the first version neither scratch nor a kept record holds."""
    n = 1
    while (os.path.exists(os.path.join(FRAMES_DIR, f"{name}-v{n}.{ext}"))
           or os.path.exists(os.path.join(KEPT_FRAMES_DIR, f"{name}-v{n}.json"))):
        n += 1
    return os.path.join(FRAMES_DIR, f"{name}-v{n}.{ext}"), n


def resolve_image(ref):
    """A path, a frame id (roofs_house_a-v2), or a generation's name for its newest frame."""
    for cand in (ref, os.path.join(ROOT, ref)):
        if os.path.isfile(cand):
            return os.path.abspath(cand)
    for rec in reversed(read_index()):
        if ref in (rec.get("id"), rec.get("name")) and os.path.isfile(os.path.join(ROOT, rec["file"])):
            return os.path.join(ROOT, rec["file"])
    sys.exit(f"cannot resolve image: {ref}")


def composite(original_path, new_blob, mask_path, feather):
    """The new frame only where the mask is transparent; everywhere else the original, byte for byte."""
    import io
    from PIL import Image, ImageFilter

    original = Image.open(original_path).convert("RGBA")
    new = Image.open(io.BytesIO(new_blob)).convert("RGBA")
    mask = Image.open(mask_path).convert("RGBA")
    if not original.size == new.size == mask.size:
        sys.exit(f"mask/image size mismatch: image {original.size}, output {new.size}, mask {mask.size}")
    weight = mask.getchannel("A").point(lambda a: 255 - a)
    if feather:
        weight = weight.filter(ImageFilter.BoxBlur(feather)).filter(ImageFilter.BoxBlur(feather))
    out = io.BytesIO()
    Image.composite(new, original, weight).save(out, "PNG")
    return out.getvalue()


#: Below this alpha a frame's pixel is the glow the model lays round every drawing on a transparent background, never
#: paint (`qq sprites cut` keeps a rim only over 64).
GLOW_ALPHA = 16


def clear_hidden(path):
    """A transparent frame's glow made clear and black: the model leaves its glow's colour in the RGB of pixels at or
    near alpha 0, which a viewer that ignores alpha shows as haze and flares round every drawing."""
    import numpy as np
    from PIL import Image

    im = Image.open(path)
    if im.mode != "RGBA":
        return
    px = np.asarray(im).copy()
    hidden = px[..., 3] < GLOW_ALPHA
    if hidden.any() and px[hidden].any():
        px[hidden] = 0
        Image.fromarray(px, "RGBA").save(path, "PNG")


def snapshot(path, role):
    """The picture as sent, kept under its content hash; `frame` names it when it is itself a frame."""
    with open(path, "rb") as f:
        blob = f.read()
    sha = hashlib.sha256(blob).hexdigest()[:16]
    ext = os.path.splitext(path)[1].lower().lstrip(".") or "png"
    os.makedirs(INPUTS_DIR, exist_ok=True)
    kept = os.path.join(INPUTS_DIR, f"{sha}.{ext}")
    if not os.path.exists(kept):
        with open(kept, "wb") as f:
            f.write(blob)
    frame = (os.path.splitext(os.path.basename(path))[0]
             if os.path.dirname(os.path.abspath(path)) == FRAMES_DIR else None)
    return {"role": role, "sha": sha, "ext": ext, "frame": frame, "was": os.path.relpath(path, ROOT)}


def check_attempts(name, past_cap):
    """Refuses a frame past MAX_ATTEMPTS under one name, before anything is billed. Counts the frames the index and
    the kept records hold; a refusal that returned no image is in neither and is counted by whoever ran it."""
    kept = os.listdir(KEPT_FRAMES_DIR) if os.path.isdir(KEPT_FRAMES_DIR) else []
    made = len({r["id"] for r in read_index() if r.get("name") == name}
               | {f[:-5] for f in kept if f.endswith(".json") and re.fullmatch(rf"{re.escape(name)}-v\d+", f[:-5])})
    if made >= MAX_ATTEMPTS and not past_cap:
        sys.exit(f"{name} already has {made} frame(s) and {MAX_ATTEMPTS} is the limit for one generation: "
                 "report the frames to the owner instead of trying again (--past-cap only on the owner's word)")


def generate(args, name, op, refs, inputs, mask=None):
    check_attempts(name, args.past_cap)
    settings = openrouter.settings_for(args.model, refs, aspect=args.aspect, quality=args.quality,
                                       background=args.background)
    print(f"settings {json.dumps(settings)}", file=sys.stderr)
    frames, cost, _, usage = openrouter.request(args.model, args.prompt, refs, settings)
    if mask:
        frames = [(composite(refs[0], blob, mask, args.feather), "png") for blob, _ in frames]
    for blob, ext in frames:
        path, ver = next_path(name, ext)
        with open(path, "wb") as f:
            f.write(blob)
        if ext == "png":
            clear_hidden(path)
        size = openrouter.png_size(path)
        append_index({
            "id": f"{name}-v{ver}",
            "ts": time.strftime("%Y-%m-%dT%H:%M:%S"),
            "op": op,
            "name": name,
            "file": os.path.relpath(path, ROOT),
            "model": args.model,
            "size": f"{size[0]}x{size[1]}" if size else None,
            "settings": settings,
            "costUsd": cost,
            # What the cost was made of — input tokens (images among them), cached, output — so a change to what a
            # frame is sent can be weighed by its tokens and not guessed at.
            "usage": usage,
            "prompt": args.prompt,
            "inputs": inputs,
            **({"cells": cells_listed(args.cells)} if getattr(args, "cells", None) else {}),
        })
        print(os.path.relpath(path, ROOT))


def cells_listed(path):
    """The asset ids a frame asked without a layout draws, in the brief's order (left to right, top to bottom): what
    `qq sprites cut` matches the drawings it finds to."""
    with open(path, encoding="utf-8") as f:
        ids = json.load(f)
    if not isinstance(ids, list) or not ids or not all(isinstance(i, str) and re.fullmatch(r"[a-z0-9_]+", i) for i in ids):
        sys.exit(f"{path}: a cells file is a JSON list of asset ids")
    return ids


def cmd_gen(args):
    name = args.name or re.sub(r"[^a-z0-9]+", "_", args.prompt.lower()).strip("_")[:40].rstrip("_") or "image"
    generate(args, name, "gen", [], [])


def cmd_edit(args):
    refs = [resolve_image(r) for r in args.image]
    mask = resolve_image(args.mask) if args.mask else None
    name = args.name or re.sub(r"-v\d+$", "", os.path.splitext(os.path.basename(refs[0]))[0])
    inputs = [snapshot(p, f"image {n}") for n, p in enumerate(refs, 1)]
    if mask:
        inputs.append(snapshot(mask, "mask"))
    if args.aspect is None:
        size = openrouter.png_size(refs[0])
        if size:
            args.aspect = openrouter.nearest_aspect(*size)
    generate(args, name, "edit", refs, inputs, mask)


def cmd_video(args):
    """A clip from a first frame: the Video API's settings checked against the model's record, the clip kept as a
    frame of its own (an .mp4) with the same record a picture's frame gets."""
    check_attempts(args.name, args.past_cap)
    m = openrouter.video_model(args.model)
    for name, value, offered in (("resolution", args.resolution, m.get("supported_resolutions")),
                                 ("aspect", args.aspect, m.get("supported_aspect_ratios")),
                                 ("duration", args.duration, m.get("supported_durations"))):
        if offered and value not in offered:
            sys.exit(f"{args.model} offers {name} {offered}, not {value}")
    settings = {"resolution": args.resolution, "aspect_ratio": args.aspect, "duration": args.duration}
    # The record's `generate_audio` says whether the setting is offered; False is a model that always renders
    # sound and refuses the setting.
    if m.get("generate_audio"):
        settings["generate_audio"] = False
    for option in args.option:
        # A provider's own parameter, as the model lists it (`allowed_passthrough_parameters`), sent under
        # provider.options.<slug>.parameters.
        slug, _, kv = option.partition(":")
        key, _, value = kv.partition("=")
        if key not in (m.get("allowed_passthrough_parameters") or []):
            sys.exit(f"{args.model} passes through {m.get('allowed_passthrough_parameters')}, not {key!r}")
        settings.setdefault("provider", {"options": {}})["options"].setdefault(slug, {"parameters": {}})[
            "parameters"][key] = value
    image = resolve_image(args.image)
    last = resolve_image(args.last_image) if args.last_image else None
    if last and "last_frame" not in (m.get("supported_frame_images") or []):
        sys.exit(f"{args.model} takes no last frame")
    print(f"settings {json.dumps(settings)}", file=sys.stderr)
    blob, cost, _, usage = openrouter.video_request(args.model, args.prompt, image, settings, last)
    path, ver = next_path(args.name, "mp4")
    with open(path, "wb") as f:
        f.write(blob)
    append_index({
        "id": f"{args.name}-v{ver}", "ts": time.strftime("%Y-%m-%dT%H:%M:%S"), "op": "video", "name": args.name,
        "file": os.path.relpath(path, ROOT), "model": args.model, "settings": settings, "costUsd": cost,
        "usage": usage, "prompt": args.prompt,
        "inputs": [snapshot(image, "first frame")] + ([snapshot(last, "last frame")] if last else []),
    })
    print(os.path.relpath(path, ROOT))


def cmd_credit(args):
    used, limit, left = openrouter.credit()
    print(f"used ${used:.4f}" + (f" of ${limit:.2f}, ${left:.4f} left" if limit is not None else ", no limit"))


def cmd_list(args):
    recs = [r for r in read_index() if not args.filter or args.filter in r.get("id", "") + r.get("prompt", "")]
    for r in recs[-args.count:]:
        print(f"{r['id']:<28} {r['op']:<4} {r['file']}  {r['prompt'][:56]}")


def cmd_show(args):
    for r in reversed(read_index()):
        if args.ref in (r.get("id"), r.get("name")):
            print(json.dumps(r, indent=2))
            return
    sys.exit(f"not found: {args.ref}")


def flags(p):
    p.add_argument("--name", help="generation name, the frame's stem (default: slug of prompt / first image's stem)")
    p.add_argument("--aspect", choices=openrouter.ASPECTS, default=None,
                   help="aspect ratio (default: the model's own for gen, the first image's nearest for edit)")
    p.add_argument("--model", default=DEFAULT_MODEL, help=f"owner's word only (default {DEFAULT_MODEL})")
    p.add_argument("--quality", default=DEFAULT_QUALITY, help=f"owner's word only (default {DEFAULT_QUALITY})")
    p.add_argument("--background", default=DEFAULT_BACKGROUND,
                   help=f"owner's word only (default {DEFAULT_BACKGROUND}, where the model offers it)")
    p.add_argument("--cells", default=None,
                   help="JSON list of the asset ids a frame without a layout draws, in the brief's order; kept in its record")
    p.add_argument("--past-cap", action="store_true",
                   help=f"allow more than {MAX_ATTEMPTS} frames under one --name: owner's word only")


def main():
    ap = argparse.ArgumentParser(prog="remotegen.py", description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    g = sub.add_parser("gen", help="generate from a text prompt")
    g.add_argument("prompt")
    flags(g)
    g.set_defaults(func=cmd_gen)

    e = sub.add_parser("edit", help="draw from one or more reference images")
    e.add_argument("--image", action="append", required=True,
                   help="path, id or name; repeat for several, named in the prompt as image 1, image 2, ...")
    e.add_argument("-p", "--prompt", required=True)
    e.add_argument("--mask", help="PNG whose transparent area is what gets replaced")
    e.add_argument("--feather", type=int, default=2, help="pixels of blend at the mask edge")
    flags(e)
    e.set_defaults(func=cmd_edit)

    v = sub.add_parser("video", help="a clip from a first frame, through the Video API")
    v.add_argument("--image", required=True, help="the first frame: path, id or name")
    v.add_argument("--last-image", default=None, help="the last frame, where the model takes one")
    v.add_argument("-p", "--prompt", required=True)
    v.add_argument("--name", required=True, help="generation name, the clip's stem")
    v.add_argument("--model", default=DEFAULT_VIDEO_MODEL, help=f"default {DEFAULT_VIDEO_MODEL}")
    v.add_argument("--resolution", default="480p")
    v.add_argument("--aspect", default="3:2")
    v.add_argument("--duration", type=int, default=1, help="seconds, as the model offers them")
    v.add_argument("--option", action="append", default=[], metavar="SLUG:KEY=VALUE",
                   help="a provider's own parameter, e.g. google-vertex:negativePrompt=zoom, pan")
    v.add_argument("--past-cap", action="store_true", help=f"allow more than {MAX_ATTEMPTS} clips under one --name")
    v.set_defaults(func=cmd_video)

    sub.add_parser("credit", help="what the key has spent and has left").set_defaults(func=cmd_credit)

    l = sub.add_parser("list", help="recent frames")
    l.add_argument("-n", "--count", type=int, default=10)
    l.add_argument("--filter")
    l.set_defaults(func=cmd_list)

    s = sub.add_parser("show", help="full record for one frame id, or a name's newest")
    s.add_argument("ref")
    s.set_defaults(func=cmd_show)

    args = ap.parse_args()
    os.makedirs(FRAMES_DIR, exist_ok=True)
    args.func(args)


if __name__ == "__main__":
    main()
