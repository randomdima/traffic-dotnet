"""
Shared bones of this project's build-time tools. Private: the leading `_` keeps it out of the
`qq --help` registry, because it is a module and not a command.

Finding the project, keeping the last ten full logs of a kind, and cutting a long build log down
to the few lines worth reading. One copy, so the tools cannot drift apart.

One thing to know about the output: `proc.run` captures a child instead of `tee`-ing it, so a
long run prints nothing until it ends. The full log is still written, and it is still the thing
to read when the summary is not enough.
"""

from __future__ import annotations

import os
import re
import time
from pathlib import Path

from common import paths, proc
from common.cli import fail

#: The project root, taken from this file rather than from the working directory: this checkout
#: currently sits *inside* another project's, so `paths.project_root()` walking up from the cwd
#: would find whichever of the two the caller happened to stand in. Not resolved: a git worktree reaches these tools
#: through a link to the main checkout's `.qq`, and is its own root.
ROOT = Path(__file__).absolute().parent.parent

#: What tells this project apart from any other checkout, and what `dotnet build` is given.
MARKER = "traffic-dotnet.csproj"

#: A user-local SDK install: where an SDK newer than the system's package is put (`dotnet-install.sh`).
USER_DOTNET = Path.home() / ".dotnet"


def use_pinned_sdk():
    """
    `global.json` names the SDK; where only the user-local install has it, that install is put first on PATH and made
    the runtime's root, so `dotnet`, the apphost of a built binary and every child of either find it. Nothing is
    changed where the pinned SDK is not there either — the build's own message then says which one is wanted.
    """
    try:
        import json
        pinned = json.loads((ROOT / "global.json").read_text(encoding="utf-8"))["sdk"]["version"]
    except (OSError, ValueError, KeyError):
        return
    if not (USER_DOTNET / "sdk" / pinned).is_dir():
        return
    os.environ["PATH"] = f"{USER_DOTNET}{os.pathsep}{os.environ.get('PATH', '')}"
    os.environ["DOTNET_ROOT"] = str(USER_DOTNET)


use_pinned_sdk()


def root() -> Path:
    """The project root, refusing a directory that is not this project."""
    if not (ROOT / MARKER).is_file():
        fail(f"not the traffic-dotnet project: no {MARKER} in {paths.display(ROOT)}")
    return ROOT


def rel(base: Path, path: Path) -> str:
    """A path as this project prints one: relative to the root."""
    return str(path.relative_to(base)).replace("\\", "/")


def dotnet(base: Path, *argv: str) -> proc.Result:
    """
    The SDK, in the project. `check=False` on purpose: a failed build and a failing test are both
    answers the caller handles itself, and neither is a reason to raise past the summary it was
    about to print.
    """
    return proc.run([*memory_cap(), "dotnet", *argv], cwd=base, check=False)


def memory_cap() -> list[str]:
    """
    What holds a light run's SDK, and every child it starts (the build, glslc, the test host, lavapipe), to a share of
    the machine's memory, past which the kernel kills the run and not the desktop around it — or nothing for a full run.
    Set by whatever lightens the run (`QQ_MEMORY_MAX`, systemd's own size words).
    """
    most = os.environ.get("QQ_MEMORY_MAX")
    if not most:
        return []
    return ["systemd-run", "--user", "--scope", "--quiet", "-p", f"MemoryMax={most}", "-p", "MemorySwapMax=0", "--"]


#: How many full logs of one kind are kept under .tmp/logs/.
KEEP_LOGS = 10


def write_log(base: Path, kind: str, text: str) -> Path:
    """
    Keep the whole of a run's output, not the grep of it: a failure usually explains itself in the
    lines the summary drops, and a second run costs minutes. The last `KEEP_LOGS` of each kind are
    kept; the rest go.
    """
    logs = base / ".tmp" / "logs"
    logs.mkdir(parents=True, exist_ok=True)
    for stale in sorted(logs.glob(f"{kind}-*.log"), key=lambda f: f.stat().st_mtime, reverse=True)[KEEP_LOGS - 1 :]:
        stale.unlink()
    path = logs / f"{kind}-{time.strftime('%Y%m%d-%H%M%S')}.log"
    path.write_text(text, encoding="utf-8", errors="replace")
    print(f"=== full output {rel(base, path)} ({path.stat().st_size // 1024} KiB)")
    return path


def show(text: str, pattern: str, limit: int = 0) -> list[str]:
    """The lines of a run worth reading, printed and returned. `limit` 0 prints all of them."""
    hits = [line for line in text.splitlines() if re.search(pattern, line)]
    for line in hits[: limit or None]:
        print(line)
    return hits
