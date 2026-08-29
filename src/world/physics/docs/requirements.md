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
possible**. `PHY-5b` is the one exemption, and it is about what may reach a body rather than about what a
body may refuse.

That terrain is **not** a collider is `PHY-8`, and it belongs to
[world/terrain](../../terrain/docs/requirements.md): this layer never sees it.

Hard rules live here and in the damage resolver only. **Soft rules never touch this layer**: nothing may
teleport, snap, clamp or nudge a body because it is somewhere illegal (SIM-1).

## What the layer must provide

Rigid bodies in 2D with mass, linear and angular velocity and a collision shape; contact resolution that
prevents overlap and produces an impulse response; an impulse interface, central and at a point; contact
reporting naming which two bodies **began** touching and the velocities they carried into that tick;
static bodies; layers and masks under the rule that two bodies interact when **either** scans the other,
and a body's layer changeable while it stands; per-body rotation lock and gravity off. **A pair is exempt
only where a layer says so** — a coupled tow (`EVA-5`) is not one, and `PHY-5b` is the only one there is.

**Nothing is swept** (`SOL-17`): a stated limit of the solver, not a defect of this layer.

## Damage

**PHY-3** Damage is binary. No health, no accumulation, no severity curve: a dynamic object is either
intact or it is not — a person **down in the road**, a car **broken**. **Nobody in this town dies.** A car
is finished when it breaks and a person is not: the worst a contact does to somebody is put them on the
ground, and what happens next is a rescue (`PER-18`) rather than an ending. That is why a broken car is
terminal (`AGT-5`) and a casualty is not.

**PHY-4** What a contact does is decided by its energy — half the pair's reduced mass times the square of
their closing speed along the contact normal — measured against **that participant's own tolerance**. One
energy per contact, one tolerance per kind of body, and every pairing in the town falls out of the two
**without a special case**: the same speed that puts a pedestrian in the road barely marks the car that
hit them, because the pedestrian weighs a seventeenth of it. Below its own tolerance a contact is a
harmless bump, which is what keeps cars nudging each other in traffic intact. **Every kind of body carries
exactly one tolerance**, so every pairing has two bands and no pairing has three.

**PHY-4a** Three exemptions, which are rules rather than arithmetic:

1. person ↔ person contact is harmless at any energy;
2. a person against static geometry is harmless at any energy;
3. a static object is never affected by anything.

**PHY-4b** Some vehicles are built not to break. A car whose variant is **unbreakable** never breaks, at
whatever energy — an exemption from the outcome PHY-3 would otherwise write, and from nothing else. It
stays a full participant in every contact it takes: what hits it is judged exactly as it would be against
any other car. That is what separates it from PHY-5a, which is silent because the body it speaks of is
already spent; an unbreakable one never is.

**PHY-5** Bodies in the road and broken cars are **never removed from the world**. Only their state
changes: they keep their body and collision shape, stay dynamic, and take no actions. A broken car cannot
be driven again, and all four of its wheels are locked with nobody at the pedals, so a shunted wreck skids
as a block rather than rolling away. **They lock where the crash left them pointing** — a car wrecked
mid-corner keeps the lock its rack was wound to, because nothing afterwards is turning it back, and that
angle is both what the four patches skid along and what the four tyres are drawn at.

**PHY-5b** A body lying in the road **collides with nothing that moves**. The contact that put it there is
the last one it takes: from that moment the only thing it meets is static geometry, which is what it
fetches up against and stops on, and every car, walker and rescue in the town passes through it. It is a
change of layer and not a removal — the body is still in the world, still dynamic, still slowed by the
ground under it (`PHY-8`), and still somewhere an ambulance has to come to. **A broken car is not this**: a
wreck stays a full participant and is shunted like any other obstruction.

This is the one exemption from `PHY-9`, and it is narrow: a casualty is put where the impact throws it and
is dragged where a stretcher crew drags it, so it is still moved by things outside its own intent. What it
is no longer is something for the town to push around for the length of a rescue.

**PHY-5a** A body that is already what a contact could make of it — a wreck, or somebody already lying in
the road — cannot be made that again, and **contributes nothing to the other participant**: a car may pass
over a casualty without breaking, and a broken car is judged as an intact one would be for whatever hits
it. **Spent is not the same as terminal**: a casualty gets up again once a hospital has had them
(`PER-18`), and is a full participant once more when they do.

**PHY-6** The driver of a car that breaks is **put out of it as a casualty**, on the road beside their own
door. They are not physically simulated inside the car and take none of the contact's energy; what puts
them there is the wreck itself, and from the moment they are down they are on `PER-18`'s terms like
anybody a car has hit. **It is a placement and never a search**: an ordinary exit looks for clear ground
beside the car and waits while there is none (`PHY-7a`), and a driver left waiting inside a wreck is a
casualty nothing will ever come for (`AMB-7`).

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
