#!/usr/bin/env python3
"""Remote image generation / editing CLI through OpenRouter's image-output models.

Stdlib only. Borrows localgen's index, naming, transparency key and mask compositing. Frames, their
records and every picture they were asked from go to .tmp/gen/; `qq sprites` cuts a frame and keeps
what is used, with its lineage, under raw_assets/.

  gen  "a red hatchback, top-down" --name sedan_red --transparent
  edit --image layout.png --image style.png -p "paint the outlines of image 1 in the style of image 2"
  models
  credit
  list -n 10 | show sedan_red-v2

The key is read from OPENROUTER_API_KEY or ~/.qq/openrouter.key (the one `qq pilot` uses); the
project holds none and it is never printed. Every call is billed to it.
"""

import argparse
import base64
import hashlib
import http.client
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request

sys.dont_write_bytecode = True
sys.path.insert(0,os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "localgen"))
import localgen as lg  # noqa: E402

# A frame is scratch: a sheet never goes into git, only the variants cut from it and kept
# (raw_assets/index.md). localgen's helpers read these module globals at call time.
GEN_DIR = os.path.join(lg.ROOT, ".tmp", "gen")
lg.OUT_DIR = os.path.join(GEN_DIR, "frames")
lg.INDEX = os.path.join(GEN_DIR, "index.jsonl")
# Every picture a frame was asked from, by content, as it was when sent: a layout redrawn under the same
# path afterwards must not rewrite what an older frame's record says it was given.
INPUTS_DIR = os.path.join(GEN_DIR, "inputs")

