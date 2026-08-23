# Goals

What this project is for, what it refuses to be, and the two rules every other decision falls out of.
The rules it holds the *town* to are [requirements.md](requirements.md) and the slices'
own; how anything is checked is [verification.md](verification.md); why any of it reads as it does is
[decision-log.md](decision-log.md).

## What it is

A top-down multi-agent traffic simulation of a small town — pedestrians, cars and traffic lights, all
three of them agents — whose purpose is to **make emergent behaviour observable**. Nothing is
choreographed: a car stops because *it* can see something in front of it, a walker crosses because *it*
judged a gap. There is no global traffic controller, no scheduler deciding who goes next, no scripted
queue.

Everything else is detail on one idea: **the town's behaviour is emergent, so the rules have to be local
and the failures have to be recoverable.** A build that reaches the same picture by any other route has
built something that looks like this and is not it.

And it is written with **no engine under it**: C# on .NET 10, managed from the decision loop to the bytes
the GPU reads, with the unmanaged boundary at the graphics driver and the window and nowhere else. No
Godot, no MonoGame, no Unity, no retained renderer, no ECS framework — nothing that wants to own the
loop, the window, the update order or the object model. The physics is not a package either:
[world/physics/](../src/world/physics/) is this project's own broad phase, narrow phase and contact solver.

## The quality bar

**Plausibility, not fidelity.** A human watching the town should find the layout and the agents'
decisions unsurprising. That is what every judgement call is settled against, and it is why several
figures are frankly unrealistic — a walker moves at a run because a town watched at speed is a town whose
behaviour can be seen in a minute rather than an hour.

Where realism and observability conflict, **observability wins**.

## The two engineering rules

Neither is negotiable without retiring the point of the project, and each has a gate in
[tests/gates/](../src/tests/gates/) rather than a habit.

### 1. The frame's managed→native crossing count is O(1) in the size of the town

The boundary is not forbidden — a window has to be opened and a buffer has to reach a GPU — it is
**counted, bounded and constant**. A frame that makes one call per car is this project's cardinal sin.

**A low-level API earns its place by being quiet, not by being low-level.** Owning the upload path and
making the frame's call count a constant is the goal; being close to the metal is a side effect. How that
is arranged is [Runtime](../src/runtime/docs/requirements.md).

### 2. The steady state allocates nothing

Not *little* — nothing. A tick and a frame in a standing town show a flat allocation counter: the roster
is laid once as structure-of-arrays, transient working sets come from a pool or the stack, and the hot
path holds no LINQ, no iterator, no closure, no `params`, no boxing and no interface call the JIT cannot
devirtualise.

This is a rule rather than an optimisation because **a GC pause is a measurement destroyed**. A figure
taken across a collection is a figure nobody can quote, and a project that has to explain its outliers
has not answered the question it was built for.

## What is pinned, and what is left open

**Structure of arrays, everywhere.** This is the one thing the requirements deliberately leave open that
this project settles ([decision-log.md](decision-log.md)). Everything else — the renderer's API, the
window, the typeface, the test runner, the solver — is decided on the two rules above.

## What it is not

- **Not a game.** No player character, no objective, no score, no win or lose. There is a hand that can
  take over one unit at a time, and that is a debugging instrument with an interface.
- **Not a scientific simulation.** Nothing is calibrated against real traffic data and there are no
  accuracy targets.
- **Not a framework, and not a library anybody else is meant to use.** Two pipelines and a handful of
  draw calls is a finding, not an untidiness to hide behind an abstraction.
- **Not a general-purpose .NET benchmark.** It measures this town, on this machine, and every figure it
  prints is quoted with the census that says whether the town it ran was a town.
