// TownWorld.TickCar on the device for a car following its route's line on no pass (CarAction.Follow): the line under it
// read (ReadTheLine, line.glsl), what it is told set (SetTheContext, drive_context.glsl) and the line driven (Drive,
// follower.glsl) — the command the tyre pass then delivers. The ground under it is car_ground.comp's, read before.
//
// The including kernel includes what line.glsl, follower.glsl and drive_context.glsl ask for, then those three; its
// CONNECTOR_AT is road_joins.glsl's.

// What a car's tick carries from one tick to the next and reports, the device's own (CarFleet.ProgressM,
// CoveredSinceClaimM, AlongMps, OffLineM, PlannedMps, Hold): flags is what the tick found.
struct CarTickRow
{
    float progressM;
    float coveredSinceClaimM;
    float alongMps;
    float offLineM;
    float plannedMps;
    uint hold;
    uint flags;
};

// What the host tells a car's tick, its decisions' and its claims' (CarFleet.PaceMps, TownWorld.OffTheLineAllowanceM):
// flags in the bits ContextCarRow's flags have.
struct CarToldRow
{
    uint flags;
    float paceMps;
    float allowanceM;
    float pad;
};

const uint TickLost = 32u;
const uint TickAsksTheRoute = 64u;
const uint TickShifted = 128u;
const uint TickHazard = 256u;

// What the last claims rebuild granted a car and what cut it, and the place it was sent to (CarFleet.AuthorityM,
// HorizonM, ClaimFromM, CommittedToM, GrantMarginM, GrantCutBy; LaneOccupancy.HoldCutOn; TownWorld.SceneOf).
struct CarGrantRow
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

DriveContextRow carTickToldNothing()
{
    float infinity = contextInfinity();
    return DriveContextRow(1.0, infinity, 0u, infinity, 0.0, 0u, infinity, 0.0, 0.0, 0u, infinity, 0.0);
}

// A car off its line (Rejoining.LoseTheLine through TownWorld.Hold): stopping on the handbrake and told nothing, with no
// junction ahead of it but the one it had already committed to.
void carTickLose(CarBuildRow build, inout CarTickRow tick, out TyreCommand command, inout BoxAheadRow box)
{
    tick.flags |= TickLost;
    tick.hold = HoldLostLine;
    command = TyreCommand(0.0, 0.0, build.brakingMps2, 1u);
    box = BoxAheadRow(contextInfinity(), contextInfinity(), box.flags & BoxCommitted, 0u);
}

// One tick of a following car: command holds the last tick's on the way in and this one's on the way out, and box the
// junction ahead as the last tick left it and as this one reads it.
void carTick(
    uint car, CarBuildRow build, TyrePose pose, float groundCoefficient, CarToldRow told, CarGrantRow grant, ClaimRow cutBy,
    inout CarTickRow tick, inout CarLineRow line, inout TyreCommand command, out DriveContextRow context, inout BoxAheadRow box)
{
    tick.flags = 0u;
    context = carTickToldNothing();
    if (line.laneCount == 0u)
    {
        carTickLose(build, tick, command, box);
        return;
    }

    float alongMps = tyreDot(pose.velocityMps, pose.forward);
    vec2 rearAxleM = followRearAxleM(build, pose.positionM, pose.forward);
    uint lanesWere = line.laneCount;
    LineAdvance advance = lineAdvance(car, build, rearAxleM, tick.progressM, line);
    // A lone sum has no product to contract into, and `precise` here sends glslang tracing every write to tick and line.
    tick.coveredSinceClaimM += advance.coveredM;
    if (line.laneCount < lanesWere) tick.flags |= TickShifted;

    tick.progressM = advance.progressM;
    tick.alongMps = alongMps;
    tick.offLineM = lineOffM(car, line, rearAxleM, advance.progressM, 0.0);
    if (tick.offLineM > told.allowanceM)
    {
        carTickLose(build, tick, command, box);
        return;
    }

    int lastLane = LANE_CHAIN[car * LINE_FIGURES.mostLanes + line.laneCount - 1u];
    bool roadRunsOn = CONNECTOR_AT[lastLane + 1] > CONNECTOR_AT[lastLane];
    if (lineRunsShort(build, line, tick.progressM, roadRunsOn)) tick.flags |= TickAsksTheRoute;

    ContextCarRow row = ContextCarRow(
        tick.progressM, tick.coveredSinceClaimM, grant.authorityM, grant.horizonM, grant.claimFromM, grant.committedToM,
        grant.grantMarginM, groundCoefficient, tick.plannedMps, grant.grantCutBy, told.flags, grant.cutOn, grant.placeM,
        grant.placeReachM, 0.0, pose.positionM, 0.0, 0.0);
    box = contextReadTheBoxAhead(car, build, line, row);
    context = contextSetTheContext(car, build, line, row, cutBy);

    uint first = car * LINE_FIGURES.arcsPerCar;
    uint hold;
    float plannedMps;
    bool hazard;
    command = followDriveTheLine(
        build, pose, first, first + line.arcCount, first, tick.progressM, line.lengthM, context, pose.forward, alongMps, false,
        command, overtakeNone(), false, (told.flags & ContextOnACall) != 0u, told.paceMps, hold, plannedMps, hazard);
    tick.hold = hold;
    tick.plannedMps = plannedMps;
    if (hazard) tick.flags |= TickHazard;
}
