"""OpenRouter's Image API: the key, the catalogue, and billed frame requests. Stdlib only.

The client remotegen and imgbench share; neither one's settings, index or rules live here. Every error is a
`sys.exit` with one line, which imgbench catches per frame and classifies.
"""

import base64
import http.client
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request

API = "https://openrouter.ai/api/v1"
KEY_FILES = ("~/.qq/openrouter.key", "~/.config/openrouter.key")
ASPECTS = ("1:1", "2:3", "3:2", "3:4", "4:3", "4:5", "5:4", "9:16", "16:9", "21:9")
MIME = {".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".webp": "image/webp"}
RETRIES = 3
# Refused before any work was done, so a retry cannot bill twice. 520-524 are the upstream's own (Seedream).
RETRY_CODES = (408, 429, 500, 502, 503, 504, 520, 521, 522, 523, 524)


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


def call(method, path, payload=None, timeout=300, retries=RETRIES):
    """(reply, seconds): seconds is the successful attempt's alone — retry sleeps are the service's, not the
    model's. A GET is always retried; a POST `retries` times on a refusal, never on a dropped reply."""
    req = urllib.request.Request(
        API + path, None if payload is None else json.dumps(payload).encode(),
        {"Authorization": f"Bearer {read_key()}", "Content-Type": "application/json",
         "X-Title": "traffic-dotnet remotegen"}, method=method)
    retries = retries if method == "POST" else max(retries, RETRIES)
    for attempt in range(retries + 1):
        t0 = time.time()
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                reply = json.loads(r.read())
            return reply, time.time() - t0
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
            # The request went out and the reply broke (IncompleteRead, reset, timeout, bad JSON): a POST's
            # frame may already be billed.
            if method == "GET" and attempt < retries:
                time.sleep(5)
                continue
            sys.exit(f"OpenRouter dropped the reply (possibly billed): {type(e).__name__}: {e}"[:300])


def credit():
    """(used, limit, left) in USD; limit and left are None on a key without a limit."""
    d = call("GET", "/key", timeout=30)[0].get("data", {})
    return d.get("usage", 0), d.get("limit"), d.get("limit_remaining")


def image_models():
    """Every image-output model with its prices. The plain /models listing leaves out the image-only ones
    (FLUX, Seedream, gpt-image, ...); this filter is the only listing that has them."""
    return [m for m in call("GET", "/models?output_modalities=image", timeout=60)[0].get("data", [])
            if not m["id"].startswith("openrouter/")]


_IMAGE_API = {}


def image_api_catalogue():
    """{id: record} of the Image API: what each model takes and which settings it offers."""
    if not _IMAGE_API:
        _IMAGE_API.update({m["id"]: m for m in call("GET", "/images/models", timeout=60)[0].get("data", [])})
    return _IMAGE_API


def image_api_model(model):
    if model not in image_api_catalogue():
        sys.exit(f"{model} is not on OpenRouter's Image API; `models` lists the image models")
    return _IMAGE_API[model]


def references_most(model):
    """How many pictures a model takes; None when the Image API does not list it."""
    m = image_api_catalogue().get(model)
    return None if m is None else (m["supported_parameters"].get("input_references") or {}).get("max", 0)


def ratio(aspect):
    w, h = aspect.split(":")
    return int(w) / int(h)


def nearest_aspect(w, h):
    return min(ASPECTS, key=lambda a: abs(ratio(a) - w / h))


def supported_aspect(model, aspect):
    """The aspect asked for, or the nearest the model offers (Recraft has no 3:2)."""
    params = image_api_model(model)["supported_parameters"]
    ratios = [a for a in (params.get("aspect_ratio") or {}).get("values", []) if re.fullmatch(r"\d+:\d+", a)]
    if not aspect or not ratios or aspect in ratios:
        return aspect if ratios else None
    return min(ratios, key=lambda a: abs(ratio(a) - ratio(aspect)))


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


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    return int.from_bytes(head[16:20], "big"), int.from_bytes(head[20:24], "big")


def image_part(path):
    mime = MIME.get(os.path.splitext(path)[1].lower())
    if not mime:
        sys.exit(f"unsupported reference type: {path}")
    with open(path, "rb") as f:
        data = base64.b64encode(f.read()).decode("ascii")
    return {"type": "image_url", "image_url": {"url": f"data:{mime};base64,{data}"}}


def ext_of(mime):
    # A vector model answers in SVG: saved as what it is, never under a raster name.
    return {"image/jpeg": "jpg", "image/svg+xml": "svg"}.get(mime, mime.split("/")[-1] or "png")


def frames_of(reply):
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


def settings_for(model, refs, aspect=None, quality=None, resolution=None, seed=None):
    """What a request sends besides the model, prompt and pictures, as far as the model takes each. Refuses
    more pictures than the model takes."""
    params = image_api_model(model)["supported_parameters"]
    most = (params.get("input_references") or {}).get("max", 0)
    if len(refs) > most:
        sys.exit(f"{model} takes at most {most} reference image(s), given {len(refs)}")
    sent = {"aspect_ratio": supported_aspect(model, aspect), "resolution": offered(params, "resolution", resolution),
            "quality": offered(params, "quality", quality), "seed": seed if "seed" in params else None}
    return {k: v for k, v in sent.items() if v is not None}


def request(model, prompt, refs, settings, retries=RETRIES, echo=True):
    """One billed frame: ([(bytes, ext)], cost USD or None when the reply stated none, seconds, usage).
    With `echo`, prints `usage` and the status line to stderr even when the request fails, so a frame already
    billed is never unaccounted; a caller without it records them itself."""
    payload = {"model": model, "prompt": prompt, **settings}
    if refs:
        payload["input_references"] = [image_part(p) for p in refs]
    frames, cost, seconds, usage = [], None, 0.0, {}
    try:
        reply, seconds = call("POST", "/images", payload, retries=retries)
        usage = reply.get("usage") or {}
        cost = usage.get("cost")
        if echo:
            # The token breakdown says where a token-priced model's cost went: the pictures in or the one out.
            print(f"usage {json.dumps(usage)}", file=sys.stderr)
        frames = frames_of(reply)
    finally:
        if echo:
            print(f"{len(frames)} image(s) in {seconds:.1f}s, "
                  + (f"${float(cost):.4f}" if cost is not None else "$?"), file=sys.stderr)
    return frames, None if cost is None else float(cost), seconds, usage
