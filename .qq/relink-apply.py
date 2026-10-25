"""
Rewrite the relative links between documents that a move has broken.

**This one writes.** Run `qq relink-docs` first and read what it says it would do: the two share
their whole implementation (`_relink.py`), so the report is exactly this run without the write.
It edits .md files in place across the tree; the way back is git.

Named off the reporting tool's prefix on purpose. An allowlist rule is a prefix match, so a name
that extended `relink-docs` would be pre-approved by any rule covering it — the writing half of a
split pair must not be reachable through the reading half's permission.

It only ever replaces a link it resolved to a single file, by longest unique path suffix. A link
that already resolves is left alone, so this is safe to re-run, and anything ambiguous or
unmatched is printed untouched for a person to deal with.

Exit status is the number of links left for a person, so a tree it fully repaired exits 0.
"""

from __future__ import annotations

import argparse

import _relink
import _sim
from common import cli


def main():
    parse_args()
    repaired, left = _relink.repair(_sim.root(), write=True)
    print()
    print(f"{repaired} link(s) repaired, {left} left for a person")
    return 1 if left else 0


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq relink-apply",
        description="Rewrite the document links a move has broken. Writes .md files in place.",
        allow_abbrev=False,
    )
    return parser.parse_args()


if __name__ == "__main__":
    cli.entry(main)
