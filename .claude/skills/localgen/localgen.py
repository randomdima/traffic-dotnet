#!/usr/bin/env python3
"""Local image generation / img2img CLI, driving ComfyUI in the `local-ai` toolbox.

Stdlib only, self-contained. Writes to raw_assets/generated/ and appends one record
per image to raw_assets/generated/index.jsonl.

  gen  "a red hatchback, top-down"  --name sedan_red --transparent
  edit --image sedan_red -p "make it a taxi" --strength 0.6
  list -n 10
  show sedan_red-v2
  status | up

Model stack (Z-Image Turbo + pixel-art LoRA) lives in ~/local-ai/ComfyUI/models.
"""

import argparse
import json
import os
import random
import re
import shutil
import struct
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
import zlib


def project_root():
    d = os.path.dirname(os.path.abspath(__file__))
    while d != os.path.dirname(d):
        if os.path.isfile(os.path.join(d, "traffic-dotnet.csproj")):
            return d
        d = os.path.dirname(d)
    sys.exit("cannot find the project root (no traffic-dotnet.csproj above this script)")


ROOT = project_root()
OUT_DIR = os.path.join(ROOT, "raw_assets", "generated")
INDEX = os.path.join(OUT_DIR, "index.jsonl")

HOST = os.environ.get("LOCALGEN_HOST", "http://127.0.0.1:8188")
CONTAINER = os.environ.get("LOCALGEN_CONTAINER", "local-ai")
COMFY_DIR = os.path.expanduser("~/local-ai/ComfyUI")

UNET = "z_image_turbo_int8_convrot.safetensors"
CLIP = "qwen_3_4b_fp8_mixed.safetensors"
VAE = "z_image_ae.safetensors"
LORA = "pixel_art_style_z_image_turbo.safetensors"

# The model will not hand back the exact colour it was asked for — a prompt for #FF00FF
# comes back as a muted rose — so the key colour is measured from the border, never assumed.
KEY_HARD = 40                    # channel distance from it: fully transparent below this
KEY_SOFT = 110                   # ... fully opaque above this, alpha ramped between


# ------------------------------------------------------------------ index / naming

def read_index():
    if not os.path.exists(INDEX):
        return []
    out = []
    with open(INDEX) as f:
        for line in f:
            line = line.strip()
            if line:
                try:
                    out.append(json.loads(line))
                except json.JSONDecodeError:
                    pass
    return out


def append_index(rec):
    os.makedirs(OUT_DIR, exist_ok=True)
    with open(INDEX, "a") as f:
        f.write(json.dumps(rec) + "\n")


def slug(text, limit=40):
    s = re.sub(r"[^a-z0-9]+", "_", text.lower()).strip("_")
    return s[:limit].rstrip("_") or "image"


def next_path(name, ext):
    """raw_assets/generated/<name>-v<N>.<ext>, N = first free version."""
    os.makedirs(OUT_DIR, exist_ok=True)
    n = 1
    while os.path.exists(os.path.join(OUT_DIR, f"{name}-v{n}.{ext}")):
        n += 1
    return os.path.join(OUT_DIR, f"{name}-v{n}.{ext}"), n


def resolve_image(ref):
    """A path, or a name/id from the index (newest match wins)."""
    for cand in (ref, os.path.join(ROOT, ref), os.path.join(OUT_DIR, ref)):
        if os.path.isfile(cand):
            return os.path.abspath(cand)
    for rec in reversed(read_index()):
        if ref in (rec.get("id"), rec.get("name")) or ref == os.path.basename(rec.get("file", "")):
            p = os.path.join(ROOT, rec["file"])
            if os.path.isfile(p):
                return p
    # bare name without version: newest file on disk with that stem
    matches = sorted(
        p for p in (os.listdir(OUT_DIR) if os.path.isdir(OUT_DIR) else [])
        if p.startswith(ref + "-v")
    )
    if matches:
        return os.path.join(OUT_DIR, matches[-1])
    sys.exit(f"cannot resolve image: {ref}")


# ------------------------------------------------------------------ ComfyUI API

