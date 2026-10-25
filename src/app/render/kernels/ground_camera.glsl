// The camera as the ground's two stages read it (CameraView): the vertex stage its place and periods, the fragment
// stage the water's waves.
//
// The camera lives in a buffer the CPU writes into rather than in a push constant, because a push
// constant is recorded into the command buffer and this engine records its command buffers once.
// The whole block is declared even where a stage reads two of it, because std140 lays a member at the
// offset its predecessors leave it at: a block that stopped at clipPerM would put facing where uiPx is.
layout(set = 0, binding = 0) uniform Camera {
    vec2 centreM;
    vec2 clipPerM;
    vec2 uiPx;
    // How far the town is turned on screen, as its cosine and its sine (OBS-1c). Upright is (1, 0).
    vec2 facing;
    // The period each surface's texture repeats over: grass, tarmac, pavement and deck, then the water and the shore.
    vec4 surfacePeriodsM;
    vec4 waterPeriodM;
    vec4 propCull;
    vec4 turning;
    vec4 carTilt;
    vec4 cloudField;
    vec4 cloudLook;
    // The patches every surface wears its looks by (GroundPatches): the field's mean and one over its spread; then each
    // surface's light and warmth a standard deviation of it, and where its second look begins and is whole, in the
    // surfaces' order; one over each surface's patches' width, the four on land then the water's and the shore's; the
    // hexes the looks are scattered in; and the lumps a fade is ragged by.
    vec4 patchField;
    vec4 patchLook[6];
    vec4 patchPerAcross;
    vec4 patchPerAcrossWet;
    vec4 patchScatter;
    vec4 patchRagged;
    // How far the water's still look and its lively one have drifted, in metres (GroundPatches.DriftAt).
    vec4 waterDriftM;
    // The waves the water sways under (WaterWaves): each train's wavenumber along x, in radians a metre, then the sway
    // downwind under the tallest crest along x, in metres; the same along y; how far each train has run; how far its
    // crests are bent off their lines, then one over the width of the lumps that bend them, in metres; and the crests'
    // light, the fewest pixels a train is drawn across and how far the water's patches move the waves' height; and how
    // far a crest moves the water's fade up the shore, as a share of its cover, and how much of the waterline is foam.
    vec4 waveNumberXPerM;
    vec4 waveNumberYPerM;
    vec4 wavePhaseRad;
    vec4 waveBendRad;
    vec4 waveLook;
    vec4 waveShore;
} camera;
