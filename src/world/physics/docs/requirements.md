# Physics and damage — requirements

The hard rules: what a contact does, what "damaged" means, and what a body is subject to whatever it
intends. **What this project's own solver must be** is [solver.md](solver.md). How a car and a person are
actuated belongs to the agents that do it ([car](../../../agents/car/docs/requirements.md),
[person](../../../agents/person/docs/requirements.md)); containment is
[world/containment](../../containment/docs/requirements.md).

## Hard rules

**PHY-1** Solid bodies never overlap; contact produces a collision.

**PHY-2** Static objects cannot move and cannot be damaged or destroyed; they are immovable collision
geometry.

**PHY-9** Agents are subject to external forces regardless of their own intent — **being pushed is always
possible**.

That terrain is **not** a collider is `PHY-8`, and it belongs to
[world/terrain](../../terrain/docs/requirements.md): this layer never sees it.

Hard rules live here and in the damage resolver only. **Soft rules never touch this layer**: nothing may
teleport, snap, clamp or nudge a body because it is somewhere illegal (SIM-1).

## What the layer must provide

Rigid bodies in 2D with mass, linear and angular velocity and a collision shape; contact resolution that
prevents overlap and produces an impulse response; an impulse interface, central and at a point; contact
reporting naming which two bodies **began** touching and the velocities they carried into that tick;
static bodies; layers and masks under the rule that two bodies interact when **either** scans the other;
per-body rotation lock and gravity off.

**Nothing is swept.** A fast body can pass through a thin one; this is a stated limit, not a defect.

## Damage

**PHY-3** Damage is binary. No health, no accumulation, no severity curve: a dynamic object is either
intact or in its terminal state — a person **dead**, a car **broken**.

**PHY-4** What a contact does is decided by its energy — half the pair's reduced mass times the square of
their closing speed along the contact normal — measured against **that participant's own tolerance**. One
energy per contact, one tolerance per kind of body, and every pairing in the town falls out of the two
**without a special case**: the same speed that kills a pedestrian barely marks the car that hit them,
because the pedestrian weighs a seventeenth of it. Below its own tolerance a contact is a harmless bump,
which is what keeps cars nudging each other in traffic intact. A person carries **two** tolerances and so
sees three bands; every other body carries one.

**PHY-4a** Three exemptions, which are rules rather than arithmetic:

1. person ↔ person contact is harmless at any energy;
2. a person against static geometry is harmless at any energy;
3. a static object is never affected by anything.

**PHY-5** Dead and broken objects are **never removed from the world**. Only their state changes: they
keep their body and collision shape, stay dynamic, can still be pushed, and take no actions. A broken car
cannot be driven again, and all four of its wheels are locked with nobody at the pedals, so a shunted
wreck skids as a block rather than rolling away.

**PHY-5a** A body already in its terminal state cannot enter another and **contributes nothing to the
other participant**: a car may drive over a dead person without breaking, and a broken car is judged as
an intact one would be for whatever hits it.

**PHY-6** The driver contained in a car that becomes broken is unaffected — they are not physically
simulated — and may exit normally.

**Damage is judged once per touch, on the tick the pair start touching, at the speed both carried *into*
that tick.** A pair resting against each other in a queue is one judgement, never sixty a second. This
needs a pair table with an explicit end-of-tick sweep; getting it wrong is the difference between a queue
and a massacre.

**One component owns this arithmetic** and is the only place damage is decided.

## What this slice must produce

- The damage table reproducing every ordered pair of kinds and every energy band correctly, including
  that the same contact may break one participant and not the other (VER-6).
- A town where no dynamic body ends up overlapping another over a long unattended run (VER-3) — asked of
  a town that is running, not of a staged pair.
