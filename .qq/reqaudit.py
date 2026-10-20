"""
Weigh every rule: who obeys it, who only mentions it, and which ones only say no.

`doclint` asks whether a citation resolves and `req` looks one rule up. This asks the question you
need before deleting a rule — what would change if it were gone:

    grip          behaviour code, tests, other rules, or nothing but its own paragraph
    reach         how many files and how many slices answer to it
    churn         how often the statement itself has been rewritten, from the log
    shape         whether it states a relation, carries a number, or only forbids something

A rule cited by behaviour code is load-bearing whatever it says. A rule cited only by the test that
checks it is a rule the suite keeps alive; a rule cited by nothing is a paragraph. Nothing here
deletes anything: it prints the table, and which rules go is a reading of it.
"""

from __future__ import annotations

import argparse
import re
from collections import Counter, defaultdict
from pathlib import Path
from typing import NamedTuple

import _docs
import _sim
from common import cli, git

#: Where a citation carries weight. Behaviour code obeys a rule; a test only checks it; a document
#: only refers to it. The three answer different questions and are never summed.
TESTS = "src/tests/"
HISTORY = "decision-log.md"

#: A rule that only refuses. The project's own complaint about these is SIM-7: a second gate in
#: front of one already answering does not make the town safer, it makes the first one useless.
FORBIDS = re.compile(r"\b(?:never|no\b|not\b|nothing|refus\w+|reject\w+|forbid\w+|must not|may not"
                     r"|cannot|is a defect|fails?\b|banned|prohibit\w+)", re.IGNORECASE)

#: A figure inside a statement. "A rule states a relation; a number is data" — so a rule carrying a
#: magnitude with a unit is stating data, and `SimConfig` is where data lives.
NUMBER = re.compile(r"(?<![\w.])\d+(?:\.\d+)?\s*(?:m|km|s|ms|kg|deg|°|%|m/s|m/s²|mps|px|Hz)\b")

#: Two rules that read as the same rule. Compared on the words that carry meaning, so wording alone
#: does not hide a duplicate and shared boilerplate does not invent one.
STOP = set("a an the and or of to in on is are be it its that this what which with as at by for "
           "from not no never nothing than then so its it's one two any every each all".split())


class Rule(NamedTuple):
    """One rule, and everything that turns on it."""

    ident: str
    family: str
    owner: str
    slice: str
    words: int
    code: int
    tests: int
    docs: int
    logs: int
    files: int
    slices: int
    rewrites: int
    forbids: bool
    numbers: int
    gist: str

    @property
    def grip(self) -> str:
        """The shortest true answer to 'what obeys this'."""
        if self.code:
            return "code"
        if self.tests:
            return "tests"
        if self.docs:
            return "docs"
        if self.logs:
            return "log"
        return "nothing"


def main():
    a = parse_args()
    base = _sim.root()
    found = _docs.audit(base, "docs/index.md")
    rewrites = statement_churn(base)

    rules: list[Rule] = []
    for ident, sites in found.defined.items():
        owner = sites[0]
        cites = found.cited.get(ident, [])
        code = [s for s in cites if s.path.endswith(".cs") and not s.path.startswith(TESTS)]
        tests = [s for s in cites if s.path.endswith(".cs") and s.path.startswith(TESTS)]
        logs = [s for s in cites if s.path.endswith(HISTORY)]
        docs = [s for s in cites if s.path.endswith(".md") and not s.path.endswith(HISTORY)]
        rules.append(Rule(
            ident=ident,
            family=ident.split("-")[0],
            owner=owner.path,
            slice=owning_slice(owner.path),
            words=len(owner.text.split()),
            code=len(code),
            tests=len(tests),
            docs=len(docs),
            logs=len(logs),
            files=len({s.path for s in code + tests}),
            slices=len({owning_slice(s.path) for s in code}),
            rewrites=rewrites.get(ident, 0),
            forbids=bool(FORBIDS.search(_docs.gist(owner.text))),
            numbers=len(NUMBER.findall(owner.text)),
            gist=_docs.gist(owner.text),
        ))

    rules.sort(key=key_of)
    report = render(rules, twins(rules), a)
    if a.out:
        out = Path(a.out)
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(report, encoding="utf-8")
        print(f"wrote {out} ({len(rules)} rules)")
    else:
        print(report)
    return 0


