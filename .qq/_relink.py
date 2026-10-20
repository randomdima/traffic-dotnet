"""
Repairing relative links between documents after the tree has moved. Private: the two tools that
front it are `relink-docs` (report) and `relink-apply` (rewrite).

Why it exists: a relative link is broken by a move at *either* end, and everything about a feature
lives in that feature's folder here — so moving one slice moves its code, its `docs/`, its tests
and its art at once, and every document that pointed into any of them.

How a target is resolved
------------------------
Not by a table of moves — by the tree itself. A broken link is matched against every file in the
project by its **longest path suffix**: a link that used to read `../../physics/docs/solver.md`
still ends in `docs/solver.md`, and the match must be unique. The longest suffix that matches
anything is the one used, so a bare `index.md` — which matches many — resolves to nothing and is
reported rather than guessed at.

That is the whole safety property: **it never picks between candidates.** Anything ambiguous, and
anything with no match at all, is printed for a person to deal with, and the exit status is
non-zero while any remain.

Anchors (`#section`) are carried through untouched. A link that already resolves is left alone,
so this is safe to re-run.
"""

from __future__ import annotations

import os
import re
from pathlib import Path

#: Folders a document never links into: output, scratch, and the harness's own.
SKIP = {".claude", ".git", ".qq", ".tmp", "bin", "obj", "TestResults", "__pycache__"}

#: A markdown link: `[text](target)`.
LINK = re.compile(r"(\[[^\]]*\]\()([^)\s]+)(\))")

#: Targets that are not paths into this tree.
EXTERNAL = ("http://", "https://", "mailto:", "#")

#: The trees a slice is mirrored under, longest first — the first match wins. Stripping one off both
#: the linking document and a candidate is what makes `assets/agents/car/` and
#: `src/tests/agents/car/` count as near to `src/agents/car/` when a tie has to be broken.
ROOTS = ("", "src/tests/", "assets/", "src/", "docs/")


def repair(base: Path, write: bool) -> tuple[int, int]:
    """Walk the documents, print every link this would change, and return (repaired, left)."""
    every = inventory(base)
    index: dict[str, list[str]] = {}
    for path in every:
        parts = path.split("/")
        for cut in range(len(parts)):
            index.setdefault("/".join(parts[cut:]), []).append(path)

    repaired = left = 0
    for path in sorted(p for p in every if p.endswith(".md")):
        here = os.path.dirname(path)
        text = (base / path).read_text(encoding="utf-8", errors="surrogateescape")
        notes: list[str] = []

        def fix(match: re.Match[str]) -> str:
            nonlocal repaired, left
            head, target, tail = match.group(1), match.group(2), match.group(3)
            if target.startswith(EXTERNAL):
                return match.group(0)
            file_part, _, anchor = target.partition("#")
            if not file_part or (base / os.path.normpath(os.path.join(here, file_part))).exists():
                return match.group(0)

            found, why = resolve(index, file_part, path)
            if found is None:
                left += 1
                notes.append(f"    UNRESOLVED  {target}   ({why})")
                return match.group(0)

            new = os.path.relpath(found, here or ".").replace(os.sep, "/")
            if anchor:
                new += "#" + anchor
            repaired += 1
            notes.append(f"    {target}\n            -> {new}")
            return head + new + tail

        fixed = LINK.sub(fix, text)
        if notes:
            print(path)
            print("\n".join(notes))
        if write and fixed != text:
            (base / path).write_text(fixed, encoding="utf-8", errors="surrogateescape")

    return repaired, left


def inventory(base: Path) -> list[str]:
    """Every file in the tree, as a root-relative posix path, for suffix matching."""
    every = []
    for root, dirs, files in os.walk(base):
        dirs[:] = [d for d in dirs if d not in SKIP]
        for name in files:
            every.append(os.path.relpath(os.path.join(root, name), base).replace(os.sep, "/"))
    return every


def resolve(index: dict[str, list[str]], target: str, document: str) -> tuple[str | None, str | None]:
    """
    The file whose path ends with the longest suffix of `target`.

    Where several end that way, the one living in the same slice as the document that links to it
    wins — but only if it wins outright. A tie at the top score is reported, never guessed.
    """
    parts = [p for p in target.split("/") if p not in (".", "..")]
    for cut in range(len(parts)):
        suffix = "/".join(parts[cut:])
        hits = index.get(suffix, [])
        if len(hits) == 1:
            return hits[0], None
        if len(hits) > 1:
            mine = slice_of(document)
            scored = sorted(((agreement(slice_of(hit), mine), hit) for hit in hits), reverse=True)
            if len(scored) > 1 and scored[0][0] == scored[1][0]:
                return None, f"ambiguous ({len(hits)} candidates for {suffix}, none nearer)"
            if scored[0][0] == 0:
                return None, f"ambiguous ({len(hits)} candidates for {suffix})"
            return scored[0][1], None
    return None, "no match"


def slice_of(path: str) -> str:
    for root in ROOTS:
        if root and path.startswith(root):
            return path[len(root):]
    return path


def agreement(a: str, b: str) -> int:
    """How many leading path components two files share — how near they are to each other."""
    left, right = a.split("/"), b.split("/")
    shared = 0
    while shared < len(left) and shared < len(right) and left[shared] == right[shared]:
        shared += 1
    return shared
