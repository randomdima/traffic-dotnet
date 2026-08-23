#version 460

// The placeholder the shader step is proved with: no vertex input, no descriptors, three vertices
// generated from gl_VertexIndex. It exists so a clean build fails loudly if glslc is missing rather
// than at the first real pipeline, and it is the first thing the renderer replaces.

layout(location = 0) out vec3 vColour;

vec2 kPositions[3] = vec2[](vec2(0.0, -0.5), vec2(0.5, 0.5), vec2(-0.5, 0.5));
vec3 kColours[3] = vec3[](vec3(1.0, 0.0, 0.0), vec3(0.0, 1.0, 0.0), vec3(0.0, 0.0, 1.0));

void main() {
    gl_Position = vec4(kPositions[gl_VertexIndex], 0.0, 1.0);
    vColour = kColours[gl_VertexIndex];
}
