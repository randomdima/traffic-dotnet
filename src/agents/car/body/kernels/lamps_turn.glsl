// CarLamps.TurnAheadRad on the device: how far a car's line bends over the next metres of it, line for line with the
// host's, `precise` throughout — what lamps.glsl's facts are handed as turnAheadRad.
//
// The including kernel supplies LAMP_LINE_ARCS, the fleet's lines as arc.glsl's Arc; arc.glsl is included first.

// How far the line of arcCount arcs from firstArc bends over the next aheadM past progressM, summed along its arcs.
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
