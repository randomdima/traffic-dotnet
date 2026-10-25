"""
Profile a running town with perf once its map is loaded, and say where the CPU went.

A record builds Release, opens the map in a window, waits until the frame loop has been JIT'd (the
runtime's perf map names `Game::Step`), then samples every thread for the asked seconds. The stacks
are folded to `.tmp/prof/<map>-<stamp>.folded` (flamegraph/speedscope-readable, frames already
shortened to `Type::Method`) beside the run's own output, so a second look is `--last` (or `--run`
for an older one) and costs no run. `--focus` drills into one symbol: who calls it and what it
spends its time in.

`--annotate` goes below the symbol, for a method that is hot in itself: the runtime lists that
method's JIT'd code into `.asm` (`DOTNET_JitDisasm`; the file wants an absolute path, and on the
game's own stdout it interleaves with the game's lines), every sample's leaf offset is kept in
`.leaves`, and the samples are binned by the listing's instruction groups - labels are all a
Release listing carries offsets for. The method is named when recording; `--last` reads it back.
A generic method's instantiations share one perf-map name, so a sample's version is where its code
starts, and its listing is the one of that code's size in the perf map lines kept as `.map`.

`--bench NAME` profiles one of the engine's headless probes for that map instead of a window, sampled
from just after launch to its exit: `--bench age` is a town's tick from its first minute to its sixth
with no frame under it, `--bench load` (or `--load`) is its opening, broken down by the stages it prints.
A figure is then a share of the whole run and `cores busy` says how parallel it ran.

`--root` profiles a town this tree does not ship: a folder under `.tmp/` holding its own `towns/`
briefs, an `assets/` (a link to the tree's is enough) and a copy of a Release build's output in
`bin/`. The binary finds its root by walking up from itself, so the copy reads that folder's briefs
and nothing is built. Recordings still land in `.tmp/prof/`.

`--memory` watches the same run for memory - counters, the resident set by what backs it and a heap dump
by type for a window, the resident set over time for a probe - and `--roots`/`--owners` read a kept dump
back for who holds what, `--buffers` every GPU buffer and the field holding it; all of it is `_heap.py`'s. `--counters` is a window's counters alone, no dump. A
window's snapshots are taken after perf stops and inside an exit grace widened for them. The tools are
`~/.dotnet/tools` globals.

`--events` records a group of the CPU's own counters instead of its cycles alone - `cycles,instructions,
L1-dcache-load-misses,cache-misses` is the question "is this slow because it waits on memory" - each folded by
the count it carries into `<stem>.<event>.folded`. The report then adds, self time by method over the whole run
or `--focus`'s subtree, the second event per the first (instructions per cycle) and every further one per
thousand of the second (misses per thousand instructions). The plain `.folded` stays a count of the first
event's samples, so every other figure reads as it always did.

`--gpu` samples the same window (or probe, launch to exit) for what the process asks of the GPU - each engine's
busy share and its resident VRAM/GTT off the kernel's DRM fdinfo, beside the card's own busy and memory - into
`.gpu`; it is `_gpu.py`'s. A window's town is ticked on the device, as the game's is by default; `--managed-world` steps it in managed code. `--frame`
opens the status panel's frame read-out so the exit's budget line says where the CPU frame went. `--no-cpu` runs
no perf at all — what is left is the read-out and `--gpu`/`--memory` — for a GPU question, or a machine whose
`perf_event_paranoid` refuses perf ("Failure to open any events").

perf walks JIT'd stacks by frame pointers, so an inlined method is missing and a frameless one hides its caller;
the report puts both back from the source's own calls (`_nest.py`), `(Type::Method)` marking an inlined one, and
`--raw` reads the stacks as walked. The runtime's own sampler (`dotnet-trace`'s thread time) walks them whole but
only stops a thread at a safe point, which moved a third of the car tick into the turns on O30: its stacks are no
fix for these. `DOTNET_JitFramed` changes nothing in a Release runtime.

The runtime is started with three knobs perf needs: a perf map (JIT'd code has no symbols
otherwise), W^X off (double-mapped code pages hide the map's addresses) and ReadyToRun off (so
framework code is JIT'd and named too). Samples are user-space only at this machine's
`perf_event_paranoid`, so a figure is CPU spent, never time blocked. `ms/frame` is that CPU over the
game's own last-window fps times the seconds sampled - a rate, not a count of the frames themselves. That
window is the game's last half second, the exit grace after the samples end; under `--memory` that is two
minutes on, past whatever the town did since, so a memory run quotes shares and no `ms/frame`.
"""

import argparse
import bisect
import os
import re
import time
from collections import Counter, defaultdict
from pathlib import Path

import _gpu
import _heap
import _nest
import _sim
from common import cli, paths, proc

