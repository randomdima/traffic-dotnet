// Ground swept once and committed to, as rows on the device (SweptGround), field for field with SweptRun.

#ifndef SWEPT_ROWS_GLSL
#define SWEPT_ROWS_GLSL

// One way under a swept body: the stretch of it covered from the station firstAtM to lastAtM along the path (SweptRun).
struct SweptRun
{
    int way;
    float fromM;
    float toM;
    float firstAtM;
    float lastAtM;
};

#endif
