using System.Numerics;
using TrafficSimulation.World.Routing;

namespace TrafficSimulation.World.Town;

/// <summary>
/// The room the interface draws a whole route out of (CTL-1a): a slot for each unit the selection may
/// hold, carrying what that unit is holding itself, the rest of the way past it, and what that rest was
/// planned for.
/// </summary>
/// <remarks>
/// <para>
/// <b>Laid with the town, like everything else the frame reads.</b> A slot is planned again only when the
/// question it answers changes — which for a body under way is when its own route is planned again, since
/// what is asked of the network is the far end of the queue in hand and not where the body has got to.
/// <b>What the body is holding is not planned at all</b> and carries no question beside it: it is read off
/// the body and laid out again every frame, because it begins where the body has got to.
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
    /// How much of a route one slot may hold. <b>A bound on the work and not a figure anything reads</b>,
    /// and room enough for a whole way across the largest shipped town with a good deal to spare — a route
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
    readonly Vector2[] _held;
    readonly int[] _count;
    readonly int[] _heldCount;
    readonly Asked[] _asked;

    public SelectionPaths(int slots, TravelGraph driving, TravelGraph walking, int mostLinks)
    {
        Drive = new RouteSearch(driving, mostEntries: 1, mostGoals: 2, mostLinks);
        Walk = new RouteSearch(walking, mostEntries: 2, mostGoals: 2, mostLinks);
        _lanes = new int[slots * MostStretches];
        _points = new Vector2[slots * MostPoints];
        _held = new Vector2[slots * MostPoints];
        _count = new int[slots];
        _heldCount = new int[slots];
        _asked = new Asked[slots];
        Ways = new int[MostStretches];
    }

    /// <summary>
    /// The ways a walked route expands into before it is stationed into points — the walker's own chain
    /// (<see cref="Routing.RouteChain"/>) in the interface's room. <b>Shared and not kept</b>, one slot
    /// being planned at a time.
    /// </summary>
    public int[] Ways { get; }

    public RouteSearch Drive { get; }

    public RouteSearch Walk { get; }

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
    /// The room the points of the walk a body is <b>already holding</b> are stationed into, which is a
    /// different span from the plan laid past it. <b>Stationed again every frame and never cached</b>: it
    /// begins under the body, so the question it answers changes as the body walks — which is the car's
    /// line being drawn live while only the lanes past it are planned once.
    /// </summary>
    public Span<Vector2> HeldPointsOf(int slot) => _held.AsSpan(slot * MostPoints, MostPoints);

    public ReadOnlySpan<Vector2> HeldPoints(int slot) => _held.AsSpan(slot * MostPoints, _heldCount[slot]);

    /// <summary>How many points this slot's held walk was stationed into, which is not a cached answer and has no question beside it.</summary>
    public void Stationed(int slot, int count) => _heldCount[slot] = count;

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
