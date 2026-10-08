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

#endif
