"""
Look a requirement up: what it says, which document owns it, and the code that cites it.

The rules are sliced the way the code is, so the document holding `TER-5c` is findable only by
reading the map in docs/index.md or grepping every `docs/` folder for it. This resolves the ID
against the documents themselves — no second copy to fall out of date — and reports what the map
is supposed to say, plus every call site that cites the rule.

With a family instead of a rule it lists that family; with nothing it prints the map: every
family, how many rules it holds, and which documents state them. The index is
[_docs.py](_docs.py), which `doclint` audits with.

Every rule also carries a rung saying whose it is and what bending it costs (docs/priority.md),
printed beside the statement; `--rungs` lists the owner's own band, which is held in the documents
and in no register of its own.
"""

from __future__ import annotations

import argparse
import re
from pathlib import Path

import _docs
import _sim
from common import cli


def main():
    a = parse_args()
    base = _sim.root()
    defined = _docs.definitions(base)

    if a.rungs:
        return show_rungs(defined, a.limit)
    if a.id is None:
        return show_map(defined)
    if "-" not in a.id:
        return show_family(base, defined, a.id.upper(), a.next, a.limit)
    return show_one(base, defined, _docs.spelled(a.id), a.limit)


def show_one(base: Path, defined: dict, ident: str, limit: int) -> int:
    """One rule: where it is stated, what it states, and everything that cites it."""
    if ident not in defined:
        cli.fail(f"no document defines {ident}" + near(defined, ident))
    for site in defined[ident]:
        print(f"{ident} — {site} — {whose(site.rung)}")
        for row in site.text.splitlines():
            print(f"  {row}")

    sites = _docs.cited(base).get(ident, [])
    print(f"\ncited in {len(sites)} places" + (":" if sites else ""))
    for site in sites[: limit or None]:
        print(f"  {str(site):<48}  {site.text[:88]}")
    if limit and len(sites) > limit:
        print(f"  … {len(sites) - limit} more (--limit=0 for all)")
    return 0


def show_family(base: Path, defined: dict, prefix: str, want_next: bool, limit: int) -> int:
    """Every rule sharing a family, in the order their numbers run."""
    family = sorted((key for key in defined if key.split("-")[0] == prefix), key=_docs.sort_key)
    if not family:
        cli.fail(f"no rule has the family {prefix}. defined: {', '.join(sorted({k.split('-')[0] for k in defined}))}")
    for ident in family[: limit or None]:
        site = defined[ident][0]
        print(f"{ident:<10} {site.rung or '--':<3} {str(site):<52} {_docs.gist(site.text)}")
    if limit and len(family) > limit:
        print(f"… {len(family) - limit} more (--limit=0 for all)")
    if want_next:
        print(allocation(prefix, family))
    return 0


def show_map(defined: dict) -> int:
    """The ID map, derived: which documents state which family, and how much of it."""
    families: dict[str, dict[str, int]] = {}
    for ident, sites in defined.items():
        where = families.setdefault(ident.split("-")[0], {})
        for site in sites:
            where[site.path] = where.get(site.path, 0) + 1
    for prefix in sorted(families, key=lambda name: -sum(families[name].values())):
        print(f"{prefix:<5} {sum(families[prefix].values()):>3}  {owners(families[prefix])}")
    print(f"\n{sum(len(sites) for sites in defined.values())} rules stated in "
          f"{len({site.path for sites in defined.values() for site in sites})} documents")
    return 0


def whose(rung: str | None) -> str:
    """Who a rung says the rule belongs to, spelled out — the one thing a reader must not guess."""
    if rung is None:
        return "no rung (docs/priority.md)"
    if rung in _docs.OWNER_RUNGS:
        held = "stated explicitly, bends for nothing" if rung == "P0" else "stated softly, bends at an edge case"
        return f"{rung} — the owner's, {held}"
    return f"{rung} — the assistant's"


def show_rungs(defined: dict, limit: int) -> int:
    """
    The owner's band, which is the whole of the P0/P1 register: it is held in the documents and
    nowhere else, so listing it is reading them rather than opening a second list that can drift.
    """
    owned = sorted(
        ((ident, site) for ident, sites in defined.items() for site in sites
         if site.rung in _docs.OWNER_RUNGS),
        key=lambda pair: (pair[1].rung, _docs.sort_key(pair[0])))
    if not owned:
        print("no rule carries P0 or P1: every rule stated is the assistant's own (docs/priority.md).")
        return 0
    for ident, site in owned[: limit or None]:
        print(f"{site.rung}  {ident:<10} {str(site):<52} {_docs.gist(site.text)}")
    if limit and len(owned) > limit:
        print(f"… {len(owned) - limit} more (--limit=0 for all)")
    print(f"\n{len(owned)} of {sum(len(sites) for sites in defined.values())} rules are the owner's.")
    return 0


def allocation(prefix: str, family: list[str]) -> str:
    """
    The next number to hand out, and the ones inside the range that nothing currently states.

    A gap is a retired rule as often as an unused number, and a retired number is never reused —
    so the gaps are printed to be avoided, not to be filled.
    """
    used = sorted({int(re.match(r"[A-Z]+-(\d+)", ident).group(1)) for ident in family})
    gaps = [number for number in range(1, used[-1]) if number not in used]
    answer = f"\nnext free: {prefix}-{used[-1] + 1}"
    if gaps:
        answer += (f"\nnot currently stated: {', '.join(f'{prefix}-{number}' for number in gaps)}"
                   " — retired or never used, and never reused either way")
    return answer


def owners(where: dict[str, int]) -> str:
    """
    Which documents state a family, shortest answer first: one document, a few of them, or — for
    a catalogue that is one page per entry — the folder they all sit in.
    """
    ranked = sorted(where.items(), key=lambda pair: -pair[1])
    if len(ranked) == 1:
        return ranked[0][0]
    if len(ranked) <= 3:
        return ", ".join(f"{path} ({count})" for path, count in ranked)
    folders = {path.rsplit("/", 1)[0] for path in where}
    if len(folders) == 1:
        return f"{folders.pop()}/ — {len(ranked)} pages"
    return ", ".join(f"{path} ({count})" for path, count in ranked[:3]) + f", and {len(ranked) - 3} more"


def near(defined: dict, ident: str) -> str:
    """What the caller probably meant, when nothing states what they asked for."""
    family = ident.split("-")[0]
    close = sorted((key for key in defined if key.startswith(ident) or key.split("-")[0] == family), key=_docs.sort_key)
    return f". near it: {', '.join(close[:8])}" if close else ""


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq req",
        description="Resolve a requirement ID against the documents that state it.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("id", nargs="?", type=_docs.identifier,
                        help="a rule (TER-5c), a family (TER), or nothing for the map of every family")
    parser.add_argument("--next", action="store_true",
                        help="with a family: the next free number, and the numbers nothing states any more")
    parser.add_argument("--rungs", action="store_true",
                        help="the owner's band — every P0 and P1 rule, which is the whole of that "
                             "register and is held in the documents themselves (docs/priority.md)")
    parser.add_argument("--limit", type=cli.counting, default=40, metavar="INT",
                        help="max rows to print (0 = no limit)")
    return parser.parse_args()


if __name__ == "__main__":
    cli.entry(main)
