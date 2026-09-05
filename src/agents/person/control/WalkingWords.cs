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
    /// <b>Every answer is a literal, and the return is the string rather than a span over one</b>: a panel
    /// draws it into a buffer and an instrument keeps it, and taking a copy of a span is an allocation on
    /// a hot path for a word that never changes.
    /// </remarks>
    /// <param name="stopsInM">How much room this body needs to stop, which is what says a line ahead of it is blocked.</param>
    public static string WalkName(PersonFleet people, int person, float stopsInM)
    {
        if (people.Wounded[person]) return "wounded, waiting for an ambulance";
        if (people.HeldAtTheKerb[person]) return "held at the kerb";

        // What the trip is doing outranks what the body is doing, because a body standing still is the
        // one thing several of these states have in common.
        if (!people.Walking[person])
        {
            return people.Stage[person] switch
            {
                TripStage.WaitingForAPlace => "waiting for a place",
                TripStage.Alighting => "getting out",
                TripStage.UnderOrders => "awaiting orders",
                TripStage.StandingBy => "standing by",
                _ => "standing",
            };
        }

        // Standing still with a line ahead of it and no kerb in front: the ground it wanted is somebody
        // else's, which is the other state this layer could not otherwise tell from walking. The lane it
        // was refused is worth naming apart from the pavement it is queueing on — one is traffic and the
        // other is a crowd, and they look identical from here.
        if (people.IsHeldByTheClaims(person, stopsInM))
        {
            return people.RefusedWay[person] == PersonFleet.NoWay
                ? "waiting behind somebody"
                : "waiting for a lane";
        }

        var taken = people.WalkedTaken[person];
        var line = people.WalkedCrossingOf(person);
        if (taken > 0 && taken <= line.Length && line[taken - 1] >= 0) return "on the crossing";

        return people.Stage[person] == TripStage.WalkingToTheCar ? "walking to a car" : "walking";
    }

    /// <summary>
    /// Which leg of PER-9's trip this person is on, which is a different question from what their body is
    /// doing: a walker standing still is between two of these and a walker walking is inside one.
    /// </summary>
    public static string StageName(TripStage stage) => stage switch
    {
        TripStage.StandingBy => "between trips",
        TripStage.WalkingToTheDoor => "to a door",
        TripStage.WalkingToTheCar => "to a car",
        TripStage.Driving => "driving",
        TripStage.Alighting => "getting out",
        TripStage.WaitingForAPlace => "waiting for a place",
        TripStage.Dwelling => "inside",
        TripStage.UnderOrders => "under orders",
        TripStage.OnDuty => "on duty, in the seat",
        TripStage.Attending => "attending",
        _ => "on a trip",
    };
}
