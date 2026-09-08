# Requirement priority

**Every requirement says whose it is.** A rule states a relation; a priority states what bending it
costs, and — at the top of the ladder — who has to be asked. It is written as a backticked rung
directly after the ID, and nothing else on the line moves:

```
**TER-3d** `P0` Render geometry does not overlap.
**TER-7a** `P3` A band of ground ends where its own line ends, square across …
```

The rungs, the marker and the check are here; what each rule *says* stays in the document that owns it
([index.md](index.md)).

## Why the scale exists

The documents in this project are mostly written by an assistant, and read back later nothing
distinguishes a rule the owner asked for from one the assistant inferred and then cited as though it had
been handed down. The second kind then argues against the first. **The rung is the record of where a rule
came from**, so a rule can be weighed without anyone having to remember the conversation it was written
in.

## The ladder

**`P0` and `P1` are the owner's. `P2`–`P9` are the assistant's.** That split is the whole point of the
scale and it is not a matter of degree: the top two rungs record authority and the rest record
consequence.

| Rung | Whose | What bending it costs |
|---|---|---|
| `P0` | the owner, stated explicitly | Nothing bends it. Not for an edge case, not for a test, not for a deadline. **It changes only when the owner asks for the change**, and an assistant that finds it inconvenient reports the conflict and stops. |
| `P1` | the owner, stated softly | It may be bent at a genuine edge case, in a fixture or in a test, and **the bend is written down** in the nearest `decision-log.md`. It may not be bent because something else was easier. |
| `P2` | the assistant | An engine rule, or its direct consequence. Bending it is a change to [goals.md](goals.md) first, and there is a gate in [tests/gates/](../src/tests/gates/) that fails before the habit does. |
| `P3` | the assistant | The town stops being sound — geometry that disagrees with itself, a body hurt by arithmetic that is wrong, ground granted twice. |
| `P4` | the assistant | Two answers to one question: a second representation, a figure in two places, a derived observable authored as an input. |
| `P5` | the assistant | An agent does not do its job. The town runs and is not a town. |
| `P6` | the assistant | The town is laid or drawn wrong. The simulation is right and the picture or the plan of it is not. |
| `P7` | the assistant | The player's hands, the camera, the interface. |
| `P8` | the assistant | An instrument lies — a probe, a claim, a caption, a debug layer reporting something other than what happened. |
| `P9` | the assistant | A detail, or a preference that could have gone the other way. |

**A rule that could be argued into two assistant rungs takes the lower-numbered one**, for the same
reason the test ladder does: over-stating costs care nobody needed and under-stating costs a defect
nobody looked for. **A rule may never be argued into `P0` or `P1`** — those are granted, not deduced.

## Two ladders spelled the same way

`P0`–`P9` also names the test ladder ([tests/Priority.cs](../src/tests/Priority.cs)), which asks a
different question — *what is the town if this test is wrong?* — of a different subject. **A requirement
carries a rung; a test class carries a rung; neither is read off the other**, and a `P0` requirement may
well be guarded by a `P5` test class. Where the two could be confused, the axis is named: a *requirement
rung* and a *test rung*.

## How a rung is granted, changed and checked

- **The owner grants `P0` and `P1` by saying so**, in as many words, about a rule or about a statement
  that then becomes one. Nothing else grants them — not a strongly worded document, not a rule the code
  leans on heavily, not an assistant's reading of what the owner would probably want.
- **A rung is never inferred upward.** An assistant may lower its own rung and may not raise a rule into
  the owner's band, and moving a rule *out* of `P0`/`P1` needs the owner just as much as moving it in.
- **A promotion rewrites the marker in place.** No history in the requirement — that is what the nearest
  `decision-log.md` is for, and a superseded rung is deleted rather than annotated.
- **`qq doclint` fails on a rule with no rung or an unreadable one**, so the scale cannot rot back into
  prose. `qq req <ID>` prints the rung with the statement, and `qq req --rungs` lists the owner's band —
  which is the whole of the `P0`/`P1` register, held nowhere else.

## Where a conflict goes

**A `P0` that the code does not meet is reported, not quietly reinterpreted.** The rule stands as
written and the code is what is wrong; whether it gets fixed now is the owner's call, and the gap is
named in [index.md](index.md#known-gaps) until it closes. An assistant that finds a `P0` expensive says
so and stops — it does not restate the rule in terms it can satisfy, which is the failure this whole
scale was added to make visible.