def api_get(path, timeout=30, raw=False):
    with urllib.request.urlopen(HOST + path, timeout=timeout) as r:
        return r.read() if raw else json.loads(r.read())


def api_post(path, payload, timeout=60):
    req = urllib.request.Request(HOST + path, json.dumps(payload).encode(),
                                 {"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read())
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", "replace")
        try:
            err = json.loads(body)
            msg = err.get("error", {}).get("message") or body
            node = err.get("node_errors")
            if node:
                msg += " " + json.dumps(node)[:800]
        except Exception:
            msg = body[:800]
        sys.exit(f"ComfyUI rejected the workflow ({e.code}): {msg}")


def alive():
    try:
        api_get("/system_stats", timeout=3)
        return True
    except Exception:
        return False


def stop_server():
    subprocess.run(["toolbox", "run", "-c", CONTAINER, "bash", "-lc",
                    "pkill -f 'python main.py' || true"], check=False)
    for _ in range(15):
        if not alive():
            return
        time.sleep(1)


def start_server(wait=180):
    """Launch run.sh inside the toolbox container and wait for the port."""
    if alive():
        return
    if not shutil.which("toolbox"):
        sys.exit(f"ComfyUI is not answering on {HOST} and `toolbox` is not on PATH")
    print(f"starting ComfyUI in the {CONTAINER} container ...", file=sys.stderr)
    subprocess.run(
        ["toolbox", "run", "-c", CONTAINER, "bash", "-lc",
         f"cd {COMFY_DIR} && nohup ./run.sh > /tmp/comfy.log 2>&1 & sleep 2"],
        check=False,
    )
    t0 = time.time()
    while time.time() - t0 < wait:
        if alive():
            print(f"ComfyUI up after {time.time() - t0:.0f}s", file=sys.stderr)
            return
        time.sleep(2)
    sys.exit(f"ComfyUI did not come up within {wait}s "
             f"(log: toolbox run -c {CONTAINER} tail -40 /tmp/comfy.log)")


def upload_image(path):
    """POST /upload/image so LoadImage can read it. Returns the name to hand LoadImage."""
    boundary = "----localgen" + uuid.uuid4().hex
    with open(path, "rb") as f:
        blob = f.read()
    buf = bytearray()
    buf += f"--{boundary}\r\n".encode()
    buf += (f'Content-Disposition: form-data; name="image"; '
            f'filename="{os.path.basename(path)}"\r\n').encode()
    buf += b"Content-Type: image/png\r\n\r\n" + blob + b"\r\n"
    for name, value in (("overwrite", "true"), ("type", "input")):
        buf += f"--{boundary}\r\n".encode()
        buf += f'Content-Disposition: form-data; name="{name}"\r\n\r\n'.encode()
        buf += f"{value}\r\n".encode()
    buf += f"--{boundary}--\r\n".encode()
    req = urllib.request.Request(HOST + "/upload/image", bytes(buf),
                                 {"Content-Type": f"multipart/form-data; boundary={boundary}"})
    with urllib.request.urlopen(req, timeout=120) as r:
        resp = json.loads(r.read())
    sub = resp.get("subfolder") or ""
    return f"{sub}/{resp['name']}" if sub else resp["name"]


def is_flat(blob):
    """True for the featureless grey or black frame a degraded server returns.

    Two measures, both of which a real render clears comfortably: the 1st-to-99th
    percentile luminance spread (100+ for a sprite on a flat field, under 10 for a
    collapsed latent) and the number of distinct 16-level colour buckets (200+ against
    fewer than 10). Requiring both keeps a small, low-contrast sprite out of it.
    """
    try:
        w, h, px = png_read(blob)
    except ValueError:
        return False
    step = max(1, (w * h) // 20000) * 4
    sample = range(0, len(px) - 3, step)
    lum = sorted((px[i] * 299 + px[i + 1] * 587 + px[i + 2] * 114) // 1000
                 for i in sample)
    buckets = {(px[i] >> 4, px[i + 1] >> 4, px[i + 2] >> 4) for i in sample}
    n = len(lum)
    return lum[int(n * 0.99)] - lum[int(n * 0.01)] < 25 and len(buckets) < 20


def run_workflow(wf, timeout=1800, allow_restart=True):
    """Queue a graph, wait for it, return the raw bytes of every image it produced."""
    blobs = queue_workflow(wf, timeout)
    if allow_restart and all(is_flat(b) for b in blobs):
        # The VAE encode path rots after a few dozen prompts on this ROCm/int8 stack and
        # every image comes back blank until the server is restarted. Restart and retry
        # once; a second blank frame is the prompt's fault, not the server's.
        print("blank frame — restarting ComfyUI and retrying once", file=sys.stderr)
        stop_server()
        start_server()
        blobs = queue_workflow(wf, timeout)
        if all(is_flat(b) for b in blobs):
            sys.exit("still blank after a restart — check the prompt, size and strength")
    return blobs


def queue_workflow(wf, timeout=1800):
    pid = api_post("/prompt", {"prompt": wf, "client_id": uuid.uuid4().hex})["prompt_id"]
    t0 = time.time()
    while True:
        hist = api_get(f"/history/{pid}")
        if pid in hist:
            entry = hist[pid]
            status = entry.get("status", {})
            if status.get("status_str") == "error":
                for m in status.get("messages", []):
                    if m[0] in ("execution_error", "execution_interrupted"):
                        sys.exit("ComfyUI error: " + json.dumps(m[1])[:1500])
                sys.exit("ComfyUI error (no message)")
            out = []
            for node in entry.get("outputs", {}).values():
                for img in node.get("images", []):
                    q = (f"/view?filename={urllib.parse.quote(img['filename'])}"
                         f"&subfolder={urllib.parse.quote(img.get('subfolder', ''))}"
                         f"&type={img.get('type', 'output')}")
                    out.append(api_get(q, timeout=120, raw=True))
            if not out:
                sys.exit("workflow finished but produced no image")
            print(f"{len(out)} image(s) in {time.time() - t0:.0f}s", file=sys.stderr)
            return out
        if time.time() - t0 > timeout:
            sys.exit(f"timed out after {timeout}s waiting for ComfyUI")
        time.sleep(2)


# -------------------------------------------------------------------- workflow

def build(args, prompt, latent_node, extra=None):
    """Common Z-Image Turbo graph. `latent_node` is the [node_id, slot] feeding KSampler."""
    wf = {
        "1": {"class_type": "UNETLoader",
              "inputs": {"unet_name": args.unet, "weight_dtype": "default"}},
        "2": {"class_type": "CLIPLoader",
              "inputs": {"clip_name": args.clip, "type": "lumina2", "device": "default"}},
        "3": {"class_type": "VAELoader", "inputs": {"vae_name": args.vae}},
        "5": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["2", 0], "text": prompt}},
        "8": {"class_type": "ModelSamplingAuraFlow",
              "inputs": {"model": ["1", 0], "shift": args.shift}},
        "9": {"class_type": "KSampler",
              "inputs": {"model": ["8", 0], "positive": ["5", 0], "negative": ["6", 0],
                         "latent_image": latent_node, "seed": args.seed, "steps": args.steps,
                         "cfg": args.cfg, "sampler_name": args.sampler, "scheduler": "simple",
                         "denoise": args.denoise}},
        "10": {"class_type": "VAEDecode", "inputs": {"samples": ["9", 0], "vae": ["3", 0]}},
        "11": {"class_type": "PreviewImage", "inputs": {"images": ["10", 0]}},
    }
    if args.lora_strength > 0:
        wf["4"] = {"class_type": "LoraLoaderModelOnly",
                   "inputs": {"model": ["1", 0], "lora_name": args.lora,
                              "strength_model": args.lora_strength}}
        wf["8"]["inputs"]["model"] = ["4", 0]
    # Turbo runs at cfg 1.0, where the negative branch is ignored; only encode text for it
    # when the caller has actually raised cfg.
    if args.negative and args.cfg > 1.0:
        wf["6"] = {"class_type": "CLIPTextEncode",
                   "inputs": {"clip": ["2", 0], "text": args.negative}}
    else:
        wf["6"] = {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["5", 0]}}
    wf.update(extra or {})
    return wf


