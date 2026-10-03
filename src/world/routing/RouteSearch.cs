namespace TrafficSimulation.World.Routing;

/// <summary>
/// One network's search, <b>sectioned</b>: the cells the route crosses first (<see cref="RouteCells"/>), then the
/// links of the first few of them in detail, handed over as far as the first link into the cell past them.
/// Together with the buffers a search writes into — single-threaded by the tick's own shape, so one set serves
/// every agent and a search allocates nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>What a plan costs is the window and not the trip.</b> A flood over the whole graph settles every link
/// cheaper than its answer, which for a trip across the town is half the town; a sectioned one settles the
/// cells it details and the cells' own graph, whatever the trip.
/// </para>
/// <para>
/// <b>A trip no longer than a section is searched over the whole graph, exactly</b>: what that flood settles is the
/// short reach of its own answer, and a window of the cells the cells' route crosses can miss the cheapest way round
/// a corner of them — a car sent the long way through a junction it was meant to turn at.
/// </para>
/// <para>
/// <b>A route that stops short is a route that ran out</b> (<see cref="StopsShort"/>): the body travels it and
/// asks again from its end, as it always has where its own queue filled. The section's last turn is chosen with
/// the cell past it searched as far as its core (<see cref="RouteCells.AimWithinM"/>) — the overlap — and the
/// first link into that cell is handed over with the section, so whoever asks next has ground in hand while
/// they do, and what lies past it is decided again with the section after it in view.
/// </para>
/// <para>
/// <b>The whole graph is the answer to anything the window cannot settle</b>: a window holding a closed link or a
/// priced-up one, or one the fine search found no way through even a ring wider, is searched again unsectioned.
/// So a ban is never crossed (SRV-10), a way given up on is weighed against everything round it, and a route that
/// exists is never refused for the cells it was looked for in. A goal no run of cells reaches is refused without
/// a fine search, since the cells never refuse what the links allow.
/// </para>
/// <para>
/// <b>What it costs a route is measured, not argued</b>: <c>--bench census</c> follows sampled routes section by
/// section and prints how much dearer they come out than the cheapest.
/// </para>
/// </remarks>
internal sealed class RouteSearch
{
    readonly RouteCells _cells;
    readonly RoutePlanner _planner;
    readonly RoutePlanner _coarse;
    readonly RouteEntry[] _entries;
    readonly RouteGoal[] _goals;
    readonly RouteEntry[] _cellEntries;
    readonly RouteGoal[] _cellGoals;
    readonly int[] _links;
    readonly int[] _crossed;
    readonly int[] _inWindow;
    readonly int[] _windowCells;

    int _stamp;
    int _windowCount;

    public RouteSearch(RouteCells cells, int mostEntries, int mostGoals, int mostLinks)
    {
        _cells = cells;
        _planner = new RoutePlanner(cells.Graph);
        _coarse = new RoutePlanner(cells.Coarse);
        _entries = new RouteEntry[mostEntries];
        _goals = new RouteGoal[mostGoals];
        _cellEntries = new RouteEntry[mostEntries];
        _cellGoals = new RouteGoal[mostGoals];
        _links = new int[mostLinks];
        _crossed = new int[cells.CellCount + 1];
        _inWindow = new int[cells.CellCount];
        _windowCells = new int[cells.CellCount];
    }

    /// <summary>Where the search may start from, to be filled before <see cref="Plan(int, int, LinkSurcharges?, out int)"/>.</summary>
    public Span<RouteEntry> Entries => _entries;

    /// <summary>Where it may finish, to be filled before <see cref="Plan(int, int, LinkSurcharges?, out int)"/>.</summary>
    public Span<RouteGoal> Goals => _goals;

    /// <summary>The links of the last plan, valid until the next one over this search.</summary>
    public ReadOnlySpan<int> Links(int count) => _links.AsSpan(0, count);

    /// <summary>
    /// Whether the last plan stopped at the end of its section, short of the goal it names, and is to be asked
    /// again from where its route ends.
    /// </summary>
    public bool StopsShort { get; private set; }

    /// <summary>
    /// Whether the last plan was a long one searched over the whole graph, its window having left it unsettled — and
    /// not one short enough to be searched that way to begin with.
    /// </summary>
    public bool OverTheWholeGraph { get; private set; }

