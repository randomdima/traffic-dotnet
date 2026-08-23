using System.Numerics;

namespace TrafficSimulation.World.Routing;

/// <summary>
/// Where a body joins the network: the link it is on, how far into that link it stands, and what is left
/// of the link ahead of it.
/// </summary>
/// <remarks>
/// <b>The stretch already covered is spent</b> — charging for it again lets a route that turns round at
/// the next junction look cheaper than carrying on. For a body standing still the caller offers the links
/// out of the nearest node with the whole of each ahead of it; for a body under way it offers the link it
/// is already committed to.
/// </remarks>
internal readonly record struct RouteEntry(int Link, float AlongM, float RemainingM);

/// <summary>A destination: <b>a place on a link</b>, which is what a destination always is. Never a node.</summary>
internal readonly record struct RouteGoal(int Link, float AlongM);

/// <summary>
/// One A* over an abstract weighted graph, used by both agent kinds. <b>The search state is
/// a directed link, never a node</b>, because what a turn costs depends on the way the body arrived as
/// well as the way it leaves — settle nodes and the planner quietly returns routes that are not the
/// cheapest, which is not visibly wrong, just wrong.
/// </summary>
/// <remarks>
/// <para>
/// <b>The goal is tracked apart from the frontier.</b> A link runs one way, so a destination twenty
/// metres behind is round the block and down this link again; a goal link settled cheaply on the way past
/// would otherwise never be reached again. Every relaxation into a goal link offers its own arrival,
/// whether or not that link is settled — which is also what lets the body on a link carry both of its
/// costs at once (reach the far end and carry on, or stop part-way along and be done) and lets the search
/// decide between them, rather than a caller deciding it outside and getting one of them wrong.
/// </para>
/// <para>
/// <b>Nothing here allocates.</b> The per-link state is laid once against the network and reused; a
/// generation stamp is what makes a search start clean without clearing a town's worth of arrays.
/// </para>
/// </remarks>
internal sealed class RoutePlanner
{
    readonly TravelGraph _graph;
    readonly float[] _costM;
    readonly int[] _cameFrom;
    readonly int[] _stamp;
    readonly int[] _heapAt;
    readonly int[] _heapLink;
    readonly float[] _heapKeyM;

    int _generation;
    int _heapCount;

    public RoutePlanner(TravelGraph graph)
    {
        _graph = graph;
        _costM = new float[graph.LinkCount];
        _cameFrom = new int[graph.LinkCount];
        _stamp = new int[graph.LinkCount];
        _heapAt = new int[graph.LinkCount];
        _heapLink = new int[graph.LinkCount];
        _heapKeyM = new float[graph.LinkCount];
    }

    /// <summary>How many links the search settled, for whoever is measuring what a route costs to find.</summary>
    public int SettledLinks { get; private set; }

