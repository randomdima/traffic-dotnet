// A walker's actions on the device, as a claims rebuild asks them: walking its route (WalkingItsRoute), walking back
// onto it (WalkingBack), walking to a post (WalkingToThePost) and getting past somebody on it (Sidestepping) — the ground
// each walks, where each aims, and the one place an action changes (PersonActions). Line for line with the host over
// walking_ground.glsl, which the including kernel includes first.

// Sidestep's legs (SidestepLeg).
const uint LegAcross = 0u;
const uint LegAlong = 1u;
const uint LegBack = 2u;
const uint LegOver = 3u;

// Where a walker stands on its pass (SidestepPlace).
struct SidestepPlace
{
    float walkedM;
    float acrossM;
    uint leg;
};

bool walkerHasAPass(WalkerRow row) { return row.passAsideM != 0.0; }

// A pass dropped (Sidestep.None).
void walkerDropThePass(inout WalkerRow row)
{
    row.passSlot = 0;
    row.passFromM = 0.0;
    row.passClearM = 0.0;
    row.passAsideM = 0.0;
    row.flags &= ~WalkerPassBegun;
}

// A walker handed over to another action, and the pass its sidestep owned let go (PersonActions.Enter).
void walkerEnter(inout WalkerRow row, uint action)
{
    if (row.action == action) return;

    if (row.action == ActionSidestep) walkerDropThePass(row);
    row.action = action;
}

// PersonActions.SetWalking.
void walkerSetWalking(inout WalkerRow row, bool walking)
{
    row.flags = walking ? row.flags | WalkerWalking : row.flags & ~WalkerWalking;
    walkerEnter(row, walking ? ActionWalk : ActionStand);
}

// PersonActions.WalksItsRoute and WalksOnItsOwnFeet.
bool walkerWalksItsRoute(uint action) { return action == ActionWalk || action == ActionSidestep || action == ActionRejoin; }

bool walkerWalksOnItsOwnFeet(WalkerRow row)
{
    return walkerIs(row, WalkerWalking) && (walkerWalksItsRoute(row.action) || row.action == ActionPost);
}

// One tick of walking the chain: where the body now stands on its way, the next way where it has walked this one out,
// and the hop onto the goal once the chain is (WalkingItsRoute.WalkTheWay). A way taken restarts the walker's progress
// clock, which is the decision's and is read off the slot it moved to.
void walkerWalkTheWay(inout WalkerRow row, uint walker)
{
    if (!walkerIs(row, WalkerWalking) || !walkerWalksItsRoute(row.action)) return;

    if (walkerCurrentRouteWay(row, walker) == WalkerNone)
    {
        row.destinationM = row.positionM;
        return;
    }

    float strideM = walkerAStrideM();
    walkerPlaceItOnItsWay(row, walker, strideM);
    while (walkerHasWalkedOut(row, walkerEndOfTheWayM(row, walker)) && !walkerOnTheLastWay(row))
    {
        int before = walkerCurrentRouteWay(row, walker);
        int next;
        if (!walkerTakeNextRouteWay(row, walker, next)) break;

        float fromM;
        float toM;
        walkSpanOfWay(before, next, walkerPeekNextRouteWay(row, walker), fromM, toM);
        row.onWayM = fromM;
        walkerPlaceItOnItsWay(row, walker, strideM);
    }

    float stopsAtM = walkerEndOfTheWayM(row, walker);
    if (!walkerOnTheLastWay(row) || !walkerHasWalkedOut(row, stopsAtM)) return;

    vec2 endsAtM = row.goalM;
    if (walkerIs(row, WalkerRunsOut) && !walkerEndOfTheWalk(row, walker, stopsAtM, endsAtM)) endsAtM = row.positionM;

    row.destinationM = endsAtM;
    precise vec2 leftM = endsAtM - row.positionM;
    if (walkerLength(leftM) <= row.radiusM) walkerSetWalking(row, false);
}

// Whether a walker's body stands on any of the pavement, its own way or not (WalkingBack.StandsOnThePavement).
bool walkerStandsOnThePavement(WalkerRow row)
{
    AtlasCover under[AtlasMostWaysUnderABody];
    uint dropped = 0u;
    int count = atlasUnderDisc(row.positionM, row.radiusM, WALKER_FIGURES.groundChannel, under, dropped);
    for (int at = 0; at < count; at++)
    {
        if (under[at].way >= WALKING_FIGURES.firstFootwayWay) return true;
    }

    return false;
}

// Begun or ended by where the walker stands: off the pavement altogether it walks back onto its way, to where it
// decides then (WalkingBack.Consider, BackOntoItsWayM).
void walkerConsiderWalkingBack(inout WalkerRow row, uint walker)
{
    if ((row.action != ActionWalk && row.action != ActionRejoin) || !walkerIs(row, WalkerWalking)) return;

    int way = walkerCurrentRouteWay(row, walker);
    bool onIt = row.onWay != WalkerNone || walkerIsHopping(row, walker) || way == WalkerNone || walkerStandsOnThePavement(row);
    if (!onIt && row.action != ActionRejoin)
    {
        float headingRad;
        walkSampleAt(walkWay(way), row.onWayM, row.backToM, headingRad);
    }

    walkerEnter(row, onIt ? ActionWalk : ActionRejoin);
}

