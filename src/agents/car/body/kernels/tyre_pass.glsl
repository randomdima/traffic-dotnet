// TyrePass on the device: one car's tyres for one tick, whole — the throttle it may ask for, the standstill it may
// skip, the ground under each wheel, the model's four impulses, the hold of a car its locks keep still, the slide it
// lifts for next tick, and the marks it lays. Line for line with the host, `precise` where it rounds twice.
//
// The including kernel includes car_build.glsl, tyre.glsl, suspension.glsl and tyre_marks.glsl first, defines TYRE_WHEEL_GROUNDS_BINDING,
// and its TYRE_FIGURES also carries slipBackOffPerTick, slipRecoverPerTick (SimConfig.TyreSlipBackOffPerTick and
// TyreSlipRecoverPerTick), minSlipThrottleFraction, tractionThrottleFraction, nominalGripMps2 and markSpacingM.

// The only two grounds a wheel stands on (WheelGrounds): two rows laid once, off every way and then on one.
layout(std430, binding = TYRE_WHEEL_GROUNDS_BINDING) readonly buffer TyreWheelGrounds { TyreSurface tyreWheelGrounds[]; };

// What a car's tyres carry from one tick to the next (TyreCarry).
struct TyreCarry
{
    float spinMps[TyreWheels];
    float scrubTravelM[TyreWheels];
    vec2 markFromM[TyreWheels];
    float markIntensity[TyreWheels];
    bool marking[TyreWheels];
    float slipThrottle;
    bool drivenSlipping;
};

// One stretch of mark a wheel laid (TyreMark); its width is the car's and its wheel is where it stands among the car's.
struct TyreMark
{
    vec2 fromM;
    vec2 toM;
    float intensity;
    bool ploughed;
};

// The throttle's back-off wound on for this tick, every tick and whether or not the wheels turn (TyrePass.WindTheSlipThrottle).
void tyrePassWindTheSlipThrottle(inout TyreCarry carry)
{
    if (carry.drivenSlipping)
    {
        precise float liftedTo = carry.slipThrottle - TYRE_FIGURES.slipBackOffPerTick;
        carry.slipThrottle = max(liftedTo, TYRE_FIGURES.minSlipThrottleFraction);
    }
    else
    {
        precise float recoveredTo = carry.slipThrottle + TYRE_FIGURES.slipRecoverPerTick;
        carry.slipThrottle = min(recoveredTo, 1.0);
    }
}

// The most a driven axle may be asked for, off the throttle's back-off as it was wound this tick (TyrePass.DriveCeilingMps2).
float tyrePassCeilingMps2(CarBuildRow car, bool hand, float acrossMps2, float slipThrottle)
{
    if (hand) return tyreDriveLeftMps2(car.gripMps2, acrossMps2);

    float leftMps2 = tyreDriveLeftMps2(TYRE_FIGURES.nominalGripMps2, acrossMps2);
    precise float ceilingMps2 = leftMps2 * TYRE_FIGURES.tractionThrottleFraction * slipThrottle;
    return ceilingMps2;
}

// A body at a dead standstill, nobody on the throttle, and wheels holding nothing over (TyrePass.AtRest) — the room past
// the car's own wheels carried as nothing. A device that flushes denormals (RADV) reads a denormal velocity as none, so
// a body the host left one is at rest here.
bool tyrePassAtRest(TyrePose pose, TyreCommand command, TyreCarry carry)
{
    if (pose.velocityMps != vec2(0.0) || pose.yawRateRadPerS != 0.0 || command.throttleMps2 != 0.0) return false;

    for (int wheel = 0; wheel < TyreWheels; wheel++)
    {
        if (carry.spinMps[wheel] != 0.0 || carry.marking[wheel] || carry.scrubTravelM[wheel] > 0.0) return false;
    }

    return true;
}

// A car whose pass would clear its impulses and scrub and change nothing else (TyrePass.Settled): at rest, with the
// throttle's back-off wound all the way back and no slide to lift for.
bool tyrePassSettled(TyrePose pose, TyreCommand command, TyreCarry carry)
{
    return carry.slipThrottle == 1.0 && !carry.drivenSlipping && tyrePassAtRest(pose, command, carry);
}

