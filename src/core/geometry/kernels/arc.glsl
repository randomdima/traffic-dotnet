// ArcSeg on the device: one piece of a chain, where it puts a point a distance along it and which of its points is
// nearest a place. Each function follows its C# counterpart line for line (ArcSeg.FromStartM, Spline.NearestOnArc,
// NearestOnFlat), `precise` wherever the host's arithmetic would round twice. sin, cos and atan are the device's own.
//
// A piece is two words of four: (start.xy, the unit vector of its first heading) and (heading, length, curvature, 0).
// The unit vector is the host's, laid with the piece, so a straight is answered to the host's float. A chain of them
// is arc_chain.glsl's.

struct Arc
{
    vec4 a;
    vec4 b;
};

const float ArcStraightCurvature = 1e-6;
const float ArcFlatCurvature = 1e-4;
const int ArcFlatSteps = 3;
const float ArcSeriesLimitRad = 0.6;
const float ArcTau = 6.28318530717958647692;

vec2 arcStart(Arc arc) { return arc.a.xy; }
vec2 arcStartUnit(Arc arc) { return arc.a.zw; }
float arcHeadingRad(Arc arc) { return arc.b.x; }
float arcLengthM(Arc arc) { return arc.b.y; }
float arcCurvature(Arc arc) { return arc.b.z; }

float arcSinc(float x)
{
    if (abs(x) >= ArcSeriesLimitRad)
    {
        precise float s = sin(x) / x;
        return s;
    }

    precise float square = x * x;
    precise float series = 1.0 - square * (1.0 / 6.0) + square * square * (1.0 / 120.0) - square * square * square * (1.0 / 5040.0);
    return series;
}

vec2 arcFromStart(Arc arc, float distanceM)
{
    precise float halfTurnRad = arcCurvature(arc) * distanceM * 0.5;
    if (halfTurnRad == 0.0)
    {
        precise vec2 straight = distanceM * arcStartUnit(arc);
        return straight;
    }

    precise float chordM = distanceM * arcSinc(halfTurnRad);
    precise float heading = arcHeadingRad(arc) + halfTurnRad;
    precise vec2 r = chordM * vec2(cos(heading), sin(heading));
    return r;
}

vec2 arcPointAt(Arc arc, float distanceM)
{
    precise vec2 p = arcStart(arc) + arcFromStart(arc, distanceM);
    return p;
}

float arcHeadingAt(Arc arc, float distanceM)
{
    precise float h = arcHeadingRad(arc) + arcCurvature(arc) * distanceM;
    return h;
}

float arcDot(vec2 a, vec2 b)
{
    precise float d = a.x * b.x + a.y * b.y;
    return d;
}

float arcNearestOnFlat(Arc arc, vec2 pointM)
{
    precise vec2 offM = pointM - arcStart(arc);
    float alongM = clamp(arcDot(offM, arcStartUnit(arc)), 0.0, arcLengthM(arc));
    for (int step = 0; step < ArcFlatSteps; step++)
    {
        float h = arcHeadingAt(arc, alongM);
        vec2 heading = vec2(cos(h), sin(h));
        precise vec2 left = offM - arcFromStart(arc, alongM);
        precise float moved = alongM + arcDot(left, heading);
        alongM = clamp(moved, 0.0, arcLengthM(arc));
    }

    return alongM;
}

float arcNearest(Arc arc, vec2 pointM)
{
    vec2 along = arcStartUnit(arc);
    float curvature = arcCurvature(arc);
    if (abs(curvature) < ArcStraightCurvature) return clamp(arcDot(pointM - arcStart(arc), along), 0.0, arcLengthM(arc));

    if (abs(curvature) < ArcFlatCurvature) return arcNearestOnFlat(arc, pointM);

    precise float radius = 1.0 / curvature;
    precise vec2 centreM = arcStart(arc) + radius * vec2(-along.y, along.x);
    precise vec2 fromCentre = arcStart(arc) - centreM;
    precise vec2 toPoint = pointM - centreM;
    if (arcDot(toPoint, toPoint) < 1e-10) return 0.0;

    precise float crossed = fromCentre.x * toPoint.y - fromCentre.y * toPoint.x;
    float turnRad = atan(crossed, arcDot(fromCentre, toPoint));
    precise float alongM = turnRad / curvature;
    if (alongM < 0.0)
    {
        precise float round_ = alongM + ArcTau / abs(curvature);
        alongM = round_ <= arcLengthM(arc) ? round_ : 0.0;
    }

    return clamp(alongM, 0.0, arcLengthM(arc));
}
