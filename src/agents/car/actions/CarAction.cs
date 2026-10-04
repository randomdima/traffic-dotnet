namespace TrafficSimulation.Agents.Car.Actions;

/// <summary>
/// <b>What a car is doing</b> — one action at a time, entered and left explicitly (CAR-15b). Each drives only the
/// ground it has claimed, and knows when it is done and what it hands over to.
/// </summary>
internal enum CarAction : byte
{
    /// <summary>Moving nowhere of its own: parked, stood down, or a wreck. Its body is all it holds.</summary>
    Stand,

    /// <summary>Driving the route's own lanes, on ground asked for down them and had in part (TER-4c.1).</summary>
    Follow,

    /// <summary>Getting past a body at rest over the lane beside (CAR-46): decided, then asked for whole, then driven.</summary>
    Overtake,

    /// <summary>
    /// Moving across onto the lane beside running its way (CAR-53): looked for while the car drives on down its own, then
    /// asked for as the step and the room past it, then driven.
    /// </summary>
    Switch,

    /// <summary>Backing down its own lane for the room to step out round what it has decided to pass (CAR-50).</summary>
    BackUp,

    /// <summary>Into a bay (GEN-4f): up to where it waits for its manoeuvre, then the manoeuvre, asked for whole.</summary>
    Park,

    /// <summary>Out of a bay (GEN-4f): the manoeuvre, asked for whole from where the car stands in it.</summary>
    Unpark,

    /// <summary>Off its line (CAR-9): at rest where it stands, then onto the lane under it.</summary>
    Rejoin,

    /// <summary>A hand at the wheel (CTL-5, S-7): the hand drives, and the car holds what it can no longer stop short of.</summary>
    Hand,

    /// <summary>On an evacuator's bar (EVA-5): moved by the truck, and laid under it.</summary>
    Towed,
}
