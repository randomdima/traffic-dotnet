// A chain of arcs on the device (Spline.ProjectM, SampleAt): a run [first, end) of pieces in the buffer the including
// kernel names SPLINE_ARCS, declared before this is included. The pieces' own arithmetic is arc.glsl's.

// The distance along the chain [first, end) whose point is nearest the one given, searched within windowM of aroundM,
// and how far off it landed, squared (Spline.ProjectM).
float splineProject(uint first, uint end, vec2 pointM, float aroundM, float windowM, out float offSq)
{
    float fromM = max(0.0, aroundM - windowM);
    precise float toM = aroundM + windowM;
    float bestM = fromM;
    float bestSq = 3.402823466e+38;
    float startM = 0.0;
    for (uint at = first; at < end; at++)
    {
        Arc arc = SPLINE_ARCS[at];
        precise float endM = startM + arcLengthM(arc);
        if (endM >= fromM && startM <= toM)
        {
            float alongM = arcNearest(arc, pointM);
            precise float onChain = startM + alongM;
            precise float clamped = clamp(onChain, fromM, toM) - startM;
            precise vec2 off = arcPointAt(arc, clamped) - pointM;
            precise float distanceSq = off.x * off.x + off.y * off.y;
            if (distanceSq < bestSq)
            {
                bestSq = distanceSq;
                precise float atM = startM + clamped;
                bestM = atM;
            }
        }

        startM = endM;
    }

    offSq = bestSq;
    return bestM;
}

// The chain [first, end) at one distance from its start, clamped to its ends (Spline.SampleAt).
void splineSample(uint first, uint end, float distanceM, out vec2 positionM, out float headingRad)
{
    positionM = vec2(0.0);
    headingRad = 0.0;
    float remainingM = max(0.0, distanceM);
    for (uint at = first; at < end; at++)
    {
        Arc arc = SPLINE_ARCS[at];
        if (remainingM > arcLengthM(arc) && at < end - 1u)
        {
            remainingM -= arcLengthM(arc);
            continue;
        }

        float alongM = min(remainingM, arcLengthM(arc));
        positionM = arcPointAt(arc, alongM);
        headingRad = arcHeadingAt(arc, alongM);
        return;
    }
}

// The same sample with the bend of the arc it falls on (SplineSample.Curvature), nought on an empty chain.
void splineSampleBent(uint first, uint end, float distanceM, out vec2 positionM, out float headingRad, out float curvature)
{
    positionM = vec2(0.0);
    headingRad = 0.0;
    curvature = 0.0;
    float remainingM = max(0.0, distanceM);
    for (uint at = first; at < end; at++)
    {
        Arc arc = SPLINE_ARCS[at];
        if (remainingM > arcLengthM(arc) && at < end - 1u)
        {
            remainingM -= arcLengthM(arc);
            continue;
        }

        float alongM = min(remainingM, arcLengthM(arc));
        positionM = arcPointAt(arc, alongM);
        headingRad = arcHeadingAt(arc, alongM);
        curvature = arcCurvature(arc);
        return;
    }
}
