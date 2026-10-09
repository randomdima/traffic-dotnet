// A switching car's look for room on the device (Switching.Consider, Shape, MostBendUnder, UnderTheCar, IsOnTheRoad,
// Sweep, Knows, IsUnheld), its parts: a step drawn from where the car stands, the body read at each station down it off
// the atlas, and what that is asked of the claims the town's readers hold — line for line with the host, every quotient
// and root its own to the bit (rounded.glsl, pass_shape.glsl). The pose a step is read at is the device's own
// (overtake.glsl), its trigonometry trig.glsl's.
//
// The including kernel includes arc.glsl, car_build.glsl, car_line.glsl, arc_chain.glsl (its SPLINE_ARCS the lines),
// overtake.glsl, rounded.glsl, pass_shape.glsl, atlas.glsl and plan_rows.glsl first, and supplies SWITCH_FIGURES' fields
// reactionS, passSpareM, standOffM, gripMargin, brakingMargin, firstConnector, firstFootway, arcsPerCar and mostLanes;
// CAR_LINES (CarLineRow[] a car), LANE_CHAIN and LANE_END (a car's lanes), LANE_BESIDE (int[] two a lane: toward the kerb,
// toward the line), MARK_AT (uint[] a way and one past the last) and MARKS (CrossedSection[]), ZEBRA_WAYS (uint[] a bit a
// way: a crossing's), and WAYS_KNOWN (uint[], ascending: the ways the readers' layer holds, SwitchWaysLightsOnly marking
// those it holds the lights' holds of alone) with SWITCH_PUSH.waysKnown how many of them.

// DeviceRebuild's LightsOnly: a way sent back for its lights' holds alone, which the readers' layer does not hold.
const uint SwitchWaysLightsOnly = 0x80000000u;

// How many times a step is drawn again for a line bending under it more than it was drawn for, before the look is left
// to the town: each drawing bends past the last, and a town's line settles in two.
const int SwitchMostShapings = 16;

// What a look came to (SwitchOutcome, by number) — and Defers, where it is the town's to take: room found and a step to
// ask for, or more than the device's rooms hold.
const uint SwitchBegun = 0u;
const uint SwitchWaits = 2u;
const uint SwitchLooked = 3u;
const uint SwitchUnknown = 4u;
const uint SwitchAsks = 5u;
const uint SwitchDefers = 0xFFFFFFFFu;

// Of a look still under way: a step drawn, to be swept (switchShape).
const uint SwitchDrawn = 0xFFFFFFFEu;

// Whether the town's traffic drives a way, and whether it is a box's connector (TownWays.IsDriven, KindOf).
bool switchIsCarriageway(int way) { return uint(way) < SWITCH_FIGURES.firstFootway; }
bool switchIsConnector(int way) { return uint(way) >= SWITCH_FIGURES.firstConnector && switchIsCarriageway(way); }

// Whether a zebra's paint lies over any of a stretch of one way, read off its marks (DrivingGround.CrossesAZebra).
bool switchCrossesAZebra(int way, float fromM, float toM)
{
    for (uint mark = MARK_AT[way]; mark < MARK_AT[way + 1]; mark++)
    {
        CrossedSection section = MARKS[mark];
        if (section.mineFromM >= toM) break;

        uint onWay = uint(section.onWay);
        if (section.mineToM > fromM && (ZEBRA_WAYS[onWay >> 5u] & (1u << (onWay & 31u))) != 0u) return true;
    }

    return false;
}

// Whether the readers' layer holds a way's lists (LaneOccupancy.Knows): sent back with the last layer, for more than its
// lights' holds.
bool switchKnows(int way)
{
    uint low = 0u;
    uint high = SWITCH_PUSH.waysKnown;
    while (low < high)
    {
        uint middle = (low + high) >> 1u;
        if ((WAYS_KNOWN[middle] & ~SwitchWaysLightsOnly) < uint(way)) low = middle + 1u;
        else high = middle;
    }

    return low < SWITCH_PUSH.waysKnown && WAYS_KNOWN[low] == uint(way);
}

// How many stations a car's swept ground is read at, both ends among them (DrivingGround.StationsOfTheSweep).
int switchStations(float flankM, float fromM, float toM)
{
    if (toM <= fromM) return 0;

    precise float spanM = toM - fromM;
    return int(ceil(roundedQuotient(spanM, flankM))) + 1;
}

// Where along the line one station of a sweep from fromM to toM stands (Switching.UnderTheCar).
float switchStationM(CarBuildRow build, float fromM, float toM, int station)
{
    precise float flankM = build.widthM * 0.5;
    precise float stationM = fromM + (float(station) * flankM);
    return min(toM, stationM);
}

// The ways under the car at one metre of its step, into atlasFound: its collider stood where the step puts the rear axle
// there, pointed the way the step points it, and grown by spareM ahead and to either side (Switching.UnderTheCar) — how many.
int switchUnderTheCar(uint car, CarBuildRow build, OvertakeRow step, float atM, float spareM, uint channels)
{
    uint first = car * SWITCH_FIGURES.arcsPerCar;
    vec2 axleM;
    vec2 forward;
    overtakePoseAtM(step, first, first + CAR_LINES[car].arcCount, atM, axleM, forward);

    precise float halfSpareM = spareM * 0.5;
    precise float aheadM = build.centreAheadOfAxleM + halfSpareM;
    precise vec2 centreM = axleM + (forward * aheadM);
    precise float halfLengthM = (build.collisionLengthM * 0.5) + halfSpareM;
    precise float halfWidthM = (build.collisionWidthM * 0.5) + spareM;
    uint dropped = 0u;
    return atlasUnderBox(centreM, forward, halfLengthM, halfWidthM, channels, dropped);
}

