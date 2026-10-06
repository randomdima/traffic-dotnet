// The ground's tables on the device (GroundTable), declared from binding GROUND_BINDING on — so a kernel that asks
// what is underfoot, the car's included, binds them after its own. The field layouts are DeviceGround's, row for row.

struct GroundSlot
{
    vec2 leastM;
    vec2 mostM;
    int arcStart;
    int arcEnd;
    int chainId;
    float lengthM;
};

struct GroundRoadRow
{
    float halfM;
    float lengthM;
    float reachM;
    uint levelAndBay;
    int deckFrom;
    int deckTo;
    int paintFrom;
    int paintTo;
};

struct GroundRing
{
    vec2 leastM;
    vec2 mostM;
    int pointStart;
    int pointEnd;
    int cells;
    int pad;
};

struct GroundEdgeCells
{
    vec2 leastM;
    float cellM;
    int columns;
    int rows;
    int startBase;
    int edgeBase;
    int emptyBase;
};

layout(std430, binding = GROUND_BINDING + 0) readonly buffer GroundArcs { Arc groundArcs[]; };
layout(std430, binding = GROUND_BINDING + 1) readonly buffer GroundSlots { GroundSlot groundSlots[]; };
layout(std430, binding = GROUND_BINDING + 2) readonly buffer GroundCellStarts { int groundCells[]; };
layout(std430, binding = GROUND_BINDING + 3) readonly buffer GroundEntrySlots { int groundEntries[]; };
layout(std430, binding = GROUND_BINDING + 4) readonly buffer GroundRoadRows { GroundRoadRow groundRoads[]; };
layout(std430, binding = GROUND_BINDING + 5) readonly buffer GroundDeckRuns { vec4 groundDecks[]; };
layout(std430, binding = GROUND_BINDING + 6) readonly buffer GroundPaintRuns { vec2 groundPaints[]; };
layout(std430, binding = GROUND_BINDING + 7) readonly buffer GroundSlabBoxes { vec4 groundSlabs[]; };
layout(std430, binding = GROUND_BINDING + 8) readonly buffer GroundLattices { RingLattice groundLattices[]; };
layout(std430, binding = GROUND_BINDING + 9) readonly buffer GroundLatticeCells { int groundLatticeCells[]; };
layout(std430, binding = GROUND_BINDING + 10) readonly buffer GroundLatticeFiling { int groundLatticeFiling[]; };
layout(std430, binding = GROUND_BINDING + 11) readonly buffer GroundLatticePieces { RingPiece groundLatticePieces[]; };
layout(std430, binding = GROUND_BINDING + 12) readonly buffer GroundRingRows { GroundRing groundRings[]; };
layout(std430, binding = GROUND_BINDING + 13) readonly buffer GroundRingPoints { vec2 groundRingPoints[]; };
layout(std430, binding = GROUND_BINDING + 14) readonly buffer GroundEdgeCellSets { GroundEdgeCells groundEdgeCellSets[]; };
layout(std430, binding = GROUND_BINDING + 15) readonly buffer GroundEdgeStarts { int groundEdgeStart[]; };
layout(std430, binding = GROUND_BINDING + 16) readonly buffer GroundEdgeEdgeList { int groundEdgeEdges[]; };
layout(std430, binding = GROUND_BINDING + 17) readonly buffer GroundEdgeEmptyFlags { uint groundEdgeEmpty[]; };

// How many bindings the tables take, for whatever a kernel binds after them.
const int GroundBindings = 18;
