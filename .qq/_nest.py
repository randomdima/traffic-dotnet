"""
Put back the frames perf's stacks lose, read off the source's own calls.

perf walks a JIT'd stack by frame pointers, and two things drop a frame from it: the JIT inlining a method into its
caller, and a method compiled without a frame of its own, whose caller is then skipped on every sample beneath it
(`LaneOccupancy::Answer` reads as called from `SimLoop::AdvanceTimed`, not `TownWorld::TakeTheTurns`). Where a
frame's body under `src/` holds no call that can land on the frame perf put beneath it, the shortest chain of calls the
source does hold between the two is put in: as perf spells it where perf saw that method as a frame elsewhere (a
frameless callee hid it here), and as `(Type::Method)` where it never did (inlined). Where two chains fit as well as
each other nothing is put in - perf's edge is short a frame, which is less wrong than a guessed one.

The call graph is a scanner's, not a compiler's. A method is its type and name, a property its getter (`get_View`), and
a call a PascalCase name before `(` - or, for a getter, without one. A call on nothing binds to the type's own member
and its overrides in the types derived from it; one on a field or a type to the members of the types that name names
(`_following` → `Following`), and to every member of the name where it names none.
"""

from __future__ import annotations

import functools
import re
from collections import defaultdict, deque
from pathlib import Path

import outline

CALL = re.compile(r"(?:(\w+)\s*\??\.\s*)?\b([A-Z_]\w*)\s*(?:<[^<>();]*>)?\s*\(")
#: A property read: a PascalCase name with no parameter list or type arguments after it.
READ = re.compile(r"(?:(\w+)\s*\??\.\s*)?\b([A-Z_]\w*)\b(?!\s*[(<\w])")
MEMBER = re.compile(r"(\w+)\s*(?:<[^<>]*>)?\s*\(")
TYPE = re.compile(r"^(?:class|struct|record|interface)\s+(?:struct\s+|class\s+)?(\w+)(?:<[^>]*>)?(?:\([^)]*\))?\s*(?::\s*(.*))?")

#: Frames a chain may put in: past it a gap is more likely the scanner's than the stack's.
MOST_FILLED = 8


class CallGraph:
    def __init__(self, src: Path):
        #: Each member's calls as (name, what it is called on, or "").
        self.calls: dict[str, set[tuple[str, str]]] = defaultdict(set)
        self.named: dict[str, set[str]] = defaultdict(set)
        self.bases: dict[str, set[str]] = defaultdict(set)
        for source in src.rglob("*.cs"):
            if source.relative_to(src).parts[0] != "tests":
                self._read(source)

    def _read(self, source: Path):
        lines = source.read_text(encoding="utf-8", errors="replace").splitlines()
        code, in_block = [], False
        for line in lines:
            text, in_block = outline.uncomment(line, in_block)
            code.append(text)
        types: list[tuple[int, int, str]] = []
        for _, start, end, text, _ in outline.scan(lines):
            if m := TYPE.match(text):
                types.append((start, end, m[1]))
                self.bases[m[1]] |= {re.sub(r"<.*", "", b).strip() for b in (m[2] or "").split(",") if b.strip()}
                continue
            owner = min((t for t in types if t[0] <= start <= t[1]), key=lambda t: t[1] - t[0], default=None)
            if owner is None:
                continue
            head = text.split("(", 1)[0]
            if "(" in text:
                m = MEMBER.search(text)
                if not m or "=" in head:
                    continue
                name = m[1]
            elif "=" not in text:
                name = f"get_{text.split()[-1]}"
            else:
                continue
            key = f"{owner[2]}::{name}"
            body = " ".join(code[start - 1:end])
            self.calls[key] |= {(c, on) for on, c in CALL.findall(body) if c != name}
            self.calls[key] |= {(f"get_{c}", on) for on, c in READ.findall(body)}
            self.named[name].add(key)

    @functools.cache
    def derives(self, kind: str, base: str) -> bool:
        seen, todo = set(), [kind]
        while todo:
            k = todo.pop()
            if k == base:
                return True
            if k not in seen:
                seen.add(k)
                todo += self.bases.get(k, ())
        return False

    @functools.cache
    def resolve(self, key: str, name: str, on: str) -> frozenset[str]:
        return frozenset(self._resolve(key, name, on))

    def _resolve(self, key: str, name: str, on: str) -> set[str]:
        every = self.named.get(name, set())
        kind = key.split("::")[0]
        if not on or on == "this":
            own = {k for k in every if self.derives(k.split("::")[0], kind)}
            if own:
                return own
        receiver = on.lstrip("_").lower()
        if len(receiver) < 3:
            return every
        named = {k for k in every if receiver in (t := k.split("::")[0].lower()) or t in receiver}
        return named or every

    @functools.cache
    def calls_key(self, key: str, callee: str) -> bool:
        return any(callee in self.resolve(key, name, on) for name, on in self.calls.get(key, ()))

    @functools.cached_property
    def callers(self) -> dict[str, set[str]]:
        """Each key's callers, every call resolved: the graph walked backwards."""
        callers = defaultdict(set)
        for key, calls in self.calls.items():
            for name, on in calls:
                for landed in self.resolve(key, name, on):
                    callers[landed].add(key)
        return callers

    @functools.cache
    def reaching(self, target: str) -> dict[str, int]:
        """How few calls each key is from `target`, for the keys at most MOST_FILLED away; `target` itself is 0."""
        away, frontier = {target: 0}, {target}
        for step in range(1, MOST_FILLED + 1):
            frontier = {c for k in frontier for c in self.callers.get(k, ()) if c not in away}
            away |= dict.fromkeys(frontier, step)
        return away

    @functools.cache
    def chains(self, start: str, target: str) -> list[list[str]]:
        """Every chain of keys from `start` to `target`, the ends left out, no longer than the shortest plus one."""
        # A key is walked on from only where the target is near enough for the chain through it to be kept: the rest
        # end no chain, and walked they were every path MOST_FILLED deep — nine seconds for a pair with no chain, a
        # minute and a half of one report. A chain through a key `d` calls from the target is at least `d` keys more.
        away = self.reaching(target)
        found, depth, reached = [], None, {start: 0}
        queue = deque([(start, [])])
        while queue:
            key, chain = queue.popleft()
            if depth is not None and len(chain) > depth + 1:
                break
            for name, on in self.calls.get(key, ()):
                landed = self.resolve(key, name, on)
                if target in landed and chain:
                    found.append(chain)
                    depth = len(chain) if depth is None else depth
                    continue
                if len(chain) >= MOST_FILLED:
                    continue
                longest = MOST_FILLED if depth is None else depth + 1
                for nxt in landed:
                    if len(chain) + away.get(nxt, MOST_FILLED + 1) > longest:
                        continue
                    # A key is walked on from at its shortest reach and one past it, which is all a chain one longer
                    # than the shortest can pass through it at.
                    if nxt not in chain and len(chain) + 1 <= reached.setdefault(nxt, len(chain) + 1) + 1:
                        queue.append((nxt, chain + [nxt]))
        return found


