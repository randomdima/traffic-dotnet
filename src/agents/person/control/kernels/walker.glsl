// WalkerFollower on the device: one tick of one walker — stand, turn on the spot, or walk straight at the aim, and the
// impulse that costs at the grip it has (WalkerFollower.Tick, Step, TurnToward, Wrap, AtMost). Line for line with the
// host, `precise` throughout; the bearing is trig.glsl's arctangent, and nothing else is trigonometry; every quotient and
// root is rounded.glsl's.
//
// The including kernel supplies WALKER_FIGURES' fields: walkSpeedMps, turnRateDegPerS, footGripMps2, slidingGripMps2.

#include "core/geometry/kernels/trig.glsl"
#include "core/geometry/kernels/rounded.glsl"

// MathF.PI and the Tau Wrap reduces by, as the host's floats.
const float WalkerPi = 3.14159274;
const float WalkerTau = 6.28318548;

// What one tick asks of one walker (WalkerStep).
struct WalkerStep
{
    float headingRad;
    vec2 desiredMps;
    vec2 impulseNs;
};

float walkerLength(vec2 v) { return roundedLength(v); }

// Into (−π, π]. The host's % truncates toward zero and is exact; this is the same remainder for an angle under two and a
// half turns either way, which a heading's difference from a bearing never comes near: past it τ·n is no longer a float.
float walkerWrap(float angleRad)
{
    float turns = trunc(roundedQuotient(angleRad, WalkerTau));
    precise float wrapped = angleRad - (WalkerTau * turns);
    if (wrapped > WalkerPi)
    {
        precise float back = wrapped - WalkerTau;
        wrapped = back;
    }
    else if (wrapped <= -WalkerPi)
    {
        precise float on = wrapped + WalkerTau;
        wrapped = on;
    }

    return wrapped;
}

float walkerTurnToward(float fromRad, float toRad, float mostRad)
{
    precise float apart = toRad - fromRad;
    float difference = walkerWrap(apart);
    if (difference > mostRad) difference = mostRad;
    else if (difference < -mostRad) difference = -mostRad;

    precise float turned = fromRad + difference;
    return walkerWrap(turned);
}

vec2 walkerAtMost(vec2 changeMps, float mostMps)
{
    float lengthMps = walkerLength(changeMps);
    if (!(lengthMps > mostMps)) return changeMps;

    float share = roundedQuotient(mostMps, lengthMps);
    precise vec2 cut = changeMps * share;
    return cut;
}

WalkerStep walkerStep(
    float headingRad, vec2 positionM, vec2 velocityMps, vec2 declaredMps, vec2 aimM, bool moving, bool onFeet, float massKg,
    float dtS)
{
    float heading = headingRad;
    vec2 desired = vec2(0.0);

    precise vec2 toAim = aimM - positionM;
    float distanceM = walkerLength(toAim);
    precise float distanceSquared = distanceM * distanceM;
    if (distanceSquared > 1e-8)
    {
        float paceMps = WALKER_FIGURES.walkSpeedMps;
        float bearingRad = trigAtan2(toAim.y, toAim.x);
        precise float mostRad = roundedQuotient(WALKER_FIGURES.turnRateDegPerS * WalkerPi, 180.0) * dtS;
        precise float hopM = paceMps * dtS;

        if (distanceM <= hopM)
        {
            if (moving)
            {
                desired = roundedQuotient(toAim, dtS);
            }
        }
        else
        {
            precise float offRad = bearingRad - headingRad;
            if (abs(walkerWrap(offRad)) > mostRad)
            {
                heading = walkerTurnToward(headingRad, bearingRad, mostRad);
            }
            else
            {
                heading = bearingRad;
                if (moving)
                {
                    precise vec2 pace = roundedQuotient(toAim, distanceM) * paceMps;
                    desired = pace;
                }
            }
        }
    }

    vec2 correctionMps;
    if (onFeet)
    {
        precise float gripMps = WALKER_FIGURES.footGripMps2 * dtS;
        precise vec2 shoveMps = declaredMps - velocityMps;
        vec2 takenBackMps = walkerAtMost(shoveMps, gripMps);
        precise vec2 own = desired - declaredMps + takenBackMps;
        correctionMps = own;
    }
    else
    {
        precise float gripMps = WALKER_FIGURES.slidingGripMps2 * dtS;
        precise vec2 wantedMps = desired - velocityMps;
        correctionMps = walkerAtMost(wantedMps, gripMps);
    }

    precise vec2 impulse = correctionMps * massKg;
    return WalkerStep(heading, desired, impulse);
}

// WalkerFollower.Tick: false where the tick writes nothing, which is a casualty's — its slide is the body phase's.
bool walkerTick(
    float headingRad, vec2 positionM, vec2 velocityMps, vec2 declaredMps, vec2 aimM, bool walking, bool wounded, bool inside,
    bool paused, float massKg, float dtS, out WalkerStep step)
{
    if (paused)
    {
        step = walkerStep(headingRad, positionM, velocityMps, declaredMps, positionM, false, !wounded, massKg, dtS);
        return true;
    }

    if (inside)
    {
        step = WalkerStep(headingRad, vec2(0.0), vec2(0.0));
        return true;
    }

    if (wounded)
    {
        step = WalkerStep(headingRad, vec2(0.0), vec2(0.0));
        return false;
    }

    step = walkerStep(headingRad, positionM, velocityMps, declaredMps, aimM, walking, true, massKg, dtS);
    return true;
}