API ="https://openrouter.ai/api/v1"
KEY_FILES = ("~/.qq/openrouter.key", "~/.config/openrouter.key")
# The project's pick for sprite sheets, with DEFAULT_QUALITY and the Image API: the only model to draw every
# sheet of imgbench's train benchmark top-down and right, and at quality low half the price with no visible
# loss (2026-10-18; the owner's call to make it the default).
DEFAULT_MODEL = "openai/gpt-image-2.5-sunburst"
DEFAULT_QUALITY = "low"
# Gemini's image models take one of these instead of a pixel size; a --size or a reference's
# size is snapped to the nearest.
ASPECTS = ("1:1", "2:3", "3:2", "3:4", "4:3", "4:5", "5:4", "9:16", "16:9", "21:9")
MIME = {".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".webp": "image/webp"}
RETRIES = 3
# The owner's limit: one generation (one --name) gets this many remote frames, then it is reported back rather than
# tried again.
MAX_ATTEMPTS = 3


def read_key():
    key = os.environ.get("OPENROUTER_API_KEY", "").strip()
    if key:
        return key
    for candidate in KEY_FILES:
        path = os.path.expanduser(candidate)
        if os.path.isfile(path):
            with open(path) as f:
                key = f.read().strip()
            if key:
                return key
    sys.exit(f"no OpenRouter key: set OPENROUTER_API_KEY or put it in {KEY_FILES[0]}")


# Refused before any work was done, so a retry cannot bill twice. 520-524 are the upstream's own (Seedream).
RETRY_CODES = (408, 429, 500, 502, 503, 504, 520, 521, 522, 523, 524)
# Seconds the last successful request took, its own attempt only: retry sleeps are the service's, not the
# model's.
LAST_ATTEMPT_S = [0.0]


def call(method, path, payload=None, timeout=300):
    req = urllib.request.Request(
        API + path, None if payload is None else json.dumps(payload).encode(),
        {"Authorization": f"Bearer {read_key()}", "Content-Type": "application/json",
         "X-Title": "traffic-dotnet remotegen"}, method=method)
    # A catalogue GET is always retried; a frame's POST as --retries says (imgbench retries in later passes).
    retries = RETRIES if method == "POST" else max(RETRIES, 3)
    for attempt in range(retries + 1):
        t0 = time.time()
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                reply = json.loads(r.read())
            LAST_ATTEMPT_S[0] = time.time() - t0
            return reply
        except urllib.error.HTTPError as e:
            body = e.read().decode("utf-8", "replace")
            # 401/402/403 are the key's or the account's: report, never route around.
            if e.code in RETRY_CODES and attempt < retries:
                # A provider's 429 outlasts a few seconds; 5 s and 10 s lost every frame of a busy model.
                wait = (30 if e.code == 429 else 5) * (attempt + 1)
                print(f"OpenRouter {e.code}, retrying in {wait}s", file=sys.stderr)
                time.sleep(wait)
                continue
            try:
                msg = json.loads(body).get("error", {}).get("message") or body
            except ValueError:
                msg = body
            sys.exit(f"OpenRouter refused ({e.code}): {msg[:800]}")
        except urllib.error.URLError as e:
            # Not connected at all: nothing was sent, so nothing was billed.
            if not isinstance(e.reason, TimeoutError) and attempt < retries:
                time.sleep(5)
                continue
            if not isinstance(e.reason, TimeoutError):
                sys.exit(f"cannot reach OpenRouter: {e.reason}")
            sys.exit(f"OpenRouter dropped the reply (possibly billed): timed out after {timeout}s")
        except (http.client.HTTPException, OSError, ValueError) as e:
            # The request went out and the reply broke (IncompleteRead, reset, timeout, bad JSON). A GET is
            # retried; a POST is not, since the frame may already be billed.
            if method == "GET" and attempt < retries:
                time.sleep(5)
                continue
            sys.exit(f"OpenRouter dropped the reply (possibly billed): {type(e).__name__}: {e}"[:300])


def image_part(path):
    mime = MIME.get(os.path.splitext(path)[1].lower())
    if not mime:
        sys.exit(f"unsupported reference type: {path}")
    with open(path, "rb") as f:
        data = base64.b64encode(f.read()).decode("ascii")
    return {"type": "image_url", "image_url": {"url": f"data:{mime};base64,{data}"}}


def nearest_aspect(w, h):
    return min(ASPECTS, key=lambda a: abs(int(a.split(":")[0]) / int(a.split(":")[1]) - w / h))


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    return int.from_bytes(head[16:20], "big"), int.from_bytes(head[20:24], "big")


def image_models():
    """Every image-output model. The plain /models listing leaves out the image-only ones (FLUX,
    Seedream, gpt-image, ...); this filter is the only listing that has them."""
    return [m for m in call("GET", "/models?output_modalities=image", timeout=60).get("data", [])
            if not m["id"].startswith("openrouter/")]


def output_modalities(model):
    """An image-only model refuses a request that also asks for text (404 "No endpoints found")."""
    for m in image_models():
        if m["id"] == model:
            return (m.get("architecture") or {}).get("output_modalities") or ["image"]
    sys.exit(f"{model} is not an image-output model on OpenRouter; `models` lists them")


_IMAGE_API = {}


def image_api_catalogue():
    """{id: record} of the Image API: what each model takes and which settings it offers."""
    if not _IMAGE_API:
        _IMAGE_API.update({m["id"]: m for m in call("GET", "/images/models", timeout=60).get("data", [])})
    return _IMAGE_API


def image_api_model(model):
    if model not in image_api_catalogue():
        sys.exit(f"{model} is not on OpenRouter's Image API; `models` lists the image models")
    return _IMAGE_API[model]


RESOLUTIONS = ("512", "768", "1K", "1.5K", "2K", "4K")


def references_most(model):
    """How many pictures a model takes; None when the Image API does not list it."""
    m = image_api_catalogue().get(model)
    return None if m is None else (m["supported_parameters"].get("input_references") or {}).get("max", 0)


def supported_aspect(model, aspect):
    """The aspect asked for, or the nearest the model offers (Recraft has no 3:2)."""
    params = image_api_model(model)["supported_parameters"]
    ratios = [a for a in (params.get("aspect_ratio") or {}).get("values", []) if re.fullmatch(r"\d+:\d+", a)]
    if not aspect or not ratios or aspect in ratios:
        return aspect if ratios else None
    w, h = map(int, aspect.split(":"))
    return min(ratios, key=lambda a: abs(int(a.split(":")[0]) / int(a.split(":")[1]) - w / h))


def offered(params, name, asked):
    """`asked` for an enum parameter the model takes; "min" its first value past "auto" (the catalogue lists
    them smallest first); None — the model's own choice — for "auto" or when the model takes no such parameter."""
    values = [v for v in (params.get(name) or {}).get("values", []) if v != "auto"]
    if not asked or asked == "auto" or not values:
        return None
    if asked == "min":
        return values[0]
    if asked not in values:
        sys.exit(f"{name} {asked} is not one of {values}")
    return asked


def generate_images(args, prompt, refs):
    """The same frame through the Image API (/images), which every image model takes and some take only:
    gpt-image, FLUX.3, Krea, Qwen-Image. Resolution and quality are the model's own unless asked for."""
    most = references_most(args.model)
    if len(refs) > most:
        sys.exit(f"{args.model} takes at most {most} reference image(s), given {len(refs)}")
    payload = {"model": args.model, "prompt": prompt}
    if refs:
        payload["input_references"] = [image_part(p) for p in refs]
    args.aspect = supported_aspect(args.model, args.aspect)   # recorded as sent
    if args.aspect:
        payload["aspect_ratio"] = args.aspect
    params = image_api_model(args.model)["supported_parameters"]
    args.image_size = offered(params, "resolution", args.image_size)
    if args.image_size:
        payload["resolution"] = args.image_size
    args.quality = offered(params, "quality", args.quality)
    if args.quality:
        payload["quality"] = args.quality
    if args.seed is not None and "seed" in params:
        payload["seed"] = args.seed
    else:
        args.seed = None   # recorded as not sent: most image models take none
    settings = {k: v for k, v in payload.items() if k not in ("model", "prompt", "input_references")}
    args.sent = settings
    print(f"settings {json.dumps(settings)}", file=sys.stderr)
    return billed_requests(args, "/images", payload, images_api_frames)


def ext_of(mime):
    # A vector model answers in SVG: saved as what it is, never under a raster name.
    return {"image/jpeg": "jpg", "image/svg+xml": "svg"}.get(mime, mime.split("/")[-1] or "png")


def images_api_frames(reply):
    if not reply.get("data"):
        sys.exit("no image in the reply: " + json.dumps(reply)[:600])
    out = []
    for img in reply["data"]:
        if img.get("b64_json"):
            blob = base64.b64decode(img["b64_json"])
        else:
            with urllib.request.urlopen(img["url"], timeout=120) as r:
                blob = r.read()
        out.append((blob, ext_of(img.get("media_type") or "image/png")))
    return out


def chat_frames(reply):
    message = (reply.get("choices") or [{}])[0].get("message") or {}
    images = message.get("images") or []
    if not images:
        sys.exit("no image in the reply; the model said: " + str(message.get("content"))[:600])
    out = []
    for img in images:
        head, data = img["image_url"]["url"].split(",", 1)
        out.append((base64.b64decode(data), ext_of(head[5:].split(";")[0])))
    return out


def billed_requests(args, path, payload, frames_of):
    """One billed request per frame — the API has no batch. Returns [(bytes, ext)], total cost in USD.
    The status line is printed even when a request fails, so a frame already billed is never unaccounted;
    its time is the successful attempts' alone, and a cost the reply did not state is `$?`."""
    out, cost, known, seconds = [], 0.0, True, 0.0
    try:
        for _ in range(args.count):
            reply = call("POST", path, payload)
            seconds += LAST_ATTEMPT_S[0]
            stated = (reply.get("usage") or {}).get("cost")
            known = known and stated is not None
            # The token breakdown says where a token-priced model's cost went: the pictures in or the one out.
            print(f"usage {json.dumps(reply.get('usage') or {})}", file=sys.stderr)
            cost += float(stated or 0)
            out += frames_of(reply)
    finally:
        print(f"{len(out)} image(s) in {seconds:.1f}s, " + (f"${cost:.4f}" if known else "$?"), file=sys.stderr)
    return out, cost


def generate(args, prompt, refs):
    if args.api == "images":
        return generate_images(args, prompt, refs)
    content = [{"type": "text", "text": prompt}] + [image_part(p) for p in refs]
    payload = {"model": args.model, "modalities": output_modalities(args.model),
               "messages": [{"role": "user", "content": content}],
               "usage": {"include": True}}
    config = {k: v for k, v in (("aspect_ratio", args.aspect), ("image_size", args.image_size)) if v}
    if config:
        payload["image_config"] = config
    if args.seed is not None:
        payload["seed"] = args.seed
    args.sent = {"api": "chat", **config, **({"seed": args.seed} if args.seed is not None else {})}
    return billed_requests(args, "/chat/completions", payload, chat_frames)


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
             if os.path.dirname(os.path.abspath(path)) == os.path.abspath(lg.OUT_DIR) else None)
    return {"role": role, "sha": sha, "ext": ext, "frame": frame, "was": os.path.relpath(path, lg.ROOT)}


