"""
Print what a C# file holds and the line ranges to read it by, or what a slice's files hold.

A whole-file read of a thousand-line partial spends more context than the answer is worth, and
most of it is never the part in question. This gives every type and member its range, so the
next read is `offset`/`limit` instead of the file. Given a directory it lists the files, longest
first — the same question asked of a slice.

Brace counting ignores comments and string literals, but it is a scanner and not a parser: a
verbatim string holding an unbalanced brace can skew the range that encloses it. The start lines
are read off the declarations themselves and are exact.
"""

from __future__ import annotations

import argparse
import re
from pathlib import Path

import _sim
from common import cli, paths

#: A declaration worth a line of outline: the modifiers this project actually writes, then
#: anything. Fields are skipped later by their lack of a body — an outline of every backing field
#: is the file again.
DECL = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:(?:public|private|protected|internal|static|readonly|sealed|abstract|virtual|override"
    r"|partial|async|unsafe|extern|new|ref|const|volatile|required|file|implicit|explicit|event)\s+)*"
    r"(?:(?P<kind>class|struct|record|enum|interface|delegate)\s+)?"
    r"(?P<rest>[A-Za-z_~][^;{]*)"
)

#: A summary line as this project writes one, on the member it belongs to.
SUMMARY = re.compile(r"<summary>\s*(.*?)\s*</summary>", re.S)

#: The markup a doc comment carries for a reader of the source, which an outline row has no room for.
MARKUP = re.compile(r"</?[a-z]+[^>]*>")


def main():
    a = parse_args()
    base = _sim.root()
    target = locate(a.path, base)

    if target.is_dir():
        return list_slice(base, target, a.limit)
    if target.suffix != ".cs":
        cli.fail(f"not a C# file: {_sim.rel(base, target)}")
    return list_file(base, target, a.limit)


def list_file(base: Path, path: Path, limit: int) -> int:
    """One file: every type and member, indented by nesting, with its range."""
    lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
    found = scan(lines)
    print(f"{_sim.rel(base, path)} — {len(lines)} lines, {len(found)} declarations")
    for level, start, end, text, summary in found[: limit or None]:
        span = f"{start:>5}-{end:<5}"
        note = f"  — {summary}" if summary else ""
        print(f"  {span} {'  ' * level}{text}{note}")
    if limit and len(found) > limit:
        print(f"  … {len(found) - limit} more (--limit=0 for all)")
    return 0


def list_slice(base: Path, path: Path, limit: int) -> int:
    """A directory: its C# files, longest first, each with the type it is mostly about."""
    sources = [(f, f.read_text(encoding="utf-8", errors="replace").splitlines()) for f in path.rglob("*.cs")]
    if not sources:
        cli.fail(f"no C# files under {_sim.rel(base, path)}")
    sources.sort(key=lambda pair: -len(pair[1]))
    for source, lines in sources[: limit or None]:
        found = scan(lines)
        members = sum(1 for level, *_ in found if level == 1)
        print(f"{len(lines):>5}  {_sim.rel(base, source):<52} {principal(found)}"
              + (f" ({members} members)" if members else ""))
    shown = f", showing {limit}" if limit and len(sources) > limit else ""
    print(f"{sum(len(lines) for _, lines in sources):>5}  in {len(sources)} files under {_sim.rel(base, path)}{shown}")
    return 0


def principal(found: list[tuple]) -> str:
    """
    The type a file is mostly about: the top-level one holding the most members, not the first
    one declared — a file often opens with the small record its main type is written in terms of.
    """
    tops = [(start, end, text) for level, start, end, text, _ in found if level == 0]
    if not tops:
        return ""
    return max(tops, key=lambda top: top[1] - top[0])[2]


