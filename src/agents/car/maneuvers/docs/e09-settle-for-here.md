# `E-9` — settle for here

Code: [E09SettleForHere.cs](../reactive/E09SettleForHere.cs) · [catalogue](index.md)

**Scenario.** Nothing else on the ladder worked, but where the car stands is somewhere it may lawfully
stop. **Rung 7**, and one of the two terminal entries.

**`Sa` — the state it starts in.** The whole body on drivable ground, **not across a lane** — not in a
junction box, not committed to a template laid across the road behind it — and this leg has not already
settled once.

**`Sb` — the state it delivers.** The car stopped and held with the handbrake, every claim released, the
booked bay back on the market, the leg over, and **the driver out and walking the rest of the trip**.

**Line.** None. It stops where it stands.

**Do.** Give up the booking, hold, end the leg.

**Guards.** None.

**Bounds.** None needed — it ends the leg on its first tick.

**Exits.**

| | Successor |
|---|---|
| always | the leg is finished: **settled** |
| the `Sa` refuses | `E-10` |

**An agent uses the actions it has to get as close to its goal as it can; it does not drop the goal.**
That is the whole difference between this entry and `E-10`: the car is parked badly rather than
abandoned, and the person carries on to where they were going on foot.

**Terminal means it may not hand a leg back to something that has already failed.** Past the first
recovery, nowhere legal to stop is `E-10` rather than a second settle — otherwise a leg that cannot end
keeps being offered the same ending.

**Refs.** CAR-9a, CAR-6.4, MAN-5, PER-*.