def key_of(rule: Rule) -> tuple:
    return (rule.family, _docs.sort_key(rule.ident))


def owning_slice(rel: str) -> str:
    """The slice a path belongs to, named the way CLAUDE.md names one."""
    parts = rel.split("/")
    if parts[0] != "src":
        return "(project)"
    if "docs" in parts:
        return "/".join(parts[1:parts.index("docs")])
    return "/".join(parts[1:-1])


def statement_churn(base: Path) -> dict[str, int]:
    """
    How often each rule's own statement has been rewritten, from one walk of the log.

    A commit that adds or removes the line *defining* a rule rewrote that rule; a commit that only
    moves a citation did not. The first such commit is the rule being written, so it does not count.
    """
    seen: dict[str, set[str]] = defaultdict(set)
    result = git.probe(base, "log", "-p", "-U0", "--no-merges", "--format=%x00%H", "--", "*.md")
    if result.code != 0:
        return {}
    commit = ""
    for line in result.stdout.splitlines():
        if line.startswith("\0"):
            commit = line[1:].strip()
        elif line[:1] in "+-" and line[1:2] not in ("+", "-"):
            match = _docs.DEFINITION.match(line[1:])
            if match:
                seen[match.group("id") or match.group("heading")].add(commit)
    return {ident: len(commits) - 1 for ident, commits in seen.items()}


def twins(rules: list[Rule]) -> list[tuple[Rule, Rule, float]]:
    """Pairs of rules whose statements say close to the same thing, worst overlap first."""
    bags = {r.ident: {w for w in re.findall(r"[a-z]{3,}", r.gist.lower()) if w not in STOP} for r in rules}
    out = []
    for i, one in enumerate(rules):
        for other in rules[i + 1:]:
            a, b = bags[one.ident], bags[other.ident]
            if len(a) < 4 or len(b) < 4:
                continue
            overlap = len(a & b) / len(a | b)
            if overlap >= 0.5:
                out.append((one, other, overlap))
    return sorted(out, key=lambda t: -t[2])


