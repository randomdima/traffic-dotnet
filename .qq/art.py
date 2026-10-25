"""
Check every sheet against the texel density its own configuration declares, and bring back the ones
that are over it.

The art is cut on a grid: `ArtPixelsPerMetre` for the ground, the buildings and the props, and
`CarSpritePixelsPerMetre` for the cars — both of them figures on `ViewFigures`, read from there and
never repeated here. A sheet stored above its grid is texels the renderer cannot show: the camera
stops zooming at one art texel to one display pixel, so anything finer than the grid is minified at
every zoom the game has and is downloaded, decoded, packed and uploaded for nothing.

    qq art                        what each sheet's density is, and which are over their grid
    qq art --fix                  resize the ones that are over, in place
    qq art --fix --art=31.5       and move the art grid itself, for a coarser town
    qq art --fix --format=webp    and store them as WebP, renaming what names them
    qq art --fix --format=webp --error=3   holding it to a tighter bound, at more bytes

**The two grids move separately, and the cars' should not move at all.** CAR-12 asks that every
variant's tyres show past its own bodywork by a stated few millimetres, measured off the silhouette
in the picture — at 96 px/m one texel is 10 mm and that is a feature; at 48 it is 21 mm and there is
nowhere to draw it. The fleet is a sixth of the art and the thing most closely looked at. `--cars` is
here for completeness and `qq tests unit` is what says whether a town survived using it.

**A resize is done on premultiplied alpha**, so the transparent black outside a sprite cannot bleed
into its edge. **A WebP is written with a lossless alpha channel** whatever the colour quality is,
because a sprite's edge is its silhouette and a soft one shows — and **the quality is chosen per
sheet by measuring it**, not fixed across the town, because chroma subsampling costs a building's
flat wall nothing and a walker cut into sixty-four small frames a great deal. A sheet that cannot be
held to `--error` at any quality is stored lossless.

The metre size of a sheet is its own variant file's — `footprintM` for a body with a footprint,
`heightM` for a walker's sheet, and the ground's period for a surface that tiles. A sheet no file
gives a size for is left alone and said so.

**A name change is followed.** Converting rewrites the `"…"` that named the old file in the variant
JSON beside it, by text rather than by reparsing, so the hand-laid formatting of those files
survives. The handful of names spelled out in C# — the ground surfaces, the signal heads and the
lamp atlas in `ProjectPaths` — are not in a JSON and are listed on the way out to be changed by hand.
"""

from __future__ import annotations

import argparse
import io
import json
import re
from pathlib import Path

import _sim
from common import cli

#: Where the grid figures are stated, and the only place they are stated.
#: What a call may take: every sheet decoded, and re-encoded at three qualities when `--fix` is on.
TIMEOUT_S = 600

FIGURES = "src/core/config/EngineFigures.cs"

#: What overrides them at run time, if anything does.
OVERRIDES = "assets/shared/config/SimConfig.json"

#: The ground surfaces, in the order `ProjectPaths.GroundSurfaceFiles` returns them and the shore
#: (`ShoreSurfaceFile`), against the `ViewFigures` period each one repeats over.
SURFACES = {
    "grass": "GroundPeriodGrassM",
    "pavement": "GroundPeriodTarmacM",
    "sidewalk": "GroundPeriodPavementM",
    "bridge": "GroundPeriodDeckM",
    "water": "GroundPeriodWaterM",
    "shore": "GroundPeriodShoreM",
}

#: How many facing rows a walker's sheet is cut into. `PersonCatalog.FacingRows`.
FACING_ROWS = 8

#: What a sheet may be stored as. ImageSharp reads a file by its header rather than its name, so both
#: heads take either without being told which.
STORED = (".png", ".webp")

#: The WebP qualities to try, cheapest first, before giving up on lossy for a sheet altogether.
QUALITIES = (90, 95, 98)