// Whether a walker is still walking the route its pass was asked for on, and how far down it it has come since
// (Sidestepping.IsStillOnItsSidestep).
bool walkerIsStillOnItsSidestep(WalkerRow row, uint walker, out float walkedM)
{
    walkedM = 0.0;
    if (!walkerHasAWalkToPlan(row, walker) || walkerIs(row, WalkerInside) || walkerRouteAt(row) < row.passSlot) return false;

    walkedM = walkerWalkedSince(row, walker, row.passSlot, row.passFromM);
    return true;
}

// Which leg of the pass a walker is on (Sidestep.LegAt).
uint walkerLegAt(WalkerRow row, float walkedM, float acrossM, float alongWithinM, float acrossWithinM)
{
    precise float clearFromM = row.passClearM - alongWithinM;
    if (walkedM >= clearFromM) return abs(acrossM) <= acrossWithinM ? LegOver : LegBack;

    precise float apartM = row.passAsideM - acrossM;
    return abs(apartM) <= acrossWithinM ? LegAlong : LegAcross;
}

// Where a walker stands on its pass (Sidestepping.WhereOnItsSidestep).
bool walkerWhereOnItsSidestep(WalkerRow row, uint walker, out SidestepPlace place)
{
    place = SidestepPlace(0.0, 0.0, LegAcross);
    float walkedM;
    vec2 hereM;
    float headingRad;
    if (!walkerIsStillOnItsSidestep(row, walker, walkedM) || !walkerOnTheWalkAt(row, walker, 0.0, hereM, headingRad)) return false;

    precise vec2 offM = row.positionM - hereM;
    float acrossM = walkerDot(offM, walkerRightOf(headingRad));
    place = SidestepPlace(walkedM, acrossM, walkerLegAt(row, walkedM, acrossM, WALKER_FIGURES.stepM, row.radiusM));
    return true;
}

// How far down its walk a walker's plan begins: the front of its body, or past the end of its pass
// (Sidestepping.WalkPlannedFromM).
float walkerWalkPlannedFromM(WalkerRow row, uint walker)
{
    float frontM = row.radiusM;
    float walkedM;
    if (!walkerIs(row, WalkerPassBegun) || !walkerIsStillOnItsSidestep(row, walker, walkedM)) return frontM;

    precise float pastM = row.passClearM - walkedM + frontM;
    return max(frontM, pastM);
}

// A stride of the walker's own, which a run of its pass is given back once it has walked past by (Sidestepping.AStrideM).
float walkerBehindItsPassM(WalkerRow row, float walkedM)
{
    precise float strideM = row.radiusM * 2.0;
    precise float behindM = walkedM - strideM;
    return behindM;
}

// The ground a walker's pass will cover, laid as passes at BODY_COVER[into ..] — or none, and the walker walking again,
// where it is no longer on its sidestep (Sidestepping.Lay); how many.
uint walkerLayTheSidestep(inout WalkerRow row, uint walker, int occupant, uint roster, uint into)
{
    if (row.action != ActionSidestep || !walkerHasAPass(row)) return 0u;

    SidestepPlace place;
    if (!walkerWhereOnItsSidestep(row, walker, place))
    {
        walkerEnter(row, ActionWalk);
        return 0u;
    }

    float behindM = walkerBehindItsPassM(row, place.walkedM);
    uint flags = RungHard | (roster << ClaimRosterShift) | ClaimPassing;
    uint written = 0u;
    uint first = walker * WALKER_FIGURES.passRoom;
    for (uint at = 0u; at < row.passRuns; at++)
    {
        SweptRun run = PASS_RUNS[first + at];
        if (run.lastAtM < behindM) continue;

        BODY_COVER[into + written] = ClaimRow(
            run.fromM, run.toM, 0.0, 0.0, 0.0, -walkerInfinity(), run.fromM, occupant, WalkerNone, OnwardNoWay, uint(run.way), flags);
        written++;
    }

    return written;
}

// Whether somebody stands inside what is left of a walker's pass, and which body on which way
// (Sidestepping.TheBodyInTheSidestep).
bool walkerTheBodyInTheSidestep(WalkerRow row, uint walker, int occupant, uint roster, out uint body, out int on)
{
    body = NoRow;
    on = WalkerNone;
    SidestepPlace place;
    if (!walkerWhereOnItsSidestep(row, walker, place)) return false;

    float behindM = walkerBehindItsPassM(row, place.walkedM);
    uint first = walker * WALKER_FIGURES.passRoom;
    for (uint at = 0u; at < row.passRuns; at++)
    {
        SweptRun run = PASS_RUNS[first + at];
        if (run.lastAtM < behindM ||!claimsAheadBody(uint(run.way), run.fromM, run.toM, occupant, roster, body)) continue;

        on = run.way;
        return true;
    }

    return false;
}