#: Build, a city's lay (most of a minute), warmup, the sample window at its ceiling, and the fold.
TIMEOUT_S = 900

RUN =re.compile(r"^[A-Za-z0-9_-]{1,64}$")
FOCUS = re.compile(r"^[A-Za-z0-9_.:+`<>]{2,80}$")
METHOD = re.compile(r"^(?:[A-Za-z_][A-Za-z0-9_+]*::)?[A-Za-z_][A-Za-z0-9_]*$")
ENGINE = "traffic-dotnet.csproj"
BINARY = Path("bin/Release/net11.0/traffic-dotnet")
OUT = Path(".tmp/prof")

RUNTIME_ENV = {"DOTNET_PerfMapEnabled": "3", "DOTNET_EnableWriteXorExecute": "0", "DOTNET_ReadyToRun": "0"}
LOOP_STARTED = re.compile(r"Game::Step\(")
LOAD_LIMIT_S = 300
EXIT_GRACE_S = 5
#: A heap dump of a city at thirty times Odesa's area: GBs written, then listed by SOS and its pages read.
MEMORY_GRACE_S = 120
MAX_SECONDS = 300

#: The method's own assembly tag, `[traffic-dotnet] `; a return type's tag has no space after it.
ASSEMBLY = re.compile(r"\[[^\]\s]+\] ")
GENERIC = re.compile(r"\[[^\]]*\]")
FPS = re.compile(r"(\d+) fps in the last window")
META = re.compile(r"^prof: map=(\S+) seconds=(\d+) hz=(\d+)(?: grace=(\d+))?$")
EVENTS = re.compile(r"^prof: events=(\S+)$")
EVENT = re.compile(r"^[A-Za-z][A-Za-z0-9_.-]*(?::[a-z]+)?$")
LISTING = re.compile(r"^; Assembly listing for method (?P<type>[^:(]+):(?P<method>[^(]+)\(.*\((?P<tier>[^()]+)\)$")
GROUP = re.compile(r"^(G_M\d+_IG\d+):\s+;; offset=0x([0-9A-Fa-f]+)")
LISTING_END = re.compile(r"^; Total bytes of code (\d+)")
DATA = re.compile(r"^RWD\d+")


