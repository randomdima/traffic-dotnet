namespace TrafficSimulation.World.Routing;

/// <summary>
/// <b>Where a sectioned search may go</b>: the cells its section is detailed over, and the cell past them whose
/// core it reaches into before handing the section over (<see cref="RouteCells.AimWithinM"/>). The whole graph
/// where it is <c>default</c>.
/// </summary>
/// <remarks>
/// A window bans nothing a route needs: a search it leaves without an answer is asked again over the whole
/// graph (<see cref="RouteSearch"/>).
/// </remarks>
internal readonly ref struct RouteWindow
{
    readonly ReadOnlySpan<int> _cellOf;
    readonly ReadOnlySpan<float> _nearM;
    readonly ReadOnlySpan<int> _cellStamp;
    readonly int _stamp;
    readonly int _aimCell;
    readonly float _aimWithinM;

    /// <param name="cellStamp">One stamp a cell; a cell is in the window where it carries <paramref name="stamp"/>.</param>
    /// <param name="aimCell">
    /// The cell past the section whose core the search may finish in, or <see cref="RouteCells.NoCell"/> where the
    /// goal itself is in the window and nothing short of it is a place to finish.
    /// </param>
    /// <param name="anywhereInTheAim">
    /// Whether any link of that cell will do — for a window asked again because its core was out of reach.
    /// </param>
    public RouteWindow(RouteCells cells, ReadOnlySpan<int> cellStamp, int stamp, int aimCell, bool anywhereInTheAim)
    {
        _cellOf = cells.CellsOfLinks;
        _nearM = cells.NearMiddlesOfLinks;
        _cellStamp = cellStamp;
        _stamp = stamp;
        _aimCell = aimCell;
        _aimWithinM = anywhereInTheAim ? float.PositiveInfinity : cells.AimWithinM;
    }

    public bool Aims => !_cellOf.IsEmpty && _aimCell != RouteCells.NoCell;

    public bool Holds(int link) => _cellOf.IsEmpty || _cellStamp[_cellOf[link]] == _stamp;

    /// <summary>Whether entering this link is a place to finish short of every goal: the core of the cell past the section.</summary>
    public bool IsAim(int link) => _cellOf[link] == _aimCell && _nearM[link] <= _aimWithinM;
}
