# Drawing the town — decision log

Why this slice reads as it does. The rules themselves are [requirements.md](requirements.md); how a type
works is its own XML docs.

## 2026-10-30 — a casualty is its stance, reddened, and a walker has one sheet

**The owner: "animation should be reddened standing frame."** A body down in the road is the walk sheet's standing cell
at the facing it went down at, multiplied by `Theme.Casualty` through the instance's tint (`PersonSprites`,
`sprites.glsl`), on host and device alike. A tint now says what a walker is as well as how hard a mark was pressed; the
second sheet a look carried for lying down, and its atlas slot, are gone, and so is the depth order's rank for a body
lying flat (PER-31): a casualty stands among the others by its feet.

## 2026-10-30 — the atlas is held in BC7 or ASTC, baked by the build

**The owner installed KTX-Software and chose the build-time route**: every sheet is encoded once to UASTC and transcoded in
the build to BC7 and to ASTC 4×4 (`SheetBake`), and a device fetches the one it samples — BC7 on desktops, ASTC on phones,
the pictures where neither — so a page carries no transcoder and a device never downloads a format it cannot use. A
quarter of the device memory of `rgba8unorm` pages, and a page's heap holds no blocks: they go to the device straight out
of the archive that brought them.

- **Boxes on block corners, a gutter a block wide**, extruded in the build: a block on a sheet's rim holds nothing of a
  neighbour, so clamping at a sheet's edge stays true as the one-texel gutter made it for texels.
- **The generated sheets are baked like the rest** — they depend on the catalogues and the figures alone — and a run
  checks every sheet against the record (`BakedSheets.Matches`): a desktop whose art or figures moved since the bake packs
  pictures and says so; a page, which holds no picture to fall back to, refuses.
- **UASTC at effort 2, no RDO**: effort 4 bought 0.3 dB for 15× the time; RDO 4–10% of the download for 1–9 dB.
- **No ETC2 pack**: every device that runs WebGPU samples BC or ASTC.
- **One size a sheet**: blocks are not resized on a device, so a player's cars and walkers packed at half their texels
  were a second copy of each in every pack. The owner chose the one size and a player's zoom as close as a reader's
  (multiplayer log, 2026-10-30).
- **Measured**: the showroom drawn from BC7 differs from the pictures by more than 24 levels in none of its pixels on the
  desktop, and the page draws it alike in BC7 and in ASTC (SwiftShader, `qq web --sheets astc`).

## 2026-10-28 — walkers are put in the order drawn by counting, on the device, every frame

**Walkers were drawn in roster order**, so two that overlapped were drawn either way round (PER-31). The owner asked for
the fix to be the device's alone, since the desktop sends nothing of a walker to be drawn.

- **A third pass of the emit counts, and sorts nothing** (`sprites_order.comp`): the write pass builds the walkers
  shown aside, and each is written to the head of the over run at the count of those that rank before it. Which
  walker comes before which is decided again every frame, because the visible walkers are rebuilt every frame anyway.
- **A workgroup a walker, its threads sharing the count.** A thread a walker that counted every other one, one read
  after another, cost 61 µs for 195 walkers; reading them a tile at a time into shared memory cost 23 µs; a workgroup
  each costs 3 µs (`--bench deviceworld`, Saltbay's whole town, RX 9070 XT).
- **Not a depth test**: it needs a depth attachment, a pipeline of its own and a draw of its own for the walkers, and
  an alpha cut-off that either hardens their edges or leaves the ground showing between two that overlap.
- **Not buckets by row**: an atomic place within a bucket is in no fixed order, so walkers on one kerb line, at one
  `y`, would swap in front of each other frame by frame. A count is the same every time.
- **Not an order of the whole town kept from tick to tick**: a walker coming out of a door appears at any `y`, and a
  few swaps a tick put it right only after many ticks.
- **The host's fill sorts by the same key** (`PersonSprites.Depth`), so the page draws walkers in the same order, and
  the probe that holds the device's instances to the host's still compares them one to one.

## 2026-10-26 — a corner files its surface in six bits

**Every one of the sixteen surfaces five bits held was taken** when the shoulder (GEN-75) came, so the word gives the
surface six bits and the third tint channel, and a strip's across, eight: a tint is a few shades to begin with, and a
strip is a few hundred texels across at most. **The shoulder takes no binding of its own**: its looks are laid in the
walk's array after the walk's (`ProjectPaths.GroundSurfaceLooksAfter`), and both vertex stages read the walk's period
and patches for it. A separate intercity asphalt was built the same way and dropped at the owner's word, the one asphalt
now wearing its art.

## 2026-10-24 — the ground is generated, worn in two looks the patches choose between

**The owner: "improve [ground and water] to look better, more matching our new car and building models. More
diverse. Generate new assets."** A first pass — one mosaic per surface, its tiles evened onto one tone, a faint tint
over 45 m, the walk paved in square slabs — was judged worse: **"dont use 'tiled' textures - they dont rotate nicely",
"grass feels less quality than it was before", "i see no variations"**. Of the ways then offered, the owner chose
**textures blended by noise**, **painted like before**, **quality low**.

- **Two looks a surface, blended by a field that does not repeat** (`GroundPatches`): a kept lawn and a worn one, fresh
  asphalt and worn, clean concrete and weathered. Where one gives way to the other is the field's, a couple of dozen
  metres to a lump, so the town shows patches at every zoom and no period anywhere. **One texture a surface, its looks
  its layers**: no binding is added on either head, and the five surfaces still differ in size.
- **No look has a direction or a grid.** A texture is anchored to the world's axes, so slab joints, ripples running one
  way or a rust drip are square to the world and not to the street they lie under; the walk is poured concrete.
- **A look keeps half its tone against the first** (`_surfaces.LOOK_TONE_KEPT`): left whole, the worn lawn read as a
  camouflage blotch and the weathered walk as smudges along every street. Within a look every tile is evened onto one
  tone, or its quarters show as a chequerboard (`ground_a-v1`).
- **Painted, not photographic.** At quality low the model draws fine detail as speckle mush; a calm base with sparse,
  crisp details a few decimetres across reads as quality (`raw_assets/world/terrain/ground/surfaces/prompting.md`).
- **The tint stays, and moves with the looks**: the same field pales and warms a surface where it is worn.
- **The walk is footway asphalt**, the owner's pick of three city-style trials after rustic ones (flags, cobbles,
  aggregate) were "not fitting": a surface's tried looks stay in its file's `trial` and are kept, never joined.
- **The shore is a surface of its own** (`Surface.Shore`): the owner picked a pebble trial meant for the walk "for
  river/sea connections". It was the walk's texture before; its binding is appended after the last, as the track's was,
  so no binding moved.
- **The shore's edges fade and are not lined** (GEN-2c): the owner, "instead of solid line we can blur it to each
  connection". The grass fades out onto the pebbles from the shore's outer edge and the water from its own
  (`View.ShoreFadeM`), both on the shore so neither reaches a walk. **Blended, because a fade is cover and the ground's
  corner has no room for a second surface**: a fading corner files its cover where a tint goes, and the ground pass
  blends — opaque everywhere else, so nothing else changed. The two rings that left the lines went from the plan.
