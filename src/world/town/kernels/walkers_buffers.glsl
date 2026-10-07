// The one binding layout and push block every kernel of the walkers' side of a rebuild runs over (walkers_station,
// walkers_body, walkers_plan, walkers_read), so one set serves them all — laid by app/devicetick/DeviceWalkers — and
// everything walkers.glsl reads included over it.

layout(push_constant) uniform Figures
{
    uint walkerCount;
    uint routeRoom;
    uint straightRoom;
    uint passRoom;
    uint coverRoom;
    uint proposedRoom;
    uint laneCount;
    int firstFootwayWay;
    int firstMitreWay;
    uint groundChannel;
    float stepM;
    float standstillGapM;
    float walkAheadM;
    float offLaneM;
    float touchM;
    int cellsFromX;
    int cellsFromY;
    int cellsWidth;
    int cellsHeight;
    float perCellM;
    float cellM;
    int shift;
    int mask;
    int startShift;
    int rowStartsShift;
    int startsPerCell;
    float metresPerOutside;
    float alongPerMetre;
    uint hasChannels;
} figures;

#include "core/geometry/kernels/rounded.glsl"
#include "core/geometry/kernels/arc.glsl"
#include "world/foot/kernels/walking_rows.glsl"
#include "world/road/kernels/swept_rows.glsl"
#include "world/road/kernels/claims_rows.glsl"
#include "world/road/kernels/claims_marks.glsl"
#include "agents/person/actions/kernels/walker_rows.glsl"

// The walkers, their routes, the ground of the straights they committed to and of the passes they decided on.
layout(std430, binding = 0) buffer Walkers { WalkerRow walkers[]; };
layout(std430, binding = 1) readonly buffer Routes { int walkRoutes[]; };
layout(std430, binding = 2) buffer Straights { SweptRun straightRuns[]; };
layout(std430, binding = 3) readonly buffer Passes { SweptRun passRuns[]; };

// The pavement's ways and their lines, and the marks.
layout(std430, binding = 4) readonly buffer WalkWays { WalkWayRow walkWays[]; };
layout(std430, binding = 5) readonly buffer WalkArcs { Arc walkArcs[]; };
layout(std430, binding = 6) readonly buffer MarkAt { uint markAt[]; };
layout(std430, binding = 7) readonly buffer Marks { WayMark marks[]; };

// The claims as rows and each way's bodies as runs of the order (claims_lists.glsl), and what each walker's hold came to.
layout(std430, binding = 8) readonly buffer Rows { ClaimRow rows[]; };
layout(std430, binding = 9) readonly buffer BodyRuns { uvec2 bodyRuns[]; };
layout(std430, binding = 10) readonly buffer Order { uint order[]; };
layout(std430, binding = 11) readonly buffer Answers { ClaimAnswer answers[]; };

// What the walkers lay, a room each: their bodies' and passes' rows, the ways of their walks, their plans' rows and
// holds, and how many of each (bodies, passes, ways — or ~0 for none planned — and plan rows).
layout(std430, binding = 12) writeonly buffer Covers { ClaimRow covers[]; };
layout(std430, binding = 13) writeonly buffer Ways { WalkerWay ways[]; };
layout(std430, binding = 14) writeonly buffer Proposed { ClaimRow proposed[]; };
layout(std430, binding = 15) writeonly buffer Holds { HoldRow holds[]; };
layout(std430, binding = 16) writeonly buffer Counts { uvec4 counts[]; };

#define WALKER_FIGURES figures
#define WALKING_FIGURES figures
#define ATLAS_FIGURES figures
#define ATLAS_BINDING 17
#define WALK_WAYS walkWays
#define SPLINE_ARCS walkArcs
#define WALK_ROUTES walkRoutes
#define SWEPT_RUNS straightRuns
#define PASS_RUNS passRuns
#define MARK_AT markAt
#define MARK_SECTIONS marks
#define CLAIM_ROWS rows
#define BODY_RUNS bodyRuns
#define BODY_ORDER order
#define BODY_COVER covers
#define CLAIMS_PROPOSED proposed

#include "core/geometry/kernels/arc_chain.glsl"
#include "world/foot/kernels/walking.glsl"
#include "world/road/kernels/atlas.glsl"
#include "world/road/kernels/swept.glsl"
#include "world/road/kernels/claims_questions.glsl"
#include "world/road/kernels/claims_propose.glsl"
#include "agents/person/actions/kernels/walking_ground.glsl"
#include "agents/person/actions/kernels/walker_actions.glsl"
#include "world/town/kernels/walkers.glsl"
