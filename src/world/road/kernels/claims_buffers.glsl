// The one binding layout every pass of the claims runs over (claims_answer, claims_show, claims_cut), so one set serves
// them all, and claims.glsl over it. The including kernel has included claims_rows.glsl and declared its push block as
// `figures` (ClaimsPush in app/devicetick/DeviceClaims) first.

layout(std430, binding = 0) buffer Rows { ClaimRow rows[]; };
layout(std430, binding = 1) readonly buffer BodyRuns { uvec2 bodyRuns[]; };
layout(std430, binding = 2) readonly buffer PlannedRuns { uvec2 plannedRuns[]; };
layout(std430, binding = 3) readonly buffer BodyOrder { uint bodyOrder[]; };
layout(std430, binding = 4) readonly buffer PlannedOrder { uint plannedOrder[]; };
layout(std430, binding = 5) readonly buffer Holds { HoldRow holds[]; };
layout(std430, binding = 6) buffer Shown { float shown[]; };
layout(std430, binding = 7) buffer Answers { ClaimAnswer answers[]; };
layout(std430, binding = 8) buffer Moved { uint moved[]; };

#define CLAIMS_FIGURES figures
#define CLAIM_ROWS rows
#define BODY_RUNS bodyRuns
#define PLANNED_RUNS plannedRuns
#define BODY_ORDER bodyOrder
#define PLANNED_ORDER plannedOrder
#define HOLD_ROWS holds
#define SHOWN_TO shown

#include "world/road/kernels/claims.glsl"