    /// <summary>
    /// The cheapest chain of links from any of <paramref name="entries"/> to any of
    /// <paramref name="goals"/>, written into <paramref name="intoLinks"/> in the order it is travelled.
    /// Returns how many were written, or zero where there is no route or the chain will not fit.
    /// </summary>
    /// <param name="goalPointM">
    /// Where the destination actually stands, which is what bounds the search. It has to be the place the
    /// goals describe: the heuristic is the straight line to it, and a point somewhere else is a bound
    /// that is not admissible.
    /// </param>
    /// <param name="surcharges">What the town has priced up since the network was laid, or nothing.</param>
    public int Plan(
        ReadOnlySpan<RouteEntry> entries, ReadOnlySpan<RouteGoal> goals, Vector2 goalPointM,
        LinkSurcharges? surcharges, Span<int> intoLinks, out float costM, out int goalSlot)
    {
        _generation++;
        _heapCount = 0;
        SettledLinks = 0;
        costM = float.PositiveInfinity;
        goalSlot = -1;

        var bestPred = TravelGraph.NoLink;

        foreach (var entry in entries)
        {
            Touch(entry.Link);
            if (entry.RemainingM < _costM[entry.Link])
            {
                _costM[entry.Link] = entry.RemainingM;
                _cameFrom[entry.Link] = TravelGraph.NoLink;
                Push(entry.Link, entry.RemainingM + Heuristic(entry.Link, goalPointM));
            }

            // The goal on the link the body is already committed to, and only where it is still ahead:
            // one behind is round the block, which is what the search is for.
            for (var slot = 0; slot < goals.Length; slot++)
            {
                if (goals[slot].Link != entry.Link || goals[slot].AlongM < entry.AlongM) continue;

                var directM = goals[slot].AlongM - entry.AlongM;
                if (directM >= costM) continue;

                costM = directM;
                bestPred = TravelGraph.NoLink;
                goalSlot = slot;
            }
        }

        while (_heapCount > 0)
        {
            var boundM = _heapKeyM[0];
            if (boundM >= costM) break;

            var link = Pop();
            SettledLinks++;

            var turns = _graph.TurnsFrom(link);
            var prices = _graph.TurnPricesFrom(link);
            for (var turn = 0; turn < turns.Length; turn++)
            {
                var onto = turns[turn];
                var enterM = prices[turn] + (surcharges?.PriceM(onto) ?? 0f);

                for (var slot = 0; slot < goals.Length; slot++)
                {
                    if (goals[slot].Link != onto) continue;

                    // What a link costs as the *last* one is not its weight: the route stops part-way
                    // along it and pays for the run into the destination and no more. Charging the whole
                    // link makes the two directions of one stretch cost the same, and the search then has
                    // no reason to prefer the one that reaches the destination first.
                    var arriveM = _costM[link] + enterM + goals[slot].AlongM;
                    if (arriveM >= costM) continue;

                    costM = arriveM;
                    bestPred = link;
                    goalSlot = slot;
                }

                var throughM = _costM[link] + enterM + _graph.WeightM(onto);
                Touch(onto);
                if (throughM >= _costM[onto]) continue;

                _costM[onto] = throughM;
                _cameFrom[onto] = link;
                Push(onto, throughM + Heuristic(onto, goalPointM));
            }
        }

        if (goalSlot < 0) return 0;

        var length = 1;
        for (var back = bestPred; back != TravelGraph.NoLink; back = _cameFrom[back]) length++;
        if (length > intoLinks.Length)
        {
            costM = float.PositiveInfinity;
            goalSlot = -1;
            return 0;
        }

        intoLinks[length - 1] = goals[goalSlot].Link;
        var write = length - 2;
        for (var back = bestPred; back != TravelGraph.NoLink; back = _cameFrom[back]) intoLinks[write--] = back;

        return length;
    }

    /// <summary>
    /// The straight line from where this link arrives to where the destination stands. Admissible because
    /// no link is priced below the span between its own two anchors (<see cref="TravelGraph.Builder"/>),
    /// so no continuation can undercut it.
    /// </summary>
    float Heuristic(int link, Vector2 goalPointM) => (_graph.EndAnchorM(link) - goalPointM).Length();

    void Touch(int link)
    {
        if (_stamp[link] == _generation) return;

        _stamp[link] = _generation;
        _costM[link] = float.PositiveInfinity;
        _cameFrom[link] = TravelGraph.NoLink;
        _heapAt[link] = NotInHeap;
    }

    void Push(int link, float keyM)
    {
        var at = _heapAt[link];
        if (at == NotInHeap)
        {
            at = _heapCount++;
            _heapLink[at] = link;
        }

        _heapKeyM[at] = keyM;
        _heapAt[link] = at;
        SiftUp(at);
    }

    int Pop()
    {
        var link = _heapLink[0];
        _heapAt[link] = NotInHeap;

        var last = --_heapCount;
        if (last > 0)
        {
            _heapLink[0] = _heapLink[last];
            _heapKeyM[0] = _heapKeyM[last];
            _heapAt[_heapLink[0]] = 0;
            SiftDown(0);
        }

        return link;
    }

    void SiftUp(int at)
    {
        while (at > 0)
        {
            var parent = (at - 1) / 2;
            if (_heapKeyM[parent] <= _heapKeyM[at]) break;

            Swap(at, parent);
            at = parent;
        }
    }

    void SiftDown(int at)
    {
        while (true)
        {
            var left = at * 2 + 1;
            if (left >= _heapCount) break;

            var smallest = left + 1 < _heapCount && _heapKeyM[left + 1] < _heapKeyM[left] ? left + 1 : left;
            if (_heapKeyM[at] <= _heapKeyM[smallest]) break;

            Swap(at, smallest);
            at = smallest;
        }
    }

    void Swap(int left, int right)
    {
        (_heapLink[left], _heapLink[right]) = (_heapLink[right], _heapLink[left]);
        (_heapKeyM[left], _heapKeyM[right]) = (_heapKeyM[right], _heapKeyM[left]);
        _heapAt[_heapLink[left]] = left;
        _heapAt[_heapLink[right]] = right;
    }

    const int NotInHeap = -1;
}
