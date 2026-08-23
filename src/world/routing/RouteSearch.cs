using System.Numerics;

namespace TrafficSimulation.World.Routing;

/// <summary>
/// One network's planner together with the buffers a search over it writes into. Single-threaded by the
/// tick's own shape, so one set of buffers serves every agent and a search allocates nothing.
/// </summary>
internal sealed class RouteSearch
{
    readonly RoutePlanner _planner;
    readonly RouteEntry[] _entries;
    readonly RouteGoal[] _goals;
    readonly int[] _links;

    public RouteSearch(TravelGraph graph, int mostEntries, int mostGoals, int mostLinks)
    {
        _planner = new RoutePlanner(graph);
        _entries = new RouteEntry[mostEntries];
        _goals = new RouteGoal[mostGoals];
        _links = new int[mostLinks];
    }

    /// <summary>Where the search may start from, to be filled before <see cref="Plan"/>.</summary>
    public Span<RouteEntry> Entries => _entries;

    /// <summary>Where it may finish, to be filled before <see cref="Plan"/>.</summary>
    public Span<RouteGoal> Goals => _goals;

    /// <summary>The links of the last plan, valid until the next one over this search.</summary>
    public ReadOnlySpan<int> Links(int count) => _links.AsSpan(0, count);

    /// <inheritdoc cref="RoutePlanner.Plan"/>
    public int Plan(
        int entryCount, int goalCount, Vector2 goalPointM, LinkSurcharges? surcharges, out int goalSlot) =>
        _planner.Plan(
            _entries.AsSpan(0, entryCount), _goals.AsSpan(0, goalCount), goalPointM, surcharges,
            _links, out _, out goalSlot);
}
