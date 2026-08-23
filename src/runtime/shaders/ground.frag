#version 460
#extension GL_EXT_nonuniform_qualifier : require

// One surface's texture per index, reached through descriptor indexing: one set bound once instead
// of a set per material, which is what keeps the whole frame's crossing count at five whatever the
// town is made of (Engines/dotnet/Docs/tooling.md, "Keeping it sane").
layout(set = 0, binding = 0) uniform sampler2D surfaces[];

layout(location = 0) in vec2 inUv;
layout(location = 1) in vec3 inTint;
layout(location = 2) flat in uint inSurface;

layout(location = 0) out vec4 outColour;

// Not a surface: the tint alone, for a mark that is not ground.
const uint PAINT = 255u;

void main() {
    // An edge is the surface drawn darker and paint is the surface drawn brighter, so both are the
    // ground's own texture through a tint and the grain comes through both.
    outColour = inSurface == PAINT
        ? vec4(inTint, 1.0)
        : vec4(texture(surfaces[nonuniformEXT(inSurface)], inUv).rgb * inTint, 1.0);
}