    /// <summary>How many links the last plan settled, both of its searches counted where it took two.</summary>
    public int SettledLinks { get; private set; }

    /// <summary>
    /// What the route the last plan returned costs: to its goal, or — stopping short — to the far end of its last
    /// link.
    /// </summary>
    public float CostM { get; private set; }

    /// <inheritdoc cref="Plan(int, int, LinkSurcharges?, ReadOnlySpan{bool}, out int)"/>
    public int Plan(int entryCount, int goalCount, LinkSurcharges? surcharges, out int goalSlot) =>
        Plan(entryCount, goalCount, surcharges, default, out goalSlot);

    /// <summary>
    /// The route from any of the entries to any of the goals, as <see cref="RoutePlanner"/> answers it — <b>or the
    /// section of it in front of the body</b> (<see cref="StopsShort"/>), in which case <paramref name="goalSlot"/>
    /// names the goal the cells lead to and the route ends short of it.
    /// </summary>
    /// <param name="closed">The links a search may not enter (SRV-10), or empty where none is closed.</param>
    public int Plan(
        int entryCount, int goalCount, LinkSurcharges? surcharges, ReadOnlySpan<bool> closed, out int goalSlot)
    {
        var entries = _entries.AsSpan(0, entryCount);
        var goals = _goals.AsSpan(0, goalCount);
        StopsShort = false;
        OverTheWholeGraph = false;
        SettledLinks = 0;
        CostM = float.PositiveInfinity;

        var crossed = CrossTheCells(entries, goals, out goalSlot);
        if (crossed == 0) return 0;

        if (crossed <= _cells.SectionCells + 1) return Exactly(entries, goals, surcharges, closed, out goalSlot);

        var aimCell = OpenTheWindow(entries);
        if (!TheWindowIsClear(surcharges, closed)) return OverTheWhole(entries, goals, surcharges, closed, out goalSlot);

        var count = InTheWindow(entries, goals, surcharges, closed, aimCell, anywhereInTheAim: false, out var costM, out var slot);
        if (count == 0)
        {
            // Asked again a ring wider and anywhere in the aim's cell, before the whole graph: what a window misses is
            // nearly always a goal come at round a block that reaches one cell further, or an aim's core that the
            // way on only passes the edge of.
            Widen();
            if (!TheWindowIsClear(surcharges, closed)) return OverTheWhole(entries, goals, surcharges, closed, out goalSlot);

            count = InTheWindow(entries, goals, surcharges, closed, aimCell, anywhereInTheAim: true, out costM, out slot);
            if (count == 0) return OverTheWhole(entries, goals, surcharges, closed, out goalSlot);
        }

        if (slot != RoutePlanner.AtTheAim)
        {
            goalSlot = slot;
            CostM = costM;
            return count;
        }

        StopsShort = true;
        count = Handed(count, aimCell);
        CostM = _planner.ReachedM(_links[count - 1]);
        return count;
    }

    /// <summary>
    /// The cells the route crosses, in the order it crosses them, written into <see cref="_crossed"/>; nought where
    /// no run of cells reaches a goal, and the goal reached named either way.
    /// </summary>
    int CrossTheCells(ReadOnlySpan<RouteEntry> entries, ReadOnlySpan<RouteGoal> goals, out int goalSlot)
    {
        for (var slot = 0; slot < entries.Length; slot++)
        {
            _cellEntries[slot] = new RouteEntry(_cells.CellOf(entries[slot].Link), 0f, 0f);
        }

        for (var slot = 0; slot < goals.Length; slot++)
        {
            _cellGoals[slot] = new RouteGoal(_cells.CellOf(goals[slot].Link), 0f);
        }

        return _coarse.Plan(
            _cellEntries.AsSpan(0, entries.Length), _cellGoals.AsSpan(0, goals.Length), null, _crossed, out _,
            out goalSlot);
    }

