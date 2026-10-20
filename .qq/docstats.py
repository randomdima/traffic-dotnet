"""
Measure the documentation: how much of it there is, whose it is, and what still answers to code.

`doclint` asks whether the citations resolve. This asks the questions you need before deciding a
document should be shorter, merged or gone:

    size          words per document, per kind, per slice, against the code that slice holds
    grip          rules stated, and how many of them the code actually cites by ID
    reach         which documents are linked from elsewhere, and which nothing points at
    drift         when the document was last touched against when its slice's code was

Nothing here judges. It prints the table; the reading of it belongs in whatever the audit produces.
Default output is the per-slice summary; `--docs` lists every file, `--out` writes the whole thing
as markdown.
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

#: A document at `<slice>/docs/<name>.md` belongs to that slice. Anything above `src/` belongs to
#: the project itself, which is a group and not a slice.
PROJECT = "(project)"

#: Code under no slice with a `docs/` of its own — the test tree, mostly.
UNSLICED = "(unsliced)"

#: What counts as code a slice is documenting. The browser head is one slice whose whole body is
#: script and markup, so a C#-only count would read it as documentation about nothing.
CODE = {".cs", ".js", ".ts", ".html", ".css"}

#: A markdown link to another document, relative to the linking file.
LINK = re.compile(r"\[[^\]]*\]\((?!https?:|#)([^)#]+)")

#: A name a document quotes as the code's — `LaneOccupancy`, `ViewFigures.ArtPixelsPerMetre`. Four
#: letters at least: `Sa` and `Sb` are this project's own notation for a manoeuvre's two states.
QUOTED = re.compile(r"`([A-Z][a-z][A-Za-z0-9]{2,}(?:\.[A-Za-z][A-Za-z0-9]*)*)`")

#: Where a name may be defined. The build files count: the browser head's rules are MSBuild
#: properties, and a document naming one is not naming something gone.
DECLARED = {".cs", ".js", ".ts", ".csproj", ".props", ".targets", ".json"}

#: What a decision log calls an entry: a second-level heading. Counting them says whether a log is
#: long because it records much or because each entry is.
ENTRY = re.compile(r"^##\s+\S")

#: The dated form of that heading. Some logs date every entry and some date none, and which is
#: which is the only thing that says whether the log can be read in the order it happened.
DATED = re.compile(r"^##\s+\d{4}-\d{2}-\d{2}")

#: How many words make a passage worth calling repeated. Long enough that a shared idiom — this
#: project writes "and nowhere else" and "is a defect" often — is not a hit, short enough to catch a
#: sentence carried from one document into another.
SHINGLE = 12

#: What a word is, once the markup is off: a document quoting `LaneOccupancy` and one saying
#: LaneOccupancy are repeating each other, and neither the backticks nor a bold run is the text.
WORD = re.compile(r"[a-z0-9]+")


class Doc(NamedTuple):
    """One document, measured."""

    path: str
    slice: str
    kind: str
    words: int
    lines: int
    stated: int
    #: Words inside the rule statements themselves. The rest of a requirements document is the
    #: prose around them, which is where a page grows without stating anything more.
    stated_words: int
    cited_by_code: int
    entries: int
    dated: int
    inbound: int
    touched: str

    @property
    def uncited(self) -> int:
        return self.stated - self.cited_by_code


class Slice(NamedTuple):
    """A slice's documents against the code they are about."""

    name: str
    docs: list[Doc]
    code_lines: int
    code_touched: str

    @property
    def words(self) -> int:
        return sum(d.words for d in self.docs)

    @property
    def stated(self) -> int:
        return sum(d.stated for d in self.docs)

    @property
    def cited_by_code(self) -> int:
        return sum(d.cited_by_code for d in self.docs)

    @property
    def density(self) -> float:
        """Documented words per hundred lines of the slice's own code."""
        return 100.0 * self.words / self.code_lines if self.code_lines else float("inf")


