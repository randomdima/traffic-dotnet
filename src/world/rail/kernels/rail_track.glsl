// The railway on the device (RailGraph, TrackLine; DeviceWorld.Rail): every segment's points, laid once, and the
// switches as the trains last set them (TRN-9) — what a rail vehicle's wheels are walked along. Each function follows its
// C# counterpart line for line, `precise` where the host rounds twice and every quotient and root rounded.glsl's, so a
// wheel stands where the host's does to the bit.
//
// The including kernel binds, by these names: railPoints (vec4 — a point's place, and how far along its segment it
// stands), railSegments (ivec4 — its first point, how many, and the node at its start and at its end), railNodes (ivec4 —
// its first end, how many, and its switch or −1), railEnds (int — each node's segment ends, the common end first at a
// switch) and railSwitches (uint — each switch's position) — and includes shape.glsl, which has no guard of its own, first.

#ifndef RAIL_TRACK_GLSL
#define RAIL_TRACK_GLSL

#include "core/geometry/kernels/rounded.glsl"

// RailGraph.Nowhere, and RailGraph.MostSegmentsAWheelCrosses.
const int RailNowhere = -1;
const int RailMostSegmentsAWheelCrosses = 4;

vec2 railPointOf(int segment, int point) { return railPoints[railSegments[segment].x + point].xy; }

float railAtOf(int segment, int point) { return railPoints[railSegments[segment].x + point].z; }

int railPointsOf(int segment) { return railSegments[segment].y; }

float railLengthOf(int segment) { return railAtOf(segment, railPointsOf(segment) - 1); }

// TrackLine.ChordAt: the last point at or before a distance along, short of the last point.
int railChordAt(int segment, float alongM)
{
    int low = 0;
    int high = railPointsOf(segment) - 1;
    while (low < high)
    {
        int middle = (low + high + 1) / 2;
        if (railAtOf(segment, middle) <= alongM) low = middle;
        else high = middle - 1;
    }

    return clamp(low, 0, railPointsOf(segment) - 2);
}

// TrackLine.PlaceAt.
vec2 railPlaceAt(int segment, float alongM)
{
    int chord = railChordAt(segment, alongM);
    precise float lengthM = railAtOf(segment, chord + 1) - railAtOf(segment, chord);
    precise float into = alongM - railAtOf(segment, chord);
    float share = lengthM > 0.0 ? clamp(roundedQuotient(into, lengthM), 0.0, 1.0) : 0.0;
    vec2 from = railPointOf(segment, chord);
    precise vec2 r = from + ((railPointOf(segment, chord + 1) - from) * share);
    return r;
}

// TrackLine.NearestAlongM: a walk of every chord, for a wheel put down.
float railNearestAlong(int segment, vec2 pointM)
{
    float bestM = 0.0;
    float bestAwayM2 = floatInfinity();
    for (int chord = 0; chord + 1 < railPointsOf(segment); chord++)
    {
        vec2 from = railPointOf(segment, chord);
        precise vec2 runM = railPointOf(segment, chord + 1) - from;
        float lengthM2 = shapeDot(runM, runM);
        float share = lengthM2 > 0.0 ? clamp(roundedQuotient(shapeDot(pointM - from, runM), lengthM2), 0.0, 1.0) : 0.0;
        precise vec2 on = from + (runM * share);
        precise vec2 away = on - pointM;
        float awayM2 = shapeDot(away, away);
        if (awayM2 >= bestAwayM2) continue;

        precise float atM = railAtOf(segment, chord) + (share * roundedSqrt(lengthM2));
        bestM = atM;
        bestAwayM2 = awayM2;
    }

    return bestM;
}

// TrackLine.NearestAlongFromM: chord to chord from where the wheel last stood, never back; how far past either end in
// beyondM, negative before the start.
float railNearestAlongFrom(int segment, vec2 pointM, float nearM, out float beyondM)
{
    int chord = railChordAt(segment, nearM);
    int step = 0;
    int last = railPointsOf(segment) - 2;
    for (int walked = 0; walked <= railPointsOf(segment); walked++)
    {
        vec2 from = railPointOf(segment, chord);
        precise vec2 runM = railPointOf(segment, chord + 1) - from;
        precise float lengthM = railAtOf(segment, chord + 1) - railAtOf(segment, chord);
        float alongChordM = lengthM > 0.0 ? roundedQuotient(shapeDot(pointM - from, runM), lengthM) : 0.0;
        if (alongChordM < 0.0 && chord > 0 && step <= 0)
        {
            chord--;
            step = -1;
            continue;
        }

        if (alongChordM > lengthM && chord < last && step >= 0)
        {
            chord++;
            step = 1;
            continue;
        }

        precise float pastM = alongChordM - lengthM;
        beyondM = alongChordM < 0.0 && chord == 0 ? alongChordM : alongChordM > lengthM && chord == last ? pastM : 0.0;
        precise float atM = railAtOf(segment, chord) + clamp(alongChordM, 0.0, lengthM);
        return atM;
    }

    beyondM = 0.0;
    return nearM;
}

