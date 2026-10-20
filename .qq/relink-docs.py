"""
Report the relative links between documents that a move has broken, and where each one now points.

Read-only: it prints what would change and writes nothing. `qq relink-apply` is the tool that
makes the change — a separate one, because a checker that can also rewrite what it checks is a
checker that can be made to pass, and named off this one's prefix so an allowlist rule here cannot
reach it.

A broken link is matched against every file in the tree by its longest path suffix, and the match
has to be unique: it never picks between candidates. Anything ambiguous, and anything with no
match at all, is printed for a person to deal with, and those are what the non-zero exit means.
The reasoning and the safety property in full are in `_relink.py` beside this file.

Exit status is the number of links left for a person, so a clean tree exits 0.
"""

from __future__ import annotations

import argparse

import _relink
import _sim
from common import cli


def main():
    parse_args()
    repaired, left = _relink.repair(_sim.root(), write=False)
    print()
    print(f"{repaired} link(s) would be repaired, {left} left for a person")
    print("(dry run — nothing written; qq relink-apply makes the change)")
    return 1 if left else 0


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq relink-docs",
        description="Report the document links a move has broken, and where each one now points.",
        allow_abbrev=False,
    )
    return parser.parse_args()


if __name__ == "__main__":
    cli.entry(main)
