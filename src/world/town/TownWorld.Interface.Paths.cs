using System.Numerics;
using TrafficSimulation.Agents.Car.Body;
using TrafficSimulation.Agents.Person.Body;
using TrafficSimulation.World.Foot;
using TrafficSimulation.World.Parking;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// <b>CTL-1a: the rest of the way, for a unit somebody picked out.</b> A body carries a bounded run of its
/// own route and plans the next one when that runs out, so what it is holding is the near end of a long
/// trip and not the whole of it. The interface asks for the far end here.
/// </summary>
/// <remarks>
/// <para>
/// <b>It is the same route and not a second opinion</b>: the same network, the same planner, the same
/// prices and the same goals the leg itself is aimed at (<see cref="RouteGoalsFor"/>), asked from the end
/// of what the body holds rather than from under it. What the body will plan when it gets there is this,
/// unless the town has priced something up in between — which is a route changing under a driver, and the
/// picture says so on the frame it happens.
/// </para>
/// <para>
/// <b>Only for the selection</b> (CTL-1b), because it is a search and there are tens of thousands of
/// bodies. It is bounded twice over: by how many units may be picked out, and by a plan being asked for
/// again only when the far end of the queue in hand moves.
/// </para>
/// </remarks>
internal sealed partial class TownWorld
{
    /// <summary>
    /// The lanes past the end of the route this car is holding, out to where it is going — nothing where
    /// the route it holds already ends there (<see cref="CarFleet.RouteRunsOut"/>, which is what the
    /// caller asks before it asks this).
    /// </summary>
    /// <param name="slot">Which of the selection's units this is, which is where the plan is kept.</param>
    /// <param name="fromLane">The last lane the car is holding, and the one the rest is planned from.</param>
    public ReadOnlySpan<int> RouteBeyond(int slot, int car, int fromLane)
    {
        if (slot < 0 || slot >= _paths.Slots || fromLane < 0) return default;

        var asked = new SelectionPaths.Asked(
            SelectionKind.Car, car, fromLane, Vector2.Zero, Cars.DestinationM[car], BayAimedAt(car));

        if (!_paths.Holds(slot, asked))
        {
            _paths.Held(slot, asked, PlanTheRestOfTheRoute(car, fromLane, _paths.LanesOf(slot)));
        }

        return _paths.LanesHeld(slot);
    }

    /// <summary>
    /// The points past the end of the line this walker is holding, out to where it is going — nothing
    /// where the line already reaches it (<see cref="PersonFleet.WalkedRunsOut"/>).
    /// </summary>
    /// <param name="fromM">The last point of that line, which is where the rest of the walk is laid from.</param>
    public ReadOnlySpan<Vector2> WalkBeyond(int slot, int person, Vector2 fromM)
    {
        if (slot < 0 || slot >= _paths.Slots) return default;

        var asked = new SelectionPaths.Asked(
            SelectionKind.Person, person, CarFleet.NoLane, fromM, People.GoalM[person], ParkingRegistry.NoBay);

        if (!_paths.Holds(slot, asked))
        {
            _paths.Held(slot, asked, PlanTheRestOfTheWalk(person, fromM, _paths.PointsOf(slot)));
        }

        return _paths.PointsHeld(slot);
    }

    /// <summary>
    /// A route from the far end of <paramref name="fromLane"/> to where the car is going, expanded into
    /// lanes — <see cref="TryPlan"/>'s own steps, over the interface's search and into the interface's
    /// room, and touching nothing the car is holding.
    /// </summary>
    int PlanTheRestOfTheRoute(int car, int fromLane, Span<int> into)
    {
        if (!Cars.HasDestination[car]) return 0;

        var search = _paths.Drive;
        var goalCount = RouteGoalsFor(car, search.Goals, out var goalPointM);
        if (goalCount == 0) return 0;

        search.Entries[0] = _driving.EntryOnLane(fromLane, _roads.LaneLengthM[fromLane]);
        if (search.Entries[0].Link == TravelGraph.NoLink) return 0;

        var linkCount = search.Plan(1, goalCount, goalPointM, _surcharges, out var goalSlot);
        if (linkCount == 0 || goalSlot < 0) return 0;

        return LayRouteLanes(fromLane, search.Links(linkCount), search.Goals[goalSlot], into, out _, out _);
    }

    /// <summary>
    /// The rest of the walk from <paramref name="fromM"/>, laid as the same points
    /// <see cref="LayWalk"/> lays — the goal itself is not one of them, because the hop off the network
    /// onto it is the last thing the walker's own line does and the goal already carries its own mark.
    /// </summary>
    int PlanTheRestOfTheWalk(int person, Vector2 fromM, Span<Vector2> into)
    {
        var walking = Walking;
        var search = _paths.Walk;
        var goalM = People.GoalM[person];
        var entryCount = walking.EntriesNear(fromM, search.Entries);
        var goalCount = walking.GoalsAt(goalM, search.Goals);
        if (entryCount == 0 || goalCount == 0) return 0;

        var linkCount = search.Plan(entryCount, goalCount, goalM, _surcharges, out var goalSlot);
        if (linkCount == 0 || goalSlot < 0) return 0;

        // Which of the two ways along its own stretch the search set off down is the first link it
        // returned; laying the line from the other one starts the walk facing backwards.
        var links = search.Links(linkCount);
        var entry = search.Entries[0];
        for (var at = 0; at < entryCount; at++)
        {
            if (search.Entries[at].Link == links[0]) entry = search.Entries[at];
        }

        return WalkedLine.Lay(
            walking, links, entry, search.Goals[goalSlot], _config.Network.SplineToleranceWalkedM,
            _bands.CrossingOfEdge, into, _paths.Crossing, _paths.Way, _paths.AlongM, out _);
    }
}
