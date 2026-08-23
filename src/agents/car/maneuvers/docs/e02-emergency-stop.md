# `E-2` — emergency stop

Code: [E02EmergencyStop.cs](../reactive/E02EmergencyStop.cs) · [catalogue](index.md)

**Scenario.** Something is in the path within braking distance and the ordinary speed profile will not
stop the car in time. **Row 1 of the arbitration**: it outranks everything, including a manoeuvre in the
middle of its own procedure.

**`Sa` — the state it starts in.** None. Row 1 binds everywhere, including inside a recovery.

**The trigger**, which is the whole of the entry and lives in its own file so nothing else can hold a
second opinion about it: the car is moving; the **closing** speed against what it found in front — not this
car's own — is above a stop; and the deceleration that closing speed would need over the gap is more than
the profile planned for, where "planned for" is the lesser of what the pedal asks and what the tyres can
put down on the ground under them, less the margin the profile keeps.

**`Sb` — the state it delivers.** The car stopped, or the hazard gone.

**Line.** None of its own, and deliberately: **braking rather than swerving, always.** A swerve wants a
lane verified clear, and verifying one is `E-4`'s job and takes time this does not have.

**Do.** The full pedal at once — braking that ramps up wastes the most valuable distance there is — with
the wheel left exactly where it was, because the path stays straight. **The handbrake is not touched**:
locking wheels at road speed is a skid.

**Guards.** None. It is the guard.

**Bounds.** The reflex hold: it keeps the name for a second after the hazard has gone, imposing nothing
while it does.

**Exits.**

| | Successor |
|---|---|
| no hazard, and the reflex hold is spent | resume the suspended entry, through its own `Sa` |

**Why it is asked outside the decision clock.** By the time a procedure noticed, it would be too late, and
a hazard inside braking distance is not something to discover at the end of a scheduling interval. It is
asked on every tick by the sensing half of the driver and costs nothing: arithmetic on the headway already
read and two speeds, with no query anywhere in it.

**Why the reflex holds its name for a beat.** A car braking hard in stop-start traffic drops below the
closing speed that triggered this, is let go, accelerates into the same gap and triggers it again — which
is **one** emergency stop and not twenty, and counting it as twenty buries the reading this entry exists
to give.

**What a high count means.** Frequent use of this is a **planning failure and not a safety feature**.
Constant flat-out braking means the profile or the looking is wrong upstream, which is exactly why the
trace counts it.

**Refs.** CAR-6.1, SIM-1, S-2, S-5.