def main():
    a = parse_args()
    base = _sim.root()
    figures = read_figures(base)
    if a.art:
        figures["ArtPixelsPerMetre"] = a.art
    if a.cars:
        figures["CarSpritePixelsPerMetre"] = a.cars

    sheets = [s for s in survey(base, figures) if s.declared]

    over = [s for s in sheets if s.ratio > 1.005]
    over.sort(key=lambda s: s.saving, reverse=True)

    print(f"{len(sheets)} sheets carry a declared metre size; {len(over)} are above their grid\n")
    print(f"{'density':>12s}  {'grid':>6s}  {'size':>20s}  {'file'}")
    for sheet in (over if over else sheets)[: a.limit]:
        print(f"{sheet.density:9.1f} px/m  {sheet.grid:6.0f}  {sheet.shape:>20s}  {sheet.name}")

    total = sum(s.bytes for s in sheets)
    saving = sum(s.saving for s in over)
    print(f"\n{total / 1048576:.1f} MB in sheets with a size; {saving / 1048576:.1f} MB of it is above the grid")

    if not a.fix:
        if over:
            print("\nrun with --fix to bring them back")
        return 0

    return rewrite(base, survey(base, figures), a)


class Sheet:
    """One art file, the metres it covers, and the density that makes it."""

    def __init__(self, path: Path, name: str, size: tuple[int, int], metres: float | None, grid: float):
        self.path, self.name, self.grid = path, name, grid
        self.width, self.height = size
        self.metres = metres
        self.bytes = path.stat().st_size

    @property
    def declared(self) -> bool:
        return self.metres is not None and self.metres > 0

    @property
    def density(self) -> float:
        """Art texels per metre, along whichever edge the variant file gave a size for."""
        return self.measured / self.metres

    @property
    def ratio(self) -> float:
        return self.density / self.grid

    @property
    def shape(self) -> str:
        return f"{self.width}x{self.height}"

    @property
    def saving(self) -> int:
        """What the file would drop by, if bytes fell with texels. Near enough to rank by."""
        return 0 if self.ratio <= 1 else int(self.bytes * (1 - 1 / self.ratio**2))

    #: The grid this kind of sheet's own size has to land on, whatever the density asks for.
    cells = 1

    @property
    def wanted(self) -> tuple[int, int]:
        """The size that puts this sheet on its grid, never enlarging one that is already under it."""
        if not self.declared or self.ratio <= 1.005:
            return self.width, self.height

        return (
            max(self.cells, round(self.width / self.ratio / self.cells) * self.cells),
            max(self.cells, round(self.height / self.ratio / self.cells) * self.cells),
        )


class Body(Sheet):
    """A sprite whose variant file gives a footprint: the width is the metres."""

    measured = property(lambda self: self.width)


class Walker(Sheet):
    """
    A walk sheet cut into facing rows: one frame's height is the person's height. **Its size is a
    whole number of cells in both directions** — the frame is picked by dividing the sheet, so a
    sheet that is not divides a walker's feet into the row below.
    """

    measured = property(lambda self: self.height / FACING_ROWS)
    cells = FACING_ROWS


class Down(Sheet):
    """The one frame a walker is drawn as once it is down."""

    measured = property(lambda self: self.height)


class Prop(Sheet):
    """
    A prop, whose `diameterM` is the disc the town reserves for it. **The longest side is the
    metres**: the picture is drawn to fit inside that disc, so a sheet wider than it is tall spends
    its width on the diameter and its height on whatever the aspect leaves.
    """

    measured = property(lambda self: max(self.width, self.height))


class Tile(Sheet):
    """A ground surface: the whole image is one period of the repeat."""

    measured = property(lambda self: self.width)


def read_figures(base: Path) -> dict[str, float]:
    """
    The grid and the periods, out of the file that states them, with whatever `SimConfig.json`
    overrides. Parsed rather than copied: a second copy of a figure is a figure that goes stale.
    """
    text = (base / FIGURES).read_text(encoding="utf-8")
    figures = {
        name: float(value)
        for name, value in re.findall(r"public float (\w+) \{ get; init; \} = ([\d.]+)f;", text)
    }

    override = base / OVERRIDES
    if override.is_file():
        view = json.loads(override.read_text(encoding="utf-8")).get("view", {})
        for name, value in view.items():
            key = name[0].upper() + name[1:]
            if key in figures:
                figures[key] = float(value)

    missing = [n for n in ("ArtPixelsPerMetre", "CarSpritePixelsPerMetre", *SURFACES.values()) if n not in figures]
    if missing:
        cli.fail(f"{FIGURES} no longer states {', '.join(missing)} — this tool reads them from there")

    return figures