def main():
    a = parse_args()
    base = _sim.root()

    found = _docs.audit(base, "docs/index.md")
    cited_ids = {ident for ident, sites in found.cited.items() if any(s.path.endswith(".cs") for s in sites)}

    stated_here: dict[str, list[str]] = defaultdict(list)
    statement_words: Counter = Counter()
    for ident, sites in found.defined.items():
        stated_here[sites[0].path].append(ident)
        statement_words[sites[0].path] += len(sites[0].text.split())

    touched = last_touched(base)
    inbound = link_targets(base)
    owners = slice_of_code(base)
    known = vocabulary(base)

    gone: dict[str, Counter] = {}
    texts: dict[str, str] = {}
    docs: list[Doc] = []
    for path in _docs.documents(base):
        rel = _sim.rel(base, path)
        text = path.read_text(encoding="utf-8", errors="replace")
        texts[rel] = text
        ids = stated_here.get(rel, [])
        if missing := drifted(text, known):
            gone[rel] = missing
        docs.append(Doc(
            path=rel,
            slice=owning_slice(rel),
            kind=kind_of(rel),
            words=len(text.split()),
            lines=len(text.splitlines()),
            stated=len(ids),
            stated_words=statement_words.get(rel, 0),
            cited_by_code=sum(1 for i in ids if i in cited_ids),
            entries=sum(1 for line in text.splitlines() if ENTRY.match(line)),
            dated=sum(1 for line in text.splitlines() if DATED.match(line)),
            inbound=inbound.get(rel, 0),
            touched=touched.get(rel, "—"),
        ))

    slices = group(docs, owners, touched)
    report = render(docs, slices, owners, gone, repeated(texts), a)
    if a.out:
        out = Path(a.out)
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(report, encoding="utf-8")
        print(f"wrote {_sim.rel(base, out.resolve()) if out.is_absolute() else out} "
              f"({len(docs)} documents, {sum(d.words for d in docs):,} words)")
    else:
        print(report)
    return 0


def owning_slice(rel: str) -> str:
    """The slice a document is about: the folder its `docs/` hangs off, under `src/`."""
    parts = rel.split("/")
    if "docs" not in parts or parts[0] != "src":
        return PROJECT
    return "/".join(parts[1:parts.index("docs")])


def kind_of(rel: str) -> str:
    """What sort of document it is, by the only naming convention this project keeps."""
    name = rel.split("/")[-1]
    if name == "decision-log.md":
        return "decision-log"
    if name == "requirements.md":
        return "requirements"
    if name == "index.md":
        return "index"
    if rel.startswith("src/"):
        return "catalogue"
    return "root"


def slice_of_code(base: Path) -> dict[str, int]:
    """
    Lines of code owned by each slice — the nearest ancestor under `src/` that keeps a `docs/` of
    its own. The root `docs/` is the project's and owns no code, so it is not an ancestor here.
    """
    owned: Counter = Counter()
    slices = {p.parent for p in (base / "src").rglob("docs") if p.is_dir()}
    for path in (base / "src").rglob("*"):
        if path.suffix not in CODE or _docs.SKIP & set(path.relative_to(base).parts):
            continue
        home = next((p for p in path.parents if p in slices), None)
        name = _sim.rel(base, home)[len("src/"):] if home else UNSLICED
        owned[name] += len(path.read_text(encoding="utf-8", errors="replace").splitlines())
    return dict(owned)


def vocabulary(base: Path) -> set[str]:
    """Every name the tree declares, as a document would quote it — the head of a dotted path."""
    found: set[str] = set()
    for path in base.rglob("*"):
        if path.suffix not in DECLARED or _docs.SKIP & set(path.relative_to(base).parts):
            continue
        found.update(re.findall(r"\b[A-Z][a-z][A-Za-z0-9]{2,}\b",
                                path.read_text(encoding="utf-8", errors="replace")))
    return found


def drifted(text: str, known: set[str]) -> Counter:
    """The names a document quotes as the code's that the code no longer has."""
    gone: Counter = Counter()
    for match in QUOTED.finditer(text):
        head = match.group(1).split(".")[0]
        if head not in known:
            gone[head] += 1
    return gone


class Repeat(NamedTuple):
    """A passage more than one document carries, and everywhere it is carried."""

    words: int
    where: tuple[str, ...]
    text: str


