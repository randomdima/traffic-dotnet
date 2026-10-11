// TownWorld.Actions on the device: the one place a car is handed to another action, whether its line is a piece of its
// manoeuvre, and what a piece driven to its end leads to (TownWorld.Enter, IsManoeuvring, TheManoeuvreIsDriven). Includes
// town_leg_calls.glsl, whose declarations it calls.

#ifndef TOWN_ACTIONS_GLSL
#define TOWN_ACTIONS_GLSL

#include "world/town/kernels/town_leg_calls.glsl"
#include "agents/car/actions/kernels/car_actions.glsl"

void town_Enter(uint car, uint action) { carActions_Enter(car, action); }

bool town_IsManoeuvring(uint car) { return carActions_IsManoeuvring(car); }

// The next piece, or at the end of the last whatever the action ends in: a car parked, or out on its lane.
bool town_TheManoeuvreIsDriven(uint car)
{
    if (bayManoeuvring_TakeTheNextPiece(car)) return true;

    return cars_Action(car) == CarActionPark ? parkingIn_Arrive(car) : pullingOut_Arrive(car);
}

#endif
