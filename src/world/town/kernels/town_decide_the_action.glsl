// TownWorld.DecideTheAction on the device: what the car's action decides, on the car's own clock rather than its tick
// (CAR-15b) — each action's own Decide, defined in its own slice's kernels. A kernel that decides a car includes this
// beside town_legs.glsl, and those.

#ifndef TOWN_DECIDE_THE_ACTION_GLSL
#define TOWN_DECIDE_THE_ACTION_GLSL

#include "world/town/kernels/town_leg_calls.glsl"

// The car's actions' decisions (Following, Overtaking, Switching, ParkingIn, PullingOut, Rejoining .Decide).
void following_Decide(uint car, float sinceLastDecisionS);
void overtaking_Decide(uint car, float sinceLastDecisionS);
void switching_Decide(uint car, float sinceLastDecisionS);
void parkingIn_Decide(uint car);
void pullingOut_Decide(uint car);
void rejoining_Decide(uint car, float sinceLastDecisionS);

void town_DecideTheAction(uint car, float sinceLastDecisionS)
{
    uint action = cars_Action(car);
    if (action == CarActionFollow)
    {
        following_Decide(car, sinceLastDecisionS);

        // A pass drawn now is looked round for at once, as it was on the tick after it was drawn — and so is the room to
        // move across.
        if (cars_Action(car) == CarActionOvertake) overtaking_Decide(car, 0.0);
        if (cars_Action(car) == CarActionSwitch) switching_Decide(car, 0.0);
        return;
    }

    if (action == CarActionOvertake || action == CarActionBackUp)
    {
        overtaking_Decide(car, sinceLastDecisionS);
        return;
    }

    if (action == CarActionSwitch)
    {
        switching_Decide(car, sinceLastDecisionS);
        return;
    }

    if (action == CarActionPark)
    {
        parkingIn_Decide(car);
        return;
    }

    if (action == CarActionUnpark)
    {
        pullingOut_Decide(car);
        return;
    }

    if (action == CarActionRejoin) rejoining_Decide(car, sinceLastDecisionS);
}

#endif