- **Both kerbs are laid as their stones** (TER-3d, TER-7b, 2026-10-25): the owner, on the walk faded into the grass,
  "sidewalk is weird. Lets bring back solid separator … with some texture … add car-sidewalk kerb texture as well. So
  its not just a line" — which put the walk's kerb back in the `P0` layer list. Each kerb is a strip of generated art
  laid along its line, as the track is (`Surface.Kerb`, `Surface.Edging`, `World.Terrain.KerbLooks`), its corners
  filing how many pictures along they stand, the picture's low side — the riser — on the road's side of the town's
  kerb and the grass's of the walk's. **Generated, not drawn by the stage**: the owner, on stones the shader drew,
  "did you draw it yourself? generate some texture instead". Laid at the cars' grid (96 px/m), a kerb being a fifth
  of a metre across and read up close. The town's kerb is no longer the road's white edge line. **Each edge its own
  stone, and each casting a shadow** (the owner: "use different one for road-sidewalk and sidewalk-grass … make it
  nicer, it looks just as a line"): granite kerbstones on the road and a row of granite setts on the grass, and a
  band darkening the ground beyond each low face (`Surface.Shadow`, `View.KerbShadowM`), laid from the face and never
  under the stones so the part's filing cannot draw it over them. **Then softer and less alike** ("Dont do contrast
  black border separation. Make some variations, and 'real' feel"): the strips drawn again six metres long with
  hairline joints and no two stones alike, cut joint to joint so four or five metres of stones pass before one
  repeats; each kerb wears the walk's patches on a field of its own; the shadow a fifth of the light, not half.
- **No look is drawn as its repeat** (2026-10-25): the owner, on the review that found each look's 16 m period plain
  wherever one look stood alone, and a two-tile look's quarters mirrored copies of each other, "fix repeats, add
  improvements … water, pavement, grass, sidewalk all must have its own". The ground is cut into hexes five metres
  apart (`View.GroundScatterAcrossM`), each reading its look from a place of its own and blended into its neighbours
  — **offset and never turned**, because the looks are lit from one side and a turned hex is a stone lit from the
  wrong one. **A field per surface** (the lattice hashed with the surface's number, each its own width): read off one,
  lawn, walk and road wore at the same places. The tint was raised, the grass's most; a fade's cover is ragged at its
  middle (`View.GroundFadeRagged*`) so turf meets the shore in tufts; and the water's two looks drift with the
  prevailing wind at their own shares of it (`GroundPatches.DriftAt`), so they slide over each other.
- **The owner then allowed quality high for the ground** ("try again with better quality setting"): the land is
  painted at high; the water stays as drawn at low, high drawing it in rows of waves.

## 2026-10-25 — the water sways under three trains of sine waves

**The owner: "review how water is rendered - lets add some cheap wave animation, that does not affect performance
much".** The two drifting looks moved the water but never made it undulate.

- **Analytic, not another texture** (`WaterWaves`): three trains, the longest down the prevailing wind and two crossing
  it, whose summed height moves the picture downwind and lightens the crests. A water pixel pays three sines, three
  lumps of value noise and no texture read; every other surface pays one `fwidth`.
- **Then made not to repeat** (the owner: "can we make the pattern less repeatable?"): straight crests of one height
  crossed in a lattice that read as stripes. Each train's crests are bent by lumps of their own
  (`View.WaterWaveBendM`), and the waves run higher where the water's patches show its lively look and lower where
  they show the still one (`View.WaterWaveSwellShare`) — read off the field the looks are already blended by, so the
  swell costs nothing.
- **The waterline laps** (the owner: "can we also make connection to shore have this effect"): the water's fade onto the
  shore is moved up it under a crest and back under a trough, by the same height the colour is
  (`View.WaterWaveSwashM`), most at the fade's middle and not at its ends, as the ragging is — so the fade still lies
  where it is laid and needs no wider band. **Then foam along it** ("make it more visible"): the swash raised to most
  of the fade's width, a soft edge moving within a metre and a half still read as barely moving, so the fade's middle is
  whitened wherever the swash has put it, most as a crest runs up (`View.WaterWaveFoamShare`).
- **The phase is worked out at the corners**: it is linear in the place, so the interpolation is exact and one varying
  carries it. The rest the ground's fragment stage reads from the camera block (`ground_camera.glsl`), which both heads
  now show that stage: the figures would have taken three more varyings, and a page has sixteen.
- **The speed is derived from the length** (deep water, ω = √(g k)), never authored, and the phase is wrapped on the
  CPU in double so a long-running clock never steps the crests.
- **A train narrower on screen than `View.WaterWaveLeastPx` fades out**, since a crest a pixel or two across flickers
  rather than moves; zoomed out, only the long train is drawn, and below about two pixels a metre none.

## 2026-10-24 — clouds are a field over the frame, not puffs

**The owner: "review smoke mechanics — lets add clouds now — only showing as shadows"** (`OBJ-8`).

- **Not the smoke's machinery.** A puff is a sprite laid off a track the device keeps, because it leaves a moving
  body; a cloud leaves nothing, so it needs no track, no emit and no room: a field read off the clock and the seed, one
  triangle over the frame, and a frame's whole cost is the two vectors it writes into the camera's block.
- **Drawn on every Vulkan head and in a picture**, where the smoke is the device-stepped world's alone: nothing of it
  is stepped, so a `--shot` shows the sky the window does.
- **Carried the wind's prevailing way, not the wind of the moment** (`Wind.PrevailingRad`): the air a kilometre up is
  steadier than at the ground, and following the swing would need the wind's carry integrated from the start of the
  clock — the one thing that would have to be kept.
- **The edge is derived, not authored** (`SimConfig.CloudPenumbraM`): the clouds' height and the sun's width are the
  figures; the blur they make is read off them.
- **The owner: "make them less dark and more cloud like".** Four octaves of blots taking 30 % of the light read as ink
  stains: the field is now six octaves bent by two coarse ones (billows, and lumps down to 16 m), and a shadow deepens
  over `CloudFigures.ThickensOverM` in from its edge to 22 %, so a fringe is thin. At 80 m in from the edge the whole sky
  read as a haze over the ground; at 30 m the clouds are patches with clear sky between.
- **The owner: "make them 5% lighter and less dense".** Taken as five points off the light a shadow takes
  (`ShadeShare` 0.22 → 0.17) and a quarter of the sky covered rather than 35 % (`CoverShare`).

## 2026-10-24 — a plume's root is its pot, and a diesel's its stack

**The owner: "review where smoke originates — it seems to be off centre of the chimney sometimes, especially on
trains"** (`OBJ-7`, `TRN-15`).

- **Where a puff leaves was right**: a pot's mark is on its picture's flue, a stack's on its picture's, and the youngest
  puff the emit draws stands on either (0.00 m off a diesel's stack at 70 km/h). **What showed off centre was where the
  smoke turned visible**: a puff is born clear and thickens, and while it did it was being carried off.
- **A chimney's puff rises before the wind takes it** (`ChimneyFigures.RiseS`, which was the fade-in, `FadeInS`): it
  stands on the pot as it thickens, since straight up is no way at all from above, and only then is carried. It had been
  carried all of its 0.4 s fade-in — 0.6 m downwind at the breeze, more in a gust, so the root stood beside the pot and
  moved with the wind. Shortening the fade-in alone left the root on the gaps between the young puffs, a third of a metre
  apart; halving the time between puffs closed them but doubled the plume's thickness, and was not taken.
- **A diesel's smoke is thickest a twentieth of the way through its life** (`SmokeFigures.StackThickestAt`, from 0.15):
  pulling hard, a puff thickened over half a second while the train ran on from it, and its smoke turned dark two and a
  half metres behind the stack.

## 2026-10-24 — smoke goes where the air of every moment since took it

**The owner: "review wind logic — make sure it's changing direction smoothly and slowly; direction and strength are two
independent params changed independently"** (`CAR-54`, `OBJ-7`).

- **The wind itself held**: its bearing swings 60° either side over five minutes, under 1.3° a second at its fastest,
  and its speed gusts on a sine of its own phase and period. Both are now terms of their own (`Wind.BearingRad`,
  `SpeedMps`), neither reading the other's figures.
- **What was wrong was the smoke**: every puff was placed at the frame's wind times its age, so each change of wind was
  laid on the whole plume at once — a chimney's plume stretched and shrank by two fifths every gust, nine seconds a
  beat, and a turn swung it rigid about the pot. Now a frame carries the air of 32 moments back over the longest any
  puff lives (`Wind.Back`: how far the air has carried since each, Simpson's rule a step, and its velocity then), and a
  puff is read between them: carried by the air it was out in, closing on the air it left in. A turn bends a plume from
  the pot out, and a gust is a stretch of it carried further.
- **The emit was not given the wind's formula** to integrate a puff at a time — that would be a second copy of `Wind`;
  the host's 63 evaluations a frame are the whole cost.

## 2026-10-24 — every chimney takes its turn, and a roof vent is not one

**The owner, shown a closed box and a domed cowl smoking: "fix a few mislabeled chimneys — they are not, and instead
focus on finding real chimneys: they look exactly as the working ones and don't smoke"**; then "all of them must be
marked and be able to smoke, but you can periodically disable some of them and enable other, ideally without extra
properties — just by id" (`OBJ-7`).

- **Two looks are not chimneys**, and the owner drew the line at them: a closed box with a grey inner square
  (`apartments_deep_20`, `tower_06`) and a domed cowl on a square plate (`apartments_deep_16`, `_19`, `garages_13`,
  `tower_02`, `condo_pool`, `hospital_02`, `office_01`, `school_01`, `school_deep_02`'s second, `warehouse_saw`), with
  the round drains (`garages_deep_07`, `tower_deep_06`). Open pipes, capped pipes on a round collar and open-topped boxes
  stay chimneys.
- **No real chimney was unmarked**: every brick stack on every roof was counted against its marks, and the unmarked
  roofs hold none. The stacks seen cold were the three in five the seed had never lit.
- **A fire takes turns, off its key alone** (`ChimneyFigures.FirePeriodS`, `SmokingShare`): every pot is laid, its row's
  key the stack's fire above `ChimneySprites.PotBits` and the pot below, and the emit relights or lets out each fire every
  period off a hash of the fire and the turn, each fire's turns staggered off its own key. It is asked at the moment a
  puff left the pot, so a plume grows out of a pot and the last one off a fire let out drifts away whole. A per-row
  flag or phase was the other way, and was not wanted.

## 2026-10-24 — a car's smoke is short, drawn over the cars, and shows idling

**The owner: "make car smoke a bit shorter and more dependent on thrust"**, then **"a bit shorter and starting closer to
car center (a bit)"**, then **"active car — when driver is on or when in manual control — should do idle smoke as well.
right now its only on throttle"** (`CAR-54`).

- **A car pulling away puts out a short, thick trail** (`SmokeFigures.FullLifeS` 0.45 s): a car at speed lays it
  along its way, a quarter shorter than it was. The stack's is untouched.
- **A car idling puts out a wisp that shows**: two-thirds as opaque as at full throttle, under half as wide, and blown
  out of the pipe hard enough (`IdleOutflowMps`) to clear the bumper before it thickens. At a third as opaque and blown
  out at under a metre a second, an idling puff thickened under the body or too faintly to see, so a car smoked only on
  the throttle. What smokes is unchanged: a car its headlamps burn on, driven or under a hand (`CarLamps.Showing`).
- **The share between the pairs is the throttle's, laid linearly**; a curve on it was the other way, and was not needed.
- **A car's smoke is drawn over every car** (`PartSmoke` last of a car's parts in `sprites.glsl`), its own and any
  driving through its trail, asked for over the bodies hiding it (**"seems like smoke is drawn under cars"**, then
  "1"). A car through the trail ahead is hazed by it. Over its own body only was the other way: it needs a level's slots
  laid car by car rather than part by part, which interleaves every car's tyres, body and lamps with the next's.
- **A puff leaves the pipe's end**, where `qq exhausts` put it on the bumper. Under the bodies it was born a little in
  from it, to grow out from beneath the bumper; over them, born clear, its root needs no hiding.

## 2026-10-24 — a flat roof's flue pipes and open stacks are chimneys too

**The owner: "Review chimneys on buildings - we have a lot of unmapped assets. Scan all buildings and map chimneys"**,
and, asked where the line is, chose stacks and capped pipes (`OBJ-7`).

- **A chimney is anything on a roof that a flue comes out of**: a brick or rendered stack with its pots, an open-topped
  box or pipe, and a round pipe under a rain cap, the flat roofs' mushroom cowls among them. 75 more roofs smoke —
  the panel blocks' and towers' rows of flues, the schools', hospitals', shops' and offices' cowls, the town hall's two
  stacks, the fire station's flue — 198 of the 376 in all; `tower_08`'s five open boxes are twin stacks.
- **Still left as drawn**: louvred and grilled boxes, pyramid-capped vents, condenser and fan units, spherical turbine
  vents, skylights, and the round grates and drains at a roof's corners. Each roof has one pot width (`potWidthM`), so
  a roof with both broad cowls and narrow pots (`tower_08`) takes one between them.

## 2026-10-23 — a twin stack smokes from both its pots

**The owner: "There are some twin pipes on assets. Use double thinner smoke there"** (`OBJ-7`).

- **A chimney in a roof's file is its stack's pots**, `X, Y` a pot (`chimneys`), so a single `[x, y]` reads as it did. The
  stack is drawn once for whether it smokes, and every pot of one that does is a row of its own, a pot wide — two thin
  plumes off a twin where there was one wide one. A width per roof and a spacing per stack was the other shape, and could
  not say a roof whose stacks differ, which most apartment roofs are.
- **Nearly every stack the art draws is a twin**, pots wall to wall across the picture; of 215 stacks, 28 carry one pot
  and five three, read off `qq chimneys pots` close-ups and laid with `split`. A house's pot is 0.35 m
  (`ChimneyFigures.PotWidthM`), and 87 roofs whose art is drawn larger say their own, 0.4–1.6 m.
- **The pots were not found by a finder**: one looking for the dark inside a stack's pale cap was tried and given up — a
  cap breaks into pieces under any one threshold, and a pot's glint splits it. It is the same reason the stacks
  themselves are read by eye.

## 2026-10-23 — smoke varies with the throttle and the pot

**The owner: "Lets do smoke variations. More dense on large chimneys, trains. Larger, smaller, longer, shorter.
Depending on throttle. Minimal on cars, max on rushing diesel train"** (`CAR-54`, `TRN-15`, `OBJ-7`).

- **An exhaust's every figure is a pair, idling and at full throttle** (`ExhaustLook`): opacity, the width a puff leaves
  at, how long it lives and how hard it is blown out. A track point keeps whether the body ran and its throttle share
  apart rather than one opacity, so the emit reads all four off the moment a puff left. A car idling is the least
  smoke drawn; a diesel at full throttle the most — over twice as wide as it leaves, 3.5 s of life, 0.9 opaque.
- **A stack's track is longer than a car's** (`StackTrackPoints`, `StackTrackEveryS`): 16 points four a second, so a puff
  lives 3.5 s where a car's lives under one; a car's track was not lengthened, since there are thousands of them and a
  dozen rail vehicles. A puff's life is still bounded by its track — laying it from where a standing stack is, as a
  chimney's is, was the other way, and would have left a moving diesel's trail short.
- **A stack is thickest early** (`StackThickestAt`), a car's halfway: a life warped so that share is its middle keeps the
  car's parabola exactly and lets soot roll dark off the stack.
- **A pot's width is read off its picture** (`potWidthM`, `qq chimneys set … --pot`), authored as the geometry it is; a
  roof that says none has a house's (`ChimneyFigures.PotWidthM`). What a wider pot puts out is derived, not tuned: a puff
  leaves as wide as the pot; it is as opaque as `k` house puffs laid over each other, the depth of smoke a look down
  crosses; and it lives `√k` times as long.
- **`qq drive --live SECONDS`**: the frames `qq drive` took are filled on the host, which draws no smoke, so a live drive
  in a window — closed after the seconds, since one follows its script for more — is how the smoke is looked at; and a
  live `shot` now honours `look`, which it ignored.

## 2026-10-23 — every picture read for a chimney, and a diesel's stack smokes

**The owner: "Check all assets for chimneys. And trains"** (`OBJ-7`, `TRN-15`).

- **Every roof and every rail vehicle was looked at**, not only those whose description names a chimney: `qq chimneys`
  takes all 387 pictures, `--unmarked --grid 6` lays the 276 unread small on triage sheets, and each candidate was read
  ruled and ringed. 16 more roofs smoke — the common houses' and the corner shop's stacks, eleven industrial roofs' brick
  stacks, a garage block's three stove flues — and the two diesels' exhaust stacks. Left as drawn: louvred extractor
  boxes, condenser fan units with their hubs showing, the domes, and the electrics' pantographs.
- **A rail vehicle's track is laid as a car's** (`smoke_track.comp`, one dispatch over the cars and then the vehicles,
  past every car's track): it runs while it is in the world (TRN-7), and its throttle is its rail command's. Its
  puffs go in whichever run the vehicle is drawn in, over it.
- **A stack is not a tailpipe**: it blows straight up, so a puff leaves it with the vehicle's velocity and no outflow,
  wider, thicker (`StackIdleOpacity`, `StackFullOpacity`) and dark (`StackShade`). Drawn with a car's figures it was there and could not be seen: pale grey on a pale grey roof, and a
  plume a puff's life long is under a metre off a standing stack.

**The owner let the host go** ("WE dont care about cpu matching gpu. That can be gpu only logic and we can focus working
only there and fix its issues. Lets add more intermediate draws of smoke. ( dont store, just inbetween ). And add smoke
to building chimneys"), `CAR-54`, `OBJ-7`.

- **A puff is split by halving** (`SmokeFigures.PuffSplits`, `PuffGapM`): the puffs are laid on a grid four times as
  fine as an idling car's, and a puff is drawn where its car's speed off the air would leave the next one coarser grid's
  puff further off than the gap — so a car speeding up draws more between those it drew and none of them goes, and each
  is the share of a whole puff's thickness its time is. Nothing is kept for one; each is laid along the track as before.
  From about 45 km/h every one is drawn, a fifth of a metre apart and further the faster the car. Stretching a puff
  along its velocity was the other way to close the beads, and was not asked for.
- **The smoke is aged off the moment of the view the host shows** (`SmokeHead.shownS`, the live end's time before the
  step that laid the newest): the newest puff had left a step's travel behind the drawn pipe.
- **A chimney's pots were read off its picture by eye** (`qq chimneys`): a stack is a pale box with dark pots in it, and
  a roof's tiles, seams and hatches are as much that to any measure tried — dark compact blots and pale boxes holding
  them alike found hundreds a roof — so each of the 111 roofs whose description names a chimney was read off a sheet
  ruled in metres and its pots ringed on a second to check.
- **A chimney's puff is where the wind has carried it since** and the eddy it left on, given up as a car's puff gives
  up its speed. It is drawn last of the over run, over the roofs and the cars, and its room is a figure (`MostPuffsDrawn`) rather than
  every chimney's whole plume — a frame that would draw more cuts its own furthest chimneys and nothing else.
- **What it costs, measured** (`qq prof --gpu --no-cpu`, the default framing, RX 9070 XT): OdesaOsm 16.6 % busy at 60 fps
  with its 35 648 smoking chimneys, against the 16–19 % taken before any smoke (2026-10-23, below); Odesa 5.9 %. A
  chimney out of view costs its thread a cull.
- **`--bench deviceworld` stays**, holding everything but the smoke to the host's fill; the smoke is counted apart.

## 2026-10-23 — a car's smoke is laid along the last second of its way, on the device alone, and the wind carries it

**The owner asked for the exhausts marked and smoked** ("Find and mark exhaust pipes. and render some simple smoke
animation on them. with 'inertion' so moving objects leave a trail"), `CAR-54`.

- **The art draws no tailpipe**, seen from above, so a pipe is placed and not found: `qq exhausts` puts one on the
  rear silhouette at three tenths of the width out on the left — the side away from the kerb in a town that drives on
  the right — two for the sporting and the luxury looks, none for the four electric ones.
- **A puff is where the car was when it left** — between two points of the car's way, the last second of it kept at
  eight a second (`DeviceSmoke`, `smoke_track.comp`) — and the run it has made since off the velocity it left with,
  which it gives up to the air. That is the inertia, and a trail round a corner is the corner. **Two cuts were turned
  down on the way.** The first kept nothing and ran the car's present motion back along its yaw rate: "that limitation
  is not acceptable. they must follow real path. try to keep it optimal - keep a small track behind". The second laid a
  track wherever the moving rows were — the device's lay pass, the host's fill, and a tick at a time by a run that
  ticks without drawing — to keep the host's fill and the device's emit one to the bit: "You can do that on gpu only.
  Since its doing physics ticks. We need to store const last N points. ( lets for last second, 8 points ) And render
  between them."
- **So the track is the step's**: a pass behind every step of the device's world (`DeviceWorld.Begin`), from the moment
  anything draws off that world (`DeviceWorld.LayTheSmoke`), each point stamped with the step's own clock — and the
  smoke is drawn by the device's emit and nothing else. **A world stepped in managed code draws none**: the page's, a
  `--shot`'s, a scripted drive's and the suite's; a live drive (`--drive --live`) is where a frame of it is taken.
  `--bench deviceworld` holds everything else the emit draws to the host's fill, and counts the puffs apart.
- **A puff is clear as it leaves the pipe and thickest halfway**: faded only towards the end, the young small puffs
  behind a car at speed strung out as beads.
- **The wind is the town's** ("lets add wind to the map. As a small force vector that is moving light air objects a
  bit. And slowly chaning its direction over time"): a function of the clock off the plan's seed (`Wind`), a breeze of
  1.5 m/s that swings 60° either side of a seeded bearing over five minutes and gusts. A puff closes on the air's
  velocity rather than on standing still, so it goes with the wind once it has given up what it left with. It reaches
  the emit as a word of each frame; nothing that stands reads it.

## 2026-10-24 — a fan is drawn in one of ten looks, fitted to the rim its body's picture draws

**The owner asked for every fan drawn in a look of its own** ("Lets try using predefined 10 vents for all of them. Size
them to fit. Dont cut them. Generate 10 good images" — the rotating coolers, `OBJ-6`, `TRN-13`). The fans the art draws
are small, blurred and drawn differently on every roof, and turned out of the picture they stay so.

- **Ten looks, each asked as a frame of its own** (`assets/world/building/fans/`, `fan_*`): one round fan seen from
  above, whole, taken off its frame by its drawn extent and never fitted or clipped (`qq sprites cut`). The art's fans
  were the style picture. $0.12, one attempt each; four drew a blade count other than the one asked
  (raw_assets/world/building/prompting.md).
- **Still a fan's geometry and nothing else, and its ID**: a body's look is a hash of its ID beside its share of a turn
  (`fans.glsl`'s `fansShare`), so every fan of one roof wears one look, as a unit's twin fans are one make. The looks'
  places are the fan table's last rows (`FanTable.PutLooks`), written when the atlas is packed, since the fragment stage
  reads that block and not the sheets'. No instance and no field a fan.
- **Sized to fit: the look's middle on the hub and its edge on the rim**, each way out of the hub the share of the way
  to the rim the share of the way to the look's edge, the rim taken a texel and a half wide of the ellipse fitted
  through it. So the look covers the drawn fan, squashed or off its hub, and turns round; past the rim the roof shows.
  A sliver of the art's opening still shows where it wanders wider than that ([tech-debt](../../../../docs/tech-debt.md)).
- **Laid over the body's own texel by the look's alpha**, so a look drawn with a soft edge blends into the roof.

## 2026-10-23 — a fan is its geometry, read off its body's ID, a roof's and a train's alike

**The owner asked for the roofs' fans turned** ("good do it", offered after a survey of what else the art draws that
turns): the round condenser fans on the office, hospital and shop roofs (`OBJ-6`). Tried on three roofs first —
`office_01`, `office_deep_01`, `hospital_04`, nine fans — before the catalogue.

**The first cut was the trains'** — a disc sheet a fan and an instance a fan a building, turned in the vertex stage —
and **the owner turned it down**: "lets do this without new textures and per item props / it should store only its
geometry and cut it in runtime / no per item fields - their animation should be based on its ID + frameID / so its
ticking and they have some random offset". It cost a sheet slot a fan and, laid for the busiest roof on every
building, some 13 MB of instances on OdesaOsm.

- **A fan is its geometry and nothing else** (`FanTable`): a run a sheet slot, then each fan's circle and its rim's
  ellipse in its sheet's units, one uniform block both heads read, and the body's own fragment stage draws it
  (`fans.glsl`, `town.wgsl`), so a fan is under nothing its body is not.
- **The trains' fans went the same way** — the owner, offered it as a simplification: "yes do it consistant - why is it
  even separate code path". Gone with it: a disc sheet a fan, an instance a fan a wagon and the room laid for them, and
  the device emit's fan pass. The disc's outer band drawn as each ring's mean colour went too, since a ring's mean is a
  loop a fragment; the shots show no facets on the rim without it.
- **Each fan from its own start** — the owner's, and the one place an animation here is staggered (the sirens and the
  indicators run in step, the car log's 2026-10-23): a body's share of a turn is a hash of its ID (`SpriteInstance.Id`
  — a building's or a wagon's index, the sheet word's spare half, so an instance stays 28 bytes), the same every frame
  on both heads, and each further fan of it a golden section on. Not its place in a run: a wagon's moves with whatever
  is drawn before it. How far every fan has turned is the tick's (`FanTable.TurnsAt`), so a fan ticks with the town and
  stands with it paused.
- **A body whose widest fan is under `FanLeastPx` across cuts none that frame** (`CameraView.Turning`): the vertex
  stage hands the fragment stage no run, so a whole town's framing pays nothing for its fans. The bar was first a car
  part's, a pixel and a half; with the catalogue's fans cut, the owner asked for the animation dropped past some zoom,
  and it is now a figure of its own at four pixels — a blade's width, the default framing still turning every fan,
  and a town's framing none (`EngineFigures.cs`, `ViewFigures`). Measured the same day
  (`qq prof --gpu --no-cpu`), animation does not show in a frame at all: Odesa 0.23 ms of CPU and the GPU 4–5 % busy at
  120 fps at a street's framing and the whole town's alike; OdesaOsm held to 60 fps by its 4.5 ms tick, the GPU 16–19 %
  busy with the town's tick on it. So the lamps stay instances (folding them into the body was weighed: 5× less room
  laid, every car's quad grown by its glow) until a figure says otherwise.
- **The fan's figures moved down to `core/geometry`** (`DrawnFan`), since a train and a roof both read one.
- **The catalogue's fans were cut by a finder and a look** (the owner: "continue adding animation props to assets"):
  276 fans on 74 roofs more, the hospitals, offices, shops, schools, kiosks and a few towers. `qq props seed` finds them
  (`_props_trace.fan_hubs`); each roof was looked at and what is no fan taken out. Left standing: the corner drains and
  flues it took for fans; the apartments' vent pipes and chimney pots; the garages' and canopies' boxes and bolts; and
  the fans [tech-debt](../../../../docs/tech-debt.md) names.

## 2026-10-20 — the atlas is five pages, the roofs at their art grid

**The owner took a fifth 64 MB page over drawing the roofs coarser.** Every building roof was drawn again at its
grown footprint (CityGen's log of the day) and installed at the art grid where its frame held it: the prefabs are
55 Mpx, three and a third pages of texels, and the shipped art packs into five (`SheetAtlasTests`, 320 MB). A
building grid of 24 px/m, three pages, was offered and refused.

## 2026-10-12 — a frame writes only what changed: marks, scenery and the ground drawn off rows laid once

The engine review's draw items. Whole-town framing, RX 9070 XT,
`qq prof --frame --view 40000` (the exit's new `written` line): Odesa 1 916 → 93 kB a frame written into memory the
device reads, ticked on the device 1 796 → 33 kB with its sprites 0.13 → 0.01 ms; OdesaOsm 9 896 → 1 781 kB.

- **An instance is 28 bytes** (`SpriteInstance`): the half-size two halves, the cell of its sheet four bytes, the tint
  four normalised bytes. The emit rounds the halves in integer arithmetic as `Half` does (`spritesHalf`), so the device's
  instances stay the host's to the bit (`--bench deviceworld`). A cell and not a pair of texture coordinates, because
  every sheet the town draws a part of is cut on a grid.
- **The marks are a ring of 24-byte rows drawn where they lie** (`mark.vert`): on the device the tyre pass lays them into
  a ring there (`drift_marks.glsl`) and the view's pass moves its head, so nothing crosses to the host and back; on the
  host each swapchain image's copy of the ring takes only the marks laid since it last drew. Drawn oldest first, the
  laid order, rubber and soil as they fell.
- **The scenery's pieces are on the device, laid once** (`scenery.vert`): a frame writes the runs of each row in view
  and the thinning (`ScenerySprites.Spans`), the same pieces in the same order the host's fill took, so a pan neither
  thins nor shimmers.
- **A prop smaller than `PropLeastPx` across is put out of the picture by the vertex stage** (`CameraView.PropCull`),
  standing or scenery: a whole-town framing had drawn hundreds of thousands of them a pixel or less, speckling the grass.
  A building is drawn at any size.
- **The ground is drawn a band of rows at a time** (`GroundBands`): each part's triangles filed by the band they stand
  in, in classes a doubling of height, a draw a class, the parts in TER-7b's order and recorded once; a frame writes
  each draw's first and count. Every e2e frame but the props' is unchanged to the pixel.
- **What moves is emitted inside the frame that draws it** (`IMovingEmit`, `DeviceSprites.Record`): recorded once a
  swapchain image ahead of the pass, steered by that image's frame words — the view, the clock, the view slot the host
  shows, and the lay's workgroups, nought where no batch of signals came between — at no crossing and no submit of its
  own. The rows are laid off the view the host is showing, so a body is drawn where its selection and paths are.
- **What a frame writes for the device to read is in the device's own memory where it is offered mapped**
  (`HostAccess.Streams`), and the sheets' table is written there once.

## 2026-10-09 — a frame draws what moves where the device emits it

**Where the world is the device's, the moving runs are the emit's** (`DeviceSprites`) and are drawn
where it wrote them: `TownRenderer.DrawTheMovingFrom` records each image's over and above draws against the emit's
instances and its two indirect draws, the host fills only the stretch standing (`TownSprites.FillStill`), and the
renderer is laid no room of its own for what moves. Filled on the host, a frame of
OdesaOsm at 40 000 cars and 40 000 people needed every car handed back after its last tick (1.7 ms) and the solver's
moving grid laid for the picture alone (`PhysicsWorld.IndexMoving`, 3.1 ms).

- **What a car's lamps say is the car's own** (`CarLamps.Showing`): the town signals it where it changes
  (`ControlAction.Show`) and the emit reads the lamp set off the world's row of the car (`sprites_world.comp`), so no
  line, pass or junction ahead crosses to be drawn. Packed as arcs, the lines had been the emit's room — about 0.74 GB on
  the device and as much mapped at 40 000 cars.
- **Every row is laid on the device off the world's own state** (root `decision-log.md`, 2026-10-11): where each body
  is and points, its wheel, what it shows, its tow and its level; a look is laid once. Nothing of
  an agent is sent for a frame.

In the window (OdesaOsm, 40 000 of each, RX 9070 XT, `qq prof --device-tick --frame`, the whole town then ticked on the
device): 40 → 59 fps, the display's own rate; the
frame's CPU 25.1 → 15.9 ms, its sprites 2.63 → 0.99 ms; 1.5 → 1.0 ticks a frame.

## 2026-10-07 — the moving sprites are emitted on the device, beside the fill

**The world is the device's** (root log, 2026-10-11), so where the cars and the walkers stand is only there, and what a
frame draws of them has to be written there. `TownSprites.FillMoving` is the host's half — the walkers,
every car part by part, the heads, and the cars on the level above, as `Fill` lays them past the marks and the scenery —
and `kernels/sprites_count.comp` and `sprites_write.comp` over `sprites.glsl` are the device's, run by
`app/devicetick/DeviceSprites`. A car's lamps are `agents/car/body/kernels/lamps.glsl` and a walker's cell
`agents/person/body/kernels/look.glsl`, each its own slice's.

- **Count, scan, write.** A count a part of a unit, laid in the order the instances are drawn — the walkers', each car
  part's a run apiece, the heads', the car parts again for the level above — then a prefix scan, then a pass that lays
  each part where it starts and writes the two draws' counts. Fixed slots a car, compacted, was weighed and not done:
  it leaves the parts interleaved car by car where the host lays them part by part, and the compaction is a scan anyway.
- **Every unit is asked, and none through a grid.** The host asks the solver's grids who may be near (`CarsIn`,
  `PeopleIn`) and culls each part against its own reach; the device asks every unit the per-part question, a thread a
  unit. What the grid answers holds everything the culls keep, so the instances are the same.
- **The view, the clock and the live rosters are a block in a buffer, not push constants**: the frame that will carry
  the emit is recorded once (`CameraView`).
- **What only a type held is data now**: the heads in the order their rows file them (`RowFile.Filed`), a head's cell
  (`SignalSprites.CellOf`), a look's build with no car wearing it (`CarFleet.BuildOfVariant`); and `FillCars` takes
  the fleets rather than the town, so a staged fleet is drawn by the code a town is.

Held to the host's fill as it was laid: the fixture town running, five framings a second for twenty seconds; 2 000
staged walkers of every look standing, walking, down and inside; 2 000 staged cars of every look with every lamp input,
wrecked, towing by either end, under a hand, on three levels, at three scales. Every field is the host's to the bit but
a tow arm's bearing, the device's arctangent; a walker's facing and stride and the arm's direction are rounded.glsl's
quotients and root. On OdesaOsm (RX 9070 XT; 10 013 cars, 10 000 walkers, 2 714 heads) the
emit took 25–29 µs at every framing, 14 of them the scan, where the fill took 0.7 µs at a street and 137 µs over the
whole town; no instance differed. **A head's colour is `signal.glsl`'s, whose quotients are the device's own**: on RADV
a head at a cycle's edge could turn a frame early, and none did there.

## 2026-10-06 — a frame asks the grid what it can see, and the buildings are laid once

The owner asked that a frame do as little as it can, and pointed at the grid: the town already knows what stands in
each cell. Measured on `O10` (12 107 cars, 12 240 walkers) by `--bench sprites`, the fill was 231 µs at a street —
to draw about a hundred and fifty instances — and 2.5 ms over the whole town, every frame, because every fill walked
its whole roster and the buildings were copied, 693 265 of them, into each frame's buffer. Now:

- **Who may be in the frame is asked of the solver's own grids** (`TownWorld.CarsIn`, `PeopleIn`), sorted, so the
  order drawn is the roster's as before. A box wider than the roster is cells is answered as the roster.
- **The marks and the signal heads are filed by row** (`DriftMarks`, `RowFile`): a row and not a cell, two words a
  row however wide the town. A view over half the rows reads the ring straight through.
- **The buildings and props are on the device once**, and a frame writes which stretch of them — whole rows, one
  indirect draw with a first instance (`drawIndirectFirstInstance` is asked for now; a page without
  `indirect-first-instance` draws from the first of them). The sprites are three runs: under, the laid stretch, over.
- **A lamp's glow under `CarPartLeastPx` is left out**, as a steered tyre is, and a pass whose widest is under it is
  not walked.

| `O10`, µs a fill | street, 70 m | district, 300 m | quarter, 1.5 km | whole town |
|---|---|---|---|---|
| before | 232 | 231 | 520 | 2 488 |
| after | 2.6 | 21 | 128 | 299 |

A frame in flight writes 21 MB of instances where it wrote 58; the 37 MB of buildings is one copy on the device.
Nothing allocates. **Fusing a car's four passes into one was weighed and not done**: over a frame's own short list
the four cost half a microsecond together.

## 2026-10-25 — every tyre is a quad under its body, because the body tilts and the tyres do not

The rear pair was a black box painted under the bodywork when the atlas page was filled, so it went wherever the
body's quad went. A body drawn tilted on its springs (CAR-56) would have carried its rear tyres with it while the front
pair stood still — the first try, which slid the body off its wheels, read as wheels knocked off their line for
exactly that reason. **Now every wheel is a quad, plain rubber off a one-texel sheet**
([CarSheets](../CarSheets.cs)), at the offset its impulse acts on, the steered ones at the rack's angle, and **none at
all once a tyre is under `CarPartLeastPx`**, which is every framing that holds many cars. A four-wheeled car is five
instances close up, where it was three, and still one from a district out; the room is laid for the catalogue's most
wheels on one look (`TownSprites.MostWheelsOnALook`), and the device's emit reads each look's wheels off its row. The sheet is the art as cut, no longer padded, so the
quad is the footprint.

**No tyre is baked into the shipped files**, because the track and the tyre are the variant's own figures and the
silhouette CAR-12 is measured off is the bodywork alone: a tyre baked into the picture is a second copy of `trackM`
that a moved track leaves behind.

**The tilt rides in the instance's ID**, which only a building and a wagon used (OBJ-6): nose down and right side
down a signed byte each, told apart by the sheet being a car body's (`CameraView.CarTilt`). No instance grows, and
the vertex stage draws the tilted plane with its corners' w set, so the picture across it is the plane's and not two
triangles bent at their seam.

## 2026-10-06 — no tyre rolls

A tyre was its own quad of a scrolling tread: four instances a car every frame, a fifth texture binding and a
branch in every sprite fragment for the one sheet that tiled, and a phase per wheel rolled every tick — for a
detail a few pixels across at any framing past a street. **No tyre rolls now**; a front tyre is drawn at the rack's
angle rather than its own Ackermann one. 12 baked steering angles was weighed and refused — the variants' tyres
differ, a quad's turn is already free in the vertex stage, and twelve sheets a look does not fit the sheet table.

## 2026-10-06 — scenery is twelve bytes, and a frame draws a budget of it

**A traced city's scenery is 1 774 263 props on `OdesaOsm`** (`CityPlan.Scenery`), drawn and standing nothing. Kept
as the standing instances are, it would be 99 MB of instances and a frame buffer as large again for every frame in
flight, since the buffer is laid for everything that can be drawn at once (`TownSprites.CapacityFor`). So
`ScenerySprites` keeps it apart:

- **Twelve bytes a piece** — its place, its look and its girth as a half — made into an instance only when it is in
  view: 21 MB.
- **Filed by row and found by column**: the index is where each row of the town's main cells starts, 12 KB, and a
  row's cells in view are a binary search away, where a cell's offsets were 15.5 MB.
- **At most `SceneryMostDrawn` a frame, 65 536**, and past it every n-th piece by its own index, n a power of two —
  an even thinning that only moves as the zoom crosses a doubling, so a pan does not shimmer. Open ground holds that
  many in a view a couple of kilometres wide, where a tree is a pixel or two. The buffer grows by the budget, 3.7 MB,
  whatever the size of the town.
- **Under everything that stands**, after the marks: nothing comes near it but its own neighbours.

Laying the index takes 65 ms. A frame's fill is 0.03 ms at a street, 0.04 ms over a district (21 077 drawn) and
0.09 ms over the city, thinned to 34 591. Both heads draw it, the page through the same fill.

## 2026-10-05 — a corner of the ground is twelve bytes

**A traced city's ground is four million corners, and most of each was not its place**: a texture coordinate that
is the place over a period, three floats of a shade that is one of a handful, and a word for one of six surfaces.
A corner is now its place and one word (`GroundVertex`): three channels of nine bits up to
`GroundVertex.BrightestTint`, a little over paint's, and the surface in the five above them. **The vertex stage
divides the place by its surface's period**, so the texture is anchored to the world origin exactly as before, and
the periods ride the camera — written by the renderer from the mesh it was laid with (`CameraView.SurfacePeriodsM`),
so nothing that builds a camera knows them. A shade is filed to within 0.003 of itself, and a weld is keyed on the
word.

On OdesaOsm the mesh is 133 → 50 MB on the device and as much again off it, and laying it holds 1 774 → 1 655 MiB at
most; the corners are the same 4 165 049. The visual tier passes whole and a shot of each head is the picture it was.

## 2026-10-18 — the sheet table holds 2048, and a device with less is refused

Nine civilian cars and their wrecks took the town past 512 sheets. **The table is 2048 places, 64 KB** (`SheetAtlas.Slots`):
WebGPU's default `maxUniformBufferBindingSize`, which every adapter offers, and what desktop and handset Vulkan drivers
report, so it stays a uniform and no head changes binding. Vulkan promises only 16 KB, so the Vulkan head reads
`maxUniformBufferRange` and refuses a device under the table rather than drawing past it; a storage buffer is the step
if 2048 is ever spent. What bounds the art past that is the pages' memory, not the count. The length is one constant,
and `SheetAtlasTests` holds both shaders' arrays to it.

## 2026-10-04 — a prefab with no picture is a plain block, and the sheet table holds 512

A traced town's buildings are 355 prefabs, most with no art yet ([citygen's log](../../../citygen/docs/decision-log.md)).
**An undrawn prefab is drawn from a sheet built for it** (`PrefabSprites`): its rounded rectangle in its look's colour
and clear past its corners, its walls darker and its door's wall lighter, at 4 px/m — so the placement can be judged,
look by look, before any of it is painted. The footprints under them went from tan to grey, so where a prefab parts
from the survey reads against it. **The sheet table went from 192 places to 512** (`SheetSlots`, both shaders): 498
sheets now, and 512 of 32 bytes is the 16 KB uniform range every Vulkan device must offer.

## 2026-10-04 — a bridge over a road is drawn above it, and so are the cars on it

The owner asked for bridges over roads drawn above them as roads of their own (`TER-7b`). **Two draws of the ground,
of its marks and of the bodies, in one recording**: the ground's parts to the paint, the ground's marks, the bodies on
the ground, the level above (`GroundPart.Above` — its decks, carriageway, kerb and paint), its marks, then the bodies
on it. Each pair is one buffer — the index buffer's last part, the underlay past `UnderlayCapacity` and the instances
past the ground's runs — bound at a second offset. The commands are recorded once per image, so a frame still makes
the five crossings it did.

- **A mark goes with the way it is about** (2026-10-05, the owner): a bridge's lanes, joins, ribbons and claims drawn
  in the ground's one run were under its own deck. A join at a bridgehead is on the ground, as its tarmac is.

- **Paint goes with its carriageway**: a run of lane dashes stops at a bridgehead (`CentrelineRuns`), and each mark is
  laid in its own level's stack.
- **The bridge's kerb is not struck across its ends**, where it is the ground's road carried on; the ground runs on
  under the deck there, so the deck's end meets tarmac.
- **A skid on a bridge and a walker are on the ground's run**: a mark keeps no level, and a walker is always on the
  ground (`PHY-1a`), so either on a deck is drawn under it.

## 2026-10-03 — the ground lives on the device, copied there once

`OdesaOsm` ran at 3 fps on an RX 9070 XT, each frame 326 ms waiting on the GPU with the CPU idle. The ground's
vertex and index buffers were mapped memory, and the first host-visible type on a discrete card is the host's
own RAM, so every frame fetched the whole ground across PCIe: harmless for `Odesa`'s 6 MB, and 420 MB for a
traced city's 7.57 million triangles. **The ground is now device-local, written by a staged copy**
(`GpuBuffer.Upload`) when the renderer is made and when a layer is switched (`ShowGround`). 3 → 120 fps there,
89 → 120 on `Odesa`, both at the display's own rate, and every pixel of a shot is the same. Asking for memory
both device-local and mapped was 119 fps as well, but only where the whole of the card is mapped (resizable
BAR); without it that window is 256 MB, smaller than the one city that needs it. What a frame writes stays
mapped — a few MB, and written every frame.

## 2026-09-19 — a frame is filled into the image it will be drawn into, and the image is taken first

Cars blinked as they moved. The instance buffer, the two quad buffers and the three counts were one of
each, written every frame over a swapchain of three images with no wait before the fill, so a draw one or
two frames back was still fetching slots the next fill was rewriting — a mark, a tyre or a lamp where a
car's body was. A standing town writes the same bytes twice, which is why only a moving car showed it. The
camera was per image all along, so this was a miss rather than a design.

**Every buffer a frame fills is now the image's own, and the image is taken and its fence waited on before
the fill** (`TakeImage`) — per image rather than per frame in flight, because the recording that binds them
is written once per image. **The frame is still five crossings**; what it costs is memory, about 22 MB of
overlay quads where it was 7.5 MB, and the sprites again per image. Holding an image across a frame is what
the rebuild on a resize had to learn (`DrainAcquire`).

## 2026-09-16 — a mark between two ribbons is struck off the line they were offset from

Where two ribbons of driven ground touch, the merged shell says nothing: the seam is interior to it.
Cutting the two bands against each other to paint what they share would be a second merge for a line the
town already knows, so the mark is struck off the line the pair was offset from — **down a street, the
road's own centreline; between two bays, one way's line moved half its own width**, with which pairs
touch measured rather than read off the order they were emitted in.

**Which roads carry two ribbons is asked once** (`RoadArrays.LinesBetweenLanes`, beside the lane offset it
shares its arithmetic with), or the picture and the lanes could disagree about it. **Where a run of paint stops,
and how far short of an arm's zebra and bar, is the road's** (`CentrelineRuns`, `Crossings`, `StopBars`);
this pass decides the pitch, the width and the shade.

## 2026-09-16 — a fill has no visible edge, so the line is cut for the picture and the fill for the kerb

A boundary was read once and every layer laid from that reading, at a tolerance argued from the zoom the
camera opens at. But **every shell filled here carries a kerb laid over it**, so where a fill cuts a corner
the layer beneath shows and the stone covers both: the one defect a thinning can cause is a fill's edge
reaching out from under its kerb. So the fill's whole budget is `HiddenShare` of a kerb's half-width —
75 mm, 25 mm inside the stone's own edge, asked of the mesh by
`NeitherFillsEdgeShowsFromUnderTheKerbAlongIt` — and **the line is cut for the picture**: the sag, and
`ChordTurnRad`, because the step a sag earns grows as the radius shrinks and the boundary's tightest turn,
0.14 m, came back at sixty degrees a chord inside a two-centimetre sag. **The two readings are nested
thinnings of one flattening**, so they part by the fill's own budget rather than by the sum of two strays,
which is what makes 75 mm a bound and not an estimate.

**Tried and not kept**: one reading for both, since the seam is under the stone either way; and a 120 mm
thinning of it, which saved a fifth of the ground and left 15° chords plainly faceted at 100 px/m. **A
stroke's corner became one cross-section on the bisector** wherever the sag allows — it pinches the ribbon
to `w·cos ½θ`, the figure a chord bows off its arc — and a fan only where it does not. A mitre was refused:
it stands `½w(sec ½θ − 1)` outside the line (TER-3d).

**Odesa, seed 1: 155 755 triangles to 155 133 at 8° a chord**, the fills from 37 937 corners to 34 737.
Above 8° the angle is nearly free (11° comes to 146 868) and the line's own two-centimetre thinning, a pixel
at 50 px/m, carries the rest. **A road cannot get much cheaper**: 75 mm is the 72 mm the ground was
already thinned at, and the last quarter of the half-width buys 9% for a fill edge standing at the kerb's
own, which is no margin.

## 2026-09-16 — the mesh is laid in parts, and a part is a run of the index buffer

`GroundMesh.Build` writes down where each layer started, what it came to and what it cost (`GroundPart`,
`GroundTally`). **The parts tile the mesh**, so a layer is left out of the picture by packing the wanted
runs to the front of the index buffer and shortening the draw: no recording, no pipeline, no ground laid
again. Hiding a layer by not laying it, as `RoadFigures.CarriagewayDrawn` did, costs a relay and leaves no
figure to read; cutting everything and drawing some of it keeps the tallies whole.

**Odesa, seed 1 — 155 755 triangles laid in 9.6 s.** The two kerbs are three quarters of the triangles
(59 006 and 58 124) and a sixth of the time; the two fills are the reverse (19 335 and 19 046, in 54.6 and
78.1 ms). A stroke is quick, dense and welds almost nothing; a fill is slow and `n − 2` a ring however it
is cut. **None of that is the load**: every layer together is 165 ms, and 9.3 s of the rest is the merge
behind the boundary (`LaneShell`), which is not this slice's to fix.

## 2026-09-16 — a layer is the offset filled whole, and the road is what covers it

The walk was the offset with the driven ground cut back out of it (`ArcSubtract`), and the carriageway was
then laid over it anyway: **the cut removed exactly what the next layer covered**, and carried the boundary
a second time, thinned on its own terms and free to leave grass between it and the carriageway's own copy.
TER-7b says no layer is cut to leave room for the next, so the walk is the offset filled whole.

**Odesa's ground fell from 174 634 triangles to 155 755**, the walk from 37 925 to 19 046, and opening it
by about 34 ms. Over three towns the cut layers and the whole ones came to 869 831 m² against 869 842 m²,
so the cut had been taking the carriageway and nothing else. What it adds back is overdraw, which with no
depth buffer and the grass under everything is the carriageway's six per cent of the land through a
one-sample shader. `ArcSubtract` stays, with its tests, as tooling.

## 2026-09-16 — the line a stroke is struck from runs down the middle of it

**A kerb straddles its shell** (TER-3d), half in the road and half in the concrete, where it had stood
wholly outside. It costs the ruler reading — kerb to centreline measures the lane less half a stone — and
buys what a one-sided stroke could not state: **no part of a stroke stands further from its line than half
its width**, so a kerb bulging off its own line is a defect rather than the shape.

The one-sided stroke had two corner faults. **A corner was crossed by a chord**, a notch of
`½w(1 − cos ½θ)` outside every turn and the whole half-width at a turn right round; it is swept now
(`GroundMesh.Turned`). **And the width was held through hooks** where a movement's ribbon folds, so an edge
came back on the far side of its own line; it stops at the middle of the turn (`GroundMesh.Reach`).
Odesa's tightest hook, 0.14 m, folded a kerb reaching 0.20 m and does not fold one reaching 0.10 m, so
centring cured the shipped case and the clamp guards tighter ones (`--bench outset` counts them). The
half-metre floor under `StepM` fired only where it was worst and went; `Ribbon` went into `Stroke`, a
deck being a stroke about a road's line.

**It cost 5.6% more ground triangles on Odesa, 165 353 to 174 634**, mostly on the straights, where a
cross-section with both ends off the line lays two real triangles in place of a degenerate one.

## 2026-09-15 — two fills and two kerbs, and a kerb is a stroke with a mesh of its own

The ground beside a road was four bands, each two offsets subtracted, so the curve two neighbours share
was carried twice, thinned differently, and grass showed between them. It is now **two fills and two
strokes**: the carriageway is the boundary filled, the walk is that boundary moved by one figure
(`GroundRings`) and covered by the carriageway, and **each kerb is a stroke along its own line at
`KerbWidthM`, taking no point from any fill** — two hundred millimetres wherever the line runs, which a
difference of two thinned fills cannot promise.

**This is a change to TER-7b and the owner made it**: a kerb had been what a layer leaves of the one under
it, which is now said of a rim and an edge line alone, and a kerb is a stroke along a shell of its own,
still struck from the boundary and never from the line before it. It costs a third of what it did — one
move of the outline against three moves and three cuts — and no new surface or art.
`RoadFigures.CarriagewayDrawn`, which hid the road's surface while the boundary was being looked at, was
deleted rather than turned on.

## 2026-09-15 — the ground is thinned, and the only lever on the count was the corners

**Every triangulation of the same corners is the same count** — `n − 2` a ring by any partition — and added
points only climb (`T = N + 2I + 2H − 2`), so the whole trade is how many corners the boundary is read as
against the accuracy given up. Nine ways were built and measured on Odesa (seed 1, 414 249 m² of driven
ground over 110.6 km of boundary), and eight were deleted with the menu page and the `--ui cut=WORD` that
chose between them. **Douglas–Peucker over the drawn sag won**: at 8 cm, 14 842 triangles against the
sag alone's 28 895, the best shape of the nine, and three times as quick to lay.

**Compared at equal loss, which is the only reading that means anything**, it beat a coarser sag (15 169
triangles, 7.8% slivers against 5.1%), because a sag is spent evenly and a thinning where the boundary
bends — and it improved the shape too, the corners it drops being the near-collinear ones that made the
slivers. **Visvalingam–Whyatt was rejected**: about 3% fewer corners for 43% more time. **The cap on the
longest edge was dropped** — 10% of the triangles, bought for a symptom the flips had since fixed — because
a ground vertex's texture coordinate is affine in its position: triangle shape buys nothing in this
renderer, so the count is the metric and shape the tiebreak.

## 2026-09-15 — the driven ground is drawn as the one shape it is

`ShellFill` turns the boundary's rings into triangles, so **the driven ground is one region, cut once,
with every block the streets enclose left as the hole it is** — not a ribbon per road at several sizes: a
road, a movement and a junction's wedge are one shape on the ground and one shape here. **It is cut at the
tolerance a picture is worth** (`ChordSagM`, 2 cm) and not at the fill's own tenth of a millimetre, which on
a city is a hundred times the triangles for no difference a frame shows. What it costs at load is the
merge, not the cutting.

## 2026-09-12 — a layer is a region and not a heap of pieces

The ground was a union stated by over-painting: every road, movement, wedge and car park laid at several
sizes until their outlines agreed. It cost nothing per junction, and it could never **hand anybody the
boundary** — a rim was what overdraw left, with no length, no arcs and no side, so nothing could ask
whether a pavement was a walk wide. **Each layer is now the ground within one distance of the kerb, filled
as the shape it is. This is a change to TER-7b and the owner made it.** It gives up that no piece knew what
was beside it, and buys a line with a figure on it at every edge in the picture, the answer being the same
figure compared the other way.

## 2026-08-30 — the art was never pixel art, and was stored as though it were

`ArtPixelsPerMetre` claimed 21 px/m blown up ×3; tested, the block structure is absent and the sheets hold
96 000–263 000 distinct colours each, which cost 26 MB of art in a format chosen for flat colour. The grid
moved to 31.5 px/m — not 21, which would go soft long before the zoom runs out — and storage to WebP:
2.7 MB, and one atlas page from three. The fleet did not move: `CAR-12`'s 9 mm tyre overhang is under half
a texel at 48 px/m. Halving everything was tried first, and the unit tier named exactly what could not be
resampled.

## 2026-08-30 — a sheet is a rectangle of a page, not a descriptor

Descriptor indexing is a Vulkan extension neither WebGPU nor WebGL2 has, so a browser would have needed a
second answer to "which picture"; and on the desktop a wave spanning quads from different sheets is a
divergent index the driver scalarises. [SheetAtlas](../SheetAtlas.cs) packs the sheets into one array
texture, where a layer is a coordinate. The five ground surfaces stay out, being different sizes and
wrap-seamless.