// TrackLine.ChordWay.
vec2 railChordWay(int segment, int chord)
{
    precise vec2 runM = railPointOf(segment, chord + 1) - railPointOf(segment, chord);
    float lengthM = roundedLength(runM);
    return lengthM > 0.0 ? roundedQuotient(runM, lengthM) : vec2(1.0, 0.0);
}

// TrackLine.Eased.
vec2 railEased(vec2 one, vec2 other, float share)
{
    precise vec2 blended = (one * (1.0 - share)) + (other * share);
    float lengthM = roundedLength(blended);
    return lengthM > 0.0 ? roundedQuotient(blended, lengthM) : other;
}

// TrackLine.EasedAlongAt.
vec2 railEasedAlongAt(int segment, float alongM)
{
    int chord = railChordAt(segment, alongM);
    precise float lengthM = railAtOf(segment, chord + 1) - railAtOf(segment, chord);
    precise float into = alongM - railAtOf(segment, chord);
    float intoM = clamp(into, 0.0, lengthM);
    vec2 way = railChordWay(segment, chord);
    if (chord > 0)
    {
        precise float before = railAtOf(segment, chord) - railAtOf(segment, chord - 1);
        precise float windowM = min(before, lengthM) * 0.5;
        precise float half_ = 0.5 * intoM;
        precise float share = 0.5 + roundedQuotient(half_, windowM);
        if (intoM < windowM) return railEased(railChordWay(segment, chord - 1), way, share);
    }

    if (chord + 2 < railPointsOf(segment))
    {
        precise float after = railAtOf(segment, chord + 2) - railAtOf(segment, chord + 1);
        precise float windowM = min(after, lengthM) * 0.5;
        precise float leftM = lengthM - intoM;
        precise float half_ = 0.5 * leftM;
        precise float share = 0.5 + roundedQuotient(half_, windowM);
        if (leftM < windowM) return railEased(railChordWay(segment, chord + 1), way, share);
    }

    return way;
}

// RailGraph.NextEnd: the segment end a wheel runs out of one onto, with the switches as set.
int railNextEnd(int end)
{
    int node = end % 2 == 0 ? railSegments[end / 2].z : railSegments[end / 2].w;
    ivec4 n = railNodes[node];
    int first = n.x;
    int count = n.y;
    if (count < 2) return RailNowhere;
    if (count == 2) return railEnds[first] == end ? railEnds[first + 1] : railEnds[first];
    if (railEnds[first] != end) return railEnds[first];

    return railEnds[first + 1 + min(int(railSwitches[n.z]), count - 2)];
}

// RailGraph.Nearest: a walk of every segment, for a wheel put down.
ivec2 railNearest(vec2 pointM, int segments)
{
    int best = 0;
    float bestAlongM = 0.0;
    float bestAwayM2 = floatInfinity();
    for (int segment = 0; segment < segments; segment++)
    {
        float alongM = railNearestAlong(segment, pointM);
        precise vec2 away = railPlaceAt(segment, alongM) - pointM;
        float awayM2 = shapeDot(away, away);
        if (awayM2 < bestAwayM2)
        {
            best = segment;
            bestAlongM = alongM;
            bestAwayM2 = awayM2;
        }
    }

    return ivec2(best, floatBitsToInt(bestAlongM));
}

// RailGraph.Follow: a wheel's cursor (its segment, and its distance along it as a float's bits) walked on to where its
// pivot is; the place on the track there in onM, and which way the track runs in way.
ivec2 railFollow(ivec2 cursor, vec2 pivotM, int segments, out vec2 onM, out vec2 way)
{
    if (cursor.x < 0) cursor = railNearest(pivotM, segments);

    for (int step = 0; step <= RailMostSegmentsAWheelCrosses; step++)
    {
        float beyondM;
        float alongM = railNearestAlongFrom(cursor.x, pivotM, intBitsToFloat(cursor.y), beyondM);
        cursor.y = floatBitsToInt(alongM);
        if (beyondM == 0.0) break;

        int next = railNextEnd((cursor.x * 2) + (beyondM > 0.0 ? 1 : 0));
        if (next == RailNowhere) break;

        cursor.x = next / 2;
        cursor.y = floatBitsToInt(next % 2 == 0 ? 0.0 : railLengthOf(cursor.x));
    }

    onM = railPlaceAt(cursor.x, intBitsToFloat(cursor.y));
    way = railEasedAlongAt(cursor.x, intBitsToFloat(cursor.y));
    return cursor;
}

#endif