def main():
    a = parse_args()
    base = _sim.root()
    if a.run:
        folded = named(base, a.run)
    elif a.last:
        folded = newest(base)
    else:
        folded = record(base, a)
    stacks, meta = load(folded)
    print(f"=== {_sim.rel(base, folded)}")
    if not stacks:
        # A run with no CPU samples (`--no-cpu`): the game's own read-out, its memory and the GPU's.
        for line in meta["lines"]:
            print(f"  {line}")
        _heap.report(folded, a)
        _gpu.report(folded)
        return 0
    if not a.raw:
        stacks, filled = _nest.repair(stacks, base / "src")
        total = sum(n for _, n in stacks) or 1
        print(f"  {100 * filled / total:.1f}% of samples had frames put back from the source's calls; "
              "(Type::Method) is one perf never saw, so inlined")
    report(folded, stacks, meta, a)
    if a.roots:
        _heap.roots(base, folded, a)
    elif a.owners:
        _heap.owners(base, folded, a)
    elif a.buffers:
        _heap.buffers(base, folded, a)
    elif not (a.focus or a.annotate):
        _heap.report(folded, a)
        _gpu.report(folded)
    return 0


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq prof",
        description="Profile a running town with perf once its map is loaded, and say where the CPU went.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("--map", type=_sim.map_name, default="Odesa", help="the map to open")
    parser.add_argument("--seconds", type=window, default=60, metavar="INT",
                        help=f"sampled seconds after the loop starts (1..{MAX_SECONDS})")
    parser.add_argument("--warmup", type=cli.counting, default=3, metavar="INT",
                        help="seconds between the loop starting and sampling, for tier-1 JIT to settle")
    parser.add_argument("--hz", type=cli.counting, default=999, metavar="INT", help="samples per second per thread")
    parser.add_argument("--fullscreen", action="store_true", help="open fullscreen rather than in a window")
    parser.add_argument("--memory", action="store_true",
                        help="also record memory: for a window the GC and process counters over it, the resident "
                             "set by what backs it and a heap dump by type at its end; for a probe the resident set "
                             "over the run, against the probe's own stages")
    parser.add_argument("--counters", action="store_true",
                        help="of --memory, only the runtime's GC and process counters over a window: whether it "
                             "allocates, without the minutes a heap dump of a city takes. The listener allocates "
                             "~32 kB/s in the process itself, whatever the frame rate: read a floor as the instrument")
    parser.add_argument("--gpu", action="store_true",
                        help="also sample the GPU: this process's engine busy time and resident VRAM/GTT off DRM "
                             "fdinfo, and the card's own busy and memory, over the window or the probe")
    parser.add_argument("--no-cpu", action="store_true",
                        help="sample no CPU stacks (no perf): the window's own read-out, and with --gpu or --memory "
                             "those - for a machine whose perf_event_paranoid refuses perf, or a GPU question")
    parser.add_argument("--managed-world", action="store_true",
                        help="step the window's world in managed code rather than on the device (the game's "
                             "--managed-world); not with --bench, which always steps the managed world, and whose "
                             "deviceworld probe soaks the device world headless")
    parser.add_argument("--frame", action="store_true",
                        help="open the status panel's frame read-out (--ui frame), so the exit's budget says "
                             "where the CPU frame went: sim, sprites, interface, submit, and the kB a frame wrote "
                             "into memory the device reads")
    parser.add_argument("--view", type=float, default=None, metavar="METRES",
                        help="open the window on this span in metres (the game's --view) - a whole town's framing "
                             "is its own extent")
    parser.add_argument("--present", choices=("fifo", "mailbox", "immediate"), default=None,
                        help="the window's present mode (the game's --present): mailbox or immediate lift the "
                             "display's refresh cap, so the frame rate says what the CPU frame allows")
    parser.add_argument("--roots", type=type_name, default=None, metavar="TYPE",
                        help="with --last or --run of a window's --memory recording: who holds that exact type's "
                             "biggest objects (System.Int32[]), off the kept dump - --limit objects, --depth holders")
    parser.add_argument("--owners", action="store_true",
                        help="with --last or --run of a window's --memory recording: every object of 100 kB or more "
                             "totalled by who holds it - --depth holders a row, --limit rows")
    parser.add_argument("--buffers", action="store_true",
                        help="with --last or --run of a window's --memory recording: every GPU buffer the heap "
                             "holds, its bytes, device-local or mapped, and the field holding it, totalled by "
                             "field - --limit rows")
    parser.add_argument("--events", type=event_list, default=None, metavar="LIST",
                        help="record these CPU counters as one group instead of cycles alone "
                             "(cycles,instructions,L1-dcache-load-misses,cache-misses), and report the second per the "
                             "first and every further one per thousand of the second, by method")
    parser.add_argument("--no-build", action="store_true", help="run the Release binary as it stands")
    parser.add_argument("--bench", type=_sim.map_name, default=None, metavar="NAME",
                        help="profile this headless probe for the map, launch to exit (age: the tick of a "
                             "town to its sixth minute; load: its opening) rather than a window's running "
                             "loop; --seconds, --warmup and --fullscreen are not read")
    parser.add_argument("--load", action="store_true", help="the same as --bench load")
    parser.add_argument("--root", default=None, metavar="DIR",
                        help="a folder under .tmp/ with its own towns/, assets/ and a Release build copied "
                             "into bin/, whose binary is run instead of this tree's; nothing is built")
    parser.add_argument("--last", action="store_true", help="read the newest recording instead of taking one")
    parser.add_argument("--run", type=run_name, default=None, metavar="STEM",
                        help=f"read this recording instead of taking one - its file stem in {OUT}/, "
                             "as the === line prints it (Odesa-20260927-184456), for an A/B against --last")
    parser.add_argument("--focus", type=focus_name, default=None,
                        help="substring of a Type::Method to drill into: its callers and callee tree")
    parser.add_argument("--annotate", type=method_name, default=None,
                        help="Method or Type::Method whose own samples are placed on its JIT'd code; "
                             "name it when recording, since the listing is taken then")
    parser.add_argument("--raw", action="store_true",
                        help="read the stacks as perf walked them, without the frames an inlined or frameless "
                             "method dropped put back from the source's calls (_nest.py)")
    parser.add_argument("--depth", type=cli.counting, default=4, metavar="INT", help="levels of a focus tree")
    parser.add_argument("--min", type=float, default=0.5, metavar="PCT",
                        help="smallest share of all samples a row is printed for")
    parser.add_argument("--limit", type=cli.counting, default=30, metavar="INT", help="rows per top list (0 = all)")
    a = parser.parse_args()
    if a.load:
        a.bench = "load"
    if a.bench and (a.managed_world or a.frame or a.counters):
        parser.error("--managed-world, --frame and --counters are a window's: --bench deviceworld is the device world's probe")
    return a


def focus_name(value: str) -> str:
    if not FOCUS.match(value):
        raise argparse.ArgumentTypeError(f"{value!r} is not a symbol fragment")
    return value


def type_name(value: str) -> str:
    if not _heap.TYPE.match(value):
        raise argparse.ArgumentTypeError(f"{value!r} is not a type name")
    return value


def run_name(value: str) -> str:
    if not RUN.match(value):
        raise argparse.ArgumentTypeError(f"{value!r} is not a recording's file stem")
    return value


