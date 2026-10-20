"""
Build the game, run the suite, check the citations, then take a probe's figures.

The standard build-and-test route, so a check does not need approving every time it is run. The
suite is `qq tests all`'s and is run through it rather than copied, so what "the suite" means cannot
differ between the two. It carries the five gates in src/tests/gates/ — the steady state allocates nothing, the frame's
managed→native crossings are flat in the size of the town, nothing ends up inside anything else,
the solver's step stays inside its budget, no way's claims are laid twice or with a hole in one —
so a plain run is what says a milestone still holds.

The citations are the fifth thing nothing else can check: a doc comment naming a rule that has
since been renumbered or retired compiles, passes and misleads. `qq doclint` is the whole report.
The provenance is the sixth: every image under assets/ traces to the prompt that made it, or is on the
legacy list (raw_assets/index.md). `qq sprites check` is the whole report.

The probes are off by default because they are figures rather than gates, and they are always
built and run in **Release**: a tick cost or an allocation figure taken off a Debug build is not
a figure. What is *printed* is the few lines worth reading; what the run *saw* is kept whole
under .tmp/logs/.

Nothing here photographs the town. Tiers 3 and 4 of docs/verification.md are the game's own
`--shot`, which needs no window and no display — a wrapper around it would be a second place for
the arguments to be wrong.

A cold build with probes is a few minutes, which is what `TIMEOUT_S` below is for — no `--timeout`
is needed on the call.
"""

from __future__ import annotations

import argparse
import os
import re

import _docs
import _sim
import sprites
import tests
from common import cli, proc

#: What a call may take: a build, the suite, the citations and a probe's figures.
TIMEOUT_S = 900

#: The game. It is built by the test project's reference too, but building it first is what tells a
#: compile error apart from a test failure — and the tiers below build the test project themselves.
ENGINE = "traffic-dotnet.csproj"

#: The document whose ID map the families are checked against, as `qq doclint` checks it.
MAP = "docs/index.md"

#: And said anyway, belt and braces: an E2E test reached some other way still takes its frame unjudged.
JUDGING = ("TRAFFIC_E2E_JUDGE", "off")

#: Where a Release build lands, and what it is called there — `AssemblyName` in the .csproj.
RELEASE = "bin/Release/net11.0/traffic-dotnet"

#: Every probe the build ships, under the name `--bench` takes. Read off it rather than listed here,
#: because a copy of the list goes stale the day a probe is added or deleted.
CATALOGUE = "src/bench/CheckCatalogue.cs"


def probe_names() -> list[str]:
    """The names `--bench` takes, as the catalogue declares them, and `all`."""
    source = (_sim.ROOT / CATALOGUE).read_text(encoding="utf-8")
    return re.findall(r'^\s*new\("([a-z-]+)"', source, re.MULTILINE) + ["all"]


def map_names() -> list[str]:
    """The maps a file ships for, which is every map a probe can be pointed at."""
    return sorted(path.stem for path in (_sim.ROOT / "towns").glob("*.map"))


def main():
    a = parse_args()
    base = _sim.root()
    log = []
    status = 0
    os.environ[JUDGING[0]] = JUDGING[1]
    tests.lighten(a.full)

    print("=== build")
    built = _sim.dotnet(base, "build", ENGINE, *tests.light_build())
    log.append("=== build\n" + built.stdout + built.stderr)
    _sim.show(built.stdout, r"error|warning CS|Build succeeded|Build FAILED")
    if built.code != 0:
        # Never run the suite against the last good assembly: the results would look real.
        _sim.write_log(base, "checks", "\n".join(log))
        print("=== status 1 (build failed)")
        return 1

    # `qq tests`' own machinery and not a second `dotnet test` of its own. Run this tool's way it was
    # the whole suite in Debug as one un-split invocation — six minutes for what the tiers do in one
    # and a half, and a second answer to what "the suite" is the day a tier is added.
    status = tests.suite(base, tests.ALL, log) or status

    status = citations(base) or status
    status = provenance(base) or status

    if a.probes != "none":
        status = probes(base, a.probes, a.map, log) or status

    _sim.write_log(base, "checks", "\n".join(log))
    print(f"=== status {status}")
    return status


def citations(base):
    """
    The rules against the code that cites them — a second's reading, and the one check here that no
    test can make: a doc comment citing a rule that has been renumbered or retired compiles fine.

    The whole report is `qq doclint`; what is printed here is the count and whether it is sound.
    """
    found = _docs.audit(base, MAP)
    if found.sound:
        print(f"=== citations ok ({len(found.defined)} rules, "
              f"{sum(len(sites) for sites in found.cited.values())} citations)")
        return 0

    print(f"=== citations: {len(found.twice)} stated twice, {len(found.dangling)} cited by code and "
          f"stated nowhere, {len(found.unmapped)} families off the map, {len(found.unrunged)} with no "
          f"rung — qq doclint says which")
    return 1


def provenance(base):
    """Every image under assets/ traces to the prompt that made it, or is on the legacy list
    (raw_assets/index.md). The whole report is `qq sprites check`."""
    problems = sprites.audit(base)
    print(f"=== provenance: {len(problems)} problem(s) — qq sprites check says which" if problems
          else "=== provenance ok")
    return 1 if problems else 0


def probes(base, which, town, log):
    """The figures, off a Release build, because a Debug figure is not one."""
    print(f"=== probes ({which} on {town}, Release)")
    built = _sim.dotnet(base, "build", ENGINE, "-c", "Release")
    log.append("=== probes build\n" + built.stdout + built.stderr)
    if built.code != 0:
        _sim.show(built.stdout, r"error")
        return 1

    run = proc.run([str(base / RELEASE), "--bench", which, "--map", town], cwd=base, check=False)
    log.append(f"=== probes {which}\n" + run.stdout + run.stderr)
    print(run.stdout.strip())
    return 1 if run.code != 0 else 0


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq checks",
        description="Build the game, run the suite, check the citations, then take a probe's figures.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("--probes", default="none", choices=["none"] + probe_names(),
                        help=f"which figures to take after the suite passes ({CATALOGUE})")
    parser.add_argument("--map", default="Odesa", choices=map_names(),
                        help="the town the probes run on")
    parser.add_argument("--full", action="store_true",
                        help="build and test on every core at normal priority, rather than light (qq tests --help)")
    return parser.parse_args()


if __name__ == "__main__":
    cli.entry(main)
