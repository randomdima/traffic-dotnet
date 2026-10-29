# Prompting walkers

What the walker briefs taught. Read before writing the next one. `qq walkers orbit` writes a look's clip brief with
every lesson here that a sentence can carry. The brief each clip or frame was asked with is in its record
(`qq sprites trace <id> --full`).

## The orbit flow — the best so far (2026-10-28)

**A look is one 5 s clip from one front picture, and never more** (the owner, 2026-10-28; remotegen's core rule):
every facing, every walk frame and any stance are cut from that one clip. A longer clip or a second one is asked
first. The clip: a figure of a cast frame (nine looks standing, 3×3, the
game's view) laid alone at 3:4 on chroma green, or blue where the figure's own colours stand out in green, and
HeyGen Video 1 at 480p ($0.05). In the clip the walker walks on the spot facing one way while the camera cuts round
it, one facing a second: front, front and left side, left side, back and left side, back. The same figure, laid flat,
is the look's down picture. Step by step:

    qq walkers scaffold --sheet=<S> && qq walkers cast --sheet=<S>   # nine variant files; the cast brief and its call
    #   or, after the owner's photos cropped to each person under .tmp/gen/photos/: --photos=P1,P2,... (one a look)
    qq walkers orbit <id> --cast=walkers_<S>-v1:<N>         # first frame and brief; prints the calls below
    .claude/skills/remotegen/remotegen.py video …           # the clip, walkorbit_<id>-v1
    qq sprites cut walkorbit_<id>-v1 --as <id>              # flags; look at <id>_walk.gif and the per-facing GIFs
    # before keeping: look at the side row beside the front — the crown and shoulder tops must show (eye level, below)
    qq sprites keep walkorbit_<id>-v1 <id> --score=4 && qq sprites use <id>-vN
    qq walkers down <id> --cast=walkers_<S>-v1:<N>          # the down picture, kept and installed
    # the look in its catalogue (Catalog.json or Service.json), then the showroom's row of people, captioned:
    dotnet run --project traffic-dotnet.csproj -- --map Showroom --shot .tmp/showroom.png --caption --view 40 --at 340 344 --seconds 3

**The brief is four sentences** (`ORBIT` in `.qq/walkers.py`; the owner, 2026-10-28: the video model confuses fast):
five 1 s shots and hard cuts; walks on the spot and never turns, only the camera moving round to the left, high above
and always at the same distance, each shot named — the two diagonals "(mostly his front)" and "(mostly his back)"; the
pace; the flat screen. Each phrase answers a fault the trials below met: without "same distance" the camera zoomed in
through every shot, without the diagonals' cues back-left came out a profile, without "never turns" one look turned
within every shot, without the pace the loops seamed worse. Its camera can still go round the right side: look at the
diagonals before keeping, and cut those shots `--mirror`. The long brief it replaced, each of whose sentences is
below, held the camera at eye level less often; neither has been rolled against the other on one look.

What each sentence of the long brief was there for, from the trials below:

- **"It is the camera that changes"**, with each shot named by where the camera stands, not by how far it turned
  since the last one. Named by the turn, the angles went wrong.
- **"Walking from the very first moment"**: without it, the front shot starts from the standing first frame and its
  loop has a seam.
- **Back-left "halfway between his side and his back"**: without it, back-left comes out a second profile.
- **"The camera never comes down to eye level: in every shot … we look down onto the top of his head and his
  shoulders"**, said once for the whole clip, and the side shot as "at his left side, high above", **never "in
  profile"**. Asked "we see him in profile", four of eighteen side shots came out at eye level (below); a profile is
  what a camera level with the face sees.
- **"Quick, short steps, three steps every second … never a run, one foot always on the ground"**: a one-second
  shot then holds more than one stride, so the cut has room to choose its loop. "Brisk", or a double-speed
  fast-forward, did not help.
- Nothing more per shot: added detail ("one eye, her chest in front", "never to the picture's right") dropped the
  camera to eye level.

The cut finds the shots by the frame's upper half changing all at once, takes the best loop in each, scales each
shot to one height, and turns each facing's loop to start at its frame nearest standing (`stance_column`): column 0
is the stance the game stands a stopped walker in, and every look's sheet starts its walk alike. **The stance is
picked from the walk, never generated** (the owner, 2026-10-28, after the trials below found no stance after a cut in
one 5 s clip). From the side or a
diagonal that is the passing, feet closest together (`feet_apart`). From in front or behind it is two legs reaching
the ground side by side, equally low and alike in width (`feet_side_by_side`): there the passing lifts the swinging
foot towards the camera or hides it — a raised knee the owner saw on most north and south stances — and a foot kicked
back towards a camera behind is drawn as low as the planted one, so a level of the lowest paint either side of the body
picked raised knees too. Every north and south row of the eighteen holds at least one such frame. No clip holds a true
standstill, so a stance can keep a short step. Before 2026-10-28 the loop was only weighed towards such a start, and
77 of the eighteen looks' 90 facings began mid-stride; the sheet was also laid by pasting each figure through its own
alpha, which squared every soft edge's alpha and thinned the outlines. All eighteen were cut again from their clips
with both fixed. `--shots` picks the shots when the clip went wrong in one; `--mirror` flips the shots the camera
took round the walker's right side (one in four service clips); `--grid` cuts a clip of several looks in tiles.
**The back-left shot is the one most often taken round the right**, and the cut cannot tell: it reads as a walker
going up-right in the up-left row, and its mirror up-left in the up-right row — the two north diagonals swapped while
every other row is true. `recovery_a` and `recovery_c` were kept so and cut again with `--mirror 3` (`-v7`); look at a
cut's `_walk_1_back-left.gif` before keeping it. Best
result: `walkfast_linen` (seams 0.41–0.78 of a frame's change, every facing true).

**A side shot at eye level is the commonest failure, and the flags only point at it** (`_walkers.eye_level_flags`):
a flat profile, no crown or shoulder tops, the legs full length. The cut flags the side row scaled under 0.8 of the
back's and front's mean (the camera came lower and nearer) or standing over 5.3 head-widths tall; set on the
eighteen looks of 2026-10-28, every eye-level side raised one. The scale's threshold was 0.9 while shots were scaled
by their loop's median; scaled by the stance, the eye-level sides read 0.73–0.86 and the kept ones 0.83–1.05. A
bun or a ponytail widens the head and hides the second. The flag is a reason to look, not a verdict: the side row is
looked at beside the front before every keep.

## What a walker frame is

**A look is one sheet of the engine's: eight facing rows anticlockwise from up, eight walk columns, column 0 the
stance** (`PersonCatalog`). A walker is drawn upright whichever way it walks, seen from in front and above, so it is
not the same picture turned: every facing is a drawing of its own. **Only five are asked** — back, back and left side,
left side, front and left side, front — and `qq sprites use` mirrors the three right-hand facings out of them, a walker
being the same either way round. What is asymmetric (a bag on one hip, a parting) swaps sides when the walker turns
right; nothing a look is told apart by may be.

**The frame is always 8 columns by 5 rows**, one row a facing: one look walking eight frames, or two looks of four.
**It is asked with two pictures**: the style picture, and the pose guide that draws the grid and every pose
(below, "The pose guide"). `.qq/_walkers.py` holds the layout, the guide, the mirror and the column spread; `cut` finds
the 40 drawings by their gaps
(`_outline.drawings`), scales each row to the look's median height, stands every figure's feet on one line and centres
its head and shoulders, so a stride does not jitter the walker sideways. The kept variant is the look's whole block,
aligned, at the frame's resolution; `use` writes the 8×8 sheet at the cars' grid (96 px/m, a cell `heightM` tall).

**Judge a look walking, not standing**: `cut` writes `<id>_walk.gif` beside the block, every facing laid where the
compass has it and stepped through the eight columns the engine picks; `qq walkers preview <id>` does the same for an
installed sheet. Then a captioned `--shot` close enough to read a face (`--view 6` to `16`).

## The style picture

Three of the owner's fleet cars (`crossover_pearl`, `estate_steel`, `kombi_sage`, `qq sprites style`), asked for the
paint, finish and light only: there is no finished walker in the game's present style to show. Once a set is in, a
couple of installed walkers' blocks are the better picture — a person drawn in the style is what the next look has to
match, and a car says nothing about how a face or cloth is painted.

## The first demo (2026-10-27) — three looks, two frames, $0.026

`walkers_pair_a-v1` (`tee_blue`, `tee_red`, four frames each) and `walkers_solo_a-v1` (`coat_brown`, eight frames), one
roll each, scored 3.

- **The grid and the facings are held**: all 40 drawings of each frame were found in order, every row walks the way
  its words say, and nobody in rows 2–4 faces right. Each person is the same person in all their cells — face, hair,
  clothes and the bag on its strap.
- **The front row is drawn taller than the rest**, 14–15 % in `walkers_pair_a-v1`, 8 % in `walkers_solo_a-v1`. `cut`
  scales each row to the look's median and flags a row more than 6 % off, so it costs nothing.
- **Eight frames walk; four shuffle.** `coat_brown`'s side row is a real cycle — stance, step, full stride, pass, the
  other stride, a trailing foot lifted (`coat_brown-v1`). In the pair the four columns are nearly one pose: the side
  row's strides barely open and the stance is not feet together (`tee_red-v1`, `tee_blue-v1`). **Ask one look to a
  frame, eight frames**; the frame costs $0.013 either way.
- **The arms hardly swing** in either, and the front and back rows' legs move least, as they would seen end on. The
  brief now asks for arms swinging clearly opposite the legs and the feet a foot's length apart at full stride;
  untested.
- **Proportions lean stocky**: heads about a sixth of the height against the seventh asked, `tee_red-v1` short in the
  leg. Untested: naming the leg length ("legs about half the height").

## Walks cut from a video (2026-10-27) — side view trial, three models, $0.38

**The owner, on the keyed-guide GIFs: "Nope not working"; try a model that makes video, then cut frames.** The
first frame — and the last, where the model takes one — is the look's own side stance (`tee_red-v3`, column 1) scaled
to 55 % of a portrait frame on flat chroma green; the clip asks for walking on the spot as on a hidden treadmill,
camera locked, facing held, the screen flat green. The clip is decoded with PyAV (`python3 -m pip install --user av`:
the machine's GStreamer has no H.264 decoder), keyed off its border colour (`sprites.chroma_key`), despilled
(`_walkers.despilled`), and one cycle found and spread over eight columns (`_walkers.cycle_of`: the lag whose legs
repeat best, from the frame whose legs are closest together).

- **Every model walks with both legs**, the near and the far one taking turns, in place and facing one way: what
  three image briefs could not get.
- `walkclip_side_veo-v1` (Veo 3.1 Lite, 720p 9:16, 4 s, first and last frame, $0.12): a smooth natural cycle of
  1.17 s, short steps, gentle arms. It slows into its last frame, which `CLIP_EDGE_SHARE` leaves out.
- `walkclip_side_seed-v1` (Seedance 2.0 Fast, 480p 3:4, $0.17): as good a gait, 0.96 s; the screen a duller green.
- `walkclip_side_grok-v1` (Grok Imagine 1.5 Lite, 480p 2:3, first frame only, $0.09): long stiff strides, arms
  reaching out ahead. Not used.
- The key leaves green specks and fringes round the feet, where the model shades the ground; `despilled` clears them.

**The owner: keep the actor in the middle, and make the walk symmetric.** Measured, the camera neither moves nor zooms
in Veo or Seedance (height within 2–3 %, the walk's own bob); the actor slides 1–3 % of its height. The cut is now
phase-locked (`_walkers.phased_cycle`): the feet's spread (`gait_signal`) finds every heel strike and passing, and of
every run passing–strike–passing–strike–passing the one with the most alike steps — length, time, and its end
repeating its start — gives the columns: passings at 0 and 4, strikes at 2 and 6, midpoints between. The slide is
taken out by one straight line through the cycle (`placed_row`), not frame by frame, so the sway stays and nothing
jitters.

- **Seedance walks symmetric, Veo does not**: Seedance's strikes come every 11–12 frames at one spread (the chosen
  cycle 1.3 % apart in length, 9 % in time); Veo's alternate long and short (spread 0.17/0.14) at 12–16 frames.
- **The owner: still jumping, and the end does not run into the start.** Both were the cut's. The line fit let the
  feet float off the floor with the clip's bob, and phase-locked columns at uneven gaps changed the walk's speed and
  left a seam. Now (`looped_cycle`, `registered_row`): the stretch that loops best — its frame one cycle on against
  its start, over one frame's change — with even halves, sampled at even steps of time; every crop ends at its lowest
  paint, so the bearing foot is on one floor; and each frame is slid onto the first one's head and torso by most paint
  overlapping. Seedance: seam 0.24 of a frame's change, steps 1.7 % apart in length and 8 % in time, 3 frames a column
  exactly, feet on one line.
- **A clip asked to loop is the smoothest** (the owner: the first frame should follow the last, and a short clip will
  do). A mid-walk passing frame from the first clip, keyed and put back on green, as both first and last frame, and
  "already walking … ends exactly as it begins, mid-step": the clip walks from its first frame to its last and closes
  on itself, so the cut searches it end to end (`looped_cycle(edge_share=0)` over every frame but the last, the
  first's duplicate). `walkloop_side_wan-v1` (Wan 3.0, 480p 3:4, 2 s, $0.21 — not the $0.10 its per-second price
  says): two even cycles of 0.97 s, strikes 20/19, end against start 0.05 of a frame's change. `walkloop_side_seed-v1`
  (Seedance 2.0 Fast, 4 s, $0.17): closes as well (0.18), its strikes alternating 18/16.
- **All five facings in one clip** (the owner: every angle at once, cheaply). `walkloop_all5_wan-v1` (Wan 3.0, 480p
  16:9, 2 s, $0.21): the five passing poses of `tee_red-v3`'s pose-guided sheet side by side on green, 300 px tall,
  as first and last frame, each figure's direction named left to right. All five walk on the spot, both legs, in one
  rhythm (0.93–0.97 s), seams 0.34–0.61 of a frame's change; the cut splits the frame into bands at the first frame's
  gaps and treats each band as a clip. Back-left drifts a little towards side-on late in its cycle and front-left
  dips its head mid-cycle. A neighbour's foot reaching into a band is cut off (`figure_of` keeps the widest run of
  painted columns). The feet-spread signal hardly moves for the back and front views (0.08–0.09) though both walk:
  a forward foot there is mostly hidden behind the body.
- **All eight facings in one clip, nothing mirrored** (`walkloop_all8_wan-v1`, Wan 3.0 720p 16:9, 2 s, $0.425 — 720p
  bills twice 480p). Seeded by one image of the look's eight passing poses (`walkers_seed8_tee_red-v1`, $0.027:
  image 1 the look's block for who she is, image 2 an eight-facing passing guide, `stick_figures` over
  `ENGINE_HEADINGS`), 420 px tall, laid two rows of four on green at 300 px, each figure's torso at its slot's middle
  and its feet on the slot's floor. The right-hand facings are drawn, not mirrored: the bag stays on her right hip.
  One fault in the seed: front-left drew the bag on her left hip.
- **Eight walkers walk slower**: a step every 25 frames, one cycle the whole 2 s clip (1.87 s, columns 7 frames
  apart). The engine steps columns by distance walked, so cadence costs nothing; `CYCLE_S` now reaches 2.2 s and a
  clip asked to loop on itself is offered whole. One window for all eight (`shared_cycle`): seams 0.35–0.73. The cut
  no longer re-centres (the owner: centre in the video, not the tool): each facing is its fixed slot of the frame.
  Drift is read against the median facing, every facing's arm swing moving its upper body alike.
- **The guide is the seed's, and it buys the facings**: without it (`walkers_seed8_noguide_tee_red-v1`, $0.014) the
  eight are still mid-step, but front-left came out nearly facing us and back-right with her face showing; with it
  all eight face true. The video model never sees it and walks on its own from the seed.
- **Cheaper video for the eight-facing seed** (the same `pose_all8.png`): Seedance 2.0 Mini (480p 16:9, 4 s, first
  and last frame, $0.14) walks both legs and holds its facings, seams 0.54–2.06 (front-right and back flagged);
  Wan 2.7 (720p, 2 s) bills a 5 s minimum, $0.50, and barely walks; Grok Imagine Lite (480p, 2 s, no last frame,
  $0.05) flings its legs into kicks and finds no loop. Wan 3.0 bills 480p at a flat $0.2125 for 2 s.
- **A new look by video alone does not hold its facings** (the owner: a new look has no rotated pictures of its own,
  only the existing ones). First frame `tee_red`'s five facings, no last frame, "in the first second each changes into
  the same police officer, then walks in place" (`walkmorph_police_*`). The change and the walk work on Veo 3.1 Lite
  ($0.12) and Seedance 2.0 Mini ($0.14); Grok flickers between the two people. But the facings collapse: Veo v1 turned
  back-left into a side view and front-left into a front one; Seedance Mini v2 turned back and back-left into side
  views; Veo v2 fell apart, its figures multiplying and swapping places. "Treadmill" draws a treadmill pad under every
  figure; "walk in place … nothing under the feet" does not.
- **One front picture, turned by the video model** (2026-10-28): the cast `walkers_cast_b-v2` (nine people standing,
  facing south, camera about 55°, a light pixel-art feel; "looking down shortens a body, never widens it" and
  "ordinary average build" keep a high camera from drawing them stocky) gives figure 1 alone, on green, as the only
  first frame; "walks on the spot … turns left in five equal stages" (`walkturn_*`, one roll each, $0.62 in all).
  Every model turns her front → profile → back. Veo 3.1 Lite (720p 9:16, 4 s, $0.12): the cleanest turn and the
  style held, the steps small. HeyGen Video 1 (480p, 5 s, $0.05, sound always on): turns and walks, style held.
  Grok Lite (4 s, $0.09): walks in profile, but flicks back towards us near the end. Seedance 1.5 Pro (4 s, $0.05):
  shuffles, and draws a floor shadow and a duller green. Seedance 2.0 Mini and Fast ($0.14, $0.17): jog rather than
  walk, the style drifting to painted. "Brisk" and "each foot lifting, swinging forward" are what made them run.
- **The turn in held stages, in one clip** (`walkturn2_*`, 6 s, one roll each, $0.90): "an easy everyday walk, never
  a run … one foot always on the ground … elbows straight" stops every model running. "Five equal parts, each held
  for two full steps, then an eighth of a turn within one step": HeyGen ($0.06) and Seedance 2.0 Mini ($0.21) hold
  the five facings in roughly even stages; Veo Lite ($0.18) rushes front to profile in the first 40 % and spends half
  the clip on the back; Seedance 2.0 Fast lingers on front-left; Seedance 1.5 Pro faces us for half the clip, then
  jumps to the back; Grok flips between front and back.
- **Five copies of the one front picture, each told its own facing** (`walkrow5_*`, first frame the figure laid five
  times in a row, 16:9, 4 s, one roll each, $0.62): every model moves the five as one, turning them all the same way at
  the same moment — Veo, Grok and the three Seedances alike. Only HeyGen ($0.05) splits them, and only three ways:
  the first stays front, the middle three turn to profile, the last to its back; no copy takes a 45° facing.
- **HeyGen, made to hold each copy's facing** (`walkrow5h_*`, 5 s, $0.05 each): each copy's turn named as a fraction
  ("an eighth … a quarter … three eighths … half a turn") turns four of them to profile at once, the fifth to its back
  only late. Magenta arrows on the floor under each copy, with that prompt, are ignored. Arrows with a prompt that
  names each copy by its arrow ("turns to face exactly where her own arrow points") hold front, profile and back
  from the first half second to the end, walking all the way, but both diagonals still come out in profile; the green
  flickers brighter halfway through, and the arrows stay drawn.
- **The diagonals by what shows** (`walkrow5h_cues`, `_reorder`, `_reordercues`, $0.05 each): describing the back
  diagonal by what we see ("her back together with her left side, only the edge of her cheek and no eyes … not in
  profile: most of her back shows") gets it, held to the end. The front diagonal never comes: "we still see her face
  … both her eyes still show" is drawn in profile on every roll. Reordering the row (front, side, back, then the two
  diagonals) changes nothing: the diagonals still copy the profile, the back one less so with the cues.
- **A hint figure under each copy** (the owner: a hint in the same style, never the look's own facings). The image
  model draws a stand-in in the five facings at once (`walkers_hint_a`: cast `walkers_cast_b-v2` as style picture,
  a stick-figure guide of the five standing facings at 55°, a woman and a man in plain grey, $0.032), every facing
  true, the front diagonal included. Laid smaller under each copy, the woman in grey makes HeyGen
  (`walkrow5h_people`, $0.05) turn the five copies five ways for the first time — front, front-left, profile,
  roughly back-left, back — and hold them, walking; but the copies take the hint's hair (a bun for the ponytail),
  and the hints walk too. Stick figures in their place (`walkrow5h_sticks`) are ignored: the copies go to profile
  and back, the sticks dance.
- **The hint flattened to a silhouette, fading out** (`walkrow5h_shapes`, $0.05): the woman in grey filled magenta,
  pale pink where skin shows, "only marks … fade away completely in the first half second", and the walk asked as
  "the planted foot slides back along the floor under her, as on a moving walkway". The silhouettes fade as asked
  and the copies keep their own hair, but the facings fall back to three (front, profile three times, back): the
  detail that leaked the bun is what steered the diagonals.
- **A hairless hint, fading out** (2026-10-28). A "female/male shop-window mannequin, bald" brief is refused by the
  image provider's safety system; "an unpainted clay game figurine … a smooth grey cap where hair would be" is not
  (`walkers_hint_b`, $0.032, five facings true, the heads a little large). Under the copies, fading
  (`walkrow5h_figurine`, $0.05): the copies keep their own look and the figurines fade, but the facings come out
  front, profile, profile, back, back. The grey woman in grey shading with her hair lifted to the skin's grey
  (`walkrow5h_grey`, $0.05) does not fade: each statue becomes a small copy of the girl, and all ten turn together.
  Of every hint tried, only the full-detail woman in grey steered five facings, and she leaked her hair.
- **The camera orbits, she does not turn** (`walkorbit_heygen`, HeyGen, 3:4, 5 s, one roll, $0.05): one front picture
  as first frame; "it is the camera that changes, not her … five shots of exactly one second, hard cuts on each
  one-second tick … an eighth of a turn further round her, towards her left-hand side", each shot named by where the
  camera stands. All five facings true, held still within each shot, her look kept, the side view a real stride
  (heel in front, toe behind), legs alternating. Hard cuts measured at 1.00, 1.88, 2.88 and 3.79 s (frame change
  three times the median; the turning clips have none), so shots of 24, 21, 24, 22 and 33 frames at 24 fps — about
  one step cycle each. The first shot starts from the standing first frame.
- **Each orbit shot as a loop** (`walkorbit_heygen` v1–v3, cut at its hard cuts — the strongest frame changes past
  twice the median, shots at least 0.6 s — each shot searched for its best cycle by `shared_cycle`): v1 (above) has
  the high camera, but its front shot starts from standing and its side and back-left shots step unevenly. v2 (6 s,
  a front warm-up shot first, each shot named by its turn in degrees and where her feet point, "exactly two steps a
  second … every shot holds exactly one whole cycle and ends in the pose it began with", $0.06) loops cleanly in all
  five facings (seams 0.47–0.91 of a frame's change, no flags) but drops the camera to nearly eye level, the figure
  larger. v3 (v2 with the turns as eighths and "its height, its distance and its steep downward tilt never change …
  never at eye level, never closer", $0.06) keeps the high camera but loops worse (four facings flagged) and changes
  the green's shade at every cut. HeyGen's "one second" shots run 0.88–1.2 s, the last one longer; a cycle of
  0.75–1.08 s fits in each.
- **Two seconds a shot loops; one does not** (`walkorbit_b` v1–v3, HeyGen, 3:4, 12 s, $0.12 each): a one-second shot
  is one cycle, so the cutter has one to three starts to choose from and takes whatever seam the shot ends on. Six
  shots of two seconds — a front warm-up, then front to back — "one whole stride cycle every second, every step as
  long as the one before it … every shot holds exactly two whole cycles and ends in the pose it began with" gave
  cycles of 0.92–1.08 s and seams of 0.28–0.56 of a frame's change in every facing of every roll, the step timing
  flag gone. The facings and the camera are what still fail. Each shot named relative to the last ("another eighth of
  a turn", v1) put the back-left shot facing the picture's right; adding detail to each shot ("one eye, her chest in
  front", "her face hidden", "never to the picture's right", v2) dropped the camera to eye level and drew back-left as
  a second profile. v3, the `walkorbit_heygen-v1` wording kept whole — each shot named by where the camera stands — got
  front-left, side, back-left and back true at a high camera, but zoomed into her face for the second front shot; the
  warm-up shot is a true front and is cut in its place (`qq sprites cut … --shots 0,3,4,5,6`). The camera still sits
  lower and nearer for front-left, side and back-left than for front and back: their figures stood 18% taller, which
  the cutter now scales out per shot; the pitch it cannot.
- **Nine looks in one orbit clip** (`walkorbit9-v1`, HeyGen, 3:4, 480p, 12 s, $0.12): the nine-person cast laid 3×3 on
  green as the first frame, the `walkorbit_b-v3` wording put as "nine small videos tiled side by side … the camera
  jumps … in all nine tiles at once, in the same way". The tiles stay put, all nine walk in step, the camera stays
  high, and every shot change is one hard cut for all (`qq sprites cut … --grid 3x3`). Back, back-left and front are
  true; front-left comes out nearly a profile, so it and the side read alike. Each look is about 85 px/m, a little
  under the engine's 96, and its loops seam at 0.5–1.9 of a frame's change against the one-look clip's 0.3–0.5 — the
  smaller figure's frame-to-frame change is mostly the video's compression. A look in green (the polo) keys to grey.
- **The shortest orbit, 5 s** (the linen-jacket man, cast figure 3, HeyGen, 3:4, 480p). `walkorbit_linen-v1` (6 s, a
  2 s front shot then four 1 s shots, $0.06): its back-left a near profile, front-left's camera lower, and four
  facings' loops flagged. Its side-to-back-left cut changed the frame only 2.2 times the median, and was missed until
  `orbit_shots` read the frame's upper half. Then two 5 s rolls of five 1 s shots, each with "walking from the very first
  moment" in shot 1 and back-left as "halfway between his side and his back", $0.05 each. All five facings are true and
  the camera stays high in both, and the front no longer starts from standing. Pace barely follows the brief: "quick,
  short steps, three steps every second … never a run" (`walkfast_linen`) gives cycles of 0.75–0.92 s and the best
  seams (0.41–0.78), against 0.9–1.1 s for "fast-forward footage … played back at double speed" (`walk2x_linen`,
  seams 0.58–1.06). Neither ran. Allowing cycles as short as 0.35 s (`--cycle`) gets one step cut as a whole cycle.
- **Four looks in one 5 s orbit fail** (`walkorbit4` v1–v3, HeyGen, 3:4, 480p, $0.05 each): cast figures laid 2×2
  at 0.55 of a tile's height, the `walkfast_linen` brief put as "four small videos tiled side by side … in all four
  tiles at once". The tiles stay put, all four walk in step and the camera stays high, at about 118 px/m a look, but
  no roll keeps its looks. v1 (figures 2, 3, 4, 9) cuts four times, not five: no back-left, the side held 2.4 s; in
  the back shot the linen man wears the white shirt of the tile below him. Seams 0.27–1.3, against 0.41–0.78 for the
  same man alone. v2 names the cuts' times and gives each tile a line on its clothes from behind: back-left lasts
  0.4 s between two side shots, and at the back cut two tiles' looks are swapped. v3 (figures 3, 4, 5, 8, no two
  alike in colour, the lines dropped): five shots at the asked times, but from back-left on the top row's two looks
  are replaced by the bottom row's. The 3×3 roll's 2 s shots kept every look; 1 s shots of several looks do not.
- **The cast's other seven looks, one 5 s clip each** (`walkorbit_<id>-v1`, `qq walkers orbit`, $0.05 each): every
  clip cut at five shots and every look kept its clothes in all five facings. `curly_navy`'s and `polo_green`'s
  back-left come out near a profile. `polo_green` on a blue screen (0, 71, 187) keeps its shirt green, where the
  `walkorbit9-v1` green screen keyed it grey. Each cut flags one to five facings (timing, length, seam), as
  `walkfast_linen` did, and none was re-rolled.
- **The uniforms, a cast of their own** (`walkers_service_a-v1`, `qq walkers cast`, $0.014, then a 5 s clip each,
  $0.50): three police, three paramedics and three recovery crew, the cast brief unchanged but for the people. Every
  look kept its uniform in all five facings, and the recovery crew's fluorescent vests stayed yellow on the blue screen
  `screen_for` chose for them. Two clips took the camera round the right side: `recovery_a` in all three side shots
  and `recovery_c` in back-left only, both cut mirrored (`--mirror 1,2,3`, `--mirror 3`). `paramedic_a` v1's
  back-left was a front-left showing her chest; v2 was true. The cuts flag more seams than the civilians' (one to
  four facings each); none was re-rolled for it.
- **Four of the eighteen walked their side at eye level** (found 2026-10-28, after install, by the owner): `curly_navy`,
  `polo_green`, `paramedic_b` and `recovery_a` v1 — a flat profile, no crown, full-length legs, their back-left near a
  profile too. Every one was asked "we see him in profile". Asked again ($0.20) with that gone and "the camera never
  comes down to eye level … we look down onto the top of his head and his shoulders" once for the clip, all four v2
  came out from above in every facing, back-left with them, and turned the right way. Three more sides sat only a
  little low, raising the scale flag — `paramedic_a` (0.79), `ponytail_red` (0.87), `recovery_c` (0.88) — and asked
  the same way ($0.15: `walkorbit_paramedic_a-v3`, `walkorbit_recovery_c-v2`, `walkorbit_ponytail_red-v1`) came out
  from above too, their sides scaled 1.04–1.12. Seven for seven with the new wording.
- **Five looks again, for wrong facings and size jumps the owner saw turning** (2026-10-28, $0.30): `paramedic_c`,
  `curly_navy`, `shirt_white`, `hoodie_black`, `polo_green`, one roll each with the brief unchanged. Three came out
  true in all five facings, the camera high (`walkorbit_shirt_white-v2`, `walkorbit_hoodie_black-v2`,
  `walkorbit_polo_green-v3`, kept at 4). Two drew back-left as a second profile — `paramedic_c-v2` and
  `curly_navy-v3` — so alike the side shot that the cut found four shots, not five. `paramedic_c-v3`, shot 4 alone
  given the cue that held back-left in `walkrow5h_cues` ("we see his back together with his left side, only the edge
  of his cheek and no eyes — not in profile: most of his back shows"), came out a true back-left with the camera
  still high; `ORBIT` now carries it. `curly_navy` had used its three rolls and stays on `-v2`'s clip.
  **The owner, on those: 1, 8 and 6 still change size as they turn, and `hoodie_black`'s north diagonals are
  swapped** (cut again with `--mirror 3`, `-v7`). The cut scaled each shot so its loop's median height matched;
  from in front or behind a stride drops the forward foot lower in the picture and stretches that height, from the
  side it does not, so the front and back came out about a tenth smaller than the side — 345 px standing against
  389 for `polo_green`. Each shot is now scaled by its stance's height (`cut_clip`), and `curly_navy-v7`,
  `shirt_white-v7`, `polo_green-v8` and `hoodie_black-v7` were cut again so, then the other fourteen from their
  clips with the options their variants' records name (owner: "we don't want inconsistent logic"): every look's
  stance within 1.4 % across its eight facings. Head width does not settle it either way — hair and hoods move it a
  tenth or more a look.
- **A true stance per facing, asked of HeyGen** (owner: each facing should start standing, then walk; 2026-10-28,
  `linen_grey`, $0.40, nothing kept). **Made in breach of the shape** — 10 s clips and a second clip per look, never
  asked — and the reason remotegen's core rule now says so outright; none of it is a flow. What it showed: HeyGen never
  stops a walk once it has begun. "For the first half second he stands still … then steps off" in 2 s shots
  (`walkorbit_linen_grey-v1`) and "walks in bursts, stop and go … every cut falls while he is standing"
  (`-v2`) both walked through every cut. A standing orbit before the walking one (`-v3`, ten 1 s shots) held the
  stand for its first two shots only, then walked, its facings out of order. A clip of standing alone
  (`walkstand_linen_grey-v1`) held all five facings still, at the cast picture's steeper camera than a walking clip
  keeps — its stances drew stocky against the walk. Five standing facings laid in a row as a first frame
  (`walkrow_linen_grey-v1`) stood at frame 0, then all five walked on the spot holding their facings, under one camera.
  Inside one 5 s clip, a stance is still the walk's frame nearest standing (`stance_column`).
- **The stance in one 5 s clip, on a minimal brief** (owner: keep the video prompt minimal, it confuses fast;
  `walkstance_linen_grey` v1–v3, HeyGen 480p 3:4 5 s, the cast figure as first frame, $0.15, nothing kept). Four
  sentences: five 1 s shots, hard cuts; one spot, the camera round to his left, high above; the five shots named; "every
  shot starts with him standing still, then he walks on the spot"; flat blue. Then each shot as "the same moment
  filmed five times" (v2), then as "five separate takes … every take starts again from the beginning" (v3). **No roll
  stood after a cut**: in all three only the clip's first frames stand, and every later shot opens mid-walk — HeyGen
  carries the walk on across a cut whatever the cut is called. The short brief keeps all five facings true (back-left
  too) and, once "always at the same distance" is in (v2, v3), the camera steady; without it (v1) the camera zooms in
  through each shot. With no pace sentence the loops seam worse — 0.22–1.80 of a frame's change against `walkfast_linen`'s
  0.41–0.78, five or six facings flagged a roll against two.
- **Three looks from the owner's photos, on the minimal brief** (2026-10-28, `walkers_photo_a-v1`, $0.03; clips
  $0.30). The cast: one Sunburst frame, image 1 the installed cast `walkers_cast_b-v2` for its style, images 2–4 the
  photos cropped to each person (`qq walkers cast --photos`), "draw each as the same person … take nothing else from the
  photo: not its pose, camera, light or background". All three came out standing south at the cast's camera and
  proportions, faces, hair, clothes, a handbag and a T-shirt's print kept; a backpack and a towel seen only on a strap
  in the photo were drawn whole. Kept as rolled. Then one 5 s clip each, `ORBIT` cut to four sentences (the owner: the
  video model confuses fast). v1 of each: facings in order, but `quilted_navy`'s side and back-left taken round her
  right (cut `--mirror 2,3`, kept at 4); `tee_print`'s back-left a near profile showing his chest; `flannel_hat` turning
  through her front shot, her front-left a plain front. "(mostly his front)" and "(mostly his back)" after the two
  diagonals gave `tee_print-v2` true in all five (kept at 4); `flannel_hat-v2` turned within every shot, and "never
  turns" (v3) held her front still, with her side leaning towards back-left — kept at 3, her last roll. Her front
  shot also walks towards the camera, which leaves her standing smallest of the looks (docs/tech-debt.md).
- **The owner on those three: they change size as they walk, and the walk does not loop** (2026-10-28; two more rolls
  each, $0.30, none kept). Measured: in the four-sentence brief's front and front-left shots the lowest foot slides
  6–21 % of the figure's height a second down the picture while the head top barely moves — the walker walks towards
  the camera, which `walkfast_linen-v1` (the long brief, "the planted foot slides back … as on a moving walkway") does
  within 5 %. The cut now flags it (`ADVANCE_FLAG`) and takes it out (`held_still`: a straight line through each
  shot's heights and lowest paint, every frame scaled and moved back onto its mean): front rows' height spread over
  the loop fell from 0.17–0.27 to 0.06–0.07 and each look's stance from 0.76–0.81 of its cell to 0.84–0.88, with no
  new roll. Asked back in the brief, the walkway held the feet within ±6 % a second but turned three of the six
  diagonals round the walker's right. "Four steps every second" left HeyGen's step at 0.42–0.50 s, the same as
  "three". The seams did not move with either: each 1 s shot holds one cycle of 0.85–1.0 s and a few frames, so the
  cut has two or three starts to choose from, and the seam is what the shot gives (docs/tech-debt.md). Re-cut and kept
  from the first rolls: `quilted_navy-v2`, `flannel_hat-v2`, `tee_print-v2`.
- **The owner: size is still the biggest issue, facing down against down-left** (`shirt_white`). Every facing was
  scaled to one standing height, but the front shot keeps the cast picture's steep camera while HeyGen takes the
  others lower, so at one height the front draws its head and body larger: `linen_grey`'s head 35 px from in front,
  28–30 from every other side. Head width cannot be the measure — a bun, a ponytail or a brim swings it up to twice
  over between facings. A width across the picture does not change with the camera's pitch, and a body is as wide from
  in front as from behind: the front is now scaled so its torso (25–45 % down, above a belt) is as wide as the back's
  (`_walkers.front_by_width`), and only ever shrunk — `police_b`'s back was the shot taken nearer, 13 % wider. Fronts
  moved ×0.82–1.00, `linen_grey` and `police_a` most; his front head 31 px against 32 from front-left. The diagonals
  are left on height: HeyGen seldom takes them at 45°. All 21 looks were cut again from their clips with this and
  `held_still`, their options read off their kept variants' records.
- **The head outlined in every facing** (`tee_print`, `qq props seed tee_print --cell F,0`, each jaw placed by eye and
  checked by a second agent): widths 33.0–37.1 cm, within ±6 % of their median; heights crown to chin 37.1–39.1 cm
  but front-left 44.5 (and the back 32.5, crown to nape) — its shot's camera steeper, more crown and face showing. By
  the head's √(width × height), the back by width against the front, the cut's facings were 0–3.5 % off, front-left
  the most: `qq sprites cut … --scale 0=1.017,1=1.004,2=0.997,3=0.965,4=1.005` (`tee_print-v4`), recorded in the
  variant's cut. Barely visible at the game's scale; the cell, set by front-left's tallest frame, shrank 4 %, so the
  whole look now stands 4 % taller (docs/tech-debt.md).
- **"The camera travels along with him" in place of "walks on the spot"** (`walkorbit_tee_print-v5`, one roll, $0.05,
  the owner asking for a constant angle): "walks forward the whole time … the camera travels along with him, so he stays
  the same size in the middle of the picture, and only its angle changes". Drift fell where it was worst — front +10 →
  +4 %, front-left +17 → −13 %, back +9 → 0 % of his height a second — but changed sign rather than vanishing, so
  `held_still` is still wanted. It took the camera round his right side (cut `--mirror 1,2,3`, the print's word then
  reading backwards on the side and front-left), and the diagonals seamed worse: front-left 0.91 → 1.33, back-left
  0.77 → 1.55 with its timing off. The owner: it kept his size consistent. v6 ($0.05) named it "a tracking shot: the
  camera moves with him at exactly his walking speed … his feet always at the same height in it", the walk "at one
  steady pace", and the turn its own sentence ("Between shots only the camera's angle changes, round to his left"):
  side and back-left went round his left but front-left round his right (cut `--mirror 1`); front and back loop well
  (seams 0.42, 0.63) with no drift; front-left −17 % and back-left +8 % are held still. Side to back-left came as a
  sweep over four frames, not a hard cut (cut `--split 68-72`), leaving each 22 frames, and both seam at 1.7–1.8.
  The owner: the angle is good, but the camera zooms or pans within a shot — measured on the diagonals, front-left's
  feet climbing 57 px/s with the head still and its torso narrowing 28 % a second, as the camera kept circling.
  v7 ($0.05): "fixed to him, as if on a rig that moves with him: within a shot it never zooms, pans, tilts or circles
  … Only at each cut does the camera's angle change", and "instant hard cuts". Within-shot motion roughly halved
  (front-left 25 px/s, −21 %; back-left 10 px/s, −9 %; front, side, back still within 16 px/s and 3 %), five hard cuts
  found, but front-left and back-left round his right and the side round his left (cut `--mirror 1,3`), so the print
  reads backwards on two rows; front seams 1.47. Which side a facing was taken from is read off which way he faces: the
  sheet's left facings face the picture's left. Kept at 3 (`tee_print-v9`, its diagonals sized by width). v8 ($0.05):
  "each from its own fixed camera" for the camera going "round", and "already walking in the first frame": all five
  shots round his left, but the diagonals turned more within their shots than ever (torso +51 % and +36 % a second),
  the front still started from standing, and every seam 0.97–1.46. Across v5–v8 how far a diagonal turns within its
  shot swings by roll more than by wording; not kept. v9 ($0.05), the long brief `walkorbit_hoodie_black-v2` was asked
  with (sixteen of the installed looks were cut from it, `tee_print` never before): all five shots round his left (no
  mirror, the print true), front and back the steadiest yet (feet and sideways under 2 px/s, torso within 0.2 %),
  back seam 0.49 and back-left 0.54; but front-left turned +41 % a second (v7 −21 %) and its timing is off (0.81), the
  side seams 1.29. The torso's change over a front-left shot measured on the sixteen long-brief clips runs +21 to
  +69 % a second too — a measure that mixes arm swing with the turn, so it does not say the long brief turns more.
- **What makes `curly_navy` read better than `tee_print` is the drawing, not the walk** (2026-10-28, the owner: walker
  2 on the showroom "very good"). Its seams (1.1–1.65), drift and turning within the diagonals are no better. But its
  clip kept the cast's bold dark ink outline and flat colours, and its legs (mid-blue jeans, white trainers) stand out
  on the asphalt. `tee_print`'s first frame, from the photo cast (`walkers_photo_a`), has thinner lines and smoother
  shading, and every one of his clips (v1–v9, the long brief's "flat-shaded pixel-art game-sprite style" included)
  re-rendered him soft and near 3D with the outline gone; black, grey and dark-grey trainers then lose the stride on
  grey. A 1 px dark rim laid round each figure at install was tried and taken out (2026-10-29): opaque, it counted as
  the figure's solid part, so the selection ring (CTL-1) started a pixel further out between two dark lines and read
  thin; and the owner: every look already carries a dark rim of its own.
- **The rig brief with each shot's facing named** (2026-10-29, `.tmp/gen/briefs/orbit_v2_template.txt`, one roll
  each, $0.10): `tee_print-v7`'s locked camera ("within a shot the camera is locked to her: it never zooms, pans, tilts
  or circles") with the long brief's per-shot facing ("she faces towards us and to the picture's left"), "walking from
  the very first moment", "goes round her left side only", and "drawn throughout in the first frame's style, with its
  dark outlines and flat colours, never a soft 3D render". Both clips went round the left in all five shots — so a
  one-handed prop stays in one hand — and neither kept an outline. `flannel_hat-v6`: seams 0.28–1.27 (her `-v3` clip
  0.59–2.13), drift 7 % (21 %), a true side; her towel and backpack drawn as one white backpack; kept at 4
  (`flannel_hat-v5`). `quilted_navy-v4`: her side at eye level (5.4 head-widths tall against `-v1`'s 4.4) and her front
  and back seams 1.3–2.0 (0.5–1.0); not kept. One win in two: not yet `ORBIT`.
- **The owner: walking south, `flannel_hat` and `quilted_navy` "move with only one leg and then cut and repeat"**
  (2026-10-29). Which foot reaches lower, frame by frame through `walkorbit_flannel_hat-v6`'s front shot: one foot
  leads frames 2–10, the feet stand together 11–18, the other starts to lead only at 19 and is still reaching when
  the shot ends at 23; her back shot the same. HeyGen walked her step, pause, step, a stride of 1.2–1.3 s, and a 1 s
  shot holds one step and the start of the next, so the eight columns are one leg's step and a cut.
  `walkorbit_curly_navy-v2`'s front: one foot 1–6, together 7–14, the other 15–21 — both steps inside the second. The
  cut's `legSwap` (columns 2 and 6 against the stance) reads the pause as a swap and did not see it.
- **A 10 s clip of five 2 s shots for those two** (2026-10-29, the owner: "do 10s but only for this two"; $0.20,
  one roll each, `walkorbit_quilted_navy-v5`, `walkorbit_flannel_hat-v7`): the rig brief with "five two-second shots"
  and the long brief's "never comes down to eye level … the top of her head and her shoulders". Both went round the
  left in all five shots — the bag in one hand, the towel on one shoulder, her grey backpack back — and neither side
  came at eye level (4.38 and 3.38 head-widths). Every front and back loop holds one stride, each foot leading for
  three columns; every loop 0.96–1.08 s, seams 0.38–0.80. Two cut changes came with it: a shot is at least 0.6 of
  the shot a facing is asked for (`MIN_SHOT_SHARE`; held at 0.6 s, `quilted_navy-v5`'s front was cut into three on
  its strides), and the side and diagonal loops are held within 15 % of the front and back loops' stride
  (`stride_bounds`: her side took 0.75 s, a step and a half, against 1.0 s). Both kept at 4 (`quilted_navy-v5`,
  `flannel_hat-v6`). Then `tee_print` the same way (`walkorbit_tee_print-v10`, $0.10, the owner: "do 10s for walker 11
  as well"): round his left in all five, the print true, the first cut of any look with no flag — every loop 1.0 s,
  seams 0.30–0.50, drift under 4 %, each foot leading three columns front and back — and his diagonals turning within
  their shots +9.6 % and −5.7 % a second, against `-v7`'s −21 % and −9 %. Kept at 4 (`tee_print-v10`).
- **A fourth photo look, alone on its cast frame, its walk asked symmetric** (2026-10-29, `shirt_burgundy`, the owner:
  "again allowed 10s. Symmetric."; cast $0.026, clips $0.20). The photo held a pen in one hand and a water bottle in
  the other; cropped short of the bottle and described "both hands empty, nothing carried", the cast (`walkers_photo_b-v1`,
  one figure) drew him empty-handed, glasses and beard kept — `CAST_PHOTOS` now lets a look's line overrule "anything
  carried". `walkorbit_shirt_burgundy-v1` (the `tee_print-v10` brief as it was) came round his left in all five, a high
  true side, but limped: from in front his right foot lifted for about six frames a cycle and his left for two or three
  (steps 0.5 off in time). v2 put the pace sentence as "his left and right legs take turns exactly alike, each foot
  lifting for as long and stepping as far as the other": front 0.07 and back 0.0 off in time, loops 1.13–1.17 s, seams
  0.45–0.66, only back-left flagged (strides 0.26 unlike). Kept at 4 (`shirt_burgundy-v1`). One roll each way: the
  parity sentence is untried on any other look.
- **A longer Veo clip with a negative prompt was worse** (`walkclip_side_veo-v2`, 6 s, `negativePrompt` through
  `remotegen video --option`, "every step exactly as long as the one before", $0.18): it walked unevenly with pauses
  and one forward kick, its best cycle 2.3 s.

## One leg walks (2026-10-27) — `walkers_solo_a` v2 and v3, $0.029, nothing kept

**The owner: the walk moves only the leg nearest us; both legs must take turns.** Read off v1 leg by leg, every column
of every facing has the same leg in front — the side view the same foot forward, the back view always the right foot
low, the front always the left — at strides of different widths: one stride drawn eight times, never its opposite.
`cut` now measures it (`_walkers.legs_swapped`: how far the two full strides' leg silhouettes differ, against the
stance) and flags a facing under 0.6; v1 reads 0.11–0.43 on four of five.

- **v2** described each column by which leg is in front, what that looks like in each row (near and far leg, a foot
  forward higher or lower in the picture) and that columns 5–8 are 1–4 with the legs swapped. The front row began to
  alternate and the side row partly; **the frame came back 10 columns wide**, so it cannot be cut.
- **v3** shortened the cycle to a line a column, asked for exactly eight in a row and the far leg painted a shade
  darker. **9 columns**; the front row alternates, the side row still mostly repeats one stride.
- **The more a brief says about the columns, the less the grid holds**: v1's short cycle kept 8×5, both longer ones
  did not. Words alone have not got opposite strides and a kept grid in one frame.

## The pose guide (2026-10-27) — `walkers_guide_a`, `_b`, `_c`, one roll each, $0.077, all three kept

**The owner allowed a second picture for walkers: a pose guide** (`_walkers.pose_guide`, laid by `qq walkers brief`
as `.tmp/gen/templates/walkers_pose_guide_8.png`). It is drawn, not generated: a 3D walk cycle — thigh, knee, arm and
elbow swings (`GUIDE_SWING_DEG`) — projected orthographically at the game's 40° view into the frame's own 8×5 grid,
the left limbs blue, the right red, the nose a dot. The brief says what the colours mean and to take the poses and the
grid from it and nothing else, and its column list is back to a line a column.

- **All three held the 8×5 grid and swapped legs** on every facing: `legs_swapped` 0.62–5.1 (`coat_brown-v2`),
  0.67–1.44 (`tee_blue-v2`), 0.86–1.72 (`tee_red-v2`), against 0.11–0.43 without it. The side views show the far arm
  forward in columns 6–8 only, the second step's.
- **Strides are wider and the figures slimmer** than v1's, nearer a real walk; nothing of the sticks, colours or grey
  was copied.
- The frame costs $0.026 with the guide against $0.013: the second picture's tokens.
- **Weakest still: the side and back-left views**, where a near-leg stride and a far-leg stride share a silhouette
  and differ only in shading — `legs_swapped` is lowest there. Judge them in the GIF.

**The owner on those GIFs: the walker turns a little during the walk, the step is too long, the in-betweens are weak.**
`walkers_guide_c-v2` (`tee_red-v3`, $0.027, kept at 4) was asked with the guide keyed instead of a sine
(`GUIDE_GAIT_DEG`: contact, down, mid-stance, heel rising, on the toes, lifting behind, passing, reaching), the thigh's
swing cut from 26° to 15° and the knee's lift behind kept under a kick; the brief names each column by its phase, asks
a relaxed stroll with steps no longer than the guide's, and holds one facing a row ("head, shoulders, hips and feet
the same way as the row's first column"). The side row came back a clean cycle of eight distinct poses, the steps
shorter, every row on one facing; `legs_swapped` 0.77–1.45. Left: a few small grey specks beside the feet in two
cells, under what `use` clears at the sheet's size.