def method_name(value: str) -> str:
    if not METHOD.match(value):
        raise argparse.ArgumentTypeError(f"{value!r} is not Method or Type::Method")
    return value


def event_list(value: str) -> list[str]:
    events = value.split(",")
    if len(events) < 2 or not all(EVENT.match(e) for e in events):
        raise argparse.ArgumentTypeError(f"{value!r} is not two or more perf events, comma-separated")
    return events


def sampling(a) -> list:
    """What perf samples on: the asked counters as one group, or cycles at the asked rate."""
    return ["-e", "{" + ",".join(a.events) + "}", "-F", a.hz] if a.events else ["-F", a.hz]


def window(value: str) -> int:
    n = cli.counting(value)
    if not 1 <= n <= MAX_SECONDS:
        raise argparse.ArgumentTypeError(f"{n} is outside 1..{MAX_SECONDS}")
    return n


def record(base: Path, a) -> Path:
    binary = base / BINARY
    if a.root:
        binary = paths.resolve_in_root(base / a.root, base / ".tmp") / "bin" / BINARY.name
        if not binary.is_file():
            cli.fail(f"no {BINARY.name} in {_sim.rel(base, binary.parent)}: copy {BINARY.parent}/ there first")
    elif not a.no_build:
        built = _sim.dotnet(base, "build", "-c", "Release", ENGINE)
        if built.code != 0:
            _sim.write_log(base, "prof-build", built.stdout + built.stderr)
            cli.fail("Release build failed")

    out = base / OUT
    out.mkdir(parents=True, exist_ok=True)
    kind = f"-{a.bench}" if a.bench else "-managed" if a.managed_world else ""
    stem = out / f"{a.map}{kind}-{time.strftime('%Y%m%d-%H%M%S')}"
    log = stem.with_suffix(".log")
    data = stem.with_suffix(".data")
    if a.bench:
        return record_bench(base, binary, stem, a)

    grace = MEMORY_GRACE_S if a.memory else EXIT_GRACE_S
    log.write_text(f"prof: map={a.map} seconds={a.seconds} hz={a.hz} grace={grace}\n{events_line(a)}", encoding="utf-8")

    argv = [binary, "--map", a.map, "--seconds", str(a.warmup + a.seconds + grace)]
    if not a.fullscreen:
        argv.append("--windowed")
    if a.managed_world:
        argv.append("--managed-world")
    if a.frame:
        argv += ["--ui", "frame"]
    if a.view is not None:
        argv += ["--view", str(a.view)]
    if a.present:
        argv += ["--present", a.present]
    os.environ.update(RUNTIME_ENV)
    annotating(stem, a)
    started = time.monotonic()
    pid = proc.spawn(argv, log=log, cwd=base)
    perf_map = Path(f"/tmp/perf-{pid}.map")
    try:
        wait_for_loop(pid, perf_map)
        print(f"loop started {time.monotonic() - started:.1f} s after launch; sampling {a.seconds} s")
        time.sleep(a.warmup)
        counters = _heap.watch_counters(pid, stem, a.seconds) if a.memory or a.counters else None
        gpu = _gpu.watch(pid, stem, a.seconds) if a.gpu else None
        if a.no_cpu:
            time.sleep(a.seconds)
        else:
            proc.run(["perf", "record", *sampling(a), "-g", "-p", pid, "-o", data, "--", "sleep", a.seconds], cwd=base)
        if gpu:
            gpu.join()
        if counters:
            reap(counters)
        if a.memory:
            _heap.snapshot_maps(pid, stem)
            _heap.snapshot_heap(base, pid, stem)
        peak = reap(pid)
        with log.open("a", encoding="utf-8") as f:
            f.write(peak)
        if a.annotate:
            keep_map(perf_map, stem.with_suffix(".map"), a.annotate)
        folded = stem.with_suffix(".folded")
        if a.no_cpu:
            folded.write_text("", encoding="utf-8")
        else:
            fold(base, data, folded, a.events)
    finally:
        perf_map.unlink(missing_ok=True)
        data.unlink(missing_ok=True)
    return folded


def annotating(stem: Path, a):
    """The runtime told to list the named method's JIT'd code beside the recording, where one is named."""
    if not a.annotate:
        return
    # The runtime matches a type against its full name, so a bare one is found through a wildcard.
    os.environ["DOTNET_JitDisasm"] = f"*{a.annotate.replace('::', ':')}" if "::" in a.annotate else a.annotate
    os.environ["DOTNET_JitStdOutFile"] = str(stem.with_suffix(".asm").resolve())


