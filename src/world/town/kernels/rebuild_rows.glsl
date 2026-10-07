// The rows a claims rebuild is numbered and filed with on the device (TownWorld.RebuildLaneOccupancy, as
// app/devicetick/DeviceRebuild records it), field for field with DeviceRebuild's. The including kernel includes
// claims_rows.glsl first.
//
// Every slot and every hold is numbered as the host's laying numbers it: the pieces each producer laid are counted unit
// by unit in the host's laying order, the counts scanned, and each unit's filed from where its scan says — a unit being
// one walker's body, one car's, one light's stretch, one car's claim or one walker's walk, or a run the host still lays
// itself (HostUnit).

// Which numbering a host-laid unit is filed in: among the bodies, after them (the passes); among the plans, at a unit of
// their own; or after the answers (the backing).
const uint HostPasses = 0u;
const uint HostPlans = 1u;
const uint HostBacking = 2u;

// One run the host still lays itself (DeviceRebuild.HostUnit): its rows and holds in the host's own table, the slot and
// the hold the host numbered its first — what its rows' holds and its holds' rows are counted from — and where it is
// filed: its numbering and, among the plans, its unit.
struct HostUnit
{
    uint rowsAt;
    uint rowCount;
    uint holdsAt;
    uint holdCount;
    int hostSlotFirst;
    int hostHoldFirst;
    uint numbering;
    uint unit;
};

// What the host tells the rebuild of one car (DeviceRebuild.CarClaimRow): whether it was asked this laying and whether
// its claim is the plan the device lays (CarAsked, CarPlansOnTheDevice); where else its claim is laid — a host unit, the
// plans' unit its grant's hold is in and that hold's place there — and, for a claim the host laid, what the car was left
// granted before its grant is read (CarFleet.AuthorityM, HorizonM, ClaimFromM, CommittedToM, GrantMarginM, GrantCutBy);
// and, for a car on a pass whose plan the host laid as though its pass were clear, the run of BodyAskRows that looks for
// a body inside it (Following.AsksOfItsPass).
struct CarClaimRow
{
    uint flags;
    int hostUnit;
    int holdUnit;
    int holdOffset;
    float authorityM;
    float horizonM;
    float claimFromM;
    float committedToM;
    float grantMarginM;
    uint grantCutBy;
    int askFirst;
    uint askCount;
};

const uint CarAsked = 1u;
const uint CarPlansOnTheDevice = 2u;

// One stretch of one way asked for a body standing in it (BodyAsk): somebody on foot alone, a body beginning at its end
// left out (AskOnFoot) — otherwise anybody but the asker, one beginning there taken — and where on the asker's line it
// stops: inTheWayM, or (AskFromTheBody) the line metre fromM falls at carried on to the body's near edge.
struct BodyAskRow
{
    int way;
    float fromM;
    float toM;
    uint flags;
    float inTheWayM;
    uint pad;
};

const uint AskOnFoot = 1u;
const uint AskFromTheBody = 2u;

// What a car on a pass found (rebuild_asks.comp): whether a body stands inside it, where its nose stops short of it
// (float bits), the body's row, and the way it stands on.
const uint AskFound = 0u;
const uint AskInTheWayM = 1u;
const uint AskBody = 2u;
const uint AskOn = 3u;

// A row the host cut after it was laid (LaneOccupancy.TakeFrom, CutHold): where it now ends, and whether it is gone.
struct RowEdit
{
    uint slot;
    float toM;
    uint gone;
    uint pad;
};

// A hold the host cut after it was laid: what it came to and what cut it, `laid` where that is what it was laid to before
// the plans are answered, and otherwise its answer after them.
struct HoldEdit
{
    uint hold;
    uint laid;
    uint pad0;
    uint pad1;
    ClaimAnswer answer;
    ClaimRow cutter;
};

// car_tick.glsl's CarGrantRow, field for field: the rebuild writes every field but the place, which is a decision's.
struct CarGrant
{
    float authorityM;
    float horizonM;
    float claimFromM;
    float committedToM;
    float grantMarginM;
    uint grantCutBy;
    int cutOn;
    float placeReachM;
    vec2 placeM;
};

// CarToldRow's bits that are the claims' (drive_context.glsl's ContextHasAHold, ContextCut).
const uint ToldHasAHold = 8u;
const uint ToldCut = 16u;

float rebuildInfinity() { return uintBitsToFloat(0x7F800000u); }

// What a hold answers before any pass, and what a hold laid where it stands answers every pass (LaneOccupancy's laid
// answer).
ClaimAnswer rebuildLaidAnswer(HoldRow held)
{
    return ClaimAnswer(held.laidCutLineM, held.laidMarginM, held.laidCutBy, held.laidCutOn, rebuildInfinity(), 0.0, 0.0, 0.0);
}

// How far along its holder's line a hold is shown to the others before the first pass: nothing of one answered with the
// plans, and the whole of one laid where it stands (LaneOccupancy.BeginTheAnswers).
float rebuildShownAtFirst(HoldRow held)
{
    return (held.flags & HoldAnswered) != 0u ? -rebuildInfinity() : rebuildInfinity();
}
