using System.Numerics;
using TrafficSimulation.Core.Geometry;

namespace TrafficSimulation.World.Road;

/// <summary>
/// <b>The shape of a network a body's ground can be read off</b> (<see cref="GroundUnder"/>): lanes with
/// lines, widths and setbacks, nodes with lanes at their ends, and a movement over each node with a line of
/// its own. The town has three of these — the carriageway, the pavement and the parking bays — and <b>they
/// are one system read three times, never three systems</b> (TER-4c.2).
/// </summary>
/// <remarks>
/// <para>
/// <b>Implemented by readonly structs and taken as a generic argument</b>, so every call below is a direct
/// one and the walk allocates nothing. Taken as an interface instead it is a virtual call per way per body
/// per tick on the town's hottest path.
/// </para>
/// <para>
/// <b>Each names its own block of the town's one numbering</b> (<see cref="TownWays"/>). A network holds
/// lanes and turn slots of its own and knows where its block begins, so a walk over it comes back speaking
/// way numbers that are comparable with every other network's — which is the whole of what makes them one
/// town rather than three.
/// </para>
/// </remarks>
internal interface IWayNetwork
{
    /// <summary>The town's way number for one of this network's lanes.</summary>
    int WayOfLane(int lane);

    /// <summary>And for one of its turn slots.</summary>
    int WayOfTurn(int slot);

    /// <summary>The lane whose own line passes nearest a point, and how far along it that is.</summary>
    int NearestLane(Vector2 atM, out float alongM);

    /// <summary>The lane running back down the same stretch, or negative where there is none.</summary>
    int Reverse(int lane);

    float LaneLengthM(int lane);

    /// <summary>
    /// How wide the ground this lane is driven or walked down is — half of what the stretch carries, since
    /// a stretch carries one each way.
    /// </summary>
    float LaneWidthM(int lane);

    ReadOnlySpan<ArcSeg> ArcsOf(int lane);

    /// <summary>How far into a lane every movement onto it arrives (TER-5d).</summary>
    float JoinedAtM(int lane);

    /// <summary>And how far short of its end every movement off it leaves.</summary>
    float LeftAtM(int lane);

    /// <summary>
    /// The node this lane leaves, or negative where it leaves none — a lane may run out onto a network other
    /// than its own, and what a body standing at that end holds there is that network's walk to say.
    /// </summary>
    int FromNode(int lane);

    /// <summary>And the node it arrives at, on the same terms.</summary>
    int ToNode(int lane);

    ReadOnlySpan<int> LanesIn(int node);

    ReadOnlySpan<int> LanesOut(int node);

    /// <summary>The lanes a body on this one may leave for, at the node this one ends at.</summary>
    ReadOnlySpan<int> TurnsFrom(int lane);

    /// <summary>Where the <paramref name="turn"/>th way out of a lane stands in the town's own turn table.</summary>
    int TurnSlotAt(int lane, int turn);

    /// <summary>The line of one movement over a node, which is empty where the two lanes butt.</summary>
    ReadOnlySpan<ArcSeg> JoinArcs(int slot);

    float JoinLengthM(int slot);

    /// <summary>How much room the walk over this network needs (<see cref="GroundUnder.MostWaysUnderAPlace"/>).</summary>
    int MostWaysUnderAPlace { get; }
}

/// <summary>The carriageway as <see cref="IWayNetwork"/>: the town's lanes, its junctions and the joins across them.</summary>
internal readonly struct RoadWays(RoadGraph roads) : IWayNetwork
{
    /// <summary>The carriageway is the first block of the town's numbering, so a lane and its way are one integer.</summary>
    public int WayOfLane(int lane) => lane;

    public int WayOfTurn(int slot) => roads.LaneCount + slot;

    public int NearestLane(Vector2 atM, out float alongM) => roads.NearestLane(atM, out alongM);

    public int Reverse(int lane) => roads.LaneReverse[lane];

    public float LaneLengthM(int lane) => roads.LaneLengthM[lane];

    public float LaneWidthM(int lane) => roads.LaneWidthM[lane];

    public ReadOnlySpan<ArcSeg> ArcsOf(int lane) => roads.ArcsOf(lane);

    /// <summary>
    /// Nought at both ends: <b>a carriageway lane is cut back to the points its movements hand over at</b>
    /// (TER-5d), so its own line is the whole of what is driven and the ground past either end is the
    /// junction's. It is the other networks that hold a line running on past where they are travelled.
    /// </summary>
    public float JoinedAtM(int lane) => 0f;

    /// <inheritdoc cref="JoinedAtM"/>
    public float LeftAtM(int lane) => 0f;

    public int FromNode(int lane) => roads.LaneFromNode[lane];

    public int ToNode(int lane) => roads.LaneToNode[lane];

    public ReadOnlySpan<int> LanesIn(int node) => roads.LanesIn(node);

    public ReadOnlySpan<int> LanesOut(int node) => roads.LanesOut(node);

    public ReadOnlySpan<int> TurnsFrom(int lane) => roads.TurnsFrom(lane);

    public int TurnSlotAt(int lane, int turn) => roads.TurnSlotAt(lane, turn);

    public ReadOnlySpan<ArcSeg> JoinArcs(int slot) => roads.JoinArcs(slot);

    public float JoinLengthM(int slot) => roads.JoinLengthM(slot);

    public int MostWaysUnderAPlace =>
        GroundUnder.MostWaysUnderAPlace(roads.MostTurnsAtANode, roads.MostLanesAtANode);
}