def survey(base: Path, figures: dict[str, float]) -> list[Sheet]:
    """Every PNG under assets/, with the metres its own variant file says it covers."""
    art, car = figures["ArtPixelsPerMetre"], figures["CarSpritePixelsPerMetre"]
    sized: dict[Path, tuple[type[Sheet], float, float]] = {}

    surfaces = base / "assets/world/terrain/ground/surfaces"
    for stem, period in SURFACES.items():
        for stored in STORED:
            sized[surfaces / (stem + stored)] = (Tile, figures[period], art)

    for variant in sorted((base / "assets").rglob("*.json")):
        try:
            described = json.loads(variant.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, UnicodeDecodeError):
            continue
        if not isinstance(described, dict):
            continue

        # The cars and the kerbs are laid at the cars' grid (`sprites.FAMILIES`).
        grid = car if "/agents/car/" in variant.as_posix() or "/ground/kerbs/" in variant.as_posix() else art
        if "sprite" in described and "footprintM" in described:
            sized[variant.parent / described["sprite"]] = (Body, float(described["footprintM"][0]), grid)
        wreck = described.get("wreck")
        if isinstance(wreck, dict) and "sprite" in wreck and "footprintM" in described:
            # The wreck is the same car, drawn at the same footprint with a scale beside it.
            width = float(described["footprintM"][0]) * float(wreck.get("scale", [1, 1])[0])
            sized[variant.parent / wreck["sprite"]] = (Body, width, grid)
        if "sprite" in described and "diameterM" in described:
            # A prop is drawn to fit the disc the town reserves for it: its longest side is diameterM.
            sized[variant.parent / described["sprite"]] = (Prop, float(described["diameterM"]), grid)
        if "sheet" in described and "heightM" in described:
            sized[variant.parent / described["sheet"]] = (Walker, float(described["heightM"]), grid)
        if "down" in described and "heightM" in described:
            sized[variant.parent / described["down"]] = (Down, float(described["heightM"]), grid)

    sheets = []
    for path in sorted(p for p in (base / "assets").rglob("*") if p.suffix in STORED):
        kind, metres, grid = sized.get(path, (Body, None, art))
        sheets.append(kind(path, _sim.rel(base, path), dimensions(path), metres, grid))

    return sheets


def dimensions(path: Path) -> tuple[int, int]:
    """A PNG's IHDR, read without decoding; anything else through the imaging library."""
    import struct

    with path.open("rb") as file:
        head = file.read(24)
    if head[:8] == b"\x89PNG\r\n\x1a\n":
        return struct.unpack(">II", head[16:24])

    from PIL import Image

    with Image.open(path) as image:
        return image.width, image.height


def rewrite(base: Path, sheets: list[Sheet], a) -> int:
    """
    Every sheet down to its grid, down by `--scale` on top of that, and stored in `--format`. One
    pass: a sheet is decoded once and written once, and its old file is only unlinked after the new
    one is on disk.
    """
    before = after = 0
    done, orphans, how = 0, [], {}

    for sheet in sheets:
        wanted = sheet.wanted
        into = sheet.path.with_suffix("." + a.format)
        if wanted == (sheet.width, sheet.height) and into == sheet.path:
            continue

        image, stored = encode(shrink(sheet.path, wanted), a.format, a.error)
        before += sheet.bytes
        after += len(image)
        into.write_bytes(image)
        if into != sheet.path:
            sheet.path.unlink()
            if not name(base, sheet.path, into.name):
                orphans.append(_sim.rel(base, into))

        done += 1
        how[stored] = how.get(stored, 0) + 1
        print(f"   {sheet.shape:>11s} -> {wanted[0]}x{wanted[1]:<5d}  {stored:8s}  {_sim.rel(base, into)}")

    if done == 0:
        print("\nnothing to rewrite")
        return 0

    print(f"\n{done} sheets rewritten: {before / 1048576:.1f} MB -> {after / 1048576:.1f} MB")
    print("   stored as " + ", ".join(f"{count} {kind}" for kind, count in sorted(how.items())))
    if orphans:
        print(f"\n{len(orphans)} are named in code and not in a variant file — change those by hand:")
        for orphan in orphans:
            print(f"   {orphan}")

    print("\nlook at a frame before committing: qq tests e2e")
    return 0