def render(rules: list[Rule], pairs: list[tuple[Rule, Rule, float]], a) -> str:
    """The whole weighing as markdown."""
    total = len(rules)
    by_grip = Counter(r.grip for r in rules)

    out = ["# Rules, weighed", ""]
    out += [f"{total} rules in {len({r.family for r in rules})} families, stated in "
            f"{len({r.owner for r in rules})} documents. "
            f"{by_grip['code']} are cited by behaviour code, {by_grip['tests']} only by a test, "
            f"{by_grip['docs']} only by another document, {by_grip['log']} only by a decision log, "
            f"and {by_grip['nothing']} by nothing at all.", ""]

    out += ["## By family", "",
            "| Family | Rules | Code | Tests only | Docs only | Dead | Forbids | Rewritten | Owner |",
            "|---|--:|--:|--:|--:|--:|--:|--:|---|"]
    fams: dict[str, list[Rule]] = defaultdict(list)
    for rule in rules:
        fams[rule.family].append(rule)
    for family, members in sorted(fams.items(), key=lambda kv: -len(kv[1])):
        grips = Counter(r.grip for r in members)
        owners = Counter(r.owner for r in members)
        where = owners.most_common(1)[0][0] if len(owners) == 1 else f"{len(owners)} documents"
        out.append(f"| {family} | {len(members)} | {grips['code']} | {grips['tests']} | "
                   f"{grips['docs'] + grips['log']} | {grips['nothing']} | "
                   f"{sum(1 for r in members if r.forbids)} | "
                   f"{sum(1 for r in members if r.rewrites)} | {where} |")

    out += ["", "## By document", "",
            "| Document | Rules | Code | Dead | Words in statements |", "|---|--:|--:|--:|--:|"]
    docs: dict[str, list[Rule]] = defaultdict(list)
    for rule in rules:
        docs[rule.owner].append(rule)
    for owner, members in sorted(docs.items(), key=lambda kv: -len(kv[1])):
        grips = Counter(r.grip for r in members)
        out.append(f"| {owner} | {len(members)} | {grips['code']} | {grips['nothing']} | "
                   f"{sum(r.words for r in members):,} |")

    out += section("Nothing cites these", rules, lambda r: r.grip == "nothing",
                   "No code, no test, no other rule and no log names them. Deleting one changes nothing "
                   "a machine can see.")
    out += section("Only a test cites these", rules, lambda r: r.grip == "tests",
                   "The suite keeps them alive. Either the rule is real and the code should cite it, or "
                   "the test is the rule and the paragraph is a second copy.")
    out += section("Only prose cites these", rules, lambda r: r.grip in ("docs", "log"),
                   "Named by another document or by history, obeyed by nothing.")
    out += section("Forbid something, and no code obeys them", rules,
                   lambda r: r.forbids and not r.code,
                   "A refusal nothing enforces. SIM-7's own complaint, turned on the rules themselves.")
    out += section("Carry a number", rules, lambda r: r.numbers >= 1,
                   "A rule states a relation; a number is data, and data lives on `SimConfig`.",
                   extra=lambda r: f"{r.numbers} figure(s)")

    churned = [r for r in rules if r.rewrites >= a.churn]
    if churned:
        out += ["", f"## Rewritten {a.churn}+ times", "",
                "The statement itself changed, not a citation. A rule that will not settle is one the "
                "code keeps disagreeing with.", "",
                "| Rule | Rewrites | Grip | Owner | Says |", "|---|--:|---|---|---|"]
        for rule in sorted(churned, key=lambda r: (-r.rewrites, r.ident)):
            out.append(f"| `{rule.ident}` | {rule.rewrites} | {rule.grip} | {rule.owner} | {rule.gist} |")

    if pairs:
        out += ["", "## Say close to the same thing", "",
                "| Overlap | Rule | Says | Rule | Says |", "|--:|---|---|---|---|"]
        for one, other, overlap in pairs[: a.limit or None]:
            out.append(f"| {overlap:.0%} | `{one.ident}` | {one.gist} | `{other.ident}` | {other.gist} |")

    if a.rules:
        out += ["", "## Every rule", "",
                "| Rule | Owner | Words | Code | Tests | Docs | Logs | Files | Slices | Rewrites | Says |",
                "|---|---|--:|--:|--:|--:|--:|--:|--:|--:|---|"]
        for rule in rules:
            out.append(f"| `{rule.ident}` | {rule.owner} | {rule.words} | {rule.code} | {rule.tests} | "
                       f"{rule.docs} | {rule.logs} | {rule.files} | {rule.slices} | {rule.rewrites} | "
                       f"{rule.gist} |")

    return "\n".join(out) + "\n"


def section(title: str, rules: list[Rule], keep, note: str, extra=None) -> list[str]:
    """One table of the rules answering some question, or nothing at all when none do."""
    chosen = [r for r in rules if keep(r)]
    if not chosen:
        return []
    head = "| Rule | Owner | Words | Says |"
    rule_bar = "|---|---|--:|---|"
    if extra:
        head = "| Rule | Owner | Words | Grip | What | Says |"
        rule_bar = "|---|---|--:|---|---|---|"
    out = ["", f"## {title} — {len(chosen)}", "", note, "", head, rule_bar]
    for rule in sorted(chosen, key=key_of):
        if extra:
            out.append(f"| `{rule.ident}` | {rule.owner} | {rule.words} | {rule.grip} | "
                       f"{extra(rule)} | {rule.gist} |")
        else:
            out.append(f"| `{rule.ident}` | {rule.owner} | {rule.words} | {rule.gist} |")
    return out


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq reqaudit",
        description="Weigh every rule by what obeys it, how often it is rewritten, and what it forbids.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("--rules", action="store_true", help="list every rule, not just the answers")
    parser.add_argument("--churn", type=cli.counting, default=2, metavar="INT",
                        help="how many rewrites make a rule worth naming")
    parser.add_argument("--limit", type=cli.counting, default=25, metavar="INT",
                        help="rows in the near-duplicate table (0 = no limit)")
    parser.add_argument("--out", metavar="PATH", help="write the report as markdown instead of printing it")
    return parser.parse_args()


if __name__ == "__main__":
    cli.entry(main)