# ------------------------------------------------------------------ PNG + key

def png_read(blob):
    """Decode an 8-bit non-interlaced RGB/RGBA PNG to (w, h, rgba bytearray)."""
    if blob[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("not a PNG")
    pos, idat, w = 8, bytearray(), None
    while pos < len(blob):
        (length,) = struct.unpack(">I", blob[pos:pos + 4])
        ctype = blob[pos + 4:pos + 8]
        data = blob[pos + 8:pos + 8 + length]
        pos += 12 + length
        if ctype == b"IHDR":
            w, h, depth, color, _, _, interlace = struct.unpack(">IIBBBBB", data)
            if depth != 8 or color not in (2, 6) or interlace:
                raise ValueError(f"unsupported PNG (depth {depth}, colour {color})")
            bpp = 3 if color == 2 else 4
        elif ctype == b"IDAT":
            idat += data
        elif ctype == b"IEND":
            break
    if w is None:
        raise ValueError("PNG has no IHDR")
    raw = zlib.decompress(bytes(idat))

    stride = w * bpp
    rows, prev, pos = [], bytearray(stride), 0
    for _ in range(h):
        ft = raw[pos]
        line = bytearray(raw[pos + 1:pos + 1 + stride])
        pos += 1 + stride
        if ft == 1:
            for i in range(bpp, stride):
                line[i] = (line[i] + line[i - bpp]) & 0xFF
        elif ft == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif ft == 3:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                line[i] = (line[i] + ((a + prev[i]) >> 1)) & 0xFF
        elif ft == 4:
            for i in range(stride):
                a = line[i - bpp] if i >= bpp else 0
                c = prev[i - bpp] if i >= bpp else 0
                b = prev[i]
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                pr = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 0xFF
        elif ft:
            raise ValueError(f"unknown PNG filter {ft}")
        rows.append(line)
        prev = line

    if bpp == 4:
        return w, h, bytearray(b"".join(rows))
    rgba = bytearray(w * h * 4)
    for y, line in enumerate(rows):
        o = y * w * 4
        for x in range(w):
            rgba[o + x * 4:o + x * 4 + 3] = line[x * 3:x * 3 + 3]
            rgba[o + x * 4 + 3] = 255
    return w, h, rgba


def png_bytes(w, h, rgba):
    """Encode RGBA as an 8-bit PNG, every row unfiltered."""
    raw = bytearray()
    stride = w * 4
    for y in range(h):
        raw.append(0)
        raw += rgba[y * stride:(y + 1) * stride]

    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF))

    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(bytes(raw), 6))
            + chunk(b"IEND", b""))


