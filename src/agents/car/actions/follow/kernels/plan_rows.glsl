// The rows a car's plan is laid from and into (plan.glsl), field for field with app/devicetick/DevicePlans.

// ClaimPriority's rungs a plan reads.
const uint RungSpecial = 2u;
const uint RungFirm = 7u;

// One way's section a mark links another's stretch to (CrossedSection), filed under that other way.
struct CrossedSection
{
    int onWay;
    float fromM;
    float toM;
    float mineFromM;
    float mineToM;
};

// What the plan reads of a car (CarFleet), and whether it plans down its line at all this rebuild (TownWorld's
// PlansDownItsLine) — the host's to say until the actions are the device's.
struct PlanCarRow
{
    float progressM;
    float alongMps;
    float plannedMps;
    float groundCoefficient;
    float grantMarginM;
    float keptOffWhatItMayPassM;
    uint flags;
    uint build;
};

const uint PlanCarProposes = 1u;
const uint PlanCarOnACall = 2u;
const uint PlanCarLineIsReverse = 4u;

// What a car's claim came to (CarFleet.ClaimFromM, ClaimToM, CommittedToM, HorizonM; HolderTerms.PassesBelowMps), and
// how many pieces it laid.
struct PlanOutRow
{
    float claimFromM;
    float claimToM;
    float committedToM;
    float horizonM;
    float passesBelowMps;
    uint pieces;
    uint laidAHold;
    uint pad;
};

// A stretch of a way under a line (LineWay).
struct LineWayRow
{
    uint way;
    float fromM;
    float toM;
    float lineFromM;
};
