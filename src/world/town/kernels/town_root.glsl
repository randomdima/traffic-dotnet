// The town's tables, each reached by its address through the root a resident kernel binds at set 0, binding 0
// (app/devicetick/TownRoot): one id a table, each module's ids in its own range and named in its own header.

#ifndef TOWN_ROOT_GLSL
#define TOWN_ROOT_GLSL

#extension GL_EXT_buffer_reference : require
#extension GL_EXT_shader_explicit_arithmetic_types_int64 : require

layout(std430, set = 0, binding = 0) readonly buffer TownRootTable
{
    uint64_t at[];
} townRoot;

layout(buffer_reference, std430, buffer_reference_align = 4) buffer Floats { float v[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer Uints { uint v[]; };
layout(buffer_reference, std430, buffer_reference_align = 4) buffer Ints { int v[]; };
layout(buffer_reference, std430, buffer_reference_align = 8) buffer Vec2s { vec2 v[]; };
layout(buffer_reference, std430, buffer_reference_align = 16) buffer Vec4s { vec4 v[]; };
layout(buffer_reference, std430, buffer_reference_align = 8) buffer Uint64s { uint64_t v[]; };

// A table by its id, as a reference of type T.
#define TOWN(T, id) T(townRoot.at[id])

#endif