def png_write(path, w, h, rgba):
    with open(path, "wb") as f:
        f.write(png_bytes(w, h, rgba))


def border_colour(px, w, h):
    """Modal colour of the one-pixel frame, in 16-level buckets, returned as its mean."""
    counts, sums = {}, {}

    def sample(x, y):
        i = (y * w + x) * 4
        r, g, b = px[i], px[i + 1], px[i + 2]
        k = (r >> 4, g >> 4, b >> 4)
        counts[k] = counts.get(k, 0) + 1
        s = sums.setdefault(k, [0, 0, 0])
        s[0] += r; s[1] += g; s[2] += b

    for x in range(w):
        sample(x, 0); sample(x, h - 1)
    for y in range(h):
        sample(0, y); sample(w - 1, y)
    k = max(counts, key=counts.get)
    n = counts[k]
    return tuple(v // n for v in sums[k]), n / (2 * (w + h))


def chroma_key(blob, hard=KEY_HARD, soft=KEY_SOFT, keep_shadow=False):
    """Cut the background to alpha: measure the border colour, flood in from the edges.

    Flooding rather than keying every matching pixel is what keeps a car's own body
    colour when it happens to sit near the background's.
    """
    w, h, px = png_read(blob)
    (kr, kg, kb), share = border_colour(px, w, h)
    if share < 0.5:
        print(f"warning: border is not one flat colour ({share:.0%} of it matched "
              f"rgb({kr},{kg},{kb})) — keying may be partial", file=sys.stderr)

    def dist(i):
        return max(abs(px[i] - kr), abs(px[i + 1] - kg), abs(px[i + 2] - kb))

    ksum = max(1, kr + kg + kb)

    def shadow(i):
        """A cast shadow is the background colour scaled down — same hue, less light.

        The model draws one however firmly the prompt forbids it, and it is background,
        not sprite. A dark outline is not caught: its hue is nowhere near the key's.
        """
        k = (px[i] + px[i + 1] + px[i + 2]) / ksum
        if not 0.35 <= k <= 1.0:
            return False
        return max(abs(px[i] - kr * k), abs(px[i + 1] - kg * k),
                   abs(px[i + 2] - kb * k)) <= hard

    seen = bytearray(w * h)
    stack = []
    for x in range(w):
        stack.append(x); stack.append((h - 1) * w + x)
    for y in range(h):
        stack.append(y * w); stack.append(y * w + w - 1)
    span = max(1, soft - hard)
    while stack:
        p = stack.pop()
        if seen[p]:
            continue
        seen[p] = 1
        i = p * 4
        d = dist(i)
        if d <= hard or (keep_shadow is False and shadow(i)):
            # Clear the colour as well as the alpha: a fully transparent pixel that keeps
            # the background's rose bleeds it back out under bilinear filtering.
            px[i] = px[i + 1] = px[i + 2] = px[i + 3] = 0
        elif d >= soft:
            continue
        else:
            # A partly-matching pixel is an anti-aliased edge: fade it, pull its
            # background tint out, and stop — the sprite proper lies beyond it.
            px[i + 3] = int(255 * (d - hard) / span)
            r, g, b = px[i], px[i + 1], px[i + 2]
            px[i] = max(0, min(255, r - (kr - kg) // 3))
            px[i + 2] = max(0, min(255, b - (kb - kg) // 3))
            continue
        x, y = p % w, p // w
        if x:
            stack.append(p - 1)
        if x < w - 1:
            stack.append(p + 1)
        if y:
            stack.append(p - w)
        if y < h - 1:
            stack.append(p + w)
    return w, h, px


def blur_weights(wt, w, h, radius):
    """Two box passes over a 0..255 weight plane — enough to hide a hard mask edge."""
    for _ in range(2):
        for y in range(h):
            row = wt[y * w:(y + 1) * w]
            acc, out = 0, bytearray(w)
            for x in range(w + radius):
                if x < w:
                    acc += row[x]
                if x - 2 * radius - 1 >= 0:
                    acc -= row[x - 2 * radius - 1]
                if x >= radius:
                    out[x - radius] = acc // min(2 * radius + 1, w)
            wt[y * w:(y + 1) * w] = out
        col = bytearray(h)
        for x in range(w):
            for y in range(h):
                col[y] = wt[y * w + x]
            acc = 0
            for y in range(h + radius):
                if y < h:
                    acc += col[y]
                if y - 2 * radius - 1 >= 0:
                    acc -= col[y - 2 * radius - 1]
                if y >= radius:
                    wt[(y - radius) * w + x] = acc // min(2 * radius + 1, h)
    return wt


def composite(original_blob, new_blob, mask_path, feather):
    """Keep the new pixels only where the mask is transparent; everything else is the
    original, byte for byte. ComfyUI's own latent noise mask returns a blank image on
    this model stack, so the mask is honoured here instead."""
    ow, oh, orig = png_read(original_blob)
    nw, nh, new = png_read(new_blob)
    with open(mask_path, "rb") as f:
        mw, mh, mask = png_read(f.read())
    if (ow, oh) != (nw, nh) or (ow, oh) != (mw, mh):
        sys.exit(f"mask/image size mismatch: image {ow}x{oh}, output {nw}x{nh}, "
                 f"mask {mw}x{mh}")
    wt = bytearray(255 - mask[i] for i in range(3, len(mask), 4))
    if feather:
        wt = blur_weights(wt, ow, oh, feather)
    for p, a in enumerate(wt):
        if not a:
            continue
        i = p * 4
        if a == 255:
            orig[i:i + 4] = new[i:i + 4]
        else:
            for c in range(4):
                orig[i + c] = (orig[i + c] * (255 - a) + new[i + c] * a) // 255
    return png_bytes(ow, oh, orig)


# -------------------------------------------------------------------- recording

def record(blobs, name, args, op, prompt, parents):
    written = []
    for blob in blobs:
        path, ver = next_path(name, "png")
        w, h = struct.unpack(">II", blob[16:24])
        if args.transparent:
            w, h, px = chroma_key(blob, keep_shadow=args.keep_shadow)
            png_write(path, w, h, px)
        else:
            with open(path, "wb") as f:
                f.write(blob)
        rel = os.path.relpath(path, ROOT)
        append_index({
            "id": f"{name}-v{ver}",
            "ts": time.strftime("%Y-%m-%dT%H:%M:%S"),
            "op": op,
            "name": name,
            "file": rel,
            "model": f"z-image-turbo{'+pixel-lora' if args.lora_strength > 0 else ''}",
            "backend": "local",
            "size": f"{w}x{h}",
            "seed": args.seed,
            "steps": args.steps,
            "prompt": prompt,
            "parents": parents,
        })
        written.append(rel)
    for rel in written:
        print(rel)


# --------------------------------------------------------------------- commands

BG_CLAUSE = (" The background is one single flat saturated magenta colour, edge to edge,"
             " completely uniform: no gradient, no vignette, no texture, and absolutely"
             " no cast shadow, glow or outline of the subject on it. Nothing in the"
             " subject is that colour.")


def cmd_gen(args):
    if not args.no_autostart:
        start_server()
    prompt = args.prompt + (BG_CLAUSE if args.transparent else "")
    w, h = parse_size(args.size)
    wf = build(args, prompt, ["7", 0], {
        "7": {"class_type": "EmptySD3LatentImage",
              "inputs": {"width": w, "height": h, "batch_size": args.count}},
    })
    blobs = run_workflow(wf)
    record(blobs, args.name or slug(args.prompt), args, "gen", prompt, [])


def cmd_edit(args):
    if not args.no_autostart:
        start_server()
    paths = [resolve_image(r) for r in args.image]
    if len(paths) > 1:
        sys.exit("local img2img takes exactly one --image")
    args.denoise = args.strength
    uploaded = upload_image(paths[0])
    prompt = args.prompt + (BG_CLAUSE if args.transparent else "")
    extra = {
        "20": {"class_type": "LoadImage", "inputs": {"image": uploaded}},
        "21": {"class_type": "VAEEncode", "inputs": {"pixels": ["20", 0], "vae": ["3", 0]}},
    }
    latent = ["21", 0]
    if args.count > 1:
        extra["24"] = {"class_type": "RepeatLatentBatch",
                       "inputs": {"samples": latent, "amount": args.count}}
        latent = ["24", 0]
    blobs = run_workflow(build(args, prompt, latent, extra))
    if args.mask:
        with open(paths[0], "rb") as f:
            original = f.read()
        blobs = [composite(original, b, resolve_image(args.mask), args.feather)
                 for b in blobs]
    base = args.name or re.sub(r"-v\d+$", "",
                               os.path.splitext(os.path.basename(paths[0]))[0])
    record(blobs, base, args, "edit", prompt, [os.path.relpath(p, ROOT) for p in paths])


def cmd_list(args):
    recs = read_index()
    if args.filter:
        recs = [r for r in recs if args.filter in r.get("id", "") + r.get("prompt", "")]
    for r in recs[-args.count:]:
        print(f"{r['id']:<28} {r['op']:<4} {r['file']}  {r['prompt'][:56]}")


def cmd_show(args):
    for r in read_index():
        if args.ref in (r.get("id"), r.get("name")):
            print(json.dumps(r, indent=2))
            return
    sys.exit(f"not found: {args.ref}")


def cmd_status(args):
    if not alive():
        print(f"ComfyUI: down ({HOST}) — `.claude/skills/localgen/localgen.py up`")
        return
    stats = api_get("/system_stats")
    dev = (stats.get("devices") or [{}])[0]
    free = dev.get("vram_free", 0) / 2**30
    total = dev.get("vram_total", 0) / 2**30
    print(f"ComfyUI: up ({HOST})  {dev.get('name', '?')}  VRAM {free:.1f}/{total:.1f} GiB free")
    for node, field in (("UNETLoader", "unet_name"), ("LoraLoaderModelOnly", "lora_name")):
        try:
            info = api_get(f"/object_info/{node}")
            opts = info[node]["input"]["required"][field][0]
            print(f"{field}: " + ", ".join(opts))
        except Exception:
            pass


def cmd_up(args):
    if args.restart:
        stop_server()
    start_server()
    cmd_status(args)


# ------------------------------------------------------------------------ glue

def parse_size(text):
    m = re.fullmatch(r"(\d+)x(\d+)", text.strip())
    if not m:
        sys.exit(f"bad --size {text!r}, want WxH e.g. 1024x1024")
    w, h = int(m.group(1)), int(m.group(2))
    if w % 16 or h % 16:
        sys.exit("--size must be a multiple of 16")
    return w, h


def common_flags(p):
    p.add_argument("--name", help="base filename (default: slug of prompt)")
    p.add_argument("--size", default="1024x1024", help="WxH, multiples of 16 (default 1024x1024)")
    p.add_argument("-n", "--count", type=int, default=1,
                   help="images in one batch — one model load, so far cheaper than N calls")
    p.add_argument("--transparent", action="store_true",
                   help="render on a flat field and key it out to alpha")
    p.add_argument("--keep-shadow", action="store_true",
                   help="with --transparent, keep the cast shadow instead of keying it too")
    p.add_argument("--seed", type=int, default=None)
    p.add_argument("--steps", type=int, default=8, help="turbo model, 8 is the tuned value")
    p.add_argument("--cfg", type=float, default=1.0)
    p.add_argument("--negative", default="", help="only used when --cfg > 1")
    p.add_argument("--sampler", default="res_multistep")
    p.add_argument("--shift", type=float, default=3.0)
    p.add_argument("--lora-strength", type=float, default=1.0,
                   help="pixel-art LoRA; 0 disables it")
    p.add_argument("--unet", default=UNET)
    p.add_argument("--clip", default=CLIP)
    p.add_argument("--vae", default=VAE)
    p.add_argument("--lora", default=LORA)
    p.add_argument("--no-autostart", action="store_true",
                   help="fail instead of starting ComfyUI in the toolbox")


def main():
    ap = argparse.ArgumentParser(prog="localgen.py", description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    g = sub.add_parser("gen", help="generate from a text prompt")
    g.add_argument("prompt")
    common_flags(g)
    g.set_defaults(func=cmd_gen, denoise=1.0)

    e = sub.add_parser("edit", help="img2img / refine an existing image")
    e.add_argument("--image", action="append", required=True, help="path, id or name")
    e.add_argument("-p", "--prompt", required=True)
    e.add_argument("--strength", type=float, default=0.6,
                   help="how much to redraw, 0..1 (default 0.6)")
    e.add_argument("--mask", help="PNG whose transparent area is what gets replaced")
    e.add_argument("--feather", type=int, default=2,
                   help="pixels of blend at the mask edge (default 2, 0 for a hard cut)")
    common_flags(e)
    e.set_defaults(func=cmd_edit)

    l = sub.add_parser("list", help="recent generations")
    l.add_argument("-n", "--count", type=int, default=10)
    l.add_argument("--filter")
    l.set_defaults(func=cmd_list)

    s = sub.add_parser("show", help="full record for one id")
    s.add_argument("ref")
    s.set_defaults(func=cmd_show)

    sub.add_parser("status", help="is ComfyUI up, which models are installed").set_defaults(
        func=cmd_status)
    u = sub.add_parser("up", help="start ComfyUI in the toolbox and wait for it")
    u.add_argument("--restart", action="store_true", help="kill a running server first")
    u.set_defaults(func=cmd_up)

    args = ap.parse_args()
    if getattr(args, "seed", None) is None and hasattr(args, "steps"):
        args.seed = random.randrange(2**31)
    os.makedirs(OUT_DIR, exist_ok=True)
    args.func(args)


if __name__ == "__main__":
    main()
