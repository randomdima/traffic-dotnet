// What a car follows and where it was read (LineFollowed), field for field: the lane, the join off it, where the stretch
// stops, what the reading is taken around, the reading, and the serial of the follow it was taken against.

#ifndef LINE_FOLLOWED_GLSL
#define LINE_FOLLOWED_GLSL

struct LineFollowedRow
{
    int lane;
    int join;
    float stopsAtM;
    float aroundM;
    float progressM;
    float offLineM;
    uint serial;
    uint pad;
};

#endif
