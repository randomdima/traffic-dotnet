// The references the town's state headers share (town_cars.glsl and its siblings, app/devicetick/TownState): a table of
// 64-bit counts, one of doubles, and the rows more than one module keeps.

#ifndef TOWN_TABLES_GLSL
#define TOWN_TABLES_GLSL

#include "world/town/kernels/town_rows.glsl"
#include "core/simulation/kernels/rng.glsl"

layout(buffer_reference, std430, buffer_reference_align = 8) buffer Int64s { int64_t v[]; };
layout(buffer_reference, std430, buffer_reference_align = 8) buffer Rngs { Rng r[]; };

// One way under a body (WayCover): the stretch of it covered, in the way's own metres.
struct WayCover
{
    int way;
    float fromM;
    float toM;
};

layout(buffer_reference, std430, buffer_reference_align = 4) buffer WayCoverRows { WayCover r[]; };

#endif
