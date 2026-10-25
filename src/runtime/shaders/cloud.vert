#version 460

// The clouds' shadows (OBJ-8, CloudShadows): one triangle over the whole target, each corner told where in the town it
// stands, so the fragment stage reads the clouds' field at the very point of the town under it.

// The whole block is declared, because std140 lays a member at the offset its predecessors leave it at (CameraView).
layout(set = 0, binding = 0) uniform Camera {
    vec2 centreM;
    vec2 clipPerM;
    vec2 uiPx;
    vec2 facing;
    vec4 surfacePeriodsM;
    vec4 waterPeriodM;
    vec4 propCull;
    vec4 turning;
    vec4 carTilt;
    // Where the town's origin is in the field and how many lives it has lived; then one over a cloud's width, where in
    // the field a shadow begins and where it is deepest, and the light it takes there (CloudShadow).
    vec4 cloudField;
    vec4 cloudLook;
} camera;

// Where in the field this point of the town is, in clouds across, then in lives.
layout(location = 0) out vec3 outField;
layout(location = 1) flat out vec3 outLook;

void main() {
    // A shade of nought is a clear sky: the triangle put past the far plane, so nothing of it is rasterised.
    if (camera.cloudLook.w <= 0.0) {
        gl_Position = vec4(0.0, 0.0, 2.0, 1.0);
        outField = vec3(0.0);
        outLook = vec3(0.0);
        return;
    }

    // (-1, -1), (3, -1), (-1, 3): one triangle the screen is inside.
    vec2 clip = vec2(float((gl_VertexIndex & 1) << 2), float((gl_VertexIndex & 2) << 1)) - 1.0;
    gl_Position = vec4(clip, 0.0, 1.0);

    // toClip undone: off the clip's scale, then turned back by the town's turn (OBS-1c).
    vec2 turnedM = clip / camera.clipPerM;
    vec2 fromCentreM = vec2(
        turnedM.x * camera.facing.x + turnedM.y * camera.facing.y,
        -turnedM.x * camera.facing.y + turnedM.y * camera.facing.x);
    vec2 atM = camera.centreM + fromCentreM;

    outField = vec3((atM + camera.cloudField.xy) * camera.cloudLook.x, camera.cloudField.z);
    outLook = camera.cloudLook.yzw;
}