def record_bench(base: Path, binary: Path, stem: Path, a) -> Path:
    """
    One probe for the map, headless and whole: `--bench NAME` sampled until it exits. perf attaches to the
    pid rather than starting the binary, so the pid - and with it the perf map to clean up - is known;
    what runs before it attaches is the runtime starting, a few milliseconds.
    """
    log, data = stem.with_suffix(".log"), stem.with_suffix(".data")
    log.write_text("", encoding="utf-8")
    os.environ.update(RUNTIME_ENV)
    if a.bench in ("load", "deviceload"):
        # Profile-guided inlining folds a parallel job's delegate into the pool's worker, and a load is mostly
        # such jobs: without it each job keeps a frame of its own and says whose time it was. A tick is not,
        # and is profiled with the inlining it runs with.
        os.environ["DOTNET_TieredPGO"] = "0"
    annotating(stem, a)
    started = time.monotonic()
    pid = proc.spawn([binary, "--bench", a.bench, "--map", a.map], log=log, cwd=base)
    perf_map = Path(f"/tmp/perf-{pid}.map")
    gpu = _gpu.watch(pid, stem, None) if a.gpu else None
    try:
        if a.memory:
            # perf in the background, so the resident set can be read while the probe runs.
            sampler = proc.spawn(["perf", "record", *sampling(a), "-g", "-p", pid, "-o", data], cwd=base)
            peak = _heap.watch_rss(pid, log, stem)
            reap(sampler)
        else:
            proc.run(["perf", "record", *sampling(a), "-g", "-p", pid, "-o", data], cwd=base, check=False)
            peak = reap(pid)
        if gpu:
            gpu.join()
        if a.annotate:
            keep_map(perf_map, stem.with_suffix(".map"), a.annotate)
        seconds = max(1, round(time.monotonic() - started))
        with log.open("a", encoding="utf-8") as f:
            f.write(f"{peak}prof: map={a.map} seconds={seconds} hz={a.hz}\n{events_line(a)}")
        print(log.read_text(encoding="utf-8", errors="replace").strip())
        folded = stem.with_suffix(".folded")
        fold(base, data, folded, a.events)
    finally:
        perf_map.unlink(missing_ok=True)
        data.unlink(missing_ok=True)
    return folded


def wait_for_loop(pid: int, perf_map: Path):
    deadline = time.monotonic() + LOAD_LIMIT_S
    while time.monotonic() < deadline:
        if os.waitpid(pid, os.WNOHANG) != (0, 0):
            cli.fail("the game exited before its frame loop started; see its log in .tmp/prof/")
        if perf_map.is_file() and LOOP_STARTED.search(perf_map.read_text(errors="replace")):
            return
        time.sleep(0.25)
    cli.fail(f"no Game::Step in {perf_map} after {LOAD_LIMIT_S} s")


def reap(pid: int) -> str:
    """
    Wait for the game's own exit, which prints its frame budget, and say its peak resident set - the load's
    high-water mark as much as the loop's. `subprocess` may have reaped it already, and then there is none.
    """
    try:
        _, _, usage = os.wait4(pid, 0)
    except ChildProcessError:
        return ""
    return f"peak resident {usage.ru_maxrss / 1024:,.0f} MiB\n"  # ru_maxrss is KiB on Linux


def events_line(a) -> str:
    return f"prof: events={','.join(a.events)}\n" if a.events else ""


def fold(base: Path, data: Path, folded: Path, events: list[str] | None = None):
    """
    perf's leaf-first samples as root-first `thread;frame;... count` lines, host frames above Main cut,
    and every sample's leaf as `count<TAB>offset<TAB>code start<TAB>full symbol` in `.leaves`. The start
    (ip less offset) is what tells a generic method's instantiations apart: the perf map gives them all
    the one name. With `events`, the first event's samples are the plain fold, and each event's counts
    are folded into `<stem>.<event>.folded` as well.
    """
    fields = "comm,tid,period,event,ip,sym,symoff" if events else "comm,tid,ip,sym,symoff"
    script = proc.run(["perf", "script", "-i", data, "-F", fields, "--no-inline"], cwd=base)
    stacks, leaves = Counter(), Counter()
    counted = {event: Counter() for event in events or []}
    thread, event, period, frames = None, None, 0, []
    for line in script.stdout.splitlines() + [""]:
        if not line.strip():
            if thread is not None:
                stack = ";".join([thread] + trimmed([sym for sym, _, _ in frames[::-1]]))
                if event in counted:
                    counted[event][stack] += period
                if not events or event == events[0]:
                    if frames:
                        leaves[frames[0]] += 1
                    stacks[stack] += 1
            thread, event, period, frames = None, None, 0, []
        elif not line[0].isspace():
            if events:
                # `comm tid period event:`, the event perhaps carrying a `:u` modifier.
                head, period_text, event_text = line.rsplit(None, 2)
                thread = head.rsplit(None, 1)[0].strip().replace(";", ",")
                period = int(period_text) if period_text.isdigit() else 0
                event = next((e for e in events if event_text.rstrip(":").split(":")[0] == e.split(":")[0]), None)
            else:
                thread = line.rsplit(None, 1)[0].strip().replace(";", ",")
        else:
            parts = line.strip().split(None, 1)
            sym, off = split_offset(parts[1] if len(parts) > 1 else "[unknown]")
            ip = int(parts[0], 16) if re.fullmatch(r"[0-9a-fA-F]+", parts[0]) else -1
            frames.append((sym, off, ip - off if ip >= 0 and off >= 0 else -1))
    folded.write_text("".join(f"{stack} {n}\n" for stack, n in stacks.most_common()), encoding="utf-8")
    for event, counts in counted.items():
        event_folded(folded, event).write_text(
            "".join(f"{stack} {n}\n" for stack, n in counts.most_common()), encoding="utf-8")
    folded.with_suffix(".leaves").write_text(
        "".join(f"{n}\t{off}\t{start}\t{sym}\n" for (sym, off, start), n in leaves.most_common()), encoding="utf-8")