    /// <summary>
    /// <b>The window</b>: the cells the entries stand in, the first of the cells crossed, which are the section,
    /// and the cell past them, which is the overlap — <b>and every cell one way on from any of those</b>. Answers
    /// the overlap's cell.
    /// </summary>
    /// <remarks>
    /// <b>The ring is what a cell's own one-way links need.</b> A cell is one piece of the graph read both ways
    /// round, and not one a body can always cross in the direction it is going: a crossing walked one way may be
    /// filed in the cell on one side of the road and the crossing back in the cell on the other. A route between
    /// two cells the cells say are joined then steps through a third for a link or two, and a window of the
    /// crossed cells alone would refuse it.
    /// </remarks>
    int OpenTheWindow(ReadOnlySpan<RouteEntry> entries)
    {
        if (_stamp == int.MaxValue)
        {
            Array.Clear(_inWindow);
            _stamp = 0;
        }

        _stamp++;
        _windowCount = 0;
        foreach (var entry in entries) Window(_cells.CellOf(entry.Link));

        for (var at = 0; at <= _cells.SectionCells; at++) Window(_crossed[at]);

        Widen();
        return _crossed[_cells.SectionCells];
    }

    /// <summary>The window a ring wider: every cell one way on from any cell already in it.</summary>
    void Widen()
    {
        var before = _windowCount;
        for (var slot = 0; slot < before; slot++)
        {
            foreach (var next in _cells.Coarse.TurnsFrom(_windowCells[slot])) Window(next);
        }
    }

    int InTheWindow(
        ReadOnlySpan<RouteEntry> entries, ReadOnlySpan<RouteGoal> goals, LinkSurcharges? surcharges,
        ReadOnlySpan<bool> closed, int aimCell, bool anywhereInTheAim, out float costM, out int goalSlot)
    {
        var count = _planner.Plan(
            entries, goals, surcharges, closed, new RouteWindow(_cells, _inWindow, _stamp, aimCell, anywhereInTheAim),
            _links, out costM, out goalSlot);
        SettledLinks += _planner.SettledLinks;
        return count;
    }

    void Window(int cell)
    {
        if (_inWindow[cell] == _stamp) return;

        _inWindow[cell] = _stamp;
        _windowCells[_windowCount++] = cell;
    }

    /// <summary>
    /// Whether nothing in the window is closed or priced up — the two things the cells cannot see, and so the two
    /// a window may not be trusted to route round.
    /// </summary>
    bool TheWindowIsClear(LinkSurcharges? surcharges, ReadOnlySpan<bool> closed)
    {
        if (surcharges is not null)
        {
            for (var index = 0; index < surcharges.MayBeLive; index++)
            {
                var link = surcharges.MarkedLink(index);
                if (link != TravelGraph.NoLink && _inWindow[_cells.CellOf(link)] == _stamp) return false;
            }
        }

        if (closed.IsEmpty) return true;

        for (var slot = 0; slot < _windowCount; slot++)
        {
            foreach (var link in _cells.LinksOf(_windowCells[slot]))
            {
                if (closed[link]) return false;
            }
        }

        return true;
    }

    int OverTheWhole(
        ReadOnlySpan<RouteEntry> entries, ReadOnlySpan<RouteGoal> goals, LinkSurcharges? surcharges,
        ReadOnlySpan<bool> closed, out int goalSlot)
    {
        OverTheWholeGraph = true;
        return Exactly(entries, goals, surcharges, closed, out goalSlot);
    }

    /// <summary>The cheapest route, searched over the whole graph — the flood that is the planner's own answer.</summary>
    int Exactly(
        ReadOnlySpan<RouteEntry> entries, ReadOnlySpan<RouteGoal> goals, LinkSurcharges? surcharges,
        ReadOnlySpan<bool> closed, out int goalSlot)
    {
        var count = _planner.Plan(entries, goals, surcharges, closed, _links, out var costM, out goalSlot);
        SettledLinks += _planner.SettledLinks;
        CostM = costM;
        return count;
    }

    /// <summary>
    /// How much of a route that finished at its aim is handed over: <b>as far as the first link into the aim's cell</b>,
    /// past the section. The rest was searched to choose that link and is searched again with the next section.
    /// </summary>
    int Handed(int count, int aimCell)
    {
        for (var at = 1; at < count; at++)
        {
            if (_cells.CellOf(_links[at]) == aimCell) return at + 1;
        }

        return count;
    }
}