def record(frames, name, args, op, prompt, inputs, cost):
    written = []
    for blob, ext in frames:
        if args.transparent and ext != "png":
            print(f"warning: {args.model} returned {ext}, not keyed", file=sys.stderr)
        path, ver = lg.next_path(name, ext)
        if args.transparent and ext == "png":
            w, h, px = lg.chroma_key(blob, keep_shadow=args.keep_shadow)
            lg.png_write(path, w, h, px)
        else:
            with open(path, "wb") as f:
                f.write(blob)
        size = png_size(path)
        rel = os.path.relpath(path, lg.ROOT)
        lg.append_index({
            "id": f"{name}-v{ver}",
            "ts": time.strftime("%Y-%m-%dT%H:%M:%S"),
            "op": op,
            "name": name,
            "file": rel,
            "model": args.model,
            "backend": "openrouter",
            "size": f"{size[0]}x{size[1]}" if size else None,
            "settings": getattr(args, "sent", {}),
            "costUsd": round(cost / len(frames), 5),
            "prompt": prompt,
            "inputs": inputs,
        })
        written.append(rel)
    for rel in written:
        print(rel)


def check_attempts(args, name):
    """Refuses a frame past MAX_ATTEMPTS under one name, before anything is billed. Counts the frames the index
    holds; a refusal that returned no image is not in it and is counted by whoever ran it."""
    if args.past_cap:
        return
    made = sum(1 for r in lg.read_index() if r.get("name") == name and r.get("backend") == "openrouter")
    if made + args.count > MAX_ATTEMPTS:
        sys.exit(f"{name} already has {made} remote frame(s) and {MAX_ATTEMPTS} is the limit for one generation: "
                 "report the frames to the owner instead of trying again (--past-cap only on the owner's word)")


