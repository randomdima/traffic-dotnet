"""
The GPU half of `qq prof`: what a run's process asks of the device, read off the kernel's own accounting.

`--gpu` leaves `.gpu` beside the recording: every `EVERY_S`, the process's DRM clients (`/proc/<pid>/fdinfo`) -
each engine's busy time in ns and each memory region's resident KiB, per PCI device - and that device's own
sysfs counters (`gpu_busy_percent`, `mem_info_vram_used`, `mem_info_gtt_used`), which are the whole card's and so
include the desktop. An engine's busy share is its ns over the window's; an engine shows up only once used.
"""

import os
import re
import threading
import time
from collections import defaultdict
from pathlib import Path

EVERY_S = 0.5
FIELD = re.compile(r"^(?P<key>[a-z0-9-]+):\s+(?P<value>\d+)(?:\s*(?P<unit>ns|KiB|MiB))?\s*$")
#: The resident memory regions worth a row; `total` repeats `resident` for unshared buffers. amdgpu's own say where
#: buffers asked to be: VRAM requested over VRAM resident is device-local memory the driver evicted to system RAM.
REGIONS = ("drm-resident-vram", "drm-resident-gtt", "drm-resident-cpu",
           "amd-requested-vram", "amd-requested-gtt", "amd-evicted-vram")
CARD_FILES = ("gpu_busy_percent", "mem_info_vram_used", "mem_info_gtt_used")
#: The clock tables amdgpu marks its current level in with `*` (`2: 2520Mhz *`); `S:` is the sleep level.
CLOCK_FILES = ("pp_dpm_sclk", "pp_dpm_mclk")
CURRENT_CLOCK = re.compile(r"^\S+:\s*(\d+)Mhz\s*\*", re.MULTILINE)


def watch(pid: int, stem: Path, seconds: float | None) -> threading.Thread:
    """Sample until `seconds` pass or the process exits, on a thread; join it before `report`."""
    thread = threading.Thread(target=sample, args=(pid, stem.with_suffix(".gpu"), seconds), daemon=True)
    thread.start()
    return thread


def sample(pid: int, out: Path, seconds: float | None):
    cards = card_dirs()
    started = time.monotonic()
    with out.open("w", encoding="utf-8") as f:
        while seconds is None or time.monotonic() - started < seconds:
            at = time.monotonic() - started
            clients = read_clients(pid)
            if clients is None:
                break
            for pdev, fields in clients.items():
                for key, value in fields.items():
                    f.write(f"{at:.3f}\t{pdev}\t{key}\t{value}\n")
                card = cards.get(pdev)
                for name in CARD_FILES if card else ():
                    try:
                        f.write(f"{at:.3f}\t{pdev}\tcard-{name}\t{int((card / name).read_text())}\n")
                    except (OSError, ValueError):
                        pass
                for name in CLOCK_FILES if card else ():
                    try:
                        if m := CURRENT_CLOCK.search((card / name).read_text()):
                            f.write(f"{at:.3f}\t{pdev}\tcard-{name}\t{m[1]}\n")
                    except OSError:
                        pass
            f.flush()
            time.sleep(EVERY_S)


def card_dirs() -> dict[str, Path]:
    """Each DRM card's sysfs device directory by its PCI address, the key fdinfo's `drm-pdev` gives."""
    found = {}
    for card in Path("/sys/class/drm").glob("card[0-9]*"):
        if "-" not in card.name:
            device = (card / "device").resolve()
            found[device.name] = device
    return found


def read_clients(pid: int) -> dict[str, dict[str, int]] | None:
    """Engine ns and resident KiB summed over the process's DRM clients, per device; None once it has exited."""
    fdinfo = Path(f"/proc/{pid}/fdinfo")
    try:
        entries = os.listdir(fdinfo)
    except OSError:
        return None
    seen, totals = set(), defaultdict(lambda: defaultdict(int))
    for entry in entries:
        try:
            text = (fdinfo / entry).read_text(errors="replace")
        except OSError:
            continue
        if "drm-client-id" not in text:
            continue
        fields = {}
        for line in text.splitlines():
            if m := FIELD.match(line):
                value = int(m["value"]) * (1024 if m["unit"] == "MiB" else 1)
                fields[m["key"]] = value
        pdev = next((line.split()[1] for line in text.splitlines() if line.startswith("drm-pdev:")), "?")
        client = (pdev, fields.get("drm-client-id"))
        if client in seen:
            continue
        seen.add(client)
        for key, value in fields.items():
            if key.startswith("drm-engine-") or key in REGIONS:
                totals[pdev][key] += value
    return totals


def report(folded: Path):
    gpu = folded.with_suffix(".gpu")
    if not gpu.is_file():
        return
    series = defaultdict(list)
    for line in gpu.read_text(encoding="utf-8").splitlines():
        at, pdev, key, value = line.split("\t")
        series[(pdev, key)].append((float(at), int(value)))
    devices = sorted({pdev for pdev, _ in series})
    print(f"\ngpu over the window ({gpu.name}), every {EVERY_S} s")
    for pdev in devices:
        engines = {key: s for (p, key), s in series.items() if p == pdev and key.startswith("drm-engine-")}
        busy = {key: (s[-1][1] - s[0][1]) / 1e9 / (s[-1][0] - s[0][0])
                for key, s in engines.items() if len(s) > 1 and s[-1][0] > s[0][0]}
        if not any(busy.values()):
            continue
        print(f"  {pdev}{' ' + name_of(pdev) if name_of(pdev) else ''}")
        for key, share in sorted(busy.items(), key=lambda kv: -kv[1]):
            if share > 0:
                print(f"    {100 * share:6.1f}% busy  {key.removeprefix('drm-engine-')} (this process)")
        for key in REGIONS:
            if s := series.get((pdev, key)):
                values = [v for _, v in s]
                what = key.removeprefix("drm-resident-") + " resident" if key.startswith("drm-") else key.removeprefix("amd-")
                print(f"    {values[0] / 1024:8,.0f} {values[-1] / 1024:8,.0f} {max(values) / 1024:8,.0f} MiB first/last/peak  "
                      f"{what} (this process)")
        if s := series.get((pdev, "card-gpu_busy_percent")):
            values = [v for _, v in s]
            print(f"    {sum(values) / len(values):6.1f}% mean, {max(values)}% peak  card busy (whole card)")
        for name, what in (("pp_dpm_sclk", "shader clock"), ("pp_dpm_mclk", "memory clock")):
            if s := series.get((pdev, f"card-{name}")):
                values = sorted(v for _, v in s)
                print(f"    {values[0]:6,} {values[len(values) // 2]:6,} {values[-1]:6,} MHz min/median/max  {what}")
        for name in ("mem_info_vram_used", "mem_info_gtt_used"):
            if s := series.get((pdev, f"card-{name}")):
                values = [v for _, v in s]
                print(f"    {values[0] / 2**20:8,.0f} {values[-1] / 2**20:8,.0f} {max(values) / 2**20:8,.0f} MiB first/last/peak  "
                      f"{name.removeprefix('mem_info_').removesuffix('_used')} used (whole card)")


def name_of(pdev: str) -> str:
    """The device's product name where the driver says one, for telling an integrated GPU from a discrete one."""
    try:
        return (card_dirs()[pdev] / "product_name").read_text().strip()
    except (KeyError, OSError):
        return ""
