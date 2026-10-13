// DriftMarks on the device: the ring of marks the tyres lay, kept where it is drawn from rather than handed to the host
// and back. Its rows are drift_mark_rows.glsl's; the ring's word block carries the draw a frame makes of it first, so the
// block is the indirect buffer of that draw. Nothing decides on a mark, so nothing here is held to the host to the bit:
// the order a tick's marks land in is the order the threads claimed their slots.
//
// The including kernel includes drift_mark_rows.glsl, declares its buffers over the two rows, and defines
// DRIFT_MARK_RING (a DriftMarkRing) before this — and DRIFT_MARK_ROWS (DriftMarkRow[]) where it lays marks.

#include "world/terrain/kernels/drift_mark_rows.glsl"

#ifdef DRIFT_MARK_ROWS

// DriftMarks.Mark's shortest stretch: under it there is no mark to speak of.
const float DriftMarkLeastM = 1e-3;

// One stretch from where the wheel began it to where it is, laid at the next place this tick has not claimed — or
// nothing, as DriftMarks.Mark lays nothing, where it is too short, too faint or too narrow.
void driftMarkLay(vec2 fromM, vec2 toM, float widthM, float intensity, bool ploughed)
{
    vec2 spanM = toM - fromM;
    float lengthM = length(spanM);
    if (lengthM < DriftMarkLeastM || intensity <= 0.0 || widthM <= 0.0) return;

    uint claimed = atomicAdd(DRIFT_MARK_RING.claimed, 1u);
    float strength = clamp(intensity, 0.0, 1.0);
    DRIFT_MARK_ROWS[(DRIFT_MARK_RING.head + claimed) % DRIFT_MARK_RING.capacity] = DriftMarkRow(
        (fromM + toM) * 0.5, lengthM, widthM, atan(spanM.y, spanM.x), ploughed ? -strength : strength);
}

#endif

// The tick's marks taken into the ring, by the one thread of a pass that runs after every lay: the head moved past them,
// the count of what is written grown to at most the ring, the claims let go, and the frame's draw told how many.
void driftMarksSettle()
{
    uint claimed = DRIFT_MARK_RING.claimed;
    uint capacity = DRIFT_MARK_RING.capacity;
    uint laid = min(DRIFT_MARK_RING.laid + min(claimed, capacity), capacity);
    DRIFT_MARK_RING.head = (DRIFT_MARK_RING.head + claimed) % capacity;
    DRIFT_MARK_RING.laid = laid;
    DRIFT_MARK_RING.claimed = 0u;
    DRIFT_MARK_RING.draw = uvec4(4u, laid, 0u, 0u);
}
