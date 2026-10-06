// The reservations' rows on the device (LaneOccupancy), field for field with app/devicetick/DeviceClaims: every claim
// a row, every hold a row, and what each hold was answered.

// ClaimPriority's rungs that are named here.
const uint RungHard = 0u;

// LaneRoster.Driving, and LaneOccupancy's NoWay.
const uint RosterDriving = 0u;
const int OnwardNoWay = -1;

// HoldRow.flags.
const uint HoldAnswered = 1u;
const uint HoldOnACall = 2u;

// ClaimRow.flags.
const uint ClaimRungBits = 0xFu;
const uint ClaimRosterShift = 4u;
const uint ClaimOnItsLine = 1u << 6u;
const uint ClaimSecondary = 1u << 7u;
const uint ClaimStill = 1u << 8u;
const uint ClaimPassing = 1u << 9u;
const uint ClaimGone = 1u << 10u;

const uint NoRow = 0xFFFFFFFFu;

// One reservation on one stretch of one way (LaneClaim), with the way it is on; a main claim of a plan carries how far
// along the way its holder's own body already reaches (Reach's standsToM).
struct ClaimRow
{
    float fromM;
    float toM;
    float alongMps;
    float lineFromM;
    float aheadM;
    float committedToM;
    float standsToM;
    int occupant;
    int hold;
    int onward;
    uint way;
    uint flags;
};

// One hold: its pieces, the ground it keeps off a cut, whether it is answered with the plans, its holder's terms
// (HolderTerms), and where it was laid to end (LaneOccupancy.BeginHold, EndHold) — the answer of a hold placed rather
// than asked.
struct HoldRow
{
    uint firstRow;
    uint endRow;
    float standingMarginM;
    uint flags;
    float keptOffWhatItMayPassM;
    float passesBelowMps;
    float laidCutLineM;
    float laidMarginM;
    uint laidCutBy;
    int laidCutOn;
    float pad0;
    float pad1;
};

// What one hold was answered (PlanAnswer): cutBy a row, or NoRow.
struct ClaimAnswer
{
    float cutLineM;
    float marginM;
    uint cutBy;
    int cutOn;
    float cutWayM;
    float pad0;
    float pad1;
    float pad2;
};
