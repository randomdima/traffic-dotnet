// CarLamps on the device (CAR-14): which of a car's lenses are lit at one instant, given what its lamps say
// (CarLamps.Showing, worked out on the host) — line for line with CarLamps.Shown and Phase, `precise` throughout. There
// is no transcendental and no quotient in a lamp, so every lens is the host's.
//
// The including kernel supplies LAMP_FIGURES' fields: flashHz, flashOnShare, beaconHz.

// CarLampSet, by bit.
const uint LampTurnLeft = 1u;
const uint LampTurnRight = 2u;
const uint LampBrake = 4u;
const uint LampReverse = 8u;
const uint LampBeacon = 16u;
const uint LampWorks = 32u;
const uint LampHead = 64u;

// CarLampFitting, by number.
const uint FittingRear = 0u;
const uint FittingIndicator = 1u;
const uint FittingBeaconRed = 2u;
const uint FittingBeaconBlue = 3u;
const uint FittingHead = 5u;
const uint FittingBrake = 6u;
const uint FittingReverse = 7u;

// CarLamp, by number.
const uint LampIndicator = 0u;
const uint LampBrakeRed = 1u;
const uint LampReverseWhite = 2u;
const uint LampBeaconRed = 3u;
const uint LampBeaconBlue = 4u;
const uint LampBeaconAmber = 5u;
const uint LampHeadWhite = 6u;

// CarLamps.Phase: how far through its own period a flash of this rate is, in [0, 1).
float lampsPhase(float elapsedS, float rateHz)
{
    precise float turns = elapsedS * rateHz;
    precise float phase = turns - floor(turns);
    return phase;
}

// What one car's lamps are doing at one instant, worked out once for all its lenses (CarLamps.Shown's head).
struct CarLampMoment
{
    uint showing;
    bool braking;
    bool rearOn;
    uint indicating;
    bool flashOn;
    bool beaconOn;
    bool amberOn;
    bool firstHalf;
    // Whether the look draws a red beacon end (CarLamps.HasRedGlass), which the including kernel reads off its lenses.
    bool redGlass;
};

CarLampMoment lampsMoment(uint showing, float elapsedS)
{
    CarLampMoment moment;
    moment.showing = showing;
    moment.braking = (showing & LampBrake) != 0u;
    moment.rearOn = moment.braking || (showing & LampReverse) != 0u;
    moment.indicating = showing & (LampTurnLeft | LampTurnRight);

    moment.flashOn = moment.indicating != 0u && lampsPhase(elapsedS, LAMP_FIGURES.flashHz) < LAMP_FIGURES.flashOnShare;
    moment.beaconOn = (showing & LampBeacon) != 0u;
    moment.amberOn = moment.beaconOn || (showing & LampWorks) != 0u;

    moment.firstHalf = lampsPhase(elapsedS, LAMP_FIGURES.beaconHz) < 0.5;
    moment.redGlass = false;
    return moment;
}

// One lens at that instant (CarLamps.Shown's switch): the CarLamp it carries, and whether it is lit.
uint lampsLensColour(CarLampMoment moment, uint fitting, vec2 atBodyM, out bool lit)
{
    bool onTheRight = atBodyM.y > 0.0;
    if (fitting == FittingRear)
    {
        lit = moment.rearOn;
        return !moment.braking && (moment.showing & LampReverse) != 0u ? LampReverseWhite : LampBrakeRed;
    }

    if (fitting == FittingIndicator)
    {
        lit = moment.flashOn && moment.indicating == (onTheRight ? LampTurnRight : LampTurnLeft);
        return LampIndicator;
    }

    if (fitting == FittingBeaconRed)
    {
        lit = moment.beaconOn && moment.firstHalf;
        return LampBeaconRed;
    }

    if (fitting == FittingBeaconBlue)
    {
        lit = moment.beaconOn && (moment.redGlass ? !moment.firstHalf : moment.firstHalf != onTheRight);
        return LampBeaconBlue;
    }

    if (fitting == FittingHead)
    {
        lit = (moment.showing & LampHead) != 0u;
        return LampHeadWhite;
    }

    if (fitting == FittingBrake)
    {
        lit = moment.braking;
        return LampBrakeRed;
    }

    if (fitting == FittingReverse)
    {
        lit = (moment.showing & LampReverse) != 0u;
        return LampReverseWhite;
    }

    lit = moment.amberOn && moment.firstHalf != onTheRight;
    return LampBeaconAmber;
}
