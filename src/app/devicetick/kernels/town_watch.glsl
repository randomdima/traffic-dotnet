// A watch over every body as the device keeps it (app/devicetick/DeviceTownWatch): what a step reads of it, field for field
// with world/town/TownWatched.cs's TownWatchedTick, and what it has against one body, its TownWatchNote.

#ifndef TOWN_WATCH_GLSL
#define TOWN_WATCH_GLSL

#include "world/town/kernels/car_watched.glsl"

// The deepest, longest and furthest of a step, each with the lowest-numbered body that had it — NoBody where none had any.
// A depth and a distance past are kept as their bits: neither is ever below nought, so the bits order as the metres do.
struct WatchedRow
{
    uint deepestBits;
    uint deepestBody;
    uint stuckTicks;
    uint stuckBody;
    uint pastBits;
    uint pastBody;
    uint pastTicks;
    uint pastTicksBody;
    uint stillTicks;
    float drivenM;
    uint pad0;
    uint pad1;
};

struct WatchNoteRow
{
    uint body;
    float overlapM;
    uint stuckTicks;
    float pastM;
    uint pastTicks;
};

// Which body a car is (app/devicetick/DeviceBodyPhase.CarBodyRow).
struct WatchCarBody
{
    int body;
    uint flags;
};

const uint NoBody = 0xFFFFFFFFu;

WatchedRow nothingWatched()
{
    return WatchedRow(0u, NoBody, 0u, NoBody, 0u, NoBody, 0u, NoBody, 0u, 0.0, 0u, 0u);
}

// Whether one figure and its body beat another's: greater, or as great and lower-numbered.
bool beats(uint figure, uint body, uint otherFigure, uint otherBody)
{
    return figure > otherFigure || (figure == otherFigure && body < otherBody);
}

WatchedRow better(WatchedRow a, WatchedRow b)
{
    WatchedRow row = a;
    if (beats(b.deepestBits, b.deepestBody, a.deepestBits, a.deepestBody))
    {
        row.deepestBits = b.deepestBits;
        row.deepestBody = b.deepestBody;
    }

    if (beats(b.stuckTicks, b.stuckBody, a.stuckTicks, a.stuckBody))
    {
        row.stuckTicks = b.stuckTicks;
        row.stuckBody = b.stuckBody;
    }

    if (beats(b.pastBits, b.pastBody, a.pastBits, a.pastBody))
    {
        row.pastBits = b.pastBits;
        row.pastBody = b.pastBody;
    }

    if (beats(b.pastTicks, b.pastTicksBody, a.pastTicks, a.pastTicksBody))
    {
        row.pastTicks = b.pastTicks;
        row.pastTicksBody = b.pastTicksBody;
    }

    row.stillTicks = max(a.stillTicks, b.stillTicks);
    row.drivenM = a.drivenM + b.drivenM;
    return row;
}

#endif
