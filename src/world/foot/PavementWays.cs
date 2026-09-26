using System.Numerics;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// <b>The pavement as <see cref="IWayNetwork"/></b>: every stretch's two lanes, the places they meet at and
/// the mitre each corner is turned on — the walking network read in the same words the carriageway is
/// (TER-4c.2).
/// </summary>
/// <remarks>
/// <para>
/// <b>The setbacks are the mitres'</b> (<see cref="WalkingNetwork.WalkedFromM"/>): outside the span a walk
/// covers, a stretch's own line runs on under a corner rather than under itself. <b>It is where the two
/// networks part company</b> — a carriageway lane ends at the points its movements hand over at (TER-5d)
/// and answers nought at both ends, while a pavement hands over at a point per turn and so keeps a line
/// that runs past them.
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

    public int WayOfConnector(int connector) => firstMitreWay + connector;

    public int NearestLane(Vector2 atM, out float alongM)
    {
        // Every stretch of the graph is one lane (WLK-8), so the nearest stretch is the nearest lane.
        var edge = walking.Foot.NearestEdge(atM, out var alongEdgeM);
        if (edge < 0)
        {
            alongM = 0f;
            return -1;
        }

        // The stretch's metres are not the lane's — the lane has the corners at its ends cut off and the one
        // it carries added on — so the share walked is the seed and the lane's own line is what the place is
        // projected onto, exactly as every other lane of the walk is.
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

    public int PlaceBefore(int lane) => walking.Places.Starting(lane);

    public int PlaceAfter(int lane) => walking.Places.Arriving(lane);

    public ReadOnlySpan<int> LanesArriving(int place) => walking.Places.LanesArriving(place);

    public ReadOnlySpan<int> LanesLeaving(int place) => walking.Places.LanesLeaving(place);

    /// <summary>The pavement's connectors are its mitres: the corner between the lane arrived on and the lane left for.</summary>
    public ConnectorRun ConnectorsFrom(int lane) =>
        new(walking.TurnSlotAt(lane, 0), walking.TurnsFrom(lane).Length);

    public ReadOnlySpan<ArcSeg> ConnectorArcs(int connector) => walking.JoinArcs(connector);

    public float ConnectorLengthM(int connector) => walking.JoinLengthM(connector);

    /// <summary>A mitre is the ground of the lane it leads onto, and that lane's width (WLK-8).</summary>
    public float ConnectorWidthM(int connector) => walking.LaneWidthM(walking.TurnToEdge(connector));
}