// Whether every way atlasFound holds of a read is carriageway clear of every zebra (Switching.IsOnTheRoad, one station).
bool switchIsOnTheRoad(int under)
{
    for (int at = 0; at < under; at++)
    {
        AtlasCover cover = atlasFound[at];
        if (!switchIsCarriageway(cover.way) || switchCrossesAZebra(cover.way, cover.fromM, cover.toM)) return false;
    }

    return true;
}

// The carriageway's of the ways atlasFound holds of a read, kept at its head in order (Switching.SweepInto): how many.
int switchKeepTheCarriageway(int under)
{
    int kept = 0;
    for (int at = 0; at < under; at++)
    {
        if (!switchIsCarriageway(atlasFound[at].way)) continue;

        atlasFound[kept] = atlasFound[at];
        kept++;
    }

    return kept;
}

// The most a car's line bends under a stretch of it, on its own line or on the lane beside, whichever is tighter —
// infinite where the lane beside runs round the line's own centre (Switching.MostBendUnder).
float switchMostBendUnder(uint car, float fromM, float toM, float asideM)
{
    float mostBend = 0.0;
    float startM = 0.0;
    uint first = car * SWITCH_FIGURES.arcsPerCar;
    uint end = first + CAR_LINES[car].arcCount;
    for (uint at = first; at < end; at++)
    {
        Arc arc = SPLINE_ARCS[at];
        precise float endM = startM + arcLengthM(arc);
        if (startM >= toM) break;

        if (endM > fromM)
        {
            float curvature = arcCurvature(arc);
            precise float towardsTheCentre = 1.0 - (curvature * asideM);
            if (towardsTheCentre <= 0.0) return passInfinity();

            mostBend = max(mostBend, max(abs(curvature), abs(roundedQuotient(curvature, towardsTheCentre))));
        }

        startM = endM;
    }

    return mostBend;
}

// How much road a body doing this speed needs before it can be at rest (DrivingGround.StoppingM).
float switchStoppingM(float alongMps, float brakingMps2)
{
    if (alongMps <= 0.0) return 0.0;

    precise float squared = alongMps * alongMps;
    precise float twice = 2.0 * brakingMps2;
    return roundedQuotient(squared, twice);
}

// A step across drawn for a car meaning to move across toward inward, asideM across, where its tick left it
// (Switching.Shape, but for IsOnTheRoad, which is read with the sweep): SwitchDrawn and the step, where one is — Looked
// where no step it can drive lands inside its lane, Defers where the town is to draw it — and how far ahead its step from
// rest is, and where the ground past the step ends, as far as the drawing got.
uint switchShape(
    uint car, CarBuildRow build, CarTickRow tick, bool inward, float asideM, out OvertakeRow step, inout float fromRestM,
    inout float roomToM)
{
    step = overtakeNone();
    CarLineRow line = CAR_LINES[car];
    int lane = line.laneCount > 0u ? LANE_CHAIN[car * SWITCH_FIGURES.mostLanes] : -1;
    int beside = lane < 0 ? -1 : LANE_BESIDE[(uint(lane) * 2u) + (inward ? 1u : 0u)];
    if (beside < 0) return SwitchLooked;

    // A step nowhere across is drawn of quotients of nothing: the town's.
    if (asideM == 0.0) return SwitchDefers;

    precise float corneringMps2 = build.gripMps2 * SWITCH_FIGURES.gripMargin;
    precise float brakingMps2 = buildUtmostBrakingMps2(build) * SWITCH_FIGURES.brakingMargin;
    float speedMps = max(0.0, tick.alongMps);
    precise float outM = tick.progressM + (speedMps * 2.0 * SWITCH_FIGURES.reactionS) + SWITCH_FIGURES.passSpareM;
    float landsByM = LANE_END[car * SWITCH_FIGURES.mostLanes];

    float restM;
    float restMps;
    precise float spareFromRestM = passShape(build, 0.0, asideM, corneringMps2, 0.0, restM, restMps)
        ? restM + SWITCH_FIGURES.passSpareM
        : passInfinity();
    fromRestM = spareFromRestM;

    float lineBend = 0.0;
    for (int shaping = 0; shaping < SwitchMostShapings; shaping++)
    {
        float stepM;
        float fromMps;
        if (!passShape(build, speedMps, asideM, corneringMps2, lineBend, stepM, fromMps)) return SwitchLooked;

        precise float steppedOutM = outM + stepM;
        if (steppedOutM > landsByM) return SwitchLooked;

        float outMps = max(fromMps, passStepMps(build, stepM, asideM, corneringMps2, lineBend));
        precise float roomM = (outMps * SWITCH_FIGURES.reactionS) + switchStoppingM(outMps, brakingMps2) + SWITCH_FIGURES.standOffM;
        precise float reachM = steppedOutM + build.noseAheadOfAxleM + roomM;
        float toM = min(reachM, line.lengthM);

        float bend = switchMostBendUnder(car, outM, toM, asideM);
        if (isinf(bend)) return SwitchLooked;

        if (bend > lineBend)
        {
            lineBend = bend;
            continue;
        }

        roomToM = toM;
        step = OvertakeRow(lane, outM, stepM, passInfinity(), 0.0, asideM, outMps, passInfinity(), steppedOutM, 0u, 0.0, 0.0);
        return SwitchDrawn;
    }

    return SwitchDefers;
}
