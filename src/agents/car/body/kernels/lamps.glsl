// CarLamps on the device (CAR-14): what a car's lamps say, and which of its lenses are lit at one instant — line for
// line with CarLamps.Showing, OnThePass, Towards, TurnAheadRad, Shown and Phase, `precise` throughout. There is no
// transcendental and no quotient in a lamp, so every lens is the host's.
//
// The including kernel supplies LAMP_FIGURES' fields: lampBrakeMps2, stepBackLeadS, junctionAheadM, turnAheadM,
// indicateRad, flashHz, flashOnShare, beaconHz — and LAMP_LINE_ARCS, the fleet's lines as arc.glsl's Arc, a car's
// from where its row says. arc.glsl is included first.

// CarLampSet, by bit.
const uint LampTurnLeft = 1u;
const uint LampTurnRight = 2u;
const uint LampBrake = 4u;
const uint LampReverse = 8u;
const uint LampBeacon = 16u;
const uint LampWorks = 32u;

// CarLampFitting, by number.
const uint FittingRear = 0u;
const uint FittingIndicator = 1u;
const uint FittingBeaconRed = 2u;
const uint FittingBeaconBlue = 3u;

// CarLamp, by number.
const uint LampIndicator = 0u;
const uint LampBrakeRed = 1u;
const uint LampReverseWhite = 2u;
const uint LampBeaconRed = 3u;
const uint LampBeaconBlue = 4u;
const uint LampBeaconAmber = 5u;

// Everything Showing reads of a car: its command, its priority, its pass and the junction its line enters.
struct CarLampFacts
{
    bool broken;
    bool driven;
    bool hand;
    bool reverse;
    bool blueLight;
    bool atWork;
    bool passBegun;
    bool turningAtTheBox;
    bool lineIsReverse;
    float brakeMps2;
    float progressM;
    float alongMps;
    float passSteppedOutM;
    float passBackM;
    float passEndsM;
    float passAsideM;
    float contextPassAsideM;
    float toTheBoxM;
    uint firstArc;
    uint arcCount;
};

// CarLamps.Towards: the indicator on the side of the car something this far across its line is, the right where positive.
uint lampsTowards(float asideM) { return asideM > 0.0 ? LampTurnRight : LampTurnLeft; }

// CarLamps.OnThePass (CAR-14.7).
uint lampsOnThePass(CarLampFacts car)
{
    if (car.progressM < car.passSteppedOutM) return lampsTowards(car.passAsideM);

    precise float leadM = max(0.0, car.alongMps) * LAMP_FIGURES.stepBackLeadS;
    precise float backFromM = car.passBackM - leadM;
    float sayBackFromM = max(car.passSteppedOutM, backFromM);
    return car.progressM >= sayBackFromM ? lampsTowards(-car.passAsideM) : 0u;
}

// CarLamps.TurnAheadRad: how far the line bends over the next aheadM of it, summed along its arcs.
float lampsTurnAheadRad(uint firstArc, uint arcCount, float progressM, float aheadM)
{
    precise float turnRad = 0.0;
    precise float startM = 0.0;
    precise float lastM = progressM + aheadM;
    for (uint at = 0u; at < arcCount; at++)
    {
        if (startM >= lastM) break;

        Arc arc = LAMP_LINE_ARCS[firstArc + at];
        precise float endM = startM + arcLengthM(arc);
        float fromM = max(startM, progressM);
        float toM = min(endM, lastM);
        if (toM > fromM)
        {
            precise float acrossM = toM - fromM;
            precise float bentRad = arcCurvature(arc) * acrossM;
            turnRad = turnRad + bentRad;
        }

        startM = endM;
    }

    return turnRad;
}

// CarLamps.Showing: what this car's lamps say this tick, as a CarLampSet.
uint lampsShowing(CarLampFacts car)
{
    if (car.broken || (!car.driven && !car.hand)) return 0u;

    uint saying = 0u;
    if (car.brakeMps2 > LAMP_FIGURES.lampBrakeMps2) saying |= LampBrake;
    if (car.reverse) saying |= LampReverse;
    if (car.blueLight || car.hand) saying |= LampBeacon;
    if (car.atWork) saying |= LampWorks;

    if (car.passBegun && car.progressM < car.passEndsM) return saying | lampsOnThePass(car);
    if (car.contextPassAsideM != 0.0) return saying | lampsTowards(car.contextPassAsideM);
    if (!car.turningAtTheBox || car.toTheBoxM > LAMP_FIGURES.junctionAheadM) return saying;

    float turnRad = lampsTurnAheadRad(car.firstArc, car.arcCount, car.progressM, LAMP_FIGURES.turnAheadM);
    if (car.lineIsReverse) turnRad = -turnRad;

    if (turnRad >= LAMP_FIGURES.indicateRad) saying |= LampTurnRight;
    else if (turnRad <= -LAMP_FIGURES.indicateRad) saying |= LampTurnLeft;

    return saying;
}

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
    bool swapped;
};

CarLampMoment lampsMoment(uint showing, float elapsedS, float rateJitter)
{
    CarLampMoment moment;
    moment.showing = showing;
    moment.braking = (showing & LampBrake) != 0u;
    moment.rearOn = moment.braking || (showing & LampReverse) != 0u;
    moment.indicating = showing & (LampTurnLeft | LampTurnRight);

    precise float flashHz = LAMP_FIGURES.flashHz * rateJitter;
    moment.flashOn = moment.indicating != 0u && lampsPhase(elapsedS, flashHz) < LAMP_FIGURES.flashOnShare;
    moment.beaconOn = (showing & LampBeacon) != 0u;
    moment.amberOn = moment.beaconOn || (showing & LampWorks) != 0u;

    precise float beaconHz = LAMP_FIGURES.beaconHz * rateJitter;
    moment.firstHalf = lampsPhase(elapsedS, beaconHz) < 0.5;
    moment.swapped = moment.beaconOn && moment.firstHalf;
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
        lit = moment.beaconOn;
        return moment.swapped ? LampBeaconBlue : LampBeaconRed;
    }

    if (fitting == FittingBeaconBlue)
    {
        lit = moment.beaconOn;
        return moment.swapped ? LampBeaconRed : LampBeaconBlue;
    }

    lit = moment.amberOn && moment.firstHalf != onTheRight;
    return LampBeaconAmber;
}
