# Service vehicle plan

Same columns and rules as `fleet_plan.md`. **Each service vehicle is a fleet body repainted** (the owner,
2026-10-22: the new fleet's style, the current `police_white`'s features and proportions; the first free-drawn set
came back unlike real cars): its figures are that body's plan row, its picture the installed body sent as image 1 and
redrawn in the livery, with the current service picture as image 2 for the livery and equipment.

**Lens cap** (`CarLamps.MostLenses` 10): headlamps 2, indicators 4, tail 2 and a beacon bar's two ends — the tail is
one `rear` lens a side (red, white in reverse), never brake and reverse apart. One bar a vehicle, each half no more
than 0.4 m across (`LampAtlas.CellPx`).

## batch 1

Bodies, in order: `niva_olive`, `boxsaloon_beige`, `hatch_white`, `budgetsuv_white`, `fleetsaloon_white`,
`panelvan_white`, `hybrid_silver`, `crossover_pearl`, `compactsaloon_white`.

| id | describe | lengthM | widthM | cornerM | massKg | frontOverhangM | rearOverhangM | drive | steerDeg | friction | cgM | frontShare | speed | accel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| policeniva_white | Soviet three-door small 4×4 patrol car in police livery | 3.74 | 1.68 | 0.22 | 1260 | 0.70 | 0.84 | A | 32 | 0.80 | 0.72 | 0.55 | 0.85 | 0.85 | 0.9 |
| policesaloon80s_white | 1980s Soviet four-door saloon patrol car in police livery | 4.13 | 1.62 | 0.16 | 1110 | 0.78 | 0.93 | R | 32 | 0.78 | 0.60 | 0.53 | 0.9 | 0.85 | 0.88 |
| policehatch_white | modern five-door family hatchback patrol car in police livery | 4.28 | 1.79 | 0.34 | 1350 | 0.88 | 0.77 | F | 32 | 0.86 | 0.57 | 0.61 | 1.1 | 1.25 | 1.05 |
| policesuv_white | modern budget compact SUV patrol car in police livery | 4.34 | 1.80 | 0.30 | 1350 | 0.82 | 0.82 | F | 32 | 0.82 | 0.68 | 0.61 | 1.0 | 1.05 | 1.0 |
| police_white | plain modern budget four-door saloon patrol car in police livery | 4.35 | 1.73 | 0.26 | 1150 | 0.82 | 0.90 | F | 33 | 0.84 | 0.58 | 0.62 | 1.18 | 1.5 | 1.12 |
| policevan_white | modern small panel van in police livery | 4.41 | 1.79 | 0.24 | 1450 | 0.86 | 0.87 | F | 33 | 0.80 | 0.72 | 0.60 | 0.95 | 1.0 | 0.95 |
| policehybrid_white | modern hybrid five-door liftback patrol car in police livery | 4.54 | 1.76 | 0.32 | 1430 | 0.88 | 0.97 | F | 33 | 0.86 | 0.55 | 0.61 | 1.1 | 1.1 | 1.05 |
| policecrossover_white | modern compact SUV crossover patrol car in police livery | 4.60 | 1.86 | 0.34 | 1650 | 0.95 | 0.96 | A | 31 | 0.88 | 0.68 | 0.58 | 1.1 | 1.3 | 1.05 |
| policesaloon_white | modern compact four-door saloon patrol car in police livery | 4.63 | 1.78 | 0.30 | 1350 | 0.92 | 1.00 | F | 32 | 0.88 | 0.56 | 0.61 | 1.15 | 1.3 | 1.08 |

## batch 2

Ambulances, repainted the same way. Bodies, in order: `loafvan_grey`, `panelvan_white`, `execestate90s_silver`,
`passvan90s_silver`, `panelvan90s_white`, `vanbus_white`, `hightopvan_white`, `lightvan_white`, `bigvan_silver`.
Figures are the body's row with 300 kg of stretcher and kit (cg 0.03 m higher) and the pull slightly blunted.

