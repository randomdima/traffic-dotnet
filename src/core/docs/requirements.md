# The kernel — requirements

Units, the two seeds, the tick and the clock that spreads the town's thinking across it. **Nothing here
knows about a town**: `core/` is the frame everything else is written in, and a type that needs to know
what a junction is does not belong in it ([docs/slice-map.md](../../../docs/slice-map.md)).

## Units and coordinates

**SIM-3** `P3` Units are **metric throughout**: metres, seconds, metres per second and per second squared,
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

Every number the simulation runs on is on `SimConfig`: the nested groups are **authored** and are the only
figures the override file may set, and everything on the root is **derived** from them. That is why moving
one authored ratio moves the whole town, and why the override file refuses a derived key. **A literal in
behaviour code is a defect.**

## The two random streams

**SIM-4** `P3` Exactly **two independent, seeded, reproducible streams**, and which one a draw comes from is
part of the specification of that draw:

| Stream | Owns |
|---|---|
| **World seed** | Everything about the *place*: layout, placement, sizes and capacities, which look a body wears, initial placement, signal phase offsets |
| **Agent seed** | Everything about the *behaviour*: destination choice, walk-or-drive, dwell times, per-agent jitter on every patience clock |

Both are settable independently, so a layout can be replayed with different behaviour and the same
behaviour tried on a different layout.

**AGT-6** `P3` All agent randomness draws from the agent stream, and **nothing else in the program holds an
unseeded generator**. Each placement pass takes its own derived sub-stream, so adding a pass does not
shift the draws of the passes after it.

## The tick

**Fixed timestep, 60 Hz.** Agents think in the physics tick, so behaviour and physics share one timeline.

**The loop order is fixed and it matters:**

1. read the player's direct input, so the keys land before the decisions they feed;
2. rebuild from the body roster what the tick knows about where bodies are — the walkers' **proximity
   index** and the lanes' **occupancy**;
3. `Decide` for every non-terminal agent, in a stable roster order;
4. `Step` every body — this is where impulses are applied;
5. end-of-tick contact arbitration → damage.

Nothing in the index survives a tick and no body moves before step 4, so **every decision in a tick is
taken against the same instant of the world**. A host with no index — a test fixture, a single-agent rig
— reports "nothing nearby" rather than crashing.

**Never place a body at a velocity it did not accelerate into.** Drive it up to speed instead; a tyre model
reading a velocity no wheel produced reports an acceleration no tyre could have caused — silent in the tick
it happens and simply wrong in the next.

## The decision clock

**Bodies move every tick. Decisions do not.** Each agent decides — its errand, and the next line of its
leg — every `AgentDecisionIntervalS`, staggered by the agent's own index so the town's thinking spreads
across the ticks rather than spiking on one. Three rules hold it honest:

- **It is stated in seconds, never in ticks**, because what it bounds is how far the world moves under a
  stale answer.
- **It is a floor on the rate, never a ceiling**: an agent may declare that it decides every tick
  (`ISimWorld.DecidesEveryTick`).
- **The hard rules, the sensing and the claims are taken every tick regardless**, and are not decisions.
  Setting the interval to 0 must make every agent decide every tick and reproduce the un-clocked town
  exactly; that equivalence is the test that the clock changed no behaviour it should not have.

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
