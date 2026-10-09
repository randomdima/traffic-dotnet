// WalkingGround on the device: a walker's place on its route and the walk ahead of it — the ways under a stretch of it,
// where it is a distance down it, how far it has come — the straight it walks off the network, the ground its body
// stands on, the claim it lays over its walk and the grant that comes back. Line for line with the host, `precise`
// wherever the host rounds and the quotients and roots rounded.glsl's; a point on a bent way, where a walker is found on
// one and which way it faces are trig.glsl's sine, cosine and arctangent.
//
// A walker is a WalkerRow; its route is WALKER_FIGURES.routeRoom ways of WALK_ROUTES from walker × routeRoom, its
// straight's runs WALKER_FIGURES.straightRoom of SWEPT_RUNS, and its pass's runs WALKER_FIGURES.passRoom of PASS_RUNS.
// The including kernel supplies WALKER_FIGURES' fields routeRoom, straightRoom, passRoom, stepM, standstillGapM,
// walkAheadM, offLaneM and groundChannel, declares WALK_ROUTES (int[]), SWEPT_RUNS and PASS_RUNS (SweptRun[]),
// MARK_AT and MARK_SECTIONS (claims_propose.glsl's) and BODY_COVER (ClaimRow[]), and includes walker_rows.glsl,
// rounded.glsl, arc.glsl, arc_chain.glsl, walking.glsl, atlas.glsl, swept.glsl, claims_rows.glsl, claims_questions.glsl
// and claims_propose.glsl first.

#include "core/geometry/kernels/trig.glsl"

bool walkerIs(WalkerRow walker, uint flag) { return (walker.flags & flag) != 0u; }

float walkerInfinity() { return uintBitsToFloat(0x7F800000u); }

float walkerLength(vec2 v)
{
    precise float squared = v.x * v.x + v.y * v.y;
    return roundedSqrt(squared);
}

float walkerDot(vec2 a, vec2 b)
{
    precise float d = a.x * b.x + a.y * b.y;
    return d;
}

vec2 walkerOver(vec2 v, float by) { return vec2(roundedQuotient(v.x, by), roundedQuotient(v.y, by)); }

// Heading.Unit and RightOf, to trig.glsl's cosine and sine.
vec2 walkerUnit(float headingRad) { return trigUnit(headingRad); }

vec2 walkerRightOf(float headingRad)
{
    vec2 unit = walkerUnit(headingRad);
    return vec2(-unit.y, unit.x);
}

// How far in front of itself a walker plans, and a stride either side of its line (WalkingGround.PlansAheadM, AStrideM).
float walkerPlansAheadM()
{
    precise float m = WALKER_FIGURES.stepM + WALKER_FIGURES.standstillGapM;
    return m;
}

float walkerAStrideM()
{
    precise float m = WALKER_FIGURES.walkAheadM + WALKER_FIGURES.offLaneM;
    return m;
}

// The route (PersonFleet.RouteOf, CurrentRouteWay, RouteAt, PeekNextRouteWay, RouteWayBefore, TakeNextRouteWay,
// OnTheLastWay).
int walkerRouteWay(uint walker, int slot) { return WALK_ROUTES[walker * WALKER_FIGURES.routeRoom + uint(slot)]; }

int walkerCurrentRouteWay(WalkerRow row, uint walker)
{
    return row.routeTaken < 1 ? WalkerNone : walkerRouteWay(walker, row.routeTaken - 1);
}

int walkerRouteAt(WalkerRow row) { return row.routeTaken - 1; }

int walkerPeekNextRouteWay(WalkerRow row, uint walker)
{
    return row.routeTaken >= row.routeCount ? WalkerNone : walkerRouteWay(walker, row.routeTaken);
}

int walkerRouteWayBefore(WalkerRow row, uint walker)
{
    return row.routeTaken < 2 ? WalkerNone : walkerRouteWay(walker, row.routeTaken - 2);
}

bool walkerOnTheLastWay(WalkerRow row) { return row.routeTaken >= row.routeCount; }

bool walkerTakeNextRouteWay(inout WalkerRow row, uint walker, out int way)
{
    if (row.routeTaken >= row.routeCount)
    {
        way = WalkerNone;
        return false;
    }

    way = walkerRouteWay(walker, row.routeTaken);
    row.routeTaken++;
    return true;
}

// The town's way for a way of a route chain, or none for the hop off the network (WalkingGround.WayOf).
int walkerWayOf(int code)
{
    return code == WalkerRouteNoWay || code == WalkNoLane ? WalkerNone : walkTownWay(code);
}

