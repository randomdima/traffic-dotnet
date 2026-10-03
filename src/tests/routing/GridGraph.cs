using TrafficSimulation.World.Routing;

namespace TrafficSimulation.Tests.Routing;

/// <summary>
/// A square grid of blocks laid as a travel graph: one link each way down every side of every block, and a
/// free turn from each link onto every link leaving the corner it arrives at except its own way back.
/// </summary>
internal sealed class GridGraph
{
    readonly Dictionary<(int FromX, int FromY, int ToX, int ToY), int> _links = [];

    public GridGraph(int corners, float blockM)
    {
        var builder = new TravelGraph.Builder();
        for (var y = 0; y < corners; y++)
        {
            for (var x = 0; x < corners; x++)
            {
                if (x + 1 < corners) Both(x, y, x + 1, y);
                if (y + 1 < corners) Both(x, y, x, y + 1);
            }
        }

        foreach (var ((fromX, fromY, toX, toY), link) in _links)
        {
            foreach (var ((nextFromX, nextFromY, nextToX, nextToY), next) in _links)
            {
                var leavesWhereThisArrives = nextFromX == toX && nextFromY == toY;
                var isTheWayBack = nextToX == fromX && nextToY == fromY;
                if (leavesWhereThisArrives && !isTheWayBack) builder.Join(link, next);
            }
        }

        Graph = builder.Build(new FreeTurns());

        void Both(int x0, int y0, int x1, int y1)
        {
            _links[(x0, y0, x1, y1)] = builder.AddLink(blockM);
            _links[(x1, y1, x0, y0)] = builder.AddLink(blockM);
        }
    }

    public TravelGraph Graph { get; }

    /// <summary>The link down one side of a block, from one corner to the next.</summary>
    public int Link(int fromX, int fromY, int toX, int toY) => _links[(fromX, fromY, toX, toY)];

    /// <summary>The links that turn onto this one, which the graph itself does not keep.</summary>
    public IEnumerable<int> Arriving(int link)
    {
        for (var from = 0; from < Graph.LinkCount; from++)
        {
            if (Graph.TurnsFrom(from).Contains(link)) yield return from;
        }
    }

    readonly struct FreeTurns : ITurnPricer
    {
        public float PriceM(int fromLink, int toLink) => 0f;
    }
}
