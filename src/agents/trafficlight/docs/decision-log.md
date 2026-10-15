# The traffic light agent — decision log

Why the rules in [requirements.md](requirements.md) read as they do.

## 2026-10-12 — the amber is 2 s

**`SignalFigures.AmberTailS` went 1.5 → 2 s.** A car that could no longer stop at the amber's start goes on (TLT-2a),
and one doing that while slowing for the turn past the bar — 18 m/s at the amber, 7.5 m/s at the bar — had its rear
axle over it a few tenths into the red (CAR-13.3). With the car's plan end kept as a hold found there would cut it
(car log, same day), the reds crossed per 1 800 ticks of `--bench drive` at 1.5 / 2 / 2.5 / 3 s: Exam 2 / 1 / 0 / 0,
Odesa 6 / 2 / 1 / 1, River 1 / 1 / 1 / 1, O10 36 / 11 / 5 / 1; mean speed on Odesa 10.06 / 9.90 / 9.74 / 9.68 m/s.
Hard brakings unmoved by it. 2 s is where Odesa came under its count before the driver's longer committed ground; a
longer amber is held as a red is and taken off every green, which the cycle's short half does not spare.

## 2026-10-06 — a light's colour is asked on the device, off the service's own tables

**The tick moves to the device** (root log). `control/kernels/signal.glsl` is `SignalCycle`, `SignalService`'s two
lookups and a stretch's choice between them (`SignalHolds.ColourOf`, which `Lay` now asks), read off the service's
own tables (`SignalService.Table`). One thread a stretch, since a stretch is what a hold is laid for. Held over every
approach and crossing of the suite's fixture town, lit and unlit, at moments over four cycles and a float either side
of every edge, and with no cycle at all: every colour the host's. Nothing in a light is a transcendental, so nothing
is forgiven.

**The hold a colour lays is still the host's**: it runs to the first body travelling the way, which is the claims'
to say, and comes with them.

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
- **Its rung is a new one, p4**, below a call's: "not higher than police or ambulance"
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
