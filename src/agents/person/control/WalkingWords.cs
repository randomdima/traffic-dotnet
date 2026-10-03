using TrafficSimulation.Agents.Person.Body;

namespace TrafficSimulation.Agents.Person.Control;

/// <summary>
/// What a walker is doing, in words, for whatever puts it on screen — the walking network's answer to
/// <see cref="Car.Control.DrivingWords"/>. It lives with the walker because it is a reading of the
/// walker's own state and not a fact about any panel: the debug layer and the interface both ask for it,
/// and a second copy of this switch would drift from the trip's own stages the day one is added.
/// </summary>
internal static class WalkingWords
{
    /// <summary>
    /// What a walker is doing, in the words its own follower uses. <b>A state and not a manoeuvre</b>: the
    /// walker's catalogue is unbuilt, and a label naming one of its entries would claim behaviour that is
    /// not there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every answer is a literal, and the return is the string rather than a span over one</b>: a panel
    /// draws it into a buffer and an instrument keeps it, and taking a copy of a span is an allocation on
    /// a hot path for a word that never changes.
    /// </para>
    /// <para>
    /// <b>Being held is the grant's to say</b> (PER-26): a walker granted nothing is giving way, and one granted
    /// nothing at a zebra is waiting to cross it.
    /// </para>
    /// </remarks>
    public static string WalkName(PersonFleet people, int person)
    {
        if (people.Wounded[person]) return "wounded, waiting for an ambulance";

        // What the trip is doing outranks what the body is doing, because a body standing still is the
        // one thing several of these states have in common.
        if (!people.Walking[person])
        {
            return people.Stage[person] switch
            {
                TripStage.WaitingForAPlace => "waiting for a place",
                TripStage.UnderOrders => "awaiting orders",
                TripStage.StandingBy => "standing by",
                _ => "standing",
            };
        }

        // A crossing the traffic has is a walker standing at a kerb (PER-27), which is a state of its
        // own and not a walk: what it is doing is the thing a watcher wants named.
        if (people.Pass[person].Begun) return "stepping round somebody";

        var held = people.GrantM[person] <= 0f;
        if (held && people.OnCrossing[person] != PersonFleet.NoCrossing) return "waiting to cross";
        if (people.OnCrossing[person] != PersonFleet.NoCrossing) return "on the crossing";
        if (held) return "giving way";

        // Off every way of the network, which is a walk in a straight line at the nearest of them
        // (PER-25) and is worth naming apart from a walk down a lane. <b>Asked of the route and not of
        // <see cref="PersonFleet.OnWay"/></b>: that one is where the town agreed the body is standing, and
        // it is empty for the stretch at the start of a leg spent crossing to the far lane of a pavement —
        // which is a walk down a way like any other and was being read out as a walk to the kerb.
        return people.CurrentRouteWay(person) == PersonFleet.NoWay ? "walking to the pavement" : "walking";
    }

    /// <summary>
    /// Which leg of PER-9's trip this person is on, which is a different question from what their body is
    /// doing: a walker standing still is between two of these and a walker walking is inside one.
    /// </summary>
    public static string StageName(TripStage stage) => stage switch
    {
        TripStage.StandingBy => "between trips",
        TripStage.WalkingToTheDoor => "to a door",
        TripStage.WalkingToTheCar => "to their car",
        TripStage.Driving => "driving",
        TripStage.WalkingFromTheCar => "from their car to a door",
        TripStage.WaitingForAPlace => "waiting for a place",
        TripStage.Dwelling => "inside",
        TripStage.UnderOrders => "under orders",
        _ => "on a trip",
    };
}