// A pass laid this rebuild kept where it still has its ground to itself, and withdrawn otherwise
// (Sidestepping.KeepOrWithdrawTheSidestep).
void walkerKeepOrWithdrawTheSidestep(inout WalkerRow row, uint walker, int occupant, uint roster)
{
    uint first = walker * WALKER_FIGURES.passRoom;
    for (uint at = 0u; at < row.passRuns; at++)
    {
        SweptRun run = PASS_RUNS[first + at];
        if (claimsKeepsItsPass(uint(run.way), run.fromM, run.toM, occupant, roster)) continue;

        walkerDropThePass(row);
        return;
    }

    row.flags |= WalkerPassBegun;
}

// This rebuild's word on a walker's pass: over once it is back on its route past the one it passed, and begun or
// withdrawn in the rebuild after it was asked for (Sidestepping.Consider).
void walkerConsiderTheSidestep(inout WalkerRow row, uint walker, int occupant, uint roster)
{
    if (row.action != ActionSidestep) return;

    if (walkerIs(row, WalkerPassBegun))
    {
        SidestepPlace place;
        if (!walkerWhereOnItsSidestep(row, walker, place) || place.leg != LegOver) return;

        walkerEnter(row, ActionWalk);
        return;
    }

    if (walkerHasAPass(row)) walkerKeepOrWithdrawTheSidestep(row, walker, occupant, roster);
}

// Where a walker on a pass aims: across onto the lane beside, a stride down it, and back onto its route; false once
// the pass is over (Sidestepping.Aim).
bool walkerAimTheSidestep(inout WalkerRow row, uint walker)
{
    SidestepPlace place;
    if (!walkerIs(row, WalkerPassBegun) || !walkerWhereOnItsSidestep(row, walker, place)) return false;

    vec2 atM;
    float headingRad;
    if (place.leg == LegAcross || place.leg == LegBack)
    {
        if (!walkerOnTheWalkAt(row, walker, 0.0, atM, headingRad)) return false;

        precise vec2 asideM = atM + (walkerRightOf(headingRad) * row.passAsideM);
        row.destinationM = place.leg == LegAcross ? asideM : atM;
        return true;
    }

    if (place.leg != LegAlong) return false;

    precise float clearM = row.passClearM - place.walkedM;
    float strideM = min(min(WALKER_FIGURES.walkAheadM, row.grantM), clearM);
    if (!walkerOnTheWalkAt(row, walker, max(0.0, strideM), atM, headingRad)) return false;

    precise vec2 asideM = atM + (walkerRightOf(headingRad) * row.passAsideM);
    row.destinationM = asideM;
    return true;
}

// The ways a walker's plan is laid down from the front of its body, down its route or over the hop off the end of it
// (WalkingItsRoute.WaysOf); how many.
int walkerWaysOfTheRoute(inout WalkerRow row, uint walker, out WalkerWay into[WalkerMostWays])
{
    if (walkerIs(row, WalkerInside) || !walkerHasAWalkToPlan(row, walker)) return 0;

    float frontM = walkerWalkPlannedFromM(row, walker);
    if (walkerIsHopping(row, walker))
    {
        return walkerTheStraightAhead(row, walker, row.destinationM, frontM, walkerPlansAheadM(), into);
    }

    precise float aheadM = frontM + walkerPlansAheadM();
    int count = walkerWaysAlongTheWalk(row, walker, aheadM, into);
    int first = 0;
    while (first < count)
    {
        precise float endsM = into[first].lineFromM + (into[first].toM - into[first].fromM);
        if (endsM > frontM) break;

        first++;
    }

    if (first == count) return 0;

    if (into[first].lineFromM < frontM)
    {
        precise float fromM = into[first].fromM + (frontM - into[first].lineFromM);
        into[first].fromM = fromM;
        into[first].lineFromM = frontM;
    }

    for (int at = first; at < count; at++) into[at - first] = into[at];
    return count - first;
}

// Where a walker on its way aims, on the grant this rebuild gave it (WalkingItsRoute.Aim).
void walkerAimDownTheRoute(inout WalkerRow row, uint walker)
{
    int way = walkerCurrentRouteWay(row, walker);
    if (way == WalkerNone) return;

    if (row.grantM <= 0.0)
    {
        row.destinationM = row.positionM;
        return;
    }

    if (walkerIsHopping(row, walker))
    {
        precise vec2 towards = row.destinationM - row.positionM;
        float reachM = row.grantM;
        precise float reachSquaredM2 = reachM * reachM;
        if (walkerDot(towards, towards) > reachSquaredM2)
        {
            precise vec2 aimM = row.positionM + (walkerOver(towards, walkerLength(towards)) * reachM);
            row.destinationM = aimM;
        }

        return;
    }

    if (walkerAimTheSidestep(row, walker)) return;

    float strideM = min(WALKER_FIGURES.walkAheadM, row.grantM);
    precise float strideOnM = row.onWayM + strideM;
    float aheadM = min(strideOnM, walkerEndOfTheWayM(row, walker));
    float headingRad;
    walkSampleAt(walkWay(way), aheadM, row.destinationM, headingRad);
}