// How fast the walker is going the way it faces (WalkingGround.AlongItsWalkMps): the host's own, sent with the row, since a
// heading's unit from the device's trigonometry puts the pace on every row and plan a last bit elsewhere.
float walkerAlongItsWalkMps(WalkerRow row) { return row.alongMps; }

// WalkingGround.HasWalkedOut.
bool walkerHasWalkedOut(WalkerRow row, float endM)
{
    precise float withinM = endM - WALKER_FIGURES.stepM;
    return row.onWayM >= withinM;
}

// WalkingGround.EndOfTheWayM.
float walkerEndOfTheWayM(WalkerRow row, uint walker)
{
    if (walkerOnTheLastWay(row)) return row.routeToM;

    float fromM;
    float endM;
    walkSpanOfWay(
        walkerRouteWayBefore(row, walker), walkerCurrentRouteWay(row, walker), walkerPeekNextRouteWay(row, walker), fromM,
        endM);
    return endM;
}

// WalkingGround.IsHopping.
bool walkerIsHopping(WalkerRow row, uint walker)
{
    return walkerOnTheLastWay(row) && walkerHasWalkedOut(row, walkerEndOfTheWayM(row, walker));
}

// WalkingGround.IsOnItsWay.
bool walkerIsOnItsWay(WalkerRow row, uint walker)
{
    int way = walkerCurrentRouteWay(row, walker);
    if (way == WalkerNone) return false;

    precise float halfM = walkWay(way).widthM * 0.5;
    return row.offWayM <= halfM;
}

// WalkingGround.HasAWalkToPlan.
bool walkerHasAWalkToPlan(WalkerRow row, uint walker)
{
    return walkerIs(row, WalkerWalking) && !walkerIs(row, WalkerWounded) && row.action != ActionHand
        && walkerWayOf(walkerCurrentRouteWay(row, walker)) != WalkerNone;
}

// WalkingGround.IsAfoot.
bool walkerIsAfoot(WalkerRow row, uint walker, out int way)
{
    way = WalkerNone;
    if (!walkerHasAWalkToPlan(row, walker) || !walkerIsOnItsWay(row, walker)) return false;

    way = walkerWayOf(walkerCurrentRouteWay(row, walker));
    return true;
}

// Where on the way it is walking the body now stands, and how far off it (WalkingGround.PlaceItOnItsWay).
void walkerPlaceItOnItsWay(inout WalkerRow row, uint walker, float windowM)
{
    WalkWayRow way = walkWay(walkerCurrentRouteWay(row, walker));
    if (way.arcEnd == way.arcFirst)
    {
        row.offWayM = 0.0;
        return;
    }

    float offSq;
    float alongM = splineProject(way.arcFirst, way.arcEnd, row.positionM, row.onWayM, windowM, offSq);
    row.onWayM = alongM;

    vec2 atM;
    float headingRad;
    walkSampleAt(way, alongM, atM, headingRad);
    precise vec2 offM = atM - row.positionM;
    row.offWayM = walkerLength(offM);
}

// The stretch of one way of the chain the walk still covers (WalkingGround.WalkedOfSlot).
void walkerWalkedOfSlot(WalkerRow row, uint walker, int slot, out float fromM, out float endM)
{
    walkSpanOfWay(
        slot > 0 ? walkerRouteWay(walker, slot - 1) : WalkNoLane, walkerRouteWay(walker, slot),
        slot + 1 < row.routeCount ? walkerRouteWay(walker, slot + 1) : WalkNoLane, fromM, endM);

    if (slot == row.routeCount - 1) endM = min(endM, row.routeToM);
    if (slot == walkerRouteAt(row)) fromM = max(fromM, row.onWayM);
}

// The ways under the walk from the body's own place to aheadM in front of it (WalkingGround.WaysAlongTheWalk).
int walkerWaysAlongTheWalk(WalkerRow row, uint walker, float aheadM, out WalkerWay into[WalkerMostWays])
{
    int at = walkerRouteAt(row);
    if (at < 0 || at >= row.routeCount) return 0;

    int written = 0;
    float walkedM = 0.0;
    for (int slot = at; slot < row.routeCount && walkedM < aheadM && written < WalkerMostWays; slot++)
    {
        int townWay = walkerWayOf(walkerRouteWay(walker, slot));
        if (townWay == WalkerNone) break;

        float fromM;
        float endM;
        walkerWalkedOfSlot(row, walker, slot, fromM, endM);
        if (endM <= fromM) continue;

        precise float lengthM = endM - fromM;
        precise float leftM = aheadM - walkedM;
        precise float toM = fromM + min(lengthM, leftM);
        into[written] = WalkerWay(townWay, fromM, toM, walkedM);
        written++;
        precise float walked = walkedM + lengthM;
        walkedM = walked;
    }

    return written;
}

