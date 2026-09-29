# The traffic light agent — decision log

Why the rules in [requirements.md](requirements.md) read as they do.

## 2026-09-28 — a light holds ground, and a town lights most of what it can

**The owner asked for the lights back, as a reservation.** Four fifths of the junctions that can carry
lights are lit, the bigger ones more likely; no roundabout is; and what a light does is hold the lanes it
blocks as a secondary claim, at a high rung but under the police and the ambulance — the zebras included.

**Every bit of the drivers' own light-reading went** (`SignalStopM`, the blue light's and the reckless
driver's exemptions, the stop term the follower braked for). A light is now one more holder on the
planned layer (`SignalHolds`), and everything that used to be an exemption is the ladder: a call beats it by
rung, and a car that can no longer stop beats it as it beats every rung, which is what the amber is for.

- **A secondary claim and not a main one.** Laid as a main claim, it would place secondary claims through its
  marks — onto the zebra across the approach it holds — and the walker the red was holding the traffic for
  would be refused the paint. Two secondary claims never meet, so the zebra and the lane under it are each
  held by exactly the light that governs them.
- **From the bar to the mouth of the box, cut at the first body travelling the lane.** Held at the bar
  alone, a car queued past it — on the zebra, in the mouth — would be refused the box when the phase turned
  under it and stand there until green. Cut at the first such body, whatever has started has started, which
  is the positional test `TLT-2a` already asked for; the one difference from the rear-axle test it replaced
  is a nose over the bar, which now counts as started.
- **The zebras are held from either kerb, cut at the first walker on the paint going that way**, so a walker
  already crossing when it turns red walks off it and the one at the kerb waits (PER-27).
- **A body only across the held way cuts nothing of it.** Cut at any body, a car driving over a red zebra
  broke its hold into a piece either side of the car — the owner saw it on the overlay — and a walker on the
  zebra did the same to the approach under it. Neither is what the light governs, the body already stops
  whoever asks that way short of it, and the metres beyond it were held by nothing.
- **Placed before any plan, and reading nothing planned**, because a secondary claim cuts nothing where it
  is placed and meets no other.
- **A connector is not held.** Only a car already in the box could meet a claim there, and that car has
  started.
- **Its rung is a new one, p4**, between a closed road and the paint: "not higher than police or ambulance"
  put it below both, and holding the zebras put it above the walker it holds.
- **The grant a light cuts is read at the reaction lead** (`CarFollower`), as the stop term it replaced was.
  Read at the grant alone, a car pulling away towards a red was still accelerating when the ground it could
  no longer stop short of reached the bar, and that ground beat the light: the scenario map's
  "The cross traffic waits out its red" ran the red at 12 m/s.

**The share is drawn exactly and weighted by movements.** Each junction that can be lit is drawn with a key
of a uniform draw raised to one over its arms times its arms less one (Efraimidis–Spirakis), and the brief's
share of them with the highest keys is lit — so a town lights exactly the share it asked for. **Weighted by
arms alone it was barely a lean**: in a town of as many tees as crossroads, lighting four fifths of them
lights about 85 % of the crossroads and 76 % of the tees; weighted by movements, about 91 % and 70 %.

**The plan's own bars went with the old reading** (`CityPlan.StopLines`). The town lays its bars once off
its kerb ends (`StopBars`), the picture already painted those, and the scenario map's bars had been laid a
second time at the same places by the same arithmetic; the lights, the heads and the lanes' furniture read
the one laying now.
