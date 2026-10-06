// SignalCycle on the device: which colour an axis of a junction is showing at a moment, what the crossing over a road on
// it shows, and what a light's stretch is shown through the town's own lookups (SignalCycle.ForAxis, ForCrossing;
// SignalService.ForApproach, ForCrossing; SignalHolds.ColourOf). Line for line with the host and `precise` throughout:
// there is no transcendental in a light, so its colour is the host's at every edge of the cycle.
//
// The including kernel supplies SIGNAL_FIGURES' fields: cycleS, amberTailS, laneCount, crossingCount — and the
// service's tables (SignalService.Table) as SIGNAL_LANE_AXIS, SIGNAL_LANE_JUNCTION, SIGNAL_OFFSETS, SIGNAL_CROSSING_AXIS
// and SIGNAL_CROSSING_JUNCTION.

// SignalColour, by number.
const uint SignalRed = 0u;
const uint SignalAmber = 1u;
const uint SignalGreen = 2u;

const int SignalAxes = 2;
const int SignalNoAxis = -1;

uint signalForAxis(int axis, float offsetS, float timeS)
{
    float cycleS = SIGNAL_FIGURES.cycleS;
    if (cycleS <= 0.0) return SignalGreen;

    precise float phaseS = cycleS / float(SignalAxes);
    precise float atS = timeS + offsetS;
    precise float turnsS = floor(atS / cycleS) * cycleS;
    precise float intoCycleS = atS - turnsS;

    precise float phases = intoCycleS / phaseS;
    int greenAxis = min(int(phases), SignalAxes - 1);
    if (greenAxis != axis) return SignalRed;

    precise float intoS = intoCycleS - (float(greenAxis) * phaseS);
    precise float amberFromS = phaseS - SIGNAL_FIGURES.amberTailS;
    return intoS >= amberFromS ? SignalAmber : SignalGreen;
}

uint signalForCrossingOver(int roadAxis, float offsetS, float timeS)
{
    return signalForAxis(roadAxis, offsetS, timeS) == SignalRed ? SignalGreen : SignalRed;
}

int signalAxisOfLane(int lane)
{
    return lane >= 0 && lane < int(SIGNAL_FIGURES.laneCount) ? SIGNAL_LANE_AXIS[lane] : SignalNoAxis;
}

bool signalCrossingIsLit(int crossing)
{
    return crossing >= 0 && crossing < int(SIGNAL_FIGURES.crossingCount) && SIGNAL_CROSSING_AXIS[crossing] != SignalNoAxis;
}

uint signalForApproach(int lane, float timeS)
{
    int axis = signalAxisOfLane(lane);
    if (axis == SignalNoAxis) return SignalGreen;

    return signalForAxis(axis, SIGNAL_OFFSETS[SIGNAL_LANE_JUNCTION[lane]], timeS);
}

uint signalForCrossing(int crossing, float timeS)
{
    if (!signalCrossingIsLit(crossing)) return SignalRed;

    return signalForCrossingOver(SIGNAL_CROSSING_AXIS[crossing], SIGNAL_OFFSETS[SIGNAL_CROSSING_JUNCTION[crossing]], timeS);
}

// SignalHolds.ColourOf: an approach's colour or a crossing's paint's; a stretch is held on anything but green.
uint signalForStretch(int subject, bool forCars, float timeS)
{
    return forCars ? signalForApproach(subject, timeS) : signalForCrossing(subject, timeS);
}
