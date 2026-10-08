// TownWorld.WalkerTakesOnlyTheClock on the device: whether a walker's decision would do anything but run its leg's clock
// — read off its kept row, which the town checks holds the walker as it does before asking (DeviceWalkers.HoldsAsTheTownHas),
// where its body stands as the bodies' step left it, and its hold as the town's readers read it (WalkerReadersHold).
// Every figure a verdict turns on is the host's to the bit: lengths through walkerLength, no product contracted into a sum.
//
// The including kernel includes walkers_buffers.glsl and defines DECIDE (WalkerDecideFigures).

#include "agents/person/actions/kernels/walker_readers.glsl"

// A walker's leg's clock (LegProgress): how long it has got nowhere, the closest it has come, and the route slot it was
// last run or laid at — a rebuild that walked the walker on to another restarts it, as the town's taking that up does
// (TownWorld.KeptOnTheDevice).
struct WalkerClock
{
    float sinceS;
    float closestM;
    int routeTakenAt;
    uint pad;
};

// LaneOccupancy.RunsOn.
const int DecideRunsOn = -2;

// Which way a walker's route takes after one of its ways (Sidestepping.OnwardAlongTheWalk).
int decideOnwardAlongTheWalk(WalkerRow row, uint walker, int way)
{
    for (int slot = max(0, walkerRouteAt(row)); slot < row.routeCount; slot++)
    {
        if (walkerWayOf(walkerRouteWay(walker, slot)) != way) continue;

        return slot + 1 < row.routeCount ? walkerWayOf(walkerRouteWay(walker, slot + 1)) : WalkerNone;
    }

    return WalkerNone;
}

// LaneClaim.MayBePassedBy, of a holder on no call.
bool decideMayBePassedBy(ClaimRow claim, int onward)
{
    if (!claimHasBody(claim) || claimPassing(claim) || (claim.flags & ClaimStill) == 0u) return false;

    bool makesIt = (claim.flags & ClaimOnItsLine) != 0u && claim.onward != WalkerNone
                   && (claim.onward == onward || claim.onward == DecideRunsOn || onward == DecideRunsOn);
    return !makesIt;
}

// Whether a walker's walk was ended by somebody it may get past, walking a way of its route on its feet
// (Sidestepping.ComesUpToOneItMayPass).
bool decideComesUpToOneItMayPass(WalkerRow row, uint walker, WalkerReadersHold readers)
{
    int code = walkerCurrentRouteWay(row, walker);
    if ((readers.bits & ReadersHasAHold) == 0u || code == WalkerNone || walkIsACorner(code)) return false;
    if (!walkerIs(row, WalkerWalking) || walkerIs(row, WalkerWounded) || walkerIsHopping(row, walker)) return false;

    return (readers.bits & ReadersCut) != 0u && decideMayBePassedBy(readers.cutter, decideOnwardAlongTheWalk(row, walker, readers.cutOn));
}

// Whether a walker has lost the way it was walking (TownWorld.HasLostItsLine).
bool decideHasLostItsLine(WalkerRow row, uint walker)
{
    if (!walkerIs(row, WalkerWalking) || walkerCurrentRouteWay(row, walker) == WalkerNone) return false;

    precise float tolerableM = DECIDE.lostOffM + abs(row.passAsideM);
    return row.offWayM > tolerableM;
}

// Whether a walker's chain ran out of room and its end is coming within its plan's reach (TownWorld.ComesToTheEndOfItsChain).
bool decideComesToTheEndOfItsChain(WalkerRow row, uint walker)
{
    if (!walkerIs(row, WalkerWalking) || !walkerIs(row, WalkerRunsOut)) return false;

    int at = walkerRouteAt(row);
    if (at < 0) return false;

    precise float reachM = (row.radiusM + walkerPlansAheadM()) + DECIDE.walksBeforeTheNextM;
    float walkedM = 0.0;
    for (int slot = at; slot < row.routeCount; slot++)
    {
        float fromM;
        float endM;
        walkerWalkedOfSlot(row, walker, slot, fromM, endM);
        precise float spanM = endM - fromM;
        precise float sumM = walkedM + max(0.0, spanM);
        walkedM = sumM;
        if (walkedM > reachM) return false;
    }

    return true;
}

// How much of a walker's leg is left to walk (TownWorld.RemainingOnTheWalkM).
float decideRemainingOnTheWalkM(WalkerRow row, uint walker, vec2 positionM)
{
    if (walkerCurrentRouteWay(row, walker) == WalkerNone)
    {
        precise vec2 toGoal = row.goalM - positionM;
        return walkerLength(toGoal);
    }

    if (walkerOnTheLastWay(row) && walkerHasWalkedOut(row, row.routeToM))
    {
        precise vec2 toDestination = row.destinationM - positionM;
        return walkerLength(toDestination);
    }

    precise float offM = row.offWayM - WALKER_FIGURES.offLaneM;
    precise float leftM = walkerEndOfTheWayM(row, walker) - row.onWayM;
    precise float remainingM = max(0.0, offM) + max(0.0, leftM);
    return remainingM;
}

// Whether a light holds the crossing in front of a walker (WalkingGround.HeldByALight).
bool decideHeldByALight(WalkerRow row, WalkerReadersHold readers)
{
    if (row.grantM > 0.0) return false;

    return (readers.bits & ReadersCut) != 0u && !claimHasBody(readers.cutter) && claimRoster(readers.cutter) == RosterSignal;
}

// A walker's clock restarted at the route slot its row is at (LegProgress.Restart): nothing reached, no time run up.
WalkerClock decideRestarted(WalkerRow row) { return WalkerClock(0.0, uintBitsToFloat(0x7F7FFFFFu), row.routeTaken, 0u); }

// Whether a walker's decision does anything but run its leg's clock (TownWorld.WalkerTakesOnlyTheClock), and its clock run
// where it does not. A clock that would run out is left as it stood: the town's decision runs it.
bool decideWalkerMoreThanTheClock(WalkerRow row, uint walker, vec2 positionM, WalkerReadersHold readers, inout WalkerClock clock)
{
    if (walkerIs(row, WalkerWounded) || walkerIs(row, WalkerInside) || !walkerIs(row, WalkerWalking)) return true;
    if (row.stage == StageOnDuty) return true;
    if (row.stage == StageWalkingToTheCar)
    {
        precise vec2 toGoal = row.goalM - positionM;
        if (walkerLength(toGoal) <= DECIDE.wayInReachM) return true;
    }

    if (row.action != ActionWalk || decideComesUpToOneItMayPass(row, walker, readers)) return true;
    if (decideHasLostItsLine(row, walker) || decideComesToTheEndOfItsChain(row, walker)) return true;

    float remainingM = decideRemainingOnTheWalkM(row, walker, positionM);
    if (decideHeldByALight(row, readers))
    {
        if (clock.sinceS >= DECIDE.patienceS) return true;

        clock.closestM = min(clock.closestM, remainingM);
        return false;
    }

    precise float nearerThanM = clock.closestM - DECIDE.byM;
    if (remainingM < nearerThanM)
    {
        clock.sinceS = 0.0;
        clock.closestM = remainingM;
        return false;
    }

    precise float runS = clock.sinceS + DECIDE.sinceLastDecisionS;
    if (runS >= DECIDE.patienceS) return true;

    clock.sinceS = runS;
    return false;
}
