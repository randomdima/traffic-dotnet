// GroundShapes.At on the device: what is on the ground at a point, on a level, solved against the town's own shapes in
// the reverse of the order they are drawn (TER-7) — the roads' paint, bays and decks, the carriageway's rings, the
// slabs, the water and its shore, the walk — and the same answer the host gives, off the host's own tables
// (GroundTable), laid once.
//
// The including kernel declares the tables from binding GROUND_BINDING on, defines GROUND_PUSH's fields (see
// ground_tables.glsl) and enables 64-bit floats. The answer is Ground's own number: grass 0, road 1, crosswalk 2,
// parking 3, water 4, sidewalk 5.

const uint GroundGrass = 0u;
const uint GroundRoad = 1u;
const uint GroundCrosswalk = 2u;
const uint GroundParking = 3u;
const uint GroundWater = 4u;
const uint GroundSidewalk = 5u;

const uint LatticeCarriageway = 0u;
const uint LatticeWalk = 1u;
const uint LatticeAbove = 2u;

struct RoadSays
{
    bool deck;
    bool crossing;
    bool bay;
};

// One road's say about a point it was projected onto at atM (GroundShapes.Weigh). The road's arcs are its slot's in
// the index, which are the road's own segments.
void groundWeigh(uint slot, int road, float atM, vec2 pointM, uint level, inout RoadSays says)
{
    GroundRoadRow row = groundRoads[road];
    if ((row.levelAndBay & 0xFFu) != level) return;

    GroundSlot s = groundSlots[slot];
    vec2 onM;
    float headingRad;
    splineSample(uint(s.arcStart), uint(s.arcEnd), atM, onM, headingRad);
    vec2 direction = trigUnit(headingRad);
    vec2 right = vec2(-direction.y, direction.x);
    precise vec2 offsetM = pointM - onM;

    float alongM = arcDot(offsetM, direction);
    float pastM = atM <= 0.0 ? max(0.0, -alongM) : atM >= row.lengthM ? max(0.0, alongM) : 0.0;
    float acrossM = abs(arcDot(offsetM, right));
    if (acrossM > row.reachM) return;
    if (pastM > 0.0) return;

    if (acrossM <= row.halfM)
    {
        for (int run = row.paintFrom; run < row.paintTo; run++)
        {
            vec2 paint = groundPaints[run];
            if (atM >= paint.x && atM <= paint.y) says.crossing = true;
        }

        if ((row.levelAndBay >> 8) != 0u) says.bay = true;
    }

    for (int run = row.deckFrom; run < row.deckTo; run++)
    {
        vec4 deck = groundDecks[run];
        if (atM < deck.x || atM > deck.y || acrossM > deck.z) continue;

        says.deck = true;
    }
}

// Everything the roads of one level say about a point, off the index (GroundShapes.Roads, ChainIndex.Near). A road
// met in two cells is weighed twice to the same answer, since what is kept is three flags.
RoadSays groundRoadsAt(vec2 pointM, uint level)
{
    RoadSays says = RoadSays(false, false, false);
    float radiusM = farthestReachM;
    precise vec2 leastM = pointM - vec2(radiusM);
    precise vec2 mostM = pointM + vec2(radiusM);
    precise float lx = leastM.x * roadPerCellM;
    precise float ly = leastM.y * roadPerCellM;
    precise float mx = mostM.x * roadPerCellM;
    precise float my = mostM.y * roadPerCellM;
    int fromX = int(floor(lx));
    int fromY = int(floor(ly));
    int toX = int(floor(mx));
    int toY = int(floor(my));
    int lastX = roadFromX + roadWidth - 1;
    int lastY = roadFromY + roadHeight - 1;
    if (roadWidth == 0 || roadHeight == 0 || toX < roadFromX || toY < roadFromY || fromX > lastX || fromY > lastY) return says;

    fromX = clamp(fromX, roadFromX, lastX);
    fromY = clamp(fromY, roadFromY, lastY);
    toX = clamp(toX, roadFromX, lastX);
    toY = clamp(toY, roadFromY, lastY);
    precise float reachSq = radiusM * radiusM;
    for (int y = fromY; y <= toY; y++)
    {
        for (int x = fromX; x <= toX; x++)
        {
            int cell = (y - roadFromY) * roadWidth + (x - roadFromX);
            for (int entry = groundCells[cell]; entry < groundCells[cell + 1]; entry++)
            {
                int slot = groundEntries[entry];
                GroundSlot s = groundSlots[slot];
                vec2 outside = max(max(s.leastM - pointM, pointM - s.mostM), vec2(0.0));
                if (arcDot(outside, outside) > reachSq) continue;

                float offSq;
                float atM = splineProject(uint(s.arcStart), uint(s.arcEnd), pointM, s.lengthM * 0.5, s.lengthM, offSq);
                if (offSq > reachSq) continue;

                groundWeigh(uint(slot), s.chainId, atM, pointM, level, says);
            }
        }
    }

    return says;
}

