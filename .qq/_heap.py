"""
The memory half of `qq prof`: what a run's process holds, and what holds it.

`--memory` on a window's run leaves, beside the recording:
- `.counters.csv`, the runtime's GC and process meters once a second over the sample window (`dotnet-counters`);
- `.maps`, the resident set by what backs it, off `/proc/<pid>/smaps` at the window's end, so the GC's share of
  the working set and the driver's are told apart;
- `.dump` and `.heap`, the heap dumped (`dotnet-dump`) and SOS's `dumpheap -stat` over it: every type's count
  and total bytes, dead objects not yet collected included and the free space between objects as `Free`;
- `.pages` and `.resident`, which anonymous pages were present before the dump and so how much of each object
  of `OWNED_MIN_BYTES` or more is in memory - an array on pages nothing has written holds no RAM.

`--memory` on a probe (`--bench`) leaves `.rss`, the resident set every 50 ms from launch to exit with the moment
each of the probe's own stage lines landed in its log, so a load's peak has a stage.

Read back off a kept dump: `--roots TYPE`, who holds that type's biggest objects, and `--owners`, every object
of `OWNED_MIN_BYTES` or more totalled by who holds it. Owners are `gcroot`'s first chain to each object, cut to
`--depth` holders, so they name types and not fields. `--buffers` is the GPU's side: every `GpuBuffer` object's
size and whether it is mapped, named by the field of the engine's own object that holds it - two levels where the
holder is a `ResidentRows<T>` - since the memory under a buffer is the driver's and no heap figure shows it.
"""

import bisect
import os
import re
import struct
import time
from collections import Counter, defaultdict
from pathlib import Path

from common import cli, proc

DIAG_TOOLS = Path.home() / ".dotnet" / "tools"
COUNTERS = re.compile(r"^[^,]+,(?P<provider>[^,]+),(?P<name>.+),(?P<kind>[^,]+),(?P<value>[-0-9.Ee+]+)$")
#: `dumpheap -stat`: method table, count, total bytes, type - ascending by bytes.
HEAP_ROW = re.compile(r"^\s*(?P<mt>[0-9a-f]{8,16})\s+(?P<count>[\d,]+)\s+(?P<bytes>[\d,]+)\s+(?P<type>\S.*?)\s*$")
#: `dumpheap -min`/`-mt`: address, method table, size.
OBJECT_ROW = re.compile(r"^\s*(?P<address>[0-9a-f]{8,16})\s+(?P<mt>[0-9a-f]{8,16})\s+(?P<bytes>[\d,]+)\s*$")
CHAIN_LINK = re.compile(r"^\s*->\s+[0-9a-f]+\s+(\S.*?)\s*$")
GCROOT_END = re.compile(r"^Found \d+ unique roots")
TYPE = re.compile(r"^[A-Za-z0-9_.+`<>\[\], ]{2,160}$")
MAPPING = re.compile(r"^[0-9a-f]+-[0-9a-f]+ (?P<perms>\S{4}) \S+ \S+ \S+\s*(?P<path>.*)$")
#: A probe's top-level stage line, `  plan     658 ms ...`; a stage within one is indented further.
STAGE = re.compile(r"^  (?P<stage>\S[^\d]*?)\s+\d+ ms")

#: `dumpobjgcrefs`: one `GC Refs:` block an object, a row a reference field - name, offset, target, type.
REFS_HEAD = "GC Refs:"
REF_ROW = re.compile(r"^(?P<name>\S+)\s+0x[0-9a-f]+\s+(?P<target>[0-9a-f]{8,16})\s+\S")
#: `dq -c N`: an address and its qwords.
QWORDS = re.compile(r"^[0-9a-f]+:\s+(?P<words>[0-9a-f ]+?)\s*$")
BUFFER_TYPE = "TrafficSimulation.Runtime.GpuBuffer"
#: `dumpobj` crashes SOS on this runtime's dumps, so a buffer's two figures are read as words: the CLR lays a class's
#: references first, then its 8-byte primitives in order - the method table, every reference (counted off the first
#: buffer's `dumpobjgcrefs`), then `_mapped` and `SizeBytes` - so these are words past the references.
BUFFER_MAPPED_PAST_REFS, BUFFER_SIZE_PAST_REFS = 1, 2
#: Bigger than any device has: a buffer read this big means the layout above no longer holds.
BUFFER_SIZE_LIMIT = 1 << 40
#: Where a buffer is held from: the engine's own classes, not their arrays or the runtime's lists.
HOLDER_PREFIX = "TrafficSimulation."
RESIDENT_ROWS = "ResidentRows<"

