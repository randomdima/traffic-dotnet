# Driving by hand from a script — decisions

Why this slice reads as it does. What it *must* do is [requirements.md](requirements.md).

## A script played from the start, rather than a session left running

A hand steering a car wants to see what the last step did before deciding the next one, and the obvious
shape for that is a process holding the town open and taking commands. This does the other thing: the
script is a file, and every run plays the whole of it from the town's own first tick.

The reason is that nothing here sits in front of a screen. What drives is a tool call, and a process
between calls is a thing to keep alive, to address, to time out and to clean up — while a seeded town and
a written hand give the same answer twice, so replaying a step longer script costs the ticks and buys the
whole state back exactly. Appending a line is then the whole of steering, and the cost is bounded by the
map: the fixture stands up in a second or two, a city in most of a minute.

**What it costs** is that a drive cannot be long: the replay is linear in the script, so a ten-minute
drive is ten minutes of ticking every time a step is added. Nothing in this project wants one — a
manoeuvre is seconds — and the day something does, the answer is the town's own state written out and read
back, not a daemon.

## What driving one live showed, and what it cost to make it possible

The first watched drive found three things at once, and none of them was about the car:

- **A windowed run opens with the agents held** (`RunState.AgentsHeld`, a temporary default while the lane
  layer is rebuilt), so a drive stood in a town where nothing else moved. The `Pause` key is the answer and
  the drive presses it (`agents on`) rather than the default being quietly changed under the rework.
- **The camera stayed where the run opened**, because `Follow` is only ever asked for a unit by a *click*.
  A drive picking a car out asks for the same thing now — and, less obviously, so does a drive setting the
  span for a frame: `Follow` reads any camera it did not leave as a reader taking over, so framing a shot
  stood the follow down and left the car driving out of the picture.
- **A hand with seconds of lag cannot steer.** Between reading a frame and the step landing, a car at town
  speed covers a hundred metres and the step is then about somewhere it has left — which is how the first
  two cars were wrecked. `pace N` is the answer: the town runs at a fraction of real time, the lag costs a
  fraction of a second of it, and **nothing about the drive changes**, because a step holds for the seconds
  of the town it names and the tape is counted in ticks.

**The pedals still surprise a driver, and that is the model working.** A burst that ends on the brake spends
the first second of the next one lifting off it (`CarBuild.PedalRateMps3`, `Driving.PedalTravelS`): the
travel is the body's, exactly as it is for the town's own drivers.

## What the second watched drive found, and none of it was the drive's

Twenty-two minutes at the wheel of two cars in Odesa, at `pace 0.35` and then `pace 0.2`. The drive did
what it says; what a driver could not find out was the whole of the trouble.

- **The read-out has no row that says the car is dead.** `UnitReadout` writes `wheel` from
  `world.HandsOn`, which is a fact about the *selection*, so a wrecked car still prints *in the player's
  hand* while `TownWorld.Handed` is refusing the hand and `HoldAgents` is parking it with everything else.
  The only tell is `doing wrecked`, one word in a row that otherwise says what a manoeuvre is.
- **A held agent is an obstacle nothing reports.** With the agents held, a parked car is immovable and
  reads as nothing: a hand-driven car takes no claims, so `ahead` said *clear* against a car 2.4 m away
  that the drive had to find with `cars X Y`. Leaning on it broke the town's own claim — *nobody goes on
  into ground it was refused*, 6.1 m deep, on car 113, the car being shoved (`DRV-5`: quoted, gates
  nothing).
- **A kerb stops 1400 kg dead at 15 km/h**, three times at the same junction corner, with `ahead clear`
  and `room the road to itself` throughout, and **`off line` reads 0.00 m for a car with no line**, so
  leaving the carriageway reads exactly like staying on it. A hand at the wheel has the frame and nothing
  else to tell it where the road is.
- **The brake through zero is the reverse** (`DRV-1` says so) and the reading does not: a car asked to
  stop from 11 km/h was backing east at 13 km/h a second later, and only the pose said which way.

**`pace 0.35` is not slow enough at a junction.** A two-second burst at that pace still crosses a road
blind, which is how the first car was wrecked on the third corner; `pace 0.2` held the second one.

## A second seat rather than a second selection, and an eye rather than the glass

