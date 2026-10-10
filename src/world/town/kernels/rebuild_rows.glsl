// The rows a claims rebuild is numbered and filed with on the device (TownWorld.RebuildLaneOccupancy, as
// app/devicetick/DeviceRebuild records it), field for field with DeviceRebuild's. The including kernel includes
// claims_rows.glsl first.
//
// Every slot and every hold is numbered as the host's laying numbers it: the pieces each producer laid are counted unit
// by unit in the host's laying order, the counts scanned, and each unit's filed from where its scan says — a unit being
// one walker's body, one car's, one light's stretch, the claims laid where they stand (rebuild_standing.comp), one car's
// claim or one walker's walk, or a run the host still lays itself (HostUnit).

// Which numbering a host-laid unit is filed in: among the bodies, after them (the passes); or among the plans, at a unit
// of their own.
const uint HostPasses = 0u;
const uint HostPlans = 1u;

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

// What the host tells the rebuild of one car (DeviceRebuild.CarClaimRow): whether it was asked this laying, whether its
// claim is the plan the device lays, whether it is laid where it stands, its hold that unit's where it has one, and
// whether its plan is past a pass begun (CarAsked, CarPlansOnTheDevice, CarStands, CarOnAPass); for a claim not the
// device's plan, what the car was left granted before its grant is read (CarFleet.AuthorityM, HorizonM, ClaimFromM,
// CommittedToM, GrantMarginM, GrantCutBy); and, for a car on a pass, the run of BodyAskRows that looks for a body inside
// it, its plan laid as though the pass were clear (Following.AsksOfItsPass).
struct CarClaimRow
{
    uint flags;
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
const uint CarStands = 4u;
const uint CarOnAPass = 8u;

// What rebuild_standing.comp laid, before it is filed: a hold of its own cut by one of its own rows names that row from
// the unit's first (StandingOwnRow); and of each light's stretch, whether it took the piece (StretchTaken) and whether it
// cut the light's hold nearer than it was laid to end (StretchCut) — then where, by which of its rows, on which way.
const uint StandingOwnRow = 0x80000000u;
const uint StretchTaken = 1u;
const uint StretchCut = 2u;

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
