"""
Run one or more tiers of the test suite, each in the configuration its answer is true in.

The suite is selected by `Tier` (src/tests/Tier.cs), not by folder, because cost does not follow
the slice tree: one feature's folder holds a microsecond of arithmetic beside a second of a whole town.
Run untiered it is minutes, which is minutes nobody spends after every edit; run as
`qq tests` it is five seconds.

A tier is run when it is named, and naming several runs several. Nothing is implied by anything.

    qq tests                    unit                 ~5s      after every edit
    qq tests unit town          + a whole town       ~1m10s   before saying a behaviour change works
    qq tests perf               the five gates       ~35s     after touching a hot path
    qq tests device             the tick's kernels   —        after touching a kernel or runtime/, on lavapipe
    qq tests all                unit, town, perf, device ~1m45s before a commit worth being sure of
    qq tests maps               the shipped cities   ~25s     before shipping or retuning a city
    qq tests exam               the scenario map     ~1m      after changing how traffic meets: every card passes
    qq tests e2e                the frames, unjudged ~2m10s   after changing anything that draws
    qq tests e2e --judge        the frames, judged by an agent: MONEY, and about half an hour
    qq tests --changed          whatever the working tree has moved, and nothing else

**A call of this tool is not capped at the proxy's ten seconds** — `TIMEOUT_S` below is its own, so
`qq tests all` and `qq tests e2e --judge` run to the end without `--timeout` being spelled out.

**`maps` is not in `all` and is not reached by `--changed`.** A shipped city is content — a brief, a seed
and whatever the generator made of them — and what this engine owes a town is asked of towns the suite lays
for itself. Editing a brief, adding a city or retuning the generator is when somebody runs it.

**Nor is `exam`.** It drives the engine end to end through one staged scenario a card, and every card is
expected to pass — but it is a whole town driven for two minutes, run when a change can have moved
how traffic meets rather than on every commit, as the frames are.

`--changed` reads the working tree against HEAD and names the tiers those files can have moved
(`SELECT` below). It is the answer to reaching for `all` because choosing was harder than waiting.

`--root` runs the suite of a copy of the tree kept under `.tmp/` — a lab variant being measured beside this
one (`qq prof --root` is the same folder's binary) — built and tested there, with its log kept there too.

**It is the edit loop's tier and not the commit's.** The map is deliberately narrow — a change under
`agents/` runs unit and town, because the gates are about the machinery a tick is made of rather than
about one driver's arithmetic — so an allocation introduced in behaviour code is caught by the `all`
a commit is worth being sure of, and not by this.

**One take of two tiers is deliberately not run in Release**, and it is the whole of what Debug is
still built for here: `CrossingGateTests` (perf) and `DeviceCrossingGateTests` (device) read a counter that
is `[Conditional("DEBUG")]`, so a Release run of either compares zero to zero and passes having measured
nothing. The e2e tier stays in
Debug for the same counter, which the shot path prints beside every frame.

**A run is light unless `--full` is given**: at a low priority and on half the cores — the build, xUnit's
parallel classes, the runtime's own `Parallel` loops and lavapipe's threads — so the machine stays usable beside
it. The times above are full runs'; a light one takes longer, and its budget line says it was light.

Output is the summary line per take; the whole of what the run saw is kept under .tmp/logs/.
"""

from __future__ import annotations

import argparse
import os
import time
from typing import NamedTuple

import _sim
from common import cli, git, paths

#: What a call of this tool may take before the proxy kills it. **This is the tool the proxy's
#: ten-second default was wrong about**: `all` is two builds and every gate, and `e2e --judge` is
#: half an hour of an agent looking at pictures — so the budget is the longest run that exists here
#: and not the usual one, since a watchdog that fires on a slow machine is a retry and not a finding.
TIMEOUT_S = 2700

#: The test project. The game assembly comes with it as a project reference.
TESTS = "src/tests/traffic-dotnet-tests.csproj"