def event_folded(folded: Path, event: str) -> Path:
    return folded.with_suffix(f".{event.replace(':', '-')}.folded")


def keep_map(perf_map: Path, kept: Path, wanted: str):
    """The perf map's lines for the annotated method, `start size name`, so a version's size names its listing."""
    lines = []
    for line in perf_map.read_text(errors="replace").splitlines() if perf_map.is_file() else []:
        parts = line.split(None, 2)
        if len(parts) == 3 and names(short(parts[2]), wanted):
            lines.append(line)
    kept.write_text("".join(f"{line}\n" for line in lines), encoding="utf-8")


def split_offset(frame: str) -> tuple[str, int]:
    """`sym+0x1a3` as the symbol and 0x1a3; a frame perf gave no offset for keeps -1."""
    sym, sep, off = frame.rpartition("+0x")
    try:
        return (sym, int(off, 16)) if sep else (frame, -1)
    except ValueError:
        return frame, -1


def trimmed(root_first: list[str]) -> list[str]:
    first_own = next((i for i, f in enumerate(root_first) if "[traffic-dotnet]" in f), 0)
    return [short(f) for f in root_first[first_own:]]


def short(sym: str) -> str:
    """`instance float32 [asm] Ns.Type::Method(args)[Tier]` as `Type::Method`; native names lose only their args."""
    name = ASSEMBLY.split(sym.split("(", 1)[0])[-1]
    if "::" not in name:
        return name.replace(";", ",")
    kind, method = name.rsplit("::", 1)
    return f"{GENERIC.sub('', kind).rsplit('.', 1)[-1]}::{method}".replace(";", ",")


def newest(base: Path) -> Path:
    found = sorted((base / OUT).glob("*.folded"), key=lambda f: f.stat().st_mtime)
    if not found:
        cli.fail(f"no recording in {OUT}/ - run qq prof without --last first")
    return found[-1]


def named(base: Path, stem: str) -> Path:
    folded = base / OUT / f"{stem}.folded"
    if not folded.is_file():
        cli.fail(f"no recording {stem}.folded in {OUT}/")
    return folded


def load(folded: Path):
    stacks = []
    for line in folded.read_text(encoding="utf-8").splitlines():
        path, n = line.rsplit(" ", 1)
        stacks.append((path.split(";"), int(n)))
    meta = {"map": "?", "seconds": 0, "hz": 0, "grace": EXIT_GRACE_S, "fps": 0, "lines": [], "events": []}
    log = folded.with_suffix(".log")
    if log.is_file():
        for line in log.read_text(encoding="utf-8", errors="replace").splitlines():
            if m := META.match(line):
                meta.update(map=m[1], seconds=int(m[2]), hz=int(m[3]), grace=int(m[4] or EXIT_GRACE_S))
            elif m := EVENTS.match(line):
                meta["events"] = m[1].split(",")
            elif m := FPS.search(line):
                meta["fps"] = int(m[1])
                meta["lines"].append(line.strip())
            elif line.strip().startswith(("frame ", "cpu ", "written ", "peak resident ")):
                meta["lines"].append(line.strip())
    return stacks, meta


