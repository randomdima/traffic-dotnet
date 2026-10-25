"""
The rules as a machine can read them: where each ID is stated, and everywhere one is cited.

Private, like `_sim`: a module and not a command. One copy, because `req` looks an ID up and
`doclint` audits the same index, and two scanners would disagree about what a definition is
within a week.

A rule is *defined* where a document states it — a bold run opening a paragraph or a list item,
or the title of a catalogue page — and *cited* everywhere else it is named, in code or prose.
Nothing here writes: the documents are the index, and this only reads them.
"""

from __future__ import annotations

import re
from pathlib import Path
from typing import NamedTuple

import _sim

#: An ID where it is being defined. `**TER-4** A road…`, `- **MAN-1 — Chaining.**`,
#: `# \`P-8\` — take the junction`.
DEFINITION = re.compile(
    r"^\s*(?:[-*]\s+)?\*\*`?(?P<id>[A-Z]{1,4}-[0-9][0-9a-zA-Z.]*)`?(?=\*\*|\s|\.)"
    r"|^#{1,6}\s+`?(?P<heading>[A-Z]{1,4}-[0-9][0-9a-zA-Z.]*)`?\s*(?:`P[0-9]`\s*)?(?:—|-|$)"
)

#: A fence opening or closing a code block. **What is inside one is a sample and not a statement** —
#: docs/priority.md shows the shape of a rule line, and read as prose that sample states the very
#: rules it is quoting.
FENCE = re.compile(r"^\s*(?:```|~~~)")

#: The same shape anywhere else: a citation, in code or in another document. A dot inside an ID
#: is part of it (`TER-3c.4`); a dot after it is the end of somebody's sentence.
CITATION = re.compile(r"(?<![A-Za-z0-9])(?P<id>[A-Z]{1,4}-[0-9][0-9a-zA-Z]*(?:\.[0-9a-zA-Z]+)*)(?![A-Za-z0-9])")

#: Nothing under any of these is a document or a source of this project's own.
SKIP = {".git", ".tmp", "bin", "obj", ".qq", ".claude", ".vscode"}

#: A number named as gone is not a citation of a rule: the catalogue keeps its gaps on purpose and
#: says so in prose and in a doc comment, and every retired number it lists would otherwise read as
#: a citation of something no document states.
RETIRED = re.compile(r"\bretir(?:e|ed|es|ement)\b|\bnumbering has gaps\b", re.IGNORECASE)

#: A decision log records why a rule reads as it does and quotes its ID to do so, in the same bold
#: an unrelated document would state a rule in. History states nothing, so it is read for citations
#: and never for definitions — otherwise every logged decision looks like a second copy of the rule.
HISTORY = "decision-log.md"

#: Shapes that read as an ID and are not one: a date, a version, an enum with a number in it, a hash.
NOT_AN_ID = re.compile(r"^(?:[A-Z]{1,4}-\d{4,}|RGB-\d|UTF-\d|FNV-1a?)$")

#: The rung a rule carries, immediately after its ID: ``**TER-3d** `P0` Render geometry …``. Whose
#: the rule is and what bending it costs (docs/priority.md). Read off the definition's own line and
#: never from a table, because a rung kept anywhere but beside the statement is a second copy of it.
RUNG = re.compile(r"`(?P<rung>P[0-9])`")

#: How far past the ID a rung may sit and still be this rule's. Enough for the closing ``**`` and a
#: space; short enough that a rule quoting `P5` in its own prose is not read as carrying one.
RUNG_REACH = 8

#: The rungs that are the owner's. Nothing infers one; they are granted in as many words.
OWNER_RUNGS = ("P0", "P1")


class Site(tuple):
    """One place a rule is named: the file (relative to the root), the line, and that line."""

    __slots__ = ()

    def __new__(cls, path: str, line: int, text: str, rung: str | None = None):
        return super().__new__(cls, (path, line, text, rung))

    path = property(lambda self: self[0])
    line = property(lambda self: self[1])
    text = property(lambda self: self[2])
    #: The rung on a definition, or None where it carries none. Always None on a citation.
    rung = property(lambda self: self[3])

    def __str__(self) -> str:
        return f"{self.path}:{self.line}"


def rung_of(line: str, ident: str) -> str | None:
    """
    The rung a definition's own line carries, or None. Looked for in the few characters after the
    ID and nowhere else, so a statement that happens to mention `P5` further along is not read as
    being one.
    """
    at = line.find(ident)
    if at < 0:
        return None
    match = RUNG.search(line, at + len(ident), at + len(ident) + RUNG_REACH)
    return match.group("rung") if match else None


def definitions(base: Path) -> dict[str, list[Site]]:
    """Every ID any document defines, with the statement that defines it, in document order."""
    found: dict[str, list[Site]] = {}
    for path in documents(base):
        if path.name == HISTORY:
            continue
        lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
        fenced = False
        for number, line in enumerate(lines, start=1):
            if FENCE.match(line):
                fenced = not fenced
                continue
            if fenced:
                continue
            match = DEFINITION.match(line)
            #: A number named as retired states nothing — the catalogue explains its own gaps in the
            #: same bold a rule is stated in, and read as definitions those explanations are rules
            #: with no statement and no rung.
            if not match or RETIRED.search(line):
                continue
            ident = match.group("id") or match.group("heading")
            statement = "\n".join(paragraph(lines, number - 1))
            found.setdefault(ident, []).append(
                Site(_sim.rel(base, path), number, statement, rung_of(line, ident)))
    return found


