using System.Numerics;

namespace TrafficSimulation.Agents.Person.Control;

/// <summary>How a trip gets to its door (PER-29).</summary>
internal enum TripMode : byte
{
    /// <summary>On foot, the whole way.</summary>
    Walked,

    /// <summary>On foot to their car, at the wheel to a bay near the door, and on foot from there.</summary>
    Driven,

    /// <summary>Too far to walk, with no car to hand: not a trip this person can make.</summary>
    OutOfReach,
}

/// <summary>PER-29's choice between the two ways to a door.</summary>
internal static class TripModes
{
    /// <summary>
    /// <b>A door is walked to where it is within a walk of both the person and their car</b>, and driven to
    /// anywhere else they have a car to hand for. Within a walk of the car as well, so that wherever a person's
    /// walks take them, their car is never further off than a walk.
    /// </summary>
    /// <param name="aboard">At the wheel already, which is a drive whatever the door: the car has to be parked first.</param>
    /// <param name="carM">Where their car stands; read only where it is to hand.</param>
    /// <param name="walkReachM">The furthest a trip is walked, as the crow flies.</param>
    public static TripMode Of(
        Vector2 fromM, Vector2 doorM, bool aboard, bool carToHand, Vector2 carM, float walkReachM)
    {
        if (aboard) return TripMode.Driven;

        var walkable = Vector2.Distance(fromM, doorM) <= walkReachM;
        if (!carToHand) return walkable ? TripMode.Walked : TripMode.OutOfReach;

        return walkable && Vector2.Distance(carM, doorM) <= walkReachM ? TripMode.Walked : TripMode.Driven;
    }
}

/// <summary>
/// What a person is doing about the trip they are on. <b>The life cycle as observable states</b> — every
/// one of them is a state somebody watching can be told about, and there is no state a body can be in
/// that this does not name.
/// </summary>
/// <remarks>
/// <b>A trip is walked, or driven between two walks</b> (PER-29): inside a building, walking to the next one,
/// walking to their own car and driving it to a bay near the next one, waiting for room at the door, or
/// standing between two trips. Nothing here arbitrates and nothing here is an action — a leg is what an agent
/// does (AGT-7), and this is the errand the legs are being walked and driven for.
/// </remarks>
internal enum TripStage : byte
{
    /// <summary>Between goals, idling the brief interval that stops a town setting off on one tick.</summary>
    StandingBy,

    /// <summary>Walking to the destination building's own way in.</summary>
    WalkingToTheDoor,

    /// <summary>Walking to their own car's way in (GEN-4e), the destination too far to walk to (PER-29).</summary>
    WalkingToTheCar,

    /// <summary>
    /// <b>At the wheel of their own car</b> (PER-29), the car's leg aimed at a bay near the destination. <b>The
    /// driving is the car's</b>: the trip waits for it to park and then lets its owner out to walk the rest.
    /// </summary>
    Driving,

    /// <summary>Out of the car they parked, walking from its bay to the destination building's way in.</summary>
    WalkingFromTheCar,

    /// <summary>The building was full at the door, so standing where it arrived and asking again.</summary>
    WaitingForAPlace,

    /// <summary>Inside, for the bounded interval that guarantees whoever is waiting outside gets a place (PER-11).</summary>
    Dwelling,

    /// <summary>CTL-2: the goal was pinned by a hand, so nothing here draws another when it is reached.</summary>
    UnderOrders,

    /// <summary>
    /// <b>A police officer on duty</b> (SRV-11): aboard their car, or out of it walking straight to where the
    /// closure puts them and standing there. <b>The errand is the car's</b>, so nothing of the trip runs — no
    /// route, no clock that gives the walk up, nothing drawn when it arrives.
    /// </summary>
    OnDuty,
}