def cmd_gen(args):
    name = args.name or lg.slug(args.prompt)
    check_attempts(args, name)
    prompt = args.prompt + (lg.BG_CLAUSE if args.transparent else "")
    frames, cost = generate(args, prompt, [])
    record(frames, name, args, "gen", prompt, [], cost)


def cmd_edit(args):
    paths = [lg.resolve_image(r) for r in args.image]
    base = args.name or lg.re.sub(r"-v\d+$", "", os.path.splitext(os.path.basename(paths[0]))[0])
    check_attempts(args, base)
    inputs = [snapshot(p, f"image {n}") for n, p in enumerate(paths, 1)]
    if args.mask:
        inputs.append(snapshot(lg.resolve_image(args.mask), "mask"))
    if args.aspect is None:
        size = png_size(paths[0])
        if size:
            args.aspect = nearest_aspect(*size)
    prompt = args.prompt + (lg.BG_CLAUSE if args.transparent else "")
    frames, cost = generate(args, prompt, paths)
    if args.mask:
        with open(paths[0], "rb") as f:
            original = f.read()
        frames = [(lg.composite(original, b, lg.resolve_image(args.mask), args.feather), "png")
                  for b, _ in frames]
    record(frames, base, args, "edit", prompt, inputs, cost)


def cmd_models(args):
    rows = []
    for m in image_models():
        arch, p = m.get("architecture") or {}, m.get("pricing") or {}
        rows.append((m["id"], float(p.get("prompt") or 0) * 1e6, float(p.get("image_output") or 0) * 1e6,
                     "+".join(sorted(arch.get("input_modalities", []))),
                     "+".join(sorted(arch.get("output_modalities", [])))))
    if args.json:
        print(json.dumps([dict(zip(("id", "inUsdPerMtok", "imageOutUsdPerMtok", "inputs", "outputs"), r))
                          for r in rows], indent=1))
        return
    print(f"{'model':<44} {'in $/Mtok':>10} {'img out $/Mtok':>15}  inputs -> outputs")
    for mid, pin, pout, inputs, outputs in sorted(rows, key=lambda r: r[2]):
        print(f"{mid:<44} {pin:>10.2f} {pout:>15.2f}  {inputs} -> {outputs}")