def key_of(frame: str) -> str:
    """A folded frame as the graph names it: no generic arity, the innermost of a nested type."""
    kind, sep, method = re.sub(r"`\d+", "", frame.strip("()")).rpartition("::")
    return f"{kind.rsplit('+', 1)[-1]}::{method}" if sep else frame


def repair(stacks: list[tuple[list[str], int]], src: Path) -> tuple[list[tuple[list[str], int]], int]:
    """The stacks with the frames the source says are missing put in, and how many samples needed one."""
    graph = CallGraph(src)
    spelled = {key_of(f): f for path, _ in stacks for f in path}
    framed = {key_of(b) for path, _ in stacks for a, b in zip(path, path[1:]) if graph.calls_key(key_of(a), key_of(b))}
    chosen: dict[tuple[str, str, frozenset], list[str] | None] = {}
    repaired, touched = [], 0
    for path, n in stacks:
        out, filled = path[:1], False
        types = frozenset(key_of(f).split("::")[0] for f in path)
        for frame in path[1:]:
            caller, callee = key_of(out[-1]), key_of(frame)
            if caller in graph.calls and callee in graph.calls and not graph.calls_key(caller, callee):
                cache = (caller, callee, types)
                if cache not in chosen:
                    chosen[cache] = best(graph.chains(caller, callee), set(spelled), types, callee in framed)
                if gap := chosen[cache]:
                    out += [spelled.get(k, f"({k})") for k in gap]
                    filled = True
            out.append(frame)
        repaired.append((out, n))
        touched += n if filled else 0
    return repaired, touched


def best(chains: list[list[str]], seen: set[str], types: frozenset, callee_framed: bool) -> list[str] | None:
    """
    The one chain that fits best, or none where two fit as well - the shortest and one a frame longer are both chains
    the code can take, so length decides nothing: the one perf's stacks fit, then the one the stack's own types run
    through most. What perf's stacks fit - a callee perf saw under a direct caller has a frame, so what hid
    its caller here was inlining and every frame put in is one perf never saw; a callee never seen so is frameless,
    and hid a caller perf does see elsewhere.
    """
    if not chains:
        return None

    def fits(chain: list[str]) -> int:
        if callee_framed:
            return -sum(k in seen for k in chain)
        return (chain[-1] in seen) - sum(k in seen for k in chain[:-1])

    def score(chain: list[str]) -> tuple:
        return (fits(chain) == 0 if callee_framed else fits(chain) == 1,
                sum(k.split("::")[0] in types for k in chain), fits(chain))

    ranked = sorted(chains, key=score, reverse=True)
    if len(ranked) > 1 and score(ranked[0]) == score(ranked[1]) and ranked[0] != ranked[1]:
        return None
    return ranked[0]
