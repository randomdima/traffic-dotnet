// TownWorld.TickCar on the device for a car following its route's line on no pass (CarAction.Follow): the line under it
// read (ReadTheLine, line.glsl), what it is told set (SetTheContext, drive_context.glsl) and the line driven (Drive,
// follower.glsl) — the command the tyre pass then delivers.
//
// The including kernel includes what line.glsl, follower.glsl and drive_context.glsl ask for, then those three; its
// CONNECTOR_AT is road_joins.glsl's.

#include "world/town/kernels/car_tick_rows.glsl"

DriveContextRow carTickToldNothing()
{
    float infinity = contextInfinity();
    return DriveContextRow(infinity, 0u, infinity, 0.0, 0u, infinity, 0.0, 0.0, 0u, infinity, 0.0, 0.0);
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

// The pass measured from the start of the next lane of the chain, which begins shiftM along the line it was measured on
// (Overtake.From).
OvertakeRow carTickPassFrom(OvertakeRow pass, int lane, float shiftM)
{
    precise float outM = pass.outM - shiftM;
    precise float backM = pass.backM - shiftM;
    precise float clearsM = pass.clearsM - shiftM;
    pass.lane = lane;
    pass.outM = outM;
    pass.backM = backM;
    pass.clearsM = clearsM;
    return pass;
}

// Where a car moving across for its route stands short of its line's end, or infinity (Switching.WaitAtM).
float carTickWaitAtM(CarToldRow told, CarLineRow line, float progressM)
{
    if ((told.flags & ToldWaitsShortOfTheEnd) == 0u) return contextInfinity();

    precise float toM = line.lengthM - told.fromRestM - CAR_TICK_FIGURES.passSpareM - progressM;
    return toM >= 0.0 ? toM : contextInfinity();
}

// One tick of a car following its line, waiting to rejoin it, or moving across onto the lane beside (Following.Tick,
// Rejoining.Tick, Switching.Tick): command holds the last tick's on the way in and this one's on the way out, box the
// junction ahead as the last tick left it and as this one reads it, and pass the step the car is on, shifted on with
// its line. A switch's step kept or withdrawn is the claims' to answer, and the lane beside it steps out onto the
// host's to lay (TickSteppedOut); the device drives on along the step meanwhile.
void carTick(
    uint car, CarBuildRow build, TyrePose pose, CarToldRow told, CarGrantRow grant, ClaimRow cutBy,
    inout CarTickRow tick, inout CarLineRow line, inout TyreCommand command, out DriveContextRow context, inout BoxAheadRow box,
    inout OvertakeRow pass)
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
    if (line.laneCount < lanesWere)
    {
        tick.flags |= TickShifted;
        if (pass.lane != -1) pass = carTickPassFrom(pass, LANE_CHAIN[car * LINE_FIGURES.mostLanes], advance.shiftM);
    }

    tick.progressM = advance.progressM;
    tick.alongMps = alongMps;
    float asideM = overtakeBegun(pass) ? overtakeAsideAtM(pass, advance.progressM) : 0.0;
    tick.offLineM = lineOffM(car, line, rearAxleM, advance.progressM, asideM);
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
        grant.grantMarginM, tick.plannedMps, grant.grantCutBy, told.flags, grant.cutOn, 0.0, told.placeM, told.placeReachM, 0.0,
        pose.positionM, 0.0, 0.0);
    box = contextReadTheBoxAhead(car, build, line, row);
    context = contextSetTheContext(car, build, line, row, cutBy);
    if ((told.flags & ToldSwitching) != 0u)
    {
        context.waitsToPass = 0u;
        if (overtakeBegun(pass) && tick.progressM >= overtakeSteppedOutM(pass)) tick.flags |= TickSteppedOut;
        if (!overtakeBegun(pass))
        {
            context.passAsideM = told.passAsideM;
            context.waitAtM = carTickWaitAtM(told, line, tick.progressM);
        }
    }

    uint first = car * LINE_FIGURES.arcsPerCar;
    uint hold;
    float plannedMps;
    bool hazard;
    command = followDriveTheLine(
        build, pose, first, first + line.arcCount, first, tick.progressM, line.lengthM, context, pose.forward, alongMps, false,
        command, pass, false, (told.flags & ContextOnACall) != 0u, told.paceMps, hold, plannedMps, hazard);
    tick.hold = hold;
    tick.plannedMps = plannedMps;
    if (hazard) tick.flags |= TickHazard;
}
