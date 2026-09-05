using System.Numerics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// <b>The pavement as <see cref="IWayNetwork"/></b>: every stretch's two lanes, the nodes they meet at and
/// the mitre each corner is turned on — the walking network read in the same words the carriageway is, so
/// that one walk lays a body onto whichever of them it is standing on (TER-4c.2,
/// <see cref="GroundUnder"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The setbacks are the mitres'</b> (<see cref="WalkingNetwork.WalkedFromM"/>): outside the span a walk
/// covers, a lane's own line runs on under a corner rather than under itself, exactly as a lane's does into a
/// junction — so the two networks answer TER-5d with the same two figures.
/// </para>
/// <para>
/// <b>Its two blocks come last in the town's numbering</b> (<see cref="TownWays"/>) and it is told where
/// they begin rather than working it out, because where they begin is a fact about every other network and
/// not about this one.
/// </para>
/// </remarks>
internal readonly struct PavementWays(WalkingNetwork walking, int firstFootwayWay, int firstMitreWay)
    : IWayNetwork
{
    public int WayOfLane(int lane) => firstFootwayWay + lane;

    public int WayOfTurn(int slot) => firstMitreWay + slot;

    public int NearestLane(Vector2 atM, out float alongM)
    {
        // The graph's own line is the middle of the band and a lane is half a band to one side of it, so the
        // stretch is what is searched for and the two lanes are read off it. Which of them the place is on
        // is not decided here: both are written, and the band each is in is what settles it.
        var edge = walking.Foot.NearestEdge(atM, out var alongEdgeM);
        if (edge < 0)
        {
            alongM = 0f;
            return -1;
        }

        // The stretch's metres are not the lane's — a lane offset out of a bend is longer than the kerb
        // inside it — so the share walked is the seed and the lane's own line is what the place is projected
        // onto, exactly as every other lane of the walk is.
        var edgeLengthM = MathF.Max(1e-4f, walking.Foot.LengthM(edge));
        var laneLengthM = walking.LaneLengthM(edge);
        var seedM = alongEdgeM / edgeLengthM * laneLengthM;
        alongM = Spline.ProjectM(walking.LaneOf(edge), atM, seedM, laneLengthM);
        return edge;
    }

    public int Reverse(int lane) => walking.Foot.Reverse(lane);

    public float LaneLengthM(int lane) => walking.LaneLengthM(lane);

    public float LaneWidthM(int lane) => walking.LaneWidthM(lane);

    public ReadOnlySpan<ArcSeg> ArcsOf(int lane) => walking.LaneOf(lane);

    public float JoinedAtM(int lane) => walking.WalkedFromM(lane);

    public float LeftAtM(int lane) => MathF.Max(0f, walking.LaneLengthM(lane) - walking.WalkedToM(lane));

    public int FromNode(int lane) => walking.Foot.FromNode(lane);

    public int ToNode(int lane) => walking.Foot.ToNode(lane);

    public ReadOnlySpan<int> LanesIn(int node) => walking.Foot.EdgesIn(node);

    public ReadOnlySpan<int> LanesOut(int node) => walking.Foot.EdgesOut(node);

    public ReadOnlySpan<int> TurnsFrom(int lane) => walking.TurnsFrom(lane);

    public int TurnSlotAt(int lane, int turn) => walking.TurnSlotAt(lane, turn);

    public ReadOnlySpan<ArcSeg> JoinArcs(int slot) => walking.JoinArcs(slot);

    public float JoinLengthM(int slot) => walking.JoinLengthM(slot);

    public int MostWaysUnderAPlace =>
        GroundUnder.MostWaysUnderAPlace(walking.MostTurnsAtANode, walking.MostLanesAtANode);

    /// <summary>
    /// <b>Whether a stretch is paint over a carriageway rather than ground the walk has to itself.</b> The
    /// ground under a crossing has two names and one owner (TER-5c.1): what holds a walker off a car there is
    /// that car's stretch of the <em>lane</em>, looked up where the crossing runs over it. Written here as
    /// well, one car would hold one piece of ground twice, under two claims whose answers could differ.
    /// </summary>
    public bool IsACrossing(int lane) => walking.Foot.KindOf(lane) == FootEdgeKind.Crossing;

    /// <summary>The stretch a mitre leads onto, which is whose ground the corner is.</summary>
    public int TurnToLane(int slot) => walking.TurnToEdge(slot);
}