def report(folded: Path, stacks, meta, a):
    total = sum(n for _, n in stacks)
    if not total:
        cli.fail("the recording holds no samples")
    ms_per_sample = 1000 / meta["hz"] if meta["hz"] else 0
    # The read-out is the game's last half second at exit: only as near the samples as the default grace is
    # it a rate for them.
    read_late = meta["grace"] > EXIT_GRACE_S
    frames = 0 if read_late else meta["fps"] * meta["seconds"]

    def figure(n: int) -> str:
        pct = f"{100 * n / total:6.2f}%"
        return f"{pct} {n * ms_per_sample / frames:6.3f} ms/frame" if frames else pct

    cpu_s = total * ms_per_sample / 1000
    print(f"{meta['map']}: {total} samples over {meta['seconds']} s at {meta['hz']} Hz"
          f" = {cpu_s:.1f} CPU s ({cpu_s / meta['seconds']:.2f} cores busy)" if meta["seconds"] else f"{total} samples")
    for line in meta["lines"]:
        print(f"  {line}")
    if read_late and meta["fps"]:
        print(f"  (that fps is the exit's, {meta['grace']} s after the samples ended: no ms/frame is quoted)")

    rows = a.limit or None
    threads = Counter()
    for path, n in stacks:
        threads[path[0]] += n
    print("\nthreads")
    for name, n in threads.most_common(rows):
        if 100 * n / total >= a.min:
            print(f"  {figure(n)}  {name}")

    if meta["events"]:
        counters(folded, meta["events"], a)

    if a.focus or a.annotate:
        if a.focus:
            focus(stacks, total, figure, a)
        if a.annotate:
            annotate(folded, total, figure, a)
        return

    own, inclusive = Counter(), Counter()
    for path, n in stacks:
        own[path[-1]] += n
        for name in set(path[1:]):
            inclusive[name] += n
    print("\nself")
    for name, n in own.most_common(rows):
        print(f"  {figure(n)}  {name}")
    print("\ninclusive")
    for name, n in inclusive.most_common(rows):
        print(f"  {figure(n)}  {name}")


def counters(folded: Path, events: list[str], a):
    """
    Each method's own counts of every event recorded, over the whole run or the stacks through `--focus`: its share
    of the first, the second per the first, and every further one per thousand of the second.
    """
    own = {}
    for event in events:
        path = event_folded(folded, event)
        if not path.is_file():
            cli.fail(f"{path.name} is missing: record again with --events")
        counts = Counter()
        for line in path.read_text(encoding="utf-8").splitlines():
            stack, n = line.rsplit(" ", 1)
            frames = stack.split(";")
            if a.focus and not any(a.focus in f for f in frames[1:]):
                continue
            counts[frames[-1]] += int(n)
        own[event] = counts

    first, second, rest = events[0], events[1], events[2:]
    whole = {event: sum(counts.values()) for event, counts in own.items()}
    if not whole[first]:
        cli.fail(f"no {first} counted" + (f" under {a.focus!r}" if a.focus else ""))

    def ratios(get) -> str:
        cells = [f"{get(second) / get(first):6.2f}" if get(first) else "     -"]
        cells += [f"{1000 * get(e) / get(second):8.2f}" if get(second) else "       -" for e in rest]
        return " ".join(cells)

    scope = f"under {a.focus!r}" if a.focus else "the whole run"
    print(f"\ncounters, self by method, {scope}: {second} per {first}, then each per 1000 {second}")
    print(f"  {'share':>7} {'per':>6} " + " ".join(f"{e[:8]:>8}" for e in rest) + "  method")
    print(f"  {'all':>7} {ratios(lambda e: whole[e])}  ({whole[first]:,} {first})")
    for name, n in own[first].most_common(a.limit or None):
        share = 100 * n / whole[first]
        if share < a.min:
            break
        print(f"  {share:6.2f}% {ratios(lambda e, name=name: own[e][name])}  {name}")


def focus(stacks, total, figure, a):
    matched, callers, callees, own = Counter(), Counter(), tree(), 0
    for path, n in stacks:
        at = next((i for i, f in enumerate(path) if i and a.focus in f), None)
        if at is None:
            continue
        matched[path[at]] += n
        callers[tuple(path[max(1, at - a.depth):at][::-1])] += n
        if at == len(path) - 1:
            own += n
        node = callees
        for f in path[at + 1:at + 1 + a.depth]:
            node = node[f]
            node["$"] = node.get("$", 0) + n
    if not matched:
        cli.fail(f"no frame matches {a.focus!r}")
    whole = sum(matched.values())
    print(f"\nfocus {a.focus!r}: {figure(whole)} inclusive, {figure(own)} self")
    for name, n in matched.most_common():
        print(f"  {figure(n)}  {name}")

    print(f"\ncallers (nearest first, {a.depth} deep)")
    for chain, n in callers.most_common(a.limit or None):
        if 100 * n / total >= a.min:
            print(f"  {figure(n)}  {' < '.join(chain) or '<thread root>'}")

    print("\ncallees")
    walk(callees, total, figure, a.min, 1)


