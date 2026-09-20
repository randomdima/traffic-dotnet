namespace TrafficSimulation.Agents.Person.Control;

/// <summary>
/// What a person is doing about the trip they are on. <b>The life cycle as observable states</b> — every
/// one of them is a state somebody watching can be told about, and there is no state a body can be in
/// that this does not name.
/// </summary>
/// <remarks>
/// <b>There are five of them because a walk is the whole of what a walker does</b> (PER-25): it is inside
/// a building, walking to the next one, waiting for room at the door it reached, or standing between the
/// two. Nothing here is a manoeuvre and nothing here arbitrates — the catalogue AGT-7 asks for is the
/// absence recorded in the known gaps, and numbering these as though it existed would claim behaviour
/// that is not here.
/// </remarks>
internal enum TripStage : byte
{
    /// <summary>Between goals, idling the brief interval that stops a town setting off on one tick.</summary>
    StandingBy,

    /// <summary>Walking to the destination building's own way in.</summary>
    WalkingToTheDoor,

    /// <summary>The building was full at the door, so standing where it arrived and asking again.</summary>
    WaitingForAPlace,

    /// <summary>Inside, for the bounded interval that guarantees whoever is waiting outside gets a place (PER-11).</summary>
    Dwelling,

    /// <summary>CTL-2: the goal was pinned by a hand, so nothing here draws another when it is reached.</summary>
    UnderOrders,
}