def repeated(texts: dict[str, str]) -> list[Repeat]:
    """
    Passages of `SHINGLE` words or more that appear in two or more documents, longest first.

    Two documents saying the same thing is the consolidation question asked mechanically: one of
    them is the owner and the other is a copy that will disagree with it. Runs are grown so a
    repeated paragraph is reported once at its full length rather than as every window over it.
    """
    words = {path: WORD.findall(text.lower()) for path, text in texts.items()}
    index: dict[tuple[str, ...], set[str]] = defaultdict(set)
    for path, run in words.items():
        for start in range(len(run) - SHINGLE + 1):
            index[tuple(run[start:start + SHINGLE])].add(path)

    found: dict[tuple[str, ...], Repeat] = {}
    for path, run in words.items():
        start = 0
        while start <= len(run) - SHINGLE:
            here = index[tuple(run[start:start + SHINGLE])]
            if len(here) < 2:
                start += 1
                continue
            end = start
            while (end + 1 <= len(run) - SHINGLE
                   and index[tuple(run[end + 1:end + 1 + SHINGLE])] >= here):
                end += 1
            passage = tuple(run[start:end + SHINGLE])
            if passage not in found or len(here) > len(found[passage].where):
                found[passage] = Repeat(len(passage), tuple(sorted(here)), " ".join(passage))
            start = end + 1
    return sorted(found.values(), key=lambda r: (-r.words, r.where))


def link_targets(base: Path) -> dict[str, int]:
    """How many documents link to each document — reach, as the tree itself states it."""
    counted: Counter = Counter()
    for path in _docs.documents(base):
        for target in LINK.findall(path.read_text(encoding="utf-8", errors="replace")):
            resolved = (path.parent / target.strip()).resolve()
            if resolved.is_dir():
                resolved = resolved / "index.md"
            if resolved.is_file() and resolved.suffix == ".md":
                counted[_sim.rel(base, resolved)] += 1
    return dict(counted)


def last_touched(base: Path) -> dict[str, str]:
    """The date each tracked path last changed, from one walk of the log."""
    found: dict[str, str] = {}
    result = git.probe(base, "log", "--name-only", "--format=%x00%as", "--no-merges")
    if result.code != 0:
        return found
    date = "—"
    for line in result.stdout.splitlines():
        if line.startswith("\0"):
            date = line[1:].strip() or date
        elif line.strip():
            found.setdefault(line.strip(), date)
    return found


def group(docs: list[Doc], owners: dict[str, int], touched: dict[str, str]) -> list[Slice]:
    """Documents gathered under the slice they are about, newest code date carried alongside."""
    held: dict[str, list[Doc]] = defaultdict(list)
    for doc in docs:
        held[doc.slice].append(doc)
    out = []
    for name, members in held.items():
        dates = [d for path, d in touched.items()
                 if path.startswith(f"src/{name}/") and Path(path).suffix in CODE]
        out.append(Slice(name, members, owners.get(name, 0), max(dates, default="—")))
    return sorted(out, key=lambda s: -s.words)


