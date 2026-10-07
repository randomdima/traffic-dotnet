// The pavement's ways on the device (WalkingNetwork): the line each is walked on, how long it is and how wide its
// ground, what a corner takes off the lanes either side of it, and the zebra a lane is the paint of. A way is numbered
// as a walked route numbers it (WalkingNetwork.IsACorner) — a stretch's own lane, or the complement of a corner's turn
// slot — so a corner of the first slot is spelled as NoLane and read as it, as the host reads it.
//
// The including kernel includes walking_rows.glsl, declares WALK_WAYS (WalkWayRow[], every lane then every corner) and
// SPLINE_ARCS (Arc[], the lines the rows' runs point into), supplies WALKING_FIGURES' fields laneCount, firstFootwayWay
// and firstMitreWay (TownWays'), and includes arc.glsl and arc_chain.glsl first.

bool walkIsACorner(int way) { return way < 0; }

WalkWayRow walkWay(int way)
{
    return WALK_WAYS[way < 0 ? WALKING_FIGURES.laneCount + uint(~way) : uint(way)];
}

// The way of the town's numbering a way of the walk is (TownWays.OfFootway, OfMitre).
int walkTownWay(int way)
{
    return way < 0 ? WALKING_FIGURES.firstMitreWay + ~way : WALKING_FIGURES.firstFootwayWay + way;
}

// The row of one of the town's pavement ways (WayLines.LineOf over a footway or a mitre).
WalkWayRow walkWayOfTown(int townWay) { return WALK_WAYS[uint(townWay - WALKING_FIGURES.firstFootwayWay)]; }

// The zebra one of the town's ways is the paint of, or -1 (WayLines.ZebraOf): only a pavement lane can be.
int walkZebraOf(int townWay)
{
    if (townWay < WALKING_FIGURES.firstFootwayWay || townWay >= WALKING_FIGURES.firstMitreWay) return -1;

    return walkWayOfTown(townWay).zebra;
}

// The stretch of one way of a route the route covers, given the ways either side of it (WalkingNetwork.SpanOfWay).
void walkSpanOfWay(int before, int way, int after, out float fromM, out float toM)
{
    fromM = 0.0;
    toM = walkWay(way).lengthM;
    if (walkIsACorner(way)) return;

    if (before != WalkNoLane && walkIsACorner(before)) fromM = walkWay(before).joinToM;
    if (after != WalkNoLane && walkIsACorner(after))
    {
        precise float shortM = toM - walkWay(after).joinFromM;
        toM = min(toM, shortM);
    }

    toM = max(fromM, toM);
}

// A way's line at one distance along it (Spline.SampleAt over WalkingNetwork.WayArcs).
void walkSampleAt(WalkWayRow row, float distanceM, out vec2 positionM, out float headingRad)
{
    splineSample(row.arcFirst, row.arcEnd, distanceM, positionM, headingRad);
}
