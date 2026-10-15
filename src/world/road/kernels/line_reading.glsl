// Where a car's rear axle stands on the lanes it follows (LineReading, LineFollowed), as the host reads it to the bit: the
// projection Spline.ProjectM walks over the line's own head, and the point Spline.SampleFrom finds where it landed. The
// stretch is walked off the lanes as LineAssembler lays it — a lane's arcs as they stand and the one its stretch stops in
// cut short (Spline.HeadInto), the join's whole, the next lane's — never off a copy of the car's line.
//
// The includer names the first of two bindings (LINE_READING_BINDING): the arcs, and the words that say where each lane's
// and join's begin, the lanes' lengths and the lane each join leads onto. It gets trig.glsl and rounded.glsl.

#ifndef LINE_READING_GLSL
#define LINE_READING_GLSL

#include "core/geometry/kernels/trig.glsl"
#include "core/geometry/kernels/rounded.glsl"
#include "world/road/kernels/line_followed.glsl"

// One arc as the device holds it (DeviceLanes.ArcRow): ArcSeg's start, its direction, its heading, length and curvature.
struct LaneArc
{
    vec2 startM;
    vec2 startUnit;
    float headingRad;
    float lengthM;
    float curvature;
    float pad;
};

// The lanes' arcs and then the joins'; and where each lane's arcs begin, the lanes' lengths, where each join's begin and the
// lane each join leads onto, in one run of words whose head names where each table and the joins' arcs begin
// (DeviceLanes.Words) — two bindings, where six would take more storage buffers than WebGPU gives a kernel.
layout(std430, binding = LINE_READING_BINDING) readonly buffer LineArcs { LaneArc lineArcs[]; };
layout(std430, binding = LINE_READING_BINDING + 1) readonly buffer LineWords { int lineWords[]; };

const int LineLaneArcFromAt = 0;
const int LineLaneLengthsAt = 1;
const int LineJoinArcFromAt = 2;
const int LineJoinToAt = 3;
const int LineJoinArcsAt = 4;

int lineWord(int table, int at) { return lineWords[lineWords[table] + at]; }

int laneArcFrom(int lane) { return lineWord(LineLaneArcFromAt, lane); }

float laneLengthM(int lane) { return intBitsToFloat(lineWord(LineLaneLengthsAt, lane)); }

int joinArcFrom(int join) { return lineWord(LineJoinArcFromAt, join); }

int joinTo(int join) { return lineWord(LineJoinToAt, join); }

LaneArc laneArc(int at) { return lineArcs[at]; }

LaneArc joinArc(int at) { return lineArcs[lineWords[LineJoinArcsAt] + at]; }

// ArcSeg's and Spline's figures, and C#'s 1f / 6f, 1f / 120f and 1f / 5040f and MathF.Tau to the bit.
const float LineStraightCurvature = 1e-6;
const float LineFlatCurvature = 1e-4;
const int LineFlatSteps = 3;
const float LineSeriesLimitRad = 0.6;
const float LineOnTheCentreM2 = 1e-10;

float lineSixth() { return uintBitsToFloat(0x3e2aaaabu); }
float lineHundredTwentieth() { return uintBitsToFloat(0x3c088889u); }
float lineFiveThousandFortieth() { return uintBitsToFloat(0x39500d01u); }
float lineTau() { return uintBitsToFloat(0x40c90fdbu); }
float lineMostFloat() { return floatGreatest(); }

// MathF.Max and MathF.Min: NaN wins, and of two zeros the positive is the greater.
float lineMax(float a, float b)
{
    if (floatIsNaN(a)) return a;
    if (floatIsNaN(b)) return b;
    if (a == b) return (floatBitsToUint(a) & 0x80000000u) != 0u ? b : a;
    return a > b ? a : b;
}

float lineMin(float a, float b)
{
    if (floatIsNaN(a)) return a;
    if (floatIsNaN(b)) return b;
    if (a == b) return (floatBitsToUint(a) & 0x80000000u) != 0u ? a : b;
    return a < b ? a : b;
}

// Math.Clamp.
float lineClamp(float value, float least, float most) { return value < least ? least : value > most ? most : value; }

float lineDot(vec2 a, vec2 b)
{
    precise float dot_ = a.x * b.x + a.y * b.y;
    return dot_;
}