A driver that is not the person watching wants three things the live drive could not give it, and each of
them was a fork with an obvious cheap answer that would have been wrong.

**The car.** The drive picks its car out, which is a click — and a click is the reader's. Handing the seat
the *selection* would mean that clicking a second car to look at it took the wheel out of the bot's hands,
and that the bot's own first step took the reader's selection away. So the hand names its car and the world
carries the two apart (`CTL-5d`): the player's over whatever is picked out, a second driver's over one
number. The player wins a car both are holding, because the pointer is the thing a person can see.

**The eye.** A window has one camera and it belongs to whoever is looking through it, so a bot reading the
glass would be blind the moment somebody panned away — and would be reading the panels, the menu and every
debug layer besides. The eye is the shot path (`SHT-1`) on the device the window is already drawing with, at
a camera and a frame size of its own, with `none` for its interface. **Bare is the whole point**: the
overlays draw what the town knows about itself, and a driver given the lines, the claims and the room ahead
is driving on instruments no driver has.

What it costs is a stall: the read-back waits the device idle, so every frame the bot asks for is a hitch in
the window somebody is watching. It is paid by the asking and nobody else, and a driver asks for one every
few seconds.

**The wheel is taken by sitting down.** The first run let the seat hold nothing until its first `drive` step
landed, and a car under nobody's hand is a car driving its own route: the bot inherited one at 49 km/h and
wrecked it in four turns. A seat now holds the wheel with nothing pressed from its first tick, which is
`CTL-5b` read the only way that makes sense for a driver who has not spoken yet.

**The dashboard is written beside the frame.** The readings are already echoed as they are taken (DRV-7),
but what reads a bot's frames is a program watching a folder rather than somebody reading a terminal. A
`.txt` beside the `.png` is the same reading in the same tick; a picture whose figures arrived down another
pipe is two things to line up and one of them to get wrong.

**The handbook is the engine's and not the harness's.** The first bot was briefed by a prompt somebody typed:
"keep to about 25-40 km/h", "a kerb stops you dead". Both were true of the car that was being driven the week
they were written, and neither was read off it. What a driver needs is exactly the set of figures the solver
is about to hold it to — what the pedal is worth, how long it travels, where the wheel locks, what radius the
tyres hold at the speed the streets were laid for — so the seat writes them itself, off `CarBuild` and off the
eye's own framing, and whoever is briefing the driver reads that file. A harness that quoted a figure of its
own would be a second spec sheet, and the car is retuned more often than the prompt is.

**The town waits for it, and what stops is the clock.** A driver that thinks for eight seconds while the town
runs has driven eight seconds blind, and no amount of `pace` fixes that — it only makes the blind stretch
cheaper. With `--bot-waits` a seat with nothing on its tape takes the time scale to zero, so the frame it was
given is still true when its answer lands and a burst means exactly the seconds it asked for. **Holding the
agents instead would be the wrong half**: that stops the traffic deciding and leaves this car rolling, which
is the opposite of what is wanted. The seat is therefore followed once before the ticks are counted as well
as once inside them — a waiting town is due no ticks at all, and the step that releases it arrives in a file.

The cost is that the run keys are the drive's for as long as it holds them: the freeze key cannot win an
argument with a seat that re-asks for the wait every frame. It is asked for by name on the command line, and
the panel says `waiting` rather than `frozen` so that whoever is watching can tell a standing town from a
hung one.

## The pedals are named and the figures are refused rather than clamped

`drive 1.5 throttle=1 steer=-0.4` says what is held and for how long, and a script that asks for
`throttle=2` is an error. Clamping would make the log say the car was given everything when the line asked
for twice that, which is the one thing an instrument may not do; and a positional form (`drive 1.5 1 -0.4`)
reads as three numbers a week later.

## The read-out is the panel's, moved rather than copied

The rows a step prints were `UnitPanel`'s own. They are `UnitReadout`'s now, drawn by the panel and printed
here — the shape the claims already use, where one machine answers the panel, the probe and the tier
([verification](../../../../docs/verification.md#what-a-map-claims-about-itself)). A log with its own idea
of what a car is doing would disagree with the frame taken beside it within a month.

The pose is the exception and is this slice's own: no panel writes where a body is, because the panel is
drawn over a town where a reader can see it and a log is not.