def scan(lines: list[str]) -> list[tuple[int, int, int, str, str]]:
    """
    Every declaration in a file as (nesting, first line, last line, text, summary).

    One pass, tracking brace depth over the code left after comments and literals are removed. A
    declaration is remembered until the brace that opens its body arrives, which is the next line
    in this project's style, or until the `;` that ends a bodiless one. Only a scope a declaration
    can appear in is read for one, which is what keeps a call inside a method body out of the
    outline.
    """
    found: list[list] = []
    stack: list[list | None] = []
    depth = 0
    pending = None
    doc: list[str] = []
    in_block = False

    for number, raw in enumerate(lines, start=1):
        stripped = raw.strip()
        if stripped.startswith("///"):
            doc.append(stripped[3:].strip())
            continue

        code, in_block = uncomment(raw, in_block)
        if code.strip() and pending is None and declarable(stack) and not stripped.startswith(("//", "*", "using", "namespace")):
            match = DECL.match(code)
            if match and looks_declarative(code):
                pending = (number, signature(match), summarise(doc), bool(match.group("kind")))

        for char in code:
            if char == "{":
                depth += 1
                if pending:
                    entry = [len(stack), pending[0], pending[0], pending[1], pending[2], pending[3]]
                    found.append(entry)
                    stack.append(entry)
                    pending = None
                else:
                    stack.append(None)
            elif char == "}":
                if stack:
                    top = stack.pop()
                    if top is not None:
                        top[2] = number
                depth = max(0, depth - 1)

        if pending and code.rstrip().endswith(";"):
            found.append([len(stack), pending[0], number, pending[1], pending[2], pending[3]])
            pending = None
        if not stripped.startswith("///"):
            doc = []

    return [(level, start, end, text, summary) for level, start, end, text, summary, _ in found]


def declarable(stack: list[list | None]) -> bool:
    """Whether the scope now open can hold a declaration: the file itself, or a type's body."""
    return not stack or (stack[-1] is not None and stack[-1][5])


def looks_declarative(code: str) -> bool:
    """A member has a body, a parameter list or a type keyword; a field has none of the three."""
    if re.search(r"\b(class|struct|record|enum|interface|delegate)\b", code):
        return True
    if "(" in code:
        return not re.match(r"^\s*(if|for|foreach|while|switch|catch|lock|using|fixed|return|throw)\b", code.strip())
    return "{" in code or "=>" in code


def signature(match: re.Match) -> str:
    """The declaration as written, minus its modifiers, cut at the parameter list."""
    kind = match.group("kind")
    rest = " ".join(match.group("rest").split())
    rest = re.sub(r"\s*(\{|=>).*$", "", rest).rstrip()
    return f"{kind} {rest}" if kind else rest


def summarise(doc: list[str]) -> str:
    """The `<summary>` above a declaration, as one line short enough to sit beside its range."""
    match = SUMMARY.search(" ".join(doc))
    if not match:
        return ""
    text = " ".join(MARKUP.sub("", match.group(1)).split())
    return text if len(text) <= 72 else text[:71] + "…"


def uncomment(line: str, in_block: bool) -> tuple[str, bool]:
    """The line's code, with block comments, line comments and literals blanked out."""
    out = []
    index = 0
    while index < len(line):
        rest = line[index:]
        if in_block:
            end = rest.find("*/")
            if end < 0:
                return "".join(out), True
            index += end + 2
            in_block = False
        elif rest.startswith("/*"):
            in_block = True
            index += 2
        elif rest.startswith("//"):
            break
        elif rest.startswith(('@"', '$@"', '@$"')):
            index = skip_verbatim(line, index + rest.index('"') + 1)
        elif rest[0] in "\"'":
            index = skip_literal(line, index + 1, rest[0])
        else:
            out.append(rest[0])
            index += 1
    return "".join(out), in_block


def skip_literal(line: str, index: int, quote: str) -> int:
    """Past a regular literal, honouring backslash escapes."""
    while index < len(line):
        if line[index] == "\\":
            index += 2
            continue
        if line[index] == quote:
            return index + 1
        index += 1
    return index


def skip_verbatim(line: str, index: int) -> int:
    """Past a verbatim literal, where the only escape is a doubled quote."""
    while index < len(line):
        if line[index] == '"':
            if line[index : index + 2] == '""':
                index += 2
                continue
            return index + 1
        index += 1
    return index


def locate(candidate: str, base: Path) -> Path:
    """A path as typed, or the same name under src/ — `world/town` is how this project says it."""
    for attempt in (candidate, f"src/{candidate}"):
        path = paths.resolve_in_root(attempt, base)
        if path.exists():
            return path
    cli.fail(f"no such file or directory: {candidate}")


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq outline",
        description="Print a C# file's declarations with their line ranges, or a slice's files.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("path", nargs="?", default="src",
                        help="a .cs file, or a directory: a path from the root or a slice name like world/town")
    parser.add_argument("--limit", type=cli.counting, default=60, metavar="INT",
                        help="max rows to print (0 = no limit)")
    return parser.parse_args()


if __name__ == "__main__":
    cli.entry(main)
