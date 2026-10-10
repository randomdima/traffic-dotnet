// A car as a watch the device keeps reads it (app/devicetick/DeviceTownWatch.CarRow), written by whoever ticked it last:
// how far past its grant it is, and whether it is driven and not wrecked.

#ifndef CAR_WATCHED_GLSL
#define CAR_WATCHED_GLSL

struct WatchCarRow
{
    float pastM;
    uint driven;
};

#endif
