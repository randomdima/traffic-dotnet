#version 460

// The interface and the debug layers: the same quad the bodies are drawn with, under either of two
// transforms. A panel, a glyph, a tape and a debug line are all this one instance — which is what
// keeps the whole interface inside a recording that was written once.

layout(set = 0, binding = 0) uniform Camera {
    vec2 centreM;
    vec2 clipPerM;
    vec2 uiPx;
    // How far the town is turned on screen, as its cosine and its sine (OBS-1c). Upright is (1, 0),
    // and a screen-space quad never reaches this: the interface does not turn with the town.
    vec2 facing;
} camera;

// The town's own metres to clip. **The turn is applied here and nowhere else**: a band is built about
// its own direction in the town's axes, and turning the offset turns the built band with it.
vec4 toClip(vec2 atM) {
    vec2 fromCentreM = atM - camera.centreM;
    vec2 turnedM = vec2(
        fromCentreM.x * camera.facing.x - fromCentreM.y * camera.facing.y,
        fromCentreM.x * camera.facing.y + fromCentreM.y * camera.facing.x);
    return vec4(turnedM * camera.clipPerM, 0.0, 1.0);
}

layout(location = 0) in vec2 inCentre;
layout(location = 1) in vec2 inHalfSize;
layout(location = 2) in vec2 inUvMin;
layout(location = 3) in vec2 inUvSize;
layout(location = 4) in vec4 inColour;
layout(location = 5) in float inRotation;
layout(location = 6) in uint inScreen;
layout(location = 7) in float inTaper;

layout(location = 0) out vec2 outUv;
layout(location = 1) out vec4 outColour;

void main() {
    vec2 corner = vec2(float(gl_VertexIndex & 1), float((gl_VertexIndex >> 1) & 1));
    vec2 side = corner * 2.0 - 1.0;

    // The taper slants the two ends in opposite directions, which is how a piece of a band round a
    // bend is cut square to the line rather than to the chord: the next piece is cut on the same
    // line, so the two share an edge and no notch opens on the outside of the turn. It is zero for
    // everything that is a rectangle, which is everything else drawn here.
    vec2 fromCentre = vec2(side.x * (inHalfSize.x - side.y * inTaper), side.y * inHalfSize.y);

    vec2 along = vec2(cos(inRotation), sin(inRotation));
    vec2 across = vec2(-along.y, along.x);
    vec2 at = inCentre + along * fromCentre.x + across * fromCentre.y;

    // Screen space is in interface pixels from the top-left, so an interface that is laid out once
    // does not move when the camera does; world space is the town's own metres, which is what a
    // debug line drawn where it happens needs. Interface pixels and not the framebuffer's: this
    // division is where the desktop's scale factor is paid, and a 2x desktop hands in half the
    // extent so a panel comes out twice the pixels and the same size on the glass.
    gl_Position = inScreen == 0u
        ? toClip(at)
        : vec4(at / camera.uiPx * 2.0 - 1.0, 0.0, 1.0);

    outUv = inUvMin + corner * inUvSize;
    outColour = inColour;
}
