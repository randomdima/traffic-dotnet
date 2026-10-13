// The ring of marks' two rows (drift_marks.glsl), for a kernel to declare its buffers over and a stage that draws marks
// to read: DriftMarks.DriftMarkRow and DriftMarks.RingWords, field for field.

#ifndef DRIFT_MARK_ROWS_GLSL
#define DRIFT_MARK_ROWS_GLSL

// One stretch of mark as it is drawn: its middle, its length along the travel, the width of the tyre that made it, its
// bearing, and how hard the tyre worked the ground — negative where it ploughed rather than laid rubber.
struct DriftMarkRow
{
    vec2 centreM;
    float lengthM;
    float widthM;
    float headingRad;
    float strength;
};

// The ring's words: the frame's draw of it (VkDrawIndirectCommand, its instance count the marks laid), the place the
// next mark goes, how many places are written, how many it holds, and how many this tick has claimed so far.
struct DriftMarkRing
{
    uvec4 draw;
    uint head;
    uint laid;
    uint capacity;
    uint claimed;
};

#endif
