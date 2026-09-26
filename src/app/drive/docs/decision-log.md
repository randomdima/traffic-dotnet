# Driving by hand from a script — decisions

Why this slice reads as it does. What it *must* do is [requirements.md](requirements.md).

## A script played from the start, rather than a session left running

A hand steering a car wants to see what the last step did before deciding the next one, and the obvious
shape for that is a process holding the town open and taking commands. This does the other thing: the
script is a file, and every run plays the whole of it from the town's own first tick.

Nothing here sits in front of a screen. What drives is a tool call, and a process between calls is a thing
to keep alive, to address, to time out and to clean up — while a seeded town and a written hand give the
same answer twice, so replaying a step longer script costs the ticks and buys the whole state back
exactly. **What it costs** is that the replay is linear in the script: the fixture stands up in a second or
two, a city in most of a minute, and a ten-minute drive is ten minutes of ticking every time a step is
added. A drive somebody is watching is the live window instead (DRV-7).

## What driving one live showed, and what it cost to make it possible

What the first watched drive found that still binds, none of it about the car:

- **The camera stayed where the run opened**, because `Follow` is only ever asked for a unit by a *click*.
  A drive picking a car out asks for the same thing now — and, less obviously, so does a drive setting the
  span for a frame: `Follow` reads any camera it did not leave as a reader taking over, so framing a shot
  stood the follow down and left the car driving out of the picture.
- **A hand with seconds of lag cannot steer.** Between reading a frame and the step landing, a car at town
  speed covers a hundred metres and the step is then about somewhere it has left — which is how the first
  two cars were wrecked. `pace N` is the answer: the lag costs a fraction of a second of the town, and
  **nothing about the drive changes**, because a step holds for the seconds of the town it names and the
  tape is counted in ticks.

**The pedals still surprise a driver, and that is the model working.** A burst that ends on the brake spends
the first second of the next one lifting off it (`CarBuild.PedalRateMps3`, `Driving.PedalTravelS`): the
travel is the body's, exactly as it is for the town's own drivers.

## What the second watched drive found, and none of it was the drive's

Twenty-two minutes at the wheel of two cars in Odesa, at `pace 0.35` and then `pace 0.2`. The drive did
what it says; the trouble was what a driver could not find out from the read-out:

- **No row says the car is dead.** `UnitReadout` writes `wheel` from `world.HandsOn`, a fact about the
  *selection*, so a wrecked car still prints *in the player's hand* while `TownWorld.Handed` refuses the
  hand. The only tell is `doing wrecked`.
- **A held agent is an obstacle nothing reports.** A hand-driven car takes no claims, so `ahead` said
  *clear* against a parked car 2.4 m away. Leaning on it broke the town's own claim — *nobody goes on into
  ground it was refused*, 6.1 m deep, on the car being shoved (`DRV-5`: quoted, gates nothing).
- **A kerb stops 1400 kg dead at 15 km/h** with `ahead clear` and `room the road to itself` throughout, and
  **`off line` reads 0.00 m for a car with no line**, so leaving the carriageway reads exactly like staying
  on it.
- **The brake through zero is the reverse** (`DRV-1`) and the reading does not say so: a car asked to stop
  from 11 km/h was backing east at 13 km/h a second later, and only the pose said which way.

**`pace 0.35` is not slow enough at a junction** — a two-second burst still crosses a road blind, which is
how the first car was wrecked; `pace 0.2` held the second one.

## A second seat rather than a second selection, and an eye rather than the glass

A driver that is not the person watching wants three things the live drive could not give it, and each was
a fork with an obvious cheap answer that would have been wrong.

**The car.** Handing the seat the *selection* would mean that clicking a second car to look at it took the
wheel out of the bot's hands, and that the bot's own first step took the reader's selection away. So the
hand names its car and the world carries the two apart (`CTL-5d`).

**The eye.** A window has one camera and it belongs to whoever is looking through it, so a bot reading the
glass would be blind the moment somebody panned away — and would be reading the panels, the menu and every
debug layer besides. The eye is the shot path (`SHT-1`) on the device the window already draws with, at a
camera and a frame size of its own, with `none` for its interface. What it costs is a stall: the read-back
waits the device idle, so every frame the bot asks for is a hitch in the window somebody is watching —
paid by the asking alone, every few seconds.

**The wheel is taken by sitting down.** The first run let the seat hold nothing until its first `drive`
step landed, and a car under nobody's hand drives its own route: the bot inherited one at 49 km/h and
wrecked it in four turns.

**The handbook is the engine's and not the harness's.** The first bot was briefed by a prompt somebody
typed — "keep to about 25-40 km/h", "a kerb stops you dead" — true of the car being driven the week they
were written and read off nothing. A harness that quoted figures of its own would be a second spec sheet,
and the car is retuned more often than the prompt is.

**The town waits for it, and what stops is the clock.** A driver that thinks for eight seconds while the
town runs has driven eight seconds blind, and `pace` only makes the blind stretch cheaper. **Holding the
agents instead would be the wrong half**: that stops the traffic deciding and leaves this car rolling. The
cost is that the run keys are the drive's while it holds them — the freeze key cannot win an argument with
a seat that re-asks for the wait every frame — so it is asked for by name on the command line.

## The pedals are named and the figures are refused rather than clamped

`drive 1.5 throttle=1 steer=-0.4` says what is held and for how long. Clamping `throttle=2` would make the
log say the car was given everything when the line asked for twice that, which is the one thing an
instrument may not do; and a positional form (`drive 1.5 1 -0.4`) reads as three numbers a week later.

## The read-out is the panel's, moved rather than copied

The rows a step prints were `UnitPanel`'s own; they are `UnitReadout`'s now, drawn by the panel and
printed here — the shape the claims already use, where one machine answers the panel, the probe and the
tier ([verification](../../../../docs/verification.md#what-a-map-claims-about-itself)). A log with its own
idea of what a car is doing would disagree with the frame taken beside it within a month.
