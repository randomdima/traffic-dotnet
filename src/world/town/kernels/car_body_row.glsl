// What a car's body is laid from (car_bodies.glsl), field for field with app/devicetick/DeviceBodies.BodyRow.

#ifndef CAR_BODY_ROW_GLSL
#define CAR_BODY_ROW_GLSL

// What the laying reads of one car (CarFleet): where it stands, where its wheels are under it (TyreModel.WheelAtM), its
// channels, and of the car it is laid as (TownWorld.LaidAs) — itself, or the truck it hangs from — how fast it goes along
// its line and what the town says of it this rebuild.
struct CarBodyRow
{
    vec2 centreM;
    vec2 forward;
    float frontAxleM;
    float rearAxleM;
    float halfTrackM;
    uint channels;
    int occupant;
    float alongMps;
    uint flags;
    uint pad;
};

// CarBodyRow.flags: of the car it is laid as, whether it drives its route's line (TownWorld.IsUnderWay) and whether
// that line runs on past its leading edge (DrivingGround.RunsOnPastItself); of the car itself, whether it is at rest,
// and whether it is blocked there (DriveContext.Blocked).
const uint BodyTravelling = 1u;
const uint BodyRunsOnPastItself = 2u;
const uint BodyStill = 4u;
const uint BodyBlocked = 8u;

#endif
