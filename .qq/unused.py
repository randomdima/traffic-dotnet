"""
Name the declarations under src/ nothing reads: every type, member, field and enum member whose name is
spelled nowhere but where it is declared, and those only the tests still spell.

**A scanner, not a compiler.** A name is counted as an identifier token in the code left after
comments and literals are blanked (`outline.uncomment`), so a doc comment's `cref` keeps nothing
alive and a `nameof` does. Two members sharing a name keep each other alive, so this misses dead code
and does not invent it: what it prints is a candidate to read, never a deletion to make blind. What is
called by the runtime rather than by the code — an `override`, an operator, a constructor, `Main`, a
property only a serializer writes — is either skipped or worth the read anyway.
"""

from __future__ import annotations

import argparse
import re
from collections import Counter
from pathlib import Path

import _sim
import outline
from common import cli

IDENT = re.compile(r"\b[A-Za-z_]\w*\b")

#: The modifiers a field is written with in this project, then its type, its name, and `=` or `;`.
FIELD = re.compile(
    r"^\s*(?:\[[^\]]*\]\s*)*"
    r"(?:(?:public|private|protected|internal|static|readonly|const|volatile|new|required|unsafe|fixed)\s+)+"
    r"(?!(?:partial\s+)?(?:class|struct|record|enum|interface|ref\s+struct)\b)"  # a bodiless `struct NoPush;` is a type
    r"[\w<>\[\],.?*() ]+?\s+(?P<name>[A-Za-z_]\w*)\s*(?:=(?!>)|;|\[)"
)

#: A member of an enum's body as this project writes one: one a line.
ENUM_MEMBER = re.compile(r"^\s*(?:\[[^\]]*\]\s*)*(?P<name>[A-Za-z_]\w*)\s*(?:=[^,]*)?,?\s*$")

#: Called by the runtime, the language or a framework, not by name in the code.
SPOKEN_FOR = {"Main", "Dispose", "Equals", "GetHashCode", "ToString", "CompareTo", "GetEnumerator",
              "MoveNext", "Current", "Deconstruct", "OnCreate", "OnDestroy", "OnResume", "OnPause", "Program",
              "SurfaceCreated", "SurfaceChanged", "SurfaceDestroyed", "SurfaceRedrawNeeded"}

#: A field that holds a GPU row's std430 layout and is never read by name.
PADDING = re.compile(r"^_pad\d*$")

TESTS = "src/tests/"


def main():
    a = parse_args()
    base = _sim.root()
    sources = sorted((base / "src").rglob("*.cs"))
    used_src: Counter[str] = Counter()
    used_tests: Counter[str] = Counter()
    declared: list[tuple[str, int, str, str]] = []
    declared_count: Counter[str] = Counter()

    # A figure the tools read out of the source (`qq art` reads the art grids) is in use.
    for tool in (base / ".qq").glob("*.py"):
        used_src.update(IDENT.findall(tool.read_text(encoding="utf-8", errors="replace")))

    for path in sources:
        rel = _sim.rel(base, path)
        lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
        code = blank(lines)
        tokens = Counter(IDENT.findall("\n".join(code)))
        (used_tests if rel.startswith(TESTS) else used_src).update(tokens)
        if rel.startswith(TESTS):
            continue
        for line, kind, name in declarations(lines, code):
            declared_count[name] += 1
            declared.append((rel, line, kind, name))

    under = a.path.rstrip("/")
    unused, test_only = [], []
    for rel, line, kind, name in declared:
        if under and not (rel.startswith(under) or rel.startswith(f"src/{under}")):
            continue
        if name in SPOKEN_FOR or PADDING.match(name):
            continue
        beyond = used_src[name] - declared_count[name]
        if beyond > 0:
            continue
        (test_only if used_tests[name] else unused).append((rel, line, kind, name))

    text = report(unused, test_only, a.limit)
    if a.out:
        out = base / a.out
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(report(unused, test_only, 0), encoding="utf-8")
        print(f"{len(unused)} unused, {len(test_only)} read by the tests alone → {a.out}")
    else:
        print(text, end="")
    return 0


def blank(lines: list[str]) -> list[str]:
    """
    Every line's code, with comments and literals blanked and the line count kept. An interpolated
    literal keeps its holes, which is where a formatted row calls the helpers that print it.
    """
    text = "\n".join(lines)
    out: list[str] = []
    index, size = 0, len(text)
    while index < size:
        if text.startswith("//", index):
            end = text.find("\n", index)
            index = size if end < 0 else end
        elif text.startswith("/*", index):
            end = text.find("*/", index + 2)
            stop = size if end < 0 else end + 2
            out.append("\n" * text.count("\n", index, stop))
            index = stop
        elif text[index] == "'":
            index = past_char(text, index)
        else:
            match = LITERAL.match(text, index)
            if match:
                index = literal(text, index, match.group(0), out)
            else:
                out.append(text[index])
                index += 1
    return "".join(out).split("\n")


