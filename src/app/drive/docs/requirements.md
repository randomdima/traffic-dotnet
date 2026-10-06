# Driving by hand from a script — requirements

A hand at the wheel with nobody sitting in front of the screen: a town stood up, one unit picked out, and
a script holding the keys through it, with figures after every step and a frame wherever the script asks
for one. It is the instrument an assistant drives a car with — what the player does with a keyboard
([app/playercontrol](../../playercontrol/docs/requirements.md)), asked for in writing.

It is not a second way to move a body (DRV-1): this slice stages the run, arranges the readings and asks
[app/shot](../../shot/docs/requirements.md) for the pictures.

**DRV-1** `P7` **A script may hold the keys and nothing else.** The pedals, the wheel and the handbrake are
pushed through the same seam a keyboard's are and read by the agent loop every tick (`CTL-5`, `CTL-6`), an
order is the right-click `CTL-8` already offers, and the lever is `CTL-7`'s. **Nothing is written into a
body**: no pose, no velocity, no goal placed behind the driver's back, and no rule switched off for the
run.

- **A pedal figure is one a key could have produced** — nought to one of the travel that pedal has, and
  the wheel between full left and full right. A figure outside that is refused rather than clamped: a
  script asking for more car than the player has is a script that has misunderstood what it is holding.
- **The brake and the reverse are one pedal**, as they are on the keys.
- **Letting go coasts and does not hand the unit back** (`CTL-5b`), so a script that stops holding the
  keys is still at the wheel; the ways out are the reset and a change of selection, and a step that asks
  the town to drive itself while the wheel is held is refused.
- **What the town does about it is the town's.** A hand-driven car is queued around, given way to and
  collided with exactly as any other, and the hard envelope — the gear's cap, the pedal travel, the
  steering lock, the friction ellipse, the damage — binds whoever is holding the keys.
- **The run keys are keys too.** Holding the town's agents and letting go of them again is the `Pause` key
  a reader has (`RunState.AgentsHeld`), reached by the same press and changing nothing else about the run.

**DRV-2** `P8` **The drive is a document and not a command line.** One line is one step, a `#` starts a
comment, and **a line the reader cannot parse fails the run** — a step quietly dropped is a drive whose
log describes a car nobody drove.

**DRV-3** `P4` **Every step reports, and the figures are the interface's own.** What a step leaves behind is
the unit read-out the panel draws (`OBS-2m`) — one machine and two readers — plus the pose, which is the
one thing a reader has on the town that a log has not. **Nothing about the unit is worked out here**: a log
that did its own arithmetic would be a second opinion that could not be settled by looking at a frame of
the same tick.

**DRV-4** `P8` **A frame is the game's own frame.** With no window, pictures go through the one shot path
(`SHT-1`) and carry the band that says how to take them again (`SHT-2`); **in a window, the frame on the
glass is read back** rather than staged a second time, and it carries no band — a live drive cannot be
taken again (`CTL-6`), so there is nothing for one to say. Either way the camera rides the unit under the
hand unless the script pins it somewhere. A drive is looked at in the frames and read in the
figures, and neither is allowed to be a picture of this slice.

