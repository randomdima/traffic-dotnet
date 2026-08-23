#version 460

// One image for the whole interface: the glyph sheet, whose last cell is solid. A rectangle is that
// cell stretched, a glyph is its own cell, and neither this shader nor the recording can tell them
// apart — which is why there is one interface pipeline and not two.
layout(set = 0, binding = 3) uniform sampler2D glyphs;

layout(location = 0) in vec2 inUv;
layout(location = 1) in vec4 inColour;

layout(location = 0) out vec4 outColour;

void main() {
    // The sheet is coverage and nothing else: it carries no colour of its own, so the quad's colour
    // is the whole of what is drawn and the sheet only says how much of it lands.
    outColour = vec4(inColour.rgb, inColour.a * texture(glyphs, inUv).a);
}
