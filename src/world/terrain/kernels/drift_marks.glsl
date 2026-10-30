// DriftMarks on the device: the ring of knots the tyres' strokes lay, kept where it is drawn from rather than handed to
// the host and back. Its rows are drift_mark_rows.glsl's; the ring's word block carries the draw a frame makes of it
// first, so the block is the indirect buffer of that draw. Nothing decides on a mark, so nothing here is held to the host
// to the bit: the order a tick's knots land in is the order the threads claimed their places.
//
// The including kernel includes drift_mark_rows.glsl, declares its buffers over the two rows, and defines
// DRIFT_MARK_RING (a DriftMarkRing) before this — and, where it lays knots, DRIFT_MARK_ROWS (DriftKnot[]),
// DRIFT_MARK_LASTS (uint[], each wheel's stroke's last knot, DriftNoKnot for none) and DRIFT_MARK_FIT (knotTurnCos,
// knotMostM, knotStrengthStep: DriftMarks.StrokeFit).

#include "world/terrain/kernels/drift_mark_rows.glsl"

#ifdef DRIFT_MARK_ROWS

// DriftMarks.MinLengthM: under it there is no mark to speak of.
const float DriftMarkLeastM = 1e-3;

// Whether knot `knot` of every knot laid is still in the ring (DriftMarks.Holds), as the tick began.
bool driftMarkHolds(uint knot)
{
    return knot != DriftNoKnot && DRIFT_MARK_RING.total - knot <= DRIFT_MARK_RING.laid;
}

uint driftMarkSlot(uint knot) { return knot % DRIFT_MARK_RING.capacity; }

// A knot laid at the next place this tick has not claimed; its place among every knot laid.
uint driftMarkAppend(DriftKnot knot)
{
    uint laid = DRIFT_MARK_RING.total + atomicAdd(DRIFT_MARK_RING.claimed, 1u);
    DRIFT_MARK_ROWS[driftMarkSlot(laid)] = knot;
    return laid;
}

// Whether the curve from `pinned` to a last knot here would stray past the stroke's fit (DriftMarks.Bends).
bool driftMarkBends(DriftKnot pinned, vec2 toM, float headingRad, float strength)
{
    vec2 apartM = toM - pinned.atM;
    return cos(headingRad - pinned.headingRad) < DRIFT_MARK_FIT.knotTurnCos
           || dot(apartM, apartM) > DRIFT_MARK_FIT.knotMostM * DRIFT_MARK_FIT.knotMostM
           || abs(strength - pinned.strength) > DRIFT_MARK_FIT.knotStrengthStep;
}

// One stretch of wheel `wheel`'s travel laid on its stroke (DriftMarks.Lay): the last knot moved to its end while the
// stroke stays within its fit of the knot before, a knot laid on past it, and a stroke begun where the stretch does not
// carry on from the last knot in the same kind, width and level — or nothing, where it is too short, too faint or too
// narrow.
void driftMarkLay(uint wheel, vec2 fromM, vec2 toM, float widthM, float intensity, bool ploughed, bool raised)
{
    vec2 spanM = toM - fromM;
    if (length(spanM) < DriftMarkLeastM || intensity <= 0.0 || widthM <= 0.0) return;

    float strength = clamp(intensity, 0.0, 1.0) * (ploughed ? -1.0 : 1.0);
    float leveledWidthM = raised ? -widthM : widthM;
    float headingRad = atan(spanM.y, spanM.x);
    uint last = DRIFT_MARK_LASTS[wheel];
    if (driftMarkHolds(last))
    {
        DriftKnot lastKnot = DRIFT_MARK_ROWS[driftMarkSlot(last)];
        if (lastKnot.atM == fromM && lastKnot.widthM == leveledWidthM && (lastKnot.strength < 0.0) == ploughed)
        {
            uint pinned = lastKnot.previous;
            if (driftMarkHolds(pinned) && !driftMarkBends(DRIFT_MARK_ROWS[driftMarkSlot(pinned)], toM, headingRad, strength))
            {
                DRIFT_MARK_ROWS[driftMarkSlot(last)] = DriftKnot(toM, headingRad, leveledWidthM, strength, pinned);
                return;
            }

            DRIFT_MARK_LASTS[wheel] = driftMarkAppend(DriftKnot(toM, headingRad, leveledWidthM, strength, last));
            return;
        }
    }

    uint first = driftMarkAppend(DriftKnot(fromM, headingRad, leveledWidthM, strength, DriftNoKnot));
    DRIFT_MARK_LASTS[wheel] = driftMarkAppend(DriftKnot(toM, headingRad, leveledWidthM, strength, first));
}

#endif

// The tick's knots taken into the ring, by the one thread of a pass that runs after every lay: every knot counted, the
// count of what is written grown to at most the ring, the claims let go, and the frame's draw told how many.
void driftMarksSettle()
{
    uint claimed = DRIFT_MARK_RING.claimed;
    uint capacity = DRIFT_MARK_RING.capacity;
    uint laid = min(DRIFT_MARK_RING.laid + min(claimed, capacity), capacity);
    DRIFT_MARK_RING.total += claimed;
    DRIFT_MARK_RING.laid = laid;
    DRIFT_MARK_RING.claimed = 0u;
    DRIFT_MARK_RING.draw = uvec4(DriftKnotCorners, laid, 0u, 0u);
}