**DRV-5** `P8` **What the town claims while a hand drives is quoted and gates nothing.** `CTL-5` puts the
player outside the soft rules on purpose, so a claim broken by a car somebody drove into something says
what the player did rather than what the town does ([verification](../../../../docs/verification.md#what-a-map-claims-about-itself)).
The table is printed on the way out as a reading.

**DRV-7** `P7` **A drive somebody is watching follows the file as it is written.** `--drive FILE --live` opens
the window the game always opens, stands the town up, and reads the script again as it grows: a step
appended while the town runs is driven when it arrives, and its reading is said as it is taken rather than
at the end. **It is the same steps doing the same things to the same seam** — what a step means is stated
once, and only *when* it happens differs.

- **The keys are the drive's while it has the wheel**, and the camera, the panels and the run keys stay
  whoever is watching's. A unit the drive picks out is a unit the camera stands on (`OBS-1a`), exactly as a
  clicked one is.
- **A tape that has run out coasts** (`CTL-5b`): the wheel is still held and nothing is pressed, so a car
  whose driver has stopped typing rolls to a stop rather than carrying on into whatever is in front of it.
- **A step that cannot be carried out is said and dropped**, and the run goes on: a mistyped line in a live
  drive is a line to write again, not a town to tear down.
- **The pace is a key like the others.** Whoever writes the steps takes seconds to decide the next one, so
  a live drive may run the town at a fraction of real time (`pace N`, `RunState.SetPace`) and spend a
  fraction of itself waiting for them. **Nothing about the drive changes with it**: a step holds for the
  seconds of the *town* it names.
- **What is drawn is the window's and what is written may be smaller** (`--frame-width`). The town is drawn
  at whatever the window is; the frames handed to whoever is driving are a scaled copy of that same
  picture, because a driver reading them wants them to arrive.

**DRV-8** `P7` **A second seat: a hand that names its car, an eye of its own, and no interface in it.** A run
may carry a driver who is not the one watching it (`--bot FILE --bot-car N`). It is the live drive's own
machine — the same steps, the same seam, the same tape — with three things different, and every one of them
is there so that **two people can drive one town at once**.

- **The car is named and never picked out, and the reader sees it by its own mark and the camera standing
  on it** (`CTL-5d`). The selection, the panels, the switches and the keys stay with whoever has the
  window, so a reader may drive a second car by hand, pan the town, open the menu and click whatever they
  like while the seat drives.
- **It takes the wheel when it sits down**, not when it first presses a pedal: a car whose driver has said
  nothing yet coasts with somebody in it (`CTL-5b`), rather than driving its own route until the first
  step arrives.
- **The eye is the shot path at a camera of its own** (`SHT-1`), pinned on the car it is driving and drawn
  **bare** — no panel, no menu, no debug layer, no caption. What the layers draw is what the *town* knows
  about itself, and a driver shown the lines, the claims and the room ahead would be driving on readings no
  driver has. The frame's span and size are the seat's own words, because a picture that has to travel is
  smaller than a window.
- **The wheel is a setting and the pedals are a burst.** A `drive` line that does not name `steer` leaves
  the wheel where the last one put it, because a driver that has to restate its own steering on every line
  spends every line restating it — and `steer=0` is an angle asked for, which is why the two are told apart
  by the line and not by the figure. **Letting go still lets go of the wheel** (`DRV-7`): a tape that has run
  out straightens up as well as lifting off, and the setting is what the next line comes back to.
- **What it is told about its car is a dashboard**: where it stands, how fast, which way it points, what it
  is and whether it is still in one piece. **Not the unit read-out** (`OBS-2m`) — that is the reader's
  panel and it is full of what the town knows.
- **And once, before it has driven a metre, the handbook**: what this car will do, what a pedal and the
  wheel are worth on it, what the streets it is on were laid for, and how much town a frame holds. **Every
  figure in it is read off the build the solver drives** and the eye's own framing, because a briefing
  somebody typed is a briefing that quotes a car that was retuned since. It is written beside the frames
  when the seat is taken, for the same reason a frame carries its dashboard.
- **The town may wait for a driver that is thinking** (`--bot-waits`). With nothing left on its tape the
  clock stops — not the agents, the clock — and the next step starts it again, so **a driver that takes ten
  seconds to look at a frame has not moved a metre in them** and the picture it answered is still true when
  the answer lands. A run asked for that way has given its clock away for the length of the drive, and the
  panel says `waiting` rather than `frozen` so that a standing town does not read as a hung one.
- **A frame is written with its dashboard beside it**, same name and a `.txt`: what reads these is a
  program watching a folder, and a picture whose figures came down another pipe is two readings to line up.
- **Every other word is refused** — picking units out, orders, the lever and the reset all reach the
  reader's own selection. A step outside a driver's own vocabulary is said and dropped (`DRV-7`).

**DRV-6** `P8` **The same script over the same map is the same run.** A town is laid off its map at the
map's own seed and the script is the whole of what is done to it, so a drive is replayed rather than resumed:
appending a step and playing the script again is how a hand steers over several sittings without a process
left standing between them. It is the exception `CTL-6` names — a hand forks the timeline, and a written
hand forks it the same way every time.

## What this slice may know

`app/drive/` sits beside [app/shot](../../shot/docs/requirements.md) under `app/main/`: it depends on the
hand ([app/playercontrol](../../playercontrol/docs/requirements.md)), on the read-out
([app/hud](../../hud/docs/requirements.md)), on the shot path for a picture and on `bench/` for what the
town claims. **Nothing depends on it but `app/main/` and the workshop.**
