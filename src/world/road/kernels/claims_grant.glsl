// What a car reads off what its hold came to (DrivingGround.ReadTheGrant, KindOf), over values alone: no buffer of the
// claims is read here. The including kernel includes claims_rows.glsl first.

// HeadwayKind, by number.
const uint HeadwayNothing = 0u;
const uint HeadwayQueue = 1u;
const uint HeadwayObstruction = 2u;
const uint HeadwayClaimed = 3u;
const uint HeadwayLight = 4u;
const uint HeadwayWalker = 5u;
const uint HeadwayPassing = 6u;

// What a claim that ended a grant is to the driver it ended (DrivingGround.KindOf).
uint claimsKindOfRow(ClaimRow claim)
{
    if (!claimHasBody(claim)) return claimRoster(claim) == RosterSignal ? HeadwayLight : HeadwayClaimed;
    if (claimPassing(claim)) return HeadwayPassing;
    if (claimRoster(claim) == RosterWalking) return HeadwayWalker;
    return (claim.flags & ClaimOnItsLine) != 0u ? HeadwayQueue : HeadwayObstruction;
}

// What a car got off its plan (DrivingGround.ReadTheGrant): how far past its nose the plan survived less what it keeps off
// what cut it — infinity where nothing did — the margin, and what cut it, `cutter` being the row the answer names where
// it names one. A hold nothing cut leaves the margin and the cutter as the claim's clearing left them.
vec4 claimsGrantOf(ClaimAnswer answer, ClaimRow cutter, float claimFromM, float clearedMarginM, uint clearedKind)
{
    if (isinf(answer.cutLineM) && answer.cutLineM > 0.0) return vec4(answer.cutLineM, clearedMarginM, float(clearedKind), 0.0);

    precise float authorityM = answer.cutLineM - answer.marginM - claimFromM;
    uint kind = answer.cutBy == NoRow ? HeadwayClaimed : claimsKindOfRow(cutter);
    return vec4(authorityM, answer.marginM, float(kind), 0.0);
}
