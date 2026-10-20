"""
Prove a set of car looks works in the game: one animated GIF of each look's car put through every lamp state and
wrecked.

**The picture is the engine's** (`--proof`, src/app/shot/LampProof.cs): the looks are told to it as the fleet, so their
lamp sheet is cut afresh from their sprites and zone maps first, and every body, tyre, lens and glow in a frame is an
instance the town's own frame is built from. A tile whose lamp does not light, lights off its lens or flashes out of
step with the rest is the asset to look at.

    qq proof                                   every generated look (one with a `.src.json` beside its sprite)
    qq proof --looks=hatch_coral,micro_mint    just those
    qq proof --looks=all                       the whole fleet as Fleet.json lists it
"""

import argparse
import json
import re

import _sim
from common import cli, paths

#: The build and a told fleet's lamp cut, then a frame a state a look.
TIMEOUT_S = 300

ENGINE = "traffic-dotnet.csproj"
VARIANTS = "assets/agents/car/variants"
ID = re.compile(r"^[a-z0-9_]{1,40}$")


def main():
    a = parse_args()
    base = _sim.root()
    out = paths.resolve_in_root(a.out, base)
    if not out.is_relative_to(base / ".tmp"):
        cli.fail(f"a proof is scratch and goes under .tmp/: {_sim.rel(base, out)} is not")

    looks = chosen(base, a.looks)
    if not a.no_build:
        built = _sim.dotnet(base, "build", ENGINE, "-c", "Release", "-v", "q", "--nologo")
        if built.code != 0:
            print((built.stdout + built.stderr).strip())
            return cli.fail("the build failed, so nothing was proven")

    argv = ["run", "--project", ENGINE, "-c", "Release", "--no-build", "--"]
    if looks:
        argv += ["--fleet", ",".join(looks)]
    ran = _sim.dotnet(base, *argv, "--proof", _sim.rel(base, out), *(["--no-glow"] if a.no_glow else []))
    text = (ran.stdout + ran.stderr).strip()
    if ran.code != 0:
        print(text)
        return cli.fail(text.splitlines()[-1] if text else "the proof failed with no output")

    print(text.splitlines()[-1])
    if a.frames:
        folder, count = write_frames(out)
        print(f"{_sim.rel(base, folder)}: {count} frames")
    print(f"looks: {', '.join(looks) if looks else 'the shipped fleet and the service vehicles'}")
    return 0


def chosen(base, asked: str) -> list[str]:
    """The ids to tell the engine as the fleet, or none for the shipped one."""
    if asked == "all":
        return []
    if asked:
        ids = [i for i in asked.split(",") if i]
        if bad := [i for i in ids if not ID.match(i) or not (base / VARIANTS / i / f"{i}.json").is_file()]:
            cli.fail(f"no car look {', '.join(bad)} under {VARIANTS}")
        return ids
    generated = []
    for folder in sorted((base / VARIANTS).iterdir()):
        d = folder / f"{folder.name}.json"
        if d.is_file() and (folder / f"{folder.name}.src.json").is_file() and "footprintM" in json.loads(d.read_text()):
            generated.append(folder.name)
    if not generated:
        cli.fail(f"no look under {VARIANTS} traces to a generated frame: name some with --looks")
    return generated


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq proof",
        description="Prove a set of car looks works in the game: one animated GIF of each look's car put through "
                    "every lamp state and wrecked.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("--looks", default="",
                        help="comma-separated look ids, or `all` for the shipped fleet; empty is every generated look")
    parser.add_argument("--out", default=".tmp/proof/lamps.gif", help="where the GIF goes, under .tmp/")
    parser.add_argument("--no-build", action="store_true", help="prove with the engine already built")
    parser.add_argument("--no-glow", action="store_true",
                        help="draw the lit lenses alone, without the light spilling round them")
    parser.add_argument("--frames", action="store_true",
                        help="also write each frame as a PNG, beside the GIF in <stem>_frames/, to be read one by one")
    return parser.parse_args()


def write_frames(gif):
    """Each frame of the GIF as its own PNG in `<stem>_frames/`, numbered: a GIF is read as its first frame only."""
    from PIL import Image, ImageSequence

    folder = gif.with_name(f"{gif.stem}_frames")
    folder.mkdir(exist_ok=True)
    for old in folder.glob("*.png"):
        old.unlink()
    with Image.open(gif) as movie:
        for n, frame in enumerate(ImageSequence.Iterator(movie)):
            frame.convert("RGB").save(folder / f"{n:02d}.png")
    return folder, n + 1


if __name__ == "__main__":
    cli.entry(main)
