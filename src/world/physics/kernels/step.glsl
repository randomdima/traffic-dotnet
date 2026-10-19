// The constants every kernel of the solver's step reads, in one block so a step is recorded with one struct
// (DeviceSolver.StepPush). Figures are the SimConfig's, derived on the host exactly as the CPU step derives them.

layout(push_constant) uniform Step
{
    // The window both grids are filed over, in cells of the main level, and the level's cells per metre.
    int windowFromX;
    int windowFromY;
    int windowWidth;
    int windowHeight;
    float perCellM;

    float dtS;
    float speculativeM;
    float friction;

    // How many of the roster a kernel is run over, which pass of a two-pass kernel this is, and how many a run may
    // write before it is full.
    uint rosterCount;
    uint mode;
    uint capacity;

    uint enabledBit;

    // What of a body's motion one step's damping keeps: 1 / (1 + dt · damping), taken on the host as the CPU takes it.
    float linearKeep;
    float angularKeep;
    float allowedPenetrationM;

    // The push-out rate per metre of overlap: the config's bias over the step, divided on the host.
    float contactBias;
    float maxPushOutMps;
    float inverseStep;
    uint velocityIterations;
    uint positionIterations;
    uint staticBit;
    uint filedFrozenBit;
    uint frozenBit;

    // How many steps in a row a body ends at rest before it freezes, and the rest speed squared, taken on the host.
    uint ticksToFreeze;
    float restSpeedMpsSquared;

    // How many bodies the host counts at rest afresh this step (DeviceSolver.CountAfresh).
    uint afreshCount;

    // How many pairs a step can list as begun touching (touches.comp).
    uint begunRoom;

    // How many buckets a hashed grid's cells are filed in, a power of two — or none, for a grid filed cell for cell.
    uint buckets;

    // How many contacts a step may hold, where `capacity` is how many candidates (DeviceSolver.ContactCapacity), and how
    // many of them the bodies it woke may find (woken.comp).
    uint contactCapacity;
    uint sideRoom;

    // How many links the solve holds (solve.comp, PhysicsWorld.Links), and the flag a body never damped carries (BodyFlags.Undamped).
    uint linkCount;
    uint undampedBit;
};

// Where the step keeps its running totals and the indirect dispatches made of them (DeviceSolver.Totals).
const uint CandidateTotalAt = 0u;
const uint ContactTotalAt = 1u;
const uint OverflowAt = 2u;
const uint CandidateGroupsAt = 4u;
const uint ContactGroupsAt = 8u;
const uint ColoursAt = 12u;
const uint LeftOverAt = 13u;

int cellX(float atM) { precise float scaled = atM * perCellM; return clamp(int(floor(scaled)), windowFromX, windowFromX + windowWidth - 1); }
int cellY(float atM) { precise float scaled = atM * perCellM; return clamp(int(floor(scaled)), windowFromY, windowFromY + windowHeight - 1); }
uint cellIndex(int x, int y) { return uint((y - windowFromY) * windowWidth + (x - windowFromX)); }

// Where a grid keeps a cell's run: the cell's own place, or the bucket it hashes to where the grid is hashed (buckets).
// Neighbouring cells are numbered one or a row apart, so the number is mixed whole before it is cut to the buckets.
uint slotOf(uint cell)
{
    if (buckets == 0u) return cell;

    uint mixed = cell;
    mixed ^= mixed >> 16;
    mixed *= 0x7feb352du;
    mixed ^= mixed >> 15;
    mixed *= 0x846ca68bu;
    mixed ^= mixed >> 16;
    return mixed & (buckets - 1u);
}
