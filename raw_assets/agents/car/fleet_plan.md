# Civilian fleet plan

Real body classes, generic names. Figures are the class's typical ones: length and width without mirrors, overhangs
from the bumper to the wheel centre. `drive` F front, R rear, A all. The catalogue is a street in an Eastern European
city today: about two fifths Soviet-era and 1990s–2000s used imports, half 2010s, a tenth new and electric, with vans
and taxis among them, in the condition a street holds them — new, good, average, worn.

**An id names a class, an era and a colour, never a make or model.** Batches 1–2 were installed before this rule and
keep their ids until the owner says otherwise.

**Overhangs are the class's own, and nothing corrects them later.** `qq sprites use` measures the footprint, the
collision box and the track off the picture; the axles stay where the plan put them. The wheelbase they leave (length
less both overhangs) is checked against the class's before a batch is scaffolded.

**`describe` says only what shows from straight above** (roof, bonnet, glass, lamps, paint, roof rails, arch trim);
side mouldings, door rails and side windows are not seen and tempt the model to tilt the view. **Wear is in the paint
only**: no dents, scratches or bent panels, which `NEW` forbids (the outline is whole).

Condition words in `describe`: **new** showroom gloss; **good** clean paint; **average** slightly dull paint, a few
small scuffs; **worn** faded sun-bleached paint, rust spots on the sills and wheel arches, one panel a different shade.

