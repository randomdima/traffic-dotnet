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
    /// <b>The walk this body is already holding</b>, as the points a picture is drawn through: the ways of
    /// its own chain still to be walked (<see cref="PersonFleet.RouteWays"/>), from where it stands on the
    /// one it is on to where the chain stops. <b>The walker's <c>LineOf</c></b> — what a car's assembled
    /// line is to <see cref="RouteBeyond"/>, this is to <see cref="WalkBeyond"/>.
    /// </summary>
    /// <remarks>
    /// <b>Stationed rather than planned</b>, so it is neither a search nor a second opinion: a walker holds
    /// ways and is held on each way's own arc, and the straights between the points written here exist only
    /// because a screen draws straights (<see cref="WalkedLine"/>). It is laid again every frame because it
    /// begins under the body.
    /// </remarks>
    public ReadOnlySpan<Vector2> WalkHeld(int slot, int person)
    {
        if (slot < 0 || slot >= _paths.Slots) return default;

        var route = People.RouteOf(person);
        var at = People.RouteAt(person);
        var count = People.RouteCount[person];
        if (at < 0 || at >= count)
        {
            _paths.Stationed(slot, 0);
            return default;
        }

        _paths.Stationed(
            slot,
            WalkedLine.Station(
                Walking, route[at..count], People.OnWayM[person], People.RouteToM[person],
                _config.Network.SplineToleranceWalkedM, _paths.HeldPointsOf(slot), out _));

        return _paths.HeldPoints(slot);
    }

    /// <summary>
    /// The walk past the end of the chain this body is holding, out to where it is going — nothing where
    /// the chain it holds already ends there (<see cref="PersonFleet.RouteRunsOut"/>, which is what the
    /// caller asks before it asks this).
    /// </summary>
    /// <param name="fromM">
    /// Where the rest is planned from, which is the end of the chain in hand and <b>not the body</b>: asked
    /// from under a body that is walking, this is a search a frame — and the answer to a question the town
    /// is not asking, since what the body will do next is plan from where its chain stops.
    /// </param>
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
        var goalCount = RouteGoalsFor(car, search.Goals);
        if (goalCount == 0) return 0;

        search.Entries[0] = _driving.EntryOnLane(fromLane, _roads.LaneLengthM[fromLane]);
        if (search.Entries[0].Link == TravelGraph.NoLink) return 0;

        var linkCount = search.Plan(1, goalCount, _surcharges, ClosedLinksFor(car), out var goalSlot);
        if (linkCount == 0 || goalSlot < 0) return 0;

        return LayRouteLanes(fromLane, search.Links(linkCount), search.Goals[goalSlot], into, out _, out _);
    }

    /// <summary>
    /// The rest of the walk from <paramref name="fromM"/>: <b>the walker's own steps</b> — one search, one
    /// expansion into ways (<see cref="RouteChain"/>) — and then those ways stationed into the points a
    /// picture is drawn through, which is the one thing the interface wants that a body does not.
    /// </summary>
    /// <remarks>
    /// The goal itself is not one of them, the hop off the network onto it being the last thing a walk
    /// does and the goal already carrying its own mark.
    /// </remarks>
    int PlanTheRestOfTheWalk(int person, Vector2 fromM, Span<Vector2> into)
    {
        var walking = Walking;
        var search = _paths.Walk;
        var goalM = People.GoalM[person];
        var entryCount = walking.EntriesNear(fromM, search.Entries);
        var goalCount = walking.GoalsAt(goalM, search.Goals);
        if (entryCount == 0 || goalCount == 0) return 0;

        var linkCount = search.Plan(entryCount, goalCount, _walkSurcharges, out var goalSlot);
        if (linkCount == 0 || goalSlot < 0) return 0;

        var links = search.Links(linkCount);
        var entry = WalkingNetwork.SetOffFrom(search.Entries[..entryCount], links[0]);

        var runs = walking.Runs;
        var goal = search.Goals[goalSlot];
        var joins = new WalkingNetwork.FootJoins(walking);
        var ways = _paths.Ways.AsSpan();
        var written = RouteChain.LayInto(
            ref joins, runs, links, fromWay: NothingBehind,
            firstSlot: runs.PieceAt(links[0], entry.AlongM, out var enteredAtM), goal, ways, out var ranOut);

        if (written == 0) return 0;

        return WalkedLine.Station(
            walking, ways[..written], walking.LaneMOf(ways[0], enteredAtM),
            StopsAtM(walking, ways[..written], goal, ranOut), _config.Network.SplineToleranceWalkedM, into, out _);
    }
}