def cmd_credit(args):
    d = call("GET", "/key", timeout=30).get("data", {})
    limit, left = d.get("limit"), d.get("limit_remaining")
    print(f"used ${d.get('usage', 0):.4f}"
          + (f" of ${limit:.2f}, ${left:.4f} left" if limit is not None else ", no limit"))


def flags(p):
    p.add_argument("--model", default=DEFAULT_MODEL, help=f"OpenRouter model id (default {DEFAULT_MODEL}); "
                                                          "`models` lists the image-output ones")
    p.add_argument("--name", help="base filename (default: slug of prompt / first image's stem)")
    p.add_argument("--aspect", choices=ASPECTS, default=None,
                   help="aspect ratio (default: the model's own for gen, the first image's nearest for edit)")
    p.add_argument("--image-size", choices=RESOLUTIONS + ("min",), default=None,
                   help="output resolution tier (default the model's own, ~1 MP); chat takes 1K/2K/4K, Gemini only; "
                        "min: the model's smallest, on the Image API, and nothing sent to a model that takes none")
    p.add_argument("--quality", default=DEFAULT_QUALITY,
                   help=f"Image API quality tier (gpt-image: low|medium|high|...; default {DEFAULT_QUALITY}), min for "
                        "the model's lowest, auto for the model's own choice; not sent to a model that takes none")
    p.add_argument("--api", choices=("chat", "images"), default="images",
                   help="images: the Image API (default), which every image model takes and gpt-image, FLUX.3, "
                        "Krea and Qwen-Image take only; chat: /chat/completions")
    p.add_argument("--retries", type=int, default=RETRIES,
                   help=f"retries of a frame the service refused (429, 5xx), with backoff (default {RETRIES}); 0 "
                        "fails at once, for a caller that retries later itself")
    p.add_argument("-n", "--count", type=int, default=1, help="frames; one billed request each")
    p.add_argument("--past-cap", action="store_true",
                   help=f"allow more than {MAX_ATTEMPTS} frames under one --name: only when the owner asked for more, "
                        "or for imgbench, whose sweep bounds its own frames")
    p.add_argument("--transparent", action="store_true", help="render on a flat field and key it out to alpha")
    p.add_argument("--keep-shadow", action="store_true",
                   help="with --transparent, keep the cast shadow instead of keying it too")
    p.add_argument("--seed", type=int, default=None, help="passed through; not every model honours it")


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

    m = sub.add_parser("models", help="image-output models and their prices")
    m.add_argument("--json", action="store_true")
    m.set_defaults(func=cmd_models)
    sub.add_parser("credit", help="what the key has spent and has left").set_defaults(func=cmd_credit)

    l = sub.add_parser("list", help="recent generations, local and remote")
    l.add_argument("-n", "--count", type=int, default=10)
    l.add_argument("--filter")
    l.set_defaults(func=lg.cmd_list)

    s = sub.add_parser("show", help="full record for one id")
    s.add_argument("ref")
    s.set_defaults(func=lg.cmd_show)

    args = ap.parse_args()
    global RETRIES
    RETRIES = getattr(args, "retries", RETRIES)
    os.makedirs(lg.OUT_DIR, exist_ok=True)
    args.func(args)


if __name__ == "__main__":
    main()