// How far down its route a walker has come from a place on it (WalkingGround.WalkedSince).
float walkerWalkedSince(WalkerRow row, uint walker, int slot, float fromM)
{
    int at = walkerRouteAt(row);
    if (at == slot)
    {
        precise float sinceM = row.onWayM - fromM;
        return sinceM;
    }

    float walkedM = -fromM;
    for (int way = slot; way < at; way++)
    {
        float spanFromM;
        float spanToM;
        walkSpanOfWay(
            way > 0 ? walkerRouteWay(walker, way - 1) : WalkNoLane, walkerRouteWay(walker, way),
            way + 1 < row.routeCount ? walkerRouteWay(walker, way + 1) : WalkNoLane, spanFromM, spanToM);
        precise float spanM = spanToM - (way == slot ? 0.0 : spanFromM);
        precise float walked = walkedM + spanM;
        walkedM = walked;
    }

    float onFromM;
    float onToM;
    walkSpanOfWay(
        walkerRouteWay(walker, at - 1), walkerRouteWay(walker, at),
        at + 1 < row.routeCount ? walkerRouteWay(walker, at + 1) : WalkNoLane, onFromM, onToM);
    precise float sinceM = walkedM + row.onWayM - onFromM;
    return sinceM;
}

// Where the walk is a distance down it from the body's own place (WalkingGround.OnTheWalkAt).
bool walkerOnTheWalkAt(WalkerRow row, uint walker, float aheadM, out vec2 positionM, out float headingRad)
{
    positionM = vec2(0.0);
    headingRad = 0.0;
    WalkerWay pieces[WalkerMostWays];
    precise float reachM = aheadM + WALKER_FIGURES.stepM;
    int count = walkerWaysAlongTheWalk(row, walker, reachM, pieces);
    for (int index = 0; index < count; index++)
    {
        WalkerWay piece = pieces[index];
        precise float endsM = piece.lineFromM + (piece.toM - piece.fromM);
        if (aheadM > endsM) continue;

        precise float atM = piece.fromM + max(0.0, aheadM - piece.lineFromM);
        walkSampleAt(walkWayOfTown(piece.way), atM, positionM, headingRad);
        return true;
    }

    return false;
}

// Whether the walker walks the straight it committed to towards toM (WalkingGround.IsOnTheStraightTo): the same place,
// and its body within a stride of the line.
bool walkerIsOnTheStraightTo(WalkerRow row, vec2 toM)
{
    if (!walkerIs(row, WalkerOnAStraight) || !all(equal(row.straightToM, toM))) return false;

    vec2 fromM = row.straightFromM;
    precise vec2 line = toM - fromM;
    float lengthSquaredM2 = walkerDot(line, line);
    float shareOf = 0.0;
    if (lengthSquaredM2 > 0.0)
    {
        precise vec2 offM = row.positionM - fromM;
        shareOf = clamp(roundedQuotient(walkerDot(offM, line), lengthSquaredM2), 0.0, 1.0);
    }

    precise vec2 nearestM = fromM + (line * shareOf);
    precise vec2 apartM = row.positionM - nearestM;
    return walkerLength(apartM) <= walkerAStrideM();
}

// The ground a body walking straight from one place to another covers, into atlasFound (WalkingGround.UnderTheStretch).
int walkerUnderTheStretch(vec2 fromM, vec2 toM, float halfWidthM, inout uint dropped)
{
    precise vec2 along = toM - fromM;
    float lengthM = walkerLength(along);
    if (lengthM <= 0.0) return atlasUnderDisc(fromM, halfWidthM, WALKER_FIGURES.groundChannel, dropped);

    precise vec2 centreM = (fromM + toM) * 0.5;
    precise float halfLengthM = (lengthM * 0.5) + halfWidthM;
    return atlasUnderBox(centreM, walkerOver(along, lengthM), halfLengthM, halfWidthM, WALKER_FIGURES.groundChannel, dropped);
}

