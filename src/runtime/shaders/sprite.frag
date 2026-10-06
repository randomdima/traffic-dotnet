#version 460

// Every sheet in the town on one array texture, packed by SheetAtlas: which picture an instance is
// drawn with is a layer and a rectangle rather than a descriptor, so nothing here is indexed by a
// number the driver has to scalarise. Clamped and un-mipped, because a page is sheets side by side.
layout(set = 0, binding = 2) uniform sampler2DArray sheets;

layout(location = 0) in vec3 inUv;
layout(location = 1) in vec4 inTint;

layout(location = 0) out vec4 outColour;

void main() {
    vec4 texel = texture(sheets, inUv);
    // The art is keyed, so most of every cell is nothing at all; discarding it keeps the blend from
    // laying a rectangle of near-zero alpha over whatever the sprite is standing on.
    if (texel.a < 0.004) discard;

    // The tint's fourth is the instance's own opacity — one for everything the town draws but a mark,
    // which is laid at whatever strength the tyre that made it earned.
    outColour = vec4(texel.rgb * inTint.rgb, texel.a * inTint.a);
}
