// CarActions on the device (CAR-15b): the one place a car's action changes, and what the action it leaves owned — a pass,
// a manoeuvre, the ground it was granted — dropped with it. The fields are the includer's accessors (town_cars.glsl,
// town_rows.glsl, town_car_actions.glsl); a manoeuvre's own questions are Manoeuvres' (manoeuvres_IsBegun,
// manoeuvres_Clear), declared by the includer.

#ifndef CAR_ACTIONS_GLSL
#define CAR_ACTIONS_GLSL

// LaneOccupancy.NoHold: a car whose plan laid no hold this rebuild.
const int LaneOccupancyNoHold = -1;

bool carActions_IsAtABay(uint action) { return action == CarActionPark || action == CarActionUnpark; }

bool carActions_PlansDownTheRoute(uint action)
{
    return action == CarActionFollow || action == CarActionOvertake || action == CarActionSwitch || action == CarActionBackUp
        || action == CarActionPark;
}

bool carActions_DrivesTheRoute(uint car)
{
    uint action = cars_Action(car);
    if (action == CarActionFollow || action == CarActionOvertake || action == CarActionSwitch || action == CarActionBackUp) return true;

    return action == CarActionPark && !manoeuvres_IsBegun(car);
}

bool carActions_IsManoeuvring(uint car)
{
    return cars_Action(car) == CarActionUnpark || (cars_Action(car) == CarActionPark && manoeuvres_IsBegun(car));
}

// A car handed to another action: a pass is its overtake's and a manoeuvre its bay's, and a grant its action's claim —
// but for the plan down the route's line, handed on between the actions that drive it (TER-4c.8).
void carActions_Enter(uint car, uint action)
{
    uint was = cars_Action(car);
    if (was == action) return;

    if (was == CarActionOvertake || was == CarActionSwitch)
    {
        cars_PassWord(car, 0u) = -1;
        for (uint word = 1u; word < OvertakeWords; word++) cars_PassWord(car, word) = 0;
    }

    if (carActions_IsAtABay(was) && !carActions_IsAtABay(action)) manoeuvres_Clear(car);

    if (!(carActions_PlansDownTheRoute(was) && carActions_PlansDownTheRoute(action) && drivingGround_PlanHold(car) != LaneOccupancyNoHold))
    {
        cars_AuthorityM(car) = 0.0;
        cars_CoveredSinceClaimM(car) = 0.0;
    }

    cars_Action(car) = action;
    carActions_Entries(car)++;
}

#endif