Lamps: one unit at each corner, never a light bar across the whole tail or nose (a lens over 0.42 m is past the lamp
sheet's cell, `LampAtlas.CellPx`).

What the handling columns feed (`CarBuild.Resolve`): `accel` is the engine's reach past the car's own driven tyres
(CAR-45, held to 0.5–1.5); `speed` is a multiple of `Car.MaxSpeedMps`, which no town street reaches, so what it moves is
the car's sight (`SightM`, with its square); `brake` changes only how fast the pedal ramps, since every pedal is past
every tyre (docs/tech-debt.md).

Batches by length, nine to a sheet (3×3); batch 1 the ten smallest (4×3). A 3×3 cell holds about 460 px of car
(`fleet100_b02`, 113 px/m at 4.07 m), so batch 10 lands near 93 px/m and batch 11's 5.91 m van near 78, against the
art grid's 96: their cuts are read against `qq art` before they are kept.

## batch 1

| id | describe | lengthM | widthM | cornerM | massKg | frontOverhangM | rearOverhangM | drive | steerDeg | friction | cgM | frontShare | speed | accel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| microcar_white | modern two-seat city microcar, good condition, white with a contrasting black safety-cell frame round the cabin, a very short stubby nose, a big curved windscreen, one rear window, almost no boot, its flanks straight and full width from nose to tail, round-cornered headlamps, small tail lamps | 2.70 | 1.66 | 0.45 | 900 | 0.47 | 0.36 | R | 36 | 0.82 | 0.62 | 0.40 | 0.78 | 0.9 | 0.95 |
| oka_red | 1990s Russian tiny three-door microcar, worn: faded red paint, rust spots on the sills, short flat bonnet, upright boxy cabin, flat roof, upright hatch, small rectangular headlamps, small square tail lamps | 3.20 | 1.42 | 0.20 | 650 | 0.55 | 0.45 | F | 35 | 0.74 | 0.60 | 0.62 | 0.62 | 0.6 | 0.8 |
| citymini_yellow | late-1990s Korean tiny five-door city car, average condition, bright yellow, short round bonnet, tall rounded roof, short upright hatchback tail, big round-edged headlamps, small vertical tail lamps | 3.50 | 1.50 | 0.30 | 810 | 0.66 | 0.50 | F | 35 | 0.78 | 0.58 | 0.62 | 0.75 | 0.75 | 0.88 |
| citymini_green | late-1990s Korean tiny five-door city car, worn: faded pale green paint, rust spots on the wheel arches, short round bonnet, tall rounded roof, short upright hatchback tail, big round-edged headlamps, small vertical tail lamps | 3.50 | 1.50 | 0.30 | 810 | 0.66 | 0.50 | F | 35 | 0.76 | 0.58 | 0.62 | 0.72 | 0.72 | 0.85 |
| retro500_cream | modern retro two-door small car, good condition, cream with a white roof, domed rounded bonnet, round shoulders all round, short rounded tail, round headlamps, small round-cornered tail lamps | 3.57 | 1.63 | 0.45 | 950 | 0.73 | 0.54 | F | 34 | 0.82 | 0.58 | 0.64 | 0.85 | 0.9 | 0.95 |
| citycar_lime | modern five-door city car, good condition, lime green, short rounded bonnet, tall roof with one long glass area, short hatchback tail, slim headlamps swept back into the front wings, tall tail lamps | 3.60 | 1.60 | 0.32 | 940 | 0.70 | 0.52 | F | 34 | 0.82 | 0.56 | 0.62 | 0.85 | 0.95 | 0.95 |
| cityhatch_silver | modern five-door city hatchback, good condition, silver, short sloping bonnet, long roof, upright hatchback tail, swept-back headlamps, tall tail lamps up the rear corners | 3.67 | 1.68 | 0.32 | 1000 | 0.70 | 0.54 | F | 34 | 0.82 | 0.57 | 0.62 | 0.86 | 0.92 | 0.95 |
| tavria_white | 1990s Ukrainian small three-door hatchback, worn: yellowed white paint, rust along the sills, one front wing in a slightly different white, wedge-shaped flat bonnet, upright cabin, short hatch, plain rectangular headlamps, wide flat tail lamps | 3.71 | 1.55 | 0.18 | 745 | 0.72 | 0.67 | F | 34 | 0.74 | 0.58 | 0.62 | 0.66 | 0.62 | 0.8 |
| rearengine_sky | 1960s Soviet rear-engined two-door small saloon, worn: faded sky blue paint, rust spots, a rounded humped body, short sloping front lid, rounded roof, long rear deck over the engine with air intake slots on its sides, chrome bumpers, round headlamps, small tail lamps | 3.73 | 1.49 | 0.40 | 840 | 0.70 | 0.87 | R | 33 | 0.72 | 0.60 | 0.42 | 0.6 | 0.55 | 0.75 |
| niva_olive | Soviet three-door small 4×4, average condition, olive green, boxy with flat panels, short flat bonnet, upright cab with a flat roof, squared tail, black plastic wheel-arch trims, round headlamps in a flat grille, small square tail lamps | 3.74 | 1.68 | 0.22 | 1210 | 0.70 | 0.84 | A | 32 | 0.80 | 0.70 | 0.55 | 0.72 | 0.75 | 0.85 |

## batch 2

| id | describe | lengthM | widthM | cornerM | massKg | frontOverhangM | rearOverhangM | drive | steerDeg | friction | cgM | frontShare | speed | accel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| minihatch_green | modern retro three-door premium small car, good condition, dark racing green with a white roof and white mirror caps, short round bonnet, wraparound windscreen, short flat tail, round headlamps, oval tail lamps | 3.82 | 1.73 | 0.38 | 1200 | 0.72 | 0.60 | F | 33 | 0.88 | 0.53 | 0.63 | 0.95 | 1.1 | 1.0 |
| aveo_blue | 2000s small Korean five-door hatchback, average condition, mid blue, short rounded bonnet, tall roof, upright tail, large teardrop headlamps, tall tail lamps | 3.88 | 1.67 | 0.32 | 1100 | 0.80 | 0.60 | F | 34 | 0.80 | 0.57 | 0.62 | 0.82 | 0.82 | 0.9 |
| samara_silver | late-1980s Soviet wedge five-door hatchback, average condition, silver, low sloping wedge bonnet, angular cabin, short upright hatch, black plastic bumpers, slim rectangular headlamps, wide flat tail lamps split into red, amber and white sections | 4.01 | 1.65 | 0.20 | 945 | 0.80 | 0.75 | F | 33 | 0.78 | 0.55 | 0.62 | 0.8 | 0.78 | 0.85 |
| golf3_red | 1990s German three-door compact hatchback, worn: faded red paint, rust spots on the wheel arches, one door slightly darker red, level bonnet, long roof, upright hatch with wide pillars, round-cornered headlamps, tall tail lamps | 4.02 | 1.70 | 0.24 | 1050 | 0.83 | 0.72 | F | 33 | 0.78 | 0.56 | 0.62 | 0.8 | 0.8 | 0.85 |
| offroad4x4_khaki | Soviet utility 4×4 with a hard top, worn: faded khaki paint, rust at the edges, flat bonnet, upright square cab, flat roof, flat square tail with a spare wheel on the tailgate, round headlamps on the wings, small square tail lamps | 4.03 | 1.79 | 0.16 | 1540 | 0.75 | 0.88 | A | 31 | 0.78 | 0.78 | 0.55 | 0.62 | 0.6 | 0.75 |
| supermini_red | modern five-door supermini hatchback, good condition, bright red, short sloping bonnet, rounded roof, steep hatchback tail with a small spoiler at the top of the rear window, swept headlamps, tail lamps wrapping round the corners | 4.05 | 1.74 | 0.32 | 1150 | 0.80 | 0.70 | F | 33 | 0.84 | 0.55 | 0.62 | 0.92 | 1.05 | 1.0 |
| supermini_white | modern five-door supermini hatchback, average condition, white, short sloping bonnet, rounded roof, steep hatchback tail, swept headlamps, tail lamps wrapping round the corners | 4.05 | 1.74 | 0.32 | 1150 | 0.80 | 0.70 | F | 33 | 0.84 | 0.55 | 0.62 | 0.9 | 1.0 | 1.0 |
| budgethatch_orange | modern budget five-door hatchback, good condition, bright orange, rounded bonnet, tall roof with roof rails, upright hatch, black unpainted bumpers, tall headlamps, vertical tail lamps | 4.07 | 1.73 | 0.30 | 1080 | 0.80 | 0.66 | F | 33 | 0.82 | 0.57 | 0.62 | 0.86 | 0.9 | 0.95 |
| classic_white | 1970s Soviet compact four-door saloon, average condition, off-white, long flat bonnet, upright three-box roof, separate flat boot, thin chrome bumpers, two round headlamps each side in a chrome grille, vertical tail lamps split into red, amber and white sections | 4.07 | 1.61 | 0.16 | 1035 | 0.75 | 0.95 | R | 32 | 0.76 | 0.58 | 0.53 | 0.72 | 0.7 | 0.8 |

## batch 3

| id | describe | lengthM | widthM | cornerM | massKg | frontOverhangM | rearOverhangM | drive | steerDeg | friction | cgM | frontShare | speed | accel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| classic_red | 1970s Soviet compact four-door saloon, worn: faded brick red paint, rust spots on the sills and boot lid, one wing in primer grey, long flat bonnet, upright three-box roof, separate flat boot, thin chrome bumpers, two round headlamps each side, vertical tail lamps split into red, amber and white sections | 4.07 | 1.61 | 0.16 | 1035 | 0.75 | 0.95 | R | 32 | 0.74 | 0.58 | 0.53 | 0.68 | 0.65 | 0.78 |
| boxestate_blue | 1980s Soviet boxy estate car, worn: faded blue paint, rust on the sills and tailgate edge, completely boxy with flat panels and square corners, long flat bonnet, long flat roof to an upright square tailgate, thin chrome bumpers, square headlamps, tall tail lamps split into red, amber and white sections | 4.12 | 1.62 | 0.16 | 1090 | 0.78 | 0.92 | R | 32 | 0.76 | 0.60 | 0.52 | 0.7 | 0.68 | 0.8 |
| boxsaloon_beige | 1980s Soviet four-door saloon, average condition, faded beige, completely boxy with flat panels and sharp square corners, long and slim, long flat bonnet, upright three-box roof, separate flat boot, thin chrome bumpers, square headlamps, wide flat tail lamps across the corners split into red, amber and white sections | 4.13 | 1.62 | 0.16 | 1060 | 0.78 | 0.93 | R | 32 | 0.78 | 0.58 | 0.53 | 0.75 | 0.72 | 0.82 |
| kombiold_red | 2000s small leisure van, worn: faded red paint, scuffed black bumpers, short sloping bonnet, tall boxy body with a flat high roof, big square tailgate, roof bars, tall headlamps, tall narrow tail lamps | 4.14 | 1.72 | 0.24 | 1250 | 0.80 | 0.75 | F | 33 | 0.78 | 0.70 | 0.60 | 0.75 | 0.72 | 0.85 |
| hatch90s_black | late-1990s German five-door compact hatchback, average condition, black with a few scuffs, rounded level bonnet, long arched roof, upright hatch with wide pillars, smooth teardrop headlamps, round-cornered tail lamps | 4.15 | 1.73 | 0.30 | 1200 | 0.88 | 0.76 | F | 33 | 0.80 | 0.56 | 0.62 | 0.85 | 0.88 | 0.9 |
| smallsuv_yellow | modern small coupe-like crossover, good condition, bright yellow with a black roof, bulging wheel arches with black trim, short high bonnet, sloping roof, small round-edged headlamps on top of the wings, boomerang tail lamps | 4.21 | 1.80 | 0.36 | 1300 | 0.82 | 0.80 | F | 33 | 0.84 | 0.62 | 0.61 | 0.9 | 0.95 | 0.95 |
| wedgesaloon_cherry | 1990s Soviet wedge four-door saloon, worn: faded cherry red paint, rust spots on the sills and boot, low sloping wedge bonnet, angular cabin, short separate boot, black plastic bumpers, slim rectangular headlamps, wide flat tail lamps split into red, amber and white sections | 4.21 | 1.65 | 0.20 | 970 | 0.80 | 0.95 | F | 33 | 0.76 | 0.55 | 0.62 | 0.76 | 0.75 | 0.82 |
| saloon00s_silver | 2000s budget compact four-door saloon, average condition, silver, rounded bonnet, rounded roof, short separate boot, oval headlamps, round-cornered tail lamps | 4.24 | 1.68 | 0.30 | 1080 | 0.82 | 0.85 | F | 33 | 0.80 | 0.56 | 0.62 | 0.8 | 0.8 | 0.88 |
| saloon00s_green | 2000s budget compact four-door saloon, worn: faded dark green paint, rust spots on the wheel arches, one rear door in a lighter green, rounded bonnet, rounded roof, short separate boot, oval headlamps, round-cornered tail lamps | 4.24 | 1.68 | 0.30 | 1080 | 0.82 | 0.85 | F | 33 | 0.78 | 0.56 | 0.62 | 0.78 | 0.76 | 0.85 |

## batch 4

| id | describe | lengthM | widthM | cornerM | massKg | frontOverhangM | rearOverhangM | drive | steerDeg | friction | cgM | frontShare | speed | accel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| saloon70s_teal | 1970s Soviet small four-door saloon, worn: faded teal paint, rust patches, a straight-edged body, flat bonnet, upright cabin, separate boot, chrome bumpers, rectangular headlamps, vertical tail lamps split into red, amber and white sections | 4.25 | 1.55 | 0.18 | 1045 | 0.82 | 1.03 | R | 32 | 0.74 | 0.60 | 0.53 | 0.68 | 0.65 | 0.78 |
| evhatch_blue | new electric family hatchback, glossy light blue with a black roof and glass sunroof panel, very short smooth bonnet with no grille, long cabin, short tail, thin slit headlamps, slim separate tail lamps at each rear corner | 4.26 | 1.81 | 0.34 | 1800 | 0.72 | 0.77 | R | 35 | 0.88 | 0.52 | 0.45 | 0.95 | 1.25 | 1.05 |
| budgetsaloon_grey | modern budget four-door saloon, average condition, dark grey, rounded bonnet, tall roof, short separate boot, large angular headlamps, round-cornered tail lamps | 4.26 | 1.70 | 0.28 | 1120 | 0.80 | 0.85 | F | 33 | 0.80 | 0.56 | 0.62 | 0.82 | 0.82 | 0.9 |
| hatch_graphite | modern five-door family hatchback, good condition, dark graphite grey, level bonnet with a crease down each side, long flat roof, near-vertical hatchback tail, wide rear pillars, narrow headlamps, wide tail lamps | 4.28 | 1.79 | 0.34 | 1300 | 0.88 | 0.77 | F | 32 | 0.86 | 0.55 | 0.61 | 0.97 | 1.15 | 1.0 |
| hatch_white | modern five-door family hatchback, average condition, white, level bonnet with a crease down each side, long flat roof, near-vertical hatchback tail, narrow headlamps, wide tail lamps | 4.28 | 1.79 | 0.34 | 1300 | 0.88 | 0.77 | F | 32 | 0.86 | 0.55 | 0.61 | 0.95 | 1.1 | 1.0 |
| hatch_blue | modern five-door family hatchback, good condition, deep metallic blue, level bonnet with a crease down each side, long flat roof, near-vertical hatchback tail, narrow headlamps, wide tail lamps | 4.28 | 1.79 | 0.34 | 1300 | 0.88 | 0.77 | F | 32 | 0.86 | 0.55 | 0.61 | 0.97 | 1.15 | 1.0 |
| estate90s_red | 1990s compact estate car, worn: faded red paint, rust spots on the sills, one rear door in a slightly different red, rounded bonnet, long roof with roof rails to an upright tailgate, slim rectangular headlamps, tall tail lamps | 4.28 | 1.69 | 0.28 | 1150 | 0.86 | 0.90 | F | 33 | 0.78 | 0.58 | 0.62 | 0.78 | 0.78 | 0.85 |
| saloon90s_white | late-1990s Japanese compact four-door saloon, average condition, white, rounded bonnet, rounded roof, short separate boot, rectangular headlamps, wide tail lamps | 4.30 | 1.69 | 0.28 | 1150 | 0.86 | 0.98 | F | 33 | 0.80 | 0.56 | 0.62 | 0.82 | 0.82 | 0.9 |
| budgetsuv_brown | modern budget compact SUV, average condition, metallic brown, tall squared bonnet, black plastic wheel-arch trim, roof rails, upright tailgate, square headlamps, tall tail lamps | 4.34 | 1.80 | 0.30 | 1300 | 0.82 | 0.82 | A | 32 | 0.80 | 0.66 | 0.60 | 0.82 | 0.85 | 0.9 |

## batch 5

| id | describe | lengthM | widthM | cornerM | massKg | frontOverhangM | rearOverhangM | drive | steerDeg | friction | cgM | frontShare | speed | accel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| budgetsuv_white | modern budget compact SUV, good condition, white, tall squared bonnet, black plastic wheel-arch trim, roof rails, upright tailgate, square headlamps, tall tail lamps | 4.34 | 1.80 | 0.30 | 1300 | 0.82 | 0.82 | F | 32 | 0.80 | 0.66 | 0.61 | 0.84 | 0.88 | 0.9 |
| fleetsaloon_white | plain modern budget four-door saloon, average condition, white, simple rounded bonnet, tall roof, short separate boot lid, black unpainted bumpers and mirrors, large headlamps, round-cornered tail lamps | 4.35 | 1.73 | 0.26 | 1100 | 0.82 | 0.90 | F | 33 | 0.82 | 0.56 | 0.62 | 0.88 | 0.95 | 0.95 |
| fleetsaloon_silver | plain modern budget four-door saloon, worn: dull silver paint, one rear wing a slightly darker grey, simple rounded bonnet, tall roof, short separate boot lid, black unpainted bumpers, large headlamps, round-cornered tail lamps | 4.35 | 1.73 | 0.26 | 1100 | 0.82 | 0.90 | F | 33 | 0.80 | 0.56 | 0.62 | 0.85 | 0.9 | 0.92 |
| cab_white | modern budget four-door saloon taxi, average condition, white with a thin black-and-yellow chequer strip along each flank and a small plain yellow roof sign, simple rounded bonnet, tall roof, short separate boot, large headlamps, round-cornered tail lamps | 4.35 | 1.73 | 0.26 | 1120 | 0.82 | 0.90 | F | 33 | 0.82 | 0.57 | 0.62 | 0.88 | 0.95 | 0.95 |
| liftback90s_green | 1990s Soviet five-door liftback, worn: faded dark green paint, rust along the sills, long flat bonnet, long sloping rear window to a short tail, black plastic bumpers, rectangular headlamps, wide flat tail lamps split into red, amber and white sections | 4.35 | 1.69 | 0.20 | 1080 | 0.82 | 0.95 | F | 33 | 0.76 | 0.58 | 0.62 | 0.72 | 0.7 | 0.8 |
| loafvan_grey | Soviet forward-control 4×4 minibus, worn: faded grey paint with rust at the corners, no bonnet, a rounded loaf-shaped body with a flat front and curved roof edges, round headlamps, small tail lamps | 4.36 | 1.94 | 0.40 | 1850 | 0.95 | 1.11 | A | 31 | 0.76 | 0.85 | 0.50 | 0.6 | 0.55 | 0.72 |
| panelvan_white | modern small panel van, average condition, white with scuffs on the black bumpers, short sloping bonnet, tall flat roof, two square rear doors, large headlamps, tall tail lamps | 4.41 | 1.79 | 0.24 | 1400 | 0.86 | 0.87 | F | 33 | 0.80 | 0.70 | 0.60 | 0.8 | 0.82 | 0.88 |
| family90s_silver | 1990s German family four-door saloon, average condition, silver, smooth rounded wedge bonnet, rounded roof, short high boot, slim headlamps, wide tail lamps | 4.48 | 1.71 | 0.28 | 1250 | 0.85 | 0.95 | F | 33 | 0.80 | 0.56 | 0.62 | 0.85 | 0.85 | 0.9 |
| crossover_red | modern compact crossover, good condition, deep red, tall bonnet with creases, black plastic wheel-arch trim, roof rails, sloping tailgate, slim swept headlamps, slim tail lamps at the corners | 4.48 | 1.85 | 0.34 | 1500 | 0.90 | 0.90 | F | 32 | 0.84 | 0.64 | 0.60 | 0.92 | 1.0 | 0.98 |

## batch 6

| id | describe | lengthM | widthM | cornerM | massKg | frontOverhangM | rearOverhangM | drive | steerDeg | friction | cgM | frontShare | speed | accel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| kombi_sage | modern tall boxy leisure van with seats, good condition, sage green, short sloping bonnet, long flat high roof, big square tailgate, black bumpers | 4.50 | 1.82 | 0.24 | 1450 | 0.88 | 0.87 | F | 32 | 0.84 | 0.70 | 0.60 | 0.88 | 0.95 | 0.95 |
| chinasuv_white | new Chinese compact SUV, glossy pearl white with a black roof, tall bonnet, large grille, black wheel-arch trim, roof rails, sloping tailgate, slim headlamps, slim separate tail lamps at each corner | 4.50 | 1.84 | 0.34 | 1500 | 0.88 | 0.90 | F | 32 | 0.84 | 0.64 | 0.60 | 0.9 | 0.98 | 0.98 |
| liftback00s_green | 2000s Czech compact five-door liftback, average condition, dark green, level bonnet, long roof sloping to a short tail, twin-lens headlamps, tall tail lamps | 4.51 | 1.73 | 0.28 | 1250 | 0.90 | 1.10 | F | 33 | 0.82 | 0.56 | 0.62 | 0.88 | 0.88 | 0.92 |
| sportscar_red | new rear-engined sports coupe, glossy red, low wide body, long sloping front lid between raised front wings, rounded fastback roof, short rear deck with a slim spoiler, round headlamps on top of the wings, slim tail lamps | 4.52 | 1.85 | 0.40 | 1500 | 0.95 | 1.10 | R | 33 | 0.96 | 0.46 | 0.38 | 1.3 | 1.5 | 1.2 |
| mpv_silver | modern compact people carrier, average condition, silver, short sloping bonnet, steep windscreen, tall long roof, upright tailgate, large headlamps, tall tail lamps | 4.53 | 1.83 | 0.30 | 1500 | 0.88 | 0.90 | F | 33 | 0.82 | 0.66 | 0.60 | 0.86 | 0.88 | 0.92 |
| hybrid_white | modern hybrid five-door liftback, good condition, white, low sloping bonnet, arched roof sloping to a high split tail, sharp angular headlamps, angular tail lamps at the corners | 4.54 | 1.76 | 0.32 | 1380 | 0.88 | 0.97 | F | 33 | 0.84 | 0.53 | 0.61 | 0.9 | 0.92 | 0.98 |
| hybrid_silver | modern hybrid five-door liftback, average condition, silver, low sloping bonnet, arched roof sloping to a high split tail, sharp angular headlamps, angular tail lamps at the corners | 4.54 | 1.76 | 0.32 | 1380 | 0.88 | 0.97 | F | 33 | 0.84 | 0.53 | 0.61 | 0.88 | 0.9 | 0.98 |
| smoothsaloon_white | early-1990s German family four-door saloon, worn: dull white paint, rust spots, one front wing a slightly different white, smooth bonnet with no grille, flat roof, long separate boot, wide flat headlamps, wide tail lamps | 4.57 | 1.71 | 0.24 | 1250 | 0.88 | 1.05 | F | 33 | 0.78 | 0.56 | 0.62 | 0.8 | 0.8 | 0.85 |
| archsaloon_silver | 2000s Japanese compact four-door saloon, average condition, silver, rounded bonnet, arched roof, short high boot, angular headlamps, round tail lamps | 4.57 | 1.76 | 0.28 | 1300 | 0.92 | 1.05 | F | 33 | 0.82 | 0.55 | 0.62 | 0.88 | 0.9 | 0.92 |

## batch 7

| id | describe | lengthM | widthM | cornerM | massKg | frontOverhangM | rearOverhangM | drive | steerDeg | friction | cgM | frontShare | speed | accel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| crossover_pearl | modern compact SUV crossover, good condition, pearl white with a black roof, tall squared bonnet, black plastic wheel-arch trim at all four corners, black roof rails, near-vertical tailgate | 4.60 | 1.86 | 0.34 | 1600 | 0.95 | 0.96 | A | 31 | 0.86 | 0.66 | 0.58 | 0.95 | 1.1 | 0.98 |
| compactsuv_black | modern compact SUV, good condition, gloss black, tall squared bonnet, black wheel-arch trim, roof rails, near-vertical tailgate, slim angular headlamps, slim tail lamps | 4.60 | 1.86 | 0.32 | 1650 | 0.95 | 0.96 | A | 31 | 0.86 | 0.66 | 0.58 | 0.95 | 1.1 | 0.98 |
| compactsuv_green | modern compact SUV, average condition, dark khaki green, tall squared bonnet, black wheel-arch trim, roof rails, near-vertical tailgate, slim angular headlamps, slim tail lamps | 4.60 | 1.86 | 0.32 | 1650 | 0.95 | 0.96 | A | 31 | 0.86 | 0.66 | 0.58 | 0.93 | 1.05 | 0.98 |
| compactsaloon_white | modern compact four-door saloon, good condition, white, long low bonnet, sleek roof, short boot, swept headlamps, tail lamps wrapping onto the boot lid | 4.63 | 1.78 | 0.30 | 1300 | 0.92 | 1.00 | F | 32 | 0.86 | 0.54 | 0.61 | 0.95 | 1.05 | 1.0 |
| compactsaloon_grey | modern compact four-door saloon, average condition, mid grey, long low bonnet, sleek roof, short boot, swept headlamps, tail lamps wrapping onto the boot lid | 4.63 | 1.78 | 0.30 | 1300 | 0.92 | 1.00 | F | 32 | 0.86 | 0.54 | 0.61 | 0.93 | 1.0 | 1.0 |
| premsuv00s_black | early-2000s premium SUV, average condition, black with dull scuffed bumpers, long bonnet, rounded roof, upright tailgate, large teardrop headlamps, wide tail lamps | 4.67 | 1.87 | 0.32 | 2150 | 0.90 | 1.00 | A | 31 | 0.84 | 0.70 | 0.50 | 0.95 | 1.05 | 0.95 |
| longestate_green | late-1990s German family estate car, average condition, dark green, level bonnet, long roof with silver roof rails to a sloping tailgate, smooth headlamps, tall tail lamps | 4.68 | 1.74 | 0.28 | 1400 | 0.92 | 1.04 | F | 32 | 0.82 | 0.57 | 0.61 | 0.88 | 0.88 | 0.92 |
| estate_steel | modern family estate car, good condition, steel blue, level bonnet, long roof running all the way to a squared-off tail with a tall rear window, two thin silver roof rails along the roof edges | 4.69 | 1.83 | 0.30 | 1400 | 0.92 | 1.08 | F | 32 | 0.86 | 0.56 | 0.60 | 0.97 | 1.15 | 1.0 |
| estate_black | modern family estate car, good condition, black, level bonnet, long roof to a squared-off tail, two thin silver roof rails, slim headlamps, slim tail lamps | 4.69 | 1.83 | 0.30 | 1400 | 0.92 | 1.08 | F | 32 | 0.86 | 0.56 | 0.60 | 0.97 | 1.15 | 1.0 |

## batch 8

| id | describe | lengthM | widthM | cornerM | massKg | frontOverhangM | rearOverhangM | drive | steerDeg | friction | cgM | frontShare | speed | accel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| liftback_black | modern family five-door liftback, good condition, black, level bonnet, long roof sloping to a short tail, split headlamps, angular tail lamps | 4.69 | 1.81 | 0.30 | 1350 | 0.92 | 1.06 | F | 32 | 0.86 | 0.55 | 0.61 | 0.97 | 1.1 | 1.0 |
| liftback_grey | modern family five-door liftback, average condition, grey, level bonnet, long roof sloping to a short tail, split headlamps, angular tail lamps | 4.69 | 1.81 | 0.30 | 1350 | 0.92 | 1.06 | F | 32 | 0.86 | 0.55 | 0.61 | 0.95 | 1.05 | 1.0 |
| evsaloon_white | new electric four-door saloon, glossy white with an all-glass roof, very short smooth bonnet with no grille, long cabin, short boot, slim headlamps, slim separate tail lamps at each rear corner | 4.69 | 1.85 | 0.34 | 1800 | 0.85 | 1.00 | R | 34 | 0.90 | 0.48 | 0.47 | 1.1 | 1.4 | 1.1 |
| evsaloon_red | new electric four-door saloon, glossy deep red with an all-glass roof, very short smooth bonnet with no grille, long cabin, short boot, slim headlamps, slim separate tail lamps at each rear corner | 4.69 | 1.85 | 0.34 | 1800 | 0.85 | 1.00 | R | 34 | 0.90 | 0.48 | 0.47 | 1.1 | 1.4 | 1.1 |
| premsaloon_blue | modern premium compact saloon, good condition, deep metallic blue, long bonnet, cab-rearward roof, short boot, slim angular headlamps, L-shaped tail lamps | 4.71 | 1.83 | 0.30 | 1550 | 0.80 | 1.06 | R | 32 | 0.90 | 0.52 | 0.52 | 1.05 | 1.25 | 1.05 |
| premsaloon_white | modern premium compact saloon, good condition, white, long bonnet, cab-rearward roof, short boot, slim angular headlamps, L-shaped tail lamps | 4.71 | 1.83 | 0.30 | 1550 | 0.80 | 1.06 | R | 32 | 0.90 | 0.52 | 0.52 | 1.05 | 1.25 | 1.05 |
| bigsaloon70s_black | 1970s Soviet large four-door saloon, good condition kept as a classic, gloss black, long flat bonnet, upright roof, long flat boot, chrome bumpers and trim strips, round headlamps in a wide chrome grille, vertical tail lamps split into red, amber and white sections | 4.73 | 1.80 | 0.18 | 1420 | 0.85 | 1.15 | R | 31 | 0.76 | 0.62 | 0.53 | 0.75 | 0.7 | 0.8 |
| evsuv_grey | new electric crossover, glossy mid grey with an all-glass roof, short smooth bonnet with no grille, tall rounded cabin, short tail, slim headlamps, slim separate tail lamps at each rear corner | 4.75 | 1.92 | 0.36 | 2000 | 0.85 | 1.00 | A | 33 | 0.90 | 0.55 | 0.48 | 1.05 | 1.35 | 1.05 |
| execsaloon90s_beige | 1990s German boxy executive four-door saloon, average condition, beige, long flat bonnet, upright glasshouse, long flat boot, wide rectangular headlamps, wide ribbed tail lamps split into red, amber and white sections | 4.76 | 1.74 | 0.20 | 1400 | 0.85 | 1.11 | R | 32 | 0.80 | 0.56 | 0.53 | 0.85 | 0.85 | 0.9 |

## batch 9

| id | describe | lengthM | widthM | cornerM | massKg | frontOverhangM | rearOverhangM | drive | steerDeg | friction | cgM | frontShare | speed | accel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| execestate90s_silver | 1990s German boxy executive estate car, worn: dull silver paint, rust spots on the tailgate, long flat bonnet, long roof with roof rails to an upright tailgate, wide rectangular headlamps, tall tail lamps split into red, amber and white sections | 4.77 | 1.74 | 0.20 | 1500 | 0.85 | 1.12 | R | 32 | 0.78 | 0.58 | 0.52 | 0.82 | 0.8 | 0.88 |
| sportsaloon_blue | late-1990s German sports saloon, average condition, dark metallic blue, long bonnet, cab-rearward roof, short boot, twin round headlamps under a glass cover, wide tail lamps | 4.78 | 1.80 | 0.28 | 1550 | 0.83 | 1.10 | R | 32 | 0.84 | 0.54 | 0.52 | 0.95 | 1.0 | 0.95 |
| coupe_yellow | modern two-door muscle coupe, good condition, bright yellow with two black stripes over the bonnet and roof, long bonnet, short fastback roof, short tail, slim headlamps, three-bar tail lamps at each rear corner | 4.78 | 1.92 | 0.32 | 1700 | 0.95 | 1.05 | R | 32 | 0.90 | 0.52 | 0.54 | 1.2 | 1.35 | 1.05 |
| aerosaloon_silver | early-1990s German aerodynamic four-door saloon, worn: dull silver paint, rust spots on the sills, smooth sloping bonnet, flush windows, long boot, wide flat headlamps, a wide tail lamp at each rear corner | 4.79 | 1.78 | 0.28 | 1350 | 0.97 | 1.13 | F | 32 | 0.80 | 0.56 | 0.62 | 0.85 | 0.82 | 0.88 |
| passvan90s_silver | 1990s passenger van, average condition, silver, a short sloping nose, long tall body with a flat roof, big square tailgate, rectangular headlamps, tall tail lamps | 4.79 | 1.84 | 0.22 | 1750 | 0.92 | 0.95 | F | 32 | 0.78 | 0.78 | 0.60 | 0.78 | 0.72 | 0.82 |
| sevenseat_silver | modern seven-seat SUV, good condition, silver, tall long bonnet, long roof with roof rails, upright tailgate, large angular headlamps, tall tail lamps | 4.79 | 1.90 | 0.32 | 1900 | 0.95 | 1.05 | A | 31 | 0.84 | 0.70 | 0.58 | 0.92 | 0.98 | 0.95 |
| offroad00s_grey | 2000s Japanese off-road SUV, average condition, grey, flat bonnet, upright cab, flat roof, square tail with a covered spare wheel on the tailgate, large headlamps, tall tail lamps | 4.80 | 1.88 | 0.26 | 2100 | 0.88 | 1.05 | A | 31 | 0.80 | 0.78 | 0.55 | 0.85 | 0.85 | 0.88 |
| offroader_black | modern boxy luxury off-roader, good condition, gloss black, flat square bonnet with indicators on top of the wings, upright square cab, flat roof, square tail with a covered spare wheel, round headlamps, square tail lamps | 4.82 | 1.93 | 0.18 | 2450 | 0.82 | 1.15 | A | 31 | 0.84 | 0.80 | 0.52 | 0.95 | 1.05 | 0.92 |
| midsaloon00s_white | 2000s Japanese mid-size four-door saloon, average condition, white, long rounded bonnet, rounded roof, short high boot, large headlamps, wide tail lamps | 4.82 | 1.79 | 0.30 | 1450 | 0.95 | 1.07 | F | 32 | 0.84 | 0.55 | 0.62 | 0.9 | 0.92 | 0.95 |

## batch 10

| id | describe | lengthM | widthM | cornerM | massKg | frontOverhangM | rearOverhangM | drive | steerDeg | friction | cgM | frontShare | speed | accel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| panelvan90s_white | 1990s panel van, worn: dull white paint, rust spots on the doors and roof edges, a short sloping bonnet, tall body with a flat roof, two square rear doors, rectangular headlamps, tall narrow tail lamps | 4.83 | 1.93 | 0.22 | 1800 | 0.80 | 1.20 | R | 32 | 0.76 | 0.82 | 0.55 | 0.72 | 0.65 | 0.78 |
| midsaloon_black | modern mid-size four-door saloon, good condition, gloss black, long low bonnet, sleek roof sloping into a short boot, thin chrome strip round the side windows, slim headlamps, wide tail lamps | 4.88 | 1.84 | 0.30 | 1550 | 0.98 | 1.08 | F | 31 | 0.88 | 0.54 | 0.61 | 1.0 | 1.2 | 1.05 |
| midsaloon_silver | modern mid-size four-door saloon, average condition, silver, long low bonnet, sleek roof sloping into a short boot, slim headlamps, wide tail lamps | 4.88 | 1.84 | 0.30 | 1550 | 0.98 | 1.08 | F | 31 | 0.88 | 0.54 | 0.61 | 0.98 | 1.15 | 1.05 |
| cab_yellow | modern mid-size four-door saloon taxi, average condition, bright yellow with a small plain white roof sign, long low bonnet, sleek roof, short boot, slim headlamps, wide tail lamps | 4.88 | 1.84 | 0.30 | 1570 | 0.98 | 1.08 | F | 31 | 0.86 | 0.55 | 0.61 | 0.95 | 1.1 | 1.0 |
| bigsaloon90s_white | 1990s Soviet large boxy four-door saloon, worn: dull white paint, rust on the sills and boot edge, long flat bonnet, upright roof, long flat boot, black bumpers, rectangular headlamps, wide flat tail lamps split into red, amber and white sections | 4.88 | 1.80 | 0.20 | 1400 | 0.88 | 1.20 | R | 31 | 0.76 | 0.62 | 0.53 | 0.75 | 0.7 | 0.8 |
| vanbus_white | modern mid-size passenger van, good condition, white, short sloping bonnet, long tall body with a flat roof, big square tailgate, large headlamps, tall tail lamps | 4.90 | 1.90 | 0.24 | 2000 | 0.80 | 0.95 | R | 32 | 0.82 | 0.78 | 0.55 | 0.88 | 0.88 | 0.9 |
| premsuv_white | new premium large SUV, glossy white, long tall bonnet with a big grille, long roof with roof rails, sloping tailgate, slim headlamps, slim tail lamps at each rear corner | 4.92 | 2.00 | 0.34 | 2200 | 0.95 | 1.05 | A | 31 | 0.88 | 0.68 | 0.52 | 1.05 | 1.2 | 1.0 |
| premsuv_black | new premium large SUV, gloss black, long tall bonnet with a big grille, long roof with roof rails, sloping tailgate, slim headlamps, slim tail lamps at each rear corner | 4.92 | 2.00 | 0.34 | 2200 | 0.95 | 1.05 | A | 31 | 0.88 | 0.68 | 0.52 | 1.05 | 1.2 | 1.0 |
| premestate_grey | modern premium estate car, good condition, dark grey, long bonnet, long roof with roof rails to a sloping tailgate, slim headlamps, slim tail lamps | 4.93 | 1.85 | 0.30 | 1750 | 0.95 | 1.05 | A | 31 | 0.88 | 0.56 | 0.55 | 1.05 | 1.2 | 1.05 |

## batch 11

| id | describe | lengthM | widthM | cornerM | massKg | frontOverhangM | rearOverhangM | drive | steerDeg | friction | cgM | frontShare | speed | accel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| execsaloon_black | modern executive four-door saloon, good condition, gloss black, long bonnet, cab-rearward roof, short boot, slim headlamps, wide tail lamps at the corners | 4.94 | 1.87 | 0.30 | 1750 | 0.85 | 1.15 | R | 32 | 0.90 | 0.53 | 0.52 | 1.08 | 1.25 | 1.05 |
| largesuv_black | modern large body-on-frame SUV, good condition, black, long square bonnet, tall flat roof with roof rails, upright tailgate, large headlamps, tall tail lamps | 4.95 | 1.98 | 0.28 | 2600 | 0.90 | 1.10 | A | 31 | 0.82 | 0.82 | 0.54 | 0.92 | 0.95 | 0.9 |
| luxsuv_black | new luxury SUV, gloss black, long flat bonnet, floating flat roof, short upright tail, slim headlamps, slim tail lamps at each rear corner | 5.05 | 2.05 | 0.32 | 2500 | 0.95 | 1.10 | A | 31 | 0.88 | 0.70 | 0.52 | 1.05 | 1.2 | 1.0 |
| luxvan_black | new luxury people van, gloss black, short sloping bonnet, long tall body with a flat roof, tinted glass, big tailgate, slim headlamps, tall tail lamps | 5.14 | 1.93 | 0.26 | 2200 | 0.85 | 1.05 | R | 32 | 0.84 | 0.76 | 0.55 | 0.92 | 0.95 | 0.95 |
| luxsaloon_black | new luxury limousine saloon, gloss black, very long bonnet, long cabin, short boot, chrome strip round the windows, slim headlamps, slim wide tail lamps at the corners | 5.18 | 1.92 | 0.32 | 2050 | 0.95 | 1.15 | R | 31 | 0.90 | 0.54 | 0.52 | 1.1 | 1.25 | 1.05 |
| pickup_silver | modern double-cab pickup with a hard flat bed cover, good condition, silver, tall square bonnet, double cab, long flat covered bed, tall headlamps, tall tail lamps at the bed's rear corners | 5.33 | 1.86 | 0.24 | 2100 | 0.90 | 1.12 | A | 31 | 0.82 | 0.78 | 0.58 | 0.85 | 0.85 | 0.85 |
| hightopvan_white | modern high-roof panel van, average condition, white, short sloping bonnet, very tall flat roof, two square rear doors, large headlamps, tall narrow tail lamps | 5.41 | 2.05 | 0.24 | 2100 | 0.80 | 1.05 | F | 32 | 0.80 | 0.85 | 0.60 | 0.8 | 0.75 | 0.85 |
| lightvan_white | 2000s Russian light commercial van, worn: dull white paint, rust at the wheel arches and door edges, a short bonnet, tall boxy cab and body, flat roof, two square rear doors, rectangular headlamps, small square tail lamps | 5.50 | 2.07 | 0.22 | 2100 | 1.05 | 1.20 | R | 32 | 0.76 | 0.85 | 0.55 | 0.7 | 0.62 | 0.75 |
| bigvan_silver | large high-roof panel van, average condition, silver, short sloping bonnet, very long tall body with a flat roof, two square rear doors, large headlamps, tall narrow tail lamps | 5.91 | 1.99 | 0.24 | 2300 | 0.98 | 1.27 | R | 32 | 0.80 | 0.88 | 0.55 | 0.78 | 0.7 | 0.82 |

## End state

The hundred replace the sixteen legacy looks in `Fleet.json` once the new fleet spans every size (after batch 11),
and the earlier looks no batch asks again (`citybox_sky`, `estate_navy`, `hatch_coral`, `micro_mint`, `minivan_wine`,
`panelvan_forest`, `retro_cream`, `saloon_beige`, `suv_graphite`) go with them — _owner_: both are in
docs/tech-debt.md. The service vehicles (`police_white`, `ambulance_white`, `evacuator_yellow`) are not this plan's.