def annotate(folded: Path, total, figure, a):
    """The method's samples on its instruction groups, the hot ones printed with their code, in code order."""
    leaves = folded.with_suffix(".leaves")
    if not leaves.is_file():
        cli.fail(f"{leaves.name} is missing: the recording predates --annotate, so record again")
    # A version is a symbol and where its code starts: a generic method's instantiations share the symbol.
    versions, offsets = Counter(), defaultdict(Counter)
    for line in leaves.read_text(encoding="utf-8").splitlines():
        fields = line.split("\t", 3)
        n, off, start, sym = fields if len(fields) == 4 else (fields[0], fields[1], "-1", fields[2])
        if names(short(sym), a.annotate):
            versions[(sym, int(start))] += int(n)
            offsets[(sym, int(start))][int(off)] += int(n)
    if not versions:
        cli.fail(f"no sample landed in {a.annotate} itself")

    (sym, start), own = versions.most_common(1)[0]
    tag = sym.rsplit("[", 1)[-1].rstrip("]")
    # A recording from before the listing had a file of its own carries it in the log.
    asm = folded.with_suffix(".asm") if folded.with_suffix(".asm").is_file() else folded.with_suffix(".log")
    listing = listing_of(asm, a.annotate, tier_key(tag), code_size(folded.with_suffix(".map"), start))
    if listing is None:
        cli.fail(f"no {tag} listing of {a.annotate} in {asm.name}: record with --annotate={a.annotate}")
    groups, code_bytes, instance = listing

    starts = [begin for _, begin, _ in groups]
    per_group = Counter()
    for off, n in offsets[(sym, start)].items():
        per_group[max(0, bisect.bisect_right(starts, off) - 1)] += n
    others = ", ".join(f"[{s.rsplit('[', 1)[-1]} {100 * n / total:.2f}%" for (s, _), n in versions.most_common()[1:])
    print(f"\nannotate '{a.annotate}{instance}' [{tag}]: {figure(own)} self, {code_bytes} bytes in {len(groups)} groups"
          + (f"; other versions {others}" if others else ""))

    cold = 0
    for index, (label, start, code) in enumerate(groups):
        n = per_group[index]
        if 100 * n / total < a.min:
            cold += n
            continue
        print(f"\n  {figure(n)}  {100 * n / own:5.1f}% of the method  {label} @0x{start:04X}")
        for instruction in code:
            print(f"      {instruction}")
    print(f"\n  {figure(cold)}  in groups under --min={a.min}")


def names(short_name: str, wanted: str) -> bool:
    """Whether `Type::Method` is the method asked for, by `Method` alone or by `Type::Method`."""
    return short_name == wanted if "::" in wanted else short_name.rsplit("::", 1)[-1] == wanted


def tier_key(label: str) -> str:
    """A perf-map tag (`OptimizedTier1`) and a listing's tier (`Tier1`) read to the same key."""
    label = label.lower().replace(" ", "")
    if "osr" in label:
        return "osr"
    if "instrumented" in label:
        return "instrumented"
    if "tier1" in label or "optimized" in label or "fullopts" in label:
        return "tier1"
    return "tier0"


def code_size(kept_map: Path, start: int):
    """How long the code starting there is, by the perf map kept at record time; None where there is no telling."""
    for line in kept_map.read_text(encoding="utf-8").splitlines() if kept_map.is_file() and start >= 0 else []:
        begin, size, _ = line.split(None, 2)
        if int(begin, 16) == start:
            return int(size, 16)
    return None


def listing_of(asm: Path, wanted: str, key: str, size):
    """
    The method's listing at that tier in the run's `.asm` - of that code size where one is given, so a generic
    method's instantiation is the one sampled - as groups `(label, start, code)`, size, and instantiation.
    The last such listing wins: a method re-JIT'd at one tier is the later code.
    """
    found, groups, taking, instance = None, [], False, ""
    for line in asm.read_text(encoding="utf-8", errors="replace").splitlines() if asm.is_file() else []:
        if m := LISTING.match(line):
            instance = "".join(f"[{arg.rsplit('.', 1)[-1]}]" for arg in re.findall(r"\[([^\]]*)\]", m["method"]))
            name = f"{m['type'].rsplit('.', 1)[-1]}::{GENERIC.sub('', m['method'])}"
            taking, groups = names(name, wanted) and tier_key(m["tier"]) == key, []
        elif not taking:
            continue
        elif m := LISTING_END.match(line):
            if size is None or int(m[1]) == size:
                found = (groups, int(m[1]), instance)
            taking = False
        elif m := GROUP.match(line):
            groups.append((m[1], int(m[2], 16), []))
        elif groups and line.strip() and not line.lstrip().startswith((";", "align")) and not DATA.match(line):
            groups[-1][2].append(line.strip())
    return found


def tree():
    return defaultdict(tree)


def walk(node, total, figure, floor, depth):
    for name, child in sorted(((k, v) for k, v in node.items() if k != "$"), key=lambda kv: -kv[1]["$"]):
        n = child["$"]
        if 100 * n / total < floor:
            continue
        print(f"  {figure(n)}  {'  ' * (depth - 1)}{name}")
        walk(child, total, figure, floor, depth + 1)


if __name__ == "__main__":
    cli.entry(main)