// A straight committed to from where the walker stands to toM, the ground under it swept once
// (WalkingGround.CommitTheStraight). What does not fit is planned no further.
void walkerCommitTheStraight(inout WalkerRow row, uint walker, vec2 toM)
{
    vec2 fromM = row.positionM;
    row.straightFromM = fromM;
    row.straightToM = toM;
    row.straightRuns = 0u;
    row.flags |= WalkerOnAStraight;
    float stationAtM = -walkerInfinity();

    precise vec2 line = toM - fromM;
    float lengthM = walkerLength(line);
    vec2 forward = lengthM > 0.0 ? walkerOver(line, lengthM) : vec2(0.0);
    float radiusM = row.radiusM;
    precise float strideM = radiusM * 2.0;
    precise float spanM = strideM * 2.0;
    uint first = walker * WALKER_FIGURES.straightRoom;
    for (float atM = 0.0; ; )
    {
        precise float nextM = atM + strideM;
        float toStationM = min(lengthM, nextM);
        precise vec2 stationM = fromM + (forward * atM);
        precise vec2 toStation = fromM + (forward * toStationM);
        uint dropped = 0u;
        int count = walkerUnderTheStretch(stationM, toStation, radiusM, dropped);
        uint runs = row.straightRuns;
        bool fits = sweptStation(first, runs, WALKER_FIGURES.straightRoom, stationAtM, atM, spanM, count);
        row.straightRuns = runs;
        if (!fits || toStationM >= lengthM) return;

        atM = nextM;
    }
}

// The ways under a straight walk from the front of the body towards toM, as far as it would take to come to rest,
// nearest first, read off the ground the straight was swept over (WalkingGround.TheStraightAhead).
int walkerTheStraightAhead(inout WalkerRow row, uint walker, vec2 toM, float frontM, float aheadM, out WalkerWay into[WalkerMostWays])
{
    if (!walkerIsOnTheStraightTo(row, toM)) walkerCommitTheStraight(row, walker, toM);

    vec2 fromM = row.straightFromM;
    precise vec2 line = toM - fromM;
    float lengthM = walkerLength(line);
    precise vec2 offM = row.positionM - fromM;
    float alongM = lengthM > 0.0 ? walkerDot(offM, walkerOver(line, lengthM)) : 0.0;
    precise float nearM = alongM + frontM;
    precise float reachM = nearM + aheadM;
    float farM = min(lengthM, reachM);
    if (farM <= nearM) return 0;

    precise float strideM = row.radiusM * 2.0;
    int written = 0;
    uint first = walker * WALKER_FIGURES.straightRoom;
    for (uint at = 0u; at < row.straightRuns; at++)
    {
        SweptRun run = SWEPT_RUNS[first + at];
        if (run.firstAtM > farM || written == WalkerMostWays) break;

        precise float lastReachM = run.lastAtM + strideM;
        if (lastReachM < nearM) continue;

        precise float lineFromM = run.firstAtM - alongM;
        into[written] = WalkerWay(run.way, run.fromM, run.toM, max(frontM, lineFromM));
        written++;
    }

    return written;
}

// A walker walking straight at a place aims at it, and no further along the straight than it was granted
// (WalkingGround.AimAlongTheStraight).
void walkerAimAlongTheStraight(inout WalkerRow row, vec2 toM)
{
    precise vec2 towards = toM - row.positionM;
    float reachM = max(0.0, row.grantM);
    precise float reachSquaredM2 = reachM * reachM;
    if (walkerDot(towards, towards) > reachSquaredM2)
    {
        precise vec2 aimM = row.positionM + (walkerOver(towards, walkerLength(towards)) * reachM);
        row.destinationM = aimM;
        return;
    }

    row.destinationM = toM;
}

// Where a walk down the route ends, stopsAtM along its last way — or the end of the stretch before a last corner of no
// length (WalkingNetwork.EndOfTheWalk over PersonFleet.RouteOf).
bool walkerEndOfTheWalk(WalkerRow row, uint walker, float stopsAtM, out vec2 atM)
{
    for (int slot = row.routeCount - 1; slot >= 0; slot--)
    {
        WalkWayRow way = walkWay(walkerRouteWay(walker, slot));
        if (way.arcEnd == way.arcFirst) continue;

        float endsAtM = stopsAtM;
        if (slot < row.routeCount - 1)
        {
            float fromM;
            walkSpanOfWay(
                slot > 0 ? walkerRouteWay(walker, slot - 1) : WalkNoLane, walkerRouteWay(walker, slot),
                walkerRouteWay(walker, slot + 1), fromM, endsAtM);
        }

        float headingRad;
        walkSampleAt(way, endsAtM, atM, headingRad);
        return true;
    }

    atM = vec2(0.0);
    return false;
}

