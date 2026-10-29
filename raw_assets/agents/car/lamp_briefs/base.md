# Cutting one car's lamps — rules for an agent

Project: /var/home/random/Documents/traffic-dotnet. Tool: `qq props <action> CAR [options]`. Read `qq props --help`
fully first — it is the manual.

Loop: `qq props seed CAR --force`, `qq props show CAR` (writes a PNG under .tmp/props/; open it with the Read tool; if
it says the zoom is too small, follow its advice), then `find`, `trace`, `snap`, `move`, `bow`, `drop`, `undo` (several
steps back) as needed, `qq props cut CAR` (Read the *_lit.png to check each lamp burns over the lens the art drew),
and finally `qq props install CAR`.

- Work the AUTHORED side only: the upper half of the sprite (rows above the axis). Afterwards `qq props mirror`
  repaints the lower side's lamp art from the upper side's lamps, reflected where each copy lies — so fit each prop
  to the upper side's lens and ignore copy-side disagreement. Keep each copy roughly over the lower side's lamp (it
  says where the reflection is painted); do not tune it.
- Every car gets head, ind_front, brake, ind_rear, reverse. Where the art draws no separate lens for a lamp, it is a
  part (share) of the main lamp, as the manual says. An amber patch drawn inside a red rear lamp is the indicator
  unless it is plainly a glint. Roof rails, rust, paint highlights and trim are not lenses.
- Pale headlamp glass on white or silver paint, and any lamp on black paint, is easy to misread: judge the rim on the
  picture and on the `dark` panel. On red paint, follow a red lamp's dark frame, not its colour.
- A lens must be under 56 texels a side and within 28 of its centre (`INSTALL_CELL_PX`): the report flags it PAST THE CELL and install refuses it. Trim a tip, or
  cut a long housing in two with `qq props divide CAR --prop=brake --at=X,Y+X,Y --into=ind_rear` (the line through
  the two texels, at the seam the art draws), always INTO a lamp that is now a part (ind_rear or reverse), so the car
  stays at ten lenses. A car draws ten lenses at most: the report says TOO MANY LENSES, and `qq props remove CAR
  --prop=P` takes a prop out (ind_front, ind_rear or reverse becomes its main lamp's part again; a seeded brake2
  goes).
- Placing points by hand, or adding `"over"`, is an edit of the props JSON under .tmp/props/ — fine. Run `qq props
  show` after it, so the step is in the undo history.
- Judge on the pictures, not only the numbers. Close, not pixel-perfect: half a texel off the rim is on it.
- Only work on your car. Do not edit any code or tool, do not touch other cars, do not run tests, builds, git or
  `qq props mirror`. Finish with `qq props install CAR` succeeding.

Report back in under 120 words: installed yes/no, which lamps are their own lens vs a share, anything doubtful, and
every place the tool misbehaved or its manual misled you (one line each, concrete).