#: The opening of a string literal: its prefix (`$`, `@`, either order, `$$`) and its quotes.
LITERAL = re.compile(r'(?:\$+@?|@\$*)?"(?:"")?')


def literal(text: str, index: int, opening: str, out: list[str]) -> int:
    """Past one string literal, writing out its newlines and, for an interpolated one, its holes."""
    interpolated = "$" in opening
    verbatim = "@" in opening
    raw = opening.endswith('"""')
    if opening.endswith('""') and not raw:
        return index + len(opening)  # the empty literal
    quotes = '"""' if raw else '"'
    index += len(opening)
    depth = 0
    while index < len(text):
        char = text[index]
        if char == "\n":
            out.append("\n")
        if depth:
            if char == "{":
                depth += 1
            elif char == "}":
                depth -= 1
                if not depth:
                    out.append(" ")
                    index += 1
                    continue
            out.append(char if char != "\n" else "")
        elif char == "\\" and not verbatim and not raw:
            index += 2
            continue
        elif interpolated and char == "{":
            if text.startswith("{{", index):
                index += 2
                continue
            depth = 1
            out.append(" ")
        elif text.startswith(quotes, index):
            if verbatim and text.startswith('""', index):
                index += 2
                continue
            return index + len(quotes)
        index += 1
    return index


def past_char(text: str, index: int) -> int:
    """Past a character literal, honouring a backslash escape."""
    index += 1
    while index < len(text) and text[index] != "'":
        index += 2 if text[index] == "\\" else 1
    return index + 1


def declarations(lines: list[str], code: list[str]) -> list[tuple[int, str, str]]:
    """(line, kind, name) of every type and member `outline.scan` finds, plus fields and enum members."""
    found = []
    for level, start, end, text, _ in outline.scan(lines):
        named = name_of(text)
        if named:
            found.append((start, *named))
    for start, end, name in enums(lines):
        for number in range(start + 1, end):
            match = ENUM_MEMBER.match(code[number - 1])
            if match and match.group("name") not in {"public", "private", "internal"}:
                found.append((number, "enum member", match.group("name")))
    for number, line in enumerate(code, start=1):
        if "(" in line.split("=")[0]:
            continue
        match = FIELD.match(line)
        if match:
            found.append((number, "field", match.group("name")))
    return found


def enums(lines: list[str]) -> list[tuple[int, int, str]]:
    return [(start, end, text) for _, start, end, text, _ in outline.scan(lines) if text.startswith("enum ")]


def name_of(signature: str) -> tuple[str, str] | None:
    """What a declaration is and the name it gives, or None for one with no name of its own to lose."""
    kind = re.match(r"^(class|struct|record struct|record|enum|interface|delegate)\s+", signature)
    if kind:
        rest = signature[kind.end():]
        if kind.group(1) == "delegate":
            match = re.search(r"([A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*\(", rest)
        else:
            match = re.match(r"(?:struct\s+)?([A-Za-z_]\w*)", rest)
        return (kind.group(1), match.group(1)) if match else None
    if re.search(r"\boperator\b|\bthis\s*\[|^~|\boverride\b", signature):
        return None
    if "(" in signature:
        head = signature.split("(")[0]
        if "=" in head:
            return None  # a field initialised by a call, which the field scan names
        head = re.sub(r"<[^<>]*>$", "", head.strip())
        names = IDENT.findall(head)
        if len(names) < 2:
            return None
        return "method", names[-1]
    names = IDENT.findall(signature)
    return ("property", names[-1]) if len(names) >= 2 else None


def report(unused: list, test_only: list, limit: int) -> str:
    lines = [f"# Unused declarations\n\n{len(unused)} spelled nowhere else; {len(test_only)} spelled only by the tests.\n"]
    for title, rows in (("## Spelled nowhere else", unused), ("## Spelled only by the tests", test_only)):
        lines.append(f"\n{title}\n")
        for rel, line, kind, name in rows[: limit or None]:
            lines.append(f"- `{rel}:{line}` {kind} `{name}`")
        if limit and len(rows) > limit:
            lines.append(f"- … {len(rows) - limit} more (--limit=0 or --out for all)")
    return "\n".join(lines) + "\n"


def parse_args():
    parser = argparse.ArgumentParser(
        prog="qq unused",
        description="Name the declarations under src/ nothing reads, and those only the tests read.",
        allow_abbrev=False,
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument("path", nargs="?", default="",
                        help="only declarations under this path or slice, like world/town (all of src/ if omitted)")
    parser.add_argument("--limit", type=cli.counting, default=40, metavar="INT",
                        help="max rows a section prints (0 = no limit)")
    parser.add_argument("--out", help="write the whole report to this markdown file instead")
    return parser.parse_args()


if __name__ == "__main__":
    cli.entry(main)
