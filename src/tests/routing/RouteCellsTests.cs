using TrafficSimulation.World.Routing;
using Xunit;

namespace TrafficSimulation.Tests.Routing;

/// <summary>The cells a sectioned search crosses before it details any of them, cut from a hand-laid grid.</summary>
[Trait(Tier.Key, Tier.Unit)]
[Trait(Priority.Key, Priority.P4)]
public class RouteCellsTests
{
    /// <summary>
    /// <b>A cell is one piece of the graph</b>: every link of it is reached from its middle without leaving it,
    /// read both ways round — which is what lets a window of whole cells hold a route across one.
    /// </summary>
    [Fact]
    public void ACellIsOnePieceOfTheGraph()
    {
        var grid = new GridGraph(corners: 8, blockM: 100f);
        var cells = RouteCells.Lay(grid.Graph, cellReachM: 250f, sectionCells: 2, aimWithinM: 50f);

        for (var cell = 0; cell < cells.CellCount; cell++)
        {
            var reached = new HashSet<int> { cells.MiddleOf(cell) };
            var frontier = new Queue<int>(reached);
            while (frontier.TryDequeue(out var link))
            {
                foreach (var next in grid.Graph.TurnsFrom(link).ToArray().Concat(grid.Arriving(link)))
                {
                    if (cells.CellOf(next) == cell && reached.Add(next)) frontier.Enqueue(next);
                }
            }

            Assert.Equal(cells.LinksOf(cell).ToArray().Order(), reached.Order());
        }
    }
}