class Take(NamedTuple):
    """One `dotnet test` run: a configuration, and the part of a tier whose answer is true in it."""

    #: The build configuration this take is run in.
    config: str
    #: What narrows the tier's trait to the tests this take is for, or nothing for all of them.
    narrow: str = ""
    #: Why this take is on its own, printed beside it so a split tier says what split it.
    why: str = ""
    #: Whether a crash of the test host leaves a dump and the tests in flight under BLAME (`--blame-crash`): a
    #: kernel's fault on lavapipe takes the whole host down with nothing but the drivers' warnings in the log
    #: (src/runtime/docs/decision-log.md, 2026-10-10).
    blames: bool = False


class Suite(NamedTuple):
    """A tier: the trait it is selected by, what it is for, and the runs it takes to answer it."""

    trait: str
    what: str
    takes: tuple[Take, ...]


#: Each tier and the runs it takes. The order is the order they run in — cheapest first, so a broken
#: build or a broken unit test is reported before the gates.
#:
#: **Almost everything is measured in Release**, where it is around four times faster and asserts
#: exactly the same things: this engine has no `Debug.Assert` anywhere, and the gates read allocation
#: off `GC.GetAllocatedBytesForCurrentThread`, which counts the same bytes either way. What is left in
#: Debug is the one gate that cannot be taken without it.
TIERS = {
    "unit": Suite("Unit", "engine-free arithmetic and the fixture town", (Take("Release"),)),
    "town": Suite("Town", "questions asked of a town the suite lays", (Take("Release"),)),
    "perf": Suite("Perf", "the five gates, serialised", (
        Take("Release", "FullyQualifiedName!~CrossingGate"),
        Take("Debug", "FullyQualifiedName~CrossingGate", "the counter it reads is [Conditional(DEBUG)]"),
    )),
    "device": Suite("Device", "the tick's kernels, on lavapipe", (
        Take("Release", "FullyQualifiedName!~CrossingGate", blames=True),
        Take("Debug", "FullyQualifiedName~CrossingGate", "the counter it reads is [Conditional(DEBUG)]", blames=True),
    )),
    "maps": Suite("Maps", "the shipped cities, asked for by name", (Take("Release"),)),
    "exam": Suite("Exam", "the scenario map, one case a card", (Take("Release"),)),
    "e2e": Suite("E2E", "the visual tier, staged and photographed", (Take("Debug"),)),
}

#: What `all` means: everything that costs no money, needs nobody to look at a picture, and is about
#: this engine rather than about somebody's map. `maps` is deliberately outside it — a shipped city is
#: content, and a suite that gated on it would be a function of the content rather than of the code.
#: `exam` is outside it too: it is a whole town driven end to end, card by card, run when a change can have
#: moved how traffic meets.
ALL = ["unit", "town", "perf", "device"]

#: What a change to a path can have moved, most reaching first — the first match wins. The tick,
#: the solver and the kernel are where a change reaches everything, so they take the widest tier;
#: what only draws cannot move a number, and gets the tier that looks at pictures instead. `unit`
#: is on every row because every row can break the arithmetic under it. `device` follows the
#: kernels and the machine they run on.
SELECT = [
    ("src/core/", ["unit", "town", "perf"]),
    ("src/runtime/", ["unit", "town", "perf", "device"]),
    ("src/world/physics/", ["unit", "town", "perf", "device"]),
    ("src/tests/gates/", ["unit", "town", "perf"]),
    ("src/world/town/", ["unit", "town", "perf"]),
    ("src/world/road/", ["unit", "town", "perf"]),
    ("src/app/render/", ["unit", "e2e"]),
    ("src/app/shot/", ["unit", "e2e"]),
    ("src/app/debug/", ["unit", "e2e"]),
    ("src/app/hud/", ["unit", "e2e"]),
    ("src/app/camera/", ["unit", "e2e"]),
    ("src/app/screen/", ["unit", "e2e"]),
    ("assets/", ["unit", "e2e"]),
    ("towns/", ["unit", "maps"]),
    ("src/citygen/gen/", ["unit", "town", "maps"]),
    ("src/", ["unit", "town"]),
]

#: Where a kernel or its driver lives, in whichever slice: a change there is the device tier's as well as
#: whatever its prefix in SELECT says. A test file is the device tier's when it carries the trait.
DEVICE_PATHS = ("/kernels/", "src/app/devicetick/")
DEVICE_TRAIT = "Tier.Device"