// One wheel's mark for this tick (TyrePass.LayMark): true where a stretch was laid, which is then `laid`.
bool tyrePassLayMark(vec2 atM, TyreSurface surface, TyreScrub scrub, int wheel, float dtS, inout TyreCarry carry, out TyreMark laid)
{
    laid = TyreMark(vec2(0.0), vec2(0.0), 0.0, false);
    bool ploughing = (scrub.flags & 1u) != 0u;
    if (scrub.slideSpeedMps <= 0.0 && !ploughing && carry.scrubTravelM[wheel] <= 0.0 && !carry.marking[wheel]) return false;

    carry.scrubTravelM[wheel] = tyreScrubTravelM(carry.scrubTravelM[wheel], scrub.slideSpeedMps, dtS);
    float intensity = tyreGroundMarkIntensity(surface, scrub, carry.scrubTravelM[wheel]);
    bool ploughs = surface.ploughs != 0u;

    if (intensity <= 0.0)
    {
        bool closing = carry.marking[wheel];
        if (closing) laid = TyreMark(carry.markFromM[wheel], atM, carry.markIntensity[wheel], ploughs);
        carry.marking[wheel] = false;
        return closing;
    }

    bool lays = false;
    if (!carry.marking[wheel])
    {
        carry.markFromM[wheel] = atM;
    }
    else
    {
        precise vec2 apartM = carry.markFromM[wheel] - atM;
        precise float spacingSquared = TYRE_FIGURES.markSpacingM * TYRE_FIGURES.markSpacingM;
        if (tyreDot(apartM, apartM) >= spacingSquared)
        {
            laid = TyreMark(carry.markFromM[wheel], atM, intensity, ploughs);
            carry.markFromM[wheel] = atM;
            lays = true;
        }
    }

    carry.marking[wheel] = true;
    carry.markIntensity[wheel] = intensity;
    return lays;
}

// One car's patches for this tick (TyrePass.Step): true where the car was at rest and nothing was asked of its tyres.
// `wheelsOnAWay` has a bit a wheel that stood on a way when the car's body was last laid, and `overNothing` one that
// stands on nothing (ControlAction.Hang); `laid` has a bit a wheel that laid a mark, which is then that wheel's of
// `marks`.
bool tyrePass(
    CarBuildRow car, TyrePose pose, TyreCommand command, bool hand, float inertiaKgM2, uint wheelsOnAWay,
    uint overNothing, float dtS, inout TyreCarry carry, out vec4 impulses[TyreWheels], out TyreScrub scrub[TyreWheels],
    out TyreMark marks[TyreWheels], out uint laid)
{
    laid = 0u;
    for (int wheel = 0; wheel < TyreWheels; wheel++)
    {
        impulses[wheel] = vec4(0.0);
        scrub[wheel] = TyreScrub(0.0, 0.0, 0.0, 0u);
        marks[wheel] = TyreMark(vec2(0.0), vec2(0.0), 0.0, false);
    }

    tyrePassWindTheSlipThrottle(carry);
    if (tyrePassAtRest(pose, command, carry))
    {
        carry.drivenSlipping = false;
        return true;
    }

    int wheels = int(car.wheelCount);
    float loadShare[TyreWheels];
    float acrossMps2 = suspensionLoads(car, pose.sprung, overNothing, loadShare);
    float ceilingMps2 = tyrePassCeilingMps2(car, hand, acrossMps2, carry.slipThrottle);
    vec2 atM[TyreWheels];
    tyreWheelPointsM(car, pose, atM);
    TyreSurface ground[TyreWheels];
    for (int wheel = 0; wheel < TyreWheels; wheel++)
    {
        if (wheel >= wheels) break;

        ground[wheel] = tyreWheelGrounds[(wheelsOnAWay >> uint(wheel)) & 1u];
    }

    tyreStep(car, pose, command, ceilingMps2, loadShare, atM, ground, carry.spinMps, dtS, impulses, scrub);

    if ((tyreLocksEveryWheel(command) || tyreHandbrake(command)) && command.throttleMps2 == 0.0
        && tyreHoldStill(car, pose, inertiaKgM2, loadShare, atM, ground, dtS, impulses, carry.spinMps))
    {
        for (int wheel = 0; wheel < TyreWheels; wheel++) scrub[wheel] = TyreScrub(0.0, 0.0, 0.0, 0u);
    }

    bool onThePedal = command.throttleMps2 > 0.0;
    bool slipping = false;
    for (int wheel = 0; wheel < TyreWheels; wheel++)
    {
        if (wheel >= wheels) break;

        bool sliding = (scrub[wheel].flags & 2u) != 0u;
        slipping = slipping || (sliding && onThePedal && buildWheelIs(car, wheel, WheelDriven));
        if (tyrePassLayMark(atM[wheel], ground[wheel], scrub[wheel], wheel, dtS, carry, marks[wheel])) laid |= 1u << uint(wheel);
    }

    carry.drivenSlipping = slipping;
    return false;
}
