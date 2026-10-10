// Every hold a rebuild lays before its plans are answered numbered and filed on the device, with the rows they lay
// (TownWorld.LightTheWays, PlanTheDrive, PlanTheWalk): a unit each in the host's order — every light's stretch, then the
// claims laid where they stand (rebuild_standing.comp), then every car's claim, then every walker's walk — counted,
// scanned, and each unit's filed from where its scans say, after the bodies and their passes. The one binding layout both
// kernels of it run over (rebuild_count.comp, rebuild_file.comp), laid by app/devicetick/DeviceRebuild. The including
// kernel declares `figures` (stretchCount, carCount, walkerCount, mostPieces, standOffM) first.
//
// A unit the host lays itself is filed by rebuild_host.comp; here it is only counted.

#include "world/road/kernels/claims_rows.glsl"
#include "world/road/kernels/claims_tally.glsl"
#include "agents/car/actions/follow/kernels/plan_rows.glsl"
#include "world/town/kernels/rebuild_rows.glsl"

// The lights' stretches as signal_holds.comp left them: held, placed, where cut and by which row; and the piece placed.
layout(std430, binding = 0) readonly buffer Held { vec4 held[]; };
layout(std430, binding = 1) readonly buffer StretchPieces { ClaimRow stretchPieces[]; };

// What the host lays itself, and what it says of each car.
layout(std430, binding = 2) readonly buffer HostUnits { HostUnit hostUnits[]; };
layout(std430, binding = 3) readonly buffer CarClaims { CarClaimRow carClaims[]; };

// The cars' plans as plan_cars.comp was sent them and left them (app/devicetick/DevicePlans).
layout(std430, binding = 4) readonly buffer PlanCars { uvec4 planCars[]; };
layout(std430, binding = 5) readonly buffer PlanFigures { vec4 planFigures[]; };
layout(std430, binding = 6) readonly buffer PlanResults { PlanOutRow planResults[]; };
layout(std430, binding = 7) readonly buffer PlanPieces { ClaimRow planPieces[]; };

// The walkers' walks as walkers_plan.comp left them (app/devicetick/DeviceWalkers): a hold laid, ways, rows; the hold and
// the rows.
layout(std430, binding = 8) readonly buffer WalkerCounts { uvec4 walkerCounts[]; };
layout(std430, binding = 9) readonly buffer WalkerHolds { HoldRow walkerHolds[]; };
layout(std430, binding = 10) readonly buffer WalkerProposed { ClaimRow walkerProposed[]; };

// Each unit's rows and holds, scanned in place into where each starts.
layout(std430, binding = 11) buffer UnitRows { uint unitRows[]; };
layout(std430, binding = 12) buffer UnitHolds { uint unitHolds[]; };

// The claims' own tables (app/devicetick/DeviceClaims.OnTheDevice).
layout(std430, binding = 13) writeonly buffer Rows { ClaimRow rows[]; };
layout(std430, binding = 14) writeonly buffer Holds { HoldRow holds[]; };
layout(std430, binding = 15) writeonly buffer Answers { ClaimAnswer answers[]; };
layout(std430, binding = 16) writeonly buffer Shown { float shown[]; };
layout(std430, binding = 17) buffer Tally { uint tally[]; };

// What each car on a pass found inside it (rebuild_asks.comp).
layout(std430, binding = 18) readonly buffer AskAnswers { uvec4 askAnswers[]; };

// The claims laid where they stand as rebuild_standing.comp numbered them from the unit's own first — how many rows and
// holds, the rows and the holds — and what it took of each light's stretch.
layout(std430, binding = 19) readonly buffer StandingCounts { uint standingCounts[]; };
layout(std430, binding = 20) readonly buffer StandingRows { ClaimRow standingRows[]; };
layout(std430, binding = 21) readonly buffer StandingHolds { HoldRow standingHolds[]; };
layout(std430, binding = 22) readonly buffer StretchCuts { uvec4 stretchCuts[]; };

// Whether a car on a pass has a body inside it, so its claim is the hold short of that and not the plan the host laid
// past its pass (Following.Plan).
bool plansHeldInItsPass(uint car) { return carClaims[car].askCount > 0u && askAnswers[car][AskFound] != 0u; }

uint plansUnitCount() { return figures.stretchCount + 1u + figures.carCount + figures.walkerCount; }

uint plansFirstCar() { return figures.stretchCount + 1u; }

uint plansFirstWalker() { return plansFirstCar() + figures.carCount; }

// How many rows and holds one unit lays (x rows, y holds).
uvec2 plansCountOf(uint unit)
{
    if (unit < figures.stretchCount)
    {
        vec4 stretch = held[unit];
        return uvec2(stretch.y != 0.0 ? 1u : 0u, stretch.x != 0.0 ? 1u : 0u);
    }

    if (unit < plansFirstCar()) return uvec2(standingCounts[0], standingCounts[1]);

    if (unit < plansFirstWalker())
    {
        uint car = unit - plansFirstCar();
        CarClaimRow claim = carClaims[car];
        if ((claim.flags & CarPlansOnTheDevice) != 0u) return uvec2(planResults[car].pieces, planResults[car].laidAHold);
        if (plansHeldInItsPass(car)) return uvec2(0u, 1u);
        if (claim.hostUnit < 0) return uvec2(0u);

        HostUnit laid = hostUnits[claim.hostUnit];
        return uvec2(laid.rowCount, laid.holdCount);
    }

    uvec4 walk = walkerCounts[unit - plansFirstWalker()];
    return uvec2(walk.w, walk.x);
}

// One hold filed, with the answer it was laid to and how far it is shown before the first pass.
void plansFileTheHold(uint holdAt, HoldRow held)
{
    holds[holdAt] = held;
    answers[holdAt] = rebuildLaidAnswer(held);
    shown[holdAt] = rebuildShownAtFirst(held);
}
