# The kernel — requirements

Units, the two seeds, the tick and the clock that spreads the town's thinking across it. **Nothing here
knows about a town**: `core/` is the frame everything else is written in, and a type that needs to know
what a junction is does not belong in it ([docs/slice-map.md](../../../docs/slice-map.md)). Reading a
`.town` file is `citygen`'s for exactly that reason; what `core/persistence/` keeps is the cursor that
walks the bytes.

## Units and coordinates

**SIM-3** Units are **metric throughout**: metres, seconds, metres per second and per second squared,
radians or degrees stated in the name.

- **A field's unit is part of its name** — `…M`, `…Mps`, `…Mps2`, `…S`, `…Deg` for authored figures and
  `…Px` for runtime ones. The one bug this prevents is silent.
- **The metre-to-pixel factor is a single global constant with one conversion site.** Runtime state may
  be kept in either space, but nothing converts twice and nothing converts by hand.
- **`+y` is down**, the ordinary 2D canvas convention; headings are measured from `+x`, turning toward
  `+y`. Flipping the convention flips every arc's curvature sign.
- **Every size is derived from the car's width.** Ratios are normative in *form* — one constant rescales
  the whole town — even where their values are not. A variant car may be smaller than the standard box
  but never larger, because lane and junction geometry is assembled once for the whole fleet against it.

## Where a figure lives

Every number the simulation runs on is on `SimConfig`. Its shape says which kind it is: the nested groups
are **authored**, and they are the only figures the override file may set; everything on the root is
**derived** from them. That is why moving one authored ratio moves the whole town, and why the override
file refuses a derived key.

**A literal in behaviour code is a defect**, and a number that exists in two places eventually disagrees
with itself.

## The two random streams

**SIM-4** Exactly **two independent, seeded, reproducible streams**, and which one a draw comes from is
part of the specification of that draw:

| Stream | Owns |
|---|---|
| **World seed** | Everything about the *place*: layout, placement, sizes and capacities, which look a body wears, initial placement, signal phase offsets |
| **Agent seed** | Everything about the *behaviour*: destination choice, walk-or-drive, dwell times, per-agent jitter on every patience clock |

Both are settable independently, so a layout can be replayed with different behaviour and the same
behaviour tried on a different layout.

**AGT-6** All agent randomness draws from the agent stream, and **nothing else in the program holds an
unseeded generator**. Each placement pass takes its own derived sub-stream, so adding a pass does not
shift the draws of the passes after it.

## The tick

**Fixed timestep, 60 Hz.** Agents think in the physics tick, so behaviour and physics share one timeline.

**The loop order is fixed and it matters:**

1. read the player's direct input, so the keys land before the decisions they feed;
2. rebuild the **proximity index** from the body roster — where every walker and moving car is this tick,
   with its velocity and its half-width;
3. `Decide` for every non-terminal agent, in a stable roster order;
4. `Step` every body — this is where impulses are applied;
5. end-of-tick contact arbitration → damage.

Nothing in the index survives a tick and no body moves before step 4, so **every decision in a tick is
taken against the same instant of the world**. A host with no index — a test fixture, a single-agent rig
— reports "nothing nearby" rather than crashing.

Two traps that are silent in the tick they happen and simply wrong in the next:

- **A body spawned into a running world skips its first tick.** Expect the mass of a newly created body to
  reach the solver a step behind the code that set it, which divides the first impulse by a default mass
  and applies it at hundreds of times the intended magnitude. Nothing in this town has anywhere to be
  inside 16 ms.
- **Never place a body at a velocity it did not accelerate into.** Drive it up to speed instead; a tyre
  model reading a velocity no wheel produced reports an acceleration no tyre could have caused.

## The decision clock

**Bodies move every tick. Manoeuvre *procedures* do not.** Each agent runs its catalogue every
`AgentDecisionIntervalS`, staggered by the agent's own index so the town's thinking spreads across the
ticks rather than spiking on one. Three rules hold it honest:

- **It is stated in seconds, never in ticks**, because what it bounds is how far the world moves under a
  stale answer.
- **It is a floor on the rate, never a ceiling.** A manoeuvre declares for itself that it runs every tick,
  and two kinds always do: one **negotiating with something that is itself moving**, and one **steering to
  a pose**.
- **Hard rules and the junction reservation are asked every tick regardless.** Setting the interval to 0
  must make every agent think every tick and reproduce the un-clocked town exactly; that equivalence is
  the test that the clock changed no behaviour it should not have.

## Determinism, and how far it goes

Generation and the agent decision *sequence* are reproducible by construction (SIM-4), and a frame-
identical run additionally needs the fixed timestep and the stable order, both present. **Manual orders
and hand driving legitimately fork the timeline** (CTL-6), so seeded runs are reproducible *unattended*
only.

Two things worth planning for:

- In a deterministic chaotic town, **"bit-identical" and "nearly identical" are different kinds of
  change**. An optimisation preserving the exact arithmetic can be proved neutral by running it; one that
  merely rounds differently produces an identical town for thousands of ticks and a visibly different one
  by thirty-six thousand.
- **The JIT selects instructions for the machine it runs on**, so the same binary is not the same
  arithmetic on two different CPUs. Any claim about reproducibility is a claim about one machine unless
  it says otherwise.