#: The smallest object `--roots` asks the owner of.
ROOTS_MIN_BYTES = 100_000
#: The smallest object `--owners` totals; everything under it is one row.
OWNED_MIN_BYTES = 100_000
RSS_EVERY_S = 0.05


def watch_counters(pid: int, stem: Path, seconds: int) -> int:
    """`dotnet-counters` over the sample window, every runtime meter once a second; its pid, to wait on."""
    h, m, s = seconds // 3600, seconds // 60 % 60, seconds % 60
    return proc.spawn([DIAG_TOOLS / "dotnet-counters", "collect", "-p", pid, "--format", "csv",
                       "-o", stem.with_suffix(".counters.csv"), "--duration", f"00:{h:02}:{m:02}:{s:02}"],
                      log=stem.with_suffix(".counters.log"))


def snapshot_maps(pid: int, stem: Path):
    """
    The resident set by what backs it, as `kind<TAB>KiB` lines in `.maps`, and which pages of every anonymous
    read-write mapping are present, as `.pages`. The pages are read before the dump because the dump reads every
    page of the heap, and a page never written is then mapped to the zero page and reads as present.
    """
    kinds, kind, anonymous = Counter(), "?", []
    for line in Path(f"/proc/{pid}/smaps").read_text(errors="replace").splitlines():
        if m := MAPPING.match(line):
            kind = backing(m["perms"], m["path"].strip())
            if m["perms"].startswith("rw") and not m["path"].strip():
                start, end = line.split(None, 1)[0].split("-")
                anonymous.append((int(start, 16), int(end, 16)))
        elif line.startswith("Rss:"):
            kinds[kind] += int(line.split()[1])
    stem.with_suffix(".maps").write_text("".join(f"{k}\t{n}\n" for k, n in kinds.most_common()), encoding="utf-8")
    page = os.sysconf("SC_PAGE_SIZE")
    with open(f"/proc/{pid}/pagemap", "rb") as pagemap, stem.with_suffix(".pages").open("wb") as out:
        for start, end in anonymous:
            pagemap.seek(start // page * 8)
            entries = pagemap.read((end - start) // page * 8)
            present = bytes(entries[at] >> 7 for at in range(7, len(entries), 8))  # bit 63 of each entry
            out.write(struct.pack("<QQ", start, len(present)) + present)


def backing(perms: str, path: str) -> str:
    """What one mapping is, as far as its path and permissions say."""
    if path.startswith("/dev/dri"):
        return "GPU memory the driver maps (/dev/dri)"
    if ".so" in path:
        return "shared libraries"
    if path.endswith(".dll"):
        return "managed assemblies"
    if "memfd:" in path or "doublemapper" in path:
        return "double-mapped code (memfd)"
    if path == "[heap]":
        return "native heap (brk)"
    if path.startswith("[stack"):
        return "stacks"
    if not path:
        return "anonymous, executable: JIT'd code" if "x" in perms else "anonymous: GC heap and native allocations"
    return path if path.startswith("[") else "other mapped files"


def snapshot_heap(base: Path, pid: int, stem: Path):
    """
    The heap as `.dump`, its by-type table as `.heap`, and how much of every object of `OWNED_MIN_BYTES` or more
    is resident as `.resident` - read off `/proc/<pid>/pagemap` while the game is still in its grace. A steady
    state collects nothing, so the dump's addresses are still the objects' own when the pages are read.
    """
    dump = stem.with_suffix(".dump")
    proc.run([DIAG_TOOLS / "dotnet-dump", "collect", "-p", pid, "--type", "Heap", "-o", dump], cwd=base)
    text = analyze(base, dump, ["dumpheap -stat", f"dumpheap -min {OWNED_MIN_BYTES}"])
    stat_end = text.find("\n", text.find("\nTotal ") + 1) + 1
    stem.with_suffix(".heap").write_text(text[:stat_end], encoding="utf-8")
    listed = listed_objects(text[stat_end:])
    stem.with_suffix(".resident").write_text(
        "".join(f"{address}\t{n}\n" for address, n in residency(stem.with_suffix(".pages"), listed)), encoding="utf-8")


def residency(pages: Path, listed: list[tuple[int, str, str]]):
    """Each object's resident bytes: its pages `.pages` holds as present, clipped to the object."""
    page = os.sysconf("SC_PAGE_SIZE")
    data, at, mappings = pages.read_bytes(), 0, []
    while at < len(data):
        start, count = struct.unpack_from("<QQ", data, at)
        mappings.append((start, data[at + 16:at + 16 + count]))
        at += 16 + count
    starts = [start for start, _ in mappings]
    for size, address, _ in listed:
        begin = int(address, 16)
        present = 0
        for number in range(begin // page, (begin + size - 1) // page + 1):
            index = bisect.bisect_right(starts, number * page) - 1
            if index >= 0:
                start, bits = mappings[index]
                offset = number - start // page
                present += bits[offset] if offset < len(bits) else 0
        yield address, min(size, present * page)


def analyze(base: Path, dump: Path, commands: list[str]) -> str:
    """SOS commands run over a dump in one session, their output in order."""
    argv = [DIAG_TOOLS / "dotnet-dump", "analyze", dump]
    for command in commands + ["exit"]:
        argv += ["-c", command]
    return proc.run(argv, cwd=base).stdout


def watch_rss(pid: int, log: Path, stem: Path) -> str:
    """
    The resident set every `RSS_EVERY_S` until the process exits, and when each stage line reached its log, into
    `.rss`; its peak resident set as the line `reap` would have said. A probe's output is flushed line by line,
    so a stage line lands as the stage ends.
    """
    samples, stages, read = [], [], 0
    status = Path(f"/proc/{pid}/status")
    started = time.monotonic()
    while True:
        try:
            done, _, usage = os.wait4(pid, os.WNOHANG)
        except ChildProcessError:
            return ""
        at = time.monotonic() - started
        if done:
            break
        try:
            samples.append((at, next(int(line.split()[1]) for line in status.read_text().splitlines()
                                     if line.startswith("VmRSS:")) / 1024))
        except (OSError, StopIteration):
            pass
        text = log.read_text(encoding="utf-8", errors="replace")
        complete = text.rfind("\n") + 1
        for line in text[read:complete].splitlines():
            if m := STAGE.match(line):
                stages.append((m["stage"], at))
        read = max(read, complete)
        time.sleep(RSS_EVERY_S)
    stem.with_suffix(".rss").write_text(
        "".join(f"stage\t{name}\t{at:.3f}\n" for name, at in stages)
        + "".join(f"{at:.3f}\t{mib:.0f}\n" for at, mib in samples), encoding="utf-8")
    return f"peak resident {usage.ru_maxrss / 1024:,.0f} MiB\n"  # ru_maxrss is KiB on Linux


def report(folded: Path, a):
    """A `--memory` recording read back: counters, the resident set by backing, the heap by type, the load's curve."""
    csv, maps = folded.with_suffix(".counters.csv"), folded.with_suffix(".maps")
    heap, rss = folded.with_suffix(".heap"), folded.with_suffix(".rss")
    if csv.is_file():
        series = defaultdict(list)
        for line in csv.read_text(encoding="utf-8", errors="replace").splitlines()[1:]:
            if m := COUNTERS.match(line):
                series[(m["name"], m["kind"])].append(float(m["value"]))
        print("\ncounters (first / last / peak, or a rate's total)")
        for (name, kind), values in sorted(series.items()):
            if kind == "Rate":
                print(f"  {sum(values):>16,.2f} total        {name}")
            else:
                print(f"  {values[0]:>16,.0f} {values[-1]:>16,.0f} {max(values):>16,.0f}  {name}")
    if maps.is_file():
        rows = [line.split("\t") for line in maps.read_text(encoding="utf-8").splitlines()]
        print(f"\nresident at the window's end ({maps.name}): {sum(int(n) for _, n in rows) / 1024:,.0f} MiB; MiB")
        for kind, n in rows:
            if int(n) >= 1024:
                print(f"  {int(n) / 1024:10,.0f}  {kind}")
    if heap.is_file():
        rows = heap_rows(heap)
        total = sum(size for _, _, size, _ in rows)
        print(f"\nheap at the window's end ({heap.name}): {total / 1e6:,.1f} MB in "
              f"{sum(n for _, n, _, _ in rows):,} objects, Free included; MB, objects")
        for _, n, size, name in rows[:a.limit or None]:
            print(f"  {size / 1e6:10,.1f} {n:>9,}  {name}")
    if rss.is_file():
        report_rss(rss)


def report_rss(rss: Path):
    stages, samples = [], []
    for line in rss.read_text(encoding="utf-8").splitlines():
        fields = line.split("\t")
        if fields[0] == "stage":
            stages.append((fields[1], float(fields[2])))
        else:
            samples.append((float(fields[0]), float(fields[1])))
    if not samples:
        return
    peak_at, peak = max(samples, key=lambda s: s[1])

    def rss_at(t: float) -> float:
        return next((mib for at, mib in reversed(samples) if at <= t), 0.0)

    during = next((name for name, at in stages if at >= peak_at), "after the last stage")
    print(f"\nresident over the run ({rss.name}): peak {peak:,.0f} MiB at {peak_at:.1f} s, during {during}")
    begun = 0.0
    for name, at in stages:
        print(f"  {name:<12} {begun:6.1f}-{at:6.1f} s  most {max((m for t, m in samples if begun <= t <= at), default=0):7,.0f}"
              f" MiB, {rss_at(at):7,.0f} at its end")
        begun = at


def heap_rows(heap: Path) -> list[tuple[str, int, int, str]]:
    """`dumpheap -stat`'s rows as (method table, count, bytes, type), biggest first."""
    rows = []
    for line in heap.read_text(encoding="utf-8", errors="replace").splitlines():
        if m := HEAP_ROW.match(line):
            rows.append((m["mt"], int(m["count"].replace(",", "")), int(m["bytes"].replace(",", "")), m["type"]))
    return sorted(rows, key=lambda row: -row[2])


def kept(folded: Path) -> tuple[Path, Path]:
    dump, heap = folded.with_suffix(".dump"), folded.with_suffix(".heap")
    if not dump.is_file() or not heap.is_file():
        cli.fail(f"no {dump.name} beside the recording - take one with --memory")
    return dump, heap


def roots(base: Path, folded: Path, a):
    """Who holds the biggest objects of one type: each object's size and the chain `gcroot` walks to it."""
    dump, heap = kept(folded)
    tables = [mt for mt, _, _, name in heap_rows(heap) if name == a.roots]
    if not tables:
        cli.fail(f"no type {a.roots!r} in {heap.name}")
    found = objects(base, dump, [f"dumpheap -mt {mt} -min {ROOTS_MIN_BYTES}" for mt in tables])[:a.limit or None]
    print(f"\nwho holds {a.roots} ({len(found)} of {ROOTS_MIN_BYTES:,} bytes or more, biggest first)")
    for (size, address, _), chain in zip(found, chains(analyze(base, dump, [f"gcroot {address}" for _, address, _ in found]))):
        print(f"  {size / 1e6:10,.1f} MB  {holders(chain, a.depth)}")


def owners(base: Path, folded: Path, a):
    """Every object of `OWNED_MIN_BYTES` or more, totalled by who holds it, and the types each holder's bytes are."""
    dump, heap = kept(folded)
    rows = heap_rows(heap)
    names = {mt: name for mt, _, _, name in rows}
    found = objects(base, dump, [f"dumpheap -min {OWNED_MIN_BYTES}"])
    paged = folded.with_suffix(".resident")
    resident = dict(line.split("\t") for line in paged.read_text(encoding="utf-8").splitlines()) if paged.is_file() else {}
    held, count, kept_in, kinds = Counter(), Counter(), Counter(), defaultdict(Counter)
    for (size, address, mt), chain in zip(found, chains(analyze(base, dump, [f"gcroot {address}" for _, address, _ in found]))):
        owner = holders(chain, a.depth)
        held[owner] += size
        count[owner] += 1
        kept_in[owner] += int(resident.get(address, size))
        kinds[owner][short_type(names.get(mt, mt))] += size
    total = sum(size for _, _, size, _ in rows)
    free = sum(size for _, _, size, name in rows if name == "Free")
    under = total - sum(held.values()) - free
    order = kept_in if resident else held
    print(f"\nwho holds the heap ({heap.name}): {total / 1e6:,.1f} MB, {sum(held.values()) / 1e6:,.1f} of it in "
          f"{len(found):,} objects of {OWNED_MIN_BYTES:,} bytes or more"
          + (f", {sum(kept_in.values()) / 1e6:,.1f} of that resident" if resident else ", residency not recorded")
          + "; MB held, MB resident, objects, holder, what")
    for owner, _ in order.most_common(a.limit or None):
        what = ", ".join(f"{name} {n / 1e6:,.1f}" for name, n in kinds[owner].most_common(3))
        shown = f"{kept_in[owner] / 1e6:10,.1f}" if resident else f"{'-':>10}"
        print(f"  {held[owner] / 1e6:10,.1f}{shown} {count[owner]:>6,}  {owner}  [{what}]")
    print(f"  {under / 1e6:10,.1f}         objects under {OWNED_MIN_BYTES:,} bytes")
    print(f"  {free / 1e6:10,.1f}         Free")


def buffers(base: Path, folded: Path, a):
    """Every `GpuBuffer` the heap holds, totalled by the field holding it: bytes, and device-local or mapped."""
    dump, heap = kept(folded)
    names = {int(mt, 16): name for mt, _, _, name in heap_rows(heap)}
    wanted = []
    for line in analyze(base, dump, [f"dumpheap -type {HOLDER_PREFIX}"]).splitlines():
        if (m := OBJECT_ROW.match(line)) and (name := names.get(int(m["mt"], 16), "")).startswith(HOLDER_PREFIX) \
                and not name.endswith("]"):
            wanted.append((int(m["address"], 16), int(m["mt"], 16)))
    found = [address for address, mt in wanted if names[mt] == BUFFER_TYPE]
    if not found:
        cli.fail(f"no {BUFFER_TYPE} in {dump.name}")
    keepers = [(address, mt) for address, mt in wanted if names[mt] != BUFFER_TYPE]
    refs_of_one = len(object_refs(analyze(base, dump, [f"dumpobjgcrefs {found[0]:x}"]), found[:1])[0])
    mapped_word, size_word = refs_of_one + BUFFER_MAPPED_PAST_REFS, refs_of_one + BUFFER_SIZE_PAST_REFS
    words = buffer_words(
        analyze(base, dump, [f"dq -c {size_word + 1} {address:x}" for address in found]), found, mapped_word, size_word)
    refs = object_refs(analyze(base, dump, [f"dumpobjgcrefs {address:x}" for address, _ in keepers]), keepers)

    held_by = defaultdict(list)
    for (address, mt), own in zip(keepers, refs):
        for field, target in own:
            held_by[target].append((address, short_type(names[mt]), field))

    def holder(address: int) -> str:
        via = held_by.get(address)
        if not via:
            return "(no field of the engine's: a list, an array or a struct)"
        at, kind, field = via[0]
        # A helper type (ResidentRows<T>, a nested Sent<T>) says less than the field its owner keeps it in.
        if kind.startswith(RESIDENT_ROWS) or "+" in kind:
            if parent := held_by.get(at):
                _, owner, owned = parent[0]
                return f"{owner}.{owned} -> {kind.split('<', 1)[0]}.{field}"
        return f"{kind}.{field}"

    # What no field of the engine's holds is named by its root's chain instead, where it is big enough to matter.
    unnamed = [address for address, (_, size) in zip(found, words) if address not in held_by and size >= ROOTS_MIN_BYTES]
    rooted = dict(zip(unnamed, chains(analyze(base, dump, [f"gcroot {address:x}" for address in unnamed]))))

    rows, count = Counter(), Counter()
    totals = Counter()
    for address, (mapped, size) in zip(found, words):
        kind = "mapped" if mapped else "device"
        name = f"[root] {holders(rooted[address], a.depth)}" if address in rooted else holder(address)
        rows[(name, kind)] += size
        count[(name, kind)] += 1
        totals[kind] += size
    print(f"\ngpu buffers the heap holds ({dump.name}): {sum(count.values()):,} buffers, "
          f"{totals['device'] / 2**30:,.2f} GiB device-local, {totals['mapped'] / 2**30:,.2f} GiB mapped; "
          "MiB, buffers, kind, held by")
    for (name, kind), size in rows.most_common(a.limit or None):
        print(f"  {size / 2**20:10,.1f} {count[(name, kind)]:>4}  {kind:<6}  {name}")


def buffer_words(text: str, found: list[int], mapped_word: int, size_word: int) -> list[tuple[bool, int]]:
    """Each buffer's two words as (mapped, bytes), in the order asked for; `dq` prints four words a line."""
    memory = {}
    for line in text.splitlines():
        if m := QWORDS.match(line):
            at = int(line.split(":", 1)[0], 16)
            for index, word in enumerate(m["words"].split()):
                memory[at + 8 * index] = int(word, 16)
    read = []
    for address in found:
        mapped, size = memory.get(address + 8 * mapped_word), memory.get(address + 8 * size_word)
        if mapped is None or size is None or size >= BUFFER_SIZE_LIMIT:
            cli.fail(f"{address:x} is not a GpuBuffer as laid out: {BUFFER_TYPE}'s fields have moved")
        read.append((mapped != 0, size))
    return read


def object_refs(text: str, holders: list[tuple[int, int]]) -> list[list[tuple[str, int]]]:
    """Each object's reference fields as (name, target), one `dumpobjgcrefs` block an object, in the order asked."""
    found = []
    for line in text.splitlines():
        if line.startswith(REFS_HEAD):
            found.append([])
        elif found and (m := REF_ROW.match(line)):
            found[-1].append((m["name"], int(m["target"], 16)))
    if len(found) != len(holders):
        cli.fail(f"dumpobjgcrefs gave {len(found)} objects of {len(holders)} asked")
    return found


def objects(base: Path, dump: Path, commands: list[str]) -> list[tuple[int, str, str]]:
    """The objects `dumpheap` lists, as (bytes, address, method table), biggest first."""
    return listed_objects(analyze(base, dump, commands))


def listed_objects(text: str) -> list[tuple[int, str, str]]:
    found = []
    for line in text.splitlines():
        if m := OBJECT_ROW.match(line):
            found.append((int(m["bytes"].replace(",", "")), m["address"], m["mt"]))
    return sorted(found, reverse=True)


def holders(chain: list[str], depth: int) -> str:
    """The object's nearest holders, nearest first; an object no root reaches is dead and not yet collected."""
    return " <- ".join(chain[::-1][1:depth + 1]) or "(no root: dead)"


def chains(text: str) -> list[list[str]]:
    """
    Each `gcroot`'s first chain, as the types it walks from the root to the object. A command's chains are
    blank-line separated and it ends on `Found N unique roots.`; an object nothing roots has no chain.
    """
    found, first, chain = [], None, []
    for line in text.splitlines():
        if m := CHAIN_LINK.match(line):
            chain.append(short_type(m[1]))
        elif not line.strip() and chain:
            first = first or chain
            chain = []
        elif GCROOT_END.match(line):
            found.append(first or chain)
            first, chain = None, []
    return found


def short_type(name: str) -> str:
    """`TrafficSimulation.World.Road.RibbonAtlas` as `RibbonAtlas`, generic arguments and all kept short."""
    return re.sub(r"(?:[A-Za-z_][A-Za-z0-9_]*\.)+", "", name)
