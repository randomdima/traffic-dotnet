using TrafficSimulation.CityGen;
using TrafficSimulation.Core.Config;
using TrafficSimulation.Core.Geometry;
using TrafficSimulation.World.Road;

namespace TrafficSimulation.World.Foot;

/// <summary>
/// <b>The line any one of the town's ways is travelled on, and how wide it is</b> — a lane's own arcs, a junction's
/// join, one side of a pavement or the mitre at its corner — and the zebra a way is the paint of. <b>The one place the
/// kinds are told apart</b>, because this is the lowest slice that knows all of them. It is what the atlas lays every
/// ribbon from (<see cref="RibbonAtlas"/>, TER-4c.4).
/// </summary>
/// <remarks>
/// <b>Each is the width that way is travelled at</b> and never a second figure: a lane's is the lane's, a join's the
/// narrower of the two it joins (TER-5d.1), a bay's the space it serves (<see cref="SimConfig.ParkingSpaceWidthM"/>)
/// and not the tarmac laid for it, and a footway's and a mitre's the walking lane's.
/// <para>
/// <b>A bay's is the space because the ground between two spaces is nobody's</b> (GEN-4c): a car turning into one
/// swings a corner over its neighbour's mouth, and read at the width of the tarmac that would stand it on the
/// neighbour's ground wherever a car stood there, which is every car park the town fills.
/// </para>
/// </remarks>
internal sealed class WayLines(
    RoadGraph roads, WalkingNetwork walking, TownWays ways, CrossingEdges crossings, SimConfig config) : IRibbonLines
{
    public int WayCount => ways.Count;

    public ReadOnlySpan<ArcSeg> LineOf(int way, out float widthM)
    {
        switch (ways.KindOf(way))
        {
            case WayKind.Lane:
                var lane = ways.RoadLaneOf(way);
                widthM = roads.IsABayArm(lane) ? config.ParkingSpaceWidthM : roads.LaneWidthM[lane];
                return roads.ArcsOf(lane);

            case WayKind.Connector:
                var slot = ways.RoadConnectorOf(way);
                widthM = roads.ConnectorWidthM(slot);
                return roads.ConnectorArcs(slot);

            case WayKind.Footway:
                var edge = ways.FootwayOf(way);
                widthM = walking.LaneWidthM(edge);
                return walking.LaneOf(edge);

            default:
                var mitre = ways.MitreOf(way);
                widthM = walking.LaneWidthM(walking.TurnToEdge(mitre));
                return walking.JoinArcs(mitre);
        }
    }

    /// <summary>
    /// <b>The zebra one of the town's ways is the paint of</b> — a walking lane over it, from one kerb to the
    /// other — or <see cref="RibbonMarks.NoZebra"/>.
    /// </summary>
    public int ZebraOf(int way)
    {
        if (ways.KindOf(way) != WayKind.Footway) return RibbonMarks.NoZebra;

        var crossing = crossings.CrossingOf(ways.FootwayOf(way));
        return crossing == CityPlan.NoRecord ? RibbonMarks.NoZebra : crossing;
    }

    /// <summary>Whether one of the town's ways is the paint of a zebra, walked from one kerb to the other.</summary>
    public bool IsTheCrossing(int way) => ZebraOf(way) != RibbonMarks.NoZebra;

    public bool IsDriven(int way) => ways.IsDriven(way);

    /// <summary>
    /// <b>The level one of the town's ways is travelled on</b>: a lane's road's, a connector's two lanes' where they
    /// share one (<see cref="LaneLines.ConnectorLevel"/>), and the ground for the walk, which is laid off the
    /// ground's own boundary.
    /// </summary>
    public byte LevelOf(int way) => ways.KindOf(way) switch
    {
        WayKind.Lane => roads.LaneLevel[ways.RoadLaneOf(way)],
        WayKind.Connector => roads.ConnectorLevel(ways.RoadConnectorOf(way)),
        _ => CityPlan.RoadArrays.Ground,
    };
}
