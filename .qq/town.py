"""
Report what a town holds: road curvature, junction spacing and arms, the joints nothing meets at, the
car parks cut into its roads, the roofs its buildings wear and where, and what its own boundary comes to when it is
moved off itself.

`--roofs` lists every building (`--bench roofs`), or with `--roofs=ID,ID` only those wearing those roofs — where each
stands, to frame a picture of it with `--shot … --at X Y`.

**The figures are the engine's and this only asks for them.** The readings come off the plan the
generator just laid (`--bench shape`, `--bench joints`, `--bench close`, `--bench shapes`, src/bench/TownShape.cs;
`--bench connectors`, src/bench/ConnectorShape.cs;
`--bench outset`, src/bench/BoundaryProbe.cs), so there is one reader of a town and it is the one that
made it. `--outset` is the one of them that *gates*: the boundary of a shape moved off itself is closed,
so a run left open exits non-zero and prints the boundary and the candidates either side of the hole. This tool used to parse a baked `.town`
file — a second reader of a format, kept in step with the first by hand — and no town is carried as a
file any more.

What a town *holds* rather than what shape it came out is `--bench census`, which stands the graphs up
as well and costs seconds rather than the lay.
"""

import argparse
import re

import _sim
from common import cli

NAME = re.compile(r"^[A-Za-z0-9_-]{1,32}$")

#: A city is most of a minute to lay, and `qq town` with no map lays every one of them.
TIMEOUT_S = 600

ENGINE = "traffic-dotnet.csproj"


def main():
    a = parse_args()
    base = _sim.root()

    if not a.map:
        return run(base, "shapes")

    if a.joints:
        return run(base, "joints", a.map)

    if a.parks:
        return run(base, "parks", a.map)

    if a.roofs is not None:
        return run(base, "roofs", a.map, keep=roof_filter(a.roofs))

    if a.outset:
        return run(base, "outset", a.map)

    if a.close:
        return run(base, "close", a.map)

    if a.connectors:
        return run(base, "connectors", a.map)

    return run(base, "shape", a.map)


def run(base, check: str, map_name: str | None = None, keep=None) -> int:
    argv = ["run", "--project", ENGINE, "-c", "Release", "--", "--bench", check]
    if map_name:
        argv += ["--map", map_name]

    laid = _sim.dotnet(base, *argv)
    text = (laid.stdout + laid.stderr).strip()
    if laid.code != 0:
        cli.fail(text.splitlines()[-1] if text else f"--bench {check} failed with no output")

    print("\n".join(line for line in text.splitlines() if keep is None or keep(line)))
    return 0


def roof_filter(ids: str):
    """The lines of `--bench roofs` to keep: all of them, or the heading and the buildings wearing one of `ids`."""
    wanted = {i for i in ids.split(",") if i}
    if not wanted:
        return None
    return lambda line: not line.startswith("  ") or line.split()[0] == "x" or line.split()[-1] in wanted


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq town",
        description="Report what a town holds: road curvature, junction spacing and arms, ground shares.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("map", nargs="?", type=map_name,
                        help="the map to report on, by name; every shipped map as one row each if omitted")
    parser.add_argument("--joints", action="store_true",
                        help="list every junction only two roads meet at, where it stands and what kept it")
    parser.add_argument("--parks", action="store_true",
                        help="list every car park cut into a road, where it stands, its arms and its bays")
    parser.add_argument("--roofs", nargs="?", const="", default=None, metavar="ID,ID",
                        help="list every building: where it stands, its heading, the roof it wears and that roof's "
                             "fans; with ids, only the buildings wearing those roofs")
    parser.add_argument("--outset", action="store_true",
                        help="move the town's boundary off itself and report what closed and what did not")
    parser.add_argument("--close", action="store_true",
                        help="list the roads between junctions of three arms or more standing nearer than a traced "
                             "map gathers into one, and how such pairs chain")
    parser.add_argument("--connectors", action="store_true",
                        help="read every connector for winding, swinging out, running past its corner, bowing, "
                             "tightness and leaving its junction, and list the worst")
    return parser.parse_args()


def map_name(value: str) -> str:
    if not NAME.match(value):
        raise argparse.ArgumentTypeError(f"{value!r} is not a map name")
    return value


if __name__ == "__main__":
    cli.entry(main)
