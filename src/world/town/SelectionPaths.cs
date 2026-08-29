using System.Numerics;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// The room the interface plans a whole path into (CTL-1a): a slot for each unit the selection may hold,
/// carrying the rest of the way past what that unit is holding itself, and what it was planned for.
/// </summary>
/// <remarks>
/// <para>
/// <b>Laid with the town, like everything else the frame reads.</b> A slot is planned again only when the
/// question it answers changes — which for a body under way is when its own route is planned again, since
/// what is asked of the network is the far end of the queue in hand and not where the body has got to.
/// </para>
/// <para>
/// <b>Its own searches, and not the tick's.</b> The two the drive and the walk use hold the links of the
/// last plan over them, and a frame that planned through those would be answering a question by
/// overwriting the answer to the one the tick is still holding.
/// </para>
/// </remarks>
internal sealed class SelectionPaths
{
    /// <summary>
    /// How much of a path one slot may hold. <b>A bound on the work and not a figure anything reads</b>,
    /// and room enough for a whole way across the largest shipped town with a good deal to spare — a path
    /// longer than this is drawn as far as it goes.
    /// </summary>
    /// <remarks>
    /// A route measures in lanes and is short: a way across a city is a few dozen of them, since a lane is
    /// a whole block. A walk measures in the points its line is stationed at, which is the pavement cut far
    /// finer, and the same distance is a couple of thousand of them.
    /// </remarks>
    public const int MostStretches = 512;

    public const int MostPoints = 4096;

    readonly int[] _lanes;
    readonly Vector2[] _points;
    readonly int[] _count;
    readonly Asked[] _asked;

    public SelectionPaths(int slots, TravelGraph driving, TravelGraph walking, int mostLinks)
    {
        Drive = new RouteSearch(driving, mostEntries: 1, mostGoals: 2, mostLinks);
        Walk = new RouteSearch(walking, mostEntries: 2, mostGoals: 2, mostLinks);
        _lanes = new int[slots * MostStretches];
        _points = new Vector2[slots * MostPoints];
        _count = new int[slots];
        _asked = new Asked[slots];
        Crossing = new int[MostPoints];
        Way = new int[MostPoints];
        AlongM = new float[MostPoints];
    }

    public RouteSearch Drive { get; }

    public RouteSearch Walk { get; }

    /// <summary>
    /// What a walked line is laid alongside its points — which crossing each stands on, which way of the
    /// pavement, and how far along it. <b>Shared and not kept</b>: the interface draws the points and
    /// nothing else, and the line is laid one slot at a time.
    /// </summary>
    public int[] Crossing { get; }

    public int[] Way { get; }

    public float[] AlongM { get; }

    public int Slots => _count.Length;

    /// <summary>
    /// Whether this slot already holds the answer to exactly this question — including where the answer
    /// was that there is nothing to draw, which is a search not worth running again every frame. A slot
    /// nothing has been planned into holds <see cref="SelectionKind.None"/> and matches no unit.
    /// </summary>
    public bool Holds(int slot, in Asked asked) => _asked[slot].SameAs(asked);

    public void Held(int slot, in Asked asked, int count)
    {
        _asked[slot] = asked;
        _count[slot] = count;
    }

    public Span<int> LanesOf(int slot) => _lanes.AsSpan(slot * MostStretches, MostStretches);

    public Span<Vector2> PointsOf(int slot) => _points.AsSpan(slot * MostPoints, MostPoints);

    public ReadOnlySpan<int> LanesHeld(int slot) => _lanes.AsSpan(slot * MostStretches, _count[slot]);

    public ReadOnlySpan<Vector2> PointsHeld(int slot) => _points.AsSpan(slot * MostPoints, _count[slot]);

    /// <summary>
    /// What a slot's path was planned for: the unit, where the path it is drawn past ends — the last lane
    /// of a route or the last point of a line — and where the unit is going, as both the place and the bay
    /// a leg may be aimed at. <b>All of it, so that a plan is asked for again when any of it moves and
    /// never otherwise</b>: none of it changes as the body drives along what it is already holding.
    /// </summary>
    public readonly record struct Asked(
        SelectionKind Kind, int Unit, int FromLane, Vector2 FromM, Vector2 GoalM, int Bay)
    {
        public bool SameAs(in Asked other) =>
            Kind == other.Kind && Unit == other.Unit && FromLane == other.FromLane && FromM == other.FromM
            && GoalM == other.GoalM && Bay == other.Bay;
    }
}