def render(docs: list[Doc], slices: list[Slice], owners: dict[str, int],
           gone: dict[str, Counter], twice: list[Repeat], a) -> str:
    """The whole measurement as markdown — the same text whether it is printed or written out."""
    words = sum(d.words for d in docs)
    lines = sum(d.lines for d in docs)
    stated = sum(d.stated for d in docs)
    cited = sum(d.cited_by_code for d in docs)
    code = sum(s.code_lines for s in slices)

    out = ["# Documentation, measured", ""]
    out += [f"{len(docs)} documents, {words:,} words, {lines:,} lines. "
            f"{stated} rules stated, {cited} of them cited by code ({pct(cited, stated)}). "
            f"{code:,} lines of code sit in a slice that documents itself; "
            f"{owners.get(UNSLICED, 0):,} sit under none.", ""]

    out += ["## By kind", "", "| Kind | Docs | Words | Share | Rules | Cited by code |", "|---|--:|--:|--:|--:|--:|"]
    by_kind: dict[str, list[Doc]] = defaultdict(list)
    for doc in docs:
        by_kind[doc.kind].append(doc)
    for kind, members in sorted(by_kind.items(), key=lambda kv: -sum(d.words for d in kv[1])):
        w = sum(d.words for d in members)
        s = sum(d.stated for d in members)
        c = sum(d.cited_by_code for d in members)
        out.append(f"| {kind} | {len(members)} | {w:,} | {pct(w, words)} | {s} | {c} |")

    out += ["", "## By slice", "",
            "| Slice | Docs | Words | Share | Code lines | Words/100 lines | Rules | Cited | Doc touched | Code touched |",
            "|---|--:|--:|--:|--:|--:|--:|--:|---|---|"]
    for s in slices:
        newest = max((d.touched for d in s.docs), default="—")
        density = "—" if not s.code_lines else f"{s.density:.0f}"
        out.append(f"| {s.name} | {len(s.docs)} | {s.words:,} | {pct(s.words, words)} | "
                   f"{s.code_lines or '—'} | {density} | {s.stated} | {s.cited_by_code} | "
                   f"{newest} | {s.code_touched} |")
    out.append(f"| **total** | **{len(docs)}** | **{words:,}** | | **{code:,}** | "
               f"**{100.0 * words / code:.0f}** | **{stated}** | **{cited}** | | |")

    out += ["", "## Longest documents", "",
            "`Rule words` is what the statements themselves come to; the rest of a requirements "
            "document is the prose around them.", "",
            "| Document | Words | Rules | Rule words | Rule share | Cited | Entries | Inbound | Touched |",
            "|---|--:|--:|--:|--:|--:|--:|--:|---|"]
    for doc in sorted(docs, key=lambda d: -d.words)[: a.limit or None]:
        out.append(f"| {doc.path} | {doc.words:,} | {doc.stated} | {doc.stated_words or '—'} | "
                   f"{pct(doc.stated_words, doc.words) if doc.stated else '—'} | {doc.cited_by_code} | "
                   f"{doc.entries or '—'} | {doc.inbound} | {doc.touched} |")

    logs = sorted((d for d in docs if d.kind == "decision-log"), key=lambda d: -d.words)
    out += ["", "## Decision logs", "",
            "| Log | Words | Entries | Dated | Words/entry | Inbound | Touched |",
            "|---|--:|--:|--:|--:|--:|---|"]
    for doc in logs:
        each = f"{doc.words // doc.entries:,}" if doc.entries else "—"
        out.append(f"| {doc.path} | {doc.words:,} | {doc.entries or '—'} | {doc.dated} | {each} | "
                   f"{doc.inbound} | {doc.touched} |")

    orphans = [d for d in docs if not d.inbound and d.kind != "root"]
    if orphans:
        out += ["", "## Nothing links here", ""]
        out += [f"- {d.path} — {d.words:,} words" for d in sorted(orphans, key=lambda d: -d.words)]

    if gone:
        out += ["", "## Names the code no longer has", "",
                "A log naming what was deleted is doing its job; a requirement doing it is stale.", "",
                "| Document | Names | Which |", "|---|--:|---|"]
        for path, names in sorted(gone.items(), key=lambda kv: -sum(kv[1].values())):
            which = ", ".join(f"`{n}`" for n, _ in names.most_common(6))
            out.append(f"| {path} | {sum(names.values())} | {which} |")

    if twice:
        shown = [r for r in twice if r.words >= a.passage]
        out += ["", f"## Said in more than one place — {len(shown)}", "",
                f"Passages of {a.passage} words or more carried by two or more documents. One of them "
                "owns the thing; the rest are copies that will disagree with it.", "",
                "| Words | Documents | Passage |", "|--:|---|---|"]
        for repeat in shown[: a.limit or None]:
            text = repeat.text if len(repeat.text) <= 120 else repeat.text[:117] + "…"
            out.append(f"| {repeat.words} | {'<br>'.join(repeat.where)} | {text} |")

    silent = [d for d in docs if d.stated and not d.cited_by_code]
    if silent:
        out += ["", "## States rules no code cites", ""]
        out += [f"- {d.path} — {d.stated} rules, {d.words:,} words"
                for d in sorted(silent, key=lambda d: -d.stated)]

    if a.docs:
        out += ["", "## Every document", "",
                "| Document | Slice | Kind | Words | Lines | Rules | Cited | Inbound | Touched |",
                "|---|---|---|--:|--:|--:|--:|--:|---|"]
        for doc in sorted(docs, key=lambda d: d.path):
            out.append(f"| {doc.path} | {doc.slice} | {doc.kind} | {doc.words:,} | {doc.lines} | "
                       f"{doc.stated} | {doc.cited_by_code} | {doc.inbound} | {doc.touched} |")

    return "\n".join(out) + "\n"


def pct(part: int, whole: int) -> str:
    return f"{100.0 * part / whole:.0f}%" if whole else "—"


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq docstats",
        description="Measure the documentation: size, ownership, citation grip, reach and drift.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("--docs", action="store_true", help="list every document, not just the longest")
    parser.add_argument("--limit", type=cli.counting, default=20, metavar="INT",
                        help="rows in the longest-documents table (0 = no limit)")
    parser.add_argument("--passage", type=cli.counting, default=SHINGLE, metavar="INT",
                        help="shortest repeated passage worth reporting, in words")
    parser.add_argument("--out", metavar="PATH", help="write the report as markdown instead of printing it")
    return parser.parse_args()


if __name__ == "__main__":
    cli.entry(main)
