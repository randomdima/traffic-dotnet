"""
The doc comments' own citations as a machine can read them: every `<see cref="…"/>`, and whether
what it names is still there.

A cref is a citation exactly as a rule ID is, and nothing checks one either: the build generates no
XML documentation, so `CS1574` never fires, and a member that is renamed leaves every comment
naming it pointing at nothing. Private, like `_docs`: a module and not a command.

It is a scanner and not a compiler, and it is lenient in four places on purpose. A cref is weighed
only where this project declares the type it names, so `List{T}.Add` is somebody else's to answer
for. A member is looked for as a word anywhere in the source of every file declaring that type —
partial types are one text, and two types of one name are read as one — with the doc comments
stripped out first, so that a citation cannot vouch for itself. An unqualified cref is offered
every type its own file declares. And a member none of them states is looked for in whatever their
declarations name after the colon, so an inherited one still resolves. Each of those can pass
something it should not; none of them can fail something correct, which is the side a gate belongs
on.

The one thing weighed about a qualifier is whether this project has it at all: a cref ending in a
type of ours, written with a segment that is neither a namespace of ours nor an enclosing type,
names where that type used to live. Nothing else about a qualifier is checked — a cref may name as
much or as little of the path as it likes, which is what `_strayed` is careful to allow. And the
converse: a cref qualified by a namespace of ours whose last word is no type of ours names a type
that was deleted (`_gone`), since nothing but this project's types lives in its namespaces.

Before any of that, a cref naming a word no line of code says at all is dangling outright
(`_unheard`): whatever declares or uses a type or member says its name somewhere, so a name only a
doc comment says was deleted or renamed. A library's member is let off, being citable unused.
"""

from __future__ import annotations

import re
from pathlib import Path

import _docs
import _sim

#: One citation in a doc comment. The generic parameters and the argument list are part of how a
#: cref is written and not part of the name: `Spline.CrossingsOf(in ArcSeg, …)` names `CrossingsOf`,
#: and `Span{T}` names `Span`.
CREF = re.compile(r'cref="(?P<name>[^"]+)"')

#: A type this project declares, and whatever its declaration carries after the name — which is
#: read for a colon, and so for the bases and interfaces an inherited member could be stated on.
TYPE = re.compile(
    r"\b(?:class|struct|record|interface|enum)\s+(?P<name>[A-Z][A-Za-z0-9_]*)(?P<rest>[^{\r\n]*)")

#: Where this project puts its types. Read for the segments a qualified cref may be spelled with,
#: so that a namespace a move left behind is a citation naming nothing rather than one nobody weighs.
NAMESPACE = re.compile(r"^\s*namespace\s+(?P<name>[A-Za-z0-9_.]+)", re.MULTILINE)


def scan(base: Path) -> tuple[int, dict[str, list[_docs.Site]]]:
    """
    Every cref in the project, and the ones naming a member their type does not have — keyed by the
    cref as it is written, with every place it is written that way.
    """
    files = _sources(base)
    body: dict[str, str] = {}
    beside: dict[str, set[str]] = {}
    segments: set[str] = set()
    namespaces: set[str] = set()
    for path, text in files:
        stripped = _stripped(text)
        for match in TYPE.finditer(text):
            name = match.group("name")
            body[name] = body.get(name, "") + stripped
            beside.setdefault(name, set()).update(_after_colon(match.group("rest")))

        for match in NAMESPACE.finditer(text):
            namespaces.add(match.group("name"))
            segments.update(match.group("name").split("."))

    heard = _words(files)
    counted = 0
    dangling: dict[str, list[_docs.Site]] = {}
    for path, text in files:
        mine = {match.group("name") for match in TYPE.finditer(text)}
        for number, line in enumerate(text.splitlines(), start=1):
            for match in CREF.finditer(line):
                counted += 1
                cref = match.group("name")
                if _unheard(cref, heard, body):
                    dangling.setdefault(cref, []).append(
                        _docs.Site(_sim.rel(base, path), number, line.strip()))
                    continue

                member, owners = _asks(cref, mine, body, segments)
                if member is None:
                    if not (_strayed(cref, body, segments) or _gone(cref, body, namespaces)):
                        continue
                elif _holds(body, beside, owners, member):
                    continue

                dangling.setdefault(cref, []).append(
                    _docs.Site(_sim.rel(base, path), number, line.strip()))

    return counted, dangling