float lineCross(vec2 a, vec2 b)
{
    precise float cross_ = a.x * b.y - a.y * b.x;
    return cross_;
}

// ArcSeg.Sinc.
float lineSinc(float x)
{
    if (abs(x) >= LineSeriesLimitRad) return roundedQuotient(trigSin(x), x);

    precise float square = x * x;
    precise float sinc = 1.0 - square * lineSixth() + square * square * lineHundredTwentieth()
                         - square * square * square * lineFiveThousandFortieth();
    return sinc;
}

// ArcSeg.FromStartM.
vec2 lineFromStart(LaneArc arc, float distanceM)
{
    precise float halfTurnRad = arc.curvature * distanceM * 0.5;
    if (halfTurnRad == 0.0)
    {
        precise vec2 straight = distanceM * arc.startUnit;
        return straight;
    }

    precise float chordM = distanceM * lineSinc(halfTurnRad);
    precise float turnedRad = arc.headingRad + halfTurnRad;
    precise vec2 chord = chordM * trigUnit(turnedRad);
    return chord;
}

vec2 linePointAt(LaneArc arc, float distanceM)
{
    precise vec2 pointM = arc.startM + lineFromStart(arc, distanceM);
    return pointM;
}

// Spline.NearestOnFlat.
float lineNearestOnFlat(LaneArc arc, vec2 pointM)
{
    precise vec2 offM = pointM - arc.startM;
    float alongM = lineClamp(lineDot(offM, arc.startUnit), 0.0, arc.lengthM);
    for (int step = 0; step < LineFlatSteps; step++)
    {
        precise float headingRad = arc.headingRad + arc.curvature * alongM;
        vec2 heading = trigUnit(headingRad);
        precise vec2 short_ = offM - lineFromStart(arc, alongM);
        precise float movedM = alongM + lineDot(short_, heading);
        alongM = lineClamp(movedM, 0.0, arc.lengthM);
    }

    return alongM;
}

// Spline.NearestOnArc.
float lineNearestOnArc(LaneArc arc, vec2 pointM)
{
    vec2 along = arc.startUnit;
    if (abs(arc.curvature) < LineStraightCurvature)
    {
        precise vec2 offM = pointM - arc.startM;
        return lineClamp(lineDot(offM, along), 0.0, arc.lengthM);
    }

    if (abs(arc.curvature) < LineFlatCurvature) return lineNearestOnFlat(arc, pointM);

    float radius = roundedQuotient(1.0, arc.curvature);
    precise vec2 centreM = arc.startM + radius * vec2(-along.y, along.x);
    precise vec2 fromCentre = arc.startM - centreM;
    precise vec2 toPoint = pointM - centreM;
    if (lineDot(toPoint, toPoint) < LineOnTheCentreM2) return 0.0;

    float turnRad = trigAtan2(lineCross(fromCentre, toPoint), lineDot(fromCentre, toPoint));
    float alongM = roundedQuotient(turnRad, arc.curvature);
    if (alongM < 0.0)
    {
        precise float roundM = alongM + roundedQuotient(lineTau(), abs(arc.curvature));
        alongM = roundM <= arc.lengthM ? roundM : 0.0;
    }

    return lineClamp(alongM, 0.0, arc.lengthM);
}

// A walk over the stretch's pieces in order: the lane's, the join's, the next lane's.
struct LinePieces
{
    int part;
    int at;
    int to;
    float inLaneM;
    float toM;
};

LinePieces lineBegin(LineFollowedRow followed)
{
    return LinePieces(
        0, laneArcFrom(followed.lane), laneArcFrom(followed.lane + 1), 0.0,
        followed.join < 0 ? followed.stopsAtM : laneLengthM(followed.lane));
}

