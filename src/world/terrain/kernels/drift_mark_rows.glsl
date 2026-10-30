// The ring of marks' two rows (drift_marks.glsl), for a kernel to declare its buffers over and a stage that draws marks
// to read: DriftMarks.DriftKnot and DriftMarks.DriftMarkRing, field for field.

#ifndef DRIFT_MARK_ROWS_GLSL
#define DRIFT_MARK_ROWS_GLSL

// One knot of a mark's stroke: where the wheel was, the way it was travelling, the width of the tyre — negative where it
// was laid on the level above the ground — how hard it worked the ground — negative where it ploughed rather than laid
// rubber — and the knot before it on its stroke, by its place among every knot laid, or DriftNoKnot where it starts one.
struct DriftKnot
{
    vec2 atM;
    float headingRad;
    float widthM;
    float strength;
    uint previous;
};

// DriftMarks.NoKnot.
const uint DriftNoKnot = 0xFFFFFFFFu;

// DriftMarkRing.Pieces and StripCorners: a knot's curve in so many pieces, a strip of two corners a piece and two more.
const uint DriftKnotPieces = 8u;
const uint DriftKnotCorners = 2u * (DriftKnotPieces + 1u);

// The ring's words: the frame's draw of it (VkDrawIndirectCommand, its instance count the knots laid), every knot laid
// since the ring was cleared, how many places are written, how many it holds, and how many this tick has claimed so far.
struct DriftMarkRing
{
    uvec4 draw;
    uint total;
    uint laid;
    uint capacity;
    uint claimed;
};

#endif
