// LegProgress on the device: whether an actor is getting anywhere on its leg — the closest it has come to the end of what
// it is covering and how long since it did better — over one actor's clock, its two words handed in as the actor's table
// holds them (town_rows.glsl's cars_ClockSinceS, cars_ClockClosestM). Who touched a clock is the hybrid's bookkeeping, and
// not kept.

#ifndef LEG_PROGRESS_GLSL
#define LEG_PROGRESS_GLSL

// A fresh leg: nothing reached yet, and no time run up against it (float.MaxValue).
void legProgress_Restart(inout float sinceS, inout float closestM)
{
    sinceS = 0.0;
    closestM = 3.402823466e+38;
}

// How far is left this decision, and how much world has passed since the last; nearer by less than byM leaves the mark.
void legProgress_Note(inout float sinceS, inout float closestM, float remainingM, float byM, float sinceLastDecisionS)
{
    precise float nearerThanM = closestM - byM;
    if (remainingM < nearerThanM)
    {
        closestM = remainingM;
        sinceS = 0.0;
        return;
    }

    precise float ranS = sinceS + sinceLastDecisionS;
    sinceS = ranS;
}

// The clock held where the leg waits for what will end on its own: it buys the wait and not the standing.
void legProgress_Hold(inout float closestM, float remainingM) { closestM = min(closestM, remainingM); }

bool legProgress_IsStuck(float sinceS, float patienceS) { return sinceS >= patienceS; }

// Whether it would be stuck after this decision's Note — or, held, as it stands — without noting it.
bool legProgress_WouldBeStuck(float sinceS, float closestM, float remainingM, float byM, float sinceLastDecisionS, bool held, float patienceS)
{
    if (held) return legProgress_IsStuck(sinceS, patienceS);

    precise float nearerThanM = closestM - byM;
    precise float ranS = sinceS + sinceLastDecisionS;
    return remainingM >= nearerThanM && ranS >= patienceS;
}

#endif