bool groundSlabReaches(vec2 pointM)
{
    for (uint slab = 0u; slab < slabCount; slab++)
    {
        vec4 box = groundSlabs[slab];
        precise vec2 offsetM = pointM - box.xy;
        if (offsetM.x < 0.0 || offsetM.y < 0.0) continue;
        if (offsetM.x > box.z || offsetM.y > box.w) continue;

        return true;
    }

    return false;
}

// Whether a ray east from the point crosses the ring an odd number of times (GroundShapes.Rings.Inside).
bool groundInside(int first, int end, vec2 pointM)
{
    bool inside = false;
    int count = end - first;
    for (int here = 0, there = count - 1; here < count; there = here++)
    {
        vec2 a = groundRingPoints[first + here];
        vec2 b = groundRingPoints[first + there];
        if ((a.y > pointM.y) == (b.y > pointM.y)) continue;

        precise float crossingX = ((b.x - a.x) * (pointM.y - a.y) / (b.y - a.y)) + a.x;
        if (pointM.x < crossingX) inside = !inside;
    }

    return inside;
}

ivec2 groundEdgeCell(GroundEdgeCells cells, vec2 pointM)
{
    precise float column = (pointM.x - cells.leastM.x) / cells.cellM;
    precise float row = (pointM.y - cells.leastM.y) / cells.cellM;
    return ivec2(clamp(int(floor(column)), 0, cells.columns - 1), clamp(int(floor(row)), 0, cells.rows - 1));
}

// The same of a long ring, off its edges by cell (GroundShapes.EdgeCells.Inside).
bool groundInsideFiled(GroundEdgeCells cells, int first, int end, vec2 pointM)
{
    precise float topM = cells.leastM.y + (float(cells.rows) * cells.cellM);
    if (pointM.y < cells.leastM.y || pointM.y > topM) return false;

    int count = end - first;
    bool inside = false;
    ivec2 at = groundEdgeCell(cells, pointM);
    for (int column = at.x; column < cells.columns; column++)
    {
        int cell = (at.y * cells.columns) + column;
        int from_ = groundEdgeStart[cells.startBase + cell];
        int to_ = groundEdgeStart[cells.startBase + cell + 1];
        if (from_ == to_) return inside != (groundEdgeEmpty[cells.emptyBase + cell] != 0u);

        for (int entry = from_; entry < to_; entry++)
        {
            int edge = groundEdgeEdges[cells.edgeBase + entry];
            vec2 a = groundRingPoints[first + edge];
            vec2 b = groundRingPoints[first + (edge + 1) % count];
            if ((a.y > pointM.y) == (b.y > pointM.y)) continue;

            precise float crossingX = ((b.x - a.x) * (pointM.y - a.y) / (b.y - a.y)) + a.x;
            if (pointM.x >= crossingX) continue;

            int counted = clamp(
                groundEdgeCell(cells, vec2(crossingX, pointM.y)).x, groundEdgeCell(cells, min(a, b)).x, groundEdgeCell(cells, max(a, b)).x);
            if (counted == column) inside = !inside;
        }
    }

    return inside;
}

// Whether any ring of a run [from, to) of the ring table covers the point (GroundShapes.Rings.Covers).
bool groundRingsCover(uint from_, uint to_, vec2 pointM)
{
    for (uint ring = from_; ring < to_; ring++)
    {
        GroundRing r = groundRings[ring];
        if (pointM.x < r.leastM.x || pointM.x > r.mostM.x) continue;
        if (pointM.y < r.leastM.y || pointM.y > r.mostM.y) continue;

        bool inside = r.cells >= 0
            ? groundInsideFiled(groundEdgeCellSets[r.cells], r.pointStart, r.pointEnd, pointM)
            : groundInside(r.pointStart, r.pointEnd, pointM);
        if (inside) return true;
    }

    return false;
}

uint groundAtGround(vec2 pointM)
{
    RoadSays roads = groundRoadsAt(pointM, 0u);
    if (roads.crossing) return GroundCrosswalk;
    if (roads.bay) return GroundParking;
    if (ringWinding(groundLattices[LatticeCarriageway], pointM) > 0) return GroundRoad;
    if (groundSlabReaches(pointM)) return GroundParking;
    if (roads.deck) return GroundSidewalk;
    if (groundRingsCover(waterFrom, waterTo, pointM)) return GroundWater;
    if (groundRingsCover(shoreFrom, shoreTo, pointM)) return GroundSidewalk;
    if (ringWinding(groundLattices[LatticeWalk], pointM) > 0) return GroundSidewalk;

    return GroundGrass;
}

// What a body on one level stands on (GroundShapes.At with a level, PHY-1a).
uint groundAt(vec2 pointM, uint level)
{
    if (level == 0u) return groundAtGround(pointM);

    RoadSays roads = groundRoadsAt(pointM, level);
    if (roads.crossing) return GroundCrosswalk;
    if (ringWinding(groundLattices[LatticeAbove], pointM) > 0) return GroundRoad;
    if (roads.deck) return GroundSidewalk;

    return groundAtGround(pointM);
}
