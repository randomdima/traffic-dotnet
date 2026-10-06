// GroundCatalog on the device: what each kind of ground does to a body on it — its grip, its drag, the factor on the
// mark threshold and whether a wheel ploughs it — laid once off GroundCatalog.Table and indexed by Ground's own number,
// which is what ground.glsl answers.
//
// The including kernel defines GROUND_EFFECT_BINDING and includes citygen/kernels/ground.glsl, with its tables, first.

struct GroundEffect
{
    float coefficient;
    float dragMps2;
    float markFactor;
    uint ploughs;
};

layout(std430, binding = GROUND_EFFECT_BINDING) readonly buffer GroundEffects { GroundEffect groundEffects[]; };

// What the ground under a point does to a body standing on one level (GroundLocator.EffectAt).
GroundEffect groundEffectAt(vec2 pointM, uint level)
{
    return groundEffects[groundAt(pointM, level)];
}
