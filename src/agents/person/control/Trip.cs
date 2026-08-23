using TrafficSimulation.Core.Config;

namespace TrafficSimulation.Agents.Person.Control;

/// <summary>
/// What a person is doing about the trip they are on. <b>The life cycle as observable states</b>
/// every one of them is a state somebody watching can be told about, and
/// there is no state a body can be in that this does not name.
/// </summary>
/// <remarks>
/// <b>These are not the manoeuvre catalogue's entries and do not pretend to be.</b> Each covers the
/// stretch of a trip a named entry will own — `P-1` leave the building, `P-2` walk the block, `P-5`
/// approach the car, `P-6` board, `P-7` alight, `P-9` enter, `P-10` dwell, `P-11` wait for a place,
/// `P-12` stand by — and what is missing is the arbitration and the escalation ladder between them,
/// which is M8. A label reading `P-2` would claim behaviour that is not here.
/// </remarks>
internal enum TripStage : byte
{
    /// <summary>`P-12`: between goals, idling the brief interval that stops a town setting off on one tick.</summary>
    StandingBy,

    /// <summary>`P-2`: walking to the destination building's own way in.</summary>
    WalkingToTheDoor,

    /// <summary>`P-5`: walking to the way in of the car this trip is using.</summary>
    WalkingToTheCar,

    /// <summary>Inside the car, which is driving. The person supplies the car's action set (PER-6) and nothing else.</summary>
    Driving,

    /// <summary>`P-7`: asking the car for a spot beside it, which is refused while every position round it is taken.</summary>
    Alighting,

    /// <summary>`P-11`: the building was full at the door, so standing on walkable ground near it and asking again.</summary>
    WaitingForAPlace,

    /// <summary>`P-10`: inside, for the bounded interval that guarantees whoever is waiting outside gets a place.</summary>
    Dwelling,

    /// <summary>CTL-2: the goal was pinned by a hand, so nothing here draws another when it is reached.</summary>
    UnderOrders,
}

/// <summary>The trip's own decisions, as pure functions of the figures — what a walker chooses, and never how it gets there.</summary>
internal static class Trip
{
    /// <summary>
    /// <b>WLK-8, and it is structural rather than a weighted coin</b>: a trip is walked when the
    /// destination is in the same block — a route to it that never sets foot on a carriageway — or when
    /// it is inside the walk-worth distance. Anything else is worth a car.
    /// </summary>
    /// <param name="crossesACarriageway">
    /// Whether the walk this person would actually take steps onto a road, read off the line the
    /// planner laid rather than guessed at from the distance: <b>same block is a fact about the route</b>.
    /// </param>
    public static bool IsWorthWalking(SimConfig config, float farM, bool crossesACarriageway) =>
        !crossesACarriageway || farM <= config.PersonWalkWorthM;

    /// <summary>
    /// PER-10a's ceiling: no leg of a trip is a longer walk than the person would ever have chosen, so
    /// a drive that would land them further out than this from the door is not a drive worth taking —
    /// and one that ends there anyway drops the destination rather than handing over the walk.
    /// </summary>
    public static bool IsTooFarToWalk(SimConfig config, float farM) => farM > config.PersonWalkWorthM;
}