bool lineNext(inout LinePieces pieces, LineFollowedRow followed, out LaneArc piece)
{
    for (;;)
    {
        if (pieces.at >= pieces.to)
        {
            if (pieces.part == 0 && followed.join >= 0)
            {
                pieces = LinePieces(1, joinArcFrom(followed.join), joinArcFrom(followed.join + 1), 0.0, 0.0);
                continue;
            }

            if (pieces.part == 1)
            {
                int lane = joinTo(followed.join);
                pieces = LinePieces(2, laneArcFrom(lane), laneArcFrom(lane + 1), 0.0, followed.stopsAtM);
                continue;
            }

            return false;
        }

        if (pieces.part == 1)
        {
            piece = joinArc(pieces.at++);
            return true;
        }

        LaneArc arc = laneArc(pieces.at++);
        float startM = pieces.inLaneM;
        precise float endM = startM + arc.lengthM;
        float takeToM = lineMin(pieces.toM, endM);
        pieces.inLaneM = endM;
        if (takeToM > startM)
        {
            piece = arc;
            if (takeToM != endM)
            {
                precise float cutM = takeToM - startM;
                piece.lengthM = cutM;
            }

            return true;
        }
    }
}

// LineReading.Read: the reading around followed.aroundM written into it.
void lineRead(inout LineFollowedRow followed, vec2 rearAxleM, float windowM)
{
    if (followed.lane < 0)
    {
        followed.progressM = floatNaN();
        followed.offLineM = floatNaN();
        return;
    }

    precise float fromM = lineMax(0.0, followed.aroundM - windowM);
    precise float toM = followed.aroundM + windowM;
    float bestM = fromM;
    float bestSq = lineMostFloat();
    float startM = 0.0;

    LaneArc piece;
    LinePieces pieces = lineBegin(followed);
    while (lineNext(pieces, followed, piece))
    {
        if (startM > toM) break;

        precise float endM = startM + piece.lengthM;
        if (endM >= fromM)
        {
            precise float onM = startM + lineNearestOnArc(piece, rearAxleM);
            precise float alongM = lineClamp(onM, fromM, toM) - startM;
            precise vec2 offM = linePointAt(piece, alongM) - rearAxleM;
            precise float distanceSq = offM.x * offM.x + offM.y * offM.y;
            if (distanceSq < bestSq)
            {
                bestSq = distanceSq;
                precise float atM = startM + alongM;
                bestM = atM;
            }
        }

        startM = endM;
    }

    float remainingM = lineMax(0.0, bestM);
    LaneArc held = LaneArc(vec2(0.0), vec2(0.0), 0.0, 0.0, 0.0, 0.0);
    float heldStartM = 0.0;
    bool holds = false;
    pieces = lineBegin(followed);
    while (lineNext(pieces, followed, piece))
    {
        if (!holds)
        {
            held = piece;
            holds = true;
            continue;
        }

        precise float pastM = remainingM - heldStartM;
        if (pastM <= held.lengthM) break;

        precise float nextStartM = heldStartM + held.lengthM;
        heldStartM = nextStartM;
        held = piece;
    }

    precise float intoM = remainingM - heldStartM;
    precise vec2 offLine = linePointAt(held, lineMin(intoM, held.lengthM)) - rearAxleM;
    followed.progressM = bestM;
    followed.offLineM = roundedLength(offLine);
}

// LineReading.Lead: the point a lookahead past the reading, and how far along it was taken — NaN where that runs past the
// last piece of the stretch.
vec2 lineLead(LineFollowedRow followed, float lookaheadM, out float atM)
{
    // float.NaN's own bits, as the managed world writes it.
    atM = floatOfBits(0xffc00000u);
    if (followed.lane < 0 || floatIsNaN(followed.progressM)) return vec2(0.0);

    precise float aheadM = followed.progressM + lookaheadM;
    float remainingM = lineMax(0.0, aheadM);
    LaneArc held = LaneArc(vec2(0.0), vec2(0.0), 0.0, 0.0, 0.0, 0.0);
    float heldStartM = 0.0;
    bool holds = false;
    LaneArc piece;
    LinePieces pieces = lineBegin(followed);
    while (lineNext(pieces, followed, piece))
    {
        if (!holds)
        {
            held = piece;
            holds = true;
            continue;
        }

        precise float pastM = remainingM - heldStartM;
        if (pastM <= held.lengthM) break;

        precise float nextStartM = heldStartM + held.lengthM;
        heldStartM = nextStartM;
        held = piece;
    }

    precise float intoM = remainingM - heldStartM;
    if (intoM > held.lengthM) return vec2(0.0);

    atM = aheadM;
    return linePointAt(held, lineMin(intoM, held.lengthM));
}

#endif