// A body's piece laid as a row (LaneOccupancy.LayBody): standing, so a body; its holder its occupant.
ClaimRow walkerBodyRow(int way, float fromM, float toM, float alongMps, int occupant, uint roster, bool onItsLine, int onward, bool still)
{
    uint flags = RungHard | (roster << ClaimRosterShift) | (onItsLine ? ClaimOnItsLine : 0u) | (still ? ClaimStill : 0u);
    return ClaimRow(fromM, toM, alongMps, 0.0, 0.0, -walkerInfinity(), fromM, occupant, WalkerNone, onward, uint(way), flags);
}

// The ground a walker's body stands on, laid as rows under whoever it is held as at BODY_COVER[into ..], in the order
// the town lays them (WalkingGround.BodyCover); how many.
uint walkerBodyCover(WalkerRow row, uint walker, int occupant, uint roster, uint into)
{
    float radiusM = row.radiusM;
    int walking = row.onWay;
    float alongMps = walkerAlongItsWalkMps(row);
    int onward = walking == WalkerNone ? OnwardNoWay : walkerWayOf(walkerPeekNextRouteWay(row, walker));
    bool still = walkerIs(row, WalkerWounded) || all(equal(row.declaredMps, vec2(0.0)));

    uint written = 0u;
    uint dropped = 0u;
    int count = atlasUnderAWheel(row.positionM, radiusM, WALKER_FIGURES.groundChannel, dropped);
    for (int at = 0; at < count; at++)
    {
        AtlasCover under = atlasFound[at];
        bool onItsLine = under.way == walking;
        BODY_COVER[into + written] = walkerBodyRow(
            under.way, under.fromM, under.toM, onItsLine ? alongMps : 0.0, occupant, roster, onItsLine,
            onItsLine ? onward : OnwardNoWay, still);
        written++;
    }

    if (walking == WalkerNone) return written;

    float atM = row.onWayM;
    precise float fromM = atM - radiusM;
    precise float toM = atM + radiusM;
    BODY_COVER[into + written] = walkerBodyRow(
        walking, max(0.0, fromM), min(walkWayOfTown(walking).lengthM, toM), alongMps, occupant, roster, true, onward, still);
    return written + 1u;
}

// A walker's claim laid over the ways its action walks, as one hold at a walker's rung (WalkingGround.Claim, WalkAsk,
// LevelWithM), at CLAIMS_PROPOSED[into ..]; how many rows. A walk with no ways lays none, and is still a hold.
uint walkerClaim(WalkerRow row, int occupant, uint roster, int hold, WalkerWay walk[WalkerMostWays], int count, uint into)
{
    float alongMps = walkerAlongItsWalkMps(row);
    uint written = 0u;
    for (int index = 0; index < count; index++)
    {
        WalkerWay way = walk[index];
        precise float aheadM = way.lineFromM - row.radiusM;
        ClaimsAsk ask = ClaimsAsk(hold, occupant, roster, RungAfoot, way.fromM, way.lineFromM, aheadM, -walkerInfinity(), alongMps);
        float standsToM = max(way.fromM, claimsBodyReachesToM(uint(way.way), occupant, roster));
        written = claimsPropose(ask, uint(way.way), way.toM, standsToM, into, written);
    }

    return written;
}

// The zebra the walker is walking, or is held off where its plan was refused on it (WalkingGround.TheZebraOf).
int walkerTheZebraOf(WalkerRow row, uint walker, int cutOn)
{
    if (walkerIs(row, WalkerInside)) return WalkerNone;

    int on = walkerWayOf(walkerCurrentRouteWay(row, walker));
    if (on != WalkerNone && walkZebraOf(on) != WalkerNone) return walkZebraOf(on);

    int refusedOn = row.grantM <= 0.0 ? cutOn : WalkerNone;
    return refusedOn >= 0 ? walkZebraOf(refusedOn) : WalkerNone;
}

// What the walker got: how far past the front of its body its plan survived, less the gap it keeps off what cut it
// (WalkingGround.ReadTheGrant). answer is what its hold came to (LaneOccupancy.HoldEndsAtM, HoldCutOn).
void walkerReadTheGrant(inout WalkerRow row, uint walker, ClaimAnswer answer)
{
    if (row.hold == WalkerNone) row.grantM = 0.0;
    else if (isinf(answer.cutLineM) && answer.cutLineM > 0.0) row.grantM = walkerInfinity();
    else
    {
        precise float grantM = answer.cutLineM - answer.marginM - row.radiusM;
        row.grantM = grantM;
    }

    row.onCrossing = walkerTheZebraOf(row, walker, row.hold == WalkerNone ? WalkerNone : answer.cutOn);
}