def _asks(cref: str, mine: set[str], body: dict[str, str], segments: set[str]) -> tuple[str | None, set[str]]:
    """
    What one cref has to be true of: the member it names and the types that could state it, or no
    member at all where it names a type or a type this project does not declare.
    """
    bare = re.sub(r"[({].*", "", cref).strip()
    if not bare:
        return None, set()

    owner, _, member = bare.rpartition(".")
    if owner:
        # `TrafficSimulation.Core.Geometry.Spline` ends in a namespace and a type, not a member — and so does
        # `Road.LanePlaces`, a type of the `World.Road` namespace, however many types are named `Road` too.
        held = owner.rpartition(".")[2]
        if member in body and held in segments:
            return None, set()
        return (member, {held}) if held in body else (None, set())

    # Unqualified: a type if this project has one of that name, and otherwise a member of whichever
    # types the file it is written in declares — which is how this project cites a neighbour.
    return (None, set()) if bare in body or not mine else (bare, mine)


def _strayed(cref: str, body: dict[str, str], segments: set[str]) -> bool:
    """
    Whether a cref naming one of this project's types is spelled with a qualifier the project does
    not have. `Core.Geometry.BucketGrid` is that type where it lives and `Shared.Spatial.BucketGrid`
    is where it lived once — the same citation, and one of them stops resolving the day the type is
    moved. Weighed only where the last word is a type of ours, so a member and anything somebody else
    declares are left where `_asks` left them; and a segment is allowed to be an enclosing type as
    well as part of a namespace, since that is how a nested type is written.
    """
    bare = re.sub(r"[({].*", "", cref).strip()
    parts = bare.split(".")
    if len(parts) < 2 or parts[-1] not in body:
        return False

    return any(part not in body and part not in segments for part in parts[:-1])


def _words(files: list[tuple[Path, str]]) -> set[str]:
    """Every identifier the project's code says anywhere, doc comments left out."""
    heard: set[str] = set()
    for _, text in files:
        heard.update(re.findall(r"[A-Za-z_][A-Za-z0-9_]*", _stripped(text)))
    return heard


def _unheard(cref: str, heard: set[str], body: dict[str, str]) -> bool:
    """
    Whether a cref names something no line of code says at all: a type or member that was deleted or
    renamed, `RightOfWay.Emergency` once the enum went. A qualifier is a namespace or a type, and the
    code that uses either says its name. The last word is weighed only where its owner is one of this
    project's types, since a library's member may be cited without this code ever calling it
    (`Environment.TickCount64`).
    """
    parts = [part for part in re.sub(r"[({].*", "", cref).strip().split(".") if part]
    if not parts:
        return False

    if any(part not in heard for part in parts[:-1]):
        return True

    return parts[-1] not in heard and (len(parts) == 1 or parts[-2] in body)


def _gone(cref: str, body: dict[str, str], namespaces: set[str]) -> bool:
    """
    Whether a cref names a type in one of this project's namespaces that the project no longer
    declares. `CityGen.SkidpadPlan` is qualified by a namespace of ours, so the last word can only be
    a type of ours, and no file declares one. A cref naming a namespace itself is left alone.
    """
    bare = re.sub(r"[({].*", "", cref).strip()
    qualifier, _, last = bare.rpartition(".")
    if not qualifier or last in body:
        return False

    def ours(path: str) -> bool:
        return any(ns == path or ns.endswith("." + path) for ns in namespaces)

    return ours(qualifier) and not ours(bare)


def _holds(body: dict[str, str], beside: dict[str, set[str]], owners: set[str], member: str) -> bool:
    """Whether any of those types, or anything their declarations stand on, states such a member."""
    word = re.compile(rf"\b{re.escape(member)}\b")
    seen: set[str] = set()
    pending = list(owners)
    while pending:
        name = pending.pop()
        if name in seen or name not in body:
            continue

        seen.add(name)
        if word.search(body[name]):
            return True

        pending.extend(beside.get(name, ()))

    return False


def _sources(base: Path) -> list[tuple[Path, str]]:
    """Every C# file of this project, read once — the scan asks two questions of each."""
    return [
        (path, path.read_text(encoding="utf-8", errors="replace"))
        for path in sorted(base.rglob("*.cs"))
        if not _docs.SKIP & set(path.relative_to(base).parts)
    ]


def _stripped(text: str) -> str:
    """
    One file's code without its doc comments, and so without the crefs in them. A cref names a
    member the file may well not state, so read with the comments left in, every dangling citation
    is its own evidence that what it names exists.
    """
    return "\n".join(line for line in text.splitlines() if not line.lstrip().startswith("///"))


def _after_colon(rest: str) -> list[str]:
    """
    The names a declaration stands on, as the bare identifiers they are made of: everything past
    the colon that closes the type's own name, its generic parameters and its primary constructor.
    """
    depth = 0
    for at, letter in enumerate(rest):
        if letter in "(<[":
            depth += 1
        elif letter in ")>]":
            depth -= 1
        elif letter == ":" and depth == 0:
            return re.findall(r"[A-Z][A-Za-z0-9_]*", rest[at + 1:])

    return []
