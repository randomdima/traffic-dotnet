// One way's section a mark links another's stretch to (WayCrossings' CrossedSection), filed under that other way: the
// way it is on, the stretch of that way, and the stretch of its own.

#ifndef CROSSED_SECTION_GLSL
#define CROSSED_SECTION_GLSL

struct CrossedSection
{
    int onWay;
    float fromM;
    float toM;
    float mineFromM;
    float mineToM;
};

#endif
