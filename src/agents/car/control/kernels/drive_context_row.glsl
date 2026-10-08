// What the driver has been told about the world this tick (DriveContext), field for field with
// app/devicetick/DeviceCarTick.ContextRow.

#ifndef DRIVE_CONTEXT_ROW_GLSL
#define DRIVE_CONTEXT_ROW_GLSL

struct DriveContextRow
{
    float authorityM;
    uint grantCutBy;
    float placeStopM;
    float marginM;
    uint waitsToPass;
    float horizonM;
    float passAsideM;
    float backUpM;
    uint blocked;
    float waitAtM;
    float pad0;
    float pad1;
};

#endif
