// The walkers' side of a claims rebuild on the device (TownWorld.RebuildLaneOccupancy, StationTheWalker,
// LayTheWalkersBody, GroundHeldAs, PlanTheWalk, WaysOfTheWalk, AimTheWalker): every walker stationed on its walk, its
// body laid, its walk planned and claimed, and once the plans are answered its grant read, its pass considered and its
// aim set — each a walker's own, one thread a walker. The including kernel includes walking_ground.glsl and
// walker_actions.glsl first.

// What one walker's walk came to (TownWorld.PlanTheWalk): its hold — laidAHold where it laid one — the rows it laid,
// and how many ways its action planned it down, or -1 where it planned none.
struct WalkersPlan
{
    HoldRow hold;
    uint rows;
    int ways;
    uint laidAHold;
};

// Whose ground a walker's is: its own, the car it walks to or from (PER-29), or for an officer on duty the car they are
// put out of (SRV-11) (TownWorld.GroundHeldAs). dutyCar is the first car whose officer the walker is, or none.
int walkersGroundHeldAs(WalkerRow row, uint walker, out uint roster)
{
    roster = RosterWalking;
    if (walkerIs(row, WalkerInside)) return int(walker);

    if ((row.stage == StageWalkingToTheCar || row.stage == StageWalkingFromTheCar) && row.car != WalkerNone)
    {
        roster = RosterDriving;
        return row.car;
    }

    if (row.stage != StageOnDuty || row.dutyCar == WalkerNone) return int(walker);

    roster = RosterDriving;
    return row.dutyCar;
}

// Whose ground the walker's body was laid as this rebuild (WalkingGround.HeldAs).
int walkersHeldAs(WalkerRow row, out uint roster)
{
    roster = walkerIs(row, WalkerHeldAsDriving) ? RosterDriving : RosterWalking;
    return row.heldAs;
}

// Where a walker stands on its walk, worked out before anything is laid (TownWorld.StationTheWalker).
void walkersStation(inout WalkerRow row, uint walker)
{
    row.onWay = WalkerNone;
    if (walkerIs(row, WalkerInside)) return;

    walkerWalkTheWay(row, walker);
    int way;
    if (walkerIsAfoot(row, walker, way)) row.onWay = way;
    walkerConsiderWalkingBack(row, walker);
}

// A walker where its centre stands, held as whoever the town holds its ground as, at BODY_COVER[into ..]
// (TownWorld.LayTheWalkersBody); how many rows. Whose ground it is is held to for an inside walker too, whose body lays
// nothing, so its walk reads it rather than asking again.
uint walkersLayTheBody(inout WalkerRow row, uint walker, uint into)
{
    uint roster;
    int occupant = walkersGroundHeldAs(row, walker, roster);
    row.heldAs = occupant;
    row.flags = roster == RosterDriving ? row.flags | WalkerHeldAsDriving : row.flags & ~WalkerHeldAsDriving;
    if (walkerIs(row, WalkerInside)) return 0u;

    return walkerBodyCover(row, walker, occupant, roster, into);
}

// The ground a walker's action walks, nearest first (TownWorld.WaysOfTheWalk; WalkingBack.WaysOf,
// WalkingToThePost.WaysOf, WalkingItsRoute.WaysOf).
int walkersWaysOfTheWalk(inout WalkerRow row, uint walker, out WalkerWay into[WalkerMostWays])
{
    if (row.action == ActionRejoin) return walkerTheStraightAhead(row, walker, row.backToM, row.radiusM, walkerPlansAheadM(), into);
    if (row.action == ActionPost) return walkerTheStraightAhead(row, walker, row.goalM, row.radiusM, walkerPlansAheadM(), into);

    return walkerWaysOfTheRoute(row, walker, into);
}

// The walk in front of a walker claimed by the action it is doing, as hold `hold` with its rows at
// CLAIMS_PROPOSED[into ..] (TownWorld.PlanTheWalk; WalkingGround.Claim, HoldShortOf): a walker on a pass with a body
// inside it held where it stands, and every other walker on its own feet laying its walk's ways whole.
WalkersPlan walkersPlanTheWalk(inout WalkerRow row, uint walker, int hold, uint into, out WalkerWay ways[WalkerMostWays])
{
    float gapM = WALKER_FIGURES.standstillGapM;
    WalkersPlan plan = WalkersPlan(
        HoldRow(into, into, gapM, 0u, gapM, -walkerInfinity(), walkerInfinity(), 0.0, NoRow, WalkerNone, 0.0, 0.0), 0u, -1, 0u);
    row.hold = WalkerNone;
    if (!walkerWalksOnItsOwnFeet(row)) return plan;

    row.hold = hold;
    plan.laidAHold = 1u;
    if (walkerIs(row, WalkerPassBegun))
    {
        uint heldRoster;
        int heldAs = walkersHeldAs(row, heldRoster);
        uint body;
        int on;
        if (walkerTheBodyInTheSidestep(row, walker, heldAs, heldRoster, body, on))
        {
            plan.hold.laidCutLineM = row.radiusM;
            plan.hold.laidMarginM = gapM;
            plan.hold.laidCutBy = body;
            plan.hold.laidCutOn = on;
            return plan;
        }
    }

    plan.ways = walkersWaysOfTheWalk(row, walker, ways);
    uint roster;
    int occupant = walkersHeldAs(row, roster);
    plan.rows = walkerClaim(row, occupant, roster, hold, ways, plan.ways, into);
    plan.hold.endRow = into + plan.rows;
    if (plan.ways > 0) plan.hold.flags |= HoldAnswered;
    return plan;
}

// Where a walker aims on the grant this rebuild gave it, as the action it is doing has it (TownWorld.AimTheWalker).
void walkersAimTheWalker(inout WalkerRow row, uint walker)
{
    if (!walkerIs(row, WalkerWalking)) return;

    if (row.action == ActionPost) walkerAimAlongTheStraight(row, row.goalM);
    else if (row.action == ActionRejoin) walkerAimAlongTheStraight(row, row.backToM);
    else if (row.action == ActionWalk || row.action == ActionSidestep) walkerAimDownTheRoute(row, walker);
}

// What a walker reads of its answer: its grant, its pass's word and where it aims (WalkingGround.ReadTheGrant,
// Sidestepping.Consider, TownWorld.AimTheWalker). answer is what its hold came to.
void walkersReadTheAnswer(inout WalkerRow row, uint walker, ClaimAnswer answer)
{
    walkerReadTheGrant(row, walker, answer);
    uint roster;
    int occupant = walkersHeldAs(row, roster);
    walkerConsiderTheSidestep(row, walker, occupant, roster);
    walkersAimTheWalker(row, walker);
}