def cited(base: Path) -> dict[str, list[Site]]:
    """Every ID named anywhere it is not being defined — the citations, code and prose alike."""
    found: dict[str, list[Site]] = {}
    for path in sources(base):
        for number, line in enumerate(path.read_text(encoding="utf-8", errors="replace").splitlines(), start=1):
            if DEFINITION.match(line) or RETIRED.search(line):
                continue
            for match in CITATION.finditer(line):
                ident = match.group("id")
                if not NOT_AN_ID.match(ident):
                    found.setdefault(ident, []).append(Site(_sim.rel(base, path), number, line.strip()))
    return found


class Audit(NamedTuple):
    """What the documents and their citations disagree about, and what each side holds."""

    #: IDs more than one document states.
    twice: dict[str, list[Site]]
    #: IDs whose statement carries no rung, or an unreadable one (docs/priority.md).
    unrunged: dict[str, list[Site]]
    #: IDs the code cites that no document states.
    dangling: dict[str, list[Site]]
    #: Families the map never names, and a document stating each.
    unmapped: dict[str, str]
    #: IDs named in prose, stated nowhere, and cited by no code — a retired number being discussed,
    #: with everywhere it is discussed. A log explaining a retirement across several lines lands
    #: here, because `RETIRED` reads one line at a time; so does a genuine typo, which is why the
    #: sites are kept rather than counted.
    prose_only: dict[str, list[Site]]
    #: Everything stated, and everything citing it.
    defined: dict[str, list[Site]]
    cited: dict[str, list[Site]]

    @property
    def sound(self) -> bool:
        return not (self.twice or self.dangling or self.unmapped or self.unrunged)


def audit(base: Path, mapped_in: str) -> Audit:
    """
    The rules against their citations. Shared, because `doclint` reports this and `checks` runs it:
    two copies of the question would answer it differently the week one of them was edited.
    """
    defined = definitions(base)
    named = cited(base)
    unstated = {ident: sites for ident, sites in named.items() if ident not in defined}
    dangling = {
        ident: code for ident, sites in unstated.items()
        if (code := [site for site in sites if site.path.endswith(".cs")])
    }
    text = (base / mapped_in).read_text(encoding="utf-8", errors="replace") if (base / mapped_in).is_file() else ""
    unmapped: dict[str, str] = {}
    for ident, sites in defined.items():
        family = ident.split("-")[0]
        if text and family not in unmapped and f"{family}-" not in text:
            unmapped[family] = sites[0].path

    return Audit(
        twice={ident: sites for ident, sites in defined.items() if len(sites) > 1},
        unrunged={
            ident: bare for ident, sites in defined.items()
            if (bare := [site for site in sites if site.rung is None])
        },
        dangling=dangling,
        unmapped=unmapped,
        prose_only={ident: sites for ident, sites in unstated.items() if ident not in dangling},
        defined=defined,
        cited=named,
    )


def paragraph(lines: list[str], index: int) -> list[str]:
    """
    The statement itself: from its first line to the blank line that ends it, or to the next rule.

    Documents that state a family as consecutive one-line rules are as common here as ones that give
    each a paragraph, and without the second stop every rule in such a run would carry the rest of
    the run as its own text — `SOL-11` reading as `SOL-11`…`SOL-19`.
    """
    out: list[str] = []
    for line in lines[index:]:
        if out and (not line.strip() or DEFINITION.match(line)):
            break
        out.append(line.rstrip())
    return out


def documents(base: Path) -> list[Path]:
    """Every markdown document in the project, in a stable order."""
    return sorted(path for path in base.rglob("*.md") if not SKIP & set(path.relative_to(base).parts))


def sources(base: Path) -> list[Path]:
    """Everywhere a rule may be cited: the code, the tests and the documents."""
    return sorted(
        path for path in base.rglob("*")
        if path.suffix in (".cs", ".md") and not SKIP & set(path.relative_to(base).parts)
    )


def gist(statement: str) -> str:
    """A statement's opening clause, with its markup taken off, to sit on one line."""
    body = " ".join(statement.split())
    body = re.sub(r"^\s*[-*]\s+", "", body)
    body = re.sub(r"\*\*|`", "", body)
    body = re.sub(r"\[([^\]]*)\]\([^)]*\)", r"\1", body)
    body = re.sub(r"^[A-Z]{1,4}-[0-9][0-9a-zA-Z.]*\s*(?:P[0-9]\s*)?(?:—\s*)?", "", body)
    cut = re.split(r"(?<=[.;])\s", body)[0]
    return cut if len(cut) <= 96 else cut[:95] + "…"


def sort_key(ident: str) -> tuple:
    """`TER-5` before `TER-5a` before `TER-6`, and `TER-3c.4` where it belongs."""
    return tuple(
        (int(re.match(r"\d*", part).group() or 0), part)
        for part in re.split(r"[-.]", ident)[1:]
    )


def spelled(ident: str) -> str:
    """An ID as the documents write one: the family shouts, the suffix does not — `TER-5c`."""
    prefix, _, rest = ident.partition("-")
    return f"{prefix.upper()}-{rest}"


def identifier(value: str) -> str:
    """An argparse `type` for a rule or a family: `TER-5c`, `P-8`, or a bare `TER`."""
    if not re.fullmatch(r"[A-Za-z]{1,4}(-[0-9][0-9a-zA-Z.]*)?", value):
        from argparse import ArgumentTypeError

        raise ArgumentTypeError(f"expected a rule ID like TER-5c or a prefix like TER: got {value!r}")
    return value