def name(base: Path, was: Path, now: str) -> bool:
    """
    The variant file beside a sheet, told what the sheet is called now. Rewritten as text and not as
    reparsed JSON, so the hand-laid formatting of those files survives; `False` when no file names it.
    """
    found = False
    for variant in sorted(was.parent.glob("*.json")):
        text = variant.read_text(encoding="utf-8")
        if f'"{was.name}"' not in text:
            continue
        variant.write_text(text.replace(f'"{was.name}"', f'"{now}"'), encoding="utf-8")
        found = True

    return found


def encode(image, fmt: str, bound: float) -> tuple[bytes, str]:
    """
    The smallest encoding of this sheet whose error stays under `bound`, and what that turned out to
    be. **A quality number is the wrong thing to fix across a town**: WebP's chroma subsampling costs
    almost nothing on a building's flat wall and a great deal on a walker cut into sixty-four frames
    of a figure thirty pixels wide, so the sheet that cannot afford it is stored lossless and says so.

    **Alpha is lossless in every case.** A sprite's edge is its silhouette, and a soft one shows at
    every zoom where a softened colour does not.

    The error is measured over the pixels that are actually opaque. A nearly-transparent pixel has no
    settled colour — dividing the alpha back out of one amplifies whatever noise it had — so counting
    those would drown the figure in a number nobody can see.
    """
    if fmt != "webp":
        return written(image, "PNG", optimize=True), "png"

    import numpy as np
    from PIL import Image

    was = np.asarray(image).astype(np.float32)
    solid = was[..., 3] > 128
    for quality in QUALITIES:
        kept = written(image, "WEBP", quality=quality, alpha_quality=100, method=6)
        if not solid.any():
            return kept, f"q{quality}"

        now = np.asarray(Image.open(io.BytesIO(kept)).convert("RGBA")).astype(np.float32)
        if np.abs(was[..., :3] - now[..., :3])[solid].mean() <= bound:
            return kept, f"q{quality}"

    return written(image, "WEBP", lossless=True, method=6), "lossless"


def written(image, fmt: str, **how) -> bytes:
    kept = io.BytesIO()
    image.save(kept, fmt, **how)
    return kept.getvalue()


def shrink(path: Path, wanted: tuple[int, int]):
    """
    Downsample on premultiplied alpha and come back out of it, so the transparent black around a
    sprite cannot bleed a dark fringe into its edge.
    """
    from PIL import Image
    import numpy as np

    image = Image.open(path).convert("RGBA")
    if (image.width, image.height) == wanted:
        return image

    # Channel at a time and in floating point. Premultiplied colour rounded back to bytes before the
    # resample would quantise a nearly-transparent pixel to a handful of levels, and dividing the
    # alpha back out multiplies that error by however transparent it was — which is a sprite's whole
    # edge. This costs four resamples and keeps them clean.
    pixels = np.asarray(image).astype(np.float32)
    alpha = pixels[..., 3] / 255.0
    planes = [
        np.asarray(Image.fromarray(plane, "F").resize(wanted, Image.LANCZOS))
        for plane in (*(pixels[..., c] * alpha for c in range(3)), pixels[..., 3])
    ]

    out = np.clip(planes[3], 0, 255)
    lit = out > 0
    colour = np.stack(
        [np.divide(plane, out / 255.0, out=np.zeros_like(plane), where=lit) for plane in planes[:3]], axis=2
    )
    return Image.fromarray(
        np.concatenate([np.clip(colour, 0, 255), out[..., None]], axis=2).astype(np.uint8), "RGBA"
    )


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq art",
        description="Check every sheet against the texel density its own configuration declares.",
        allow_abbrev=False,
    )
    parser.add_argument("--fix", action="store_true", help="resize the sheets that are over their grid")
    parser.add_argument("--art", type=float, help="the art grid to hold to, px/m (default: ViewFigures')")
    parser.add_argument("--cars", type=float, help="the car grid to hold to, px/m (default: ViewFigures')")
    parser.add_argument("--format", choices=("png", "webp"), default="png", help="how to store them (default png)")
    parser.add_argument("--error", type=float, default=6.0,
                        help="how far a webp may move an opaque pixel, 0-255 (default 6); a sheet that "
                             "cannot be held to it is stored lossless")
    parser.add_argument("--limit", type=int, default=20, help="how many rows to print")
    return parser.parse_args()


if __name__ == "__main__":
    cli.entry(main)
