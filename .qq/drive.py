"""
Drive a car of a town by hand from a script, and read back what it did and what it looks like.

**The driving is the engine's and this only asks for it** (`--drive`, src/app/drive/). A script is a file
of steps — pick a car out, hold the pedals and the wheel for a while, order, take a frame — pushed through
the very seam the player's keys go through, so nothing in one can ask for more than a key can (DRV-1,
src/app/drive/docs/requirements.md says what a step may be, and the engine prints the list when it cannot
read a line).

Write the script with the file tools and run it with this. **There is no process left standing between
runs**: the town is seeded, so the whole script is played again from the first tick each time and
appending a step is the whole of steering (DRV-6). Every step prints what the unit is doing — the unit
panel's own rows — and a `shot` step leaves a captioned frame under `--frames`.
"""

import argparse
import re

import _sim
from common import cli, paths

NAME = re.compile(r"^[A-Za-z0-9_-]{1,32}$")

#: Standing a city up is most of a minute and a script may drive for several of them.
TIMEOUT_S = 900

ENGINE = "traffic-dotnet.csproj"

#: How tall a frame is against its width. The town is looked at on a widescreen window.
FRAME_ASPECT = 9 / 16


def main():
    a = parse_args()
    base = _sim.root()

    script = paths.resolve_in_root(a.script, base)
    if not script.is_file():
        cli.fail(
            f"no drive script at {_sim.rel(base, script)}: write one first — "
            "the steps are src/app/drive/docs/requirements.md"
        )

    frames = scratch(base, a.frames)
    out = scratch(base, a.out)

    if not a.no_build:
        built = _sim.dotnet(base, "build", ENGINE, "-c", "Release", "-v", "q", "--nologo")
        if built.code != 0:
            print((built.stdout + built.stderr).strip())
            return cli.fail("the build failed, so nothing was driven")

    height = int(a.width * FRAME_ASPECT)
    argv = [
        "run", "--project", ENGINE, "-c", "Release", "--no-build", "--",
        "--map", a.map,
        "--drive", _sim.rel(base, script),
        "--frames", _sim.rel(base, frames),
        "--out", _sim.rel(base, out),
        "--size", str(a.width), str(height),
    ]
    if a.view > 0:
        argv += ["--view", str(a.view)]
    if a.ui:
        argv += ["--ui", a.ui]

    driven = _sim.dotnet(base, *argv)
    text = (driven.stdout + driven.stderr).strip()
    print(text)
    if driven.code != 0:
        return cli.fail(text.splitlines()[-1] if text else "the drive failed with no output")

    return 0


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq drive",
        description="Drive a car of a town by hand from a script, and read back what it did.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("--script", default=".tmp/drive/script.txt",
                        help="the script of steps to drive, written with the file tools")
    parser.add_argument("--map", default="Test", type=map_name,
                        help="the map to stand the town up from")
    parser.add_argument("--frames", default=".tmp/drive",
                        help="where a shot step leaves its captioned frames")
    parser.add_argument("--out", default=".tmp/drive/log.md",
                        help="where the whole drive is written as one document")
    parser.add_argument("--view", default=0, type=cli.counting,
                        help="how much town a frame spans, in whole metres (0 = the figure on SimConfig)")
    parser.add_argument("--width", default=1280, type=cli.counting,
                        help="how wide a frame is in pixels; the height is 16:9 of it")
    parser.add_argument("--ui", default="", type=ui_words,
                        help="the --ui words every frame is drawn with, comma-separated")
    parser.add_argument("--no-build", action="store_true",
                        help="drive with the engine already built")
    return parser.parse_args()


def scratch(base, candidate: str):
    """
    A path this run may write to. **Only `.tmp/`**, which is gitignored and wiped without asking: a drive
    writes frames and a log, and nothing it is given should be able to land on a source file.
    """
    full = paths.resolve_in_root(candidate, base)
    if not full.is_relative_to(base / ".tmp"):
        cli.fail(f"a drive writes into .tmp/ and {_sim.rel(base, full)} is not under it")
    return full


def map_name(value: str) -> str:
    if not NAME.match(value):
        raise argparse.ArgumentTypeError(f"{value!r} is not a map name")
    return value


def ui_words(value: str) -> str:
    """The `--ui` list, as the engine's own words: names and dashes, nothing that could carry an argument."""
    for word in value.split(","):
        if word and not re.match(r"^[A-Za-z0-9.-]{1,32}$", word):
            raise argparse.ArgumentTypeError(f"{word!r} is not a --ui word")
    return value


if __name__ == "__main__":
    cli.entry(main)
