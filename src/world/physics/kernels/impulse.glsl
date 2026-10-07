// An impulse spent on one body's motion, as the agents actuate it (PhysicsWorld.ApplyImpulseAt, ApplyCentralImpulse):
// the motion as the step keeps it (velocity xy, yaw rate z, w carried) and the body's two inverses (inverse mass,
// inverse inertia). An impulse of nothing changes nothing, as on the host. `precise` on every product and sum the host
// rounds, so a body's motion after its impulses is the host's to the bit.
//
// Includes shape.glsl, so a kernel including this one does not include that one again.

#ifndef IMPULSE_GLSL
#define IMPULSE_GLSL

#include "shape.glsl"

vec4 impulsedAt(vec4 motion, vec2 positionM, vec2 inverses, vec2 impulseNs, vec2 atM)
{
    if (impulseNs == vec2(0.0)) return motion;

    precise vec2 gainedMps = impulseNs * inverses.x;
    precise vec2 velocityMps = motion.xy + gainedMps;
    precise vec2 armM = atM - positionM;
    precise float turnedRadPerS = inverses.y * shapeCross(armM, impulseNs);
    precise float yawRateRadPerS = motion.z + turnedRadPerS;
    return vec4(velocityMps, yawRateRadPerS, motion.w);
}

vec4 impulsedCentrally(vec4 motion, float inverseMass, vec2 impulseNs)
{
    if (impulseNs == vec2(0.0)) return motion;

    precise vec2 gainedMps = impulseNs * inverseMass;
    precise vec2 velocityMps = motion.xy + gainedMps;
    return vec4(velocityMps, motion.zw);
}

#endif
