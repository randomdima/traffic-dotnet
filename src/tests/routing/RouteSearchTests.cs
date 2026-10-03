using TrafficSimulation.World.Routing;
using Xunit;

namespace TrafficSimulation.Tests.Routing;

/// <summary>
/// The sectioned search, asked on a hand-laid grid of hundred-metre blocks: the cells first, then the few in front
/// of the body in detail, and the whole graph where those cannot settle the answer.
/// </summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class RouteSearchTests
{
    const int Corners = 14;
    const int MostLinks = 256;

    /// <summary>A goal across the grid is handed over a section at a time: the route stops short of it.</summary>
    [Fact]
    public void AFarGoalIsHandedOverASectionAtATime()
    {
        var (grid, search) = Searching();
        var goal = FarGoal(grid);

        var count = Ask(search, new RouteEntry(grid.Link(0, 0, 1, 0), 0f, 100f), goal, out _);

        Assert.True(search.StopsShort);
        Assert.DoesNotContain(goal.Link, search.Links(count).ToArray());
    }

    /// <summary>
    /// <b>Following the sections arrives</b>: each asked again from the end of the last, the way a body asks
    /// where its route runs out, the last of them ends on the goal.
    /// </summary>
    [Fact]
    public void FollowingTheSectionsArrivesAtTheGoal()
    {
        var (grid, search) = Searching();
        var goal = FarGoal(grid);
        var entry = new RouteEntry(grid.Link(0, 0, 1, 0), 0f, 100f);

        for (var section = 0; section < Corners * Corners; section++)
        {
            var count = Ask(search, entry, goal, out _);
            Assert.NotEqual(0, count);

            var last = search.Links(count)[^1];
            if (!search.StopsShort)
            {
                Assert.Equal(goal.Link, last);
                return;
            }

            entry = new RouteEntry(last, grid.Graph.WeightM(last), 0f);
        }

        Assert.Fail("the sections never reached the goal");
    }

    /// <summary>A goal within reach of the window is the whole route, and the cheapest one.</summary>
    [Fact]
    public void ANearGoalIsTheWholeRouteAtTheCheapest()
    {
        var (grid, search) = Searching();
        var entry = new RouteEntry(grid.Link(0, 0, 1, 0), 0f, 100f);
        var goal = new RouteGoal(grid.Link(2, 1, 2, 2), 50f);

        Ask(search, entry, goal, out _);
        new RoutePlanner(grid.Graph).Plan([entry], [goal], null, new int[MostLinks], out var cheapestM, out _);

        Assert.False(search.StopsShort);
        Assert.Equal(cheapestM, search.CostM, 3);
    }

    /// <summary><b>A closed link in the window is never entered</b> (SRV-10), the cells being blind to it.</summary>
    [Fact]
    public void AClosedLinkInTheWindowIsNeverEntered()
    {
        var (grid, search) = Searching();
        var closed = new bool[grid.Graph.LinkCount];
        var shut = grid.Link(2, 0, 3, 0);
        closed[shut] = true;

        search.Entries[0] = new RouteEntry(grid.Link(0, 0, 1, 0), 0f, 100f);
        search.Goals[0] = new RouteGoal(grid.Link(Corners - 2, 0, Corners - 1, 0), 50f);
        var count = search.Plan(1, 1, null, closed, out _);

        Assert.NotEqual(0, count);
        Assert.DoesNotContain(shut, search.Links(count).ToArray());
    }

    /// <summary>
    /// <b>A way given up on in the window is weighed against everything round it</b>, as the whole graph weighs it —
    /// the cells being blind to a price laid since they were.
    /// </summary>
    [Fact]
    public void APricedUpLinkInTheWindowIsWeighedAsTheWholeGraphWeighsIt()
    {
        var (grid, search) = Searching();
        var marks = new LinkSurcharges(4);
        marks.Advance(0f);
        marks.Mark(grid.Link(2, 0, 3, 0), priceM: 1000f, forS: 10f);
        var entry = new RouteEntry(grid.Link(0, 0, 1, 0), 0f, 100f);
        var goal = new RouteGoal(grid.Link(Corners - 2, 0, Corners - 1, 0), 50f);

        search.Entries[0] = entry;
        search.Goals[0] = goal;
        search.Plan(1, 1, marks, out _);
        new RoutePlanner(grid.Graph).Plan([entry], [goal], marks, new int[MostLinks], out var wholeM, out _);

        Assert.Equal(wholeM, search.CostM, 3);
    }

    /// <summary>A goal no run of cells reaches is one no route reaches, and is refused without a fine search at all.</summary>
    [Fact]
    public void AGoalNoRunOfCellsReachesIsRefusedWithoutAFineSearch()
    {
        var builder = new TravelGraph.Builder();
        var near = builder.AddLink(50f);
        var far = builder.AddLink(50f);
        var cells = RouteCells.Lay(builder.Build(new NoTurns()), cellReachM: 25f, sectionCells: 2, aimWithinM: 10f);
        var search = new RouteSearch(cells, mostEntries: 1, mostGoals: 1, MostLinks);

        var count = Ask(search, new RouteEntry(near, 0f, 50f), new RouteGoal(far, 10f), out var goalSlot);

        Assert.Equal(0, count);
        Assert.Equal(RoutePlanner.NoGoal, goalSlot);
        Assert.Equal(0, search.SettledLinks);
    }

    /// <summary>Rule 2: a sectioned search allocates nothing, however many of them are run.</summary>
    [Fact]
    public void SectionedPlanningAllocatesNothing()
    {
        var (grid, search) = Searching();
        var entry = new RouteEntry(grid.Link(0, 0, 1, 0), 0f, 100f);
        var goal = FarGoal(grid);

        for (var warmUp = 0; warmUp < 32; warmUp++) Ask(search, entry, goal, out _);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var plan = 0; plan < 500; plan++) Ask(search, entry, goal, out _);

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    static (GridGraph Grid, RouteSearch Search) Searching()
    {
        var grid = new GridGraph(Corners, blockM: 100f);
        var cells = RouteCells.Lay(grid.Graph, cellReachM: 150f, sectionCells: 2, aimWithinM: 50f);
        return (grid, new RouteSearch(cells, mostEntries: 1, mostGoals: 1, MostLinks));
    }

    static RouteGoal FarGoal(GridGraph grid) => new(grid.Link(Corners - 2, Corners - 1, Corners - 1, Corners - 1), 50f);

    static int Ask(RouteSearch search, RouteEntry entry, RouteGoal goal, out int goalSlot)
    {
        search.Entries[0] = entry;
        search.Goals[0] = goal;
        return search.Plan(1, 1, null, out goalSlot);
    }

    readonly struct NoTurns : ITurnPricer
    {
        public float PriceM(int fromLink, int toLink) => 0f;
    }
}
