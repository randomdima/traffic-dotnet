namespace TrafficSimulation.World.Routing;

/// <summary>How the cells are laid: the middles, the filing, and the prices between them. Build-time only.</summary>
internal sealed partial class RouteCells
{
    /// <summary>
    /// <b>The middles</b>: every link no middle laid before it reaches, taken in the graph's own order. Each is
    /// further than the reach from all the others, and every link is within it of one.
    /// </summary>
    static int[] Middles(TravelGraph graph, Arrivals arrivals, float reachM)
    {
        var middles = new List<int>();
        var covered = new bool[graph.LinkCount];
        var reachedM = new float[graph.LinkCount];
        var flood = new int[graph.LinkCount];
        var frontier = new PriorityQueue<int, float>();

        for (var link = 0; link < graph.LinkCount; link++)
        {
            if (covered[link]) continue;

            middles.Add(link);
            var stamp = middles.Count;
            flood[link] = stamp;
            reachedM[link] = 0f;
            frontier.Enqueue(link, 0f);
            while (frontier.TryDequeue(out var at, out var atM))
            {
                if (atM > reachedM[at]) continue;

                covered[at] = true;
                foreach (var next in graph.TurnsFrom(at)) Reach(next);
                foreach (var next in arrivals.From(at)) Reach(next);

                void Reach(int next)
                {
                    var nextM = atM + StepM(graph, at, next);
                    if (nextM > reachM || (flood[next] == stamp && nextM >= reachedM[next])) return;

                    flood[next] = stamp;
                    reachedM[next] = nextM;
                    frontier.Enqueue(next, nextM);
                }
            }
        }

        return [.. middles];
    }

    /// <summary>
    /// Every link filed under the middle nearest it — one flood from all of them at once, so a cell is one piece
    /// of the graph.
    /// </summary>
    static void File(TravelGraph graph, Arrivals arrivals, int[] middles, int[] cellOf, float[] nearM)
    {
        Array.Fill(cellOf, NoCell);
        Array.Fill(nearM, float.PositiveInfinity);
        var frontier = new PriorityQueue<int, float>();
        for (var cell = 0; cell < middles.Length; cell++)
        {
            cellOf[middles[cell]] = cell;
            nearM[middles[cell]] = 0f;
            frontier.Enqueue(middles[cell], 0f);
        }

        while (frontier.TryDequeue(out var at, out var atM))
        {
            if (atM > nearM[at]) continue;

            foreach (var next in graph.TurnsFrom(at)) Reach(next);
            foreach (var next in arrivals.From(at)) Reach(next);

            void Reach(int next)
            {
                var nextM = atM + StepM(graph, at, next);
                if (nextM >= nearM[next]) return;

                nearM[next] = nextM;
                cellOf[next] = cellOf[at];
                frontier.Enqueue(next, nextM);
            }
        }
    }

    /// <summary>
    /// The coarse graph: one way between two cells for every pair some turn crosses, priced at the cheapest of
    /// those turns middle to middle. <b>A turn priced out of reach joins nothing</b>: no search ever takes one, so
    /// neither may the cells.
    /// </summary>
    static TravelGraph Join(TravelGraph graph, int cellCount, int[] cellOf, float[] nearM)
    {
        var acrossM = new Dictionary<(int From, int To), float>();
        for (var link = 0; link < graph.LinkCount; link++)
        {
            var turns = graph.TurnsFrom(link);
            var prices = graph.TurnPricesFrom(link);
            for (var turn = 0; turn < turns.Length; turn++)
            {
                var onto = turns[turn];
                var pair = (cellOf[link], cellOf[onto]);
                if (pair.Item1 == pair.Item2 || !float.IsFinite(prices[turn])) continue;

                var priceM = nearM[link] + StepM(graph, link, onto) + prices[turn] + nearM[onto];
                if (!acrossM.TryGetValue(pair, out var knownM) || priceM < knownM) acrossM[pair] = priceM;
            }
        }

        var builder = new TravelGraph.Builder();
        for (var cell = 0; cell < cellCount; cell++) builder.AddLink(0f);

        var pairs = acrossM.Keys.ToArray();
        Array.Sort(pairs);
        foreach (var (from, to) in pairs) builder.Join(from, to);

        return builder.Build(new Across(acrossM));
    }

    /// <summary>How far it is from the middle of one link to the middle of the next, read the same both ways round.</summary>
    static float StepM(TravelGraph graph, int from, int to) => (graph.WeightM(from) + graph.WeightM(to)) * 0.5f;

    /// <summary>Every link's arrivals — the links that turn onto it — so the graph can be read the other way round.</summary>
    readonly struct Arrivals(int[] offsets, int[] from)
    {
        public ReadOnlySpan<int> From(int link) => from.AsSpan(offsets[link], offsets[link + 1] - offsets[link]);

        public static Arrivals Of(TravelGraph graph)
        {
            var offsets = new int[graph.LinkCount + 1];
            for (var link = 0; link < graph.LinkCount; link++)
            {
                foreach (var onto in graph.TurnsFrom(link)) offsets[onto + 1]++;
            }

            for (var link = 1; link <= graph.LinkCount; link++) offsets[link] += offsets[link - 1];

            var from = new int[offsets[graph.LinkCount]];
            var slot = (int[])offsets.Clone();
            for (var link = 0; link < graph.LinkCount; link++)
            {
                foreach (var onto in graph.TurnsFrom(link)) from[slot[onto]++] = link;
            }

            return new Arrivals(offsets, from);
        }
    }

    readonly struct Across(Dictionary<(int From, int To), float> acrossM) : ITurnPricer
    {
        public float PriceM(int fromLink, int toLink) => acrossM[(fromLink, toLink)];
    }
}