| id | describe | lengthM | widthM | cornerM | massKg | frontOverhangM | rearOverhangM | drive | steerDeg | friction | cgM | frontShare | speed | accel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| ambulanceloaf_white | Soviet forward-control 4×4 minibus ambulance in ambulance livery | 4.36 | 1.94 | 0.40 | 2150 | 0.95 | 1.11 | A | 31 | 0.76 | 0.88 | 0.50 | 0.6 | 0.5 | 0.72 |
| ambulancevan_white | modern small panel van ambulance in ambulance livery | 4.41 | 1.79 | 0.24 | 1700 | 0.86 | 0.87 | F | 33 | 0.80 | 0.73 | 0.60 | 0.8 | 0.78 | 0.88 |
| ambulanceestate90s_white | 1990s boxy estate car ambulance in ambulance livery | 4.77 | 1.74 | 0.20 | 1800 | 0.85 | 1.12 | R | 32 | 0.78 | 0.61 | 0.52 | 0.82 | 0.76 | 0.88 |
| ambulancepassvan90s_white | 1990s passenger van ambulance in ambulance livery | 4.79 | 1.84 | 0.22 | 2050 | 0.92 | 0.95 | F | 32 | 0.78 | 0.81 | 0.60 | 0.78 | 0.68 | 0.82 |
| ambulancevan90s_white | 1990s panel van ambulance in ambulance livery | 4.83 | 1.93 | 0.22 | 2100 | 0.80 | 1.20 | R | 32 | 0.76 | 0.85 | 0.55 | 0.72 | 0.62 | 0.78 |
| ambulancebus_white | modern mid-size van ambulance in ambulance livery | 4.90 | 1.90 | 0.24 | 2300 | 0.80 | 0.95 | R | 32 | 0.82 | 0.81 | 0.55 | 0.88 | 0.84 | 0.9 |
| ambulance_white | modern high-roof van ambulance in ambulance livery | 5.41 | 2.05 | 0.24 | 2400 | 0.80 | 1.05 | F | 32 | 0.80 | 0.88 | 0.60 | 0.8 | 0.71 | 0.85 |
| ambulancelight_white | 2000s Russian light van ambulance in ambulance livery | 5.50 | 2.07 | 0.22 | 2400 | 1.05 | 1.20 | R | 32 | 0.76 | 0.88 | 0.55 | 0.7 | 0.59 | 0.75 |
| ambulancebig_white | large high-roof van ambulance in ambulance livery | 5.91 | 1.99 | 0.24 | 2600 | 0.98 | 1.27 | R | 32 | 0.80 | 0.91 | 0.55 | 0.78 | 0.66 | 0.82 |

## batch 3

Tow trucks: each body keeps its cab and carries an open steel tow deck behind it, the wheel-lift arm under its tail
(the shared beam pictures). Bodies, in order: `loafvan_grey`, `offroad00s_grey`, `passvan90s_silver`,
`panelvan90s_white`, `vanbus_white`, `pickup_silver`, `hightopvan_white`, `lightvan_white`, `bigvan_silver`.
Widths are the installed bodies' (`collisionM`), not their plan rows'. Figures are the body's row with 900 kg of deck
and lift (750 on the three smallest), cg 0.10 m higher, the pull blunted: speed ×0.85, acceleration ×0.75,
braking ×0.9.

| id | describe | lengthM | widthM | cornerM | massKg | frontOverhangM | rearOverhangM | drive | steerDeg | friction | cgM | frontShare | speed | accel | brake |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| evacuatorloaf_yellow | Soviet forward-control 4×4 tow truck with an open tow deck, in recovery livery | 4.36 | 2.15 | 0.40 | 2600 | 0.95 | 1.11 | A | 31 | 0.76 | 0.95 | 0.50 | 0.51 | 0.41 | 0.65 |
| evacuatoroffroad_yellow | 2000s Japanese off-road 4×4 tow truck with an open tow deck, in recovery livery | 4.80 | 1.89 | 0.26 | 2850 | 0.88 | 1.05 | A | 31 | 0.80 | 0.88 | 0.55 | 0.72 | 0.64 | 0.79 |
| evacuatorpassvan90s_yellow | 1990s van-cab tow truck with an open tow deck, in recovery livery | 4.79 | 1.92 | 0.22 | 2500 | 0.92 | 0.95 | F | 32 | 0.78 | 0.88 | 0.60 | 0.66 | 0.54 | 0.74 |
| evacuatorvan90s_yellow | 1990s van-cab tow truck with an open tow deck, worn, in recovery livery | 4.83 | 1.90 | 0.22 | 2700 | 0.80 | 1.20 | R | 32 | 0.76 | 0.92 | 0.55 | 0.61 | 0.49 | 0.70 |
| evacuatorbus_yellow | modern van-cab tow truck with an open tow deck, in recovery livery | 4.90 | 2.11 | 0.24 | 2900 | 0.80 | 0.95 | R | 32 | 0.82 | 0.88 | 0.55 | 0.75 | 0.66 | 0.81 |
| evacuatorpickup_yellow | modern double-cab pickup tow truck with an open tow deck, in recovery livery | 5.33 | 2.07 | 0.24 | 3000 | 0.90 | 1.12 | A | 31 | 0.82 | 0.88 | 0.58 | 0.72 | 0.64 | 0.77 |
| evacuatorhightop_yellow | modern high-cab tow truck with an open tow deck, in recovery livery | 5.41 | 2.21 | 0.24 | 3000 | 0.80 | 1.05 | F | 32 | 0.80 | 0.95 | 0.60 | 0.68 | 0.56 | 0.77 |
| evacuator_yellow | 2000s Russian light tow truck with an open tow deck, in recovery livery | 5.50 | 2.26 | 0.22 | 3000 | 1.05 | 1.20 | R | 32 | 0.76 | 0.95 | 0.55 | 0.60 | 0.47 | 0.68 |
| evacuatorbig_yellow | large tow truck with an open tow deck, in recovery livery | 5.91 | 2.03 | 0.24 | 3200 | 0.98 | 1.27 | R | 32 | 0.80 | 0.98 | 0.55 | 0.66 | 0.53 | 0.74 |
