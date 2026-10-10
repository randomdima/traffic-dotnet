// Overtake on the device (CAR-46): a car's line moved across onto the lane beside for a stretch and back, at the rear
// axle and in the line's own metres — how far across, how steeply and how that slope turns at a metre, where the axle
// stands and which way the car points there, and how the pass bends. Line for line with the host, `precise` where it
// rounds twice; its trigonometry is trig.glsl's and its quotients and roots rounded.glsl's.
//
// The including kernel includes arc.glsl and arc_chain.glsl (its SPLINE_ARCS the lines) first.

#ifndef OVERTAKE_GLSL
#define OVERTAKE_GLSL

// A pass (Overtake), field for field with the host's row: begun is a bool.
struct OvertakeRow
{
    int lane;
    float outM;
    float outStepM;
    float backM;
    float backStepM;
    float asideM;
    float outMps;
    float backMps;
    float clearsM;
    uint begun;
    float pad0;
    float pad1;
};

const float OvertakeTau = 6.28318530717958647692;

OvertakeRow overtakeNone() { return OvertakeRow(-1, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0u, 0.0, 0.0); }

bool overtakeBegun(OvertakeRow pass) { return pass.begun != 0u; }

float overtakeSteppedOutM(OvertakeRow pass)
{
    precise float atM = pass.outM + pass.outStepM;
    return atM;
}

float overtakeEndsM(OvertakeRow pass)
{
    precise float atM = pass.backM + pass.backStepM;
    return atM;
}

// How much of the way across a step is share of the way into it (Overtake.Rise).
float overtakeRise(float share)
{
    precise float turned = OvertakeTau * share;
    precise float rise = share - roundedQuotient(trigSin(turned), OvertakeTau);
    return rise;
}

// And how steeply, in the step's own across for its own length (Overtake.RiseSlope).
float overtakeRiseSlope(float share)
{
    float sine;
    float cosine;
    precise float turned = OvertakeTau * share;
    trigSinCos(turned, sine, cosine);
    precise float slope = 1.0 - cosine;
    return slope;
}

float overtakeTurnOfAStep(OvertakeRow pass, float intoM, float stepM)
{
    float turned = roundedQuotient(OvertakeTau * intoM, stepM);
    precise float turn = roundedQuotient(pass.asideM, stepM * stepM) * OvertakeTau * trigSin(turned);
    return turn;
}

// How far across its line the rear axle is aimed at this metre of it (Overtake.AsideAtM).
float overtakeAsideAtM(OvertakeRow pass, float atM)
{
    if (atM <= pass.outM || atM >= overtakeEndsM(pass)) return 0.0;
    if (atM < overtakeSteppedOutM(pass))
    {
        float share = roundedQuotient(atM - pass.outM, pass.outStepM);
        precise float aside = pass.asideM * overtakeRise(share);
        return aside;
    }

    if (atM <= pass.backM) return pass.asideM;

    float share = roundedQuotient(atM - pass.backM, pass.backStepM);
    precise float aside = pass.asideM * (1.0 - overtakeRise(share));
    return aside;
}

// And how steeply: metres across for a metre along (Overtake.SlopeAtM).
float overtakeSlopeAtM(OvertakeRow pass, float atM)
{
    if (atM <= pass.outM || atM >= overtakeEndsM(pass)) return 0.0;
    if (atM < overtakeSteppedOutM(pass))
    {
        float share = roundedQuotient(atM - pass.outM, pass.outStepM);
        precise float slope = roundedQuotient(pass.asideM, pass.outStepM) * overtakeRiseSlope(share);
        return slope;
    }

    if (atM <= pass.backM) return 0.0;

    float share = roundedQuotient(atM - pass.backM, pass.backStepM);
    precise float slope = roundedQuotient(-pass.asideM, pass.backStepM) * overtakeRiseSlope(share);
    return slope;
}

// And how that slope is changing, a metre along (Overtake.TurnAtM).
float overtakeTurnAtM(OvertakeRow pass, float atM)
{
    if (atM <= pass.outM || atM >= overtakeEndsM(pass)) return 0.0;
    if (atM < overtakeSteppedOutM(pass)) return overtakeTurnOfAStep(pass, atM - pass.outM, pass.outStepM);
    if (atM <= pass.backM) return 0.0;

    return -overtakeTurnOfAStep(pass, atM - pass.backM, pass.backStepM);
}

// The most the pass may be driven at, at this metre of it (Overtake.MostMpsAtM).
float overtakeMostMpsAtM(OvertakeRow pass, float atM)
{
    if (atM < overtakeSteppedOutM(pass)) return pass.outMps;

    return atM >= pass.backM ? pass.backMps : uintBitsToFloat(0x7F800000u);
}

// Where the rear axle stands at this metre of the line, and which way the car points (Overtake.PoseAtM).
void overtakePoseAtM(OvertakeRow pass, uint first, uint end, float atM, out vec2 axleM, out vec2 forward)
{
    vec2 onM;
    float headingRad;
    float curvature;
    splineSampleBent(first, end, atM, onM, headingRad, curvature);
    vec2 direction = trigUnit(headingRad);
    vec2 right = vec2(-direction.y, direction.x);
    float asideM = overtakeAsideAtM(pass, atM);
    precise vec2 axle = onM + (right * asideM);
    axleM = axle;
    precise vec2 pointing = (direction * (1.0 - (curvature * asideM))) + (right * overtakeSlopeAtM(pass, atM));
    precise float lengthSquared = pointing.x * pointing.x + pointing.y * pointing.y;
    forward = roundedQuotient(pointing, roundedSqrt(lengthSquared));
}

// How the pass bends at this metre of the line, the line's own bend under it included, signed as the line's
// (Overtake.BendAtM).
float overtakeBendAtM(OvertakeRow pass, uint first, uint end, float atM)
{
    vec2 onM;
    float headingRad;
    float lineBend;
    splineSampleBent(first, end, atM, onM, headingRad, lineBend);
    float slope = overtakeSlopeAtM(pass, atM);
    precise float along = 1.0 - (lineBend * overtakeAsideAtM(pass, atM));
    precise float alongSquared = along * along;
    precise float bent = (alongSquared * lineBend) + (along * overtakeTurnAtM(pass, atM)) + (2.0 * lineBend * slope * slope);
    precise float stretch = alongSquared + (slope * slope);
    precise float under = stretch * roundedSqrt(stretch);
    return roundedQuotient(bent, under);
}

#endif
