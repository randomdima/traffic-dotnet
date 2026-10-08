// What a rebuild's stages made of one walker a device keeps (app/devicetick/DeviceWalkers.WalkerOut), field for field —
// written by walkers_kept_out.comp and read by walkers_kept_aims.comp.

#ifndef WALKER_OUT_GLSL
#define WALKER_OUT_GLSL

struct WalkerOut
{
    vec2 destinationM;
    vec2 backToM;
    float onWayM;
    float offWayM;
    float grantM;
    float passFromM;
    float passClearM;
    float passAsideM;
    int passSlot;
    int routeTaken;
    int onWay;
    int onCrossing;
    int heldAs;
    uint flags;
    uint action;
    int hold;
    uint events;
    uint walker;
};

// One kept row as it goes out; the includer includes walker_rows.glsl first.
WalkerOut walkerOutOf(WalkerRow now, uint walker)
{
    return WalkerOut(
        now.destinationM, now.backToM, now.onWayM, now.offWayM, now.grantM, now.passFromM, now.passClearM, now.passAsideM,
        now.passSlot, now.routeTaken, now.onWay, now.onCrossing, now.heldAs, now.flags, now.action, now.hold, now.events, walker);
}

#endif