#: The lowest rung on src/tests/Priority.cs, which is also `--upto`'s default: everything.
LAST_RUNG = 9

#: What the suite everybody runs is allowed to cost, in seconds (docs/verification.md). It is reported
#: and never asserted — a wall clock measures the machine as much as the suite — but it is the figure a
#: new test is weighed against, and the rung it is dropped from is the bottom of `Priority`.
BUDGET_S = 300

#: A working tree this far from HEAD is not an edit anybody is choosing a tier for.
SPRAWL = 60

#: One take's filter matched no test. Neither a pass nor a failure until every take has been asked.
MISSED = -1

#: Where a take that blames leaves the dump of a crashed test host.
BLAME = ".tmp/blame"

#: A light run's share of the cores (one in this many) and its niceness: the default, so a run leaves the machine usable.
LIGHT_SHARE = 2
LIGHT_NICE = 10

#: The most memory a light run may take, build and suite alike, before the kernel kills it rather than the desktop —
#: a quarter of this machine's, so two runs side by side still leave it usable.
LIGHT_MEMORY = "14G"

#: How many cores a light run takes, or None for a full one; set once by `lighten`.
_light_cores: int | None = None


def lighten(full: bool) -> None:
    """
    A light run, unless `full`: this process and every child at a low priority, and the build, xUnit, the runtime's
    `Parallel` loops and lavapipe each held to a share of the cores. Children inherit both through the environment
    and the niceness, so every tool that runs the suite calls this once before it.
    """
    global _light_cores
    if full:
        return
    _light_cores = max(1, (os.cpu_count() or LIGHT_SHARE) // LIGHT_SHARE)
    os.setpriority(os.PRIO_PROCESS, 0, LIGHT_NICE)
    os.environ["DOTNET_PROCESSOR_COUNT"] = str(_light_cores)
    os.environ["LP_NUM_THREADS"] = str(_light_cores)
    os.environ.setdefault("QQ_MEMORY_MAX", LIGHT_MEMORY)


def light_build() -> list[str]:
    """What holds a build to the light share of the cores, or nothing for a full run."""
    return [f"-maxcpucount:{_light_cores}"] if _light_cores else []


def light_test() -> list[str]:
    """What holds xUnit's parallel classes to the light share, as a run setting after `--`, or nothing."""
    return ["--", f"xUnit.MaxParallelThreads={_light_cores}"] if _light_cores else []


def main():
    a = parse_args()
    base = _sim.root()
    if a.root:
        base = paths.resolve_in_root(base / a.root, base / ".tmp")
        if not (base / TESTS).is_file():
            cli.fail(f"no {TESTS} under {a.root}: --root names a copy of the tree")
    chosen = pick(a.tiers + (from_the_tree(base) if a.changed else []))
    lighten(a.full)
    log = []
    began = time.monotonic()

    status = suite(base, chosen, log, name=a.name, upto=a.upto, judge=a.judge)

    _sim.write_log(base, "tests", "\n".join(log))
    print(budget(chosen, time.monotonic() - began))
    print(f"=== status {status}")
    return status


def suite(base, chosen: list[str], log: list[str], name=None, upto=LAST_RUNG, judge=False) -> int:
    """
    Build what the chosen tiers need and take each of them, in the configuration its answer is true in.

    <b>Separate from `main` because it has a second caller</b>: `qq checks` runs the same three tiers as
    part of a build-and-test, and running them its own way meant the whole suite in Debug as one
    un-split invocation — four times the wall clock for the same assertions, and a second answer to
    what "the suite" means the day a tier was added.
    """
    status = 0
    matched: dict[str, bool] = {}

    if "e2e" in chosen and not judge:
        # Off unless asked for by name: judging is a few dollars and about half an hour, and a tool
        # that spends either because of a default is a tool nobody can run to check a frame.
        os.environ["TRAFFIC_E2E_JUDGE"] = "off"

    for config in ordered_configs(chosen):
        print(f"=== build ({config})")
        built = _sim.dotnet(base, "build", TESTS, "-c", config, *light_build())
        log.append(f"=== build ({config})\n" + built.stdout + built.stderr)
        if built.code != 0:
            _sim.show(built.stdout, r"error")
            return 1

        for tier in chosen:
            for take in [t for t in TIERS[tier].takes if t.config == config]:
                outcome = run(base, tier, take, name, upto, log)
                matched.setdefault(tier, False)
                matched[tier] = matched[tier] or outcome != MISSED
                status = (0 if outcome == MISSED else outcome) or status

    # A name that matched nothing anywhere is a name that names nothing — one take or one tier missing
    # it is ordinary (a class lives in one tier, and `all` asks several), every one missing it is not.
    if matched and not any(matched.values()):
        print(f"=== no test in {', '.join(matched)} is named `{name}` — nothing ran")
        status = 1

    return status


def budget(chosen: list[str], tookS: float) -> str:
    """
    What the run cost against what it is allowed. **A reading and not a gate** — a wall clock is the one
    figure this project refuses to assert on, because it measures the machine as much as the suite — so
    this says the number and lets a person decide, exactly as a probe that gates nothing does.

    The five minutes are `all`'s alone (docs/verification.md). A run that named a tier outside it is being
    asked for deliberately and is nobody's budget.
    """
    spent = f"{int(tookS // 60)}m {tookS % 60:04.1f}s" if tookS >= 60 else f"{tookS:.1f}s"
    if _light_cores:
        spent += f" (light: {_light_cores} of {os.cpu_count()} cores)"
    if not set(chosen) <= set(ALL):
        return f"=== took {spent}"

    left = BUDGET_S - tookS
    if left >= 0:
        return f"=== took {spent} of the suite's {BUDGET_S // 60:.0f}-minute budget, {left / 60:.1f} min spare"
    return (f"=== took {spent}, which is {-left:.0f}s OVER the suite's {BUDGET_S // 60:.0f}-minute budget — "
            "something is dropped or made cheaper, from the bottom of src/tests/Priority.cs")


def from_the_tree(base) -> list[str]:
    """
    The tiers the working tree's own changes can have moved, and the reason printed with them.

    Nothing here decides what a change means: it maps each path to the tier that could catch it
    going wrong, and a path the map has never heard of is `unit` — cheap, and never zero tiers.
    """
    changed = [change.path for change in git.porcelain(git.status(base, "--untracked-files=all"))]
    if not changed:
        print("=== changed: nothing since HEAD — unit only")
        return []
    if len(changed) > SPRAWL:
        print(f"=== changed: {len(changed)} files, which is a tree and not an edit — running all")
        return ALL

    wanted, why = [], {}
    for path in changed:
        prefix, tiers = next(((prefix, tiers) for prefix, tiers in SELECT if path.startswith(prefix)), ("", ["unit"]))
        why.setdefault(prefix or "elsewhere", 0)
        why[prefix or "elsewhere"] += 1
        wanted.extend(tiers)
        if "device" not in tiers and on_the_device(base, path):
            why.setdefault("kernels", 0)
            why["kernels"] += 1
            wanted.append("device")
    where = ", ".join(f"{prefix} ({count})" for prefix, count in sorted(why.items(), key=lambda pair: -pair[1]))
    print(f"=== changed: {len(changed)} files — {where}")
    return wanted


def on_the_device(base, path: str) -> bool:
    """Whether a changed path is a kernel, the driver that runs them, or a test that asks one."""
    if any(mark in path for mark in DEVICE_PATHS):
        return True
    if not (path.startswith("src/tests/") and path.endswith(".cs")):
        return False
    try:
        return DEVICE_TRAIT in (base / path).read_text(encoding="utf-8")
    except OSError:
        return False


def run(base, tier, take, name, upto, log):
    """One take of one tier, against the build already made for its configuration."""
    suite = TIERS[tier]
    judged = " judged" if tier == "e2e" and os.environ.get("TRAFFIC_E2E_JUDGE") != "off" else ""
    cut = f", P0–P{upto}" if upto < LAST_RUNG else ""
    print(f"=== {tier} ({take.config}{judged}{cut}) — {take.why or suite.what}")

    blame = ["--blame-crash", "--blame-crash-dump-type", "mini", "--results-directory", BLAME] if take.blames else []
    # The run settings go last: everything after their `--` is one.
    argv = ["test", TESTS, "-c", take.config, "--no-build",
            "--filter", filter_for(suite.trait, take.narrow, name, upto), *blame, *light_test()]
    tested = _sim.dotnet(base, *argv)
    log.append(f"=== {tier} ({take.config})\n" + tested.stdout + tested.stderr)
    _sim.show(tested.stdout, r"error CS|^\s+(Failed|Error Message)|Failed!|Passed!|No test matches|Test Run Aborted")
    if "Test host process crashed" in tested.stdout + tested.stderr:
        print(f"=== the test host crashed: its dump and the test it was running are under {BLAME}/")
    # `dotnet test` exits 0 when the filter matched nothing at all, which reads as a tier that passed
    # and is the one outcome this tool must never report as green. The caller decides what it means:
    # one take of a tier missing a name is ordinary, every take missing it is a name that names
    # nothing. Unnarrowed it is a tier no test class carries, and that is a failure here.
    if "No test matches" in tested.stdout:
        return MISSED if name else 1
    return 0 if tested.code == 0 else 1


def filter_for(trait: str, narrow: str, name: str | None, upto: int) -> str:
    """
    The tier, cut down to the take, cut again at a rung when `--upto` was given, and narrowed by
    substring when `--name` was.

    The name is matched against the display name as well as the fully qualified one, because a
    `[Theory]` case is one method with its argument in the display name only — without it `--name`
    can select a class but never one of the cases inside it, which for the visual tier is every test
    it has.

    **The rung is spelled out and not compared**, because `dotnet test`'s filter has no ordering: the
    only thing it can say about a trait is that it equals a value, so `P0–P3` is four alternatives.
    """
    named = f"(FullyQualifiedName~{name}|DisplayName~{name})" if name else ""
    rungs = "|".join(f"P=P{rung}" for rung in range(upto + 1)) if upto < LAST_RUNG else ""
    return "&".join(part for part in (f"Tier={trait}", narrow, f"({rungs})" if rungs else "", named) if part)


def pick(tiers: list[str]) -> list[str]:
    """The tiers to run, de-duplicated and back in cheapest-first order."""
    wanted = set()
    for tier in tiers or ["unit"]:
        wanted.update(ALL if tier == "all" else [tier])
    return [tier for tier in TIERS if tier in wanted]


def ordered_configs(chosen: list[str]) -> list[str]:
    """The configurations the chosen tiers need, each built once, cheapest tier's first."""
    configs = []
    for tier in chosen:
        for take in TIERS[tier].takes:
            if take.config not in configs:
                configs.append(take.config)
    return configs


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq tests",
        description="Run one or more tiers of the test suite.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("tiers", nargs="*", default=["unit"],
                        choices=[*TIERS, "all"],
                        help="which tiers to run (src/tests/Tier.cs); 'all' is " + ", ".join(ALL))
    parser.add_argument("--changed", action="store_true",
                        help="add the tiers the working tree's own changes can have moved")
    parser.add_argument("--judge", action="store_true",
                        help="e2e only: have an agent judge the frames. Costs money and about half an hour")
    parser.add_argument("--upto", type=int, default=LAST_RUNG, choices=range(LAST_RUNG + 1),
                        metavar="RUNG",
                        help="run only P0 through this rung (src/tests/Priority.cs): 0 is the engine's own "
                             "integrity, 9 is where a lamp is drawn")
    parser.add_argument("--name", default=None,
                        help="narrow a tier to the tests whose name contains this — a class, a method, "
                             "or one case of a [Theory] by its argument (a visual scenario's own name)")
    parser.add_argument("--root", default=None, metavar="DIR",
                        help="test a copy of the tree in this folder under .tmp/ instead of this one")
    parser.add_argument("--full", action="store_true",
                        help="take every core at normal priority, rather than the light run's half at a low one")
    return parser.parse_args()


if __name__ == "__main__":
    cli.entry(main)
