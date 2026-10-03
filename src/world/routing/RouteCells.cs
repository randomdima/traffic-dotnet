using TrafficSimulation.Core.Config;

namespace TrafficSimulation.World.Routing;

/// <summary>
/// <b>A network's links filed into cells, and the cells joined into a graph of their own</b> — the coarse
/// tier a sectioned search (<see cref="RouteSearch"/>) asks first, so that the fine search floods only the few
/// cells the route is about to cross rather than every link cheaper than the trip.
/// </summary>
/// <remarks>
/// <para>
/// <b>Grown over the graph and never laid over the ground.</b> A cell is every link nearer one middle link
/// than any other, measured along the graph as if every link ran both ways — which is what keeps a cell one
/// piece; the middles are spread so that none stands within <see cref="CellReachM"/> of another and every link
/// stands within it of one. Nothing here reads a coordinate, so the global tier still has none to read.
/// </para>
/// <para>
/// <b>Its prices are an estimate, and say only which cells the fine search may enter.</b> A way from one cell
/// into the next is priced middle to middle over the cheapest turn between them, read both ways round — which
/// prices a corridor through cells' middles near its cost and a route clipping their corners above it. Which
/// links the route takes is still the fine search's.
/// </para>
/// <para>
/// <b>The cells never refuse what the links allow</b>: every turn a search may take from a link in one cell
/// onto a link in another is a way between the two, so a goal no run of cells reaches is a goal no route
/// reaches.
/// </para>
/// </remarks>
internal sealed partial class RouteCells
{
    public const int NoCell = -1;

    readonly int[] _cellOf;
    readonly float[] _nearM;
    readonly int[] _middle;
    readonly int[] _linkOffsets;
    readonly int[] _links;

    RouteCells(
        TravelGraph graph, TravelGraph coarse, int[] cellOf, float[] nearM, int[] middle, int[] linkOffsets,
        int[] links, float cellReachM, int sectionCells, float aimWithinM)
    {
        Graph = graph;
        Coarse = coarse;
        _cellOf = cellOf;
        _nearM = nearM;
        _middle = middle;
        _linkOffsets = linkOffsets;
        _links = links;
        CellReachM = cellReachM;
        SectionCells = sectionCells;
        AimWithinM = aimWithinM;
    }

    /// <summary>The graph the cells are cut from.</summary>
    public TravelGraph Graph { get; }

    /// <summary>
    /// <b>The cells as a travel graph of their own</b>: a cell is a link of no weight, and a way into a
    /// neighbouring cell a turn priced middle to middle.
    /// </summary>
    public TravelGraph Coarse { get; }

    public int CellCount => _middle.Length;

    /// <summary>
    /// How far a cell reaches from its middle, along the graph read both ways round — which no link of it stands
    /// further than.
    /// </summary>
    public float CellReachM { get; }

    /// <summary>How many cells a search details and hands over, the one the body sets off in included.</summary>
    public int SectionCells { get; }

    /// <summary>
    /// <b>How far a cell's core reaches from its middle</b>, read both ways round: the links a way across the cell
    /// is priced through, and the ones the search past a section has to reach before it hands the section over —
    /// <b>the overlap</b>. The section's last turn is chosen with the ground beyond it searched, and that ground is
    /// searched again by the next section rather than travelled on this one's say.
    /// </summary>
    public float AimWithinM { get; }

    public int CellOf(int link) => _cellOf[link];

    /// <summary>Every link's cell, by link — what a window reads on every relaxation.</summary>
    public ReadOnlySpan<int> CellsOfLinks => _cellOf;

    /// <summary>Whether a link is in its cell's core (<see cref="AimWithinM"/>).</summary>
    public bool IsCore(int link) => _nearM[link] <= AimWithinM;

    /// <summary>How far each link stands from its cell's middle, read both ways round — what <see cref="IsCore"/> asks.</summary>
    public ReadOnlySpan<float> NearMiddlesOfLinks => _nearM;

    /// <summary>The link a cell was grown about.</summary>
    public int MiddleOf(int cell) => _middle[cell];

    public ReadOnlySpan<int> LinksOf(int cell) => _links.AsSpan(_linkOffsets[cell], _linkOffsets[cell + 1] - _linkOffsets[cell]);

    /// <summary>Cuts a graph into cells at the town's own figures.</summary>
    public static RouteCells Of(TravelGraph graph, SimConfig config) =>
        Lay(graph, config.Network.RouteCellReachM, config.Network.RouteSectionCells, config.Network.RouteAimWithinM);

    /// <summary>Cuts a graph into cells. Build-time only, and it allocates freely.</summary>
    /// <param name="cellReachM">How far a cell reaches from its middle (<see cref="CellReachM"/>).</param>
    /// <param name="sectionCells">How many cells a search details and hands over (<see cref="SectionCells"/>).</param>
    /// <param name="aimWithinM">How near the next cell's middle the overlap reaches (<see cref="AimWithinM"/>).</param>
    public static RouteCells Lay(TravelGraph graph, float cellReachM, int sectionCells, float aimWithinM)
    {
        var arrivals = Arrivals.Of(graph);
        var middles = Middles(graph, arrivals, cellReachM);
        var cellOf = new int[graph.LinkCount];
        var nearM = new float[graph.LinkCount];
        File(graph, arrivals, middles, cellOf, nearM);

        var linkOffsets = new int[middles.Length + 1];
        foreach (var cell in cellOf) linkOffsets[cell + 1]++;
        for (var cell = 1; cell <= middles.Length; cell++) linkOffsets[cell] += linkOffsets[cell - 1];

        var links = new int[graph.LinkCount];
        var slot = (int[])linkOffsets.Clone();
        for (var link = 0; link < graph.LinkCount; link++) links[slot[cellOf[link]]++] = link;

        return new RouteCells(
            graph, Join(graph, middles.Length, cellOf, nearM), cellOf, nearM, middles, linkOffsets, links, cellReachM,
            sectionCells, aimWithinM);
    }
}
